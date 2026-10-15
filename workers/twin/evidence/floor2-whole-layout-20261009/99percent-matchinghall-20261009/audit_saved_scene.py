from pathlib import Path
import re,json,hashlib,struct,yaml,numpy as np
from functools import lru_cache
E=Path(__file__).resolve().parent
O=Path('workers/twin/output/floor2-whole-layout-20261009/99percent-matchinghall-20261009');O.mkdir(parents=True,exist_ok=True)
S=Path('Assets/ChooGuard/Scenes/FpsStation.unity');raw=S.read_bytes();txt=raw.decode();sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
blocks={int(m[2]):(int(m[1]),m[0])for m in re.finditer(r'^--- !u!(\d+) &(\d+)\n.*?(?=^--- !u!|\Z)',txt,re.M|re.S)}
def ref(t,k):
 m=re.search(r'^  '+re.escape(k)+r': \{fileID: (-?\d+)',t,re.M);return int(m[1])if m else 0
def vec(t,k):return yaml.safe_load(re.search(r'^  '+k+r': (.+)$',t,re.M)[1])
gos={};trs={};bygo={}
for fid,(typ,t)in blocks.items():
 if typ==1:gos[fid]={'name':yaml.safe_load(re.search(r'^  m_Name: (.*?)\n  m_TagString:',t,re.M|re.S)[1]),'active':int(re.search(r'^  m_IsActive: (\d+)$',t,re.M)[1])}
 elif typ==4:trs[fid]={'go':ref(t,'m_GameObject'),'parent':ref(t,'m_Father'),'p':vec(t,'m_LocalPosition'),'q':vec(t,'m_LocalRotation'),'s':vec(t,'m_LocalScale')};bygo.setdefault(ref(t,'m_GameObject'),{})[typ]=fid
 elif typ in[23,33,64]:bygo.setdefault(ref(t,'m_GameObject'),{})[typ]=fid
@lru_cache(None)
def frame(tid):
 t=trs[tid];x,y,z,w=[t['q'][i]for i in 'xyzw'];R=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
 M=np.eye(4);M[:3,:3]=R@np.diag([t['s'][i]for i in'xyz']);M[:3,3]=[t['p'][i]for i in'xyz'];return frame(t['parent'])@M if t['parent'] else M
@lru_cache(None)
def path(tid):
 t=trs[tid];n=gos[t['go']]['name'];return path(t['parent'])+'/'+n if t['parent']else n
@lru_cache(None)
def active(tid):return gos[trs[tid]['go']]['active']and(active(trs[tid]['parent'])if trs[tid]['parent']else True)
F=np.array([[.9602936856769431,0,.2789911060392293],[0,1,0],[-.2789911060392293,0,.9602936856769431]]);I=np.linalg.inv(F)
reg=json.loads(Path('workers/twin/output/floor2-official-pdf-registration-20261009/architectural-registration.json').read_text());T=np.array(reg['main']['native_UV_to_raster_pixels']);B=np.array(reg['box']['native_UV_to_raster_pixels'])
inv=json.loads(Path('.planning/2026-10-09-floor2-completion-pass2/after-facades/current-inventory.json').read_text());mm={m['id']:m for m in inv['meshes']};old={int(r['global_object_id'].split('-')[-2]):r for r in inv['renderers']};meta={m['asset_guid']:m['asset']for m in inv['meshes']if m.get('asset_guid')}
for ap in Path('Assets/ChooGuard/Art/StationInterior').rglob('*.asset.meta'):
 gm=re.search(r'^guid: ([0-9a-f]+)',ap.read_text(),re.M)
 if gm:meta[gm[1]]=str(ap)[:-5]
