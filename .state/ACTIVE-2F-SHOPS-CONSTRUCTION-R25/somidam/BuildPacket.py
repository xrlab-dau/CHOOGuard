import json,math,hashlib,shutil
from pathlib import Path
import numpy as np
from PIL import Image
P=Path(__file__).parent.resolve();ROOT=Path('/Users/um-yunsang/CHOOGuard');S=P/'sources';AS='Assets/ChooGuard/Art/StationInterior/R25_Shops/somidam'
b=json.loads((P/'MODEL-PLACEMENT-BASIS-R25.json').read_text());base=np.array(b['base_q']);u=np.array(b['u']);v=np.array(b['v']);x0,x1=b['u_limits'];z0,z1=b['v_limits'];W=x1-x0;D=z1-z0;org=base+x0*u+z0*v;angle=math.degrees(math.atan2(-u[1],u[0]));objs=[];mats={};imports=[];lib={}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def imp(src,rel):
 src=Path(src).resolve();imports.append({'source':str(src),'destination':rel,'sha256':sha(src)});return AS+'/'+rel
# Own imported actual-source model derivatives, protected originals retained.
case=imp(S/'case/Cake-Display.fbx','Models/Cake-Display.fbx');coffee=imp(S/'coffee/Coffee-Machine.fbx','Models/Coffee-Machine.fbx');chair=imp(S/'0a45955e2328459d95f403cb00c08b3a/Dining-Chair.fbx','Models/Dining-Chair.fbx')
for dest,source in [('case','case'),('coffee','coffee')]:
 for f in sorted((S/source/'textures').glob('*.png')):imp(f,f'Textures/{dest}/{f.name}')
# Normal texture names expressly identify import type; ORM converted from source channels.
for dest,num in [('coffee',3),('case',2)]:imp(S/dest/f'textures/Image_{num}.png',f'Textures/{dest}/surface_normal.png')
for part in ['coffee','case']:
 im=np.array(Image.open(S/part/'textures/Image_1.png').convert('RGB'));out=np.zeros((im.shape[0],im.shape[1],4),dtype=np.uint8);out[:,:,:3]=im[:,:,2:3];out[:,:,3]=255-im[:,:,1];ip=S/part/'textures/metallic_smoothness.png';Image.fromarray(out).save(ip);imp(ip,f'Textures/{part}/metallic_smoothness.png')
