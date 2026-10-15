# Source automatic button runtime preflight

Read-only review of current SourcePartAutomaticButton, StationDoor.RequestButtonOpen/lifecycle, current SourcePartReconciler, and committed tests. No Unity execution, Assets/code edits, nested delegation, or inspection of unfinished staging implementation. Existing original MeshCollider transfer is implemented; this review does not assert any phantom static-door collider omission.

## Current behavior supported by source

- RequestButtonOpen rejects non-sliding and Sealed doors. Locked doors reject requests except public fire-alarm override, consistent with UpdateSliding's alarm rule; tenant Locked remains blocked during fire. Existing EditMode test covers that distinction.
- Five current exported original buttons share local bounds approximately X=[0,0.0129903806], Y=[-0.03,0.03], Z=[-0.125,0.125]. With PressDirectionLocal=left, current front selection picks 12/32 vertices on the protruding maximum-X side and leaves the mounting X≈0 plane fixed. Direction transforms through Plate, so translating/rotating the leaf parent does not change which local vertices are pressed. Travel is normalized in world space, which accounts for the button with 0.8-scale transform.
- Serialized editor installation assigns fields before PlayMode Awake, so that path initializes normally. Current source runtime checks do not yet cover original sliding leaves/buttons, exact mesh-channel release, or component-removal cleanup; existing scene test exercises a hinged source leaf.

## Actionable findings and requested verification

1. **P1 preservation: original mesh identity and full release channels are not restored.** Awake swaps Plate.sharedMesh to a runtime clone but never stores the original Mesh reference (35–44). OnDestroy destroys the clone without restoring Plate.sharedMesh (54); removing only the button component leaves a surviving MeshFilter pointing to a destroyed mesh. Update recalculates normals even while idle or fully released (45–52), and neither original normals nor tangents are cached/restored. Vertices return to original positions, but source shading channels are not contractually restored. Save source mesh identity and all changed channels; restore them on release/disable and restore the original Mesh pointer before destroying the clone. Avoid idle mesh writes. Test the actual imported plate at rest, pressed, released, and after component removal; compare source vertices/normals/tangents exactly and ensure the shared imported mesh is never mutated.

2. **P2 input contract: invalid direction/travel can accept a button request without valid depression.** PressDirectionLocal and Travel are unvalidated. Zero direction gives a degenerate axis/front mask yet Door.RequestButtonOpen and AcceptedPresses can succeed; nonfinite or negative travel can deform invalidly/outward. A direction parallel to the face selects lateral geometry rather than a front plane. Require finite nonzero direction, finite positive travel bounded by projected thickness at actual scale, finite readable mesh channels, and a nonempty front selection with fixed back plane. Existing left direction on the measured five plates is valid; do not reject correct source orientation blindly. Test zero/nonfinite directions, negative/oversize travel, and source-front inward displacement versus fixed mounting vertices.

3. **P2 lifecycle integration: active runtime AddComponent followed by field assignment is not initialized.** Button Awake runs with Plate==null and exits, and no later Configure/ensure-initialized path exists. Later assignment of Door/Plate/direction can open the door but never clones/deforms the plate. StationDoor similarly caches Leaves closed poses/body setup/Centre/mode in Awake; active runtime AddComponent followed by Leaves/Centre configuration misses those caches. Serialized editor installation is unaffected. Expose explicit runtime Configure/initialize sequencing or construct inactive fixtures and activate only after all serialized fields are set. Test both the intended installation sequence and active late configuration; assert actual source vertex world positions move with the configured sliding leaf, and a button attached to that leaf starts in the original pose and keeps an inward local press while the target leaf moves.

4. **Nonblocking feedback consistency:** SourcePartAutomaticButton.CanInteract/Holdable do not reject Locked mode, so the hold affordance can be offered and the plate depressed before RequestButtonOpen rejects it. There is no locked-door opening bypass. Consider sharing the request-availability predicate for consistent prompt/hold gating. Test denied requests do not increment AcceptedPresses or move the door, including Sealed, Swing, Elevator, Locked tenant under fire, and Locked public without fire; public-fire override should remain accepted.

Required integration test should demonstrate the real new source sliding leaves move under the button request, not only AcceptedPresses/text changes. Check leaf kinematic body/collider travel, exact initial source world geometry, attached-plate depression in moving coordinates, release, mode rejection, and teardown source restoration. Root owns Unity execution. No claim of whole station completion or field accuracy follows from this review.

## Source snapshot hashes

- `Assets/ChooGuard/App/Fps/Twin/SourcePartAutomaticButton.cs`: `e16a818b57a64d8064c6b78e55e68728a4460dbbca1f95560c5edd16f57b82d6`
- `Assets/ChooGuard/App/Fps/Facilities/StationDoor.cs`: `a24135b408f1aa8399cba2109754878b91c1cab6e4afdf7051eb93439ad40f01`
- `Assets/ChooGuard/Editor/Twin/SourcePartReconciler.cs`: `7c51414e6a358166577173bb08320076c723d09dde5dc2d2c3f3277be5f6f33e`
- `Assets/ChooGuard/Tests/EditMode/SourceReconciliationTests.cs`: `73a428d33d6eb7160867ed04c4fa9061cab8f8bcb3b74dec5fa4715b2b8a50b8`
- `Assets/ChooGuard/Tests/PlayMode/SourcePartSceneTests.cs`: `d94f432b5a4fcf902d7c1276d1a5b0b0eca46db6c9dcec72e2cddab66a9d36d0`


## Lifecycle patch and completed native staging recheck

