from pathlib import Path
import json,math,hashlib
W=Path(__file__).resolve().parent
base=json.loads((W/'ELECTRICAL-PACKET-R26.json').read_text())
p={'status':'SOURCE_EQUIPMENT_FIT_TO_CURRENT_SAME_FIXTURE_MODEL','materials':base['materials'],'objects':[],'mesh_library':{},'counts':{},'scope_exclusions':['Only original second right-display SMPS and 13th storage1200mmT7 are added','Existing373electricalparts and currentbooth/native/floor/frame/columns unchanged','Sourceoriginalsymbolabsolute paperpose is not metric authority; individual same-furniture placement is MODEL','No Light,Text,TMP,photo-signplanes, QA,Play,reopen,probes,captures','Hiddenmanufacturer/cable/casingdetailsnotobservedasbuilt; dimensionedT7length1.200m retained']}
def b(g,n,x,y,z,w,h,d,m='enamel'):
 p['objects'].append({'group':g,'id':n,'kind':'box','position':[x,y,z],'size':[w,h,d],'material':m,'collider':False})
def c(g,n,x,y,z,diam,h,m='steel'):
 p['objects'].append({'group':g,'id':n,'kind':'cylinder','position':[x,y,z],'size':[diam,h/2,diam],'material':m,'collider':False})
def line(g,n,a,z,d=.016,m='steel'):
 v=[z[i]-a[i] for i in range(3)];ln=sum(x*x for x in v)**.5;p['objects'].append({'group':g,'id':n,'kind':'conduit','position':[(a[i]+z[i])/2 for i in range(3)],'size':[d,ln/2,d],'axis':v,'material':m,'collider':False})
g='RIGHT_DISPLAY_SMPS_8_SAME_FIXTURE_MIGRATION';x=3.995;z=3.365
b(g,'RIGHT_B_power_supply_body',x,.18,z,.20,.045,.09,'steel')
for k in range(7):b(g,'RIGHT_B_vent_louvre_'+str(k),x-.075+k*.025,.204,z,.003,.002,.07,'black')
b(g,'RIGHT_B_terminal_guard',x+.075,.188,z+.05,.06,.035,.015,'black')
for k in range(3):c(g,'RIGHT_B_terminal_screw_'+str(k),x+.055+k*.018,.208,z+.05,.007,.002)
line(g,'RIGHT_B_input_pigtail',[x+.10,.18,z],[x+.14,.18,z+.05],.007,'black')
line(g,'RIGHT_B_source_R2_floor_spur',[4.40,-.04,z],[x,-.04,z])
line(g,'RIGHT_B_source_R2_furniture_riser',[x,-.04,z],[x,.18,z])
g='STORAGE_13TH_T7_1200_PHYSICAL_HOUSING_ONLY';x=5.17;z=2.95
b(g,'storage_T7_full_1200mm_case',x,3.155,z,.035,.035,1.20,'white')
b(g,'storage_T7_diffuser',x,3.134,z,.024,.008,1.17,'diffuser')
for k,zz in enumerate([z-.5925,z+.5925]):b(g,'storage_T7_closed_endcap_'+str(k),x,3.155,zz,.035,.035,.015,'white')
for k,zz in enumerate([z-.45,z+.45]):b(g,'storage_T7_ceiling_clip_'+str(k),x,3.19,zz,.022,.03,.025,'steel')
line(g,'storage_T7_source_L1_ceiling_approach',[4.48,3.28,3.43],[5.17,3.28,3.43])
line(g,'storage_T7_source_L1_ceiling_branch',[5.17,3.28,3.43],[5.17,3.28,2.95])
line(g,'storage_T7_source_L1_drop',[5.17,3.28,2.95],[5.17,3.175,2.95])
p['counts']={'physical_parts':len(p['objects']),'equipment_assemblies':2,'new_SMPS':1,'SMPS_total_after_followup':8,'new_T7_1200mm_housings':1,'T7_1200mm_total_after_followup':13,'previously_held_source_equipment_resolved':2,'remaining_source_pose_conflicts':0,'Light_components_added':0}
p['placement_basis']={'design':'E009 displaySMPS+E010 storage1200mmT7, July2026','actual_current_furniture':'right_low_storage_1 and storage_ceiling_rear_native_clear exact current mesh snapshot','SMPS_support':{'cabinet_center':[3.995,.275,3.365],'cabinet_dimensions':[.45,.52,.78],'new_SMPS_center':[3.995,.18,3.365],'new_SMPS_dimensions':[.20,.045,.09],'relationship':'Second source rightdisplay supply migrated into existing nextsamefixture lowercupboard; no nativebodyintersection; sourceR2same displaycircuit'},'T7_support':{'ceiling_original_design_local_center':[4.865,3.215,4.01],'ceiling_original_design_dimensions':[1.01,.03,3.72],'new_case_center':[5.17,3.155,2.95],'new_case_dimensions':[.035,.035,1.2],'new_axis':'source1200mmfull length rotated90degrees into same storage continuousdepth, never shortened','current_storage_clear_x':[4.36,5.37],'new_housing_x_extent':[5.1525,5.1875],'current_storage_continuous_z':[2.15,5.87],'new_housing_z_extent':[2.35,3.55],'native_support_max_depth_z':2.135,'source_L1_storage_circuit_retained':True,'existing_downlight_center_x':4.865,'fixture_shift_from_downlight_center_x':.305},'classification':'SOURCE_DESIGN_MODEL_NOT_MEASURED_ASBUILT','fixed_geometry_changed':False}
f=W/'ELECTRICAL-FIT-PACKET-002.json';f.write_text(json.dumps(p,ensure_ascii=False,indent=2));(W/'MODEL-SOURCE-FIT-RESOLUTION-002.json').write_text(json.dumps(p['placement_basis'],ensure_ascii=False,indent=2));print(p['counts']);print('sha256',hashlib.sha256(f.read_bytes()).hexdigest())
