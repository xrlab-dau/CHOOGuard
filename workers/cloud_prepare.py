#!/usr/bin/env python3
"""Explicit UBA preparation: verify tracked managed dependencies, package runtimes and create test fixtures."""
from __future__ import annotations

import argparse
import json
import os
import pathlib
import platform
import struct
import sys
import tempfile
import xml.etree.ElementTree as ET

HERE = pathlib.Path(__file__).resolve().parent
# The official embeddable distribution's ._pth intentionally omits the script directory.
# Add only this checkout's worker modules; packaging needs no pip, venv or site packages.
sys.path.insert(0, str(HERE))

from package_broker import add_broker
from physics.package_runtime import build, digest


def require_python() -> None:
    identity = {
        'executable': sys.executable,
        'version': sys.version,
        'platform': sys.platform,
        'system': platform.system(),
        'machine': platform.machine(),
        'pointerBits': struct.calcsize('P') * 8,
    }
    if sys.platform != 'win32' or identity['system'] != 'Windows' or identity['machine'].lower() not in ('amd64', 'x86_64') or identity['pointerBits'] != 64 or sys.version_info < (3, 11):
        raise RuntimeError('Use a native Windows x64 Python 3.11+ packaging interpreter; actual=' + json.dumps(identity))
    # HTTPS downloads and package_runtime.digest require these stdlib capabilities.
    try:
        import hashlib
        import ssl

        if not callable(getattr(hashlib, 'file_digest', None)):
            raise ImportError('hashlib.file_digest is unavailable')
        ssl.create_default_context()
    except (ImportError, OSError) as error:
        raise RuntimeError('Packaging interpreter lacks required stdlib capabilities; actual=' + json.dumps(identity)) from error
    print('CG_CLOUD_PYTHON_READY: ' + json.dumps(identity), flush=True)


def verify_managed(project: pathlib.Path) -> None:
    lock = json.loads((HERE / 'nuget-managed.lock.json').read_text(encoding='utf-8'))
    packages = ET.parse(project / 'Assets/packages.config').getroot().findall('package')
    identities = [(row['id'], row['version'], row['targetFramework']) for row in lock['packages']]
    actual = [(row.get('id'), row.get('version'), row.get('targetFramework')) for row in packages]
    config = ET.parse(project / 'Assets/NuGet.config').getroot()
    repositories = [row.get('value') for row in config.findall('config/add') if row.get('key') == 'repositoryPath']
    if lock['schemaVersion'] != 1 or sorted(actual) != sorted(identities) or repositories != [lock['repositoryPath']] or repositories != ['./Packages']:
        raise ValueError('NuGet configuration differs from reviewed managed dependency lock')
    expected = set()
    for row in lock['packages']:
        path = project / 'Assets/Packages' / f'{row["id"]}.{row["version"]}' / 'lib' / row['targetFramework'] / f'{row["id"]}.dll'
        if digest(path) != row['sha256']:
            raise ValueError(f'Managed dependency SHA256 mismatch: {row["id"]}')
        expected.add(path)
    # An unrelated or alternative-framework binary must not silently become a Player dependency.
    actual_binaries = {path for path in (project / 'Assets/Packages').rglob('*') if path.suffix.lower() in ('.dll', '.so', '.dylib', '.bundle')}
    if actual_binaries != expected:
        raise ValueError('Tracked NuGet package binary inventory differs from the reviewed lock')


def test_fixtures(scratch: pathlib.Path) -> tuple[pathlib.Path, pathlib.Path]:
    fixtures = scratch / 'fixtures'
    fixtures.mkdir()
    links = scratch / 'build-links'
    links.mkdir()
    target = scratch / 'link-target'
    target.mkdir()
    file_target = target / 'existing.txt'
    file_target.write_text('Cloud boundary fixture\n', encoding='utf-8')
    try:
        (links / 'root-link').symlink_to(target, target_is_directory=True)
        (links / 'parent-link').symlink_to(target, target_is_directory=True)
        (links / 'leaf-link').symlink_to(file_target)
        (links / 'dangling-link').symlink_to(target / 'absent.txt')
    except OSError as error:
        raise RuntimeError('Cloud boundary tests require real Windows symlink privileges (Developer Mode or SeCreateSymbolicLinkPrivilege). No fixture substitution or test filtering.') from error
    return fixtures, links


def prepare(args: argparse.Namespace) -> None:
    if os.environ.get('CG_CLOUD_BUILD') != 'win-x64' or os.environ.get('IS_BUILDER', '').lower() != 'true' or os.environ.get('BUILDER_OS') != 'WINDOWS':
        raise RuntimeError('Explicit Windows UBA configuration required')
    require_python()
    project = args.project_root.resolve(strict=True)
    if project != HERE.parent:
        raise ValueError('UBA project root must be the checkout containing this script')
    gameplay = os.environ.get('CG_GAMEPLAY_SCENE', '')
    scene = pathlib.PurePosixPath(gameplay)
    if not gameplay.startswith('Assets/') or scene.suffix != '.unity' or '..' in scene.parts or not (project / gameplay).is_file():
        raise ValueError('CG_GAMEPLAY_SCENE must name the actual committed model scene')
    runtime = HERE / 'runtime'
    if runtime.exists():
        raise FileExistsError('workers/runtime already exists; use a clean UBA checkout, not a cached or committed runtime package')
    # Retained outside the checkout until UBA tears down the worker; the Unity test processes need these links.
    scratch = pathlib.Path(tempfile.mkdtemp(prefix='chooguard-uba-'))
    fixtures, links = test_fixtures(scratch)
    verify_managed(project)
    print(json.dumps(build(argparse.Namespace(platform='win-x64', output=runtime, compile_sqlite=False))))
    print(json.dumps(add_broker('win-x64', runtime)))
    manifest = json.loads((runtime / 'runtime-manifest.json').read_text(encoding='utf-8'))
    entry, = manifest['platforms']
    sqlite = runtime / entry['sqlite']
    extra = os.environ.get('UNITY_EXTRA_PARAMS', '')
    if '-cgFixtureRoot' in extra or '-cgBuildLinkFixture' in extra:
        raise ValueError('Do not duplicate cloud-owned fixture arguments in UNITY_EXTRA_PARAMS')
    values = {
        'CG_CLOUD_OUTPUT_DIRECTORY': str(args.output_directory.resolve()),
        'CG_RUNTIME_PACKAGE': str(runtime),
        'CG_TEST_SQLITE_BINARY': str(sqlite),
        'CG_TEST_SQLITE_SHA256': digest(sqlite),
        'CG_TEST_SQLITE_SOURCE_ID': entry['sqliteSourceId'],
        'UNITY_EXTRA_PARAMS': f'{extra} -cgFixtureRoot "{fixtures.as_posix()}" -cgBuildLinkFixture "{links.as_posix()}"'.strip(),
    }
    with args.env_file.open('a', encoding='utf-8', newline='\n') as stream:
        for key, value in values.items():
            if '\n' in value or '\r' in value:
                raise ValueError(f'UBA environment value must be single-line: {key}')
            stream.write(f'{key}={value}\n')
    print('CG_CLOUD_PREBUILD_READY: managed hashes verified, pinned runtime assembled; Unity/tests/Player execution NOT_RUN')


if __name__ == '__main__':
    if sys.argv[1:] == ['--check-python']:
        require_python()
        raise SystemExit(0)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=pathlib.Path, required=True)
    parser.add_argument('--output-directory', type=pathlib.Path, required=True)
    parser.add_argument('--env-file', type=pathlib.Path, required=True)
    prepare(parser.parse_args())