The current button patch addresses earlier source restoration/late-configuration issues: lazy EnsurePlate initializes a configured plate; original source Mesh is restored before clone destruction on disable/destroy; original normals/tangents are cached and restored on full release; idle frames avoid recalculation; finite/nonzero press direction and finite positive travel are enforced; travel must stay below 80% of actual world-space projected thickness. Existing .002m travel is below source thresholds at both scales. Direction is still part of the source-specific binding contract, so actual -X on these five Unity meshes must be supplied. Serialized inactive operator construction ensures StationDoor Awake sees complete leaf configuration when activated in PlayMode; no generic StationDoor late-configuration API was added or is required by this staging path.

Completed staging was then authorized for inspection. Packet contains nine unique leaf parts/five bindings/93 selected triangles (8×10 plus fifth13), finite positions and slides, and valid indices. Slides are along the registered facade/opening direction; transverse normal component is at most 3.3e-8m. Paired leaf travel is 1.36874999m; fifth leaf travel is 1.24999991m. Fifth packet's complete 29-vertex assembly array includes static vertices, but only11 vertices are referenced by selected13 triangles; the selected moving geometry has height2.60000086m. No claim that the complete static fifth frame would move is warranted.

### Before-Apply gates

- **Known P1, already assigned to root:** staged binding and packet +X represent the source archive convention, but actual Unity imported plate front is X=+.0129903806 and mounting back X=0. Copied binding must use -X, with the archive-versus-Unity-axis difference recorded. +X would depress the mounting/back plane toward the protruding front. Root has explicitly committed to this correction.
- **P1 transaction boundary, caller must resolve:** Stage performs real production renderer/collider background replacement (137–138) and returns successfully with all replacement leaves still under inactive operators. Its internal rollback only runs while complete=false; returned Result exposes doors/receipt but no original owner mesh references or rollback handle. A conservation/activation/runtime-test failure after Stage returns therefore cannot be rolled back through the returned transaction state. Before Apply, caller must capture the original visual/collider mesh references, original button parents/statuses, generated assets and created roots (or add a returned rollback callback/journal) and retain them through activation and final save. Keeping the installation merely unsaved does not restore the live scene. This is a recovery-contract gap, not a claim that a collider transfer is missing.

### Geometry/material/collider/activation boundaries otherwise supported

Match audits current visual and collision background triangles independently, requires each requested source face have exactly one owner, and rejects two source faces claiming one owner. Source materials/submeshes and all available normals/tangents/colors/UV channels are copied from live native renderers when constructing leaves; original winding/index ordering is retained per copied triangle. MeshCollider backgrounds are cut separately, and each recreated moving leaf receives a matching MeshCollider. Bind preflight checks preserved normals/UV/count, current world positions, exact leaf collider mesh, and retained Original405 identity count. Operators are inactive while fields/leaves/buttons are configured; caller activation and scene save are explicit. Stage does not itself save or mark production_installed=true.

After Bind reparenting, root should verify all nine source leaf and five source plate world vertices remain in the original closed pose before activation/save, because the staged check is before final reparenting. Runtime test must exercise actual motion, source collider travel, plate front/back movement and complete release/disable restoration, then persist a final validated receipt. These are explicit tests/gates, not observed failures in the present source.

Fifth opening Staff is a conservative, unverified simulation policy. It makes PassableByPeople false, which prevents NPC routing/use; UpdateSliding still senses nearby PersonBody even on Staff doors, so the policy does not prove that sensors never auto-open it. Do not claim surveyed use classification.

No reviewer Unity, Assets/code edits, or nested delegation. Completed staging snapshot hashes:

- `Assets/ChooGuard/App/Fps/Twin/SourcePartAutomaticButton.cs`: `5b94ed5a27e414e04c16189033755480775ab85a502d6299d7df906b9da5b351`
- `workers/twin/output/remaining-native-doors-20261009/ReviewedNativeSlidingLeafSetup.cs`: `b09ec6abc15ada0d04fead733945a02adc8a6259d6b3edc9a6dd7794966288fb`
- `workers/twin/output/remaining-native-doors-20261009/ReviewedNativeAutomaticDoorBindings.cs`: `a6b9e6101fb0f4907ebbd8f2bfd46b7443c0c8b84f8fb830e5aac576cf7495be`
- `workers/twin/output/remaining-native-doors-20261009/sliding-leaf-dependencies.json`: `08f50c3330e7a6099778a16bb7c3755695205fb4ca082560cb792c701a58c4db`


## Native install helper transaction follow-up — clear

Read-only final helper check of `AgentScripts/NativeAutomaticDoorInstall.cs` closes the caller-owned transaction-state gate. Before Stage, helper captures every prior scene MeshFilter/MeshCollider mesh reference, all prior transform identities, and five original buttons' parents/local position/rotation/scale/status. It retains those snapshots through post-Bind geometry checks, activation and scene save. Failure before commit restores original buttons first, removes their new controls, restores original mesh references, deletes only newly created subtrees/assets in the previously nonexistent dedicated folder, removes pending receipt and saves restored scene. Post-Bind checks cover all nine selected leaf vertices and all five ordered button vertices before operator activation; actual -X binding is explicitly required.

The commit flag is set only after successful SaveScene. Receipt publication failure after commit retains the installed scene/assets and pending journal instead of deleting committed geometry. This is an explicit committed-but-unpublished state; root owns any needed journal completion/recovery and actual runtime tests. No new blocking correctness gap was found in this helper's requested transaction boundaries. No Unity or product edits were performed by this reviewer.

Helper snapshot SHA256: `bc0c870ba297e8cf6fb5a01438fbff75b188a1c7036b5bd7b06281df37952932`.
