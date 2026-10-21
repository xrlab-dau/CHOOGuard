"""Production source geometry inventory, offline; no Unity/probe/QA access."""
from pathlib import Path
import json,gzip,struct,collections
import numpy as np
from shapely.geometry import Polygon,box,mapping
from shapely.ops import unary_union
ROOT=Path('/Users/um-yunsang/CHOOGuard');B=Path(__file__).resolve().parent
S=ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-007'
source=json.loads((S/'CURRENT-2F-MESH-SNAPSHOT.json').read_text())
rows=json.loads((B/'CANDIDATE-HEIGHT-CROSSING-OBJECTS.json').read_text())
by=collections.defaultdict(list)
for o in rows:by[o['mesh_index']].append(o)
Q=np.array([[-.34290633335,-.93936960061],[-.93936960061,.34290633335]])
roi=box(-57,-82,-8,-35);central=box(-44.5,-71.7,-20.7,-51)
report=[];native_up_triangles=0;central_rows=[]
def clip_y(poly,level,above):
    out=[]
    for i,a in enumerate(poly):
        b=poly[(i+1)%len(poly)];aa=a[1]>=level if above else a[1]<=level;bb=b[1]>=level if above else b[1]<=level
        if aa:out.append(a)
        if aa!=bb:out.append(a+(b-a)*(level-a[1])/(b[1]-a[1]))
    return out
with gzip.open(ROOT/source['mesh_binary'],'rb')as f:
    while raw:=f.read(4):
        ix=struct.unpack('<i',raw)[0];nv=struct.unpack('<i',f.read(4))[0];vb=f.read(nv*12);ns=struct.unpack('<i',f.read(4))[0];tt=[]
        for j in range(ns):
            topo,ni=struct.unpack('<ii',f.read(8));ib=f.read(ni*4)
            if ix in by and topo==0:tt.append(np.frombuffer(ib,'<i4').reshape(-1,3).copy())
        if ix not in by or not tt:continue
        local=np.frombuffer(vb,'<f4').reshape(-1,3).astype(float);tri=np.concatenate(tt)
        for o in by[ix]:
            M=np.array(o['world_matrix_column_major']).reshape(4,4,order='F');v=local@M[:3,:3].T+M[:3,3];a=v[tri]
            normals=np.cross(a[:,1]-a[:,0],a[:,2]-a[:,0]);area2=np.linalg.norm(normals,axis=1)
            good=(a[:,:,1].max(1)>=7.23)&(a[:,:,1].min(1)<=7.78)&(np.abs(normals[:,1])>area2*.50)&(area2>1e-10)
            polygons=[]
            for t in a[good]:
                pp=clip_y(list(t),7.23,True)
                if len(pp)<3:continue
                pp=clip_y(pp,7.78,False)
                if len(pp)<3:continue
                q=np.array(pp)[:,[0,2]]@Q.T;p=Polygon(q).buffer(0)
                if not p.is_empty and p.intersects(roi):
                    p=p.intersection(roi)
                    if p.area>1e-8:polygons.append(p)
            if not polygons:continue
            p=unary_union(polygons);touch=p.intersection(central)
            r={'object_id':o['object_id'],'path':o['path'],'source_kind':o['source_kind'],'mesh_asset':o['source_mesh_asset'],'horizontal_or_sloped_seat_height_faces':len(polygons),'world_y_band':[7.23,7.78],'q_bounds':list(p.bounds),'projected_face_area_m2':p.area,'actual_projected_faces':mapping(p),'central_cell_projected_area_m2':touch.area}
            report.append(r)
            if o['source_kind']=='OFFICIAL_REFERENCE':native_up_triangles+=len(polygons)
            if touch.area>1e-6:central_rows.append({k:v for k,v in r.items()if k!='actual_projected_faces'})
result={'purpose':'Actual geometry construction-source inventory for identifying existing low furniture before proposing public benches; no Scene or Assets write and no validation verdict','source_snapshot':str(S.relative_to(ROOT)),'all_candidate_objects':len(rows),'unique_source_meshes':len(by),'all_actual_height_intersecting_objects_considered':True,'wholly_contained_triangle_filter_not_used':True,'ROI_actual_q':[-57,-82,-8,-35],'horizontal_surface_height_band_world_y':[7.23,7.78],'surface_classification':'abs(normal.worldY)>0.50, includes upward and underside/sloped surfaces, clipped to both actual Y planes; preserves triangles crossing height limits','objects_with_actual_source_faces':report,'central_four_column_cell_actual_q':[-44.5,-71.7,-20.7,-51],'central_cell_objects_with_actual_source_faces':central_rows,'native_projected_face_triangles':native_up_triangles,'limits':'This identifies actual seat-height geometry; it does not prove global scene/furniture absence. Distinguish wall/floor/stair/counter and store equipment by exact source geometry/provenance. Source snapshot007 is not live native QA.'}
(B/'ALL-ACTUAL-SEAT-HEIGHT-SOURCE-GEOMETRY.json').write_text(json.dumps(result,ensure_ascii=False,separators=(',',':')))
print(json.dumps({'allobjects':len(rows),'actual_seat_height_parts':len(report),'native_triangles':native_up_triangles,'central_parts':len(central_rows),'central_source_paths':[x['path']for x in central_rows]},ensure_ascii=False))
