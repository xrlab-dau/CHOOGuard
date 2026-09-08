"""Contract tests for the isolated CPU two-view probe (no image or COLMAP needed)."""
import importlib.util
import json
from pathlib import Path

import numpy as np
import pytest

PATH = Path(__file__).resolve().parents[1] / 'tools/probe_colmap.py'
SPEC = importlib.util.spec_from_file_location('probe_colmap', PATH)
probe = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(probe)


def test_resize_intrinsics_preserves_projected_rays_and_separate_axis_factors():
    k = np.array([[400., 0, 250], [0, 410, 190], [0, 0, 1]])
    restored = probe.rescale_intrinsics(k, (378, 504), (840, 1120))
    point = np.array([.4, -.2, 2.])
    old, new = k @ point, restored @ point
    np.testing.assert_allclose(new[:2] / new[2], old[:2] / old[2] * 1120 / 504)
    stretched = probe.rescale_intrinsics(k, (100, 200), (300, 800))
    np.testing.assert_allclose(stretched[0], k[0] * 4)
    np.testing.assert_allclose(stretched[1], k[1] * 3)
    np.testing.assert_allclose(stretched[2], [0, 0, 1])


@pytest.mark.parametrize('bad', [np.diag([-1., 1., 1.]), np.full((3, 3), np.nan)])
def test_invalid_intrinsics_rejected(bad):
    with pytest.raises(ValueError):
        probe.rescale_intrinsics(bad, (10, 10), (20, 20))


def test_calibration_bound_to_npz_and_ordered_inputs(tmp_path):
    images = [tmp_path / name for name in ['b.jpg', 'a.jpg']]
    for i, path in enumerate(images):
        path.write_bytes(bytes([i]))
    npz = tmp_path / 'prediction.npz'
    np.savez(npz, intrinsics=np.repeat(np.eye(3)[None], 2, axis=0),
             processed_images=np.zeros((2, 10, 20, 3), dtype=np.uint8))
    receipt = {'status': 'succeeded', 'output': {'sha256': probe.sha(npz)},
               'inputs': [{'sha256': probe.sha(p), 'width': 40, 'height': 20} for p in images],
               'settings': {'process_res_method': 'upper_bound_resize'}}
    receipt_path = tmp_path / 'inference-receipt.json'
    receipt_path.write_text(json.dumps(receipt))
    k, provenance = probe.load_calibration(npz, images, [(20, 40), (20, 40)])
    assert k.shape == (2, 3, 3)
    assert provenance['source'] == 'DA3 predicted K; not measured calibration'
    with pytest.raises(ValueError, match='order'):
        probe.load_calibration(npz, images[::-1], [(20, 40), (20, 40)])
    with pytest.raises(ValueError, match='dimensions'):
        probe.load_calibration(npz, images, [(10, 20), (20, 40)])
    receipt['settings']['process_res_method'] = 'crop'
    receipt_path.write_text(json.dumps(receipt))
    with pytest.raises(ValueError, match='resize'):
        probe.load_calibration(npz, images, [(20, 40), (20, 40)])
    receipt['settings']['process_res_method'] = 'upper_bound_resize'
    receipt['output']['sha256'] = '0' * 64
    receipt_path.write_text(json.dumps(receipt))
    with pytest.raises(ValueError, match='SHA'):
        probe.load_calibration(npz, images, [(20, 40), (20, 40)])


def test_two_view_triangulation_cheirality_reprojection_and_angle():
    k = np.array([[700., 0, 560], [0, 700, 420], [0, 0, 1]])
    transform = np.column_stack((np.eye(3), [-1., 0, 0]))
    xyz = np.array([[0., 0, 5], [.5, .2, 7], [-.2, .3, 8], [0., 0, -3]])
    def pixels(points):
        projected = points @ k.T
        return projected[:, :2] / projected[:, 2:]
    a, b = pixels(xyz), pixels(xyz + [-1., 0, 0])
    result = probe.triangulation_diagnostics(a, b, k, k, transform)
    assert result['positiveDepthBoth'] == 3
    assert result['finiteTriangulated'] == 4
    assert result['reprojectionPixels']['max'] < 1e-8
    assert 5 < result['triangulationAngleDegrees']['median'] < 15
    assert result['units'] == 'arbitrary relative baseline'
    zero = np.column_stack((np.eye(3), np.zeros(3)))
    with pytest.raises(ValueError, match='baseline'):
        probe.triangulation_diagnostics(a, b, k, k, zero)


def test_sampson_distance_matches_horizontal_epipolar_geometry():
    f = np.array([[0., 0, 0], [0, 0, -1], [0, 1, 0]])
    a = np.array([[1., 5], [4, 3]])
    b = np.array([[2., 5], [6, 5]])
    np.testing.assert_allclose(probe.sampson_pixels(f, a, b), [0, np.sqrt(2)])
