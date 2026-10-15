#!/usr/bin/env python3
"""Read-only delivery receipt, graph encoding and cumulative Door-pose audit."""
from pathlib import Path
import hashlib, importlib.util, json, re
import numpy as np

ROOT=Path.cwd();OUT=ROOT/'workers/twin/evidence/floor2-contract-review-20261009/final-delivery';DOC=ROOT/'docs/BusanStation_2F_Guide_20261009'
spec=importlib.util.spec_from_file_location('h',ROOT/'workers/twin/evidence/floor1-saved-scene-audit-20261009/audit_scene.py');h=importlib.util.module_from_spec(spec);spec.loader.exec_module(h)
v=json.loads((DOC/'validation.json').read_text());checks=[]
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def check(name,good,detail):checks.append({'id':name,'pass':bool(good),'detail':detail})
def unwrap(x):
    if isinstance(x,str):return unwrap(json.loads(x))
    if isinstance(x,dict) and 'data' in x and 'result' in x['data']:return unwrap(x['data']['result'])
    if isinstance(x,dict) and 'result' in x and 'summary' not in x:return unwrap(x['result'])
    return x

check('current_scene_sha',sha(ROOT/v['scene']['path'])==v['scene']['sha256'],sha(ROOT/v['scene']['path']))
for x in v['production_and_packet_manifest']:
    check('production_manifest_'+x['path'],sha(ROOT/x['path'])==x['sha256'],x['path'])
for x in v['delivery_file_manifest']:
    path=DOC/x['file'];check('delivery_manifest_'+x['file'],path.stat().st_size==x['bytes'] and sha(path)==x['sha256'],x['file'])

total=passed=failed=edit=play=0;names=[];test_runs=[]
for run in v['fresh_current_saved_scene_tests']['runs']:
    actual=unwrap(json.loads((DOC/run['evidence']).read_text()))
    check('test_run_exact_'+run['evidence'],actual['summary']==run['summary'] and actual['results']==run['results'] and actual['status']=='completed',actual['summary'])
    summary=actual['summary'];total+=summary['total'];passed+=summary['passed'];failed+=summary['failed'];names.extend(x['FullName'] for x in actual['results'])
    if run['mode']=='EditMode':edit+=summary['total']
    else:play+=summary['total']
    test_runs.append({'evidence':run['evidence'],'total':summary['total'],'passed':summary['passed'],'mode':run['mode']})
check('13_unique_fresh_tests',total==passed==len(set(names))==13 and failed==0 and edit==7 and play==6,{'total':total,'passed':passed,'failed':failed,'edit':edit,'play':play})

graph_path=ROOT/'Assets/ChooGuard/Art/Emergency/Resources/StationRoutes.json';graph=json.loads(graph_path.read_text());node_count=len(graph['labels']);flat_count=len(graph['positions']);edge_count=len(graph['from'])
check('node_xyz_encoding',node_count==v['route_graph']['node_count']==1826 and flat_count==3*node_count==5478,{'node_count':node_count,'flattened_xyz_values':flat_count})
check('edge_encoding',edge_count==v['route_graph']['edge_count']==7579 and all(len(graph[k])==edge_count for k in ['to','length','link']) and all(0<=i<node_count for i in graph['from']+graph['to']),edge_count)
check('graph_report_exact',graph['report']==v['route_graph']['original_report'],graph['report'])
report=graph['report'];check('graph_reachability_scope',re.search(r'893 nodes reach no exit',report) is not None and re.search(r'1441/1441 places reach an exit',report) is not None and v['route_graph']['other_graph_nodes_reach_no_exit']==893 and v['route_graph']['selected_non_exit_places_reach_an_exit']=='1441/1441' and not v['route_graph']['all_world_nodes_or_every_landing_certified'],{'unconnected_other_nodes':893,'selected_non_exit_places':'1441/1441'})

nav=json.loads((DOC/v['navigation']['raw_verifier']).read_text());diagnosis=json.loads((DOC/v['navigation']['diagnosis']).read_text());failures=diagnosis['remaining_real_navmesh_landing_disconnections']
check('four_nav_failures_retained',not nav['passed'] and not v['navigation']['overall_pass'] and len(failures)==4 and failures==v['navigation']['remaining_same_floor_landing_disconnections'] and all(x['bidirectional_same_floor_path_status']=='PathPartial' for x in failures),failures)
check('nav_saved_scene_sha',nav['scene_sha256']==v['scene']['sha256'],nav['scene_sha256'])
check('mouth_and_native_entry_local_queries',len(nav['mouths'])==len(nav['entry_doors'])==4 and all(x['passed'] for x in nav['mouths']+nav['entry_doors']) and v['navigation']['four_mouth_local_queries_passed']==8 and v['navigation']['four_native_entry_local_queries_passed']==8,{'mouths':len(nav['mouths']),'entries':len(nav['entry_doors'])})
dense_count=sum(len(x['dense_samples']) for x in nav['mouths']);check('dense296_samples',dense_count==v['navigation']['mouth_dense_floor_and_nav_samples_passed']==296 and all(x['all_dense_samples_same_floor_and_physically_supported'] for x in nav['mouths']),dense_count)
check('native15_direction_floor_scope',len(nav['native_vertical_axes'])==15 and diagnosis['confirmed_passes']['preserved_native_operating_directions']['count']==15 and diagnosis['confirmed_passes']['preserved_native_operating_directions']['all_pass'] and diagnosis['confirmed_passes']['original_native_vertical_floor_samples']['axes']==15 and diagnosis['confirmed_passes']['original_native_vertical_floor_samples']['all_pass'],15)
check('registered22_runtime_links',nav['canonical_runtime_links_registered']==v['navigation']['canonical_runtime_links']==22,nav['canonical_runtime_links_registered'])

