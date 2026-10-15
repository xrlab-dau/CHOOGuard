"""Run a bounded, explicitly reference-only SfM/MVS study on retained public media.

This command never emits field-survey validation or verified placement records.
Camera reprojection errors, point counts and successful meshing are not metric accuracy.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys
import time

import metric_pipeline as metric


def plan(args):
    metric.require(args.reference_study, "Explicit --reference-study is required; public footage is not a field survey")
    metric.require(args.source.is_file(), "Retained public source file is missing")
    metric.require(args.source.suffix.lower() in {".mp4", ".mov", ".mkv", ".webm", ".m4v"}, "Reference study currently requires a video clip")
    metric.require(args.source_url.startswith(("https://", "http://")), "Retain the public original source URL")
    metric.require(args.clip_start >= 0 and 0 < args.duration <= 30 and 0 < args.fps <= 4, "Small pilot needs start >=0, duration <=30 seconds, fps <=4")
    metric.require(320 <= args.width <= 1280 and .2 <= args.static_top_fraction <= 1, "Pilot image size/mask fraction outside bounded range")
    metric.require(args.duration * args.fps >= 3, "Need at least three overlapping frames")
    ffmpeg = shutil.which("ffmpeg")
    metric.require(ffmpeg is not None, "ffmpeg is missing")
    return {
        "schema": "chooguard.reference-study.v1", "role": "reference_only", "metric_verified": False,
        "placement_export_allowed": False, "source": {"path": str(args.source.resolve()), "sha256": metric.digest(args.source),
        "source_url": args.source_url, "source_id": args.source_id, "capture_date": args.capture_date,
        "upload_date": args.upload_date, "capture_epoch_basis": "single retained video only; capture date may be unknown"},
        "sampling": {"clip_local_start_seconds": args.clip_start, "duration_seconds": args.duration,
                     "fps": args.fps, "width": args.width, "static_top_fraction": args.static_top_fraction},
        "output": str(args.output.resolve()),
        "extraction_command": [ffmpeg, "-v", "error", "-nostdin", "-ss", str(args.clip_start), "-i", str(args.source.resolve()),
            "-t", str(args.duration), "-vf", f"fps={args.fps},scale={args.width}:-2", "-q:v", "2", str(args.output.resolve() / "images/%06d.jpg")],
        "limitations": ["Local clip PTS is not the original video's timestamp; requested section can include earlier keyframes.",
            "Date/scale/origin are unverified. No independent field controls are supplied.",
            "Static top mask deliberately omits crowded floor regions. This pilot cannot establish a complete interior.",
            "Dynamic screens, reflection, repetitive structure and calibration uncertainty require inspection."]}


def run_reference(args):
    import cv2
    import numpy as np
    import pycolmap

    study = plan(args)
    output = Path(study["output"])
    metric.require(not output.exists(), "Fresh pilot output required; preserve previous study evidence")
    metric.require(shutil.disk_usage(output.parent if output.parent.exists() else metric.ROOT).free > 2 * 1024**3, "Need at least 2GiB free disk")
    output.mkdir(parents=True)
    images, masks, sparse = output / "images", output / "masks", output / "sparse"
    images.mkdir(); masks.mkdir(); sparse.mkdir()
    receipts = []

    def save():
        (output / "reference-study.json").write_text(json.dumps(study, indent=2, ensure_ascii=False) + "\n")
        (output / "execution-receipt.json").write_text(json.dumps({"role": "reference_only", "metric_verified": False,
            "status": study.get("status", "running"), "stages": receipts}, indent=2) + "\n")

    def external(argv, label):
        start = time.monotonic()
        with (output / f"{label}.log").open("w") as stream:
            completed = subprocess.run(argv, cwd=output, stdout=stream, stderr=subprocess.STDOUT, check=False)
        receipts.append({"stage": label, "argv": argv, "exit_code": completed.returncode, "elapsed_seconds": time.monotonic() - start,
                         "log": str(output / f"{label}.log")})
        save()
        metric.require(completed.returncode == 0, f"Reference stage {label} failed; retained actual log")

    def internal(callback, label):
        start = time.monotonic()
        try:
            result = callback()
        except Exception as exc:
            receipts.append({"stage": label, "exit_code": 1, "elapsed_seconds": time.monotonic() - start,
                             "api": "pycolmap " + pycolmap.__version__, "reason": str(exc)})
            save()
            raise
        receipts.append({"stage": label, "exit_code": 0, "elapsed_seconds": time.monotonic() - start,
                         "api": "pycolmap " + pycolmap.__version__})
        save()
        return result

    try:
        study["status"] = "running"
        save()
        external(study["extraction_command"], "extract")
        names = sorted(images.glob("*.jpg"))
        metric.require(len(names) >= 3, "Clip extraction produced fewer than three frames")
        for name in names:
            pixels = cv2.imread(str(name))
            metric.require(pixels is not None, "Extracted image cannot be decoded")
            mask = np.zeros(pixels.shape[:2], np.uint8)
            mask[:int(mask.shape[0] * args.static_top_fraction), :] = 255
            cv2.imwrite(str(masks / (name.name + ".png")), mask)
        database = output / "features.db"
        reader = pycolmap.ImageReaderOptions(camera_model="SIMPLE_RADIAL", mask_path=masks)
        features = pycolmap.FeatureExtractionOptions(num_threads=2, use_gpu=False)
        features.sift.max_num_features = 4096
        internal(lambda: pycolmap.extract_features(database, images, camera_mode=pycolmap.CameraMode.SINGLE,
            reader_options=reader, extraction_options=features, device=pycolmap.Device.cpu), "features")
        matching = pycolmap.FeatureMatchingOptions(num_threads=2, use_gpu=False)
        internal(lambda: pycolmap.match_exhaustive(database, matching_options=matching, device=pycolmap.Device.cpu), "matching")
        options = pycolmap.IncrementalPipelineOptions(num_threads=2, random_seed=0)
        options.max_num_models = 3
        models = internal(lambda: pycolmap.incremental_mapping(database, images, sparse, options=options), "mapping")
        study["sfm"] = {"image_count": len(names), "model_count": len(models), "models": [{"id": mid,
            "registered_images": model.num_reg_images(), "points3d": model.num_points3D(),
            "mean_reprojection_error_pixels": model.compute_mean_reprojection_error()} for mid, model in models.items()]}
        save()
        metric.require(len(models) == 1, "Pilot yielded disconnected/no models; do not merge relative gauges")
        model_id = next(iter(models))
        model = models[model_id]
        metric.require(model.num_reg_images() / len(names) >= .8 and model.num_points3D() >= 100,
                       "Pilot lacks 80% connected cameras or sufficient sparse points")
        model_dir = sparse / str(model_id)
        model_dir.mkdir(exist_ok=True)
        model.write(model_dir)
        model.export_PLY(output / "reference_sparse.ply")
        internal(lambda: pycolmap.undistort_images(output / "colmap", model_dir, images,
            num_threads=2, undistort_options=pycolmap.UndistortCameraOptions(max_image_size=args.width)), "undistort")
        for image in (output / "colmap/images").glob("*.jpg"):
            pixels = cv2.imread(str(image))
            mask = np.zeros(pixels.shape[:2], np.uint8)
            mask[:int(mask.shape[0] * args.static_top_fraction), :] = 255
            cv2.imwrite(str(image.with_name(image.stem + ".mask.png")), mask)
        mvs = metric.ROOT / ".tools/openmvs-v2.4.0"
        external([str(mvs / "InterfaceCOLMAP"), "-i", "colmap", "-o", "scene.mvs", "--image-folder", "images", "--max-threads", "2"], "interface")
        external([str(mvs / "DensifyPointCloud"), "scene.mvs", "--resolution-level", "1", "--min-resolution", "160",
            "--max-resolution", str(args.width), "--number-views", "4", "--number-views-fuse", "2", "--ignore-mask-label", "0",
            "--max-threads", "2", "--tower-mode", "0", "--estimate-roi", "0", "--crop-to-roi", "0", "-o", "scene_dense.mvs"], "dense")
        external([str(mvs / "ReconstructMesh"), "scene_dense.mvs", "--max-threads", "2", "--close-holes", "0", "-o", "scene_mesh.ply"], "mesh")
        if args.texture:
            external([str(mvs / "TextureMesh"), "scene_dense.mvs", "--mesh-file", "scene_mesh.ply", "--max-threads", "2", "--ignore-mask-label", "0",
                "--close-holes", "0", "--export-type", "obj", "-o", "reference_textured.obj"], "texture")
        study["status"] = "reconstructed_relative_reference"
        study["artifacts"] = [{"path": str(p.relative_to(output)), "sha256": metric.digest(p), "bytes": p.stat().st_size}
                              for p in output.glob("*") if p.suffix.lower() in {".ply", ".obj", ".mtl", ".png"}]
        save()
        return study
    except Exception as exc:
        study.update(status="failed_quality_or_stage", failure_reason=str(exc))
        save()
        raise


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference-study", action="store_true")
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--source-url", required=True)
    parser.add_argument("--source-id", required=True)
    parser.add_argument("--capture-date", default=None, help="Only an actual known capture date, otherwise omitted/unknown")
    parser.add_argument("--upload-date", default=None)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--clip-start", type=float, default=15)
    parser.add_argument("--duration", type=float, default=12)
    parser.add_argument("--fps", type=float, default=2)
    parser.add_argument("--width", type=int, default=720)
    parser.add_argument("--static-top-fraction", type=float, default=.55)
    parser.add_argument("--texture", action="store_true")
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args(argv)
    try:
        result = run_reference(args) if args.execute else plan(args)
        print(json.dumps(result, indent=2, ensure_ascii=False, allow_nan=False))
        return 0
    except (metric.EvidenceError, OSError, ValueError, RuntimeError) as exc:
        print(json.dumps({"role": "reference_only", "metric_verified": False, "placement_export_allowed": False,
                          "status": "failed_quality_or_stage", "reason": str(exc)}))
        return 2


if __name__ == "__main__":
    sys.exit(main())
