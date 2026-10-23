import bpy,sys,json
from pathlib import Path
from mathutils import Vector
b=Path(sys.argv[sys.argv.index('--')+1])
recipes=[('cupboards','Stainless-Cabinet',['Object_4']),('cupboards','Stainless-Sink',['Object_6']),('wok','Open-Stainless-Wok',['Wok_Metal_0','Wok_handle_Metal_0'])]
out=[]
for key,name,keep in recipes:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.gltf(filepath=str(b/(key+'.glb')))
 for o in list(bpy.data.objects):
  if o.type=='MESH' and o.name not in keep:bpy.data.objects.remove(o,do_unlink=True)
 mesh=[o for o in bpy.data.objects if o.type=='MESH']
 points=[o.matrix_world@Vector(c) for o in mesh for c in o.bound_box]
 mins=Vector([min(p[i] for p in points) for i in range(3)]);maxs=Vector([max(p[i] for p in points) for i in range(3)])
 bpy.ops.object.select_all(action='DESELECT')
 for o in mesh:o.select_set(True)
 tex=b/(name+'-textures');tex.mkdir(exist_ok=True)
 for im in bpy.data.images:
  if im.type=='IMAGE' and im.size[0]:
   p=tex/(im.name+'.png');im.filepath_raw=str(p);im.file_format='PNG';im.save()
 bpy.ops.export_scene.fbx(filepath=str(b/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='COPY',embed_textures=False)
 out.append({'source':key+'.glb','derivative':name+'.fbx','retained_source_objects':keep,'world_bounds_blender':{'min':list(mins),'max':list(maxs)},'materials':[m.name for o in mesh for m in o.data.materials]})
(b/'SOURCE-DERIVATIVE-RECIPES.json').write_text(json.dumps(out,indent=2))
