#!/usr/bin/env python3
"""Validate Git Flow branch and pull-request metadata."""

from __future__ import annotations

import argparse
import json
import re
import sys
import traceback
from pathlib import Path

DEVELOP_SOURCES = re.compile(r"^(feature|bugfix|chore|docs|test|refactor)/[a-z0-9][a-z0-9._-]*$|^experiment/[a-z0-9][a-z0-9._-]*$|^dependabot/[a-z0-9._/-]+$")
MAIN_SOURCES = re.compile(r"^(release|hotfix)/v?[0-9]+\.[0-9]+\.[0-9]+$")
TITLE = re.compile(r"^(feat|fix|refactor|perf|test|docs|build|ci|chore|revert)(\([a-z0-9._/-]+\))?!?: .{5,100}$")
ISSUE = re.compile(r"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?|refs?)\s+#[0-9]+\b", re.IGNORECASE)


class EventPayloadError(Exception):
    """The event payload could not be read, parsed, or understood.

    Raised instead of letting an OSError/JSONDecodeError/KeyError/TypeError
    escape as a raw traceback, so the caller can name the failure precisely and
    return the reserved "could not evaluate" exit code.
    """


def load_pull_request(event_path: str) -> tuple[dict, str]:
    """Return the pull_request object and the sender login from an event file.

    Every field this gate reads is extracted here so a malformed payload fails
    with one specific message rather than an arbitrary KeyError deep in _run().
    """
    try:
        raw = Path(event_path).read_text(encoding="utf-8")
    except OSError as exc:
        raise EventPayloadError(f"cannot read event payload {event_path!r}: {exc}") from exc

    try:
        payload = json.loads(raw)
    except json.JSONDecodeError as exc:
        raise EventPayloadError(f"event payload {event_path!r} is not valid JSON: {exc}") from exc

    if not isinstance(payload, dict):
        raise EventPayloadError(f"event payload {event_path!r} is {type(payload).__name__}, not a JSON object")

    pr = payload.get("pull_request")
    if not isinstance(pr, dict):
        raise EventPayloadError(
            f"event payload {event_path!r} has no `pull_request` object; "
            "this gate only understands pull_request events"
        )
    for key in ("head", "base"):
        ref = pr.get(key)
        if not isinstance(ref, dict) or not isinstance(ref.get("ref"), str):
            raise EventPayloadError(f"event payload {event_path!r} has no string `pull_request.{key}.ref`")

    # `sender` can be present-but-null in a payload, which a plain
    # payload.get("sender", {}) would hand straight through as None.
    sender = payload.get("sender") or {}
    actor = sender.get("login") or "" if isinstance(sender, dict) else ""
    return pr, actor


def main() -> int:
    # Exit code contract, matching scripts/ci/repository_policy.py so the
    # workflow and a human reading the Checks tab can tell both gates apart
    # the same way without opening the log:
    #   0 = metadata checked, no violations.
    #   1 = metadata checked, violations found.
    #   2 = metadata checked but the report could not be written to --report.
    #   3 = the check could not be evaluated: an unreadable or malformed event
    #       payload, or an unexpected crash. Never confusable with 1, which a
    #       raw unhandled exception used to be -- Python exits 1 on an
    #       uncaught exception, indistinguishable from a real, successfully
    #       computed "violations found" result.
    #
    # Fail-closed, unlike repository_policy.py. That gate degrades to scanning
    # all tracked files, a strict superset of the changed-file scope, so it can
    # still return a meaningful 0/1 when its scope is unavailable. This gate has
    # no equivalent fallback: if the event payload cannot be read, there is no
    # other source for the head ref, base ref, title, or body, so there is
    # nothing to check and no basis for reporting a pass. Returning 3 blocks the
    # step rather than letting an unevaluated gate look clean.
    parser = argparse.ArgumentParser()
    parser.add_argument("--event", required=True)
    parser.add_argument("--report")
    args = parser.parse_args()

    try:
        return _run(args)
    except EventPayloadError as exc:
        # Anticipated input failure: name it exactly, and do not pretend the
        # metadata was checked. No report is written, which the workflow
        # already handles by emitting its own ::error:: for a missing report
        # instead of letting `cat` mask this exit status under bash -eo
        # pipefail.
        print(f"::error::PR policy gate could not evaluate the pull request: {exc}; "
              "this is exit code 3, distinct from 1 (violations found) and 2 (report write failed)")
        return 3
    except Exception as exc:  # noqa: BLE001 - deliberate top-level safety net
        print(f"::error::PR policy gate crashed unexpectedly ({exc.__class__.__name__}: {exc}); "
              "this is exit code 3, distinct from 1 (violations found) and 2 (report write failed); "
              "see the job log below for the full traceback")
        traceback.print_exc()
        return 3


def _run(args: argparse.Namespace) -> int:
    pr, actor = load_pull_request(args.event)
    head = pr["head"]["ref"]
    base = pr["base"]["ref"]
    # A null title/body is valid JSON but not a string; treat it as empty so it
    # is reported as a policy violation rather than crashing the gate.
    title = (pr.get("title") or "").strip()
    body = pr.get("body") or ""
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

    write_failed = False
    if args.report:
        try:
            Path(args.report).write_text(report, encoding="utf-8")
        except OSError as exc:
            # The metadata check already finished; dying here would throw that
            # result away and leave the workflow to report a missing file
            # instead of the real outcome. Print the computed report to stdout
            # regardless, name the path failure in its own annotation, and use
            # the reserved code 2 so it cannot be read as a policy violation.
            write_failed = True
            print(f"::error::failed to write PR policy report to {args.report!r}: {exc}")
    print(report, end="")
    if write_failed:
        return 2
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
