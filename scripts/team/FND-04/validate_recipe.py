#!/usr/bin/env python3
"""Validate FND-04 asset-replacement and provenance recipes from synthetic metadata alone.

The validator is schema-driven (a small JSON Schema subset evaluator, stdlib only) plus a
semantic refusal layer that produces named refusal codes for cross-field defects the schema
cannot express: missing source/output digest/units/license, source and target mismatches,
and any mutation of the frozen coordinate/anchor/collision contracts.

Passing this contract means the recipe's declared metadata is internally consistent and
digest-bound. It never means scene, collision, runtime or facility acceptance, and it never
executes computer vision or calls an external generative service.

Usage:
    python3 scripts/team/FND-04/validate_recipe.py foundation/team-fixtures/FND-04/cases.json
    python3 scripts/team/FND-04/validate_recipe.py --check <recipe.json>
    python3 scripts/team/FND-04/validate_recipe.py <recipe.json> --verify-files --root .
    python3 scripts/team/FND-04/validate_recipe.py <recipe.json> --inspect-manifest
    python3 scripts/team/FND-04/validate_recipe.py --test
"""
import argparse
import copy
import hashlib
import json
import re
import sys
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parents[3]
SCHEMA_PATH = "foundation/team-fixtures/FND-04/recipe.schema.json"
CASES_PATH = "foundation/team-fixtures/FND-04/cases.json"
MANIFEST_PATH = "foundation/art/asset-manifest.json"

# Frozen contracts owned by existing coordinate/anchor/collision work. A recipe may declare
# them excluded and read-only; it may never change or write them.
FROZEN_CONTRACTS = ("coordinateAdapter", "anchors", "collisionBoxes")
BOUNDARY_TOKENS = ("mesh", "components", "pivot", "boundsUnity", "materialNames", "triangles", "file")
EXPECTED_UNITS = {
    "system": "metres",
    "lengthUnit": "m",
    "upAxis": "+Y",
    "forwardAxis": "+Z",
    "coordinateAdapter": "Unity(x,y,z) to Blender(-x,-z,y)",
}
SUPPORTED_SCHEMA_VERSION = 1
SHA256 = re.compile(r"[0-9a-f]{64}")
DATE = re.compile(r"[0-9]{4}-[0-9]{2}-[0-9]{2}")
ACCEPTANCE_CLAIM = re.compile(
    r"(?i)(\baccepted\b|\bapproved\b|\bcertified\b|\bverified\b|\bpassed\b|검증\s*완료|승인\s*완료|합격)"
)

CODES = (
    "RECIPE_SCHEMA_INVALID",
    "RECIPE_SCHEMA_UNSUPPORTED_VERSION",
    "RECIPE_SOURCE_MISSING",
    "RECIPE_SOURCE_MISMATCH",
    "RECIPE_TARGET_MISSING",
    "RECIPE_TARGET_MISMATCH",
    "RECIPE_OUTPUT_DIGEST_MISSING",
    "RECIPE_DIGEST_MISMATCH",
    "RECIPE_DUPLICATE_INPUT",
    "RECIPE_UNITS_MISSING",
    "RECIPE_UNITS_MISMATCH",
    "RECIPE_LICENSE_MISSING",
    "RECIPE_LICENSE_MISMATCH",
    "RECIPE_VERSION_MISSING",
    "RECIPE_CONTRACT_MUTATION",
    "RECIPE_CONTRACT_SET_INCOMPLETE",
    "RECIPE_CV_EXECUTION_DECLARED",
    "RECIPE_EXTERNAL_SERVICE_DECLARED",
    "RECIPE_ACCEPTANCE_OVERCLAIM",
    "RECIPE_NON_CLAIMS_MISSING",
    "RECIPE_PATH_INVALID",
    "RECIPE_FILE_MISSING",
    "RECIPE_CASE_INVALID",
    "RECIPE_SUITE_INVALID",
)

NOTE = (
    "A passing recipe is digest-bound metadata consistency only. It is not scene, collision, "
    "runtime or facility acceptance, and it grants no procedure suitability."
)


class RecipeError(ValueError):
    """Messages carry fixed reasons, never input contents."""


# --------------------------------------------------------------------------- JSON loading


def _object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise RecipeError("duplicate JSON key in recipe document")
        result[key] = value
    return result


def _constant(_):
    raise RecipeError("nonfinite JSON number in recipe document")


def decode(data):
    try:
        return json.loads(data.decode("utf-8"), object_pairs_hook=_object, parse_constant=_constant)
    except RecipeError:
        raise
    except (ValueError, RecursionError, UnicodeError):
        raise RecipeError("invalid UTF-8 JSON recipe document") from None


def load(path):
    return decode(Path(path).read_bytes())


def document_path(path, root):
    """A given path is absolute or relative to the working directory; --root is the fallback."""
    candidate = Path(path)
    if candidate.is_absolute() or candidate.is_file():
        return candidate
    return root / candidate


# ------------------------------------------------------------- JSON Schema subset evaluator


def _jtype(value):
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "boolean"
    if isinstance(value, int):
        return "integer"
    if isinstance(value, float):
        return "number"
    if isinstance(value, str):
        return "string"
    if isinstance(value, list):
        return "array"
    if isinstance(value, dict):
        return "object"
    return "unknown"


def _is_number(value):
    return _jtype(value) in ("integer", "number")


def _type_ok(value, name):
    if name == "number":
        return _is_number(value)
    if name == "integer":
        return _jtype(value) == "integer"
    return _jtype(value) == name


def _same(left, right):
    """JSON equality that keeps booleans distinct from 0/1 and numbers equal by value."""
    if isinstance(left, bool) != isinstance(right, bool):
        return False
    if _is_number(left) and _is_number(right):
        return left == right
    if _jtype(left) != _jtype(right):
        return False
    if isinstance(left, list):
        return len(left) == len(right) and all(_same(a, b) for a, b in zip(left, right))
    if isinstance(left, dict):
        return set(left) == set(right) and all(_same(left[k], right[k]) for k in left)
    return left == right


