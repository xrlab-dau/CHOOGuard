"""Deterministic Jev019-directed coastal Busan world kit. Headless Blender only."""
import bpy,math,random,json,pathlib,hashlib,shutil,sys
from mathutils import Vector,Matrix
ROOT=pathlib.Path(__file__).resolve().parents[2]
if '--bus-stop-only' in sys.argv:
 # Latest UI-only text constraint: patch only one exported asset, with no rerender.
 out=ROOT/'Assets/ChooGuard/Art/WorldSet';art=ROOT/'art/blender';manifest=json.loads((art/'world-set-manifest.json').read_text());before={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in out.glob('*.fbx') if p.name!='BusStop.fbx'}
 bpy.ops.wm.open_mainfile(filepath=str(art/'world_set.blend'));col=bpy.data.collections['BusStop'];objects=list(col.all_objects);floor=next(o for o in objects if any(m.name=='WS_Limestone' for m in o.data.materials));shift=floor.matrix_world.translation-Vector((0,0,.06))
 removed=[]
 for obj in objects:
  if any(m.name=='WS_White' for m in obj.data.materials):removed.append(obj.name);bpy.data.objects.remove(obj,do_unlink=True)
 def patch_box(name,loc,size,material,bevel=.01):
  bpy.ops.mesh.primitive_cube_add(size=1,location=Vector(loc)+shift);obj=bpy.context.object;obj.name=name;obj.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
  for old in list(obj.users_collection):old.objects.unlink(obj)
  col.objects.link(obj);obj.data.materials.append(bpy.data.materials[material]);mod=obj.modifiers.new('Edge radius','BEVEL');mod.width=bevel;mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name);return obj
 patch_box('Route map backing',(1.87,.515,1.60),(.50,.015,1.16),'WS_White',.015)
 patch_box('Nontext bus pictogram',(0,-1.09,2.94),(.34,.02,.16),'WS_White',.025)
 for x in [-.085,.085]:patch_box('Bus pictogram glazing',(x,-1.108,2.975),(.12,.01,.055),'WS_TransitBlue',.006)
 for x in [-.115,.115]:
  bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=.025,depth=.014,location=Vector((x,-1.102,2.85))+shift);obj=bpy.context.object;obj.name='Bus pictogram wheel';obj.rotation_euler[0]=math.pi/2
  for old in list(obj.users_collection):old.objects.unlink(obj)
  col.objects.link(obj);obj.data.materials.append(bpy.data.materials['WS_White'])
 groups={}
 for obj in list(col.all_objects):groups.setdefault(tuple(m.name for m in obj.data.materials),[]).append(obj)
 for group in groups.values():
  bpy.ops.object.select_all(action='DESELECT')
  for obj in group:obj.select_set(True)
  bpy.context.view_layer.objects.active=group[0]
  if len(group)>1:bpy.ops.object.join()
  bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 bpy.context.view_layer.update();objects=list(col.all_objects);vertices=[o.matrix_world@v.co for o in objects for v in o.data.vertices];lo=[min(v[k] for v in vertices) for k in range(3)];hi=[max(v[k] for v in vertices) for k in range(3)]
 bpy.ops.object.select_all(action='DESELECT')
 for obj in objects:obj.select_set(True)
 temp=art/'world-set-BusStop-patch.fbx';bpy.ops.export_scene.fbx(filepath=str(temp),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='AUTO');dest=out/'BusStop.fbx';temp.replace(dest)
 row=next(r for r in manifest['models'] if r['name']=='BusStop');row.update(bytes=dest.stat().st_size,sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),localBoundsBlender={'min':lo,'max':hi},intendedMeters={'width':hi[0]-lo[0],'depth':hi[1]-lo[1],'height':hi[2]-lo[2]},meshCount=len(objects),triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),vertices=sum(len(o.data.vertices) for o in objects),textMeshes=0,fontObjects=0,precision='Generic Korean shelter; baked lettering removed and replaced by geometric bus pictogram under latest UI-only text rule.')
 after={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in out.glob('*.fbx') if p.name!='BusStop.fbx'};assert before==after
 patch={'authority':'root-authorized latest UI-only world text constraint','mode':'BusStop-only FBX export; no render, no other8 FBX or textures modified','removedMeshes':removed,'replacement':'geometric bus silhouette/windows/wheels plus plain route backing','otherEightHashesUnchanged':True,'scriptSha256':hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),'sourceBlend':'world_set.blend remains prior frozen source snapshot; generator updated for future builds'};manifest['busStopTextPatch']=patch
 (art/'world-set-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n');(out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n');receipt=json.loads((art/'world-set-build-receipt.json').read_text());receipt['busStopTextPatch']=patch;receipt['totalMeshes']=sum(r['meshCount'] for r in manifest['models']);receipt['totalTriangles']=sum(r['triangles'] for r in manifest['models']);(art/'world-set-build-receipt.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n');(art/'world-set-busstop-text-removal-receipt.json').write_text(json.dumps({'patch':patch,'model':row},ensure_ascii=False,indent=2)+'\n');sys.exit(0)
PACK_PATH=ROOT/'.planning/2026-09-20-integrated-build/world-art-design-packet.json'
PARAMETER_BYTES=PACK_PATH.read_bytes();PACK=json.loads(PARAMETER_BYTES);PARAMETER_SHA=hashlib.sha256(PARAMETER_BYTES).hexdigest();SCRIPT_SHA=hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest()
for key,allowed in {'palette':['warm_limestone_coastal'],'lighting':['clear_coastal_afternoon'],'density':['district_batched']}.items():
 if PACK.get(key) not in allowed:raise ValueError('Unsupported Jev019 choice: '+key)
PALETTE={'warm_limestone_coastal':{'stone':(.65,.61,.51),'metal':(.19,.24,.25),'glass':(.13,.24,.29),'wood':(.31,.17,.078),'roof':(.72,.74,.69),'leaves':[(.12,.24,.07),(.20,.32,.10),(.28,.38,.14)]}}[PACK['palette']]
LIGHT={'clear_coastal_afternoon':{'sunEnergy':2.3,'sunRotation':(.46,-.42,-.7),'worldEnergy':.6,'color':(.72,.82,.94)}}[PACK['lighting']]
BUDGET={'district_batched':{'mergeByMaterial':True,'branchSides':9,'foliageSubdivisions':2,'pineFoliageSubdivisions':1,'bevelSegments':2,'architecturalSides':48}}[PACK['density']]
RNG=random.Random(190921)
OUT=ROOT/'Assets/ChooGuard/Art/WorldSet';ART=ROOT/'art/blender';OUT.mkdir(parents=True,exist_ok=True);(OUT/'Textures').mkdir(exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):bpy.data.collections.remove(c)
M={};META={};COL=None;ASSETS={};MODEL_NOTES={}
def mat(name,color,rough=.6,metal=0,texture=None):
 material=bpy.data.materials.new(name);material.use_nodes=True;material.diffuse_color=(*color,1);p=material.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
 record={'name':name,'baseColor':list(color)+[1],'roughness':rough,'metallic':metal,'textures':{}}
 if texture:
  src=ROOT/f'asset-library/models-materials/extracted/{texture}_1K-JPG'
  for channel,suffix in [('baseColor','Color'),('roughness','Roughness'),('normalGL','NormalGL')]:
   source=src/f'{texture}_1K-JPG_{suffix}.jpg';dest=OUT/'Textures'/source.name
   if source.exists():shutil.copy2(source,dest);record['textures'][channel]=str(dest.relative_to(ROOT))
  if 'baseColor' in record['textures']:
   image=bpy.data.images.load(str(ROOT/record['textures']['baseColor']),check_existing=True);node=material.node_tree.nodes.new('ShaderNodeTexImage');node.image=image;tint=material.node_tree.nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[2].default_value=(*color,1);material.node_tree.links.new(node.outputs['Color'],tint.inputs[1]);material.node_tree.links.new(tint.outputs[0],p.inputs['Base Color']);record['textureTint']=list(color);coord=material.node_tree.nodes.new('ShaderNodeTexCoord');material.node_tree.links.new(coord.outputs['Generated'],node.inputs['Vector'])
 M[name]=material;META[name]=record;return material
mat('WS_Limestone',PALETTE['stone'],.83,texture='Concrete031');mat('WS_PaintedMetal',PALETTE['metal'],.43,.55);mat('WS_Stainless',(.52,.57,.58),.28,.78,texture='Metal032');mat('WS_Glass',PALETTE['glass'],.18,.45);mat('WS_Roof',PALETTE['roof'],.38,.4);mat('WS_Wood',PALETTE['wood'],.76);mat('WS_Bark',(.19,.14,.085),.94);mat('WS_Earth',(.14,.11,.075),.97);mat('WS_White',(.84,.83,.77),.52);mat('WS_TransitBlue',(.075,.21,.32),.43,.2);mat('WS_AwningRed',(.47,.13,.10),.73);mat('WS_Lamp',(.88,.83,.60),.27)
for i,color in enumerate(PALETTE['leaves']):mat('WS_Leaf'+str(i),color,.9)
def asset(name,note):
 global COL
 COL=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(COL);ASSETS[name]=COL;MODEL_NOTES[name]=note
 return COL
def attach(obj,name,material):
 obj.name=name
 for collection in list(obj.users_collection):collection.objects.unlink(obj)
 COL.objects.link(obj)
 if material:obj.data.materials.append(M[material])
 return obj
def box(name,loc,size,material,bevel=.04):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=attach(bpy.context.object,name,material);o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  m=o.modifiers.new('Edge radius','BEVEL');m.width=bevel;m.segments=BUDGET['bevelSegments'];o.modifiers.new('Architectural normals','WEIGHTED_NORMAL')
 return o
def cylinder(name,loc,radius,depth,material,vertices=16,top=None):
 bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=radius,radius2=radius if top is None else top,depth=depth,location=loc);o=attach(bpy.context.object,name,material)
 for p in o.data.polygons:p.use_smooth=True
 return o
