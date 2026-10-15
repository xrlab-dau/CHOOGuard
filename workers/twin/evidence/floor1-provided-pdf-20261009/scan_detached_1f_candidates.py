"""One bounded read-only original lower-floor section/floor scan.
No Blender saves, source edits, Unity or current scene calls.
"""
import bpy,json,numpy as np
from pathlib import Path
ROOT=Path('/Users/um-yunsang/CHOOGuard');OUT=ROOT/'workers/twin/output/floor1-provided-pdf-20261009'
meta=[json.loads(x)for x in (ROOT/'asset-library/research-public/2026-10-08/measured-twin-parts/extraction/objects.jsonl').read_text().splitlines()]
byname={x['blender_name']:x for x in meta};byid={x['source_id']:x for x in meta}
reg=json.loads((OUT/'native-original-1f-scan.json').read_text());R=np.array(reg['source_frame_rotation']);T=np.array(reg['source_frame_translation']);F=np.array(reg['station_frame_world_rotation'])
rows=[];sections=[];owners=[];floortri=[];floorowners=[]
for obj in bpy.data.objects:
 if obj.type!='MESH':continue
 m=byname.get(obj.name)
 if not m or not m['source_path'].startswith(('ROOT/g55/','ROOT/g56/','ROOT/g58/','ROOT/g62/','ROOT/g105/')):continue
 raw=np.array([list(x.co)for x in obj.data.vertices]);M=np.array(obj.matrix_world);v=raw@M[:3,:3].T+M[:3,3];q=(v@R.T+T)@F;q=q[:,[2,0,1]];lo=q.min(0);hi=q.max(0)
 if hi[0]<-120 or lo[0]>120 or hi[1]<-125 or lo[1]>120 or hi[2]<-.6 or lo[2]>4.5:continue
 ancestry=[];a=m
 while a:
  ancestry.append({k:a.get(k)for k in ['source_id','source_path','source_name','definition_name','source_layer','effective_hidden']});a=byid.get(a['parent_id'])
 row={'source_id':m['source_id'],'source_path':m['source_path'],'mesh_id':m['mesh_id'],'source_world_matrix':M.tolist(),'native_UVY_bounds':[lo.tolist(),hi.tolist()],'materials':[x.name if x else ''for x in obj.data.materials],'effective_hidden':m['effective_hidden'],'ancestry':ancestry,'vertex_count':len(q),'section_segments_UV':[],'section_source_faces':[],'floor_triangle_count_at_Y0':0}
 obj.data.calc_loop_triangles();floorfaces=[];floorfaceids=[]
 for face in obj.data.loop_triangles:
  tri=q[list(face.vertices)]
  cross=np.cross(tri[1]-tri[0],tri[2]-tri[0]);area=abs(cross[2])*.5
  if area>.005 and np.max(np.abs(tri[:,2]))<=.15 and abs(cross[2])/max(np.linalg.norm(cross),1e-12)>.999:
   floorfaces.append(tri.tolist());floorfaceids.append({'source_polygon_index':face.polygon_index,'triangle_vertex_indices':list(face.vertices)})
  dy=tri[:,2]-1.5
  if np.min(dy)>=0 or np.max(dy)<=0:continue
  pts=[]
  for j in range(3):
   a,b=tri[j],tri[(j+1)%3];da,db=dy[j],dy[(j+1)%3]
   if da*db<0:pts.append(a[:2]+(b[:2]-a[:2])*(-da/(db-da)))
  if len(pts)==2 and np.linalg.norm(pts[0]-pts[1])>.00001:
   row['section_segments_UV'].append([p.tolist()for p in pts]);row['section_source_faces'].append({'source_polygon_index':face.polygon_index,'triangle_vertex_indices':list(face.vertices)})
 row['floor_triangle_count_at_Y0']=len(floorfaces)
 if row['section_segments_UV']:
  start=len(sections);sections.extend(row['section_segments_UV']);owners.append({'row':len(rows),'source_id':m['source_id'],'start':start,'end':len(sections)})
 if floorfaces:
  start=len(floortri);floortri.extend(floorfaces);floorowners.append({'row':len(rows),'source_id':m['source_id'],'start':start,'end':len(floortri),'source_faces':floorfaceids})
 rows.append(row)
np.save(OUT/'detached-candidate-original-section-Y1p5.npy',np.array(sections));np.save(OUT/'detached-candidate-original-floor-Y0.npy',np.array(floortri))
packet={'source_blend':reg['source_blend'],'source_blend_sha256':reg['source_blend_sha256'],'source_frame_rotation':R.tolist(),'source_frame_translation':T.tolist(),'station_frame_world_rotation':F.tolist(),'roi_not_dimensions':[-120,-125,120,120,-.6,4.5],'section_y':1.5,'floor_surface_y_range_reference_only':[-.15,.15],'rows':rows,'section_owners':owners,'floor_owners':floorowners,'production_modified':False,'site_accuracy_verified':False}
(OUT/'detached-candidate-original-scan.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'source_objects':len(rows),'section_objects':len(owners),'floor_objects':len(floorowners),'section_segments':len(sections),'floor_triangles':len(floortri)}))
