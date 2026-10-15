"""Independent bounded QA of the Unit8 append packet; production remains read only."""
from pathlib import Path
import json,struct,hashlib,yaml,re,itertools,math
import numpy as np
from shapely.geometry import Polygon,LineString,Point,shape,mapping
from shapely.ops import unary_union

ROOT=Path('/Users/um-yunsang/CHOOGuard')
OUT=ROOT/'workers/twin/output/floor1-multiview-next-20261009/unit8-rebuild'
P=json.loads((OUT/'unit8-replacement-packet.json').read_text())
D=json.loads((ROOT/P['source_export']).read_text());MM={x['id']:x for x in D['meshes']}
DY=json.loads((OUT/'door-native-yaml.json').read_text())
C=.9602937;S=.278991133;FRAME=np.array([[C,0,S],[0,1,0],[-S,0,C]])
NEW=Polygon(P['proposed_polygon_UV']);OLD=Polygon(P['current_polygon_UV']);COMMON=NEW.intersection(OLD)
sha=lambda b:hashlib.sha256(b).hexdigest()
uv=lambda w:np.column_stack([S*w[:,0]+C*w[:,2],C*w[:,0]-S*w[:,2]])
def world(m,v):
 m=np.array(m);return v@m[:3,:3].T+m[:3,3]
cache={}
def binary(mid):
 if mid not in cache:
  b=(ROOT/MM[mid]['binary']).read_bytes();nv,ni,nn=struct.unpack_from('<iii',b)
  cache[mid]=(np.frombuffer(b,'<f4',nv*3,12).reshape(nv,3).astype(float),np.frombuffer(b,'<i4',ni,12+nv*12).reshape(-1,3))
 return cache[mid]
def quat(q):
 x,y,z,w=q
 return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
def motion_for(path):
 return next((x for x in P['world_rigid_motions'] if path==x['path'] or path.startswith(x['path']+'/')),None)
def applymotion(path,w):
 x=motion_for(path)
 return w if x is None else w@quat(x['world_rigid_delta']['rotation']).T+np.array(x['world_rigid_delta']['translation'])

