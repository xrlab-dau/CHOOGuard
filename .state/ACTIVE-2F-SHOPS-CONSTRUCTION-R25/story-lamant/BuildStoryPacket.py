"""Source-bound Story Lamant construction authoring; no Unity access."""
from pathlib import Path
import json, math, hashlib, importlib.util, cv2, numpy as np
from shapely.geometry import Polygon, MultiPoint, box
from shapely import constrained_delaunay_triangles
from shapely.ops import unary_union

ROOT=Path('/Users/um-yunsang/CHOOGuard')
BASE=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/story-lamant'
SRC=BASE/'sources'; SHARED=BASE.parent/'free-assets'
ASSET='Assets/ChooGuard/Art/StationInterior/R25_Shops/story-lamant'
B=json.loads((BASE/'MODEL-PLACEMENT-BASIS-R25.json').read_text())
OR=np.array(B['front_axis_origin_q']);U=np.array(B['bay_unit_x_q']);V=np.array(B['bay_unit_depth_q'])
W=B['front_axis_span_m'];D=B['depth_to_current_floor_edge_inset_m']
ANGLE=-math.degrees(math.atan2(U[1],U[0]))
objects=[];materials={};imports=[];library={};imported={}
def q(x,z):return list(OR+U*x+V*z)
def mat(key,color,**kw):materials[key]=dict(color=color if len(color)==4 else [*color,1],**kw)
for key,color,kw in [
 ('black',[.018,.022,.024],dict(smoothness=.36)),('charcoal',[.065,.073,.077],dict(smoothness=.32)),
 ('wall',[.84,.835,.79],dict(smoothness=.22)),('ceiling',[.9,.895,.86],dict(smoothness=.23)),
 ('stone',[.045,.085,.068],dict(smoothness=.78)),('chrome',[.68,.70,.72],dict(metallic=.92,smoothness=.8)),
 ('wood',[.63,.36,.16],dict(smoothness=.3)),('plywood',[.83,.66,.4],dict(smoothness=.32)),
 ('screen_black',[.012,.015,.018],dict(smoothness=.6)),('whiteink',[.95,.96,.94],dict(smoothness=.4,emission=[.38,.38,.36,1])),
 ('lamp',[.96,.95,.88],dict(smoothness=.35,emission=[1.5,1.45,1.2,1])),
 ('red',[.78,.085,.1],{}),('yellow',[.89,.62,.08],{}),('green',[.14,.39,.22],{}),
 ('paper',[.89,.87,.8],dict(smoothness=.14)),
 ('lettuce',[.22,.43,.10],dict(smoothness=.25)),('ham',[.66,.31,.28],dict(smoothness=.45)),
 ('tomato',[.65,.07,.035],dict(smoothness=.55)),('cheese',[.93,.62,.11],dict(smoothness=.3))]:mat(key,color,**kw)
mat('glass',[.84,.92,.93,.105],smoothness=.95,transparent=True)
def imp(path,dest=None):
 path=Path(path).resolve();dest=dest or 'FreeModels/'+path.name
 if dest not in imported:
  imports.append(dict(source=str(path),destination=dest,sha256=hashlib.sha256(path.read_bytes()).hexdigest()));imported[dest]=ASSET+'/'+dest
 return imported[dest]
def add(kind,id,x,z,y=0,group='Interior',source=('OCT26-11',),**kw):
 o=dict(kind=kind,id=id,group=group,q=q(x,z),y=y,source_ids=list(source),qualification='SOURCE_REFERENCED_NATIVE_ANCHORED_MODEL_NOT_FIELD_MEASUREMENT',**kw);objects.append(o);return o
def cube(id,x,z,y,size,material,group='Interior',source=('OCT26-11',),yaw=0,**kw):return add('box',id,x,z,y,group,source,size=size,material=material,yaw=ANGLE+yaw,**kw)
def cyl(id,x,z,y,diam,h,material,group='Interior',source=('OCT26-11',),**kw):return add('cylinder',id,x,z,y,group,source,size=[diam,h/2,diam],material=material,collider=False,**kw)
def mesh(id,x,z,y,m,material,group,source=('OCT26-00',),yaw=0,**kw):
 library[id]=dict(vertices=m['vertices'],triangles=m['triangles'])
 return add('library_mesh',id,x,z,y,group,source,mesh_key=id,material=material,yaw=ANGLE+yaw,collider=False,**kw)
