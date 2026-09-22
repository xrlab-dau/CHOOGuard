"""ver13 컴포넌트 감사 — 2026-09-21 audit_interior_components.py 의 사본에 두 곳을 고쳤다.

고친 것 ①: 원본은 `children(e,'Groups')[56]` 으로 역사 루트를 **하드코딩**했다("Proven mainstation
root group56"). ver13 은 다른 파일이므로 그 인덱스가 같다는 보장이 없다. 최상위 그룹을 **전부** 훑고
그룹별로 집계해, 어느 그룹이 역사인지를 결과에서 읽는다.

고친 것 ②: 분류에 **바닥·벽·천장·슬래브**를 추가했다. 원본은 계단·에스컬레이터·엘베·문만 셌는데,
'걸어다닐 수 있는 내부가 있는가'는 계단이 아니라 **바닥면**이 답한다.

두 파일을 같은 잣대로 재려면 대조군도 필요하므로, 인자로 받은 SKP 무엇이든 처리한다.
"""
import ctypes as C, pathlib, json, numpy as np, itertools, math, sys

ROOT = pathlib.Path.cwd()
SRC = ROOT / 'asset-library/research-public/2026-09-21/quality-a/station-model-source'
OUT = ROOT / 'asset-library/research-public/2026-09-22/ver13-audit'
OUT.mkdir(parents=True, exist_ok=True)

SKP_NAME = sys.argv[1] if len(sys.argv) > 1 else '부산역ver13.skp'
TAG = sys.argv[2] if len(sys.argv) > 2 else 'ver13'
SKP = SRC / 'station-extracted' / SKP_NAME

class Ref(C.Structure): _fields_ = [('ptr', C.c_void_p)]
class Point(C.Structure): _fields_ = [('x', C.c_double), ('y', C.c_double), ('z', C.c_double)]
class Box(C.Structure): _fields_ = [('min', Point), ('max', Point)]
class Transform(C.Structure): _fields_ = [('values', C.c_double * 16)]

lib = C.CDLL(str(SRC / 'importer-isolated/sketchup_importer/SketchUpAPI.framework/Versions/A/SketchUpAPI'))
def sig(name, args):
    f = getattr(lib, name); f.argtypes = args; f.restype = C.c_int; return f

create = sig('SUModelCreateFromFile', [C.POINTER(Ref), C.c_char_p])
entities = sig('SUModelGetEntities', [Ref, C.POINTER(Ref)])
bbox = sig('SUEntitiesGetBoundingBox', [Ref, C.POINTER(Box)])
ge = sig('SUGroupGetEntities', [Ref, C.POINTER(Ref)])
de = sig('SUComponentDefinitionGetEntities', [Ref, C.POINTER(Ref)])
definition = sig('SUComponentInstanceGetDefinition', [Ref, C.POINTER(Ref)])
gt = sig('SUGroupGetTransform', [Ref, C.POINTER(Transform)])
it = sig('SUComponentInstanceGetTransform', [Ref, C.POINTER(Transform)])
for kind in ['Groups', 'Instances']:
    sig('SUEntitiesGetNum' + kind, [Ref, C.POINTER(C.c_size_t)])
    sig('SUEntitiesGet' + kind, [Ref, C.c_size_t, C.POINTER(Ref), C.POINTER(C.c_size_t)])
sc = sig('SUStringCreate', [C.POINTER(Ref)])
sn = sig('SUComponentDefinitionGetName', [Ref, C.POINTER(Ref)])
sl = sig('SUStringGetUTF8Length', [Ref, C.POINTER(C.c_size_t)])
su = sig('SUStringGetUTF8', [Ref, C.c_size_t, C.c_char_p, C.POINTER(C.c_size_t)])
sr = sig('SUStringRelease', [C.POINTER(Ref)])

def name(ref):
    s = Ref(); sc(C.byref(s)); sn(ref, C.byref(s))
    n = C.c_size_t(); sl(s, C.byref(n))
    buf = C.create_string_buffer(n.value + 1); got = C.c_size_t()
    su(s, n.value + 1, buf, C.byref(got)); result = buf.value.decode('utf-8'); sr(C.byref(s))
    return result

reg = json.loads((SRC / 'station-rigid-registration.json').read_text())
ang = math.radians(reg['yawDegreesSourceXYCCW'])
R = np.array([[math.cos(ang), -math.sin(ang)], [math.sin(ang), math.cos(ang)]])
translation = np.array(reg['translationSourceXYMetres'])

lib.SUInitialize()
m = Ref()
assert create(C.byref(m), str(SKP).encode()) == 0, f'열지 못함 {SKP}'
e = Ref(); entities(m, C.byref(e))
rows = []; names = {}; visited = 0

