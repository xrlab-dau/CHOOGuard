"""Bounded independent read-only review; writes only this review folder."""
from pathlib import Path
import sys,json,re,math,hashlib
import numpy as np
import yaml
from shapely.geometry import Polygon,LineString,Point
from shapely import contains_xy
sys.path.insert(0,str(Path('workers/twin').resolve()))
from official_plan_compare import readbin
EV=Path('workers/twin/evidence/floor2-architecture-next-20261009/review-floor1-stage')
PACKET=Path('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-packet.json')
BASE=Path('.planning/2026-10-08-interior-runtime-completion/architecture/after-busanok-finishes/current-scene-structure.json')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
p=json.loads(PACKET.read_text());s=json.loads(BASE.read_text());proposal=json.loads(Path(p['proposal']).read_text());units=proposal['operations'];mm={m['id']:m for m in s['meshes']};rr={r['global_object_id']:r for r in s['renderers']};polys=[Polygon(u['current_polygon_UV']) for u in units];cache={};checks=[];findings=[]
sin,cos=math.sin(math.radians(16.2)),math.cos(math.radians(16.2))
def uv(w):return np.c_[w[:,0]*sin+w[:,2]*cos,w[:,0]*cos-w[:,2]*sin]
def geo(r):
 m=mm[r['mesh']]
 if m['id'] not in cache:cache[m['id']]=readbin(m)
 v,t=cache[m['id']];wm=np.array(r['world_matrix']);return m,v,t,v@wm[:3,:3].T+wm[:3,3]
def chk(id,ok,details):checks.append({'id':id,'passed':bool(ok),'details':details})
install_path=Path('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-installation.json')
if install_path.exists():
 install=json.loads(install_path.read_text());chk('scene_installation_epoch',install['before_scene_sha256']==p['scene_sha256'] and install['scene_sha256']==sha(p['scene']) and install['packet_sha256']==sha(PACKET),{'before_scene_sha256':p['scene_sha256'],'after_scene_sha256':install['scene_sha256'],'interpretation':'Read-only saved installation receipt; actual Unity runtime tests remain root-owned'})
