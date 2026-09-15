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

import argparse
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
# Groups that hold judgments, not content: any other key could carry the very content the judgment withholds.
CLOSED_GROUPS = {
    "personalData": {"present", "basis"},
    "restrictedMetadata": {"present", "basis"},
    "decisions": {"use", "transform", "redistribute", "privacy", "restrictedMetadata"},
}


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
    try:
        types = {t["sourceType"]: {"rating": t["rating"], "allowed": {c["id"] for c in t["allowedClaims"]},
                                   "forbidden": {c["id"] for c in t["forbiddenClaims"]}, "uncertainty": {u["id"] for u in t["uncertainty"]}}
                 for t in policy["sourceTypes"]}
        licence_fields = list(policy["sourceTypes"][0]["license"]["recordingForm"]["requiredFields"])
        return {"types": types, "global": {g["id"] for g in policy["globalForbiddenClaims"]}, "licenseFields": licence_fields}
    except (KeyError, TypeError, IndexError):
        raise InputError("invalid_structure:source-policy.json") from None


def hashable(value, kinds=(str,)):
    return isinstance(value, kinds)


def violations(record, policy: dict) -> list[str]:
    found = []
    if not isinstance(record, dict):
        return ["record_not_object"]
    for field in REQUIRED + [f for f in policy["licenseFields"] if f not in REQUIRED]:
        value, present = get(record, field)
        if not present or value is None or value == "":
            found.append("missing_field:" + field)
    for group, keys in CLOSED_GROUPS.items():
        if isinstance(record.get(group), dict):
            found += ["unexpected_field:" + group + "." + str(key) for key in record[group] if key not in keys]
    claim_lists = {}
    for field in ("allowedClaims", "forbiddenClaims"):
        value = record.get(field) or []
        if not isinstance(value, list) or not all(hashable(c) for c in value):
            found.append("invalid_value:" + field)
            value = []
        claim_lists[field] = set(value)
    source_type = record.get("sourceType")
    kind = policy["types"].get(source_type) if hashable(source_type) else None
    if kind is None:
        found.append("unknown_source_type")
    else:
        if record.get("rating") != kind["rating"]:
            found.append("rating_mismatch")
        allowed = claim_lists["allowedClaims"]
        if allowed - kind["allowed"]:
            found.append("claim_outside_type")
        if allowed & (kind["forbidden"] | policy["global"]):
            found.append("forbidden_claim_allowed")
        if not (kind["forbidden"] | policy["global"]) <= claim_lists["forbiddenClaims"]:
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
    found += ["invalid_value:" + name for value, allowed_values, name in enums
              if value is not None and (not hashable(value) or value not in allowed_values)]
    found += ["invalid_value:" + name for value, name in ((personal, "personalData.present"), (restricted, "restrictedMetadata.present"))
              if value is not None and (not hashable(value, (bool, str)) or value not in TRI_STATE)]

    redistribute = decisions.get("redistribute")
    if (redistribute in ("blocked", "rejected") and redistribution != "not_permitted_by_default") \
            or (redistribute == "conditional" and redistribution == "not_permitted_by_default"):
        # R-12: the machine-readable flag and the decision must say the same thing, whatever the licence or personal-data state.
        found.append("redistribution_decision_contradicts_allowed")

    if licence == "unknown":
        if admission == "admitted_conditional":
            found.append("license_unknown_but_admitted")
        if redistribution != "not_permitted_by_default" or decisions.get("redistribute") not in ("blocked", "rejected"):
            found.append("license_unknown_redistribution")
        if decisions.get("transform") not in ("blocked", "rejected"):
            found.append("license_unknown_transform")
    if licence == "known_restrictive" and (decisions.get("redistribute") != "rejected" or decisions.get("transform") != "rejected"
                                           or redistribution != "not_permitted_by_default"):
        found.append("restrictive_license_not_rejected")
    if personal is True and decisions.get("redistribute") != "rejected":
        found.append("personal_data_redistribution")
    if personal == "unknown":
        conditional = redistribution == "conditional" or decisions.get("redistribute") == "conditional"
        if (redistribution == "permitted"
                or (record.get("sourceType") == "video-photo" and redistribution != "not_permitted_by_default")
                or (conditional and decisions.get("privacy") != "no_personal_data_copied")):
            # R-04 for photographic media; R-09 for any other source kept redistributable while people are unverified.
            found.append("personal_data_unknown_redistribution")
    if restricted is True and decisions.get("restrictedMetadata") != "withheld_hash_and_reason_only":
        found.append("restricted_metadata_not_withheld")
    if restricted == "unknown" and not (decisions.get("restrictedMetadata") == "withheld_hash_and_reason_only"
                                        or str(decisions.get("restrictedMetadata") or "").endswith("_not_copied")):
        found.append("restricted_metadata_unknown_not_withheld")
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


