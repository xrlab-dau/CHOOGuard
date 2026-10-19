from pathlib import Path
import json,hashlib,math,sys,shutil
import numpy as np,cv2
r=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25').resolve();o=r/'storyway-southwest';s=o/'sources';sys.path.insert(0,str(r));from photo_outline_to_solid import closed_solid_from_mask
cad=json.loads((o/'LOCAL-ACTUAL-CAD-REFERENCE-R25.json').read_text());wall=next(x for x in cad['objects'] if '015_P1_1' in x['path']);div=next(x for x in cad['objects'] if '016_P1_1' in x['path']);rear=next(x for x in cad['objects'] if '017_P2_1' in x['path'])
# Faces taken from full current sections, not neighborhood box/published area.
ss=wall['sections']['8.40000'];xs=[a[0] for seg in ss for a in seg if -31<a[1]<-24.8];x0=max(x for x in xs if x<-50.9)
ds=div['sections']['8.40000'];x1=min(a[0] for seg in ds for a in seg)
z0=max(a[1] for seg in rear['sections']['8.40000'] for a in seg if a[1]<-30.2)
front=[a[1] for seg in ss for a in seg if -24.67<a[1]<-24.64];z1=min(front);W=x1-x0;D=z1-z0
q=lambda x,z:[float(x0+x),float(z0+z)]
origin=q(W/2,D/2);source='EXACT_SW_SEPT2026'
basis={'status':'ACTUAL_CURRENT_FACE_BOUNDED_SOURCE_PHOTO_MODEL','source':cad['source'],'source_sha256':cad['source_sha256'],'anchors':[{'object_id':x['object_id'],'path':x['path'],'mesh_asset':x['mesh_asset']} for x in [wall,div,rear]],'derived_clear_faces_q':[x0,z0,x1,z1],'working_polygon_q':[q(0,0),q(W,0),q(W,D),q(0,D),q(0,0)],'width_m_MODEL':W,'depth_m_MODEL':D,'area_m2_MODEL':W*D,'published_113_1m2_used_for_dimensions':False,'source_box_replayed':False,'existing_enclosure_reused':True,'shared_Wall016':'Use westside only; keep neighbor eastface x=-45.3955 fixed; no duplicate shell. Existing gap/column y=-28.740..-27.952 untouched, with own fitout clear at least1.1m.','hallfront_normal_q':[0,1],'aperture':{'authority':'OWN-GENERATED-STOREFRONT-APERTURE-AUTHORITY-002.json','object_id':wall['object_id'],'current_source_mesh':wall['mesh_asset'],'glass_submesh':4,'current_glass_source_bounds_qx_qy_worldY':[-50.965412,-24.659073,7.1,-45.605476,-24.647072,9.62],'opening_inner_current_mullion_faces_qx':[-48.26294,-46.96796],'opening_top_worldY_MODEL':9.45,'existing_0_1m_lower_frame_retained':True,'method':'Replace only generated glass submesh4 by independent own derivative with sidepanes/head above exact aperture. Other source submeshes/frames/top/TRS/materials kept; collider uses derivative. Original mesh immutable.'},'metric_qualification':'Fixture/source visible proportions fitted within actual faces; MODEL not survey/asbuilt equality','floor_and_native_untouched':True}
(o/'MODEL-PLACEMENT-BASIS-R25.json').write_text(json.dumps(basis,ensure_ascii=False,indent=2))
pimp=json.loads((o/'PREPARED-SOURCE-IMPORTS.json').read_text());a='Assets/ChooGuard/Art/StationInterior/R25_Shops/storyway-southwest'
def imp(f,d):
 f=Path(f);pimp['imports'].append({'source':str(f),'destination':d,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()});return a+'/'+d
slots=json.loads((o/'SOURCE-DOWNLOADED-MODEL-SLOTS.json').read_text())
for key,sp in slots.items():
 pimp['models'][key]=imp(sp['own_derivative'],'Models/'+key+'-source.fbx')
 for md in sp['materials']:
  for k,f in md['maps'].items():
   nm=Path(f).name
   if nm not in [Path(x['source']).name for x in pimp['imports']]:pimp['textures'][key+'_'+md['source_slot']+'_'+k]=imp(f,'Textures/'+nm)
