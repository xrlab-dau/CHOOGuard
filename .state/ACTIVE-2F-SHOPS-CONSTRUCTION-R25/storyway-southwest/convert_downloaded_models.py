import bpy,pathlib,json,math
from mathutils import Matrix
p=pathlib.Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/storyway-southwest');out=p/'sources/construction';rows={}
for key in ['chips','can']:
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 src=p/'sources/free-models'/key/(key+'.glb');bpy.ops.import_scene.gltf(filepath=str(src))
 mats=[]
 for idx,m in enumerate([m for m in bpy.data.materials if m.users]):
  old=m.name;m.name='SW_SOURCE_'+key.upper()+'_'+str(idx);maps={}
  if m.use_nodes:
   for node in m.node_tree.nodes:
    if node.type=='BSDF_PRINCIPLED':
     for slot,slug in [('Base Color','base'),('Normal','normal')]:
      inp=node.inputs.get(slot)
      if inp and inp.is_linked:
       inn=inp.links[0].from_node
       if inn.type=='NORMAL_MAP' and inn.inputs['Color'].is_linked:inn=inn.inputs['Color'].links[0].from_node
       if inn.type=='TEX_IMAGE' and inn.image:
        f=out/'Textures'/f'{key}_{idx}_{slug}.png';f.parent.mkdir(exist_ok=True,parents=True);inn.image.filepath_raw=str(f);inn.image.file_format='PNG';inn.image.save();maps[slug]=str(f)
  mats.append({'original':old,'source_slot':m.name,'maps':maps})
 obs=[o for o in bpy.context.scene.objects if o.type=='MESH'];f=out/'Models'/f'{key}-source.fbx';f.parent.mkdir(exist_ok=True,parents=True);bpy.ops.object.select_all(action='DESELECT')
 for ob in obs:ob.select_set(True)
 
 for ob in obs:
  mw=ob.matrix_world.copy();ob.parent=None;ob.matrix_world=(Matrix.Rotation(math.pi/2,4,'X')@mw) if key=='chips' else mw
 bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);bpy.ops.export_scene.fbx(filepath=str(f),use_selection=True,object_types={'MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='AUTO')
 rows[key]={'source':str(src),'own_derivative':str(f),'objects':[{'name':o.name,'dimensions_zup':list(o.dimensions),'materials':[m.name for m in o.data.materials]} for o in obs],'materials':mats,'derivation':'Source transforms retained; FBX coordinate conversion only, source original immutable; own named source material slots for explicit URP mapping.'}
(p/'SOURCE-DOWNLOADED-MODEL-SLOTS.json').write_text(json.dumps(rows,indent=2))
