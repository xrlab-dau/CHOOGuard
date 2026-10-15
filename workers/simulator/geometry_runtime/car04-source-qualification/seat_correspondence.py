import sys,json,math,statistics,collections
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path('/Users/um-yunsang/CHOOGuard');OUT=ROOT/'workers/simulator/geometry_runtime/car04-source-qualification'
exec((OUT/'route_seat_scan.py').read_text().split('strips=[]')[0])
source_stats=json.loads((OUT/'source-routes-seats.json').read_text());fabric=source_stats['seat_meshes'][0]
paired=[c for c in fabric['components'] if c['bounds_uvh']['max'][2]-c['bounds_uvh']['min'][2]>.8 and c['bounds_uvh']['max'][1]-c['bounds_uvh']['min'][1]>.9]
rows=[]
for c in sorted(paired,key=lambda x:sum(x['bounds_uvh'][k][0] for k in ('min','max'))):
 center=sum(c['bounds_uvh'][k][0] for k in ('min','max'))*.5
 if not rows or abs(rows[-1]['u']-center)>.02:rows.append({'u':center,'pairs':[c]})
 else:rows[-1]['pairs'].append(c);rows[-1]['u']=statistics.mean(sum(x['bounds_uvh'][k][0] for k in ('min','max'))*.5 for x in rows[-1]['pairs'])
seatverts=[];seattris=[]
for obj in bpy.data.objects:
 if obj.type!='MESH' or not obj.name.startswith('Car04_') or '_Seat_' not in obj.name:continue
 start=len(seatverts);seatverts.extend(Vector(frame(obj.matrix_world@v.co)) for v in obj.data.vertices);obj.data.calc_loop_triangles();seattris.extend(tuple(start+i for i in t.vertices) for t in obj.data.loop_triangles)
seatbvh=BVHTree.FromPolygons(seatverts,seattris,all_triangles=True)
floor_tris=[];floor_meta=[]
for triangle,record in zip(triangles,metadata):
 if '_Seat_' not in record['mesh']:floor_tris.append(triangle);floor_meta.append(record)
floor_bvh=BVHTree.FromPolygons(vertices,floor_tris,all_triangles=True)
def structural_floor(u,v):
 hit,n,index,d=floor_bvh.ray_cast(Vector((u,v,.60)),Vector((0,0,-1)),.9)
 return {'height':None if hit is None else float(hit.z),'mesh':None if index is None else floor_meta[index]['mesh'],'source_polygon':None if index is None else floor_meta[index]['source_polygon']}
result=[]
for ri,row in enumerate(rows,1):
 for pair in sorted(row['pairs'],key=lambda c:c['bounds_uvh']['min'][1]):
  lo=pair['bounds_uvh']['min'];hi=pair['bounds_uvh']['max'];mid=(lo[1]+hi[1])*.5
  for s,(v0,v1) in enumerate(((lo[1],mid),(mid,hi[1]))):
   v=(v0+v1)*.5;center_u=row['u'];samples=[]
   for i in range(65):
    u=lo[0]+(hi[0]-lo[0])*i/64
    hit,n,idx,d=seatbvh.ray_cast(Vector((u,v,1.15)),Vector((0,0,-1)),.6)
    if hit is not None and .748<float(hit.z)<1.05 and abs(n.z)>.65:samples.append([float(hit.x),float(hit.y),float(hit.z)])
   upper=[p for p in seatverts if lo[0]-.01<=p.x<=hi[0]+.01 and v0+.04<=p.y<=v1-.04 and p.z>1.25]
   pad_u=statistics.median(p[0] for p in samples) if samples else None
   back_u=statistics.median(float(p.x) for p in upper) if upper else None
   facing=1 if pad_u is not None and back_u is not None and pad_u>back_u else -1
   anchor=[pad_u,v,statistics.median(p[2] for p in samples)] if samples else None
   anchor_floor=structural_floor(pad_u,v) if anchor else None
   result.append({'id':'04-SOURCE-'+str(ri).zfill(2)+chr(65+len([x for x in result if x['row']==ri])),'row':ri,'row_source_center_u':row['u'],'pair_bounds_uvh':pair['bounds_uvh'],'seat_lateral_interval_m':[v0,v1],'seat_center_v':v,'pad_samples':len(samples),'pad_surface_uvh':anchor,'backrest_source_vertex_samples':len(upper),'backrest_median_u':back_u,'facing_longitudinal_sign':facing,'floor_support':anchor_floor,'proposed_floor_anchor_world':list(origin+axis*pad_u+inward*v+Vector((0,anchor_floor['height'],0))) if anchor_floor else None,'proposed_aisle_approach_world':list(origin+axis*(pad_u+facing*.46)+inward*1.4+Vector((0,.44802487,0))) if anchor else None})
# Candidate matching is diagnostic only: these are a new source packet, not runtime activation.
old=json.loads((ROOT/'workers/simulator/data/semantic-coverage-car04-candidate.json').read_text())['candidate']['seats']
oldrows=[]
for s in old:
 p=Vector(tuple(s['anchor'][k] for k in ('x','y','z')))-origin;u=p.dot(axis);v=p.dot(inward)
 best=min(result,key=lambda q:(q['pad_surface_uvh'][0]-u)**2+(q['seat_center_v']-v)**2)
 oldrows.append({'old_id':s['id'],'old_uv':[u,v],'nearest_source_seat':best['id'],'distance_horizontal_m':math.hypot(best['pad_surface_uvh'][0]-u,best['seat_center_v']-v)})
report={'schema':'car04-source-seat-correspondence-v1','source_sha256_before':before,'source_sha256_after':coverage.digest(source),'source_unchanged':before==coverage.digest(source),'geometric_rows':len(rows),'paired_seat_units':len(paired),'source_individual_seat_candidates':len(result),'all_pad_surfaces_found':all(s['pad_samples']>0 for s in result),'seats':result,'prior_candidate_comparison':oldrows,'claim':'Originalsource topology repetition plus individual cushion/backrest and underlyingfloor correspondence. Proposed modelseat count; actual UI/NPC route/seat arrival activation requires Unity validation. Not site surveyed seat labels.'}
(OUT/'source-seat-correspondence-packet.json').write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k not in ('seats','prior_candidate_comparison')}));print(json.dumps([{'id':s['id'],'pad':s['pad_surface_uvh'],'facing':s['facing_longitudinal_sign'],'samples':s['pad_samples']} for s in result]))
