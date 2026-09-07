"""Optical-z depth grids. No implicit scale, gravity, recentering or hole filling."""

from dataclasses import dataclass

import numpy as np


@dataclass
class Surface:
    vertices: np.ndarray
    faces: np.ndarray
    colors: np.ndarray
    pixel_indices: np.ndarray
    confidence_threshold: float | None


def homogeneous_pose(extrinsics):
    e = np.asarray(extrinsics, dtype=np.float64)
    if e.shape == (3, 4):
        e = np.vstack([e, [0, 0, 0, 1]])
    if e.shape != (4, 4) or not np.isfinite(e).all():
        raise ValueError("Expected finite 3x4 or 4x4 world-to-camera pose")
    if (not np.allclose(e[3], [0, 0, 0, 1], atol=1e-6)
            or not np.allclose(e[:3, :3] @ e[:3, :3].T, np.eye(3), atol=2e-3)
            or not np.isclose(np.linalg.det(e[:3, :3]), 1, atol=2e-3)):
        raise ValueError("Camera pose must be a proper rigid transform")
    return e


def transform_points(points, matrix):
    p, t = np.asarray(points), np.asarray(matrix)
    if t.shape != (4, 4) or not np.isfinite(t).all():
        raise ValueError("Expected finite affine 4x4 transform")
    if not np.allclose(t[3], [0, 0, 0, 1]):
        raise ValueError("Projective transform is not an affine point transform")
    return p @ t[:3, :3].T + t[:3, 3]


def review_from_world(first_extrinsics):
    """OpenCV camera X-right/Y-down/Z-forward to glTF X-right/Y-up/Z-back.

    This is camera-relative orientation, NOT measured gravity or geographic north.
    The model's arbitrary unit is retained; there is no meter conversion.
    """
    return np.diag([1., -1, -1, 1]) @ homogeneous_pose(first_extrinsics)


def backproject(depth, intrinsics, extrinsics):
    d, k = np.asarray(depth, dtype=np.float64), np.asarray(intrinsics, dtype=np.float64)
    if d.ndim != 2:
        raise ValueError("Depth must be a HxW grid")
    if (k.shape != (3, 3) or not np.isfinite(k).all() or k[0, 0] <= 0 or k[1, 1] <= 0
            or not np.allclose(k[2], [0, 0, 1]) or abs(np.linalg.det(k)) < 1e-12):
        raise ValueError("Expected invertible pinhole intrinsics with positive focal lengths")
    v, u = np.indices(d.shape)
    pixels = np.stack([u, v, np.ones_like(u)], axis=-1)
    rays = pixels @ np.linalg.inv(k).T
    with np.errstate(invalid="ignore"):
        return transform_points(rays * d[..., None], np.linalg.inv(homogeneous_pose(extrinsics)))


def depth_surface(depth, confidence, colors, intrinsics, extrinsics, *, percentile=40,
                  max_relative_jump=0.08, keep_mask=None):
    d, c, rgb = np.asarray(depth), np.asarray(confidence), np.asarray(colors)
    if d.ndim != 2 or d.shape != c.shape or rgb.shape != (*d.shape, 3):
        raise ValueError("Depth/confidence/color grids must agree")
    if not 0 <= percentile <= 100 or not 0 < max_relative_jump <= 1:
        raise ValueError("Invalid retention thresholds")
    if rgb.dtype != np.uint8:
        raise ValueError("Colors must be uint8 RGB, not unscaled floats")
    valid = np.isfinite(d) & (d > 0) & np.isfinite(c)
    if keep_mask is not None:
        if np.shape(keep_mask) != d.shape or np.asarray(keep_mask).dtype != bool:
            raise ValueError("Keep mask must be a boolean HxW grid")
        valid &= keep_mask
    threshold = float(np.percentile(c[valid], percentile)) if valid.any() else None
    if threshold is not None:
        valid &= c >= threshold
    points = backproject(d, intrinsics, extrinsics)
    index = np.arange(d.size).reshape(d.shape)
    # Two triangles per pixel quad; winding faces the observing optical camera.
    a, b, e, f = index[:-1, :-1], index[1:, :-1], index[:-1, 1:], index[1:, 1:]
    faces = np.concatenate([np.stack([a, b, e], -1).reshape(-1, 3),
                            np.stack([e, b, f], -1).reshape(-1, 3)])
    faces = faces[valid.ravel()[faces].all(axis=1)]
    face_depth = d.ravel()[faces]
    if len(faces):
        faces = faces[np.ptp(face_depth, axis=1) <=
                      max_relative_jump * np.min(face_depth, axis=1)]
    retained = np.flatnonzero(valid)
    remap = np.full(d.size, -1, np.int64)
    remap[retained] = np.arange(len(retained))
    return Surface(points.reshape(-1, 3)[retained], remap[faces], rgb.reshape(-1, 3)[retained],
                   retained, threshold)


def polygon_keep_mask(height, width, exclusions):
    """Normalized original-frame polygons, evaluated at retained pixel centers.

    Caller must prove resize preserves frame aspect and introduces no crop.
    """
    v, u = np.indices((height, width))
    x, y = (u + 0.5) / width, (v + 0.5) / height
    keep = np.ones((height, width), bool)
    for polygon in exclusions:
        p = np.asarray(polygon, dtype=float)
        if p.ndim != 2 or p.shape[1] != 2 or len(p) < 3 or not np.isfinite(p).all():
            raise ValueError("Invalid normalized exclusion polygon")
        if (p < 0).any() or (p > 1).any():
            raise ValueError("Polygon must lie within normalized image bounds")
        inside = np.zeros((height, width), bool)
        for first, second in zip(p, np.roll(p, -1, axis=0)):
            if first[1] == second[1]:
                continue
            cross_x = (second[0] - first[0]) * (y-first[1]) / (second[1]-first[1]) + first[0]
            inside ^= ((first[1] > y) != (second[1] > y)) & (x < cross_x)
        keep &= ~inside
    return keep
