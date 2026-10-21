from pathlib import Path
import json,math,hashlib
import numpy as np
from shapely.geometry import box,Point,Polygon
from shapely import constrained_delaunay_triangles
from shapely.ops import unary_union
R=Path('/Users/um-yunsang/CHOOGuard');B=R/'.state/ACTIVE-ALL2F-COMPLETION-R26/public/nursing-room/constructor';old=json.loads((B/'NURSING-ROOM-PACKET-R26.json').read_text());objs=[];meshes={}
# Direct source geometry gives actual Native5 underside at12.099997m; all fixtures contact it, no new ceiling tile or native cut.
MOUNT=12.099997-7
G='Photo04_Observed_Ceiling_Equipment'
def o(name,pos,size,mat='white',kind='box',euler=(0,0,0),**kw):
 d={'id':name,'kind':kind,'position':list(pos),'scale':list(size),'euler':list(euler),'material':mat,'group':G,'source_ids':['WONEW11-FIELD04-JUL2026'],'qualification':'SOURCE_VISIBLE_FIXTURE_MODEL_PROPORTIONS_MOUNTED_TO_ACTUAL_EXISTING_NATIVE5_UNDERSIDE_NOT_SURVEYED'};d.update(kw);objs.append(d)
def extrude(name,poly,y0,y1,mat='white',offset=(0,0)):
 verts=[];tris=[]
 def add(a,b,c):
  k=len(verts);verts.extend([a,b,c]);tris.extend([k,k+1,k+2])
 def p(a,y):return [a[0]+offset[0],y,a[1]+offset[1]]
 if poly.geom_type=='Polygon':polys=[poly]
 else:polys=list(poly.geoms)
 for pp in polys:
  for tri in constrained_delaunay_triangles(pp).geoms:
   a,b,c=list(tri.exterior.coords)[:3]
   if np.cross(np.array(b)-a,np.array(c)-a)<0:b,c=c,b
   add(p(a,y0),p(b,y0),p(c,y0));add(p(c,y1),p(b,y1),p(a,y1))
  for ring in[pp.exterior,*pp.interiors]:
   coords=list(ring.coords)
   for a,b in zip(coords,coords[1:]):
    aa,bb,cc,dd=p(a,y0),p(b,y0),p(b,y1),p(a,y1);add(aa,bb,cc);add(aa,cc,dd)
 meshes[name]={'vertices':verts,'triangles':tris};o(name,(0,0,0),(1,1,1),mat,'mesh',mesh_key=name)
# One cassette, source central intake and two clearly visible lateral discharge slots.
cx=1.11;cz=1.72
panel=box(-.46,-.535,.46,.535).difference(unary_union([box(-.235,-.345,.235,.345),box(-.397,-.37,-.342,.37),box(.342,-.37,.397,.37)]))
extrude('Photo04_Cassette_White_Panel_Three_Real_Visible_Apertures',panel,MOUNT-.075,MOUNT-.047,offset=(cx,cz))
o('Cassette_Upper_Shallow_Casing',(cx,MOUNT-.018,cz),(.90,.036,1.05),'white')
o('Cassette_Central_Black_Intake_Recess',(cx,MOUNT-.049,cz),(.475,.014,.70),'black')
o('Downloaded_Huuxloc_Actual_Intake_Grille_Only',(cx,MOUNT-.072,cz),(.65,.80,.95),'grille','existing_mesh',mesh_asset='Assets/ChooGuard/Art/StationInterior/R25_Shops/storyway-north/R26_Source_Cassette_HVAC/MESH_Licensed_Huuxloc_Air_Vent_35_Intake.asset',mesh_source_bbox_center=[0,.025,0])
for x in[cx-.37,cx+.37]:
 o('Cassette_Dark_Side_Discharge_'+str(x),(x,MOUNT-.052,cz),(.060,.018,.75),'black')
 o('Cassette_Independently_Editable_Louver_'+str(x),(x,MOUNT-.072,cz),(.027,.014,.70),'white')
# One separate source round exhaust in square white grille carrier, not a second HVAC.
x=1.75;z=.56;circle=Point(0,0).buffer(.178,resolution=40)
extrude('Photo04_Exhaust_Square_Plate_With_Real_Round_Aperture',box(-.228,-.228,.228,.228).difference(circle),MOUNT-.031,MOUNT,offset=(x,z))
o('Exhaust_Visible_Black_Recess',(x,MOUNT-.018,z),(.350,.008,.350),'black','cylinder')
for i,d in enumerate(np.linspace(-.155,.155,15)):
 chord=2*math.sqrt(.172**2-d*d)
 o('Source_Round_Exhaust_Parallel_Grille_Bar_'+str(i),(x+d,MOUNT-.032,z),(.008,.015,chord),'grille')
for dz in[-.09,.09]:o('Round_Exhaust_Grille_Cross_Support_'+str(dz),(x,MOUNT-.022,z+dz),(.31,.012,.008),'grille')
# One separate round ceiling speaker with physical perforations.
x=.02;z=.58;holes=[]
for xx in np.arange(-.105,.106,.023):
 for zz in np.arange(-.105,.106,.023):
  if xx*xx+zz*zz<.108**2:holes.append(Point(xx,zz).buffer(.0035,resolution=5))
