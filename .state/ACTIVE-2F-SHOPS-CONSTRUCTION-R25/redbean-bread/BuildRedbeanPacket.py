"""Own source-bound redbean booth authoring. No Unity access during preparation."""
from pathlib import Path
import json, math, hashlib, sys, shutil
from collections import Counter
import cv2, numpy as np
from PIL import Image

ROOT=Path('/Users/um-yunsang/CHOOGuard')
P=Path(__file__).parent;S=P/'sources';M=S/'models'
AS='Assets/ChooGuard/Art/StationInterior/R25_Shops/redbean-bread'
sys.path.insert(0,str(P.parent))
from photo_outline_to_solid import closed_solid_from_mask
B=json.loads((P/'MODEL-PLACEMENT-BASIS-R25.json').read_text())
base=B['base_q'];U=B['u'];V=B['v'];ANGLE=-math.degrees(math.atan2(U[1],U[0]))
objects=[];materials={};imports=[];library={};imported={}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def imp(src,dest):
 src=Path(src).resolve()
 if dest not in imported:
  imports.append(dict(source=str(src),destination=dest,sha256=sha(src)));imported[dest]=AS+'/'+dest
 return imported[dest]
def q(u,v):return [base[i]+U[i]*u+V[i]*v for i in range(2)]
def mat(n,c,**kw):materials[n]=dict(color=c if len(c)==4 else [*c,1],smoothness=kw.pop('smoothness',.38),**kw)
def add(kind,id,u,v,y,group,material=None,yaw=0,source_ids=None,**kw):
 o=dict(kind=kind,id=id,group=group,q=q(u,v),y=y,yaw=ANGLE+yaw,
        source_ids=source_ids or ['JUL26-00','SEP26-01','SEP26-02','SEP26-03','SEP26-04'],
        qualification='PHOTO_REFERENCED_NATIVE_ANCHORED_MODEL_NOT_FIELD_MEASUREMENT',**kw)
 if material:o['material']=material
 objects.append(o);return o
def cube(id,u,v,y,size,ma,group,yaw=0,**kw):return add('box',id,u,v,y,group,ma,yaw,size=size,**kw)
def cyl(id,u,v,y,diam,height,ma,group,**kw):return add('cylinder',id,u,v,y,group,ma,size=[diam,height/2,diam],collider=False,**kw)
def mesh(id,u,v,y,m,ma,group,yaw=0,**kw):
 library[id]={'vertices':m['vertices'],'triangles':m['triangles']}
 return add('library_mesh',id,u,v,y,group,ma,yaw,mesh_key=id,collider=False,**kw)
def asset(id,u,v,y,size,ap,mapping,group,yaw=90,**kw):
 return add('asset',id,u,v,y,group,yaw=yaw,asset=ap,fit_size=size,material_map=mapping,collider=kw.pop('collider',True),**kw)
def rect(name,src,points,size):
 im=cv2.imread(str(S/src));w,h=size
 T=cv2.getPerspectiveTransform(np.float32(points),np.float32([[0,0],[w,0],[w,h],[0,h]]))
 out=cv2.warpPerspective(im,T,size);f=S/(name+'.png');cv2.imwrite(str(f),out);return f,out
def source_mask(name,im,threshold,minarea=14):
 hsv=cv2.cvtColor(im,cv2.COLOR_BGR2HSV);mask=threshold(hsv).astype('uint8')*255
 n,lab,stats,_=cv2.connectedComponentsWithStats(mask);out=np.zeros_like(mask)
 for i in range(1,n):
  x,y,w,h,a=stats[i]
  if a>=minarea and h>6 and x>1 and x+w<mask.shape[1]-2:out[lab==i]=255
 cv2.imwrite(str(S/(name+'.png')),out);return out

# Source outlines, not a font substitution or a photo sign plane.
f,sign=rect('FASCIA-SOURCE-RECTIFIED','ROOT-gkdudtjr2-224325969836-00.jpg',
 [[289,92],[725,109],[725,168],[289,151]],(1500,205))
