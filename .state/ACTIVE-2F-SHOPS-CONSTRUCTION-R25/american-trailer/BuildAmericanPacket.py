"""American Trailer Busan Station2F: actual native-vector bay, source photos, licensed meshes.
Only staging .state. Scene/assets writer is the frozen own C180 constructor.
"""
from pathlib import Path
import json,math,hashlib,random,sys,shutil
import numpy as np,cv2
from PIL import Image
ROOT=Path('/Users/um-yunsang/CHOOGuard');BASE=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/american-trailer';SHARED=BASE.parent/'free-assets'
ASSET='Assets/ChooGuard/Art/StationInterior/R25_Shops/american-trailer';source=BASE/'sources';generated=source/'construction-source';generated.mkdir(exist_ok=True)
sys.path.insert(0,str(BASE.parent));from photo_outline_to_solid import closed_solid_from_mask
basis=json.loads((BASE/'MODEL-PLACEMENT-BASIS-R25.json').read_text());FS=np.array(basis['computed_front_south_q']);T=np.array(basis['tangent_q']);N=np.array(basis['inward_q']);L=basis['frontage_m_MODEL'];YAW=math.degrees(math.atan2(-N[0],-N[1]));ORIGIN=[-63,37]
objects=[];materials={};imports=[];imported={};library={};credits=[]
PHOTO='HX2026_FRONT';PREP='HX2026_PREP';CLOSE='ENJOY2026_PREP';REF='MYPOPO2025_STORE'
def q(s,r):return (FS+T*s+N*r).tolist()
def mat(k,color,metallic=0,smoothness=.4,**extra):materials[k]=dict(color=list(color) if len(color)==4 else [*color,1],metallic=metallic,smoothness=smoothness,**extra);return k
for k,c,m,s in [('charcoal',[.035,.041,.047],.26,.42),('black',[.012,.015,.018],.1,.43),('steel',[.62,.66,.68],.93,.57),('steel_dark',[.2,.22,.24],.84,.57),('stone',[.72,.71,.65],0,.3),('tile',[.92,.915,.88],0,.68),('grout',[.64,.65,.63],0,.23),('wood',[.64,.40,.17],0,.33),('red_board',[.79,.035,.009],0,.51),('white',[.94,.94,.9],0,.52),('signwhite',[.97,.98,1],.08,.65),('logo_red',[.66,.045,.12],.12,.52),('logo_blue',[.035,.16,.49],.1,.5),('lemon',[.92,.76,.035],0,.44),('orange',[.96,.36,.018],0,.43),('grapefruit',[.94,.44,.11],0,.46),('kale',[.14,.30,.048],0,.33),('tomato',[.61,.025,.013],0,.46),('brown',[.15,.055,.025],0,.44),('plastic',[.91,.89,.81],0,.47),('water',[.56,.71,.78],0,.88)]:mat(k,c,m,s)
mat('glass',[.8,.92,.94,.12],0,.93,transparent=True);mat('cup_clear',[.87,.95,1,.15],0,.93,transparent=True);mat('sign_emission',[.94,.97,1],.04,.61,emission=[1.2,1.3,1.42,1]);mat('warm_light',[1,.94,.78],0,.52,emission=[2.3,2.05,1.5,1]);mat('screen',[.08,.13,.17],.1,.7,emission=[.08,.10,.12,1])
def imp(path,dest=None):
 path=Path(path);path=path if path.is_absolute() else ROOT/path;dest=dest or 'SourceModels/'+path.name
 if dest in imported:return ASSET+'/'+dest
 data=path.read_bytes();imports.append({'source':str(path),'destination':dest,'sha256':hashlib.sha256(data).hexdigest()});imported[dest]=True;return ASSET+'/'+dest

def add(kind,id,s,r,y,group='Interior',src=(PHOTO,),**kw):
 ob=dict(kind=kind,id=id,group=group,q=q(s,r),y=y,source_ids=list(src),qualification='PHOTO_REFERENCED_MODEL_NOT_FIELD_MEASUREMENT',**kw);objects.append(ob);return ob

