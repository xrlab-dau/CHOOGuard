"""Run-local baseline/receipt oracles; reuses the existing strict plan parser."""
import copy
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(ROOT / "docs/CHOOGuard_Story_Plan_v4/tools"))
from planlib import read_json, safe_path, PlanError
from jsonschema import Draft202012Validator


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_digest():
    files = [p for folder in ("Assets", "Packages", "ProjectSettings") for p in (ROOT / folder).rglob("*") if p.is_file()]
    files.append(ROOT / "docs/build/baseline.schema.json")
    payload = "".join(p.relative_to(ROOT).as_posix() + "\0" + sha(p) + "\n" for p in sorted(files, key=lambda p: p.relative_to(ROOT).as_posix()))
    return hashlib.sha256(payload.encode()).hexdigest()


def validate(document, schema):
    Draft202012Validator(schema).validate(document)
    ref = document["buildReceiptRef"]
    receipt_path = safe_path(ROOT, ref["path"])
    if sha(receipt_path) != ref["sha256"]:
        raise ValueError("RECEIPT_HASH_MISMATCH")
    receipt = read_json(receipt_path)
    Draft202012Validator({"$ref": "#/$defs/receipt", "$defs": schema["$defs"]}).validate(receipt)
    assert receipt_path.parent.name == receipt["runId"]
    assert receipt["sourceTreeDigest"] == source_digest()
    assert receipt["requestedTarget"] == "StandaloneWindows64"
    assert receipt["buildStatus"] == receipt["runStatus"] == "NOT_RUN"
    assert receipt["outputs"] == []
    assert receipt["host"] == document["os"]
    assert receipt["editorVersion"] == document["editorVersion"]
    assert receipt["packageLockSha256"] == document["packageLockSha256"] == sha(ROOT / "Packages/packages-lock.json")
    assert "m_EditorVersion: " + document["editorVersion"] in (ROOT / "ProjectSettings/ProjectVersion.txt").read_text().splitlines()
    pipeline = safe_path(ROOT, document["renderPipeline"]["assetPath"])
    assert document["renderPipeline"]["assetSha256"] == sha(pipeline)
    lock = read_json(ROOT / "Packages/packages-lock.json")
    assert document["renderPipeline"]["packageVersion"] == lock["dependencies"]["com.unity.render-pipelines.universal"]["version"]
    return receipt


def main():
    output = Path(sys.argv[1]).resolve()
    fixture = Path(sys.argv[2]).resolve()
    if output.exists() or fixture.exists():
        raise ValueError("Use new output and scratch fixture directories")
    fixture.mkdir(parents=True)
    checks = []
    schema = read_json(ROOT / "docs/build/baseline.schema.json")
    Draft202012Validator.check_schema(schema)
    document = read_json(ROOT / "docs/build/baseline.json")
    receipt = validate(document, schema)
    checks.append("actual_baseline_receipt_hashes_paths_status_and_source_digest")
    def rejected(name, action):
        try:
            action()
        except (ValueError, AssertionError, OSError, json.JSONDecodeError, __import__('jsonschema').ValidationError):
            checks.append(name)
            return
        raise AssertionError("Counterexample unexpectedly accepted: " + name)
    for name, text in (("duplicate_key", '{"x":1,"x":2}'), ("NaN", '{"x":NaN}')):
        path = fixture / (name + ".json")
        path.write_text(text)
        rejected(name, lambda path=path: read_json(path))
    for name, value in (("path_escape", "../escape.json"), ("absolute_path", "/tmp/escape.json"), ("missing_receipt", "docs/build/nonexistent-receipt.json")):
        damaged = copy.deepcopy(document)
        damaged["buildReceiptRef"]["path"] = value
        rejected(name, lambda damaged=damaged: validate(damaged, schema))
    damaged = copy.deepcopy(document)
    damaged["buildReceiptRef"]["sha256"] = "0" * 64
    rejected("receipt_hash_mismatch", lambda: validate(damaged, schema))
    damaged = copy.deepcopy(document)
    damaged["renderPipeline"]["assetSha256"] = "0" * 64
    rejected("pipeline_hash_mismatch", lambda: validate(damaged, schema))
    damaged = copy.deepcopy(document)
    damaged["backend"]["development"] = False
    rejected("nondevelopment_backend", lambda: validate(damaged, schema))
    damaged_receipt = copy.deepcopy(receipt)
    damaged_receipt["buildStatus"] = "PASS"
    receipt_validator = Draft202012Validator({"$ref": "#/$defs/receipt", "$defs": schema["$defs"]})
    rejected("unknown_status", lambda: receipt_validator.validate(damaged_receipt))
    damaged_receipt["buildStatus"] = "NOT_RUN"
    damaged_receipt["runStatus"] = "SUCCEEDED"
    rejected("not_run_build_with_successful_run", lambda: receipt_validator.validate(damaged_receipt))
    damaged = copy.deepcopy(document)
    damaged["backend"]["unexpected"] = 1
    rejected("unknown_nested_property", lambda: validate(damaged, schema))
    result = {"status": "PASS", "checks": checks, "count": len(checks), "sourceTreeDigest": source_digest(), "baselineSha256": sha(ROOT / "docs/build/baseline.json"), "receipt": document["buildReceiptRef"]}
    with output.open("x") as stream:
        json.dump(result, stream, indent=2)
        stream.write("\n")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
