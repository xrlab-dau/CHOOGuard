from pathlib import Path
import json,re,gzip,struct,hashlib
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path('/Users/um-yunsang/CHOOGuard');B=Path(__file__).resolve().parent
obj=json.loads((B/'O36-HEAD-SOURCE-OBJECT.json').read_text()); heads=json.loads((ROOT/'.state/CURRENT-FOUR-NATIVE-ROOF-HEAD-CONNECTIONS-C113.json').read_text())['heads'];S=json.loads((ROOT/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-007/CURRENT-2F-MESH-SNAPSHOT.json').read_text())
asset=(ROOT/obj['source_mesh_asset']).read_text(); hexdata=re.search(r'_typelessdata:\s*([0-9a-fA-F]+)',asset).group(1);raw=np.frombuffer(bytes.fromhex(hexdata),'<f4').reshape(-1,12);uv=raw[:,10:12];v=raw[:,:3].astype(float);M=np.array(obj['world_matrix_column_major']).reshape(4,4,order='F');world=v@M[:3,:3].T+M[:3,3];Q=np.array([[-.34290633335,-.93936960061],[-.93936960061,.34290633335]]);q=world[:,[0,2]]@Q.T
with gzip.open(ROOT/S['mesh_binary'],'rb') as f:
 while x:=f.read(4):
  ix=struct.unpack('<i',x)[0];nv=struct.unpack('<i',f.read(4))[0];f.read(nv*12);ns=struct.unpack('<i',f.read(4))[0];tt=[]
  for j in range(ns):
   top,ni=struct.unpack('<ii',f.read(8));ib=f.read(ni*4)
   if ix==obj['mesh_index']:tt.append(np.frombuffer(ib,'<i4').reshape(-1,3).copy() if top==0 else None)
  if ix==obj['mesh_index']:break
rep=[];can=Image.new('RGB',(1800,1400),'#eeeeee');draw=ImageDraw.Draw(can);colors={0:'#aaaab8',5:'#121212',6:'#686878',10:'#e2e2dc',64:'#9dadb2',69:'#1f1f20',70:'#98a3a6'}
for hindex,h in enumerate(heads):
 axis=np.array(h['target_axis_world_xz'])@Q.T; subreports=[];allv=[];alltri=[]
 for sel in h['exact_source_triangle_selections']:
  sub=sel['submesh'];tri=tt[sub][sel['original_triangle_ordinals']];vt=world[tri];uvt=uv[tri];alltri.extend([(sub,t)for t in tri]);allv.extend(tri.flatten());
  uniq=np.unique(tri);centered=np.column_stack((q[uniq]-axis,world[uniq,1]));
  uvsort=np.round(uvt,5).reshape(-1,6);uvsort=uvsort[np.lexsort(uvsort.T[::-1])];
  subreports.append({'submesh':sub,'material':obj['materials'][sub],'triangles':len(tri),'uv_min':uvt.reshape(-1,2).min(0).tolist(),'uv_max':uvt.reshape(-1,2).max(0).tolist(),'uv_triangles_sorted_round5_sha':hashlib.sha256(uvsort.tobytes()).hexdigest(),'centered_qx_qy_y_bounds':[centered.min(0).tolist(),centered.max(0).tolist()]})
 vv=world[np.unique(allv)];print(h['head_id'],'worldbounds',vv.min(0).tolist(),vv.max(0).tolist());rep.append({'head':h['head_id'],'axis_q':axis.tolist(),'submeshes':subreports})
 for side in range(2):
  col=hindex%2;row=hindex//2;xoff=col*900; yoff=row*700+side*320
  pitch=.35;ang=side*np.pi/2;du=np.array([np.cos(ang),np.sin(ang)]);dv=np.array([-np.sin(ang),np.cos(ang)]);z=world[:,1];dx=(q-axis)@du;dy=(q-axis)@dv;px=dx*48+xoff+440;py=(27-z)*90+yoff+60+dy*pitch*48
  poly=[]
  for sub,t in alltri:poly.append((float(dy[t].mean()),sub,list(zip(px[t],py[t]))))
  for depth,sub,pts in sorted(poly,reverse=True):draw.polygon(pts,fill=colors.get(sub,'#cc8899'),outline='#444444')
  draw.text((xoff+20,yoff+10),h['head_id']+' side '+str(side),fill='black')
can.save(B/'FOUR-NATIVE-HEAD-SOURCE-PANEL-GEOMETRY.png');(B/'FOUR-NATIVE-HEAD-UV-MATERIAL-IDENTITY.json').write_text(json.dumps(rep,ensure_ascii=False,indent=2))
print('uv per slot',[(s,[h['submeshes'][j]['uv_triangles_sorted_round5_sha'][:10] for h in rep]) for j,s in enumerate([0,5,6,10,64,69,70])])
