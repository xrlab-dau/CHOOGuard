from pathlib import Path
import json,hashlib,math,sys,shutil,datetime
import numpy as np
from shapely.geometry import Polygon,box,Point,MultiPolygon
from shapely import constrained_delaunay_triangles
from shapely.ops import unary_union
from PIL import Image
B=Path(__file__).resolve().parent;R=B.parents[2];A='Assets/ChooGuard/Art/StationInterior/R25_Shops/olive-young';FM=B/'sources/free-models';ED=np.array([.999988073,-.004884]);EW=np.array([.004884,.999988073]);FD=-13.359280103;BD=-7.359280103;W0=16.27994947239285;W1=36.91990817706349;YAW=math.degrees(math.atan2(-ED[1],ED[0]));FRONT=YAW-90
origin=(ED*((FD+BD)/2)+EW*((W0+W1)/2)).tolist()
P={'store_key':'olive-young','origin_q':origin,'floor_world_y':7.0,'source_packet':str(B/'SOURCE-EVIDENCE-R25.json'),'source_imports':[],'materials':{},'mesh_library':{},'objects':[]}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def q(d,w):return [round(float(x),8)for x in ED*d+EW*w]
def mat(k,color,**kw):P['materials'][k]={'color':color,'smoothness':.42,**kw}
mat('white',[.86,.865,.84,1],metallic=.08,smoothness=.48);mat('charcoal',[.036,.045,.053,1],metallic=.33,smoothness=.54);mat('black',[.008,.01,.013,1],metallic=.24,smoothness=.55);mat('aluminium',[.43,.46,.48,1],metallic=.85,smoothness=.72);mat('ceiling',[.76,.755,.73,1],smoothness=.25);mat('floor_gray',[.29,.29,.275,1],smoothness=.32);mat('floor_joint',[.16,.17,.17,1],smoothness=.2);mat('glass',[.68,.80,.82,.065],transparent=True,smoothness=.96);mat('shelf_glass',[.7,.82,.84,.08],transparent=True,smoothness=.92);mat('glow',[.92,.94,.91,1],emission=[1.2,1.25,1.18,1],smoothness=.6);mat('sign_white',[.91,.95,.95,1],emission=[.32,.39,.39,1]);mat('olive_green',[.48,.79,.025,1],emission=[.22,.4,.01,1]);mat('seed_red',[.86,.14,.17,1]);mat('chrome',[.65,.67,.69,1],metallic=.98,smoothness=.9);mat('warm_gray',[.42,.43,.40,1],smoothness=.23);mat('orange',[.94,.24,.015,1],smoothness=.5);mat('pink',[.79,.49,.57,1]);mat('gold',[.7,.47,.14,1],metallic=.8,smoothness=.75);mat('lip_red',[.54,.012,.04,1],smoothness=.65);mat('mirror',[.50,.55,.59,1],metallic=1,smoothness=.97);mat('display_blue',[.12,.28,.40,1],emission=[.08,.2,.28,1]);mat('label_white',[.8,.81,.79,1],smoothness=.3)
colors=[[.82,.86,.84,1],[.47,.71,.66,1],[.75,.46,.59,1],[.20,.51,.65,1],[.87,.79,.48,1],[.89,.88,.85,1]]
for i,c in enumerate(colors):mat('product_'+str(i),c,smoothness=.58)

def imp(src,dest):
 src=Path(src);P['source_imports'].append({'source':str(src.resolve()),'destination':dest,'sha256':sha(src)});return A+'/'+dest
models={}
for key,file in [('shelf','SingleFace-Shelf.fbx'),('counter','Checkout-Counter.fbx'),('pos','POS-Terminal.fbx'),('basket','shoppingbasket.fbx'),('tube','tube-Own.fbx'),('pump','pump-Own.fbx'),('lipstick','lipstick-Own.fbx'),('makeup','makeup-Own.fbx'),('foil-pouch','foil-pouch-Own.fbx')]:models[key]=imp(FM/key/file,'Models/'+key+'.fbx')
# Actual source PBR maps: each imported material receives an explicit own URP binding.
base=imp(FM/'pos/textures/Image_0.png','Textures/pos_base.png');normal=imp(FM/'pos/textures/Image_2.png','Textures/pos_normal.png');ms=imp(FM/'pos/textures/own_metallic_smoothness.png','Textures/pos_metallic_smoothness.png');normal2=imp(FM/'pos/textures/Image_3.png','Textures/pos_second_normal.png')
mat('pos_main',[1,1,1,1],texture_asset=base,normal_asset=normal,metallic_gloss_asset=ms,smoothness=.7);mat('pos_secondary',[.12,.14,.15,1],normal_asset=normal2,metallic=.2,smoothness=.6)
pb=imp(FM/'pump/textures/Image_0.png','Textures/pump_base.png');pn=imp(FM/'pump/textures/Image_2.png','Textures/pump_normal.png');pm=imp(FM/'pump/textures/own_metallic_smoothness.png','Textures/pump_metallic_smoothness.png');mat('source_sanitizer',[1,1,1,1],texture_asset=pb,normal_asset=pn,metallic_gloss_asset=pm)
for i in range(6):P['materials']['product_'+str(i)]['normal_asset']=pn
for n,slot in [(0,'holder'),(1,'red'),(2,'cap')]:
 t=imp(FM/f'lipstick/textures/Image_{n}.png',f'Textures/lipstick_{slot}.png');mat('lip_'+slot,[1,1,1,1],texture_asset=t,metallic=.65 if n!=1 else 0,smoothness=.65)