axis=json.loads((DOC/'landing-axis-character-results.json').read_text());lateral=json.loads((DOC/'landing-lateral-character-results.json').read_text());check('axis8_directional_walks',axis['passed'] and len(axis['results'])==v['actual_character_movement']['short_axis_walks']==8 and all(x['passed'] for x in axis['results']),8);check('detour4_directional_walks',lateral['passed'] and len(lateral['results'])==v['actual_character_movement']['two_original_apron_detours']==4 and all(x['passed'] and x['actual_floor_support'] for x in lateral['results']),4)
check('incomplete_field_fidelity_explicit',not v['whole_requested_real_world_identical_floor2_complete'] and not v['physical_field_accuracy_verified'] and not v['reference_scope']['independent_metric_dimensions_and_hidden_room_interiors_verified'] and not v['reference_scope']['nerf_or_3dgs_model_trained_in_this_pass'] and v['reference_scope']['facility_unique_owner_assignments_unresolved']==11 and len(v['reference_scope']['dated_tenant_artwork_differences_unresolved'])==6,'Whole floor field fidelity remains unverified; four navigation disconnections remain open')

base,_,bd,_=h.load_docs(ROOT/'.planning/2026-10-09-floor2-guide-restoration/before-2f-entry-guide.unity');final,_,ad,_=h.load_docs(ROOT/v['scene']['path']);bs,cs=h.Scene(bd),h.Scene(ad);doors=[cid for cid,d in bd.items() if d['type']==114 and 'StationDoor' in d['data'].get('m_EditorClassIdentifier','')];changed=[];native_count=0;changed_native=[]
for cid in doors:
    go=h.ref_id(bd[cid]['data']['m_GameObject']);tid=bs.transform_for_go[go];old,_=bs.world(tid);new,_=cs.world(tid);path=bs.component_path(cid);native=path=='FPSWorld' or path.startswith('FPSWorld/')
    if native:native_count+=1
    if not np.array_equal(old,new):
        row={'component_id':cid,'transform_id':tid,'element':bd[cid]['data']['Element'],'native':native,'path':path,'before_world_position':old[:3,3].tolist(),'after_world_position':new[:3,3].tolist(),'world_position_delta':(new[:3,3]-old[:3,3]).tolist(),'basis_unchanged':np.array_equal(old[:3,:3],new[:3,:3])};changed.append(row)
        if native:changed_native.append(row)
serialized_changed=[cid for cid in doors if bd[cid]['raw']!=ad[cid]['raw']]
check('all103_serialized_door_fields_cumulative',len(doors)==103 and not serialized_changed,{'door_count':len(doors),'changed':serialized_changed})
check('original_native_door_poses_cumulative',native_count>0 and not changed_native,{'native_door_count':native_count,'changed':changed_native})
four_bay=json.loads((ROOT/'workers/twin/output/floor2-main-restoration-20261009/four-bay-packet.json').read_text());motion_ids={str(x['source_transform_id']) for x in four_bay['movable_transforms']}
check('exact_four_declared_generated_door_root_moves',len(changed)==4 and all(not x['native'] and x['transform_id'] in motion_ids for x in changed),changed)
check('latest_door_delivery_claims_scoped',v['saved_scene_preservation'].get('all_103_serialized_station_door_component_fields_unchanged') is True and v['saved_scene_preservation'].get('original_native_door_world_poses_unchanged') is True and v['saved_scene_preservation'].get('four_archived_generated_door_roots_moved_in_earlier_bay_correction') is True and 'all_103_door_fields_and_poses_unchanged' not in v['saved_scene_preservation'],v['saved_scene_preservation'])

saved=json.loads((DOC/v['saved_scene_preservation']['evidence']).read_text());check('saved83_independent_checks',saved['status']=='pass' and len(saved['checks'])==v['saved_scene_preservation']['independent_checks_passed']==83 and all(x['pass'] for x in saved['checks']),83);check('saved42_cumulative_authorized_documents',len(saved['cumulative_changes'])==v['saved_scene_preservation']['existing_documents_changed_only_within_reviewed_2f_scope']==42 and saved['saved_scene_sha256']==v['scene']['sha256'],42)
result={'status':'pass' if all(x['pass'] for x in checks) else 'needs_correction','reviewed_readme_sha256':sha(DOC/'README.md'),'reviewed_validation_sha256':sha(DOC/'validation.json'),'scene_sha256':sha(ROOT/v['scene']['path']),'test_runs':test_runs,'route_encoding':{'nodes':node_count,'flattened_xyz_values':flat_count,'edges':edge_count},'cumulative_door_pose_proof':{'baseline_sha256':h.sha(base),'final_sha256':h.sha(final),'door_count':len(doors),'serialized_field_changes':serialized_changed,'native_door_count':native_count,'native_world_pose_changes':changed_native,'generated_world_pose_changes':changed,'all103_world_poses_unchanged':not changed},'checks':checks,'limits':'Evidence reconciliation only; no new Unity or field verification. README directional versus round-trip wording is reviewed separately.'}
(OUT/'audit-results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'status':result['status'],'checks':len(checks),'failed':[x for x in checks if not x['pass']],'route_encoding':result['route_encoding'],'door_count':len(doors),'native_door_count':native_count,'changed_generated_door_count':len(changed),'all103_world_poses_unchanged':not changed},ensure_ascii=False,indent=2))
