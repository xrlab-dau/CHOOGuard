"""Evidence-first metric reconstruction. No image-derived point is a field survey.

Run with the existing .tools/video-reconstruction/bin/python (NumPy/pycolmap).
Validation is of supplied survey records, never a certificate of field acquisition.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time

import numpy as np

SCHEMA = "chooguard.metric-capture.v1"
VALIDATION_SCHEMA = "chooguard.metric-validation.v1"
TARGET_M = 0.02
FIELD_METHODS = {"total_station", "laser_distance", "tape_measure", "survey_level"}
ROOT = Path(__file__).resolve().parents[2]


class EvidenceError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise EvidenceError(message)


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def stamp(value):
    require(isinstance(value, str) and value.strip(), "Missing ISO8601 capture/survey time")
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise EvidenceError(f"Invalid ISO8601 time: {value}") from exc
    require(parsed.tzinfo is not None, "Capture times must contain a timezone")
    return parsed.astimezone(timezone.utc)


def finite(value, label, positive=False):
    require(isinstance(value, (int, float)) and not isinstance(value, bool), f"{label} must be numeric")
    require(math.isfinite(value) and (not positive or value > 0), f"Invalid {label}")
    return float(value)


def xyz(value, label):
    require(isinstance(value, list) and len(value) == 3, f"{label} needs xyz array")
    return np.array([finite(v, label) for v in value], dtype=float)


def unity_conversion(frame):
    conversion = frame.get("unity_from_site", {})
    require(isinstance(conversion.get("unity_frame_id"), str) and conversion["unity_frame_id"].strip(), "Missing Unity world frame ID")
    matrix = np.asarray(conversion.get("matrix", []), dtype=float)
    require(matrix.shape == (3, 3) and np.isfinite(matrix).all(), "Site to Unity conversion needs finite 3x3 matrix")
    require(np.allclose(matrix.T @ matrix, np.eye(3), atol=1e-9), "Site to Unity conversion must preserve metric lengths")
    require(abs(float(np.linalg.det(matrix)) + 1.0) < 1e-9, "Right-handed site to left-handed Unity conversion needs determinant -1")
    offset = xyz(conversion.get("translation_m"), "Unity frame translation_m")
    return matrix, offset


def unique_ids(records, label):
    require(isinstance(records, list), f"{label} must be an array")
    require(all(isinstance(record, dict) for record in records), f"{label} records must be objects")
    ids = [record.get("id") for record in records]
    require(all(isinstance(i, str) and i.strip() for i in ids), f"{label} has missing IDs")
    require(len(ids) == len(set(ids)), f"{label} has duplicate IDs (fit and holdout must be disjoint)")
    return {record["id"]: record for record in records}


def load_manifest(path):
    manifest = json.loads(Path(path).read_text(encoding="utf-8"))
    require(isinstance(manifest, dict), "Capture manifest must be an object")
    require(manifest.get("schema") == SCHEMA, f"Expected {SCHEMA}")
    return manifest


def audit_manifest(manifest, root):
    """Verify immutable source files and a single physical acquisition epoch."""
    require(manifest.get("template_unverified") is not True, "Capture kit is unverified; supply actual capture and survey records")
    epoch = manifest.get("capture_epoch", {})
    require(isinstance(epoch, dict) and isinstance(epoch.get("id"), str) and epoch["id"].strip(), "Missing capture epoch ID")
    start, end = stamp(epoch.get("started_at")), stamp(epoch.get("ended_at"))
    require(start <= end, "Capture epoch start is after end")
    frame = manifest.get("coordinate_frame", {})
    require(isinstance(frame.get("id"), str) and frame["id"].strip(), "Missing coordinate frame ID")
    require(frame.get("units") == "metres", "Site frame must use metres")
    require(frame.get("handedness") == "right", "Registration requires a declared right-handed site frame")
    require(set(frame.get("axes", {})) == {"x", "y", "z"} and all(frame["axes"].values()), "Declare all site coordinate axes")
    unity_conversion(frame)
    sources = unique_ids(manifest.get("sources", []), "sources")
    require(bool(sources), "No local source files supplied")
    receipts = []
    for source in sources.values():
        require(source.get("role") in {"field_capture", "field_survey", "reference_only"}, f"Invalid source role: {source['id']}")
        require(isinstance(source.get("path"), str) and source["path"].strip(), f"Missing evidence path: {source['id']}")
        path = Path(source["path"])
        if not path.is_absolute():
            path = Path(root) / path
        require(path.is_file(), f"Missing evidence file: {source['path']}")
        require(re.fullmatch(r"[a-f0-9]{64}", source.get("sha256", "")) is not None, f"Missing SHA256: {source['id']}")
        actual = digest(path)
        require(actual == source["sha256"], f"SHA256 mismatch: {source['id']}")
        if source["role"] != "reference_only":
            require(source.get("epoch_id") == epoch["id"], f"Inconsistent source epoch: {source['id']}")
            observed = stamp(source.get("captured_at"))
            require(start <= observed <= end, f"Source outside capture epoch: {source['id']}")
        if source["role"] == "field_survey":
            require(source.get("survey_origin") == "independent_field_measurement", f"Survey must be independent physical measurement: {source['id']}")
            require(not source.get("derived_from"), f"Model/image-derived survey is not independent: {source['id']}")
        receipts.append({"id": source["id"], "path": str(path), "role": source["role"], "sha256": actual,
                         "bytes": path.stat().st_size, "captured_at": source.get("captured_at"),
                         "capture_epoch": source.get("epoch_id")})
    return {"status": "audited", "capture_epoch": epoch["id"], "coordinate_frame_id": frame["id"],
            "sources": receipts, "field_capture_count": sum(s["role"] == "field_capture" for s in sources.values()),
            "field_survey_count": sum(s["role"] == "field_survey" for s in sources.values()),
            "reference_only_count": sum(s["role"] == "reference_only" for s in sources.values()),
            "provenance_limit": "Hashes verify files and declarations; a human must review physical-survey authenticity."}


def survey_check(record, sources, epoch):
    require(record.get("measurement_method") in FIELD_METHODS, f"Non-field measurement method: {record['id']}")
    require(not record.get("derived_from"), f"Derived measurement cannot be independent: {record['id']}")
    uncertainty = finite(record.get("uncertainty_m"), f"uncertainty_m/{record['id']}", positive=True)
    # This is conservative bounded uncertainty, not an unstated standard deviation.
    require(record.get("uncertainty_basis") == "upper_bound", f"Specify bounded measurement uncertainty: {record['id']}")
    observed = stamp(record.get("observed_at"))
    require(stamp(epoch["started_at"]) <= observed <= stamp(epoch["ended_at"]), f"Survey outside capture epoch: {record['id']}")
    ids = record.get("evidence_source_ids", [])
    require(isinstance(ids, list) and ids and len(ids) == len(set(ids)), f"Missing/duplicate survey evidence: {record['id']}")
    for source_id in ids:
        require(source_id in sources and sources[source_id]["role"] == "field_survey", f"Survey evidence not a field survey: {record['id']}/{source_id}")
    return uncertainty


def fit_similarity(reconstruction, site):
    """Proper rotation Umeyama alignment; reject degenerate/reflective controls."""
    x, y = np.asarray(reconstruction, float), np.asarray(site, float)
    require(x.shape == y.shape and len(x) >= 3 and x.shape[1] == 3, "Need at least three paired fit points")
    xc, yc = x - x.mean(axis=0), y - y.mean(axis=0)
    require(np.linalg.matrix_rank(xc, tol=1e-10) >= 2 and np.linalg.matrix_rank(yc, tol=1e-10) >= 2,
            "Coincident or collinear controls cannot define a 3D frame")
    variance = np.mean(np.sum(xc * xc, axis=1))
    require(variance > 1e-16, "Zero/degenerate reconstruction scale")
    u, singular, vt = np.linalg.svd(yc.T @ xc / len(x))
    sign = np.ones(3)
    if np.linalg.det(u @ vt) < 0:
        sign[-1] = -1
    rotation = u @ np.diag(sign) @ vt
    scale = float(np.sum(singular * sign) / variance)
    require(math.isfinite(scale) and scale > 1e-12, "Zero/negative/degenerate fitted scale")
    translation = y.mean(axis=0) - scale * rotation @ x.mean(axis=0)
    return scale, rotation, translation


def validate_metric(manifest, root, manifest_sha256, manifest_path=None):
    audit = audit_manifest(manifest, root)
    require(audit["field_capture_count"] > 0, "Reference material alone is not a current field capture")
    sources = unique_ids(manifest["sources"], "sources")
    controls = unique_ids(manifest.get("controls", []), "controls")
    fit, held = [], []
    points = set()
    for control in controls.values():
        require(control.get("role") in {"fit", "holdout"}, f"Invalid control role: {control['id']}")
        survey_check(control, sources, manifest["capture_epoch"])
        x = xyz(control.get("reconstruction_xyz"), "reconstruction_xyz")
        y = xyz(control.get("site_xyz_m"), "site_xyz_m")
        # Changing an ID cannot make the same physical point independent.
        signature = tuple(np.round(y, 9))
        require(signature not in points, "Fit/holdout reuses the same surveyed physical point")
        points.add(signature)
        (fit if control["role"] == "fit" else held).append((control, x, y))
    require(len(fit) >= 3, "Need three non-collinear physical fit controls")
    require(len(held) >= 3, "Need at least three independent holdouts")
    require(np.linalg.matrix_rank(np.array([v[2] for v in held]) - np.mean([v[2] for v in held], axis=0), tol=1e-10) >= 2,
            "Independent holdouts must cover more than a line")
    scale, rotation, translation = fit_similarity([v[1] for v in fit], [v[2] for v in fit])
    checks = []
    for control, x, y in held:
        predicted = scale * rotation @ x + translation
        residual = float(np.linalg.norm(predicted - y))
        uncertainty = float(control["uncertainty_m"])
        checks.append({"id": control["id"], "object_id": control.get("object_id"), "role": "holdout",
                       "reconstruction_xyz": x.tolist(), "site_xyz_m": y.tolist(), "predicted_site_xyz_m": predicted.tolist(),
                       "error_m": residual, "uncertainty_m": uncertainty, "error_plus_uncertainty_m": residual + uncertainty,
                       "evidence_source_ids": control["evidence_source_ids"], "observed_at": control["observed_at"]})
    dimensions = []
    for record in unique_ids(manifest.get("dimension_checks", []), "dimension_checks").values():
        require(record["id"] not in controls, "Position and dimension check IDs must be globally disjoint")
        uncertainty = survey_check(record, sources, manifest["capture_epoch"])
        require(isinstance(record.get("object_id"), str) and record["object_id"], "Dimension check needs object_id")
        require(record.get("axis") in {"x", "y", "z"}, "Dimension check must declare Unity x/y/z axis")
        predicted = scale * finite(record.get("reconstructed_length"), "reconstructed_length", positive=True)
        measured = finite(record.get("measured_length_m"), "measured_length_m", positive=True)
        error = abs(predicted - measured)
        dimensions.append({"id": record["id"], "object_id": record["object_id"], "axis": record["axis"], "predicted_length_m": predicted,
                           "measured_length_m": measured, "error_m": error, "uncertainty_m": uncertainty,
                           "error_plus_uncertainty_m": error + uncertainty,
                           "evidence_source_ids": record["evidence_source_ids"], "observed_at": record["observed_at"]})
    passed = all(c["error_plus_uncertainty_m"] <= TARGET_M for c in checks)
    identity = hashlib.sha256((manifest_sha256 + json.dumps(checks, sort_keys=True)).encode()).hexdigest()
    return {"schema": VALIDATION_SCHEMA, "status": "validated" if passed else "failed", "validation_id": f"metric-{identity[:24]}",
            "capture_epoch": manifest["capture_epoch"]["id"], "coordinate_frame_id": manifest["coordinate_frame"]["id"],
            "coordinate_frame": manifest["coordinate_frame"], "manifest_sha256": manifest_sha256, "manifest_path": manifest_path,
            "capture_epoch_window": manifest["capture_epoch"],
            "target_max_error_m": TARGET_M, "measurement_basis": "independent_field_survey",
            "registration": {"scale": scale, "rotation": rotation.tolist(), "translation_m": translation.tolist(),
                             "fit_ids": [c[0]["id"] for c in fit]},
            "holdout": checks, "dimension_checks": dimensions, "objects": manifest.get("objects", []),
            "holdout_max_error_m": max(c["error_m"] for c in checks),
            "holdout_max_error_plus_uncertainty_m": max(c["error_plus_uncertainty_m"] for c in checks),
            "all_holdouts_within_target": passed, "evidence_audit": audit,
            "limitations": ["Global alignment does not verify every object or unobserved geometry.",
                            "Object placement requires its own independent anchor and dimension checks.",
                            "Physical survey provenance and stated bounded uncertainty require human review."]}


def vector(value):
    return dict(zip(("x", "y", "z"), map(float, value)))


def export_placement(validation, object_id, root=ROOT):
    require(validation["status"] == "validated", "Failed alignment cannot produce a verified placement")
    checks = [c for c in validation["holdout"] if c.get("object_id") == object_id]
    dimensions = [c for c in validation["dimension_checks"] if c["object_id"] == object_id]
    require(bool(checks), "Exact object anchor has no independent holdout; group fit is insufficient")
    require(len(checks) == 1, "Declare one placement anchor per object (other landmarks use separate object IDs)")
    require({c["axis"] for c in dimensions} == {"x", "y", "z"} and len(dimensions) == 3,
            "Exact object needs one independently measured dimension on each Unity x/y/z axis")
    require(all(c["error_plus_uncertainty_m"] <= TARGET_M for c in checks + dimensions), "Object position/dimension uncertainty exceeds 2cm")
    objects = unique_ids(validation["objects"], "objects")
    require(object_id in objects, "Object needs an explicit independently surveyed orientation")
    obj = objects[object_id]
    rotation = xyz(obj.get("rotation_unity_euler_deg"), "rotation_unity_euler_deg")
    require(np.all(np.abs(rotation) <= 360), "Explicit Euler rotation outside [-360,360]")
    sources = {s["id"]: s for s in validation["evidence_audit"]["sources"]}
    orientation = {"id": object_id + ":orientation", "measurement_method": obj.get("orientation_measurement_method"),
                   "evidence_source_ids": obj.get("orientation_evidence_source_ids"), "observed_at": obj.get("orientation_observed_at"),
                   "uncertainty_m": obj.get("orientation_measurement_uncertainty_m"), "uncertainty_basis": "upper_bound"}
    survey_check(orientation, sources, validation["capture_epoch_window"])
    require(sources[orientation["evidence_source_ids"][0]]["capture_epoch"] == validation["capture_epoch"], "Orientation source epoch mismatch")
    capture_ids = obj.get("capture_source_ids", [])
    require(isinstance(capture_ids, list) and capture_ids and all(sid in sources and sources[sid]["role"] == "field_capture" for sid in capture_ids),
            "Object needs actual current capture evidence")
    source_ids = sorted({sid for c in checks + dimensions + [orientation] for sid in c["evidence_source_ids"]} | set(capture_ids))
    exported_sources = []
    for sid in source_ids:
        record = dict(sources[sid])
        try:
            record["path"] = str(Path(record["path"]).resolve().relative_to(root.resolve()))
        except ValueError as exc:
            raise EvidenceError("Unity evidence must be stored inside the project root") from exc
        exported_sources.append(record)
    require(validation.get("manifest_path"), "Verified placement needs retained project-relative manifest_path")
    matrix, offset = unity_conversion(validation["coordinate_frame"])

    def unity_vector(value):
        return vector(matrix @ np.asarray(value) + offset)

    return {"schema": "chooguard.verified-placement.v1", "object_id": object_id,
            "capture_epoch": validation["capture_epoch"], "coordinate_frame_id": validation["coordinate_frame_id"],
            "unity_coordinate_frame_id": validation["coordinate_frame"]["unity_from_site"]["unity_frame_id"],
            "placement_basis": "Unity left-handed x-right y-up z-forward metres",
            "coordinate_frame": validation["coordinate_frame"],
            "placement": {"position": unity_vector(checks[0]["predicted_site_xyz_m"]), "rotation": vector(rotation)},
            "verification": {"validation_id": validation["validation_id"], "measurement_basis": validation["measurement_basis"],
                             "status": "validated", "target_max_error_m": TARGET_M, "manifest_sha256": validation["manifest_sha256"],
                             "object_id": object_id, "capture_epoch": validation["capture_epoch"],
                             "coordinate_frame_id": validation["coordinate_frame"]["unity_from_site"]["unity_frame_id"],
                             "handedness": "unity_left_handed_y_up", "validated_rotation": vector(rotation),
                             "manifest_path": validation["manifest_path"], "sources": exported_sources,
                             "source_ids": source_ids, "fit_control_ids": validation["registration"]["fit_ids"],
                             "checks": [{"id": c["id"], "object_id": object_id, "role": "holdout", "predicted_position": unity_vector(c["predicted_site_xyz_m"]),
                                         "measured_position": unity_vector(c["site_xyz_m"]), "uncertainty_m": c["uncertainty_m"],
                                         "evidence_source_ids": c["evidence_source_ids"], "observed_at": c["observed_at"]} for c in checks],
                             "dimension_checks": dimensions, "max_dimension_error_m": max(c["error_m"] for c in dimensions),
                             "max_dimension_error_plus_uncertainty_m": max(c["error_plus_uncertainty_m"] for c in dimensions)},
            "limitations": ["This record verifies supplied anchor/dimensions; orientation and surface fidelity need separate inspection."]}


def record_source(path, role, source_id, captured_at=None, epoch_id=None):
    require(path.is_file(), f"Missing source file: {path}")
    record = {"id": source_id, "path": str(path.resolve()), "sha256": digest(path), "role": role}
    if role != "reference_only":
        stamp(captured_at)
        require(bool(epoch_id), "Field source needs epoch ID")
        record.update(captured_at=captured_at, epoch_id=epoch_id)
    if role == "field_survey":
        record["survey_origin"] = "independent_field_measurement"
        record["provenance_review_required"] = True
    return record


def audit_refs(manifest_path, root):
    """Read legacy evidence strings without turning old claims into survey records."""
    visited, files, missing, urls = set(), {}, set(), set()

    def inspect(value):
        if isinstance(value, dict):
            for child in value.values():
                inspect(child)
        elif isinstance(value, list):
            for child in value:
                inspect(child)
        elif isinstance(value, str):
            urls.update(re.findall(r"https?://[^\s)\]\"<>]+", value))
            # Only known project roots; do not reinterpret free-text descriptions as paths.
            for token in re.findall(r"(?:\.planning|asset-library|content|Assets)/[^\s\";,()\[\]]+", value):
                token = token.rstrip(".")
                if token in visited:
                    continue
                visited.add(token)
                path = root / token
                if path.is_file():
                    files[token] = {"path": token, "sha256": digest(path), "bytes": path.stat().st_size, "role": "reference_only"}
                    if path.suffix.lower() == ".json":
                        try:
                            inspect(json.loads(path.read_text(encoding="utf-8")))
                        except (ValueError, UnicodeDecodeError):
                            pass
                elif not path.is_dir():
                    missing.add(token)

    inspect(json.loads(manifest_path.read_text(encoding="utf-8")))
    return {"schema": "chooguard.legacy-reference-audit.v1", "role": "reference_only", "manifest": str(manifest_path),
            "manifest_sha256": digest(manifest_path), "existing_files": list(files.values()), "missing_files": sorted(missing),
            "public_urls": sorted(urls), "survey_verified": False,
            "limitations": ["Legacy images, model screenshots and inferred coordinates cannot establish centimetre field accuracy."]}


def reconstruction_plan(manifest, root, output, fps=2.0):
    audit = audit_manifest(manifest, root)
    require(audit["field_capture_count"] > 0, "No current field capture input; old references remain reference_only")
    require(math.isfinite(fps) and 0 < fps <= 30, "Sampling fps must be in (0,30]")
    python = root / ".tools/video-reconstruction/bin/python"
    mvs = root / ".tools/openmvs-v2.4.0"
    require(python.is_file(), "Existing pycolmap Python is missing")
    ffmpeg = shutil.which("ffmpeg")
    require(ffmpeg is not None, "ffmpeg is missing")
    for binary in ("InterfaceCOLMAP", "DensifyPointCloud", "ReconstructMesh", "RefineMesh", "TextureMesh"):
        require((mvs / binary).is_file(), f"Existing OpenMVS binary missing: {binary}")
    commands = []
    images = output / "images"
    photos = []
    videos = []
    for receipt in audit["sources"]:
        if receipt["role"] != "field_capture":
            continue
        path = Path(receipt["path"])
        suffix = path.suffix.lower()
        if suffix in {".jpg", ".jpeg", ".png", ".tif", ".tiff"}:
            photos.append({"source": str(path), "destination": str(images / f"source-{len(photos):05d}{suffix}")})
        elif suffix in {".mp4", ".mov", ".m4v", ".mkv"}:
            prefix = f"video-{len(videos):04d}"
            videos.append(str(path))
            commands.append([ffmpeg, "-v", "error", "-nostdin", "-i", str(path), "-vf", f"fps={fps}", "-q:v", "2", str(images / f"{prefix}-%06d.jpg")])
        else:
            raise EvidenceError(f"Unsupported field capture media: {path}")
    require(bool(videos) or len(photos) >= 3, "Need a video or at least three overlapping source photos")
    commands.append([str(python), str(Path(__file__).resolve()), "sfm", "--images", str(images), "--output", str(output)])
    commands.extend([
        [str(mvs / "InterfaceCOLMAP"), "-i", str(output / "colmap"), "-o", str(output / "scene.mvs"), "--image-folder", "images", "--max-threads", "2"],
        [str(mvs / "DensifyPointCloud"), str(output / "scene.mvs"), "--max-threads", "2", "--resolution-level", "1", "--number-views-fuse", "3", "-o", str(output / "scene_dense.mvs")],
        [str(mvs / "ReconstructMesh"), str(output / "scene_dense.mvs"), "--pointcloud-file", str(output / "scene_dense.ply"), "--max-threads", "2", "--close-holes", "0", "-o", str(output / "scene_mesh.ply")],
        [str(mvs / "RefineMesh"), str(output / "scene_dense.mvs"), "--mesh-file", str(output / "scene_mesh.ply"), "--max-threads", "2", "--close-holes", "0", "-o", str(output / "scene_refined.ply")],
        [str(mvs / "TextureMesh"), str(output / "scene_dense.mvs"), "--mesh-file", str(output / "scene_refined.ply"), "--max-threads", "2", "--close-holes", "0", "--export-type", "obj", "-o", str(output / "scene_textured.obj")],
    ])
    return {"schema": "chooguard.reconstruction-plan.v1", "status": "ready", "capture_epoch": audit["capture_epoch"],
            "output": str(output), "copy_photos": photos, "commands": commands, "source_audit": audit,
            "metric_verified": False, "registration_required": True,
            "limitations": ["SfM/MVS output is in relative coordinates; must independently validate before placement.",
                            "Human/reflective/blur masks and disconnected models need review; input completeness is not guaranteed."]}


def execute_reconstruction(plan):
    output = Path(plan["output"])
    require(not output.exists(), "Output already exists; preserve previous reconstructions and choose a fresh directory")
    require(shutil.disk_usage(output.parent if output.parent.exists() else ROOT).free > 2 * 1024**3, "Need at least 2GiB free disk")
    for source in plan["source_audit"]["sources"]:
        require(digest(source["path"]) == source["sha256"], f"Source changed since audit: {source['id']}")
    output.mkdir(parents=True)
    (output / "images").mkdir()
    for photo in plan["copy_photos"]:
        shutil.copyfile(photo["source"], photo["destination"])
    receipts = []
    try:
        for index, argv in enumerate(plan["commands"]):
            start = time.monotonic()
            log = output / f"stage-{index:02d}.log"
            with log.open("w", encoding="utf-8") as stream:
                completed = subprocess.run(argv, cwd=output, stdout=stream, stderr=subprocess.STDOUT, check=False)
            receipts.append({"argv": argv, "exit_code": completed.returncode, "elapsed_seconds": time.monotonic() - start, "log": str(log)})
            require(completed.returncode == 0, f"Reconstruction stage {index} failed; preserved log {log}")
    finally:
        (output / "execution-receipt.json").write_text(json.dumps({"metric_verified": False, "stages": receipts}, indent=2) + "\n")
    return {"status": "reconstructed_relative", "metric_verified": False, "stages": receipts}


def sfm(images, output):
    import pycolmap
    names = sorted(p.name for p in images.iterdir() if p.is_file())
    require(len(names) >= 3, "Not enough extracted images")
    require(len(names) <= 1200, "Split capture into overlapping regions of <=1200 images before exhaustive matching")
    database, sparse = output / "features.db", output / "sparse"
    sparse.mkdir(exist_ok=True)
    reader = pycolmap.ImageReaderOptions(camera_model="SIMPLE_RADIAL")
    features = pycolmap.FeatureExtractionOptions(num_threads=2, use_gpu=False)
    features.sift.max_num_features = 8192
    pycolmap.extract_features(database, images, camera_mode=pycolmap.CameraMode.AUTO,
                             reader_options=reader, extraction_options=features, device=pycolmap.Device.cpu)
    matching = pycolmap.FeatureMatchingOptions(num_threads=2, use_gpu=False)
    # Cross-pass overlap must be matched. Exhaustive is bounded to prevent accidental quadratic jobs.
    pycolmap.match_exhaustive(database, matching_options=matching, device=pycolmap.Device.cpu)
    options = pycolmap.IncrementalPipelineOptions(num_threads=2, random_seed=0)
    models = pycolmap.incremental_mapping(database, images, sparse, options=options)
    require(bool(models), "No reconstructable cameras; cannot fabricate missing surfaces")
    model_ids = sorted(models, key=lambda mid: models[mid].num_reg_images(), reverse=True)
    summary = {"pycolmap": pycolmap.__version__, "model_ids": model_ids, "disconnected": len(models) != 1,
               "metric_verified": False, "registered_images": {str(mid): models[mid].num_reg_images() for mid in models}}
    (output / "sfm-receipt.json").write_text(json.dumps(summary, indent=2) + "\n")
    require(len(models) == 1, "Disconnected models have independent scales; review capture connectivity before meshing")
    model_dir = sparse / str(model_ids[0])
    models[model_ids[0]].write(model_dir)
    require(models[model_ids[0]].num_reg_images() / len(names) >= 0.9, "Fewer than 90% cameras registered; inspect blur/overlap before meshing")
    pycolmap.undistort_images(output / "colmap", model_dir, images, num_threads=2,
                             undistort_options=pycolmap.UndistortCameraOptions(max_image_size=2400))
    return summary


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    for name in ("audit", "validate", "export-placement", "reconstruct"):
        p = sub.add_parser(name)
        p.add_argument("--manifest", type=Path, required=True)
        p.add_argument("--project-root", type=Path, default=ROOT)
        p.add_argument("--output", type=Path)
        if name == "export-placement":
            p.add_argument("--object-id", required=True)
        if name == "reconstruct":
            p.add_argument("--execute", action="store_true", help="Run commands; default only prints a dry-run plan")
            p.add_argument("--fps", type=float, default=2.0)
    p = sub.add_parser("record-source")
    p.add_argument("path", type=Path)
    p.add_argument("--id", required=True)
    p.add_argument("--role", choices=("reference_only", "field_capture", "field_survey"), required=True)
    p.add_argument("--captured-at")
    p.add_argument("--epoch-id")
    p = sub.add_parser("audit-refs")
    p.add_argument("--manifest", type=Path, default=ROOT / "content/world/observed-interiors/manifest.json")
    p.add_argument("--project-root", type=Path, default=ROOT)
    p.add_argument("--output", type=Path)
    p = sub.add_parser("sfm")
    p.add_argument("--images", type=Path, required=True)
    p.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        if args.command == "record-source":
            result = record_source(args.path, args.role, args.id, args.captured_at, args.epoch_id)
        elif args.command == "audit-refs":
            result = audit_refs(args.manifest, args.project_root)
        elif args.command == "sfm":
            result = sfm(args.images, args.output)
        else:
            manifest = load_manifest(args.manifest)
            if args.command == "audit":
                result = audit_manifest(manifest, args.project_root)
            elif args.command == "reconstruct":
                require(args.output is not None, "Reconstruction needs fresh --output directory")
                result = reconstruction_plan(manifest, args.project_root.resolve(), args.output.resolve(), args.fps)
                if args.execute:
                    result = execute_reconstruction(result)
            else:
                try:
                    manifest_relative = str(args.manifest.resolve().relative_to(args.project_root.resolve()))
                except ValueError as exc:
                    raise EvidenceError("Manifest must be retained inside the project root") from exc
                result = validate_metric(manifest, args.project_root, digest(args.manifest), manifest_relative)
                if args.command == "export-placement":
                    result = export_placement(result, args.object_id, args.project_root)
        rendered = json.dumps(result, indent=2, ensure_ascii=False, allow_nan=False) + "\n"
        if getattr(args, "output", None) and args.command not in {"reconstruct", "sfm"}:
            require(not args.output.exists(), "Output already exists; retain earlier evidence")
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_text(rendered, encoding="utf-8")
            print(json.dumps({"status": result.get("status", "audited"), "output": str(args.output),
                              "schema": result.get("schema"), "bytes": len(rendered.encode("utf-8"))}))
        else:
            print(rendered, end="")
        return 1 if result.get("status") == "failed" else 0
    except (EvidenceError, ValueError, OSError, KeyError, TypeError) as exc:
        print(json.dumps({"schema": VALIDATION_SCHEMA, "status": "blocked", "reason": str(exc), "metric_verified": False}, ensure_ascii=False))
        return 2


if __name__ == "__main__":
    sys.exit(main())