def branch(name,a,b,r1,r2,material='WS_Bark'):
 a,b=Vector(a),Vector(b);o=cylinder(name,(a+b)/2,r1,(b-a).length,material,BUDGET['branchSides'],r2);o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o
def mesh(name,vertices,faces,material):
 data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.update();o=bpy.data.objects.new(name,data);COL.objects.link(o);data.materials.append(M[material]);return o
def foliage(loc,size,index=0):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=BUDGET['pineFoliageSubdivisions'] if COL.name=='TreePine' else BUDGET['foliageSubdivisions'],radius=1,location=loc);o=attach(bpy.context.object,'Varied leaf crown','WS_Leaf'+str(index));o.data.materials.append(M['WS_Leaf'+str((index+1)%3)])
 for v in o.data.vertices:
  direction=v.co.normalized();v.co*=RNG.uniform(.78,1.18);v.co.x*=size[0];v.co.y*=size[1];v.co.z*=size[2]
 for p in o.data.polygons:p.use_smooth=True;p.material_index=1 if RNG.random()<.24 else 0
 return o
def ring(name,z,radius,thickness,material,segments=48):
 vertices=[]
 for r in [radius-thickness,radius]:
  for i in range(segments):vertices.append((math.cos(i*math.tau/segments)*r,math.sin(i*math.tau/segments)*r,z))
 faces=[(i,(i+1)%segments,(i+1)%segments+segments,i+segments) for i in range(segments)];return mesh(name,vertices,faces,material)
