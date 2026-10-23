from pathlib import Path
import math,json,hashlib
from shapely.geometry import shape,Polygon
P=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/gijang')
a=json.load(open(P/'APPROVED-STORE-WORK-REGION.json'));poly=shape(a['geometry_qx_minus_qy']);A,B,C,D=list(poly.exterior.coords)[:-1]
L=math.dist(B,C);T=((C[0]-B[0])/L,(C[1]-B[1])/L);N=(T[1],-T[0]);BL=math.dist(A,D);BT=((D[0]-A[0])/BL,(D[1]-A[1])/BL);BN=(-BT[1],BT[0]);origin=list(poly.centroid.coords)[0];objects=[]
def add(**kw):
 kw.setdefault('source_ids',['NAVER_20240515']);kw.setdefault('qualification','PHOTO_REFERENCED_MODEL_INSIDE_USER_APPROVED_R24_NOT_FIELD_MEASURED');objects.append(kw)
def q(u,v):return [B[0]+u*T[0]+v*N[0],B[1]+u*T[1]+v*N[1]]
def qb(u,v):return [A[0]+u*BT[0]+v*BN[0],A[1]+u*BT[1]+v*BN[1]]
def yaw(t):return -math.degrees(math.atan2(t[1],t[0]))
FY=yaw(T);BY=yaw(BT)
def bx(id,at,y,size,mat,group='Fixtures',angle=FY,src=None,collider=True):
 # Strict constructive clipping to accepted perimeter; never enlarge footprint.
 theta=-math.radians(angle);xt=(math.cos(theta),math.sin(theta));zt=(-xt[1],xt[0]);w,h,d=size
 corners=[[at[0]+i*w/2*xt[0]+j*d/2*zt[0],at[1]+i*w/2*xt[1]+j*d/2*zt[1]]for i,j in [(-1,-1),(1,-1),(1,1),(-1,1)]]
 geom=Polygon(corners).intersection(poly.buffer(-.002))
 if geom.is_empty:return
 if geom.area<Polygon(corners).area-.00001:
  extrude(id,geom,y-h/2,h,mat,group,src or ['NAVER_20240515'],collider)
 else:add(kind='box',id=id,group=group,q=at,y=y,size=size,yaw=angle,material=mat,collider=collider,source_ids=src or ['NAVER_20240515'])
def edge(id,s,e,bot,h,thick,mat,group='Booth',src=None,collider=True):
 mid=[(s[i]+e[i])/2 for i in range(2)];theta=-math.degrees(math.atan2(e[1]-s[1],e[0]-s[0]));bx(id,mid,bot+h/2,[math.dist(s,e),h,thick],mat,group,theta,src,collider)
def extrude(id,g,bottom,height,mat,group='Booth',src=None,collider=True):
 if g.geom_type=='MultiPolygon':
  for i,c in enumerate(g.geoms):extrude(id+'_'+str(i),c,bottom,height,mat,group,src,collider)
  return
 co=list(g.exterior.coords)[:-1]
 if len(co)<3:return
 # ordered convex shop/cabinet intersections; separate flat-face vertices to preserve hard edges.
 verts=[];tri=[]
 def face(vs):
  st=len(verts);verts.extend(vs)
  for k in range(1,len(vs)-1):tri.extend([st,st+k,st+k+1])
 # shapely exterior may clockwise; normalize ccw.
 signed=sum(co[i][0]*co[(i+1)%len(co)][1]-co[(i+1)%len(co)][0]*co[i][1] for i in range(len(co)))
 if signed<0:co=co[::-1]
 local=lambda xy,yy:[xy[0]-origin[0],yy,xy[1]-origin[1]]
 face([local(z,bottom) for z in co]);face([local(z,bottom+height) for z in co[::-1]])
 for i in range(len(co)):
  c,d=co[i],co[(i+1)%len(co)];face([local(c,bottom),local(c,bottom+height),local(d,bottom+height),local(d,bottom)])
 add(kind='mesh',id=id,group=group,q=list(origin),y=0,vertices=verts,triangles=tri,material=mat,collider=collider,source_ids=src or ['NAVER_20240515'])
def text(id,words,at,y,width,height,color=(1,1,1,1),angle=FY,group='Signs',src=None,qual=None):
 add(kind='text',id=id,group=group,q=at,y=y,yaw=angle,text=words,width=width,height=height,color=list(color),source_ids=src or ['NAVER_20240515'],qualification=qual or 'REBUILT_KOREAN_LETTERING_FROM_PHOTO_NOT_PHOTO_BOARD')
