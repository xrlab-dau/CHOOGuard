"""TEAM-06 round-trip pilot: author one existing module into an isolated output.

Run once per round, from the repository root:
  blender -b --factory-startup --python scripts/art/team/TEAM-06/pilot_module.py -- --run 1
  blender -b --factory-startup --python scripts/art/team/TEAM-06/pilot_module.py -- --run 2

The shared driver (scripts/art/build_station_assets.py) is never executed as a script: its
module-level tail rebuilds all 33 assets and rewrites the shared .blend, FBX folder and
manifest. Only its definitions (palette, helpers, save) are loaded, with OUT redirected to
this pilot's own folder. Inputs are refused unless their hashes match the baseline manifest.
"""
import ast
import hashlib
import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace

import bpy

ROOT = Path(__file__).resolve().parents[4]
DRIVER = ROOT / 'scripts/art/build_station_assets.py'
BASELINE = ROOT / 'foundation/art/asset-manifest.json'
MODULE_SOURCE = ROOT / 'scripts/art/station_furniture.py'
GENERATED = ROOT / 'Assets/CHOOguardTeam/Art/TEAM-06/Generated'
PILOT_MANIFEST = ROOT / 'foundation/art/team/TEAM-06/pilot-manifest.json'
MODULE = 'InformationKiosk'
ROOT_NODE = MODULE + '_Pilot'
LOD1_RATIO = 0.5
# Names or calls that would write shared outputs; none may appear in the loaded definitions
# except the FBX export inside save(), whose OUT is redirected before it can run.
FORBIDDEN_IN_DEFINITIONS = {'save_as_mainfile', 'write_text', 'write_bytes'}


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def rel(path):
    return Path(path).resolve().relative_to(ROOT).as_posix()


def parse_args():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    if len(argv) != 2 or argv[0] != '--run' or argv[1] not in {'1', '2'}:
        raise SystemExit('usage: blender -b --factory-startup --python pilot_module.py -- --run 1|2')
    return int(argv[1])


def qualify_inputs(baseline):
    """Existing native baseline branch of or:74:roundtrip-baseline: hashes must match."""
    checks = {
        'generatorSha256': (rel(DRIVER), sha256(DRIVER), baseline['generatorSha256']),
        'sourceModuleSha256': (rel(MODULE_SOURCE), sha256(MODULE_SOURCE),
                               baseline['sourceModuleSha256'][rel(MODULE_SOURCE)]),
    }
    entry = next(a for a in baseline['assets'] if a['id'] == MODULE)
    checks['assetSha256'] = (entry['file'], sha256(ROOT / entry['file']), entry['sha256'])
    failed = {k: v for k, v in checks.items() if v[1] != v[2]}
    if failed:
        raise RuntimeError('Baseline input does not qualify: ' + json.dumps(failed))
    if bpy.app.version_string.split(' ')[0] != baseline['blender'].split(' ')[0]:
        raise RuntimeError('Blender %s differs from baseline %s' % (bpy.app.version_string, baseline['blender']))
    return entry, {k: {'path': v[0], 'sha256': v[1]} for k, v in checks.items()}


def load_driver_definitions(out_dir):
    source = DRIVER.read_text(encoding='utf-8')
    tree = ast.parse(source)
    cut = next(i for i, node in enumerate(tree.body) if 'sys.path.insert' in ast.get_source_segment(source, node))
    body = tree.body[:cut]
    used = {n.attr for n in ast.walk(ast.Module(body=body, type_ignores=[])) if isinstance(n, ast.Attribute)}
    if used & FORBIDDEN_IN_DEFINITIONS:
        raise RuntimeError('Driver definitions now write shared outputs: ' + ', '.join(sorted(used & FORBIDDEN_IN_DEFINITIONS)))
    ns = {'__file__': str(DRIVER), '__name__': 'chooguard_driver_definitions'}
    exec(compile(ast.Module(body=body, type_ignores=[]), str(DRIVER), 'exec'), ns)
    ns['OUT'] = out_dir
    ns['SOURCE'] = None
    return ns