def _resolve(ref, root):
    if not isinstance(ref, str) or not ref.startswith("#/"):
        raise RecipeError("unsupported schema reference")
    node = root
    for part in ref[2:].split("/"):
        node = node[part.replace("~1", "/").replace("~0", "~")]
    return node


def _schema_errors(instance, schema, path, root, out):
    if schema is True:
        return
    if schema is False:
        out.append((path, "value is not permitted here"))
        return
    if not isinstance(schema, dict):
        raise RecipeError("malformed schema fragment")
    if "$ref" in schema:
        _schema_errors(instance, _resolve(schema["$ref"], root), path, root, out)
        return
    if "type" in schema:
        names = schema["type"] if isinstance(schema["type"], list) else [schema["type"]]
        if not any(_type_ok(instance, name) for name in names):
            out.append((path, "expected type " + "/".join(names)))
            return
    if "const" in schema and not _same(instance, schema["const"]):
        out.append((path, "must equal " + json.dumps(schema["const"])))
    if "enum" in schema and not any(_same(instance, option) for option in schema["enum"]):
        out.append((path, "must be one of " + json.dumps(schema["enum"], ensure_ascii=False)))
    if "pattern" in schema and isinstance(instance, str) and not re.search(schema["pattern"], instance):
        out.append((path, "does not match " + schema["pattern"]))
    if "minLength" in schema and isinstance(instance, str) and len(instance) < schema["minLength"]:
        out.append((path, "shorter than " + str(schema["minLength"])))
    if "maxLength" in schema and isinstance(instance, str) and len(instance) > schema["maxLength"]:
        out.append((path, "longer than " + str(schema["maxLength"])))
    if "minimum" in schema and _is_number(instance) and instance < schema["minimum"]:
        out.append((path, "below minimum " + str(schema["minimum"])))
    if "maximum" in schema and _is_number(instance) and instance > schema["maximum"]:
        out.append((path, "above maximum " + str(schema["maximum"])))
    for subschema in schema.get("allOf", []):
        _schema_errors(instance, subschema, path, root, out)
    if "anyOf" in schema and not any(_accepted(instance, sub, root) for sub in schema["anyOf"]):
        out.append((path, "matches none of the permitted alternatives"))
    if "oneOf" in schema and sum(1 for sub in schema["oneOf"] if _accepted(instance, sub, root)) != 1:
        out.append((path, "must match exactly one permitted alternative"))
    if "not" in schema and _accepted(instance, schema["not"], root):
        out.append((path, "matches a forbidden form"))
    if "if" in schema:
        branch = schema.get("then") if _accepted(instance, schema["if"], root) else schema.get("else")
        if branch is not None:
            _schema_errors(instance, branch, path, root, out)
    if isinstance(instance, dict):
        for name in schema.get("required", []):
            if name not in instance:
                out.append((path + "/" + name, "required property is missing"))
        properties = schema.get("properties", {})
        extra = schema.get("additionalProperties", True)
        for name, value in instance.items():
            if name in properties:
                _schema_errors(value, properties[name], path + "/" + name, root, out)
            elif extra is False:
                out.append((path + "/" + name, "property is not declared by the contract"))
            elif isinstance(extra, dict):
                _schema_errors(value, extra, path + "/" + name, root, out)
    if isinstance(instance, list):
        if "minItems" in schema and len(instance) < schema["minItems"]:
            out.append((path, "needs at least " + str(schema["minItems"]) + " item(s)"))
        if "maxItems" in schema and len(instance) > schema["maxItems"]:
            out.append((path, "allows at most " + str(schema["maxItems"]) + " item(s)"))
        if schema.get("uniqueItems") and len({json.dumps(x, sort_keys=True, default=str) for x in instance}) != len(instance):
            out.append((path, "items must be unique"))
        if isinstance(schema.get("items"), dict):
            for index, value in enumerate(instance):
                _schema_errors(value, schema["items"], path + "/" + str(index), root, out)


def _accepted(instance, schema, root):
    probe = []
    _schema_errors(instance, schema, "", root, probe)
    return not probe


# ------------------------------------------------------------------- semantic refusal layer


def _text(value):
    return isinstance(value, str) and bool(value.strip())


def _sha(value):
    return isinstance(value, str) and bool(SHA256.fullmatch(value))


def _entries(value):
    return value if isinstance(value, list) else []


