"""I/O matrix and failure-path contract for the sequence SfM/MapAnything runners.

These import the tool modules directly (heavy pycolmap/torch imports live inside their
main(), matching probe_colmap.py's pattern) so this suite runs in the lightweight
geometry-venv without pycolmap or torch installed, matching CI's synthetic-only posture.
"""
import importlib.util
import json
from pathlib import Path
import sys

import numpy as np
import pytest

TOOLS = Path(__file__).resolve().parents[1] / 'tools'


def load(name):
    path = TOOLS / name
    spec = importlib.util.spec_from_file_location(name.removesuffix('.py'), path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


sfm = load('run_sfm_sequence.py')
mapanything = load('run_mapanything.py')
pilot = load('run_sequence_pilot.py')
sys.path.insert(0, str(TOOLS))


# --- run_sfm_sequence.py: argument-shape validation (no pycolmap import needed) ---

def test_argument_count_out_of_bounds_rejected(tmp_path):
    for images in (['a.jpg'], ['a.jpg'] * 5):
        with pytest.raises(ValueError, match='2 to 4'):
            sfm.validate_arguments(images, tmp_path / 'out', 240.0, 4.0)


@pytest.mark.parametrize('seconds', [10.0, 1000.0])
def test_time_budget_out_of_bounds_rejected(tmp_path, seconds):
    with pytest.raises(ValueError, match='runtime budget'):
        sfm.validate_arguments(['a.jpg', 'b.jpg'], tmp_path / 'out', seconds, 4.0)


@pytest.mark.parametrize('rss', [0.1, 20.0])
def test_rss_budget_out_of_bounds_rejected(tmp_path, rss):
    with pytest.raises(ValueError, match='RSS budget'):
        sfm.validate_arguments(['a.jpg', 'b.jpg'], tmp_path / 'out', 240.0, rss)


def test_mixed_source_directories_rejected(tmp_path):
    (tmp_path / 'x').mkdir()
    (tmp_path / 'y').mkdir()
    images = [str(tmp_path / 'x/a.jpg'), str(tmp_path / 'y/b.jpg')]
    with pytest.raises(ValueError, match='one source directory'):
        sfm.validate_arguments(images, tmp_path / 'out', 240.0, 4.0)


def test_duplicate_frame_names_rejected(tmp_path):
    (tmp_path / 'x').mkdir()
    images = [str(tmp_path / 'x/a.jpg')] * 2
    with pytest.raises(ValueError, match='Duplicate frame'):
        sfm.validate_arguments(images, tmp_path / 'out', 240.0, 4.0)


def test_existing_output_directory_is_never_overwritten(tmp_path, monkeypatch):
    monkeypatch.setattr(sfm, 'ROOT', tmp_path)
    output = tmp_path / 'reconstruction/output/existing-run'
    output.mkdir(parents=True)
    (tmp_path / 'frames').mkdir()
    images = [str(tmp_path / 'frames/a.jpg'), str(tmp_path / 'frames/b.jpg')]
    with pytest.raises(ValueError, match='Refuse to overwrite'):
        sfm.validate_arguments(images, output, 240.0, 4.0)


def test_output_directory_must_stay_under_reconstruction_output(tmp_path, monkeypatch):
    monkeypatch.setattr(sfm, 'ROOT', tmp_path)
    (tmp_path / 'frames').mkdir()
    images = [str(tmp_path / 'frames/a.jpg'), str(tmp_path / 'frames/b.jpg')]
    with pytest.raises(ValueError):
        sfm.validate_arguments(images, tmp_path / 'elsewhere/run', 240.0, 4.0)


# --- run_sfm_sequence.py: pure statistics/summary helpers ---

def test_stats_reports_median_p90_max_and_ignores_nonfinite():
    values = [1.0, 2.0, 3.0, 4.0, np.nan, np.inf]
    result = sfm.stats(values)
    assert result['count'] == 4
    assert result['median'] == 2.5
    assert result['max'] == 4.0


def test_stats_of_empty_or_all_nonfinite_is_explicit_none():
    assert sfm.stats([]) is None
    assert sfm.stats([np.nan, np.inf]) is None


class FakeTrack:
    def __init__(self, length):
        self._length = length

    def length(self):
        return self._length


class FakePoint:
    def __init__(self, length, error=None):
        self.track = FakeTrack(length)
        self.has_error = error is not None
        self.error = error if error is not None else 0.0


class FakeCamera:
    def __init__(self):
        self.model = type('Model', (), {'name': 'SIMPLE_RADIAL'})()
        self.params = np.array([500.0, 320.0, 240.0, 0.0])


class FakeImage:
    def __init__(self, name, n2d=10, n3d=6):
        self.name = name
        self.camera_id = 0
        self._n2d, self._n3d = n2d, n3d

    def num_points2D(self):
        return self._n2d

    def num_points3D(self):
        return self._n3d


class FakeReconstruction:
    """Minimal stand-in for pycolmap.Reconstruction, covering only what is read."""

    def __init__(self, registered_names, all_names, points):
        self._images = {i: FakeImage(name) for i, name in enumerate(registered_names)}
        self.points3D = {i: p for i, p in enumerate(points)}
        self._all_names = all_names

    def reg_image_ids(self):
        return list(self._images)

    def image(self, image_id):
        return self._images[image_id]

    def camera(self, camera_id):
        return FakeCamera()

    def num_reg_images(self):
        return len(self._images)

    def num_images(self):
        return len(self._all_names)

    def num_points3D(self):
        return len(self.points3D)

    def compute_mean_track_length(self):
        lengths = [p.track.length() for p in self.points3D.values()]
        return float(np.mean(lengths)) if lengths else 0.0

    def compute_mean_reprojection_error(self):
        errors = [p.error for p in self.points3D.values() if p.has_error]
        return float(np.mean(errors)) if errors else 0.0


def test_reconstruction_summary_separates_registered_from_unregistered():
    points = [FakePoint(3, 0.4), FakePoint(5, 1.2)]
    reconstruction = FakeReconstruction(['b.jpg', 'c.jpg'], ['a.jpg', 'b.jpg', 'c.jpg'], points)
    summary = sfm.reconstruction_summary(reconstruction, ['a.jpg', 'b.jpg', 'c.jpg'])
    assert summary['registeredImages'] == ['b.jpg', 'c.jpg']
    assert summary['unregisteredImages'] == ['a.jpg']
    assert summary['numRegisteredImages'] == 2
    assert summary['trackLengthDistribution']['median'] == 4.0
    assert summary['reprojectionErrorPixelsDistribution']['count'] == 2
    per_image = {row['name']: row for row in summary['perImage']}
    assert per_image['a.jpg']['registered'] is False
    assert per_image['b.jpg']['registered'] is True
    assert per_image['b.jpg']['cameraModel'] == 'SIMPLE_RADIAL'


def test_reconstruction_summary_with_zero_points_is_explicit_not_hidden():
    reconstruction = FakeReconstruction([], ['a.jpg', 'b.jpg'], [])
    summary = sfm.reconstruction_summary(reconstruction, ['a.jpg', 'b.jpg'])
    assert summary['registeredImages'] == []
    assert summary['unregisteredImages'] == ['a.jpg', 'b.jpg']
    assert summary['trackLengthDistribution'] is None
    assert summary['reprojectionErrorPixelsDistribution'] is None


# --- run_mapanything.py: pose adapter (DA3 is world-to-camera; MapAnything is camera-to-world) ---

def test_camera_to_world_adapter_inverts_to_world_to_camera():
    rotation = np.array([[0, -1, 0], [1, 0, 0], [0, 0, 1.]])  # 90 degrees about Z
    translation = np.array([5., -2., 1.])
    c2w = np.eye(4)
    c2w[:3, :3], c2w[:3, 3] = rotation, translation
    w2c = mapanything.world_to_camera_from_camera_to_world(c2w)
    # A world-to-camera pose maps the camera's own world-space position to the origin.
    np.testing.assert_allclose(w2c[:3, :3] @ translation + w2c[:3, 3], [0, 0, 0], atol=1e-10)
    np.testing.assert_allclose(w2c[:3, :3], rotation.T, atol=1e-10)
    # Round trip: inverting the world-to-camera result recovers the original pose.
    back = mapanything.world_to_camera_from_camera_to_world(w2c)
    np.testing.assert_allclose(back, c2w, atol=1e-10)


@pytest.mark.parametrize('bad', [np.zeros((4, 4)), np.diag([1., 1, -1, 1]), np.eye(3)])
def test_camera_to_world_adapter_rejects_non_rigid_input(bad):
    with pytest.raises(ValueError):
        mapanything.world_to_camera_from_camera_to_world(bad)


def test_torch_hub_network_is_blocked_without_an_audited_local_cache(tmp_path, monkeypatch):
    monkeypatch.setattr(mapanything, 'DINOV2_HUB_CACHE', tmp_path / 'no-such-cache')

    class FakeHub:
        def load(self, *a, **k):
            return 'network-fetched-model'
    hub = FakeHub()
    mapanything.block_torch_hub_network(hub, allow_local_cache=True)
    with pytest.raises(RuntimeError, match='blocked'):
        hub.load('facebookresearch/dinov2', 'dinov2_vitg14')


def test_torch_hub_network_rejects_unaudited_existing_cache(tmp_path, monkeypatch):
    cache = tmp_path / 'torch-hub-cache/facebookresearch_dinov2_main'
    cache.mkdir(parents=True)
    monkeypatch.setattr(mapanything, 'DINOV2_HUB_CACHE', cache)

    class FakeHub:
        def load(self, *a, **k):
            return 'served-from-local-cache'
    hub = FakeHub()
    mapanything.block_torch_hub_network(hub, allow_local_cache=True)
    with pytest.raises(RuntimeError, match='blocked'):
        hub.load('facebookresearch/dinov2', 'dinov2_vitg14')


# --- run_mapanything.py: blocked-run receipt shape (no weights, no network, no torch import) ---

def test_mapanything_runner_writes_a_blocked_receipt_when_weights_are_absent(tmp_path, monkeypatch):
    monkeypatch.setattr(mapanything, 'ROOT', tmp_path)
    monkeypatch.setattr(mapanything, 'DEFAULT_WEIGHTS_DIR', tmp_path / 'no-such-weights')
    frames = tmp_path / 'frames'
    frames.mkdir()
    from PIL import Image
    for name in ('a.jpg', 'b.jpg'):
        Image.new('RGB', (8, 6)).save(frames / name)
    output = tmp_path / 'reconstruction/output/mapanything-run'
    argv = ['run_mapanything.py', '--image', str(frames / 'a.jpg'), '--image', str(frames / 'b.jpg'),
           '--output', str(output), '--weights-dir', str(tmp_path / 'no-such-weights')]
    monkeypatch.setattr(mapanything.sys, 'argv', argv)
    assert mapanything.main() == 0
    receipt = json.loads((output / 'mapanything-receipt.json').read_text())
    assert receipt['status'] == 'blocked_weights_not_locally_available'
    assert receipt['expectedWeightSha256'] == mapanything.EXPECTED_WEIGHT_SHA256


def test_mapanything_runner_refuses_to_overwrite_a_previous_run(tmp_path, monkeypatch):
    monkeypatch.setattr(mapanything, 'ROOT', tmp_path)
    output = tmp_path / 'reconstruction/output/existing-run'
    output.mkdir(parents=True)
    frames = tmp_path / 'frames'
    frames.mkdir()
    from PIL import Image
    Image.new('RGB', (8, 6)).save(frames / 'a.jpg')
    argv = ['run_mapanything.py', '--image', str(frames / 'a.jpg'), '--output', str(output)]
    monkeypatch.setattr(mapanything.sys, 'argv', argv)
    with pytest.raises(ValueError, match='Refuse to overwrite'):
        mapanything.main()


# --- run_sequence_pilot.py: input provenance and graceful missing-interpreter handling ---

def write_frame_with_receipt(directory, name, sha_override=None):
    from PIL import Image
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / name
    Image.new('RGB', (8, 6), color=(sum(name.encode()) % 255, 0, 0)).save(path)
    video = directory / 'source.mp4'
    video.write_bytes(b'synthetic source')
    samples = directory / 'samples.json'
    frames = json.loads(samples.read_text())['frames'] if samples.is_file() else []
    frames.append({'file': name, 'sha256': sha_override or pilot.sha(path),
                   'sourcePtsSeconds': len(frames) * 10.0, 'width': 8, 'height': 6})
    samples.write_text(json.dumps({'frames': frames, 'sourceVideoPath': 'source.mp4',
        'sourceVideoSha256': pilot.sha(video), 'sourceStartPtsSeconds': 0,
        'sourceDurationSeconds': 100,
        'sequenceVerification': {'method': 'ffmpeg-scene-score', 'threshold': 0.3,
            'cutCount': 0, 'startPtsSeconds': 0, 'endPtsSeconds': 99,
            'sourceVideoSha256': pilot.sha(video), 'visualReview': 'not_performed'}}))
    return path


def test_input_provenance_requires_a_sibling_extraction_receipt(tmp_path):
    from PIL import Image
    path = tmp_path / 'lone.jpg'
    Image.new('RGB', (8, 6)).save(path)
    with pytest.raises(ValueError, match='samples.json'):
        pilot.validate_input_provenance([path])


def test_input_provenance_rejects_a_frame_not_recorded_in_the_receipt(tmp_path):
    frames_dir = tmp_path / 'frames'
    write_frame_with_receipt(frames_dir, 'a.jpg')
    (frames_dir / 'b.jpg').write_bytes(b'unrelated-file-not-in-samples-json')
    with pytest.raises(ValueError, match='not a recorded frame'):
        pilot.validate_input_provenance([frames_dir / 'b.jpg'])


def test_input_provenance_rejects_a_changed_frame_since_extraction(tmp_path):
    frames_dir = tmp_path / 'frames'
    path = write_frame_with_receipt(frames_dir, 'a.jpg', sha_override='0' * 64)
    with pytest.raises(ValueError, match='differs from its extraction receipt'):
        pilot.validate_input_provenance([path])


def test_input_provenance_accepts_matching_frames_in_order(tmp_path):
    frames_dir = tmp_path / 'frames'
    a = write_frame_with_receipt(frames_dir, 'a.jpg')
    b = write_frame_with_receipt(frames_dir, 'b.jpg')
    rows = pilot.validate_input_provenance([a, b])
    assert [row['sha256'] for row in rows] == [pilot.sha(a), pilot.sha(b)]


def test_run_stage_reports_missing_interpreter_without_raising(tmp_path):
    result = pilot.run_stage(tmp_path / 'no-such-python', Path('unused.py'), [], timeout=5)
    assert result['status'] == 'not_executed'


def test_pilot_refuses_to_overwrite_a_previous_comparison(tmp_path, monkeypatch):
    monkeypatch.setattr(pilot, 'ROOT', tmp_path)
    frames_dir = tmp_path / 'frames'
    a = write_frame_with_receipt(frames_dir, 'a.jpg')
    b = write_frame_with_receipt(frames_dir, 'b.jpg')
    output = tmp_path / 'reconstruction/output/existing-pilot'
    output.mkdir(parents=True)
    argv = ['run_sequence_pilot.py', '--image', str(a), '--image', str(b),
            '--image', str(a), '--image', str(b), '--output', str(output)]
    monkeypatch.setattr(pilot.sys, 'argv', argv)
    with pytest.raises(ValueError, match='Refuse to overwrite'):
        pilot.main()
