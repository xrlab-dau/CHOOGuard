from pathlib import Path
import json,hashlib,math,sys,shutil,cv2,numpy as np
from PIL import Image
P=Path(__file__).resolve().parent;R=P.parent;S=P/'sources';C=S/'construction';ROOT=P.parents[2];AS='Assets/ChooGuard/Art/StationInterior/R25_Shops/yeongjudang'
sys.path.insert(0,str(R));from photo_outline_to_solid import closed_solid_from_mask
objects=[];imports=[];mats={};lib={}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def imp(p,d):
 p=Path(p).resolve();imports.append({'source':str(p),'destination':d,'sha256':sha(p)});return AS+'/'+d
def mat(k,c,**kw):mats[k]={'color':c,'smoothness':.35,**kw}
# Source files from other constructors already copied into own immutable staging.
models={
 'shelf':imp(C/'modern_shelf/ShelfNoLegsOwn.fbx','Models/ShelfNoLegsOwn.fbx'),
 'cubbies':imp(C/'modern_shelf/ShelfThreeCubbiesOwn.fbx','Models/ShelfThreeCubbiesOwn.fbx'),
 'case':imp(C/'case/Cake-Display.fbx','Models/Cake-Display.fbx'),
 'counter':imp(C/'counter/counter-source-unity.fbx','Models/Counter.fbx'),
 'basket':imp(C/'wicker/WickerBodyOwn.fbx','Models/WickerBodyOwn.fbx'),
 'cake':imp(C/'financier/financier-own.fbx','Models/Financier.fbx'),
 'wrapped':imp(C/'wrapped-biscuit/wrapped-biscuit.fbx','Models/WrappedBiscuit.fbx'),
 'gift':imp(C/'giftbox/giftbox-own.fbx','Models/GiftCarton.fbx'),
 'pos':imp(C/'pos/POS-Terminal.fbx','Models/POS-Terminal.fbx'),
 'fridge':imp(C/'fridge/CommercialRefrigerator-Empty.fbx','Models/CommercialRefrigerator-Empty.fbx')}
for name in ['bag-flat','bottle','soda-bottle']:
 pp=R/'free-assets/kenney-food-kit/Models/FBX format'/f'{name}.fbx'
 if pp.exists():models[name]=imp(pp,f'Models/CC0-{name}.fbx')
