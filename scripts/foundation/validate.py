#!/usr/bin/env python3
"""Validate the tracked public fixture using Python's standard library only.

The JSON Schema evaluator implements only the explicit keyword subset below.
Unknown schema keywords fail closed; this is not a general JSON Schema engine.
No network requests, path arguments, asset loads, or geometry operations occur.
"""

import argparse
import json
from pathlib import Path
import re
import sys


ROOT = Path(__file__).resolve().parents[2]
MAX_JSON_BYTES = 1024 * 1024
DISCLAIMER = "KORAIL 검증 전 예시"
ANCHOR_IDS = [f"anchor-{index:02d}" for index in range(1, 6)]
SUPPORTED_KEYWORDS = {
    "$schema", "$id", "title", "description", "type", "const", "enum",
    "required", "additionalProperties", "properties", "items", "minItems",
    "maxItems", "minLength", "pattern",
}
SUPPORTED_TYPES = {"object": dict, "array": list, "string": str, "boolean": bool}
ANCHOR_SCHEMA = {
    "type": "object",
    "additionalProperties": False,
    "required": ["schemaVersion", "mapId", "provisional", "disclaimer", "dataClassification", "anchors"],
    "properties": {
        "schemaVersion": {"type": "string", "const": "1.0"},
        "mapId": {"type": "string", "const": "synthetic-room"},
        "provisional": {"type": "boolean", "const": True},
        "disclaimer": {"type": "string", "const": DISCLAIMER},
        "dataClassification": {"type": "string", "const": "mock"},
        "anchors": {
            "type": "array", "minItems": 5, "maxItems": 5,
            "items": {
                "type": "object", "additionalProperties": False,
                "required": ["anchorId", "temporaryDisplayName"],
                "properties": {
                    "anchorId": {"type": "string", "enum": ANCHOR_IDS},
                    "temporaryDisplayName": {"type": "string", "minLength": 1, "pattern": r"\S"},
                },
            },
        },
    },
}


class ValidationInputError(ValueError):
    """An input could not be safely read as strict UTF-8 JSON."""


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValidationInputError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def _reject_constant(value):
    raise ValidationInputError(f"non-JSON numeric constant: {value}")


def load_json(path):
    """Read bounded, strict JSON; report expected input failures uniformly."""
    path = Path(path)
    try:
        with path.open("rb") as stream:
            content = stream.read(MAX_JSON_BYTES + 1)
        if len(content) > MAX_JSON_BYTES:
            raise ValidationInputError(f"JSON input exceeds {MAX_JSON_BYTES} bytes")
        return json.loads(
            content.decode("utf-8"),
            object_pairs_hook=_unique_object,
            parse_constant=_reject_constant,
        )
    except (OSError, UnicodeError, ValueError, RecursionError) as error:
        raise ValidationInputError(f"{path.name}: {error}") from error


def _read_repository_json(relative_path):
    path = (ROOT / relative_path).resolve()
    try:
        path.relative_to(ROOT)
    except ValueError as error:
        raise ValidationInputError(f"input must remain inside repository: {relative_path}") from error
    return load_json(path)


def _check_schema(schema, path="schema"):
    """Reject unsupported/malformed schema rules before validating any data."""
    if not isinstance(schema, dict):
        return [f"{path}: schema must be an object"]
    errors = [f"{path}: unsupported schema keyword {key}" for key in schema if key not in SUPPORTED_KEYWORDS]
    if not isinstance(schema.get("type"), str) or schema["type"] not in SUPPORTED_TYPES:
        errors.append(f"{path}.type: expected a supported type")
    for key in ("minItems", "maxItems", "minLength"):
        if key in schema and (type(schema[key]) is not int or schema[key] < 0):
            errors.append(f"{path}.{key}: expected nonnegative integer")
    if "pattern" in schema:
        try:
            re.compile(schema["pattern"])
        except (TypeError, re.error):
            errors.append(f"{path}.pattern: invalid regular expression")
    if "enum" in schema and (not isinstance(schema["enum"], list) or not schema["enum"]):
        errors.append(f"{path}.enum: expected nonempty array")
    if "required" in schema and (
        not isinstance(schema["required"], list)
        or any(not isinstance(key, str) for key in schema["required"])
    ):
        errors.append(f"{path}.required: expected array of strings")
    if "additionalProperties" in schema and type(schema["additionalProperties"]) is not bool:
        errors.append(f"{path}.additionalProperties: expected boolean")
    if "properties" in schema:
        if not isinstance(schema["properties"], dict):
            errors.append(f"{path}.properties: expected object")
        else:
            for key, child in schema["properties"].items():
                errors.extend(_check_schema(child, f"{path}.properties.{key}"))
    if "items" in schema:
        errors.extend(_check_schema(schema["items"], f"{path}.items"))
    return errors


