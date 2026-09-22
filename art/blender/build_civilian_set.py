"""Headless metre-native civilian variants; original Kenney rig and real motion retained."""
import bpy, math, json, pathlib, hashlib
from mathutils import Vector
ROOT=pathlib.Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/ChooGuard/Art/CivilianSet';OUT.mkdir(parents=True,exist_ok=True)
DETAIL={'cylinder_sides':16,'sphere_rings':8,'bevel_segments':2}
EXPORT_MANIFEST=[];M={}
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def material(name,color):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*color,1);m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.8;M[name]=m
for name,color in {'CivilianJacket':(.27,.17,.105),'CivilianBlue':(.10,.29,.45),'CivilianRose':(.48,.20,.24),'Trousers':(.065,.08,.10),'Rubber':(.12,.10,.085),'Skin':(.62,.40,.28),'Hair':(.045,.025,.017),'Cream':(.72,.68,.57),'Bag':(.10,.085,.07)}.items():material(name,color)
def set_motion(rig,action):
 rig.animation_data_create();rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
COL=None
assets={}
def collection(name):
 global COL
 COL=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(COL);assets[name]=COL;return COL
def assign(obj,name,mat):
 obj.name=name
 for c in list(obj.users_collection):c.objects.unlink(obj)
 COL.objects.link(obj)
 if mat:obj.data.materials.append(M[mat])
 return obj
def box(name,loc,size,mat,bevel=.03):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=assign(bpy.context.object,name,mat);o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=o.modifiers.new('Manufactured edges','BEVEL');mod.width=bevel;mod.segments=DETAIL['bevel_segments'];mod.affect='EDGES'
  mod=o.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL')
 return o
def cyl(name,loc,r,depth,mat,axis='Z',vertices=DETAIL['cylinder_sides']):
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=loc);o=assign(bpy.context.object,name,mat)
 if axis=='X':o.rotation_euler[1]=math.pi/2
 if axis=='Y':o.rotation_euler[0]=math.pi/2
 for p in o.data.polygons:p.use_smooth=True
 return o
def beam(name,a,b,r,mat):
 a,b=Vector(a),Vector(b);o=cyl(name,(a+b)/2,r,(b-a).length,mat);o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o
def mesh(name,verts,faces,mat):
 m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update();o=bpy.data.objects.new(name,m);COL.objects.link(o);o.data.materials.append(M[mat]);return o
def export_asset(name,col,animate=False):
 # Merge by material while preserving original skin weights/armature modifiers.
 groups={}
 for obj in list(col.all_objects):
  if obj.type!='MESH':continue
  bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
  for modifier in list(obj.modifiers):
   if modifier.type!='ARMATURE':bpy.ops.object.modifier_apply(modifier=modifier.name)
  key=tuple(m.name for m in obj.data.materials);groups.setdefault(key,[]).append(obj)
 for group in groups.values():
  bpy.ops.object.select_all(action='DESELECT')
  for obj in group:obj.select_set(True)
  bpy.context.view_layer.objects.active=group[0]
  if len(group)>1:bpy.ops.object.join()
 bpy.context.view_layer.update()
 objects=list(col.all_objects);vertices=[o.matrix_world@v.co for o in objects if o.type=='MESH' for v in o.data.vertices]
 minimum=min(v.z for v in vertices)
 for obj in objects:
  if obj.parent not in objects:obj.location.z-=minimum
 bpy.context.view_layer.update()
 vertices=[o.matrix_world@v.co for o in objects if o.type=='MESH' for v in o.data.vertices];low=[min(v[k] for v in vertices) for k in range(3)];high=[max(v[k] for v in vertices) for k in range(3)]
 EXPORT_MANIFEST.append({'name':name,'fbx':'Assets/ChooGuard/Art/CivilianSet/'+name+'.fbx','units':'authored metres','boundsBlenderRestMesh':{'min':low,'max':high},'dimensionsMetres':{'width':high[0]-low[0],'depth':high[1]-low[1],'height':high[2]-low[2]},'grounded':abs(low[2])<.0001,'horizontalOrigin':'OSM origin retained' if name=='BusanFacade' else 'authored model origin','materials':sorted({m.name for o in objects if o.type=='MESH' for m in o.data.materials}),'fontObjects':sum(o.type=='FONT' for o in objects),'textMeshes':0,'facadeDesign':None})
 bpy.ops.object.select_all(action='DESELECT')
 for obj in col.all_objects:obj.select_set(True)
 if not any(o.type=='MESH' for o in col.all_objects):return
 bpy.context.view_layer.objects.active=next(o for o in col.all_objects if o.type=='MESH')
 bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,bake_anim=animate,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=animate,path_mode='AUTO',mesh_smooth_type='FACE')
