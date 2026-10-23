"""Own R25 physical Palgongtea construction; source/cad dimensions remain MODEL."""
from pathlib import Path
import json, math, hashlib, shutil, sys, importlib.util
import numpy as np
import cv2

B=Path(__file__).resolve().parent
SRC=B/'sources'
OUT='Assets/ChooGuard/Art/StationInterior/R25_Shops/palgong'
sys.path.insert(0,str(B.parent))
from photo_outline_to_solid import closed_solid_from_mask
basis=json.loads((B/'MODEL-PLACEMENT-BASIS-R25.json').read_text())
origin=np.array(basis['inner_bay_origin_q']);T=np.array(basis['computed_tangent_q']);N=np.array(basis['computed_main_outward_normal_q'])
W=basis['inner_bay_width_MODEL_m'];D=basis['inner_bay_depth_MODEL_m'];YAW=-math.degrees(math.atan2(T[1],T[0]))
objects=[];library={};materials={};imports=[];slotmaps={}
def mat(key,color,**kw):materials[key]={'color':color,**kw}
mat('black',[.018,.022,.023,1],metallic=.1,smoothness=.48)
mat('navy',[.011,.018,.045,1],smoothness=.4)
mat('steel',[.62,.65,.64,1],metallic=.93,smoothness=.7)
mat('oak',[.62,.44,.24,1],smoothness=.3)
mat('oak_light',[.78,.65,.43,1],smoothness=.3)
mat('cane',[.72,.60,.37,1],smoothness=.25)
mat('green_metal',[.12,.19,.12,1],metallic=.65,smoothness=.4)
mat('cream',[.83,.82,.74,1],smoothness=.45)
mat('plaster',[.66,.68,.61,1],smoothness=.2)
mat('ceiling',[.77,.79,.76,1],smoothness=.2)
mat('white',[.9,.91,.84,1],smoothness=.4)
mat('ink',[.055,.041,.04,1],smoothness=.2)
mat('red_ink',[.63,.08,.035,1],smoothness=.25)
mat('orange',[1,.24,.018,1],smoothness=.35)
mat('coffee',[.055,.025,.008,1],smoothness=.6)
mat('glass',[.76,.87,.86,.19],transparent=True,smoothness=.9)
mat('dark_glass',[.07,.085,.08,.28],transparent=True,smoothness=.9)
mat('leaf',[.10,.23,.055,1],smoothness=.3)
mat('stem',[.27,.32,.08,1],smoothness=.3)
mat('soil',[.055,.034,.017,1],smoothness=.1)
mat('terracotta',[.38,.29,.18,1],smoothness=.2)
mat('wicker',[.61,.43,.23,1],smoothness=.3)
mat('emissive_white',[.92,.97,.89,1],emission=[1.9,2,1.8,1],smoothness=.4)
mat('emissive_amber',[1,.38,.04,1],emission=[2,.62,.055,1],smoothness=.4)

def q(x,z):return [round(float(v),7) for v in origin+T*x+N*z]
def add(kind,id,x,z,y,material=None,group='Interior',yaw=0,**kw):
 o={'kind':kind,'id':id,'q':q(x,z),'y':y,'yaw':YAW+yaw,'group':group,'source_ids':['BUSAN2026-00','OFFICIAL2025-00_02'],'qualification':'PHOTO_REFERENCED_MODEL_NOT_FIELD_MEASUREMENT',**kw}
 if material:o['material']=material
 objects.append(o);return o
def box(id,x,z,y,size,material,group='Interior',**kw):return add('box',id,x,z,y,material,group,size=size,**kw)
def cyl(id,x,z,y,r,height,material,group='Interior',**kw):return add('cylinder',id,x,z,y,material,group,size=[r*2,height/2,r*2],**kw)
def mesh(id,key,x,z,y,material,group='Interior',**kw):return add('library_mesh',id,x,z,y,material,group,mesh_key=key,**kw)
def stage(path,dest):
 path=Path(path).resolve();imports.append({'source':str(path),'destination':dest,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()});return OUT+'/'+dest
