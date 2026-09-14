#!/usr/bin/env python3
"""M1-01 session input and Writer Lease (candidate): a simulated registry driven by the transition table in
docs/team/M1-01/session-contract.json. No lock service, no Unity process, no write outside the caller's directories.

- inspect_contract(): machine checks of the contract (required session fields, lease expiry, duplicate-rejection record,
  refusal and closing transitions preserving holder and baseRef, blocking states, outcome records).
- validate_session(): session input against the contract, the M0-03 execution profile and the allowlist row ids.
- LeaseRegistry: acquire / renew / write / release / handoff / expire / crash_detected / recover, each allowed only when the
  contract lists it for the lease's current state; every accepted or refused attempt is appended to a JSON-lines journal.

Exit codes: inspect -> 0 contract ok, 1 problems, 2 contract absent or invalid JSON;
scenarios -> 0 every scripted step matched, 1 a step mismatched, 2 inputs absent or contract invalid.
"""
from __future__ import annotations

import argparse
import copy
import datetime as dt
import fnmatch
import hashlib
import json
import re
import sys
import tempfile
from pathlib import Path, PurePosixPath

REPO = Path(__file__).resolve().parents[3]
CONTRACT = REPO / "docs/team/M1-01/session-contract.json"
PROFILE = REPO / "docs/team/M0-03/execution-profile.json"
ALLOWLIST = REPO / "docs/team/M0-03/allowlist.json"
SHA40 = re.compile(r"^[0-9a-f]{40}$")
SHA256 = re.compile(r"^[0-9a-f]{64}$")
IMPLEMENTED_KINDS = {"workspace-file", "generated-output", "project-settings", "unity-project", "unity-editor"}
REQUIRED_SESSION = {"sessionId", "workId", "issueNumber", "phase", "baseRef", "writeScope", "role", "tools", "model", "acceptance", "evidencePaths"}
REQUIRED_EVENTS = {"acquire", "renew", "write", "release", "handoff", "expire", "crash_detected", "recover"}
CLOSING_STATES = {"released", "expired", "crash_suspected", "recovered"}


class InputError(ValueError):
    pass


def iso(value: dt.datetime) -> str:
    return value.astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def parse(value: str) -> dt.datetime:
    return dt.datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=dt.timezone.utc)


def read_json(path: Path):
    try:
        return json.loads(Path(path).read_text(encoding="utf-8"))
    except FileNotFoundError:
        raise InputError("missing:" + Path(path).name) from None
    except ValueError:
        raise InputError("invalid_json:" + Path(path).name) from None


# ----------------------------------------------------------------------------- contract inspection

