"""Run EditMode or PlayMode tests in the connected Unity Editor in one run.

The named classes or tests (Class or Class.Method; none for the whole test assembly) run through
Baryonyx.Editor.CI.EditorTestRun, which saves the open scenes and starts the run in the same eval,
so Unity never asks to save a scene. With --recompile the scripts are compiled first and the run
stops at the first compile errors; --compile-only does only that. The script checks that tests
were found, reloads the scripts and retries once when none were, waits for the result file, and
compares each class or test with what it contains. Only a summary is printed; the failed tests and
the compile errors are listed, never the log. The Editor lock is held throughout.
"""

import argparse
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import uuid


CLIENT_DIRECTORY = Path(__file__).resolve().parents[1]
EDITOR_LOG = CLIENT_DIRECTORY / "Logs" / "Editor.log"
RESULT_FILE = CLIENT_DIRECTORY / "Temp" / "editor-test-run.json"
LOCK_SCRIPT = Path(__file__).resolve().parent / "editor-lock.py"
POLL_SECONDS = 2
START_SECONDS = 20
READY_SECONDS = 120
RENEW_SECONDS = 60
EVAL_TRIES = 3
DEFAULT_TIMEOUT = 600
DEFAULT_LOCK_WAIT = 600
EXIT_FAILED = 1
EXIT_USAGE = 2
EXIT_BUSY = 3
EXIT_EDITOR = 5
EXIT_COMPILE = 6
COMPILE_SECONDS = 300
COMPILE_ERRORS_SHOWN = 20
# recompile_status once nothing is left to compile ("up_to_date" when there was nothing to do).
COMPILE_DONE = {"completed", "up_to_date"}

ASSEMBLIES = {"editmode": "Baryonyx.EditModeTests", "playmode": "Baryonyx.PlayModeTests"}
# A class, or a class and one of its tests. Every test class ends in Tests, which keeps a
# namespaced class (PlayMode.HomeSceneTests) out.
SELECTOR = re.compile(r"\w+|\w+Tests\.\w+")
COMPILE_ERROR = re.compile(r"^.*error CS\d{4}:.*$", re.MULTILINE)
RELOADING = "Begin MonoManager ReloadAssembly"
RELOAD_SCRIPTS = 'UnityEditor.EditorUtility.RequestScriptReload(); return "reload";'
CANCEL_RUN = "return Baryonyx.Editor.CI.EditorTestRun.Cancel();"

spec = importlib.util.spec_from_file_location("editor_lock", LOCK_SCRIPT)
editor_lock = importlib.util.module_from_spec(spec)
spec.loader.exec_module(editor_lock)


class EditorTrouble(Exception):
    """The Editor did not reach the expected state; later results would not be trustworthy."""


def unity(name, **parameters):
    command = ["unity", "--format", "json", "command", "--caller", "plugin", "--skill", "unity-cli", name]
    for key, value in parameters.items():
        command += [f"--{key}", value if isinstance(value, str) else json.dumps(value)]
    completed = subprocess.run(command, capture_output=True, text=True, cwd=CLIENT_DIRECTORY)
    try:
        reply = json.loads(completed.stdout)
    except ValueError:
        raise EditorTrouble(f"{name}: no JSON reply ({completed.stderr.strip() or completed.stdout.strip()})")
    if not reply.get("success") or not reply["data"].get("success", True):
        raise EditorTrouble(f"{name}: {reply.get('errors') or reply['data']}")
    result = reply["data"].get("result")
    if isinstance(result, str):
        try:
            return json.loads(result)
        except ValueError:
            return result
    return result


def class_of(full_name):
    """The class segment of a test's full name, e.g. HomeSceneTests for ...PlayMode.HomeSceneTests.Opens."""
    return full_name.split("(")[0].split(".")[-2]


def method_of(full_name):
    """The test segment of a test's full name, without its arguments, e.g. Misses for ....Misses(2)."""
    return full_name.split("(")[0].split(".")[-1]


def selects(selector, full_name):
    """Whether a selector (Class or Class.Method) picks the test with this full name."""
    owner, _, method = selector.partition(".")
    return class_of(full_name) == owner and (not method or method_of(full_name) == method)


