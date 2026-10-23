from pathlib import Path
import json,math,hashlib,shutil,importlib.util,cv2,numpy as np
ROOT=Path('/Users/um-yunsang/CHOOGuard');B=ROOT/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25';O=B/'boksoon';S=O/'sources';A='Assets/ChooGuard/Art/StationInterior/R25_Shops/boksoon'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def dump(name,d): (O/name).write_text(json.dumps(d,ensure_ascii=False,indent=2))
spec=importlib.util.spec_from_file_location('outline',B/'photo_outline_to_solid.py');outline=importlib.util.module_from_spec(spec);spec.loader.exec_module(outline)
# Native face vectors and native matching-level floor north ring, independently intersected.
cad=json.loads((O/'LOCAL-ACTUAL-CAD-REFERENCE-R25.json').read_text());native=cad['objects'][1]
left=[[-2.9453633037732416,-33.07438255478925],[-2.95044,-34.37226]]
# Exact original long endpoint and continuation from source sections.
left=[s for s in native['sections']['7.15000'] if abs(s[0][0]+2.9453633037732416)<1e-9 and abs(s[0][1]+33.07438255478925)<1e-9 and math.dist(*s)>.5][0]
right=[[3.036955401590589,-34.8977817655081],[3.055722,-30.09782]]
right=[s for s in native['sections']['8.40000'] if math.dist(*s)>3 and abs(s[0][0]-3.03696)<.001][0]
back=[s for s in native['sections']['7.15000'] if abs(s[0][0]+1.3198395243486019)<1e-8 and math.dist(*s)>1][0]
north=[[-18.777824,-30.01248],[3.65573,-30.100163]]
def crossline(a,b):
 p=np.array(a[0]);r=np.array(a[1])-p;q=np.array(b[0]);s=np.array(b[1])-q;t=np.cross(q-p,s)/np.cross(r,s);return(p+t*r).tolist()
lf=crossline(left,north);rf=crossline(right,north);lb=crossline(left,back);rb=crossline(right,back)
origin=(np.array(lf)+rf)/2;u=(np.array(rf)-lf);width=float(np.linalg.norm(u));u=u/width;inside=np.array([u[1],-u[0]]);depth=float(np.dot(np.array(lb)-lf,inside));angle=math.degrees(math.atan2(-u[1],u[0]))
def q(x,v):return (origin+u*x+inside*v).tolist()
basis={'store_key':'boksoon','classification':'directly_supported native vectors; source-to-native room binding and fixture dimensions MODEL','native_room_object_id':native['object_id'],'native_room_path':native['path'],'native_mesh_asset':native['mesh_asset'],'immutable_CAD_source':cad['source'],'immutable_CAD_sha256':cad['source_sha256'],'anchors':{'left_native_face':left,'right_native_face':right,'back_native_face':back,'north_matching_level_native_floor_ring':north},'calculated_intersections_q':{'left_front':lf,'right_front':rf,'left_back':lb,'right_back':rb},'working_width_m_MODEL':width,'working_depth_m_MODEL':depth,'origin_q':origin.tolist(),'u_axis_q':u.tolist(),'inward_axis_q':inside.tolist(),'hall_outward_normal_q':(-inside).tolist(),'source_rectangle_used_as_metric':False,'native_room_reused_no_duplicate_walls':True,'no_floor_or_native_changes':True,'frontage':'Missing source black frame/fixed glass only, central 2.8m entry remains open; no native doorway cuts','neighbor_index':str(B/'ACTUAL-SAVED-STORES-INDEX-R25.json'),'neighbors_preserved':['Flowers around[-15.2,-32.25]','all1F and native stair/entry/columns','all saved R25 stores'],'fitout_qualification':'Photographic source forms/relations fitted inside actual native room; not surveyed lease/fixture equality','hidden_BOH':'Not invented'}
dump('MODEL-PLACEMENT-BASIS-R25.json',basis)
materials={'black':{'color':[.018,.023,.027,1],'metallic':.4,'smoothness':.5},'blue':{'color':[.047,.095,.17,1],'metallic':.15,'smoothness':.45},'chrome':{'color':[.69,.72,.74,1],'metallic':.95,'smoothness':.83},'white':{'color':[.93,.925,.90,1],'smoothness':.5},'beige':{'color':[.58,.57,.52,1],'metallic':.25,'smoothness':.42},'gold':{'color':[.62,.45,.20,1],'metallic':.85,'smoothness':.7},'paper':{'color':[.95,.95,.92,1],'smoothness':.25},'ink':{'color':[.028,.028,.026,1],'smoothness':.35},'glass':{'color':[.78,.88,.90,.07],'transparent':True,'smoothness':.96},'fridge_inner':{'color':[.82,.84,.84,1],'metallic':.45,'smoothness':.5},'foil':{'color':[.92,.93,.94,1],'metallic':.4,'smoothness':.5},'cream':{'color':[.72,.70,.52,1],'smoothness':.65},'rose':{'color':[.55,.28,.27,1],'smoothness':.65},'darkwine':{'color':[.08,.07,.036,1],'smoothness':.75},'white_emissive':{'color':[.95,.98,1,1],'emission':[1.8,1.9,2.0,1],'smoothness':.65},'warm_fixture':{'color':[1,.83,.66,1],'emission':[1.5,1.1,.8,1],'smoothness':.6},'browncap':{'color':[.21,.10,.055,1],'metallic':.65,'smoothness':.6},'red':{'color':[.56,.018,.012,1],'smoothness':.5}}
imports=[]
def imp(src,dst):
 src=Path(src);imports.append({'source':str(src),'destination':dst,'sha256':sha(src)});return A+'/'+dst
