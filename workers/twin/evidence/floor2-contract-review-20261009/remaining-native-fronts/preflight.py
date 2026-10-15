#!/usr/bin/env python3
"""Independent frozen packet current-source and prior21 face-lineage checks."""
from pathlib import Path
import hashlib, importlib.util, json, re, sys
import numpy as np
import yaml
ROOT=Path.cwd();OUT=ROOT/'workers/twin/evidence/floor2-contract-review-20261009/remaining-native-fronts'
spec=importlib.util.spec_from_file_location('h',ROOT/'workers/twin/evidence/floor1-saved-scene-audit-20261009/audit_scene.py');h=importlib.util.module_from_spec(spec);spec.loader.exec_module(h)
packet_path=Path(sys.argv[1]);p=json.loads(packet_path.read_text());old=json.loads((ROOT/'workers/twin/output/floor2-main-restoration-20261009/door21-native-reuse/packet.json').read_text());old_by_registered={x['original_registered_source_asset']:x for x in old['mesh_patches']};checks=[];results=[]
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def check(name,good,detail):checks.append({'id':name,'pass':bool(good),'detail':detail})
def mesh(path):return yaml.load(Path(path).read_text().split('Mesh:\n',1)[1],Loader=h.LOADER)
def indices(m):return np.frombuffer(bytes.fromhex(m['m_IndexBuffer']),'<u4').reshape(-1,3)
def cid(proof,key='global_object_id'):return re.search(r'-(\d+)-0$',proof[key])[1]
blob,pre,docs,order=h.load_docs(ROOT/'Assets/ChooGuard/Scenes/FpsStation.unity');scene=h.Scene(docs)
reg=json.loads((ROOT/'content/world/observed-interiors/floor-guide-layout.json').read_text());rows={x['source_asset']:x for x in reg['meshes']}
check('source_scene_seal',h.sha(blob)==p['scene_sha256']=='3719ef2fbc1a87a51270bbae4ae98b196c8d22b400c1675798f09bf9ddc3425d',h.sha(blob))
check('four_unique_current_sources',len(p['mesh_patches'])==len({x['source_asset'] for x in p['mesh_patches']})==4,len(p['mesh_patches']))
for ordinal,patch in enumerate(p['mesh_patches']):
    m=mesh(patch['source_asset']);idx=indices(m);raw=bytes.fromhex(m['m_VertexData']['_typelessdata']);nv=m['m_VertexData']['m_VertexCount'];stride=len(raw)//nv;positions=np.ndarray((nv,3),dtype='<f4',buffer=raw,strides=(stride,4));previous=old_by_registered[patch['original_registered_source_asset']];old_m=mesh(previous['source_asset']);old_i=indices(old_m);old_removed=set(previous['remove_triangle_ordinals']);original_map=[i for i in range(len(old_i)) if i not in old_removed]
    check('source_file_and_meta_'+str(ordinal),sha(patch['source_asset'])==patch['source_file_sha256'] and sha(patch['source_asset']+'.meta')==patch['source_meta_sha256'],patch['source_asset'])
    check('full_current_channel_and_index_hashes_'+str(ordinal),hashlib.sha256(raw).hexdigest()==patch['source_full_vertex_bytes_sha256'] and hashlib.sha256(idx.astype('<u4').tobytes()).hexdigest()==patch['source_full_index_bytes_sha256'],len(raw))
    check('exact_prior21_source_vertex_and_face_lineage_'+str(ordinal),raw==bytes.fromhex(old_m['m_VertexData']['_typelessdata']) and np.array_equal(idx,old_i[original_map]),{'prior21_removed':len(old_removed),'current_faces':len(idx)})
    check('zero_new_positions_'+str(ordinal),not patch['position_edits'],len(patch['position_edits']))
    removed=set(patch['remove_triangle_ordinals']);removed_union=set()
    check('prior21_retained_faces_not_removed_'+str(ordinal),not removed.intersection(patch['preserved_unit21_current_triangle_ordinals']),patch['preserved_unit21_current_triangle_ordinals'])
    for mask in patch['unit_contributor_masks']:
        current=mask['removed_current_triangle_ordinals'];original=mask['original_triangle_ordinals'];selected=mask['selected_triangle_ordinals'];mapped=[original_map[x] for x in selected];retained_unit=mask['current_retained_unit_faces']
        check('exact_unit_face_mapping_'+str(ordinal)+'_'+str(mask['guide_number']),mapped==original and set(selected)==set(current)|set(retained_unit) and not removed_union.intersection(current) and not set(current).intersection(retained_unit),{'unit':mask['guide_number'],'contributor_faces':len(selected),'removed':len(current),'retained':len(retained_unit),'original_bounds':[min(original),max(original)]})
        minimum_retained_y=float(positions[idx[retained_unit]].reshape(-1,3)[:,1].min()) if retained_unit else None
        check('retained_unit_geometry_above_native_aperture_'+str(ordinal)+'_'+str(mask['guide_number']),minimum_retained_y is None or minimum_retained_y>9.3999,{'minimum_retained_y':minimum_retained_y,'native_nominal_top_approximately':9.4})
        removed_union.update(current)
    check('removal_only_from_three_declared_contributors_'+str(ordinal),removed_union==removed and len(removed)==len(patch['remove_triangle_ordinals']) and {x['guide_number'] for x in patch['unit_contributor_masks']}=={20,22,23},len(removed))
    expected=[i for i in range(len(idx)) if i not in removed]
    check('exact_retained_ordinal_and_bytes_hash_'+str(ordinal),expected==patch['source_retained_triangle_ordinals'] and hashlib.sha256(idx[expected].astype('<u4').tobytes()).hexdigest()==patch['source_retained_triangle_index_u32_sha256'],len(expected))
    owner_proofs=patch['owners']+patch['collider_owners'];expected_owners={cid(x) for x in owner_proofs};actual_owners={k for k,d in docs.items() if d['type'] in [33,64] and d['data'].get('m_Mesh',{}).get('guid')==patch['source_asset_guid']}
    check('actual_unique_owner_closure_'+str(ordinal),expected_owners==actual_owners and len(expected_owners)==len(owner_proofs) and all(scene.component_path(cid(o))==o['path'] for o in owner_proofs),sorted(actual_owners))
    check('exact_current_registry_lineage_'+str(ordinal),rows[patch['original_registered_source_asset']]==patch['source_lineage_registry_entry'] and rows[patch['original_registered_source_asset']]['replacement_asset']==patch['source_asset'],patch['original_registered_source_asset'])
    results.append({'source':patch['source_asset'],'removed_faces':len(removed),'retained_faces':len(expected),'prior21_source_lineage_exact':True,'actual_owner_ids':sorted(actual_owners)})
