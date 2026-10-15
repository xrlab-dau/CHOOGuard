#!/usr/bin/env python3
"""Read-only audit of the sealed remaining-three-native-front transaction."""
from pathlib import Path
from collections import Counter
import hashlib, importlib.util, json, re, struct, sys
import numpy as np
import yaml

ROOT=Path.cwd();OUT=ROOT/'workers/twin/evidence/floor2-contract-review-20261009/remaining-native-fronts'
spec=importlib.util.spec_from_file_location('h',ROOT/'workers/twin/evidence/floor1-saved-scene-audit-20261009/audit_scene.py');h=importlib.util.module_from_spec(spec);spec.loader.exec_module(h)
packet_path=Path(sys.argv[1]);receipt_path=ROOT/'.planning/2026-10-09-floor2-guide-restoration/remaining-native-front-reuse-installation.json'
p=json.loads(packet_path.read_text());r=json.loads(receipt_path.read_text());checks=[];mesh_results=[]
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def check(name,good,detail):checks.append({'id':name,'pass':bool(good),'detail':detail})
def mesh(path):return yaml.load(Path(path).read_text().split('Mesh:\n',1)[1],Loader=h.LOADER)
def component_id(proof):return re.search(r'-(\d+)-0$',proof['global_object_id'])[1]
def f32_bytes(v):return np.array(v,dtype='<f4').tobytes()
def vec(d):return np.array([d[k] for k in ['x','y','z']],dtype=np.float64)
def integers(*v):return struct.pack('<'+'i'*len(v),*v)
def digest(data):return hashlib.sha256(data).hexdigest()
def string(v):
    b=v.encode();assert len(b)<128
    return bytes([len(b)])+b
def decoded(m):
    raw=bytes.fromhex(m['m_VertexData']['_typelessdata']);n=m['m_VertexData']['m_VertexCount'];stride=len(raw)//n
    active=[(i,c['stream'],c['offset'],c['format'],c['dimension']) for i,c in enumerate(m['m_VertexData']['m_Channels']) if c['dimension']]
    assert active==[(0,0,0,0,3),(1,0,12,0,3),(2,0,24,0,4),(4,0,40,0,2)]
    get=lambda offset,dim:np.ndarray((n,dim),dtype='<f4',buffer=raw,offset=offset,strides=(stride,4)).copy()
    assert m['m_IndexFormat']==1 and len(m['m_SubMeshes'])==1 and m['m_SubMeshes'][0]['topology']==0
    return raw,stride,get(0,3),get(12,3),get(24,4),get(40,2),np.frombuffer(bytes.fromhex(m['m_IndexBuffer']),dtype='<i4').copy()
def complete(m):
    raw,stride,v,n,t,uv,i=decoded(m)
    d=digest(integers(len(v),len(i),len(n))+v.tobytes()+i.tobytes()+n.tobytes())
    d=digest(string(d)+integers(len(uv),len(t))+uv.tobytes()+t.tobytes())
    uv4=np.c_[uv,np.zeros((len(uv),2),dtype='<f4')].astype('<f4')
    return digest(string(d)+integers(1,0,len(i))+i.tobytes()+b'\0'+integers(0)+integers(len(uv),2)+uv4.tobytes()+integers(*([0]*14)))