def _semantic_errors(recipe):
    """Named cross-field refusals. Runs on raw input, so every read is type-guarded."""
    errors = []
    add = lambda code, path, message: errors.append((code, path, message))
    if not isinstance(recipe, dict):
        add("RECIPE_SCHEMA_INVALID", "", "recipe must be a JSON object")
        return errors

    if _jtype(recipe.get("schemaVersion")) != "integer" or recipe.get("schemaVersion") != SUPPORTED_SCHEMA_VERSION:
        add("RECIPE_SCHEMA_UNSUPPORTED_VERSION", "schemaVersion",
            "recipe declares an unsupported schema version; migrate explicitly, never silently")

    scope = recipe.get("scope") if isinstance(recipe.get("scope"), dict) else {}
    provenance = recipe.get("provenance") if isinstance(recipe.get("provenance"), dict) else {}
    source = scope.get("source") if isinstance(scope.get("source"), dict) else {}
    target = scope.get("target") if isinstance(scope.get("target"), dict) else {}

    if not _text(source.get("ref")):
        add("RECIPE_SOURCE_MISSING", "scope.source.ref", "a declared provenance source reference is required")
    if not _text(provenance.get("sourceRef")):
        add("RECIPE_SOURCE_MISSING", "provenance.sourceRef", "provenance needs the exact source reference it derives from")
    elif _text(source.get("ref")) and source["ref"] != provenance["sourceRef"]:
        add("RECIPE_SOURCE_MISMATCH", "provenance.sourceRef", "provenance source reference differs from the scoped source")
    if not _sha(source.get("sha256")):
        add("RECIPE_DIGEST_MISMATCH", "scope.source.sha256", "the scoped source needs a sha256 binding")
    if not _text(target.get("assetId")):
        add("RECIPE_TARGET_MISSING", "scope.target.assetId", "the replacement target asset must be named")
    if not _text(target.get("manifestPath")) or not _sha(target.get("manifestSha256")):
        add("RECIPE_DIGEST_MISMATCH", "scope.target", "the target manifest needs a path and a sha256 binding")
    recipe_scope = scope.get("recipe") if isinstance(scope.get("recipe"), dict) else {}
    if not _text(recipe_scope.get("path")) or not _sha(recipe_scope.get("sha256")):
        add("RECIPE_DIGEST_MISMATCH", "scope.recipe", "the schema revision these cases were authored against must be digest-bound")

    license_value = provenance.get("license") if isinstance(provenance.get("license"), dict) else None
    if license_value is None:
        add("RECIPE_LICENSE_MISSING", "provenance.license", "replacement bytes need a license and attribution statement")
    else:
        if _jtype(license_value.get("reusePermitted")) != "boolean":
            add("RECIPE_LICENSE_MISMATCH", "provenance.license.reusePermitted", "reuse permission must be stated as a boolean")
        if license_value.get("reusePermitted") is True and str(license_value.get("id", "")).startswith("unknown_no_reuse"):
            add("RECIPE_LICENSE_MISMATCH", "provenance.license", "an unconfirmed reuse license cannot grant reuse permission")
        if not _text(license_value.get("id")) or not _text(license_value.get("attribution")):
            add("RECIPE_LICENSE_MISSING", "provenance.license", "license id and attribution are both required")
    if not _text(provenance.get("generator")):
        add("RECIPE_SOURCE_MISSING", "provenance.generator", "the generating tool must be named")
    if not _text(provenance.get("generatedAt")) or not DATE.fullmatch(str(provenance.get("generatedAt"))):
        add("RECIPE_SCHEMA_INVALID", "provenance.generatedAt", "generation date must be an ISO calendar date")

    version = recipe.get("version") if isinstance(recipe.get("version"), dict) else None
    if version is None:
        add("RECIPE_VERSION_MISSING", "version", "a versioned replacement needs asset version and revision")
    elif _jtype(version.get("assetVersion")) != "integer" or version.get("assetVersion", 0) < 1 or not _text(version.get("revision")):
        add("RECIPE_VERSION_MISSING", "version", "asset version must be a positive integer and revision must be named")

    digests = recipe.get("digests") if isinstance(recipe.get("digests"), dict) else None
    source_sha = source.get("sha256") if _sha(source.get("sha256")) else None
    if digests is None:
        add("RECIPE_OUTPUT_DIGEST_MISSING", "digests", "an output digest is mandatory for a replacement")
    else:
        if digests.get("algorithm") != "sha256":
            add("RECIPE_DIGEST_MISMATCH", "digests.algorithm", "only sha256 is accepted")
        if "output" not in digests:
            add("RECIPE_OUTPUT_DIGEST_MISSING", "digests.output", "an output digest is mandatory for a replacement")
        elif not _sha(digests.get("output")):
            add("RECIPE_DIGEST_MISMATCH", "digests.output", "output digest must be a 64-character sha256")
        if not _sha(digests.get("source")):
            add("RECIPE_DIGEST_MISMATCH", "digests.source", "source digest must be a 64-character sha256")
        elif source_sha is not None and digests["source"] != source_sha:
            add("RECIPE_DIGEST_MISMATCH", "digests.source", "source digest differs from the scoped source digest")
        seen = {}
        for index, entry in enumerate(_entries(digests.get("inputs"))):
            if not isinstance(entry, dict):
                add("RECIPE_SCHEMA_INVALID", "digests.inputs/" + str(index), "input digest entries must be objects")
                continue
            if not _text(entry.get("path")):
                add("RECIPE_SOURCE_MISSING", "digests.inputs/" + str(index), "a digest-bound input needs a path")
            elif entry["path"] in seen:
                add("RECIPE_DUPLICATE_INPUT", "digests.inputs/" + str(index), "input path is declared more than once")
            else:
                seen[entry["path"]] = True
            if not _sha(entry.get("sha256")):
                add("RECIPE_DIGEST_MISMATCH", "digests.inputs/" + str(index) + ".sha256", "input digest must be a 64-character sha256")

    units = recipe.get("units") if isinstance(recipe.get("units"), dict) else None
    if units is None:
        add("RECIPE_UNITS_MISSING", "units", "unit and axis system is mandatory")
    else:
        for name, expected in EXPECTED_UNITS.items():
            if units.get(name) != expected:
                add("RECIPE_UNITS_MISMATCH", "units." + name, "unit or axis value differs from the frozen scene contract")
        scale = units.get("scaleToMetres")
        if not _is_number(scale) or scale != 1:
            add("RECIPE_UNITS_MISMATCH", "units.scaleToMetres", "authored geometry must already be metre-scaled")

    replacement = recipe.get("replacement") if isinstance(recipe.get("replacement"), dict) else {}
    if replacement.get("runtimeAcceptance") is not False:
        add("RECIPE_ACCEPTANCE_OVERCLAIM", "replacement.runtimeAcceptance", "a recipe can never grant or withhold runtime acceptance")
    if replacement.get("sceneCollisionAcceptance") is not False:
        add("RECIPE_ACCEPTANCE_OVERCLAIM", "replacement.sceneCollisionAcceptance", "a recipe can never grant scene or collision acceptance")
    if _text(target.get("assetId")) and _text(replacement.get("targetAssetId")) and replacement["targetAssetId"] != target["assetId"]:
        add("RECIPE_TARGET_MISMATCH", "replacement.targetAssetId", "replacement target differs from the scoped target asset")
    if not _text(replacement.get("targetAssetId")):
        add("RECIPE_TARGET_MISSING", "replacement.targetAssetId", "the replacement target asset must be named")
    boundary = replacement.get("boundary") if isinstance(replacement.get("boundary"), dict) else {}
    included = [token for token in _entries(boundary.get("included"))]
    excluded = [token for token in _entries(boundary.get("excluded"))]
    for token in included:
        if token in FROZEN_CONTRACTS:
            add("RECIPE_CONTRACT_MUTATION", "replacement.boundary.included",
                "a frozen " + str(token) + " contract cannot be inside the replacement boundary")
    for token in FROZEN_CONTRACTS:
        if token not in excluded:
            add("RECIPE_CONTRACT_MUTATION", "replacement.boundary.excluded",
                "the replacement must explicitly exclude the frozen " + token + " contract")

    contracts = recipe.get("contracts") if isinstance(recipe.get("contracts"), dict) else None
    if contracts is None:
        add("RECIPE_CONTRACT_SET_INCOMPLETE", "contracts", "the frozen contract set must be declared read-only")
    else:
        read_only = contracts.get("readOnly")
        if not isinstance(read_only, list) or set(read_only) != set(FROZEN_CONTRACTS):
            add("RECIPE_CONTRACT_SET_INCOMPLETE", "contracts.readOnly",
                "coordinateAdapter, anchors and collisionBoxes must all be declared read-only")
        changed = contracts.get("changed")
        if not isinstance(changed, list) or changed:
            add("RECIPE_CONTRACT_MUTATION", "contracts.changed", "no frozen contract may be declared changed")
        if contracts.get("writeAllowed") is not False:
            add("RECIPE_CONTRACT_MUTATION", "contracts.writeAllowed", "a recipe may not write frozen contracts")

    execution = recipe.get("execution") if isinstance(recipe.get("execution"), dict) else {}
    if execution.get("cvExecution") is not False:
        add("RECIPE_CV_EXECUTION_DECLARED", "execution.cvExecution", "no CV or photogrammetry runner may be enabled by a recipe")
    if execution.get("externalServiceCalls") is not False or execution.get("generativeService") is not None:
        add("RECIPE_EXTERNAL_SERVICE_DECLARED", "execution", "no external or generative service may produce replacement bytes")

    non_claims = recipe.get("notAccepted")
    if not isinstance(non_claims, list) or not non_claims:
        add("RECIPE_NON_CLAIMS_MISSING", "notAccepted", "a recipe must state what it does not establish")
    else:
        for index, entry in enumerate(non_claims):
            if not _text(entry):
                add("RECIPE_NON_CLAIMS_MISSING", "notAccepted/" + str(index), "non-claims must be non-empty text")
            elif ACCEPTANCE_CLAIM.search(entry):
                add("RECIPE_ACCEPTANCE_OVERCLAIM", "notAccepted/" + str(index),
                    "a non-claim may not assert scene, collision or runtime acceptance")
    return errors