m={'white':{'color':[.91,.92,.89,1],'smoothness':.38},'charcoal':{'color':[.055,.052,.066,1],'metallic':.15,'smoothness':.4},'silver':{'color':[.64,.66,.67,1],'metallic':.88,'smoothness':.7},'glass':{'color':[.76,.88,.88,.065],'transparent':True,'smoothness':.96},'frosted':{'color':[.75,.79,.78,.6],'transparent':True,'smoothness':.4},'tile':{'color':[.82,.82,.73,1],'smoothness':.45},'lime':{'color':[.64,.80,.10,1],'smoothness':.4},'black':{'color':[.012,.014,.014,1],'smoothness':.4},'coolLED':{'color':[.94,1,1,1],'emission':[1.3,1.45,1.45,1]},'brandLED':{'color':[.91,1,.98,1],'emission':[1.3,1.5,1.4,1]},'yellowLED':{'color':[.98,.91,.09,1],'emission':[1.2,1,.04,1]},'blueLED':{'color':[.11,.58,.95,1],'emission':[.08,.5,1.4,1]},'greenLED':{'color':[.34,.88,.38,1],'emission':[.2,1,.22,1]},'orangeLED':{'color':[1,.4,.03,1],'emission':[1.4,.25,.015,1]},'shelf_enamel':{'color':[.91,.93,.88,1],'metallic':.26,'smoothness':.48},'shelf_rail':{'color':[.62,.67,.14,1],'smoothness':.34},'counter_white':{'color':[.82,.83,.74,1],'smoothness':.42}}
t=pimp['textures'];models=pimp['models']
m.update({'fridge_case':{'color':[.6,.62,.62,1],'texture_asset':t['fridge_base'],'metallic_gloss_asset':t['fridge_metal'],'metallic':.6,'smoothness':.6},'fridge_inside':{'color':[.96,.97,.94,1],'texture_asset':t['fridge_inside'],'metallic_gloss_asset':t['fridge_inside_metal'],'metallic':.3,'smoothness':.5},'water_source':{'color':[1,1,1,1],'texture_asset':t['water_base'],'normal_asset':t['water_normal'],'smoothness':.68},'pos_main':{'color':[1,1,1,1],'texture_asset':t['pos_base'],'normal_asset':t['pos_normal'],'smoothness':.45},'pos_trim':{'color':[.045,.05,.055,1],'normal_asset':t['pos_normal2'],'metallic':.65,'smoothness':.55},'stock_source':{'color':[1,1,1,1],'texture_asset':t['kenney'],'smoothness':.4}})
for key,sp in slots.items():
 for md in sp['materials']:
  slot=md['source_slot'];mt={'color':[1,1,1,1],'metallic':.6 if key=='can' else .03,'smoothness':.55 if key=='can' else .35}
  for slug,pr in [('base','texture_asset'),('normal','normal_asset')]:
   name=key+'_'+slot+'_'+slug
   if name in t:mt[pr]=t[name]
  m[slot]=mt
objs=[]
def add(kind,id,x,z,y=0,group='SourceVisibleFitout',**kw):
 sp={'kind':kind,'id':id,'q':q(x,z),'y':y,'yaw':0,'group':group,'source_ids':[source],'qualification':'PHOTO_REFERENCED_MODEL_NOT_FIELD_MEASUREMENT'};sp.update(kw);objs.append(sp);return sp
