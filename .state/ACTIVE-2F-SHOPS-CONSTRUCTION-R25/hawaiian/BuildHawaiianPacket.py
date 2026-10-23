from pathlib import Path
import json,hashlib,math,sys,shutil,collections
import cv2,numpy as np
from PIL import Image
ROOT=Path('/Users/um-yunsang/CHOOGuard');P=Path(__file__).parent;S=P/'sources';AS='Assets/ChooGuard/Art/StationInterior/R25_Shops/hawaiian'
sys.path.insert(0,str(P.parent));from photo_outline_to_solid import closed_solid_from_mask
B=json.loads((P/'MODEL-PLACEMENT-BASIS-R25.json').read_text());base=B['base_q'];U=B['u'];V=B['v'];ANGLE=-math.degrees(math.atan2(U[1],U[0]));objects=[];imports=[];mats={};lib={};free_uses=[]
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def imp(src,dst):
 src=Path(src).resolve();r={'source':str(src),'destination':dst,'sha256':sha(src)}
 if r not in imports:imports.append(r)
 return AS+'/'+dst
def q(u,v):return [base[i]+U[i]*u+V[i]*v for i in range(2)]
def mat(n,c,**kw):mats[n]={'color':c,'smoothness':kw.pop('smoothness',.38),**kw};return n
def add(kind,id,u,v,y,group,material=None,yaw=0,**kw):
 o={'kind':kind,'id':id,'group':group,'q':q(u,v),'y':y,'yaw':yaw+ANGLE,'source_ids':kw.pop('source_ids',['T2026-05','T2026-06','T2026-07']),'qualification':'PHOTO_REFERENCED_MODEL_NOT_FIELD_MEASUREMENT',**kw}
 if material:o['material']=material
 objects.append(o);return o
def box(id,u,v,y,size,ma,group,yaw=0,**kw):return add('box',id,u,v,y,group,ma,yaw,size=size,**kw)
def cyl(id,u,v,y,r,h,ma,group,**kw):return add('cylinder',id,u,v,y,group,ma,size=[r*2,h/2,r*2],**kw)
def edge(id,a,b,bottom,height,thick,ma,group):
 objects.append({'kind':'edge','id':id,'group':group,'a':q(*a),'b':q(*b),'height':height,'bottom':bottom,'thickness':thick,'material':ma,'source_ids':['T2026-04','Z2026-00'],'qualification':'PHOTO_REFERENCED_MODEL_NOT_FIELD_MEASUREMENT'})
def libobj(id,key,u,v,y,ma,group,yaw=0,**kw):return add('library_mesh',id,u,v,y,group,ma,yaw,mesh_key=key,**kw)
def photo_rect(name,src,points,wh):
 im=cv2.imread(str(S/src));M=cv2.getPerspectiveTransform(np.float32(points),np.float32([[0,0],[wh[0],0],[wh[0],wh[1]],[0,wh[1]]]))
 out=cv2.warpPerspective(im,M,wh);fn=S/(name+'.png');cv2.imwrite(str(fn),out);return fn,out
# Physical source sign lettering. Actual source photograph extracted, no font or text component.
rect,im=photo_rect('BRAND-WHITE-RECTIFIED','ROOT-zlsangkkun-224445787295-03.jpg',[[25,110],[827,104],[826,179],[22,184]],(1600,150))
hsv=cv2.cvtColor(im,cv2.COLOR_BGR2HSV);mask=((hsv[:,:,2]>177)&(hsv[:,:,1]<62)).astype('uint8')*255
# Retain source letter components and internal holes, drop edge glare specks.
num,labels,stats,cent=cv2.connectedComponentsWithStats(mask)
clean=np.zeros_like(mask)
for i in range(1,num):
 x,y,w,h,a=stats[i]
 if a>=26 and h>10 and x>1 and x+w<1598:clean[labels==i]=255