mask=source_mask('FASCIA-SOURCE-INK-MASK',sign,lambda h:(h[:,:,2]>170)&(h[:,:,1]<150),22)
library['actual_fascia_outline']=closed_solid_from_mask(mask,4.02,.55,.042,minimum_area=22,epsilon=.48)
logo=cv2.imread(str(S/'ROOT-gkdudtjr2-224325969836-00.jpg'))[246:322,155:237]
logo=cv2.resize(logo,None,fx=6,fy=6,interpolation=cv2.INTER_CUBIC)
mask=source_mask('TAESUNGDANG-SOURCE-MAROON-MASK',logo,lambda h:(h[:,:,0]>120)&(h[:,:,1]>40)&(h[:,:,2]>40),12)
# The small lettering is nearly neutral dark ink in this photograph; retain it
# from its actual source row rather than imposing the figure's hue threshold.
ink=cv2.cvtColor(logo,cv2.COLOR_BGR2GRAY)
mask[307:405,45:449]=(ink[307:405,45:449]<175).astype('uint8')*255
mask[:58]=0;mask[390:]=0;mask[:,:32]=0;mask[:,460:]=0
cv2.imwrite(str(S/'TAESUNGDANG-SOURCE-MAROON-MASK.png'),mask)
library['actual_taesungdang_outline']=closed_solid_from_mask(mask,.50,.46,.009,minimum_area=5,epsilon=.4)

# Own physically separate URP materials, explicit source material slot bindings.
for key,c,kw in [
 ('black',[.016,.019,.021],{}),('charcoal',[.045,.049,.052],{}),
 ('wood',[.40,.225,.105],{}),('wood_light',[.72,.49,.26],{}),
 ('steel',[.56,.59,.61],dict(metallic=.9,smoothness=.66)),
 ('brass',[.64,.46,.19],dict(metallic=.8,smoothness=.58)),
 ('white',[.94,.93,.87],{}),('paper',[.91,.86,.73],dict(smoothness=.18)),
 ('pink',[.88,.68,.67],{}),('maroon',[.44,.065,.15],{}),
 ('signwarm',[1,.95,.65],dict(emission=[1.1,.98,.50,1],smoothness=.45)),
 ('lamp',[1,.89,.64],dict(emission=[1.5,1.2,.68,1])),
 ('glass',[.81,.90,.91,.11],dict(transparent=True,smoothness=.96)),
 ('wrap',[.95,.96,.91,.115],dict(transparent=True,smoothness=.93)),
 ('darkglass',[.035,.047,.052],dict(metallic=.15,smoothness=.80))]:mat(key,c,**kw)
for label in ['wood_table_001','hamburger_buns']:
 f=M/label
 diff=imp(f/'diff.jpg',f'PBR/{label}/diff.jpg')
 n=next(f.glob('normal.*'));normal=imp(n,f'PBR/{label}/normal'+n.suffix)
 smooth=imp(f/'metallic_smoothness.png',f'PBR/{label}/metallic_smoothness.png')
 mat(label,[1,1,1],texture_asset=diff,normal_asset=normal,metallic_gloss_asset=smooth)
materials['wood']['texture_asset']=materials['wood_table_001']['texture_asset']
materials['wood_light']['texture_asset']=materials['wood_table_001']['texture_asset']
bread=imp(M/'hamburger_buns/hamburger_buns.fbx','FreeModels/scanned_buns/hamburger_buns.fbx')
display=imp(M/'display/50e0f0f7e37b4a80a6b809a8831835c2.fbx','FreeModels/display/BakeryDisplay.fbx')
displaymap={'R120G120B120':'glass','R127G127B127':'steel','Woods__Plastics.Finish_Carpentry.Wood.Pine.png':'wood_light','*':'steel'}
pos=imp(M/'pos/POS-Terminal.fbx','FreeModels/pos/POS-Terminal.fbx')
posdiff=imp(M/'pos/textures/Image_0.png','FreeModels/pos/base.png')
posnormal=imp(M/'pos/textures/Image_2.png','FreeModels/pos/normal.png')
a=np.array(Image.open(M/'pos/textures/Image_1.png').convert('RGB'))
rgba=np.zeros((*a.shape[:2],4),dtype='uint8');rgba[:,:,:3]=a[:,:,2,None];rgba[:,:,3]=255-a[:,:,1]
Image.fromarray(rgba).save(M/'pos/metallic_smoothness.png')
mat('pos_texture',[1,1,1],texture_asset=posdiff,normal_asset=posnormal,
 metallic_gloss_asset=imp(M/'pos/metallic_smoothness.png','FreeModels/pos/metallic_smoothness.png'))
