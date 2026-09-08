#!/usr/bin/env python3
"""Independent CPU incremental SfM (registration + bundle adjustment) on 2-4 frames.

This is not the pair-only two-view probe in probe_colmap.py. It runs COLMAP's own
feature extraction, exhaustive matching with geometric verification and incremental
mapping/BA to obtain a native sparse reconstruction and camera model. No DA3 depth,
pose or intrinsics are read or injected here; this group must stay independent of the
DA3 comparison arm so agreement/disagreement between engines stays informative.

Cuts, degenerate pairs and unregistered frames are reported, not hidden. A partially
registered result is not rewritten as a full success.
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
LOCK = ROOT / 'reconstruction/requirements-colmap.lock'


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, data: dict) -> None:
    tmp = path.with_suffix('.json.tmp')
    tmp.write_text(json.dumps(data, indent=2, ensure_ascii=False, allow_nan=False) + '\n')
    os.replace(tmp, path)


def rss_bytes() -> int:
    value = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
    return int(value if sys.platform == 'darwin' else value * 1024)


def stats(values) -> dict | None:
    values = np.asarray(list(values), dtype=np.float64)
    values = values[np.isfinite(values)]
    if not len(values):
        return None
    return {'count': int(len(values)), 'mean': float(np.mean(values)),
            'median': float(np.median(values)), 'p90': float(np.percentile(values, 90)),
            'max': float(np.max(values))}


def reconstruction_summary(reconstruction, image_names: list[str]) -> dict:
    id_by_name = {reconstruction.image(image_id).name: image_id
                  for image_id in reconstruction.reg_image_ids()}
    registered = [name for name in image_names if name in id_by_name]
    unregistered = [name for name in image_names if name not in id_by_name]
    track_lengths = [point.track.length() for point in reconstruction.points3D.values()]
    errors = [point.error for point in reconstruction.points3D.values() if point.has_error]
    per_image = []
    for name in image_names:
        image_id = id_by_name.get(name)
        if image_id is None:
            per_image.append({'name': name, 'registered': False})
            continue
        image = reconstruction.image(image_id)
        camera = reconstruction.camera(image.camera_id)
        per_image.append({'name': name, 'registered': True,
                          'points3D': image.num_points3D(), 'points2D': image.num_points2D(),
                          'cameraModel': camera.model.name,
                          'cameraParams': camera.params.tolist()})
    return {
        'registeredImages': registered, 'unregisteredImages': unregistered,
        'numRegisteredImages': reconstruction.num_reg_images(),
        'numImages': reconstruction.num_images(), 'numPoints3D': reconstruction.num_points3D(),
        'meanTrackLength': reconstruction.compute_mean_track_length(),
        'meanReprojectionError': reconstruction.compute_mean_reprojection_error(),
        'trackLengthDistribution': stats(track_lengths),
        'reprojectionErrorPixelsDistribution': stats(errors),
        'perImage': per_image,
    }


def observation_diagnostics(reconstruction):
    """Actual per-observation residuals and triangulation angles, not point-mean errors."""
    residuals, angles, per_image = [], [], {}
    for point in reconstruction.points3D.values():
        rays = []
        for element in point.track.elements:
            image = reconstruction.image(element.image_id)
            camera = reconstruction.camera(image.camera_id)
            projected = camera.img_from_cam(image.cam_from_world() * point.xyz)
            if projected is not None:
                error = float(np.linalg.norm(projected - image.point2D(element.point2D_idx).xy))
                residuals.append(error)
                per_image.setdefault(image.name, []).append(error)
            ray = point.xyz - image.projection_center()
            norm = np.linalg.norm(ray)
            if norm > 0:
                rays.append(ray / norm)
        for index, ray in enumerate(rays):
            for other in rays[index + 1:]:
                angles.append(float(np.degrees(np.arccos(np.clip(np.dot(ray, other), -1, 1)))))
    return dict(observationReprojectionPixels=stats(residuals),
                perImageReprojectionPixels={name: stats(values) for name, values in per_image.items()},
                parallaxDegrees=stats(angles),
                note='Track ray angles and pixel residuals are internal diagnostics, not measured accuracy.')


def validate_arguments(image_args: list[str], output_arg: Path, max_seconds: float,
                       max_rss_gib: float) -> tuple[list[Path], Path, list[str], Path]:
    """Argument-shape checks only; no pycolmap/PIL import so this is unit-testable alone."""
    if not 2 <= len(image_args) <= 4:
        raise ValueError('Provide 2 to 4 ordered images; longer SfM runs are a separate experiment.')
    if not 30 <= max_seconds <= 900:
        raise ValueError('Bounded runtime budget must stay within [30, 900] seconds.')
    if not 0.5 <= max_rss_gib <= 8:
        raise ValueError('Bounded RSS budget must stay within [0.5, 8] GiB.')
    images = [Path(p).resolve() for p in image_args]
    if len({p.parent for p in images}) != 1:
        raise ValueError('All input frames must share one source directory for this runner.')
    image_dir = images[0].parent
    image_names = [p.name for p in images]
    if len(set(image_names)) != len(image_names):
        raise ValueError('Duplicate frame names are not a valid sequence.')
    output = output_arg.resolve()
    output.relative_to((ROOT / 'reconstruction/output').resolve())
    if output.exists():
        raise ValueError('Refuse to overwrite a previous SfM run; choose a new output directory.')
    return images, image_dir, image_names, output


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--image', action='append', required=True,
                        help='Ordered local input path; repeat 2-4 times.')
    parser.add_argument('--output', type=Path, required=True,
                        help='New output directory under reconstruction/output/.')
    parser.add_argument('--max-seconds', type=float, default=240.0)
    parser.add_argument('--max-rss-gib', type=float, default=4.0)
    args = parser.parse_args()
    images, image_dir, image_names, output = validate_arguments(
        args.image, args.output, args.max_seconds, args.max_rss_gib)
    import pycolmap as pc
    from PIL import Image
    if pc.__version__ != '4.2.0':
        raise ValueError('This runner is audited for PyCOLMAP 4.2.0 only.')

    input_rows = []
    for path in images:
        if not path.is_file():
            raise ValueError(f'Missing input frame: {path}')
        with Image.open(path) as image:
            if image.getexif().get(274, 1) != 1:
                raise ValueError('Nontrivial EXIF orientation needs explicit calibration mapping.')
            width, height = image.size
        input_rows.append({'path': str(path.relative_to(ROOT)) if ROOT in path.parents
                           else str(path), 'sha256': sha(path), 'width': width, 'height': height})

    # Standalone execution also enforces the same source/PTS/cut binding as the pilot.
    from run_sequence_pilot import validate_input_provenance
    provenance = validate_input_provenance(images)
    output.mkdir(parents=True)
    sparse_dir = output / 'sparse'
    database_path = output / 'database.db'
    receipt_path = output / 'sfm-receipt.json'
    receipt = {'schemaVersion': 1, 'status': 'running', 'engine': 'pycolmap-incremental-mapping',
               'independentGroup': True, 'da3CalibrationUsed': False,
               'startedAt': datetime.now(timezone.utc).isoformat(),
               'inputs': input_rows, 'inputOrder': image_names, 'device': 'CPU',
               'provenance': provenance, 'minFreeDiskBytes': 8 * 1024**3,
               'initialFreeDiskBytes': shutil.disk_usage(output).free,
               'python': platform.python_version(), 'os': platform.platform(),
               'dependencies': {name: importlib.metadata.version(name)
                                for name in ['pycolmap', 'numpy', 'Pillow']},
               'runnerSha256': sha(Path(__file__)), 'requirementsLockSha256': sha(LOCK),
               'timeBudgetSeconds': args.max_seconds, 'memoryBudgetBytes': int(args.max_rss_gib * 1024**3),
               'networkImageUploads': 0, 'metricScaleValidated': False,
               'facilityFidelityValidated': False,
               'notes': ['Native camera model and sparse points only; not a fused or watertight surface.',
                         'Cuts or degenerate frames may fail to register; that is reported, not hidden.',
                         'No DA3 depth/pose/intrinsics are read by this independent SfM run.']}
    write_json(receipt_path, receipt)
    started = time.perf_counter()
    stop = threading.Event()

    def monitor() -> None:
        while not stop.wait(.5):
            if (rss_bytes() > receipt['memoryBudgetBytes'] or time.perf_counter() - started > args.max_seconds
                    or shutil.disk_usage(output).free < receipt['minFreeDiskBytes']):
                receipt.update(status='resource_budget_exceeded', peakRssBytes=rss_bytes(),
                               elapsedSeconds=time.perf_counter() - started)
                write_json(receipt_path, receipt)
                os._exit(70)
    threading.Thread(target=monitor, daemon=True, name='sfm-budget').start()
    try:
        if shutil.disk_usage(output).free < receipt['minFreeDiskBytes']:
            receipt.update(status='resource_budget_exceeded', reason='Free disk below 8 GiB before extraction')
            return 70
        extraction = pc.FeatureExtractionOptions()
        extraction.num_threads = 4
        extraction.use_gpu = False
        extraction.max_image_size = 1920
        extraction.sift.max_num_features = 8192
        reader = pc.ImageReaderOptions()
        matching = pc.FeatureMatchingOptions()
        matching.num_threads = 4
        matching.use_gpu = False
        matching.max_num_matches = 8192
        matching.sift.max_ratio = .8
        matching.sift.cross_check = True
        mapper = pc.IncrementalPipelineOptions()
        mapper.mapper.num_threads = 4
        mapper.mapper.random_seed = 0
        mapper.triangulation.random_seed = 0
        mapper.mapper.ba_local_num_images = min(6, len(images))
        mapper.min_model_size = 2
        mapper.max_runtime_seconds = int(max(1.0, args.max_seconds - (time.perf_counter() - started) - 5))
        receipt['settings'] = json.loads(json.dumps(
            {'extraction': extraction.todict(), 'matching': matching.todict(),
             'reader': reader.todict(), 'mapper': mapper.todict()}, default=str))
        step = time.perf_counter()
        pc.extract_features(database_path, image_dir, image_names=image_names,
                            camera_mode=pc.CameraMode.SINGLE, reader_options=reader,
                            extraction_options=extraction, device=pc.Device.cpu)
        receipt['extractionSeconds'] = time.perf_counter() - step
        step = time.perf_counter()
        pc.match_exhaustive(database_path, matching_options=matching, device=pc.Device.cpu)
        receipt['matchingSeconds'] = time.perf_counter() - step
        with pc.Database.open(database_path) as db:
            receipt['featuresPerImage'] = {name: len(db.read_keypoints(
                db.read_image_with_name(name).image_id)) for name in image_names}
        step = time.perf_counter()
        sparse_dir.mkdir(parents=True)
        reconstructions = pc.incremental_mapping(database_path, image_dir, sparse_dir, options=mapper)
        receipt['mappingSeconds'] = time.perf_counter() - step
        receipt['mappingAttempted'] = True
        sparse_xyz, sparse_rgb, sparse_track, sparse_error = [], [], [], []
        if not reconstructions:
            receipt.update(status='no_reconstruction_registered',
                           registeredImages=[], unregisteredImages=image_names,
                           reason='Incremental mapping produced zero models; frames may lack a valid '
                                  'initial pair (cut, degenerate motion, or insufficient overlap).')
        else:
            models = {}
            best_key = max(reconstructions, key=lambda k: reconstructions[k].num_reg_images())
            for key, reconstruction in reconstructions.items():
                model_dir = sparse_dir / str(key)
                model_dir.mkdir(parents=True, exist_ok=True)
                reconstruction.write(model_dir)
                summary = reconstruction_summary(reconstruction, image_names)
                summary.update(observation_diagnostics(reconstruction))
                models[str(key)] = summary
                if key == best_key:
                    for point in reconstruction.points3D.values():
                        sparse_xyz.append(point.xyz)
                        sparse_rgb.append(point.color)
                        sparse_track.append(point.track.length())
                        sparse_error.append(point.error if point.has_error else np.nan)
            best = models[str(best_key)]
            complete = best['numRegisteredImages'] == len(images) and len(models) == 1
            receipt.update(
                models=models, bestModel=str(best_key), fullyRegistered=complete,
                registeredImages=best['registeredImages'], unregisteredImages=best['unregisteredImages'],
                status=('succeeded_fully_registered' if complete else
                        'incomplete_split_models' if len(models) > 1 else 'succeeded_partial_registration'))
            if not complete:
                receipt['reason'] = ('Only %d/%d frames registered in the largest model; unregistered '
                                     'frames are excluded, not force-merged.' % (
                                         best['numRegisteredImages'], len(images)))
        # Sparse points are written as a portable npz here (this venv has no trimesh); the
        # separate, never-fused-with-dense PLY/manifest delivery lives in export.py.
        sparse_npz = output / 'sparse-points.npz'
        np.savez_compressed(
            sparse_npz,
            xyz=np.asarray(sparse_xyz, dtype=np.float64).reshape(-1, 3),
            rgb=np.asarray(sparse_rgb, dtype=np.uint8).reshape(-1, 3),
            track_length=np.asarray(sparse_track, dtype=np.int32),
            error=np.asarray(sparse_error, dtype=np.float64))
        outputs = [{'file': 'database.db', 'sha256': sha(database_path),
                   'bytes': database_path.stat().st_size},
                  {'file': 'sparse-points.npz', 'sha256': sha(sparse_npz),
                   'bytes': sparse_npz.stat().st_size, 'points': len(sparse_xyz)}]
        for path in sorted(sparse_dir.rglob('*')):
            if path.is_file():
                outputs.append({'file': str(path.relative_to(output)), 'sha256': sha(path),
                                'bytes': path.stat().st_size})
        receipt['outputs'] = outputs
        shutil.copyfile(LOCK, output / LOCK.name)
    except Exception as error:
        receipt.update(status='failed', error=f'{type(error).__name__}: {error}')
        raise
    finally:
        stop.set()
        receipt.update(elapsedSeconds=time.perf_counter() - started, peakRssBytes=rss_bytes(),
                       finishedAt=datetime.now(timezone.utc).isoformat())
        write_json(receipt_path, receipt)
    print(json.dumps({'status': receipt['status'], 'elapsedSeconds': receipt['elapsedSeconds'],
                      'registeredImages': receipt.get('registeredImages'),
                      'unregisteredImages': receipt.get('unregisteredImages')}, indent=2))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
