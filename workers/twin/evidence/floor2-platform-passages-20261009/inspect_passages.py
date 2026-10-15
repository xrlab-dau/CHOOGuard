from pathlib import Path
import json, math, struct
import numpy as np
from PIL import Image,ImageDraw,ImageFont
BASE=Path('workers/twin/evidence/floor2-platform-passages-20261009')
INV=Path('.planning/2026-10-09-floor2-guide-restoration/architecture/current/current-scene-structure.json')
x=json.loads(INV.read_text());meshes={m['id']:m for m in x['meshes']};s=math.sin(math.radians(16.2));c=math.cos(math.radians(16.2))
def binload(m):
 b=Path(m['binary']).read_bytes();nv,ni,nn=struct.unpack_from('<iii',b);o=12;v=np.frombuffer(b,dtype='<f4',count=nv*3,offset=o).reshape(-1,3);o+=nv*12;t=np.frombuffer(b,dtype='<i4',count=ni,offset=o).reshape(-1,3);return v,t
W,H=1600,1300;im=Image.new('RGB',(W,H),'#f6f5ed');d=ImageDraw.Draw(im);font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',14)
lo=np.array([-100,-65]);hi=np.array([145,120]);scale=min((W-80)/(hi-lo)[0],(H-80)/(hi-lo)[1]);
def xy(uv):return (40+(uv[0]-lo[0])*scale, H-40-(uv[1]-lo[1])*scale)
for u in range(-100,146,10):d.line([xy([u,lo[1]]),xy([u,hi[1]])],fill='#dddddd');d.text(xy([u,lo[1]-2]),str(u),font=font,fill='#777777')
for vv in range(-60,121,10):d.line([xy([lo[0],vv]),xy([hi[0],vv])],fill='#dddddd');d.text(xy([lo[0]-4,vv]),str(vv),font=font,fill='#777777')
sections=[];floors=[]
rs=[r for r in x['renderers']if r.get('active')and r.get('renderer_enabled')and ('FPSWorld/공식 자료 부산역 역사/' in r['path']or r['path'].startswith('실내 트윈 마감/연결 통로')or r['path'].startswith('실내 트윈 마감/승강장 측')or r['path'].startswith('맞이방 · 원본 정합')) and r['world_bounds_min'][1]<=8.05 and r['world_bounds_max'][1]>=6.95]
for r in rs:
 m=meshes[r['mesh']];
 if not m.get('binary'):continue
 v,t=binload(m);M=np.array(r['world_matrix']);wv=v@M[:3,:3].T+M[:3,3];uv=np.column_stack((wv[:,0]*s+wv[:,2]*c,wv[:,0]*c-wv[:,2]*s));p=wv[t];ys=p[:,:,1]
 flat=(np.max(ys,axis=1)<7.12)&(np.min(ys,axis=1)>6.85)
 for j in np.flatnonzero(flat):
  tri=uv[t[j]];a,b=tri[1]-tri[0],tri[2]-tri[0];area=abs(a[0]*b[1]-a[1]*b[0])/2
  if area<.005:continue
  d.polygon([xy(q)for q in tri],fill='#e4e9d5'if 'FPSWorld/'in r['path']else'#dfedff');floors.append({'owner':r['path'],'mesh':m['id'],'ordinal':int(j),'uv':tri.tolist(),'world_y':p[j,:,1].tolist(),'area':float(area)})
 for j in np.flatnonzero((np.min(ys,axis=1)<8)&(np.max(ys,axis=1)>8)):
  q=p[j];qq=uv[t[j]];ints=[]
  for a,b in [(0,1),(1,2),(2,0)]:
   if (q[a,1]-8)*(q[b,1]-8)<0:ints.append(qq[a]+(qq[b]-qq[a])*(8-q[a,1])/(q[b,1]-q[a,1]))
  if len(ints)==2:
   d.line([xy(q)for q in ints],fill='#333333'if 'FPSWorld/'in r['path']else'#aa4a44',width=1);sections.append({'owner':r['path'],'mesh':m['id'],'ordinal':int(j),'uv':[q.tolist()for q in ints]})
ret=json.loads(Path('workers/twin/output/floor2-architecture-next-20261009/whole-2f-guide-semantic-layout.json').read_text())
for r in ret['retail_nodes']:
 p=(r.get('current')or{}).get('native_root_uv_centroid')
 if p:d.ellipse([tuple(np.array(xy(p))-[8,8]),tuple(np.array(xy(p))+[8,8])],fill='#ebc56d');d.text(xy(p),str(r['guide_number']),font=font,fill='black')
ns=json.loads(Path('workers/twin/output/platform-escalators-20261009/platform-source-reconciliation.json').read_text())
for l in ns['lanes']:
 ps=l['existing_navigation_path'];uvs=[[p['x']*s+p['z']*c,p['x']*c-p['z']*s]for p in ps];d.line([xy(p)for p in uvs],fill='#409841',width=3);hi=max(ps,key=lambda p:p['y']);pp=[hi['x']*s+hi['z']*c,hi['x']*c-hi['z']*s];d.text(xy(pp),l['lane_id'].replace('esc-well-',''),font=font,fill='#15551a')
im.save(BASE/'current-2f-native-section.png');(BASE/'section-floors.json').write_text(json.dumps({'scene_sha256':x['scene_file_sha256'],'frame':'native station UVY; root yaw16.2','floor_triangles':floors,'wall_section_y8':sections},ensure_ascii=False));print('owners',len(rs),'floor',len(floors),'wallsection',len(sections))
