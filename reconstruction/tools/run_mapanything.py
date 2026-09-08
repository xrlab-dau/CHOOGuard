#!/usr/bin/env python3
"""Offline Apache MapAnything CPU inference and optical-z/camera-pose adapter.

Uses a separate locked environment and verified local DINOv2 source, never Torch Hub
remote fetching. Learned scale is not surveyed metric or collision approval.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import resource
import shutil
import socket
import sys
import threading
import time

import numpy as np

# Tools are also imported directly by synthetic unit tests.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from setup_mapanything import (STATE, SOURCE, DINO_SOURCE, DINO_SHA, REPO_SHA, LOCK,
                              MODEL_REPO, MODEL_REVISION, EXPECTED_WEIGHT_SHA256,
                              EXPECTED_WEIGHT_BYTES, EXPECTED_CONFIG_SHA256,
                              fingerprint, sha, write_json)

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_WEIGHTS_DIR = STATE / 'weights'
DINOV2_HUB_CACHE = DINO_SOURCE
DEFAULT_SETUP = STATE / 'setup-sequence-resume.json'


def rss_bytes():
    value = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
    return int(value if sys.platform == 'darwin' else value * 1024)


def homogeneous_pose(pose):
    e = np.asarray(pose, dtype=np.float64)
    if e.shape == (3, 4):
        e = np.vstack([e, [0, 0, 0, 1]])
    if (e.shape != (4, 4) or not np.isfinite(e).all()
            or not np.allclose(e[3], [0, 0, 0, 1], atol=1e-6)
            or not np.allclose(e[:3, :3] @ e[:3, :3].T, np.eye(3), atol=2e-3)
            or not np.isclose(np.linalg.det(e[:3, :3]), 1, atol=2e-3)):
        raise ValueError('Expected finite proper rigid pose')
    return e


def world_to_camera_from_camera_to_world(pose):
    e = homogeneous_pose(pose)
    r, t = e[:3, :3], e[:3, 3]
    w2c = np.eye(4)
    w2c[:3, :3], w2c[:3, 3] = r.T, -r.T @ t
    return w2c


def block_torch_hub_network(hub_module, *, allow_local_cache=False, expected_fingerprint=None):
    original = hub_module.load

    def local_only(repo, model, *args, **kwargs):
        if (not allow_local_cache or not expected_fingerprint or not DINOV2_HUB_CACHE.is_dir()
                or fingerprint(DINOV2_HUB_CACHE) != expected_fingerprint
                or repo != 'facebookresearch/dinov2' or model != 'dinov2_vitg14'):
            raise RuntimeError('blocked: unaudited Torch Hub source/model')
        # Full Apache state dict supplies the encoder weights, with strict key checking.
        for key in ('source', 'force_reload', 'trust_repo', 'skip_validation', 'pretrained'):
            kwargs.pop(key, None)
        return original(str(DINOV2_HUB_CACHE), model, *args, source='local', pretrained=False, **kwargs)
    hub_module.load = local_only


def block_network():
    def refuse(*args, **kwargs):
        raise RuntimeError('blocked: inference socket network access')
    socket.create_connection = refuse
    socket.socket.connect = refuse
    socket.socket.connect_ex = refuse
    socket.socket.sendto = refuse


def adapt_predictions(predictions):
    """MapAnything infer() arrays retain batch=1; inversion does not rescale optical Z."""
    arrays = {name: [] for name in ('depth', 'conf', 'intrinsics', 'extrinsics', 'processed_images', 'mask')}
    for prediction in predictions:
        def array(key):
            value = prediction[key]
            if hasattr(value, 'detach'):
                value = value.detach().cpu().numpy()
            return np.asarray(value)
        depth, conf = array('depth_z'), array('conf')
        k, pose, image = (array(key) for key in ('intrinsics', 'camera_poses', 'img_no_norm'))
        # apply_mask=False preserves raw nonzero depth; upstream then exposes only
        # non_ambiguous_mask, not its geometry-zeroing composite mask.
        mask = array('mask') if 'mask' in prediction else array('non_ambiguous_mask')[..., None]
        if depth.ndim != 4 or depth.shape[0] != 1 or depth.shape[-1] != 1:
            raise ValueError('Expected batch=1 optical-z grid')
        h, w = depth.shape[1:3]
        if (conf.shape != (1, h, w) or image.shape != (1, h, w, 3)
                or mask.shape != (1, h, w, 1) or k.shape != (1, 3, 3)
                or pose.shape != (1, 4, 4)):
            raise ValueError('Mismatched prediction grids/camera matrices')
        if (not all(np.isfinite(v).all() for v in (depth, conf, k, image))
                or np.any(depth <= 0) or np.any(k[:, (0, 1), (0, 1)] <= 0)
                or not np.allclose(k[0, 2], [0, 0, 1]) or image.min() < 0 or image.max() > 1):
            raise ValueError('Invalid depth, intrinsics or RGB')
        arrays['depth'].append(depth[0, :, :, 0])
        arrays['conf'].append(conf[0])
        arrays['intrinsics'].append(k[0])
        arrays['extrinsics'].append(world_to_camera_from_camera_to_world(pose[0]))
        arrays['processed_images'].append(np.rint(image[0] * 255).astype(np.uint8))
        arrays['mask'].append(mask[0, :, :, 0].astype(bool))
    return {key: np.stack(value) for key, value in arrays.items()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--image', action='append', required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--weights-dir', type=Path, default=DEFAULT_WEIGHTS_DIR)
    parser.add_argument('--setup-receipt', type=Path, default=DEFAULT_SETUP)
    parser.add_argument('--max-seconds', type=float, default=1800)
    parser.add_argument('--max-rss-gib', type=float, default=8)
    args = parser.parse_args()
    if not 1 <= len(args.image) <= 4 or not 1 <= args.max_seconds <= 1800 or not .5 <= args.max_rss_gib <= 8:
        raise ValueError('Invalid bounded image/time/RSS request')
    output = args.output.resolve()
    output.relative_to((ROOT / 'reconstruction/output').resolve())
    if output.exists():
        raise ValueError('Refuse to overwrite previous run')
    from PIL import Image
    inputs = []
    for name in args.image:
        path = Path(name).resolve()
        with Image.open(path) as image:
            if image.getexif().get(274, 1) != 1:
                raise ValueError('Unmapped EXIF rotation')
            width, height = image.size
        inputs.append(dict(path=str(path), sha256=sha(path), width=width, height=height))
    output.mkdir(parents=True)
    receipt_path = output / 'mapanything-receipt.json'
    started = time.monotonic()
    receipt = dict(schemaVersion=2, status='running', engine='mapanything', modelId=MODEL_REPO,
                   modelRevision=MODEL_REVISION, modelLicense='Apache-2.0', inputs=inputs,
                   device='CPU', precision='float32', metricScaleValidated=False,
                   startedAt=datetime.now(timezone.utc).isoformat(), runnerSha256=sha(Path(__file__)),
                   timeBudgetSeconds=args.max_seconds, memoryBudgetBytes=int(args.max_rss_gib * 1024**3),
                   initialFreeDiskBytes=shutil.disk_usage(output).free, networkImageUploads=0,
                   settings=dict(resolution_set=504, resize_mode='fixed_mapping', use_amp=False,
                                 memory_efficient_inference=True, minibatch_size=1),
                   coordinates=dict(rawPose='OpenCV camera-to-world', adaptedPose='world-to-camera',
                                    depth='optical-z', scale='learned scale; not survey validated'),
                   maskPolicy='Preserve upstream non_ambiguous_mask without zeroing raw depth; privacy masks are separate.')
    stop = threading.Event()

    def budget_reason():
        if rss_bytes() > receipt['memoryBudgetBytes']:
            return 'RSS exceeds budget'
        if time.monotonic() - started > args.max_seconds:
            return 'Time exceeds budget'
        if shutil.disk_usage(output).free < 8 * 1024**3:
            return 'Free disk below 8 GiB'
        return None

    def monitor():
        while not stop.wait(.2):
            reason = budget_reason()
            if reason:
                receipt.update(status='resource_budget_exceeded', reason=reason,
                               peakRssBytes=rss_bytes(), elapsedSeconds=time.monotonic() - started)
                write_json(receipt_path, receipt)
                os._exit(70)

    write_json(receipt_path, receipt)
    threading.Thread(target=monitor, daemon=True).start()
    try:
        if budget_reason():
            raise RuntimeError(budget_reason())
        weight = args.weights_dir / 'model.safetensors'
        if not weight.is_file():
            receipt.update(status='blocked_weights_not_locally_available',
                           expectedWeightSha256=EXPECTED_WEIGHT_SHA256, expectedWeightBytes=EXPECTED_WEIGHT_BYTES,
                           reason='Local checkpoint absent; see measured setup receipt, not a school-only decision.')
            return 0
        setup = json.loads(args.setup_receipt.read_text())
        if (setup.get('status') != 'succeeded' or setup['sourceRevision'] != REPO_SHA
                or setup['backboneRevision'] != DINO_SHA
                or fingerprint(SOURCE) != setup['sourceFingerprint']
                or fingerprint(DINO_SOURCE) != setup['backboneFingerprint']
                or sha(LOCK) != setup['requirementsLockSha256']
                or sha(weight) != EXPECTED_WEIGHT_SHA256
                or sha(args.weights_dir / 'config.json') != EXPECTED_CONFIG_SHA256):
            raise ValueError('Pinned source/backbone/lock/weights/config integrity mismatch')
        receipt.update(setupReceiptSha256=sha(args.setup_receipt), weightSha256=sha(weight),
                       requirementsLockSha256=sha(LOCK))
        os.environ.update(HF_HUB_OFFLINE='1', HF_HUB_DISABLE_TELEMETRY='1')
        block_network()
        import torch
        torch.set_num_threads(4)
        block_torch_hub_network(torch.hub, allow_local_cache=True,
                               expected_fingerprint=setup['backboneFingerprint'])
        sys.path.insert(0, str(SOURCE))
        from mapanything.models import MapAnything
        from mapanything.utils.image import load_images
        from safetensors.torch import load_model
        config = json.loads((args.weights_dir / 'config.json').read_text())
        model = MapAnything(**config).float().eval()
        load_model(model, str(weight), strict=True, device='cpu')
        views = load_images(args.image, resize_mode='fixed_mapping', resolution_set=504)
        predictions = model.infer(views, use_amp=False, memory_efficient_inference=True,
                                  minibatch_size=1, apply_mask=False, mask_edges=False)
        # Preserve every raw tensor before adapting poses, masks or display colors.
        raw = {f'view_{i}_{key}': value.detach().cpu().numpy()
               for i, row in enumerate(predictions) for key, value in row.items()
               if torch.is_tensor(value)}
        np.savez_compressed(output / 'raw-mapanything.npz', **raw)
        arrays = adapt_predictions(predictions)
        np.savez_compressed(output / 'prediction.npz', **arrays)
        receipt.update(status='succeeded', outputs=[dict(file=name, sha256=sha(output / name),
                       bytes=(output / name).stat().st_size)
                       for name in ('raw-mapanything.npz', 'prediction.npz')],
                       arrays={key: dict(shape=list(value.shape), dtype=str(value.dtype))
                               for key, value in arrays.items()})
    except Exception as error:
        receipt.update(status='failed', error=f'{type(error).__name__}: {error}')
        return 1
    finally:
        stop.set()
        receipt.update(peakRssBytes=rss_bytes(), elapsedSeconds=time.monotonic() - started,
                       finalFreeDiskBytes=shutil.disk_usage(output).free)
        write_json(receipt_path, receipt)
        print(json.dumps({k: receipt[k] for k in ('status', 'peakRssBytes', 'elapsedSeconds')}))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