for fn in ['diff.jpg','normal.jpg']:
 src=S/'models/chair'/fn;shutil.copy2(B/'story-lamant/sources/free-assets/wood_table_001'/fn,src);imp(src,'PBR/plywood/'+fn)
materials['plywood']={'color':[1,.78,.43,1],'smoothness':.42,'texture_asset':A+'/PBR/plywood/diff.jpg','normal_asset':A+'/PBR/plywood/normal.jpg'}
assets={}
for key,path in [('chair','chair/PlywoodChromeChair.fbx'),('table','table/CafeTableSteelBase.fbx'),('fridge','fridge/CommercialRefrigerator-Empty.fbx'),('bottle','fridge/ChampagneBottle_SOURCE_DERIVATIVE.fbx'),('fan','fan/DeskFan.fbx'),('pos','pos/POS-Terminal.fbx'),('case','case/DisplayCase.fbx')]:assets[key]=imp(S/'models'/path,'FreeModels/'+path)
assets['glass']=imp(B/'free-assets/kenney-food-kit/Models/FBX format/glass.fbx','FreeModels/Glass.fbx')
# Source attribution travels with runtime imports.
credit={'models':[json.loads((S/'models'/k/'SOURCE.json').read_text())for k in ['chair','table','fridge','fan','pos','case']],'smallware':'glass.fbx from Kenney Food Kit CC0 https://kenney.nl/assets/food-kit','adaptation':'Own staged derivatives and explicit URP mapping; exact observed manufacturer/SKU not established','source_files_unchanged':True}
(S/'MODEL-CREDITS.json').write_text(json.dumps(credit,ensure_ascii=False,indent=2));imp(S/'MODEL-CREDITS.json','SOURCE-LICENSES.json')
objects=[]
def add(kind,id,x,v,y,group,source=None,**kw):
 d={'kind':kind,'id':id,'group':group,'q':q(x,v),'y':y,'yaw':angle,'source_ids':source or ['SEP26-01','AUG26-05','AUG26-07'],'qualification':'SOURCE_PHOTO_NATIVE_ANCHORED_MODEL_NOT_FIELD_MEASUREMENT'};d.update(kw);objects.append(d);return d