chairtex=imp(S/'0a45955e2328459d95f403cb00c08b3a/textures/Image_0.png','Textures/chair/BaseColor.png')
posroot=P.parent/'free-assets/pos-terminal';pos=imp(posroot/'POS-Terminal.fbx','Models/POS-Terminal.fbx');poscol=imp(posroot/'textures/Image_0.png','Textures/pos/BaseColor.png');posnor=imp(posroot/'textures/Image_2.png','Textures/pos/terminal_normal.png')
frroot=P.parent/'free-assets/commercial-refrigerator';fridge=imp(frroot/'CommercialRefrigerator-Empty.fbx','Models/CommercialRefrigerator-Empty.fbx')
# Runtime attribution files copied only under active author lease by source_imports.
credits='''Somidam R25 independent photo-adapted source models\nCanarian Cafe - cake display by Lanzaman (lanzaboy), CC-BY4.0. https://sketchfab.com/3d-models/867570f94d654385bb9750248849892b\nCommercial Coffee Machine by '''+json.loads((S/'coffee-author-license.json').read_text())['user']['displayName']+''' (Sketchfab bab05bc7805d4b0dbc2cdcbb48441956), CC-BY4.0.\nVitra Eames Plastic Chair by furnny (Sketchfab0a45955e2328459d95f403cb00c08b3a), CC-BY4.0. Beige color/photo fit adaptation.\nPOS terminal by Vladyslav.Yarovyi, CC-BY4.0. https://sketchfab.com/3d-models/91c277a25bc34c5bbd080cd1e74161cb\nCommercial Fridge by Sean Thomas (foon), Khronos adapted empty derivative, CC-BY4.0. https://sketchfab.com/3d-models/2174e1e4f1f24f1a95aa110ee060f473\nPolyHaven modern_arm_chair_01 and wicker_basket_01 CC0 read-only existing free-source assets; independent instances.\nKenney Food Kit CC0 https://kenney.nl/assets/food-kit\nPhoto-derived Somidam branding/menus remain respective copyright/trademark of source owners. Photographs used for source contour derivation and genuine menu surface information, never a whole-sign or equipment stand-in.\nChanges: fitted source assets to branch-photo MODEL proportions; glass/stainless URP adaptation; nonbranch generic price stands/doilies removed from own bakery-case derivative. Original GLBs retained.\nCC-BY4.0 https://creativecommons.org/licenses/by/4.0/\n'''
(S/'ATTRIBUTION-R25.txt').write_text(credits);imp(S/'ATTRIBUTION-R25.txt','ATTRIBUTION-R25.txt')
def mat(n,c,**kw):mats[n]={'color':c,**kw}
mat('black',[.015,.018,.02,1],metallic=.15,smoothness=.45);mat('wall',[.88,.85,.78,1],smoothness=.23);mat('ceiling',[.95,.94,.89,1],smoothness=.2);mat('wood',[.53,.36,.20,1],smoothness=.27);mat('lightwood',[.72,.56,.34,1],smoothness=.32);mat('stone',[.87,.85,.78,1],smoothness=.45);mat('steel',[.64,.67,.68,1],metallic=.88,smoothness=.58);mat('darksteel',[.15,.17,.17,1],metallic=.72,smoothness=.6);mat('glass',[.84,.95,.96,.13],transparent=True,smoothness=.94);mat('packagefilm',[1,.98,.88,.12],transparent=True,smoothness=.85);mat('packetback',[.97,.91,.67,1],smoothness=.2);mat('beige',[.67,.59,.43,1],smoothness=.28);mat('fabric',[.46,.46,.44,1],smoothness=.2);mat('blind',[.35,.34,.31,1],smoothness=.16);mat('warmglow',[1,.90,.66,1],emission=[2.3,1.9,1.3,1],smoothness=.4);mat('signwhite',[.98,.99,1,1],emission=[3.5,3.7,4,1],smoothness=.6);mat('red',[.57,.11,.08,1]);mat('pink',[.72,.39,.49,1]);mat('bluecloth',[.44,.66,.72,1]);mat('purplecloth',[.46,.31,.50,1]);mat('white',[.94,.93,.88,1],smoothness=.4)
mat('coffeePBR',[1,1,1,1],texture_asset=AS+'/Textures/coffee/Image_0.png',normal_asset=AS+'/Textures/coffee/surface_normal.png',metallic_gloss_asset=AS+'/Textures/coffee/metallic_smoothness.png',smoothness=.6)
mat('chairPBR',[.78,.68,.51,1],texture_asset=chairtex,smoothness=.3)
mat('posPBR',[1,1,1,1],texture_asset=poscol,normal_asset=posnor,metallic=.15,smoothness=.48)
for i,c in enumerate([[.76,.61,.33,1],[.28,.40,.21,1],[.32,.22,.15,1],[.67,.38,.46,1],[.74,.67,.49,1],[.90,.87,.73,1]]):mat('rice'+str(i),c,smoothness=.08)
for name in ['coffee','noncoffee','tea','dessert']:
 tex=imp(S/f'menu-{name}-source.png',f'Prints/menu-{name}-source.png');mat('menu_'+name,[1,1,1,1],texture_asset=tex,emission=[.25,.25,.25,1],smoothness=.7)
