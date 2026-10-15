#!/usr/bin/env python3
"""Read-only final native frontage and completed 1F baseline preservation audit."""
from pathlib import Path
from collections import Counter
import hashlib,importlib.util,json,re,struct
import numpy as np
import yaml
ROOT=Path.cwd();OUT=ROOT/'workers/twin/evidence/floor2-contract-review-20261009/native-front-reuse'
sp=importlib.util.spec_from_file_location('h',ROOT/'workers/twin/evidence/floor1-saved-scene-audit-20261009/audit_scene.py');h=importlib.util.module_from_spec(sp);sp.loader.exec_module(h)
pp=ROOT/'workers/twin/output/floor2-main-restoration-20261009/door21-native-reuse/packet.json';rp=ROOT/'.planning/2026-10-09-floor2-guide-restoration/native-front-reuse-installation.json';p=json.loads(pp.read_text());r=json.loads(rp.read_text());checks=[]
def sha(x):return hashlib.sha256(Path(x).read_bytes()).hexdigest()
def chk(name,passed,detail):checks.append({'id':name,'pass':bool(passed),'detail':detail})
def mesh(path):return yaml.load(Path(path).read_text().split('Mesh:\n',1)[1],Loader=h.LOADER)
chk('packet_receipt_sha',sha(pp)==r['packet_sha256'],sha(pp));assert len(p['mesh_patches'])==len(r['sources']);meshes=[];remove_count=edit_count=0
reg=json.loads((ROOT/'content/world/observed-interiors/floor-guide-layout.json').read_text());rows={z['source_asset']:z for z in reg['meshes']}
for ordinal,(row,saved) in enumerate(zip(p['mesh_patches'],r['sources'])):
 source=row['source_asset'];a=mesh(source);b=mesh(saved['replacement']);ar=bytes.fromhex(a['m_VertexData']['_typelessdata']);br=bytes.fromhex(b['m_VertexData']['_typelessdata']);nv=a['m_VertexData']['m_VertexCount'];stride=len(ar)//nv;ch=a['m_VertexData']['m_Channels'];assert ch[0]['stream']==0 and ch[0]['format']==0 and ch[0]['dimension']==3
 chk('source_sha_'+str(ordinal),sha(source)==row['source_file_sha256'] and sha(source+'.meta')==row['source_meta_sha256'],source)
 chk('source_raw_prefix_'+str(ordinal),hashlib.sha256(ar).hexdigest()==row['source_full_vertex_bytes_sha256'],len(ar));expected=bytearray(ar);edited=[]
 for e in row['position_edits']:
  i=e['vertex_index'];offset=i*stride+ch[0]['offset'];old=struct.unpack_from('<fff',ar,offset);chk('exact_old_corner_'+str(ordinal)+'_'+str(i),np.array_equal(np.array(old,dtype='<f4'),np.array(e['expected'],dtype='<f4')),e)
  struct.pack_into('<fff',expected,offset,*e['replacement']);edited.append(i)
  chk('bounded_V_boundary_edit_'+str(ordinal)+'_'+str(i),np.array_equal(np.array(e['expected'][1:],dtype='<f4'),np.array(e['replacement'][1:],dtype='<f4')) and abs(e['expected'][0]+43.2)<1e-5 and abs(e['replacement'][0]+43.55)<1e-5,e)
 chk('all_unedited_channel_bytes_'+str(ordinal),bytes(expected)==br and ch==b['m_VertexData']['m_Channels'] and nv==b['m_VertexData']['m_VertexCount'],'Only exact declared position bytes differ; every normal/tangent/UV/unused vertex byte identical')
 old=np.frombuffer(bytes.fromhex(a['m_IndexBuffer']),'<u4' if a['m_IndexFormat'] else '<u2').reshape(-1,3);new=np.frombuffer(bytes.fromhex(b['m_IndexBuffer']),'<u4' if b['m_IndexFormat'] else '<u2').reshape(-1,3);removed=set(row['remove_triangle_ordinals']);expected_indices=np.array([face for i,face in enumerate(old.tolist()) if i not in removed],dtype=new.dtype)
 chk('indices_exact_'+str(ordinal),np.array_equal(new,expected_indices) and a['m_IndexFormat']==b['m_IndexFormat'] and len(b['m_SubMeshes'])==1 and b['m_SubMeshes'][0]['topology']==0,'Exact original oriented triangle sequence minus declared faces; same UInt32 format/material submesh')
 vr=rows[row['original_registered_source_asset']];prior=row['source_lineage_registry_entry'];keyfields=['source_asset','baseline_asset','patch_baseline_asset','source_digest','packet','packet_sha256']
 chk('baseline_lineage_'+str(ordinal),all(vr.get(k)==prior.get(k) for k in keyfields) and vr.get('verified_rebuild_variants')==prior.get('verified_rebuild_variants') and vr['replacement_asset']==saved['replacement'] and vr['native_front_reuse_evidence']==str(pp.relative_to(ROOT)) and vr['native_front_reuse_evidence_sha256']==sha(pp),'Earliest baseline/digest and certificates preserved; only saved replacement and additional reuse proof updated')
 remove_count+=len(removed);edit_count+=len(edited);meshes.append({'source':source,'replacement':saved['replacement'],'vertices':nv,'original_triangles':len(old),'saved_triangles':len(new),'removed_triangles':len(removed),'edited_vertices':edited,'source_full_vertex_bytes_sha256':hashlib.sha256(ar).hexdigest(),'exact_expected_saved_vertex_bytes_sha256':hashlib.sha256(expected).hexdigest(),'saved_vertex_bytes_sha256':hashlib.sha256(br).hexdigest()})
