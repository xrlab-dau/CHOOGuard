"""SEQ-01–07 regression coverage; synthetic inputs only, no image attachments."""
import json
import sys
from pathlib import Path

import numpy as np
import pytest
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
import run_sequence_pilot as pilot
import run_mapanything as ma


@pytest.fixture
def sequence(tmp_path):
    video = tmp_path / 'source.mp4'
    video.write_bytes(b'synthetic video')
    frames = []
    paths = []
    for i in range(4):
        path = tmp_path / f'{i}.png'
        Image.new('RGB', (8, 6), (i * 40, 0, 0)).save(path)
        paths.append(path)
        frames.append(dict(file=path.name, sha256=pilot.sha(path), sourcePtsSeconds=i * .5,
                           width=8, height=6))
    receipt = dict(sourceVideoPath='source.mp4', sourceVideoSha256=pilot.sha(video),
                   sourceStartPtsSeconds=0, sourceDurationSeconds=2, frames=frames,
                   sequenceVerification=dict(method='ffmpeg-scene-score', threshold=.3,
                       cutCount=0, startPtsSeconds=0, endPtsSeconds=1.5,
                       sourceVideoSha256=pilot.sha(video), visualReview='not_performed'))
    (tmp_path / 'samples.json').write_text(json.dumps(receipt))
    return paths, receipt


@pytest.mark.parametrize('corruption', ['reverse', 'duplicate', 'nan', 'width', 'source', 'cut', 'unscanned'])
def test_provenance_rejects_corruption(sequence, corruption):
    paths, receipt = sequence
    if corruption == 'reverse':
        paths = paths[::-1]
    elif corruption == 'duplicate':
        paths[1] = paths[0]
    elif corruption == 'nan':
        receipt['frames'][1]['sourcePtsSeconds'] = float('nan')
    elif corruption == 'width':
        receipt['frames'][1]['width'] = 99
    elif corruption == 'source':
        receipt['sourceVideoSha256'] = '0' * 64
    elif corruption == 'cut':
        receipt['sequenceVerification']['cutCount'] = 1
    else:
        receipt['sequenceVerification']['endPtsSeconds'] = 1
    (paths[0].parent / 'samples.json').write_text(json.dumps(receipt))
    with pytest.raises(ValueError):
        pilot.validate_input_provenance(paths)


def test_continuous_sequence_is_accepted(sequence):
    paths, _ = sequence
    assert len(pilot.validate_input_provenance(paths)) == 4


@pytest.mark.parametrize('corruption', ['exit', 'input', 'output', 'partial', 'escape'])
def test_ranking_requires_process_input_and_artifact_binding(tmp_path, corruption):
    artifact = tmp_path / 'prediction.npz'
    artifact.write_bytes(b'synthetic output')
    receipt = dict(status='succeeded', inputs=[{'sha256': 'a'}],
                   output=dict(file=artifact.name, sha256=pilot.sha(artifact)))
    stage = dict(status='exited', returncode=0)
    if corruption == 'exit':
        stage['returncode'] = 1
    elif corruption == 'input':
        receipt['inputs'][0]['sha256'] = 'b'
    elif corruption == 'output':
        artifact.write_bytes(b'changed')
    elif corruption == 'partial':
        receipt['status'] = 'succeeded_partial_registration'
    else:
        receipt['output']['file'] = '../prediction.npz'
    assert pilot.ranking_exclusion(receipt, stage, ['a'], tmp_path)


def test_successful_artifacts_can_be_included(tmp_path):
    artifact = tmp_path / 'prediction.npz'
    artifact.write_bytes(b'synthetic')
    receipt = dict(status='succeeded', inputs=[{'sha256': 'a'}],
                   output=dict(file=artifact.name, sha256=pilot.sha(artifact)))
    assert pilot.ranking_exclusion(receipt, dict(status='exited', returncode=0), ['a'], tmp_path) is None


