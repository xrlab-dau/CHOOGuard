#!/usr/bin/env python3
"""M0-02b public-use boundary checker (candidate; no network, reads repository files only).

Joins docs/team/M0-02b/public-use-boundary.json with docs/team/M0-02a/source-policy.json and the
repository source registries, then replays the negative fixtures in fixtures/cases.json.

Exit codes: 0 = every boundary record is consistent, every registry record is judged exactly once and
every fixture yields its expected verdict; 1 = any of those fails; 2 = an input file is absent or invalid.
The checker judges record consistency against the written rules. It is not a legal opinion and it grants
no permission to use, transform or redistribute any source.
"""
from __future__ import annotations

import copy
import hashlib
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
POLICY = REPO / "docs/team/M0-02a/source-policy.json"
BOUNDARY = REPO / "docs/team/M0-02b/public-use-boundary.json"
CASES = Path(__file__).resolve().parent / "cases.json"
REGISTRIES = {
    "PROC": ("foundation/procedures/source-registry.json", lambda d: [s["sourceId"] for s in d["sources"]]),
    "PLAN": ("foundation/world/team/FND-05/plan-mapping.json", lambda d: [s["id"] for s in d["sources"]]),
    "ART": ("foundation/art/object-references.json",
            lambda d: [f"{a['id']}#{i + 1}" for a in d["assets"] for i in range(len(a.get("references", [])))]),
    "PHYS": ("foundation/physics/validation/juelich-corridor-aggregates.json", lambda d: ["JUELICH-2006-CORRIDOR"]),
}
REQUIRED = [
    "sourceId", "sourceType", "rating", "allowedClaims", "forbiddenClaims", "uncertainty",
    "license.spdxOrTerms", "license.basis", "license.state", "license.evidenceRef",
    "acquisition.date", "acquisition.sourceClass", "retention.period", "retention.store",
    "redistribution.allowed", "redistribution.conditions", "personalData.present", "personalData.basis",
    "provenance.origin", "provenance.publicLocation", "provenance.retrievedDate",
    "restrictedMetadata.present", "restrictedMetadata.basis",
    "decisions.use", "decisions.transform", "decisions.redistribute", "decisions.privacy", "decisions.restrictedMetadata",
    "admission.state", "admission.reason", "sanitizeState", "publicationApprovalState", "reviewerState",
    "rawCopyInRepository", "korailReplyRequired",
]
LICENSE_STATES = {"unknown", "known_restrictive", "known_conditional", "known_permissive"}
REDISTRIBUTION = {"permitted", "conditional", "not_permitted_by_default"}
TRI_STATE = {True, False, "unknown"}
ADMISSION = {"admitted_conditional", "metadata_only", "blocked_policy_conflict", "rejected"}
USE = {"metadata_citation_only", "citation_with_conditions", "blocked"}
TRANSFORM = {"facts_only_synthetic_derivation", "blocked", "rejected"}
REDISTRIBUTE = {"conditional", "blocked", "rejected"}
PUBLICATION = {"not_requested", "requested", "approved", "denied"}
SANITIZE = {"metadata_recorded_no_original", "not_sanitized", "sanitized"}


class InputError(ValueError):
    pass


def read_json(path: Path):
    if not path.is_file():
        raise InputError(f"missing:{path.name}")
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except ValueError:
        raise InputError(f"invalid_json:{path.name}") from None


def get(record, dotted):
    node = record
    for part in dotted.split("."):
        if not isinstance(node, dict) or part not in node:
            return None, False
        node = node[part]
    return node, True


def load_policy(policy) -> dict:
    types = {t["sourceType"]: {"rating": t["rating"], "allowed": {c["id"] for c in t["allowedClaims"]},
                               "forbidden": {c["id"] for c in t["forbiddenClaims"]}, "uncertainty": {u["id"] for u in t["uncertainty"]}}
             for t in policy["sourceTypes"]}
    licence_fields = policy["sourceTypes"][0]["license"]["recordingForm"]["requiredFields"]
    return {"types": types, "global": {g["id"] for g in policy["globalForbiddenClaims"]}, "licenseFields": licence_fields}


