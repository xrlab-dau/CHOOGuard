from pathlib import Path
import json, hashlib, shutil, math, sys, importlib.util
import numpy as np, cv2
from PIL import Image
B=Path(__file__).resolve().parent; R=B.parents[2]; COMMON=B.parent; A='Assets/ChooGuard/Art/StationInterior/R25_Shops/busan-oden'; S=B/'sources'; F=S/'free-models'; ST=S/'construction'; ST.mkdir(exist_ok=True)
sp=importlib.util.spec_from_file_location('solid',COMMON/'photo_outline_to_solid.py');solid=importlib.util.module_from_spec(sp);sp.loader.exec_module(solid)
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
imports=[]; mats={}; objs=[];lib={}
def imp(src,dest):
 src=Path(src).resolve(); imports.append({'source':str(src),'destination':dest,'sha256':sha(src)});return A+'/'+dest
# Immutable shared sources copied to own staging.
for key,file in [('pos-terminal','POS-Terminal.fbx'),('commercial-refrigerator','CommercialRefrigerator-Empty.fbx')]:
 d=ST/key;d.mkdir(exist_ok=True);shared=COMMON/'free-assets'/key
 shutil.copy2(shared/file,d/file)
 for p in (shared/'textures').glob('*.png'):
  (d/'textures').mkdir(exist_ok=True);shutil.copy2(p,d/'textures'/p.name)
for file in ['bowl.fbx','cup.fbx']:
 source=COMMON/'free-assets/kenney-food-kit/Models/FBX format'/file
 if source.exists():shutil.copy2(source,ST/file)
for name in ['Stainless-Cabinet','Stainless-Sink','Open-Stainless-Wok']:
 imp(F/(name+'.fbx'),'Models/'+name+'.fbx')
imp(ST/'pos-terminal/POS-Terminal.fbx','Models/POS-Terminal.fbx')
imp(ST/'commercial-refrigerator/CommercialRefrigerator-Empty.fbx','Models/Glass-Display-Fridge.fbx')
imp(ST/'bowl.fbx','Models/Black-Bowl.fbx')
# Source textures retain original slots with own URP PBR conversion.
def orm(src,out):
 im=Image.open(src).convert('RGB');r,g,b=im.split();Image.merge('RGBA',(b,b,b,Image.eval(g,lambda v:255-v))).save(out)
def mat(name,c,metal=0,smooth=.4,**kw):mats[name]={'color':c,'metallic':metal,'smoothness':smooth,**kw}
mat('steel',[.65,.68,.7,1],.92,.65);mat('steel_dark',[.16,.18,.19,1],.9,.58);mat('black',[.018,.02,.023,1],.05,.5);mat('warm_white',[.87,.86,.81,1],0,.3);mat('charcoal',[.07,.072,.074,1],.08,.3);mat('sign_white',[.95,.98,1,1],.04,.35,emission=[.4,.43,.45,1]);mat('logo_blue',[.06,.075,.16,1],0,.25);mat('wood',[.68,.49,.25,1],0,.35);mat('fishcake',[.62,.45,.20,1],0,.38);mat('broth',[.47,.32,.10,.93],0,.65,transparent=True);mat('redfood',[.48,.055,.016,1],0,.6);mat('glass',[.65,.76,.78,.15],0,.8,transparent=True)
for i in [0,2,3]:imp(ST/f'pos-terminal/textures/Image_{i}.png',f'Textures/POS_{i}.png')
orm(ST/'pos-terminal/textures/Image_1.png',ST/'POS-metallic_smoothness.png');imp(ST/'POS-metallic_smoothness.png','Textures/POS-metallic_smoothness.png')
mat('pos_body',[1,1,1,1],.1,.5,texture_asset=A+'/Textures/POS_0.png',normal_asset=A+'/Textures/POS_2.png',metallic_gloss_asset=A+'/Textures/POS-metallic_smoothness.png')
mat('pos_screen',[.07,.08,.09,1],0,.7,normal_asset=A+'/Textures/POS_3.png')
for i in [9,6]:imp(ST/f'commercial-refrigerator/textures/Image_{i}.png',f'Textures/Fridge_{i}.png')
for i,key in [(4,'case'),(8,'interior')]:
 out=ST/f'Fridge-{key}-metallic_smoothness.png';orm(ST/f'commercial-refrigerator/textures/Image_{i}.png',out);imp(out,'Textures/'+out.name)