def downloaded(id,source,x,z,y,fit,materialmap,group='Equipment',yaw=0):
 asset=stage(source,'Models/'+Path(source).name) if not any(i['source']==str(Path(source).resolve()) for i in imports) else OUT+'/'+next(i['destination'] for i in imports if i['source']==str(Path(source).resolve()))
 slotmaps[Path(source).name]=materialmap
 return add('asset',id,x,z,y,group=group,yaw=yaw,asset=asset,fit_size=fit,material_map=materialmap)
def merge(parts):
 vv=[];tt=[]
 for p in parts:
  off=len(vv);vv.extend(p['vertices']);tt.extend([off+i for i in p['triangles']])
 return {'vertices':vv,'triangles':tt}
def lathe(profile,segments=48):
 vv=[];tt=[]
 for r,y in profile:
  for j in range(segments):a=j*2*math.pi/segments;vv.append([r*math.cos(a),y,r*math.sin(a)])
 for k in range(len(profile)-1):
  for j in range(segments):a=k*segments+j;b=k*segments+(j+1)%segments;c=b+segments;d=a+segments;tt.extend([a,c,b,a,d,c])
 return {'vertices':vv,'triangles':tt}
def tube(points,r=.005,sides=7):
 vv=[];tt=[]
 for i,p in enumerate(points):
  p=np.array(p);f=np.array(points[min(i+1,len(points)-1)])-np.array(points[max(0,i-1)]);f/=max(np.linalg.norm(f),1e-9);u=np.cross(f,[0,1,0]);
  if np.linalg.norm(u)<1e-6:u=np.cross(f,[1,0,0])
  u/=np.linalg.norm(u);v=np.cross(f,u)
  for j in range(sides):a=j*2*math.pi/sides;vv.append((p+r*(math.cos(a)*u+math.sin(a)*v)).tolist())
 for i in range(len(points)-1):
  for j in range(sides):a=i*sides+j;b=i*sides+(j+1)%sides;c=b+sides;d=a+sides;tt.extend([a,b,c,a,c,d])
 vv.extend([list(points[0]),list(points[-1])]);a0=len(vv)-2;a1=len(vv)-1
 for j in range(sides):tt.extend([a0,(j+1)%sides,j,a1,(len(points)-1)*sides+j,(len(points)-1)*sides+(j+1)%sides])
 return {'vertices':vv,'triangles':tt}

# Own licensed model imports and explicit per-source URP slots.
arm=SRC/'modern-armchair'
for slot,prefix in [('legs','00'),('pillow','01')]:
 base=stage(arm/f'{prefix}_modern_arm_chair_01_{slot}_BaseColor_RGBA.png',f'Textures/arm_{slot}_base.png')
 normal=stage(arm/f'modern_arm_chair_01_{slot}_nor_gl_1k.jpg',f'Textures/arm_{slot}_normal.jpg')
 mask=stage(arm/f'{prefix}_modern_arm_chair_01_{slot}_MetallicSmoothness.png',f'Textures/arm_{slot}_metallic_smoothness.png')
 mat('arm_'+slot,[.39,.35,.28,1] if slot=='legs' else [.08,.08,.075,1],texture_asset=base,normal_asset=normal,metallic_gloss_asset=mask,smoothness=.6)
coffee=SRC/'coffee-commercial'
cb=stage(coffee/'textures/Image_0.png','Textures/coffee_base.png');cn=stage(coffee/'textures/Image_3.png','Textures/coffee_normal.png')
mat('coffee_pbr',[1,1,1,1],texture_asset=cb,normal_asset=cn,metallic=.7,smoothness=.62)
# Preserve only exact own copies of source/license metadata inside own Assets.
for sub in ['modern-armchair','kiosk','dining-chair','cup','coffee-commercial','pos','cafe-table']:
 p=SRC/sub
 for name in ['SOURCE.json','AUTHOR-METADATA.json','ACQUISITION.json','DERIVATION.json']:
  if (p/name).exists():stage(p/name,'Credits/'+sub+'-'+name)

# Source-visible drop ceiling and suspended white rectangular fixture lattice. NO Light components.
for ix in range(6):
 for iz in range(6):
  box(f'Ceiling_tile_{ix}_{iz}',.14+(W-.28)*(ix+.5)/6,.12+(D-.24)*(iz+.5)/6,3.72,[(W-.28)/6-.012,.022,(D-.24)/6-.012],'ceiling','Ceiling',collider=False)