taxtex=imp(B/'sources/source-print/tax-refund-screen-2026.png','Textures/tax_refund_actual_screen.png');mat('kiosk_actual_UI',[1,1,1,1],texture_asset=taxtex,emission=[.25,.25,.25,1])
pouchtex=imp(B/'sources/source-print/health-pouch-print.png','Textures/source_health_pouch_print.png');mat('health_print',[1,1,1,1],texture_asset=pouchtex,smoothness=.45)
# Compact attribution is imported along with source assets.
licenses=[{'model':k,'source_immutable':True,'staged':str(FM/k)}for k in models]
(B/'SOURCE-MODEL-ATTRIBUTION.json').write_text(json.dumps({'licenses':licenses,'all_external_sources':'CC-BY-4.0; exact author/URL/license in SOURCE-EVIDENCE-R25.json','source_models_are_generic_shape_prototypes_not_exact_branch_SKU':True},indent=2))
imp(B/'SOURCE-MODEL-ATTRIBUTION.json','SOURCE-MODEL-ATTRIBUTION.json')

C=json.load(open(B/'LOCAL-ACTUAL-CAD-REFERENCE-R25.json'));columns=[]
for o in C['objects']:
 if not any(x in o['path']for x in ['NATIVE_COL_003','NATIVE_COL_010','NATIVE_COL_014']):continue
 pts=np.array([p for seg in o['sections']['8.40000']for p in seg]);v=np.column_stack((pts@ED,pts@EW));poly=Polygon(v).convex_hull.buffer(.025);columns.append((o['object_id'],poly,o['path']))
col_union=unary_union([p for _,p,_ in columns]);foot=box(FD,W0,BD,W1);clear=foot.difference(col_union)
# Construction mesh topology, including native-column apertures, is generated directly from actual vector bodies.
def extrusion_dw(poly,height):
 verts=[];tri=[]
 def vert(p,y):z=ED*p[0]+EW*p[1];return [round(float(z[0]),7),y,round(float(z[1]),7)]
 def t(a,b,c):i=len(verts);verts.extend([a,b,c]);tri.extend([i,i+1,i+2])
 polys=[poly]if poly.geom_type=='Polygon'else list(poly.geoms)
 for p in polys:
  for tr in constrained_delaunay_triangles(p).geoms:
   a,b,c=list(tr.exterior.coords)[:3]
   if np.cross(np.array(b)-a,np.array(c)-a)>0:b,c=c,b
   t(vert(a,height),vert(b,height),vert(c,height));t(vert(c,0),vert(b,0),vert(a,0))
  for ring in [p.exterior,*p.interiors]:
   for a,b in zip(list(ring.coords),list(ring.coords)[1:]):t(vert(a,0),vert(b,0),vert(b,height));t(vert(a,0),vert(b,height),vert(a,height))
 return {'vertices':verts,'triangles':tri}
def add(kind,id,group,source='WONDEOK_20260425',**kw):
 ob={'kind':kind,'id':id,'group':group,'source_ids':[source],'qualification':'SOURCE_PHOTO_AND_ACTUAL_NATIVE_VECTOR_BOUND_MODEL_NOT_SURVEYED','collider':False,**kw};P['objects'].append(ob);return ob
def boxo(id,d,w,y,ds,ws,h,material,group='PhysicalFinish',**kw):return add('box',id,group,q=q(d,w),y=y,size=[ds,h,ws],yaw=YAW,material=material,**kw)
def edge(id,a,b,bottom,height,thick,material,group='Booth',**kw):return add('edge',id,group,a=q(*a),b=q(*b),bottom=bottom,height=height,thickness=thick,material=material,**kw)
def asset(id,key,d,w,y,fit,yaw,mp,group='DownloadedPhysicalFixtures',**kw):return add('asset',id,group,q=q(d,w),y=y,fit_size=fit,yaw=yaw,asset=models[key],material_map=mp,**kw)
def solid(id,m,d,w,y,yaw,material,group='PhysicalSigns',**kw):return add('mesh',id,group,q=q(d,w),y=y,yaw=yaw,vertices=m['vertices'],triangles=m['triangles'],material=material,**kw)

