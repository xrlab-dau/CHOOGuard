import bpy,json
from pathlib import Path
p=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/soban/downloaded-models').resolve()
out=[]
for name in ['Restaurant-Kitchen-Set-Part1','Sink-CGIME06','GasStove-Lyskilde','RiceCooker-Candidate2']:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(p/(name+'.glb')))
 rows=[]
 for o in bpy.data.objects:
  if o.type=='MESH':rows.append({'name':o.name,'tris':sum(len(f.vertices)-2 for f in o.data.polygons),'vertices':len(o.data.vertices),'bounds':list(o.dimensions),'center':list(o.location),'materials':[m.name for m in o.data.materials]})
 out.append({'name':name,'meshes':rows})
(p/'SOURCE-MESH-COMPONENTS.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
