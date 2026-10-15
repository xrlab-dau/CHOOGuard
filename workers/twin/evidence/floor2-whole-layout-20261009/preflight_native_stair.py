from pathlib import Path
import json,hashlib,struct,re,collections
import numpy as np
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-whole-layout-20261009');PP=O/'native-fixed-stair-separation-packet.json';P=json.loads(PP.read_text());checks=[]
hb=lambda b:hashlib.sha256(b).hexdigest();sha=lambda p:hb(Path(p).read_bytes())
def ck(id,ok,detail):checks.append({'id':id,'pass':bool(ok),'detail':detail})
def read(p):
 b=Path(p).read_bytes();cursor=4;assert struct.unpack_from('<i',b)[0]==1;ch={}
 for label,d in [('position',3),('normal',3),('tangent',4),('color',4)]:
  count=struct.unpack_from('<i',b,cursor)[0];cursor+=4;ch[label]={'count':count,'dimension':d,'bytes':b[cursor:cursor+count*d*4]};cursor+=count*d*4
 for u in range(8):
  count,dim=struct.unpack_from('<ii',b,cursor);cursor+=8;ch['uv'+str(u)]={'count':count,'dimension':dim,'bytes':b[cursor:cursor+count*16]};cursor+=count*16
 n=struct.unpack_from('<i',b,cursor)[0];cursor+=4;subs=[]
 for s in range(n):
  topology,count=struct.unpack_from('<ii',b,cursor);cursor+=8;indices=np.frombuffer(b,'<i4',count,cursor).copy();cursor+=count*4;subs.append({'topology':topology,'indices':indices})
 assert cursor==len(b);return ch,subs
