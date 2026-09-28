# Public1F progress (lane respawn #3 = Public1F3, started 2026-09-25 ~13:50)

## Inherited (Public1F-2, killed 13:40)
- `specs/full/public1f/`: mvt.py, register_naver.py + naver-1f-uv.json (Naver lon/lat -> UV; median .17 u on 16 units),
  check_registration.py -> registration-check.json (track wall .06-.45 u, U- end wall .86, U+ slant .51, office 1.2,
  rear row +7.3 u off the registered rear frontage line => Naver rear row is distorted; closure follows live unit backs),
  liveq.py (live geometry query), plot_naver_live.py.
- POIs with names: `asset-library/research-public/2026-09-26/Public1F/naver-poi/naver-1f-poi.json`, Services23F harvest README.
- Previous transcript dumped to `.planning/2026-09-26-full-twin/public1f-work/prev-transcript.txt`.
- Findings: 1F ceiling ends at V -5 (strip V>-5 = bare 2F slab underside); U- escalator beside #9 missing (2F slab solid) -> tier c / Main decision;
  elevator car boxes (Naver 453003 lines) at U~31.5-34, V -25.7..-30.8 doors facing U+; notch #2/#3 elevators+stairs;
  Naver 4919 (U 21.5-24.8, V -25.9..-32.3), 4924 (U 28.3-31.2, V -25.3..-31.9) blocks; 5479 U- BOH block (U -84.7..-43.6, V -10.5..3.6);
  10061926 track-side BOH (U -40..21, V -9..9.4); 10061937 office (U -0.6..21.7, V -14.3..-4.8); 10061924/5308/3331 U+ track-side blocks.

## Spawn 5 (Public1F5, started ~18:20 2026-09-25)
- liveplan.py now also saves `barrier` (collider section mask); `public1f-work/liveplan-geometry-after18.npz` regenerated (after19 differs only in 점포 내부 roots).
- Closure design (v1): W1 WallCladding along 5479 south edge (-43.64,-9.42)->(-59.66,-10.51)->(-59.9,-7.72)->(-83.88,-9.38) (+ link to #9 back NW corner (-42.6,-17.1));
  office 10061937 Room (#16 east side (-0.6,-5.2)/(0,-13.1) .. (21.1,-4.9),(21.7,-12.8)); E1 wall along V -5 (= 1F ceiling edge) from office NE to U+ shell;
  U- toilets 4152/4911 ToiletRooms close 4151/stair pocket. Stair pockets (U -45..-40 V -3..5; U 33-37 V -5..0; U -91..-85 V -2..0) behind closure = tier c report.
- No door-position evidence yet -> no ServiceDoors in v1.
- DONE v1 (18:35): `specs/full/make_public_1f.py` -> `specs/full/public-1f.json` 4 WallCladding closures; check_spec 0/0; KitValidate 0 err
  (`public-1f.validate.json` — note name = <stem>.validate.json); `public1f/closure_check.py` strip reach 1889.5 -> 0; plot
  `public1f-work/closure-v1.png`. Main messaged. liveq.py now defaults to newest audit (+ --audit DIR).
- Evidence notes: U- shell inner face U ~ -90.4 (V -28) .. -92.1 (V -4); U+ facade face ~ (61.2,-20)..(65.4,-10)..(68.7,-4).
  Escalator 수직 동선 rises U 19.5 (y0) -> 31.5 (6.5), clad south face V -18.07; lockers POI (29.6,-18.7) = bank backed on it.
  KTX특송: Naver 5562 (U 53.4..62.2 V -27.5..-23.2) + P2 frontage (U ~51, V -30.7..-21.6, faces U-) sits in front of #1's
  north front edge where the builder has #1's door -> conflict, report to Main (X06: zimcarry/KTX occupy #1 corner).
  Notch cores are open floor live: E core U 21.5..34, V -32..-25.3 (4919, stair (26.2,-30), 4924, elevator cars U 31.5-34 doors U+,
  P2 threshold (29.9,-30.5) heading U+); W core U -14..-1.4, V -34.6..-27.5 (elev cars U -14..-11.5 doors U-, 5483, stair, 5482).

## DONE v2 (19:05): same spec, 18 elements, 20,992 tris; check_spec 0 err/10 warn; KitValidate 0 err (--project-path now REQUIRED
  on unity commands); closure_check strip 0, plot `public1f-work/closure-v2.png` / `closure-v2.json`. Main messaged with requests:
  walk waypoint [36,-24.6]; 5483 vs #5 door (not built); #2 edge 4; stairs/U- escalator need slabOpenings (tier c).
  Decided: 짐캐리 = live #1 (P2 svc-zimcarry; Naver 5308 track-side label contradicted) -> built by others; KTX특송 (5562) conflicts
  with #1 door edge -> report c/Main; B1 metro: Naver B1 gnd only at V -115..-204 (plaza underground), no 1F-interior entrance -> c.

## Next
- public-1f-report.json (every item built/fixed/still-c + sources), receipts.json under asset-library/research-public/2026-09-26/Public1F/,
  overlay PNG (plot_naver_live --spec), 1-2 renders (toilets U-, notch core E) with --project-path.

## v3 (19:30, in progress) — supersedes "Next" above
- Main answers: v2 built (after25); walk route-central-to-uplus fixed; 5483 / #2 north glazing = open conflicts (tier c,
  send photo evidence if found); BOH stairs -> closed stair cores (done: 1Fp-stair-core-{e,w,uminus} Room + ServiceDoor);
  U- escalator -> separate spec `specs/full/public-1f-escalator.json` (zone '1F public escalator', root '실내 트윈 마감/1F 공용
  에스컬레이터', replaces 2F floor / 2F floor finish / 1F ceiling) + proposal `specs/full/public1f/slab-opening-uminus-esc.json`.
- Services23F5 caution: hCyroXn0Jhg (2023-05) 572-590 s shows continuous 2F atrium floor -> reg request
  `reg-requests/Public1F-1.json` (RegTech5 messaged). Sheet moved to asset-library/.../Public1F/frames/. receipts.json written.
- check_spec (after25): public-1f 24 el 0 err (run it ALONE: own-root exclusion only with one root); escalator 0/0.
  closure_check after25: strip 0, no walk crossings. KitValidate both running. Renders of voidscan2-after25 seen-0-{0,1,2}
  (my runs near the U+ facade) -> public1f-work/renders/.
- Next: fix seen-0-* if caused by toilets/5564 past the live floor edge; public-1f-report.json; message Main v3.
- DONE v3 (19:45): KitValidate public-1f 0 err (21,756 tris), public-1f-escalator 0 err (4,744); report
  `specs/full/public-1f-report.json`; overlay refreshed (plot_naver_live supports foot elements); renders
  public1f-work/renders/seen-0-{0,1,2}-after25.png (no visible void). Main messaged v3 + escalator HOLD pending RegTech.
- Waiting: reg-requests/Public1F-1.done.json (RegTech5 SfM of hCy 560-600 s). If it shows the well footprint inside
  the seen 2F floor -> tell Main to drop the escalator proposal (report as conflict c); else confirm the build.
- 20:10 RegTech6 answered Public1F-1 (NO-GO as drawn; GO trimmed). Well = north V -12.25 / west U -55.6; stair dropped.
  Regenerated public-1f-escalator.json (1 el, 0/0 after30, KitValidate 0), slab-opening-uminus-esc.json, 1f-detail.json via
  make_1f_detail.py (keep_open += well; only diff = 48.73 u2 hole in 1F-ceiling-U--0; backup public1f-work/1f-detail.before.json).
  Polygon sent to Services23F5; Main messaged. Lane done.
