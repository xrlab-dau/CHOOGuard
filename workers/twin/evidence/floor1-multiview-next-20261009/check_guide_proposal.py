"""Offline source snapshot QA; writes only this lane's packet/evidence."""
import json, struct, hashlib
from pathlib import Path
import numpy as np
from shapely.geometry import Polygon, LineString
from shapely.ops import unary_union

ROOT = Path('/Users/um-yunsang/CHOOGuard')
OUT = ROOT / 'workers/twin/output/floor1-multiview-next-20261009'
EXPORT = ROOT / '.planning/2026-10-08-interior-runtime-completion/architecture/after-busanok-finishes/current-scene-structure.json'
data = json.loads(EXPORT.read_text())
packet = json.loads((OUT / 'conservative-proposals.json').read_text())
meshes = {m['id']: m for m in data['meshes']}

def binary(mid):
    raw = (ROOT / meshes[mid]['binary']).read_bytes()
    nv, ni, nn = struct.unpack_from('<iii', raw)
    vertices = np.frombuffer(raw, '<f4', nv*3, 12).reshape(nv,3).astype(float)
    indices = np.frombuffer(raw, '<i4', ni, 12+nv*12).reshape(-1,3)
    return vertices, indices

def identity(r):
    m = meshes[r['mesh']]
    return {'path':r['path'], 'global_object_id':r['global_object_id'], 'mesh':r['mesh'],
            'asset':m['asset'], 'binary':m['binary'], 'binary_sha256':m['binary_sha256']}

# The added Ground_Floor has native local XYZ=(V,Y,U) and the same rigid root.
floor_renderer = next(r for r in data['renderers'] if r['path']=='1층 · 원본 정합/Ground_Floor')
v, t = binary(floor_renderer['mesh'])
top = [Polygon(v[f][:,[2,0]]) for f in t if np.max(np.abs(v[f,1])) < .001]
floor = unary_union([p for p in top if p.area > 1e-7])
sections = json.loads((ROOT / '.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-actual-segments.json').read_text())
native = [(o,LineString(s)) for o in sections['owners'] if '/MainShell/' in o['path'] for s in o['segments'] if LineString(s).length >= 1]
public = json.loads((ROOT / '.planning/2026-09-24-interior-twin/specs/full/public-1f.json').read_text())
protected = [(e,Polygon(e['geometry']['polygon'])) for e in public['elements'] if 'polygon' in e.get('geometry',{})]
checks=[]
for op in packet['operations']:
    p=Polygon(op['proposed_polygon_UV']); current=Polygon(op['current_polygon_UV'])
    exterior=p.difference(floor.buffer(.02))
    hits=[{'owner_path':o['path'],'mesh_asset':o['mesh'],'segment_UV':list(map(list,s.coords)),
           'length_inside':s.intersection(p.buffer(-.01)).length} for o,s in native if s.intersection(p.buffer(-.01)).length > .01]
    corehits=[{'id':e['id'],'type':e['type'],'overlap_area':p.intersection(poly).area} for e,poly in protected if p.intersection(poly).area > .001]
    wall=op['dedicated_wall_owner']
    colliders=[c for c in data['colliders'] if c['path']==wall['path']]
    op['dedicated_wall_colliders']=colliders
    checks.append({'boardNo':op['boardNo'],'proposed_area':p.area,'proposed_valid':p.is_valid,
                   'supported_by_Ground_Floor_area':p.intersection(floor).area,
                   'unsupported_area_at_0_02_model_unit_tolerance':exterior.area,
                   'current_native_wall_bounds_UV': [min(x[0] for x in op['current_polygon_UV']),min(x[1] for x in op['current_polygon_UV']),max(x[0] for x in op['current_polygon_UV']),max(x[1] for x in op['current_polygon_UV'])],
                   'proposed_bounds_UV':list(p.bounds),'native_MainShell_long_segment_crossings':hits,
                   'protected_room_overlap':corehits,'dedicated_wall_collider_count':len(colliders),
                   'collider_mesh_matches_renderer':all(c.get('mesh')==wall['mesh'] for c in colliders),
                   'area_delta':p.area-current.area})

# Explicitly identify only faces from the one final BOH cladding segment.
clad=next(r for r in data['renderers'] if r['path']=='실내 트윈 마감/1F 공용/Kit_Clad_wall_painted_gypsum_fine')
v,t=binary(clad['mesh']); uv=v[:,[2,0]]
segment=LineString([[-43.64,-9.42],[-42.6,-17.1]])
from shapely.geometry import Point
candidate=[]
for i,face in enumerate(t):
    footprint=Polygon(uv[face]) if len(set(map(tuple,uv[face])))==3 else LineString(uv[face])
    # All projected vertices must lie within the small cladding thickness of this segment.
    if all(segment.distance(Point(uv[j])) < .20 for j in face):
        candidate.append({'face_ordinal':i,'indices':list(map(int,face)), 'vertices_localXYZ':v[face].tolist()})
clad_faces={'owner':identity(clad),'selected_faces':candidate,
            'selection_rule':'Every projected vertex within 0.20 model units of the exact final cladding polyline segment; inspect before mutation.',
            'source_segment_UV':list(map(list,segment.coords)),
            'proposed_segment_UV':[[-43.64,-9.42],[-42.6,-9.378]],
            'collider_owner_candidates':[c for c in data['colliders'] if c['path']=='실내 트윈 마감/1F 공용/Kit_Colliders']}

qa={'schema':'chooguard.floor1-guide-proposal-offline-QA.v1','snapshot':str(EXPORT.relative_to(ROOT)),
    'snapshot_sha256':hashlib.sha256(EXPORT.read_bytes()).hexdigest(), 'scene_sha256':data['scene_file_sha256'],
    'site_accuracy_verified':False, 'physical_accuracy_verified':False,
    'coordinate_note':'All distances and areas are existing Unity model units; source guide is schematic.',
    'floor_support_owner':identity(floor_renderer),'ground_floor_union_area':floor.area,'ground_floor_union_bounds':list(floor.bounds),
    'MainShell_test':'Source sections at Y=1.5; long segments length>=1; polygon inset0.01. Crossing test does not certify a watertight native enclosure.',
    'protected_room_test':'All polygon Room/ToiletRoom elements in current public-1f.json; preserve their meshes and colliders.',
    'unit_checks':checks, 'associated_BOH_cladding':clad_faces,
    'historical_enclosure_bounds_UV':[-93,-45,77.5,13],
    'integration_required':'Move wall, matching MeshCollider, unit-tagged batched glass/fascia/ceilings/shop floor, associated doors/interior colliders together. Preserve all unrelated faces and owner hashes.'}
(OUT/'offline-qa.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2)+'\n')
packet['offline_qa']='workers/twin/output/floor1-multiview-next-20261009/offline-qa.json'
packet['status']='ROOT_READY_WITH_CONTAINMENT_CHECKS' if all(x['proposed_valid'] and x['unsupported_area_at_0_02_model_unit_tolerance']<.01 and not x['native_MainShell_long_segment_crossings'] and not x['protected_room_overlap'] for x in checks) else 'ROOT_READY_WITH_EXPLICIT_CONTAINMENT_EXCEPTIONS'
(OUT/'conservative-proposals.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'status':packet['status'],'units':[{k:x[k] for k in ['boardNo','unsupported_area_at_0_02_model_unit_tolerance','native_MainShell_long_segment_crossings','protected_room_overlap','dedicated_wall_collider_count']} for x in checks], 'cladding_faces':len(candidate)},ensure_ascii=False))
