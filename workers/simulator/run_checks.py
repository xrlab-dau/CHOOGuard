#!/usr/bin/env python3
"""Compile/test the isolated simulator using Unity's bundled C# toolchain.

Does not launch, refresh, or modify the shared Unity project.
"""
from pathlib import Path
import argparse
import json
import os
import subprocess
import sys
import hashlib
import time

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent


def toolchain():
    candidates = list(Path('/Applications/Unity').glob('*/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge'))
    override = os.environ.get('CHOOGUARD_MONO_ROOT')
    if override:
        candidates.insert(0, Path(override))
    for candidate in candidates:
        mono, compiler = candidate / 'bin/mono', candidate / 'lib/mono/4.5/csc.exe'
        if mono.is_file() and compiler.is_file():
            return mono, compiler
    raise SystemExit('Set CHOOGUARD_MONO_ROOT to the Unity MonoBleedingEdge directory.')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('command', nargs='?', default='test', choices=['test', 'run', 'replay', 'benchmark'])
    parser.add_argument('--seed', type=int, default=20261013)
    parser.add_argument('--ticks', type=int, default=1200)
    parser.add_argument('--input', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    mono, compiler = toolchain()
    out = ROOT / 'output/simulator/checks'
    out.mkdir(parents=True, exist_ok=True)
    build = out / 'builds' / (args.command + '-' + str(os.getpid()))
    build.mkdir(parents=True, exist_ok=True)
    sources = sorted([*HERE.glob('Runtime/Core/*.cs'), *HERE.glob('Runtime/Planning/*.cs'), *HERE.glob('Tests/*.cs'), *HERE.glob('Tools/*.cs')])
    source_hashes = {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}
    implementation_sources = {path: digest for path, digest in source_hashes.items()
                              if '/Runtime/' in path or path.endswith('/Tools/SimulatorCli.cs')}
    implementation = hashlib.sha256(json.dumps(implementation_sources, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    json_dlls = sorted((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    if not json_dlls:
        raise SystemExit('Newtonsoft.Json runtime assembly not found in the existing package cache.')
    import shutil
    shutil.copy2(json_dlls[0], build / 'Newtonsoft.Json.dll')
    exe = build / 'SimulatorChecks.exe'
    netstandard = mono.parent.parent / 'lib/mono/4.5/Facades/netstandard.dll'
    cmd = [str(mono), str(compiler), '-nologo', '-langversion:latest', '-target:exe', '-out:' + str(exe), '-r:' + str(json_dlls[0]), '-r:System.Net.Http.dll', '-r:' + str(netstandard), *map(str, sources)]
    subprocess.run(cmd, check=True, cwd=ROOT)
    run = [str(mono), str(exe), args.command, '--seed', str(args.seed), '--ticks', str(args.ticks), '--source', str(HERE / 'data/platform-source.json'), '--implementation-sha', implementation]
    if args.input:
        run += ['--input', str(args.input.resolve())]
    if args.output:
        run += ['--output', str(args.output.resolve())]
    began = time.monotonic()
    result = subprocess.run(run, cwd=ROOT, capture_output=True, text=True)
    if result.stdout:
        print(result.stdout, end='')
    if result.stderr:
        print(result.stderr, end='', file=sys.stderr)
    report = {
        'command': args.command, 'exit_code': result.returncode,
        'elapsed_seconds': round(time.monotonic() - began, 4),
        'toolchain': str(mono.parent.parent), 'arguments': run[2:],
        'sources': source_hashes,
        'source_changed_during_run': any(hashlib.sha256(p.read_bytes()).hexdigest() != source_hashes[str(p.relative_to(ROOT))] for p in sources),
        'stdout': result.stdout, 'stderr': result.stderr,
        'scope': 'Engine-free state and provider-contract checks; shared Unity instance not accessed.'
    }
    (out / ('last-' + args.command + '.json')).write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    return result.returncode


if __name__ == '__main__':
    sys.exit(main())