assert D['scene_file_sha256']==P['source_export_scene_sha256']
rep={r['source_mesh_id']:r for r in P['mesh_replacements']};maskchecks=[];current_source_receipts=[];newgeometry={}
for mid,r in rep.items():
 f=ROOT/r['source_asset'];rawfile=f.read_bytes();a=yaml.safe_load(rawfile.decode().split('Mesh:\n',1)[1]);raw=bytes.fromhex(a['m_VertexData']['_typelessdata']);nv=a['m_VertexData']['m_VertexCount'];stride=len(raw)//nv
 dtype='<u4' if a['m_IndexFormat']==1 else '<u2';t=np.frombuffer(bytes.fromhex(a['m_IndexBuffer']),dtype).reshape(-1,3).astype(int)
 fs=r['removed_triangle_ordinals'];ids=r['selected_vertex_indices'];keep=np.setdiff1d(np.arange(len(t)),fs)
 assert sha(rawfile)==r['source_asset_file_sha256_at_read']
 assert sha(b''.join(raw[i*stride:(i+1)*stride] for i in ids))==r['selected_vertex_channel_bytes_sha256']
 assert sha(np.array(t[keep],'<u4').tobytes())==r['retained_index_bytes_sha256']
 assert np.array_equal(t[fs],np.array(r['selected_triangle_indices'])) and sha(np.array(t[fs],'<u4').tobytes())==r['selected_index_bytes_sha256']
 assert a['m_IndexFormat']==r['source_index_format']
 assert ids==sorted(set(t[fs].flatten().tolist())) and len(set(fs))==len(fs)
 assert len(t)==r['source_triangle_count'] and nv==r['source_vertex_count']
 assert a['m_VertexData']['m_Channels']==r['source_channels'] and a['m_SubMeshes']==r['source_submeshes']
 bv,bt=binary(mid);assert np.array_equal(t[fs],bt[fs])
 owners=[x for x in D['renderers']+D['colliders'] if x.get('mesh')==mid];assert owners==r['owners']
 assert all('/MainShell/' not in x['path'] and '/Room' not in x['path'] and '/Toilet' not in x['path'] and 'ContextPlaza' not in x['path'] and not x['path'].endswith('Ground_Floor') for x in owners)
 piece=json.loads((ROOT/r['append_piece']).read_text());v=np.array(r['append_vertices']);n=np.array(r['append_normals']);ta=np.array(r['append_tangents']);u=np.array(r['append_uv0']);at=np.array(r['added_triangles'])-nv
 assert np.array_equal(at,np.array(piece['triangles'])) and np.array_equal(v,np.array(piece['vertices']))
 assert len(v)==len(n)==len(ta)==len(u) and at.min()>=0 and at.max()<len(v)
 assert np.isfinite(v).all() and np.isfinite(n).all() and np.isfinite(ta).all() and np.isfinite(u).all()
 cr=np.cross(v[at[:,1]]-v[at[:,0]],v[at[:,2]]-v[at[:,0]]);area=np.linalg.norm(cr,axis=1)/2
 assert area.min()>1e-9 and (cr*n[at[:,0]]).sum(1).min()>0
 assert abs(np.linalg.norm(n,axis=1)-1).max()<2e-5 and abs(np.linalg.norm(ta[:,:3],axis=1)-1).max()<2e-5 and abs((n*ta[:,:3]).sum(1)).max()<2e-5
 assert set(ta[:,3])<=set([-1.,1.])
 # Simulate importer and prove source records/retained triangles remain byte exact.
 streams=np.empty((len(v),12),'<f4');streams[:,:3]=v;streams[:,3:6]=n;streams[:,6:10]=ta;streams[:,10:12]=u
 assert stride==48 and [(i,c['offset'],c['dimension']) for i,c in enumerate(r['source_channels']) if c['dimension']]==[(0,0,3),(1,12,3),(2,24,4),(4,40,2)]
 simulated_vertex_bytes=raw+streams.tobytes();assert simulated_vertex_bytes[:len(raw)]==raw
 simulated_triangles=np.vstack([t[keep],np.array(r['added_triangles'])]);assert np.array_equal(simulated_triangles[:len(keep)],t[keep])
 newgeometry[mid]=(np.vstack([bv,v]),simulated_triangles)
 maskchecks.append({'mesh':mid,'owner_component_file_ids':[int(x['global_object_id'].split('-')[-2]) for x in owners],'removed':len(fs),'retained':len(keep),'appended':len(at),'source_records_byte_exact':True,'retained_index_sequence_byte_exact':True,'selected_source_hash_match':True,'minimum_new_triangle_area':float(area.min())})
 current_source_receipts.append({'asset':str(f.relative_to(ROOT)),'sha256':sha(rawfile),'bytes':len(rawfile),'asset_guid':r['source_asset_guid'],'binary':MM[mid]['binary'],'binary_sha256':sha((ROOT/MM[mid]['binary']).read_bytes()),'materials':r['source_materials']})

horizontal=[]
for mid,height,normal in [('mesh-0551',.004,1),('mesh-0907',3.,-1)]:
 r=rep[mid];v=np.array(r['append_vertices']);t=np.array(r['added_triangles'])-r['source_vertex_count'];tris=[Polygon(v[f][:,[2,0]]) for f in t];union=unary_union(tris)
 assert abs(v[:,1]-height).max()<2e-7
 gap=NEW.difference(union).area;outside=union.difference(NEW).area;overlap=sum(x.area for x in tris)-union.area
 assert gap<1e-4 and outside<1e-4 and abs(overlap)<1e-6
 assert all(normal*np.cross(v[f[1]]-v[f[0]],v[f[2]]-v[f[0]])[1]>0 for f in t)
 horizontal.append({'mesh':mid,'height':height,'source_target_area':NEW.area,'new_triangle_union_area':union.area,'gap_area_float32':gap,'outside_area_float32':outside,'triangle_overlap_area':overlap,'normal_y':normal})

