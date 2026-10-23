import bpy,json
from pathlib import Path
from mathutils import Vector
p=Path("/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/boksoon/sources/models")
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(p/"fridge/CommercialRefrigerator.glb"))
rows=[]
for o in bpy.data.objects:
 if o.type=="MESH":
  vs=[o.matrix_world@v.co for v in o.data.vertices]
  rows.append({"name":o.name,"vertices":len(vs),"bounds":[[min(v[i]for v in vs)for i in range(3)],[max(v[i]for v in vs)for i in range(3)]],"slots":[m.name for m in o.data.materials]})
(p/"SOURCE-MESH-STRUCTURE.json").write_text(json.dumps(rows,indent=2))
print(json.dumps(rows))
