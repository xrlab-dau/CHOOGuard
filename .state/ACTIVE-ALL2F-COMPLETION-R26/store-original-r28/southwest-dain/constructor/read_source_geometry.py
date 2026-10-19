from pathlib import Path
import numpy as np,struct,gzip,json,math
R=Path('/Users/um-yunsang/CHOOGuard');W=Path(__file__).resolve().parent;P=R/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-007/CURRENT-2F-MESH-SNAPSHOT.json';d=json.loads(P.read_text());native=next(o for o in d['objects']if o['path'].endswith('OfficialStation_-부산역_0'));glass=[o for o in d['objects']if '/R25_flowers_'in o['path']and o['path'].endswith('/FridgeDoor/FridgeGlass')];sel={native['mesh_index']}|{o['mesh_index']for o in glass};dat={}
def exact(n):
 z=f.read(n)
 if len(z)!=n:raise EOFError(n)
 return z
with gzip.open(R/d['mesh_binary'],'rb') as f:
 while True:
  h=f.read(8)
  if not h:break
  idx,nv=struct.unpack('<ii',h);raw=exact(nv*12);vs=np.frombuffer(raw,'<f4').reshape(-1,3).copy()if idx in sel else None
  ns=struct.unpack('<i',exact(4))[0];subs=[]
  for j in range(ns):
   top,ni=struct.unpack('<ii',exact(8));rawi=exact(ni*4)
   if idx in sel:subs.append(np.frombuffer(rawi,'<i4').copy())
  if idx in sel:dat[idx]=(vs,subs)
  if idx>=max(sel):break
Q=np.array([[-.34290633335,0,-.93936960061],[0,1,0],[.93936960061,0,-.34290633335]])
def pts(o):
 v=dat[o['mesh_index']][0];M=np.array(o['world_matrix_column_major']).reshape(4,4,order='F');world=np.c_[v,np.ones(len(v))]@M.T;return world[:,:3]@Q.T
out=[]
for o in glass:
 q=pts(o);out.append({'object_id':o['object_id'],'path':o['path'],'source_corner_vertices_qx_worldY_minusqy':q.tolist(),'actual_plan_bounds_min':q[:,[0,2]].min(0).tolist(),'actual_plan_bounds_max':q[:,[0,2]].max(0).tolist(),'source_model_door_facing':'negative packetY, opposite observed public front +packetY','preserve_original_mesh':True})
(W/'ACTUAL-COOLER-GLASS-SOURCE-ORIENTATION-R29.json').write_text(json.dumps({'source_snapshot':str(P),'actual_geometry_not_Scene_QA':True,'glass':out},indent=2));print('glass',out)
v=pts(native);faces=[]
for si,ind in enumerate(dat[native['mesh_index']][1]):
 if len(ind)%3:continue
 tt=ind.reshape(-1,3);tri=v[tt];mn=tri.min(1);mx=tri.max(1);keep=(mx[:,0]>-25)&(mn[:,0]<-8)&(mx[:,2]>-38)&(mn[:,2]<-26)&(mx[:,1]>7)&(mn[:,1]<10.5);tri=tri[keep]
 if not len(tri):continue
 norms=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]);areas=np.linalg.norm(norms,axis=1)/2;norms/=np.maximum(np.linalg.norm(norms,axis=1,keepdims=True),1e-20);centers=tri.mean(1)
 for i in range(len(tri)):
  if areas[i]<.01:continue
  N=norms[i];ax=int(np.argmax(np.abs(N)));value=centers[i,ax];key=(ax,round(float(value),1),si,1 if N[ax]>0 else -1)
  faces.append((key,float(areas[i]),tri[i]))
clusters={}
for key,area,tri in faces:
 if key not in clusters:clusters[key]={'axis':key[0],'datum_round':key[1],'material_index':key[2],'normal_sign':key[3],'area':0,'min':tri.min(0),'max':tri.max(0)}
 c=clusters[key];c['area']+=area;c['min']=np.minimum(c['min'],tri.min(0));c['max']=np.maximum(c['max'],tri.max(0))
result=[]
for c in sorted(clusters.values(),key=lambda x:-x['area']):
 if c['area']<.15:continue
 c['min']=c['min'].tolist();c['max']=c['max'].tolist();c['material']=native['materials'][c['material_index']] if c['material_index']<len(native['materials'])else None;result.append(c)
(W/'NATIVE0-ACTUAL-MATCHING-LEVEL-FACE-DIRECTIONS-R29.json').write_text(json.dumps({'source_snapshot':str(P),'source_native_id':native['object_id'],'coordinate':'qx, worldY, -actualqy','clusters':result},ensure_ascii=False,indent=2));print('faceclusters',len(result));print(json.dumps(result[:16],ensure_ascii=False))
