"""Exact EAST source correction, only explicit previous own parts plus new editable fitout."""
import pathlib,json,hashlib,math,sys
import cv2,numpy as np
from shapely.geometry import Polygon,Point
from shapely.ops import triangulate

BASE=pathlib.Path(__file__).resolve().parent;ROOT=BASE.parents[2]
OUT=BASE/'east-correction003';OUT.mkdir(exist_ok=True)
sys.path.insert(0,str(BASE.parent))
from photo_outline_to_solid import closed_solid_from_mask
region=Polygon(json.loads((BASE/'APPROVED-STORE-WORK-REGION.json').read_text())['geometry_qx_minus_qy']['coordinates'][0])
old=json.loads((BASE/'STORE-PACKET-R25.json').read_text());original=json.loads((BASE/'APPLIED-STORE-R25.json').read_text())
def clean(s):return ''.join(c if c.isalnum()or c in '_-'else'_'for c in s)
olddefs={clean(o['id']):o for o in old['objects']};ids={o['id']:o['object_id']for o in original['objects']}
A=np.array([62.07715767682876,-26.69856675532803]);B=np.array([66.6917,-24.959912101612517]);W=float(np.linalg.norm(B-A));U=(B-A)/W;IN=np.array([U[1],-U[0]])
FY=-math.degrees(math.atan2(U[1],U[0]));OY=math.degrees(math.atan2(-IN[0],-IN[1]))
def fq(s,d=0):return list(A+U*s+IN*d)
materials={k:v for k,v in old['materials'].items()if k in ['navy','black','cream','white','steel','chrome','glass','timber_light','sign_white','display_lit']}
materials['wood_east']=dict(old['materials']['timber_light'],color=[.88,.75,.59,1])
materials['stool_black']={'color':[.018,.018,.019,1],'metallic':.25,'smoothness':.32}
materials['delivery_yellow']={'color':[.98,.58,.025,1],'smoothness':.40}
objects=[]
def add(kind,n,q=None,y=0,size=None,mat='cream',group='EastSourceFitout',yaw=0,**kw):
 o={'kind':kind,'id':n,'group':group,'source_ids':['EAST_MAY18_2026','EAST_FEB05_2026'],'qualification':'EXACT_BRANCH_PHOTO_REFERENCED_MODEL_NOT_SURVEYED','collider':False}
 if q is not None:o.update(q=list(q),y=y)
 if size is not None:o['size']=list(size)
 if mat is not None:o['material']=mat
 if yaw:o['yaw']=yaw
 o.update(kw);objects.append(o);return o
def box(n,q,y,size,mat='cream',group='EastSourceFitout',yaw=0,**kw):return add('box',n,q,y,size,mat,group,yaw,**kw)
def edge(n,a,b,bottom,height,t,mat='navy',group='EastGlassFront',**kw):return add('edge',n,mat=mat,group=group,a=a,b=b,bottom=bottom,height=height,thickness=t,**kw)
def cyl(n,q,y,d,h,mat,group='Lighting'):return add('cylinder',n,q,y,[d,h/2,d],mat,group)

# Actual EAST is a closed glazed front, not the WEST open-counter/accordion type.
for i,(lo,hi)in enumerate([(.23,1.78),(3.15,W-.23)]):
 edge(f'E003_fixed_front_glass_{i}',fq(lo,.10),fq(hi,.10),.04,2.54,.012,'glass')
 edge(f'E003_window_bar_{i}',fq(lo,.35),fq(hi,.35),.90,.055,.33,'wood_east','WindowCounter')
 edge(f'E003_window_counter_black_support_{i}',fq(lo,.35),fq(hi,.35),.04,.08,.12,'black','WindowCounter')
 edge(f'E003_glazing_lower_rail_{i}',fq(lo,.105),fq(hi,.105),.035,.045,.045)
for i,s in enumerate([.23,1.78,2.47,3.15,W-.23]):
 x,z=fq(s,.105);box('E003_front_mullion_'+str(i),[x,z],1.32,[.042,2.61,.055],'navy','EastGlassFront',FY)
edge('E003_front_top_track',fq(.23,.105),fq(W-.23,.105),2.56,.055,.07)
for i,(lo,hi)in enumerate([(1.82,2.45),(2.49,3.11)]):
 edge('E003_paired_glass_leaf_'+str(i),fq(lo,.135),fq(hi,.135),.04,2.48,.012,'glass','Entrance')
 edge('E003_leaf_bottom_frame_'+str(i),fq(lo,.135),fq(hi,.135),.04,.04,.035,'navy','Entrance')
 x,z=fq(hi-.05 if i==0 else lo+.05,.11);box('E003_door_pull_'+str(i),[x,z],1.10,[.028,.32,.045],'chrome','Entrance',FY)