def box(id,s,r,y,w,h,d,material,group='Interior',**kw):return add('box',id,s,r,y,group,material=material,size=[w,h,d],yaw=YAW,**kw)
def cyl(id,s,r,y,diam,h,material,group='Interior',**kw):return add('cylinder',id,s,r,y,group,material=material,size=[diam,h/2,diam],collider=False,**kw)
def mesh(id,s,r,y,vs,ts,material,group='Interior',yaw=None,**kw):return add('mesh',id,s,r,y,group,vertices=vs,triangles=ts,material=material,yaw=YAW if yaw is None else yaw,collider=False,**kw)
def lib(id,s,r,y,key,material,group='Interior',**kw):return add('library_mesh',id,s,r,y,group,mesh_key=key,material=material,yaw=YAW,collider=False,**kw)
def asset(id,s,r,y,size,path,group='Free_3D_Equipment',mm=None,material=None,src=(PHOTO,),yaw=None,collider=True):
 x=add('asset',id,s,r,y,group,src,asset=path,fit_size=size,yaw=YAW if yaw is None else yaw,collider=collider)
 if mm:x['material_map']=mm
 if material:x['material']=material
 return x

def smoothmap(src,prefix):
 im=np.array(Image.open(src).convert('RGB'));a=np.empty((*im.shape[:2],4),np.uint8);a[:,:,0]=im[:,:,2];a[:,:,1:3]=0;a[:,:,3]=255-im[:,:,1];dest=generated/(prefix+'-metallic_smoothness.png');Image.fromarray(a).save(dest);return imp(dest,'PBR/'+dest.name)

def ownmodel(slug):
 p=source/'free-models'/slug;d=json.loads((p/'UNITY-PBR-MATERIALS.json').read_text());ap=imp(d['export'],f'FreeModels/{slug}.fbx');maps={}
 for m in d['materials']:
  key=m['key'].replace('-','_');c=m['color'];tx=m['textures'];args={}
  if tx.get('BASE COLOR'):
   imagefile=Path(tx['BASE COLOR']);dest=generated/(slug+'-'+key+'-'+imagefile.name)
   im=Image.open(imagefile).convert('RGB')
   if slug=='blender' and m['name']=='blender':
    ar=np.array(im);hsv=cv2.cvtColor(ar,cv2.COLOR_RGB2HSV);mask=((hsv[:,:,0]<15)|(hsv[:,:,0]>165))&(hsv[:,:,1]>80);gray=cv2.cvtColor(ar,cv2.COLOR_RGB2GRAY);ar[mask]=np.stack([gray[mask]*.16,gray[mask]*.18,gray[mask]*.20],axis=-1).astype(np.uint8);im=Image.fromarray(ar)
   im.save(dest);args['texture_asset']=imp(dest,'PBR/'+dest.name);c=[1,1,1,1]
  if tx.get('NORMAL MAP'):args['normal_asset']=imp(tx['NORMAL MAP'],'PBR/'+slug+'-normal.png')
  if tx.get('METALLIC ROUGHNESS'):args['metallic_gloss_asset']=smoothmap(tx['METALLIC ROUGHNESS'],slug+'-'+key)
  if m.get('transmission',0)>.2 or 'glass' in m['name'].lower() or 'plastic_tran' in m['name'].lower():c=[.83,.93,.96,.15];args['transparent']=True
  if slug=='counter':c=[.62,.65,.67,1];m['metallic']=.9;m['roughness']=.4
  mat(key,c,m['metallic'],1-m['roughness'],**args)
 for o in d['objects']:
  maps[o['name']]=next((m.replace('-','_') for m in o['materials'] if m),'steel')
 credits.append(json.loads((p/'ACQUISITION.json').read_text()));return ap,maps

