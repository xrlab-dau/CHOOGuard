import pathlib,json,hashlib,math
import cv2,numpy as np
from shapely.geometry import box,Polygon,Point
from shapely.geometry.polygon import orient
from shapely import constrained_delaunay_triangles
P=pathlib.Path(__file__).resolve().parent;C=P.parent;R=C.parents[1];S=P/'sources';D=S/'followup002';D.mkdir(exist_ok=True)
A='Assets/ChooGuard/Art/StationInterior/R25_Shops/pascucci/Followup002';origin=np.array([-36.700906,86.762281]);u=np.array([.999988074,-.004883823]);v=np.array([.004883823,.999988074]);yaw=math.degrees(-math.atan2(u[1],u[0]));q=lambda x,z:[round(float(k),6) for k in origin+u*x+v*z]
imports=[];mats={};objs=[];lib={}
def imp(src,rel):
 src=pathlib.Path(src).resolve();imports.append({'source':str(src),'destination':rel,'sha256':hashlib.sha256(src.read_bytes()).hexdigest()});return A+'/'+rel

def mat(k,col,**kw):mats[k]={'color':col if len(col)==4 else col+[1],**kw}
mat('steel',[.51,.53,.54],metallic=.94,smoothness=.74);mat('bronze',[.48,.36,.16],metallic=.76,smoothness=.62);mat('black',[.022,.024,.025],metallic=.3,smoothness=.5);mat('stone',[.86,.86,.80],smoothness=.65);mat('wood',[.53,.27,.09],smoothness=.42);mat('white',[.92,.92,.88]);mat('red_upholstery',[.55,.008,.025],smoothness=.35);mat('glass',[.76,.88,.9,.1],transparent=True,smoothness=.96);mat('warm_light',[1,.87,.6],emission=[1.5,1.15,.5,1]);mat('cold_wall',[.84,.85,.82],smoothness=.18)
colors=[[.87,.82,.59],[.88,.27,.18],[.57,.38,.17],[.20,.07,.022],[.88,.84,.57]]
for i,col in enumerate(colors):mat('gelato_'+str(i),col,smoothness=.24)
# Rectified source information remains printing on physical framed devices/cards, not object proxy.
def rectify(name,source,corners,dim):
 im=cv2.imread(str(S/source));out=cv2.warpPerspective(im,cv2.getPerspectiveTransform(np.float32(corners),np.float32([[0,0],[dim[0]-1,0],[dim[0]-1,dim[1]-1],[0,dim[1]-1]])),dim);dest=D/(name+'.png');cv2.imwrite(str(dest),out);return imp(dest,'Prints/'+name+'.png')
prints={}
prints['fall2026']=rectify('fall2026','current2026-15-01.jpg',[[430,278],[601,281],[603,577],[431,561]],(400,620))
prints['event2026']=rectify('event2026','current2026-00-02.jpg',[[196,290],[575,279],[605,849],[237,885]],(400,640))
prints['flavors2026']=rectify('flavors2026','current2026-15-03.jpg',[[64,480],[741,473],[751,547],[65,558]],(1000,125))
prints['menu_coffee']=rectify('menu_coffee','current2026-00-03.jpg',[[169,146],[636,216],[640,474],[154,459]],(960,540))
prints['menu_season']=rectify('menu_season','current2026-00-05.jpg',[[101,269],[418,256],[416,457],[100,459]],(960,540))
prints['menu_granita']=rectify('menu_granita','current2026-00-04.jpg',[[154,257],[505,254],[514,444],[152,455]],(960,540))
prints['menu_spc']=rectify('menu_spc','current2026-00-04.jpg',[[519,260],[798,258],[798,438],[521,442]],(960,540))
for key,tex in prints.items():mat(key,[1,1,1],texture_asset=tex,smoothness=.18,**({'emission':[.18,.18,.18,1]} if key.startswith('menu_') else {}))
food=imp(C/'free-assets/kenney-food-kit/Models/FBX format/ice-cream-scoop-chocolate.fbx','Models/ice-cream-scoop.fbx')
chair=imp(S/'modern_arm_chair_01/modern_arm_chair_01.fbx','Models/modern_arm_chair_01.fbx')
lic=D/'SOURCE-LICENSES-002.md';lic.write_text('Kenney Food Kit ice-cream-scoop-chocolate source mesh CC0 https://kenney.nl/assets/food-kit . Own photo-color/fitted gelato tray fill adaptation, original source unchanged.\nPolyHaven modern_arm_chair_01 CC0 https://polyhaven.com/a/modern_arm_chair_01 . Own red upholstery/black leg materials bound by exact originalFBX two material slots from SOURCE-FBX-MATERIAL-SLOTS-002.json. Generic freechair shape fitted to source visible red armchairs; exact productidentity not claimed.\nMenu/device printing is exactbranch source2026-09-23/10-05 fieldphotograph content, rectified only as surfaceprinting on own physical devices/frames/cards; no equipment proxy, no TMP or font standin.\n');imp(lic,'SOURCE-LICENSES-002.md')

