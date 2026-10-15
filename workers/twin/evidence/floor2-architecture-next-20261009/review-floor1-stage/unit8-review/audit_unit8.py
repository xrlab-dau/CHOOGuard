from pathlib import Path
import json,hashlib,re,yaml,math,sys
import numpy as np
from shapely.geometry import Polygon,Point,LineString,shape
from shapely.ops import unary_union
from scipy.spatial.transform import Rotation
sys.path.insert(0,str(Path('workers/twin').resolve()))
from official_plan_compare import readbin
E=Path(__file__).resolve().parent
p=json.loads((E/'packet.json').read_text());s=json.loads((E/'inventory.json').read_text());rv=json.loads((E/'proposal.json').read_text());dy=json.loads((E/'door-native-yaml.json').read_text())
mm={x['id']:x for x in s['meshes']};rr={x['global_object_id']:x for x in s['renderers']+s['colliders']};checks=[];receipts=[];cache={};after={}
def sha(f):return hashlib.sha256(Path(f).read_bytes()).hexdigest()
def check(n,ok,d):checks.append({'id':n,'passed':bool(ok),'details':d})
def UV(w):return np.c_[.278991133*w[:,0]+.9602937*w[:,2],.9602937*w[:,0]-.278991133*w[:,2]]
def geo(r):
 mid=r['mesh']
 if mid not in cache:cache[mid]=readbin(mm[mid])
 v,t=cache[mid];M=np.array(r['world_matrix']);return v,t,v@M[:3,:3].T+M[:3,3]
old=Polygon(p['current_polygon_UV']);new=Polygon(p['proposed_polygon_UV']);front=LineString(np.array(p['current_polygon_UV'])[[4,5,6]])
core=Polygon(p['preserve']['end_core_approach_polygon_UV'])
check('saved_source_epoch',sha(s['scene'])==p['source_export_scene_sha256']==s['scene_file_sha256']==dy['scene_file_sha256_at_read'],s['scene_file_sha256'])
check('guide_bytes',sha('asset-library/research-public/2026-10-09/floor1-multiview-next/source05-yoonsscom/img_2.jpg')==p['guide_image_sha256'],p['guide_source'])
check('target_polygon_exact',p['proposed_polygon_UV']==rv['proposed_polygon_UV'] and new.is_valid,{'area':new.area,'added_area':new.difference(old).area})
check('protected_end_core_approach',new.intersection(core).area==0,{'overlap_area':new.intersection(core).area,'minimum_distance':new.distance(core)})
prior=[]
for folder in ['','unit16/','unit4/']:
 f=Path('workers/twin/evidence/floor2-architecture-next-20261009/review-floor1-stage')/folder/'packet.json'
 if f.exists():prior.append(json.loads(f.read_text()))