def asset(id,x,z,y,size,path,group='Downloaded_Free_Models',source=('OCT26-11',),yaw=0,material=None,material_map=None):
 ap=imp(path)
 o=add('asset',id,x,z,y,group,source,fit_size=size,asset=ap,yaw=ANGLE+yaw,collider=True)
 if material:o['material']=material
 if material_map:o['material_map']=material_map
 return o
def extrusion(id,poly,bottom,height,material,group):
 if poly.is_empty:return
 if poly.geom_type!='Polygon':
  for i,p in enumerate(poly.geoms):extrusion(id+'_'+str(i),p,bottom,height,material,group)
  return
 vs=[];ts=[]
 def tri(a,b,c):
  k=len(vs);vs.extend([a,b,c]);ts.extend([k,k+1,k+2])
 for t in constrained_delaunay_triangles(poly).geoms:
  a,b,c=list(t.exterior.coords)[:3]
  if np.cross(np.array(b)-a,np.array(c)-a)<0:b,c=c,b
  tri([a[0],height,a[1]],[c[0],height,c[1]],[b[0],height,b[1]])
  tri([a[0],0,a[1]],[b[0],0,b[1]],[c[0],0,c[1]])
 for ring in [poly.exterior,*poly.interiors]:
  for a,b in zip(ring.coords,list(ring.coords)[1:]):
   tri([a[0],0,a[1]],[a[0],height,a[1]],[b[0],height,b[1]])
   tri([a[0],0,a[1]],[b[0],height,b[1]],[b[0],0,b[1]])
 mesh(id,0,0,bottom,dict(vertices=vs,triangles=ts),material,group,('NATIVE-R19','OCT26-00','OCT26-12'))

# Own downloadable-model/PBR copies. All imports occur inside the C180 lease.
for name in ['wood_table_001','croissant','hamburger_buns']:
 p=SRC/'free-assets'/name
 diff=imp(p/'diff.jpg',f'PBR/{name}/diff.jpg')
 normal=imp(next(p.glob('normal.*')),f'PBR/{name}/normal'+next(p.glob('normal.*')).suffix)
 smooth=imp(p/'metallic_smoothness.png',f'PBR/{name}/metallic_smoothness.png')
 mat(name,[1,1,1],texture_asset=diff,normal_asset=normal,metallic_gloss_asset=smooth)
materials['plywood'].update(texture_asset=materials['wood_table_001']['texture_asset'],normal_asset=materials['wood_table_001']['normal_asset'])
materials['wood'].update(texture_asset=materials['wood_table_001']['texture_asset'])
pos_tex=imp(SHARED/'pos-terminal/textures/Image_0.png','PBR/POS/diff.png')
mat('pos_texture',[1,1,1],texture_asset=pos_tex,smoothness=.42)
chair=SRC/'FieldPlywoodChromeChair_MODEL.fbx'
table=SRC/'cafe-table/CafeTableSteelBase.fbx'
for path in [SRC/'cafe-table/ACQUISITION.json',SRC/'CHAIR-DERIVATION.json',SHARED/'source-0d033270863d45748c14556a562bc08c/ACQUISITION.json']:
 imp(path,'Credits/'+('chair-source.json' if path.parent.name.startswith('source-') else path.name))