I=json.loads(Path(P['inventory']).read_text());scene=Path('Assets/ChooGuard/Scenes/FpsStation.unity')
ck('scene_epoch',sha(scene)==P['scene_sha256']==P['scene_file_sha256_at_stage'],P['scene_sha256'])
ck('inventory_epoch',sha(P['inventory'])==P['inventory_sha256']and I['scene_file_sha256']==P['scene_sha256'],'Exact root current live geometry export')
ck('source_assembly_receipt',sha(P['source_receipt'])==P['source_receipt_sha256'],'Source/current exact face correspondence guard')
ck('descriptive_identity',P['guide_facility_assignment']is None and not P['allow_new_motion'],'Architecture identity does not claim unresolved guide facility')
ck('source_part_cover',len(P['source_objects'])==83 and sum(r['source_triangles']for r in P['source_objects'])==1306 and len({r['source_id']for r in P['source_objects']})==83 and all(r['source_path'].startswith(P['source_assembly_prefix'])for r in P['source_objects']),'83 exact source mesh objects and 1306 faces')
ck('render_uniqueness',not P['duplicate_render_geometry']and not P['existing_SourcePartIdentity_for_selected_stair'],'All active readable current renderers checked; no duplicate exact face owners or existing selected motion identity')
ck('collider_uniqueness',P['collider_summary']['unique_physics_owner_per_selected_face']and not P['collider_summary']['missing_selected_faces']and not P['collider_summary']['ambiguous_selected_faces']and len(P['collider_exact_correspondence'])==1,'1306 source faces each belong to exactly one current active collider')
ck('collider_geometry_identity',all(m['maximum_vertex_error_model']==0 and m['vertex_permutation']==[0,1,2]for m in P['collider_exact_correspondence'][0]['matches']),'Exact indexed geometry including winding, no tolerance in collider transfer')
ck('root_actual_full_channels',sha(P['complete_channels']['current_targeted_export'])==P['complete_channels']['current_targeted_export_sha256']and P['complete_channels']['complete_digest_independently_reproduced'],'Current Unity complete digest independently reconstructed from actual exported vertex/UV/index data')
for role in ['render','collider']:
 src=next(x for x in P['complete_channels']['entries']if x['role']==role);sc,ss=read(src['path']);part=P['complete_channels']['roles'][role];outs=P['complete_channels']['expected_partitions'][role];kc,ks=read(outs['retained']['path']);tc,ts=read(outs['selected']['path'])
 ck(role+'_actual_source_sha',sha(src['path'])==src['binary_sha256'],'Fresh source full-channel binary')
 ck(role+'_output_sha',all(sha(o['path'])==o['binary_sha256']for o in outs.values()),'Both expected output binary guards')
 ck(role+'_all_channels_unchanged',sc==kc==tc,'Full 94705 vertex arrays, all UV dimensions and absent channels are byte-identical')
 ck(role+'_submesh_slots',len(ss)==len(ks)==len(ts)==68 and all(a['topology']==b['topology']==c['topology']==0 for a,b,c in zip(ss,ks,ts)),'68 exact Triangle submesh slots retained')
 selection=set(part['selected_triangle_ordinals']);cursor=0
 for s,(original,keep,take)in enumerate(zip(ss,ks,ts)):
  a=original['indices'].reshape(-1,3);k=keep['indices'].reshape(-1,3);t=take['indices'].reshape(-1,3);mask=np.array([i in selection for i in range(cursor,cursor+len(a))],dtype=bool);cursor+=len(a)
  ck(role+'_slot_'+str(s)+'_exact_partition',np.array_equal(a[~mask],k)and np.array_equal(a[mask],t),'Original face order/winding retained; selected and retained declared ordinals disjoint')
 ck(role+'_all_faces_once',len(selection)==1306 and cursor==part['source_triangle_count']and sum(len(x['indices'])for x in ts)//3==1306 and sum(len(x['indices'])for x in ks)//3==part['retained_triangle_count'],'Selected+retained cover every original indexed face exactly once')
selout=P['complete_channels']['expected_partitions'];ck('render_physical_child_byte_identity',Path(selout['render']['selected']['path']).read_bytes()==Path(selout['collider']['selected']['path']).read_bytes(),'Render and physical child meshes coincide including all attributed channels and 68 slots')
for m in P['renderer']['materials']:ck('material_'+m['guid'],sha(m['path'])==m['sha256'],'Existing material file is pinned and unchanged')
ck('saved_renderer_asset',sha(P['renderer']['mesh']['asset'])==P['renderer']['mesh']['asset_sha256'],'Original serialized source clone remains unchanged')
ck('saved_collider_source_asset',sha(P['collider_exact_correspondence'][0]['mesh']['asset'])==P['collider_exact_correspondence'][0]['mesh']['asset_sha256'],'Original FBX source remains unchanged')
blocks={int(i):b for i,b in re.findall(r'^--- !u!\d+ &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',scene.read_text(),re.M|re.S)}
for key,field in [('meshfilter_component_id','meshfilter_block_sha256'),('renderer_component_id','renderer_block_sha256')]:ck('current_'+key,hb(blocks[P['renderer'][key]].encode())==P['renderer'][field],'Exact current serialized binding')
ck('inventory_owner_is_renderer',int(P['renderer']['owner']['global_object_id'].split('-')[-2])==P['renderer']['renderer_component_id']and blocks[P['renderer']['renderer_component_id']].startswith('MeshRenderer:\n'),'Inventory ownership GlobalObjectId is explicitly the renderer component')
ck('actual_meshfilter_is_distinct_component',P['renderer']['meshfilter_component_id']!=P['renderer']['renderer_component_id']and blocks[P['renderer']['meshfilter_component_id']].startswith('MeshFilter:\n'),'Actual saved scene class33 MeshFilter is829381568; class23 renderer is829381567')
ck('meshfilter_mesh_guid_binding',P['renderer']['mesh']['asset_guid']in blocks[P['renderer']['meshfilter_component_id']],'Exact current source sharedMesh GUID in the saved MeshFilter component')
ck('same_serialized_gameobject_boundaries',all(('m_GameObject: {fileID: '+str(P['renderer']['gameobject_id'])+'}')in blocks[i]for i in [P['renderer']['meshfilter_component_id'],P['renderer']['renderer_component_id'],int(P['collider_exact_correspondence'][0]['owner']['global_object_id'].split('-')[-2])]),'MeshFilter, MeshRenderer and MeshCollider belong to the same original GameObject')
for r in P['renderer']['transform_lineage']:ck('transform_'+str(r['transform_id']),hb(blocks[r['transform_id']].encode())==r['block_sha256'],'Every parent/current local TRS exact')
col=P['collider_exact_correspondence'][0];cid=int(col['owner']['global_object_id'].split('-')[-2]);ck('current_collider_component',hb(blocks[cid].encode())==col['component_block_sha256'],'Current physical fields pinned; root preserves all except declared mesh/GameObject references')
ck('same_owner_frames',P['renderer']['owner']['world_matrix']==col['owner']['world_matrix'],'Identity local child inherits the original transformed frame, no pose edit')
ck('no_production_writes',not P['production_mutated']and not P['allowed_geometry_change']and not P['materials_mutated'],'Packet and offline binaries only')
out={'schema':'chooguard.native-stair-separation-preflight.v1','packet':str(PP),'packet_sha256':sha(PP),'scene_sha256':P['scene_sha256'],'checks':checks,'passed':sum(c['pass']for c in checks),'failed':sum(not c['pass']for c in checks),'production_mutated':False,'unity_execution_performed':False,'acceptance_owner':'root','verdict':'PASS'if all(c['pass']for c in checks)else'FAIL','runtime_boundary':'This certifies the proposed exact offline mesh partition only. Root must install/replay and verify actual saved scene, render and physics union after integration.'}
(E/'native-stair-separation-preflight.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(out['verdict'],out['passed'],out['failed'],'packetSHA',out['packet_sha256'])
assert out['failed']==0
