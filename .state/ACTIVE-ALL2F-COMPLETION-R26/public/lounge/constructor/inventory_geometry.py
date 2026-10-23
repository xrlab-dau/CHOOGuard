import json,gzip,struct,sys
from pathlib import Path
import numpy as np
from shapely.geometry import Polygon,LineString,box
from shapely.ops import unary_union
from PIL import Image,ImageDraw
B=Path(__file__).parent;R=Path('/Users/um-yunsang/CHOOGuard');S=R/'.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-001';d=json.load(open(S/'CURRENT-2F-MESH-SNAPSHOT.json'));c=json.load(open(B/'ACTUAL-BOUNDS-CANDIDATES-R26.json'));sel={x['object_id'] for x in c};need={x['mesh_index'] for x in c}; meshes={}
with gzip.open(S/'CURRENT-2F-UNIQUE-MESH-GEOMETRY.bin.gz','rb') as f:
 while raw:=f.read(4):
  ix=struct.unpack('<i',raw)[0];n=struct.unpack('<i',f.read(4))[0];v=np.frombuffer(f.read(n*12),dtype='<f4').reshape(-1,3);ns=struct.unpack('<i',f.read(4))[0];t=[]
  for _ in range(ns):
   topo,ni=struct.unpack('<ii',f.read(8));a=np.frombuffer(f.read(ni*4),dtype='<i4')
   if topo==0:t.append(a.reshape(-1,3))
  if ix in need:meshes[ix]=(v.astype(float),np.concatenate(t) if t else np.empty((0,3),int))
Q=np.array([[-.34290633335,-.93936960061],[.93936960061,-.34290633335]])
cell=Polygon(json.load(open(B.parent/'LATER-R24-BOUNDED-PUBLIC-LOUNGE-AUTHORING-BASIS-R26.json'))['working_envelope_qx_minus_qy']['coordinates'][0]);clip=cell.buffer(.2);out=[]
img=Image.new('RGB',(1500,1100),'white');draw=ImageDraw.Draw(img);axs=[0,1]
def pt(v,ax):return (int((v[0]-31.8)*55)+ax*750,int((-30-v[1])*60)+25)
for row in d['objects']:
 if row['object_id'] not in sel:continue
 v,t=meshes[row['mesh_index']];M=np.array(row['world_matrix_column_major']).reshape(4,4,order='F');w=v@M[:3,:3].T+M[:3,3]
 if not len(t):continue
 hit={}
 for ax,y in zip(axs,[7.8,8.5]):
  tri=w[t];k=(tri[:,:,1].min(1)<=y)&(tri[:,:,1].max(1)>=y);tri=tri[k];hs={}
  for j in range(3):
   a,b=tri[:,j],tri[:,(j+1)%3];take=(a[:,1]<=y)!=(b[:,1]<=y);ids=np.flatnonzero(take);p=a[take]+(b[take]-a[take])*((y-a[take,1])/(b[take,1]-a[take,1]))[:,None]
   for i,q in zip(ids,p[:,[0,2]]@Q.T):hs.setdefault(int(i),[]).append(q)
  lines=[]
  for a in hs.values():
   if len(a)==2:
    g=LineString(a).intersection(clip)
    if not g.is_empty and g.length>.001:lines.append(g)
  if lines:
   hit[str(y)]=[list(g.coords) for g in lines if g.geom_type=='LineString']
   color='black' if row['source_kind']=='OFFICIAL_REFERENCE' else 'gray'
   for ln in hit[str(y)]:draw.line([pt(v,ax) for v in ln],fill=color,width=1)
 if hit:out.append({'object_id':row['object_id'],'path':row['path'],'kind':row['source_kind'],'source_mesh':row['source_mesh_asset'],'sections':hit})
for ax,y in zip(axs,[7.8,8.5]):
 draw.line([pt(v,ax) for v in cell.exterior.coords],fill='orange',width=3);draw.text((ax*750+20,5),str(y),fill='black')
img.save(B/'ACTUAL-CELL-SOURCE-AUTHORING-SECTIONS.png')
(B/'ACTUAL-CELL-SECTION-REUSE-R26.json').write_text(json.dumps(out,indent=2))
for r in out:print(r['kind'],r['path'],{k:round(sum(LineString(g).length for g in v),2) for k,v in r['sections'].items()})