check('exact408_removal_zero_position_changes',sum(len(x['remove_triangle_ordinals']) for x in p['mesh_patches'])==408 and all(not x['position_edits'] for x in p['mesh_patches']),{'removed_faces':sum(len(x['remove_triangle_ordinals']) for x in p['mesh_patches'])})
for proof in p['doors']:
    generic=cid(proof);native=cid(proof,'native_global_object_id');go=h.ref_id(docs[generic]['data']['m_GameObject']);ngo=h.ref_id(docs[native]['data']['m_GameObject'])
    check('exact_door_binding_'+str(proof['unit']),docs[generic]['data']['Element']==proof['generic_element'] and docs[generic]['data']['Index']==proof['index'] and docs[native]['data']['Element']==proof['native_element'] and scene.component_path(generic)==proof['path'] and scene.component_path(native)==proof['native_path'] and docs[go]['data']['m_IsActive']==1 and docs[ngo]['data']['m_IsActive']==1 and docs[native]['data']['m_Enabled']==1 and docs[native]['data']['Kind']==0,{'generic':generic,'native':native})
check('prior21_withholding_policy_exact',reg['withheld_duplicate_doors']==p['preserve_existing_withheld_duplicate_doors'],reg['withheld_duplicate_doors'])
for e in p['evidence']:check('frozen_evidence_'+e['path'],sha(e['path'])==e['sha256'],e['path'])
result={'status':'pass' if all(x['pass'] for x in checks) else 'blocking','packet':str(packet_path),'packet_sha256':sha(packet_path),'scene_sha256':h.sha(blob),'draft_not_frozen':packet_path.name!='packet.json','meshes':results,'checks':checks,'limits':'Current source/face-lineage/owner/policy checks only. Offline aperture geometry QA and actual CharacterController tests remain distinct.'}
(OUT/('draft-preflight.json' if result['draft_not_frozen'] else 'frozen-preflight.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'status':result['status'],'draft_not_frozen':result['draft_not_frozen'],'packet_sha256':result['packet_sha256'],'failed_checks':[x for x in checks if not x['pass']]},ensure_ascii=False,indent=2))
