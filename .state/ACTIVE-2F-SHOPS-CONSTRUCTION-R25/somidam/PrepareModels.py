import bpy,json,sys
from pathlib import Path
p=Path(__file__).parent/'sources'
for part,name in [('case','Cake-Display'),('coffee','Coffee-Machine')]:
 bpy.ops.wm.read_factory_settings(use_empty=True);src=p/part/(name+'.glb');bpy.ops.import_scene.gltf(filepath=str(src));bpy.context.scene.frame_set(0)
 removed=[]
 if part=='case':
  for ob in list(bpy.data.objects):
   if ob.type=='MESH' and any(x in ob.name.lower() for x in ['doily','price','cake_stand']):removed.append(ob.name);bpy.data.objects.remove(ob,do_unlink=True)
 textures=p/part/'textures';textures.mkdir(exist_ok=True)
 images={}
 for im in bpy.data.images:
  if im.type=='IMAGE' and im.size[0]:
   ip=textures/(im.name+'.png');im.filepath_raw=str(ip);im.file_format='PNG';im.save();images[im.name]=str(ip)
 mats=[]
 for mat in bpy.data.materials:
  slots={}
  for nd in mat.node_tree.nodes if mat.use_nodes else []:
   if nd.type=='TEX_IMAGE' and nd.image:slots[nd.label or nd.name]=images.get(nd.image.name)
  mats.append({'name':mat.name,'textures':slots})
 bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(p/part/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='COPY',embed_textures=False)
 (p/part/'MODEL-MATERIALS.json').write_text(json.dumps({'materials':mats,'removed_nonbranch_generic_price_stands':removed,'objects':[{'name':ob.name,'materials':[m.name for m in ob.data.materials]} for ob in bpy.data.objects if ob.type=='MESH']},indent=2))
