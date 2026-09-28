# Full digital twin round — no undeveloped space behind doors, openings or edges

User (2026-09-26): 남은 한계작업 진행. 자료가 없으면 자료를 찾고, 기술이 부족하면 기술을 찾고, 도구가 필요하면 설치.
Scene view example: the glass door bank on the platform-side deck (source `Layer0PlatformsDetails/OfficialStation_Layer0_4`,
UV ~48-63, V ~16.5, 2F level 7.0, signs 나가는 곳 1-7 / 선상주차장 P 8) opens onto nothing: no floor between the doors and the
main-building north facade (V ~11.9), only the 1F roof at Y 4.4 and the exit roof at Y 13.35 (`sceneview/passage.jpg`).
Keep working until the station is a complete twin: every visible or reachable space modeled from evidence, back-of-house
closed off, no walk-off drops, tier-c items pursued with new data.

## Definition of done
1. No reachable or visible void: every door, glazing or opening leads to a modeled space (floor, walls, ceiling, finishes)
   or is closed (opaque door/wall) with the space behind enclosed.
2. No unguarded drop > 0.5 u next to walkable floor unless evidence shows it open (platform edges at track level).
3. Every public item on the official 1F/2F/3F guides exists (toilets, 수유방, 역무실/유실물, 종합안내, ATM, lockers, EV,
   escalators, stairs, 기차 타는 길 gates, paid concourse and stairs to platforms, exits 1-10 vestibules, metro link).
4. Remaining tier c only after an exhaustive search (sources tried listed per item).

## Lanes (respawned 2026-09-26 after the session interruption; the first spawn only left scratch files)
| Lane | Scope | Output root(s) |
|---|---|---|
| SeamPassage | U+ 기차 타는 길 / 나가는 곳 passage behind the Layer0_4 door bank; whole seam band between the main-building north facade and the box/deck (U -12..77, V 11.9..21) incl. the box west-edge trench and the bridge east end | `실내 트윈 마감/연결 통로` |
| PlatformSide | paid concourse deck, stairs/escalators/elevators to platforms, platform level near landings, box north gate paid side, exits 9/10, port deck | `실내 트윈 마감/승강장 측` |
| Public1F | Naver indoor 1F gnd/sisul/shop vectors (registered like the units): public outline + back-of-house closure, toilets, lockers, KTX특송, elevators, metro (B1) link entrance | `실내 트윈 마감/1F 공용` |
| Services23F | 2F W.C x4, 수유방, 역무실/유실물, 회의실, ATM, double-height hall ceiling; 3F toilets, food-court interior | `실내 트윈 마감/2F 편의`, `…/3F 편의` |
| ShopInteriors | interiors behind every storefront that shows an empty box (1F 16 units, 2F main/box/3F shops) by evidenced kind; footage layouts where visible | `실내 트윈 마감/점포 내부 <zone>` |
| RegTech | stronger registration (learned features + pycolmap, landmark anchoring, held-out residuals); service for the other lanes | `.tools/regtech/` |
| KitExtend | new kit types on request (Room shell, toilet fittings, windows, canopy/soffit, ...) in `AgentScripts/MajibangBuilder.cs` kit region | kit code |
| Main | void scan, builds, audits, walks, protected-source decisions, builder plans, disk | — |

## Coordination (the hub tool is no longer available)
- Messages: `write agent://<id>` (content = message); Main is `agent://Main`. Files as fallback:
  kit requests `.planning/2026-09-26-full-twin/kit-requests/<Lane>-<n>.json` (answer `<Lane>-<n>.done.json` by KitExtend);
  registration requests `.planning/2026-09-26-full-twin/reg-requests/<Lane>-<n>.json` (answer `.done.json` by RegTech);
  requests for Main (plan edits, protected-source decisions) go in the lane's final report.
- Lanes MAY run these read-only Unity commands themselves: renders
  `unity command run_script --no-banner --json --timeout 120 --file <abs>/AgentScripts/CaptureRegisteredScene.cs --entry CaptureRegisteredScene.Calibrated --args '[["<abs pose.json>","<abs out.png>"]]' --timeout_ms 100000`
  (pose: `{"width","height","verticalFov","position":[x,y,z],"forward":[..],"up":[0,1,0]}` in Unity world; world = (u*sin16.2+v*cos16.2, y, u*cos16.2-v*sin16.2)),
  and `MajibangBuilder.KitValidate` on their own spec (`--args '[["<abs spec.json>"]]'`, writes `<spec>.validate.json`).
  NEVER KitBuild, MajibangBuilder.Main / FirstFloorInterior, eval, editor_play, save or any other scene/asset-changing command.

