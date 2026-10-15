#!/usr/bin/env python3
"""Read-only comparison of retained 1F registration against exported Unity faces.

No scene mutation or replacement geometry. Pixel masks are old observations,
not measured perimeters. Triangle ordinals refer to the exported flattened
index array. Geometric first hits are double-sided and do not simulate shaders.
"""
from __future__ import annotations

import os
os.environ.setdefault("OMP_NUM_THREADS", "2")
os.environ.setdefault("OPENBLAS_NUM_THREADS", "2")

import argparse
import hashlib
import json
import math
import re
import struct
from collections import Counter
from itertools import combinations
from pathlib import Path

import cv2
import numpy as np
import shapely
from PIL import Image, ImageDraw, ImageFont
from rtree import index as spatial_index
from shapely.geometry import Point, Polygon, mapping
from shapely.ops import unary_union

ROOT = Path(__file__).resolve().parents[2]
OLD = ROOT / ".planning/2026-09-23-video-twin"
RECOVERED = ROOT / "asset-library/research-public/2026-10-08/architectural-recovery"
DEFAULT_SCENE = ROOT / ".planning/2026-10-08-interior-runtime-completion/architecture/current-scene-structure.json"
OUT = ROOT / "workers/twin/evidence/registered-structure-20261008"
SWAP = np.array([[1., 0., 0.], [0., 0., 1.], [0., 1., 0.]])
SIN, COS = math.sin(math.radians(16.2)), math.cos(math.radians(16.2))
ENU_UVY = np.array([[SIN, COS, 0.], [COS, -SIN, 0.], [0., 0., 1.]])
# This finite volume includes all fitted polygons and nearby competing finishes.
# A miss beyond this volume is explicitly unresolved, never proof of no wall.
VOLUME_LO = np.array([-40., -.5, 8.])
VOLUME_HI = np.array([1., 12., 48.])
FRAMES = (54, 57, 61, 66, 68, 106, 133)


def read(path):
    return json.loads(Path(path).read_text())


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def save(path, data):
    Path(path).write_text(json.dumps(data, ensure_ascii=False, indent=2, allow_nan=False) + "\n")


def rel(path):
    return str(Path(path).resolve().relative_to(ROOT))


def uvy_to_world(points):
    return np.asarray(points) @ ENU_UVY @ SWAP.T


def world_to_uvy(points):
    return np.asarray(points) @ SWAP @ ENU_UVY.T


def camera_basis(cam):
    forward, up = np.asarray(cam["forward"]), np.asarray(cam["up"])
    return np.stack((np.cross(up, forward), -up, forward))


def project(world, cam):
    p = (np.asarray(world) - cam["position"]) @ camera_basis(cam).T
    with np.errstate(divide="ignore", invalid="ignore"):
        xy = p[..., :2] / p[..., 2, None]
        f, cx, cy, radial = cam["intrinsics"]
        pixels = f * xy * (1 + radial * np.sum(xy * xy, axis=-1, keepdims=True)) + [cx, cy]
    return pixels, p[..., 2]


def pixel_rays(pixels, cam):
    f, cx, cy, k = cam["intrinsics"]
    distorted = (np.asarray(pixels) - [cx, cy]) / f
    # Newton inversion of the actual SIMPLE_RADIAL radial polynomial.
    rd = np.linalg.norm(distorted, axis=1)
    radius = rd.copy()
    for _ in range(12):
        radius -= (radius + k * radius**3 - rd) / (1 + 3 * k * radius**2)
    factor = np.divide(radius, rd, out=np.ones_like(rd), where=rd > 1e-12)
    local = np.column_stack((distorted * factor[:, None], np.ones(len(pixels))))
    world = local @ camera_basis(cam)
    return world / np.linalg.norm(world, axis=1)[:, None]


def verify_cameras(cameras):
    registration = read(OLD / "metric-registration/registration.json")
    raw = read(OLD / "1f-2026-sfm/sparse/2/poses.json")
    assert raw["cameras"][0]["model"] == "SIMPLE_RADIAL"
    rotation, translation = np.asarray(registration["rotation"]), np.asarray(registration["translation"])
    result = []
    for number in FRAMES:
        name = f"{number:06d}.jpg"
        old = next(row for row in raw["poses"] if row["image"] == name)
        cam = cameras[name]
        r = np.asarray(old["worldToCameraRotation"])
        expected_c = SWAP @ (registration["scale"] * rotation @ old["centre"] + translation)
        expected_f, expected_u = SWAP @ rotation @ r[2], SWAP @ rotation @ -r[1]
        errors = [np.max(np.abs(expected_c - cam["position"])),
                  np.max(np.abs(expected_f - cam["forward"])),
                  np.max(np.abs(expected_u - cam["up"]))]
        assert max(errors) < 1e-10, (name, errors)
        principal = pixel_rays([[cam["intrinsics"][1], cam["intrinsics"][2]]], cam)[0]
        assert np.linalg.norm(principal - expected_f) < 1e-10
        probes = np.array([[50, 30], [480, 270], [900, 490]], float)
        roundtrip, _ = project(np.asarray(cam["position"]) + pixel_rays(probes, cam) * 10, cam)
        assert np.max(np.abs(roundtrip - probes)) < 1e-8
        result.append({"frame": number, "raw_sfm_position_forward_up_max_errors": errors,
                       "principal_ray_world": principal.tolist(),
                       "distorted_pixel_ray_roundtrip_max_error_960": float(np.max(np.abs(roundtrip - probes)))})
    # Independently match the source builder's P(U,V,Y)=(V,Y,U) and root yaw.
    root_yaw = np.array([[COS, 0., SIN], [0., 1., 0.], [-SIN, 0., COS]])
    probes = np.array([[5., -23., 4.9], [17., -15., 2.26]])
    builder_world = probes[:, [1, 2, 0]] @ root_yaw.T
    assert np.max(np.abs(builder_world - uvy_to_world(probes))) < 1e-10
    return {"convention": "registered Unity world = ENU[x,z,y]; no additional root yaw",
            "raw_intrinsics_model": "SIMPLE_RADIAL", "frame_checks": result,
            "UVY_to_world": "X=U*sin16.2+V*cos16.2; Y=Y; Z=U*cos16.2-V*sin16.2",
            "builder_root_inverse_max_error": float(np.max(np.abs(builder_world - uvy_to_world(probes)))),
            "source_model_scale": registration["scale"], "portal_fit_pixel_RMS_960": registration["fit"]["portalPixelRMS"],
            "surveyed_scale": False, "limits": registration["limits"]}


