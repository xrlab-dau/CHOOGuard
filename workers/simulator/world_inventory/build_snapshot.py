#!/usr/bin/env python3
"""Freeze the saved Unity world as inert geometry and typed, allowlisted data.

Reads original bytes only. Never imports Unity, evaluates scripts, or reconstructs
historical scene state. Matrices are row-major and operate on column vectors.
"""
from __future__ import annotations

import argparse
from collections import Counter, deque
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import re
import shutil
import sys
import time

import yaml

SCENE = "Assets/ChooGuard/Scenes/FpsStation.unity"
SAFE_TYPES = {1, 4, 23, 33, 64, 65, 108, 205, 1660057539}
GEOMETRY_TYPES = {23, 33, 64, 65, 108, 205}
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)([^\n]*)\n", re.M)
GUID = re.compile(rb"\bguid:\s*([0-9a-fA-F]{32})\b")
REF = re.compile(r"\{fileID:\s*(-?\d+)(?:, guid:\s*([0-9a-f]{32}), type:\s*(\d+))?\}")
BUILTIN = re.compile(r"^0{16}[def]0{15}$")
IDENTITY = [1., 0., 0., 0., 0., 1., 0., 0., 0., 0., 1., 0., 0., 0., 0., 1.]
TEXT_SUFFIXES = {'.asset', '.mat', '.prefab', '.unity', '.meta', '.shader', '.shadergraph', '.shadersubgraph', '.hlsl', '.cginc', '.compute', '.json', '.uss', '.uxml', '.lighting'}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def save_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + '\n')


def stable_read(path, attempts=4):
    for _ in range(attempts):
        before = path.stat()
        data = path.read_bytes()
        after = path.stat()
        if before.st_size == after.st_size == len(data) and before.st_mtime_ns == after.st_mtime_ns:
            if sha(data) == sha(path.read_bytes()):
                return data
        time.sleep(.05)
    raise RuntimeError(f"Source changed during stable read: {path}")


def documents(data):
    text = data.decode('utf-8-sig')
    heads = list(HEADER.finditer(text))
    docs = {}
    for n, h in enumerate(heads):
        class_id, file_id, suffix = int(h[1]), h[2], h[3]
        if 'stripped' in suffix or class_id == 1001:
            raise ValueError("Prefab instances/stripped documents require native resolved extraction; refusing incomplete inventory")
        if file_id in docs:
            raise ValueError(f"Duplicate scene fileID {file_id}")
        raw = text[h.end():heads[n + 1].start() if n + 1 < len(heads) else len(text)]
        parsed = yaml.load(raw, Loader=yaml.CSafeLoader)
        type_name, fields = next(iter(parsed.items()))
        docs[file_id] = {'fileId': file_id, 'classId': class_id, 'type': type_name, 'fields': fields, 'raw': raw}
    return docs


def vector(value, axes='xyz', default=0.):
    value = value or {}
    return [float(value.get(k, default)) for k in axes]


def normalize(q):
    length = math.sqrt(sum(v * v for v in q))
    if length < 1e-12:
        raise ValueError('Zero rotation quaternion')
    return [v / length for v in q]


def qmul(a, b):
    x,y,z,w = a; X,Y,Z,W = b
    return normalize([w*X+x*W+y*Z-z*Y, w*Y-x*Z+y*W+z*X, w*Z+x*Y-y*X+z*W, w*W-x*X-y*Y-z*Z])


def trs(t):
    x,y,z,w = normalize(t['rotation']); sx,sy,sz = t['scale']; px,py,pz = t['position']
    return [(1-2*(y*y+z*z))*sx,2*(x*y-z*w)*sy,2*(x*z+y*w)*sz,px,
            2*(x*y+z*w)*sx,(1-2*(x*x+z*z))*sy,2*(y*z-x*w)*sz,py,
            2*(x*z-y*w)*sx,2*(y*z+x*w)*sy,(1-2*(x*x+y*y))*sz,pz,0.,0.,0.,1.]


def multiply(a, b):
    return [sum(a[r*4+k] * b[k*4+c] for k in range(4)) for r in range(4) for c in range(4)]


