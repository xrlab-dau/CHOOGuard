import json

import numpy as np
import pytest
import trimesh

from chooguard_reconstruction.export import (
    export_prediction, export_sparse_reconstruction, sha256, srgb_to_linear_colors,
)


def fixture(tmp_path):
    prediction = tmp_path / 'prediction.npz'
    np.savez_compressed(prediction, depth=np.ones((2, 4, 4)), conf=np.ones((2, 4, 4)),
                        processed_images=np.full((2, 4, 4, 3), 170, np.uint8),
                        intrinsics=np.repeat(np.eye(3)[None], 2, 0),
                        extrinsics=np.repeat(np.eye(4)[None], 2, 0))
    inputs = [{'id': f'fixture-{i}', 'sha256': str(i)*64, 'dimensions_px': [4, 4]}
              for i in range(2)]
    receipt_inputs = [{'sha256': x['sha256'], 'width': x['dimensions_px'][0],
                      'height': x['dimensions_px'][1]} for x in inputs]
    audit = tmp_path / 'audit.json'
    audit.write_text(json.dumps({'fixed_image_set': inputs,
                                 'recommended_order': [x['id'] for x in inputs],
                                 'mask_proposals': {x['id']: [] for x in inputs},
                                 'coverage_description': 'fixture-only synthetic coverage'}))
    receipt = tmp_path / 'inference-receipt.json'
    receipt.write_text(json.dumps({'status': 'succeeded', 'inputs': receipt_inputs,
                                   'output': {'sha256': sha256(prediction)},
                                   'settings': {'process_res_method': 'upper_bound_resize'}}))
    return prediction, audit, receipt


def test_triangle_glb_and_point_ply_roundtrip_without_metric_promotion(tmp_path):
    prediction, audit, _ = fixture(tmp_path)
    output = tmp_path / 'output'
    result = export_prediction(prediction, audit, output)
    scene = trimesh.load(output / 'review-surfaces.glb', force='scene', process=False)
    assert len(scene.geometry) == 2
    assert sum(len(g.faces) for g in scene.geometry.values()) == 36
    cloud = trimesh.load(output / 'review-points.ply', process=False)
    assert len(cloud.vertices) == 32
    np.testing.assert_array_equal(cloud.colors[0, :3], [170, 170, 170])
    expected = srgb_to_linear_colors(np.array([170, 170, 170], dtype=np.uint8))
    for geometry in scene.geometry.values():
        np.testing.assert_array_equal(geometry.visual.vertex_colors[0, :3], expected)
    assert result['coordinates']['metersPerUnit'] is None
    assert result['quality']['collisionApproved'] is False
    assert result['quality']['crossViewFusionApproved'] is False
    assert result['crossViewDiagnostics'][0]['withinToleranceFraction'] == 1


def test_middle_gray_is_linearized_for_gltf_only():
    pixels = np.array([[0, 128, 255]], dtype=np.uint8)
    np.testing.assert_array_equal(srgb_to_linear_colors(pixels), [[0, 55, 255]])
    np.testing.assert_array_equal(pixels, [[0, 128, 255]])


@pytest.mark.parametrize('change', ['hash', 'order', 'status', 'resize'])
def test_inference_binding_mismatch_rejected(tmp_path, change):
    prediction, audit, receipt = fixture(tmp_path)
    data = json.loads(receipt.read_text())
    if change == 'hash':
        data['output']['sha256'] = '0'*64
    elif change == 'order':
        data['inputs'].reverse()
    elif change == 'status':
        data['status'] = 'failed'
    else:
        data['settings']['process_res_method'] = 'resize_crop'
    receipt.write_text(json.dumps(data))
    with pytest.raises(ValueError):
        export_prediction(prediction, audit, tmp_path / 'output')


def test_audit_dimensions_must_match_receipt_not_processed_aspect(tmp_path):
    prediction, audit, receipt = fixture(tmp_path)
    # A resize-method processed grid can legitimately have a different aspect ratio than the
    # original image once PATCH_SIZE=14 rounding is applied per axis; that alone must not fail.
    data = json.loads(audit.read_text())
    data['fixed_image_set'][0]['dimensions_px'] = [5, 4]
    audit.write_text(json.dumps(data))
    with pytest.raises(ValueError, match='dimensions_px'):
        export_prediction(prediction, audit, tmp_path / 'output')


def sparse_fixture(tmp_path, points):
    npz = tmp_path / 'sparse-points.npz'
    n = len(points)
    np.savez_compressed(npz, xyz=np.asarray(points, dtype=np.float64).reshape(n, 3),
                        rgb=np.full((n, 3), 200, np.uint8),
                        track_length=np.full(n, 3, np.int32), error=np.full(n, 0.5))
    return npz


def test_sparse_reconstruction_is_a_separate_never_fused_artifact(tmp_path):
    npz = sparse_fixture(tmp_path, [[0., 0, 1], [1., 0, 2], [0., 1, 3]])
    output = tmp_path / 'sparse-output'
    result = export_sparse_reconstruction(npz, output)
    assert result['schemaVersion'] == 'reconstruction-review-sparse-1'
    assert result['pointCount'] == 3
    assert result['quality']['fusedWithDenseSurface'] is False
    assert result['quality']['collisionApproved'] is False
    assert result['coordinates']['metersPerUnit'] is None
    assert not (output / 'review-surfaces.glb').exists()
    cloud = trimesh.load(output / 'sparse-points.ply', process=False)
    assert len(cloud.vertices) == 3


def test_sparse_reconstruction_with_zero_points_is_explicit_not_hidden(tmp_path):
    npz = sparse_fixture(tmp_path, [])
    result = export_sparse_reconstruction(npz, tmp_path / 'empty-sparse-output')
    assert result['status'] == 'no_sparse_points_available'
    assert result['pointCount'] == 0
    assert result['trackLengthDistribution'] is None


def test_sparse_reconstruction_rejects_mismatched_arrays(tmp_path):
    npz = tmp_path / 'bad.npz'
    np.savez_compressed(npz, xyz=np.zeros((2, 3)), rgb=np.zeros((2, 3), np.uint8),
                        track_length=np.zeros(1, np.int32), error=np.zeros(2))
    with pytest.raises(ValueError):
        export_sparse_reconstruction(npz, tmp_path / 'output')
