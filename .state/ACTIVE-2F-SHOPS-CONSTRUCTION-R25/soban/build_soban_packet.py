import json, math, hashlib
from pathlib import Path
import numpy as np
from shapely.geometry import shape, Polygon, box
from shapely.ops import triangulate

P=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/soban')
B=json.loads((P/'APPROVED-STORE-WORK-REGION.json').read_text())
boundary=shape(B['geometry_qx_minus_qy'])
O=np.array([51.01775046546757,-30.8655]); U=np.array([.93578126,.35258111]); D=np.array([.35258111,-.93578126]); YAW=-math.degrees(math.atan2(U[1],U[0]))
def q(x,z): return (O+U*x+D*z).tolist()
objects=[]
def add(id,kind,x,z,y,group='Fitout',src='S02',**kw):
 o=dict(id=id,kind=kind,q=q(x,z),y=y,group=group,source_ids=[src],qualification='SOURCE_OBSERVED_TYPE__APPROVED_BOUNDARY_FIT_MODEL_NOT_FIELD_MEASURED');o.update(kw);objects.append(o);return o
def cube(id,x,z,y,sx,sy,sz,mat,group='Fitout',src='S02',**kw):return add(id,'box',x,z,y,group,src,size=[sx,sy,sz],yaw=kw.pop('yaw',YAW),material=mat,**kw)
def edge(id,a,b,h,t,mat,bottom=0,group='Booth',src='S01'):
 aq=q(*a);bq=q(*b);return add(id,'edge',(a[0]+b[0])/2,(a[1]+b[1])/2,0,group,src,a=aq,b=bq,height=h,thickness=t,bottom=bottom,material=mat)
def asset(id,name,x,z,y,size,yaw=0,mat=None,group='Equipment',src='S02'):
 path='Assets/ChooGuard/Art/StationInterior/Kits/restaurant-bits/KayKit_Restaurant_Bits_1.0_FREE/Assets/fbx (unity)/'+name+'.fbx'
 o=add(id,'asset',x,z,y,group,src,asset=path,fit_size=size,yaw=YAW+yaw)
 if mat:o['material']=mat
 return o
def text(id,label,x,z,y,width,height,color=(1,1,1,1),group='Signs',src='S01',yaw=None):
 return add(id,'text',x,z,y,group,src,text=label,width=width,height=height,color=color,yaw=(YAW if yaw is None else yaw))
def mesh(id,verts,tris,x=0,z=0,y=0,mat='wood',group='Fitout',src='S02'):
 return add(id,'mesh',x,z,y,group,src,vertices=verts,triangles=tris,material=mat,collider=False)
def extrude_poly(id,poly,bottom,height,mat):
 verts=[];tris=[]
 def tri(a,b,c):
  i=len(verts);verts.extend([a,b,c]);tris.extend([i,i+1,i+2])
 for t in triangulate(poly):
  if poly.covers(t.representative_point()):
   a,b,c=list(t.exterior.coords)[:3]
   aa=[a[0]-O[0],bottom,a[1]-O[1]];bb=[b[0]-O[0],bottom,b[1]-O[1]];cc=[c[0]-O[0],bottom,c[1]-O[1]]
   tri(aa,bb,cc);tri([aa[0],bottom+height,aa[2]],[cc[0],bottom+height,cc[2]],[bb[0],bottom+height,bb[2]])
 for ring in [poly.exterior,*poly.interiors]:
  cs=list(ring.coords)
  for a,b in zip(cs,cs[1:]):
   aa=[a[0]-O[0],bottom,a[1]-O[1]];bb=[b[0]-O[0],bottom,b[1]-O[1]];cc=[b[0]-O[0],bottom+height,b[1]-O[1]];dd=[a[0]-O[0],bottom+height,a[1]-O[1]]
   tri(aa,cc,bb);tri(aa,dd,cc)
 mesh(id,verts,tris,mat=mat,group='Ceiling')