cv2.imwrite(str(S/'BRAND-SOURCE-INK-MASK.png'),clean)
lib['brand_outline']=closed_solid_from_mask(clean,3.8,.36,.038,minimum_area=22,epsilon=.48)
# Actual fridge header separate letter solids from actual field photograph.
im=cv2.imread(str(S/'ROOT-tkvlf14-224399139134-17.jpg'))[15:148,83:920]
hsv=cv2.cvtColor(im,cv2.COLOR_BGR2HSV);mask=((hsv[:,:,2]>190)&(hsv[:,:,1]<65)).astype('uint8')*255
cv2.imwrite(str(S/'CHILLER-SOURCE-INK-MASK.png'),mask)
lib['chiller_letters']=closed_solid_from_mask(mask,1.03,.125,.012,minimum_area=40,epsilon=.6)
# Actual source cabinet vinyl lettering is physically modeled too.
im=cv2.imread(str(S/'ROOT-tkvlf14-224399139134-05.jpg'))[525:551,166:425]
mask=(cv2.cvtColor(im,cv2.COLOR_BGR2HSV)[:,:,2]<97).astype('uint8')*255
cv2.imwrite(str(S/'PICKUP-RETURN-SOURCE-INK-MASK.png'),mask)
lib['pickup_letters']=closed_solid_from_mask(mask,1.24,.10,.0025,minimum_area=8,epsilon=.35)
# Full material palettes are independently authored, source originals unchanged.
mat('black',[.018,.02,.021,1],smoothness=.52);mat('cream',[.89,.87,.78,1]);mat('white',[.95,.94,.89,1]);mat('green',[.11,.37,.35,1]);mat('wood',[.62,.43,.23,1]);mat('darkwood',[.23,.13,.065,1]);mat('lightwood',[.76,.61,.40,1]);mat('steel',[.62,.64,.64,1],metallic=.87,smoothness=.66);mat('chrome',[.77,.78,.79,1],metallic=.95,smoothness=.82);mat('brass',[.58,.39,.14,1],metallic=.77,smoothness=.6);mat('glass',[.75,.89,.88,.11],transparent=True,smoothness=.96);mat('cellophane',[.94,.94,.9,.16],transparent=True,smoothness=.95);mat('rattan',[.66,.47,.25,1]);mat('signwhite',[.97,.98,1,1],emission=[.8,.9,1,1]);mat('warm',[1,.82,.47,1],emission=[1,.63,.25,1]);mat('brown',[.23,.12,.055,1]);mat('leaf',[.09,.27,.10,1]);mat('pastelpink',[.91,.68,.76,1]);mat('pastelmint',[.59,.81,.77,1]);mat('pastelcream',[.87,.78,.59,1]);mat('pastelpeach',[.91,.69,.51,1]);mat('coffee',[.18,.09,.035,1]);mat('juice',[.99,.6,.055,1]);mat('berry',[.60,.12,.18,1]);mat('cup',[.92,.89,.78,1])
# Source timber surface, not any plan imagery.
im=cv2.imread(str(S/'ROOT-tkvlf14-224399139134-11.jpg'));timber=im[38:108,110:822];cv2.imwrite(str(S/'SOURCE-TIMBER-SURFACE.png'),timber);woodtex=imp(S/'SOURCE-TIMBER-SURFACE.png','Textures/source_timber.png')
for k in ['wood','darkwood','lightwood']:mats[k]['texture_asset']=woodtex
# Source PBR downloaded models imported only into own Assets under the eventual lease.
def imported(uid,label):
 f=S/'models'/uid;dst='FreeModels/'+label
 asset=imp(f/(uid+'.fbx'),dst+'/'+label+'.fbx');doc=json.loads((f/'UNITY-MATERIAL-SOURCES.json').read_text());maps={}
 for j,m in enumerate(doc['materials']):
  name=label+'_'+str(j);params={'color':[1,1,1,1],'smoothness':.42}
  for t in m['textures']:
   if not t['file']:continue
   src=Path(t['file']);slot=t['slot']
   if slot=='BASE COLOR':params['texture_asset']=imp(src,dst+'/base_'+str(j)+'.png')
   elif slot=='NORMAL MAP':
    n=f/('normal_'+str(j)+'.png');shutil.copyfile(src,n);params['normal_asset']=imp(n,dst+'/'+str(j)+'_normal.png')
   elif slot=='METALLIC ROUGHNESS':
    a=np.array(Image.open(src).convert('RGB'));rgba=np.zeros((*a.shape[:2],4),dtype='uint8');rgba[:,:,:3]=a[:,:,2,None];rgba[:,:,3]=255-a[:,:,1];n=f/('metallic_smoothness_'+str(j)+'.png');Image.fromarray(rgba).save(n);params['metallic_gloss_asset']=imp(n,dst+'/'+str(j)+'_metallic_smoothness.png')
  mats[name]=params;maps[m['name']]=name
 maps['*']=label+'_0'
 free_uses.append({'asset':asset,'source_acquisition':str(f/'ACQUISITION.json'),'model_label':label})
 return asset,maps
