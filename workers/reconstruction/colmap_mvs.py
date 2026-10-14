"""Run installed COLMAP MVS from a registered model; retain unknown model units."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
import math
import os
from pathlib import Path
import subprocess
import sys
import time

import numpy as np
import trimesh

from surface_cloud import load_surface, sha256_file


def inventory(directory: Path) -> list[dict]:
    """Fingerprint relative files, never copy or modify the capture/model."""
    files = []
    for path in sorted(directory.rglob("*")):
        if path.is_symlink() or (hasattr(path, "is_junction") and path.is_junction()):
            raise ValueError("Use a regular input directory without links/junctions")
        if path.is_file():
            files.append({"file": path.relative_to(directory).as_posix(),
                          "bytes": path.stat().st_size, "sha256": sha256_file(path)})
    if not files:
        raise ValueError("Input directory is empty")
    return files


def inspect_outputs(output_dir: Path) -> list[dict]:
    """Check actual PLY records, without inventing scale or geometric accuracy."""
    cloud_path = output_dir / "fused.ply"
    cloud = trimesh.load(cloud_path, process=False)
    if not isinstance(cloud, trimesh.points.PointCloud) or not len(cloud.vertices):
        raise ValueError("COLMAP fusion must produce a nonempty point cloud")
    if not np.isfinite(cloud.vertices).all():
        raise ValueError("Fused coordinates contain NaN or infinity")
    raw = cloud.metadata["_ply_raw"]["vertex"]["data"]
    if not isinstance(raw, np.ndarray) or raw.dtype.names is None:
        raise ValueError("Expected COLMAP binary PLY point records")
    fields = set(raw.dtype.names)
    if not {"x", "y", "z", "nx", "ny", "nz", "red", "green", "blue"} <= fields:
        raise ValueError("Fused PLY must store xyz, normals and RGB")
    if fields & {"f_dc_0", "opacity", "scale_0", "rot_0"}:
        raise ValueError("Gaussian rendering parameters are not fused surface points")
    normals = np.column_stack([raw[name] for name in ("nx", "ny", "nz")])
    if (not np.isfinite(normals).all()
            or not np.allclose(np.linalg.norm(normals, axis=1), 1, atol=0.002)):
        raise ValueError("Fused normals must be finite unit vectors")
    mesh_path = output_dir / "meshed-poisson.ply"
    mesh = load_surface(mesh_path)
    areas = mesh.area_faces
    if not np.isfinite(areas).all() or not np.any(areas > 0):
        raise ValueError("Poisson mesh has no finite positive surface area")
    geometries = [
        {"points": len(cloud.vertices), "bounds_model_units": cloud.bounds.tolist(),
         "stored_attributes": sorted(fields)},
        {"vertices": len(mesh.vertices), "triangles": len(mesh.faces),
         "bounds_model_units": mesh.bounds.tolist(),
         "zero_area_triangles": int(np.count_nonzero(areas == 0)),
         "color_kind": mesh.visual.kind},
    ]
    return [{"file": path.name, "bytes": path.stat().st_size,
             "sha256": sha256_file(path), "geometry": geometry}
            for path, geometry in zip((cloud_path, mesh_path), geometries)]


def reconstruct(images: Path, model: Path, output_dir: Path, colmap: Path, *,
                source_reference: str, max_image_size: int = 640,
                source_images: int = 10, iterations: int = 3, poisson_depth: int = 8,
                threads: int = 6, gpu_index: int = 0, cache_gb: float = 2,
                timeout_seconds: float = 3600) -> dict:
    """Keep request/logs on failure; write a success receipt only after validation."""
    images, model, colmap = (path.resolve(strict=True) for path in (images, model, colmap))
    output_dir = output_dir.resolve()
    if output_dir.exists():
        raise ValueError("Output directory already exists; choose a new run directory")
    if not images.is_dir() or not model.is_dir() or not colmap.is_file():
        raise ValueError("Supply image/model directories and an installed COLMAP executable")
    if output_dir.is_relative_to(images) or output_dir.is_relative_to(model):
        raise ValueError("Output must be outside input directories")
    if not source_reference.strip():
        raise ValueError("source-reference must identify the capture and registration")
    integers = {"max-image-size": max_image_size, "source-images": source_images,
                "iterations": iterations, "poisson-depth": poisson_depth, "threads": threads}
    for name, value in integers.items():
        if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
            raise ValueError(f"{name} must be a positive integer")
    if isinstance(gpu_index, bool) or not isinstance(gpu_index, int) or gpu_index < 0:
        raise ValueError("gpu-index must be a nonnegative integer")
    for name, value in (("cache-gb", cache_gb), ("timeout-seconds", timeout_seconds)):
        if not math.isfinite(value) or value <= 0:
            raise ValueError(f"{name} must be finite and positive")
    if not any(all((model / (name + suffix)).is_file() for name in ("cameras", "images", "points3D"))
               for suffix in (".bin", ".txt")):
        raise ValueError("Supply one complete COLMAP sparse model (binary or text)")
    source = {"reference": source_reference, "images": inventory(images), "model": inventory(model)}
    executable_hash = sha256_file(colmap)
    worker_hash = sha256_file(Path(__file__))
    settings = {"max_image_size": max_image_size, "source_images": source_images,
                "iterations": iterations, "poisson_depth": poisson_depth, "threads": threads,
                "gpu_index": gpu_index, "cache_gb": cache_gb, "timeout_seconds_per_step": timeout_seconds}
    output_dir.mkdir(parents=True, exist_ok=False)
    dense = output_dir / "dense"
    dense.mkdir()
    commands = [
        ("version", ["help"]),
        ("model", ["model_analyzer", "--path", str(model)]),
        ("undistort", ["image_undistorter", "--image_path", str(images), "--input_path", str(model),
                       "--output_path", str(dense), "--output_type", "COLMAP",
                       "--max_image_size", str(max_image_size), "--num_patch_match_src_images", str(source_images)]),
        ("patch_match", ["patch_match_stereo", "--workspace_path", str(dense), "--workspace_format", "COLMAP",
                         "--PatchMatchStereo.max_image_size", str(max_image_size),
                         "--PatchMatchStereo.gpu_index", str(gpu_index),
                         "--PatchMatchStereo.num_iterations", str(iterations),
                         "--PatchMatchStereo.geom_consistency", "1", "--PatchMatchStereo.cache_size", str(cache_gb)]),
        ("fuse", ["stereo_fusion", "--workspace_path", str(dense), "--workspace_format", "COLMAP",
                  "--input_type", "geometric", "--output_path", str(output_dir / "fused.ply"),
                  "--StereoFusion.num_threads", str(threads), "--StereoFusion.max_image_size", str(max_image_size),
                  "--StereoFusion.cache_size", str(cache_gb), "--StereoFusion.use_cache", "1"]),
        ("mesh", ["poisson_mesher", "--input_path", str(output_dir / "fused.ply"),
                  "--output_path", str(output_dir / "meshed-poisson.ply"),
                  "--PoissonMeshing.depth", str(poisson_depth), "--PoissonMeshing.num_threads", str(threads)]),
    ]
    request = {"schema_version": 1, "started_at_utc": datetime.now(timezone.utc).isoformat(),
               "source": source, "settings": settings, "executable_sha256": executable_hash,
               "worker_sha256": worker_hash,
               "commands": [{"stage": name, "argv": [str(colmap), *args]} for name, args in commands]}
    (output_dir / "request.json").write_text(json.dumps(request, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    env = os.environ.copy()
    env["QT_QPA_PLATFORM"] = "offscreen"
    # The official Windows portable build places Qt plugins beside bin/. Do not
    # change machine configuration or assume that layout for other installations.
    plugins = colmap.parent.parent / "plugins"
    if os.name == "nt" and plugins.is_dir():
        env["QT_QPA_PLATFORM_PLUGIN_PATH"] = str(plugins)
    timings = []
    for command in request["commands"]:
        stage = command["stage"]
        started = time.monotonic()
        with (output_dir / (stage + ".log")).open("xb") as log:
            result = subprocess.run(command["argv"], cwd=output_dir, env=env,
                                    stdout=log, stderr=subprocess.STDOUT, timeout=timeout_seconds,
                                    check=False, creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
        if result.returncode:
            raise ValueError(f"COLMAP {stage} exited {result.returncode}; inspect logs, no success receipt")
        timings.append({"stage": stage, "elapsed_seconds": round(time.monotonic() - started, 3)})
        (output_dir / "stages.json").write_text(json.dumps(timings, indent=2) + "\n", encoding="utf-8")
    artifacts = inspect_outputs(output_dir)
    if inventory(images) != source["images"] or inventory(model) != source["model"]:
        raise ValueError("Input images/model changed during reconstruction")
    if sha256_file(colmap) != executable_hash or sha256_file(Path(__file__)) != worker_hash:
        raise ValueError("COLMAP executable/worker changed during reconstruction")
    receipt = {"schema_version": 1, "status": "colmap_mvs_complete_review_required",
               "completed_at_utc": datetime.now(timezone.utc).isoformat(),
               "source": source, "request_sha256": sha256_file(output_dir / "request.json"),
               "coordinates": {"unit": "unknown_model_units", "meters_per_unit": None,
                               "axis_conversion": "none", "scale_reference": None},
               "registration": "input model reused; no new SfM executed",
               "settings": settings, "stages": timings, "artifacts": artifacts,
               "validation_scope": "Process and finite geometry checks only; no absolute accuracy, mesh quality, metric handoff or Unity acceptance"}
    with (output_dir / "receipt.json").open("x", encoding="utf-8") as stream:
        json.dump(receipt, stream, indent=2, allow_nan=False)
        stream.write("\n")
    return receipt


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("images", type=Path)
    parser.add_argument("model", type=Path, help="one registered COLMAP sparse model")
    parser.add_argument("output_dir", type=Path, help="new directory outside the inputs")
    parser.add_argument("--colmap", type=Path, required=True)
    parser.add_argument("--source-reference", required=True)
    parser.add_argument("--max-image-size", type=int, default=640)
    parser.add_argument("--source-images", type=int, default=10)
    parser.add_argument("--iterations", type=int, default=3)
    parser.add_argument("--poisson-depth", type=int, default=8)
    parser.add_argument("--threads", type=int, default=6)
    parser.add_argument("--gpu-index", type=int, default=0)
    parser.add_argument("--cache-gb", type=float, default=2)
    parser.add_argument("--timeout-seconds", type=float, default=3600)
    args = parser.parse_args()
    try:
        receipt = reconstruct(**vars(args))
    except (ValueError, OSError, KeyError, IndexError, TypeError, OverflowError, subprocess.SubprocessError) as exc:
        print(f"colmap-mvs: {exc}", file=sys.stderr)
        return 2
    print(json.dumps({"status": receipt["status"], "bundle": str(args.output_dir)}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