def inspect_contract(contract: dict) -> dict:
    problems = []
    session = contract.get("sessionInput", {})
    required = {f.get("field") for f in session.get("required", [])}
    problems += [f"session_required_missing:{f}" for f in sorted(REQUIRED_SESSION - required)]
    if not {"assignee", "person", "teammate"} <= set(session.get("forbiddenFields", [])):
        problems.append("named_assignment_not_forbidden")
    if "orchestrator" in required:
        problems.append("orchestrator_required")
    lease = contract.get("lease", {})
    lease_fields = {f.get("field") for f in lease.get("fields", [])}
    problems += [f"lease_field_missing:{f}" for f in ("holder", "baseRef", "acquiredAt", "expiresAt", "fence", "baseContentSha256") if f not in lease_fields]
    ttl = lease.get("limits", {}).get("maxTtlSeconds")
    if not isinstance(ttl, int) or isinstance(ttl, bool) or ttl <= 0:
        problems.append("missing_expiry_limit")
    kinds = {k.get("kind") for k in lease.get("resourceKinds", [])}
    problems += [f"resource_kind_unimplemented:{k}" for k in sorted(kinds - IMPLEMENTED_KINDS)]
    problems += [f"implemented_kind_undocumented:{k}" for k in sorted(IMPLEMENTED_KINDS - kinds)]
    states = set(contract.get("states", []))
    if not {"active", "expired", "crash_suspected", "failed"} <= set(lease.get("blockingStates", [])):
        problems.append("blocking_state_missing")
    if not set(contract.get("terminalStates", [])) <= states:
        problems.append("terminal_state_undeclared")
    transitions = contract.get("transitions", [])
    problems += [f"transition_missing:{e}" for e in sorted(REQUIRED_EVENTS - {t.get("event") for t in transitions})]
    table = []
    for t in transitions:
        sources = t.get("from") if isinstance(t.get("from"), list) else [t.get("from")]
        for state in [s for s in sources if s is not None] + [t.get("to")]:
            if state not in states:
                problems.append(f"undeclared_state:{t.get('id')}:{state}")
        reject = t.get("onReject")
        if reject is not None and not {"holder", "baseRef"} <= set(reject.get("preserves", [])):
            problems.append(f"reject_does_not_preserve_holder_base:{t.get('id')}")
        if t.get("to") in CLOSING_STATES and not {"holder", "baseRef"} <= set(t.get("preserves", [])):
            problems.append(f"closing_transition_does_not_preserve_holder_base:{t.get('id')}")
        table.append({"id": t.get("id"), "from": sources, "event": t.get("event"), "to": t.get("to"), "rejectTo": (reject or {}).get("to")})
    acquire = next((t for t in transitions if t.get("event") == "acquire"), {})
    if not {"conflictingHolder", "conflictingScope", "conflictingBaseRef"} <= set(acquire.get("onReject", {}).get("record", [])):
        problems.append("duplicate_rejection_record_incomplete")
    recover = next((t for t in transitions if t.get("event") == "recover"), {})
    if recover.get("onReject", {}).get("to") != "failed":
        problems.append("recover_mismatch_not_failed")
    if "append-only" not in contract.get("journal", {}).get("form", ""):
        problems.append("journal_not_append_only")
    outcomes = contract.get("outcomeRecords", {})
    if not {"failed", "retried", "cancelled"} <= set(outcomes.get("kinds", [])) or "retryOf" not in outcomes.get("conditionalFields", {}).get("retried", []):
        problems.append("outcome_records_incomplete")
    return {"ok": not problems, "problems": sorted(set(problems)), "transitionTable": table, "states": sorted(states),
            "blockingStates": lease.get("blockingStates", []), "maxTtlSeconds": ttl}


# ----------------------------------------------------------------------------- session input

def lexical_repo_path(text) -> bool:
    value = str(text or "").replace("\\", "/")
    if not value or value.startswith("/") or re.match(r"^[a-zA-Z]:", value):
        return False
    return ".." not in PurePosixPath(value.rstrip("/")).parts


def norm(path: str) -> str:
    value = str(path).replace("\\", "/")
    value = value[:-3] if value.endswith("/**") else value
    return value.rstrip("/")


def within(path: str, base: str) -> bool:
    path, base = norm(path), norm(base)
    return path == base or path.startswith(base + "/")


def paths_overlap(a: str, b: str) -> bool:
    return within(a, b) or within(b, a)


def scope_covers(scope: list, path: str) -> bool:
    for entry in scope:
        if entry.endswith("/") or entry.endswith("/**"):
            if within(path, entry):
                return True
        elif path == entry or fnmatch.fnmatchcase(path, entry):
            return True
    return False


