#!/usr/bin/env python3
"""Validate Git Flow branch and pull-request metadata."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

DEVELOP_SOURCES = re.compile(r"^(feature|bugfix|chore|docs|test|refactor)/[a-z0-9][a-z0-9._-]*$|^experiment/[a-z0-9][a-z0-9._-]*$|^dependabot/[a-z0-9._/-]+$")
MAIN_SOURCES = re.compile(r"^(release|hotfix)/v?[0-9]+\.[0-9]+\.[0-9]+$")
TITLE = re.compile(r"^(feat|fix|refactor|perf|test|docs|build|ci|chore|revert)(\([a-z0-9._/-]+\))?!?: .{5,100}$")
ISSUE = re.compile(r"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?|refs?)\s+#[0-9]+\b", re.IGNORECASE)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--event", required=True)
    parser.add_argument("--report")
    args = parser.parse_args()
    payload = json.loads(Path(args.event).read_text(encoding="utf-8"))
    pr = payload["pull_request"]
    head = pr["head"]["ref"]
    base = pr["base"]["ref"]
    title = pr["title"].strip()
    body = pr.get("body") or ""
    actor = payload.get("sender", {}).get("login", "")
    automated_dependency_pr = actor == "dependabot[bot]" or head.startswith("dependabot/")
    errors: list[str] = []

    if base == "develop" and not DEVELOP_SOURCES.match(head):
        errors.append(f"`{head}` cannot target `develop`; use feature/, bugfix/, chore/, docs/, test/, refactor/, or experiment/.")
    elif base == "main" and not MAIN_SOURCES.match(head):
        errors.append(f"`{head}` cannot target `main`; only release/<semver> and hotfix/<semver> are allowed.")
    elif base not in {"develop", "main"}:
        errors.append(f"PR base must be `develop` or `main`, not `{base}`.")

    if not TITLE.match(title):
        errors.append("PR title must follow Conventional Commits, for example `feat(xr): add teleport validation`.")
    if not automated_dependency_pr:
        if not ISSUE.search(body):
            errors.append("PR body must link an issue with `Closes #123`, `Fixes #123`, or `Refs #123`.")
        for heading in ("## Summary", "## Verification", "## Data and safety", "## Evidence"):
            if heading not in body:
                errors.append(f"PR body is missing `{heading}` from the template.")

    risk = "CRITICAL" if base == "main" else "UNCLASSIFIED"
    lines = ["# PR policy report", "", f"- Head: `{head}`", f"- Base: `{base}`", f"- Preliminary risk: `{risk}`", f"- Violations: `{len(errors)}`", ""]
    lines.extend(f"- {error}" for error in errors)
    report = "\n".join(lines) + "\n"
    if args.report:
        Path(args.report).write_text(report, encoding="utf-8")
    print(report, end="")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
