"""Offline, append-only finish geometry for the next western diagonal wall.

No Scene, Assets, native or source-file writes. The original wall and collider
are retained; the serial writer may add only this finite public finish mesh.
"""
from pathlib import Path
from datetime import datetime, timezone
from collections import Counter, defaultdict
import hashlib
import json
import math
import numpy as np

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
SOURCE = ROOT / ".state/ACTIVE-ALL2F-PHOTO-TWIN-R18/inputs/CURRENT-WALLS-AND-NATIVE-FEATURE-SECTIONS-R18.json"
SOURCE_SHA = "a008f2bfa6fcca58160fa5d4d57dad3bc62a1a12448d544c2f7a44442b5f07e3"
Q = np.array([[-.34290633335024717, -.9393696006089878],
              [-.9393696006089878, .34290633335024717]])
TARGET_ID = "1922317721"
DONOR_ID = "67985992"
REAR_ID = "259783080"
TOP = 12.0999947
PLAIN_BOTTOM, FLUTE_BOTTOM = 9.38, 9.8
SKIN_OFFSET, END_MARGIN = .0007, .020
MATERIAL = "Assets/ChooGuard/Art/StationInterior/PhotoLocationTwinR15_Batch04_20261019/R15_NW_GRAY_FASCIA.asset"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def sid(row):
    return row["object_id"].split("-")[-2]


def relative(path):
    return str(path.relative_to(ROOT))


raw = SOURCE.read_bytes()
if digest(raw) != SOURCE_SHA:
    raise RuntimeError("Frozen R18 construction source changed; no silent rebinding")
source = json.loads(raw)
owner = next(r for r in source["nonWC_walls"] if sid(r) == TARGET_ID)
donor = next(r for r in source["nonWC_walls"] if sid(r) == DONOR_ID)
rear = next(r for r in source["nonWC_walls"] if sid(r) == REAR_ID)
g = owner["geometry"]
vertices = np.array(g["vertices_world"])
records = []
for sm, indices in enumerate(g["submesh_triangles"]):
    for offset in range(0, len(indices), 3):
        ids = indices[offset:offset + 3]
        tri = vertices[ids]
        cross = np.cross(tri[1] - tri[0], tri[2] - tri[0])
        length = float(np.linalg.norm(cross))
        if length < 1e-10:
            continue
        normal = cross / length
        normal_q = Q @ normal[[0, 2]]
        if abs(normal[1]) < 1e-4 and normal_q[0] > .85 and -.6 < normal_q[1] < -.45:
            records.append({"submesh": sm, "triangle": offset//3, "ids": ids,
                            "tri": tri, "normal_world": normal,
                            "normal_q": normal_q, "area": length/2})

# A largest actual public-face triangle establishes the exact long plane;
# the PCA of the entire compound end-cap mesh would introduce angular drift.
largest = max(records, key=lambda r: r["area"])
normal_q = largest["normal_q"]
tangent_q = np.array([-normal_q[1], normal_q[0]])
long_faces = [r for r in records if float(r["normal_q"] @ normal_q) > .99999]
face_vertices = np.concatenate([r["tri"] for r in long_faces])
face_q = face_vertices[:, [0, 2]] @ Q.T
s = face_q @ tangent_q
d = face_q @ normal_q
s0, s1 = float(min(s)), float(max(s))
d0 = float(np.median(d))
normal_world_xz = Q.T @ normal_q
normal_world = np.array([normal_world_xz[0], 0., normal_world_xz[1]])


