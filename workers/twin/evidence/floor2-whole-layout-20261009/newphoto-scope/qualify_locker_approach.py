from pathlib import Path
import json,hashlib,numpy as np
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-whole-layout-20261009/newphoto-scope/locker-withholding')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
p=json.loads((O/'locker-withholding-packet.json').read_text());n=json.loads((O.parent/'namcheon-source-front-packet.json').read_text());g=json.loads((E/'source-gallery-geometry.json').read_text())
F=np.array(n['native_frame']);floor=next(o for o in g['regions']['namcheon-gallery']if o['source_path']=='ROOT/g58/g32/g4/geometry');q=np.array(floor['native_vertices']);tri=q[np.array(floor['source_polygons'])];ids=np.flatnonzero((tri[:,:,2].max(1)-tri[:,:,2].min(1)<.001)&(abs(tri[:,:,2].mean(1)-7)<.001));fs=tri[ids];a=fs[:,0,:2];D=np.stack([fs[:,1,:2]-a,fs[:,2,:2]-a],axis=2);valid=abs(np.linalg.det(D))>1e-8;a=a[valid];fs=fs[valid];ids=ids[valid];DI=np.linalg.inv(D[valid])
clear=.3+.02+.05;front=np.array(p['recipe']['source_front_VYU']);edge=np.array(p['withheld_element']['geometry']['edge']);back=edge[:,1].mean();start=front.copy();start[0]-=clear;after=front.copy();after[0]=back+clear
# Local XYZ is V,Y,U. Door World transforms are the accepted current parent frame and unchanged source fields.
before=n['door_parent_pose']['station_fields_before'];C=np.array([before['Centre'][k]for k in 'xyz']);N=np.array([before['Normal'][k]for k in 'xyz']);M=np.array(n['door_parent_pose']['expected_parent_world_matrix']);world=lambda v:F@v
startw=world(start);afterw=world(after);outside=(M@np.r_[C+N,1])[:3];inside=(M@np.r_[C-N,1])[:3];out=np.linalg.solve(F,outside);ins=np.linalg.solve(F,inside)
nodes=[start,after,out,ins,out,after,start];labels=['spawn_before_former_front','past_former_back','door_outside','door_inside','door_outside_return','past_former_back_return','start_return'];samples=[]
for si,(aa,bb) in enumerate(zip(nodes[:-1],nodes[1:])):
 count=int(np.ceil(np.linalg.norm((bb-aa)[[0,2]])/.05))
 for k in range(count+1):samples.append((si,k,aa+(bb-aa)*(k/count)))
rows=[];fail=[];used=set()
for si,k,xyz in samples:
 hits=[]
 for angle in [None]+list(np.linspace(0,2*np.pi,16,endpoint=False)):
  uv=xyz[[2,0]].copy()
  if angle is not None:uv+=.3*np.array([np.cos(angle),np.sin(angle)])
  st=np.einsum('nij,nj->ni',DI,uv-a);w=np.c_[1-st.sum(1),st];insideids=np.flatnonzero(w.min(1)>=-1e-7)
  if not len(insideids):fail.append({'segment':si,'sample':k,'UV':uv.tolist(),'angle':angle});continue
  ii=int(insideids[0]);y=float(w[ii]@fs[ii,:,2]);assert abs(y-7)<.001;used.add(int(ids[ii]));hits.append({'source_face':int(ids[ii]),'native_UV':uv.tolist(),'floorY':y})
 rows.append({'segment':si,'sample':k,'centre_native_VYU':xyz.tolist(),'floor_hits':hits})
