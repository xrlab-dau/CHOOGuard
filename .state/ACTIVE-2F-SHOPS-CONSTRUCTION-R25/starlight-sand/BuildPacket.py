import pathlib,json,math,hashlib,shutil,datetime,cv2,numpy as np
from PIL import Image
P=pathlib.Path(__file__).resolve().parent;ROOT=P.parents[2];SRC=P/'sources';AS='Assets/ChooGuard/Art/StationInterior/R25_Shops/starlight-sand'
# Actual native support/wall-face axes. All dimensions below are explicitly MODEL.
s=.004884;c=math.sqrt(1-s*s);ang=math.degrees(math.asin(s));D0=-13.35928010314814;DMAX=D0+6
W0=39.53648433754995;W1=47.69365846223168;WC=(W0+W1)/2;W=W1-W0

def Q(d,w):return [round((D0+d)*c+(WC+w)*s,7),round(-(D0+d)*s+(WC+w)*c,7)]
M={};O=[];I=[];LIB={};used={}
def mat(n,color,**kw):M[n]={'color':color,**kw};return n
mat('navy',[.045,.064,.10,1],smoothness=.48);mat('frame',[.035,.041,.045,1],metallic=.65,smoothness=.62)
mat('wood',[.65,.46,.28,1],smoothness=.35);mat('wood_light',[.78,.60,.39,1],smoothness=.32);mat('wood_dark',[.43,.29,.17,1],smoothness=.32)
mat('glass',[.72,.85,.86,.12],transparent=True,smoothness=.95);mat('backglass',[.47,.61,.60,.19],transparent=True,smoothness=.84)
mat('steel',[.38,.4,.4,1],metallic=.9,smoothness=.66);mat('warmwhite',[.94,.89,.79,1],smoothness=.35)
mat('menuwhite',[1,1,1,1],smoothness=.22);mat('gold',[.73,.58,.23,1],metallic=.7,smoothness=.55)
mat('signlight',[.98,.98,.84,1],emission=[1.25,1.32,.94,1],smoothness=.62)
mat('signsage',[.42,.55,.26,1],emission=[.10,.14,.045,1],smoothness=.5)
mat('led',[1,.8,.39,1],emission=[2,1.35,.5,1],smoothness=.6)
colors={'navy':[.045,.075,.14,1],'red':[.63,.04,.10,1],'yellow':[.89,.79,.035,1],'green':[.025,.24,.18,1],'purple':[.31,.07,.37,1],'gray':[.31,.33,.30,1],'white':[.87,.85,.71,1]}
for k,col in colors.items():mat('carton_'+k,col,smoothness=.35);mat('foil_'+k,col,metallic=.55,smoothness=.57)

def imp(src,dst):
 src=pathlib.Path(src).resolve();I.append({'source':str(src),'destination':dst,'sha256':hashlib.sha256(src.read_bytes()).hexdigest()});return AS+'/'+dst

def box(id,d,w,y,dx,dy,dw,ma,group='Booth',collider=True):
 O.append({'kind':'box','id':id,'group':group,'q':Q(d,w),'y':y,'size':[dx,dy,dw],'yaw':ang,'material':ma,'collider':collider,'source_ids':['MINE_FRONT_20260906','WCW_FRONT_20261005'],'qualification':'PHOTO_REFERENCED_MODEL_INSIDE_NATIVE_FACE_DERIVED_WORK_REGION'})

def edge(id,d0,w0,d1,w1,bottom,height,t,ma,group='Booth',collider=True):
 O.append({'kind':'edge','id':id,'group':group,'a':Q(d0,w0),'b':Q(d1,w1),'bottom':bottom,'height':height,'thickness':t,'material':ma,'collider':collider,'source_ids':['MINE_FRONT_20260906','SITE_202605_RELATIONSHIP']})