def world_bounds(local, matrix):
    if not local:
        return None
    center, size = local['center'], local['size']
    out_center = [sum(matrix[r*4+c] * center[c] for c in range(3)) + matrix[r*4+3] for r in range(3)]
    extent = [sum(abs(matrix[r*4+c]) * size[c] / 2 for c in range(3)) for r in range(3)]
    return {'min': [out_center[i]-extent[i] for i in range(3)], 'max': [out_center[i]+extent[i] for i in range(3)]}


def index_guids(root):
    index, duplicate = {}, {}
    # PackageCache is inspected for package GUID resolution only, never copied wholesale.
    for base in [root/'Assets', root/'Packages', root/'Library/PackageCache']:
        if not base.exists():
            continue
        for p in base.rglob('*.meta'):
            try:
                m = GUID.search(p.read_bytes())
            except OSError:
                continue
            if not m:
                continue
            guid = m[1].decode().lower(); asset = p.with_suffix('')
            if not asset.exists() or asset.is_dir():
                continue
            rel = asset.relative_to(root).as_posix()
            if rel.startswith('Library/PackageCache/'):
                package, _, remainder = rel[len('Library/PackageCache/'):].partition('/')
                rel = 'Packages/' + package.split('@')[0] + '/' + remainder
            if guid in index and index[guid]['path'] != rel:
                duplicate.setdefault(guid, [index[guid]['path']]).append(rel)
                continue
            index[guid] = {'path': rel, 'sourcePath': asset, 'metaPath': p}
    return index, duplicate


def asset_ref(value, index):
    if not isinstance(value, dict):
        return None
    guid = value.get('guid')
    if guid is not None:
        guid = (f'{guid:032d}' if isinstance(guid, int) else str(guid)).lower()
    return {'fileId': str(value.get('fileID', 0)), 'guid': guid,
            'path': index.get(guid, {}).get('path'), 'referenceType': value.get('type'),
            'builtin': bool(guid and BUILTIN.fullmatch(guid))}


def serialized_bounds(ref, index, cache):
    if not ref or ref['fileId'] == '0' or not ref.get('guid'):
        return None
    key = (ref['guid'], ref['fileId'])
    if key in cache:
        return cache[key]
    row = index.get(ref['guid']); bounds = None
    if row and row['sourcePath'].suffix == '.asset':
        data = stable_read(row['sourcePath'])
        if data.startswith(b'%YAML'):
            for doc in documents(data).values():
                if doc['fileId'] == ref['fileId'] and doc['type'] == 'Mesh':
                    b = doc['fields'].get('m_LocalAABB')
                    if b:
                        bounds = {'center': vector(b.get('m_Center')), 'size': [2*x for x in vector(b.get('m_Extent'))]}
    cache[key] = bounds
    return bounds


def component(doc, index, matrix, bounds_cache):
    f = doc['fields']; c = {'fileId': doc['fileId'], 'classId': doc['classId'], 'type': doc['type'],
                            'enabled': bool(f['m_Enabled']) if 'm_Enabled' in f else None}
    typ = doc['type']
    if typ == 'MonoBehaviour':
        # Type identity only. Never serialize arbitrary user fields, paths, tokens, or settings.
        c['script'] = asset_ref(f.get('m_Script'), index)
        c['support'] = 'typed-unsupported-behaviour-omitted-from-geometry'
    elif typ in {'MeshFilter', 'MeshCollider'}:
        c['mesh'] = asset_ref(f.get('m_Mesh'), index)
    if typ in {'MeshCollider', 'BoxCollider'}:
        c.update(isTrigger=bool(f.get('m_IsTrigger')), material=asset_ref(f.get('m_Material'), index))
        if typ == 'BoxCollider':
            c.update(center=vector(f.get('m_Center')), size=vector(f.get('m_Size')))
            local = {'center': c['center'], 'size': c['size']}; evidence = 'serialized-box'
        else:
            c['convex'] = bool(f.get('m_Convex'))
            local = serialized_bounds(c['mesh'], index, bounds_cache)
            evidence = 'serialized-mesh-aabb' if local else 'requires-native-import'
        c.update(localBounds=local, worldBounds=world_bounds(local, matrix), boundsEvidence=evidence)
    if typ == 'MeshRenderer':
        c.update(materials=[asset_ref(x, index) for x in f.get('m_Materials', [])], castShadows=f.get('m_CastShadows'), receiveShadows=bool(f.get('m_ReceiveShadows')))
    if typ == 'LODGroup':
        c.update(localReferencePoint=vector(f.get('m_LocalReferencePoint')), size=f.get('m_Size'),
                 lods=[{'screenRelativeHeight': x.get('screenRelativeHeight'), 'fadeTransitionWidth': x.get('fadeTransitionWidth'),
                        'renderers': [str(y['renderer']['fileID']) for y in x.get('renderers', [])]} for x in f.get('m_LODs', [])])
    if typ == 'Light':
        c.update(lightType=f.get('m_Type'), color=vector(f.get('m_Color'), 'rgba'), intensity=f.get('m_Intensity'), range=f.get('m_Range'))
    return c