def validate_session(contract: dict, profile: dict, allowlist: dict, session: dict) -> list:
    spec = contract["sessionInput"]
    problems = [f"named_assignment_field:{f}" for f in spec.get("forbiddenFields", []) if f in session]
    missing = [f["field"] for f in spec["required"] if session.get(f["field"]) in (None, "", [], {})]
    if missing:
        return sorted(problems + ["missing:" + name for name in missing])
    if not isinstance(session["issueNumber"], int) or isinstance(session["issueNumber"], bool) or session["issueNumber"] <= 0:
        problems.append("invalid:issueNumber")
    if session["phase"] not in next(f["values"] for f in spec["required"] if f["field"] == "phase"):
        problems.append("invalid:phase")
    if not SHA40.match(str(session["baseRef"])):
        problems.append("invalid:baseRef")
    if not all(lexical_repo_path(p) for p in session["writeScope"] + session["evidencePaths"] + session.get("readScope", [])):
        problems.append("unsafe_repo_path")
    role = {r["roleId"]: r for r in profile["roles"]}.get(session["role"])
    if role is None:
        problems.append("unknown_role")
    rows = {r["rowId"] for r in allowlist["allowlistRows"]}
    problems += [f"unknown_tool_row:{t}" for t in session["tools"] if t not in rows]
    model = session["model"]
    if not isinstance(model, dict) or not model.get("provider") or not model.get("modelId"):
        problems.append("invalid:model")
    elif role is not None:
        scope = role.get("modelScope", {})
        allowed, denied = scope.get("allow", []), scope.get("deny", [])
        if not allowed and model["modelId"] != "none":
            problems.append("model_for_non_model_role")
        elif allowed and (not any(fnmatch.fnmatchcase(model["modelId"], p) for p in allowed) or any(fnmatch.fnmatchcase(model["modelId"], p) for p in denied)):
            problems.append("model_outside_role_scope")
    problems += [f"evidence_outside_write_scope:{p}" for p in session["evidencePaths"] if not scope_covers(session["writeScope"], p)]
    if session.get("orchestrator", "none") not in next(f["values"] for f in spec["optional"] if f["field"] == "orchestrator"):
        problems.append("unknown_orchestrator")
    return sorted(set(problems))


# ----------------------------------------------------------------------------- resources

def resource_key(resource: dict) -> str:
    kind = resource.get("kind")
    if kind == "workspace-file":
        return f"workspace-file:{resource['workspaceId']}:{norm(resource['path'])}"
    if kind == "generated-output":
        return f"generated-output:{resource['workspaceId']}:{norm(resource['root'])}"
    if kind in ("project-settings", "unity-project"):
        return f"{kind}:{resource['workspaceId']}"
    if kind == "unity-editor":
        return f"unity-editor:{resource['editorInstanceId']}"
    raise InputError("unknown_resource_kind")


def overlaps(a: dict, b: dict) -> bool:
    kinds = {a["kind"], b["kind"]}
    if kinds == {"unity-editor"}:
        return a["editorInstanceId"] == b["editorInstanceId"]
    if "unity-editor" in kinds or a.get("workspaceId") != b.get("workspaceId"):
        return False
    pick = lambda kind, field: a[field] if a["kind"] == kind else b[field]  # noqa: E731
    if kinds == {"workspace-file"}:
        return paths_overlap(a["path"], b["path"])
    if kinds == {"generated-output"}:
        return paths_overlap(a["root"], b["root"])
    if kinds == {"generated-output", "workspace-file"}:
        return within(pick("workspace-file", "path"), pick("generated-output", "root")) or within(pick("generated-output", "root"), pick("workspace-file", "path"))
    if kinds == {"project-settings", "workspace-file"}:
        return paths_overlap("ProjectSettings", pick("workspace-file", "path"))
    return kinds in ({"project-settings"}, {"unity-project"}, {"unity-project", "generated-output"}, {"unity-project", "project-settings"})


def path_inside_resource(resource: dict, path: str) -> bool:
    kind = resource["kind"]
    if kind == "workspace-file":
        return within(path, resource["path"])
    if kind == "generated-output":
        return within(path, resource["root"])
    if kind == "project-settings":
        return within(path, "ProjectSettings")
    return kind in ("unity-project", "unity-editor")


# ----------------------------------------------------------------------------- registry

