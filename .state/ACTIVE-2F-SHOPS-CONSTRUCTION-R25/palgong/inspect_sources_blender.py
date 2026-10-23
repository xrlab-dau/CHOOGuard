import bpy,json
from pathlib import Path
b=Path(__file__).parent/'sources'
out={}
for p in [b/'modern-armchair/ModernArmchair.fbx',b/'kiosk/kiosk.glb',b/'dining-chair/dining-chair.glb',b/'coffee-commercial/CommercialCoffeeMachine.fbx',b/'cafe-table/CafeTableSteelBase.fbx']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 if p.suffix=='.fbx':bpy.ops.import_scene.fbx(filepath=str(p))
 else:bpy.ops.import_scene.gltf(filepath=str(p))
 d={'materials':[m.name for m in bpy.data.materials], 'objects':[{'name':o.name,'materials':[s.material.name if s.material else '' for s in o.material_slots],'dimensions':list(o.dimensions),'location':list(o.location)} for o in bpy.data.objects if o.type=='MESH']}
 out[str(p)]=d
print(json.dumps(out,indent=2));(b/'SOURCE-MESH-MATERIAL-SLOTS.json').write_text(json.dumps(out,indent=2))