def box(id,x,v,y,size,mat,group,**kw):return add('box',id,x,v,y,group,size=size,material=mat,**kw)
def cyl(id,x,v,y,diam,height,mat,group,**kw):return add('cylinder',id,x,v,y,group,size=[diam,height/2,diam],material=mat,**kw)
def asset(key,id,x,v,y,fit,group,matmap,yaw=None,collider=True):return add('asset',id,x,v,y,group,asset=assets[key],fit_size=fit,material_map=matmap,yaw=angle+(yaw or 0),collider=collider)
# Existing room is retained; no side/rear/ceiling/floor construction.
fw=width-.14
box('Navy_Physical_Fascia',0,.10,2.91,[fw,.72,.14],'blue','Facade_Physical_Frame')
for x in [-fw/2,fw/2,-1.4,1.4]:box('Front_Jamb_'+str(x),x,.10,1.28,[.055,2.55,.06],'black','Facade_Physical_Frame')
for sign,x in [('L',-2.15),('R',2.15)]:
 box('Fixed_Glass_'+sign,x,.10,1.27,[1.40,2.48,.014],'glass','Facade_Physical_Frame',cast_shadows=False)
 box('Fixed_Sill_'+sign,x,.10,.065,[1.43,.07,.06],'black','Facade_Physical_Frame')
box('Front_Header',0,.10,2.52,[fw,.055,.06],'black','Facade_Physical_Frame')
# True source photo contour logo including counters, physical front/back/sides.
mask=cv2.imread(str(S/'BRAND-INK-MASK.png'),0);logo=outline.closed_solid_from_mask(mask,4.75,.63,.045,minimum_area=22,epsilon=.48)
add('mesh','Photo_Outline_Boksoon_Closed_3D_Brand',0,.022,2.92,'Source_Solid_Sign',['SEP26-01'],material='white_emissive',collider=False,vertices=logo['vertices'],triangles=logo['triangles'])
# Source-front actual printed banners become thin physical ink geometry, no raster photo signs.
im=cv2.imread(str(S/'ROOT-sjj926-224417262910-01.jpg'))
for side,pts,x in [('L',[[76,230],[225,232],[197,722],[31,699]],2.15),('R',[[735,215],[932,229],[926,727],[746,735]],-2.15)]:
 rect=cv2.warpPerspective(im,cv2.getPerspectiveTransform(np.float32(pts),np.float32([[0,0],[300,0],[300,780],[0,780]])),(300,780));m=cv2.inRange(cv2.cvtColor(rect,cv2.COLOR_BGR2GRAY),0,105);m[:8]=0;m[-8:]=0;m[:,:8]=0;m[:,-8:]=0
 cv2.imwrite(str(S/('BANNER-'+side+'-INK-MASK.png')),m);solid=outline.closed_solid_from_mask(m,1.03,1.84,.0012,minimum_area=5,epsilon=.65)
 box('Source_Banner_Panel_'+side,x,.060,1.35,[1.10,1.94,.012],'paper','Source_Solid_Printed_Banners')
 add('mesh','Source_Banner_Physical_Ink_'+side,x,.052,1.35,'Source_Solid_Printed_Banners',['SEP26-01'],vertices=solid['vertices'],triangles=solid['triangles'],material='ink',collider=False)
 for dx in [-.50,.50]:
  for dy in [-.89,.89]:cyl('Banner_Mount_'+side+str(dx)+str(dy),x+dx,.045,1.35+dy,.025,.012,'chrome','Source_Solid_Printed_Banners',euler=[90,angle,0],collider=False)
# Five current photographed glass-door fridges; no imported Light components.
for f in range(5):
 v=.64+.65*f
 asset('fridge','Downloaded_Bottle_Fridge_'+str(f),2.51,v,.015,[.63,1.98,.64],'Downloaded_Refrigeration',{'FridgeCase':'black','FridgeInterior':'fridge_inner','FridgeGlass':'glass','*':'chrome'},yaw=-90)
 # Actual-source bottle geometry, individual editable front stock, nominal count qualified.
 for row in range(5):
  for col in range(6):
   bv=v-.245+col*.097
   asset('bottle',f'Free_Bottle_F{f}_R{row}_C{col}',2.29,bv,.41+row*.292,[.067,.27,.067],'Downloaded_Products',{'ChampagneGlass':['cream','rose','darkwine'][f%3],'ChampagneFoil':'foil','ChampagneLabel':'paper','*':'paper'},collider=False)
