from pathlib import Path
import json,struct,hashlib,re,yaml,collections
import numpy as np
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-whole-layout-20261009')
P=O/'native-fixed-stair-separation-packet.json';p=json.loads(P.read_text());MP=Path('.planning/2026-10-09-floor2-completion-pass2/current/native-stair-channels/metadata.json');meta=json.loads(MP.read_text());hb=lambda b:hashlib.sha256(b).hexdigest();sha=lambda q:hb(Path(q).read_bytes())
def ints(*n):return struct.pack('<'+'i'*len(n),*n)
def ds(s):b=s.encode();assert len(b)<128;return bytes([len(b)])+b
def read(entry):
 b=Path(entry['path']).read_bytes();assert hb(b)==entry['binary_sha256'];pos=0
 def i():
  nonlocal pos
  v=struct.unpack_from('<i',b,pos)[0];pos+=4;return v
 def arr(d):
  nonlocal pos
  n=i();r=np.frombuffer(b,'<f4',n*d,pos).reshape(n,d).copy();pos+=n*d*4;return r
 assert i()==1
 d={'vertices':arr(3),'normals':arr(3),'tangents':arr(4),'colors':arr(4),'uv':[],'submeshes':[]}
 for u in range(8):
  n=i();dim=i();r=np.frombuffer(b,'<f4',n*4,pos).reshape(n,4).copy();pos+=n*16;d['uv'].append({'dimension':dim,'data':r})
 ns=i()
 for s in range(ns):
  topology=i();n=i();indices=np.frombuffer(b,'<i4',n,pos).copy();pos+=n*4;d['submeshes'].append({'topology':topology,'indices':indices})
 assert pos==len(b)and len(d['vertices'])==entry['vertex_count']and len(d['submeshes'])==entry['submesh_count']
 assert not entry['blendshapes']and not entry['boneweights']and not entry['bindposes']
 return d

def complete(d):
 allidx=np.concatenate([s['indices']for s in d['submeshes']]).astype('<i4');v=d['vertices'];n=d['normals'];t=d['tangents'];uv=d['uv'][0]['data'][:,:2]
 mesh=hb(ints(len(v),len(allidx),len(n))+v.astype('<f4').tobytes()+allidx.tobytes()+n.astype('<f4').tobytes())
 channel=hb(ds(mesh)+ints(len(uv),len(t))+uv.astype('<f4').tobytes()+t.astype('<f4').tobytes())
 body=ds(channel)+ints(len(d['submeshes']))
 for s in d['submeshes']:body+=ints(s['topology'],len(s['indices']))+s['indices'].astype('<i4').tobytes()
 assert len(d['colors'])==0
 body+=b'\0'+ints(0)
 for uv in d['uv']:body+=ints(len(uv['data']),uv['dimension'])+uv['data'].astype('<f4').tobytes()
 return hb(body)

