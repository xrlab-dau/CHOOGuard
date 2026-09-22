"""부산역ver13.skp 변환 — 2026-09-21 convert_station_batched.py 의 입출력 경로만 바꾼 사본.

왜 필요한가: 공식 배포 zip 에 SKP 이 2개인데 지금까지 `부산역(수정).skp`(65.7MB, 2025-12) 만
변환·감사됐다. `부산역ver13.skp`(100.7MB, 2026-03)는 `grep -rl "ver13"` 0건으로 한 번도 열리지 않았다.
'내부 없음' 판정은 전부 작은 쪽 파일에만 해당한다.

변환 로직은 한 줄도 바꾸지 않는다 — 두 파일을 같은 절차로 처리해야 비교가 성립한다.
"""
import bpy, sys, pathlib, json, time
from mathutils import Matrix
from collections import defaultdict

ROOT = pathlib.Path.cwd()
SRC = ROOT / 'asset-library/research-public/2026-09-21/quality-a/station-model-source'
OUT = ROOT / 'asset-library/research-public/2026-09-22/ver13-audit'
OUT.mkdir(parents=True, exist_ok=True)
SKP = SRC / 'station-extracted/부산역ver13.skp'

sys.path.insert(0, str(SRC / 'importer-isolated'))
import sketchup_importer as si
si.MIN_LOGS = True

bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
model = si.sketchup.Model.from_file(str(SKP))
imp = si.SceneImporter().set_filename(str(SKP))
imp.context = bpy.context; imp.reuse_material = True; imp.write_materials(model.materials)
(OUT / 'ver13-source-cameras.json').write_text(json.dumps(
    [{'name': x.name, 'orientation': x.camera.GetOrientation(), 'fov': x.camera.fov} for x in model.scenes],
    ensure_ascii=False, indent=2))

cache = {}; serial = 0; placements = []; stats = defaultdict(int)

def matname(entity, default):
    m = entity.material; return m.name if m else default

def layername(entity, inherited):
    n = entity.layer.name; return n if n != 'Layer0' else inherited

def walk(entities, key, mat='Material', layer='Layer0', depth=0):
    global serial
    if depth > 30: raise RuntimeError('Unexpected depth')
    result = []; mesh, _ = imp.write_mesh_data(entities, key, mat)
    if mesh: result.append((mesh, Matrix.Identity(4), layer))
    for j, g in enumerate(entities.groups):
        if g.hidden: continue
        for me, t, la in walk(g.entities, key + '/g' + str(j), matname(g, mat), layername(g, layer), depth + 1):
            result.append((me, Matrix(g.transform) @ t, la))
    for inst in entities.instances:
        if inst.hidden: continue
        definition = inst.definition; im = matname(inst, mat); la = layername(inst, layer)
        ck = (definition.name, im, la)
        if ck not in cache:
            cache[ck] = walk(definition.entities, 'C:' + definition.name + ':' + im + ':' + la, im, la, depth + 1)
        for me, t, l in cache[ck]: result.append((me, Matrix(inst.transform) @ t, l))
    return result

t0 = time.time(); placements = walk(model.entities, 'ROOT')
print('PROTOTYPES', len(placements), 'seconds', time.time() - t0, flush=True)

allm = list(bpy.data.materials); midx = {m.name: i for i, m in enumerate(allm)}
buckets = {}; chunks = defaultdict(int); rows = []

def flush(layer):
    b = buckets.pop(layer, None)
    if not b or not b['faces']: return
    name = 'OfficialStation_' + layer + '_' + str(chunks[layer]); chunks[layer] += 1
    me = bpy.data.meshes.new(name); me.from_pydata(b['verts'], [], b['faces']); me.update()
    used = sorted(set(b['mats'])); local = {g: i for i, g in enumerate(used)}
    for i in used: me.materials.append(allm[i])
    me.polygons.foreach_set('material_index', [local[i] for i in b['mats']])
    me.polygons.foreach_set('use_smooth', b['smooth'])
    uv = me.uv_layers.new(name='UVMap'); uv.data.foreach_set('uv', b['uv'])
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o); o['source_layer'] = layer
    pts = b['verts']
    lo = [min(p[k] for p in pts) for k in range(3)]; hi = [max(p[k] for p in pts) for k in range(3)]
    rows.append({'name': name, 'layer': layer, 'verts': len(pts), 'triangles': len(b['faces']), 'min': lo, 'max': hi})

for index, (me, t, layer) in enumerate(placements):
    b = buckets.setdefault(layer, {'verts': [], 'faces': [], 'mats': [], 'uv': [], 'smooth': []})
    offset = len(b['verts']); b['verts'].extend(tuple(t @ v.co) for v in me.vertices)
    uv = me.uv_layers.active; reverse = t.determinant() < 0
    for p in me.polygons:
        ids = list(p.vertices); loops = list(p.loop_indices)
        if reverse: ids.reverse(); loops.reverse()
        b['faces'].append(tuple(offset + i for i in ids))
        b['mats'].append(midx[me.materials[p.material_index].name]); b['smooth'].append(p.use_smooth)
        for li in loops: b['uv'].extend(uv.data[li].uv if uv else (0, 0))
    if len(b['verts']) > 60000: flush(layer)
    if index % 1000 == 0: print('BATCH', index, len(placements), flush=True)
for layer in list(buckets): flush(layer)

for me in list(bpy.data.meshes):
    if me.users == 0: bpy.data.meshes.remove(me)
s = bpy.context.scene; s.unit_settings.system = 'METRIC'; s.unit_settings.scale_length = 1
lo = [min(x['min'][k] for x in rows) for k in range(3)]
hi = [max(x['max'][k] for x in rows) for k in range(3)]
manifest = {
    'status': 'IMPORTED_BATCHED_SOURCE_GEOMETRY',
    'sourceFile': '부산역ver13.skp',
    'sourceBytes': SKP.stat().st_size,
    'sourceUnits': 'SKP internal inches converted by official-SDK binding inch_to_meter(0.0254), no arbitrary rescale',
    'materials': len(allm), 'meshChunks': len(rows),
    'vertices': sum(x['verts'] for x in rows), 'triangles': sum(x['triangles'] for x in rows),
    'bounds': {'min': lo, 'max': hi, 'size': [hi[k] - lo[k] for k in range(3)]},
    'layers': dict(chunks), 'chunks': rows,
    'componentPrototypes': len(cache), 'meshPlacements': len(placements),
    'seconds': time.time() - t0,
    'optimization': 'Original visible faces retained; merged per source layer and <=~60000 vertex chunks, shared materials and UV retained; no decimation',
    'interiorStatus': 'Requires rendered isolation and floor probes, not established by layer names',
}
(OUT / 'ver13-batched-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'ver13-batched.blend'))
print('DONE', manifest['bounds'], 'triangles', manifest['triangles'], flush=True)
