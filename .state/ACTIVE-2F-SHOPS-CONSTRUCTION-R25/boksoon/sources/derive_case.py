import bpy,json
from pathlib import Path
p=Path("/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/boksoon/sources/models/case")
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(p/"DisplayCase.glb"))
slots={o.name:[m.name for m in o.data.materials] for o in bpy.data.objects if o.type=="MESH"}
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=str(p/"DisplayCase.fbx"),use_selection=True,object_types={"MESH"},axis_forward="-Z",axis_up="Y",apply_unit_scale=True,bake_anim=False)
(p/"SOURCE-SLOTS.json").write_text(json.dumps(slots,indent=2));print(slots)
