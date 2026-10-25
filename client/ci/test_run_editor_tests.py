"""Exercise the Editor test runner against a fake Editor without starting Unity."""

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


CI_DIRECTORY = Path(__file__).resolve().parent
SCRIPT = CI_DIRECTORY / "run-editor-tests.py"
spec = importlib.util.spec_from_file_location("run_editor_tests", SCRIPT)
runner_module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner_module)
editor_lock = runner_module.editor_lock

TESTS = {
    "playmode": {
        "Baryonyx.Tests.PlayMode.HomeSceneTests": ["Opens", "Closes", "OpensTwice"],
        "Baryonyx.Tests.PlayMode.TopHomeSceneTests": ["Starts"],
        "Baryonyx.Tests.PlayMode.TavernTrainingTests": ["Trains", "Explicitly"],
    },
    "editmode": {
        "Baryonyx.Tests.EditMode.CombatTests": ["Hits", "Misses(2)"],
        "Baryonyx.Tests.HealthTests": ["Syncs"],
    },
}
EXPLICIT = {"Baryonyx.Tests.PlayMode.TavernTrainingTests.Explicitly"}


def listed(mode):
    return [
        {
            "FullName": f"{owner}.{test}",
            "Assembly": runner_module.ASSEMBLIES[mode],
            "Explicit": f"{owner}.{test}" in EXPLICIT,
        }
        for owner, tests in TESTS[mode].items()
        for test in tests
    ]


class FakeEditor:
    """Answers the Unity CLI commands and writes the result file EditorTestRun would write."""

    def __init__(self, directory, lock_path):
        self.log = directory / "Editor.log"
        self.result_file = directory / "Temp" / "editor-test-run.json"
        self.lock_path = lock_path
        self.calls = []
        self.zero_runs = 0
        self.failing = set()
        self.holders = []
        self.holder_pids = []
        self.unreachable = 0
        self.eval_timeouts = 0
        self.start_timeouts = 0
        self.start_reply = "started"
        self.compile_errors = []
        self.compiling = 0
        self.up_to_date = False

    def write_log(self, line):
        with self.log.open("a", encoding="utf-8") as stream:
            stream.write(line + "\n")

    def write_result(self, result):
        self.result_file.parent.mkdir(parents=True, exist_ok=True)
        self.result_file.write_text(json.dumps(result), encoding="utf-8")

    def start(self, code):
        run_id, mode, assembly, pattern = [part.split('"')[0] for part in code.split('@"')[1:]]
        lock = editor_lock.read(self.lock_path)
        self.holders.append(lock["owner"])
        self.holder_pids.append(lock.get("pid"))
        if self.zero_runs:
            self.zero_runs -= 1
            self.write_result({"id": run_id, "status": "started", "started": 0})
            return
        regex = runner_module.re.compile(pattern)
        tests = [test for test in listed(mode) if test["Assembly"] == assembly and not test["Explicit"]]
        tests = [
            {
                "name": test["FullName"],
                "status": "Failed" if test["FullName"] in self.failing else "Passed",
                "message": "Expected: 1" if test["FullName"] in self.failing else "",
            }
            for test in tests
            if regex.search(test["FullName"])
        ]
        failed = sum(test["status"] == "Failed" for test in tests)
        self.write_result(
            {
                "id": run_id,
                "status": "finished",
                "started": 9,
                "total": len(tests),
                "passed": len(tests) - failed,
                "failed": failed,
                "skipped": 0,
                "tests": tests,
            }
        )

    def __call__(self, name, **parameters):
        self.calls.append((name, parameters))
        if name == "list_tests":
            return {"Tests": listed(parameters["mode"])}
        if name == "editor_status":
            if self.unreachable:
                self.unreachable -= 1
                raise runner_module.EditorTrouble("No Unity Editor instances found")
            return {"status": "ready", "compiling": False, "domainReloadInProgress": False, "playMode": "stopped"}
        if name == "recompile" and self.up_to_date:
            return {"status": "up_to_date"}
        if name == "recompile_status" and self.up_to_date:
            return {"status": "up_to_date", "failed": False, "errors": []}
        if name == "recompile":
            self.compiling = 2
            self.write_log("*** Tundra build success")
            for error in self.compile_errors:
                self.write_log(error)
            return {"status": "compiling"}
        if name == "recompile_status":
            if self.compiling:
                self.compiling -= 1
                return {"status": "compiling"}
            return {"status": "completed", "failed": bool(self.compile_errors), "errors": []}
        if name == "eval":
            code = parameters["code"]
            if "EditorTestRun.Start" in code:
                if self.start_reply == "started":
                    self.start(code)
                if self.start_timeouts:
                    self.start_timeouts -= 1
                    raise runner_module.EditorTrouble("eval: Main thread operation timed out after 5000ms")
                return {"success": True, "result": self.start_reply}
            if self.eval_timeouts:
                self.eval_timeouts -= 1
                raise runner_module.EditorTrouble("eval: Main thread operation timed out after 5000ms")
            if "RequestScriptReload" in code:
                self.unreachable = 3
                self.write_log("Begin MonoManager ReloadAssembly")
                return {"success": True, "result": "reload"}
            return {"success": True, "result": "cancelled"}
        return "ok"

    def names(self):
        return [name for name, _ in self.calls]

    def starts(self):
        return [p["code"] for name, p in self.calls if name == "eval" and "EditorTestRun.Start" in p["code"]]