# Actual separate glass sheets, mullions, transom. Central sliding opening stays clear.
for tag,s,e in [('north',.055,1.08),('south',2.23,L-.055)]:
 edge('fixed_glass_'+tag,q(s,.045),q(e,.045),.07,2.23,.012,'glass',collider=True)
 for u in [s,e]:bx('aluminium_mullion_'+tag+'_'+str(round(u,3)),q(u,.05),1.18,[.042,2.36,.064],'aluminium','Booth')
 edge('glass_bottom_rail_'+tag,q(s,.05),q(e,.05),.03,.045,.064,'aluminium')
 edge('glass_top_rail_'+tag,q(s,.05),q(e,.05),2.28,.055,.064,'aluminium')
# Slider parked in front of fixed left panel, not spanning portal.
edge('sliding_leaf_parked_open',q(.10,.106),q(1.055,.106),.09,2.15,.01,'glass',collider=True)
for u in [.10,1.055]:bx('sliding_leaf_stile_'+str(u),q(u,.108),1.17,[.028,2.2,.024],'aluminium','Booth')
bx('sliding_door_motor_cover',q(L/2,.10),2.34,[L-.07,.13,.13],'aluminium','Booth')
bx('entry_motion_sensor',q(L/2,.18),2.39,[.14,.045,.055],'black','Booth',collider=False)
# Side sheets are independent and remain within accepted bay; native diagonal unmodified.
edge('north_side_glass',q(.055,.12),q(.055,3.17),.065,2.22,.012,'glass')
edge('south_side_glass',q(L-.055,.12),q(L-.055,1.29),.065,2.22,.012,'glass')
# Custom facade lettering. Header above shop only, no station entrance or ad wall occupied.
bx('charcoal_fascia',q(L/2,.095),2.61,[L-.07,.53,.12],'fascia','Booth')
text('primary_shop_name','부산 기장 특산품',q(L*.47,.028),2.60,L*.74,.27)
bx('red_seal_badge',q(L*.88,.024),2.61,[.22,.22,.018],'red','Signs')
text('red_badge_letters','명품',q(L*.88,.012),2.60,.17,.16)
# Own warm false ceiling follows accepted tapered cell.
extrude('local_warm_ceiling',poly.buffer(-.07),2.88,.065,'ceiling','Ceiling')
for i,(u,v) in enumerate([(.50,.55),(1.25,.55),(2.08,.52),(2.78,.50)]):
 bx('recessed_light_trim_'+str(i),q(u,v),2.864,[.16,.025,.13],'aluminium','Ceiling',collider=False)
 bx('warm_ceiling_lens_'+str(i),q(u,v),2.848,[.12,.015,.09],'light_lens','Ceiling',collider=False)
 if i%2==0:add(kind='light',id='warm_shop_light_'+str(i),group='Lighting',q=q(u,v),y=2.69,range=2.5,intensity=.3,color=[1,.87,.67,1])
# Source visible black track spotlights, overhead ventilation grille.
edge('black_front_light_track',q(.24,.40),q(L-.25,.40),2.72,.025,.03,'black','Ceiling',collider=False)
for i,u in enumerate([.52,1.48,2.55]):
 bx('track_light_housing_'+str(i),q(u,.40),2.64,[.085,.15,.095],'black','Lighting',collider=False)
 bx('track_light_lens_'+str(i),q(u,.42),2.555,[.06,.015,.06],'light_lens','Lighting',collider=False)
bx('ceiling_grille_dark_recess',q(1.50,1.26),2.856,[.64,.025,.31],'black','Ceiling',collider=False)
for i in range(8):bx('ceiling_grille_slat_'+str(i),q(1.22+i*.078,1.26),2.841,[.023,.016,.29],'aluminium','Ceiling',collider=False)
# Source observed warm wood three rear bays. Metric orientation follows native diagonal backface.
for bay,bc in enumerate([.70,1.87,3.02]):
 center=qb(bc,.24)
 bx(f'rear_bay_{bay}_backpanel',qb(bc,.075),1.23,[1.07,2.30,.045],'rack_back','RearDisplay',BY)
 for off in [-.55,.55]:bx(f'rear_bay_{bay}_wood_upright_{off}',qb(bc+off,.22),1.23,[.07,2.30,.38],'oak','RearDisplay',BY)
 for lev,z in enumerate([.14,.69,1.24,1.79,2.23]):bx(f'rear_bay_{bay}_shelf_{lev}',qb(bc,.25),z,[1.07,.035,.37],'shelf_dark','RearDisplay',BY)
 bx(f'rear_bay_{bay}_red_product_header',qb(bc,.28),2.34,[1.07,.20,.035],'red','RearDisplay',BY)
 text(f'rear_bay_{bay}_header_lettering','기장미역',qb(bc,.303),2.34,.76,.115,angle=BY,group='ProductSigns',src=['JHLIFE_156'])