oven=imp(M/'kaykit/oven.fbx','FreeModels/kaykit/oven.fbx')
bench=imp(M/'kaykit/kitchentable_A_large.fbx','FreeModels/kaykit/steel_bench.fbx')
credits=S/'SOURCE-LICENSES.md'
credits.write_text('''# R25 Redbean source model attribution

Bakery Display Case by mmcmindes, CC BY 4.0.
https://sketchfab.com/3d-models/bakery-display-case-50e0f0f7e37b4a80a6b809a8831835c2
Adapted only in own URP wood/steel/glass materials and proportions to field-source side chiller. Exact manufacturer identity not implied.

POS terminal by Vladyslav.Yarovyi, CC BY 4.0.
https://sketchfab.com/3d-models/pos-terminal-91c277a25bc34c5bbd080cd1e74161cb

Hamburger Buns and Wood Table 001 by Poly Haven contributors, CC0 1.0.
https://polyhaven.com/a/hamburger_buns
https://polyhaven.com/a/wood_table_001
Round bread scan adapted to source-visible individual packaged breads; not exact SKU geometry.

Oven and kitchen workbench by Kay Lousberg, KayKit Restaurant Bits FREE, CC0 1.0.
Source package license is included. Own independent steel/window/handle adaptation; no exact make or field measurement claim.

Original branch photographs: Naver gkdudtjr2 published 2026-07-19 and gpwl98 published 2026-09-11. Used as source evidence for own sign solids and actual printed menu/packaging information. No CC0 license is asserted for these photographs.
''')
imp(credits,'Credits/SOURCE-LICENSES.md')
for f,dest in [(M/'display/ACQUISITION.json','display-acquisition.json'),(M/'display/metadata.json','display-license-metadata.json'),
 (M/'pos/ACQUISITION.json','pos-acquisition.json'),(M/'kaykit/License.txt','KayKit-License.txt'),
 (M/'BNC-POLYHAVEN-SOURCE-LICENSE.json','PolyHaven-acquisition.json')]:imp(f,'Credits/'+dest)

# Shallow open-front source booth. Floor unchanged, no new glass room front.
REAR=5.72;FRONT=9.02;SOUTH=-4.35;NORTH=1.94;MID=(SOUTH+NORTH)/2;W=NORTH-SOUTH
cube('SourceBlackFasciaClosedPanel',8.86,MID,3.59,[W-.08,.76,.15],'black','Source_Booth_Fascia',yaw=90)
cube('SourceFasciaLowerRail',8.90,MID,3.18,[W-.08,.07,.12],'charcoal','Source_Booth_Fascia',yaw=90)
cube('SourceBlackSoffit',7.35,MID,3.99,[3.18,.075,W-.12],'charcoal','Source_Booth_Ceiling')
cube('SourceRearDarkPartition',5.79,MID,1.95,[.09,3.86,W-.14],'charcoal','Source_Visible_Rear')
# Folded grilles/reveals stay within own side and never duplicate Hawaiian glass.
for side,vc in [('South',SOUTH+.10),('North',NORTH-.12)]:
 cube('SideBlackReveal_'+side,8.78,vc,1.61,[.19,3.18,.15],'black','Source_Compressed_Grilles')
 for i in range(11):
  cube(f'FoldedGrille_{side}_{i}',8.22+i*.056,vc,1.60,[.035,3.11,.045],'charcoal','Source_Compressed_Grilles',collider=False)
mesh('ActualPhotoClosedSolidFascia',8.951,MID,3.61,library['actual_fascia_outline'],'signwarm','Physical_Source_Closed_Signs',yaw=90)
for i in range(6):cube('SourceSignStandoff_'+str(i),8.925,MID-1.7+i*.68,3.61,[.035,.08,.02],'steel','Physical_Source_Closed_Signs',collider=False)