# Brand-pattern safety band is physical printed film on the glass, not furniture text.
for i,(lo,hi)in enumerate([(.25,1.73),(3.20,W-.25)]):edge('E003_glass_safety_strip_'+str(i),fq(lo,.088),fq(hi,.088),.94,.09,.002,'cream','EastGlassFront')
for i in range(6):edge('E003_upper_navy_horizontal_rib_'+str(i),fq(.03,.105),fq(W-.03,.105),3.28+i*.034,.016,.12,'navy','Fascia')

# Downloaded realistic CC0 four-leg high-stool geometry, finish adapted to black field stools.
stool=json.loads((BASE/'sources/east-exact003/METAL-STOOL-ACQUISITION.json').read_text())
imports=[{'source':stool['source'],'destination':'FreeModels/metal_stool_03.fbx','sha256':stool['sha256']}]
crate=json.loads((BASE/'sources/east-exact003/PLASTIC-CRATE-ACQUISITION.json').read_text())
imports.append({'source':crate['source'],'destination':'FreeModels/plastic_crate_01.fbx','sha256':crate['sha256']})
assetfolder='Assets/ChooGuard/Art/StationInterior/R25_Shops/bnc-east/EastBranchCorrection003'
for i,s in enumerate([.82,3.43,4.02,4.58]):
 add('asset','E003_CC0_BLACK_HIGH_STOOL_'+str(i),fq(s,.79),0,mat='stool_black',group='WindowCounter',yaw=OY,
     asset=assetfolder+'/FreeModels/metal_stool_03.fbx',fit_size=[.40,1.06,.43],collider=True)
for i in range(4):
 add('asset','E003_CC0_YELLOW_BAKERY_DELIVERY_CRATE_'+str(i),fq(.69,1.40),.97+i*.22,mat='delivery_yellow',group='SourceVisibleEquipment',yaw=FY,
     asset=assetfolder+'/FreeModels/plastic_crate_01.fbx',fit_size=[.58,.22,.40],collider=True,
     source_ids=['EAST_CURRENT_20260923'])

# Flat natural timber wall and flat-front open bakery shelving, matching EAST material family.
ring=list(region.buffer(-.10,join_style=2).exterior.coords)
for i,(a,b)in enumerate(zip(ring,ring[1:])):
 if max(a[1],b[1])>-27 and abs(a[0]-b[0])>3:continue
 if max(a[1],b[1])<-39 and min(a[0],b[0])<63 and abs(a[0]-b[0])>1.4:
  a,b=sorted([np.array(a),np.array(b)],key=lambda q:q[0]);t=(b-a)/np.linalg.norm(b-a);n=np.array([-t[1],t[0]]);p0=a+t*.22;p1=a+t*1.36
  edge('E003_rear_staff_door_left_wall',list(a),list(p0),.16,2.68,.025,'wood_east','TimberWall')
  edge('E003_rear_staff_door_right_wall',list(p1),list(b),.16,2.68,.025,'wood_east','TimberWall')
  edge('E003_rear_staff_door_header',list(p0),list(p1),2.24,.60,.025,'wood_east','TimberWall')
  doorq=(p0+p1)/2+n*.035;dyaw=math.degrees(math.atan2(n[0],n[1]));dm=np.full((440,180),255,np.uint8);cv2.ellipse(dm,(90,132),(54,32),0,0,360,0,-1)
  door=closed_solid_from_mask(dm,1.10,2.18,.042,minimum_area=2,epsilon=.35)
  add('mesh','E003_STAINLESS_STAFF_DOOR_WITH_OVAL_APERTURE',doorq,1.13,mat='steel',group='StaffDoor',yaw=dyaw,
      vertices=door['vertices'],triangles=door['triangles'],qualification='EXACT_EAST_PHOTO_STEEL_OVAL_WINDOW_DOOR_TYPE_MODEL_ATTACHED_APPROVED_REAR_WALL')
  add('sphere','E003_staff_door_oval_glass',doorq+n*.005,1.56,[.66,.32,.012],'glass','StaffDoor',dyaw)
  for j,pj in enumerate([p0,p1]):box('E003_staff_door_frame_'+str(j),list(pj+n*.025),1.13,[.035,2.23,.055],'steel','StaffDoor',dyaw)
  box('E003_staff_door_pull',list(doorq+t*.38+n*.035),1.10,[.035,.22,.045],'chrome','StaffDoor',dyaw)
  continue
 edge('E003_rear_wood_cladding_'+str(i),list(a),list(b),.16,2.68,.025,'wood_east','TimberWall')
 # source vertical wood grain joints, per actual wall segments
 length=math.dist(a,b)
 for j in range(1,int(length/.42)):
  x=a[0]+(b[0]-a[0])*j/int(length/.42);z=a[1]+(b[1]-a[1])*j/int(length/.42)
  box(f'E003_timber_vertical_joint_{i}_{j}',[x,z],1.52,[.008,2.68,.008],'timber_light','TimberWall')

