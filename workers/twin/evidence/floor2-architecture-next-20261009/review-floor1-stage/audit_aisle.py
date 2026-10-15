"""Qualitative guide aisle plus bounded source-wall and public-collider section checks."""
from pathlib import Path
import json,sys,math,hashlib
import numpy as np
from shapely.geometry import Polygon,LineString
from shapely.ops import unary_union,nearest_points
sys.path.insert(0,'workers/twin')
from official_plan_compare import readbin,cut
EV=Path('workers/twin/evidence/floor2-architecture-next-20261009/review-floor1-stage');PACKET=Path('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-packet.json');BASE=Path('.planning/2026-10-08-interior-runtime-completion/architecture/after-busanok-finishes/current-scene-structure.json');SPEC=Path('.planning/2026-09-23-video-twin/1f-implementation/interior-spec.json')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
p=json.loads(PACKET.read_text());s=json.loads(BASE.read_text());spec=json.loads(SPEC.read_text());mm={x['id']:x for x in s['meshes']};sn,cs=math.sin(math.radians(16.2)),math.cos(math.radians(16.2))
def uv_segments(wseg):return np.stack([wseg[:,:,0]*sn+wseg[:,:,1]*cs,wseg[:,:,0]*cs-wseg[:,:,1]*sn],axis=2)
front={};proof=[]
for u in spec['units']:
 if u['boardNo']>8:continue
 path='1층 · 원본 정합/1층 실내 · 근거 표기/Ground_Unit_'+str(u['mid'])+'_Walls';r=next(x for x in s['renderers'] if x['path']==path);m=mm[r['mesh']];v,t=readbin(m);wm=np.array(r['world_matrix']);w=v@wm[:3,:3].T+wm[:3,3];seg=uv_segments(cut(w[t],1.5));front[u['boardNo']]=unary_union([LineString(x) for x in seg]);proof.append({'boardNo':u['boardNo'],'owner':path,'mesh':m['id'],'source_binary_sha256':m['binary_sha256'],'world_matrix':r['world_matrix'],'qualification':'Native wall section is partial; open storefront edges are intentionally absent. It does not define the full shop occupation polygon.'})
clear=[]
for u in p['units']:
 after=Polygon(u['after']['polygon']);before=Polygon(u['before']['polygon']);j=min(front,key=lambda n:after.distance(front[n]));points=nearest_points(after,front[j]);clear.append({'rear_unit':u['boardNo'],'nearest_unmoved_front_row_wall_unit':j,'before_distance':before.distance(front[j]),'after_distance':after.distance(front[j]),'nearest_points':[list(x.coords)[0] for x in points]})
row=next(x for x in s['colliders'] if x['path']=='실내 트윈 마감/1F 공용/Kit_Colliders');m=mm[row['mesh']];v,t=readbin(m);patch=next(x for x in p['mesh_patches'] if x['source_asset']==m['asset']);after=v.astype(float).copy();after[np.array(patch['vertex_indices'],int)]=np.array(patch['replacement_positions']);wm=np.array(row['world_matrix']);w_before=v@wm[:3,:3].T+wm[:3,3];w_after=after@wm[:3,:3].T+wm[:3,3];hits=[]
for i in range(len(t)):
 for version,w in [('before_BOH',w_before),('after_BOH',w_after)]:
  segments=uv_segments(cut(w[t[i:i+1]],1.5))
  for u in p['units']:
   poly=Polygon(u['after']['polygon']).buffer(-.01);length=sum(LineString(x).intersection(poly).length for x in segments)
   if length>1e-5:hits.append({'triangle':i,'boardNo':u['boardNo'],'version':version,'intersected_length_model_units':length})
r={'schema':'chooguard.floor1-guide-aisle-review.v1','packet_sha256':sha(PACKET),'guide':p['guide'],'guide_sha256':p['guide_sha256'],'source_floor_label':'1F','visual_observation':'The diagram labels the open 전문식당가 between rear9–15 and opposing front3–7; rear units remain contiguous with16. No metric aisle width is supplied.','coordinate_claim':'Native model distances only. Nearest retained wall sections are not usable aisle widths, because door operation, facade depth and fixtures affect clearance.','unmoved_front_row_wall_receipts':proof,'rear_to_front_native_wall_distances':clear,'minimum_after_wall_distance':min(x['after_distance'] for x in clear),'public_collider_owner':row,'public_collider_source_binary_sha256':m['binary_sha256'],'public_collider_exact_section_test':{'height':1.5,'proposed_polygon_inset':.01,'hits':hits,'after_BOH_hits':[x for x in hits if x['version']=='after_BOH']},'remaining_runtime_gate':'Root checks navigation/door poses and fixture clearance after installation. No site or regulation clearance is certified.','production_writes':0}
(EV/'aisle-review.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'minimum_after_native_wall_distance':r['minimum_after_wall_distance'],'public_before_hits':[x for x in hits if x['version']=='before_BOH'],'public_after_hits':r['public_collider_exact_section_test']['after_BOH_hits']},ensure_ascii=False))
