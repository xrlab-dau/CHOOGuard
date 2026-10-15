#!/usr/bin/env python3
"""Run a bounded isolated standalone build or Metal graphics proof."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import signal
import subprocess
import time

ROOT = Path(__file__).resolve().parents[3]
PROJECT = ROOT / 'output/simulator/platform-ktx-unity'
OUTPUT = PROJECT / 'diagnostics/graphics-resume'
UNITY_APP = Path('/Applications/Unity/Unity-6000.3.23f1/Unity.app')
AUTHORITY = ROOT / '.planning/2026-10-13-codex-claude-bd6-resume/WRITER-AUTHORITY.json'


def alive(pid):
    try:
        os.kill(pid, 0)
        return True
    except ProcessLookupError:
        return False


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('phase', choices=['build', 'player'])
    args = parser.parse_args()
    authority = json.loads(AUTHORITY.read_text())
    assert authority['status'] == 'ACTIVE'
    assert PROJECT.is_dir() and (UNITY_APP / 'Contents/PlaybackEngines/MacStandaloneSupport').is_dir()
    protected = [14339, 14399]
    log = OUTPUT / (args.phase + '.log')
    env = os.environ.copy()
    env['DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE'] = 'false'
    if args.phase == 'build':
        env['DOTNET_STARTUP_HOOKS'] = str(PROJECT / 'diagnostics/group-hook/GroupLookupHook.dll')
        command = [str(UNITY_APP / 'Contents/MacOS/Unity'), '-batchmode', '-nographics', '-projectPath', str(PROJECT),
                   '-executeMethod', 'ChooGuard.Seeded.Unity.GraphicsResume.Editor.BuildGraphicsPlayer.Build', '-quit', '-logFile', str(log)]
        timeout = 300
    else:
        env.pop('DOTNET_STARTUP_HOOKS', None)
        player = OUTPUT / 'PlatformGraphics.app/Contents/MacOS/PlatformGraphics'
        if not player.is_file():
            # Unity preserves the configured product name inside a differently named bundle.
            from plistlib import loads
            info = loads((OUTPUT / 'PlatformGraphics.app/Contents/Info.plist').read_bytes())
            player = OUTPUT / 'PlatformGraphics.app/Contents/MacOS' / info['CFBundleExecutable']
        assert player.is_file()
        command = [str(player), '-batchmode', '-force-metal', '-logFile', str(log),
                   '-chooguard-graphics-proof', str(OUTPUT / 'player-proof')]
        timeout = 120
    files = list(Path(__file__).parent.glob('*.cs'))
    receipt = {'schema': 'chooguard.bounded-standalone-process.v1', 'phase': args.phase,
               'command': command, 'cwd': str(PROJECT), 'timeout_seconds': timeout,
               'fresh_authority_sha256': hashlib.sha256(AUTHORITY.read_bytes()).hexdigest(),
               'diagnostic_source_hashes': {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in files},
               'protected_before': {str(p): alive(p) for p in protected}, 'shared_processes_signalled': []}
    OUTPUT.mkdir(parents=True, exist_ok=True)
    receipt_path = OUTPUT / ('process-' + args.phase + '.json')
    start = time.monotonic()
    with (OUTPUT / (args.phase + '-console.log')).open('wb') as stream:
        process = subprocess.Popen(command, cwd=PROJECT, env=env, start_new_session=True, stdout=stream, stderr=subprocess.STDOUT)
        receipt['pid'] = process.pid
        receipt['pgid'] = os.getpgid(process.pid)
        receipt_path.write_text(json.dumps(receipt, indent=2) + '\n')
        print(json.dumps({'phase': args.phase, 'pid': process.pid, 'receipt': str(receipt_path)}), flush=True)
        try:
            process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            receipt['timed_out'] = True
            os.killpg(process.pid, signal.SIGTERM)
            try:
                process.wait(timeout=8)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait(timeout=5)
    receipt['exit_code'] = process.returncode
    receipt['elapsed_seconds'] = round(time.monotonic() - start, 3)
    receipt['protected_after'] = {str(p): alive(p) for p in protected}
    receipt['diagnostic_source_changed'] = any(hashlib.sha256((ROOT / p).read_bytes()).hexdigest() != h for p, h in receipt['diagnostic_source_hashes'].items())
    receipt_path.write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps(receipt), flush=True)
    return process.returncode


if __name__ == '__main__':
    raise SystemExit(main())