# Source-left fluted POS station, source-center two-level display, source-right glass chiller.
POSV=SOUTH+.84
cube('SourcePOSFlutedBody',8.34,POSV,.59,[.82,1.18,1.27],'wood','Source_POS_Counter')
cube('SourcePOSTop',8.34,POSV,1.195,[.92,.045,1.34],'brass','Source_POS_Counter')
for i in range(32):cube('POSFrontTimberReed_'+str(i),8.758,POSV-.60+i*.038,.58,[.016,1.10,.016],'wood_light','Source_POS_Counter',collider=False)
asset('ActualDownloadedPOSTerminal',8.35,POSV,1.22,[.38,.42,.35],pos,{'Material_2100454184':'pos_texture','Material_2100454185':'black','*':'black'},'Downloaded_Equipment',yaw=-90)
CV=SOUTH+3.12
cube('SourceTwoLevelBreadCounter',8.31,CV,.50,[.78,.99,3.14],'wood','Source_Open_Bread_Display')
cube('SourceCounterLowerDeck',8.32,CV,1.01,[.89,.05,3.25],'wood','Source_Open_Bread_Display')
cube('SourceCounterBrassToeRail',8.72,CV,.055,[.035,.085,3.15],'brass','Source_Open_Bread_Display',collider=False)
for vv in [CV-1.55,CV,CV+1.55]:
 cyl('SourceUpperShelfPost_'+str(vv),8.10,vv,1.33,.038,.62,'brass','Source_Open_Bread_Display')
cube('SourceOpenRaisedShelf',8.10,CV,1.64,[.42,.055,3.20],'wood','Source_Open_Bread_Display')
for v in [CV-1.57,CV-.52,CV+.52,CV+1.57]:cube('CounterPanelBrassJoin_'+str(v),8.711,v,.5,[.025,.92,.028],'brass','Source_Open_Bread_Display',collider=False)
cube('UpperShelfWarmLight',8.12,CV,1.604,[.035,.015,3.12],'lamp','Source_Display_Lighting',collider=False)
CHV=NORTH-.83
cube('SourceChillerTimberPlinth',8.30,CHV,.22,[.84,.44,1.38],'wood','Source_Glass_Chiller')
asset('ActualDownloadedBakeryDisplay',8.30,CHV,.44,[1.34,1.29,.77],display,displaymap,'Downloaded_Equipment')
for k,y in enumerate([.68,1.04,1.39]):
 for j in range(8):cube(f'ChillerSlattedTray_{k}_{j}',8.29-.29+j*.081,CHV,y,[.055,.018,1.22],'wood_light','Source_Glass_Chiller',collider=False)
 cube('ChillerShelfLight_'+str(k),8.05,CHV,y+.23,[.018,.014,1.23],'lamp','Source_Glass_Chiller',collider=False)
for j in range(24):cube('ChillerLowerAirGrille_'+str(j),8.699,CHV-.57+j*.049,.45,[.021,.055,.012],'black','Source_Glass_Chiller',collider=False)

# Actual source Taesungdang house lightbox has its own solid profile and raised outline.
def extruded_polygon(coords,depth):
 from shapely.geometry import Polygon
 from shapely import constrained_delaunay_triangles
 poly=Polygon(coords);vs=[];ts=[]
 def tri(a,b,c):k=len(vs);vs.extend([a,b,c]);ts.extend([k,k+1,k+2])
 for t in constrained_delaunay_triangles(poly).geoms:
  a,b,c=list(t.exterior.coords)[:3]
  if np.cross(np.array(b)-a,np.array(c)-a)<0:b,c=c,b
  tri([*a,0],[*b,0],[*c,0]);tri([*c,-depth],[*b,-depth],[*a,-depth])
 for a,b in zip(poly.exterior.coords,list(poly.exterior.coords)[1:]):
  tri([*a,-depth],[*b,-depth],[*b,0]);tri([*a,-depth],[*b,0],[*a,0])
 return dict(vertices=vs,triangles=ts)
house=extruded_polygon([[-.30,-.26],[.30,-.26],[.30,.12],[0,.29],[-.30,.12]],.075)
mesh('SourceTaesungdangHouseLightbox',8.71,POSV+.15,2.53,house,'white','Physical_Source_Closed_Signs',yaw=90)
mesh('SourceTaesungdangMaroonLogo',8.721,POSV+.15,2.53,library['actual_taesungdang_outline'],'maroon','Physical_Source_Closed_Signs',yaw=90)

