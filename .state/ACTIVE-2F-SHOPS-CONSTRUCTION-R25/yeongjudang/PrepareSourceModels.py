import bpy,json
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent;A=P/'sources/construction';records={}
for key,fp in [('wicker',A/'wicker/wicker_basket_02.fbx'),('shelf',A/'modern_shelf/modern_shelf.fbx'),('case',A/'case/Cake-Display.fbx'),('counter',A/'counter/counter-source-unity.fbx')]:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(fp));out=[]
 for ob in bpy.data.objects:
  if ob.type!='MESH':continue
  vs=[ob.matrix_world@Vector(v) for v in ob.bound_box];out.append({'name':ob.name,'faces':len(ob.data.polygons),'materials':[m.name for m in ob.data.materials],'bounds':[[min(v[i] for v in vs) for i in range(3)],[max(v[i] for v in vs) for i in range(3)]]})
 records[key]=out
 if key=='wicker':
  # Actual PolyHaven source has separable body/lid. Retain body only for the observed open-tray shape.
  for ob in list(bpy.data.objects):
   if 'lid' in ob.name.lower():bpy.data.objects.remove(ob,do_unlink=True)
  bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(A/'wicker/WickerBodyOwn.fbx'),use_selection=True,object_types={'MESH'},bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
(P/'SOURCE-MODEL-GEOMETRY-INVENTORY.json').write_text(json.dumps(records,indent=2))
