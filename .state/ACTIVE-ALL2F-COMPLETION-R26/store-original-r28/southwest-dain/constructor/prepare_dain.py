from pathlib import Path
import json,hashlib,sys,importlib.util,math
import cv2,numpy as np
ROOT=Path('/Users/um-yunsang/CHOOGuard'); W=Path(__file__).resolve().parent
P=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25'; S=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/public/southwest-field-identity/sources'
spec=importlib.util.spec_from_file_location('solid',P/'photo_outline_to_solid.py');solid=importlib.util.module_from_spec(spec);spec.loader.exec_module(solid)
objects=[];mats={};library={}
def mat(k,c,metal=0,smooth=.3,transparent=False):mats[k]={'color':c,'metallic':metal,'smoothness':smooth,'transparent':transparent}
mat('white',[.92,.92,.90,1],0,.45);mat('black',[.018,.021,.019,1],.12,.4);mat('silver',[.51,.52,.50,1],.75,.45);mat('gray',[.26,.28,.27,1],.4,.3);mat('olive',[.26,.29,.20,1],0,.24);mat('wood',[.47,.32,.18,1],0,.32);mat('box',[.83,.72,.54,1],0,.22);mat('bread',[.57,.29,.09,1],0,.25);mat('barley',[.78,.53,.25,1],0,.22);mat('powder',[.89,.84,.74,1],0,.18);mat('glass',[.72,.82,.83,.16],0,.85,True);mat('ink_white',[.98,.98,.96,1],0,.2);mat('red',[.64,.015,.025,1],0,.3);mat('brownink',[.31,.12,.07,1],0,.18);mat('blueboard',[.015,.21,.31,1],0,.28)
flower_materials=json.loads((P/'flowers/FLOWERS-PHYSICAL-FREE-ASSET-PACKET-R25.json').read_text())['materials']
for key in ['plant01_leaves','plant01_pot','potted_plant_04']:mats[key]=flower_materials[key]
source_ids=['DAILY20240904_16','DAIN_SOURCE_FIELD00','DININGCODE20240324']
def part(k,id,g,q,y,m=None,**kw):
 o={'kind':k,'id':id,'group':g,'q':q,'y':y,'yaw':.22415,'source_ids':source_ids,'qualification':'PHOTO_MODEL_2022_2024_GUIDE2026_NOT_SURVEYED',**kw}
 if m is not None:o['material']=m
 objects.append(o);return o
def box(id,g,q,y,size,m,**kw):return part('cube',id,g,q,y,m,size=size,**kw)
def asset(path,id,g,q,y,fit,mmap=None,**kw):
 o=part('asset',id,g,q,y,asset=path,fit_size=fit,**kw)
 if mmap:o['material_map']=mmap
 return o
# Source-backed seating in the exact empty lower L leg. Original shell/glass/doors remain native.
chair='Assets/ChooGuard/Art/StationInterior/R25_Shops/story-lamant/FreeModels/FieldPlywoodChromeChair_MODEL.fbx'
table='Assets/ChooGuard/Art/StationInterior/R25_Shops/story-lamant/FreeModels/CafeTableSteelBase.fbx'
for i,yy in enumerate([-38.55,-36.75]):
 asset(table,f'Downloaded_Table_Steel_Base_{i}','Seating',[-17.63,yy],.02,[.57,.70,.57],{'*':'black'})
 box(f'Observed_Square_Wood_Tabletop_{i}','Seating',[-17.63,yy],.745,[.70,.035,.70],'wood')
 for j,xx in enumerate([-18.38,-16.89]):
  asset(chair,f'Downloaded_Formed_Back_Green_Chair_{i}_{j}','Seating',[xx,yy],.025,[.47,.84,.49],{'Object_2':'black','Object_3':'black','Object_4':'olive','*':'olive'},yaw=90.22415 if j==0 else -89.77585)
