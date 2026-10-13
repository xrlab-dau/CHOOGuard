"""Run CloudCompare SOR/subsampling with input provenance and verifiable outputs."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import subprocess
import sys

import numpy as np
import trimesh

from surface_cloud import sha256_file


def read_cloud(path: Path) -> dict:
    """Reuse trimesh's PLY decoder, including explicit stored attribute presence."""
    cloud = trimesh.load(path, process=False)
    if not isinstance(cloud, trimesh.points.PointCloud) or len(cloud.vertices) == 0:
        raise ValueError("Expected a nonempty PLY point cloud without faces")
    data = cloud.metadata["_ply_raw"]["vertex"]["data"]
    # Sampler and the tested CloudCompare binary both write structured binary PLY.
    if not isinstance(data, np.ndarray) or data.dtype.names is None:
        raise ValueError("Expected binary PLY point records")
    fields = set(data.dtype.names)
    if not {"x", "y", "z", "nx", "ny", "nz"} <= fields:
        raise ValueError("Point cloud must contain explicit xyz and normals")
    xyz = np.asarray(cloud.vertices)
    normals = np.column_stack([data[name] for name in ("nx", "ny", "nz")])
    if not np.isfinite(xyz).all() or not np.isfinite(normals).all():
        raise ValueError("Point coordinates/normals contain NaN or infinity")
    if not np.allclose(np.linalg.norm(normals, axis=1), 1, atol=0.002):
        raise ValueError("Point normals must have unit length")
    if any(data.dtype[name] != np.dtype("<f4") for name in ("x", "y", "z")):
        raise ValueError("Expected sampler-compatible float32 xyz")
    color_fields = fields & {"red", "green", "blue"}
    if color_fields and len(color_fields) != 3:
        raise ValueError("Incomplete RGB attributes")
    if "alpha" in fields and np.any(data["alpha"] != 255):
        raise ValueError("CloudCompare drops alpha; input must be opaque before cleanup")
    names = ["x", "y", "z"] + (["red", "green", "blue"] if color_fields else [])
    # Canonical records compare retained positions AND RGB, regardless of PLY field order.
    keys = np.empty(len(data), dtype=[(name, data.dtype[name]) for name in names])
    for name in names:
        keys[name] = data[name]
    return {"keys": keys, "points": len(data), "bounds_m": cloud.bounds.tolist(),
            "rgb": bool(color_fields), "alpha_stored": "alpha" in fields}


def verify_subset(before: dict, after: dict) -> None:
    """SOR and spatial subsampling must only remove existing xyz/RGB records."""
    if before["keys"].dtype != after["keys"].dtype:
        raise ValueError("Cleanup changed coordinate or RGB storage")
    source, source_counts = np.unique(before["keys"], return_counts=True)
    output, output_counts = np.unique(after["keys"], return_counts=True)
    indices = np.searchsorted(source, output)
    if (np.any(indices >= len(source)) or not np.array_equal(source[indices], output)
            or np.any(output_counts > source_counts[indices])):
        raise ValueError("Cleanup moved/added points, changed RGB, or duplicated records")


