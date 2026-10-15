# Busanok fixed glazed return bounded review

Initial source snapshot: `review-snapshot.json`. No Unity or production changes were made by this lane.

**Two integrity gaps require correction before publication.**

1. **P1 — canonical rebuild protection omits the fixed collision barrier.** `BusanokGlazedReturnSetup.cs:211` creates a collider-only child; the current `ArchitecturalCorrectionRegistry.PublishSavedCorrection` and `ResolveForRebuild` store/check only visible MeshFilter companions. Disabling, deleting or replacing that barrier can pass Main preflight while the original collider retains only eight faces. Add an exact canonical collider dependency with active/enabled, non-trigger/non-convex state, asset/GUID/digest and frozen absolute frame. A changed barrier negative fixture should fail before Main mutates shared materials or destroys its root.

2. **P1 — metadata recovery does not pin visible companion geometry/materials.** `BusanokReturnInstall.Describe` journals only companion owner paths. `FinalizeInstalled` checks their counts and frames, then publishes current mesh/material contents as canonical. A changed glass vertex/UV/normal/tangent or material can pass with unchanged counts/frame. Pin all four companion meshes/materials and barrier identities before Save; recovery must verify those rows and unchanged frozen packet/source hashes.

3. **P2 — finite companion frames.** Canonical companion comparison uses `Abs(delta) > tolerance`; a NaN delta does not fail. Validate finite actual/expected values in renderer and collider dependency matrices.

The independent geometry reproduction passed: the packet matches actual source binary positions/indices and original asset hashes; only source wall triangles8..11 at X=36.5 are selected; the other eight faces and source channels are cloned intact. The same four faces form the fixed collision barrier. Visible geometry contains344 nondegenerate triangles (56 glass,192 frame,84 kick,12 fascia), all finite. Existing frontal sources and other walls are unchanged. The source profile materials are reused directly; no material fallback or additional original-source identity is introduced.

Main now regenerates pure current-plan shop geometry before material writes/root deletion and enforces complete registered source coverage. The separate companion root lies outside MainRoot with the source frame. The installer retains prior shared meshes/colliders and transforms through activation and successful Save; precommit cleanup is dedicated to the newly created root/assets, and metadata failure after Save leaves the pending journal.

Runtime verification remains root-owned: compile the staged setup/wrapper, prove actual Main regeneration returns the saved wall clone, reject changed source/frame/missing expected owner, reject missing/disabled/changed barrier, reject companion channel/material tampering during journal recovery, and confirm fixed collision continues blocking the return while native front/other walls remain unchanged. No physical site accuracy or whole-station completion claim follows from this correction.

## Recheck of the three updated integration files

The original P1 findings are fixed: the registry now persists and checks the fixed collider dependency; the pending journal pins all four complete companion meshes/materials and the barrier before Save; recovery validates exact proofs before publication. The finite companion/collider frame guard is also fixed. Complete digest now covers all eight UV channels/dimensions, colors/format and submesh topology/indices. Transaction rollback/commit ordering remains intact.

Two follow-ups were reported to root: (1) inner collider-loop `var matches` shadows the enclosing `matches` local, a C# compile error; (2) full-source digest is unconditionally added to legacy four-argument publication, so republishing the F931 canopy source causes the separately certified E328 rebuild variant to fail ordered complete-digest equality. Full-source enforcement should be opt-in for new Busanok publication, or require a separately certified complete variant. Preserve the existing pinned proof and caller behavior.

Exact follow-up snapshot is `recheck-snapshot.json`. No Unity or production mutation was made.

## Final bounded recheck — clear

All reported findings are now resolved in the three reviewed files. The collider local was renamed `colliderMatches`, and ordered complete source enforcement is opt-in when collision companions are published, preserving the legacy four-argument canopy variant contract. The exact saved companion/material/barrier journal proofs and canonical fixed collision checks remain intact. Main still regenerates current-plan shop geometry before mutation, and rollback/save/commit ordering remains scoped and recoverable.

No remaining blocking correctness finding was identified in these updated paths. Exact current hashes and resolved finding IDs are in `final-recheck-snapshot.json`; historical findings are retained above. This is a source review, with Unity compilation, installed Main regeneration, metadata failure recovery and negative fixtures owned by root.
