from pathlib import Path
import json,hashlib,math,sys
import numpy as np,cv2
r=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25').resolve();o=r/'storyway-north';s=o/'sources';sys.path.insert(0,str(r));from photo_outline_to_solid import closed_solid_from_mask
# Actual body sections produce native axes and clear faces; source map boxes never used.
cad=json.loads((o/'LOCAL-ACTUAL-CAD-REFERENCE-R25.json').read_text()); cols=[]
for no in ['029','028']:
 obj=next(x for x in cad['objects'] if f'NATIVE_COL_{no}_2F_MODEL' in x['path']);pts=np.array([p for seg in obj['sections']['8.40000'] for p in seg]);center=(pts.min(axis=0)+pts.max(axis=0))/2;cols.append((obj,pts,center))
base=cols[0][2];u=cols[1][2]-base;u=u/np.linalg.norm(u);v=np.array([-u[1],u[0]]);uvs=[np.column_stack(((p-base)@u,(p-base)@v)) for _,p,_ in cols]
x0=float(uvs[0][:,0].max())+.06;x1=float(uvs[1][:,0].min())-.06;z0=max(float(x[:,1].max()) for x in uvs)+.08
# Published 42.0 m2 is independent area reference, not source pixel depth. Our model net width consumes no column faces.
z1=z0+42.0/(x1-x0); W=x1-x0;D=z1-z0
q=lambda x,z: [float(t) for t in base+u*(x0+x)+v*(z0+z)]
yaw=-math.degrees(math.atan2(u[1],u[0]));origin=q(W/2,D/2)
basis={'status':'ACTUAL_COLUMN_FACE_BOUNDED_PHOTO_REFERENCED_MODEL_NOT_SURVEY','source':str(o/'LOCAL-ACTUAL-CAD-REFERENCE-R25.json'),'native_axis_base_q':base.tolist(),'u':u.tolist(),'v':v.tolist(),'actual_column_face_anchors':[{'path':ob['path'],'object_id':ob['object_id'],'center_q':c.tolist(),'uv_extrema':[[float(uv[:,i].min()),float(uv[:,i].max())] for i in [0,1]]} for (ob,p,c),uv in zip(cols,uvs)],'working_uv':[x0,z0,x1,z1],'working_polygon_q':[q(0,0),q(W,0),q(W,D),q(0,D),q(0,0)],'model_width_m':W,'model_depth_m':D,'area_reference_m2':42.0,'area_role':'Published commercial area constrains MODEL depth only, not measured lease boundary. No old polygon replay.','front':'North +v facing already saved Pascucci, source photo reflection directly binds branch. East return has visible glazed end entry.','north_clearance_to_Pascucci':'Pascucci current front qY87.34; own maximum qY79.02 leaves approximately8.3m hall. No Pascucci edit.','south_clearance':'Back south +80mm beyond actual column northface; actual C97 escalator max qY71.737 stays outside and below our south edge qY72.98.','fixed_regions':'Only own near-column bay used; no protectedOfficial6 room here, no existing native enclosed room within own polygon in current sections. No native/floor/column/circulation edits.','shell_qualification':'MODEL enclosure fitted to actual clear native faces and source glazed corner relationships; no individual surveyed wall correspondence claimed.','source_pixels_used_for_metric_geometry':False,'old_R21_R22_polygon_replayed':False}
(o/'MODEL-PLACEMENT-BASIS-R25.json').write_text(json.dumps(basis,ensure_ascii=False,indent=2))
# Own actual north branch sign, independent from east branch. Rectification is for sign outline only.
im=cv2.imread(str(s/'daytour_88-09.jpg'));src=np.float32([[405,150],[640,155],[640,215],[405,210]]);dst=np.float32([[0,0],[939,0],[939,239],[0,239]]);rect=cv2.warpPerspective(im,cv2.getPerspectiveTransform(src,dst),(940,240));hsv=cv2.cvtColor(rect,cv2.COLOR_BGR2HSV);mask=cv2.inRange(hsv,np.array([0,0,230]),np.array([179,180,255]))
n,lab,stats,cent=cv2.connectedComponentsWithStats(mask);clean=np.zeros_like(mask);sign_parts=[]
for i in range(1,n):
 if stats[i,cv2.CC_STAT_AREA]>60:
  component=np.zeros_like(mask);component[lab==i]=255
  if cent[i][0]<180:
   contours,_=cv2.findContours(component,cv2.RETR_EXTERNAL,cv2.CHAIN_APPROX_SIMPLE);cv2.drawContours(component,contours,-1,255,-1)
  clean[component>0]=255
  part=closed_solid_from_mask(component,2.35,.60,.035,minimum_area=60,epsilon=.6);part['centroid_px']=cent[i].tolist();sign_parts.append(part)
