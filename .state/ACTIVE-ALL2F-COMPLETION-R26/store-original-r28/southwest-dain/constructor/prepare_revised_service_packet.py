from pathlib import Path
import json,hashlib,importlib.util,copy
import cv2,numpy as np
W=Path(__file__).resolve().parent;R=Path('/Users/um-yunsang/CHOOGuard');P=R/'.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25'
spec=importlib.util.spec_from_file_location('solid',P/'photo_outline_to_solid.py');solid=importlib.util.module_from_spec(spec);spec.loader.exec_module(solid)
base=json.loads((W/'FROZEN-SOURCE-OBSERVED-DAIN-PACKET-R29.json').read_text());packet=copy.deepcopy(base)
packet['objects']=[x for x in base['objects'] if x['group'] in ['PastryCounter','PastryMerchandise','ReturnCounter']]
packet['mesh_library']={k:v for k,v in base['mesh_library'].items() if k in ['Source_Round_Gyeongju_Bread','Source_FlowerPressed_Walnut_Pastry']}
packet['preserve_actual_Flowers_objects']='Exact11 wrapper corrections only under NEW root joint authority, otherwise fixed'
packet['no_new_shell_or_ceiling']=True
packet['supplement_existing_root']='FPSWorld/Architecture/2F/MAIN2F_R25_SOURCE_BOUND_SPECIALIST_SHOPS/R29_southwest-dain_SOURCE_OBSERVED_MODEL'
source=R/'.state/ACTIVE-ALL2F-COMPLETION-R26/public/southwest-field-identity/sources/diningcode-dain/FIELD-20240324-02.webp'
packet['source_packet']=str(W/'REVISED-JOINT-SERVICE-AND-RETURN-HEADER-PROPOSAL-R29.json')
new=[]
def part(kind,id,group,q,y,material,**kw):
 x={'kind':kind,'id':id,'group':group,'q':q,'y':y,'yaw':.22415,'material':material,'source_ids':['DININGCODE20240324_FIELD02','DAIN_SOURCE_FIELD00','DAILY20240904_16'],'qualification':'SOURCE_VISIBLE_2022_2024_PROPORTION_MODEL_NOT_SURVEYED',**kw};packet['objects'].append(x);new.append(x);return x