def driver_context(ns):
    # Same surface the driver hands to station_* builders; parts() reads the live global.
    names = ['start', 'save', 'box', 'pipe', 'curve', 'ellipsoid', 'frame', 'bolts', 'finish', 'v',
             'unit_face_uv', 'set_group', 'tilt']
    context = SimpleNamespace(**{name: ns[name] for name in names})
    context.parts = lambda: ns['parts']
    context.materials = ns['M']
    return context


def evaluated_triangles(obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    count = len(mesh.loop_triangles)
    evaluated.to_mesh_clear()
    return count


def build_roundtrip(ns, record, out_dir):
    """LOD0 = driver-merged meshes, LOD1 = decimated copies, colliders = authored boxes."""
    visual = sorted(bpy.data.collections[MODULE].objects, key=lambda o: o.name)
    root = bpy.data.objects.new(ROOT_NODE, None)
    bpy.context.scene.collection.objects.link(root)
    lods = {0: [], 1: []}
    for obj in visual:
        base_name = obj.name
        obj.name = base_name + '_LOD0'
        obj.parent = root
        lods[0].append(obj)
        copy = obj.copy()
        copy.data = obj.data.copy()
        copy.name = base_name + '_LOD1'
        bpy.context.scene.collection.objects.link(copy)
        copy.parent = root
        decimate = copy.modifiers.new('LOD1 decimate', 'DECIMATE')
        decimate.ratio = LOD1_RATIO
        lods[1].append(copy)
    colliders = []
    for box in record['collisionBoxes']:
        lo, hi = box['boundsUnity']['min'], box['boundsUnity']['max']
        centre = [(a + b) / 2 for a, b in zip(lo, hi)]
        size = [b - a for a, b in zip(lo, hi)]
        bpy.ops.mesh.primitive_cube_add(size=1, location=ns['v'](centre))
        collider = bpy.context.object
        collider.dimensions = (size[0], size[2], size[1])
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        collider.name = 'Collider_' + box['part']
        collider.data.materials.clear()
        collider.parent = root
        colliders.append({'name': collider.name, 'part': box['part'], 'component': box['component'],
                          'boundsUnity': ns['unity_bounds']([collider])})
    bpy.ops.object.select_all(action='DESELECT')
    for obj in [root] + lods[0] + lods[1]:
        obj.select_set(True)
    for collider in colliders:
        bpy.data.objects[collider['name']].select_set(True)
    bpy.context.view_layer.objects.active = root
    path = out_dir / (MODULE + '_RoundTrip.fbx')
    # Identical axis/unit options to the driver's export; EMPTY added only for the LOD parent.
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH', 'EMPTY'},
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, apply_unit_scale=True,
                             add_leaf_bones=False, bake_anim=False, use_mesh_modifiers=True,
                             mesh_smooth_type='FACE', path_mode='STRIP')

    def lod_stats(level):
        meshes = [{'name': o.name, 'material': o.data.materials[0].name, 'triangles': evaluated_triangles(o)}
                  for o in lods[level]]
        textures = sorted({node.image.name for o in lods[level] for m in o.data.materials if m and m.use_nodes
                           for node in m.node_tree.nodes if node.type == 'TEX_IMAGE' and node.image})
        return {'lod': level, 'triangles': sum(m['triangles'] for m in meshes), 'meshes': meshes,
                'materials': sorted({m['material'] for m in meshes}), 'textures': textures}

    return path, [lod_stats(0), lod_stats(1)], colliders


def git_head():
    try:
        return subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=ROOT, capture_output=True, text=True,
                              check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return None


