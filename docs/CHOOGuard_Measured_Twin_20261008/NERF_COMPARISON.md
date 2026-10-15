# Actual NeRF reference comparison — Busan retail corridor

The actual **20,000-step directional/hierarchical follow-up** trained on 46 views and reproduced six disjoint RGB views at **19.99dB PSNR**, improving on the preserved Tiny pilot (16.58dB) by 3.41dB. Training took 23.39 minutes on Metal with two CPU threads. The bounded field improves corridor/storefront appearance, while fine architectural details remain blurred. It is not a verified editable interior or a general method ranking.

The direct source → Tiny pilot → directional/hierarchical follow-up → existing MVS figure is `workers/twin/evidence/nerf-reference-20261008/paper-nerf/capacity-comparison.png`. Both actual checkpoints and the fixed camera/split/metric evidence are retained.


## Higher-capacity follow-up — actual directional and hierarchical NeRF

A separate original-paper PyTorch port was actually trained for **20000 fixed steps** with separate coarse/fine D8/W128 fields, position encoding 10, direction encoding 4 and 32 coarse+64 inverse-CDF fine samples. Density receives position only; colour receives the view direction. Its 256-channel bottleneck and head follow the [author implementation](https://github.com/bmild/nerf/blob/14c55567a6d0fbd75d3fd12b0411f98160ba3237/run_nerf_helpers.py); rendering and importance sampling follow [run_nerf.py](https://github.com/bmild/nerf/blob/14c55567a6d0fbd75d3fd12b0411f98160ba3237/run_nerf.py). Width 256→128 and samples 64/128→32/64 remain bounded-run reductions. This is a paper-equation port, not execution of Nerfstudio or the original TensorFlow tool.

| Same six disjoint RGB views | Tiny pilot | Directional/hierarchical follow-up |
|---|---:|---:|
| Fixed training steps | 3000 | 20000 |
| Train time | 72.9s | 1403.4s |
| Parameter count | 126212 | 366728 |
| Fixed train PSNR final | 17.34dB | 21.34dB |
| Held-out static-mask PSNR | 16.58dB | 19.99dB |
| Held-out static-mask MAE | 0.1040 | 0.0633 |

The source is the retained [Busan Station video](https://www.youtube.com/watch?v=RY2qvE0Tugk), uploaded 2023-05-01; its actual capture date is unknown. A 26-second local clip segment supplied 52 undistorted views at 2fps. This is historical public-media reconstruction, not a new field survey. Camera/geometry scale remains unverified.

Measured process peak resident memory during the follow-up is 519.4MiB, and the sampled maximum MPS driver allocation is 1131.8MiB. These are distinct overlapping memory measures, not values to add. The CPU thread limit is 2.

The same diagnostic views were already inspected in the pilot. Their colours remain excluded from follow-up training, and the 20k schedule was fixed before follow-up evaluation; this is not a fresh blind test. Existing MVS still used all 52 source images and is not an equal-training-data comparison. The 73-second Tiny pilot alone cannot establish whether NeRF is suitable for Busan Station.

Official Nerfstudio platform/dependency assessment is retained in `workers/twin/evidence/nerf-reference-20261008/nerfstudio-platform-assessment.json`. Current [train.py](https://github.com/nerfstudio-project/nerfstudio/blob/50e0e3c70c775e89333256213363badbf074f29d/nerfstudio/scripts/train.py) explicitly supports CPU/MPS and the vanilla fields have Torch encodings despite the README's general NVIDIA/CUDA setup. Actual binary-only resolution failed on Python 3.14 (`xatlas`) and installed Python 3.13 (`fpsample`). The existing Blender Python 3.11 ABI resolves all 230 wheels. Bounded ZIP directory reads verify 509,633,000 bytes compressed plus 1,677,057,242 bytes expanded: the standard installation peak is 2,186,690,242 bytes before these experiment artifacts, above the 2GB limit. No package was installed or source build launched. Selective sequential installation was not attempted. These environment/budget constraints do not demonstrate that Nerfstudio or NeRF inherently requires CUDA or cannot run on this Mac.

Primary code, documentation, licences and compatibility evidence are registered in `asset-library/research-public/2026-10-08/nerf-methods/receipts.json`, with URLs, content types, acquisition times, SHA256 and byte counts. Both CPU and MPS checks exercise inverse-CDF boundaries, colour direction dependence, density direction independence and finite coarse/fine gradients.

The actual follow-up checkpoint is `workers/twin/output/nerf-reference-20261008/paper-nerf/checkpoint.pt`, SHA256 `ae9bf3e2a7c9a92a5026c721fe2b39af99e9b69b2b83d8697af94f250306036b`. Its source images, rendered novel views, initial-field views, observed MVS baseline, depth/opacity and fixed manifest are under `workers/twin/output/nerf-reference-20261008/paper-nerf`. The same-camera montage is `workers/twin/evidence/nerf-reference-20261008/paper-nerf/heldout-comparison.png`; actual metrics/timing and hierarchy/direction/autograd QA are under `workers/twin/evidence/nerf-reference-20261008/paper-nerf`. All geometry/appearance remains in the source block's unknown relative gauge. No density mesh or verified wall was installed.

The radiance field produces an image from a camera ray. It does not provide separately editable shop walls, ceiling panels, doors or interaction parts, and it does not verify their metric locations. Its opaque-far integration returns a full image even where surface support is weak; filled pixels are not proof of reconstructed geometry. Existing observed MVS provides explicit photographed triangles that can be inspected/edited while its unobserved holes remain visible. Both require independent registration and structural evidence before any production placement is certified.

Reproduce: `OMP_NUM_THREADS=2 OPENBLAS_NUM_THREADS=2 VECLIB_MAXIMUM_THREADS=2 python3 workers/twin/nerf_reference.py --method paper --steps 20000 --samples 32 --fine-samples 64`.

Actual saved-image review: Corridor depth, wall/column outlines, ceiling light positions and the close storefront appearance are substantially clearer than the Tiny pilot. Fine tile joints, signage text, cabinet detail and some boundary edges remain blurred/distorted in this bounded20k run. The improvement is evidence for useful local view reproduction, not a physical location/complete interior certificate or a judgment on allNeRFmethods. ExistingMVSretains photographed detail and visibleholes; it used all52frames and therefore doesnotprovide anequal-supervisionranking.

Actual checkpoint reload on CPU and Metal passes on 128 fixed rays; maximum absolute RGB difference 0.00000286. Source image/pose/sparse/MVS hashes remained unchanged.

## Initial Tiny-NeRF pilot (preserved)

An actual local Tiny-NeRF-family radiance field was trained on 46 source views and rendered at 6 fixed views whose RGB images were excluded from training. This is a bounded PyTorch port of the original authors' [Tiny-NeRF notebook](https://github.com/bmild/nerf/blob/14c55567a6d0fbd75d3fd12b0411f98160ba3237/tiny_nerf.ipynb), not the full paper model. No scene geometry was installed or certified.

| Actual measured run | Result |
|---|---:|
| Device / CPU thread limit | mps / 2 |
| Steps / rays per step / samples per ray | 3000 / 256 / 64 |
| Network / train width / eval size | D8 W128 posenc6 / 192 / 320×177 |
| Train time | 72.9s |
| Fixed training rays PSNR, initial → final | 11.91 → 17.34dB |
| Held-out NeRF PSNR, upper85% mask | 16.58dB |
| Held-out NeRF MAE, encoded RGB [0,1] | 0.1040 |
| Existing observed MVS coverage, same mask | 93.9% |
| Existing MVS PSNR on its observed pixels | 19.39dB |

**These scores are not an equal-data method ranking.** Existing MVS used all52 images for geometry and texture, including the NeRF-held-out views. NeRF's colours are disjoint; calibration/SfM poses are still jointly estimated from all views. Static upper-region temporal interpolation is weaker than an independent field capture. MVS common-pixel scores exclude preserved holes; full-mask scores count holes as black and are recorded separately.

Camera QA independently reads COLMAP binary quaternion/translation and sparse observations, verifies JSON camera poses, OpenGL/CV direction conversion, separate fx/fy and pixel-centre resizing. A constant-volume, empty-density and actual autograd check verifies the emission/absorption renderer on the selected device. Source hashes, fixed splits and normalization are retained.

The [official dependency README](https://github.com/bmild/nerf/blob/14c55567a6d0fbd75d3fd12b0411f98160ba3237/README.md) requires TensorFlow1.15, unavailable in the installed Python3.14 environment. This local port uses installed PyTorch, D8 depth with width reduced256→128, position-only encoding6, unit rays, stratified coarse samples and the original opaque-far convention. It has no view-dependent head, fine hierarchical network, occupancy acceleration, mesh extraction or NeRF-paper fidelity claim.

Artifacts:

- `workers/twin/output/nerf-reference-20261008/checkpoint.pt`: actual trained network checkpoint; SHA256 `d348b35e15fe4a73d06f3e55574411568be2ffa88e0c28a23dcf0f37bf570eb8`.
- `workers/twin/output/nerf-reference-20261008/dataset-manifest.json` and `transforms.relative.json`: exact split, source hashes and relative camera conversion.
- `workers/twin/output/nerf-reference-20261008/heldout/`: six source/render/MVS images plus relative depth/opacity arrays.
- `workers/twin/evidence/nerf-reference-20261008/heldout-comparison.png`: source → trained NeRF → existing MVS → error montage.
- `workers/twin/evidence/nerf-reference-20261008/evaluation.json`, `training-receipt.json`, `camera-conversion-qa.json`, `volume-render-qa.json`: actual timing, metrics and QA.
- `workers/twin/evidence/nerf-reference-20261008/primary-source/`: original notebook, MIT licence, dependency README and implementation provenance.

Held-out image names: 000006.jpg, 000014.jpg, 000022.jpg, 000030.jpg, 000038.jpg, 000046.jpg. Image supervision never loads these in the training routine. Normalization and bounds use sparse points with at least two training-camera track supports. Source video is historical public media in an unverified relative gauge, not an independent metric survey. Dense appearance/depth is not evidence of unseen walls or a metric-correct interior. The resulting field remains reference-only and is not promoted to production geometry.

Reproduce locally: `OMP_NUM_THREADS=2 OPENBLAS_NUM_THREADS=2 VECLIB_MAXIMUM_THREADS=2 python3 workers/twin/nerf_reference.py --steps 3000`. CPU can be selected with `--device cpu`; no CUDA dependency or cloud job is required.

Visual inspection of all six saved comparison rows: this short Tiny-NeRF run remains blurred/fog-like. Ceiling tiles, doorway edges, signs and cabinet detail are not reliably resolved. The experiment trains and renders successfully, but its architecture detail is insufficient for digital-twin replacement. Existing MVS retains observed detail, with visible holes and distortion. The saved initial untrained field pooled PSNR is 11.97dB; the trained field improves to16.58dB. CPU checkpoint reload reproduces128 Metal-rendered sample pixels within 0.001960 encoded RGB, including PNG rounding; independent CPU and Metal volume/autograd checks passed.