# Thin source gray finish and suspended ceiling remain separate editable bodies, with actual column holes. Mainfloor untouched.
for id,y,h,ma in [('gray_store_finish',.012,.008,'floor_gray'),('source_suspended_ceiling',3.18,.065,'ceiling')]:
 m=extrusion_dw(clear,h);add('mesh',id,'SourceVisibleFinish',q=[0,0],y=y,vertices=m['vertices'],triangles=m['triangles'],material=ma)
for w in np.arange(W0+.6,W1,.6):
 for p in [box(FD+.015,w-.0009,BD-.015,w+.0009).difference(col_union)]:
  if p.is_empty:continue
  m=extrusion_dw(p,.0005);add('mesh','tile_joint_w_'+str(round(w,3)),'GrayFloorFinishJoints',q=[0,0],y=.0202,vertices=m['vertices'],triangles=m['triangles'],material='floor_joint')
for d in np.arange(FD+.6,BD,.6):
 m=extrusion_dw(box(d-.0009,W0+.01,d+.0009,W1-.01).difference(col_union),.0005);add('mesh','tile_joint_d_'+str(round(d,3)),'GrayFloorFinishJoints',q=[0,0],y=.0202,vertices=m['vertices'],triangles=m['triangles'],material='floor_joint')
# One new shop shell only; native columns and native service wall remain fixed. The rear public strip is outside all own objects.
E0,E1=32.2,34.6
for i,(a,b)in enumerate([(W0+.04,E0),(E1,W1-.04)]):
 edge('front_clear_glazing_'+str(i),(FD+.028,a),(FD+.028,b),.12,3.0,.012,'glass',collider=True)
 edge('front_lower_rail_'+str(i),(FD+.015,a),(FD+.015,b),.015,.11,.055,'charcoal')
 edge('front_upper_rail_'+str(i),(FD+.015,a),(FD+.015,b),3.08,.115,.065,'charcoal')
 for j,w in enumerate(np.arange(a,b,.98)):
  boxo(f'front_mullion_{i}_{j}',FD+.02,w,1.62,.05,.036,3.08,'aluminium')
# Rear glazing separates shop from public service approach, with source-visible open terminal approach gap.
for i,(a,b) in enumerate([(W0+.08,30.95),(34.6,W1-.08)]):
 edge('rear_clear_glazing_'+str(i),(BD-.018,a),(BD-.018,b),.08,3.04,.012,'glass',collider=True)
 edge('rear_header_'+str(i),(BD-.025,a),(BD-.025,b),3.08,.10,.06,'charcoal')
 for j,w in enumerate(np.arange(a,b,1.42)):boxo(f'rear_mullion_{i}_{j}',BD-.025,w,1.6,.04,.04,3.12,'aluminium')
# One source-visible gray checkout end. The opposite short end stays glazed.
edge('checkout_gray_end_main',(FD+.04,W0+.035),(BD-1.18,W0+.035),.02,3.15,.065,'warm_gray',collider=True)
edge('checkout_gray_end_return',(BD-.32,W0+.035),(BD-.04,W0+.035),.02,3.15,.065,'warm_gray',collider=True)
edge('checkout_staff_door_overhead',(BD-1.18,W0+.035),(BD-.32,W0+.035),2.21,.96,.065,'warm_gray',collider=True)
edge('opposite_end_glass',(FD+.04,W1-.035),(BD-.04,W1-.035),.10,3.05,.012,'glass',collider=True)
edge('long_front_crown',(FD+.012,W0),(FD+.012,W1),3.18,.20,.10,'charcoal')
boxo('entrance_charcoal_header',FD+.012,(E0+E1)/2,2.915,.12,4.15,.59,'charcoal','ActualSourceEntrance',collider=True)
for k,w in [('a',E0),('b',E1)]:boxo('entrance_jamb_'+k,FD+.03,w,1.40,.105,.075,2.80,'charcoal','ActualSourceEntrance',collider=True)
# Open/retracted door panels leave full2.4m source-inspired entry clear.
for i,w in enumerate([E0-.56,E1+.56]):
 edge('retracted_glass_leaf_'+str(i),(FD+.085,w-.52),(FD+.085,w+.52),.12,2.60,.012,'glass','ActualSourceEntrance')
 for j,v in enumerate([w-.52,w+.52]):boxo(f'door_leaf_stile_{i}_{j}',FD+.085,v,1.42,.04,.035,2.60,'charcoal','ActualSourceEntrance')
 boxo('leaf_pull_'+str(i),FD+.11,w,1.06,.045,.035,.30,'aluminium','ActualSourceEntrance')
