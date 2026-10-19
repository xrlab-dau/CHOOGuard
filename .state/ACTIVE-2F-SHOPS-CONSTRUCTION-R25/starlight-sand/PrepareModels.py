import bpy,sys,json,pathlib,shutil
from mathutils import Vector
p=pathlib.Path(__file__).resolve().parent;s=p/'sources';out=s/'models';out.mkdir(exist_ok=True)
models={'financier':s/'financier.glb','cookie':s/'cookieReal.glb','giftbox':s/'giftbox.glb','displaycase':s/'displaycase.glb','wrapped-biscuit':s/'biscuit.glb'}
result={}
for key,src in models.items():
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(src));bpy.context.scene.frame_set(0)
 dst=out/key;dst.mkdir(exist_ok=True);tex=dst/'textures';tex.mkdir(exist_ok=True)
 original=[]
 for o in bpy.data.objects:
  if o.type=='MESH':
   pts=[o.matrix_world@Vector(v) for v in o.bound_box];original.append({'name':o.name,'verts':len(o.data.vertices),'faces':len(o.data.polygons),'materials':[m.name if m else '' for m in o.data.materials],'bbox':[[min(v[i] for v in pts) for i in range(3)],[max(v[i] for v in pts) for i in range(3)]]})
 (dst/'SOURCE-GEOMETRY-INVENTORY.json').write_text(json.dumps(original,indent=2))
 if key in ['financier','cookie']:
  for o in list(bpy.data.objects):
   if o.type=='MESH' and len(o.data.polygons)>6000:
    bpy.context.view_layer.objects.active=o
    m=o.modifiers.new('OwnSmallProductDerivative','DECIMATE');m.ratio=6000/len(o.data.polygons);bpy.ops.object.modifier_apply(modifier=m.name)
 materialdata=[]
 for m in bpy.data.materials:
  slots={};base=[1,1,1,1];metal=0;rough=.5
  if m.use_nodes:
   for n in m.node_tree.nodes:
    if n.type=='BSDF_PRINCIPLED':
     base=list(n.inputs['Base Color'].default_value);metal=n.inputs['Metallic'].default_value;rough=n.inputs['Roughness'].default_value
    if n.type=='TEX_IMAGE' and n.image and n.image.size[0]:
     im=n.image;name=''.join(c if c.isalnum() or c in '_-' else '_' for c in im.name)+'.png';dest=tex/name;im.filepath_raw=str(dest);im.file_format='PNG';im.save()
     slots[n.label or n.name]=str(dest.relative_to(s))
  materialdata.append({'name':m.name,'base':base,'metallic':metal,'roughness':rough,'textures':slots})
 bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(dst/(key+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='COPY',embed_textures=False)
 result[key]={'source':str(src),'asset':str(dst/(key+'.fbx')),'materials':materialdata,'objects':[{'name':o.name,'faces':len(o.data.polygons),'materials':[m.name if m else '' for m in o.data.materials]} for o in bpy.data.objects if o.type=='MESH']}
 (dst/'UNITY-PBR-SLOTS.json').write_text(json.dumps(result[key],indent=2))
(s/'MODEL-CONVERSION-RESULT.json').write_text(json.dumps(result,indent=2));print('OWN_STAGED_SOURCE_DERIVATIVES_READY')