# Actual free source assets: realistic family matching model topology, PBR adaptations.
models={k:ownmodel(k) for k in ['blender','grinder','squeezer-c','counter','lemon-scan','orange-fruit']}
# coffee source has one complete PBR atlas; preserve actual mesh details.
cp=source/'free-models/coffee';coffee=imp(cp/'coffee.fbx','FreeModels/commercial_coffee.fbx');mat('coffee_pbr',[1,1,1],.7,.6,texture_asset=imp(cp/'textures/Image_0.png','PBR/coffee_albedo.png'),normal_asset=imp(cp/'textures/Image_3.png','PBR/coffee_normal.png'),metallic_gloss_asset=smoothmap(cp/'textures/Image_1.png','coffee'));credits.append(json.loads((cp/'ACQUISITION.json').read_text()))
# Commercial refrigerator bySeanThomas/Khronos, source empty model (no unrelated stock).
fridge=imp(SHARED/'commercial-refrigerator/CommercialRefrigerator-Empty.fbx','FreeModels/commercial_fridge.fbx');fr=SHARED/'commercial-refrigerator/textures';mat('fridge_case',[.20,.22,.23],.68,.55,texture_asset=imp(fr/'Image_9.png','PBR/fridge_case_albedo.png'),metallic_gloss_asset=smoothmap(fr/'Image_4.png','fridge_case'));mat('fridge_inside',[.86,.90,.92],.45,.48,texture_asset=imp(fr/'Image_6.png','PBR/fridge_inside_albedo.png'),metallic_gloss_asset=smoothmap(fr/'Image_8.png','fridge_inside'));frmap={'FridgeGlass':'glass','FridgeCase':'fridge_case','FridgeDoor':'fridge_case','FridgeDoorInner':'fridge_inside','FridgeInterior':'fridge_inside'};credits.append({'source':'https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/CommercialRefrigerator','author':'SeanThomas(foon),Khronoscontributors','license':'CC-BY-4.0','source_file':str(SHARED/'commercial-refrigerator/CommercialRefrigerator-Empty.fbx')})
pos=imp(SHARED/'pos-terminal/POS-Terminal.fbx','FreeModels/POS-terminal.fbx');pt=SHARED/'pos-terminal/textures';mat('pos_pbr',[1,1,1],.4,.45,texture_asset=imp(pt/'Image_0.png','PBR/pos_albedo.png'),normal_asset=imp(pt/'Image_2.png','PBR/pos_normal.png'),metallic_gloss_asset=smoothmap(pt/'Image_1.png','pos'));credits.append(json.loads((SHARED/'pos-terminal/ACQUISITION.json').read_text()))
kenney=SHARED/'kenney-food-kit/Models/FBX format';foods={name:imp(kenney/(name+'.fbx'),'FreeModels/'+name+'.fbx') for name in ['orange','lemon','lemon-half','banana','cup','cup-coffee','soda-bottle','carton','cooking-knife','cutting-board','tomato','salad']};credits.append({'source':'https://kenney.nl/assets/food-kit','author':'Kenney','license':'CC0','models':list(foods)})

# Native rear/neighbor walls remain original. Interior removable lining is 6cm in front of native,
# and south divider is inside own side. North divider is exclusively pharmacy author's ownership.
box('Interior_Rear_White_Tiled_Liner',L/2,3.02,1.4,L-.26,2.8,.025,'tile','Booth_Lining')
for i in range(1,10):box('Rear_Grout_Horizontal_'+str(i),L/2,3.003,.28*i,L-.27,.007,.003,'grout','Booth_Lining',collider=False)
for row in range(10):
 for k in range(12):box(f'Rear_Grout_Vertical_{row}_{k}',.24+k*.53+(row%2)*.26,3.001,.14+row*.28,.005,.27,.003,'grout','Booth_Lining',collider=False)
box('Own_South_Return_Inside_Approved_Gijang_Edge',.13,1.59,1.44,.055,2.88,2.92,'tile','Booth_Lining')
# Photo shows open front beneath dark fascia; no door or hall blocker is created.
box('Fascia_Dark_Panel_PhotoShape',L/2,.15,2.98,L-.28,.64,.22,'charcoal','Physical_Front_Sign')
box('Fascia_Top_Frame',L/2,.15,3.312,L-.24,.025,.25,'black','Physical_Front_Sign',collider=False)
box('Fascia_Lower_Frame',L/2,.15,2.647,L-.24,.024,.25,'black','Physical_Front_Sign',collider=False)
for s in [.18,L-.18]:box('Fascia_End_Frame_'+str(s),s,.15,2.98,.027,.65,.25,'black','Physical_Front_Sign',collider=False)
# Light soffit behind fascia and individually editable recessed lights (not a native roof).
box('Shop_Interior_Soffit_Removable',L/2,1.55,2.65,L-.35,.07,2.70,'stone','Booth_Ceiling')
for s in [.68,2.0,3.35,4.7,5.95]:
 cyl('Recessed_Light_Bezel_'+str(s),s,.52,2.609,.16,.012,'steel_dark','Lighting')
 cyl('Recessed_Light_Diffuser_'+str(s),s,.52,2.599,.137,.012,'warm_light','Lighting')
 add('light','Own_Downlight_'+str(s),s,.68,2.49,'Lighting',color=[1,.9,.73,1],range=2.4,intensity=.55)