# Own enclosure follows the exact accepted polyline; native footprint and column hole stay fixed.
cs=list(boundary.buffer(-.065,join_style=2).exterior.coords)
for i,(a,b) in enumerate(zip(cs,cs[1:])):
 if abs(np.dot(np.array(a)-O,D))<.15 and abs(np.dot(np.array(b)-O,D))<.15:continue # hall-facing glass front
 aq=np.array(a);bq=np.array(b);length=np.linalg.norm(bq-aq)
 if length<.00001: continue
 c=(aq+bq)/2
 objects.append(dict(id=f'Approved_side_back_wall_{i:02}',kind='edge',q=c.tolist(),a=aq.tolist(),b=bq.tolist(),height=3.1,thickness=.10,bottom=0,material='white',group='Booth',source_ids=['S02'],qualification='APPROVED_CAD_LINE_BOUNDARY__PHOTO_FINISH_MODEL'))
extrude_poly('Own_dark_ceiling_follows_approved_polygon_and_column_void',boundary,3.10,.10,'charcoal')
W=11.818367930144177
edge('Navy_sign_fascia',(0,.10),(W,.10),.64,.15,'navy',2.46)
for j in range(6):edge(f'Navy_horizontal_louvre_{j}',(0,.10),(W,.10),.025,.19,'navy',3.12+j*.055)
for a,b,k in [(0.14,4.69,'left'),(7.09,W-.14,'right')]:
 edge('Fixed_front_glass_'+k,(a,.13),(b,.13),2.32,.018,'glass',.12)
for a in [0.10,4.73,7.05,W-.10]:cube('Dark_vertical_glass_mullion_'+str(a),a,.13,1.25,.055,2.5,.075,'navy','Booth')
edge('Header_sliding_door_rail',(0,.13),(W,.13),.07,.11,'metal',2.36)
edge('Threshold_flush_door_guide',(0,.13),(W,.13),.018,.07,'metal',.02)
# A clear 2.2m central walk-through with each glass sliding leaf parked at its own side.
for a,b,k in [(3.55,4.70,'left'),(7.12,8.27,'right')]:
 edge('Open_sliding_leaf_'+k,(a,.22),(b,.22),2.25,.022,'glass',.10,'Doors')
 for xx in [a,b]:cube('Sliding_leaf_frame_'+k+str(xx),xx,.22,1.23,.035,2.3,.045,'metal','Doors')
 cube('Sliding_leaf_pull_'+k,(a+b)/2,.17,1.05,.025,.32,.025,'metal','Doors')
text('Direct_modeled_brand_letters','소 반 한 식',5.9,.00,2.77,4.35,.43)
cube('Yellow_bowl_brand_plaque',3.15,.00,2.78,.46,.46,.045,'yellow','Signs')
text('Modeled_bowl_icon','♨',3.15,-.035,2.79,.37,.36)

# Original source-specific furniture: rectangular honey tops, slim black steel legs,
# spindle wood backs and dark padded seats. Their metric layout is MODEL within approved cell.
def chair(id,x,z,ang):
 # mesh rods/leg profiles as one editable wooden frame, separate padded seat
 a=math.radians(ang);ca,sa=math.cos(a),math.sin(a);verts=[];tri=[]
 def bar(cx,cy,cz,sx,sy,sz):
  start=len(verts)
  for dx,dy,dz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]:
   xx=cx+dx*sx/2;zz=cz+dz*sz/2;xx,zz=xx*ca+zz*sa,-xx*sa+zz*ca
   pp=U*xx+D*zz;verts.append([pp[0],cy+dy*sy/2,pp[1]])
  tri.extend(start+i for i in [0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,1,2,6,1,6,5,0,4,7,0,7,3])
 for xx in [-.19,.19]:
  for zz in [-.19,.19]:bar(xx,.225,zz,.035,.45,.035)
 for xx in [-.195,.195]:bar(xx,.66,.205,.032,.48,.035)
 for xx in [-.13,-.065,0,.065,.13]:bar(xx,.695,.205,.018,.31,.025)
 bar(0,.875,.205,.43,.045,.045);bar(0,.49,.18,.43,.035,.035)
 mesh(id+'_honey_spindle_frame',verts,tri,x,z,mat='wood',group='Seating')
 cube(id+'_brown_padded_seat',x,z,.47,.43,.05,.43,'brown','Seating',yaw=YAW+ang)
