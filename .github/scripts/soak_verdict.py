#!/usr/bin/env python3
"""Verdict for a player smoke run in the soak mode (ChooGuard.App.Fps.Shell.SoakRun).

Usage: soak_verdict.py REPORT_JSON EXPECTED_SHIFTS OS_LABEL [PLAYER_EXIT_CODE]

The player writes the report only after every shift has loaded the station and a fresh emergency session, played,
and returned to the title; it then quits with 0. A missing report means a crash, hang or early quit. The run fails
when the report is missing, when shifts are missing, when any shift logged an exception, or when a shift's session
never spawned its crowd. Logged errors only warn: headless players (-nographics) log some graphics-only errors.
"""
from __future__ import annotations

import json
import os
import sys


def evaluate(report: dict | None, expected_shifts: int, exit_code: str = "0") -> tuple[list[str], list[str]]:
    errors: list[str] = []
    warnings: list[str] = []
    if report is None:
        return [f"no soak report (player exit {exit_code or 'unknown'}): crash, hang or early quit; see the player log"], []
    runs = report.get("runs") or []
    if len(runs) != expected_shifts:
        errors.append(f"{len(runs)} of {expected_shifts} shift(s) completed")
    for run in runs:
        label = f"shift {run.get('index')}"
        if run.get("exceptions", 0):
            errors.append(f"{label}: {run['exceptions']} exception(s), first: {run.get('firstError')}")
        if not run.get("people", 0):
            errors.append(f"{label}: the emergency session never spawned its crowd")
        if run.get("errors", 0) and not run.get("exceptions", 0):
            warnings.append(f"{label}: {run['errors']} logged error(s), first: {run.get('firstError')}")
    if exit_code not in ("", "0"):
        errors.append(f"player exited {exit_code} after writing the report")
    return errors, warnings


def render(os_label: str, report: dict | None, errors: list[str], warnings: list[str]) -> str:
    lines = [f"## Player smoke run ({os_label})", ""]
    if report:
        lines.append(f"device `{report.get('device')}`, gpu `{report.get('gpu')}`")
        lines += ["", "| Shift | Load (s) | People | Errors | Exceptions | Max frame (ms) |", "|---:|---:|---:|---:|---:|---:|"]
        for run in report.get("runs") or []:
            lines.append(f"| {run.get('index')} | {run.get('loadSeconds', 0):.1f} | {run.get('people')} | {run.get('errors')} | "
                         f"{run.get('exceptions')} | {run.get('maxFrameMs', 0):.0f} |")
    verdict = "❌ " + "; ".join(errors) if errors else ("⚠️ " + "; ".join(warnings) if warnings else "✅ pass")
    return "\n".join(lines + ["", f"**Verdict:** {verdict}", ""])


def main(argv: list[str]) -> int:
    if len(argv) not in (3, 4):
        print(__doc__, file=sys.stderr)
        return 64
    path, expected, os_label = argv[0], int(argv[1]), argv[2]
    exit_code = argv[3] if len(argv) == 4 else "0"
    try:
        with open(path, encoding="utf-8") as handle:
            report = json.load(handle)
    except (FileNotFoundError, ValueError):
        report = None
    errors, warnings = evaluate(report, expected, exit_code)
    for message in errors:
        print(f"::error title=Player smoke ({os_label})::{message}")
    for message in warnings:
        print(f"::warning title=Player smoke ({os_label})::{message}")
    summary = render(os_label, report, errors, warnings)
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as handle:
            handle.write(summary)
    else:
        print(summary)
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
