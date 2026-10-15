import bpy,json,math
from pathlib import Path
from mathutils import Matrix,Vector
out=Path('/Users/um-yunsang/CHOOGuard/workers/twin/evidence/remaining-elevator-pair-20261009')
root=Path('/Users/um-yunsang/CHOOGuard')
rows=json.loads((root/'.planning/2026-10-08-interior-runtime-completion/native-station-door-snapshot.json').read_text())
stops=[r for r in rows if r['kind']=='Elevator' and r['element']not in ['B-ELEV2F-wing','3F-ELEV-wing']]
p=json.loads((root/'workers/twin/output/remaining-native-doors-20261009/sliding-leaf-dependencies.json').read_text())
R=Matrix(p['source_to_current_world_rotation']);T=Vector(p['source_to_current_world_translation'])
records=[]
for ob in bpy.data.objects:
 if ob.type!='MESH':continue
 points=[R@(ob.matrix_world@Vector(q))+T for q in ob.bound_box]
 lo=[min(v[k]for v in points)for k in range(3)];hi=[max(v[k]for v in points)for k in range(3)]
 if any(hi[k]-lo[k]>8 for k in [0,2]):continue
 matched=[]
 for s in stops:
  c=s['centre_world']
  dx=max(lo[0]-c[0],c[0]-hi[0],0);dz=max(lo[2]-c[2],c[2]-hi[2],0)
  if math.hypot(dx,dz)<=4 and hi[1]>=-1 and lo[1]<=18:matched.append({'element':s['element'],'index':s['index'],'horizontal_aabb_distance_m':math.hypot(dx,dz)})
 if not matched:continue
 parents=[];cur=ob
 while cur:
  parents.append({'name':cur.name,'source_id':cur.get('source_id'),'source_path':cur.get('source_path'),'source_name':cur.get('source_name')});cur=cur.parent
 records.append({'name':ob.name,'source_id':ob.get('source_id'),'source_path':ob.get('source_path'),'source_name':ob.get('source_name'),'mesh_source_id':ob.get('source_mesh_id',ob.data.get('source_id')),'vertices':len(ob.data.vertices),'triangles':len(ob.data.polygons),'world_bounds_min':lo,'world_bounds_max':hi,'parents':parents[:8],'matched_stops':matched,'materials':[m.name if m else None for m in ob.data.materials]})
(out/'source-near-six-landings.json').write_text(json.dumps({'frame_basis':'existing native-door source-to-current rigid registration; model consistency only, not site survey','source_blend':str(root/'asset-library/research-public/2026-10-08/measured-twin-parts/extraction/station-source-parts.blend'),'stop_count':len(stops),'search_radius_m':4,'filter':'full source MESH AABB X/Z widths at most8m, Y intersects[-1,18]','records':records},ensure_ascii=False,indent=2)+'\n')
print('SOURCE_NEAR_SIX_LANDINGS',len(records))
for s in stops:
 a=[r for r in records if any(x['element']==s['element']and x['index']==s['index']for x in r['matched_stops'])]
 print(s['element'],s['index'],'local_source_meshes',len(a))