for z in [.10,D-.10]:box('Existing_bay_aligned_header_'+str(z),W/2,z,3.47,[W-.12,.22,.10],'steel','Frontage',collider=False)
for x in [.10,W-.10]:box('Existing_bay_aligned_sideheader_'+str(x),x,D/2,3.47,[.10,.22,D-.12],'steel','Frontage',collider=False)
for z in [.45,1.82,3.19,4.56,5.93]:
 box('Fixture_rectangular_long_'+str(z),W/2,z,3.32,[W-.56,.034,.033],'white','PhysicalFixtures',collider=False)
 box('Fixture_diffuser_long_'+str(z),W/2,z,3.296,[W-.58,.009,.028],'emissive_white','PhysicalFixtures',collider=False)
for x in [.30,2.13,3.96,W-.30]:
 box('Fixture_rectangular_cross_'+str(x),x,D/2,3.32,[.033,.034,D-.86],'white','PhysicalFixtures',collider=False)
 box('Fixture_diffuser_cross_'+str(x),x,D/2,3.296,[.028,.009,D-.88],'emissive_white','PhysicalFixtures',collider=False)
for x in [.45,W-.45]:
 for z in [.5,3.2,5.9]:cyl(f'Fixture_suspension_{x}_{z}',x,z,3.51,.006,.36,'steel','PhysicalFixtures',collider=False)

# Rear prep seen in source: counter front, short right return, rear worktops and glazing; no hidden rooms.
counterL=.38;counterR=4.58;counterZ=1.52
box('Counter_oak_cabinet_body',(counterL+counterR)/2,counterZ,.46,[counterR-counterL,.91,.69],'oak','Counter')
box('Counter_stone_slab',(counterL+counterR)/2,counterZ, .947,[counterR-counterL+.10,.055,.84],'cream','Counter')
for j in range(68):
 x=counterL+.025+(counterR-counterL-.05)*j/67
 box('Counter_vertical_batten_'+str(j),x,counterZ+.368,.47,[.028,.87,.035],'oak_light','Counter',collider=False)
box('Counter_amber_diffuser',(counterL+counterR)/2,counterZ+.378,.91,[counterR-counterL-.02,.018,.018],'emissive_amber','PhysicalFixtures',collider=False)
box('Counter_short_return_body',counterR-.16,.95,.46,[.34,.91,1.05],'oak','Counter')
box('Counter_return_stone',counterR-.16,.95,.947,[.44,.055,1.10],'cream','Counter')
for j in range(18):box('Counter_return_batten_'+str(j),counterR+.028,.50+j*.052,.47,[.025,.87,.028],'oak_light','Counter',collider=False)
box('Rear_stainless_worktop',2.42,.41,.91,[4.1,.055,.58],'steel','Prep')
box('Rear_open_shelf',2.42,.43,.55,[4.08,.04,.53],'steel','Prep')
for x in [.42,1.75,3.05,4.42]:box('Rear_open_shelf_leg_'+str(x),x,.42,.46,[.035,.88,.42],'steel','Prep')
# Black framed background seen through source opening; no opaque fabricated hidden BOH.
for x in [.28,1.60,2.92,4.65]:box('Rear_window_mullion_'+str(x),x,.10,2.10,[.04,2.73,.04],'black','ObservedRearFrame')
for y in [1.12,2.50,3.43]:box('Rear_window_transom_'+str(y),2.46,.10,y,[4.43,.04,.04],'black','ObservedRearFrame')
box('Rear_window_source_glazing',2.46,.10,2.28,[4.40,2.28,.012],'dark_glass','ObservedRearFrame',collider=False)
# Actual plaster sign pier at back/side, source visibility only; keep both public faces open.
box('Source_plaster_sign_pier',5.30,.30,1.75,[1.52,3.43,.12],'plaster','SourceVisibleFinishes')
box('Source_vertical_wood_panel',.13,1.15,1.74,[.11,3.43,1.86],'oak_light','SourceVisibleFinishes')