# Pillow-shaped dry-goods bags. Source seaweed, kelp and carton pack families.
def pouch(id,at,bottom,w,h,d,mat,ang,label):
 # edge seams pinch; curved middle bellies, low polygon count. Individually editable packages.
 verts=[];tri=[]
 levels=[(0,.86,.38),(h*.10,1,.7),(h*.48,1.05,1),(h*.91,.94,.65),(h,.88,.27)]
 for yy,ww,dd in levels:
  for sx,sz in [(-1,-1),(1,-1),(1,1),(-1,1)]:verts.append([sx*w*ww/2,yy,sz*d*dd/2])
 for k in range(len(levels)-1):
  for j in range(4):a=k*4+j;b=k*4+(j+1)%4;c=(k+1)*4+(j+1)%4;e=(k+1)*4+j;tri.extend([a,b,c,a,c,e])
 tri.extend([0,2,1,0,3,2,16,17,18,16,18,19]);tri=[v for i in range(0,len(tri),3)for v in [tri[i],tri[i+2],tri[i+1]]]
 add(kind='mesh',id=id,group='PackagedSeaweed',q=at,y=bottom,yaw=ang,vertices=verts,triangles=tri,material=mat,collider=False,source_ids=['JHLIFE_156','NAVER_20231029'])
 # front is +Z of mesh, orient barcode/label towards customer.
 rad=math.radians(ang);normal=(math.sin(rad),math.cos(rad));lp=[at[i]+normal[i]*(d*.52+.002) for i in range(2)]
 bx(id+'_printed_label',lp,bottom+h*.49,[w*.88,h*.49,.006],'label_cream','ProductLabels',ang,['JHLIFE_156'],False)
 text(id+'_product_type',label,[lp[i]+normal[i]*.004 for i in range(2)],bottom+h*.54,w*.78,h*.20,(.06,.045,.035,1),ang,'ProductLabels',['JHLIFE_156'])
 bx(id+'_sealed_top',at,bottom+h-.015,[w*.88,.019,d*.28],'sealed_cream','PackagedSeaweed',ang,['JHLIFE_156'],False)
for bay,bc in enumerate([.70,1.87,3.02]):
 for level,z in enumerate([.18,.73,1.28,1.83]):
  for col in range(4):
   u=bc-.405+col*.27;v=.28
   label='기장미역' if bay!=2 or level%2==0 else '다시마'
   pouch(f'back_pack_B{bay}_L{level}_{col}',qb(u,v),z,.225,.40,.095,'pouch_cream' if (level+bay)%3 else 'seaweed_dark',BY,label)
# Front white illuminated sales-counter is placed back from portal, with south staff opening retained.
# Counter does not bridge full bay: source customer portal leads to front purchasing strip.
bx('front_sales_counter_body',q(1.37,.88),.48,[2.30,.90,.47],'counter_white','SalesCounter',src=['NAVER_20231029'])
bx('front_sales_counter_dark_top',q(1.37,.88),.955,[2.34,.055,.50],'shelf_dark','SalesCounter',src=['NAVER_20231029'])
bx('front_counter_backlit_face',q(1.37,.626),.51,[2.28,.79,.018],'backlit_white','SalesCounter',src=['NAVER_20231029'])
for i,label in enumerate(['기장미역','독도새우','은빛멸치']):
 u=.60+i*.76
 text('front_category_'+str(i),label,q(u,.61),.47,.63,.16,(.08,.09,.07,1),group='ProductSigns',src=['NAVER_20231029'])
 bx('front_footkick_'+str(i),q(u,.88),.06,[.74,.12,.41],'fascia','SalesCounter',src=['NAVER_20231029'])
for row in range(2):
 for col in range(9):
  u=.37+col*.245;v=.76+row*.18
  pouch(f'front_small_package_R{row}_{col}',q(u,v),.99,.20,.17,.06,'seaweed_dark' if col%3 else 'pouch_cream',FY,'특산품')
# Source observed brown giftboxes on shelving and counter.
for i in range(5):
 bx('counter_gift_carton_'+str(i),q(.43+i*.31,1.13),1.11,[.26,.28,.13],'gift_kraft','GiftBoxes',src=['JHLIFE_156','NAVER_20231029'],collider=False)
 text('counter_gift_carton_type_'+str(i),'기장미역',q(.43+i*.31,1.054),1.13,.23,.065,(.10,.055,.025,1),group='ProductLabels',src=['JHLIFE_156'])