chair,cm=imported('8d2fb8b8215f41fba59d0ec4606fb6b3','bistro_chair')
bent,bm=imported('e76cb526238a493c90255ad4a900f83d','bentwood_chair')
coffee,em=imported('bab05bc7805d4b0dbc2cdcbb48441956','espresso_machine')
bm={k:'bentwood_chair_1' for k in bm}
# Adapt only own chair texture: dark/cream weave and blonde frame qualify as field-photo model.
chairtex=S/'models/8d2fb8b8215f41fba59d0ec4606fb6b3/textures/Image_0.png';a=cv2.imread(str(chairtex));hsv=cv2.cvtColor(a,cv2.COLOR_BGR2HSV);sel=(hsv[:,:,0]>32)&(hsv[:,:,0]<105)&(hsv[:,:,1]>30);a[sel]=(a[sel].astype(float)*.22).astype('uint8');n=S/'models/8d2fb8b8215f41fba59d0ec4606fb6b3/FIELD-DARK-WEAVE.png';cv2.imwrite(str(n),a);mats['bistro_chair_0']['texture_asset']=imp(n,'FreeModels/bistro_chair/field_dark_weave.png')
# Real PolyHaven scanned bread meshes/PBR, source copies staged in this store.
foodassets={}
for label in ['croissant','hamburger_buns']:
 f=S/'models'/label;dst='FreeModels/'+label;asset=imp(f/(label+'.fbx'),dst+'/'+label+'.fbx');params={'color':[1,1,1,1],'smoothness':.32,'texture_asset':imp(f/'diff.jpg',dst+'/diff.jpg')}
 if (f/'normal.exr').exists():params['normal_asset']=imp(f/'normal.exr',dst+'/normal.exr')
 if (f/'metallic_smoothness.png').exists():params['metallic_gloss_asset']=imp(f/'metallic_smoothness.png',dst+'/metallic_smoothness.png')
 mats[label]=params;foodassets[label]=asset;free_uses.append({'asset':asset,'source':'PolyHaven CC0','provenance_copy':str(S/'models/BNC-POLYHAVEN-LICENSE-SOURCE.json')})
# Adapted original downloaded commercial refrigerator body to actual open field chiller.
chiller=imp(S/'models/open-chiller/Open-Chiller-PhotoAdapted.fbx','FreeModels/open_chiller/Open-Chiller-PhotoAdapted.fbx');free_uses.append({'asset':chiller,'source':'Sean Thomas/Khronos CC-BY4.0','derivation':str(S/'models/open-chiller/MODEL-DERIVATION.json')})
# Real downloaded POS and food/smallwares.
F=P.parent/'free-assets';pos=imp(F/'pos-terminal/POS-Terminal.fbx','FreeModels/pos/POS-Terminal.fbx');posbase=imp(F/'pos-terminal/textures/Image_0.png','FreeModels/pos/base.png');mat('pos',[1,1,1,1],texture_asset=posbase,smoothness=.55)
ken={}
for label in ['cup','cup-coffee','cup-saucer','soda-bottle','bag','cookie-chocolate','donut','muffin']:
 ken[label]=imp(F/('kenney-food-kit/Models/FBX format/'+label+'.fbx'),'FreeModels/kenney/'+label+'.fbx')
free_uses.extend([{'asset':pos,'source':'Vladyslav.Yarovyi CC-BY4.0','source_receipt':str(F/'pos-terminal/ACQUISITION.json')},{'assets':list(ken.values()),'source':'Kenney Food Kit CC0','source_receipt':str(F/'KENNEY-FOOD-KIT-ACQUISITION.json')}])
# Printed source artwork only on actual physical menu/product panels.
def print_material(name,src,points,wh=(512,768)):
 f,im=photo_rect('PRINT-'+name,src,points,wh);return mat(name,[1,1,1,1],texture_asset=imp(f,'Prints/'+name+'.png'),smoothness=.25)
def print_quad(id,u,v,y,w,h,ma,group,yaw=180):
 verts=[[w/2,-h/2,0],[-w/2,-h/2,0],[-w/2,h/2,0],[w/2,h/2,0]]
 return add('mesh',id,u,v,y,group,ma,yaw,vertices=verts,triangles=[0,2,1,0,3,2],uv=[[0,0],[1,0],[1,1],[0,1]],collider=False)
menuquads=[[[76,309],[223,306],[217,578],[80,577]],[[266,306],[420,307],[419,584],[260,580]],[[459,307],[608,307],[610,586],[455,584]],[[646,304],[815,300],[824,586],[641,587]]]
for i,points in enumerate(menuquads):print_material('menu_'+str(i),'ROOT-zlsangkkun-224445787295-03.jpg',points)
# Product package source print surfaces: photograph of actual branch retail wall.
for i,(x0,y0,x1,y1) in enumerate([(208,326,286,406),(288,326,366,405),(368,325,441,403),(443,324,516,402),(519,323,589,402),(591,323,660,401),(663,323,729,401)]):
 print_material('gift_'+str(i),'ROOT-tkvlf14-224399139134-11.jpg',[[x0,y0],[x1,y0],[x1,y1],[x0,y1]],(192,224))
for i,(x0,x1) in enumerate([(186,276),(310,385),(416,479),(502,565),(579,641),(665,728),(746,806)]):
 print_material('pouch_'+str(i),'ROOT-tkvlf14-224399139134-11.jpg',[[x0,133],[x1,143],[x1,272],[x0,270]],(192,300))
