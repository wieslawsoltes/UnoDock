#!/usr/bin/env python3
"""Run every selected real-host suite in its own process; require complete JUnit evidence.

Suite names come from the built application's registry, not a second hardcoded list.
No test is retried and an exit code of zero cannot override missing/failed results.
Run this tool within a dedicated display/session when native input is enabled.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import xml.etree.ElementTree as ET


def read_manifest(path: Path) -> list[str]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list) or not data:
        raise ValueError("The application did not select any test suites.")
    if any(not isinstance(name, str) or not name or any(c not in "abcdefghijklmnopqrstuvwxyz0123456789-" for c in name) for name in data):
        raise ValueError("The application returned invalid suite names.")
    if len(set(data)) != len(data):
        raise ValueError("The application returned duplicate suites.")
    return data


def verify_suite(path: Path, name: str) -> dict[str, int]:
    root = ET.parse(path).getroot()
    if root.tag != "testsuite" or root.get("name") != name:
        raise ValueError(f"Unexpected JUnit suite identity: {path}")
    cases = root.findall("testcase")
    if not cases or len(cases) != int(root.get("tests", "-1")):
        raise ValueError(f"Missing or partial JUnit cases in {name}")
    identities = [(case.get("classname", ""), case.get("name", "")) for case in cases]
    if any(not identity[1] for identity in identities) or len(set(identities)) != len(identities):
        raise ValueError(f"Missing/duplicate JUnit case names in {name}")
    failed = sum(case.find("failure") is not None or case.find("error") is not None for case in cases)
    skipped = sum(case.find("skipped") is not None for case in cases)
    if failed or skipped or int(root.get("failures", "0")) or int(root.get("errors", "0")):
        raise ValueError(f"{name}: {failed} failed, {skipped} skipped out of {len(cases)} cases")
    return {"executed": len(cases), "passed": len(cases)}


def execute(command: list[str], env: dict[str, str], log: Path, timeout: float) -> int:
    # Direct dotnet <assembly> execution has no build/restore subprocess. The
    # native windows belong to this process and the OS reclaims them on exit.
    with log.open("wb") as output:
        try:
            return subprocess.run(command, env=env, stdout=output, stderr=subprocess.STDOUT, timeout=timeout, check=False).returncode
        finally:
            output.flush()
            print(log.read_text(encoding="utf-8", errors="replace"), flush=True)


def run(app: Path, output: Path, selector: str, dotnet: str, per_suite: float, total: float) -> int:
    app = app.resolve(strict=True)
    output.mkdir(parents=True, exist_ok=True)
    output = output.resolve()
    # Do not accept evidence from a previous invocation as current execution.
    if any(output.iterdir()):
        raise ValueError(f"Use an empty results directory: {output}")
    env = os.environ.copy()
    env.update(UNODOCK_SELFTEST="1", UNODOCK_TEST_RESULTS=str(output), UNODOCK_TEST_SUITE=selector, UNODOCK_LIST_TESTS="1")
    command = [dotnet, str(app)]
    started = time.monotonic()
    code = execute(command, env, output / "suite-discovery.log", min(per_suite, total))
    if code:
        raise RuntimeError(f"Suite discovery exited with {code}")
    suites = read_manifest(output / "selected-suites.json")
    (output / "execution-plan.json").write_text(json.dumps({"schema": 1, "selector": selector, "suites": suites}, indent=2) + "\n", encoding="utf-8")
    env.pop("UNODOCK_LIST_TESTS")
    results = []
    for name in suites:
        remaining = total - (time.monotonic() - started)
        if remaining <= 0:
            raise TimeoutError("The total desktop acceptance deadline expired.")
        env["UNODOCK_TEST_SUITE"] = name
        print(f"\n=== Real desktop suite: {name} ===", flush=True)
        attempt = time.monotonic()
        error = None
        counts = {}
        try:
            code = execute(command, env, output / (name + ".log"), min(per_suite, remaining))
            if code:
                raise RuntimeError(f"The native host exited with {code}")
            counts = verify_suite(output / (name + ".xml"), name)
        except (OSError, ValueError, ET.ParseError, subprocess.TimeoutExpired, RuntimeError) as failure:
            error = str(failure)
            print(f"FAIL {name}: {error}", file=sys.stderr, flush=True)
        results.append({"suite": name, "seconds": time.monotonic() - attempt, "error": error, **counts})
        # A durable record survives a later timeout without claiming unrun work.
        (output / "isolated-execution.json").write_text(json.dumps({"schema": 1, "planned": suites, "completed": results}, indent=2) + "\n", encoding="utf-8")
    return 1 if any(result["error"] for result in results) else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--selector", default="all")
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--suite-timeout", type=float, default=120)
    parser.add_argument("--total-timeout", type=float, default=900)
    args = parser.parse_args()
    if not 0 < args.suite_timeout <= args.total_timeout < float("inf"):
        parser.error("Timeouts must be finite and 0 < suite-timeout <= total-timeout.")
    try:
        return run(args.app, args.output, args.selector, args.dotnet, args.suite_timeout, args.total_timeout)
    except (OSError, ValueError, ET.ParseError, subprocess.TimeoutExpired, RuntimeError) as error:
        print(f"Desktop acceptance incomplete: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
