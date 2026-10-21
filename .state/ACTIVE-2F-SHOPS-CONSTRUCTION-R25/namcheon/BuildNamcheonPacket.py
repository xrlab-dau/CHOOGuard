import json,pathlib,hashlib,sys,math,shutil,cv2,numpy as np,datetime
P=pathlib.Path(__file__).resolve().parent; B=P.parent; S=P/'sources';A='Assets/ChooGuard/Art/StationInterior/R25_Shops/namcheon';sys.path.insert(0,str(B));from photo_outline_to_solid import closed_solid_from_mask
sha=lambda p:hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
D=json.loads((P/'LOCAL-ACTUAL-CAD-REFERENCE-R25.json').read_text()); T=np.array([.999988,-.004886]);T=T/np.linalg.norm(T); N=np.array([-T[1],T[0]])
get=lambda label:next(o for o in D['objects'] if label in o['path'])
# The direction comes from actual repeated wall060 face segments; endpoints unchanged.
wleft=get('FOOTPRINT-060');wright=get('FOOTPRINT-061_P1');wback=get('FOOTPRINT-061_P2')
pts=lambda o:np.array(o['sections']['8.40000']).reshape(-1,2)
left=float(max(pts(wleft)@T));right=float(min(pts(wright)@T));front=float(min(max(pts(wleft)@N),max(pts(wright)@N)));back=float(max(pts(wback)@N));W=right-left;DEP=front-back
# u runs source-image right to left (+T); d extends inward from hall facade (-N).
def q(u,d):return list((T*(left+u)+N*(front-d)).round(6))
yaw=math.degrees(math.atan2(N[0],N[1]));O=[];M={};imports=[];LIB={};credits=[]
def mat(k,col,metal=0,smooth=.4,**kw):M[k]={'color':list(col),'metallic':metal,'smoothness':smooth,**kw}
mat('fascia',[.034,.04,.045,1],.12,.38);mat('frame',[.026,.031,.035,1],.55,.56);mat('letter',[.91,.94,.97,1],.12,.65,emission=[.13,.15,.17,1]);mat('letter_side',[.65,.70,.73,1],.45,.35);mat('white',[.91,.90,.86,1],.05,.58);mat('brick',[.76,.75,.69,1],0,.29);mat('brick_alt',[.83,.81,.75,1],0,.3);mat('mortar',[.48,.46,.42,1],0,.2);mat('bench',[.032,.062,.10,1],0,.34);mat('benchseat',[.09,.12,.15,1],0,.45);mat('steel',[.48,.53,.53,1],.86,.7);mat('chair',[.67,.72,.67,1],.45,.58);mat('chair_white',[.81,.83,.78,1],.4,.65);mat('wood',[.52,.36,.21,1],0,.32);mat('red',[.54,.045,.025,1],0,.47);mat('ink',[.028,.025,.021,1],0,.3);mat('yellow',[.94,.64,.015,1],0,.42);mat('glass',[.68,.80,.81,.10],.05,.92,transparent=True);mat('bulb',[.95,.43,.04,1],.05,.65,emission=[1.4,.48,.04,1]);mat('sauce',[.38,.025,.007,1],.03,.92);mat('tteok',[.65,.09,.015,1],0,.76);mat('fishcake',[.43,.15,.038,1],0,.61);mat('terminal',[.025,.029,.032,1],.25,.45);mat('screen',[.12,.17,.15,1],.1,.85,emission=[.03,.045,.035,1]);mat('bowl',[.018,.021,.022,1],.06,.8)
def obj(kind,id,u,d,y,group,material=None,**kwargs):
 a={'kind':kind,'id':id,'group':group,'q':q(u,d),'y':y,'yaw':yaw,'source_ids':['SEP30-FRONT','OCT06-FRONT'],'qualification':'SOURCE_VISIBLE_FAMILY_AND_PHOTO_FITTED_MODEL_NOT_MEASURED',**kwargs}
 if material:a['material']=material
 O.append(a);return a
def box(id,u,d,y,size,ma,group='PhysicalDetails',**kw):return obj('box',id,u,d,y,group,ma,size=size,**kw)
def cyl(id,u,d,y,diam,height,ma,group='PhysicalDetails',**kw):return obj('cylinder',id,u,d,y,group,ma,size=[diam,height/2,diam],**kw)
def asset(id,file,u,d,y,fit,group,material_map,angle=0,**kw):return obj('asset',id,u,d,y,group,asset=A+'/'+file,fit_size=fit,material_map=material_map,yaw=yaw+angle,**kw)
def imp(file,dest):
 file=pathlib.Path(file);imports.append({'source':str(file),'destination':dest,'sha256':sha(file)})
 return dest
