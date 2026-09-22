"""Blender 4.5: authored Korean transport/response hero set, no proprietary game assets."""
import bpy, math, json, pathlib, sys, hashlib
from mathutils import Vector
ROOT=pathlib.Path(__file__).resolve().parents[2]
if '--resume-render' in sys.argv:
 # Recover after successful export/save without regenerating any mesh or FBX.
 art=ROOT/'art/blender';out=ROOT/'Assets/ChooGuard/Art/ResponseSet';manifest=json.loads((art/'response-set-metre-manifest.json').read_text());geometry_sha=manifest['scriptSha256'];current_sha=hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest()
 for row in manifest['models']:
  if hashlib.sha256((ROOT/row['fbx']).read_bytes()).hexdigest()!=row['sha256']:raise ValueError('Export changed before render recovery')
 bpy.ops.wm.open_mainfile(filepath=str(art/'response_set.blend'))
 site=json.loads((ROOT/'asset-library/space-references/busan-reconstruction/busan-site-selected.json').read_text());points=next(f for f in site['features'] if f['id']=='165346389')['points'][4:20];front=[Vector((p['x'],p['z'],0)) for p in points];entries=[];index=0
 for a,b in zip(front,front[1:]):
  delta=b-a;tangent=delta.normalized();normal=Vector((-tangent.y,tangent.x,0));count=max(1,math.ceil(delta.length/1.5))
  for j in range(count):
   if index%12==6:entries.append((a+delta*((j+.5)/count),normal,tangent))
   index+=1
 entry,outward,tangent=min(entries,key=lambda e:abs(e[0].y))
 for row in manifest['models']:
  name=row['name'];col=bpy.data.collections[name];objects=list(col.all_objects)
  if name=='BusanFacade':row['facadeDesign']['entranceCanopyClearHeight']=3.17;continue
  if not name.startswith('Crew'):
   for obj in objects:obj.hide_render=True
   continue
  index=['CrewOperations','CrewFire','CrewMedical'].index(name);rig=next(o for o in objects if o.type=='ARMATURE');idle=next(a for a in bpy.data.actions if '|idle|' in a.name);rig.animation_data.action=idle;rig.animation_data.action_slot=idle.slots[0];bpy.context.scene.frame_set(1)
  parent=bpy.data.objects.new(name+' metre preview',None);bpy.context.scene.collection.objects.link(parent)
  for obj in objects:
   if obj.parent not in objects:matrix=obj.matrix_world.copy();obj.parent=parent;obj.matrix_world=matrix
  scale=1.78/row['dimensionsMetres']['height'];parent.scale=(scale,)*3;parent.location=entry+outward*2+tangent*((index-1)*1.4)+Vector((0,0,.3));parent.rotation_euler[2]=math.atan2(outward.y,outward.x)+math.pi/2
 bpy.ops.mesh.primitive_cube_add(size=1,location=(entry.x,entry.y,-.10));ground=bpy.context.object;ground.name='Metre plaza';ground.dimensions=(65,65,.2);ground.data.materials.append(bpy.data.materials['Concrete'])
 world=bpy.context.scene.world or bpy.data.worlds.new('World');bpy.context.scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.2,.25,.3,1);world.node_tree.nodes['Background'].inputs[1].default_value=.6
 bpy.ops.object.light_add(type='AREA',location=entry+outward*12+Vector((0,0,25)));bpy.context.object.data.energy=4200;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=20
 bpy.ops.object.light_add(type='SUN');bpy.context.object.data.energy=2;bpy.context.object.rotation_euler=(.4,-.5,-.5)
 bpy.ops.object.camera_add(location=entry+outward*24+tangent*12+Vector((0,0,11)));camera=bpy.context.object;camera.rotation_euler=(entry+Vector((0,0,7.8))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=32
 scene=bpy.context.scene;scene.camera=camera;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=2048;scene.render.resolution_y=1536;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=str(art/'response-set-metre-preview.png');bpy.ops.render.render(write_still=True)
 manifest['geometryScriptSha256']=geometry_sha;manifest['previewScriptSha256']=current_sha;manifest['recovery']='Snapshot collection iteration; saved blend reused after native preview crash; FBXs not regenerated';(art/'response-set-metre-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n');(out/'metre-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
 receipt={'executionMode':'headless generation/export followed by saved-blend render-only recovery','blender':bpy.app.version_string,'geometryScriptSha256':geometry_sha,'previewScriptSha256':current_sha,'spatialContract':'.planning/2026-09-20-integrated-build/spatial-metre-contract.json','exports':[r['name'] for r in manifest['models']],'metreManifest':'art/blender/response-set-metre-manifest.json','renderCount':1,'preview':'art/blender/response-set-metre-preview.png','textPolicy':'No FONT or authored text meshes','facadeDesign':next(r['facadeDesign'] for r in manifest['models'] if r['name']=='BusanFacade'),'scope':'OSM horizontal coordinates; vertical/details are unmeasured design assumptions; no Unity verification'}
 (art/'build-receipt.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n');sys.exit(0)
PACK=json.loads((ROOT/'art/blender/jev-parameters.json').read_text())
SPATIAL_PATH=ROOT/'.planning/2026-09-20-integrated-build/spatial-metre-contract.json'
SPATIAL=json.loads(SPATIAL_PATH.read_text())
if SPATIAL['units']['osmSourceScale']!=1 or SPATIAL['units']['unityUnitsPerMetre']!=1:raise ValueError('Metre-native authoring contract required')
SCRIPT_SHA=hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest()
EXPORT_MANIFEST=[]
FACADE_DESIGN={'sourceXYScale':1,'floorToFloor':SPATIAL['designNotMeasured']['floorToFloor'],'storeys':3,'roofLow':16.2,'roofHigh':16.8,'glazingBayMaximum':1.5,'doorClearHeight':2.2,'doorLeafWidth':1.0,'terraceHeight':.3,'entranceCanopyClearHeight':3.17,'referenceAdultHeight':SPATIAL['designNotMeasured']['adultCivilianHeight'],'precision':'Horizontal frontage source-derived; all vertical/detail values unmeasured design assumptions'}
ALLOWED={'glassPalette':{'blue_grey'},'framePlacement':{'outside_glass'},'detailScale':{'rts_readable'},'crewFix':{'correct_action_slot_first'},'trainWindows':{'conform_to_body_surface'},'previewGate':{'front_threequarter_walk_pose'}}
P=PACK.get('parameters',{})
if set(P)!=set(ALLOWED):raise ValueError('Incomplete constrained Jev parameter pack')
for key,choices in ALLOWED.items():
 if P[key] not in choices:raise ValueError('Unsupported Jev choice: '+key+'='+str(P[key]))
GLASS_SPEC={'blue_grey':{'color':(.16,.22,.25),'metallic':.45,'roughness':.24}}[P['glassPalette']]
FRAME_OFFSET={'outside_glass':-.15}[P['framePlacement']]
DETAIL={'rts_readable':{'cylinder_sides':16,'sphere_rings':8,'bevel_segments':2,'frame_radius':.065}}[P['detailScale']]
CREW_OPERATION={'correct_action_slot_first':'bind_action_and_nla_slots_before_bake'}[P['crewFix']]
WINDOW_SPEC={'conform_to_body_surface':{'profile':[(1,0),(1,.45),(.88,.83),(.55,1),(-.55,1),(-.88,.83),(-1,.45),(-1,0),(-.85,-.55),(-.5,-.68),(.5,-.68),(.85,-.55)],'surface_offset':.015,'center_z':1.49,'height':.48}}[P['trainWindows']]
PREVIEW_VIEWS={'front_threequarter_walk_pose':('three_quarter','front','walk')}[P['previewGate']]
APPLIED={'spatialContract':str(SPATIAL_PATH.relative_to(ROOT)),'facadeDesign':FACADE_DESIGN,'worldTextPolicy':'no FONT or text meshes','requestedRenders':1,'glass':GLASS_SPEC,'mullionOffsetMeters':FRAME_OFFSET,'detailBudget':DETAIL,'crewOperation':CREW_OPERATION,'trainWindowConstruction':WINDOW_SPEC,'previousPreviewRequest':PREVIEW_VIEWS,'previewViews':('metre_entry_with_people',)}
def set_motion(rig,action):
 if CREW_OPERATION!='bind_action_and_nla_slots_before_bake':raise ValueError('Crew operation unavailable')
 rig.animation_data_create();rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
# Jev supplies choices, not geometry/code or image inspection; deterministic operations below implement them.
OUT=ROOT/'Assets/ChooGuard/Art/ResponseSet' ;OUT.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'art/blender';SOURCE.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):
 if c.name!='Collection':bpy.data.collections.remove(c)
M={}
def material(name,color,metal=0,rough=.5,alpha=1):
 m=bpy.data.materials.new(name);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,alpha);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 m.diffuse_color=(*color,alpha);M[name]=m;return m
