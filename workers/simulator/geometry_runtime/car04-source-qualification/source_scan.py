import sys, json, hashlib, math, importlib.util
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path('/Users/um-yunsang/CHOOGuard')
OUT=ROOT/'workers/simulator/geometry_runtime/car04-source-qualification'
spec=importlib.util.spec_from_file_location('coverage',ROOT/'workers/simulator/Tools/coverage_inventory.py');coverage=importlib.util.module_from_spec(spec);spec.loader.exec_module(coverage)
source=ROOT/coverage.SOURCES['ktx'];before=coverage.digest(source)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source))
station=json.loads((ROOT/coverage.SOURCES['station-points']).read_text());candidate=json.loads((ROOT/'workers/simulator/data/semantic-coverage-car04-candidate.json').read_text())['candidate']
axis=Vector(tuple(station['train']['axis'][k] for k in ('x','y','z'))).normalized()
a=Vector(tuple(candidate['door']['outside'][k] for k in ('x','y','z')));b=Vector(tuple(candidate['door']['inside'][k] for k in ('x','y','z')))
inward=b-a;inward.y=0;inward.normalize();origin=a+inward*.8
def frame(v):
 w=Vector(coverage.source_to_world(v));d=w-origin
 return [float(d.dot(axis)),float(d.dot(inward)),float(w.y)]
def bounds(vs):return {'min':[min(v[i] for v in vs) for i in range(3)],'max':[max(v[i] for v in vs) for i in range(3)]}
records=[]
for car in sorted((o for o in bpy.data.objects if o.type=='EMPTY' and o.name.startswith('Car0')),key=lambda o:o.name):
 for obj in sorted((o for o in car.children_recursive if o.type=='MESH'),key=lambda o:o.name):
  vs=[frame(obj.matrix_world@v.co) for v in obj.data.vertices]
  mats=[s.material.name if s.material else None for s in obj.material_slots]
  row={'car':car.name,'name':obj.name,'vertices':len(vs),'polygons':len(obj.data.polygons),'materials':mats,'bounds_uvh':bounds(vs),'matrix_world':[list(r) for r in obj.matrix_world]}
  # Car04 body/doors plus adjacent-car source comparators.
  if any(s in obj.name for s in ('_in_','_out_','Door_','_D L_','_D R_')) and (car.name.startswith(('Car03_','Car04_','Car05_'))):
   obj.data.calc_loop_triangles()
   row['positions_uvh']=vs;row['positions_local']=[list(v.co) for v in obj.data.vertices]
   row['triangles']=[{'id':i,'indices':list(t.vertices),'polygon':t.polygon_index,'material':t.material_index} for i,t in enumerate(obj.data.loop_triangles)]
   row['uv_layers']=[u.name for u in obj.data.uv_layers]
  records.append(row)
result={'schema':'car04-source-qualification-v1','tool':'Blender '+bpy.app.version_string,'source_path':str(source.relative_to(ROOT)),'source_sha256_before':before,'source_sha256_after':coverage.digest(source),'source_unchanged':coverage.digest(source)==before,'frame':{'origin_world':list(origin),'longitudinal_axis_world':list(axis),'inward_axis_world':list(inward),'axes':['longitudinal_U_m','inward_V_m','world_height_m']},'meshes':records}
(OUT/'source-geometry.json').write_text(json.dumps(result,separators=(',',':')))
print(json.dumps({'source_unchanged':result['source_unchanged'],'meshes':len(records),'exported_triangle_meshes':sum('triangles' in x for x in records),'out':str(OUT/'source-geometry.json')}))
