"""Reject missing, empty, skipped-only, and failed Unity NUnit result sets."""

import argparse
from pathlib import Path
import xml.etree.ElementTree as ET


def verify_results(directory: Path, assembly: str) -> int:
    cases = []
    for file in directory.rglob("*.xml"):
        root = ET.parse(file).getroot()
        if root.tag != "test-run":
            continue
        for suite in root.iter("test-suite"):
            if suite.get("type") == "Assembly" and suite.get("name") == assembly + ".dll":
                if root.get("result") != "Passed" or suite.get("result") != "Passed":
                    raise ValueError(f"{assembly}: NUnit reports a failed or incomplete run")
                cases.extend(suite.iter("test-case"))
    if not cases:
        raise ValueError(f"No test results found for {assembly} in {directory}")
    failed = [case.get("fullname") for case in cases if case.get("result") == "Failed"]
    passed = sum(case.get("result") == "Passed" for case in cases)
    if failed or not passed:
        raise ValueError(f"{assembly}: passed={passed}, failed={failed}; at least one pass is required")
    print(f"{assembly}: {passed} passed, {len(cases) - passed} skipped, 0 failed")
    return passed


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("assembly")
    args = parser.parse_args()
    verify_results(args.directory, args.assembly)
