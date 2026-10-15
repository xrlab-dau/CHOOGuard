"""Read-only remaining guide coverage and exact production-packet collider slices."""
from pathlib import Path
import json,struct,hashlib
import numpy as np
from shapely.geometry import Polygon,LineString,Point
from shapely.ops import unary_union
ROOT=Path('/Users/um-yunsang/CHOOGuard');BASE=ROOT/'workers/twin/output/floor1-multiview-next-20261009';OUT=BASE/'remaining-guide-coverage'
EX=ROOT/'.planning/2026-10-08-interior-runtime-completion/architecture/after-busanok-finishes/current-scene-structure.json'
data=json.loads(EX.read_text());meshmeta={m['id']:m for m in data['meshes']}
production=json.loads((ROOT/'.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-packet.json').read_text())
receipt=json.loads((ROOT/'.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-installation.json').read_text())
patches={x['source_asset']:x for x in production['mesh_patches']};motions={x['path']:np.array(x['after_position'])-np.array(x['before_position']) for x in receipt['motions'] if 'path' in x}
guide=json.loads((BASE/'guide-semantics.json').read_text());M=np.array(guide['registration']['pixel_homogeneous_row_to_UV_matrix'])
spec=json.loads((ROOT/'.planning/2026-09-23-video-twin/1f-implementation/interior-spec.json').read_text());public=json.loads((ROOT/'.planning/2026-09-24-interior-twin/specs/full/public-1f.json').read_text())
cache={}
def binary(mid):
 if mid not in cache:
  m=meshmeta[mid];raw=(ROOT/m['binary']).read_bytes();nv,ni,nn=struct.unpack_from('<iii',raw);v=np.frombuffer(raw,'<f4',nv*3,12).reshape(nv,3).astype(float);t=np.frombuffer(raw,'<i4',ni,12+nv*12).reshape(-1,3)
  if m['asset'] in patches:
   p=patches[m['asset']];v[p['vertex_indices']]=p['replacement_positions']
  cache[mid]=(v,t)
 return cache[mid]
c=.9602937;s=.278991133
def worldUV(w):return np.column_stack([s*w[:,0]+c*w[:,2],c*w[:,0]-s*w[:,2]])
def world(r,v):
 mat=np.array(r['world_matrix'],float);w=v@mat[:3,:3].T+mat[:3,3]
 if r['path'] in motions:w+=motions[r['path']]
 return w
def owner(r):
 z={k:r[k] for k in ['path','global_object_id','type'] if k in r}
 if r.get('mesh'):
  m=meshmeta[r['mesh']];z.update({'snapshot_mesh':r['mesh'],'source_asset':m['asset'],'source_binary_sha256':m['binary_sha256'],'production_mesh_patch_applied_in_memory':m['asset'] in patches})
 return z
def slice_faces(r,height=1.5):
 if r.get('mesh'):v,t=binary(r['mesh'])
 elif r['type']=='BoxCollider':
  cen=np.array(r['local_center']);sz=np.array(r['local_size'])/2
  v=np.array([[x,y,z] for x in [-1,1] for y in [-1,1] for z in [-1,1]])*sz+cen
  t=np.array([[0,1,3],[0,3,2],[4,6,7],[4,7,5],[0,4,5],[0,5,1],[2,3,7],[2,7,6],[0,2,6],[0,6,4],[1,5,7],[1,7,3]])
 else:return []
 w=world(r,v);tri=w[t];dy=tri[:,:,1]-height;mask=(dy.min(1)<=0)&(dy.max(1)>=0);result=[]
 for i in np.where(mask)[0]:
  pts=[];tr=tri[i]
  for j,k in [(0,1),(1,2),(2,0)]:
   yj=tr[j,1]-height;yk=tr[k,1]-height
   if abs(yj)<1e-7:pts.append(tr[j])
   if yj*yk <0:pts.append(tr[j]+(tr[k]-tr[j])*(-yj)/(yk-yj))
  if len(pts)<2:continue
  uv=worldUV(np.array(pts));uv=np.unique(np.round(uv,6),axis=0)
  if len(uv)<2:continue
  distance=((uv[:,None]-uv[None,:])**2).sum(2);j,k=np.unravel_index(distance.argmax(),distance.shape);seg=LineString([uv[j],uv[k]])
  if seg.length>.001:result.append((int(i),seg))
 return result