def bus_pictogram():
 box('Nontext bus pictogram',(0,-1.09,2.94),(.34,.02,.16),'WS_White',.025)
 for x in [-.085,.085]:box('Bus pictogram glazing',(x,-1.108,2.975),(.12,.01,.055),'WS_TransitBlue',.006)
 for x in [-.115,.115]:
  wheel=cylinder('Bus pictogram wheel',(x,-1.102,2.85),.025,.014,'WS_White',12);wheel.rotation_euler[0]=math.pi/2
# Branched, non-identical crowns; restricted scatter is owned by the world-surface lane.
asset('TreeBroadleaf','Authored coastal urban broadleaf; not a botanically identified specimen. No placement claim.')
trunk=[(0,0,0),(.08,.02,1.9),(.18,-.04,3.7),(.35,.04,5.4)]
for i in range(3):branch('Tapered trunk',trunk[i],trunk[i+1],.29-i*.055,.235-i*.055)
for k in range(8):
 angle=k*math.tau/8+RNG.uniform(-.24,.24);origin=Vector((.12,0,2.5+k*.29));end=origin+Vector((math.cos(angle)*RNG.uniform(1.1,2.1),math.sin(angle)*RNG.uniform(1.1,2.1),RNG.uniform(1.4,2.4)));branch('Primary limb',origin,end,.11,.027)
 for j in range(3):
  tip=end+Vector((RNG.uniform(-.8,.8),RNG.uniform(-.8,.8),RNG.uniform(.0,.65)));branch('Fine branch',end,tip,.035,.012);foliage(tip,(RNG.uniform(.75,1.25),RNG.uniform(.8,1.3),RNG.uniform(.6,1)),k%3)
