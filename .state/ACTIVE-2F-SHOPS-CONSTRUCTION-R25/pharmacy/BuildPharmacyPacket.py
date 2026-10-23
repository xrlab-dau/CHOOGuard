from pathlib import Path
import json,hashlib,math,shutil,datetime
import numpy as np
from shapely.geometry import LineString,Point,Polygon
b=Path(__file__).parent;root=Path.cwd();src=b/'sources';assets='Assets/ChooGuard/Art/StationInterior/R25_Shops/pharmacy';sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
# Exact working boundary is reconstructed from current native vectors, never copied from red pen/candidate polygon.
A=np.array([-61.01859347064644,48.13220250844756]);B=np.array([-61.01237249750023,49.40137622911994]);t=(B-A)/np.linalg.norm(B-A);n=np.array([-t[1],t[0]])
F=A+.03*n;R0=np.array([-64.336764,34.808166]);R1=np.array([-64.271598,48.116292]);rt=(R1-R0)/np.linalg.norm(R1-R0);rn=np.array([rt[1],-rt[0]]);R=R0+.03*rn
joint=np.array([-64.40723035904642,40.83953224743903]);south=float(joint@t)
cad=json.loads(Path('.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/cad/r19/CURRENT-LIVE-2F-OBJECT-VECTOR-CAD-R19-NO-TEXT.json').read_text());col=next(o for o in cad['objects'] if o['path'].endswith('NATIVE_COL_066_2F_MODEL'));ps=np.array([p for s in col['sections']['8.40000'] for p in s]);north=float((ps@t).min()-.03)
def intersect_line(p,d,normal,cut):return p+d*((cut-float(p@normal))/float(d@normal))
fs=intersect_line(F,t,t,south);fn=intersect_line(F,t,t,north);rs=intersect_line(R,rt,t,south);rr=intersect_line(R,rt,t,north);L=north-south;D=min(float((rs-fs)@n),float((rr-fn)@n));ring=[fs.tolist(),fn.tolist(),rr.tolist(),rs.tolist(),fs.tolist()];origin=fs;angle=-math.degrees(math.atan2(t[1],t[0]));frontyaw=math.degrees(math.atan2(-n[0],-n[1]))
def q(u,v):return (origin+t*u+n*v).round(7).tolist()
basis={'schema':'chooguard.R25.actual-native-constructed-store-bay.v1','store_key':'pharmacy','status':'SOURCE_VISIBLE_MODEL_DERIVED_FROM_CURRENT_NATIVE_VECTOR_INTERSECTIONS','baseline':'.state/CURRENT-2F-VECTOR-CAD-R19.json','baseline_data_sha256':sha('.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/cad/r19/CURRENT-LIVE-2F-OBJECT-VECTOR-CAD-R19-NO-TEXT.json'),'source_branch':'새부산약국 부산역2F점','source_identity':['2026-05-18 djagytt photo23, immediately adjacent AmericanTrailer','2026-10-06 ellie photo28 direct front, same name/cross/booth','2022-05-09 bbumpbum names BusanStation branch; historical white counter superseded by2026blue'],'anchors':[{'role':'FIXED_NATIVE_RETURN_FACE_EXTENDED','path':'FPSWorld/공식 자료 부산역 역사/PlatformsParking/OfficialStation_-부산역_선로상층부_0','section_y':8.4,'a':A.tolist(),'b':B.tolist(),'inward_offset_m':.03},{'role':'ACTUAL_MAIN_FLOOR_EXTERIOR_RING_INWARD_CLEARANCE','path':'FPSWorld/Architecture/2F/MAIN2F_APPROVED_C53_FLOOR_C54/MAIN2F_C53_FLOOR-PART-02_CLOSED_BODY_MODEL','a':R0.tolist(),'b':R1.tolist(),'inward_offset_m':.03},{'role':'SOURCE_NEIGHBOR_TENANT_DIVIDER_MODEL_PERPENDICULAR_TO_NATIVE_RETURN','native_cladding_joint':joint.tolist(),'ownership':'This pharmacy constructs own south divider; AmericanTrailer preserves same shared boundary, no duplicate'},{'role':'NATIVE066_SOUTH_SUPPORT_FACE_LIMIT','object_id':col['object_id'],'minimum_support_tangent_projection':north+.03,'clearance_m':.03}],'computed_working_footprint_qx_minus_qy':{'type':'Polygon','coordinates':[ring]},'shop_local_origin_q':q(0,0),'shop_local_front_tangent_q':t.tolist(),'shop_local_inward_normal_q':n.tolist(),'front_length_MODEL_m':L,'minimum_depth_MODEL_m':D,'scene_floor_world_y':7,'existing_native_rear_wall_reused_not_duplicated':True,'public_entrance_preserved':'Entire work is south of native066 projected face by30mm; native return/doorway near qY48.10 and east hall remain clear. No west central public entry occupied.','not_restored_old_candidate':True,'actual_fixture_dimensions':'Photo proportional MODEL fitted to structural bay; no original measured fixture/elevation drawing located','all_scene_fixed_references_unchanged':True,'field_measurement_or_99percent_claim':False}
(b/'MODEL-PLACEMENT-BASIS-R25.json').write_text(json.dumps(basis,ensure_ascii=False,indent=2))
# Own stage only. Assets import and derivative material creation happen inside author lease.
posfree=b.parent/'free-assets/pos-terminal';posown=src/'pos-terminal';posown.mkdir(exist_ok=True)
for f in ['POS-Terminal.fbx','AUTHOR-METADATA.json','ACQUISITION.json']:
 shutil.copy2(posfree/f,posown/f)
