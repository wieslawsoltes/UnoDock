#!/usr/bin/env python3
"""Require actual, complete custom-chrome execution, not a successful host exit."""
from pathlib import Path
import json
import platform
import sys
import xml.etree.ElementTree as ET


def verify(directory: Path) -> None:
    system = platform.system()
    if system not in ("Darwin", "Linux", "Windows"):
        raise ValueError(f"Unsupported acceptance platform: {system}")
    names: set[str] = set()
    counts = {}
    for kind, suite in (("document", "floating-chrome-documents"), ("tools", "floating-chrome-tools")):
        minimum = (24 if kind == "document" else 23) + (0 if system == "Darwin" else 11)
        root = ET.parse(directory / (suite + ".xml")).getroot()
        cases = root.findall("testcase")
        current = [case.get("name", "") for case in cases]
        if root.tag != "testsuite" or root.get("name") != suite:
            raise ValueError(f"Unexpected chrome suite identity: {suite}")
        if len(cases) < minimum or int(root.get("tests", "-1")) != len(cases):
            raise ValueError(f"Partial {suite}: {len(cases)}, expected at least {minimum}")
        if any(not name for name in current) or len(set(current)) != len(current) or names.intersection(current):
            raise ValueError("Chrome cases require globally unique names")
        failed = [case.get("name") for case in cases if any(case.find(tag) is not None for tag in ("failure", "error", "skipped"))]
        if failed or any(int(root.get(key, "0")) for key in ("failures", "errors", "skipped")):
            raise ValueError(f"Chrome acceptance failed: {failed}")
        required = {f"chrome/{kind}: native frame is replaced and caption controls remain real",
                    f"chrome/{kind}: system/custom switching retains window, caption and editors",
                    f"chrome/{kind}: late resize events cannot cancel an independently started successor"}
        for edge in ("Left", "Top", "Right", "Bottom", "TopLeft", "TopRight", "BottomLeft", "BottomRight"):
            for cancel in (False, True):
                required.add(f"chrome/{kind}: actual native resize {edge}, cancel={cancel}")
            if system != "Darwin":
                required.add(f"chrome/{kind}: physical pointer resizes {edge}")
        if system != "Darwin":
            required.update((f"chrome/{kind}: physical custom maximize and restore buttons retain the native host",
                             f"chrome/{kind}: physical custom close honors veto and then closes once",
                             f"chrome/{kind}: physical Escape cancels resize without a late-release commit"))
        if not required.issubset(current):
            raise ValueError(f"Missing chrome cases: {sorted(required.difference(current))}")
        counts[suite] = len(cases)
        names.update(current)
    execution = json.loads((directory / "isolated-execution.json").read_text(encoding="utf-8"))
    if set(execution["planned"]) != set(counts) or len(execution["completed"]) != 2:
        raise ValueError("Both registered chrome suites must execute exactly once")
    for result in execution["completed"]:
        if result["error"] or result.get("executed") != counts.get(result["suite"]):
            raise ValueError("Isolated execution and JUnit evidence disagree")
    summary = {"schema": 1, "platform": system, "executed": len(names), "passed": len(names), "failed": 0,
               "suites": counts, "revision": (directory / "source-revision.txt").read_text().strip()}
    (directory / "chrome-acceptance.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary))


if __name__ == "__main__":
    try:
        verify(Path(sys.argv[1]))
    except (OSError, ValueError, KeyError, TypeError, IndexError, ET.ParseError) as error:
        print(f"Custom chrome evidence incomplete: {error}", file=sys.stderr)
        raise SystemExit(1)