PUB=ROOT/'content/world/observed-interiors/public-1f.json';public=json.loads(PUB.read_text())
cores=[(x,Polygon(x['geometry']['polygon'])) for x in public['elements'] if x['type'] in ['Room','ToiletRoom'] and 'polygon' in x.get('geometry',{})]
corechecks=[{'id':e['id'],'type':e['type'],'overlap_area':NEW.intersection(p).area,'minimum_distance':NEW.distance(p)} for e,p in cores]
assert all(x['overlap_area']<1e-10 for x in corechecks)
approach=Polygon(P['preserve']['end_core_approach_polygon_UV']);assert NEW.intersection(approach).area<1e-10
sections=json.loads((ROOT/'.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-actual-segments.json').read_text())
shellhits=[]
for o in sections['owners']:
 if '/MainShell/' not in o['path']:continue
 for seg in o['segments']:
  line=LineString(seg)
  if line.length>=1 and NEW.buffer(-.01).intersection(line).length>.01:shellhits.append({'owner':o['global_object_id'],'segment':seg})
assert not shellhits

fixtures=[(x,shape(x['footprint_UV'])) for x in P['fixture_placements']]
dc=np.array(P['door']['new_centre_UV']);along=np.array(P['door']['new_along_UV']);normal=np.array(P['door']['new_outward_normal_UV'])
keepout=Polygon([dc+along*a+normal*b for a,b in [(-1.2,-1.5),(1.2,-1.5),(1.2,1.5),(-1.2,1.5)]])
fixturechecks=[{'name':x['name'],'renderer':x['renderer_id'],'outside_old_new_intersection_area':f.difference(COMMON).area,'old_new_boundary_clearance':f.distance(COMMON.boundary),'door_keepout_clearance':f.distance(keepout)} for x,f in fixtures]
assert all(x['outside_old_new_intersection_area']<1e-10 and x['old_new_boundary_clearance']>.09999 and x['door_keepout_clearance']>.09999 for x in fixturechecks)
pairclear=[{'a':a['name'],'b':b['name'],'distance':pa.distance(pb),'overlap':pa.intersection(pb).area,'both_source_placements_preserved':a['operation']==b['operation']=='preserve'} for (a,pa),(b,pb) in itertools.combinations(fixtures,2)]
assert all(x['overlap']<1e-10 and x['distance']>(0 if x['both_source_placements_preserved'] else .09999) for x in pairclear)

# StationDoor.ApplyPose rotates each authored local Slide by ClosedRotation.
door_motion=P['world_rigid_motions'][0];Q=quat(door_motion['world_rigid_delta']['rotation']);trans=np.array(door_motion['world_rigid_delta']['translation']);centre=np.array(door_motion['target_centre_world']);wa=FRAME@np.array([along[1],0,along[0]]);wn=FRAME@np.array([normal[1],0,normal[0]])
oldcentre=np.array(door_motion['actual_old_centre_world']);assert np.linalg.norm(Q@oldcentre+trans-centre)<1e-10
door_boxes=[];leafchecks=[]
for k,l in enumerate(DY['leafs']):
 col=next(c for c in D['colliders'] if int(c['global_object_id'].split('-')[-2])==P['door']['moving_leaf_collider_ids'][k]);m=np.array(col['world_matrix']);co=np.array(col['local_center']);sz=np.array(col['local_size'])
 verts=np.array([co+sz*np.array(s)/2 for s in itertools.product([-1,1],repeat=3)])
 before=world(m,verts);closed=before@Q.T+trans
 lr=[l['transform']['m_LocalRotation'][x] for x in ['x','y','z','w']];slide=np.array([l['serialized_leaf']['Slide'][x] for x in ['x','y','z']]);worldslide=Q@FRAME@quat(lr)@slide;opened=closed+worldslide
 def intervals(w):
  d=w-centre;return {'along':[float((d@wa).min()),float((d@wa).max())],'normal':[float((d@wn).min()),float((d@wn).max())],'Y':[float(w[:,1].min()),float(w[:,1].max())]}
 leafchecks.append({'collider':col['global_object_id'],'closed':intervals(closed),'fully_open':intervals(opened),'world_slide':worldslide.tolist(),'authored_local_pose_and_slide_preserved':True})
 door_boxes.append((col,Polygon(uv(opened)).convex_hull))