def box(id,x,z,y,size,mat,group='SourceVisibleFitout',**kw):return add('box',id,x,z,y,group=group,size=size,material=mat,**kw)
def asset(id,key,x,z,y,fit,map,rot=0,group='DownloadedSourceModels',collider=True):return add('asset',id,x,z,y,group=group,asset=models[key],fit_size=fit,material_map=map,yaw=rot,collider=collider)
# Native enclosure, jambs, front glazing except source-visible entry and all native wall/ceiling/floor remain.
box('Own_Source_Ceramic_Finish',W/2,D/2,.003,[W-.16,.006,D-.16],'tile','OwnInteriorFinish')
# Physical source fascia skin attached in front of existing generated fascia; no duplicate room shell.
box('Actual_Dark_Front_Fascia_Surface',W/2,D+.087,2.97,[W-.18,.64,.022],'charcoal','PhysicalSourceOutlineSigns',collider=False)
for i in range(6):box('Fascia_Horizontal_Source_Rib_'+str(i),W/2,D+.089,3.31+i*.055,[W-.18,.015,.024],'charcoal','PhysicalSourceOutlineSigns',collider=False)
# Source photo rectification exclusively for sign outline, never architectural geometry.
sign=o/'sign-model-authoring';sign.mkdir(exist_ok=True);im=cv2.imread(str(s/'ROOT-kmk78800-224405723594-26.jpg'))
rect=cv2.warpPerspective(im,cv2.getPerspectiveTransform(np.float32([[222,94],[568,101],[568,182],[221,175]]),np.float32([[0,0],[1037,0],[1037,242],[0,242]])),(1038,243));hsv=cv2.cvtColor(rect,cv2.COLOR_BGR2HSV);white=cv2.inRange(hsv,np.array([0,0,185]),np.array([179,125,255]));n,lab,stats,cent=cv2.connectedComponentsWithStats(white);clean=np.zeros_like(white);parts=[]
for i in range(1,n):
 if stats[i,cv2.CC_STAT_AREA]<80:continue
 mask=np.uint8(lab==i)*255;cx,cy=cent[i];part=closed_solid_from_mask(mask,2.13,.50,.035,minimum_area=70,epsilon=.5);parts.append({'material':'brandLED','part':part});clean[mask>0]=255
for name,lo,hi in [('blueLED',[80,140,110],[125,255,255]),('greenLED',[35,130,110],[79,255,255]),('yellowLED',[22,150,160],[34,255,255]),('orangeLED',[0,150,130],[21,255,255])]:
 mask=cv2.inRange(hsv,np.array(lo),np.array(hi));mask[:,210:]=0
 n,lab,stats,cent=cv2.connectedComponentsWithStats(mask)
 for i in range(1,n):
  if stats[i,cv2.CC_STAT_AREA]<70:continue
  component=np.uint8(lab==i)*255;part=closed_solid_from_mask(component,2.13,.50,.035,minimum_area=60,epsilon=.5);parts.append({'material':name,'part':part});clean[component>0]=255
cv2.imwrite(str(sign/'SW-ACTUAL-PHOTO-BRAND-RECTIFIED.jpg'),rect);cv2.imwrite(str(sign/'SW-ACTUAL-PHOTO-BRAND-MASK.png'),clean)
for i,x in enumerate(parts):add('mesh','Source_Storyway_Closed_Outline_'+str(i),W*.56,D+.13,2.98,group='PhysicalSourceOutlineSigns',vertices=x['part']['vertices'],triangles=x['part']['triangles'],material=x['material'],collider=False)
# Actual circular convenience board: photo-traced yellow ring and actual white Hangul/logo contours.
cr=im[208:344,54:194];hv=cv2.cvtColor(cr,cv2.COLOR_BGR2HSV);circlecenter=(71,66)
white=cv2.inRange(hv,np.array([0,0,165]),np.array([179,125,255]));ys,xs=np.indices(white.shape);white[((xs-circlecenter[0])**2+(ys-circlecenter[1])**2)>63**2]=0
# Keep actual lettering. Exclude broad top ceiling reflection above source letters.
white[:40]=0
ring=cv2.inRange(hv,np.array([22,110,130]),np.array([43,255,255]));ring[((xs-circlecenter[0])**2+(ys-circlecenter[1])**2)<58**2]=0
for key,mask,mat in [('White_Source_Convenience_Letters',white,'brandLED'),('Source_Yellow_Ring',ring,'yellowLED')]:
 part=closed_solid_from_mask(mask,.78,.76,.012,minimum_area=3,epsilon=.25);add('mesh',key,W-.56,D+.042,2.03,group='PhysicalSourceOutlineSigns',vertices=part['vertices'],triangles=part['triangles'],material=mat,collider=False)
