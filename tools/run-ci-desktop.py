#!/usr/bin/env python3
"""Run every desktop acceptance group of one CI job against a single build.

Each group is an isolated run-desktop-tests.py invocation with its own results
directory and evidence verifier, so a group's gate sees exactly its suites. The
final "remaining" group runs every other registered suite once, excluding what
the groups already executed (Linux and Windows). On Linux, the floating chrome group runs under a
real window manager (Openbox), started only for that group.

Every group runs even after an earlier failure; the exit code is non-zero if any
group or verifier failed. Linux must run inside a dedicated X display (xvfb-run).
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
SYSTEM = platform.system()


def tool(*args: str) -> list[str]:
    return [sys.executable, *args]


def groups(revision: str) -> list[dict]:
    """(name, selector, results subdirectory, verifier, extra environment)."""
    verify = lambda check: tool(str(ROOT / "tools/verify-suite-evidence.py"), check)
    return [
        {"name": "native", "selector": "desktop-acceptance", "dir": "native",
         "verify": tool(str(ROOT / "tools/verify-native-results.py")), "env": {"UNODOCK_INPUT_TRACE": "1"}},
        *({"name": suite, "selector": suite, "dir": "inspector/" + suite} for suite in ("native-inspector", "inspector-quality", "sample-quality")),
        {"name": "inspector", "verify_only": True, "dir": "inspector",
         "verify": tool(str(ROOT / "tools/verify-native-inspector.py")), "verify_args": ["--revision", revision]},
        {"name": "fluent-navigator", "selector": "fluent-navigator", "dir": "fluent-navigator", "verify": verify("fluent-navigator")},
        {"name": "fluent-presentation", "selector": "fluent-presentation", "dir": "fluent"},
        {"name": "fluent-state-resources", "selector": "fluent-state-resources", "dir": "fluent/states",
         "verify": verify("fluent-presentation"), "verify_dir": "fluent"},
        {"name": "docking-sizing", "selector": "docking-sizing", "dir": "sizing", "verify": verify("docking-sizing")},
        {"name": "xaml-workbench", "selector": "xaml-workbench", "dir": "xaml-workbench", "verify": verify("xaml-workbench")},
        {"name": "xaml-workspaces", "selector": "xaml-workspaces", "dir": "xaml-workspaces", "verify": verify("xaml-workspaces")},
        {"name": "floating-chrome", "selector": "floating-chrome", "dir": "chrome", "wm": SYSTEM == "Linux",
         "verify": tool(str(ROOT / "tools/verify-floating-chrome.py")),
         "env": {"UNODOCK_REQUIRE_WM": "1" if SYSTEM == "Linux" else "0"}},
        {"name": "remaining", "remaining": True, "dir": "remaining"},
    ]


def start_window_manager() -> subprocess.Popen:
    log = open(os.environ.get("RUNNER_TEMP", "/tmp") + "/unodock-openbox.log", "wb")
    manager = subprocess.Popen(["openbox", "--config-file", "/etc/xdg/openbox/rc.xml"], stdout=log, stderr=subprocess.STDOUT)
    for _ in range(100):
        probe = subprocess.run(["xprop", "-root", "_NET_SUPPORTING_WM_CHECK"], capture_output=True, text=True)
        if "window id" in probe.stdout:
            return manager
        time.sleep(0.05)
    manager.kill()
    raise RuntimeError("Openbox did not become the active window manager.")


def discover(app: Path, selector: str, scratch: Path) -> list[str]:
    """Suites a selector runs, from the application's own registry, without running them."""
    scratch.mkdir(parents=True, exist_ok=True)
    env = os.environ.copy()
    env.update(UNODOCK_SELFTEST="1", UNODOCK_TEST_RESULTS=str(scratch), UNODOCK_TEST_SUITE=selector, UNODOCK_LIST_TESTS="1")
    command = [str(app)] if app.suffix.lower() == ".exe" else ["dotnet", str(app)]
    subprocess.run(command, env=env, check=True, timeout=300, stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)
    suites = json.loads((scratch / "selected-suites.json").read_text(encoding="utf-8"))
    shutil.rmtree(scratch, ignore_errors=True)
    return suites


