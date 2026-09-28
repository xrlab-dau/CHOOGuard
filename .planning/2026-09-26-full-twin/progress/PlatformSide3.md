# PlatformSide progress (spawns 3-5)

## State
- 2026-09-25 18:20 PlatformSide5 resumed (5th spawn). check_spec default now geometry-after19 (shop interiors built).
- Scratch `.planning/2026-09-26-full-twin/platform-side-work/`: geo.py (tri cache), tris17.npz (whole station after17),
  pmap.py (plan map), probe.py (column heights), at.py (surfaces at a point + owner), wallface.py (section hits along V),
  holes.py (Y7 floor holes at 0.1 step), srcfloor.py (owner-filtered height ascii).

## Key facts
- U+ = north, V+ = east. Box hall U -17..65, V 12..100. Platform Y0, deck 7.0.
- Box SSW wall inner face (source 선로상층부_0, y 8): u = -13.633 - 0.0722 v (V 26..71).
- Well notches (builder box floor cut by majibang-plan wells, nothing at Y7; below: source substructure 5.0-6.1 and platform 0):
  w1 U wall..-13.5, V 27.5-32.5 (builder ServiceWall at U -13 V25-35 east of it); w2 U wall..-15.5 V 44.5-49.5 (+sliver to -16.5 V 43.5-44.5);
  w3 U wall..-16.5 V 67.5-71.5 (+sliver to -18.5 V 71.5-73); builder service room U -16..-10 V 67.5-72.
- North well n1 (fall-7-27): source escalator/stair Layer0_5 rises north (U57 ~1.7-3.5, U62 ~4.5-5.5) to deck 7 at U~66;
  source upper slab opening U 57.7-67 V 53-57 and U 61-68 V 46-51.5. Source full-height wall at U ~57.5 (V 52.8-57, slanted 57.55@53.5 -> 57.33@56.5)
  and U ~61.1 (V 48.5-52.5). Box-side notch = U 54.5..wall, V 49.5-57.5 -> infill (inside box, source floor below closed at U<57.7).
- Decision: infill slabs at y 6.995 (Main infill convention: finish polished, edgeFinish plaster, soffit plaster, depth .3), tier b.

## Done
- v1 18:35: specs/full/make_platform_side.py -> platform-side.json (6 Slabs: notch-ssw-00..03, notch-n1-00, notch-n2-00),
  platform-side-report.json, platform-side-overlay.png; check_spec 0 err, KitValidate 0 err (platform-side.validate.json); Main messaged.
- Exit research: box east wall is continuous source glass at exits 9/10 (render renders/east-in-exit10.png); units along east wall
  (majibang-plan secondFloorUnits): PASCUCCI u16-25, 떡공방 26-33, 카카오 33-40, 크리스피 40-47 (v 85..wall), 국제시장단팥빵/하와이안 u56-62.

- v2 18:41: + SSW slivers to the SE corner (V 26-86), east-facade sliver, 3F SSW wall-foot strip (y12.195, V37-78). 10 Slabs,
  check_spec 0 err, KitValidate 0 err (09:40Z, --project-path /Users/um-yunsang/CHOOGuard REQUIRED on unity commands). Main messaged.
- Source walls (FBX per-face materials, fbx/walls_mat.py -> fbx/ssw-src.npz): no door-leaf material (a280 유리#001) on the box SSW
  or east walls at y 7-10 -> exits 9/10 and the south gate are NOT open in the source; SE-corner diagonal (V 83-93) = 유리#008 glass.
- Paid concourse (source) already detailed (renders-sheet.jpg: EV towers, stair heads, signs, gate glass wall); seen-7-34/37/38/42/44/47
  = source stair/escalator openings and track edges guarded by source balustrades (no action).
- OSM (asset-library/research-public/2026-09-26/PlatformSide/osm/overpass-station.json, ODbL) -> UV (platform-side-work/osmuv.py via
  naver-1f-uv.json): station building layer 2 has an east wing U 3-13, V 100-126 (exit 9 connector) + footway bridge layer 1 from
  (7,122) east to 북항; steps viaduct U 13.5-23 V 113-120; separate south building U -51..-19 V 15-96 (south strip). OSM ~+4-5 V vs model.
- Naver 2F POIs (names via map.naver.com/p/api/place/summary/{id}, naver/unnamed-poi-names.json): 2F+3F elevator at UV (1.3,120.5)
  = in the east wing (exit 9); south strip: 2F stairs (-38.6,89.7), (-36.6,72.7), escalators (-37,74.9), (-35.7,52), 2F 화장실 (-20.9,84.3).

- 19:05 report v2 written (platform-side-report.json: 5 built items + 8 assessed items/requests: paid circulation verified vs Naver
  (19 POIs <=1.6 u), paid seen runs guarded, south gate request (SSW wall V~50-62, floor behind 7.2), exit 9 request (facade U~4-12,
  wing U~1-13 V94.5-121, EV (1.3,120.5)), exit 10 c/request (u~48-55), port deck c). Receipts: asset-library/research-public/2026-09-26/PlatformSide/receipts.json.
  Spec elements unchanged since KitValidate 09:40Z (only basis.method text); check_spec 0 err.

- v3 19:10 (Main request after25): rendered all 21 seen runs tagged PlatformSide (platform-side-work/renders25/, render.py --poses,
  sheets s1-s4.jpg). Fixed: seen-7-5/6/7 (main-building north facade floor groove, strip-main-north-00..05), seen-12-0 end gap
  (strip-ssw-3f-01; strip-ssw-3f now face3 to V 93.3). Rest open by design (glass/exterior/source-guarded), per run in report v3.
  Generator default audit now geometry-after25 (own root excluded -> v1/v2 plates reproduce). 17 Slabs; check_spec 0 err; KitValidate 0 err (10:05Z).

## Next (if respawned)
- After Main decides the SSW / east-facade openings: south gate Gate line + signs; exit 9 wing (Slab/Room/ElevatorFront/DoorSet);
  unassessed: seen-7-8, seen-12-0.
