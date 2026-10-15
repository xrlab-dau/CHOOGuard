from pathlib import Path
import json, hashlib, math

E=Path('workers/twin/evidence/floor2-platform-passages-20261009')
O=Path('workers/twin/output/floor2-platform-passages-20261009')
path=O/'four-mouths-physical-walk-spines.json'
packet=json.loads(path.read_text())
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
sn=math.sin(math.radians(16.2));cs=math.cos(math.radians(16.2))
world=lambda p:[p[0]*sn+p[1]*cs,p[2],p[0]*cs-p[1]*sn]
uvy=lambda p:[p['x']*sn+p['z']*cs,p['x']*cs-p['z']*sn,p['y']]
topologypath=O/'four-mouths-final-topology.json'
navpath=Path('workers/twin/output/platform-escalators-20261009/platform-source-reconciliation.json')
typepath=Path('workers/twin/output/platform-escalators-20261009/platform-type-investigation.json')
topology=json.loads(topologypath.read_text())
nav=json.loads(navpath.read_text())
types={a['lane_id']:a for a in json.loads(typepath.read_text())['rows']}
mapped={a['lane_id']:a for a in topology['source_platform_lanes']}
assert len(packet['paths'])==4 and not packet['unreadable_colliders']
for row in packet['paths']:
    assert row['missing_floor_samples']==0 and row['continuous_centre_floor_projection_coverage'] and row['body_plane_sweep_clear'],row['name']
    assert row['reverse_UVY']==list(reversed(row['forward_UVY']))
    row['forward_world_foot_XYZ']=[world(p)for p in row['forward_UVY']]
    row['reverse_world_foot_XYZ']=list(reversed(row['forward_world_foot_XYZ']))
    guide=next(a for a in topology['passages']if a['guide_node']=='tracks-'+row['name'])
    row['guide_node']=guide['guide_node']
    row['navigation_target']={'native_banks':guide['native_banks'],'guide_neighbours':guide['guide_neighbours'],'world_foot_XYZ':row['forward_world_foot_XYZ'][-1]}
    if row['name']=='upper-left':
        assert sorted(row['must_hold_open_door_elements'])==['B-ENH-GATE-doors-inner-1','B-ENH-GATE-doors-outer-1']
    else:assert not row['must_hold_open_door_elements']
packet['native_vertical_paths']=[]
for lane in nav['lanes']:
    id=lane['lane_id'];match=mapped[id];points=[uvy(p)for p in lane['existing_navigation_path']]
    # The stored route may be ordered upwards. Deliver both directions explicitly
    # while preserving every original node and its saved height exactly.
    descending=points if points[0][2]>points[-1][2]else list(reversed(points))
    owners=match['current_native_tread_owners']
    assert len(owners)==41 and len(lane['source_parts'])==41
    packet['native_vertical_paths'].append({
        'name':id,'route_kind':'CURRENT_MODEL_ESCALATOR_AXIS',
        'status':'EXISTING_SOURCE_NAVIGATION_ROUTE_STAGED_RUNTIME_PENDING',
        'native_root_path':'FPSWorld/승강장 디딤판 운행/'+id,
        'forward_UVY':descending,'reverse_UVY':list(reversed(descending)),
        'forward_world_foot_XYZ':[world(p)for p in descending],
        'reverse_world_foot_XYZ':[world(p)for p in reversed(descending)],
        'source_parts_count':len(owners),'native_owner_records':owners,
        'source_parent':lane['source_parent'],
        'track_group_label_in_current_project':match['track_group_label_in_current_project'],
        'field_track_assignment_verified':False,
        'mechanism_classification':types[id]['classification'],
        'parallel_fixed_stair_route_staged':False,
        'all_original_navigation_nodes_preserved':True,
        'expected_Y_authority':'original saved navigation values, not a new independent surface raycast or site measurement',
        'runtime_precondition':'Use actual native drive safely stopped for reciprocal fixed-pose controller walking; restore prior operating state afterwards. Test riding separately.',
        'must_hold_open_door_elements':[],
        'static_geometry_hit_exemptions':[],
        'mouth_to_vertical_route_bridge_fabricated':False})
assert len(packet['native_vertical_paths'])==15
assert sum(a['source_parts_count']for a in packet['native_vertical_paths'])==615
packet['frame']={'rotation_y_degrees':16.2,'UVY_to_world_XYZ':'[U*sin(a)+V*cos(a),Y,U*cos(a)-V*sin(a)]','Y_is_foot_surface_height':True,'coordinates_are_current_native_model_units':True}
packet['source_provenance']=[{'path':str(p),'sha256':sha(p)}for p in [topologypath,navpath,typepath,E/'stage_walk_spines.py']]
packet['root_runtime_acceptance']={
    'mouth_runs':8,'native_vertical_runs':30,
    'required':'Actual CharacterController reaches each waypoint in both directions, stays grounded on expected floor, does not pass through static colliders, and only named operating doors are held open.',
    'failure_policy':'Keep exact hit owner/triangle/UVY and actual ground evidence. A hit against a real floor, jamb, column or rail does not authorize its deletion.',
    'no_full_physical_accuracy_claim':True}
packet['runtime_limitations'].extend([
    'Mouth static plane checks sample fixed world heights 7.5/8/8.5; overhead and full capsule contact remain actual runtime gates.',
    'Vertical saved navigation values are source correspondence, not current full-capsule support verification.',
    'No cross-deck bridge between the four mouths and vertical routes is inferred from the schematic guide.',
    'Separate parallel fixed stairs require direct current tread/floor geometry verification; historical catalog endpoints are not staged as physical walking paths.'])
path.write_text(json.dumps(packet,ensure_ascii=False,indent=2))
receipt={
    'schema':'chooguard.physical-walk-spine-staging-receipt.v1',
    'packet':str(path),'packet_sha256':sha(path),
    'scene_sha256':packet['scene_sha256'],
    'installation_receipt_sha256':packet['receipt_sha256'],
    'mouth_count':4,'mouth_bidirectional_runtime_runs':8,
    'mouth_floor_sample_count':sum(len(r['floor_support_samples'])for r in packet['paths']),
    'missing_mouth_floor_samples':0,'continuous_existing_centre_floor_coverage_all_mouths':True,
    'static_body_plane_sweep_clear_all_mouths':True,
    'named_operating_door_exemption_count':2,
    'unreadable_active_candidate_colliders':0,
    'native_vertical_route_count':15,'preserved_source_tread_owner_count':615,
    'native_vertical_bidirectional_runtime_runs':30,
    'current_assets_or_unity_changes_by_this_lane':False,
    'actual_runtime_verified':False,'field_accuracy_verified':False}
(E/'physical-walk-spine-staging-receipt.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2))
print(json.dumps(receipt,ensure_ascii=False,indent=2))