def crew(name,role):
 col=collection(name)
 before=set(bpy.data.objects)
 bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/ChooGuard/ThirdParty/Models/MiniCharacters/character-male-a.fbx'))
 imported=set(bpy.data.objects)-before;rig=next(o for o in imported if o.type=='ARMATURE');top=next(o for o in imported if o.type=='EMPTY')
 for o in imported:
  for old in list(o.users_collection):old.objects.unlink(o)
  col.objects.link(o)
 for o in list(imported):
  if o.type=='MESH':bpy.data.objects.remove(o,do_unlink=True)
 rig.animation_data_clear()
 uniform='Civilian'+role
 def bind(o,bone):
  if o.type!='MESH':return
  group=o.vertex_groups.new(name=bone);group.add(list(range(len(o.data.vertices))),1,'REPLACE');mod=o.modifiers.new('Original Kenney bone motion','ARMATURE');mod.object=rig
  world=o.matrix_world.copy();o.parent=top;o.matrix_world=world
 def limb(name,a,b,r,mat,bone):
  o=beam(name,a,b,r,mat);bind(o,bone);return o
 def ellipsoid(name,loc,scale,mat,bone):
  bpy.ops.mesh.primitive_uv_sphere_add(segments=DETAIL['cylinder_sides'],ring_count=DETAIL['sphere_rings'],radius=1,location=loc);o=assign(bpy.context.object,name,mat);o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
  for p in o.data.polygons:p.use_smooth=True
  bind(o,bone);return o
 # Continuous clothed lofts with proximal torso-to-limb weight blending.
 def loft(name,rings,bone,axis='Z',weights=None,mat=uniform):
  vertices=[];faces=[];steps=16
  for center,rx,ry in rings:
   for n in range(steps):
    angle=2*math.pi*n/steps
    offset=Vector((rx*math.cos(angle),ry*math.sin(angle),0)) if axis=='Z' else Vector((0,rx*math.cos(angle),ry*math.sin(angle)))
    vertices.append(tuple(Vector(center)+offset))
  for r in range(len(rings)-1):
   for n in range(steps):faces.append((r*steps+n,r*steps+(n+1)%steps,(r+1)*steps+(n+1)%steps,(r+1)*steps+n))
  faces.extend([tuple(reversed(range(steps))),tuple((len(rings)-1)*steps+n for n in range(steps))]);o=mesh(name,vertices,faces,mat)
  for polygon in o.data.polygons:polygon.use_smooth=True
  limb_group=o.vertex_groups.new(name=bone);torso_group=o.vertex_groups.new(name='torso') if bone!='torso' else None
  for ring in range(len(rings)):
   weight=weights[ring] if weights else 1.;indices=list(range(ring*steps,(ring+1)*steps));limb_group.add(indices,weight,'REPLACE')
   if torso_group and weight<1:torso_group.add(indices,1-weight,'REPLACE')
  modifier=o.modifiers.new('Original rig, blended clothing skin','ARMATURE');modifier.object=rig
  world=o.matrix_world.copy();o.parent=top;o.matrix_world=world
  return o
 loft('Continuous tailored jacket',[((0,.024,.174),.073,.047),((0,.024,.195),.076,.05),((0,.022,.225),.069,.049),((0,.02,.27),.087,.054),((0,.019,.292),.087,.048),((0,.018,.315),.027,.026)],'torso')
 for side in [-1,1]:
  bone='leg-left' if side>0 else 'leg-right';x=side*.062
  loft('Continuous trousers',[((x,.025,.187),.037,.044),((x,.023,.155),.033,.036),((x,.02,.109),.029,.033),((x,.017,.07),.026,.028),((x,.014,.027),.024,.026)],bone,weights=[0,.7,1,1,1],mat='Trousers')
  ellipsoid('Everyday shoe',(x,-.009,.023),(.028,.049,.022),'Rubber',bone)
  bone='arm-left' if side>0 else 'arm-right'
  loft('Continuous jacket sleeve',[((side*.062,.019,.285),.033,.043),((side*.105,.019,.285),.031,.035),((side*.165,.018,.279),.027,.03),((side*.213,.015,.272),.023,.027),((side*.255,.014,.266),.021,.022)],bone,'X',[0,.5,1,1,1])
  ellipsoid('Hand',(side*.268,.013,.264),(.018,.021,.019),'Skin',bone)
 ellipsoid('Neck',(0,.015,.327),(.023,.025,.032),'Skin','head')
 ellipsoid('Adult head',(0,.006,.365),(.035,.038,.034),'Skin','head')
 ellipsoid('Nose',(0,-.034,.361),(.004,.005,.006),'Skin','head')
 ellipsoid('Natural hair crown',(0,.013,.388),(.036,.037,.014),'Hair','head')
 ellipsoid('Natural hair back',(0,.034,.375),(.034,.014,.024),'Hair','head')
 if role=='Jacket':
  o=box('Small everyday backpack',(0,.085,.25),(.095,.048,.10),'Bag',.012);bind(o,'torso')
 top.scale.x*=1.55;top.scale.y*=1.65;top.scale.z*=3.8
 bpy.context.view_layer.update()
 points=[o.matrix_world@v.co for o in col.all_objects if o.type=='MESH' for v in o.data.vertices]
 factor=1.72/(max(v.z for v in points)-min(v.z for v in points));top.scale*=factor
 # Preserve only existing real idle/walk/interact actions, not synthetic animation.
 for action in list(bpy.data.actions):
  if not any(key in action.name for key in ['|idle|','|walk|','|interact-right|']):
   bpy.data.actions.remove(action)
 rig.animation_data_create();idle=next(a for a in bpy.data.actions if '|idle|' in a.name)
 # Explicit strip slots survive Blender's FBX NLA exporter; its all-actions path loses slots.
 for key in ['idle','walk','interact-right']:
  action=next(a for a in bpy.data.actions if '|'+key+'|' in a.name);track=rig.animation_data.nla_tracks.new();track.name=key;strip=track.strips.new(key,1,action);strip.action_slot=action.slots[0];strip.name=key
 rig.animation_data.action=None
 bpy.context.scene.frame_set(1);bpy.context.view_layer.update();export_asset(name,col,True)
 for track in rig.animation_data.nla_tracks:track.mute=True
 walk=next(a for a in bpy.data.actions if '|walk|' in a.name);set_motion(rig,walk);bpy.context.scene.frame_set(14);bpy.context.view_layer.update()
 rig.name=name+'Rig';top.name=name+'Root'
 return col