def children(ent, kind):
    n = C.c_size_t(); getattr(lib, 'SUEntitiesGetNum' + kind)(ent, C.byref(n))
    refs = (Ref * n.value)(); got = C.c_size_t()
    getattr(lib, 'SUEntitiesGet' + kind)(ent, n, refs, C.byref(got))
    return refs

def classify(n):
    if '계단' in n: return 'source_named_stair'
    if '에스컬' in n: return 'source_named_escalator'
    if '엘베' in n or '엘리베' in n: return 'source_named_lift'
    if n.startswith('문_') or '정문' in n or '출입' in n: return 'source_named_door_or_entrance'
    # 추가 분류 — '걸어다닐 수 있는가'는 바닥이 답한다
    if '바닥' in n or 'floor' in n.lower() or '슬래브' in n or 'slab' in n.lower(): return 'source_named_floor'
    if '천장' in n or 'ceiling' in n.lower(): return 'source_named_ceiling'
    if n.startswith('벽') or '내벽' in n or 'wall' in n.lower(): return 'source_named_wall'
    if '대합실' in n or '맞이방' in n or 'concourse' in n.lower(): return 'source_named_concourse'
    return None

def record(ent, M, path, n, kind, group):
    b = Box()
    if bbox(ent, C.byref(b)): return
    corners = np.array([[x, y, z, 1] for x, y, z in itertools.product(
        [b.min.x, b.max.x], [b.min.y, b.max.y], [b.min.z, b.max.z])])
    v = (corners @ M.T)[:, :3] * .0254
    lo = v.min(0); hi = v.max(0); center = (lo + hi) / 2
    xy = R @ center[:2] + translation
    rows.append({'path': path, 'topGroup': group, 'sourceDefinitionName': n, 'classification': kind,
                 'sourceBoundsMinXYZ': lo.tolist(), 'sourceBoundsMaxXYZ': hi.tolist(),
                 'sourceCenterXYZ': center.tolist(),
                 'registeredCenterEastUpNorth': [float(xy[0]), float(center[2]), float(xy[1])],
                 'proofScope': 'Actual source named component and transformed bounds only; not clear opening, traversability or interior adjacency proof'})

def walk(ent, M, path, group, depth=0):
    global visited
    visited += 1
    if depth > 24: raise RuntimeError('Depth guard')
    for i, g in enumerate(children(ent, 'Groups')):
        child = Ref(); ge(g, C.byref(child)); t = Transform(); gt(g, C.byref(t))
        A = np.array(t.values).reshape(4, 4).T
        walk(child, M @ A, path + '/g' + str(i), group, depth + 1)
    for i, inst in enumerate(children(ent, 'Instances')):
        d = Ref(); definition(inst, C.byref(d)); child = Ref(); de(d, C.byref(child))
        t = Transform(); it(inst, C.byref(t)); A = np.array(t.values).reshape(4, 4).T
        if d.ptr not in names: names[d.ptr] = name(d)
        n = names[d.ptr]; p = path + '/i' + str(i); kind = classify(n)
        if kind: record(child, M @ A, p, n, kind, group)
        walk(child, M @ A, p, group, depth + 1)

# 원본과 달리 최상위 그룹을 전부 훑는다 — ver13 의 역사 루트 인덱스를 가정하지 않는다.
tops = children(e, 'Groups')
print('TOP_LEVEL_GROUPS', len(tops), flush=True)
for gi, g in enumerate(tops):
    root = Ref(); ge(g, C.byref(root)); t = Transform(); gt(g, C.byref(t))
    walk(root, np.array(t.values).reshape(4, 4).T, f'ROOT/g{gi}', gi)
    if gi % 10 == 0: print('GROUP', gi, len(tops), 'rows', len(rows), flush=True)

summary = {}
per_group = {}
for x in rows:
    summary[x['classification']] = summary.get(x['classification'], 0) + 1
    key = str(x['topGroup'])
    per_group.setdefault(key, {})
    per_group[key][x['classification']] = per_group[key].get(x['classification'], 0) + 1

report = {'scope': f'ALL top-level groups of {SKP_NAME} (원본은 group56 만 봤다)',
          'sourceFile': SKP_NAME, 'sourceBytes': SKP.stat().st_size,
          'topLevelGroups': len(tops), 'visitedEntityContainers': visited,
          'counts': summary, 'countsPerTopGroup': per_group, 'instances': rows,
          'registrationPath': str(SRC / 'station-rigid-registration.json'),
          'noWalkabilityClaim': True}
(OUT / f'{TAG}-interior-source-components.json').write_text(
    json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print('SUMMARY', summary, flush=True)
print('GROUPS_WITH_HITS', sorted(per_group.keys(), key=int), flush=True)
