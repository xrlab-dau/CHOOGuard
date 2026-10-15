# 1F retained-source rebuild lineage

Read-only review. Canonical source files, Unity assets and registries were not edited; Unity was not called. Reproduce with `python3 workers/twin/evidence/floor1-rebuild-lineage-20261009/prove_lineage.py`. The script writes `lineage-proof.json` in this folder only.

The stored lineage is directly supported. Fresh generation of the four missing batches remains unsupported because no such generated channel export exists and this review does not run Unity. The prior preflight's exact23-source expectation (19 accepted,4 missing) must remain enforced.

| Retained builder source | Source vertices / triangles | Current guide lineage | Stored verification |
| --- | --- | --- | --- |
| Kit/shops_1F/Kit_CeilingGrid.asset | 7920 /3960 | source ->1F-008 ->unit16/1F-005 | Complete digests, exact indices/positions/normal/UV/tangent pass |
| Kit/shops_1F/Kit_Light_조명기구_downlight.asset | 5940 /1980 | source ->1F-022 ->unit16/1F-014 | Complete digests, exact indices/positions/normal/UV/tangent pass |
| Kit/shops_1F/Kit_ShopCeiling_Kit_GridWhite.asset | 171 /57 | source ->1F-014 ->unit16/1F-007 | Complete digests, exact indices/positions/normal/UV/tangent pass |
| Kit/shops_1F/Kit_Colliders.asset | 768 /384 | raw ->ArchitectureCorrections/1F_KitFixturesOnly (768 /132) ->1F-046 ->unit16/1F-027 | Exact252-wall/132-fixture partition; all768 vertex stream bytes preserved; both guide patches exact |

Paths in the table are under `Assets/ChooGuard/Art/StationInterior/`; numbered guide assets are under `FloorGuideLayout20261009/`.

The three envelope sources were retained and their renderers withheld by `InteriorEnvelopeRepair.Apply`. Its production receipt is `.planning/2026-10-08-interior-runtime-completion/architecture-envelope-repair.json`. The source collision partition is `.planning/2026-10-08-interior-runtime-completion/architecture-kitshop-overlap.json`. The entire252+132 ordinal set is unique, disjoint and complete; all preserved indices occur in exact original order. The raw binary export and recomposed YAML geometry hashes agree. The current canonical source/baseline complete digests and final unit16 replacement digests all agree with the registry, and both pinned packet stages preserve indices, normals,UV0 and tangents byte for byte.

All16 canonical and legacy shop elements have identical polygon/y/y1 and source envelope parameters (front,ceiling,downlightPitch,collider,openEdges). Canonical `buildEnvelope:false` is the generation omission. It suppresses the ceiling/downlight branch and wall collision generation. Current canonical layouts include11 collision shelf/gondola/bakeryShelf/cooler boxes; KColBox emits24 vertices /12 triangles each. Their in-memory batch is therefore expected to contain264 vertices /132 triangles. This is a code-derived count, not a fresh Unity observation. FixturesOnly retained all768 original vertices, including504 now-unreferenced wall vertices. A direct source alias cannot pass its ordered topology/channel contract.

Concrete source/path/scope fix

1. Keep canonical observed layouts and their shared-envelope ownership validation. Add an explicit, bounded retained-source replay policy for the1F shop root, shared by KitBuild and KitVerifyCorrections. When registered retained envelope sources are required, emit their source ceiling/downlight geometry and source wall collisions using the same16 canonical source polygons and original params. Preserve `buildEnvelope:false` as the runtime enclosure ownership decision. Do not replay the whole old spec: its entries lack placementEvidence, so current KShopLayout skips them, and historical inferred fixture layouts differ.
2. Register the exact raw collider source `Kit/shops_1F/Kit_Colliders.asset` to its FixturesOnly partition as an architectural correction for the exact owner `실내 트윈 마감/점포 내부 1F/Kit_Colliders`. Pin the raw original and replacement complete channels, original frame, source ordinal partition and retained enclosure-owner dependencies. Change the guide row's source_asset to the raw builder path, retaining FixturesOnly as baseline_asset and the existing `patch_baseline_asset` /unit16 pinned packet chain. This makes source discovery and Batch.Save resolve raw384 triangles ->FixturesOnly132 ->guide132, and keeps23 unique expected source paths. The architectural publisher currently targets MeshFilters; a collider-only registration needs its own exact owner validation, not an unchecked generic alias.
3. Ensure production Batch.Save installs the reviewed correction for collision, and recreates the three retained envelope batches with renderers withheld. It currently creates enabled renderers and may create child shadow casters via CastEnclosureShadow; withhold those child casters as well. Preserve retained source assets, replacement meshes and receipt provenance.
4. Validate actual generated channels for all23 expected sources before asset writes. For the collider, require a fresh raw batch at768 vertices /384 triangles, its complete digest/indices equal the pinned raw source, the exact252 removal and132 retained order, then both downstream guide checks. For ceiling/grid/downlight, compare the fresh ordered indices and complete attributes to the original retained sources before guide resolution. Continue failing missing or unexpected sources and changed attributes.

Pinned collider digests

- Raw source CompleteChannelDigest: `947db83c5ec6ecfd77cef81c4c08cea505430ecdd270f5b9a6d70e0290f582fd`
- Raw source ChannelDigest: `be3d16f07014c300346b296e3cb02b4e3d488e6f4a5d2e336e6be34f1200017d`
- FixturesOnly CompleteChannelDigest: `02b5c96a1ada6a6dcdd0f46305aa0918d587d921b356f2c529526de1f59b4a3a`
- FixturesOnly ChannelDigest: `6777a2a3f98a0f228892029423be5cb9ba824cd8b110c53f90db51e9c6dfb6ec`

The YAML parser rejects non-Float32 or multi-stream formats instead of silently guessing. It reconstructs position/normal/index binary digest, UV0/tangent ChannelDigest, and topology/color/UV0..7 CompleteChannelDigest. It verifies each digest against canonical authority. This proves existing stored assets and dependencies only; a new source rebuild must produce its own generation receipt.