wood={}
for f,key in [('diff.jpg','texture_asset'),('normal.jpg','normal_asset'),('metallic_smoothness.png','metallic_gloss_asset')]:wood[key]=imp(C/'wood-pbr'/f,'PBR/Wood/'+f)
for name,c in [('wood',[.68,.54,.39,1]),('darkwood',[.43,.32,.22,1]),('lightwood',[.80,.67,.48,1])]:mat(name,c,**wood,smoothness=.30)
mat('charcoal',[.035,.042,.057,1],metallic=.15,smoothness=.32);mat('black',[.018,.02,.022,1],metallic=.20,smoothness=.44)
mat('steel',[.66,.69,.70,1],metallic=.88,smoothness=.58);mat('white',[.96,.95,.89,1],smoothness=.23)
mat('glass',[.80,.91,.95,.12],transparent=True,smoothness=.96);mat('film',[.97,.97,.90,.13],transparent=True,smoothness=.76)
mat('cream',[.87,.70,.33,1],smoothness=.12);mat('red',[.60,.20,.075,1],emission=[.12,.015,.003,1])
mat('signwhite',[1,.99,.94,1],emission=[3.3,3.5,3.6,1],smoothness=.62);mat('warmglow',[1,.9,.65,1],emission=[2.3,1.7,.9,1])
for name,c in [('ivory',[.91,.85,.71,1]),('carton',[.66,.48,.28,1]),('pink',[.78,.46,.43,1]),('lavender',[.54,.48,.59,1]),('yellow',[.85,.63,.13,1]),('navy',[.045,.035,.03,1])]:mat(name,c,smoothness=.18)
mat('wicker',[1,1,1,1],texture_asset=imp(C/'wicker/wicker_basket_02_diff_1k.jpg','PBR/Wicker/diff.jpg'),smoothness=.15)
# Reuse independently copied original scan map, not another store runtime assets.
fc=S/'construction/Financier-albedo.png';shutil.copy2(R/'starlight-sand/sources/models/financier/textures/Image_0.png',fc)
mat('baked',[1,.88,.59,1],texture_asset=imp(fc,'PBR/Cake/albedo.png'),smoothness=.18)
mat('posbody',[.76,.77,.75,1],texture_asset=imp(C/'pos/textures/Image_0.png','PBR/POS/albedo.png'),normal_asset=imp(C/'pos/textures/Image_2.png','PBR/POS/normal.png'),metallic=.18,smoothness=.55)
mat('posscreen',[.04,.055,.065,1],normal_asset=imp(C/'pos/textures/Image_3.png','PBR/POS/screen_normal.png'),smoothness=.7)
# Exact original refrigerator slots retained with separate case/interior/glass maps.
mat('fridgecase',[1,1,1,1],texture_asset=imp(C/'fridge/textures/Image_9.png','PBR/Fridge/case-albedo.png'),normal_asset=imp(C/'fridge/textures/Image_7.png','PBR/Fridge/case-normal.png'),metallic_gloss_asset=imp(C/'fridge/textures/URP_Case_MetallicSmoothness.png','PBR/Fridge/case-metallic_smoothness.png'))
mat('fridgeinner',[1,1,1,1],texture_asset=imp(C/'fridge/textures/Image_8.png','PBR/Fridge/inner-albedo.png'),normal_asset=imp(C/'fridge/textures/Image_6.png','PBR/Fridge/inner-normal.png'),metallic_gloss_asset=imp(C/'fridge/textures/URP_Interior_MetallicSmoothness.png','PBR/Fridge/inner-metallic_smoothness.png'))
for name in ['walnut','egg']:mat('menu_'+name,[1,1,1,1],texture_asset=imp(S/f'menu-{name}-print.png',f'Prints/menu-{name}.png'),smoothness=.38)
def add(id,kind,x,z,y,ma=None,size=None,yaw=0,group='Fitout',source=None,**kw):
 o={'id':id,'kind':kind,'q':[x,z],'y':y,'yaw':yaw,'group':group,'source_ids':source or ['JUNE26-04','JUNE26-11'],'qualification':'PHOTO_BOUND_MODEL_FIT_NOT_SURVEYED'}
 if ma:o['material']=ma
 if size:o['size']=size
 o.update(kw);objects.append(o);return o
def box(id,x,z,y,s,ma='wood',**kw):return add(id,'box',x,z,y,ma,s,**kw)
def asset(id,key,x,z,y,fit,mm,yaw=0,group='DownloadedFixtures',source=None,**kw):return add(id,'asset',x,z,y,yaw=yaw,group=group,source=source,asset=models[key],fit_size=fit,material_map=mm,**kw)
def cyl(id,x,z,y,d,h,ma,**kw):return add(id,'cylinder',x,z,y,ma,[d,h/2,d],**kw)
# Fixed native room is reused. Exact current innerfaces; no new wall/glazing shell.
x0=-45.395475;x1=-37.175574;front=-24.659072;rear=-30.281999
# Own attached fascia is inset behind current frontage; lettering front sits outward without floor intrusion.
box('AttachedCharcoalFascia',-41.2855,-24.710,2.91,[8.08,.58,.075],'charcoal',group='PhysicalSigns',source=['JUNE26-02','JUL26-14'])
for i in range(5):box(f'FasciaLouvre{i}',-41.2855,-24.670,3.25+i*.048,[8.08,.022,.034],'black',group='PhysicalSigns',collider=False)
mask=cv2.imread(str(S/'sign-fascia-ink-mask.png'),0)
solid=closed_solid_from_mask(mask,3.72,.70,.034,minimum_area=15,epsilon=.65);lib['fascia_source_letters']={k:solid[k] for k in ['vertices','triangles']}
add('ActualFasciaLettersClosedSolids','library_mesh',-40.7,-24.604,2.90,'signwhite',mesh_key='fascia_source_letters',group='PhysicalSigns',source=['JUNE26-02'],collider=False)
for i,x in enumerate([-42.30,-40.75,-39.20]):box(f'SignStandoff{i}',x,-24.660,2.87,[.027,.035,.065],'steel',group='PhysicalSigns',collider=False)
# Front gift area is on the photograph-right (low qx) side; source entrance pane remains clear.
asset('SourceWoodWindowThreeTierRack','shelf',-44.0,-25.12,.16,[2.48,1.32,.43],{'*':'wood'},group='GiftShelving',source=['JUNE26-04','FEB26-08'])
# Wall-side cabinets and source downloaded shelving adapted to three display surfaces above a closed base.
for i,z in enumerate([-26.35,-27.65,-29.14]):
 asset(f'SourceSideBaseCabinet{i}','counter',-44.73,z,0,[1.18,.70,.53],{'*':'wood'},yaw=90,group='GiftShelving',source=['JUNE26-04','FEB26-06','FEB26-07'])
 asset(f'SourceSideUpperShelf{i}','cubbies',-44.73,z,.70,[1.18,2.02,.44],{'*':'wood'},yaw=90,group='GiftShelving',source=['JUNE26-04','FEB26-06','FEB26-07'])
 for yy in [1.40,2.05,2.70]:box(f'SideShelfStrip_{i}_{yy}',-44.48,z,yy,[.014,.016,1.06],'warmglow',group='GiftShelving',collider=False)