def obj(kind,id,x,z,y,material,group='GelatoInsert',**kw):
 d={'kind':kind,'id':id,'q':q(x,z),'y':y,'yaw':yaw,'material':material,'group':group,'source_ids':['BUSAN_CURRENT_20261005','BUSAN_CURRENT_20260923'],'qualification':'SOURCE_VISIBLE_PHOTO_REFERENCED_MODEL_NOT_SURVEY',**kw};objs.append(d);return d

def b(id,x,z,y,sx,sy,sz,m,g='GelatoInsert',**kw):return obj('box',id,x,z,y,m,g,size=[sx,sy,sz],**kw)
def cy(id,x,z,y,dx,dy,dz,m,g='GelatoInsert',**kw):return obj('cylinder',id,x,z,y,m,g,size=[dx,dy/2,dz],**kw)
def asset(id,path,x,z,y,fit,m,g='GelatoFill',**kw):return obj('asset',id,x,z,y,m,g,asset=path,fit_size=fit,**kw)
def tri_mesh(poly,lo,hi):
 poly=orient(poly,sign=1);verts=[];tris=[]
 def tri(a,b,c):i=len(verts);verts.extend([a,b,c]);tris.extend([i,i+1,i+2])
 for t in constrained_delaunay_triangles(poly).geoms:
  a,b0,c=list(t.exterior.coords)[:3];tri([a[0],lo,a[1]],[b0[0],lo,b0[1]],[c[0],lo,c[1]]);tri([c[0],hi,c[1]],[b0[0],hi,b0[1]],[a[0],hi,a[1]])
 for ring in [poly.exterior,*poly.interiors]:
  pts=list(ring.coords)
  for a,b0 in zip(pts,pts[1:]):
   aa,bb,cc,dd=[a[0],lo,a[1]],[b0[0],lo,b0[1]],[b0[0],hi,b0[1]],[a[0],hi,a[1]];tri(aa,bb,cc);tri(aa,cc,dd)
 return {'vertices':verts,'triangles':tris}
def combine(meshes):
 out={'vertices':[],'triangles':[]}
 for m in meshes:
  n=len(out['vertices']);out['vertices']+=m['vertices'];out['triangles']+=[i+n for i in m['triangles']]
 return out

