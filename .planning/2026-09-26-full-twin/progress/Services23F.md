# Services23F progress (lane Services23F5, spawned 18:2x 2026-09-25; spawns 3/4 were killed by interruptions)

## Inherited (Services23F-2)
- Naver indoor harvest: asset-library/research-public/2026-09-26/Services23F/naver-indoor-harvest/ (picker POIs, summaries, raster tiles r/ r21/, overlays/)
- Code: .planning/2026-09-24-interior-twin/specs/full/services23f/ (nvuv.py UV transform, resample.py, icp.py, ov2.py, slice_live.py, floorgrid.py)
- icp-2f.json: per-region rigid corrections Naver->live (west, east, boxw, south)
- Finding: 2F U+ east void (U 67-77) = EV/stair circulation (SeamPassage) ; toilets there are ours.

## Spawn 3 (killed 14:02) outputs
- services23f-work/seg-2f-{west,east,box}.json, seg-3f-raw.json, seg-polys*.json (segment.py on ICP-corrected Naver raster), ov3-*.png overlays.
- POIs (ICP-corrected UV): 2F 남자화장실 (-86.7,-9.8) -> seg toilet t63; 여자화장실 (-85.6,-16.3) -> t74; 201호 회의실 (-73.6,-3.5) -> r34 pair;
  202호 (-81.1,-1.3) -> r14; stairs (-87.3,-1.5),(-87.5,-25.9); north 여자화장실 (62.6,-8.3) -> toilet (62.5,-8.7) poly; second north
  toilet (59.6,-13.4); box 공용화장실 (-20.2,85.0); box 은행 (0.2,82.2); 현금인출기 (18.6,-1.1); 3F 여자화장실 (-12.4,71.1).
- Builder service units already built (empty ServiceRoom boxes, 4.1 walls, 1.8 open door, sign): 화장실 box u[-17.5,-11] v[80,89];
  화장실 u[-16,-10] v[67.5,72]; 역무실 u[-13,-3] v[25,35]; 수유방 u[-9,-3] v[16.4,21.5]; 화장실 u[52,61] v[-4.5,4.5];
  화장실 u[48,56] v[-19,-11.5]; 철도경찰서 u[-55,-44] v[-3.3,4.2]. No builder unit at the south end (U -80..-91).

## Spawn 5
- v1 DELIVERED (Main building it): specs/full/services-2f.json, 4 ToiletRooms (WC-2F-S-M/S-F, WC-2F-N-F/N-M); check_spec 0 err,
  KitValidate 0 err (19.9k tris). Plan edit agreed by Main: remove builder 화장실 shells u[52,61] v[-4.5,4.5] and u[48,56] v[-19,-11.5]
  (answered: shell1 = open hall; shell2 = open hall + r16 corner -> RM-2F-N-r16 in v2); Main applies plan edit with the v2 build.
- UNITY: always `unity command --project-path /Users/um-yunsang/CHOOGuard ...` (port 7801 = FPS session /tmp Editor).
- v2 draft: .planning/2026-09-26-full-twin/services23f-work/services-2f.v2.json (32 el: + south rooms 201/202/r34/r32/back0-2/r80/r24,
  north r16/r56, box WC-2F-B1-M/F (vestibule split inside builder shell), WC-2F-B2-A, RM-2F-B-역무실/수유방, ATM-2F). check_spec 0 err
  (KitExtend5 fixed Room slab floor check). KitValidate v2 running -> then write generator output to services-2f.json + message Main.
- v2 DELIVERED + BUILT by Main (after25, with plan edit S1 removing the two north shells).
- v3 (services-2f.json, 43 el): v2 + toilet entries from footage hCyroXn0Jhg 575/577 s (doors adjacent at the partition), r56
  extended to the facade polyline from SeamPassage5 (they build r26 only), RM-2F-S-철도경찰 + lid slab 11.15 inside builder shell,
  atrium (G-M02/BL-29) WallCladding on the live ceiling hole U -62..-44 V -22..-4 from 10.47 to 22.97 + Lighting; check_spec 0 err.
