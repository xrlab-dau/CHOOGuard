# Native counterleaf transaction review

Follow-up reviewed UTC: 2026-10-08T19:32:31.628178+00:00

**Current source verdict: CLEAR within the six-fragment / 24-exact-original-face scope.** The earlier three staging P1 findings and the later caller P1/P2 findings are corrected in the current reviewed source. No additional concrete P1/P2 was found in the YAML/identity helper review. Root compilation and actual Unity installation/save/recovery validation remain separate requirements.

## Corrected transaction findings

- **Earlier P1, save-boundary recovery without durable old-owner evidence — corrected.** Apply now makes a durable original scene copy at the fixed Baseline path and verifies its exact original SHA before staging (caller lines 144-151). The pending journal records this path, the original SHA, and distinct old mesh/material/all-property-texture/physics-material asset and meta hashes (lines 167-173). FinalizeInstalled unconditionally calls CheckSavedBaseline at line 200, including when `saved_scene_sha256` is absent. The helper requires the baseline path/hash; parses both original and saved scene block IDs; demands precisely the 36 new IDs belonging to six GameObjects with five components each; rejects missing or changed preexisting serialized objects; and permits only exactly one occurrence of each new fragment Transform child reference within its old parent's m_Children section (lines 93-128). All frozen old dependency files are rehashed before journal promotion. Existing owner names, mesh/collider/material references, pose, component data and SourceIds therefore remain durably checked as old serialized block contents. VerifyRecovery now removes `saved_scene_sha256` when creating its false-marker pending fixture (line 225), exercising the previously untested save edge.
- **Earlier P2, pending deletion before rollback restoration — corrected.** The rollback save return is now required, then the exact original scene hash is required, and only then is pending deleted (lines 182-185). Failure keeps the pending evidence and durable original scene file instead of silently deleting the journal before the restoration assertion. No production rollback failure was induced by this review.

## YAML/identity helper verification

Fresh local inspection found **26,105 original serialized blocks**. Both exact target native-parent Transforms, `124133893` and `1694344430`, already have nonempty multiline m_Children sections, with five existing children each. The helper's new-child-line removal therefore reconstructs their actual original section text; the empty-list `m_Children: []` formatting case does not apply to these fixed targets.

A bounded in-memory Python reproduction of the same block-ID and child-section checks used the actual frozen scene YAML. It accepted exactly 36 new blocks plus three appended Transform children under each of the two target parents. It rejected all seven negative fixtures: unrelated old serialized field change; old object removal; extra new serialized object; same-count replacement of an old object ID; duplicate new child reference; original parent pose change; and original parent child-membership change. These fixtures created no files and did not touch Unity or production data. They validate the comparison logic and actual old-parent text shape; they are not C# or Unity execution tests.

Source review found no concrete type/API compilation error in the new Regex, dictionary, LINQ or GlobalObjectId code. Each new serialized ID must be nonzero, absent in the original block set and unique; a different new component count or an unexpected scene block fails the exact-set check. The two parent sections are the only old block exception and become byte-equal to their originals after the six exact known child references are removed. Root's Unity compiler is authoritative.

## Corrected staging guards

- Packet seal: exact SHA `df1e591f2e6918235ec2f0e8f51743f7d016b24252474944fe503fe25faf08e2` is pinned by setup line 22 and checked before Stage and activation (lines 268/341), plus Apply/Finalize (caller lines 134/193). Exactly six fixed original part IDs and 24 fixed face ordinals remain additionally approved.
- Dirty source proof: CleanSource rejects dirty source Mesh, each ordered Material, and every nonnull material-property texture. Activation reruns Prepare. Caller source/fragment proofs reject dirty dependencies, freeze mesh/material/texture and collision object identities and disk hashes, preserve complete channel digests and reject transient renderer property blocks.
- Actual staged world geometry: setup validation reads each staged mesh's actual selected indices and TransformPoint vertices after SetLocal, rechecks original-corner error and native winding, then runs NoOverlap on the resulting actual 24 triangles (lines 353-379). The guard checks loaded same-scene MeshFilter and MeshCollider surfaces, including inactive owners and both normal signs, using 0.0004 near-plane tolerance and 1e-10 positive-area threshold. The earlier planned-frame-only absence gap is corrected.

## Preservation and runtime evidence boundary

The caller also retains pre-save loaded-object dictionaries for every original MeshFilter mesh, MeshCollider mesh, Transform world frame and SourcePartIdentity SourceId, validates these before SaveScene, and saves only the six new mesh assets. Source/fragment appearance, collider flags, complete channels and owner frames are frozen before saving and checked during finalization. The setup mutates only new mesh assets, six new owners and dedicated folders; it supplies no operating pair, hinge, controller or invented closed pose and never cuts background geometry.

The earlier independent exact-face mapping and frozen planned-face absence proofs still match the unchanged packet bytes. This lane did not run Unity compile, Stage/Activate, rollback, cold scene reopen, saved collision checks or VerifyRecovery. Root owns those executions and receipts. Source preservation and absence within this conservative packet establish neither full counterleaf restoration nor physical site accuracy or whole-station completion.

## Exact reviewed bytes

- Packet: `df1e591f2e6918235ec2f0e8f51743f7d016b24252474944fe503fe25faf08e2`
- Staging setup: `976ad4f34ecfab8a1a82a2f6e21eedfcead95abeb887a893b16dc53b0a472a0a`
- Root caller: `d9f84a7f4de0c659928eb9e1dc9ecea462c03dfd0679f7b689515e02c1c1cea3`

Fresh packet evidence hashes (the production scene was saved concurrently by root after the original 26,105-block fixture snapshot; its frozen-scene mismatch records that later scene change, not a change to the reviewed packet or original baseline):

- `frozen_scene`: MISMATCH `7d14af92cddc20f6c1de0310a64f9d9261b5cfefff65b60001c6ad1b8f6d608a`
- `scene_export`: MATCH `ad95c6163e056e3743c244ace07ee6b9a6315866d5e3632dad3b171ad49a7153`
- `source_geometry`: MATCH `1502ba2bc03fbc0e6dc07530fd518e68df30dacba6a288d590044f624114ec32`
- `absence_audit`: MATCH `47985cc716001e9bde4cc9060b5121c8217d2681b53e773db9ac35f51d4d64ac`
- `offline_qa`: MATCH `f08ddbd4d0cfca5ef638ea4648b0b851858b64128080aed5ca0751d2c587bdd2`

Durable original Baseline file observed after the concurrent save: `5febc30153d38e48a263399aa9658163c3ab1f75d4c0185f256d22d18b9f7654`. Root caller also gained its diagnostic `InspectPending` method during review; the refreshed source tail was read and adds no Apply/Finalize/rollback mutation path.

## Review work record

Read the existing bounded review plan and independent summaries, setup, packet, caller, CompleteChannelDigest implementation and exact two old-parent YAML blocks. Reviewed the follow-up durable-baseline correction and ran the seven in-memory negative fixtures above. Initial packet summary lookup used the incorrect `parts` key and was immediately corrected to `fragments`; it caused no mutation. Wrote only this report. No Unity, browser, external/cloud action, nested delegation, code/Assets mutation, scene export/recount or other evidence edit occurred. No relevant memory evidence was reused.