# Photo-traced real brand letters/logo: closedsolid depth, sidewalls, stepped bevel edge.
sign=cv2.imread(str(source/'SOURCE-SIGN-RECTIFIED.png'));hsv=cv2.cvtColor(sign,cv2.COLOR_BGR2HSV);white=cv2.inRange(hsv,np.array([0,0,145]),np.array([179,95,255]));white[:6]=0;white[-6:]=0;white[:,:10]=0;white[:,-10:]=0
# Remove small compression islands while retaining actual letter holes and logo details.
num,labels,stats,_=cv2.connectedComponentsWithStats(white);white=np.where(np.isin(labels,[i for i in range(1,num) if stats[i,4]>12]),255,0).astype(np.uint8)
for label,mask,material,depth,r in [('Brand_Shadow_Back',cv2.dilate(white,np.ones((3,3),np.uint8)),'steel_dark',.045,.021),('Brand_White_Bevel_Solid',white,'sign_emission',.028,.012)]:
 d=closed_solid_from_mask(mask,4.84,.615,depth=depth,minimum_area=12,epsilon=.36);mesh(label,L/2,r,2.983,d['vertices'],d['triangles'],material,'Physical_Front_Sign',src=(PHOTO,))
# Central actual logo color is physical raised colored strips/flag field, no photograph plane.
for label,lo,hi,material in [('Trailer_Flag_Red',[145,45,80],[179,255,255],'logo_red'),('Trailer_Flag_Blue',[90,65,50],[135,255,255],'logo_blue')]:
 mask=cv2.inRange(hsv,np.array(lo),np.array(hi));mask[:,:675]=0;mask[:,954:]=0;mask=cv2.morphologyEx(mask,cv2.MORPH_CLOSE,np.ones((2,2),np.uint8));d=closed_solid_from_mask(mask,4.84,.615,depth=.012,minimum_area=3,epsilon=.30)
 if d['vertices']:mesh(label,L/2,.006,2.983,d['vertices'],d['triangles'],material,'Physical_Front_Sign',src=(PHOTO,))
# Printed source menus on physical hanging display panels; never equipment substitutes.
front=cv2.imread(str(source/'hxaxnxa-224312851410-00.jpg'));quad=np.float32([[178,236],[777,241],[774,326],[179,321]]);M=cv2.getPerspectiveTransform(quad,np.float32([[0,0],[1600,0],[1600,250],[0,250]]));menu=cv2.warpPerspective(front,M,(1600,250))
def printpanel(id,s,r,y,w,h,im):
 file=generated/(id+'.png');cv2.imwrite(str(file),im);mk=id+'_print';mat(mk,[1,1,1],0,.47,texture_asset=imp(file,'Prints/'+file.name),emission=[.15,.15,.15,1]);box(id+'_BlackPhysicalFrame',s,r+.012,y,w+.035,h+.035,.042,'black','Printed_Source_Menus',collider=False)
 vs=[[w/2,h/2,0],[-w/2,h/2,0],[-w/2,-h/2,0],[w/2,-h/2,0]];mesh(id,s,r-.013,y,vs,[0,1,2,0,2,3],mk,'Printed_Source_Menus',uv=[[0,1],[1,1],[1,0],[0,0]],src=(PHOTO,))
for i,(a,b) in enumerate([(0,400),(400,800),(800,1200),(1200,1600)]):printpanel('Actual_Menu_'+str(i),1.38+i*1.13,.57,2.23,1.10,.52,menu[:,a:b])
# Rear photo wordmark also physical source-image outline, not TMP/font generation.
backcrop=front[343:375,308:639];gray=cv2.cvtColor(backcrop,cv2.COLOR_BGR2GRAY);mask=cv2.inRange(gray,0,100);d=closed_solid_from_mask(mask,2.78,.25,.008,5,.35);mesh('Rear_Source_Coffee_Juice_Letter_Solids',3.54,2.98,1.92,d['vertices'],d['triangles'],'black','Physical_Source_Wall_Letters',src=(PHOTO,))

