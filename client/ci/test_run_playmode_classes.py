"""Exercise the PlayMode class runner against a fake Editor without starting Unity."""

import importlib.util
from pathlib import Path
import tempfile
import unittest


CI_DIRECTORY = Path(__file__).resolve().parent
SCRIPT = CI_DIRECTORY / "run-playmode-classes.py"
spec = importlib.util.spec_from_file_location("run_playmode_classes", SCRIPT)
runner_module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner_module)
editor_lock = runner_module.editor_lock

TESTS = {
    "HomeSceneTests": ["Opens", "Closes"],
    "TopHomeSceneTests": ["Starts"],
    "TavernTrainingTests": ["Trains"],
}


class FakeEditor:
    """Answers the Unity CLI commands and writes the collector lines a real run would log."""

    def __init__(self, log, lock_path):
        self.log = log
        self.lock_path = lock_path
        self.calls = []
        self.zero_runs = 0
        self.failing = set()
        self.status = {"status": "completed", "results": []}
        self.holders = []
        self.unreachable = 0
        self.eval_timeouts = 0

    def write(self, line):
        with self.log.open("a", encoding="utf-8") as stream:
            stream.write(line + "\n")

    def __call__(self, name, **parameters):
        self.calls.append((name, parameters))
        if name == "list_tests":
            return {
                "Tests": [
                    {"FullName": f"Baryonyx.Tests.PlayMode.{owner}.{test}", "Explicit": False}
                    for owner, tests in TESTS.items()
                    for test in tests
                ]
            }
        if name == "editor_status":
            if self.unreachable:
                self.unreachable -= 1
                raise runner_module.EditorTrouble("No Unity Editor instances found")
            return {"status": "ready", "compiling": False, "domainReloadInProgress": False, "playMode": "stopped"}
        if name == "eval":
            if self.eval_timeouts:
                self.eval_timeouts -= 1
                raise runner_module.EditorTrouble("eval: Main thread operation timed out after 5000ms")
            if "RequestScriptReload" in parameters["code"]:
                self.unreachable = 3
                self.write("Begin MonoManager ReloadAssembly")
            return {"success": True, "result": 0 if "SaveScene" in parameters["code"] else "reload"}
        if name == "run_tests":
            self.holders.append(editor_lock.read(self.lock_path)["owner"])
            if self.zero_runs:
                self.zero_runs -= 1
                self.write("[TestResultCollector] Run started: 0 test(s)")
                return "started"
            marker = parameters["filter"].lower()
            names = [
                f"Baryonyx.Tests.PlayMode.{owner}.{test}"
                for owner, tests in TESTS.items()
                for test in tests
                if marker in f"baryonyx.tests.playmode.{owner}.{test}".lower()
            ]
            results = [
                {"FullName": full, "Status": "Failed" if full in self.failing else "Passed", "Message": "Expected: 1"}
                for full in names
            ]
            failed = sum(result["Status"] == "Failed" for result in results)
            self.status = {"status": "completed", "results": results}
            self.write("[TestResultCollector] Run started: 4 test(s)")
            self.write(f"[TestResultCollector] Run finished: {len(names)} total, {len(names) - failed} passed, {failed} failed")
            return "started"
        if name == "test_status":
            return self.status
        return "ok"

    def names(self):
        return [name for name, _ in self.calls]


class Clock:
    def __init__(self):
        self.current = 1000.0

    def now(self):
        return self.current

    def sleep(self, seconds):
        self.current += seconds


class RunPlaymodeClassesTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="baryonyx-playmode-classes-")
        self.addCleanup(temporary.cleanup)
        directory = Path(temporary.name)
        self.lock_path = directory / "Temp" / "editor-lock.json"
        self.editor = FakeEditor(directory / "Editor.log", self.lock_path)
        self.clock = Clock()
        self.output = []
        self.runner = runner_module.Runner(
            command=self.editor, log=self.editor.log, now=self.clock.now, sleep=self.clock.sleep, out=self.output.append
        )

    def main(self, *argv):
        return runner_module.main(list(argv), runner=self.runner, lock_file=self.lock_path)

    def test_each_class_runs_alone_under_the_lock_and_the_lock_is_returned(self):
        self.assertEqual(self.main("HomeSceneTests", "TavernTrainingTests", "--owner", "skills"), 0)
        filters = [parameters["filter"] for name, parameters in self.editor.calls if name == "run_tests"]
        self.assertEqual(filters, ["PlayMode.HomeSceneTests.", "PlayMode.TavernTrainingTests."])
        self.assertEqual(self.editor.holders, ["skills", "skills"])
        self.assertIn("PASS HomeSceneTests: 2/2 passed, 0 failed, 2 expected (2s)", self.output)
        self.assertEqual(self.output[-1], "2/2 classes passed")
        self.assertIsNone(editor_lock.read(self.lock_path))

    def test_the_open_scenes_are_saved_before_every_run(self):
        self.main("HomeSceneTests", "TavernTrainingTests")
        names = self.editor.names()
        for index, name in enumerate(names):
            if name == "run_tests":
                self.assertEqual(names[index - 1], "eval")

    def test_a_run_that_finds_no_tests_reloads_the_scripts_and_retries_once(self):
        self.editor.zero_runs = 1
        self.assertEqual(self.main("HomeSceneTests"), 0)
        self.assertEqual(self.editor.names().count("run_tests"), 2)
        self.assertIn("cancel_tests", self.editor.names())
        reloads = [p for name, p in self.editor.calls if name == "eval" and "RequestScriptReload" in p["code"]]
        self.assertEqual(len(reloads), 1)
        self.assertEqual(self.output[0], "HomeSceneTests: no tests found; reloading scripts and retrying once")
        self.assertIn("PASS HomeSceneTests: 2/2 passed, 0 failed, 2 expected", self.output[1])

    def test_a_class_that_still_finds_no_tests_fails_without_a_third_try(self):
        self.editor.zero_runs = 2
        self.assertEqual(self.main("HomeSceneTests"), runner_module.EXIT_FAILED)
        self.assertEqual(self.editor.names().count("run_tests"), 2)
        self.assertTrue(self.output[1].startswith("FAIL HomeSceneTests: still no tests found"))

    def test_a_failed_test_is_listed_and_the_remaining_classes_still_run(self):
        self.editor.failing = {"Baryonyx.Tests.PlayMode.HomeSceneTests.Closes"}
        self.assertEqual(self.main("HomeSceneTests", "TavernTrainingTests"), runner_module.EXIT_FAILED)
        self.assertIn("FAIL HomeSceneTests: 1/2 passed, 1 failed, 2 expected (2s)", self.output)
        self.assertIn("    Failed: Baryonyx.Tests.PlayMode.HomeSceneTests.Closes (Expected: 1)", self.output)
        self.assertIn("PASS TavernTrainingTests: 1/1 passed, 0 failed, 1 expected (2s)", self.output)
        self.assertEqual(self.output[-1], "1/2 classes passed")

    def test_an_eval_that_times_out_is_sent_again(self):
        self.editor.eval_timeouts = 2
        self.assertEqual(self.main("TavernTrainingTests"), 0)
        self.assertEqual(self.editor.names().count("eval"), 3)

    def test_an_eval_that_keeps_timing_out_stops_the_run_and_returns_the_lock(self):
        self.editor.eval_timeouts = 3
        self.assertEqual(self.main("TavernTrainingTests"), runner_module.EXIT_EDITOR)
        self.assertNotIn("run_tests", self.editor.names())
        self.assertIsNone(editor_lock.read(self.lock_path))

    def test_an_unknown_class_stops_before_taking_the_lock(self):
        self.assertEqual(self.main("HomeScene", "TavernTrainingTests"), runner_module.EXIT_USAGE)
        self.assertNotIn("run_tests", self.editor.names())
        self.assertFalse(self.lock_path.exists())

    def test_the_class_boundary_keeps_a_longer_class_name_out(self):
        counts = self.runner.expected_counts(["HomeSceneTests"])
        self.assertEqual(counts, {"HomeSceneTests": 2})

    def test_a_held_token_is_renewed_but_not_released(self):
        token = editor_lock.acquire(self.lock_path, "skills", 0, 600)
        self.assertEqual(self.main("TavernTrainingTests", "--token", token), 0)
        self.assertEqual(editor_lock.read(self.lock_path)["token"], token)

    def test_a_busy_editor_returns_busy_without_running(self):
        editor_lock.acquire(self.lock_path, "other chat", 0, 600)
        self.assertEqual(self.main("TavernTrainingTests", "--lock-wait", "0"), runner_module.EXIT_BUSY)
        self.assertNotIn("run_tests", self.editor.names())


if __name__ == "__main__":
    unittest.main()
