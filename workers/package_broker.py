#!/usr/bin/env python3
"""Add the portable Node gameplay broker to an already materialized platform package.

Does not run npm scripts, a broker, or any paid provider call. No credentials are copied.
"""
from __future__ import annotations

import argparse
import base64
import hashlib
import io
import json
import os
import pathlib
import shutil
import tarfile
import tempfile
import urllib.request
import zipfile

from physics.package_runtime import digest, fetch, relative

HERE = pathlib.Path(__file__).resolve().parent


def add_broker(platform_id: str, output: pathlib.Path) -> dict:
    output = output.resolve(strict=True)
    manifest_path = output / 'runtime-manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    entries = [entry for entry in manifest['platforms'] if entry['id'] == platform_id]
    if manifest['schemaVersion'] != 1 or len(entries) != 1:
        raise ValueError('Materialize the physics/SQLite platform before adding its broker')
    entry = entries[0]
    final = output / platform_id / 'broker'
    if final.exists() or 'broker' in entry:
        raise FileExistsError('Broker package already exists; create a fresh --output package for upgrades')
    lock = json.loads((HERE / 'node-runtime.lock.json').read_text(encoding='utf-8'))
    spec = lock['platforms'][platform_id]
    cache = HERE / 'physics/.package-cache'
    cache.mkdir(exist_ok=True)
    data = fetch(spec, cache)
    with tempfile.TemporaryDirectory(prefix='.broker-', dir=output) as temporary:
        stage = pathlib.Path(temporary) / 'broker'
        stage.mkdir()
        executable = 'node.exe' if platform_id == 'win-x64' else 'node'
        if platform_id == 'win-x64':
            with zipfile.ZipFile(io.BytesIO(data)) as archive:
                (stage / executable).write_bytes(archive.read(spec['prefix'] + '/' + spec['executable']))
                (stage / 'NODE-LICENSE').write_bytes(archive.read(spec['prefix'] + '/LICENSE'))
        else:
            with tarfile.open(fileobj=io.BytesIO(data), mode='r:gz') as archive:
                for source, target in ((spec['executable'], executable), ('LICENSE', 'NODE-LICENSE')):
                    member = archive.getmember(spec['prefix'] + '/' + source)
                    if not member.isfile():
                        raise ValueError('Expected regular official Node runtime member')
                    with archive.extractfile(member) as stream:
                        (stage / target).write_bytes(stream.read())
            (stage / executable).chmod(0o755)
        source = HERE / 'prediction'
        for name in ('gameplay-broker.mjs', 'gameplay-core.mjs', 'gameplay-wire.mjs', 'gameplay-providers.mjs', 'gameplay-ledger.mjs', 'package.json', 'package-lock.json'):
            shutil.copy2(source / name, stage / name)
        npm_lock = json.loads((source / 'package-lock.json').read_text(encoding='utf-8'))
        if npm_lock['lockfileVersion'] != 3:
            raise ValueError('Unsupported npm artifact lock version')
        for name, package in npm_lock['packages'].items():
            if name == '':
                continue
            if not name.startswith('node_modules/') or package.get('hasInstallScript'):
                raise ValueError(f'Unexpected dependency/install-script requirement: {name}')
            relative(name)
            integrity = package['integrity']
            if not integrity.startswith('sha512-'):
                raise ValueError('Locked npm dependency requires SHA512 integrity')
            with urllib.request.urlopen(package['resolved'], timeout=60) as response:
                payload = response.read()
            if base64.b64encode(hashlib.sha512(payload).digest()).decode() != integrity[7:]:
                raise ValueError(f'Npm integrity mismatch: {name}')
            with tarfile.open(fileobj=io.BytesIO(payload), mode='r:gz') as archive:
                for member in archive.getmembers():
                    path = relative(member.name)
                    if path.parts[0] != 'package':
                        raise ValueError('Unexpected npm tar root')
                    if member.isdir():
                        continue
                    if not member.isfile() or len(path.parts) < 2:
                        raise ValueError('Only regular npm package files are accepted')
                    target = stage / name / str(pathlib.PurePosixPath(*path.parts[1:]))
                    target.parent.mkdir(parents=True, exist_ok=True)
                    with archive.extractfile(member) as stream, target.open('xb') as out:
                        shutil.copyfileobj(stream, out)
        schemas = stage / 'schemas'
        schemas.mkdir()
        canonical = HERE.parent / 'docs/CHOOGuard_Story_Plan_v5/design/fps-ai-20260925/contracts'
        for name in ('npc-decision.schema.json', 'future-step.schema.json'):
            shutil.copy2(canonical / name, schemas / name)
        shutil.copy2(HERE / 'node-runtime.lock.json', stage / 'node-runtime-provenance.json')
        prefix = platform_id + '/broker/'
        entry['node'] = prefix + executable
        entry['broker'] = prefix + 'gameplay-broker.mjs'
        entry['schemaDirectory'] = prefix + 'schemas'
        entry['files'].extend({'path': prefix + item.relative_to(stage).as_posix(), 'sha256': digest(item)} for item in sorted(stage.rglob('*')) if item.is_file())
        manifest_temp = pathlib.Path(temporary) / 'runtime-manifest.json'
        manifest_temp.write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
        stage.rename(final)
        os.replace(manifest_temp, manifest_path)
    return {'platform': platform_id, 'manifest': str(manifest_path), 'verifiedFiles': len(entry['files']), 'nodeVersion': lock['version'], 'execution': 'NOT_RUN'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--platform', choices=('win-x64', 'osx-arm64'), required=True)
    parser.add_argument('--output', type=pathlib.Path, default=HERE / 'runtime')
    args = parser.parse_args()
    print(json.dumps(add_broker(args.platform, args.output), indent=2))