def build_inventory(docs, scene_guid, scene_hash, index, captured):
    gos = {k:v for k,v in docs.items() if v['classId'] == 1}
    transforms = {k:v for k,v in docs.items() if v['classId'] == 4}
    by_go = {str(v['fields']['m_GameObject']['fileID']): v for v in transforms.values()}
    if len(by_go) != len(gos):
        raise ValueError('Expected exactly one Transform per GameObject')
    component_owners={}
    for go_id,go in gos.items():
        for ref in go['fields'].get('m_Component',[]):
            cid=str(ref['component']['fileID'])
            if cid in component_owners or cid not in docs:
                raise ValueError(f'Duplicate or dangling component {cid}')
            component_owners[cid]=go_id
            if str(docs[cid]['fields'].get('m_GameObject',{}).get('fileID'))!=go_id:
                raise ValueError(f'Component GameObject ownership mismatch {cid}')
    expected_children={k:[] for k in transforms}
    for tid,td in transforms.items():
        parent=str(td['fields']['m_Father']['fileID'])
        if parent!='0':
            if parent not in transforms: raise ValueError(f'Dangling parent Transform {parent}')
            expected_children[parent].append(tid)
    for tid,td in transforms.items():
        actual=[str(v['fileID']) for v in td['fields'].get('m_Children',[])]
        if sorted(actual)!=sorted(expected_children[tid]): raise ValueError(f'Child/parent hierarchy mismatch {tid}')
    made, visiting, bounds_cache = {}, set(), {}
    def make(go_id):
        if go_id in made:
            return made[go_id]
        if go_id in visiting:
            raise ValueError('Hierarchy cycle')
        visiting.add(go_id)
        go = gos[go_id]; f = go['fields']; td = by_go[go_id]; t = td['fields']
        parent_transform = str(t['m_Father']['fileID'])
        parent_go = str(transforms[parent_transform]['fields']['m_GameObject']['fileID']) if parent_transform != '0' else None
        parent = make(parent_go) if parent_go else None
        local = {'position': vector(t.get('m_LocalPosition')), 'rotation': normalize(vector(t.get('m_LocalRotation'), 'xyzw')), 'scale': vector(t.get('m_LocalScale'), default=1.)}
        wm = multiply(parent['worldTransform']['localToWorldMatrix4x4'], trs(local)) if parent else trs(local)
        rotation = qmul(parent['worldTransform']['rotation'], local['rotation']) if parent else local['rotation']
        name = str(f['m_Name']); components = []
        for r in f.get('m_Component', []):
            cid = str(r['component']['fileID'])
            if cid not in docs:
                raise ValueError(f'Dangling component {cid} on GameObject {go_id}')
            components.append(component(docs[cid], index, wm, bounds_cache))
        active = bool(f['m_IsActive']); hierarchy_active = active and (parent['activeInHierarchy'] if parent else True)
        result = {'id': f'unity:{scene_guid}:{go_id}', 'sceneFileId': go_id, 'transformFileId': td['fileId'],
                  'parentId': parent['id'] if parent else None, 'name': name, 'path': (parent['path']+'/' if parent else '')+name,
                  'activeSelf': active, 'activeInHierarchy': hierarchy_active,
                  'physicalEligible': hierarchy_active and any(c['type'] in {'MeshCollider','BoxCollider'} and c['enabled'] and not c['isTrigger'] for c in components),
                  'layer': f.get('m_Layer', 0), 'tag': f.get('m_TagString', 'Untagged'),
                  'localTransform': local,
                  'worldTransform': {'position': [wm[3],wm[7],wm[11]], 'rotation': rotation,
                                     'lossyScale': [math.sqrt(sum(wm[r*4+c]**2 for r in range(3))) for c in range(3)],
                                     'localToWorldMatrix4x4': wm},
                  'components': components, 'geometryProvenance': {'mode': 'typed-only', 'sourceSceneSha256': scene_hash},
                  'sourceBindings': []}
        made[go_id] = result; visiting.remove(go_id); return result
    objects = [make(k) for k in sorted(gos, key=int)]
    roots = [x for x in objects if x['parentId'] is None]
    inventory = {'schemaVersion': 'chooguard.world-objects.v1',
        'sourceSnapshot': {'scenePath': SCENE, 'sceneGuid': scene_guid, 'sceneSha256': scene_hash, 'snapshotId': scene_hash[:16], 'capturedAtUtc': captured},
        'frame': {'coordinateSystem': 'Unity left-handed Y-up', 'matrixOrder': 'row-major-column-vector', 'sourceToSimulationMatrix4x4': IDENTITY,
                  'registration': 'preserved-saved-scene', 'metricClaim': 'saved model units; not an independent site survey',
                  'lossyScaleConvention': 'nonnegative matrix column magnitudes; matrix retains reflection/shear'},
        'objects': objects, 'sourceBindings': [], 'sourceRoots': [{k:r[k] for k in ('id','sceneFileId','name','path','activeSelf','activeInHierarchy','localTransform','worldTransform')} for r in roots]}
    inventory['counts'] = {'objects': len(objects), 'activeSelf': sum(x['activeSelf'] for x in objects),
        'activeInHierarchy': sum(x['activeInHierarchy'] for x in objects), 'physicalEligible': sum(x['physicalEligible'] for x in objects),
        'componentTypes': dict(sorted(Counter(c['type'] for o in objects for c in o['components']).items()))}
    return inventory


