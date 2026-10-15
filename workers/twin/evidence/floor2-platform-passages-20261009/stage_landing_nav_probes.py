from pathlib import Path
import json,hashlib,math
O=Path('workers/twin/output/floor2-platform-passages-20261009')
P=Path('Assets/ChooGuard/Art/Emergency/station-points.json')
R=Path('.planning/2026-10-09-floor2-guide-restoration/floor2-navigation-final-verification.json')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
points=json.loads(P.read_text());final=json.loads(R.read_text());entries={a['id']:a for a in points['escalators']}
probes=[]
xyz=lambda p:[p['x'],p['y'],p['z']]
def feet(id):
    e=entries[id];a,b=xyz(e['path'][0]),xyz(e['path'][-1]);return (b,a)if e['up']else(a,b)
def pair(id,a,b,kind,authority,area_mask):
    direct=math.dist(a,b)
    for reverse in [False,True]:
        start,end=(b,a)if reverse else(a,b)
        probes.append({'id':id+('-reverse'if reverse else'-forward'),'kind':kind,'from_world_foot':start,'to_world_foot':end,'authority':authority,'sample_radius_m':.55,'same_endpoint_floor_tolerance_m':.30,'area_mask':area_mask,'area_mask_excludes_existing_ride_and_elevator_links':True,'required_path_status':'PathComplete','direct_distance_m':direct,'maximum_local_path_length_m':direct*1.4+2,'same_floor_corner_Y_range':[min(a[1],b[1])-.30,max(a[1],b[1])+.30]if kind=='same_floor_landing_access'else None,'query_does_not_move_foot_endpoints':True,'actual_full_capsule_not_tested_by_nav_query':True})
mask=-1 & ~((1<<3)|(1<<4))
for id in ['esc-well-n-56','esc-well-s-1','esc-well-s-34']:
    top,bottom=feet(id);e=entries[id]
    pair(id+'-high-to-adjacent-stair',top,xyz(e['stairsTop']),'same_floor_landing_access','unchanged native high foot to existing canonical adjacent stair high foot',mask)
    pair(id+'-low-to-adjacent-stair',bottom,xyz(e['stairsBottom']),'same_floor_landing_access','unchanged native low foot to existing canonical adjacent stair low foot',mask)
    pair(id+'-adjacent-stair-flight',xyz(e['stairsTop']),xyz(e['stairsBottom']),'existing_native_stair_flight','existing canonical stair top/bottom; geometry support independently audited; no new link',mask)
top,bottom=feet('esc-well-m-89-1');other_top,other_bottom=feet('esc-well-m-89-0')
pair('esc-well-m-89-pair-high-landing',top,other_top,'same_floor_landing_access','unchanged paired native high feet, no inferred fixed-stair node',mask)
pair('esc-well-m-89-pair-low-landing',bottom,other_bottom,'same_floor_landing_access','unchanged paired native low feet, no inferred fixed-stair node',mask)
packet={'schema':'chooguard.floor2-existing-landing-nav-probes.v1','source_scene_sha256':final['scene_sha256'],'source_station_points':str(P),'source_station_points_sha256':sha(P),'final_verification':str(R),'final_verification_sha256':sha(R),'no_Unity_executed_by_lane':True,'no_source_or_endpoint_mutation':True,'canonical_escalator_operating_directions_unchanged':True,'instructions':'Root registers existing saved nav and all existing 22 canonical directed runtime links, then runs these exact foot queries. Use area_mask -25 to measure pure walking/stairs without escalator area3/elevator area4 shortcuts. Record SamplePosition actual endpoints, CalculatePath status/corners/length; for same-floor probes also require every corner within stated Y band. Do not move endpoints, widen sample radius or create new stairs/reverse transport links. Raw query success/failure remains distinct from physical capsule verification.','probes':probes}
(O/'landing-access-navigation-probes.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2));print(len(probes),sha(O/'landing-access-navigation-probes.json'))