# ------------------------------------------------------------------------ file verification


def local_path(root, relative):
    if not _text(relative) or "\\" in relative or ":" in relative:
        raise RecipeError("invalid relative path in recipe")
    path = PurePosixPath(relative)
    if path.is_absolute() or ".." in path.parts:
        raise RecipeError("recipe path escapes the verified root")
    resolved = (root / relative).resolve()
    try:
        resolved.relative_to(Path(root).resolve())
    except ValueError:
        raise RecipeError("recipe path escapes the verified root") from None
    return resolved


def _bindings(recipe):
    scope = recipe.get("scope") if isinstance(recipe.get("scope"), dict) else {}
    source = scope.get("source") if isinstance(scope.get("source"), dict) else {}
    recipe_scope = scope.get("recipe") if isinstance(scope.get("recipe"), dict) else {}
    target = scope.get("target") if isinstance(scope.get("target"), dict) else {}
    digests = recipe.get("digests") if isinstance(recipe.get("digests"), dict) else {}
    replacement = recipe.get("replacement") if isinstance(recipe.get("replacement"), dict) else {}
    bindings = [
        ("scope.source.ref", source.get("ref"), source.get("sha256")),
        ("scope.recipe.path", recipe_scope.get("path"), recipe_scope.get("sha256")),
        ("scope.target.manifestPath", target.get("manifestPath"), target.get("manifestSha256")),
    ]
    for label, container in (("digests.inputs", digests.get("inputs")), ("replacement.inputs", replacement.get("inputs")),
                             ("replacement.outputs", replacement.get("outputs"))):
        for index, entry in enumerate(_entries(container)):
            if isinstance(entry, dict):
                bindings.append((label + "/" + str(index) + ".path", entry.get("path"), entry.get("sha256")))
    return bindings


def verify_files(recipe, root):
    """Hash the recipe's file bindings against a checkout. Read-only; never writes a file."""
    errors = []
    for path_label, relative, expected in _bindings(recipe):
        if not _text(relative) or "://" in relative:
            continue
        if not _sha(expected):
            continue
        try:
            resolved = local_path(root, relative)
        except RecipeError as error:
            errors.append((path_label + ":" + relative, str(error), "RECIPE_PATH_INVALID"))
            continue
        if not resolved.is_file():
            errors.append((path_label + ":" + relative, "bound file is absent from the verified root", "RECIPE_FILE_MISSING"))
            continue
        if hashlib.sha256(resolved.read_bytes()).hexdigest() != expected:
            errors.append((path_label + ":" + relative, "bound digest does not match the file on disk", "RECIPE_DIGEST_MISMATCH"))
    return errors


# --------------------------------------------------------------------------- recipe checks


def validate_recipe(recipe, schema, root=None, verify=False):
    errors = [{"code": code, "path": path, "message": message} for code, path, message in _semantic_errors(recipe)]
    probe = []
    _schema_errors(recipe, schema, "", schema, probe)
    errors.extend({"code": "RECIPE_SCHEMA_INVALID", "path": path.lstrip("/"), "message": message} for path, message in probe)
    if verify and root is not None:
        errors.extend({"code": code, "path": path, "message": message} for path, message, code in verify_files(recipe, root))
    unique = {(item["code"], item["path"], item["message"]): item for item in errors}
    ordered = [unique[key] for key in sorted(unique)]
    return ordered


def _report(recipe, errors):
    return {
        "scope": "FND-04/recipe-contract",
        "status": "refused" if errors else "accepted",
        "recipeId": recipe.get("recipeId") if isinstance(recipe, dict) else None,
        "errors": errors,
        "codes": sorted({item["code"] for item in errors}),
        "claims": {
            "recipeContractChecked": True,
            "runtimeAcceptance": False,
            "sceneCollisionAcceptance": False,
            "facilityFidelityValidated": False,
            "collisionContractChanged": any(item["code"] == "RECIPE_CONTRACT_MUTATION" for item in errors),
        },
        "note": NOTE,
    }