class LeaseRegistry:
    def __init__(self, contract: dict, journal_path: Path):
        report = inspect_contract(contract)
        if not report["ok"]:
            raise InputError("contract_invalid:" + ",".join(report["problems"]))
        self.max_ttl = contract["lease"]["limits"]["maxTtlSeconds"]
        self.blocking = set(contract["lease"]["blockingStates"])
        self.table = {}
        for t in contract["transitions"]:
            for state in (t["from"] if isinstance(t["from"], list) else [t["from"]]):
                self.table[(state, t["event"])] = t
        self.journal_path = Path(journal_path)
        self.leases, self.fences, self.seq = {}, {}, 0

    def _record(self, event, now, result, lease, reason, details, holder=None, base_ref=None):
        self.seq += 1
        entry = {"seq": self.seq, "at": iso(now), "event": event, "leaseId": lease["leaseId"] if lease else None, "result": result, "reason": reason,
                 "holder": lease["holder"] if lease else holder, "baseRef": lease["baseRef"] if lease else base_ref,
                 "fence": lease["fence"] if lease else None, "details": details or {}}
        with self.journal_path.open("a", encoding="utf-8", newline="\n") as stream:
            stream.write(json.dumps(entry, ensure_ascii=False, sort_keys=True) + "\n")
        return entry["seq"]

    def _reject(self, event, now, reason, lease=None, details=None, holder=None, base_ref=None):
        seq = self._record(event, now, "rejected", lease, reason, details, holder, base_ref)
        return {"ok": False, "reason": reason, "journalSeq": seq, "lease": copy.deepcopy(lease), "details": details or {}}

    def _accept(self, event, now, lease, details=None):
        seq = self._record(event, now, "accepted", lease, None, details)
        return {"ok": True, "reason": None, "journalSeq": seq, "lease": copy.deepcopy(lease), "details": details or {}}

    def _ttl_ok(self, now, expires_at):
        return 0 < (expires_at - now).total_seconds() <= self.max_ttl

    def acquire(self, holder, resource, base_ref, now, expires_at, base_content_sha256):
        request = {"requester": holder, "requestedResource": resource}
        if not holder or not SHA40.match(str(base_ref or "")) or expires_at is None or not SHA256.match(str(base_content_sha256 or "")):
            return self._reject("acquire", now, "lease_fields_unrecordable", details=request, holder=holder, base_ref=base_ref)
        if not self._ttl_ok(now, expires_at):
            return self._reject("acquire", now, "expiry_out_of_bounds", details=request, holder=holder, base_ref=base_ref)
        key = resource_key(resource)
        conflicts = [lease for lease in self.leases.values() if lease["state"] in self.blocking and overlaps(lease["resource"], resource)]
        if conflicts:
            first = conflicts[0]
            details = {**request, "conflictingLeaseId": first["leaseId"], "conflictingHolder": first["holder"], "conflictingScope": first["resource"],
                       "conflictingBaseRef": first["baseRef"], "conflictingState": first["state"], "conflictCount": len(conflicts)}
            return self._reject("acquire", now, "overlap_with_" + first["state"], details=details, holder=holder, base_ref=base_ref)
        self.fences[key] = self.fences.get(key, 0) + 1
        lease = {"leaseId": f"lease-{len(self.leases) + 1}", "resource": dict(resource), "resourceKey": key, "holder": holder, "baseRef": base_ref,
                 "acquiredAt": iso(now), "expiresAt": iso(expires_at), "fence": self.fences[key], "baseContentSha256": base_content_sha256,
                 "lastWriteSha256": None, "state": self.table[(None, "acquire")]["to"]}
        self.leases[lease["leaseId"]] = lease
        return self._accept("acquire", now, lease)

    def _guard_holder(self, event, lease, caller, fence, now):
        if (lease["state"], event) not in self.table:
            return self._reject(event, now, "transition_not_in_contract", lease, {"caller": caller, "state": lease["state"]})
        if caller != lease["holder"]:
            return self._reject(event, now, "caller_not_holder", lease, {"caller": caller})
        if fence != lease["fence"] or self.fences[lease["resourceKey"]] != fence:
            return self._reject(event, now, "stale_fence", lease, {"caller": caller, "presentedFence": fence})
        return None

    def renew(self, lease_id, caller, fence, now, new_expires_at):
        lease = self.leases[lease_id]
        refused = self._guard_holder("renew", lease, caller, fence, now)
        if refused:
            return refused
        if now >= parse(lease["expiresAt"]):
            return self._reject("renew", now, "lease_expired", lease, {"caller": caller})
        if not self._ttl_ok(now, new_expires_at):
            return self._reject("renew", now, "expiry_out_of_bounds", lease, {"caller": caller})
        lease["expiresAt"] = iso(new_expires_at)
        return self._accept("renew", now, lease)

    def write(self, lease_id, caller, fence, path, now, content_sha256_after):
        lease = self.leases[lease_id]
        refused = self._guard_holder("write", lease, caller, fence, now)
        if refused:
            return refused
        if now >= parse(lease["expiresAt"]):
            return self._reject("write", now, "lease_expired", lease, {"caller": caller, "path": path})
        if not path_inside_resource(lease["resource"], path):
            return self._reject("write", now, "path_outside_lease", lease, {"caller": caller, "path": path})
        lease["lastWriteSha256"] = content_sha256_after
        return self._accept("write", now, lease, {"path": path})

    def release(self, lease_id, caller, fence, now):
        lease = self.leases[lease_id]
        refused = self._guard_holder("release", lease, caller, fence, now)
        if refused:
            return refused
        lease["state"] = self.table[(lease["state"], "release")]["to"]
        return self._accept("release", now, lease)

    def handoff(self, lease_id, caller, fence, now, record):
        lease = self.leases[lease_id]
        refused = self._guard_holder("handoff", lease, caller, fence, now)
        if refused:
            return refused
        missing = [k for k in ("nextSessionId", "outputs", "tests", "knownLimits") if not record.get(k)]
        if missing:
            return self._reject("handoff", now, "handoff_record_incomplete", lease, {"missing": missing})
        lease["state"] = self.table[(lease["state"], "handoff")]["to"]
        lease["handoff"] = copy.deepcopy(record)
        return self._accept("handoff", now, lease, {"nextSessionId": record["nextSessionId"]})

    def expire_due(self, now):
        results = []
        for lease in self.leases.values():
            if (lease["state"], "expire") in self.table and now >= parse(lease["expiresAt"]):
                lease["state"] = self.table[(lease["state"], "expire")]["to"]
                results.append(self._accept("expire", now, lease))
        return results

    def crash_detected(self, lease_id, now):
        lease = self.leases[lease_id]
        if (lease["state"], "crash_detected") not in self.table:
            return self._reject("crash_detected", now, "transition_not_in_contract", lease, {"state": lease["state"]})
        lease["state"] = self.table[(lease["state"], "crash_detected")]["to"]
        return self._accept("crash_detected", now, lease)

    def recover(self, lease_id, caller, caller_role, current_sha256, now):
        lease = self.leases[lease_id]
        transition = self.table.get((lease["state"], "recover"))
        if transition is None:
            return self._reject("recover", now, "transition_not_in_contract", lease, {"caller": caller, "state": lease["state"]})
        if caller_role != "role:pm":
            return self._reject("recover", now, "caller_not_pm", lease, {"caller": caller})
        expected = {lease["baseContentSha256"]} | ({lease["lastWriteSha256"]} if lease["lastWriteSha256"] else set())
        if current_sha256 not in expected:
            lease["state"] = transition["onReject"]["to"]
            return self._reject("recover", now, "content_changed_since_last_authorized_write", lease,
                                {"caller": caller, "expectedSha256": sorted(expected), "observedSha256": current_sha256})
        lease["state"] = transition["to"]
        return self._accept("recover", now, lease, {"caller": caller})

    def journal(self) -> list:
        if not self.journal_path.exists():
            return []
        return [json.loads(line) for line in self.journal_path.read_text(encoding="utf-8").splitlines() if line.strip()]


