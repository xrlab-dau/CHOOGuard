"""Source-metre, evidence-constrained facade drafts. Never writes Assets or Unity scenes."""
import bpy, math, json, pathlib, hashlib, sys
from mathutils import Vector
from mathutils.geometry import tessellate_polygon
ROOT=pathlib.Path(__file__).resolve().parents[2]; ART=ROOT/'art/blender'; OUT=ART/'restoration-exports'; OUT.mkdir(exist_ok=True)
SRC=ROOT/'.planning/2026-09-21-fleet-building/building-source-graph.json'
source=json.loads(SRC.read_text()); CENTRAL_ONLY='--central-only' in sys.argv; prior=None
if CENTRAL_ONLY:
 prior=json.loads((ART/'restored-facilities-manifest.json').read_text());bpy.ops.wm.open_mainfile(filepath=str(ART/'restored-facilities.blend'))
 old=bpy.data.collections.get('Central119Restored')
 if old:
  for o in list(old.objects):bpy.data.objects.remove(o,do_unlink=True)
  bpy.data.collections.remove(old)
 for o in list(bpy.data.objects):
  if o.type in {'CAMERA','LIGHT'}:bpy.data.objects.remove(o,do_unlink=True)
else:
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene; scene.unit_settings.system='METRIC'; scene.unit_settings.scale_length=1
M={}; collections={}; records=[]; col=None
for name,color in {'White':(.77,.79,.78),'Red':(.64,.035,.025),'Glass':(.075,.16,.19),'Frame':(.38,.43,.44),'Dark':(.055,.07,.075),'Brown':(.34,.18,.075),'Paving':(.45,.44,.4),'Blue':(.035,.12,.22)}.items():
 m=bpy.data.materials.get('Restoration_'+name) or bpy.data.materials.new('Restoration_'+name); m.diffuse_color=(*color,1);m.use_nodes=True; p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=.26 if name=='Glass' else .65;M[name]=m

def mesh(name,verts,faces,mat):
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(name,me);col.objects.link(o);me.materials.append(M[mat]);return o

def box(name,loc,size,mat,angle=0):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.dimensions=size;o.rotation_euler.z=angle;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 for c in list(o.users_collection):c.objects.unlink(o)
 col.objects.link(o);o.data.materials.append(M[mat]);return o

def beam(name,a,b,width,mat):
 a,b=Vector(a),Vector(b);o=box(name,(a+b)/2,(width,width,(b-a).length),mat);o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o

def prism(name,poly,z,h,mat):
 # Normalize winding; triangulate concave roof instead of unsupported n-gon fan.
 p=[Vector((x,y,0)) for x,y in poly]; area=sum(a.x*b.y-b.x*a.y for a,b in zip(p,p[1:]+p[:1]))/2
 if area<0:p.reverse()
 n=len(p);vs=[(v.x,v.y,k) for k in (z,z+h) for v in p];ts=tessellate_polygon([p]);faces=[]
 for tri in ts:
  ids=[v if isinstance(v,int) else min(range(n),key=lambda j:(p[j]-v).length) for v in tri];faces += [tuple(reversed(ids)),tuple(i+n for i in ids)]
 faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 return mesh(name,vs,faces,mat)

def group(name):
 global col
 col=bpy.data.collections.new(name);scene.collection.children.link(col);collections[name]=col

def facade_box(name,p,t,n,u,v,z,w,d,h,mat):
 q=p+t*u+n*v+Vector((0,0,z));return box(name,q,(w,d,h),mat,math.atan2(t.y,t.x))

