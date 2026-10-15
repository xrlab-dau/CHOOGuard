"""Read-only current source geometry contact candidates + source pixel overlays."""
import json,struct,hashlib,numpy as np
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path('/Users/um-yunsang/CHOOGuard');OUT=ROOT/'workers/twin/output/floor1-provided-pdf-20261009';EV=ROOT/'workers/twin/evidence/floor1-provided-pdf-20261009'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def dump(n,x):(OUT/n).write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n')
invpath=ROOT/'.planning/2026-10-09-floor2-completion-pass2/after-facades/current-inventory.json';inv=json.loads(invpath.read_text());meshes={x['id']:x for x in inv['meshes']};fr=next(r for r in inv['renderers']if r['path']=='1층 · 원본 정합/Ground_Floor');F=np.linalg.inv(np.array(fr['world_matrix']));reg=json.loads((OUT/'native-topology-registration.json').read_text());A=np.array(reg['pixel_to_native_UV_affine']);IA=np.linalg.inv(A[:2]);runpath=ROOT/'.planning/2026-10-09-floor2-completion-pass2/floor1-pdf-runtime-initial-physical-failures.json';run=json.loads(runpath.read_text())
paths={'shell':'FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_5','road':'FPSWorld/공식 자료 부산역 주변 공간/ContextPlaza/OfficialStation_-유라시아 도로_0'}
def pttri(p,z):
 a,b,c=z;u=b-a;v=c-a;w=p-a;d00=u@u;d01=u@v;d11=v@v;d20=w@u;d21=w@v;den=d00*d11-d01*d01
 if abs(den)>1e-16:
  s=(d11*d20-d01*d21)/den;t=(d00*d21-d01*d20)/den
  if s>=0 and t>=0 and s+t<=1:q=a+s*u+t*v;return float(np.linalg.norm(p-q))
 return min(float(np.linalg.norm(p-(z[k]+np.clip((p-z[k])@(z[(k+1)%3]-z[k])/max(np.linalg.norm(z[(k+1)%3]-z[k])**2,1e-20),0,1)*(z[(k+1)%3]-z[k]))))for k in range(3))
def segseg(p1,q1,p2,q2):
 d1=q1-p1;d2=q2-p2;r=p1-p2;a=d1@d1;e=d2@d2;f=d2@r;c=d1@r;b=d1@d2;den=a*e-b*b
 if a<=1e-16:return np.linalg.norm(p1-(p2+np.clip(f/max(e,1e-16),0,1)*d2))
 if e<=1e-16:return np.linalg.norm(p2-(p1+np.clip(-c/a,0,1)*d1))
 s=np.clip((b*f-c*e)/den,0,1)if den>1e-16 else 0;t=(b*s+f)/e
 if t<0:t=0;s=np.clip(-c/a,0,1)
 elif t>1:t=1;s=np.clip((b-c)/a,0,1)
 return float(np.linalg.norm((p1+s*d1)-(p2+t*d2)))
def segtri(p,q,z):
 n=np.cross(z[1]-z[0],z[2]-z[0]);den=n@(q-p)
 if abs(den)>1e-16:
  t=n@(z[0]-p)/den
  if 0<=t<=1 and pttri(p+t*(q-p),z)<1e-8:return 0.
 return min(pttri(p,z),pttri(q,z),*[segseg(p,q,z[k],z[(k+1)%3])for k in range(3)])
owners={};targets=[];trilist=[]
for kind,path in paths.items():
 r=next(x for x in inv['renderers']if x['path']==path);m=meshes[r['mesh']];raw=(ROOT/m['binary']).read_bytes();assert sha(ROOT/m['binary'])==m['binary_sha256'];nv,ni,nn=struct.unpack_from('<iii',raw);v=np.frombuffer(raw,'<f4',nv*3,12).reshape(nv,3).astype(float);indices=np.frombuffer(raw,'<i4',ni,12+nv*12).reshape(-1,3);w=(np.c_[v,np.ones(len(v))]@np.array(r['world_matrix']).T)[:,:3];uvy=(np.c_[w,np.ones(len(w))]@F.T)[:,[2,0,1]];tri=w[indices];lo=tri.min(1);hi=tri.max(1);owners[kind]={k:r[k]for k in ['path','global_object_id','mesh','world_matrix']}|{k:m[k]for k in ['asset','asset_guid','asset_sha256','binary','binary_sha256']};selected={}
 for route in run['actual_bidirectional_source_crossings']:
  matching=[(k,s)for k,s in enumerate(route['samples'])if path in s.get('capsule_contacts',[])]
  if not matching:continue
  # Keep first original contact in each directed route; do not copy whole runtime proof.
  k,s=matching[0];p=np.array(s['actual']);a=p+[0,.28,0];b=p+[0,1.72-.28,0];limit=.325;sel=np.where((lo[:,0]<=p[0]+limit)&(hi[:,0]>=p[0]-limit)&(lo[:,2]<=p[2]+limit)&(hi[:,2]>=p[2]-limit)&(lo[:,1]<=b[1]+limit)&(hi[:,1]>=a[1]-limit))[0];hits=[]
  for idx in sel:
   z=tri[idx];distance=segtri(a,b,z)
   if distance>limit:continue
   n=np.cross(z[1]-z[0],z[2]-z[0]);n=n/max(np.linalg.norm(n),1e-20);px=(uvy[indices[idx],:2]-A[2])@IA
   row={'current_triangle_index':int(idx),'current_vertex_indices':indices[idx].tolist(),'world_vertices':z.tolist(),'native_UVY_vertices':uvy[indices[idx]].tolist(),'source_pixel_projection_reference_only':px.tolist(),'world_bounds':[lo[idx].tolist(),hi[idx].tolist()],'normal_world':n.tolist(),'capsule_segment_triangle_distance_reference_only':float(distance),'horizontal_surface':bool(abs(n[1])>.995),'original_archive_face_matches':[]}
   selected[int(idx)]=row;hits.append(int(idx))
  targets.append({'kind':kind,'route':route['route'],'reverse':route['reverse'],'source_pixels':route['source_pixels'],'runtime_sample_index':k,'runtime_target_world':s['target'],'runtime_actual_world':s['actual'],'runtime_horizontal_error':s['horizontal_error'],'candidate_current_triangles':hits,'physics_reexecuted':False})
 owners[kind]['candidate_triangles']=list(selected.values());trilist.extend([{'owner':kind,**r}for r in selected.values()])
dump('original-source-contact-candidates.json',{'schema':'chooguard.original-source-contact-candidates.v1','runtime_proof':str(runpath.relative_to(ROOT)),'runtime_proof_sha256':sha(runpath),'runtime_saved_scene_sha256':run['saved_scene_sha256'],'geometry_inventory':str(invpath.relative_to(ROOT)),'geometry_inventory_sha256':sha(invpath),'geometry_scene_epoch_sha256':inv['scene_file_sha256'],'owners':owners,'targets':targets,'physical_capsule':{'radius':.28,'height':1.72,'candidate_tolerance':.045,'skin_not_reproduced':True},'site_accuracy_verified':False,'production_mutated':False})
print(json.dumps({'targets':len(targets),'unique_triangle_candidates':{k:len(v['candidate_triangles'])for k,v in owners.items()},'owner_ids':{k:v['global_object_id']for k,v in owners.items()}}))