tables=[(2.0,2.5),(5.7,2.8),(9.1,2.8),(1.9,5.3),(5.6,5.7),(8.45,5.7),(7.65,8.0),(6.65,10.55)]
for n,(x,z) in enumerate(tables):
 cube(f'Table_{n:02}_honey_top',x,z,.755,1.20,.045,.75,'wood','Seating')
 for dx in [-.53,.53]:
  for dz in [-.30,.30]:cube(f'Table_{n:02}_black_leg_{dx}_{dz}',x+dx,z+dz,.36,.035,.72,.035,'black','Seating')
 for j,(dx,dz,ang) in enumerate([(-.31,-.64,180),(.31,-.64,180),(-.31,.64,0),(.31,.64,0)]):
  if boundary.buffer(-.22).contains(Polygon([q(x+dx-.24,z+dz-.26),q(x+dx+.24,z+dz-.26),q(x+dx+.24,z+dz+.26),q(x+dx-.24,z+dz+.26)])):
   chair(f'Table{n:02}_chair{j}',x+dx,z+dz,ang)

# White tiled open kitchen and oak front service pass observed in source S02, no sealed room fiction.
cube('Open_kitchen_service_oak_front',3.05,8.72,.53,5.55,1.06,.58,'wood','Kitchen')
cube('Stainless_service_pass_top',3.05,8.72,1.075,5.65,.05,.68,'metal','Kitchen')
cube('Kitchen_service_left_return',.60,9.60,.50,.62,1.0,1.5,'metal','Kitchen')
cube('White_tile_hood_backing',2.90,11.8,1.40,5.5,2.8,.10,'tile','Kitchen')
cube('Observed_stainless_cooker_worktop',2.95,11.10,.86,4.65,.10,.80,'metal','Kitchen')
cube('Observed_commercial_hood_body',2.95,11.1,2.35,4.7,.45,1.05,'metal','Kitchen')
for xx in [1.05,2.25,3.45,4.65]:
 cube(f'Hood_baffle_{xx}',xx,10.95,2.10,1.06,.025,.65,'darkmetal','Kitchen')
cube('Hood_vertical_rect_duct',3.50,11.12,2.92,.48,.68,.50,'metal','Kitchen')
cube('Hood_safety_service_duct',1.1,11.12,2.9,.32,.55,.36,'metal','Kitchen')
for n,x in enumerate([1.30,2.32,3.34]):
 asset(f'Observed_stove_position_{n}','stove_single_countertop',x,11.02,.94,[.58,.27,.60],mat='darkmetal',group='Kitchen')
 asset(f'Observed_pot_{n}','stew_bowl',x,11.02,1.18,[.31,.17,.31],mat='black',group='Kitchen')
asset('Observed_rear_sink_unit','kitchentable_sink',.52,10.5,0,[.75,.92,.65],group='Kitchen')
for n,x in enumerate([4.35,4.9]):
 add(f'Observed_black_rice_cooker_{n}','cylinder',x,10.95,1.12,'Kitchen','S02',size=[.40,.18,.40],material='black')
 add(f'Observed_rice_cooker_lid_{n}','sphere',x,10.95,1.31,'Kitchen','S02',size=[.40,.08,.40],material='metal')
 cube(f'Rice_cooker_control_{n}',x,10.72,1.13,.16,.08,.025,'black','Kitchen')
