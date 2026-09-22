"""바닥 지지 격자 프로브 — 2026-09-21 audit_floor_support.py 의 두 결함을 고쳤다.

결함 ①: 레이어 4개만 남기고 나머지를 **삭제한 뒤** 프로브했다. 시야를 미리 좁혔다.
        → 아무것도 지우지 않는다. 히트마다 `source_layer` 를 기록해 분석 단계에서 나눈다.

결함 ②: 작성자가 임의로 고른 **7개 점**에만 레이를 쐈다(`author-selected interior probes`).
        7점이 전부 빗나가도 결과는 똑같이 False 다. 즉 '없다'와 '못 찾았다'를 구분할 수 없었다.
        → 역사 풋프린트를 **규칙적 격자**로 훑고, 위를 보는 면(normalZ>0.8)의 z 분포를
          히스토그램으로 낸다. 층이 있으면 특정 z 대역에 면적이 몰린다. 이것이 정량 증거다.

판정 기준: 어떤 z 대역(0.5m bin)에 위를 보는 히트가 격자점의 상당 비율로 몰리면 '바닥면'이다.
지붕 하나만 있으면 높은 z 에 한 덩어리만 나온다. 층이 여럿이면 봉우리가 여럿이다.
"""
import bpy, pathlib, json, sys
from collections import defaultdict
from mathutils import Vector

ROOT = pathlib.Path.cwd()
OUT = ROOT / 'asset-library/research-public/2026-09-22/ver13-audit'
# Blender 자체 인자와 섞이지 않게 '--' 뒤만 본다.
argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
BLEND = argv[0] if len(argv) > 0 else str(OUT / 'ver13-batched.blend')
TAG = argv[1] if len(argv) > 1 else 'ver13'

bpy.ops.wm.open_mainfile(filepath=BLEND)
s = bpy.context.scene

# 역사 레이어의 XY 풋프린트를 격자 범위로 삼는다. 주변 건물·도로는 범위 밖이라 자연히 빠진다.
STATION = {'-부산역', '-부산역_선로상층부', '-부산역_선로상층부(주차장)', '-부산역_출구지붕'}
lo = [1e9, 1e9]; hi = [-1e9, -1e9]
for o in s.objects:
    if o.type != 'MESH' or o.get('source_layer') not in STATION: continue
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        for k in range(2):
            lo[k] = min(lo[k], w[k]); hi[k] = max(hi[k], w[k])
print('STATION_FOOTPRINT', [round(x, 1) for x in lo], [round(x, 1) for x in hi], flush=True)

STEP = 3.0
dg = bpy.context.evaluated_depsgraph_get()
bins = defaultdict(lambda: {'up': 0, 'any': 0, 'layers': defaultdict(int)})
probes = 0; probes_with_hit = 0; total_hits = 0

x = lo[0]
while x <= hi[0]:
    y = lo[1]
    while y <= hi[1]:
        probes += 1
        origin = Vector((x, y, 140.0)); got = 0
        for _ in range(80):                      # 한 수직선에서 최대 80 면까지 뚫고 내려간다
            hit, p, n, idx, o, mat = s.ray_cast(dg, origin, Vector((0, 0, -1)), distance=200)
            if not hit: break
            got += 1; total_hits += 1
            b = round(p.z * 2) / 2               # 0.5m bin
            rec = bins[b]; rec['any'] += 1
            if n.z > 0.8:
                rec['up'] += 1
                rec['layers'][str(o.get('source_layer'))] += 1
            origin = p - Vector((0, 0, .005))
        if got: probes_with_hit += 1
        y += STEP
    if int((x - lo[0]) / STEP) % 20 == 0: print('COL', round(x, 1), 'of', round(hi[0], 1), flush=True)
    x += STEP

rows = [{'zMetres': z, 'upFacingHits': d['up'], 'allHits': d['any'],
         'upFacingShareOfProbes': round(d['up'] / probes, 4) if probes else 0,
         'layers': dict(d['layers'])}
        for z, d in sorted(bins.items()) if d['up'] > 0]
rows.sort(key=lambda r: -r['upFacingHits'])

report = {
    'surface': 'Read-only vertical geometric intersections over a regular grid; not nav/path validation',
    'sourceBlend': BLEND,
    'stationLayersUsedForFootprint': sorted(STATION),
    'noLayerDeletion': True,
    'gridStepMetres': STEP,
    'footprintMin': [round(v, 2) for v in lo], 'footprintMax': [round(v, 2) for v in hi],
    'probePoints': probes, 'probePointsWithAnyHit': probes_with_hit, 'totalHits': total_hits,
    'upFacingByHeightDescending': rows[:60],
    'interpretation': 'A floor shows as a z-bin whose upFacingShareOfProbes is a large fraction of the footprint. A roof-only shell shows one high band. Multiple storeys show multiple bands.',
}
(OUT / f'{TAG}-floor-histogram.json').write_text(json.dumps(report, ensure_ascii=False, indent=2))
print('PROBES', probes, 'WITH_HIT', probes_with_hit, 'HITS', total_hits, flush=True)
for r in rows[:18]:
    print(f"  z={r['zMetres']:>7.1f}m  up={r['upFacingHits']:>6}  share={r['upFacingShareOfProbes']:.3f}  {list(r['layers'].items())[:3]}", flush=True)
