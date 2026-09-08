#!/usr/bin/env python3
"""Install an isolated, pinned DA3 Small/Base environment after explicit user approval.

No repository clone, image transfer, CUDA extension, Gaussian renderer or app server.
The upstream checkout must already exist at the exact revision below.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
STATE = ROOT / '.local/reconstruction'
SOURCE = STATE / 'Depth-Anything-3'
VENV = STATE / 'venv'
REVISION = '3d835ec1a5802d64a8b8b15f817a1ab54809bfe4'
MODELS = {
    'SMALL': {'id': 'depth-anything/DA3-SMALL',
              'revision': 'e08cab65ca0ec38e7826075418411ab90cab4da3',
              'weightSha256': '364492e38a3a06d221ac75da7f6621ada3f2361cd24fde11ba79091e9f40efcf',
              'weightBytes': 137248940, 'configBlob': 'd2fb247e43e97657fa2b8c7613848f567e8ee556',
              'receipt': 'setup-receipt.json'},
    'BASE': {'id': 'depth-anything/DA3-BASE',
             'revision': 'f4a6c9b3c95e41c82048423d3493a81ec3fa810e',
             'weightSha256': 'e01067dc1659613083d9145a9a2547ccdbe6ccbbf83c4fe7b3e8a4e2bdae78b5',
             'weightBytes': 541518028, 'configBlob': '4fbbc20883d49b5c1d06683753c8ad1a8d4a97e5',
             'receipt': 'setup-receipt-base.json'},
}
API_UPSTREAM_SHA256 = '042ae8f6bfd1e5610100a585ebb00f8cbc3320f9aa9e03a07530fd4deda8d2d8'
API_RELATIVE = Path('src/depth_anything_3/api.py')
LOCK = ROOT / 'reconstruction/requirements-da3.lock'

ORIGINAL_FORWARD = '''        # Determine optimal autocast dtype
        autocast_dtype = torch.bfloat16 if torch.cuda.is_bf16_supported() else torch.float16
        with torch.no_grad():
            with torch.autocast(device_type=image.device.type, dtype=autocast_dtype):
                return self.model(
                    image, extrinsics, intrinsics, export_feat_layers, infer_gs, use_ray_pose, ref_view_strategy
                )
'''
PATCHED_FORWARD = '''        # CHOOGuard: CUDA-only AMP; initial MPS/CPU execution remains float32.
        amp_context = nullcontext()
        if image.device.type == "cuda":
            autocast_dtype = torch.bfloat16 if torch.cuda.is_bf16_supported() else torch.float16
            amp_context = torch.autocast(device_type="cuda", dtype=autocast_dtype)
        with torch.no_grad(), amp_context:
            return self.model(
                image, extrinsics, intrinsics, export_feat_layers, infer_gs, use_ray_pose, ref_view_strategy
            )
'''
PATCHES = (
    ('nullcontext', 'import time\n', 'import time\nfrom contextlib import nullcontext\n'),
    ('remove_eager_export', 'from depth_anything_3.utils.export import export\n', ''),
    ('remove_eager_pose_alignment', 'from depth_anything_3.utils.pose_align import align_poses_umeyama\n', ''),
    ('non_cuda_float32', ORIGINAL_FORWARD, PATCHED_FORWARD),
    ('lazy_pose_alignment',
     '        if extrinsics is None:\n            return prediction\n        prediction.intrinsics = intrinsics.numpy()\n',
     '        if extrinsics is None:\n            return prediction\n        from depth_anything_3.utils.pose_align import align_poses_umeyama\n        prediction.intrinsics = intrinsics.numpy()\n'),
    ('lazy_export',
     '        """Export results to specified format and directory."""\n        start_time = time.time()\n',
     '        """Export results to specified format and directory."""\n        from depth_anything_3.utils.export import export\n        start_time = time.time()\n'),
)


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def source_revision(source: Path) -> str:
    git_dir = source / '.git'
    if not git_dir.is_dir():
        raise ValueError('Expected an existing ordinary pinned checkout with a .git directory.')
    head = (git_dir / 'HEAD').read_text().strip()
    if head.startswith('ref: '):
        ref = head[5:]
        parts = PurePosixPath(ref)
        if parts.is_absolute() or '..' in parts.parts:
            raise ValueError('Invalid checkout ref path.')
        loose = git_dir / ref
        if loose.is_file():
            head = loose.read_text().strip()
        else:
            packed = git_dir / 'packed-refs'
            matches = [line.split()[0] for line in packed.read_text().splitlines()
                       if line and not line.startswith(('#', '^')) and line.split()[1] == ref]
            if len(matches) != 1:
                raise ValueError('Cannot resolve the pinned checkout revision.')
            head = matches[0]
    if head != REVISION:
        raise ValueError('Upstream checkout revision differs from the audited revision.')
    return head


def patched_api(original: bytes) -> bytes:
    if hashlib.sha256(original).hexdigest() != API_UPSTREAM_SHA256:
        raise ValueError('Upstream api.py does not match the audited source hash.')
    text = original.decode('utf-8')
    for label, before, after in PATCHES:
        count = text.count(before)
        if count != 1:
            raise ValueError(f'Patch {label} expected one match, observed {count}.')
        text = text.replace(before, after, 1)
    return text.encode('utf-8')


def verify_patched_api(value: bytes) -> bytes:
    """Reverse the exact six edits, prove upstream hash, then reproduce the patch."""
    text = value.decode('utf-8')
    # Removed imports are restored at their unique original neighbors.
    reversals = (
        (PATCHES[5][2], PATCHES[5][1]),
        (PATCHES[4][2], PATCHES[4][1]),
        (PATCHED_FORWARD, ORIGINAL_FORWARD),
        ('from depth_anything_3.specs import Prediction\n',
         'from depth_anything_3.specs import Prediction\nfrom depth_anything_3.utils.export import export\n'),
        ('from depth_anything_3.utils.logger import logger\n',
         'from depth_anything_3.utils.logger import logger\nfrom depth_anything_3.utils.pose_align import align_poses_umeyama\n'),
        (PATCHES[0][2], PATCHES[0][1]),
    )
    for before, after in reversals:
        if text.count(before) != 1:
            raise ValueError('Existing api.py is neither audited upstream nor the exact local patch.')
        text = text.replace(before, after, 1)
    original = text.encode('utf-8')
    if patched_api(original) != value:
        raise ValueError('Patched api.py contains unexpected changes.')
    return value


def source_fingerprint(source: Path) -> str:
    files = sorted(path for path in (source / 'src/depth_anything_3').rglob('*')
                   if path.is_file() and path.suffix in {'.py', '.yaml', '.yml'})
    records = '\n'.join(str(path.relative_to(source)) + ':' + digest(path) for path in files)
    return hashlib.sha256(records.encode()).hexdigest()


def atomic_json(path: Path, value: dict) -> None:
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')
    os.replace(temporary, path)


def download(filename: str, model: dict, *, sha256: str | None = None, blob: str | None = None,
             size: int | None = None) -> dict:
    target = STATE / 'models' / model['id'].split('/')[-1] / filename
    url = f'https://huggingface.co/{model["id"]}/resolve/{model["revision"]}/{filename}'
    def valid(path: Path) -> bool:
        if not path.is_file() or (size is not None and path.stat().st_size != size):
            return False
        if sha256 is not None and digest(path) != sha256:
            return False
        if blob is not None:
            data = path.read_bytes()
            if hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest() != blob:
                return False
        return True
    if target.exists() and not valid(target):
        raise ValueError('Existing model file failed integrity validation: ' + filename)
    if not target.exists():
        temporary = target.with_suffix(target.suffix + '.part')
        request = urllib.request.Request(url, headers={'User-Agent': 'CHOOGuard-local-model-setup'})
        with urllib.request.urlopen(request, timeout=90) as response, temporary.open('wb') as stream:
            shutil.copyfileobj(response, stream, length=1024 * 1024)
        if not valid(temporary):
            raise ValueError('Downloaded model file failed integrity validation: ' + filename)
        os.replace(temporary, target)
    return {'file': filename, 'bytes': target.stat().st_size, 'sha256': digest(target), 'url': url}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', choices=tuple(MODELS), default='SMALL')
    parser.add_argument('--check-only', action='store_true', help='Check source and exact patch without installation or writes.')
    args = parser.parse_args()
    model = MODELS[args.model]
    model_dir = STATE / 'models' / model['id'].split('/')[-1]
    source_revision(SOURCE)
    api = SOURCE / API_RELATIVE
    original = api.read_bytes()
    if hashlib.sha256(original).hexdigest() == API_UPSTREAM_SHA256:
        patched = patched_api(original)
    else:
        patched = verify_patched_api(original)
    patch_sha = hashlib.sha256(patched).hexdigest()
    if args.check_only:
        print(json.dumps({'sourceRevision': REVISION, 'patchCount': len(PATCHES),
                          'patchedApiSha256': patch_sha, 'scope': 'source preflight only'}))
        return 0
    if not LOCK.is_file():
        raise ValueError('Missing reviewed dependency lock; compile requirements-da3.in before setup.')
    if shutil.disk_usage(STATE).free < 5 * 1024**3:
        raise ValueError('Less than 5 GiB free: stop before setup.')
    uv = shutil.which('uv')
    if uv is None:
        raise ValueError('uv is required; this script does not install package managers.')
    if original != patched:
        api.write_bytes(patched)
    if not (VENV / 'bin/python').exists():
        subprocess.run([uv, 'venv', '--python', '3.11', str(VENV)], check=True)
    subprocess.run([uv, 'pip', 'sync', '--python', str(VENV / 'bin/python'),
                    '--require-hashes', str(LOCK)], check=True)
    model_dir.mkdir(parents=True, exist_ok=True)
    downloads = [download('config.json', model, blob=model['configBlob']),
                 download('model.safetensors', model, sha256=model['weightSha256'], size=model['weightBytes'])]
    receipt = {'sourceRevision': REVISION, 'sourcePath': str(SOURCE.relative_to(ROOT)),
               'apiUpstreamSha256': API_UPSTREAM_SHA256, 'apiPatchedSha256': patch_sha,
               'patchCount': len(PATCHES), 'patches': [p[0] for p in PATCHES],
               'sourceFingerprint': source_fingerprint(SOURCE),
               'requirementsLockSha256': digest(LOCK), 'modelId': model['id'],
               'modelRevision': model['revision'], 'modelLicense': 'Apache-2.0',
               'modelPath': str(model_dir.relative_to(ROOT)), 'downloads': downloads,
               'environment': str(VENV.relative_to(ROOT)),
               'excluded': ['xformers', 'gsplat', 'open3d', 'pycolmap', 'e3nn', 'moviepy', 'app server'],
               'inferenceRun': False}
    atomic_json(STATE / model['receipt'], receipt)
    print(json.dumps({'status': 'setup_complete', 'modelId': model['id'],
                      'weightBytes': model['weightBytes'], 'patchCount': len(PATCHES)}))
    return 0


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        print('DA3 setup failed: ' + str(error), file=sys.stderr)
        raise SystemExit(1)