# ------------------------------------------------------------------------ manifest mapping


MANIFEST_MAPPING = [
    ("units.system", "manifest.units", "metres"),
    ("units.coordinateAdapter", "manifest.coordinateAdapter", "Unity(x,y,z) to Blender(-x,-z,y)"),
    ("provenance.generator", "manifest.generator", "chooguard-synthetic-blender-kit"),
    ("replacement.targetAssetId", "manifest.assets[].id", "one of 33 native asset IDs"),
    ("digests.output", "manifest.assets[].sha256", "delivered bytes of the target asset"),
    ("digests.inputs[].path", "manifest.assets[].sourceModule", "authoring module of the target asset"),
    ("replacement.boundary.excluded", "manifest.assets[].collisionBoxes", "frozen: collision boxes stay owned by the manifest"),
]


def inspect_manifest(recipe, manifest, manifest_sha):
    """Read-only field mapping between the proposed recipe and the current manifest contract."""
    rows = []
    errors = []
    scope = recipe.get("scope") if isinstance(recipe.get("scope"), dict) else {}
    target = scope.get("target") if isinstance(scope.get("target"), dict) else {}
    units = recipe.get("units") if isinstance(recipe.get("units"), dict) else {}
    provenance = recipe.get("provenance") if isinstance(recipe.get("provenance"), dict) else {}
    digests = recipe.get("digests") if isinstance(recipe.get("digests"), dict) else {}
    boundary = (recipe.get("replacement") or {}).get("boundary") if isinstance(recipe.get("replacement"), dict) else {}
    boundary = boundary if isinstance(boundary, dict) else {}
    assets = manifest.get("assets") if isinstance(manifest.get("assets"), list) else []
    by_id = {asset.get("id"): asset for asset in assets if isinstance(asset, dict)}
    input_paths = sorted(entry.get("path") for entry in _entries(digests.get("inputs"))
                         if isinstance(entry, dict) and _text(entry.get("path")))
    asset = by_id.get(target.get("assetId"))
    rows.append({"recipeField": "units.system", "manifestField": "units",
                 "recipe": units.get("system"), "manifest": manifest.get("units")})
    rows.append({"recipeField": "units.coordinateAdapter", "manifestField": "coordinateAdapter",
                 "recipe": units.get("coordinateAdapter"), "manifest": manifest.get("coordinateAdapter")})
    rows.append({"recipeField": "provenance.generator", "manifestField": "generator",
                 "recipe": provenance.get("generator"), "manifest": manifest.get("generator")})
    rows.append({"recipeField": "replacement.targetAssetId", "manifestField": "assets[].id",
                 "recipe": target.get("assetId"), "manifest": "present" if asset else "absent"})
    rows.append({"recipeField": "replacement.boundary.excluded", "manifestField": "assets[].collisionBoxes",
                 "recipe": sorted(boundary.get("excluded") or []), "manifest": "frozen and never written by this recipe"})
    if asset is None:
        errors.append(("scope.target.assetId", "the target asset is absent from the manifest contract", "RECIPE_TARGET_MISSING"))
    else:
        rows.append({"recipeField": "digests.output", "manifestField": "assets[" + str(target.get("assetId")) + "].sha256",
                     "recipe": digests.get("output"), "manifest": asset.get("sha256")})
        rows.append({"recipeField": "digests.inputs[].path", "manifestField": "assets[].sourceModule",
                     "recipe": input_paths, "manifest": asset.get("sourceModule")})
        if asset.get("sourceModule") not in input_paths:
            errors.append(("digests.inputs", "the target asset's authoring module is not a digest-bound input", "RECIPE_SOURCE_MISSING"))
    if manifest_sha is not None and _sha(target.get("manifestSha256")) and target["manifestSha256"] != manifest_sha:
        errors.append(("scope.target.manifestSha256", "the bound manifest digest differs from the manifest on disk", "RECIPE_DIGEST_MISMATCH"))
    # generator/units are exact; the manifest's coordinateAdapter also records how the
    # adapter was verified, so the recipe's frozen transform must be contained in it.
    for name, manifest_field, manifest_value, recipe_value, code, mode in (
        ("units.system", "units", manifest.get("units"), units.get("system"), "RECIPE_UNITS_MISMATCH", "equals"),
        ("units.coordinateAdapter", "coordinateAdapter", manifest.get("coordinateAdapter"),
         units.get("coordinateAdapter"), "RECIPE_UNITS_MISMATCH", "contained"),
        ("provenance.generator", "generator", manifest.get("generator"), provenance.get("generator"),
         "RECIPE_SOURCE_MISMATCH", "equals"),
    ):
        if recipe_value is None:
            continue
        agreed = recipe_value == manifest_value if mode == "equals" else (
            isinstance(manifest_value, str) and isinstance(recipe_value, str) and recipe_value in manifest_value)
        if not agreed:
            errors.append((name, "recipe " + name + " is not the manifest's " + manifest_field, code))
    return {
        "mapping": MANIFEST_MAPPING,
        "rows": rows,
        "errors": [{"code": code, "path": path, "message": message} for path, message, code in errors],
        "note": "Read-only comparison. Passing this mapping is metadata agreement, not scene, collision or runtime acceptance.",
    }


# ---------------------------------------------------------------------------- case suites


def _pointer(pointer):
    if not _text(pointer):
        raise RecipeError("case edit pointer must be a non-empty dotted path")
    parts = []
    for segment in pointer.split("."):
        if not segment:
            raise RecipeError("case edit pointer has an empty segment")
        parts.append(int(segment) if segment.isdigit() else segment)
    return parts


def apply_edits(document, case):
    """Expand a compact negative case into a full recipe document.

    Removals are applied before assignments, so a case may drop a block and then set a
    remaining field without depending on JSON member order.
    """
    result = copy.deepcopy(document)
    for pointer in case.get("remove", []):
        parts = _pointer(pointer)
        parent = result
        for segment in parts[:-1]:
            parent = parent[segment]
        if isinstance(parent, list):
            parent.pop(parts[-1])
        else:
            del parent[parts[-1]]
    for pointer, value in (case.get("set") or {}).items():
        parts = _pointer(pointer)
        parent = result
        for segment in parts[:-1]:
            parent = parent[segment]
        parent[parts[-1]] = copy.deepcopy(value)
    return result