material('Pearl',(0.74,.78,.79),.35,.29);material('Navy',(.025,.07,.14),.3,.32);material('Glass',GLASS_SPEC['color'],GLASS_SPEC['metallic'],GLASS_SPEC['roughness']);material('Steel',(.43,.48,.5),.72,.3);material('Roof',(.17,.20,.21),.5,.43);material('Concrete',(.55,.53,.47),0,.85);material('Red',(.64,.045,.032),.2,.33);material('AmbulanceWhite',(.84,.85,.8),.15,.38);material('Orange',(.92,.24,.04),.1,.45);material('Reflective',(.78,.88,.18),.15,.4);material('Rubber',(.018,.023,.026),0,.88);material('Lamp',(.72,.88,.92),.15,.2);material('Skin',(.55,.34,.23),0,.65);material('Uniform',(.035,.085,.13),0,.75);material('PPE',(.47,.39,.21),0,.76);material('Medic',(.12,.29,.32),0,.65)
for name,folder in [('Concrete','Concrete031'),('Steel','Metal032')]:
 path=ROOT/f'asset-library/models-materials/extracted/{folder}_1K-JPG/{folder}_1K-JPG_Color.jpg'
 if path.exists():
  m=M[name];node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=bpy.data.images.load(str(path));m.node_tree.links.new(node.outputs['Color'],m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
  coord=m.node_tree.nodes.new('ShaderNodeTexCoord');m.node_tree.links.new(coord.outputs['Generated'],node.inputs['Vector'])
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
# No FONT objects or text geometry: world text is excluded by the current user contract.
collection('BusanFacade')
site=json.loads((ROOT/'asset-library/space-references/busan-reconstruction/busan-site-selected.json').read_text())
points=next(f for f in site['features'] if f['id']=='165346389')['points'][4:20]
front=[Vector((p['x'],p['z'],0)) for p in points]
bay_index=0
ENTRANCES=[]
for i in range(len(front)-1):
 a,b=front[i],front[i+1];d=b-a;length=d.length;tangent=d.normalized();outward=Vector((-tangent.y,tangent.x,0));angle=math.atan2(d.y,d.x)-math.pi/2
 segments=max(1,math.ceil(length/FACADE_DESIGN['glazingBayMaximum']));spacing=length/segments
 def facade_box(name,position,depth,width,height,mat,bevel=.02):
  o=box(name,position,(depth,width,height),mat,bevel);o.rotation_euler[2]=angle;return o
 for j in range(segments):
  middle=a+d*((j+.5)/segments)
  for floor in range(3):facade_box('Metre glazing bay',middle+Vector((0,0,.3+floor*5+2.25)),.085,spacing-.08,4.5,'Glass',.01)
  p=a+d*(j/segments)+outward*abs(FRAME_OFFSET);beam('Metre vertical mullion',(p.x,p.y,.3),(p.x,p.y,15.4),DETAIL['frame_radius'],'Steel')
  if bay_index%12==6:
   ENTRANCES.append((middle.copy(),outward.copy(),tangent.copy()))
   floor=.3
   for side in [-.55,.55]:
    p=middle+tangent*side+outward*.13
    facade_box('Door leaf 2.2m',p+Vector((0,0,floor+1.1)),.14,1.0,2.2,'Navy',.012)
    facade_box('Door vision panel',p+outward*.10+Vector((0,0,floor+1.35)),.04,.76,1.45,'Glass',.018)
    h=p+outward*.18+tangent*.32;beam('Door pull handle',(h.x,h.y,floor+.8),(h.x,h.y,floor+1.45),.022,'Steel')
   facade_box('Human scale entrance canopy',middle+outward*1.7+Vector((0,0,3.55)),3.5,3.6,.16,'Roof',.025)
   for side in [-1.5,1.5]:
    p=middle+outward*3+tangent*side;beam('Canopy column',(p.x,p.y,.3),(p.x,p.y,3.46),.06,'Steel')
   for step in range(2):facade_box('Entrance step',middle+outward*(3.65+step*.3)+Vector((0,0,.225-step*.075)),.32,3.6,.15,'Concrete',.01)
  bay_index+=1
 for z in [.3,1.8,3.3,5.0,6.8,8.3,10.0,11.8,13.3,15.4]:
  aa=a+outward*abs(FRAME_OFFSET);bb=b+outward*abs(FRAME_OFFSET);beam('Metre horizontal mullion',(aa.x,aa.y,z),(bb.x,bb.y,z),DETAIL['frame_radius'],'Steel')
 beam('Primary steel rib',(a.x,a.y,.15),(a.x,a.y,16.25),.13,'Steel')
 front_a=a+outward*1.8;front_b=b+outward*1.8;rear_a=a-outward*6;rear_b=b-outward*6
 mesh('Metre curved eave',[(front_a.x,front_a.y,16.2),(front_b.x,front_b.y,16.2),(rear_b.x,rear_b.y,16.8),(rear_a.x,rear_a.y,16.8)],[(0,1,2,3)],'Roof')
 beam('Eave silver edge',(front_a.x,front_a.y,16.2),(front_b.x,front_b.y,16.2),.095,'Steel');beam('Roof rib',(front_a.x,front_a.y,16.2),(rear_a.x,rear_a.y,16.8),.055,'Steel')
 facade_box('Metre terrace',(a+b)/2+outward*1.75+Vector((0,0,.15)),3.5,length,.3,'Concrete',.02)
# Train: revolved/swept cross sections, sloped nose, continuous glazing, doors and bogies.
def train(name,coach=False):
 collection(name)
 rings=[(-12,.08,.5),(-11.5,.7,.75),(-10,1.27,1.3),(-8,1.42,1.8),(-6,1.45,1.85),(10,1.45,1.85),(11.5,1.25,1.65)] if not coach else [(-11.7,1.25,1.7),(-11,1.45,1.85),(11,1.45,1.85),(11.7,1.25,1.7)]
 verts=[];count=12
 for y,width,height in rings:
  for n in range(count):
   cross=WINDOW_SPEC['profile'];xx,zz=cross[n];verts.append((xx*width,y,1.18+zz*height*.7))
 faces=[]
 for r in range(len(rings)-1):
  for n in range(count):faces.append((r*count+n,r*count+(n+1)%count,(r+1)*count+(n+1)%count,(r+1)*count+n))
 faces.extend([tuple(reversed(range(count))),tuple((len(rings)-1)*count+n for n in range(count))]);o=mesh('Aerodynamic shell',verts,faces,'Pearl')
 bevel=o.modifiers.new('Shell edge fillet','BEVEL');bevel.width=.07;bevel.segments=3;o.modifiers.new('Body normals','WEIGHTED_NORMAL')
 box('Underframe',(0,0,.48),(2.35,20,.4),'Navy',.1)
 for side in [-1,1]:
  box('Navy belt',(side*1.42,1,1.05),(.07,18.7,.34),'Navy',.015)
  for y in range(-5,10,2):box('Passenger window',(side*(1.45+WINDOW_SPEC['surface_offset']),y,WINDOW_SPEC['center_z']),(.025,1.33,WINDOW_SPEC['height']),'Glass',.07)
  for y in [-6.8,10]:
   box('Passenger door',(side*1.46,y,1.38),(.045,.78,1.35),'Steel',.02);box('Door window',(side*1.49,y,1.75),(.04,.52,.5),'Glass',.035)
 for y in [-6.5,7.5]:
  box('Bogie',(0,y,.4),(2.18,2.3,.45),'Rubber',.08)
  for yy in [y-.7,y+.7]:
   for x in [-1.15,1.15]:cyl('Steel wheel',(x,yy,.38),.36,.18,'Steel','X')
 if not coach:
  mesh('Swept cab windshield',[(-.72,-9.9,2.15),(.72,-9.9,2.15),(.85,-8.1,2.49),(-.85,-8.1,2.49)],[(0,1,2,3)],'Glass')
  for x in [-.5,.5]:box('Nose lamp',(x,-10.4,1.05),(.32,.13,.19),'Lamp',.06)
  # No service-name text: the navy belt is the non-text identifier.
train('HighSpeedCab');train('HighSpeedCoach',True)
# Distinct Korean response vehicles; no licensed manufacturers' designs/logos.
def vehicle(name,ambulance=False):
 collection(name);length=6.2 if ambulance else 8.2;white='AmbulanceWhite' if ambulance else 'Red'
 box('Chassis',(0,0,.55),(2.35,length,.45),'Rubber',.09)
 box('Equipment body',(0,.8,1.65),(2.4,length-2.3,2.05),white,.15)
 # Mesh cab with a slanted windshield and roof; eight-point wedge, not a cube silhouette.
 front=-length/2;rear=front+2.3
 verts=[(-1.13,front,.8),(1.13,front,.8),(1.13,rear,.8),(-1.13,rear,.8),(-1.05,front+.35,2.45),(1.05,front+.35,2.45),(1.05,rear,2.65),(-1.05,rear,2.65)]
 cab=mesh('Sloped crew cab',verts,[(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)],white)
 mod=cab.modifiers.new('Cab soft corners','BEVEL');mod.width=.09;mod.segments=3;cab.modifiers.new('Cab normals','WEIGHTED_NORMAL')
 mesh('Windscreen',[(-.94,front+.10,1.55),(.94,front+.10,1.55),(.9,front+.285,2.28),(-.9,front+.285,2.28)],[(0,1,2,3)],'Glass')
 for side in [-1,1]:
  box('Cab side window',(side*1.09,front+1.25,2),(.035,1.15,.55),'Glass',.045)
  box('Cab handle',(side*1.15,front+1.6,1.45),(.05,.28,.05),'Steel',.01)
  beam('Mirror arm',(side*1.08,front+.45,1.92),(side*1.46,front+.4,1.92),.035,'Steel');box('Wing mirror',(side*1.46,front+.4,1.85),(.16,.14,.4),'Rubber')
  box('Safety stripe',(side*1.22,.75,1.25),(.035,length-2.4,.16),'Orange' if ambulance else 'Reflective',.005)
  if not ambulance:
   for y in [-.5,.75,2.1]:
    box('Roller equipment shutter',(side*1.23,y,1.8),(.04,1.12,1.1),'Steel',.01)
    for z in [1.35,1.5,1.65,1.8,1.95,2.1,2.25]:box('Shutter rib',(side*1.26,y,z),(.025,1.08,.025),'Roof',.002)
    box('Shutter latch',(side*1.29,y,1.45),(.04,.34,.05),'Rubber')
  else:
   box('Patient window',(side*1.23,1.1,2.03),(.045,1.8,.45),'Glass',.055)
   box('Medical cross vertical',(side*1.265,2,1.55),(.03,.16,.65),'Red',.002);box('Medical cross horizontal',(side*1.27,2,1.55),(.035,.65,.16),'Red',.002)
  # No numeral markings: safety stripes and geometric medical cross remain.
 for y in [front+1.05,length/2-1.15]:
  for x in [-1.18,1.18]:
   cyl('Tire',(x,y,.61),.52,.26,'Rubber','X',24);cyl('Wheel rim',(x*1.13,y,.61),.29,.035,'Steel','X',20)
 box('Front bumper',(0,front-.08,.73),(2.4,.2,.27),'Steel',.05);box('Radiator grille',(0,front-.02,1.18),(.9,.08,.42),'Rubber')
 for x in [-.84,.84]:box('Headlamp',(x,front-.06,1.2),(.38,.12,.28),'Lamp',.04)
 box('Lightbar housing',(0,front+1.3,2.72),(1.9,.4,.12),'Roof')
 for x in [-.65,.65]:box('Emergency beacon',(x,front+1.3,2.83),(.52,.37,.18),'Red',.055)
 if not ambulance:
  for x in [-.64,.64]:beam('Roof ladder rail',(x,-.5,2.85),(x,3.4,2.85),.045,'Steel')
  for y in [i*.35-.4 for i in range(11)]:beam('Roof ladder rung',(-.64,y,2.85),(.64,y,2.85),.035,'Steel')
  cyl('Hose reel',(0,2,3),.45,.85,'Red','X');beam('Water monitor',(0,.3,2.8),(0,-.2,3.3),.085,'Steel')
vehicle('FireEngine119');vehicle('Ambulance119',True)
# Crew source retains the public rig and clips; mesh/head proportion changes are below after rig inspection.
# Exports use deterministic material names for the Unity URP remapping.
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
 EXPORT_MANIFEST.append({'name':name,'fbx':'Assets/ChooGuard/Art/ResponseSet/'+name+'.fbx','units':'authored metres','boundsBlenderRestMesh':{'min':low,'max':high},'dimensionsMetres':{'width':high[0]-low[0],'depth':high[1]-low[1],'height':high[2]-low[2]},'grounded':abs(low[2])<.0001,'horizontalOrigin':'OSM origin retained' if name=='BusanFacade' else 'authored model origin','materials':sorted({m.name for o in objects if o.type=='MESH' for m in o.data.materials}),'fontObjects':sum(o.type=='FONT' for o in objects),'textMeshes':0,'facadeDesign':FACADE_DESIGN if name=='BusanFacade' else None})
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
 uniform='PPE' if role=='fire' else 'Medic' if role=='medical' else 'Uniform'
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
  loft('Continuous trousers',[((x,.025,.187),.037,.044),((x,.023,.155),.033,.036),((x,.02,.109),.029,.033),((x,.017,.07),.026,.028),((x,.014,.027),.024,.026)],bone,weights=[0,.7,1,1,1])
  ellipsoid('Fitted boot',(x,-.009,.023),(.028,.049,.022),'Rubber',bone)
  if role=='fire':limb('Ankle reflective band',(x,.017,.065),(x,.017,.076),.028,'Reflective',bone)
  bone='arm-left' if side>0 else 'arm-right'
  loft('Continuous jacket sleeve',[((side*.062,.019,.285),.033,.043),((side*.105,.019,.285),.031,.035),((side*.165,.018,.279),.027,.03),((side*.213,.015,.272),.023,.027),((side*.255,.014,.266),.021,.022)],bone,'X',[0,.5,1,1,1])
  ellipsoid('Fitted glove',(side*.268,.013,.264),(.018,.021,.019),'Rubber' if role=='fire' else 'Skin',bone)
  if role=='fire':limb('Sleeve reflective band',(side*.208,.016,.273),(side*.22,.016,.27),.025,'Reflective',bone)
 ellipsoid('Neck',(0,.015,.327),(.023,.025,.032),'Skin','head')
 ellipsoid('Adult head',(0,.006,.365),(.035,.038,.034),'Skin','head')
 ellipsoid('Nose',(0,-.034,.361),(.008,.012,.012),'Skin','head')
 if role=='fire':
  ellipsoid('Safety helmet',(0,.006,.397),(.047,.05,.020),'Reflective','head')
  o=box('Helmet brim',(0,-.008,.385),(.10,.115,.006),'Reflective',.006);bind(o,'head')
  o=box('Face shield',(0,-.039,.366),(.077,.008,.033),'Glass',.004);bind(o,'head')
  for x in [-.04,.04]:
   o=cyl('Breathing air cylinder',(x,.09,.249),.02,.115,'Steel');bind(o,'torso')
  for z in [.213,.27]:
   o=box('Jacket reflective strip',(0,-.039,z),(.157,.006,.012),'Reflective',.002);bind(o,'torso')
 else:
  ellipsoid('Hair',(0,.013,.388),(.038,.038,.015),'Navy','head')
  if role=='medical':
   o=box('Medical shoulder panel',(0,-.038,.28),(.12,.006,.025),'Orange',.003);bind(o,'torso')
   o=box('Carried hip medical pouch',(-.108,.005,.194),(.043,.065,.065),'Orange',.009);bind(o,'torso')
   limb('Medical bag shoulder strap',(-.06,-.04,.295),(-.105,-.035,.205),.003,'Navy','torso')
   o=box('Medical vest cross',(-.02,-.05,.26),(.007,.003,.025),'Pearl',.001);bind(o,'torso')
   o=box('Medical vest cross',(-.02,-.051,.26),(.025,.003,.007),'Pearl',.001);bind(o,'torso')
  else:
   o=box('Operations vest',(0,-.04,.247),(.13,.005,.09),'Reflective',.006);bind(o,'torso')
   o=box('Radio',(side*.05,-.055,.28),(.02,.015,.032),'Rubber',.002);bind(o,'torso')
 top.scale.x*=2.4;top.scale.y*=2.4;top.scale.z*=3.8
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
for name,col in list(assets.items()):export_asset(name,col)
crew('CrewOperations','operations');crew('CrewFire','fire');crew('CrewMedical','medical')

assert len(EXPORT_MANIFEST)==8 and all(row['grounded'] and row['fontObjects']==0 for row in EXPORT_MANIFEST)
for row in EXPORT_MANIFEST:
 path=ROOT/row['fbx'];row['bytes']=path.stat().st_size;row['sha256']=hashlib.sha256(path.read_bytes()).hexdigest()
manifest={'spatialContract':str(SPATIAL_PATH.relative_to(ROOT)),'scriptSha256':SCRIPT_SHA,'textPolicy':'No Blender FONT or text-generated mesh objects','models':EXPORT_MANIFEST,'precision':'Facade horizontal frontage is OSM source; heights and human-scale details are design, not surveyed measurements. Crew bounds are rest-mesh bounds; existing NLA clips retained.'}
(SOURCE/'response-set-metre-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n');(OUT/'metre-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
# Save geometry before arranging a single render, as requested by the latest contract.
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'response_set.blend'))
# Single metre-scale proof view: people and door share the same physical coordinate scale.
entry,outward,tangent=min(ENTRANCES,key=lambda e:abs(e[0].y))
for name,col in assets.items():
 if name=='BusanFacade':continue
 if not name.startswith('Crew'):
  for obj in list(col.all_objects):obj.hide_render=True
  continue
 index=['CrewOperations','CrewFire','CrewMedical'].index(name);objects=list(col.all_objects)
 rig=next(o for o in objects if o.type=='ARMATURE');set_motion(rig,next(a for a in bpy.data.actions if '|idle|' in a.name));bpy.context.scene.frame_set(1)
 parent=bpy.data.objects.new(name+' metre preview',None);bpy.context.scene.collection.objects.link(parent)
 for obj in objects:
  if obj.parent not in objects:matrix=obj.matrix_world.copy();obj.parent=parent;obj.matrix_world=matrix
 row=next(row for row in EXPORT_MANIFEST if row['name']==name);scale=SPATIAL['designNotMeasured']['crewHeight']/row['dimensionsMetres']['height'];parent.scale=(scale,)*3
 parent.location=entry+outward*2+tangent*((index-1)*1.4)+Vector((0,0,.3));parent.rotation_euler[2]=math.atan2(outward.y,outward.x)+math.pi/2
