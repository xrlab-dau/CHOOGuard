import bpy,json,re,sys
from pathlib import Path
from mathutils import Vector
BASE=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/american-trailer/sources/free-models')
for slug in (sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['blender','grinder','squeezer-c','counter']):
 p=BASE/slug;bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(p/(slug+'.glb')))
 removed=[]
 if slug=='lemon-scan':
  keep=sorted([o for o in bpy.data.objects if o.type=='MESH'],key=lambda o:o.name)[0]
  for o in list(bpy.data.objects):
   if o.type=='MESH' and o!=keep:removed.append(o.name);bpy.data.objects.remove(o,do_unlink=True)
  (p/'DERIVATIVE-SCOPE.json').write_text(json.dumps({'source_unchanged':True,'one_original_lemon_retained':keep.name,'separate_extra_lemons_removed':removed},indent=2))
 if slug=='counter':
  # Source is a combined industrial kitchen. Extract actual lower straight stainless counter,
  # without floor/rack/return: local derivative only, source GLB preserved.
  created=[]
  for o in list(bpy.data.objects):
   if o.type!='MESH':continue
   vs=[o.matrix_world@v.co for v in o.data.vertices];faces=[];verts=[];maps={};mids=[]
   for f in o.data.polygons:
    pts=[vs[i] for i in f.vertices]
    if o.name not in ['Object_6'] or not all(2636.8<=v.x<=2898.55 and 164.7<=v.y<=250 and -.1<=v.z<=101 for v in pts):continue
    ids=[]
    for i in f.vertices:
     if i not in maps:maps[i]=len(verts);verts.append(tuple(vs[i]))
     ids.append(maps[i])
    faces.append(ids);mids.append(f.material_index)
   if faces:
    m=bpy.data.meshes.new('StainlessStraightSourceBody');m.from_pydata(verts,[],faces);m.update();n=bpy.data.objects.new(o.name+'_LowerStraight_Source',m);bpy.context.collection.objects.link(n)
    for mat in o.data.materials:m.materials.append(mat)
    for f,idx in zip(m.polygons,mids):f.material_index=idx
    created.append(n)
  keep=set(created)
  for o in list(bpy.data.objects):
   if o not in keep:removed.append(o.name);bpy.data.objects.remove(o,do_unlink=True)
  (p/'DERIVATIVE-SCOPE.json').write_text(json.dumps({'source_unchanged':True,'selected_original_mesh_portion':'sourcelowerstraightcounter Object4/6/7;actualfaceswithinworldbbox2636.8:2898.55,164.7:250,-.1:101','floor_upperrack_unrelatedreturn_removed':removed,'remaining_meshes':len(created)},indent=2))
 # Split by material so renderer-name mapping reliably preserves own PBR per material.
 for o in list(bpy.data.objects):
  if o.type!='MESH':continue
  bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
  if len(o.data.materials)>1:
   bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='MATERIAL');bpy.ops.object.mode_set(mode='OBJECT')
 texture_dir=p/'unity-source-textures';texture_dir.mkdir(exist_ok=True);images={}
 for i,im in enumerate(bpy.data.images):
  if im.type=='IMAGE' and im.size[0]:
   name=f'{slug}_image_{i}.png';dest=texture_dir/name;im.filepath_raw=str(dest);im.file_format='PNG';im.save();images[im.name]=str(dest)
 materials=[];matkeys={}
 for i,m in enumerate(bpy.data.materials):
  if m.users==0:continue
  key=f'{slug}_pbr_{i}';matkeys[m.name]=key;out={'name':m.name,'key':key,'textures':{}}
  bsdf=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) if m.use_nodes else None
  out['color']=list(bsdf.inputs['Base Color'].default_value) if bsdf else list(m.diffuse_color)
  out['metallic']=float(bsdf.inputs['Metallic'].default_value) if bsdf else float(m.metallic)
  out['roughness']=float(bsdf.inputs['Roughness'].default_value) if bsdf else float(m.roughness)
  if bsdf and 'Transmission Weight' in bsdf.inputs:out['transmission']=float(bsdf.inputs['Transmission Weight'].default_value)
  for n in m.node_tree.nodes if m.use_nodes else []:
   if n.type=='TEX_IMAGE' and n.image:out['textures'][n.label or n.name]=images[n.image.name]
  materials.append(out)
 objects=[]
 for i,o in enumerate([o for o in bpy.data.objects if o.type=='MESH']):
  o.name=f'{slug}_source_part_{i}';pts=[o.matrix_world@Vector(v) for v in o.bound_box]
  objects.append({'name':o.name,'materials':[matkeys.get(m.name) for m in o.data.materials],'min':[min(v[k] for v in pts) for k in range(3)],'max':[max(v[k] for v in pts) for k in range(3)],'vertices':len(o.data.vertices)})
 bpy.ops.object.select_all(action='SELECT');dest=p/(slug+'-source-unity.fbx');bpy.ops.export_scene.fbx(filepath=str(dest),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='COPY',embed_textures=False)
 (p/'UNITY-PBR-MATERIALS.json').write_text(json.dumps({'slug':slug,'materials':materials,'objects':objects,'original_glb_preserved':True,'export':str(dest)},indent=2));print('READY',slug,len(objects),len(materials))
