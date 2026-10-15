from pathlib import Path
import json,hashlib,struct,copy,numpy as np
from scipy.spatial import cKDTree
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-whole-layout-20261009/99percent-matchinghall-20261009');h=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();hb=lambda b:hashlib.sha256(b).hexdigest()
s=Path('workers/twin/evidence/floor2-whole-layout-20261009/seal_native_stair_channels.py').read_text();exec(s[s.index('def ints'):s.index('entries={')])

# Export v1 stores color float values but not format; obtain exact format from pinned saved native asset channel metadata.
base_complete=complete
def complete(d):
 if not len(d['colors']):return base_complete(d)
 allidx=np.concatenate([s['indices']for s in d['submeshes']]).astype('<i4');v=d['vertices'];n=d['normals'];t=d['tangents'];u=d['uv'][0]['data'][:,:2]
 mesh=hb(ints(len(v),len(allidx),len(n))+v.astype('<f4').tobytes()+allidx.tobytes()+n.astype('<f4').tobytes());channel=hb(ds(mesh)+ints(len(u),len(t))+u.astype('<f4').tobytes()+t.astype('<f4').tobytes());body=ds(channel)+ints(len(d['submeshes']))
 for sub in d['submeshes']:body+=ints(sub['topology'],len(sub['indices']))+sub['indices'].astype('<i4').tobytes()
 body+=b'\1'+ints(0)+ints(len(d['colors']))+d['colors'].astype('<f4').tobytes()
 for uv in d['uv']:body+=ints(len(uv['data']),uv['dimension'])+uv['data'].astype('<f4').tobytes()
 return hb(body)

MP=Path('.planning/2026-10-09-interior-99-reconstruction/matchinghall-full-channels/metadata.json');meta=json.loads(MP.read_text());entries={e['role']:e for e in meta['entries']};data={k:read(e)for k,e in entries.items()};assert all(complete(d)==entries[k]['complete_channel_digest']for k,d in data.items())
req=json.loads((O/'current-channel-export-request.json').read_text());generated=json.loads((O/'current-generated-contributors.json').read_text());assert meta['scene_sha256']==req['scene_sha256'] and h(generated['source_spec'])==generated['source_spec_sha256'];lookup={r['owner']['path'].split('/')[-1]:r for r in req['rows']if r['owner']['path'].startswith('실내 트윈 마감/2F 편의/')};lookup['Kit_Colliders']=req['collider_rows'][0]
# Current source geometry underwent declared prior omissions; match against actual complete channels, never force an ordinal rebase.
all_matches=[];missing=[];ambiguous=[];meshsel={};glazing=next(r for r in req['rows']if r['owner']['path'].endswith('/Observed office front glazing'))
prepared={}
for k,d in data.items():
 actual=np.concatenate([sub['indices']for sub in d['submeshes']]).reshape(-1,3);v=d['vertices'][actual];prepared[k]=(actual,v,cKDTree(v.mean(1)),d['normals'][actual],d['uv'][0]['data'][actual,:2])
def find(k,ep,en,eu):
 ix,p,tree,n,u=prepared[k];cands=tree.query_ball_point(ep.mean(0),1e-4);matches=[]
 for a in cands:
  for rot in range(3):
   pe=float(np.max(abs(np.roll(p[a],rot,axis=0)-ep)));ne=float(np.max(abs(np.roll(n[a],rot,axis=0)-en)));ue=float(np.max(abs(np.roll(u[a],rot,axis=0)-eu)))
   if pe<1e-5 and ne<1e-6 and ue<1e-6:matches.append({'triangle_ordinal':int(a),'cyclic_offset':rot,'position_error':pe,'normal_error':ne,'uv0_error':ue})
 return matches