def asset(id,k,d,w,y,fit,ma=None,group='DownloadedPhysicalModels',yaw=None,collider=False,map=None):
 o={'kind':'asset','id':id,'group':group,'q':Q(d,w),'y':y,'fit_size':fit,'yaw':(ang-90 if yaw is None else yaw),'asset':used[k]['asset'],'collider':collider,'source_ids':[used[k]['source_id'],'MINE_FRONT_20260906'],'qualification':'DOWNLOADED_SOURCE_MESH_PHOTO_ADAPTED_MODEL_NOT_EXACT_SKU_OR_SURVEY'}
 if map:o['material_map']=map
 elif ma:o['material_map']={'*':ma}
 else:o['material_map']=used[k]['map']
 O.append(o)

# PBR wood from directly downloaded CC0 PolyHaven source retained unchanged.
for n,f in [('texture_asset','diff.jpg'),('normal_asset','normal.jpg'),('metallic_gloss_asset','metallic_smoothness.png')]:
 a=imp(SRC/'wood-pbr'/f,'PBR/Wood/'+f)
 for k in ['wood','wood_light','wood_dark']:M[k][n]=a

# Normalized physical source derivatives and explicit URP slots, never source Standard rendering.
for k,filename,sourceid in [('financier','financier-own/financier-own.fbx','FREE_FIG_FINANCIER_ALBAN'),('cookie','cookie-own/cookie-own.fbx','FREE_COOKIE_ZIARA'),('carton','giftbox-own/giftbox-own.fbx','FREE_GIFTBOX_MULTIPAINKILLER'),('giftbox','giftbox/giftbox.fbx','FREE_GIFTBOX_MULTIPAINKILLER'),('display','displaycase/displaycase.fbx','FREE_DISPLAYCASE_MMCMINDES'),('wrapped','wrapped-biscuit/wrapped-biscuit.fbx','FREE_BISCUIT_HANNAH'),('counter','counter/counter-source-unity.fbx','FREE_BUN_COUNTER_SIYARASM')]:
 used[k]={'asset':imp(SRC/'models'/filename,'Models/'+filename),'source_id':sourceid,'map':{}}