def depot(index,name,edge,step):
 f=source['facilities'][index];poly=f['geometry']['localFootprintMeters'][:-1];group(name);levels=f['geometry']['levels']['value'];height=4.4+(levels-1)*step
 a=Vector((*poly[edge],0));b=Vector((*poly[(edge+1)%len(poly)],0));t=(b-a).normalized();length=(b-a).length
 area=sum(p[0]*q[1]-q[0]*p[1] for p,q in zip(poly,poly[1:]+poly[:1]))/2;n=Vector((t.y,-t.x,0))*(1 if area>0 else -1)
 prism('Source footprint ground slab',poly,0,.16,'Paving');prism('Upper occupied volume INFERRED_HEIGHT',poly,4.4,height-4.4,'White');prism('Roof slab INFERRED_FORM' if index else 'Observed parapet roof',poly,height,.25,'White')
 for i,(p,q) in enumerate(zip(poly,poly[1:]+poly[:1])):
  if i==edge:continue
  d=Vector((*q,0))-Vector((*p,0));mid=(Vector((*p,0))+Vector((*q,0)))/2
  box('Unobserved ground wall INFERRED',mid+Vector((0,0,2.2)),(d.length,.25,4.4),'Brown' if index else 'White',math.atan2(d.y,d.x))
 # Three real open recesses, not dark decals over a solid wall. Dimensions are authored.
 bay_width=min(6.8,(length-3)/3);spacing=length/3
 for i in range(3):
  u=(i+.5)*spacing
  facade_box('Apparatus opening recessed back INFERRED_DEPTH',a,t,n,u,-5,1.9,bay_width,.15,3.8,'Dark')
  for side in [-1,1]:facade_box('Bay reveal',a,t,n,u+side*bay_width/2,-2.5,1.9,.18,5,3.8,'Brown' if index else 'White')
  facade_box('Apparatus lintel',a,t,n,u,0,4.03,spacing,.42,.74,'Brown' if index else 'White')
 for j in range(4):
  pier=max(.25,spacing-bay_width);u=j*spacing
  if j in [0,3]:pier*=.5;u+=pier*.5*(1 if j==0 else -1)
  facade_box('Apparatus pier',a,t,n,u,0,1.9,pier,.42,3.8,'Brown' if index else 'White')
 for level in range(1,levels):
  z=4.4+(level-1)*step
  if index==0 and level==levels-1:
   # Photo: predominantly opaque top fascia, left recessed red-framed opening, low narrow glazing.
   facade_box('Top low narrow window strip',a,t,n,length*.60,.15,z+.48,length*.68,.13,.42,'Glass')
   for j in range(1,15):facade_box('Top narrow window mullion',a,t,n,length*.26+j*length*.68/15,.24,z+.48,.055,.08,.44,'Frame')
   u=length*.115;w=length*.15;hh=step*.68;cz=z+step*.60
   facade_box('Observed upper left recessed opening',a,t,n,u,.16,cz,w,.16,hh,'Dark')
   for side in [-1,1]:facade_box('Localized red top left jamb',a,t,n,u+side*(w/2+.1),.28,cz,.24,.3,hh+.45,'Red')
   for side in [-1,1]:facade_box('Localized red top left lintel sill',a,t,n,u,.28,cz+side*(hh/2+.1),w+.45,.3,.24,'Red')
  elif index==0:
   facade_box('Observed horizontal glazing band',a,t,n,length*.5,.15,z+step*.53,length-.9,.13,step*.42,'Glass')
   for j in range(1,19):facade_box('Band mullion',a,t,n,j*length/19,.24,z+step*.53,.055,.08,step*.43,'Frame')
   facade_box('White horizontal spandrel',a,t,n,length*.5,.23,z+.28,length+.3,.15,.45,'White')
  else:
   for j in range(4):
    u=(j+.5)*length/4
    facade_box('Observed punched window',a,t,n,u,.15,z+step*.54,1.85,.15,1.55,'Glass')
    for s in [-1,1]:facade_box('Window jamb',a,t,n,u+s*.95,.25,z+step*.54,.09,.12,1.7,'Frame')
    facade_box('Window sill',a,t,n,u,.28,z+step*.54-.82,2.04,.22,.12,'White')
    facade_box('Window centre bar',a,t,n,u,.26,z+step*.54,.065,.08,1.6,'White')
    if j in [1,3]:facade_box('Observed wall AC unit',a,t,n,u+1.3,.4,z+.75,.55,.5,.42,'White')
   facade_box('Panel horizontal joint',a,t,n,length/2,.22,z+.08,length,.02,.025,'Frame')
 if index==0:
  facade_box('Observed red apparatus tier lintel',a,t,n,length/2,.3,4.4,length+.2,.25,.18,'Red')
  facade_box('Selected lower facade right red return',a,t,n,length-.1,.28,6.0,.24,.28,3.4,'Red')
  mast=a+t*length*.15-n*2
  for k in range(9):box('Observed striped roof mast',mast+Vector((0,0,height+.4+k*.48)),(.2,.2,.48),'White' if k%2 else 'Red')
 else:facade_box('Observed narrow vertical glazing strip',a,t,n,length-.6,.25,9.4,.7,.15,9.5,'Glass')
 # Surface boundary only; this is not a proven parking capacity or dispatch location.
 apron=[list((a+n*.4)[:2]),list((b+n*.4)[:2]),list((b+n*5)[:2]),list((a+n*5)[:2])]
 prism('Front apron INFERRED_EXTENT',apron,0,.06,'Paving')
 records.append({'name':name,'osmWay':f['geometry']['osmWay'],'sourceFootprint':poly,'sourceAreaM2':abs(area),'levelsTagged':levels,'authoredHeightMetres':height,'heightProvenance':'INFERRED: apparatus tier 4.4m plus upper tiers '+str(step)+'m; no measured source height','frontEdgeIndex':edge,'frontEdge':[list(a[:2]),list(b[:2])],'outwardXY':list(n[:2]),'frontSelection':'INFERRED from source road geometry and photo street frontage; requires site confirmation','apronBoundaryINFERRED':apron,'openingCount':3,'openingEvidence':'three directly visible' if index==0 else 'at least three visible, full count unknown','roofEvidence':'photo low parapet' if index==0 else 'INFERRED flat closure; roof cropped in 2015 official image','cameraTarget':list((a+b)/2+Vector((0,0,height*.48))),'cameraOutward':list(n),'cameraScale':length*1.65,'sourcePhoto':f['photos'][0]})

