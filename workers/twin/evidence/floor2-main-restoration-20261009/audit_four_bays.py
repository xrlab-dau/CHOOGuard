from pathlib import Path
import json,hashlib,sys,re,yaml
import numpy as np
from shapely.geometry import Polygon,LineString,Point
from shapely.ops import unary_union
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
sys.path.insert(0,str(Path('workers/twin').resolve()));from official_plan_compare import readbin,cut
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-main-restoration-20261009');p=json.loads((O/'four-bay-packet.json').read_text());s=json.loads((E/'current-inventory.json').read_text());comp=json.loads((E/'scene-components.json').read_text());mm={x['id']:x for x in s['meshes']};rr={r['global_object_id']:r for r in s['renderers']+s['colliders']};ref=next(r for r in s['renderers'] if r['path']=='맞이방 · 원본 정합/Shop_다인명가_Walls');FRAME=np.array(ref['world_matrix'])[:3,:3];INV=np.linalg.inv(FRAME);checks=[];cache={};after={};fixture=[]
sha=lambda f:hashlib.sha256(Path(f).read_bytes()).hexdigest()
def check(name,ok,d):checks.append({'id':name,'passed':bool(ok),'details':d})
def geo(r):
 mid=r['mesh']
 if mid not in cache:cache[mid]=readbin(mm[mid])
 v,t=cache[mid];M=np.array(r['world_matrix']);return v,t,v@M[:3,:3].T+M[:3,3]
def q(w):return (w@INV.T)[:,[2,0,1]]
check('saved_source_epoch',sha(p['scene'])==s['scene_file_sha256']==p['scene_sha256'],p['scene_sha256']);check('guide_proposal_hash',sha(p['guide'])==p['guide_sha256'] and sha(p['proposal'])==p['proposal_sha256'],p['guide']);check('no_rejections',not p['rejected_owners'],p['rejected_owners']);check('only_bounded2f',p['floor']==2 and len(p['units'])==4 and [u['boardNo'] for u in p['units']]==[20,21,23,22],p['units'])
for qi,row in enumerate(p['mesh_patches']):
 f=row['source_asset'];body=Path(f).read_bytes();a=yaml.safe_load(body.decode().split('Mesh:\n',1)[1]);raw=bytes.fromhex(a['m_VertexData']['_typelessdata']);nv=a['m_VertexData']['m_VertexCount'];stride=len(raw)//nv;ids=np.array(row['vertex_indices']);sel=np.array(row['selected_triangles']);before=np.array(row['expected_positions'],'<f4');target=np.array(row['replacement_positions'],'<f4');users=[r for r in s['renderers']+s['colliders'] if mm.get(r.get('mesh'),{}).get('asset')==f]
 check(f'exact_file_GUID_{qi}',sha(f)==row['source_file_sha256'] and re.search('^guid: (.*)$',Path(f+'.meta').read_text(),re.M)[1]==row['source_asset_guid'],f)
 check(f'complete_owner_frame_{qi}',row['owners']==[r for r in users if r.get('type')!='MeshCollider'] and row['collider_owners']==[r for r in users if r.get('type')=='MeshCollider'],{'renderers':len(row['owners']),'colliders':len(row['collider_owners'])})
 r=users[0];v,t,w=geo(r);M=np.array(r['world_matrix']);vafter=v.copy();vafter[ids]=target;aw=vafter@M[:3,:3].T+M[:3,3];bq,aq=q(w),q(aw);after[f]=(v,t,w,vafter,aw,bq,aq,r)
 check(f'exact_source_binary_{qi}',np.array_equal(v[ids],before) and row['source_binary_sha256']==mm[r['mesh']]['binary_sha256'] and nv==row['vertices'] and len(t)==row['triangles'],{'source_mesh':r['mesh'],'vertices':nv,'triangles':len(t)})
 check(f'exact_full_channels_mask_{qi}',a['m_VertexData']['m_Channels']==row['source_channels'] and a['m_SubMeshes']==row['source_submeshes'] and hashlib.sha256(raw).hexdigest()==row['source_full_vertex_bytes_sha256'] and hashlib.sha256(b''.join(raw[i*stride:(i+1)*stride] for i in ids)).hexdigest()==row['source_selected_vertex_channel_bytes_sha256'] and hashlib.sha256(t[sel].astype('<u4').tobytes()).hexdigest()==row['source_selected_index_bytes_sha256'],'Original normals/tangents/UV0/materials/submesh/index ordering remain unmodified')
 mask=np.zeros(len(v),bool);mask[ids]=True;tm=np.zeros(len(t),bool);tm[sel]=True;check(f'index_closure_{qi}',np.array_equal(np.unique(t[sel]),ids) and np.array_equal(np.flatnonzero(mask[t].any(1)),sel),'Every face sharing a selected index included')
 refs=np.unique(t);_,weld=np.unique(np.round(v,5),axis=0,return_inverse=True);ed=weld[t];aa,bb=ed.ravel(),ed[:,[1,2,0]].ravel();_,lab=connected_components(coo_matrix((np.ones(len(aa)),(aa,bb)),shape=(weld.max()+1,weld.max()+1)),directed=False);labels=lab[weld]
 torn=[int(k) for k in np.unique(weld[ids]) if not mask[refs[weld[refs]==k]].all()];partial=[int(k) for k in np.unique(labels[ids]) if not tm[labels[t[:,0]]==k].all()]
 check(f'weld_component_closure_{qi}',not torn and not partial,{'split_rounded_welds':torn,'partial_components':partial})
 nb=np.cross(v[t[sel,1]]-v[t[sel,0]],v[t[sel,2]]-v[t[sel,0]]);na=np.cross(vafter[t[sel,1]]-vafter[t[sel,0]],vafter[t[sel,2]]-vafter[t[sel,0]]);check(f'no_flip_collapse_{qi}',(np.sum(nb*na,1)>0).all() and (np.linalg.norm(na,axis=1)>1e-8).all(),{'flipped_faces':sel[np.sum(nb*na,1)<=0].tolist()})
 check(f'all_other_vertices_Y_indices_preserved_{qi}',np.array_equal(vafter[~mask],v[~mask]) and np.array_equal(vafter[:,1],v[:,1]),'Original Y and every unselected vertex unchanged; triangle indices not edited')
 for unit in row['unit_contributor_masks']:
  vi=np.array(unit['selected_vertex_indices']);u=next(u for u in p['units'] if u['boardNo']==unit['guide_number']);b=u['before'];n=u['after'];has_arch=row['mapping_mode']=='affine_bay_depth'
  check(f'unit_source_bounds_{qi}_{unit["guide_number"]}',((bq[vi,:2]>=np.array([b['u'][0]-.35001,b['v'][0]-.35001]))&(bq[vi,:2]<=np.array([b['u'][1]+.35001,b['v'][1]+.35001]))).all(),{'before_native_UV_bounds':[bq[vi,:2].min(0).tolist(),bq[vi,:2].max(0).tolist()]})
  if not has_arch:
   d=aq[vi]-bq[vi];res=float(np.max(np.linalg.norm(d-d[0],axis=1)));ulp=float(np.max(abs(np.spacing(np.vstack([v[vi],vafter[vi]])))))
   check(f'rigid_fixture_unit_{qi}_{unit["guide_number"]}',res<=ulp*4,{'max_native_delta_residual_float32':res,'maximum_source_or_target_float32_spacing':ulp,'delta_UV':d.mean(0)[:2].tolist()});fixture.append({'source_asset':f,'unit':unit['guide_number'],'vertices':len(vi),'rigid_delta_residual':res,'mapping_mode':row['mapping_mode']})
  else:
   err=np.max(abs(aq[vi,:2].min(0)-[n['u'][0],n['v'][0]]));err=max(err,float(np.max(abs(aq[vi,:2].max(0)-[n['u'][1],n['v'][1]]))))
   check(f'exact_target_arch_bounds_{qi}_{unit["guide_number"]}',err<=2e-5,{'after_native_UV_bounds':[aq[vi,:2].min(0).tolist(),aq[vi,:2].max(0).tolist()],'declared_rect_float32_max_residual':float(err)})