def ml(id,key,x,z,y,m,g='GelatoInsert',**kw):return obj('library_mesh',id,x,z,y,m,g,mesh_key=key,collider=False,**kw)
def rounded(w,d,r):return box(-w/2+r,-d/2+r,w/2-r,d/2-r).buffer(r,resolution=8)
# Own existing counter only gets an actual local aperture for the recessed 6-pan unit.
plan=Polygon([(1.12,2.38),(1.98,2.38),(1.98,4.30),(6.90,4.30),(6.90,5.05),(1.12,5.05)]).buffer(.14,join_style=1).buffer(-.14,join_style=1)
aperture=box(5.45,4.35,6.65,5.0)
lib['counter_body_local_aperture']=combine([tri_mesh(plan,.055,.65),tri_mesh(plan.difference(aperture),.65,1.045)])
lib['counter_top_local_aperture']=tri_mesh(plan.buffer(.025).difference(aperture),1.045,1.095)
prior=json.loads((P/'APPLIED-STORE-R25.json').read_text());old={o['id']:o['object_id'] for o in prior['objects']}
replacements=[]
for id,key,oldkey in [('Curved_source_maroon_L_counter','counter_body_local_aperture','counter_body'),('White_stone_L_countertop','counter_top_local_aperture','counter_top')]:
 replacements.append({'path':'FPSWorld/Architecture/2F/MAIN2F_R25_SOURCE_BOUND_SPECIALIST_SHOPS/R25_pascucci_SOURCE_BOUND_MODEL/Counter/'+id,'expected_object_id':old[id],'expected_mesh_asset':'Assets/ChooGuard/Art/StationInterior/R25_Shops/pascucci/MESH_SHARED_'+oldkey+'.asset','new_mesh_key':key})
# Recessed visible housing, hollow stainlesspan bodies and bronze/steel frame.
outer=rounded(1.24,.70,.025);inner=rounded(1.16,.62,.018);lib['gelato_frame']=tri_mesh(outer.difference(inner),1.075,1.11)
ml('Recessed_bronze_top_frame','gelato_frame',6.05,4.675,0,'bronze')
b('Recessed_cooler_bottom',6.05,4.675,.697,1.19,.055,.64,'steel')
for x in [5.465,6.635]:b('Inner_cold_side_'+str(x),x,4.675,.885,.022,.34,.63,'cold_wall',collider=False)
for z in [4.365,4.985]:b('Inner_cold_end_'+str(z),6.05,z,.885,1.18,.34,.02,'cold_wall',collider=False)
# Source perforated stainless falsefloor physically cut with small holes in ownmesh.
floorpoly=rounded(1.13,.59,.012)
for i in range(28):
 for j in range(14):
  xx=-.535+i*.0395;zz=-.264+j*.040
  floorpoly=floorpoly.difference(Point(xx,zz).buffer(.007,resolution=4))
lib['perforated_base']=tri_mesh(floorpoly,.752,.762);ml('Physical_perforated_tray_base','perforated_base',6.05,4.675,0,'steel')
# 6 hollow pans; latest2026-10-05 has front-left empty, five real downloadedfoodfilled.
panout=rounded(.306,.242,.025);paninner=rounded(.284,.220,.019)
lib['GN_pan']=combine([tri_mesh(panout,.773,.789),tri_mesh(panout.difference(paninner),.789,.972),tri_mesh(panout.buffer(.008).difference(paninner),.972,.980)])
positions=[(5.72,4.80),(6.05,4.80),(6.38,4.80),(5.72,4.54),(6.05,4.54),(6.38,4.54)]
fillindex=0
for i,(xx,zz) in enumerate(positions):
 ml('Hollow_GN_pan_'+str(i),'GN_pan',xx,zz,0,'steel')
 if i==3:continue
 color='gelato_'+str(fillindex);fillindex+=1
 for j,(dx,dz) in enumerate([(-.065,-.052),(.065,-.052),(-.065,.052),(.065,.052)]):
  asset('Downloaded_gelato_fill_%s_%s'%(i,j),food,xx+dx,zz+dz,.91,[.185,.072+(j%2)*.008,.15],color,collider=False,yaw=yaw+j*53)
# Back light/rail and a source visible slidingglass halfcover, no fake device label.
b('Coldcase_back_light',6.05,4.967,1.018,1.11,.014,.015,'warm_light',collider=False)
b('Sliding_glass_cover_half',6.34,4.675,1.124,.58,.006,.59,'glass',collider=False)
for zz in [4.345,5.005]:b('Glass_slide_track_'+str(zz),6.05,zz,1.12,1.23,.025,.016,'steel',collider=False)
b('Glass_cover_handle',6.31,4.675,1.145,.23,.018,.025,'steel',collider=False)
# Closed card/panel printing on public lip and in physical countertop frames.
def print_face(id,x,z,y,w,h,matkey,angle,group='Menus'):
 verts=[[-w/2,-h/2,0],[w/2,-h/2,0],[w/2,h/2,0],[-w/2,h/2,0]]
 return obj('mesh',id,x,z,y,matkey,group,vertices=verts,triangles=[0,1,2,0,2,3],uv=[[1,0],[0,0],[0,1],[1,1]],yaw=angle,collider=False)