foliage((.2,0,6.25),(1.1,1.2,.9),2)
asset('TreePine','Authored irregular pine-like canopy with lateral branches; not a uniform conical forest asset.')
for a,b,r1,r2 in [((0,0,0),(.15,.08,2.6),.26,.2),((.15,.08,2.6),(.46,.04,5.2),.2,.12),((.46,.04,5.2),(.24,.16,7.6),.12,.035)]:branch('Bent pine trunk',a,b,r1,r2)
for tier in range(5):
 for k in range(4):
  angle=k*math.pi/2+tier*.68;z=3.0+tier*.91;length=2.0-tier*.22;end=Vector((.25+math.cos(angle)*length,math.sin(angle)*length,z+.25));branch('Pine limb',(.2,0,z),end,.09-tier*.01,.021)
  for j in range(3):foliage(end+Vector((RNG.uniform(-.45,.45),RNG.uniform(-.45,.45),RNG.uniform(-.08,.15))),(length*.46,.66,.35+tier*.015),(tier+k)%2)
foliage((.25,.08,7.65),(.65,.65,.55),1)
asset('StreetLamp','Authored Korean urban LED streetlight approximation, 8m pole. No exact municipal product claim.')
cylinder('Bolted foundation',(0,0,.08),.36,.16,'WS_Limestone',16);cylinder('Pole base',(0,0,.3),.16,.45,'WS_PaintedMetal',12);cylinder('Tapered pole',(0,0,4.2),.095,7.8,'WS_PaintedMetal',12,.065)
for x in [-.17,.17]:
 for y in [-.17,.17]:cylinder('Anchor bolt',(x,y,.19),.026,.07,'WS_Stainless',8)
branch('Swept lamp arm',(0,0,7.8),(0,-1.8,8.05),.07,.045,'WS_PaintedMetal');box('LED housing',(0,-2.05,8.06),(.42,1.05,.14),'WS_Stainless',.06);box('Diffuser',(0,-2.08,7.98),(.32,.83,.025),'WS_Lamp',.02)
asset('Bench','Authored timber-and-steel civic bench, 1.9m seat.')
for x in [-.68,.68]:
 for y in [-.24,.23]:box('Steel leg',(x,y,.24),(.085,.10,.48),'WS_PaintedMetal',.015)
 branch('Seat frame',(x,-.31,.45),(x,.30,.45),.055,.055,'WS_PaintedMetal');branch('Back support',(x,.22,.38),(x,.37,.97),.04,.04,'WS_PaintedMetal')
for y in [-.25,-.12,.01,.14,.27]:box('Seat slat',(0,y,.51),(1.9,.105,.065),'WS_Wood',.013)
for z in [.70,.83,.96]:box('Back slat',(0,.32+(z-.7)*.18,z),(1.9,.075,.09),'WS_Wood',.012)
for x in [-.85,.85]:branch('Armrest',(x,-.22,.72),(x,.32,.72),.033,.033,'WS_Stainless');branch('Armrest support',(x,-.15,.50),(x,-.15,.72),.03,.03,'WS_Stainless')
asset('BusStop','Authored Korean street shelter: roof, glazing, accessible opening, route panel and bench. Generic service branding.')
box('Shelter footing',(0,0,.06),(5.4,2,.12),'WS_Limestone',.025)
for x in [-2.35,2.35]:
 for y in [.68,-.55]:box('Shelter column',(x,y,1.45),(.10,.10,2.8),'WS_PaintedMetal',.015)
