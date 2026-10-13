"""Prepare a retained surface mesh for Blender/FBX without changing Unity Assets."""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import subprocess
import sys

import numpy as np
import trimesh

from surface_cloud import load_surface, sha256_file


AXES = ("X", "Y", "Z", "NEGATIVE_X", "NEGATIVE_Y", "NEGATIVE_Z")


def local_mesh(source: Path, sampling: dict) -> tuple[bytes, dict]:
    """Recenter in float64 BEFORE Blender's float32 geometry can lose precision."""
    if sampling["schema_version"] != 1 or sampling["status"] != "surface_sampling_complete":
        raise ValueError("Expected a completed surface sampling receipt")
    if sha256_file(source) != sampling["source"]["sha256"]:
        raise ValueError("Retained mesh does not match the sampling source hash")
    coords = sampling["coordinates"]
    scale = coords["meters_per_source_unit"]
    origin = np.asarray(coords["source_origin"], dtype=np.float64)
    if coords["unit"] != "metre" or not math.isfinite(scale) or scale <= 0:
        raise ValueError("Sampling coordinates must provide a positive scale to metres")
    if origin.shape != (3,) or not np.isfinite(origin).all():
        raise ValueError("Invalid sampling origin")
    matrix = np.eye(4)
    matrix[:3, :3] *= scale
    matrix[:3, 3] = -origin * scale
    if not np.array_equal(matrix, np.asarray(coords["source_to_local_m"])):
        raise ValueError("Sampling coordinate matrix disagrees with origin/scale")
    mesh = load_surface(source)
    with np.errstate(over="ignore", invalid="ignore"):
        local = (mesh.vertices - origin) * scale
        encoded = local.astype(np.float32)
        error = float(np.max(np.abs(local - encoded.astype(np.float64))))
    if not np.isfinite(encoded).all() or error > .001:
        raise ValueError("Local mesh exceeds 1 mm float32 storage error; use a nearer origin")
    mesh.vertices = encoded
    if not np.isfinite(mesh.area_faces).all() or np.any(mesh.area_faces <= 0):
        raise ValueError("Retained mesh has degenerate faces; repair explicitly before resampling")
    if not np.allclose(np.linalg.norm(mesh.face_normals, axis=1), 1):
        raise ValueError("Retained mesh has invalid face normals")
    return trimesh.exchange.ply.export_ply(mesh, encoding="binary_little_endian"), {
        "vertices": len(mesh.vertices), "triangles": len(mesh.faces), "bounds_m": mesh.bounds.tolist(),
        "max_float32_error_m": error, "color_kind": mesh.visual.kind,
    }


