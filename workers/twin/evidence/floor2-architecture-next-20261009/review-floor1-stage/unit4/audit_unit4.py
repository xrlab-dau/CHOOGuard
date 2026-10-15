"""Independent bounded Unit4 packet audit. Evidence writes only."""
from pathlib import Path
import sys,json,hashlib,re,math
import numpy as np
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
from shapely.geometry import Polygon,Point,LineString
sys.path.insert(0,str(Path('workers/twin').resolve()))
from official_plan_compare import readbin
EV=Path(__file__).resolve().parent
def load(name):return json.loads((EV/name).read_text())
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
p,s,proposal,fixtures,lights=map(load,['packet.json','current-inventory.json','proposal.json','fixture-attribution-masks.json','facade-downlight-masks.json'])
mm={m['id']:m for m in s['meshes']};rr={r['global_object_id']:r for r in s['renderers']+s['colliders']};cache={};checks=[];patch_receipts=[];after_data={};before_data={};mapping_anomalies=[]
old=np.array(p['units'][0]['before']['polygon']);new=np.array(p['units'][0]['after']['polygon']);oldpoly,newpoly=Polygon(old),Polygon(new)
sin,cos=math.sin(math.radians(16.2)),math.cos(math.radians(16.2))
def uv(w):return np.c_[w[:,0]*sin+w[:,2]*cos,w[:,0]*cos-w[:,2]*sin]
def check(name,passed,details):checks.append({'id':name,'passed':bool(passed),'details':details})
def geo(r):
 m=mm[r['mesh']]
 if m['binary'] not in cache:cache[m['binary']]=readbin(m)
 v,t=cache[m['binary']];wm=np.array(r['world_matrix']);return m,v,t,v@wm[:3,:3].T+wm[:3,3]
patches={q['source_asset']:q for q in p['mesh_patches']}
installation=Path('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/unit4-installation.json')
epoch_ok=sha(p['scene'])==p['scene_sha256']==s['scene_file_sha256'];epoch_details={'staged_scene_sha256':p['scene_sha256'],'inventory_scene_sha256':s['scene_file_sha256']}
if not epoch_ok and installation.exists():
 receipt=json.loads(installation.read_text());epoch_ok=receipt['before_scene_sha256']==p['scene_sha256']==s['scene_file_sha256'] and receipt['packet_sha256']==sha(EV/'packet.json') and receipt['scene_sha256']==sha(p['scene']);epoch_details['root_installation_receipt']=str(installation);epoch_details['interpretation']='Root-owned saved receipt read only; no child Unity/runtime validation assertion.'