# Photographed high blinds above fridge wall, fixed native wall/glazing reused.
box('Photo_Blind_Headrail',2.86,2.08,3.18,[.065,.045,3.35],'white','Source_Visible_Upper_Blinds')
for i in range(24):box('Photo_Blind_Slat_'+str(i),2.86,2.08,2.055+i*.046,[.070,.014,3.35],'white','Source_Visible_Upper_Blinds',collider=False)
for v in [.52,3.64]:cyl('Blind_Draw_Cord_'+str(v),2.82,v,2.61,.003,1.12,'paper','Source_Visible_Upper_Blinds',collider=False)
# Source stainless service counter with actual downloaded POS.
box('Stainless_Service_Counter',.8,1.23,.44,[2.05,.88,.62],'chrome','Service_Counter')
box('Service_Black_Worktop',.8,1.23,.90,[2.11,.055,.68],'black','Service_Counter')
asset('pos','Downloaded_Actual_POS_Terminal',1.50,1.23,.93,[.34,.39,.29],'Service_Counter',{'*':'black'},yaw=180,collider=False)
for i in range(8):asset('glass','Downloaded_Tasting_Glass_'+str(i),.06+i*.095,1.12,.93,[.055,.082,.055],'Service_Counter',{'*':'white'},collider=False)
for i in range(3):asset('bottle','Counter_Actual_Free_Bottle_'+str(i),.45+i*.12,1.36,.93,[.074,.29,.074],'Service_Counter',{'ChampagneGlass':['cream','rose','darkwine'][i],'ChampagneFoil':'foil','ChampagneLabel':'paper','*':'paper'},collider=False)
# Three photographed white round pedestal tables, downloaded steel chassis + source-sized top.
for i,(x,v) in enumerate([(-1.65,.98),(-1.65,2.83),(-.02,3.62)]):
 asset('table','Downloaded_Steel_Pedestal_'+str(i),x,v,0,[.54,.70,.54],'Downloaded_Seating',{'*':'chrome'})
 cyl('White_Round_Tabletop_'+str(i),x,v,.728,.88,.045,'white','Downloaded_Seating')
 # Actual-source formed-shell/chrome model, amber plywood mapped per material slot.
 for j,(dx,dv,rot) in enumerate([(0,-.56,180),(0,.56,0),(-.56,0,-90),(.56,0,90)]):
  asset('chair',f'Downloaded_Amber_Plywood_Chair_{i}_{j}',x+dx,v+dv,0,[.43,.80,.47],'Downloaded_Seating',{'Black_Opaque_Plastic':'black','Metal':'chrome','Plastic':'plywood','*':'plywood'},yaw=rot)
 # Cordless lamp body/source bottle contour, cap and physical emission only.
 asset('bottle','Bottle_Shaped_Table_Fixture_'+str(i),x,v,.755,[.072,.27,.072],'Physical_Fixtures_No_Lights',{'*':'warm_fixture'},collider=False)
 add('sphere','Fixture_Brown_Cap_'+str(i),x,v,1.04,'Physical_Fixtures_No_Lights',size=[.061,.061,.061],material='browncap',collider=False)
 cyl('Fixture_Metal_Base_'+str(i),x,v,.77,.078,.025,'chrome','Physical_Fixtures_No_Lights',collider=False)
