import pathlib,json,math,hashlib,shutil,importlib.util
import numpy as np,cv2
from shapely.geometry import Polygon,Point,box
from shapely import constrained_delaunay_triangles
from shapely.geometry.polygon import orient
P=pathlib.Path(__file__).resolve().parent;C=P.parent;R=C.parents[1];S=P/'sources';A='Assets/ChooGuard/Art/StationInterior/R25_Shops/pascucci'
u=np.array([.999988074,-.004883823]);v=np.array([.004883823,.999988074]);origin=np.array([-36.700906,86.762281]);yaw=math.degrees(-math.atan2(u[1],u[0]));q=lambda x,z:[round(float(k),6) for k in origin+u*x+v*z]
local=json.loads((P/'LOCAL-ACTUAL-CAD-REFERENCE-R25.json').read_text());columns=[]
for o in local['objects']:
 if 'NATIVE_COL_' in o['path']:
  pts=np.array([p for seg in o['sections']['7.15000'] for p in seg]);cen=(pts.min(0)+pts.max(0))/2;uv=np.array([np.dot(cen-origin,u),np.dot(cen-origin,v)]);columns.append({'path':o['path'],'object_id':o['object_id'],'center_q':cen.tolist(),'center_uv':uv.tolist(),'extrema_uv':[[float(np.min((pts-origin)@u)),float(np.max((pts-origin)@u))],[float(np.min((pts-origin)@v)),float(np.max((pts-origin)@v))]]})
west=next(c for c in columns if '_042_' in c['path'])['extrema_uv'][0][1]+.06
east=next(c for c in columns if '_037_' in c['path'])['extrema_uv'][0][0]-.06
front=max(c['extrema_uv'][1][1] for c in columns if any(k in c['path'] for k in ['_042_','_040_','_037_']))+.06
rear=6.753945
foot=Polygon([q(west,front),q(east,front),q(east,rear),q(west,rear)])
(P/'MODEL-PLACEMENT-BASIS-R25.json').write_text(json.dumps({'status':'ACTUAL_NATIVE_ANCHORED_PHOTO_REFERENCED_MODEL_NOT_SURVEY','native_axes':{'base_COL042_q':origin.tolist(),'u':u.tolist(),'v':v.tolist()},'actual_columns':columns,'derived_working_uv':[west,front,east,rear],'own_working_polygon_q':list(foot.exterior.coords),'west_face_coordination':'Somidam owns westside of COL042/045; Pascucci starts east of projected eastnativeface+60mm, no equal split or duplicate sharedwall. Shared structural support gap remains native.','front':'Actual front column innerface+60mm; corridor remains entirely outside.','rear':'Actual approved floor exterior qY93.7 and matching-level official slopedfacade section near93.76; ownfinish set back at v6.753945, no floor edit.','long_extent':'Native structural span COL042→040→037; middle COL040/043 retained; broad long cafe observed; exact tenant extent MODEL, not scaled sourceboxes.','native_reuse':'Existing native columns remain exposed; own ceiling has hole around rear COL043 and no native mutation. Existing original rear facade remains fixed; ownwindowblinds are on interior side.','source_box_used_for_dimensions':False,'source':str(P/'LOCAL-ACTUAL-CAD-REFERENCE-R25.json'),'frozenR19_omits_R25_neighbors':'Preserve ACTUAL-SAVED-STORES-INDEX-R25.json; ownconstruction only.'},ensure_ascii=False,indent=2))
imports=[];imported={}
def imp(src,rel=None):
 src=pathlib.Path(src).resolve();rel=rel or 'Sources/'+src.name
 if rel in imported:return A+'/'+rel
 imports.append({'source':str(src),'destination':rel,'sha256':hashlib.sha256(src.read_bytes()).hexdigest()});imported[rel]=str(src);return A+'/'+rel