# Actual binary owners identify source contributors across renamed layered assets.
for qi,q in enumerate(p['mesh_replacements']):
 mid=q['source_mesh_id'];m=mm[mid];f=Path(q['source_asset']);body=f.read_bytes();a=yaml.safe_load(body.decode().split('Mesh:\n',1)[1]);raw=bytes.fromhex(a['m_VertexData']['_typelessdata']);nv=a['m_VertexData']['m_VertexCount'];st=len(raw)//nv;ch={}
 for i,c in enumerate(a['m_VertexData']['m_Channels']):
  if c['dimension']:ch[i]=np.ndarray((nv,c['dimension']),dtype='<f4',buffer=raw,offset=c['offset'],strides=(st,4)).copy()
 t=np.frombuffer(bytes.fromhex(a['m_IndexBuffer']),'<u4' if a['m_IndexFormat']==1 else '<u2').reshape(-1,3).astype(np.int64)
 fs=np.array(q['removed_triangle_ordinals'],int);ids=np.unique(t[fs]);keep=np.ones(len(t),bool);keep[fs]=False;kt=t[keep]
 check(f'source_file_{qi}',hashlib.sha256(body).hexdigest()==q['source_asset_file_sha256_at_read'],str(f))
 check(f'source_guid_{qi}',re.search(r'^guid: (.*)$',Path(str(f)+'.meta').read_text(),re.M)[1]==q['source_asset_guid']==m['asset_guid'],m['asset_guid'])
 v,bt=readbin(m);check(f'complete_binary_{qi}',sha(m['binary'])==q['source_binary_sha256'] and nv==q['source_vertex_count']==len(v) and len(t)==q['source_triangle_count'] and np.array_equal(v,ch[0]) and np.array_equal(t,bt),{'mesh':mid,'asset':str(f),'nv':nv,'nt':len(t)})
 check(f'exact_source_mask_{qi}',len(set(fs))==len(fs) and ids.tolist()==q['selected_vertex_indices'] and hashlib.sha256(b''.join(raw[i*st:(i+1)*st] for i in ids)).hexdigest()==q['selected_vertex_channel_bytes_sha256'] and hashlib.sha256(kt.astype('<u4').tobytes()).hexdigest()==q['retained_index_bytes_sha256'],{'removed_faces':len(fs),'selected_vertices':len(ids),'kept_faces':len(kt)})
 check(f'full_source_channels_{qi}',a['m_VertexData']['m_Channels']==q['source_channels'] and a['m_SubMeshes']==q['source_submeshes'] and set(ch)=={0,1,2,4} and st==48 and a['m_SubMeshes'][0]['topology']==0 and len(a['m_SubMeshes'])==1,{'stride':st,'channels':list(ch)})
 users=[r for r in s['renderers']+s['colliders'] if r.get('mesh')==mid];check(f'complete_owner_set_{qi}',{r['global_object_id'] for r in users}=={o['global_object_id'] for o in q['owners']},{'owners':len(users)})
 for oi,o in enumerate(q['owners']):
  r=rr[o['global_object_id']];check(f'owner_material_frame_{qi}_{oi}',o==r and m['asset']==q['source_asset'] and ('materials' not in r or r['materials']==q['source_materials']),{'path':o['path'],'matrix':o['world_matrix'],'materials':o.get('materials')})
  bv,bt,w=geo(r);quv=UV(w);pts=quv[t[fs]]
  inside=all(old.buffer(.045).covers(Point(z)) for z in pts.reshape(-1,2)) if '/점포 내부 1F/' in r['path'] else all(front.distance(Point(z))<.36001 for z in pts.reshape(-1,2)) if '4912_Walls' not in r['path'] else True
  check(f'local_unit8_contributor_{qi}_{oi}',inside,{'native_UV_bounds':[pts.min((0,1)).tolist(),pts.max((0,1)).tolist()]})
 gids={x['global_object_id'] for x in q['owners']};negative=[]
 for pp in prior:
  for z in pp.get('mesh_patches',[]):
   if not gids.intersection(x['global_object_id'] for x in z['owners']+z.get('collider_owners',[])):continue
   prev_ids=np.array(z['vertex_indices']);d=np.array(z['replacement_positions'])-z['expected_positions'];moved=prev_ids[np.linalg.norm(d,axis=1)>1e-7]
   ov=sorted(set(ids).intersection(moved.tolist()))
   if ov:negative.append({'batch':pp.get('batch_id'),'indices':ov})
 check(f'no_previous_units_removed_{qi}',not negative,negative)
 # Added indices are absolute and old complete channels are retained as prefix.
 av=np.array(q['append_vertices'],dtype='<f4');an=np.array(q['append_normals'],dtype='<f4');at=np.array(q['append_tangents'],dtype='<f4');au=np.array(q['append_uv0'],dtype='<f4');ti=np.array(q['added_triangles'],int);local=ti-nv
 check(f'added_complete_indices_{qi}',min(ti.ravel())>=nv and max(ti.ravel())<nv+len(av) and set(local.ravel())==set(range(len(av))) and len(av)==len(an)==len(at)==len(au),{'added_vertices':len(av),'added_faces':len(ti)})
 finite=all(np.isfinite(x).all() for x in [av,an,at,au]);cross=np.cross(av[local[:,1]]-av[local[:,0]],av[local[:,2]]-av[local[:,0]]);sq=(cross*cross).sum(1);dot=(cross*an[local].sum(1)).sum(1)
 nerr=float(abs((an*an).sum(1)-1).max());terr=float(abs((at[:,:3]*at[:,:3]).sum(1)-1).max());ort=float(abs((an*at[:,:3]).sum(1)).max())
 check(f'added_loader_attributes_{qi}',finite and nerr<=1e-4 and terr<=1e-4 and ort<=1e-4 and abs(abs(at[:,3])-1).max()<=1e-4,{'normal_squared_length_residual':nerr,'tangent_squared_length_residual':terr,'orthogonal_residual':ort})
 check(f'added_loader_winding_{qi}',sq.min()>=1e-12 and dot.min()>0,{'minimum_cross_squared_length':float(sq.min()),'min_winding_dot':float(dot.min()),'failed_face_ordinals':np.flatnonzero((sq<1e-12)|(dot<=0)).tolist()})
 check(f'new_selected_index_metadata_{qi}',q['source_index_format']==a['m_IndexFormat'] and q['selected_triangle_indices']==t[fs].tolist() and q['selected_index_bytes_sha256']==hashlib.sha256(t[fs].astype('<u4').tobytes()).hexdigest(),'Added final metadata matches exact source indices; geometry unchanged from initial snapshot')
 prefixpass=all(np.array_equal(np.concatenate([ch[k],x])[:nv],ch[k]) for k,x in [(0,av),(1,an),(2,at),(4,au)])
 check(f'full_prefix_retained_{qi}',prefixpass and q['preserve_original_vertex_channel_bytes'] and q['preserve_unrelated_triangle_order_indices_and_materials'],'Old position/normal/tangent/UV0 values and retained index ordering have exact unchanged prefixes in offline rebuild')
 r=users[0];M=np.array(r['world_matrix']);aw=av@M[:3,:3].T+M[:3,3];aq=UV(aw);after[mid]=(aw,local,aq)
 receipts.append({'mesh':mid,'source_asset':str(f),'source_file_sha256':sha(f),'meta_sha256':sha(str(f)+'.meta'),'source_full_channel_sha256':hashlib.sha256(raw).hexdigest(),'source_selected_sha256':q['selected_vertex_channel_bytes_sha256'],'removed_faces':len(fs),'added_vertices':len(av),'added_faces':len(ti),'retained_faces':len(kt),'appended_UV_bounds':[aq.min(0).tolist(),aq.max(0).tolist()]})
