"""Correct the three new finish meshes only; frozen inputs stay untouched.

The first new append mesh is already applied. Root must replace that append
mesh only, never the original base wall. The other two are bounded additions.
"""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import math

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[4]
NW = OUT.parent
MIN_Y = 9.38
FIRST_SHA = "152ecb4024dec9f374a4159676aaf0fb2961733b26c4aaa331ab016ead8eb2c5"
NEXT_SHA = "05a2a8e9683e2f6c709351be5c51ce0413abe4226b6018a3f0e37237c3fb5cf0"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def rel(path):
    return str(path.relative_to(ROOT))


def read_frozen(path, expected):
    raw = path.read_bytes()
    if sha(raw) != expected:
        raise RuntimeError("Frozen input identity changed: "+str(path))
    return json.loads(raw)


first_path = NW / "UPPER-BAND-MESH-PAYLOAD-R18.json"
next_path = NW / "next-band/NEXT-BAND-MESH-PAYLOAD-R18.json"
first = read_frozen(first_path,FIRST_SHA)
following = read_frozen(next_path,NEXT_SHA)


def clip_geometry(g):
    old_v = g["vertices_world"]
    old_uv = g["uv"]
    old_n = g["normals_world"]
    out_v,out_uv,out_n=[],[],[]
    cache={}
    stats={"fully_retained_source_triangles":0,"fully_removed_source_triangles":0,
           "clipped_source_triangles":[],"created_clip_vertices":0}

    def original(ix):
        return {"key":("original",ix),"p":old_v[ix],"uv":old_uv[ix],"n":old_n[ix],"original":ix}

    def intersection(a,b):
        lo,hi=sorted([a["original"],b["original"]])
        key=("clip",lo,hi,MIN_Y)
        t=(MIN_Y-a["p"][1])/(b["p"][1]-a["p"][1])
        p=[a["p"][k]+t*(b["p"][k]-a["p"][k]) for k in range(3)]
        p[1]=MIN_Y
        uv=[a["uv"][k]+t*(b["uv"][k]-a["uv"][k]) for k in range(2)]
        normal=[a["n"][k]+t*(b["n"][k]-a["n"][k]) for k in range(3)]
        length=math.sqrt(sum(v*v for v in normal))
        normal=[v/length for v in normal]
        return {"key":key,"p":p,"uv":uv,"n":normal}

    def emit(v):
        key=v["key"]
        if key not in cache:
            cache[key]=len(out_v);out_v.append(v["p"]);out_uv.append(v["uv"]);out_n.append(v["n"])
            if key[0]=="clip":stats["created_clip_vertices"]+=1
        return cache[key]

    out_submeshes=[]
    for sm,indices in enumerate(g["submesh_triangles"]):
        out_indices=[]
        for off in range(0,len(indices),3):
            ids=indices[off:off+3];tri=[original(i) for i in ids]
            inside=[v["p"][1]>=MIN_Y for v in tri]
            if all(inside):
                out_indices.extend(emit(v) for v in tri)
                stats["fully_retained_source_triangles"]+=1
                continue
            if not any(inside):
                stats["fully_removed_source_triangles"]+=1
                continue
            polygon=[]
            previous=tri[-1];prev_inside=previous["p"][1]>=MIN_Y
            for current in tri:
                curr_inside=current["p"][1]>=MIN_Y
                if curr_inside!=prev_inside:
                    polygon.append(intersection(previous,current))
                if curr_inside:
                    polygon.append(current)
                previous=current;prev_inside=curr_inside
            first_out=len(out_indices)//3
            for i in range(1,len(polygon)-1):
                out_indices.extend(emit(v) for v in [polygon[0],polygon[i],polygon[i+1]])
            stats["clipped_source_triangles"].append({"submesh":sm,"source_triangle_index":off//3,
                "source_vertex_indices":ids,"output_triangle_start":first_out,
                "output_triangle_count":len(polygon)-2,
                "new_boundary_world_y":MIN_Y,"UV_interpolation":"original edge-linear"})
        out_submeshes.append(out_indices)
    return {"vertices_world":out_v,"uv":out_uv,"normals_world":out_n,
            "submesh_triangles":out_submeshes},stats


