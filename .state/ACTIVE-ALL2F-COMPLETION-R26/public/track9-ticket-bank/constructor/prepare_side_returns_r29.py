from pathlib import Path
import json,numpy as np,math,hashlib,datetime
ROOT=Path('/Users/um-yunsang/CHOOGuard');B=Path(__file__).resolve().parent;D=B/'side-return-r29';D.mkdir(exist_ok=True)
F4='Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/Track9TicketBank';F5='Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/SelfTicket';Q=np.array([[-.34290633335024717,-.9393696006089878],[-.9393696006089878,.3429063333502471]])
snap=json.loads((ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-007/CURRENT-2F-MESH-SNAPSHOT.json').read_text())
def host(ident):
 o=next(x for x in snap['objects']if '-'+ident+'-0' in x['object_id']);return {'id':o['object_id'],'path':o['path'],'mesh_asset':o['source_mesh_asset'],'world_matrix_column_major':o['world_matrix_column_major']}
def bank(name,h,normal,backplane,yrange,length,offset,source):
 cy=sum(yrange)/2;q=[backplane+normal*offset,cy];w=Q@np.array(q);fw=Q@np.array([normal,0.]);yaw=math.degrees(math.atan2(fw[0],fw[1]));return {'root_name':name,'parent_path':'FPSWorld/Architecture/2F/MAIN2F_R26_SOURCE_BOUND_PUBLIC_EQUIPMENT','host':h,'source':source,'origin_actual_q':q,'origin_world_xz':w.tolist(),'floor_y':7.,'world_yaw':yaw,'public_normal_actual_q':[normal,0],'actual_host_qY':yrange,'source_proportioned_bank_length_m':length,'longitudinal_actual_qY':[cy-length/2,cy+length/2],'backing_face_actual_qX':backplane,'equipment_outward_offset_MODEL_m':offset,'objects':[]}
four=json.loads((B/'TRACK9-TICKET-BANK-PACKET-R26.json').read_text());fit=json.loads((B/'TRACK9-SOURCE-PROPORTION-FIT-002.json').read_text());ch={r['id']:r for r in fit['changes']}
b4=bank('R29_NURSERY_FOUR_MODERN_TVMS_SIDE_RETURN_R29',host('288332569'),-1,-3.575988,[-7.461432,-.980438],5.6,.725,'May2026 baitfishingrod source06; source05/07 nurse/OliveYoung/EXIT9 route and Apr2026 guide')
for o in four['objects']:
 if o['id'].startswith(('White_Equipment_Carrier_','Dark_Equipment_Plinth_')):continue
 c=ch[o['id']];a={'id':o['id'],'group':o['group'],'kind':o['kind'],'position':c['position'],'scale':c['scale'],'euler':o['euler'],'material_asset':F4+'/MAT_'+o['material']+'.mat','collider':o['collider'],'source_ids':o['source_ids']}
 if o['kind']=='mesh':a['mesh_asset']=F4+'/MESH_'+o['mesh_key']+'.asset'
 b4['objects'].append(a)
for n,pos,size,mat in [('White_Continuous_Equipment_Rear_Carrier',(0,1.43*.87,-.695),(5.56,2.65*.87,.028),'white'),('Dark_Continuous_Equipment_Plinth',(0,.075*.87,-.57),(5.56,.15*.87,.25),'darkgray')]:
 b4['objects'].append({'id':n,'group':'SourceEquipmentRecess','kind':'box','position':list(pos),'scale':list(size),'euler':[0,0,0],'material_asset':F4+'/MAT_'+mat+'.mat','collider':False,'source_ids':['ACTUAL2026WHITE_RECESS_CARRIER_ON_CORRECT_RETAINED_SIDEWALL']})
b4['source_visible_machine_count']=4;b4['reused_model_assets']=F4;b4['source_model_unit_width_m']=.7392;b4['source_model_unit_height_m']=1.898775;b4['old_wrong_central_partition_carrier_split_removed']=True;b4['actual_longitudinal_max_extent_m']=5.6
p5=json.loads((ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/public/self-ticket/constructor/SELF-TICKET-PACKET-R26.json').read_text());line=p5['placement_line_actual_q'];C=np.array(line['origin']);T=np.array(line['tangent']);N=np.array(line['normal']);baseline=p5['objects'][0]['yaw']
b5=bank('R29_COUNTER_FIVE_TVMS_SIDE_RETURN_R29',host('1331305054'),1,28.005002,[-7.173187,.057096],6.57,1.0,'2023May retained walk clip218..252 five-machine bank; 2022original fullplan and currentguide/primaryKORAILmeetingmap match counterEASTreturn')
for o in p5['objects']:
 if o['kind']!='library_mesh':raise RuntimeError(o['kind'])
 a=np.array([o['q'][0],-o['q'][1]])-C;u=float(a@T);v=float(a@N)
 if o['id']=='tickets-fascia':v+=.036 # retained actual old002 physicalink-to-housing11mm clearance
 b5['objects'].append({'id':o['id'],'group':o['group'],'kind':'mesh','position':[-u,o['y'],v],'scale':o['size'],'euler':[0,o['yaw']-baseline,0],'material_asset':F5+'/MAT_'+o['material']+'.mat','mesh_asset':F5+'/MESH_SHARED_'+o['mesh_key']+'.asset','collider':o['collider'],'source_ids':o['source_ids']})
b5['source_visible_machine_count']=5;b5['reused_model_assets']=F5;b5['actual_longitudinal_max_extent_m']=6.61;b5['public_portal_preservation']='Installation follows eastreturn only; no cross-portal front bar atqY0. Bothends retained withinhost6.61m envelope <=7.230283m actualreturn.'
assets=sorted(set(o[k]for b in[b4,b5]for o in b['objects']for k in['material_asset','mesh_asset']if k in o));missing=[a for a in assets if not(ROOT/a).exists()]
if missing:raise RuntimeError(missing)
authority=B/'remap-r29/SIDE-RETURN-CONSTRUCTION-AUTHORITY-R29.json';packet={'schema':'chooguard.r29.corrected_tvm_side_return_construction.v1','scope':'C180_R29_TVM_SIDE_RETURN_BOTH_BANKS_005','author':'classic:/root/r26_photo_booth','authority':str(authority.relative_to(ROOT)),'root_elected_multiplan_correspondence':'.state/ACTIVE-ALL2F-COMPLETION-R26/public/rail-police/multiplan-r28/POLICE-FOUR-PLAN-HOST-PROPOSAL-R28.json#TVM_source_to_face_addendum','structural_correspondence':'.state/ACTIVE-ALL2F-COMPLETION-R26/public/rail-police/multiplan-r28/SOURCE-TO-CURRENT-STRUCTURAL-POINT-CORRESPONDENCES-R28.json','forbidden_old_root_ids':['GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-525479395-0','GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-1048122836-0'],'banks':[b4,b5],'preserved_model_asset_paths':assets,'geometry_qualification':'Source-topology/model reconstruction elected by root after four-plan actual-column/returnpoint reconciliation. Human-proportioned sizes and depths are MODEL, not fieldmeasurements,99percent or new userconfirmation. Nursery source number9 is EXIT9, not boardingTrack9.','originals_native_floor_walls_columns_portals_T_junctions_unchanged':True,'no_light_Text_TMP_photo_only_signs':True,'agent_QA_Play_probe_reopen_capture':False}
f=D/'BOTH-TVM-SIDE-RETURN-PACKET-R29-005.json';f.write_text(json.dumps(packet,separators=(',',':'),allow_nan=False));print('banks',[(b['root_name'],len(b['objects']),b['origin_actual_q'],b['world_yaw'],b['longitudinal_actual_qY'])for b in[b4,b5]],'reuse',len(assets),'sha',hashlib.sha256(f.read_bytes()).hexdigest())