def validate_outcomes(contract: dict, records: list) -> list:
    spec = contract["outcomeRecords"]
    problems, seen = [], []
    for index, record in enumerate(records):
        for field in spec["requiredFields"] + spec["conditionalFields"].get(record.get("outcome"), []):
            if record.get(field) in (None, "", []):
                problems.append(f"record{index}:missing:{field}")
        if record.get("outcome") not in spec["kinds"]:
            problems.append(f"record{index}:unknown_outcome")
        key = (record.get("sessionId"), record.get("attempt"))
        if record.get("outcome") == "retried" and (record.get("sessionId"), record.get("retryOf")) not in seen:
            problems.append(f"record{index}:retry_of_unknown_attempt")
        if key in seen:
            problems.append(f"record{index}:attempt_overwritten")
        seen.append(key)
    return problems


# ----------------------------------------------------------------------------- scripted scenarios

BASE_A, BASE_B = "a" * 40, "b" * 40
SHA_X, SHA_Y = "1" * 64, "2" * 64
T0 = dt.datetime(2026, 9, 15, 0, 0, tzinfo=dt.timezone.utc)


def at(minutes):
    return T0 + dt.timedelta(minutes=minutes)


def wf(path, ws="ws-1"):
    return {"kind": "workspace-file", "workspaceId": ws, "path": path}