# Financier scanned source baked diffuse.
ma=mat('financier_baked',[.95,.94,.91,1],smoothness=.34);M[ma]['texture_asset']=imp(SRC/'models/financier/textures/Image_0.png','PBR/Financier/albedo.png');used['financier']['map']={'material_0':ma,'*':ma}
# Cookie source maps, ORM to URP metallic/smoothness with roughness green preserved.
ma=mat('cookie_baked',[1,1,1,1],smoothness=.35);M[ma]['texture_asset']=imp(SRC/'models/cookie/textures/Image_0.png','PBR/Cookie/albedo.png');M[ma]['normal_asset']=imp(SRC/'models/cookie/textures/Image_2.png','PBR/Cookie/normal.png')
a=np.array(Image.open(SRC/'models/cookie/textures/Image_1.png').convert('RGB'));out=np.zeros((*a.shape[:2],4),dtype='uint8');out[:,:,0]=a[:,:,2];out[:,:,3]=255-a[:,:,1];Image.fromarray(out).save(SRC/'cookie-metallic_smoothness.png');M[ma]['metallic_gloss_asset']=imp(SRC/'cookie-metallic_smoothness.png','PBR/Cookie/metallic_smoothness.png');used['cookie']['map']={'lambert2':ma,'*':ma}
# Source display glass and base are distinct materialslots.
used['display']['map']={'R120G120B120':'glass','R127G127B127':'wood','Woods__Plastics.Finish_Carpentry.Wood.Pine.png':'wood_dark','*':'glass'}
# Actual industrial counter model, source material identities explicitly adapted to photo millwork.
used['counter']['map']={'*':'wood'}
used['wrapped']['map']={'*':'carton_navy'}
# POS, including source PBR maps and separate source normal slot.
pos=P.parent/'free-assets/pos-terminal';used['pos']={'asset':imp(pos/'POS-Terminal.fbx','Models/POS/POS-Terminal.fbx'),'source_id':'FREE_POS_VLADYSLAV_YAROVYI'}
ma=mat('pos_primary',[1,1,1,1],metallic=.25,smoothness=.65);M[ma]['texture_asset']=imp(pos/'textures/Image_0.png','PBR/POS/albedo.png');M[ma]['normal_asset']=imp(pos/'textures/Image_2.png','PBR/POS/normal.png')
a=np.array(Image.open(pos/'textures/Image_1.png').convert('RGB'));out=np.zeros((*a.shape[:2],4),dtype='uint8');out[:,:,0]=a[:,:,2];out[:,:,3]=255-a[:,:,1];Image.fromarray(out).save(SRC/'pos-metallic_smoothness.png');M[ma]['metallic_gloss_asset']=imp(SRC/'pos-metallic_smoothness.png','PBR/POS/metallic_smoothness.png')
mb=mat('pos_screen',[.025,.038,.05,1],metallic=.05,smoothness=.72);M[mb]['normal_asset']=imp(pos/'textures/Image_3.png','PBR/POS/screen_normal.png');used['pos']['map']={'Material_2100454184':ma,'Material_2100454185':mb,'*':ma}
# Actual paper menu source information on physical board; no equipment photo plane.
im=cv2.imread(str(SRC/'ROOT-mine6409-224437460852-01.jpg'));crop=im[174:402,55:900];cv2.imwrite(str(SRC/'actual-menu-print.png'),crop)
M['menuprint']={'color':[1,1,1,1],'smoothness':.2,'texture_asset':imp(SRC/'actual-menu-print.png','Prints/source-menu.png')}
# Architecture: existing native floor/column and access walls remain fixed. No floor overlay/cut.
# Source front fixed windows flank a central clear, opened sliding entrance.
for w in [-W/2+.045,W/2-.045]:
 box('FrontCornerPost_'+str(w),.04,w,1.71,.075,3.42,.075,'frame')
for w in [-1.62,1.62]:box('EntryJamb_'+str(w),.04,w,1.34,.075,2.68,.075,'frame')
edge('FrontHead',.04,-W/2+.05,.04,W/2-.05,3.32,.10,.10,'frame')
edge('UpperFrontGlazing',.04,-W/2+.07,.04,W/2-.07,2.72,.60,.014,'glass',collider=False)
# Fixed windows; central portal3.24m is completely free.
for side,lo,hi in [('R',-W/2+.07,-1.66),('L',1.66,W/2-.07)]:
 edge('FixedFrontGlass_'+side,.045,lo,.045,hi,.07,2.58,.014,'glass')
 edge('WindowBottomRail_'+side,.04,lo,.04,hi,0,.07,.065,'frame')
 edge('WindowHeadRail_'+side,.04,lo,.04,hi,2.65,.065,.065,'frame')
# Open sliding leaves retained against fixed windows, hardware separated.
for side,w in [('R',-2.42),('L',2.42)]:
 edge('OpenSlidingLeafGlass_'+side,.12,w-.73,.12,w+.73,.08,2.54,.012,'glass')
 for ww in [w-.75,w+.75]:box('SlidingLeafStile_'+side+str(ww),.12,ww,1.35,.045,2.64,.045,'frame')
 box('SlidingLeafHandle_'+side,.19,w+(.62 if side=='R' else -.62),1.12,.035,.42,.025,'steel',collider=False)
# Side glazing to source-visible6m booth depth. Native018 is10mm clear of outer partition innerface.
for side,w in [('R',-W/2+.04),('L',W/2-.04)]:
 edge('SideGlazing_'+side,.08,w,5.92,w,.06,3.26,.014,'glass')
 edge('SideBase_'+side,.08,w,5.92,w,0,.06,.065,'frame')
 edge('SideTop_'+side,.08,w,5.92,w,3.32,.08,.075,'frame')
 for j,d in enumerate([2,4,5.94]):box('SideMullion_'+side+str(j),d,w,1.69,.055,3.38,.055,'frame')