mat('fridge_case',[.2,.21,.22,1],.7,.5,texture_asset=A+'/Textures/Fridge_9.png',metallic_gloss_asset=A+'/Textures/Fridge-case-metallic_smoothness.png')
mat('fridge_interior',[.8,.82,.82,1],.5,.5,texture_asset=A+'/Textures/Fridge_6.png',metallic_gloss_asset=A+'/Textures/Fridge-interior-metallic_smoothness.png')
# Native-face coordinate frame, qx,-qy.
basis=json.loads((B/'MODEL-PLACEMENT-BASIS-R25.json').read_text());O=np.array(basis['origin_q']);T=np.array(basis['frontage_unit_tangent']);N=np.array(basis['outward_normal']);YAW=math.degrees(math.atan2(N[0],N[1]))
def q(s,d):return (O+T*s-N*d).round(6).tolist()
def part(kind,id,group,s,d,y,material,**kw):
 o={'kind':kind,'id':id,'group':group,'q':q(s,d),'y':y,'yaw':YAW,'material':material,'source_ids':['FIELD_20260518_11','KITCHEN_20261005_00'],'qualification':'SOURCE_PHOTO_FIT_MODEL_NOT_SURVEYED',**kw};objs.append(o);return o
def box(id,group,s,d,y,w,h,dep,m,**kw):return part('cube',id,group,s,d,y,m,size=[w,h,dep],**kw)
def asset(name,id,group,s,d,y,fit,mmap,**kw):
 o=part('asset',id,group,s,d,y,None,asset=A+'/Models/'+name+'.fbx',fit_size=fit,material_map=mmap,**kw);o.pop('material');return o
# Actual fascia and source outlined sign; no duplicate shell/glass/doors.
box('Fascia_Closed_Charcoal_Sheet','PhysicalSigns',0,.09,2.96,5.64,.58,.095,'charcoal')
box('Fascia_Top_Aluminum_Cap','PhysicalSigns',0,.04,3.27,5.68,.05,.14,'steel_dark')
box('Fascia_Bottom_Aluminum_Cap','PhysicalSigns',0,.04,2.66,5.68,.05,.14,'steel_dark')
mask=cv2.imread(str(S/'fascia-ink-mask.png'),0);shape=solid.closed_solid_from_mask(mask,3.34,.45,.034,minimum_area=20,epsilon=.65)
lib['Actual_Fascia_White_Letter_Contours']={k:shape[k] for k in ('vertices','triangles')}
part('library_mesh','Source_Fascia_Closed_Letters_and_Logo','PhysicalSigns',-.23,.025,2.99,'sign_white',mesh_key='Actual_Fascia_White_Letter_Contours',collider=False)
# Stainless free cabinet modules underneath the observed service counter.
for i,s in enumerate([-.99,-.05,.89]):asset('Stainless-Cabinet',f'Downloaded_Stainless_Service_Base_{i:02}','ServiceCounter',s,.47,.035,[.65,.88,.91],{'METAL':'steel','*':'steel'},yaw=YAW-90)
box('Observed_Marble_Front_Apron','ServiceCounter',-.05,.105,.50,2.82,.83,.055,'warm_white')
box('Black_Stone_Counter_Front_Edge','ServiceCounter',-.05,.10,.962,2.90,.075,.09,'black')
box('Steel_Counter_South_Return','ServiceCounter',-1.47,.49,.95,.07,.10,.79,'steel')
box('Steel_Counter_North_Return','ServiceCounter',1.37,.49,.95,.07,.10,.79,'steel')
box('Steel_Counter_Actual_Back_Worktop_Strip','ServiceCounter',-.05,.845,.993,2.90,.028,.13,'steel')
box('Steel_Counter_Actual_Front_Worktop_Strip','ServiceCounter',-.05,.125,.993,2.90,.028,.095,'steel')
box('Steel_Counter_Actual_Middle_Bridge','ServiceCounter',-.16,.49,.993,.07,.028,.64,'steel')
# Proper hollow well frames, six broth compartments and actual folded skewered fish.
# Objects authored in sign-local x=along storefront (negative s), z=outward(-d).
def cylinder_between(id,group,sa,da,ya,sb,db,yb,r,material):
 qa=np.array(q(sa,da));qb=np.array(q(sb,db));v=np.array([qb[0]-qa[0],yb-ya,qb[1]-qa[1]]);length=np.linalg.norm(v);mid=(qa+qb)/2
 yaw=math.degrees(math.atan2(v[0],v[2]));pitch=math.degrees(math.acos(max(-1,min(1,v[1]/length))))
 objs.append({'kind':'cylinder','id':id,'group':group,'q':mid.tolist(),'y':(ya+yb)/2,'euler':[pitch,yaw,0],'size':[r*2,length/2,r*2],'material':material,'collider':False,'source_ids':['KITCHEN_20261005_01']})
