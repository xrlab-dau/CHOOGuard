from pathlib import Path
import json, re, struct, math, heapq, hashlib
import numpy as np
from shapely.geometry import LineString,Polygon,Point,box
from shapely.ops import unary_union
from shapely import intersects_xy
from shapely.strtree import STRtree
E=Path('workers/twin/evidence/floor2-platform-passages-20261009');O=Path('workers/twin/output/floor2-platform-passages-20261009');invpath=Path('.planning/2026-10-09-floor2-guide-restoration/architecture/current/current-scene-structure.json');x=json.loads(invpath.read_text());ms={m['id']:m for m in x['meshes']};installed=json.loads(Path('.planning/2026-10-09-floor2-guide-restoration/2f-passages-installation.json').read_text());replacement={a['source_asset']:a['replacement_asset']for a in installed['meshes']};sn=math.sin(math.radians(16.2));cs=math.cos(math.radians(16.2));sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
def load(m):
 if m['asset']in replacement:
  txt=Path(replacement[m['asset']]).read_text();part=txt.split('m_VertexData:',1)[1].split('m_CompressedMesh:',1)[0];n=int(re.search(r'm_VertexCount: (\d+)',part)[1]);chs=[tuple(map(int,z))for z in re.findall(r'- stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)',part)];assert all(a==0 and f==0 for a,o,f,d in chs if d);stride=max(o+d*4 for a,o,f,d in chs);data=bytes.fromhex(re.search(r'_typelessdata: ([0-9a-f]+)',part)[1]);v=np.ndarray((n,3),dtype='<f4',buffer=data,offset=chs[0][1],strides=(stride,4)).copy();index=bytes.fromhex(re.search(r'm_IndexBuffer: ([0-9a-f]+)',txt)[1]);dt='<u4'if int(re.search(r'm_IndexFormat: (\d+)',txt)[1])else'<u2';t=np.frombuffer(index,dtype=dt).reshape(-1,3);return v,t
 b=Path(m['binary']).read_bytes();n,k,_=struct.unpack_from('<iii',b);return np.frombuffer(b,dtype='<f4',count=n*3,offset=12).reshape(-1,3),np.frombuffer(b,dtype='<i4',count=k,offset=12+n*12).reshape(-1,3)
regions={
 'upper-left':{'bounds':[53,58,76,74],'tries':[[[55,62.409],[73,62.397]],[[55,66.159],[73,66.118]],[[55,69.804],[73,69.868]]],'Y':7.0},
 'upper-right':{'bounds':[-29,50,-8,62],'tries':[[[-10,56],[-25,56]]],'Y':7.0},
 'diagonal-left':{'bounds':[51,22,79,35],'tries':[[[55,28.5],[75,28.5]]],'Y':7.0},
 'main-up':{'bounds':[-33.0,-1,-29.8,20],'tries':[[[-31.375,0],[-31.375,19]]],'Y':7.0}}