# Real downloaded cabinets and source production equipment.
counter,cmap=models['counter'];asset('Rear_Source_Stainless_Counter_Cabinet',3.52,2.59,0,[4.46,.88,.65],counter,mm={'*':'steel'},group='Counters_Free_Source')
box('Rear_Stone_Worktop',3.52,2.57,.91,4.58,.055,.70,'stone','Counters_Free_Source')
asset('Front_Source_Counter_Cabinet',3.85,.61,0,[2.36,.88,.68],counter,mm={'*':'steel'},group='Counters_Free_Source')
box('Front_Stone_Prep_Worktop',3.85,.58,.91,2.44,.055,.76,'stone','Counters_Free_Source')
# Realphoto wood pickup block separate from equipment.
box('Wooden_Pickup_Endcap',2.66,.47,.93,.60,.26,.70,'wood','Counter_Photo_Details')
for i in range(7):box('Pickup_Endgrain_'+str(i),2.66,.109,.825+i*.035,.63,.003,.003,'stone','Counter_Photo_Details',collider=False)
asset('Tall_Glass_Door_Refrigerator',1.96,.61,0,[.77,1.89,.69],fridge,mm=frmap,group='Refrigeration_Free_Source')
# Short glazed produce cabinet matches source, derived independent instance of actual refrigerator.
asset('Low_Glass_Produce_Case',.98,.59,0,[1.02,1.25,.66],fridge,mm=frmap,group='Refrigeration_Free_Source')
asset('Kiosk_Source_Counter_Unit',5.99,.63,0,[.56,.88,.64],counter,mm={'*':'steel'},group='Counters_Free_Source')
box('Kiosk_Stone_Worktop',5.99,.61,.91,.58,.055,.68,'stone','Counters_Free_Source')
# The physical actual counter kiosk is distinguished from existing station objects.
asset('Customer_POS_Actual_Free_3D',5.99,.62,.92,[.46,.65,.31],pos,mm={'*':'pos_pbr'},group='POS_Free_Source')
box('Kiosk_Photo_Upright_Housing',5.99,.72,1.21,.42,.65,.095,'black','POS_Free_Source')
# Source photo screen printing fits in a modeled kiosk surface, never replaces its body.
kiosk=cv2.imread(str(source/'hxaxnxa-224312851410-06.jpg'));kh,kw=kiosk.shape[:2];kc=kiosk[int(kh*.24):int(kh*.63),int(kw*.23):int(kw*.71)];printpanel('Kiosk_Actual_Source_Screen',5.99,.35,1.30,.30,.43,kc)
asset('Commercial_Espresso_Machine_Real_Model',4.77,2.45,.94,[.95,.49,.48],coffee,material='coffee_pbr',group='Coffee_Free_Source',src=(PHOTO,REF))
grp,gmap=models['grinder'];asset('Coffee_Grinder_Real_EK43_Mesh',3.96,2.42,.94,[.25,.66,.36],grp,mm=gmap,group='Coffee_Free_Source',src=(PHOTO,REF))
bld,bmap=models['blender'];asset('Smoothie_Blender_Real_Model',2.95,2.44,.94,[.25,.47,.24],bld,mm=bmap,group='Coffee_Free_Source',src=(CLOSE,))
press,pmap=models['squeezer-c'];asset('Manual_Lever_Citrus_Press_Free_Real_Model',3.72,.95,.94,[.29,.70,.34],press,mm=pmap,group='Manual_Citrus_Prep',src=(PREP,CLOSE))
# Local photo-specific substantial base and extra support geometry, adapted to source family.
box('Press_Photo_Stainless_Baseplate',3.72,.94,.955,.30,.026,.35,'steel','Manual_Citrus_Prep',src=(PREP,CLOSE))
for s in [3.615,3.825]:cyl('Press_Photo_Twin_Support_'+str(s),s,1.06,1.265,.023,.61,'steel','Manual_Citrus_Prep',src=(PREP,CLOSE))
# Two physical reamers and collecting jugs visible on left of main press; custom source profile.
def lathe(key,profile,n=40):
 vs=[];ts=[]
 for y,r in profile:
  for i in range(n):a=i*math.tau/n;vs.append([r*math.cos(a),y,r*math.sin(a)])
 for j in range(len(profile)-1):
  for i in range(n):a=j*n+i;b=j*n+(i+1)%n;c=(j+1)*n+(i+1)%n;d=(j+1)*n+i;ts.extend([a,b,c,a,c,d])
 library[key]={'vertices':vs,'triangles':ts};return key
