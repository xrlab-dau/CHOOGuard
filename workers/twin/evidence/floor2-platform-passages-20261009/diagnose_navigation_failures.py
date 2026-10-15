from pathlib import Path
import json,hashlib,math,struct
import numpy as np
E=Path('workers/twin/evidence/floor2-platform-passages-20261009');O=Path('workers/twin/output/floor2-platform-passages-20261009')
reportpath=Path('.planning/2026-10-09-floor2-guide-restoration/floor2-navigation-verification.json')
pointspath=Path('Assets/ChooGuard/Art/Emergency/station-points.json')
packetpath=O/'four-mouths-physical-walk-spines.json'
inventorypath=Path('.planning/2026-10-09-floor2-guide-restoration/architecture/current/current-scene-structure.json')
componentpath=Path('workers/twin/evidence/floor2-main-restoration-20261009/whole-guide-scene-components.json')
dependency=Path('workers/twin/output/remaining-native-doors-20261009/sliding-leaf-dependencies.json')
blockerpath=Path('.planning/2026-10-09-floor2-guide-restoration/door15-seal-blockers.json')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
read=lambda p:json.loads(Path(p).read_text())
r=read(reportpath);points=read(pointspath);packet=read(packetpath);inv=read(inventorypath);components=read(componentpath);dep=read(dependency)
angle=math.radians(16.2);sn,cs=math.sin(angle),math.cos(angle)
def UVY(p):return [float(p[0]*sn+p[2]*cs),float(p[0]*cs-p[2]*sn),float(p[1])]
def XYZ(p):return [float(p[0]*sn+p[1]*cs),float(p[2]),float(p[0]*cs-p[1]*sn)]
links=[]
catalog={a['id']:a for a in points['escalators']}
staged={a['name']:a for a in packet['native_vertical_paths']}
for a in points['escalators']:
    start,end=a['path'][0],a['path'][-1]
    row={'id':a['id'],'label':a['label'],'source':'station-points.escalators.path endpoints','start_world':start,'end_world':end,'bidirectional':False,'area':3,'width':0,'costModifier':-1,'up':a['up'],'root_created_at_runtime':'EmergencySession/승강 설비/'+a['label']}
    if a['id']in staged:
        w=staged[a['id']]['forward_world_foot_XYZ'];to=[start[k]for k in ['x','y','z']];fr=[end[k]for k in ['x','y','z']]
        errs=[math.dist(w[0],fr if a['up']else to),math.dist(w[-1],to if a['up']else fr)]
        row.update({'staged_foot_endpoints_max_delta':max(errs),'staged_descending_direction_is_link_direction':not a['up'],'native_tread_root':staged[a['id']]['native_root_path']})
    links.append(row)
for a in points['elevators']:
    for i in range(len(a['stops'])):
        for j in range(i+1,len(a['stops'])):
            links.append({'id':a['id']+':'+str(i)+'-'+str(j),'label':a['label'],'source':'station-points.elevators.stops pair','start_world':a['stops'][i]['door'],'end_world':a['stops'][j]['door'],'bidirectional':True,'area':4,'width':0,'costModifier':-1,'root_created_at_runtime':'EmergencySession/승강 설비/'+a['label']})
partial=[];height=[]
for axis in r['native_vertical_axes']:
    if axis['top_to_bottom_path']['status']=='PathPartial' or axis['bottom_to_top_path']['status']=='PathPartial':
        observations=[]
        for direction in ['top_to_bottom_path','bottom_to_top_path']:
            q=axis[direction];corners=q['actual_corners_world']
            observations.append({'direction':direction,'status':q['status'],'length':q['actual_corner_length_m'],'actual_corners_world':corners,'stopped_world':corners[-1]if corners else None,'target_nav_world':q['to']['position'],'distance_stopped_to_target':math.dist(corners[-1],q['to']['position'])if corners else None})
        partial.append({'lane_id':axis['name'],'all_physical_samples_pass':axis['all_staged_samples_have_actual_expected_floor_hit'],'endpoints_sample_same_floor_within_055':all(q['from']['same_floor_within_radius']and q['to']['same_floor_within_radius']for q in [axis['top_to_bottom_path'],axis['bottom_to_top_path']]),'raw_component_observations':observations,'canonical_missing_runtime_link':next(a for a in links if a['id']==axis['name']),'component_ids_not_exported':True,'cause_not_yet_live_link_verified':'Raw graph excludes known production links; actual source/island connection is not proven repaired until identical live-link and physical checks pass.'})
    for i,sample in enumerate(axis['staged_saved_navigation_floor_samples']):
        if not sample['actual_expected_floor_hit']:
            a=catalog[axis['name']];sourceindex=len(a['path'])-1-i if a['up']else i;old=a['path'][sourceindex];hit=sample['nearest_upward_surface']
            height.append({'lane_id':axis['name'],'descending_staged_index':i,'canonical_internal_path_index':sourceindex,'is_foot_endpoint':sourceindex in [0,len(a['path'])-1],'old_canonical_path_world':old,'actual_nearest_upward_source_hit':hit,'down_hits':sample['down_hits'],'same_XZ_candidate_canonical_Y_only':{**old,'y':hit['world_point'][1]},'source_generator_failure':'StationSurvey.Surface selects hit closest to expected guess, so the lower substrate wins over the .453 tread at these internal points.','disposition':'Preserve sealed old packet. Root may stage a separately versioned, exact-source internal-Y correction with original endpoint/XZ preservation, then verify nearest real surface and physical traverse; do not remove the true tread or relax .30 tolerance.'})