cube('Serving_backshelf',2.2,11.65,1.35,3.7,.05,.4,'metal','Kitchen')
for n in range(7):
 for k in range(4):asset(f'White_serving_bowl_stack_{n}_{k}','bowl_small',.8+n*.43,11.65,1.39+k*.03,[.17,.055,.17],mat='white',group='Kitchen')

# Actual self-service zone at the kitchen side: white purifier, dark UV cup cabinet,
# white wall unit and open wooden utensil shelf, all separately editable.
cube('White_water_purifier_body',5.90,8.90,.57,.38,1.14,.42,'white','SelfService')
cube('Water_purifier_top',5.90,8.9,1.2,.40,.16,.43,'white','SelfService')
cube('Water_purifier_dark_recess',5.9,8.65,.96,.25,.28,.025,'black','SelfService')
cube('Cup_sterilizer_dark_cabinet',6.42,8.87,.73,.55,1.46,.45,'black','SelfService')
cube('Cup_sterilizer_glass_door',6.42,8.62,.87,.48,1.03,.018,'glass','SelfService')
for row in range(4):
 cube(f'UV_shelf_{row}',6.42,8.86,.47+row*.24,.50,.025,.38,'metal','SelfService')
 for col in range(4):add(f'UV_tumbler_{row}_{col}','cylinder',6.23+col*.12,8.85,.54+row*.24,'SelfService','S02',size=[.065,.065,.065],material='metal',collider=False)
cube('Wall_UV_white_sterilizer',6.35,9.1,2.07,.74,.49,.32,'white','SelfService')
for z in [2.00,2.13]:cube(f'Wall_UV_blue_slot_{z}',6.35,8.92,z,.50,.035,.02,'blue','SelfService')
cube('Rear_glass_drinks_fridge',4.95,9.55,.91,.69,1.82,.65,'white','SelfService')
cube('Fridge_transparent_door',4.95,9.20,.96,.60,1.53,.018,'glass','SelfService')
for r in range(5):
 cube(f'Fridge_shelf_{r}',4.95,9.53,.30+r*.29,.58,.02,.56,'metal','SelfService')
 for c in range(4):add(f'Fridge_drink_bottle_{r}_{c}','cylinder',4.74+c*.14,9.32,.40+r*.29,'SelfService','S01',size=[.075,.095,.075],material=['green','yellow','blue','white'][c],collider=False)
cube('Observed_open_wood_utensil_rack',7.03,9.02,.95,.38,1.90,.30,'wood','SelfService')
for n in range(6):cube(f'Utensil_rack_shelf_{n}',7.03,9.02,.2+n*.28,.37,.025,.30,'wood','SelfService')

# POS/kiosk accurately follows the observed tall black terminal; interfaces are modeled,
# no photo used as a substitute for the physical sign/cabinet.
cube('Black_order_kiosk_pedestal',1.10,7.85,.43,.47,.86,.38,'black','Cashier')
cube('Black_order_kiosk_body',1.1,7.85,1.35,.61,1.14,.12,'black','Cashier')
cube('Kiosk_touch_display',1.1,7.77,1.48,.52,.72,.02,'screen','Cashier')
text('Kiosk_modeled_header','소반한식',1.1,7.745,1.72,.42,.08,group='Cashier',src='S02')
for row in range(3):
 for col in range(3):cube(f'Kiosk_menu_tile_{row}_{col}',.94+col*.16,7.75,1.50-row*.16,.13,.12,.012,'yellow','Cashier')
cube('Kiosk_card_reader',1.26,7.75,.94,.14,.15,.06,'metal','Cashier')
cube('Kiosk_receipt_slot',.99,7.75,.96,.17,.022,.025,'black','Cashier')
cube('POS_display',.48,8.44,1.24,.35,.29,.055,'black','Cashier')
cube('POS_stand',.48,8.52,1.10,.10,.17,.12,'black','Cashier')