def planned(directory: Path) -> list[str]:
    try:
        return json.loads((directory / "execution-plan.json").read_text(encoding="utf-8"))["suites"]
    except (OSError, ValueError, KeyError):
        return []


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    # Pull-request CI stays lean: Linux runs every remaining suite, Windows its
    # acceptance set, and macOS (a small hosted display) the focused groups.
    default = {"Windows": "windows-acceptance", "Linux": "all"}.get(SYSTEM, "")
    parser.add_argument("--remaining-selector", default=default, help="Selector for the remaining suites; empty skips them.")
    parser.add_argument("--only", default="", help="Comma-separated group names (local reproduction).")
    # CI runs the two halves as parallel jobs. "remaining" excludes the suites the focused
    # groups would run, discovered from the application without running them.
    parser.add_argument("--part", choices=["all", "groups", "remaining"], default="all", help="Run the focused groups, the remaining suites, or both.")
    args = parser.parse_args()
    revision = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, capture_output=True, text=True, check=True).stdout.strip()
    only = {name for name in args.only.split(",") if name}
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    (output / "source-revision.txt").write_text(revision + "\n")
    runner = [*tool(str(ROOT / "tools/run-desktop-tests.py")), "--app", str(args.app), "--suite-timeout", "300"]
    executed: list[str] = []
    summary = []
    if args.part == "remaining":
        for group in groups(revision):
            if "selector" in group:
                executed += [suite for suite in discover(args.app.resolve(), group["selector"], output / ".discovery") if suite not in executed]
    for group in groups(revision):
        if only and group["name"] not in only or group.get("remaining") and not args.remaining_selector:
            continue
        if args.part == "groups" and group.get("remaining") or args.part == "remaining" and not group.get("remaining"):
            continue
        directory = output / group["dir"]
        started = time.monotonic()
        failures = []
        print(f"\n##### Desktop group: {group['name']} #####", flush=True)
        if not group.get("verify_only"):
            env = os.environ.copy()
            env.update(group.get("env", {}))
            selector = args.remaining_selector if group.get("remaining") else group["selector"]
            command = [*runner, "--output", str(directory), "--selector", selector, "--total-timeout", "2400" if group.get("remaining") else "900"]
            if group.get("remaining") and executed:
                command += ["--exclude", ",".join(executed)]
            manager = start_window_manager() if group.get("wm") else None
            try:
                if subprocess.run(command, env=env, check=False).returncode:
                    failures.append("suites")
            finally:
                if manager:
                    manager.terminate()
                    manager.wait(10)
            executed += [suite for suite in planned(directory) if suite not in executed]
        if group.get("verify"):
            directory = output / group.get("verify_dir", group["dir"])
            directory.mkdir(parents=True, exist_ok=True)
            (directory / "source-revision.txt").write_text(revision + "\n")
            if subprocess.run([*group["verify"], str(directory), *group.get("verify_args", [])], check=False).returncode:
                failures.append("evidence")
        summary.append({"group": group["name"], "seconds": round(time.monotonic() - started, 1), "failures": failures})
        print(f"##### {group['name']}: {'FAILED ' + '+'.join(failures) if failures else 'passed'} in {summary[-1]['seconds']}s", flush=True)
    (output / "ci-desktop-summary.json").write_text(json.dumps({"schema": 1, "platform": SYSTEM, "part": args.part, "revision": revision, "groups": summary}, indent=2) + "\n")
    failed = [entry["group"] for entry in summary if entry["failures"]]
    print("\nDesktop groups failed: " + ", ".join(failed) if failed else "\nAll desktop groups passed.", flush=True)
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