def violations(record, policy: dict) -> list[str]:
    found = []
    if not isinstance(record, dict):
        return ["record_not_object"]
    for field in REQUIRED + [f for f in policy["licenseFields"] if f not in REQUIRED]:
        value, present = get(record, field)
        if not present or value is None or value == "":
            found.append("missing_field:" + field)
    kind = policy["types"].get(record.get("sourceType"))
    if kind is None:
        found.append("unknown_source_type")
    else:
        if record.get("rating") != kind["rating"]:
            found.append("rating_mismatch")
        allowed = set(record.get("allowedClaims") or [])
        if allowed - kind["allowed"]:
            found.append("claim_outside_type")
        if allowed & (kind["forbidden"] | policy["global"]):
            found.append("forbidden_claim_allowed")
        forbidden = set(record.get("forbiddenClaims") or [])
        if not (kind["forbidden"] | policy["global"]) <= forbidden:
            found.append("forbidden_claims_incomplete")
    uncertainty = record.get("uncertainty")
    if not isinstance(uncertainty, list) or not uncertainty or any(
            not isinstance(u, dict) or not u.get("state") or not u.get("resolutionOwner") for u in uncertainty):
        found.append("uncertainty_incomplete")

    licence, _ = get(record, "license.state")
    redistribution, _ = get(record, "redistribution.allowed")
    personal, _ = get(record, "personalData.present")
    restricted, _ = get(record, "restrictedMetadata.present")
    decisions = record.get("decisions") if isinstance(record.get("decisions"), dict) else {}
    admission, _ = get(record, "admission.state")
    enums = [(licence, LICENSE_STATES, "license.state"), (redistribution, REDISTRIBUTION, "redistribution.allowed"),
             (admission, ADMISSION, "admission.state"), (decisions.get("use"), USE, "decisions.use"),
             (decisions.get("transform"), TRANSFORM, "decisions.transform"), (decisions.get("redistribute"), REDISTRIBUTE, "decisions.redistribute"),
             (record.get("publicationApprovalState"), PUBLICATION, "publicationApprovalState"), (record.get("sanitizeState"), SANITIZE, "sanitizeState")]
    found += ["invalid_value:" + name for value, allowed_values, name in enums if value is not None and value not in allowed_values]
    found += ["invalid_value:" + name for value, name in ((personal, "personalData.present"), (restricted, "restrictedMetadata.present"))
              if value is not None and value not in TRI_STATE]

    if licence == "unknown":
        if admission == "admitted_conditional":
            found.append("license_unknown_but_admitted")
        if redistribution != "not_permitted_by_default" or decisions.get("redistribute") not in ("blocked", "rejected"):
            found.append("license_unknown_redistribution")
        if decisions.get("transform") not in ("blocked", "rejected"):
            found.append("license_unknown_transform")
    if licence == "known_restrictive" and (decisions.get("redistribute") != "rejected" or decisions.get("transform") != "rejected"):
        found.append("restrictive_license_not_rejected")
    if personal is True and decisions.get("redistribute") != "rejected":
        found.append("personal_data_redistribution")
    if personal == "unknown" and (redistribution == "permitted" or (record.get("sourceType") == "video-photo" and redistribution != "not_permitted_by_default")):
        found.append("personal_data_unknown_redistribution")
    if restricted is True and decisions.get("restrictedMetadata") != "withheld_hash_and_reason_only":
        found.append("restricted_metadata_not_withheld")
    if record.get("rawCopyInRepository") is not False:
        found.append("raw_copy_in_repository")
    if record.get("korailReplyRequired") is not False:
        found.append("korail_reply_as_prerequisite")
    publication = record.get("publicationApprovalState")
    approver = get(record, "publicationApproval.approverRole")[0]
    if publication == "approved" and not approver:
        found.append("publication_approved_without_approver")
    if record.get("sanitizeState") == "sanitized" and publication == "approved" and not approver:
        found.append("sanitize_conflated_with_publication")
    if admission == "admitted_conditional" and licence not in ("known_conditional", "known_permissive"):
        found.append("admitted_without_known_permitting_license")
    if record.get("conflicts") and admission != "blocked_policy_conflict":
        found.append("conflict_not_blocked")
    if admission == "blocked_policy_conflict" and not record.get("conflicts"):
        found.append("blocked_conflict_without_conflict_record")
    for group in ("personalData", "restrictedMetadata"):
        value, _ = get(record, group + ".present")
        if value == "unknown" and not get(record, group + ".basis")[0]:
            found.append("unknown_without_reason:" + group)
    return sorted(set(found))