# Circular physical dark backing (cylinder nativeheight2, 90deg outward normal).
add('cylinder','Convenience_Actual_Round_Frame',W-.56,D+.023,2.03,group='PhysicalSourceOutlineSigns',size=[.80,.009,.80],euler=[90,0,0],material='charcoal',collider=False)
for x in [W*.56-.8,W*.56+.8]:box('Sign_Mount_'+str(x),x,D+.105,2.98,[.035,.12,.035],'silver','PhysicalSourceOutlineSigns',collider=False)
# Visible dropped acoustic ceiling, own independent fitout below original station geometry.
for ix,x in enumerate(np.arange(.32,W-.18,.61)):
 for iz,z in enumerate(np.arange(.32,D-.18,.61)):
  box(f'Acoustic_Ceiling_Source_Module_{ix}_{iz}',x,z,3.10,[.592,.025,.592],'coolLED' if (ix%3==1 and iz%2==0) else 'white','SourceVisibleCeiling',collider=False)
# The actual green/white rear chiller band, numbering7and6 authored from actual source ink.
box('Rear_Observed_Lime_Header',W/2,.11,2.52,[W-.22,.38,.035],'lime','SourceVisibleRefrigeration')
box('Rear_Observed_White_Number_Band',W/2,.13,2.20,[W-.22,.24,.035],'white','SourceVisibleRefrigeration')
for n,(xa,ya,xb,yb),cx in [('7',(322,300,340,324),3.72),('6',(535,305,551,327),2.36)]:
 rr=im[ya:yb,xa:xb];gray=cv2.cvtColor(rr,cv2.COLOR_BGR2GRAY);mask=cv2.inRange(gray,0,110);part=closed_solid_from_mask(mask,.11,.18,.003,minimum_area=2,epsilon=.2);add('mesh','Photo_Traced_Chiller_Number_'+n,cx,.151,2.20,group='SourceVisibleRefrigeration',vertices=part['vertices'],triangles=part['triangles'],material='charcoal',collider=False)
fm={'FridgeCase':'fridge_case','FridgeDoor':'fridge_case','FridgeDoorInner':'fridge_inside','FridgeGlass':'glass','FridgeInterior':'fridge_inside'}
for i,x in enumerate([.59,1.44]):asset('Source_Actual_Drink_Cabinet_'+str(i),'fridge',x,.54,.01,[.80,2.04,.73],fm)
# Open food chillers source photograph left/centre, each actual downloaded shelf with source-adapted compressor/lighting.
for i,x in enumerate([2.42,3.42,4.42]):
 asset('Open_Food_Chiller_Source_Shelf_'+str(i),'shelf',x,.53,.20,[.95,1.70,.65],{'mat_shelf':'shelf_enamel','mat_shelf_glass':'shelf_rail'})
 box('Open_Chiller_Base_'+str(i),x,.52,.13,[.97,.25,.68],'white','SourceVisibleRefrigeration')
 box('Open_Chiller_Canopy_'+str(i),x,.52,2.0,[.98,.12,.67],'silver','SourceVisibleRefrigeration')
 for j,y in enumerate([.30,.68,1.06,1.44,1.81]):box(f'Open_Chiller_Source_LED_{i}_{j}',x,.85,y,[.92,.016,.018],'coolLED','SourceVisibleRefrigeration',collider=False)
# Central back-to-back grocery gondola, source white rack faces; actual free shelf geometry.
for j,z in enumerate([2.30,3.28]):
 for side,rot in [(-1,90),(1,270)]:asset(f'Observed_Central_Gondola_{j}_{side}','shelf',2.85+side*.21,z,.01,[.94,1.48,.38],{'mat_shelf':'shelf_enamel','mat_shelf_glass':'shelf_rail'},rot=rot)