# Actual downloaded coffee machine and POS. All source materials explicitly mapped to own URP.
downloaded('Downloaded_commercial_espresso',coffee/'CommercialCoffeeMachine.fbx',1.55,.48,.96,[1.02,.57,.51],{'Coffee_machine_base':'coffee_pbr'},yaw=0)
downloaded('Downloaded_cashier_payment_terminal',SRC/'pos/POS-Terminal.fbx',1.04,1.51,.98,[.23,.38,.20],{'Material_2100454184':'black','Material_2100454185':'black','*':'black'},yaw=0)

# Source four stainless tea urns with actual cylindrical bodies, lids, spigots and handles.
library['TeaUrn']=lathe([(0,0),(.13,0),(.145,.025),(.145,.46),(.136,.49),(0,.49)],48)
library['TeaLid']=lathe([(0,0),(.148,0),(.148,.018),(.10,.045),(0,.045)],48)
for j in range(4):
 x=2.75+j*.40;mesh('Tea_urn_stainless_'+str(j),'TeaUrn',x,.40,.95,'steel','TeaPreparation')
 mesh('Tea_urn_lid_'+str(j),'TeaLid',x,.40,1.44,'steel','TeaPreparation')
 box('Tea_urn_front_level_'+str(j),x,.549,1.23,[.027,.28,.018],'black','TeaPreparation',collider=False)
 box('Tea_urn_tap_housing_'+str(j),x,.579,1.06,[.06,.05,.06],'black','TeaPreparation',collider=False)
 cyl('Tea_urn_lid_knob_'+str(j),x,.40,1.50,.023,.025,'black','TeaPreparation',collider=False)
 for s in [-1,1]:box(f'Tea_urn_side_handle_{j}_{s}',x+s*.16,.40,1.33,[.03,.11,.04],'black','TeaPreparation',collider=False)

# Downloaded kiosk source hull, fitted to paired photo kiosks at east public edge, outward +qx.
for j,z in enumerate([4.80,5.63]):
 downloaded('Downloaded_order_kiosk_'+str(j),SRC/'kiosk/kiosk.fbx',5.63,z,.012,[.43,1.72,.35],{'PICO_PAY':'black','*':'black'},'Kiosks',yaw=90)
 # Actual card reader, printer slot and scanner form source-specific separate editable hardware.
 box('Kiosk_payment_hardware_'+str(j),5.84,z,.86,[.055,.27,.32],'black','Kiosks')
 box('Kiosk_cardreader_face_'+str(j),5.872,z-.09,.86,[.018,.10,.10],'steel','Kiosks',collider=False)
 box('Kiosk_receipt_slot_'+str(j),5.878,z+.08,1.00,[.012,.018,.16],'ink','Kiosks',collider=False)
 box('Kiosk_scanner_'+str(j),5.88,z+.075,.83,[.012,.07,.10],'dark_glass','Kiosks',collider=False)
 box('Kiosk_source_screen_bezel_'+str(j),5.865,z,1.34,[.028,.64,.34],'black','Kiosks',collider=False)
 box('Kiosk_source_screen_surface_'+str(j),5.882,z,1.34,[.008,.58,.29],'cream','Kiosks',collider=False)

# Source-based menu/sign source contours: all visible ink is real thin closed mesh, no fonts/TMP/photo planes.
def source_ink(key,img,quad,shape,physical,rule='white',minarea=6,epsilon=.33):
 im=cv2.imread(str(img));dst=np.array([[0,0],[shape[0]-1,0],[shape[0]-1,shape[1]-1],[0,shape[1]-1]],np.float32)
 H=cv2.getPerspectiveTransform(np.array(quad,np.float32),dst);rect=cv2.warpPerspective(im,H,shape);hsv=cv2.cvtColor(rect,cv2.COLOR_BGR2HSV)
 if rule=='white':mask=(hsv[:,:,1]<90)&(hsv[:,:,2]>185)
 elif rule=='red':mask=((hsv[:,:,0]<14)|(hsv[:,:,0]>170))&(hsv[:,:,1]>100)&(hsv[:,:,2]>90)
 elif rule=='dark':mask=cv2.cvtColor(rect,cv2.COLOR_BGR2GRAY)<125
 elif rule=='menu_red':mask=((hsv[:,:,0]<20)|(hsv[:,:,0]>165))&(hsv[:,:,1]>80)&(hsv[:,:,2]>90)
 mask=mask.astype('uint8')*255;mask[:3]=0;mask[-3:]=0;mask[:,:3]=0;mask[:,-3:]=0
 cv2.imwrite(str(SRC/(key+'-rectified.png')),rect);cv2.imwrite(str(SRC/(key+'-ink.png')),mask)
 solid=closed_solid_from_mask(mask,*physical,minimum_area=minarea,epsilon=epsilon);library[key]={'vertices':solid['vertices'],'triangles':solid['triangles']}
 return {'mesh_key':key,'source_file':str(img),'quad_px':quad,'source_polygons':solid['source_polygons'],'closed_front_back_side':True,'source_raster_rendered':False,'depth_MODEL_m':physical[2]}
