# Openings + world extension round — open the 5 evidenced source walls, extend the world, close the remaining tier c

User (2026-09-25 20:56): "실내 디지털 트윈 모델링 작업 이어서 진행해". Decisions (21:00, ask): open ALL 5 evidenced openings in the
protected source and extend the world ("5곳 모두 열고 월드 확장까지") — non-destructively: FBX/assets untouched, scene-level
override meshes with a revert manifest; save the scene now and at milestones ("지금 저장"; saved 21:04, previous disk file in
`.planning/2026-09-24-interior-twin/backups/FpsStation.disk-before-save-20260925.unity`).
Previous round: `.planning/2026-09-26-full-twin/` (plan.md, verification.json = baseline, voidscan2-after33, walks 8/8).

## Openings (evidence from the previous round; exact positions are this round's job)
| # | Where (station UVY) | Source object(s) | Behind it |
|---|---|---|---|
| 1 | south gate 기차 타는 길: box SSW wall, inner face u = -13.63 - .0722 v, V ~50-62 (±6), y 7.0-9.4; strip floor 7.2 (+0.2 step) | PlatformsParking/OfficialStation_-부산역_선로상층부_0 | source south strip (floor 7.2, stairs/escalators to platforms) |
| 2 | exit 9: box east facade, inner face v = 97.97 + .0722 (u - 62), U ~4-12, y 7.0-9.4 | 선로상층부_0 | exit-9 wing U ~1-13, V 94.5..~121, floor 7.0, 2F/3F elevator at (1.3, 120.5) (Naver, OSM way 165346389) — world extension |
| 3 | exit 10: same facade, U ~48-55 (inference) | 선로상층부_0 | unknown — find evidence |
| 4 | port deck / 북항 connection (OSM way 987862341), world ends at V ~117.5 | (depends on the connection point) | pedestrian deck/bridge — world extension |
| 5 | main building 2F north facade U 55-65, V 11.7-12.2, Y 7-10.47 (open floor in the 9yw video 199.2-215.3 s) | Layer0PlatformsDetails/OfficialStation_Layer0_5, _Layer0_4, MainShell/OfficialStation_-부산역_5 | main 2F NE end (LED screen, A1 column already built) |
All five targets are large combined meshes whose MeshCollider uses the same mesh.

## Source override contract (SourceCut lane writes the tool; ONLY Main applies/reverts)
- Tool `AgentScripts/SourceOverrides.cs`: `SourceOverrides.Preview|Apply|Revert` with args `[<openings.json>]`.
- `openings.json`: `{"openings": [{"id", "target": "<scene path>", "box": {"p0": [u,v], "p1": [u,v], "half": t, "y": [y0, y1]},
  "evidence": [...], "by": "<lane>"}]}` — the box is the wall segment p0->p1 (station UV), +-half across it, y0..y1 absolute.
  Keep floors intact: y0 at or above the higher floor top on either side (south gate: 7.21).
- Apply: per target object one override mesh = original minus all its boxes (triangles clipped exactly, attributes interpolated),
  saved under `Assets/ChooGuard/Art/StationInterior/SourceOverrides/`, swapped into MeshFilter + MeshCollider; manifest
  `.planning/2026-09-27-openings-extension/source-overrides.json` (original mesh asset path + name per object) makes Revert exact.
- Preview: same computation in memory, report per box (triangles removed/split, removed area by facing) + optional render with a
  temporary HideAndDontSave clone; no asset or scene writes.
- Lanes deliver opening boxes as `.planning/2026-09-27-openings-extension/openings/<Lane>.json`; Main merges and applies.