# Woven lamps constructed as real ribs and rings from the viewed field profile.
def tube_mesh(curves,radius=.005,sides=5):
 verts=[];tri=[]
 for pts in curves:
  pts=[np.array(x,float) for x in pts]
  for j,p in enumerate(pts):
   t=pts[min(len(pts)-1,j+1)]-pts[max(0,j-1)];t/=max(np.linalg.norm(t),1e-9);ref=np.array([0,1,0])
   if abs(t@ref)>.95:ref=np.array([1,0,0])
   a=np.cross(t,ref);a/=np.linalg.norm(a);b=np.cross(t,a)
   for k in range(sides):verts.append((p+radius*(a*math.cos(k*2*math.pi/sides)+b*math.sin(k*2*math.pi/sides))).tolist())
  start=len(verts)-len(pts)*sides
  for j in range(len(pts)-1):
   for k in range(sides):
    a=start+j*sides+k;b=start+j*sides+(k+1)%sides;c=b+sides;d=a+sides;tri.extend([a,b,c,a,c,d])
 return {'vertices':verts,'triangles':tri}
def shade(shape):
 h=.56 if shape==0 else .43;R=.33 if shape==0 else .28
 def rad(t):return .055+R*(math.sin(t*math.pi*.75)**.80)
 curves=[]
 for k in range(44):
  a=k*2*math.pi/44;curves.append([[rad(t)*math.cos(a),-h*t,rad(t)*math.sin(a)] for t in np.linspace(0,1,22)])
 for t in np.linspace(.1,1,16):curves.append([[rad(t)*math.cos(a),-h*t,rad(t)*math.sin(a)] for a in np.linspace(0,2*math.pi,65)])
 return tube_mesh(curves,.0048,5)
lib['rattan_shade_0']=shade(0);lib['rattan_shade_1']=shade(1)
# Cafe own fitted shell: retained floor/rear facade/north wall are reused unchanged.
edge('front_black_header',[8.92,2.],[8.92,11.36],2.65,.55,.18,'black','GlassFrontAndPhysicalSigns')
# Front seating glazing and framing; actual source-visible entrance is left open.
for k,(a,b) in enumerate([(2.,3.55),(3.55,5.1),(5.1,6.65),(6.65,8.2),(10.1,11.36)]):
 edge('front_glass_'+str(k),[8.985,a+.035],[8.985,b-.035],.08,2.55,.012,'glass','GlassFrontAndPhysicalSigns')
 edge('front_bottom_'+str(k),[8.98,a],[8.98,b],.015,.065,.065,'black','GlassFrontAndPhysicalSigns')
for k,b in enumerate([2.04,3.55,5.1,6.65,8.2,10.1,11.32]):box('front_mullion_'+str(k),8.975,b,1.35,[.065,2.7,.065],'black','GlassFrontAndPhysicalSigns')
# The front opening remains 1.9m clear. Source doors modeled folded against own inside short return.
for k,(u,vv) in enumerate([(8.65,8.23),(8.65,10.06)]):
 for side in [-1,1]:box('open_leaf_stile_'+str(k)+'_'+str(side),u+side*.29,vv,1.32,[.055,2.64,.055],'black','GlassFrontAndPhysicalSigns')
 for yy in [.03,2.61]:box('open_leaf_rail_'+str(k)+'_'+str(yy),u,vv,yy,[.63,.06,.055],'black','GlassFrontAndPhysicalSigns')
 box('open_leaf_glass_'+str(k),u,vv,1.32,[.51,2.48,.012],'glass','GlassFrontAndPhysicalSigns',collider=False)
 box('door_handle_'+str(k),u+.12,vv-.05,1.12,[.025,.46,.025],'steel','GlassFrontAndPhysicalSigns',collider=False)
edge('south_source_glazed_return',[.27,2.06],[8.94,2.06],.08,2.55,.012,'glass','SouthOwnSideSourceGlass')
edge('south_black_upper',[.27,2.10],[8.94,2.10],2.65,.48,.15,'black','SouthOwnSideSourceGlass')
for k,u0 in enumerate(np.linspace(.27,8.94,7)):
 box('south_frame_'+str(k),float(u0),2.07,1.34,[.06,2.68,.06],'black','SouthOwnSideSourceGlass')
# Actual white lettering is thick closed source meshes, with separate black panel and emitter.
libobj('front_actual_source_brand','brand_outline',9.015,8.55,2.94,'signwhite','GlassFrontAndPhysicalSigns',90,collider=False,source_ids=['Z2026-03','Z2026-00'])
# Cafe visible ceiling; kept within own work polygon and below fixed original structure.
box('photo_white_ceiling',4.62,6.69,3.34,[8.64,.09,9.15],'white','VisibleCeiling')
for a,b in [((.45,2.29),(8.8,2.29)),((.45,11.05),(8.8,11.05)),((.45,2.29),(.45,11.05)),((8.8,2.29),(8.8,11.05))]:edge('cove_'+str(len(objects)),a,b,3.22,.06,.06,'warm','VisibleCeiling')
for i,u0 in enumerate([1.2,3.2,5.2,7.25]):
 for j,v0 in enumerate([3.,5.5,8.,10.6]):cyl('downlight_'+str(i)+'_'+str(j),u0,v0,3.275,.055,.017,'warm','VisibleCeiling',collider=False)