b('Physical_six_flavor_card_panel',6.05,4.315,1.048,1.15,.116,.018,'bronze')
print_face('Actual2026_six_flavor_cards',6.05,4.304,1.048,1.11,.102,'flavors2026',yaw+180,'GelatoCards')
for key,x,z,y,w,h in [('fall2026',3.23,4.55,1.325,.25,.39),('event2026',1.57,2.58,1.69,.26,.40)]:
 b(key+'_physical_backboard',x,z,y,w+.035,h+.035,.024,'black','Menus',collider=False)
 # +Z outward afteryaw180 => publicfront qvminus.
 print_face(key+'_source_print',x,z-.014,y,w,h,key,yaw+180)
 b(key+'_tabletop_foot',x,z+.025,y-h/2-.018,w*.7,.03,.14,'black','Menus',collider=False)
 b(key+'_rear_brace',x,z+.065,y-.04,.025,h*.63,.025,'black','Menus',collider=False)
# Four sourcecurrent digital menu device panels on west wall: physicalbezel,housing,suspension.
for j,key in enumerate(['menu_coffee','menu_season','menu_granita','menu_spc']):
 zz=2.12+j*.835
 b(key+'_physical_housing',.965,zz,2.30,.062,.50,.82,'black','MenuDevices',collider=False)
 # Extrudedplane front is+u, atinside ofwestshell.
 print_face(key+'_actual_screen',1.000,zz,2.30,.785,.459,key,yaw+90,'MenuDevices')
 for y0 in [2.055,2.545]:b(key+'_bronze_edge_'+str(y0),1.003,zz,y0,.014,.017,.822,'bronze','MenuDevices',collider=False)
for zz in [1.9,4.85]:cy('Menu_suspension_'+str(zz),.94,zz,2.79,.013,.52,.013,'bronze','MenuDevices',collider=False)
# Four public-hall-facing red loungechairs explicitly visible2026Sep, not84seat forcing.
for j,(xx,ang) in enumerate([(9.20,90),(10.70,-90),(12.62,90),(14.12,-90)]):
 asset('Current_source_red_lounge_chair_'+str(j),chair,xx,1.33,0,[.62,.72,.65],'red_upholstery','CurrentVisibleSeating',yaw=yaw+ang,material_slots={'*':['black','red_upholstery']})
for j,xx in enumerate([9.95,13.37]):
 b('Current_square_lounge_table_'+str(j),xx,1.38,.67,.68,.045,.64,'wood','CurrentVisibleSeating');cy('Current_lounge_table_stem_'+str(j),xx,1.38,.33,.038,.62,.038,'black','CurrentVisibleSeating');cy('Current_lounge_table_foot_'+str(j),xx,1.38,.028,.40,.04,.40,'black','CurrentVisibleSeating')
