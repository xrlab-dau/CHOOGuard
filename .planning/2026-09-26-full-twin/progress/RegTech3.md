# RegTech progress (RegTech5 resumed 18:20 2026-09-25; file name kept)

## Done
- Tool code: `.tools/regtech/{register.py,rt_common.py,rt_sfm.py,rt_anchor.py}`; matching resumable (`match-progress.tsv`, flushed every 50 pairs).
- RegTech4: localize `q` -> `p.name` fixed.
- RegTech5 18:25: old chain at 2-4 s/pair (4096 kp, GPU shared) killed at 1000/10536. Added `match --match-kp` (default 2048,
  indices mapped back; bench 0.47 vs 0.80 s/pair, `regtech-work/bench_match2.py`). RY2 pairs pruned to 6287 (seq 5, gaps 10,20,
  dense 226-236/304-314 win 10, retrieval 5 + 1000 done as extras; old list `ry2-3f/pairs-full.txt`).
- New chain `regtech/run_chain.sh` launched 18:26 (nohup): RY2 match -> map; kmCz features(mask) -> pairs -> match -> map.
- Anchor additions: `--heldout-times "t0~t1"` (time-block split), camera `forward` heading check + `--gate-deg 6`,
  summary key `controls` (was landmarks). Localize: ref frames reduced to 3D-carrying keypoints + cache (≈10 s/photo),
  `--same-camera` (fixed SfM intrinsics), `--check controls.json` (landmark px check), incremental output, `--top 8`.
- Controls generator `regtech/controls/make_controls.py` -> `ry2-3f.json` (9 old rendered poses, sigma .3, forward),
  `march-smoke.json` (62 registered cams), `march-portal.json` (8 MainShell portal corners in 000034, check only).
- march-smoke: anchor hash PASS held-out 0.107 u / heading 0.60 deg (fit 0.118); timeblock 33~47 PASS held-out 0.171;
  localize 000034/000016/000061 (2258/689/1820 inliers, 2.2 px); portal corners 4.73 px RMS independent (old fit 4.10 as fitted).
  Floor check (regtech-work/ry2_checks.py <anchor> 0 0): floor layer -0.002 vs 0.
- README `.tools/regtech/README.md`; READY created 18:40; broadcast sent to agent://all. reg-requests dir created (empty).

- 18:40 RY2 pairs pruned again (4287: seq 3, gaps 8, dense win 6, retrieval 3 + 1500 done); chain relaunched (run_chain.sh).
- Seam request SeamPassage-1 (reg-requests/): work dir `regtech/seam-9yw` (135 frames 186-230 s, 3 fps, mask captions),
  `seam-9yw/run.sh` sfm launched 18:37 (log seam-9yw/sfm.log). Gridded frames for picking in regtech-work/seam/.
