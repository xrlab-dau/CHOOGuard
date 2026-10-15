import sys,json,math,collections
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path('/Users/um-yunsang/CHOOGuard');OUT=ROOT/'workers/simulator/geometry_runtime/car04-source-qualification'
exec((OUT/'source_scan.py').read_text().split('records=[]')[0])
vertices=[];triangles=[];metadata=[];seat_report=[]
for obj in sorted(bpy.data.objects,key=lambda o:o.name):
 if obj.type!='MESH':continue
 vs=[Vector(frame(obj.matrix_world@v.co)) for v in obj.data.vertices];start=len(vertices);vertices.extend(vs);obj.data.calc_loop_triangles()
 for t in obj.data.loop_triangles:triangles.append(tuple(start+i for i in t.vertices));metadata.append({'mesh':obj.name,'triangle':len(triangles)-1,'source_polygon':t.polygon_index,'source_indices':list(t.vertices)})
 if not obj.name.startswith('Car04_') or '_Seat_' not in obj.name:continue
 # Weld source-coordinate duplicates only; disconnected geometric pieces remain explicit.
 key=lambda v:tuple(round(float(x),5) for x in v)
 adjacency=collections.defaultdict(set);positions={}
 for t in obj.data.loop_triangles:
  ks=[key(vs[i]) for i in t.vertices]
  for k in ks:positions[k]=k;adjacency[k].update(ks)
 remaining=set(adjacency);components=[]
 while remaining:
  seed=min(remaining);remaining.remove(seed);found={seed};todo=[seed]
  while todo:
   for n in adjacency[todo.pop()]&remaining:remaining.remove(n);found.add(n);todo.append(n)
  coords=list(found);components.append({'vertices':len(found),'bounds_uvh':{'min':[min(v[a] for v in coords) for a in range(3)],'max':[max(v[a] for v in coords) for a in range(3)]}})
 seat_report.append({'mesh':obj.name,'materials':[s.material.name for s in obj.material_slots],'source_vertices':len(vs),'components':components})
bvh=BVHTree.FromPolygons(vertices,triangles,all_triangles=True)
def floor(u,v):
 hit,normal,index,d=bvh.ray_cast(Vector((u,v,.72)),Vector((0,0,-1)),.9)
 return {'u':u,'v':v,'height':None if hit is None else float(hit.z),'normal':None if normal is None else list(normal),'mesh':None if index is None else metadata[index]['mesh'],'source_polygon':None if index is None else metadata[index]['source_polygon']}
strips=[]
for name,u0,u1 in [('car03-car04',1.7,4.4),('car04-car05',-17.1,-14.3)]:
 rows=[floor(round(u0+i*.05,5),v) for i in range(round((u1-u0)/.05)+1) for v in (1.15,1.4,1.65)]
 strips.append({'connection':name,'samples':rows})
# Interior, source-only clearance at centerline and lateral alternatives.
clearance=[]
for name,u0,u1 in [('car03-car04',1.7,4.4),('car04-car05',-17.1,-14.3)]:
 for i in range(round((u1-u0)/.1)+1):
  u=u0+i*.1
  for v in (1.15,1.4,1.65):
   near=[]
   fh=floor(u,v)['height']
   for h in (fh+.3,fh+.6,fh+.9,fh+1.2,fh+1.42):
    location,normal,index,dist=bvh.find_nearest(Vector((u,v,h)))
    near.append({'center_height':h,'distance':float(dist),'mesh':metadata[index]['mesh'],'source_polygon':metadata[index]['source_polygon']})
   clearance.append({'connection':name,'u':u,'v':v,'source_floor_height':fh,'minimum_distance':min(x['distance'] for x in near),'nearest_samples':near})
result={'schema':'car04-source-routes-seats-v1','source_sha256_before':before,'source_sha256_after':coverage.digest(source),'source_unchanged':before==coverage.digest(source),'frame':{'origin_world':list(origin),'longitudinal_axis_world':list(axis),'inward_axis_world':list(inward)},'source_triangles':len(triangles),'floor_strips':strips,'clearance_strips':clearance,'seat_meshes':seat_report,'claim':'Source FBX geometry only; floor-support and nearest-surface offline evidence, not Unity CharacterController/NavMesh acceptance.'}
(OUT/'source-routes-seats.json').write_text(json.dumps(result,indent=2));print(json.dumps({'source_unchanged':result['source_unchanged'],'triangles':len(triangles),'seat_meshes':[(x['mesh'],len(x['components'])) for x in seat_report]}))