# Floor fan from real downloaded source; physical geometry only.
asset('fan','Downloaded_Physical_Floor_Fan',-2.40,1.93,0,[.57,.64,.32],'Physical_Fixtures_No_Lights',{'*':'black'},yaw=90)
# Two true downloaded glass cases fitted to photographed pale panel/chrome modular type.
for k,(x,v,w) in enumerate([(1.35,2.66,1.35),(1.45,3.97,1.25)]):
 asset('case','Downloaded_Modular_Glass_Case_'+str(k),x,v,0,[w,.87,.58],'Downloaded_Merchandise_Cases',{'R120G120B120':'glass','R127G127B127':'chrome','Woods__Plastics.Finish_Carpentry.Wood.Pine.png':'beige','*':'glass'})
 # Source chrome tube rails and ball joints, independently modeled external frame.
 for dx in [-w/2,w/2]:
  for dv in [-.29,.29]:
   cyl(f'Case_{k}_Tube_{dx}_{dv}',x+dx,v+dv,.46,.014,.89,'chrome','Case_Source_Frame',collider=False)
   for h in [.04,.52,.90]:add('sphere',f'Case_{k}_Joint_{dx}_{dv}_{h}',x+dx,v+dv,h,'Case_Source_Frame',size=[.028]*3,material='chrome',collider=False)
 for h in [.52,.90]:
  for dv in [-.29,.29]:box(f'Case_{k}_Rail_{h}_{dv}',x,v+dv,h,[w,.014,.014],'chrome','Case_Source_Frame',collider=False)
 for j in range(4):asset('bottle',f'Case_Bottle_{k}_{j}',x-w*.32+j*w*.22,v,.18,[.065,.25,.065],'Downloaded_Products',{'ChampagneGlass':['cream','rose'][j%2],'ChampagneFoil':'foil','ChampagneLabel':'paper','*':'paper'},collider=False)
# Source wall-mounted air grille/AC face is local fitout; no native mesh is cut.
box('Source_Visible_AC_Face',-2.80,2.75,1.12,[.09,1.85,.66],'white','Physical_Fixtures_No_Lights')
for i in range(8):box('Source_Visible_AC_Louvre_'+str(i),-2.74,2.75,1.55+i*.045,[.025,.018,.46],'black','Physical_Fixtures_No_Lights',collider=False)
# Real source menu printing modeled as closed thin ink on a framed counter panel.
menu=cv2.imread(str(S/'ROOT-sjj926-224417262910-07.jpg'));pts=np.float32([[23,48],[951,80],[823,1143],[158,1165]])
rect=cv2.warpPerspective(menu,cv2.getPerspectiveTransform(pts,np.float32([[0,0],[700,0],[700,1000],[0,1000]])),(700,1000));m=cv2.inRange(cv2.cvtColor(rect,cv2.COLOR_BGR2GRAY),0,112);m[:20]=0;m[-20:]=0;m[:,:20]=0;m[:,-20:]=0
cv2.imwrite(str(S/'MENU-SOURCE-INK-MASK.png'),m);solid=outline.closed_solid_from_mask(m,.285,.405,.0008,minimum_area=7,epsilon=.65)
box('Menu_Brass_Frame',.06,1.40,1.17,[.32,.44,.018],'gold','Source_Menu_Solid_Print')
box('Menu_White_Panel',.06,1.388,1.17,[.299,.421,.008],'paper','Source_Menu_Solid_Print')
add('mesh','Menu_Actual_Source_Ink',.06,1.382,1.17,'Source_Menu_Solid_Print',['SEP26-07'],vertices=solid['vertices'],triangles=solid['triangles'],material='ink',collider=False)
# Floor safety extinguisher visible in current seating photo; actual cylinder body/no lettering.
cyl('Visible_Extinguisher_Body',-2.62,3.90,.205,.13,.36,'red','Physical_Fixtures_No_Lights')
box('Extinguisher_Handle',-2.62,3.90,.43,[.11,.045,.05],'black','Physical_Fixtures_No_Lights',collider=False)
# Exact source ledger and explicit uncertainty.
sources=[]
for file,ids in [('ROOT-ha_yeon_--224383979559-INDEX.json',[1,5,6,7,14]),('ROOT-sjj926-224417262910-INDEX.json',[1,3,5,7])]:
 d=json.loads((S/file).read_text());sources.append({'url':d['url'],'epoch':d['source_date'],'classification':'directly_supported exact2F photographed visible forms, metric dimensions MODEL','directly_viewed':[d['images'][i]for i in ids]})