def stripped_scene(docs):
    chunks = ['%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n']
    for fid, d in docs.items():
        if d['classId'] not in SAFE_TYPES:
            continue
        raw = d['raw']
        if d['classId'] == 1:
            raw = re.sub(r'^  - component: \{fileID: (-?\d+)\}\n', lambda m: m[0] if docs[m[1]]['classId'] in SAFE_TYPES else '', raw, flags=re.M)
        chunks.append(f"--- !u!{d['classId']} &{fid}\n" + raw)
    return ''.join(chunks).encode()


def dependency_closure(root, scene_bytes, index, pack):
    seeds = sorted(set(g.decode().lower() for g in GUID.findall(scene_bytes)))
    queue = deque((g, 'geometry-scene') for g in seeds); visited = set(); files = {}; unresolved = {}; builtins = set(); unsafe = []; prerequisites = []
    def add_file(source, target, via):
        if target in files:
            return
        data = stable_read(source)
        dest = pack/target; dest.parent.mkdir(parents=True, exist_ok=True); dest.write_bytes(data)
        if sha(stable_read(source)) != sha(data):
            raise RuntimeError(f'Source changed while copying closure: {source}')
        files[target] = {'path': target, 'sha256': sha(data), 'bytes': len(data), 'sourcePath': source.relative_to(root).as_posix(), 'via': via}
        if source.suffix.lower() in TEXT_SUFFIXES:
            for g in GUID.findall(data):
                queue.append((g.decode().lower(), target))
            if source.suffix.lower() in {'.shader','.hlsl','.cginc','.compute'}:
                text = data.decode('utf-8', errors='replace')
                for inc in re.findall(r'^\s*#\s*include(?:_with_pragmas)?\s+["<]([^">]+)[">]', text, re.M):
                    if inc.startswith('Packages/'):
                        parts = inc.split('/'); package = parts[1]
                        choices = list((root/'Library/PackageCache').glob(package+'@*'))
                        candidate = root/inc
                        if not candidate.exists() and choices:
                            candidate = choices[0].joinpath(*parts[2:])
                        inc_target = inc
                    else:
                        candidate = source.parent/inc; inc_target = str(Path(target).parent/inc)
                    if candidate.exists() and candidate.is_file():
                        add_file(candidate, Path(inc_target).as_posix(), target)
                    # Built-in Unity includes (e.g. UnityCG.cginc) are engine resources.
    while queue:
        guid, via = queue.popleft()
        if guid in visited:
            continue
        visited.add(guid)
        if guid == '0'*32 or BUILTIN.fullmatch(guid):
            builtins.add(guid); continue
        row = index.get(guid)
        if not row:
            unresolved.setdefault(guid, []).append(via); continue
        source = row['sourcePath']; target = row['path']
        if source.suffix.lower() in {'.cs','.dll','.exe','.js','.py','.sh','.unity'}:
            if target.startswith('Packages/com.unity.'):
                prerequisites.append({'guid': guid, 'path': target, 'via': via, 'sha256': sha(stable_read(source)),
                                      'policy': 'official-engine-package-prerequisite-not-copied'})
            else:
                unsafe.append({'guid': guid, 'path': target, 'via': via})
            continue
        add_file(source, target, via)
        add_file(row['metaPath'], target+'.meta', via)
    return {'schemaVersion': 'chooguard.geometry-closure.v1', 'seedGuids': seeds, 'visitedGuids': sorted(visited),
            'builtinGuids': sorted(builtins), 'unresolvedGuids': unresolved, 'excludedExecutableDependencies': unsafe,
            'packagePrerequisites': prerequisites, 'files': sorted(files.values(), key=lambda x:x['path']), 'totalBytes': sum(x['bytes'] for x in files.values())}


