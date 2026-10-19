from pathlib import Path
import json,hashlib,math,sys,shutil
import cv2,numpy as np
BASE=Path('/Users/um-yunsang/CHOOGuard');COMMON=BASE/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25';OWN=COMMON/'cheongdo-gimbap';S=OWN/'sources';AS='Assets/ChooGuard/Art/StationInterior/R25_Shops/cheongdo-gimbap'
sys.path.insert(0,str(COMMON));from photo_outline_to_solid import closed_solid_from_mask
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
# Recompute current native anchors from complete R19 source segments, not old candidate coordinates.
cad=json.loads((OWN/'LOCAL-ACTUAL-CAD-REFERENCE-R25.json').read_text());native=next(o for o in cad['objects'] if o['object_id'].endswith('-127163917-0'))
segs=native['sections']['7.15000'];find=lambda a,b:next((s for s in segs if math.dist(s[0],a)<.005 and math.dist(s[1],b)<.005),None)
frontsegs=[s for s in native['sections']['8.40000'] if all(.3<v[0]<.4 and -42<v[1]<-34 for v in s)]
back=next(s for s in native['sections']['8.40000'] if math.dist(*s)>6 and all(4.68<v[0]<4.73 and -42<v[1]<-34 for v in s))
a=np.array([.34844904304402746,-40.4323142793503]);b=np.array([.349330895852102,-40.207635097933164]);split=(a+b)/2
frontdir=np.array([.003904,-.99999238]);inward=np.array([.99999238,.003904]);outward=-inward
# Fitout stays strictly inside the actual front/back faces; tenant divider is only a MODEL extension.
working=[[.48,-35.10],[4.61,-35.10],[4.59,-40.24],[.46,-40.22]]
basis={'store_key':'cheongdo-gimbap','status':'NATIVE_FACES_REUSED_PHOTO_FITOUT_MODEL','native_owner_object_id':native['object_id'],'native_path':native['path'],'cad_source':cad['source'],'cad_sha256':cad['source_sha256'],'actual_frontage_sections':frontsegs,'actual_backface':back,'individual_division_datum':{'native_adjacent_jamb_endpoints':[a.tolist(),b.tolist()],'midpoint':split.tolist(),'normal_into_room':inward.tolist(),'qualification':'MODEL tenant split from exact native adjacent jamb endpoints, not measured existing full partition'},'furniture_work_region_qx_minus_qy':working,'public_hall_outward_normal_q':outward.tolist(),'existing_native_front_opening_kept':[[.3656697307798602,-36.026096130278916],[.35366369,-39.099906]],'central_clear_route_MODEL':{'x':[.45,4.5],'z':[-38.55,-37.65],'clear_width_m':.9},'native_shell_reused':True,'new_floor_or_duplicate_structural_enclosure':False,'metric_fit':'All furnishing dimensions and source-to-native fit are MODEL; no field survey or exact SKU identity claimed','source_photo_dimensions_not_metric':True,'native_columns_floor_doors_circulation_unchanged':True}
(OWN/'MODEL-PLACEMENT-BASIS-R25.json').write_text(json.dumps(basis,ensure_ascii=False,indent=2))
imports=[]; imported={}
def imp(src,dest):
 src=Path(src).resolve();out=AS+'/'+dest
 if out not in imported:imports.append({'source':str(src),'destination':dest,'sha256':sha(src)});imported[out]=True
 return out