# Partial near-left service cabinet in exactfrontphoto; operating POS make/layout inferred MODEL and explicit.
asset('Partial_Source_Front_Service_Cabinet','counter',W-.54,D-1.27,.01,[.74,.88,.58],{'wire_160160160':'counter_white'},rot=90)
asset('Downloaded_POS_Physical_Device','pos',W-.54,D-1.27,.91,[.24,.28,.23],{'Material_2100454184':'pos_main','Material_2100454185':'pos_trim'},rot=90)
# Imported textured packaging sources as category-level products. Do not claim exact installedSKUs/inventory.
cm={md['source_slot']:md['source_slot'] for md in slots['can']['materials']};chm={md['source_slot']:md['source_slot'] for md in slots['chips']['materials']}
for j,z in enumerate([2.30,3.28]):
 for iy,y in enumerate([.17,.48,.79,1.10,1.38]):
  for side in [-1,1]:
   for k in range(5):
    key='chips' if iy in [0,2,4] else 'can';fit=[.17,.23,.075] if key=='chips' else [.068,.125,.068]
    asset(f'Editable_Physical_Grocery_{j}_{iy}_{side}_{k}',key,2.85+side*.29,z-.37+k*.18,y,fit,chm if key=='chips' else cm,rot=90 if side==1 else 270,group='DownloadedEditableMerchandise',collider=False)
for bank,x in enumerate([.59,1.44,2.42,3.42,4.42]):
 for iy,y in enumerate([.32,.70,1.08,1.46,1.81]):
  for k in range(5):
   key='water' if bank<2 and iy%2==0 else 'can' if bank<2 else 'bag-flat' if iy%2 else 'cup';fit={'water':[.065,.23,.065],'can':[.066,.125,.066],'bag-flat':[.14,.20,.065],'cup':[.105,.115,.105]}[key]
   mp={'BottleMat':'water_source'} if key=='water' else cm if key=='can' else {'colormap':'stock_source'}
   asset(f'Observed_Refrigerated_Category_Stock_{bank}_{iy}_{k}',key,x-.29+k*.145,.63,y,fit,mp,group='DownloadedEditableMerchandise',collider=False)
packet={'store_key':'storyway-southwest','origin_q':origin,'floor_world_y':7.0,'source_packet':'SOURCE-EVIDENCE-R25.json','source_imports':pimp['imports'],'generated_collider_aperture':json.loads((o/'GENERATED-COLLIDER-ENTRANCE-DERIVATIVE-R25.json').read_text()),'materials':m,'objects':objs,'limits':['No original exact branch construction plans obtained.','Visible fitout dimensions/arrangement, ceiling grid count, source SKU inventories and partial service cabinet/POS position are MODEL, not surveyed.','Current source native/generated enclosure reused; only explicitly scoped own generated glass aperture changes. Existing100mm lower frame is retained.','No hidden BOH invented; no public hall props or neighbor work.','User validates. No agent QA/tests/probes/reopen/Play/captures.']}
(o/'STORE-PACKET-R25.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2));sha=hashlib.sha256((o/'STORE-PACKET-R25.json').read_bytes()).hexdigest()
template=(r/'StoreBuilderR25-v4-SOURCE-MATERIALS.template.cs').read_text();repls={'__STORE_KEY__':'storyway-southwest','__WORKER_THREAD__':'classic:/root/r25_storyway_southwest','__EXACT_SCOPE__':'C180_R25_STORYWAY_SOUTHWEST_APPLY_002','__PACKET_PATH__':str(o/'STORE-PACKET-R25.json'),'__PACKET_SHA256__':sha}
for k,val in repls.items():template=template.replace(k,val)
(o/'ApplyStoreR25.cs').write_text(template)
(sign/'SW-ACTUAL-PHOTO-SOLID-SIGN.json').write_text(json.dumps({'source':str(s/'ROOT-kmk78800-224405723594-26.jpg'),'method':'Exact branch photographed white/luminous ink and colored dot masks to closed front/back/side meshes, real letter holes retained. Circular convenience board traced from exact photo independently. No font/TMP/wholephoto board.','brand_parts':len(parts),'brand_source_polygons':sum(x['part']['source_polygons'] for x in parts),'actual_source_shape':True},indent=2))
print(json.dumps({'objects':len(objs),'downloaded_asset_instances':sum(x['kind']=='asset' for x in objs),'width':W,'depth':D,'packet_sha':sha}))