class Clock:
    def __init__(self):
        self.current = 1000.0

    def now(self):
        return self.current

    def sleep(self, seconds):
        self.current += seconds


class RunEditorTestsTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="baryonyx-editor-tests-")
        self.addCleanup(temporary.cleanup)
        directory = Path(temporary.name)
        self.lock_path = directory / "Temp" / "editor-lock.json"
        self.editor = FakeEditor(directory, self.lock_path)
        self.clock = Clock()
        self.output = []
        self.runner = runner_module.Runner(
            command=self.editor,
            log=self.editor.log,
            result_file=self.editor.result_file,
            now=self.clock.now,
            sleep=self.clock.sleep,
            out=self.output.append,
        )

    def main(self, *argv):
        return runner_module.main(list(argv), runner=self.runner, lock_file=self.lock_path)

    def test_the_classes_run_together_in_one_run_under_the_lock_and_the_lock_is_returned(self):
        self.assertEqual(self.main("HomeSceneTests", "TavernTrainingTests", "--owner", "skills"), 0)
        self.assertEqual(len(self.editor.starts()), 1)
        self.assertEqual(self.editor.holders, ["skills"])
        self.assertIn("PASS HomeSceneTests: 3/3 passed, 0 failed, 3 expected", self.output)
        self.assertIn("PASS TavernTrainingTests: 1/1 passed, 0 failed, 1 expected", self.output)
        self.assertEqual(self.output[-1], "4/4 passed, 0 failed (2s)")
        self.assertIsNone(editor_lock.read(self.lock_path))

    def test_the_pattern_keeps_a_longer_class_name_out(self):
        self.main("HomeSceneTests")
        self.assertNotIn("TopHomeSceneTests", "".join(self.output))
        self.assertIn("PASS HomeSceneTests: 3/3 passed, 0 failed, 3 expected", self.output)

    def test_saving_and_starting_are_one_eval(self):
        self.main("HomeSceneTests")
        evals = [p["code"] for name, p in self.editor.calls if name == "eval"]
        self.assertEqual(len(evals), 1)
        self.assertIn("EditorTestRun.Start(", evals[0])
        self.assertIn('@"playmode", @"Baryonyx.PlayModeTests"', evals[0])

    def test_no_class_runs_the_whole_assembly(self):
        self.assertEqual(self.main("--mode", "editmode"), 0)
        self.assertIn('@"Baryonyx.EditModeTests", @""', self.editor.starts()[0])
        self.assertIn("PASS Baryonyx.EditModeTests: 3/3 passed, 0 failed, 3 expected", self.output)

    def test_editmode_classes_outside_the_editmode_namespace_are_found(self):
        self.assertEqual(self.main("--mode", "editmode", "HealthTests", "CombatTests"), 0)
        self.assertIn("PASS HealthTests: 1/1 passed, 0 failed, 1 expected", self.output)
        self.assertIn("PASS CombatTests: 2/2 passed, 0 failed, 2 expected", self.output)

    def test_a_run_that_finds_no_tests_reloads_the_scripts_and_retries_once(self):
        self.editor.zero_runs = 1
        self.assertEqual(self.main("HomeSceneTests"), 0)
        self.assertEqual(len(self.editor.starts()), 2)
        self.assertTrue(any("EditorTestRun.Cancel" in p.get("code", "") for _, p in self.editor.calls))
        reloads = [p for name, p in self.editor.calls if name == "eval" and "RequestScriptReload" in p["code"]]
        self.assertEqual(len(reloads), 1)
        self.assertEqual(self.output[0], "no tests found; reloading scripts and retrying once")
        self.assertIn("PASS HomeSceneTests: 3/3 passed, 0 failed, 3 expected", self.output)

    def test_a_run_that_still_finds_no_tests_fails_without_a_third_try(self):
        self.editor.zero_runs = 2
        self.assertEqual(self.main("HomeSceneTests"), runner_module.EXIT_FAILED)
        self.assertEqual(len(self.editor.starts()), 2)
        self.assertTrue(self.output[1].startswith("FAIL: still no tests found"))

    def test_a_failed_test_is_listed_and_fails_only_its_class(self):
        self.editor.failing = {"Baryonyx.Tests.PlayMode.HomeSceneTests.Closes"}
        self.assertEqual(self.main("HomeSceneTests", "TavernTrainingTests"), runner_module.EXIT_FAILED)
        self.assertIn("FAIL HomeSceneTests: 2/3 passed, 1 failed, 3 expected", self.output)
        self.assertIn("    Failed: Baryonyx.Tests.PlayMode.HomeSceneTests.Closes (Expected: 1)", self.output)
        self.assertIn("PASS TavernTrainingTests: 1/1 passed, 0 failed, 1 expected", self.output)

    def test_a_refused_start_stops_with_the_editor_reason(self):
        self.editor.start_reply = "error: an untitled scene has unsaved changes; save or discard it by hand first"
        self.assertEqual(self.main("HomeSceneTests"), runner_module.EXIT_EDITOR)
        self.assertIn("untitled scene", self.output[-1])
        self.assertIsNone(editor_lock.read(self.lock_path))

    def test_a_start_whose_reply_timed_out_is_not_sent_again_when_it_went_through(self):
        self.editor.start_timeouts = 1
        self.assertEqual(self.main("HomeSceneTests"), 0)
        self.assertEqual(len(self.editor.starts()), 1)

    def test_an_eval_that_keeps_timing_out_stops_the_run_and_returns_the_lock(self):
        self.editor.zero_runs = 1
        self.editor.eval_timeouts = 3
        self.assertEqual(self.main("TavernTrainingTests"), runner_module.EXIT_EDITOR)
        self.assertIsNone(editor_lock.read(self.lock_path))

    def test_an_unknown_class_stops_before_taking_the_lock(self):
        self.assertEqual(self.main("HomeScene", "TavernTrainingTests"), runner_module.EXIT_USAGE)
        self.assertEqual(self.editor.starts(), [])
        self.assertFalse(self.lock_path.exists())

    def test_a_namespaced_or_odd_name_stops_before_asking_the_editor(self):
        self.assertEqual(self.main("PlayMode.HomeSceneTests"), runner_module.EXIT_USAGE)
        self.assertEqual(self.editor.calls, [])

    def test_explicit_tests_are_not_expected(self):
        counts = self.runner.expected_counts("playmode", ["TavernTrainingTests", "HomeSceneTests"])
        self.assertEqual(counts, {"TavernTrainingTests": 1, "HomeSceneTests": 3})

    def test_a_held_token_is_renewed_but_not_released(self):
        token = editor_lock.acquire(self.lock_path, "skills", 0, 600)
        self.assertEqual(self.main("TavernTrainingTests", "--token", token), 0)
        self.assertEqual(editor_lock.read(self.lock_path)["token"], token)

    def test_the_end_of_turn_hook_leaves_the_lock_to_a_running_script(self):
        token = editor_lock.acquire(self.lock_path, "skills", 0, 600, session="chat")
        self.assertEqual(self.main("TavernTrainingTests", "--token", token), 0)
        self.assertEqual(self.editor.holder_pids, [runner_module.os.getpid()])
        self.assertIsNone(editor_lock.read(self.lock_path)["pid"])
        self.assertTrue(editor_lock.release_session(self.lock_path, "chat"))

    def test_a_test_name_runs_only_that_test_and_not_one_whose_name_it_begins(self):
        self.assertEqual(self.main("HomeSceneTests.Opens"), 0)
        self.assertIn("PASS HomeSceneTests.Opens: 1/1 passed, 0 failed, 1 expected", self.output)
        self.assertEqual(self.output[-1], "1/1 passed, 0 failed (2s)")

    def test_a_test_with_arguments_is_picked_by_its_name(self):
        self.assertEqual(self.main("--mode", "editmode", "CombatTests.Misses"), 0)
        self.assertIn("PASS CombatTests.Misses: 1/1 passed, 0 failed, 1 expected", self.output)

    def test_a_class_and_one_of_its_tests_count_the_test_once(self):
        self.assertEqual(self.main("HomeSceneTests", "HomeSceneTests.Opens"), 0)
        self.assertEqual(len(self.editor.starts()), 1)

    def test_an_unknown_test_name_stops_before_taking_the_lock(self):
        self.assertEqual(self.main("HomeSceneTests.Flies"), runner_module.EXIT_USAGE)
        self.assertEqual(self.editor.starts(), [])
        self.assertFalse(self.lock_path.exists())

    def test_recompile_compiles_under_the_lock_then_runs(self):
        self.assertEqual(self.main("HomeSceneTests.Opens", "--recompile"), 0)
        names = self.editor.names()
        self.assertLess(names.index("recompile"), names.index("list_tests"))
        self.assertTrue(self.output[0].startswith("compiled ("))
        self.assertIn("PASS HomeSceneTests.Opens: 1/1 passed, 0 failed, 1 expected", self.output)
        self.assertIsNone(editor_lock.read(self.lock_path))

    def test_compile_errors_stop_before_the_tests_and_show_only_the_error_lines(self):
        self.editor.compile_errors = [
            "Assets/A.cs(3,1): error CS1002: ; expected",
            "Assets/A.cs(3,1): error CS1002: ; expected",
        ]
        self.assertEqual(self.main("HomeSceneTests", "--recompile"), runner_module.EXIT_COMPILE)
        self.assertEqual(self.editor.starts(), [])
        self.assertTrue(self.output[0].startswith("FAIL: compile errors"))
        self.assertEqual(self.output[1:], ["    Assets/A.cs(3,1): error CS1002: ; expected"])
        self.assertIsNone(editor_lock.read(self.lock_path))

    def test_nothing_to_compile_goes_on_to_the_tests(self):
        self.editor.up_to_date = True
        self.assertEqual(self.main("HomeSceneTests.Opens", "--recompile"), 0)
        self.assertTrue(self.output[0].startswith("compiled ("))
        self.assertLess(self.clock.current - 1000.0, 60)

    def test_compile_only_runs_no_tests(self):
        self.assertEqual(self.main("--compile-only"), 0)
        self.assertEqual(self.editor.starts(), [])
        self.assertNotIn("list_tests", self.editor.names())
        self.assertEqual(len(self.output), 1)

    def test_a_busy_editor_returns_busy_without_running(self):
        editor_lock.acquire(self.lock_path, "other chat", 0, 600)
        self.assertEqual(self.main("TavernTrainingTests", "--lock-wait", "0"), runner_module.EXIT_BUSY)
        self.assertEqual(self.editor.starts(), [])


if __name__ == "__main__":
    unittest.main()