materials={
'charcoal':{'color':[.035,.033,.031,1],'metallic':.12,'smoothness':.35},
'stone':{'color':[.09,.09,.085,1],'smoothness':.48},
'blackmetal':{'color':[.018,.021,.022,1],'metallic':.75,'smoothness':.6},
'stainless':{'color':[.58,.60,.60,1],'metallic':.86,'smoothness':.62},
'wood':{'color':[.49,.35,.19,1],'smoothness':.32},
'woodlight':{'color':[.62,.47,.29,1],'smoothness':.34},
'woodshadow':{'color':[.16,.13,.09,1],'smoothness':.24},
'white':{'color':[.88,.87,.82,1],'smoothness':.53},
'signwhite':{'color':[1,.99,.95,1],'emission':[1.1,1.06,.98,1],'smoothness':.4},
'signred':{'color':[.78,.012,.035,1],'emission':[.38,.004,.01,1],'smoothness':.45},
'menuwhite':{'color':[.91,.91,.84,1],'emission':[.45,.44,.37,1]},
'ink':{'color':[.035,.037,.035,1],'smoothness':.3},
'ceiling':{'color':[.69,.67,.58,1],'smoothness':.22},
'downlight':{'color':[.95,.93,.80,1],'emission':[.8,.76,.6,1]},
'broth':{'color':[.49,.42,.18,1],'smoothness':.8},
'noodle':{'color':[.88,.76,.43,1],'smoothness':.45},
'nori':{'color':[.022,.038,.018,1],'smoothness':.5},
'rice':{'color':[.91,.89,.77,1],'smoothness':.3},
'carrot':{'color':[.91,.34,.035,1],'smoothness':.4},
'vegetable':{'color':[.15,.37,.055,1],'smoothness':.4},
'plate':{'color':[.38,.72,.015,1],'smoothness':.75},
'sesame':{'color':[.72,.57,.3,1],'smoothness':.2},
'fishcake':{'color':[.92,.76,.64,1],'smoothness':.4},
'fishpink':{'color':[.85,.23,.37,1],'smoothness':.4},
'glass':{'color':[.66,.75,.76,.15],'transparent':True,'smoothness':.93},
'screen':{'color':[.018,.028,.032,1],'emission':[.025,.04,.045,1],'smoothness':.75},
}
objs=[]
def add(kind,id,q,y,**kw):
 d={'kind':kind,'id':id,'q':list(q),'y':y,'group':kw.pop('group','Interior'),'source_ids':kw.pop('source_ids',['JUNE26-01']),'collider':kw.pop('collider',False)};d.update(kw);objs.append(d);return d
def box(id,q,y,size,mat,**kw):return add('box',id,q,y,size=size,material=mat,**kw)
def asset(id,file,q,y,size,map,**kw):return add('asset',id,q,y,asset=file,fit_size=size,material_map=map,**kw)
def mesh(id,data,q,y,mat,yaw=0,**kw):return add('mesh',id,q,y,vertices=data['vertices'],triangles=data['triangles'],material=mat,yaw=yaw,**kw)
# Downloaded model imports; source files and license/acquisition metadata retained in own stage/runtime.
chair=imp(S/'models/ladder-chair/LadderChair.fbx','Models/LadderChair.fbx')
ct=imp(S/'models/counter/counter-source-unity.fbx','Models/ServiceCounter.fbx')
table=imp(S/'models/cafe-table/CafeTableSteelBase.fbx','Models/TableSteelBase.fbx')
mon=imp(S/'models/pos-monitor/Monitor.fbx','Models/POSMonitor.fbx')
fridge=imp(S/'models/commercial-refrigerator/CommercialRefrigerator-Empty.fbx','Models/Refrigerator.fbx')
ken=COMMON/'free-assets/kenney-food-kit/Models/FBX format'
foods={n:imp(ken/(n+'.fbx'),'Models/Food/'+n+'.fbx') for n in ['bowl','plate-dinner','plate-rectangle','rice-ball','chopstick','utensil-spoon','cooking-knife','pan','pot','bottle-ketchup','cutting-board-japanese']}
# Branded fascia is its own source-shape physical assembly; front +Z is rotated to public-hall -qx.
front_yaw=-90.22368
box('SOURCE_BLACK_FASCIA_PANEL',[.315,-37.67],3.16,[.105,.77,5.16],'charcoal',group='PhysicalSign',collider=True)
box('FASCIA_TOP_CAP',[.30,-37.67],3.565,[.13,.045,5.21],'blackmetal',group='PhysicalSign')
box('FASCIA_BOTTOM_LIP',[.30,-37.67],2.762,[.13,.025,5.20],'blackmetal',group='PhysicalSign')
# Extract actual source-outline letter/logo shapes; no fonts, TMP or photograph billboard.
im=cv2.imread(str(S/'ROOT-kde2239-224300910098-01.jpg'));quad=np.float32([[237,246],[780,294],[780,407],[237,359]]);dst=np.float32([[0,0],[899,0],[899,199],[0,199]])
rect=cv2.warpPerspective(im,cv2.getPerspectiveTransform(quad,dst),(900,200));hsv=cv2.cvtColor(rect,cv2.COLOR_BGR2HSV)
white=cv2.inRange(hsv,np.array([0,0,200]),np.array([179,95,255]));white[:5]=0;white[-8:]=0;white[:,:9]=0;white[:,-9:]=0
red=cv2.inRange(hsv,np.array([0,120,100]),np.array([15,255,255]))|cv2.inRange(hsv,np.array([166,100,100]),np.array([179,255,255]));red[:,145:]=0
cv2.imwrite(str(S/'SOURCE-SIGN-RECTIFIED.png'),rect);cv2.imwrite(str(S/'SOURCE-SIGN-WHITE-MASK.png'),white);cv2.imwrite(str(S/'SOURCE-SIGN-RED-MASK.png'),red)
for label,mask,mat,depth in [('WHITE_ACTUAL_LETTER_COUNTERS',white,'signwhite',.033),('RED_SOURCE_SEAL',red,'signred',.038)]:
 sol=closed_solid_from_mask(mask,3.02,.625,depth=depth,minimum_area=12,epsilon=.45);mesh('SIGN_'+label,sol,[.247,-37.67],3.16,mat,yaw=front_yaw,group='PhysicalSign');(S/('SIGN-'+label+'-SOLID.json')).write_text(json.dumps({'photo':str(S/'ROOT-kde2239-224300910098-01.jpg'),'source_quad':quad.tolist(),'physical_width_m':3.02,'physical_height_m':.625,'polygons':sol['source_polygons'],'vertices':len(sol['vertices']),'font_used':False,'holes_retained':True},indent=2))