check('protected_native_owners_excluded',all(not o['path'].startswith('FPSWorld/') and not any(k in o['path'] for k in ['Ground_Floor','/Kit_Room','ToiletRoom','/1F 공용/']) for q in p['mesh_replacements'] for o in q['owners']) and not p['new_mesh_pieces'] and not p['native_floor_append_required'],'Ground/MainShell/Room/core/floor assets are outside all replacement rows')
# Horizontal finishes exactly fill target footprint, without unsupported hole.
for mid,y,n in [('mesh-0551',.004,1),('mesh-0907',3.,-1)]:
 aw,t,quv=after[mid];u=unary_union([Polygon(z) for z in quv[t]]);missing=new.difference(u.buffer(2e-5)).area;outside=u.difference(new.buffer(2e-5)).area
 check('horizontal_finish_'+mid,missing==0 and outside==0 and np.max(abs(aw[:,1]-y))<1e-6,{'missing_area_at_existing_float_tol':missing,'outside_area_at_existing_float_tol':outside,'area':u.area,'Y':y})
# Native support owner geometry is not modified; independently recompute supplied exact top faces.
support=[]
for i,f in enumerate(p['floor_support']['native_top_floor_owners']):
 r=rr[f['owner']['global_object_id']];c=rr[f['collider']['global_object_id']];v,t,w=geo(c);tri=w[t[f['faces']]];cross=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]);q=UV(w)
 check('native_floor_owner_'+str(i),r==f['owner'] and c==f['collider'] and c['enabled'] and c['active'] and not c['trigger'] and abs(tri[:,:,1]).max()<=.002 and cross[:,1].min()>0 and r['mesh'] not in after,{'mesh':r['mesh'],'faces':f['faces'],'worldYrange':[float(tri[:,:,1].min()),float(tri[:,:,1].max())]})
 support.extend(Polygon(z) for z in q[t[f['faces']]])
