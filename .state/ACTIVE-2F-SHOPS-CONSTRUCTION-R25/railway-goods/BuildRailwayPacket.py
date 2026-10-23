from pathlib import Path
import json,math,hashlib,sys,cv2,numpy as np
ROOT=Path('/Users/um-yunsang/CHOOGuard');BASE=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25';OWN=BASE/'railway-goods';ART='Assets/ChooGuard/Art/StationInterior/R25_Shops/railway-goods'
sys.path.insert(0,str(BASE));from photo_outline_to_solid import closed_solid_from_mask

def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def dump(n,x):(OWN/n).write_text(json.dumps(x,ensure_ascii=False,indent=2))
# Native support axes, from full original R19 segment endpoints, never a sourcebox.
C=np.array([-11.66371516,53.37755215]);D=np.array([1.39998021,-.00683216]);D=D/np.linalg.norm(D);U=np.array([D[1],-D[0]])
# Original front setback(.13+.5+.105=.735) fitted to actual native front face.
# Native current long dimension1.4 and transverse1m remain fixed. Right faceoffset.4m.
origin=C-U*4.6-D*(.735+.7);YAW=math.degrees(math.atan2(-U[1],U[0]))
def q(x,z):return [round(float(v),7) for v in origin+U*x+D*z]
foot=[q(0,0),q(5.5,0),q(5.5,6),q(0,6),q(0,0)]
dump('MODEL-PLACEMENT-BASIS-R25.json',{'status':'ORIGINAL_DESIGN_DIMENSIONS_MIGRATED_TO_ACTUAL_NATIVE_FACES_MODEL','source_pdf':'.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/root/2026-goods-shop-architecture-original.pdf','pdf_sha256':'b5b375cf97740c017fa483587c4b839ccc0450741b2f6c7bc091b905152e826a','source_pages_0based':[4,7,8,10,13,14,20,21,22,23,24,47],'source_dimensions_m':[5.5,6.0],'actual_native_anchor':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-1479865581-0','native_current_column_long_transverse_m':[1.4,1.0],'native_center_q':C.tolist(),'actual_depth_axis_q':D.tolist(),'actual_source_right_axis_q':U.tolist(),'source_front_to_actual_column_front_offset_m':.735,'source_right_boundary_to_actual_column_rightface_m':.4,'source_metric_fit_is_MODEL_not_field_survey':True,'booth_footprint_q':foot,'front_endpoints':[q(0,0),q(5.5,0)],'rear_endpoints':[q(0,6),q(5.5,6)],'shared_wall':None,'native063_clearance_m':3.08,'native_floor_not_cut_or_replaced':True,'R21_R22_candidate_box_used_for_metric':False,'source_images_warped_for_floorplan_alignment':False,'column_original_geometry_changed':False,'qualification':'Absolute local support binding is MODEL; original dimensions and actual native vectors explicit. Current Oct2026 photos corroborate branch/configuration, not field measurement.'})
p={'store_key':'railway-goods','origin_q':q(0,0),'floor_world_y':7,'source_packet':'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/railway-goods/SOURCE-EVIDENCE-R25.json','materials':{},'objects':[],'mesh_library':{},'source_imports':[]}
M=p['materials'];O=p['objects'];L=p['mesh_library']
def mat(n,c,metal=0,smooth=.35,**kw):M[n]={'color':c,'metallic':metal,'smoothness':smooth,**kw}
mat('charcoal',[.035,.055,.065,1],.65,.48);mat('stainless',[.62,.64,.65,1],.88,.65);mat('rackgray',[.48,.49,.49,1],.7,.42);mat('white',[.91,.9,.86,1],.05,.42);mat('cream',[.85,.81,.69,1],0,.14);mat('black',[.015,.018,.022,1],.25,.42);mat('navy',[.025,.08,.26,1],.12,.4);mat('blue',[.035,.35,.72,1],.05,.28);mat('red',[.69,.055,.045,1],0,.25);mat('yellow',[.96,.79,.15,1],0,.3);mat('green',[.075,.42,.20,1],0,.3);mat('pink',[.79,.39,.57,1],0,.3);mat('skin',[.91,.77,.59,1],0,.15);mat('glass',[.72,.83,.86,.08],0,.95,transparent=True);mat('packglass',[.92,.96,1,.09],0,.9,transparent=True);mat('warmwhite',[.96,.96,.92,1],.1,.6,emission=[1.25,1.25,1.20,1]);mat('letterwhite',[.95,.95,.91,1],.2,.62,emission=[.8,.8,.72,1]);mat('pos_black',[.025,.029,.032,1],.05,.48)
# Immutable downloaded sources copied into own stage; imports happen only under lease.
def imp(src,dest):
 src=Path(src);p['source_imports'].append({'source':str(src),'destination':dest,'sha256':sha(src)});return ART+'/'+dest
shelf=imp(OWN/'sources/free-models/store-shelves/SingleFace-Shelf.fbx','Sources/SingleFace-Shelf.fbx')
pos=imp(OWN/'sources/free-models/pos-terminal/POS-Terminal.fbx','Sources/POS-Terminal.fbx')
bag=imp(OWN/'sources/free-models/tote-bag/tote-bag.fbx','Sources/Tote-Bag.fbx')
normal=imp(OWN/'sources/free-models/pos-terminal/textures/Image_2.png','Sources/POS_normal.png');M['pos_black']['normal_asset']=normal
for n,source in [('rackgray','Peter_D Store Shelves CC-BY4 / mat_shelf'),('glass','Peter_D Store Shelves CC-BY4 / mat_shelf_glass'),('pos_black','Vladyslav.Yarovyi POS CC-BY4 / Material_2100454184+185'),('cream','Jeremy E. Grayson tote CC-BY4 / None slots')]:M[n]['source_material_identity']=source
# Source actual printed surfaces live on solid packaging, never entire-room/sign planes.
texdir=OWN/'sources/derived-product-print';texdir.mkdir(exist_ok=True)
for i,(file,rect) in enumerate([('ROOT-funnycake5-224432769870-02.jpg',(7,34,288,248)),('ROOT-funnycake5-224432769870-05.jpg',(65,83,552,596)),('ROOT-funnycake5-224432769870-07.jpg',(40,77,315,262)),('ROOT-funnycake5-224432769870-18.jpg',(335,222,531,360))]):
 a=cv2.imread(str(OWN/'sources'/file));x0,y0,x1,y1=rect;a=a[y0:y1,x0:x1];a=cv2.resize(a,(512,512));f=texdir/('product-print-%d.png'%i);cv2.imwrite(str(f),a);asset=imp(f,'Sources/product-print-%d.png'%i);mat('print%d'%i,[1,1,1,1],0,.35,texture_asset=asset)

def add(kind,id,x,z,y,matr,group,**kw):
 a={'kind':kind,'id':id,'q':q(x,z),'y':round(y,5),'yaw':YAW,'material':matr,'group':group,'source_ids':['DESIGN2026','OCT2026_PHOTO'],'qualification':'SOURCE_VISIBLE_OR_ORIGINAL_DESIGN_MODEL_NOT_FIELD_MEASUREMENT',**kw};O.append(a);return a

def box(id,x,z,y,w,h,d,m='stainless',g='Fixtures',**kw):return add('box',id,x,z,y,m,g,size=[w,h,d],**kw)
def sphere(id,x,z,y,w,h,d,m,g='Goods',**kw):return add('sphere',id,x,z,y,m,g,size=[w,h,d],collider=False,**kw)
def cyl(id,x,z,y,w,h,d,m,g='Fixtures',rot=None):return add('cylinder',id,x,z,y,m,g,size=[w,h/2,d],collider=False,**({'euler':rot} if rot else {}))
def asset(id,x,z,y,fit,path,mapping,g,ry=0,collider=True):return add('asset',id,x,z,y,None,g,asset=path,fit_size=fit,material_map=mapping,yaw=YAW+ry,collider=collider)
def lib(id,key,x,z,y,m,g='Goods',scale=None,ry=0):return add('library_mesh',id,x,z,y,m,g,mesh_key=key,collider=False,yaw=YAW+ry,**({'size':scale} if scale else {}))
# Originalglassmetalbooth front1.8m opening, retained as open slidingleaves.
for x,z in [(.065,.065),(5.435,.065),(.065,5.935),(5.435,5.935)]:box('frame_post_%s_%s'%(x,z),x,z,1.60,.13,3.2,.13,'charcoal','Booth')
for z in [.065,5.935]:
 for y,h in [(.05,.10),(3.15,.10)]:box('crossbeam_%s_%s'%(z,y),2.75,z,y,5.5,h,.13,'charcoal','Booth')
for x in [.065,5.435]:
 for y,h in [(.05,.10),(3.15,.10)]:box('sidebeam_%s_%s'%(x,y),x,3,y,.13,h,5.74,'charcoal','Booth')
 for k in range(4):box('side_glass_%s_%d'%(x,k),x,.13+(k+.5)*1.435,1.60,.01,3,1.425,'glass','Booth',cast_shadows=False)
for k in range(4):box('rear_glass_%d'%k,.13+(k+.5)*1.31,5.935,1.60,1.30,3,.01,'glass','Booth',cast_shadows=False)
# Fixed panesleft and rightofdoor and rightcolumnwindow.
for k,(x,w) in enumerate([(.58,.9),(3.38,.9),(4.625,1.49)]):box('front_fixedglass_%d'%k,x,.065,1.15,w,2.2,.01,'glass','Booth',cast_shadows=False)
for x in [1.055,2.905,3.855]:box('front_mullion_%s'%x,x,.065,1.15,.05,2.2,.06,'charcoal','Booth')
for x in [.59,3.37]:
 box('opened_sliding_leaf_%s'%x,x,.14,1.15,.88,2.19,.01,'glass','Entrance',cast_shadows=False)
 for dx in [-.44,.44]:box('opened_leaf_jamb_%s_%s'%(x,dx),x+dx,.14,1.15,.03,2.2,.05,'charcoal','Entrance')
 cyl('leaf_handle_%s'%x,x+(.28 if x<2 else -.28),.105,1.05,.018,.34,.018,'stainless','Entrance')
box('automatic_header',1.98,.065,2.60,3.7,.8,.13,'charcoal','Booth');box('interior_door_motor',1.98,.18,2.26,3.7,.12,.20,'stainless','Entrance')
# Own ceiling is a thin new booth component; no station roof/floor mutation.
box('display_ceiling_main',2.105,3,3.565,3.95,.03,5.74,'white','Ceiling')
box('display_ceiling_front_native_clear',4.14,.425,3.565,.12,.03,.59,'white','Ceiling')
box('display_ceiling_rear_native_clear',4.14,4.01,3.565,.12,.03,3.72,'white','Ceiling')
box('storage_ceiling_front_native_clear',4.865,.425,3.215,1.01,.03,.59,'white','Ceiling')
box('storage_ceiling_rear_native_clear',4.865,4.01,3.215,1.01,.03,3.72,'white','Ceiling')
box('storage_ceiling_side_native_clear',5.245,1.435,3.215,.25,.03,1.43,'white','Ceiling')
# Source interiorpartition behindactualcol; portal in originalrearportion. No duplicated nativecolumn.
for za,zb in [(2.16,3.92),(4.76,5.87)]:box('storage_partition_%s'%za,4.28,(za+zb)/2,1.6,.16,3.2,zb-za,'white','Partition')
box('storage_portal_header',4.28,4.34,2.665,.16,1.07,.84,'white','Partition')
box('storage_pocket_door_open',4.30,4.96,1.05,.055,2.1,.82,'white','Partition')
# Native021 source-facing column cladding omitted deliberately; original mesh/material staysfixed.
# Ceiling trackslights from actual12x1.2 and4x.9 T7 fittings, not emissive labels.
for ix,x in enumerate([.665,1.565,2.765,3.665]):
 for k in range(3):
  z=.94+1.3*k
  box('T7_%d_%d'%(ix,k),x,z,3.34,.035,.035,1.2,'warmwhite','Lighting',collider=False)
  cyl('T7_drop_%d_%d'%(ix,k),x,z,3.44,.012,.20,.012,'stainless','Lighting')
 for k,z in enumerate([.9,2.2,3.5]):
  s=cyl('track_spot_%d_%d'%(ix,k),x+.05,z,3.27,.065,.14,.065,'white','Lighting');s['euler']=[18,YAW,0]
 box('T7_short_%d'%ix,x,4.87,3.34,.035,.035,.9,'warmwhite','Lighting',collider=False)
for x,z in [(1.3,2),(3.25,3.4),(2.0,4.8)]:add('light','source_local_light_%s_%s'%(x,z),x,z,2.9,'white','Lighting',range=3.7,intensity=.55,color=[1,.97,.91,1])
# CircularLG cassette visiblephoto01, actual sourceequipment dimensionsmodel.
cyl('round_AC_outer',2.165,2.83,3.42,1.04,.22,1.04,'white','Ceiling')
cyl('round_AC_dark_vent',2.165,2.83,3.295,.83,.025,.83,'black','Ceiling')
cyl('round_AC_center',2.165,2.83,3.275,.62,.03,.62,'rackgray','Ceiling')
for k in range(20):
 a=k*math.pi/10;box('AC_vent_louvre_%d'%k,2.165+.32*math.cos(a),2.83+.32*math.sin(a),3.255,.045,.02,.11,'white','Ceiling',collider=False,yaw=YAW-math.degrees(a))
# Exact original islanddim1.655x.910x1.660, rotated longitudinally towardfrontdepth.
# Buildcabinet/sheffphysicalparts, no opaqueproxy around downloadedshelf.
for i in range(4):
 x=1.72+(.455 if i%2 else -.455)/2;z=1.6+(.8275 if i//2 else -.8275)/2
 box('island_cabinet_%d'%i,x,z,.295,.44,.48,.80,'stainless','Island')
 box('island_frontdoor_%d'%i,x+(.229 if i%2 else -.229),z,.30,.018,.45,.77,'stainless','Island')
 for dz in [-.30,.30]:
  s=cyl('island_caster_%d_%s'%(i,dz),x,z+dz,.055,.065,.045,.065,'black','Island',rot=[90,YAW,0])
box('island_spine',1.72,1.6,1.02,.055,.96,1.655,'rackgray','Island')
for k,y in enumerate([.54,.915,1.265]):box('island_shelf_%d'%k,1.72,1.6,y,.91,.024,1.655,'stainless','Island')
box('island_top_header',1.72,1.6,1.55,.065,.22,1.655,'stainless','Island')
# Matching actual downloaded perforatedwall racks:7left,5back,4right. Graysourcepaintandglassslots explicitlymapped.
for k in range(7):asset('downloaded_left_rack_%d'%k,.39,.54+.805*k,0,[.805,2.70,.47],shelf,{'mat_shelf':'rackgray','mat_shelf_glass':'glass','*':'rackgray'},'DownloadedRacks',ry=90)
for k in range(5):asset('downloaded_back_rack_%d'%k,.53+.805*k,5.60,0,[.805,2.70,.47],shelf,{'mat_shelf':'rackgray','mat_shelf_glass':'glass','*':'rackgray'},'DownloadedRacks',ry=180)
for k in range(4):asset('downloaded_right_pegboard_%d'%k,4.09,2.56+.805*k,.52,[.805,2.4,.065],shelf,{'mat_shelf':'rackgray','mat_shelf_glass':'glass','*':'rackgray'},'DownloadedRacks',ry=-90)
for k in range(7):
 z=.54+.805*k;box('left_low_storage_%d'%k,.37,z,.275,.44,.52,.78,'stainless','Cabinets');box('left_upper_lightbox_%d'%k,.37,z,2.82,.46,.4,.78,'stainless','Cabinets');box('left_shelf_light_%d'%k,.57,z,2.60,.02,.02,.76,'warmwhite','Cabinets',collider=False)
for k in range(5):
 x=.53+.805*k;box('rear_low_storage_%d'%k,x,5.62,.275,.78,.52,.43,'stainless','Cabinets');box('rear_upper_lightbox_%d'%k,x,5.62,2.82,.78,.4,.43,'stainless','Cabinets')
for k in range(4):box('right_low_storage_%d'%k,3.995,2.56+.805*k,.275,.45,.52,.78,'stainless','Cabinets')
# Actual original sourcecounter2x.6x1.1, back opening shelving and drawers.
box('counter_top',3.2,4.26,1.08,2,.04,.6,'white','Counter')
for x in [2.23,4.17]:box('counter_cheek_%s'%x,x,4.26,.55,.06,1.06,.60,'white','Counter')
box('counter_public_front',3.2,3.97,.55,2,1.04,.03,'white','Counter')
for y in [.07,.435,.83]:box('counter_rearshelf_%s'%y,3.2,4.28,y,1.86,.02,.50,'white','Counter')
for x in [2.7,3.3,3.9]:box('counter_drawer_%s'%x,x,4.53,.94,.56,.19,.025,'white','Counter')
asset('downloaded_POS',3.37,4.21,1.1,[.22,.25,.24],pos,{'Material_2100454184':'pos_black','Material_2100454185':'pos_black','*':'pos_black'},'DownloadedEquipment',ry=180,collider=False)
box('tablet_till_stand',2.65,4.20,1.145,.15,.09,.14,'charcoal','Counter');s=box('tablet_till_display',2.65,4.18,1.30,.27,.18,.025,'black','Counter');s['euler']=[20,YAW,0]
# Closedminiaturetrains: actualcurrentmodel ITX/KTXprofile mesh, windows,wheels,couplers. No steamtoy/phototrainproxy.
def train_mesh():
 v=[];t=[];rings=[(-.37,.045,.025),(-.32,.075,.07),(-.23,.08,.11),(.28,.08,.11),(.34,.07,.10)]
 for x,w,h in rings:
  for zz,yy in [(-w,.035),(-w,h*.80),(-w*.68,h),(w*.68,h),(w,h*.80),(w,.035)]:v.append([x,yy,zz])
 n=6
 for k in range(len(rings)-1):
  for j in range(n):a=k*n+j;b=k*n+(j+1)%n;c=(k+1)*n+(j+1)%n;d=(k+1)*n+j;t.extend([a,b,c,a,c,d])
 for a in range(1,n-1):t.extend([0,a+1,a]);j=(len(rings)-1)*n;t.extend([j,j+a,j+a+1])
 return {'vertices':v,'triangles':t}
L['actual_modern_ITX_KTX_body']=train_mesh()
def miniature(id,x,z,y,ry=0,scale=.72,color='white'):
 g='Goods_Trains';a=math.radians(ry)
 def at(dx,dz):return x+scale*(dx*math.cos(a)+dz*math.sin(a)),z+scale*(-dx*math.sin(a)+dz*math.cos(a))
 lib(id+'_profile','actual_modern_ITX_KTX_body',x,z,y,'stainless',g,scale=[scale,scale,scale],ry=ry)
 for side in [-1,1]:
  xx,zz=at(.015,side*.0805);box(id+'_blue_stripe_%d'%side,xx,zz,y+.052*scale,.55*scale,.015*scale,.003*scale,'blue',g,collider=False,yaw=YAW+ry)
  for k in range(6):
   xx,zz=at(-.20+k*.081,side*.081);box(id+'_window_%d_%d'%(side,k),xx,zz,y+.089*scale,.065*scale,.035*scale,.003*scale,'black',g,collider=False,yaw=YAW+ry)
  for k in [-.20,.21]:
   xx,zz=at(k,side*.064);cyl(id+'_wheel_%d_%s'%(side,k),xx,zz,y+.023*scale,.049*scale,.018*scale,.049*scale,'black',g,rot=[90,YAW+ry,0])
 xx,zz=at(-.285,0);s=box(id+'_sloped_windscreen',xx,zz,y+.082*scale,.035*scale,.041*scale,.114*scale,'black',g,collider=False);s['euler']=[0,YAW+ry,29]
 for dx in [-.365,.345]:
  xx,zz=at(dx,0);box(id+'_coupler_%s'%dx,xx,zz,y+.031*scale,.045*scale,.022*scale,.04*scale,'black',g,collider=False,yaw=YAW+ry)
for k in range(6):miniature('left_loose_ITX_%d'%k,.57,.60+.82*k,2.13,ry=90,scale=.9,color='stainless')
for k in range(5):miniature('rear_loose_KTX_%d'%k,.54+.79*k,5.43,2.13,scale=.86)
miniature('center_loose_train_A',1.72,1.35,1.28,ry=90,scale=.66);miniature('center_loose_train_B',1.72,1.85,1.28,ry=90,scale=.66)
# Solid printed trainsetcartons, source surfaceprint only, not equipmentgeometry substitutes.
for wall in ['left','rear']:
 for row,y in enumerate([.66,1.16,1.66,2.22]):
  for k in range(7 if wall=='left' else 5):
   x,z=(.56,.51+.805*k) if wall=='left' else (.53+.805*k,5.44)
   ry=90 if wall=='left' else 0
   box('%s_trainset_carton_%d_%d'%(wall,row,k),x,z,y+.17,.72,.34,.16,['navy','pink','green','red'][(row+k)%4],'Goods_Packaged',collider=False,yaw=YAW+ry)
   # Sourceprintfront on actual volume; slight raisedprintedface thickness. Source crop doesn't replace modeltrains above.
   xx,zz=(x+.085,z) if wall=='left' else (x,z-.085)
   box('%s_trainset_print_%d_%d'%(wall,row,k),xx,zz,y+.17,.69,.31,.002,'print%d'%((row+k)%3),'Goods_Packaged',collider=False,yaw=YAW+ry)
# Keyrings/souvenirs: actualphysicalmetalrings andhooks, separatepolycar/mascotpendants, clearbagvolume+cardback.
def torus_mesh(R=.018,r=.003,N=20,n=6):
 v=[];t=[]
 for i in range(N):
  a=2*math.pi*i/N
  for j in range(n):b=2*math.pi*j/n;v.append([(R+r*math.cos(b))*math.cos(a),(R+r*math.cos(b))*math.sin(a),r*math.sin(b)])
 for i in range(N):
  for j in range(n):a=i*n+j;b=((i+1)%N)*n+j;c=((i+1)%N)*n+(j+1)%n;d=i*n+(j+1)%n;t.extend([a,b,c,a,c,d])
 return {'vertices':v,'triangles':t}
L['key_ring_solid']=torus_mesh()
for col in range(16):
 z=2.2+col*.205
 for row in range(4):
  y=.98+row*.33;ident='keyring_%d_%d'%(col,row)
  box(ident+'_hook',3.985,z,y+.065,.12,.008,.008,'stainless','Goods_Keyrings',collider=False)
  box(ident+'_backcard',3.91,z,y,.004,.19,.13,'white','Goods_Keyrings',collider=False)
  box(ident+'_clear_sleeve',3.885,z,y-.025,.020,.16,.13,'packglass','Goods_Keyrings',collider=False)
  lib(ident+'_metalring','key_ring_solid',3.865,z,y+.037,'stainless','Goods_Keyrings',ry=-90)
  # Capsule trainpendantbody andbluewindow as source form.
  sphere(ident+'_trainpendant',3.858,z,y-.036,.017,.044,.092,['navy','blue','red','green'][col%4],'Goods_Keyrings')
  box(ident+'_silverroof',3.847,z,y-.026,.004,.014,.078,'stainless','Goods_Keyrings',collider=False)
# Downloadedtoteinstances actualsourcegeometry with explicit own materialslots; independent fitted copies.
for k in range(3):asset('downloaded_canvas_bag_%d'%k,3.84,2.30+k*.44,2.58,[.33,.37,.07],bag,{'None':'cream','None.001':'cream','None.002':'cream','None.003':'cream','*':'cream'},'DownloadedMerchandise',ry=-90,collider=False)
for k in range(3):
 for j in range(4):box('tote_giftbox_%d_%d'%(k,j),3.87,3.58+k*.34,.61+j*.05,.24,.045,.30,'cream','Goods_Bags',collider=False)
# Physicalpouchesnot labelledrectangles: roundedclothbody, zipper, stitchedtrim andpulltab.
for k in range(7):
 z=2.3+k*.37;sphere('cloth_pouch_%d'%k,1.46,z,.72,.18,.12,.055,'navy','Goods_Bags');box('pouch_zipper_%d'%k,1.46,z-.03,.778,.165,.007,.004,'stainless','Goods_Bags',collider=False);lib('pouch_zip_ring_%d'%k,'key_ring_solid',1.54,z-.033,.775,'stainless','Goods_Bags',scale=[.25,.25,.25])
# RailRo source23: helmet/head, yellowears, bluebody, redscarf, darkeyes andcurvedsmile physicalseparateparts.
def mascot(i,x,z,y,scale=1):
 g='Goods_RailRo';s=scale
 sphere(i+'_body',x,z,y+.10*s,.15*s,.19*s,.10*s,'blue',g);sphere(i+'_helmet',x,z,y+.25*s,.22*s,.21*s,.17*s,'white',g);sphere(i+'_face',x,z-.059*s,y+.237*s,.171*s,.137*s,.055*s,'skin',g)
 for sign in [-1,1]:
  sphere(i+'_ear_%s'%sign,x+sign*.108*s,z,y+.266*s,.035*s,.065*s,.065*s,'yellow',g);sphere(i+'_eye_%s'%sign,x+sign*.036*s,z-.089*s,y+.247*s,.015*s,.018*s,.009*s,'black',g);sphere(i+'_foot_%s'%sign,x+sign*.059*s,z-.035*s,y+.034*s,.075*s,.067*s,.085*s,'blue',g);sphere(i+'_hand_%s'%sign,x+sign*.091*s,z,y+.124*s,.045*s,.07*s,.055*s,'blue',g)
 box(i+'_red_scarf',x,z-.046*s,y+.162*s,.15*s,.025*s,.026*s,'red',g,collider=False);sphere(i+'_smile',x,z-.092*s,y+.211*s,.031*s,.009*s,.006*s,'red',g)
 for dx in [-.028,.028]:box(i+'_rail_logo_vertical_%s'%dx,x+dx*s,z-.060*s,y+.321*s,.009*s,.065*s,.005*s,'blue',g,collider=False)
 for yy in [.305,.333]:box(i+'_rail_logo_cross_%s'%yy,x,z-.061*s,y+yy*s,.096*s,.008*s,.005*s,'blue',g,collider=False)
for k in range(4):mascot('railro_%d'%k,1.50+(k%2)*.45,1.18+(k//2)*.72,.57,scale=.85)
# Actualphoto and originaloutline signs: physicalclosedfront/back/side plusbeveledmetalbacking.
for name,width,height in [('front',3.09,.30),('side',1.18,.34)]:
 mask=cv2.imread(str(OWN/'sign-model-authoring'/('source-%s-ink-mask.png'%name)),0)
 # exactsourcecontours, notfont norTMP.
 solid=closed_solid_from_mask(mask,width,height,.04,minimum_area=25,epsilon=.65)
 L[name+'_letters']=solid
 bevel=closed_solid_from_mask(cv2.dilate(mask,np.ones((3,3),np.uint8)),width,height,.043,minimum_area=25,epsilon=.65)
 L[name+'_bevel']=bevel
x,z,y=1.98,-.012,2.60
lib('front_source_letter_bevel','front_bevel',x,z+.007,y,'stainless','PhysicalSigns',ry=180)
lib('front_source_letter_solids','front_letters',x,z-.003,y,'letterwhite','PhysicalSigns',ry=180)
# Columnbrand source sign carried by its own sourcepanel, originalcolumnnevermodified.
box('KoreaTrainMates_sign_carrier',4.62,.16,2.58,1.26,.49,.035,'charcoal','PhysicalSigns')
lib('KoreaTrainMates_source_bevel','side_bevel',4.62,.126,2.59,'stainless','PhysicalSigns',ry=180)
lib('KoreaTrainMates_source_solids','side_letters',4.62,.121,2.59,'letterwhite','PhysicalSigns',ry=180)
for x in [.31,3.65]:cyl('fascia_fixing_%s'%x,x,.0,2.99,.018,.022,.018,'stainless','PhysicalSigns',rot=[90,YAW,0])
# Materialfield only on primitives; imported slots retainexplicitmaps.
for a in O:
 if a.get('material') is None:a.pop('material',None)
# Limit all unmeasuredmerch counts/locations to MODEL visiblefamily, notSKUinventory assertion.
dump('STORE-PACKET-R25.json',p)
source={'store_key':'railway-goods','actual_branch':'부산역 2F 2026 신축 철도기념품 GIFTSHOP','original_design':{'path':'.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/research/root/2026-goods-shop-architecture-original.pdf','sha256':'b5b375cf97740c017fa483587c4b839ccc0450741b2f6c7bc091b905152e826a','epoch':'cover2026.07/internal2026.05; original construction design, not surveyed asbuilt','pages_0based_directly_viewed':[4,6,7,10,12,13,14,20,23,24,47]},'field_sources':[{'id':'OCT2026_PHOTO','url':'https://m.blog.naver.com/funnycake5/224432769870','epoch':'2026-10-06','directly_viewed':['00','01','03','18','23','contact'],'path':'sources/ROOT-funnycake5-224432769870-INDEX.json','copyright':'Reference only; source-derivedphysicalcontours and individualproductprint crops onsolidcartons, no entirephoto sign or furnitureplane'},{'url':'https://m.blog.naver.com/viyen/224437277206','epoch':'2026-10-10','directly_viewed':['00'],'finding':'Explicitly current2F, previous1F branch not used'}],'downloaded_free_models':[{'model':'Store Shelves','author':'Peter_D','license':'CC-BY-4.0','url':'https://sketchfab.com/3d-models/store-shelves-8406794ee19b4af49e0ccc02d1132915','own_source':'sources/free-models/store-shelves/SingleFace-Shelf.fbx','source_geometry':'Actual downloaded perforatedshopracks; existing own-staged singleface derivative; independent fittedinstances/URPmetalfinish','source_preview_directly_viewed':True,'instances':16},{'model':'POS terminal','author':'Vladyslav.Yarovyi','license':'CC-BY-4.0','url':'https://sketchfab.com/3d-models/pos-terminal-91c277a25bc34c5bbd080cd1e74161cb','own_source':'sources/free-models/pos-terminal/POS-Terminal.fbx','source_geometry':'Actual downloaded device form with source normal; explicitdarkownURPperallsource slots, unrelated originalRussian UI omitted','source_preview_directly_viewed':True,'instances':1},{'model':'Tote Bag','author':'Jeremy E. Grayson','license':'CC-BY-4.0','url':'https://sketchfab.com/3d-models/tote-bag-0b088d83e5f9458d88ab923e126f519e','own_source':'sources/free-models/tote-bag/tote-bag.glb','download':'https://huggingface.co/datasets/allenai/objaverse/resolve/main/glbs/000-039/0b088d83e5f9458d88ab923e126f519e.glb','source_preview_directly_viewed':True,'adaptation':'OwnFBX derivative and creamphoto-based material. Bag folds/handlepose genericasset, not exactSKU replica.','instances':3}],'source_specific_authored_models':['modernITX/KTX closedrailcarbody withseparatewheels/windows/stripes/couplers','RailRohelmet/yellowearpieces/bluebody/redscarf/facehelmetlogo','keyringmetalrings/clearpackages/trainpendants','roundedpouches/zipperpulls','originaldimensionedisland/cabinets/counter/booth/lights/circularAC','sourcelettersclosedsolid4cmdepth/metalbevel/backing/hardware/emission'],'sign_method':'ActualOctfrontphoto rectification and rasterforeground contours; sideoriginaldesignsource tracing withdimlinesremoved andmanualcleanup, countersretained; no font/TMP/text, no wholesignphoto','material_identity':'v4 importedmaterialnames maintained; explicit ownURP mapsperallsource slots, stagedsourcesimmutable','limits':['Physical sourcevisible product families replicated; exact SKU counts/printdetails/stock not established.','Originalarchitecture dimensions and currentnative anchoring are MODEL, not surveyedmatch.','Tote sourcefolds/handles generic, sourcehiddenstockroomequipment omitted.','No newplatformfloor or nativecolumn/body modification.','No agent QA/Play/probe/reopen/capture, uservalidates.','No whole2F completion/99percent claim.']}
dump('SOURCE-EVIDENCE-R25.json',source)
# Sourcecredits importedintoactualruntimefolder throughsamelease.
credits=OWN/'sources/ATTRIBUTION-R25.txt';credits.write_text('\n'.join([m['model']+' by '+m['author']+' / '+m['license']+' / '+m['url'] for m in source['downloaded_free_models']])+'\nIndependent geometry-fit/material derivatives; original sources immutable. See SOURCE-EVIDENCE-R25.json.\n')
imp(credits,'ATTRIBUTION-R25.txt');dump('STORE-PACKET-R25.json',p)
packet=OWN/'STORE-PACKET-R25.json';scope='C180_R25_RAILWAY_GOODS_APPLY_001';s=(BASE/'StoreBuilderR25-v4-SOURCE-MATERIALS.template.cs').read_text()
for a,b in {'__STORE_KEY__':'railway-goods','__WORKER_THREAD__':'classic:/root/r25_railway_goods','__EXACT_SCOPE__':scope,'__PACKET_PATH__':str(packet.relative_to(ROOT)),'__PACKET_SHA256__':sha(packet)}.items():s=s.replace(a,b)
(OWN/'ApplyStoreR25.cs').write_text(s)
dump('PACKET-COUNTS-R25.json',{'objects':len(O),'kind_counts':{k:sum(a['kind']==k for a in O) for k in set(a['kind'] for a in O)},'downloaded_model_instances':sum(a['kind']=='asset' for a in O),'mesh_library_count':len(L),'footprint_q':foot,'yaw':YAW,'source_imports':len(p['source_imports']),'packet_sha256':sha(packet)})
print(json.dumps({'objects':len(O),'downloaded_asset_instances':sum(a['kind']=='asset' for a in O),'footprint':foot,'yaw':YAW,'packet_sha':sha(packet)}))