def expand_case(suite, case, seen=None):
    seen = seen or ()
    case_id = case.get("caseId")
    if not _text(case_id):
        raise RecipeError("every case needs a caseId")
    if case_id in seen:
        raise RecipeError("case inheritance cycle")
    if "recipe" in case:
        return case["recipe"]
    parent_id = case.get("basedOn")
    if not _text(parent_id):
        raise RecipeError("case " + case_id + " declares neither recipe nor basedOn")
    pool = _entries(suite.get("validCases")) + _entries(suite.get("negativeCases"))
    parent = next((item for item in pool if isinstance(item, dict) and item.get("caseId") == parent_id), None)
    if parent is None:
        raise RecipeError("case " + case_id + " references an unknown base case")
    base = expand_case(suite, parent, seen + (case_id,))
    return apply_edits(base, case)


def run_suite(suite, schema, root=None, verify=False):
    if not isinstance(suite, dict):
        return {"scope": "FND-04/recipe-case-suite", "status": "failed", "cases": 0, "unexpected": [],
                "errors": [{"code": "RECIPE_SUITE_INVALID", "path": "", "message": "suite must be a JSON object"}], "note": NOTE}
    results = []
    unexpected = []
    for kind, expect_accept in (("validCases", True), ("negativeCases", False)):
        for case in _entries(suite.get(kind)):
            if not isinstance(case, dict):
                unexpected.append({"caseId": None, "reason": "case must be a JSON object"})
                continue
            case_id = case.get("caseId")
            try:
                recipe = expand_case(suite, case)
                errors = validate_recipe(recipe, schema, root, verify)
            except (RecipeError, KeyError, IndexError, TypeError) as error:
                results.append({"caseId": case_id, "kind": kind, "status": "error", "codes": []})
                unexpected.append({"caseId": case_id, "reason": "case could not be expanded: " + str(error)})
                continue
            codes = sorted({item["code"] for item in errors})
            if expect_accept:
                ok = not errors
                detail = "expected acceptance, got " + ",".join(codes)
            else:
                wanted = case.get("expectedCode")
                ok = bool(errors) and wanted in codes
                detail = "expected refusal " + str(wanted) + ", got " + ",".join(codes)
            results.append({"caseId": case_id, "kind": kind, "status": "pass" if ok else "unexpected", "codes": codes})
            if not ok:
                unexpected.append({"caseId": case_id, "reason": detail})
    is_ok = not unexpected
    return {
        "scope": "FND-04/recipe-case-suite",
        "status": "ok" if is_ok else "failed",
        "fixtureId": suite.get("fixtureId"),
        "cases": len(results),
        "accepted": sum(1 for item in results if item["kind"] == "validCases" and item["status"] == "pass"),
        "refused": sum(1 for item in results if item["kind"] == "negativeCases" and item["status"] == "pass"),
        "results": results,
        "unexpected": unexpected,
        "errors": [{"code": "RECIPE_CASE_INVALID", "path": item.get("caseId") or "", "message": item["reason"]}
                   for item in unexpected if item.get("caseId")],
        "note": NOTE,
    }


# ------------------------------------------------------------------------------ self tests


def _test_recipe():
    """A complete, self-contained recipe so --test never depends on fixture files."""
    sha = "a" * 64
    return {
        "schemaVersion": 1,
        "recipeId": "FND-04-TEST-01",
        "classification": "PUBLIC_SYNTHETIC",
        "scope": {
            "source": {"ref": "foundation/art/object-references.json", "availability": "published", "sha256": sha},
            "recipe": {"path": SCHEMA_PATH, "sha256": sha},
            "target": {"assetId": "SituationPanel", "manifestPath": MANIFEST_PATH, "manifestSha256": sha},
        },
        "provenance": {
            "sourceRef": "foundation/art/object-references.json",
            "sourceKind": "synthetic_metadata",
            "generator": "chooguard-synthetic-blender-kit",
            "generatedAt": "2026-09-14",
            "license": {"id": "repository-authoring-policy", "reusePermitted": False, "attribution": "CHOOGuard synthetic kit"},
        },
        "version": {"assetVersion": 3, "revision": "r1", "supersedes": None, "supersededBy": None},
        "digests": {"algorithm": "sha256", "source": sha, "output": sha,
                    "inputs": [{"path": "scripts/art/station_equipment.py", "sha256": sha}]},
        "units": dict(EXPECTED_UNITS, scaleToMetres=1),
        "replacement": {
            "targetAssetId": "SituationPanel",
            "boundary": {"included": ["mesh", "components", "pivot", "boundsUnity"],
                         "excluded": ["collisionBoxes", "anchors", "coordinateAdapter"]},
            "inputs": [{"path": "scripts/art/station_equipment.py", "sha256": sha}],
            "outputs": [{"path": "Assets/CHOOguardArt/Blender/SituationPanel.fbx", "sha256": sha}],
            "runtimeAcceptance": False,
            "sceneCollisionAcceptance": False,
        },
        "contracts": {"readOnly": ["coordinateAdapter", "anchors", "collisionBoxes"], "changed": [], "writeAllowed": False},
        "execution": {"cvExecution": False, "externalServiceCalls": False, "generativeService": None},
        "notAccepted": ["This contract does not establish scene, collision, runtime or facility acceptance."],
    }