def actual_donor_profile():
    """Recover the realized prism profile, not a historical design parameter.

    Weld duplicate hard-edge positions only for component interpretation.
    This does not change or export a replacement donor mesh.
    """
    dg = donor["geometry"]
    dv = np.array(dg["vertices_world"])
    material_index = donor["material_assets"].index(MATERIAL)
    indices = dg["submesh_triangles"][material_index]
    source_triangles = []
    tall_extents = Counter()
    for offset in range(0, len(indices), 3):
        ids = indices[offset:offset+3]
        points = dv[ids]
        y0, y1 = float(points[:,1].min()), float(points[:,1].max())
        source_triangles.append((offset//3, ids, points))
        if y1-y0 > 1:
            tall_extents[(round(y0,5), round(y1,5))] += 1
    rib_y0, rib_y1 = tall_extents.most_common(1)[0][0]
    parents = {}

    def find(k):
        parents.setdefault(k,k)
        if parents[k] != k:
            parents[k] = find(parents[k])
        return parents[k]

    def join(a,b):
        aa,bb = find(a),find(b)
        if aa != bb:
            parents[bb] = aa

    candidates = []
    for tri, ids, points in source_triangles:
        if points[:,1].min() < rib_y0-1e-5 or points[:,1].max() > rib_y1+1e-5:
            continue
        keys = [tuple(round(float(c),4) for c in p) for p in points]
        for k in keys:
            find(k)
        join(keys[0],keys[1]); join(keys[1],keys[2])
        candidates.append((tri,ids,keys))
    components = defaultdict(list)
    for row in candidates:
        components[find(row[2][0])].append(row)
    prisms = []
    for rows in components.values():
        if len(rows) != 12:
            continue
        by_position = defaultdict(list)
        for _, ids, _ in rows:
            for ix in ids:
                p = dv[ix]
                by_position[tuple(round(float(c),4) for c in p)].append(p)
        if len(by_position) != 8:
            continue
        corners = np.array([np.mean(v,axis=0) for v in by_position.values()])
        prisms.append({"triangle_indices": sorted(r[0] for r in rows),
                       "corners": corners, "center": np.mean(corners,axis=0)})
    centers = np.array([p["center"] for p in prisms])
    _, axes = np.linalg.eigh(np.cov(centers[:,[0,2]].T))
    donor_t = axes[:,-1]
    if float(donor_t @ (Q.T @ tangent_q)) < 0:
        donor_t = -donor_t
    order = np.argsort(centers[:,[0,2]] @ donor_t)
    prisms = [prisms[int(i)] for i in order]
    centers = np.array([p["center"] for p in prisms])
    pitches = np.linalg.norm(np.diff(centers[:,[0,2]],axis=0),axis=1)
    rib_triangles = {i for p in prisms for i in p["triangle_indices"]}
    backing = []
    for tri, ids, points in source_triangles:
        if tri in rib_triangles or points[:,1].max()-points[:,1].min() < 1:
            continue
        cross = np.cross(points[1]-points[0],points[2]-points[0])
        length = float(np.linalg.norm(cross))
        if length > .01 and abs(cross[1]/length) < 1e-4:
            backing.append((tri,points,cross,length))
    normal_sum = sum((r[2] for r in backing),np.zeros(3))
    dn_world = normal_sum/np.linalg.norm(normal_sum)
    dn = dn_world[[0,2]]
    backing_points = np.concatenate([r[1] for r in backing])
    backing_d = float(np.median(backing_points[:,[0,2]] @ dn))
    widths,depths,gaps = [],[],[]
    for p in prisms:
        corners = p["corners"][:,[0,2]]
        widths.append(float(np.ptp(corners @ donor_t)))
        depths.append(float(np.ptp(corners @ dn)))
        gaps.append(float(np.min(corners @ dn))-backing_d)
    nearest = min(prisms,key=lambda p: float(np.linalg.norm(p["center"][[0,2]]-np.array(at_target_start_xz))))
    nearest_q = Q @ nearest["center"][[0,2]]
    profile = {
        "schema": "chooguard.r18.realized_donor_flute_profile.v1",
        "source_input_sha256": SOURCE_SHA, "donor_object_id": donor["object_id"],
        "donor_mesh_asset": donor["mesh_asset"], "donor_mesh_object_id": donor["mesh_object_id"],
        "donor_geometry_sha256_canonical_json": digest(json.dumps(dg,sort_keys=True,separators=(",", ":")).encode()),
        "gray_submesh_index": material_index, "material_asset": MATERIAL,
        "extraction": "Repeated tall Y extent; weld source positions at0.1mm for connectivity only; retain95 disconnected 12-triangle/8-corner prisms. Prism centers determine actual row pitch; corner projections determine cross-section; nonprism gray triangles determine backing plane.",
        "complete_prism_count": len(prisms), "source_component_triangle_count": len(rib_triangles),
        "rib_y": [rib_y0,rib_y1], "plain_fascia_y": [float(backing_points[:,1].min()),rib_y0],
        "plain_top_cap_y": [rib_y1,float(backing_points[:,1].max())],
        "realized_pitch_median_m": float(np.median(pitches)),
        "realized_pitch_range_m": [float(min(pitches)),float(max(pitches))],
        "realized_face_width_median_m": float(np.median(widths)),
        "realized_face_width_range_m": [min(widths),max(widths)],
        "realized_full_prism_depth_median_m": float(np.median(depths)),
        "realized_full_prism_depth_range_m": [min(depths),max(depths)],
        "realized_backing_to_prism_gap_median_m": float(np.median(gaps)),
        "realized_backing_to_prism_gap_range_m": [min(gaps),max(gaps)],
        "donor_tangent_world_xz": donor_t.tolist(), "donor_backing_outward_normal_world": dn_world.tolist(),
        "donor_backing_plane_dot_normal_world_xz": backing_d,
        "donor_backing_triangle_indices": [r[0] for r in backing],
        "phase_anchor_actual_donor_prism_center_world": nearest["center"].tolist(),
        "phase_anchor_actual_donor_prism_center_q": nearest_q.tolist(),
        "phase_anchor_projected_to_target_s": float(nearest_q @ tangent_q),
        "phase_anchor_donor_triangle_indices": nearest["triangle_indices"],
        "source_prism_samples": [{"triangle_indices":p["triangle_indices"],
            "center_world":p["center"].tolist(),"corners_world":p["corners"].tolist()}
            for p in [prisms[0],prisms[len(prisms)//2],prisms[-1]]],
        "historical_builder_pitch_parameter_reported_by_parent_m": .085,
        "historical_parameter_vs_realized_geometry": "The frozen world mesh realizes approximately83.827mm spacing. Reuse its actual geometry values; do not substitute the historical85mm command or the superseded100mm target guess.",
        "dimensions_are_existing_MODEL_geometry_not_site_measurements": True,
        "donor_modified": False}
    return profile


target_start_q = tangent_q*s0+normal_q*d0
at_target_start_xz = Q.T @ target_start_q
donor_profile = actual_donor_profile()
PITCH = donor_profile["realized_pitch_median_m"]
RIB_WIDTH = donor_profile["realized_face_width_median_m"]
RIB_DEPTH = donor_profile["realized_full_prism_depth_median_m"]
RIB_GAP = donor_profile["realized_backing_to_prism_gap_median_m"]
FLUTE_BOTTOM,RIB_TOP = donor_profile["rib_y"]
PLAIN_BOTTOM = donor_profile["plain_fascia_y"][0]
PHASE_S = donor_profile["phase_anchor_projected_to_target_s"]
donor_profile_path = OUT / "DONOR-FLUTE-PROFILE-R18.json"
donor_profile_path.write_text(json.dumps(donor_profile,ensure_ascii=False,indent=2)+"\n")


def at(ss, dd, yy):
    qq = tangent_q*ss + normal_q*dd
    xz = Q.T @ qq
    return [float(xz[0]), float(yy), float(xz[1])]


def at_q(ss):
    return (tangent_q*ss + normal_q*d0).tolist()


def selected(y0, y1):
    rows = []
    for sm in range(len(g["submesh_triangles"])):
        ts = [r["triangle"] for r in long_faces if r["submesh"] == sm
              and float(max(r["tri"][:, 1])) >= y0-1e-6
              and float(min(r["tri"][:, 1])) <= y1+1e-6]
        if ts:
            rows.append({"submesh": sm, "triangle_indices_in_submesh": ts,
                         "clip_world_y": [y0, y1],
                         "boundary_triangles_require_exact_interval_clip": True})
    return rows


verts, uv, normals, triangles = [], [], [], []
parts = []


def quad(points, wanted_normal, uv_points):
    start = len(verts)
    v = [np.array(p) for p in points]
    cross = np.cross(v[1]-v[0], v[2]-v[0])
    wanted = np.array(wanted_normal)
    norm = cross / np.linalg.norm(cross)
    ids = [0, 1, 2, 0, 2, 3]
    if float(norm @ wanted) < 0:
        ids = [0, 2, 1, 0, 3, 2]
        norm = -norm
    verts.extend([p.tolist() for p in v])
    uv.extend(uv_points)
    normals.extend([norm.tolist()]*4)
    triangles.extend(start+i for i in ids)


def front(ss0, ss1, y0, y1, dd):
    quad([at(ss0, dd, y0), at(ss1, dd, y0), at(ss1, dd, y1), at(ss0, dd, y1)],
         normal_world, [[ss0-s0, y0-7], [ss1-s0, y0-7], [ss1-s0, y1-7], [ss0-s0, y1-7]])


start = len(triangles)//3
front(s0, s1, PLAIN_BOTTOM, FLUTE_BOTTOM, d0+SKIN_OFFSET)
parts.append({"id": "PLAIN_CHARCOAL_FASCIA", "triangle_range_start": start,
              "triangle_count": len(triangles)//3-start,
              "world_y": [PLAIN_BOTTOM, FLUTE_BOTTOM]})
start = len(triangles)//3
front(s0, s1, FLUTE_BOTTOM, TOP, d0+SKIN_OFFSET)
parts.append({"id": "FLUTED_BAND_VALLEY_BACKING", "triangle_range_start": start,
              "triangle_count": len(triangles)//3-start,
              "world_y": [FLUTE_BOTTOM, TOP]})

# These are cosmetic slats over the existing unchanged opaque wall. They never
# create a passage, remove a backface, open the top or change a collider.
start = len(triangles)//3
rib_ranges = []
# Continue the actual nearest donor rib-center phase onto the next face.
first_index = math.ceil((s0+END_MARGIN+RIB_WIDTH/2-PHASE_S)/PITCH)
cursor = PHASE_S+first_index*PITCH-RIB_WIDTH/2
tangent_world_xz = Q.T @ tangent_q
tangent_world = np.array([tangent_world_xz[0], 0., tangent_world_xz[1]])
while cursor + RIB_WIDTH <= s1 - END_MARGIN + 1e-10:
    a, b = cursor, cursor+RIB_WIDTH
    low, high = d0+SKIN_OFFSET+RIB_GAP, d0+SKIN_OFFSET+RIB_GAP+RIB_DEPTH
    front(a, b, FLUTE_BOTTOM, RIB_TOP, high)
    quad([at(b, low, FLUTE_BOTTOM), at(a, low, FLUTE_BOTTOM), at(a, low, RIB_TOP), at(b, low, RIB_TOP)],
         -normal_world, [[0,0],[RIB_WIDTH,0],[RIB_WIDTH,RIB_TOP-FLUTE_BOTTOM],[0,RIB_TOP-FLUTE_BOTTOM]])
    quad([at(a, low, FLUTE_BOTTOM), at(a, high, FLUTE_BOTTOM), at(a, high, RIB_TOP), at(a, low, RIB_TOP)],
         -tangent_world, [[0,0],[RIB_DEPTH,0],[RIB_DEPTH,RIB_TOP-FLUTE_BOTTOM],[0,RIB_TOP-FLUTE_BOTTOM]])
    quad([at(b, high, FLUTE_BOTTOM), at(b, low, FLUTE_BOTTOM), at(b, low, RIB_TOP), at(b, high, RIB_TOP)],
         tangent_world, [[0,0],[RIB_DEPTH,0],[RIB_DEPTH,RIB_TOP-FLUTE_BOTTOM],[0,RIB_TOP-FLUTE_BOTTOM]])
    quad([at(a, low, RIB_TOP), at(a, high, RIB_TOP), at(b, high, RIB_TOP), at(b, low, RIB_TOP)],
         [0,1,0], [[0,0],[0,RIB_DEPTH],[RIB_WIDTH,RIB_DEPTH],[RIB_WIDTH,0]])
    quad([at(a, high, FLUTE_BOTTOM), at(a, low, FLUTE_BOTTOM), at(b, low, FLUTE_BOTTOM), at(b, high, FLUTE_BOTTOM)],
         [0,-1,0], [[0,0],[0,RIB_DEPTH],[RIB_WIDTH,RIB_DEPTH],[RIB_WIDTH,0]])
    rib_ranges.append([a,b])
    cursor += PITCH
parts.append({"id": "VERTICAL_RIBS", "triangle_range_start": start,
              "triangle_count": len(triangles)//3-start, "rib_count": len(rib_ranges),
              "world_y": [FLUTE_BOTTOM, RIB_TOP], "plain_top_cap_world_y": [RIB_TOP,TOP]})

original_receipts = json.loads((ROOT / "asset-library/research-public/2026-10-08/measured-twin/receipts.json").read_text())["receipts"]
names = ["IMG20231215080539.jpg", "IMG20231215080611.jpg", "IMG20231215080637.jpg"]
observations = [
    "Donut branch: plain dark-gray horizontal fascia below a visibly separate band of narrow vertical dark ribs. The low service counter remains open above it; no new glass or lower edit is proposed here.",
    "AmericanTrailer branch: a plain charcoal fascia runs directly below vertical narrow flutes; three rectangular black ventilation grilles interrupt the upper band. Their individual placement on the target remains unqualified and no grille is invented.",
    "Pharmacy branch: the same dark plain fascia and upper vertical-rib band continue toward a right-hand gray louvered service return. Its public glazing and existing entrance are separate lower features and are not changed."
]
photos = []
for name, observed in zip(names, observations):
    receipt = next(r for r in original_receipts if r["path"].endswith(name))
    p = ROOT / receipt["path"]
    if digest(p.read_bytes()) != receipt["sha256"]:
        raise RuntimeError("Retained original evidence does not match its receipt")
    photos.append({"id": name.removesuffix(".jpg"), "original_path": receipt["path"],
        "original_url": receipt["url"], "page_url": "https://ryokou-recommend.com/busan-station/",
        "sha256": receipt["sha256"], "bytes": receipt["bytes"],
        "original_dimensions_px": receipt["image_dimensions_px"],
        "unmodified_original_excerpt": {"roi_px": [0, 0, 4624, 1250],
             "coordinate_reference": "ORIGINAL_4624x3468_PIXELS",
             "excerpt_is_roi_reference_only_no_resampling_or_new_image": True},
        "observed_literal_shape": observed, "camera_metadata": receipt["capture_date"],
        "published_utc": "2024-01-09T16:33:43+00:00",
        "site_displayed_publication_jst": "2024-01-10",
        "independent_observation_group": "SAME_AUTHOR_SAME_2023_VISIT_NOT_THREE_INDEPENDENT_SURVEYS",
        "qualification": "directly_supported_historical_actual_branch_shape; current_2026_epoch_not_proven",
        "reference_only_no_photo_texture_or_logo_reproduction": True})

column = next(r for r in source["independent_columns"] if sid(r) == "248739979")
colq = np.array(column["geometry"]["vertices_world"])[:,[0,2]] @ Q.T
donor_q = np.array(donor["geometry"]["vertices_world"])[:,[0,2]] @ Q.T
distance = np.linalg.norm(donor_q - np.array(at_q(s0)), axis=1)
nearest = donor_q[int(np.argmin(distance))]

payload = {"schema": "chooguard.r18.append_only_public_finish_mesh.v1",
    "status": "PREPARED_OFFLINE_NOT_APPLIED", "source_input": relative(SOURCE), "source_input_sha256": SOURCE_SHA,
    "target_owner_id": owner["object_id"], "target_owner_path": owner["path"],
    "operation": "ADD_FINITE_UPPER_PUBLIC_FINISH_CHILD_KEEP_ORIGINAL_RENDERER_AND_COLLIDER",
    "new_renderer_name": "R18_NW_NEXT_DIAGONAL_SHARED_UPPER_FASCIA_AND_FLUTES_MODEL",
    "new_mesh_geometry_space": "WORLD; root must preserve this placement when parenting",
    "new_renderer_has_collider": False, "materials": [MATERIAL],
    "actual_donor_profile_reference": relative(donor_profile_path),
    "actual_donor_profile_sha256": digest(donor_profile_path.read_bytes()),
    "geometry": {"vertices_world": verts, "uv": uv, "normals_world": normals,
                 "submesh_triangles": [triangles]},
    "parts": parts, "original_mesh_rewritten": False,
    "original_source_assets_mutated": False, "all_original_owner_triangles_uv_and_colliders_retained": True}
payload_path = OUT / "UPPER-BAND-MESH-PAYLOAD-R18.json"
payload_bytes = (json.dumps(payload, ensure_ascii=False, separators=(",", ":"))+"\n").encode()
payload_path.write_bytes(payload_bytes)

packet = {"schema": "chooguard.r18.northwest.shared_upper_architectural_band.v1",
    "status": "READY_FINITE_UPPER_BAND_ONLY_FOR_ROOT_SERIAL_WRITER",
    "utc": datetime.now(timezone.utc).isoformat(),
    "source_input": {"path": relative(SOURCE), "sha256": SOURCE_SHA, "utc": source.get("utc")},
    "scope": "One new western diagonal current wall, shared plain fascia and vertical flutes only.",
    "target": {"object_id": owner["object_id"], "path": owner["path"], "mesh_asset": owner["mesh_asset"],
        "mesh_object_id": owner["mesh_object_id"], "world_matrix": owner["world_matrix"],
        "geometry_sha256_canonical_json": digest(json.dumps(g, sort_keys=True, separators=(",", ":")).encode()),
        "original_vertex_count": len(g["vertices_world"]), "original_uv_count": len(g["uv"]),
        "original_submesh_triangle_counts": [len(t)//3 for t in g["submesh_triangles"]]},
    "exact_current_public_face": {"basis": "Largest actual current long public-face triangle; no source-pixel endpoints and no whole-mesh PCA drift.",
        "basis_triangle": {"submesh": largest["submesh"], "triangle_index_in_submesh": largest["triangle"],
            "vertex_indices": largest["ids"], "vertices_world": largest["tri"].tolist(),
            "uv": [g["uv"][i] for i in largest["ids"]]},
        "tangent_q": tangent_q.tolist(), "public_normal_q": normal_q.tolist(),
        "public_normal_world": normal_world.tolist(), "plane_equation": "dot(public_normal_q, q) = plane_d",
        "plane_d": d0, "source_plane_numeric_range": [float(min(d)), float(max(d))],
        "s_interval": [s0,s1], "s_equation": "dot(tangent_q, q)",
        "selected_a_q": at_q(s0), "selected_b_q": at_q(s1), "length_model_m": s1-s0,
        "plain_fascia_world_y": [PLAIN_BOTTOM, FLUTE_BOTTOM], "fluted_upper_world_y": [FLUTE_BOTTOM, RIB_TOP],
        "plain_top_cap_world_y": [RIB_TOP,TOP],
        "original_support_triangles_plain_fascia": selected(PLAIN_BOTTOM, FLUTE_BOTTOM),
        "original_support_triangles_fluted_upper": selected(FLUTE_BOTTOM, TOP),
        "these_triangle_ids_are_support_references_not_deletions": True},
    "fixed_and_current_anchor_chain": {"binding_level": "SHARED_ROW_UPPER_ARCHITECTURE; NOT AN INDIVIDUAL TENANT WIDTH CLAIM",
        "chain": ["Existing uniquely located west EV remains the neighborhood anchor for the applied donut.",
            "Applied donut67985992 is the immediately preceding long diagonal front; current target1922317721 continues its public-side direction.",
            "Target long-face end meets the actual next column248739979 and ends before its body; endpoints come from current triangles.",
            "Actual 2023 donut, AmericanTrailer and pharmacy photos consistently show the same split plain-fascia/vertical-flute architecture; the current 2026 guide retains the ordered western 1/2/3/4 retail neighborhoods."],
        "preceding_applied_donut": {"object_id": donor["object_id"], "path": donor["path"],
            "mesh_asset": donor["mesh_asset"], "unchanged": True,
            "nearest_actual_donor_vertex_to_target_public_start_q": nearest.tolist(),
            "distance_model_m": float(min(distance)),
            "small_original_cap_join_is_preserved_not_filled_or_extended": True},
        "end_column": {"object_id": column["object_id"], "path": column["path"],
            "actual_q_bounds": [float(colq[:,0].min()),float(colq[:,1].min()),float(colq[:,0].max()),float(colq[:,1].max())],
            "preserve_geometry_materials_and_axis": True},
        "rear_preserved": {"object_id": rear["object_id"], "path": rear["path"]},
        "guide": {"path": ".state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/root/current-guide-2f-original-20260419.jpg",
            "role": "Semantic order/context only, never coordinates, widths or wall geometry."}},
    "source_photos": photos,
    "actual_donor_profile": {"path": relative(donor_profile_path),
        "sha256": digest(donor_profile_path.read_bytes()), **donor_profile},
    "construction": {"plain_and_fluted_finish_material_asset": MATERIAL,
        "profile_height_donor": "Exact realized donor prism Y9.8..12.0, plain fascia9.38..9.8 and plain topcap12.0..12.0999947. These are existing MODEL mesh values, not photo measurements.",
        "original_wall_base_XZ_top_TRS_all_vertices_triangles_uv_materials_and_colliders_unchanged": True,
        "method": "Append one opaque cosmetic finish mesh only over the selected public upper plane. The backing covers old panel joints; narrow surface ribs supply actual shape and light/shadow. No wall triangles are removed.",
        "backing_normal_offset_model_m": SKIN_OFFSET,
        "rib_back_gap_from_backing_model_m": RIB_GAP,
        "rib_full_prism_depth_model_m": RIB_DEPTH,
        "rib_projection_beyond_backing_model_m": RIB_GAP+RIB_DEPTH,
        "rib_pitch_model_m": PITCH, "rib_face_width_model_m": RIB_WIDTH,
        "rib_end_margin_model_m": END_MARGIN, "rib_count": len(rib_ranges),
        "rib_phase_basis": "Continue the nearest actual donor prism center, projected into this target's existing tangent basis; retain a20mm original end-cap margin.",
        "new_finish_max_public_projection_model_m": SKIN_OFFSET+RIB_GAP+RIB_DEPTH,
        "rib_pitch_depth_height_phase_are_field_measured": False,
        "no_photo_pixels_used_as_metric_or_surface_texture": True,
        "new_finish_y_max": TOP, "original_rear_end_lower_and_top_surfaces_not_cut_or_moved": True,
        "lower_tenant_splits_counters_glass_entries_untouched": True,
        "new_holes_glass_door_or_entry": False},
    "payload": {"path": relative(payload_path), "sha256": digest(payload_bytes),
        "new_vertex_count": len(verts), "new_triangle_count": len(triangles)//3,
        "material_count": 1, "builder": relative(Path(__file__).resolve())},
    "pending": [{"feature": "Precise individual tenant split and lower service/front opening profiles", "reason": "Shared-band row binding does not determine store2/3 metric boundaries or door leaves; lower geometry is retained."},
        {"feature": "AmericanTrailer upper ventilation grille locations", "reason": "Visible in the literal source, but the current owner's individual tenant position is unqualified; no grille is invented."},
        {"feature": "2026 same-location repeat view", "reason": "The 2026 guide supports facility order only. Source facade photos are historical2023; no present-day equality or99percent score is claimed."}],
    "source_epoch_limit": "EXIF reports2023-12-15 without timezone; publicationUTC2024-01-09 corresponds to displayedJapan2024-01-10. Camera dates are embedded metadata, not independently verified site survey times.",
    "source_research_limits": "Targeted Korean portal queries returned a historical pharmacy primary plus aggregate navigation for recent AmericanTrailer visits. Unresolved blog IDs, unrelated Diningcode click results and listing snippets were not adopted as photo geometry evidence.",
    "new_original_download_count": 0, "new_original_download_bytes": 0, "new_download_budget_bytes": 12000000,
    "ready_feature_count": 1, "ready_owner_count": 1,
    "protected": ["applied donut67985992", "rear259783080", "applied NW StoryWay/Baekmidang", "old unresolved NW3", "all1F", "fixed native/Native6", "WC", "columns/EV", "currentR17floor/openings", "all user deletions"],
    "Scene_Assets_UI_native_changes": False, "QA_tests_probes_reopen_captures": False,
    "applied_or_saved": False, "all2F_complete": False, "99percent_achieved_or_measured": False}
(OUT / "CONSTRUCTION-PACKET.json").write_text(json.dumps(packet, ensure_ascii=False, indent=2)+"\n")
(OUT / "findings.md").write_text("""# R18 northwest next finite feature

Ready for the root serial writer: one shared upper fascia/fluting feature on current wall1922317721. Source geometry is frozen R18 SHA a008f2bfa6fcca58160fa5d4d57dad3bc62a1a12448d544c2f7a44442b5f07e3. Exact face normals, endpoints, support triangle indices and UV samples are in CONSTRUCTION-PACKET.json. The mesh payload is an append-only cosmetic child; it never rewrites the original wall or collider.

The target follows the already-applied donut diagonal front and ends at actual column248739979. Current geometry supplies its 7.866m finite span. Three retained actual 2023 branch photographs visibly show a plain dark fascia below narrow vertical flutes. The AmericanTrailer source also contains upper grille interruptions, whose exact tenant-to-owner locations remain pending. The 2026 guide supplies only ordered retail identities; no box width or pixel endpoint defines geometry.

The revised profile comes from95 complete actual donor rib prisms: median realized pitch83.827mm, cross-section approximately24x24mm, backing-to-prism gap approximately3mm, rib Y9.8..12.0 and plain cap12.0..12.0999947. DONOR-FLUTE-PROFILE-R18.json records exact medians/ranges, source triangle IDs, sample corners and nearest-donor phase. The prior100mm/55mm/8mm target guess is superseded. The historical85mm design command differs from the realized world mesh; the actual frozen geometry is used. These remain existing MODEL dimensions, not field measurements. The original base, XZ, top, mesh vertices/triangles/UV/materials, colliders, lower tenants/entries, rear259783080, donor67985992, all fixed/native/WC/1F and current floor remain unchanged. No new passage, glass sheet, door, sign or grille is added.

Actual unmodified source paths, URLs, hashes, source epoch and original-pixel excerpt rectangles are recorded in the packet. The three photographs are one author's historical visit, not independent surveys. No current2026 finish identity or99percent completion is claimed. No source originals were downloaded this lane. No Scene/Assets/UI/native/QA/capture write ran.
""")
(OUT / "task_plan.md").write_text("# R18 northwest bounded lane\n\n- [x] Read retained original 2F public/retail evidence and current compact/full source.\n- [x] Bind a new shared upper-band feature to owner1922317721, adjacent applied donor and actual end column.\n- [x] Prepare exact public-face support triangles/normal/extent, finite append-only mesh payload and retained builder.\n\nStop at this offline packet. Root owns source inspection and serialized native execution. No old planning restoration, new originals, Scene/Assets/UI/native/QA/capture writes.\n")
(OUT / "progress.md").write_text("# Progress\n\nCompleted one finite upper-band packet for wall1922317721. New mesh has "+str(len(verts))+" vertices and "+str(len(triangles)//3)+" triangles; original wall/collider retained. Actual source photos/URLs and exact public-face support IDs are recorded. Historical photo epoch and MODEL pitch/depth/height limits remain explicit. Lower tenant/entry profiles and grille positions stay pending. No native work queued by this worker.\n")
print(json.dumps({"status": packet["status"], "owner": TARGET_ID, "length_model_m": s1-s0,
    "ribs": len(rib_ranges), "new_triangles": len(triangles)//3,
    "packet": relative(OUT / "CONSTRUCTION-PACKET.json"), "payload": relative(payload_path),
    "payload_sha256": digest(payload_bytes)}, ensure_ascii=False))