def fish_ribbon_mesh(length=.20,width=.075):
 vertices=[];tris=[];steps=32
 for i in range(steps+1):
  u=i/steps;x=(u-.5)*length;y=.024*math.sin(u*math.pi*6)
  for dy,z in [(-.006,-width/2),(-.006,width/2),(.006,-width/2),(.006,width/2)]:vertices.append([x,y+dy,z])
 for i in range(steps):
  a=i*4;b=a+4
  for v in [(a,a+1,b+1,b),(a+2,b+2,b+3,a+3),(a,b,b+2,a+2),(a+1,a+3,b+3,b+1)]:
   u,vv,w,z=v;tris.extend([u,vv,w,u,w,z])
 tris.extend([0,2,3,0,3,1]);a=steps*4;tris.extend([a,a+1,a+3,a,a+3,a+2])
 return {'vertices':vertices,'triangles':tris}
lib['Actual_Folded_Fishcake_Sheet']=fish_ribbon_mesh()
for wi,sc in enumerate([-.78,.46]):
 w=1.17;dep=.64
 box(f'Oden_Well_{wi}_Base','OdenWells',sc,.49,.815,w,.025,dep,'steel')
 for j,ss in enumerate([sc-w/2,sc+w/2]):box(f'Oden_Well_{wi}_End_{j}','OdenWells',ss,.49,.905,.025,.18,dep,'steel')
 for j,dd in enumerate([.49-dep/2,.49+dep/2]):box(f'Oden_Well_{wi}_Long_Wall_{j}','OdenWells',sc,dd,.905,w,.18,.025,'steel')
 for j,dd in enumerate([.49-dep/2,.49+dep/2]):box(f'Oden_Well_{wi}_Rolled_Rim_{j}','OdenWells',sc,dd,1.012,w+.07,.025,.048,'steel')
 for j,ss in enumerate([sc-w/2,sc+w/2]):box(f'Oden_Well_{wi}_Rim_End_{j}','OdenWells',ss,.49,1.012,.052,.025,dep+.07,'steel')
 for j in range(3):
  ss=sc+(j-1)*w/3
  box(f'Oden_Well_{wi}_Broth_{j}','OdenWells',ss,.49,.977,w/3-.027,.009,dep-.045,'broth',collider=False)
  if j<2:box(f'Oden_Well_{wi}_Divider_{j}','OdenWells',sc+(j-.5)*w/3,.49,.914,.014,.183,dep-.02,'steel',collider=False)
  for k in range(5):
   s=ss+(k-2)*.040;d=.48+.025*math.sin(k)
   cylinder_between(f'Oden_Well_{wi}_Skewer_{j}_{k}','Skewers',s-.08,d-.15,1.045,s+.11,d+.24,1.12,.0025,'wood')
   part('library_mesh',f'Oden_Well_{wi}_Folded_Fish_{j}_{k}','Skewers',s,d,1.015+.005*k,'fishcake',mesh_key='Actual_Folded_Fishcake_Sheet',yaw=YAW+30+k*3,collider=False,source_ids=['KITCHEN_20261005_01'])