def q(x,z):return (org+x*u+z*v).tolist()
def add(id,kind,x,z,y,material=None,size=None,yaw=0,group='Interior',source=['L2026-003','L2026-004','L2026-005'],**kw):
 ob={'id':id,'kind':kind,'q':q(x,z),'y':y,'yaw':angle+yaw,'group':group,'source_ids':source,'qualification':'PHOTO_REFERENCED_MODEL_DIMENSIONS_NOT_SURVEYED'}
 if material:ob['material']=material
 if size:ob['size']=size
 ob.update(kw);objs.append(ob);return ob

def box(id,x,z,y,size,ma='wood',**kw):return add(id,'box',x,z,y,ma,size,**kw)
def cyl(id,x,z,y,diam,h,ma,**kw):return add(id,'cylinder',x,z,y,ma,[diam,h/2,diam],**kw)
def sphere(id,x,z,y,size,ma,**kw):return add(id,'sphere',x,z,y,ma,size,**kw)
def edge(id,a,bb,y,h,t,ma,group='Interior',**kw):
 ob=add(id,'edge',0,0,0,ma,group=group,**kw);ob.update(a=q(*a),b=q(*bb),bottom=y,height=h,thickness=t);return ob
# Front six-bay glazing with central two-bay public opening. Only own booth parts.
bay=W/6
box('FrontHeader_Black_Fascia',W/2,.075,2.96,[W-.08,.62,.14],'black',group='Storefront')
for i in [0,1,2,4,5,6]:box(f'Front_Mullion_{i}',i*bay+.028 if i==0 else i*bay-.028 if i==6 else i*bay,.095,1.34,[.052,2.68,.085],'black',group='Storefront')
for i in [0,1,4,5]:
 x=(i+.5)*bay;box(f'GlazedFixedBay_{i}',x,.10,1.34,[bay-.065,2.61,.018],'glass',group='Storefront',collider=True)
 box(f'FrontSill_{i}',x,.10,.055,[bay-.05,.075,.11],'black',group='Storefront')
 for yy in [.94,1.0,1.06]:box(f'FrostedStripe_{i}_{yy}',x,.085,yy,[bay-.13,.015,.0018],'white',group='Storefront',collider=False)
# Paired glass leaves parked alongside adjacent fixed bays: source central two-panel access stays open.
for i,x in enumerate([1.65*bay,4.35*bay]):
 box(f'ParkedSlidingDoorGlass_{i}',x,.17,1.33,[bay-.08,2.58,.018],'glass',group='Storefront');box(f'ParkedLeafRail_{i}',x,.17,2.63,[bay,.045,.08],'black',group='Storefront')
 cyl(f'DoorVerticalPull_{i}',x+(-.36 if i==0 else .36),.13,1.12,.022,.39,'steel',group='Storefront')
box('CentralDoorFloorTrack',W/2,.13,.014,[2*bay,.018,.08],'steel',group='Storefront')
# Native rear shell/window bodies are reused. Independent new interior side finishes only.
box('West_InnerFinish',.035,D/2,1.53,[.06,3.06,D-.04],'wall',group='Shell')
box('East_InnerFinish',W-.035,D/2,1.53,[.06,3.06,D-.04],'wall',group='Shell')
box('IndependentShopCeiling',W/2,D/2,3.08,[W-.06,.085,D-.04],'ceiling',group='Ceiling')
for x in [.19,W-.19]:box('CoveHousing_'+str(x),x,D/2,2.97,[.12,.17,D-.25],'wall',group='Ceiling');box('CoveLED_'+str(x),x,D/2,3.001,[.05,.018,D-.3],'warmglow',group='Ceiling',collider=False)
box('RearCoveHousing',W/2,D-.12,2.96,[W-.2,.15,.12],'wall',group='Ceiling');box('RearCoveLED',W/2,D-.17,3.0,[W-.3,.018,.028],'warmglow',group='Ceiling',collider=False)
# Photo-observed HVAC cassettes and downlight rows.
for j,z in enumerate([1.15,3.25,5.25]):
 box(f'CeilingCassette_{j}',3.55,z,3.025,[.74,.075,.74],'white',group='Ceiling')
 box(f'CassetteCenter_{j}',3.55,z,2.98,[.52,.012,.52],'blind',group='Ceiling')
 for k in [-1,1]:box(f'CassetteFin_{j}_{k}',3.55+k*.31,z,2.98,[.04,.012,.56],'black',group='Ceiling')