packet={'store_key':'pascucci','origin_q':origin.tolist(),'floor_world_y':7.,'source_packet':str(P.relative_to(R)/'SOURCE-EVIDENCE-FOLLOWUP-002.json'),'source_imports':imports,'materials':mats,'mesh_library':lib,'objects':objs,'own_mesh_replacements':replacements,'source_epoch':'2026-10-05/2026-09-23 exactbranch','no_scene_QA':True,'no_84_seat_claim':True}
f=P/'FOLLOWUP-PACKET-002.json';f.write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')));sha=hashlib.sha256(f.read_bytes()).hexdigest();scope='C180_R25_PASCUCCI_APPLY_002'
cs=(C/'StoreBuilderR25-v3-FREE-ASSETS.template.cs').read_text()
cs=cs.replace('const string ParentPath="FPSWorld/Architecture/2F/MAIN2F_R25_SOURCE_BOUND_SPECIALIST_SHOPS";','const string ParentPath="FPSWorld/Architecture/2F/MAIN2F_R25_SOURCE_BOUND_SPECIALIST_SHOPS/R25_pascucci_SOURCE_BOUND_MODEL";')
cs=cs.replace('string folder="Assets/ChooGuard/Art/StationInterior/R25_Shops/"+StoreKey;string name="R25_"+Clean(StoreKey)+"_SOURCE_BOUND_MODEL";','string folder="Assets/ChooGuard/Art/StationInterior/R25_Shops/"+StoreKey+"/Followup002";string name="SOURCE_VISIBLE_FOLLOWUP_002";if(GameObject.Find(ParentPath)==null)throw new Exception("Existing own Pascucci root required");')
slotcode='if(spec["material_slots"] is JObject slotMap){foreach(var rr in model.GetComponentsInChildren<Renderer>()){var order=slotMap[rr.name]??slotMap["*"];if(order!=null)rr.sharedMaterials=order.Select(mk=>MaterialFor((string)mk)).ToArray();}}'
cs=cs.replace('if((bool?)spec["collider"]!=false&&model.GetComponentsInChildren<Collider>().Length==0&&!first)',slotcode+'if((bool?)spec["collider"]!=false&&model.GetComponentsInChildren<Collider>().Length==0&&!first)')
replacecode='''foreach(var spec in (JArray)p["own_mesh_replacements"]){Guard();var go=GameObject.Find((string)spec["path"]);if(go==null||!go.transform.IsChildOf(parent)||GlobalObjectId.GetGlobalObjectIdSlow(go).ToString()!=(string)spec["expected_object_id"])throw new Exception("Exact own counter object required");var mf=go.GetComponent<MeshFilter>();if(mf==null||AssetDatabase.GetAssetPath(mf.sharedMesh)!=(string)spec["expected_mesh_asset"])throw new Exception("Own unchanged counter mesh required");Undo.RecordObject(mf,"Pascucci visible gelato aperture");mf.sharedMesh=LibraryMesh(p,folder,(string)spec["new_mesh_key"]);EditorUtility.SetDirty(mf);}'''
cs=cs.replace('AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);',replacecode+'AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);')
cs=cs.replace('["object_count"]=applied.Count,["created_asset_paths"]','["object_count"]=applied.Count,["own_counter_mesh_replacements"]=p["own_mesh_replacements"].DeepClone(),["prior_store_receipt"]=".state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/pascucci/APPLIED-STORE-R25.json",["created_asset_paths"]')
cs=cs.replace('"/APPLIED-STORE-R25.json"','"/APPLIED-FOLLOWUP-R25-002.json"')
for a,z in {'__STORE_KEY__':'pascucci','__WORKER_THREAD__':'classic:/root/r25_pascucci','__EXACT_SCOPE__':scope,'__PACKET_PATH__':str(f),'__PACKET_SHA256__':sha}.items():cs=cs.replace(a,z)
(P/'ApplyFollowup002.cs').write_text(cs)
evidence={'scope':'Source-visible followup002','directly_viewed':[{'source':'https://m.blog.naver.com/dorongation/224420732947','published_utc':'2026-09-23T02:18:52.321Z','files':['sources/current2026-00-contact.jpg','sources/current2026-00-02.jpg','sources/current2026-00-03.jpg','sources/current2026-00-04.jpg','sources/current2026-00-05.jpg','sources/current2026-00-07.jpg','sources/current2026-00-11.jpg'],'observed':'Currentbluefour-screenmenudevices, eveningeventphysicalframe, palecommunitytables/bentplychairs/windowbanquette, fourredloungechairsinpublichall-sideglazedfront.'},{'source':'https://m.blog.naver.com/butterfly7s/224431789930','published_utc':'2026-10-05T02:47:50.926Z','files':['sources/current2026-15-contact.jpg','sources/current2026-15-01.jpg','sources/current2026-15-03.jpg'],'observed':'PhysicalFALLINBLACK counterframe; recessed6GNgelatopans5filled1empty,actualsixflavorcards,currentwarmwood/ambercabinetstate.'},{'source':'https://m.blog.naver.com/silveragent/224415944829','published_utc':'2026-09-18T03:55:07.981Z','files':['sources/current2026-20-contact.jpg'],'observed':'Exactbranchfront andgelatoinsertcorroboration.'},{'source':'https://m.blog.naver.com/mongsangteller/223417891331','epoch':'2024-04-17 post2023renewal','files':['sources/branch-20240417-04.jpg','sources/branch-20240417-05.jpg','sources/branch-20240417-15.jpg','sources/branch-20240417-16.jpg'],'use':'Fullphysicalinsert/form study; oldclipboardmenu prices not replayed as current because2026surfaceprinting available.'}], 'official84seat_audit':{'source':'https://www.bsdonggu.go.kr/board/view.donggu?boardId=BBS_0000257&dataSid=5012638&menuCd=DOM_000000313002003004&startPage=1','files':['sources/official-84seats-front.png','sources/official-84seats-interior.png'],'directly_supported':'Attachedphotosshowpre-renewal redCAFFE letters, blackwalls/chandeliers/oldchairlayout unlike2023renewal/current2026photos.','inference':'Sourceimagefilenames1577066534...correspondUnix2019-12-23; uploadepochinferencenotpublicationproof.','classification':'contradicts_premise_that84seats_is_current_layout_authority','action':'No84seatforcedfit, nofootprint/nativechanges. Fourredchairsaddedsolelyfromcurrentphotos; currentfullseatcount remainsunqualified.'},'free_models':[{'asset':'KenneyFoodKit ice-cream-scoop-chocolate.fbx','license':'CC0','source':'https://kenney.nl/assets/food-kit','applied':'20 actual3Dfood instances,5pans photo-colored/reproportioned, onecurrentempty pan preserved; notphototexturegelato'},{'asset':'PolyHaven modern_arm_chair_01.fbx','license':'CC0','source':'https://polyhaven.com/a/modern_arm_chair_01','applied':'4 realfreeassetinstanceswithredcushion/blackframe, originals unchanged','slot_binding_evidence':'SOURCE-FBX-MATERIAL-SLOTS-002.json exact originalFBX slots0legs/1pillow, explicitorderedmaterialbinding instead of names after importerNone'}],'own_counter_aperture':'Only two own001 counter MeshFilter references replaced with localaperture derivatives; exactownobjectID andoldmeshpath guarded, TRS/materials/restgeometry unchanged. No native/floor/wall opening edits.','print_treatment':'Current2026sourcecontent rectified only fordevice/UIprintingandactualphysicalcards/frames; separate solidframes/hardware/emissiveface. NoTMP/font/device-label substitute.','source_spc_limit':'Fourthdisplayrightedge sourcecropped; visibleUI croppedcontent retained, fullpanel dimensions MODEL.','metric_limit':'All new fits/placement MODEL; no surveyed/asbuiltidentity claim, nooriginalexactbranchfloorplan found. Hidden/unseen BOH not built.','no_QA_tests_Play_reopen_capture':True}
(P/'SOURCE-EVIDENCE-FOLLOWUP-002.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2))
(P/'task_plan.md').write_text('# Pascucci R25 followup002\n\n1. Current2026 exactbranch sources and84seat epoch — complete\n2. Physicalgelatoinsert/menus/currentvisible seating packet — complete\n3. Apply/save own002 underC180 — in_progress\n4. Recordactualcompletion andstop — pending\n\nNoQA/Play/reopen/probes/capture; native/neighborfixed.\n')
(P/'progress.md').open('a').write('\nFollowup002 frozenpacket authored: %s newobjects,%s sourceimports,2 owncountermesh replacements foractualrecess; actual20food+4freechairinstances, currentphysicalmenus/screens/cards. SourceFBXexactslots explicitbind added;001packet/scripts untouched.\n'%(len(objs),len(imports)))
print(json.dumps({'new_objects':len(objs),'imports':len(imports),'own_mesh_replacements':len(replacements),'packet_sha256':sha}))