signrecords=[]
signrecords.append(source_ink('BrandPrimary',SRC/'ROOT-palgongtea_official-223745972521-00.jpg',[[404,34],[930,36],[929,134],[401,132]],(800,150),(2.46,.46,.035),'white',20,.4))
box('Brand_hanging_panel',3.10,5.98,2.88,[2.67,.57,.07],'navy','PhysicalSigns',collider=False)
mesh('Brand_actual_letter_closed_solids','BrandPrimary',3.10,6.023,2.88,'emissive_white','PhysicalSigns',collider=False)
for x in [2.18,4.02]:cyl('Brand_hanger_'+str(x),x,5.98,3.24,.006,.44,'steel','PhysicalSigns',collider=False)
signrecords.append(source_ink('BrandWall',SRC/'ROOT-palgongtea_official-223745972521-00.jpg',[[31,238],[316,238],[316,359],[31,359]],(720,300),(1.26,.53,.018),'red',14,.45))
mesh('Brand_actual_wall_letter_solids','BrandWall',5.30,.374,2.22,'red_ink','PhysicalSigns',collider=False)
menu_source=SRC/'ROOT-kjeongy0-224220758980-02.jpg'
quad=[[30,62],[942,49],[946,537],[22,534]]
signrecords.append(source_ink('MenuDarkInk',menu_source,quad,(1200,630),(.94,.494,.0015),'dark',5,.28))
signrecords.append(source_ink('MenuRedInk',menu_source,quad,(1200,630),(.94,.494,.0018),'menu_red',5,.28))
box('Actual_full_menu_backing',5.61,4.79,2.03,[.028,.51,.97],'cream','PhysicalMenus',collider=False)
mesh('Actual_menu_dark_contours','MenuDarkInk',5.63,4.79,2.03,'ink','PhysicalMenus',yaw=90,collider=False)
mesh('Actual_menu_red_contours','MenuRedInk',5.632,4.79,2.03,'red_ink','PhysicalMenus',yaw=90,collider=False)
for j,z in enumerate([4.80,5.63]):
 # Source menu information repeated on actual model's display; reduced but physical source-derived contour.
 mesh('Kiosk_menu_content_'+str(j),'MenuDarkInk',5.89,z,1.34,'ink','Kiosks',yaw=90,size=[.29/.94,.54/.494,1],collider=False)
for j,x in enumerate([1.35,2.53,3.71]):
 box('Rear_digital_menu_frame_'+str(j),x,.62,2.53,[1.11,.61,.06],'black','PhysicalMenus',collider=False)
 box('Rear_digital_menu_screen_'+str(j),x,.658,2.53,[1.06,.56,.012],'orange' if j==0 else 'cream','PhysicalMenus',collider=False)
 mesh('Rear_digital_menu_ink_'+str(j),'MenuDarkInk',x,.671,2.53,'white' if j==0 else 'ink','PhysicalMenus',size=[1.04/.94,.54/.494,1],collider=False)
 for xx in [x-.33,x+.33]:cyl('Rear_menu_suspension_'+str(j)+'_'+str(xx),xx,.62,3.05,.006,.47,'steel','PhysicalFixtures',collider=False)