for row,x in enumerate([.9,2.35,4.7,6.25]):
 for j,z in enumerate(np.linspace(.5,D-.5,9)):
  cyl(f'DownlightTrim_{row}_{j}',x,float(z),3.008,.10,.018,'white',group='Ceiling',collider=False);cyl(f'DownlightEmitter_{row}_{j}',x,float(z),2.995,.071,.009,'warmglow',group='Ceiling',collider=False)
# Three rear roller blinds and suspended globe lights; existing rear native glazing remains untouched.
for i in range(3):
 x=(i+.5)*W/3;box(f'RearBlindFabric_{i}',x,D-.11,2.18,[W/3-.085,1.2,.018],'blind',group='RearWindow')
 box(f'RearBlindHem_{i}',x,D-.125,1.57,[W/3-.06,.026,.03],'steel',group='RearWindow');box(f'RearBlindTube_{i}',x,D-.105,2.81,[W/3-.05,.055,.045],'white',group='RearWindow')
 cyl(f'PendantCable_{i}',x,D-.62,2.68,.012,.66,'black',group='Lighting',collider=False);sphere(f'OpalGlobe_{i}',x,D-.62,2.25,[.22,.22,.22],'warmglow',group='Lighting',collider=False)
 add(f'RearActualLight_{i}','light',x,D-.62,2.18,color=[1,.88,.68,1],range=3.5,intensity=.7,group='Lighting',source=['L2026-005'])
# L-shaped sales/prep area on west; wood front and thin stone worktops.
box('PrepBackCabinet',.44,3.07,.48,[.74,3.7,.90],'wood',group='Prep');box('PrepBackWorktop',.47,3.07,.96,[.82,3.78,.05],'stone',group='Prep')
# Correct sizes are x/y/z: custom source counters extend in depth.
objs[-2]['size']=[.74,.90,3.7];objs[-1]['size']=[.82,.055,3.78]
box('ServiceCounter_Run',1.78,3.13,.50,[.63,1.0,3.75],'lightwood',group='ServiceCounter');box('ServiceWorktop_Run',1.78,3.13,1.035,[.78,.065,3.8],'stone',group='ServiceCounter')
box('CashierReturn',1.28,1.32,.51,[1.55,1.02,.64],'lightwood',group='ServiceCounter');box('CashierStone',1.28,1.32,1.055,[1.62,.055,.72],'stone',group='ServiceCounter')
# Individual thin flutes on visible east counter face.
for i,z in enumerate(np.arange(1.28,4.99,.045)):box(f'CounterFlute_{i}',2.112,float(z),.51,[.018,.97,.016],'wood',group='ServiceCounter',collider=False)
for i,x in enumerate(np.arange(.55,2.01,.045)):box(f'CashierFlute_{i}',float(x),.987,.51,[.016,.97,.018],'wood',group='ServiceCounter',collider=False)
# Downloaded espresso/POS/refrigerator actual 3D meshes, fitted and source-adapted.
add('DownloadedCommercialEspresso','asset',.48,2.44,.99,asset=coffee,fit_size=[.90,.49,.60],material_map={'*':'coffeePBR'},yaw=90,group='PrepEquipment',source=['L2026-004','F-COFFEE'])
add('DownloadedModernPOSTerminal','asset',1.40,1.30,1.09,asset=pos,fit_size=[.40,.48,.34],material_map={'*':'posPBR'},yaw=180,group='SalesEquipment',source=['L2026-004','F-POS'])
add('DownloadedPrepFridge','asset',.54,4.83,.03,asset=fridge,fit_size=[.91,2.10,.72],material_map={'*':'white'},yaw=90,group='PrepEquipment',source=['L2026-004','F-FRIDGE'],qualification='PHOTO_VISIBLE_TALL_PREP_APPLIANCE_MODEL_GENERIC_FREE_SOURCE_NOT_MANUFACTURER_ID')
# Visible rear worktop and stainless sink/curved faucet, not invented hidden BOH.
box('PrepVisibleRearWorktop',1.35,4.95,.96,[1.4,.055,.58],'steel',group='Prep');box('SinkBowlInterior',1.45,4.92,.92,[.47,.055,.36],'darksteel',group='Prep')
for dx,dz,siz in [(0,-.21,[.51,.025,.035]),(0,.21,[.51,.025,.035]),(-.25,0,[.025,.025,.42]),(.25,0,[.025,.025,.42])]:box('SinkRim_'+str(dx)+str(dz),1.45+dx,4.92+dz,.99,siz,'steel',group='Prep')
cyl('FaucetStem',1.46,5.13,1.15,.025,.34,'steel',group='Prep');box('FaucetSpout',1.46,5.04,1.31,[.028,.026,.19],'steel',group='Prep')
# Two downloadable physical curved-glass rice display cabinets.
case_meta=json.loads((S/'case/MODEL-MATERIALS.json').read_text());cm={'*':'steel'}
for ob in case_meta['objects']:
 n=ob['name'];low=n.lower()
 if any(t in low for t in ['lh_front','rh_front','lh_side','rh_side','_shelf','fridge_back']):cm[n]='glass'
 if 'light' in low:cm[n]='warmglow'