# Main bar: long physical timber cabinet with separate toe, top, panel joins and underlight.
box('bar_toe',5.65,9.92,.10,[4.4,.18,.78],'darkwood','CoffeeCounter')
box('bar_cabinet',5.65,9.92,.55,[4.4,.84,.81],'lightwood','CoffeeCounter')
box('bar_top',5.65,9.92,.995,[4.55,.065,.9],'darkwood','CoffeeCounter')
box('bar_underlighting',5.65,9.48,.16,[4.35,.035,.025],'warm','CoffeeCounter',collider=False)
for i in range(8):box('bar_panel_join_'+str(i),3.48+i*.55,9.507,.57,[.007,.76,.008],'wood','CoffeeCounter',collider=False)
libobj('actual_pickup_return_source_letters','pickup_letters',4.5,9.506,.66,'black','CoffeeCounter',180,collider=False,source_ids=['T2026-05'])
# Source-visible rear worktop, espresso and sink, no hidden BOH invented.
box('visible_rear_worktop',5.7,10.98,.91,[4.3,.065,.64],'steel','VisibleBackbar')
box('visible_rear_understorage',5.7,10.98,.45,[4.25,.84,.59],'cream','VisibleBackbar')
add('asset','downloaded_commercial_espresso',5.4,10.92,.96,'VisibleBackbar',None,180,asset=coffee,fit_size=[1.05,.57,.58],material_map=em)
# Actual-shaped sink bowl is a separate open rim and basin; no label proxy.
box('sink_back_dark_opening',3.9,10.94,.954,[.57,.009,.40],'black','VisibleBackbar',collider=False)
for i,(uu,vv,ss) in enumerate([(3.60,10.94,[.028,.04,.45]),(4.20,10.94,[.028,.04,.45]),(3.9,10.715,[.62,.04,.03]),(3.9,11.165,[.62,.04,.03])]):box('sink_rim_'+str(i),uu,vv,.97,ss,'steel','VisibleBackbar',collider=False)
cyl('sink_tap_stem',3.88,11.18,1.13,.018,.35,'chrome','VisibleBackbar',collider=False)
box('sink_tap_spout',3.88,11.08,1.30,[.034,.034,.22],'chrome','VisibleBackbar',collider=False)
# Downloaded POS terminal plus source-visible cup stock and pump bottles.
add('asset','downloaded_pos',7.24,9.82,1.034,'CoffeeCounter','pos',180,asset=pos,fit_size=[.38,.37,.33])
for i in range(7):
 add('asset','counter_pump_bottle_'+str(i),3.57+i*.18,10.65,.965,'VisibleBackbar','berry' if i%3==0 else 'coffee',asset=ken['soda-bottle'],fit_size=[.09,.29,.09],collider=False)
 box('pump_head_'+str(i),3.57+i*.18,10.65,1.285,[.12,.025,.025],'black','VisibleBackbar',collider=False)
for i in range(4):
 for j in range(4):add('asset','counter_stack_cup_'+str(i)+'_'+str(j),6.25+i*.11,10.78,.97+j*.045,'VisibleBackbar','pastelmint',asset=ken['cup'],fit_size=[.09,.105,.09],collider=False)
# Actual green canopy with photo-traced 3D letters and four physically framed prints.
box('green_rear_canopy',5.52,10.72,2.94,[4.90,.45,1.1],'green','BackbarPhysicalSignAndMenus')
libobj('rear_actual_source_brand','brand_outline',5.52,10.145,2.99,'signwhite','BackbarPhysicalSignAndMenus',180,size=[1,.94,1],collider=False,source_ids=['Z2026-03','T2026-05'])
for i,u0 in enumerate([3.88,4.97,6.06,7.15]):
 box('menu_physical_frame_'+str(i),u0,10.40,2.03,[.91,1.34,.055],'cream','BackbarPhysicalSignAndMenus')
 print_quad('source_menu_print_'+str(i),u0,10.366,2.03,.84,1.25,'menu_'+str(i),'BackbarPhysicalSignAndMenus')
 # Actual suspension hardware separately modeled.
 for side in [-1,1]:box('menu_hanger_'+str(i)+'_'+str(side),u0+side*.31,10.41,2.69,[.013,.20,.013],'steel','BackbarPhysicalSignAndMenus',collider=False)