sign=o/'sign-model-authoring';sign.mkdir(exist_ok=True);cv2.imwrite(str(sign/'NORTH-BRANCH-SIGN-RECTIFIED.jpg'),rect);cv2.imwrite(str(sign/'NORTH-BRANCH-SIGN-MASK.png'),clean)
solid={'source_polygons':sum(x['source_polygons'] for x in sign_parts),'parts':sign_parts,'method':'Own exact north-branch 20240620 field photo high-core luminous ink trace with five separate physical dots, smile and eight letter outlines; original holes retained.'};(sign/'NORTH-BRANCH-PHOTO-SOLID-SIGN.json').write_text(json.dumps(solid))
# A second face uses the same actual source brand; no layout copied.
pimp=json.loads((o/'PREPARED-SOURCE-IMPORTS.json').read_text());models=pimp['models'];t=pimp['textures'];m={
'charcoal':{'color':[.025,.033,.036,1],'metallic':.28,'smoothness':.44},'black':{'color':[.01,.012,.014,1],'smoothness':.4},'white':{'color':[.91,.92,.90,1],'smoothness':.35},'tile':{'color':[.83,.82,.76,1],'smoothness':.52},'grout':{'color':[.48,.49,.47,1],'smoothness':.15},'orange':{'color':[.95,.54,.018,1],'smoothness':.6},'yellow':{'color':[.98,.67,.03,1],'smoothness':.52},'silver':{'color':[.59,.63,.64,1],'metallic':.9,'smoothness':.66},'glass':{'color':[.78,.89,.9,.08],'smoothness':.92,'transparent':True},'frosted':{'color':[.65,.71,.72,.6],'smoothness':.4,'transparent':True},'warmLED':{'color':[1,.98,.86,1],'emission':[1.45,1.42,1.14,1]},'signGlow':{'color':[.88,1,.72,1],'emission':[.72,1,.5,1]},'coolLED':{'color':[.9,.98,1,1],'emission':[1.25,1.4,1.5,1]},'shelf_enamel':{'color':[.93,.94,.91,1],'metallic':.35,'smoothness':.45},'shelf_rail':{'color':[.60,.65,.14,1],'smoothness':.33},
'fridge_case':{'color':[.15,.17,.18,1],'metallic':.65,'smoothness':.56,'texture_asset':t['fridge_base'],'metallic_gloss_asset':t['fridge_metal']},'fridge_inside':{'color':[.9,.92,.92,1],'metallic':.3,'smoothness':.45,'texture_asset':t['fridge_inside'],'metallic_gloss_asset':t['fridge_inside_metal']},'pos_main':{'color':[1,1,1,1],'texture_asset':t['pos_base'],'normal_asset':t['pos_normal'],'metallic':.1,'smoothness':.5},'pos_trim':{'color':[.07,.08,.09,1],'normal_asset':t['pos_normal2'],'metallic':.65,'smoothness':.6},'water_source':{'color':[1,1,1,1],'texture_asset':t['water_base'],'normal_asset':t['water_normal'],'smoothness':.6},'stock_source':{'color':[1,1,1,1],'texture_asset':t['kenney'],'smoothness':.34},'counter_white':{'color':[.85,.86,.81,1],'smoothness':.4},'wood':{'color':[.53,.36,.2,1],'smoothness':.33},'paper':{'color':[.85,.84,.77,1],'smoothness':.15}}
objs=[]
def add(kind,id,x,z,y=0,group='SourceVisibleFitout',source='NORTH_FIELD',**kw):
 a={'kind':kind,'id':id,'q':q(x,z),'y':y,'yaw':yaw,'group':group,'source_ids':[source],'qualification':'PHOTO_REFERENCED_MODEL_NOT_FIELD_MEASUREMENT'};a.update(kw);objs.append(a);return a