def geometry(scene):
    records = {m["id"]: m for m in scene["meshes"]}
    cache, triangles, owners, ordinals, vertex_ids, owner_records, omitted = {}, [], [], [], [], [], []
    for row in scene["renderers"]:
        if not row["active"] or not row["renderer_enabled"] or "ShadowCaster" in row["path"]:
            continue
        if np.any(np.asarray(row["world_bounds_max"]) < VOLUME_LO) or np.any(np.asarray(row["world_bounds_min"]) > VOLUME_HI):
            continue
        mesh = records[row["mesh"]]
        if not mesh.get("binary"):
            omitted.append({"owner": row["path"], "mesh": row["mesh"], "asset": mesh["asset"],
                            "reason": "actual mesh unreadable; no substitute box generated"})
            continue
        if mesh["id"] not in cache:
            path = ROOT / mesh["binary"]
            assert sha(path) == mesh["binary_sha256"], str(path)
            with path.open("rb") as stream:
                nv, ni, nn = struct.unpack("<iii", stream.read(12))
                vertices = np.frombuffer(stream.read(nv * 12), dtype="<f4").reshape(-1, 3)
                indices = np.frombuffer(stream.read(ni * 4), dtype="<i4").reshape(-1, 3)
                assert ni % 3 == 0 and (indices >= 0).all() and (indices < nv).all()
                assert path.stat().st_size == 12 + nv * 12 + ni * 4 + nn * 12
            cache[mesh["id"]] = vertices, indices
        vertices, indices = cache[mesh["id"]]
        transform = np.asarray(row["world_matrix"])
        world = vertices @ transform[:3, :3].T + transform[:3, 3]
        tr = world[indices]
        use = (tr.max(1) >= VOLUME_LO).all(1) & (tr.min(1) <= VOLUME_HI).all(1)
        if not use.any():
            continue
        owner = len(owner_records)
        material_contracts = []
        for material in row["materials"]:
            text = (ROOT / material).read_text() if (ROOT / material).exists() else ""
            surface = re.search(r"- _Surface: (\d+)", text)
            cull = re.search(r"- _Cull: (\d+)", text)
            material_contracts.append({"path": material, "sha256": sha(ROOT / material) if text else None,
                                       "URP_surface": int(surface[1]) if surface else None,
                                       "cull": int(cull[1]) if cull else None})
        owner_records.append({"owner": row["path"], "global_object_id": row["global_object_id"],
                              "mesh": mesh["id"], "mesh_asset": mesh["asset"],
                              "mesh_asset_guid": mesh["asset_guid"],
                              "binary": mesh["binary"], "binary_sha256": mesh["binary_sha256"],
                              "world_matrix": row["world_matrix"], "materials": row["materials"],
                              "material_contracts": material_contracts})
        triangles.append(tr[use].astype(np.float64))
        owners.append(np.full(use.sum(), owner, np.int32))
        ordinals.append(np.flatnonzero(use).astype(np.int32))
        vertex_ids.append(indices[use])
    return {"triangles": np.concatenate(triangles), "owner": np.concatenate(owners),
            "ordinal": np.concatenate(ordinals), "vertices": np.concatenate(vertex_ids),
            "owners": owner_records, "omitted_unreadable_renderers": omitted}


def projection_index(triangles, cam):
    # Edge midpoint samples bound small radial curvature. A broad12px pad also
    # keeps triangle candidate selection conservative; ray intersection is exact.
    samples = np.concatenate((triangles, (triangles + np.roll(triangles, -1, axis=1)) / 2), axis=1)
    pixels, depths = project(samples, cam)
    front = (depths.min(1) > .05)
    low = np.min(pixels, axis=1) - 12
    high = np.max(pixels, axis=1) + 12
    crossing = (depths.min(1) <= .05) & (depths.max(1) > .05)
    low[crossing], high[crossing] = [0, 0], [960, 540]
    use = (front | crossing) & np.isfinite(low).all(1) & np.isfinite(high).all(1)
    use &= (high >= [0, 0]).all(1) & (low <= [960, 540]).all(1)
    ids = np.flatnonzero(use)
    return spatial_index.Index(((int(i), (*low[i], *high[i]), None) for i in ids)), len(ids)


def hits(pixels, camera, geo, tree, known_opaque_frontfaces=False):
    triangles, origin = geo["triangles"], np.asarray(camera["position"])
    rows = []
    for pixel, direction in zip(pixels, pixel_rays(pixels, camera)):
        candidates = np.fromiter(tree.intersection((*pixel, *pixel)), dtype=np.int64)
        if not len(candidates):
            rows.append({"pixel_960": pixel.tolist(), "hit": None}); continue
        tr = triangles[candidates]
        e1, e2 = tr[:, 1] - tr[:, 0], tr[:, 2] - tr[:, 0]
        pvec = np.cross(direction, e2)
        det = np.einsum("ij,ij->i", e1, pvec)
        valid = np.abs(det) > 1e-10
        inv = np.divide(1., det, out=np.zeros_like(det), where=valid)
        tv = origin - tr[:, 0]
        u = np.einsum("ij,ij->i", tv, pvec) * inv
        q = np.cross(tv, e1)
        v = q @ direction * inv
        distance = np.einsum("ij,ij->i", e2, q) * inv
        valid &= (u >= -1e-7) & (v >= -1e-7) & (u + v <= 1 + 1e-7) & (distance > .03)
        if known_opaque_frontfaces:
            # Only certain single-material opaque owners; batched submesh face
            # ranges are absent from export, so mixed materials remain unknown.
            material_valid = []
            for triangle_id in candidates:
                contracts = geo["owners"][int(geo["owner"][triangle_id])]["material_contracts"]
                contract = contracts[0] if len(contracts) == 1 else {}
                opaque = contract.get("URP_surface") == 0
                front = det[len(material_valid)] > 0 if contract.get("cull") == 2 else True
                material_valid.append(opaque and front)
            valid &= np.asarray(material_valid)
        if not valid.any():
            rows.append({"pixel_960": pixel.tolist(), "hit": None}); continue
        distance[~valid] = np.inf
        i = int(candidates[int(np.argmin(distance))])
        point = origin + direction * float(np.min(distance))
        normal = np.cross(triangles[i, 1] - triangles[i, 0], triangles[i, 2] - triangles[i, 0])
        nlength = np.linalg.norm(normal)
        normal = normal / nlength if nlength else normal
        rows.append({"pixel_960": pixel.tolist(), "hit": {"triangle_array_id": i,
                     "owner_index": int(geo["owner"][i]), "triangle_ordinal": int(geo["ordinal"][i]),
                     "vertex_indices": geo["vertices"][i].tolist(), "world": point.tolist(),
                     "UVY": world_to_uvy(point).tolist(), "distance_source_units": float(np.min(distance)),
                     "world_triangle_normal": normal.tolist()}})
    return rows