def run_scenarios(contract: dict) -> dict:
    steps = []

    def expect(scenario, name, result, ok, reason=None, **checks):
        observed = {"ok": result["ok"], "reason": result["reason"]}
        passed = result["ok"] == ok and (reason is None or result["reason"] == reason)
        for key, value in checks.items():
            actual = result["details"].get(key) if key.startswith("conflicting") else (result["lease"] or {}).get(key)
            observed[key] = actual
            passed = passed and actual == value
        steps.append({"scenario": scenario, "step": name, "expected": {"ok": ok, "reason": reason, **checks}, "observed": observed, "passed": passed})
        return result

    with tempfile.TemporaryDirectory(prefix="m1-01-") as tmp:
        def registry(name):
            return LeaseRegistry(contract, Path(tmp) / f"{name}.jsonl")

        s = "S1 same path"
        r = registry("s1")
        expect(s, "A acquires", r.acquire("session-a", wf("Assets/Scenes/Main.unity"), BASE_A, at(0), at(30), SHA_X), True, fence=1)
        expect(s, "B same path refused", r.acquire("session-b", wf("Assets/Scenes/Main.unity"), BASE_B, at(1), at(31), SHA_X), False,
               "overlap_with_active", conflictingHolder="session-a", conflictingBaseRef=BASE_A, conflictingScope=wf("Assets/Scenes/Main.unity"))
        expect(s, "B parent directory refused", r.acquire("session-b", wf("Assets/Scenes/"), BASE_B, at(1), at(31), SHA_X), False, "overlap_with_active", conflictingHolder="session-a")
        expect(s, "B sibling prefix allowed", r.acquire("session-b", wf("Assets/Scenes/Main.unity.meta"), BASE_B, at(1), at(31), SHA_X), True, fence=1)
        expect(s, "C other workspace allowed", r.acquire("session-c", wf("Assets/Scenes/Main.unity", "ws-2"), BASE_B, at(1), at(31), SHA_X), True, fence=1)

        s = "S2 one Unity writer"
        r = registry("s2")
        expect(s, "A leases the Unity project", r.acquire("session-a", {"kind": "unity-project", "workspaceId": "ws-1"}, BASE_A, at(0), at(30), SHA_X), True)
        expect(s, "B generated output refused", r.acquire("session-b", {"kind": "generated-output", "workspaceId": "ws-1", "root": "Assets/CHOOguardGenerated"}, BASE_B, at(1), at(31), SHA_X),
               False, "overlap_with_active", conflictingHolder="session-a", conflictingBaseRef=BASE_A)
        expect(s, "B project settings refused", r.acquire("session-b", {"kind": "project-settings", "workspaceId": "ws-1"}, BASE_B, at(1), at(31), SHA_X), False, "overlap_with_active")
        expect(s, "B second Unity project writer refused", r.acquire("session-b", {"kind": "unity-project", "workspaceId": "ws-1"}, BASE_B, at(1), at(31), SHA_X), False, "overlap_with_active")
        expect(s, "A leases editor instance", r.acquire("session-a", {"kind": "unity-editor", "editorInstanceId": "editor-7"}, BASE_A, at(0), at(30), SHA_X), True)
        expect(s, "B same editor instance refused", r.acquire("session-b", {"kind": "unity-editor", "editorInstanceId": "editor-7"}, BASE_B, at(2), at(30), SHA_X), False, "overlap_with_active")

        s = "S3 generated output and settings"
        r = registry("s3")
        expect(s, "A leases output root", r.acquire("session-a", {"kind": "generated-output", "workspaceId": "ws-1", "root": "Assets/CHOOguardGenerated"}, BASE_A, at(0), at(30), SHA_X), True)
        expect(s, "B file under root refused", r.acquire("session-b", wf("Assets/CHOOguardGenerated/Demo/Scene.unity"), BASE_B, at(1), at(31), SHA_X), False, "overlap_with_active", conflictingBaseRef=BASE_A)
        expect(s, "B project settings alongside output allowed", r.acquire("session-b", {"kind": "project-settings", "workspaceId": "ws-1"}, BASE_B, at(1), at(31), SHA_X), True)
        expect(s, "C ProjectSettings file refused", r.acquire("session-c", wf("ProjectSettings/ProjectSettings.asset"), BASE_B, at(2), at(31), SHA_X), False, "overlap_with_active", conflictingHolder="session-b")

        s = "S4 unrecordable lease"
        r = registry("s4")
        expect(s, "missing expiry refused", r.acquire("session-a", wf("docs/a.md"), BASE_A, at(0), None, SHA_X), False, "lease_fields_unrecordable")
        expect(s, "missing base ref refused", r.acquire("session-a", wf("docs/a.md"), "", at(0), at(10), SHA_X), False, "lease_fields_unrecordable")
        expect(s, "missing holder refused", r.acquire("", wf("docs/a.md"), BASE_A, at(0), at(10), SHA_X), False, "lease_fields_unrecordable")
        expect(s, "TTL beyond limit refused", r.acquire("session-a", wf("docs/a.md"), BASE_A, at(0), at(61), SHA_X), False, "expiry_out_of_bounds")
        expect(s, "nothing was issued, so the first real lease gets fence 1", r.acquire("session-a", wf("docs/a.md"), BASE_A, at(0), at(10), SHA_X), True, fence=1)

        s = "S5 renew, write and expiry"
        r = registry("s5")
        lid = r.acquire("session-a", wf("docs/team/M1-01/"), BASE_A, at(0), at(20), SHA_X)["lease"]["leaseId"]
        expect(s, "B renew refused", r.renew(lid, "session-b", 1, at(5), at(25)), False, "caller_not_holder", holder="session-a", baseRef=BASE_A)
        expect(s, "A stale fence write refused", r.write(lid, "session-a", 2, "docs/team/M1-01/x.json", at(5), SHA_Y), False, "stale_fence")
        expect(s, "A write outside lease refused", r.write(lid, "session-a", 1, "docs/team/M1-01x/x.json", at(5), SHA_Y), False, "path_outside_lease")
        expect(s, "A write accepted", r.write(lid, "session-a", 1, "docs/team/M1-01/x.json", at(5), SHA_Y), True, lastWriteSha256=SHA_Y)
        expect(s, "A renew accepted", r.renew(lid, "session-a", 1, at(10), at(40)), True, expiresAt=iso(at(40)))
        expect(s, "A write after expiry refused", r.write(lid, "session-a", 1, "docs/team/M1-01/y.json", at(41), SHA_Y), False, "lease_expired")
        expect(s, "A renew after expiry refused", r.renew(lid, "session-a", 1, at(41), at(50)), False, "lease_expired")
        r.expire_due(at(41))
        expect(s, "expired lease still blocks", r.acquire("session-b", wf("docs/team/M1-01/x.json"), BASE_B, at(42), at(50), SHA_Y), False, "overlap_with_expired", conflictingHolder="session-a")
        expect(s, "release of an expired lease is not in the contract", r.release(lid, "session-a", 1, at(42)), False, "transition_not_in_contract", state="expired", holder="session-a", baseRef=BASE_A)

        s = "S6 normal handoff"
        r = registry("s6")
        lid = r.acquire("session-a", wf("Assets/Prefabs/Gate.prefab"), BASE_A, at(0), at(30), SHA_X)["lease"]["leaseId"]
        expect(s, "incomplete handoff record refused", r.handoff(lid, "session-a", 1, at(10), {"nextSessionId": "session-b"}), False, "handoff_record_incomplete", state="active")
        expect(s, "A hands off", r.handoff(lid, "session-a", 1, at(10), {"nextSessionId": "session-b", "outputs": ["Assets/Prefabs/Gate.prefab"], "tests": ["EditMode GateTests"], "knownLimits": ["collider not tuned"]}),
               True, state="released", holder="session-a", baseRef=BASE_A)
        next_id = expect(s, "B acquires with fence 2", r.acquire("session-b", wf("Assets/Prefabs/Gate.prefab"), BASE_B, at(11), at(40), SHA_X), True, fence=2)["lease"]["leaseId"]
        expect(s, "A write after handoff refused", r.write(lid, "session-a", 1, "Assets/Prefabs/Gate.prefab", at(12), SHA_Y), False, "transition_not_in_contract")
        expect(s, "B writes with fence 2", r.write(next_id, "session-b", 2, "Assets/Prefabs/Gate.prefab", at(12), SHA_Y), True)

        s = "S7 crash recovery"
        r = registry("s7")
        lid = r.acquire("session-a", wf("Assets/Scenes/Hall.unity"), BASE_A, at(0), at(30), SHA_X)["lease"]["leaseId"]
        r.write(lid, "session-a", 1, "Assets/Scenes/Hall.unity", at(3), SHA_Y)
        expect(s, "crash reported", r.crash_detected(lid, at(5)), True, state="crash_suspected", holder="session-a", baseRef=BASE_A)
        expect(s, "B cannot take a crash-suspected resource", r.acquire("session-b", wf("Assets/Scenes/Hall.unity"), BASE_B, at(6), at(30), SHA_Y), False, "overlap_with_crash_suspected")
        expect(s, "non-PM recovery refused", r.recover(lid, "session-b", "role:authoring-agent", SHA_Y, at(6)), False, "caller_not_pm", state="crash_suspected")
        expect(s, "PM recovers when content equals the last authorized write", r.recover(lid, "pm-session", "role:pm", SHA_Y, at(7)), True, state="recovered", holder="session-a", baseRef=BASE_A)
        expect(s, "B acquires with fence 2", r.acquire("session-b", wf("Assets/Scenes/Hall.unity"), BASE_B, at(8), at(30), SHA_Y), True, fence=2)
        expect(s, "A late write refused", r.write(lid, "session-a", 1, "Assets/Scenes/Hall.unity", at(9), SHA_X), False, "transition_not_in_contract")

        s = "S8 recovery with changed content"
        r = registry("s8")
        lid = r.acquire("session-a", wf("Assets/Scenes/Platform.unity"), BASE_A, at(0), at(10), SHA_X)["lease"]["leaseId"]
        r.expire_due(at(10))
        expect(s, "PM recovery with unexpected content fails", r.recover(lid, "pm-session", "role:pm", "3" * 64, at(11)), False,
               "content_changed_since_last_authorized_write", state="failed", holder="session-a", baseRef=BASE_A, fence=1)
        expect(s, "failed lease keeps blocking", r.acquire("session-b", wf("Assets/Scenes/Platform.unity"), BASE_B, at(12), at(30), SHA_X), False, "overlap_with_failed", conflictingHolder="session-a")
        expect(s, "recovery of a failed lease is not in the contract", r.recover(lid, "pm-session", "role:pm", SHA_X, at(13)), False, "transition_not_in_contract", state="failed")

        journals = {p.stem: [json.loads(line) for line in p.read_text(encoding="utf-8").splitlines()] for p in sorted(Path(tmp).glob("*.jsonl"))}
    return {"schemaVersion": 1, "kind": "m1-01-writer-lease-scenarios", "state": "passed" if all(step["passed"] for step in steps) else "mismatch",
            "counts": {"scenarios": len({step["scenario"] for step in steps}), "steps": len(steps), "passed": sum(step["passed"] for step in steps)},
            "journal": {"entries": sum(len(e) for e in journals.values()),
                        "rejectedEntriesRetained": sum(1 for e in journals.values() for entry in e if entry["result"] == "rejected"),
                        "sequencesContiguous": all([entry["seq"] for entry in e] == list(range(1, len(e) + 1)) for e in journals.values()),
                        "sha256": hashlib.sha256(json.dumps(journals, sort_keys=True).encode("utf-8")).hexdigest()},
            "steps": steps}


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    for name in ("inspect", "scenarios"):
        sub.add_parser(name).add_argument("--contract", type=Path, default=CONTRACT)
    args = parser.parse_args(argv)
    try:
        contract = read_json(args.contract)
        report = inspect_contract(contract) if args.command == "inspect" else run_scenarios(contract)
    except InputError as error:
        print(json.dumps({"state": "cannot_start", "error": str(error)}))
        return 2
    print(json.dumps(report, ensure_ascii=False, indent=2))
    ok = report["ok"] if args.command == "inspect" else report["state"] == "passed"
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
