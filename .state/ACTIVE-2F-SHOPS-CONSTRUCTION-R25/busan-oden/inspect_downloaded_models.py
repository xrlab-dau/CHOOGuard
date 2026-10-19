import bpy,json,sys
from pathlib import Path
b=Path(sys.argv[sys.argv.index('--')+1])
for key in ['cupboards','bun-counter','wok']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.gltf(filepath=str(b/(key+'.glb')))
 out=[]
 for o in bpy.data.objects:
  if o.type=='MESH':out.append({'name':o.name,'dimensions':list(o.dimensions),'location':list(o.location),'materials':[m.name for m in o.data.materials]})
 (b/(key+'-OBJECTS.json')).write_text(json.dumps(out,indent=2))
 print(key,len(out))