for ix,(xx,zz,fit,yaw) in enumerate([(3.15,1.25,[1.72,1.46,.83],180),(.93,.61,[1.62,1.20,.76],180)]):
 add(f'DownloadedCurvedGlassRiceCase_{ix}','asset',xx,zz,.025,asset=case,fit_size=fit,material_map=cm,yaw=yaw,group='RiceDisplay',source=['L2026-003','L2026-008','L2026-010','L2026-011','F-CASE'])
 # Photo stainless vent grilles on lower base; individual narrow bands.
 for bank in [-1,1]:
  for j in range(8):box(f'CaseVent_{ix}_{bank}_{j}',xx+bank*fit[0]*.25,zz-.425,.14+j*.026,[fit[0]*.41,.009,.012],'darksteel',group='RiceDisplay',collider=False)
# Case goods are proper 3D ricecake rounded solids, pale pouches and wicker trays.
# Direct modeled shape because free search returned unrelated noodles/waffles/ND assets, not this branch goods.
def lathe(name,profile,seg=28):
 vv=[];tt=[]
 for yy,r in profile:
  for k in range(seg):a=2*math.pi*k/seg;vv.append([r*math.cos(a),yy,r*math.sin(a)])
 for j in range(len(profile)-1):
  for k in range(seg):a=j*seg+k;b=j*seg+(k+1)%seg;c=(j+1)*seg+(k+1)%seg;d=(j+1)*seg+k;tt.extend([a,c,b,a,d,c])
 lib[name]={'vertices':vv,'triangles':tt}