# Actual open chiller derivative with source-shaped illuminated header (no IcyDrinks source identity).
add('asset','downloaded_photo_adapted_open_chiller',8.10,10.67,0,'ColdMerchandise',None,180,asset=chiller,fit_size=[1.18,1.97,.66],material_map={'FridgeInterior':'steel','FridgeCase':'steel','*':'steel'})
box('chiller_actual_header',8.10,10.31,1.82,[1.15,.23,.06],'steel','ColdMerchandise')
libobj('source_sandwich_beverage_header','chiller_letters',8.1,10.266,1.83,'signwhite','ColdMerchandise',180,collider=False,source_ids=['T2026-17'])
for lev in range(4):
 for i in range(7):
  add('asset','cold_bottle_'+str(lev)+'_'+str(i),7.66+i*.145,10.52,.25+lev*.36,'ColdMerchandise','juice' if lev==3 else 'berry' if lev==2 else 'pastelmint',asset=ken['soda-bottle'],fit_size=[.085,.25,.085],collider=False)
# Photo-observed bakery island is open wood with baskets, actual bread meshes and cellophane wraps.
box('bakery_island_toe',4.65,6.8,.09,[4.1,.16,1.32],'darkwood','OpenBakeryIsland')
box('bakery_island_body',4.65,6.8,.50,[4.1,.80,1.32],'lightwood','OpenBakeryIsland')
box('bakery_island_top',4.65,6.8,.945,[4.28,.08,1.46],'darkwood','OpenBakeryIsland')
for vv in [6.125,7.475]:box('island_underlight_'+str(vv),4.65,vv,.13,[4.05,.027,.018],'warm','OpenBakeryIsland',collider=False)
for i in range(9):box('island_panel_seam_'+str(i),2.65+i*.45,6.13,.53,[.007,.73,.008],'wood','OpenBakeryIsland',collider=False)
# Each real-source bread instance is editable, independently fitted with source PBR.
for row in range(3):
 for col in range(10):
  uu=2.90+col*.38;vv=6.36+row*.43;label='croissant' if (row+col)%3 else 'hamburger_buns';fit=[.31,.15,.23] if label=='croissant' else [.24,.13,.24]
  box('bread_tray_'+str(row)+'_'+str(col),uu,vv,1.005,[.34,.027,.31],'wood','BakeryProducts',collider=False)
  add('asset','scanned_bread_'+str(row)+'_'+str(col),uu,vv,1.025,'BakeryProducts',label,(col%3-1)*12,asset=foodassets[label],fit_size=fit,collider=False)
  # Physical loose clear wrapping follows individual product volume.
  if col%2==0:box('clear_wrapper_'+str(row)+'_'+str(col),uu,vv,1.12,[.32,.21,.26],'cellophane','BakeryProducts',collider=False)
# Central higher stand with real bag/pouch merchandise.
box('island_upper_riser',4.65,6.8,1.22,[3.7,.035,.26],'darkwood','OpenBakeryIsland')
for col in range(12):add('asset','bagged_cookie_'+str(col),2.95+col*.30,6.8,1.25,'BakeryProducts','pastelcream',asset=ken['bag'],fit_size=[.20,.24,.12],collider=False)
# Retail shelf: own physical timber boards and individually modeled printed cartons/pouches.
box('retail_wood_back',1.25,10.15,1.22,[1.62,2.24,.055],'darkwood','RetailGiftWall')
for lev in range(4):
 box('retail_shelf_'+str(lev),1.25,9.97,.32+lev*.56,[1.76,.045,.42],'darkwood','RetailGiftWall')
 if lev<3:
  for col in range(7):
   uu=.55+col*.235;y0=.36+lev*.56
   if lev==2:
    add('asset','actual_coffee_pouch_'+str(lev)+'_'+str(col),uu,9.94,y0,'RetailGiftWall','pastelmint',asset=ken['bag'],fit_size=[.19,.31,.10],collider=False)
    print_quad('pouch_source_print_'+str(lev)+'_'+str(col),uu,9.876,y0+.155,.18,.30,'pouch_'+str(col),'RetailGiftWall')
   else:
    for depth in range(2):
     box('actual_gift_carton_'+str(lev)+'_'+str(col)+'_'+str(depth),uu,9.94,y0+.105+depth*.215,[.205,.21,.14],['pastelpink','pastelmint','pastelcream','pastelpeach'][col%4],'RetailGiftWall',collider=False)
     print_quad('carton_source_print_'+str(lev)+'_'+str(col)+'_'+str(depth),uu,9.863,y0+.105+depth*.215,.202,.206,'gift_'+str(col),'RetailGiftWall')
