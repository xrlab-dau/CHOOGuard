"""Read-only BOH topology/profile/channel and generic collider validation."""
from pathlib import Path
import json,hashlib,sys,numpy as np
sys.path.insert(0,'workers/twin')
from official_plan_compare import readbin
EV=Path('workers/twin/evidence/floor2-architecture-next-20261009/review-floor1-stage');P=Path('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-packet.json');B=Path('.planning/2026-10-08-interior-runtime-completion/architecture/after-busanok-finishes/current-scene-structure.json')
def sha(x):return hashlib.sha256(Path(x).read_bytes()).hexdigest()
p=json.loads(P.read_text());s=json.loads(B.read_text());mm={m['asset']:m for m in s['meshes']};owners={x['global_object_id']:x for x in s['renderers']+s['colliders']};checks=[];details=[]
def chk(id,ok,data):checks.append({'id':id,'passed':bool(ok),'details':data})
for q in p['mesh_patches']:
 if not ('boh_evidence' in q or 'collider_owners' in q):continue
 m=mm[q['source_asset']];v,t=readbin(m);ids=np.array(q['vertex_indices'],int);expect=np.array(q['expected_positions']).reshape(-1,3);replacement=np.array(q['replacement_positions']).reshape(-1,3)
 chk(q['source_asset']+'_bytes',sha(q['source_asset'])==q['source_file_sha256'] and m['binary_sha256']==q['source_binary_sha256'] and np.array_equal(v[ids],expect),len(ids))
 for o in q['owners']+q.get('collider_owners',[]):
  actual=owners[o['global_object_id']];chk(o['path']+'_owner',actual['path']==o['path'] and np.array_equal(np.array(actual['world_matrix']),np.array(o['world_matrix'])),o['global_object_id'])
 if q.get('boh_evidence'):chk(q['source_asset']+'_BOH_evidence',sha(q['boh_evidence'])==q['boh_evidence_sha256'],q['boh_evidence'])
 after=v.astype(float).copy();after[ids]=replacement
 if 'removed_triangle_ordinals' in q:
  removed=np.array(q['removed_triangle_ordinals'],int);keep=np.delete(t,removed,axis=0);added=np.array(q.get('added_triangles',[]),int).reshape(-1,3);affected=np.unique(t[removed]);preserved_vertices=np.unique(keep)
  chk(q['source_asset']+'_exclusive_reuse',not np.intersect1d(affected,preserved_vertices).size and not np.intersect1d(ids,preserved_vertices).size,{'removed':len(removed),'retained':len(keep),'added':len(added)})
  chk(q['source_asset']+'_preserve_positions',np.array_equal(after[preserved_vertices],v[preserved_vertices]),'All retained face positions are exact source values')
  chk(q['source_asset']+'_single_material',m['submeshes']==1,'Installer requires single triangle submesh; retained sequence remains exact')
  attrs=q.get('attribute_overrides',[])
  chk(q['source_asset']+'_attributes_bounded',{a['index'] for a in attrs}<=set(ids) and (not len(added) or {a['index'] for a in attrs}==set(ids)),len(attrs))
  if len(added):
   byindex={a['index']:a for a in attrs};tris=after[added];cross=np.cross(tris[:,1]-tris[:,0],tris[:,2]-tris[:,0]);areas=np.linalg.norm(cross,axis=1);norm=cross/areas[:,None];centre=replacement.mean(0);outward=np.sum(norm*(tris.mean(1)-centre),axis=1)
   chk(q['source_asset']+'_new_closed_box',len(added)==12 and len(np.unique(np.round(replacement,7),axis=0))==8 and (areas>1e-8).all() and (outward>0).all(),{'unique_corners':len(np.unique(np.round(replacement,7),axis=0)),'min_outward':float(outward.min()),'min_double_area':float(areas.min())})
   attr_errors=[]
   for k,tri in enumerate(added):
    for idx in tri:
     a=byindex[int(idx)];n=np.array(a['normal']);tg=np.array(a['tangent']);uv=np.array(a['uv0']);attr_errors.append(max(abs(np.linalg.norm(n)-1),abs(np.linalg.norm(tg[:3])-1),abs(np.dot(n,tg[:3])),np.linalg.norm(n-norm[k])))
   chk(q['source_asset']+'_normal_tangent_winding',max(attr_errors)<1e-9 and all(abs(a['tangent'][3])==1 for a in attrs),'Unit orthogonal normals/tangents match actual triangle winding')
   uv_errors=[]
   for k in range(0,len(added),2):
    indices=[int(added[k][0]),int(added[k][1]),int(added[k][2]),int(added[k+1][2])];xyz=after[indices];uv=np.array([byindex[i]['uv0'] for i in indices]);uv_errors+=[abs(np.linalg.norm(xyz[1]-xyz[0])-np.linalg.norm(uv[1]-uv[0])),abs(np.linalg.norm(xyz[3]-xyz[0])-np.linalg.norm(uv[3]-uv[0]))]
   chk(q['source_asset']+'_UV_edge_scale',max(uv_errors)<1e-9,{'max_error':float(max(uv_errors)),'claim':'New plain box UV scale follows geometry edges; old7.75length texture/profile not compressed'})
   details.append({'asset':q['source_asset'],'removed_faces':len(removed),'added_faces':len(added),'reused_vertices':ids.tolist(),'box_dimensions':sorted([float(np.linalg.norm(replacement[1]-replacement[0])),float(np.linalg.norm(replacement[3]-replacement[0])),float(np.linalg.norm(replacement[9]-replacement[8]))])})
 else:
  selected=np.array(q['selected_triangles'],int);mask=np.zeros(len(v),bool);mask[ids]=True;chk(q['source_asset']+'_index_closure',np.array_equal(np.flatnonzero(mask[t].any(1)),selected),'Collider selected indices affect no other triangles')
  # Coincident points at fixed preceding BOH endpoint may be shared geometrically with retained faces; only moved endpoints can tear.
  bypos={}
  for i,pt in enumerate(v):bypos.setdefault(tuple(np.round(pt,5)),[]).append(i)
  delta=after-v;torn=[vv for vv in bypos.values() if len(vv)>1 and np.linalg.norm(delta[vv]-delta[vv[0]],axis=1).max()>2e-5]
  chk(q['source_asset']+'_coordinate_weld_closure',not torn,{'torn_groups':len(torn)})
  if 'boh_evidence' in q and 'proposed_segment_UV' in json.loads(Path(q['boh_evidence']).read_text()):
   recipe=json.loads(Path(q['boh_evidence']).read_text());a,b=np.array(recipe['proposed_segment_UV']);uv=np.c_[replacement[:,2],replacement[:,0]];ab=b-a;f=np.clip((uv-a)@ab/np.dot(ab,ab),0,1);err=np.linalg.norm(uv-(a+f[:,None]*ab),axis=1)
   chk(q['source_asset']+'_new_collider_edge',err.max()<1e-5 and set(np.round(replacement[:,1],3))=={0.,6.7},{'max_distance_to_new_edge':float(err.max()),'proposed_edge':recipe['proposed_segment_UV']})