lathe('ricecake',[(0,0),(.008,.033),(.018,.048),(.048,.052),(.067,.043),(.074,.023),(.076,0)],24)
# Pouches use closed creased mesh, no photograph rectangle standin.
lib['pouch']={'vertices':[[-.064,0,-.009],[.064,0,-.009],[.073,.13,-.007],[-.073,.13,-.007],[-.052,.005,.025],[.052,.005,.025],[.062,.127,.018],[-.062,.127,.018]],'triangles':[0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5]}
basket='Assets/ChooGuard/Art/StationInterior/FreeSourceAssetsC165_20261018/Prefabs/wicker_basket_01.prefab'
for ci,(cx,cz,ch) in enumerate([(3.15,1.25,1.46),(.93,.61,1.20)]):
 for shelf in range(3):
  yy=.62+shelf*(.25 if ci==0 else .19)
  for bank in [-1,1]:
   bx=cx+bank*.43;add(f'WickerCaseTray_{ci}_{shelf}_{bank}','asset',bx,cz,yy-.05,asset=basket,fit_size=[.74,.07,.49],collider=False,group='RiceMerchandise',source=['L2026-008','L2026-010','L2026-011','F-WICKER'])
   for row in range(2):
    for col in range(5):
     xx=bx+(col-2)*.125;zz=cz+(row-.5)*.18;flavor=(shelf*2+(bank==1)+ci)%6
     add(f'Ricecake_{ci}_{shelf}_{bank}_{row}_{col}','library_mesh',xx,zz,yy+.015,'rice'+str(flavor),mesh_key='ricecake',size=[.95,.85,.95],collider=False,group='RiceMerchandise',source=['L2026-008','L2026-010','L2026-011'])
     add(f'ClearPouch_{ci}_{shelf}_{bank}_{row}_{col}','library_mesh',xx,zz,yy+.005,'packagefilm',mesh_key='pouch',collider=False,group='RiceMerchandise',source=['L2026-008','L2026-010','L2026-011'])
     box(f'PalePacketBacking_{ci}_{shelf}_{bank}_{row}_{col}',xx,zz+.029,yy+.067,[.125,.126,.005],'packetback',group='RiceMerchandise',collider=False,source=['L2026-008','L2026-010','L2026-011'])
# Countertop warming cabinet from direct source009, physical glass front/racks/switch.
box('WarmingCabinetShell',.70,1.58,1.37,[.63,.59,.48],'steel',group='SalesEquipment',source=['L2026-009']);box('WarmingCabinetBlackDoor',.70,1.325,1.38,[.56,.51,.024],'black',group='SalesEquipment',source=['L2026-009']);box('WarmingCabinetGlass',.70,1.307,1.39,[.48,.42,.01],'glass',group='SalesEquipment',source=['L2026-009']);sphere('WarmingCabinetSwitch',.89,1.288,1.60,[.035,.035,.015],'red',group='SalesEquipment',source=['L2026-009']);box('WarmingCabinetPull',.94,1.281,1.38,[.025,.22,.033],'steel',group='SalesEquipment',source=['L2026-009'])
# Gift display actual visible shelving, boxes and tied cloth bows.
for y in [1.20,1.72,2.24]:box('WestGiftShelf_'+str(y),.13,1.70,y,[.26,.045,2.1],'lightwood',group='GiftDisplay',source=['L2026-007']);box('GiftShelfLED_'+str(y),.24,1.7,y-.02,[.014,.01,2.0],'warmglow',group='GiftDisplay',collider=False,source=['L2026-007'])
for j in range(8):
 zz=.86+j*.24;ma=['bluecloth','purplecloth','pink'][j%3];box(f'ClothWrappedGift_{j}',.23,zz,1.94,[.25,.30,.19],ma,group='GiftDisplay',source=['L2026-007'])
 for k in [-1,1]:sphere(f'GiftBowLoop_{j}_{k}',.377,zz+k*.045,1.97,[.04,.12,.08],ma,group='GiftDisplay',source=['L2026-007'],collider=False)
 sphere(f'GiftBowKnot_{j}',.393,zz,1.97,[.045,.045,.045],ma,group='GiftDisplay',source=['L2026-007'],collider=False)