# Narrow timber eating ledge visible along the internal back wall; open below, no fabricated enclosure.
box('Observed_Wall_Eating_Ledge','Seating',[-19.36,-37.65],.94,[.34,.035,3.72],'wood')
for i,yy in enumerate([-39.10,-37.65,-36.20]):box(f'Ledge_Open_Steel_Leg_{i}','Seating',[-19.36,yy],.47,[.033,.91,.033],'black')
# Visible coffee workstation and the historical photographed tall gray cold cabinet, lower-leg only.
box('White_Coffee_Base_Body','CoffeeStation',[-17.34,-35.46],.46,[1.18,.88,.63],'white')
box('Black_Coffee_Worktop','CoffeeStation',[-17.34,-35.46],.925,[1.23,.045,.65],'black')
for j,xx in enumerate([-17.63,-17.05]):
 box(f'Coffee_Cabinet_Door_{j}','CoffeeStation',[xx,-35.795],.47,[.555,.80,.025],'white')
 box(f'Coffee_Cabinet_Handle_{j}','CoffeeStation',[xx+.20,-35.819],.62,[.08,.018,.025],'silver',collider=False)
coffee='Assets/ChooGuard/Art/StationInterior/R25_Shops/pascucci/Models/CommercialCoffeeMachine.fbx'
asset(coffee,'Downloaded_Black_Commercial_Coffee_Machine','CoffeeStation',[-17.50,-35.45],.95,[.68,.49,.49],{'*':'black'},yaw=180.22415)
# Source-reflected equipment has no legible make/model; no fabricated branding.
pos='Assets/ChooGuard/Art/StationInterior/R25_Shops/story-lamant/FreeModels/POS-Terminal.fbx'
asset(pos,'Downloaded_POS_Terminal','CoffeeStation',[-16.95,-35.42],.95,[.30,.34,.29],{'*':'black'},yaw=90.22415)
box('Observed_Gray_Cold_Cabinet','CoffeeStation',[-18.81,-35.47],.99,[.67,1.91,.69],'gray')
box('Cold_Cabinet_Door_Gasket','CoffeeStation',[-18.81,-35.831],.88,[.614,1.60,.025],'black')
box('Cold_Cabinet_Door','CoffeeStation',[-18.81,-35.852],.88,[.585,1.57,.024],'gray')
box('Cold_Cabinet_Handle','CoffeeStation',[-18.58,-35.878],.93,[.023,.40,.026],'black',collider=False)
for i in range(12):box(f'Cold_Cabinet_Condenser_Vent_{i}','CoffeeStation',[-19.09+i*.05,-35.828],1.87,[.026,.055,.015],'black',collider=False)
# Pastry fitout: narrow actual empty native return strip, behind existing glazing; no new shop shell.
cx=-14.49;cy=-34.01;cw=1.19;cd=.62
box('Pastry_Case_White_Insulated_Base','PastryCounter',[cx,cy],.22,[cw,.38,cd],'white')
box('Pastry_Case_Black_Base_Shoe','PastryCounter',[cx,cy],.045,[cw+.015,.06,cd+.01],'black')
for zz,yy in enumerate([.39,.72,1.05]):
 box(f'Pastry_Case_Shelf_{zz}','PastryCounter',[cx,cy],yy,[cw-.045,.018,cd-.045],'glass',collider=False)
 for side,xx in enumerate([cx-cw/2,cx+cw/2]):box(f'Pastry_Case_Shelf_Trim_{zz}_{side}','PastryCounter',[xx,cy],yy,[.022,.026,cd],'black',collider=False)
for i,xx in enumerate([cx-cw/2,cx+cw/2]):
 for j,zz in enumerate([cy-cd/2,cy+cd/2]):box(f'Pastry_Case_Frame_{i}_{j}','PastryCounter',[xx,zz],.78,[.024,.90,.024],'black')
box('Pastry_Case_Top_Glass','PastryCounter',[cx,cy],1.238,[cw,.012,cd],'glass',collider=False)
box('Pastry_Case_Top_Front_Rail','PastryCounter',[cx,cy-cd/2],1.24,[cw+.03,.026,.027],'black',collider=False)
box('Pastry_Case_Front_Glass','PastryCounter',[cx,cy-cd/2],.80,[cw-.025,.855,.009],'glass',collider=False)
for i,xx in enumerate([cx-cw/2,cx+cw/2]):box(f'Pastry_Case_Side_Glass_{i}','PastryCounter',[xx,cy],.80,[.009,.855,cd-.025],'glass',collider=False)
# Shared closed solids copied from visible pastry shapes; no invented printed SKU labels.
def lathe_bread(walnut=False):
 v=[];tr=[];n=20
 profiles=[(0.0,.0),(.012,.025),(.045,.034),(.072,.030),(.085,.016),(.087,.0)] if walnut else [(0,.0),(.008,.029),(.025,.036),(.040,.032),(.047,.015),(.046,.0)]
 for k,(y,r) in enumerate(profiles):
  if walnut:y*=.5
  for i in range(n):
   a=2*math.pi*i/n; rr=r*(1+.10*math.cos(a*6)) if walnut else r;v.append([math.cos(a)*rr,y,math.sin(a)*rr*(.70 if walnut else 1)])
 for k in range(len(profiles)-1):
  for i in range(n):
   a=k*n+i;b=k*n+(i+1)%n;c=b+n;d=a+n;tr += [a,b,c,a,c,d]
 return {'vertices':v,'triangles':tr}
