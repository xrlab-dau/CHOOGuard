"""Blender-only worker; invoke through prepare_blender.py in a fresh background process."""
import json
from pathlib import Path
import sys


def run(directory: Path) -> dict:
    import bpy
    import numpy as np
    from bpy_extras.io_utils import axis_conversion

    if not bpy.app.background:
        raise RuntimeError("Use a fresh background Blender process, never an interactive scene")
    request = json.loads((directory / "request.json").read_text(encoding="utf-8"))
    # The factory-startup process is disposable; no user's open scene is accessed.
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    bpy.ops.wm.ply_import(filepath=str(directory / "source_local_m.ply"),
                          forward_axis="Y", up_axis="Z", merge_verts=False, import_colors="SRGB")
    objects = list(bpy.context.selected_objects)
    if len(objects) != 1 or objects[0].type != "MESH":
        raise ValueError("Expected one imported surface mesh")
    obj = objects[0]
    obj.name = "ReconstructionReview"
    # PLY import is identity; apply a recorded, right-handed axis rotation explicitly.
    forward = request["source_forward"].replace("NEGATIVE_", "-")
    up = request["source_up"].replace("NEGATIVE_", "-")
    transform = axis_conversion(from_forward=forward, from_up=up, to_forward="Y", to_up="Z").to_4x4()
    obj.data.transform(transform)
    obj.data.update()
    bpy.context.view_layer.update()

    def stats(target):
        mesh = target.data
        mesh.calc_loop_triangles()
        if not mesh.vertices or not mesh.loop_triangles:
            raise ValueError("No usable surface after processing")
        xyz = np.empty(len(mesh.vertices) * 3, dtype=np.float64)
        mesh.vertices.foreach_get("co", xyz)
        xyz = xyz.reshape((-1, 3))
        matrix = np.array(target.matrix_world, dtype=np.float64)
        world = xyz @ matrix[:3, :3].T + matrix[:3, 3]
        if not np.isfinite(world).all() or any(t.area <= 0 for t in mesh.loop_triangles):
            raise ValueError("Invalid coordinates or degenerate triangles")
        return {"vertices": len(mesh.vertices), "triangles": len(mesh.loop_triangles),
                "bounds_m": [world.min(axis=0).tolist(), world.max(axis=0).tolist()],
                "color_attributes": [a.name for a in mesh.color_attributes]}

    before = stats(obj)
    if request["decimate_ratio"] < 1:
        modifier = obj.modifiers.new("ExplicitDecimation", "DECIMATE")
        modifier.ratio = request["decimate_ratio"]
        modifier.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    after = stats(obj)
    if after["triangles"] > before["triangles"]:
        raise ValueError("Decimation unexpectedly increased triangle count")
    if before["color_attributes"] and not after["color_attributes"]:
        raise ValueError("Decimation lost vertex colors")
    # A Blender preview material does not imply Unity can import this node graph.
    if obj.data.color_attributes:
        material = bpy.data.materials.new("VertexColorReview")
        material.use_nodes = True
        color = material.node_tree.nodes.new("ShaderNodeVertexColor")
        color.layer_name = obj.data.color_attributes[0].name
        material.node_tree.links.new(color.outputs["Color"],
                                     material.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
        obj.data.materials.append(material)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.wm.ply_export(filepath=str(directory / "prepared.ply"), export_selected_objects=True,
                         forward_axis="Y", up_axis="Z", export_normals=True,
                         export_colors="SRGB", export_triangulated_mesh=True)
    # Reuse this repository's art/blender FBX axis convention; unit scale is explicit.
    bpy.ops.export_scene.fbx(filepath=str(directory / "prepared.fbx"), use_selection=True,
                             object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                             global_scale=1, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             bake_anim=False, add_leaf_bones=False, use_triangles=True, colors_type="SRGB")
    bpy.ops.wm.save_as_mainfile(filepath=str(directory / "review.blend"))
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.import_scene.fbx(filepath=str(directory / "prepared.fbx"), use_anim=False,
                             use_image_search=False, colors_type="SRGB")
    imported = [item for item in bpy.context.selected_objects if item.type == "MESH"]
    if len(imported) != 1:
        raise ValueError("FBX reimport did not produce one mesh")
    roundtrip = stats(imported[0])
    bounds_error = float(np.max(np.abs(np.array(after["bounds_m"]) - np.array(roundtrip["bounds_m"]))))
    if bounds_error > .001 or roundtrip["triangles"] != after["triangles"]:
        raise ValueError("FBX reimport changed bounds by >1mm or changed triangle count")
    if after["color_attributes"] and not roundtrip["color_attributes"]:
        raise ValueError("FBX reimport lost color attributes")
    report = {
        "status": "fbx_roundtrip_checked", "version": bpy.app.version_string,
        "build_hash": bpy.app.build_hash.decode("ascii"), "before": before, "after": after,
        "local_source_to_blender": [list(row) for row in transform],
        "fbx_settings": {"axis_forward": "-Z", "axis_up": "Y", "units": "metre",
                         "apply_scale_options": "FBX_SCALE_UNITS"},
        "roundtrip": roundtrip, "roundtrip_max_bounds_error_m": bounds_error,
        "review_required": ["geometry/color fidelity after decimation", "thin structures and openings",
                            "Unity vertex-color shader or explicit texture bake", "separate colliders and target-build tests"],
        "limitations": "No surface reconstruction from cleaned points, hole filling, topology certification or automatic texture bake",
    }
    (directory / "blender_report.json").write_text(json.dumps(report, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    return report


if __name__ == "__main__":
    run(Path(sys.argv[sys.argv.index("--") + 1]).resolve(strict=True))
