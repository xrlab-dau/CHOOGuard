#!/usr/bin/env python3
"""Synthetic M1-01 lease decisions carried through the M1-05 record pipeline."""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import subprocess
import tempfile
from pathlib import Path

from jsonschema import Draft202012Validator
from jsonschema.exceptions import SchemaError

REPO = Path(__file__).resolve().parents[3]
SOURCES = (
    "docs/team/M1-01/session-contract.json",
    "scripts/team/M1-01/writer_lease.py",
    "docs/team/M1-05/record-schema.json",
    "scripts/team/M1-05/record_pipeline.py",
    "scripts/team/M1-06/contention.py",
    "scripts/team/M1-06/test_contention.py",
)


def module(name, relative):
    spec = importlib.util.spec_from_file_location(name, REPO / relative)
    loaded = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(loaded)
    return loaded
BASE_A, BASE_B = "a" * 40, "b" * 40
lease = pipeline = None


def load_suppliers():
    """Load suppliers only after main() has verified committed source bytes."""
    global lease, pipeline
    if lease is None:
        lease = module("m106_lease", SOURCES[1])
        pipeline = module("m106_pipeline", SOURCES[3])
    return lease, pipeline


def schema_errors(record):
    _, record_pipeline = load_suppliers()
    schema = json.loads((REPO / SOURCES[2]).read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(schema)
    return [error.message for error in Draft202012Validator(schema).iter_errors(record)]


def run_fixture(inject_precondition_failure=False):
    lease_module, record_pipeline = load_suppliers()
    contract = lease_module.read_json(REPO / SOURCES[0])
    cases, registries = [], {}
    at, wf = lease_module.at, lease_module.wf
    with tempfile.TemporaryDirectory(prefix="m1-06-synthetic-") as temporary:
        root = Path(temporary)

        def registry(name):
            registries[name] = lease_module.LeaseRegistry(contract, root / (name + ".jsonl"))
            return registries[name]

        def record(name, group, result, ok=True, reason=None, kind="tool", scope=None):
            current = result["lease"] or {}
            entry = registries[group].journal()[-1]
            event_kind = kind if result["ok"] else "failure"
            event_outcome = "failed" if not result["ok"] else ("cancelled" if kind == "cancel" else "recorded")
            cases.append({"id": name, "registry": group, "journalSeq": result["journalSeq"],
                          "accepted": result["ok"], "reason": result["reason"],
                          "expected": {"accepted": ok, "reason": reason},
                          "passed": result["ok"] == ok and result["reason"] == reason,
                          "holder": entry["holder"], "baseRef": entry["baseRef"],
                          "fence": current.get("fence"), "scope": scope or current.get("resource"),
                          "details": result["details"], "eventKind": event_kind, "eventOutcome": event_outcome})
            return current.get("leaseId")

        resources = {
            "workspace-file": wf("Assets/Synthetic.fixture"),
            "unity-project": {"kind": "unity-project", "workspaceId": "ws-1"},
            "unity-editor": {"kind": "unity-editor", "editorInstanceId": "synthetic-editor"},
            "generated-output": {"kind": "generated-output", "workspaceId": "ws-1", "root": "Exports/"},
            "manifest": wf("Packages/manifest.json"),
        }
        for name, resource in resources.items():
            r = registry(name)
            record(name + "-acquire", name, r.acquire("synthetic-a", resource, BASE_A, at(0), at(30), lease_module.SHA_X))
            record(name + "-conflict", name, r.acquire("synthetic-b", resource, BASE_B, at(1), at(31), lease_module.SHA_X),
                   False, "overlap_with_active", scope=resource)

        name, resource = "handoff", wf("Assets/Synthetic.fixture")
        r = registry(name)
        old = record("handoff-start", name, r.acquire("" if inject_precondition_failure else "synthetic-a", resource, BASE_A, at(0), at(30), lease_module.SHA_X))
        if old is None:
            cases.append({"id": "handoff-dependent-skipped", "registry": name, "journalSeq": None,
                          "accepted": False, "reason": "dependent_step_not_run",
                          "expected": {"accepted": False, "reason": "dependent_step_not_run"}, "passed": True,
                          "holder": None, "baseRef": None, "fence": None, "scope": resource, "details": {},
                          "eventKind": "failure", "eventOutcome": "failed"})
        else:
            record("handoff", name, r.handoff(old, "synthetic-a", 1, at(1), {
                "nextSessionId": "synthetic-b", "outputs": ["synthetic fixture"],
                "tests": ["synthetic assertions"], "knownLimits": ["no live Editor"]}))
            new = record("handoff-acquire", name, r.acquire("synthetic-b", resource, BASE_B, at(2), at(32), lease_module.SHA_X), kind="retry")
            record("old-holder-write", name, r.write(old, "synthetic-a", 1, resource["path"], at(3), lease_module.SHA_Y),
                   False, "transition_not_in_contract")
            record("cancel", name, r.release(new, "synthetic-b", 2, at(4)), kind="cancel")

        r = registry("unknown")
        record("unknown-holder", "unknown", r.acquire("", resource, BASE_A, at(0), at(30), lease_module.SHA_X),
               False, "lease_fields_unrecordable", scope=resource)

        r = registry("expiry")
        old = record("expiry-start", "expiry", r.acquire("synthetic-a", resource, BASE_A, at(0), at(10), lease_module.SHA_X))
        for result in r.expire_due(at(11)):
            record("expire", "expiry", result)
        record("expired-conflict", "expiry", r.acquire("synthetic-b", resource, BASE_B, at(12), at(32), lease_module.SHA_X),
               False, "overlap_with_expired", scope=resource)
        record("expiry-recover", "expiry", r.recover(old, "synthetic-pm", "role:pm", lease_module.SHA_X, at(13)))
        record("recovery-retry", "expiry", r.acquire("synthetic-b", resource, BASE_B, at(14), at(34), lease_module.SHA_X), kind="retry")

        for dirty in (False, True):
            name = "dirty" if dirty else "crash"
            r = registry(name)
            old = record(name + "-start", name, r.acquire("synthetic-a", resource, BASE_A, at(0), at(30), lease_module.SHA_X))
            record(name + "-write", name, r.write(old, "synthetic-a", 1, resource["path"], at(1), lease_module.SHA_Y))
            record(name + "-detected", name, r.crash_detected(old, at(2)))
            record(name + "-conflict", name, r.acquire("synthetic-b", resource, BASE_B, at(3), at(33), lease_module.SHA_Y),
                   False, "overlap_with_crash_suspected", scope=resource)
            record(name + "-recovery", name, r.recover(old, "synthetic-pm", "role:pm", "3" * 64 if dirty else lease_module.SHA_Y, at(4)),
                   not dirty, "content_changed_since_last_authorized_write" if dirty else None)
            if not dirty:
                record("crash-retry", name, r.acquire("synthetic-b", resource, BASE_B, at(5), at(35), lease_module.SHA_Y), kind="retry")
            else:
                record("dirty-still-blocks", name, r.acquire("synthetic-b", resource, BASE_B, at(5), at(35), lease_module.SHA_Y),
                       False, "overlap_with_failed", scope=resource)

        journals = {name: r.journal() for name, r in registries.items()}
        matched = sum(map(len, journals.values())) == len(cases)
        for case in cases:
            if case["journalSeq"] is None:
                matched = False
                continue
            entry = journals[case["registry"]][case["journalSeq"] - 1]
            matched = matched and all(entry[key] == case[key] for key in ("reason", "holder", "baseRef", "fence", "details"))
            matched = matched and (entry["result"] == "accepted") == case["accepted"]

        # These placeholders are newly authored synthetic public inputs. Actual
        # required evidence is the separate synthetic observation/journal receipt.
        raw = {"schemaVersion": 1, "state": "raw", "events": [
            {"kind": c["eventKind"], "outcome": c["eventOutcome"], "classification": "public",
             "content": "synthetic fixture event", "requiredForEvidence": False} for c in cases]}
        raw_path = root / "raw.json"
        raw_bytes = record_pipeline.encoded(raw)
        raw_path.write_bytes(raw_bytes)
        manifest = record_pipeline.build(raw_path, root / "private", root / "public")
        private = json.loads((root / "private/redacted.json").read_bytes())
        bundle = {name: (root / "public" / name).read_bytes() for name in ("events.json", "manifest.json")}
        public = json.loads(bundle["events.json"])
        errors = [error for record_value in (raw, private, public, manifest) for error in schema_errors(record_value)]
        unchanged = raw_path.read_bytes() == raw_bytes and private["rawSha256"] == record_pipeline.digest(raw_bytes)
        preserved = [(e["kind"], e["outcome"]) for e in public["events"]] == [(c["eventKind"], c["eventOutcome"]) for c in cases]
        passed = all(c["passed"] for c in cases) and matched and unchanged and preserved and not errors
        state = "cannot_proceed" if errors else ("passed" if passed else "failed")
        report = {"schemaVersion": 1, "phase": "candidate", "state": state,
                  "scope": "synthetic registry/record integration; no live Editor, Native runtime or OS lock",
                  "fixtureIdentity": "holders, timestamps, base refs and content hashes in cases/journals are synthetic",
                  "cases": cases, "journals": journals, "journalMatchesCalls": matched,
                  "schemaErrors": errors, "privateInputUnchanged": unchanged, "eventOrderPreserved": preserved,
                  "publicCandidate": public, "publicManifest": manifest}
        return report, bundle


def safe_fixture():
    """Return a durable failed report if a dependent call raises unexpectedly."""
    try:
        return run_fixture()
    except Exception as error:
        return ({"schemaVersion": 1, "phase": "candidate", "state": "failed",
                 "failure": {"type": type(error).__name__, "reason": "dependent_step_not_run",
                              "message": str(error)}, "cases": [], "journals": {},
                 "schemaErrors": [], "privateInputUnchanged": True,
                 "eventOrderPreserved": False}, {})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path, help="new, exclusively owned run directory")
    parser.add_argument("--inject-precondition-failure", action="store_true", help=argparse.SUPPRESS)
    args = parser.parse_args()
    try:
        revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=REPO, text=True).strip()
        sources = {}
        for relative in SOURCES:
            committed = subprocess.check_output(["git", "show", revision + ":" + relative], cwd=REPO)
            # Hash the executable checkout bytes; Git may apply CRLF conversion.
            actual = (REPO / relative).read_bytes()
            if actual.replace(b"\r\n", b"\n") != committed.replace(b"\r\n", b"\n"):
                raise ValueError("source differs from recorded revision: " + relative)
            sources[relative] = {"sha256": hashlib.sha256(actual).hexdigest(), "gitBlobSha256": hashlib.sha256(committed).hexdigest()}
        if args.output.exists():
            raise ValueError("output exists; choose a fresh run directory")
        report, bundle = safe_fixture() if not args.inject_precondition_failure else run_fixture(True)
        report["sourceRevision"], report["sourceFiles"] = revision, sources
        args.output.mkdir(parents=True, exist_ok=False)
        (args.output / "public").mkdir()
        for name, data in bundle.items():
            with (args.output / "public" / name).open("xb") as stream:
                stream.write(data)
        with (args.output / "receipt.json").open("xb") as stream:
            stream.write(pipeline.encoded(report))
        print(json.dumps({"state": report["state"], "steps": len(report["cases"]), "sourceRevision": revision}))
        return {"passed": 0, "failed": 1, "cannot_proceed": 2}[report["state"]]
    except (OSError, ValueError, SchemaError, subprocess.CalledProcessError) as error:
        parser.exit(2, "cannot_proceed: " + str(error) + "\n")


if __name__ == "__main__":
    raise SystemExit(main())
