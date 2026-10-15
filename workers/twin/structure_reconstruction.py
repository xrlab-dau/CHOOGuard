"""Extract observed architectural plane fragments; never fabricate unseen walls.

Input is retained OpenMVS photographed triangles in an arbitrary COLMAP frame.
All distances/areas are relative units. Public media is not a metric survey.
The derived plane mesh preserves selected original triangle topology and UVs.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import struct
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "workers/twin/output/interior-blocks-20261008"
OUTPUT = ROOT / "workers/twin/output/interior-structure-20261008"
EVIDENCE = ROOT / "workers/twin/evidence/interior-structure-20261008"
BLOCKS = ["retail-corner", "food-court", "ticketing-wall", "retail-corridor",
          "information-corridor-static", "corridor-connector"]


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n")


def load_obj(path):
    vertices, uv, faces, texture_faces = [], [], [], []
    for line in path.read_text().splitlines():
        words = line.split()
        if not words:
            continue
        if words[0] == "v":
            vertices.append([float(v) for v in words[1:4]])
        elif words[0] == "vt":
            uv.append([float(v) for v in words[1:3]])
        elif words[0] == "f":
            assert len(words) == 4, "Retained source must be triangular"
            refs = [v.split("/") for v in words[1:]]
            faces.append([int(v[0]) - 1 for v in refs])
            texture_faces.append([int(v[1]) - 1 for v in refs])
    return (np.array(vertices), np.array(uv), np.array(faces, dtype=np.int32),
            np.array(texture_faces, dtype=np.int32))


def load_dense(path):
    """Read actual variable OpenMVS view lists, without a fabricated camera map."""
    with path.open("rb") as stream:
        header = []
        while True:
            line = stream.readline().decode("ascii").strip()
            header.append(line)
            if line == "end_header":
                break
        assert "format binary_little_endian 1.0" in header
        count = int(next(s.split()[-1] for s in header if s.startswith("element vertex ")))
        expected = ["property float32 x", "property float32 y", "property float32 z",
                    "property uint8 red", "property uint8 green", "property uint8 blue",
                    "property float32 nx", "property float32 ny", "property float32 nz",
                    "property list uint8 uint32 view_indices", "property list uint8 float32 view_weights"]
        assert [s for s in header if s.startswith("property ")] == expected
        positions = np.empty((count, 3)); colours = np.empty((count, 3), dtype=np.uint8)
        views = []
        for i in range(count):
            row = stream.read(27); assert len(row) == 27
            positions[i] = struct.unpack_from("<fff", row)
            colours[i] = list(row[12:15])
            n = stream.read(1)[0]; view_bytes = stream.read(4 * n)
            assert len(view_bytes) == 4 * n
            views.append(np.frombuffer(view_bytes, dtype="<u4").copy())
            w = stream.read(1)[0]; assert len(stream.read(4 * w)) == 4 * w
        assert not stream.read(1)
    assert np.isfinite(positions).all()
    return positions, colours, views


def write_cloud(path, points, colours):
    dtype = np.dtype([("x", "<f4"), ("y", "<f4"), ("z", "<f4"),
                      ("red", "u1"), ("green", "u1"), ("blue", "u1")])
    rows = np.empty(len(points), dtype=dtype)
    for i, axis in enumerate("xyz"):
        rows[axis] = points[:, i]
    for i, channel in enumerate(["red", "green", "blue"]):
        rows[channel] = colours[:, i]
    header = ("ply\nformat binary_little_endian 1.0\n" + f"element vertex {len(points)}\n" +
              "property float x\nproperty float y\nproperty float z\n" +
              "property uchar red\nproperty uchar green\nproperty uchar blue\nend_header\n")
    path.write_bytes(header.encode() + rows.tobytes())


def plane_fit(points, weights):
    centre = np.average(points, axis=0, weights=weights)
    delta = points - centre
    cov = (delta.T * weights) @ delta / weights.sum()
    vals, vectors = np.linalg.eigh(cov)
    normal = vectors[:, 0]
    return normal, -normal @ centre, centre, vals


def angle_degrees(a, b):
    return float(np.rad2deg(np.arccos(np.clip(abs(a @ b), 0, 1))))


def points_in_observed_footprint(points, triangles, normal, offset):
    """Conservative projected-point inclusion in retained triangle footprints.

    Approximate 16-nearest triangle centres may miss large thin triangles but
    cannot create an unobserved footprint: every accepted point passes an
    actual triangle barycentric inclusion check. No hull/rectangle is used.
    """
    if not len(points):
        return np.zeros(0, dtype=bool)
    t = triangles - (triangles @ normal + offset)[:, :, None] * normal
    q = points - (points @ normal + offset)[:, None] * normal
    index = cv2.flann_Index(t.mean(axis=1).astype(np.float32), {"algorithm": 1, "trees": 4})
    nearest, _ = index.knnSearch(q.astype(np.float32), min(16, len(t)), params={"checks": 64})
    selected = t[nearest]
    a = selected[:, :, 1] - selected[:, :, 0]
    b = selected[:, :, 2] - selected[:, :, 0]
    c = q[:, None, :] - selected[:, :, 0]
    aa = np.sum(a*a, axis=2); ab = np.sum(a*b, axis=2); bb = np.sum(b*b, axis=2)
    ac = np.sum(a*c, axis=2); bc = np.sum(b*c, axis=2)
    den = aa*bb-ab*ab; safe = np.maximum(den, 1e-20)
    u = (bb*ac-ab*bc)/safe; v = (aa*bc-ab*ac)/safe
    return ((den > 1e-16) & (u >= 0) & (v >= 0) & (u+v <= 1)).any(axis=1)


def fragment_obj(path, vertices, uv, faces, texture_faces, face_ids, normal, offset):
    selected = faces[face_ids]
    used, inverse = np.unique(selected, return_inverse=True)
    raw = vertices[used]
    projected = raw - (raw @ normal + offset)[:, None] * normal
    tf = texture_faces[face_ids]
    tex_used, tex_inverse = np.unique(tf, return_inverse=True)
    with path.open("w") as stream:
        stream.write("# reference_only; observed triangles projected to fitted plane; unknown scale\n")
        stream.write("mtllib photographic.mtl\n")
        for v in projected:
            stream.write("v " + " ".join(f"{p:.9g}" for p in v) + "\n")
        for t in uv[tex_used]:
            stream.write("vt " + " ".join(f"{p:.9g}" for p in t) + "\n")
        stream.write("usemtl photographic\n")
        for f, t in zip(inverse.reshape(-1, 3), tex_inverse.reshape(-1, 3)):
            stream.write("f " + " ".join(f"{a + 1}/{b + 1}" for a, b in zip(f, t)) + "\n")
    assert np.isfinite(projected).all()
    assert np.abs(projected @ normal + offset).max() < 1e-8
    edges = np.sort(np.vstack([selected[:, [0, 1]], selected[:, [1, 2]], selected[:, [2, 0]]]), axis=1)
    distinct, counts = np.unique(edges, axis=0, return_counts=True)
    boundary = distinct[counts == 1]
    # Boundary segments are observed mesh boundaries, not automatic architectural openings.
    return projected, used, boundary


def extract(block, max_planes=14):
    src = SOURCE / block; dest = OUTPUT / block; dest.mkdir(parents=True, exist_ok=True)
    original = src / "reference_observed_surfaces.obj"
    vertices, uv, faces, texture_faces = load_obj(original)
    tri = vertices[faces]; centres = tri.mean(axis=1)
    cross = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    twice_area = np.linalg.norm(cross, axis=1)
    normals = cross / np.maximum(twice_area[:, None], 1e-15)
    areas = twice_area * .5
    poses = json.loads((src / "camera-poses.relative.json").read_text())["cameras"]
    camera_centres = np.array([p["projection_centre"] for p in poses])
    camera_ups = np.array([-np.array(p["world_from_camera_cv"])[:, 1] for p in poses])
    up = camera_ups.mean(axis=0); up /= np.linalg.norm(up)
    diag = float(np.linalg.norm(np.ptp(vertices, axis=0)))
    tau = diag * .0015
    rng = np.random.default_rng(20261008)
    samples = rng.choice(len(faces), min(12000, len(faces)), replace=False, p=areas / areas.sum())
    # Weighted sampling already represents area; candidate score uses equal sampled weights.
    cp, cn = centres[samples], normals[samples]
    alive = np.ones(len(faces), dtype=bool)
    dp, dc, dv = load_dense(src / "scene_dense.ply")
    texture = src / "reference_photographic_material_00_map_Kd.jpg"
    shutil.copyfile(texture, dest / "photographic.jpg")
    (dest / "photographic.mtl").write_text("newmtl photographic\nKd 1 1 1\nd 1\nmap_Kd photographic.jpg\n")
    planes = []
    cos_tol = np.cos(np.deg2rad(35))
    for iteration in range(max_planes):
        active = alive[samples]
        choices = np.flatnonzero(active)
        if len(choices) < 100:
            break
        seeds = rng.choice(choices, min(280, len(choices)), replace=False)
        candidate_n = cn[seeds]
        upright = np.abs(candidate_n @ up)
        axis_candidates = (upright < .35) | (upright > .87)
        candidate_n = candidate_n[axis_candidates]; seeds = seeds[axis_candidates]
        if not len(seeds):
            break
        offsets = -np.sum(candidate_n * cp[seeds], axis=1)
        distance = np.abs(cp @ candidate_n.T + offsets)
        alignment = np.abs(cn @ candidate_n.T)
        score = ((distance < tau) & (alignment > cos_tol) & active[:, None]).sum(axis=0)
        best = int(np.argmax(score)); n = candidate_n[best]; d = offsets[best]
        ids = np.flatnonzero(alive & (np.abs(centres @ n + d) < tau) & (np.abs(normals @ n) > cos_tol))
        for _ in range(5):
            if len(ids) < 100:
                break
            n2, d2, centre, eigenvalues = plane_fit(centres[ids], areas[ids])
            if n2 @ n < 0:
                n2 = -n2; d2 = -d2
            n, d = n2, d2
            ids = np.flatnonzero(alive & (np.abs(centres @ n + d) < tau) &
                                 (np.abs(normals @ n) > cos_tol) &
                                 (np.abs(tri @ n + d).max(axis=1) < 2.5 * tau))
        if len(ids) < 100 or areas[ids].sum() < areas.sum() * .008:
            # Remove the failed weak seed support so later candidates can be considered.
            alive[ids] = False
            continue
        n, d, centre, eigenvalues = plane_fit(centres[ids], areas[ids])
        if n @ (np.median(camera_centres, axis=0) - centre) < 0:
            n = -n; d = -d
        residual = centres[ids] @ n + d
        vertex_residual = tri[ids] @ n + d
        ptype = "vertical_wall_storefront_or_column_face_candidate"
        dotup = abs(n @ up)
        height = float((centre - np.median(camera_centres, axis=0)) @ up)
        if dotup > .87:
            ptype = "upper_horizontal_ceiling_or_soffit_candidate" if height > 0 else "lower_horizontal_floor_or_counter_candidate"
        elif dotup >= .35:
            ptype = "inclined_planar_surface_candidate"
        plane_id = f"plane-{len(planes) + 1:02d}"
        projected, used, boundary = fragment_obj(dest / (plane_id + ".obj"), vertices, uv, faces,
                                                texture_faces, ids, n, d)
        # Bounding box is only a fast rejection, followed by actual triangle inclusion.
        lo = vertices[used].min(axis=0) - 2 * tau; hi = vertices[used].max(axis=0) + 2 * tau
        point_ids = np.flatnonzero((np.abs(dp @ n + d) < 2 * tau) & (dp >= lo).all(axis=1) & (dp <= hi).all(axis=1))
        inside = points_in_observed_footprint(dp[point_ids], tri[ids], n, d)
        point_ids = point_ids[inside]
        write_cloud(dest / (plane_id + ".support.ply"), dp[point_ids], dc[point_ids])
        view_counts = np.zeros(len(poses), dtype=int)
        for p in point_ids:
            view_ids = dv[p]
            assert np.all(view_ids < len(poses)), "Unexpected retained OpenMVS camera index"
            view_counts[view_ids] += 1
        # The OpenMVS InterfaceCOLMAP image list is exported in image-id order (not name order).
        # Preserve explicit MVS index only; mapping to image names requires separate verification.
        bootstrap = []
        for _ in range(64):
            pick = rng.choice(len(ids), min(3000, len(ids)), replace=True, p=areas[ids] / areas[ids].sum())
            bn, bd, _, _ = plane_fit(centres[ids[pick]], np.ones(len(pick)))
            if bn @ n < 0:
                bn = -bn; bd = -bd
            bootstrap.append([angle_degrees(bn, n), abs(float((bn @ centre + bd)))])
        # Existing topology boundaries exported as lines for Blender review. Never label holes doors.
        bpath = dest / (plane_id + ".boundary.obj")
        with bpath.open("w") as stream:
            stream.write("# observed patch boundary; architectural opening identity unverified\n")
            for v in vertices[used]:
                stream.write("v " + " ".join(f"{x:.9g}" for x in v) + "\n")
            index = {int(old): i + 1 for i, old in enumerate(used)}
            for a, b in boundary:
                stream.write(f"l {index[int(a)]} {index[int(b)]}\n")
        write_json(dest / (plane_id + ".source-face-ids.json"), {"source_obj_sha256": digest(original), "face_indices_zero_based": ids.tolist()})
        row = {"id": plane_id, "classification": ptype, "classification_is_candidate": True,
               "equation_relative": {"normal": n.tolist(), "offset": float(d), "equation": "dot(normal, XYZ) + offset = 0"},
               "centre_relative": centre.tolist(), "up_alignment_abs": float(dotup),
               "centre_height_above_median_camera_relative": height,
               "observed_triangle_count": len(ids), "observed_vertex_count": len(used),
               "observed_original_area_relative_squared": float(areas[ids].sum()),
               "observed_area_fraction_of_input": float(areas[ids].sum() / areas.sum()),
               "observed_boundary_segment_count": len(boundary),
               "new_unobserved_faces_created": 0,
               "plane_fragment_is_projected_observed_triangles": True,
               "uncertainty_relative": {"face_centre_rms": float(np.sqrt(np.average(residual**2, weights=areas[ids]))),
                    "face_centre_median_abs": float(np.median(abs(residual))),
                    "face_centre_p95_abs": float(np.percentile(abs(residual), 95)),
                    "vertex_p95_projection_displacement": float(np.percentile(abs(vertex_residual), 95)),
                    "vertex_max_projection_displacement": float(abs(vertex_residual).max()),
                    "bootstrap_normal_p95_deg": float(np.percentile(np.array(bootstrap)[:, 0], 95)),
                    "bootstrap_centre_offset_p95": float(np.percentile(np.array(bootstrap)[:, 1], 95)),
                    "bootstrap_is_conditional_model_repeatability_not_site_accuracy": True},
               "dense_support": {"retained_points": len(point_ids),
                    "selection": "within 2 distance thresholds and actual projected observed triangle barycentric footprint",
                    "conservative_approximate_nearest_triangle_count": 16,
                    "median_original_view_count": float(np.median([len(dv[i]) for i in point_ids])) if len(point_ids) else None,
                    "mvs_view_indices_with_support": np.flatnonzero(view_counts).tolist(),
                    "mvs_view_point_counts": view_counts.tolist(),
                    "view_index_to_name_not_assumed": True},
               "artifacts": {"mesh": str((dest / (plane_id + ".obj")).relative_to(ROOT)),
                             "points": str((dest / (plane_id + ".support.ply")).relative_to(ROOT)),
                             "boundary": str(bpath.relative_to(ROOT))},
               "limitations": ["Coplanar points can include separate nearby surfaces and reflections.",
                               "Observed mesh boundaries do not establish a door/opening.",
                               "No depth/extrusion or missing footprint was synthesized."]}
        planes.append(row); alive[ids] = False
    assert planes
    source_hashes = {str(p.relative_to(ROOT)): digest(p) for p in [original, src / "scene_dense.ply", src / "camera-poses.relative.json", texture]}
    result = {"schema": "chooguard.observed-structural-planes.v1", "block": block,
              "role": "reference_only", "metric_verified": False, "placement_export_allowed": False,
              "distance_unit": "arbitrary retained COLMAP relative unit", "physical_accuracy_m": None,
              "source_hashes": source_hashes,
              "input": {"vertices": len(vertices), "triangles": len(faces), "area_relative_squared": float(areas.sum()),
                        "retained_camera_count": len(poses), "dense_point_count": len(dp)},
              "orientation": {"up_relative": up.tolist(), "basis": "mean retained camera image-up; not surveyed gravity",
                              "camera_up_max_deviation_deg": float(np.rad2deg(np.arccos(np.clip(camera_ups @ up, -1, 1))).max())},
              "extraction": {"random_seed": 20261008, "plane_distance_threshold_relative": tau,
                             "threshold_basis": "0.0015 times observed input bounding diagonal; no assumed metres",
                             "normal_tolerance_deg": 35, "maximum_planes": max_planes,
                             "selected_triangle_count": sum(p["observed_triangle_count"] for p in planes),
                             "selected_area_fraction": sum(p["observed_original_area_relative_squared"] for p in planes) / float(areas.sum()),
                             "remaining_triangle_count": len(faces) - sum(p["observed_triangle_count"] for p in planes)},
              "planes": planes}
    write_json(dest / "structural-planes.relative.json", result)
    print(json.dumps({"block": block, "planes": len(planes), "selected_faces": result["extraction"]["selected_triangle_count"],
                      "area_fraction": round(result["extraction"]["selected_area_fraction"], 3)}), flush=True)
    return result


def ray_surface(vertices, faces, pose, pixel):
    """Actual nearest retained-triangle ray intersection for an image annotation."""
    matrix = np.array(pose["world_from_camera_cv"])
    origin = matrix[:, 3]
    direction = matrix[:, :3] @ np.linalg.solve(np.array(pose["calibration_matrix"]), np.array([*pixel, 1]))
    direction /= np.linalg.norm(direction)
    tri = vertices[faces]; a = tri[:, 1]-tri[:, 0]; b = tri[:, 2]-tri[:, 0]
    h = np.cross(np.broadcast_to(direction, b.shape), b); det = np.sum(a*h, axis=1)
    usable = abs(det) > 1e-10; inv = np.zeros(len(det)); inv[usable] = 1/det[usable]
    s = origin-tri[:, 0]; u = np.sum(s*h, axis=1)*inv; q = np.cross(s, a)
    v = q@direction*inv; t = np.sum(b*q, axis=1)*inv
    hit = usable & (u >= 0) & (v >= 0) & (u+v <= 1) & (t > 0)
    if not hit.any():
        return None
    selected = np.flatnonzero(hit); face = int(selected[np.argmin(t[selected])])
    return {"position_relative": (origin+t[face]*direction).tolist(), "source_face_index_zero_based": face,
            "barycentric": [float(1-u[face]-v[face]), float(u[face]), float(v[face])],
            "distance_along_camera_ray_relative": float(t[face])}


def semantic_anchors():
    # Human-readable identities were inspected in these exact retained images.
    # Image pixels are annotations, never independently measured physical controls.
    definitions = [
      {"id": "CAFE-CRAFT-STONE-FRONT", "block": "retail-corridor", "image": "000001.jpg", "pixel": [629, 294],
       "identity": "CAFE DE CRAFT stone-clad column front fish logo", "crop": [390, 35, 756, 490],
       "target": {"file": ".planning/2026-09-24-interior-twin/measure-2f-main/observations.json", "columnId": "COL-L-CAFE-STONE"}},
      {"id": "CAFE-CRAFT-STONE-SIDE", "block": "retail-corridor", "image": "000001.jpg", "pixel": [449, 296],
       "identity": "CAFE DE CRAFT stone-clad column side fish logo", "crop": [390, 35, 756, 490],
       "target": {"file": ".planning/2026-09-24-interior-twin/measure-2f-main/observations.json", "columnId": "COL-L-CAFE-STONE"}},
      {"id": "BOKSOONDOGA-FASCIA", "block": "retail-corridor", "image": "000043.jpg", "pixel": [700, 50],
       "identity": "복순도가 lettering fascia over glass refrigerator frontage", "crop": [260, 0, 960, 185],
       "target": {"file": ".planning/2026-09-23-video-twin/majibang-plan.json", "secondFloorUnits_name": "복순도가"}},
      {"id": "KKOTDEUL-FRONTAGE", "block": "corridor-connector", "image": "000031.jpg", "pixel": [430, 229],
       "identity": "꽃들 illuminated fascia over open flower frontage", "crop": [253, 164, 470, 460],
       "target": {"file": ".planning/2026-09-23-video-twin/majibang-plan.json", "secondFloorUnits_name": "꽃들"}},
      {"id": "PASCUCCI-FASCIA", "block": "retail-corner", "image": "000021.jpg", "pixel": [261, 169],
       "identity": "CAFFE PASCUCCI illuminated lettering on dark fascia", "crop": [66, 77, 450, 244],
       "target": {"file": ".planning/2026-09-23-video-twin/majibang-plan.json", "secondFloorUnits_name": "CAFFE PASCUCCI"}},
      {"id": "STORYWAY-FASCIA", "block": "retail-corner", "image": "000021.jpg", "pixel": [878, 214],
       "identity": "StoryWay fascia across photographed corridor from PASCUCCI", "crop": [770, 175, 960, 273],
       "target": {"file": ".planning/2026-09-23-video-twin/majibang-plan.json", "secondFloorUnits_name": "편의점 StoryWay"}},
    ]
    anchors = []
    cache = {}
    for entry in definitions:
        block = entry["block"]
        if block not in cache:
            src = SOURCE/block
            vertices, uv, faces, tf = load_obj(src/"reference_observed_surfaces.obj")
            poses = json.loads((src/"camera-poses.relative.json").read_text())["cameras"]
            structure = json.loads((OUTPUT/block/"structural-planes.relative.json").read_text())
            assignments = {}
            for plane in structure["planes"]:
                ids = json.loads((OUTPUT/block/(plane["id"]+".source-face-ids.json")).read_text())["face_indices_zero_based"]
                assignments.update({i:plane["id"] for i in ids})
            cache[block] = vertices, faces, poses, assignments
        vertices, faces, poses, assignments = cache[block]
        pose = next(p for p in poses if p["image"] == entry["image"])
        source = ROOT/pose["undistorted_image"]
        source_image = cv2.imread(str(source)); assert source_image is not None
        hit = ray_surface(vertices, faces, pose, entry["pixel"])
        entry["status"] = "identity_observed_mesh_ray_anchor" if hit else "identity_observed_no_retained_mesh_ray_hit"
        entry["source_image"] = str(source.relative_to(ROOT)); entry["source_image_sha256"] = digest(source)
        entry["annotation_basis"] = "manual semantic annotation in exact retained undistorted image"
        entry["pixel_origin"] = "top-left, retained calibrated image pixels"
        entry["role"] = "reference_only"; entry["metric_verified"] = False
        entry["local_mesh_ray_intersection"] = hit
        if hit:
            hit["selected_structural_plane_id"] = assignments.get(hit["source_face_index_zero_based"])
            projection = np.linalg.solve(np.vstack([np.array(pose["world_from_camera_cv"]), [0,0,0,1]]),
                                         np.array([*hit["position_relative"],1]))[:3]
            pxy = np.array(pose["calibration_matrix"])@projection
            entry["ray_reprojection_error_pixels"] = float(np.linalg.norm(pxy[:2]/pxy[2]-entry["pixel"]))
        x0,y0,x1,y1 = entry["crop"]; annotated=source_image.copy()
        cv2.drawMarker(annotated,tuple(entry["pixel"]),(0,0,255),cv2.MARKER_CROSS,24,2)
        crop=annotated[y0:y1,x0:x1];path=EVIDENCE/(entry["id"].lower()+"-source-annotation.jpg")
        cv2.imwrite(str(path),crop,[cv2.IMWRITE_JPEG_QUALITY,95]);entry["annotation_image"] = str(path.relative_to(ROOT))
        target_path=ROOT/entry["target"]["file"];entry["target_authority_sha256"] = digest(target_path)
        entry["numeric_target_point"] = None
        entry["station_correspondence_status"] = "supported_identity_only_numeric_point_unverified"
        entry["station_fit_eligible"] = False
        entry["limitations"] = ["Logo/letter position is not the shop footprint corner or column survey control.",
                                "Existing semantic coordinate authority has inferred registration and no independent site validation.",
                                "Historical public-video epoch; upload date does not establish capture date/current site."]
        anchors.append(entry)
    write_json(OUTPUT/"semantic-correspondence-anchors.json", {"schema":"chooguard.structural-semantic-anchors.v1",
               "role":"reference_only", "metric_verified":False, "anchors":anchors})
    overlaps=json.loads((ROOT/'workers/twin/evidence/interior-blocks-20261008/corridor-relative-alignment.json').read_text())
    write_json(OUTPUT/"supported-registration.relative.json", {"schema":"chooguard.structural-registration.v1",
               "role":"reference_only", "metric_verified":False, "placement_export_allowed":False,
               "station_frame_registration":{"status":"insufficient_numeric_same_feature_controls",
                   "station_from_relative_matrix":None,"supported_semantic_identity_anchors":len(anchors),
                   "numeric_station_control_points":0,"independent_field_check_points":0,
                   "reason":"Existing plan offers shop regions and inferred column bounds, not independently verified same physical point coordinates.",
                   "no_translation_scale_rotation_invented":True},
               "supported_relative_corridor_registration":overlaps,
               "remaining_unseen":["back sides/depths of columns and shop walls", "occluded lower areas behind pedestrians",
                                  "most building interior beyond the six filmed blocks", "closed shop/service rooms, shaft interiors",
                                  "continuous floor/wall/ceiling geometry across unfilmed gaps"]})
    return anchors


def validate_outputs():
    package=json.loads((EVIDENCE/"blender-structure-package-qa.json").read_text())
    fbx_proof=json.loads((EVIDENCE/"fbx-structure-integrity-qa.json").read_text())
    fbx_checks={p['block']:p for p in fbx_proof['results']}
    blocks={p["block"]:p for p in package["results"]};rows=[];checks=[]
    for block in BLOCKS:
        j=json.loads((OUTPUT/block/"structural-planes.relative.json").read_text())
        v, uv, f, tf=load_obj(SOURCE/block/"reference_observed_surfaces.obj");assigned=[]
        for path,hash_value in j["source_hashes"].items():
            assert digest(ROOT/path)==hash_value, "Source artifact changed"
        for p in j["planes"]:
            ids=np.array(json.loads((OUTPUT/block/(p['id']+'.source-face-ids.json')).read_text())["face_indices_zero_based"])
            assigned.extend(ids.tolist());pv,puv,pf,ptf=load_obj(ROOT/p['artifacts']['mesh'])
            assert len(pf)==len(ids)==p['observed_triangle_count']
            assert np.array_equal(puv[ptf], uv[tf[ids]]), "Original photographed UV changed"
            n=np.array(p['equation_relative']['normal']);d=p['equation_relative']['offset']
            assert np.abs(pv@n+d).max()<1e-7
            assert np.isfinite(pv).all() and p['new_unobserved_faces_created']==0
        assert len(set(assigned))==len(assigned), "One source triangle assigned twice within block"
        assert blocks[block]['blender_reopen_passed'] and blocks[block]['fbx_reimport_passed']
        assert fbx_checks[block]['fbx_fresh_reimport_passed']
        assert fbx_checks[block]['material_name_manifest_match'] and fbx_checks[block]['frame_empty_probes_roundtrip_passed']
        views=[]
        for render in blocks[block]['render_records']:
            src=cv2.imread(str(ROOT/render['source_image']));rgba=cv2.imread(str(ROOT/render['render']),cv2.IMREAD_UNCHANGED)
            assert rgba is not None and rgba.shape[2]==4 and src is not None
            source=cv2.resize(src,(rgba.shape[1],rgba.shape[0]));a=rgba[:,:,3].astype(float)/255
            overlay=np.rint(source*(1-a[:,:,None]*.48)+rgba[:,:,:3]*(a[:,:,None]*.48)).astype(np.uint8)
            out=EVIDENCE/(block+'-'+render['source_camera_image'].replace('.jpg','')+'-source-overlay.jpg')
            cv2.imwrite(str(out),overlay,[cv2.IMWRITE_JPEG_QUALITY,94])
            coverage=float((rgba[:,:,3]>127).mean())
            views.append({'source_image':render['source_image'],'source_sha256':digest(ROOT/render['source_image']),
                          'overlay':str(out.relative_to(ROOT)), 'plane_raster_coverage_fraction':coverage,
                          'coverage_is_one_image_not_building_percentage':True})
            if render==blocks[block]['render_records'][1]:
                row=np.zeros((240,1152,3),np.uint8)
                for i,(label,img) in enumerate([('retained source',source),('observed plane overlay',overlay),('only selected plane fragments',rgba[:,:,:3])]):
                    row[24:,384*i:384*(i+1)]=cv2.resize(img,(384,216))
                    cv2.putText(row,block+' / '+label,(384*i+5,17),cv2.FONT_HERSHEY_SIMPLEX,.40,(255,255,255),1)
                rows.append(row)
        checks.append({'block':block,'source_hashes_unchanged':True,'original_uv_exact':True,
                       'source_triangle_assignments_unique':True,'plane_equations_finite_passed':True,
                       'plane_meshes':len(j['planes']),'observed_triangles':len(assigned),
                       'actual_source_camera_views':views,'blender_reopen_passed':True,'fbx_reimport_passed':True})
        checks[-1]['fbx_integrity']=fbx_checks[block]
    cv2.imwrite(str(EVIDENCE/'source-plane-overlay-comparison.jpg'),np.vstack(rows),[cv2.IMWRITE_JPEG_QUALITY,94])
    anchors=semantic_anchors()
    artifacts=[]
    for path in sorted(OUTPUT.rglob('*')):
        if path.is_file() and path.suffix not in {'.blend1','.py'}:
            artifacts.append({'path':str(path.relative_to(ROOT)),'bytes':path.stat().st_size,'sha256':digest(path)})
    result={'schema':'chooguard.structural-artifact-qa.v1','role':'reference_only','metric_verified':False,
            'placement_export_allowed':False,'blocks':checks,'plane_fragment_meshes':sum(c['plane_meshes'] for c in checks),
            'observed_triangle_sum_including_overlap':sum(c['observed_triangles'] for c in checks),
            'independent_station_metric_controls':0,'semantic_identity_anchors':len(anchors),
            'actual_mesh_ray_anchor_count':sum(a['local_mesh_ray_intersection'] is not None for a in anchors),
            'artifacts':artifacts,'unobserved_fill_triangles':0,'building_coverage_percent':None}
    write_json(EVIDENCE/'artifact-integrity-and-source-camera-qa.json',result)
    print(json.dumps({k:result[k] for k in ['plane_fragment_meshes','observed_triangle_sum_including_overlap','actual_mesh_ray_anchor_count','unobserved_fill_triangles']}),flush=True)


def main():
    parser = argparse.ArgumentParser(); parser.add_argument("--blocks", nargs="+", default=BLOCKS)
    parser.add_argument("--max-planes", type=int, default=14); parser.add_argument("--validate", action="store_true"); args = parser.parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True); EVIDENCE.mkdir(parents=True, exist_ok=True)
    if args.validate:
        validate_outputs();return
    results = [extract(b, args.max_planes) for b in args.blocks]
    write_json(EVIDENCE / "structural-extraction-summary.json", {"schema": "chooguard.structural-extraction-summary.v1",
               "role": "reference_only", "metric_verified": False, "placement_export_allowed": False,
               "blocks": [{"block": r["block"], "plane_count": len(r["planes"]), "input": r["input"], "extraction": r["extraction"]} for r in results]})


if __name__ == "__main__":
    main()