# Back source-visible wall cabinet only; no invented hidden kitchen.
for i,x in enumerate([-43.90,-42.65]):
 asset(f'RearBaseCabinet{i}','counter',x,-29.91,0,[1.18,.70,.50],{'*':'wood'},group='GiftShelving')
 asset(f'RearUpperShelf{i}','cubbies',x,-29.91,.70,[1.18,2.02,.43],{'*':'wood'},group='GiftShelving')
# Downloaded counter and curved bakery case at photograph-left; exact SKU qualified.
asset('DownloadedCashCounter','counter',-39.38,-25.59,0,[3.04,.91,.72],{'*':'wood'},group='SalesEquipment',source=['FEB26-00','JUNE26-02'])
case_map={'*':'steel','cake_display_UV':'steel'}
for ob in json.loads((C/'case/MODEL-MATERIALS.json').read_text())['objects']:
 n=ob['name'];ll=n.lower()
 if any(v in ll for v in ['lh_front','rh_front','lh_side','rh_side','fridge_back','_shelf','roll_front']):case_map[n]='glass'
 if 'light' in ll:case_map[n]='warmglow'
asset('DownloadedCurvedWarmCakeCase','case',-38.55,-25.57,.91,[1.74,1.18,.71],case_map,group='SalesEquipment',source=['FEB26-00','JUNE26-12'])
# Distinct frontdrink cooler copied from actual source, with modern source material identities kept.
fm={'FridgeCase':'fridgecase','FridgeInterior':'fridgeinner','FridgeGlass':'glass','Glass':'glass','*':'fridgeinner'}
asset('DownloadedChilledDrinksCase','fridge',-40.30,-25.55,.10,[.78,1.58,.63],fm,group='SalesEquipment',source=['FEB26-05','JUNE26-10'])
asset('DownloadedTouchPOSTerminal','pos',-40.86,-25.55,.94,[.34,.41,.30],{'Material_2100454184':'posbody','Material_2100454185':'posscreen','*':'posbody'},yaw=180,group='SalesEquipment',source=['FEB26-00','JUNE26-09'])
# Physical counter-end illuminated panel, with separate photo-contour letter extrusions.
box('CounterEndSignFrame',-37.70,-26.24,2.12,[.62,.91,.09],'black',group='PhysicalSigns',source=['FEB26-00'])
box('CounterEndSignLightbox',-37.70,-26.180,2.12,[.55,.83,.015],'signwhite',group='PhysicalSigns',source=['FEB26-00'],collider=False)
a=cv2.imread(str(S/'counter-sign-rectified.png'));m=cv2.imread(str(S/'counter-sign-red-mask.png'),0);g=cv2.cvtColor(a,cv2.COLOR_BGR2GRAY);m[30:120,115:290]=(g[30:120,115:290]<230).astype('uint8')*255
# Header source is small; retain observed contour, no invented font.
cv2.imwrite(str(S/'counter-sign-red-mask.png'),m);ss=closed_solid_from_mask(m,.52,.78,.012,minimum_area=6,epsilon=.5);lib['counter_sign_letters']={k:ss[k] for k in ['vertices','triangles']}
add('CounterSignActualRedClosedLetters','library_mesh',-37.70,-26.164,2.12,'red',mesh_key='counter_sign_letters',group='PhysicalSigns',source=['FEB26-00'],collider=False)
# Genuine source promotional panels remain printed information on independent framed solid boards.
for j,(x,name) in enumerate([(-43.02,'walnut'),(-44.12,'egg')]):
 box(f'PrintFrame{j}',x,-24.83,2.11,[1.04,.86,.035],'darkwood',group='MenuPrintPanels',source=['JUNE26-11'])
 # Photo extraction aspect fitted to actual panel only; editable physical panel and surface separate.
 vs=[[.49,-.395,0],[-.49,-.395,0],[-.49,.395,0],[.49,.395,0]]
 add(f'ActualSourceMenuPrint{j}','mesh',x,-24.802,2.11,'menu_'+name,vertices=vs,triangles=[0,2,1,0,3,2],uv=[[0,0],[1,0],[1,1],[0,1]],group='MenuPrintPanels',source=['JUNE26-11'],collider=False)