mats={}
def mat(k,col,**kw):mats[k]={'color':col if len(col)==4 else col+[1],**kw};return k
mat('black',[.025,.028,.03],metallic=.4,smoothness=.6);mat('maroon',[.24,.035,.055],metallic=.1,smoothness=.54);mat('warmwall',[.66,.39,.17],smoothness=.34);mat('ceiling',[.83,.8,.71]);mat('white',[.95,.94,.89],smoothness=.55);mat('steel',[.46,.49,.5],metallic=.91,smoothness=.68);mat('stone',[.86,.86,.8],smoothness=.7);mat('green',[.58,.64,.57],smoothness=.5);mat('leather',[.26,.24,.2],smoothness=.43);mat('glass',[.72,.86,.89,.12],transparent=True,smoothness=.95);mat('blind',[.58,.65,.65]);mat('warm_light',[1,.84,.48],emission=[1.8,1.26,.5,1]);mat('sign',[1,.94,.68],emission=[2,1.8,.96,1]);mat('red',[.55,.025,.028],smoothness=.55);mat('foil',[.77,.79,.77],metallic=.65,smoothness=.5);mat('wood',[.66,.37,.15],smoothness=.42)
# Owncopies of shared immutable sources; all actual Assets copying is deferred to own lease.
shutil.copytree(C/'bnc-east/sources/free-assets/wood_table_001',S/'wood_table_001',dirs_exist_ok=True)
mats['wood']['texture_asset']=imp(S/'wood_table_001/diff.jpg','Textures/wood_diff.jpg');mats['wood']['normal_asset']=imp(S/'wood_table_001/normal.jpg','Textures/wood_normal.jpg')
coffee=imp(S/'coffee-commercial/CommercialCoffeeMachine.fbx','Models/CommercialCoffeeMachine.fbx')
mat('coffee_pbr',[1,1,1],texture_asset=imp(S/'coffee-commercial/textures/Image_0.png','Textures/coffee_base.png'),normal_asset=imp(S/'coffee-commercial/textures/Image_3.png','Textures/coffee_normal.png'),metallic=.7,smoothness=.6)
pos=imp(C/'free-assets/pos-terminal/POS-Terminal.fbx','Models/POS-Terminal.fbx');mat('pos_pbr',[1,1,1],texture_asset=imp(C/'free-assets/pos-terminal/textures/Image_0.png','Textures/pos_base.png'),normal_asset=imp(C/'free-assets/pos-terminal/textures/Image_2.png','Textures/pos_normal.png'),metallic=.3,smoothness=.5)
stool=imp(S/'metal_stool_01/metal_stool_01.fbx','Models/metal_stool_01.fbx')
food={}
for k in ['croissant','strawberry_chocolate_cake']:
 food[k]=imp(S/k/(k+'.fbx'),'Models/'+k+'.fbx');mat(k,[1,1,1],texture_asset=imp(S/k/'diff.jpg','Textures/'+k+'_base.jpg'),normal_asset=imp(S/k/'normal.exr','Textures/'+k+'_normal.exr'),metallic=0,smoothness=.28)
ken=C/'free-assets/kenney-food-kit/Models/FBX format';cup=imp(ken/'mug.fbx','Models/mug.fbx');bag=imp(ken/'bag.fbx','Models/coffee_bag.fbx');plate=imp(ken/'plate.fbx','Models/plate.fbx')
fridge=imp(C/'free-assets/commercial-refrigerator/CommercialRefrigerator-Empty.fbx','Models/CommercialRefrigerator-Empty.fbx')
mat('fridge_case',[1,1,1],texture_asset=imp(C/'free-assets/commercial-refrigerator/textures/Image_9.png','Textures/fridge_case.png'),metallic=.55,smoothness=.55)
mat('fridge_interior',[1,1,1],texture_asset=imp(C/'free-assets/commercial-refrigerator/textures/Image_6.png','Textures/fridge_interior.png'),metallic=.4,smoothness=.6)
# Metadata is imported within own lease alongside own runtime assets.
lic=S/'SOURCE-LICENSES.md';lic.write_text('Commercial Coffee Machine by M.Reslan: CC-BY4.0 https://sketchfab.com/3d-models/commercial-coffee-machine-bab05bc7805d4b0dbc2cdcbb48441956\nPOS Terminal by Vladyslav.Yarovyi: CC-BY4.0 https://sketchfab.com/3d-models/pos-terminal-91c277a25bc34c5bbd080cd1e74161cb\nPolyHaven metal_stool_01, croissant, strawberry_chocolate_cake, wood_table_001: CC0 https://polyhaven.com/license\nKenney Food Kit: CC0 https://kenney.nl/assets/food-kit\nOwn adaptations: fitted dimensions, URP materials; source-generated furniture and physical sign based on fieldphotos. Thirdparty source photos are construction reference, not redistributed as sign billboards.\n')
lic.open('a').write('Commercial Refrigerator by Sean Thomas (foon), Khronos contributors CC-BY4.0 https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/CommercialRefrigerator ; empty derivative source preserved; ownURPglass/materials.\n')
imp(lic,'SOURCE-LICENSES.md')
objs=[];lib={}
def obj(kind,id,x,z,y,material,group='Interior',**kw):
 d={'kind':kind,'id':id,'q':q(x,z),'y':y,'yaw':yaw,'material':material,'group':group,'source_ids':['BUSAN_20240417_RENEWAL2023'],'qualification':'PHOTO_REFERENCED_MODEL_NOT_FIELD_MEASUREMENT',**kw};objs.append(d);return d
