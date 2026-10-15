from pathlib import Path
import json,struct,hashlib,re,yaml,copy,math
import numpy as np
from shapely.geometry import Polygon,box
from shapely.ops import triangulate
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-whole-layout-20261009/newphoto-scope');O.mkdir(exist_ok=True)
hb=lambda b:hashlib.sha256(b).hexdigest();sha=lambda p:hb(Path(p).read_bytes())
helpers=Path('workers/twin/evidence/floor2-whole-layout-20261009/seal_native_stair_channels.py').read_text();exec(helpers[helpers.index('def ints'):helpers.index('entries={')])
S=json.load(open('.planning/2026-10-09-floor2-completion-pass2/current/current-inventory.json'));MM={m['id']:m for m in S['meshes']};C=json.load(open(E/'namcheon-current-contributors.json'));MP=Path('.planning/2026-10-09-floor2-completion-pass2/namcheon-full-channels/metadata.json');meta=json.loads(MP.read_text());entries={r['role']:r for r in meta['entries']};data={k:read(v)for k,v in entries.items()}
for k,d in data.items():assert complete(d)==entries[k]['complete_channel_digest']
F=np.array([[.9602937,0,.278991133],[0,1,0],[-.278991133,0,.9602937]]);I=np.linalg.inv(F);FH=np.eye(4);FH[:3,:3]=F
slope=.07227666563340311;intercept=14.598612646986819;angle=math.atan(slope);cs=math.cos(angle);sn=math.sin(angle)
Ax=np.array([[cs,0,sn],[0,1,0],[-sn,0,cs]])
oldcenter=np.array([-3.31,7,-36.5]);newcenter=np.array([slope*-36.5+intercept,7,-36.5]);DL=np.eye(4);DL[:3,:3]=Ax;DL[:3,3]=newcenter-Ax@oldcenter;DW=FH@DL@np.linalg.inv(FH)
scene=Path('Assets/ChooGuard/Scenes/FpsStation.unity');txt=scene.read_text();blocks={int(i):(int(c),b)for c,i,b in re.findall(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',txt,re.M|re.S)};parsed={i:yaml.safe_load(b)for i,(c,b)in blocks.items()};bygo={}
for i,(c,b)in blocks.items():
 z=next(iter(parsed[i].values()))
 if isinstance(z,dict)and isinstance(z.get('m_GameObject'),dict):bygo.setdefault(z['m_GameObject']['fileID'],{}).setdefault(c,[]).append(i)
def component_receipt(id):return {'component_id':id,'class_id':blocks[id][0],'block_sha256':hb(blocks[id][1].encode())}
def owner_receipt(r):
 id=int(r['global_object_id'].split('-')[-2]);assert blocks[id][0]in[23,64];go=next(iter(parsed[id].values()))['m_GameObject']['fileID'];z={'owner':r,'renderer_or_collider_component':component_receipt(id),'gameobject_id':go,'transform_component':component_receipt(bygo[go][4][0])}
 if blocks[id][0]==23:z['meshfilter_component']=component_receipt(bygo[go][33][0])
 return z
patches=[];doors=[]
for row in C['rows']:
 k=row['mesh']['id'];d=data[k];M=np.array(row['owner']['world_matrix']);flat=np.concatenate([s['indices']for s in d['submeshes']]).reshape(-1,3);ids=np.array(row['selected_vertex_indices'],int);selected=np.array(row['selected_triangle_ordinals'],int)
 assert np.array_equal(np.unique(flat[selected]),ids);assert not row['shared_selected_unselected_vertices'];assert sha(row['mesh']['asset'])==row['source_live_file_sha256']
 if '출입문/문짝' in row['owner']['path']:
  doors.append({**owner_receipt(row['owner']),'mesh_source':row['mesh'],'full_channel_export':entries[k],'expected_world_matrix':(DW@M).tolist(),'mesh_channels_change':False});continue
 if any(p['mesh_id']==k for p in patches):continue
 nd=copy.deepcopy(d);Rlocal=np.linalg.inv(M)@DW@M;v=nd['vertices'];v[ids]=(v[ids].astype(float)@Rlocal[:3,:3].T+Rlocal[:3,3]).astype('<f4')
 for key in ['normals','tangents']:
  if len(nd[key]):nd[key][ids,:3]=(nd[key][ids,:3].astype(float)@Rlocal[:3,:3].T).astype('<f4')
 owners=[owner_receipt(z['owner'])for z in C['rows']if z['mesh']['id']==k]
 colliders=[owner_receipt(z)for z in S['colliders']if z.get('mesh')==k and any(z['path']==rr['owner']['path']for rr in owners)]
 output=write(nd,O/(k+'-namcheon-aligned.bin'))
 unaffected=np.setdiff1d(np.arange(len(d['vertices'])),ids)
 assert np.array_equal(d['vertices'][unaffected],nd['vertices'][unaffected])
 assert all(np.array_equal(a['indices'],b['indices'])for a,b in zip(d['submeshes'],nd['submeshes']))
 assert all(np.array_equal(a['data'],b['data'])and a['dimension']==b['dimension']for a,b in zip(d['uv'],nd['uv']))
 patches.append({'kind':'rigid_selected_vertices','mesh_id':k,'source_mesh':row['mesh'],'full_channel_export':entries[k],'source_live_asset_sha256':sha(row['mesh']['asset']),'source_meta_sha256':sha(row['mesh']['asset']+'.meta'),'render_owners':owners,'collider_owners':colliders,'selected_triangle_ordinals':selected.tolist(),'selected_vertex_indices':ids.tolist(),'selected_unselected_shared_vertex_count':0,'local_rigid_transform':Rlocal.tolist(),'expected_output':output,'all_other_vertices_and_indices_preserved':True,'all_uv_color_dimensions_preserved':True})
# Clip only exact native wall plane over the photo supported storefront; preserve all floors/jambs/adjacent connector.
center=np.array([-36.5,newcenter[0],7]);along=np.array([cs,sn,0]);normal=np.array([-sn,cs,0]);rectangle=box(-3.5,7.01,3.5,10.45)
clip=[]
for role in ['native-main-shell6-render','native-main-shell6-collider']:
 rectangle=box(-3.5,7.01,3.5,10.45)if role.endswith('render')else box(-.9,7.01,.9,9.365)
 e=entries[role];d=data[role];owners=[r for r in S['renderers'if role.endswith('render')else'colliders']if r['path'].endswith('MainShell/OfficialStation_-부산역_6')];assert len(owners)==1;r=owners[0];M=np.array(r['world_matrix']);q=((d['vertices']@M[:3,:3].T+M[:3,3])@I.T)[:,[2,0,1]];nd=copy.deepcopy(d);newattrs={k:[]for k in ['vertices','normals','tangents','colors']};newuv=[[]for _ in range(8)];newsubs=[];changes=[];global_start=0
 for si,sub in enumerate(d['submeshes']):
  assert sub['topology']==0;newidx=[]
  for fi,idx in enumerate(sub['indices'].reshape(-1,3)):
   t=q[idx];distance=(t-center)@normal
   if max(abs(distance))>.002 or np.ptp(t[:,2])<.01:newidx.extend(idx.tolist());continue
   xx=(t-center)@along;pts=np.column_stack((xx,t[:,2]));poly=Polygon(pts);inter=poly.intersection(rectangle)
   if inter.area<1e-7:newidx.extend(idx.tolist());continue
   kept=poly.difference(rectangle);geoms=list(kept.geoms)if kept.geom_type=='MultiPolygon'else[kept];pieces=[]
   for g in geoms:
    if g.is_empty or g.area<1e-10:continue
    pieces += [tt for tt in triangulate(g)if g.covers(tt.representative_point())and tt.area>1e-10]
   assert abs(sum(p.area for p in pieces)-kept.area)<1e-7
   srcsigned=np.cross(pts[1]-pts[0],pts[2]-pts[0]);generated=[]
   A=np.column_stack((pts[1]-pts[0],pts[2]-pts[0]))
   for piece in pieces:
    pp=np.array(piece.exterior.coords)[:3]
    if np.cross(pp[1]-pp[0],pp[2]-pp[0])*srcsigned<0:pp=pp[[0,2,1]]
    triangle=[]
    for point in pp:
     ab=np.linalg.solve(A,point-pts[0]);bary=np.array([1-ab.sum(),*ab]);assert bary.min()>-1e-6
     # Original channels stay byte exact; every new corner receives barycentric source channel values.
     ni=len(d['vertices'])+len(newattrs['vertices']);triangle.append(ni)
     for key in newattrs:
      if len(d[key]):newattrs[key].append((bary@d[key][idx].astype(float)).astype('<f4'))
     for ui,u in enumerate(d['uv']):
      if len(u['data']):newuv[ui].append((bary@u['data'][idx].astype(float)).astype('<f4'))
     generated.append({'vertex_index':ni,'source_indices':idx.tolist(),'source_barycentric_weights':bary.tolist()})
    newidx.extend(triangle)
   changes.append({'global_triangle_ordinal':global_start+fi,'submesh':si,'submesh_triangle_ordinal':fi,'source_indices':idx.tolist(),'source_native_vertices':t.tolist(),'removed_area_model2':float(inter.area),'retained_area_model2':float(kept.area),'replacement_triangles':len(pieces),'generated_vertices':generated})
  newsubs.append({'topology':0,'indices':np.array(newidx,'<i4')});global_start+=len(sub['indices'])//3
 for key,arr in newattrs.items():
  if arr:nd[key]=np.concatenate((d[key],np.array(arr,'<f4')))
 for ui,arr in enumerate(newuv):
  if arr:nd['uv'][ui]['data']=np.concatenate((d['uv'][ui]['data'],np.array(arr,'<f4')))
 nd['submeshes']=newsubs;assert len(changes)>0
 output=write(nd,O/(role+'-namcheon-open.bin'))
 clip.append({'kind':'native_front_wall_clip','role':role,'source_mesh':MM[r['mesh']],'owner_receipt':owner_receipt(r),'full_channel_export':e,'source_live_asset_sha256':sha(e['asset']),'source_meta_sha256':sha(e['asset']+'.meta'),'plane_tolerance_model':.002,'clip_along_range_model':[-3.5,3.5]if role.endswith('render')else[-.9,.9],'clip_Y_range_model':[7.01,10.45]if role.endswith('render')else[7.01,9.365],'changed_source_faces':changes,'original_vertex_channels_byte_preserved':True,'extra_vertices_barycentric_source_channels':True,'all_unselected_triangles_preserved':True,'expected_output':output})
 print(role,len(changes),'removedarea',sum(x['removed_area_model2']for x in changes),'newverts',len(nd['vertices'])-len(d['vertices']))
# Door parent is transformed as one preserved subtree; local leaf geometry and animation vectors remain unchanged.
station_id=197238225;assert blocks[station_id][0]==114;station=parsed[station_id]['MonoBehaviour'];go=station['m_GameObject']['fileID'];tid=bygo[go][4][0]
old_fields={k:station[k]for k in ['Centre','Normal','Along']};new_fields={'Centre':dict(zip(['x','y','z'],newcenter.tolist())),'Normal':dict(zip(['x','y','z'],(Ax@np.array([-1.,0,0])).tolist())),'Along':dict(zip(['x','y','z'],(Ax@np.array([0,0,-1.])).tolist()))}
from scipy.spatial.transform import Rotation
def transform_world(id):
 z=parsed[id]['Transform'];p=z['m_LocalPosition'];r=z['m_LocalRotation'];sc=z['m_LocalScale'];L=np.eye(4);L[:3,:3]=Rotation.from_quat([r[k]for k in ['x','y','z','w']]).as_matrix()@np.diag([sc[k]for k in ['x','y','z']]);L[:3,3]=[p[k]for k in ['x','y','z']];father=z['m_Father']['fileID'];return transform_world(father)@L if father else L
parent_old=transform_world(tid);parent_new=DW@parent_old
# Show exact source controls as current/native/world points. No calibrated camera claim.
frontends=[[-36.5-3.5*cs,newcenter[0]-3.5*sn,7],[-36.5+3.5*cs,newcenter[0]+3.5*sn,7]]
world=lambda q:(F@np.array([q[1],q[2],q[0]])).tolist()
packet={'schema':'chooguard.namcheon-source-front-alignment.v1','status':'SEALED_ROOT_REVIEW','scene_epoch':meta['scene_sha256'],'inventory_geometry_epoch':S['scene_file_sha256'],'live_scene_sha256_at_seal':sha(scene),'native_frame':F.tolist(),'source_full_channel_metadata':str(MP),'source_full_channel_metadata_sha256':sha(MP),'photos':[{'path':str(Path('asset-library/research-public/2026-10-09/floor2-facility-field-sources')/('lockers-nose-'+z+'.jpg')),'sha256':sha(Path('asset-library/research-public/2026-10-09/floor2-facility-field-sources')/('lockers-nose-'+z+'.jpg'))}for z in ['006','008']],'guide_number':26,'unit_label':'남천할매떡볶이','positive_control':'Photo008 shows Namcheon glass/front and the already accepted native adjacent connector sharing a contiguous facade plane. Rigidly align only existing unit26 model envelope to exact native wall plane left of that connector.','target_frontline_V_from_U':[slope,intercept],'old_front_center_native_UVY':[-36.5,-3.31,7],'new_front_center_native_UVY':[-36.5,float(newcenter[0]),7],'front_endpoints_native_UVY':frontends,'front_endpoints_world':[world(q)for q in frontends],'rigid_yaw_radians':angle,'world_rigid_transform':DW.tolist(),'native_local_xyz_transform':DL.tolist(),'current_model_width_depth_preserved_not_field_dimensions':[7,7],'mesh_pose_patches':patches,'native_wall_clips':clip,'door_parent_pose':{'station_component':component_receipt(station_id),'parent_transform':component_receipt(tid),'gameobject_id':go,'station_fields_before':old_fields,'station_fields_after':old_fields,'recipe_fields_if_rebuilt_in_original_root_frame':new_fields,'world_delta':DW.tolist(),'old_parent_world_matrix':parent_old.tolist(),'expected_parent_world_matrix':parent_new.tolist(),'runtime_fields_are_parent_local_and_preserved':True,'leaf_receipts':doors,'preserve_all_other_station_fields':True,'preserve_local_leaf_TRS_mesh_channels_collider_boxes_and_animation_vectors':True},'source_support':{'path':str(E/'namcheon-candidate-source-floor-ceiling.json'),'sha256':sha(E/'namcheon-candidate-source-floor-ceiling.json'),'sampled_existing_depth_not_measurement':7,'source_floor':'ROOT/g58/g32/g4/geometry face33 Y7','source_ceiling':'ROOT/g58/g0/geometry face12 Y11','source_upper_deck':'ROOT/g58/g0/geometry face6 Y12.3'},'preserve':['all other guide unit faces/vertices','all source floor and ceiling geometry','existing accepted adjacent connector aperture and jambs','north 2F-3F escalator pair and all other circulation owners','unit26 inferred model width/depth and internal arrangement, no new hidden rooms','ALL door component fields including parent-local Centre/Normal/Along and leaf local mesh/animation vectors','source material slots and UV/channel dimensions'],'camera_fit':{'accepted':False,'reason':'No six independently unique photo-to-native3D control points. Photo008 front corners are largely coplanar; AV/AC pillar and visible upper escalator pair are not uniquely paired to native source owners. A camera fitted to generated7m dimensions would be circular evidence.'},'replay_contract':{'registry_target':'content/world/observed-interiors/namcheon-source-front-alignment.json','source_plan_overrides':['.planning/2026-09-23-video-twin/majibang-plan.json unit26 polygon/front','.planning/2026-09-24-interior-twin/specs/2f-main.json Storefront2F-main-front-04-남천할매떡볶이 + corresponding unit ceiling/envelope face scopes'],'exact_replay_or_strict_reject':True,'rule':'Install source hashes, mesh digests, owner IDs/frames, exact selected vertex sets and native clipping barycentric recipes. On regeneration apply only when complete source channels and pinned parent dependencies match; reject changed source. Do not regenerate/move unrelated complete main roots.','root_acceptance_required':True},'summary':{'render_uses':len(C['rows']),'unique_contributor_meshes':len(data)-2,'selected_renderer_triangles':sum(len(r['selected_triangle_ordinals'])for r in C['rows']),'unique_selected_mesh_triangles':sum(len(r['selected_triangle_ordinals'])for i,r in enumerate(C['rows'])if r['mesh']['id']not in {z['mesh']['id']for z in C['rows'][:i]}),'channel_patched_meshes':len(patches)+len(clip),'all_complete_source_digests_reproduced':True,'no_selected_unselected_shared_vertices':True,'source_floor_append_count':0,'extra_gallery_or_escalator_count':0,'hidden_room_measurements':0,'production_mutated_by_worker':False}}
(O/'namcheon-source-front-packet.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2)+'\n')
print('PACKET',sha(O/'namcheon-source-front-packet.json'),packet['summary'])