def write(d,path):
 b=ints(1)
 for key in ['vertices','normals','tangents','colors']:b+=ints(len(d[key]))+d[key].astype('<f4').tobytes()
 for uv in d['uv']:b+=ints(len(uv['data']),uv['dimension'])+uv['data'].astype('<f4').tobytes()
 b+=ints(len(d['submeshes']))
 for s in d['submeshes']:b+=ints(s['topology'],len(s['indices']))+s['indices'].astype('<i4').tobytes()
 path.write_bytes(b);return {'path':str(path),'binary_sha256':hb(b),'expected_complete_channel_digest':complete(d),'vertex_count':len(d['vertices']),'submesh_count':len(d['submeshes']),'triangle_count':sum(len(s['indices'])for s in d['submeshes'])//3}

entries={e['role']:e for e in meta['entries']};data={role:read(e)for role,e in entries.items()}
for role,d in data.items():assert complete(d)==entries[role]['complete_channel_digest']
vr=p['renderer'];raw=np.frombuffer(bytes.fromhex(vr['selected_complete_vertex_bytes_hex']),'<f4').reshape(-1,12);ids=np.array(vr['selected_vertex_indices'])
assert np.array_equal(raw[:,:3],data['render']['vertices'][ids]);assert np.array_equal(raw[:,3:6],data['render']['normals'][ids]);assert np.array_equal(raw[:,6:10],data['render']['tangents'][ids]);assert np.array_equal(raw[:,10:12],data['render']['uv'][0]['data'][ids,:2])
for key in ['vertices','normals','tangents','colors']:assert np.array_equal(data['render'][key],data['collider'][key])
for u in range(8):assert data['render']['uv'][u]['dimension']==data['collider']['uv'][u]['dimension']and np.array_equal(data['render']['uv'][u]['data'],data['collider']['uv'][u]['data'])
assert len(p['collider_exact_correspondence'])==1 and p['collider_summary']['unique_physics_owner_per_selected_face']
sel_render=set(vr['selected_triangle_ordinals']);sel_coll=set(m['collider_triangle']for m in p['collider_exact_correspondence'][0]['matches']);assert len(sel_render)==len(sel_coll)==1306
# Current export stores submesh logical indices in the same flattened order as the exact current inventory.
col=p['collider_exact_correspondence'][0];fb=Path(col['mesh']['binary']).read_bytes();nv,ni,nn=struct.unpack_from('<iii',fb);colidx=np.frombuffer(fb,'<i4',ni,12+nv*12).reshape(-1,3)
for role,dd in data.items():
 logical=np.concatenate([s['indices']for s in dd['submeshes']]).reshape(-1,3)
 expected=colidx if role=='collider'else np.concatenate([np.array(s['selected_indices'],int).reshape(-1,3)for s in vr['submesh_partition']if s['selected_indices']])
 if role=='collider':assert np.array_equal(logical,colidx)
# Each physical matched face keeps exact indices including winding, not only coincident coordinates.
renderidx=np.concatenate([s['indices']for s in data['render']['submeshes']]).reshape(-1,3)
assert np.array_equal(renderidx[np.array(sorted(sel_render))],np.array(vr['selected_triangles']))
for m in col['matches']:assert np.array_equal(renderidx[m['renderer_triangle']],colidx[m['collider_triangle']])
outputs={};partition={}
for role,d in data.items():
 selection=sel_render if role=='render'else sel_coll;start=0;parts=[];keep_sub=[];take_sub=[];retained=[]
 for si,s in enumerate(d['submeshes']):
  assert s['topology']==0 and len(s['indices'])%3==0
  ts=s['indices'].reshape(-1,3);ords=np.arange(start,start+len(ts));mask=np.isin(ords,list(selection));taken=ts[mask].astype('<i4');kept=ts[~mask].astype('<i4');keep_sub.append({'topology':s['topology'],'indices':kept.ravel()});take_sub.append({'topology':s['topology'],'indices':taken.ravel()});retained.extend(ords[~mask].tolist());parts.append({'submesh':si,'source_global_triangle_start':start,'selected_submesh_triangle_ordinals':np.flatnonzero(mask).tolist(),'selected_global_triangle_ordinals':ords[mask].tolist(),'selected_indices':taken.tolist(),'selected_index_sequence_sha256':hb(taken.tobytes()),'retained_index_sequence_sha256':hb(kept.tobytes()),'selected_triangle_count':len(taken),'retained_triangle_count':len(kept)});start+=len(ts)
 keep={**d,'submeshes':keep_sub};take={**d,'submeshes':take_sub}
 outputs[role]={'retained':write(keep,O/(role+'-background-partition.bin')),'selected':write(take,O/(role+'-fixed-stair-partition.bin'))}
 partition[role]={'selected_triangle_ordinals':sorted(selection),'selected_triangle_count':len(selection),'source_triangle_count':start,'retained_triangle_count':len(retained),'submesh_partition':parts,'retained_flattened_index_sequence_sha256':hb(np.concatenate([s['indices']for s in keep_sub]).astype('<i4').tobytes()),'selected_flattened_index_sequence_sha256':hb(np.concatenate([s['indices']for s in take_sub]).astype('<i4').tobytes()),'source_index_sequence_sha256':hb(np.concatenate([s['indices']for s in d['submeshes']]).astype('<i4').tobytes())}
# Collider component/Transform serialization fields pinned; root preserves those fields on both partitions.
scene=Path('Assets/ChooGuard/Scenes/FpsStation.unity');txt=scene.read_text();blocks={int(i):b for i,b in re.findall(r'^--- !u!\d+ &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',txt,re.M|re.S)};cid=int(col['owner']['global_object_id'].split('-')[-2]);cb=blocks[cid];colfields=yaml.safe_load(cb)['MeshCollider']
p['complete_channels']={'current_targeted_export':str(MP),'current_targeted_export_sha256':sha(MP),'scene_sha256':meta['scene_sha256'],'entries':meta['entries'],'complete_digest_independently_reproduced':True,'all_vertex_channels_render_collider_equal':True,'binary_vertex_channels_equal_to_saved_renderer_asset':True,'color_and_uv1_through7_absent':True,'skin_and_blend_shapes_absent':True,'roles':partition,'expected_partitions':outputs}
p['collider_exact_correspondence'][0]['full_channel_export']=entries['collider'];p['collider_exact_correspondence'][0]['component_block_sha256']=hb(cb.encode());p['collider_exact_correspondence'][0]['component_fields']=colfields;p['collider_exact_correspondence'][0]['all_channel_scope']='Current actual complete P/N/T/UV0 and absent color/UV1..7/skin/blend channels independently sealed. Source triangle indices map exactly including winding.'
p['collider_summary']['full_FBX_channels_pending']=False;p['summary']['collider_split_ready']=True;p['collider_summary']['source_and_selected_index_winding_exact']=True
p['preserved'][1]='Other 96428 renderer triangle indices, other '+str(partition['collider']['retained_triangle_count'])+' collider triangle indices and all 68 material slot order';p['preserved'][3]='Original collider complete physical surface union and all serialized physics fields under declared 1306-face partition'
p['integration_contract']['collider_optional_partition']='Clone actual current original FBX collider twice with complete vertex buffers unchanged; retain source submesh count/topology and use independently selected current collider face ordinals per submesh. Parent collider uses retained clone; one identity-TRS child uses selected clone. Both preserve existing layer/physics-material/trigger/convex/cooking/enabled flags. Exact declared physical union is unchanged. Verify expected complete digests from this packet before saving.'
p['summary']['complete_channel_digests_reproduced']=True;p['summary']['render_source_triangles']=partition['render']['source_triangle_count'];p['summary']['collider_source_triangles']=partition['collider']['source_triangle_count'];p['summary']['all_selected_render_collider_indices_exact']=True
P.write_text(json.dumps(p,ensure_ascii=False,indent=2)+'\n')
print('SEALED',p['summary']);print('EXPECTED',outputs)
