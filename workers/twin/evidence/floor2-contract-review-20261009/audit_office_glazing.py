#!/usr/bin/env python3
"""Independent read-only office surface-transfer and cumulative 2F preservation audit."""
from pathlib import Path
import collections,hashlib,importlib.util,json,re
import numpy as np
import yaml
ROOT=Path.cwd();OUT=ROOT/'workers/twin/evidence/floor2-contract-review-20261009'
sp=importlib.util.spec_from_file_location('scene_helper',ROOT/'workers/twin/evidence/floor1-saved-scene-audit-20261009/audit_scene.py');h=importlib.util.module_from_spec(sp);sp.loader.exec_module(h)
pp=ROOT/'workers/twin/output/floor2-main-restoration-20261009/office-glazing-surface-proposal.json';rp=ROOT/'.planning/2026-10-09-floor2-guide-restoration/2f-office-installation.json';p=json.loads(pp.read_text());checks=[]
def sha(x):return hashlib.sha256(Path(x).read_bytes()).hexdigest()
def chk(name,passed,detail):checks.append({'id':name,'pass':bool(passed),'detail':detail})
def mesh(path):return yaml.load(Path(path).read_text().split('Mesh:\n',1)[1],Loader=h.LOADER)
def raw(a):return bytes.fromhex(a['m_VertexData']['_typelessdata'])
def indices(a):return np.frombuffer(bytes.fromhex(a['m_IndexBuffer']),'<u4' if a['m_IndexFormat'] else '<u2').reshape(-1,3)
a=mesh(p['source_asset']);ar=raw(a);ai=indices(a);wanted=set(p['selected_triangle_ordinals']);selection=np.array(sorted(wanted));retained=np.array([i for i in range(len(ai)) if i not in wanted]);source_geo={'source_vertices':a['m_VertexData']['m_VertexCount'],'source_triangles':len(ai),'retained_triangles':len(retained),'glazing_triangles':len(selection)}
chk('source_file_sha',sha(p['source_asset'])==p['source_file_sha256'],p['source_asset']);chk('source_full_channels',hashlib.sha256(ar).hexdigest()==p['source_full_vertex_channel_sha256'] and a['m_VertexData']['m_Channels']==p['source_channels'],hashlib.sha256(ar).hexdigest());chk('source_indices',hashlib.sha256(ai.astype('<u4').tobytes()).hexdigest()==p['source_all_indices_sha256'],len(ai));chk('selected_faces',ai[selection].tolist()==p['selected_source_triangles'] and hashlib.sha256(ai[selection].astype('<u4').tobytes()).hexdigest()==p['selected_index_bytes_sha256'],selection.tolist());chk('retained_faces',hashlib.sha256(ai[retained].astype('<u4').tobytes()).hexdigest()==p['retained_opaque_index_sequence_sha256'],len(retained));protected=[i for group in p['protected_opaque_faces'].values() for i in group];chk('protected_headers_and_jambs',not set(protected)&wanted,protected)
matp=p['glass_material'];matbody=Path(matp['asset']).read_text();mat=yaml.load(matbody.split('Material:\n',1)[1].split('\n--- !u!',1)[0],Loader=h.LOADER);props=mat['m_SavedProperties'];floats={k:v for d in props['m_Floats'] for k,v in d.items()};colors={k:v for d in props['m_Colors'] for k,v in d.items()};shader_guid=mat['m_Shader']['guid'];shader_meta=list((ROOT/'Library/PackageCache').glob('com.unity.render-pipelines.universal*/Shaders/Lit.shader.meta'))
chk('material_identity',sha(matp['asset'])==matp['file_sha256'] and re.search(r'^guid: (\w+)$',Path(matp['asset']+'.meta').read_text(),re.M)[1]==matp['guid'],matp['asset']);chk('valid_transparent_urp_material',mat['m_CustomRenderQueue']==3000 and mat['stringTagMap'].get('RenderType')=='Transparent' and floats['_Surface']==1 and floats['_ZWrite']==0 and 0<colors['_BaseColor']['a']<1 and '_SURFACE_TYPE_TRANSPARENT' in mat['m_ValidKeywords'] and any('guid: '+shader_guid in f.read_text() for f in shader_meta),{'shader':shader_guid,'alpha':colors['_BaseColor']['a'],'src_blend':floats['_SrcBlend'],'dst_blend':floats['_DstBlend'],'queue':mat['m_CustomRenderQueue']})
for e in p['evidence']:chk('evidence_'+e['path'],sha(e['path'])==e['sha256'],e['path'])
preflight={'status':'pass' if all(c['pass'] for c in checks) else 'blocking','packet_sha256':sha(pp),'code_sha256':sha(ROOT/'AgentScripts/ObservedOfficeGlazingInstall.cs'),'source_geometry':source_geo,'checks':checks,'persistence_limit':'Saved registry pins retained source and companion mesh/material/frame. The opt-in saved child replay is separately reviewed and verified in office-replay-review.json; complete production root regeneration was not performed.'}
(OUT/'office-preflight-review.json').write_text(json.dumps(preflight,ensure_ascii=False,indent=2)+'\n')
if not rp.exists():
 print('OFFICE_PREFLIGHT',preflight['status'],'production receipt pending');raise SystemExit(0)
