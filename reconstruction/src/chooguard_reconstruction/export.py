"""Export review artifacts outside Git. Per-view surfaces are not a fused digital twin."""

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
import trimesh

from .geometry import depth_surface, polygon_keep_mask, review_from_world, transform_points
from .quality import cross_view_depth_check


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def srgb_to_linear_colors(colors):
    """glTF COLOR_0 is linear, unlike JPEG pixels and conventional colored PLY."""
    rgb = np.asarray(colors)
    if rgb.dtype != np.uint8:
        raise ValueError("Expected 8-bit sRGB input colors")
    encoded = rgb.astype(np.float64) / 255
    linear = np.where(encoded <= .04045, encoded / 12.92, ((encoded + .055) / 1.055)**2.4)
    return np.rint(linear * 255).astype(np.uint8)


def export_prediction(prediction_path, audit_path, output_dir):
    output = Path(output_dir)
    output.mkdir(parents=True, exist_ok=True)
    audit = json.loads(Path(audit_path).read_text())
    with np.load(prediction_path, allow_pickle=False) as data:
        depth, confidence = data['depth'], data['conf']
        images, k, e = data['processed_images'], data['intrinsics'], data['extrinsics']
    selected = audit['fixed_image_set']
    if len(depth) != len(selected) or [x['id'] for x in selected] != audit['recommended_order']:
        raise ValueError("Prediction order must equal the fixed audited image order")
    # Verify input binding via sibling receipt, rather than trusting equal image counts.
    receipt_path = Path(prediction_path).with_name('inference-receipt.json')
    receipt = json.loads(receipt_path.read_text())
    if receipt.get('status') != 'succeeded' or receipt.get('output', {}).get('sha256') != sha256(prediction_path):
        raise ValueError("Prediction hash or successful inference receipt is missing")
    if receipt.get('settings', {}).get('process_res_method') != 'upper_bound_resize':
        raise ValueError("Mask mapping supports only the audited aspect-preserving resize")
    expected = [x['sha256'] for x in selected]
    actual = [x['sha256'] for x in receipt['inputs']]
    if actual != expected:
        raise ValueError("Inference input hashes/order differ from the audited image set")
    transform = review_from_world(e[0])
    scene, all_points, all_colors, views, masks = trimesh.Scene(), [], [], [], []
    for index, item in enumerate(selected):
        h, w = depth[index].shape
        original_w, original_h = item['dimensions_px']
        if (original_h, original_w) != (receipt['inputs'][index]['height'], receipt['inputs'][index]['width']):
            raise ValueError("Audit dimensions_px must match the receipt-bound original image size")
        # 'upper_bound_resize' independently rescales each axis to the processed grid, so a
        # normalized-coordinate mask stays valid per axis even though PATCH_SIZE=14 rounding can
        # make the processed aspect ratio differ slightly from the original; no crop offset exists.
        mask = polygon_keep_mask(h, w, [x['polygon'] for x in audit['mask_proposals'][item['id']]])
        surface = depth_surface(depth[index], confidence[index], images[index], k[index], e[index],
                                keep_mask=mask)
        retained = np.zeros(h*w, bool)
        retained[surface.pixel_indices] = True
        masks.append(retained.reshape(h, w))
        vertices = transform_points(surface.vertices, transform)
        mesh = trimesh.Trimesh(vertices, surface.faces,
                               vertex_colors=srgb_to_linear_colors(surface.colors), process=False)
        if len(surface.faces):
            scene.add_geometry(mesh, node_name=item['id'], geom_name=item['id'])
        all_points.append(vertices)
        all_colors.append(surface.colors)
        views.append({'id': item['id'], 'points': len(vertices), 'triangles': len(surface.faces),
                      'excludedPixels': int((~mask).sum()),
                      'confidenceThreshold': surface.confidence_threshold})
    if not any(x['triangles'] for x in views):
        raise ValueError("No retained surface: insufficient supported coverage")
    scene.export(str(output / 'review-surfaces.glb'))
    points = trimesh.PointCloud(np.concatenate(all_points), colors=np.concatenate(all_colors))
    points.export(str(output / 'review-points.ply'))
    cross_checks = []
    for first in range(len(depth)):
        for second in range(len(depth)):
            if first == second:
                continue
            check = cross_view_depth_check(depth[first], k[first], e[first], depth[second],
                                           k[second], e[second], masks[first], masks[second])
            cross_checks.append({'source': selected[first]['id'], 'target': selected[second]['id'],
                                 **check})
    result = {
        'schemaVersion': 'reconstruction-review-1',
        'status': 'uncalibrated_per_view_surfaces',
        'sourcePredictionSha256': sha256(prediction_path),
        'inputAuditSha256': sha256(audit_path),
        'inferenceReceiptSha256': sha256(receipt_path),
        'colorSpaces': {'input': 'srgb-rgb8', 'ply': 'srgb-rgb8',
                        'glbColor0': 'linear-rgb8-normalized'},
        'viewOrder': audit['recommended_order'], 'views': views,
        'crossViewDiagnostics': cross_checks,
        'coordinates': {'raw': 'DA3 world, optical-z depth, world-to-camera extrinsics',
                        'review': 'right handed, first camera X right/Y up/Z back',
                        'reviewFromRawRowMajor': transform.ravel().tolist(),
                        'units': 'model_relative', 'metersPerUnit': None,
                        'gravityCalibrated': False, 'northCalibrated': False},
        'quality': {'metricApproved': False, 'collisionApproved': False,
                    'watertight': False, 'crossViewFusionApproved': False,
                    'coverage': audit['coverage_description'],
                    'filter': '40th confidence percentile; max 8% relative triangle depth jump',
                    'masks': 'manual conservative polygons; hidden surfaces remain absent'},
        'artifacts': {name: sha256(output / name)
                      for name in ['review-surfaces.glb', 'review-points.ply']},
    }
    (output / 'review-manifest.json').write_text(json.dumps(result, indent=2) + '\n')
    return result


