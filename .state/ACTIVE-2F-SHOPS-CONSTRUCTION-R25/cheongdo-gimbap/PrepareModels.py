import bpy,json
from pathlib import Path
from mathutils import Vector
p=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/cheongdo-gimbap/sources/models')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(p/'pos-monitor/Monitor.glb'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];record=[]
for o in meshes:
 b=[o.matrix_world@Vector(v) for v in o.bound_box];record.append({'name':o.name,'materials':[m.name if m else None for m in o.data.materials],'bounds':[[min(v[i] for v in b) for i in range(3)],[max(v[i] for v in b) for i in range(3)]]})
 # Exact imported source mesh retained, no authored labels or camera/light imports.
for o in list(bpy.context.scene.objects):
 if o.type not in ['MESH','EMPTY']:bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.export_scene.fbx(filepath=str(p/'pos-monitor/Monitor.fbx'),use_selection=False,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='COPY',embed_textures=True)
(p/'pos-monitor/DERIVATION.json').write_text(json.dumps({'source_preserved':True,'meshes':record,'export':'Monitor.fbx'},indent=2))
print(json.dumps(record))
