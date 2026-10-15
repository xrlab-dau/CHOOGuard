"""Read existing Unity YAML and binary exports; write bounded lineage evidence only."""
from pathlib import Path
import hashlib, json, re, struct, math
ROOT=Path('/Users/um-yunsang/CHOOGuard')
OUT=ROOT/'workers/twin/evidence/floor1-rebuild-lineage-20261009'
def sha(p):return hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
def js(p):return json.loads((ROOT/p).read_text())
def digest(b):return hashlib.sha256(b).hexdigest()
def i(n):return struct.pack('<i',n)
def s(v):return bytes([len(v)])+v.encode() # digest strings are64 UTF-8 bytes

def mesh(p):
 text=(ROOT/p).read_text();data=text.split('  m_VertexData:',1)[1].split('  m_CompressedMesh:',1)[0]
 count=int(re.search(r'm_VertexCount: (\d+)',data).group(1))
 channels=[tuple(map(int,m)) for m in re.findall(r'- stream: (\d+)\n\s+offset: (\d+)\n\s+format: (\d+)\n\s+dimension: (\d+)',data)]
 raw=bytes.fromhex(re.search(r'_typelessdata: ([0-9a-f]+)',data).group(1))
 active=[c for c in channels if c[3]]
 assert all(c[0]==0 and c[2]==0 for c in active),'unsupported vertex format'
 stride=max(c[1]+4*c[3] for c in active);assert len(raw)==count*stride
 ib=bytes.fromhex(re.search(r'  m_IndexBuffer: ([0-9a-f]+)',text).group(1))
 assert re.search(r'  m_IndexFormat: (\d+)',text).group(1)=='1'
 idx=struct.unpack('<'+'i'*(len(ib)//4),ib)
 assert all(0<=x<count for x in idx)
 def attr(index):
  stream,offset,fmt,dim=channels[index]
  return b''.join(raw[x*stride+offset:x*stride+offset+dim*4] for x in range(count)) if dim else b''
 pos,norm,tan,uv=attr(0),attr(1),attr(2),attr(4)
 assert all(math.isfinite(v) for v in struct.unpack('<'+'f'*(len(raw)//4),raw))
 binary=i(count)+i(len(idx))+i(count)+pos+ib+norm
 channel=digest(s(digest(binary))+i(count if uv else 0)+i(count if tan else 0)+uv+tan)
 complete=s(channel)+i(1)+i(0)+i(len(idx))+ib+struct.pack('<?',False)+i(0)
 for ch in range(8):
  c=channels[ch+4];values=attr(ch+4)
  complete+=i(count if values else 0)+i(c[3] if values else 0)
  if values:
   for v in range(count):complete+=values[v*c[3]*4:(v+1)*c[3]*4]+bytes((4-c[3])*4)
 return {'path':p,'file_sha256':sha(p),'vertices':count,'triangles':len(idx)//3,'indices':idx,'index_bytes':ib,'raw':raw,'channels':channels,'attributes':{'positions':pos,'normals':norm,'tangents':tan,'uv0':uv},'binary_sha256':digest(binary),'binary':binary,'channels_sha256':channel,'complete_digest':digest(complete),'referenced_vertices':len(set(idx))}

def report(m):return {k:v for k,v in m.items() if k not in ['indices','index_bytes','raw','attributes','binary']}
registry=js('content/world/observed-interiors/floor-guide-layout.json')
packet=js('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-packet.json')
overlap=js('.planning/2026-10-08-interior-runtime-completion/architecture-kitshop-overlap.json')
receipt=js('.planning/2026-10-08-interior-runtime-completion/architecture-envelope-repair.json')
source_paths=['Assets/ChooGuard/Art/StationInterior/Kit/shops_1F/Kit_CeilingGrid.asset','Assets/ChooGuard/Art/StationInterior/Kit/shops_1F/Kit_Light_조명기구_downlight.asset','Assets/ChooGuard/Art/StationInterior/Kit/shops_1F/Kit_ShopCeiling_Kit_GridWhite.asset','Assets/ChooGuard/Art/StationInterior/ArchitectureCorrections/1F_KitFixturesOnly.asset']
raw=mesh('Assets/ChooGuard/Art/StationInterior/Kit/shops_1F/Kit_Colliders.asset');fixed=mesh(source_paths[-1])
removed=overlap['duplicate_kit_triangle_ordinals'];kept=overlap['preserve_remaining_triangle_ordinals'];partition=set(removed)|set(kept)
expected_idx=tuple(x for ordinal in kept for x in raw['indices'][ordinal*3:ordinal*3+3])
collider={'raw':report(raw),'fixtures_baseline':report(fixed),'removed_wall_triangles':len(removed),'retained_fixture_triangles':len(kept),'removal_ordinals':removed,'retained_ordinals':kept,'ordinals_unique_disjoint_complete':len(set(removed))==len(removed) and len(set(kept))==len(kept) and not(set(removed)&set(kept)) and partition==set(range(raw['triangles'])),'exact_retained_indices_source_order':expected_idx==fixed['indices'],'all_vertex_stream_bytes_identical':raw['raw']==fixed['raw'],'all_vertex_attribute_arrays_identical':{k:raw['attributes'][k]==fixed['attributes'][k] for k in raw['attributes']},'raw_binary_export_matches':sha(overlap['kit_mesh']['binary'])==raw['binary_sha256']==overlap['kit_mesh']['binary_sha256'],'source_to_baseline_complete_digest_equal':raw['complete_digest']==fixed['complete_digest'],'qualification':'This proves stored raw-to-correction partition; freshly generated observed-spec channels were not supplied for this missing batch.'}
def patch_evidence(patch,a,b):
 expected=bytearray(a['attributes']['positions'])
 for vi,old,new in zip(patch['vertex_indices'],patch['expected_positions'],patch['replacement_positions']):
  assert max(abs(x-y) for x,y in zip(struct.unpack_from('<3f',a['attributes']['positions'],vi*12),old))<.00001
  struct.pack_into('<3f',expected,vi*12,*new)
 return {'packet_vertices':patch['vertices'],'packet_triangles':patch['triangles'],'packet_selected_triangles':len(patch['selected_triangles']),'packet_moved_vertices':len(patch['vertex_indices']),'baseline':report(a),'replacement':report(b),'baseline_file_matches_packet':a['file_sha256']==patch['source_file_sha256'],'baseline_binary_matches_packet':a['binary_sha256']==patch['source_binary_sha256'],'exact_same_indices':a['index_bytes']==b['index_bytes'],'exact_patch_positions_match_replacement':bytes(expected)==b['attributes']['positions'],'normal_uv_tangent_dependencies':{k:a['attributes'][k]==b['attributes'][k] for k in ['normals','tangents','uv0']},'packet_owner_paths':[x['path'] for x in patch.get('owners',[])+patch.get('collider_owners',[])]}
lineage=[]
for p in source_paths:
 row=next(x for x in registry['meshes'] if x['source_asset']==p)
 current_packet=js(row['packet'])
 patch_source=row.get('patch_baseline_asset',row['baseline_asset'])
 patch=next(x for x in current_packet['mesh_patches'] if x['source_asset']==patch_source)
 a=mesh(row['baseline_asset']);b=mesh(row['replacement_asset']);intermediate=mesh(patch_source)
 steps=[]
 if patch_source!=a['path']:
  old_patch=next(x for x in packet['mesh_patches'] if x['source_asset']==a['path'])
  steps.append(patch_evidence(old_patch,a,intermediate))
 steps.append(patch_evidence(patch,intermediate,b))
 lineage.append({'registry_row':row,'actual_packet_sha256':sha(row['packet']),'packet_hash_matches_registry':sha(row['packet'])==row['packet_sha256'],'baseline':report(a),'replacement':report(b),'baseline_complete_matches_registry':a['complete_digest']==row['source_digest'],'replacement_complete_matches_registry':b['complete_digest']==row['replacement_digest'],'ordered_patch_steps':steps})
can=js('content/world/observed-interiors/shops-1f.json');old=js(can['sourceSpec']);byid={x['id']:x for x in old['elements']}
policy=[]
for e in can['elements']:
 a=byid[e['id']]; keys=['front','ceiling','downlightPitch','collider','openEdges']; policy.append({'id':e['id'],'same_geometry':a['geometry']==e['geometry'],'same_source_envelope_parameter_values':all(a['params'].get(k)==e['params'].get(k) for k in keys),'canonical_buildEnvelope':e['params'].get('buildEnvelope',True),'legacy_buildEnvelope':a['params'].get('buildEnvelope',True),'legacy_layout_without_placementEvidence':sum(not x.get('placementEvidence') for x in a['params'].get('layout',[])),'canonical_fixture_collision_boxes':sum(x['fixture'] in ['shelf','gondola','bakeryShelf','cooler'] for x in e['params'].get('layout',[]) if x.get('placementEvidence'))})
proof={'schema':'chooguard.floor1-source-rebuild-lineage-readonly.v1','inputs':[{'path':p,'sha256':sha(p)} for p in ['AgentScripts/MajibangBuilder.cs','Assets/ChooGuard/Editor/Twin/InteriorEnvelopeRepair.cs','Assets/ChooGuard/Editor/Twin/FloorGuideLayoutRegistry.cs','Assets/ChooGuard/Editor/Twin/ArchitecturalCorrectionRegistry.cs','content/world/observed-interiors/floor-guide-layout.json','content/world/observed-interiors/structural-corrections.json','content/world/observed-interiors/shops-1f.json',can['sourceSpec'],'.planning/2026-10-08-interior-runtime-completion/architecture-kitshop-overlap.json','.planning/2026-10-08-interior-runtime-completion/architecture-envelope-repair.json','.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-packet.json']],'raw_to_fixtures_only':collider,'guide_layout_lineages':lineage,'shop_policy':policy,'canonical_collision_fixture_box_total':sum(x['canonical_fixture_collision_boxes'] for x in policy),'canonical_expected_fixture_triangle_count_from_explicit_KColBox_calls':12*sum(x['canonical_fixture_collision_boxes'] for x in policy),'canonical_expected_fixture_vertex_count_from_explicit_KColBox_calls':24*sum(x['canonical_fixture_collision_boxes'] for x in policy),'source_receipt':receipt,'fresh_rebuild_claim_supported':False,'unity_calls':0,'assets_or_registry_writes':0,'classification':'stored lineage directly_supported; fresh generation insufficient'}
(OUT/'lineage-proof.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'collider_counts':[raw['vertices'],raw['triangles'],fixed['vertices'],fixed['triangles']],'partition_exact':collider['exact_retained_indices_source_order'],'all_original_attributes_identical':collider['all_vertex_stream_bytes_identical'],'raw_complete':raw['complete_digest'],'fixtures_complete':fixed['complete_digest'],'guide_lineages':[{'path':x['baseline']['path'],'complete_match':x['baseline_complete_matches_registry'],'replacement_match':x['replacement_complete_matches_registry'],'packet_position_match':[step['exact_patch_positions_match_replacement'] for step in x['ordered_patch_steps']],'attribute_equal':[step['normal_uv_tangent_dependencies'] for step in x['ordered_patch_steps']]} for x in lineage],'observed_fixture_boxes':proof['canonical_collision_fixture_box_total'],'predicted_observed_collider_vertices':proof['canonical_expected_fixture_vertex_count_from_explicit_KColBox_calls']},ensure_ascii=False,indent=2))