# Photo fluted divider between order/service route and seats, with warm top strip.
box('SeatingDivider',2.66,4.13,.58,[.13,1.16,3.7],'lightwood',group='Seating');box('DividerStoneCap',2.66,4.13,1.19,[.20,.055,3.74],'stone',group='Seating');box('DividerWarmLED',2.735,4.13,1.17,[.008,.018,3.62],'warmglow',group='Seating',collider=False)
for i,z in enumerate(np.arange(2.3,5.96,.042)):box(f'DividerFlute_{i}',2.737,float(z),.57,[.013,1.10,.015],'wood',group='Seating',collider=False)
# Photo right wall long upholstered bench and square two-top tables.
box('EastBenchBase',W-.34,3.38,.22,[.62,.40,5.08],'wood',group='Seating');box('EastBenchSeat',W-.37,3.38,.45,[.67,.095,5.10],'beige',group='Seating');box('EastBenchBack',W-.11,3.38,.82,[.14,.68,5.12],'beige',group='Seating')
for i,z in enumerate([1.19,2.49,3.79,5.09]):
 box(f'SquareTwoTop_{i}',W-1.28,z,.75,[.67,.045,.63],'white',group='Furniture',source=['L2026-005']);cyl(f'TablePedestal_{i}',W-1.28,z,.38,.055,.70,'steel',group='Furniture');cyl(f'TableFoot_{i}',W-1.28,z,.035,.43,.035,'darksteel',group='Furniture')
 add(f'DownloadedBeigeShellChair_{i}','asset',W-2.02,z,.025,asset=chair,fit_size=[.49,.79,.49],material_map={'*':'chairPBR'},yaw=90,group='Furniture',source=['L2026-005','F-CHAIR'])
# Source grey upholstered wood armchairs and circular white pedestal tables.
arm='Assets/ChooGuard/Art/StationInterior/MAIN2F_PhotoSpecialistStores20261016/PolyHaven/modern_arm_chair_01/modern_arm_chair_01.fbx'
for i,z in enumerate([2.66,3.94,5.22]):
 add(f'DownloadedGrayArmchair_{i}','asset',3.17,z,.025,asset=arm,fit_size=[.62,.77,.67],yaw=90,group='Furniture',source=['L2026-005','F-ARMCHAIR'])
 cyl(f'RoundCafeTableTop_{i}',3.93,z,.75,.63,.045,'white',group='Furniture');cyl(f'RoundTableStem_{i}',3.93,z,.39,.055,.69,'white',group='Furniture');cyl(f'RoundTableBase_{i}',3.93,z,.035,.43,.04,'white',group='Furniture')
# Source bar perch stools with round chrome feet, curved seat, footrest rings.
for i,z in enumerate([3.15,4.25,5.35]):
 cyl(f'BarStoolPedestal_{i}',2.26,z,.38,.045,.68,'steel',group='Furniture');cyl(f'BarStoolBase_{i}',2.26,z,.035,.38,.045,'steel',group='Furniture');sphere(f'BarStoolSculptedSeat_{i}',2.26,z,.74,[.36,.13,.35],'beige',group='Furniture');box(f'BarStoolBack_{i}',2.12,z,.89,[.06,.30,.33],'beige',group='Furniture');cyl(f'BarStoolFootRing_{i}',2.26,z,.25,.27,.018,'steel',group='Furniture')
# Genuine source printed menu surface on four individual suspended screen assemblies.
for i,name in enumerate(['coffee','noncoffee','tea','dessert']):
 x=.42+i*.52;z=2.35;box(f'SuspendedScreenFrame_{i}',x,z,2.47,[.50,.63,.055],'black',group='MenuScreens',source=['L2026-012','L2026-013'])
 cyl(f'ScreenCeilingRod_{i}',x,z,2.89,.016,.30,'black',group='MenuScreens',collider=False,source=['L2026-004'])
 vv=[[.23,.295,0],[-.23,.295,0],[-.23,-.295,0],[.23,-.295,0]]
 add(f'ActualMenuPrint_{i}','mesh',x,z-.035,2.47,'menu_'+name,vertices=vv,triangles=[0,1,2,0,2,3],uv=[[0,1],[1,1],[1,0],[0,0]],yaw=180,collider=False,group='MenuScreens',source=['L2026-012','L2026-013'])
