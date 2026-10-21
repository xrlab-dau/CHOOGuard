import bpy,json
from pathlib import Path
from mathutils import Vector
b=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(b/'cup.glb'))
rec=[]
for o in bpy.data.objects:
 if o.type!='MESH':continue
 pts=[o.matrix_world@Vector(p) for p in o.bound_box];mn=[min(p[i] for p in pts) for i in range(3)];mx=[max(p[i] for p in pts) for i in range(3)];rec.append({'name':o.name,'min':mn,'max':mx,'height':mx[2]-mn[2]})
# Tallest source mesh is the hollow tapered cup; separate loose lid left as unused source evidence.
keep=max(rec,key=lambda r:r['height'])['name']
for o in list(bpy.data.objects):
 if o.type=='MESH' and o.name!=keep:bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(b/'CupBody-SourceDerivative.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False)
(b/'DERIVATION.json').write_text(json.dumps({'original_glb_preserved':True,'original_fbx_preserved':True,'selected_source_mesh':keep,'source_mesh_bounds':rec,'change':'Isolate tall tapered hollow cup; loose source lid excluded. Photo-fit orange sleeve and clear dome are independent physical geometry.'},indent=2));print(json.dumps(rec))