check('current_scene_source_epoch',epoch_ok,epoch_details)
check('reference_hashes',sha(p['guide'])==p['guide_sha256'] and sha(p['proposal'])==p['proposal_sha256'] and sha(p['fixture_attribution'])==p['fixture_attribution_sha256'] and all(sha(x['path'])==x['sha256'] for x in p.get('additional_evidence',[])),{'guide':p['guide'],'proposal':p['proposal'],'fixtures':p['fixture_attribution']})
check('only_unit4_no_rejections',[u['boardNo'] for u in p['units']]==[4] and not p['rejected_owners'],p['units'])
check('fixed_native_polygon_vertices',np.array_equal(old[1:7],new[1:7]) and np.array_equal(new,np.array(proposal['operations'][0]['proposed_polygon_UV'])),{'fixed_indices':[1,2,3,4,5,6],'new_polygon_UV':new.tolist()})
check('new_footprint_subset',newpoly.is_valid and newpoly.difference(oldpoly).area==0,{'freed_area_model_units_squared':oldpoly.difference(newpoly).area,'outside_area':newpoly.difference(oldpoly).area})
check('preserved_flush_floor_assets',not any(any(z in o['path'] for z in ['Ground_Floor','Kit_Floor_']) for q in p['mesh_patches'] for o in q['owners']), 'Original floor geometry under both new store and released strip is not edited')
check('no_native_shell_or_public_room_edits',not any(o['path'].startswith('FPSWorld/') or '/1F 공용/' in o['path'] or '/Kit_Room' in o['path'] for q in p['mesh_patches'] for o in q['owners']+q.get('collider_owners',[])), 'All native MainShell/plaza owners and public Room/core owners protected')
for qi,q in enumerate(p['mesh_patches']):
 check('source_asset_bytes_'+str(qi),sha(q['source_asset'])==q['source_file_sha256'],q['source_asset'])
 check('source_asset_guid_'+str(qi),re.search(r'^guid: (.*)$',Path(q['source_asset']+'.meta').read_text(),re.M)[1]==q['source_asset_guid'],q['source_asset_guid'])
 users=[r for r in s['renderers'] if mm.get(r['mesh'],{}).get('asset')==q['source_asset']];cols=[r for r in s['colliders'] if mm.get(r.get('mesh'),{}).get('asset')==q['source_asset']]
 check('all_renderer_instances_'+str(qi),{x['global_object_id'] for x in users}=={x['global_object_id'] for x in q['owners']},{'renderers':len(users),'colliders':len(cols)})
 check('all_collider_instances_'+str(qi),{x['global_object_id'] for x in cols}=={x['global_object_id'] for x in q['collider_owners']} if 'collider_owners' in q else all(any(c['world_matrix']==o['world_matrix'] for o in q['owners']) for c in cols),'Explicit collider masks or exact shared renderer frame')
 ids=np.array(q['vertex_indices'],int);ti=np.array(q['selected_triangles'],int);expected=np.array(q['expected_positions']);replacement=np.array(q['replacement_positions'])
 for oi,o in enumerate(q['owners']+q.get('collider_owners',[])):
  r=rr[o['global_object_id']];m,v,t,w=geo(r);wm=np.array(o['world_matrix']);after=v.astype(float).copy();after[ids]=replacement;after32=after.astype(np.float32).astype(float);aw=after32@wm[:3,:3].T+wm[:3,3]
  check(f'owner_frame_{qi}_{oi}',o['path']==r['path'] and o['world_matrix']==r['world_matrix'] and m['asset']==q['source_asset'],o)
  check(f'exact_source_binary_{qi}_{oi}',m['binary_sha256']==q['source_binary_sha256'] and len(v)==q['vertices'] and len(t)==q['triangles'] and np.array_equal(v[ids],expected),{'mesh':m['id'],'binary':m['binary'],'sha256':m['binary_sha256']})
  mask=np.zeros(len(v),bool);mask[ids]=True;tm=np.zeros(len(t),bool);tm[ti]=True
  check(f'index_mask_closure_{qi}_{oi}',np.array_equal(np.unique(t[ti]),ids) and np.array_equal(np.flatnonzero(mask[t].any(1)),ti),'Every indexed face sharing selected vertices moves coherently')
  _,weld=np.unique(np.round(v,5),axis=0,return_inverse=True);a,b=weld[t].ravel(),weld[t[:,[1,2,0]]].ravel();_,wl=connected_components(coo_matrix((np.ones(len(a)),(a,b)),shape=(weld.max()+1,weld.max()+1)),directed=False);labels=wl[weld]
  referenced=np.zeros(len(v),bool);referenced[np.unique(t)]=True
  split=[int(x) for x in np.unique(weld[ids]) if not mask[(weld==x)&referenced].all()]
  incomplete=[int(x) for x in np.unique(labels[t[ti,0]]) if not tm[labels[t[:,0]]==x].all()]
  # Float32 source corners rounded into one weld may differ by a few ULPs.
  # Record these independently from exact-coordinate weld splits.
  exact_positions={}
  for vi in np.unique(t):exact_positions.setdefault(tuple(v[vi]),[]).append(int(vi))
  exact_torn=[vv for vv in exact_positions.values() if mask[vv].any() and np.max(abs((after32[vv]-v[vv])-(after32[vv[0]]-v[vv[0]])))>0]
  rounded_delta_max=max((float(np.linalg.norm((after32[(weld==x)&referenced]-v[(weld==x)&referenced])-(after32[ids[np.flatnonzero(weld[ids]==x)[0]]]-v[ids[np.flatnonzero(weld[ids]==x)[0]]]),axis=1).max()) for x in np.unique(weld[ids])),default=0.)
  check(f'weld_component_closure_{qi}_{oi}',not split and not incomplete and not exact_torn,{'split_rounded_welds':split,'incomplete_components':incomplete,'exact_coordinate_torn_groups':exact_torn,'max_rounded_weld_delta_residual_float32':rounded_delta_max})
  bv=v[t[ti]].astype(float);av=after32[t[ti]];nb=np.cross(bv[:,1]-bv[:,0],bv[:,2]-bv[:,0]);na=np.cross(av[:,1]-av[:,0],av[:,2]-av[:,0]);area_before=np.linalg.norm(nb,axis=1);area_after=np.linalg.norm(na,axis=1);good=area_before>1e-12
  flips=ti[good&(np.sum(nb*na,axis=1)<=0)];collapses=ti[good&(area_after<=1e-12)]
  angles=np.degrees(np.arccos(np.clip(np.sum(nb[good]*na[good],axis=1)/area_before[good]/area_after[good],-1,1)))
  check(f'no_face_flip_or_collapse_{qi}_{oi}',not len(flips) and not len(collapses),{'flipped_source_triangle_ordinals':flips.tolist(),'collapsed_source_triangle_ordinals':collapses.tolist(),'max_geometric_normal_change_degrees':float(angles.max()) if len(angles) else 0.})
  point_uv=uv(w);after_uv=uv(aw);fixed=[]
  for corner in old[1:7]:fixed.extend(np.flatnonzero(np.linalg.norm(point_uv-corner,axis=1)<1e-5).tolist())
  check(f'fixed_actual_source_corners_{qi}_{oi}',not fixed or np.array_equal(after32[fixed],v[fixed]),{'source_vertex_indices':sorted(set(fixed)),'fixed_corner_float32_max_delta':float(np.max(abs(after32[fixed]-v[fixed]))) if fixed else 0.})
  check(f'unchanged_vertical_profile_{qi}_{oi}',np.array_equal(after32[:,1],v[:,1]),'Y coordinates stay byte-equivalent float32')
  if oi==0:before_data[q['source_asset']]=(v,t,w,point_uv,r);after_data[q['source_asset']]=(after32,t,aw,after_uv,r)
  patch_receipts.append({'source_asset':q['source_asset'],'owner':o,'selected_vertices':len(ids),'selected_triangles':len(ti),'total_vertices':len(v),'total_triangles':len(t),'actual_after_uv_bounds':[after_uv[ids].min(0).tolist(),after_uv[ids].max(0).tolist()]})