def apply_patch(base: dict, patch) -> dict:
    """A patch that is not an object, or whose dotted path crosses a non-object value, is an invalid fixture file (exit 2)."""
    if not isinstance(patch, dict) or not isinstance(patch.get("set") or {}, dict) or not isinstance(patch.get("delete") or [], list) \
            or not all(isinstance(dotted, str) and dotted for dotted in patch.get("delete") or []):
        raise InputError("invalid_structure:cases:patch")
    record = copy.deepcopy(base)
    for dotted, value in (patch.get("set") or {}).items():
        node, parts = record, dotted.split(".")
        for part in parts[:-1]:
            node = node.setdefault(part, {})
            if not isinstance(node, dict):
                raise InputError("invalid_structure:cases:patch:" + dotted)
        node[parts[-1]] = value
    for dotted in patch.get("delete") or []:
        node, parts = record, str(dotted).split(".")
        for part in parts[:-1]:
            node = node.get(part, {})
            if not isinstance(node, dict):
                raise InputError("invalid_structure:cases:patch:" + str(dotted))
        node.pop(parts[-1], None)
    return record


def registry_ids() -> dict:
    found = {}
    for prefix, (path, ids) in REGISTRIES.items():
        try:
            found[prefix] = [f"{prefix}:{i}" for i in ids(read_json(REPO / path))]
        except (KeyError, TypeError, IndexError):
            raise InputError("invalid_structure:" + Path(path).name) from None
    return found


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
        # A negative fixture names every violation it produces, so an unexpected extra code also fails the fixture.
        ok = (got == []) if expect == "accept" else (bool(got) and set(expect) == set(got))
        results.append({"caseId": case["caseId"], "expect": expect, "got": got, "ok": ok})
    return results


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--boundary", type=Path, default=BOUNDARY)
    parser.add_argument("--cases", type=Path, default=CASES)
    args = parser.parse_args(argv)
    try:
        policy_bytes, boundary_bytes, cases_bytes = POLICY.read_bytes() if POLICY.is_file() else b"", b"", b""
        policy = load_policy(read_json(POLICY))
        boundary = read_json(args.boundary)
        cases = read_json(args.cases)
        if not isinstance(boundary, dict) or not isinstance(boundary.get("sources"), list) or not all(isinstance(s, dict) for s in boundary["sources"]):
            raise InputError("invalid_structure:" + args.boundary.name)
        if not isinstance(cases, dict) or not isinstance(cases.get("cases"), list) or not isinstance(cases.get("baseRecord"), dict) \
                or any(not isinstance(c, dict) or "caseId" not in c or not (c.get("expect") == "accept" or isinstance(c.get("expect"), list))
                       for c in cases["cases"]):
            raise InputError("invalid_structure:cases")
        case_ids = [c["caseId"] for c in cases["cases"]]
        duplicates = sorted({str(i) for i in case_ids if case_ids.count(i) > 1})
        if duplicates:
            # A copied fixture that kept its id would silently run twice and change the fixture count.
            raise InputError("invalid_structure:cases:duplicate_caseid:" + duplicates[0])
        join = join_report(boundary["sources"], policy)
        fixtures = run_fixtures(cases, policy)
        boundary_bytes, cases_bytes = args.boundary.read_bytes(), args.cases.read_bytes()
    except InputError as error:
        # Only input-shape problems are reported as cannot_start; any other exception is a checker defect and propagates.
        print(json.dumps({"state": "cannot_start", "error": str(error)}))
        return 2
    passed = (not join["missingJudgments"] and not join["unexpectedJudgments"] and not join["duplicateJudgments"]
              and not join["recordsWithViolations"] and join["everySourceHasConditionAndLimit"] and all(r["ok"] for r in fixtures))
    report = {"schemaVersion": 1, "kind": "m0-02b-boundary-check", "state": "consistent" if passed else "inconsistent",
              "policySha256": hashlib.sha256(policy_bytes).hexdigest(), "boundarySha256": hashlib.sha256(boundary_bytes).hexdigest(),
              "casesSha256": hashlib.sha256(cases_bytes).hexdigest(), "join": join,
              "fixtures": {"total": len(fixtures), "ok": sum(r["ok"] for r in fixtures), "results": fixtures}}
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main())
