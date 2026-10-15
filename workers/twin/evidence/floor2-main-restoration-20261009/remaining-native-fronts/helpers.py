from pathlib import Path
import json,re,yaml,hashlib,struct,sys
import numpy as np
from scipy.spatial.transform import Rotation
from shapely.geometry import Polygon,LineString
from shapely.ops import unary_union
sys.path.insert(0,str(Path('workers/twin').resolve()));from official_plan_compare import cut,readbin
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-main-restoration-20261009/remaining-native-fronts');O.mkdir(exist_ok=True)
P=json.loads(Path('workers/twin/output/floor2-main-restoration-20261009/four-bay-packet.json').read_text());R=json.loads(Path('content/world/observed-interiors/floor-guide-layout.json').read_text());REG={r['source_asset']:r for r in R['meshes']};SCENE=Path(P['scene']);txt=SCENE.read_text();blocks={int(i):b for i,b in re.findall(r'^--- !u!\d+ &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',txt,re.M|re.S)};tf={};go={};go_tf={};sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();hashbytes=lambda b:hashlib.sha256(b).hexdigest()
for i,b in blocks.items():
 if b.startswith('GameObject:\n'):
  g=yaml.safe_load(b)['GameObject'];go[i]={'name':g['m_Name'],'active':g['m_IsActive']}
 elif b.startswith(('Transform:\n','RectTransform:\n')):
  x=yaml.safe_load(b)[b.split(':')[0]];tf[i]=x;go_tf[x['m_GameObject']['fileID']]=i
cache={}
def matrix(i):
 if i in cache:return cache[i]
 t=tf[i];a=lambda k:np.array([float(t[k][d]) for d in ('x','y','z')]);r=t['m_LocalRotation'];q=[float(r[k]) for k in ('x','y','z','w')];m=np.eye(4);m[:3,:3]=Rotation.from_quat(q).as_matrix()@np.diag(a('m_LocalScale'));m[:3,3]=a('m_LocalPosition');p=t['m_Father']['fileID'];cache[i]=matrix(p)@m if p in tf else m;return cache[i]
def path(i):
 t=tf[i];n=str(go[t['m_GameObject']['fileID']]['name']);p=t['m_Father']['fileID'];return path(p)+'/'+n if p in tf else n
def lineage(i):
 out=[]
 while i in tf:
  out.append({'transform_id':i,'block_sha256':hashbytes(blocks[i].encode()),'fields':tf[i]});i=tf[i]['m_Father']['fileID']
 return out
SCENE_GUID=re.search(r'^guid: (.*)$',Path(str(SCENE)+'.meta').read_text(),re.M)[1]
def meshread(p):
 text=Path(p).read_text();vd=yaml.safe_load(re.search(r'^  m_VertexData:\n.*?(?=^  m_CompressedMesh:)',text,re.M|re.S)[0])['m_VertexData'];raw=bytes.fromhex(vd['_typelessdata']);nv=vd['m_VertexCount'];ch=vd['m_Channels'];sm=yaml.safe_load(re.search(r'^  m_SubMeshes:\n.*?(?=^  m_Shapes:)',text,re.M|re.S)[0])['m_SubMeshes'];fmt=int(re.search(r'^  m_IndexFormat: (\d+)$',text,re.M)[1]);ib=bytes.fromhex(re.search(r'^  m_IndexBuffer: (.*)$',text,re.M)[1]);idx=np.frombuffer(ib,'<u4' if fmt else '<u2');dt=4 if fmt else 2;t=np.concatenate([idx[s['firstByte']//dt:s['firstByte']//dt+s['indexCount']].astype(np.int64)+s['baseVertex'] for s in sm]).reshape(-1,3);stride=len(raw)//nv;v=np.ndarray((nv,3),'<f4',raw,offset=ch[0]['offset'],strides=(stride,4)).copy();return {'v':v,'t':t,'raw':raw,'vd':vd,'channels':ch,'submeshes':sm,'index_format':fmt,'index_bytes':ib,'stride':stride}
def ownerproof(guid):
 owners=[];cols=[]
 for i,b in blocks.items():
  typ=b.split(':')[0]
  if typ not in ['MeshFilter','MeshCollider'] or not re.search(r'm_Mesh: \{[^\n]*guid: '+guid+r',',b):continue
  gid=int(re.search(r'^  m_GameObject: \{fileID: (\d+)\}',b,re.M)[1]);tti=go_tf[gid];proof={'path':path(tti),'global_object_id':f'GlobalObjectId_V1-2-{SCENE_GUID}-{i}-0','component_id':i,'gameobject_id':gid,'transform_id':tti,'component_block_sha256':hashbytes(b.encode()),'world_matrix':matrix(tti).tolist(),'transform_lineage':lineage(tti),'active_self':go[gid]['active']}
  if typ=='MeshFilter':
   comps=re.findall(r'component: \{fileID: (\d+)\}',blocks[gid]);render=next(int(c) for c in comps if blocks.get(int(c),'').startswith('MeshRenderer:\n'));rb=blocks[render];proof['renderer_component_id']=render;proof['renderer_block_sha256']=hashbytes(rb.encode());proof['materials']=re.findall(r'^  - \{fileID: (\d+), guid: (\w+), type: (\d+)\}$',rb,re.M);owners.append(proof)
  else:proof['collider_block_sha256']=hashbytes(b.encode());cols.append(proof)
 return owners,cols