def cleanup(bundle: Path, output_dir: Path, executable: Path, *, tool_version: str,
            neighbors: int, sigma: float, spacing_m: float, timeout_seconds: float) -> dict:
    """A receipt is emitted only after process, integrity and subset checks pass."""
    bundle = bundle.resolve(strict=True)
    executable = executable.resolve(strict=True)
    output_dir = output_dir.resolve()
    if output_dir.exists():
        raise ValueError("Output directory already exists; choose a new run directory")
    if not executable.is_file() or not tool_version.strip():
        raise ValueError("Supply an executable file and its version/provenance label")
    if isinstance(neighbors, bool) or not isinstance(neighbors, int) or neighbors < 2:
        raise ValueError("SOR neighbors must be an integer >= 2")
    for name, value in (("sigma", sigma), ("spacing-m", spacing_m), ("timeout-seconds", timeout_seconds)):
        if not math.isfinite(value) or value <= 0:
            raise ValueError(f"{name} must be finite and positive")
    receipt_path = bundle / "receipt.json"
    receipt_hash = sha256_file(receipt_path)
    parent = json.loads(receipt_path.read_text(encoding="utf-8"))
    if (parent["schema_version"] != 1 or parent["status"] != "surface_sampling_complete"
            or parent["output"]["file"] != "surface_raw.ply"
            or parent["coordinates"]["unit"] != "metre"):
        raise ValueError("Expected a completed local-metre surface sampler bundle")
    source = bundle / "surface_raw.ply"
    input_hash = sha256_file(source)
    if input_hash != parent["output"]["sha256"] or source.stat().st_size != parent["output"]["bytes"]:
        raise ValueError("Input PLY does not match its sampling receipt")
    raw = read_cloud(source)
    if raw["points"] != parent["geometry"]["points"] or raw["points"] <= neighbors:
        raise ValueError("Input point count differs from receipt or is too small for SOR")
    executable_hash = sha256_file(executable)
    output_dir.mkdir(parents=True, exist_ok=False)
    sor_path = output_dir / "surface_sor.ply"
    clean_path = output_dir / "surface_clean.ply"
    command = [str(executable), "-SILENT", "-LOG_FILE", str(output_dir / "cloudcompare.log"),
               "-AUTO_SAVE", "OFF", "-C_EXPORT_FMT", "PLY", "-PLY_EXPORT_FMT", "BINARY_LE",
               "-O", "-GLOBAL_SHIFT", "0", "0", "0", str(source),
               # FILE has its own space-separated-list parser. Fixed relative names
               # avoid a second quoting language even when output_dir contains spaces.
               "-SOR", str(neighbors), str(sigma), "-SAVE_CLOUDS", "FILE", "./surface_sor.ply",
               "-SS", "SPATIAL", str(spacing_m), "-SAVE_CLOUDS", "FILE", "./surface_clean.ply"]
    request = {"command": command, "tool_version_declared": tool_version,
               "working_directory": str(output_dir),
               "executable_sha256": executable_hash, "parent_receipt_sha256": receipt_hash,
               "started_at": datetime.now(timezone.utc).isoformat()}
    (output_dir / "request.json").write_text(json.dumps(request, indent=2) + "\n", encoding="utf-8")
    # Direct argument vector: spaces/non-ASCII paths are not shell commands.
    with (output_dir / "process.log").open("xb") as log:
        result = subprocess.run(command, cwd=output_dir, stdout=log, stderr=subprocess.STDOUT,
                                timeout=timeout_seconds, check=False,
                                creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
    if result.returncode != 0:
        raise ValueError(f"CloudCompare exited {result.returncode}; inspect logs, no success receipt")
    if sha256_file(source) != input_hash or sha256_file(receipt_path) != receipt_hash:
        raise ValueError("Input bundle changed during cleanup")
    if sha256_file(executable) != executable_hash:
        raise ValueError("CloudCompare executable changed during cleanup")
    sor = read_cloud(sor_path)
    clean = read_cloud(clean_path)
    verify_subset(raw, sor)
    verify_subset(sor, clean)
    stages = []
    previous = raw["points"]
    for label, path, cloud in (("sor", sor_path, sor), ("spatial_subsampling", clean_path, clean)):
        stages.append({"stage": label, "file": path.name, "sha256": sha256_file(path),
                       "bytes": path.stat().st_size, "points": cloud["points"],
                       "removed_from_previous": previous - cloud["points"],
                       "removed_fraction": 1 - cloud["points"] / previous,
                       "bounds_m": cloud["bounds_m"], "rgb": cloud["rgb"],
                       "alpha_stored": cloud["alpha_stored"]})
        previous = cloud["points"]
    receipt = {
        "schema_version": 1, "status": "cleanup_complete_review_required",
        "completed_at": datetime.now(timezone.utc).isoformat(),
        "source": parent["source"], "coordinates": parent["coordinates"],
        "parent_receipt_sha256": receipt_hash, "input_sha256": input_hash,
        "input_points": raw["points"], "tool": request,
        "parameters": {"sor_neighbors": neighbors, "sor_sigma": sigma, "spatial_step_m": spacing_m},
        "stages": stages,
        "attributes": "xyz/RGB retained exactly; opaque alpha may be omitted; CloudCompare quantizes normals",
        "validation_scope": "Process and file/subset checks only; no geometric-quality, AI-mask or Unity approval",
    }
    # Carry the exact parent receipt, not just its path on one machine.
    parent_bytes = receipt_path.read_bytes()
    if hashlib.sha256(parent_bytes).hexdigest() != receipt_hash:
        raise ValueError("Parent receipt changed while recording provenance")
    (output_dir / "sampling_receipt.json").write_bytes(parent_bytes)
    with (output_dir / "receipt.json").open("x", encoding="utf-8") as stream:
        json.dump(receipt, stream, ensure_ascii=False, indent=2, allow_nan=False)
        stream.write("\n")
    return receipt


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("bundle", type=Path, help="completed surface_cloud.py output directory")
    parser.add_argument("output_dir", type=Path, help="new cleanup directory")
    parser.add_argument("--cloudcompare", type=Path, required=True)
    parser.add_argument("--tool-version", required=True, help="installed version/provenance; not auto-detected")
    parser.add_argument("--neighbors", type=int, required=True)
    parser.add_argument("--sigma", type=float, required=True)
    parser.add_argument("--spacing-m", type=float, required=True)
    parser.add_argument("--timeout-seconds", type=float, default=1800)
    args = parser.parse_args()
    try:
        receipt = cleanup(args.bundle, args.output_dir, args.cloudcompare, tool_version=args.tool_version,
                          neighbors=args.neighbors, sigma=args.sigma, spacing_m=args.spacing_m,
                          timeout_seconds=args.timeout_seconds)
    except (ValueError, OSError, KeyError, IndexError, TypeError, OverflowError, subprocess.SubprocessError) as exc:
        print(f"clean-cloud: {exc}", file=sys.stderr)
        return 2
    print(json.dumps({"status": receipt["status"], "input_points": receipt["input_points"],
                      "output_points": receipt["stages"][-1]["points"], "bundle": str(args.output_dir)}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
