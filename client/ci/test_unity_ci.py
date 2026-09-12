"""Exercise Unity CI helpers in temporary projects without starting Unity."""

import importlib.util
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET


CI_DIRECTORY = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("verify_test_results", CI_DIRECTORY / "verify-test-results.py")
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)

ASSEMBLIES = {
    "editmode": "Baryonyx.EditModeTests",
    "playmode": "Baryonyx.PlayModeTests",
}


class ResultVerificationTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="baryonyx-unity-results-")
        self.addCleanup(temporary.cleanup)
        self.directory = Path(temporary.name)

    def write_results(self, mode="editmode", cases=("Passed",), run_result="Passed", suite_result="Passed"):
        root = ET.Element("test-run", result=run_result)
        suite = ET.SubElement(root, "test-suite", type="Assembly", name=ASSEMBLIES[mode] + ".dll", result=suite_result)
        for index, result in enumerate(cases):
            ET.SubElement(suite, "test-case", fullname=f"Example.Test{index}", result=result)
        ET.ElementTree(root).write(self.directory / f"{mode}-results.xml", encoding="utf-8")

    def test_accepts_each_assembly_and_counts_only_its_passes(self):
        self.write_results(cases=("Passed", "Passed", "Skipped"))
        self.write_results(mode="playmode")
        self.assertEqual(verifier.verify_results(self.directory, ASSEMBLIES["editmode"]), 2)
        self.assertEqual(verifier.verify_results(self.directory, ASSEMBLIES["playmode"]), 1)

    def test_rejects_missing_directory(self):
        with self.assertRaises(ValueError):
            verifier.verify_results(self.directory / "missing", ASSEMBLIES["editmode"])

    def test_rejects_missing_xml(self):
        (self.directory / "editmode.log").write_text("No test results", encoding="utf-8")
        with self.assertRaises(ValueError):
            verifier.verify_results(self.directory, ASSEMBLIES["editmode"])

    def test_rejects_results_for_only_the_other_mode(self):
        self.write_results(mode="playmode")
        with self.assertRaises(ValueError):
            verifier.verify_results(self.directory, ASSEMBLIES["editmode"])

    def test_rejects_empty_skipped_only_or_failed_cases(self):
        for cases in [(), ("Skipped",), ("Passed", "Failed")]:
            with self.subTest(cases=cases):
                self.write_results(cases=cases)
                with self.assertRaises(ValueError):
                    verifier.verify_results(self.directory, ASSEMBLIES["editmode"])

    def test_rejects_failed_or_incomplete_run_and_suite(self):
        for run_result, suite_result in [("Failed", "Passed"), ("Passed", "Failed"), ("Inconclusive", "Passed")]:
            with self.subTest(run_result=run_result, suite_result=suite_result):
                self.write_results(run_result=run_result, suite_result=suite_result)
                with self.assertRaises(ValueError):
                    verifier.verify_results(self.directory, ASSEMBLIES["editmode"])

    def test_rejects_malformed_xml(self):
        (self.directory / "editmode-results.xml").write_text("<test-run>", encoding="utf-8")
        with self.assertRaises(ET.ParseError):
            verifier.verify_results(self.directory, ASSEMBLIES["editmode"])


