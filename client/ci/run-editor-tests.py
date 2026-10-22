"""Run EditMode or PlayMode tests in the connected Unity Editor in one run.

The named classes (or the whole test assembly) run through Baryonyx.Editor.CI.EditorTestRun, which
saves the open scenes and starts the run in the same eval, so Unity never asks to save a scene.
The script checks that tests were found, reloads the scripts and retries once when none were,
waits for the result file, and compares each class with the tests it contains.
The Editor lock is held throughout.
"""

import argparse
import importlib.util
import json
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

ASSEMBLIES = {"editmode": "Baryonyx.EditModeTests", "playmode": "Baryonyx.PlayModeTests"}
CLASS_NAME = re.compile(r"\w+")
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


def group_pattern(classes):
    """A regex for Filter.groupNames; the dots on both sides keep TopHomeSceneTests out of HomeSceneTests."""
    if not classes:
        return ""
    return r"^Baryonyx\.Tests\.(?:\w+\.)*(?:" + "|".join(classes) + r")\."


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
        return {name: sum(1 for test in listed if class_of(test["FullName"]) == name) for name in classes}

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
        groups = {name: [test for test in tests if class_of(test["name"]) == name] for name in classes}
        if not classes:
            groups = {ASSEMBLIES[mode]: tests}
        all_ok = sum(len(group) for group in groups.values()) == result["total"]
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
    parser.add_argument("classes", nargs="*", help="test class names, e.g. HomeSceneTests; none runs the whole assembly")
    parser.add_argument("--mode", choices=sorted(ASSEMBLIES), default="playmode", help="test mode (default: playmode)")
    parser.add_argument("--owner", default="editor tests", help="name shown to other chats waiting for the Editor")
    parser.add_argument("--token", help="an Editor lock token this chat already holds; it is renewed, not released")
    parser.add_argument("--lock-wait", type=float, default=DEFAULT_LOCK_WAIT, help="seconds to wait for the Editor")
    parser.add_argument("--timeout", type=int, default=DEFAULT_TIMEOUT, help="seconds allowed for the run")
    args = parser.parse_args(argv)
    runner = runner or Runner()
    lock_file = lock_file or editor_lock.LOCK_FILE

    malformed = [name for name in args.classes if not CLASS_NAME.fullmatch(name)]
    if malformed:
        runner.out(f"not a class name: {', '.join(malformed)} (use class names without the namespace)")
        return EXIT_USAGE
    try:
        expected = runner.expected_counts(args.mode, args.classes)
    except EditorTrouble as trouble:
        runner.out(f"editor: {trouble}")
        return EXIT_EDITOR
    unknown = [name for name, count in expected.items() if count == 0]
    if unknown:
        runner.out(f"no {args.mode} tests in: {', '.join(unknown)} (use class names without the namespace)")
        return EXIT_USAGE

    token = args.token
    if token is None:
        try:
            token = editor_lock.acquire(lock_file, args.owner, args.lock_wait, editor_lock.DEFAULT_LEASE_SECONDS)
        except editor_lock.Busy as busy:
            runner.out(f"busy: {editor_lock.describe(busy.lock, time.time())}")
            return EXIT_BUSY

    def renew():
        editor_lock.renew(lock_file, token, editor_lock.DEFAULT_LEASE_SECONDS)

    try:
        renew()
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
        if args.token is None:
            try:
                editor_lock.release(lock_file, token)
            except editor_lock.NotHeld:
                pass


if __name__ == "__main__":
    sys.exit(main())
