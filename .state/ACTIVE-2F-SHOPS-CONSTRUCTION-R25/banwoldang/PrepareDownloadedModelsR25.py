import bpy,sys,json
from pathlib import Path
from mathutils import Vector
B=Path(__file__).resolve().parent;A=B/'sources/models'
for slug in ['tray','chicken','slotted-spatula','ladle']:
 p=A/slug;src=p/(slug+'.glb');bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(src));bpy.context.scene.frame_set(0)
 for o in list(bpy.data.objects):
  if o.type not in {'MESH','EMPTY'}:bpy.data.objects.remove(o,do_unlink=True)
 before=sum(len(o.data.polygons) for o in bpy.data.objects if o.type=='MESH')
 if slug=='chicken':
  for o in bpy.data.objects:
   if o.type=='MESH':
    bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('OWN_SCANNED_FOOD_2400_TRI_DERIVATIVE','DECIMATE');mod.ratio=.24;bpy.ops.object.modifier_apply(modifier=mod.name)
 materials=[];imdir=p/'textures';imdir.mkdir(exist_ok=True)
 images={}
 for im in bpy.data.images:
  if im.type=='IMAGE' and im.size[0]:
   name=''.join(c if c.isalnum() or c in '_-' else '_' for c in im.name)+'.png';out=imdir/name;im.filepath_raw=str(out);im.file_format='PNG';im.save();images[im.name]=str(out)
 for mat in bpy.data.materials:
  tx={}
  for node in mat.node_tree.nodes if mat.use_nodes else []:
   if node.type=='TEX_IMAGE' and node.image:
    tx[node.label or node.name]={'file':images.get(node.image.name),'outputs_to':[a.to_socket.name for out in node.outputs for a in out.links]}
  bs=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) if mat.use_nodes else None
  materials.append({'name':mat.name,'textures':tx,'color':list(bs.inputs['Base Color'].default_value) if bs else list(mat.diffuse_color),'metallic':float(bs.inputs['Metallic'].default_value) if bs else 0,'roughness':float(bs.inputs['Roughness'].default_value) if bs else .5})
 objects=[]
 for o in bpy.data.objects:
  if o.type=='MESH':
   pts=[o.matrix_world@Vector(v) for v in o.bound_box];objects.append({'name':o.name,'materials':[m.name for m in o.data.materials], 'min':[min(v[i] for v in pts) for i in range(3)],'max':[max(v[i] for v in pts) for i in range(3)],'vertices':len(o.data.vertices),'polygons':len(o.data.polygons)})
 bpy.ops.object.select_all(action='SELECT');out=p/(slug+'-Unity.fbx');bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='STRIP',embed_textures=False)
 (p/'UNITY-PBR-MATERIALS.json').write_text(json.dumps({'source_glb':str(src),'source_unchanged':True,'own_derivative':str(out),'original_polygon_count':before,'materials':materials,'objects':objects},indent=2));print('PREPARED',slug,objects,flush=True)