for x in [-1.6,0,1.6]:
 box('Back glazing',(x,.72,1.4),(1.49,.04,2.32),'WS_Glass',.01);box('Window lower rail',(x,.70,.52),(1.54,.075,.05),'WS_Stainless',.008)
for x in [-2.35,2.35]:box('Side glazing',(x,.2,1.4),(.04,.98,2.32),'WS_Glass',.008)
verts=[]
for x in [-2.75,2.75]:
 for j in range(9):y=-1.0+j*.25;verts.append((x,y,2.93+.16*math.cos((y+.15)*math.pi/2)))
roof=mesh('Gently curved shelter roof',verts,[(j,9+j,10+j,j+1) for j in range(8)],'WS_Stainless');mod=roof.modifiers.new('Roof thickness','SOLIDIFY');mod.thickness=.075
box('Blue station fascia',(0,-1.03,2.93),(5.5,.09,.22),'WS_TransitBlue',.025);bus_pictogram()
box('Route display',(1.87,.60,1.58),(.62,.14,1.38),'WS_PaintedMetal',.045);box('Route map glass',(1.87,.515,1.60),(.50,.015,1.16),'WS_White',.015)
for z in [1.2,1.42,1.64,1.86,2.08]:box('Route diagram',(1.86,.5,z),(.37,.01,.016),'WS_TransitBlue',.002)
for x in [-1.25,.1]:box('Bench leg',(x,.18,.28),(.08,.30,.48),'WS_PaintedMetal',.015)
for y in [-.03,.09,.21,.33]:box('Shelter bench',(-.6,y,.55),(2.2,.10,.06),'WS_Wood',.012)
asset('Planter','Authored limestone planter with coping, visible soil and low foliage.')
for x in [-.96,.96]:box('Planter short wall',(x,0,.39),(.13,1.22,.78),'WS_Limestone',.04)
for y in [-.55,.55]:box('Planter long wall',(0,y,.39),(1.92,.13,.78),'WS_Limestone',.04)
box('Coping',(0,0,.80),(2.15,1.35,.12),'WS_Limestone',.045);box('Inset soil',(0,0,.87),(1.86,1.06,.035),'WS_Earth',.012)
for k in range(9):foliage((RNG.uniform(-.75,.75),RNG.uniform(-.35,.35),.94),(.35,.28,.23),k%3)
# Landmarks: recognizable broad silhouettes; only tower120m is source-confirmed dimension.
asset('BusanTower','Official Busan city page confirms120m tower/observatory. Shaft/deck/base proportions authored approximations; linked photo download returned401. Not surveyed CAD.')
for z,r,h in [(1,11,2),(2.8,8.6,1.6),(4.0,6.8,.8)]:cylinder('Circular podium',(0,0,z),r,h,'WS_Limestone',48)
cylinder('Slender tapering shaft',(0,0,47.5),3.3,85,'WS_White',BUDGET['architecturalSides'],2.55)
for k in range(8):
 angle=k*math.tau/8;branch('Shaft seam',(math.cos(angle)*3.3,math.sin(angle)*3.3,5),(math.cos(angle)*2.6,math.sin(angle)*2.6,90),.07,.055,'WS_Limestone')
cylinder('Observation flare',(0,0,94),3.0,8,'WS_White',48,7.8);cylinder('Observation lower deck',(0,0,98.5),8.3,1.3,'WS_Limestone',48)
cylinder('Panoramic observation glazing',(0,0,102.2),7.8,6,'WS_Glass',48)
for k in range(24):
 angle=k*math.tau/24;branch('Observation mullion',(math.cos(angle)*7.9,math.sin(angle)*7.9,99),(math.cos(angle)*7.9,math.sin(angle)*7.9,105.2),.11,.11,'WS_Stainless')