MUTATIONS = [
    ("missing source ref", {"remove": ["scope.source.ref"]}, "RECIPE_SOURCE_MISSING"),
    ("missing provenance source", {"remove": ["provenance.sourceRef"]}, "RECIPE_SOURCE_MISSING"),
    ("missing output digest", {"remove": ["digests.output"]}, "RECIPE_OUTPUT_DIGEST_MISSING"),
    ("missing units", {"remove": ["units"]}, "RECIPE_UNITS_MISSING"),
    ("missing license", {"remove": ["provenance.license"]}, "RECIPE_LICENSE_MISSING"),
    ("missing version", {"remove": ["version"]}, "RECIPE_VERSION_MISSING"),
    ("malformed output digest", {"set": {"digests.output": "sha256:abcd"}}, "RECIPE_DIGEST_MISMATCH"),
    ("source digest mismatch", {"set": {"digests.source": "b" * 64}}, "RECIPE_DIGEST_MISMATCH"),
    ("duplicate input digest", {"set": {"digests.inputs": [
        {"path": "scripts/art/station_equipment.py", "sha256": "a" * 64},
        {"path": "scripts/art/station_equipment.py", "sha256": "a" * 64}]}}, "RECIPE_DUPLICATE_INPUT"),
    ("unit system mismatch", {"set": {"units.system": "feet", "units.lengthUnit": "ft"}}, "RECIPE_UNITS_MISMATCH"),
    ("axis mismatch", {"set": {"units.upAxis": "-Y"}}, "RECIPE_UNITS_MISMATCH"),
    ("license reuse overclaim", {"set": {"provenance.license.id": "unknown_no_reuse_license_confirmed",
                                        "provenance.license.reusePermitted": True}}, "RECIPE_LICENSE_MISMATCH"),
    ("source reference mismatch", {"set": {"provenance.sourceRef": "foundation/art/other.json"}}, "RECIPE_SOURCE_MISMATCH"),
    ("target mismatch", {"set": {"replacement.targetAssetId": "Bench"}}, "RECIPE_TARGET_MISMATCH"),
    ("collision inside boundary", {"set": {"replacement.boundary.included": ["mesh", "collisionBoxes"]}}, "RECIPE_CONTRACT_MUTATION"),
    ("frozen contract dropped from exclusion", {"set": {"replacement.boundary.excluded": ["worldIds"]}}, "RECIPE_CONTRACT_MUTATION"),
    ("declared contract change", {"set": {"contracts.changed": ["collisionBoxes"]}}, "RECIPE_CONTRACT_MUTATION"),
    ("contract write allowed", {"set": {"contracts.writeAllowed": True}}, "RECIPE_CONTRACT_MUTATION"),
    ("read-only set incomplete", {"set": {"contracts.readOnly": ["coordinateAdapter"]}}, "RECIPE_CONTRACT_SET_INCOMPLETE"),
    ("unsupported schema version", {"set": {"schemaVersion": 2}}, "RECIPE_SCHEMA_UNSUPPORTED_VERSION"),
    ("cv execution enabled", {"set": {"execution.cvExecution": True}}, "RECIPE_CV_EXECUTION_DECLARED"),
    ("external service enabled", {"set": {"execution.externalServiceCalls": True}}, "RECIPE_EXTERNAL_SERVICE_DECLARED"),
    ("generative service named", {"set": {"execution.generativeService": "text-to-3d"}}, "RECIPE_EXTERNAL_SERVICE_DECLARED"),
    ("runtime acceptance overclaim", {"set": {"replacement.runtimeAcceptance": True}}, "RECIPE_ACCEPTANCE_OVERCLAIM"),
    ("scene acceptance overclaim", {"set": {"replacement.sceneCollisionAcceptance": True}}, "RECIPE_ACCEPTANCE_OVERCLAIM"),
    ("acceptance worded into a non-claim", {"set": {"notAccepted": ["Verified and accepted for runtime."]}}, "RECIPE_ACCEPTANCE_OVERCLAIM"),
    ("non-claims removed", {"set": {"notAccepted": []}}, "RECIPE_NON_CLAIMS_MISSING"),
    ("unknown top-level field", {"set": {"approval": "granted"}}, "RECIPE_SCHEMA_INVALID"),
    ("unsupported unit system value", {"set": {"units.system": "imperial"}}, "RECIPE_SCHEMA_INVALID"),
]


def _fixture_tests(schema, root, checker):
    import tempfile
    with tempfile.TemporaryDirectory() as directory:
        directory = Path(directory)
        target = directory / "sample.bin"
        target.write_bytes(b"fnd-04 synthetic replacement payload\n")
        digest = hashlib.sha256(target.read_bytes()).hexdigest()
        recipe = _test_recipe()
        recipe["scope"]["recipe"] = {"path": "sample.bin", "sha256": digest}
        recipe["scope"]["target"] = {"assetId": "SituationPanel", "manifestPath": "sample.bin", "manifestSha256": digest}
        recipe["digests"]["source"] = digest
        recipe["digests"]["inputs"] = [{"path": "sample.bin", "sha256": digest}]
        recipe["replacement"]["inputs"] = [{"path": "sample.bin", "sha256": digest}]
        recipe["replacement"]["outputs"] = [{"path": "sample.bin", "sha256": digest}]
        recipe["scope"]["source"]["ref"] = "sample.bin"
        recipe["scope"]["source"]["sha256"] = digest
        recipe["provenance"]["sourceRef"] = "sample.bin"
        checker.expect("verify-files accepts matching digests",
                       not validate_recipe(recipe, schema, directory, True),
                       json.dumps(validate_recipe(recipe, schema, directory, True), ensure_ascii=False))
        wrong = copy.deepcopy(recipe)
        wrong["replacement"]["outputs"][0]["sha256"] = "c" * 64
        checker.expect("verify-files rejects a changed digest",
                       "RECIPE_DIGEST_MISMATCH" in _codes(validate_recipe(wrong, schema, directory, True)))
        absent = copy.deepcopy(recipe)
        absent["digests"]["inputs"] = [{"path": "missing.bin", "sha256": digest}]
        checker.expect("verify-files rejects an absent file",
                       "RECIPE_FILE_MISSING" in _codes(validate_recipe(absent, schema, directory, True)))
        escaping = copy.deepcopy(recipe)
        escaping["digests"]["inputs"] = [{"path": "../escape.bin", "sha256": digest}]
        checker.expect("verify-files rejects a path escaping the root",
                       "RECIPE_PATH_INVALID" in _codes(validate_recipe(escaping, schema, directory, True)))

    checker.expect("duplicate JSON keys are refused", _raise(RecipeError, decode, b'{"a":1,"a":2}'))
    checker.expect("nonfinite JSON numbers are refused", _raise(RecipeError, decode, b'{"a":NaN}'))
    checker.expect("malformed JSON is refused", _raise(RecipeError, decode, b"{not json"))

    document = {"a": {"b": [1, 2, 3]}}
    edited = apply_edits(document, {"set": {"a.b.1": 9}, "remove": ["a.b.0"]})
    checker.expect("case edits remove before assigning", edited == {"a": {"b": [2, 9]}}, json.dumps(edited))
    dropped = apply_edits(document, {"remove": ["a.b"]})
    checker.expect("case edits remove a whole key", dropped == {"a": {}}, json.dumps(dropped))
    checker.expect("case edits never mutate the base document", document == {"a": {"b": [1, 2, 3]}})
    checker.expect("case edits refuse an unknown pointer", _raise(KeyError, apply_edits, document, {"remove": ["a.z"]}))

    probe = {"$defs": {"n": {"type": "integer"}}, "type": "object", "required": ["n"],
             "properties": {"n": {"$ref": "#/$defs/n"}}, "additionalProperties": False}
    checker.expect("schema evaluator accepts a matching document", _accepted({"n": 1}, probe, probe))
    checker.expect("schema evaluator refuses a boolean for an integer", not _accepted({"n": True}, probe, probe))
    checker.expect("schema evaluator refuses an undeclared property", not _accepted({"n": 1, "x": 2}, probe, probe))
    checker.expect("schema evaluator refuses a missing property", not _accepted({}, probe, probe))

    suite_path = next((candidate / CASES_PATH for candidate in (Path(root), ROOT)
                       if (candidate / CASES_PATH).is_file()), None)
    if suite_path is not None:
        report = run_suite(load(suite_path), schema, ROOT, False)
        checker.expect("fixture case suite behaves as declared", report["status"] == "ok",
                       json.dumps(report["unexpected"], ensure_ascii=False))
    else:
        checker.failures.append("fixture case suite is absent under " + str(root) + " and " + str(ROOT))