doors=json.loads(Path('.planning/2026-10-08-interior-runtime-completion/native-station-door-snapshot.json').read_text());allowed=[r for r in doors if r['element'].startswith('B-ENH-GATE-doors-')];allowedpaths=[r['path']for r in allowed]
# Cached current-world triangles; floor support and body slice are read from actual geometry.
geo=[];missing=[]
for r in x['colliders']:
 if not r.get('active')or not r.get('enabled')or r.get('trigger'):continue
 bbmin=r['world_bounds_min'];bbmax=r['world_bounds_max']
 if bbmax[1]<6.85 or bbmin[1]>8.8:continue
 if any(r['path'].startswith(p+'/')for p in allowedpaths):continue
 M=np.array(r['world_matrix'])
 if r.get('type')=='MeshCollider':
  m=ms.get(r.get('mesh'))
  if not m or not m.get('binary'):missing.append(r['path']);continue
  v,t=load(m);w=v@M[:3,:3].T+M[:3,3];p=w[t]
 elif r.get('type')=='BoxCollider':
  cen=np.array(r['local_center']);sz=np.array(r['local_size'])/2;v=np.array([cen+[a*sz[0],b*sz[1],c*sz[2]]for a,b,c in[(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),(1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]]);t=np.array([[0,1,3],[0,3,2],[4,6,7],[4,7,5],[0,4,5],[0,5,1],[2,3,7],[2,7,6],[0,2,6],[0,6,4],[1,5,7],[1,7,3]]);w=v@M[:3,:3].T+M[:3,3];p=w[t]
 else:continue
 q=np.stack((p[:,:,0]*sn+p[:,:,2]*cs,p[:,:,0]*cs-p[:,:,2]*sn,p[:,:,1]),axis=2);geo.append((r,p,q))
allresults=[]
for name,a in regions.items():
 bb=a['bounds'];domain=box(*bb);segments=[];owners=[];floor=[]
 for r,p,q in geo:
  qlo=q[:,:,:2].min(axis=1);qhi=q[:,:,:2].max(axis=1);cand=(qhi[:,0]>=bb[0])&(qlo[:,0]<=bb[2])&(qhi[:,1]>=bb[1])&(qlo[:,1]<=bb[3]);ords=np.flatnonzero(cand)
  if not len(ords):continue
  rowhits=[]
  for j in ords:
   yy=q[j,:,2]
   # Floors/steps at <=7.3 are passable; original colliders remain in actual runtime test.
   if yy.min()>=6.8 and yy.max()<=7.31:
    normal=np.cross(p[j,1]-p[j,0],p[j,2]-p[j,0]);
    if normal[1]>1e-8:floor.append((q[j,:,:2],yy,r['path'],int(j)))
   for height in [7.5,8.0,8.5]:
    if yy.min()>=height or yy.max()<=height:continue
    ints=[]
    for k,l in[(0,1),(1,2),(2,0)]:
     if(yy[k]-height)*(yy[l]-height)<0:ints.append(q[j,k,:2]+(q[j,l,:2]-q[j,k,:2])*(height-yy[k])/(yy[l]-yy[k]))
    if len(ints)==2 and np.linalg.norm(ints[1]-ints[0])>1e-7:
     seg=LineString(ints).intersection(domain)
     if not seg.is_empty:segments.append(seg);rowhits.append(int(j))
  if rowhits:owners.append({'path':r['path'],'global_object_id':r['global_object_id'],'type':r['type'],'triangle_ordinals':sorted(set(rowhits))})
 obstacle=unary_union(segments).buffer(.34) if segments else Polygon()
 floorpolys=[Polygon(q)for q,yy,path,tri in floor];floorindex=STRtree(floorpolys);floorunion=unary_union([p for p in floorpolys if p.area>1e-9]).buffer(1e-6)
 step=.10;us=np.arange(bb[0],bb[2]+step/2,step);vs=np.arange(bb[1],bb[3]+step/2,step);UU,VV=np.meshgrid(us,vs,indexing='ij');blocked=intersects_xy(obstacle,UU,VV)|~intersects_xy(floorunion,UU,VV)
 def ij(pt):return tuple(int(round((pt[k]-bb[k])/step))for k in[0,1])
 def pos(k):return [float(us[k[0]]),float(vs[k[1]])]
 def astar(start,end):
  start,end=ij(start),ij(end)
  if blocked[start]or blocked[end]:return None
  pq=[(0.,0.,start)];dist={start:0.};prev={};done=set()
  while pq:
   _,cost,q=heapq.heappop(pq)
   if q in done:continue
   done.add(q)
   if q==end:
    out=[q]
    while out[-1]!=start:out.append(prev[out[-1]])
    return [pos(v)for v in out[::-1]]
   for d in[(1,0),(-1,0),(0,1),(0,-1)]:
    z=(q[0]+d[0],q[1]+d[1])
    if not(0<=z[0]<len(us) and 0<=z[1]<len(vs))or blocked[z]:continue
    cc=cost+step
    if cc<dist.get(z,1e10):dist[z]=cc;prev[z]=q;h=math.hypot(z[0]-end[0],z[1]-end[1])*step;heapq.heappush(pq,(cc+h,cc,z))
  return None
 found=[]
 for start,end in a['tries']:
  path=astar(start,end)
  if path:
   # Exact static sweep simplification, retaining enough points around real posts and native panels.
   simple=[path[0]];at=0
   while at<len(path)-1:
    far=len(path)-1
    while far>at+1 and (LineString([path[at],path[far]]).intersects(obstacle)or not floorunion.covers(LineString([path[at],path[far]]))):far-=1
    simple.append(path[far]);at=far
   found.append(simple)
 if not found:
  allresults.append({'name':name,'status':'NO_SAFE_STATIC_SPINE_FOUND','region_bounds':bb,'obstacle_owners':owners,'tries':a['tries']});print(name,'NO PATH',len(segments));continue
 spine=min(found,key=lambda p:sum(np.linalg.norm(np.array(v)-u)for u,v in zip(p,p[1:])))
 continuousfloor=all(floorunion.covers(LineString([p,q]))for p,q in zip(spine,spine[1:]));assert continuousfloor,(name,'spine must continuously stay on actual existing floor projection')
 # Surface queries select actual existing upward floor faces. Preserve every point as a support receipt.
 def support(uv):
  best=None
  for idx in floorindex.query(Point(uv).buffer(1e-6)):
   q,yy,path,tri=floor[idx]
   if not Polygon(q).buffer(1e-6).covers(Point(uv)):continue
   A=np.column_stack((q,np.ones(3)))
   if abs(np.linalg.det(A))<1e-9:continue
   bc=np.linalg.solve(A.T,[uv[0],uv[1],1]);Y=float(np.dot(bc,yy));
   if best is None or Y>best['Y']:best={'Y':Y,'owner':path,'triangle':tri}
  return best
 samples=[];way=[]
 for i,(u,v)in enumerate(zip(spine,spine[1:])):
  length=np.linalg.norm(np.array(v)-u)
  for z in np.linspace(0,1,int(math.ceil(length/.25))+1):
   uv=(np.array(u)+(np.array(v)-u)*z).tolist();sup=support(uv);Y=sup['Y']if sup else a['Y'];samples.append({'UV':uv,'expected_floor_Y':Y,'floor_support':sup});
   if name=='upper-right':way.append([uv[0],uv[1],Y])
 if name!='upper-right':way=[[u,v,(support([u,v])or{'Y':a['Y']})['Y']]for u,v in spine]
 required=[]
 if name=='upper-left':
  vv=np.median([p[1]for p in spine]);required=[r['element']for r in allowed if abs(r['centre_world'][0]*cs-r['centre_world'][2]*sn-vv)<1.5]
 allresults.append({'name':name,'status':'STATIC_GEOMETRY_SPINE_STAGED_RUNTIME_PENDING','forward_UVY':way,'reverse_UVY':list(reversed(way)),'must_hold_open_door_elements':required,'door_exemptions_are_only_real_current_StationDoor_leaves':True,'radius_native_model_m':.30,'safety_margin_m':.04,'height_m':1.72,'step_offset_m':.3,'floor_support_samples':samples,'missing_floor_samples':sum(r['floor_support']is None for r in samples),'continuous_centre_floor_projection_coverage':continuousfloor,'floor_projection_epsilon_m':1e-6,'obstacle_owners':owners,'body_plane_sweep_clear':all(not LineString([p,q]).intersects(obstacle)for p,q in zip(spine,spine[1:]))});print(name,'SPINE',spine,'missingfloor',sum(r['floor_support']is None for r in samples),'doors',required)
packet={'schema':'chooguard.four-guide-mouth-physical-walk-spines.v1','scene_sha256':installed['scene_sha256'],'installation_receipt':'.planning/2026-10-09-floor2-guide-restoration/2f-passages-installation.json','receipt_sha256':sha('.planning/2026-10-09-floor2-guide-restoration/2f-passages-installation.json'),'source_export':str(invpath),'source_export_sha256':sha(invpath),'native_replacement_assets':[{'path':p,'sha256':sha(p)}for p in replacement.values()],'actual_runtime_verified':False,'reference_only':True,'physical_accuracy_verified':False,'method':'current installed MC meshes; exact body plane7.5/8/8.5 staticgeometry; r.3 plus.04 clearance; actual upward existing floor support; excludes only B-ENH-GATE real operating door leaves whileheldopen','paths':allresults,'unreadable_colliders':missing,'runtime_limitations':['Plane sampling isoffline candidate validation, not actual fullcapsule physics; root tests everyforward/reverse spine on actual CharacterController','Staticsource leafs/posts/rails are not exempted','Floor followsnativeY7.2 ramp; no geometrydeletion from constantYcapsulehit']}
(O/'four-mouths-physical-walk-spines.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2));print('saved',O/'four-mouths-physical-walk-spines.json')
