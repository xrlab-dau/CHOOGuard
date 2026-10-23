"""Read existing native source mesh faces for the requested construction correction.
No Unity invocation, scene probe, capture or validation stage.
"""
from pathlib import Path
import json,re,math
import numpy as np
from shapely.geometry import Polygon,box
from shapely.ops import unary_union
ROOT=Path('/Users/um-yunsang/CHOOGuard');P=Path(__file__).parent
b=json.loads((P/'MODEL-PLACEMENT-BASIS-R25.json').read_text());base=np.array(b['base_q']);U=np.array(b['u']);V=np.array(b['v'])
s=json.loads((ROOT/'.state/ACTIVE-ALL2F-PHOTO-TWIN-R18/inputs/CURRENT-LIVE-2F-VECTOR-CAD-SOURCE-R19.json').read_text())
o=next(o for o in s['objects'] if o['path'].endswith('OfficialStation_-부산역_선로상층부_0'))
t=(ROOT/o['mesh_asset']).read_text()
count=int(re.search(r'm_VertexCount: (\d+)',t).group(1));size=int(re.search(r'm_DataSize: (\d+)',t).group(1))
data=bytes.fromhex(re.search(r'_typelessdata: ([0-9a-fA-F]+)',t).group(1));stride=size//count
verts=np.ndarray((count,3),dtype='<f4',buffer=data,strides=(stride,4)).astype(float)
idx=np.frombuffer(bytes.fromhex(re.search(r'm_IndexBuffer: ([0-9a-fA-F]+)',t).group(1)),dtype='<u4').reshape(-1,3)
matrix=np.array(o['world_matrix']).reshape(4,4,order='F');world=np.c_[verts,np.ones(count)]@matrix.T
q=np.c_[-.34290633335*world[:,0]-.93936960061*world[:,2],.93936960061*world[:,0]-.34290633335*world[:,2]]
uv=np.c_[(q-base)@U,(q-base)@V,world[:,1]]
sub=[]
for match in re.finditer(r'    firstByte: (\d+)\n    indexCount: (\d+)',t):sub.append((int(match[1])//12,int(match[2])//3))
if not sub:
 for match in re.finditer(r'  - firstByte: (\d+).*?indexCount: (\d+)',t,re.S):sub.append((int(match[1])//12,int(match[2])//3))
out=[];polys=[]
for i,tri in enumerate(idx):
 pts=uv[tri]
 if pts[:,2].max()<7.01 or pts[:,2].min()>11.1 or pts[:,1].max()<-4.5 or pts[:,1].min()>2.05:continue
 if pts[:,0].max()<-.10 or pts[:,0].min()>.10:continue
 n=np.cross(pts[1]-pts[0],pts[2]-pts[0]);L=np.linalg.norm(n)
 if L<1e-9 or abs(n[0])/L<.95:continue
 # Exact near-facade vertical triangles, rather than the object's platform-ceiling projection.
 if max(abs(pts[:,0]))>.12:continue
 poly=Polygon(pts[:,[1,2]])
 if poly.area<.00001:continue
 si=next((j for j,(start,n) in enumerate(sub) if start<=i<start+n),None)
 out.append({'triangle':i,'submesh':si,'u_range':pts[:,0].tolist(),'v_y_polygon':pts[:,[1,2]].tolist()})
 polys.append(poly)
union=unary_union(polys)
opening=box(-4.35,7.0,1.94,9.5).difference(union.buffer(.003))
def geo(g):return g.__geo_interface__
result={'status':'ACTUAL_NATIVE_VERTICAL_FACE_SOURCE_DERIVATION_FOR_CONSTRUCTION','native_object_id':o['object_id'],'native_path':o['path'],'native_mesh_asset':o['mesh_asset'],
 'native_source':'R19 immutable actual mesh/worldmatrix','base_q':base.tolist(),'u':U.tolist(),'v':V.tolist(),'vertical_source_triangles':out,
 'vertical_face_union_v_y':geo(union),'uncovered_v_y_to_9_5':geo(opening),
 'wall_u_min_max':[min(min(x['u_range']) for x in out),max(max(x['u_range']) for x in out)],
 'note':'Native mesh triangles read offline to author direct requested placement; roof/ceiling allheight projection excluded. Openings cannot be closed by generated parts.'}
(P/'NATIVE-VERTICAL-FACE-CONTACT-SOURCE-002.json').write_text(json.dumps(result,ensure_ascii=False,indent=2))
print('stride',stride,'triangles',len(out),'submeshes',len(sub),'planeu',result['wall_u_min_max'])
for y in [7.15,7.5,8.0,8.4,8.8,9.0,9.4]:
 from shapely.geometry import LineString
 solid=union.intersection(LineString([(-4.35,y),(1.94,y)]));gap=LineString([(-4.35,y),(1.94,y)]).difference(union.buffer(.003))
 print('y',y,'solid',solid.wkt,'gaps',gap.wkt)
print('materials',[o['material_assets'][j] for j in sorted({x['submesh'] for x in out if x['submesh'] is not None})])