# Small staff workspace/POS behind counter is explicitly inferred, not source-observed placement.
bx('staff_pos_shelf_MODEL',q(.43,1.43),.93,[.52,.08,.42],'oak','StaffCheckout_MODEL',src=['MODEL_LOGICAL_OPERATION'])
# Free Kenney source meshes reused via local mesh_ref extension; no existing asset mutation.
for i,part in enumerate(['0','1']):
 add(kind='mesh_ref',id='kenney_POS_screen_'+part,group='StaffCheckout_MODEL',q=q(.44,1.43),y=1.20,size=[.32,.28,.19],yaw=FY+180,material='black' if i==0 else 'screen_glow',mesh_asset=f'Assets/ChooGuard/Art/StationInterior/GlassShopFitoutC160_20261018/Meshes/kenney_furniture_computerScreen__{part}.asset',normalization_assets=[f'Assets/ChooGuard/Art/StationInterior/GlassShopFitoutC160_20261018/Meshes/kenney_furniture_computerScreen__{s}.asset'for s in ['0','1']],collider=False,source_ids=['FREE_KENNEY_CC0','MODEL_SOURCE_OCCLUDED_POS'],qualification='SOURCE_OCCLUDED_POS_WORKSTATION_LOCATION_INFERRED_MODEL')
for part in ['0','1']:
 add(kind='mesh_ref',id='kenney_POS_keyboard_'+part,group='StaffCheckout_MODEL',q=q(.44,1.27),y=1.00,size=[.27,.035,.10],yaw=FY+180,material='black',mesh_asset=f'Assets/ChooGuard/Art/StationInterior/GlassShopFitoutC160_20261018/Meshes/kenney_furniture_computerKeyboard__{part}.asset',normalization_assets=[f'Assets/ChooGuard/Art/StationInterior/GlassShopFitoutC160_20261018/Meshes/kenney_furniture_computerKeyboard__{s}.asset'for s in ['0','1']],collider=False,source_ids=['FREE_KENNEY_CC0','MODEL_SOURCE_OCCLUDED_POS'],qualification='SOURCE_OCCLUDED_POS_WORKSTATION_LOCATION_INFERRED_MODEL')
bx('payment_reader_MODEL',q(.72,1.11),1.03,[.10,.13,.07],'black','StaffCheckout_MODEL',src=['MODEL_LOGICAL_OPERATION'],collider=False)
materials={
 'glass':{'color':[.76,.85,.84,.12],'transparent':True,'smoothness':.96},
 'aluminium':{'color':[.29,.30,.28,1],'metallic':.70,'smoothness':.54},
 'fascia':{'color':[.21,.22,.20,1],'smoothness':.24},
 'black':{'color':[.025,.025,.022,1],'smoothness':.35},
 'red':{'color':[.54,.045,.024,1],'smoothness':.38},
 'oak':{'color':[.65,.45,.23,1],'smoothness':.32},
 'ceiling':{'color':[.77,.68,.49,1],'smoothness':.25},
 'rack_back':{'color':[.21,.18,.13,1],'smoothness':.25},
 'shelf_dark':{'color':[.075,.072,.057,1],'smoothness':.53},
 'light_lens':{'color':[1,.88,.62,1],'emission':[1.8,1.45,.81,1],'smoothness':.60},
 'pouch_cream':{'color':[.88,.84,.68,1],'smoothness':.79},
 'sealed_cream':{'color':[.89,.87,.78,1],'smoothness':.61},
 'seaweed_dark':{'color':[.075,.084,.035,1],'smoothness':.84},
 'label_cream':{'color':[.91,.88,.76,1],'smoothness':.35},
 'counter_white':{'color':[.83,.84,.80,1],'smoothness':.48},
 'backlit_white':{'color':[.85,.89,.87,1],'emission':[.33,.38,.36,1],'smoothness':.37},
 'gift_kraft':{'color':[.58,.44,.27,1],'smoothness':.26},
 'screen_glow':{'color':[.16,.30,.31,1],'emission':[.04,.10,.11,1],'smoothness':.8}
}
packet={'schema':'R25builder1','store_key':'gijang','brand':'부산 기장 특산품','origin_q':list(origin),'floor_world_y':7.00000048,'source_packet':str(P/'SOURCE-EVIDENCE-R25.json'),'approved_geometry_qx_minus_qy':a['geometry_qx_minus_qy'],'all_parts_separately_editable':True,'metric_qualification':'PHOTO_SHAPES_WITHIN_R24_FIXED_METRIC_MODEL_NOT_SURVEYED_ASBUILT','source_epoch':'2023and2024fieldimages;2026guideidentity','native_floors_columns_transport_doors_other_stores_unchanged':True,'materials':materials,'objects':objects}
(P/'partplacement.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2))
print('parts',len(objects),'meshes',sum(o['kind']=='mesh'for o in objects),'area',poly.area,'packetsha',hashlib.sha256((P/'partplacement.json').read_bytes()).hexdigest())
