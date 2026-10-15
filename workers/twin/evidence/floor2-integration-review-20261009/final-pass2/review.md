# Independent final source and facade review

Verdict: no remaining concrete serialized-source or facade geometry blocker found in reviewed state. Read-only work; no Unity execution, Assets changes or canonical writes by reviewer.

## Exact snapshots and results

- Before native stair: scene SHA256 `80a55958f086fd89ca524f1124643003a4df387eca05869dea61424650f15af9`.
- Saved native stair snapshot, taken from the exact facade-before backup: `814c6e483496233b5e6fb4450f8a9423f293f2b8febf37423d0e4aa95fcea555`.
- Actual saved scene after facade installation: `062a4f1ce2be1e5f9d2f53df56bbd081684a8d993f01d13bdd27b182df4af200`.
- `serialized-review.json`: 129 checks passed, zero failed. `saved-facade-review.json`: 82 checks passed, zero failed. These are independent serialized-content checks, not runtime or photo-capture results.

## Native stair evidence

The strict scene diff changes only Transform 829381565's child list, MeshCollider 829381566's mesh reference and MeshFilter 829381568's mesh reference. No existing block is removed. Exactly six child blocks are added: GameObject, Transform, MeshFilter, MeshRenderer, MeshCollider and one static SourcePartIdentity.

All four serialized partition assets retain the original 94,705 complete vertex rows, byte-identical vertex layout and channels, 68 triangle submesh slots, face order and winding. They exactly match the sealed expected binaries. Selected 1,306 faces plus retained 96,428 render faces or 100,236 collider faces independently cover their original sources once with no overlap. Each partition's bounds follow referenced vertices. The selected physical and visual partitions coincide.

The child has identity local TRS and the original parent world frame. Child renderer fields and all 68 material slots equal the unchanged parent renderer; physics fields equal the original collider, excluding the declared mesh/GameObject references. Original source mesh/material files remain pinned. All 83 source IDs, paths, world matrices and per-object face counts equal the sealed packet and sum to 1,306. The new identity records a static native architectural part and no guide-facility assignment or motion.

## Facade evidence

The installed registry and actual scene have six scoped appearance owners, four text displays, five door label corrections and four photographic child planes. Every photographed plane has 125 vertices and 192 faces; actual triangle winding agrees with its front normals, all UVs remain within the original image, and the four corner UVs reproduce sealed pixel controls. Imported JPG bytes equal original source artifacts. Reference aspect ratios are preserved; photo children have identity position/rotation, the recorded scale, and no collider.

Homography independently reproduces all five control quads exactly. Enabled photos 8, 9 and 27 are affine crops; unit 18's 24 by 4 piecewise mesh has maximum triangle-centroid approximation error of 0.002351 pixels. Unit 21 remains a disabled reference photograph. Y is correctly flipped from image coordinates to texture UVs. Original unit 8 and 18 four-vertex lightboxes remain untouched; the latter's 6.88 m board centers a shorter aspect-preserved photographed sign instead of stretching it across the fascia.

Unit 21's replacement retains every baseline vertex/channel and exactly moves original faces 0–11 to black material slot 1. Original faces 12–23 stay in slot 0 with the original material. The two sets share no vertex indices; unit 21 is at z 18.4–18.7 and unit 22 at z 1.85–2.15 in their original local frame. This is an exact material partition, not a shape edit.

All original SourcePartIdentity components, including the six native-stair components, remain byte-unchanged after facade installation. Each of the five corrected StationDoor components differs only in Label; element IDs and operation fields are unchanged.

## Source/rebuild review and corrected installation error

Actual MajibangBuilder contains observed-facade source resolution after architectural/guide resolution, appearance replay, text replay and door-display replay. It includes registered sources in receipts, protects them from stale deletion and runs complete source preflight for main and box roots. Static registry replay checks original source channels/layout/bounds, scoped owner frames and immutable assets before restoring photo children. Root owns execution of these rebuild preflights.

The initial installer used JObject.FromObject(Vector3), which can recursively serialize normalized properties. Root replaced it with explicit x/y/z fields. Root also captured a stable scene path to prevent rollback from masking the primary exception after the old Scene handle becomes invalid. The saved installation receipt confirms the successful local six/four/five/four result at the scene SHA above.

Root still owns rendered front captures, actual source replay execution and any physical walking validation. This review stops at the completed bounded serialized-source and geometry evidence.
