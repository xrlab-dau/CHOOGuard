#!/usr/bin/env python3
"""Independent saved four-bay artifact and Scene audit, no production writes."""
from pathlib import Path
import importlib.util, collections, hashlib, json, re, struct
import numpy as np
import yaml
ROOT=Path.cwd();OUT=ROOT/'workers/twin/evidence/floor2-contract-review-20261009'
helper=ROOT/'workers/twin/evidence/floor1-saved-scene-audit-20261009/audit_scene.py'
spec=importlib.util.spec_from_file_location('scene_helper',helper);h=importlib.util.module_from_spec(spec);spec.loader.exec_module(h)
packet_path=ROOT/'workers/twin/output/floor2-main-restoration-20261009/four-bay-packet.json'
receipt_path=ROOT/'.planning/2026-10-09-floor2-guide-restoration/2f-entry-installation.json'
packet=json.loads(packet_path.read_text());receipt=json.loads(receipt_path.read_text());checks=[]
def chk(name,passed,detail):checks.append({'id':name,'pass':bool(passed),'detail':detail})
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def mesh(p):return yaml.load(Path(p).read_text().split('Mesh:\n',1)[1],Loader=h.LOADER)
chk('packet_receipt_hash',sha(packet_path)==receipt['packet_sha256'],sha(packet_path))
chk('unique_source_rows',len({r['source_asset'] for r in packet['mesh_patches']})==len(packet['mesh_patches']),len(packet['mesh_patches']))
geo=[]
replacements={m['source_asset']:m['replacement_asset'] for m in receipt['meshes']}
for i,r in enumerate(packet['mesh_patches']):
 a=mesh(r['source_asset']);b=mesh(replacements[r['source_asset']]);x=a['m_VertexData'];y=b['m_VertexData'];raw=bytes.fromhex(x['_typelessdata']);got=bytes.fromhex(y['_typelessdata']);nv=x['m_VertexCount'];channels=x['m_Channels'];active=[c for c in channels if c['dimension']]
 chk(f'source_{i}_file',sha(r['source_asset'])==r['source_file_sha256'],r['source_asset'])
 chk(f'channels_{i}',channels==y['m_Channels'] and nv==y['m_VertexCount'] and all(c['stream']==0 and c['format']==0 for c in active),'Reviewed float32 stream 0 layout preserved')
 stride=len(raw)//nv;pos=channels[0];assert pos['format']==0 and pos['dimension']==3 and pos['stream']==0
 expected=bytearray(raw)
 for idx,new,old in zip(r['vertex_indices'],r['replacement_positions'],r['expected_positions']):
  offset=idx*stride+pos['offset'];source=struct.unpack_from('<fff',raw,offset)
  chk(f'corner_{i}_{idx}',np.max(np.abs(np.array(source)-old))<=2e-6,idx)
  struct.pack_into('<fff',expected,offset,*new)
 chk(f'exact_replacement_bytes_{i}',bytes(expected)==got,'Only declared position float32 bytes changed; every other channel and unused vertex exact')
 chk(f'indices_{i}',a['m_IndexBuffer']==b['m_IndexBuffer'] and a['m_IndexFormat']==b['m_IndexFormat'],'Exact index sequence/index format retained')
 sa=[{k:v for k,v in s.items() if k!='localAABB'} for s in a['m_SubMeshes']];sb=[{k:v for k,v in s.items() if k!='localAABB'} for s in b['m_SubMeshes']]
 chk(f'submeshes_{i}',sa==sb,'Submesh descriptors except recomputed local bounds retained')
 geo.append({'source':r['source_asset'],'replacement':replacements[r['source_asset']],'vertices':nv,'changed_vertices':len(r['vertex_indices']),'stride':stride,'channels':channels,'exact_expected_raw_sha256':hashlib.sha256(expected).hexdigest(),'actual_raw_sha256':hashlib.sha256(got).hexdigest()})
