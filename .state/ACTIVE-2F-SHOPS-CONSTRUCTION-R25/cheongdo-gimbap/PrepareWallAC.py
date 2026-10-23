import bpy,json
from pathlib import Path
p=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/cheongdo-gimbap/sources/models/wall-ac')
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(p/'WallAC.glb'))
slots=[{'renderer':o.name,'materials':[m.name if m else None for m in o.data.materials]} for o in bpy.context.scene.objects if o.type=='MESH']
for o in list(bpy.context.scene.objects):
 if o.type not in ['MESH','EMPTY']:bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.export_scene.fbx(filepath=str(p/'WallAC.fbx'),object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False)
(p/'DERIVATION.json').write_text(json.dumps({'source_preserved':True,'slots':slots,'appearance':'Own solid white URP source photo appearance; brand pixels not imported'},indent=2));print(slots)