kioskfile=imp(S/'models/084a535f5fb542c99eb3564a28195e3e/PayStation-Kiosk.fbx','Models/PayStation-Kiosk.fbx')
chairfile=imp(S/'models/3fdcc5bc702d40f6bfee171e7046e32d/Tolix-Chair.fbx','Models/Tolix-Chair.fbx')
tablefile=imp(S/'models/table/CafeTableSteelBase.fbx','Models/CafeTableSteelBase.fbx');counterfile=imp(S/'models/counter/counter-source-unity.fbx','Models/ServiceCounter.fbx');posfile=imp(S/'models/pos/POS-Terminal.fbx','Models/POS-Terminal.fbx')
food=B/'free-assets/kenney-food-kit/Models/FBX format'
for nm in ['plate','bowl','sausage-half','utensil-spoon','cup']:imp(food/(nm+'.fbx'),'Models/Food/'+nm+'.fbx')
shutil.copy2(B/'free-assets/kenney-food-kit/License.txt',S/'models/Kenney-License.txt')
# Reuse original side/back enclosure; only source surface finishes and front assembly added.
frontd=.13;clear=(4.05,5.25);height=2.47
for a,b in [(0.08,clear[0]),(clear[1],W-.08)]:
 box('FixedGlass_%s'%a,(a+b)/2,frontd,1.28,[b-a,2.34,.015],'glass','Frontage',collider=True)
 box('GlassBottomRail_%s'%a,(a+b)/2,frontd,.095,[b-a,.13,.065],'frame','Frontage')
 box('GlassTopRail_%s'%a,(a+b)/2,frontd,height,[b-a,.065,.065],'frame','Frontage')
 # Thin frosted band across non-door glazing.
 box('EtchedBand_%s'%a,(a+b)/2,frontd-.013,1.06,[b-a,.085,.004],'white','Frontage',collider=False)
for u in [.06,1.45,2.88,clear[0],clear[1],6.55,W-.06]:box('FrontMullion_'+str(u),u,frontd,1.28,[.055,2.52,.085],'frame','Frontage')
# Entry remains open at original source central glass door. Open leaf is parked inside own zone.
# Leaf and hardware correspond to photo shown opening and do not obstruct clear corridor.
leafu=clear[1]-.06;leafd=.72
lf=box('OpenGlassDoor',leafu,leafd,1.23,[1.10,2.37,.014],'glass','Entry',yaw=yaw+88,collider=False)
for dd in [.19,1.26]:box('OpenDoorUpright_'+str(dd),leafu,dd,1.23,[.045,2.43,.045],'frame','Entry')
box('DoorHandle',leafu-.045,.48,1.08,[.035,.35,.03],'steel','Entry',collider=False)
box('FasciaBacking',W/2,.11,2.84,[W-.04,.69,.19],'fascia','Signs')
box('FasciaTopLip',W/2,.095,3.195,[W-.04,.035,.235],'frame','Signs')
# Physical white raised brand solids from original source photograph masks.
cont=S/'contours';brand=cv2.imread(str(cont/'brand-white-mask.png'),0)
# Seven photo letter cells preserve all counters; no font or Text/TMP used.
for i,(a,b) in enumerate([(0,147),(148,307),(307,446),(446,602),(602,737),(737,872),(872,1005)]):
 mask=np.zeros_like(brand);mask[:,a:b]=brand[:,a:b];m=closed_solid_from_mask(mask,3.45,.47,.058,minimum_area=7,epsilon=.55)
 obj('mesh','BrandSourceLetter_%02d'%i,4.40,.001,2.85,'Signs','letter',vertices=m['vertices'],triangles=m['triangles'],collider=False,source_ids=['SEP30-FRONT-CONTOUR'])