entries=[]
inputs=[(first,first_path,FIRST_SHA,True)]+[(r,next_path,NEXT_SHA,False) for r in following["new_renderers"]]
mesh_dir=OUT/"meshes"
mesh_dir.mkdir(parents=True,exist_ok=True)
for row,path,input_sha,is_applied in inputs:
    geometry,stats=clip_geometry(row["geometry"])
    owner=row["target_owner_id"]
    short=owner.split("-")[-2]
    renderer=row.get("new_renderer_name",row.get("renderer_name"))
    entry={"base_wall_owner_id":owner,"base_wall_owner_path":row["target_owner_path"],
        "base_wall_is_not_the_replacement_target":True,
        "operation":"REPLACE_ONLY_ALREADY_APPLIED_NEW_APPEND_FINISH_MESH" if is_applied else "ADD_NEW_FINISH_CHILD_ONLY",
        "existing_append_renderer_name":renderer if is_applied else None,
        "new_renderer_name":renderer+"_BOUNDED_UPPER_R2",
        "new_mesh_geometry_space":"WORLD; preserve exact world placement when parenting",
        "new_renderer_has_collider":False,"materials":row["materials"],"geometry":geometry,
        "source_new_finish_payload":{"path":rel(path),"sha256":input_sha,
            "mesh_from_new_renderers_array":None if is_applied else owner},
        "exact_clip":{"halfspace":"worldY >= 9.38","world_y_min":MIN_Y,
            "scope":"Only the source new finish mesh. No base wall/native/WC/collider is clipped.",
            "all_remaining_source_vertex_positions_UV_normals_preserved":True,
            "intersections":"Exact source triangle edge intersections with interpolated UV/normals.",**stats},
        "rib_profile_unchanged":{"pitch":"exact extracted donor profile already present in frozen input mesh",
            "section":"exact existing width/depth/backing gap retained",
            "world_y":[9.8,12.0],"top_cap_world_y":[12.0,12.0999947]},
        "withdrawn_interval_world_y":[8.12,MIN_Y],
        "old_lower_band_is_withdrawn":True,
        "source_body_UV_materials_TRS_colliders_and_all_other_owners_unchanged":True}
    mesh_path=mesh_dir/(short+"-BOUNDED-UPPER-R2.json")
    data=(json.dumps(entry,ensure_ascii=False,separators=(",", ":"))+"\n").encode();mesh_path.write_bytes(data)
    entry["individual_geometry_file"]={"path":rel(mesh_path),"sha256":sha(data)}
    entries.append(entry)

payload={"schema":"chooguard.r18.bounded_upper_r2.corrective_finish_meshes.v1",
    "status":"PREPARED_NEW_CORRECTIVE_PAYLOAD_NOT_APPLIED",
    "correction":"WITHDRAW_ALL_NEW_FINISH_BELOW_WORLD_Y_9_38",
    "approved_vertical_scope":{"plain_fascia":[9.38,9.8],"actual_donor_ribs":[9.8,12.0],
        "plain_top_cap":[12.0,12.0999947]},
    "new_renderers":entries,
    "only_first_new_append_mesh_is_replaced":True,
    "first_base_wall_owner_1922317721_is_not_replaced_or_trimmed":True,
    "second_and_third_are_new_bounded_finish_meshes_only":True,
    "old_frozen_payloads_unchanged_but_lower_extent_withdrawn":True,
    "original_scene_assets_native_WC_1F_floor_columns_EV_openings_colliders_unchanged_by_builder":True}