# Black serving bowls, source free actual hollow mesh.
for i in range(15):asset('Black-Bowl',f'Downloaded_Black_Bowl_Stack_{i:02}','Smallwares',1.14,.61,1.005+i*.018,[.185,.065,.175],{'*':'black'},collider=False)
# Counter POS on source-visible right, retaining its real source slots.
box('POS_Stone_Pedestal','POS',1.06,1.04,.89,.62,.88,.50,'black')
asset('POS-Terminal','Downloaded_POS_Terminal','POS',1.07,1.04,1.34,[.47,.46,.38],{'Material_2100454184':'pos_body','Material_2100454185':'pos_screen','*':'pos_body'},yaw=YAW+180)
# White marble-pattern backsplash is a thin removable finish, not a replacement native wall.
sourceim=cv2.imread(str(S/'ROOT-whmoon00-224431790085-00.jpg'));cv2.imwrite(str(ST/'actual-marble-backsplash.png'),sourceim[313:355,379:686])
tex=imp(ST/'actual-marble-backsplash.png','Textures/Actual-Marble-Backsplash.png');mat('marble_finish',[.96,.95,.94,1],0,.3,texture_asset=tex)
for rr in range(4):
 for cc in range(6):box(f'Actual_Marble_Tile_Finish_{rr}_{cc}','VisibleKitchen',-1.35+cc*.30,3.117,1.08+rr*.30,.295,.295,.013,'marble_finish',collider=False)
# Rear visible kitchen: downloaded stainless lower modules and sink + open wok.
for i,s in enumerate([-1.0,.0]):asset('Stainless-Cabinet',f'Downloaded_Rear_Prep_Cabinet_{i}','VisibleKitchen',s,2.74,.025,[.62,.89,.95],{'METAL':'steel','*':'steel'},yaw=YAW-90)
asset('Stainless-Sink','Downloaded_Actual_Hollow_Sink','VisibleKitchen',-1.02,2.74,.69,[.52,.23,.68],{'sink_metal':'steel','*':'steel'},yaw=YAW-90)
asset('Open-Stainless-Wok','Downloaded_Open_Stainless_Wok','VisibleKitchen',-.12,2.69,.915,[.47,.14,.39],{'Metal':'steel','*':'steel'},collider=False)
# Open stainless hood is modeled from actual trapezoid contour, no light components.
def frustum(wb,wt,db,dt,h):
 v=[[-wb/2,0,-db/2],[wb/2,0,-db/2],[wb/2,0,db/2],[-wb/2,0,db/2],[-wt/2,h,-dt/2],[wt/2,h,-dt/2],[wt/2,h,dt/2],[-wt/2,h,dt/2]];tr=[]
 for a,b,c,d in [(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)]:tr += [a,b,c,a,c,d]
 return {'vertices':v,'triangles':tr}
lib['Actual_Open_Trapezoid_Steel_Hood']=frustum(1.37,1.18,.78,.56,.56)
part('library_mesh','Observed_Stainless_Open_Hood','VisibleKitchen',-.53,2.65,1.98,'steel',mesh_key='Actual_Open_Trapezoid_Steel_Hood')
box('Hood_Black_Filter_Underside','VisibleKitchen',-.53,2.65,1.994,1.27,.022,.68,'steel_dark',collider=False)
for k in range(12):box(f'Hood_Filter_Slot_{k:02}','VisibleKitchen',-.53+(k-5.5)*.096,2.65,1.979,.018,.012,.61,'black',collider=False)
box('Observed_Hood_Duct','VisibleKitchen',-.53,2.65,2.60,.45,.18,.42,'steel')
# Photo-matched six-door stainless refrigerator: separate body skins, six framed doors/handles/hinges.
fs=1.18;fd=2.73;fw=2.0;fh=2.1
box('SixDoor_Refrigerator_Insulated_Body','VisibleKitchen',fs,fd,1.08,fw,fh,.67,'steel_dark')
box('SixDoor_Refrigerator_Top_Condenser','VisibleKitchen',fs,fd,2.19,fw,.20,.67,'steel')
for row in range(2):
 for col in range(3):
  ss=fs+(col-1)*.66;yy=.58+row*1.015
  box(f'SixDoor_Fridge_Gasket_{row}_{col}','VisibleKitchen',ss,2.375,yy,.65,.995,.034,'black')
  box(f'SixDoor_Fridge_Actual_Metal_Door_{row}_{col}','VisibleKitchen',ss,2.35,yy,.616,.955,.041,'steel')
  box(f'SixDoor_Fridge_Actual_Handle_{row}_{col}','VisibleKitchen',ss+.23,2.292,yy-.02,.034,.30,.035,'black',collider=False)
  for kk in [-1,1]:box(f'SixDoor_Fridge_Hinge_{row}_{col}_{kk}','VisibleKitchen',ss-.30,2.31,yy+kk*.34,.025,.065,.041,'steel_dark',collider=False)