assert len(partial)==4 and len(height)==2 and all(not a['is_foot_endpoint']for a in height)
ms={a['id']:a for a in inv['meshes']}
def meshrow(owner):
    m=ms[owner['mesh']];b=Path(m['binary']).read_bytes();n,k,_=struct.unpack_from('<iii',b);v=np.frombuffer(b,dtype='<f4',count=n*3,offset=12).reshape(-1,3);t=np.frombuffer(b,dtype='<i4',count=k,offset=12+n*12).reshape(-1,3);M=np.array(owner['world_matrix']);w=v@M[:3,:3].T+M[:3,3];return m,v,t,w
path='FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_0'
mc=next(a for a in inv['colliders']if a['path']==path);mf=next(a for a in inv['renderers']if a['path']==path)
cm,cv,ct,cw=meshrow(mc);fm,fv,ft,fw=meshrow(mf)
key=lambda p:tuple(sorted(tuple(np.round(v,5))for v in p))
targets={key(cw[ct[j]]):j for j in [47716,44432]};matches={j:[]for j in [47716,44432]}
for j,tri in enumerate(ft):
    k=key(fw[tri])
    if k in targets:matches[targets[k]].append(j)
facets=[{'collider_triangle':j,'source_vertex_indices':ct[j].tolist(),'source_local_points':cv[ct[j]].tolist(),'world_points':cw[ct[j]].tolist(),'native_UVY_points':[UVY(p)for p in cw[ct[j]]],'renderer_coordinate_matching_triangle_ordinals':matches[j],'removed_or_moved':False}for j in [47716,44432]]
native=next(a for a in components['all_doors']if a['element']=='native-auto-9783ce238277b47411a9f63a');kit=next(a for a in components['all_doors']if a['element']=='2F-main-front-15-호두나니&부산오뎅');binding=next(a for a in dep['bindings']if a['source_button_id']=='9783ce238277b47411a9f63a')
centre=np.array(native['native_UVY'][:2]);along=np.array(UVY(binding['along_current_world'])[:2]);along/=np.linalg.norm(along);normal=np.array(UVY(binding['normal_current_world'])[:2]);normal/=np.linalg.norm(normal);half=native['fields']['Width']/2;ends=[(centre-along*half).tolist(),(centre+along*half).tolist()]
geometry={'schema':'chooguard.door15-native-aperture-association.v1','reference_only':True,'actual_source_geometry':True,'site_identity_of_tenant_frontage_not_verified':True,'native_door':native,'generated_tenant_door':kit,'native_binding':binding,'native_along_UV':along.tolist(),'native_normal_UV':normal.tolist(),'native_door_closed_plane_endpoints_UV':ends,'native_aperture_V_minmax':[min(a[1]for a in ends),max(a[1]for a in ends)],'generated_door_V_minmax':[kit['native_UVY'][1]-.9,kit['native_UVY'][1]+.9],'kit_plus_V_overshoot_into_native_fixed_bay':kit['native_UVY'][1]+.9-max(a[1]for a in ends),'original_fixed_jamb_faces':facets,'render_source':{'owner_global_id':mf['global_object_id'],'owner_world_matrix':mf['world_matrix'],'mesh_metadata':fm,'source_file_sha256':sha(fm['asset'])},'collision_source':{'owner_global_id':mc['global_object_id'],'owner_world_matrix':mc['world_matrix'],'mesh_metadata':cm,'source_file_sha256':sha(cm['asset'])},'live_seal_rays':read(blockerpath),'initial_stale_seal_only_hypothesis':'contradicted by current actual DoorSeal ray hits; withdrawn','bounded_resolution':{'native_doors_and_jambs':'retain original pose, widths, source meshes, public runtime policy and single authoritative source-leaf drive','generated_bay21_frontage':'correct generated opening/aperture correspondence within existing native tilted clear envelope, rather than cutting true jamb for the rectangular kit door','overlapping_leaf_control':'native-auto-9783... owns actual leaf operation; any tenant interaction proxy must delegate instead of two controllers driving the same source leaves','no_unproven_serial_door_invention':True,'unverified_gate7_identity':'The old Gate7 proof has a historical assembly centre; no current glyph-to-native-auto-9783 identity equivalence is established by this leaf metadata. Do not delete/move Gate7.','root_gates':['independent full-width actual source-native aperture clearance with real native door operated','actual floor on both sides and proper route/door use semantics','guide/photo association before public entrance is relabelled or converted to a tenant boundary','generated-only frontage update and source-builder regeneration/preservation','actual nav crossing ≤3m and live capsule, unchanged original source geometry']}}
(O/'door15-native-aperture-diagnosis.json').write_text(json.dumps(geometry,ensure_ascii=False,indent=2))
files=[reportpath,pointspath,packetpath,inventorypath,componentpath,dependency,blockerpath,Path('Assets/ChooGuard/App/Fps/Emergency/StationWorld.cs'),Path('Assets/ChooGuard/App/Fps/Emergency/StationLinks.cs'),Path('Assets/ChooGuard/App/Fps/Emergency/BakedFloor.cs'),Path('Assets/ChooGuard/Editor/StationNavigationBuilder.cs'),Path('Assets/ChooGuard/Editor/StationRouteBuilder.cs'),Path('Assets/ChooGuard/Editor/StationSurvey.cs'),Path('AgentScripts/MajibangBuilder.cs')]
diagnosis={'schema':'chooguard.floor2-navigation-failure-diagnosis.v1','mode':'metadata_only_no_Unity_or_Assets_mutation','evidence':[{'path':str(p),'sha256':sha(p)}for p in files],'actual_original_verification_stays_FAIL':not r['passed'],'four_mouths_passed':all(a['passed']for a in r['mouths']),'mouth_dense_samples_passed':sum(len(a['dense_samples'])for a in r['mouths']),'entry15_disposition':'Current source-native leaf and fixed jamb are real blockers for generated doorway envelope, not merely stale seal. See door15-native-aperture-diagnosis.json.','missing_production_links':links,'missing_escalator_links':21,'missing_elevator_links':1,'canonical_native_endpoint_max_delta':max(a.get('staged_foot_endpoints_max_delta',0)for a in links),'raw_partial_axes':partial,'actual_internal_surface_height_failures':height,'recommended_check_partitions':[{'id':'raw_saved_floor','scope':'four-mouth endpoints/dense same-floor+actual floor, four generated/native accepted entry crossings','same_acceptance_limits':True},{'id':'live_production_link_graph','scope':'same exact fifteen top/bottom feet, known canonical one-way area3 and elevator area4 links; verify actual registration validity and corners/length','same_acceptance_limits':True,'no_fake_reverse_links':True,'no_endpoint_relocation':True,'remaining_reverse_partials_remain_FAILURE':True},{'id':'real_vertical_surface_and_motion','scope':'all staged axis samples nearest actual upward surfaces, exact native treads, actual walking/riding; retain old failing expected-height receipt and separately verify source-corrected interior route metadata','same_acceptance_limits':True},{'id':'passenger_route_graph','scope':'rebake StationRoutes only after accepted nav/metadata changes; run RouteGraphTests in actual EmergencySession parent runtime; check profile excluding elevator and real door states'}],'exact_runtime_mirror':'StationRouteBuilder.AddLinks(StationPoints,List<NavMeshLinkInstance>) already adds the same saved canonical links as StationWorld/StationLinks; root may invoke helper by reflection for an ephemeral mirror and remove only owned instances, or start actual EmergencySession for Play verification. StationWorld(parent:null) does not add links.','unverified':'Whether all four raw partitions fully reconnect under the real directed runtime links is not tested by this lane. Do not claim PASS until actual root queries pass unchanged criteria.'}
(O/'navigation-failure-diagnosis.json').write_text(json.dumps(diagnosis,ensure_ascii=False,indent=2))
assert sha(packetpath)=='333ef0e109ea910324f4e7bec85685e3c3a539a7a325d326187120315b2d77ff'
print({'missing_runtime_links':len(links),'raw_partial_axes':[a['lane_id']for a in partial],'actual_internal_height_failures':[a['lane_id']for a in height],'native_aperture_V':geometry['native_aperture_V_minmax'],'kit_V':geometry['generated_door_V_minmax'],'fixed_face_render_matches':matches,'original_sealed_packet_preserved':True})