def box(id,g,q,y,size,m,**kw):return part('cube',id,g,q,y,m,size=size,**kw)
def trace(name,mask,width,height,depth,min_area=3,eps=.32):
 cv2.imwrite(str(W/(name+'-source-mask.png')),mask)
 s=solid.closed_solid_from_mask(mask,width,height,depth,minimum_area=min_area,epsilon=eps)
 packet['mesh_library'][name]={k:s[k] for k in ['vertices','triangles']}
 return {'mesh_key':name,'source_polygons':s['source_polygons'],'vertices':len(s['vertices']),'triangles':len(s['triangles'])//3,'displayed_image':False,'closed_physical_contours':True}
# Header is the distinct BELOW-fascia hanging rail of the real pastry return.
# Preserve original raster aspect; extract only source-letter ROIs, never type a font.
im=cv2.imread(str(W/'RETURN-HEADER-FIELD02-TRACE-REFERENCE.png'));hsv=cv2.cvtColor(im,cv2.COLOR_BGR2HSV)
white=cv2.inRange(hsv,np.array([0,0,230]),np.array([179,90,255]));roi=np.zeros(white.shape,np.uint8)
# Letter outlines and four red-logo glyphs, excluding ceiling/reflection regions.
for x0,y0,x1,y1 in [(90,98,161,168),(190,107,254,177),(88,189,159,254),(185,195,254,268),(310,114,475,301),(468,130,655,306),(651,135,826,315),(846,157,1006,323),(1003,179,1140,325),(1134,186,1263,333),(1257,183,1390,341)]:roi[y0:y1,x0:x1]=255
white=cv2.bitwise_and(white,roi)
red=cv2.bitwise_or(cv2.inRange(hsv,np.array([0,90,75]),np.array([12,255,255])),cv2.inRange(hsv,np.array([170,90,75]),np.array([179,255,255])));rroi=np.zeros(red.shape,np.uint8);rroi[80:288,70:284]=255;red=cv2.bitwise_and(red,rroi)
# The black edge around white glyphs is a real relief backing contour, not a photo.
outline=cv2.dilate(cv2.bitwise_or(white,red),np.ones((5,5),np.uint8),iterations=1)
meshmeta=[]
for name,mask,mat,depth,q in [('PastryReturn_Source_Black_Glyph_Edges',outline,'black',.022,[-13.842,-34.545]),('PastryReturn_Source_Red_Dain_Squares',red,'red',.018,[-13.842,-34.551]),('PastryReturn_Source_White_Closed_Glyphs',white,'ink_white',.021,[-13.842,-34.560])]:
 meshmeta.append(trace(name,mask,2.90,2.90*350/1460,depth,3,.32))
 part('library_mesh',name,'PastryReturnSourceHeader',q,2.70,mat,mesh_key=name,yaw=180.22415,collider=False)
box('PastryReturn_Observed_Black_Hanging_Rail','PastryReturnSourceHeader',[-13.842,-34.516],2.66,[2.85,.185,.034],'black',collider=False)
for i,x in enumerate([-14.91,-12.83]):box('PastryReturn_Observed_Hanger_'+str(i),'PastryReturnSourceHeader',[x,-34.505],2.923,[.017,.274,.017],'black',collider=False)
# Staffed service, not self-service: source shows actual POS behind LEFT of case.
# Compact existing POS is moved, not duplicated; a shallow local counter wing supports it.
box('Observed_Cashier_Service_Wing_Body','PastryCashierService',[-14.85,-33.51],.45,[.62,.86,.30],'white')
box('Observed_Cashier_Service_Wing_Black_Top','PastryCashierService',[-14.85,-33.51],.925,[.65,.045,.32],'black')
box('Observed_Cashier_Service_Wing_Door','PastryCashierService',[-14.85,-33.344],.45,[.565,.79,.022],'white')
box('Observed_Cashier_Service_Wing_Handle','PastryCashierService',[-14.63,-33.326],.64,[.018,.15,.022],'black',collider=False)
# Source-visible white two-door bread-stock cabinet behind right counter, doors face -qx.
# This is visible equipment; no hidden kitchen, oven or new room boundary is inferred.
box('Observed_White_Bread_Stock_Cabinet_Body','PastryBreadStock',[-13.07,-33.18],1.05,[.60,2.06,.88],'white')
box('Observed_White_Bread_Stock_Cabinet_Front_Gasket','PastryBreadStock',[-13.383,-33.18],1.02,[.018,1.91,.84],'black')
for i,y in enumerate([-33.40,-32.96]):
 box('Bread_Stock_White_Door_'+str(i),'PastryBreadStock',[-13.399,y],1.02,[.022,1.87,.409],'white')
 box('Bread_Stock_Door_Handle_'+str(i),'PastryBreadStock',[-13.423,y+(.15 if i==0 else -.15)],.87,[.024,.43,.020],'black',collider=False)
box('Bread_Stock_White_Top_Fascia','PastryBreadStock',[-13.402,-33.18],2.032,[.026,.102,.885],'white',collider=False)
# Source legends are individually traced only where actually legible.
ci=cv2.imread(str(W/'WHITE-STOCK-CABINET-FIELD02-TRACE-REFERENCE.png'))
caption_specs=[('LeftDoor_Gyeongju',(117,98,155,122),[-13.426,-33.40],1.66,.27,.17),('LeftDoor_Barley',(115,181,157,206),[-13.426,-33.40],1.05,.30,.18),('RightDoor_Gyeongju',(203,96,244,124),[-13.426,-32.96],1.66,.28,.19),('RightDoor_Barley',(202,191,249,218),[-13.426,-32.96],1.05,.31,.18),('RightDoor_Walnut',(202,323,247,351),[-13.426,-32.96],.45,.31,.19)]
for name,rect,q,y,width,height in caption_specs:
 x0,y0,x1,y1=rect;c=ci[y0:y1,x0:x1];h=cv2.cvtColor(c,cv2.COLOR_BGR2HSV)
 m=cv2.inRange(h,np.array([0,45,45]),np.array([35,255,215]));m[:1]=0;m[-1:]=0;m[:,:1]=0;m[:,-1:]=0
 key='Bread_Stock_Source_'+name;meshmeta.append(trace(key,m,width,height,.003,1,.18));part('library_mesh',key,'PastryBreadStock',q,y,'brownink',mesh_key=key,yaw=270.22415,collider=False)
# Physical repeated source pastry illustrations, no raster label planes.
for door,yy in enumerate([-33.40,-32.96]):
 for row,hh in enumerate([1.35,.77,.20] if door else [1.35,.77]):
  for i in range(6):
   key='Source_FlowerPressed_Walnut_Pastry' if row==2 else 'Source_Round_Gyeongju_Bread'
   part('library_mesh',f'BreadStock_Physical_Source_Pastry_Relief_{door}_{row}_{i}','PastryBreadStock',[-13.427,yy+(i%3-1)*.047],hh+(i//3-.5)*.059,'barley',mesh_key=key,size=[.55,.28,.55],euler=[90,270.22415,0],yaw=270.22415,collider=False)
# Preserve complete original requested supplement and record exact added source apparatus.
pending=W/'PENDING-DAIN-RETURN-SERVICE-PACKET-R29.json';pending.write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':')))
p=json.loads((W/'BOUNDED-FLOWERS-11-DAIN-178-SOURCE-PROPOSAL-R29.json').read_text())
p['status']='REVISED_BOUNDED_SOURCE_CONSTRUCTION_PROPOSAL_READONLY_AWAITING_NEW_ROOT_JOINT_SCOPE'
p['supersedes_readonly_candidate']='BOUNDED-FLOWERS-11-DAIN-178-SOURCE-PROPOSAL-R29.json: +.72m / .126m is separation only, never operator space.'
for s in p['minimal_Flowers_changes']:
 cooler=s['kind']=='refrigerator_complete_wrapper';d=1.60 if cooler else 1.47;s['delta_packet_q']=[0,d];s['proposed_q']=[s['old_q'][0],round(s['old_q'][1]+d,6)]
 s['reason']='Source-observed Flowers public-facing glass correction plus minimum rear allocation for photographed staffed Dain counter: unchanged X/heights/materials, cooler+1.60m, content+1.47m retains .15m front offset. Exact translation is MODEL, not measured.'