# Negative source contribution against both installed preceding packets. Vertex
# indexing remains stable in the installed guide layers; no Unit4 topology edit.
registry=load('registry.json');previous_packets={}
for row in registry['meshes']:
 if row['replacement_asset'] not in patches:continue
 name=row['packet']
 if name not in previous_packets:previous_packets[name]=json.loads(Path(name).read_text())
 oldpacket=previous_packets[name];oldrow=next((x for x in oldpacket['mesh_patches'] if x['source_asset']==row['baseline_asset']),None)
 if oldrow is None:oldrow=next((x for x in oldpacket['mesh_patches'] if x['source_asset']==row.get('source_asset')),None)
 if oldrow is None:continue
 # Some layered registry rows preserve the earliest builder baseline, while the
# latest packet's immediate source is a previous replacement. Resolve by packet
# owner identity rather than guessing a filename.
for q in p['mesh_patches']:
 overlapping=[]
 for pp in previous_packets.values():
  gids={o['global_object_id'] for o in q['owners']+q.get('collider_owners',[])}
  for prev in pp['mesh_patches']:
   if not gids.intersection(o['global_object_id'] for o in prev['owners']+prev.get('collider_owners',[])):continue
   prev_ids=np.array(prev['vertex_indices']);d=np.array(prev['replacement_positions'])-prev['expected_positions'];moved=prev_ids[np.linalg.norm(d,axis=1)>1e-7];same=sorted(set(q['vertex_indices']).intersection(moved.tolist()))
   if same:overlapping.append({'previous_packet_batch':pp['batch_id'],'vertex_indices':same})
 check('preserve_prior_unit_contributors_'+Path(q['source_asset']).name,not overlapping,{'source_asset':q['source_asset'],'previous_moved_vertex_overlap':overlapping})