box('SealPhysicalFrame',6.56,.006,2.86,[.47,.61,.08],'frame','Signs',collider=False);box('SealWhiteEnamel',6.56,-.038,2.86,[.43,.57,.018],'white','Signs',collider=False)
for nm,ma in [('seal-red-mask.png','red'),('seal-black-mask.png','ink')]:
 mask=cv2.imread(str(cont/nm),0)
 if ma=='ink':mask[:35]=0
 m=closed_solid_from_mask(mask,.40,.55,.008,minimum_area=5,epsilon=.55);obj('mesh','SealContour_'+ma,6.56,-.052,2.86,'Signs',ma,vertices=m['vertices'],triangles=m['triangles'],collider=False,source_ids=['SEP07-SEAL-CONTOUR'])
# Actual source menu ink and fields become closed physical ink meshes on metal plate.
im=cv2.imread(str(S/'ROOT-wdefs420-224427350117-01.jpg'));H=cv2.getPerspectiveTransform(np.float32([[8,40],[882,65],[882,442],[8,442]]),np.float32([[0,0],[1049,0],[1049,459],[0,459]]));im=cv2.warpPerspective(im,H,(1050,460));cv2.imwrite(str(cont/'menu-rectified.png'),im);hs=cv2.cvtColor(im,cv2.COLOR_BGR2HSV)
black=np.uint8((hs[:,:,2]<155)&((hs[:,:,1]<145)|(hs[:,:,2]<85)))*255;yellow=np.uint8((hs[:,:,0]>14)&(hs[:,:,0]<40)&(hs[:,:,1]>115)&(hs[:,:,2]>120))*255;red=np.uint8(((hs[:,:,0]<12)|(hs[:,:,0]>173))&(hs[:,:,1]>90)&(hs[:,:,2]>90))*255
for a in [black,yellow,red]:a[:4]=0;a[-4:]=0;a[:,:4]=0;a[:,-4:]=0
box('MenuBlackFrame',6.52,.18,2.02,[2.32,1.07,.065],'frame','Menu')
box('MenuEnamelFace',6.52,.14,2.02,[2.26,1.015,.018],'white','Menu',collider=False)
for nm,mask,ma in [('black',black,'ink'),('yellow',yellow,'yellow'),('red',red,'red')]:
 cv2.imwrite(str(cont/('menu-'+nm+'-mask.png')),mask);m=closed_solid_from_mask(mask,2.23,.995,.0015,minimum_area=4,epsilon=.8);obj('mesh','MenuPhysicalInk_'+nm,6.52,.126,2.02,'Menu',ma,vertices=m['vertices'],triangles=m['triangles'],collider=False,source_ids=['SEP30-MENU'])
# Source left-side counter (source photo left maps to +T) and front-visible equipment only.
asset('SourceSteelServiceCounter',counterfile,6.48,1.28,.04,[2.33,.94,.68],'Service',{'*':'steel'})
box('DarkCounterTop',6.48,1.28,1.015,[2.38,.055,.76],'terminal','Service')
asset('CounterPaymentTerminal',posfile,6.11,1.21,1.05,[.20,.10,.35],'Service',{'Material_2100454184':'terminal','Material_2100454185':'terminal','*':'terminal'},angle=180,collider=False)
# Photo-observed vertical self-order device, actual source asset screen adapted to family form.
asset('DownloadedSelfOrderKiosk',kioskfile,5.36,1.32,.02,[.37,1.58,.40],'Service',{'*':'terminal'},collider=True)
box('SelfOrderScreenGlass',5.36,1.108,1.25,[.25,.38,.004],'screen','Service',collider=False)
# Counter top canisters, source-visible utensils; none assigned to hidden kitchen.
for i in range(3):
 cyl('UtensilCanister_%d'%i,6.55+i*.25,1.30,1.135,.14,.24,'steel','Service',collider=False)
 for j in range(2):asset('Utensil_%d_%d'%(i,j),'Models/Food/utensil-spoon.fbx',6.55+i*.25+j*.025,1.30,1.15,[.035,.29,.025],'Service',{'*':'steel'},angle=j*20,collider=False)
# Thin whitebrick surface laminates reuse existing enclosure planes. No new booth shell.
# Back finish excludes corner openings/jamb margins recorded in actual wall geometry.
for row in range(7):
 for col in range(17):
  u=.65+col*.405+(row%2)*.195
  if u>W-.40:continue
  box('BackBrick_%02d_%02d'%(row,col),u,DEP-.06,1.17+row*.205,[.395,.192,.025],'brick_alt' if (row+col)%4==0 else 'brick','WallFinish',collider=False)
