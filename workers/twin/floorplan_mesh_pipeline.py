"""Build editable architectural meshes from reviewed source-pixel polygons.

This is geometry generation, not a floorplan recognition model. Its input must
separate observed walls, openings, columns and nonfloor regions. Lease polygons
alone never generate enclosing walls. No measured-accuracy claim is made here.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
import shapely
from shapely.geometry import Polygon, shape
from shapely.geometry.polygon import orient


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def geometry(value):
    if isinstance(value, dict):
        return shape(value)
    return Polygon(value)


def polygons(value):
    if value.is_empty:
        return []
    if value.geom_type == "Polygon":
        return [value]
    return [part for part in value.geoms if part.geom_type == "Polygon"]


def extrude(poly, bottom: float, top: float, frame: np.ndarray):
    """Return closed, outward-facing triangle soup; holes remain holes."""
    if not poly.is_valid or poly.area <= 0 or not top > bottom:
        raise ValueError("Valid nonempty polygon and positive extrusion required")
    verts, triangles, uv = [], [], []

    def world(p, y):
        x, z = np.r_[p[:2], 1.0] @ frame
        return [float(x), float(y), float(z)]

    def triangle(points, desired):
        p = np.asarray(points, float)
        n = np.cross(p[1] - p[0], p[2] - p[0])
        if np.linalg.norm(n) < 1e-10:
            raise ValueError("Degenerate triangle")
        if np.dot(n, desired) < 0:
            p[[1, 2]] = p[[2, 1]]
        start = len(verts)
        verts.extend(p.tolist())
        triangles.extend([start, start + 1, start + 2])
        if abs(n[1]) > max(abs(n[0]), abs(n[2])):
            uv.extend([[float(v[0]), float(v[2])] for v in p])
        else:
            axis = 2 if abs(n[0]) > abs(n[2]) else 0
            uv.extend([[float(v[axis]), float(v[1])] for v in p])

    poly = orient(poly, sign=1)
    cap_area = 0.0
    for tri in shapely.constrained_delaunay_triangles(poly).geoms:
        if not poly.covers(tri):
            raise ValueError("Cap triangulation crosses polygon boundary")
        q = list(tri.exterior.coords)[:3]
        triangle([world(p, top) for p in q], [0, 1, 0])
        triangle([world(p, bottom) for p in q], [0, -1, 0])
        cap_area += tri.area
    if abs(cap_area - poly.area) > max(1e-7, poly.area * 1e-9):
        raise ValueError("Cap area does not preserve source polygon")
    for ring in [poly.exterior, *poly.interiors]:
        q = list(ring.coords)
        for a, b in zip(q, q[1:]):
            p0, p1, p2, p3 = world(a, bottom), world(b, bottom), world(b, top), world(a, top)
            # Interior of a source CCW exterior is left of the edge. The right
            # vector points out; CW hole rings naturally reverse that direction.
            edge = np.asarray(b) - np.asarray(a)
            outside_pixel = np.array([edge[1], -edge[0]])
            outside_xz = outside_pixel @ frame[:2]
            outside = [outside_xz[0], 0, outside_xz[1]]
            triangle([p0, p1, p2], outside)
            triangle([p0, p2, p3], outside)
    return {"vertices": verts, "triangles": triangles, "uv": uv}


def build(spec_path: Path, frame_path: Path, output: Path):
    spec, frame_spec = json.loads(spec_path.read_text()), json.loads(frame_path.read_text())
    source = Path(spec.get("source", frame_spec["source"]))
    if digest(source) != spec["source_sha256"] or frame_spec["source_sha256"] != spec["source_sha256"]:
        raise ValueError("Source raster or coordinate authority changed")
    frame = np.asarray(frame_spec["pixel_to_world_XZ_row_matrix"], float)
    if frame.shape != (3, 2) or not np.isfinite(frame).all() or abs(np.linalg.det(frame[:2])) < 1e-8:
        raise ValueError("Invalid pixel frame")
    objects, identifiers, checks = [], set(), []
    for item in spec["solids"]:
        name = item["id"]
        if name in identifiers:
            raise ValueError("Duplicate source feature id: " + name)
        identifiers.add(name)
        poly = geometry(item["polygon_px"])
        if not poly.is_valid or poly.is_empty:
            raise ValueError("Invalid source polygon: " + name)
        bottom, top = float(item["bottom_y"]), float(item["top_y"])
        for i, part in enumerate(polygons(poly)):
            mesh = extrude(part, bottom, top, frame)
            objects.append({"id": name if i == 0 else name + "_" + str(i),
                            "kind": item["kind"], "material": item.get("material", "plaster"),
                            "collider": item.get("collider", True),
                            "source_feature": name, "source_polygon_px": shapely.geometry.mapping(part),
                            "bottom_y": bottom, "top_y": top, **mesh})
        checks.append({"id": name, "source_area_px2": poly.area,
                       "source_polygon_valid": True, "height_positive": top > bottom})
    package = {"schema": "chooguard.reviewed-floorplan-mesh-package.v1",
               "source": str(source), "source_sha256": digest(source),
               "spec": str(spec_path), "spec_sha256": digest(spec_path),
               "frame": str(frame_path), "frame_sha256": digest(frame_path),
               "coordinate_space": "Unity world XYZ, Y up",
               "site_similarity_percent": None, "metric_dimensions_measured": False,
               "objects": objects, "checks": checks}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(package, ensure_ascii=False, separators=(",", ":")) + "\n")
    print(json.dumps({"mesh_package": str(output), "sha256": digest(output),
                      "objects": len(objects), "triangles": sum(len(o["triangles"]) // 3 for o in objects)},
                     ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("spec", type=Path)
    parser.add_argument("frame", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    build(args.spec, args.frame, args.output)
