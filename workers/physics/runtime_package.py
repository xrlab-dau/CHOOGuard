#!/usr/bin/env python3
"""Launch only the hash-pinned packaged scientific worker, never the caller's virtualenv."""
from __future__ import annotations

import hashlib
import json
import os
import pathlib
import platform
import subprocess

HERE = pathlib.Path(__file__).resolve().parent


def resolve_runtime() -> tuple[pathlib.Path, dict]:
    root = pathlib.Path(os.environ.get('CG_RUNTIME_PACKAGE', str(HERE.parent / 'runtime'))).resolve()
    system, machine = platform.system(), platform.machine().lower()
    host = 'win-x64' if system == 'Windows' and machine in ('amd64', 'x86_64') else 'osx-arm64' if system == 'Darwin' and machine == 'arm64' else 'osx-x64' if system == 'Darwin' and machine == 'x86_64' else None
    if host is None:
        raise RuntimeError(f'RUNTIME_PLATFORM_UNSUPPORTED: {system}/{machine}')
    manifest_path = root / 'runtime-manifest.json'
    if not manifest_path.is_file():
        raise FileNotFoundError(f'RUNTIME_PACKAGE_MISSING: {manifest_path}; run package_runtime.py for this platform')
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    if manifest.get('schemaVersion') != 1:
        raise ValueError('RUNTIME_MANIFEST_VERSION')
    entries = [entry for entry in manifest['platforms'] if entry['id'] == host]
    if len(entries) != 1:
        raise RuntimeError(f'RUNTIME_PLATFORM_PACKAGE_MISSING_OR_DUPLICATE: {host}')
    entry = entries[0]
    seen = set()
    for file in entry['files']:
        path = package_path(root, file['path'])
        if file['path'] in seen:
            raise ValueError('RUNTIME_FILE_DUPLICATE')
        seen.add(file['path'])
        with path.open('rb') as stream:
            actual = hashlib.file_digest(stream, 'sha256').hexdigest()
        if actual != file['sha256']:
            raise ValueError(f'RUNTIME_PACKAGE_HASH_MISMATCH: {path}')
    for key in ('python', 'worker', 'sqlite'):
        if entry[key] not in seen:
            raise ValueError(f'RUNTIME_FILE_NOT_PINNED: {entry[key]}')
    return root, entry


def package_path(root: pathlib.Path, relative: str) -> pathlib.Path:
    path = pathlib.PurePosixPath(relative)
    if not relative or path.is_absolute() or any(part in ('..', '.') for part in path.parts) or '\\' in relative or ':' in relative:
        raise ValueError('RUNTIME_PATH_ESCAPE')
    result = root / relative
    if not result.resolve().is_relative_to(root):
        raise ValueError('RUNTIME_PATH_ESCAPE')
    current = root
    for part in path.parts:
        current /= part
        if current.is_symlink():
            raise ValueError('RUNTIME_SYMLINK_FORBIDDEN')
    return result


def configure_environment() -> tuple[pathlib.Path, dict]:
    root, entry = resolve_runtime()
    os.environ['CG_RUNTIME_PACKAGE'] = str(root)
    os.environ['CG_PHYSICS_UPSTREAM'] = str(package_path(root, entry['upstream']))
    os.environ['CG_PHYSICS_CASE'] = str(package_path(root, entry['referenceCase']))
    return root, entry


def launch_worker(**kwargs) -> subprocess.Popen:
    root, entry = configure_environment()
    command = [str(package_path(root, entry['python'])), '-I', '-B', '-u', '-X', 'utf8', str(package_path(root, entry['worker']))]
    if kwargs.get('shell'):
        raise ValueError('Shell worker launch is forbidden')
    if kwargs.get('text'):
        kwargs.setdefault('encoding', 'utf-8')
    return subprocess.Popen(command, cwd=root, **kwargs)


if __name__ == '__main__':
    process = launch_worker()
    try:
        raise SystemExit(process.wait())
    except KeyboardInterrupt:
        process.terminate()
        try:
            process.wait(timeout=2)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
        raise SystemExit(130)
