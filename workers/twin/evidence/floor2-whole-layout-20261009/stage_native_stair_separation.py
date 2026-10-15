from pathlib import Path
import json,hashlib,struct,re,yaml,collections,itertools
import numpy as np
from scipy.spatial import cKDTree

E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-whole-layout-20261009')
IPath=Path('.planning/2026-10-09-floor2-completion-pass2/current/current-inventory.json')
S=json.loads(IPath.read_text());MM={m['id']:m for m in S['meshes']}
APath=O/'exact-native-assemblies.json';A=json.loads(APath.read_text());a=next(r for r in A['assemblies']if r['id']=='upper-fixed-stair')
F=np.array(A['native_frame']);Finv=np.linalg.inv(F)
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();hb=lambda b:hashlib.sha256(b).hexdigest()
def read(m):
 b=Path(m['binary']).read_bytes();assert hb(b)==m['binary_sha256'];nv,ni,nn=struct.unpack_from('<iii',b)
 return np.frombuffer(b,'<f4',nv*3,12).reshape(nv,3),np.frombuffer(b,'<i4',ni,12+nv*12).reshape(-1,3),np.frombuffer(b,'<f4',nn*3,12+nv*12+ni*4).reshape(nn,3)
def mesh_yaml(m):
 assert sha(m['asset'])==m['asset_sha256'];t=Path(m['asset']).read_text();vd=yaml.safe_load(re.search(r'^  m_VertexData:\n.*?(?=^  m_CompressedMesh:)',t,re.M|re.S)[0])['m_VertexData'];sm=yaml.safe_load(re.search(r'^  m_SubMeshes:\n.*?(?=^  m_Shapes:)',t,re.M|re.S)[0])['m_SubMeshes'];fmt=int(re.search(r'^  m_IndexFormat: (\d+)$',t,re.M)[1]);ib=bytes.fromhex(re.search(r'^  m_IndexBuffer: (.*)$',t,re.M)[1]);idx=np.frombuffer(ib,'<u4'if fmt else'<u2');dt=4 if fmt else 2;flat=np.concatenate([idx[r['firstByte']//dt:r['firstByte']//dt+r['indexCount']].astype(np.int64)+r['baseVertex']for r in sm]).reshape(-1,3);raw=bytes.fromhex(vd['_typelessdata']);stride=len(raw)//vd['m_VertexCount'];v=np.ndarray((vd['m_VertexCount'],3),'<f4',raw,offset=vd['m_Channels'][0]['offset'],strides=(stride,4)).copy()
 extras={}
 for key,nextkey in [('m_Shapes','m_BindPose'),('m_BindPose','m_BoneNameHashes'),('m_VariableBoneCountWeights','m_MeshCompression'),('m_CompressedMesh','m_LocalAABB')]:
  x=re.search(r'^  '+key+r':.*?(?=^  '+nextkey+r':)',t,re.M|re.S);extras[key]=yaml.safe_load(x[0])[key]if x else None
 return {'v':v,'t':flat,'vd':vd,'raw':raw,'stride':stride,'index_format':fmt,'index_bytes':ib,'submeshes':sm,'extras':extras}
def native(v,r):
 M=np.array(r['world_matrix']);return((v@M[:3,:3].T+M[:3,3])@Finv.T)[:,[2,0,1]]
matched={m['current_owner_global_id']for r in a['source_face_correspondence']for m in r['matches']};assert len(matched)==1
owner=next(r['owner']for r in a['current_owner_receipts']if r['owner']['global_object_id']in matched);m=MM[owner['mesh']];q=mesh_yaml(m);v,t,n=read(m);assert np.array_equal(q['v'],v)and np.array_equal(q['t'],t)
sel=sorted({m['current_triangle']for r in a['source_face_correspondence']for m in r['matches']});assert len(sel)==1306
ids=np.unique(t[sel]);nativev=native(v,owner);native_tri=nativev[t[sel]];lo=native_tri.min((0,1));hi=native_tri.max((0,1))
assert q['stride']==48;assert[(i,c['stream'],c['offset'],c['format'],c['dimension'])for i,c in enumerate(q['vd']['m_Channels'])if c['dimension']]==[(0,0,0,0,3),(1,0,12,0,3),(2,0,24,0,4),(4,0,40,0,2)]
# Every submesh keeps its exact material-slot index and the original index order.
submesh_partition=[];start=0;keepfaces=[]
for si,s in enumerate(q['submeshes']):
 assert s['topology']==0 and s['indexCount']%3==0
 ords=np.arange(start,start+s['indexCount']//3);chosen=np.isin(ords,sel);keep=ords[~chosen];take=ords[chosen];keepfaces.extend(keep.tolist())
 submesh_partition.append({'submesh':si,'source_descriptor':s,'source_global_triangle_start':start,'selected_global_triangle_ordinals':take.tolist(),'selected_submesh_triangle_ordinals':(take-start).tolist(),'selected_indices':t[take].tolist(),'retained_index_sequence_sha256':hb(t[keep].astype('<u4').tobytes()),'selected_index_sequence_sha256':hb(t[take].astype('<u4').tobytes()),'selected_triangle_count':len(take),'retained_triangle_count':len(keep),'base_vertex_contract':'Use original descriptors/baseVertex with original index-buffer values, or emit logical indices with baseVertex=0; do not mix the two.'});start+=len(ords)
# Exact same indexed triangle map for every existing collider; report any duplicate owners explicitly.
perms=np.array(list(itertools.permutations(range(3))));collider_matches=[];candidate_receipts=[]
for c in S['colliders']:
 if not c.get('mesh')or not MM[c['mesh']].get('binary')or not c.get('active')or not c.get('enabled'):continue
 cm=MM[c['mesh']];cv,ct,cn=read(cm);cq=native(cv,c);ctr=cq[ct];mn=ctr.min(1);mx=ctr.max(1);ci=np.flatnonzero(np.all(mx>=lo-.001,1)&np.all(mn<=hi+.001,1))
 if not len(ci):continue
 tree=cKDTree(ctr[ci].mean(1));matches=[]
 for sourceord,tr in zip(sel,native_tri):
  for localix in tree.query_ball_point(tr.mean(0),.0005):
   targetix=int(ci[localix]);dis=np.linalg.norm(ctr[targetix][perms]-tr[None,:,:],axis=2).max(1);pi=int(np.argmin(dis))
   if dis[pi]<.0005:matches.append({'renderer_triangle':sourceord,'collider_triangle':targetix,'vertex_permutation':perms[pi].tolist(),'maximum_vertex_error_model':float(dis[pi])})
 candidate_receipts.append({'owner':c,'mesh':cm,'faces_in_assembly_volume':len(ci),'exact_match_count':len(matches),'note':'Bounds intersection is context only, never permission to alter other collider geometry.'})
 if matches:collider_matches.append({'owner':c,'mesh':cm,'matches':matches,'positions_exact_to_renderer_vertex_buffer':bool(np.array_equal(cv,v)),'normals_exact_to_renderer_vertex_buffer':bool(np.array_equal(cn,n)),'all_channel_scope':'Binary contains position/normal/indices only. Original FBX tangent/UV bytes need root targeted current export before collider partition.'})
# Detect all exact render duplicates, including generated roots. Original exact source map considered FPSWorld only.
render_duplicate_matches=[]
for r in S['renderers']:
 if not r.get('mesh')or not MM[r['mesh']].get('binary')or not r.get('active')or not r.get('renderer_enabled'):continue
 if r['global_object_id']==owner['global_object_id']:continue
 rv,rt,rn=read(MM[r['mesh']]);rq=native(rv,r);rr=rq[rt];ri=np.flatnonzero(np.all(rr.max(1)>=lo-.001,1)&np.all(rr.min(1)<=hi+.001,1))
 if not len(ri):continue
 tree=cKDTree(rr[ri].mean(1));matches=[]
 for sourceord,tr in zip(sel,native_tri):
  for localix in tree.query_ball_point(tr.mean(0),.0005):
   ti=int(ri[localix]);dis=np.linalg.norm(rr[ti][perms]-tr[None,:,:],axis=2).max(1);pi=int(np.argmin(dis))
   if dis[pi]<.0005:matches.append({'source_renderer_triangle':sourceord,'duplicate_triangle':ti,'error':float(dis[pi])})
 if matches:render_duplicate_matches.append({'owner':r,'mesh':MM[r['mesh']],'matches':matches})
# Exact current serialized owner, transform and material references.
scene=Path(S['scene']);txt=scene.read_text();blocks={int(i):b for i,b in re.findall(r'^--- !u!\d+ &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',txt,re.M|re.S)}
rid=int(owner['global_object_id'].split('-')[-2]);renderer_component=blocks[rid];assert renderer_component.startswith('MeshRenderer:\n');goid=int(re.search(r'^  m_GameObject: \{fileID: (\d+)\}',renderer_component,re.M)[1]);go=blocks[goid];cids=[int(i)for i in re.findall(r'component: \{fileID: (\d+)\}',go)];tid=next(i for i in cids if blocks[i].startswith('Transform:\n'));compid=next(i for i in cids if blocks[i].startswith('MeshFilter:\n'));component=blocks[compid];lineage=[];cur=tid
while cur in blocks and blocks[cur].startswith('Transform:\n'):
 d=yaml.safe_load(blocks[cur])['Transform'];lineage.append({'transform_id':cur,'fields':d,'block_sha256':hb(blocks[cur].encode())});cur=d['m_Father']['fileID']
material_receipts=[{'path':p,'sha256':sha(p),'guid':re.search(r'^guid: (.*)$',Path(p+'.meta').read_text(),re.M)[1]}for p in owner['materials']]
sourceident=json.loads((E/'current-components.json').read_text());covered_sourceids={r['source_id']for r in a['source_face_correspondence']};existing_identities=[r for r in sourceident['all_source_identities']if r['fields']['SourceId']in covered_sourceids or r['fields']['SourcePath'].startswith('ROOT/g62/g44/g1/')]
col_counts=collections.Counter(x['renderer_triangle']for row in collider_matches for x in row['matches'])
packet={'schema':'chooguard.native-fixed-stair-geometry-preserving-separation.v1','role':'root_reviewable_bounded_current_source_partition','descriptive_id':'native-fixed-stair-ROOT-g62-g44-g1','guide_facility_assignment':None,'guide_assignment_policy':'Do not assign stairs-upper-left solely from this source group. Exact architecture ownership is independent of guide-symbol identity.','scene_sha256':S['scene_file_sha256'],'scene_file_sha256_at_stage':sha(scene),'inventory':str(IPath),'inventory_sha256':sha(IPath),'source_blend_sha256':A['source_blend_sha256'],'original_skp_sha256':'8e79e8186c22ed76fd1baa4cd06ee23bfaaa76659d06d7d4cec67847dec052db','source_assembly_prefix':'ROOT/g62/g44/g1/','source_receipt':str(APath),'source_receipt_sha256':sha(APath),'source_objects':a['source_face_correspondence'],'source_objects_count':len(a['source_face_correspondence']),'maximum_source_current_vertex_error_model':a['maximum_vertex_error_model'],'renderer':{'owner':owner,'mesh':m,'gameobject_id':goid,'meshfilter_component_id':compid,'meshfilter_global_object_id':owner['global_object_id'].rsplit('-',2)[0]+'-'+str(compid)+'-0','owner_global_object_id_component_type':'MeshRenderer','renderer_component_id':rid,'transform_id':tid,'meshfilter_block_sha256':hb(component.encode()),'renderer_block_sha256':hb(blocks[rid].encode()),'transform_lineage':lineage,'materials':material_receipts,'complete_vertex_buffer_sha256':hb(q['raw']),'complete_index_buffer_sha256':hb(q['index_bytes']),'source_channels':q['vd']['m_Channels'],'source_stride_bytes':q['stride'],'source_index_format':q['index_format'],'source_submeshes':q['submeshes'],'channel_absence_proof':q['extras'],'source_vertex_count':len(v),'source_triangle_count':len(t),'selected_triangle_ordinals':sel,'selected_triangles':t[sel].tolist(),'selected_vertex_indices':ids.tolist(),'selected_complete_vertex_buffer_sha256':hb(b''.join(q['raw'][int(i)*q['stride']:(int(i)+1)*q['stride']]for i in ids)),'selected_complete_vertex_bytes_hex':b''.join(q['raw'][int(i)*q['stride']:(int(i)+1)*q['stride']]for i in ids).hex(),'retained_triangle_count':len(keepfaces),'retained_index_sequence_sha256':hb(t[keepfaces].astype('<u4').tobytes()),'submesh_partition':submesh_partition,'selected_native_UVY_bounds':[lo.tolist(),hi.tolist()]},'collider_exact_correspondence':collider_matches,'collider_context_candidates':candidate_receipts,'collider_summary':{'selected_renderer_faces':len(sel),'unique_physics_owner_per_selected_face':all(col_counts[x]==1 for x in sel),'missing_selected_faces':[x for x in sel if not col_counts[x]],'ambiguous_selected_faces':[x for x in sel if col_counts[x]>1],'distinct_matching_colliders':len(collider_matches),'full_FBX_channels_pending':True},'duplicate_render_geometry':render_duplicate_matches,'existing_SourcePartIdentity_for_selected_stair':existing_identities,'operation_duplicate_policy':'Current source fixed stair has no SourcePartIdentity-driven motion object; retain all moving tread components and existing StationEscalator routes unchanged. Source names containing 계단 do not authorize new motion.','integration_contract':{'renderer':'Clone current source mesh twice with identical full vertex/channel buffers and submesh/material slot count. Background withholds only selected face ordinals. Child retains only the selected face ordinals, with exact original order, winding and material slot. Recompute bounds from referenced indices, not all retained unused vertices.','child_pose':'Identity local TRS as direct child of original owner GameObject; child inherits the exact current mirrored/scaled original world matrix. No vertices moved, synthesized or rounded.','collider_safe_default':'Retain original collider unchanged. This produces one complete physical copy and two disjoint render partitions, with no duplicate collider.','collider_optional_partition':'Root may split the one exact current collider after targeted full-channel/submesh export guard. Remove only independently matched collider ordinals and clone all original collider channels into retained/selected meshes. Same world matrix, original enabled/trigger/convex/cooking/material/layer fields preserved; selected physical surface union identical.','identity':'Use descriptive native-fixed-stair-ROOT-g62-g44-g1; preserve all 83 source mesh object IDs and matrices in receipt. Do not assign guide facility ID or add StationEscalator/StationElevator behavior.','replay':'Persist exact current asset/channel/index guards and companion-creation dependency. Regeneration must reproduce both disjoint render partitions and chosen collider ownership, or reject changed source; never replay a source-relative partition against shifted current ordinals.'},'allowed_geometry_change':False,'allow_new_motion':False,'materials_mutated':False,'production_mutated':False,'site_accuracy_verified':False,'preserved':['All 94705 source vertices and their complete P/N/T/UV0 bytes','Other 96428 renderer triangle indices and all material slot order','Current source world matrix and parent hierarchy','Original collider unless independently sealed partition is selected by root','Operating escalator owners/routes and StationDoor components','Entire1F geometry, walls/floor/roof/cap and all shop recipes'],'summary':{'selected_triangles':len(sel),'selected_vertices':len(ids),'exact_source_objects':len(a['source_face_correspondence']),'render_duplicate_owners':len(render_duplicate_matches),'collider_split_ready':False}}
(O/'native-fixed-stair-separation-packet.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2)+'\n')
print('STAIR_PACKET',packet['summary'],'COLLIDERS',packet['collider_summary'],'triangles',len(t),'retained',len(keepfaces),'selected_submeshes',[(p['submesh'],p['selected_triangle_count'])for p in submesh_partition if p['selected_triangle_count']])