check('no_original_native_or_1f_owners',all(not o['path'].startswith(('FPSWorld/','1층')) and '점포 내부 2F' not in o['path'] for row in p['mesh_patches'] for o in row['owners']+row['collider_owners']),'No original MainShell/EV/floor or 1F/other observed tenant assets modified')
# Whole footprints independently supported by actual enabled nontrigger original MeshCollider top faces.
polys={u['boardNo']:Polygon([(u['after']['u'][0],u['after']['v'][0]),(u['after']['u'][1],u['after']['v'][0]),(u['after']['u'][1],u['after']['v'][1]),(u['after']['u'][0],u['after']['v'][1])]) for u in p['units']};support=[];floor_receipts=[];native_blockers=[]
for c in s['colliders']:
 if c.get('type')!='MeshCollider' or not c.get('mesh') or not c['path'].startswith('FPSWorld/공식 자료 부산역 역사/MainShell') or not c.get('enabled') or not c.get('active') or c.get('trigger'):continue
 m=mm[c['mesh']]
 if not m.get('binary'):continue
 v,t,w=geo(c);qq=q(w);tri=w[t];n=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]);h=np.max(abs(tri[:,:,1]-7),axis=1);fs=np.flatnonzero((h<.002)&(n[:,1]>1e-8));kept=[]
 for fi in fs:
  pp=Polygon(qq[t[fi],:2])
  if pp.is_valid and pp.area>1e-9 and any(pp.intersection(z).area>1e-10 for z in polys.values()):support.append(pp);kept.append(int(fi))
 if kept:floor_receipts.append({'owner':c,'mesh':m,'selected_floor_face_ordinals':kept,'worldY_range':[float(tri[kept,:,1].min()),float(tri[kept,:,1].max())]})
 # Record exact source vertical partitions crossing a shrunken tenant interior, not an exterior overlap premise.
 seg=cut(tri,8.25)
 for u,poly in polys.items():
  intr=[]
  for z in seg:
   w3=np.c_[z[:,0],np.full(2,8.25),z[:,1]];zz=q(w3)[:,:2];line=LineString(zz)
   hit=line.intersection(poly.buffer(-.05))
   if hit.length>1e-4:intr.append(zz.tolist())
  if intr:native_blockers.append({'unit':u,'owner':c['path'],'crossing_source_segments_UV':intr})