extrude('Photo04_Round_Speaker_Perforated_Face',Point(0,0).buffer(.148,resolution=40).difference(unary_union(holes)),MOUNT-.038,MOUNT-.022,'grille',(x,z))
o('Speaker_Dark_Recess',(x,MOUNT-.013,z),(.28,.008,.28),'black','cylinder')
extrude('Speaker_White_Mounting_Rim',Point(0,0).buffer(.166,resolution=40).difference(Point(0,0).buffer(.15,resolution=40)),MOUNT-.025,MOUNT,'white',(x,z))
# One identifiable detector housing, source04 ribbed smoke detector, not guessed additional devices.
x=-.72;z=.55
o('Photo04_Smoke_Detector_Upper_Mount',(x,MOUNT-.012,z),(.184,.012,.184),'white','cylinder')
o('Photo04_Smoke_Detector_Ribbed_Body',(x,MOUNT-.045,z),(.140,.026,.140),'white','cylinder')
for i in range(16):
 a=2*math.pi*i/16;o('Smoke_Detector_Physical_Side_Vent_'+str(i),(x+.067*math.sin(a),MOUNT-.051,z+.067*math.cos(a)),(.012,.020,.006),'black',euler=(0,math.degrees(a),0))
o('Detector_Indicator_Dot_No_Light',(x,MOUNT-.073,z),(.006,.001,.006),'red','cylinder')
# Exactly three directly visible actual recessed downlight bodies, no Light or emissive simulation component.
for i,(x,z)in enumerate([(1.00,.57),(-.22,1.91),(-2.13,3.06)]):
 extrude('Photo04_Downlight_'+str(i)+'_Metal_Annular_Trim',Point(0,0).buffer(.106,resolution=40).difference(Point(0,0).buffer(.088,resolution=40)),MOUNT-.035,MOUNT,'steel',(x,z))
 o('Downlight_'+str(i)+'_White_Opal_Diffuser',(x,MOUNT-.030,z),(.172,.004,.172),'diffuser','cylinder')
# All fixture outlines are actual field types/counts; spacing/sizes photo-proportioned into fixed current cell.
p={k:old[k]for k in ['origin_world_xz','origin_actual_q','floor_world_y','yaw_world']};p.update({'schema':'chooguard.r26.nurse_ceiling_source_bodies.002','author':'classic:/root/r26_nursing_room','scope':'C180_R26_NURSING_ROOM_CEILING_002','asset_folder':'Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/NursingRoomCeiling002','parent_path':'FPSWorld/Architecture/2F/MAIN2F_R26_SOURCE_BOUND_PUBLIC_EQUIPMENT/R26_NURSING_ROOM_SOURCE_MODEL','group_name':'Photo04_Observed_Ceiling_Fixture_Bodies_002','preserved_initial_root_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-'+json.loads((B/'APPLIED-NURSING-ROOM-R26.json').read_text())['root_object_id'].split('-')[-2]+'-0','existing_ceiling_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-263923090-0','ceiling_mount_world_y':MOUNT+7,'ceiling_source_geometry':'ACTUAL-OWN-ROOM-CEILING-TOP-SOURCE-002.json','materials':{k:old['materials'][k]for k in ['white','black','steel','red']},'mesh_library':meshes,'objects':objs,'source_visible_fixture_assemblies':{'cassette_HVAC':1,'round_exhaust_grille_in_square_carrier':1,'round_speaker':1,'smoke_detector':1,'downlight_housings':3},'attribution':'Backrooms Blender Kit by Huuxloc, CC BY 4.0, https://sketchfab.com/3d-models/backrooms-blender-kit-89fad66d968e44aabedf65ac5a06f877 . Only pre-existing independently extracted Air_Vent_35 mesh41 reused, original sourceSHA9904fda74762dca5fb27cc632020e041a38171bc784b9521a8ee0d52d90f03fd. No full kit or room import.\nAll other fixture physical bodies source-shaped from FIELD04, https://m.blog.naver.com/wonew11/224339225506. Source counts identifiable visible items only. Shape/spacing MODEL, actual native5 underside mount12.099997m from clipped source triangles, no ceiling tiles or Lights.\n','geometry_qualification':'Observed fixture types/counts and source arrangement; proportioned MODEL on actual existing Native5 ceiling underside. No exact equipment SKU, calibrated dimension or room height inference.'})
p['materials']['grille']={'color':[.49,.50,.48,1],'metallic':.28,'smoothness':.36};p['materials']['diffuser']={'color':[.9,.91,.87,1],'metallic':0,'smoothness':.36}
(B/'NURSING-CEILING-PACKET-002.json').write_text(json.dumps(p,separators=(',',':'),allow_nan=False));print('additional',len(objs),'meshes',len(meshes),'packetsha',hashlib.sha256((B/'NURSING-CEILING-PACKET-002.json').read_bytes()).hexdigest())
