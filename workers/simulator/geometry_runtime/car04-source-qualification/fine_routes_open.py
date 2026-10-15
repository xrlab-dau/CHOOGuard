import sys,json,math,collections
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path('/Users/um-yunsang/CHOOGuard');OUT=ROOT/'workers/simulator/geometry_runtime/car04-source-qualification'
exec((OUT/'route_seat_scan.py').read_text().split('strips=[]')[0])
contract=json.loads((OUT/'source-leaf-contract.json').read_text())
selection={}
for leaf in contract['leaves']:
 for part in leaf['source_parts']:
  for polygon in part['blender_triangles']:selection[(part['source_mesh'],polygon)]=leaf['open_delta_uvh']
opened=[];moved=collections.Counter()
for ti,(triangle,record) in enumerate(zip(triangles,metadata)):
 delta=selection.get((record['mesh'],record['source_polygon']))
 if delta is None:opened.append(triangle);continue
 start=len(vertices);vertices.extend(vertices[v]+Vector(delta) for v in triangle);opened.append((start,start+1,start+2));moved[record['mesh']]+=1
assert sum(moved.values())==552
bvh=BVHTree.FromPolygons(vertices,opened,all_triangles=True)
report=[]
def world(u,v,h):return list(origin+axis*u+inward*v+Vector((0,h,0)))
for name,start,stop in [('car03-to-car04',6.0,-13.0),('car05-to-car04',-19.2,-1.5)]:
 du=.005;dh=.005;count=round(abs(stop-start)/du);sign=1 if stop>start else -1;missing=[];minclear=100;minrow=None;previous=None;maxslope=0;supports=collections.Counter();waypoints=[];bad=[]
 for i in range(count+1):
  u=start+sign*i*du;f=floor(u,1.4);h=f['height']
  if h is None:missing.append(u);continue
  supports[f['mesh']]+=1
  if previous is not None:maxslope=max(maxslope,abs(h-previous)/du)
  previous=h
  if i%100==0 or i==count:waypoints.append({'u':u,'v':1.4,'source_floor_height':h,'world':world(u,1.4,h),'source_floor_mesh':f['mesh']})
  for j in range(round(1.12/dh)+1):
   center=Vector((u,1.4,h+.3+j*dh));loc,n,index,dist=bvh.find_nearest(center)
   if dist<minclear:minclear=float(dist);minrow={'u':u,'v':1.4,'center_height':float(center.z),'distance':minclear,'source_mesh':metadata[index]['mesh'],'source_polygon':metadata[index]['source_polygon']}
   if dist<.28:bad.append({'u':u,'height':float(center.z),'distance':float(dist),'source_mesh':metadata[index]['mesh']})
 # A conservative nearest-surface sampling cover bound for route slope and vertical sampling.
 cover=math.sqrt((du*.5)**2+((maxslope*du+dh)*.5)**2)
 report.append({'route':name,'source_only':True,'no_new_floor':True,'source_meshes_unchanged':True,'derived_cabin_leaf_triangles_moved':552,'sample_spacing_longitudinal_m':du,'sample_spacing_vertical_m':dh,'capsule_radius_m':.28,'capsule_center_above_floor_range_m':[.3,1.42],'sampled_route_points':count+1,'sampled_sphere_centers':(count+1)*(round(1.12/dh)+1),'floor_support_missing':missing,'max_floor_slope':maxslope,'source_floor_mesh_counts':dict(supports),'minimum_sampled_clearance_m':minclear,'nearest_surface':minrow,'sampling_cover_bound_m':cover,'conservative_continuous_clearance_lower_bound_m':minclear-cover,'capsule_clearance_qualified_offline':not missing and minclear-cover>=.28,'intersections':bad,'waypoints':waypoints})
result={'schema':'car04-source-intercar-routes-v1','source_sha256_before':before,'source_sha256_after':coverage.digest(source),'source_unchanged':before==coverage.digest(source),'routes':report,'claim':'Offline reversible source-leaf-open support and conservative capsule-envelope qualification only. Actual Unity capsule motion and NavMesh path/replay still required.'}
(OUT/'source-intercar-open-route-packet.json').write_text(json.dumps(result,indent=2));print(json.dumps([{k:v for k,v in x.items() if k not in ('waypoints','intersections','floor_support_missing')} for x in report]))