def box(id,x,z,y,size,mat,group='BoothPhysicalConstruction',**kw):return add('box',id,x,z,y,group=group,size=size,material=mat,**kw)
def asset(id,key,x,z,y,fit,map,yawplus=0,collider=True,group='DownloadedSourceModels'):
 return add('asset',id,x,z,y,group=group,asset=models[key],fit_size=fit,material_map=map,yaw=yaw+yawplus,collider=collider)
def edge(id,a,b,height,thickness,mat,bottom=0,group='BoothPhysicalConstruction'):
 objs.append({'kind':'edge','id':id,'a':q(*a),'b':q(*b),'height':height,'bottom':bottom,'thickness':thickness,'material':mat,'group':group,'source_ids':['NORTH_FIELD'],'qualification':'PHOTO_REFERENCED_MODEL_NOT_FIELD_MEASUREMENT'})
# No new station floor; 12mm finish is movable own store finish over accepted floor.
box('Own_Ceramic_Finish',W/2,D/2,.006,[W-.12,.012,D-.12],'tile')
for x in np.arange(.60,W,.60):box(f'Tile_Grout_X_{x:.2f}',x,D/2,.013,[.006,.002,D-.12],'grout',collider=False)
for z in np.arange(.60,D,.60):box(f'Tile_Grout_Z_{z:.2f}',W/2,z,.013,[W-.12,.002,.006],'grout',collider=False)
# Opaque back and westreturn, glazed northface and east entry; own shell never overlaps column bodies.
edge('South_Rear_Enclosure',(0,0),(W,0),3.05,.08,'charcoal')
edge('South_Interior_Orange_Wall',(.06,.065),(W-.06,.065),2.74,.025,'orange')
edge('West_Opaque_Return',(0,0),(0,D),3.05,.08,'charcoal')
edge('East_Interior_Glass_Return',(W,.06),(W,D-1.5),2.57,.018,'glass')
edge('North_Glass_Face',(.04,D),(W-.04,D),2.57,.018,'glass')
# East end aperture remains truly open from zD-1.5 toD-.08, with one retracted door leaf.
edge('East_Entry_Open_Leaf',(W-.055,D-1.53),(W-.055,D-.88),2.40,.016,'glass')
for x in np.linspace(.03,W-.03,6):box(f'North_Aluminium_Mullion_{x:.2f}',x,D,1.29,[.045,2.58,.07],'charcoal')
for z in [0.08,D-1.52,D-.08]:box(f'East_Entry_Jamb_{z:.2f}',W,z,1.29,[.06,2.58,.045],'charcoal')
for z in np.arange(1.0,D-1.52,1.12):box(f'East_Glass_Mullion_{z:.2f}',W,z,1.29,[.06,2.58,.04],'charcoal')
for z in [.04,D]:box(f'North_Bottom_Track_{z:.2f}',W/2,z,.045,[W,.09,.07],'charcoal')
box('East_Automatic_Door_Head',W,D-.8,2.52,[.13,.13,1.55],'charcoal')
box('North_Flat_Fascia',W/2,D,2.84,[W,.62,.16],'charcoal')
box('East_Flat_Fascia',W,D/2,2.84,[.16,.62,D],'charcoal')
# Own solid sign fronts to actual hall (+qY and +qX), source letter counters retained.
m['dot_teal']={'color':[.47,.83,.68,1],'emission':[.3,.67,.44,1]};m['dot_yellow']={'color':[.97,.91,.15,1],'emission':[1,.78,.05,1]}
for i,part in enumerate(sign_parts):
 cx,cy=part['centroid_px'];mat='dot_teal' if cx<180 and cy<96 and cx<85 else 'dot_yellow' if cx<180 and cy<96 else 'signGlow'
 add('mesh',f'North_Photo_Outline_Part_{i}',W*.48,D+.095,2.84,group='PhysicalSourceOutlineSigns',vertices=part['vertices'],triangles=part['triangles'],material=mat,yaw=yaw,collider=False)
 add('mesh',f'East_Photo_Outline_Part_{i}',W+.095,D*.64,2.84,group='PhysicalSourceOutlineSigns',vertices=part['vertices'],triangles=part['triangles'],material=mat,yaw=yaw+90,collider=False,size=[.88,.88,1])