logos=json.load(open(B/'sign-model-authoring/SOLID-LOGO-MESHES.json'))
for key,ma in [('white','sign_white'),('green','olive_green'),('red','seed_red')]:solid('actual_photo_closed_logo_'+key,logos[key],FD-.061,(E0+E1)/2,2.915,FRONT,ma)
# Metal sign standoffs are individually editable.
for w in [31.66,35.14]:boxo('sign_standoff_'+str(w),FD+.045,w,2.915,.13,.04,.04,'aluminium','PhysicalSigns')

# Photo-visible white enamel source shelving: actual downloaded perforated uprights, shelves and adjustable feet.
shelf_map={'mat_shelf':'white','mat_shelf_glass':'shelf_glass','*':'white'};black_shelf={'mat_shelf':'black','mat_shelf_glass':'shelf_glass','*':'black'}
shelf_sites=[]
for w in np.arange(W0+.75,31.7,1.1):shelf_sites.append(('front',FD+.35,float(w),1.08,1.45,YAW+90))
for w in [35.30,36.38]:shelf_sites.append(('front',FD+.35,w,.96,1.45,YAW+90))
for w in np.arange(W0+2.25,30.55,1.06):shelf_sites.append(('back',BD-.34,float(w),1.04,1.85,FRONT))
for w in [35.3,36.3]:shelf_sites.append(('back',BD-.34,w,.95,1.85,FRONT))
# White double-sided central gondola segments, interrupted for entry cross-aisles and source-column access.
for w in [19.25,20.35,23.20,24.3,25.4,26.5,27.6,28.7]:
 for face,dd,yaw in [('a',-10.44,FRONT),('b',-10.88,YAW+90)]:shelf_sites.append(('middle_'+face,dd,w,1.04,1.55,yaw))
# Physical product forms: no labels/flat merchandise proxies. Generic shape/paint variants are not exact SKU inventory.
product_count=0
for n,(row,d,w,width,height,yaw) in enumerate(shelf_sites):
 footprint=box(d-.24,w-width/2,d+.24,w+width/2)
 if footprint.intersection(col_union).area>.001:continue
 ma=black_shelf if row=='middle_a'and 23<w<28 else shelf_map
 asset(f'downloaded_perforated_shelf_{n}','shelf',d,w,.035,[width,height,.43],yaw,ma,collider=True)
 # Separate photographic lightbox and under-shelf LED details, without type/font standins.
 if row=='back':
  boxo(f'illuminated_header_body_{n}',d,w,height+.13,.13,width,.25,'white','SourceLitHeaders')
  boxo(f'illuminated_header_face_{n}',d-.08,w,height+.13,.014,width-.035,.21,'glow','SourceLitHeaders')
  for y in [.32,.67,1.02,1.37]:boxo(f'shelf_LED_{n}_{y}',d-.19,w,y+.02,.018,width-.05,.015,'glow','ShelfLightStrips')
 # Four source-model merchandise tiers at exact source shelf deck heights; one middle tier left clear as observed variable stocking.
 ys=[.040+z*height/1.3733035326 for z in [.1801784,.4076713,.6476713,1.1276714]]
 for tier,y in enumerate(ys):
  for k in range(6):
   ww=w-width*.40+k*width*.16;dd=d+(.12 if yaw==YAW+90 else -.12);variant=(n+tier+k)%6
   if ma is black_shelf:
    if tier%2==0:
     key='lipstick';fit=[.034,.115,.075];mp={'Material_31':'lip_holder','Material_29':'lip_red','Material_30':'lip_cap','*':'black'}
    else:key='makeup';fit=[.10,.10,.105];mp={'Black':'black','blinn5':'lip_red','Iron':'chrome','lambert1':'warm_gray','Wood':'gold','Blush':'pink','Crystal':'mirror','*':'black'}
   elif tier==0 and n%4==0:
    key='foil-pouch';fit=[.09,.16,.025];mp={'*':'product_'+str(variant)}
   elif(tier+k+n)%4==0:
    key='tube';fit=[.052,.14,.035];mp={'*':'product_'+str(variant)}
   else:
    key='pump';fit=[.055,.17 if row!='front'else.19,.055];mp={'*':'source_sanitizer'if row=='front'and n%7==0 else'product_'+str(variant)}
   asset(f'physical_merchandise_{n}_{tier}_{k}',key,dd,ww,y,fit,yaw,mp,'PhysicalMerchandise',collider=False)
   product_count+=1
   # Real electronic paper shelf-label casing and inset display, separate hardware from shelf/product geometry.
   if k%2==0:
    facing=-1 if yaw==FRONT else 1;boxo(f'electronic_label_case_{n}_{tier}_{k}',d+facing*.224,ww,y-.04,.012,.105,.049,'label_white','ElectronicPriceLabelHardware')
    boxo(f'electronic_ink_display_{n}_{tier}_{k}',d+facing*.231,ww,y-.04,.0015,.087,.034,'warm_gray','ElectronicPriceLabelHardware')