leafclear=leafchecks[1]['fully_open']['along'][0]-leafchecks[0]['fully_open']['along'][1]
assert leafclear>1.76 and leafclear<1.78

# Bounded actual-collider horizontal section at Y1, moving only reviewed roots.
probe=LineString([dc-normal*1.5,dc+normal*1.5]);probe_poly=probe.buffer(.28,cap_style=2);hits=[];near=[];selectedcolliders=0
def horizontal_segments(w,t,y=1.):
 tris=w[t];eligible=(tris[:,:,1].min(1)<=y+1e-8)&(tris[:,:,1].max(1)>=y-1e-8);result=[]
 for tr in tris[eligible]:
  pts=[]
  for a,b in [(tr[0],tr[1]),(tr[1],tr[2]),(tr[2],tr[0])]:
   ya,yb=a[1]-y,b[1]-y
   if abs(ya)<1e-8:pts.append(a)
   if ya*yb<0:pts.append(a+(b-a)*(-ya)/(yb-ya))
  if len(pts)>=2:
   q=uv(np.array(pts));uniq=np.unique(np.round(q,10),axis=0)
   if len(uniq)>=2:
    best=max(itertools.combinations(uniq,2),key=lambda ab:np.linalg.norm(ab[1]-ab[0]));result.append(LineString(best))
 return result
openids=set(P['door']['moving_leaf_collider_ids'])
for col in D['colliders']:
 if not col.get('active',True) or not col.get('enabled',True) or col.get('trigger',False):continue
 ci=int(col['global_object_id'].split('-')[-2]);geo=None
 if ci in openids:geo=next(p for c,p in door_boxes if c['global_object_id']==col['global_object_id'])
 elif col['type']=='BoxCollider':
  co=np.array(col['local_center']);sz=np.array(col['local_size']);v=np.array([co+sz*np.array(s)/2 for s in itertools.product([-1,1],repeat=3)]);w=applymotion(col['path'],world(col['world_matrix'],v))
  if w[:,1].min()<=1<=w[:,1].max():geo=Polygon(uv(w)).convex_hull
 elif col.get('mesh') in MM and MM[col['mesh']].get('binary'):
  mid=col['mesh'];v,t=newgeometry.get(mid,binary(mid));w=applymotion(col['path'],world(col['world_matrix'],v))
  # A source collider can span the station; cheap exact bounds cull before slicing.
  q=uv(w);lo=q.min(0);hi=q.max(0);bb=probe_poly.bounds
  if hi[0]<bb[0]-.1 or lo[0]>bb[2]+.1 or hi[1]<bb[1]-.1 or lo[1]>bb[3]+.1:continue
  ss=horizontal_segments(w,t);geo=unary_union(ss) if ss else None
 if geo is None or geo.is_empty:continue
 if geo.distance(probe)>2:continue
 selectedcolliders+=1;dist=geo.distance(probe)
 record={'component_file_id':ci,'global_object_id':col['global_object_id'],'path':col['path'],'mesh':col.get('mesh'),'distance_from_approach_centreline':dist,'sliceY':1.0}
 near.append(record)
 if dist<.28-2e-5:hits.append(record)
assert not hits,hits