for j in range(16):box(f'Condenser_Vent_{j:02}','VisibleKitchen',fs+(j-7.5)*.116,2.382,2.19,.063,.025,.005,'steel_dark',collider=False)
# Right source-visible black glass merchandise cabinet, downloaded actual fridge shell.
asset('Glass-Display-Fridge','Downloaded_Glass_Display_Fridge','Display',2.24,.72,.04,[.57,1.62,.60],{'FridgeCase':'fridge_case','FridgeGlass':'glass','FridgeInterior':'fridge_interior','*':'steel'})
# Source merchandise bags/food on inner shelves represented as actual folded fishcakes, no labels.
for k in range(9):part('library_mesh',f'Display_Physical_Fishcake_{k:02}','Display',2.24+(k%3-1)*.12,.70,.49+(k//3)*.32,'fishcake',mesh_key='Actual_Folded_Fishcake_Sheet',size=[.6,.6,.6],collider=False)
# Physical six-panel menu bank, source food printing separated from actual extruded ink.
im=cv2.imread(str(S/'ROOT-whmoon00-224431790085-00.jpg'))
quads=[[[60,0],[366,0],[355,183],[57,148]],[[382,0],[584,29],[578,212],[379,180]],[[600,38],[745,75],[738,244],[593,211]],[[754,80],[865,108],[858,272],[747,243]],[[875,114],[965,138],[965,282],[867,273]]]
for i in range(6):
 ss=(i-2.5)*.465;box(f'Physical_Menu_Frame_{i+1}','MenuPanels',ss,.96,2.36,.455,.47,.065,'black')
 menuim=im;quad=quads[i] if i<5 else [[404,260],[447,257],[447,312],[406,313]]
 if i==5:menuim=cv2.imread(str(S/'field-11-20260518-2F.jpg'))
 rect=cv2.warpPerspective(menuim,cv2.getPerspectiveTransform(np.float32(quad),np.float32([[0,0],[400,0],[400,400],[0,400]])),(400,400))
 hsv=cv2.cvtColor(rect,cv2.COLOR_BGR2HSV);ink=cv2.inRange(hsv,np.array([0,0,175]),np.array([179,120,255]));rows=np.zeros((400,400),np.uint8)
 # Only actual source text bands, avoiding photograph of bowls/food highlights.
 if i==0:rows[278:360]=255
 elif i in [1,2]:rows[:58]=255;rows[285:360]=255
 else:rows[:75]=255;rows[270:370]=255
 ink=cv2.bitwise_and(ink,rows);ink[:5]=0;ink[-5:]=0;ink[:,:5]=0;ink[:,-5:]=0
 cv2.imwrite(str(ST/f'menu-{i+1}-actual-ink-mask.png'),ink)
 # Only source food illustration retained as physical print, lettering rebuilt as solid ink contours.
 printed=cv2.inpaint(rect,cv2.dilate(ink,np.ones((3,3),np.uint8)),3,cv2.INPAINT_TELEA);cv2.imwrite(str(ST/f'menu-{i+1}-food-print.png'),printed)
 tex=imp(ST/f'menu-{i+1}-food-print.png',f'Textures/Menu-{i+1}-Food-Print.png');mat(f'menu_{i+1}',[1,1,1,1],0,.3,texture_asset=tex)
 # Plane UV orientation from outward side (source image-left is local+X).
 verts=[[-.209,-.215,0],[.209,-.215,0],[.209,.215,0],[-.209,.215,0]];tri=[0,1,2,0,2,3,2,1,0,3,2,0]
 part('mesh',f'Menu_{i+1}_Physical_Food_Print_Surface','MenuPanels',ss,.916,2.36,f'menu_{i+1}',vertices=verts,triangles=tri,uv=[[1,0],[0,0],[0,1],[1,1]],collider=False)
 sh=solid.closed_solid_from_mask(ink,.418,.43,.0015,minimum_area=5,epsilon=.7);lib[f'menu_{i+1}_actual_ink']={k:sh[k] for k in ['vertices','triangles']}
 part('library_mesh',f'Menu_{i+1}_Actual_Closed_Contour_Ink','MenuPanels',ss,.912,2.36,'sign_white',mesh_key=f'menu_{i+1}_actual_ink',collider=False)
packet={'store_key':'busan-oden','origin_q':O.tolist(),'floor_world_y':7.0,'source_packet':str(B/'SOURCE-EVIDENCE-R25.json'),'materials':mats,'source_imports':imports,'mesh_library':lib,'objects':objs,'no_new_enclosure':True,'no_scene_lights':True,'model_dimensions_qualified':True}
(B/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
# Source and license metadata compact evidence. Credits imported under own lease too.
credits=['Busan Oden R25 physical free-model credits','Stainless Steel Cupboards for Commercial Kitchen by mdv_mlhtr_dev_, CC BY 4.0, https://sketchfab.com/3d-models/a23753fc573346cdb72f6709e974b798','Stainless Steel Wok by Aullwen, CC BY 4.0, https://sketchfab.com/3d-models/ad73e31b3f4543de9a6f42d9ff3e3f1c','POS terminal by Vladyslav.Yarovyi, CC BY 4.0, https://sketchfab.com/3d-models/91c277a25bc34c5bbd080cd1e74161cb','Commercial Fridge by Sean Thomas (foon), Khronos contributors, CC BY 4.0, https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/CommercialRefrigerator','Kenney Food Kit bowl, CC0, https://kenney.nl/assets/food-kit','Models independently fitted/recolored; cabinet/sink and open wok derivatives retain source object recipes. No exact SKU or surveyed dimensions asserted.']
(ST/'FREE-MODEL-CREDITS.txt').write_text('\n'.join(credits));imp(ST/'FREE-MODEL-CREDITS.txt','FREE-MODEL-CREDITS.txt');packet['source_imports']=imports;(B/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
photos=[{'id':'FIELD_20260518_11','url':'https://m.blog.naver.com/djagytt/224289573890','date':'2026-05-18','local':str(S/'field-11-20260518-2F.jpg'),'directly_viewed':True,'observed':'Actual 2F combined frontage 유부주머니&부산오뎅 beside 경주빵찰보리빵; open kitchen servicecounter, sliding aluminum glass and fascia retained; native room reused.'}]
idx=json.loads((S/'ROOT-whmoon00-224431790085-INDEX.json').read_text())
for x in idx['images'][:2]:photos.append({'id':'KITCHEN_20261005_'+str(x['index']).zfill(2),'url':idx['url'],'image_url':x['url'],'date':'2026-10-05','local':x['path'],'sha256':x['sha256'],'directly_viewed':True,'observed':'Stainless hood, POS, multi-door steel rear fridge, source framed menu panels, twin hollow oden wells, skewers, folded fishcakes, black bowls.'})
evidence={'store_key':'busan-oden','classification':'directly_supported_source_visible_form_with_MODEL_fit','photos':photos,'original_plan_search':{'queries':['부산역 부산오뎅 설계','부산역 부산오뎅 도면','부산역 부산오뎅 선상 설계'],'result':'No exact original floor/ceiling/elevation/furniture plans located; common commercial map supplies tenant identity only.'},'branch_binding':'May2026 original fieldwalk explicitly2F; fascia matches current selected26 source identity and native south room next to selected25. October kitchen morphology matches same open counter, but post itself does not state floor. Current exact shop SKU/metric match not asserted.','models':[{'identity':c,'source_immutable':True} for c in credits[1:-1]],'source_model_derivatives':str(F/'SOURCE-DERIVATIVE-RECIPES.json'),'asset_copies':'Own staging and own Asset path only','no_equipment_photo_proxies':True,'sign_method':'Actual field-photo ink extraction; closed front/back/side meshes with counters; physical charcoal sheet/frame. No font/TMP objects. Menu source ink is extruded separately from food print surface.','limits':['No surveyed equipment or sign dimensions.','Original exact branch design drawings not located.','Sixth menu content follows May2026 visible print because its face is outside October crop; current readable content is not confirmed.','Hidden BOH and exact products/SKUs are unknown, no hidden additions.','No Scene QA/Play/probes/reopen/captures; user validates.']}
(B/'SOURCE-EVIDENCE-R25.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2))
print('packet',len(objs),'objects',len(imports),'imports',len(lib),'mesh prototypes',sha(B/'STORE-PACKET-R25.json'))
