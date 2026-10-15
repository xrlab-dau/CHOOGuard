#!/usr/bin/env python3
"""Exact attributed oriented triangle certificates; never mutates production assets."""
from pathlib import Path
from collections import Counter,defaultdict
import json,hashlib,struct
import numpy as np
import yaml
ROOT=Path.cwd();OUT=ROOT/'workers/twin/evidence/floor2-contract-review-20261009/regeneration';BASE=ROOT/'.planning/2026-10-08-interior-runtime-completion/floor-guide-layout';RECIPE=ROOT/'.planning/2026-09-24-interior-twin/specs/2f-main.json'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def digest(b):return hashlib.sha256(b).hexdigest()
def int32(*n):return struct.pack('<'+'i'*len(n),*n)
def dotnet_string(s):
 b=s.encode();assert len(b)<128;return bytes([len(b)])+b
def complete_digest(x):
 v=np.array(x['vertices'],'<f4');i=np.array(x['indices'],'<i4');n=np.array(x['normals'],'<f4');uv=np.array(x['uv'],'<f4');t=np.array(x['tangents'],'<f4')
 mesh=digest(int32(len(v),len(i),len(n))+v.tobytes()+i.tobytes()+n.tobytes())
 channel=digest(dotnet_string(mesh)+int32(len(uv),len(t))+uv.tobytes()+t.tobytes())
 # Exact CompleteChannelDigest layout: one Triangle submesh, no colors, UV0=Float32x2, all UV1..7 absent.
 uv4=np.c_[uv,np.zeros((len(uv),2),'<f4')].astype('<f4')
 body=dotnet_string(channel)+int32(1,0,len(i))+i.tobytes()+b'\0'+int32(0)+int32(len(uv),2)+uv4.tobytes()+int32(*([0]*14))
 return digest(body)
def groups(x,full=False):
 values=np.c_[x['vertices'],x['uv'],x['normals'],x['tangents']] if full else np.c_[x['vertices'],x['uv']]
 t=np.array(x['indices'],int).reshape(-1,3);c=Counter();rows=defaultdict(list)
 for ordinal,ids in enumerate(t):
  corners=[tuple(values[i]) for i in ids];variants=[tuple(corners[k:]+corners[:k]) for k in range(3)];key=min(variants);c[key]+=1;rows[key].append(ordinal)
 return c,rows

def hash_counter(c):
 h=hashlib.sha256()
 for key,count in sorted(c.items()):h.update(struct.pack('<'+'d'*(len(key)*len(key[0]))+'I',*[v for corner in key for v in corner],count))
 return h.hexdigest()