# Open pedestal tables / downloaded source chairs: observed pattern and silhouette retained as bounded model.
seats=[(1.45,3.55),(3.7,3.55),(6.0,3.55),(1.50,5.9),(7.55,5.9),(1.5,8.05)]
for i,(uu,vv) in enumerate(seats):
 r=.37 if i<4 else .33
 cyl('table_pedestal_base_'+str(i),uu,vv,.035,.22,.055,'brass','CafeSeating')
 cyl('table_pedestal_stem_'+str(i),uu,vv,.38,.035,.69,'brass','CafeSeating')
 cyl('round_table_top_'+str(i),uu,vv,.748,r,.045,'darkwood','CafeSeating')
 for j,sgn in enumerate([-1,1]):
  cvv=vv+sgn*.67;asset=chair if (i+j)%2==0 else bent;mp=cm if asset==chair else bm
  add('asset','downloaded_cafe_chair_'+str(i)+'_'+str(j),uu,cvv,0,'CafeSeating',None,0 if sgn>0 else 180,asset=asset,fit_size=[.46,.9,.50],material_map=mp)
 for j in range(2):
  add('asset','table_cup_'+str(i)+'_'+str(j),uu+(-.16 if j==0 else .16),vv,.78,'CafeSeating','pastelmint',asset=ken['cup-coffee'],fit_size=[.09,.13,.09],collider=False)
# Actual woven rattan pendants, source-derived physical ribs/rings and lamps.
for i,(uu,vv,h0) in enumerate([(3.0,6.3,2.76),(4.25,6.6,2.92),(5.4,6.55,2.72),(6.2,7.15,3.02),(3.55,7.2,3.02),(5.0,7.35,2.88),(1.7,5.1,3.02)]):
 libobj('source_woven_pendant_'+str(i),'rattan_shade_'+str(i%2),uu,vv,h0,'rattan','WovenPendants',collider=False)
 cyl('pendant_cable_'+str(i),uu,vv,(h0+3.28)/2,.008,3.28-h0,'steel','WovenPendants',collider=False)
 add('sphere','pendant_bulb_'+str(i),uu,vv,h0-.27,'WovenPendants','warm',size=[.10,.13,.10],collider=False)
# Ceiling fans: actual rounded wood blades, source-visible fixed model only; no animation/probe.
# Rounded closed blades rather than labels or rectangles.
curves=[]
for k in range(5):
 a=k*2*math.pi/5
 pts=[]
 for t in np.linspace(0,1,18):
  aa=a+(.18*math.sin(t*math.pi));rr=.15+.55*t;pts.append([rr*math.cos(aa),0,rr*math.sin(aa)])
 curves.append(pts)
# Construct broad solid elliptical blades by triangle fans + side faces.
verts=[];tris=[]
for k in range(5):
 a=k*2*math.pi/5;cx=.43*math.cos(a);cz=.43*math.sin(a);n0=len(verts)
 ring=[]
 for i in range(32):
  t=i*math.tau/32;x=.29*math.cos(t);z=.11*math.sin(t);ring.append([cx+x*math.cos(a)-z*math.sin(a),0,cz+x*math.sin(a)+z*math.cos(a)])
 for yy in [-.012,.012]:verts.extend([[x,yy,z] for x,_,z in ring])
 for i in range(1,31):tris.extend([n0,n0+i+1,n0+i,n0+32,n0+32+i,n0+32+i+1])
 for i in range(32):j=(i+1)%32;tris.extend([n0+i,n0+j,n0+j+32,n0+i,n0+j+32,n0+i+32])
lib['fan_blades']={'vertices':verts,'triangles':tris}
for i,(uu,vv) in enumerate([(2.4,4.25),(5.8,8.2)]):
 cyl('fan_downrod_'+str(i),uu,vv,3.08,.018,.36,'brass','SourceCeilingFans',collider=False)
 cyl('fan_motor_'+str(i),uu,vv,2.87,.105,.12,'brass','SourceCeilingFans',collider=False)
 libobj('source_wood_fan_blades_'+str(i),'fan_blades',uu,vv,2.88,'wood','SourceCeilingFans',collider=False)
# Source-visible compact ice appliance uses a real downloaded cabinet derivative as its physical base.
add('asset','downloaded_cabinet_photo_adapted_ice_appliance',2.72,10.86,0,'VisibleBackbar','steel',180,asset=chiller,fit_size=[.69,1.65,.67])
box('ice_appliance_white_top',2.72,10.49,1.53,[.68,.15,.05],'white','VisibleBackbar')
for level in range(2):
 for side in [-1,1]:
  uu=2.72+side*.16;yy=.82+level*.40
  box('ice_vent_dark_'+str(level)+'_'+str(side),uu,10.50,yy,[.285,.32,.025],'black','VisibleBackbar')
  for rib in range(9):box('ice_vent_rib_'+str(level)+'_'+str(side)+'_'+str(rib),uu-.13+rib*.032,10.479,yy,[.009,.315,.009],'steel','VisibleBackbar',collider=False)
