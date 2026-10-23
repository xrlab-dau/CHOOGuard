from pathlib import Path
import json,math,hashlib,sys,datetime
import cv2,numpy as np
from shapely.geometry import Polygon,box
sys.path.insert(0,str(Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25').resolve()))
from photo_outline_to_solid import closed_solid_from_mask
R=Path('/Users/um-yunsang/CHOOGuard');B=Path(__file__).parent.resolve();S=B.parent/'sources';PD=B/'prepared';PD.mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
A='Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/PublicLounge'
p={'store_key':'public-lounge','origin_q':[38,-37.5],'floor_world_y':7,'source_packet':str((B/'SOURCE-EVIDENCE-R26.json').relative_to(R)),'materials':{},'objects':[],'mesh_library':{},'source_imports':[]}
materials={'black':([.022,.024,.026,1],.04,.33),'leather':([.09,.093,.096,1],.02,.42),'leather-gray':([.28,.285,.28,1],.01,.40),'seam':([.14,.145,.14,1],0,.35),'white':([.88,.87,.83,1],.04,.42),'counter-white':([.80,.78,.72,1],.02,.48),'silver':([.50,.53,.54,1],.9,.60),'warm-wood':([.54,.36,.17,1],0,.3),'blueplate':([.04,.065,.095,1],.2,.4),'iceglass':([.35,.62,.67,.20],.0,.7),'straw-white':([.88,.91,.86,1],0,.25),'packet-green':([.16,.40,.26,1],0,.3),'pastry':([.68,.43,.17,1],0,.2),'blue-water':([.45,.68,.78,.55],0,.5),'letter':([.90,.95,1,1],0,.4),'ink':([.025,.055,.085,1],0,.32),'kiosk-cyan':([.13,.45,.55,1],0,.4)}
for k,(c,m,s) in materials.items():p['materials'][k]={'color':c,'metallic':m,'smoothness':s}
for k in ['iceglass','blue-water']:p['materials'][k]['transparent']=True
p['materials']['letter']['emission']=[.35,.42,.47,1]

def add(n,k,x,z,y,sz=None,mat='black',g='Equipment',yaw=0,**kw):
 a={'id':n,'kind':k,'q':[x,z],'y':y,'material':mat,'group':g,'yaw':yaw,'source_ids':['2026_BUSAN_KORAIL_LOUNGE_SOURCE'],'qualification':'SOURCE_VISIBLE_PHOTO_PROPORTIONED_MODEL_INSIDE_R24_CELL0_NOT_SURVEY'}
 if sz is not None:a['size']=sz
 a.update(kw);p['objects'].append(a);return a

def rounded_mesh(name,w,h,d,r=.03,wrinkle=False):
 vertices=[];tri=[];uv=[];n=10 if wrinkle else 5;ext=np.array([w,h,d])/2;inner=ext-min(r,min(ext)*.85)
 for axis in range(3):
  for sign in [-1,1]:
   other=[i for i in range(3)if i!=axis];start=len(vertices)
   for i in range(n+1):
    for j in range(n+1):
     q=np.zeros(3);q[axis]=sign*ext[axis];q[other[0]]=(i/n*2-1)*ext[other[0]];q[other[1]]=(j/n*2-1)*ext[other[1]]
     c=np.clip(q,-inner,inner);v=q-c;norm=np.linalg.norm(v);q=c+v/max(norm,1e-10)*min(r,min(ext)*.85)
     if wrinkle and axis==1 and sign>0:
      xn=q[0]/ext[0];zn=q[2]/ext[2];q[1]+=.008*math.sin(22*xn+4*zn)*abs(xn)**2*(1-abs(zn))+.006*math.sin(19*zn-2*xn)*abs(zn)**2*(1-abs(xn))
     vertices.append(q.tolist());uv.append([i/n,j/n])
   for i in range(n):
    for j in range(n):
     a=start+i*(n+1)+j;b=a+n+1;c=b+1;d0=a+1
     seq=[a,b,c,a,c,d0];normal=np.cross(np.array(vertices[b])-vertices[a],np.array(vertices[c])-vertices[a]);
     if normal[axis]*sign<0:seq=[a,c,b,a,d0,c]
     tri.extend(seq)
 p['mesh_library'][name]={'vertices':vertices,'triangles':tri,'uv':uv}
rounded_mesh('Slipper_Chair_Seat_Cushion',.75,.16,.73,.045,True)
rounded_mesh('Slipper_Chair_Back',.75,.62,.13,.03)
rounded_mesh('Slipper_Chair_Lower_Box',.75,.32,.74,.022)
rounded_mesh('Loose_Pillow_With_Shallow_Photo_Creases',.66,.07,.64,.028,True)
rounded_mesh('Bench_Seat',.85,.15,.62,.035,True)
rounded_mesh('Bench_Back',.85,.48,.10,.03)

def lib(n,key,x,z,y,mat,g,yaw=0):return add(n,'library_mesh',x,z,y,[1,1,1],mat,g,yaw,mesh_key=key)
def cyl(n,x,z,y,r,h,mat='silver',g='Furniture',**kw):return add(n,'cylinder',x,z,y,[r*2,h/2,r*2],mat,g,**kw)
def imported(n,asset,x,z,y,fit,mat=None,g='Downloaded_Free_Assets',yaw=0):
 a=add(n,'asset',x,z,y,None,mat or'black',g,yaw,asset=asset,fit_size=fit)
 if mat is None:a.pop('material')
 return a
# Photo-visible front side black armless bench; actual Storyway-east partition behind stays fixed.
for i in range(4):
 z=-32.05-i*.86;x=42.84
 lib(f'Bench_Seat_{i}','Bench_Seat',x,z,.45,'leather','Bench_And_Side_Tables',90)
 lib(f'Bench_Back_{i}','Bench_Back',x+.31,z,.73,'leather','Bench_And_Side_Tables',90)
 add(f'Bench_Steel_Rail_{i}','box',x,z,.20,[.56,.055,.86],'silver','Bench_And_Side_Tables')
 for dz in [-.32,.32]:add(f'Bench_Leg_{i}_{dz}','box',x,z+dz,.115,[.045,.23,.045],'silver','Bench_And_Side_Tables')
for i,z in enumerate([-32.35,-34.25]):
 x=42.15;add(f'White_Narrow_Cantilever_Side_Table_{i}','box',x,z,.615,[.45,.035,.43],'white','Bench_And_Side_Tables')
 add(f'White_Side_Table_Upright_{i}','box',x-.16,z,.30,[.035,.60,.43],'white','Bench_And_Side_Tables')
 add(f'White_Side_Table_Base_{i}','box',x-.03,z,.022,[.48,.044,.43],'white','Bench_And_Side_Tables')
# Only four directly visible square window chairs, not an inferred 80-seat inventory.
for i,(x,z,yaw) in enumerate([(39.4,-43.25,0),(41.2,-43.25,0),(39.4,-41.2,180),(41.2,-41.2,180)]):
 g=f'Window_Lounge_Chair_{i}'
 lib(f'Chair_{i}_Lower','Slipper_Chair_Lower_Box',x,z,.23,'leather',g,yaw)
 lib(f'Chair_{i}_Seat','Slipper_Chair_Seat_Cushion',x,z,.45,'leather-gray',g,yaw)
 ang=math.radians(yaw);bx=x+math.sin(ang)*.32;bz=z+math.cos(ang)*.32
 lib(f'Chair_{i}_Back','Slipper_Chair_Back',bx,bz,.68,'leather',g,yaw)
 lib(f'Chair_{i}_Loose_Pillow','Loose_Pillow_With_Shallow_Photo_Creases',x,z,.555,'leather-gray',g,yaw)
 for dx in [-.29,.29]:
  for dz in [-.29,.29]:add(f'Chair_{i}_Foot_{dx}_{dz}','box',x+dx,z+dz,.04,[.04,.08,.04],'black',g)
for i,(x,z) in enumerate([(39.4,-42.22),(41.2,-42.22)]):
 g='White_Round_Lounge_Tables';cyl(f'Table_{i}_White_Top',x,z,.72,.37,.032,'white',g)
 cyl(f'Table_{i}_Steel_Post',x,z,.355,.027,.69,'silver',g)
 # Actual source has visible two-leg cross metal foot, reuse licensed narrow steel cafe-table base.
 imported(f'Downloaded_Licensed_Steel_Table_Base_{i}','Assets/ChooGuard/Art/StationInterior/R25_Shops/story-lamant/FreeModels/CafeTableSteelBase.fbx',x,z,0,[.58,.69,.58],'silver',g)
# Source white high side counter; use only source visible span, not a new partition.
x,z=35.55,-40.10
add('White_High_Side_Counter_Top','box',x,z,.93,[.48,.04,2.6],'white','High_Side_Counter')
for j,dz in enumerate([-.95,.95]):add(f'White_High_Counter_Panel_{j}','box',x,z+dz,.45,[.43,.90,.035],'white','High_Side_Counter')
# Counter front is parallel to existing party boundary. 1.1m clear equipment working strip at fixed wall.
CX,CZ=41.65,-38.20
add('Cafe_Counter_White_Worktop','box',CX,CZ,.95,[.82,.055,4.20],'counter-white','Cafe_Counter')
add('Cafe_Counter_Source_Wood_Front','box',CX-.36,CZ,.46,[.055,.87,4.20],'warm-wood','Cafe_Counter')
add('Counter_Source_Toekick','box',CX-.31,CZ,.065,[.08,.13,4.13],'black','Cafe_Counter')
for j,dz in enumerate([-.95,.95]):add(f'Counter_Separate_Cabinet_{j}','box',CX,CZ+dz,.46,[.74,.86,1.84],'counter-white','Cafe_Counter')
# Source curved black chilled pastry/water case; cabinet orientation yaw90 around local central group.
# Build in coordinates along counter z. Front viewing surface faces toward public centre (negative x).
x,z=CX,-36.95;W,D,H=1.50,.69,1.15
add('Chilled_Case_Lower_Refrigerator_Back','box',x+.31,z,1.20,[.05,.47,W],'black','Curved_Glass_Chilled_Case')
for side in [-1,1]:add(f'Chilled_Case_Lower_End_{side}','box',x,z+side*(W/2-.02),1.20,[D,.47,.04],'black','Curved_Glass_Chilled_Case')
for yy in [.98,1.43]:add(f'Chilled_Case_Lower_Frame_{yy}','box',x,z,yy,[D,.035,W],'black','Curved_Glass_Chilled_Case')
add('Chilled_Case_Lower_Real_Clear_Door','box',x-.347,z,1.205,[.008,.40,W-.08],'iceglass','Curved_Glass_Chilled_Case')
add('Chilled_Case_Lower_Door_Separate_Handle','box',x-.364,z-.56,1.205,[.024,.17,.025],'white','Curved_Glass_Chilled_Case')
for j in range(2):
 add(f'Chilled_Case_Separate_Shelf_{j}','box',x,z,1.48+j*.27,[.62,.018,1.43],'silver','Curved_Glass_Chilled_Case')
# Curved canopy follows actual source profile; closed thin glass shell from polygon quadrilateral strips.
vs=[];ts=[];steps=12
for j in range(steps+1):
 t=j/steps*math.pi/2;xx=-D/2+.28*(1-math.cos(t));yy=1.48+.55*math.sin(t)
 for zz in [-W/2,W/2]:vs.append([xx,yy,zz])
for j in range(steps):
 a=j*2;ts.extend([a,a+2,a+3,a,a+3,a+1])
# Give actual glass thickness and close edges.
n0=len(vs);vs+= [[a+.006,b,c] for a,b,c in vs]
for t in [ts[i:i+3] for i in range(0,len(ts),3)]:ts.extend([t[2]+n0,t[1]+n0,t[0]+n0])
for j in range(steps):
 for k in [0,1]:a=j*2+k;b=(j+1)*2+k;ts.extend([a,b,b+n0,a,b+n0,a+n0])
for a,b in [(0,1),(steps*2,steps*2+1)]:ts.extend([a,b,b+n0,a,b+n0,a+n0])
p['mesh_library']['Real_Curved_Chilled_Case_Glass']={'vertices':vs,'triangles':ts}
lib('Chilled_Case_Real_Curved_Glass','Real_Curved_Chilled_Case_Glass',x,z,0,'iceglass','Curved_Glass_Chilled_Case')
add('Chilled_Case_Upper_Flat_Glass_Top','box',x+.14,z,2.033,[.41,.006,W],'iceglass','Curved_Glass_Chilled_Case')
for side in [-1,1]:add(f'Chilled_Case_Rear_Sliding_Glass_{side}','box',x+.334,z+side*.36,1.765,[.007,.53,.715],'iceglass','Curved_Glass_Chilled_Case')
for yy in [1.48,2.034]:add(f'Chilled_Case_Rear_Track_{yy}','box',x+.334,z,yy,[.024,.02,W],'black','Curved_Glass_Chilled_Case')
for j,zz in enumerate([-W/2,W/2]):
 add(f'Case_End_{j}','box',x,z+zz,1.67,[.66,.70,.018],'iceglass','Curved_Glass_Chilled_Case')
 add(f'Case_End_Black_Front_Rail_{j}','box',x-.33,z+zz,1.64,[.018,.77,.024],'black','Curved_Glass_Chilled_Case')
# Source product rows. Free bakery mesh shape only, no invented SKU labels.
for i in range(4):
 bz=z-.52+i*.32;bx=x-.07
 imported(f'Downloaded_Visible_Round_Pastry_Inside_Source_Pouch_{i}','Assets/ChooGuard/Art/StationInterior/R25_Shops/bnc-east/FreeModels/hamburger_buns/hamburger_buns.fbx',bx,bz,1.50,[.14,.08,.12],None,'Chilled_Case_Visible_Foods',90)
 for side in [-1,1]:add(f'Paper_Pouch_{i}_Side_{side}','box',bx+side*.09,bz,1.62,[.008,.25,.17],'white','Chilled_Case_Visible_Foods',collider=False)
 add(f'Paper_Pouch_{i}_Back','box',bx+.06,bz,1.62,[.008,.25,.18],'white','Chilled_Case_Visible_Foods',collider=False)
 # Actual source white pouch with limited clear window, not a fictitious branded wrapper.
 for side in [-1,1]:add(f'Paper_Pouch_{i}_Window_Frame_{side}','box',bx-.091,bz+side*.065,1.62,[.008,.23,.035],'white','Chilled_Case_Visible_Foods',collider=False)
 add(f'Paper_Pouch_{i}_Clear_Window','box',bx-.093,bz,1.62,[.005,.17,.10],'iceglass','Chilled_Case_Visible_Foods',collider=False)
 add(f'Paper_Pouch_{i}_Folded_Top','box',bx,bz,1.752,[.18,.015,.17],'white','Chilled_Case_Visible_Foods',collider=False)
for r in range(3):
 for j in range(6):
  bx=x-.20+r*.17;bz=z-.57+j*.225
  cyl(f'Water_Bottle_{r}_{j}',bx,bz,1.14,.032,.19,'blue-water','Chilled_Case_Visible_Water')
  cyl(f'Water_Cap_{r}_{j}',bx,bz,1.245,.021,.025,'white','Chilled_Case_Visible_Water')
# Visible espresso, POS, trays, own visible wood textured-glass screen.
imported('Downloaded_Source_Proportioned_Commercial_Espresso','Assets/ChooGuard/Art/StationInterior/R25_Shops/pascucci/Models/CommercialCoffeeMachine.fbx',41.65,-39.13,.98,[.93,.54,.56],None,'Coffee_Equipment',90)
imported('Downloaded_POS_Receipt_Card_Equipment','Assets/ChooGuard/Art/StationInterior/R25_Shops/pascucci/Models/POS-Terminal.fbx',41.60,-38.12,.98,[.33,.37,.30],None,'Coffee_Equipment',-90)
for i in range(8):
 imported(f'Downloaded_Kenney_Stack_Cup_{i}','Assets/ChooGuard/Art/StationInterior/R25_Shops/pascucci/Models/mug.fbx',42.12+(i%2)*.11,-38.78-(i//2)*.13,1.50,[.085,.09,.08],'white','Coffee_Visible_Cups',90)
add('Visible_Back_Counter_Cabinet','box',42.82,-39.85,.485,[.60,.97,1.23],'white','Coffee_Equipment')
add('Visible_Back_Counter_Worktop','box',42.82,-39.85,.99,[.63,.04,1.28],'counter-white','Coffee_Equipment')
for j,z0 in enumerate([-39.55,-39.85,-40.15]):
 add(f'Visible_Blender_Jug_{j}','box',42.57,z0,1.39,[.15,.28,.15],'iceglass','Coffee_Equipment')
 add(f'Visible_Blender_Lid_{j}','box',42.57,z0,1.54,[.17,.025,.17],'black','Coffee_Equipment')
 add(f'Visible_Blender_Base_{j}','box',42.57,z0,1.12,[.19,.22,.20],'white','Coffee_Equipment')
 add(f'Visible_Blender_Handle_{j}','box',42.68,z0,1.38,[.03,.18,.03],'black','Coffee_Equipment')
add('Source_Wood_Separator_Lower','box',41.68,-40.07,.50,[.075,1.00,1.15],'warm-wood','Counter_Equipment_Screen')
add('Source_Textured_Glass_Separator','box',41.68,-40.07,1.31,[.026,.48,1.02],'iceglass','Counter_Equipment_Screen')
for j,z0 in enumerate([-40.61,-39.53]):add(f'Screen_Wood_Post_{j}','box',41.68,z0,1.30,[.074,.60,.074],'warm-wood','Counter_Equipment_Screen')
add('Screen_Wood_Header','box',41.68,-40.07,1.565,[.074,.07,1.15],'warm-wood','Counter_Equipment_Screen')
# Source black straw/sugar bins and actual physical contents.
for i in range(4):
 z=-35.75-i*.18;x=41.66
 for s in [-1,1]:add(f'Utensil_Bin_{i}_Side_{s}','box',x+s*.075,z,1.055,[.015,.19,.16],'black','Self_Corner')
 add(f'Utensil_Bin_{i}_Base','box',x,z,.974,[.15,.02,.16],'black','Self_Corner')
 add(f'Utensil_Bin_{i}_Rear','box',x+.005,z+.07,1.055,[.15,.19,.015],'black','Self_Corner')
 for j in range(8):
  if i<2: add(f'Wrapped_Straw_{i}_{j}','box',x-.045+(j%3)*.04,z-.04+(j//3)*.035,1.14,[.019,.26,.018],'straw-white','Self_Corner',euler=[(j%3-1)*4,0,6],collider=False)
  else:add(f'Sugar_Packet_{i}_{j}','box',x-.04+(j%2)*.06,z-.05+(j//2)*.028,1.055,[.05,.14,.005],'straw-white','Self_Corner',collider=False)
for i in range(5):add(f'Black_Service_Tray_{i}','box',41.66,-35.17,.98+i*.012,[.30,.012,.43],'black','Self_Corner')
# Charging panel visible at source window-side furniture, kept independent from actual columns.
for i,(x,z) in enumerate([(38.55,-43.8),(40.35,-43.8)]):
 add(f'Charging_Service_Pedestal_{i}','box',x,z,.34,[.20,.68,.17],'silver','Charging_Equipment')
 for j in [-1,1]:
  cyl(f'Charging_Round_Recess_{i}_{j}',x+j*.045,z+.09,.43,.036,.012,'black','Charging_Equipment',euler=[90,0,0])
  for dx in [-.012,.012]:add(f'Charging_Pin_Opening_{i}_{j}_{dx}','box',x+j*.045+dx,z+.098,.43,[.006,.009,.006],'black','Charging_Equipment',collider=False)
# Physical print/sign source contour helpers; no font/rendered label substitutes.
def source_plate(name,photo,quad,wh,size,pos,threshold=185,white=False,yaw=0,board=True,ink='ink'):
 im=cv2.imread(str(S/photo));src=np.array(quad,np.float32);dst=np.array([[0,0],[wh[0]-1,0],[wh[0]-1,wh[1]-1],[0,wh[1]-1]],np.float32);im=cv2.warpPerspective(im,cv2.getPerspectiveTransform(src,dst),wh)
 gray=cv2.cvtColor(im,cv2.COLOR_BGR2GRAY);mask=np.uint8(gray>threshold if white else gray<threshold)*255
 if white:
  hsv=cv2.cvtColor(im,cv2.COLOR_BGR2HSV);mask=np.uint8((gray>threshold)&(hsv[:,:,1]<100))*255
 cv2.imwrite(str(PD/(name+'-rectified.jpg')),im);cv2.imwrite(str(PD/(name+'-actual-source-mask.png')),mask)
 m=closed_solid_from_mask(mask,size[0],size[1],.018,minimum_area=8,epsilon=.38)
 p['mesh_library'][name+'_ActualSource_ClosedContour']=m
 x,z,y=pos
 if board:add(name+'_Source_Backing','box',x,z,y,[size[0]+.10,size[1]+.08,.028],'blueplate' if white else'white','Physical_Source_Signs',yaw)
 aa=math.radians(yaw);lib(name+'_ActualSource_ClosedContour',name+'_ActualSource_ClosedContour',x+math.sin(aa)*.022,z+math.cos(aa)*.022,y,'letter' if white else ink,'Physical_Source_Signs',yaw)
 return im
source_plate('KORAIL_LOUNGE_BUSAN','dail2yeon/FIELD-03.jpg',[[117,120],[576,130],[576,173],[117,163]],(1500,150),(5.70,.47),(38.40,-31.17,3.10),185,True)
source_plate('BUSAN_Small_Actual_Source','dail2yeon/FIELD-03.jpg',[[586,142],[650,145],[650,163],[586,160]],(400,110),(.62,.17),(35.12,-31.15,3.02),85,True,board=False)
source_plate('CAFE_STORYWAY','dail2yeon/FIELD-11.jpg',[[55,83],[881,298],[876,423],[53,225]],(1500,260),(2.10,.36),(35.35,-31.10,2.48),210,True)
# Physical menu/order/help notices modeled from actual source pixels; contemporary source epoch retained.
source_plate('Order_Here_Actual_Korean','kgs6897/FIELD-00.jpg',[[77,12],[482,107],[478,311],[89,222]],(800,340),(.47,.20),(39.9,-31.60,1.91),135)
source_plate('Self_Corner_Actual_Korean','kgs6897/FIELD-06.jpg',[[0,0],[442,26],[455,409],[0,332]],(800,500),(.35,.22),(41.67,-35.6,1.39),130,yaw=-90)
def screen_solid(n,x,z,y,w,h,material,g,yaw=0):
 vertices=[[w/2,h/2,.002],[-w/2,h/2,.002],[-w/2,-h/2,.002],[w/2,-h/2,.002],[w/2,h/2,-.002],[-w/2,h/2,-.002],[-w/2,-h/2,-.002],[w/2,-h/2,-.002]]
 tris=[0,1,2,0,2,3,6,5,4,7,6,4,0,4,5,0,5,1,1,5,6,1,6,2,2,6,7,2,7,3,3,7,4,3,4,0]
 return add(n,'mesh',x,z,y,[1,1,1],material,g,yaw,vertices=vertices,triangles=tris,uv=[[0,1],[1,1],[1,0],[0,0]]+[[0,0]]*4,collider=False)
# Actual front menu stand: source full-front image carries the full board; no font is substituted.
source_plate('Full_Menu_Front_Source_Ink','dail2yeon/FIELD-03.jpg',[[141,339],[181,340],[183,519],[140,520]],(250,1200),(.43,1.45),(40.52,-31.66,1.06),137)
for side in [-1,1]:add(f'Menu_Stand_{side}_Tube','box',40.52+side*.25,-31.67,.93,[.025,1.80,.035],'black','Front_Menu_Stand')
add('Menu_Stand_Actual_Base','box',40.52,-31.65,.022,[.60,.044,.43],'black','Front_Menu_Stand')

# Black freestanding touch kiosk is equipment, UI picture is allowed and distinct from physical signs.
KX,KZ=39.90,-31.65
add('Kiosk_Floor_Plate','box',KX,KZ,.027,[.48,.054,.48],'black','Kiosk')
add('Kiosk_Pedestal','box',KX,KZ,.55,[.20,1.05,.20],'black','Kiosk')
add('Kiosk_Separate_Enclosure','box',KX,KZ,1.31,[.47,1.14,.16],'black','Kiosk')
add('Kiosk_Touch_Bezel_Recess','box',KX,KZ+.086,1.48,[.418,.68,.012],'black','Kiosk')
# Source actual screen UI of current2026 kiosk; physical notice above is independently meshed.
im=cv2.imread(str(S/'kgs6897/FIELD-00.jpg'));q=np.array([[92,285],[479,345],[462,1002],[72,865]],np.float32);ui=cv2.warpPerspective(im,cv2.getPerspectiveTransform(q,np.array([[0,0],[399,0],[399,679],[0,679]],np.float32)),(400,680));cv2.imwrite(str(PD/'Kiosk_202610_ScreenUI.jpg'),ui)
uiasset=A+'/Screens/Kiosk_202610_ScreenUI.jpg';p['materials']['kiosk-screen-ui']={'color':[1,1,1,1],'texture_asset':uiasset,'smoothness':.45};p['source_imports'].append({'source':str(PD/'Kiosk_202610_ScreenUI.jpg'),'destination':'Screens/Kiosk_202610_ScreenUI.jpg','sha256':sha(PD/'Kiosk_202610_ScreenUI.jpg')})
screen_solid('Kiosk_Equipment_TouchScreen_SourceEpoch_UI',KX,KZ+.096,1.45,.40,.68,'kiosk-screen-ui','Kiosk')
add('Kiosk_Receipt_Well','box',KX-.12,KZ+.094,1.04,[.15,.035,.016],'black','Kiosk')
add('Kiosk_Card_Terminal','box',KX+.10,KZ+.12,.94,[.16,.14,.07],'silver','Kiosk',euler=[20,0,0])
add('Kiosk_QR_Scanner_Lens','box',KX+.11,KZ+.16,1.12,[.09,.07,.014],'kiosk-cyan','Kiosk')
# Departure monitor physical enclosure; source epoch image remains equipment UI, never a physical sign substitute.
TX,TZ=43.30,-35.20
add('Monitor_Source_Wall_Mount_Bracket','box',43.3616,-35.20,1.90,[.0252,.35,.45],'black','Departure_Display')
add('Departure_Information_Monitor_Enclosure','box',TX,TZ,1.90,[.10,.83,1.40],'black','Departure_Display')
im=cv2.imread(str(S/'dail2yeon/FIELD-08.jpg'));quad=np.array([[142,590],[719,558],[725,919],[144,933]],np.float32);ui=cv2.warpPerspective(im,cv2.getPerspectiveTransform(quad,np.array([[0,0],[959,0],[959,539],[0,539]],np.float32)),(960,540));cv2.imwrite(str(PD/'Departures_SourceEpoch_ScreenUI.jpg'),ui)
p['source_imports'].append({'source':str(PD/'Departures_SourceEpoch_ScreenUI.jpg'),'destination':'Screens/Departures_SourceEpoch_ScreenUI.jpg','sha256':sha(PD/'Departures_SourceEpoch_ScreenUI.jpg')});p['materials']['departure-screen-ui']={'color':[1,1,1,1],'texture_asset':A+'/Screens/Departures_SourceEpoch_ScreenUI.jpg','smoothness':.5}
screen_solid('Departure_Monitor_Equipment_UI',TX-.057,TZ,1.90,1.21,.68,'departure-screen-ui','Departure_Display',yaw=-90)
# Pendant meshes only, no Light components; actual source hardware visible over bench.
# Half-dome shade is an open shell with real thickness, not an opaque sphere.
v=[];t=[];N=28;RINGS=8
for j in range(RINGS+1):
 th=(j/RINGS)*math.pi/2
 for i in range(N):
  ph=2*math.pi*i/N;v.append([.185*math.sin(th)*math.cos(ph),.185*math.cos(th),.185*math.sin(th)*math.sin(ph)])
for j in range(RINGS):
 for i in range(N):a=j*N+i;b=j*N+(i+1)%N;c=(j+1)*N+(i+1)%N;d=(j+1)*N+i;t.extend([a,b,c,a,c,d])
outerlen=len(v);outertris=t[:]
v += [[a*.975,b*.975,c*.975]for a,b,c in v]
for j in range(0,len(outertris),3):a,b,c=outertris[j:j+3];t.extend([c+outerlen,b+outerlen,a+outerlen])
for j in range(N):a=RINGS*N+j;b=RINGS*N+(j+1)%N;t.extend([a,b,b+outerlen,a,b+outerlen,a+outerlen])
p['mesh_library']['Source_Actual_Pendant_HalfDome']={'vertices':v,'triangles':t}
for i,z in enumerate([-32.40,-34.2]):
 lib(f'Pendant_{i}_Physical_Shade','Source_Actual_Pendant_HalfDome',42.35,z,2.38,'silver','Physical_Fixture_Hardware')
 cyl(f'Pendant_{i}_Hanging_Cord',42.35,z,2.84,.003,.55,'black','Physical_Fixture_Hardware')
 add(f'Pendant_{i}_Inner_Physical_Bowl','sphere',42.35,z,2.35,[.17,.13,.17],'white','Physical_Fixture_Hardware',collider=False)
# Footprints obey exact accepted work envelope, independent of source paper pixel scaling.
cell=Polygon(json.load(open(B.parent/'LATER-R24-BOUNDED-PUBLIC-LOUNGE-AUTHORING-BASIS-R26.json'))['working_envelope_qx_minus_qy']['coordinates'][0])
# Position eligibility is a construction boundary, not Scene validation.
for o in p['objects']:
 if not cell.contains(__import__('shapely').geometry.Point(o['q'])):raise ValueError('Own part outside approved cell '+o['id'])
(B/'PUBLIC-LOUNGE-PACKET-R26.json').write_text(json.dumps(p,separators=(',',':'),ensure_ascii=False))
viewed=['dail2yeon/FIELD-03.jpg','dail2yeon/FIELD-06.jpg','dail2yeon/FIELD-07.jpg','dail2yeon/FIELD-08.jpg','dail2yeon/FIELD-09.jpg','dail2yeon/FIELD-11.jpg','kgs6897/FIELD-00.jpg','kgs6897/FIELD-01.jpg','kgs6897/FIELD-03.jpg','kgs6897/FIELD-04.jpg','kgs6897/FIELD-05.jpg','kgs6897/FIELD-06.jpg','suneunjaeyongin/FIELD-04.jpg']
e={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'source_epoch':'2026-August and October actual Busan KORAIL LOUNGE CAFE STORYWAY','full_source_images_directly_viewed':[{'path':str((S/x).relative_to(R)),'sha256':sha(S/x)}for x in viewed],'primary_publications':['https://m.blog.naver.com/dail2yeon/224386553651','https://m.blog.naver.com/kgs6897/224434236149','https://m.blog.naver.com/suneunjaeyongin/224378103442'],'retained_license_references':['.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/pascucci/SOURCE-EVIDENCE-R25.json','.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/bnc-east/SOURCE-EVIDENCE-R25.json','.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/story-lamant/sources/cafe-table/ACQUISITION.json'],'downloaded_asset_references':[{'asset':o['asset'],'adaption':'Source-proportioned fit, exact field SKU not asserted'}for o in p['objects']if o['kind']=='asset'],'equipment_UI':'Only two current-source equipment screen faces use UI image textures. All physical signs are closed source-contour solids, with counters. No font/Text/TMP/photo-only physical sign planes.','dimension_and_position_qualification':'Bounded R24 cell0 source-topology MODEL, no measured pier identity or absolute in-cell field furniture coordinates. Existing geometry untouched.','source_visible_only':'4 slipper chairs,2 round tables,4-segment bench,2 side tables,1 limited side counter,source cafe service kit; no inferred hidden kitchen or eighty-seat plan.','all2F_complete':False}
(B/'SOURCE-EVIDENCE-R26.json').write_text(json.dumps(e,ensure_ascii=False,indent=2))
print(json.dumps({'objects':len(p['objects']),'mesh_library':len(p['mesh_library']),'packet_sha256':sha(B/'PUBLIC-LOUNGE-PACKET-R26.json')}))