for x in [W*.48-.9,W*.48+.9]:box(f'North_Sign_Mount_{x:.2f}',x,D+.085,2.84,[.04,.12,.06],'silver','PhysicalSourceOutlineSigns',collider=False)
# White exposed T-bar acoustic ceiling, alternating luminous panels.
box('Ceiling_Suspension_Frame',W/2,D/2,3.14,[W,.06,D],'white')
for ix,x in enumerate(np.arange(.32,W,.61)):
 for iz,z in enumerate(np.arange(.32,D,.61)):
  if x+.29>W-.02 or z+.29>D-.02:continue
  box(f'Acoustic_Ceiling_Tile_{ix}_{iz}',x,z,3.09,[.59,.025,.59],'warmLED' if (ix+iz)%4==0 else 'white','SourceVisibleCeiling',collider=False)
# Refrigeration is an actual downloaded fridge derivative, transparent glass and per-origin slots.
fm={'FridgeCase':'fridge_case','FridgeDoor':'fridge_case','FridgeDoorInner':'fridge_inside','FridgeGlass':'glass','FridgeInterior':'fridge_inside'}
for i,x in enumerate([3.7,4.55,5.40,6.25]):asset(f'Cool_Drink_Source_Cabinet_{i}','fridge',x,.52,.015,[.82,2.02,.71],fm)
# Open FAST FOOD section derived using downloaded retail shelf shape; side panels, toe grille and LED directly constructed.
for i,x in enumerate([.73,1.72,2.71]):
 asset(f'Open_FastFood_Refrigerated_Source_Shelf_{i}','shelf',x,.53,.16,[.96,1.78,.65],{'mat_shelf':'shelf_enamel','mat_shelf_glass':'shelf_rail'})
 box(f'Open_Chiller_Compressor_Base_{i}',x,.51,.15,[.97,.27,.69],'white','RefrigerationSourceAdaptation')
 box(f'Open_Chiller_Upper_Canopy_{i}',x,.5,2.04,[.97,.11,.68],'charcoal','RefrigerationSourceAdaptation')
 for yy in [.29,.68,1.07,1.46,1.85]:box(f'Open_Chiller_Shelf_LED_{i}_{yy}',x,.84,yy,[.91,.018,.018],'coolLED','RefrigerationSourceAdaptation',collider=False)
# Rear photo yellow/orange category band is a physical printed band; no generic typography standin.
box('Rear_Yellow_Upper_Band',W/2,.12,2.44,[W-.18,.56,.045],'orange','RefrigerationSourceAdaptation')
# Central low grocery aisles, source geometry retained; source-image layout independently inferred within clear aisles.
for col,x in enumerate([1.65,3.38]):
 for seg,z in enumerate([2.65,3.66]):
  for side,plus in enumerate([0,180]):asset(f'Central_Stocked_Gondola_{col}_{seg}_{side}','shelf',x+(.20 if side else -.20),z,.015,[.95,1.48,.36],{'mat_shelf':'shelf_enamel','mat_shelf_glass':'shelf_rail'},yawplus=90+plus)
# Checkout source cabinet + actual POS at east entry far side, retaining wide entrance route.
asset('Own_Source_Checkout_Counter','counter',5.94,2.62,.015,[1.35,.88,.6],{'wire_160160160':'counter_white'},yawplus=90)
asset('Source_POS_Terminal','pos',5.96,2.59,.91,[.20,.25,.23],{'Material_2100454184':'pos_main','Material_2100454185':'pos_trim'},yawplus=90)
# Entry accessory/newspaper wire rack built from physical rods, not flat billboard merchandise.
for x in [4.57,5.14]:
 for z in [D-.49,D-.91]:box(f'Entry_Rack_Upright_{x}_{z}',x,z,.95,[.018,1.87,.018],'silver','PhysicalWireAccessories',collider=False)