# Warm interior lining attached to fixed native rear face only; no duplicate native wall or floor.
back0=np.array(back[0]);back1=np.array(back[1]);bd=(back1-back0)/np.linalg.norm(back1-back0)
def back_x(z):return float(back0[0]+(z-back0[1])*(back1[0]-back0[0])/(back1[1]-back0[1]))
for i,z in enumerate(np.arange(-40.18,-35.18,.075)):
 box(f'REAR_TIMBER_BATTEN_{i:02}',[back_x(z)-.047,float(z)],1.38,[.034,2.76,.044],'woodlight',group='NativeRearFaceLining')
box('REAR_BATTEN_SHADOW_BACKER',[back_x(-37.67)-.02,-37.67],1.36,[.018,2.72,5.0],'woodshadow',group='NativeRearFaceLining')
# Source low suspended ceiling, nonstructural fitout held clear of all fixed native mesh.
box('SOURCE_SUSPENDED_CEILING',[2.55,-37.68],2.805,[4.08,.055,5.10],'ceiling',group='CeilingFitout')
for ix,x in enumerate([1.05,2.35,3.7]):
 for iz,z in enumerate([-36.45,-37.7,-39.0]):
  add('cylinder',f'PHYSICAL_DOWNLIGHT_RIM_{ix}_{iz}',[x,z],2.765,size=[.155,.012,.155],material='stainless',group='CeilingFixtures')
  add('cylinder',f'PHYSICAL_DOWNLIGHT_DIFFUSER_{ix}_{iz}',[x,z],2.748,size=[.125,.009,.125],material='downlight',group='CeilingFixtures')