def prepare(source: Path, sampling_receipt: Path, output_dir: Path, blender: Path, *,
            source_forward: str, source_up: str, frame_reference: str,
            decimate_ratio: float = 1, timeout_seconds: float = 1800) -> dict:
    source = source.resolve(strict=True)
    sampling_receipt = sampling_receipt.resolve(strict=True)
    blender = blender.resolve(strict=True)
    output_dir = output_dir.resolve()
    if output_dir.exists():
        raise ValueError("Output directory already exists; choose a new handoff directory")
    if source_forward not in AXES or source_up not in AXES or source_forward[-1] == source_up[-1]:
        raise ValueError("Forward/up must be different axes of a right-handed source frame")
    if not frame_reference.strip():
        raise ValueError("frame-reference must explain how the right-handed source axes were verified")
    if not math.isfinite(decimate_ratio) or not 0 < decimate_ratio <= 1:
        raise ValueError("decimate-ratio must be in (0, 1]")
    if not math.isfinite(timeout_seconds) or timeout_seconds <= 0:
        raise ValueError("timeout-seconds must be finite and positive")
    parent_bytes = sampling_receipt.read_bytes()
    parent_hash = hashlib.sha256(parent_bytes).hexdigest()
    parent = json.loads(parent_bytes)
    payload, geometry = local_mesh(source, parent)
    source_hash = parent["source"]["sha256"]
    blender_hash = sha256_file(blender)
    worker = Path(__file__).with_name("blender_mesh.py").resolve(strict=True)
    worker_hash = sha256_file(worker)
    output_dir.mkdir(parents=True, exist_ok=False)
    (output_dir / "source_local_m.ply").write_bytes(payload)
    (output_dir / "sampling_receipt.json").write_bytes(parent_bytes)
    request = {
        "source_forward": source_forward, "source_up": source_up, "frame_reference": frame_reference,
        "source_handedness": "right-handed; declared by caller, not inferred",
        "decimate_ratio": decimate_ratio, "input_geometry": geometry,
        "source": parent["source"], "sampling_receipt_sha256": parent_hash,
    }
    (output_dir / "request.json").write_text(json.dumps(request, indent=2) + "\n", encoding="utf-8")
    command = [str(blender), "--background", "--factory-startup", "--disable-autoexec",
               "--python-exit-code", "2", "--python", str(worker), "--", str(output_dir)]
    with (output_dir / "blender.log").open("xb") as log:
        result = subprocess.run(command, cwd=output_dir, stdout=log, stderr=subprocess.STDOUT,
                                timeout=timeout_seconds, check=False,
                                creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
    if result.returncode:
        raise ValueError(f"Blender exited {result.returncode}; inspect blender.log, no success receipt")
    report = json.loads((output_dir / "blender_report.json").read_text(encoding="utf-8"))
    if report["status"] != "fbx_roundtrip_checked":
        raise ValueError("Blender did not complete its round-trip checks")
    if sha256_file(source) != source_hash or sha256_file(sampling_receipt) != parent_hash:
        raise ValueError("Source mesh/sampling receipt changed during processing")
    if sha256_file(blender) != blender_hash or sha256_file(worker) != worker_hash:
        raise ValueError("Blender executable or worker changed during processing")
    artifacts = []
    for name in ("source_local_m.ply", "prepared.ply", "prepared.fbx", "review.blend", "blender_report.json"):
        path = output_dir / name
        if not path.is_file() or path.stat().st_size == 0:
            raise ValueError(f"Missing or empty Blender artifact: {name}")
        artifacts.append({"file": name, "bytes": path.stat().st_size, "sha256": sha256_file(path)})
    receipt = {
        "schema_version": 1, "status": "blender_handoff_complete_review_required",
        "source": parent["source"], "sampling_coordinates": parent["coordinates"],
        "sampling_receipt_sha256": parent_hash, "request": request, "blender": report,
        "tool": {"executable_sha256": blender_hash, "worker_sha256": worker_hash, "command": command},
        "artifacts": artifacts,
        "validation_scope": "Mesh files and Blender FBX reimport only; no captured-building accuracy, collider or Unity approval",
    }
    with (output_dir / "receipt.json").open("x", encoding="utf-8") as stream:
        json.dump(receipt, stream, ensure_ascii=False, indent=2, allow_nan=False)
        stream.write("\n")
    return receipt


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mesh", type=Path)
    parser.add_argument("sampling_receipt", type=Path)
    parser.add_argument("output_dir", type=Path)
    parser.add_argument("--blender", type=Path, required=True)
    parser.add_argument("--source-forward", choices=AXES, required=True)
    parser.add_argument("--source-up", choices=AXES, required=True)
    parser.add_argument("--frame-reference", required=True)
    parser.add_argument("--decimate-ratio", type=float, default=1)
    parser.add_argument("--timeout-seconds", type=float, default=1800)
    args = parser.parse_args()
    try:
        receipt = prepare(args.mesh, args.sampling_receipt, args.output_dir, args.blender,
                          source_forward=args.source_forward, source_up=args.source_up,
                          frame_reference=args.frame_reference, decimate_ratio=args.decimate_ratio,
                          timeout_seconds=args.timeout_seconds)
    except (ValueError, OSError, KeyError, IndexError, TypeError, OverflowError, subprocess.SubprocessError) as exc:
        print(f"prepare-blender: {exc}", file=sys.stderr)
        return 2
    print(json.dumps({"status": receipt["status"], "bundle": str(args.output_dir)}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
