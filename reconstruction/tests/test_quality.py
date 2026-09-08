import numpy as np

from chooguard_reconstruction.quality import cross_view_depth_check


def check(e, mask=None):
    d = np.full((4, 4), 3.)
    keep = np.ones_like(d, bool)
    return cross_view_depth_check(d, np.eye(3), np.eye(4), d, np.eye(3), e,
                                  keep, keep if mask is None else mask)


def test_identity_predicted_depth_consistency():
    result = check(np.eye(4))
    assert result['withinToleranceFraction'] == 1
    assert result['medianRelativeDepthDifference'] == 0


def test_wrong_camera_translation_is_detected():
    pose = np.eye(4)
    pose[2, 3] = 3
    result = check(pose)
    assert result['withinToleranceFraction'] == 0
    assert result['medianRelativeDepthDifference'] == 1


def test_no_overlap_is_unknown_not_pass():
    result = check(np.eye(4), np.zeros((4, 4), bool))
    assert result['overlapSamples'] == 0
    assert result['withinToleranceFraction'] is None