credit=SRC/'SOURCE-LICENSES.md'
credit.write_text('# Story Lamant source model attribution\n\n4-Legged Ant Chair by Deborah Kumagai, CC-BY 4.0. https://sketchfab.com/3d-models/4-legged-ant-chair-0d033270863d45748c14556a562bc08c . Independent formed-shell derivative; original retained.\n\nTable for cafes and bars by Andy.Honor, CC-BY 4.0. https://sketchfab.com/3d-models/table-for-cafes-and-bars-b405587c16bf4851921f0620c4c98998 . Steel table base derivative; original slatted top/chairs excluded.\n\nBakery Display Case by mmcmindes, CC-BY 4.0. https://sketchfab.com/3d-models/bakery-display-case-50e0f0f7e37b4a80a6b809a8831835c2 . Own charcoal-base/glass material adaptation and low-case proportion fit.\n\nPOS terminal by Vladyslav.Yarovyi, CC-BY 4.0. https://sketchfab.com/3d-models/pos-terminal-91c277a25bc34c5bbd080cd1e74161cb .\n\nCroissant, Hamburger Buns and Wood Table 001 by Poly Haven contributors, CC0 1.0: https://polyhaven.com/license .\n\nField photographs by Naver author aru0219, published 2026-10-13; evidence-source-derived custom sign geometry, menus and appearance; no claim the photographs are CC0.\n')
imp(credit,'Credits/SOURCE-LICENSES.md')

# Reuse all fixed native bodies. Subtract their actual full section footprints.
native=[]
for col in B['front_native_anchors']+B['rear_native_anchors']:
 pts=np.array(col['body_section']).reshape(-1,2)
 local=[[(p-OR)@U,(p-OR)@V] for p in pts]
 native.append(MultiPoint(local).convex_hull.buffer(.015))
cut=unary_union(native)
room=box(.045,.02,W-.045,D-.035)
extrusion('SourceSeparateCeiling',room.difference(cut),3.03,.07,'ceiling','Booth_Shell')
for name,shape in [('WestReturn',box(.035,.03,.125,D-.03)),('EastWhiteWall',box(W-.125,.03,W-.035,D-.03))]:
 extrusion(name,shape.difference(cut),0,3.03,'wall','Booth_Shell')
# Rear photographic glazing/blinds above the source counter; no new floor geometry.
cube('RearGlazing',W/2,D-.085,1.65,[W-.3,2.7,.012],'glass','Booth_Shell',collider=False)
for i in range(6):
 x=.75+i*1.36
 cube(f'RearBlind_{i}',x,D-.13,2.0,[1.28,1.75,.016],'wall','Rear_Counter',collider=False)
 cube(f'RearMullion_{i}',x-.67,D-.1,1.65,[.038,2.7,.04],'whiteink','Booth_Shell',collider=False)

# Actual black glazed frontage, entry door leaves open into shop, source-visible handles.
lo=.61;hi=W-.61;doorA=W/2-.96;doorB=W/2+.96
cube('BlackFascia',W/2,.065,2.86,[hi-lo,.19+.5,.16],'black','Booth_Front',source=('OCT26-00',))
for k,(a,b) in enumerate([(lo,lo+1.45),(lo+1.45,doorA),(doorB,hi-1.45),(hi-1.45,hi)]):
 if b-a<.1:continue
 cube(f'FixedGlazing_{k}',(a+b)/2,.065,1.25,[b-a-.05,2.48,.012],'glass','Booth_Front',source=('OCT26-00',),collider=False)
 for side,x in [('L',a),('R',b)]:cube(f'GlazingMullion_{k}_{side}',x,.065,1.28,[.045,2.56,.075],'black','Booth_Front',source=('OCT26-00',))
 cube(f'GlazingSill_{k}',(a+b)/2,.065,.055,[b-a,.11,.075],'black','Booth_Front',source=('OCT26-00',))
for i,(hinge,rot,sign) in enumerate([(doorA,-72,1),(doorB,72,-1)]):
 theta=math.radians(rot);cx=hinge+sign*.46*math.cos(theta);cz=.065-sign*.46*math.sin(theta)
 cube(f'OpenGlassDoor_{i}',cx,cz,1.25,[.91,2.48,.012],'glass','Entry_Doors',source=('OCT26-00',),yaw=rot,collider=False)
 for dx in [-.46,.46]:cube(f'DoorFrame_{i}_{dx}',cx+dx*math.cos(theta),cz-dx*math.sin(theta),1.28,[.04,2.56,.06],'black','Entry_Doors',source=('OCT26-00',),yaw=rot)
 for h in [.04,2.53]:cube(f'DoorRail_{i}_{h}',cx,cz,h,[.96,.05,.06],'black','Entry_Doors',source=('OCT26-00',),yaw=rot)
 cube(f'DoorPull_{i}',cx+sign*.31*math.cos(theta),cz-sign*.31*math.sin(theta)-.05,1.1,[.025,.38,.035],'chrome','Entry_Doors',source=('OCT26-00',),yaw=rot,collider=False)