for role in ['Jacket','Blue','Rose']:crew('Civilian'+role,role)
for row in EXPORT_MANIFEST:
 assert abs(row['dimensionsMetres']['height']-1.72)<.0001 and row['grounded'] and row['fontObjects']==0
 row['sha256']=hashlib.sha256((ROOT/row['fbx']).read_bytes()).hexdigest()
manifest={'source':'Assets/ChooGuard/ThirdParty/Models/MiniCharacters/character-male-a.fbx','sourceSha256':hashlib.sha256((ROOT/'Assets/ChooGuard/ThirdParty/Models/MiniCharacters/character-male-a.fbx').read_bytes()).hexdigest(),'provenance':'Existing Kenney Mini Characters rig and original idle/walk/interact actions; newly authored civilian clothing mesh; no downloaded files','models':EXPORT_MANIFEST,'materials':{n:list(m.diffuse_color) for n,m in M.items()},'verification':'Blender rest mesh metre bounds; original animated actions exported; Unity import/runtime unverified'}
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
for i,(name,col) in enumerate(assets.items()):
 objs=list(col.all_objects)
 for obj in objs:
  if obj.parent not in objs:obj.location.x+=(i-1)*1.25
rigs=[o for o in bpy.data.objects if o.type=='ARMATURE']
for rig in rigs:
 set_motion(rig,next(a for a in bpy.data.actions if '|walk|' in a.name))
bpy.context.scene.frame_set(4)
bpy.ops.mesh.primitive_plane_add(size=200);bpy.context.object.data.materials.append(M['Cream'])
world=bpy.data.worlds.new('Civilian preview world');bpy.context.scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.3,.35,.4,1)
bpy.ops.object.light_add(type='AREA',location=(1,-3,6));bpy.context.object.data.energy=650;bpy.context.object.data.size=5
bpy.ops.object.camera_add(location=(3,-7,2.8));camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,.9))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=4.5
scene=bpy.context.scene;scene.camera=camera;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1400;scene.render.resolution_y=800;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(ROOT/'art/blender/civilian-set-preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'art/blender/civilian_set.blend'));bpy.ops.render.render(write_still=True)
