#!/usr/bin/env python3
"""Require actual, complete custom-chrome execution, not a successful host exit."""
from pathlib import Path
import json
import platform
import sys
import xml.etree.ElementTree as ET


def verify(directory: Path) -> None:
    system = platform.system()
    minimum = 47 if system == "Darwin" else 69
    root = ET.parse(directory / "floating-chrome.xml").getroot()
    cases = root.findall("testcase")
    names = [case.get("name", "") for case in cases]
    if root.tag != "testsuite" or root.get("name") != "floating-chrome":
        raise ValueError("Unexpected chrome suite identity")
    if len(cases) < minimum or int(root.get("tests", "-1")) != len(cases):
        raise ValueError(f"Partial chrome execution: {len(cases)}, expected at least {minimum}")
    if any(not name for name in names) or len(set(names)) != len(names):
        raise ValueError("Chrome cases require unique names")
    failed = [case.get("name") for case in cases if any(case.find(tag) is not None for tag in ("failure", "error", "skipped"))]
    if failed or any(int(root.get(key, "0")) for key in ("failures", "errors", "skipped")):
        raise ValueError(f"Chrome acceptance failed: {failed}")
    if system in ("Linux", "Windows"):
        for kind in ("document", "tools"):
            for edge in ("Left", "Top", "Right", "Bottom", "TopLeft", "TopRight", "BottomLeft", "BottomRight"):
                if f"chrome/{kind}: physical pointer resizes {edge}" not in names:
                    raise ValueError(f"Missing real pointer resize: {kind}/{edge}")
            if f"chrome/{kind}: physical custom maximize and restore buttons retain the native host" not in names:
                raise ValueError(f"Missing real maximize/restore: {kind}")
    summary = {"schema": 1, "platform": system, "executed": len(cases), "passed": len(cases), "failed": 0,
               "revision": (directory / "source-revision.txt").read_text().strip()}
    (directory / "chrome-acceptance.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary))


if __name__ == "__main__":
    try:
        verify(Path(sys.argv[1]))
    except (OSError, ValueError, IndexError, ET.ParseError) as error:
        print(f"Custom chrome evidence incomplete: {error}", file=sys.stderr)
        raise SystemExit(1)
