import bpy,sys,json
from pathlib import Path
p=Path(__file__).parent/'sources/models/open-chiller';p.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(p/'Original.glb'))
removed=[]
for o in list(bpy.data.objects):
 if any(n in o.name for n in ['Champagne','FridgeDoor','FridgeGlass']):removed.append(o.name);bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(p/'Open-Chiller-PhotoAdapted.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',bake_anim=False)
(p/'MODEL-DERIVATION.json').write_text(json.dumps({'source':'CommercialRefrigerator by SeanThomas/Khronos CC-BY4.0','source_glb_preserved':True,'removed_from_independent_derivative':removed,'adaptation':'Door and unrelated champagne removed to approximate source-visible open commercial chiller; own steel override and source-shaped header added at construction. No exact manufacturer identity or surveyed equality claimed.'},indent=2))
