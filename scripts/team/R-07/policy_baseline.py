#!/usr/bin/env python3
"""R-07 policy baseline gate for the bootstrap verifier (isolated candidate; no network, no installs).

scripts/bootstrap/verify_toolchain.py records policy_sha256 but never compares it with an expected
baseline, so a policy byte change leaves required_ok and its exit code unchanged. This module reuses
the verifier's own POLICY_GLOBS, REQUIRED_POLICY_GLOBS and sha256 and rejects:

- missing_path              a baseline policy file is absent
- empty_file                a baseline policy file is empty
- hash_drift                a baseline policy file's bytes changed
- unexpected_file           a file newly matches the policy scope
- required_glob_unmatched   a required policy glob has no non-empty file
- scope_changed             the verifier's current POLICY_GLOBS differ from the baseline scope

Exit codes: 0 = state ok, 1 = findings (state cannot_proceed), 2 = a target input (root, baseline,
receipt, profile or output) is absent, invalid or would be overwritten; nothing is checked.
Outputs name repository-relative paths and fixed codes only, never absolute paths or file contents.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path, PurePosixPath

REPO = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(REPO / "scripts" / "bootstrap"))
import verify_toolchain as verifier  # noqa: E402  read-only reuse of the canonical verifier

SCHEMA_VERSION = 1
BASELINE_KIND = "r07-policy-baseline"
RESULT_KIND = "r07-policy-baseline-result"
EXIT_OK, EXIT_FINDINGS, EXIT_CANNOT_START = 0, 1, 2
FINDING_CODES = ("missing_path", "empty_file", "hash_drift", "unexpected_file", "required_glob_unmatched", "scope_changed")


class TargetError(ValueError):
    """A target input cannot be used. The message is a fixed code, never a path or content."""


def portable(name: str) -> bool:
    path = PurePosixPath(name)
    return (bool(name) and not path.is_absolute() and ".." not in path.parts and "\\" not in name
            and ":" not in name and path.parts[0] != ".git")


def scope_files(root: Path, globs: list[str]) -> list[str]:
    """The same selection verify_toolchain.main() hashes: every file matched by a policy glob."""
    found = set()
    for pattern in globs:
        for path in root.glob(pattern):
            if path.is_file():
                found.add(path.relative_to(root).as_posix())
    return sorted(found)


def unmatched_required(root: Path, required: list[str]) -> list[str]:
    return [g for g in required if not any(p.is_file() and p.stat().st_size > 0 for p in root.glob(g))]


def require_root(root: Path) -> Path:
    if not root.is_dir():
        raise TargetError("target_root_missing")
    return root.resolve()


def build(root: Path, globs: list[str] | None = None, required: list[str] | None = None, source_ref: str = "") -> dict:
    root = require_root(root)
    globs = list(verifier.POLICY_GLOBS if globs is None else globs)
    required = list(verifier.REQUIRED_POLICY_GLOBS if required is None else required)
    if unmatched_required(root, required):
        raise TargetError("baseline_source_required_glob_unmatched")
    files = {}
    for name in scope_files(root, globs):
        size = (root / name).stat().st_size
        if size == 0:
            raise TargetError("baseline_source_empty_file")
        files[name] = {"sha256": verifier.sha256(root / name), "bytes": size}
    if not files:
        raise TargetError("baseline_source_empty_scope")
    return {"schemaVersion": SCHEMA_VERSION, "kind": BASELINE_KIND, "sourceRef": source_ref,
            "verifier": "scripts/bootstrap/verify_toolchain.py", "globs": globs, "requiredGlobs": required, "files": files}


def validate_baseline(baseline) -> dict:
    ok = (isinstance(baseline, dict) and baseline.get("schemaVersion") == SCHEMA_VERSION and baseline.get("kind") == BASELINE_KIND
          and isinstance(baseline.get("globs"), list) and baseline["globs"] and all(isinstance(g, str) and g for g in baseline["globs"])
          and isinstance(baseline.get("requiredGlobs"), list) and all(isinstance(g, str) and g for g in baseline["requiredGlobs"])
          and isinstance(baseline.get("files"), dict) and baseline["files"])
    if ok:
        for name, entry in baseline["files"].items():
            if not (portable(name) and isinstance(entry, dict) and isinstance(entry.get("sha256"), str)
                    and len(entry["sha256"]) == 64 and all(c in "0123456789abcdef" for c in entry["sha256"])
                    and isinstance(entry.get("bytes"), int) and not isinstance(entry.get("bytes"), bool) and entry["bytes"] > 0):
                ok = False
                break
    if not ok:
        raise TargetError("baseline_invalid")
    return baseline


def digest_json(value) -> str:
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()


def result(baseline: dict, findings: list[dict], subject: str) -> dict:
    findings = sorted(findings, key=lambda f: (FINDING_CODES.index(f["code"]), f["path"]))
    counts = {code: sum(1 for f in findings if f["code"] == code) for code in FINDING_CODES}
    return {"schemaVersion": SCHEMA_VERSION, "kind": RESULT_KIND, "subject": subject,
            "state": "ok" if not findings else "cannot_proceed", "baselineSha256": digest_json(baseline),
            "baselineFiles": len(baseline["files"]), "findings": findings, "counts": counts}


def check(root: Path, baseline: dict, verifier_globs: list[str] | None = None) -> dict:
    baseline = validate_baseline(baseline)
    root = require_root(root)
    findings = []
    for name, expected in baseline["files"].items():
        path = root / name
        if not path.is_file():
            findings.append({"code": "missing_path", "path": name})
        elif path.stat().st_size == 0:
            findings.append({"code": "empty_file", "path": name})
        elif verifier.sha256(path) != expected["sha256"]:
            findings.append({"code": "hash_drift", "path": name})
    findings += [{"code": "unexpected_file", "path": name} for name in scope_files(root, baseline["globs"]) if name not in baseline["files"]]
    findings += [{"code": "required_glob_unmatched", "path": g} for g in unmatched_required(root, baseline["requiredGlobs"])]
    current = list(verifier.POLICY_GLOBS if verifier_globs is None else verifier_globs)
    if current != baseline["globs"]:
        findings += [{"code": "scope_changed", "path": g} for g in sorted(set(current) ^ set(baseline["globs"]))] or \
                    [{"code": "scope_changed", "path": "(order)"}]
    return result(baseline, findings, "worktree")


def check_receipt(receipt, baseline: dict) -> dict:
    """Compare a verify_toolchain receipt's policy_sha256 with the baseline, without re-reading files."""
    baseline = validate_baseline(baseline)
    hashes = receipt.get("policy_sha256") if isinstance(receipt, dict) else None
    if (not isinstance(hashes, dict) or not hashes or receipt.get("record_type") != "machine_observation_not_approval"
            or any(not portable(k) or not isinstance(v, str) or len(v) != 64 for k, v in hashes.items())):
        raise TargetError("receipt_invalid")
    findings = []
    for name, expected in baseline["files"].items():
        if name not in hashes:
            findings.append({"code": "missing_path", "path": name})
        elif hashes[name] != expected["sha256"]:
            findings.append({"code": "hash_drift", "path": name})
    findings += [{"code": "unexpected_file", "path": name} for name in sorted(hashes) if name not in baseline["files"]]
    return result(baseline, findings, "verifier_receipt")