# Physical merchandise: downloaded pouches/wrapped biscuits/cartons/baskets and scanned bread.
def pouch(id,x,z,y,yaw=0):
 asset(id,'bag-flat',x,z,y,[.19,.30,.072],{'*':'film'},yaw=yaw,group='PackagedGoods',source=['JUNE26-05','JUNE26-07','FEB26-08'],collider=False)
 # Actual opaque pouch back and contents create a 3D package; no product text labels.
 ang=math.radians(yaw);sx=math.sin(ang);cx=math.cos(ang)
 box(id+'_WhiteBack',x-sx*.018,z-cx*.018,y+.14,[.17,.26,.016],'ivory',yaw=yaw,group='PackagedGoods',collider=False)
 for k in range(3):asset(id+'_Cookie'+str(k),'cake',x+cx*(k-1)*.035+sx*.013,z-sx*(k-1)*.035+cx*.013,y+.035+k*.05,[.075,.026,.055],{'*':'baked'},yaw=90*(k%2),group='PackagedGoods',collider=False)
for k,z in enumerate([-26.10,-26.47,-26.82,-27.37,-27.72,-28.07,-28.82,-29.17,-29.52]):
 for j,y in enumerate([.78,1.43,2.08]):pouch(f'SidePouch{k}_{j}',-44.43,z,y,90)
for k,x in enumerate([-44.98,-44.63,-44.28,-43.93,-43.58,-43.23]):pouch(f'FrontPouch{k}',x,-25.06,.23,0)
# Oval shallow wicker trays retain original woven topology; the lid is excluded.
for k,(x,z,y) in enumerate([(-44.72,-25.13,.84),(-43.75,-25.13,.84),(-44.1,-25.12,1.50),(-43.90,-29.80,1.43)]):
 asset(f'DownloadedWickerTray{k}','basket',x,z,y,[.48,.105,.31],{'wicker_basket_02':'wicker','*':'wicker'},group='GiftMerchandise',source=['JUNE26-06','JUNE26-08','FEB26-08'],collider=False)
 for j in range(5):asset(f'TrayWrappedBiscuit{k}_{j}','wrapped',x-.17+j*.084,z,y+.09,[.075,.055,.15],{'*':'carton'},group='GiftMerchandise',collider=False)
for row,y in enumerate([.23,.84,1.50]):
 for k in range(5):
  x=-44.92+k*.44
  asset(f'GiftCarton{row}_{k}','gift',x,-25.13,y,[.31,.13,.23],{'*':['carton','navy','yellow','pink','ivory'][k]},group='GiftMerchandise',source=['JUNE26-04','JUNE26-08','FEB26-08'],collider=False)