for yy in [.12,.48,.84,1.20,1.56,1.90]:box(f'Entry_Rack_Shelf_{yy}',4.85,D-.69,yy,[.59,.018,.45],'white','PhysicalWireAccessories',collider=False)
for x in np.arange(4.58,5.15,.065):box(f'Entry_Rack_Wire_{x}',x,D-.49,.98,[.004,1.83,.004],'silver','PhysicalWireAccessories',collider=False)
# Downloaded actual product meshes are category models, no exact SKU or inventory claim.
for col,x in enumerate([1.65,3.38]):
 for seg,z in enumerate([2.65,3.66]):
  for shelf,yy in enumerate([.18,.48,.78,1.08,1.34]):
   for k in range(6):
    px=x-.29+(k%2)*.58;pz=z-.39+(k//2)*.38;key=['bag-flat','cup','chocolate-wrapper','soda-can','soda-bottle'][(shelf+col+seg)%5]
    fit={'bag-flat':[.15,.21,.065],'cup':[.12,.12,.12],'chocolate-wrapper':[.10,.15,.025],'soda-can':[.065,.125,.065],'soda-bottle':[.07,.23,.07]}[key]
    asset(f'Visible_Packaged_Goods_{col}_{seg}_{shelf}_{k}',key,px,pz,yy,fit,{'colormap':'stock_source'},collider=False,group='DownloadedEditableMerchandise')
for bank,xbase in enumerate([.73,1.72,2.71,3.7,4.55,5.40,6.25]):
 for row,yy in enumerate([.32,.69,1.06,1.43,1.77]):
  for k in range(5):
   key='water' if bank>2 and row%2==0 else 'soda-bottle' if bank>2 else 'cup'
   fit=[.065,.23,.065] if key!='cup' else [.12,.12,.12]
   asset(f'Refrigerated_Physical_Stock_{bank}_{row}_{k}',key,xbase-.29+k*.145,.64,yy,fit,{'BottleMat':'water_source'} if key=='water' else {'colormap':'stock_source'},collider=False,group='DownloadedEditableMerchandise')
for shelf,yy in enumerate([.16,.52,.88,1.24,1.6]):
 for k in range(3):asset(f'Entry_Accessories_Physical_Pack_{shelf}_{k}','bag-flat',4.65+k*.18,D-.64,yy,[.14,.23,.07],{'colormap':'stock_source'},collider=False,group='DownloadedEditableMerchandise')
packet={'store_key':'storyway-north','origin_q':origin,'floor_world_y':7.0,'source_packet':'SOURCE-EVIDENCE-R25.json','source_imports':pimp['imports'],'materials':m,'objects':objs,'limits':['No original current branch floor/ceiling/elevation/furniture plans obtained.','42m2 depth, fixture dimensions, number of visible merchandise facings and exact shelving fit are MODEL, not surveyed.','No hidden BOH or unobserved exits invented.','Source free models use generic identity, no exact source SKU claim.','Observed seasonal decorations, exterior lucky-box vending and suitcases not placed in hall; exact models/sourceepoch unresolved.','Ceiling HVAC actual downloaded matching asset not yet established; not replaced by label.','User validates; no agent QA/capture/Play/probe/reopen.']}
(o/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2));sha=hashlib.sha256((o/'STORE-PACKET-R25.json').read_bytes()).hexdigest()
template=(r/'StoreBuilderR25-v4-SOURCE-MATERIALS.template.cs').read_text();repls={'__STORE_KEY__':'storyway-north','__WORKER_THREAD__':'classic:/root/r25_storyway_north','__EXACT_SCOPE__':'C180_R25_STORYWAY_NORTH_APPLY_001','__PACKET_PATH__':str(o/'STORE-PACKET-R25.json'),'__PACKET_SHA256__':sha}
for k,val in repls.items():template=template.replace(k,val)
(o/'ApplyStoreR25.cs').write_text(template)
print(json.dumps({'objects':len(objs),'assets':sum(x['kind']=='asset' for x in objs),'sign_polygons':solid['source_polygons'],'native_width':W,'model_depth':D,'packet_sha256':sha},indent=2))