# Exact photograph wordmark contour, separate thick closed solid and mounting pins.
spec=importlib.util.spec_from_file_location('outline',BASE.parent/'photo_outline_to_solid.py');outline=importlib.util.module_from_spec(spec);spec.loader.exec_module(outline)
ink=cv2.imread(str(SRC/'logo-mask.png'),0)
m=outline.closed_solid_from_mask(ink,2.75,.70,.052,minimum_area=30,epsilon=.7)
mesh('ActualPhotoLamantWordmark',W/2,-.027,2.86,m,'whiteink','Physical_Source_Signs',('OCT26-00',),yaw=180)
for i in range(5):cube(f'WordmarkMount_{i}',W/2-1.1+i*.55,.014,2.86,[.018,.15,.055],'chrome','Physical_Source_Signs',source=('OCT26-00',),collider=False)
mask=cv2.imread(str(SRC/'medallion-mask.png'),0);mask[:25]=0;mask[:,0:30]=0;mask[:,610:]=0;mask[485:]=0
m=outline.closed_solid_from_mask(mask,.92,.75,.0015,minimum_area=50,epsilon=.7)
mesh('PhotoBakeWithLoveGlassInk',1.25,.051,1.89,m,'whiteink','Physical_Source_Signs',('OCT26-01',),yaw=180)
# Source floral cutout outlines, separately editable colored solids on glazing.
photo=cv2.imread(str(SRC/'ROOT-aru0219-224439792763-01.jpg'))[748:1120,30:690]
hsv=cv2.cvtColor(photo,cv2.COLOR_BGR2HSV)
for color,mask in [('red',cv2.inRange(hsv,np.array([0,90,45]),np.array([12,255,255]))|cv2.inRange(hsv,np.array([170,90,45]),np.array([179,255,255]))),('yellow',cv2.inRange(hsv,np.array([16,90,65]),np.array([38,255,255])))]:
 n,lab,stats,_=cv2.connectedComponentsWithStats(mask);areas=stats[1:,cv2.CC_STAT_AREA];idx=1+int(np.argmax(areas));mask=(lab==idx).astype('uint8')*255
 mask=cv2.morphologyEx(mask,cv2.MORPH_CLOSE,np.ones((5,5),np.uint8))
 for i,x in enumerate([1.30,2.50,6.13,7.1]):
  black=cv2.dilate(mask,np.ones((15,15),np.uint8))
  mesh(f'FlowerOutline_{color}_{i}',x,.046,.48,outline.closed_solid_from_mask(black,1.0,.65,.002,80,1),'black','Source_Floral_Glass_Decals',('OCT26-01',),yaw=180)
  mesh(f'FlowerInk_{color}_{i}',x,.043,.48,outline.closed_solid_from_mask(mask,1.0,.65,.002,80,1),color,'Source_Floral_Glass_Decals',('OCT26-01',),yaw=180)