- services-3f.json v1: WC-3F-F inside builder 3F 화장실 shell (box frame, west edge a -12.55); check_spec 0/0, KitValidate 0 err.
- Receipts: asset-library/research-public/2026-09-26/Services23F/receipts.json; frames/hcy_*.jpg.
- voidscan2-after25: seen-12-4 (3F food-court east windows, V 92-99) = exterior view to the rail yard (report); seen-0-2 near r56.
- Overlay: specs/full/services-23f-overlay.png (panels services23f-work/final-*.png); report has requestsForMain + notes.
- v3 2F (43 el, 87.9k tris) + 3F v1 (1 el) KitValidate 0 err at spec paths (09:59 UTC); messaged Main 19:0x.
- Renders of built v2 (after25): services23f-work/render-{south-wc,north-wc,box-wc,atrium-up}.png (poses in poses/). atrium-up shows
  the MainShell roof with skylight bands over the hole and the open plenum edges (closed by the v3 WC-ATR claddings).
- Open for a next spawn: atrium west extension if Main trims the grid ceiling (ATR_RUNS); 3F men's toilet + food-court BL-18 wait
  for RegTech5 RY2 3F registration; window bands on atrium walls (no metric placement).
- v3 + 3F v1 BUILT by Main (after26/27). Main requests (19:1x): r80 door, WC-3F-F door_check, 매표창구 lid, 스토리웨이, atrium west (wait).
- v4: r80 door moved to V 2.15 (corridor end), r56 slab .3 (no floor collider at 68.2,-5..-3 in after28), CL-2F-매표창구 SuspendedCeiling
  10.47 in the ticket hole; NEW specs/full/services-2f-storyway.json (root 실내 트윈 마감/2F 스토리웨이, zone "2F storyway": Room shell
  + Storefront + ShopInterior convenience) from Naver f37 + zimcarry 2025 map/photo (storyway/ in research dir). WC-3F-F: door_check
  has no 3F walk routes (route_seeds(12.2) empty); outside/inside are in the main 3F component -> reported. check_spec 0 err all.
  Helper: services23f-work/reachmap.py (prints walk/reach grid). KitValidate v4 running -> message Main.
- v4 DELIVERED to Main (19:3x): services-2f 44 el (88.0k), services-2f-storyway 3 el (13.1k), services-3f 1 el; check_spec 0 err each
  (run each spec separately: mixed roots disable the own-root exclusion), KitValidate 0 err, door_check after28 only WC-3F-F (no 3F routes).
- Waiting: Main's go for the atrium west extension (RegTech5 measuring hCyroXn0Jhg 560-600 s); then edit ATR_RUNS + 2f-main.json via
  make_2f_specs.py (only the low-ceiling polygon).
- v5 (Main GO 19:5x): (1) atrium west: make_2f_specs.py ATRIUM_WEST_U -78.9 / _NORTH -6.4 (RegTech6 Public1F-1.done.json) ->
  2f-main.json regenerated: low-ceiling-0 loses U -78.9..-62 V -21.95..-6.4 and gains the S1 shell footprints (holes to roof 23
  in after28); 15 Storefront ids renumbered (content identical). Diff: services23f-work/atrium-west/diff.json (+ before/after json).
  services-2f ATR_RUNS: S/J3/N4/W at the new edges, LT-ATR L-shape (46 el). (2) U- escalator well: make_2f_main_enhance.py reads
  specs/full/public1f/slab-opening-uminus-esc.json (OPEN-2F-ESC-UMINUS) as a floor hole; overlay now sections check_spec's audit
  (after11 meshes deleted). 2FM-FLOOR-0 loses the well (48.7 u2) and gains the S1 shell footprints. Diff: services23f-work/esc-cut/diff.json.
  check_spec after30 0 err (2f-main, 2f-main-enhance, services-2f); door_check after30 prints only done. KitValidate running.
- v5 BUILT (after31/32). services-3f v2 (Main request 21:1x, RY2 fl-hash sigma 1.5 m): + RM-3F-FC-짬뽕 (kitchen Room a 37.4-44.4
  b 57.45-60.3, serving window), CT-3F-FC-짬뽕 counter, 5 DB-3F-FC-menu lightboxes (b 57.2, a 39.55-42.19), CT-3F-FC-빈티지38
  (a 22.6-27 b 54.6); "replaces" builder 푸드코트 table/chair (plan edit FC-1: "stalls": [] + keep-clear boxes). check_spec after32
  0/0, door_check done, KitValidate 0 err (4.1k tris). Overlay specs/full/services-3f-foodcourt-overlay.png; tools
  services23f-work/fc/fcplot.py, ry2-fc-sheet.jpg. 3F men's toilet c (cameras a 8.1-44.1, toilet at a -12.6..-9.7).