candidate=json.loads((EV/'generic-collider-candidates.json').read_text())
for c in candidate['mesh_patch_candidates']:
 staged=next(q for q in p['mesh_patches'] if q['source_asset']==c['source_asset']);fields=['vertex_indices','expected_positions','replacement_positions','selected_triangles']
 chk(c['source_asset']+'_candidate_exact_map',all(np.array_equal(np.array(c[k]),np.array(staged[k])) for k in fields),'Staged generic collider mapping equals independently bounded source candidate')
chk('protected_Room_owner_absent',all(not any('899526584' in o['global_object_id'] for o in q['owners']+q.get('collider_owners',[])) for q in p['mesh_patches']),'Original public Room/core faces remain unchanged')
r={'schema':'chooguard.floor1-BOH-topology-independent-review.v1','packet_sha256':sha(P),'checks':checks,'all_passed':all(x['passed'] for x in checks),'profile_details':details,'packet_counts':{'meshes':len(p['mesh_patches']),'renderer_owners':sum(len(x['owners']) for x in p['mesh_patches']),'selected_vertices':sum(len(x['vertex_indices']) for x in p['mesh_patches']),'motions':len(p['movable_transforms'])},'candidate_evidence_sha256':sha(EV/'generic-collider-candidates.json'),'production_writes':0}
(EV/'boh-topology-review.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'checks':len(checks),'failed':[x for x in checks if not x['passed']],'counts':r['packet_counts'],'candidate_sha256':r['candidate_evidence_sha256']},ensure_ascii=False))
