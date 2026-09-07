"""Predicted cross-view depth consistency is a diagnostic, not survey accuracy."""

import numpy as np

from .geometry import backproject, homogeneous_pose, transform_points


def cross_view_depth_check(source_depth, source_k, source_e, target_depth, target_k, target_e,
                           source_keep, target_keep, tolerance=.05):
    world = backproject(source_depth, source_k, source_e)
    points = world[source_keep & np.isfinite(source_depth) & (source_depth > 0)]
    camera = transform_points(points, homogeneous_pose(target_e))
    camera = camera[np.isfinite(camera).all(axis=1) & (camera[:, 2] > 0)]
    projected = camera @ np.asarray(target_k).T
    xy = np.rint(projected[:, :2] / projected[:, 2, None]).astype(np.int64)
    h, w = target_depth.shape
    inside = (xy[:, 0] >= 0) & (xy[:, 0] < w) & (xy[:, 1] >= 0) & (xy[:, 1] < h)
    xy, camera = xy[inside], camera[inside]
    target = target_depth[xy[:, 1], xy[:, 0]]
    usable = target_keep[xy[:, 1], xy[:, 0]] & np.isfinite(target) & (target > 0)
    camera, target = camera[usable], target[usable]
    if not len(target):
        return {'overlapSamples': 0, 'withinToleranceFraction': None,
                'medianRelativeDepthDifference': None, 'tolerance': tolerance}
    difference = np.abs(camera[:, 2] - target) / target
    return {'overlapSamples': len(target),
            'withinToleranceFraction': float(np.mean(difference <= tolerance)),
            'medianRelativeDepthDifference': float(np.median(difference)),
            'tolerance': tolerance,
            'interpretation': 'predicted depth agreement; includes occlusions, not external accuracy'}