## Interruption safety (the session was cut twice; every lane restarts from files)
- Keep `.planning/2026-09-26-full-twin/progress/<Lane>.md` current: done (with file paths), evidence found (URLs, frame
  times, measurements), decisions, open questions, exact next steps. Update it after every milestone and at least every
  ~15 tool calls. A respawned lane reads its progress file and `resume/<Lane>.md` first and continues without redoing.
- Never keep data only in /tmp: scratch goes to `.planning/2026-09-26-full-twin/<lane>-work/`, evidence to
  `asset-library/research-public/2026-09-26/<Lane>/`.
- Deliver incrementally: as soon as a spec passes check_spec + KitValidate (even a first subset), message Main
  (`write agent://Main`: spec path, root, what it adds) so it gets built; keep refining afterwards.

## Rules
- Evidence tiers as before (a / a-failed / b built; c reported). New data is expected: search the web, download videos
  (yt-dlp, ≤720p unless detail needs 1080p), photos, official guides; register them. Store under
  `asset-library/research-public/2026-09-26/<lane>/` with `receipts.json` (URL, retrieved date, what it shows, licence note).
  Disk: ~5 GiB free — at most 600 MB of new downloads per lane, no model weights > 400 MB; delete intermediates when done.
- Protected source (`FPSWorld/*`) is never edited. If evidence shows an opening where the source mesh is solid, report it with
  the evidence and the exact region; Main decides — do not work around it with overlapping geometry.
- Specs go through check_spec.py (default audit: the newest `geometry-afterNN` Main announces; now geometry-after33) and
  KitValidate; each spec has its own zone name
  (kit-folders.json guard).

## New kit types for this round (contract; KitExtend implements, lanes may use them in specs immediately)
Envelope, tiers, frames and materials as `kit-spec.md`. check_spec.py knows these types.
- `Room` | `polygon` (floor outline), `y`, `y1` (ceiling underside) | `wallFinish` plaster; `wallThickness` .12; `openings`
  [{`edge` (index), `at` (m from edge start), `width`, `height`, `kind` door/window/open}]; `ceiling` ceiling600/plaster/none;
  `floor` floorGranite/vinyl/none; `interior` none/office/storage/staff (a few generic props); `light` true; `collider` true.
  Encloses back-of-house space so nothing behind a door or window is void; doors themselves are ServiceDoor/DoorSet elements
  placed in the openings.
- `ToiletRoom` | `polygon`, `y`, `y1` | `kind` male/female/accessible/unisex; `entry` {`edge`, `at`, `width`}; `stalls` (auto);
  `urinals` (male, auto); `basins` (auto); `wallFinish` tile; `floor` tile; `sign` true (pictogram at the entry). Partitions,
  stall doors, WC pans, urinals with dividers, vanity with basins/mirror/dryer, ceiling + light, colliders on walls/partitions.
- `Window` | `edge`, `y` (sill) | `height` 1.2; `mullionPitch` 1.2; `frame` stainless/black/white; `glass` clear/frosted/teal;
  `sill` true. Pair with a WallCladding/Room opening.
- `Canopy` | `polygon`, `holes?`, `y` (underside) | `finish` metalSlat/aluminium/plaster; `fascia` .4; `lights` {pitch, ...}.
  Covered outdoor passages and decks.
- `Slab` | `polygon`, `holes?`, `y` (top surface) | `depth` .3; `finish` floorGranite/polished/concrete/<mat> (top);
  `edgeFinish` plaster; `soffit` plaster/none (underside); `collider` true (walkable top + edge faces). Walkable floor plates:
  passages, decks, landings and the infill strips where the 1 u cell floor leaves gaps to the walls (Main, voidscan2 fall runs).

## Storage (user directive 2026-09-25 19:05: local disk is short -> Unity Cloud)
- Cold data lives in Unity Cloud Asset Manager (org dbstkd5865, project "CHOOGuard Cold Storage", Personal cap 10 GB).
  Restore any offloaded file with `.tools/unitycloud/venv/bin/python .tools/unitycloud/uc.py get <repo path>`
  (manifest `asset-library/unity-cloud-manifest.json`). Uploads are Main-only (`uc.py put --delete`); the cap is nearly used.
- Lanes: no new large downloads; delete intermediates (frames, feature DBs) as soon as their results are written.

## Outcome (2026-09-25 20:20)
Integrated and verified through geometry-after33; details, diffs, walks, void scans, still-c lists and the decisions left to
the user: `verification.json`. Cold data: Unity Cloud (see Storage).
- SUPERSEDED 2026-09-25 23:20 (user: "유니티 클라우드 사용하지마"): no Unity Cloud use at all (no `uc.py get` either); the files
  offloaded earlier stay listed in the manifest and are not available locally.
