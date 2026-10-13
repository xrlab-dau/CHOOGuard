# Large-building reconstruction for CHOOGuard

Research checked 2026-10-11; tool integration checked 2026-10-13. The initial
recommendation is **CityGaussianV2 as a reproducible baseline**, compared against
CityGS-X and COLMAP MVS on the same captured region. This is an implementation-fit
decision, not an established universal ranking.

## What exists and what still needs input

The Korean presentation was prepared first: 16 main slides, two supplied Unity demo
videos, and five appendix/reference slides, with speaker notes and a PDF. Its research
examples are credited to their authors. The supplied CHOOG videos show the existing
Unity project; they are not field captures of a building.

The [surface-cloud worker](../../workers/reconstruction/README.md) implements the
extracted-mesh-to-point-cloud handoff with reproducible samples and coordinate metadata.
Its CloudCompare adapter now verifies a real SOR/subsampling run on analytic test
geometry, retaining both stages, counts, logs and hashes. This is tool interoperability
evidence, not a reconstructed building or an automatic quality approval.
No new CHOOGuard building has yet been trained, cleaned in CloudCompare, processed in
Blender, or accepted in a Unity build through this pipeline. A real capture set and
scale evidence are still required. The inspected workstation has a 4 GB RTX 3050 Laptop;
full-building training has not been demonstrated on it.

## Why this method

| Method | Primary evidence | Role in the comparison |
|---|---|---|
| Mega-NeRF / Block-NeRF | [CVPR 2022 factory example](https://meganerf.cmusatyalab.org/), [city blocks](https://waymo.com/research/block-nerf/) | Large-scene decomposition; view synthesis is not an accepted collision mesh |
| 2DGS / SuGaR | [SIGGRAPH 2024](https://surfsplatting.github.io/), [CVPR 2024](https://anttwo.github.io/sugar/) | Surface-oriented Gaussian representation and mesh extraction |
| VastGaussian | [CVPR 2024](https://vastgaussian.github.io/) | Large-scene cell partition and appearance/rendering comparison |
| CityGaussianV2 | [ICLR 2025 project](https://dekuliutesla.github.io/CityGaussianV2/), [official code](https://github.com/Linketic/CityGaussian) | First baseline: large-scene processing plus documented surface extraction |
| CityGS-X | [ICCV 2025 paper](https://openaccess.thecvf.com/content/ICCV2025/papers/Gao_CityGS-X_A_Scalable_Architecture_for_Efficient_and_Geometrically_Accurate_Large-Scale_ICCV_2025_paper.pdf), [official code](https://github.com/gyy456/CityGS-X) | Strong alternative with depth constraints and mesh output |
| UrbanGS / Urban-GS | [Li et al., ICLR 2026](https://arxiv.org/abs/2602.02089), [Wang et al., CVPR 2026](https://github.com/wangm-buaa/Urban-GS) | Distinct works: verify reproducibility and mesh path before adoption |

The ICLR UrbanGS paper describes future code release; this investigation did not
obtain an official execution repository for it. The CVPR Urban-GS repository is public
and focuses on aerial-to-street rendering/compactness; its README does not document
the same mesh handoff. Do not conflate the two names or infer absence of code forever.

Gaussian Splatting and NeRF are related radiance-field approaches with different
representations. A Gaussian PLY stores rendering primitives, not necessarily samples
on walls/floors. Use depth fusion or an extracted surface before passing geometry
to CloudCompare. Paper FPS, image scores and geometry scores apply to their stated
hardware/data/evaluation conditions, not this project's unmeasured Unity performance.

## Handoff sequence

1. **Capture and camera registration.** Preserve original files and hashes. Capture
   overlapping views with camera translation, both sides of important openings, and
   scale/control evidence. Inspect blur, reflection, repeated columns and moving
   people. Record COLMAP registration/reprojection results and disconnected models.
2. **Train and extract.** Fix code commit, settings, image resolution, held-out views,
   hardware and scale. Start with one connected region. Use the official
   [CityGaussianV2 sequence](https://github.com/Linketic/CityGaussian/blob/main/doc/run%26eval.md):
   coarse model, partition, regional training, merge and `gs2d_mesh_extraction.py`.
   Keep a COLMAP MVS baseline and compare CityGS-X under matching conditions.
3. **Create the point cloud.** Run `workers/reconstruction/surface_cloud.py` on the
   extracted PLY mesh. Keep the mesh as well. Record explicit units, source kind,
   scale evidence, axes and both coordinate transforms in the bundle.
4. **CloudCompare cleanup.** Run `workers/reconstruction/clean_cloud.py` on the sampling
   bundle with explicit SOR and spacing settings. Retain raw, SOR and sampled data;
   record any separate crop or mask operation. Inspect thin structures and important cross-sections.
   AI can propose masks and settings; measured effects and observation evidence
   decide acceptance. A small before/after distance is not absolute survey accuracy.
5. **Blender asset work.** Verify the retained mesh against the cloud, or explicitly
   reconstruct a surface and compare it. Repair relevant topology, bake color/normal
   textures, partition assets and simplify while preserving doors/stairs/railings.
   Prepare separate collision and interaction objects. Preserve unobserved regions
   as unknown or labelled estimates.
6. **Unity acceptance.** In an isolated scene, check metre scale, axes, ground and
   joins; configure LOD/loading and colliders/NavMesh; exercise player and NPC routes.
   Connect existing training behavior without replacing JEV decisions with scripted
   incidents. Measure actual target-build frame times, memory and loading. Follow
   repository checks and review before merging.

Existing Busan transfer geometry has unresolved joins/heights. See
[the existing source-based reconstruction record](../CHOOGuard_Story_Plan_v5/BUSAN_TRANSFER_RECONSTRUCTION.md).
New reconstruction must not turn those uncertainties into assumed surveyed facts.

## Small-region acceptance record

For each method, record input hashes, code/config versions, camera registration,
measured control-length errors, missed important structures, processing time, peak
VRAM, output size, point/face counts, and target Unity build behavior/performance.
Use separate controls for fitting and evaluation. F1/Chamfer comparisons require a
reference surface, alignment, evaluation region and tolerance; do not invent them.

90 Hz gives an 11.1 ms frame budget as an example. The actual target device and quality
settings determine the budget. A high-resolution splat viewer benchmark does not
establish VR performance, interaction or collision correctness.

Code/data licences remain separate. The CityGaussian repository declares CC BY-NC-SA
4.0; dependencies, pretrained weights and capture rights need their own records
before redistribution or product use. No public dataset or third-party figure is
added to Unity Assets by this change.