def b(id,x,z,y,sx,sy,sz,material,group='Interior',**kw):return obj('box',id,x,z,y,material,group,size=[sx,sy,sz],**kw)
def cy(id,x,z,y,dx,dy,dz,material,group='Interior',**kw):return obj('cylinder',id,x,z,y,material,group,size=[dx,dy/2,dz],**kw)
def edge(id,a,bottompt,bottom,height,thickness,material,group='Booth'):
 objs.append({'kind':'edge','id':id,'a':q(*a),'b':q(*bottompt),'bottom':bottom,'height':height,'thickness':thickness,'material':material,'group':group,'source_ids':['BUSAN_20240417_RENEWAL2023']})
def asset(id,path,x,z,y,fit,material,**kw):return obj('asset',id,x,z,y,material,asset=path,fit_size=fit,**kw)
def extrude(poly,lo,hi):
 poly=orient(poly,sign=1);verts=[];tris=[]
 def tri(a,b,c):i=len(verts);verts.extend([a,b,c]);tris.extend([i,i+1,i+2])
 for t in constrained_delaunay_triangles(poly).geoms:
  if not poly.covers(t.representative_point()):continue
  a,b,c=list(t.exterior.coords)[:3]
  tri([a[0],lo,a[1]],[b[0],lo,b[1]],[c[0],lo,c[1]])
  tri([c[0],hi,c[1]],[b[0],hi,b[1]],[a[0],hi,a[1]])
 for ring in [poly.exterior,*poly.interiors]:
  pp=list(ring.coords)
  for a,b0 in zip(pp,pp[1:]):
   aa,bb,cc,dd=[a[0],lo,a[1]],[b0[0],lo,b0[1]],[b0[0],hi,b0[1]],[a[0],hi,a[1]];tri(aa,bb,cc);tri(aa,cc,dd)
 return {'vertices':verts,'triangles':tris}
