#!/usr/bin/env python3
"""Read-only exact YAML preservation audit; output stays in this evidence folder."""
from pathlib import Path
import argparse, collections, hashlib, json, re
import numpy as np
import yaml

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parent
WORK = ROOT / '.planning/2026-10-08-interior-runtime-completion/floor-guide-layout'
BASE = WORK / 'before-floor1-guide.unity'
SCENE = ROOT / 'Assets/ChooGuard/Scenes/FpsStation.unity'
LOADER = getattr(yaml, 'CSafeLoader', yaml.SafeLoader)
HEADER = re.compile(r'^--- !u!(\d+) &(\d+)(?: stripped)?\n', re.M)

def sha(data):
    return hashlib.sha256(data).hexdigest()

def load_docs(path):
    blob = path.read_bytes(); text = blob.decode('utf-8'); heads = list(HEADER.finditer(text))
    docs = {}
    for i, h in enumerate(heads):
        raw = text[h.start():heads[i+1].start() if i+1 < len(heads) else len(text)]
        body = yaml.load(raw[h.end()-h.start():], Loader=LOADER)
        docs[h[2]] = {'id': h[2], 'type': int(h[1]), 'raw': raw, 'data': next(iter(body.values()))}
    return blob, text[:heads[0].start()], docs, [(h[2], int(h[1])) for h in heads]

def ref_id(ref):
    return str(ref.get('fileID', 0))

def vec(row, keys):
    return np.array([row[k] for k in keys], dtype=float)

def rotation(q):
    x,y,z,w = q / np.linalg.norm(q)
    return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                     [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                     [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])

def quat_multiply(a,b):
    x,y,z,w=a; X,Y,Z,W=b
    return np.array([w*X+x*W+y*Z-z*Y,w*Y-x*Z+y*W+z*X,w*Z+x*Y-y*X+z*W,w*W-x*X-y*Y-z*Z])

class Scene:
    def __init__(self, docs):
        self.docs=docs; self.prefabs={}; self.guid_paths={}; self.world_cache={}; self.path_cache={}
        self.transform_for_go={ref_id(d['data']['m_GameObject']): k for k,d in docs.items() if d['type']==4 and 'm_GameObject' in d['data']}
        self.components_for_go=collections.defaultdict(list)
        for k,d in docs.items():
            if 'm_GameObject' in d['data']: self.components_for_go[ref_id(d['data']['m_GameObject'])].append(k)
    def prefab(self,guid):
        if guid not in self.prefabs:
            if not self.guid_paths:
                for p in (ROOT/'Assets').rglob('*.prefab.meta'):
                    m=re.search(r'^guid: (\w+)$',p.read_text(),re.M)
                    if m:self.guid_paths[m[1]]=Path(str(p)[:-5])
            _,_,d,_=load_docs(self.guid_paths[guid]);self.prefabs[guid]=d
        return self.prefabs[guid]
    def effective(self,tid):
        d=self.docs[tid]['data']
        if 'm_LocalPosition' in d:return d,None
        source=d['m_CorrespondingSourceObject']; pid=ref_id(d['m_PrefabInstance']); sd=dict(self.prefab(source['guid'])[ref_id(source)]['data'])
        sd={k:dict(v) if isinstance(v,dict) else v for k,v in sd.items()}
        for m in self.docs[pid]['data']['m_Modification']['m_Modifications']:
            if m['target']!=source:continue
            prop=m['propertyPath'];parts=prop.split('.')
            if len(parts)==2 and parts[0] in ['m_LocalPosition','m_LocalRotation','m_LocalScale']:sd[parts[0]][parts[1]]=float(m['value'])
        sd['m_Father']=self.docs[pid]['data']['m_Modification']['m_TransformParent']
        return sd,(pid,source)
    def path(self,tid):
        if tid=='0':return ''
        if tid in self.path_cache:return self.path_cache[tid]
        d,pref=self.effective(tid)
        if pref:
            pid,source=pref; go=self.prefab(source['guid'])[ref_id(d['m_GameObject'])]['data']; name=go['m_Name']
            for m in self.docs[pid]['data']['m_Modification']['m_Modifications']:
                if ref_id(m['target'])==ref_id(d['m_GameObject']) and m['propertyPath']=='m_Name':name=m['value']
        else:name=self.docs[ref_id(d['m_GameObject'])]['data']['m_Name']
        parent=self.path(ref_id(d['m_Father'])); result=(parent+'/' if parent else '')+name
        self.path_cache[tid]=result;return result
    def component_path(self,cid):
        return self.path(self.transform_for_go[ref_id(self.docs[cid]['data']['m_GameObject'])])
    def world(self,tid):
        if tid=='0':return np.eye(4),np.array([0.,0.,0.,1.])
        if tid in self.world_cache:return self.world_cache[tid]
        d,_=self.effective(tid); parent,pq=self.world(ref_id(d['m_Father'])); q=vec(d['m_LocalRotation'],'xyzw')
        local=np.eye(4);local[:3,:3]=rotation(q) @ np.diag(vec(d['m_LocalScale'],'xyz'));local[:3,3]=vec(d['m_LocalPosition'],'xyz')
        value=(parent@local,quat_multiply(pq,q));self.world_cache[tid]=value;return value