library['Source_Round_Gyeongju_Bread']=lathe_bread();library['Source_FlowerPressed_Walnut_Pastry']=lathe_bread(True)
for row,yy in enumerate([.409,.739,1.069]):
 for col,xx in enumerate([cx-.285,cx+.285]):
  ti=f'{row}_{col}';box(f'Pastry_Tray_{ti}_Bottom','PastryMerchandise',[xx,cy],yy,[.52,.012,.45],'box',collider=False)
  for side,x in enumerate([xx-.26,xx+.26]):box(f'Pastry_Tray_{ti}_Side_{side}','PastryMerchandise',[x,cy],yy+.025,[.012,.04,.45],'box',collider=False)
  for side,z in enumerate([cy-.225,cy+.225]):box(f'Pastry_Tray_{ti}_End_{side}','PastryMerchandise',[xx,z],yy+.025,[.52,.04,.012],'box',collider=False)
  for a in range(5):
   for b in range(4):
    key='Source_FlowerPressed_Walnut_Pastry' if row==2 else 'Source_Round_Gyeongju_Bread'
    part('library_mesh',f'Source_Pastry_{ti}_{a}_{b}','PastryMerchandise',[xx+(a-2)*.086,cy+(b-1.5)*.095],yy+.012,'bread' if row!=1 else 'barley',mesh_key=key,collider=False)
# Adjacent white hinged counter and actual potted greenery, within the same bounded strip.
box('White_Return_Counter_Body','ReturnCounter',[-13.23,-34.01],.46,[1.20,.88,.62],'white')
box('White_Return_Countertop','ReturnCounter',[-13.23,-34.01],.925,[1.22,.045,.64],'white')
for i,xx in enumerate([-13.53,-12.93]):
 box(f'Return_Cabinet_Door_{i}','ReturnCounter',[xx,-34.334],.46,[.565,.80,.022],'white')
 box(f'Return_Cabinet_Handle_{i}','ReturnCounter',[xx+(.20 if i==0 else -.20),-34.352],.65,[.018,.16,.025],'black',collider=False)
asset('Assets/ChooGuard/Art/StationInterior/R25_Shops/flowers/Models/potted_plant_01.fbx','Downloaded_Source_Leafy_Counter_Plant','ReturnCounter',[-12.95,-34.01],.95,[.43,.72,.43],{'*':'plant01_leaves','potted_plant_01_potted_plant_01_pot':'plant01_pot','potted_plant_01_potted_plant_01_leaves':'plant01_leaves'},collider=False)
asset('Assets/ChooGuard/Art/StationInterior/R25_Shops/flowers/Models/potted_plant_04.fbx','Downloaded_Source_Small_Counter_Plant','ReturnCounter',[-13.43,-34.03],.95,[.28,.43,.28],{'*':'potted_plant_04'},collider=False)
# Actual2024 main fascia text -> physical closed source contours, no images or font-generated lettering.
im=cv2.imread(str(S/'r29-wide-route/DAILY-FIELD-16.jpg'));crop=im[1190:1460,2150:3400];hsv=cv2.cvtColor(crop,cv2.COLOR_BGR2HSV)
white=cv2.inRange(hsv,np.array([0,0,177]),np.array([179,85,255]));roi=np.zeros(white.shape,np.uint8);roi[70:210,70:1040]=255;white=cv2.bitwise_and(white,roi)
red=cv2.bitwise_or(cv2.inRange(hsv,np.array([0,120,100]),np.array([12,255,255])),cv2.inRange(hsv,np.array([170,120,100]),np.array([179,255,255])));rm=np.zeros(red.shape,np.uint8);rm[80:210,70:185]=255;red=cv2.bitwise_and(red,rm)
for name,mask,m in [('Actual2024_Main_White_Contour_Ink',white,'ink_white'),('Actual2024_Dain_Red_Logo_Contour',red,'red')]:
 cv2.imwrite(str(W/(name+'-mask.png')),mask);shape=solid.closed_solid_from_mask(mask,4.1,.8856,.018,minimum_area=8,epsilon=.48);library[name]={k:shape[k] for k in ['vertices','triangles']};part('library_mesh',name,'PhysicalSourceSigns',[-15.467,-37.36],3.005,m,mesh_key=name,yaw=90.22415,collider=False)
