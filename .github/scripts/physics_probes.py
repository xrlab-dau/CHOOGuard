#!/usr/bin/env python3
"""Verdict for workers/physics/verification/run_probes.py, which always exits 0 and rewrites its receipt.

Usage: physics_probes.py COMMITTED_RECEIPT FRESH_RECEIPT

Fails when a probe fails, when the committed receipt no longer describes the committed script or pinned
sources (evidence left stale after an edit), or when a case's outcome or completion time moved by more than
one integration step (dt = 0.05 s) from the committed evidence.
"""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

SCRIPT = Path("workers/physics/verification/run_probes.py")
DT = 0.05


def compare(committed: dict, fresh: dict, script_sha256: str) -> list[str]:
    errors = [f"probe {r['case']} failed: {json.dumps(r, sort_keys=True)}" for r in fresh["results"] if not r.get("passed")]
    if committed.get("script_sha256") != script_sha256:
        errors.append("committed receipt.json was produced by a different run_probes.py; re-run it and commit the receipt")
    if committed.get("source_digests") != fresh.get("source_digests"):
        errors.append("pinned solver sources changed without a new receipt")
    previous = {r["case"]: r for r in committed.get("results", [])}
    for result in fresh["results"]:
        before = previous.get(result["case"])
        if before is None:
            errors.append(f"probe {result['case']} has no committed evidence")
            continue
        if before.get("passed") != result.get("passed"):
            errors.append(f"probe {result['case']} outcome changed: {before.get('passed')} -> {result.get('passed')}")
        for key in ("elapsed_s", "narrow_s", "wide_s"):
            if key in before and key in result and abs(before[key] - result[key]) > DT + 1e-9:
                errors.append(f"probe {result['case']} {key} moved {before[key]:.3f} -> {result[key]:.3f} s (more than one dt)")
    return errors


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print(__doc__, file=sys.stderr)
        return 64
    committed, fresh = (json.loads(Path(p).read_text(encoding="utf-8")) for p in argv)
    errors = compare(committed, fresh, hashlib.sha256(SCRIPT.read_bytes()).hexdigest())
    for error in errors:
        print(f"::error title=Physics probes::{error}")
    print(f"physics probes: {len(fresh['results'])} case(s), {len(errors)} problem(s); jupedsim {fresh.get('jupedsim_version')} on {fresh.get('platform')}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
