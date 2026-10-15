#!/usr/bin/env python3
"""Open the isolated simulator and its local sign-in helper from a desktop session."""
from pathlib import Path
import argparse
import json
import os
import plistlib
import stat
import signal
import subprocess
import sys
import time
import urllib.request

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
PROJECT = ROOT / 'output/simulator/platform-ktx-unity'
PRIVATE = Path.home() / '.local/share/chooguard/provider'


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def helper_ready():
    descriptor = PRIVATE / 'connection.json'
    try:
        if descriptor.stat().st_size > 16384:
            return False
        data = json.loads(descriptor.read_text())
        if data.get('schema') != 1 or data.get('endpoint') != 'http://127.0.0.1:18953':
            return False
        request = urllib.request.Request(data['endpoint'] + '/status', headers={'Authorization': 'Bearer ' + data['capability']})
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
        with opener.open(request, timeout=1) as response:
            return response.status == 200
    except (OSError, ValueError, KeyError):
        return False


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--unity', type=Path, help='Exact Unity editor executable for this independent project')
    parser.add_argument('--editor', action='store_true', help='Open the isolated editor instead of a matching built player')
    parser.add_argument('--graphics-proof', action='store_true', help='Run the matching standalone capture/acceptance harness from a desktop session')
    args = parser.parse_args()
    scene = PROJECT / 'Assets/Simulator/Scenes/PlatformSeededSimulator.unity'
    if not scene.is_file():
        raise SystemExit('The isolated scene must be built first; see workers/simulator/README.md.')
    version = (PROJECT / 'ProjectSettings/ProjectVersion.txt').read_text().split('m_EditorVersion: ', 1)[1].splitlines()[0]
    editor = args.unity or Path('/Applications/Unity') / ('Unity-' + version) / 'Unity.app/Contents/MacOS/Unity'
    bundle=PROJECT/'diagnostics/graphics-resume/PlatformGraphics.app'
    player=None
    try:
        build=json.loads((bundle.parent/'build-receipt.json').read_text())
        manifest=json.loads((PROJECT/'Assets/Simulator/Resources/SourceManifest.json').read_text())
        if build.get('result')=='Succeeded' and build.get('source_assembly_hash')==manifest.get('runtime_fingerprint'):
            candidate=bundle/'Contents/MacOS'/plistlib.loads((bundle/'Contents/Info.plist').read_bytes())['CFBundleExecutable']
            if candidate.is_file():player=candidate
    except (OSError,ValueError,KeyError,plistlib.InvalidFileException):
        pass
    use_editor=args.editor or args.unity is not None or player is None
    if args.graphics_proof and use_editor:
        raise SystemExit('A standalone player matching the current staged sources is required for graphics proof.')
    if use_editor and not editor.is_file():
        raise SystemExit('Unity editor not found. Supply its executable with --unity.')
    authority = json.loads((ROOT / '.planning/2026-10-13-codex-claude-bd6-resume/WRITER-AUTHORITY.json').read_text())
    if authority.get('status') != 'ACTIVE':
        raise SystemExit('Current writer authority must be checked before launching the isolated editor.')
    owned_helper = None
    helper_log = None
    editor_process = None
    try:
        if not helper_ready():
            PRIVATE.mkdir(parents=True, exist_ok=True, mode=0o700)
            info = PRIVATE.lstat()
            if not stat.S_ISDIR(info.st_mode) or info.st_uid != os.getuid() or info.st_mode & 0o077:
                raise SystemExit('The app sign-in directory must be private and owned by the current user.')
            # Provider tokens are managed by the helper, outside Unity and this repository.
            log_path = PRIVATE / 'helper-launch.log'
            fd = os.open(log_path, os.O_WRONLY | os.O_CREAT | os.O_APPEND | os.O_NOFOLLOW, 0o600)
            os.fchmod(fd, 0o600)
            helper_log = os.fdopen(fd, 'ab')
            helper_python = HERE / 'provider_auth/.venv/bin/python'
            if not helper_python.is_file():
                helper_python = Path(sys.executable)
            owned_helper = subprocess.Popen([str(helper_python), str(HERE / 'provider_auth/sidecar.py')], cwd=HERE / 'provider_auth', stdout=helper_log, stderr=subprocess.STDOUT, start_new_session=True)
            until = time.monotonic() + 10
            while time.monotonic() < until and owned_helper.poll() is None and not helper_ready():
                time.sleep(.2)
            if not helper_ready():
                raise SystemExit('The local AI sign-in helper did not start. Install provider_auth/requirements.txt as described in its README.')
        print('Opening the independent platform/KTX simulator. Use Open AI connection for subscription sign-in; no model call starts here.')
        env = os.environ.copy()
        env['DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE'] = 'false'
        command=[str(editor), '-projectPath', str(PROJECT), '-openfile', str(scene)] if use_editor else [str(player),'-force-metal','-logFile',str(PROJECT/'diagnostics/desktop-player.log')]
        if args.graphics_proof:command+=['-batchmode','-chooguard-graphics-proof',str(PROJECT/'diagnostics/graphics-resume/desktop-proof')]
        editor_process = subprocess.Popen(command, cwd=PROJECT, env=env)
        return editor_process.wait()
    except KeyboardInterrupt:
        if editor_process is not None and editor_process.poll() is None:
            editor_process.terminate()
        return 130
    finally:
        if owned_helper is not None and owned_helper.poll() is None:
            owned_helper.send_signal(signal.SIGINT)
            try:
                owned_helper.wait(timeout=5)
            except subprocess.TimeoutExpired:
                owned_helper.kill()
                owned_helper.wait(timeout=5)
        if helper_log is not None:
            helper_log.close()


if __name__ == '__main__':
    sys.exit(main())
