#!/usr/bin/env python3
"""Run local float32 DA3 Small/Base inference and retain raw CV-coordinate predictions.

No image uploads, mesh conversion, Gaussian generation or Unity operations.
Default order is photo B then A, following the separately audited capture chronology.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import gc
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import platform
import resource
import sys
import threading
import time

ROOT = Path(__file__).resolve().parents[2]
STATE = ROOT / '.local/reconstruction'
SOURCE = STATE / 'Depth-Anything-3'
EXPECTED_REVISION = '3d835ec1a5802d64a8b8b15f817a1ab54809bfe4'
MODELS = {
    'SMALL': ('depth-anything/DA3-SMALL', 'e08cab65ca0ec38e7826075418411ab90cab4da3',
              '364492e38a3a06d221ac75da7f6621ada3f2361cd24fde11ba79091e9f40efcf', 'setup-receipt.json'),
    'BASE': ('depth-anything/DA3-BASE', 'f4a6c9b3c95e41c82048423d3493a81ec3fa810e',
             'e01067dc1659613083d9145a9a2547ccdbe6ccbbf83c4fe7b3e8a4e2bdae78b5', 'setup-receipt-base.json'),
}
DEFAULT_IMAGES = [ROOT / '.planning/2026-09-07-object-reference-audit/references/furniture' / name
                  for name in ('busan-concourse-b.jpg', 'busan-concourse-a.jpg')]


def sha(path: Path) -> str:
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def source_fingerprint() -> str:
    files = sorted(p for p in (SOURCE / 'src/depth_anything_3').rglob('*')
                   if p.is_file() and p.suffix in {'.py', '.yaml', '.yml'})
    return hashlib.sha256('\n'.join(str(p.relative_to(SOURCE))+':'+sha(p) for p in files).encode()).hexdigest()


def write_json(path: Path, value: dict) -> None:
    temporary = path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')
    os.replace(temporary, path)


def rss_bytes() -> int:
    value = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
    return int(value if sys.platform == 'darwin' else value * 1024)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', choices=tuple(MODELS), default='SMALL')
    parser.add_argument('--image', action='append', help='Ordered local input path; repeat per image.')
    parser.add_argument('--output', help='New output directory; separate default for each model.')
    parser.add_argument('--process-res', type=int, default=336)
    parser.add_argument('--device', choices=('auto', 'mps', 'cpu'), default='auto')
    parser.add_argument('--cpu-fallback', action='store_true', help='Record an MPS failure and explicitly retry on CPU.')
    parser.add_argument('--max-process-gib', type=float, default=8.0)
    parser.add_argument('--max-mps-gib', type=float, default=7.0)
    args = parser.parse_args()
    model_id, model_revision, expected_weight_sha, setup_name = MODELS[args.model]
    if not 56 <= args.process_res <= 504 or args.process_res % 14:
        raise ValueError('process-res must be a multiple of 14 in [56, 504].')
    if not 1 <= args.max_process_gib <= 10 or not 1 <= args.max_mps_gib <= 9:
        raise ValueError('Memory budgets exceed this bounded 16 GiB workstation runner.')
    images = [Path(x).resolve() for x in args.image] if args.image else DEFAULT_IMAGES
    if not 1 <= len(images) <= 4 or not all(p.is_file() for p in images):
        raise ValueError('Provide one to four existing local images.')
    setup_path = STATE / setup_name
    setup = json.loads(setup_path.read_text())
    if setup['modelId'] != model_id or setup['modelRevision'] != model_revision:
        raise ValueError('Model identity differs from the audited requested checkpoint.')
    if setup['sourceRevision'] != EXPECTED_REVISION or setup['sourceFingerprint'] != source_fingerprint():
        raise ValueError('DA3 source changed after the audited setup.')
    if setup['requirementsLockSha256'] != sha(ROOT / 'reconstruction/requirements-da3.lock'):
        raise ValueError('Dependency lock differs from setup receipt.')
    model_dir = ROOT / setup['modelPath']
    if sha(model_dir / 'model.safetensors') != expected_weight_sha:
        raise ValueError('Checkpoint integrity mismatch.')
    default_output = 'busan-concourse-pilot' if args.model == 'SMALL' else 'busan-concourse-base-pilot'
    output = Path(args.output or 'reconstruction/output/' + default_output)
    if not output.is_absolute():
        output = ROOT / output
    output = output.resolve()
    output.relative_to((ROOT / 'reconstruction/output').resolve())
    output.mkdir(parents=True, exist_ok=True)
    receipt_path, npz_path = output / 'inference-receipt.json', output / 'prediction.npz'
    if receipt_path.exists() or npz_path.exists():
        raise ValueError('Output already contains an inference result; use another run directory.')
    # Local files only: model loading and inference must not contact the Hub.
    os.environ['HF_HUB_OFFLINE'] = '1'
    os.environ['HF_HUB_DISABLE_TELEMETRY'] = '1'
    sys.path.insert(0, str(SOURCE / 'src'))
    import numpy as np
    from PIL import Image
    import torch
    from depth_anything_3.api import DepthAnything3

    torch.set_num_threads(4)
    available = bool(torch.backends.mps.is_available())
    if args.device == 'mps' and not available:
        raise ValueError('MPS was requested but is unavailable.')
    device = 'mps' if args.device == 'mps' or (args.device == 'auto' and available) else 'cpu'
    inputs = []
    for index, path in enumerate(images):
        with Image.open(path) as im:
            width, height = im.size
        try:
            display_path = str(path.relative_to(ROOT))
        except ValueError:
            display_path = path.name
        inputs.append({'index': index, 'path': display_path, 'sha256': sha(path),
                       'bytes': path.stat().st_size, 'width': width, 'height': height})
    receipt = {'schemaVersion': 1, 'status': 'running',
               'startedAt': datetime.now(timezone.utc).isoformat(), 'inputs': inputs,
               'inputOrder': 'B then A' if images == DEFAULT_IMAGES else 'explicit command-line order',
               'modelId': setup['modelId'], 'modelRevision': setup['modelRevision'],
               'modelLicense': setup['modelLicense'], 'weightSha256': expected_weight_sha,
               'setupReceiptSha256': sha(setup_path), 'sourceRevision': EXPECTED_REVISION,
               'apiPatchedSha256': setup['apiPatchedSha256'],
               'requirementsLockSha256': setup['requirementsLockSha256'],
               'runnerSha256': sha(Path(__file__)), 'python': platform.python_version(),
               'os': platform.platform(), 'mpsAvailable': available, 'deviceRequested': args.device,
               'precision': 'float32; non-CUDA autocast disabled',
               'settings': {'process_res': args.process_res, 'process_res_method': 'upper_bound_resize',
                            'infer_gs': False, 'use_ray_pose': False, 'ref_view_strategy': 'first',
                            'export_dir': None},
               'coordinates': {'extrinsics': 'OpenCV/COLMAP world-to-camera; shape recorded below',
                               'cameraAxes': 'x right, y down, z forward',
                               'depth': 'camera Z-depth in relative model scale',
                               'intrinsics': 'pixel K for returned processed_images/depth grid',
                               'appliedExportTransform': None, 'units': 'relative_scale_unknown'},
               'networkImageUploads': 0, 'meshExported': False, 'facilityFidelityValidated': False,
               'memoryBudgetGiB': {'processRss': args.max_process_gib, 'mpsDriver': args.max_mps_gib},
               'attempts': [], 'dependencies': {name: importlib.metadata.version(name) for name in
                   ('torch','torchvision','numpy','Pillow','opencv-python-headless','einops',
                    'omegaconf','addict','huggingface-hub','safetensors','imageio','tqdm')}}
    write_json(receipt_path, receipt)
    stop = threading.Event()
    peaks = {'processRssBytes': rss_bytes(), 'mpsAllocatedBytes': 0, 'mpsDriverBytes': 0}
    def monitor() -> None:
        while not stop.wait(.5):
            peaks['processRssBytes'] = max(peaks['processRssBytes'], rss_bytes())
            if available:
                peaks['mpsAllocatedBytes'] = max(peaks['mpsAllocatedBytes'], torch.mps.current_allocated_memory())
                peaks['mpsDriverBytes'] = max(peaks['mpsDriverBytes'], torch.mps.driver_allocated_memory())
            if peaks['processRssBytes'] > args.max_process_gib * 1024**3 or peaks['mpsDriverBytes'] > args.max_mps_gib * 1024**3:
                receipt.update(status='memory_budget_exceeded', peakMemory=peaks.copy())
                write_json(receipt_path, receipt)
                os._exit(70)
    threading.Thread(target=monitor, name='da3-memory-budget', daemon=True).start()
    started = time.perf_counter()
    try:
        model = DepthAnything3.from_pretrained(str(model_dir), local_files_only=True).float().eval()
        arrays = None
        devices = [device] + (['cpu'] if device == 'mps' and args.cpu_fallback else [])
        for attempt_device in devices:
            attempt = {'device': attempt_device, 'status': 'running'}
            receipt['attempts'].append(attempt)
            attempt_start = time.perf_counter()
            try:
                model = model.to(attempt_device).float()
                # Upstream caches this attribute; reset it when explicitly retrying on CPU.
                model.device = torch.device(attempt_device)
                if attempt_device == 'mps':
                    torch.mps.set_per_process_memory_fraction(.50)
                    torch.mps.synchronize()
                prediction = model.inference([str(p) for p in images], **receipt['settings'])
                if attempt_device == 'mps':
                    torch.mps.synchronize()
                candidate = {name: getattr(prediction, name) for name in
                             ('depth','conf','extrinsics','intrinsics','processed_images')}
                if any(value is None for value in candidate.values()):
                    raise ValueError('Required prediction field is missing.')
                for name in ('depth','conf','extrinsics','intrinsics'):
                    candidate[name] = np.asarray(candidate[name], dtype=np.float32)
                    if not np.isfinite(candidate[name]).all():
                        raise ValueError('Nonfinite prediction field: ' + name)
                n, h, w = candidate['depth'].shape
                if n != len(images) or candidate['conf'].shape != (n,h,w) or candidate['processed_images'].shape != (n,h,w,3):
                    raise ValueError('Depth, color and confidence grids do not match.')
                if candidate['extrinsics'].shape not in ((n,3,4),(n,4,4)) or candidate['intrinsics'].shape != (n,3,3):
                    raise ValueError('Unexpected camera matrix shape.')
                if not (candidate['depth'] > 0).all() or not (candidate['intrinsics'][:,(0,1),(0,1)] > 0).all():
                    raise ValueError('Nonpositive depth or focal length.')
                arrays = candidate
                attempt.update(status='succeeded', seconds=time.perf_counter()-attempt_start)
                receipt['deviceUsed'] = attempt_device
                receipt['modelIsMetric'] = bool(prediction.is_metric)
                break
            except Exception as error:
                attempt.update(status='failed', seconds=time.perf_counter()-attempt_start,
                               errorType=type(error).__name__, error=str(error)[:1200])
                write_json(receipt_path, receipt)
                if attempt_device != 'mps' or not args.cpu_fallback:
                    raise
                print('MPS inference failed; explicit --cpu-fallback retry follows.', flush=True)
                model = model.to('cpu')
                model.device = torch.device('cpu')
                gc.collect()
                torch.mps.empty_cache()
        if arrays is None:
            raise RuntimeError('No inference attempt produced valid arrays.')
        temporary = npz_path.with_suffix('.npz.tmp')
        with temporary.open('wb') as stream:
            np.savez_compressed(stream, **arrays)
        os.replace(temporary, npz_path)
        peaks['processRssBytes'] = max(peaks['processRssBytes'], rss_bytes())
        receipt.update(status='succeeded', finishedAt=datetime.now(timezone.utc).isoformat(),
                       elapsedSeconds=time.perf_counter()-started, peakMemory=peaks.copy(),
                       output={'file': 'prediction.npz', 'sha256': sha(npz_path), 'bytes': npz_path.stat().st_size,
                               'arrays': {name: {'shape': list(value.shape), 'dtype': str(value.dtype)} for name,value in arrays.items()}},
                       depthRange=[float(arrays['depth'].min()),float(arrays['depth'].max())],
                       confidenceRange=[float(arrays['conf'].min()),float(arrays['conf'].max())])
        write_json(receipt_path, receipt)
        print(json.dumps({'status': receipt['status'], 'device': receipt['deviceUsed'],
                          'seconds': receipt['elapsedSeconds'], 'peakMemory': receipt['peakMemory'],
                          'output': str(npz_path.relative_to(ROOT))}))
        return 0
    except Exception as error:
        receipt.update(status='failed', elapsedSeconds=time.perf_counter()-started,
                       peakMemory=peaks.copy(), errorType=type(error).__name__, error=str(error)[:1200])
        write_json(receipt_path, receipt)
        raise
    finally:
        stop.set()


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except Exception as error:
        print('DA3 inference failed: ' + str(error), file=sys.stderr)
        raise SystemExit(1)