imports=[]
def imp(p,d):
 imports.append({'source':str(p.resolve()),'destination':d,'sha256':sha(p)});return assets+'/'+d
shelf=imp(src/'store-shelves/SingleFace-Shelf.fbx','Models/SingleFace-Shelf.fbx');cartons=[imp(src/f'three-pill-boxes/MedicineCarton-{i}.fbx',f'Models/MedicineCarton-{i}.fbx') for i in range(3)];bottle=imp(src/'pill-bottle.fbx','Models/PillBottle.fbx');pos=imp(posown/'POS-Terminal.fbx','Models/POS-Terminal.fbx')
label=imp(src/'textures/pill_label.png','Textures/pill_label.png');posbase=imp(posfree/'textures/Image_0.png','Textures/pos_base.png')
mats={
 'charcoal':{'color':[.105,.12,.13,1],'metallic':.32,'smoothness':.42},'white':{'color':[.91,.93,.92,1],'smoothness':.36},'blue':{'color':[.015,.27,.72,1],'smoothness':.44},'steel':{'color':[.58,.64,.66,1],'metallic':.84,'smoothness':.68},'glass':{'color':[.71,.86,.88,.115],'transparent':True,'smoothness':.94},'black':{'color':[.035,.04,.045,1],'smoothness':.38},'emitter':{'color':[.93,.97,1,1],'emission':[1.2,1.25,1.28,1]},'red_emit':{'color':[.76,.005,.025,1],'emission':[.72,0,.01,1]},'carton_white':{'color':[.91,.92,.91,1],'smoothness':.18},'carton_blue':{'color':[.06,.36,.74,1],'smoothness':.23},'carton_yellow':{'color':[.97,.72,.065,1],'smoothness':.2},'carton_pink':{'color':[.89,.26,.46,1],'smoothness':.22},'carton_green':{'color':[.15,.49,.24,1],'smoothness':.2},'carton_orange':{'color':[.91,.40,.11,1],'smoothness':.2},'paperboard':{'color':[.55,.44,.28,1],'smoothness':.1},'amber':{'color':[.54,.21,.025,1],'smoothness':.72},'pilllabel':{'color':[1,1,1,1],'texture_asset':label,'smoothness':.15},'pos':{'color':[1,1,1,1],'texture_asset':posbase,'smoothness':.5}}
objects=[]
def add(kind,id,u,v,y,group,material=None,**kw):
 o={'kind':kind,'id':id,'q':q(u,v),'y':y,'group':group,'source_ids':['CURRENT_FIELD_20261006_PHOTO28'],'qualification':'SOURCE_VISIBLE_MODEL_NOT_MEASURED'}
 if material:o['material']=material
 o.update(kw);objects.append(o);return o
