from pathlib import Path
import json,math,struct,hashlib
import numpy as np
from shapely.geometry import Polygon,LineString,Point,box
BASE=Path('workers/twin/evidence/floor2-platform-passages-20261009');OUT=Path('workers/twin/output/floor2-platform-passages-20261009');OUT.mkdir(parents=True,exist_ok=True)
x=json.loads(Path('.planning/2026-10-09-floor2-guide-restoration/architecture/current/current-scene-structure.json').read_text());meshes={m['id']:m for m in x['meshes']};s=math.sin(math.radians(16.2));c=math.cos(math.radians(16.2))
regions={
 'tracks-upper-left':{'bounds_uv':[60,59,72,73],'probe_path_uv':[[62,66.16],[70,66.16]],'mapping':'retail#5/#4 side; native three inner/outer automatic door sets at V62.4/66.1/69.8','confidence':'local-guide-adjacency-and-native-public-doors'},
 'tracks-upper-right':{'bounds_uv':[-28,50,-10,61],'probe_path_uv':[[-13,56],[-25,56]],'mapping':'retail#12+two EVs side; native SouthGate portal and transition ramp','confidence':'local-guide-adjacency-and-existing-source-opening'},
 'tracks-diagonal-left':{'bounds_uv':[45,8,77,26],'probe_path_uv':[[53,10],[53,19]],'mapping':'below retail#2 strip; native seam-north facade opening spanning U50–65, V11.5–12.4','confidence':'local-guide-adjacency-and-existing-source-opening; exact historical gate-number evidence pending'},
 'tracks-main-up':{'bounds_uv':[-46,2,-30,20],'probe_path_uv':[[-38,5],[-38,16]],'mapping':'retail#26/ticket-front side; native west overtrack deck/boarding opening','confidence':'local-guide-adjacency-and-existing-source-opening; width was inferred'}
}
def load(m):
 b=Path(m['binary']).read_bytes();nv,ni,nn=struct.unpack_from('<iii',b);o=12;v=np.frombuffer(b,dtype='<f4',count=nv*3,offset=o).reshape(-1,3);o+=nv*12;t=np.frombuffer(b,dtype='<i4',count=ni,offset=o).reshape(-1,3);return v,t
for g in regions.values():g.update({'mesh_owners':[],'floor_probes':[],'direct_path_section_blockers':[],'bounds':box(*g['bounds_uv'])})
for r in x['colliders']:
 if r.get('type')!='MeshCollider'or not r.get('active')or not r.get('enabled')or r.get('trigger')or not r.get('mesh'):continue
 m=meshes[r['mesh']]
 if not m.get('binary'):continue
 # Skip remote/extraneous by worldbounds->UV.
 mn=r['world_bounds_min'];mx=r['world_bounds_max']
 if mx[1]<6.8 or mn[1]>10.5:continue
 v,t=load(m);M=np.array(r['world_matrix']);wv=v@M[:3,:3].T+M[:3,3];uv=np.column_stack((wv[:,0]*s+wv[:,2]*c,wv[:,0]*c-wv[:,2]*s));p=wv[t];ys=p[:,:,1];q=uv[t];qlo=q.min(axis=1);qhi=q.max(axis=1)
 for gid,g in regions.items():
  bb=g['bounds_uv'];mask=(qhi[:,0]>=bb[0])&(qlo[:,0]<=bb[2])&(qhi[:,1]>=bb[1])&(qlo[:,1]<=bb[3])&(ys.max(axis=1)>=6.8)&(ys.min(axis=1)<=10.5);ords=np.flatnonzero(mask)
  if not len(ords):continue
  g['mesh_owners'].append({'path':r['path'],'global_object_id':r['global_object_id'],'mesh':m['id'],'asset':m['asset'],'asset_guid':m['asset_guid'],'binary':m['binary'],'binary_sha256':m['binary_sha256'],'asset_file_sha256':hashlib.sha256(Path(m['asset']).read_bytes()).hexdigest(),'world_matrix':r['world_matrix'],'triangle_ordinals':ords.tolist()})
  # Floor at integer probe rows and lane center; retain exact source face receipts.
  if '_floor_hits'not in g:g['_floor_hits']={}
  for uu in np.linspace(bb[0]+.75,bb[2]-.75,5):
   for vv in np.linspace(bb[1]+.75,bb[3]-.75,5):
    key=(float(uu),float(vv));point=Point(*key)
    for j in ords:
     if max(ys[j])>7.31 or min(ys[j])<6.85:continue
     poly=Polygon(q[j]);
     if poly.area<.005 or not poly.buffer(1e-6).covers(point):continue
     # Full barycentric Y evaluation avoids floor-as-underside.
     A=np.column_stack((q[j],np.ones(3)));a=np.linalg.det(A)
     if abs(a)<1e-9:continue
     bc=np.linalg.solve(A.T,np.array([uu,vv,1.]));y=float(np.dot(bc,ys[j]));cross=np.cross(p[j,1]-p[j,0],p[j,2]-p[j,0]);
     if cross[1]<=0:continue
     hit={'owner':r['path'],'mesh':m['id'],'triangle_ordinal':int(j),'Y':y}
     if key not in g['_floor_hits']or y>g['_floor_hits'][key]['Y']:g['_floor_hits'][key]=hit
  path=LineString(g['probe_path_uv']).buffer(.31);hits=[]
  for j in ords:
   if ys[j].min()>=8 or ys[j].max()<=8:continue
   ints=[]
   for a,b in[(0,1),(1,2),(2,0)]:
    if(ys[j,a]-8)*(ys[j,b]-8)<0:ints.append(q[j,a]+(q[j,b]-q[j,a])*(8-ys[j,a])/(ys[j,b]-ys[j,a]))
   if len(ints)==2 and LineString(ints).intersects(path):hits.append(int(j))
  if hits:g['direct_path_section_blockers'].append({'path':r['path'],'mesh':m['id'],'triangle_ordinals':hits})
for gid,g in regions.items():
 bb=g['bounds_uv'];g['floor_probes']=[{'UV':list(k),**v}for k,v in g.pop('_floor_hits',{}).items()];g.pop('bounds');g['direct_path_probe_is_not_actual_runtime_capsule_test']=True
 print(gid,'owners',len(g['mesh_owners']),'floor',len(g['floor_probes']),'blockers',[(r['path'],len(r['triangle_ordinals']))for r in g['direct_path_section_blockers']])
packet={'schema':'chooguard.four-guide-platform-passage-native-audit.v1','source_export':'.planning/2026-10-09-floor2-guide-restoration/architecture/current/current-scene-structure.json','scene_sha256':x['scene_file_sha256'],'guide_source':'asset-library/research-public/2026-10-09/floor2-architecture-next/guide03-kgnwrite-005-original.jpg','guide_sha256':'1ca0473b6b3cd9f661e208e21d5a9d4e1fe817ff1792b72a7140fb3d66cdb953','frame':'root native UVY: U=X*sin16.2+Z*cos16.2; V=X*cos16.2-Z*sin16.2; Y=worldY','site_accuracy_verified':False,'physical_dimensions_proposed':False,'regions':regions,'mesh_patches':[],'movable_transforms':[]}
(OUT/'four-guide-passages-native-audit.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2))