chk('reviewed_total_changes',remove_count==136 and edit_count==14,{'removed_generated_faces':remove_count,'edited_boundary_vertices':edit_count})
for e in p['evidence']:chk('evidence_'+e['path'],sha(e['path'])==e['sha256'],e['path'])
base=ROOT/'.planning/2026-10-09-floor2-guide-restoration/before-native-front-reuse.unity';scene=ROOT/'Assets/ChooGuard/Scenes/FpsStation.unity';before,pre,bd,order=h.load_docs(base);after,apost,ad,aorder=h.load_docs(scene);bs=h.Scene(bd);cs=h.Scene(ad);allowed={};mesh_ids=set()
chk('native_front_baseline_sha',h.sha(before)==p['scene_sha256'],h.sha(before));chk('saved_scene_sha',h.sha(after)==r['scene_sha256'],h.sha(after));chk('identity_order',order==aorder and pre==apost,len(order))
for row,saved in zip(p['mesh_patches'],r['sources']):
 guid=re.search(r'^guid: (\w+)$',Path(saved['replacement']+'.meta').read_text(),re.M)[1]
 for proof,typ in [(o,33) for o in row['owners']]+[(o,64) for o in row['collider_owners']]:
  cid=re.search(r'-(\d+)-0$',proof['global_object_id'])[1];old=bd[cid]['data']['m_Mesh'];expected=dict(old);expected['guid']=guid
  chk('owner_'+cid,bd[cid]['type']==typ and bs.component_path(cid)==proof['path'] and old['guid']==row['source_asset_guid'] and ad[cid]['data']['m_Mesh']==expected,proof['path']);allowed[cid]=['m_Mesh'];mesh_ids.add(cid)
action=p['gameobject_actions'][0];gid=str(action['gameobject_id']);allowed[gid]=['m_IsActive'];chk('only_duplicate_active_flag',bd[gid]['data']['m_IsActive']==1 and ad[gid]['data']['m_IsActive']==0,gid)
changed=[];unexpected=[]
for cid,old in bd.items():
 new=ad[cid]
 if old['raw']==new['raw']:continue
 fields=allowed.get(cid,[]);ok=bool(fields) and h.strip_fields(old['raw'],fields)==h.strip_fields(new['raw'],fields);x={'id':cid,'type':old['type'],'authorized_fields':fields,'only_authorized':ok};changed.append(x)
 if not ok:unexpected.append(x)
chk('only_declared_native_front_documents',not unexpected and len(changed)==9,changed)
# Every source-owned native document, including all leaf descendants/operator/jambs, stays byte-identical.
paths={}
for tid,d in bd.items():
 if d['type']==4:
  try:paths[tid]=bs.path(tid)
  except (KeyError,TypeError):pass