def _codes(errors):
    return sorted({item["code"] for item in errors})


def _raise(exception, function, *arguments):
    try:
        function(*arguments)
    except exception:
        return True
    return False


class Checker:
    def __init__(self):
        self.passed = 0
        self.failures = []

    def expect(self, name, condition, detail=""):
        if condition:
            self.passed += 1
        else:
            self.failures.append(name + (": " + detail if detail else ""))


def run_tests(schema, root):
    checker = Checker()
    checker.expect("schema declares its required fields and definitions",
                   isinstance(schema, dict) and "required" in schema and "properties" in schema and "$defs" in schema)
    base = _test_recipe()
    errors = validate_recipe(base, schema)
    checker.expect("a complete recipe is accepted", not errors, json.dumps(errors, ensure_ascii=False))
    for name, edits, expected in MUTATIONS:
        mutated = apply_edits(base, edits)
        codes = _codes(validate_recipe(mutated, schema))
        checker.expect(name + " is refused as " + expected, expected in codes, ",".join(codes))
    _fixture_tests(schema, root, checker)
    return {
        "scope": "FND-04/recipe-validator-self-test",
        "status": "ok" if not checker.failures else "failed",
        "tests": checker.passed + len(checker.failures),
        "passed": checker.passed,
        "failed": len(checker.failures),
        "failures": checker.failures,
        "note": NOTE,
    }


# ------------------------------------------------------------------------------ entry point


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("path", nargs="?", help="recipe JSON or a case-suite fixture such as cases.json")
    parser.add_argument("--check", metavar="PATH", help="validate exactly one recipe document")
    parser.add_argument("--test", action="store_true", help="run the validator's own unit tests")
    parser.add_argument("--schema", default=None, help="schema path override, defaulting to the validator's own tree")
    parser.add_argument("--root", default=None,
                        help="tree that the recipe's file bindings resolve against; a positional path "
                             "is looked up in the working directory first")
    parser.add_argument("--verify-files", action="store_true", help="hash the recipe's file bindings against --root")
    parser.add_argument("--inspect-manifest", action="store_true", help="read-only mapping against foundation/art/asset-manifest.json")
    parser.add_argument("--manifest", default=None, help="manifest path, relative to --root")
    arguments = parser.parse_args()

    # --root governs where the recipe and its file bindings live; the schema belongs to the
    # validator's own tree, so a checkout is never asked to supply an unpublished revision.
    root = Path(arguments.root).resolve() if arguments.root else ROOT
    schema_path = Path(arguments.schema).resolve() if arguments.schema else ROOT / SCHEMA_PATH
    target = arguments.check or arguments.path
    if arguments.check and arguments.path:
        parser.error("use either a positional path or --check, not both")
    if not arguments.test and not target:
        parser.error("a recipe path or --test is required")
    try:
        if arguments.test:
            try:
                schema = load(schema_path)
            except (RecipeError, OSError) as error:
                print(json.dumps({"scope": "FND-04/recipe-validator-self-test", "status": "failed",
                                  "failures": ["schema unavailable: " + str(error)]}, ensure_ascii=False, indent=2))
                return 1
            report = run_tests(schema, root)
        else:
            schema = load(schema_path)
            document = load(document_path(target, root))
            if isinstance(document, dict) and ("validCases" in document or "negativeCases" in document):
                report = run_suite(document, schema, root, arguments.verify_files)
            else:
                report = _report(document, validate_recipe(document, schema, root, arguments.verify_files))
                if arguments.inspect_manifest:
                    manifest_path = root / (arguments.manifest or MANIFEST_PATH)
                    manifest_bytes = manifest_path.read_bytes()
                    report["manifest"] = inspect_manifest(
                        document, decode(manifest_bytes), hashlib.sha256(manifest_bytes).hexdigest())
                    report["manifest"]["path"] = str(manifest_path)
    except (RecipeError, OSError, KeyError, IndexError, TypeError) as error:
        report = {"scope": "FND-04/recipe-contract", "status": "failed",
                  "errors": [{"code": "RECIPE_SCHEMA_INVALID", "path": "", "message": str(error)}],
                  "codes": ["RECIPE_SCHEMA_INVALID"], "note": NOTE}
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if report.get("status") in ("accepted", "ok") else 1


if __name__ == "__main__":
    sys.exit(main())