# Rear existing source black glazed grid; own booth ends atd6, leaves3m service/native-door corridor behind.
edge('RearGlazing',5.95,-W/2+.05,5.95,W/2-.05,.04,3.30,.012,'backglass')
for j in range(12):box('RearVerticalGlazingBar_'+str(j),5.95,-W/2+.07+j*(W-.14)/11,1.69,.05,3.38,.045,'frame')
for j,h in enumerate([.05,.95,1.86,2.62,3.35]):edge('RearHorizontalGlazingBar_'+str(j),5.95,-W/2+.05,5.95,W/2-.05,h,.045,.055,'frame')
# Native col018 remains fixed: avoid own roof slab over its footprint by constructing strips around it.
# Ceiling is own thin source feature, with explicit column cutout in plan, not a native/floor edit.
colwlo=46.603666007341886-WC;colwhi=47.60367837568196-WC;coldlo=-12.624280103148141-D0;coldhi=-11.224281156380162-D0
# rectangles partition own ceiling around actual1.4x1 column body;20mm joint gap.
for n,(d0,d1,w0,w1) in enumerate([(0,coldlo-.02,-W/2,W/2),(coldhi+.02,6,-W/2,W/2),(coldlo-.02,coldhi+.02,-W/2,colwlo-.02),(coldlo-.02,coldhi+.02,colwhi+.02,W/2)]):
 if d1>d0 and w1>w0:box('OwnCeilingWithFixedColumnGap_'+str(n),(d0+d1)/2,(w0+w1)/2,3.45,d1-d0,.08,w1-w0,'warmwhite')
# Observed wood feature wall/panels and editable vertical reclaimed planks.
for side,w,ds,de in [('L',W/2-.20,2.4,5.7),('R',-W/2+.20,2.8,5.75)]:
 for j in range(15):
  dd=ds+(de-ds)*(j+.5)/15;box('ReclaimedWood_'+side+str(j),dd,w,1.7,(de-ds)/15-.006,3.22,.035,['wood','wood_light','wood_dark'][j%3],group='SourceWoodMillwork')
# Real downloaded bakery cases, physical model base counter and POS.
for j,w in enumerate([.60,-1.55]):
 asset('DownloadedBakeryCase_'+str(j),'display',4.18,w,.13,[2.12,1.14,.90],group='SourceCounter',collider=True)
 box('CaseWoodPlinth_'+str(j),4.18,w,.08,.89,.16,2.1,'wood',group='SourceCounter')
 for k,h in enumerate([.44,.78,1.10]):
  box('CaseWarmLight_'+str(j)+'_'+str(k),3.76,w,h,.018,.012,2.04,'led',group='SourceCounter',collider=False)
 for ww in [w-1.04,w+1.04]:box('CaseMetalFrame_'+str(j)+str(ww),3.75,ww,.80,.025,.86,.025,'steel',group='SourceCounter',collider=False)
asset('DownloadedCounterPOS','counter',4.18,2.42,0,[1.20,.94,.86],group='SourceCounter',collider=True)
asset('DownloadedPOS','pos',4.12,2.38,.96,[.31,.27,.33],group='SourceCounter')
# Source downloaded counter units form sidewall merchandise base, adapted source millwork.
for side,w,dd,ln in [('L',3.40,2.48,2.65),('R',-3.40,1.60,1.85)]:
 asset('DownloadedSideCounter_'+side,'counter',dd,w,0,[ln,.88,.62],group='SideDisplay',yaw=ang,collider=True)
 # Own wood face planks remain directly editable, genuine source cladding around actual model.
 for j in range(13):box('SideCabinetWood_'+side+str(j),dd-ln/2+ln*(j+.5)/13,w+(-.32 if side=='L' else .32),.45,ln/13-.006,.86,.025,['wood','wood_light','wood_dark'][j%3],group='SideDisplay')
 for j,h in enumerate([.90,1.34,1.78]):
  box('DisplayShelf_'+side+str(j),dd,w,h,ln,.025,.66,'steel',group='SideDisplay')
  box('DisplayShelfLight_'+side+str(j),dd,w+(-.32 if side=='L' else .32),h-.025,ln-.03,.008,.012,'led',group='SideDisplay',collider=False)