def box(id,u,v,y,w,h,d,material,group,**kw):return add('box',id,u,v,y,group,material,size=[w,h,d],yaw=angle,**kw)
def cylinder(id,u,v,y,r,h,material,group,**kw):return add('cylinder',id,u,v,y,group,material,size=[r*2,h/2,r*2],collider=False,**kw)
def asset(id,u,v,y,path,size,material=None,yaw=None,group='FreeModels',**kw):return add('asset',id,u,v,y,group,material,asset=path,fit_size=size,yaw=angle if yaw is None else yaw,**kw)
# The actual native rear is reused. New booth shell is limited to tenant-owned sides and street facade.
box('South_shared_tenant_divider',.045,D/2,1.47,.08,2.94,D-.10,'charcoal','Booth')
box('North_own_divider_before_native066',L-.055,D/2,1.47,.08,2.94,D-.10,'charcoal','Booth')
box('Fascia_physical_panel',L/2,.105,2.765,L-.12,.59,.15,'charcoal','Booth')
box('Own_ceiling',L/2,D/2,2.94,L-.16,.075,D-.12,'white','Ceiling')
# Open sliding leaves are modeled parked over fixed side panels. Central visitor opening remains physically clear.
sidew=1.46
for side,uc in [('S',sidew/2+.08),('N',L-sidew/2-.08)]:
 box('Fixed_glazing_'+side,uc,.08,1.21,sidew,2.34,.014,'glass','FacadeGlass',cast_shadows=False)
 for uu in [uc-sidew/2,uc+sidew/2]:box('Glazing_stile_'+side+str(uu),uu,.10,1.23,.038,2.42,.052,'steel','FacadeFrames')
 for yy in [.055,2.42]:box('Glazing_rail_'+side+str(yy),uc,.10,yy,sidew,.045,.052,'steel','FacadeFrames')
 box('Open_sliding_leaf_'+side,uc,.145,1.20,sidew-.12,2.30,.012,'glass','FacadeGlass',cast_shadows=False)
 box('Sliding_pull_'+side,uc+(.40 if side=='S' else -.40),.20,1.15,.018,.34,.035,'steel','FacadeHardware',collider=False)
for uu in [.085,L-.085]:box('Front_side_reveal_'+str(uu),uu,.08,1.25,.10,2.5,.15,'charcoal','Booth')
box('Entry_head_track',L/2,.105,2.435,L-.15,.046,.07,'steel','FacadeHardware')
# Source-blue consultation counter, inset from public facade. Its central customer space stays within booth.
cw=L-1.0;cu=cw/2+.25
box('Counter_closed_blue_casework',cu,1.05,.50,cw,.87,.58,'blue','Counter')
box('Counter_white_top',cu,1.05,.973,cw+.04,.075,.65,'white','Counter')
box('Counter_steel_toe',cu,.754,.095,cw-.05,.105,.025,'steel','Counter')
for i in range(7):
 uu=.25+cw*i/7
 box('Counter_editable_door_'+str(i),uu+cw/14,1.348,.49,cw/7-.015,.69,.026,'blue','Counter')
 box('Counter_handle_'+str(i),uu+cw/14,1.369,.74,.18,.012,.017,'steel','Counter',collider=False)
# Downloaded detailed five-tier metal shelf geometry is fitted to the observed white pharmacy grid.
cols=7;sw=(L-.34)/cols
for c in range(cols):
 uc=.17+(c+.5)*sw
 asset('Free_shelf_module_'+str(c),uc,D-.30,.13,shelf,[sw-.055,2.12,.43],'white',group='DownloadedShelves')
 box('White_casework_upright_'+str(c),.17+c*sw,D-.245,1.21,.028,2.17,.47,'white','ShelfGrid')
 box('Observed_blue_shelf_header_'+str(c),uc,D-.515,2.30,sw,.13,.045,'blue','ShelfGrid')
 # Product objects stay individually editable; imported beveled cartons rather than name-text blocks.
 levels=[.40,.77,1.14,1.51,1.88]
 for row,yy in enumerate(levels):
  for k in range(5):
   u=uc-sw/2+.115+k*(sw-.23)/4;idx=(c+row+k)%3;color=['carton_white','carton_blue','carton_yellow','carton_pink','carton_green','carton_orange'][(c*3+row+k)%6]
   w=.105+(k%2)*.026;h=.19+(row%2)*.04;dd=.11
   asset(f'Medicine_carton_{c}_{row}_{k}',u,D-.49,yy,cartons[idx],[w,h,dd],color,group='EditableMedicineProducts',collider=False,source_ids=['CURRENT_FIELD_20261006_PHOTO28','FREE_IPAU_THREE_PILL_BOXES'])
   # Source packaging color bands are physical folded-carton surface details, with no substitute object text.
   box(f'Carton_color_band_{c}_{row}_{k}',u,D-.547,yy+h*.38,w*.92,h*.17,.0012,['blue','white'][idx%2],'PackagingSurfaceDetails',collider=False)
 # Closed cartons stacked above the shelf wall are visible in2026 source.
 for stack in range(2):
  asset(f'Upper_stock_carton_{c}_{stack}',uc+(-.15 if stack==0 else .17),D-.31,2.395,cartons[(c+stack)%3],[.24,.15,.18],['carton_blue','carton_yellow','carton_white'][c%3],group='EditableMedicineProducts',collider=False)