# Complete explicit fixture assemblies, with one rigid delta across every role.
assembly_receipts=[]
for fi in (1,2,3,4):
 rows=[r for r in fixtures['mesh_mask_rows'] if r['sourceIndex']==fi];deltas=[];collider_points=[];outside=[]
 for r in rows:
  q=patches.get(r['source_asset']);v,t,w,uvs,owner=before_data[r['source_asset']];a,_,aw,au,_=after_data[r['source_asset']];vi=np.array(r['vertex_indices']);selected=set(q['vertex_indices']) if q else set()
  check('assembly_coverage_'+str(fi)+'_'+r['role']+'_'+Path(r['source_asset']).name,set(vi).issubset(selected),{'sourceIndex':fi,'role':r['role'],'source_vertices':len(vi)})
  deltas.extend((aw[vi]-w[vi]).tolist());out=[int(x) for x in vi if not newpoly.buffer(1e-5).covers(Point(au[x]))];outside.extend({'source_asset':r['source_asset'],'vertex':x,'after_UV':au[x].tolist()} for x in out)
  if r['role']=='collider':collider_points.extend(au[vi].tolist())
 ds=np.array(deltas);rigid_error=float(np.linalg.norm(ds-ds[0],axis=1).max())
 if fi>1:check('rigid_whole_gondola_'+str(fi),rigid_error<1e-5,{'sourceIndex':fi,'all_role_max_world_delta_residual_float32':rigid_error,'common_world_delta':ds.mean(0).tolist()})
 check('whole_fixture_inside_new_shop_'+str(fi),not outside,{'sourceIndex':fi,'outside_vertices':outside})
 points=np.array(collider_points);clearance=float(new[0,1]-points[:,1].max());assembly_receipts.append({'sourceIndex':fi,'fixture':rows[0]['fixture'],'collider_UV_bounds':[points.min(0).tolist(),points.max(0).tolist()],'aisle_front_V_clearance_model_units':clearance,'rigid_delta_residual_float32':rigid_error})
check('first_gondola_aisle_inset',assembly_receipts[1]['aisle_front_V_clearance_model_units']>.07999,assembly_receipts[1])

# Authored fascia rings and lenses retain their dimensions and common anchor delta.
light_receipts=[]
for fixture_id in sorted(set(r['fixture_id'] for r in lights['mesh_mask_rows'])):
 rows=[r for r in lights['mesh_mask_rows'] if r['fixture_id']==fixture_id];deltas=[];selected_counts=[]
 for r in rows:
  ids=np.array(r['vertex_indices']);q=patches.get(r['source_asset']);selected=set(q['vertex_indices']) if q else set();is_fixed=r['preserve_fixed_frontage']
  check('exact_facade_fixture_mask_'+fixture_id+'_'+r['role'],not set(ids).intersection(selected) if is_fixed else set(ids).issubset(selected),{'role':r['role'],'fixed':is_fixed,'source_vertices':len(ids)})
  if is_fixed:continue
  v,t,w,uvs,owner=before_data[r['source_asset']];a,_,aw,au,_=after_data[r['source_asset']];deltas.extend((aw[ids]-w[ids]).tolist())
 if deltas:
  ds=np.array(deltas);err=float(np.linalg.norm(ds-ds[0],axis=1).max());check('rigid_ring_and_lens_'+fixture_id,err<1e-5,{'max_world_delta_residual_float32':err});light_receipts.append({'fixture_id':fixture_id,'world_delta':ds.mean(0).tolist(),'max_delta_residual_float32':err})