# One actual source interior makep:rem thick outline header (printed-source plane is not a sign substitute).
pm=json.load(open(B/'sign-model-authoring/PREM-INTERIOR-SOLID-MESH.json'));solid('source_prem_closed_brand_outline',pm,BD-.425,24.25,2.00,FRONT,'black','SourceLitHeaders')
# Source poster is not used as a whole kiosk. Physical equipment has molded body, plinth, screen recess, scanner and slots.
def extrusion_xy(poly,depth):
 verts=[];tris=[]
 def t(a,b,c):i=len(verts);verts.extend([a,b,c]);tris.extend([i,i+1,i+2])
 polys=[poly]if poly.geom_type=='Polygon'else list(poly.geoms)
 for po in polys:
  for tr in constrained_delaunay_triangles(po).geoms:
   a,b,c=list(tr.exterior.coords)[:3]
   if np.cross(np.array(b)-a,np.array(c)-a)<0:b,c=c,b
   t([*a,0],[*b,0],[*c,0]);t([*c,-depth],[*b,-depth],[*a,-depth])
  for ring in [po.exterior,*po.interiors]:
   for a,b in zip(list(ring.coords),list(ring.coords)[1:]):t([*a,-depth],[*b,-depth],[*b,0]);t([*a,-depth],[*b,0],[*a,0])
 return {'vertices':verts,'triangles':tris}
def rounded(w,h,r):return box(-w/2+r,-h/2+r,w/2-r,h/2-r).buffer(r,quad_segs=6)
kd,kw=-8.18,31.15
solid('tax_kiosk_molded_white_head',extrusion_xy(rounded(.49,.99,.055),.13),kd,kw,1.16,FRONT,'white','ActualTaxRefundKiosk')
solid('tax_kiosk_screen_bezel',extrusion_xy(rounded(.408,.66,.025),.025),kd-.073,kw,1.28,FRONT,'black','ActualTaxRefundKiosk')
# Screen UI is authentic on the physical recessed display only.
v=[[-.185,-.302,.001],[.185,-.302,.001],[.185,.302,.001],[-.185,.302,.001]]
add('mesh','tax_kiosk_actual_display_surface','ActualTaxRefundKiosk',q=q(kd-.102,kw),y=1.28,yaw=FRONT,vertices=v,triangles=[0,1,2,0,2,3],uv=[[0,0],[1,0],[1,1],[0,1]],material='kiosk_actual_UI')
solid('tax_kiosk_recessed_scanner',extrusion_xy(rounded(.21,.09,.016),.035),kd-.085,kw-.105,.84,FRONT,'black','ActualTaxRefundKiosk')
solid('tax_kiosk_receipt_slot',extrusion_xy(rounded(.14,.017,.005),.025),kd-.09,kw+.12,.88,FRONT,'black','ActualTaxRefundKiosk')
solid('tax_kiosk_lower_cardreader',extrusion_xy(rounded(.078,.11,.012),.025),kd-.10,kw+.13,.73,FRONT,'charcoal','ActualTaxRefundKiosk')
boxo('tax_kiosk_pedestal',kd+.065,kw,.38,.18,.22,.70,'white','ActualTaxRefundKiosk')
# Foot is rounded polygon oriented on floor, separate from pedestal and upper enclosure.
footmesh=extrusion_dw(rounded(.48,.49,.09),.055)
solid('tax_kiosk_weighted_round_foot',footmesh,kd+.07,kw,.025,0,'white','ActualTaxRefundKiosk')
boxo('tax_kiosk_orange_top_trim',kd-.069,kw,1.62,.018,.36,.026,'orange','ActualTaxRefundKiosk')
# Checkout source cabinet adapted with own URP material, physical downloaded POS and source-visible end staff door.
asset('downloaded_checkout_cabinet','counter',-10.01,W0+1.07,.025,[3.25,.88,.65],YAW,{'*':'white'},'Checkout',collider=True)
boxo('checkout_countertop',-10.01,W0+1.07,.925,3.32,.71,.065,'white','Checkout')
for i,d in enumerate([-10.98,-9.94,-8.92]):asset('downloaded_POS_'+str(i),'pos',d,W0+1.0,.96,[.32,.35,.36],YAW+180,{'Material_2100454184':'pos_main','Material_2100454185':'pos_secondary','*':'pos_secondary'},'Checkout')
for i,d in enumerate([-10.9,-9.95,-8.98]):
 boxo('checkout_digital_screen_frame_'+str(i),d,W0+.10,1.99,.88,.07,.52,'charcoal','CheckoutScreens')
 boxo('checkout_digital_screen_'+str(i),d,W0+.14,1.99,.82,.012,.46,'display_blue','CheckoutScreens')
