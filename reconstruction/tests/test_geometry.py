import numpy as np
import pytest

from chooguard_reconstruction.geometry import (
    backproject, depth_surface, homogeneous_pose, review_from_world, transform_points,
    polygon_keep_mask,
)


def test_depth_is_optical_z_and_extrinsics_are_world_to_camera():
    depth = np.full((2, 2), 2.0)
    k = np.array([[2, 0, 0], [0, 2, 0], [0, 0, 1.]])
    e = np.eye(4)
    e[:3, 3] = [3, 4, 5]
    points = backproject(depth, k, e)
    np.testing.assert_allclose(points[1, 1], [-2, -3, -3])
    camera = transform_points(points, e)
    np.testing.assert_allclose(camera[:, :, 2], depth)


def test_rotated_camera_roundtrip():
    e = np.array([[0, 0, 1, 3], [0, 1, 0, 0], [-1, 0, 0, 2], [0, 0, 0, 1.]])
    p = backproject(np.ones((3, 3)), np.eye(3), e)
    recovered = transform_points(p, e)
    np.testing.assert_allclose(recovered[2, 1], [1, 2, 1])


def test_review_transform_is_camera_relative_right_handed_not_metric():
    e = np.eye(4)
    e[0, 3] = -7
    t = review_from_world(e)
    np.testing.assert_allclose(transform_points(np.array([[8., 2, 3]]), t), [[1, -2, -3]])
    assert np.linalg.det(t[:3, :3]) == pytest.approx(1)


def test_three_by_four_pose_is_expanded():
    np.testing.assert_allclose(homogeneous_pose(np.eye(4)[:3]), np.eye(4))


@pytest.mark.parametrize('k', [np.zeros((3, 3)), np.diag([-1., 1, 1]), np.eye(4)])
def test_invalid_intrinsics_rejected(k):
    with pytest.raises(ValueError):
        backproject(np.ones((2, 2)), k, np.eye(4))


@pytest.mark.parametrize('e', [np.zeros((4, 4)), np.diag([1., 1, -1, 1]), np.eye(3)])
def test_non_rigid_pose_rejected(e):
    with pytest.raises(ValueError):
        homogeneous_pose(e)


def make_surface(depth, **kwargs):
    return depth_surface(depth, np.ones_like(depth), np.zeros((*depth.shape, 3), np.uint8),
                         np.eye(3), np.eye(4), percentile=0, **kwargs)


def test_plane_triangulates_and_normals_face_observing_camera():
    s = make_surface(np.ones((3, 3)))
    assert s.faces.shape == (8, 3)
    a, b, c = s.vertices[s.faces].transpose(1, 0, 2)
    assert (np.cross(b-a, c-a)[:, 2] < 0).all()


def test_discontinuity_does_not_bridge_foreground_background():
    d = np.ones((3, 4))
    d[:, 2:] = 10
    s = make_surface(d, max_relative_jump=0.1)
    face_z = s.vertices[s.faces, 2]
    assert (np.ptp(face_z, axis=1) == 0).all()
    assert len(s.faces) == 8


def test_excluded_pixel_never_becomes_surface_or_point():
    mask = np.ones((3, 3), bool)
    mask[1, 1] = False
    s = make_surface(np.ones((3, 3)), keep_mask=mask)
    assert not (s.pixel_indices == 4).any()
    assert len(s.vertices) == 8


def test_nonfinite_depth_and_confidence_removed():
    d = np.ones((3, 3))
    d[0, 0] = np.nan
    d[1, 1] = -1
    s = make_surface(d)
    assert np.isfinite(s.vertices).all()
    assert len(s.vertices) == 7


def test_empty_confidence_surface_is_explicit():
    d = np.ones((2, 2))
    s = depth_surface(d, np.full_like(d, np.nan), np.zeros((2, 2, 3), np.uint8),
                      np.eye(3), np.eye(4))
    assert s.vertices.shape == (0, 3)
    assert s.faces.shape == (0, 3)


def test_bad_mask_shape_rejected():
    with pytest.raises(ValueError):
        make_surface(np.ones((3, 3)), keep_mask=np.ones((2, 2), bool))


def test_mask_removes_lower_half_at_any_aspect_preserving_resolution():
    polygon = [[0, .5], [1, .5], [1, 1], [0, 1]]
    mask = polygon_keep_mask(6, 8, [polygon])
    assert mask[:3].all()
    assert not mask[3:].any()


def test_model_review_blender_unity_transform_keeps_known_forward_and_right():
    # Blender GLB import (x,-z,y), then FBX staging Z180, then verified Unity importer.
    gltf_to_blender = np.array([[1, 0, 0], [0, 0, -1], [0, 1, 0]])
    staging = np.diag([-1, -1, 1])
    unity_from_blender = np.array([[-1, 0, 0], [0, 0, 1], [0, -1, 0]])
    np.testing.assert_allclose(unity_from_blender @ staging @ gltf_to_blender,
                               np.diag([1, 1, -1]))