def group_pattern(selectors):
    """A regex for Filter.groupNames; the dots on both sides keep TopHomeSceneTests out of HomeSceneTests,
    and a test's name ends at its arguments or the end, so Opens does not pick OpensTwice."""
    if not selectors:
        return ""
    parts = []
    for selector in selectors:
        owner, _, method = selector.partition(".")
        parts.append(owner + r"\." + (method + r"(?:\(|$)" if method else ""))
    return r"^Baryonyx\.Tests\.(?:\w+\.)*(?:" + "|".join(parts) + ")"


def start_code(run_id, mode, classes):
    arguments = ", ".join(f'@"{value}"' for value in (run_id, mode, ASSEMBLIES[mode], group_pattern(classes)))
    return f"return Baryonyx.Editor.CI.EditorTestRun.Start({arguments});"


class Runner:
    def __init__(
        self, command=unity, log=EDITOR_LOG, result_file=RESULT_FILE, now=time.time, sleep=time.sleep, out=print
    ):
        self.command = command
        self.log = log
        self.result_file = result_file
        self.now = now
        self.sleep = sleep
        self.out = out

    def log_size(self):
        try:
            return self.log.stat().st_size
        except FileNotFoundError:
            return 0

    def log_since(self, offset):
        try:
            with self.log.open("rb") as stream:
                stream.seek(offset)
                return stream.read().decode("utf-8", errors="replace")
        except FileNotFoundError:
            return ""

    def result(self, run_id):
        """The result file of this run, or None while it holds another run or is being replaced."""
        try:
            data = json.loads(self.result_file.read_text(encoding="utf-8"))
        except (FileNotFoundError, ValueError):
            return None
        return data if data.get("id") == run_id else None

    def expected_counts(self, mode, classes):
        """Tests per class, or per assembly when no class is named; explicit tests do not run."""
        listed = [
            test
            for test in self.command("list_tests", mode=mode)["Tests"]
            if test.get("Assembly") == ASSEMBLIES[mode] and not test["Explicit"]
        ]
        if not classes:
            return {ASSEMBLIES[mode]: len(listed)}
        return {name: sum(1 for test in listed if selects(name, test["FullName"])) for name in classes}

    def wait_ready(self):
        deadline = self.now() + READY_SECONDS
        while True:
            try:
                status = self.command("editor_status")
            except EditorTrouble as trouble:
                # The Editor stops answering while it reloads the scripts.
                status = {"status": str(trouble)}
            if (
                status["status"] == "ready"
                and not status.get("compiling")
                and not status.get("domainReloadInProgress")
                and status.get("playMode") == "stopped"
            ):
                return
            if self.now() >= deadline:
                raise EditorTrouble(f"Editor not ready after {READY_SECONDS}s: {status}")
            self.sleep(POLL_SECONDS)

    def evaluate(self, code, timeout=30000, tries=EVAL_TRIES):
        # The reply gives up after 5 s on a busy or background Editor while the code still runs.
        for attempt in range(tries):
            try:
                reply = self.command("eval", code=code, timeout=timeout)
                break
            except EditorTrouble as trouble:
                if "timed out" not in str(trouble) or attempt == tries - 1:
                    raise
                self.wait_ready()
        if not reply.get("success"):
            raise EditorTrouble(f"eval failed: {reply.get('diagnostics') or reply}")
        return reply.get("result")

    def prepare(self):
        if self.command("editor_status")["playMode"] != "stopped":
            self.command("editor_stop")
        self.wait_ready()

    def start(self, run_id, mode, classes):
        """Save the scenes and start the run in one eval. Sending it twice would start two runs,
        so after a timed-out reply the result file tells whether the first one went through."""
        code = start_code(run_id, mode, classes)
        try:
            reply = self.evaluate(code, tries=1)
        except EditorTrouble as trouble:
            if "timed out" not in str(trouble):
                raise
            self.wait_ready()
            if self.result(run_id) is not None:
                return
            reply = self.evaluate(code, tries=1)
        if reply != "started":
            raise EditorTrouble(f"the run did not start: {reply}")

    def recompile(self):
        """Compile the scripts and wait until the Editor is ready again. Returns the compile errors
        (each line once, at most COMPILE_ERRORS_SHOWN), or an empty list."""
        offset = self.log_size()
        self.prepare()
        self.command("recompile")
        # Right after the request the Editor may still report ready; give it a moment to begin.
        begun_by = self.now() + START_SECONDS
        while self.now() < begun_by:
            log = self.log_since(offset)
            if "Tundra build" in log or RELOADING in log or COMPILE_ERROR.search(log):
                break
            try:
                # "completed" may still be the last compile's; anything else means this one began
                # (or found nothing to do).
                if self.command("recompile_status").get("status") != "completed":
                    break
            except EditorTrouble:
                break
            self.sleep(POLL_SECONDS)
        deadline = self.now() + COMPILE_SECONDS
        while True:
            try:
                status = self.command("recompile_status")
            except EditorTrouble as trouble:
                status = {"status": str(trouble)}
            if status.get("status") in COMPILE_DONE:
                break
            if self.now() >= deadline:
                raise EditorTrouble(f"the scripts did not compile within {COMPILE_SECONDS}s: {status}")
            self.sleep(POLL_SECONDS)
        self.wait_ready()
        errors = []
        for line in COMPILE_ERROR.findall(self.log_since(offset)) + [
            error if isinstance(error, str) else json.dumps(error, ensure_ascii=False)
            for error in status.get("errors") or []
        ]:
            line = line.strip()[:300]
            if line not in errors:
                errors.append(line)
        if status.get("failed") and not errors:
            errors.append("the compile failed; see client/Logs/Editor.log")
        return errors[:COMPILE_ERRORS_SHOWN]

    def compile_step(self):
        """Recompile and print one line, or the errors. True when it compiled."""
        started = self.now()
        errors = self.recompile()
        if errors:
            self.out(f"FAIL: compile errors ({self.now() - started:.0f}s)")
            for line in errors:
                self.out(f"    {line}")
            return False
        self.out(f"compiled ({self.now() - started:.0f}s)")
        return True

    def stop_run(self):
        self.evaluate(CANCEL_RUN)
        if self.command("editor_status")["playMode"] != "stopped":
            self.command("editor_stop")

    def reload_scripts(self):
        # Right after the request the Editor still reports ready, so wait for the reload to begin.
        offset = self.log_size()
        self.evaluate(RELOAD_SCRIPTS)
        begun_by = self.now() + START_SECONDS
        while RELOADING not in self.log_since(offset) and self.now() < begun_by:
            self.sleep(POLL_SECONDS)
        self.wait_ready()

    def attempt(self, mode, classes, timeout, renew):
        """Run once. Returns the finished result, or None when no tests were found."""
        self.prepare()
        run_id = uuid.uuid4().hex
        self.start(run_id, mode, classes)
        started_by = self.now() + START_SECONDS
        finished_by = self.now() + timeout
        renew_at = self.now() + RENEW_SECONDS
        while True:
            self.sleep(POLL_SECONDS)
            if self.now() >= renew_at:
                renew()
                renew_at = self.now() + RENEW_SECONDS
            result = self.result(run_id)
            status = result["status"] if result else None
            if status == "finished":
                if result["total"] == 0:
                    self.stop_run()
                    return None
                return result
            if (status == "started" and result["started"] == 0) or (
                status != "started" and self.now() >= started_by
            ):
                self.stop_run()
                return None
            if self.now() >= finished_by:
                self.stop_run()
                raise EditorTrouble(f"the run did not finish within {timeout}s")

    def report(self, mode, classes, expected, result, elapsed):
        """Print one line per class (or for the assembly) and the tests that did not pass."""
        tests = result["tests"]
        groups = {name: [test for test in tests if selects(name, test["name"])] for name in classes}
        if not classes:
            groups = {ASSEMBLIES[mode]: tests}
        # A class and one of its tests may both be named; count each test once.
        all_ok = len({test["name"] for group in groups.values() for test in group}) == result["total"]
        for name, group in groups.items():
            passed = sum(test["status"] == "Passed" for test in group)
            failed = sum(test["status"] == "Failed" for test in group)
            ok = len(group) == expected[name] and passed == expected[name]
            all_ok = all_ok and ok
            self.out(
                f"{'PASS' if ok else 'FAIL'} {name}: {passed}/{len(group)} passed, {failed} failed, "
                f"{expected[name]} expected"
            )
            for test in group:
                if test["status"] != "Passed":
                    message = (test.get("message") or "").strip().splitlines()
                    self.out(f"    {test['status']}: {test['name']}" + (f" ({message[0]})" if message else ""))
        self.out(
            f"{result['passed']}/{result['total']} passed, {result['failed']} failed ({elapsed:.0f}s)"
        )
        return all_ok

    def run(self, mode, classes, expected, timeout, renew=lambda: None):
        started = self.now()
        result = self.attempt(mode, classes, timeout, renew)
        if result is None:
            self.out("no tests found; reloading scripts and retrying once")
            self.reload_scripts()
            renew()
            result = self.attempt(mode, classes, timeout, renew)
        elapsed = self.now() - started
        if result is None:
            self.out(f"FAIL: still no tests found after the reload ({elapsed:.0f}s)")
            return False
        return self.report(mode, classes, expected, result, elapsed)