box('ice_lower_service_panel',2.72,10.49,.30,[.65,.48,.045],'steel','VisibleBackbar')
# Actual source-visible broad tropical foliage: curved leaf solids and veins, no labels.
leafverts=[];leaftris=[]
for k in range(8):
 a=k*math.tau/8;L=.7+.18*(k%3);start=len(leafverts);cx=.18*math.cos(a);cz=.18*math.sin(a)
 for j in range(13):
  t=j/12;rad=.12+.42*t;x=cx+math.cos(a)*rad;z=cz+math.sin(a)*rad;y=.55+L*t-.28*t*t;w=.13*math.sin(math.pi*t)**.7
  for side in [-1,1]:leafverts.append([x-side*w*math.sin(a),y-.07*abs(side)*math.sin(math.pi*t),z+side*w*math.cos(a)])
 for j in range(12):
  a0=start+j*2;b=a0+1;c=a0+3;d=a0+2;leaftris.extend([a0,b,c,a0,c,d,c,b,a0,d,c,a0])
lib['source_broad_tropical_leaves']={'vertices':leafverts,'triangles':leaftris}
for i,(uu,vv) in enumerate([(7.70,2.85),(.85,8.0)]):
 cyl('tropical_planter_'+str(i),uu,vv,.27,.23,.54,'cream','SourceVisiblePlants')
 libobj('source_tropical_foliage_'+str(i),'source_broad_tropical_leaves',uu,vv,.36,'leaf','SourceVisiblePlants',collider=False)
 for stem in range(7):cyl('tropical_stem_'+str(i)+'_'+str(stem),uu+.12*math.cos(stem),vv+.12*math.sin(stem),.82,.009,1.1,'leaf','SourceVisiblePlants',collider=False)

# Existing rear public portal and native facade remain open; no new wall or door across it.
packet={'store_key':'hawaiian','origin_q':base,'floor_world_y':7,'source_packet':'SOURCE-EVIDENCE-R25.json','materials':mats,'source_imports':imports,'mesh_library':lib,'objects':objects,'physical_source_signs':True,'placement_basis':'MODEL-PLACEMENT-BASIS-R25.json','no_QA_Play_tests_reopen_capture':True}
(P/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
(P/'ACTUAL-FREE-MODEL-USES-R25.json').write_text(json.dumps(free_uses,ensure_ascii=False,indent=2))
# Preserve license text within runtime model folder, imported during active lease.
credits=['HAWAIIAN R25 - THIRD PARTY SOURCE CREDITS','Actual source models fitted and material-adapted; exact branch manufacturer/surveyed equality not asserted.','Maison Gatti rattan chair by alban - CC BY 4.0 - https://sketchfab.com/3d-models/8d2fb8b8215f41fba59d0ec4606fb6b3','Bentwood chair by ValeGoG - CC BY 4.0 - https://sketchfab.com/3d-models/e76cb526238a493c90255ad4a900f83d','Commercial Coffee Machine by M.Reslan - CC BY 4.0 - https://sketchfab.com/3d-models/bab05bc7805d4b0dbc2cdcbb48441956','Commercial Refrigerator by Sean Thomas (foon), Khronos contributors - CC BY 4.0 - https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/CommercialRefrigerator','POS Terminal by Vladyslav.Yarovyi - CC BY 4.0 - https://sketchfab.com/3d-models/91c277a25bc34c5bbd080cd1e74161cb','PolyHaven croissant and hamburger_buns - CC0 1.0 - https://polyhaven.com/license','Kenney Food Kit - CC0 1.0 - https://kenney.nl/assets/food-kit','CC BY 4.0: https://creativecommons.org/licenses/by/4.0/']
licensefile=S/'THIRD-PARTY-CREDITS.txt';licensefile.write_text('\n'.join(credits));packet['source_imports'].append({'source':str(licensefile.resolve()),'destination':'THIRD-PARTY-CREDITS.txt','sha256':sha(licensefile)})
(P/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
text=(P.parent/'StoreBuilderR25-v4-SOURCE-MATERIALS.template.cs').read_text()
for a,b in {'__STORE_KEY__':'hawaiian','__WORKER_THREAD__':'classic:/root/r25_hawaiian','__EXACT_SCOPE__':'C180_R25_HAWAIIAN_APPLY_001','__PACKET_PATH__':str((P/'STORE-PACKET-R25.json').resolve()),'__PACKET_SHA256__':sha(P/'STORE-PACKET-R25.json')}.items():text=text.replace(a,b)
(P/'ApplyStoreR25.cs').write_text(text)
print(json.dumps({'objects':len(objects),'kind_counts':dict(collections.Counter(x['kind'] for x in objects)),'imports':len(imports),'materials':len(mats),'brand_source_polygons':lib['brand_outline']['source_polygons'],'packet_sha256':sha(P/'STORE-PACKET-R25.json'),'source_mesh_libraries':{k:len(v['vertices']) for k,v in lib.items()}},indent=2))