depot(0,'Central119Restored',1,3.2)
if not CENTRAL_ONLY:depot(1,'Choryang119Restored',2,3.0)
if not CENTRAL_ONLY:
 group('BusanStationRestored');site=json.loads((ROOT/'asset-library/space-references/busan-reconstruction/busan-site-selected.json').read_text());points=next(f for f in site['features'] if f['id']=='165346389')['points'][4:20];front=[Vector((p['x'],p['z'],0)) for p in points]
 for i,(a,b) in enumerate(zip(front,front[1:])):
  t=(b-a).normalized();n=Vector((-t.y,t.x,0));length=(b-a).length;mid=(a+b)/2
  # Retain 0/5/10m anchors: tiers align with existing authored station floor contract.
  facade_box('Curtain wall',a,t,n,length/2,0,8.3,length,.12,15.8,'Glass')
  for j in range(max(1,math.ceil(length/2.6))+1):
   u=length*j/max(1,math.ceil(length/2.6));facade_box('Curtain wall vertical grid',a,t,n,u,.15,8.3,.07,.12,15.8,'Frame')
  for z in [.4,1.9,3.4,5,6.5,8,9.5,11,12.5,14,15.5,16.2]:facade_box('Curtain wall horizontal grid',a,t,n,length/2,.15,z,length,.12,.07,'Frame')
  facade_box('Observed blue fascia',a,t,n,length/2,.3,12.4,length,.6,.28,'Blue')
  # Broad shallow roof section: lower front eave, raised centre, lower rear closure.
  stations=[(5,16.4),(1,17.15),(-5,17.55),(-13,17.25),(-20,16.6)]
  vs=[]
  for point in [a,b]:
   for offset,z in stations:vs.append(tuple(point+n*offset+Vector((0,0,z))))
  roof=mesh('Broad shallow roof INFERRED_DEPTH',vs,[(k,k+1,k+6,k+5) for k in range(4)],'Dark');mod=roof.modifiers.new('Roof physical thickness','SOLIDIFY');mod.thickness=.22
  beam('Dark continuous roof edge',a+n*5+Vector((0,0,16.4)),b+n*5+Vector((0,0,16.4)),.28,'Dark')
  for u in [length*.25,length*.75]:
   p=a+t*u;beam('Observed inclined eave strut',p+Vector((0,0,12.55)),p+n*3+Vector((0,0,16.65)),.22,'White')
  # Ground-level terrace leaves reference hall and all interior floor polygons untouched.
  facade_box('Approach terrace INFERRED',a,t,n,length/2,3,.12,length,6,.24,'Paving')
 # One broad canopy and repeated glazed end cores, matching observed facade hierarchy.
 for idx in [1,8,13]:
  a,b=front[idx],front[idx+1];t=(b-a).normalized();n=Vector((-t.y,t.x,0));L=(b-a).length
  if idx==8:
   facade_box('Broad central entrance canopy',a,t,n,L/2,4,4.4,27,7,.23,'Dark')
   for u in [-10,0,10]:beam('Entrance canopy column',a+t*(L/2+u)+n*6+Vector((0,0,.24)),a+t*(L/2+u)+n*6+Vector((0,0,4.28)),.18,'Frame')
  else:
   facade_box('Glazed vertical circulation observed form',a,t,n,L/2,2.2,4.25,3.8,3.2,8.5,'Glass')
   for u in [-1.9,1.9]:facade_box('Core vertical frame',a,t,n,L/2+u,3.9,4.3,.12,.12,8.6,'Frame')
  # Shallow steps represent visible approach form; no claimed continuous surveyed route.
  for s in range(7):facade_box('Approach stair INFERRED',a,t,n,L/2,7+s*.34,.21-s*.025,6,.35,.12,'Paving')
 records.append({'name':'BusanStationRestored','osmWay':165346389,'sourceFrontVertices':[[v.x,v.y] for v in front],'sourcePhoto':source['facilities'][2]['photos'][0],'levelsTagged':3,'floorAnchorsUnchanged':[0,5,10],'roof':'Observed broad shallow cantilever and inclined supports; all section heights/depths INFERRED','platformRoof':'Deliberately not extruded as headhouse; existing platform complex retained by integration','interior':'No changes; preserves FDS30x20, floor roots and navigation keepouts','cameraTarget':[-42,-8,8],'cameraOutward':[-1,.25,0],'cameraScale':210})