# Photo right wall is reused; source finish is a thin laminate inset from native face.
for row in range(7):
 for col in range(13):
  d=.54+col*.405+(row%2)*.195
  if d>DEP-.6:continue
  box('RightBrick_%02d_%02d'%(row,col),.035,d,1.17+row*.205,[.025,.192,.395],'brick_alt' if col%3==0 else 'brick','WallFinish',collider=False)
# Continuous blue perimeter bench from directly observed seat/back geometry.
for i in range(6):
 u=.78+i*.56
 box('BackBenchSeat_%d'%i,u,DEP-.47,.455,[.55,.11,.51],'benchseat','Seating')
 box('BackBenchBack_%d'%i,u,DEP-.14,.79,[.55,.71,.055],'bench','Seating')
 for dx in [-.18,.18]:box('BenchLeg_%d_%s'%(i,dx),u+dx,DEP-.48,.22,[.055,.44,.055],'frame','Seating')
for i in range(4):
 d=1.50+i*1.05
 box('SideBenchSeat_%d'%i,.31,d,.455,[.52,.11,1.03],'benchseat','Seating')
 box('SideBenchBack_%d'%i,.06,d,.79,[.055,.71,1.03],'bench','Seating')
# Four back-row and three front-row source-visible square white pedestal table families.
tables=[(1.18,DEP-1.10),(2.47,DEP-1.10),(3.76,DEP-1.10),(1.15,2.00),(2.48,2.00),(3.78,2.00),(1.2,3.65),(2.63,3.65)]
for i,(u,d) in enumerate(tables):
 asset('DownloadedTableBase_%02d'%i,tablefile,u,d,.02,[.52,.70,.52],'Seating',{'*':'steel'})
 box('WhiteLaminateTable_%02d'%i,u,d,.755,[.78,.038,.75],'white','Seating')
 box('PlywoodTableEdge_%02d'%i,u,d,.731,[.78,.016,.75],'wood','Seating',collider=False)
 # Rounded metal Tolix furniture, actual free downloaded chair. Central entry lane remains clear.
 positions=[(u,d-.64,0)]
 if d<DEP-1.5:positions.append((u,d+.64,180))
 if i in [0,1,2]:positions.append((u+.58,d,-90))
 for j,(cu,cd,ang) in enumerate(positions):
  if cu<4.0:asset('TolixChair_%02d_%02d'%(i,j),chairfile,cu,cd,.02,[.45,.84,.49],'Seating',{'Scene_-_Root':'chair_white' if i%3==0 else 'chair','*':'chair'},angle=ang)
# Table food is real source-model geometry with explicit adaptation, not a photo plane.
for i in [0,3,5]:
 u,d=tables[i];asset('BlackServingPlate_%d'%i,'Models/Food/plate.fbx',u,d,.785,[.29,.022,.29],'Food',{'*':'bowl'},collider=False)
 cyl('RedSauce_%d'%i,u,d,.811,.235,.01,'sauce','Food',collider=False)
 for j in range(7):
  ang=j*2.399;ru=u+.064*math.cos(ang);rd=d+.064*math.sin(ang)
  asset('SourceAdaptedRiceCake_%d_%d'%(i,j),'Models/Food/sausage-half.fbx',ru,rd,.818,[.048,.040,.092],'Food',{'*':'tteok'},angle=j*53,collider=False,qualification='CC0_FOOD_SOURCE_FORM_ADAPTED_TO_PHOTO_GARAETTEOK_MODEL_NOT_SAUSAGE_IDENTITY')
 for j in range(3):
  box('SourceVisibleFishcake_%d_%d'%(i,j),u-.07+j*.065,d+.025,.832,[.065,.012,.055],'fishcake','Food',yaw=yaw+j*22,collider=False)
 asset('SteelTableSpoon_%d'%i,'Models/Food/utensil-spoon.fbx',u+.30,d,.79,[.045,.013,.19],'Food',{'*':'steel'},collider=False)
 asset('SmallSideBowl_%d'%i,'Models/Food/bowl.fbx',u-.25,d+.1,.788,[.09,.03,.09],'Food',{'*':'bowl'},collider=False)
