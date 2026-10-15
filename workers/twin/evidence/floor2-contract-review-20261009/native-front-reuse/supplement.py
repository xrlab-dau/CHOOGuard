#!/usr/bin/env python3
"""Independent saved bounds, complete digest and actual source-owner closure."""
from pathlib import Path
import hashlib, importlib.util, json, re, struct
import numpy as np
import yaml

ROOT=Path.cwd()
OUT=ROOT/'workers/twin/evidence/floor2-contract-review-20261009/native-front-reuse'
spec=importlib.util.spec_from_file_location('scene_helpers', ROOT/'workers/twin/evidence/floor1-saved-scene-audit-20261009/audit_scene.py')
h=importlib.util.module_from_spec(spec);spec.loader.exec_module(h)
packet_path=ROOT/'workers/twin/output/floor2-main-restoration-20261009/door21-native-reuse/packet.json'
p=json.loads(packet_path.read_text())
r=json.loads((ROOT/'.planning/2026-10-09-floor2-guide-restoration/native-front-reuse-installation.json').read_text())
reg=json.loads((ROOT/'content/world/observed-interiors/floor-guide-layout.json').read_text())
rows={x['source_asset']:x for x in reg['meshes']}
_,_,before,_=h.load_docs(ROOT/'.planning/2026-10-09-floor2-guide-restoration/before-native-front-reuse.unity')
checks=[];items=[]
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def digest(data):return hashlib.sha256(data).hexdigest()
def integers(*n):return struct.pack('<'+'i'*len(n),*n)
def string(s):
    b=s.encode();assert len(b)<128
    return bytes([len(b)])+b
def check(name,good,detail):checks.append({'id':name,'pass':bool(good),'detail':detail})
def mesh(path):return yaml.load(Path(path).read_text().split('Mesh:\n',1)[1],Loader=h.LOADER)
def vec(d):return np.array([d[k] for k in ['x','y','z']],dtype=np.float64)
def decode(m):
    raw=bytes.fromhex(m['m_VertexData']['_typelessdata']);n=m['m_VertexData']['m_VertexCount'];stride=len(raw)//n
    active=[(i,c['stream'],c['offset'],c['format'],c['dimension']) for i,c in enumerate(m['m_VertexData']['m_Channels']) if c['dimension']]
    assert active==[(0,0,0,0,3),(1,0,12,0,3),(2,0,24,0,4),(4,0,40,0,2)]
    a=lambda offset,dim:np.ndarray((n,dim),dtype='<f4',buffer=raw,offset=offset,strides=(stride,4)).copy()
    return a(0,3),a(12,3),a(24,4),a(40,2),np.frombuffer(bytes.fromhex(m['m_IndexBuffer']),dtype='<i4').copy()
def complete(m):
    v,n,t,uv,i=decode(m)
    d=digest(integers(len(v),len(i),len(n))+v.tobytes()+i.tobytes()+n.tobytes())
    d=digest(string(d)+integers(len(uv),len(t))+uv.tobytes()+t.tobytes())
    uv4=np.c_[uv,np.zeros((len(uv),2),dtype='<f4')].astype('<f4')
    return digest(string(d)+integers(1,0,len(i))+i.tobytes()+b'\0'+integers(0)+integers(len(uv),2)+uv4.tobytes()+integers(*([0]*14)))

