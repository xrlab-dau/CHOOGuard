from pathlib import Path
import json,gzip,struct,numpy as np
ROOT=Path('/Users/um-yunsang/CHOOGuard');BASE=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/public/information-desk/constructor';S=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-001'
s=json.loads((S/'CURRENT-2F-MESH-SNAPSHOT.json').read_text());Q=np.array([[-.34290633335,-.93936960061],[-.93936960061,.34290633335]])
axes={'024':np.array([62.9020729,25.2392368]),'022':np.array([54.14528,1.609622]),'032':np.array([83.52171,17.5978985]),'028':np.array([74.76493,-6.03171349])}
rows=[]
for r in s['objects']:
 if not r.get('mesh_index'):continue
 a=np.array(r['bounds_min_world']);b=np.array(r['bounds_max_world'])
 if a[1]>13 or b[1]<7.05:continue
 for c in axes.values():
  d=np.maximum(a[[0,2]]-c,0)+np.minimum(b[[0,2]]-c,0)
  if np.linalg.norm(d)<7:rows.append(r);break
indices={r['mesh_index'] for r in rows};ms={}
with gzip.open(S/'CURRENT-2F-UNIQUE-MESH-GEOMETRY.bin.gz','rb') as f:
 while raw:=f.read(4):
  ix=struct.unpack('<i',raw)[0];nv=struct.unpack('<i',f.read(4))[0];vs=np.frombuffer(f.read(nv*12),dtype='<f4').reshape(-1,3).astype(float);ns=struct.unpack('<i',f.read(4))[0];ts=[]
  for si in range(ns):
   topo,ni=struct.unpack('<ii',f.read(8));inds=np.frombuffer(f.read(ni*4),dtype='<i4')
   if ix in indices and topo==0:ts.append((si,inds.reshape(-1,3)))
  if ix in indices:ms[ix]=(vs,ts)
npzs={};report=[]
for r in rows:
 vs,ts=ms[r['mesh_index']];m=np.array(r['world_matrix_column_major']).reshape((4,4),order='F');vw=(vs@m[:3,:3].T)+m[:3,3]
 for label,c in axes.items():
  vv=np.linalg.norm(vw[:,[0,2]]-c,axis=1)<7
  for si,t in ts:
   mask=vv[t].any(axis=1)&(vw[t,1].max(axis=1)>7.05)&(vw[t,1].min(axis=1)<13)
   a=t[mask]
   if not len(a):continue
   used=np.unique(a);v=vw[used];lookup=np.zeros(len(vs),dtype=int);lookup[used]=np.arange(len(used));tt=lookup[a]
   key=f"{label}_{r['mesh_index']}_{si}"
   npzs[key+'_v']=v;npzs[key+'_t']=tt
   mat=r['materials'][si] if si<len(r['materials']) else {}
   report.append({'key':key,'column':label,'path':r['path'],'mesh':r['mesh_index'],'submesh':si,'triangles':len(a),'bounds':[v.min(axis=0).tolist(),v.max(axis=0).tolist()],'mat':mat})
np.savez_compressed(BASE/'ACTUAL-FOUR-COLUMN-NEIGHBORHOOD-GEOMETRY.npz',**npzs)
(BASE/'ACTUAL-FOUR-COLUMN-NEIGHBORHOOD-GEOMETRY.json').write_text(json.dumps(report,indent=2))
print('rows',len(rows),'parts',len(report))
for a in report:
 if a['mesh'] in [2446,2620]:print(a['key'],a['triangles'],np.round(a['bounds'],3).tolist(),a['mat'].get('asset'))
