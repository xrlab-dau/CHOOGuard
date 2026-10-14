# Photographic COLMAP baseline

`workers/reconstruction/colmap_mvs.py` supplies a reproducible MVS comparison for
the planned CityGaussianV2/CityGS-X evaluation. It uses the installed official
COLMAP commands rather than implementing camera estimation, depth fusion or meshing.
The inputs are photographs and **one already registered sparse model**. Registration
is not rerun, and a successful receipt does not establish metre scale or accuracy.

## Run

Install the locked reconstruction Python dependencies described in the
[worker README](../../workers/reconstruction/README.md). Install COLMAP separately
with CUDA MVS support and a compatible GPU/driver. The wrapper neither downloads
software nor changes drivers. Use an actual executable path, such as the official
Windows portable package's `bin/colmap.exe`, rather than a shell command string.

```powershell
python workers/reconstruction/colmap_mvs.py capture/images capture/sparse/0 `
  artifacts/reconstruction/mvs-001 --colmap $ColmapExe `
  --source-reference "capture ID and camera-registration provenance" `
  --max-image-size 640 --source-images 10 --iterations 3 `
  --poisson-depth 8 --threads 6 --cache-gb 2 --gpu-index 0
```

The output directory must be new and outside both inputs. Model files can be
`cameras/images/points3D.bin` or `.txt`. Paths containing spaces and Korean text
remain individual process arguments. Defaults are an exploratory low-resolution
configuration, not recommended production quality. GPU memory requirements also
depend on the images, source views and software version.

The worker fingerprints the original images/model and executable, logs the actual
version and model analyzer output, and executes:

1. Image undistortion and downsampling, selecting up to the requested source views.
2. CUDA PatchMatch with geometric consistency enabled.
3. Fusion of the geometric depth maps to `fused.ply`.
4. Poisson reconstruction to `meshed-poisson.ply`.

`dense/` retains undistorted images, model, configuration and intermediate depth
maps. `request.json` records commands, settings and input hashes; individual logs
and `stages.json` record progress. Each command has a default one-hour timeout
(`--timeout-seconds` changes it). Local requests/logs contain filesystem paths;
review them before sharing. No network request is made by the wrapper.

Only completed processes with a nonempty finite xyz/normal/RGB cloud and a finite
positive-area mesh get `receipt.json`. The worker also rechecks input/tool hashes.
A failure, timeout, missing output or changed input leaves diagnostics without a
success receipt. Preserve failed runs and choose a fresh directory for retry.

## Units and acceptance

The receipt explicitly stores `unknown_model_units` and null scale. Bounds are
labelled `bounds_model_units`; there is no implicit 1-unit-to-1-metre conversion.
This bundle cannot be passed directly to the metre-based CloudCompare adapter.
Establish scale and a verified coordinate frame, then use the retained mesh with
`surface_cloud.py` and the documented CloudCompare/Blender handoffs.

Fused depth points differ from sparse feature points and Gaussian rendering
centers. Poisson can smooth boundaries or bridge unsupported regions. Inspect
openings, stairs, rooflines, vegetation, floaters and holes against the photographs
and fused points. Finite coordinates, a low reprojection error, or a successful
process do not establish metric accuracy, completeness or Unity readiness.

## Public example and provenance

The official [South Building example](https://colmap.github.io/datasets.html)
contains 128 photographs of UNC Chapel Hill's South Building, credited to
Christopher Zach. The downloaded archive also includes a database and sparse model.
The supplied model has 128 registered images, 61,514 sparse points and 326,515
observations, with mean reprojection error 0.497032 pixels. These statistics are
from the supplied model, not a new registration result or measured survey error.

Acquisition URLs, byte counts, SHA256 fingerprints and licence notes are retained in
[`receipts.json`](../../asset-library/research-public/2026-10-14/colmap-south-building/receipts.json).
The complete ZIP size and CRC were checked before extraction. The release API did
not supply publisher digests; these hashes identify the local downloads and are
not independent publisher verification. Code and data licences are separate:
COLMAP uses BSD-3-Clause, bundled dependencies retain their own terms, and a separate
dataset licence was not found on the landing page or in this archive. No photographs,
archive, executable, database or reconstructed model is redistributed in this PR.

The workstation used for the experiment has an RTX 3050 Laptop GPU with 4 GB VRAM,
driver 545.92, and the official COLMAP 3.11.1 Windows CUDA package (commit 682ea9a).
The package's build workflow specifies CUDA 12.6.2. This execution is a compatibility
observation for this machine, not a guarantee for other CUDA builds or drivers.

## Sources

- [COLMAP 3.11 CLI and native pipeline](https://colmap.github.io/legacy/3.11/cli.html)
- [Official 3.11.1 release](https://github.com/colmap/colmap/releases/tag/3.11.1)
- [Official Windows build configuration](https://github.com/colmap/colmap/blob/3.11.1/.github/workflows/build-windows.yml)
- [COLMAP licence](https://github.com/colmap/colmap/blob/3.11.1/COPYING.txt)
- [NVIDIA CUDA minor-version compatibility](https://docs.nvidia.com/deploy/cuda-compatibility/minor-version-compatibility.html)

## Executed result: 2026-10-14

The worker completed on all 128 supplied registered views using 320 pixels,
five source views, one PatchMatch iteration and Poisson depth 7. The original images
and sparse-model hashes were unchanged. The [machine-readable record](colmap-south-building-20261014.json)
contains settings, output hashes, bounds and stage timings.

| Output | Observed result |
|---|---:|
| Fused cloud | 136,938 points; 3,697,573 bytes; stored xyz/normals/RGB |
| Poisson mesh | 943 vertices; 1,537 triangles; 39,354 bytes |
| Undistortion / PatchMatch / fusion / meshing | 27.484 / 289.687 / 48.641 / 1.375 seconds |

This low-resolution run verifies native execution and file contracts. The mesh is
very coarse and has substantially smaller bounds than the fused cloud; that alone
cannot quantify completeness because the cloud also contains distant floaters.
An internal plot of the actual colored points shows building facades, missing roof
coverage and scattered points outside the main structure. Poisson's native trim
default of 10 was left unchanged. The output is not an accepted building asset.
Timings overlap another local 640-pixel experiment and are not isolated performance
benchmarks. Peak VRAM was not measured.

A separate real CloudCompare 2.13.2 SOR trial (`neighbors=16`, `sigma=2`) retained
136,442 of 136,938 points, removing 496. Exact xyz/RGB subset verification passed;
stored normals remained finite and unit length. The raw cloud is retained, no
spatial subsampling was applied, and units remain unknown. The 496 removed points
are not all established errors; compare thin structures and removed regions before
acceptance. This is parameter-assisted geometric filtering, not an implemented AI
semantic mask. The local CLI used a new working directory and the following form:

```powershell
& $CloudCompareExe -SILENT -AUTO_SAVE OFF -C_EXPORT_FMT PLY -PLY_EXPORT_FMT BINARY_LE `
  -O -GLOBAL_SHIFT 0 0 0 $FusedPly -SOR 16 2 -SAVE_CLOUDS FILE ./fused_sor.ply
```

The seven new offline contract tests cover successful output parsing and provenance,
unknown units, process failure/timeout, changed input/tool, existing/nested output,
invalid inputs and malformed/missing geometry. The combined suite ran 41 tests:
38 passed and three pre-existing optional CloudCompare/Blender integrations were
skipped. Those skips do not stand in for the separate native COLMAP and SOR runs
reported above. `graphify update .` could not run because neither the CLI nor MCP
provider is installed; the knowledge graph was not refreshed.