r=json.loads(rp.read_text());chk('receipt_packet_sha',r['packet_sha256']==sha(pp),sha(pp));outputs=[]
for role,key,expected in [('retained','retained_asset',ai[retained]),('glazing','glazing_asset',ai[selection])]:
 b=mesh(r[key]);chk(role+'_vertex_bytes',raw(b)==ar and b['m_VertexData']['m_Channels']==a['m_VertexData']['m_Channels'] and b['m_VertexData']['m_VertexCount']==a['m_VertexData']['m_VertexCount'],'Original vertex/channel bytes exact');chk(role+'_index_bytes',np.array_equal(indices(b),expected) and b['m_IndexFormat']==a['m_IndexFormat'] and len(b['m_SubMeshes'])==1,'Only reviewed triangle sequence assigned');outputs.append({'role':role,'asset':r[key],'vertices':b['m_VertexData']['m_VertexCount'],'triangles':len(expected),'original_vertex_bytes_sha256':hashlib.sha256(raw(b)).hexdigest()})
base=ROOT/'.planning/2026-10-09-floor2-guide-restoration/before-2f-office-guide.unity';scene=ROOT/'Assets/ChooGuard/Scenes/FpsStation.unity';before,pre,bd,order=h.load_docs(base);after,apost,ad,aorder=h.load_docs(scene);bs=h.Scene(bd);cs=h.Scene(ad)
chk('office_baseline_sha',h.sha(before)==p['scene_sha256'],h.sha(before));chk('office_saved_sha',h.sha(after)==r['scene_sha256'],h.sha(after));chk('existing_ids_order',pre==apost and [(k,t) for k,t in aorder if k in bd]==order,{'before':len(order),'after':len(aorder)})
cid=re.search(r'-(\d+)-0$',p['owner']['global_object_id'])[1];tid=bs.transform_for_go[h.ref_id(bd[cid]['data']['m_GameObject'])];target_guid=re.search(r'^guid: (\w+)$',Path(r['retained_asset']+'.meta').read_text(),re.M)[1];expected=dict(bd[cid]['data']['m_Mesh']);expected['guid']=target_guid
chk('target_reference',ad[cid]['data']['m_Mesh']==expected,p['owner']['path'])
new_ids=set(ad)-set(bd);new_tf=[k for k in new_ids if ad[k]['type']==4];new_filter=[k for k in new_ids if ad[k]['type']==33];new_renderer=[k for k in new_ids if ad[k]['type']==23];new_go=[k for k in new_ids if ad[k]['type']==1]
chk('new_node_only',len(new_ids)==4 and len(new_tf)==len(new_filter)==len(new_renderer)==len(new_go)==1,[{'id':k,'type':ad[k]['type']} for k in sorted(new_ids)])
if len(new_tf)==1:
 nt=new_tf[0];d=ad[nt]['data'];chk('companion_parent_identity',h.ref_id(d['m_Father'])==tid and d['m_LocalPosition']=={'x':0,'y':0,'z':0} and d['m_LocalRotation']=={'x':0,'y':0,'z':0,'w':1} and d['m_LocalScale']=={'x':1,'y':1,'z':1} and cs.path(nt)==r['companion'],d)
 chk('parent_children_append_only',ad[tid]['data']['m_Children']==bd[tid]['data']['m_Children']+[{'fileID':int(nt)}],ad[tid]['data']['m_Children'])
 if len(new_renderer)==1:
  rend=ad[new_renderer[0]]['data'];chk('companion_material_reference',rend['m_Materials']==[{'fileID':2100000,'guid':matp['guid'],'type':2}] and rend['m_Enabled']==1 and rend['m_CastShadows']==0,rend['m_Materials'])
 if len(new_filter)==1:
  gg=re.search(r'^guid: (\w+)$',Path(r['glazing_asset']+'.meta').read_text(),re.M)[1];chk('companion_mesh_reference',ad[new_filter[0]]['data']['m_Mesh']['guid']==gg,ad[new_filter[0]]['data']['m_Mesh'])
changed=[];unexpected=[]
for k,b in bd.items():
 n=ad[k]
 if b['raw']==n['raw']:continue
 if k==cid:ok=h.strip_fields(b['raw'],['m_Mesh'])==h.strip_fields(n['raw'],['m_Mesh'])
 elif k==tid:
  bd2=dict(b['data']);ad2=dict(n['data']);bd2.pop('m_Children');ad2.pop('m_Children');ok=bd2==ad2
 else:ok=False
 z={'id':k,'type':b['type'],'authorized':ok};changed.append(z)
 if not ok:unexpected.append(z)
