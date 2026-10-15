#!/usr/bin/env python3
"""Export preserved station source components, never fabricate surveyed placements.

Inventory runs in ordinary Python; export runs once in Blender 4.5.9. The output
contains front-face tessellation from the archived source and source-qualified IDs.
"""
from __future__ import annotations
import argparse
from array import array
from collections import Counter, defaultdict
import hashlib
import json
import math
from pathlib import Path
import sys
import time

MAIN_PREFIXES = ("ROOT/g56", "ROOT/g105")
CIRCULATION_PREFIX = "ROOT/g58"

def sha256(path):
    d = hashlib.sha256()
    with Path(path).open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""): d.update(block)
    return d.hexdigest()

def write_json(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

def beneath(path, prefix):
    return path == prefix or path.startswith(prefix + "/")

def classify(row):
    """Naming is evidence of source identity, never proof of motion or site epoch."""
    name = row.get("definition_name", "")
    if name.startswith(("문_역#", "문_역.M#", "문A_역#", "문A_역#", "문(철)")):
        return "door_component"
    if name.startswith("문Part#"): return "door_subcomponent"
    if name.startswith("문_자동문버튼#"): return "automatic_door_button"
    if name.startswith("문_자동문센서#"): return "automatic_door_sensor"
    if name.startswith("문_부속.손잡이#") or name.startswith("손잡이#"): return "handle_source_component"
    if name.startswith("소화전"): return "fire_hydrant_component"
    if name.startswith("송수구"): return "fire_inlet_component"
    if name.startswith("전기_콘센트#"): return "electrical_outlet_component"
    if name.startswith("전기_분전반#"): return "distribution_board_component"
    if name.startswith("스피커_역#"): return "speaker_component"
    if name.startswith("벤치_부속#"): return "bench_support_component"
    if name.startswith("에스컬_"): return "escalator_" + ("balustrade_component" if "난간" in name else "entrance_component")
    return None

def inventory(args):
    base = args.extraction.resolve()
    manifest = json.loads((base / "manifest.json").read_text(encoding="utf-8"))
    rows = [json.loads(line) for line in (base / manifest["objects"]["path"]).open(encoding="utf-8")]
    by_id = {row["source_id"]: row for row in rows}
    children = defaultdict(list)
    for row in rows: children[row["parent_id"]].append(row)
    primary = [row for row in rows if any(beneath(row["source_path"], prefix) for prefix in MAIN_PREFIXES)]
    circulation = [row for row in rows if beneath(row["source_path"], CIRCULATION_PREFIX) and classify(row) is not None]
    wanted = {row["source_id"] for row in primary}
    def descendants(row):
        yield row
        for child in children[row["source_id"]]: yield from descendants(child)
    for row in circulation:
        wanted.update(child["source_id"] for child in descendants(row))
    for identity in tuple(wanted):
        row = by_id[identity]
        while row["parent_id"]:
            row = by_id[row["parent_id"]]
            wanted.add(row["source_id"])
    selected = [row for row in rows if row["source_id"] in wanted]
    semantics = []
    for row in selected:
        category = classify(row)
        if category is None: continue
        descendants_list = list(descendants(row))
        mesh_ids = [child["source_id"] for child in descendants_list if child["mesh_id"]]
        semantics.append({"source_id": row["source_id"], "source_path": row["source_path"],
                          "source_definition": row.get("definition_name"), "category": category,
                          "evidence": "archived_source_definition_name_and_preserved_hierarchy",
                          "confidence": "source_identity_supported_motion_unverified",
                          "production_name": "N_" + row["source_id"], "hidden": row["effective_hidden"],
                          "local_matrix": row["local_matrix"], "world_matrix": row["world_matrix"],
                          "descendant_mesh_ids": mesh_ids, "mesh_fragments": len(mesh_ids),
                          "mechanical_pivot_verified": False, "site_position_verified": False})
    plan = {"schema": "chooguard.source-operable-parts.v1", "physical_accuracy_verified": False,
            "all_interior_objects_complete": False, "source_manifest_sha256": sha256(base / "manifest.json"),
            "source_sha256": manifest["source"]["sha256"], "source_blend_sha256": manifest["blend"]["sha256"],
            "scope": "Preserved station body/upper concourse source plus named platform circulation/door/utility components; surrounding buildings excluded",
            "coordinate_system": manifest["coordinate_system"],
            "semantic_contract": "Categories identify source names. A door component is not automatically a leaf. Bench supports are not counted as complete benches. Motion pivots and current real installations remain unverified.",
            "counts": {"selected_objects": len(selected), "selected_mesh_instances": sum(r["mesh_id"] is not None for r in selected),
                       "semantic_source_components": len(semantics), "categories": dict(Counter(s["category"] for s in semantics))},
            "objects": selected, "semantic_components": semantics}
    args.output.mkdir(parents=True, exist_ok=True)
    write_json(args.output / "selection.json", plan)
    print(json.dumps(plan["counts"], ensure_ascii=False), flush=True)
    return plan, manifest

def export(args):
    import bpy
    from mathutils import Matrix, Vector
    start = time.monotonic()
    plan, manifest = inventory(args)
    base = args.extraction.resolve()
    source_blend = base / manifest["blend"]["path"]
    if sha256(source_blend) != manifest["blend"]["sha256"]:
        raise ValueError("Archived blend digest changed")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    rows = plan["objects"]
    wanted_names = {row["blender_name"] for row in rows}
    print("OPERABLE_PARTS_LOAD", len(wanted_names), flush=True)
    with bpy.data.libraries.load(str(source_blend), link=False) as (available, loaded):
        missing = wanted_names - set(available.objects)
        if missing: raise ValueError("Missing source objects: " + str(sorted(missing)[:5]))
        loaded.objects = sorted(wanted_names)
    objects = {obj["source_id"]: obj for obj in loaded.objects if obj}
    collection = bpy.data.collections.new("Busan station preserved parts")
    bpy.context.scene.collection.children.link(collection)
    mesh_records, max_local_error, world_bounds = [], 0.0, []
    computed_world = {}
    for row in rows:
        obj = objects[row["source_id"]]
        collection.objects.link(obj)
        obj.name = "N_" + row["source_id"]
        local = obj.matrix_parent_inverse @ obj.matrix_basis
        error = max(abs(local[r][c] - row["local_matrix"][r][c]) for r in range(4) for c in range(4))
        max_local_error = max(error, max_local_error)
        parent = objects.get(row["parent_id"])
        if obj.parent != parent: raise ValueError("Preserved parent differs: " + row["source_id"])
        world = computed_world[row["parent_id"]] @ local if row["parent_id"] else local
        computed_world[row["source_id"]] = world
        obj["source_semantic_category"] = classify(row) or "unclassified_source_geometry"
        obj["mechanical_pivot_verified"] = False
        if obj.type == "MESH":
            mesh = obj.data
            if any(len(poly.vertices) != 3 for poly in mesh.polygons): raise ValueError("Non-triangulated source mesh")
            if len(mesh.uv_layers.active.data) != len(mesh.loops): raise ValueError("Missing source UV")
            bounds = [world @ Vector(corner) for corner in obj.bound_box]
            minimum = [min(v[i] for v in bounds) for i in range(3)]
            maximum = [max(v[i] for v in bounds) for i in range(3)]
            if not all(math.isfinite(v) for v in minimum + maximum): raise ValueError("Non-finite source bounds")
            mesh_records.append({"source_id": row["source_id"], "source_mesh_id": row["mesh_id"],
                                 "export_name": obj.name, "vertices": len(mesh.vertices), "triangles": len(mesh.polygons),
                                 "uv_loops": len(mesh.loops), "world_min": minimum, "world_max": maximum,
                                 "world_pivot": list(world.translation), "material_slots": [m.name for m in mesh.materials]})
            world_bounds.extend(bounds)
        # Export hidden source geometry as geometry with explicit visibility metadata.
        # Unity installer, not this exporter, chooses whether to show hidden parts.
        obj["source_effective_hidden"] = row["effective_hidden"]
        obj.hide_viewport = False
        obj.hide_render = False
    if max_local_error > .001: raise ValueError("Source local matrices changed")
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    bpy.context.view_layer.update()
    staged = args.output / "station-operable-source.blend"
    print("OPERABLE_PARTS_SAVE", len(objects), flush=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(staged), compress=True)
    output_fbx = args.output / "station-operable-source.fbx"
    print("OPERABLE_PARTS_FBX", flush=True)
    bpy.ops.export_scene.fbx(filepath=str(output_fbx), use_selection=False, object_types={"MESH", "EMPTY"},
        global_scale=1, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
        bake_space_transform=False, use_mesh_modifiers=False, mesh_smooth_type="FACE", use_custom_props=True,
        path_mode="COPY", embed_textures=True, bake_anim=False, use_triangles=True)
    plan.pop("objects")
    plan["export"] = {"blend": {"path": staged.name, "bytes": staged.stat().st_size, "sha256": sha256(staged)},
                      "fbx": {"path": output_fbx.name, "bytes": output_fbx.stat().st_size, "sha256": sha256(output_fbx)},
                      "blender_version": bpy.app.version_string, "script_sha256": sha256(__file__),
                      "format": "FBX binary, unit scale metres, Blender Z up to FBX Y up/-Z forward; no world registration transform",
                      "max_local_matrix_entry_error": max_local_error, "elapsed_seconds": time.monotonic() - start,
                      "source_hidden_geometry_included": True,
                      "mesh_instances": mesh_records}
    plan["world_bounds"] = {"min": [min(v[i] for v in world_bounds) for i in range(3)],
                            "max": [max(v[i] for v in world_bounds) for i in range(3)]}
    write_json(args.output / "manifest.json", plan)
    print("OPERABLE_PARTS_COMPLETE", json.dumps({"counts": plan["counts"], "bytes": output_fbx.stat().st_size}), flush=True)

def verify(args, candidates=False):
    """Independently re-import the Unity-readable FBX and compare source world geometry."""
    import bpy
    from mathutils import Matrix, Vector
    start = time.monotonic()
    plan = json.loads((args.output / "manifest.json").read_text(encoding="utf-8"))
    source = json.loads((args.output / ("candidate-manifest.json" if candidates else "selection.json")).read_text(encoding="utf-8"))
    file_record = source["file"] if candidates else plan["export"]["fbx"]
    expected_objects = source["counts"]["objects"] if candidates else source["counts"]["selected_objects"]
    fbx = args.output / file_record["path"]
    if sha256(fbx) != file_record["sha256"]: raise ValueError("FBX digest changed")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    print("OPERABLE_PARTS_REIMPORT", str(fbx), flush=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx), use_custom_normals=True, use_custom_props=True, use_image_search=False)
    by_name = {obj.name: obj for obj in bpy.context.scene.objects}
    imported = {obj.get("source_id"): obj for obj in bpy.context.scene.objects if obj.get("source_id")}
    if len(imported) != expected_objects:
        raise ValueError("FBX source IDs/hierarchy objects differ: " + str(len(imported)))
    max_pivot_error, max_bound_error, checked, errors = 0., 0., 0, []
    differing_source_meshes = []
    source_meshes = {row["source_id"]: row for row in plan["export"]["mesh_instances"]}
    component_geometry = {}
    bpy.context.view_layer.update()
    for row in source["objects"]:
        obj = imported[row["source_id"]]
        if obj.name != "N_" + row["source_id"]: errors.append("identity_name:" + row["source_id"])
        if (obj.parent.get("source_id") if obj.parent else None) != row["parent_id"]:
            errors.append("parent:" + row["source_id"])
        expected = Vector([row["world_matrix"][i][3] for i in range(3)])
        actual = obj.matrix_world.translation
        max_pivot_error = max(max_pivot_error, (expected - actual).length)
        if obj.type != "MESH": continue
        mesh_row = source_meshes[row["source_id"]]
        obj.data.calc_loop_triangles()
        if len(obj.data.loop_triangles) != mesh_row["triangles"]:
            errors.append("triangles:" + row["source_id"])
            differing_source_meshes.append({"source_id": row["source_id"], "source_mesh_id": row["mesh_id"],
                "source_triangles": mesh_row["triangles"], "reimported_triangles": len(obj.data.loop_triangles),
                "source_uv_loops": mesh_row["uv_loops"], "reimported_uv_loops": len(obj.data.loops)})
        if not obj.data.uv_layers.active or len(obj.data.uv_layers.active.data) != mesh_row["uv_loops"]:
            errors.append("uv_count:" + row["source_id"])
        bounds = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
        lo = [min(v[i] for v in bounds) for i in range(3)]
        hi = [max(v[i] for v in bounds) for i in range(3)]
        max_bound_error = max(max_bound_error, max(abs(a-b) for a,b in zip(lo + hi, mesh_row["world_min"] + mesh_row["world_max"])))
        checked += 1
    for row in plan["semantic_components"]:
        obj = imported[row["source_id"]]
        inv = obj.matrix_world.inverted_safe()
        points = []
        for mesh_id in row["descendant_mesh_ids"]:
            mesh_obj = imported[mesh_id]
            points.extend(inv @ mesh_obj.matrix_world @ Vector(corner) for corner in mesh_obj.bound_box)
        if points:
            minimum = [min(v[i] for v in points) for i in range(3)]
            maximum = [max(v[i] for v in points) for i in range(3)]
            component_geometry[row["source_id"]] = {"source_definition": row["source_definition"], "category": row["category"],
                "local_bounds": {"min": minimum, "max": maximum}, "local_dimensions": [maximum[i]-minimum[i] for i in range(3)],
                "source_pivot_local_origin": [0,0,0], "unity_node": obj.name, "triangles": sum(len(imported[mid].data.loop_triangles) for mid in row["descendant_mesh_ids"])}
    result = {"schema": "chooguard.source-operable-fbx-qa.v1", "physical_accuracy_verified": False,
              "reimported_objects": len(imported), "checked_mesh_instances": checked,
              "max_world_pivot_error_m": max_pivot_error, "max_world_aabb_coordinate_error_m": max_bound_error,
              "all_source_parents_match": not any(e.startswith("parent:") for e in errors),
              "triangles_uv_loop_counts_match": not any(e.startswith(("triangles:", "uv_count:")) for e in errors),
              "id_names_match": not any(e.startswith("identity_name:") for e in errors),
              "materials": len(bpy.data.materials), "images": len(bpy.data.images),
              "errors": errors, "differing_source_meshes": differing_source_meshes, "component_geometry": component_geometry, "elapsed_seconds": time.monotonic()-start,
              "passed": not errors and max_pivot_error <= .002 and max_bound_error <= .002,
              "accuracy_scope": "FBX transport/source transform preservation only; no current station physical survey"}
    write_json(args.output / ("candidate-qa.json" if candidates else "qa.json"), result)
    print("OPERABLE_PARTS_QA", json.dumps({k:v for k,v in result.items() if k not in ("component_geometry", "errors", "differing_source_meshes")}), flush=True)
    if candidates:
        if not result["passed"]: raise ValueError("Candidate FBX transport QA failed; see candidate-qa.json")
        return
    if not result["all_source_parents_match"] or not result["id_names_match"] or max_pivot_error > .002 or max_bound_error > .002:
        raise ValueError("Source transform transport QA failed")
    # A practical runtime import: only source-named candidate components, their
    # descendants and exact source parents. Structural source is retained in full FBX.
    children = defaultdict(list)
    all_rows = {r["source_id"]: r for r in source["objects"]}
    for row in source["objects"]: children[row["parent_id"]].append(row["source_id"])
    selected_ids = set()
    def add_tree(identity):
        if identity in selected_ids: return
        selected_ids.add(identity)
        for child in children[identity]: add_tree(child)
    for row in source["semantic_components"]: add_tree(row["source_id"])
    for identity in tuple(selected_ids):
        while all_rows[identity]["parent_id"]:
            identity = all_rows[identity]["parent_id"]
            selected_ids.add(identity)
    candidate_errors = [e for e in errors if e.split(":",1)[-1] in selected_ids]
    if candidate_errors: raise ValueError("Candidate source geometry changed: " + str(candidate_errors[:5]))
    bpy.ops.object.select_all(action="DESELECT")
    for identity in selected_ids: imported[identity].select_set(True)
    slim = args.output / "station-operable-candidates.fbx"
    bpy.ops.export_scene.fbx(filepath=str(slim), use_selection=True, object_types={"MESH", "EMPTY"},
        global_scale=1, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
        bake_space_transform=False, use_mesh_modifiers=False, mesh_smooth_type="FACE", use_custom_props=True,
        path_mode="COPY", embed_textures=True, bake_anim=False, use_triangles=True)
    write_json(args.output / "candidate-manifest.json", {
        "schema": "chooguard.source-operable-candidates.v1", "physical_accuracy_verified": False,
        "status": "SOURCE_PARTS_WITHOUT_CURRENT_SITE_CERTIFICATION",
        "source_sha256": source["source_sha256"], "source_blend_sha256": source["source_blend_sha256"],
        "file": {"path": slim.name, "sha256": sha256(slim), "bytes": slim.stat().st_size},
        "objects": [r for r in source["objects"] if r["source_id"] in selected_ids],
        "semantic_components": source["semantic_components"], "component_geometry": component_geometry,
        "counts": {"objects": len(selected_ids), "mesh_instances": sum(all_rows[i]["mesh_id"] is not None for i in selected_ids),
                   "semantic_components": len(source["semantic_components"])},
        "world_source_transport_qa": "qa.json",
        "strict_candidate_transport_qa": "candidate-qa.json",
        "source_background_mesh_count_differences": len(differing_source_meshes),
        "production_installed": False,
        "installation_contract": "Match archived source frame to existing geometry and suppress corresponding batched faces before activation; no arbitrary placement or additional surveyed-equipment claim"})
    print("OPERABLE_PARTS_CANDIDATES", len(selected_ids), slim.stat().st_size, flush=True)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("operation", choices=("inventory", "export", "verify", "verify_candidates"))
    p.add_argument("--extraction", type=Path, required=True)
    p.add_argument("--output", type=Path, required=True)
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    args = p.parse_args(argv)
    if args.operation == "export": export(args)
    elif args.operation == "verify": verify(args)
    elif args.operation == "verify_candidates": verify(args, candidates=True)
    else: inventory(args)

if __name__ == "__main__": main()
