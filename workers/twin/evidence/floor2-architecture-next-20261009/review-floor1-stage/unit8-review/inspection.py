from pathlib import Path
import json,struct,hashlib,re,yaml
import numpy as np
from shapely.geometry import Polygon,LineString,Point
ROOT=Path('/Users/um-yunsang/CHOOGuard')
OUT=ROOT/'workers/twin/output/floor1-multiview-next-20261009/unit8-rebuild';OUT.mkdir(exist_ok=True)
EX=ROOT/'.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/current-before-unit8/current-scene-structure.json'
d=json.loads(EX.read_text());mm={x['id']:x for x in d['meshes']}
review=json.loads((OUT.parent/'remaining-guide-coverage/constrained-proposals8-review.json').read_text())
old=Polygon(review['current_polygon_UV']);new=Polygon(review['proposed_polygon_UV']);a=np.array(review['current_polygon_UV'])
front=LineString([a[4],a[5],a[6]]);back=LineString(a[:2]);closed=old.boundary.difference(front.buffer(.000001))
c=.9602937;s=.278991133
def binmesh(mid):
 raw=(ROOT/mm[mid]['binary']).read_bytes();nv,ni,nn=struct.unpack_from('<iii',raw)
 return np.frombuffer(raw,'<f4',nv*3,12).reshape(nv,3).astype(float),np.frombuffer(raw,'<i4',ni,12+nv*12).reshape(-1,3)
def world(r,v):
 m=np.array(r['world_matrix']);return v@m[:3,:3].T+m[:3,3]
def uv(w):return np.column_stack([s*w[:,0]+c*w[:,2],c*w[:,0]-s*w[:,2]])
paths=['1층 · 원본 정합/1층 실내 · 근거 표기/Ground_Unit_4912_Walls',
 '실내 트윈 마감/점포 내부 1F/Kit_Floor_floor_granite_tile',
 '실내 트윈 마감/점포 내부 1F/Kit_ShopCeiling_Kit_GridWhite',
 '실내 트윈 마감/점포 내부 1F/Kit_ShopWall_Kit_Paint_D9CBB0',
 '실내 트윈 마감/점포 내부 1F/Kit_ShopWall_wall_wood_slat_panel',
 '실내 트윈 마감/점포 내부 1F/Kit_Joint',
 '실내 트윈 마감/점포 내부 1F/Kit_Light_조명기구_downlight',
 '실내 트윈 마감/1F/Kit_Frame_Canopy_Dark',
 '실내 트윈 마감/1F/Kit_Glass',
 '실내 트윈 마감/1F/Kit_Trim_Kit_Hairline',
 '실내 트윈 마감/1F/Kit_Fascia_Kit_Paint_1E2226',
 '실내 트윈 마감/1F/Kit_Clad_wall_painted_gypsum_fine',
 '실내 트윈 마감/1F/Kit_Joint',
 '실내 트윈 마감/1F/Kit_Light_조명기구_downlight',
 '실내 트윈 마감/1F/Kit_Light_조명기구_발광',
 '실내 트윈 마감/1F/Kit_Colliders',
 '실내 트윈 마감/점포 내부 1F/Kit_Colliders']
rows=[]
for path in paths:
 r=next((x for x in d['renderers']+d['colliders'] if x['path']==path),None)
 if not r:continue
 v,t=binmesh(r['mesh']);w=world(r,v);q=uv(w);trq=q[t];trw=w[t]
 if '4912_Walls' in path:faces=list(range(len(t)));reason='dedicated all'
 elif '/점포 내부 1F/' in path:
  faces=[i for i in range(len(t)) if all(old.buffer(.045).covers(Point(x)) for x in trq[i])];reason='all vertices inside source shop+.045'
 else:
  faces=[i for i in range(len(t)) if all(front.distance(Point(x))<.36 for x in trq[i]) and trw[i,:,1].max()<=4.801];reason='front4/5 all vertices<.36 andY<=4.801'
 if not faces:continue
 m=mm[r['mesh']];asset=ROOT/m['asset'];raw=asset.read_bytes();y=yaml.safe_load(raw.decode().split('Mesh:\n',1)[1]);
 row={'owner':r,'mesh':m,'asset_sha256_at_read':hashlib.sha256(raw).hexdigest(),'mask_reason':reason,'face_ordinals':faces,
  'vertex_indices':sorted(set(t[faces].flatten().tolist())),
  'channels':y['m_VertexData']['m_Channels'],'submeshes':y['m_SubMeshes'],
  'selected_bounds_UV':[trq[faces].min((0,1)).tolist(),trq[faces].max((0,1)).tolist()],
  'selected_y_range':[float(trw[faces,:,1].min()),float(trw[faces,:,1].max())]}
 rows.append(row)
 print(m['id'],path,len(faces),'/',len(t),'UV',np.round(trq[faces].min((0,1)),3),np.round(trq[faces].max((0,1)),3),'Y',row['selected_y_range'])
(OUT/'owner-mask-inspection.json').write_text(json.dumps({'snapshot':str(EX.relative_to(ROOT)),'scene_sha256':d['scene_file_sha256'],'owners':rows},ensure_ascii=False,indent=2)+'\n')

scene=(ROOT/d['scene']).read_text();blocks={int(i):b for i,b in re.findall(r'^--- !u!\d+ &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',scene,re.M|re.S)}
doorblock=next((i,b) for i,b in blocks.items() if 'Element: 1F-unit08-front5\n' in b)
door=yaml.safe_load(doorblock[1])['MonoBehaviour'];g=door['m_GameObject']['fileID'];go=yaml.safe_load(blocks[g])['GameObject'];tfid=next(x['component']['fileID'] for x in go['m_Component'] if 'Transform:\n' in blocks[x['component']['fileID']]);tf=yaml.safe_load(blocks[tfid])['Transform']
leafrows=[]
for leaf in door['Leaves']:
 lid=leaf['Pivot']['fileID'];lt=yaml.safe_load(blocks[lid])['Transform'];lgo=yaml.safe_load(blocks[lt['m_GameObject']['fileID']])['GameObject'];leafrows.append({'transform_file_id':lid,'transform':lt,'gameobject_file_id':lt['m_GameObject']['fileID'],'name':lgo['m_Name'],'serialized_leaf':leaf})
texts=[]
for i,b in blocks.items():
 if 'm_Name:' in b and b.startswith('GameObject:\n'):
  gob=yaml.safe_load(b)['GameObject'];name=gob['m_Name']
  if name and ('양산국밥' in name or 'SHOP-1F-08' in name):texts.append({'gameobject_file_id':i,'name':name,'components':yaml.safe_load(b)['GameObject']['m_Component']})
(OUT/'door-native-yaml.json').write_text(json.dumps({'scene_file_sha256_at_read':hashlib.sha256(scene.encode()).hexdigest(),'StationDoor_component_file_id':doorblock[0],'StationDoor':door,'gameobject_file_id':g,'transform_file_id':tfid,'transform':tf,'leafs':leafrows,'matching_gameobjects':texts},ensure_ascii=False,indent=2)+'\n')
print('DOOR component',doorblock[0],'parenttransform',tfid,'leafs',[(x['transform_file_id'],x['name']) for x in leafrows])