# Rear source menu board physical support and actual source ink.
box('MenuPanelBacking',3.72,-.44,1.58,.04,.54,4.22,'menuwhite',group='SourceInformation',collider=False)
# Vertical front plane with source-image-left mapped local+X to preserve readability.
def printed(id,d,w,y,width,height,ma,group):
 O.append({'kind':'mesh','id':id,'group':group,'q':Q(d,w),'y':y,'yaw':ang-90,'vertices':[[width/2,-height/2,0],[-width/2,-height/2,0],[-width/2,height/2,0],[width/2,height/2,0]],'triangles':[0,2,1,0,3,2],'uv':[[0,0],[1,0],[1,1],[0,1]],'material':ma,'collider':False,'source_ids':['MINE_MENU_20260906'],'qualification':'ACTUAL_SOURCE_PRINT_ON_PHYSICAL_MENU_PANEL_NOT_EQUIPMENT_PROXY'})
printed('ActualMenuPrintedSurface',3.695,-.44,1.58,4.18,.50,'menuprint','SourceInformation')
# Downloaded scanned pastries and cookies on three real case tiers, source-visible arrangement.
for tier,yy in enumerate([.47,.80,1.10]):
 for j in range(26):
  w=-2.54+j*.155
  for row,dd in enumerate([3.92,4.18,4.43]):
   if tier==2 and j%4<3:
    asset('ScannedFinancier_'+str(tier)+'_'+str(j)+'_'+str(row),'financier',dd,w,yy,[.125,.035,.065],group='PhysicalPastries',yaw=ang)
   elif tier==2:
    asset('ScannedCookie_'+str(tier)+'_'+str(j)+'_'+str(row),'cookie',dd,w,yy,[.075,.020,.075],group='PhysicalPastries',yaw=ang)
   else:
    col=list(colors)[j%len(colors)];asset('RealWrapper_'+str(tier)+'_'+str(j)+'_'+str(row),'wrapped',dd,w,yy,[.13,.033,.065],ma='carton_'+col,group='PackagedGoods',yaw=ang-90)
# Actual packaging source shape (closed beveled base+lid) fitted to rectangular current gift cartons.
for side,w,dd,ln in [('L',3.38,2.48,2.65),('R',-3.38,1.60,1.85)]:
 for tier,h in enumerate([.92,1.36,1.80]):
  for j in range(9 if side=='L' else 7):
   d=dd-ln/2+.16+j*.27;col=list(colors)[(j+tier)%7]
   asset('GiftCarton_'+side+str(tier)+'_'+str(j),'carton',d,w,h,[.22,.20,.17],ma='carton_'+col,group='PhysicalGiftCartons',yaw=ang)
# Low front right rows of colored cylindrical/rectangular gifts; currentsource07 has repeated closed retail cartons.
for j,col in enumerate(colors):
 for row in range(3):
  asset('SourceGiftStack_'+str(j)+'_'+str(row),'carton',.77+row*.25,-2.0-j*.22,.91,[.20,.21,.15],ma='carton_'+col,group='PhysicalGiftCartons',yaw=ang)