def bind_platform_source(inventory, root):
    """Reuse only exact, explicit IDs from the existing source contract; grant no capabilities."""
    source_path = 'workers/simulator/data/platform-source.json'
    data = stable_read(root/source_path); source = json.loads(data)
    coverage_path = 'workers/simulator/data/semantic-coverage.json'
    coverage_bytes = stable_read(root/coverage_path); coverage=json.loads(coverage_bytes)
    targets={e['id'] for e in coverage['physical_entities']}
    bypath = {o['path']:o for o in inventory['objects']}
    bindings = []
    def bind(path, entity, relation):
        if entity not in targets:
            return
        obj = bypath.get(path)
        if obj is None:
            raise ValueError(f'Explicit platform-source binding absent from saved scene: {path}')
        row = {'entityId':entity, 'objectId':obj['id'], 'relation':relation, 'capabilities':[],
               'evidence':{'path':source_path,'sha256':sha(data),'kind':'explicit-existing-platform-source-path',
                           'sourceSceneSha256':inventory['sourceSnapshot']['sceneSha256'], 'sceneFileId':obj['sceneFileId'],
                           'coveragePath':coverage_path,'coverageSha256':sha(coverage_bytes)}}
        bindings.append(row); obj['sourceBindings'].append(row)
    bind(source['train']['root'], 'train:ktx', 'alias')
    for car in source['train']['cars']:
        path = source['train']['root']+'/'+car['name']
        bind(path, 'car:'+str(car['number']), 'alias')
        for leaf in car['door']['leaves']:
            bind(path+'/'+leaf, 'door:'+str(car['number']), 'part-of')
    inventory['sourceBindings'] = bindings


