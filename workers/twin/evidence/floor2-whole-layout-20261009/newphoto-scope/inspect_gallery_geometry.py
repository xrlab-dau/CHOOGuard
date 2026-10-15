from pathlib import Path
import json,struct,hashlib
import numpy as np
from PIL import Image,ImageDraw
E=Path(__file__).resolve().parent
P=Path('.planning/2026-10-09-floor2-completion-pass2/current/current-inventory.json')
S=json.loads(P.read_text());MM={m['id']:m for m in S['meshes']}
F=np.array([[.9602937,0,.278991133],[0,1,0],[-.278991133,0,.9602937]]);I=np.linalg.inv(F)
def read(m):
 b=Path(m['binary']).read_bytes();assert hashlib.sha256(b).hexdigest()==m['binary_sha256'];nv,ni,nn=struct.unpack_from('<iii',b)
 return np.frombuffer(b,'<f4',nv*3,12).reshape(nv,3),np.frombuffer(b,'<i4',ni,12+nv*12).reshape(-1,3)
results=[];plans=[]
for r in S['renderers']:
 if not any(s in r['path'] for s in ['/MainShell/','2F-3F','Shop_남천','Majibang_','Box_Hall','Kit_Ceiling600']):continue
 m=MM[r['mesh']]
 if not m.get('binary'):continue
 v,t=read(m);M=np.array(r['world_matrix']);w=v@M[:3,:3].T+M[:3,3];q=(w@I.T)[:,[2,0,1]];tri=q[t]
 lo=tri.min(1);hi=tri.max(1);c=tri.mean(1);cross=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]);area=np.linalg.norm(cross,axis=1)/2
 horizontal=(hi[:,2]-lo[:,2]<.001)&(area>.01)
 hist={}
 for y in np.unique(np.round(c[horizontal,2],2)):
  x=horizontal&(abs(c[:,2]-y)<.006)
  if area[x].sum()>1:hist[str(y)]={'faces':int(x.sum()),'area':round(float(area[x].sum()),2),'uv_bounds':[lo[x,:2].min(0).round(3).tolist(),hi[x,:2].max(0).round(3).tolist()]}
 near=(np.all(hi[:,:2]>[-65,-25],1)&np.all(lo[:,:2]<[-15,25],1)&(hi[:,2]>6.9)&(lo[:,2]<14))
 results.append({'owner':r,'mesh':m,'native_bounds':[q.min(0).tolist(),q.max(0).tolist()],'horizontal_height_clusters':hist,'near_namcheon_faces':np.flatnonzero(near).tolist()})
 plans.append((r['path'],tri,area,horizontal))
 print(r['path'],len(t),'bounds',q.min(0).round(2),q.max(0).round(2),'horiz',hist)
(E/'gallery-geometry-audit.json').write_text(json.dumps({'inventory_sha256':hashlib.sha256(P.read_bytes()).hexdigest(),'scene_epoch':S['scene_file_sha256'],'native_frame':F.tolist(),'receipts':results},ensure_ascii=False,indent=2)+'\n')
# Exact geometry plan; horizontal source layers/recipe overlays displayed separately.
for yr in [(6.9,7.15),(10.3,10.6),(11.9,12.5),(12.5,14)]:
 im=Image.new('RGB',(1500,1350),'white');d=ImageDraw.Draw(im);xmin,xmax,ymin,ymax=-100,50,-50,100
 def px(q):return((q[0]-xmin)*9+60,(q[1]-ymin)*8+50)
 for path,tr,area,h in plans:
  ix=np.flatnonzero(h&(tr.mean(1)[:,2]>=yr[0])&(tr.mean(1)[:,2]<yr[1]))
  fill=(185,210,220)if'/MainShell/'in path else(255,210,170)
  for x in tr[ix]:d.polygon([px(z)for z in x],fill=fill,outline=(100,130,140))
  if '2F-3F'in path:
   for x in tr[::20]:d.polygon([px(z)for z in x],outline=(210,60,60))
 d.rectangle([px([-40,-3.31]),px([-33,3.69])],outline=(0,100,0),width=4);d.text(px([-40,-5]),'#26 Namcheon recipe',fill=(0,80,0))
 for u in range(-100,51,10):d.line([(px([u,ymin])),px([u,ymax])],fill=(230,230,230));d.text(px([u,ymin]),str(u),fill='black')
 for vv in range(-50,101,10):d.line([px([xmin,vv]),px([xmax,vv])],fill=(230,230,230));d.text(px([xmin,vv]),str(vv),fill='black')
 d.text((10,10),f'Exact current geometry horizontal faces Y {yr}; U horizontal, V down; red current 2F-3F bank; green Namcheon recipe',fill='black')
 im.save(E/f'native-plan-Y{yr[0]}-{yr[1]}.png')