cylinder('Observation rim',(0,0,105.6),8.9,.7,'WS_White',48);cylinder('Pagoda inspired crown',(0,0,108.1),9.6,4.2,'WS_Roof',48,4.3);cylinder('Crown upper lantern',(0,0,112.2),3.2,4,'WS_White',32);cylinder('Crown roof',(0,0,115),4.3,1.8,'WS_Roof',32,1.2);cylinder('Spire',(0,0,117.6),.68,4.8,'WS_White',16,.12)
for y in [-5.5,5.5]:box('Podium entrance',(0,y,3.8),(3,.12,2.4),'WS_Glass',.1)
asset('PortTerminal','Horizontal coastal terminal, terraced glazing and curved roof vocabulary from root reference brief; detailed actual terminal profile/dimensions not verified. Entire mass is an authored geographic-fit proxy.')
for floor in range(4):
 z=1+floor*5.4;box('Horizontal floor plate',(0,0,z),(132-floor*2,58,.65),'WS_Limestone',.28)
 if floor<3:
  for y in [-28.7,28.7]:box('Curtain glazing',(0,y,z+2.65),(129-floor*2,.12,4.65),'WS_Glass',.05)
  for x in range(-62,63,8):
   for y in [-29,29]:box('Facade mullion',(x,y,z+2.7),(.18,.18,4.8),'WS_Stainless',.02)
  for x in [-65+floor,65-floor]:box('Side glazing',(x,0,z+2.65),(.12,56,4.65),'WS_Glass',.05)
for x in range(-60,61,12):
 box('Ground pier',(x,-29,4),(.75,1.1,7),'WS_Limestone',.12)
 box('Entrance portal',(x,-29.8,3.1),(5,.2,4.7),'WS_Glass',.08)
verts=[]
for i in range(41):
 x=-69+i*138/40
 for y in [-32,32]:verts.append((x,y,22+6*math.cos(x/69*math.pi/2)))
roof=mesh('Long curved terminal roof',verts,[(2*i,2*i+2,2*i+3,2*i+1) for i in range(40)],'WS_Roof');solid=roof.modifiers.new('Roof layered thickness','SOLIDIFY');solid.thickness=.6
for x in range(-66,67,6):
 z=22+6*math.cos(x/69*math.pi/2);branch('Roof transverse ribs',(x,-32,z+.1),(x,32,z+.1),.15,.15,'WS_Stainless')
box('Arrival canopy',(0,-34,7.8),(92,13,.45),'WS_Roof',.18)
for x in range(-42,43,14):branch('Arrival canopy column',(x,-39,0),(x,-39,7.6),.28,.23,'WS_Stainless')
box('Pedestrian connection',(0,40,8),(18,27,.65),'WS_Limestone',.18)
for y in [31,43]:
 for x in [-7.5,7.5]:branch('Connection support',(x,y,0),(x,y,7.7),.4,.35,'WS_Limestone')
for y in [33,39,45,51]:
 for x in [-8.7,8.7]:box('Connection balustrade',(x,y,9.1),(.1,5.8,1.6),'WS_Glass',.04)
asset('JagalchiMarket','OSM building382696296 root evidence:151x48m planar bounding envelope,6levels,centroid35.0966,129.030656. Model fitted to that envelope. Seagull roof/facade proportions remain authored approximations; operator-page fetch timed out.')
for level in range(6):
 z=.8+level*4.5;box('Market floor band',(0,0,z),(100,37,.5),'WS_Limestone',.16)
 for y in [-18.2,18.2]:box('Horizontal market glazing',(0,y,z+2.2),(98,.14,3.8),'WS_Glass',.03)
 for x in range(-48,49,6):
  for y in [-18.45,18.45]:box('Market facade frame',(x,y,z+2.1),(.15,.16,4.1),'WS_Stainless',.01)
for x in [-49,49]:box('Market stone end',(x,0,13.6),(1.6,36,27.2),'WS_Limestone',.12)
verts=[]
for i in range(49):
 x=-55+i*110/48;wing=29+11*(abs(x)/55)**.72
 for j in range(7):y=-21+j*7;verts.append((x,y,wing+1.5*math.cos(y/21*math.pi/2)))
faces=[]
for i in range(48):
 for j in range(6):a=i*7+j;faces.append((a,a+7,a+8,a+1))