lathe('Reamer_Fluted_Steel',[(0,.07),(.018,.073),(.03,.058),(.075,.035),(.10,.012),(.102,0)])
lathe('Reamer_Black_Bowl',[(0,.068),(.005,.072),(.055,.088),(.060,.088),(.060,.077),(.015,.060),(.015,0)])
lathe('Measuring_Jug_Hollow',[(0,.043),(.004,.048),(.18,.063),(.182,.063),(.182,.059),(.008,.044),(.008,0)])
lathe('Cup_Actual_Transparent_Taper',[(0,.025),(.004,.029),(.125,.041),(.127,.043),(.132,.043),(.132,.039),(.005,.026),(.005,0)])
lathe('Cup_Domed_Clear_Lid',[(0,.044),(.005,.045),(.01,.043),(.023,.036),(.028,.025),(.03,.009),(.03,.003)])
for i,s in enumerate([3.01,3.28]):
 lib('Clear_Collecting_Jug_'+str(i),s,.94,.94,'Measuring_Jug_Hollow','glass','Manual_Citrus_Prep',src=(PREP,CLOSE))
 lib('Black_Reamer_Bowl_'+str(i),s,.94,1.13,'Reamer_Black_Bowl','black','Manual_Citrus_Prep',src=(PREP,CLOSE))
 lib('Stainless_Citrus_Reamer_'+str(i),s,.94,1.18,'Reamer_Fluted_Steel','steel','Manual_Citrus_Prep',src=(PREP,CLOSE))
# Real free knife/board meshes and their observed red surface, with half citrus.
asset('Red_Preparation_Board_RealMesh',4.48,.95,.94,[.58,.014,.34],foods['cutting-board'],material='red_board',group='Manual_Citrus_Prep',src=(PREP,CLOSE),collider=False)
asset('Working_Knife_RealMesh',4.78,1.01,.957,[.035,.025,.29],foods['cooking-knife'],material='steel',group='Manual_Citrus_Prep',src=(PREP,),collider=False)
asset('Cut_Citrus_On_Red_Board',4.36,.96,.959,[.088,.04,.08],models['orange-fruit'][0],mm=models['orange-fruit'][1],group='Manual_Citrus_Prep',src=(PREP,CLOSE),collider=False)
# Source-specific open GN produce containers with actualfree fruit meshes, not photo placards.
def openbin(key,w,d,h):
 vs=[];ts=[];loops=[]
 for ww,dd,y in [(w-.025,d-.025,0),(w,d,h),(w+.01,d+.01,h+.009),(w-.012,d-.012,h+.009),(w-.032,d-.032,.012)]:
  loop=[]
  for cx,cz,start in [(ww/2-.02,dd/2-.02,0),(-ww/2+.02,dd/2-.02,90),(-ww/2+.02,-dd/2+.02,180),(ww/2-.02,-dd/2+.02,270)]:
   for i in range(5):a=math.radians(start+i*22.5);loop.append(len(vs));vs.append([cx+.02*math.cos(a),y,cz+.02*math.sin(a)])
  loops.append(loop)
 for a,b in zip(loops,loops[1:]):
  for i in range(len(a)):j=(i+1)%len(a);ts.extend([a[i],b[i],b[j],a[i],b[j],a[j]])
 for i in range(1,len(loops[-1])-1):ts.extend([loops[-1][0],loops[-1][i],loops[-1][i+1]])
 library[key]={'vertices':vs,'triangles':ts}
openbin('Clear_Fruit_GN_Pan',.43,.42,.31);rng=random.Random(2511)
for i,(s,fruit,ma) in enumerate([(3.05,'orange','grapefruit'),(3.57,'orange','orange'),(4.09,'lemon','lemon')]):
 lib('Transparent_Fruit_Container_'+str(i),s,.37,.94,'Clear_Fruit_GN_Pan','glass','Citrus_Bins',src=(PREP,CLOSE))
 for level in range(3):
  for row in range(3):
   for col in range(3):
    ss=s+(col-1)*.109+rng.uniform(-.015,.015);rr=.37+(row-1)*.105+rng.uniform(-.012,.012);yy=.963+level*.095
    asset(f'Fresh_{fruit}_{i}_{level}_{row}_{col}',ss,rr,yy,[.105,.103,.101] if fruit=='orange' else [.097,.075,.077],foods[fruit] if fruit=='orange' else models['lemon-scan'][0],material=ma if fruit=='orange' else None,mm=None if fruit=='orange' else models['lemon-scan'][1],group='Free_3D_Fruit_Stock',src=(PREP,CLOSE),yaw=YAW+rng.uniform(-90,90),collider=False)
