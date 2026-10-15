"""Read-only bounded exact native floor/section derivation, no Unity claims.

Starts from canonical native feet. Keeps unsupported or blocked outcomes explicit.
The parent must verify every candidate using the actual CharacterController.
"""
from pathlib import Path
import hashlib, heapq, json, math, textwrap, struct
import numpy as np
from shapely import intersects_xy, set_precision
from shapely.geometry import Point, Polygon, LineString
from shapely.ops import unary_union
from shapely.strtree import STRtree

BASE = Path('workers/twin/evidence/floor2-platform-passages-20261009')
OUT = Path('workers/twin/output/floor2-landing-solution-20261009')
src = (BASE / 'audit_landing_approaches.py').read_text()
header = src.split('for id,level in cases:')[0]
header += '''\nscope['invpath']=Path('.planning/2026-10-09-floor2-completion-pass2/current/current-inventory.json')
x=json.loads(scope['invpath'].read_text()); ms={m['id']:m for m in x['meshes']}
scope['x']=x; scope['ms']=ms
def load(m):
 b=Path(m['binary']).read_bytes();n,k,_=__import__('struct').unpack_from('<iii',b)
 return np.frombuffer(b,dtype='<f4',count=n*3,offset=12).reshape(-1,3),np.frombuffer(b,dtype='<i4',count=k,offset=12+n*12).reshape(-1,3)
'''
body = textwrap.dedent(src.split('for id,level in cases:')[1].split('    rows=[]')[0])
# Wider measured native domain, including the ends of source vestibule walls.
body = body.replace('centre[[0,2]]-7', 'centre[[0,2]]-19').replace('centre[[0,2]]+7','centre[[0,2]]+19')
# Keep exact upward source faces; include exposed low steps but never synthesize floor.
body = body.replace('expected+.25', 'expected+.45').replace('expected-.25','expected-.45')
body = body.replace('for delta in [.25,.75,1.5]:','for delta in [.45,.9,1.6,1.78]:')
rows=[]
for lane, level in [('esc-well-s-34','high'),('esc-well-m-89-1','low')]:
    env={};exec(header,env);env.update(id=lane,level=level);exec(body,env)
    print(lane,'source loaded',len(env['floors']),len(env['segments']),flush=True)
    fs=env['floors'];fi=STRtree([r[0] for r in fs])
    def support(p):
        found=[];point=Point(p[[0,2]])
        for idx0 in fi.query(point.buffer(.00001)):
            poly,tri,owner,mesh,ordinal=fs[idx0]
            if not poly.buffer(.00001).covers(point):continue
            bc=np.linalg.solve(np.column_stack((tri[:,[0,2]],np.ones(3))).T,[p[0],p[2],1])
            found.append({'world_Y':float(bc@tri[:,1]),'owner':owner['path'],'global_object_id':owner['global_object_id'],'mesh_asset':mesh['asset'] if mesh else None,'mesh_guid':mesh['asset_guid'] if mesh else None,'triangle_ordinal':ordinal,'barycentric':bc.tolist()})
        return max(found,key=lambda h:h['world_Y']) if found else None
    env['support']=support
    belt,axis,side,foot=[env[k] for k in ['belt','axis','side','foot']]
    target = env['xyz'](env['e']['stairsTop' if level=='high' else 'stairsBottom']) if env['e'].get('stairs') else env['xyz'](env['entries']['esc-well-m-89-0']['path'][0])
    def local(p): return np.array([(p-belt)@axis,(p-belt)@side])
    def world(q): return belt+axis*q[0]+side*q[1]
    # Smaller search first; all bounds remain explicit in receipts.
    xmin,xmax,ymin,ymax,step=-1.,15.,-12.,12.,.05
    ds=np.arange(xmin,xmax+step*.5,step);ss=np.arange(ymin,ymax+step*.5,step)
    D,S=np.meshgrid(ds,ss,indexing='ij');X=belt[0]+axis[0]*D+side[0]*S;Z=belt[2]+axis[2]*D+side[2]*S
    region=Polygon([world(q)[[0,2]] for q in [(xmin,ymin),(xmax,ymin),(xmax,ymax),(xmin,ymax)]])
    floors=env['floorunion'].buffer(.00001)
    geometry=unary_union([set_precision(s[0],.00001).intersection(region) for s in env['segments']]).simplify(.00001,preserve_topology=True)
    def idx(p):return tuple(int(round((p[k]-[xmin,ymin][k])/step)) for k in (0,1))
    def pos(k):return np.array([ds[k[0]],ss[k[1]]])
    variants=[]
    for radius in [.34,.30]:
        obstacle=geometry.buffer(radius/math.cos(math.pi/32)+.00002,quad_segs=8)
        blocked=intersects_xy(obstacle,X,Z)|~intersects_xy(floors,X,Z)
        start,end=idx(local(foot)),idx(local(target));reason=None;path=None;visited=0
        if any(k[a]<0 or k[a]>=blocked.shape[a] for k in [start,end] for a in [0,1]):reason='EXACT_CANONICAL_ENDPOINT_OUTSIDE_BOUNDS'
        elif blocked[start] or blocked[end]:reason='CANONICAL_START_BLOCKED' if blocked[start] else 'EXACT_NEIGHBOR_BLOCKED'
        else:
            pq=[(0.,0.,start)];dist={start:0.};prev={};done=set()
            while pq:
                _,cost,q=heapq.heappop(pq)
                if q in done:continue
                done.add(q)
                if q==end:
                    keys=[q]
                    while keys[-1]!=start:keys.append(prev[keys[-1]])
                    path=[pos(k) for k in keys[::-1]];break
                for delta in [(1,0),(-1,0),(0,1),(0,-1)]:
                    z=(q[0]+delta[0],q[1]+delta[1])
                    if not (0<=z[0]<len(ds) and 0<=z[1]<len(ss)) or blocked[z]:continue
                    nc=cost+step
                    if nc<dist.get(z,1e20):
                        dist[z]=nc;prev[z]=q;heapq.heappush(pq,(nc+np.linalg.norm(pos(z)-pos(end)),nc,z))
            visited=len(done)
            if path is None:reason='NO_ROUTE_THROUGH_SUPPORTED_FLOOR_IN_BOUND'
        print(lane,'searched',radius,visited,reason,'rawpoints',len(path) if path else 0,flush=True)
        row={'radius_m':radius,'path_found':path is not None,'reason':reason,'visited_nodes':visited,'start_source_floor':env['support'](foot),'end_source_floor':env['support'](target)}
        if path:
            # Preserve the grid line exactly while discarding redundant collinear vertices.
            path=[np.array(p) for p in LineString(path).simplify(.000001).coords]
            print(lane,'candidate path corners',len(path),flush=True)
            simple=[path[0]];at=0
            while at<len(path)-1:
                far=len(path)-1
                while far>at+1:
                    line=LineString([world(path[at])[[0,2]],world(path[far])[[0,2]]])
                    if not line.intersects(obstacle) and floors.covers(line):break
                    far-=1
                simple.append(path[far]);at=far
            pts=[world(q) for q in simple];pts[0]=foot.copy();pts[-1]=target.copy()
            samples=[];points=[];valid=True
            for i,(a,b) in enumerate(zip(pts,pts[1:])):
                line=LineString([a[[0,2]],b[[0,2]]]);valid &= not line.intersects(obstacle) and floors.covers(line)
                for t in np.linspace(0,1,max(1,int(math.ceil(np.linalg.norm(b[[0,2]]-a[[0,2]])/.12)))+1):
                    p=a+(b-a)*t;hit=env['support'](p)
                    samples.append({'segment':i,'world_staged':p.tolist(),'source_upward_face':hit})
            for p in pts:
                h=env['support'](p);v=p.copy()
                if h:v[1]=h['world_Y']
                points.append({'world':v.tolist(),'source_upward_face':h})
            row.update(points=points,samples=samples,anchored_source_projection_clear=bool(valid),length_m=float(sum(np.linalg.norm(a-b) for a,b in zip(pts,pts[1:]))))
        variants.append(row)
    record={'lane_id':lane,'landing':level,'canonical_foot':foot.tolist(),'native_belt':belt.tolist(),'existing_neighbor_foot':target.tolist(),'outward_axis':axis.tolist(),'side_axis':side.tolist(),'bounds_local_m':[xmin,xmax,ymin,ymax],'grid_m':step,'source_floor_Y_band_m':[env['expected']-.45,env['expected']+.45],'body_slice_offsets_m':[.45,.9,1.6,1.78],'variants':variants}
    rows.append(record)
    print(lane,[(r['radius_m'],r['path_found'],r['reason'],r.get('length_m')) for r in variants],flush=True)
    (OUT/'wide-source-route-candidates.json').write_text(json.dumps({'schema':'chooguard.native-landing-route-candidate.v1','actual_CC_or_NavMesh_PASS':False,'canonical_or_Unity_changes':False,'method':'Exact exported source collider faces; constant-floor sections omit low spherical caps and hidden-source exposure; candidates require exact physical registration and CC tests. Wider native region and .45 lowstep band.','routes':rows},ensure_ascii=False,indent=2))
