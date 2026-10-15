"""Read only: supplement bounded lower sections with Y4.15 cuts and all horizontal source surfaces Y-0.25..4.5."""
import bpy,json,numpy as np
from pathlib import Path
ROOT=Path('/Users/um-yunsang/CHOOGuard');OUT=ROOT/'workers/twin/output/floor1-provided-pdf-20261009'
j=json.loads((OUT/'detached-candidate-original-scan.json').read_text());m=[json.loads(x)for x in (ROOT/'asset-library/research-public/2026-10-08/measured-twin-parts/extraction/objects.jsonl').read_text().splitlines()];names={x['source_id']:x['blender_name']for x in m};R=np.array(j['source_frame_rotation']);T=np.array(j['source_frame_translation']);F=np.array(j['station_frame_world_rotation']);rows=[];surfaces=[];section=[]
for row in j['rows']:
 b=np.array(row['native_UVY_bounds'])
 if b[1,0]<-50 or b[0,0]>90 or b[1,1]<8 or b[0,1]>105:continue
 o=bpy.data.objects[names[row['source_id']]];a=np.empty(len(o.data.vertices)*3);o.data.vertices.foreach_get('co',a);a=a.reshape(-1,3);M=np.array(o.matrix_world);q=((a@M[:3,:3].T+M[:3,3])@R.T+T)@F;q=q[:,[2,0,1]];o.data.calc_loop_triangles();ids=np.empty(len(o.data.loop_triangles)*3,dtype=np.int32);o.data.loop_triangles.foreach_get('vertices',ids);ids=ids.reshape(-1,3);polys=np.empty(len(ids),dtype=np.int32);o.data.loop_triangles.foreach_get('polygon_index',polys);tri=q[ids];cr=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]);area=np.abs(cr[:,2])*.5
 pick=np.where((area>.005)&(np.abs(cr[:,2])/np.maximum(np.linalg.norm(cr,axis=1),1e-12)>.999)&(tri[:,:,2].min(1)>=-.25)&(tri[:,:,2].max(1)<=4.5))[0];ss=len(surfaces);surfaces.extend(tri[pick].tolist());faces=[{'source_polygon_index':int(polys[k]),'triangle_vertex_indices':ids[k].tolist()}for k in pick];start=len(section);secfaces=[]
 for k in np.where((tri[:,:,2].min(1)<4.15)&(tri[:,:,2].max(1)>4.15))[0]:
  z=tri[k];dy=z[:,2]-4.15;pts=[]
  for h in range(3):
   a,b=z[h],z[(h+1)%3];da,db=dy[h],dy[(h+1)%3]
   if da*db<0:pts.append(a[:2]+(b[:2]-a[:2])*(-da/(db-da)))
  if len(pts)==2 and np.linalg.norm(pts[0]-pts[1])>.00001:section.append([x.tolist()for x in pts]);secfaces.append({'source_polygon_index':int(polys[k]),'triangle_vertex_indices':ids[k].tolist()})
 if len(surfaces)>ss or len(section)>start:rows.append({k:row[k]for k in ['source_id','source_path','mesh_id','source_world_matrix','native_UVY_bounds','materials','ancestry','effective_hidden']}|{'horizontal_triangles_range':[ss,len(surfaces)],'horizontal_source_faces':faces,'section_segments_range':[start,len(section)],'section_source_faces':secfaces})
np.save(OUT/'matching-original-horizontal-surfaces-Yminus0p25-to4p5.npy',np.array(surfaces));np.save(OUT/'matching-original-section-Y4p15.npy',np.array(section));(OUT/'matching-original-lower-surface-scan.json').write_text(json.dumps({'source_blend':j['source_blend'],'source_blend_sha256':j['source_blend_sha256'],'source_frame_rotation':R.tolist(),'source_frame_translation':T.tolist(),'station_frame_world_rotation':F.tolist(),'roi_not_dimensions':[-50,8,90,105],'section_y':4.15,'horizontal_surface_y_range':[-.25,4.5],'normal_tolerance':.999,'not_current_collider_evidence':True,'rows':rows},ensure_ascii=False,indent=2)+'\n');print(json.dumps({'owners':len(rows),'horizontal_triangles':len(surfaces),'section_segments':len(section)}))