collection('PreviewOnly');box('Metre plaza',(entry.x,entry.y,-.10),(65,65,.2),'Concrete',.02)
world=bpy.context.scene.world or bpy.data.worlds.new('World');bpy.context.scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.2,.25,.3,1);world.node_tree.nodes['Background'].inputs[1].default_value=.6
bpy.ops.object.light_add(type='AREA',location=entry+outward*12+Vector((0,0,25)));light=bpy.context.object;light.data.energy=4200;light.data.shape='DISK';light.data.size=20
bpy.ops.object.light_add(type='SUN');bpy.context.object.data.energy=2;bpy.context.object.rotation_euler=(.4,-.5,-.5)
bpy.ops.object.camera_add(location=entry+outward*24+tangent*12+Vector((0,0,11)));cam=bpy.context.object;cam.rotation_euler=(entry+Vector((0,0,7.8))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=32;bpy.context.scene.camera=cam
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=2048;scene.render.resolution_y=1536;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=str(SOURCE/'response-set-metre-preview.png');bpy.ops.render.render(write_still=True)
(OUT/'materials.json').write_text(json.dumps({n:{'color':list(m.diffuse_color),'metallic':m.node_tree.nodes.get('Principled BSDF').inputs['Metallic'].default_value,'roughness':m.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value} for n,m in M.items()},indent=2))
(SOURCE/'build-receipt.json').write_text(json.dumps({'parameters':PACK,'spatialContract':SPATIAL,'scriptSha256':SCRIPT_SHA,'metreManifest':'art/blender/response-set-metre-manifest.json','renderCount':1,'appliedOperations':APPLIED,'executionMode':'headless scripted Blender; no desktop input','blender':bpy.app.version_string,'exports':[n for n in assets if n!='PreviewOnly'],'authorship':'Astra scripted mesh/code; Jev constrained parameters only, no image inspection claim','styleLimit':'stylized RTS hero set, not a licensed vehicle replica or surveyed station CAD','reference':'OSM footprint + official rail station photograph; authored approximations, not engineering CAD','materialSources':['ambientCG Concrete031 CC0','ambientCG Metal032 CC0'],'preview':str(SOURCE/'response-set-metre-preview.png')},indent=2))
