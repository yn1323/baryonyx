"""Run PlayMode test classes one after another in the connected Unity Editor.

For each class this saves the open scenes, starts the run, checks that tests were found,
reloads the scripts and retries once when none were, waits for the result in the Editor log,
and compares the count with the tests the class contains. The Editor lock is held throughout.
"""

import argparse
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys
import time


CLIENT_DIRECTORY = Path(__file__).resolve().parents[1]
EDITOR_LOG = CLIENT_DIRECTORY / "Logs" / "Editor.log"
LOCK_SCRIPT = Path(__file__).resolve().parent / "editor-lock.py"
POLL_SECONDS = 2
START_SECONDS = 20
READY_SECONDS = 120
EVAL_TRIES = 3
DEFAULT_CLASS_TIMEOUT = 300
DEFAULT_LOCK_WAIT = 600
EXIT_FAILED = 1
EXIT_USAGE = 2
EXIT_BUSY = 3
EXIT_EDITOR = 5

STARTED = re.compile(r"\[TestResultCollector\] Run started: (\d+) test\(s\)")
RELOADING = "Begin MonoManager ReloadAssembly"
FINISHED = re.compile(r"\[TestResultCollector\] Run finished: (\d+) total, (\d+) passed, (\d+) failed")

SAVE_SCENES = """
var untitled = 0;
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
{
    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
    if (!scene.isDirty) continue;
    if (string.IsNullOrEmpty(scene.path)) { untitled++; continue; }
    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
}
return untitled;
"""
RELOAD_SCRIPTS = 'UnityEditor.EditorUtility.RequestScriptReload(); return "reload";'

spec = importlib.util.spec_from_file_location("editor_lock", LOCK_SCRIPT)
editor_lock = importlib.util.module_from_spec(spec)
spec.loader.exec_module(editor_lock)


class EditorTrouble(Exception):
    """The Editor did not reach the expected state; later classes would not be trustworthy."""


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


class Runner:
    def __init__(self, command=unity, log=EDITOR_LOG, now=time.time, sleep=time.sleep, out=print):
        self.command = command
        self.log = log
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

    def expected_counts(self, classes):
        listed = self.command("list_tests", mode="playmode")["Tests"]
        counts = {}
        for name in classes:
            marker = f"playmode.{name}.".lower()
            counts[name] = sum(1 for test in listed if marker in test["FullName"].lower() and not test["Explicit"])
        return counts

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

    def evaluate(self, code, timeout=30000):
        # The reply gives up after 5 s on a busy or background Editor while the code still runs;
        # both snippets here are safe to send again.
        for attempt in range(EVAL_TRIES):
            try:
                reply = self.command("eval", code=code, timeout=timeout)
                break
            except EditorTrouble as trouble:
                if "timed out" not in str(trouble) or attempt == EVAL_TRIES - 1:
                    raise
                self.wait_ready()
        if not reply.get("success"):
            raise EditorTrouble(f"eval failed: {reply.get('diagnostics') or reply}")
        return reply.get("result")

    def prepare(self):
        if self.command("editor_status")["playMode"] != "stopped":
            self.command("editor_stop")
        self.wait_ready()
        untitled = self.evaluate(SAVE_SCENES)
        if untitled:
            raise EditorTrouble("an untitled scene has unsaved changes; save or discard it by hand first")

    def stop_run(self):
        try:
            self.command("cancel_tests")
        except EditorTrouble:
            pass  # Nothing was running any more.
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

    def attempt(self, name, timeout):
        """Run one class once. Returns the finished counts, or None when no tests were found."""
        self.prepare()
        offset = self.log_size()
        self.command("run_tests", mode="playmode", filter=f"PlayMode.{name}.", async_tests=True, timeout=timeout)
        started_by = self.now() + START_SECONDS
        finished_by = self.now() + timeout
        while True:
            self.sleep(POLL_SECONDS)
            text = self.log_since(offset)
            finished = FINISHED.findall(text)
            if finished:
                total, passed, failed = map(int, finished[-1])
                if total == 0:
                    self.stop_run()
                    return None
                return total, passed, failed
            started = STARTED.findall(text)
            if (started and int(started[-1]) == 0) or (not started and self.now() >= started_by):
                self.stop_run()
                return None
            if self.now() >= finished_by:
                self.stop_run()
                raise EditorTrouble(f"{name} did not finish within {timeout}s")

    def failures(self):
        status = self.command("test_status")
        if status.get("status") != "completed":
            return []
        lines = []
        for result in status.get("results", []):
            if result["Status"] != "Passed":
                message = (result.get("Message") or "").strip().splitlines()
                lines.append(f"    {result['Status']}: {result['FullName']}" + (f" ({message[0]})" if message else ""))
        return lines

    def run_class(self, name, expected, timeout):
        started = self.now()
        counts = self.attempt(name, timeout)
        if counts is None:
            self.out(f"{name}: no tests found; reloading scripts and retrying once")
            self.reload_scripts()
            counts = self.attempt(name, timeout)
        elapsed = self.now() - started
        if counts is None:
            self.out(f"FAIL {name}: still no tests found after the reload ({elapsed:.0f}s)")
            return False
        total, passed, failed = counts
        ok = total == expected and passed == expected
        self.out(
            f"{'PASS' if ok else 'FAIL'} {name}: {passed}/{total} passed, {failed} failed, "
            f"{expected} expected ({elapsed:.0f}s)"
        )
        if not ok:
            for line in self.failures():
                self.out(line)
        return ok

    def run(self, classes, counts, timeout, renew=lambda: None):
        results = {}
        for name in classes:
            results[name] = self.run_class(name, counts[name], timeout)
            renew()
        passed = sum(results.values())
        self.out(f"{passed}/{len(classes)} classes passed")
        return all(results.values())


def main(argv=None, runner=None, lock_file=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("classes", nargs="+", help="PlayMode test class names, e.g. HomeSceneTests")
    parser.add_argument("--owner", default="playmode tests", help="name shown to other chats waiting for the Editor")
    parser.add_argument("--token", help="an Editor lock token this chat already holds; it is renewed, not released")
    parser.add_argument("--lock-wait", type=float, default=DEFAULT_LOCK_WAIT, help="seconds to wait for the Editor")
    parser.add_argument("--timeout", type=int, default=DEFAULT_CLASS_TIMEOUT, help="seconds allowed for each class")
    args = parser.parse_args(argv)
    runner = runner or Runner()
    lock_file = lock_file or editor_lock.LOCK_FILE

    try:
        expected = runner.expected_counts(args.classes)
    except EditorTrouble as trouble:
        runner.out(f"editor: {trouble}")
        return EXIT_EDITOR
    unknown = [name for name, count in expected.items() if count == 0]
    if unknown:
        runner.out(f"no PlayMode tests in: {', '.join(unknown)} (use class names without the namespace)")
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
        return 0 if runner.run(args.classes, expected, args.timeout, renew) else EXIT_FAILED
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