# Four real source menu panels with printed image information on physical hardware.
menu_quads=[[[262,236],[383,239],[383,318],[262,315]],[[392,239],[509,240],[509,319],[392,318]],
 [[519,240],[629,241],[629,319],[519,319]],[[639,241],[745,241],[745,319],[639,319]]]
for i,pts in enumerate(menu_quads):
 f,_=rect('MENU-SOURCE-'+str(i),'ROOT-gkdudtjr2-224325969836-00.jpg',pts,(600,430))
 tex=imp(f,'Prints/menu-source-'+str(i)+'.png');mat('menu_'+str(i),[1,1,1],texture_asset=tex,emission=[.12,.12,.10,1])
 v=SOUTH+1.85+i*.94
 cube('SourceMenuPanelHousing_'+str(i),7.85,v,2.63,[.07,.72,.88],'black','Source_Menu_Physical_Panels')
 o=add('mesh','SourceMenuPrintedFace_'+str(i),7.894,v,2.63,'Source_Menu_Physical_Panels','menu_'+str(i),90,
       vertices=[[-.42,-.34,0],[.42,-.34,0],[.42,.34,0],[-.42,.34,0]],triangles=[0,1,2,0,2,3],
       uv=[[1,0],[0,0],[0,1],[1,1]],collider=False)
 cube('MenuSuspensionRod_'+str(i),7.84,v,3.31,[.018,.63,.018],'black','Source_Menu_Physical_Panels',collider=False)

# Visible stainless preparation benches, stacked rear oven and sheet-pan racks.
for i,v in enumerate([SOUTH+1.80,SOUTH+4.07]):
 asset('DownloadedSteelPrepBench_'+str(i),6.27,v,0,[2.08,.90,.65],bench,{'*':'steel'},'Downloaded_Visible_Rear_Equipment')
for k in range(2):
 asset('DownloadedRearOven_'+str(k),6.25,SOUTH+3.63,.91+k*.49,[.84,.47,.60],oven,{'*':'steel'},'Downloaded_Visible_Rear_Equipment')
 cube('OvenDarkGlazedWindow_'+str(k),6.565,SOUTH+3.63,1.13+k*.49,[.018,.28,.59],'darkglass','Source_Rear_Equipment_Detail',collider=False)
 cube('OvenSteelPull_'+str(k),6.60,SOUTH+3.63,1.27+k*.49,[.045,.026,.53],'steel','Source_Rear_Equipment_Detail',collider=False)
 for z in [SOUTH+3.26,SOUTH+4.00]:cyl('OvenControl_'+str((k,z)),6.60,z,1.25+k*.49,.035,.022,'black','Source_Rear_Equipment_Detail')
for rack,v in enumerate([SOUTH+1.40,SOUTH+2.45]):
 for u in [5.98,6.47]:
  for vv in [v-.43,v+.43]:cyl('RackUpright_'+str((rack,u,vv)),u,vv,1.59,.023,1.36,'steel','Source_Visible_Tray_Racks')
 for j in range(5):
  y=1.00+j*.27
  cube('RackShelf_'+str((rack,j)),6.22,v,y,[.54,.025,.91],'steel','Source_Visible_Tray_Racks',collider=False)
 for j in range(7):cube('StackedSheetPan_'+str((rack,j)),6.21,v,2.09+j*.019,[.49,.012,.83],'steel','Source_Visible_Tray_Racks',collider=False)

# Dense actual scanned round-bread assets, individual transparent wrappers and open gift sets.
counter=0
for tier,(u,y,v0,n,cols) in enumerate([(8.35,1.045,CV-1.40,12,2),(8.10,1.675,CV-1.38,12,1),
                                        (8.30,.71,CHV-.53,5,2),(8.30,1.07,CHV-.53,5,2),(8.30,1.42,CHV-.53,5,2)]):
 for j in range(n):
  for row in range(cols):
   vv=v0+j*(2.76/(n-1) if n==12 else .265);uu=u+(row-(cols-1)/2)*.215
   counter+=1
   asset('ScannedPackagedBread_'+str(counter),uu,vv,y,[.164,.105,.145],bread,{'*':'hamburger_buns'},'Downloaded_Scanned_Bread_Products',yaw=(j*17)%360,collider=False)
   cube('IndividualClearBreadWrapper_'+str(counter),uu,vv,y+.067,[.183,.142,.163],'wrap','Individual_Physical_Packaging',collider=False)
   # folded sealed seam, separate transparent geometry
   cube('BreadBagTopSeal_'+str(counter),uu,vv,y+.146,[.162,.012,.018],'wrap','Individual_Physical_Packaging',collider=False)