for i in range(4):asset('downloaded_customer_basket_'+str(i),'basket',-12.45,31.63,.03+i*.08,[.40,.24,.30],YAW,{'*':'charcoal'},'EntryBasketStack')
# The source photograph shows STAFF ONLY door; only its visible closed leaf/frame, no hidden-room reconstruction.
boxo('photo_staff_door_leaf',BD-.75,W0+.087,1.085,.77,.028,2.10,'charcoal','VisibleStaffDoor',collider=True)
for i,d in enumerate([BD-1.17,BD-.33]):boxo('staff_door_frame_'+str(i),d,W0+.09,1.10,.055,.055,2.20,'black','VisibleStaffDoor')
boxo('staff_door_lintel',BD-.75,W0+.09,2.18,.89,.055,.055,'black','VisibleStaffDoor');boxo('staff_door_handle',BD-.46,W0+.116,1.01,.025,.035,.18,'aluminium','VisibleStaffDoor')
# Real source ceiling strips and downlights; the original native body excludes new fixture geometry.
for j,w in enumerate(np.arange(W0+.65,W1-.3,1.05)):
 parts=box(FD+.38,w-.018,BD-.39,w+.018).difference(col_union)
 for k,po in enumerate([parts]if parts.geom_type=='Polygon'else list(parts.geoms)):
  if po.is_empty:continue
  m=extrusion_dw(po,.018);add('mesh',f'linear_light_diffuser_{j}_{k}','SourceCeilingLighting',q=[0,0],y=3.142,vertices=m['vertices'],triangles=m['triangles'],material='glow')
for i,d in enumerate([FD+.27,BD-.27]):
 for j,w in enumerate(np.arange(W0+.8,W1-.25,1.55)):
  if col_union.distance(Point(d,w))<.1:continue
  add('cylinder',f'recessed_downlight_frame_{i}_{j}','SourceCeilingLighting',q=q(d,w),y=3.155,size=[.125,.009,.125],material='aluminium')
  add('cylinder',f'recessed_downlight_diffuser_{i}_{j}','SourceCeilingLighting',q=q(d,w),y=3.143,size=[.104,.006,.104],material='glow')
# Observed white ceiling cassette with actual separate grille/louvres.
for z,w in enumerate([W0+3.7,W0+12.8]):
 boxo('cassette_outer_frame_'+str(z),-9.4,w,3.14,.77,.77,.075,'white','SourceCeilingEquipment')
 boxo('cassette_recessed_grille_'+str(z),-9.4,w,3.09,.50,.50,.025,'charcoal','SourceCeilingEquipment')
 for j in range(10):boxo(f'cassette_grille_blade_{z}_{j}',-9.62+j*.049,w,3.071,.019,.47,.014,'warm_gray','SourceCeilingEquipment')
 for i,dd in enumerate([-9.735,-9.065]):boxo(f'cassette_outer_louvre_{z}_{i}',dd,w,3.08,.065,.60,.025,'white','SourceCeilingEquipment')
# Actual-source anti-theft antenna frames, leaving source entrance clear between them.
outer=rounded(.32,1.31,.07);inner=rounded(.23,1.16,.06);antenna=extrusion_xy(outer.difference(inner),.046)
for i,w in enumerate([E0+.06,E1-.06]):solid('entry_security_antenna_'+str(i),antenna,FD+.38,w,.72,YAW,'white','EntrySecurityHardware')
# Stand-alone physical makeup tester mirrors and trays supplement source asset clusters.
for i,w in enumerate([23.2,25.4,27.6]):
 boxo('tester_platform_'+str(i),-10.4,w,1.58,.40,.86,.04,'black','MakeupTesterDetails')
 for j in range(5):asset(f'open_lipstick_tester_{i}_{j}','lipstick',-10.50,w-.3+j*.15,1.60,[.035,.11,.064],FRONT,{'Material_31':'lip_holder','Material_29':'lip_red','Material_30':'lip_cap','*':'black'},'MakeupTesterDetails')
 # Small round source-form chrome vanity mirror, separate glass/chrome/back stand.
 add('cylinder','tester_mirror_base_'+str(i),'MakeupTesterDetails',q=q(-10.3,w),y=1.63,size=[.12,.012,.12],material='chrome')
 boxo('tester_mirror_stem_'+str(i),-10.3,w,1.76,.018,.018,.24,'chrome','MakeupTesterDetails')
 add('cylinder','tester_mirror_face_'+str(i),'MakeupTesterDetails',q=q(-10.3,w),y=1.93,size=[.18,.005,.18],euler=[90,FRONT,0],material='mirror')