# Validate geometry before exporting any artifact.
for row in records:
 objects=list(collections[row['name']].objects);assert all(o.type=='MESH' for o in objects)
 coords=[o.matrix_world@v.co for o in objects for v in o.data.vertices];lo=[min(v[k] for v in coords) for k in range(3)];hi=[max(v[k] for v in coords) for k in range(3)]
 row['boundsBlenderMetres']={'min':lo,'max':hi};row['meshCount']=len(objects);row['fontObjects']=0;row['textMeshes']=0
 assert all(math.isfinite(v) for p in coords for v in p)
 if 'sourceAreaM2' in row:assert abs(row['sourceAreaM2']-[1022.8,231.6][records.index(row)])<.15
 for o in objects:
  assert all(p.area>1e-8 for p in o.data.polygons),o.name
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 dest=OUT/(row['name']+'.fbx');bpy.ops.export_scene.fbx(filepath=str(dest),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False)
 row['fbx']=str(dest.relative_to(ROOT));row['sha256']=hashlib.sha256(dest.read_bytes()).hexdigest();row['units']='1 Blender unit = 1 metre; FBX Y up, -Z forward; Unity local x/y/z = source east/up/north'
# Render each original-coordinate model independently; presentation cameras never exported.
scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=1300;scene.render.resolution_y=950;scene.render.resolution_percentage=100
scene.world.use_nodes=True;scene.world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.45,.5,.6,1);scene.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.65;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
bpy.ops.object.light_add(type='SUN',location=(0,0,100));sun=bpy.context.object;sun.rotation_euler=(.45,-.55,-.4);sun.data.energy=2.5
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO';cam.data.lens=45;cam.data.clip_end=4000
for row in records:
 for c in bpy.data.collections:
  if c.name in {'Central119Restored','Choryang119Restored','BusanStationRestored'}:c.hide_render=c.name!=row['name']
 target=Vector(row['cameraTarget']);n=Vector(row['cameraOutward']);cam.location=target+n*row['cameraScale']+Vector((0,0,row['cameraScale']*.3));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();bpy.context.view_layer.update()
 coords=[o.matrix_world@v.co for o in collections[row['name']].objects for v in o.data.vertices];q=cam.rotation_euler.to_quaternion().inverted();view=[q@(v-target) for v in coords];xmin,xmax=min(v.x for v in view),max(v.x for v in view);ymin,ymax=min(v.y for v in view),max(v.y for v in view);cam.location+=cam.rotation_euler.to_quaternion()@Vector(((xmin+xmax)/2,(ymin+ymax)/2,0));cam.data.ortho_scale=max(xmax-xmin,(ymax-ymin)*1300/950)*1.14;scene.render.filepath=str(ART/(row['name']+'-preview.png'));bpy.ops.render.render(write_still=True);row['preview']=str(pathlib.Path(scene.render.filepath).relative_to(ROOT))
for c in bpy.data.collections:c.hide_render=False
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'restored-facilities.blend'))
if CENTRAL_ONLY:
 records += [r for r in prior['models'] if r['name']!='Central119Restored']
manifest={'status':'DRAFT_NOT_UNITY_APPLIED','units':'metres','sourceOriginWgs84':[35.1151,129.0417],'sourceGraphSha256':hashlib.sha256(SRC.read_bytes()).hexdigest(),'scriptSha256':hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),'blenderVersion':bpy.app.version_string,'models':records,'checks':{'finiteVertices':True,'nonzeroPolygonAreas':True,'sourceFootprintAreasWithin015m2':True,'threeOpenRecessesPerDepot':True,'concaveRoofTriangulated':True,'fontObjects':0,'worldTextMeshes':0},'limitations':['Photos constrain only visible surfaces; all vertical dimensions and hidden surfaces inferred.','No as-built interiors, agency catalog, camera, fleet or Unity scene edits.','No product acceptance or native validation.']}
(ART/'restored-facilities-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