def main(argv=None, runner=None, lock_file=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "classes",
        nargs="*",
        help="test classes or tests, e.g. HomeSceneTests or HomeSceneTests.Opens; none runs the whole assembly",
    )
    parser.add_argument("--mode", choices=sorted(ASSEMBLIES), default="playmode", help="test mode (default: playmode)")
    parser.add_argument("--owner", default="editor tests", help="name shown to other chats waiting for the Editor")
    parser.add_argument("--token", help="an Editor lock token this chat already holds; it is renewed, not released")
    parser.add_argument("--lock-wait", type=float, default=DEFAULT_LOCK_WAIT, help="seconds to wait for the Editor")
    parser.add_argument("--timeout", type=int, default=DEFAULT_TIMEOUT, help="seconds allowed for the run")
    parser.add_argument("--recompile", action="store_true", help="compile the scripts first; stop at compile errors")
    parser.add_argument("--compile-only", action="store_true", help="compile the scripts and run no tests")
    args = parser.parse_args(argv)
    runner = runner or Runner()
    lock_file = lock_file or editor_lock.LOCK_FILE

    malformed = [name for name in args.classes if not SELECTOR.fullmatch(name)]
    if malformed:
        runner.out(
            f"not a class or test name: {', '.join(malformed)} (use Class or Class.Method without the namespace)"
        )
        return EXIT_USAGE
    compile_first = args.recompile or args.compile_only

    def expected_tests():
        """The tests per class, or None after printing why the names do not run."""
        expected = runner.expected_counts(args.mode, args.classes)
        unknown = [name for name, count in expected.items() if count == 0]
        if unknown:
            runner.out(
                f"no {args.mode} tests in: {', '.join(unknown)} (use Class or Class.Method without the namespace)"
            )
            return None
        return expected

    # Without a compile the names are checked before the Editor is taken; after one they can only
    # be checked once the new tests are compiled.
    expected = None
    if not compile_first:
        try:
            expected = expected_tests()
        except EditorTrouble as trouble:
            runner.out(f"editor: {trouble}")
            return EXIT_EDITOR
        if expected is None:
            return EXIT_USAGE

    # The pid in the lock keeps the end-of-turn hook from returning the lock while this script runs.
    token = args.token
    if token is None:
        try:
            token = editor_lock.acquire(
                lock_file,
                args.owner,
                args.lock_wait,
                editor_lock.DEFAULT_LEASE_SECONDS,
                session=os.environ.get(editor_lock.SESSION_VARIABLE),
                pid=os.getpid(),
            )
        except editor_lock.Busy as busy:
            runner.out(f"busy: {editor_lock.describe(busy.lock, busy.ahead, time.time())}")
            return EXIT_BUSY

    def renew():
        editor_lock.renew(lock_file, token, editor_lock.DEFAULT_LEASE_SECONDS)

    try:
        editor_lock.renew(lock_file, token, editor_lock.DEFAULT_LEASE_SECONDS, pid=os.getpid())
        if compile_first:
            if not runner.compile_step():
                return EXIT_COMPILE
            renew()
            if args.compile_only:
                return 0
            expected = expected_tests()
            if expected is None:
                return EXIT_USAGE
        ok = runner.run(args.mode, args.classes, expected, args.timeout, renew)
        renew()
        return 0 if ok else EXIT_FAILED
    except editor_lock.NotHeld:
        runner.out("the Editor lock expired or was taken over; results after that point are not trustworthy")
        return EXIT_EDITOR
    except EditorTrouble as trouble:
        runner.out(f"editor: {trouble}")
        return EXIT_EDITOR
    finally:
        try:
            if args.token is None:
                editor_lock.release(lock_file, token)
            else:
                editor_lock.renew(lock_file, token, editor_lock.DEFAULT_LEASE_SECONDS, pid=None)
        except editor_lock.NotHeld:
            pass


if __name__ == "__main__":
    sys.exit(main())
