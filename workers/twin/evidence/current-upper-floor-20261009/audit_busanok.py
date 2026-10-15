"""Read-only facet audit of current Busanok owners. No scene/asset mutation."""
import hashlib
import json
import pathlib
import struct
import numpy as np

BASE = pathlib.Path('/Users/um-yunsang/CHOOGuard')
OUT = BASE / 'workers/twin/evidence/current-upper-floor-20261009'
SCENE = BASE / '.planning/2026-10-08-interior-runtime-completion/architecture/after-native-doors/current-scene-structure.json'
d = json.loads(SCENE.read_text())
mm = {m['id']: m for m in d['meshes']}
glass = next(r for r in d['renderers'] if r['path'] == '실내 트윈 마감/3F 점포/Box frame/Box_Kit_Glass')
box = np.array(glass['world_matrix'])
inv_box = np.linalg.inv(box)
lo = np.array([26.5, 12.2, -2.35])
hi = np.array([36.5, 15.65, 6.75])
corners = np.array([[x, y, z, 1] for x in [lo[0],hi[0]] for y in [lo[1],hi[1]] for z in [lo[2],hi[2]]]) @ box.T
world_lo, world_hi = corners[:,:3].min(0), corners[:,:3].max(0)
cache = {}
def mesh(mid):
    if mid not in cache:
        m = mm[mid]
        b = (BASE / m['binary']).read_bytes()
        assert hashlib.sha256(b).hexdigest() == m['binary_sha256'], mid
        nv, ni, nn = struct.unpack_from('<iii', b)
        assert nv == m['vertices'] and len(b) == 12 + nv*12 + ni*4 + nn*12
        v = np.frombuffer(b,dtype='<f4',count=nv*3,offset=12).reshape(-1,3)
        t = np.frombuffer(b,dtype='<i4',count=ni,offset=12+nv*12).reshape(-1,3)
        assert not len(t) or (t.max() < nv and t.min() >= 0)
        cache[mid] = v, t
    return cache[mid]

planes = {'front_u_6.75': (2, 6.75), 'back_u_-2.35': (2,-2.35), 'side_v_26.5': (0,26.5), 'side_v_36.5': (0,36.5)}
hits = []
scanned = 0
for r in d['renderers']:
    if not (r['active'] and r['renderer_enabled']): continue
    if not (np.all(np.array(r['world_bounds_max']) >= world_lo-.15) and np.all(np.array(r['world_bounds_min']) <= world_hi+.15)): continue
    v, t = mesh(r['mesh'])
    if not len(t): continue
    xform = inv_box @ np.array(r['world_matrix'])
    p = (np.column_stack((v,np.ones(len(v)))) @ xform.T)[:,:3]
    tri = p[t]
    a, b = tri.min(1), tri.max(1)
    inside = np.all(a >= lo-.15,axis=1) & np.all(b <= hi+.15,axis=1)
    scanned += 1
    for name,(axis,value) in planes.items():
        # The generous plane band finds both glass sheets and thin wall/frame returns.
        # A substantial vertical triangle is required; tiny frame facets are omitted.
        tangent = 0 if axis==2 else 2
        mask = inside & (np.max(np.abs(tri[:,:,axis]-value),axis=1) < .08) & ((b[:,1]-a[:,1])>.2) & ((b[:,tangent]-a[:,tangent])>.2)
        ids = np.flatnonzero(mask)
        if len(ids):
            hits.append({'plane':name,'renderer':r,'mesh':mm[r['mesh']], 'triangle_ordinals_zero_based':ids.tolist(), 'facets':[{'triangle':int(i),'vertex_indices':t[i].tolist(),'box_xyz':tri[i].round(7).tolist()} for i in ids]})

wall = next(r for r in d['renderers'] if r['path'].endswith('/Box_Shop_부산옥_Walls'))
colliders = [c for c in d['colliders'] if c['path'] == wall['path'] or c['path'] == glass['path']]
plan = json.loads((BASE/'.planning/2026-09-23-video-twin/majibang-plan.json').read_text())
unit = next(u for u in plan['thirdFloor']['units'] if u['name']=='부산옥')
result = {
 'schema':'chooguard.busanok-local-topology-facet-audit.v1',
 'role':'reference_only',
 'scene_inventory':str(SCENE.relative_to(BASE)),
 'scene_inventory_sha256':hashlib.sha256(SCENE.read_bytes()).hexdigest(),
 'scene_file_sha256':d['scene_file_sha256'], 'sampled_utc':d['sampled_utc'],
 'coordinate_frame':{'box_xyz':'[v, heightY, u] from current Box frame; scene units do not establish site measurements','box_world_matrix':box.tolist()},
 'recipe':unit,
 'plane_search':{'box_bounds_min':lo.tolist(),'box_bounds_max':hi.tolist(),'max_plane_distance_scene_units':.08,'substantial_vertical_facet_min_span_scene_units':.2,'scanned_renderer_count':scanned,'verified_mesh_binary_count':len(cache)},
 'hits':hits,
 'named_owner_colliders':colliders,
 'important_identity_boundary':'These Busanok owners are retained production MajibangBuilder/Kit geometry, with null original source_component_id. No exact original SketchUp source instance has been identified as Busanok.',
 'conclusion':{
  'current_front_glazing':'mesh-0412 triangles 0..51 only on u=6.75 inside the Busanok envelope; 6 fixed lower panes + 7 upper transom panes. Door leaves are separate existing owners.',
  'current_opaque_returns':'mesh-3348 triangles 0..3 v=26.5; 4..7 u=-2.35; 8..11 v=36.5. The same mesh is the enabled static MeshCollider.',
  'photo_supported_mismatch':'New 2026-08-09 published interior photo has two perpendicular glazed tenant faces. Current named Busanok envelope has one glazed face and both perpendicular returns opaque.',
  'registration_remaining':'Photo does not independently establish which local v side is the pictured glazed return, where the counter lies in that frame, or metric dimensions. Side v=26.5 and v=36.5 remain alternate candidate facet groups; do not cut both.'
 }
}
(OUT/'busanok-facet-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2))
print(json.dumps({'scanned':scanned,'verified_binary_meshes':len(cache),'hits':[{'plane':h['plane'],'path':h['renderer']['path'],'mesh':h['mesh']['id'],'triangles':h['triangle_ordinals_zero_based']} for h in hits]},ensure_ascii=False,indent=2))