# Match current collider floors independently in exact completed packet channels/indices.
namespace={'Path':Path,'np':np,'struct':__import__('struct'),'hashlib':hashlib,'hb':lambda b:hashlib.sha256(b).hexdigest()};s=Path('workers/twin/evidence/floor2-whole-layout-20261009/seal_native_stair_channels.py').read_text();exec(s[s.index('def ints'):s.index('entries={')],namespace)
x=n['native_wall_clips'][1];entry={**x['full_channel_export'],**x['expected_output']};entry['complete_channel_digest']=entry.pop('expected_complete_channel_digest');d=namespace['read'](entry);assert namespace['complete'](d)==entry['complete_channel_digest'];verts=d['vertices'];idx=np.concatenate([t['indices']for t in d['submeshes']]).reshape(-1,3);A=np.array(x['owner_receipt']['owner']['world_matrix']);w=verts@A[:3,:3].T+A[:3,3];native=(w@np.linalg.inv(F).T)[:,[2,0,1]];t=native[idx];flt=(abs(t[:,:,2].mean(1)-7)<.001)&(np.ptp(t[:,:,2],axis=1)<.001);candidate=np.flatnonzero(flt);maps=[]
for face in sorted(used):
 source=tri[face];best=[]
 for ci in candidate:
  for winding in [1,-1]:
   oriented=t[ci] if winding==1 else t[ci][[0,2,1]]
   for rot in range(3):
    er=float(np.max(abs(np.roll(oriented,rot,axis=0)-source)))
    if er<.001:best.append({'current_flattened_triangle':int(ci),'source_to_current_winding':winding,'cyclic_rotation':rot,'maximum_native_coordinate_error':er,'indices':idx[ci].tolist()})
 assert len(best)==1,(face,best);maps.append({'source_face':face,**best[0]})
proof={'schema':'chooguard.locker-approach-source-floor-preflight.v1','status':'PASS'if not fail else'FAIL','runtime_execution_performed':False,'floor_appended':False,'source_packets':[{'path':str(O/'locker-withholding-packet.json'),'sha256':sha(O/'locker-withholding-packet.json')},{'path':str(O.parent/'namcheon-source-front-packet.json'),'sha256':sha(O.parent/'namcheon-source-front-packet.json')}],'source_geometry':{'path':str(E/'source-gallery-geometry.json'),'sha256':sha(E/'source-gallery-geometry.json'),'source_blend_sha256':g['source_blend_sha256'],'source_floor_path':floor['source_path'],'actual_floor_native_Y':7},'current_collider':{'owner':x['owner_receipt']['owner'],'binary':entry,'source_face_exact_current_triangle_correspondence':maps},'capsule_radius':.3,'capsule_height':1.72,'footprint_ring_samples_per_centre':16,'clearance_derivation':'.3 capsule radius + .02 skin + .05 one movement step','nodes':[{'label':label,'native_VYU':v.tolist(),'world_XYZ':world(v).tolist()}for label,v in zip(labels,nodes)],'source_floor_samples':rows,'failed_floor_samples':fail,'sample_centres':len(rows),'qualified_footprint_points':sum(len(r['floor_hits'])for r in rows),'maximum_horizontal_step':max(float(np.linalg.norm((bb-aa)[[0,2]])/int(np.ceil(np.linalg.norm((bb-aa)[[0,2]])/.05)))for aa,bb in zip(nodes[:-1],nodes[1:])),'former_bank_source_box':{'native_U':[-37.5-p['recipe']['width']/2,-37.5+p['recipe']['width']/2],'native_V':[float(front[0]),float(back)],'native_Y':[7,7+p['recipe']['height']]}}
# All nodes and all linear route segments lie in one convex original floor triangle.
# Minimum edge-distance at segment endpoints bounds every intermediate center;
# a radius .3 disk then fits for the complete continuous sweep, not just the sample ring.
assert len(used)==1
supportTriangle=tri[next(iter(used)),:,:2];edges=np.roll(supportTriangle,-1,axis=0)-supportTriangle;length=np.linalg.norm(edges,axis=1);margins=[]
for label,node in zip(labels,nodes):
 uv=node[[2,0]];cross=edges[:,0]*(uv-supportTriangle)[:,1]-edges[:,1]*(uv-supportTriangle)[:,0];signed=cross/length
 assert np.all(signed>0)or np.all(signed<0)
 distances=abs(signed);assert distances.min()>.3
 margins.append({'label':label,'minimum_distance_to_source_triangle_edge':float(distances.min()),'full_capsule_floor_disk_margin':float(distances.min()-.3)})
proof['continuous_floor_sweep']={'method':'Source floor triangle33 is convex. Each route node lies inside its radius-.3 erosion; therefore every full circular footprint along every linear connecting segment lies in the same original floor triangle. No floor extension.','radius':.3,'nodes':margins,'minimum_full_disk_margin':min(x['full_capsule_floor_disk_margin']for x in margins),'complete_continuous_sweep_supported':True}
(O/'locker-approach-source-floor-proof.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n');print('status',proof['status'],'samplecentres',len(rows),'footprintpoints',proof['qualified_footprint_points'],'usedfaces',maps,'fails',len(fail));print('nodes',proof['nodes']);assert not fail