def _stats(values):
    values = np.asarray(list(values), dtype=np.float64)
    values = values[np.isfinite(values)]
    if not len(values):
        return None
    return {'count': int(len(values)), 'median': float(np.median(values)),
            'p90': float(np.percentile(values, 90)), 'max': float(np.max(values))}


def export_sparse_reconstruction(sparse_points_npz, output_dir):
    """Deliver a sparse SfM point cloud as its own artifact.

    This is never fused with the dense per-view DA3 surfaces from export_prediction:
    different engine, different manifest schema, no shared vertices or transform.
    """
    output = Path(output_dir)
    output.mkdir(parents=True, exist_ok=True)
    with np.load(sparse_points_npz, allow_pickle=False) as data:
        xyz, rgb = data['xyz'], data['rgb']
        track_length, error = data['track_length'], data['error']
    if xyz.ndim != 2 or xyz.shape[1] != 3 or rgb.shape != xyz.shape:
        raise ValueError("Expected Nx3 sparse xyz/rgb arrays")
    if rgb.dtype != np.uint8:
        raise ValueError("Sparse point colors must be uint8 RGB, not unscaled floats")
    if len(track_length) != len(xyz) or len(error) != len(xyz):
        raise ValueError("Track length/error arrays must match the point count")
    cloud = trimesh.PointCloud(xyz, colors=rgb)
    cloud.export(str(output / 'sparse-points.ply'))
    manifest = {
        'schemaVersion': 'reconstruction-review-sparse-1',
        'status': ('no_sparse_points_available' if not len(xyz)
                   else 'uncalibrated_sparse_points_not_fused_with_dense_surfaces'),
        'sourceNpzSha256': sha256(sparse_points_npz),
        'engine': 'pycolmap-incremental-mapping', 'pointCount': int(len(xyz)),
        'trackLengthDistribution': _stats(track_length),
        'reprojectionErrorPixelsDistribution': _stats(error),
        'coordinates': {'raw': 'COLMAP native sparse reconstruction (independent of DA3)',
                        'units': 'model_relative', 'metersPerUnit': None,
                        'gravityCalibrated': False, 'northCalibrated': False},
        'quality': {'metricApproved': False, 'collisionApproved': False,
                    'fusedWithDenseSurface': False,
                    'note': 'Sparse SfM points and dense DA3 triangle surfaces are separate '
                            'deliverables from independent, non-calibration-sharing engines.'},
        'artifacts': {'sparse-points.ply': sha256(output / 'sparse-points.ply')},
    }
    (output / 'sparse-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prediction', type=Path, required=True)
    parser.add_argument('--audit', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(export_prediction(args.prediction, args.audit, args.output), indent=2))


if __name__ == '__main__':
    main()