box('Shelf_grid_last_upright',L-.17,D-.245,1.21,.028,2.17,.47,'white','ShelfGrid')
# Source side shelves: small products at glazed returns without occupying circulation.
for si,uc in enumerate([.27,L-.27]):
 for r,yy in enumerate([.29,.66,1.03,1.40,1.77]):
  box(f'Side_glass_shelf_{si}_{r}',uc,.65,yy,.32,.018,.73,'glass','SideDisplays',collider=False)
  for k in range(3):
   asset(f'Side_carton_{si}_{r}_{k}',uc,.40+k*.20,yy+.02,cartons[(r+k)%3],[.095,.17,.075],['carton_green','carton_pink','carton_yellow'][(r+k)%3],group='EditableMedicineProducts',collider=False)
# Actual downloaded closed pill bottles and detailed POS placed on consultation surface.
for i in range(14):
 u=.42+i*.145
 asset('Downloaded_pill_bottle_'+str(i),u,.96,1.017,bottle,[.065,.116,.065],yaw=frontyaw,group='DownloadedMedicineBottles',collider=False,material_map={'pill-bottle_cap':'white','pill-bottle_label':'pilllabel','pill-bottle_tablets':'white','pill-bottle_vial':'amber','*':'white'},source_ids=['CURRENT_FIELD_20261006_PHOTO28','FREE_GRAB3D_PILL_BOTTLE_CC0'])
asset('Downloaded_POS_terminal',cw-.43,1.07,1.018,pos,[.34,.38,.31],yaw=frontyaw+180,group='DownloadedPOS',material_map={'Terminal001_Material #2100454184_0':'pos','*':'black'},source_ids=['CURRENT_FIELD_20261006_PHOTO28','FREE_POS_V_YAROVYI'])
# Clearly visible recessed cassette AC, louvres, downlights and wall fan.
box('Ceiling_AC_frame',L/2,1.52,2.855,.84,.095,.84,'white','CeilingEquipment')
box('AC_recess',L/2,1.52,2.796,.58,.022,.58,'black','CeilingEquipment')
for i in range(13):box('AC_grille_blade_'+str(i),L/2-.27+i*.045,1.52,2.777,.014,.025,.54,'white','CeilingEquipment',collider=False)
for u in [L*.22,L*.52,L*.80]:
 for v in [.64,2.35]:
  cylinder('Downlight_trim_'+str(u)+str(v),u,v,2.88,.085,.024,'steel','Lighting')
  cylinder('Downlight_diffuser_'+str(u)+str(v),u,v,2.864,.068,.014,'emitter','Lighting')
  add('light','Warm_observed_downlight_'+str(u)+str(v),u,v,2.72,'Lighting',range=2.7,intensity=.32,color=[1,.97,.92,1])
# Wall mounted fan: detailed physically editable frame rings, hub and swept blades. Not an inferred hidden prep machine.
fan_u=.39;fan_v=D-.55;fy=2.46
box('Fan_wall_mount',fan_u,D-.10,fy,.14,.14,.035,'white','VisibleWallFan',collider=False)
box('Fan_arm',fan_u,D-.30,fy-.09,.05,.07,.40,'white','VisibleWallFan',collider=False)
# Circular cage in localXY normal+Z oriented outward into shop.
verts=[];tris=[]
for i in range(48):
 a=i*2*math.pi/48;b2=(i+1)*2*math.pi/48
 for zz in [-.01,.01]:
  for r in [.206,.214]:verts.append([r*math.cos(a),r*math.sin(a),zz])
 # ring segments independently form closed sections
 for i2 in range(4):
  pass
# Use closed torus cage library.
def torus(R,r):
 vs=[];ts=[]
 for i in range(48):
  a=2*math.pi*i/48
  for k in range(6):
   z=2*math.pi*k/6;vs.append([(R+r*math.cos(z))*math.cos(a),(R+r*math.cos(z))*math.sin(a),r*math.sin(z)])
 for i in range(48):
  for k in range(6):
   a=i*6+k;b=i*6+(k+1)%6;c=((i+1)%48)*6+(k+1)%6;d=((i+1)%48)*6+k;ts.extend([a,b,c,a,c,d])
 return {'vertices':vs,'triangles':ts}