# Authoring basis is saved before application. These are bounded MODEL metric fits, never field measurement.
basis={'status':'SOURCE_RELATIONSHIP_TO_ACTUAL_NATIVE_FACES_MODEL','coordinate_system':'d=eD dot q,w=eW dot q;q=[qx,-qy]metres','depth_axis_q':ED.tolist(),'width_axis_q':EW.tolist(),'front_d':FD,'rear_d':BD,'side_w':[W0,W1],'footprint_q':[q(FD,W0),q(FD,W1),q(BD,W1),q(BD,W0),q(FD,W0)],'anchors':[{'object_id':o['object_id'],'path':o['path'],'actual_level':'8.40000'}for o in C['objects']if any(x in o['path']for x in ['FOOTPRINT-027','FOOTPRINT-036','NATIVE_COL_003','NATIVE_COL_010','NATIVE_COL_014'])],'front_rear_line_evidence':'Original2026 SITE4 shows Railway/Starlight/Olive collinear front/rear; migrate exact Railway applied frontage/rear line using actual native row. Railway33sqm/width not transferred. Starlight own saved matching line supplied independently.','side_line_evidence':'wall027 actual outerface maxw16.279949472;wall036 actual innerface minw36.919908177 extension to common facade. No source box extent.','existing_native_shell_policy':'No duplicate native walls; own source booth finish/glass inside vacant retail row, native service walls/doorframes fixed.','public_service_strip_clear_d':[BD,-4.2243],'neighbor_starlight_min_w':39.536484338,'neighbor_gap_m':39.536484338-W1,'native_columns_preserved':[{'object_id':i,'path':p,'exclusion_polygon_dw':list(poly.exterior.coords)}for i,poly,p in columns],'ceiling_and_floor_finish_column_holes_from_actual_native_bodies':True,'entry_w_MODEL':[E0,E1],'measured_branch_dimensions':False,'source_pixel_metric_or_R21_R22_box_used':False,'qualification':'All absolute tenant binding/fixture positions/finishes fit are MODEL. Source plan cropped at Olive area: no full-width drawing found. Structural native and public access fixed.'}
(B/'MODEL-PLACEMENT-BASIS-R25.json').write_text(json.dumps(basis,ensure_ascii=False,indent=2))
# Actual source evidence and licenses are retained in own namespace and runtime attribution text.
photo=[]
for post,files in [('ROOT-wondeok9-224257999622',[0,1,3,4,6,9,12,15]),('ROOT-beautyjujuju-223741152515',[3,7,15,17,19,24])]:
 idx=json.load(open(B/f'sources/{post}-INDEX.json'))
 photo.append({'id':'WONDEOK_20260425'if'wondeok'in post else'BEAUTY_20250128','url':idx.get('url'),'date':idx.get('source_date'),'exact_branch':'Olive Young Busan Station History branch,206 Jungang-daero,inside2F; excludes197outside/남포타운','full_images_directly_viewed':[idx['images'][i]['path']for i in files],'source_epoch_priority':'2026white kiosk supersedes2025orange kiosk; 2025 product shape details corroborate visible categories only','photo_copyright':'Author retains copyright; evidence/contour tracing, genuine cropped device/product print only; no whole sign/merchandise proxy planes'})
free=[]
for k in ['tube','pump','lipstick','makeup','foil-pouch']:
 d=json.load(open(FM/k/'SKETCHFAB-SOURCE.json'));free.append({'key':k,'name':d['name'],'author':d['user']['displayName'],'url':d['viewerUrl'],'license':d['license'],'source_preview_directly_viewed':True,'local_original':str(FM/k/(k+'.glb')),'sha256_original':sha(FM/k/(k+'.glb')),'own_runtime_derivative':models[k],'modifications':'Own derivative with source geometry/material slots, local fit and photo paint; original immutable. Tube/foil source subdivision derivative only. Exact SKU not established.'})
