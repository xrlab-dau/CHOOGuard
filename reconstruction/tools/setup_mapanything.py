#!/usr/bin/env python3
"""Install the pinned Apache CPU arm in its own locked environment (approved local setup).

Only public code/packages/weights are downloaded. Inference never downloads a backbone.
Prior setup receipts are preserved; disk/time failures remain measured blocked results.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import time

ROOT = Path(__file__).resolve().parents[2]
STATE = ROOT / '.local/reconstruction/mapanything'
SOURCE = STATE / 'map-anything'
REPO_URL = 'https://github.com/facebookresearch/map-anything.git'
REPO_SHA = '3d10cf7a3016fc0f9bb13a071ee66c47b10be0d9'
DINO_SHA = '7764ea0f912e53c92e82eb78a2a1631e92725fc8'
DINO_SOURCE = STATE / 'dinov2'
MODEL_REPO = 'facebook/map-anything-apache'
MODEL_REVISION = '00f9c245bbcb60522d1ed7f9e9d88462c6e3f38a'
EXPECTED_CONFIG_SHA256 = '65701d09d99ed37a21d295f0d138978b3d584ab3bccdbcb4a2853da212b676c5'
EXPECTED_WEIGHT_SHA256 = 'fa06c0fdccefc5048e072c85935d5789b1e36b307f3859033c17f9dcb9fd5201'
EXPECTED_WEIGHT_BYTES = 4914062480
LOCK = ROOT / 'reconstruction/requirements-mapanything.lock'
VENV = ROOT / '.local/reconstruction/mapanything-venv'


def sha(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def fingerprint(directory):
    files = sorted(p for p in directory.rglob('*') if p.is_file()
                   and '.git' not in p.parts and '__pycache__' not in p.parts)
    return hashlib.sha256('\n'.join(str(p.relative_to(directory)) + ':' + sha(p)
                                    for p in files).encode()).hexdigest()


def write_json(path, data):
    temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(data, indent=2) + '\n')
    os.replace(temporary, path)


def verified_checkout(path, url, revision, run):
    if not path.exists():
        run(['git', 'clone', '--filter=blob:none', '--no-checkout', url, str(path)])
        run(['git', '-C', str(path), 'checkout', '--detach', revision])
    head = subprocess.check_output(['git', '-C', str(path), 'rev-parse', 'HEAD'], text=True).strip()
    dirty = subprocess.check_output(['git', '-C', str(path), 'status', '--porcelain'], text=True).strip()
    if head != revision or dirty:
        raise ValueError('Source checkout differs from pinned clean revision: ' + path.name)
    if 'Apache License' not in (path / 'LICENSE').read_text():
        raise ValueError('Expected Apache license')
    return fingerprint(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True, help='New local setup receipt filename')
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError('Refuse to overwrite setup evidence')
    args.output.resolve().relative_to(STATE.resolve())
    STATE.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    receipt = dict(schemaVersion=2, status='running', startedAt=datetime.now(timezone.utc).isoformat(),
                   modelRevision=MODEL_REVISION, sourceRevision=REPO_SHA, backboneRevision=DINO_SHA,
                   initialFreeDiskBytes=shutil.disk_usage(STATE).free, timeBudgetSeconds=1800,
                   minFreeDiskBytes=8 * 1024**3, requirementsLockSha256=sha(LOCK),
                   runnerSha256=sha(Path(__file__)), commands=[])

    def run(command):
        if shutil.disk_usage(STATE).free < 8 * 1024**3:
            raise RuntimeError('blocked_disk_budget: less than 8 GiB free')
        remaining = 1800 - (time.monotonic() - started)
        if remaining <= 0:
            raise TimeoutError('setup exceeded 1800 seconds')
        from run_sequence_pilot import run_bounded_command
        result = run_bounded_command(command, timeout=remaining)
        receipt['commands'].append(dict(command=command, **result))
        if result['status'] != 'exited' or result.get('returncode') != 0:
            raise RuntimeError('Setup command blocked/failed: ' + result['status'] + ': '
                               + result.get('stderrTail', '')[-1000:])

    try:
        weight = STATE / 'weights/model.safetensors'
        required = 8 * 1024**3 + (0 if weight.exists() else EXPECTED_WEIGHT_BYTES) + 2 * 1024**3
        receipt['requiredFreeDiskBytesBeforeSetup'] = required
        if receipt['initialFreeDiskBytes'] < required:
            raise RuntimeError('blocked_disk_budget: measured free disk below weight + 2 GiB setup reserve + 8 GiB floor')
        receipt['sourceFingerprint'] = verified_checkout(SOURCE, REPO_URL, REPO_SHA, run)
        receipt['backboneFingerprint'] = verified_checkout(
            DINO_SOURCE, 'https://github.com/facebookresearch/dinov2.git', DINO_SHA, run)
        if not VENV.exists():
            run(['uv', 'venv', '--python', '3.11', str(VENV)])
        run(['uv', 'pip', 'sync', '--python', str(VENV / 'bin/python'), '--require-hashes', str(LOCK)])
        weight.parent.mkdir(exist_ok=True)
        for filename, digest in [('config.json', EXPECTED_CONFIG_SHA256),
                                 ('model.safetensors', EXPECTED_WEIGHT_SHA256)]:
            target = weight.parent / filename
            if not target.exists():
                partial = target.with_suffix(target.suffix + '.partial')
                if partial.exists():
                    raise ValueError('Previous partial download preserved; inspect it before retry')
                run(['curl', '--fail', '--location', '--silent', '--show-error', '--max-time',
                     str(max(1, int(1800 - (time.monotonic() - started)))),
                     f'https://huggingface.co/{MODEL_REPO}/resolve/{MODEL_REVISION}/{filename}',
                     '--output', str(partial)])
                if sha(partial) != digest:
                    raise ValueError('Downloaded hash mismatch: ' + filename)
                partial.rename(target)
            if sha(target) != digest:
                raise ValueError('Local hash mismatch: ' + filename)
        receipt.update(status='succeeded', weightSha256=sha(weight), configSha256=EXPECTED_CONFIG_SHA256,
                       environmentBuilt=True, backbonePretrained=False,
                       note='Full Apache checkpoint supplies encoder weights; strict state load required.')
    except Exception as error:
        receipt.update(status='blocked_setup', error=f'{type(error).__name__}: {error}')
    receipt.update(elapsedSeconds=time.monotonic() - started,
                   finalFreeDiskBytes=shutil.disk_usage(STATE).free)
    write_json(args.output, receipt)
    print(json.dumps({k: receipt[k] for k in ('status', 'elapsedSeconds', 'finalFreeDiskBytes')}))
    return 0 if receipt['status'] == 'succeeded' else 2


if __name__ == '__main__':
    raise SystemExit(main())