# Cloth parcels: actual source gift boxes remain as volume, modeled cloth cover/bow adapt observations.
for k,(x,z,y) in enumerate([(-44.8,-25.11,1.50),(-43.21,-25.11,.84),(-43.26,-29.80,1.43)]):
 ma=['pink','lavender','ivory'][k];asset(f'DownloadedClothGiftVolume{k}','gift',x,z,y,[.27,.25,.24],{'*':ma},group='GiftMerchandise',source=['JUNE26-04','JUNE26-11'],collider=False)
 add(f'ClothGather{k}','sphere',x,z+.06,y+.24,ma,[.14,.09,.12],group='GiftMerchandise',collider=False)
 for side in [-1,1]:add(f'ClothBow{k}_{side}','sphere',x+side*.06,z+.07,y+.27,ma,[.12,.06,.045],euler=[0,0,side*25],group='GiftMerchandise',collider=False)
# Cakes on separate wire trays inside three observed warm cabinet levels.
for level,y in enumerate([1.07,1.40,1.73]):
 box(f'WireTrayBase{level}',-38.55,-25.52,y,[1.53,.015,.47],'steel',group='WarmBread',collider=False)
 for wire in range(18):box(f'WireGrid{level}_{wire}',-39.25+wire*.082,-25.51,y+.016,[.004,.005,.45],'steel',group='WarmBread',collider=False)
 for k in range(7):
  x=-39.20+k*.215
  asset(f'DownloadedCakeBottom{level}_{k}','cake',x,-25.45,y+.02,[.177,.040,.128],{'*':'baked'},group='WarmBread',source=['JUNE26-12'],collider=False)
  box(f'FilledCakeCream{level}_{k}',x,-25.45,y+.065,[.15,.024,.105],'cream',group='WarmBread',collider=False)
  asset(f'DownloadedCakeTop{level}_{k}','cake',x,-25.45,y+.077,[.177,.040,.128],{'*':'baked'},group='WarmBread',source=['JUNE26-12'],collider=False)
# Bottled drinks in real downloaded beverage cabinet source as observed, without invented brand labels.
for row,y in enumerate([.34,.70,1.08]):
 for k in range(4):
  if 'soda-bottle' in models:asset(f'DownloadedBottledDrink{row}_{k}','soda-bottle',-40.55+k*.16,-25.30,y,[.095,.25,.095],{'*':['ivory','yellow','carton','pink'][k]},group='ChilledDrinks',source=['FEB26-05','JUNE26-10'],collider=False)
# Photo-observed white ceiling/track fixtures only in own native room. No public/platform projection.
box('VisibleCeilingFront',-41.2855,-26.33,3.09,[7.98,.055,2.94],'white',group='Ceiling',source=['JUNE26-04','FEB26-00'])
box('VisibleCeilingBack',-41.2855,-29.47,3.09,[7.98,.055,1.02],'white',group='Ceiling',source=['JUNE26-04'])
box('VisibleCeilingColumnNotchBand',-41.2855,-28.38,3.09,[7.51,.055,1.16],'white',group='Ceiling',source=['JUNE26-04'],qualification='MODEL_CEILING_NOTCHED_TO_PRESERVE_BOTH_ACTUAL_COLUMN_BODIES')
for row,x in enumerate([-44.48,-38.22]):
 box(f'SpotTrack{row}',x,-27.38,2.995,[.04,.035,4.65],'white',group='Ceiling',collider=False)
 for k,z in enumerate([-25.60,-26.62,-27.65,-28.68,-29.47]):
  cyl(f'SpotHousing{row}_{k}',x,z,2.84,.10,.25,'white',group='Lighting',collider=False,euler=[20*(-1 if row==0 else 1),0,0])
  cyl(f'SpotEmitter{row}_{k}',x,z+.04,2.72,.075,.012,'warmglow',group='Lighting',collider=False)
