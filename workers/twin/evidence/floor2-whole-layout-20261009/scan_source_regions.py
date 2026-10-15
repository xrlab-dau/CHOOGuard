import bpy,json,hashlib
from pathlib import Path
from mathutils import Matrix,Vector

root=Path('/Users/um-yunsang/CHOOGuard')
out=root/'workers/twin/evidence/floor2-whole-layout-20261009'
d=json.loads((root/'workers/twin/output/remaining-native-doors-20261009/sliding-leaf-dependencies.json').read_text())
R=Matrix(d['source_to_current_world_rotation']);T=Vector(d['source_to_current_world_translation'])
F=Matrix(json.loads((root/'workers/twin/output/floor2-main-restoration-20261009/whole-guide-current-audit.json').read_text())['native_station_frame']).inverted()
nodes=json.loads((root/'workers/twin/output/floor2-main-restoration-20261009/facility-native-controls.json').read_text())
ids=nodes['summary']['unresolved_unique_source_assignment']
regions={x['id']:x['native_control_bounds_UV'] for x in nodes['facility_nodes']if x['id']in ids}
records={k:[]for k in regions}
def q(v):
    w=F@(R@v+T)
    return Vector((w.z,w.x,w.y))
def lineage(o):
    result=[]
    while o:
        result.append({'source_id':o.get('source_id'),'source_path':o.get('source_path'),'name':o.name,'source_layer':o.get('source_layer')})
        o=o.parent
    return result
scanned=0
for o in bpy.data.objects:
    if o.type!='MESH':continue
    # Exact mesh AABB only filters navigation; detailed triangles follow for bounded meshes.
    bbox=[q(o.matrix_world@Vector(v))for v in o.bound_box]
    lo=[min(v[k]for v in bbox)for k in range(3)];hi=[max(v[k]for v in bbox)for k in range(3)]
    if hi[2]<-.1 or lo[2]>15:continue
    targets=[k for k,b in regions.items()if hi[0]>b[0]-1 and lo[0]<b[2]+1 and hi[1]>b[1]-1 and lo[1]<b[3]+1]
    if not targets:continue
    scanned+=1
    row={'source_id':o.get('source_id'),'source_path':o.get('source_path'),'name':o.name,'native_UVY_bounds':[lo,hi],
         'vertices':len(o.data.vertices),'triangles':len(o.data.polygons),'material_slots':[m.name if m else None for m in o.data.materials],
         'source_world_matrix':[list(r)for r in o.matrix_world],'effective_hidden':bool(o.get('effective_hidden',False)),
         'ancestors':lineage(o.parent)}
    # Preserve exact source vertices/indices for all bounded local meshes, no synthetic part recovery.
    if hi[0]-lo[0]<30 and hi[1]-lo[1]<30 and len(o.data.vertices)<20000:
        verts=[q(o.matrix_world@v.co)for v in o.data.vertices]
        row['native_vertices']=[list(v)for v in verts]
        row['source_local_vertices']=[list(v.co)for v in o.data.vertices]
        row['source_polygons']=[list(p.vertices)for p in o.data.polygons]
        row['source_material_indices']=[p.material_index for p in o.data.polygons]
    for k in targets:records[k].append(row)
path=root/'asset-library/research-public/2026-10-08/measured-twin-parts/extraction/station-source-parts.blend'
result={'source_blend':str(path),'source_blend_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'frame_rotation':d['source_to_current_world_rotation'],
        'frame_translation':d['source_to_current_world_translation'],'inspection_bounds_not_dimensions':regions,'scanned_overlapping_meshes':scanned,
        'regions':records,'accuracy':'exact archived model locality; no facility identity or field metric claim'}
(out/'source-region-geometry.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print('SOURCE_REGIONS', {k:len(v)for k,v in records.items()})