free.extend([{'key':'shelf','name':'Store Shelves','author':'Peter_D','url':'https://sketchfab.com/3d-models/store-shelves-8406794ee19b4af49e0ccc02d1132915','license':'CC-BY-4.0','original_own_staged_copy':str(FM/'shelf/store-shelves.glb'),'preview_directly_viewed':True,'derivation':'Actual source five-tier one-face perforated module, photo-fitted independent objects, own URP paint/glass slots.'},{'key':'pos','name':'POS terminal','author':'Vladyslav.Yarovyi','url':'https://sketchfab.com/3d-models/pos-terminal-91c277a25bc34c5bbd080cd1e74161cb','license':'CC-BY-4.0','PBR':'Own base/normal/metallic-smoothness exact per-source slots, originals preserved.'},{'key':'counter','name':'Industrial Kitchen - Bun Counter','author':'siyarasm','url':'https://sketchfab.com/3d-models/industrial-kitchen-bun-counter-50a9dc05104048ffba83eca72a003873','license':'CC-BY-4.0','changes':'Own copy lower straight cabinet only; source-visible checkout envelope fitted, own URP.'},{'key':'basket','name':'Shopping Basket','author':'laper','url':'https://sketchfab.com/3d-models/shopping-basket-e75663bc485a44198f30d8ec52c42a73','license':'CC-BY-4.0','changes':'Independent source copy with photo gray plastic material; no source edits.'}])
limits=['Exact branch full floor/ceiling/elevation/furniture measured design not located. Railway original SITE4 only supplies adjacent collinearity, not full Olive width.','Absolute source-to-native tenant binding and all fixture dimensions are MODEL from actual native axes and source-visible relationships, not a survey.','Exact SKU inventory, source-occluded hidden BOH, electrical/plumbing/circulation requirements not established. No invented hidden-room interior.','2026white kiosk selected;2025orange apparatus not replayed. Kiosk is explicit photo-built physical body/screens/slots/base, not a photo-plane equipment substitute.','Main floor structure/native bodies/public entries/other shops remain fixed. User owns validation. No QA/Play/probes/reopen/captures.','No whole2F/99percent/digital-twin-complete claim.']
evidence={'store_key':'olive-young','utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'source_photos':photo,'original_design_search':{'queries':['올리브영 부산역사점 평','올리브영 부산역사점 도면','original2026railway architecture SITE4'],'result':'No exact branch full furniture design located; adjacent original SITE4 directly viewed for native row relationship.','directly_viewed_original_site':str(B/'sources/RAILWAY-ORIGINAL-SITE-P04.png'),'source_pdf':'.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/root/2026-goods-shop-architecture-original.pdf','page_0based':4},'free_model_sources':free,'solid_sign_geometry':str(B/'sign-model-authoring/SOURCE-SIGN-PROVENANCE.json'),'materials':'v4 explicit own URP material_map on every downloaded model; exact original slots retained, source PBR normal/MR converted only in own staging.','source_shape_models_count':len(models),'physical_product_instances':product_count,'limitations':limits}
(B/'SOURCE-EVIDENCE-R25.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2))
# Include full attribution, not just pointer, inside own runtime folder.
attr=B/'SOURCE-MODEL-ATTRIBUTION.json';attr.write_text(json.dumps({'store_key':'olive-young','free_models':free,'source_photos':photo,'modifications_not_observed_asbuilt':limits},ensure_ascii=False,indent=2))
for x in P['source_imports']:
 if x['destination']=='SOURCE-MODEL-ATTRIBUTION.json':x['sha256']=sha(attr)
packet=B/'STORE-PACKET-R25.json';packet.write_text(json.dumps(P,ensure_ascii=False,separators=(',',':')))
scope='C180_R25_OLIVE_YOUNG_APPLY_001';template=(B.parent/'StoreBuilderR25-v4-SOURCE-MATERIALS.template.cs').read_text()
for a,v in {'__STORE_KEY__':'olive-young','__WORKER_THREAD__':'classic:/root/r25_olive_young','__EXACT_SCOPE__':scope,'__PACKET_PATH__':str(packet),'__PACKET_SHA256__':sha(packet)}.items():template=template.replace(a,v)
(B/'ApplyStoreR25.cs').write_text(template)
print(json.dumps({'objects':len(P['objects']),'downloaded_asset_instances':sum(x['kind']=='asset'for x in P['objects']),'physical_products':product_count,'source_models':len(models),'materials':len(P['materials']),'imports':len(P['source_imports']),'packet_sha256':sha(packet),'footprint_q':basis['footprint_q']},ensure_ascii=False))