# The coffee board belongs only to the retained fixed-glass panel south of the public door interval.
board=im[1481:1610,2050:2370];bh=cv2.cvtColor(board,cv2.COLOR_BGR2HSV);mask=cv2.inRange(bh,np.array([0,30,25]),np.array([30,255,180]));roi=np.zeros(mask.shape,np.uint8);roi[8:118,30:295]=255;mask=cv2.bitwise_and(mask,roi)
cv2.imwrite(str(W/'Actual_Coffee_Script-mask.png'),mask);shape=solid.closed_solid_from_mask(mask,1.06,.427,.008,minimum_area=8,epsilon=.45);library['Actual_Coffee_Source_Script']={k:shape[k] for k in ['vertices','triangles']}
box('Actual_Hanging_Coffee_Board','PhysicalSourceSigns',[-15.702,-39.48],2.28,[.024,.427,1.06],'white')
part('library_mesh','Actual_Coffee_Source_Script','PhysicalSourceSigns',[-15.685,-39.48],2.28,'brownink',mesh_key='Actual_Coffee_Source_Script',yaw=90.22415,collider=False)
# Current-blue menu source is used only as a tracing reference. Actual visible glyphs become mesh.
menu=im[1610:1719,2080:2590];mh=cv2.cvtColor(menu,cv2.COLOR_BGR2HSV);mask=cv2.inRange(mh,np.array([0,0,150]),np.array([179,100,255]));mask[:8]=0;mask[-8:]=0;mask[:,:8]=0;mask[:,-8:]=0
cv2.imwrite(str(W/'Actual_Menu_Source_White_Ink-mask.png'),mask);shape=solid.closed_solid_from_mask(mask,1.75,.374,.003,minimum_area=3,epsilon=.35);library['Actual_Menu_Source_Ink']={k:shape[k] for k in ['vertices','triangles']}
box('Observed_Blue_Menu_Board','PhysicalSourceSigns',[-19.65,-37.70],1.94,[.025,.374,1.75],'blueboard')
part('library_mesh','Actual_Menu_Source_Ink','PhysicalSourceSigns',[-19.629,-37.70],1.94,'ink_white',mesh_key='Actual_Menu_Source_Ink',yaw=90.22415,collider=False)
packet={'store_key':'southwest-dain','origin_q':[-17.0,-36.4],'floor_world_y':7.0,'materials':mats,'source_imports':[],'mesh_library':library,'objects':objects,'source_packet':str(W/'MODEL-PLACEMENT-BASIS-R29.json'),'no_text_components':True,'no_image_or_font_atlas_letters':True,'no_new_shell_or_ceiling':True,'reuse_native_glazing_doors':True,'stock_quantities_and_dimensions':'PHOTO_MODEL_NOT_FIELD_MEASUREMENT','current_brand_epoch_qualification':'ObservedDain2022/2024 +guidephotographed2026; October2026tenantnotindependentlysurveyed','preserve_actual_Flowers_objects':True}
out=W/'FROZEN-SOURCE-OBSERVED-DAIN-PACKET-R29.json';out.write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
# Physical free-asset provenance stays attached to the exact unchanged installed source files.
assets=sorted(set(o['asset'] for o in objects if o['kind']=='asset'))
(W/'FREE-ASSET-REUSE-ATTRIBUTION-R29.json').write_text(json.dumps({'sources':[{'asset':a,'sha256':hashlib.sha256((ROOT/a).read_bytes()).hexdigest(),'original_unchanged':True,'licensing_registry':str(P/'CURRENT-PHYSICAL-FREE-MODEL-SOURCES-R25.json')}for a in assets],'fit_model_not_exact_brand_product':True,'all_existing_source_assets_preserved':True},indent=2))
print(json.dumps({'parts':len(objects),'shared_meshes':len(library),'free_model_types':len(assets),'packet_sha256':hashlib.sha256(out.read_bytes()).hexdigest()}))