lib={'fan_cage':torus(.211,.006),'fan_hub':torus(.048,.015)}
for key in ['fan_cage','fan_hub']:add('library_mesh','Fan_'+key,fan_u,fan_v,fy,'VisibleWallFan','white',mesh_key=key,yaw=frontyaw,collider=False)
for i in range(12):
 a=math.pi*i/6
 # Straight physical cage spokes in vertical sign plane.
 pts=np.array([[0,0],[.209*math.cos(a),.209*math.sin(a)]])
 mid=(pts[0]+pts[1])/2;u=fan_u+mid[0];y=fy+mid[1]
 add('box','Fan_cage_spoke_'+str(i),u,fan_v,y,'VisibleWallFan','steel',size=[.211,.003,.003],euler=[0,frontyaw,math.degrees(a)],collider=False)
for i in range(3):
 a=2*math.pi*i/3;vs=[]
 outline=[(.035,-.018),(.12,-.049),(.19,-.018),(.173,.048),(.08,.062)]
 for zz in [-.008,.008]:
  for x,y in outline:vs.append([x*math.cos(a)-y*math.sin(a),x*math.sin(a)+y*math.cos(a),zz])
 tr=[0,2,1,0,3,2,0,4,3,5,6,7,5,7,8,5,8,9]
 for k in range(5):kk=(k+1)%5;tr.extend([k,kk,kk+5,k,kk+5,k+5])
 add('mesh','Fan_swept_blade_'+str(i),fan_u,fan_v+.012,fy,'VisibleWallFan','white',vertices=vs,triangles=tr,yaw=frontyaw,collider=False)
# The latest actual source Hangul and cross are closed beveled solids. No font/TMP/sign-plane.
signs=json.loads((b/'sign-model-authoring/SOURCE-TRACED-SIGN-BEVELED.json').read_text())
for key,uu,ma in [('brand',L/2+.31,'emitter'),('cross_red',L/2-1.33,'red_emit'),('cross_white',L/2-1.33,'emitter')]:
 ss=signs[key];add('mesh','Source_traced_beveled_'+key,uu,.008 if key!='cross_white' else -.006,2.765,'PhysicalSourceSign',ma,vertices=ss['vertices'],triangles=ss['triangles'],yaw=frontyaw,collider=False,source_ids=['CURRENT_FIELD_20261006_PHOTO28','ACTUAL_SOURCE_SIGN_MASK'])
# Custom ordinary ancillary storage cartons visible below side shelves, within booth.
for i in range(3):box('Visible_stock_shipping_carton_'+str(i),.48+i*.22,1.42,.15,.20,.29,.21,'paperboard','VisibleStock',collider=False)
packet={'store_key':'pharmacy','origin_q':q(0,0),'floor_world_y':7,'source_packet':str(b/'SOURCE-EVIDENCE-R25.json'),'materials':mats,'mesh_library':lib,'source_imports':imports,'objects':objects,'placement_basis':str(b/'MODEL-PLACEMENT-BASIS-R25.json'),'hidden_BOH_detail':'Not reconstructed; hidden dispensing machine/equipment unqualified and absent','all_dimensions_model':True,'text_components':False,'existing_native_rear_wall_reused':True}
(b/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2))
# Evidence/attribution packet distinguishes exact current field photo from generic free component source.
sources=[]
for uid,epoch,local,url in [('CURRENT_FIELD_20261006_PHOTO28','2026-10-06','ellie-28.jpg','https://m.blog.naver.com/ellie0116/224433385586'),('CURRENT_FIELD_20260518_PHOTO23','2026-05-18','djagytt-23.jpg','https://m.blog.naver.com/djagytt/224289573890'),('HISTORICAL_BRANCH_20220509','2022-05-09','bbumpbum-01.jpg','https://m.blog.naver.com/bbumpbum/222725829482')]:sources.append({'id':uid,'source_url':url,'epoch':epoch,'local':str(src/local),'directly_viewed':True,'scope':'Exact BusanStation2F branch;2022counterfinish historical, superseded by2026blue.','license':'Author retains photo copyright; evidence/reference only, not redistributed as full sign/fixtures planes'})
used=[{'id':'FREE_GRAB3D_PILL_BOTTLE_CC0','source':'https://grab3d.com/models/props/pill-bottle/','author':'Grab3D','license':'CC0-1.0','local':str(src/'pill-bottle.glb'),'derived':'pill-bottle.fbx','changes':'Own source texture/material; fitted0.116m bottle; no medication identity claim'},{'id':'FREE_POS_V_YAROVYI',**json.loads((posown/'ACQUISITION.json').read_text()),'local':str(posown/'POS-Terminal.fbx'),'changes':'Own imported source, fit to photo-visible point-of-sale terminal'}]
for name,id in [('store-shelves','FREE_PETER_D_STORE_SHELVES'),('three-pill-boxes','FREE_IPAU_THREE_PILL_BOXES')]:
 a=json.loads((src/name/'ACQUISITION.json').read_text());used.append({'id':id,**a,'local':str(src/name/(name+'.glb')),'changes':'Own independently imported derivative; source geometry kept, realistic fit and photo-based paint/packaging colors; model product label identities not transferred','source_preview_directly_viewed':True})
