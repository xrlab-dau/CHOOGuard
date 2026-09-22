"""닫힌 공간 탐지 — '걸어다닐 수 있는 내부가 있는가'에 대한 직접 검사.

방의 기하학적 정의: 어떤 (x,y) 에서 **위를 보는 면(바닥)** 위에 사람 키만큼 떨어져
**아래를 보는 면(천장)** 이 있으면, 그 사이가 방이다.

앞선 격자 프로브는 위를 보는 면만 셌다. 그래서 z=7.0m 의 `-부산역` 면 267개가
2F 콩코스 바닥인지 실외 캐노피 윗면인지 구분할 수 없었다. 캐노피는 위에 천장이 없다.

여기서는 한 수직선의 모든 교차면을 법선 방향과 함께 모아 (바닥, 천장) 쌍을 만든다.
머리 공간 2.2m 이상, 층고 6m 이하를 사람이 설 수 있는 방으로 본다.
"""
import bpy, pathlib, json, sys
from collections import defaultdict
from mathutils import Vector

ROOT = pathlib.Path.cwd()
OUT = ROOT / 'asset-library/research-public/2026-09-22/ver13-audit'
argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
BLEND = argv[0] if argv else str(OUT / 'ver13-batched.blend')

bpy.ops.wm.open_mainfile(filepath=BLEND)
s = bpy.context.scene

STATION = {'-부산역', '-부산역_선로상층부', '-부산역_선로상층부(주차장)', '-부산역_출구지붕'}
INTERIOR_CANDIDATE = STATION | {'Layer0'}

# 역사 본체의 XY 범위로 격자를 한정한다. 주변 건물은 이 판정의 대상이 아니다.
lo = [1e9, 1e9]; hi = [-1e9, -1e9]
for o in s.objects:
    if o.type != 'MESH' or o.get('source_layer') not in STATION: continue
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        for k in range(2):
            lo[k] = min(lo[k], w[k]); hi[k] = max(hi[k], w[k])

MIN_HEAD = 2.2     # 사람이 설 수 있는 최소 머리 공간
MAX_STOREY = 6.0   # 이보다 높으면 방이 아니라 지붕 아래 외부 공간으로 본다
STEP = 2.0

dg = bpy.context.evaluated_depsgraph_get()
rooms = []
by_floor_z = defaultdict(int)
probes = 0

x = lo[0]
while x <= hi[0]:
    y = lo[1]
    while y <= hi[1]:
        probes += 1
        origin = Vector((x, y, 140.0)); surfaces = []
        for _ in range(120):
            hit, p, n, idx, o, mat = s.ray_cast(dg, origin, Vector((0, 0, -1)), distance=220)
            if not hit: break
            surfaces.append((p.z, n.z, str(o.get('source_layer'))))
            origin = p - Vector((0, 0, .005))
        # 위에서 아래로 정렬돼 있다. 바닥(위를 봄) 바로 위에 천장(아래를 봄)이 있는 쌍을 찾는다.
        for i in range(len(surfaces)):
            fz, fn, fl = surfaces[i]
            if fn <= 0.8 or fl not in INTERIOR_CANDIDATE: continue   # 바닥 후보 아님
            # 이 바닥보다 위에 있는 가장 가까운 아래보기 면 = 천장
            ceil = None
            for j in range(i - 1, -1, -1):
                cz, cn, cl = surfaces[j]
                if cz <= fz + 0.05: continue
                if cn < -0.8 and cl in INTERIOR_CANDIDATE:
                    ceil = (cz, cl); break
                if cn > 0.8: break        # 다른 바닥을 먼저 만나면 이 바닥의 방은 그 아래가 아니다
            if ceil is None: continue
            head = ceil[0] - fz
            if MIN_HEAD <= head <= MAX_STOREY:
                rooms.append({'xy': [round(x, 1), round(y, 1)], 'floorZ': round(fz, 2),
                              'ceilingZ': round(ceil[0], 2), 'headroom': round(head, 2),
                              'floorLayer': fl, 'ceilingLayer': ceil[1]})
                by_floor_z[round(fz * 2) / 2] += 1
        y += STEP
    x += STEP

bands = sorted(by_floor_z.items(), key=lambda kv: -kv[1])
# 대역별 레이어 구성 — 어느 FBX 를 뽑아야 하는지가 여기서 정해진다.
band_layers = defaultdict(lambda: defaultdict(int))
for r in rooms:
    band_layers[round(r['floorZ'] * 2) / 2][r['floorLayer'] + ' / ' + r['ceilingLayer']] += 1
report = {
    'question': 'Is there walkable enclosed interior? A room = up-facing floor with a down-facing ceiling 2.2-6.0m above.',
    'sourceBlend': BLEND,
    'gridStepMetres': STEP, 'minHeadroom': MIN_HEAD, 'maxStorey': MAX_STOREY,
    'layersTreatedAsInterior': sorted(INTERIOR_CANDIDATE),
    'probePoints': probes, 'roomSamples': len(rooms),
    'roomShareOfProbes': round(len(rooms) / probes, 4) if probes else 0,
    'floorBandsDescending': [{'floorZMetres': z, 'samples': n,
                              'layerPairs': dict(sorted(band_layers[z].items(), key=lambda kv: -kv[1])[:4])}
                             for z, n in bands[:25]],
    'examples': rooms[:40],
    'honest_limits': [
        'Vertical ray sampling only. A room smaller than the 2m grid can be missed entirely.',
        'Layer0 is SketchUp default layer and mixes props with structure; treating it as interior may overcount.',
        'Down-facing surface is not proof of a finished ceiling; it can be the underside of a canopy.',
        'No wall enclosure test was performed, so an open-sided covered area can register as a room.',
    ],
}
(OUT / 'ver13-enclosed-space.json').write_text(json.dumps(report, ensure_ascii=False, indent=2))
print('PROBES', probes, 'ROOM_SAMPLES', len(rooms), 'SHARE', report['roomShareOfProbes'], flush=True)
for z, n in bands[:10]:
    top = sorted(band_layers[z].items(), key=lambda kv: -kv[1])[:2]
    print(f'  floor z={z:>7.1f}m  samples={n:>5}  {top}', flush=True)