else:chk('scene_current_sha',sha(p['scene'])==p['scene_sha256'],p['scene_sha256'])
chk('guide_sha',sha(p['guide'])==p['guide_sha256'],p['guide'])
chk('proposal_sha',sha(p['proposal'])==p['proposal_sha256'],p['proposal'])
patch_rows=[];coordinate_splits=[];patches={q['source_asset']:q for q in p['mesh_patches']}
for pi,q in enumerate(p['mesh_patches']):
 if 'boh_evidence' in q:continue
 ids=np.array(q['vertex_indices'],int);seltri=np.array(q['selected_triangles'],int);expected=np.array(q['expected_positions']);replacement=np.array(q['replacement_positions']);delta=replacement-expected
 chk(f'asset_sha_{pi}',sha(q['source_asset'])==q['source_file_sha256'],q['source_asset'])
 ownership=[]
 for oi,owner in enumerate(q['owners']):
  r=rr[owner['global_object_id']];m,v,t,world=geo(r);wm=np.array(owner['world_matrix'])
  chk(f'owner_frame_{pi}_{oi}',r['path']==owner['path'] and np.max(abs(wm-np.array(r['world_matrix'])))==0,owner['path'])
  chk(f'binary_and_positions_{pi}_{oi}',m['binary_sha256']==q['source_binary_sha256'] and len(v)==q['vertices'] and len(t)==q['triangles'] and np.array_equal(v[ids],expected),m['id'])
  mask=np.zeros(len(v),bool);mask[ids]=True;tm=np.zeros(len(t),bool);tm[seltri]=True
  chk(f'closed_masks_{pi}_{oi}',np.array_equal(np.flatnonzero(np.any(mask[t],axis=1)),seltri) and np.array_equal(np.unique(t[seltri]),ids),{'selected_triangles':len(seltri),'selected_vertices':len(ids)})
  by_position={}
  for vi,pt in enumerate(v):by_position.setdefault(tuple(np.round(pt,5)),[]).append(vi)
  split_groups=[vv for vv in by_position.values() if mask[vv].any() and not mask[vv].all()]
  if split_groups and oi==0:
   coordinate_splits.append({'asset':q['source_asset'],'owner':owner['path'],'groups':len(split_groups),'selected_unselected_coincident_vertex_groups':split_groups})
  full_after=v.astype(float).copy();full_after[ids]=replacement
  split_delta=[]
  for vv in by_position.values():
   if len(vv)>1 and mask[vv].all() and np.linalg.norm((full_after[vv]-v[vv])-(full_after[vv[0]]-v[vv[0]]),axis=1).max()>2e-5:split_delta.append(vv)
  chk(f'welded_position_closure_{pi}_{oi}',not split_groups and not split_delta,{'mask_split_groups':len(split_groups),'delta_split_groups':len(split_delta),'position_weld_rounding_decimal_places':5})
  worldd=delta@wm[:3,:3].T;want=np.array([x['world_delta'] for x in p['units']]);dist=np.linalg.norm(worldd[:,None,:]-want[None,:,:],axis=2);labels=dist.argmin(1);err=dist.min(1)
  corrected_err=err.copy();snap_count=0
  if any(z in owner['path'] for z in ['_Walls','ShopWall','ShopCeiling','Kit_Floor','Ground_ShopCeiling']):
   old=np.array(units[5]['current_polygon_UV']);new=np.array(units[5]['proposed_polygon_UV']);base_delta=np.array(p['units'][5]['world_delta']);base_uv_delta=np.array([0,np.linalg.norm(base_delta)])
   points=uv(world[ids]);a,b=old[2],old[3];f=np.clip(((points-a)@(b-a))/np.dot(b-a,b-a),0,1);foot=a+f[:,None]*(b-a);allow=(labels==5)&(np.linalg.norm(points-foot,axis=1)<.12)
   newfoot=new[2]+f[:,None]*(new[3]-new[2]);corr_uv=newfoot-foot-base_uv_delta;corr_world=np.c_[corr_uv[:,0]*sin+corr_uv[:,1]*cos,np.zeros(len(ids)),corr_uv[:,0]*cos-corr_uv[:,1]*sin]
   corrected_err[allow]=np.linalg.norm(worldd[allow]-base_delta-corr_world[allow],axis=1);snap_count=int(allow.sum())
  chk(f'unit_delta_{pi}_{oi}',float(corrected_err.max())<1e-5,{'raw_max_error':float(err.max()),'checked_snap_vertices':snap_count,'after_known_snap_max_error':float(corrected_err.max())})
  labelsall=np.full(len(v),-1,int);labelsall[ids]=labels;tri_labels=labelsall[t[seltri]]
  chk(f'rigid_faces_{pi}_{oi}',np.all(tri_labels==tri_labels[:,0,None]),'No selected triangle mixes unit translations')
  uvs=uv(world);spatial=[]
  for j,u in enumerate(units):
   vi=ids[labels==j];ti=seltri[tri_labels[:,0]==j]
   if not len(vi):continue
   inside=contains_xy(polys[j].buffer(.351),uvs[vi,0],uvs[vi,1]);is_dedicated=owner['path']==u['dedicated_wall_owner']['path'];okay=inside.all() or is_dedicated
   chk(f'unit_spatial_{pi}_{oi}_{j}',okay,{'boardNo':u['boardNo'],'inside_buffer_count':int(inside.sum()),'vertices':len(vi),'dedicated':is_dedicated})
   spatial.append({'boardNo':u['boardNo'],'vertices':len(vi),'triangles':len(ti),'uv_bounds':[uvs[vi].min(0).tolist(),uvs[vi].max(0).tolist()],'world_y_bounds':[float(world[vi,1].min()),float(world[vi,1].max())],'triangle_ordinals':ti.tolist()})
  ownership.append({'path':r['path'],'global_object_id':r['global_object_id'],'mesh':m['id'],'material_slots':r['materials'],'world_matrix':owner['world_matrix'],'selected_by_unit':spatial})
 patch_rows.append({'asset':q['source_asset'],'selected_vertices':len(ids),'total_vertices':q['vertices'],'selected_triangles':len(seltri),'owners':ownership})
# All non-selected active renderers bearing explicit SHOP-1F tags must be covered by one motion.
motions=p['movable_transforms'];pathmotions={x['path']:x for x in motions if x['kind']=='path'};doormotions={x['door_element']:x for x in motions if x['kind']=='door'};fixturegroups={}
for r in s['renderers']:
 mt=re.match(r'^실내 트윈 마감/점포 내부 1F/(SHOP-1F-(\d\d) [^/]+)',r['path'])
 if mt and 9<=int(mt[2])<=15 and r['active']:
  parent='실내 트윈 마감/점포 내부 1F/'+mt[1];fixturegroups.setdefault(parent,[]).append(r['path'])
chk('all_named_fixture_groups',set(fixturegroups)==set(pathmotions),{'groups':len(fixturegroups),'motions':len(pathmotions),'missing':sorted(set(fixturegroups)-set(pathmotions)),'unexpected':sorted(set(pathmotions)-set(fixturegroups))})
collider_fixture_groups={}
for r in s['colliders']:
 mt=re.match(r'^실내 트윈 마감/점포 내부 1F/(SHOP-1F-(\d\d) [^/]+)',r['path'])
 if mt and 9<=int(mt[2])<=15 and r['active']:
  parent='실내 트윈 마감/점포 내부 1F/'+mt[1];collider_fixture_groups.setdefault(parent,[]).append(r['path'])
