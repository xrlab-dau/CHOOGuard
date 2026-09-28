# KitExtend progress (respawn KitExtend3 -> KitExtend5, 2026-09-25)

## Done before the cut (resume/KitExtend.md)
- Slab (KitSlab) + answer kit-requests/Main-slab.done.json; ShopInteriors-1 params (openEdges/back/layout) + ShopInteriors-1.done.json.
- Room / ToiletRoom / Window / Canopy code (MajibangBuilder.cs ~4663-5430), dispatcher cases, frosted glass, `concrete`/`tile` keywords.

## KitExtend3 (cut 14:02)
- [x] Dry-run KitBuild compiles; make_showcase_full.py -> showcase-full.json (14 elements); first renders kit-full-*.png (13:56).
- [x] Ceiling fill ring inset to wall centreline (KRoomCeiling `fill` = KInset(poly, t/2)) -> sun light-leak streaks gone (verified 18:21 render kit-full-staff.png).
- [x] Sent conventions to Public1F3 + Main (see below).

## KitExtend5 (started 18:15)
- [x] Dry-run compiles (18:18). Fresh renders 18:21: leaks fixed; remaining: serrated sun band at the inner-wall top (preview only, no roof), male WC ceilingPan noisy/dark, only 1 real light in 12x8 WC (KGrid floor()).
- [x] Re-render 18:24/18:27: staff serrated band gone (ShadowCap), 4 lights; ToiletRoom default ceiling -> plaster (ceilingPan read as diamond plate); light `drop` .6 (was .3 hot spots); Canopy realPitch lights use cover grid; removed unused `dy`.
- [x] KitValidate full/{room,toilet,window-canopy}-test.json 0 errors (18:25).
- [x] kit-spec.md rows Slab/Room/ToiletRoom/Window/Canopy + ShopInterior openEdges/back/layout.
- [x] Conventions sent to Main, Public1F5, Services23F5 (18:3x). Main asks answered: before/after KitValidate on copies of shops-1f.json (112,882 tris) + infill.json (5,652) identical (kitextend-work/MajibangBuilder_before.cs = reverted copy; *-before/after.validate.json); ShadowCap path `<root>/Kit_ShadowCap` / `Box_Kit_ShadowCap`.
- [x] Services23F5 request: check_spec.py Room/ToiletRoom `slab` > 0 -> no live-floor check + 'room slab' volume; services-2f.v2.json 0 errors.
- [x] make_showcase_full.py toilets view reframed (eye 397.5,387 between storage/staff looking at female/accessible/unisex entries).
- [x] Final render 18:4x (all 11 kit-full-*.png, report kit-full-report.json), KitValidate room/toilet/window-canopy 0 errors, slab 0, shop 1 (intentional 'sofa' reject); triangle-budget rows + render list in kit-spec.md; dry-runs KitBuild/KitValidate/Main/FirstFloorInterior compile (--project-path).
- Next (if respawned): serve kit-requests/ + messages only; nothing pending.
- ALWAYS pass `--project-path /Users/um-yunsang/CHOOGuard` to unity commands (second Editor on 7801).
- Note: wall_toilet_tile_300x600 (shared Lane mat, smoothness .8, base .55) reads dark on walls facing the preview camera: env reflection in the preview; not changed (shared material).
- (done) final dry-runs.
- Note: KitPreview prefix arg WITHOUT trailing '-' (kit writes `<prefix>-<shot>.png`).

## Conventions (answers)
- Room/ToiletRoom walls: built INWARD from polygon edges (outer face on the edge, inner face wallThickness inward, mitred) = check_spec model.
- Floor overlay over the whole outline at y+.004 (no collider; FloorFinish uses .003); `floor: "none"` to keep the existing floor only;
  `slab` > 0 adds a structural Slab (top at y, collider) for rooms over voids.
- ToiletRoom entry = OPEN gap (no leaf), default width 1.2, height 2.3; put a ServiceDoor/DoorSet element in it if a leaf is evidenced.
- Walls y..wallTop (default y1); ceiling underside at y1 on the inner ring (plate extends to the wall centreline); invisible shadow cap at y1+.3.