before,pre,bd,order=h.load_docs(ROOT/'.planning/2026-10-09-floor2-guide-restoration/before-remaining-native-front-reuse.unity')
after,apost,ad,aorder=h.load_docs(ROOT/'Assets/ChooGuard/Scenes/FpsStation.unity');bs=h.Scene(bd)
prior=json.loads((ROOT/'workers/twin/evidence/floor2-contract-review-20261009/native-front-reuse/saved-audit.json').read_text())
registry=json.loads((ROOT/'content/world/observed-interiors/floor-guide-layout.json').read_text());rows={x['source_asset']:x for x in registry['meshes']};allowed={};removed_count=edited_count=0
check('packet_receipt_sha',sha(packet_path)==r['packet_sha256'],sha(packet_path))
check('current21_baseline_sha',h.sha(before)==p['scene_sha256']==prior['scene_sha256'],h.sha(before))
check('saved_receipt_sha',h.sha(after)==r['scene_sha256'],h.sha(after))
check('document_identity_order',pre==apost and order==aorder,len(ad))
check('new_mesh_source_count',len(p['mesh_patches'])==len(r['sources'])==4,len(p['mesh_patches']))
for ordinal,(patch,saved) in enumerate(zip(p['mesh_patches'],r['sources'])):
    a=mesh(patch['source_asset']);b=mesh(saved['replacement']);ar,stride,av,an,at,auv,ai=decoded(a);br,bst,bv,bn,bt,buv,bi=decoded(b);expected=bytearray(ar)
    check('immutable_source_'+str(ordinal),sha(patch['source_asset'])==patch['source_file_sha256'] and sha(patch['source_asset']+'.meta')==patch['source_meta_sha256'],patch['source_asset'])
    check('current_source_complete_vertex_bytes_'+str(ordinal),digest(ar)==patch['source_full_vertex_bytes_sha256'],digest(ar))
    edits=patch.get('position_edits',[])
    check('unique_edited_vertices_'+str(ordinal),len({e['vertex_index'] for e in edits})==len(edits),len(edits))
    for edit in edits:
        index=edit['vertex_index'];check('expected_vertex_'+str(ordinal)+'_'+str(index),f32_bytes(av[index])==f32_bytes(edit['expected']),edit)
        struct.pack_into('<fff',expected,index*stride,*edit['replacement'])
    check('exact_all_channels_'+str(ordinal),bytes(expected)==br and stride==bst and a['m_VertexData']['m_Channels']==b['m_VertexData']['m_Channels'],'All undeclared vertex/channel/unused bytes exact')
    removed=patch['remove_triangle_ordinals'];rm=set(removed);ai3=ai.reshape(-1,3)
    check('unique_bounded_removed_faces_'+str(ordinal),len(rm)==len(removed) and all(0<=i<len(ai3) for i in rm),len(rm))
    retained=np.array([x for i,x in enumerate(ai3.tolist()) if i not in rm],dtype='<i4').reshape(-1)
    check('exact_oriented_retained_indices_'+str(ordinal),np.array_equal(retained,bi),{'source':len(ai)//3,'removed':len(rm),'saved':len(bi)//3})
    owner_ids={component_id(x) for x in patch['owners']+patch['collider_owners']};actual_ids={cid for cid,doc in bd.items() if doc['type'] in [33,64] and doc['data'].get('m_Mesh',{}).get('guid')==patch['source_asset_guid']}
    check('exact_actual_owner_closure_'+str(ordinal),owner_ids==actual_ids and len(owner_ids)==len(patch['owners'])+len(patch['collider_owners']),{'declared':sorted(owner_ids),'actual':sorted(actual_ids)})
    target_guid=re.search(r'^guid: (\w+)$',Path(saved['replacement']+'.meta').read_text(),re.M)[1]
    for owner,typ in [(x,33) for x in patch['owners']]+[(x,64) for x in patch['collider_owners']]:
        cid=component_id(owner);expected_ref=dict(bd[cid]['data']['m_Mesh']);expected_ref['guid']=target_guid
        check('saved_owner_'+cid,bd[cid]['type']==typ and bs.component_path(cid)==owner['path'] and ad[cid]['data']['m_Mesh']==expected_ref,owner['path']);allowed[cid]=['m_Mesh']
    row=rows[patch['original_registered_source_asset']];previous=patch['source_lineage_registry_entry']
    fields=['source_asset','baseline_asset','patch_baseline_asset','source_digest','packet','packet_sha256','verified_rebuild_variants']
    check('baseline_lineage_'+str(ordinal),all(row.get(k)==previous.get(k) for k in fields) and row['replacement_asset']==saved['replacement'] and row['native_front_reuse_evidence_sha256']==sha(packet_path),'Earliest baseline, source digest and exact certificates retained')
    d=complete(b);check('independent_complete_replacement_digest_'+str(ordinal),d==row['replacement_digest'],d)
    bound=b['m_LocalAABB'];c=vec(bound['m_Center']);e=vec(bound['m_Extent']);delta=float(max(np.abs(c-e-bv.min(axis=0)).max(),np.abs(c+e-bv.max(axis=0)).max()))
    sm=b['m_SubMeshes'][0];sc=vec(sm['localAABB']['m_Center']);se=vec(sm['localAABB']['m_Extent']);used=bv[np.unique(bi)];under=float(max((sc-se-used.min(axis=0)).max(),(used.max(axis=0)-sc-se).max(),0))
    check('finite_mesh_and_submesh_bounds_'+str(ordinal),np.isfinite(bv).all() and np.isfinite(c).all() and np.isfinite(e).all() and np.all(e>=0) and delta<1e-5 and under<1e-5 and sm['indexCount']==len(bi),{'mesh_bounds_delta':delta,'retained_undercoverage':under})
    keys=['m_Shapes','m_BindPose','m_BoneNameHashes','m_RootBoneNameHash','m_BonesAABB','m_VariableBoneCountWeights','m_MeshCompression','m_IsReadable','m_KeepVertices','m_KeepIndices']
    check('shape_skin_metadata_'+str(ordinal),all(a.get(k)==b.get(k) for k in keys),keys)
    for material in patch.get('source_materials',[]):
        meta=Path(material['asset']+'.meta');guid=re.search(r'^guid: (\w+)$',meta.read_text(),re.M)[1]
        check('source_material_'+str(ordinal)+'_'+material['guid'],sha(material['asset'])==material['file_sha256'] and sha(meta)==material['meta_sha256'] and guid==material['guid'],material['asset'])
    removed_count+=len(rm);edited_count+=len(edits);mesh_results.append({'source':patch['source_asset'],'replacement':saved['replacement'],'vertices':len(bv),'removed_triangles':len(rm),'edited_vertices':len(edits),'retained_triangles':len(bi)//3,'complete_channels_sha256':d,'all_vertex_bytes_sha256':digest(br),'actual_owner_ids':sorted(actual_ids)})

for evidence in p['evidence']:check('frozen_evidence_'+evidence['path'],sha(evidence['path'])==evidence['sha256'],evidence['path'])
check('exact_three_new_door_bindings',len(p['doors'])==3 and len({d['generic_element'] for d in p['doors']})==3 and len({d['native_element'] for d in p['doors']})==3,len(p['doors']))
for proof in p['doors']:
    cid=component_id(proof);gid=h.ref_id(bd[cid]['data']['m_GameObject']);allowed[gid]=['m_IsActive']
    check('only_generated_door_inactive_'+gid,bd[gid]['data']['m_IsActive']==1 and ad[gid]['data']['m_IsActive']==0,proof['generic_element'])
changed=[];bad=[]
for cid,old in bd.items():
    new=ad[cid]
    if old['raw']==new['raw']:continue
    fields=allowed.get(cid,[]);ok=bool(fields) and h.strip_fields(old['raw'],fields)==h.strip_fields(new['raw'],fields);x={'id':cid,'type':old['type'],'authorized_fields':fields,'only_authorized':ok};changed.append(x)
    if not ok:bad.append(x)
check('only_declared_remaining_documents',not bad and len(changed)==len(allowed),changed)
paths={}
for tid,doc in bd.items():
    if doc['type']==4:
        try:paths[tid]=bs.path(tid)
        except (KeyError,TypeError):pass
native_gos={h.ref_id(bd[k]['data']['m_GameObject']) for k,path in paths.items() if path=='FPSWorld' or path.startswith('FPSWorld/')};native_ids=native_gos|{k for k,doc in bd.items() if h.ref_id(doc['data'].get('m_GameObject',{})) in native_gos};native_changed=[k for k in native_ids if bd[k]['raw']!=ad[k]['raw']]
check('all_native_source_documents_exact',not native_changed,{'count':len(native_ids),'changed':native_changed})
for label,predicate in [('all_door_fields',lambda doc:doc['type']==114 and 'StationDoor' in doc['data'].get('m_EditorClassIdentifier','')),('all_transform_fields',lambda doc:doc['type']==4),('all_source_identity_fields',lambda doc:doc['type']==114 and 'SourcePartIdentity' in doc['data'].get('m_EditorClassIdentifier',''))]:
    ids=[k for k,doc in bd.items() if predicate(doc)];check(label,all(bd[k]['raw']==ad[k]['raw'] for k in ids),len(ids))
old_packet_path=ROOT/'workers/twin/output/floor2-main-restoration-20261009/door21-native-reuse/packet.json';old_packet=json.loads(old_packet_path.read_text());old_door=old_packet['withhold_door'];old_cid=component_id(old_door);old_gid=h.ref_id(bd[old_cid]['data']['m_GameObject'])
check('existing21_duplicate_remains_inactive',bd[old_gid]['raw']==ad[old_gid]['raw'] and ad[old_gid]['data']['m_IsActive']==0,old_gid)
withheld=registry['withheld_duplicate_doors'];check('all_four_withholding_policies',len(withheld)==4,len(withheld))
for proof,evidence in [(old_door,old_packet_path)]+[(d,packet_path) for d in p['doors']]:
    matches=[x for x in withheld if x['element']==proof['generic_element'] and x['index']==proof['index']]
    check('persistence_'+proof['generic_element'],len(matches)==1 and matches[0]['native_element']==proof['native_element'] and matches[0]['evidence_sha256']==sha(evidence),matches)

first_blob,first_pre,first_docs,first_order=h.load_docs(ROOT/'.planning/2026-10-09-floor2-guide-restoration/before-2f-entry-guide.unity');cumulative_allowed={x['id'] for x in prior['cumulative_completed_1f_baseline_authorized_changes']}|set(allowed);cumulative=[];cumulative_bad=[]
for cid,old in first_docs.items():
    if old['raw']==ad[cid]['raw']:continue
    new=ad[cid];typ=old['type'];fields=['m_Mesh'] if typ in [33,64] else ['m_LocalPosition'] if typ==4 else ['m_IsActive'] if typ==1 else []
    if cid in cumulative_allowed and typ==4 and old['data']['m_Children']!=new['data']['m_Children']:
        a=dict(old['data']);b=dict(new['data']);a.pop('m_Children');b.pop('m_Children');ok=a==b
    else:ok=cid in cumulative_allowed and bool(fields) and h.strip_fields(old['raw'],fields)==h.strip_fields(new['raw'],fields)
    x={'id':cid,'type':typ,'only_authorized_2f_fields':ok};cumulative.append(x)
    if not ok:cumulative_bad.append(x)
office=json.loads((ROOT/'workers/twin/evidence/floor2-contract-review-20261009/office-saved-audit.json').read_text());check('completed_1f_preserved',not cumulative_bad and first_pre==apost and [(k,t) for k,t in aorder if k in first_docs]==first_order and set(ad)-set(first_docs)==set(office['new_document_ids']),{'baseline_sha256':h.sha(first_blob),'changed_existing_documents':len(cumulative),'changed_types':dict(Counter(str(x['type']) for x in cumulative)),'unexpected':cumulative_bad})
result={'status':'pass' if all(x['pass'] for x in checks) else 'blocking','packet':str(packet_path),'packet_sha256':sha(packet_path),'source_baseline_scene_sha256':h.sha(before),'saved_scene_sha256':h.sha(after),'completed_1f_baseline_sha256':h.sha(first_blob),'document_count':len(ad),'removed_generated_faces':removed_count,'edited_vertices':edited_count,'stage_changed_documents':changed,'native_document_count':len(native_ids),'meshes':mesh_results,'cumulative_changes':cumulative,'checks':checks,'limits':'Exact saved source/Scene and persistence artifact checks; actual native leaf motion and CharacterController routes are root-owned. Public references do not establish measured field dimensions.'}
(OUT/'saved-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:result[k] for k in ['status','saved_scene_sha256','document_count','removed_generated_faces','edited_vertices','stage_changed_documents','native_document_count']},ensure_ascii=False,indent=2));print('FAILED CHECKS',json.dumps([x for x in checks if not x['pass']],ensure_ascii=False))