def alias_analysis(inventory, root):
    objects=inventory['objects']; result=[]
    specs=[('FPSWorld/KTXSource','Assets/ChooGuard/Art/Fps/KTX/ktx-single-10car-left-open.fbx','ktx'),
           ('FPSWorld/공식 자료 부산역 역사/PlatformsParking','Assets/ChooGuard/Art/OfficialBusanStation/OfficialBusanStation_PlatformsParking.fbx','platforms-parking'),
           ('FPSWorld/공식 자료 부산역 역사/Layer0PlatformsDetails','Assets/ChooGuard/Art/OfficialBusanStation/OfficialBusanStation_Layer0PlatformsDetails.fbx','platform-details')]
    staged=root/'output/simulator/platform-ktx-unity'; manifest_path=staged/'Assets/Simulator/Resources/SourceManifest.json'
    for prefix,asset_path,kind in specs:
        rows=[o for o in objects if o['path']==prefix or o['path'].startswith(prefix+'/')]; r=next(o for o in rows if o['path']==prefix)
        source_asset=stable_read(root/asset_path); staged_asset=staged/asset_path
        meshrows=[{'objectId':o['id'],'sceneFileId':o['sceneFileId'],'path':o['path'],'relativePath':o['path'][len(prefix):].lstrip('/'),
                   'activeInHierarchy':o['activeInHierarchy'],'localTransform':o['localTransform'],'worldMatrix4x4':o['worldTransform']['localToWorldMatrix4x4'],
                   'mesh':c['mesh'],'materials':next((x['materials'] for x in o['components'] if x['type']=='MeshRenderer'),[]),
                   'savedDerivative':c['mesh']['path']!=asset_path} for o in rows for c in o['components'] if c['type']=='MeshFilter']
        prepared_path=staged/'Assets/Simulator/Prepared'/Path(asset_path).with_suffix('.prefab').name
        prepared_summary=None
        if prepared_path.exists():
            pd=documents(stable_read(prepared_path)); pg={k:d for k,d in pd.items() if d['classId']==1}; pt={k:d for k,d in pd.items() if d['classId']==4}
            pbygo={str(d['fields']['m_GameObject']['fileID']):d for d in pt.values()}
            pname={k:str(d['fields']['m_Name']) for k,d in pg.items()}; prepared=[]
            for d in pd.values():
                if d['type']!='MeshFilter': continue
                go=str(d['fields']['m_GameObject']['fileID']); tf=pbygo[go]['fields']
                prepared.append({'name':pname[go],'mesh':{'fileId':str(d['fields']['m_Mesh']['fileID']),'guid':d['fields']['m_Mesh'].get('guid')},
                    'localTransform':{'position':vector(tf.get('m_LocalPosition')),'rotation':normalize(vector(tf.get('m_LocalRotation'),'xyzw')),'scale':vector(tf.get('m_LocalScale'),default=1.)}})
            differences=[]
            for p in prepared:
                matches=[m for m in meshrows if m['path'].split('/')[-1]==p['name']]
                if len(matches)!=1: differences.append({'name':p['name'],'reason':'saved-node-count-differs','savedMatches':len(matches)}); continue
                m=matches[0]
                if any(str(m['mesh'].get(k))!=str(p['mesh'].get(k)) for k in ('guid','fileId')): differences.append({'name':p['name'],'reason':'saved-mesh-reference-differs','sceneFileId':m['sceneFileId']})
                if m['localTransform']!=p['localTransform']: differences.append({'name':p['name'],'reason':'saved-local-transform-differs','sceneFileId':m['sceneFileId']})
            prepared_summary={'path':str(prepared_path.relative_to(root)),'sha256':sha(stable_read(prepared_path)),
                              'objects':len(pg),'meshFilters':len(prepared),'differences':differences}
        result.append({'kind':kind,'savedRootId':r['id'],'savedRootSceneFileId':r['sceneFileId'],'savedRootPath':prefix,
            'savedRootLocalTransform':r['localTransform'],'savedRootWorldTransform':r['worldTransform'], 'savedObjects':len(rows),
            'savedMeshFilters':len(meshrows),'savedActiveMeshFilters':sum(m['activeInHierarchy'] for m in meshrows),
            'sourceAssetPath':asset_path,'sourceAssetSha256':sha(source_asset),
            'stagedAssetSha256':sha(stable_read(staged_asset)) if staged_asset.exists() else None,
            'sourceAndStagedAssetBytesEqual':staged_asset.exists() and sha(source_asset)==sha(stable_read(staged_asset)),
            'sourceAndStagedMetaBytesEqual':staged_asset.exists() and stable_read(root/(asset_path+'.meta'))==stable_read(Path(str(staged_asset)+'.meta')),
            'wholeSubtreeAliasQualified':False,'wholeSubtreeAliasReason':'Saved scene contains current overrides/additions/active flags absent from native source FBX; require exact per-object native match before excluding.',
            'preparedPrefabComparison':prepared_summary,'meshRows':meshrows})
    return {'schemaVersion':'chooguard.world-runtime-alias-analysis.v1','sourceSceneSha256':inventory['sourceSnapshot']['sceneSha256'],
        'sourceManifest':{'path':str(manifest_path.relative_to(root)), 'sha256':sha(stable_read(manifest_path))} if manifest_path.exists() else None,
        'identityFramePreserved':True,'qualifiedWholeSubtreeExclusions':[], 'roots':result,
        'contract':'Every saved object stays in inventory. Native/runtime alias requires exact source mesh GUID/fileID, world matrix, material refs and active state. Current generated derivatives and user inactivity cannot be replaced by native FBX history. Production door aperture geometry requires explicit integration ownership.'}