- cross-epoch localize kmCz->March: FAILED (6-28 inliers; regtech-work/kmcz-in-march.json) -> localize now rejects < --min-inliers 60.
- New `rt_mesh.py` + `register.py meshfit` (point-to-plane ICP vs geometry-afterNN renderers, time-block held-out, constraint
  report) and `--check-only` (meshcheck.json: anchor's points vs geometry by time block). March: ICP fit with source-only mesh
  is wrong (interior not in source); all-geometry meshcheck of hash anchor = median 0.046 u (blocks 0.033-0.076), ICP fit made
  held-out worse (0.034 -> 0.070) => use mesh as CHECK, fit from controls.
- regtech-work/kmcz_init.py: composes new->old SfM with the rejected provisional placement (start point only).

- 18:50 chain split: `regtech/run_ry2_map.sh <match pid>` waits for the RY2 match then maps; `regtech/run_kmcz.sh`
  (features MPS -> pairs seq3/gap8/retr3 -> match on CPU REGTECH_DEVICE=cpu 1024 kp -> map). rt_sfm `_device()` honours
  REGTECH_DEVICE / REGTECH_THREADS. CPU bench: 1024 kp 0.51 s/pair (4 threads), 2048 kp 1.64.
- Report generator `regtech-work/make_report.py` (reads every case's anchors/meshcheck/checks/localized + requests + disk;
  per-case notes from `regtech-work/report-notes.json`).

- 19:00 MPS is heavily contended (bg matchers 2-3 s/pair; fg 1.27): seam matching moved to CPU (seam-9yw/run.sh: match
  CPU 3 threads 1024 kp -> map); kmCz CPU match ~0.5 s/pair. rt_sfm features: existing files validated (_feat_ok) + atomic
  writes (an empty km__t01237333 npz from the disk-full run broke pairs; deleted, rerun).
- Request Public1F-1 (hCyroXn0Jhg 560-600 s, 2F south atrium, is a 1F->2F well at U -53.5..-43.4, V -17..-9.8 in the viewed
  floor?): work dir regtech/pub1f-hcy (120 frames, mask KineMaster "0.82,0,1,0.15"), pub1f-hcy/run.sh (features -> pairs ->
  waits for kmCz match -> CPU match -> map). Answer to Public1F5 (or Main) + Public1F-1.done.json.
- Seam landmarks: source Layer0_4 door-bank posts / Layer0_5 / MainShell_5 in U 48-70 V 8-24 (regtech-work/topview.py plan
  view helper; sfm view = leveled top view for coarse alignment).

- 19:17 seam SfM done (134/135, 1.31 px; top view regtech-work/seam/sfm-top.png, plan regtech-work/seam/plan-all.png).
- 19:18 kmCz SfM done: c0 272 frames 1244.6-1334.7 s (bridges the 1300.7 cut, reaches outside GATE1), c1 49 escalator frames.
  kmcz_init.py -> kmcz-hall/init-provisional.json; meshfit `provisional` (hold out 1300.7-1340): FAIL held-out 0.159 u
  (0.154 before), 37 % near geometry. Cross-epoch localize into March FAILED. => still no identifiable transform.
- regtech-report.json written by regtech-work/make_report.py (+ regtech-work/report-notes.json per-case notes). Rerun after
  every new result.
- Stopped at 19:20 by the request budget. Background jobs still running: run_ry2_cpu.sh (RY2 CPU match 2550/4287 -> map),
  pub1f-hcy/run.sh (CPU match 450/805 -> map). Check chain.log.

## RegTech6 (resumed 19:21)
- 19:22 pub1f-hcy SfM 120/120, 0.99 px. 19:39 RY2 CPU match + map done (chain.log).
- Public1F-1 ANSWERED (reg-requests/Public1F-1.done.json, Main messaged ~19:47): NO-GO for the polygon as drawn. The north strip
  V -9.8..-11.3 is seen floor (pedestrian at -48.4,-10.2; videographer path along V -9.4..-9.8). V -11.5..-17 is never in view.
  Stainless columns at V -11.4..-12.4 (U -59.5/-52.5/-44.8); stone ledge west face at U -55.6 (V -12.2..-14).
  Atrium upper west wall U -78.9 (above the toilet face U -79.3); 286 points above Y 10.47 over U -80..-62.
  Controls regtech/controls/pub1f.json (builder/Naver kit + SfM plane points + door head Y 9.05). Anchors: ctl-hash
  (hash every 3) and tb-shops (hold out 560-573.5; partition predicted 0.59 m off). Both FAIL the strict gate. No source geometry
  is visible there. Work files: regtech-work/pub1f/ (picks*.json, tri-*.json, footprint-*.json, fp-all.json, proj/ overlays).
  Helpers: /tmp/grid.py (gridded/zoomed frames); floor-hit picks via triangulate --picks with "plane".
- Main decided (~19:55): GO for a trimmed well (north edge V -11.8, west edge >= U -55.6) and GO for the atrium west at U -78.9.
- SeamPassage-1 ANSWERED (reg-requests/SeamPassage-1.done.json, Main messaged ~20:50). Anchor src-hash, controls
  regtech/controls/seam.json: source exit-8 jamb (64.95,12.16) in 4 frames + post + corridor south wall + floor. Visually
  verified at the exit (proj/s__t00215333.jpg). Strict gate FAIL (LOO). The facade line V 11.7-12.2 is open floor in the video;
  the LED, A1 column and toilet are south of it. Work files: regtech-work/seam/.
- RY2 3F done ~21:05 (regtech-work/ry2/ry2-result.json, Main messaged). Anchors fl-hash / fl-tb-start / fl-tb-end use
  controls/ry2-3f-floor.json (old cams + 16 floor points Y 12.2). Time blocks miss by 1.27-1.54 m across the food court;
  balustrade walk b >= 46.2; floor 12.26. Poses in fl-hash/poses-foodcourt (+ render cmp_*.jpg). 3F men's toilet not found.
- make_report.py rerun (report-notes.json updated). database.db deleted for pub1f-hcy, seam-9yw, ry2-3f (keep sfm/model);
  regtech dir 524 MB. No background jobs.

## Next (exact)
1. kmCz: GATE1 / exterior source-vertex picks in 1318-1334 s frames -> landmark anchor with held-out -> compare with the
   previous gate (ceiling median 0.212 / p95 0.463).
2. RY2: find a source/landmark control inside the food court (e.g. 3F balustrade edge b 44.3 or the stall fronts once built)
   to beat the 1.5 m time-block error; look for the 3F men's toilet in RY2 or other footage.
3. Seam: add a far source control (door-bank round column 54.3,19.9 when visible) to make LOO meaningful.

## RegTech7 (resumed 21:10, openings-extension round)
- Serving requests for `.planning/2026-09-27-openings-extension/` lanes (SouthGate, EastExits, SeamNorth, Evidence3F,
  Conflicts1F, Signage). Queue: none yet (Public1F-1, SeamPassage-1 answered by RegTech6). regtech dir 524 MB. Idle-waiting.
