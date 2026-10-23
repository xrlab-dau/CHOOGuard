import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
b=Path('/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/olive-young/sources/free-models');report={}

def bbox(obs):
 pts=[o.matrix_world@Vector(c) for o in obs for c in o.bound_box];lo=Vector([min(p[k] for p in pts) for k in range(3)]);hi=Vector([max(p[k] for p in pts) for k in range(3)]);return lo,hi

def flatten(obs):
 for o in obs:
  mw=o.matrix_world.copy();o.parent=None;o.matrix_world=mw

def extract_materials(folder,obs):
 mats={m.name:m for o in obs for m in o.data.materials if m};out=[];texdir=folder/'textures';texdir.mkdir(exist_ok=True)
 for name,m in mats.items():
  a={'name':name,'color':list(m.diffuse_color),'metallic':m.metallic,'roughness':m.roughness,'textures':[]}
  if m.use_nodes:
   for n in m.node_tree.nodes:
    if n.type=='TEX_IMAGE' and n.image:
     im=n.image;file=texdir/(im.name.replace('/','_').replace('\\','_')+'.png');im.filepath_raw=str(file);im.file_format='PNG';im.save();labels=[l.to_node.label or l.to_node.name for l in n.outputs['Color'].links];a['textures'].append({'file':str(file),'links':labels})
  out.append(a)
 (folder/'UNITY-MATERIAL-SOURCES-OWN.json').write_text(json.dumps(out,indent=2));return out

def export(key,normalize=None,rotate=None,selection=None):
 folder=b/key;bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(folder/(key+'.glb')))
 obs=[o for o in bpy.context.scene.objects if o.type=='MESH'];flatten(obs)
 if selection:obs=[o for o in obs if selection(o)]
 for o in list(bpy.context.scene.objects):
  if o.type=='MESH' and o not in obs:bpy.data.objects.remove(o,do_unlink=True)
 lo,hi=bbox(obs);before=list(hi-lo)
 if rotate:
  mat=Matrix.Rotation(rotate[1],4,rotate[0]);
  for o in obs:o.matrix_world=mat@o.matrix_world
 # Mild planar collapse on own derivatives only: remove source render subdivisions, preserve disconnected physical objects and material slots.
 for o in obs:
  if len(o.data.polygons)>15000 and key in ['tube','foil-pouch']:
   bpy.context.view_layer.objects.active=o;o.select_set(True);mod=o.modifiers.new('OwnSmallMerchandiseSurface','DECIMATE');mod.decimate_type='DISSOLVE';mod.angle_limit=math.radians(2);bpy.ops.object.modifier_apply(modifier=mod.name);
   if len(o.data.polygons)>4500:
    mod=o.modifiers.new('OwnSmallSourceSubdivisionDerivative','DECIMATE');mod.ratio=4500/len(o.data.polygons);bpy.ops.object.modifier_apply(modifier=mod.name)
   o.select_set(False)
 lo,hi=bbox(obs);dims=hi-lo;maxdim=max(dims)
 factor=1/maxdim
 for o in obs:
  o.matrix_world=Matrix.Scale(factor,4)@o.matrix_world
 lo,hi=bbox(obs);offset=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
 for o in obs:o.location-=offset
 bpy.ops.object.select_all(action='DESELECT')
 for o in obs:o.select_set(True)
 bpy.context.view_layer.objects.active=obs[0]
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 mats=extract_materials(folder,obs)
 bpy.ops.export_scene.fbx(filepath=str(folder/(key+'-Own.fbx')),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP',apply_unit_scale=True)
 lo,hi=bbox(obs);report[key]={'original_world_dimensions':before,'own_world_dimensions':list(hi-lo),'objects':[{'name':o.name,'vertices':len(o.data.vertices),'faces':len(o.data.polygons),'materials':[m.name if m else '' for m in o.data.materials]} for o in obs],'materials':mats}

export('tube')
export('pump')
export('lipstick')
export('makeup')
export('foil-pouch')
(b/'OWN-DERIVATIVE-MODEL-REPORT.json').write_text(json.dumps(report,indent=2))
print('OWN_DERIVATIVES',json.dumps({k:{'dims':v['own_world_dimensions'],'objects':len(v['objects']),'faces':sum(o['faces'] for o in v['objects'])}for k,v in report.items()}))
