from pathlib import Path
import json,hashlib,re,struct,yaml,sys,numpy as np
from shapely.geometry import Polygon,LineString
from shapely.ops import unary_union
from scipy.spatial.transform import Rotation
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-main-restoration-20261009/door21-native-reuse');P=json.loads((O/'packet.json').read_text());old=json.loads(Path('workers/twin/output/floor2-main-restoration-20261009/four-bay-packet.json').read_text());oldmap={r['source_asset']:r for r in old['mesh_patches']};sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();hb=lambda b:hashlib.sha256(b).hexdigest();checks=[]
def ck(i,ok,d):checks.append({'id':i,'passed':bool(ok),'details':d})
source_scene=Path(sys.argv[1]) if len(sys.argv)>1 else Path(P['scene']);text=source_scene.read_text();blocks={int(i):b for i,b in re.findall(r'^--- !u!\d+ &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S)}
ck('scene_epoch',sha(source_scene)==P['scene_sha256'],{'packet_source_epoch':P['scene_sha256'],'verified_source_scene':str(source_scene),'actual_sha256':sha(source_scene)});ck('evidence_seals',all(sha(x['path'])==x['sha256'] for x in P['evidence']) and sha(P['proposal'])==P['proposal_sha256'] and sha(P['guide'])==P['guide_sha256'],'Every packet dependency byte matched')
geo=[]
for row in P['mesh_patches']:
 f=Path(row['source_asset']);a=yaml.safe_load(f.read_text().split('Mesh:\n',1)[1]);vd=a['m_VertexData'];raw=bytes.fromhex(vd['_typelessdata']);nv=vd['m_VertexCount'];stride=len(raw)//nv;v=np.ndarray((nv,3),'<f4',raw,strides=(stride,4)).copy();t=np.frombuffer(bytes.fromhex(a['m_IndexBuffer']),'<u4' if a['m_IndexFormat'] else '<u2').reshape(-1,3);ids=np.array([x['vertex_index'] for x in row['position_edits']],int);va=v.copy()
 for e in row['position_edits']:
  ck(f.name+'_expected_vertex_'+str(e['vertex_index']),np.array_equal(v[e['vertex_index']],np.array(e['expected'],'<f4')),e['expected']);va[e['vertex_index']]=e['replacement']
 removed=set(row['remove_triangle_ordinals']);ret=np.array([i for i in range(len(t)) if i not in removed]);ta=t[ret]
 ck(f.name+'_file_guid_full_channels',sha(f)==row['source_file_sha256'] and re.search(r'^guid: (.*)$',Path(str(f)+'.meta').read_text(),re.M)[1]==row['source_asset_guid'] and hb(raw)==row['source_full_vertex_bytes_sha256'] and a['m_VertexData']['m_Channels']==row['source_channels'],'Exact saved source asset, interleaved full channel buffer and GUID')
 ck(f.name+'_retained_face_sequence',ret.tolist()==row['source_retained_triangle_ordinals'] and hb(ta.astype('<u4').tobytes())==row['source_retained_triangle_index_u32_sha256'],'All unremoved face ordering/winding unchanged')
 updated=bytearray(raw)
 for i in ids:updated[i*stride:i*stride+12]=va[i].astype('<f4').tobytes()
 diffs=np.flatnonzero(np.frombuffer(raw,'u1')!=np.frombuffer(updated,'u1'));allowed={int(i)*stride+j for i in ids for j in range(12)}
 ck(f.name+'_only_declared_position_channel_bytes',set(diffs)<=allowed and all(raw[i*stride+12:(i+1)*stride]==updated[i*stride+12:(i+1)*stride] for i in range(nv)),'Every normal, tangent, UV and extra source byte unchanged; no new vertices')
 orig=oldmap[row['original_registered_source_asset']];mask=next(m for m in orig['unit_contributor_masks'] if m['guide_number']==21);other=[m for m in orig['unit_contributor_masks'] if m['guide_number']!=21];otherfs={f for m in other for f in m['selected_triangle_ordinals']};otherids={i for m in other for i in m['selected_vertex_indices']}
 ck(f.name+'_unit20_22_23_negative_masks',not removed&otherfs and not set(ids)&otherids,{'other_units':[m['guide_number'] for m in other],'removed_other_faces':list(removed&otherfs),'moved_other_indices':list(set(ids)&otherids)})
 # Full exact coordinate aliases referenced by this Unit21 source; moving one side must move every corresponding source alias.
 refs=np.unique(t[np.array(mask['selected_triangle_ordinals'])]);torn=[]
 for i in ids:
  aliases=refs[np.all(v[refs]==v[i],axis=1)];targets=va[aliases]
  if not np.all(targets==va[i]):torn.append({'index':int(i),'aliases':aliases.tolist()})
 ck(f.name+'_zero_torn_exact_welds',not torn,torn)
 owners=row['owners']+row['collider_owners'];actual=[]
 for i,b in blocks.items():
  if b.startswith(('MeshFilter:\n','MeshCollider:\n')) and re.search(r'm_Mesh: \{[^\n]*guid: '+row['source_asset_guid']+',',b):actual.append(i)
 ck(f.name+'_all_current_shared_MF_MC',set(actual)=={x['component_id'] for x in owners},{'actual':actual,'listed':[x['component_id'] for x in owners]})
 for owner in owners:
  ck(f.name+'_owner_block_'+str(owner['component_id']),hb(blocks[owner['component_id']].encode())==owner['component_block_sha256'],owner['path'])
  ck(f.name+'_frame_lineage_'+str(owner['component_id']),all(hb(blocks[x['transform_id']].encode())==x['block_sha256'] for x in owner['transform_lineage']),owner['world_matrix'])
  if 'renderer_component_id' in owner:ck(f.name+'_material_renderer_'+str(owner['renderer_component_id']),hb(blocks[owner['renderer_component_id']].encode())==owner['renderer_block_sha256'],'Complete serialized renderer fields/material array unchanged')
 if owners:
  M=np.array(owners[0]['world_matrix']);w=va@M[:3,:3].T+M[:3,3];geo.append((f.name,w,ta))
# Independent exact ray/triangle test along native opening normal, lower return adjusted included.
n=P['native_operator_preservation'];M=np.array(n['world_matrix']);c=np.array(n['world_centre']);F=n['fields'];along=M[:3,:3]@np.array([float(F['Along'][k]) for k in ('x','y','z')]);along/=np.linalg.norm(along);normal=M[:3,:3]@np.array([float(F['Normal'][k]) for k in ('x','y','z')]);normal/=np.linalg.norm(normal);width=float(F['Width']);height=float(F['Height']);d=-normal;hits=[];samples=[]
for s in np.linspace(-width/2,width/2,9):
 for y in [.05,.4,1.1,1.7,2.2,height]:
  start=c+along*s+np.array([0,y,0])+normal*.4;sample={'s':float(s),'y_above_sill':y};samples.append(sample)
  for name,v,t in geo:
   tri=v[t];e1=tri[:,1]-tri[:,0];e2=tri[:,2]-tri[:,0];h=np.cross(np.broadcast_to(d,e2.shape),e2);a=(e1*h).sum(1);ok=abs(a)>1e-12;inv=np.divide(1,a,out=np.zeros_like(a),where=ok);z=start-tri[:,0];u=inv*(z*h).sum(1);q=np.cross(z,e1);vv=inv*(q*d).sum(1);dist=inv*(e2*q).sum(1);ii=np.flatnonzero(ok&(u>=-1e-8)&(vv>=-1e-8)&(u+vv<=1+1e-8)&(dist>=0)&(dist<=1.1))
   if len(ii):hits.append({'sample':sample,'source':name,'retained_triangle_after_ordinals':ii.tolist(),'distances':dist[ii].tolist()})
ck('native_nominal_aperture_54_staged_static_front_rays_clear',not hits,{'sample_count':len(samples),'outside_normal_start':.4,'inside_normal_end':.7,'hits':hits,'only_proposed_generated_static_batches_tested':True,'native_moving_leaf_and_all_world_runtime_not_certified':True})
# Native preserved serialized controller and complete leaf hierarchy.
ck('native_operator_and_leaves_all_exact',hb(blocks[n['station_door_component_id']].encode())==n['protected_component_block_sha256'] and hb(blocks[n['transform_id']].encode())==n['protected_transform_block_sha256'] and all(hb(blocks[x['transform_id']].encode())==x['block_sha256'] and all(hb(blocks[k['component_id']].encode())==k['block_sha256'] for k in x['components']) for leaf in n['leaf_subtrees'] for x in leaf['nodes']),'All controller fields/pose/source-root transforms/leaf nodes unchanged')
a=P['gameobject_actions'][0];ck('duplicate_operator_archived_exact_root_only',a['gameobject_id']==2089695507 and a['replacement_active_self'] is False and hb(blocks[a['gameobject_id']].encode())==a['source_gameobject_block_sha256'] and P['withhold_door']['generic_element']==a['door_element'] and P['withhold_door']['native_element']==n['element'],'Current generated root inactive proposal retains IDs/fields/assets/local poses; root loader must execute archival atomically')
ck('all103_serialized_stationdoor_bindings_retained',sum('m_EditorClassIdentifier: ChooGuard.App::ChooGuard.App.Fps.Facilities.StationDoor\n' in b for b in blocks.values())==103,'103 serialized StationDoor components preserved; one generated operator inactive after application, native remains active')
report={'schema':'chooguard.unit21-native-reuse-independent-byte-qa.v1','packet_sha256':sha(O/'packet.json'),'scene_sha256':P['scene_sha256'],'verified_scene_path':str(source_scene),'status':'PASS_OFFLINE_PROPOSAL_ONLY' if all(c['passed'] for c in checks) else 'REJECT','summary':{'checks':len(checks),'passed':sum(c['passed'] for c in checks),'staged_static_aperture_rays':len(samples),'runtime_clearance_verified':False},'checks':checks,'production_mutated':False};(E/'independent-qa.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'status':report['status'],'summary':report['summary'],'failures':[c for c in checks if not c['passed']]},ensure_ascii=False,indent=2))