# Only photographed visible service strip: actual free lower counter hardware, black photo facade, prep surface.
asset('DOWNLOADED_SERVICE_COUNTER',ct,[2.52,-35.86],0,[2.62,.89,.58],{'*':'stainless'},group='Service',source_ids=['JUNE26-02'],collider=True)
box('COUNTER_SOURCE_CHARCOAL_FACE',[2.52,-36.17],.435,[2.63,.87,.035],'charcoal',group='Service',source_ids=['JUNE26-02'],collider=True)
box('COUNTER_DARK_STONE_TOP',[2.52,-35.87],.925,[2.72,.055,.69],'stone',group='Service',source_ids=['JUNE26-02'],collider=True)
box('VISIBLE_PASS_SHELF',[2.33,-35.30],1.36,[2.15,.035,.32],'stainless',group='SourceVisiblePrep',source_ids=['JUNE26-02'])
# Menu in physical frame; all text is source contour print solids, never live font/text/photo plane.
box('MENU_PHYSICAL_FRAME',[2.52,-35.205],2.22,[2.92,.86,.065],'blackmetal',group='PhysicalMenu',source_ids=['JUNE26-02'])
box('MENU_PHYSICAL_LUMINOUS_PANEL',[2.52,-35.246],2.22,[2.82,.76,.018],'menuwhite',group='PhysicalMenu',source_ids=['JUNE26-02'])
im2=cv2.imread(str(S/'ROOT-kde2239-224300910098-02.jpg'));quad2=np.float32([[99,386],[780,450],[774,641],[84,609]]);r2=cv2.warpPerspective(im2,cv2.getPerspectiveTransform(quad2,np.float32([[0,0],[1199,0],[1199,359],[0,359]])),(1200,360));gray=cv2.cvtColor(r2,cv2.COLOR_BGR2GRAY);mask=np.where(gray<185,255,0).astype(np.uint8);mask[:16]=0;mask[-16:]=0;mask[:,:20]=0;mask[:,-20:]=0
cv2.imwrite(str(S/'SOURCE-MENU-RECTIFIED.png'),r2);cv2.imwrite(str(S/'SOURCE-MENU-PRINT-MASK.png'),mask);solid=closed_solid_from_mask(mask,2.78,.72,depth=.0012,minimum_area=3,epsilon=.2);mesh('MENU_SOURCE_PRINT_SOLID',solid,[2.52,-35.261],2.22,'ink',yaw=180,group='PhysicalMenu',source_ids=['JUNE26-02'])
# POS hardware real downloaded monitor, plus separate handheld card reader source.
asset('DOWNLOADED_POS_MONITOR',mon,[3.46,-35.98],.955,[.45,.36,.19],{'*':'blackmetal','Display':'screen'},group='Checkout',yaw=180,source_ids=['JUNE26-02'])
card=imp(S/'models/pos-terminal/POS-Terminal.fbx','Models/CardTerminal.fbx');asset('DOWNLOADED_CARD_TERMINAL',card,[3.07,-36.07],.958,[.105,.06,.19],{'*':'blackmetal'},group='Checkout',source_ids=['JUNE26-02'])
# Two source free refrigerator bodies represent photographed double stainless cold doors; glass/drink-brand material removed.
frmap={'*':'stainless','FridgeCase':'stainless','FridgeGlass':'stainless','FridgeLogo':'stainless','FridgeHandle':'blackmetal'}
for i,x in enumerate([3.65,4.21]):asset('DOWNLOADED_VISIBLE_COLD_DOOR_'+str(i),fridge,[x,-35.42],.02,[.53,1.9,.54],frmap,group='SourceVisiblePrep',yaw=180,source_ids=['JUNE26-02'],collider=True)
# Source bowls and smallwares visible in service opening.
for i,x in enumerate([1.57,1.88,2.19]):
 for k in range(5):asset(f'WHITE_BOWL_STACK_{i}_{k}',foods['bowl'],[x,-35.30],1.39+k*.033,[.19,.065,.19],{'*':'white'},group='SourceVisiblePrep',source_ids=['JUNE26-02'])
for i in range(5):asset(f'GREEN_PLATES_STACK_{i}',foods['plate-dinner'],[2.71,-35.37],.97+i*.012,[.21,.02,.21],{'*':'plate'},group='SourceVisiblePrep',source_ids=['JUNE26-02'])
asset('METAL_MIXING_BOWL',foods['bowl'],[2.15,-35.59],.955,[.24,.12,.24],{'*':'stainless'},group='SourceVisiblePrep',source_ids=['JUNE26-02'])
asset('SOURCE_VISIBLE_PREP_PAN',foods['pan'],[1.49,-35.62],.94,[.28,.11,.34],{'*':'stainless'},group='SourceVisiblePrep',source_ids=['JUNE26-02'])
asset('SOURCE_VISIBLE_PREP_POT',foods['pot'],[1.85,-35.47],.96,[.22,.2,.26],{'*':'stainless'},group='SourceVisiblePrep',source_ids=['JUNE26-02'])
asset('CONDIMENT_SQUEEZE_BOTTLE',foods['bottle-ketchup'],[2.78,-35.30],1.39,[.055,.22,.055],{'*':'carrot'},group='SourceVisiblePrep',source_ids=['JUNE26-02'])
asset('PREP_CUTTING_BOARD',foods['cutting-board-japanese'],[2.70,-35.66],.96,[.30,.02,.20],{'*':'woodlight'},group='SourceVisiblePrep',source_ids=['JUNE26-02'])
asset('PREP_KNIFE',foods['cooking-knife'],[2.75,-35.66],.98,[.27,.023,.04],{'*':'stainless'},group='SourceVisiblePrep',source_ids=['JUNE26-02'])
# Four photo-type tables/eight downloaded ladder-back chairs, central entry and through route left open.
tables=[(1.44,-36.85),(3.42,-36.85),(1.44,-39.22),(3.42,-39.22)]
for i,(x,z) in enumerate(tables):
 asset(f'DOWNLOADED_TABLE_BASE_{i}',table,[x,z],0,[.66,.705,.61],{'*':'blackmetal'},group='Dining',collider=True)
 box(f'LIGHTWOOD_TABLE_TOP_{i}',[x,z],.735,[.78,.055,.71],'woodlight',group='Dining',collider=True)
 for side,dx,yaw in [('FRONT',-.57,90),('REAR',.57,-90)]:asset(f'DOWNLOADED_LADDER_CHAIR_{i}_{side}',chair,[x+dx,z],0,[.43,.84,.46],{'*':'wood'},group='Dining',yaw=yaw,collider=True)