u=unary_union(support);check('native_floor_support_coverage',new.difference(u.buffer(2e-5)).area==0,{'target_area':new.area,'raw_uncovered_area':new.difference(u).area,'uncovered_at_existing_2e-5_tolerance':new.difference(u.buffer(2e-5)).area})
# Preserve all source fixture meshes; exact roots move once, no other unit descendants.
motions=p['world_rigid_motions'];check('bounded_motion_roots',len(motions)==7 and len({x['transform_file_id'] for x in motions})==7 and all('SHOP-1F-08' in x['path'] or '양산국밥' in x['path'] for x in motions),'Five fixtures, one existing fascia sign and one two-leaf StationDoor root')
common=old.intersection(new);fps=[]
for f in p['fixture_placements']:
 r=rr[f['renderer_id']];lo,hi=r['world_bounds_min'],r['world_bounds_max'];corners=np.array([[x,0,z] for x,z in [(lo[0],lo[2]),(hi[0],lo[2]),(hi[0],hi[2]),(lo[0],hi[2])]])
 mot=next((x for x in motions if x['path']=='실내 트윈 마감/점포 내부 1F/'+f['name']),None)
 if mot:corners=corners+np.array(mot['world_rigid_delta']['translation'])
 poly=Polygon(UV(corners));claimed=shape(f['footprint_UV']);fps.append((f['name'],poly))
 check('fixture_'+f['name'],common.buffer(-.09998).covers(poly) and poly.hausdorff_distance(claimed)<2e-5 and bool(mot)==(f['operation']=='translate'),{'method':'Actual renderer AABB transformed rigidly and projected','outside_area':poly.difference(new).area,'min_new_boundary_clearance':poly.distance(new.boundary),'claimed_hull_residual':poly.hausdorff_distance(claimed)})
check('no_fixture_mesh_edits',not any('/SHOP-1F-' in o['path'] for q in p['mesh_replacements'] for o in q['owners']),'Imported prefab channels/colliders retain exact source assets')
# One original door, both leafs: rigid frame maps centre and along direction.
dm=motions[0];Q=Rotation.from_quat(dm['world_rigid_delta']['rotation']).as_matrix();T=np.array(dm['world_rigid_delta']['translation']);dc=np.array(p['door']['new_centre_UV']);ex=np.array(p['door']['new_along_UV']);normal=np.array(p['door']['new_outward_normal_UV']);oldC=np.array(dm['actual_old_centre_world']);newC=Q@oldC+T
check('door_centre_rigid_map',np.linalg.norm(newC-np.array(dm['target_centre_world']))<1e-12 and np.linalg.norm(UV(newC[None])[0]-dc)<2e-5 and len(dy['leafs'])==2 and dy['StationDoor']['Element']==p['door']['element'],{'mapped_centre_UV':UV(newC[None])[0].tolist(),'existing_leafs':2})
check('door_nominal_aperture',abs(np.linalg.norm(np.diff(np.array(p['door']['nominal_aperture_endpoints_UV']),axis=0)[0])-1.8)<1e-12,{'nominal':1.8,'static_post_inner_clear':1.75,'fully_open_leaf_box_approx':1.77})
# Verify static collider clear gap at Y1 against actual float32 appended geometry.
aw,t,quv=after['mesh-3455'];intervals=[]
for tri in t:
 w=aw[tri];hit=[]
 for j in range(3):
  a,b=w[j],w[(j+1)%3]
  if (a[1]-1)*(b[1]-1)<=0 and a[1]!=b[1]:hit.append(a+(b-a)*(1-a[1])/(b[1]-a[1]))
 if len(hit)>=2:
  q=UV(np.array(hit));rel=q-dc;along=rel@ex;n=rel@normal
  if abs(n).max()<.25:intervals.append([float(along.min()),float(along.max())])
left=max(x[1] for x in intervals if x[1]<0);right=min(x[0] for x in intervals if x[0]>0)
check('static_glazing_collider_gap',left<=-.9+2e-5 and right>=.9-2e-5 and not any(a<.9-2e-5 and b>-.9+2e-5 for a,b in intervals),{'left_along':left,'right_along':right,'actual_static_collision_gap':right-left,'nominal_interval':[-.9,.9]})
# Paired existing rings/lenses have unchanged source channels and rigid local delta.
for i,l in enumerate(p['existing_light_placements']):
 q=next(q for q in p['mesh_replacements'] if q['source_mesh_id']==l['mesh']);ids=np.unique(readbin(mm[l['mesh']])[1][l['source_faces']]);oldv=readbin(mm[l['mesh']])[0][ids];piece=json.loads(Path(q['append_piece']).read_text());
 check('light_ownership_'+str(i),set(l['source_faces']).issubset(q['removed_triangle_ordinals']) and len(l['source_faces']) in [20,40],{'mesh':l['mesh'],'source_faces':l['source_faces'],'kind':l['kind']})