union=unary_union(support);floor_results=[]
for u,poly in polys.items():
 missing=poly.difference(union.buffer(2e-5)).area;floor_results.append({'unit':u,'rectangle_UV':list(poly.exterior.coords),'area':poly.area,'uncovered_area_at_existing_float_tolerance':missing});check('full_original_collision_floor_'+str(u),missing<1e-10,floor_results[-1])
# Door/static source frontage is a rigid common delta; original spans remain unchanged.
kitcol=next(row for row in p['mesh_patches'] if row['source_asset'].endswith('/2F_main/Kit_Colliders.asset'));v,t,w,va,aw,bq,aq,r=after[kitcol['source_asset']];opening=[]
for d in comp['doors']:
 motion=next(m for m in p['movable_transforms'] if m.get('door_element')==d['fields']['Element']);u=next(u for u in p['units'] if u['boardNo']==motion['unit']);localC=np.array([d['fields']['Centre'][k] for k in ['x','y','z']]);c=localC@FRAME.T+motion['world_delta'];cq=q(c[None])[0];along=np.array([d['fields']['Along'][k] for k in ['x','y','z']],float);ex=along[[2,0]];ex/=np.linalg.norm(ex);section=[]
 ur=next(z for z in kitcol['unit_contributor_masks'] if z['guide_number']==u['boardNo'])
 for fi in ur['selected_triangle_ordinals']:
  tri=aq[t[fi]];hits=[]
  for j in range(3):
   a,b=tri[j],tri[(j+1)%3]
   if (a[2]-8)*(b[2]-8)<=0 and a[2]!=b[2]:hits.append(a+(b-a)*(8-a[2])/(b[2]-a[2]))
  if len(hits)>=2:
   al=(np.array(hits)[:,:2]-cq[:2])@ex;section.append([float(al.min()),float(al.max())])
 left=max(z[1] for z in section if z[1]<0);right=min(z[0] for z in section if z[0]>0);width=d['fields']['Width'];clo=cq[0];front=u['after']['u'][0] if u['name'] in ['다인명가','호두나니&부산오뎅'] else u['after']['u'][1]
 check('door_static_alignment_'+str(u['boardNo']),abs(clo-front)<2e-5 and left<=-width/2+2e-5 and right>=width/2-2e-5,{'element':d['fields']['Element'],'targetCentreUV':cq[:2].tolist(),'nominal_width':width,'static_collider_gap':right-left,'static_endpoints_along':[left,right]});opening.append({'unit':u['boardNo'],'door':d['fields']['Element'],'centre_UV':cq[:2].tolist(),'nominal':width,'static_gap':right-left,'StationDoor_fields_sha256':hashlib.sha256(json.dumps(d['fields'],sort_keys=True).encode()).hexdigest(),'leaf_count':len(d['leaves'])})
check('all103bindings_preserved',len(comp['all_door_bindings'])==103 and len([m for m in p['movable_transforms'] if m['kind']=='door'])==4 and len({m['source_transform_id'] for m in p['movable_transforms']})==12,'Only4door parents move; all103Elements/componentIDs and every leaf/local field retained')
check('no_nested_motion_roots',not any(a!=b and b['path'].startswith(a['path']+'/') for a in p['movable_transforms'] for b in p['movable_transforms']),'4 door +4TextMesh sign +4fill light independent roots')
(E/'four-bay-floor-support.json').write_text(json.dumps({'scene_sha256':s['scene_file_sha256'],'native_frame':FRAME.tolist(),'source_receipts':floor_receipts,'units':floor_results,'native_interior_wall_crossings':native_blockers,'reference_only':True},ensure_ascii=False,indent=2)+'\n')
report={'schema':'chooguard.main2f-fourbay-offline-qa.v1','packet_sha256':sha(O/'four-bay-packet.json'),'status':'PASS' if all(x['passed'] for x in checks) else 'REJECT','summary':{'checks':len(checks),'passed':sum(x['passed'] for x in checks),'meshes':len(p['mesh_patches']),'vertices':sum(len(r['vertex_indices']) for r in p['mesh_patches']),'motions':len(p['movable_transforms'])},'checks':checks,'rigid_fixture_slots':fixture,'door_opening_receipts':opening,'native_interior_wall_crossings':native_blockers,'root_runtime_required':True,'site_accuracy_verified':False,'production_mutated':False};(E/'four-bay-qa.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'status':report['status'],'summary':report['summary'],'failures':[x for x in checks if not x['passed']],'native_interior_wall_crossings':[(b['unit'],b['owner'],len(b['crossing_source_segments_UV'])) for b in native_blockers]},ensure_ascii=False,indent=2))
