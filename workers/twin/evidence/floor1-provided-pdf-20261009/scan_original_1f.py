import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
import numpy as np

ROOT=Path('/Users/um-yunsang/CHOOGuard')
EV=ROOT/'workers/twin/evidence/floor1-provided-pdf-20261009'
OUT=ROOT/'workers/twin/output/floor1-provided-pdf-20261009'
meta=[json.loads(x)for x in (ROOT/'asset-library/research-public/2026-10-08/measured-twin-parts/extraction/objects.jsonl').read_text().splitlines()]
byname={x['blender_name']:x for x in meta};byid={x['source_id']:x for x in meta}
reg=json.loads((ROOT/'workers/twin/evidence/floor2-whole-layout-20261009/source-region-geometry.json').read_text())
R=np.array(reg['frame_rotation']);T=np.array(reg['frame_translation'])
fr=np.array([[.9602936856769431,0,.2789911060392293],[0,1,0],[-.2789911060392293,0,.9602936856769431]])
def ancestors(m):
 out=[]
 while m:
  out.append({k:m.get(k)for k in ['source_id','source_path','source_name','source_layer','definition_name','effective_hidden']});m=byid.get(m['parent_id'])
 return out
rows=[];segments=[];owners=[]
for obj in bpy.data.objects:
 if obj.type!='MESH':continue
 m=byname.get(obj.name)
 if not m:continue
 if not m['source_path'].startswith('ROOT/g56/'):continue
 v=np.array([list(obj.matrix_world@x.co)for x in obj.data.vertices]);q=(v@R.T+T)@fr;q=q[:,[2,0,1]]
 lo=q.min(axis=0);hi=q.max(axis=0)
 if hi[0]<-100 or lo[0]>85 or hi[1]<-70 or lo[1]>20:continue
 row={'source':m,'ancestors':ancestors(m),'native_UVY_bounds':[lo.tolist(),hi.tolist()],
      'materials':[x.name if x else ''for x in obj.data.materials],'vertex_count':len(v),
      'crosses_Y1_5':bool(lo[2]<=1.5<=hi[2])}
 obj.data.calc_loop_triangles(); seg=[]
 if row['crosses_Y1_5']:
  for face in obj.data.loop_triangles:
   tri=q[list(face.vertices)];dy=tri[:,2]-1.5
   if np.min(dy)>=0 or np.max(dy)<=0:continue
   pts=[]
   for j in range(3):
    a,b=tri[j],tri[(j+1)%3];da,db=dy[j],dy[(j+1)%3]
    if da*db<0:pts.append(a[:2]+(b[:2]-a[:2])*(-da/(db-da)))
   if len(pts)==2 and np.linalg.norm(pts[0]-pts[1])>.00001:seg.append([x.tolist()for x in pts])
 row['section_segment_count']=len(seg);row['section_segments_UV']=seg
 if seg:
  begin=len(segments);segments.extend(seg);owners.append({'row':len(rows),'source_id':m['source_id'],'start':begin,'end':len(segments)})
 rows.append(row)
np.save(OUT/'native-original-1f-section.npy',np.array(segments))
receipt={'source_blend':reg['source_blend'],'source_blend_sha256':reg['source_blend_sha256'],'source_frame_rotation':reg['frame_rotation'],'source_frame_translation':reg['frame_translation'],'station_frame_world_rotation':fr.tolist(),'sectionY':1.5,'included_source_path_prefix':'ROOT/g56/','roi_not_dimensions':[-100,-70,85,20],'rows':rows,'segment_owners':owners,'site_accuracy_verified':False,'procedure':'Read-only source blend mesh cuts; includes hidden status rather than silently excluding it.'}
(OUT/'native-original-1f-scan.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'objects':len(rows),'crossingY':sum(x['crosses_Y1_5']for x in rows),'segments':len(segments)}))
