# Surface point-cloud handoff

Turn an **extracted triangle mesh** into a surface point cloud for CloudCompare.
This offline tool does not train NeRF/Gaussian Splatting or certify building accuracy.
For CityGaussianV2, start with the official mesh extraction output (for example
`fuse_post.ply`), not the Gaussian parameter PLY or COLMAP sparse points.

## Install and run

Use Python 3.12+ in a dedicated environment. No Unity licence, CUDA or paid service is
required. The locked dependencies are NumPy 2.5.3 and trimesh 5.1.1; both are reused
instead of introducing another mesh parser or sampling algorithm.

```sh
python -m venv .venv-reconstruction
# Activate that environment using your shell, then:
python -m pip install --require-hashes --no-deps -r .github/requirements/reconstruction.txt
python -m unittest discover -s workers/reconstruction -p 'test_*.py' -v
python workers/reconstruction/surface_cloud.py --help
```

Example **after** establishing the input's scale and axes:

```sh
python workers/reconstruction/surface_cloud.py capture/fuse_post.ply artifacts/reconstruction/run-001 \
  --points 1000000 --seed 42 --meters-per-unit 1 \
  --origin 0 0 0 --axes "right-handed, +Z up, +X east" \
  --source-kind reconstruction-mesh \
  --source-reference "capture run ID, model commit, extraction settings" \
  --scale-reference "survey/control-point record establishing metres"
```

The axes and scale text above are placeholders, not facts about a new capture.
On PowerShell, use one line or replace the shell continuations with backticks;
on this Windows workstation the launcher outside a venv is `py -3`.

## Output contract

The output directory must be new. A successful run writes:

- `surface_raw.ply`: binary little-endian xyz, face normals, and RGBA only if the mesh
  supplied colors. Samples follow triangle **area**, not vertex density. Vertex
  colors are interpolated using barycentric coordinates; face colors remain constant.
- `receipt.json`: input/output SHA256, source classification, sampling settings,
  dependency versions, bounds, area, skipped zero-area faces, and transforms.

The transformation is `local_metres = (source - origin) * meters_per_unit`.
Both directions of this transform are recorded. Axis directions and handedness are
preserved: **no automatic Blender/Unity axis conversion** occurs. Keep the receipt
with the PLY. Apply the same transform to the original mesh when comparing them.

Input PLY is decoded by trimesh with automatic geometry processing disabled. Files
with Gaussian attributes, no faces, invalid indices, non-finite coordinates, or no
positive-area faces are rejected. Texture-only input needs an explicit vertex-color
bake. Normals follow triangle winding and are not automatically oriented outward.
Zero-area faces are counted and excluded; other topology is not repaired.

The library exports float32 coordinates. The converter rejects sampling when its
float32 rounding error exceeds 1 mm; supply a nearby source-space `--origin` when
working with large global coordinates. This is a **storage precision check**, not a
claim of 1 mm reconstruction accuracy. Input units require independent evidence.
Recentering cannot recover precision already lost in a float32 global-coordinate
input; retain high-precision source geometry and establish a local origin early.

No source file or existing output directory is overwritten. The receipt is written
last; a directory without a valid receipt is incomplete (for example after disk
exhaustion) and must not be used as a successful run. Use a fresh directory for a retry.
Generation loads a mesh and the sampled arrays into memory; start with a small region
and sample count before processing a whole building.

## CloudCompare and Blender

For a verified local-metre sampling bundle, run the cleanup adapter. These parameters
are a **starting example**, not a tuned building preset:

```powershell
python workers/reconstruction/clean_cloud.py artifacts/reconstruction/run-001 `
  artifacts/reconstruction/clean-001 --cloudcompare $CloudCompareExe `
  --tool-version "2.13.2 official Windows x64 archive" `
  --neighbors 16 --sigma 2 --spacing-m 0.02
```

The executable must already be installed; the adapter does not download or install
anything. `--tool-version` records your declared version/provenance, while the tool
also hashes the actual executable. The Windows portable archive tested here is
linked from the [official download page](https://www.cloudcompare.org/release/index.html).

Choose a **new** output directory and retain the original bundle. The adapter verifies
the input against its sampling receipt, runs CloudCompare silently with explicit zero
global shift, and saves both `surface_sor.ply` and `surface_clean.ply`. It checks that
each stage only removes existing xyz/RGB records, with duplicate counts respected.
It never automatically shifts adjacent regions or changes their axes/units.

`receipt.json` records stage counts/bounds/hashes, separate SOR and subsampling removal
rates, parameters, command and executable hash. `sampling_receipt.json` carries the
original provenance and transform; `request.json`, `process.log` and `cloudcompare.log`
support diagnosis. A timeout (default 30 minutes), failed process, missing output,
changed input or invalid output produces **no success receipt**. Preserve that failed
directory for inspection and retry in a new one. Processing and verification load
arrays into memory; start with a small region. The adapter does not extract a rejected
point cloud: retain raw and both stages to inspect the removed regions in CloudCompare.

CloudCompare omits alpha and compresses normals. Nonopaque input is rejected; retained
xyz/RGB must match exactly, and stored normals must remain finite and unit length.
The adapter does not claim identical normals, deterministic CloudCompare point order,
or validation of the declared tool version. Direct CLI `-SAVE_CLOUDS FILE` parses its
argument again as a filename list: the adapter uses fixed relative filenames in its
new output directory, covering paths containing spaces and Korean text.

The final status is `cleanup_complete_review_required`. Compare stairs, railings and
door openings before/after filtering; sparse valid structures can be removed by SOR.
AI masks/parameter suggestions still need this geometric review. A large reduction
from spatial subsampling is **not** a measured outlier rate.

Keep the original extracted mesh for Blender. A point cloud alone has no faces for
Unity rendering/collision: either edit the retained mesh or explicitly reconstruct a
surface from the cleaned cloud and compare the two paths. Bake colors/textures,
reduce geometry with important boundaries preserved, and author separate colliders.
See [the project workflow](../../docs/reconstruction/README.md) for the Unity gate.

## Verified scope and provenance

Tests use analytic triangles to check interior samples, area ratios, color gradients,
face normals, scale/origin, byte-level PLY layout, invalid inputs and no-overwrite
behavior. They do not substitute for a field-capture or Unity build acceptance test.
Do not label `test-fixture` or `existing-model` output as a reconstructed real building.

Cleanup contract tests run in normal worker CI without a CloudCompare installation.
For a real integration run, set `CLOUDCOMPARE_EXE` to the executable and optionally
`CLOUDCOMPARE_VERSION`, then run the same unittest command above. Without this variable,
the integration test is explicitly skipped. On 2026-10-13, all 26 tests passed on
Windows with official CloudCompare 2.13.2, including real colored/uncolored clouds
and Korean/space-containing paths. A separate **analytic triangle** run used 100,000
samples: SOR retained 97,058, and 0.02 m subsampling retained 3,111. These numbers
demonstrate interoperability only; they are not building accuracy or quality results.

- [CityGaussianV2 extraction](https://github.com/Linketic/CityGaussian/blob/main/doc/run%26eval.md)
- [trimesh surface sampling](https://trimesh.org/trimesh.sample.html)
- [trimesh source and MIT licence](https://github.com/mikedh/trimesh/tree/5.1.1)
- [NumPy BSD licence](https://numpy.org/doc/stable/license.html)
- [CloudCompare CLI](https://cloudcompare.org/doc/wiki/index.php/Command_line_mode)
- [Global Shift and Scale](https://cloudcompare.org/doc/wiki/index.php/Global_Shift_and_Scale)