for element in generated['elements']:
 for batch in element['batches']:
  row=lookup[batch['name']];k=row['mesh']['id'];p=np.array(batch['vertices_local_xyz'],dtype='<f4');n=np.array(batch['normals'],dtype='<f4');u=np.array(batch['uv0'],dtype='<f4');tr=np.array(batch['indices']).reshape(-1,3)
  for bti,ti in enumerate(tr):
   m=find(k,p[ti],n[ti],u[ti]);role='shared_current_batch'
   if not m and batch['name']=='Kit_RoomWall_벽_내장':
    m=find(glazing['mesh']['id'],p[ti],n[ti],u[ti]);role='preserved_photo_supported_glazing'
   if not m:missing.append({'element':element['id'],'batch':batch['name'],'generated_triangle':bti,'position':p[ti].tolist()});continue
   if len(m)!=1:ambiguous.append({'element':element['id'],'batch':batch['name'],'generated_triangle':bti,'candidates':m});continue
   actualkey=glazing['mesh']['id']if role=='preserved_photo_supported_glazing'else k;hit=m[0];record={'element':element['id'],'batch':batch['name'],'generated_triangle':bti,'role':actualkey,'disposition':role,**hit};all_matches.append(record);meshsel.setdefault(actualkey,{}).setdefault(element['id'],[]).append(hit['triangle_ordinal'])
packet=[]
for k,selected in meshsel.items():
 d=data[k];actual=prepared[k][0];selectedall=sorted(set(i for ids in selected.values()for i in ids));assert sum(len(x)for x in selected.values())==len(selectedall),(k,'element selected triangle overlap')
 first=sorted(set(i for name,ids in selected.items()if name.startswith('RM-2F-B-')for i in ids));keep=sorted(set(range(len(actual)))-set(first));shared=sorted(set(actual[first].ravel())&set(actual[keep].ravel()))if first else[]
 row=next(r for r in req['rows']+req['collider_rows']if r['mesh']['id']==k);packet.append({'role':k,'current_owner':row,'full_channel_export':entries[k],'per_element_selected_triangle_ordinals':selected,'first_scope_selected_triangle_ordinals':first,'first_scope_selected_count':len(first),'first_scope_retained_count':len(keep),'first_scope_shared_selected_retained_vertices':shared,'first_scope_selected_indices_sha256':hb(actual[first].astype('<i4').tobytes()),'first_scope_retained_indices_sha256':hb(actual[keep].astype('<i4').tobytes()),'photo_glazing_preserve_for_source_reprojection':k==glazing['mesh']['id']})
report={'schema':'chooguard.matchinghall-actual-source-partition.v1','scene_sha256':meta['scene_sha256'],'metadata':str(MP),'metadata_sha256':h(MP),'request_sha256':h(O/'current-channel-export-request.json'),'generated_contributors_sha256':h(O/'current-generated-contributors.json'),'all15_source_complete_digests_reproduced':len(entries)==15,'actual_mesh_count':len(entries),'status':'PASS'if not missing and not ambiguous else'HOLD','matching_rule':'Unique oriented P/N/UV0 triangle match against actual current complete channels; cyclic winding rotations only, no reverse winding, no forced ordinal rebases. Office eight pre-separated glass faces matched independently.','matches':all_matches,'missing':missing,'ambiguous':ambiguous,'meshes':packet,'first_scope_existing_elements':['RM-2F-B-역무실','RM-2F-B-수유방'],'source_wc_envelopes_captured_but_not_replaced':['WC-2F-B1-M','WC-2F-B1-F','WC-2F-B2-A'],'replacement_apply_ready':False,'remaining_install_requirements':['Root evidence sufficiency review against new engineering source','Native opaque-wall exact partition and physics checks','Observed office glass correspondence/replay to exact source facade','Exact model floor/capsule route preflight and actual saved verification']}
(O/'exact-generated-contributor-partition.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print('STATUS',report['status'],'sourceMeshes',len(entries),'matches',len(all_matches),'missing',len(missing),'ambiguous',len(ambiguous));print([(p['role'],p['first_scope_selected_count'],p['first_scope_retained_count'],len(p['first_scope_shared_selected_retained_vertices']))for p in packet])