# Ribbons on upper showcase giftpackaging as directlyvisible source, only7units.
for j,col in enumerate(colors):asset('RibbonGift_'+str(j),'giftbox',3.1-j*.34,3.40,2.17,[.19,.28,.16],map={'M_BBottom':'carton_'+col,'M_BTop':'carton_'+col,'M_Lines':'navy','*':'carton_'+col},group='PhysicalGiftCartons',yaw=ang)
# Source-shaped thick sign: separate navy fascia, green physical plates, actual Korean/star/English solids.
box('FasciaPhysicalPanel',.025,0,2.995,.16,.74,5.62,'navy',group='ActualSourceSolidSign')
for n,w,ma in [('logo',1.86,'signsage'),('english',-1.79,'signsage')]:box('SignBackplate_'+n,-.072,w,2.995,.052,.65 if n=='logo' else .56,.66 if n=='logo' else .88,ma,group='ActualSourceSolidSign',collider=False)
for name,w,y in [('logo',1.86,2.995),('korean',.20,2.995),('english',-1.79,2.995)]:
 solid=json.loads((P/('sign-'+name+'-solid.json')).read_text());LIB[name]={k:solid[k] for k in ['vertices','triangles']}
 O.append({'kind':'library_mesh','mesh_key':name,'id':'SourceContourClosedSolid_'+name,'group':'ActualSourceSolidSign','q':Q(-.108,w),'y':y,'yaw':ang-90,'material':'signlight','collider':False,'source_ids':['WCW_SIGN_20261005'],'qualification':'ACTUAL_SOURCE_GLYPH_OUTLINE_CLOSED_FRONT_BACK_SIDES_35MM_NO_TMP'})
for j,w in enumerate([-2.53,-1.1,1.1,2.53]):box('FasciaMountingStud_'+str(j),.13,w,2.99,.09,.05,.05,'steel',group='ActualSourceSolidSign',collider=False)
# Observed warm ceiling downlights and counter lamps; no scene-wide lighting changes.
for row,dd in enumerate([1.3,3.05,5.1]):
 for j,w in enumerate([-2.7,0,2.7]):
  # Source native column avoidance.
  if coldlo-.1<dd<coldhi+.1 and colwlo-.1<w<colwhi+.1:continue
  O.append({'kind':'cylinder','id':'DownlightTrim_'+str(row)+'_'+str(j),'group':'SourceLighting','q':Q(dd,w),'y':3.395,'size':[.115,.018,.115],'material':'steel','collider':False,'source_ids':['MINE_FRONT_20260906']})
  O.append({'kind':'cylinder','id':'DownlightLens_'+str(row)+'_'+str(j),'group':'SourceLighting','q':Q(dd,w),'y':3.37,'size':[.088,.007,.088],'material':'led','collider':False,'source_ids':['MINE_FRONT_20260906']})
  O.append({'kind':'light','id':'WarmDownlight_'+str(row)+'_'+str(j),'group':'SourceLighting','q':Q(dd,w),'y':3.30,'range':2.5,'intensity':.4,'color':[1,.86,.65,1],'source_ids':['MINE_FRONT_20260906']})
# Source acquisition credits accompany own runtime assets.
credits=[]
for k in ['displaycase','financier','cookieReal','giftbox','biscuit']:
 n={'cookieReal':'cookie'}.get(k,k);f=SRC/(n+'-AUTHOR-METADATA.json')
 if k=='cookieReal':f=SRC/'cookieReal-AUTHOR-METADATA.json'
 # cookieReal metadata uses cookieReal key, downloaded loop retained exactly.
 if not f.exists():continue
 d=json.loads(f.read_text());credits.append({'name':d.get('name'),'source_url':d.get('viewerUrl'),'author':d.get('user',{}).get('displayName'),'license':d.get('license'),'original_immutable':True,'exact_branch_SKU':False})