## Lanes
| Lane | Scope | Output |
|---|---|---|
| SourceCut | SourceOverrides.cs (Preview/Apply/Revert, exact clipping, manifest), tests on copies | tool + report |
| SouthGate | opening 1: exact gate position/width/height from footage (RegTech), gate type, threshold ramp, signage, reveal; check the source south strip is complete/guarded once reachable | openings/SouthGate.json + specs/full/south-gate.json (root `실내 트윈 마감/남측 게이트`) |
| EastExits | openings 2, 3, 4 + world extension: exit-9 wing, exit 10 outside, 북항 deck/bridge (OSM, Naver, photos, video, news); closed/guarded far ends, no walk-off | openings/EastExits.json + specs/full/east-exits.json (+ `east-extension.json`) roots `실내 트윈 마감/동측 출구`, `실내 트윈 마감/동측 확장` |
| SeamNorth | opening 5 + NE 1F-2F stair, glass elevator box, the toilet-pictogram conflict; seam/seam-fit adjustments after the cut | openings/SeamNorth.json + seam-fit / specs/full/seam-north.json (root `실내 트윈 마감/연결 통로 북측`) |
| Evidence3F | 3F men's toilet, food-court stalls a 46-56, 빈티지38 portal, fridge block (route change -> request to Main) | services-3f.json v3 (with Services' generator) or specs/full/3f-extra.json |
| Conflicts1F | KTX특송 vs #1 door, Naver 5483 vs #5 door side, #2 north glazing vs core 4924, toilet hanging signs, staff stair #14/#15, B1 metro link | public-1f.json v4 and/or interior-spec.json edit requests |
| Signage | numbered exit badges 1-10 (2FM-NUMBERED-EXITS), gate signs, atrium upper window bands | specs/full/signage.json (root `실내 트윈 마감/안내 표지`) |
| KitExtend | new kit types on request: `Portal` (reveal lining of a cut opening: jambs, head, threshold, depth, finish), `Ramp` (threshold/slope, collider), others | kit code + kit-spec.md |
| RegTech | registration service for new footage (requests in `reg-requests/`) | .done.json answers |
| Main | apply overrides, builds, audits, diffs, void scans, walks, renders, scene saves, disk/cloud | — |

## Rules (inherit `.planning/2026-09-26-full-twin/plan.md`: tiers, check_spec, KitValidate, frames, interruption safety)
- Every `unity command` passes `--project-path /Users/um-yunsang/CHOOGuard`. Lanes: read-only renders + KitValidate + the
  SourceOverrides Preview only. Main: Apply/Revert, KitBuild, builder, saves.
- check_spec default audit = the newest `geometry-afterNN` Main announces (now geometry-after33). Run each spec alone.
- Evidence first; tier c only after an exhaustive search (sources tried listed per item). New data under
  `asset-library/research-public/2026-09-27/<Lane>/` with receipts.json; ≤ 400 MB per lane, delete clips after frames are
  extracted. User (23:20): NO Unity Cloud at all — no uploads, no downloads (`uc.py` is off limits); work only with the local
  Editor. Files offloaded earlier (manifest `asset-library/unity-cloud-manifest.json`) are NOT available: re-acquire from the
  original source (YouTube/blog URL in the receipts) if you really need one, else use what is local.
- Progress: `.planning/2026-09-27-openings-extension/progress/<Lane>.md` after every milestone (done/evidence/decisions/next).
- Deliver early: first validated subset to Main within ~40 min.

## Status 23:30 (after the API rate-limit stop at 21:25; user: "limit 재설정. 로컬 유니티 에디터로 작업 진행해. 유니티 클라우드 사용하지마")
- SourceCut DONE: `AgentScripts/SourceOverrides.cs` Preview/Apply/Revert; Preview exact (U30-40 test: removed 34.79 u2 = L x H,
  far triangles identical, area balance 0); Main's acceptance: Apply(test.json) changed only MainShell_5 renderer+collider,
  Revert restored an audit identical to geometry-after33.
- KitExtend6 left `Portal` and `Ramp` in MajibangBuilder.cs (compiles) + check_spec models; docs/tests/identity proof pending.
- Evidence gathered before the stop: progress/*.md (EastExits: exit 9/10 photos + Naver/OSM wing; Evidence3F / Conflicts1F /
  Signage candidate sources); SeamNorth opening boxes in openings/SeamNorth.json.
- Lanes run in two waves to stay under the API rate limit: wave 1 SouthGate, EastExits, SeamNorth, KitExtend7, RegTech8;
  wave 2 Evidence3F, Conflicts1F, Signage.

## Outcome (2026-09-26 01:20)
All 5 openings applied via SourceOverrides and finished behind; world extended east (exit-9 wing + 310 m port deck); 12/12 walk routes
COMPLETE; details, still-c and limits in `verification.json`.