limits=['No authenticated exact2F original floor/ceiling/elevation/furniture plans found after exact branch Korean/English searches.','All source-to-native correspondence/fixture metric fits are MODEL; no surveyed equality.','Nominal seating/product counts and generic free-model identities do not establish exact manufacturer/SKU or complete inventory.','Existing native room shell/floor is reused without changing native TRS/materials; unseen wall construction/BOH/hidden stock remains unresolved.','Sept21 source photo02 is gold-fascia large1F bar branch and explicitly excluded.','Exact support-face correspondence for hanging fabric-bag/umbrella/skin-care merchandise and individual printed packaging SKUs remains unresolved, so those details are not passed off as exact observed inventory.','No runtime/effectiveness/user acceptance claim; user validates; no agent Scene QA/Play/tests/reopen/captures.']
evidence={'store_key':'boksoon','exact_branch':'BusanStation2F blue fascia inner room east of central entrance','sources':sources,'original_design_discovery':{'queries':['부산역 복순도가 2층 매장 인테리어 도면 설계','Boksoondoga Busan station store interior architect plan','복순도가 부산역 도면','복순도가 부산역 2층 설계'],'result':'NO_AUTHENTICATED_EXACT_BRANCH_ORIGINAL_DRAWING_FOUND','official_context':'https://en.boksoondoga.com/space','other_branch_design_transferred':False},'free_model_credits':credit,'actual_asset_manifest':[{'source':x['source'],'destination':x['destination'],'sha256':x['sha256']}for x in imports],'source_previews_directly_viewed':['4-Legged Ant Chair','Commercial Fridge','Bakery Display Case'],'source_sign_authoring':{'brand':'Sept21 actual photo fascia mask; source contour 45mm closed solids with counters/front/back/sides','banners_menu':'source photo ink extracted to thin physical solids on independently modeled physical panels','visible_raster_sign_planes':False,'text_TMP_components':0},'light_components_created':0,'model_importLights':False,'missing_or_qualified':limits,'no_agent_Scene_QA':True}
dump('SOURCE-EVIDENCE-R25.json',evidence)
packet={'store_key':'boksoon','origin_q':origin.tolist(),'floor_world_y':7,'source_packet':str(O/'SOURCE-EVIDENCE-R25.json'),'placement_basis':str(O/'MODEL-PLACEMENT-BASIS-R25.json'),'materials':materials,'source_imports':imports,'objects':objects,'qualified_limits':limits}
dump('STORE-PACKET-R25.json',packet)
script=(B/'StoreBuilderR25-v5-NO-LIGHTS.template.cs').read_text()
for k,v in {'__STORE_KEY__':'boksoon','__WORKER_THREAD__':'classic:/root/r25_boksoon','__EXACT_SCOPE__':'C180_R25_BOKSOON_APPLY_002','__PACKET_PATH__':str(O/'STORE-PACKET-R25.json'),'__PACKET_SHA256__':sha(O/'STORE-PACKET-R25.json')}.items():script=script.replace(k,v)
# Unused legacy text branch removed in own copy; no Text/TMP creation code retained.
script=script.replace('using TMPro;','').replace('TMP_FontAsset localFont=null;','')
a=script.index('     if(kind=="text"){');b=script.index('     }else if(kind=="light"){',a)
script=script[:a]+'     if(kind=="text"){throw new Exception("User forbids world Text/TMP standins");\n'+script[b:]
(O/'ApplyStoreR25.cs').write_text(script)
print(json.dumps({'object_count':len(objects),'downloaded_asset_instances':sum(x['kind']=='asset'for x in objects),'width':width,'depth':depth,'front_origin':origin.tolist(),'packet_sha256':sha(O/'STORE-PACKET-R25.json'),'sign_vertices':len(logo['vertices'])}))
p=O/'planning/task_plan.md';p.write_text(p.read_text().replace('[in_progress] Bind','[complete] Bind').replace('[pending] Prepare','[complete] Prepare').replace('[pending] Import','[in_progress] Import'))
p=O/'planning/progress.md';p.write_text(p.read_text()+'\nPrepared own staged free models, actual source photo masks/physical logo and printed ink, exact native face intersections and own packet. Major free models include formed plywood/chrome chairs, pedestal bases, five fridges, 150 stock bottles, two glass cases, fan/POS/glasses. No Scene/Assets mutation before queued lease.\n')
