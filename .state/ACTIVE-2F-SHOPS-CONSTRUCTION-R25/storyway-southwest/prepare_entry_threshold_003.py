from pathlib import Path
import json,yaml,numpy as np,hashlib
p=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/storyway-southwest');L,R,B,T=-48.26294,-46.96796,7.0,7.12001
qx=lambda v:-.34290633335*v[0]-.93936960061*v[2]
qy=lambda v:.93936960061*v[0]-.34290633335*v[2]
planes=[lambda v:qx(v)-L,lambda v:R-qx(v),lambda v:v[1]-B,lambda v:T-v[1]]
def clip(poly,fn,positive):
 if not poly:return []
 out=[];prev=poly[-1];dp=fn(prev)*(1 if positive else -1)
 for cur in poly:
  dc=fn(cur)*(1 if positive else -1)
  if (dc>=-1e-10)!=(dp>=-1e-10):out.append(prev+(cur-prev)*(dp/(dp-dc)))
  if dc>=-1e-10:out.append(cur)
  prev=cur;dp=dc
 return out
rows={}
for key,name in [('render','MESH_OWN_GENERATED_FRONTGLASS_OPEN_ENTRY.asset'),('collider','MESH_OWN_GENERATED_COLLISION_OPEN_ENTRY.asset')]:
 path=Path('Assets/ChooGuard/Art/StationInterior/R25_Shops/storyway-southwest')/name;d=yaml.safe_load('\n'.join(path.read_text().splitlines()[3:]))['Mesh'];v=d['m_VertexData'];ar=np.frombuffer(bytes.fromhex(v['_typelessdata']),dtype='<f4').reshape(v['m_VertexCount'],-1);assert ar.shape[1]==8;verts=[np.array(x,dtype=float) for x in ar];initial=len(verts);ic=4 if d['m_IndexFormat']==1 else 2;ix=np.frombuffer(bytes.fromhex(d['m_IndexBuffer']),dtype='<u4' if ic==4 else '<u2');subs=[];changed=0;removed_ys=[]
 def emit(poly,out):
  if len(poly)<3:return
  for k in range(1,len(poly)-1):
   tri=[poly[0],poly[k],poly[k+1]]
   if np.linalg.norm(np.cross(tri[1][:3]-tri[0][:3],tri[2][:3]-tri[0][:3]))<1e-10:continue
   n=len(verts);verts.extend(tri);out.extend([n,n+1,n+2])
 for sm in d['m_SubMeshes']:
  ss=ix[sm['firstByte']//ic:sm['firstByte']//ic+sm['indexCount']];out=[]
  for ids in ss.reshape(-1,3):
   tri=[verts[i] for i in ids];xs=[qx(v) for v in tri];ys=[v[1] for v in tri];zs=[qy(v) for v in tri]
   # The narrow sourceface band includes current glass/frame foot only. Mullion boundary faces have2e-5m tolerance.
   if min(zs)<-24.811 or max(zs)>-24.570 or min(xs)>=R-1e-4 or max(xs)<=L+1e-4 or min(ys)>=T or max(ys)<B-2e-6:out.extend(int(i) for i in ids);continue
   inside=tri
   for fn in planes:
    emit(clip(inside,fn,False),out);inside=clip(inside,fn,True)
    if not inside:break
   if inside:
    removed_ys.extend(float(v[1]) for v in inside)
   changed+=1
  subs.append(out)
 # Source lower rail has top7.12 due20mm overlap; this belongs to lower remnant, not header/mullions.
 rows[key]={'source_mesh':str(path),'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'original_vertex_count':initial,'submesh_count':len(subs),'appended_vertices':[[float(v) for v in x[:3]] for x in verts[initial:]],'appended_normals':[[float(v) for v in x[3:6]] for x in verts[initial:]],'appended_uv':[[float(v) for v in x[6:8]] for x in verts[initial:]],'submesh_triangles':subs,'source_intersecting_triangles':changed,'removed_source_worldY_bounds':[min(removed_ys),max(removed_ys)] if removed_ys else None,'original_source_triangles':len(ix)//3,'result_triangles':sum(len(s)//3 for s in subs),'output_mesh':f'Assets/ChooGuard/Art/StationInterior/R25_Shops/storyway-southwest/MESH_OWN_ENTRY_TO_FLOOR_003_{key.upper()}.asset'}
print({k:{z:v for z,v in d.items() if z in ['source_intersecting_triangles','removed_source_worldY_bounds','original_source_triangles','result_triangles']} for k,d in rows.items()})
packet={'authority':'OWN-GENERATED-ENTRY-TO-FLOOR-AUTHORITY-003.json','object_id':'GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-718422858-0','source_photo':'sources/ROOT-kmk78800-224405723594-26.jpg','source_photo_directly_viewed':True,'source_epoch':'2026-09-09','existing_aperture_qx':[L,R],'existing_header_worldY':9.45,'lower_remnant_cut_top_worldY':T,'new_lower_aperture_worldY':B,'old_lower_aperture_worldY':7.1,'front_band_qy':[-24.811,-24.570],'method':'Subtract lower remnant in already opened current independent entry; all source outside-aperture triangles/vertices and existing mullion/header boundary faces reused. Currentrenderandcollider sourced separately. Floor/native/frames/TRS/materials/source originals fixed.','source_metric_status':'Actual current CAD/jamb constraints and source observed continuous entry floor; exact fit MODEL, not surveyed','derivatives':rows}
p.joinpath('ENTRY-THRESHOLD-SOURCE-PACKET-003.json').write_text(json.dumps(packet,separators=(',',':')))
sha=hashlib.sha256(p.joinpath('ENTRY-THRESHOLD-SOURCE-PACKET-003.json').read_bytes()).hexdigest();p.joinpath('ENTRY-THRESHOLD-PACKET-SHA-003.txt').write_text(sha+'\n');print('packetsha',sha)
p.joinpath('findings.md').open('a').write('\n003sourceauthoring: existingrenderlowerremnant includesoriginalbottomrailoverlap toworldY7.12, while mainbase tops7.10. Remove this lowerentry-span remnant by subtractingalreadyopenaperture down to7.0; native/currentmullion boundary faces explicitly retained with0.1mm numeric tolerance. No sourceaboveexistingapertureheader9.45 touched. Bothcurrent002render/collider source hashes frozen independently.\n')