payload_path=OUT/"BOUNDED-UPPER-R2-PAYLOAD.json"
payload_bytes=(json.dumps(payload,ensure_ascii=False,separators=(",", ":"))+"\n").encode();payload_path.write_bytes(payload_bytes)
packet={"schema":"chooguard.r18.bounded_upper_r2.correction_packet.v1",
    "status":"READY_CORRECT_FIRST_OWN_APPEND_AND_ADD_TWO_BOUNDED_FINISHES",
    "utc":datetime.now(timezone.utc).isoformat(),
    "error_and_withdrawal":"The donor gray material's global minimum8.12 came from other faces and was incorrectly promoted to the fascia minimum. That lower extent was not authorized. All new finish in8.12<=Y<9.38 is explicitly withdrawn.",
    "authorized_scope":"Shared UPPER architectural band only: Y>=9.38. No lower tenant/frontage/entry edits.",
    "frozen_input_payloads":[{"path":rel(first_path),"sha256":FIRST_SHA},{"path":rel(next_path),"sha256":NEXT_SHA}],
    "frozen_input_files_modified":False,
    "root_application_order":["Resolve the already-applied FIRST own new append renderer by its source name/parent, fresh-read its current native identity, then replace only that finish mesh with corrected geometry1922317721. Do not replace its base wall renderer or collider.",
        "Add bounded new finish children1967088396 and1327299834 from this corrected payload. Retain the existing WC-adjacent0.403m exclusion and all original end/cap joins."],
    "payload":{"path":rel(payload_path),"sha256":sha(payload_bytes),"corrected_mesh_count":len(entries)},
    "meshes":[{"base_owner_id":r["base_wall_owner_id"],"operation":r["operation"],
        "existing_append_renderer_name":r["existing_append_renderer_name"],
        "new_renderer_name":r["new_renderer_name"],"geometry_file":r["individual_geometry_file"],
        "world_y_min":min(v[1] for v in r["geometry"]["vertices_world"]),
        "world_y_max":max(v[1] for v in r["geometry"]["vertices_world"]),
        "new_vertex_count":len(r["geometry"]["vertices_world"]),
        "new_triangle_count":sum(len(t)//3 for t in r["geometry"]["submesh_triangles"]),
        "exact_clip":r["exact_clip"]} for r in entries],
    "preserved":["every base wall and its UV/materials/TRS/colliders", "actual donor67985992", "rear259783080",
        "upper rib pitch/width/depth/gap andY9.8..12.0", "topcapY12.0..12.0999947",
        "lower tenants/counters/glass/entries", "all native/Native6/1F/WC/floor/columns/EVs/deletions"],
    "source_investigation_performed":False,"new_original_downloads":0,
    "Scene_Assets_Unity_UI_changes":False,"QA_tests_probes_captures":False,
    "applied_or_saved":False,"all2F_complete":False,"99percent_achieved_or_measured":False,
    "builder":rel(Path(__file__).resolve())}
(OUT/"CONSTRUCTION-PACKET.json").write_text(json.dumps(packet,ensure_ascii=False,indent=2)+"\n")
(OUT/"task_plan.md").write_text('# Bounded upper R2 correction\n\n- [x] Read frozen new-finish payloads only; leave their bytes unchanged.\n- [x] Exactly clip the three new finish meshes toY>=9.38, preserving source UV and all upper profile geometry.\n- [x] Deliver one corrective payload plus three individual mesh geometries; explicitly withdraw the old lower band.\n\nRoot replaces only the first own append mesh and adds the two new bounded children. No base wall or collider change, source investigation, Unity/Assets/QA work. Stop.\n')
(OUT/"findings.md").write_text('# Correction\n\nThe donor material global minimum8.12 was not a supported fascia height. The old added lower band is withdrawn. The corrected approved plain fascia isY9.38..9.8; actual donor ribs remainY9.8..12.0, with the plain cap to12.0999947. Exact clipping retains every unaffected upper vertex/UV/normal and interpolates only boundary intersections. Original frozen payloads and all original base walls, materials, UV, TRS and colliders are untouched.\n')
(OUT/"progress.md").write_text('# Progress\n\nCompleted new bounded-upper-r2 packet and three corrected new-finish geometries. The first already-applied append mesh is the only replacement target; the base wall is expressly excluded. Lower extent8.12..9.38 is withdrawn. No source investigation or native/Scene/Assets/QA call ran.\n')
print(json.dumps({"payload":rel(payload_path),"payload_sha256":sha(payload_bytes),
    "meshes":[{"owner":r["base_owner_id"].split("-")[-2],"Y":[r["world_y_min"],r["world_y_max"]],
        "triangles":r["new_triangle_count"],"sha256":r["geometry_file"]["sha256"]} for r in packet["meshes"]]},ensure_ascii=False))
