#!/usr/bin/env python3
"""Bounded local CPU SIFT/two-view probe; no dense reconstruction or metric claim.

F/H estimates use image correspondences only. Relative pose additionally depends
on optional, explicitly identified DA3 intrinsics. No DA3 pose initializes COLMAP.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import platform
import resource
import shutil
import sys
import threading
import time

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
IMAGE_DIR = ROOT / 'private-data/public-concourse-pilot'
NAMES = ['busan-concourse-b.jpg', 'busan-concourse-a.jpg']
LOCK = ROOT / 'reconstruction/requirements-colmap.lock'


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, data: dict) -> None:
    tmp = path.with_suffix('.json.tmp')
    tmp.write_text(json.dumps(data, indent=2, ensure_ascii=False, allow_nan=False) + '\n')
    os.replace(tmp, path)


def rescale_intrinsics(k: np.ndarray, processed_hw: tuple, original_hw: tuple) -> np.ndarray:
    k = np.asarray(k, dtype=np.float64)
    if (k.shape != (3, 3) or not np.isfinite(k).all() or k[0, 0] <= 0 or k[1, 1] <= 0
            or not np.allclose(k[2], [0, 0, 1]) or k[0, 1] != 0 or k[1, 0] != 0):
        raise ValueError('Expected finite, positive-focal, zero-skew PINHOLE intrinsics.')
    if min(*processed_hw, *original_hw) <= 0:
        raise ValueError('Image dimensions must be positive.')
    scale = np.diag([original_hw[1] / processed_hw[1],
                     original_hw[0] / processed_hw[0], 1.])
    return scale @ k


def load_calibration(npz: Path, images: list[Path], image_hw: list[tuple]) -> tuple:
    receipt_path = npz.parent / 'inference-receipt.json'
    receipt = json.loads(receipt_path.read_text())
    if receipt['status'] != 'succeeded' or receipt['output']['sha256'] != sha(npz):
        raise ValueError('Successful inference receipt and prediction SHA must agree.')
    if [i['sha256'] for i in receipt['inputs']] != [sha(p) for p in images]:
        raise ValueError('Calibration input hashes/order differ from the current B→A pair.')
    if [(i['height'], i['width']) for i in receipt['inputs']] != image_hw:
        raise ValueError('Calibration input dimensions differ from original pixels.')
    if receipt['settings']['process_res_method'] != 'upper_bound_resize':
        raise ValueError('Only the audited pure resize method is supported; no crop/EXIF guess.')
    with np.load(npz, allow_pickle=False) as arrays:
        processed = arrays['processed_images']
        all_k = arrays['intrinsics'].copy()
        if processed.ndim != 4 or processed.shape[0] != 2 or all_k.shape != (2, 3, 3):
            raise ValueError('Expected exactly two processed images and K matrices.')
        hw = processed.shape[1:3]
    for original in image_hw:
        if abs(original[1] / original[0] - hw[1] / hw[0]) > 1e-6:
            raise ValueError('Resized image aspect differs; unknown crop or padding.')
    result = np.stack([rescale_intrinsics(k, hw, size) for k, size in zip(all_k, image_hw)])
    return result, {
        'source': 'DA3 predicted K; not measured calibration',
        'predictionSha256': sha(npz), 'inferenceReceiptSha256': sha(receipt_path),
        'processedHeightWidth': list(hw), 'originalHeightWidth': image_hw,
        'resize': 'K_original = diag(W_original/W_processed,H_original/H_processed,1) @ K',
        'originalPixelIntrinsics': result.tolist(), 'distortion': 'assumed zero; not calibrated',
        'DA3ExtrinsicsUsedForInitialization': False,
    }


def stats(values: np.ndarray) -> dict | None:
    values = np.asarray(values)
    values = values[np.isfinite(values)]
    if not len(values):
        return None
    return {'median': float(np.median(values)), 'p90': float(np.percentile(values, 90)),
            'max': float(np.max(values))}


def sampson_pixels(f: np.ndarray, a: np.ndarray, b: np.ndarray) -> np.ndarray:
    ah = np.column_stack((a, np.ones(len(a))))
    bh = np.column_stack((b, np.ones(len(b))))
    fa, ftb = ah @ f.T, bh @ f
    denominator = np.sum(fa[:, :2] ** 2, axis=1) + np.sum(ftb[:, :2] ** 2, axis=1)
    residual = np.abs(np.sum(bh * fa, axis=1))
    return np.divide(residual, np.sqrt(denominator),
                     out=np.full(len(a), np.nan), where=denominator > 1e-20)


def triangulation_diagnostics(a: np.ndarray, b: np.ndarray, k1: np.ndarray,
                              k2: np.ndarray, cam2_from_cam1: np.ndarray) -> dict:
    e2 = np.asarray(cam2_from_cam1, dtype=np.float64)
    if e2.shape != (3, 4) or not np.isfinite(e2).all():
        raise ValueError('Expected a finite 3x4 camera2-from-camera1 pose.')
    if np.linalg.norm(e2[:, 3]) < 1e-10:
        raise ValueError('Zero baseline cannot establish triangulation.')
    p1, p2 = k1 @ np.eye(4)[:3], k2 @ e2
    points = []
    valid_indices = []
    for i, (u, v) in enumerate(zip(a, b)):
        system = np.stack([u[0] * p1[2] - p1[0], u[1] * p1[2] - p1[1],
                           v[0] * p2[2] - p2[0], v[1] * p2[2] - p2[1]])
        _, _, vh = np.linalg.svd(system)
        homogeneous = vh[-1]
        if abs(homogeneous[3]) > 1e-12:
            point = homogeneous[:3] / homogeneous[3]
            if np.isfinite(point).all():
                points.append(point)
                valid_indices.append(i)
    xyz = np.asarray(points).reshape(-1, 3)
    xyz2 = xyz @ e2[:, :3].T + e2[:, 3]
    positive = (xyz[:, 2] > 0) & (xyz2[:, 2] > 0)
    kept = np.asarray(valid_indices, dtype=int)[positive]
    x, x2 = xyz[positive], xyz2[positive]
    reprojection, angles = np.array([]), np.array([])
    if len(x):
        uv1, uv2 = x @ k1.T, x2 @ k2.T
        error1 = np.linalg.norm(uv1[:, :2] / uv1[:, 2:] - a[kept], axis=1)
        error2 = np.linalg.norm(uv2[:, :2] / uv2[:, 2:] - b[kept], axis=1)
        reprojection = np.concatenate((error1, error2))
        center2 = -e2[:, :3].T @ e2[:, 3]
        ray2 = x - center2
        cosine = np.sum(x * ray2, axis=1) / (
            np.linalg.norm(x, axis=1) * np.linalg.norm(ray2, axis=1))
        # COLMAP reports the acute triangulation angle, in radians internally.
        raw = np.arccos(np.clip(cosine, -1, 1))
        angles = np.degrees(np.minimum(raw, np.pi - raw))
    return {'inputInliers': len(a), 'finiteTriangulated': len(xyz),
            'positiveDepthBoth': int(positive.sum()),
            'reprojectionPixels': stats(reprojection),
            'triangulationAngleDegrees': stats(angles),
            'anglesUnderOneDegree': int(np.sum(angles < 1)),
            'units': 'arbitrary relative baseline',
            'method': 'linear DLT, no bundle adjustment; stats only for positive-depth points'}


def rss_bytes() -> int:
    value = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
    return int(value if sys.platform == 'darwin' else value * 1024)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prediction', type=Path, help='Optional receipt-bound DA3 K hints.')
    parser.add_argument('--output', type=Path,
                        default=ROOT / 'reconstruction/output/busan-concourse-colmap-probe')
    args = parser.parse_args()
    import pycolmap as pc
    from PIL import Image
    if pc.__version__ != '4.2.0':
        raise ValueError('This probe is audited for PyCOLMAP 4.2.0 only.')
    output = args.output.resolve()
    output.relative_to((ROOT / 'reconstruction/output').resolve())
    if output.exists():
        raise ValueError('Refuse to overwrite a previous probe; choose a new output directory.')
    images = [IMAGE_DIR / name for name in NAMES]
    input_rows, image_hw = [], []
    for path in images:
        with Image.open(path) as image:
            if image.getexif().get(274, 1) != 1:
                raise ValueError('Nontrivial EXIF orientation needs explicit calibration mapping.')
            width, height = image.size
        if width * height > 2_000_000:
            raise ValueError('This bounded probe accepts at most 2 megapixels per image.')
        image_hw.append((height, width))
        input_rows.append({'path': str(path.relative_to(ROOT)), 'sha256': sha(path),
                           'width': width, 'height': height})
    calibration, calibration_receipt = None, None
    if args.prediction:
        calibration, calibration_receipt = load_calibration(
            args.prediction.resolve(), images, image_hw)
    output.mkdir(parents=True)
    receipt_path, database_path = output / 'probe-receipt.json', output / 'features.db'
    receipt = {'schemaVersion': 1, 'status': 'running',
               'startedAt': datetime.now(timezone.utc).isoformat(),
               'inputs': input_rows, 'inputOrder': 'B then A', 'device': 'CPU',
               'python': platform.python_version(), 'os': platform.platform(),
               'dependencies': {name: importlib.metadata.version(name)
                                for name in ['pycolmap', 'numpy', 'Pillow']},
               'runnerSha256': sha(Path(__file__)), 'requirementsLockSha256': sha(LOCK),
               'calibrationHints': calibration_receipt, 'networkImageUploads': 0,
               'mappingAttempted': False, 'registeredCameras': 0,
               'denseReconstructionAttempted': False, 'metricScaleValidated': False,
               'facilityFidelityValidated': False, 'masksApplied': False,
               'memoryBudgetBytes': 4 * 1024 ** 3, 'timeBudgetSeconds': 120,
               'notes': ['Pair pose is not a registered sparse reconstruction.',
                         'F/H support alone does not establish scene depth or metric accuracy.',
                         'Repeated architecture and moving passengers can affect matching.',
                         'No DA3 depth or extrinsics are used by the estimators.']}
    write_json(receipt_path, receipt)
    started = time.perf_counter()
    stop = threading.Event()
    def monitor():
        while not stop.wait(.5):
            if rss_bytes() > receipt['memoryBudgetBytes'] or time.perf_counter() - started > 120:
                receipt.update(status='resource_budget_exceeded', peakRssBytes=rss_bytes(),
                               elapsedSeconds=time.perf_counter() - started)
                write_json(receipt_path, receipt)
                os._exit(70)
    threading.Thread(target=monitor, daemon=True, name='colmap-budget').start()
    try:
        extraction = pc.FeatureExtractionOptions()
        extraction.num_threads = 4
        extraction.use_gpu = False
        extraction.max_image_size = 1600
        extraction.sift.max_num_features = 4096
        matching = pc.FeatureMatchingOptions()
        matching.num_threads = 4
        matching.use_gpu = False
        matching.max_num_matches = 4096
        matching.skip_geometric_verification = True
        matching.sift.cpu_brute_force_matcher = True
        matching.sift.max_ratio = .8
        matching.sift.cross_check = True
        ransac = pc.RANSACOptions()
        ransac.max_error = 4.
        ransac.max_num_trials = 10000
        ransac.min_num_trials = 100
        ransac.random_seed = 0
        ransac.num_threads = 1
        receipt['settings'] = {'extraction': extraction.todict(),
                               'matching': matching.todict(), 'ransac': ransac.todict()}
        # Enum objects in option dictionaries are made readable, not serialized as raw IDs.
        receipt['settings'] = json.loads(json.dumps(receipt['settings'], default=str))
        step = time.perf_counter()
        pc.extract_features(database_path, IMAGE_DIR, image_names=NAMES,
                            extraction_options=extraction, device=pc.Device.cpu)
        receipt['extractionSeconds'] = time.perf_counter() - step
        step = time.perf_counter()
        pc.match_exhaustive(database_path, matching_options=matching, device=pc.Device.cpu)
        receipt['matchingSeconds'] = time.perf_counter() - step
        with pc.Database.open(database_path) as db:
            ids = [db.read_image_with_name(name).image_id for name in NAMES]
            keypoints = [db.read_keypoints(i)[:, :2].astype(np.float64) for i in ids]
            matches = db.read_matches(*ids)
        receipt['featuresPerImage'] = [len(k) for k in keypoints]
        receipt['rawMutualRatioMatches'] = len(matches)
        a, b = keypoints[0][matches[:, 0]], keypoints[1][matches[:, 1]]
        step = time.perf_counter()
        fundamental = pc.estimate_fundamental_matrix(a, b, ransac) if len(matches) >= 8 else None
        homography = pc.estimate_homography_matrix(a, b, ransac) if len(matches) >= 4 else None
        arrays = {'keypoints_b': keypoints[0], 'keypoints_a': keypoints[1], 'matches': matches}
        for name, result, matrix_key in [('fundamental', fundamental, 'F'),
                                         ('homography', homography, 'H')]:
            if result is None:
                receipt[name] = {'status': 'no_estimate', 'inliers': 0}
                continue
            mask = np.asarray(result['inlier_mask'], dtype=bool)
            matrix = np.asarray(result[matrix_key])
            arrays[name + '_inlier_mask'] = mask
            arrays[matrix_key] = matrix
            receipt[name] = {'status': 'estimated', 'inliers': int(mask.sum()),
                             'fractionOfMatches': float(mask.mean()), 'matrix': matrix.tolist()}
            if name == 'fundamental':
                errors = sampson_pixels(matrix, a, b)
                receipt[name]['sampsonPixelsAll'] = stats(errors)
                receipt[name]['sampsonPixelsInliers'] = stats(errors[mask])
        if calibration is not None and len(matches) >= 15:
            cameras = [pc.Camera(model='PINHOLE', width=hw[1], height=hw[0],
                                 params=[k[0, 0], k[1, 1], k[0, 2], k[1, 2]])
                       for k, hw in zip(calibration, image_hw)]
            options = pc.TwoViewGeometryOptions()
            options.compute_relative_pose = True
            options.ransac = ransac
            geometry = pc.estimate_calibrated_two_view_geometry(
                cameras[0], keypoints[0], cameras[1], keypoints[1], matches, options)
            config = pc.TwoViewGeometryConfiguration(geometry.config).name
            conditional = {'configuration': config, 'inliers': len(geometry.inlier_matches),
                           'colmapTriangulationAngleDegrees': float(np.degrees(geometry.tri_angle)),
                           'poseRecovered': geometry.cam2_from_cam1 is not None,
                           'calibrationDependency': 'DA3 predicted K; zero lens distortion assumption'}
            if geometry.cam2_from_cam1 is not None:
                pose = geometry.cam2_from_cam1.matrix()
                conditional['camAFromCamB'] = pose.tolist()
                conditional['coordinates'] = 'OpenCV/COLMAP x-right,y-down,z-forward; arbitrary baseline'
                arrays['conditional_cam_a_from_cam_b'] = pose
                arrays['conditional_inlier_matches'] = geometry.inlier_matches
                if np.linalg.norm(pose[:, 3]) > 1e-10:
                    pair = geometry.inlier_matches
                    conditional['triangulation'] = triangulation_diagnostics(
                        keypoints[0][pair[:, 0]], keypoints[1][pair[:, 1]],
                        calibration[0], calibration[1], pose)
                else:
                    conditional['triangulation'] = {'status': 'zero_baseline_not_triangulated'}
            receipt['conditionalCalibratedPair'] = conditional
        receipt['estimationSeconds'] = time.perf_counter() - step
        f_count, h_count = receipt['fundamental']['inliers'], receipt['homography']['inliers']
        receipt['degeneracyDiagnostics'] = {
            'homographyToFundamentalInlierRatio': h_count / f_count if f_count else None,
            'interpretation': 'High H/F support can indicate planar or near-pure-rotation ambiguity; '
                              'it is not a facility-wide depth validation.'}
        npz = output / 'pair-diagnostics.npz'
        np.savez_compressed(npz, **arrays)
        shutil.copyfile(LOCK, output / LOCK.name)
        receipt['outputs'] = [{'file': path.name, 'sha256': sha(path),
                               'bytes': path.stat().st_size} for path in [npz, database_path]]
        receipt['status'] = 'succeeded'
    except Exception as error:
        receipt.update(status='failed', error=f'{type(error).__name__}: {error}')
        raise
    finally:
        stop.set()
        receipt.update(elapsedSeconds=time.perf_counter() - started, peakRssBytes=rss_bytes(),
                       finishedAt=datetime.now(timezone.utc).isoformat())
        write_json(receipt_path, receipt)
    print(json.dumps(receipt, indent=2))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