# Editable cups filled with visible kale/tomato on low glasscase shelves.
for row,yy in enumerate([.48,.74,1.0]):
 for k in range(5):
  s=.64+k*.15;rr=.41
  lib(f'Produce_Cup_{row}_{k}',s,rr,yy,'Cup_Actual_Transparent_Taper','cup_clear','Refrigerator_Stock',src=(REF,))
  if row==0:
   asset(f'Milk_Carton_{k}',s,rr,yy,[.085,.18,.08],foods['carton'],material='plastic',group='Refrigerator_Stock',src=(REF,),collider=False)
  else:
   for j in range(3):asset(f'Cup_Produce_{row}_{k}_{j}',s+(j-1)*.017,rr,yy+.025+j*.022,[.035,.033,.033],foods['salad'] if row==2 else foods['tomato'],material='kale' if row==2 else 'tomato',group='Refrigerator_Stock',src=(REF,),collider=False)
# Bottle silhouettes are actual free geometry, spaced on full-height refrigerator shelves.
for level,yy in enumerate([.27,.59,.91,1.23,1.55]):
 for k in range(5):
  s=1.70+k*.125;rr=.40;asset(f'Cold_Beverage_Bottle_{level}_{k}',s,rr,yy,[.075,.22,.075],foods['soda-bottle'],material='brown' if level>2 else 'water' if level==0 else 'plastic',group='Refrigerator_Stock',src=(PHOTO,REF),collider=False)
# Source cup stacks, syrup pumps, service consumables on physical surfaces.
for stack,s in enumerate([2.7,5.34]):
 for k in range(8):lib(f'Takeaway_Cup_Stack_{stack}_{k}',s,2.45,.94+k*.027,'Cup_Actual_Transparent_Taper','cup_clear','Service_Smallwares',src=(PHOTO,REF))
for k in range(5):
 s=3.1+k*.15;asset('Source_Syrup_Bottle_'+str(k),s,2.65,.94,[.085,.27,.085],foods['soda-bottle'],material='brown',group='Service_Smallwares',collider=False)
# Physical service bin, towel and cup staging, directly visible but exact sizesMODEL.
box('Service_Paper_Towel_Dispenser',5.48,2.64,1.09,.24,.30,.18,'white','Service_Smallwares')
for k in range(6):box('Visible_Folded_Towel_'+str(k),5.69,2.54,.955+k*.014,.24,.013,.18,'plastic','Service_Smallwares',collider=False)
# Independent editable food cups at point of sale, plus source observed berries/banana storage.
for i in range(3):asset('Banana_Stock_FreeMesh_'+str(i),2.55+i*.15,2.64,.96,[.19,.10,.12],foods['banana'],material='lemon',group='Service_Smallwares',collider=False)

packet={'schema':'chooguard.store-source-model.r25.v3','store_key':'american-trailer','origin_q':ORIGIN,'floor_world_y':7.0,'source_packet':str((BASE/'SOURCE-EVIDENCE-R25.json').relative_to(ROOT)),'placement_basis':str((BASE/'MODEL-PLACEMENT-BASIS-R25.json').relative_to(ROOT)),'materials':materials,'source_imports':imports,'mesh_library':library,'objects':objects,'all_dimensions_MODEL_not_field_measured':True,'native_objects_changed':False,'staff_access_and_publichall_preserved':True,'north_divider_owned_by_pharmacy':True,'kind_text_count':0,'hidden_backroom_not_reconstructed':True}
(BASE/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
(BASE/'ASSET-CREDITS-R25.json').write_text(json.dumps({'store_key':'american-trailer','credits':credits,'source_assets_unchanged':True,'own_source_geometry_and_PBR_derivatives':True},ensure_ascii=False,indent=2))
print('R25_AMERICAN_PACKET',len(objects),'objects',len(materials),'materials',len(imports),'sourceimports','physicalsigns',sum(1 for x in objects if x['group']=='Physical_Front_Sign'))
