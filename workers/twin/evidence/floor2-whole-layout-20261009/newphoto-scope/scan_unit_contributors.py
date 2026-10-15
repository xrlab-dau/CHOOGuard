from pathlib import Path
import json,struct,hashlib
import numpy as np
E=Path(__file__).resolve().parent
P=Path('.planning/2026-10-09-floor2-completion-pass2/current/current-inventory.json');S=json.loads(P.read_text());MM={m['id']:m for m in S['meshes']}
F=np.array([[.9602937,0,.278991133],[0,1,0],[-.278991133,0,.9602937]]);I=np.linalg.inv(F)
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(m):
 b=Path(m['binary']).read_bytes();assert sha(m['binary'])==m['binary_sha256'];nv,ni,nn=struct.unpack_from('<iii',b)
 return np.frombuffer(b,'<f4',nv*3,12).reshape(nv,3),np.frombuffer(b,'<i4',ni,12+nv*12).reshape(-1,3)
rows=[]
for r in S['renderers']:
 if not r['path'].startswith(('맞이방 · 원본 정합/','실내 트윈 마감/2F 본관/')):continue
 if any(s in r['path']for s in ['Floor','Bench','Standee','OpeningGuard','Box frame','Ticket','Platform']):continue
 m=MM.get(r.get('mesh'))
 if not m or not m.get('binary'):continue
 v,t=read(m);M=np.array(r['world_matrix']);w=v@M[:3,:3].T+M[:3,3];q=(w@I.T)[:,[2,0,1]];tr=q[t];lo=tr.min(1);hi=tr.max(1)
 inside=np.all(lo>[-40.5,-3.9,6.95],1)&np.all(hi<[-32.5,4,14.1],1)
 ix=np.flatnonzero(inside)
 if not len(ix):continue
 sv=np.unique(t[ix]);outside=np.unique(t[~inside]);shared=np.intersect1d(sv,outside)
 rows.append({'owner':r,'mesh':m,'source_live_file_sha256':sha(m['asset']),'whole_mesh_selected':len(ix)==len(t),'selected_triangle_ordinals':ix.tolist(),'selected_vertex_indices':sv.tolist(),'shared_selected_unselected_vertices':shared.tolist(),'selected_native_UVY_bounds':[q[sv].min(0).tolist(),q[sv].max(0).tolist()]})
 print(r['global_object_id'].split('-')[-2],m['id'],len(ix),len(t),'sharedverts',len(shared),r['path'])
(E/'namcheon-current-contributors.json').write_text(json.dumps({'inventory_sha256':sha(P),'scene_epoch':S['scene_file_sha256'],'classification':'current recipe envelope; exact geometric ownership, no field dimensions accepted','rows':rows},ensure_ascii=False,indent=2)+'\n')
