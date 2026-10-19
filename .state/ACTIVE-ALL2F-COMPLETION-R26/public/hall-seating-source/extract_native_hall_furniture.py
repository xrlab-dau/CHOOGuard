"""Read-only construction-source inventory; does not access Unity or change assets."""
from pathlib import Path
import gzip, json, struct
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path('/Users/um-yunsang/CHOOGuard')
B = Path(__file__).resolve().parent
S = ROOT / '.state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-007'
source = json.loads((S / 'CURRENT-2F-MESH-SNAPSHOT.json').read_text())
Q = np.array([[-.34290633335, -.93936960061], [-.93936960061, .34290633335]])
rows = [r for r in source['objects'] if r.get('source_kind') == 'OFFICIAL_REFERENCE'
        and 'Station' in r['path'] and r['bounds_min_world'][1] < 8.6
        and r['bounds_max_world'][1] > 7.15]
by_index = {r['mesh_index']: r for r in rows}
report, pieces = [], {}
im = Image.new('RGB', (1400, 1300), 'white')
draw = ImageDraw.Draw(im)
def pp(p):
    return ((p[0] + 55) * 31, (p[1] + 80) * 31)

with gzip.open(S / 'CURRENT-2F-UNIQUE-MESH-GEOMETRY.bin.gz', 'rb') as f:
    while raw := f.read(4):
        ix = struct.unpack('<i', raw)[0]
        nv = struct.unpack('<i', f.read(4))[0]
        vb = f.read(nv * 12)
        ns = struct.unpack('<i', f.read(4))[0]
        r = by_index.get(ix)
        if r:
            v = np.frombuffer(vb, dtype='<f4').reshape(-1, 3).astype(float)
            m = np.array(r['world_matrix_column_major']).reshape((4, 4), order='F')
            v = v @ m[:3, :3].T + m[:3, 3]
            q = v[:, [0, 2]] @ Q.T
        for si in range(ns):
            topo, ni = struct.unpack('<ii', f.read(8))
            ib = f.read(ni * 4)
            if not r or topo != 0:
                continue
            t = np.frombuffer(ib, dtype='<i4').reshape(-1, 3)
            tq, ty = q[t], v[t, 1]
            in_roi = ((tq[:, :, 0].max(1) > -54) & (tq[:, :, 0].min(1) < -11)
                      & (tq[:, :, 1].max(1) > -79) & (tq[:, :, 1].min(1) < -39))
            above_floor = (ty.min(1) > 7.15) & (ty.max(1) < 8.6)
            a = t[in_roi & above_floor]
            if not len(a):
                continue
            used = np.unique(a)
            lookup = np.zeros(len(v), dtype=int)
            lookup[used] = np.arange(len(used))
            key = f'native_{ix}_{si}'
            pieces[key + '_v'] = v[used]
            pieces[key + '_t'] = lookup[a]
            mat = r['materials'][si] if si < len(r['materials']) else {}
            report.append(dict(key=key, path=r['path'], object_id=r['object_id'],
                               submesh=si, triangles=len(a), material=mat,
                               q_bounds=[q[used].min(0).tolist(), q[used].max(0).tolist()],
                               y_bounds=[float(v[used, 1].min()), float(v[used, 1].max())]))
            for tri in q[a]:
                draw.polygon([pp(p) for p in tri], fill=(135, 135, 135))

np.savez_compressed(B / 'EXISTING-NATIVE-HALL-FURNITURE-GEOMETRY.npz', **pieces)
(B / 'EXISTING-NATIVE-HALL-FURNITURE-SOURCE-INVENTORY.json').write_text(json.dumps({
    'purpose': 'Identify actual existing native geometry before any proposed public seating construction; no new occupancy authority or validation verdict.',
    'source': str(S.relative_to(ROOT)), 'scene_access': False,
    'native_geometry_changed': False, 'roi_actual_q': [[-54,-79],[-11,-39]],
    'selected_height_band_world_y': [7.15,8.6], 'parts': report,
    'limits': 'Only native triangle geometry wholly above7.15/below8.6. Crossing surfaces, other heights and unsampled faces are not proof of absence.'
}, ensure_ascii=False, indent=2))
im.save(B / 'NATIVE-HALL-FURNITURE-PLAN-NO-TEXT.png')
print(json.dumps({'native_submesh_parts': len(report), 'triangles': sum(p['triangles'] for p in report)}))
