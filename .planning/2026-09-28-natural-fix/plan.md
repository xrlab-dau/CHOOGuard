# Naturalness round - floating / wall-less / unreal elements the user flagged (2026-09-26 01:10-01:40 KST)

User messages (verbatim, in order):
1. "OfficialStation_-부산역_5 이 오브젝트와 주위 오브젝트들이 아직도 부자연스럽고, OfficialStation_-부산역_선로상층부_0 맞이방과 연결 벽이 부자연스러워"
2. "Box_Majibang_3F_EdgeBand 이 오브젝트도 공중에 띄워져있는거야? 벽이 없는거야? 제대로 확인해봐"
3. "Kit_Canopy_Canopy_Dark, Kit_Slab_floor_station_polished, Kit_Glass_Kit_GlassClear, Kit_RoomWall_기둥_마감 외부에 있는 이 오브젝트들
   실제 존재하는 오브젝트 맞는거야? 너무 부자연스러운데"
Decision (ask, 01:35): exterior = "역 인접 구역만 근거로 재구축" - remove the ribbon deck, dark canopies, elevator box and 3F wing;
rebuild from evidence: exit-9 forecourt (slanted white columns + slat canopy), 부산역 하늘광장 at its satellite footprint
(lawn/sand/paving, railings), exit-10 covered walkway (white steel frame + glass roof), support columns and ground below;
walkable area ends at the plaza's far end (where the bridge over the road starts) with a closed gate.

Baseline: scene saved at after49 (`.planning/2026-09-27-openings-extension/verification.json`); audit
`.planning/2026-09-23-video-twin/1f-implementation/geometry-after49` (check_spec default). Editor: ours PID 51435, port 7801 -
EVERY `unity command` passes `--project-path /Users/um-yunsang/CHOOGuard` (the other session's worktree Editor owns 7800).

## Diagnosis so far (Main, diag in `.planning/2026-09-27-openings-extension/diag/`)
- Tools: `diag/pick.py AUDIT POSE OUT [cols]` (owner of each pixel of a pose, CPU ray cast on the audit; ~15 s at 64 cols),
  `diag/sections.py AUDIT OUT SPEC.json` (vertical cross-sections, owners coloured), `east-exits-work/satuv.py` (Naver z19
  satellite resampled to station UV + overlays).
- EdgeBand (`맞이방 · 원본 정합/Box frame/Box_Majibang_3F_EdgeBand`, one Canopy_Dark batch of 3 quads from
  majibang-plan.json `thirdFloor.edgeBands`): band 1 (3F east slab edge, box a 19.2-65 b 44.3) and band 2 (wing edge a 16,
  b -17.8..39.4) sit on the 3F slab edges (sections `diag/sections/s-band1-a42.png`, `s-band2-b10.png`). Band 3 "West bulkhead
  band" (b 0.05, a 30-60, y 9.3-12.0, INFERRED, no panels) FLOATS: nothing below (hall floor 7.0), nothing above (raised roof
  ~20.8 over b > -1, lower roof/liner 15.2 over b < -1, step face at b ~ -1), open ends (`s-band3-a45.png`,
  renders `diag/band-sheet.jpg`). The screens `Box_Majibang_WallPanel_*_West_*` hang on it. Real (RY2qvE0Tugk 322 s / 287 s,
  `diag/ry2-band-frames.jpg`): the far side of the hall carries a dark band with LED screens and green departure boards over
  the gate/shop line, a light-grey solid bulkhead block with three screens rising to the roof at the centre, clerestory
  glazing above the band elsewhere.
- 선로상층부_0 junction: source materials are double-sided (cull 0) and several are near-black (3208d394 .13, 1038eb58 .03,
  6df65da9 .0, 0d504331 .02); from the 2F box hall and the seam their faces read as black ceilings / walls
  (`diag/box-sheet.jpg`, owners `diag/sections/pick-box-*.txt`, `pick-seam-to-box-wall.txt`).
- MainShell_5 surroundings (`diag/ms5-sheet.jpg`, `pick-ms5-*.txt`): big plain light-grey MainShell_5 wall panels, dark
  slanted `실내 트윈 마감/연결 통로 설비/Kit_Column_Kit_Stainless` blocks through the ceiling, blank blue
  `Kit_Glyph_Kit_ScreenBlue` board, freestanding grey `맞이방 · 원본 정합/Majibang_ServiceWalls` panels in the hall
  (`pick-box-hall-to-sw.txt` 43 %), black ceiling bands.
