"""Compute actual current geometry envelope as construction input, no Unity calls."""
from pathlib import Path
import json,gzip,struct,numpy as np,datetime
ROOT=Path('/Users/um-yunsang/CHOOGuard');P=Path(__file__).resolve().parent
s=json.loads((ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-001/CURRENT-2F-MESH-SNAPSHOT.json').read_text())
Q=np.array([[-.34290633335024717,-.9393696006089878],[-.9393696006089878,.3429063333502471]])
lo=np.array([37.10,-36.13,7.001]);hi=np.array([39.10,-8.89,9.62]);candidates=[]
for o in s['objects']:
 if 'mesh_index' not in o:continue
 b0=np.array(o['bounds_min_world']);b1=np.array(o['bounds_max_world'])
 if b0[1]>=hi[2] or b1[1]<=lo[2]:continue
 p=np.array([[x,z]for x in[b0[0],b1[0]]for z in[b0[2],b1[2]]])@Q.T
 if (p.max(0)<lo[:2]).any()or(p.min(0)>hi[:2]).any():continue
 candidates.append(o)
wanted={o['mesh_index']for o in candidates};meshes={}
with gzip.open(ROOT/s['mesh_binary'],'rb')as f:
 while raw:=f.read(4):
  ix=struct.unpack('<i',raw)[0];nv=struct.unpack('<i',f.read(4))[0];rawv=f.read(nv*12);ns=struct.unpack('<i',f.read(4))[0];tt=[]
  for j in range(ns):
   topo,ni=struct.unpack('<ii',f.read(8));r=f.read(ni*4)
   if ix in wanted and topo==0:tt.append(np.frombuffer(r,'<i4').reshape(-1,3).copy())
  if ix in wanted:meshes[ix]=(np.frombuffer(rawv,'<f4').reshape(-1,3).astype(float),np.concatenate(tt)if tt else np.empty((0,3),int))
def clip(tri):
 p=list(tri)
 for axis in range(3):
  for v,above in [(lo[axis],True),(hi[axis],False)]:
   out=[]
   for i,a in enumerate(p):
    b=p[(i+1)%len(p)];aa=a[axis]>=v if above else a[axis]<=v;bb=b[axis]>=v if above else b[axis]<=v
    if aa:out.append(a)
    if aa!=bb:out.append(a+(b-a)*(v-a[axis])/(b[axis]-a[axis]))
   p=out
   if not p:return False
 return len(p)>=3 and any(np.linalg.norm(np.cross(p[i]-p[0],p[i+1]-p[0]))>1e-9 for i in range(1,len(p)-1))
rows=[];total=0
for o in candidates:
 v,t=meshes[o['mesh_index']];M=np.array(o['world_matrix_column_major']).reshape(4,4,order='F');v=v@M[:3,:3].T+M[:3,3];q=np.c_[v[:,[0,2]]@Q.T,v[:,1]];a=q[t]
 sel=(a.min(1)<=hi).all(1)&(a.max(1)>=lo).all(1);a=a[sel];n=sum(clip(tri)for tri in a);total+=n
 rows.append({'object_id':o['object_id'],'path':o['path'],'potentially_crossing_triangles':len(a),'actual_clipped_faces_in_equipment_envelope':n})
d={'status':'CURRENT_MESH_CONSTRUCTION_ENVELOPE','utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'actual_geometry_source':s['mesh_binary'],'source_scene_sha':s['scene_sha256_after'],'source_editing_state_dirty':s['scene_dirty_after'],'envelope_actual_qx_qy_y':[lo.tolist(),hi.tolist()],'floor_contact_y':7.0,'native_or_existing_equipment_faces_inside':total,'inventory_rows':rows,'purpose':'Actual equipment installation empty-envelope input; not a Scene QA or surveyed position assertion','Native_Scene_Assets_changed':False}
(P/'CURRENT-ADDITIONAL-EQUIPMENT-ENVELOPE-002.json').write_text(json.dumps(d,ensure_ascii=False,indent=2));print({'candidates':len(candidates),'clipped_faces':total,'record':str(P/'CURRENT-ADDITIONAL-EQUIPMENT-ENVELOPE-002.json')})
if total:raise SystemExit('Existing physical faces occupy proposed envelope; refine own construction only')