for i,z in enumerate([-30.55,-32.10,-33.65,-35.20]):
 # old scan products stay in place, but WEST raised-panel cabinetry is removed.
 box('E003_flat_bread_display_base_'+str(i),[65.95,z],.42,[.57,.84,1.41],'wood_east','BakeryDisplay',collider=True)
 for h in [.91,1.43,1.98]:box(f'E003_open_wood_shelf_{i}_{h}',[65.95,z],h,[.59,.055,1.43],'wood_east','BakeryDisplay')
 for zz in [z-.70,z+.70]:box('E003_rack_vertical_'+str(i)+'_'+str(zz),[66.20,zz],1.14,[.04,2.26,.04],'wood_east','BakeryDisplay')
for i,x in enumerate([63.15,64.57,65.83]):
 box('E003_straight_rear_bread_bench_'+str(i),[x,-36.25],.43,[1.30,.86,.64],'wood_east','BakeryDisplay',collider=True)
 box('E003_rear_bench_top_'+str(i),[x,-36.25],.90,[1.32,.055,.66],'cream','BakeryDisplay')
for i,(q,w,d)in enumerate([([64.50,-29.58],1.22,.72),([64.45,-33.83],.95,.68)]):
 box('E003_cream_product_island_'+str(i),q,.44,[w,.88,d],'cream','BakeryDisplay',collider=True)
 box('E003_cream_island_top_'+str(i),q,.915,[w+.02,.05,d+.02],'white','BakeryDisplay')

# Cream production/display bank and metal tray/cup equipment actually visible behind EAST glass.
for i,z in enumerate([-30.7,-31.85,-33.0]):
 box('E003_cream_work_cabinet_'+str(i),[63.05,z],.82,[.70,1.64,1.10],'cream','ProductionCounter',collider=True)
 box('E003_work_cabinet_stainless_top_'+str(i),[63.05,z],1.665,[.72,.05,1.12],'steel','ProductionCounter')
 for k in [-.31,.31]:
  box(f'E003_cabinet_recessed_handle_{i}_{k}',[63.411,z+k],.95,[.025,.11,.035],'chrome','ProductionCounter')
  box(f'E003_cabinet_foot_{i}_{k}',[63.05,z+k],.055,[.40,.11,.035],'steel','ProductionCounter')
for j in range(7):
 z=-34.0-j*.15
 box('E003_stainless_tray_rack_shelf_'+str(j),[63.02,z],1.80+j*.04,[.63,.022,.13],'steel','SourceVisibleEquipment')