# Latest explicit user instruction: no Scene Light components; source fixture meshes/emission retained.
credits='''YEONGJUDANG R25 source-adapted models. Exact brand or manufacturer identity and dimensions are not surveyed.
Modern Small Shelf by Blender3D, CC-BY4.0. https://sketchfab.com/3d-models/modern-small-shelf-dec75836e8c14c179be89e4420fa7a97 . Own axis/palette adaptation.
Canarian Cafe cake display by Lanzaman (lanzaboy), CC-BY4.0. https://sketchfab.com/3d-models/867570f94d654385bb9750248849892b . Own fit/palette adaptation; public source body retained.
Industrial Kitchen Bun Counter by siyarasm, CC-BY4.0. https://sketchfab.com/3d-models/industrial-kitchen-bun-counter-50a9dc05104048ffba83eca72a003873 . Existing downloaded empty counter derivative; own fit/palette.
Fig Financier by alban, CC-BY4.0. https://sketchfab.com/3d-models/fig-financier-1699cdf272374a19997e6692541c4d1b . Copied baked blank derivative and scan color, not actual store SKU.
Wicker Basket02 by Kuutti Siitonen, PolyHaven CC0. https://polyhaven.com/a/wicker_basket_02 . Own original body-only shallow tray derivative.
POS terminal by Vladyslav.Yarovyi, CC-BY4.0. https://sketchfab.com/3d-models/91c277a25bc34c5bbd080cd1e74161cb . Palette/source-slot adaptation, not actual kiosk SKU.
Commercial Fridge by Sean Thomas (foon), Khronos contributors, CC-BY4.0. https://sketchfab.com/3d-models/2174e1e4f1f24f1a95aa110ee060f473 . Empty source derivative and own fitted drink case.
Kenney Food Kit pouches/beverage bottles CC0. https://kenney.nl/assets/food-kit . Own material and contents adaptation.
Gift box by Multipainkiller Studio, CC-BY4.0. https://sketchfab.com/3d-models/gift-box-9aadeeb6635440af88606903a06950d8 . Own plain carton derivative.
Biscuit by Hannah Sapiee, CC-BY4.0. https://sketchfab.com/3d-models/biscuit-673c52eef7014c50ac13e6edda4a422f . Own palette adaptation.
PolyHaven wood_table_001 PBR CC0. https://polyhaven.com/a/wood_table_001 .
Field photographs are research/physical outline extraction; no whole photo signage/equipment stand-ins.
'''
(S/'R25-SOURCE-CREDITS.txt').write_text(credits);imp(S/'R25-SOURCE-CREDITS.txt','R25-SOURCE-CREDITS.txt')
# Retain fuller source provenance beside runtime import, within own active lease.
for n in ['flowers','somidam','starlight-sand']:imp(C/f'SOURCE-{n}.json',f'Attribution/SOURCE-{n}.json')
packet={'store_key':'yeongjudang','origin_q':[0,0],'floor_world_y':7,'source_packet':str(P/'SOURCE-EVIDENCE-R25.json'),'materials':mats,'source_imports':imports,'mesh_library':lib,'objects':objects,'no_scene_light_components':True,'scene_light_user_deletion_authority':'../ACTIVE-DELETE-ALL-SCENE-LIGHTS-20261019/USER-AUTHORITY.json','no_text_proxy':True,'no_photo_sign_plane':True,'native_floor_columns_shell_preserved':True,'qualification':'PHOTO_BOUND_MODEL_FIT_NOT_SURVEYED_NO_COMPLETE_DIGITAL_TWIN_CLAIM'}
(P/'STORE-PACKET-R25-NO-LIGHTS-003.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
(P/'SIGN-SOURCE-SOLID-BASIS.json').write_text(json.dumps({'primary':{k:v for k,v in solid.items() if k not in ['vertices','triangles']},'secondary':{k:v for k,v in ss.items() if k not in ['vertices','triangles']},'mask_files':['sources/sign-fascia-ink-mask.png','sources/counter-sign-red-mask.png'],'outward_q_normal':[0,1],'brand_text_component':False,'whole_photo_visible_sign':False},indent=2))
print(json.dumps({'objects':len(objects),'imports':len(imports),'asset_instances':sum(a['kind']=='asset' for a in objects),'materials':len(mats),'packet_sha':sha(P/'STORE-PACKET-R25-NO-LIGHTS-003.json')}))