def validate_inventory(inventory):
    objects = inventory['objects']; byid = {o['id']:o for o in objects}; bycid = {c['fileId']:c for o in objects for c in o['components']}; errors=[]
    if len(byid) != len(objects): errors.append('duplicate-object-id')
    for o in objects:
        parent = byid.get(o['parentId'])
        if o['parentId'] and parent is None: errors.append('dangling-parent:'+o['id'])
        expected_active = o['activeSelf'] and (parent['activeInHierarchy'] if parent else True)
        if o['activeInHierarchy'] != expected_active: errors.append('active-hierarchy:'+o['id'])
        expected_matrix = multiply(parent['worldTransform']['localToWorldMatrix4x4'], trs(o['localTransform'])) if parent else trs(o['localTransform'])
        if any(abs(a-b)>1e-8*(1+abs(b)) for a,b in zip(o['worldTransform']['localToWorldMatrix4x4'],expected_matrix)): errors.append('world-matrix:'+o['id'])
        physical = expected_active and any(c['type'] in {'MeshCollider','BoxCollider'} and c['enabled'] and not c['isTrigger'] for c in o['components'])
        if physical != o['physicalEligible']: errors.append('physical-flag:'+o['id'])
        for c in o['components']:
            if c['type'] == 'LODGroup':
                for lod in c['lods']:
                    for r in lod['renderers']:
                        if r != '0' and (r not in bycid or bycid[r]['type'] != 'MeshRenderer'): errors.append('dangling-lod:'+r)
    return errors