def main():
    run = parse_args()
    baseline = json.loads(BASELINE.read_text(encoding='utf-8'))
    entry, qualified = qualify_inputs(baseline)
    out_dir = GENERATED / ('run%d' % run)
    out_dir.mkdir(parents=True, exist_ok=True)
    ns = load_driver_definitions(out_dir)
    sys.path.insert(0, str(MODULE_SOURCE.parent))
    import station_furniture
    station_furniture._kiosk(driver_context(ns))
    record = ns['assets'][-1]
    baseline_fields = ['triangles', 'meshParts', 'boundsUnity', 'components', 'collisionBoxes']
    baseline_match = {field: record[field] == entry[field] for field in baseline_fields}
    reference_fbx = out_dir / (MODULE + '.fbx')
    roundtrip_fbx, lods, colliders = build_roundtrip(ns, record, out_dir)
    if lods[0]['triangles'] != record['triangles']:
        raise RuntimeError('LOD0 triangles %d differ from driver record %d' % (lods[0]['triangles'], record['triangles']))

    manifest = json.loads(PILOT_MANIFEST.read_text(encoding='utf-8')) if PILOT_MANIFEST.exists() else {}
    manifest.update({
        'workId': 'TEAM-06', 'issue': 74, 'phase': 'candidate', 'module': MODULE,
        'contract': {
            'coordinateAdapter': baseline['coordinateAdapter'], 'units': baseline['units'],
            'lodNaming': '<mesh>_LOD<n> under ' + ROOT_NODE, 'lod1': 'Blender Decimate collapse ratio %.2f' % LOD1_RATIO,
            'colliderNaming': 'Collider_<part>, no material, authored from driver collisionBoxes',
            'outputs': 'Assets/CHOOguardTeam/Art/TEAM-06/Generated/run<n>/ only; shared blend/FBX/manifest read-only',
        },
    })
    manifest['inputs'] = {
        'branch': 'or:74:roundtrip-baseline -> artifact:65:existing-native-33-baseline:candidate',
        'baselineManifest': {'path': rel(BASELINE), 'sha256': sha256(BASELINE)},
        'sourceBlend': {'path': baseline['source'], 'sha256': sha256(ROOT / baseline['source']), 'use': 'read-only, not opened'},
        'qualified': qualified, 'baseRef': git_head(),
    }
    runs = manifest.setdefault('blender', {}).setdefault('runs', {})
    runs[str(run)] = {
        'capturedAt': datetime.now(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ'),
        'blender': bpy.app.version_string,
        'invocation': 'blender -b --factory-startup --python %s -- --run %d' % (rel(__file__), run),
        'pilotSourceSha256': sha256(__file__),
        'outputs': {rel(p): {'sha256': sha256(p), 'bytes': p.stat().st_size} for p in (reference_fbx, roundtrip_fbx)},
        'lod0DriverRecord': {field: record[field] for field in baseline_fields},
        'baselineMatch': baseline_match,
        'objectNames': sorted(o.name for o in bpy.data.objects[ROOT_NODE].children),
        'pivot': {'root': list(bpy.data.objects[ROOT_NODE].location), 'meshOrigins': 'world origin (driver ORIGIN_CURSOR)'},
        'unitScaleLength': bpy.context.scene.unit_settings.scale_length,
        'lods': lods, 'colliders': colliders,
    }
    if {'1', '2'} <= runs.keys():
        a, b = runs['1'], runs['2']
        semantic = ['lod0DriverRecord', 'objectNames', 'pivot', 'unitScaleLength', 'lods', 'colliders']
        manifest['blender']['comparison'] = {
            'semanticFieldsEqual': {field: a[field] == b[field] for field in semantic},
            'outputBytesEqual': {Path(p).name: a['outputs'][p]['sha256'] == b['outputs'][q]['sha256']
                                 for p, q in zip(sorted(a['outputs']), sorted(b['outputs']))},
        }
    PILOT_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    PILOT_MANIFEST.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print('TEAM06_PILOT ' + json.dumps({'run': run, 'baselineMatch': baseline_match,
                                         'lodTriangles': [l['triangles'] for l in lods], 'colliders': len(colliders)}))


main()
