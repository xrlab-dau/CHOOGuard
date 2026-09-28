# Main progress (openings + extension round) — a resumed Main reads this first

## 21:10 start (2026-09-25)
- User: "실내 디지털 트윈 모델링 작업 이어서 진행해". Decisions (ask): open all 5 source openings + world extension (non-destructive
  overrides); save the scene now and at milestones.
- Audit after34 == after33 (no external change; after34 deleted). Scene SAVED 21:04 (SceneSnapshot.Save; previous disk file
  backed up to .planning/2026-09-24-interior-twin/backups/FpsStation.disk-before-save-20260925.unity). isDirty false, 18 roots.
- Source objects at the openings (after33): #1/#2/#3 PlatformsParking/OfficialStation_-부산역_선로상층부_0 (83,873 tris,
  MeshRenderer + MeshCollider same mesh; #1 also MainShell _7/_6 edges); #5 Layer0PlatformsDetails/OfficialStation_Layer0_5
  (101,542), _Layer0_4 (94,743), MainShell/OfficialStation_-부산역_5 (94,776).
- Lanes spawned 21:10: SourceCut, SouthGate, EastExits, SeamNorth, Evidence3F, Conflicts1F, Signage, KitExtend6, RegTech7.
- Main next: SourceCut acceptance (Apply+Revert test), merge openings/*.json -> Apply -> audit -> voidscan -> announce;
  build lane specs; save the scene after each milestone; final walks/void scan/verification.

## 23:30 after the rate-limit stop (all 9 lanes died with 429 at ~21:25)
- User: "limit 재설정. 로컬 유니티 에디터로 작업 진행해. 유니티 클라우드 사용하지마" -> no Unity Cloud use at all (plan.md updated,
  previous plan's Storage section marked superseded). The 38 offloaded files stay in the cloud (not restored; say so in the report).
- SourceCut acceptance by Main: Apply(openings/test.json) -> geometry-after34 changed only MainShell_5 renderer+collider;
  Revert -> geometry-after35 identical to after33 (both test audits deleted). Tool ready: AgentScripts/SourceOverrides.cs.
- Digests of the stopped lanes: resume/<Lane>.md. Wave 1 respawned 23:32: SouthGate2, EastExits2, SeamNorth2, KitExtend7,
  RegTech8 (spawn args resume/spawn-wave1.json). Wave 2 (Evidence3F2, Conflicts1F2, Signage2) when wave-1 lanes finish.

## 00:00 (2026-09-26) after40
- Applied: south-gate-2f-tracks (provisional V 52-60; RegTech8 SouthGate-1 NO-MATCH -> stays [INFERENCE] within the evidenced
  V ~50-62) -> after36; seam-north-5a/b/c (MainShell_5 U 50-64.93) -> after37. Built: south-gate v1 (Portal+Ramp) after38;
  seam-north.json (3 Portals + floor band/tactile patches, new root 연결 통로 북측) + public-1f (unchanged) after39; south-gate v2
  (Ramp landing, 4 Balustrades, 21 lights) after40. 2F fall cells 10 -> 1. Scene saved after each milestone.
- Open: black rectangle across the strip behind the gate (SouthGate2 v3; seen-7-7/7-8), EastExits2 openings 2/3 + extension,
  SeamNorth2 NE stair/glass lift/pictogram, Evidence3F2 fridge v4 (KitExtend7 cooler kind), Signage2.
- Conflicts1F2 finished: all six items still-c with new sources (photos in asset-library/research-public/2026-09-27/Conflicts1F).
- Audit bin policy: keep the newest two audits complete (lanes read the previous one).

## 00:40 after44
- south-gate v3 (after41): remaining 2F fall cell (-17.875,58.875) = voidscan window artifact (ramp top 7.13 > L+.12); a level-7.2
  scan of the strip reached from the gate (315 u2) has 0 fall cells. Black panel across the strip = source defect
  (MainShell_6, source-6df65da90f8211785446.mat BaseColor 0) — reported, not covered.
- services-3f v4 (fridge FX-3F-FC-fridge, KitExtend7 Fixture kind cooler) after42; route-3f-centre-to-foodcourt re-planned around it
  (A* on after40, 1.4 u clearance) -> walk COMPLETE 7/7.
- east-exit9-2f + east-exit10-2f APPLIED (after43); east-extension slab subset (3 Slabs) built (after44). Walls/guards/canopy/doors
  pending from EastExits2. voidscan2.py takes VOIDSCAN_BOUNDS=u0,v0,u1,v1 (default grid ends at V 102; the extension reaches V ~280).
- Finished without new geometry (sources tried in their reports): Signage2 (exit badges 1-7, window bands), SeamNorth2 (NE stair,
  glass lift, pictogram), Conflicts1F2, RegTech8 (Evidence3F-1, SouthGate-1, Signage-1 all declined: registration gates failed).

## 01:20 FINAL (after49)
- Exits 9/10 re-applied with half .8 (outer facade layer 0.48-0.72 u kept them closed at .45) -> after47; wing north-end rails opened
  onto the deck (make_east_exits.py) -> after48; deck slab mitred like the rails (corner falls) -> after49. Scene saved.
- Routes added by A* (main-work/make_routes.py): exit9->port deck, exit10 landing, south gate->strip, seam north opening; 12/12 COMPLETE.
- verification.json written. Editor: ours PID 51435 port 7801 (the other session's worktree Editor owns 7800).