def cases():
    result = []
    for component in read(OLD / "ground-core-fit/observations.json")["components"]:
        if component["id"] not in ("higher-lobby-ceiling", "low-suspended-ceiling", "right-approach-wall"):
            continue
        fitted = read(OLD / f"ground-core-fit/{component['id']}.json")
        for observation in component["observations"]:
            if observation["frame"] in FRAMES:
                result.append({"id": f"{component['id']}-{observation['frame']:06d}",
                               "frame": observation["frame"], "kind": component["id"],
                               "polygon_960": observation["polygon"], "historical_role": observation["role"],
                               "fit": fitted, "source": f".planning/2026-09-23-video-twin/ground-core-fit/{component['id']}.json"})
    frontage = read(OLD / "entry-frontage-fit/result.json")["components"][0]
    observations = read(OLD / "entry-frontage-fit/observations.json")["components"][0]
    plane = frontage["planeUVY"]
    for number in (57,61,66,68):
        name = f"{number:06d}.jpg"
        polygon = observations["fit"].get(name) or observations["check"].get(name)
        role = "fit" if name in observations["fit"] else "check"
        if number == 68:
            polygon = [[878,45],[927,45],[935,350],[878,350]]
            role = "new conservative interior photo mask; existing fixed3Dpier retained; no fit adjustment"
        case = {"id": f"artbox-terminal-pier-{number:06d}", "frame": number, "kind": "artbox_terminal_pier",
                "polygon_960": polygon, "historical_role": role,
                "fit": {"plane": {"normalUVY": plane[:3], "offset": plane[3]},
                        "supportedPolygonUVY": frontage["outlineUVY"], "dependentAxis": 1,
                        "uncertainty": {"notes": "Fixed outline from66; source registration and triangulation uncertainty preserved; all frames from same source video"}},
                "source": ".planning/2026-09-23-video-twin/entry-frontage-fit/result.json"}
        if number == 61:
            case["sample_exclusion"] = {"y_min_960": 235,
                    "reason": "Conservative exclusion of lower pier region near moving foreground passenger; visual review of recaptured61. Not a physical wall boundary."}
        result.append(case)
    return result


def plane_coverage(case, geo, tolerance=.15):
    fit = case["fit"]
    patch = np.asarray(fit["supportedPolygonUVY"])
    axis = fit["dependentAxis"]
    independent = [a for a in range(3) if a != axis]
    plane_n, plane_d = np.asarray(fit["plane"]["normalUVY"]), fit["plane"]["offset"]
    polygon = Polygon(patch[:, independent])
    triangles = world_to_uvy(geo["triangles"])
    lo, hi = patch.min(0) - .45, patch.max(0) + .45
    selected = np.flatnonzero((triangles.max(1) >= lo).all(1) & (triangles.min(1) <= hi).all(1))
    groups = {}
    for i in selected:
        triangle = triangles[i]
        projected = Polygon(triangle[:, independent])
        if not projected.is_valid or projected.area < 1e-9:
            continue
        clipped = projected.intersection(polygon)
        if clipped.is_empty or clipped.area < 1e-9:
            continue
        n = np.cross(triangle[1] - triangle[0], triangle[2] - triangle[0])
        if abs(n[axis]) < 1e-9:
            continue
        coords = []
        for part in ([clipped] if clipped.geom_type == "Polygon" else getattr(clipped, "geoms", [])):
            if part.geom_type == "Polygon":
                coords.extend(part.exterior.coords)
        if not coords:
            continue
        xyz = np.zeros((len(coords), 3)); xyz[:, independent] = coords
        xyz[:, axis] = (n @ triangle[0] - xyz[:, independent] @ n[independent]) / n[axis]
        signed = xyz @ plane_n + plane_d
        key = int(geo["owner"][i])
        group = groups.setdefault(key, {"projected": [], "coplanar": [], "signed_distances": [], "triangles": []})
        group["projected"].append(clipped)
        group["signed_distances"].extend(signed.tolist())
        group["triangles"].append(int(geo["ordinal"][i]))
        if np.max(np.abs(signed)) <= tolerance:
            group["coplanar"].append(clipped)
    rows, covered = [], []
    for owner, group in groups.items():
        area = unary_union(group["projected"]).area
        coplanar = unary_union(group["coplanar"]).area if group["coplanar"] else 0.
        covered += group["coplanar"]
        rows.append({"owner_index": owner, "owner": geo["owners"][owner]["owner"],
                     "projected_overlap_fraction": float(area / polygon.area),
                     "coplanar_overlap_fraction": float(coplanar / polygon.area),
                     "signed_plane_distance_source_units_min_median_max": [float(np.min(group["signed_distances"])),
                            float(np.median(group["signed_distances"])), float(np.max(group["signed_distances"]))],
                     "triangle_ordinals": sorted(set(group["triangles"]))})
    return {"patch_area_source_units_squared": polygon.area, "plane_tolerance_source_units": tolerance,
            "union_coplanar_coverage_fraction": float(unary_union(covered).area / polygon.area) if covered else 0.,
            "owners": sorted(rows, key=lambda r: -r["projected_overlap_fraction"]),
            "caution": "Coverage of finite old observed patch only; .15 tolerance is not as-built accuracy or full-surface completeness."}