# Actual source right-wall geometric timber light feature; it is physical strips, not text.
box('RightWallLightRecess',W-.074,3.65,2.17,[.025,.68,2.28],'warmglow',group='WallDetails')
for z in [2.5,4.8]:box('TimberFeatureEnd_'+str(z),W-.095,z,2.17,[.04,.72,.035],'wood',group='WallDetails')
for yy in [1.81,2.53]:box('TimberFeatureRail_'+str(yy),W-.095,3.65,yy,[.04,.036,2.33],'wood',group='WallDetails')
for i,z in enumerate([2.85,3.3,3.75,4.2,4.6]):
 ob=box('TimberFeatureDiagonal_'+str(i),W-.101,z,2.17,[.036,.70,.028],'wood',group='WallDetails');ob['euler']=[32 if i%2 else -32,angle,0]
# Source original sign contour closed front/back/sides, thickness and bevel. No TextMeshPro/font.
sign=json.loads((P/'SIGN-BEVELED-SOURCE-SOLID.json').read_text());add('ActualSomidamPhotoOutlineSolid','mesh',W/2,.0005,2.97,'signwhite',vertices=sign['vertices'],triangles=sign['triangles'],yaw=180,group='StorefrontSign',source=['L2026-003'],collider=False)
for x in [W/2-1.35,W/2+1.35]:box('SignMount_'+str(x),x,.045,2.97,[.035,.035,.055],'steel',group='StorefrontSign')
# Actual tiny cups/trays using downloaded CC0 smallwares, with source-visible no product-name standins.
kenney=P.parent/'free-assets/kenney-food-kit/Models/FBX format'
if not kenney.exists():kenney=next((P.parent/'free-assets/kenney-food-kit').rglob('cup.fbx')).parent if list((P.parent/'free-assets/kenney-food-kit').rglob('cup.fbx')) else kenney
cups=list((P.parent/'free-assets/kenney-food-kit').rglob('*cup*fbx'))
if cups:
 cup=imp(cups[0],'Models/CC0-Cup.fbx')
 for j,z in enumerate([2.6,3.0,3.4]):add(f'DownloadedPrepCup_{j}','asset',.43,z,1.02,asset=cup,fit_size=[.10,.12,.10],material='white',collider=False,group='PrepEquipment',source=['L2026-004','F-KENNEY'])
packet={'schema':'chooguard.r25.store.physical_source_model.v3','store_key':'somidam','origin_q':org.tolist(),'floor_world_y':7.0,'source_packet':'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/somidam/SOURCE-EVIDENCE-R25.json','materials':mats,'source_imports':imports,'mesh_library':lib,'objects':objs,'bounds_qualification':'Current-CAD bounded MODEL, not surveyed lease perimeter or measured complete digital twin','no_text_proxy':True,'no_photo_sign_plane':True,'native_floor_columns_shell_preserved':True}
(P/'STORE-MODEL-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
# Freeze own copied constructor. No shared template mutation.
shaP=sha(P/'STORE-MODEL-PACKET-R25.json');cs=(P.parent/'StoreBuilderR25-v3-FREE-ASSETS.template.cs').read_text()
for a,c in [('__STORE_KEY__','somidam'),('__WORKER_THREAD__','classic:/root/r25_somidam'),('__EXACT_SCOPE__','C180_R25_SOMIDAM_APPLY_001'),('__PACKET_PATH__',str(P/'STORE-MODEL-PACKET-R25.json')),('__PACKET_SHA256__',shaP)]:cs=cs.replace(a,c)
(P/'ApplyStoreR25.cs').write_text(cs)
summary={'packet_sha256':shaP,'objects':len(objs),'source_imports':len(imports),'source_asset_instances':sum(x['kind']=='asset' for x in objs),'physical_source_sign':{'bevel_m':sign['bevel_m'],'depth_m':sign['depth_m'],'vertices':len(sign['vertices']),'triangles':len(sign['triangles'])//3},'MODEL_dimensions_m':[W,D],'no_Text_components':all(o['kind']!='text' for o in objs)}
(P/'FROZEN-PACKET-R25.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