- Exterior (`diag/east-overlay-model.png`): `실내 트윈 마감/동측 확장` + `동측 출구` = 8 m ribbon deck ~310 m on the OSM line,
  dark slat canopy, glass rails, 3x3 m elevator lobby 7-15 m, 3F wing slab 12.2 + roof 15; NOTHING exists below Y 5 north of
  V 117.5 (world ends) -> it floats over the skybox. Real: exit 9 -> slat canopy on slanted white steel columns
  (Hmj t028-t032) -> 부산역 하늘광장, elevated ~60 x 95 m (satellite U -5..56, V 100..195; BPA 2017: 100 x 60 m stage-1
  plaza, 570 m route) with lawn/sand + paving + rails (Hmj t033-t044, simplyssol 07) -> colourful-rail bridge (t045-t048) ->
  white-frame glass-roof walkway (t049-t052, ssh 14) -> terminal 2F. Exit 10 opens straight into a covered corridor
  (simplyssol 10) = the white strip U 45-56 on the satellite. Naver 2F labels the U 0-12 wing "나가는곳 8번출구" (EastExits2
  judged it outdated; re-check).

## Lanes
| Lane | Scope | Output (root) |
|---|---|---|
| SkyPlaza | exterior rebuild per the decision; keeps only the exit-9/10 door banks + reveals of east-exits.json | generator `specs/full/make_sky_plaza.py`, `specs/full/sky-plaza.json` (root `실내 트윈 마감/하늘광장`), trimmed `east-exits.json` (doors/reveals only), removal list, walk routes, kit requests |
| KitExtend8 | the ONLY editor of `AgentScripts/MajibangBuilder.cs` this round: kit features requested by lanes (inclined / V columns, glass-roof canopy with white steel frame, slat colour, lawn / sand / paving / asphalt finishes, piers, closed boundary fence/gate, anything HallJunction/SeamMain need) + kit-spec.md rows + KitPreview + test specs | builder code, `.planning/2026-09-28-natural-fix/kit-requests/*.done.json` |
| HallJunction | band 3 (+ verify bands 1-2), the 선로상층부_0 <-> 맞이방 junction (black source faces, connection wall), freestanding `Majibang_ServiceWalls` in the hall; owns `맞이방 · 원본 정합/*` (builder plan edits = requests to Main) and `실내 트윈 마감/2F 박스*` | plan-edit requests, `specs/full/hall-junction.json` (root `실내 트윈 마감/홀 접합부`), opening/override requests |
| SeamMain | MainShell_5 surroundings: seam passage + 2F main NE end (grey MainShell_5 panels, dark slanted columns, blank blue board, black ceiling bands); owns `실내 트윈 마감/연결 통로*`, `2F 본관*`, `2F 연결*` specs | spec edits (make_seam*.py, seam-north), `specs/full/seam-main-fix.json` (root `실내 트윈 마감/본관 접합부`) |
| FloatSweep | read-only scene-wide detector: components (> 1 m2) with no support (nothing within 5 cm of their top or bottom edge / back), wall-less edges beside walkable floor, near-black large faces visible from walkable cells; ranked list with owner, UVY, pick/section evidence | tool + `float-sweep.json` + crops; items routed to owners by Main |

## Rules (as the previous rounds)
- Lanes never change the scene: no KitBuild, builder Main/Apply, SourceOverrides.Apply/Revert, SceneSnapshot.Save, Play mode,
  eval. Allowed Editor calls: CaptureRegisteredScene.Calibrated renders, MajibangBuilder.KitValidate / KitPreview,
  SourceOverrides.Preview. Busy/compile error -> wait ~30 s and retry.
- Evidence tiers a / b built, c reported with sources tried; label guesses [INFERENCE]; positions from registered /
  topological evidence (satellite registered to UV, OSM, Naver indoor, source geometry, SfM); appearance from the kit.
- Specs: `{"zone","root","basis","elements":[...]}` from a deterministic generator; check_spec 0 errors (run alone) +
  KitValidate 0 errors before handover; unique zones (`kit-folders.json`).
- New data under `asset-library/research-public/2026-09-28/<Lane>/` + receipts.json; <= 400 MB per lane; delete clips after
  frame extraction; NO Unity Cloud; texture assets only CC0 / explicitly licensed (ambientCG, Poly Haven) - never Naver /
  YouTube / blog imagery as textures.
- Progress: `.planning/2026-09-28-natural-fix/progress/<Lane>.md` after every milestone; first validated subset to Main within
  ~40 min. Messaging ids: SkyPlaza, KitExtend8, HallJunction, SeamMain, FloatSweep, Main (`write agent://<id>`).
- Main integrates: builds, removes superseded roots, audits (geometry-after50+), diffs, renders, walks, void scan, saves.