for b in range(6):
 v=CV-1.30+b*.50
 cube('OpenGiftBoxBase_'+str(b),8.48,v,1.055,[.28,.035,.44],'paper','Source_Gift_Packs',collider=False)
 for vv in [v-.218,v+.218]:cube('GiftBoxEnd_'+str((b,vv)),8.48,vv,1.12,[.28,.14,.012],'paper','Source_Gift_Packs',collider=False)
 cube('GiftBoxRaisedLid_'+str(b),8.30,v,1.20,[.018,.32,.44],'paper','Source_Gift_Packs',collider=False)
 for k in range(2):
  asset('GiftScannedBun_'+str((b,k)),8.48,v+(k-.5)*.19,1.075,[.145,.085,.145],bread,{'*':'hamburger_buns'},'Downloaded_Scanned_Bread_Products',yaw=k*30,collider=False)
# Source giftbox display at left rear, below lit menu. No hidden room or seating added.
for k in range(4):
 cube('POSSideGiftShelf_'+str(k),7.10,SOUTH+.44,.90+k*.40,[.48,.032,.64],'steel','Source_Gift_Pack_Display',collider=False)
 for j in range(2):cube('PhysicalGiftCarton_'+str((k,j)),7.10,SOUTH+.28+j*.30,1.00+k*.40,[.34,.17,.27],'pink' if k%2 else 'paper','Source_Gift_Pack_Display',collider=False)

# Warm track fixtures, physical cylinders and lenses individually editable.
cube('FrontLightingTrack',8.15,MID,3.14,[.025,.025,W-.45],'black','Source_Track_Lighting',collider=False)
for i in range(9):
 v=SOUTH+.40+i*(W-.80)/8
 cyl('TrackLampBody_'+str(i),8.15,v,3.02,.092,.18,'black','Source_Track_Lighting')
 cyl('TrackLampLens_'+str(i),8.15,v,2.925,.076,.008,'lamp','Source_Track_Lighting')
for i in range(4):
 v=SOUTH+1.38+i*1.2
 cyl('DisplayLampBody_'+str(i),7.74,v,2.14,.10,.14,'black','Source_Track_Lighting')
 cyl('DisplayLampLens_'+str(i),7.74,v,2.065,.084,.007,'lamp','Source_Track_Lighting')
 add('light','SourceWarmDisplayLight_'+str(i),7.84,v,2.10,'Source_Track_Lighting',color=[1,.84,.59,1],range=2.4,intensity=.28)

packet=dict(store_key='redbean-bread',origin_q=base,floor_world_y=7.0,materials=materials,
 source_imports=imports,mesh_library=library,objects=objects,
 source_packet={'source_evidence':str(P/'SOURCE-EVIDENCE-R25.json'),'model_placement_basis':str(P/'MODEL-PLACEMENT-BASIS-R25.json'),
 'identity':'부산국제시장단팥빵 primary fascia and 태성당 package/lightbox coexist in July/Sept2026 exactbranch',
 'all_dimensions':'PHOTO_REFERENCED_MODEL_NOT_SURVEYED','no_hidden_BOH':True,'no_TMP_or_text_signs':True})
out=P/'STORE-PACKET-R25.json';out.write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
scope='C180_R25_REDBEAN_BREAD_APPLY_001'
cs=(P.parent/'StoreBuilderR25-v4-SOURCE-MATERIALS.template.cs').read_text()
for a,b in {'__STORE_KEY__':'redbean-bread','__WORKER_THREAD__':'classic:/root/r25_redbean_bread',
 '__EXACT_SCOPE__':scope,'__PACKET_PATH__':str(out),'__PACKET_SHA256__':sha(out)}.items():cs=cs.replace(a,b)
(P/'ApplyStoreR25.cs').write_text(cs)
print(json.dumps({'objects':len(objects),'kinds':dict(Counter(o['kind'] for o in objects)),'imports':len(imports),'materials':len(materials),'physical_sign_contours':library['actual_fascia_outline']['source_polygons'],'packet_sha256':sha(out)}))