chk('office_scene_changed_only_target_and_child_link',not unexpected and len(changed)==2,changed)
collider_ids=[k for k,d in bd.items() if d['type']==64];transform_ids=[k for k,d in bd.items() if d['type']==4];door_ids=[k for k,d in bd.items() if d['type']==114 and 'StationDoor' in d['data'].get('m_EditorClassIdentifier','')]
chk('every_previous_collider_exact',all(bd[k]['raw']==ad[k]['raw'] for k in collider_ids),len(collider_ids));chk('every_previous_door_exact',all(bd[k]['raw']==ad[k]['raw'] for k in door_ids),len(door_ids));chk('every_previous_transform_pose_exact',all(all(bd[k]['data'].get(f)==ad[k]['data'].get(f) for f in ['m_LocalPosition','m_LocalRotation','m_LocalScale','m_Father']) for k in transform_ids),len(transform_ids))
reg=json.loads((ROOT/'content/world/observed-interiors/structural-corrections.json').read_text());entries=[e for e in reg['entries'] if e['source_asset']==p['source_asset']];chk('registry_unique_source',len(entries)==1,len(entries))
if len(entries)==1:
 e=entries[0];co=e.get('companions',[]);chk('registry_material_frame_and_dependency',e['owner']==p['owner']['path'] and e['replacement_asset']==r['retained_asset'] and len(co)==1 and co[0]['owner']==r['companion'] and co[0]['asset']==r['glazing_asset'] and [{k:m[k] for k in ['asset','guid']} for m in co[0]['materials']]==[{'asset':matp['asset'],'guid':matp['guid']}] and e.get('source_complete_channels_sha256') is not None and e.get('replacement_complete_channels_sha256') is not None and co[0].get('complete_channels_sha256') is not None,co)
# Cumulative preservation from completed 1F baseline. Every earlier 2F correction is explicitly authorized.
fb=ROOT/'.planning/2026-10-09-floor2-guide-restoration/before-2f-entry-guide.unity';fbblob,fp,fbd,fo=h.load_docs(fb);authorized={cid,tid};cumulative_motions=set();prior_meshes=set()
for packet_name in ['workers/twin/output/floor2-main-restoration-20261009/four-bay-packet.json','workers/twin/output/floor2-platform-passages-20261009/main-up-current-source-openings.json']:
 prior=json.loads((ROOT/packet_name).read_text())
 for row in prior['mesh_patches']:
  for o in row['owners']+row['collider_owners']:prior_meshes.add(re.search(r'-(\d+)-0$',o['global_object_id'])[1])
 for m in prior.get('movable_transforms',[]):cumulative_motions.add(str(m['source_transform_id']))
authorized|=prior_meshes|cumulative_motions;cumulative_changes=[];cumulative_unexpected=[]
for k,b in fbd.items():
 n=ad[k]
 if b['raw']==n['raw']:continue
 if k in prior_meshes or k==cid:ok=h.strip_fields(b['raw'],['m_Mesh'])==h.strip_fields(n['raw'],['m_Mesh'])
 elif k in cumulative_motions:ok=h.strip_fields(b['raw'],['m_LocalPosition'])==h.strip_fields(n['raw'],['m_LocalPosition'])
 elif k==tid:
  o=dict(b['data']);v=dict(n['data']);o.pop('m_Children');v.pop('m_Children');ok=o==v
 else:ok=False
 q={'id':k,'type':b['type'],'only_authorized_2f_fields':ok};cumulative_changes.append(q)
 if not ok:cumulative_unexpected.append(q)
chk('completed_1f_baseline_preserved',not cumulative_unexpected and [(k,t) for k,t in aorder if k in fbd]==fo,{'baseline_sha256':h.sha(fbblob),'changed_existing_documents':len(cumulative_changes),'unexpected':cumulative_unexpected})
result={'status':'pass' if all(c['pass'] for c in checks) else 'blocking','packet_sha256':sha(pp),'office_baseline_sha256':h.sha(before),'completed_1f_baseline_sha256':h.sha(fbblob),'scene_sha256':h.sha(after),'existing_document_count':len(bd),'saved_document_count':len(ad),'office_existing_changed_documents':changed,'new_document_ids':sorted(new_ids),'cumulative_authorized_2f_existing_changes':cumulative_changes,'mesh_outputs':outputs,'checks':checks,'limits':preflight['persistence_limit']}
(OUT/'office-saved-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:result[k] for k in ['status','scene_sha256','existing_document_count','saved_document_count','office_existing_changed_documents','new_document_ids']},ensure_ascii=False,indent=2));print('FAILED CHECKS',json.dumps([c for c in checks if not c['pass']],ensure_ascii=False))