# Directly built physical menu panels and raised typography.
for x,key in [(1.15,'left'),(10.56,'right')]:
 cube('Tall_menu_backing_'+key,x,.075,1.20,1.65,2.30,.055,'white','Signs')
 text('Menu_brand_'+key,'소반한식',x,.035,2.13,1.3,.21,color=[.08,.08,.1,1])
 labels=['해물순두부  10,000','굴순두부  10,500','전주비빔밥  12,000','고등어솥밥  12,000','뚝배기불고기  12,000','바지락칼국수  10,500']
 for n,label in enumerate(labels):text('Menu_row_'+key+str(n),label,x,.032,1.83-n*.23,1.42,.12,color=[.08,.08,.1,1])
cube('Kitchen_high_menu_lightbox',3.05,8.74,2.55,5.4,.60,.11,'white','Signs')
for j,label in enumerate(['순두부류','한식류','솥밥류','칼국수류','냉면류']):text('Kitchen_menu_category_'+str(j),label,.95+j*1.05,8.66,2.55,.88,.15,color=[.1,.1,.1,1],src='S03')
cube('Entrance_digital_menu_stand',9.20,.80,.52,.09,1.04,.1,'black','Signs')
cube('Entrance_digital_menu_tv',9.20,.80,1.22,1.02,.62,.08,'black','Signs')
cube('Entrance_digital_menu_screen',9.20,.745,1.22,.93,.53,.015,'screen','Signs')
text('Digital_menu_modeled_text','TAKE OUT\n소반한식',9.20,.72,1.23,.77,.36,src='S01')

# Track lights and observed woven amber pendant pair, no global illumination modification.
for z in [2.1,5.3,8.1]:
 edge('Black_ceiling_track_'+str(z),(.7,z),(9.3-z*.18,z),.035,.045,'black',3.02,'Lighting','S02')
 for n,x in enumerate([1.1,3.6,6.1,8.1-z*.1]):
  add(f'Track_spot_{z}_{n}','cylinder',x,z,2.95,'Lighting','S02',size=[.13,.09,.13],material='black',collider=False)
  add(f'Warm_downlight_{z}_{n}','cylinder',x,z,2.845,'Lighting','S02',size=[.11,.01,.11],material='emissive',collider=False)
for n,(x,z) in enumerate([(9.0,4.3),(8.40,7.2)]):
 cube(f'Pendant_cord_{n}',x,z,2.78,.012,.55,.012,'black','Lighting')
 add(f'Woven_amber_pendant_{n}','cylinder',x,z,2.47,'Lighting','S02',size=[.38,.15,.38],material='wicker',collider=False)
 for k in range(12):
  angle=k*math.tau/12
  cube(f'Woven_vertical_reed_{n}_{k}',x+.18*math.cos(angle),z+.18*math.sin(angle),2.47,.016,.32,.016,'wood','Lighting')
 add(f'Own_pendant_light_{n}','light',x,z,2.34,'Lighting','S02',color=[1,.80,.49,1],range=2.8,intensity=.22)

# Source S06/S07 directly records the dining-room cassette A/C and side-wall clock.
cube('Observed_ceiling_cassette_AC',5.7,5.4,3.00,.92,.12,.92,'white','HVAC','S06')
cube('AC_return_dark_filter',5.7,5.4,2.925,.52,.025,.52,'darkmetal','HVAC','S06')
for side in [-1,1]:
 cube('AC_side_louvre_'+str(side),5.7+side*.36,5.4,2.92,.12,.018,.68,'metal','HVAC','S06')
 cube('AC_cross_louvre_'+str(side),5.7,5.4+side*.36,2.92,.68,.018,.12,'metal','HVAC','S06')
add('Observed_wall_clock','cylinder',8.80,7.00,2.04,'WallDetails','S02',size=[.30,.025,.30],material='white',euler=[90,YAW+90,0],collider=False)
cube('Clock_hands',8.76,7.0,2.04,.01,.14,.02,'black','WallDetails','S02')
cube('Observed_tray_return_wood_body',6.12,9.25,.81,.40,1.62,.45,'wood','SelfService','S06')
text('Tray_return_direct_letters','퇴식대',6.12,9.0,1.63,.36,.11,color=[.08,.08,.08,1],src='S06')

