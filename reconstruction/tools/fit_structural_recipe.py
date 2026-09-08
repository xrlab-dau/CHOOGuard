"""Fit a bounded photo-guided joint without pairing different cut endpoints."""

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np

from chooguard_reconstruction.geometry import homogeneous_pose, review_from_world, transform_points
from chooguard_reconstruction.structure import (
    point_on_axis_for_pixel, project_point, resize_pixel_center,
    triangulate_axis_line, triangulate_landmark,
)


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def fit(landmark_path, prediction_path, output):
    annotation = json.loads(Path(landmark_path).read_text())
    receipt = json.loads(Path(prediction_path).with_name('inference-receipt.json').read_text())
    views = annotation['view_order']
    if views != ['B', 'A'] or [annotation['images'][v]['sha256'] for v in views] != [
            item['sha256'] for item in receipt['inputs']]:
        raise ValueError('Landmark and inference image order/hash mismatch')
    if receipt['status'] != 'succeeded' or receipt['output']['sha256'] != sha(prediction_path):
        raise ValueError('Missing successful bound prediction')
    with np.load(prediction_path, allow_pickle=False) as prediction:
        k, e = prediction['intrinsics'], prediction['extrinsics']
        height, width = prediction['depth'].shape[1:]
    landmarks = {x['id']: x for x in annotation['landmarks']}

    def pixel(id, v):
        im = annotation['images'][v]
        return resize_pixel_center(landmarks[id]['observations'][v]['pixel'],
                                   [im['width'], im['height']], [width, height])

    registration = {}
    for id in annotation['registration_markers']:
        if not landmarks[id]['direct_triangulation_candidate']:
            raise ValueError('Registration must use physical correspondence candidates')
        tolerance = min(landmarks[id]['observations'][v]['uncertainty_px'] *
                        width / annotation['images'][v]['width'] for v in views)
        registration[id] = triangulate_landmark([pixel(id, v) for v in views], k, e,
                                                max_reprojection_pixels=tolerance)
    pairs = {'column': ['C1', 'C2'], 'left': ['W1', 'W2'], 'right': ['E1', 'E2']}
    axes = {name: triangulate_axis_line([[pixel(id, v) for id in ids] for v in views], k, e)
            for name, ids in pairs.items()}
    # The visibly shared joint constrains connection, while point identity remains
    # uncertain. Fit axes to lines and trim in B; do not triangulate paired cuts.
    joint = np.asarray(point_on_axis_for_pixel(axes['column'], pixel('J0', 'B'), k[0], e[0])['point'])
    raw_nodes = {'J0': joint}
    diagnostics = {'registration': registration, 'axisFits': axes, 'regularizedMemberResiduals': {}}
    for name, ids in pairs.items():
        axis = {**axes[name], 'origin': joint.tolist()} if name != 'column' else axes[name]
        for id in ids:
            raw_nodes[id] = np.asarray(point_on_axis_for_pixel(axis, pixel(id, 'B'), k[0], e[0])['point'])
        errors = {}
        for index, v in enumerate(views):
            line = np.cross([*pixel(ids[0], v), 1.], [*pixel(ids[1], v), 1.])
            line /= np.linalg.norm(line[:2])
            scale = annotation['images'][v]['width']/width
            maximum = max(abs(np.dot(line, [*project_point(q, k[index], e[index]), 1.])) * scale
                          for q in [joint, raw_nodes[ids[-1]]])
            tolerance = max(landmarks[id]['observations'][v]['uncertainty_px'] for id in ids)
            if maximum > tolerance:
                raise ValueError(f'{name}/{v} axis lies outside annotated uncertainty: {maximum:.2f}px')
            errors[v] = {'maximumNormalResidualOriginalPixels': maximum,
                         'annotationTolerancePixels': tolerance}
        diagnostics['regularizedMemberResiduals'][name] = errors
    transform = review_from_world(e[0])
    constraints = {x['member']: x for x in annotation['projected_width_constraints']}
    fitted_members = []
    section_estimates = {}
    for member in annotation['members']:
        id = member['id']
        start, end = member['graph_from'], member['graph_to']
        midpoint = (raw_nodes[start]+raw_nodes[end])/2
        if id != 'collar_joint':
            estimates = []
            for index, v in enumerate(views):
                observed = constraints[id][v]
                pixel_width = observed.get('width_px', observed.get('approx_width_px'))
                z = transform_points(midpoint, homogeneous_pose(e[index]))[2]
                original_fx = k[index, 0, 0] * annotation['images'][v]['width']/width
                estimates.append(float(pixel_width*z/original_fx))
            section_estimates[id] = estimates
            diameter = float(np.mean(estimates))
        else:
            # The cover is visibly wider; its exact profile is hidden. This ratio
            # is an explicit authoring assumption, never an observed specification.
            diameter = float(np.mean(section_estimates['column_visible_segment'])) * 1.16
        fitted_members.append({'id': id, 'start': start, 'end': end,
                               'section': {'shape': 'round', 'widthUnits': diameter,
                                           'profile': 'three_band_cover_estimate' if id == 'collar_joint' else 'plain_tube',
                                           'authority': 'photo_ratio_estimate' if id == 'collar_joint'
                                           else 'projected_photo_width_estimate'},
                               'colorSrgb': [.76, .77, .77],
                               'endpointStatus': member['endpoint_status']})
    result = {'schemaVersion': 'structural-recipe-1', 'unit': 'model_relative', 'metricApproved': False,
              'nodes': {id: {'pointReview': transform_points(point, transform).tolist()}
                        for id, point in raw_nodes.items()}, 'members': fitted_members,
              'references': {'landmarkSha256': sha(landmark_path),
                             'predictionSha256': sha(prediction_path),
                             'fitterSha256': sha(__file__), 'diagnostics': diagnostics,
                             'sectionWidthEstimatesPerView': section_estimates},
              'limitations': ['Approximate image centerlines and predicted K/E; no metric or gravity calibration.',
                              'Common-junction regularization is an observed connectivity prior within annotated pixel uncertainty.',
                              'Pixel width to round section is an estimate; collar 1.16 ratio, approximate three bands and neutral paint are authoring assumptions.',
                              'Cut caps close the study intervals; they are not observed physical member ends.',
                              'Only the nearest column/collar and two short branches, not the roof grid or station.']}
    Path(output).parent.mkdir(parents=True, exist_ok=True)
    Path(output).write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--landmarks', type=Path, required=True)
    parser.add_argument('--prediction', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = fit(args.landmarks, args.prediction, args.output)
    print(json.dumps({'nodes': len(result['nodes']), 'members': len(result['members']),
                      'diagnostics': result['references']['diagnostics']['regularizedMemberResiduals']}, indent=2))


if __name__ == '__main__':
    main()