# Actual physical hanging lamp fixtures only. All Unity Light components stay deleted.
for i,u in enumerate([1.16,2.70,4.0]):
 cyl('PendantCord_%d'%i,u,3.25,2.67,.012,.55,'terminal','PhysicalFixtures',collider=False)
 cyl('PendantSocket_%d'%i,u,3.25,2.38,.045,.09,'terminal','PhysicalFixtures',collider=False)
 obj('sphere','PendantBulb_%d'%i,u,3.25,2.28,'PhysicalFixtures','bulb',size=[.075,.14,.075],collider=False)
# Source black picture frames, physical small wall frames, no invented room labels.
for i,(u,y,w,h) in enumerate([(1.05,2.05,.30,.38),(1.57,1.84,.22,.28),(2.01,2.13,.34,.27)]):
 box('WallPictureFrame_%d'%i,u,DEP-.105,y,[w,h,.04],'frame','WallDetails',collider=False)
 box('WallPicturePaper_%d'%i,u,DEP-.13,y,[w-.04,h-.04,.01],'white','WallDetails',collider=False)
# Retain uncertainty about air-conditioning SKU and hidden supply/storage; no fabricated BOH.
P.joinpath('MODEL-PLACEMENT-BASIS-R25.json').write_text(json.dumps({'store_key':'namcheon','status':'CURRENT_WALL_FACE_INTERSECTIONS_AND_SOURCE_VISIBLE_MODEL','cad_source':D['source'],'cad_sha256':D['source_sha256'],'fixed_anchors':[{'id':o['object_id'],'path':o['path']} for o in [wleft,wright,wback]],'method':'Interior faces are maxT wall060 and minT wall061P1; public front is min of actual end normals; back is maxN actual wall061P2. Intersections of T/N supporting lines give corners. Old sourcebox not used for geometry.','T':T.tolist(),'outward_N':N.tolist(),'left_T':left,'right_T':right,'front_N':front,'back_N':back,'working_width_MODEL_m':W,'working_depth_MODEL_m':DEP,'working_corners_q':[q(0,0),q(W,0),q(W,DEP),q(0,DEP)],'frontage_inset_m':frontd,'entry_clear_u_interval':list(clear),'entry_clear_width_MODEL_m':clear[1]-clear[0],'source_photo_left_corresponds_to_positive_T':True,'reuse_existing_enclosure':True,'native_walls_doors_columns_materials_floor_openings_untouched':True,'hidden_BOH_modeled':False,'metric_measurement':False},ensure_ascii=False,indent=2))
packet={'store_key':'namcheon','origin_q':q(W/2,DEP/2),'floor_world_y':7.0,'source_packet':'SOURCE-EVIDENCE-R25.json','source_imports':imports,'materials':M,'objects':O}
(P/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
# Source records preserve acquisition/license identity and attribution inside own runtime folder.
for folder,name in [('3fdcc5bc702d40f6bfee171e7046e32d','Tolix chair by allenbranch'),('counter','Industrial kitchen bun counter by siyarasm'),('table','Cafe table by Andy.Honor'),('pos','POS terminal by Vladyslav.Yarovyi'),('084a535f5fb542c99eb3564a28195e3e','Pay Station Kiosk by djeustice')]:
 a=json.loads((S/'models'/folder/'ACQUISITION.json').read_text());credits.append({'name':name,'license':'CC-BY-4.0','acquisition':a,'adaptation':'Own copy photo fitted; current URP per source-slot maps; exact SKU and surveyed equality not established'})
credits.append({'name':'Food Kit','author':'Kenney','source_url':'https://kenney.nl/assets/food-kit','license':'CC0','adaptations':['black plates and small bowls','source geometric sausage-half form adapted to photo thick rice cakes with sauce','steel spoon'],'exact_food_identity_geometry':'MODEL matched family and proportions from exact branch photo'})
(P/'ASSET-CREDITS-R25.json').write_text(json.dumps(credits,ensure_ascii=False,indent=2));imp(P/'ASSET-CREDITS-R25.json','ATTRIBUTION-NAMCHEON-R25.json');packet['source_imports']=imports;(P/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
sources=[]
for idx,idpre in [('ROOT-sujin_note-224399487243-INDEX.json','SEP07'),('ROOT-wdefs420-224427350117-INDEX.json','SEP30'),('ROOT-ellie0116-224433385586-INDEX.json','OCT06')]:
 j=json.loads((S/idx).read_text());inds=[0,5] if idpre=='SEP07' else [0,1,2,5,6] if idpre=='SEP30' else [4]
 for i in inds:
  rec=next(x for x in j['images'] if x['index']==i);sources.append({'id':idpre+'-%02d'%i,'url':j['url'],'published':j['source_date'],'image_url':rec['url'],'local_file':str(S/pathlib.Path(rec['path']).name),'sha256':sha(S/pathlib.Path(rec['path']).name),'directly_viewed_full':True})
evidence={'store_key':'namcheon','branch':'남천할매떡볶이 부산역2층맞이방스낵점','source_epochs':'September7, September30 and October6 2026; photo fileepochs preserved in source indices','source_classification':'directly_supported exact branch visible shapes; room fit and all unmeasured dimensions MODEL','sources':sources,'original_design_research':{'queries':['부산역 남천할매떡볶이 인테리어 도면 설계','부산역 2층 남천할매떡볶이 내부 주방 키오스크'],'result':'No authenticated exact-branch original floor/ceiling/elevation/furniture drawing acquired; unrelated mainbranch and foodtruck plans excluded'},'direct_observed':['blackfascia white raised sevenKoreanletters and redseal','central glass entry, left foodorder counter and kiosk, rightdining','whitebrick bluewallbench white pedestal squaretables gray/mintTolixchairs','warmhanging physicalfixtures','source thick garaetteok and fishcake in blackplates'],'excluded_source_premises':[{'file':'ROOT-sujin_note-224399487243-06.jpg','classification':'contradicts_premise','reason':'Full source photograph is train cabin window/seats, not store rear bench; never used for shop modeling.'}],'source_id_aliases':{'SEP30-FRONT':'SEP30-00','OCT06-FRONT':'OCT06-04','SEP30-FRONT-CONTOUR':'SEP30-00','SEP07-SEAL-CONTOUR':'SEP07-05','SEP30-MENU':'SEP30-01'},'sign_method':'Source image contours; closed front/back/sides with real letter counters. Mainbrand seven separate meshes; source redseal and blackink plus wholemenu contour inks; no font/TMP/wholephotos used as signfaces.','models':credits,'limits':['Unseen hidden kitchen and supply systems not modeled','Furniture actual SKU, exact count behind occlusion, equipment details and metric dimensions remain MODEL','No measured field equality, percent fidelity, efficacy or performance claims','Unconfirmed doorway/native correspondence not used to cut existing geometry'],'preserve':'All native/floor/openings/columns/WC/circulation/1F/otherstores/publicstaffplatformdoors/useredits/deletions/sourceAssets','lights':'No Unity Light components constructed; physical bulbs/emissive surfaces only','QA_Play_tests_reopen_capture':False}
(P/'SOURCE-EVIDENCE-R25.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2))
sc='C180_R25_NAMCHEON_APPLY_001';cs=(B/'StoreBuilderR25-v5-NO-LIGHTS.template.cs').read_text();repl={'__STORE_KEY__':'namcheon','__WORKER_THREAD__':'classic:/root/r25_namcheon','__EXACT_SCOPE__':sc,'__PACKET_PATH__':str(P/'STORE-PACKET-R25.json'),'__PACKET_SHA256__':sha(P/'STORE-PACKET-R25.json')}
for x,y in repl.items():cs=cs.replace(x,y)
(P/'ApplyStoreR25.cs').write_text(cs)
(P/'planning/task_plan.md').write_text('# Namcheon R25 physical construction\n\n1. Source photographs and actual CAD authority: complete\n2. Fit source-visible interior, downloaded models and solid sign: complete\n3. C180 own scoped apply/save and receipts: in_progress\n\nPreserve native geometry, all Lights deleted; no QA/reopen/capture. No nested agents.\n')
(P/'planning/progress.md').open('a').write('\nPrepared sourcecontour signs and menu, downloaded CC-BY Tolix chairs, copied licensed table/counter/POS into own staging and fit source families. Derived room from actual 060/061 wall face lines, no old box dimensions. Packet and v5 builder ready; no Scene/Assets changes yet.\n')
print(json.dumps({'objects':len(O),'downloaded_asset_instances':sum(o['kind']=='asset' for o in O),'physical_source_contour_meshes':sum(o['kind']=='mesh' for o in O),'imports':len(imports),'width':W,'depth':DEP,'packet_bytes':(P/'STORE-PACKET-R25.json').stat().st_size,'scope':sc}))
