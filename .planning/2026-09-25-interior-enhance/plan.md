# Interior enhancement round — 1F, 2F main majibang, bridge, 2F box majibang, 3F

User (2026-09-25): 전체 내부 모델링을 확인해서 비어있거나 부족한 부분을 찾고 고도화 (1층, 2층, 3층, 맞이방 포함).
Standing rule (user, 2026-09-24): know the implemented / not-implemented boundary exactly; build only evidenced parts.

## Baseline (live scene, audit `1f-implementation/geometry-after11`)
- Renders: `audit/views/*.png`, sheets `audit/sheet-1f.jpg`, `audit/sheet-2f3f.jpg`.
- Gap backlogs: 2F/bridge/box/3F `2026-09-24-interior-twin/evidence-2f.json` (`gaps` G-*, `buildList` BL-*; statuses are from
  2026-09-24 11h and partly superseded by the specs below); 1F `2026-09-23-video-twin/1f-boundary-refresh/evidence-inventory.json`.
- Built kit layers (KitBuild roots under `실내 트윈 마감`): `1F` (specs/1f-detail.json), `수직 동선` (escalator-core),
  `2F 본관` (2f-main.json), `2F 박스` (box-3f/box-hall.json), `2F 연결` (bridge.json), `3F` (3f.json),
  `2F 박스 점포` (box-storefronts.json), `3F 점포` (3f-storefronts.json). Builder roots: `맞이방 · 원본 정합` (majibang-plan.json,
  Main only), `1층 · 원본 정합` (interior-spec.json 16 units, Main only). Source shell `FPSWorld/*`: protected, never modified.

## Lanes (parallel) and ownership
| Lane | Owns (write) | Output root |
|---|---|---|
| InteriorKit (kit code) | `AgentScripts/MajibangBuilder.cs` Interior kit region, `kit-spec.md`, `kit-preview/` | — |
| Enhance1F | `specs/make_1f_detail.py`, `specs/escalator-core/make_escalator_core.py`, `specs/enhance/*1f*` | `실내 트윈 마감/1F 보강` |
| EnhanceMain2F | `specs/make_2f_specs.py`, `specs/enhance/*2f-main*` | `실내 트윈 마감/2F 본관 보강` |
| EnhanceBox2F | `specs/box-3f/make_box_3f.py` (+ its 5 JSON outputs), `specs/enhance/*2f-box*`, `*2f-bridge*` | `실내 트윈 마감/2F 박스 보강`, `…/2F 연결 보강` |
| Enhance3F | `specs/enhance/*3f*` (existing 3F elements change only via EnhanceBox2F) | `실내 트윈 마감/3F 보강` |
| Main | scene, builds, audits, walks, renders, `majibang-plan.json`, `interior-spec.json` | — |

Zone lanes never run Unity commands. Changes to builder plans (majibang-plan.json / interior-spec.json) are requested from Main.

## New kit types (contract; implemented by InteriorKit, usable in specs immediately)
Envelope, tiers, frames, materials exactly as `kit-spec.md`. Listed params are the minimum; extras must be documented there.
- `Balustrade` | `polyline`, `y` | `height` 1.1; `panel` glass/bars/solid; `glass` clear/teal; `rail` round/flat/none; `railFinish` stainless;
  `postPitch` 1.2 (0 = frameless clamps); `shoe` true; `faceToward` (walker side); `collider` true.
- `DisplayBoard` | `point`, `y` (bottom) | `heading` (screen normal toward viewers); `mount` hanging/wall/floor; `kind` departure/arrival/screen/
  ledband/clock/timetable; `width` 2.4; `height` .6; `depth` .12; `doubleSided` (hanging default true); `ceilingY`; `title`;
  `rows` [{time, train, dest, track, status}] (world text); `texture`/`material` (screen image).
- `Counter` | `polyline` (customer-side front), `y` | `faceToward` (customer side); `depth` .7; `height` 1.05; `top`; `body`; `screens`
  [{at, width, height}] (glass ticket-window partitions); `equipment` true; `backPanel` {height, finish, text}; `label`; `collider` true.
- `DoorSet` | `edge`, `y` | `faceToward` (outside); `doorHeight` 2.4; `frameHeight` 3.0; `doors` [{at, width 1.8, type auto/swing, open}] or
  `count`+`pitch`; `frame` stainless; `transom`/`sidelights` glass; `vestibule` 0 (depth of an inner second line); `number` (yellow exit
  badge), `sign`; door openings stay walkable (no collider across an open/auto door).
- `Fixture` | `point`, `y` | `kind` clock/totem/standee/planter/bin/aed/cctv/speaker/extinguisher/hydrant/atm/kiosk/frame/charger;
  `heading`; `mount` floor/wall/ceiling/column; `width`/`height`/`text`/`texture` per kind; floor props get colliders.

## Rules for every lane
- Evidence tier on every element: a / a-failed / b built; c never built (reported). Positions come from registered or topological
  evidence (units, source walls/columns, registered boards/plans, SfM fits); appearance may use the kit library. Label guesses [INFERENCE].
- Offline checks before hand-off: `specs/enhance/check_spec.py <spec.json>` (footprint vs live geometry, walk corridors, floor datum,
  zone footprint). Walk corridors (`1f-implementation/walks/*.json`, 0.6 u buffer) stay clear except intended fixtures along edges.
- Budget per enhancement root: ≤ 120k triangles, ≤ 60 lights (mixed, no shadows) unless justified.
- Hand-off: spec JSON + generator + `<zone>-report.json` (gap id → built / fixed / still c + reason, evidence, position basis) +
  plan overlay PNG. Main builds, audits, renders and returns findings.

## Verification (Main)
Per build: before/after ownership diff (only the lane's root changes), renders at the baseline poses + new poses at empty spots,
audit metrics (`audit/metrics-*.json`: ceiling coverage, empty-floor share, eye-level wall finish), Play-mode route walks at the end.

## Outcome (2026-09-25)
Integrated and verified; details, diffs, walks, metrics, still-c lists and open decisions: `verification.json`.
Before/after renders: `audit/compare-1f.jpg`, `audit/compare-2f-main.jpg`, `audit/compare-2f-box-3f.jpg`.
