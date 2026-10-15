from pathlib import Path
import json,math,heapq,hashlib,textwrap,sys
import numpy as np
from shapely.geometry import LineString,Point,Polygon
from shapely.ops import unary_union
from shapely import intersects_xy,set_precision

E=Path('workers/twin/evidence/floor2-platform-passages-20261009');O=Path('workers/twin/output/floor2-platform-passages-20261009')
# Reuse the read-only geometry section only: no source packet writer or plot writer executes.
source=(E/'audit_landing_approaches.py').read_text();header=source.split('for id,level in cases:')[0];body=textwrap.dedent(source.split('for id,level in cases:')[1].split('    rows=[]')[0])
step_aware='--step-aware'in sys.argv
if step_aware:body=body.replace('for delta in [.25,.75,1.5]:','for delta in [.4,.9,1.6,1.78]:')
spines=[]
cases=[('esc-well-s-34','high'),('esc-well-m-89-1','low')]if step_aware else [('esc-well-n-56','low'),('esc-well-s-1','high'),('esc-well-s-34','high'),('esc-well-m-89-1','low')]
for id,level in cases:
    scope={};exec(header,scope);scope.update(id=id,level=level);exec(body,scope)
    entries=scope['entries'];e=scope['e'];xyz=scope['xyz'];belt=scope['belt'];axis=scope['axis'];side=scope['side'];expected=scope['expected'];floorunion=scope['floorunion'];segments=scope['segments'];support=scope['support']
    target=xyz(e['stairsTop'if level=='high'else'stairsBottom'])if e.get('stairs')else xyz(entries['esc-well-m-89-0']['path'][0])
    # Original float seam error is measured <4.1 micrometres. This numerical tolerance never fabricates a sizable floor.
    floors=floorunion.buffer(.00001);xmin,xmax=-.5,5.5;ymin,ymax=-3.8,3.8;step=.05
    ds=np.arange(xmin,xmax+step/2,step);ss=np.arange(ymin,ymax+step/2,step);DD,SS=np.meshgrid(ds,ss,indexing='ij')
    XX=belt[0]+axis[0]*DD+side[0]*SS;ZZ=belt[2]+axis[2]*DD+side[2]*SS
    def world(point):return np.array([belt[0]+axis[0]*point[0]+side[0]*point[1],expected,belt[2]+axis[2]*point[0]+side[2]*point[1]])
    def local(point):return np.array([(point-belt)@axis,(point-belt)@side])
    def grid(point):return tuple(int(round((point[k]-[xmin,ymin][k])/step))for k in [0,1])
    def pos(index):return np.array([ds[index[0]],ss[index[1]]])
    vertices=[world(p)[[0,2]]for p in[(xmin-.5,ymin-.5),(xmax+.5,ymin-.5),(xmax+.5,ymax+.5),(xmin-.5,ymax+.5)]]
    region=Polygon(vertices)
    clipped=[set_precision(r[0],.00001).intersection(region)for r in segments]
    body_geometry=unary_union([r for r in clipped if not r.is_empty]).simplify(.00001,preserve_topology=True)
    results=[]
    for radius in [.34,.30]:
        # Eight-quadrant polygon buffer is slightly inflated, so its circle chord
        # never shrinks the actual requested source-clearance radius.
        obstacles=body_geometry.buffer(radius/math.cos(math.pi/32)+.00002,quad_segs=8)
        blocked=intersects_xy(obstacles,XX,ZZ)|~intersects_xy(floors,XX,ZZ)
        start,end=grid([0,0]),grid(local(target));path=None;reason=None
        if any(q[k]<0 or q[k]>=blocked.shape[k]for q in [start,end]for k in [0,1]):reason='START_OR_END_OUTSIDE_BOUNDED_REGION'
        elif blocked[start]or blocked[end]:reason='START_OR_END_INSIDE_ORIGINAL_STATIC_BODY_OR_UNSUPPORTED_FLOOR'
        else:
            pq=[(0,0,start)];dist={start:0};prev={};done=set()
            while pq:
                _,cost,q=heapq.heappop(pq)
                if q in done:continue
                done.add(q)
                if q==end:
                    keys=[q]
                    while keys[-1]!=start:keys.append(prev[keys[-1]])
                    path=[pos(z)for z in keys[::-1]];break
                for d in[(1,0),(-1,0),(0,1),(0,-1)]:
                    z=(q[0]+d[0],q[1]+d[1])
                    if not(0<=z[0]<len(ds) and 0<=z[1]<len(ss))or blocked[z]:continue
                    next_cost=cost+step
                    if next_cost<dist.get(z,1e20):
                        dist[z]=next_cost;prev[z]=q;h=np.linalg.norm(pos(z)-pos(end));heapq.heappush(pq,(next_cost+h,next_cost,z))
            if path is None:reason='NO_EXISTING_SOURCE_FLOOR_BODY_CLEAR_SPINE_IN_BOUNDED_2D_REGION'
        row={'buffer_radius_m':radius,'grid_m':step,'reason':reason,'path_found':path is not None}
        if path:
            simple=[path[0]];at=0
            while at<len(path)-1:
                far=len(path)-1
                while far>at+1:
                    line=LineString([world(path[at])[[0,2]],world(path[far])[[0,2]]])
                    if not line.intersects(obstacles)and floors.covers(line):break
                    far-=1
                simple.append(path[far]);at=far
            # Exact source belt and existing neighbor feet, rather than grid endpoints, anchor the candidate.
            coords=[world(z)for z in simple];coords[0]=belt.copy();coords[-1]=target.copy();way=[];samples=[];clear=True;continuous=True
            for a,b in zip(coords,coords[1:]):
                line=LineString([a[[0,2]],b[[0,2]]]);clear&=not line.intersects(obstacles);continuous&=floors.covers(line)
                n=int(math.ceil(np.linalg.norm(b[[0,2]]-a[[0,2]])/.20))
                for t in np.linspace(0,1,n+1):
                    p=a+(b-a)*t;hit=support(p)
                    samples.append({'expected_world':p.tolist(),'existing_source_floor':hit})
            for p in coords:
                hit=support(p);v=p.copy()
                if hit:v[1]=hit['world_Y']
                way.append({'world':v.tolist(),'source_floor':hit})
            row.update({'world_waypoints':way,'samples_max_spacing_m':.20,'source_floor_samples':samples,'missing_floor_samples':sum(r['existing_source_floor']is None for r in samples),'anchored_three_plane_buffer_sweep_clear':bool(clear),'anchored_floor_projection_continuous_at_measured_float_tolerance':bool(continuous),'length_m':float(sum(np.linalg.norm(a-b)for a,b in zip(coords,coords[1:]))),'actual_Unity_capsule_navigation_verified':False})
        results.append(row)
    spines.append({'lane_id':id,'landing':level,'unchanged_canonical_foot':scope['foot'].tolist(),'native_belt_endpoint':belt.tolist(),'actual_existing_neighbor_foot':target.tolist(),'candidates':results})
    (O/('landing-step-aware-lateral-spines-in-progress.json'if step_aware else'landing-lateral-spines-in-progress.json')).write_text(json.dumps(spines,ensure_ascii=False,indent=2))
    print(id,[(r['buffer_radius_m'],r['path_found'],r.get('reason'),r.get('length_m'),r.get('anchored_three_plane_buffer_sweep_clear'),r.get('missing_floor_samples'))for r in results])