credits.append({'name':'POS terminal','author':'Vladyslav.Yarovyi','source_url':'https://sketchfab.com/3d-models/pos-terminal-91c277a25bc34c5bbd080cd1e74161cb','license':'CC-BY-4.0'})
credits.append({'name':'Industrial Kitchen Bun Counter','author':'siyarasm','source_url':'https://sketchfab.com/3d-models/industrial-kitchen-bun-counter-50a9dc05104048ffba83eca72a003873','license':'CC-BY-4.0'})
credits.append({'name':'wood_table_001 PBR','author':'PolyHaven','source_url':'https://polyhaven.com/a/wood_table_001','license':'CC0-1.0'})
(SRC/'ATTRIBUTION-R25.json').write_text(json.dumps(credits,ensure_ascii=False,indent=2));imp(SRC/'ATTRIBUTION-R25.json','ATTRIBUTION-R25.json')
packet={'store_key':'starlight-sand','origin_q':Q(3,0),'floor_world_y':7,'source_packet':str((P/'SOURCE-EVIDENCE-R25.json').relative_to(ROOT)),'materials':M,'mesh_library':LIB,'objects':O,'source_imports':I}
(P/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
# Structural basis is concrete actual-face construction, no colored-box metric or source-image scale.
anchorpath=P/'LOCAL-ACTUAL-CAD-REFERENCE-R25.json'
basis={'status':'MODEL_NATIVE_FACE_AND_SOURCE_RELATIONSHIP_DERIVED','coordinate_system':'q=[qx,-qy] metres, floorY7.0','source_reference':str(anchorpath.relative_to(ROOT)),'source_reference_sha256':hashlib.sha256(anchorpath.read_bytes()).hexdigest(),'actual_fixed_anchors':[{'id':'NATIVE_COL_018_2F_MODEL','object':'1922034296','d':[-12.624280103148141,-11.224281156380162],'w':[46.603666007341886,47.60367837568196]},{'id':'R11-WALL-FOOTPRINT-042_P1_1','object':'1201191874','selected_station_w':W0,'use':'extend actual outer face line, not raster box'},{'id':'R11-WALL-FOOTPRINT-043_P1_1','object':'1404385831','selected_station_w':W1,'use':'extend actual outer face line, not raster box'}],'d_axis':[c,-s],'w_axis':[s,c],'native_aligned_interval_d':[D0,DMAX],'native_aligned_interval_w':[W0,W1],'constructed_footprint_q':[Q(0,-W/2),Q(0,W/2),Q(6,W/2),Q(6,-W/2),Q(0,-W/2)],'width_MODEL_m':W,'depth_MODEL_m':6,'source_common_facade_relationship':'Original2026SITE4 shows separate Goods/Starlight/Olive booth fronts and rears on common collinear row; Goods nativeanchored exactfront/rearfacet endpoints are extended to Starlight actual sideface stations. Sharedrow correspondence remains MODEL, not Starlight surveyed dimensions. No33sqm/5500width transfer or sourcepixel metric.','railway_front_reference_q':[[-13.0762495,57.9845004],[-13.1030902,52.4845658]],'railway_rear_reference_q':[[-7.0763209,57.9552196],[-7.1031616,52.4552851]],'source_col018_consistency':'actualsupportinternalfrontsetback approximately.735m confirms commonfacade relationship only, not independent observed field measurement','native_col018_preserved':True,'own_ceiling_column_cutout':'actualfixedbody1.4x1.0+20mm joint gap','minimum_side_partition_col_gap_MODEL_m':.010,'native_floor_edit':False,'service_access_strip':'Entire rearband between own rear≈qx-7.13 and actualnativewall≈qx-4.1 remains open3m; right/native-green-door approach continues. SourceMay35 and SITE4.','adjacent_goods_gap_MODEL_m':4.76,'existing_native_enclosure':'R19matchingY8.4 has no own booth enclosure inselected bay; no existing official room walls duplicated. Existingnativecolumns remain in place.','not_qualified':['Exact measured Starlight dimensions','Hidden kitchen/BOH equipment or partition uses','Complete exact product SKU/tin artwork','Runtime/visual effectiveness/whole2Fcompletion']}
(P/'MODEL-PLACEMENT-BASIS-R25.json').write_text(json.dumps(basis,ensure_ascii=False,indent=2))
print('PACKET',len(O),'objects',sum(x['kind']=='asset' for x in O),'assetinstances',len(I),'imports',len(M),'URPmaterials',len(json.dumps(packet)))