check('preserved_light_count',len(p['existing_light_placements'])==36 and sum(l['kind']=='shop' for l in p['existing_light_placements'])==16 and sum(l['kind']=='front' for l in p['existing_light_placements'])==20,'18 downlight discs plus 18 annuli; no count change')
# Exact source-disc channel and rigid-position copies, including all annuli.
light_offsets={}
for i,l in enumerate(p['existing_light_placements']):
 q=next(q for q in p['mesh_replacements'] if q['source_mesh_id']==l['mesh']);a=yaml.safe_load(Path(q['source_asset']).read_text().split('Mesh:\n',1)[1]);raw=bytes.fromhex(a['m_VertexData']['_typelessdata']);nv=a['m_VertexData']['m_VertexCount'];st=len(raw)//nv
 ch={k:np.ndarray((nv,c['dimension']),dtype='<f4',buffer=raw,offset=c['offset'],strides=(st,4)).copy() for k,c in enumerate(a['m_VertexData']['m_Channels']) if c['dimension']};t=readbin(mm[l['mesh']])[1];ids=np.unique(t[l['source_faces']]);o=light_offsets.get(l['mesh'],0);light_offsets[l['mesh']]=o+len(ids);dv=np.array(l['after_centre_UV'])-l['before_centre_UV'];delta=np.array([dv[1],0,dv[0]]);sl=slice(o,o+len(ids));imap={int(v):o+k for k,v in enumerate(ids)}
 expected=(ch[0][ids]+delta).astype('<f4');actual=np.array(q['append_vertices'],'<f4')[sl];attrs=all(np.array_equal(np.array(q[key],'<f4')[sl],ch[channel][ids]) for key,channel in [('append_normals',1),('append_tangents',2),('append_uv0',4)])
 local=np.array(q['added_triangles'])-nv;af=local[np.all((local>=o)&(local<o+len(ids)),1)];expectedt=[[imap[int(x)] for x in tri] for tri in t[l['source_faces']]]
 check('exact_rigid_light_copy_'+str(i),np.array_equal(actual,expected) and attrs and af.tolist()==expectedt,{'mesh':l['mesh'],'source_vertices':len(ids),'exact_original_normals_tangents_uv0':attrs,'rigid_translation_UV':dv.tolist()})
# Native source SHOP footprints identify untouched rear-row contributors.
shop_spec=json.loads(Path('content/world/observed-interiors/shops-1f.json').read_text());other_shops=[(e['id'],Polygon(e['geometry']['polygon'])) for e in shop_spec['elements'] if e['id']!='SHOP-1F-08']
for qi,q in enumerate(p['mesh_replacements']):
 r=q['owners'][0];v,t,w=geo(r);centres=UV(w)[t[q['removed_triangle_ordinals']]].mean(1);over=[]
 for name,poly in other_shops:
  inside=[int(q['removed_triangle_ordinals'][i]) for i,z in enumerate(centres) if poly.buffer(-.025).covers(Point(z))]
  if inside:over.append({'other_source_unit':name,'removed_face_ordinals':inside})
 check('no_other_shop_interior_contributor_'+str(qi),not over,over)
# One source StationDoor's unchanged fields and actual leaf box transforms.
leaf_receipts=[];FRAME=np.array(dm['before_world_matrix'])[:3,:3]
for i,l in enumerate(dy['leafs']):
 col=rr[next(g for g in rr if g.endswith('-'+str(p['door']['moving_leaf_collider_ids'][i])+'-0'))];M=np.array(col['world_matrix']);centre=np.array(col['local_center']);half=np.array(col['local_size'])/2
 corners=np.array([centre+half*np.array([x,y,z]) for x in [-1,1] for y in [-1,1] for z in [-1,1]]);w=corners@M[:3,:3].T+M[:3,3];closed=w@Q.T+T;rot=Rotation.from_quat([l['transform']['m_LocalRotation'][k] for k in ['x','y','z','w']]).as_matrix();slide=np.array([l['serialized_leaf']['Slide'][k] for k in ['x','y','z']]);dw=Q@FRAME@rot@slide;opened=closed+dw
 c=(UV(closed)-dc)@ex;z=(UV(opened)-dc)@ex;leaf_receipts.append({'collider':col['global_object_id'],'closed_along':[float(c.min()),float(c.max())],'fully_open_along':[float(z.min()),float(z.max())],'world_slide':dw.tolist(),'local_transform_sha256':hashlib.sha256(json.dumps(l['transform'],sort_keys=True).encode()).hexdigest(),'serialized_leaf_sha256':hashlib.sha256(json.dumps(l['serialized_leaf'],sort_keys=True).encode()).hexdigest()})
 check('door_leaf_source_protected_'+str(i),col['active'] and col['enabled'] and col['path'].startswith(dm['path']+'/') and l['serialized_leaf']==dy['StationDoor']['Leaves'][i] and col.get('mesh') not in after,leaf_receipts[-1])