roof=mesh('Twin rising seagull roof wings',verts,faces,'WS_Roof');solid=roof.modifiers.new('Curved roof edge thickness','SOLIDIFY');solid.thickness=.48
for x in range(-54,55,6):
 z=29+11*(abs(x)/55)**.72;branch('Wing rib',(x,-21,z+.12),(x,21,z+.12),.17,.17,'WS_Stainless')
for x in range(-48,49,8):
 top=29+11*(abs(x)/55)**.72
 for y in [-18.0,18.0]:branch('Upper roof support',(x,y,27.0),(x,y,top),.17,.14,'WS_Stainless')
for x in range(-48,48,8):
 za=29+11*(abs(x)/55)**.72;zb=29+11*(abs(x+8)/55)**.72
 for y in [-18.1,18.1]:
  mesh('Wing clerestory glazing',[(x,y,27.0),(x+8,y,27.0),(x+8,y,zb),(x,y,za)],[(0,1,2,3)] if y<0 else [(3,2,1,0)],'WS_Glass')
  branch('Wing triangulation',(x,y,27.1),(x+8,y,zb-.2),.09,.09,'WS_Stainless')
for x in range(-44,45,8):
 box('Ground shop glazing',(x,-18.65,2.1),(6.5,.15,3.5),'WS_Glass',.05)
 canopy=box('Market awning',(x,-20.3,4.15),(7.1,3.1,.2),'WS_AwningRed' if (x//8)%2 else 'WS_TransitBlue',.04);canopy.rotation_euler[0]=.14
 for side in [-2.8,2.8]:branch('Awning support',(x+side,-21.7,.0),(x+side,-21.7,4),.055,.055,'WS_Stainless')
# Shared material grouping keeps each static model at a bounded renderer count.
def finish(name,col):
 objects=list(col.all_objects)
 if name=='JagalchiMarket':
  corners=[o.matrix_world@Vector(v) for o in objects for v in o.bound_box];dx=max(v.x for v in corners)-min(v.x for v in corners);dy=max(v.y for v in corners)-min(v.y for v in corners);fit=Matrix.Diagonal((151/dx,48/dy,1,1))
  for obj in objects:obj.matrix_world=fit@obj.matrix_world
 for obj in objects:
  if obj.type!='MESH':continue
  bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
  for modifier in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=modifier.name)
 corners=[o.matrix_world@v.co for o in objects if o.type=='MESH' for v in o.data.vertices];low=Vector([min(v[k] for v in corners) for k in range(3)]);high=Vector([max(v[k] for v in corners) for k in range(3)])
 offset=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))
 for obj in objects:obj.location-=offset
 groups={}
 for obj in objects:groups.setdefault(tuple(m.name for m in obj.data.materials),[]).append(obj)
 for group in groups.values():
  bpy.ops.object.select_all(action='DESELECT')
  for obj in group:obj.select_set(True)
  bpy.context.view_layer.objects.active=group[0]
  if len(group)>1:bpy.ops.object.join()
  bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 bpy.context.view_layer.update()
 # Ground exactly from geometry after material batching, not rotated object bounding boxes.
 final_objects=list(col.all_objects);minimum=min((o.matrix_world@v.co).z for o in final_objects for v in o.data.vertices)
 for o in final_objects:o.location.z-=minimum
 bpy.context.view_layer.update()
 bpy.ops.object.select_all(action='DESELECT')
 for obj in col.all_objects:obj.select_set(True)
 temp=ART/('world-set-'+name+'.fbx');bpy.ops.export_scene.fbx(filepath=str(temp),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='AUTO');dest=OUT/(name+'.fbx');temp.replace(dest)
 objects=list(col.all_objects);corners=[o.matrix_world@v.co for o in objects for v in o.data.vertices];lo=[min(v[k] for v in corners) for k in range(3)];hi=[max(v[k] for v in corners) for k in range(3)];dimensions=[hi[k]-lo[k] for k in range(3)]
 return {'name':name,'fbx':str(dest.relative_to(ROOT)),'bytes':dest.stat().st_size,'sha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'units':'meters','axes':{'source':'Blender X right,Y forward,Z up','fbx':'-Z forward,Y up; Unity bounds must be observed after import'},'groundPivot':True,'localBoundsBlender':{'min':lo,'max':hi},'intendedMeters':{'width':dimensions[0],'depth':dimensions[1],'height':dimensions[2]},'materials':sorted({m.name for o in objects for m in o.data.materials}),'meshCount':len(objects),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),'vertices':sum(len(o.data.vertices) for o in objects),'precision':MODEL_NOTES[name],'parameters':{'palette':PACK['palette'],'density':PACK['density'],'seed':190921}}
models=[finish(name,col) for name,col in ASSETS.items()]
assert [m['name'] for m in models]==PACK['exports']
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'world_set.blend'))
manifest={'schemaVersion':1,'parameterSource':str(PACK_PATH.relative_to(ROOT)),'jevRef':PACK['jevRef'],'sourceRef':PACK['sourceRef'],'roles':{'Jev':'advisory design/parameter choice','Astra low':'script and actual procedural Blender execution'},'blender':bpy.app.version_string,'units':'meters authored; consumer fits geographic source footprint without claiming measured geometry','appliedOperations':{'palette':PALETTE,'lighting':LIGHT,'budget':BUDGET,'seed':190921},'materials':list(META.values()),'models':models,'acceptance':'render review and Unity material/bounds validation remain separate from successful generation'}
(ART/'world-set-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n');(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
# Contact arrangement uses scaled previews; exported meter dimensions above stay unchanged.
placements={'TreeBroadleaf':((-15,-11,0),1.25),'TreePine':((-4,-11,0),1.15),'StreetLamp':((5,-11,0),1.15),'Bench':((10,-11,0),2.4),'BusStop':((17,-11,0),1.6),'Planter':((23,-11,0),1.8),'BusanTower':((-18,7,0),.16),'PortTerminal':((0,8,0),.18),'JagalchiMarket':((24,8,0),.15)}
for name,col in ASSETS.items():
 parent=bpy.data.objects.new(name+' preview',None);bpy.context.scene.collection.objects.link(parent)
 for obj in col.all_objects:
  matrix=obj.matrix_world.copy();obj.parent=parent;obj.matrix_world=matrix
 loc,scale=placements[name];parent.location=loc;parent.scale=(scale,)*3
asset('PreviewOnly','Not exported')
box('Studio',(3,0,-.22),(70,55,.4),'WS_Limestone',.05)
world=bpy.context.scene.world or bpy.data.worlds.new('World');bpy.context.scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(*LIGHT['color'],1);world.node_tree.nodes['Background'].inputs[1].default_value=LIGHT['worldEnergy']
bpy.ops.object.light_add(type='SUN');bpy.context.object.data.energy=LIGHT['sunEnergy'];bpy.context.object.rotation_euler=LIGHT['sunRotation']
bpy.ops.object.light_add(type='AREA',location=(-20,-25,35));bpy.context.object.data.energy=4000;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=25
bpy.ops.object.camera_add(location=(45,-62,45));camera=bpy.context.object;camera.rotation_euler=(Vector((3,0,7))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=72;scene=bpy.context.scene;scene.camera=camera;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=2048;scene.render.resolution_y=1536;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=str(ART/'world-set-contact.png');bpy.ops.render.render(write_still=True)
camera.location=(42,-39,28);camera.rotation_euler=(Vector((11,8,3))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=44;scene.render.filepath=str(ART/'world-set-landmark-hero.png');bpy.ops.render.render(write_still=True)
(ART/'world-set-build-receipt.json').write_text(json.dumps({'parameterSource':str(PACK_PATH.relative_to(ROOT)),'parameterSha256':PARAMETER_SHA,'scriptSha256':SCRIPT_SHA,'blender':bpy.app.version_string,'executed':'headless build, FBX export, Cycles contact and hero renders','appliedOperations':manifest['appliedOperations'],'exports':[m['fbx'] for m in models],'totalMeshes':sum(m['meshCount'] for m in models),'totalTriangles':sum(m['triangles'] for m in models),'manifest':'art/blender/world-set-manifest.json','renders':['art/blender/world-set-contact.png','art/blender/world-set-landmark-hero.png'],'scope':'stylized detailed RTS kit; production acceptance and Unity validation not claimed'},ensure_ascii=False,indent=2)+'\n')
