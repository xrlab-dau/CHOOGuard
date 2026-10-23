import bpy, json
from pathlib import Path
from mathutils import Vector
p=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/american-trailer/sources/free-models/counter')
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(p/'counter.glb'))
a=[]
for o in bpy.data.objects:
 if o.type!='MESH':continue
 pts=[o.matrix_world@Vector(v) for v in o.bound_box];a.append({'name':o.name,'min':[min(v[k] for v in pts) for k in range(3)],'max':[max(v[k] for v in pts) for k in range(3)],'vertices':len(o.data.vertices),'materials':[m.name for m in o.data.materials]})
(p/'OBJECT-BOUNDS.json').write_text(json.dumps(a,indent=2));print(json.dumps(a))
