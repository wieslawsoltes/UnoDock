#!/usr/bin/env python3
"""Require complete custom-chrome and resize-ownership evidence, not a zero exit."""
from pathlib import Path
import json
import platform
import re
import sys
import xml.etree.ElementTree as ET

EDGES = ("Left", "Top", "Right", "Bottom", "TopLeft", "TopRight", "BottomLeft", "BottomRight")
POLICIES = ("MinWidth", "MinHeight", "MaxWidth", "MaxHeight", "FlowDirection",
            "ResizeBorderThickness", "IsEnabled", "Manager.IsEnabled")


def contracts(system: str) -> dict[str, tuple[int, set[str]]]:
    if system not in ("Darwin", "Linux", "Windows"):
        raise ValueError(f"Unsupported acceptance platform: {system}")
    physical = system != "Darwin"
    result = {}
    for kind, suffix in (("document", "documents"), ("tools", "tools")):
        # Retain every original native-frame, resize and physical-button gate.
        required = {f"chrome/{kind}: initial client geometry settles without pointer input",
                    f"chrome/{kind}: native frame is replaced and caption controls remain real",
                    f"chrome/{kind}: system/custom switching retains window, caption and editors",
                    f"chrome/{kind}: late resize events cannot cancel an independently started successor"}
        for edge in EDGES:
            for cancel in (False, True):
                required.add(f"chrome/{kind}: actual native resize {edge}, cancel={cancel}")
            if physical:
                required.add(f"chrome/{kind}: physical pointer resizes {edge}")
        if physical:
            required.update((f"chrome/{kind}: physical custom maximize and restore buttons retain the native host",
                             f"chrome/{kind}: physical custom close honors veto and then closes once",
                             f"chrome/{kind}: physical Escape cancels resize without a late-release commit"))
        result[f"floating-chrome-{suffix}"] = ((24 if kind == "document" else 23) + (11 if physical else 0), required)
        required = {f"resize-policy/{kind}: {description}" for description in (
            "RTL grips remain on physical native edges",
            "zero border sides cannot resize through their corners",
            "a stationary resize does not clamp an existing oversized frame",
            "an application native move wins over resize, cancel=False",
            "an application native move wins over resize, cancel=True",
            "source parent ABA cannot revive the old resize",
            "presenter replacement withdraws resize ownership",
            "invalid begin and released sessions do not retain policy observers")}
        required.update(f"resize-policy/{kind}: policy mutation revokes the capture permanently: {policy}" for policy in POLICIES)
        if physical:
            required.update(f"resize-policy/{kind}: physical RTL pointer resizes {edge}" for edge in EDGES)
            required.update((f"resize-policy/{kind}: policy revocation releases a physical capture and clock",
                             f"resize-policy/{kind}: retained input from another grip cannot end a successor capture"))
        result[f"floating-resize-policy-{suffix}"] = (26 if physical else 16, required)
    return result


def verify(directory: Path) -> None:
    summary_path = directory / "chrome-acceptance.json"
    # A failed re-verification must not leave a prior passing summary behind.
    summary_path.unlink(missing_ok=True)
    system = platform.system()
    names: set[str] = set()
    counts = {}
    for suite, (minimum, required) in contracts(system).items():
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
        if not required.issubset(current):
            raise ValueError(f"Missing chrome cases: {sorted(required.difference(current))}")
        counts[suite] = len(cases)
        names.update(current)
    execution = json.loads((directory / "isolated-execution.json").read_text(encoding="utf-8"))
    planned = execution["planned"]
    completed = execution["completed"]
    if execution.get("schema") != 1 or not isinstance(planned, list) or not isinstance(completed, list):
        raise ValueError("Invalid isolated execution schema")
    if len(planned) != len(counts) or set(planned) != set(counts):
        raise ValueError("All four registered chrome suites must be planned exactly once")
    completed_names = [result["suite"] for result in completed]
    if len(completed_names) != len(counts) or set(completed_names) != set(counts):
        raise ValueError("All four registered chrome suites must complete exactly once")
    for result in completed:
        if result["error"] is not None or any(type(result.get(key)) is not int or result[key] != counts[result["suite"]]
                                              for key in ("executed", "passed")):
            raise ValueError("Isolated execution and JUnit evidence disagree")
    revision = (directory / "source-revision.txt").read_text(encoding="utf-8").strip()
    if not re.fullmatch(r"[0-9a-fA-F]{40}", revision):
        raise ValueError("A complete source commit SHA is required")
    summary = {"schema": 1, "platform": system, "executed": len(names), "passed": len(names), "failed": 0,
               "suites": counts, "revision": revision}
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary))


if __name__ == "__main__":
    try:
        verify(Path(sys.argv[1]))
    except (OSError, ValueError, KeyError, TypeError, IndexError, ET.ParseError) as error:
        print(f"Custom chrome evidence incomplete: {error}", file=sys.stderr)
        raise SystemExit(1)