def sample_case(case, camera, geo, tree, step=18):
    polygon = np.asarray(case["polygon_960"])
    mask = np.zeros((540, 960), np.uint8)
    cv2.fillPoly(mask, [polygon.astype(np.int32)], 1)
    mask = cv2.erode(mask, np.ones((13, 13), np.uint8))
    if case.get("sample_exclusion"):
        mask[case["sample_exclusion"]["y_min_960"]:, :] = 0
    yy, xx = np.mgrid[step // 2:540:step, step // 2:960:step]
    pixels = np.column_stack((xx[mask[yy, xx].astype(bool)], yy[mask[yy, xx].astype(bool)])).astype(float)
    rows = hits(pixels, camera, geo, tree)
    opaque = hits(pixels, camera, geo, tree, known_opaque_frontfaces=True)
    n, offset = np.asarray(case["fit"]["plane"]["normalUVY"]), case["fit"]["plane"]["offset"]
    directions = pixel_rays(pixels, camera)
    c_uvy = world_to_uvy(camera["position"])
    dir_uvy = world_to_uvy(directions)
    denom = dir_uvy @ n
    predicted_t = -(c_uvy @ n + offset) / denom
    for row, opaque_row, direction, t in zip(rows, opaque, directions, predicted_t):
        row["observed_plane_world"] = (np.asarray(camera["position"]) + direction * t).tolist() if t > 0 else None
        if row["hit"]:
            row["hit"]["signed_observed_plane_distance_source_units"] = float(np.asarray(row["hit"]["UVY"]) @ n + offset)
            row["hit"]["ray_distance_minus_observed_plane_source_units"] = float(row["hit"]["distance_source_units"] - t)
        row["known_opaque_frontface_hit"] = opaque_row["hit"]
        if row["known_opaque_frontface_hit"]:
            row["known_opaque_frontface_hit"]["signed_observed_plane_distance_source_units"] = float(np.asarray(opaque_row["hit"]["UVY"]) @ n + offset)
            row["known_opaque_frontface_hit"]["ray_distance_minus_observed_plane_source_units"] = float(opaque_row["hit"]["distance_source_units"] - t)
    matched = [r["hit"] for r in rows if r["hit"]]
    count = Counter(h["owner_index"] for h in matched)
    return {"sample_count": len(rows), "geometric_hits": len(matched), "sample_exclusion": case.get("sample_exclusion"),
            "misses_unresolved_bounded_export_volume": len(rows) - len(matched),
            "owner_hit_counts": [{"owner_index": owner, "owner": geo["owners"][owner]["owner"], "count": count[owner]} for owner in count],
            "samples": rows, "coverage": plane_coverage(case, geo)}


def font(size=19):
    path = Path("/System/Library/Fonts/Supplemental/Arial.ttf")
    return ImageFont.truetype(str(path), size) if path.exists() else ImageFont.load_default()


def overlay(case, result, cam, geo, out):
    image = Image.open(RECOVERED / f"frames/{case['frame']:06d}.png").convert("RGBA")
    layer = Image.new("RGBA", image.size)
    draw = ImageDraw.Draw(layer)
    polygon = np.asarray(case["polygon_960"]) * 2
    draw.line([tuple(p) for p in np.r_[polygon, polygon[:1]]], fill=(45, 255, 115, 255), width=4)
    projected, depth = project(uvy_to_world(case["fit"]["supportedPolygonUVY"]), cam)
    if (depth > 0).all():
        p = projected * 2
        draw.line([tuple(v) for v in np.r_[p, p[:1]]], fill=(255, 220, 60, 220), width=3)
    # Only a few exact hit faces, avoiding whole-scene wireframe ambiguity.
    primary = {r["owner_index"] for r in sorted(result["owner_hit_counts"], key=lambda r: -r["count"])[:3]}
    drawn = set()
    for row in result["samples"]:
        x, y = np.asarray(row["pixel_960"]) * 2
        hit = row["hit"]
        delta = hit["signed_observed_plane_distance_source_units"] if hit else None
        color = (60, 240, 255, 180) if delta is not None and abs(delta) <= .15 else (255, 70, 90, 180)
        draw.ellipse((x-3, y-3, x+3, y+3), fill=color)
        if hit and hit["owner_index"] in primary and hit["triangle_array_id"] not in drawn:
            i = hit["triangle_array_id"]
            p, z = project(geo["triangles"][i], cam)
            # Large faces may dominate the image; clip their lines to ROI later.
            if (z > .05).all() and np.max(np.abs(p)) < 5000:
                draw.line([tuple(v * 2) for v in np.r_[p, p[:1]]], fill=(255, 130, 80, 80), width=2)
                drawn.add(i)
    # Restrict all mesh edges and hit dots to a tight observed crop margin.
    roi = Image.new("L", image.size)
    roi_draw = ImageDraw.Draw(roi)
    bounds = (max(0, int(polygon[:,0].min())-30), max(0, int(polygon[:,1].min())-30),
              min(image.width, int(polygon[:,0].max())+30), min(image.height, int(polygon[:,1].max())+30))
    roi_draw.rectangle(bounds, fill=255)
    layer.putalpha(Image.fromarray(np.minimum(np.asarray(layer.getchannel("A")), np.asarray(roi))))
    image = Image.alpha_composite(image, layer).convert("RGB")
    panel = Image.new("RGB", (1920, 1160), "#171a21")
    panel.paste(image, (0, 0)); d = ImageDraw.Draw(panel)
    d.text((20, 1090), f"{case['id']} | green: old photo mask; yellow: retained patch; cyan: plane-close hit; red: other/no hit", font=font(22), fill="white")
    d.text((20, 1126), "Model-scale registration, not survey. Dots are geometric hits; shader transparency and people excluded. See report for owner / triangle ordinals.", font=font(20), fill="#c0c9d8")
    panel.save(out)


def distorted_render_pair(number, camera):
    render = ROOT / f".planning/2026-10-08-interior-runtime-completion/architecture/reference-views/{number:06d}-current-envelopes.png"
    receipt = Path(str(render) + ".json")
    if not render.exists() or not receipt.exists():
        return None
    capture = read(receipt)
    for key in ("position", "forward", "up", "intrinsics"):
        assert np.allclose(capture["camera"][key], camera[key], atol=1e-10), (number, key)
    raw = np.asarray(Image.open(render).convert("RGB"))
    assert raw.shape[:2] == (540, 960)
    yy, xx = np.mgrid[0:540, 0:960]
    pixels = np.c_[xx.ravel(), yy.ravel()]
    rays = pixel_rays(pixels, camera) @ camera_basis(camera).T
    f, cx, cy, _ = camera["intrinsics"]
    pinhole = rays[:, :2] / rays[:, 2, None] * f + [cx, cy]
    warped = cv2.remap(raw, pinhole[:, 0].reshape(540,960).astype(np.float32),
                       pinhole[:, 1].reshape(540,960).astype(np.float32), cv2.INTER_LINEAR)
    source = Image.open(RECOVERED / f"frames/{number:06d}.png").convert("RGB").resize((960,540), Image.Resampling.LANCZOS)
    pair = Image.new("RGB", (1920, 590), "#171a21"); pair.paste(source, (0,50)); pair.paste(Image.fromarray(warped), (960,50))
    d = ImageDraw.Draw(pair);d.text((15,15), f"{number:06d}: public March2026 source (distorted)", font=font(22), fill="white")
    d.text((975,15), "Current Unity render, warped by retained SIMPLE_RADIAL k1", font=font(22), fill="white")
    output = OUT / f"{number:06d}-source-current-distortion-matched.png"; pair.save(output)
    return {"output": rel(output), "unity_capture": rel(render), "unity_capture_sha256": sha(render),
            "unity_capture_receipt": capture, "lens_warp": "output source-distorted pixel -> inverse SIMPLE_RADIAL -> pinhole Unity render"}


def clip_halfspace(points, normal, offset):
    """Keep dot(normal,p)+offset>=0; exact convex polygon clipping."""
    points = np.asarray(points)
    if len(points) < 3:
        return np.empty((0,3))
    result = []
    for a, b in zip(points, np.roll(points,-1,axis=0)):
        da, db = a @ normal + offset, b @ normal + offset
        if da >= -1e-10:
            result.append(a)
        if (da > 1e-10 and db < -1e-10) or (da < -1e-10 and db > 1e-10):
            result.append(a + (b-a) * da / (da-db))
    return np.asarray(result).reshape(-1,3)


def face_barycentric(world, triangle):
    edges = (triangle[1:] - triangle[0]).T
    uv = np.linalg.lstsq(edges, (np.asarray(world) - triangle[0]).T, rcond=None)[0].T
    bary = np.column_stack((1-uv.sum(1),uv))
    assert np.max(np.abs(bary @ triangle - world)) < 1e-7
    assert bary.min() >= -1e-7 and bary.max() <= 1+1e-7
    return bary


def frontage_conflict_regions(spec, results, cameras, geo):
    pier_cases = [c for c in results if c["kind"] == "artbox_terminal_pier"]
    validated = {}
    for case in pier_cases:
        for sample in case["samples"]:
            hit = sample["known_opaque_frontface_hit"]
            if hit and hit["signed_observed_plane_distance_source_units"] > .15:
                i = hit["triangle_array_id"]
                validated.setdefault(i, set()).add(case["frame"])
    validated = {i: views for i, views in validated.items() if len(views)>=2}
    sourcefit = pier_cases[0]
    n_uvy = np.asarray(sourcefit["fit_plane"]["normalUVY"])
    d = sourcefit["fit_plane"]["offset"]
    n_world = SWAP @ ENU_UVY.T @ n_uvy
    fixed_world = uvy_to_world(sourcefit["supported_polygon_UVY"])
    rows = []
    for i, view_ids in sorted(validated.items()):
        triangle = geo["triangles"][i]
        owner = geo["owners"][int(geo["owner"][i])]
        inverse = np.linalg.inv(np.asarray(owner["world_matrix"]))
        regions = []
        for number in sorted(view_ids):
            case = next(c for c in spec if c["frame"]==number and c["kind"]=="artbox_terminal_pier")
            cam = cameras[f"{number:06d}.jpg"]
            mask = Polygon(case["polygon_960"])
            if case.get("sample_exclusion"):
                cutoff = case["sample_exclusion"]["y_min_960"]
                mask = mask.intersection(Polygon([[0,0],[960,0],[960,cutoff],[0,cutoff]]))
            projected, depths = project(fixed_world, cam)
            assert (depths > 0).all()
            mask = mask.intersection(Polygon(projected)).buffer(-8,join_style=2)
            if mask.is_empty or mask.geom_type != "Polygon":
                continue
            pixels = np.asarray(mask.exterior.coords[:-1])
            rays = pixel_rays(pixels,cam)
            centre = np.asarray(cam["position"])
            central_ray = np.mean(rays,axis=0)
            clipped = triangle.copy(); halfspaces = []
            for a,b in zip(rays,np.roll(rays,-1,axis=0)):
                normal = np.cross(a,b);normal /= np.linalg.norm(normal)
                if normal @ central_ray < 0:
                    normal *= -1
                offset = -normal @ centre
                halfspaces.append({"normal_world":normal.tolist(),"offset":float(offset)})
                clipped = clip_halfspace(clipped,normal,offset)
            forward = np.asarray(cam["forward"])
            clipped = clip_halfspace(clipped,forward,-forward @ centre-.03)
            side = 1 if centre @ n_world + d >= 0 else -1
            # Keep only clearly in-front faces, never the plane-close true pier.
            plane_offset = side*d-.15
            clipped = clip_halfspace(clipped,side*n_world,plane_offset)
            halfspaces.extend([{"normal_world":forward.tolist(),"offset":float(-forward@centre-.03)},
                               {"normal_world":(side*n_world).tolist(),"offset":float(plane_offset)}])
            if len(clipped)<3:
                continue
            bary = face_barycentric(clipped,triangle)
            local = clipped @ inverse[:3,:3].T + inverse[:3,3]
            region = Polygon(bary[:,1:])
            if not region.is_valid or region.area < 1e-10:
                continue
            regions.append({"frame":number,"source_camera":cam,"source_mask_inset_pixels_960":pixels.tolist(),
                            "frustum_and_depth_halfspaces_keep_positive":halfspaces,
                            "world_polygon":clipped.tolist(),"local_polygon":local.tolist(),
                            "source_triangle_barycentric_polygon":bary.tolist(),"_polygon":region})
        intersections = []
        for a,b in combinations(regions,2):
            intersection = a["_polygon"].intersection(b["_polygon"])
            if not intersection.is_empty and intersection.area > 1e-9:
                intersections.append({"frames":[a["frame"],b["frame"]],
                                      "source_triangle_barycentric_1_2_geometry":mapping(intersection)})
        if not intersections:
            continue
        from shapely.geometry import shape
        confirmed = unary_union([shape(r["source_triangle_barycentric_1_2_geometry"]) for r in intersections])
        # Additional preservation guard: a clipped point must remain inside the
        # fixed pier outline cone in ALL four source views. This protects a real
        # neighboring fascia seen in another view, even if two masks overlap it.
        guarded = triangle.copy()
        for number in (57,61,66,68):
            cam = cameras[f"{number:06d}.jpg"]
            pix,z = project(fixed_world,cam)
            contour = Polygon(pix).buffer(-8,join_style=2)
            assert not contour.is_empty and contour.geom_type == "Polygon"
            rays = pixel_rays(np.asarray(contour.exterior.coords[:-1]),cam)
            centre = np.asarray(cam["position"])
            centre_ray = rays.mean(0)
            for a,b in zip(rays,np.roll(rays,-1,axis=0)):
                normal = np.cross(a,b); normal/=np.linalg.norm(normal)
                if normal@centre_ray<0:
                    normal*=-1
                guarded = clip_halfspace(guarded,normal,-normal@centre)
        guarded_polygon = Polygon(face_barycentric(guarded,triangle)[:,1:]) if len(guarded)>=3 else Polygon()
        strict = confirmed.intersection(guarded_polygon)
        assert 0 <= strict.area <= confirmed.area+1e-9 <= .5+1e-9
        strict_world = []
        for poly in ([strict] if strict.geom_type=="Polygon" else getattr(strict,"geoms",[])):
            if poly.is_empty or poly.geom_type != "Polygon":
                continue
            rings=[]
            for ring in [poly.exterior,*poly.interiors]:
                uv=np.asarray(ring.coords);bary=np.c_[1-uv.sum(1),uv]
                world=bary@triangle
                check_world = np.concatenate([world, (world+np.roll(world,-1,axis=0))/2])
                for number in (57,61,66,68):
                    cam=cameras[f"{number:06d}.jpg"]
                    pix,z=project(check_world,cam)
                    feature_pix,_=project(fixed_world,cam)
                    feature_polygon=Polygon(feature_pix).buffer(.1)
                    assert (z>0).all() and all(feature_polygon.covers(Point(p)) for p in pix), "Strict clip escapes fixed feature in another source view"
                rings.append({"barycentric":bary.tolist(),"world":world.tolist(),
                              "local":(world@inverse[:3,:3].T+inverse[:3,3]).tolist()})
            strict_world.append({"rings":rings})
        base_triangle=Polygon([[0.,0.],[1.,0.],[0.,1.]])
        remainder=base_triangle.difference(strict)
        kept_uv, kept_weights, kept_polygons = [], [], []
        omitted_sliver_count, omitted_sliver_area = 0, 0.
        omitted_float32_count, omitted_float32_area = 0, 0.
        float32_signed_areas=[]
        local_triangle32=(triangle@inverse[:3,:3].T+inverse[:3,3]).astype(np.float32)
        local_source_normal32=np.cross(local_triangle32[1]-local_triangle32[0],local_triangle32[2]-local_triangle32[0])
        source_normal=np.cross(triangle[1]-triangle[0],triangle[2]-triangle[0])
        if not remainder.is_empty:
            # GEOS constrained Delaunay respects every boundary and hole. No
            # triangulation of a convex hull, centroid filter or hole filling.
            tessellation=shapely.constrained_delaunay_triangles(remainder)
            for face in tessellation.geoms:
                assert face.geom_type=="Polygon"
                uv=np.asarray(face.exterior.coords[:-1],dtype=np.float64)
                assert uv.shape==(3,2) and not face.interiors
                signed=(uv[1,0]-uv[0,0])*(uv[2,1]-uv[0,1])-(uv[1,1]-uv[0,1])*(uv[2,0]-uv[0,0])
                if abs(signed)<=1e-12:
                    omitted_sliver_count+=1;omitted_sliver_area+=abs(signed)/2
                    continue
                if signed<0:
                    uv=uv[[0,2,1]]
                uv32=uv.astype(np.float32)
                d1,d2=uv32[1]-uv32[0],uv32[2]-uv32[0]
                # Exact C# preflight: float32 differences, double products.
                signed32=float(d1[0])*float(d2[1])-float(d1[1])*float(d2[0])
                weights32=np.c_[np.float32(1)-uv32[:,0]-uv32[:,1],uv32].astype(np.float32)
                local32=(local_triangle32[0]*weights32[:,0,None]+local_triangle32[1]*weights32[:,1,None]+local_triangle32[2]*weights32[:,2,None]).astype(np.float32)
                normal32=np.cross(local32[1]-local32[0],local32[2]-local32[0])
                actual_local_winding=float(normal32.astype(np.float64)@local_source_normal32.astype(np.float64))
                if signed32<=1e-12 or actual_local_winding<=0:
                    omitted_float32_count+=1;omitted_float32_area+=abs(signed)/2
                    continue
                float32_signed_areas.append(signed32)
                kept=Polygon(uv)
                assert kept.difference(base_triangle).area<1e-12
                assert kept.intersection(strict).area<1e-12, "Kept face crosses strict cut"
                bary=np.c_[1-uv.sum(1),uv]
                assert bary.min()>=-1e-9 and bary.max()<=1+1e-9
                world=bary@triangle
                kept_normal=np.cross(world[1]-world[0],world[2]-world[0])
                assert kept_normal@source_normal>0, "Remainder reverses source winding"
                kept_uv.append(uv.tolist());kept_weights.append(bary.tolist());kept_polygons.append(kept)
        union=unary_union(kept_polygons) if kept_polygons else Polygon()
        summed_area=sum(p.area for p in kept_polygons)
        coverage_residual=float(union.symmetric_difference(remainder).area)
        area_residual=float(abs(summed_area+strict.area-.5))
        overlap_residual=float(abs(summed_area-union.area))
        float32_fraction_residual=float(abs(sum(float32_signed_areas)-(1-strict.area/.5)))
        assert max(coverage_residual,area_residual,overlap_residual)/.5<=1e-5, "Remainder tessellation loses/fills/overlaps geometry"
        assert float32_fraction_residual<=1e-5, "Unity float32 preflight area fraction mismatch"
        for region in regions:
            region.pop("_polygon")
        rows.append({**owner,"triangle_ordinal":int(geo["ordinal"][i]),
                     "vertex_indices":geo["vertices"][i].tolist(),
                     "original_triangle_vertices_world":triangle.tolist(),
                     "original_triangle_vertices_local":(triangle@inverse[:3,:3].T+inverse[:3,3]).tolist(),
                     "direct_nearest_opaque_conflict_frames":sorted(view_ids),
                     "per_view_frustum_clipped_regions":regions,
                     "two_view_intersections":intersections,
                     "confirmed_conflict_union_barycentric_1_2_geometry":mapping(confirmed),
                     "confirmed_conflict_original_triangle_area_fraction":float(confirmed.area/.5),
                     "preserve_original_triangle_area_fraction":float(1-confirmed.area/.5),
                     "strict_all_four_fixed_pier_frustum_guard_geometry":mapping(strict),
                     "strict_guarded_conflict_original_triangle_area_fraction":float(strict.area/.5),
                     "strict_preserve_original_triangle_area_fraction":float(1-strict.area/.5),
                     "strict_guarded_conflict_polygons":strict_world,
                     "strict_remaining_barycentric_triangles":kept_uv,
                     "strict_remaining_barycentric_weights":kept_weights,
                     "strict_remaining_barycentric_coordinate_convention":"Each vertex[u,v] is original index1/index2 weight; original index0 weight=1-u-v. Positive signed area preserves original triangle winding.",
                     "strict_remainder_verification":{"method":"Shapely constrained_delaunay_triangles(original base triangle minus strict cut), boundaries and holes preserved",
                            "shapely_version":shapely.__version__,"geos_version":shapely.geos_version_string,
                            "kept_triangle_count":len(kept_uv),"base_triangle_barycentric_area":.5,
                            "strict_cut_barycentric_area":float(strict.area),"kept_barycentric_area":float(summed_area),
                            "area_conservation_residual":area_residual,"coverage_symmetric_difference_area":coverage_residual,
                            "omitted_sliver_signed_doubled_area_tolerance":1e-12,
                            "omitted_sliver_count":omitted_sliver_count,"omitted_sliver_barycentric_area":omitted_sliver_area,
                            "omitted_float32_collapsed_or_reversed_sliver_count":omitted_float32_count,
                            "omitted_float32_sliver_barycentric_area":omitted_float32_area,
                            "float32_minimum_signed_doubled_barycentric_area":min(float32_signed_areas) if float32_signed_areas else None,
                            "float32_preflight_area_fraction_residual":float32_fraction_residual,
                            "maximum_conservation_fraction_tolerance":1e-5,
                            "float32_actual_source_local_interpolated_winding_preserved":True,
                            "kept_triangle_overlap_area":overlap_residual,"kept_triangles_do_not_cross_cut":True,
                            "all_kept_triangles_preserve_original_winding":True},
                     "preferred_cut_contract":"Use strict_all_four_fixed_pier_frustum_guard_geometry only. Empty means keep original face. Two-view union alone is a diagnostic candidate, not the recommended cut.",
                     "UV_normals_preservation": "For each clip vertex barycentric[a,b,c], interpolate all original vertex attributes from original vertex_indices; preserve material/submesh. Do not delete whole face or owner."})
    return {"schema":"chooguard.bounded-frontage-visibility-conflict.v1","site_accuracy_verified":False,
            "frames_checked":[57,61,66,68],"fixed_pier_outline_UVY":sourcefit["supported_polygon_UVY"],
            "fixed_pier_plane":sourcefit["fit_plane"],"repair_geometry_is_candidate_only":True,
            "method":"Known single-material opaque frontface nearest hits contradicted in>=2sourceviews; intersect original face with both conservative source camera frusta, inset8pixels and stop.15modelunit before retained pier plane.",
            "source_views_are_independent_acquisitions":False,
            "preferred_cut_geometry_field":"strict_all_four_fixed_pier_frustum_guard_geometry",
            "candidate_two_view_union_must_not_be_used_as_whole_face_removal":True,
            "verification":{"strict_vertices_and_edge_midpoints_inside_original_feature_in_all_four_source_views":True,
                            "barycentric_reconstruction_and_original_face_conservation_checked":True,
                            "strict_remainder_constrained_tessellation_area_holes_no_cut_crossing_winding_verified":True,
                            "Unity_float32_weights_preflight_area_and_actual_source_local_interpolated_winding_verified":True},
            "unaffected_face_contract":"Only listed faces with nonempty strict geometry receive this remainder tessellation. Copy every unaffected original face index, vertex attribute and submesh/material exactly.",
            "preserve":["Ground_Unit_4916_Walls existing true pier", "plane-close Kit_ShopWall_벽_내장", "all glass (excluded from opaque conflict)", "all neighboring real fascia, jambs and front glazing outside confirmed polygon union"],
            "cut_scope":"Only two-view intersection union on original face; not whole triangle, renderer, storefront or collider.",
            "faces":rows}


def conflict_overlays(conflicts,cameras):
    outputs=[]
    for number in conflicts["frames_checked"]:
        cam=cameras[f"{number:06d}.jpg"]
        image=Image.open(RECOVERED/f"frames/{number:06d}.png").convert("RGBA")
        layer=Image.new("RGBA",image.size);draw=ImageDraw.Draw(layer)
        for face in conflicts["faces"]:
            for polygon in face["strict_guarded_conflict_polygons"]:
                p,z=project(polygon["rings"][0]["world"],cam)
                assert (z>0).all()
                pixels=[tuple(x*2) for x in p]
                draw.polygon(pixels,fill=(255,35,160,75),outline=(255,100,200,230),width=2)
        image=Image.alpha_composite(image,layer).convert("RGB")
        p,z=project(uvy_to_world(conflicts["fixed_pier_outline_UVY"]),cam)
        box=(max(0,int(p[:,0].min()*2)-45),max(0,int(p[:,1].min()*2)-45),
             min(1920,int(p[:,0].max()*2)+45),min(1080,int(p[:,1].max()*2)+45))
        crop=image.crop(box)
        scale=min(2.,1100/crop.height)
        crop=crop.resize((int(crop.width*scale),int(crop.height*scale)),Image.Resampling.LANCZOS)
        panel=Image.new("RGB",(max(660,crop.width),crop.height+90),"#171a21")
        panel.paste(crop,((panel.width-crop.width)//2,90)); d=ImageDraw.Draw(panel)
        d.text((12,12),f"{number:06d}: bounded ARTBOX frontage clip candidate",font=font(22),fill="white")
        d.text((12,44),"Magenta: source-face region inside all four fixed-pier frusta",font=font(18),fill="white")
        d.text((12,66),"Reference model scale; keep real fascia/jamb/glass outside region",font=font(17),fill="#c0c9d8")
        output=OUT/f"{number:06d}-artbox-strict-conflict-crop.png";panel.save(output);outputs.append(rel(output))
    return outputs


def main():
    parser = argparse.ArgumentParser(); parser.add_argument("--scene", type=Path, default=DEFAULT_SCENE)
    args = parser.parse_args();OUT.mkdir(parents=True, exist_ok=True)
    cameras = read(OLD / "metric-registration/registered-cameras.json")
    verification = verify_cameras(cameras)
    scene = read(args.scene); scene_hash = sha(args.scene)
    geo = geometry(scene)
    source_map = read(RECOVERED / "source-map.json")
    for receipt in source_map["frames"]:
        if receipt["frame"] in FRAMES:
            assert sha(ROOT / receipt["path"]) == receipt["sha256"], receipt["path"]
    report = {"schema": "chooguard.registered-structural-pixel-comparison.v1", "site_accuracy_verified": False,
              "worker_sha256": sha(__file__), "scene_export": rel(args.scene), "scene_export_sha256": scene_hash,
              "scene_file_sha256": scene["scene_file_sha256"], "scene_sampled_utc": scene["sampled_utc"],
              "camera_verification": verification, "source_video_url": source_map["source_video_url"],
              "frame_capture_receipts": [r for r in source_map["frames"] if r["frame"] in FRAMES],
              "finite_geometry_volume_world": [VOLUME_LO.tolist(), VOLUME_HI.tolist()],
              "included_triangles": len(geo["triangles"]), "included_owners": len(geo["owners"]),
              "omitted_unreadable_renderers": geo["omitted_unreadable_renderers"],
              "method": "Exact exported matrix/indices, SIMPLE_RADIAL projection, finite photo masks, double-sided first-hit rays and clipped plane overlap. No old mesh-coverage results reused.",
              "limits": ["Source metric registration inherits original model scale, not survey; held-out plumb disagreement up to0.65deg.",
                         "Recaptured source PNGs are not historical JPEG/container bytes; seek quantization up to0.031667s.",
                         "Masked polygons select observed interiors, not surveyed boundaries. Geometric hits do not evaluate shader transparency/backface rendering.",
                         "Finite volume and unreadable actual fixture meshes make geometric misses unresolved; no substitute boxes created.",
                         "B&C renewal hoarding is temporary and is excluded from permanent wall reconstruction.",
                         "Coplanar zero alone is not proof of a missing ceiling: nearby offsets/current visible finish must be inspected."]}
    spec = cases(); results = []
    for number in FRAMES:
        camera = cameras[f"{number:06d}.jpg"]
        tree, candidates = projection_index(geo["triangles"], camera)
        print(f"frame{number}: indexed{candidates} faces", flush=True)
        for case in [s for s in spec if s["frame"] == number]:
            result = sample_case(case, camera, geo, tree)
            result.update({"id": case["id"], "frame": number, "kind": case["kind"], "source_fit": case["source"],
                           "polygon_960": case["polygon_960"], "historical_observation_role": case["historical_role"],
                           "fit_uncertainty": case["fit"].get("uncertainty"), "fit_plane": case["fit"]["plane"],
                           "supported_polygon_UVY": case["fit"]["supportedPolygonUVY"]})
            output = OUT / f"{case['id']}-overlay.png"; overlay(case, result, camera, geo, output)
            result["overlay"] = rel(output);results.append(result)
            print(f" {case['id']}: samples{result['sample_count']} closeCoverage{result['coverage']['union_coplanar_coverage_fraction']:.4f}", flush=True)
        tree.close()
    report["owners"] = geo["owners"]
    report["cases"] = results
    pixel_errors, face_distances = [], []
    for case in results:
        cam = cameras[f"{case['frame']:06d}.jpg"]
        for sample in case["samples"]:
            for key in ("hit", "known_opaque_frontface_hit"):
                hit = sample[key]
                if hit is None:
                    continue
                p, z = project(np.asarray([hit["world"]]), cam)
                pixel_errors.append(float(np.linalg.norm(p[0] - sample["pixel_960"])))
                tri = geo["triangles"][hit["triangle_array_id"]]
                normal = np.cross(tri[1] - tri[0], tri[2] - tri[0])
                face_distances.append(float(abs((np.asarray(hit["world"]) - tri[0]) @ normal) / np.linalg.norm(normal)))
    assert max(pixel_errors) < 1e-7, "Hit reprojects to a different source pixel"
    assert max(face_distances) < 1e-7, "Hit is not on its exported owner triangle"
    report["verification"] = {"exact_triangle_hits_checked": len(pixel_errors),
                              "maximum_hit_reprojection_error_pixels_960": max(pixel_errors),
                              "maximum_hit_exported_face_distance_source_units": max(face_distances),
                              "mesh_binary_sha256_verified": True, "recovered_source_png_sha256_verified": True,
                              "no_scene_mutation": True, "visual_inspection_is_separate": True}
    report["lens_matched_render_pairs"] = [pair for n in (61,106) if (pair := distorted_render_pair(n,cameras[f"{n:06d}.jpg"]))]
    conflicts = frontage_conflict_regions(spec,results,cameras,geo)
    conflicts.update({"scene_export":rel(args.scene),"scene_export_sha256":scene_hash,
                      "scene_file_sha256":scene["scene_file_sha256"],"worker_sha256":sha(__file__)})
    conflicts["strict_clip_overlays"]=conflict_overlays(conflicts,cameras)
    conflicts["strict_nonempty_faces"]=sum(f["strict_guarded_conflict_original_triangle_area_fraction"]>1e-9 for f in conflicts["faces"])
    save(OUT/"artbox-frontage-conflict-clips.json",conflicts)
    report["bounded_frontage_conflict_clips"] = {"path":rel(OUT/"artbox-frontage-conflict-clips.json"),
                                               "face_count":len(conflicts["faces"]),"sha256":sha(OUT/"artbox-frontage-conflict-clips.json")}
    assert sha(args.scene) == scene_hash, "Scene export changed during analysis; rerun on a stable export"
    save(OUT / "comparison.json", report)
    print("saved " + rel(OUT / "comparison.json"), flush=True)


if __name__ == "__main__":
    main()