# Nori open triangular sleeve matches source05, downloaded rice-ball supplies inner rice geometry.
def sleeve():
 verts=[];tris=[];cross=[[-.031,0],[.031,0],[0,.054]]
 for i in range(3):
  a=cross[i];b=cross[(i+1)%3];k=len(verts);verts.extend([[a[0],a[1],-.076],[b[0],b[1],-.076],[b[0],b[1],.076],[a[0],a[1],.076]]);tris.extend([k,k+1,k+2,k,k+2,k+3,k+2,k+1,k,k+3,k+2,k])
 return {'vertices':verts,'triangles':tris}
for seti,(x,z) in enumerate([tables[0],tables[3]]):
 group='PhotoFoodSet_'+str(seti);asset(f'GRAY_SERVING_TRAY_{seti}',foods['plate-rectangle'],[x,z],.767,[.49,.025,.36],{'*':'stone'},group=group,source_ids=['JUNE26-03'])
 asset(f'GREEN_OVAL_GIMBAP_PLATE_{seti}',foods['plate-dinner'],[x-.12,z],.792,[.18,.023,.29],{'*':'plate'},group=group,source_ids=['JUNE26-03','JUNE26-05'])
 for gi,dx in enumerate([-.155,-.088]):
  q=[x+dx,z];asset(f'DOWNLOADED_RICE_TRIANGLE_{seti}_{gi}',foods['rice-ball'],q,.819,[.055,.048,.145],{'*':'rice'},group=group,source_ids=['JUNE26-05'])
  mesh(f'SOURCE_NORI_TRIANGULAR_SLEEVE_{seti}_{gi}',sleeve(),q,.818,'nori',group=group,source_ids=['JUNE26-05'])
  for end,zz in enumerate([-.077,.077]):
   add('sphere',f'CARROT_FILL_{seti}_{gi}_{end}',[q[0]-.006,z+zz],.84,size=[.015,.012,.007],material='carrot',group=group,source_ids=['JUNE26-05'])
   add('sphere',f'VEGETABLE_FILL_{seti}_{gi}_{end}',[q[0]+.007,z+zz],.837,size=[.014,.012,.007],material='vegetable',group=group,source_ids=['JUNE26-05'])
  for si in range(10):
   zz=-.06+.012*si;add('sphere',f'SESAME_{seti}_{gi}_{si}',[q[0]-.009+(si%3)*.008,z+zz],.863-(si%3)*.002,size=[.0028,.0015,.0048],material='sesame',group=group,source_ids=['JUNE26-05'])
 asset(f'BLACK_UDON_BOWL_{seti}',foods['bowl'],[x+.11,z+.015],.793,[.25,.112,.25],{'*':'charcoal'},group=group,source_ids=['JUNE26-03','JUNE26-04'])
 add('cylinder',f'BROTH_SURFACE_{seti}',[x+.11,z+.015],.880,size=[.215,.002,.215],material='broth',group=group,source_ids=['JUNE26-04'])
 for ni in range(6):
  # curved source-noodle lengths, physical solid swept tubes
  verts=[];tris=[]
  for a in range(15):
   ang=.15+a/14*math.pi*1.5;r=.018+ni*.009
   for k in range(6):
    th=k/6*math.tau;verts.append([math.cos(ang)*r+math.cos(th)*.003,.002+math.sin(th)*.003,math.sin(ang)*r])
  for a in range(14):
   for k in range(6):
    v=a*6+k;w=a*6+(k+1)%6;tris.extend([v,w,w+6,v,w+6,v+6])
  mesh(f'UDON_NOODLE_{seti}_{ni}',{'vertices':verts,'triangles':tris},[x+.11,z+.015],.883,'noodle',group=group,source_ids=['JUNE26-04'])
 add('sphere',f'FISHCAKE_SLICE_{seti}',[x+.14,z+.04],.890,size=[.052,.006,.034],material='fishcake',group=group,source_ids=['JUNE26-04'])
 add('sphere',f'FISHCAKE_PINK_{seti}',[x+.14,z+.04],.895,size=[.025,.003,.013],material='fishpink',group=group,source_ids=['JUNE26-04'])
 for hi in range(5):add('sphere',f'GREEN_GARNISH_{seti}_{hi}',[x+.07+hi*.012,z+.015+(hi%2)*.025],.89,size=[.012,.006,.025],material='vegetable',group=group,source_ids=['JUNE26-04'])
 for ci,xx in enumerate([-.011,.003]):asset(f'DOWNLOADED_CHOPSTICK_{seti}_{ci}',foods['chopstick'],[x+xx,z-.14],.8,[.008,.008,.24],{'*':'charcoal'},group=group,source_ids=['JUNE26-03'])
 asset(f'DOWNLOADED_SOUP_SPOON_{seti}',foods['utensil-spoon'],[x+.035,z-.12],.801,[.035,.015,.19],{'*':'charcoal'},group=group,source_ids=['JUNE26-03'])
