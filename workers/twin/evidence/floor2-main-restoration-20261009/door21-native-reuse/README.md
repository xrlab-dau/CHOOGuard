# Unit21 native doorway reuse — accepted bounded proposal

The original four-bay Unit21 clearance claim omitted the preserved tilted native jamb and an overlapping native automatic door. This correction supersedes that claim. It does not relabel the original 215-check packet as a complete physical clearance test.

The accepted packet reuses the intact native automatic operator `native-auto-9783ce238277b47411a9f63a`. Generated tenant operator `2F-main-front-15-호두나니&부산오뎅` is archived by setting its exact GameObject2089695507 inactive; no component/Element/leaf/local field is deleted or renamed. Native public Use0, source geometry, root, operating leaves and all serialized StationDoor fields stay unchanged.

| Changed source category | Exact change |
| --- | --- |
| Current FGL Glass | Remove Unit21 lower panes404–407 and416–419:8faces |
| Current FGL Frame | Remove1680–1727 and1740–1775:84faces |
| Current FGL Trim | Remove660–695:36faces, including redundant operator header |
| Current FGL collision batch | Remove176–183:8faces |
| Current FGL Shop21 walls | Move12 exact lower-edge position indices fromV−43.19999695 toV−43.54999924 |
| Current shared FGL shop ceiling | Move2 lower-edge position indices by the same declared boundary change |

No fixture/ring/light/door transform stretches or moves occur. Source channels, retained face sequence/winding, materials, every unselected vertex and Units20/22/23/1F remain outside the edit. The full current saved owner set includes the shared ceiling shadow caster; no renderer or collider user is omitted. Upper21 transom glass408–415 and420–423, top header frame1728–1739, fascia/sign, lighting and other walls remain intact. There is no procedural front-closing Shop21 wall: its builder generates three returns/back walls only.

The native controller declares nominal width2.7375 and height2.4; these are source model values, not field measurements. Its current posed endpoints are nativeV−43.425564394 and−40.694995414, with a4.078-degree rotation relative to the procedural frontage. The lower inferred bay edge changes by0.35model units toV−43.55, leaving0.124435606model units beyond the native lower endpoint. This preserves guide21-below20 order; the guide does not establish a metric aperture centre or bay dimension.

Current original enabled MainShell collision-floor triangles cover the enlarged Unit21 rectangle and added1.2075model-unit² strip within the existing2e−5 coordinate tolerance. No new floor triangles are appended. The exact current `native-door-collision-background-2809044.asset` has zero vertical section crossings inside the added strip atY8.25; this is a bounded source-face test, not certification of every collider in the live world.

Independent source/geometry review passed78/78 against `.planning/2026-10-09-floor2-guide-restoration/before-native-front-reuse.unity`, whose hash exactly equals packet source epoch1e15cae6edceb65da229497a0dffe67af05b3b9d35066739a3285c36dcfc34e4. Exact coordinate-alias checks found zero torn welds. All54 sampled normal rays through the full nominal native aperture are clear in the proposed generated static batches, including the corrected return. Initial staging review also passed29/29. Nominal aperture width is distinct from runtime capsule clearance, which this lane does not claim.

Root integrated packet146e6a476775b01880e4cbac9323da6a9fdfdec8faa6732717678d13d13c9b13 and reported saved scene3719ef2fbc1a87a51270bbae4ae98b196c8d22b400c1675798f09bf9ddc3425d. The first live source recheck correctly rejected the already-applied scene against the old source epoch; that result is preserved in `live-source-guard-rejection-after-root-apply.json`. It is not a geometry failure or a reason to weaken source guards. The accepted source check uses the byte-matched pre-apply backup.

Root owns actual saved-asset verification, source-rebuild replay, final registry preservation, native-door Play motion and capsule traversal before any clearance/Sealed claim. An independent root reviewer owns the actual saved6assets check. This lane authored no Unity/Assets/canonical writes and stops here.