def test_mapanything_adapter_preserves_optical_z_and_inverts_pose():
    raw = dict(depth_z=np.ones((1, 2, 3, 1)), conf=np.ones((1, 2, 3)),
               camera_poses=np.eye(4)[None], intrinsics=np.eye(3)[None],
               img_no_norm=np.zeros((1, 2, 3, 3)), mask=np.ones((1, 2, 3, 1), bool))
    raw['camera_poses'][0, 0, 3] = 5
    out = ma.adapt_predictions([raw])
    assert out['depth'].shape == (1, 2, 3)
    assert out['extrinsics'][0, 0, 3] == -5
    assert out['processed_images'].dtype == np.uint8
    raw['non_ambiguous_mask'] = raw.pop('mask')[..., 0]
    raw['non_ambiguous_mask'][0, 0, 0] = False
    assert not ma.adapt_predictions([raw])['mask'][0, 0, 0]
    raw['intrinsics'][0, 0, 0] = -1
    with pytest.raises(ValueError):
        ma.adapt_predictions([raw])


def test_hub_load_forces_fingerprinted_local_source_without_weights(tmp_path, monkeypatch):
    (tmp_path / 'hubconf.py').write_text('# synthetic source')
    monkeypatch.setattr(ma, 'DINOV2_HUB_CACHE', tmp_path)
    calls = []
    hub = type('Hub', (), {})()
    hub.load = lambda *a, **k: calls.append((a, k))
    ma.block_torch_hub_network(hub, allow_local_cache=True,
                              expected_fingerprint=ma.fingerprint(tmp_path))
    hub.load('facebookresearch/dinov2', 'dinov2_vitg14', pretrained=True, force_reload=True)
    assert calls == [((str(tmp_path), 'dinov2_vitg14'), {'source': 'local', 'pretrained': False})]
    (tmp_path / 'hubconf.py').write_text('# changed')
    with pytest.raises(RuntimeError, match='blocked'):
        hub.load('facebookresearch/dinov2', 'dinov2_vitg14')
    assert len(calls) == 1


def test_sfm_observation_diagnostics_reports_actual_residuals_and_parallax():
    import run_sfm_sequence as sfm
    class Image:
        camera_id = 0
        name = 'synthetic'
        def cam_from_world(self):
            return 1
        def projection_center(self):
            return np.zeros(3)
        def point2D(self, index):
            return type('Point2D', (), {'xy': np.array([.5, 0])})()
    camera = type('Camera', (), {'img_from_cam': lambda self, xyz: np.array([0., 0.])})()
    element = type('Element', (), {'image_id': 0, 'point2D_idx': 0})()
    point = type('Point', (), {'xyz': np.array([0., 0., 1.]),
                             'track': type('Track', (), {'elements': [element, element]})()})()
    reconstruction = type('Reconstruction', (), {'points3D': {0: point},
        'image': lambda self, i: Image(), 'camera': lambda self, i: camera})()
    diagnostics = sfm.observation_diagnostics(reconstruction)
    assert diagnostics['observationReprojectionPixels']['median'] == .5
    assert diagnostics['observationReprojectionPixels']['p90'] == .5
    assert diagnostics['parallaxDegrees']['median'] == 0


def test_stage_records_real_time_budget_stop(tmp_path):
    script = tmp_path / 'sleep.py'
    script.write_text('import time; time.sleep(.2)')
    stage = pilot.run_stage(Path(sys.executable), script, [], timeout=.1)
    assert stage['status'] == 'timed_out'


def test_bounded_command_records_real_rss_stop():
    result = pilot.run_bounded_command([sys.executable, '-c', 'import time; time.sleep(5)'],
                                       timeout=5, max_rss_bytes=1)
    assert result['status'] == 'resource_budget_exceeded'
    assert result['peakSampledProcessTreeRssBytes'] > 1
    assert result['returncode'] != 0


def test_stage_rejects_low_disk_before_launch(tmp_path, monkeypatch):
    monkeypatch.setattr(pilot.shutil, 'disk_usage', lambda _: type('Disk', (), {'free': 0})())
    stage = pilot.run_stage(Path(sys.executable), tmp_path / 'absent.py', [], timeout=5)
    assert stage['status'] == 'resource_budget_exceeded'