class TestRunnerTests(unittest.TestCase):
    def setUp(self):
        if os.name == "nt":
            bash = Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "Git/bin/bash.exe"
            self.bash = str(bash) if bash.is_file() else None
        else:
            self.bash = shutil.which("bash")
        if not self.bash:
            self.fail("Bash is required; use Git for Windows on Windows.")

        temporary = tempfile.TemporaryDirectory(prefix="baryonyx-unity-runner-")
        self.addCleanup(temporary.cleanup)
        self.directory = Path(temporary.name).resolve()
        self.library = self.directory / "client/Library"
        self.library.mkdir(parents=True)
        (self.library / "imported-assets").write_text("keep", encoding="utf-8")
        self.results = self.directory / "client/TestResults"
        for mode in ASSEMBLIES:
            directory = self.results / mode
            directory.mkdir(parents=True)
            (directory / "stale.xml").write_text("old results", encoding="utf-8")
        for name in ["burst.pid", "ilpp.pid"]:
            (self.library / name).write_text("123", encoding="utf-8")

        self.bin_directory = self.directory / "bin"
        self.bin_directory.mkdir()
        runner_temp = self.directory / "runner-temp"
        cli_directory = runner_temp / "baryonyx-game-ci"
        cli_directory.mkdir(parents=True)
        self.write_executable(cli_directory / "game-ci", """#!/bin/bash
set -euo pipefail
test -f client/Library/imported-assets
test ! -e client/Library/burst.pid
test ! -e client/Library/ilpp.pid
printf '%s\\n' "$@" >> invocations.txt
for argument in "$@"; do
  case "$argument" in
    --artifactsPath=*) results="${argument#--artifactsPath=}" ;;
  esac
done
test ! -e "$results/stale.xml"
mkdir -p "$results"
printf 'current run\\n' > "$results/run.log"
printf '456\\n' > client/Library/burst.pid
printf '456\\n' > client/Library/ilpp.pid
exit "${TEST_CLI_EXIT_CODE:-0}"
""")
        # Only this temporary fixture bypasses the pinned production checksum.
        self.write_executable(self.bin_directory / "sha256sum", """#!/bin/bash
cat > /dev/null
exit "${TEST_CHECKSUM_EXIT_CODE:-0}"
""")
        self.write_executable(self.bin_directory / "curl", "#!/bin/bash\necho 'Unexpected download' >&2\nexit 99\n")
        self.environment = {
            **os.environ,
            "RUNNER_TEMP": "runner-temp",
            "UNITY_VERSION": "6000.6.0f1",
            "LC_ALL": "C",
        }

    def write_executable(self, path, content):
        path.write_text(content, encoding="utf-8", newline="\n")
        path.chmod(0o755)

    def run_mode(self, mode, assembly=None, **environment):
        # All cleanup in the helper runs inside this test-owned temporary project.
        self.assertTrue(self.results.resolve().is_relative_to(self.directory))
        return subprocess.run(
            [
                self.bash, "-c", 'PATH="$PWD/bin:$PATH" exec bash "$@"', "unity-ci-test",
                (CI_DIRECTORY / "run-unity-tests.sh").as_posix(), mode, assembly or ASSEMBLIES[mode],
            ],
            cwd=self.directory,
            env={**self.environment, **environment},
            capture_output=True,
            encoding="utf-8",
            timeout=20,
        )

    def test_sequential_modes_keep_library_and_each_others_results(self):
        for mode in ASSEMBLIES:
            result = self.run_mode(mode)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertFalse((self.results / mode / "stale.xml").exists())
            self.assertTrue((self.results / mode / "run.log").is_file())
        self.assertEqual((self.library / "imported-assets").read_text(encoding="utf-8"), "keep")
        self.assertTrue((self.results / "editmode/run.log").is_file())
        invocations = (self.directory / "invocations.txt").read_text(encoding="utf-8")
        for mode, assembly in ASSEMBLIES.items():
            self.assertIn(f"--testPlatforms={mode}\n", invocations)
            self.assertIn(f"--customParameters=-assemblyNames {assembly}\n", invocations)
            self.assertIn(f"--artifactsPath=client/TestResults/{mode}\n", invocations)

    def test_failed_cli_preserves_failure_and_does_not_leave_stale_results(self):
        result = self.run_mode("editmode", TEST_CLI_EXIT_CODE="2")
        self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
        self.assertFalse((self.results / "editmode/stale.xml").exists())
        self.assertTrue((self.results / "playmode/stale.xml").is_file())
        next_result = self.run_mode("playmode")
        self.assertEqual(next_result.returncode, 0, next_result.stdout + next_result.stderr)
        self.assertTrue((self.results / "editmode/run.log").is_file())

    def test_bad_cached_checksum_prevents_execution(self):
        result = self.run_mode("editmode", TEST_CHECKSUM_EXIT_CODE="1")
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse((self.directory / "invocations.txt").exists())
        self.assertFalse((self.results / "editmode/stale.xml").exists())

    def test_invalid_mode_or_assembly_does_not_remove_files(self):
        for mode, assembly in [("../Library", ASSEMBLIES["editmode"]), ("editmode", ASSEMBLIES["playmode"])]:
            with self.subTest(mode=mode, assembly=assembly):
                result = self.run_mode(mode, assembly)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("Unsupported test mode or assembly", result.stderr)
                self.assertTrue((self.library / "burst.pid").is_file())
                self.assertTrue((self.results / "editmode/stale.xml").is_file())
                self.assertFalse((self.directory / "invocations.txt").exists())


if __name__ == "__main__":
    unittest.main()
