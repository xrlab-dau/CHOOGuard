import bpy,json
from pathlib import Path
b=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/pharmacy').resolve()
for n in ['store-shelves','three-pill-boxes']:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(b/'sources'/n/(n+'.glb')))
 o=[]
 for ob in bpy.data.objects:
  if ob.type=='MESH':o.append({'name':ob.name,'parent':ob.parent.name if ob.parent else None,'vertices':len(ob.data.vertices),'bbox':[list(ob.matrix_world@__import__('mathutils').Vector(v)) for v in ob.bound_box],'materials':[m.name for m in ob.data.materials]})
 (b/'sources'/n/'geometry-inventory.json').write_text(json.dumps(o,indent=2));print(n,[(x['name'],x['vertices']) for x in o])