def authority(root: Path, globs: list[str], profile) -> dict:
    """Two policy authorities side by side. Reports differences; never picks one."""
    root = require_root(root)
    sources = profile.get("policy", {}).get("policySources") if isinstance(profile, dict) else None
    if not isinstance(sources, list) or not sources or any(not isinstance(s, dict) or not portable(str(s.get("path", ""))) for s in sources):
        raise TargetError("profile_invalid")
    verifier_scope = set(scope_files(root, globs))
    profile_paths = {s["path"] for s in sources}
    drift = sorted(s["path"] for s in sources if (root / s["path"]).is_file() and s.get("sha256AtRef")
                   and verifier.sha256(root / s["path"]) != s["sha256AtRef"])
    absent = sorted(p for p in profile_paths if not (root / p).is_file())
    both, only_verifier, only_profile = sorted(verifier_scope & profile_paths), sorted(verifier_scope - profile_paths), sorted(profile_paths - verifier_scope)
    return {"schemaVersion": SCHEMA_VERSION, "kind": "r07-policy-authority-comparison",
            "state": "authority_unresolved" if only_verifier or only_profile else "aligned",
            "verifierScopeFiles": len(verifier_scope), "profilePolicySources": len(profile_paths),
            "inBoth": both, "onlyVerifierScope": only_verifier, "onlyProfilePolicySources": only_profile,
            "profileSha256AtRefDiffersFromWorktree": drift, "profileSourcesAbsentFromWorktree": absent,
            "rule": "Differences are preserved for a PM authority decision; this comparison does not choose a policy source."}