for ordinal,(patch,saved) in enumerate(zip(p['mesh_patches'],r['sources'])):
    a=mesh(patch['source_asset']);b=mesh(saved['replacement']);v,n,t,uv,i=decode(b);used=v[np.unique(i)]
    owner_ids={str(re.search(r'-(\d+)-0$',x['global_object_id'])[1]) for x in patch['owners']+patch['collider_owners']}
    actual_ids={cid for cid,doc in before.items() if doc['type'] in [33,64] and doc['data'].get('m_Mesh',{}).get('guid')==patch['source_asset_guid']}
    check('actual_owner_closure_'+str(ordinal),owner_ids==actual_ids,{'expected':sorted(owner_ids),'actual':sorted(actual_ids)})
    bound=b['m_LocalAABB'];center=vec(bound['m_Center']);extent=vec(bound['m_Extent'])
    minimum=v.min(axis=0).astype(np.float64);maximum=v.max(axis=0).astype(np.float64)
    delta=float(max(np.abs(center-extent-minimum).max(),np.abs(center+extent-maximum).max()))
    check('all_vertex_bounds_'+str(ordinal),np.isfinite(v).all() and np.isfinite(center).all() and np.isfinite(extent).all() and np.all(extent>=0) and delta<1e-5,{'maximum_float32_serialization_delta':delta,'minimum':minimum.tolist(),'maximum':maximum.tolist()})
    sub=b['m_SubMeshes'][0];sc=vec(sub['localAABB']['m_Center']);se=vec(sub['localAABB']['m_Extent'])
    under=float(max((sc-se-used.min(axis=0)).max(),(used.max(axis=0)-sc-se).max(),0))
    check('retained_submesh_bounds_'+str(ordinal),under<1e-5 and sub['indexCount']==len(i) and sub['firstByte']==0 and sub['baseVertex']==0 and sub['firstVertex']<=int(i.min()) and sub['firstVertex']+sub['vertexCount']>int(i.max()),{'used_vertex_undercoverage':under,'submesh_index_count':sub['indexCount'],'retained_indices':len(i)})
    vd=complete(b);row=rows[patch['original_registered_source_asset']]
    check('independent_complete_replacement_digest_'+str(ordinal),vd==row['replacement_digest'],{'independent':vd,'registry':row['replacement_digest']})
    keys=['m_Shapes','m_BindPose','m_BoneNameHashes','m_RootBoneNameHash','m_BonesAABB','m_VariableBoneCountWeights','m_MeshCompression','m_IsReadable','m_KeepVertices','m_KeepIndices']
    check('shape_skin_readability_metadata_'+str(ordinal),all(a.get(k)==b.get(k) for k in keys),keys)
    items.append({'source':patch['source_asset'],'replacement':saved['replacement'],'exact_actual_owner_closure':sorted(actual_ids),'complete_channels_sha256':vd,'all_vertex_bounds_delta':delta,'retained_submesh_undercoverage':under,'replacement_file_sha256':sha(saved['replacement'])})

proof_path=ROOT/'.planning/2026-10-08-interior-runtime-completion/source-rebuild-channel-comparison.json'
proofs=json.loads(proof_path.read_text())
check('fresh_2f_main_nine_source_acceptance',len(proofs)==9 and all(x['accepted'] for x in proofs),{'path':str(proof_path.relative_to(ROOT)),'sha256':sha(proof_path),'source_count':len(proofs),'accepted':sum(x['accepted'] for x in proofs),'modified_ns':proof_path.stat().st_mtime_ns})
for proof in proofs:
    row=rows[proof['source_asset']]
    gen=proof['rebuilt_complete_channels_sha256']
    original=proof['original_complete_channels_sha256']
    variants=row.get('verified_rebuild_variants',[])
    exact=gen==original==row['source_digest']
    if not exact:
        candidates=[x for x in variants if x.get('generated_complete_digest')==gen]
        # Existing verified variant schema is read directly; digest strings must occur in exactly one sealed entry.
        if not candidates:candidates=[x for x in variants if gen in json.dumps(x)]
        exact=len(candidates)==1 and original==row['source_digest']
    check('pinned_fresh_source_'+Path(proof['source_asset']).stem,exact,{'source_asset':proof['source_asset'],'original_complete_digest':original,'rebuilt_complete_digest':gen,'registry_source_digest':row['source_digest'],'exact_or_certified_variant':bool(exact)})

result={'status':'pass' if all(x['pass'] for x in checks) else 'blocking','packet_sha256':sha(packet_path),'scene_sha256':sha(ROOT/'Assets/ChooGuard/Scenes/FpsStation.unity'),'saved_scene_sha256_under_review':r['scene_sha256'],'meshes':items,'fresh_source_proof_sha256':sha(proof_path),'checks':checks,'limits':'Saved bounds/digests, actual baseline closure and current source preflight only. Nav/runtime output can change the final Scene hash after this geometry audit; field dimensions are unverified.'}
(OUT/'supplemental-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'status':result['status'],'mesh_count':len(items),'failed_checks':[x for x in checks if not x['pass']]},ensure_ascii=False,indent=2))
