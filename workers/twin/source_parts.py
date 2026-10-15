#!/usr/bin/env python3
"""Stage an editable, unflattened SKP hierarchy using the archived Blender SDK binding.

Run with Blender 4.5 / Python 3.11, never against the production Unity scene.
Source-hash-qualified IDs are stable for this archived model; they are not surveyed asset IDs.
"""
from __future__ import annotations

import argparse
from array import array
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import sys
import tempfile
import time
from urllib.parse import quote


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def write_json(path: Path, value) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def rows(matrix):
    return [[float(value) for value in row] for row in matrix]


def array_hash(values: array) -> str:
    if sys.byteorder != "little":
        values.byteswap()
    return hashlib.sha256(values.tobytes()).hexdigest()


def mesh_receipt(mesh, geometry_id, material, source_faces):
    coords = array("f", [0]) * (len(mesh.vertices) * 3)
    mesh.vertices.foreach_get("co", coords)
    uv = array("f", [0]) * (len(mesh.loops) * 2)
    mesh.uv_layers.active.data.foreach_get("uv", uv)
    if not all(math.isfinite(value) for value in coords) or not all(math.isfinite(value) for value in uv):
        raise ValueError("Non-finite source geometry or UV: " + geometry_id)
    indices = array("i", [0]) * len(mesh.loops)
    mesh.loops.foreach_get("vertex_index", indices)
    materials = array("i", [0]) * len(mesh.polygons)
    mesh.polygons.foreach_get("material_index", materials)
    return {"name": mesh.name, "source_geometry_id": geometry_id, "inherited_material": material,
            "source_faces": source_faces, "vertices": len(mesh.vertices), "triangles": len(mesh.polygons),
            "uv_loops": len(mesh.uv_layers.active.data), "material_slots": [m.name for m in mesh.materials],
            "vertices_sha256": array_hash(coords), "uv_sha256": array_hash(uv),
            "indices_sha256": array_hash(indices), "material_indices_sha256": array_hash(materials)}