evidence={'schema':'chooguard.source-evidence.R25.v1','store_key':'pharmacy','actual_branch':'새부산약국 부산역2층점, central public hall AmericanTrailer-adjacent branch','field_sources':sources,'free_model_sources':used,'rejected_downloads':[{'source':str(src/'medicine-carton/medicine-carton.glb'),'reason':'Futuristic emergency crate shape unlike observed merchandise, not placed'},{'source':str(src/'medicines/medicines.glb'),'reason':'Unrelated foreign/toxic printed bottle/syringe, not placed'}],'original_design_search':'Source-index official2F store plans establish identity/neighborhood. Exact current branch furniture/elevation/ceiling dimensions not located; field-photo visible construction qualified MODEL.','directly_viewed_images':['sources/ellie-28.jpg','sources/djagytt-23.jpg','sources/djagytt-21.jpg','sources/bbumpbum-01.jpg','sources/store-shelves/source-preview.jpg','sources/three-pill-boxes/source-preview.jpg','sign-model-authoring/letters-mask.png'],'source_sign_method':str(b/'sign-model-authoring/SOURCE-SIGN-PROVENANCE.json'),'limitations':['Exact product inventory, unseen BOH and dispensing machinery not established.','No field measured absolute fixture dimensions.','No runtime/visual QA by agents; user validates.','No99percent or complete2F claim.']}
(b/'SOURCE-EVIDENCE-R25.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2))
license_text='R25 pharmacy free-model attribution\n\n'
for a in used:license_text+=json.dumps(a,ensure_ascii=False,indent=2)+'\n\n'
(src/'ATTRIBUTION-USED-MODELS.md').write_text(license_text)
scope='C180_R25_PHARMACY_APPLY_001';template=(b.parent/'StoreBuilderR25-v3-FREE-ASSETS.template.cs').read_text();rep={'__STORE_KEY__':'pharmacy','__WORKER_THREAD__':'classic:/root/r25_pharmacy','__EXACT_SCOPE__':scope,'__PACKET_PATH__':str(b/'STORE-PACKET-R25.json'),'__PACKET_SHA256__':sha(b/'STORE-PACKET-R25.json')}
for a,v in rep.items():template=template.replace(a,v)
# asset imports with materialImportMode.None clear source slots; renderer maps keep physical parts distinct.
(b/'ApplyStoreR25.cs').write_text(template)
(b/'FROZEN-APPLY-PACKET-R25.json').write_text(json.dumps({'scope':scope,'script':str(b/'ApplyStoreR25.cs'),'script_sha256':sha(b/'ApplyStoreR25.cs'),'packet':str(b/'STORE-PACKET-R25.json'),'packet_sha256':sha(b/'STORE-PACKET-R25.json'),'object_count':len(objects),'free_asset_imports':len(imports),'no_text':all(x['kind']!='text' for x in objects),'native_broker_only':True},indent=2))
(b/'plan/task_plan.md').write_text('# R25 pharmacy construction\n\n1. Exact branch source + current native footprint — complete\n2. Licensed source models + source physical sign + frozen construction packet — complete\n3. FIFO C180 apply and save; actual receipt and qualified completion — in_progress\n\nNo agent QA/capture/probe/play/reopen/tests. Own state/Assets only.\n')
(b/'plan/progress.md').write_text('# Progress\n\nCurrent exact branch2026photo28 inspected. NativeR19 vector intersections form bay; AmericanTrailer author shares south boundary and avoidsduplicate. Actual free shelf/cartons/POS/bottles staged, physical sourceoutlined beveled sign complete. Immutable packet constructed; pendingC180FIFOapply/save.\n')
print('FROZEN',len(objects),'objects','dimensions',L,D,'ring',ring,'signyaw',frontyaw,'regularangle',angle)