for j in range(10):
 add('mesh_ref','E003_CC0_VISIBLE_CUP_'+str(j),[62.90+(j%2)*.11,-31.75-(j//2)*.11],1.72,mat='glass',group='SourceVisibleEquipment',
     mesh_asset='Assets/ChooGuard/Art/StationInterior/GlassShopFitoutC160_20261018/Meshes/kenney_food_cup__0.asset',fit_size=[.073,.105,.073])

# Plain pale ceiling with small downlights/cassette, no WEST coffers/medallions/teal dividers.
poly=region.buffer(-.09,join_style=2);vs=[];ts=[]
for t in triangulate(poly):
 if not poly.covers(t.representative_point()):continue
 c=list(t.exterior.coords)[:3];i=len(vs);vs.extend([[x-64,2.91,z+32]for x,z in c]);vs.extend([[x-64,2.96,z+32]for x,z in c]);ts.extend([i,i+1,i+2,i+3,i+5,i+4])
for a,b in zip(list(poly.exterior.coords),list(poly.exterior.coords)[1:]):
 i=len(vs);vs.extend([[a[0]-64,2.91,a[1]+32],[b[0]-64,2.91,b[1]+32],[b[0]-64,2.96,b[1]+32],[a[0]-64,2.96,a[1]+32]]);ts.extend([i,i+2,i+1,i,i+3,i+2])
add('mesh','E003_plain_pale_ceiling',[64,-32],0,mat='cream',group='Ceiling',vertices=vs,triangles=ts)
for ix in range(2):
 for iz in range(6):
  q=[63.2+ix*2.4,-28.8-iz*1.7]
  if not poly.covers(Point(q).buffer(.14)):continue
  cyl(f'E003_recessed_downlight_trim_{ix}_{iz}',q,2.885,.115,.025,'white')
  cyl(f'E003_recessed_downlight_lens_{ix}_{iz}',q,2.868,.085,.008,'display_lit')
for i,z in enumerate([-29.5,-34.5]):
 box('E003_cassette_AC_body_'+str(i),[64.5,z],2.88,[.83,.065,.83],'cream','HVAC')
 for k in range(10):box(f'E003_AC_louvre_{i}_{k}',[64.5,z+(k-4.5)*.052],2.84,[.62,.008,.016],'black','HVAC')

# Exact EAST custom wordmark including loaf emblem, traced from field photo, no font.
source=cv2.imread(str(BASE/'sources/east-exact003/east-sign-source-crop.jpg'))
src=np.float32([[5,100],[388,10],[388,85],[5,177]])
dst=np.float32([[0,0],[1000,0],[1000,220],[0,220]])
rect=cv2.warpPerspective(source,cv2.getPerspectiveTransform(src,dst),(1000,220))
hsv=cv2.cvtColor(rect,cv2.COLOR_BGR2HSV);mask=cv2.inRange(hsv,(0,0,180),(179,90,255))
cv2.imwrite(str(OUT/'EAST-ACTUAL-SIGN-RECTIFIED.png'),rect);cv2.imwrite(str(OUT/'EAST-ACTUAL-INK-MASK.png'),mask)
mesh=closed_solid_from_mask(mask,2.45,.40,.035,minimum_area=6,epsilon=.4)
add('mesh','E003_ACTUAL_PHOTO_LOAF_BNC_BAKERY_SOLID',fq(W*.53,.007),2.985,mat='sign_white',group='PhysicalSign',yaw=OY,
    vertices=mesh['vertices'],triangles=mesh['triangles'],source_ids=['EAST_FEB05_2026_EXACT_FRONT'],
    qualification='DIRECT_PHOTO_GLYPH_OUTLINE_CLOSED_35MM_PHYSICAL_SOLID_NO_FONT_NO_BILLBOARD')

# Select exact previous OWN identities for retirement. Preserve merchandise/POS/cold-display hardware.
removegroups={'Cabinetry','StaffFront','Ceiling','Lighting','HVAC','Entrance','VisibleStaffEquipment','Signage'}
remove=[]
for o in original['objects']:
 d=olddefs[o['id']]
 if d['group']in removegroups or o['id']=='cream_header_above_fascia':remove.append({'id':o['id'],'object_id':o['object_id']})
for name in ['cashier_brushed_stainless_cabinet','cashier_countertop']:
 remove.append({'id':name,'object_id':ids[name]})
signr=json.loads((BASE/'sign-solid/APPLIED-PHOTO-SOLID-SIGNS-R25.json').read_text())
for o in signr['created_objects']:remove.append({'id':o['name'],'object_id':o['object_id']})

# Rigid own component moves re-use actual cold-display equipment and all cake scans.
moves=[]
case_defs=[('front_refrigerated_case_0',fq(2.74,.53),FY,[65.18,-30.80],90),
           ('front_refrigerated_case_1',fq(4.02,.53),FY,[65.18,-32.14],90),
           ('left_interior_refrigerated_0',[62.85,-30.85],90,[65.18,-33.60],90),
           ('left_interior_refrigerated_1',[62.85,-32.42],90,[65.18,-35.16],90)]
for prefix,oldq,oldyaw,newq,newyaw in case_defs:
 for o in original['objects']:
  name=o['id'];d=olddefs[name]
  in_case=name.startswith(prefix)
  if prefix.startswith('front_refrigerated'):
   idx=prefix[-1];in_case|=name.startswith('front_cold_cake_'+idx+'_')
  else:
   idx=prefix[-1];in_case|=name.startswith('interior_cake_'+idx+'_')
  if in_case:moves.append({'id':name,'object_id':o['object_id'],'old_center_q':oldq,'new_center_q':newq,'delta_yaw':newyaw-oldyaw})

packet={'store_key':'bnc-east','origin_q':[64,-32],'floor_world_y':7.0,
 'source_packet':str((BASE/'SOURCE-EVIDENCE-EAST-CORRECTED-003.json').relative_to(ROOT)),
 'source_imports':imports,'materials':materials,'mesh_library':{},'objects':objects,
 'remove_exact_previous_own_objects':remove,'move_exact_retained_equipment_and_cakes':moves,
 'move_retained_POS':{'ids':[{'id':o['id'],'object_id':o['object_id']}for o in original['objects']if olddefs[o['id']]['group']=='Cashier'and o['id']not in ['cashier_brushed_stainless_cabinet','cashier_countertop']],
     'old_center_q':fq(1.55,.53),'new_center_q':[63.05,-31.85],'delta_yaw':90-FY,'height_offset':.68},
 'keep_products_198':True,'scope':'Only exact own previous WEST-misbound fitout; approved footprint unchanged; no native/floor/otherstore changes.'}
(OUT/'EAST-CORRECTION-PACKET-003.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2))
print({'new_objects':len(objects),'retired_previous_owned_candidates':len(remove),'retained_equipment_cake_moves':len(moves),'sign_triangles':len(mesh['triangles'])//3,'sha256':hashlib.sha256((OUT/'EAST-CORRECTION-PACKET-003.json').read_bytes()).hexdigest()})