base=ROOT/'.planning/2026-10-09-floor2-guide-restoration/before-2f-entry-guide.unity';scene=ROOT/'Assets/ChooGuard/Scenes/FpsStation.unity'
before,preamble,bd,order=h.load_docs(base);after,apreamble,ad,aorder=h.load_docs(scene);bs=h.Scene(bd);cs=h.Scene(ad);allowed={};motions=[]
chk('baseline_sha',h.sha(before)==packet['scene_sha256'],h.sha(before));chk('saved_scene_sha',h.sha(after)==receipt['scene_sha256'],h.sha(after));chk('identity_order',order==aorder and preamble==apreamble,len(order))
for r in packet['mesh_patches']:
 target=ROOT/(replacements[r['source_asset']]+'.meta');guid=re.search(r'^guid: (\w+)$',target.read_text(),re.M)[1]
 for owner,typ in [(x,33) for x in r['owners']]+[(x,64) for x in r['collider_owners']]:
  cid=re.search(r'-(\d+)-0$',owner['global_object_id'])[1];old=bd[cid]['data']['m_Mesh'];new=ad[cid]['data']['m_Mesh'];expected=dict(old);expected['guid']=guid
  chk('owner_'+cid,bd[cid]['type']==typ and bs.component_path(cid)==owner['path'] and old['guid']==r['source_asset_guid'] and new==expected,owner['path']);allowed[cid]=['m_Mesh']
for row in packet['movable_transforms']:
 tid=str(row['source_transform_id']);old,oq=bs.world(tid);new,nq=cs.world(tid);expected=old[:3,3]+np.array(row['world_delta']);err=float(np.max(np.abs(new[:3,3]-expected)))
 chk('motion_'+tid,err<2e-5 and np.max(np.abs(oq-nq))<2e-6,{'path':bs.path(tid),'position_error_m':err});allowed[tid]=['m_LocalPosition'];motions.append({'transform_id':tid,'path':bs.path(tid),'position_error_m':err})
changed=[];unexpected=[]
for cid,b in bd.items():
 a=ad[cid]
 if b['raw']==a['raw']:continue
 fields=allowed.get(cid,[]);ok=bool(fields) and h.strip_fields(b['raw'],fields)==h.strip_fields(a['raw'],fields)
 row={'id':cid,'type':b['type'],'fields':fields,'only_authorized_fields':ok};changed.append(row)
 if not ok:unexpected.append(row)
chk('only_declared_scene_documents',not unexpected,unexpected)
protected={}
for name,predicate in {
 'station_doors':lambda d:d['type']==114 and 'StationDoor' in d['data'].get('m_EditorClassIdentifier',''),
 'source_identity':lambda d:d['type']==114 and 'SourcePartIdentity' in d['data'].get('m_EditorClassIdentifier',''),
 'prefab_instances':lambda d:d['type']==1001,
}.items():
 ids=[k for k,d in bd.items() if predicate(d)];changed_ids=[k for k in ids if bd[k]['raw']!=ad[k]['raw']];protected[name]={'count':len(ids),'changed_ids':changed_ids};chk('preserve_'+name,not changed_ids,protected[name])
result={'status':'pass' if all(c['pass'] for c in checks) else 'blocking','packet':str(packet_path.relative_to(ROOT)),'packet_sha256':sha(packet_path),'baseline_sha256':h.sha(before),'scene_sha256':h.sha(after),'document_count':len(order),'changed_document_count':len(changed),'changed_document_types':dict(collections.Counter(str(c['type']) for c in changed)),'unexpected_scene_changes':unexpected,'protected':protected,'meshes':geo,'motions':motions,'checks':checks,'limits':'Exact saved artifacts and declared relocation are verified. Guide registration/field dimensions are not established by this audit; no unseen interior proof.'}
(OUT/'four-bay-saved-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:result[k] for k in ['status','scene_sha256','document_count','changed_document_count','changed_document_types','unexpected_scene_changes','protected']},ensure_ascii=False,indent=2))
print('FAILED CHECKS',json.dumps([c for c in checks if not c['pass']],ensure_ascii=False))