def extract(args) -> None:
    import bpy
    from mathutils import Matrix

    source, output, importer = args.source.resolve(), args.output.resolve(), args.importer.resolve()
    if (output / "station-source-parts.blend").exists() or (output / "manifest.json").exists():
        raise FileExistsError("Existing staged extraction is preserved; choose a fresh output directory")
    if not source.is_file() or not (importer / "sketchup_importer").is_dir():
        raise FileNotFoundError("The archived source and isolated SDK importer are required")
    output.mkdir(parents=True, exist_ok=True)
    (output / "temporary-textures").mkdir(exist_ok=True)
    tempfile.tempdir = str(output / "temporary-textures")
    sys.path.insert(0, str(importer))
    import sketchup_importer as si

    start = time.monotonic()
    source_hash = sha256(source)
    si.MIN_LOGS = True
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    model = si.sketchup.Model.from_file(str(source))
    writer = si.SceneImporter().set_filename(str(source))
    writer.context = bpy.context
    writer.reuse_material = True
    writer.write_materials(model.materials)
    collection = bpy.data.collections.new("Official SKP source hierarchy - not surveyed")
    bpy.context.scene.collection.children.link(collection)
    descriptors, mesh_cache, mesh_records, component_names = {}, {}, [], set()
    object_rows, objects = [], {}
    counts = Counter()

    def stable_id(key):
        return hashlib.sha256((source_hash + ":" + key).encode("utf-8")).hexdigest()[:24]

    def material_name(entity, inherited):
        material = entity.material
        return material.name if material is not None else inherited

    def layer_info(entity, inherited):
        layer = entity.layer
        if layer is None or layer.name == "Layer0":
            return inherited
        return (layer.name, bool(layer.visible))

    def describe(entities, geometry_key):
        if geometry_key in descriptors:
            return descriptors[geometry_key]
        result = {"entities": entities, "groups": [], "instances": []}
        descriptors[geometry_key] = result
        for index, group in enumerate(entities.groups):
            result["groups"].append({"source": group, "name": group.name, "index": index,
                                      "key": geometry_key + "/g" + str(index), "transform": group.transform})
        for index, instance in enumerate(entities.instances):
            definition = instance.definition
            component_names.add(definition.name)
            result["instances"].append({"source": instance, "name": instance.name, "index": index,
                                         "entity_id": int(instance.entity.id), "definition": definition,
                                         "key": "DEF/" + quote(definition.name, safe=""), "transform": instance.transform})
        return result

    def geometry(descriptor, geometry_key, inherited):
        # Material inheritance may create a mesh variant, but the variant is reused by every matching instance.
        key = (geometry_key, inherited)
        if key in mesh_cache:
            return mesh_cache[key]
        entities = descriptor["entities"]
        if entities.NumFaces() == 0:
            mesh_cache[key] = None
            return None
        vertices, triangles, uvs, face_materials, smooth = [], [], [], [], []
        vertex_lookup, slot_lookup = {}, {}
        slots = []
        for face in entities.faces:
            material = face.material
            name = material.name if material is not None else inherited
            if name not in slot_lookup:
                slot_lookup[name] = len(slots)
                slots.append(writer.materials.get(name, writer.materials["Material"]))
            if material is None and inherited != "Material":
                face.st_scale = writer.materials_scales.get(inherited, (1.0, 1.0))
            face_vertices, face_triangles, face_uvs = face.tessfaces
            if len(face_vertices) != len(face_uvs):
                raise ValueError("Source tessellation has incomplete front UVs: " + geometry_key)
            mapping = []
            for vertex in face_vertices:
                vertex = tuple(vertex)
                if vertex not in vertex_lookup:
                    vertex_lookup[vertex] = len(vertices)
                    vertices.append(vertex)
                mapping.append(vertex_lookup[vertex])
            is_smooth = any(edge.GetSmooth() for edge in face.edges)
            for triangle in face_triangles:
                triangles.append(tuple(mapping[i] for i in triangle))
                uvs.extend(value for i in triangle for value in face_uvs[i])
                face_materials.append(slot_lookup[name])
                smooth.append(is_smooth)
        mesh_id = stable_id("MESH/" + geometry_key + "/" + inherited)
        mesh = bpy.data.meshes.new("SKP_mesh_" + mesh_id)
        mesh.from_pydata(vertices, [], triangles)
        for material in slots:
            mesh.materials.append(material)
        mesh.polygons.foreach_set("material_index", face_materials)
        mesh.polygons.foreach_set("use_smooth", smooth)
        mesh.uv_layers.new(name="SourceFrontUV")
        mesh.uv_layers.active.data.foreach_set("uv", uvs)
        mesh.update()
        mesh["source_geometry_key"] = geometry_key
        mesh["source_mesh_id"] = mesh_id
        mesh["source_sha256"] = source_hash
        record = mesh_receipt(mesh, stable_id("GEOMETRY/" + geometry_key), inherited, entities.NumFaces())
        record["source_mesh_id"] = mesh_id
        record["source_geometry_key"] = geometry_key
        mesh_records.append(record)
        mesh_cache[key] = mesh
        return mesh

    def add_object(path, name, kind, parent, local, world, mesh=None, hidden=False, layer="Layer0", **extra):
        identity = stable_id("OBJECT/" + path)
        if identity in objects:
            raise ValueError("Duplicate source path ID: " + path)
        display_name = extra.pop("display_name", name or kind)
        obj = bpy.data.objects.new(display_name[:36] + "__" + identity[:12], mesh)
        collection.objects.link(obj)
        obj.parent = parent
        # TRS is directly editable. Retain any source shear exactly in parent-inverse rather than dropping it.
        location, rotation, scale = local.decompose()
        rebuilt = Matrix.LocRotScale(location, rotation, scale)
        matrix_error = max(abs(local[r][c] - rebuilt[r][c]) for r in range(4) for c in range(4))
        affine = matrix_error > 1e-5
        obj.matrix_parent_inverse = local if affine else Matrix.Identity(4)
        obj.matrix_basis = Matrix.Identity(4) if affine else local
        obj.hide_viewport = obj.hide_render = bool(hidden)
        obj["source_id"] = identity
        obj["source_path"] = path
        obj["source_name"] = name
        obj["source_kind"] = kind
        obj["source_layer"] = layer
        obj["source_sha256"] = source_hash
        obj["physical_accuracy_verified"] = False
        if mesh is None:
            obj.empty_display_type = "PLAIN_AXES"
            obj.empty_display_size = .05
        row = {"source_id": identity, "source_path": path, "source_name": name, "kind": kind,
               "blender_name": obj.name, "parent_id": parent.get("source_id") if parent else None,
               "source_layer": layer, "effective_hidden": bool(hidden), "local_matrix": rows(local),
               "world_matrix": rows(world), "transform_storage": "parent_inverse_affine" if affine else "editable_TRS",
               "mesh_id": mesh.get("source_mesh_id") if mesh else None, **extra}
        objects[identity] = obj
        object_rows.append(row)
        counts[kind] += 1
        if affine:
            counts["affine_transforms"] += 1
        return obj

    def expand(entities, geometry_key, path, parent, world, inherited="Material", layer=("Layer0", True), hidden=False, stack=()):
        if len(stack) > 64:
            raise ValueError("Unexpected source hierarchy depth")
        descriptor = describe(entities, geometry_key)
        mesh = geometry(descriptor, geometry_key, inherited)
        if mesh is not None:
            add_object(path + "/geometry", "Geometry", "mesh_instance", parent, Matrix.Identity(4), world,
                       mesh=mesh, hidden=hidden or not layer[1], layer=layer[0], source_geometry_key=geometry_key)
        for group in descriptor["groups"]:
            source_group = group["source"]
            local = Matrix(group["transform"])
            current_layer = layer_info(source_group, layer)
            source_hidden = bool(source_group.hidden)
            effective_hidden = hidden or source_hidden or not current_layer[1]
            child_path = path + "/g" + str(group["index"])
            node = add_object(child_path, group["name"], "group", parent, local, world @ local,
                              hidden=effective_hidden, layer=current_layer[0], source_hidden=source_hidden,
                              source_ordinal=group["index"])
            expand(source_group.entities, group["key"], child_path, node, world @ local,
                   material_name(source_group, inherited), current_layer, effective_hidden, stack + (group["key"],))
        for instance in descriptor["instances"]:
            source_instance = instance["source"]
            definition = instance["definition"]
            if instance["key"] in stack:
                raise ValueError("Recursive component definition: " + definition.name)
            local = Matrix(instance["transform"])
            current_layer = layer_info(source_instance, layer)
            source_hidden = bool(source_instance.hidden)
            effective_hidden = hidden or source_hidden or not current_layer[1]
            child_path = path + "/i" + str(instance["entity_id"])
            node = add_object(child_path, instance["name"], "component_instance", parent, local, world @ local,
                              hidden=effective_hidden, layer=current_layer[0], source_hidden=source_hidden,
                              display_name=instance["name"] or definition.name, source_instance_name=instance["name"],
                              source_entity_id=instance["entity_id"], definition_name=definition.name,
                              definition_id=stable_id("DEFINITION/" + definition.name))
            expand(definition.entities, instance["key"], child_path, node, world @ local,
                   material_name(source_instance, inherited), current_layer, effective_hidden, stack + (instance["key"],))
        if len(object_rows) and len(object_rows) % 5000 == 0:
            print("SOURCE_PARTS_PROGRESS", len(object_rows), len(mesh_records), flush=True)

    source_root = add_object("ROOT", source.stem, "source_root", None, Matrix.Identity(4), Matrix.Identity(4))
    expand(model.entities, "ROOT", "ROOT", source_root, Matrix.Identity(4))
    # Saved local matrices are primary. Hidden objects need not have an evaluated matrix_world.
    max_transform_error = 0.0
    for row in object_rows:
        obj = objects[row["source_id"]]
        actual = obj.matrix_parent_inverse @ obj.matrix_basis
        max_transform_error = max(max_transform_error, max(abs(actual[r][c] - row["local_matrix"][r][c]) for r in range(4) for c in range(4)))
    if max_transform_error > .001:
        raise ValueError("Hierarchy transform preservation failed: " + str(max_transform_error))
    used_materials = {material.name for mesh in mesh_cache.values() if mesh is not None for material in mesh.materials}
    images = [{"name": image.name, "size": list(image.size), "packed": image.packed_file is not None}
              for image in bpy.data.images if image.source == "FILE"]
    if any(not image["packed"] for image in images):
        raise ValueError("Staged source textures must be packed into the editable blend")
    objects_path = output / "objects.jsonl"
    with objects_path.open("w", encoding="utf-8") as stream:
        for row in object_rows:
            stream.write(json.dumps(row, ensure_ascii=False, separators=(",", ":")) + "\n")
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    scene["physical_accuracy_verified"] = False
    scene["source_sha256"] = source_hash
    blend_path = output / "station-source-parts.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True)
    manifest = {"schema_version": 1, "status": "EDITABLE_SOURCE_HIERARCHY_STAGED", "physical_accuracy_verified": False,
                "all_semantic_physical_parts_verified": False, "production_scene_modified": False,
                "source": {"path": str(source), "sha256": source_hash, "bytes": source.stat().st_size},
                "coordinate_system": "Original SKP local/world coordinates, Z up; SDK inches converted to metres by archived binding, no site registration",
                "id_contract": "sha256(source_file_sha256 + ':' + source_path_or_definition_key)[:24]; stable for this archived file, no cross-revision asset identity claim",
                "visibility": "Hidden groups/components and source layer visibility retained, including their hidden descendants",
                "surface_contract": "Source front-face tessellation, materials, smoothing flags and per-loop front UV retained; no geometry batching, no decimation. Back-face-specific materials and standalone curves/annotations are outside this front-surface extraction",
                "counts": {**dict(counts), "total_objects": len(object_rows), "source_top_level_component_definitions": model.NumComponentDefinitions(),
                           "referenced_component_definitions": len(component_names), "source_geometry_templates": len(descriptors),
                           "shared_mesh_definitions_including_material_variants": len(mesh_records),
                           "unique_mesh_vertices": sum(row["vertices"] for row in mesh_records),
                           "unique_mesh_triangles": sum(row["triangles"] for row in mesh_records),
                           "expanded_mesh_instance_triangles": sum(len(obj.data.polygons) for obj in objects.values() if obj.type == "MESH"),
                           "used_materials": len(used_materials), "packed_images": len(images)},
                "max_saved_local_matrix_entry_error": max_transform_error,
                "world_transform_authority": "Manifest world matrices are composed from source parent/world and local matrices; hidden Blender dependency-graph world caches are not authority",
                "mesh_definitions": mesh_records, "materials_used": sorted(used_materials), "images": images,
                "objects": {"path": objects_path.name, "sha256": sha256(objects_path)},
                "blend": {"path": blend_path.name, "sha256": sha256(blend_path), "bytes": blend_path.stat().st_size},
                "toolchain": {"blender_version": bpy.app.version_string, "python_version": sys.version,
                              "binding_path": str(importer / "sketchup_importer"), "script_sha256": sha256(Path(__file__))},
                "elapsed_seconds": time.monotonic() - start}
    write_json(output / "manifest.json", manifest)
    print("SOURCE_PARTS_COMPLETE", json.dumps(manifest["counts"], ensure_ascii=False), flush=True)


