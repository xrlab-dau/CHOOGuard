# Shop and slab rebuild preflight extension

Only `AgentScripts/MajibangBuilder.cs` was changed. No Unity command, production scene edit, Assets write, registry publication, full Main build or KitBuild occurred in this lane. Root owns actual compilation and after-publication verification.

## Generator contract

- `Main` still calls `VerifyShopCorrectionsForPlan` immediately after reading the plan, before shared material writes or root destruction.
- All canonical source assets under the Main root must be regenerated exactly once. Unsupported/missing/duplicate sources remain fatal.
- Registered third-floor slab sources are rebuilt by the actual `PolySlab` helper using current `thirdFloor.slabs`, third-floor top/depth, inherited frame and existing `Floor_Granite` material. Repeated same-name contributors accumulate in plan order.
- `ShopGeometry` runs only for units that contribute to a registered source batch. Shared shop ceilings require every matching contributor in actual Main order: third-floor units, then second-floor units. Individual wall-only validation regenerates the matching tenant only.
- Current `Box_Majibang_ShopCeiling` has five plan contributors: 3F 부산옥, 에그박스, 비빔가; then 2F 편의점 StoryWay and 부산별빛샌드. Omitting the latter two is not an exact source reconstruction.
- The preflight loads existing material assets only and uses HideAndDontSave transforms/meshes. Registry ResolveForRebuild validates source channel digest, owner, frozen world frame, saved replacement and companions.
- Existing root/frame/box transforms, box mode/east coordinate, door width, storefront flag, folder/prefix, batches, and all touched material references including stone restore in finally. No pure generator used here touches receipts/models/doors/slabOpeningReport.
- Runtime report schema v2 adds generator provenance to the existing shop-rebuild-source-channel-comparison.json. Empty registration is explicitly PASSED 0; it does not establish correctness for unpublished patches.

## Captured authority snapshot

- Plan SHA256: `25de3f2d6fe9270ee75d10b1cebe032935c1864e06b8a7a395e7acc1a713138e`.
- Registry SHA256 when inspected: `51a82236af4fa1e23d9f02513ba32c1a8da5a4ef44a7b09a8fa71d19c3c54669`.
- Main-root registered sources when inspected: 1.
  - `Assets/ChooGuard/Art/StationInterior/Majibang/Box_Shop_부산옥_Walls.asset`.

## Required root verification

Compile the ephemeral MajibangBuilder script, then call `MajibangBuilder.VerifyShopCorrections` with empty arguments after wall/ceiling/floor canonical entries and their saved companions are published. Actual runtime digest/frame acceptance, not this static note, decides whether the source was reproduced. Existing wall-only seventh registry entry should remain one accepted comparison before additional entries.

This preflight preserves documented source corrections on an unchanged source rebuild. It does not prove full Busan Station geometry or field accuracy.