# Downloaded furniture: black upholstered wooden lounge armchairs, green tubular dining/cane seats.
for j,(x,z,yaw) in enumerate([(1.10,5.70,180),(2.15,5.70,180),(1.10,4.13,0),(2.15,4.13,0),(3.17,5.70,180),(4.17,5.70,180)]):
 downloaded('Downloaded_black_wood_lounge_'+str(j),arm/'ModernArmchair.fbx',x,z,.01,[.70,.78,.73],{'modern_arm_chair_01_legs':'arm_legs','modern_arm_chair_01_pillow':'arm_pillow','*':'arm_pillow'},'LoungeSeating',yaw=yaw)
diningmap={'Scene_-_Root':'green_metal','Object_29':'cane','Object_32':'cane','Object_5':'cane','Object_8':'cane','*':'green_metal'}
for j,(x,z,yaw) in enumerate([(1.24,2.57,180),(1.24,3.65,0),(2.65,2.57,180),(2.65,3.65,0),(4.02,2.57,180),(4.02,3.65,0)]):
 downloaded('Downloaded_cane_green_dining_'+str(j),SRC/'dining-chair/dining-chair.fbx',x,z,.01,[.55,.79,.56],diningmap,'DiningSeating',yaw=yaw)
for j,(x,z) in enumerate([(1.24,3.10),(2.65,3.10),(4.02,3.10)]):
 downloaded('Downloaded_cafe_table_base_'+str(j),SRC/'cafe-table/CafeTableSteelBase.fbx',x,z,.01,[.48,.69,.48],{'Silver_B':'green_metal','*':'green_metal'},'DiningTables')
 cyl('Cafe_table_round_top_'+str(j),x,z,.735,.44,.045,'oak_light','DiningTables')
for j,(x,z) in enumerate([(1.62,4.90),(3.68,4.90)]):
 downloaded('Downloaded_lounge_table_base_'+str(j),SRC/'cafe-table/CafeTableSteelBase.fbx',x,z,.01,[.52,.48,.46],{'Silver_B':'black','*':'black'},'LoungeTables')
 box('Lounge_table_rounded_top_'+str(j),x,z,.53,[.85,.035,.58],'oak','LoungeTables',collider=False)

# Source-visible woven pendant shades, physically open woven ribs and circles.
shadeparts=[]
for j in range(32):
 a=j*math.pi*2/32;pts=[]
 for k in range(13):
  h=k/12;r=.12+.15*math.sin(h*math.pi*.82);pts.append([r*math.cos(a),h*.35,r*math.sin(a)])
 shadeparts.append(tube(pts,.004,5))
for k in range(11):
 h=k/10;r=.12+.15*math.sin(h*math.pi*.82);pts=[[r*math.cos(j*math.pi*2/64),h*.35,r*math.sin(j*math.pi*2/64)] for j in range(65)];shadeparts.append(tube(pts,.0035,5))
library['WovenShade']=merge(shadeparts)
for j,(x,z,y) in enumerate([(2.15,2.32,2.46),(2.87,2.32,2.64),(3.55,2.32,2.36),(4.12,2.32,2.56)]):
 mesh('Source_woven_pendant_'+str(j),'WovenShade',x,z,y,'wicker','PhysicalFixtures',collider=False)
 cyl('Pendant_cord_'+str(j),x,z,(3.3+y+.35)/2,.004,3.3-y-.35,'black','PhysicalFixtures',collider=False)
 cyl('Pendant_inner_diffuser_'+str(j),x,z,y+.055,.10,.018,'emissive_amber','PhysicalFixtures',collider=False)

# Source-shaped areca palm fronds: genuine closed leaf/stem solids, no plant photo planes.
leafparts=[];stemparts=[]
for fi in range(12):
 a=fi*2.39996323;radial=np.array([math.cos(a),0,math.sin(a)]);side=np.array([-math.sin(a),0,math.cos(a)]);height=.9+(fi%4)*.19;reach=.68+(fi%3)*.11
 def center(t):return radial*(reach*t)+np.array([0,height+1.02*t-.70*t*t,0])
 stemparts.append(tube([[0,.04,0],*(center(t).tolist() for t in np.linspace(0,1,18))],.006,6))
 for j in range(2,21):
  t=j/22;p=center(t);length=.10+.23*math.sin(t*math.pi)**.7
  for s in [-1,1]:
   tip=p+side*s*length+radial*.11+np.array([0,-.095*t,0]);mid=(p+tip)/2+np.array([0,.025,0]);wide=radial*.014*(1-.4*t)
   pts=[p,mid+wide,tip,mid-wide];vs=[v.tolist() for v in pts]+[(v+np.array([0,-.0015,0])).tolist() for v in pts];ts=[0,1,2,0,2,3,6,5,4,7,6,4]
   for k in range(4):n=(k+1)%4;ts.extend([k,n,n+4,k,n+4,k+4])
   leafparts.append({'vertices':vs,'triangles':ts})