def apply_patch(base: dict, patch: dict) -> dict:
    record = copy.deepcopy(base)
    for dotted, value in (patch.get("set") or {}).items():
        node = record
        parts = dotted.split(".")
        for part in parts[:-1]:
            node = node.setdefault(part, {})
        node[parts[-1]] = value
    for dotted in patch.get("delete") or []:
        node = record
        parts = dotted.split(".")
        for part in parts[:-1]:
            node = node.get(part, {})
        node.pop(parts[-1], None)
    return record


def registry_ids() -> dict:
    return {prefix: [f"{prefix}:{i}" for i in ids(read_json(REPO / path))] for prefix, (path, ids) in REGISTRIES.items()}


def join_report(sources: list, policy: dict) -> dict:
    expected = registry_ids()
    judged = [s.get("sourceId") for s in sources]
    all_expected = [i for ids in expected.values() for i in ids]
    per_record = {s.get("sourceId"): violations(s, policy) for s in sources}
    return {
        "registries": {p: {"path": REGISTRIES[p][0], "records": len(ids), "sha256": hashlib.sha256((REPO / REGISTRIES[p][0]).read_bytes()).hexdigest()}
                       for p, ids in expected.items()},
        "expectedSources": len(all_expected), "judgedSources": len(judged),
        "missingJudgments": sorted(set(all_expected) - set(judged)), "unexpectedJudgments": sorted(set(judged) - set(all_expected)),
        "duplicateJudgments": sorted({i for i in judged if judged.count(i) > 1}),
        "recordsWithViolations": {k: v for k, v in per_record.items() if v},
        "everySourceHasConditionAndLimit": all(isinstance(s.get("redistribution"), dict) and s["redistribution"].get("conditions")
                                               and isinstance(s.get("admission"), dict) and s["admission"].get("reason") for s in sources),
    }


def run_fixtures(cases: dict, policy: dict) -> list:
    results = []
    for case in cases["cases"]:
        got = violations(apply_patch(cases["baseRecord"], case.get("patch", {})), policy)
        expect = case["expect"]
        ok = (got == []) if expect == "accept" else (bool(got) and set(expect) <= set(got))
        results.append({"caseId": case["caseId"], "expect": expect, "got": got, "ok": ok})
    return results


def main() -> int:
    try:
        policy = load_policy(read_json(POLICY))
        boundary = read_json(BOUNDARY)
        cases = read_json(CASES)
        if not isinstance(boundary.get("sources"), list) or not isinstance(cases.get("cases"), list) or not isinstance(cases.get("baseRecord"), dict):
            raise InputError("invalid_structure")
    except InputError as error:
        print(json.dumps({"state": "cannot_start", "error": str(error)}))
        return 2
    join = join_report(boundary["sources"], policy)
    fixtures = run_fixtures(cases, policy)
    passed = (not join["missingJudgments"] and not join["unexpectedJudgments"] and not join["duplicateJudgments"]
              and not join["recordsWithViolations"] and join["everySourceHasConditionAndLimit"] and all(r["ok"] for r in fixtures))
    report = {"schemaVersion": 1, "kind": "m0-02b-boundary-check", "state": "consistent" if passed else "inconsistent",
              "policySha256": hashlib.sha256(POLICY.read_bytes()).hexdigest(), "boundarySha256": hashlib.sha256(BOUNDARY.read_bytes()).hexdigest(),
              "casesSha256": hashlib.sha256(CASES.read_bytes()).hexdigest(), "join": join,
              "fixtures": {"total": len(fixtures), "ok": sum(r["ok"] for r in fixtures), "results": fixtures}}
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main())