# Test real collider geometry after applying the already-installed production packet in memory.
sections=[]
for r in data['colliders']:
 if not r.get('active',True) or not r.get('enabled',True) or r.get('trigger'):continue
 if r['world_bounds_min'][1]>1.5 or r['world_bounds_max'][1]<1.5:continue
 if 'KTX' in r['path'] or '디딤판 운행' in r['path']:continue
 for i,seg in slice_faces(r):
  x0,y0,x1,y1=seg.bounds
  if x1>=-94 and x0<=78 and y1>=-48 and y0<=-12:sections.append((r,i,seg))

passes=[]
for gid,center,width,vend in [('P1',41.5,6,-41),('P2',25.85,5,-45),('P3',9.95,7,-47),('P4',-4.49,5,-47),('P5',-21.1,12,-47),('P6',-64.58,9,-42)]:
 candidates=[]
 for u in np.linspace(center-width/2,center+width/2,31):
  line=LineString([[float(u),-24],[float(u),vend]])
  hits=[(r,i,seg) for r,i,seg in sections if seg.distance(line)<.28]
  candidates.append((float(u),line,hits))
 clear=[z for z in candidates if not z[2]]
 # Representative probe prioritizes clearance; a blocked schematic center is not a phantom wall proof.
 rep=clear[len(clear)//2] if clear else min(candidates,key=lambda z:len(z[2]))
 blocking={}
 for r,i,seg in rep[2]:
  key=r['global_object_id'];blocking.setdefault(key,{'owner':owner(r),'faces':[],'segments_UV':[]});blocking[key]['faces'].append(i);blocking[key]['segments_UV'].append(list(map(list,seg.coords)))
 status='UNKNOWN' if gid in ['P2','P4'] else ('MATCH' if clear else 'UNKNOWN')
 passes.append({'feature':gid,'status':status,'guide_bbox_pixels':next(x['bbox'] for x in guide['front_access_gaps'] if x['id']==gid),
  'evidence_level':'schematic adjacency and native collider section, not surveyed door position',
  'through_access_note':'Guide gap is occupied by lift/stair symbols; white diagram region does not prove unobstructed through-access. Preserve protected core.' if gid in ['P2','P4'] else 'Guide clear access gap has a corresponding native corridor; see exact probe.',
  'probe_y':1.5,'probe_radius_model_units':.28,'candidate_U_interval':[center-width/2,center+width/2],
  'clear_straight_probe_count':len(clear),'representative_probe_UV':list(map(list,rep[1].coords)),
  'representative_blockers':list(blocking.values())})

# Semantic feature-to-native recipe associations; exact boundary faces preserve owner identity.
def recipe_faces(e,mid):
 r=next(r for r in data['renderers'] if r.get('mesh')==mid);v,t=binary(mid);uv=worldUV(world(r,v));line=Polygon(e['geometry']['polygon']).boundary if 'polygon' in e['geometry'] else LineString(e['geometry'].get('edge',e['geometry'].get('polyline')))
 faces=[i for i,f in enumerate(t) if all(line.distance(Point(uv[j]))<.22 for j in f)]
 return {'owner':owner(r),'face_ordinals_on_recipe_boundary':faces,'association_tolerance_model_units':.22}
coverage=[]
for u in spec['units']:
 if u['boardNo'] not in list(range(1,9))+[16]:continue
 n=u['boardNo'];r=next(r for r in data['renderers'] if f"Ground_Unit_{u['mid']}_Walls" in r['path']);poly=Polygon(u['polygon']);source=next(x for x in guide['units'] if x['boardNo']==n);gp=Polygon(source['scaffold_polygon_UV_approx'])
 coverage.append({'feature':f'unit-{n}','status':'UNKNOWN' if n in [4,8,16] else 'MATCH','match_scope':'shop presence and order; footprint layout UNKNOWN for4/8/16',
  'guide_name':source['guide_name'],'current_name':u['name'],'tenant_name_status':'KNOWN_MISMATCH' if n==1 else 'MATCH',
  'footprint_metric_status':'UNKNOWN','source_guide_polygon_pixels':source['source_polygon_pixels_approx'],'current_polygon_UV':u['polygon'],
  'guide_scaffold_polygon_UV':source['scaffold_polygon_UV_approx'],'schematic_scaffold_centroid_delta_model_units':poly.centroid.distance(gp.centroid),
  'schematic_scaffold_IoU':poly.intersection(gp).area/poly.union(gp).area,
  'actual_owner':owner(r),'actual_colliders':[owner(x) for x in data['colliders'] if x['path']==r['path']],
  'note':'#1 label changed from current Zimcarry to guide Morijong; this is a tenant-label mismatch, no wall-removal authority.' if n==1 else 'Guide schematic proportions differ. Units4/8/16 exceed3.5-unit scaffold centroid tolerance; their wall shapes remain unresolved. Approximate affine residuals cannot certify physical dimensions.'})

for group,ids,mid in [('WC-left',['1Fp-wc-uplus-female','1Fp-wc-uplus-male'],'mesh-3178'),('WC-right',['1Fp-wc-uminus-male','1Fp-wc-uminus-female'],'mesh-3178'),
                       ('front-lift-stair-left',['1Fp-boh-core-e-4919','1Fp-boh-core-e-4924','1Fp-elev-core-e-shaft','1Fp-stair-core-e'],'mesh-2900'),
                       ('front-lift-stair-center',['1Fp-boh-core-w-5482','1Fp-elev-core-w-shaft','1Fp-stair-core-w'],'mesh-2900')]:
 es=[e for e in public['elements'] if e['id'] in ids]
 union=unary_union([Polygon(e['geometry']['polygon']) for e in es])
 doorowners=[]
 for r in data['renderers']:
  if not r['path'].startswith('실내 트윈 마감/1F 공용/문 ·'):continue
  cw=(np.array(r['world_bounds_min'])+np.array(r['world_bounds_max']))/2
  cuv=worldUV(cw[None])[0]
  if union.distance(Point(cuv))<.7:
   doorowners.append({'renderer':owner(r),'centre_UV':cuv.tolist(),'colliders':[owner(x) for x in data['colliders'] if x['path']==r['path']]})
 coverage.append({'feature':group,'status':'MATCH','match_scope':'end/cluster placement and adjacency; exact door/gender details UNKNOWN',
 'recipe_ids':ids,'native_polygons_UV':{e['id']:e['geometry']['polygon'] for e in es},'actual_boundary_owners':[recipe_faces(e,mid) for e in es],
 'actual_door_leaf_owners':doorowners,
 'exact_shape_status':'UNKNOWN','note':'Existing protected core placement agrees with the source guide gross arrangement; keep all source Room faces.'})

for group,prefix,center,kind in [('rear-escalator-left','실내 트윈 마감/수직 동선',[18.563,-16.693],'ESC-CORE-1F2F'),
                                ('rear-escalator-right','실내 트윈 마감/1F 공용 에스컬레이터',[-59.32,-15.495],'1Fp-esc-uminus')]:
 rr=[r for r in data['renderers'] if r['path']==prefix+'/Kit_EscClad_에스컬레이터']
 coverage.append({'feature':group,'status':'MATCH','match_scope':'escalator presence beside the corresponding rear-row end; separate stair symbol UNKNOWN',
  'recipe_id':kind,'native_foot_UV':center,'actual_owners':[owner(r) for r in rr],
  'actual_collision_owners':[owner(r) for r in data['colliders'] if r['path']==prefix+'/Kit_Colliders'],
  'separate_stair_status':'UNKNOWN','note':'Sign provides escalator/stair glyph, not a separate surveyed stair footprint. Existing escalator present. Do not add a separate stair or cut protected source cores based on this glyph alone.'})

lock=next(r for r in data['renderers'] if r['path']=='실내 트윈 마감/1F 공용/Kit_Locker_보관함')
coverage.append({'feature':'locker17','status':'MATCH','match_scope':'locker presence beside the left rear escalator and across from shop2',
 'guide_bbox_pixels':guide['storage17']['bbox'],'native_recipe_id':'1Fp-lockers','native_edge_UV':[[27.8,-18.13],[31.4,-18.13]],
 'actual_owner':owner(lock),'actual_collision_owner':[owner(c) for c in data['colliders'] if c['path']=='실내 트윈 마감/1F 공용/Kit_Colliders'],
 'exact_bank_extent_status':'UNKNOWN','note':'Source17 rectangle is a label, not physical locker footprint; source recipe length is inferred.'})
doors=json.loads((ROOT/'.planning/2026-10-08-interior-runtime-completion/native-station-door-snapshot.json').read_text())
for fid,rid in [('P1','1835902679'),('P3','1900076697')]:
 r=next(r for r in data['renderers'] if r['global_object_id'].split('-')[-2]==rid);col=next(x for x in data['colliders'] if x['path']==r['path']);v,t=binary(r['mesh']);w=world(r,v);uv=worldUV(w);cen=(uv.min(0)+uv.max(0))/2
 mat=np.array(r['world_matrix']);aw=mat[:3,0]/np.linalg.norm(mat[:3,0]);axis=np.array([s*aw[0]+c*aw[2],c*aw[0]-s*aw[2]]);normal=np.array([-axis[1],axis[0]])
 near=[]
 for d in doors:
  cw=np.array(d['centre_world']);duv=worldUV(cw[None])[0]
  if cw[1]<3:near.append({'element':d['element'],'path':d['path'],'kind':d['kind'],'use':d['use'],'centre_UV':duv.tolist(),'plan_distance_model_units':float(np.linalg.norm(duv-cen)),'along_world':d['along_world'],'normal_world':d['normal_world'],'leaf_paths':[x['path'] for x in d['leafs']]})
 near=sorted(near,key=lambda x:x['plan_distance_model_units'])[:3]
 probes=[]
 for sign in [-1,1]:
  p=cen+sign*normal*1.2;probes.append({'side':sign,'UV':p.tolist(),'worldXYZ':[float(c*p[1]+s*p[0]),1.,float(-s*p[1]+c*p[0])]})
 f=next(x for x in passes if x['feature']==fid)
 f['native_leaf_mapping']={'renderer':r,'collider':col,'mesh':meshmeta[r['mesh']],'source_id_from_name':meshmeta[r['mesh']]['name'].removeprefix('N_'),
  'full_leaf_bounds_UV':[float(uv[:,0].min()),float(uv[:,1].min()),float(uv[:,0].max()),float(uv[:,1].max())],'leaf_height':float(w[:,1].max()-w[:,1].min()),
  'native_long_axis_UV':axis.tolist(),'native_normal_axis_UV':normal.tolist(),'nearest_existing_StationDoor_records':near,
  'matching_existing_StationDoor_leaf_path':any(l['path']==r['path'] for d in doors for l in d['leafs']),
  'binding_status':'No exact StationDoor leaf-path association in103-door snapshot; source IDs only retained as native GameObject names. Runtime operability requires root probe.',
  'authored_pose':'Thin leaf approximately parallel to local front facade; consistent with closed pose. Hinge/slide action not established from mesh alone.',
  'root_physics_probe_world':probes,'delete_source_faces':False}
result={'schema':'chooguard.floor1-remaining-guide-coverage.v1','production_scene_sha256':receipt['scene_sha256'],
 'production_installation_receipt':str((ROOT/'.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-installation.json').relative_to(ROOT)),
 'source_snapshot_scene_sha256':data['scene_file_sha256'],'current_geometry_method':'Read source snapshot and apply installed production mesh vertex replacements and transform deltas in memory; no Unity/Assets writes.',
 'reference_only':True,'physical_accuracy_verified':False,'guide_source_url':guide['source_url'],'guide_image_sha256':guide['native_image_sha256'],
 'features':coverage+passes,'new_bounded_architecture_repairs':[],
 'conclusion':'Feature order and gross core placement agree. Footprint layout4/8/16, exact shop dimensions, core doors/gender ordering, separate rear stairs and all six saved-pose through-access probes remain UNKNOWN. P1/P3 native source leaves lack an exact association in the103-door snapshot; root physics probes supplied. No additional exact phantom-wall deletion is supported by this bounded audit.',
 'collider_section_count':len(sections),'scene_runtime_walk_verified':False}
(OUT/'coverage.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
(OUT/'collider-sections.json').write_text(json.dumps({'plane_y':1.5,'frame':'station UV','production_scene_sha256':receipt['scene_sha256'],
 'segments':[{'owner':owner(r),'face_ordinal':i,'segment_UV':list(map(list,seg.coords))} for r,i,seg in sections]},ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'feature_count':len(result['features']),'passages':[{k:p[k] for k in ['feature','status','clear_straight_probe_count','representative_probe_UV']} for p in passes],'collider_sections':len(sections)},ensure_ascii=False))