# Long observed counter, till source asset, three suspended source menu monitors.
cube('RearCounterRibbedBody',W/2,D-1.0,.5,[W-1.15,.99,.68],'charcoal','Rear_Counter')
cube('RearCounterTop',W/2,D-1,.997,[W-1.08,.04,.75],'stone','Rear_Counter')
for i in range(45):cube(f'CounterVerticalRib_{i}',.62+i*(W-1.24)/44,D-1.355,.49,[.018,.89,.018],'black','Rear_Counter',collider=False)
asset('DownloadedPOSTerminal',W*.58,D-1.03,1.025,[.34,.38,.31],SHARED/'pos-terminal/POS-Terminal.fbx',source=('OCT26-11',),yaw=180,material_map={'Material_2100454184':'pos_texture','Material_2100454185':'black'})
for i in range(3):
 x=W/2+(i-1)*1.0
 cube(f'MenuMonitorCase_{i}',x,D-1.04,2.2,[.98,.58,.055],'screen_black','Real_Source_Menu_Monitors',source=('OCT26-02',))
 tex=imp(SRC/f'menu-screen-{i}.png',f'Prints/menu-screen-{i}.png');mat('menu'+str(i),[1,1,1],texture_asset=tex,emission=[.18,.18,.18,1])
 # Printed genuine menu surface on an actual monitor, not an equipment substitute.
 vs=[[-.465,-.26,0],[.465,-.26,0],[.465,.26,0],[-.465,.26,0]]
 o=add('mesh',f'MenuSourceScreen_{i}',x,D-1.073,2.2,'Real_Source_Menu_Monitors',('OCT26-02',),vertices=vs,triangles=[0,1,2,0,2,3],uv=[[1,0],[0,0],[0,1],[1,1]],material='menu'+str(i),yaw=ANGLE+180,collider=False)
 cube(f'MenuSuspension_{i}',x,D-1.04,2.72,[.018,.6,.018],'black','Real_Source_Menu_Monitors',source=('OCT26-02',),collider=False)

# Source bakery island custom joinery with separately editable wooden wire-rack trays.
for island,(x,z,sx,sz) in enumerate([(1.55,1.75,1.45,.95),(1.55,3.25,1.45,.95)]):
 cube(f'BakeryIslandBody_{island}',x,z,.46,[sx,.92,sz],'charcoal','Bakery_Islands',source=('OCT26-03',))
 cube(f'BakeryIslandStone_{island}',x,z,.94,[sx+.04,.05,sz+.04],'stone','Bakery_Islands',source=('OCT26-03',))
 for j in range(15):cube(f'IslandFlute_{island}_{j}',x-sx/2+.045+j*(sx-.09)/14,z-sz/2-.01,.46,[.018,.85,.025],'black','Bakery_Islands',source=('OCT26-03',),collider=False)
 for t in range(2):
  tx=x+(t-.5)*.69
  cube(f'TrayBase_{island}_{t}',tx,z,.985,[.64,.018,.79],'paper','Bakery_Wood_Trays',source=('OCT26-03',),collider=False)
  for side,dx,dz,size in [('L',-.33,0,[.025,.09,.84]),('R',.33,0,[.025,.09,.84]),('F',0,-.41,[.68,.09,.025]),('B',0,.41,[.68,.09,.025])]:cube(f'TrayRim_{island}_{t}_{side}',tx+dx,z+dz,1.02,size,'wood','Bakery_Wood_Trays',source=('OCT26-03',),collider=False)
  for w in range(14):cube(f'TrayWire_{island}_{t}_{w}',tx,z-.37+w*.055,1.002,[.62,.003,.003],'chrome','Bakery_Wood_Trays',source=('OCT26-03',),collider=False)
  for row in range(3):
   for col in range(2):
    name='croissant' if (island+t)%2==0 else 'hamburger_buns'
    asset(f'ScannedBread_{island}_{t}_{row}_{col}',tx+(col-.5)*.28,z+(row-1)*.22,1.01,[.25,.11,.20],SRC/f'free-assets/{name}/{name}.fbx',group='Actual_Downloaded_Scanned_Bakery',source=('OCT26-03','OCT26-04'),yaw=(row*17+col*13)%30,material=name)['collider']=False

