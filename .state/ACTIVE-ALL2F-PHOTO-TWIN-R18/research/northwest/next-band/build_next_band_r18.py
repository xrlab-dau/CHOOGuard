"""Offline continuation only; never executes or rewrites the first builder."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import math
import numpy as np

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[4]
NW = OUT.parent
SOURCE = ROOT / ".state/ACTIVE-ALL2F-PHOTO-TWIN-R18/inputs/CURRENT-WALLS-AND-NATIVE-FEATURE-SECTIONS-R18.json"
SOURCE_SHA = "a008f2bfa6fcca58160fa5d4d57dad3bc62a1a12448d544c2f7a44442b5f07e3"
FIRST_SHA = "152ecb4024dec9f374a4159676aaf0fb2961733b26c4aaa331ab016ead8eb2c5"
Q = np.array([[-.34290633335024717,-.9393696006089878],[-.9393696006089878,.34290633335024717]])
TARGETS = ["1967088396","1327299834"]
SKIN, MARGIN = .0007,.020


def sha(data):
    return hashlib.sha256(data).hexdigest()


def rel(path):
    return str(path.relative_to(ROOT))


raw = SOURCE.read_bytes()
if sha(raw) != SOURCE_SHA:
    raise RuntimeError("Frozen source changed; do not silently rebind")
first_path = NW / "UPPER-BAND-MESH-PAYLOAD-R18.json"
first_raw = first_path.read_bytes()
if sha(first_raw) != FIRST_SHA:
    raise RuntimeError("First payload is frozen for root; its identity changed")
source = json.loads(raw)
first = json.loads(first_raw)
profile_path = NW / "DONOR-FLUTE-PROFILE-R18.json"
profile = json.loads(profile_path.read_text())
pitch = profile["realized_pitch_median_m"]
width = profile["realized_face_width_median_m"]
depth = profile["realized_full_prism_depth_median_m"]
gap = profile["realized_backing_to_prism_gap_median_m"]
rib_y0,rib_y1 = profile["rib_y"]
plain_y0 = profile["plain_fascia_y"][0]
top = profile["plain_top_cap_y"][1]
material = profile["material_asset"]
phase_anchor_world = np.mean(np.array(first["geometry"]["vertices_world"][-24:]),axis=0)
phase_anchor_q = Q @ phase_anchor_world[[0,2]]

# A conservative exclusion uses current WC metadata only to shorten this new
# finish. It does not redefine any existing room, wall, floor or opening.
wc_rows = [r for r in source["other_2F_mesh_metadata"]
           if any("-"+s+"-" in r["object_id"] for s in ["1417713925","2041710588"])]
wc_limits = []
wc_refs = []
for r in wc_rows:
    a,b = r["bounds_min_world"],r["bounds_max_world"]
    points = np.array([Q@np.array([x,z]) for x in [a[0],b[0]] for z in [a[2],b[2]]])
    lo,hi = points.min(axis=0),points.max(axis=0)
    wc_limits.append(float(lo[1]))
    wc_refs.append({"object_id":r["object_id"],"path":r["path"],
                    "conservative_projected_metadata_bounds_q":lo.tolist()+hi.tolist(),
                    "used_for_exclusion_only":True,"preserve":True})
wc_qy_finish_limit = min(wc_limits)-MARGIN


def build(target):
    owner = next(r for r in source["nonWC_walls"] if "-"+target+"-" in r["object_id"])
    g = owner["geometry"]
    V = np.array(g["vertices_world"])
    candidates = []
    for sm,ii in enumerate(g["submesh_triangles"]):
        for k in range(0,len(ii),3):
            ids = ii[k:k+3];v = V[ids]
            cross = np.cross(v[1]-v[0],v[2]-v[0]);length = float(np.linalg.norm(cross))
            if length < 1e-9:
                continue
            normal = cross/length;nq = Q@normal[[0,2]]
            if abs(normal[1])<1e-4 and nq[0]>.85 and -.6<nq[1]<-.45:
                candidates.append({"sm":sm,"tri":k//3,"ids":ids,"v":v,"n":nq,"area":length/2})
    largest = max(candidates,key=lambda r:r["area"])
    n = largest["n"];t = np.array([-n[1],n[0]])
    faces = [r for r in candidates if float(r["n"]@n)>.99999]
    faceV = np.concatenate([r["v"] for r in faces]);faceQ = faceV[:,[0,2]]@Q.T
    s0,s1 = float(min(faceQ@t)),float(max(faceQ@t));d0=float(np.median(faceQ@n))
    original_s1 = s1
    if target == "1327299834":
        s1 = min(s1,(wc_qy_finish_limit-n[1]*d0)/t[1])
    normal_xz = Q.T@n;normal_world = np.array([normal_xz[0],0.,normal_xz[1]])
    tangent_xz = Q.T@t;tangent_world = np.array([tangent_xz[0],0.,tangent_xz[1]])
    phase_s=float(phase_anchor_q@t)
    verts,uv,normals,tris=[],[],[],[]

    def at(ss,dd,yy):
        xz=Q.T@(t*ss+n*dd)
        return np.array([xz[0],yy,xz[1]])

    def quad(points,wanted,coords):
        start=len(verts);cross=np.cross(points[1]-points[0],points[2]-points[0]);nn=cross/np.linalg.norm(cross)
        ids=[0,1,2,0,2,3]
        if float(nn@wanted)<0:
            nn=-nn;ids=[0,2,1,0,3,2]
        verts.extend(p.tolist() for p in points);uv.extend(coords);normals.extend([nn.tolist()]*4)
        tris.extend(start+i for i in ids)

    def front(a,b,y0,y1,dd):
        quad([at(a,dd,y0),at(b,dd,y0),at(b,dd,y1),at(a,dd,y1)],normal_world,
             [[a-s0,y0-7],[b-s0,y0-7],[b-s0,y1-7],[a-s0,y1-7]])

    front(s0,s1,plain_y0,rib_y0,d0+SKIN)
    front(s0,s1,rib_y0,top,d0+SKIN)
    first_index=math.ceil((s0+MARGIN+width/2-phase_s)/pitch)
    a=phase_s+first_index*pitch-width/2
    ribs=0
    while a+width<=s1-MARGIN+1e-10:
        b=a+width;low=d0+SKIN+gap;high=low+depth
        front(a,b,rib_y0,rib_y1,high)
        quad([at(b,low,rib_y0),at(a,low,rib_y0),at(a,low,rib_y1),at(b,low,rib_y1)],-normal_world,
             [[0,0],[width,0],[width,rib_y1-rib_y0],[0,rib_y1-rib_y0]])
        quad([at(a,low,rib_y0),at(a,high,rib_y0),at(a,high,rib_y1),at(a,low,rib_y1)],-tangent_world,
             [[0,0],[depth,0],[depth,rib_y1-rib_y0],[0,rib_y1-rib_y0]])
        quad([at(b,high,rib_y0),at(b,low,rib_y0),at(b,low,rib_y1),at(b,high,rib_y1)],tangent_world,
             [[0,0],[depth,0],[depth,rib_y1-rib_y0],[0,rib_y1-rib_y0]])
        quad([at(a,low,rib_y1),at(a,high,rib_y1),at(b,high,rib_y1),at(b,low,rib_y1)],np.array([0.,1.,0.]),
             [[0,0],[0,depth],[width,depth],[width,0]])
        quad([at(a,high,rib_y0),at(a,low,rib_y0),at(b,low,rib_y0),at(b,high,rib_y0)],np.array([0.,-1.,0.]),
             [[0,0],[0,depth],[width,depth],[width,0]])
        ribs+=1;a+=pitch
    support=[]
    for sm in range(len(g["submesh_triangles"])):
        selected=[]
        for r in faces:
            if r["sm"]!=sm or r["v"][:,1].max()<plain_y0 or r["v"][:,1].min()>top:
                continue
            ss=(r["v"][:,[0,2]]@Q.T)@t
            if min(ss)<=s1+1e-6 and max(ss)>=s0-1e-6:
                selected.append(r["tri"])
        if selected:
            support.append({"submesh":sm,"triangle_indices_in_submesh":selected,
                            "clip_s_interval":[s0,s1],"clip_world_y":[plain_y0,top],
                            "support_references_only_no_original_triangles_removed":True})
    entry={"target_owner_id":owner["object_id"],"target_owner_path":owner["path"],
        "renderer_name":"R18_NW_NEXT_SHARED_UPPER_BAND_"+target,
        "new_mesh_geometry_space":"WORLD; preserve world placement when parenting",
        "new_renderer_has_collider":False,"materials":[material],
        "geometry":{"vertices_world":verts,"uv":uv,"normals_world":normals,"submesh_triangles":[tris]},
        "rib_count":ribs,"new_triangle_count":len(tris)//3,
        "original_owner_mesh_materials_colliders_TRS_unchanged":True}
    detail={"object_id":owner["object_id"],"path":owner["path"],"mesh_asset":owner["mesh_asset"],
        "mesh_object_id":owner["mesh_object_id"],"world_matrix":owner["world_matrix"],
        "original_geometry_sha256_canonical_json":sha(json.dumps(g,sort_keys=True,separators=(",", ":")).encode()),
        "public_normal_q":n.tolist(),"public_normal_world":normal_world.tolist(),"tangent_q":t.tolist(),
        "plane_equation":"dot(public_normal_q,q)=plane_d","plane_d":d0,
        "s_interval":[s0,s1],"original_full_face_s_interval":[s0,original_s1],
        "selected_a_q":(t*s0+n*d0).tolist(),"selected_b_q":(t*s1+n*d0).tolist(),
        "selected_length_model_m":s1-s0,"withheld_end_length_model_m":original_s1-s1,
        "basis_triangle":{"submesh":largest["sm"],"triangle_index_in_submesh":largest["tri"],
            "vertex_indices":largest["ids"],"vertices_world":largest["v"].tolist(),
            "uv":[g["uv"][i] for i in largest["ids"]]},
        "selected_support_triangles":support,"rib_count":ribs,"new_triangle_count":len(tris)//3,
        "public_side_qualification":"Actual long face points toward the same open hall side as preceding target1922317721, with positive qX/negative qY normal. Existing columns/caps determine its endpoints; a guide box does not.",
        "source_extent_qualification":"Only shared upper fascia/flute architecture; no assertion this full span is an individual tenant.",
        "WC_end_exclusion_applied":target=="1327299834"}
    return entry,detail


built=[build(s) for s in TARGETS]
payload={"schema":"chooguard.r18.next_shared_band.append_only.v1","status":"PREPARED_OFFLINE_NOT_APPLIED",
    "source_input":rel(SOURCE),"source_input_sha256":SOURCE_SHA,
    "prerequisite_first_payload":rel(first_path),"prerequisite_first_payload_sha256":FIRST_SHA,
    "actual_donor_profile":rel(profile_path),"actual_donor_profile_sha256":sha(profile_path.read_bytes()),
    "new_renderers":[r[0] for r in built],"original_walls_and_colliders_unchanged":True,
    "no_original_assets_native_or_Scene_mutation_by_this_builder":True}
payload_path=OUT/"NEXT-BAND-MESH-PAYLOAD-R18.json"
payload_raw=(json.dumps(payload,ensure_ascii=False,separators=(",", ":"))+"\n").encode();payload_path.write_bytes(payload_raw)
current_photo_path=ROOT/".state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/root/american-trailer-20261006-original-provenance.json"
current_photo=json.loads(current_photo_path.read_text())
first_packet=json.loads((NW/"CONSTRUCTION-PACKET.json").read_text())
packet={"schema":"chooguard.r18.next_shared_western_band.v1","status":"READY_TWO_FINITE_UPPER_PUBLIC_FACES_ONLY",
    "utc":datetime.now(timezone.utc).isoformat(),"source_input":{"path":rel(SOURCE),"sha256":SOURCE_SHA},
    "targets":[r[1] for r in built],
    "structural_chain":{"preceding_first_owner":"1922317721","first_payload_frozen_sha256":FIRST_SHA,
        "fixed_column":"GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-248739979-0",
        "chain":"First front ends at the south face of column248739979. The short new face starts immediately beyond the column's opposite face; the next long current face continues toward the pharmacy/service return. Original bevel/cap joins are preserved.",
        "column_and_existing_original_wall_axes_endpoints_unchanged":True,
        "phase_anchor_from_last_prism_of_first_frozen_payload_world":phase_anchor_world.tolist(),
        "all_phase_width_depth_heights_are_existing_MODEL_geometry_not_field_measured":True},
    "profile":{"donor_object_id":profile["donor_object_id"],"donor_profile_reference":rel(profile_path),
        "donor_profile_sha256":sha(profile_path.read_bytes()),"pitch_model_m":pitch,"rib_width_model_m":width,
        "rib_full_depth_model_m":depth,"backing_gap_model_m":gap,"skin_offset_model_m":SKIN,
        "plain_fascia_world_y":[plain_y0,rib_y0],"rib_world_y":[rib_y0,rib_y1],"plain_cap_world_y":[rib_y1,top],
        "max_public_projection_model_m":SKIN+gap+depth,"end_margin_model_m":MARGIN,
        "material_asset":material},
    "literal_shared_shape_sources":{"current_2026_primary":current_photo,
        "current_source_seen":"Actual October2026 AmericanTrailer plain charcoal fascia plus dense vertical flutes. Four black grilles are visible, but their metric phase and individual tenant allocation remain unqualified; no grille is added.",
        "historical_actual_branch_photos":first_packet["source_photos"],
        "current_pharmacy_epoch_limit":"The pharmacy front is directly photographed in2023;2026 AmericanTrailer corroborates the shared row profile, not a current individual pharmacy finish measurement.",
        "source_originals_preserved_no_resampling_or_photo_textures":True},
    "WC_transition_protection":{"protected_current_metadata":wc_refs,"conservative_finish_qy_max":wc_qy_finish_limit,
        "rule":"Use projected current metadata bounds only as a conservative no-finish exclusion; do not infer a room boundary. Final segment of1327299834 remains untouched pending exact WC surface data.",
        "all_WC_geometry_materials_fixtures_and_owned_walls_unchanged":True},
    "construction":{"operation":"Add two finite opaque upper finish children only; keep original owner renderers/meshes/materials/colliders/UV/TRS intact.",
        "lower_counters_glass_doors_entries_tenant_splits_unchanged":True,"rear_end_and_neighbor_owners_unchanged":True,
        "no_new_entry_glass_grille_sign_or_floor_change":True,"new_finish_top":top},
    "payload":{"path":rel(payload_path),"sha256":sha(payload_raw),
        "new_renderer_count":2,"new_triangle_count":sum(r[0]["new_triangle_count"] for r in built),
        "builder":rel(Path(__file__).resolve())},
    "pending":["Individual tenant split/counter/glass/entry profiles remain unqualified and untouched.",
        "Four AmericanTrailer vents are not propagated across current wall spans.",
        "The final WC-adjacent end interval of1327299834 requires exact current WC face data before any finish continuation."],
    "protected":["first payload and original owner1922317721","donut67985992","rear259783080","all native/Native6/1F/WC","fixed columns/EVs","R17 floor/openings","all user deletions"],
    "new_source_download_count_by_this_lane":0,"new_source_download_bytes_by_this_lane":0,
    "Scene_Assets_UI_native_changes":False,"QA_tests_probes_reopen_captures":False,
    "applied_or_saved":False,"all2F_complete":False,"99percent_achieved_or_measured":False}
(OUT/"CONSTRUCTION-PACKET.json").write_text(json.dumps(packet,ensure_ascii=False,indent=2)+"\n")
(OUT/"task_plan.md").write_text('# Next shared western upper band\n\n- [x] Qualify actual public long faces and column/cap chain.\n- [x] Limit the WC-adjacent far end conservatively; no WC geometry touched.\n- [x] Prepare independent append-only two-renderer payload using exact donor profile/continued phase and currentOctober2026 source corroboration.\n\nFirst152ecb payload was read only and remains frozen. Stop at this packet; root owns native integration.\n')
(OUT/"progress.md").write_text('# Progress\n\nCompleted two finite upper-band faces. Original lower/side/back/top surfaces, colliders and all WC ownership retained. The last WC-adjacent interval is withheld. No new downloads or native/Assets/Scene/QA writes.\n')
(OUT/"findings.md").write_text('# Findings\n\nThe two selected public long faces continue the same structural row after fixed column248739979. They use actual face triangles/endpoints and the extracted adjacent donor profile, with continued first-payload rib phase. CurrentOctober2026 AmericanTrailer original confirms the plain charcoal fascia/dense vertical rib morphology; source vents and lower tenants remain unqualified. Historical pharmacy source supports shared profile only. The final WC-adjacent portion of1327299834 is excluded using conservative current metadata bounds, pending exact owned WC surface data. Neither the first payload nor any current object/source Asset was modified.\n')
print(json.dumps({"status":packet["status"],"faces":[{"owner":s,"length_model_m":r[1]["selected_length_model_m"],
    "withheld_end_model_m":r[1]["withheld_end_length_model_m"],"ribs":r[0]["rib_count"]} for s,r in zip(TARGETS,built)],
    "payload":rel(payload_path),"payload_sha256":sha(payload_raw),"new_triangles":packet["payload"]["new_triangle_count"]},ensure_ascii=False))
