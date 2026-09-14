#!/usr/bin/env python3
"""Local M1-05 record candidates. No network, command execution, or approval grant."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re

POLICY = "M1-05/raw-retention-v1"
KINDS = ("input", "output", "tool", "edit", "test", "failure", "retry", "cancel", "review", "approval", "redaction")
OUTCOMES = ("recorded", "passed", "failed", "cancelled", "skipped", "unknown")
CLASSIFICATIONS = ("public", "secret", "personal", "absolute_path", "restricted", "unknown")
REASONS = ("secret", "personal", "absolute_path", "restricted", "suspected_sensitive")
REPO = Path(__file__).resolve().parents[3]
SUSPECT = re.compile(
    r"(?i)(?:\b(?:token|password|secret|api[_-]?key)\s*[:=]|\bbearer\s+\S+|"
    r"[\w.+-]+@[\w.-]+\.[a-z]{2,}|[a-z]:[/\\]|\\\\|"
    r"(?:^|[\s\"'=])/(?:[^/\s]+/)*[^\s]+|private-data|restricted[/\\]|\bKORAIL\b)"
)


class RecordError(ValueError):
    """Messages contain fixed reasons, never input contents or filesystem paths."""


def encoded(record):
    return (json.dumps(record, ensure_ascii=False, sort_keys=True, indent=2, allow_nan=False) + "\n").encode("utf-8")


def digest(data):
    return hashlib.sha256(data).hexdigest()


def _keys(value, names):
    if not isinstance(value, dict) or set(value) != set(names):
        raise RecordError("unsupported or missing record fields")


def _choice(value, choices):
    if not isinstance(value, str) or value not in choices:
        raise RecordError("unsupported control value")


def _version(record, state):
    if type(record["schemaVersion"]) is not int or record["schemaVersion"] != 1 or record["state"] != state:
        raise RecordError("unsupported version or record state")


def _hash(value):
    if not isinstance(value, str) or not re.fullmatch(r"[0-9a-f]{64}", value):
        raise RecordError("invalid digest")


def _events(value):
    if not isinstance(value, list) or not value:
        raise RecordError("a nonempty event list is required")


def _typed_event(event, index):
    if type(event["ordinal"]) is not int or event["ordinal"] != index:
        raise RecordError("event ordinals must be contiguous")
    _choice(event["kind"], KINDS)
    _choice(event["outcome"], OUTCOMES)


def redact(raw, raw_sha256):
    _keys(raw, ("schemaVersion", "state", "events"))
    _version(raw, "raw")
    _hash(raw_sha256)
    _events(raw["events"])
    events = []
    for index, item in enumerate(raw["events"]):
        _keys(item, ("kind", "outcome", "classification", "content", "requiredForEvidence"))
        _choice(item["kind"], KINDS)
        _choice(item["outcome"], OUTCOMES)
        _choice(item["classification"], CLASSIFICATIONS)
        if not isinstance(item["content"], str) or type(item["requiredForEvidence"]) is not bool:
            raise RecordError("invalid event content or evidence requirement")
        try:
            item["content"].encode("utf-8")
        except UnicodeError:
            raise RecordError("event content is not valid Unicode") from None
        if item["classification"] == "unknown":
            raise RecordError("unknown classification: preserve raw and obtain classification")
        if item["requiredForEvidence"]:
            raise RecordError("required content would be omitted: preserve raw and review evidence scope")
        result = {"ordinal": index, "kind": item["kind"], "outcome": item["outcome"]}
        reason = item["classification"] if item["classification"] != "public" else None
        if reason is None and SUSPECT.search(item["content"]):
            reason = "suspected_sensitive"
        if reason:
            result["removal"] = {"reason": reason, "retentionPolicy": POLICY}
        else:
            result["content"] = item["content"]
        events.append(result)
    return {"schemaVersion": 1, "state": "redacted_private", "rawSha256": raw_sha256,
            "retentionPolicy": POLICY, "approval": "pending", "events": events}


def project_public(redacted):
    _keys(redacted, ("schemaVersion", "state", "rawSha256", "retentionPolicy", "approval", "events"))
    _version(redacted, "redacted_private")
    _hash(redacted["rawSha256"])
    if redacted["retentionPolicy"] != POLICY or redacted["approval"] != "pending":
        raise RecordError("unexpected policy or approval")
    _events(redacted["events"])
    events = []
    omitted = 0
    for index, item in enumerate(redacted["events"]):
        field = "content" if "content" in item else "removal"
        _keys(item, ("ordinal", "kind", "outcome", field))
        _typed_event(item, index)
        result = {"ordinal": index, "kind": item["kind"], "outcome": item["outcome"]}
        if field == "content":
            if not isinstance(item["content"], str):
                raise RecordError("invalid private content")
            result["disposition"] = "public_text_omitted"
            omitted += 1
        else:
            _keys(item["removal"], ("reason", "retentionPolicy"))
            _choice(item["removal"]["reason"], REASONS)
            if item["removal"]["retentionPolicy"] != POLICY:
                raise RecordError("unexpected retention policy")
            result["disposition"] = "removed"
            result["removal"] = dict(item["removal"])
        events.append(result)
    return {"schemaVersion": 1, "state": "public_candidate", "approval": "pending",
            "publicTextOmitted": omitted, "events": events}


def _validate_public(record):
    _keys(record, ("schemaVersion", "state", "approval", "publicTextOmitted", "events"))
    _version(record, "public_candidate")
    if record["approval"] != "pending":
        raise RecordError("candidate cannot grant approval")
    _events(record["events"])
    omitted = 0
    for index, item in enumerate(record["events"]):
        if not isinstance(item, dict):
            raise RecordError("invalid public event")
        removed = item.get("disposition") == "removed"
        names = ("ordinal", "kind", "outcome", "disposition") + (("removal",) if removed else ())
        _keys(item, names)
        _typed_event(item, index)
        _choice(item["disposition"], ("removed", "public_text_omitted"))
        if removed:
            _keys(item["removal"], ("reason", "retentionPolicy"))
            _choice(item["removal"]["reason"], REASONS)
            if item["removal"]["retentionPolicy"] != POLICY:
                raise RecordError("unexpected retention policy")
        else:
            omitted += 1
    if type(record["publicTextOmitted"]) is not int or record["publicTextOmitted"] != omitted:
        raise RecordError("incorrect public omission count")


def _object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise RecordError("duplicate JSON key")
        result[key] = value
    return result


def decode(data):
    try:
        return json.loads(data.decode("utf-8"), object_pairs_hook=_object,
                          parse_constant=lambda _: _reject_constant())
    except RecordError:
        raise
    except (ValueError, RecursionError):
        raise RecordError("invalid UTF-8 JSON record") from None


def _reject_constant():
    raise RecordError("nonfinite JSON number")


def _local_path(path):
    path = Path(os.path.abspath(path))
    if path.drive.startswith("\\\\"):
        raise RecordError("network paths are not supported")
    for entry in (path, *path.parents):
        if entry.is_symlink() or (hasattr(entry, "is_junction") and entry.is_junction()):
            raise RecordError("linked paths are not supported")
    return path


def _overlap(left, right):
    return left.is_relative_to(right) or right.is_relative_to(left)


def _manifest(payload):
    files = {"events.json": {"version": 1, "sha256": digest(payload), "bytes": len(payload)}}
    return {"schemaVersion": 1, "state": "public_manifest", "approval": "pending",
            "files": files, "filesDigest": digest(encoded(files))}


def _write_new(path, data):
    # Set owner-only permissions at creation, before any private bytes are written.
    with open(path, "xb", opener=lambda name, flags: os.open(name, flags, 0o600)) as stream:
        stream.write(data)


def build(raw_path, private_output, public_output):
    raw_path, private_output, public_output = map(_local_path, (raw_path, private_output, public_output))
    if raw_path.is_relative_to(REPO) or private_output.is_relative_to(REPO):
        raise RecordError("raw and private records must remain outside the source checkout")
    if any(_overlap(a, b) for a, b in ((raw_path, private_output), (raw_path, public_output), (private_output, public_output))):
        raise RecordError("input, private output and public output must be disjoint")
    if not raw_path.is_file():
        raise RecordError("raw input must be a regular file")
    for output in (private_output, public_output):
        if output.exists():
            raise RecordError("output already exists; choose a fresh run directory")
        if not output.parent.is_dir():
            raise RecordError("output parent must already exist")
    data = raw_path.read_bytes()
    private = redact(decode(data), digest(data))
    private_bytes = encoded(private)
    payload = encoded(project_public(private))
    manifest = _manifest(payload)
    # Trusted, exclusively owned local parents are required. On I/O failure, keep
    # partial output for diagnosis; never remove, reuse, or overwrite it on retry.
    private_output.mkdir(mode=0o700)
    public_output.mkdir()
    _write_new(private_output / "redacted.json", private_bytes)
    _write_new(public_output / "events.json", payload)
    _write_new(public_output / "manifest.json", encoded(manifest))
    return verify(public_output)


def verify(public_output):
    root = _local_path(public_output)
    if not root.is_dir() or {p.name for p in root.iterdir()} != {"events.json", "manifest.json"}:
        raise RecordError("candidate bundle must contain exactly its two declared files")
    for name in ("events.json", "manifest.json"):
        path = _local_path(root / name)
        if not path.is_file():
            raise RecordError("candidate payload must be a regular file")
    payload = (root / "events.json").read_bytes()
    _validate_public(decode(payload))
    manifest = decode((root / "manifest.json").read_bytes())
    _keys(manifest, ("schemaVersion", "state", "approval", "files", "filesDigest"))
    _version(manifest, "public_manifest")
    if manifest != _manifest(payload):
        raise RecordError("manifest or payload digest mismatch")
    # bool compares equal to integer in Python; require exact metadata types too.
    metadata = manifest["files"]["events.json"]
    if type(metadata["version"]) is not int or type(metadata["bytes"]) is not int:
        raise RecordError("invalid manifest numeric metadata")
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    build_parser = sub.add_parser("build")
    build_parser.add_argument("--raw", type=Path, required=True)
    build_parser.add_argument("--private-output", type=Path, required=True)
    build_parser.add_argument("--public-output", type=Path, required=True)
    verify_parser = sub.add_parser("verify")
    verify_parser.add_argument("--public-output", type=Path, required=True)
    args = parser.parse_args()
    try:
        result = build(args.raw, args.private_output, args.public_output) if args.command == "build" else verify(args.public_output)
        print(json.dumps({"status": "candidate_verified", "approval": "pending", "filesDigest": result["filesDigest"]}))
    except RecordError as error:
        parser.exit(1, f"Record pipeline refused: {error}\n")
    except OSError:
        parser.exit(1, "Record I/O failed; preserve input and any partial output, then use fresh output directories.\n")


if __name__ == "__main__":
    main()
