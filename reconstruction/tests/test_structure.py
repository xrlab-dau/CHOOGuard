import numpy as np
import pytest

from chooguard_reconstruction.structure import (
    project_point, resize_pixel_center, triangulate_landmark,
    triangulate_axis_line, point_on_axis_for_pixel,
)


def cameras():
    k = np.array([[300., 0, 200], [0, 300, 150], [0, 0, 1]])
    e0, e1 = np.eye(4), np.eye(4)
    e1[0, 3] = -1
    return k, np.stack([e0, e1])


def test_pixel_center_resize_roundtrip():
    point = np.array([247.2, 333.7])
    resized = resize_pixel_center(point, [1120, 840], [504, 378])
    np.testing.assert_allclose(resize_pixel_center(resized, [504, 378], [1120, 840]), point)


def test_asymmetric_two_view_triangulation_recovers_relative_geometry():
    k, e = cameras()
    target = np.array([.35, -.4, 6.])
    pixels = [project_point(target, k, pose) for pose in e]
    result = triangulate_landmark(pixels, [k, k], e, max_reprojection_pixels=2)
    np.testing.assert_allclose(result['point'], target, atol=1e-8)
    assert result['minimumRayAngleDegrees'] > 5
    assert max(result['reprojectionPixels']) < 1e-8
    assert result['unit'] == 'model_relative'


def test_correspondence_on_wrong_epipolar_line_is_rejected():
    k, e = cameras()
    pixels = [project_point([.3, .2, 4], k, pose) for pose in e]
    pixels[1][1] += 30
    with pytest.raises(ValueError, match='reprojection'):
        triangulate_landmark(pixels, [k, k], e, max_reprojection_pixels=2)


def test_near_zero_baseline_is_not_usable_three_dimensional_evidence():
    k, e = cameras()
    e[1, 0, 3] = -1e-6
    pixels = [project_point([.3, .2, 4], k, pose) for pose in e]
    with pytest.raises(ValueError, match='angle'):
        triangulate_landmark(pixels, [k, k], e, minimum_ray_angle_degrees=1)


def test_behind_camera_solution_is_rejected():
    k, e = cameras()
    pixels = [project_point([.3, .2, -4], k, pose, allow_behind=True) for pose in e]
    with pytest.raises(ValueError, match='behind'):
        triangulate_landmark(pixels, [k, k], e)


def test_single_view_is_not_triangulation():
    k, e = cameras()
    with pytest.raises(ValueError):
        triangulate_landmark([[1, 2]], [k], e[:1])


def test_axis_lines_use_same_member_without_pairing_cut_endpoints():
    k, e = cameras()
    origin = np.array([.4, -.3, 4.])
    direction = np.array([.1, 1., .2])
    # Each view samples a different interval on the same infinite physical axis.
    pairs = [[project_point(origin+t*direction, k, pose) for t in interval]
             for pose, interval in zip(e, [(-.2, .2), (.6, 1.3)])]
    line = triangulate_axis_line(pairs, [k, k], e)
    ray_pixel = project_point(origin+.1*direction, k, e[0])
    point = point_on_axis_for_pixel(line, ray_pixel, k, e[0])
    np.testing.assert_allclose(point['point'], origin+.1*direction, atol=1e-8)
    assert point['reprojectionPixels'] < 1e-8


def test_duplicate_camera_planes_do_not_constrain_a_unique_axis():
    k, e = cameras()
    pair = [project_point([.4, t, 4], k, e[0]) for t in (0, 1)]
    with pytest.raises(ValueError, match='plane angle'):
        triangulate_axis_line([pair, pair], [k, k], [e[0], e[0]])