# Long dark display: actual source shelves and glass, physical wares and scanned bread.
case=SHARED/'source-50e0f0f7e37b4a80a6b809a8831835c2/50e0f0f7e37b4a80a6b809a8831835c2.fbx'
imp(case.parent/'ACQUISITION.json','Credits/case-source.json')
asset('DownloadedLowBlackGlazedSandwichCase',1.72,4.76,0,[1.70,1.07,.78],case,group='Downloaded_Low_Display_Case',source=('OCT26-07','OCT26-11'),yaw=180,material_map={'R120G120B120':'glass','R127G127B127':'black','Woods__Plastics.Finish_Carpentry.Wood.Pine.png':'stone'})
for row,y in enumerate([.53,.78]):
 for i in range(3):
  x=1.2+i*.5;z=4.65
  cube(f'SandwichTray_{row}_{i}',x,z,y,[.43,.015,.35],'paper','Physical_Croissant_Sandwiches',source=('OCT26-07',),collider=False)
  asset(f'ScannedCroissantSandwichBread_{row}_{i}',x,z,y+.02,[.35,.12,.26],SRC/'free-assets/croissant/croissant.fbx',group='Physical_Croissant_Sandwiches',source=('OCT26-07',),material='croissant')['collider']=False
  # Closed thin folded fillings are source-observed physical geometry.
  cube(f'SandwichHam_{row}_{i}',x,z-.055,y+.080,[.28,.012,.19],'ham','Physical_Croissant_Sandwiches',source=('OCT26-07',),collider=False)
  cube(f'SandwichCheese_{row}_{i}',x-.02,z-.08,y+.086,[.24,.007,.16],'cheese','Physical_Croissant_Sandwiches',source=('OCT26-07',),yaw=12,collider=False)
  cyl(f'SandwichTomato_{row}_{i}',x+.03,z-.09,y+.095,.12,.012,'tomato','Physical_Croissant_Sandwiches',source=('OCT26-07',))
  for j in range(3):
   o=add('sphere',f'SandwichLettuceFold_{row}_{i}_{j}',x-.09+j*.085,z-.12,y+.10,'Physical_Croissant_Sandwiches',('OCT26-07',),size=[.11,.018,.08],material='lettuce',collider=False)
for tier,y in enumerate([.35,.95,1.52]):
 cube(f'BakeryWallShelf_{tier}',.53,3.6,y,[.64,.035,3.15],'black','Side_Bakery_Display',source=('OCT26-05','OCT26-06'))
for i,z in enumerate([2.08,3.65,5.12]):cube(f'BakeryShelfUpright_{i}',.23,z,1.0,[.035,2.0,.035],'black','Side_Bakery_Display',source=('OCT26-05',))
for y in [.40,1.00]:
 for j,z in enumerate([2.5,3.25,4.0,4.75]):
  cyl(f'PorcelainPlate_{y}_{j}',.53,z,y,.36,.018,'whiteink','Side_Bakery_Display',source=('OCT26-06',))
  asset(f'ShelfScannedBun_{y}_{j}',.53,z,y+.02,[.27,.11,.22],SRC/'free-assets/hamburger_buns/hamburger_buns.fbx',group='Actual_Downloaded_Scanned_Bakery',source=('OCT26-06',),material='hamburger_buns')['collider']=False

# Source dining furniture: downloaded formed plywood chair/chrome chassis and
# downloaded steel cross-foot table bases, photo-matched separate black tops.
chairs=0
for idx,(x,z) in enumerate([(4.55,1.75),(6.25,1.95),(4.55,3.85),(6.0,3.85),(7.45,3.85)]):
 asset(f'DownloadedCafeSteelTableBase_{idx}',x,z,0,[.56,.705,.56],table,group='Seating_Downloaded_Table_Bases',source=('OCT26-11','OCT26-12'),material='black')
 cube(f'BlackLaminateSquareTop_{idx}',x,z,.722,[.68,.032,.68],'charcoal','Seating_Downloaded_Table_Bases',source=('OCT26-11','OCT26-12'))
 for dz,yaw in ([ (.61,180)] if idx==1 else [(-.61,0),(.61,180)]):
  asset(f'DownloadedPlywoodChromeChair_{chairs}',x,z+dz,0,[.45,.8,.48],chair,group='Seating_Downloaded_Plywood_Chairs',source=('OCT26-11','OCT26-12'),yaw=yaw,material_map={'Black_Opaque_Plastic':'black','Metal':'chrome','Plastic':'plywood'});chairs+=1
