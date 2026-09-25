#!/usr/bin/env python3
"""Materialize relocatable, pinned runtimes without pip, system installs, or a repo virtualenv.

Windows can be assembled on macOS, but this is never Windows execution evidence.
macOS SQLite compilation is explicit (--compile-sqlite) and uses the verified amalgamation.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
import pathlib
import platform
import shutil
import subprocess
import tarfile
import tempfile
import urllib.request
import zipfile

HERE = pathlib.Path(__file__).resolve().parent


def digest(path: pathlib.Path) -> str:
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def relative(value: str) -> pathlib.PurePosixPath:
    path = pathlib.PurePosixPath(value)
    if path.is_absolute() or not path.parts or any(part in ('..', '.') for part in path.parts) or '\\' in value or ':' in value:
        raise ValueError(f'Unsafe archive path: {value}')
    return path


def fetch(spec: dict, cache: pathlib.Path) -> bytes:
    expected = spec['sha256']
    target = cache / expected
    if target.exists():
        data = target.read_bytes()
    else:
        with urllib.request.urlopen(spec['url'], timeout=120) as response:
            data = response.read()
    if hashlib.sha256(data).hexdigest() != expected:
        raise ValueError(f'Artifact SHA256 mismatch: {spec["url"]}')
    if 'sha3_256' in spec and hashlib.sha3_256(data).hexdigest() != spec['sha3_256']:
        raise ValueError(f'Artifact official SHA3-256 mismatch: {spec["url"]}')
    if not target.exists():
        target.write_bytes(data)
    return data


def unzip(data: bytes, destination: pathlib.Path, wheel: bool = False) -> None:
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for info in archive.infolist():
            path = relative(info.filename.rstrip('/'))
            if info.is_dir():
                continue
            if (info.external_attr >> 16) & 0o170000 == 0o120000:
                raise ValueError(f'Wheel/ZIP symlink forbidden: {path}')
            if wheel and '.data' in path.parts[0]:
                if len(path.parts) < 3 or path.parts[1] not in ('purelib', 'platlib'):
                    # Entry-point scripts are not the headless worker's entry and are not installed.
                    if len(path.parts) >= 3 and path.parts[1] == 'scripts':
                        continue
                    raise ValueError(f'Unsupported wheel data layout: {path}')
                path = pathlib.PurePosixPath(*path.parts[2:])
            target = destination / str(path)
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists():
                raise ValueError(f'Archive would overwrite {target}')
            target.write_bytes(archive.read(info))
            if info.external_attr >> 16 & 0o111:
                target.chmod(0o755)


def untar_python(data: bytes, destination: pathlib.Path) -> None:
    links = []
    with tarfile.open(fileobj=io.BytesIO(data), mode='r:gz') as archive:
        for member in archive.getmembers():
            path = relative(member.name)
            if path.parts[0] != 'python':
                raise ValueError(f'Unexpected standalone Python layout: {path}')
            if len(path.parts) == 1 or member.isdir():
                continue
            target = destination / str(pathlib.PurePosixPath(*path.parts[1:]))
            target.parent.mkdir(parents=True, exist_ok=True)
            if member.issym() or member.islnk():
                if pathlib.PurePosixPath(member.linkname).is_absolute():
                    raise ValueError('Absolute Python archive symlink')
                source = target.parent / member.linkname if member.issym() else destination / member.linkname.removeprefix('python/')
                source = source.resolve()
                if not source.is_relative_to(destination.resolve()):
                    raise ValueError('Python archive symlink escapes package')
                links.append((source, target))
            elif member.isfile():
                with archive.extractfile(member) as stream, target.open('xb') as out:
                    shutil.copyfileobj(stream, out)
                target.chmod(member.mode & 0o777)
            else:
                raise ValueError(f'Unsupported archive member: {member.name}')
    # Materialize internal file aliases so runtime deployment contains no search-path-changing symlinks.
    while links:
        unresolved = []
        for source, target in links:
            if source.is_file():
                shutil.copy2(source, target)
            else:
                unresolved.append((source, target))
        if len(unresolved) == len(links):
            raise ValueError(f'Unresolved Python archive links: {unresolved}')
        links = unresolved


def copy_reference(destination: pathlib.Path) -> None:
    source = HERE / 'cases/reference-hall'
    receipt = json.loads((source / 'receipt.json').read_text(encoding='utf-8'))
    if receipt['solverExitCode'] != 0 or not receipt['completed']:
        raise ValueError('A genuinely completed FDS reference receipt is required')
    identities = dict(receipt['outputSha256'], **{'hall.fds': receipt['deckSha256']})
    for name, expected in identities.items():
        relative(name)
        if digest(source / name) != expected:
            raise ValueError(f'FDS reference hash mismatch: {name}')
    destination.mkdir(parents=True)
    # Include raw Smokeview dependencies and logs, but never executable pickle caches.
    for item in source.iterdir():
        if item.is_file() and item.suffix != '.pickle':
            shutil.copy2(item, destination / item.name)


def build(args: argparse.Namespace) -> dict:
    lock = json.loads((HERE / 'runtime-artifacts.lock.json').read_text(encoding='utf-8'))
    upstream = json.loads((HERE / 'upstream-lock.json').read_text(encoding='utf-8'))
    spec = lock['platforms'][args.platform]
    artifacts = lock['artifacts']
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    final = output / args.platform
    if final.exists():
        raise FileExistsError(f'Package already exists, preserve it or select a fresh --output: {final}')
    cache = HERE / '.package-cache'
    cache.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.staging-', dir=output) as temporary:
        stage = pathlib.Path(temporary) / args.platform
        stage.mkdir()
        python = stage / 'python'
        python.mkdir()
        if args.platform == 'win-x64':
            unzip(fetch(artifacts[spec['python']], cache), python)
            site = python / 'Lib/site-packages'
            # Embedded Python is isolated: allow only its zip/stdlib and our fixed wheel directory.
            (python / 'python313._pth').write_text('python313.zip\n.\nLib/site-packages\nimport site\n', encoding='utf-8')
            executable = 'python/python.exe'
            sqlite_name = 'native/sqlite3.dll'
            unzip(fetch(artifacts['sqlite-win-x64'], cache), stage / 'native')
            if lock['sqliteSourceId'].encode() not in (stage / sqlite_name).read_bytes():
                raise ValueError('Official Windows SQLite source identity absent from DLL')
        else:
            if not args.compile_sqlite or platform.system() != 'Darwin' or platform.machine() != 'arm64':
                raise RuntimeError('osx-arm64 requires an arm64 macOS host and explicit --compile-sqlite; no system SQLite fallback')
            untar_python(fetch(artifacts[spec['python']], cache), python)
            site = python / 'lib/python3.13/site-packages'
            executable = 'python/bin/python3.13'
            sqlite_name = 'native/libsqlite3.dylib'
            native = stage / 'native'
            native.mkdir()
            source = pathlib.Path(temporary) / 'sqlite-source'
            unzip(fetch(artifacts['sqlite-source'], cache), source)
            amalgamation = source / 'sqlite-amalgamation-3530400/sqlite3.c'
            command = ['clang', '-O2', '-dynamiclib', '-arch', 'arm64', '-DSQLITE_THREADSAFE=1', '-DSQLITE_DEFAULT_MEMSTATUS=0', '-o', str(stage / sqlite_name), str(amalgamation)]
            subprocess.run(command, check=True)
            # Compile provenance remains explicit; runtime provider still verifies source ID on actual open.
            (native / 'build-provenance.json').write_text(json.dumps({'command': command, 'sourceArtifact': artifacts['sqlite-source'], 'compiler': subprocess.check_output(['clang', '--version'], text=True)}, indent=2) + '\n', encoding='utf-8')
        site.mkdir(parents=True, exist_ok=True)
        for key in spec['wheels']:
            unzip(fetch(artifacts[key], cache), site, wheel=True)
        if args.platform == 'win-x64':
            # JuPedSim's wheel imports MSVCP140.dll without bundling it. NumPy's pinned wheel
            # supplies the unmodified MSVC runtime under a delvewheel-mangled filename.
            candidates = list((site / 'numpy.libs').glob('msvcp140-*.dll'))
            if len(candidates) != 1:
                raise ValueError('Pinned NumPy wheel no longer supplies the reviewed MSVC dependency')
            shutil.copy2(candidates[0], python / 'MSVCP140.dll')
            (python / 'msvc-provenance.json').write_text(json.dumps({
                'sourceArtifact': artifacts['numpy-win-x64'],
                'sourceMember': 'numpy.libs/' + candidates[0].name,
                'destination': 'MSVCP140.dll', 'sha256': digest(candidates[0]),
                'modification': 'Filename only; original bytes and notices preserved',
                'notice': 'Microsoft Distributable Code; Windows only. See Python LICENSE.txt distribution restrictions.'
            }, indent=2) + '\n', encoding='utf-8')
        physics = stage / 'physics'
        physics.mkdir()
        for name in ('worker.py', 'upstream-lock.json', 'requirements-core.txt'):
            shutil.copy2(HERE / name, physics / name)
        original = physics / 'upstream'
        original.mkdir()
        for name, expected in upstream['modules'].items():
            url = f'https://raw.githubusercontent.com/PedestrianDynamics/pyFDS-Evac/{upstream["commit"]}/pyfds_evac/core/{name}'
            (original / name).write_bytes(fetch({'url': url, 'sha256': expected}, cache))
        (original / 'LICENSE').write_bytes(fetch(artifacts['pyfds-license'], cache))
        copy_reference(physics / 'cases/reference-hall')
        licenses = stage / 'licenses'
        licenses.mkdir()
        (licenses / 'jupedsim-1.4.2.tar.gz').write_bytes(fetch(artifacts['jupedsim-source'], cache))
        (licenses / 'SQLite-public-domain.txt').write_text('SQLite is public domain. https://sqlite.org/copyright.html\nOfficial artifact and checksum provenance: runtime-provenance.json\n', encoding='utf-8')
        shutil.copy2(HERE / 'runtime-artifacts.lock.json', stage / 'runtime-provenance.json')
        entry = {'id': args.platform, 'sqlite': f'{args.platform}/{sqlite_name}', 'sqliteSourceId': lock['sqliteSourceId'],
                 'python': f'{args.platform}/{executable}', 'worker': f'{args.platform}/physics/worker.py',
                 'upstream': f'{args.platform}/physics/upstream', 'referenceCase': f'{args.platform}/physics/cases/reference-hall',
                 'files': [{'path': f'{args.platform}/{item.relative_to(stage).as_posix()}', 'sha256': digest(item)} for item in sorted(stage.rglob('*')) if item.is_file()]}
        manifest_path = output / 'runtime-manifest.json'
        manifest = json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else {'schemaVersion': 1, 'platforms': []}
        if manifest['schemaVersion'] != 1 or any(value['id'] == args.platform for value in manifest['platforms']):
            raise ValueError('Existing runtime manifest conflicts with this platform package')
        manifest['platforms'].append(entry)
        manifest_temporary = pathlib.Path(temporary) / 'runtime-manifest.json'
        manifest_temporary.write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
        stage.rename(final)
        os.replace(manifest_temporary, manifest_path)
    return {'platform': args.platform, 'manifest': str(output / 'runtime-manifest.json'), 'verifiedFiles': len(entry['files']),
            'sqliteSha256': digest(final / sqlite_name), 'sourceId': lock['sqliteSourceId'], 'runtimeExecution': 'NOT_RUN'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--platform', choices=('win-x64', 'osx-arm64'), required=True)
    parser.add_argument('--output', type=pathlib.Path, default=HERE.parent / 'runtime')
    parser.add_argument('--compile-sqlite', action='store_true')
    print(json.dumps(build(parser.parse_args()), indent=2))