def _json_equal(left, right):
    # Python considers True == 1; JSON Schema does not.
    return type(left) is type(right) and left == right


def _validate_node(value, schema, path):
    expected_type = SUPPORTED_TYPES[schema["type"]]
    if type(value) is not expected_type:
        return [f"{path}: expected {schema['type']}"]
    errors = []
    if "const" in schema and not _json_equal(value, schema["const"]):
        errors.append(f"{path}: expected constant {schema['const']!r}")
    if "enum" in schema and not any(_json_equal(value, option) for option in schema["enum"]):
        errors.append(f"{path}: unsupported value")
    if isinstance(value, dict):
        properties = schema.get("properties", {})
        for key in schema.get("required", []):
            if key not in value:
                errors.append(f"{path}.{key}: required field missing")
        for key, child in value.items():
            if key in properties:
                errors.extend(_validate_node(child, properties[key], f"{path}.{key}"))
            elif schema.get("additionalProperties") is False:
                errors.append(f"{path}.{key}: unsupported field")
    elif isinstance(value, list):
        if len(value) < schema.get("minItems", 0):
            errors.append(f"{path}: minItems={schema['minItems']} violated")
        if len(value) > schema.get("maxItems", len(value)):
            errors.append(f"{path}: maxItems={schema['maxItems']} violated")
        if "items" in schema:
            for index, child in enumerate(value):
                errors.extend(_validate_node(child, schema["items"], f"{path}[{index}]"))
    elif isinstance(value, str):
        if len(value) < schema.get("minLength", 0):
            errors.append(f"{path}: text must not be empty")
        if "pattern" in schema and re.search(schema["pattern"], value) is None:
            errors.append(f"{path}: text does not match required pattern")
    return errors


def _duplicates(values, path):
    seen = set()
    errors = []
    for value in values:
        if value in seen:
            errors.append(f"{path}: duplicate ID {value}")
        seen.add(value)
    return errors


def validate_bundle(scenario, anchors, schema):
    """Return diagnostics for schema rules, unique IDs and reference integrity."""
    errors = _check_schema(schema)
    if errors:
        return errors
    errors.extend(_validate_node(scenario, schema, "scenario"))
    errors.extend(_validate_node(anchors, ANCHOR_SCHEMA, "anchors"))
    if errors:
        return errors

    roles = scenario["roles"]
    role_ids = {role["roleId"] for role in roles}
    anchor_ids = {anchor["anchorId"] for anchor in anchors["anchors"]}
    for field in ("roleId", "actionId", "targetAnchorId"):
        errors.extend(_duplicates([role[field] for role in roles], f"scenario.roles.{field}"))
    errors.extend(_duplicates([anchor["anchorId"] for anchor in anchors["anchors"]], "anchors.anchors.anchorId"))
    if scenario["representativeRoleId"] not in role_ids:
        errors.append("scenario.representativeRoleId: unknown role")
    if scenario["mapId"] != anchors["mapId"]:
        errors.append("scenario.mapId: does not match anchor catalog mapId")
    for index, role in enumerate(roles):
        path = f"scenario.roles[{index}]"
        if role["targetAnchorId"] not in anchor_ids:
            errors.append(f"{path}.targetAnchorId: unknown anchor")
        events = role["expectedVirtualTeamEvents"]
        event_roles = [event["roleId"] for event in events]
        errors.extend(_duplicates(event_roles, f"{path}.expectedVirtualTeamEvents"))
        if set(event_roles) != role_ids - {role["roleId"]}:
            errors.append(f"{path}.expectedVirtualTeamEvents: require each of the other four roles exactly once")
    return errors


def main(argv=None):
    parser = argparse.ArgumentParser(description="Validate fixed, tracked synthetic foundation files; no downloads or Unity required.")
    parser.parse_args(argv)
    try:
        scenario = _read_repository_json("foundation/scenarios/foundation-demo.json")
        anchors = _read_repository_json("foundation/anchors/synthetic-room.json")
        schema = _read_repository_json("schemas/foundation-scenario.schema.json")
        errors = validate_bundle(scenario, anchors, schema)
    except (ValidationInputError, RecursionError) as error:
        errors = [str(error)]
    if errors:
        for error in errors:
            print(f"FAIL: {error}", file=sys.stderr)
        return 1
    print("PASS: synthetic foundation fixture - 5 roles, 5 anchor references, 20 virtual team notifications; provisional example only")
    return 0


if __name__ == "__main__":
    sys.exit(main())
