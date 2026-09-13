#!/usr/bin/env python3
"""Run the dedicated native static review player; retain originals and report metadata only.

This is a capture receipt, never visual acceptance. Root owns blind labels and critic handoff.
"""
import argparse
import hashlib
import json
import os
import plistlib
import signal
import struct
import subprocess
from pathlib import Path


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def validate_frames(directory):
    directory = Path(directory)
    receipt = json.loads((directory / 'capture-complete.json').read_text())
    if (receipt['Width'], receipt['Height']) != (1920, 1080) or receipt['GraphicsApi'] == 'Null':
        raise ValueError('A native graphics device and exact1920x1080 are required.')
    frames = []
    for shot in receipt['Shots']:
        name = shot['Shot']['FileName']
        if Path(name).name != name or not name.endswith('.png'):
            raise ValueError('Invalid frame path.')
        file = directory / name
        with file.open('rb') as stream:
            header = stream.read(24)
        if header[:8] != b'\x89PNG\r\n\x1a\n' or header[12:16] != b'IHDR' or struct.unpack('>II', header[16:24]) != (1920, 1080):
            raise ValueError('Frame is not native1920x1080 PNG.')
        digest = sha(file)
        if digest != shot['Sha256'] or file.stat().st_size != shot['Bytes']:
            raise ValueError('Frame hash or size changed after capture.')
        frames.append({'Path': name, 'Sha256': digest, 'ViewId': shot['Shot']['ViewId']})
    if len(frames) != 20 or len({f['Path'] for f in frames}) != 20:
        raise ValueError('Expected all20 unique static review frames.')
    return frames


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, default=Path.cwd())
    parser.add_argument('--app', type=Path)
    parser.add_argument('--session', type=Path, required=True)
    args = parser.parse_args()
    project = args.project.resolve()
    app = (args.app or project / 'Builds/FoundationReviewMac/ChooGuardReview.app').resolve()
    with (app / 'Contents/Info.plist').open('rb') as stream:
        executable = plistlib.load(stream)['CFBundleExecutable']
    if Path(executable).name != executable:
        raise ValueError('Invalid application executable name.')
    binary = app / 'Contents/MacOS' / executable
    session = args.session.resolve()
    session.mkdir(mode=0o700)
    command = [str(binary), '-screen-width', '1920', '-screen-height', '1080', '-screen-fullscreen', '0',
               '-logFile', str(session / 'player.log'), '--cg-review-captures', str(session / 'frames')]
    code = None
    with (session / 'stdio.log').open('xb') as stream:
        process = subprocess.Popen(command, cwd=project, stdin=subprocess.DEVNULL, stdout=stream,
                                   stderr=subprocess.STDOUT, start_new_session=True)
        try:
            code = process.wait(timeout=90)
        finally:
            if process.poll() is None:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait(timeout=5)
    if code != 0:
        raise RuntimeError('Native review player failed or timed out.')
    frames = validate_frames(session / 'frames')
    text = (session / 'player.log').read_text(errors='replace').lower()
    if any(marker in text for marker in ('exception:', 'fatal error', 'review requires')):
        raise RuntimeError('Native player reported an error.')
    receipt = {'Status': 'CAPTURED_NOT_VISUALLY_REVIEWED', 'BinarySha256': sha(binary),
               'PayloadHashes': {str(p.relative_to(app)): sha(p) for p in app.rglob('*') if p.is_file()},
               'ExitCode': code, 'Frames': frames,
               'Limits': 'StaticV01-V09 only; see frame receipt for missing views. No anonymous comparison, visual PASS or performance acceptance.'}
    (session / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps({'Status': receipt['Status'], 'FrameCount': len(frames), 'Width': 1920, 'Height': 1080}))


if __name__ == '__main__':
    main()