# Fixed explicit source facades1/5 and moving facade stock all remain covered.
fronts=[e for e in load('source-frontages.json') if e['type']=='Storefront'];frontage_receipts=[]
for e in fronts:
 a,b=np.array(e['geometry']['edge']);length=np.linalg.norm(b-a);ex=(b-a)/length;normal=np.array([-ex[1],ex[0]]);line=LineString([a,b]);fixed=e['id'].endswith('front1') or e['id'].endswith('front5')
 for r in s['renderers']+s['colliders']:
  if not r['active'] or not r['path'].startswith('실내 트윈 마감/1F/') or not any(tag in r['path'].split('/')[2] for tag in ['Glass','Frame','Fascia','Lightbox','Clad','Joint','Trim','Colliders']):continue
  m,v,t,w=geo(r);uvs=uv(w);tv=uvs[t];along=(tv-a)@ex;depth=(tv-a)@normal;yb=w[t,1].min(1);yt=w[t,1].max(1)
  # All corners must lie inside this explicit source frontage prism. Source
# profiles extend up to .254 outward and .16 inward, with .025 post caps.
  candidate=np.flatnonzero((along.min(1)>=-.0251)&(along.max(1)<=length+.0251)&(abs(depth).max(1)<.351)&(yb>=-.001)&(yt<=4.981))
  if not len(candidate):continue
  q=patches.get(m['asset']);sel=set(q['selected_triangles']) if q else set();points=np.unique(t[candidate]);after=after_data.get(m['asset'],(v,t,w,uvs,r))[0]
  if fixed:
   changed=points[np.any(after[points]!=v[points],axis=1)]
   check('fixed_frontage_'+e['id']+'_'+r['path'],not len(changed),{'source_triangle_ordinals':candidate.tolist(),'changed_source_vertices':changed.tolist()})
  else:
   unselected=[int(x) for x in candidate if int(x) not in sel]
   check('moving_frontage_coverage_'+e['id']+'_'+r['path'],not unselected,{'candidate_faces':len(candidate),'unselected_source_triangle_ordinals':unselected})
  frontage_receipts.append({'element':e['id'],'owner':r['path'],'source_asset':m['asset'],'fixed':fixed,'source_triangle_ordinals':candidate.tolist()})

# Actual renderer/collider child fixtures must use exactly the one counter motion.
motions=p['movable_transforms'];parents={x['path']:x for x in motions if x['kind']=='path'};doors={x['door_element']:x for x in motions if x['kind']=='door'};counter='실내 트윈 마감/점포 내부 1F/SHOP-1F-04 counter 0'
check('exact_counter_and_door_motion_set',set(parents)=={counter} and set(doors)=={'1F-unit04-front6','1F-unit04-front8'} and all(x['boardNo']==4 for x in motions),motions)
aperture_receipts=[]
col_asset=next(q['source_asset'] for q in p['mesh_patches'] if any(o['path']=='실내 트윈 마감/1F/Kit_Colliders' for o in q.get('collider_owners',[])))
cv,ct,cw,cu,cr=before_data[col_asset];ca,_,caw,cau,_=after_data[col_asset]
for edge,at in [(6,9.677),(8,3.354)]:
 aa,bb=old[edge],old[(edge+1)%len(old)];AA,BB=new[edge],new[(edge+1)%len(new)];ex=(bb-aa)/np.linalg.norm(bb-aa);EX=(BB-AA)/np.linalg.norm(BB-AA);normal=np.array([-ex[1],ex[0]])
 tv=cu[ct];along=(tv-aa)@ex;depth=(tv-aa)@normal
 eligible=np.flatnonzero((abs(depth).max(1)<1e-5)&(along.min(1)>=-.0001)&(along.max(1)<=np.linalg.norm(bb-aa)+.0001)&(cw[ct,1].min(1)>=-.0001)&(cw[ct,1].max(1)<=3.0001))
 before_intervals=np.c_[along[eligible].min(1),along[eligible].max(1)]
 after_along=(cau[ct[eligible]]-AA)@EX;after_intervals=np.c_[after_along.min(1),after_along.max(1)]
 center=aa+ex*at;delta=np.array(doors['1F-unit04-front'+str(edge)]['world_delta']);center_after=center+np.array([delta[0]*sin+delta[2]*cos,delta[0]*cos-delta[2]*sin]);target_at=float(np.dot(center_after-AA,EX))
 left=float(before_intervals[before_intervals[:,1]<at,1].max());right=float(before_intervals[before_intervals[:,0]>at,0].min());LEFT=float(after_intervals[after_intervals[:,1]<target_at,1].max());RIGHT=float(after_intervals[after_intervals[:,0]>target_at,0].min())
 receipt={'element':'1F-unit04-front'+str(edge),'source_asset':col_asset,'source_triangle_ordinals':eligible.tolist(),'source_nominal_aperture_width':1.8,'actual_static_glazing_collision_gap_before':right-left,'actual_static_glazing_collision_gap_after':RIGHT-LEFT,'actual_gap_center_offset_from_moved_door':(RIGHT+LEFT)*.5-target_at,'actual_left_static_clearance_beyond_nominal_door_interval':target_at-.9-LEFT,'actual_right_static_clearance_beyond_nominal_door_interval':RIGHT-target_at-.9,'original_static_gap_unchanged':abs((right-left)-(RIGHT-LEFT))<1e-5,'explanation':'The original static glazing gap is1.85 because glazing stops0.025 short of each nominal1.8 aperture jamb center. The facade fit scales those outer setbacks, changing the static gap. Both new gaps fully contain the moved nominal1.8 interval. No unchanged1.85 or field measurement claim.'}
 check('static_collision_contains_nominal_aperture_'+str(edge),LEFT<=target_at-.9+1e-5 and RIGHT>=target_at+.9-1e-5,receipt);aperture_receipts.append(receipt)