# Photo black narrow common counter by the front glass, placed within own side.
cube('FrontBlackCommonTableTop',6.72,.32,.75,[2.05,.035,.43],'black','Front_Seating',source=('OCT26-12',))
for x in [5.80,7.64]:cube('CommonTableLeg_'+str(x),x,.32,.375,[.04,.75,.37],'black','Front_Seating',source=('OCT26-12',))
cube('CommonTableUpstand',6.72,.12,.87,[2.05,.2,.035],'black','Front_Seating',source=('OCT26-12',))
for i,x in enumerate([6.10,6.83,7.50]):asset('FrontChair_'+str(i),x,.90,0,[.45,.8,.48],chair,group='Seating_Downloaded_Plywood_Chairs',source=('OCT26-12',),yaw=180,material_map={'Black_Opaque_Plastic':'black','Metal':'chrome','Plastic':'plywood'})

# Observed independent white shop ceiling, tracks, downlights and cassette grilles.
for iz,z in enumerate([1.10,2.65,4.2,5.55]):
 for ix,x in enumerate([1.0,2.6,4.2,5.8,7.4]):
  cyl(f'RecessedDownlightTrim_{iz}_{ix}',x,z,3.025,.075,.025,'whiteink','Observed_Ceiling_Lights',source=('OCT26-11','OCT26-12'))
  cyl(f'RecessedDownlightGlow_{iz}_{ix}',x,z,3.010,.048,.006,'lamp','Observed_Ceiling_Lights',source=('OCT26-11','OCT26-12'))
for z in [1.45,4.65]:
 cube(f'WhiteTrack_{z}',W/2,z,2.95,[W-1.6,.035,.04],'ceiling','Observed_Ceiling_Lights',source=('OCT26-11',),collider=False)
 for i in range(5):cube(f'TrackSpot_{z}_{i}',1.3+i*1.4,z,2.86,[.085,.16,.09],'ceiling','Observed_Ceiling_Lights',source=('OCT26-11',),collider=False)
for i,(x,z) in enumerate([(2.5,4.5),(6.1,2.4)]):
 cube(f'CassetteACFrame_{i}',x,z,2.995,[.86,.075,.86],'ceiling','Source_Visible_Cassette_AC',source=('OCT26-11',),collider=False)
 for j in range(12):cube(f'ACGrille_{i}_{j}',x-.30+j*.055,z,2.951,[.015,.008,.58],'charcoal','Source_Visible_Cassette_AC',source=('OCT26-11',),collider=False)
for i,(x,z) in enumerate([(2.6,2),(5.6,2.7),(4.2,5.1)]):add('light',f'ShopLocalLight_{i}',x,z,2.85,'Observed_Ceiling_Lights',('OCT26-11',),color=[1,.95,.85,1],range=4,intensity=.7)

packet=dict(store_key='story-lamant',origin_q=list(OR),floor_world_y=7,source_packet=str(BASE.relative_to(ROOT)/'SOURCE-EVIDENCE-R25.json'),source_imports=imports,materials=materials,mesh_library=library,objects=objects)
(BASE/'STORE-CONSTRUCTION-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
sha=hashlib.sha256((BASE/'STORE-CONSTRUCTION-PACKET-R25.json').read_bytes()).hexdigest()
template=(BASE.parent/'StoreBuilderR25-v3-FREE-ASSETS.template.cs').read_text()
for a,b in {'__STORE_KEY__':'story-lamant','__WORKER_THREAD__':'classic:/root/r25_story_lamant','__EXACT_SCOPE__':'C180_R25_STORY_LAMANT_APPLY_001','__PACKET_PATH__':str(BASE/'STORE-CONSTRUCTION-PACKET-R25.json'),'__PACKET_SHA256__':sha}.items():template=template.replace(a,b)
(BASE/'ApplyStoreR25.cs').write_text(template)
print(json.dumps({'objects':len(objects),'imports':len(imports),'material_count':len(materials),'mesh_library_count':len(library),'packet_sha256':sha,'floor_changed':False,'text_objects':sum(x['kind']=='text' for x in objects),'free_model_instances':sum(x['kind']=='asset' for x in objects)},indent=2))
