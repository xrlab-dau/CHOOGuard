from pathlib import Path
import re,json,hashlib,struct,yaml,numpy as np
s=Path(__file__).with_name('audit_saved_scene.py').read_text();exec(s[:s.index('rows=[];segments=[];floors=[]')])
for p in Path('Assets/ChooGuard/Art/StationInterior').rglob('*.asset.meta'):
 m=re.search(r'^guid: ([0-9a-f]+)',p.read_text(),re.M)
 if m:meta[m[1]]=str(p)[:-5]
j=json.loads((O/'current-generated-contributors.json').read_text());names={b['name']for e in j['elements']for b in e['batches']};rows=[];coll=[]
def common(cid,typ,pp,mfid,tid):
 t=blocks[mfid][1];m=re.search(r'^  m_Mesh: \{fileID: (-?\d+), guid: ([0-9a-f]+), type: \d+\}',t,re.M);assert m,pp
 fid,guid=m.groups();asset=meta[guid];oldrow=old.get(cid);oldm=mm.get(oldrow.get('mesh'))if oldrow else None
 role=oldm['id']if oldm and oldm['asset_guid']==guid else 'matchinghall-source-'+str(cid)
 return {'owner':{'path':pp,'global_object_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-'+str(cid)+'-0','world_matrix':frame(tid).tolist()},'mesh':{'id':role,'asset':asset,'asset_guid':guid,'file_id':int(fid)},'source_live_file_sha256':sha(asset),'component_id':cid,'meshfilter_id':mfid if typ==23 else None,'component_block_sha256':hashlib.sha256(blocks[cid][1].encode()).hexdigest(),'transform_id':tid,'transform_block_sha256':hashlib.sha256(blocks[tid][1].encode()).hexdigest(),'asset_meta_sha256':sha(asset+'.meta')}
for cid,(typ,t)in blocks.items():
 if typ not in[23,64]:continue
 go=ref(t,'m_GameObject');comps=bygo[go];tid=comps[4];pp=path(tid)
 if typ==23 and pp.startswith('실내 트윈 마감/2F 편의/')and (pp.split('/')[-1]in names or pp.endswith('/Observed office front glazing')):
  rows.append(common(cid,typ,pp,comps[33],tid))
 if typ==64 and pp=='실내 트윈 마감/2F 편의/Kit_Colliders':coll.append(common(cid,typ,pp,cid,tid))
# Adjacent opaque original walls need exact source partitions before proposed replacement can render.
current=json.loads((O/'current-contributors.json').read_text());native=[]
for r in current['rows']:
 if not r['owner'].startswith('FPSWorld/공식 자료 부산역 역사/'):continue
 p=np.array(r['section_segments_sourcepixel_main']);inside=(p[:,:,0].min(1)<365)&(p[:,:,0].max(1)>301)&(p[:,:,1].min(1)<278)&(p[:,:,1].max(1)>198)
 if inside.any():
  cid=r['renderer_id'];go=ref(blocks[cid][1],'m_GameObject');c=bygo[go];rr=common(cid,23,r['owner'],c[33],c[4]);rr['role_in_proposal']='native_wall_overlap_inspection_only';rr['candidate_section_triangle_ordinals']=np.array(r['section_triangle_ordinals'])[inside].tolist();rows.append(rr);native.append(r['owner'])
  if 64 in c:coll.append(common(c[64],64,r['owner'],c[64],c[4]))
packet={'schema':'chooguard.matchinghall-current-channel-request.v1','scene_sha256':sha(S),'source_spec_sha256':j['source_spec_sha256'],'generated_contributors_sha256':sha(O/'current-generated-contributors.json'),'rows':rows,'collider_rows':coll,'triangle_partition_ready':False,'purpose':'Freeze exact saved current fullchannel geometry for five generated envelope contributor correspondence and native-wall intersections. No apply authorization.'}
(O/'current-channel-export-request.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2)+'\n');print('renderers',len(rows),'colliders',len(coll),'native',native);print('OUTPUT',O/'current-channel-export-request.json',sha(O/'current-channel-export-request.json'))
