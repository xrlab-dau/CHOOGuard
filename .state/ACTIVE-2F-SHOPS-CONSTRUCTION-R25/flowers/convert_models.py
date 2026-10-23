import bpy,json
from pathlib import Path
S=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/flowers/sources')
records=[]
for key in ['rose_bouquet','white_shelf','modern_shelf']:
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 bpy.ops.import_scene.gltf(filepath=str(S/key/(key+'.glb')))
 mats=[]
 for mat in bpy.data.materials:
  if mat.users==0:continue
  rec={'name':mat.name,'color':list(mat.diffuse_color),'textures':[]}
  if mat.use_nodes:
   for n in mat.node_tree.nodes:
    if n.type=='TEX_IMAGE' and n.image:
     img=n.image;p=S/key/(img.name.replace('/','_')+'.png');img.filepath_raw=str(p);img.file_format='PNG';img.save();rec['textures'].append({'file':str(p),'node':n.name,'links':[l.to_socket.name for l in n.outputs[0].links]})
  mats.append(rec)
 meshes=[{'name':o.name,'materials':[m.name if m else None for m in o.data.materials],'verts':len(o.data.vertices),'bound_box':[list(x) for x in o.bound_box]} for o in bpy.context.scene.objects if o.type=='MESH']
 bpy.ops.export_scene.fbx(filepath=str(S/key/(key+'.fbx')),use_selection=False,object_types={'MESH'},add_leaf_bones=False,bake_anim=False,path_mode='STRIP',axis_forward='-Z',axis_up='Y')
 records.append({'key':key,'materials':mats,'meshes':meshes})
(S/'CONVERSION-MATERIALS.json').write_text(json.dumps(records,indent=2))