library['ArecaPinnateLeaves']=merge(leafparts);library['ArecaSlenderStems']=merge(stemparts)
library['PalmPot']=lathe([(0,0),(.145,0),(.21,.40),(.22,.43),(.195,.43),(.15,.07),(0,.07)],48)
for j,(x,z,h) in enumerate([(.58,5.85,1.05),(3.00,5.88,.83),(4.91,5.85,.9),(5.55,4.02,1.36),(5.55,2.18,1.20),(5.55,.95,1.25)]):
 mesh('Palm_pot_'+str(j),'PalmPot',x,z,.015,'terracotta' if j<3 else 'cream','Plants',collider=False)
 cyl('Palm_soil_'+str(j),x,z,.42,.185,.018,'soil','Plants',collider=False)
 mesh('Palm_source_pinnate_leaves_'+str(j),'ArecaPinnateLeaves',x,z,.43,'leaf','Plants',size=[h*.46,h,h*.46],yaw=j*51,collider=False)
 mesh('Palm_source_stems_'+str(j),'ArecaSlenderStems',x,z,.43,'stem','Plants',size=[h*.46,h,h*.46],yaw=j*51,collider=False)

# Actual downloaded tapered takeaway-cup body; independent orange sleeve, clear dome and straw.
library['CupSleeve']=lathe([(.034,0),(.043,.08),(.042,.08),(.033,0)],48)
library['DomeLid']=lathe([(.049,0),(.049,.005),(.046,.006),(.045,.018),(.039,.034),(.029,.047),(.012,.055),(.009,.055),(.009,.053),(.027,.045),(.037,.032),(.043,.017),(.044,.004),(.047,.003)],48)
for j,(x,z) in enumerate([(2.80,1.67),(2.95,1.67),(3.10,1.67),(3.25,1.67),(3.40,1.67),(3.55,1.67),(1.62,4.90)]):
 base=.977 if j<6 else .55
 downloaded('Downloaded_takeaway_cup_'+str(j),SRC/'cup/CupBody-SourceDerivative.fbx',x,z,base,[.096,.15,.096],{'DefaultMaterial':'glass','*':'glass'},'Drinks')
 mesh('Orange_actual_cup_sleeve_'+str(j),'CupSleeve',x,z,base+.035,'orange','Drinks',collider=False)
 mesh('Actual_clear_dome_lid_'+str(j),'DomeLid',x,z,base+.151,'glass','Drinks',collider=False)
 cyl('Coffee_visible_content_'+str(j),x,z,base+.09,.038,.10,'coffee','Drinks',collider=False)
 cyl('Black_straw_'+str(j),x,z,base+.20,.0025,.19,'black','Drinks',collider=False)

packet={'store_key':'palgong','origin_q':origin.tolist(),'floor_world_y':7.0,'source_packet':str(B/'SOURCE-EVIDENCE-R25.json'),'materials':materials,'source_imports':imports,'mesh_library':library,'objects':objects,'placement_basis':str(B/'MODEL-PLACEMENT-BASIS-R25.json'),'lights_created':0,'text_components_created':0,'hidden_BOH_modeled':False,'primary_secondary_public_faces_open':True}
(B/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
(B/'SOURCE-SIGN-SOLIDS-R25.json').write_text(json.dumps(signrecords,indent=2))
(B/'EXPLICIT-SOURCE-URP-SLOT-MAPS-R25.json').write_text(json.dumps(slotmaps,indent=2))
print(json.dumps({'objects':len(objects),'imports':len(imports),'mesh_prototypes':len(library),'packet_bytes':(B/'STORE-PACKET-R25.json').stat().st_size,'MODEL_bay':[W,D]}))