def build(root, output):
    captured = datetime.now(timezone.utc).isoformat(); source = root/SCENE; data = stable_read(source); source_hash = sha(data)
    meta = stable_read(Path(str(source)+'.meta')); scene_guid = GUID.search(meta)[1].decode()
    destination = output/source_hash[:16]
    if (destination/'receipt.json').exists():
        raise RuntimeError(f'Frozen snapshot already exists: {destination}; verify it or use a new output parent')
    destination.mkdir(parents=True, exist_ok=True); pack = destination/'pack'
    docs = documents(data); index, duplicates = index_guids(root)
    inventory = build_inventory(docs, scene_guid, source_hash, index, captured)
    bind_platform_source(inventory, root)
    errors = validate_inventory(inventory)
    if errors: raise ValueError(errors)
    geometry = stripped_scene(docs); geometry_path = 'Assets/WorldSnapshot/FpsStation.geometry.unity'
    (pack/geometry_path).parent.mkdir(parents=True, exist_ok=True); (pack/geometry_path).write_bytes(geometry)
    (pack/(geometry_path+'.meta')).write_bytes(meta)
    save_json(destination/'world-objects.json', inventory)
    aliases=alias_analysis(inventory,root)
    save_json(destination/'runtime-alias-analysis.json', aliases)
    closure = dependency_closure(root, geometry, index, pack)
    save_json(destination/'dependency-closure.json', closure)
    full_guids = set(g.decode().lower() for g in GUID.findall(data)); safe_guids = set(closure['visitedGuids'])
    save_json(destination/'omitted-source-references.json', {'claim': 'GUID identities only; no behaviour field values exported',
       'sourceSceneUniqueGuidCount': len(full_guids), 'geometryClosureGuidCount': len(safe_guids),
       'notNeededByGeometryScene': [{'guid':g,'path':index.get(g,{}).get('path')} for g in sorted(full_guids-safe_guids)]})
    # Re-read originals after the complete copy: a moving writer must never yield a mixed snapshot.
    drift=[]
    for row in closure['files']:
        p = root/row['sourcePath']
        if sha(stable_read(p)) != row['sha256']: drift.append(row['sourcePath'])
    source_after = sha(stable_read(source)); meta_after = sha(stable_read(Path(str(source)+'.meta')))
    if source_after != source_hash: drift.append(SCENE)
    if meta_after != sha(meta): drift.append(SCENE+'.meta')
    receipt = {'schemaVersion':'chooguard.world-snapshot-receipt.v1','snapshotId':source_hash[:16], 'capturedAtUtc':captured,
        'sourceScene': {'path': SCENE,'sha256':source_hash,'bytes':len(data),'guid':scene_guid},
        'sourceSceneAfterSha256':source_after,'sourceStability':{'stable':not drift,'changedPaths':drift,'originalWrites':0,'unityCalls':0},
        'geometryScene': {'path':geometry_path,'sha256':sha(geometry),'bytes':len(geometry),'metaSha256':sha(meta)},
        'inventory': {'path':'world-objects.json','sha256':sha((destination/'world-objects.json').read_bytes()),'bytes':(destination/'world-objects.json').stat().st_size},
        'dependencyClosure': {'path':'dependency-closure.json','sha256':sha((destination/'dependency-closure.json').read_bytes()),'files':len(closure['files']),'bytes':closure['totalBytes']},
        'runtimeAliasAnalysis': {'path':'runtime-alias-analysis.json','sha256':sha((destination/'runtime-alias-analysis.json').read_bytes())},
        'packagePrerequisites':closure['packagePrerequisites'],
        'packFiles': closure['files']+[{'path':geometry_path,'sha256':sha(geometry),'bytes':len(geometry)}, {'path':geometry_path+'.meta','sha256':sha(meta),'bytes':len(meta)}],
        'counts':inventory['counts'],'sourceRoots':inventory['sourceRoots'],'monoBehavioursOmitted':sum(d['classId']==114 for d in docs.values()),
        'integrity':{'hierarchyActiveMatricesAndLOD':'PASS','sourceComponentReferences':'PASS','duplicateGuidPaths':duplicates,
                     'unresolvedGeometryGuids':closure['unresolvedGuids'],'excludedExecutableDependencies':closure['excludedExecutableDependencies']},
        'buildScript':{'path':'workers/simulator/world_inventory/build_snapshot.py','sha256':sha(Path(__file__).read_bytes())},
        'limitations':['Offline source inventory; native FBX subasset bounds require isolated Unity import verification.',
                        'Typed behaviour identities are inventoried without their serialized field values. No source behaviour execution is authorized.',
                        'Physical flags preserve saved enabled/active/nontrigger collider state; they are not navigation, functional or digital-twin completion claims.']}
    receipt['status'] = 'PASS' if not drift and not closure['unresolvedGuids'] and not closure['excludedExecutableDependencies'] else 'HOLD'
    save_json(destination/'receipt.json', receipt)
    print(json.dumps({'snapshot':str(destination),'status':receipt['status'],'sourceSha256':source_hash,'counts':inventory['counts'],
                      'closureFiles':len(closure['files']),'closureBytes':closure['totalBytes'],'unresolved':len(closure['unresolvedGuids']),'changedSources':drift}))
    return destination


if __name__ == '__main__':
    p=argparse.ArgumentParser(); p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[3]); p.add_argument('--output',type=Path)
    args=p.parse_args(); build(args.root.resolve(),args.output or args.root/'output/simulator/world-snapshot')
