#!/usr/bin/env python3
"""Require complete passing native JUnit evidence, independent of host exit status."""
import json
import platform
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def verify(directory: Path) -> None:
    system = platform.system()
    expected = {"desktop-floating": 38 if system == "Linux" else 34, "uno-theme": 10}
    if system == "Windows":
        expected["windows-floating-input"] = 8
    elif system == "Darwin":
        expected["mac-native"] = 8
    elif system != "Linux":
        raise RuntimeError(f"No native acceptance contract for {system}")
    totals = {"passed": 0, "failed": 0, "skipped": 0, "executed": 0}
    for suite, minimum in expected.items():
        path = directory / (suite + ".xml")
        if not path.is_file():
            raise RuntimeError(f"Required native suite was not executed: {suite}")
        root = ET.parse(path).getroot()
        cases = list(root.iter("testcase"))
        names = [(case.get("classname", ""), case.get("name", "")) for case in cases]
        if len(set(names)) != len(names):
            raise RuntimeError(f"Duplicate case names in {suite}")
        failed = [case for case in cases if case.find("failure") is not None or case.find("error") is not None]
        skipped = [case for case in cases if case.find("skipped") is not None]
        if len(cases) < minimum:
            raise RuntimeError(f"Incomplete {suite}: {len(cases)} cases, at least {minimum} required")
        for case in failed:
            print(f"FAIL {suite}: {case.get('name')}", file=sys.stderr)
        totals["executed"] += len(cases)
        totals["failed"] += len(failed)
        totals["skipped"] += len(skipped)
        totals["passed"] += len(cases) - len(failed) - len(skipped)
        print(f"Verified {suite}: {len(cases)} executed, {len(failed)} failed, {len(skipped)} skipped")
    (directory / "native-acceptance.json").write_text(json.dumps({"schema": 1, "platform": system, **totals}, indent=2) + "\n")
    if totals["failed"] or totals["skipped"]:
        raise RuntimeError(f"Native acceptance failed: {totals}")


if __name__ == "__main__":
    try:
        verify(Path(sys.argv[1]))
    except (IndexError, OSError, ET.ParseError, RuntimeError) as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
