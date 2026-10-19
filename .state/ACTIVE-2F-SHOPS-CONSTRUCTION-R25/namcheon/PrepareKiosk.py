import bpy,pathlib,json,sys
from mathutils import Vector
p=pathlib.Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/namcheon/sources/models')
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
s=p/'084a535f5fb542c99eb3564a28195e3e';bpy.ops.import_scene.gltf(filepath=str(s/'PayStation-Kiosk.glb'))
objs=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in objs:
 bpy.context.view_layer.objects.active=o;o.select_set(True)
for o in [o for o in bpy.context.scene.objects if o.type!='MESH']:bpy.data.objects.remove(o,do_unlink=True)
# No scene lights or cameras exported. Preserve source mesh and slot identities.
bpy.ops.export_scene.fbx(filepath=str(s/'PayStation-Kiosk.fbx'),use_selection=False,object_types={'MESH'},apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False)
(s/'UNITY-MATERIAL-SLOTS.json').write_text(json.dumps([{'object':o.name,'materials':[m.name if m else None for m in o.data.materials]} for o in objs],indent=2))
print('DONE',len(objs))