p['Flowers_fixed_public_front_runs_and_aisle'].update({'proposed_cooler_body_packetY_range_nominal':[-32.68,-31.98],'front_display_to_new_cooler_front_gap_nominal_m':.84,'remaining_generated_Flowers_XZ_changes':'Only exact2cooler wrappers and9containedflower wrappers; front displayruns/centralaisle/Native0/graypairedEXITdoor all fixed.'})
p['required_Dain_supplement']={'prepared_new_immutable_packet':str(pending),'packet_sha256':hashlib.sha256(pending.read_bytes()).hexdigest(),'current_applied_lowerleg42_preserved_except_one_source_relocated_POS':True,'original_178_parts_preserved':True,'new_total_parts':len(packet['objects']),'added_source_return_header_service_stock_parts':len(new),'existing_native_return_glazing_and_lower_panel_reused':True,'source_case_and_whitecounter_model_envelope_packet':[-15.10,-34.334,-12.62,-33.686],'counter_rear_to_new_cooler_rear_equipment_band_nominal_m':1.006,'no_added_room_shell_ceiling_native_or_floor_cut':True}
p['existing_Dain_single_POS_relocation']={'id':'Downloaded_POS_Terminal','current_object_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-1707723956-0','old_q':[-16.95,-35.42],'proposed_q':[-14.85,-33.51],'old_yaw':90.22415,'proposed_yaw':.22415,'world_floor_relative_y':.95,'fit_size_unchanged':[.30,.34,.29],'do_not_duplicate':True,'source_reason':'FIELD02 visible actual checkout monitor is BEHIND LEFT of pastry case. Original lower-leg POS source placement is superseded only for this wrapper.'}
p['actual_counter_operation_and_access']={'primary_source':str(source),'source_observations':['Black-front pastry case has continuous public glass and packaged pastry behind it; no front customer access doors are shown. Do not call self-service.','FIELD02 shows a worker behind the case and an actual POS to its rear-left.','White two-door stock unit behind right counter faces the service space laterally; large public side is blank.','DAILY16 confirms cafe lower leg joins this return; no observed full structural tenant partition is introduced.'],'source_model_dimensions_not_field_measurements':True,'nominal_case_rear_packetY':-33.686,'nominal_opposing_cooler_rear_packetY':-32.680,'combined_equipment_gap_m':1.006,'cashier_support_worktop_packet_bounds':[-15.175,-33.670,-14.525,-33.350],'cashier_standing_footprint_packet_bounds':[-15.175,-33.330,-14.525,-32.700],'cashier_standing_footprint_dimensions_m':[.65,.63],'case_centre_restock_standing_band_packet_bounds':[-14.50,-33.65,-13.48,-32.70],'centre_band_dimensions_m':[1.02,.95],'right_stock_cabinet_packet_bounds':[-13.37,-33.62,-12.77,-32.74],'stock_cabinet_operator_face':'-packetqx; opens toward central service band','operator_connection':'Rear/left open service connection into the same original Native0 L shell; no full separating wall and no invented doorway. All native fronts/entrance/grayEXITdoor and Flower publicfrontcentralaisle remain unchanged. A shared MODEL staff space is proposed from visible staffed counter; no claim to legal tenancy plan or surveyed aisle.'}
p['distinct_return_header']={'source':str(source),'source_crop':str(W/'RETURN-HEADER-FIELD02-TRACE-REFERENCE.png'),'actually_legible_content':['다인명가','경주빵','호두과자'],'is_not_saved_lowerleg_mainfront':True,'source_form':'Red four-square Dain logo and white closed glyphs on black hanging rail below plain fascia, rods above. Native fascia material remains unchanged.','packet_q':[-13.842,-34.545],'yaw':180.22415,'public_normal_packet':[-.003912,-.999992],'relative_floor_center_height_m':2.70,'full_source_crop_metric_size_m':[2.90,.695205],'actual_glyph_height_approx_m':.45,'height_basis':'MODEL below retained Native0 upperband undersideworldY10.06; source-visible suspended header and rod relationship, not a source-surveyed absolute height.','physical_meshes':meshmeta[:3],'no_visible_source_raster_font_or_Text_TMP':True}
p['additional_source_stock_cabinet']={'source':'FIELD02 clearly visible behind right returncounter','body_packet_bounds':[-13.37,-33.62,-12.77,-32.74],'body_height_m':2.06,'qualification':'VISIBLE_EQUIPMENT_SOURCE_PROPORTION_MODEL_NOT_EXACT_MAKE','legible_letters':'Original source contours 경주빵/찰보리빵/호두과자; physical pastry relief, no image labels.','glyph_meshes':meshmeta[3:]}
p['dimension_basis_and_uncertainty']['model_not_surveyed']=[x for x in p['dimension_basis_and_uncertainty']['model_not_surveyed'] if not x.startswith('+0.72')]+['+1.60m cooler and+1.47m containedbouquet translation are calculated source-layout MODEL allocations for staff operation, not photographed distance measurements.','1.006m is the equipmentback band. Cashier support occupies part of it; actual standing pocket .65x.63m and centralrestockband1.02x.95m are separately recorded. No .126m clearance is called staffspace.','Native0 exactfront/return lines fixed. Storeepoch and photographedstock counts are qualified, not99percent surveyed equality.']
p.pop('all_Dain_sign_letter_geometry_already_saved_in_lowerleg',None)
p['all_Dain_sign_claim_correction']='Saved42 mainfront/coffee/menu only. Distinct returnheader and cabinet source legends are prepared in THIS new packet, not already applied.'
p['next_if_root_authorizes']='Register NEW explicit C180 jointscope permitting exact11Flower wrapper corrections, exact1ownDain POS correction and new pending supplement only. Append into savedR29root; no full220 replay. Preserve all other objects/sourceAssets/native/floor/columns/openings. NoScene change until grant.'
out=W/'REVISED-JOINT-SERVICE-AND-RETURN-HEADER-PROPOSAL-R29.json';out.write_text(json.dumps(p,ensure_ascii=False,indent=2))
print(json.dumps({'proposal':str(out),'pending_packet':str(pending),'new_parts':len(packet['objects']),'added_header_service_stock_parts':len(new),'exact_Flowers_moved_wrappers':len(p['minimal_Flowers_changes']),'exact_own_POS_moved':1,'source_glyph_meshes':len(meshmeta),'pending_sha256':hashlib.sha256(pending.read_bytes()).hexdigest(),'scene_changes':False}))
