from pathlib import Path
import json,hashlib,math
import numpy as np
O=Path('workers/twin/output/floor2-platform-passages-20261009');R=Path('.planning/2026-10-09-floor2-guide-restoration')
B=R/'native-lane-build-boxes.json';P=Path('Assets/ChooGuard/Art/Emergency/station-points.json');S=O/'landing-lateral-root-physical-spines.json'
boxes=json.loads(B.read_text())['rows'];points={e['id']:e for e in json.loads(P.read_text())['escalators']};spines=json.loads(S.read_text())['bounded_paths'];sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def rotation(q):
    x,y,z,w=q
    return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
def interval(a,b,box,radius):
    rot=rotation(box['boxRotation']);centre=np.array(box['boxCentre']);half=np.array(box['boxSize'])/2+np.array([radius,0,radius]);a=rot.T@(a-centre);b=rot.T@(b-centre);d=b-a;lo,hi=0.,1.
    for k in range(3):
        if abs(d[k])<1e-9:
            if abs(a[k])>half[k]:return None
        else:
            bounds=sorted([(-half[k]-a[k])/d[k],(half[k]-a[k])/d[k]]);lo=max(lo,bounds[0]);hi=min(hi,bounds[1])
        if hi<lo:return None
    return [float(lo),float(hi)]
def belt_profile(point,box):
    e=points[box['id']];p=[np.array([r['x'],r['y'],r['z']])for r in e['path'][1:-1]];best=None
    for i,(a,b)in enumerate(zip(p,p[1:])):
        v=(b-a)[[0,2]];vv=float(v@v)
        if vv<1e-9:continue
        raw=float((point-a)[[0,2]]@v/vv);t=max(0,min(1,raw));projected=a+(b-a)*t;distance=float(np.linalg.norm((point-projected)[[0,2]]))
        if best is None or distance<best['planar_distance_to_belt_axis_m']:
            best={'path_interior_segment_index':i+1,'planar_distance_to_belt_axis_m':distance,'segment_raw_projection_t':raw,'closest_profile_world':projected.tolist(),'belt_profile_Y_m':float(projected[1]),'floor_Y_m':float(point[1]),'belt_profile_minus_source_floor_Y_m':float(projected[1]-point[1]),'is_vertical_overhead_belt_proven':False,'actual_source_head_ray_at_point_verified':False}
    return best
results=[]
for spine in spines:
    for candidate in spine['candidates']:
        if not candidate['path_found']:continue
        vertices=[np.array(r['world'])for r in candidate['world_waypoints']];lengths=[float(np.linalg.norm(b-a))for a,b in zip(vertices,vertices[1:])];prefix=np.cumsum([0]+lengths);variants=[]
        for radius in[0,.28]:
            hits=[]
            for i,(a,b)in enumerate(zip(vertices,vertices[1:])):
                for box in boxes:
                    hit=interval(a,b,box,radius)
                    if hit is None:continue
                    lo,hi=hit;lo_world=a+(b-a)*lo;hi_world=a+(b-a)*hi;mid=(lo_world+hi_world)/2
                    hits.append({'spine_segment_index':i,'lane_box_id':box['id'],'interval_t':hit,'spine_distance_interval_m':[float(prefix[i]+lo*lengths[i]),float(prefix[i]+hi*lengths[i])],'world_start':lo_world.tolist(),'world_end':hi_world.tolist(),'world_mid':mid.tolist(),'source_floor_Y_range_m':[float(min(lo_world[1],hi_world[1])),float(max(lo_world[1],hi_world[1]))],'box_world_Y_range_m':[box['boxCentre'][1]-box['boxSize'][1]/2,box['boxCentre'][1]+box['boxSize'][1]/2],'closest_belt_profile_at_midpoint':belt_profile(mid,box),'box_is_own_lane':box['id']==spine['lane_id']})
            variants.append({'box_XZ_inflation_radius_m':radius,'all_hits':hits,'hits_beyond_first_native_belt_walkoff_m':1,'hits_beyond_one_metre_start':sum(h['spine_distance_interval_m'][1]>1.00001 for h in hits),'maximum_hit_spine_distance_m':max([h['spine_distance_interval_m'][1]for h in hits],default=None),'any_other_lane_box_hit':any(not h['box_is_own_lane']for h in hits)})
        results.append({'spine_lane':spine['lane_id'],'source_spine_buffer_radius_m':candidate['buffer_radius_m'],'spine_length_m':candidate['length_m'],'actual_source_supported_waypoints':candidate['world_waypoints'],'tests':variants})
out={'schema':'chooguard.source-lateral-spine-21lane-mask-intersections.v1','authority':[{'path':str(p),'sha256':sha(p)}for p in[B,P,S]],'source_geometry_or_mask_modified':False,'intersection_method':'Exact parametric segment against actual quaternion OBB; local X/Z expanded by0.28 for conservative walking radius, Y not changed. Every candidate source-floor waypoint used. Belt profile interpolates current canonical interior belt nodes and records planar projection; it is not an independent vertical source ray.','spines':results,'mask_under_belt_as_cause_of_four_islands_proven':False}
p=O/'lateral-spine-lane-mask-intersections.json';p.write_text(json.dumps(out,ensure_ascii=False,indent=2))
for r in results:
    print(r['spine_lane'],r['source_spine_buffer_radius_m'],[(t['box_XZ_inflation_radius_m'],len(t['all_hits']),t['maximum_hit_spine_distance_m'],t['hits_beyond_one_metre_start'],t['any_other_lane_box_hit'])for t in r['tests']])
    for t in r['tests']:
        for h in t['all_hits']:print(' ',h['lane_box_id'],h['spine_distance_interval_m'],'belt-floor',h['closest_belt_profile_at_midpoint']['belt_profile_minus_source_floor_Y_m'])
print('SHA',sha(p))