result={'schema':'chooguard.native-existing-landing-lateral-spines.v1','source_inventory':str(scope['scope']['invpath']),'source_inventory_sha256':scope['sha'](scope['scope']['invpath']),'source_points_sha256':scope['sha'](scope['P']),'source_geometry_preserved_from3719_native_epoch':True,'scene_nav_sources_or_endpoints_modified':False,'not_a_Unity_capsule_or_nav_PASS':True,'source_floor_numerical_tolerance_m':.00001,'body_slices_above_floor_m':[.4,.9,1.6,1.78]if step_aware else[.25,.75,1.5],'low_step_surfaces_not_in_body_mask_m':.4 if step_aware else.25,'step_aware_variant_is_not_a_confirmation_of_climbability':step_aware,'bounded_paths':spines,'root_verification':'Physically move the actual .3radius CharacterController along each candidate belt-end→allwaypoints→existing-neighbor and reverse with exact native collisions and supported floor; source slices omit low climbable step edges/full spherical caps, so fail any actual blocker. Register saved NavMesh and existing22directedlinks and query every intended local spine sample with strict sameY/radius; do not move an endpoint beyond an uncrossable barrier or invent bridge/transport link.'}
(O/('landing-step-aware-lateral-root-physical-spines.json'if step_aware else'landing-lateral-root-physical-spines.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2))