chk('fixture_collider_coverage',set(collider_fixture_groups)<=set(pathmotions),{'collider_groups':len(collider_fixture_groups),'missing':sorted(set(collider_fixture_groups)-set(pathmotions))})
chk('no_nested_motion_paths',not any(a!=b and b.startswith(a+'/') for a in pathmotions for b in pathmotions),'No parent/child duplicate movement')
for path,x in pathmotions.items():chk('fixture_delta_'+path,np.allclose(x['world_delta'],p['units'][x['boardNo']-9]['world_delta'],atol=1e-8),x['boardNo'])
# Baked scene StationDoor.Element coverage, hierarchy and leaf-renderer coverage.
text=Path(p['scene']).read_text();docs={};gos={};trans={};go_trans={};doors=[]
for match in re.finditer(r'^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S):
 typ,obj,body=int(match[1]),int(match[2]),match[3];docs[obj]=(typ,body)
 if typ==1:
  nm=re.search(r'^  m_Name: (.*?)(?=^  [A-Za-z]|\Z)',body,re.M|re.S);gos[obj]=yaml.safe_load(nm[1].strip()) if nm else ''
 if typ==4:
  go=int(re.search(r'm_GameObject: \{fileID: (\d+)\}',body)[1]);father=int(re.search(r'm_Father: \{fileID: (\d+)\}',body)[1]);trans[obj]=(go,father);go_trans[go]=obj
 if typ==114 and 'ChooGuard.App.Fps.Facilities.StationDoor' in body:
  el=re.search(r'^  Element: (.*)$',body,re.M)[1];el=json.loads(el) if el.startswith('"') else el
  if re.match(r'1F-unit(09|10|11|12|13|14|15)-',el):doors.append({'component_id':obj,'game_object_id':int(re.search(r'm_GameObject: \{fileID: (\d+)\}',body)[1]),'element':el,'leaf_pivots':[int(x) for x in re.findall(r'Pivot: \{fileID: (\d+)\}',body)]})
def path_for_go(go):
 parts=[]
 while go:
  parts.append(gos.get(go,'?'));tid=go_trans.get(go);f=trans.get(tid,(0,0))[1];go=trans.get(f,(0,0))[0]
 return '/'.join(reversed(parts))
for d in doors:
 d['path']=path_for_go(d['game_object_id']);d['leaf_paths']=[path_for_go(trans[t][0]) for t in d['leaf_pivots']];d['renderer_paths']=[r['path'] for r in s['renderers'] if r['path'].startswith(d['path']+'/')]
 chk('baked_door_motion_'+d['element'],d['element'] in doormotions and bool(d['renderer_paths']) and all(x.startswith(d['path']+'/') for x in d['leaf_paths']),d)
chk('exact_door_element_set',set(d['element'] for d in doors)==set(doormotions),{'baked_count':len(doors),'motion_count':len(doormotions)})
# Explicit per-unit frontage and sign ownership independently establishes missed finishes.
spec=json.loads(Path('.planning/2026-09-24-interior-twin/specs/1f-detail.json').read_text());elements=spec['elements'];fronts={int(re.search(r'unit(\d+)',e['id'])[1]):LineString(e['geometry']['edge']) for e in elements if re.match(r'1F-unit(09|10|11|12|13|14|15)-front1$',e.get('id',''))}
missing=[];fascia_semantic_assignments=[];sign_names={9:'boksoondoga',10:'artcent',11:'pho-rail',12:'box-cup',13:'chaeseondang',14:'gaemijip',15:'saebyeokjip'}
for r in s['renderers']:
 if not r['active'] or not r['path'].startswith('실내 트윈 마감/1F/') or not r['mesh'] or not mm[r['mesh']]['binary']:continue
 if not any(k in r['path'] for k in ['Kit_Clad_','Kit_Joint','Kit_Fascia_','Kit_Lightbox_']):continue
 m,v,t,w=geo(r);uvs=uv(w);q=patches.get(m['asset']);selected=set(q['selected_triangles']) if q else set()
 for number,line in fronts.items():
  if 'Kit_Lightbox_' in r['path'] and sign_names[number] not in r['path']:continue
  tv=uvs[t];centres=tv.mean(1);dist=np.array([line.distance(Point(x)) for x in centres]);yn=w[t,1].min(1);yx=w[t,1].max(1)
  limit=.04 if any(k in r['path'] for k in ['Kit_Clad_','Kit_Joint']) else .41
  low=2.899 if any(k in r['path'] for k in ['Kit_Clad_','Kit_Joint']) else 2.39
  eligible=(dist<limit)&(yn>=low)&(yx<=4.801)
  if 'Kit_Fascia_' in r['path']:
   element=next(e for e in elements if e.get('id')==f'1F-unit{number:02d}-front1');aa,bb=np.array(element['geometry']['edge']);ex=(bb-aa)/np.linalg.norm(bb-aa);normal=np.array([-ex[1],ex[0]]);toward=np.array(element['params']['faceToward'])-(aa+bb)/2
   if np.dot(toward,normal)<0:normal=-normal
   along=(tv-aa)@ex;depth=(tv-aa)@normal;length=np.linalg.norm(bb-aa)
   eligible=(along.min(1)>=-.0001)&(along.max(1)<=length+.0001)&(depth.min(1)>=-.0501)&(depth.max(1)<=.2501)&(yn>=2.3999)&(yx<=2.9001)
  ids=np.flatnonzero(eligible);unmoved=[int(i) for i in ids if int(i) not in selected]
  if 'Kit_Fascia_' in r['path'] and q:
   qi=np.array(q['vertex_indices'],int);delta=(np.array(q['replacement_positions'])-np.array(q['expected_positions']))@np.array(r['world_matrix'])[:3,:3].T;lookup={int(vv):delta[k] for k,vv in enumerate(qi)}
   for fi in ids:
    if int(fi) not in selected:continue
    actual=lookup[int(t[fi,0])];expected_delta=np.array(p['units'][number-9]['world_delta']);actual_number=9+int(np.argmin(np.linalg.norm(np.array([u['world_delta'] for u in p['units']])-actual,axis=1)))
    fascia_semantic_assignments.append({'source_box_boardNo':number,'triangle':int(fi),'assigned_delta_boardNo':actual_number,'delta_error':float(np.linalg.norm(actual-expected_delta))})
  if unmoved:missing.append({'boardNo':number,'owner':r['path'],'asset':m['asset'],'mesh':m['id'],'binary_sha256':m['binary_sha256'],'world_matrix':r['world_matrix'],'unmoved_frontage_triangle_ordinals':unmoved,'distance_to_frontline_minmax':[float(dist[ids].min()),float(dist[ids].max())],'y_bounds':[float(yn[ids].min()),float(yx[ids].max())],'proof':'explicit unit frontage line from1f-detail spec; sign owner name or per-frontage bulkhead; not proximity to an arbitrary unit center'})
# Polygon pair overlaps and guide corridor source is qualitative; no metric claim.
relationships=[]
for i,a in enumerate(p['units']):
 for b in p['units'][i+1:]:
  aa=Polygon(a['after']['polygon']);bb=Polygon(b['after']['polygon']);relationships.append({'a':a['boardNo'],'b':b['boardNo'],'after_distance':aa.distance(bb),'after_overlap_area':aa.intersection(bb).area,'before_distance':Polygon(a['before']['polygon']).distance(Polygon(b['before']['polygon']))})
report={'schema':'chooguard.floor1-stage-independent-review.v1','packet':str(PACKET),'packet_sha256':sha(PACKET),'stage_code_sha256':sha('workers/twin/stage_floor1_guide_layout.py'),'source_inventory':str(BASE),'source_inventory_sha256':sha(BASE),'checks':checks,'all_integrity_checks_passed':all(x['passed'] for x in checks),'mesh_patch_count':len(p['mesh_patches']),'selected_vertices':sum(len(q['vertex_indices']) for q in p['mesh_patches']),'patch_owners':sum(len(q['owners']) for q in p['mesh_patches']),'motions':{'count':len(motions),'named_fixture_groups':len(pathmotions),'doors':len(doormotions),'baked_door_receipts':doors},'shared_and_dedicated_mask_audits':patch_rows,'coordinate_coincidence_splits':coordinate_splits,'unmoved_associated_frontage_finishes':missing,'fascia_semantic_delta_assignments':fascia_semantic_assignments,'pair_relationships_model_units':relationships,'site_accuracy_verified':False,'production_writes':0,'remaining_gate':'Resolve missing bulkheads/sign panels and independent corridor clearance before apply; root owns14/15seam andBOH separately.'}
(EV/'integrity-review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'checks':len(checks),'failed':[x['id'] for x in checks if not x['passed']],'patches':len(p['mesh_patches']),'motions':len(motions),'doors':len(doors),'missing_finish_records':len(missing),'unmoved_triangles':sum(len(x['unmoved_frontage_triangle_ordinals']) for x in missing)},ensure_ascii=False))
for x in missing:print(x['boardNo'],x['owner'],len(x['unmoved_frontage_triangle_ordinals']),x['distance_to_frontline_minmax'],x['y_bounds'])
