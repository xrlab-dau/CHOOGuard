import bpy,json
from pathlib import Path
b=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/olive-young/sources/free-models');out={}
for key in ['tube','makeup','pump','lipstick','foil-pouch','gondola']:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(b/key/(key+'.glb')))
 out[key]=[]
 for o in bpy.data.objects:
  if o.type=='MESH':
   out[key].append({'name':o.name,'vertices':len(o.data.vertices),'faces':len(o.data.polygons),'bounds':[list(o.dimensions),list(o.location)],'materials':[m.name if m else '' for m in o.data.materials]})
print('MODEL_INVENTORY',json.dumps(out))
(b/'BLENDER-SOURCE-INVENTORY.json').write_text(json.dumps(out,indent=2))
