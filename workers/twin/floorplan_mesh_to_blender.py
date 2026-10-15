"""Blender background entry: same reviewed mesh package used by Unity."""
import hashlib
import json
from pathlib import Path
import sys

import bpy


args = sys.argv[sys.argv.index("--") + 1:]
if len(args) != 2:
    raise ValueError("mesh-package.json output.blend required")
source, output = map(Path, args)
data = json.loads(source.read_text())
if data["coordinate_space"] != "Unity world XYZ, Y up":
    raise ValueError("Unexpected coordinate space")
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene["source_raster_sha256"] = data["source_sha256"]
scene["mesh_package_sha256"] = hashlib.sha256(source.read_bytes()).hexdigest()
scene["metric_dimensions_measured"] = False
scene["accuracy_note"] = "Official-plan relative reconstruction; not independently surveyed"
colors = {"plaster": (.78, .78, .75, 1), "granite": (.47, .48, .46, 1),
          "steel": (.39, .41, .42, 1), "glass": (.36, .53, .56, .3)}
materials, collections = {}, {}
for item in data["objects"]:
    kind = item["kind"]
    if kind not in collections:
        collection = bpy.data.collections.new(kind)
        scene.collection.children.link(collection)
        collections[kind] = collection
    label = item["material"]
    if label not in materials:
        mat = bpy.data.materials.new(label)
        mat.diffuse_color = colors.get(label, colors["plaster"])
        material_source = data.get("material_sources", {}).get(label)
        if material_source:
            texture_path = Path(material_source["texture"])
            if hashlib.sha256(texture_path.read_bytes()).hexdigest() != material_source["texture_sha256"]:
                raise ValueError("Acquired source texture changed")
            mat.use_nodes = True
            tree = mat.node_tree
            image_node = tree.nodes.new("ShaderNodeTexImage")
            image_node.image = bpy.data.images.load(str(texture_path.resolve()), check_existing=True)
            image_node.image.pack()
            uv_node = tree.nodes.new("ShaderNodeTexCoord")
            mapping = tree.nodes.new("ShaderNodeVectorMath")
            mapping.operation = "MULTIPLY_ADD"
            mapping.inputs[1].default_value = (*material_source["uv_scale"], 1)
            mapping.inputs[2].default_value = (*material_source["uv_offset"], 0)
            tree.links.new(uv_node.outputs["UV"], mapping.inputs[0])
            tree.links.new(mapping.outputs[0], image_node.inputs["Vector"])
            tree.links.new(image_node.outputs["Color"], tree.nodes.get("Principled BSDF").inputs["Base Color"])
        materials[label] = mat
    mesh = bpy.data.meshes.new(item["id"] + "_mesh")
    # Unity and Blender differ in up axis; this reflection requires winding reversal.
    vertices = [(p[0], p[2], p[1]) for p in item["vertices"]]
    t = item["triangles"]
    triangles = [(t[i], t[i + 2], t[i + 1]) for i in range(0, len(t), 3)]
    mesh.from_pydata(vertices, [], triangles)
    mesh.update()
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        uv.data[loop.index].uv = item["uv"][loop.vertex_index]
    obj = bpy.data.objects.new(item["id"], mesh)
    collections[kind].objects.link(obj)
    mesh.materials.append(materials[label])
    obj["source_feature"] = json.dumps(item.get("source_feature", item.get("source_feature_ids")), ensure_ascii=False)
    if "source_polygon_px" in item:
        obj["source_polygon_px"] = json.dumps(item["source_polygon_px"], separators=(",", ":"))
    if "center_px" in item:
        obj["source_center_px"] = item["center_px"]
        obj["height_inferred"] = item.get("height_inferred", False)
    obj["architectural_kind"] = kind
    obj["collider"] = item["collider"]
output.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(output.resolve()))
print("REVIEWED_ARCHITECTURE_BLEND_SAVED", str(output), "objects", len(data["objects"]))
