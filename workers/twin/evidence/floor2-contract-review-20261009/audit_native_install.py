#!/usr/bin/env python3
"""Read-only saved native portal audit; only this lane's evidence is written."""
from pathlib import Path
import importlib.util, collections, hashlib, json, re
import numpy as np
import yaml
ROOT=Path.cwd();OUT=ROOT/'workers/twin/evidence/floor2-contract-review-20261009'
spec=importlib.util.spec_from_file_location('scene_helper',ROOT/'workers/twin/evidence/floor1-saved-scene-audit-20261009/audit_scene.py');h=importlib.util.module_from_spec(spec);spec.loader.exec_module(h)
pp=ROOT/'workers/twin/output/floor2-platform-passages-20261009/main-up-current-source-openings.json';rp=ROOT/'.planning/2026-10-09-floor2-guide-restoration/2f-passages-installation.json'
p=json.loads(pp.read_text());receipt=json.loads(rp.read_text());checks=[]
def chk(name,passed,detail):checks.append({'id':name,'pass':bool(passed),'detail':detail})
def sha(f):return hashlib.sha256(Path(f).read_bytes()).hexdigest()
def mesh(f):return yaml.load(Path(f).read_text().split('Mesh:\n',1)[1],Loader=h.LOADER)
def data(a):
 v=a['m_VertexData'];raw=bytes.fromhex(v['_typelessdata']);nv=v['m_VertexCount'];ch=v['m_Channels'];stride=max(c['offset']+c['dimension']*4 for c in ch if c['dimension']);assert all(c['stream']==0 and c['format']==0 for c in ch if c['dimension']);assert len(raw)==stride*nv
 pos=np.ndarray((nv,3),dtype='<f4',buffer=raw,offset=ch[0]['offset'],strides=(stride,4)).astype(float)
 ix=np.frombuffer(bytes.fromhex(a['m_IndexBuffer']),'<u4' if a['m_IndexFormat'] else '<u2')
 sub=[ix[s['firstByte']//(4 if a['m_IndexFormat'] else 2):s['firstByte']//(4 if a['m_IndexFormat'] else 2)+s['indexCount']].astype(np.int64)+s['baseVertex'] for s in a['m_SubMeshes']]
 return raw,pos,sub
chk('packet_receipt_hash',sha(pp)==receipt['packet_sha256'],sha(pp));chk('owner_row_order',[m['source_asset'] for m in receipt['meshes']]==[m['source_asset'] for m in p['mesh_patches']],len(receipt['meshes']))
meshes=[]
for i,(row,saved) in enumerate(zip(p['mesh_patches'],receipt['meshes'])):
 a=mesh(row['source_asset']);b=mesh(saved['replacement_asset']);ar,ap,ai=data(a);br,bp,bi=data(b);exp=row['independent_clip_expectation'];cut_subs={f['submesh'] for f in exp['hit_triangles']};hit={(f['submesh'],f['submesh_triangle_ordinal']) for f in exp['hit_triangles']}
 chk('source_'+str(i),sha(row['source_asset'])==row['source_file_sha256'],row['source_asset'])
 chk('prefix_'+str(i),ar==br[:len(ar)] and a['m_VertexData']['m_Channels']==b['m_VertexData']['m_Channels'],'Every original vertex channel retained byte-for-byte')
 chk('format_'+str(i),a['m_IndexFormat']==b['m_IndexFormat'] and len(ai)==len(bi),'Index format/submesh count retained')
 total_kept=0;missing=[];unexpected_original=[]
 for s,(old,new) in enumerate(zip(ai,bi)):
  if s not in cut_subs:
   chk('submesh_'+str(i)+'_'+str(s),np.array_equal(old,new) and {k:v for k,v in a['m_SubMeshes'][s].items() if k!='firstByte'}=={k:v for k,v in b['m_SubMeshes'][s].items() if k!='firstByte'},'Untouched logical indices and stored descriptor retained; buffer byte offsets may shift after preceding clipped submesh')
  if a['m_SubMeshes'][s]['topology']!=0:continue
  oldtri=old.reshape(-1,3);newtri=new.reshape(-1,3);newcounts=collections.Counter(map(tuple,newtri.tolist()));expected=collections.Counter(tuple(t) for ordinal,t in enumerate(oldtri.tolist()) if (s,ordinal) not in hit)
  for tri,count in expected.items():
   if newcounts[tri]<count:missing.append({'submesh':s,'triangle':tri,'expected':count,'actual':newcounts[tri]})
  # Old cut faces must be removed; replacement fragments refer to appended vertices.
  for ordinal,t in enumerate(oldtri.tolist()):
   if (s,ordinal) in hit and newcounts[tuple(t)]:unexpected_original.append({'submesh':s,'old_hit_ordinal':ordinal,'triangle':t})
  total_kept+=sum(expected.values())
 chk('all_other_triangles_'+str(i),not missing and total_kept==exp['untouched_original_triangles'],{'untouched_count':total_kept,'missing':missing})
 chk('removed_hits_'+str(i),not unexpected_original,unexpected_original)
 report=saved['report'];chk('sealed_report_'+str(i),report['checks']['pass'] and report['checks']['cutTriangles']==len(hit) and abs(report['checks']['areaRemovedStats']-exp['removed_area_m2'])<.000051,report['checks'])
 M=np.array((row['owners']+row['collider_owners'])[0]['world_matrix']);world=bp@M[:3,:3].T+M[:3,3];sn=np.sin(np.deg2rad(16.2));cs=np.cos(np.deg2rad(16.2));q=np.c_[sn*world[:,0]+cs*world[:,2],cs*world[:,0]-sn*world[:,2],world[:,1]]
 # Independently guard every newly created triangle against a centroid inside a cut box.
 inside=0;inverted=0;degenerate=0
 for s,new in enumerate(bi):
  if b['m_SubMeshes'][s]['topology']!=0:continue
  for t in new.reshape(-1,3):
   if np.all(t<len(ap)):continue
   pts=q[t];cross=np.cross(pts[1]-pts[0],pts[2]-pts[0]);area=np.linalg.norm(cross)/2
   if area<1e-10:degenerate+=1
   cen=pts.mean(axis=0)
   for opening in row['openings']:
    box=opening['box'];start=np.array(box['p0']);end=np.array(box['p1']);axis=(end-start)/np.linalg.norm(end-start);normal=np.array([-axis[1],axis[0]]);along=(cen[:2]-start)@axis;across=(cen[:2]-start)@normal
    depth=min(along,np.linalg.norm(end-start)-along,box['half']-abs(across),cen[2]-box['y'][0],box['y'][1]-cen[2])
    if depth>1e-4:inside+=1
 chk('new_triangle_outside_portal_'+str(i),inside==0 and degenerate==0,{'inside':inside,'degenerate':degenerate})
 meshes.append({'source':row['source_asset'],'replacement':saved['replacement_asset'],'source_vertices':len(ap),'saved_vertices':len(bp),'untouched_triangles':total_kept,'hit_triangles':len(hit),'source_vertex_bytes_sha256':hashlib.sha256(ar).hexdigest(),'replacement_original_prefix_sha256':hashlib.sha256(br[:len(ar)]).hexdigest()})
base=ROOT/'.planning/2026-10-09-floor2-guide-restoration/before-2f-passages-guide.unity';scene=ROOT/'Assets/ChooGuard/Scenes/FpsStation.unity';before,pre,bd,order=h.load_docs(base);after,apost,ad,aorder=h.load_docs(scene);bs=h.Scene(bd);allowed={};refs=[]
chk('before_sha',h.sha(before)==p['scene_sha256'],h.sha(before));chk('saved_sha',h.sha(after)==receipt['scene_sha256'],h.sha(after));chk('scene_identity',pre==apost and order==aorder,len(order))
for row,saved in zip(p['mesh_patches'],receipt['meshes']):
 guid=re.search(r'^guid: (\w+)$',Path(saved['replacement_asset']+'.meta').read_text(),re.M)[1]
 for proof,typ in [(x,33) for x in row['owners']]+[(x,64) for x in row['collider_owners']]:
  cid=re.search(r'-(\d+)-0$',proof['global_object_id'])[1];old=bd[cid]['data']['m_Mesh'];expected=dict(old);expected['guid']=guid
  chk('scene_owner_'+cid,bd[cid]['type']==typ and bs.component_path(cid)==proof['path'] and old['guid']==row['source_asset_guid'] and ad[cid]['data']['m_Mesh']==expected,proof['path']);allowed[cid]=['m_Mesh'];refs.append(cid)
changed=[];unexpected=[]
for cid,old in bd.items():
 new=ad[cid]
 if old['raw']==new['raw']:continue
 fields=allowed.get(cid,[]);ok=bool(fields) and h.strip_fields(old['raw'],fields)==h.strip_fields(new['raw'],fields);row={'id':cid,'type':old['type'],'only_authorized_mesh_ref':ok};changed.append(row)
 if not ok:unexpected.append(row)
chk('scene_only_reviewed_references',not unexpected and set(refs)=={c['id'] for c in changed},unexpected)
result={'status':'pass' if all(c['pass'] for c in checks) else 'blocking','packet':str(pp.relative_to(ROOT)),'packet_sha256':sha(pp),'baseline_sha256':h.sha(before),'scene_sha256':h.sha(after),'document_count':len(order),'changed_documents':changed,'unexpected_changes':unexpected,'meshes':meshes,'checks':checks,'limits':'Guide-local bounded portal cuts and preservation verified. Scene field metric accuracy, all-floor native platform topology, and unseen interiors are not established.'}
(OUT/'native-saved-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:result[k] for k in ['status','scene_sha256','document_count','changed_documents','unexpected_changes']},ensure_ascii=False,indent=2));print('FAILED CHECKS',json.dumps([c for c in checks if not c['pass']],ensure_ascii=False))
