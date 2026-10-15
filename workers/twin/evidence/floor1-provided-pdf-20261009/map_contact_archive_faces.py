"""Read-only current triangle to exact original archive polygon/vertices. No saves."""
import bpy,json,numpy as np
from pathlib import Path
ROOT=Path('/Users/um-yunsang/CHOOGuard');OUT=ROOT/'workers/twin/output/floor1-provided-pdf-20261009'
m=[json.loads(x)for x in (ROOT/'asset-library/research-public/2026-10-08/measured-twin-parts/extraction/objects.jsonl').read_text().splitlines()];byid={x['source_id']:x for x in m};selection=json.loads((OUT/'original-contact-source-object-selection.json').read_text());c=json.loads((OUT/'original-source-contact-candidates.json').read_text());reg=json.loads((OUT/'native-original-1f-scan.json').read_text());R=np.array(reg['source_frame_rotation']);T=np.array(reg['source_frame_translation']);F=np.array(reg['station_frame_world_rotation']);targets={k:v['candidate_triangles']for k,v in c['owners'].items()};matches={k:{str(x['current_triangle_index']):[]for x in v}for k,v in targets.items()};objects=[x for x in m if x['source_id']in selection['source_ids']or('유라시아 도로' in x['source_layer']and x['kind']=='mesh_instance')];tested=0
for row in objects:
 o=bpy.data.objects[row['blender_name']]
 if o.type!='MESH':continue
 kind='shell'if row['source_id']in selection['source_ids']else'road';a=np.empty(len(o.data.vertices)*3);o.data.vertices.foreach_get('co',a);a=a.reshape(-1,3);M=np.array(o.matrix_world);world=(a@M[:3,:3].T+M[:3,3])@R.T+T;lo=world.min(0);hi=world.max(0);tested+=1
 for t in targets[kind]:
  z=np.array(t['world_vertices']);zl=z.min(0);zh=z.max(0)
  if not((lo<=zh+.001).all()and(hi>=zl-.001).all()):continue
  distance=np.linalg.norm(world[:,None,:]-z[None,:,:],axis=2);vids=distance.argmin(0);error=distance[vids,np.arange(3)]
  if error.max()>.001:continue
  facehits=[p for p in o.data.polygons if set(vids).issubset(set(p.vertices))]
  for p in facehits:
   vv=world[list(p.vertices)];uvy=(vv@F)[:,[2,0,1]];ancestry=[];a=row
   while a:ancestry.append({k:a.get(k)for k in ['source_id','source_path','source_layer','source_name','kind']});a=byid.get(a['parent_id'])
   matches[kind][str(t['current_triangle_index'])].append({'source_id':row['source_id'],'source_path':row['source_path'],'mesh_id':row['mesh_id'],'source_polygon_index':p.index,'source_polygon_vertex_indices':list(p.vertices),'matching_original_vertex_indices':vids.tolist(),'current_vertex_to_original_vertex_errors':error.tolist(),'max_current_archive_vertex_error':float(error.max()),'original_object_matrix_world':M.tolist(),'source_polygon_world_vertices':vv.tolist(),'source_polygon_native_UVY_vertices':uvy.tolist(),'material_index':p.material_index,'materials':[x.name if x else''for x in o.data.materials],'ancestry':ancestry,'effective_hidden':row['effective_hidden']})
(OUT/'original-contact-archive-face-identities.json').write_text(json.dumps({'schema':'chooguard.original-contact-archive-face-identities.v1','source_blend':reg['source_blend'],'source_blend_sha256':reg['source_blend_sha256'],'candidate_packet':'workers/twin/output/floor1-provided-pdf-20261009/original-source-contact-candidates.json','source_frame_rotation':R.tolist(),'source_frame_translation':T.tolist(),'station_frame_world_rotation':F.tolist(),'objects_tested':tested,'matching_method':'All three current world vertices matched original archive polygon vertices within0.001 model units, original polygon contains those three vertices. This numerical source-identity tolerance is not site accuracy.','matches':matches,'production_mutated':False},ensure_ascii=False,indent=2)+'\n');print(json.dumps({'objects_tested':tested,'match_counts':{k:{i:len(v)for i,v in x.items()}for k,x in matches.items()}}))
