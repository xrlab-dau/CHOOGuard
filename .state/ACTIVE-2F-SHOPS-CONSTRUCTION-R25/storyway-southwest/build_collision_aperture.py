from pathlib import Path
import yaml,numpy as np,json,hashlib
p=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/storyway-southwest');path=Path('Assets/ChooGuard/Art/StationInterior/WallColumnTopR13_20261019/R13_WALL_032.asset');d=yaml.safe_load('\n'.join(path.read_text().splitlines()[3:]))['Mesh'];v=d['m_VertexData'];ar=np.frombuffer(bytes.fromhex(v['_typelessdata']),dtype='<f4').reshape(v['m_VertexCount'],-1);verts=[np.array(x,dtype=float) for x in ar[:,:3]];initial=len(verts);ic=4 if d['m_IndexFormat']==1 else 2;ix=np.frombuffer(bytes.fromhex(d['m_IndexBuffer']),dtype='<u4' if ic==4 else '<u2');subs=[];changed=0
L,R,B,T=-48.26294,-46.96796,7.10,9.45
qx=lambda p:-.34290633335*p[0]-.93936960061*p[2]
qy=lambda p:.93936960061*p[0]-.34290633335*p[2]
planes=[lambda p:qx(p)-L,lambda p:R-qx(p),lambda p:p[1]-B,lambda p:T-p[1]]
def clip(poly,fn,positive):
 if not poly:return []
 out=[];prev=poly[-1];dp=fn(prev)*(1 if positive else -1)
 for cur in poly:
  dc=fn(cur)*(1 if positive else -1)
  if (dc>=-1e-10)!=(dp>=-1e-10):out.append(prev+(cur-prev)*(dp/(dp-dc)))
  if dc>=-1e-10:out.append(cur)
  prev=cur;dp=dc
 return out
def emit(poly,out):
 if len(poly)<3:return
 for k in range(1,len(poly)-1):
  tri=[poly[0],poly[k],poly[k+1]]
  if np.linalg.norm(np.cross(tri[1]-tri[0],tri[2]-tri[0]))<1e-10:continue
  n=len(verts);verts.extend(tri);out.extend([n,n+1,n+2])
for sm in d['m_SubMeshes']:
 ss=ix[sm['firstByte']//ic:sm['firstByte']//ic+sm['indexCount']];out=[]
 for ids in ss.reshape(-1,3):
  tri=[verts[i] for i in ids];xs=[qx(v) for v in tri];ys=[v[1] for v in tri];zs=[qy(v) for v in tri]
  if min(zs)<-24.810 or max(zs)>-24.600 or min(xs)>=R or max(xs)<=L or min(ys)>=T or max(ys)<=B:out.extend(int(i) for i in ids);continue
  inside=tri
  for fn in planes:
   emit(clip(inside,fn,False),out);inside=clip(inside,fn,True)
   if not inside:break
  changed+=1
 subs.append(out)
def qw(x,y,z):return np.array([-.34290633335*x+.93936960061*z,y,-.93936960061*x-.34290633335*z])
# New aperture reveals only; they lie on exact removed frontwall boundaries and do not alter outer faces.
z0,z1=-24.795073,-24.615072
for quad in [[qw(L,B,z0),qw(L,T,z0),qw(L,T,z1),qw(L,B,z1)],[qw(R,B,z1),qw(R,T,z1),qw(R,T,z0),qw(R,B,z0)],[qw(L,T,z1),qw(R,T,z1),qw(R,T,z0),qw(L,T,z0)]]:emit(quad,subs[0])
result={'source_mesh':str(path),'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'original_vertex_count':initial,'appended_vertices':[v.tolist() for v in verts[initial:]],'submesh_triangles':subs,'changed_front_triangles':changed,'method':'Exact source triangle subtraction in own generated front wall collision only; original vertices/untouched triangles reused, independent own derivative. Frontglass visual and separate collision both have source-visible opening.','aperture_qx_worldY':[L,B,R,T],'front_wall_qy_cut_band':[-24.810,-24.600],'original_mesh_unchanged':True}
(p/'GENERATED-COLLIDER-ENTRANCE-DERIVATIVE-R25.json').write_text(json.dumps(result,separators=(',',':')))
print({'changed_source_triangles':changed,'appended_vertices':len(verts)-initial,'source_triangles':len(ix)//3,'derivative_triangles':sum(len(s)//3 for s in subs)})
