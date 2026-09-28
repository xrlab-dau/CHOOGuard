#!/usr/bin/env python3
"""Verdict and job summary for Unity Test Framework runs (NUnit 3 XML).

Usage: unity_results.py MODE:RESULTS_XML:EDITOR_EXIT_CODE [...]
e.g.   unity_results.py EditMode:$RUNNER_TEMP/unity/EditMode-results.xml:0 PlayMode:...:2

`-runTests` exits 0 when every test passed and 2 when some failed, but a compile error or editor crash
leaves no results file at all, so the verdict needs both the XML and the exit code. A mode fails when its
results are missing or unreadable, when zero tests ran, when any test failed, or when the editor exit
code disagrees with the results. Inconclusive tests warn.
"""
from __future__ import annotations

import os
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field

MAX_FAILURES_LISTED = 25


@dataclass
class ModeResult:
    mode: str
    total: int = 0
    passed: int = 0
    failed: int = 0
    skipped: int = 0
    inconclusive: int = 0
    duration: float = 0.0
    failures: list[tuple[str, str]] = field(default_factory=list)
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)


def evaluate(mode: str, xml_path: str, exit_code: str) -> ModeResult:
    result = ModeResult(mode)
    code = int(exit_code) if exit_code.strip().lstrip("-").isdigit() else None
    try:
        root = ET.parse(xml_path).getroot()
    except FileNotFoundError:
        result.errors.append(f"no results file (editor exit {code if code is not None else 'unknown'}): compile error, crash or activation failure; see the {mode} log artifact")
        return result
    except ET.ParseError as error:
        result.errors.append(f"unreadable results file: {error}")
        return result
    run = root if root.tag == "test-run" else root.find(".//test-run")
    if run is None:
        result.errors.append("results file has no <test-run> element")
        return result
    number = lambda name: int(run.get(name, "0") or 0)  # noqa: E731
    result.total, result.passed, result.failed = number("total"), number("passed"), number("failed")
    result.skipped, result.inconclusive = number("skipped"), number("inconclusive")
    result.duration = float(run.get("duration", "0") or 0)
    for case in run.iter("test-case"):
        if case.get("result") == "Failed":
            message = (case.findtext("failure/message") or case.get("label") or "failed").strip()
            result.failures.append((case.get("fullname") or case.get("name") or "?", message))
    if result.total == 0:
        result.errors.append("0 tests ran (filtered out, or the test assemblies did not load)")
    if result.failed:
        result.errors.append(f"{result.failed} test(s) failed")
    if code is None:
        result.errors.append("editor exit code unknown (step did not run?)")
    elif code not in (0, 2) or (code == 2) != (result.failed > 0):
        result.errors.append(f"editor exit code {code} disagrees with the results ({result.failed} failed)")
    if result.inconclusive:
        result.warnings.append(f"{result.inconclusive} inconclusive test(s)")
    return result


def escape(value: str, prop: bool = False) -> str:
    value = value.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
    return value.replace(":", "%3A").replace(",", "%2C") if prop else value


def render(results: list[ModeResult]) -> str:
    lines = ["## Unity tests", "", "| Mode | Total | Passed | Failed | Skipped | Inconclusive | Time | Verdict |", "|---|---:|---:|---:|---:|---:|---:|---|"]
    for r in results:
        verdict = "❌ " + "; ".join(r.errors) if r.errors else ("⚠️ " + "; ".join(r.warnings) if r.warnings else "✅ pass")
        lines.append(f"| {r.mode} | {r.total} | {r.passed} | {r.failed} | {r.skipped} | {r.inconclusive} | {r.duration / 60:.1f} min | {verdict} |")
    failures = [(r.mode, name, message) for r in results for name, message in r.failures]
    if failures:
        lines += ["", "<details open><summary>Failed tests</summary>", ""]
        for mode, name, message in failures[:MAX_FAILURES_LISTED]:
            first = message.splitlines()[0] if message else ""
            lines.append(f"- `{mode}` **{name}** — {first}")
        if len(failures) > MAX_FAILURES_LISTED:
            lines.append(f"- … {len(failures) - MAX_FAILURES_LISTED} more in the results artifact")
        lines += ["", "</details>"]
    return "\n".join(lines) + "\n"


def main(argv: list[str]) -> int:
    if not argv:
        print(__doc__, file=sys.stderr)
        return 64
    results = []
    for spec in argv:
        mode, xml_path, exit_code = spec.split(":", 2)
        results.append(evaluate(mode, xml_path, exit_code))
    for r in results:
        for name, message in r.failures[:MAX_FAILURES_LISTED]:
            print(f"::error title={escape(r.mode + ' ' + name, True)}::{escape(message)}")
        for error in r.errors:
            print(f"::error title={escape('Unity ' + r.mode, True)}::{escape(error)}")
        for warning in r.warnings:
            print(f"::warning title={escape('Unity ' + r.mode, True)}::{escape(warning)}")
    report = render(results)
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_path:
        with open(summary_path, "a", encoding="utf-8") as handle:
            handle.write(report)
    else:
        print(report)
    return 1 if any(r.errors for r in results) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