def validate(manifest_path: Path) -> None:
    import bpy
    from mathutils import Matrix

    manifest_path = manifest_path.resolve()
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    output = manifest_path.parent
    blend = output / manifest["blend"]["path"]
    object_file = output / manifest["objects"]["path"]
    if sha256(blend) != manifest["blend"]["sha256"] or sha256(object_file) != manifest["objects"]["sha256"]:
        raise ValueError("Staged extraction digest changed")
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    objects = {obj["source_id"]: obj for obj in bpy.data.objects if "source_id" in obj}
    if len(objects) != manifest["counts"]["total_objects"]:
        raise ValueError("The .blend does not contain every source hierarchy object")
    max_error, checked = 0.0, 0
    computed_world = {}
    with object_file.open(encoding="utf-8") as stream:
        for line in stream:
            row = json.loads(line)
            obj = objects[row["source_id"]]
            if (obj.parent.get("source_id") if obj.parent else None) != row["parent_id"]:
                raise ValueError("Source parent mismatch: " + row["source_path"])
            local = obj.matrix_parent_inverse @ obj.matrix_basis
            world = computed_world[row["parent_id"]] @ local if row["parent_id"] else local
            computed_world[row["source_id"]] = world
            for actual, expected in ((local, row["local_matrix"]), (world, row["world_matrix"])):
                max_error = max(max_error, max(abs(actual[r][c] - expected[r][c]) for r in range(4) for c in range(4)))
            if obj.get("source_sha256") != manifest["source"]["sha256"] or obj.hide_viewport != row["effective_hidden"]:
                raise ValueError("Source provenance or visibility mismatch")
            if obj.get("source_name") != row["source_name"]:
                raise ValueError("Original source name changed")
            checked += 1
    mesh_objects = {obj.data.get("source_mesh_id"): obj.data for obj in objects.values() if obj.type == "MESH"}
    for expected in manifest["mesh_definitions"]:
        mesh = mesh_objects[expected["source_mesh_id"]]
        actual = mesh_receipt(mesh, expected["source_geometry_id"], expected["inherited_material"], expected["source_faces"])
        for key in ("vertices", "triangles", "uv_loops", "material_slots", "vertices_sha256", "uv_sha256", "indices_sha256", "material_indices_sha256"):
            if actual[key] != expected[key]:
                raise ValueError("Persisted geometry/UV mismatch: " + expected["name"] + " " + key)
    if max_error > .001:
        raise ValueError("Persisted hierarchy transform error: " + str(max_error))
    if any(image.source == "FILE" and image.packed_file is None for image in bpy.data.images):
        raise ValueError("Persisted textures are not self-contained")
    receipt = {"status": "PASS", "physical_accuracy_verified": False, "source_objects_checked": checked,
               "shared_mesh_definitions_checked": len(mesh_objects), "max_matrix_entry_error": max_error,
               "blend_sha256": sha256(blend), "checks": ["source-hash-qualified IDs", "parent hierarchy", "local and world transforms",
               "mesh vertices and indices", "material assignments", "front UV loops", "hidden groups/layers", "packed source textures"]}
    write_json(output / "validation-receipt.json", receipt)
    print("SOURCE_PARTS_VALIDATION", json.dumps(receipt), flush=True)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path)
    parser.add_argument("--importer", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--validate", type=Path)
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    args = parser.parse_args(argv)
    if args.validate:
        validate(args.validate)
    else:
        if not all((args.source, args.importer, args.output)):
            parser.error("--source, --importer and --output are required for extraction")
        extract(args)


if __name__ == "__main__":
    main()
