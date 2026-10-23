import bpy,json
from pathlib import Path
r=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/storyway-north');j=json.loads((r/'PREPARED-SOURCE-IMPORTS.json').read_text());out={}
for key in j['models']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 f=r/'sources/construction'/j['models'][key].split('storyway-north/')[1]
 bpy.ops.import_scene.fbx(filepath=str(f))
 obs=[o for o in bpy.data.objects if o.type=='MESH'];out[key]={'materials':{o.name:[s.material.name if s.material else None for s in o.material_slots] for o in obs},'dimensions':{o.name:list(o.dimensions) for o in obs}}
(r/'SOURCE-MODEL-SLOT-INVENTORY.json').write_text(json.dumps(out,indent=2))
