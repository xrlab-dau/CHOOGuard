#!/usr/bin/env python3
"""Synthetic M1-01 lease decisions carried through the M1-05 record pipeline."""
from __future__ import annotations

import argparse
import datetime as dt
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
        # Check both modules before executing either one; cache only a complete load.
        for relative in (SOURCES[1], SOURCES[3]):
            compile((REPO / relative).read_bytes(), relative, "exec")
        loaded_lease = module("m106_lease", SOURCES[1])
        loaded_pipeline = module("m106_pipeline", SOURCES[3])
        lease, pipeline = loaded_lease, loaded_pipeline
    return lease, pipeline


def schema_errors(record):
    schema = json.loads((REPO / SOURCES[2]).read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(schema)
    return [error.message for error in Draft202012Validator(schema).iter_errors(record)]


def run_fixture(inject_precondition_failure=False, *, inject_failure=None):
    lease_module, record_pipeline = load_suppliers()
    contract = lease_module.read_json(REPO / SOURCES[0])
    if inject_precondition_failure:
        inject_failure = "initial"
    cases, registries, skipped, failures, completed = [], {}, [], [], {}
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

        def step(name, group, action, *expected, requires=(), **options):
            unmet = [dependency for dependency in requires if not completed.get(dependency)]
            if unmet:
                skipped.append({"id": name, "reason": "dependent_step_not_run", "requires": unmet})
                completed[name] = False
                return None
            try:
                current = record(name, group, action(), *expected, **options)
                completed[name] = cases[-1]["passed"]
                return current if completed[name] else None
            except Exception as error:
                # Retain earlier calls and the synthetic journal inside the
                # temporary-directory lifetime; never export exception messages.
                failures.append({"id": name, "type": type(error).__name__})
                completed[name] = False
                return None

        resources = {
            "workspace-file": wf("Assets/Synthetic.fixture"),
            "unity-project": {"kind": "unity-project", "workspaceId": "ws-1"},
            "unity-editor": {"kind": "unity-editor", "editorInstanceId": "synthetic-editor"},
            "generated-output": {"kind": "generated-output", "workspaceId": "ws-1", "root": "Exports/"},
            "manifest": wf("Packages/manifest.json"),
        }
        for name, resource in resources.items():
            r = registry(name)
            step(name + "-acquire", name, lambda: r.acquire("synthetic-a", resource, BASE_A, at(0), at(30), lease_module.SHA_X))
            step(name + "-conflict", name, lambda: r.acquire("synthetic-b", resource, BASE_B, at(1), at(31), lease_module.SHA_X),
                 False, "overlap_with_active", scope=resource, requires=(name + "-acquire",))

        name, resource = "handoff", wf("Assets/Synthetic.fixture")
        r = registry(name)
        old = step("handoff-start", name, lambda: r.acquire("" if inject_failure == "initial" else "synthetic-a", resource, BASE_A, at(0), at(30), lease_module.SHA_X))
        step("handoff", name, lambda: r.handoff(old, "synthetic-a", 1, at(1), {
            "nextSessionId": "synthetic-b", "outputs": ["synthetic fixture"],
            "tests": ["synthetic assertions"], "knownLimits": ["no live Editor"]}), requires=("handoff-start",))
        new = step("handoff-acquire", name, lambda: r.acquire("" if inject_failure == "reacquire" else "synthetic-b", resource, BASE_B, at(2), at(32), lease_module.SHA_X),
                   kind="retry", requires=("handoff",))
        step("old-holder-write", name, lambda: r.write(old, "synthetic-a", 1, resource["path"], at(3), lease_module.SHA_Y),
             False, "transition_not_in_contract", requires=("handoff",))
        step("cancel", name, lambda: r.release(new, "synthetic-b", 2, at(4)), kind="cancel", requires=("handoff-acquire",))

        r = registry("unknown")
        step("unknown-holder", "unknown", lambda: r.acquire("", resource, BASE_A, at(0), at(30), lease_module.SHA_X),
               False, "lease_fields_unrecordable", scope=resource)

        r = registry("expiry")
        old = step("expiry-start", "expiry", lambda: r.acquire("synthetic-a", resource, BASE_A, at(0), at(10), lease_module.SHA_X))
        step("expire", "expiry", lambda: r.expire_due(at(11))[0], requires=("expiry-start",))
        step("expired-conflict", "expiry", lambda: r.acquire("synthetic-b", resource, BASE_B, at(12), at(32), lease_module.SHA_X),
             False, "overlap_with_expired", scope=resource, requires=("expire",))
        step("expiry-recover", "expiry", lambda: r.recover(old, "synthetic-pm", "role:pm", lease_module.SHA_X, at(13)), requires=("expire",))
        step("recovery-retry", "expiry", lambda: r.acquire("synthetic-b", resource, BASE_B, at(14), at(34), lease_module.SHA_X), kind="retry", requires=("expiry-recover",))

        for dirty in (False, True):
            name = "dirty" if dirty else "crash"
            r = registry(name)
            old = step(name + "-start", name, lambda: r.acquire("synthetic-a", resource, BASE_A, at(0), at(30), lease_module.SHA_X))
            step(name + "-write", name, lambda: r.write(old, "synthetic-a", 1, resource["path"], at(1), lease_module.SHA_Y), requires=(name + "-start",))
            step(name + "-detected", name, lambda: r.crash_detected(old, at(2)), requires=(name + "-write",))
            step(name + "-conflict", name, lambda: r.acquire("synthetic-b", resource, BASE_B, at(3), at(33), lease_module.SHA_Y),
                 False, "overlap_with_crash_suspected", scope=resource, requires=(name + "-detected",))
            step(name + "-recovery", name, lambda: r.recover(old, "synthetic-pm", "role:pm", "3" * 64 if dirty else lease_module.SHA_Y, at(4)),
                 not dirty, "content_changed_since_last_authorized_write" if dirty else None, requires=(name + "-detected",))
            if not dirty:
                step("crash-retry", name, lambda: r.acquire("synthetic-b", resource, BASE_B, at(5), at(35), lease_module.SHA_Y), kind="retry", requires=(name + "-recovery",))
            else:
                step("dirty-still-blocks", name, lambda: r.acquire("synthetic-b", resource, BASE_B, at(5), at(35), lease_module.SHA_Y),
                     False, "overlap_with_failed", scope=resource, requires=(name + "-recovery",))

        journals = {name: r.journal() for name, r in registries.items()}
        matched = sum(map(len, journals.values())) == len(cases)
        for case in cases:
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
        passed = all(c["passed"] for c in cases) and not skipped and not failures and matched and unchanged and preserved and not errors
        state = "cannot_proceed" if errors else ("passed" if passed else "failed")
        report = {"schemaVersion": 1, "phase": "candidate", "state": state,
                  "scope": "synthetic registry/record integration; no live Editor, Native runtime or OS lock",
                  "fixtureIdentity": "holders, timestamps, base refs and content hashes in cases/journals are synthetic",
                  "cases": cases, "skippedSteps": skipped, "failures": failures,
                  "faultInjection": inject_failure, "journals": journals, "journalMatchesCalls": matched,
                  "schemaErrors": errors, "privateInputUnchanged": unchanged, "eventOrderPreserved": preserved,
                  "publicCandidate": public, "publicManifest": manifest}
        return report, bundle


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path, help="new, exclusively owned run directory")
    parser.add_argument("--inject-precondition-failure", action="store_true", help=argparse.SUPPRESS)
    parser.add_argument("--inject-failure", choices=("initial", "reacquire"), help=argparse.SUPPRESS)
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
        # Validate data before importing supplier code or creating any output.
        json.loads((REPO / SOURCES[0]).read_bytes())
        Draft202012Validator.check_schema(json.loads((REPO / SOURCES[2]).read_bytes()))
        load_suppliers()
        report, bundle = run_fixture(args.inject_precondition_failure, inject_failure=args.inject_failure)
        report["sourceRevision"], report["sourceFiles"] = revision, sources
        report["recordedAt"] = dt.datetime.now(dt.timezone.utc).isoformat()
        args.output.mkdir(parents=True, exist_ok=False)
        (args.output / "public").mkdir()
        for name, data in bundle.items():
            with (args.output / "public" / name).open("xb") as stream:
                stream.write(data)
        with (args.output / "receipt.json").open("xb") as stream:
            stream.write((json.dumps(report, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
        print(json.dumps({"state": report["state"], "steps": len(report["cases"]),
                          "failedSteps": sum(not c["passed"] for c in report["cases"]) + len(report["failures"]),
                          "skippedSteps": len(report["skippedSteps"]), "sourceRevision": revision}))
        return {"passed": 0, "failed": 1, "cannot_proceed": 2}[report["state"]]
    except (OSError, ValueError, SchemaError, SyntaxError, ImportError, subprocess.CalledProcessError) as error:
        parser.exit(2, "cannot_proceed: " + type(error).__name__ + "\n")


if __name__ == "__main__":
    raise SystemExit(main())
