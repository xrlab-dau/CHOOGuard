import bpy,json
from pathlib import Path
S=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/flowers/sources');records=[]
for key in ['rose_bouquet','potted_plant_01','potted_plant_04','ceramic_vase_01','wicker_basket_02']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 src=S/key/(key+('.glb' if key=='rose_bouquet' else '.fbx'))
 if src.suffix=='.glb':bpy.ops.import_scene.gltf(filepath=str(src))
 else:bpy.ops.import_scene.fbx(filepath=str(src))
 # Join only pieces with identical sole material. Retain each product independent at placement.
 groups={}
 for o in list(bpy.context.scene.objects):
  if o.type=='MESH' and len(o.data.materials)==1:groups.setdefault(o.data.materials[0].name,[]).append(o)
 for mat,obs in groups.items():
  bpy.ops.object.select_all(action='DESELECT')
  for o in obs:o.select_set(True)
  bpy.context.view_layer.objects.active=obs[0]
  if len(obs)>1:bpy.ops.object.join()
  obs[0].name=key+'_'+mat
 mats=[]
 for m in bpy.data.materials:
  if not any(o.type=='MESH' and m.name in [ma.name for ma in o.data.materials if ma] for o in bpy.context.scene.objects):continue
  rec={'name':m.name,'color':list(m.diffuse_color),'textures':[]}
  if m.use_nodes:
   for n in m.node_tree.nodes:
    if n.type=='TEX_IMAGE' and n.image and n.image.has_data:
     im=n.image;fn=key+'_'+im.name.replace('/','_')+'.png';p=S/key/fn;im.filepath_raw=str(p);im.file_format='PNG';im.save();rec['textures'].append({'file':str(p),'links':[l.to_socket.name for l in n.outputs[0].links]})
  mats.append(rec)
 bpy.ops.export_scene.fbx(filepath=str(S/key/(key+'-Unity.fbx')),object_types={'MESH'},add_leaf_bones=False,bake_anim=False,path_mode='STRIP',axis_forward='-Z',axis_up='Y')
 records.append({'key':key,'materials':mats,'mesh_objects':[o.name for o in bpy.context.scene.objects if o.type=='MESH']})
(S/'UNITY-MODEL-MATERIALS.json').write_text(json.dumps(records,indent=2))