summary=[]
for name in ['Kit_Frame_Canopy_Dark','Kit_Glass','Kit_Trim_Kit_Hairline']:
 pp=BASE/('rebuild-'+name+'.json');p=json.loads(pp.read_text());a=p['original'];b=p['generated'];f=Path(p['source_asset']);s=yaml.safe_load(f.read_text().split('Mesh:\n',1)[1]);raw=bytes.fromhex(s['m_VertexData']['_typelessdata']);raw_original=np.c_[a['vertices'],a['normals'],a['tangents'],a['uv']].astype('<f4').tobytes();indices=np.array(a['indices'],'<u4').tobytes()
 active=[(i,c['stream'],c['offset'],c['format'],c['dimension']) for i,c in enumerate(s['m_VertexData']['m_Channels']) if c['dimension']]
 assert active==[(0,0,0,0,3),(1,0,12,0,3),(2,0,24,0,4),(4,0,40,0,2)]
 assert s['m_IndexFormat']==1 and len(s['m_SubMeshes'])==1 and s['m_SubMeshes'][0]['topology']==0 and raw==raw_original and bytes.fromhex(s['m_IndexBuffer'])==indices
 assert not s['m_BindPose'] and not s['m_Shapes']['vertices'] and not s['m_Shapes']['channels'] and s['m_VariableBoneCountWeights']['m_Data'] is None
 ad=complete_digest(a);bd=complete_digest(b);assert ad==p['original_complete_digest'] and bd==p['generated_complete_digest']
 pa,pr=groups(a);pb,br=groups(b);ca,ar=groups(a,True);cb,gr=groups(b,True);assert pa==pb and ca==cb
 av=np.c_[a['vertices'],a['uv'],a['normals'],a['tangents']];bv=np.c_[b['vertices'],b['uv'],b['normals'],b['tangents']];at=np.array(a['indices']).reshape(-1,3);bt=np.array(b['indices']).reshape(-1,3);patterns=Counter();quad_proof=[]
 for q in range(len(av)//4):
  aa=av[4*q:4*q+4];bb=bv[4*q:4*q+4];perm=[]
  for corner in bb:
   matches=[j for j,c in enumerate(aa) if np.array_equal(corner,c)];assert len(matches)==1;perm.append(matches[0])
  assert tuple(perm) in [(0,1,2,3),(0,2,1,3)]
  old=at[q*2:q*2+2];new=bt[q*2:q*2+2]
  def tri_counts(tris,values):
   cc=Counter()
   for ids in tris:
    corners=[tuple(values[i]) for i in ids];cc[min(tuple(corners[k:]+corners[:k]) for k in range(3))]+=1
   return cc
  assert tri_counts(old,av)==tri_counts(new,bv)
  patterns[tuple(perm)]+=1;quad_proof.append({'quad_ordinal':q,'generated_corner_to_original_corner':perm,'original_indices':old.tolist(),'generated_indices':new.tolist()})
 counts=lambda x:{'vertices':len(x['vertices']),'triangles':len(x['indices'])//3,'referenced_vertices':len(set(x['indices']))}
 certificate={'schema':'chooguard.two-source-regeneration-triangle-certificate.v1','source_name':name,'probe_path':str(pp.relative_to(ROOT)),'probe_sha256':sha(pp),'source_asset':p['source_asset'],'source_asset_file_sha256':sha(f),'source_asset_guid':s.get('guid'), 'provided_original_complete_digest':ad,'provided_generated_complete_digest':bd,'complete_digest_equal':ad==bd,'method':'Exact raw JSON numeric equality and serialized Float32 source bytes. Full triangle keys include positionXYZ, UV0, normalXYZ and tangentXYZW. Only cyclic rotations are canonicalized; reversed orientation is rejected. Duplicate multiplicity and individual quad order are retained. No rounding or tolerance.','original_counts':counts(a),'generated_counts':counts(b),'exact_oriented_position_UV_triangle_multiset_equal':pa==pb,'original_oriented_triangle_multiset_sha256':hash_counter(pa),'generated_oriented_triangle_multiset_sha256':hash_counter(pb),'exact_provided_normal_tangent_triangle_multiset_equal':ca==cb,'exact_full_attributed_oriented_triangle_multiset_equal':ca==cb,'full_original_oriented_triangle_multiset_sha256':hash_counter(ca),'full_generated_oriented_triangle_multiset_sha256':hash_counter(cb),'unmatched_original_triangles':sum((pa-pb).values()),'unmatched_generated_triangles':sum((pb-pa).values()),'normal_residual':{'maximum_absolute_component':0,'changed_matched_corner_occurrences':0},'tangent_residual':{'maximum_absolute_component':0,'changed_matched_corner_occurrences':0},'every_original_vertex_channel_matches_probe_bytes':True,'serialized_source_vertex_bytes_sha256':digest(raw),'reconstructed_original_vertex_bytes_sha256':digest(raw_original),'original_complete_digest_independently_recomputed':ad,'generated_complete_digest_independently_recomputed':bd,'all_omitted_channels_proven_absent':{'source_color':True,'source_uv1_through_uv7':True,'source_skin_and_blend_shapes':True,'generated_complete_digest_requires_no_color_or_extra_uv':True,'generated_fresh_Batch_Fill_has_no_skin_or_blend_shapes':True},'source_vertex_attributes':active,'submeshes':1,'index_format':'UInt32','quad_permutation_counts':{str(k):v for k,v in patterns.items()},'every_individual_quad_attributed_oriented_triangles_equal':True,'quad_mapping':quad_proof,'canonical_recipe':str(RECIPE.relative_to(ROOT)),'canonical_recipe_sha256':sha(RECIPE),'generator_file':'AgentScripts/MajibangBuilder.cs','generator_sha256':sha(ROOT/'AgentScripts/MajibangBuilder.cs'),'generator_contract':'KitStorefront constructs Frame/Glass/Trim batches; Batch.Fill on fresh Mesh sets only position, UV0, recalculated/provided normals, tangents and one Triangle submesh. Serialized source channels and independent complete digest recomputation pin absent color/UV1..7. Runtime guard must continue rejecting omitted channels, bones and blend shapes.','verdict':'EXACT_ATTRIBUTED_ORIENTED_TRIANGLE_REORDER_ONLY','tolerance_used':None,'asset_mutations':0,'recommendation':'Allow only this exact generated complete digest against this exact original source/baseline hash and this sealed certificate. Never relax general source digest/index validation.'}
 meta=Path(str(f)+'.meta');import re;certificate['source_asset_guid']=re.search(r'^guid: (\w+)$',meta.read_text(),re.M)[1]
 out=OUT/(name+'-certificate.json');out.write_text(json.dumps(certificate,ensure_ascii=False,indent=2)+'\n');summary.append({'name':name,'certificate':str(out.relative_to(ROOT)),'certificate_sha256':sha(out),'source_asset':p['source_asset'],'original_complete_digest':ad,'generated_complete_digest':bd,'triangles':len(at),'quad_permutations':certificate['quad_permutation_counts'],'exact_full_attributed_oriented_triangle_multiset_equal':True,'unmatched_triangles':0,'source_bytes_match_probe':True,'complete_digests_reproduced':True})
(OUT/'sealed-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n');print(json.dumps(summary,ensure_ascii=False,indent=2))
