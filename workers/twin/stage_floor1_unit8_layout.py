#!/usr/bin/env python3
"""Normalize sealed Unit8 offline geometry into the atomic scene installer contract."""
import hashlib,json
from pathlib import Path
W=Path('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout')
P=Path('workers/twin/output/floor1-multiview-next-20261009/unit8-rebuild/unit8-replacement-packet.json')
G=Path('asset-library/research-public/2026-10-09/floor1-multiview-next/source05-yoonsscom/img_2.jpg')
S=Path('Assets/ChooGuard/Scenes/FpsStation.unity')
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def main():
 x=json.loads(P.read_text()); assert x['status']=='OFFLINE_GEOMETRY_READY'
 assert sha(S)==x['source_export_scene_sha256']
 assert sha(G)==x['guide_image_sha256']
 assert not x['native_floor_append_required'] and not x['new_mesh_pieces']
 patches=[]
 for m in x['mesh_replacements']:
  assert sha(m['source_asset'])==m['source_asset_file_sha256_at_read']
  v=m['source_vertex_count']; assert all(i>=v for face in m['added_triangles'] for i in face)
  patches.append(dict(source_asset=m['source_asset'],source_asset_guid=m['source_asset_guid'],
   source_file_sha256=m['source_asset_file_sha256_at_read'],vertices=v,triangles=m['source_triangle_count'],
   vertex_indices=[],expected_positions=[],replacement_positions=[],
   owners=[o for o in m['owners'] if o.get('type')!='MeshCollider'],
   collider_owners=[o for o in m['owners'] if o.get('type')=='MeshCollider'],
   **{k:m[k] for k in ('removed_triangle_ordinals','append_vertices','append_normals','append_tangents','append_uv0','added_triangles')}))
 motions=[]
 for m in x['world_rigid_motions']:
  row=dict(kind='path',path=m['path'],world_rigid_delta=m['world_rigid_delta'])
  if m.get('StationDoor_component_file_id'):
   row['kind']='door';row['door_element']=x['door']['element']
  motions.append(row)
 packet=dict(schema='chooguard.floor-guide-layout-patch.v1',batch_id='unit8',floor=1,scene=str(S),scene_sha256=sha(S),
  reference_only=True,site_accuracy_verified=False,guide=str(G),guide_sha256=sha(G),proposal=str(P),proposal_sha256=sha(P),
  additional_evidence=[dict(path=str(p),sha256=sha(p)) for p in (P.parent/'offline-review-qa.json',)],
  method='Guide topology constrained by retained native exterior, lift/stair core and original flush floors; replace only exact Unit8 mesh faces, preserve existing material assignments and unrelated channel/index bytes.',
  units=[dict(boardNo=8,name='양산국밥',before=dict(polygon=x['current_polygon_UV']),after=dict(polygon=x['proposed_polygon_UV']),vertex_mapping='native station localXYZ=(V,Y,U); remove exact source Unit8 faces and append independently checked new constrained ten-point envelope')],
  mesh_patches=patches,movable_transforms=motions,rejected_owners=[])
 out=W/'unit8-packet.json';out.write_text(json.dumps(packet,ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(dict(packet=str(out),sha256=sha(out),proposal_sha256=sha(P),meshes=len(patches),motions=len(motions)),ensure_ascii=False))
if __name__=='__main__':main()