counter_receipts=[]
for r in s['renderers']+s['colliders']:
 if not r['active'] or not r['path'].startswith(counter+'/'):continue
 m=mm.get(r.get('mesh'));classification='exact exported geometry'
 if m and m['binary']:m,v,t,w=geo(r)
 else:
  low,high=np.array(r['world_bounds_min']),np.array(r['world_bounds_max']);w=np.array([[x,y,z] for x in [low[0],high[0]] for y in [low[1],high[1]] for z in [low[2],high[2]]]);classification='conservative current world AABB; imported source geometry not exported'
 aw=w+np.array(parents[counter]['world_delta']);points=uv(aw);outside=[int(i) for i,x in enumerate(points) if not newpoly.buffer(1e-5).covers(Point(x))]
 check('counter_contains_'+r['global_object_id'],not outside,{'owner':r['path'],'outside_vertices':outside,'classification':classification,'after_UV_bounds':[points.min(0).tolist(),points.max(0).tolist()]});counter_receipts.append({'owner':r['path'],'mesh':m['id'] if m else None,'classification':classification,'after_UV_bounds':[points.min(0).tolist(),points.max(0).tolist()]})

report={'schema':'chooguard.unit4-stage-independent-review.v1','packet_sha256':sha(EV/'packet.json'),'inventory_sha256':sha(EV/'current-inventory.json'),'scene_sha256':s['scene_file_sha256'],'checks':checks,'all_checks_passed':all(x['passed'] for x in checks),'meshes':len(p['mesh_patches']),'selected_vertices':sum(len(x['vertex_indices']) for x in p['mesh_patches']),'renderer_owners':sum(len(x['owners']) for x in p['mesh_patches']),'generic_collider_owners':sum(len(x.get('collider_owners',[])) for x in p['mesh_patches']),'patch_receipts':patch_receipts,'assembly_receipts':assembly_receipts,'facade_light_receipts':light_receipts,'source_frontage_receipts':frontage_receipts,'static_aperture_receipts':aperture_receipts,'counter_receipts':counter_receipts,'motions':motions,'production_writes':0,'site_accuracy_verified':False,'remaining_gate':'Root owns actual installation/runtime/nav/render, actual door play checks and final source-floor proof; this is bounded read-only source ownership and target geometry QA.'}
(EV/'unit4-review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'checks':len(checks),'failed':[x['id'] for x in checks if not x['passed']],'meshes':report['meshes'],'vertices':report['selected_vertices'],'assembly_receipts':assembly_receipts,'report_sha256':sha(EV/'unit4-review.json')},ensure_ascii=False))