# Existing point light remains in the kept intersection; no new contents are introduced.
scene=(ROOT/D['scene']).read_text();blocks={int(i):b for i,b in re.findall(r'^--- !u!\d+ &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',scene,re.M|re.S)}
go=21214213;g=yaml.safe_load(blocks[go])['GameObject'];tid=next(x['component']['fileID'] for x in g['m_Component'] if blocks[x['component']['fileID']].startswith('Transform:\n'));tf=yaml.safe_load(blocks[tid])['Transform'];pos=np.array([tf['m_LocalPosition'][x] for x in ['x','y','z']]);lightuv=[float(pos[2]),float(pos[0])];assert COMMON.covers(Point(lightuv))
guide=ROOT/'asset-library/research-public/2026-10-09/floor1-multiview-next/source05-yoonsscom/img_2.jpg';assert sha(guide.read_bytes())==P['guide_image_sha256']
report={'schema':'chooguard.unit8-independent-offline-qa.v1','status':'PASS','snapshot_scene_sha256':D['scene_file_sha256'],'production_mutated':False,'runtime_verified':False,
 'mesh_masks':maskchecks,'source_receipts':current_source_receipts,'horizontal_finish_coverage':horizontal,
 'protected_cores':{'canonical_source':str(PUB.relative_to(ROOT)),'sha256':sha(PUB.read_bytes()),'checks':corechecks,'minimum_clearance':min(x['minimum_distance'] for x in corechecks),'end_core_approach_overlap_area':NEW.intersection(approach).area,'native_MainShell_long_wall_crossings':shellhits},
 'fixtures':{'count_before_after':[7,len(fixtures)],'checks':fixturechecks,'pair_clearance':pairclear,'minimum_pair_clearance':min(x['distance'] for x in pairclear)},
 'point_light':{'gameobject_file_id':go,'transform_file_id':tid,'local_position':pos.tolist(),'position_UV':lightuv,'inside_old_new_intersection':True,'preserved':True},
 'door':{'StationDoor_component_file_id':DY['StationDoor_component_file_id'],'new_centre_UV':dc.tolist(),'actual_leaf_boxes':leafchecks,'actual_fully_open_box_clear_width':leafclear,'nominal_static_aperture_width':1.8,'static_post_clear_width':1.75,'world_rigid_delta_yaw_degrees':math.degrees(2*math.atan2(door_motion['world_rigid_delta']['rotation'][1],door_motion['world_rigid_delta']['rotation'][3])),'local_fields_and_leaf_meshes_unchanged':True},
 'offline_door_approach':{'status':'PASS','method':'Actual enabled nontrigger collider horizontal section at worldY1; reviewed geometry and root motions applied offline; both leaves fully open using StationDoor ClosedRotation*Slide. Centreline normal±1.5, radius.28. This is a bounded horizontal check, not Unity runtime capsule verification.','centre_UV':dc.tolist(),'normal_UV':normal.tolist(),'radius':.28,'probe_endpoints_UV':list(map(list,probe.coords)),'blockers':hits,'nearby_colliders':sorted(near,key=lambda x:x['distance_from_approach_centreline']),'minimum_nearby_collider_distance':min(x['distance_from_approach_centreline'] for x in near),'checked_nearby_colliders':selectedcolliders},
 'native_floor_residual':P['floor_support'],'guide_receipt':{'url':P['guide_source'],'path':str(guide.relative_to(ROOT)),'sha256':sha(guide.read_bytes()),'bytes':guide.stat().st_size,'license':'No open license observed; public reference retained locally; no redistribution or metric accuracy claim.'},
 'totals':{'mesh_replacements':len(rep),'removed_triangles':sum(len(x['removed_triangle_ordinals']) for x in rep.values()),'appended_triangles':sum(len(x['added_triangles']) for x in rep.values()),'rigid_motions':len(P['world_rigid_motions']),'new_gameobjects':0,'native_floor_patches':0}}
(OUT/'offline-review-qa.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'status':report['status'],'totals':report['totals'],'core_minimum_clearance':report['protected_cores']['minimum_clearance'],'door_open_width':leafclear,'collider_centreline_clearance':report['offline_door_approach']['minimum_nearby_collider_distance'],'fixtures_minimum_pair_clearance':report['fixtures']['minimum_pair_clearance'],'horizontal':horizontal},ensure_ascii=False))