leaf_clear=leaf_receipts[1]['fully_open_along'][0]-leaf_receipts[0]['fully_open_along'][1]
check('door_fully_open_actual_boxes',leaf_clear>1.76998 and leaf_clear<1.77002,{'actual_leaf_box_clear_width':leaf_clear,'nominal_authored_width':1.8,'static_post_inner_clear_width':1.75,'static_baked_collider_width':right-left})
# Root's final normalized schema must be semantically and byte-field equivalent.
n=json.loads((E/'normalized-packet.json').read_text());check('normalized_source_epoch',n['scene_sha256']==p['source_export_scene_sha256'] and n['proposal_sha256']==sha(E/'packet.json') and n['proposal']==str(Path('workers/twin/output/floor1-multiview-next-20261009/unit8-rebuild/unit8-replacement-packet.json')) and not n['rejected_owners'] and n['guide_sha256']==p['guide_image_sha256'] and sha(n['guide'])==n['guide_sha256'],'Root loader has explicit local guide path and pinned final raw packet')
check('normalized_counts',len(n['mesh_patches'])==17 and len(n['movable_transforms'])==7 and [u['boardNo'] for u in n['units']]==[8],{'normalized_meshes':len(n['mesh_patches']),'normalized_motions':len(n['movable_transforms'])})
for qi,(q,z) in enumerate(zip(p['mesh_replacements'],n['mesh_patches'])):
 fields={'source_asset':'source_asset','source_asset_guid':'source_asset_guid','source_asset_file_sha256_at_read':'source_file_sha256','source_vertex_count':'vertices','source_triangle_count':'triangles','removed_triangle_ordinals':'removed_triangle_ordinals','append_vertices':'append_vertices','append_normals':'append_normals','append_tangents':'append_tangents','append_uv0':'append_uv0','added_triangles':'added_triangles'}
 check('normalized_mesh_'+str(qi),all(q[a]==z[b] for a,b in fields.items()) and z['vertex_indices']==z['expected_positions']==z['replacement_positions']==[] and z['owners']==[x for x in q['owners'] if x.get('type')!='MeshCollider'] and z['collider_owners']==[x for x in q['owners'] if x.get('type')=='MeshCollider'],{'source_asset':q['source_asset'],'original_vertex_prefix_untouched':True,'added_absolute_indices_unchanged':True})
for i,(a,b) in enumerate(zip(motions,n['movable_transforms'])):
 check('normalized_motion_'+str(i),a['world_rigid_delta']==b['world_rigid_delta'] and a['path']==b['path'] and (i!=0 or b['kind']=='door' and b['door_element']==p['door']['element']),b)
check('normalized_additional_hashes',all(sha(x['path'])==x['sha256'] for x in n['additional_evidence']),n['additional_evidence'])
report={'schema':'chooguard.independent-unit8-append-review.v1','packet_sha256':sha(E/'packet.json'),'source_scene_sha256':s['scene_file_sha256'],'status':'PASS' if all(x['passed'] for x in checks) else 'REJECT','checks':checks,'source_receipts':receipts,'door_leaf_receipts':leaf_receipts,'normalized_packet_sha256':sha(E/'normalized-packet.json'),'summary':{'checks':len(checks),'passed':sum(x['passed'] for x in checks),'mesh_count':len(p['mesh_replacements']),'removed_faces':sum(len(q['removed_triangle_ordinals']) for q in p['mesh_replacements']),'added_faces':sum(len(q['added_triangles']) for q in p['mesh_replacements']),'world_rigid_motions':len(motions)},'runtime_verified':False,'production_mutated':False}
(E/'unit8-review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'status':report['status'],'summary':report['summary'],'failures':[x for x in checks if not x['passed']]},ensure_ascii=False,indent=2))