def ml(id,key,x,z,y,material,group='Furniture',**kw):return obj('library_mesh',id,x,z,y,material,group,mesh_key=key,collider=False,**kw)
# Exact source photograph letter contours -> separate closed thick meshes, physical sides and emission.
spec=importlib.util.spec_from_file_location('outline',C/'photo_outline_to_solid.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
photo=cv2.imread(str(S/'branch-20240417-00.jpg'));crop=photo[195:237,93:558];hsv=cv2.cvtColor(crop,cv2.COLOR_BGR2HSV);mask=cv2.inRange(hsv,np.array([0,0,174]),np.array([179,140,255]));cv2.imwrite(str(S/'sign-letter-ink-mask.png'),mask)
lib['field_letters']=mod.closed_solid_from_mask(mask,4.48,.405,.045,minimum_area=8,epsilon=.35)
(P/'SIGN-SOURCE-CONTOURS-R25.json').write_text(json.dumps({'source':'sources/branch-20240417-00.jpg','crop_xyxy':[93,195,558,237],'mask':'sources/sign-letter-ink-mask.png','method':'actual luminous field-letter ink threshold, native closed front/back/side mesh with holes; no font/TMP/billboard','mesh_polygon_count':lib['field_letters']['source_polygons'],'dimension_qualification':'MODEL physicalwidth4.48m, not measured'},indent=2))
# Shell: keep support bodies fixed; sources show own lowceiling, fascia and glazing.
w=west;e=east;f=front;r=rear
b('Fascia_main', (w+e)/2,f+.035,2.88,e-w,.52,.15,'black','Booth')
ml('Actual_field_CAFFER_PASCUCCI_letters','field_letters',4.02,f-.058,2.88,'sign','PhysicalSigns',yaw=yaw+180)
for xx in [1.35,3.0,4.7,6.5]:b('Sign_standoff_'+str(xx),xx,f+.04,2.88,.05,.05,.16,'steel','PhysicalSigns',collider=False)
# Source doorway open; side leaves retained as physical glazing, no collision across opening.
for j,(aa,bb) in enumerate([(w+.08,2.35),(4.35,8.0),(8.08,e-.08)]):
 edge('Front_glass_'+str(j),(aa,f+.04),(bb,f+.04),.06,2.55,.014,'glass')
 for xx in [aa,bb]:b('Front_mullion_'+str(j)+'_'+str(xx),xx,f+.04,1.34,.055,2.62,.065,'black','Booth')
 edge('Front_sill_'+str(j),(aa,f+.04),(bb,f+.04),.03,.055,.055,'black')
for xx in [2.35,4.35]:b('Entry_jamb_'+str(xx),xx,f+.04,1.32,.07,2.64,.08,'black','Booth')
# Open leaves slid behind adjacent fixed lights; exact passage remains2.0m.
for xx in [1.8,4.9]:b('Open_glass_leaf_'+str(xx),xx,f+.16,1.29,.96,2.48,.012,'glass','Booth',collider=False);b('Door_pull_'+str(xx),xx,f+.11,1.15,.022,.38,.035,'steel','Booth',collider=False)
edge('West_partition',(w+.05,f+.17),(w+.05,r-.03),0,2.62,.075,'warmwall')
edge('East_glass_return',(e-.045,f+.17),(e-.045,r-.04),.04,2.58,.015,'glass')
# Rear finish/blinds sit inside actual fixed facade; no originalsurface edits.
for j in range(8):
 aa=8.9+j*.79
 if aa+.77<e:b('Rear_source_blind_'+str(j),aa,r-.075,2.02,.77,1.24,.013,'blind','WindowBlinds',collider=False)
for xx in [w+.05,e-.05]:b('Side_post_'+str(xx),xx,r-.08,1.35,.06,2.7,.06,'black','Booth')
cp=box(w+.10,f+.16,e-.10,r-.1)
for c in columns:
 x,z=c['center_uv']
 if cp.contains(Point(x,z)):cp=cp.difference(Point(x,z).buffer(.55,resolution=16))
lib['ceiling']=extrude(cp,3.07,3.145);ml('Own_low_ceiling_native_column_clearance','ceiling',0,0,0,'ceiling','Ceiling')
# Counter in west structural bay: rounded maroon L with stone top, backbar visible service only.
plan=Polygon([(1.12,2.38),(1.98,2.38),(1.98,4.30),(6.90,4.30),(6.90,5.05),(1.12,5.05)]).buffer(.14,join_style=1).buffer(-.14,join_style=1)
lib['counter_body']=extrude(plan,.055,1.045);lib['counter_top']=extrude(plan.buffer(.025),1.045,1.095)
ml('Curved_source_maroon_L_counter','counter_body',0,0,0,'maroon','Counter');ml('White_stone_L_countertop','counter_top',0,0,0,'stone','Counter')
# Backbar base with individual doors and stainless worktop.
b('Visible_backbar_base',4.05,6.0,.455,5.78,.90,.72,'steel','Backbar');b('Visible_backbar_worktop',4.05,6.0,.925,5.82,.05,.76,'stone','Backbar')
for j in range(8):
 xx=1.60+j*.7;b('Backbar_door_'+str(j),xx,5.63,.455,.67,.79,.022,'steel','Backbar');b('Backbar_pull_'+str(j),xx,5.602,.78,.31,.018,.025,'black','Backbar',collider=False)
# Amber rearwall, repeated dark tall cubbies, visible mugs/bags physically instanced.
b('Amber_cubby_back',4.05,6.58,1.94,5.85,2.12,.045,'warmwall','Shelves')
for j in range(20):b('Dark_cubby_upright_'+str(j),1.15+j*.305,6.45,1.96,.022,2.1,.25,'black','Shelves',collider=False)
for row in range(4):
 yy=1.03+row*.64;b('Cubby_shelf_'+str(row),4.05,6.45,yy,5.84,.024,.29,'black','Shelves');b('Cubby_hidden_light_'+str(row),4.05,6.565,yy+.58,5.72,.018,.014,'warm_light','Shelves',collider=False)
for row in range(3):
 for j in [1,3,5,8,11,13,16,18]:
  xx=1.15+j*.305;yy=1.06+row*.64
  if (row+j)%3==0:asset('Wholebean_foil_bag_%s_%s'%(row,j),bag,xx,6.40,yy,[.14,.235,.07],'foil',collider=False,group='Merchandise')
  else:asset('Physical_mug_%s_%s'%(row,j),cup,xx,6.40,yy,[.12,.11,.10],['white','red','black'][j%3],collider=False,group='Merchandise')
asset('Downloaded_commercial_three_group_espresso',coffee,4.65,5.98,.95,[1.18,.60,.58],'coffee_pbr',group='Equipment',yaw=yaw+180)
asset('Downloaded_cashier_POS',pos,1.54,3.66,1.10,[.36,.38,.34],'pos_pbr',group='Equipment',yaw=yaw+90)
# Pastry-case physical steelbase, glasslights, three separate shelves; downloaded scannedfood.
b('Coldcase_pedestal',1.57,2.93,.42,.82,.80,1.36,'maroon','PastryCase');b('Coldcase_top',1.57,2.93,.845,.86,.05,1.40,'steel','PastryCase')
for yy in [.94,1.16,1.38]:
 b('Glasscase_shelf_'+str(yy),1.57,2.93,yy,.75,.012,1.30,'glass','PastryCase',collider=False)
 b('Glasscase_strip_'+str(yy),1.93,2.93,yy+.1,.018,.018,1.27,'warm_light','PastryCase',collider=False)
b('Glasscase_publicfront',2.00,2.93,1.15,.012,.65,1.37,'glass','PastryCase',collider=False)
for zz in [2.24,3.62]:b('Glasscase_end_'+str(zz),1.57,zz,1.15,.85,.66,.012,'glass','PastryCase',collider=False)
b('Glasscase_lid',1.57,2.93,1.47,.86,.015,1.4,'glass','PastryCase',collider=False)
for row,yy in enumerate([.95,1.17,1.39]):
 for j in range(4):
  zz=2.45+j*.31;k='strawberry_chocolate_cake' if row<2 else 'croissant';asset('Scanned_pastry_%s_%s'%(row,j),food[k],1.57,zz,yy,[.22,.15 if row<2 else .09,.23],k,collider=False,group='Merchandise',yaw=yaw+90)
# Observed bakery pedestal/counter goods, never names standing in for objects.
for j in range(6):
 xx=2.9+j*.45;asset('Bakery_counter_croissant_'+str(j),food['croissant'],xx,4.55,1.10,[.23,.095,.19],'croissant',collider=False,group='Merchandise')
# Matching source sculptural bent-ply barrelchair: closedroundseat, crossblades and curvedback.
lib['barrel_seat']=extrude(Point(0,0).buffer(.235,resolution=32),.435,.465)
arc=[]
for angle in np.linspace(12,168,46):arc.append([.251*math.cos(math.radians(angle)),.251*math.sin(math.radians(angle))])
for angle in np.linspace(168,12,46):arc.append([.231*math.cos(math.radians(angle)),.231*math.sin(math.radians(angle))])
lib['barrel_back']=extrude(Polygon(arc),.46,.745)
lib['chair_cross_a']=extrude(box(-.21,-.012,.21,.012),.02,.435);lib['chair_cross_b']=extrude(box(-.012,-.21,.012,.21),.02,.435)
def chair(id,x,z,angle):
 for key in ['barrel_seat','barrel_back','chair_cross_a','chair_cross_b']:ml(id+'_'+key,key,x,z,0,'wood','SourceFurniture',yaw=yaw+angle)
# Long communal rounded palegreen table, physical downloadedcleaned black metal stools.
table=box(-.47,-.92,.47,.92).buffer(.25,join_style=1);lib['communal_table']=extrude(table,1.02,1.085)
for i,xx in enumerate([10.35,13.65]):
 ml('Source_palegreen_community_table_'+str(i),'communal_table',xx,3.82,0,'green','SourceFurniture')
 for zz in [3.15,4.5]:b('Community_pedestal_%s_%s'%(i,zz),xx,zz,.51,.46,1.02,.42,'black','SourceFurniture')
 for side in [-1,1]:
  for j,zz in enumerate([3.03,3.82,4.61]):asset('Downloaded_community_stool_%s_%s_%s'%(i,side,j),stool,xx+side*.99,zz,0,[.39,.73,.39],'black',group='Seating')
asset('Downloaded_window_display_refrigerator',fridge,15.40,6.03,0,[.65,1.91,.64],'fridge_case',group='Equipment',material_map={'FridgeGlass':'glass','FridgeInterior':'fridge_interior','FridgeDoorInner':'fridge_interior','*':'fridge_case'},yaw=yaw+180)
# Window banquette and two square pedestal tables; exact extra seat count unverified.
b('Rear_banquette_base',11.8,6.28,.23,5.95,.44,.57,'wood','Seating');b('Rear_banquette_cushion',11.8,6.24,.47,5.94,.08,.59,'leather','Seating');b('Rear_banquette_back',11.8,6.52,.79,5.94,.56,.10,'leather','Seating')
for i,xx in enumerate([9.5,11.1,12.7,14.3]):
 b('Source_square_tabletop_'+str(i),xx,5.68,.77,.66,.05,.66,'wood','SourceFurniture');cy('Table_pedestal_'+str(i),xx,5.68,.39,.035,.72,.035,'black','SourceFurniture');cy('Table_foot_'+str(i),xx,5.68,.035,.41,.05,.41,'black','SourceFurniture');chair('Source_bentply_chair_'+str(i),xx,4.98,180)
# Visible waiting/merchandise ledge near firstbay, retain circulation through center.
b('Waiting_table_top',7.04,2.8,1.02,.74,.055,2.50,'green','SourceFurniture')
for zz in [2.0,3.6]:b('Waiting_table_support_'+str(zz),7.04,zz,.50,.57,1.,.23,'black','SourceFurniture')
b('Front_merchandise_cabinet',5.82,1.35,.4,1.42,.8,.42,'wood','Merchandise');b('Front_merchandise_top',5.82,1.35,.83,1.46,.05,.47,'stone','Merchandise')
for row in range(2):
 yy=.87+row*.25;b('Merchandise_display_shelf_'+str(row),5.82,1.35,yy,1.40,.02,.42,'glass','Merchandise',collider=False)
 for j in range(5):asset('Displayed_mug_%s_%s'%(row,j),cup,5.3+j*.25,1.34,yy+.02,[.15,.13,.12],['white','red','black'][j%3],group='Merchandise',collider=False)
# Ceiling tracks, physical spot heads and HVAC face/louvres; sourcephotos03/09.
for zz in [1.7,3.9,5.9]:
 b('Ceiling_track_'+str(zz),(w+e)/2,zz,2.98,e-w-.5,.028,.028,'white','Ceiling',collider=False)
 for xx in [2.,4.,6.,9.4,11.5,14.6]:cy('Track_spot_%s_%s'%(zz,xx),xx,zz,2.90,.065,.16,.065,'white','Ceiling',collider=False);cy('Spot_lens_%s_%s'%(zz,xx),xx,zz,2.815,.052,.008,.052,'warm_light','Ceiling',collider=False)
for xx in [4.7,11.6]:
 b('HVAC_cassette_'+str(xx),xx,3.,3.025,.85,.055,.85,'white','Ceiling',collider=False)
 for n in range(13):b('HVAC_louvre_%s_%s'%(xx,n),xx-.32+n*.053,3.,2.992,.013,.006,.62,'black','Ceiling',collider=False)
for i,xx in enumerate([9.5,11.1,12.7,14.3]):
 cy('Pendant_wire_'+str(i),xx,5.65,2.5,.004,.9,.004,'black','Lights',collider=False);cy('Pendant_disk_'+str(i),xx,5.65,2.04,.23,.022,.23,'warm_light','Lights',collider=False)
for xx in [3.0,6.,10.3,13.6]:obj('light','Own_interior_light_'+str(xx),xx,4.5,2.63,'white','Lights',color=[1,.90,.74,1],range=4.3,intensity=.24)
# Accurate logo solid on circular disc by fieldphoto mask, placed glassside without text.
logo=photo[324:374,551:616]; hsv=cv2.cvtColor(logo,cv2.COLOR_BGR2HSV);mask=cv2.inRange(hsv,np.array([0,60,20]),np.array([40,255,170]));cv2.imwrite(str(S/'round-logo-ink-mask.png'),mask);lib['round_logo']=mod.closed_solid_from_mask(mask,.45,.35,.012,minimum_area=4,epsilon=.4)
# Disc axis+Z; cylinder intrinsicY rotated90 onto storefront plane.
obj('cylinder','Source_round_logo_disc',5.82,f-.012,1.85,'stone','PhysicalSigns',size=[.64,.012,.64],euler=[90,yaw,0],collider=False)
ml('Source_round_logo_relief','round_logo',5.82,f-.036,1.85,'warmwall','PhysicalSigns',yaw=yaw+180)
packet={'store_key':'pascucci','origin_q':origin.tolist(),'floor_world_y':7.0,'source_packet':str(P.relative_to(R)/'SOURCE-EVIDENCE-R25.json'),'source_imports':imports,'materials':mats,'mesh_library':lib,'objects':objs,'limits':['Photo-proportioned MODEL geometry, not surveyed equality.','Original exactbranch floor/elevation/furniture plan not found.','Sourceepoch2024 post2023renewal, crosschecked2024/2025front; official84seat count not reproduced as unknown exactpositions.','Native columns, facade, floor, circulation and all otherstores preserved; hiddenBOH omitted.']}
(P/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
sha=hashlib.sha256((P/'STORE-PACKET-R25.json').read_bytes()).hexdigest();scope='C180_R25_PASCUCCI_APPLY_001';cs=(C/'StoreBuilderR25-v3-FREE-ASSETS.template.cs').read_text()
for a,z in {'__STORE_KEY__':'pascucci','__WORKER_THREAD__':'classic:/root/r25_pascucci','__EXACT_SCOPE__':scope,'__PACKET_PATH__':str(P/'STORE-PACKET-R25.json'),'__PACKET_SHA256__':sha}.items():cs=cs.replace(a,z)
cs=cs.replace('m.RecalculateNormals();m.RecalculateBounds();string path=folder+"/MESH_SHARED_', 'm.uv=m.vertices.Select(v=>new Vector2(v.x*.5f+v.z*.2f,v.y*.5f+v.z*.2f)).ToArray();m.RecalculateNormals();m.RecalculateBounds();string path=folder+"/MESH_SHARED_')
(P/'ApplyStoreR25.cs').write_text(cs)
evidence={'store_key':'pascucci','identity':'BusanStation2F waitinghall actual branch, source2023renewal','primary_source':{'url':'https://m.blog.naver.com/mongsangteller/223417891331','epoch':'2024-04-17 post states2023renewal','directly_viewed_files':['sources/branch-contact-00.jpg','sources/branch-contact-20.jpg','sources/branch-20240417-00.jpg','sources/branch-20240417-03.jpg','sources/branch-20240417-09.jpg','sources/branch-20240417-17.jpg','sources/branch-20240417-19.jpg','sources/branch-20240417-20.jpg'],'source_exclusions':'Photo01 pre-renewal; photo07 Yangjae branch; photo22 onlineproductsearch; photo24 otherbranchopening. None used as currentlayout.','observed':'Blackfascia/glassentry, actual luminous lettercontours; maroon rounded L-counter, stone top, ambercubbiesshelves,mugs/beanbags, threegroupespresso andPOS; scannedpastrycase products, palegreenroundedcommunitytables/darkmetalstools, sourcebentplybarrelchairs/windowbanquette, lowceilingtracklights/cassetteHVAC.'},'front_crosscheck':{'url':'https://m.blog.naver.com/oyj10244/223446992106','epoch':'2024-05-15','directly_viewed_files':['sources/naver-20240515-source03-032-800.jpg','sources/FIELD-CONTACT-02.jpg','sources/FIELD-CONTACT-03.jpg'],'facts':'FormerTteokgongbang/nowSomidam is immediately west; Pascucci flushfacade, corridor opposite StoryWay kept.'},'official_reference':{'url':'https://www.bsdonggu.go.kr/board/view.donggu?boardId=BBS_0000257&dataSid=5012638&menuCd=DOM_000000313002003004&startPage=1','root_report':'Official page84seats, sourceepoch/currentseatlocations not established; not transferred to layout'},'exactbranch_drawings':'Bounded originalplan/elevation/furniture search found no usable exactbranch design; currentphotos used, nootherbranchtransfer','free_assets':[{'name':'CommercialCoffeeMachine','author':'M.Reslan','license':'CC-BY4.0','source':'https://sketchfab.com/3d-models/commercial-coffee-machine-bab05bc7805d4b0dbc2cdcbb48441956','download':'https://huggingface.co/datasets/allenai/objaverse/resolve/main/glbs/000-045/bab05bc7805d4b0dbc2cdcbb48441956.glb','adaptation':'OwnURP and photo-fit, exactRancilio identity not claimed'},{'name':'POS-Terminal','author':'Vladyslav.Yarovyi','license':'CC-BY4.0','source':'https://sketchfab.com/3d-models/pos-terminal-91c277a25bc34c5bbd080cd1e74161cb'},{'name':'Metal Stool 01','license':'CC0','source':'https://polyhaven.com/a/metal_stool_01','adaptation':'Cleanblackmaterial replaces weathering; generic shape MODEL'},{'name':'Scannedcroissant/strawberry_chocolate_cake/wood_table_001','license':'CC0','source':'https://polyhaven.com','staged_from':'bnc-east owncopies sourceunchanged'},{'name':'Kenney Food Kit physicalmugs/beanbags/plates','license':'CC0','source':'https://kenney.nl/assets/food-kit'}],'source_derived_custom_furniture':'Source19bentplychair and source17greencommunitytable/banquette physically modeled editable solids, downloaded stools and actual downloaded equipment/products are instantiated.','signs':'Actualfieldink closedsolid extrusion with front/back/sides and holes, ownfascia/panel/standoffs/emission. NoTMP, font, photo billboard, generic label or flat sign proxy.','metric_qualification':'All fit dimensions MODEL, no measurement/survey/equality claim.','hidden_BOH':'Unobservedhiddenrooms/equipment omitted.','no_QA_tests_Play_reopen_capture':True}
(P/'SOURCE-EVIDENCE-R25.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2))
(P/'task_plan.md').write_text('# Pascucci R25 plan\n\n1. Authority, native CAD anchors, field evidence and adjacent owner agreement — complete\n2. Stage downloaded free assets and source contour solid sign — complete\n3. Build own packet and directly C180 apply/save — in_progress\n4. Record actual save receipts and limits — pending\n\nNo QA/tests/probes/Play/reopen/captures.\n')
(P/'progress.md').open('a').write('\nOwn packet built: %s objects, %s imported sourcefiles. Nativeface derivedboundary; sharedfaceSomidam agreed. Source silhouette sign and customfurniture; actual downloadedcoffee/POS/stool/pastry/mug/bag assets staged, ownimports deferred to C180.\n'%(len(objs),len(imports)))
print(json.dumps({'objects':len(objs),'imports':len(imports),'packet_sha':sha,'uv':[w,f,e,r],'source_sign_polygons':lib['field_letters']['source_polygons']}))
