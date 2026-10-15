# shops-1f preflight source-count review

Exact packet-owner mapping gives **23 registered sources** for `실내 트윈 마감/점포 내부 1F`. The current probe compared **19**, accepted all 19 with identical indices and zero reported position/normal/UV/tangent differences, and reported **zero extra sources**. Four registered sources were absent.

| Missing source | Supported cause |
|---|---|
| `Kit/shops_1F/Kit_CeilingGrid.asset` | Every canonical ShopInterior sets `buildEnvelope:false`; ceiling/downlight generation is inside that branch. |
| `Kit/shops_1F/Kit_Light_조명기구_downlight.asset` | Same generation-policy omission. |
| `Kit/shops_1F/Kit_ShopCeiling_Kit_GridWhite.asset` | Same generation-policy omission. |
| `ArchitectureCorrections/1F_KitFixturesOnly.asset` | This installed baseline is registered as its own builder source. The actual builder emits `Kit/shops_1F/Kit_Colliders.asset` instead. |

The paths above are relative to `Assets/ChooGuard/Art/StationInterior/`. Both canonical and legacy receipts contain the actual `Kit_Colliders` builder path; the structural-corrections registry has no collider lineage entry that connects it to `1F_KitFixturesOnly`. That fourth case needs an explicit, geometry-validated source mapping.

`Batch.Save` and `KitVerifyCorrections` use the same `folder + batch.Name.Replace(' ', '_') + ".asset"` rule. `B` incorporates `batchPrefix` into that same name, and both shop-root entrypoints reset the prefix to empty. The current SHOP element IDs do not supply an alternate batch prefix. A general SHOP-prefix or folder lookup change would not resolve the three suppressed contributors.

The canonical spec's 16 `buildEnvelope:false` flags differ from the legacy source spec's default `true`. Historical receipts list the ceiling/light paths, but they do not establish that the current canonical probe generated those batches. Restoring envelopes in the canonical spec would conflict with its existing no-duplicate-envelope policy.

Root must validate fresh `Kit_Colliders` geometry against the installed FixturesOnly baseline before rebasing that source, and choose a faithful generation route for the three retained ceiling/light contributors. Preserve the exact 23-source expectation. This review does not claim that a legacy replay already passed.

`source-count-proof.json` records every expected source and exact packet-owner path. `manifest.json` seals its input snapshots, code hash and packet evidence. No Unity calls or Assets edits occurred. The bounded review is complete and stopped.