ac=imp(S/'models/wall-ac/WallAC.fbx','Models/WallAC.fbx')
asset('DOWNLOADED_REAR_WALL_SPLIT_AC',ac,[4.48,-37.48],2.12,[.98,.31,.23],{'*':'white'},yaw=-90,group='PhotoVisibleEquipment',source_ids=['JUNE26-01'])
# Hidden kitchen, unobserved rice cookers and concealed equipment are explicit gaps.
credit='Downloaded model sources and fit qualifications\n\nPolyHaven painted_wooden_chair_02, CC0, https://polyhaven.com/a/painted_wooden_chair_02; warm material adaptation.\nService counter by siyarasm, CC-BY4.0, https://sketchfab.com/3d-models/industrial-kitchen-bun-counter-50a9dc05104048ffba83eca72a003873; source-derived lower counter, own charcoal facade.\nTable for cafes and bars by Andy.Honor, CC-BY4.0, https://sketchfab.com/3d-models/table-for-cafes-and-bars-b405587c16bf4851921f0620c4c98998; source base plus photo-matched top.\nComputer Monitor Lowpoly Model by Marco Zakaria, CC-BY4.0, https://sketchfab.com/3d-models/computer-monitor-lowpoly-model-1d4ee42eda804b02b05b63721921c1d7; hardware adaptation not exact POS SKU.\nPOS terminal by Vladyslav.Yarovyi, CC-BY4.0, https://sketchfab.com/3d-models/pos-terminal-91c277a25bc34c5bbd080cd1e74161cb; card-reader form only.\nCommercial Fridge by Sean Thomas / Eric Chadwick, Darmstadt Graphics Group and Khronos contributors, CC-BY4.0, https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/CommercialRefrigerator; metallic opaque material adaptation to source-visible cold doors, original glass-brand texture unused.\nWall AC by Daniyal Malik CC-BY4.0 https://sketchfab.com/3d-models/air-conditioner-777cbe0d26aa4f1588050419ec24f4e4; white source-photo material adaptation, exact SKU unknown.\nKenney Food Kit CC0 https://kenney.nl/assets/food-kit; plate, bowl, rice-ball, chopsticks, spoon, knife, pan, pot, cutting board, bottle. All dimensions / models are unmeasured photo-referenced MODEL fit, not exact SKU or as-built measurement.\n'
(S/'MODEL-CREDITS.txt').write_text(credit);imp(S/'MODEL-CREDITS.txt','MODEL-CREDITS.txt')
packet={'store_key':'cheongdo-gimbap','origin_q':[.36,-37.67],'floor_world_y':7,'source_packet':str(OWN.relative_to(BASE)/'SOURCE-EVIDENCE-R25.json'),'materials':materials,'source_imports':imports,'objects':objs,'all_dimensions':'MODEL_NOT_FIELD_SURVEY','native_reuse':True,'no_scene_lights':True}
(OWN/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
print('packet objects',len(objs),'source assets',sum(x['kind']=='asset' for x in objs),'imports',len(imports),'menuvertices',len(solid['vertices']))
print('sha256',sha(OWN/'STORE-PACKET-R25.json'))