def strip_fields(raw,fields):
    return ''.join(line for line in raw.splitlines(keepends=True) if not any(line.startswith('  '+f+':') for f in fields))

def strip_overrides(raw,source,fields):
    # Preserve prefix/suffix and every non-authorized modification chunk byte for byte.
    pattern=re.compile(r'^    - target: .*?(?=^    - target:|^    m_RemovedComponents:)', re.M|re.S)
    def retain(m):
        chunk=m[0]; entries=yaml.load(chunk,Loader=LOADER);row=entries[0]
        if row['target']==source and row['propertyPath'] in fields and ref_id(row.get('objectReference',{}))=='0':return ''
        return chunk
    return pattern.sub(retain,raw)

def run(final_sha):
    before,preamble,bd,order=load_docs(BASE);after,apreamble,ad,aorder=load_docs(SCENE)
    bs,cs=Scene(bd),Scene(ad); issues=[]; allowed={}; packets=[]; mesh_state={k:d['data'].get('m_Mesh') for k,d in bd.items() if d['type'] in (33,64)}; mesh_chain=collections.defaultdict(list); motions=[]
    path_to_tid=collections.defaultdict(list)
    for tid,d in bd.items():
        if d['type']==4:
            try:path_to_tid[bs.path(tid)].append(tid)
            except (KeyError,TypeError):pass
    for name in ['floor1','unit16','unit4','unit8']:
        ppath=WORK/(name+'-packet.json');ipath=WORK/(name+'-installation.json')
        if not ppath.exists() or not ipath.exists():
            if final_sha:issues.append({'reason':'missing_final_packet_or_installation','packet':name})
            continue
        packet=json.loads(ppath.read_text());receipt=json.loads(ipath.read_text());ph=sha(ppath.read_bytes())
        if receipt['packet_sha256']!=ph:issues.append({'reason':'installation_packet_hash_mismatch','packet':name})
        if receipt['before_scene_sha256']!=packet['scene_sha256']:issues.append({'reason':'packet_stage_before_hash_mismatch','packet':name})
        packets.append({'batch':name,'path':str(ppath.relative_to(ROOT)),'sha256':ph,'installation_sha256':sha(ipath.read_bytes()),'stage_before_scene_sha256':packet['scene_sha256'],'stage_after_scene_sha256':receipt['scene_sha256'],'mesh_patches':len(packet['mesh_patches']),'declared_motions':len(packet['movable_transforms'])})
        replacements={m['source_asset']:m['replacement_asset'] for m in receipt['meshes']}
        if set(replacements)!={p['source_asset'] for p in packet['mesh_patches']}:issues.append({'reason':'installation_mesh_source_set_mismatch','packet':name})
        for index,p in enumerate(packet['mesh_patches']):
            target=replacements[p['source_asset']];meta=ROOT/(target+'.meta');guid=re.search(r'^guid: (\w+)$',meta.read_text(),re.M)[1]
            owners=[(o,33) for o in p['owners']]+[(o,64) for o in p.get('collider_owners',[])]
            narrow=[]
            for owner,typ in owners:
                cid=re.search(r'-(\d+)-0$',owner['global_object_id'])[1]
                if bd[cid]['type']!=typ:issues.append({'reason':'owner_component_type_mismatch','id':cid,'packet':name})
                path=bs.component_path(cid)
                if path!=owner['path']:issues.append({'reason':'owner_path_mismatch','id':cid,'declared':owner['path'],'resolved':path})
                narrow.append((cid,'explicit_MF' if typ==33 else 'explicit_MC'))
                if typ==33:
                    go=ref_id(bd[cid]['data']['m_GameObject'])
                    for mcid in bs.components_for_go[go]:
                        if bd[mcid]['type']==64 and mesh_state[mcid] and mesh_state[mcid].get('guid')==p['source_asset_guid']:narrow.append((mcid,'same_exact_MF_GameObject_and_source_MC'))
            by_component=collections.defaultdict(set)
            for cid,basis in narrow:by_component[cid].add(basis)
            for cid,bases in by_component.items():
                current=mesh_state[cid]
                if current.get('guid')!=p['source_asset_guid']:issues.append({'reason':'stage_source_mesh_ref_mismatch','id':cid,'packet':name,'current':current,'source_guid':p['source_asset_guid']})
                replacement={'fileID':current['fileID'],'guid':guid,'type':current['type']};mesh_state[cid]=replacement
                allowed.setdefault(cid,{'fields':['m_Mesh'],'packets':[],'path':bs.component_path(cid),'basis':[]})
                allowed[cid]['packets'].append(name);allowed[cid]['basis'].extend(sorted(bases))
                mesh_chain[cid].append({'batch':name,'patch_index':index,'source_asset':p['source_asset'],'replacement_asset':target,'expected_ref':replacement})
        for index,m in enumerate(packet['movable_transforms']):
            if m['kind']=='door':
                doors=[k for k,d in bd.items() if d['type']==114 and d['data'].get('Element')==m['door_element'] and 'StationDoor' in d['data'].get('m_EditorClassIdentifier','')]
                tids=[bs.transform_for_go[ref_id(bd[k]['data']['m_GameObject'])] for k in doors]
            else:tids=path_to_tid[m['path']]
            if len(tids)!=1:issues.append({'reason':'motion_root_not_unique','packet':name,'motion':m,'resolved':tids});continue
            tid=tids[0];fields=['m_LocalPosition']+(['m_LocalRotation'] if 'world_rigid_delta' in m else [])
            root,pref=bs.effective(tid); aid=pref[0] if pref else tid
            entry=allowed.setdefault(aid,{'fields':fields,'packets':[],'path':bs.path(tid),'basis':['exact_declared_motion_root']})
            entry['fields']=sorted(set(entry['fields'])|set(fields));entry['packets'].append(name)
            if pref:entry['prefab_source_root']=pref[1];entry['root_transform_id']=tid
            motions.append({'batch':name,'motion_index':index,'root_transform_id':tid,'changed_document_id':aid,'path':bs.path(tid),'declaration':m})
    for i,p in enumerate(packets):
        expected_before=sha(before) if i==0 else packets[i-1]['stage_after_scene_sha256']
        if p['stage_before_scene_sha256']!=expected_before:issues.append({'reason':'packet_scene_chain_discontinuous','batch':p['batch'],'expected_before':expected_before,'actual_before':p['stage_before_scene_sha256']})
    if packets and packets[-1]['stage_after_scene_sha256']!=sha(after):issues.append({'reason':'last_installation_scene_hash_mismatch','expected':packets[-1]['stage_after_scene_sha256'],'actual':sha(after)})
    motion_groups=collections.defaultdict(list)
    for m in motions:motion_groups[m['root_transform_id']].append(m)
    residuals=[]
    for tid,group in motion_groups.items():
        old,oq=bs.world(tid);expected=old[:3,3].copy();q=oq.copy()
        for row in group:
            m=row['declaration']
            if 'world_rigid_delta' in m:
                rigid=m['world_rigid_delta'];r=np.array(rigid['rotation']);expected=rotation(r)@expected+np.array(rigid['translation']);q=quat_multiply(r,q)
            else:expected+=np.array(m['world_delta'])
        actual,aq=cs.world(tid);pos=float(np.max(np.abs(actual[:3,3]-expected)));rot=float(min(np.max(np.abs(aq-q)),np.max(np.abs(aq+q))))
        residuals.append({'root_transform_id':tid,'world_position_max_abs_m':pos,'world_rotation_quaternion_max_abs':rot})
        if pos>2e-5 or rot>2e-6:issues.append({'reason':'declared_world_motion_mismatch',**residuals[-1]})
    changed=[];unexpected=[]
    if preamble!=apreamble:unexpected.append({'reason':'YAML_preamble_changed'})
    if order!=aorder:unexpected.append({'reason':'document_ID_type_order_changed'})
    for cid in sorted(set(bd)&set(ad),key=int):
        b,a=bd[cid],ad[cid]
        if b['raw']==a['raw']:continue
        authorization=allowed.get(cid);ok=False
        if authorization:
            if b['type'] in (4,33,64):ok=strip_fields(b['raw'],authorization['fields'])==strip_fields(a['raw'],authorization['fields'])
            elif b['type']==1001:
                fields={f+'.'+v for f in authorization['fields'] for v in ('xyzw' if f=='m_LocalRotation' else 'xyz')};source=authorization['prefab_source_root']
                ok=strip_overrides(b['raw'],source,fields)==strip_overrides(a['raw'],source,fields)
        actual_fields=[k for k in set(b['data'])|set(a['data']) if b['data'].get(k)!=a['data'].get(k)]
        row={'id':cid,'type':a['type'],'path':authorization['path'] if authorization else (bs.component_path(cid) if 'm_GameObject' in b['data'] else None),'changed_fields':sorted(actual_fields),'only_authorized_fields':ok,'packets':sorted(set(authorization['packets'])) if authorization else [],'before_sha256':sha(b['raw'].encode()),'after_sha256':sha(a['raw'].encode())}
        if cid in mesh_chain:row['mesh_ref_chain']=mesh_chain[cid]
        if authorization:row['authorization_basis']=sorted(set(authorization['basis']))
        if authorization and 'prefab_source_root' in authorization:row['prefab_source_root']=authorization['prefab_source_root'];row['root_transform_id']=authorization['root_transform_id']
        changed.append(row)
        if not ok:unexpected.append(row)
    for cid,expected in mesh_state.items():
        if cid in allowed and ad[cid]['data']['m_Mesh']!=expected:issues.append({'reason':'final_mesh_ref_mismatch','id':cid,'expected':expected,'actual':ad[cid]['data']['m_Mesh']})
    doors=[k for k,d in bd.items() if d['type']==114 and 'StationDoor' in d['data'].get('m_EditorClassIdentifier','')]
    source_ids=[k for k,d in bd.items() if d['type']==114 and 'SourcePartIdentity' in d['data'].get('m_EditorClassIdentifier','')]
    def unchanged_proof(ids):
        ids=sorted(set(ids),key=int);bad=[k for k in ids if k not in ad or bd[k]['raw']!=ad[k]['raw']]
        return {'document_count':len(ids),'changed_document_ids':bad,'all_byte_identical':not bad,'baseline_content_sha256':sha(''.join(bd[k]['raw'] for k in ids).encode()),'saved_content_sha256':sha(''.join(ad[k]['raw'] for k in ids if k in ad).encode())}
    native={}
    for needle in ['FPSWorld','MainShell','Ground_Floor','ContextPlaza']:
        tids=[tid for path,ts in path_to_tid.items() if path.split('/')[-1]==needle for tid in ts]
        paths=[bs.path(tid) for tid in tids];gos=set()
        for path,ts in path_to_tid.items():
            if any(path==p or path.startswith(p+'/') for p in paths):
                for tid in ts:
                    if 'm_GameObject' in bd[tid]['data']:gos.add(ref_id(bd[tid]['data']['m_GameObject']))
        ids=gos|{k for k,d in bd.items() if ref_id(d['data'].get('m_GameObject',{})) in gos}
        native[needle]={'root_transform_ids':tids,'resolved_paths':paths,**unchanged_proof(ids)}
    door_child_ids=set()
    for door in doors:
        tid=bs.transform_for_go[ref_id(bd[door]['data']['m_GameObject'])];prefix=bs.path(tid)+'/'
        # Door names repeat, so traverse exact child IDs rather than match name paths.
        stack=[ref_id(x) for x in bd[tid]['data'].get('m_Children',[])];seen=set()
        while stack:
            child=stack.pop()
            if child in seen:continue
            seen.add(child);d=bd[child]['data'];go=ref_id(d['m_GameObject']);door_child_ids.add(go);door_child_ids.update(bs.components_for_go[go]);stack.extend(ref_id(x) for x in d.get('m_Children',[]))
    local_scales=[k for k,d in bd.items() if d['type']==4 and 'm_LocalScale' in d['data']]
    scale_changed=[k for k in local_scales if bd[k]['data']['m_LocalScale']!=ad[k]['data']['m_LocalScale']]
    native['StationDoor_components_all_local_fields']=unchanged_proof(doors)
    native['StationDoor_leaf_meshes_child_transforms_and_pivots']=unchanged_proof(door_child_ids)
    native['SourcePartIdentity_components_all_source_ID_fields']=unchanged_proof(source_ids)
    native['all_direct_Transform_scales']={'document_count':len(local_scales),'changed_document_ids':scale_changed,'all_exact_equal':not scale_changed}
    native['all_other_YAML_documents']=unchanged_proof(set(bd)-{x['id'] for x in changed})
    native['all_PrefabInstance_documents']=unchanged_proof(k for k,d in bd.items() if d['type']==1001)
    native['Room_ToiletRoom_core_face_scope']={'representation':'Canonical Room and ToiletRoom elements are combined mesh contributors, not scene roots with those literal names. Scene YAML references are attributed exactly; preserved contributor faces require the independent geometry review.','geometry_review':'workers/twin/evidence/floor2-architecture-next-20261009/review-floor1-stage/unit8-review/README.md','geometry_review_sha256':sha((ROOT/'workers/twin/evidence/floor2-architecture-next-20261009/review-floor1-stage/unit8-review/README.md').read_bytes())}
    for k,v in native.items():
        if v.get('changed_document_ids'):issues.append({'reason':'protected_native_content_changed','group':k,'ids':v['changed_document_ids']})
    if final_sha and sha(after)!=final_sha:issues.append({'reason':'root_final_SHA_mismatch','expected':final_sha,'actual':sha(after)})
    result={'schema':'chooguard.floor1-saved-scene-preservation-audit.v1','sealed_final':bool(final_sha),'status':'pass' if not issues and not unexpected else 'blocking','baseline':{'path':str(BASE.relative_to(ROOT)),'sha256':sha(before),'documents':len(bd)},'saved_scene':{'path':str(SCENE.relative_to(ROOT)),'sha256':sha(after),'documents':len(ad)},'root_final_sha256':final_sha,'packet_chain':packets,'document_identity':{'ID_type_set_equal':set(order)==set(aorder),'ID_type_order_equal':order==aorder,'new_IDs':sorted(set(ad)-set(bd),key=int),'missing_IDs':sorted(set(bd)-set(ad),key=int)},'changed_count':len(changed),'changed_by_component_type':dict(collections.Counter(str(x['type']) for x in changed)),'unexpected_chunks_count':len(unexpected),'unexpected_chunks':unexpected,'blocking_deviations':issues,'authorized_component_count':len(allowed),'motion_root_count':len(motion_groups),'world_motion_verification':{'position_tolerance_m':2e-5,'rotation_quaternion_tolerance':2e-6,'max_world_position_residual_m':max((r['world_position_max_abs_m'] for r in residuals),default=0),'max_world_rotation_residual':max((r['world_rotation_quaternion_max_abs'] for r in residuals),default=0)},'preserved_native_proof':native,'changed':changed,'declared_motion_root_attribution':motions,'scope':'Saved-scene YAML/component content and declared mesh references; geometry channels are covered by independent packet geometry QA.'}
    destination=OUT/('saved-scene-audit.json' if final_sha else 'pre-unit8-scene-audit.json');destination.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({k:result[k] for k in ['sealed_final','status','saved_scene','changed_count','changed_by_component_type','unexpected_chunks_count','blocking_deviations','world_motion_verification']},ensure_ascii=False,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--final-sha');args=parser.parse_args();run(args.final_sha)