def assetmesh(asset):
 t=Path(asset).read_text();vd=yaml.safe_load(re.search(r'^  m_VertexData:\n.*?(?=^  m_CompressedMesh:)',t,re.M|re.S)[0])['m_VertexData'];raw=bytes.fromhex(vd['_typelessdata']);n=vd['m_VertexCount'];ch=vd['m_Channels'][0];assert ch['stream']==ch['format']==0 and ch['dimension']==3
 v=np.ndarray((n,3),'<f4',raw,offset=ch['offset'],strides=(len(raw)//n,4)).copy();ib=bytes.fromhex(re.search(r'^  m_IndexBuffer: (.*)$',t,re.M)[1]);fmt=int(re.search(r'^  m_IndexFormat: (\d+)$',t,re.M)[1]);ix=np.frombuffer(ib,'<u4'if fmt else'<u2');sm=yaml.safe_load(re.search(r'^  m_SubMeshes:\n.*?(?=^  m_Shapes:)',t,re.M|re.S)[0])['m_SubMeshes'];dt=4 if fmt else 2;ts=[]
 for sub in sm:
  assert sub['topology']==0
  ts.append((ix[sub['firstByte']//dt:sub['firstByte']//dt+sub['indexCount']]+sub['baseVertex']).reshape(-1,3))
 return v,np.concatenate(ts)
def section(q,ts):
 tr=q[ts];mask=(tr[:,:,2].min(1)<8.25)&(tr[:,:,2].max(1)>8.25);segs=[];ords=[]
 for ordinal in np.flatnonzero(mask):
  pts=[]
  for i,j in[(0,1),(1,2),(2,0)]:
   a,b=tr[ordinal,i],tr[ordinal,j]
   if (a[2]<=8.25<b[2])or(b[2]<=8.25<a[2]):pts.append(a+(b-a)*(8.25-a[2])/(b[2]-a[2]))
  if len(pts)==2 and np.linalg.norm(pts[0]-pts[1])>1e-6:segs.append(np.array(pts)[:,:2]);ords.append(int(ordinal))
 return np.array(segs),ords
rows=[];segments=[];floors=[];nativefloors=[];floor_provenance=[]
for rid,(typ,t)in blocks.items():
 if typ!=23:continue
 go=ref(t,'m_GameObject');components=bygo.get(go,{});tid=components.get(4);mfid=components.get(33)
 if not tid or not mfid:continue
 pp=path(tid)
 if not(pp.startswith('FPSWorld/공식 자료 부산역 역사/')or pp.startswith('맞이방 · 원본 정합/')or pp.startswith('실내 트윈 마감/2F 편의/')):continue
 if not active(tid)or not int(re.search(r'^  m_Enabled: (\d+)',t,re.M)[1]):continue
 mf=blocks[mfid][1];meshmatch=re.search(r'^  m_Mesh: \{fileID: (-?\d+), guid: ([0-9a-f]+), type: \d+\}',mf,re.M)
 if not meshmatch:continue
 fileid,guid=meshmatch.groups();asset=meta.get(guid);r=old.get(rid);oldmesh=mm.get(r.get('mesh'))if r else None
 if not asset:continue
 try:
  if asset.endswith('.asset'):v,ts=assetmesh(asset);basis='actual_saved_asset_vertex_index_channels'
  elif oldmesh and sha(asset)==oldmesh['asset_sha256']and oldmesh.get('binary'):
   b=Path(oldmesh['binary']).read_bytes();assert hashlib.sha256(b).hexdigest()==oldmesh['binary_sha256'];nv,ni,nn=struct.unpack_from('<iii',b);v=np.frombuffer(b,'<f4',nv*3,12).reshape(-1,3);ts=np.frombuffer(b,'<i4',ni,12+nv*12).reshape(-1,3);basis='prior_actual_mesh_binary_exact_current_asset_sha_owner_guid_and_frame_verified';assert oldmesh['asset_guid']==guid and np.max(abs(frame(tid)-np.array(r['world_matrix'])))<.0001
  else:continue
 except Exception as ex:print('SKIP',pp,ex);continue
 M=frame(tid);q=((v@M[:3,:3].T+M[:3,3])@I.T)[:,[2,0,1]];segs,ords=section(q,ts)
 if len(segs):
  pix=np.c_[segs.reshape(-1,2),np.ones(len(segs)*2)]@T;pix=pix.reshape(-1,2,2);keep=(pix[:,:,1].min(1)<278)&(pix[:,:,1].max(1)>-15)&(pix[:,:,0].min(1)<478)&(pix[:,:,0].max(1)>50)
  if keep.any():
   rr={'renderer_id':rid,'meshfilter_id':mfid,'collider_id':components.get(64),'transform_id':tid,'owner':pp,'mesh_guid':guid,'mesh_file_id':int(fileid),'asset':asset,'asset_sha256':sha(asset),'geometry_basis':basis,'old_inventory_mesh':r.get('mesh')if r else None,'world_matrix':M.tolist(),'renderer_block_sha256':hashlib.sha256(t.encode()).hexdigest(),'meshfilter_block_sha256':hashlib.sha256(mf.encode()).hexdigest(),'vertex_count':len(v),'triangle_count':len(ts),'section_triangle_ordinals':np.array(ords)[keep].tolist(),'section_segments_native_UV':segs[keep].tolist(),'section_segments_sourcepixel_main':pix[keep].tolist()}
   rows.append(rr);segments.append(segs[keep])
 tr=q[ts];fl=(np.ptp(tr[:,:,2],axis=1)<.004)&(abs(tr[:,:,2].mean(1)-7)<.04)
 if fl.any():
  floors.append(tr[fl]);floor_provenance.append({'owner':pp,'asset':asset,'asset_sha256':sha(asset),'triangles':int(fl.sum()),'native_original':pp.startswith('FPSWorld/공식 자료 부산역 역사/')})
  if pp.startswith('FPSWorld/공식 자료 부산역 역사/'):nativefloors.append(tr[fl])
np.save(E/'current-section-UV.npy',np.concatenate(segments));np.save(E/'current-floor-triangles-UVY.npy',np.concatenate(floors));np.save(E/'current-native-floor-triangles-UVY.npy',np.concatenate(nativefloors));(O/'current-floor-provenance.json').write_text(json.dumps(floor_provenance,ensure_ascii=False,indent=2)+'\n');j={'schema':'chooguard.saved-matchinghall-section.v1','scene':str(S),'scene_sha256':hashlib.sha256(raw).hexdigest(),'sectionY':8.25,'sourcepixel_transform':'single_main_affine_shared_main2f_seam','rows':rows,'site_accuracy_verified':False,'production_mutated':False,'all_native_subasset_file_ids_reexported':False};(O/'current-contributors.json').write_text(json.dumps(j,ensure_ascii=False,indent=2)+'\n');print('ROWS',len(rows),'SEGMENTS',sum(len(r['section_triangle_ordinals'])for r in rows),'FLOORS',sum(len(f)for f in floors),'SCENE',j['scene_sha256'])
