"""Photo landmark triangulation in an explicitly uncalibrated camera frame."""

import numpy as np

from .geometry import homogeneous_pose, transform_points


def resize_pixel_center(pixel, source_wh, target_wh):
    sizes = np.asarray([source_wh, target_wh], dtype=float)
    point = np.asarray(pixel, dtype=float)
    if sizes.shape != (2, 2) or point.shape != (2,) or not np.isfinite(sizes).all():
        raise ValueError('Expected pixel(x,y) and positive image(width,height) pairs')
    if (sizes <= 0).any() or not np.isfinite(point).all():
        raise ValueError('Invalid image size or pixel')
    return (point + .5) * sizes[1] / sizes[0] - .5


def checked_intrinsics(value):
    k = np.asarray(value, dtype=float)
    if (k.shape != (3, 3) or not np.isfinite(k).all() or k[0, 0] <= 0 or k[1, 1] <= 0
            or not np.allclose(k[2], [0, 0, 1]) or abs(np.linalg.det(k)) < 1e-10):
        raise ValueError('Invalid pinhole camera intrinsics')
    return k


def project_point(point, intrinsics, extrinsics, *, allow_behind=False):
    camera = transform_points(np.asarray(point, dtype=float), homogeneous_pose(extrinsics))
    if not np.isfinite(camera).all() or abs(camera[2]) < 1e-12:
        raise ValueError('Cannot project this camera-plane or nonfinite point')
    if camera[2] < 0 and not allow_behind:
        raise ValueError('Point lies behind camera')
    homogeneous = checked_intrinsics(intrinsics) @ camera
    return homogeneous[:2] / homogeneous[2]


def triangulate_landmark(pixels, intrinsics, extrinsics, *, max_reprojection_pixels=3,
                         minimum_ray_angle_degrees=1):
    """DLT from at least two independently identified image observations.

    K and E may themselves be estimated: passing these checks establishes geometric
    consistency, not real-world accuracy. No depth map or forced floor plane is used.
    """
    pixels = np.asarray(pixels, dtype=float)
    n = len(pixels)
    if (pixels.shape != (n, 2) or n < 2 or len(intrinsics) != n or len(extrinsics) != n
            or not np.isfinite(pixels).all()):
        raise ValueError('At least two finite observations and matching cameras are required')
    if max_reprojection_pixels <= 0 or not 0 < minimum_ray_angle_degrees < 90:
        raise ValueError('Invalid triangulation acceptance thresholds')
    matrices = [homogeneous_pose(e) for e in extrinsics]
    ks = [checked_intrinsics(k) for k in intrinsics]
    equations = []
    for pixel, k, e in zip(pixels, ks, matrices):
        ray = np.linalg.solve(k, [*pixel, 1.])
        ray /= ray[2]
        equations.extend([ray[0] * e[2] - e[0], ray[1] * e[2] - e[1]])
    _, _, vh = np.linalg.svd(np.asarray(equations))
    candidate = vh[-1]
    if abs(candidate[3]) < 1e-12:
        raise ValueError('Degenerate ray angle: intersection at infinity')
    point = candidate[:3] / candidate[3]
    if any(transform_points(point, e)[2] <= 0 for e in matrices):
        raise ValueError('Triangulated point lies behind a camera')
    residual = [float(np.linalg.norm(project_point(point, k, e) - pixel))
                for pixel, k, e in zip(pixels, ks, matrices)]
    if max(residual) > max_reprojection_pixels:
        raise ValueError(f'Landmark reprojection exceeds uncertainty: {max(residual):.3f}px')
    origins = np.array([np.linalg.inv(e)[:3, 3] for e in matrices])
    directions = point - origins
    lengths = np.linalg.norm(directions, axis=1)
    if (lengths < 1e-12).any():
        raise ValueError('Point coincides with a camera origin')
    directions /= lengths[:, None]
    angles = [float(np.degrees(np.arccos(np.clip(np.dot(directions[i], directions[j]), -1, 1))))
              for i in range(n) for j in range(i+1, n)]
    if min(angles) < minimum_ray_angle_degrees:
        raise ValueError(f'Insufficient ray angle: {min(angles):.4f} degrees')
    return {'point': point.tolist(), 'reprojectionPixels': residual,
            'minimumRayAngleDegrees': min(angles), 'unit': 'model_relative',
            'metricApproved': False, 'authority': 'consistency of supplied camera estimates'}


def triangulate_axis_line(pixel_pairs, intrinsics, extrinsics, *, minimum_plane_angle_degrees=.5):
    """Intersect two interpretation planes of the same approximately straight member.

    Cut endpoints need not correspond. The member identity and centerline assumption
    must be independently justified; this cannot recover an unobserved truss topology.
    """
    if len(pixel_pairs) != 2 or len(intrinsics) != 2 or len(extrinsics) != 2:
        raise ValueError('An axis requires two observed line intervals')
    planes = []
    for pair, k, e in zip(pixel_pairs, intrinsics, extrinsics):
        pair = np.asarray(pair, dtype=float)
        if pair.shape != (2, 2) or not np.isfinite(pair).all():
            raise ValueError('Invalid image line interval')
        line = np.cross([*pair[0], 1.], [*pair[1], 1.])
        plane = line @ checked_intrinsics(k) @ homogeneous_pose(e)[:3]
        norm = np.linalg.norm(plane[:3])
        if norm < 1e-12:
            raise ValueError('Degenerate line interval')
        planes.append(plane/norm)
    planes = np.asarray(planes)
    direction = np.cross(planes[0, :3], planes[1, :3])
    sine = np.linalg.norm(direction)
    angle = float(np.degrees(np.arcsin(np.clip(sine, 0, 1))))
    if angle < minimum_plane_angle_degrees:
        raise ValueError(f'Insufficient interpretation plane angle: {angle:.5f}')
    direction /= sine
    origin = np.linalg.lstsq(planes[:, :3], -planes[:, 3], rcond=None)[0]
    return {'origin': origin.tolist(), 'direction': direction.tolist(),
            'planeAngleDegrees': angle, 'unit': 'model_relative', 'metricApproved': False}


def point_on_axis_for_pixel(axis, pixel, intrinsics, extrinsics):
    pose = homogeneous_pose(extrinsics)
    inverse = np.linalg.inv(pose)
    pixel = np.asarray(pixel, dtype=float)
    ray = inverse[:3, :3] @ np.linalg.solve(checked_intrinsics(intrinsics), [*pixel, 1.])
    ray /= np.linalg.norm(ray)
    origin, direction = np.asarray(axis['origin']), np.asarray(axis['direction'])
    matrix = np.stack([direction, -ray], axis=1)
    if np.linalg.cond(matrix) > 1e6:
        raise ValueError('Camera ray nearly parallel to the inferred axis')
    parameters = np.linalg.lstsq(matrix, inverse[:3, 3]-origin, rcond=None)[0]
    point = origin+parameters[0]*direction
    error = float(np.linalg.norm(project_point(point, intrinsics, pose)-pixel))
    return {'point': point.tolist(), 'reprojectionPixels': error,
            'raySeparationUnits': float(np.linalg.norm(point-(inverse[:3, 3]+parameters[1]*ray))),
            'unit': 'model_relative'}