def read_json(path: Path, code: str):
    if not path.is_file():
        raise TargetError(code + "_missing")
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, ValueError):
        raise TargetError(code + "_invalid") from None


def write_new(path: Path, value) -> None:
    if path.exists() or path.is_symlink():
        raise TargetError("output_exists")
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        with path.open("x", encoding="utf-8", newline="\n") as stream:
            stream.write(json.dumps(value, ensure_ascii=False, indent=2) + "\n")
    except FileExistsError:
        raise TargetError("output_exists") from None


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    make = sub.add_parser("build", help="record the current policy scope as a new baseline file")
    make.add_argument("--root", type=Path, default=REPO)
    make.add_argument("--output", type=Path, required=True)
    make.add_argument("--source-ref", default="")
    verify = sub.add_parser("check", help="compare a worktree (and optionally a verifier receipt) with a baseline")
    verify.add_argument("--root", type=Path, default=REPO)
    verify.add_argument("--baseline", type=Path, required=True)
    verify.add_argument("--receipt", type=Path)
    verify.add_argument("--output", type=Path)
    compare = sub.add_parser("authority", help="compare verifier policy scope with an execution profile's policy sources")
    compare.add_argument("--root", type=Path, default=REPO)
    compare.add_argument("--profile", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        if args.command == "build":
            baseline = build(args.root, source_ref=args.source_ref)
            write_new(args.output, baseline)
            print(json.dumps({"state": "baseline_written", "files": len(baseline["files"]), "baselineSha256": digest_json(baseline)}))
            return EXIT_OK
        if args.command == "authority":
            report = authority(args.root, list(verifier.POLICY_GLOBS), read_json(args.profile, "profile"))
            print(json.dumps(report, ensure_ascii=False, indent=2))
            return EXIT_OK if report["state"] == "aligned" else EXIT_FINDINGS
        if args.output is not None and (args.output.exists() or args.output.is_symlink()):
            raise TargetError("output_exists")
        baseline = validate_baseline(read_json(args.baseline, "baseline"))
        receipt = read_json(args.receipt, "receipt") if args.receipt is not None else None
        reports = [check(args.root, baseline)]
        if receipt is not None:
            reports.append(check_receipt(receipt, baseline))
        combined = {"schemaVersion": SCHEMA_VERSION, "kind": RESULT_KIND + "-set",
                    "state": "ok" if all(r["state"] == "ok" for r in reports) else "cannot_proceed", "results": reports}
        if args.output is not None:
            write_new(args.output, combined)
        print(json.dumps(combined, ensure_ascii=False, indent=2))
        return EXIT_OK if combined["state"] == "ok" else EXIT_FINDINGS
    except TargetError as error:
        print(json.dumps({"schemaVersion": SCHEMA_VERSION, "kind": RESULT_KIND, "state": "cannot_proceed", "error": str(error)}))
        return EXIT_CANNOT_START


if __name__ == "__main__":
    sys.exit(main())
