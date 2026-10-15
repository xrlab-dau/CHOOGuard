import json, numpy as np
from pathlib import Path
rows=json.load(open('workers/twin/evidence/floor2-whole-layout-20261009/newphoto-scope/source-gallery-geometry.json'))['regions']['namcheon-gallery']
tri=[];ids=[]
for r in rows:
 if 'native_vertices' not in r:continue
 q=np.array(r['native_vertices']);f=np.array(r['source_polygons']);t=q[f]
 for j,z in enumerate(t):
  if z[:,2].ptp() if False else False:pass
  if np.ptp(z[:,2])<.001:
   tri.append(z);ids.append((r['source_id'],r['source_path'],j))
tri=np.array(tri)
def supports(p):
 out=[]
 for t,id in zip(tri,ids):
  A=np.column_stack((t[1,:2]-t[0,:2],t[2,:2]-t[0,:2]));det=np.linalg.det(A)
  if abs(det)<1e-8:continue
  a=np.linalg.solve(A,p-t[0,:2])
  if a.min()>-1e-7 and a.sum()<1+1e-7:out.append({'Y':float(t[:,2].mean()),'source_id':id[0],'source_path':id[1],'source_face':id[2]})
 return sorted(out,key=lambda z:z['Y'])
a=.07227666563340311;b=14.598612646986819
checks=[]
for u in [-39.95,-38.5,-36.5,-34.5,-33.05]:
 for depth in [-.5,.05,1,3.5,6.95]:
  p=np.array([u,a*u+b+depth]);checks.append({'native_UV':p.tolist(),'native_wall_depth':depth,'supports':supports(p)})
Path('workers/twin/evidence/floor2-whole-layout-20261009/newphoto-scope/namcheon-candidate-source-floor-ceiling.json').write_text(json.dumps({'frontline_V_from_U':[a,b],'candidate_depth_not_field_measurement':7,'samples':checks},indent=2)+'\n')
for c in checks:print(np.round(c['native_UV'],3).tolist(),[(round(z['Y'],3),z['source_path'],z['source_face'])for z in c['supports']if 6.9<z['Y']<14])
