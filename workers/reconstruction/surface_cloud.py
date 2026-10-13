"""Sample an extracted PLY mesh for CloudCompare; does not train a radiance field."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import platform
import sys

import numpy as np
import trimesh


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_surface(path: Path) -> trimesh.Trimesh:
    """Let trimesh decode ASCII/binary PLY; require faces, not Gaussian centers."""
    if path.suffix.lower() != ".ply":
        raise ValueError("Expected an extracted surface mesh in .ply format")
    # Only inspect the bounded header for a clearer error; no custom PLY decoder.
    with path.open("rb") as stream:
        header = stream.read(65536).split(b"end_header", 1)[0]
    if any(token in header for token in (b" f_dc_0", b" scale_0", b" rot_0", b" opacity")):
        raise ValueError("Gaussian attributes found: extract a surface mesh first")
    mesh = trimesh.load_mesh(path, process=False)
    if not isinstance(mesh, trimesh.Trimesh) or len(mesh.faces) == 0:
        raise ValueError("No surface faces: sparse points/Gaussian centers are not a mesh")
    if not np.isfinite(mesh.vertices).all():
        raise ValueError("Mesh vertices contain NaN or infinity")
    if mesh.faces.min() < 0 or mesh.faces.max() >= len(mesh.vertices):
        raise ValueError("Mesh face index is outside the vertex array")
    if mesh.visual.kind == "texture":
        raise ValueError("Bake textures to PLY vertex colors before sampling")
    return mesh


def sample_surface(
    mesh: trimesh.Trimesh, count: int, seed: int, meters_per_unit: float, origin: list[float]
) -> tuple[trimesh.points.PointCloud, dict]:
    """Area-weighted sampling, preserving vertex color gradients and face normals."""
    if isinstance(count, bool) or not isinstance(count, int) or count <= 0:
        raise ValueError("points must be a positive integer")
    if isinstance(seed, bool) or not isinstance(seed, int) or seed < 0:
        raise ValueError("seed must be a nonnegative integer")
    if not math.isfinite(meters_per_unit) or meters_per_unit <= 0:
        raise ValueError("meters-per-unit must be finite and positive")
    offset = np.asarray(origin, dtype=np.float64)
    if offset.shape != (3,) or not np.isfinite(offset).all():
        raise ValueError("origin must contain three finite source-coordinate values")
    # Trimesh.copy() also derives vertex colors from face colors, pulling in SciPy.
    # Copy only the geometry and the supplied color representation we actually use.
    color_args = {}
    if mesh.visual.kind == "vertex":
        color_args["vertex_colors"] = mesh.visual.vertex_colors.copy()
    elif mesh.visual.kind == "face":
        color_args["face_colors"] = mesh.visual.face_colors.copy()
    mesh = trimesh.Trimesh(vertices=mesh.vertices.copy(), faces=mesh.faces.copy(), process=False, **color_args)
    # Convert before sampling: large global coordinates do not enter float32 PLY.
    with np.errstate(over="ignore", invalid="ignore"):
        mesh.vertices = (mesh.vertices - offset) * meters_per_unit
    if not np.isfinite(mesh.vertices).all():
        raise ValueError("Scale/origin conversion produced non-finite coordinates")
    bounds = mesh.bounds.tolist()
    areas = mesh.area_faces
    if not np.isfinite(areas).all() or not math.isfinite(float(areas.sum())):
        raise ValueError("Mesh surface areas are non-finite")
    positive = areas > 0
    ignored = int((~positive).sum())
    if not positive.any():
        raise ValueError("Mesh has no non-degenerate surface area")
    if ignored:
        mesh.update_faces(positive)
    positions, faces, barycentric = trimesh.sample.sample_surface(
        mesh, count, seed=seed, return_barycentric=True
    )
    normals = mesh.face_normals[faces]
    if (not np.isfinite(positions).all() or not np.isfinite(normals).all()
            or not np.allclose(np.linalg.norm(normals, axis=1), 1.0)):
        raise ValueError("Sampling produced invalid positions or normals")
    # The library exports xyz as float32. Refuse silently losing millimetres.
    with np.errstate(over="ignore", invalid="ignore"):
        encoded = positions.astype(np.float32)
        max_error = float(np.max(np.abs(positions - encoded.astype(np.float64))))
    if not np.isfinite(encoded).all() or max_error > 0.001:
        raise ValueError("PLY float32 precision exceeds 1 mm; choose a nearer --origin")
    colors = None
    if mesh.visual.kind == "vertex":
        corner_colors = mesh.visual.vertex_colors[mesh.faces[faces]]
        colors = np.rint(np.einsum("ni,nij->nj", barycentric, corner_colors)).clip(0, 255).astype(np.uint8)
        color_method = "barycentric_vertex_rgba"
    elif mesh.visual.kind == "face":
        colors = mesh.visual.face_colors[faces]
        color_method = "face_rgba"
    else:
        color_method = "none"
    cloud = trimesh.points.PointCloud(encoded, colors=colors)
    cloud.vertex_normals = normals
    return cloud, {
        "sampling": "trimesh.sample.sample_surface / triangle area weights",
        "points": count,
        "seed": seed,
        "positive_area_faces": len(mesh.faces),
        "ignored_zero_area_faces": ignored,
        "surface_area_m2": float(mesh.area),
        "source_mesh_bounds_m": bounds,
        "sampled_bounds_m": cloud.bounds.tolist(),
        "colors": color_method,
        "normals": "face_winding; orientation not automatically repaired",
        "max_float32_error_m": max_error,
    }


def convert(
    source: Path,
    output_dir: Path,
    *,
    count: int,
    seed: int,
    meters_per_unit: float,
    origin: list[float],
    axes: str,
    source_kind: str,
    source_reference: str,
    scale_reference: str,
) -> dict:
    """Write a new bundle; a final receipt distinguishes complete output."""
    source = source.resolve(strict=True)
    if output_dir.exists():
        raise ValueError("Output directory already exists; choose a new run directory")
    for name, value in (("axes", axes), ("source-reference", source_reference), ("scale-reference", scale_reference)):
        if not value.strip():
            raise ValueError(f"{name} must describe the input explicitly")
    if source_kind not in {"reconstruction-mesh", "existing-model", "test-fixture"}:
        raise ValueError("Unrecognized source-kind")
    before = sha256_file(source)
    mesh = load_surface(source)
    cloud, stats = sample_surface(mesh, count, seed, meters_per_unit, origin)
    payload = trimesh.exchange.ply.export_ply(
        cloud, encoding="binary_little_endian", vertex_normal=True, include_attributes=False
    )
    # A concurrently replaced input must not produce misleading provenance.
    if sha256_file(source) != before:
        raise ValueError("Input changed during conversion; retry from an immutable copy")
    transform = np.eye(4)
    transform[:3, :3] *= meters_per_unit
    transform[:3, 3] = -np.asarray(origin) * meters_per_unit
    inverse = np.eye(4)
    inverse[:3, :3] /= meters_per_unit
    inverse[:3, 3] = origin
    if not np.isfinite(inverse).all():
        raise ValueError("Scale cannot be represented by a finite inverse transform")
    receipt = {
        "schema_version": 1,
        "status": "surface_sampling_complete",
        "source": {"file": source.name, "sha256": before, "kind": source_kind, "reference": source_reference},
        "output": {"file": "surface_raw.ply", "sha256": hashlib.sha256(payload).hexdigest(), "bytes": len(payload)},
        "coordinates": {
            "unit": "metre",
            "axes": axes,
            "axis_conversion": "none; source handedness and axis directions preserved",
            "meters_per_source_unit": meters_per_unit,
            "scale_reference": scale_reference,
            "source_origin": list(origin),
            "source_to_local_m": transform.tolist(),
            "local_m_to_source": inverse.tolist(),
        },
        "geometry": stats,
        "versions": {"python": platform.python_version(), "numpy": np.__version__, "trimesh": trimesh.__version__},
        "validation_scope": "File and sampling checks only; metric accuracy, cleanup and Unity not validated",
    }
    # Exclusive directory creation also prevents concurrent runs overwriting one another.
    output_dir.mkdir(parents=True, exist_ok=False)
    with (output_dir / "surface_raw.ply").open("xb") as stream:
        stream.write(payload)
    with (output_dir / "receipt.json").open("x", encoding="utf-8") as stream:
        json.dump(receipt, stream, ensure_ascii=False, indent=2, allow_nan=False)
        stream.write("\n")
    return receipt


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mesh", type=Path, help="extracted triangle mesh, e.g. CityGaussianV2 fuse_post.ply")
    parser.add_argument("output_dir", type=Path, help="new directory; existing paths are never overwritten")
    parser.add_argument("--points", type=int, default=100000)
    parser.add_argument("--seed", type=int, default=20261011)
    parser.add_argument("--meters-per-unit", type=float, required=True)
    parser.add_argument("--origin", type=float, nargs=3, default=[0.0, 0.0, 0.0], metavar=("X", "Y", "Z"))
    parser.add_argument("--axes", required=True, help="source axes and handedness; no automatic Unity-axis conversion")
    parser.add_argument("--source-kind", required=True, choices=["reconstruction-mesh", "existing-model", "test-fixture"])
    parser.add_argument("--source-reference", required=True, help="capture/run/model provenance; no credentials")
    parser.add_argument("--scale-reference", required=True, help="how metres per source unit was established")
    args = parser.parse_args()
    try:
        receipt = convert(
            args.mesh, args.output_dir, count=args.points, seed=args.seed,
            meters_per_unit=args.meters_per_unit, origin=args.origin, axes=args.axes,
            source_kind=args.source_kind, source_reference=args.source_reference, scale_reference=args.scale_reference,
        )
    except (ValueError, OSError, IndexError, OverflowError, TypeError) as exc:
        print(f"surface-cloud: {exc}", file=sys.stderr)
        return 2
    print(json.dumps({"status": receipt["status"], "points": receipt["geometry"]["points"], "bundle": str(args.output_dir)}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