# Tableware visible in source: individual stainless rice pots, black trays and white side dishes.
for n,(x,z) in enumerate(tables[:5]):
 cube(f'Observed_black_meal_tray_{n}',x,z,.791,.53,.025,.40,'black','Tableware','S03')
 asset(f'Observed_ceramic_bowl_{n}','bowl',x+.12,z,.81,[.19,.075,.19],mat='white',group='Tableware',src='S03')
 asset(f'Observed_hotpot_{n}','stew_bowl',x-.12,z,.81,[.20,.11,.20],mat='black',group='Tableware',src='S03')
 for k in range(3):asset(f'Observed_side_dish_{n}_{k}','bowl_small',x-.16+k*.16,z+.14,.82,[.11,.04,.11],mat='white',group='Tableware',src='S03')

materials={
 'white':dict(color=[.92,.91,.86,1],smoothness=.40),
 'navy':dict(color=[.055,.055,.10,1],smoothness=.42,metallic=.1),
 'wood':dict(color=[.58,.36,.145,1],smoothness=.40),
 'brown':dict(color=[.12,.055,.025,1],smoothness=.42),
 'metal':dict(color=[.57,.59,.60,1],smoothness=.75,metallic=.86),
 'darkmetal':dict(color=[.18,.20,.21,1],smoothness=.58,metallic=.80),
 'black':dict(color=[.018,.018,.02,1],smoothness=.35),
 'charcoal':dict(color=[.032,.034,.035,1],smoothness=.1),
 'tile':dict(color=[.86,.87,.83,1],smoothness=.77),
 'glass':dict(color=[.79,.91,.94,.12],transparent=True,smoothness=.96,metallic=.04),
 'yellow':dict(color=[.97,.76,.035,1],smoothness=.28),
 'blue':dict(color=[.04,.14,.42,1],smoothness=.65),
 'green':dict(color=[.04,.38,.12,1],smoothness=.65),
 'screen':dict(color=[.06,.095,.08,1],smoothness=.65,emission=[.09,.13,.11,1]),
 'wicker':dict(color=[.67,.31,.055,1],smoothness=.2,emission=[.15,.055,.004,1]),
 'emissive':dict(color=[1,.84,.59,1],emission=[1.6,1.35,.95,1],smoothness=.2),
}
packet=dict(store_key='soban',origin_q=O.tolist(),floor_world_y=7.0,source_packet=str(P/'SOURCE-EVIDENCE-R25.json'),approved_boundary=B['geometry_qx_minus_qy'],materials=materials,objects=objects,qualification='OBSERVED_SHOP_TYPES_AND_RELATIONSHIPS__MODEL_METRIC_LAYOUT_WITHIN_USER_APPROVED_R24_CELL',no_global_native_floor_column_transport_mutations=True)
(P/'STORE-MODEL-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2))
sha=hashlib.sha256((P/'STORE-MODEL-PACKET-R25.json').read_bytes()).hexdigest()
s=(P.parent/'StoreBuilderR25.template.cs').read_text()
for a,b in {'__STORE_KEY__':'soban','__WORKER_THREAD__':'classic:/root/r25_soban','__EXACT_SCOPE__':'C180_R25_SOBAN_APPLY_001','__PACKET_PATH__':str(P/'STORE-MODEL-PACKET-R25.json'),'__PACKET_SHA256__':sha}.items():s=s.replace(a,b)
(P/'ApplyStoreR25.cs').write_text(s)
print(json.dumps({'objects':len(objects),'packet_sha256':sha,'asset_instances':sum(x['kind']=='asset' for x in objects),'custom_meshes':sum(x['kind']=='mesh' for x in objects)},indent=2))