native_gos={h.ref_id(bd[k]['data']['m_GameObject']) for k,path in paths.items() if path=='FPSWorld' or path.startswith('FPSWorld/')};native_ids=native_gos|{k for k,d in bd.items() if h.ref_id(d['data'].get('m_GameObject',{})) in native_gos}
changed_native=[k for k in native_ids if bd[k]['raw']!=ad[k]['raw']];chk('all_native_documents_byte_identical',not changed_native,{'document_count':len(native_ids),'changed':changed_native})
doors=[k for k,d in bd.items() if d['type']==114 and 'StationDoor' in d['data'].get('m_EditorClassIdentifier','')];tids=[k for k,d in bd.items() if d['type']==4];chk('all_103_door_fields_exact',all(bd[k]['raw']==ad[k]['raw'] for k in doors),len(doors));chk('all_transform_documents_exact',all(bd[k]['raw']==ad[k]['raw'] for k in tids),len(tids));source_ids=[k for k,d in bd.items() if d['type']==114 and 'SourcePartIdentity' in d['data'].get('m_EditorClassIdentifier','')];chk('all_source_ID_fields_exact',all(bd[k]['raw']==ad[k]['raw'] for k in source_ids),len(source_ids))
withheld=reg.get('withheld_duplicate_doors',[]);d=p['withhold_door'];chk('persistent_duplicate_policy',len(withheld)==1 and withheld[0]['element']==d['generic_element'] and withheld[0]['index']==0 and withheld[0]['native_element']==d['native_element'] and withheld[0]['evidence_sha256']==sha(pp),withheld)
# Completed 1F baseline: authorize exact accumulated 2F mesh refs, 12 motions, office child link and one duplicate inactive flag.
fb=ROOT/'.planning/2026-10-09-floor2-guide-restoration/before-2f-entry-guide.unity';fblob,fp,fbd,fo=h.load_docs(fb);cum_mesh=set(mesh_ids);motions=set()
for ppath in ['workers/twin/output/floor2-main-restoration-20261009/four-bay-packet.json','workers/twin/output/floor2-platform-passages-20261009/main-up-current-source-openings.json']:
 q=json.loads((ROOT/ppath).read_text())
 for m in q['mesh_patches']:
  for proof in m['owners']+m['collider_owners']:cum_mesh.add(re.search(r'-(\d+)-0$',proof['global_object_id'])[1])
 for m in q.get('movable_transforms',[]):motions.add(str(m['source_transform_id']))
 office=json.loads((ROOT/'workers/twin/output/floor2-main-restoration-20261009/office-glazing-surface-proposal.json').read_text());office_cid=re.search(r'-(\d+)-0$',office['owner']['global_object_id'])[1];cum_mesh.add(office_cid);office_tid=bs.transform_for_go[h.ref_id(bd[office_cid]['data']['m_GameObject'])];cumulative=[];bad=[]
for cid,old in fbd.items():
 new=ad[cid]
 if old['raw']==new['raw']:continue
 if cid in cum_mesh:ok=h.strip_fields(old['raw'],['m_Mesh'])==h.strip_fields(new['raw'],['m_Mesh'])
 elif cid in motions:ok=h.strip_fields(old['raw'],['m_LocalPosition'])==h.strip_fields(new['raw'],['m_LocalPosition'])
 elif cid==gid:ok=h.strip_fields(old['raw'],['m_IsActive'])==h.strip_fields(new['raw'],['m_IsActive'])
 elif cid==office_tid:
  a1=dict(old['data']);a2=dict(new['data']);a1.pop('m_Children');a2.pop('m_Children');ok=a1==a2
 else:ok=False
 x={'id':cid,'type':old['type'],'only_authorized_2f_fields':ok};cumulative.append(x)
 if not ok:bad.append(x)
new_ids=set(ad)-set(fbd);officeAudit=json.loads((ROOT/'workers/twin/evidence/floor2-contract-review-20261009/office-saved-audit.json').read_text());chk('completed_1f_preserved',not bad and pre==fp and [(k,t) for k,t in aorder if k in fbd]==fo and new_ids==set(officeAudit['new_document_ids']),{'baseline_sha256':h.sha(fblob),'changed_existing_documents':len(cumulative),'changed_types':dict(Counter(str(x['type']) for x in cumulative)),'unexpected':bad,'new_office_only_documents':sorted(new_ids)})
result={'status':'pass' if all(x['pass'] for x in checks) else 'blocking','packet_sha256':sha(pp),'source_baseline_sha256':h.sha(before),'completed_1f_baseline_sha256':h.sha(fblob),'scene_sha256':h.sha(after),'document_count':len(ad),'removed_generated_faces':remove_count,'edited_boundary_vertices':edit_count,'stage_changed_documents':changed,'all_native_documents_preserved':{'count':len(native_ids),'changed':changed_native},'cumulative_completed_1f_baseline_authorized_changes':cumulative,'meshes':meshes,'checks':checks,'limits':'Exact source artifact/Scene preservation and bounded native reuse verified. Actual door navigation and same-floor runtime tests are root-owned; field metric accuracy is unverified.'}
(OUT/'saved-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:result[k] for k in ['status','scene_sha256','document_count','removed_generated_faces','edited_boundary_vertices','stage_changed_documents','all_native_documents_preserved']},ensure_ascii=False,indent=2));print('FAILED CHECKS',json.dumps([x for x in checks if not x['pass']],ensure_ascii=False))
