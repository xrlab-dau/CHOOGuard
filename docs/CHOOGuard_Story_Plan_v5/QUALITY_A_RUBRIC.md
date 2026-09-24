# A 품질 고정 기준

CS-EXEC.01.01 · **A-FPS-1** · 사용자 요청: A가 될 때까지 지속 개선

9영역 모두 근거와 실행 결과로 PASS일 때만 전체 A로 판정한다. 미검증·실패 항목을 평균 점수로 덮지 않는다. 현재 판정은 **NOT_A**이며 수치 점수를 임의로 부여하지 않는다.

게임 품질(G1–G6, G9), 계산 검증/검수(G7), 절차·현장 권위(G8)를 구분한다. 공개자료의 정확도·수집 시점과 추정 범위를 기록한다. 조건을 낮춰 A를 만들지 않으며 기준 변경은 이유와 이전 기준을 보존한다.

## 채택 기록 (2026-09-23)

G1–G8은 **새로 작성한 것이 아니라** `.planning/2026-09-21-fps-reposition/fps-plan-graph.json`의 `evidenceGates`(`version: A-FPS-1-PROPOSED`)를 채택한 것이다. 그 초안은 "Root must record adoption and freeze before evaluating"이라고 적고 있었고, 채택 기록이 없어 평가를 시작할 수 없는 상태였다. 이 문서가 그 채택 기록이다.

G9는 2026-09-23 사용자 지시("튜토리얼은 정비 시뮬레이션 게임")와 같은 날 확정된 역할 경계에 따라 **신설**했다. 초안 작성 시점(2026-09-21)에는 없던 축이다.

이전 기준 **A-fixed-1**은 RTS 방향에서 작성되어 `facility/team/target` 선택, 시민 집단 지시, 기관 대시보드를 평가 대상으로 삼았으므로 현재 플레이를 평가할 수 없다. 전문은 [archive/QUALITY_A_RUBRIC_RTS_20260921.md](archive/QUALITY_A_RUBRIC_RTS_20260921.md)에 보존한다. 조건을 낮춘 것이 아니라 대상이 바뀐 것이며, 이전 기준에서 PASS였던 영역은 없다.

초안의 채택 조건을 그대로 승계한다.

> Scope change is explicit; no retroactive PASS from old receipts and no averaging missing domains.
>
> FPS-I0 and FPS-M01 are increments. Product A evaluates the accepted station/KTX FPS scope, including source-grounded terror and disaster mission coverage; an exterior fire slice cannot substitute for it.

## 담당 구분 (2026-09-23 사용자 확정)

| 영역 | 담당 |
|---|---|
| G1, G2, G3, G6, G9 | Adrianaline — 레퍼런스 게임 기반 게임성·시뮬레이션 |
| G4, G8 | PM — 부산역 내부 모델링·실제 현장 보정 |
| G5 | 공유 — 공간은 PM, 성능·주행은 Adrianaline |
| G7 | 미정 |

담당이 나뉜 영역도 전체 A 판정에는 동일하게 포함된다. 한쪽이 미완이면 전체는 A가 아니다.

## G1 Playable first-person mission loop

**PASS:** Native single-player play physically connects recognition, reporting/communication, NPC cooperation, passenger guidance, site control and handoff. The accepted release mission set includes at least one disaster and one terror-related staff response with source-matched duties. Normal, route-blocked and delayed/missed-communication variants remain playable and correctable. Completion follows world/NPC state, not button sequence or waiting. No separate debrief/quiz is required.

현재: NOT_VERIFIED; 설계 정본은 [MISSION_DESIGN.md](MISSION_DESIGN.md)(2026-09-24 채택). 첫 미션은 역사 화재·대피(Jev 055, 0.93/conf 0.89), 실행 그래프·완료 판정 규율·금지 조건이 확정됐고 G1이 요구하는 재난 1종·테러 1종·변형 3종과의 대응표도 있다.

**평가할 대상은 생겼으나 본편 코드가 0 이다.** 2026-09-24 실측: 신고·무전·전달, 승객 NPC, 직원 NPC 협업, 접근 통제, 오디오 모두 없음. 서 있는 것은 1인칭 이동·시선·상호작용 기반(`FirstPersonResponder`, `FpsInteractable`, `FirstPersonInteractionHud`)뿐이다. 설계 문서가 생긴 것과 플레이가 되는 것은 다르다.

## G2 FPS reference application and role fit

**PASS:** A source-to-runtime comparison maps movement/look responsiveness, interaction reach/visibility, spatial objectives, audiovisual acknowledgement and tension from the selected FPS references to station-staff play. Independent native review confirms actual embodied choices and readable consequences. No score for copied combat, resource building or shooter art alone.

현재: NOT_VERIFIED; 대조표 정본은 [BENCHMARK_FPS_MATRIX.md](BENCHMARK_FPS_MATRIX.md)(2026-09-24 작성). G2가 요구하는 5축(이동·시선 반응성 / 상호작용 사거리·가시성 / 공간 목표 / 시청각 확인 / 긴장)에 레퍼런스 원리와 런타임 요소를 대응시켰다.

집계: 구현 8 · 부분 2 · 미구현 9. **축 1·2는 서 있고 축 3·4·5는 거의 비어 있으며**, 빈 항목 대부분이 승객 NPC와 오디오에 걸려 있다. 전투 메커닉은 하나도 가져오지 않았고, RTS 전제 항목(건물 선택·관리 오버레이·도시 자원망)은 '가져오지 않음'으로 명시했다.

**대조표가 생긴 것이 PASS가 아니다.** G2는 *"Independent native review confirms actual embodied choices and readable consequences"* 도 요구하는데, 독립 검토도 그 검토가 확인할 플레이도 없다.

참조 자료: 1인칭 감각은 [FPS_RESEARCH_PROPOSAL.md](FPS_RESEARCH_PROPOSAL.md), 운영 루프와 자원 경쟁은 [PRODUCTION_GAME_BENCHMARK.md](PRODUCTION_GAME_BENCHMARK.md). 후자는 RTS 전제로 쓰였으므로 단일 1인칭 행위자에 맞는 항목만 인정한다.

## G3 First-person usability and Korean presentation

**PASS:** Test real Input System handlers for movement/look/interact, invalid/out-of-range/occluded targets, cancel, radio/menu focus, pause, cursor release and re-entry. At declared supported resolutions Korean prompts remain legible without blocking hazards/passengers; objectives can be followed without internal implementation knowledge. Fresh game captures and input traces required; no OS/human-parity inference.

현재: NOT_VERIFIED; former RTS G3 does not transfer

## G4 Station/KTX geometry and character presentation

**PASS:** Every release mission route, including the accepted KTX access/play area, has continuous collision, grounded spawn, correct door/platform/step scale and NPC navigation. Source-existing versus authored geometry is recorded. Close first-person views have coherent material scale, legible roles and walk/wait/guide/hold behaviour; no core placeholder route or inaccessible hollow interior passed as complete.

현재: FAIL_GAP: old interior floors unsupported; KTX playable access unverified. #237로 부산역 2층 대합실 실내가 들어왔으므로 재평가 대상이다. 담당: PM.

## G5 Bounded open site and performance

**PASS:** Station/KTX playable boundary and all release routes are traversable with blocked-route recovery; outside-city travel is excluded. On declared hardware/resolution, player-build warm and sustained mission runs including crowded turns have p95 frame time <=33.3 ms, no crashes/stalls/unbounded memory growth, and correctly timed captures.

현재: NOT_VERIFIED; narrow prior Editor sample is insufficient and above target

범위 메모: 도시철도 연결은 기록된 판정(`metro_disposition = surface_route_seal_underground`)을 따른다 — 지상 광장 경로를 만들고 지하 통로는 연출상 폐쇄로 처리하며, 검증된 실제 통로로 제시하지 않는다.

## G6 Deterministic mission and durable state

**PASS:** Typed accepted events advance mission/NPC state once. Pause/retry/focus loss, duplicate/stale acknowledgements and fallback responses preserve invariants. Save/reload or checkpoint restart restores player, passengers, incident flags and assignments consistently. Late Jev replies cannot revise completed actions. No evacuation-as-medical-recovery or timer-as-extinguishment.

현재: NOT_VERIFIED; only older component invariants exist

추가 조건(G9 연동): 튜토리얼 완료가 지속 상태로 기록되어 본편 시작 조건이 되어야 한다(`layer_coupling = state_carryover`). 튜토리얼을 건너뛰면 미션이 막히는 것이 아니라 초기 조건이 달라진다.

## G7 Computational verification and validation

**PASS:** Retain the existing scientific evidence boundary: all claimed fire/smoke/crowd/contact/health outcomes require applicable pinned published V&V, units/time/coupling/convergence and source-supported tolerances. The short old reference field cannot be stretched across the new geometry or mission time. A procedural cue-only slice has no predictive-physics PASS.

현재: NOT_VERIFIED; old 120-second reference and interrupted long run do not establish new site physics

## G8 Procedure and real-site authority

**PASS:** Every release-critical staff action is mapped to a dated applicable public manual or operator-confirmed procedure; match incident, role and site scope. Geometry/exits/occupancy/material/ventilation used in site-outcome claims retain provenance and uncertainty, with independent observed site validation for those claims. General guidance or an old different-incident chart cannot certify Busan procedures or as-built outcomes.

현재: NOT_VERIFIED; exact current operator role chain/interior/site calibration absent. 담당: PM.

## G9 Maintenance tutorial layer

**PASS:** The tutorial is a maintenance simulation played in the same first-person body as the mission layer and respects the confirmed role boundary — the player performs the checks and operations station staff actually perform, while technician work appears as an NPC handoff the player requests, witnesses and confirms. A request is not completed on issue: receive, travel, perform and result are observable, and the player has something else to do while waiting. At least one complete device tutorial runs end to end on a device evidenced in the repository and ends in a durable world-state change that G6 carries into the live layer. Procedure text distinguishes what a dated public source supports from what is a game design choice; no procedure is presented as an official internal manual while those manuals are not obtained. Completion is legible without a separate quiz, debrief screen or learning module.

현재: NOT_VERIFIED; 설계 정본은 [TUTORIAL_MAINTENANCE_DESIGN.md](TUTORIAL_MAINTENANCE_DESIGN.md). 2026-09-24 기준 절차·실행기·세션·기술자 인계 상태 기계·지속 상태 이월이 구현되어 PlayMode **53/53** 통과(영수증 [`2026-09-23-technician-handoff.json`](state/evidence/2026-09-23-technician-handoff.json), [`2026-09-24-carryover.json`](state/evidence/2026-09-24-carryover.json)). 역할 경계, "기다리는 동안 다른 일을 함", 회차 간 이월은 시험으로 확인됐다. 소화기 12개는 이제 생성기 코드로 재현된다.

2026-09-24 추가: 다중 대상화로 소화기 12개 전부가 유닛별 상태를 갖게 되어 기록된 Jev 판정(`one_each_hoisted` 0.87, `all_twelve_with_per_unit_state` 0.65)이 코드와 씬 양쪽에서 성립한다. PlayMode **61/61**(영수증 [`2026-09-24-multi-target.json`](state/evidence/2026-09-24-multi-target.json)). "기다리는 동안 다른 소화기를 점검한다"가 단일 유닛 안의 조건부 단계가 아니라 실제 유닛 전환으로 성립한다.

2026-09-24 추가: 시험만 통과하고 실제 플레이에서는 작동하지 않던 네 가지를 고쳤다 — 판정이 미리 채워져 플레이어가 고를 것이 없던 문제, 점검표가 보이지 않던 문제, 역할 경계를 어길 수단이 없던 문제, 빌드하면 튜토리얼에 도달하지 못하던 문제. PlayMode **75/75**(영수증 [`2026-09-24-playable.json`](state/evidence/2026-09-24-playable.json)). 이로써 "역할 경계를 존중한다"와 "완료가 별도 퀴즈 없이 읽힌다"가 코드 경로뿐 아니라 **플레이어가 실제로 도달할 수 있는 경로**에서 성립한다. 다만 61/61 시점에 이 네 가지가 모두 깨져 있었는데 시험이 하나도 잡지 못했다는 사실 자체가, 이 축을 자동 시험만으로 PASS 판정할 수 없다는 근거다.

2026-09-25 정정: 2026-09-24 항목에서 "점검표가 보이지 않던 문제" 를 고쳤다고 적었으나 **사실이 아니었다.** 렌더러를 붙였을 뿐 화면에는 그려지지 않았고(부착 전/후 프레임의 상단 픽셀 차이 0 개), 근거로 쓴 단언은 두 경우 모두 통과하는 것이었다. 방향(Quad 의 보이는 면)과 재질을 고쳐 실제로 그려지는 것을 픽셀 차이 **578 개**로 확인했다. 영수증 [`2026-09-25-play-verification.json`](state/evidence/2026-09-25-play-verification.json).

같은 실행에서 **시작 지점 문제**도 드러나 함께 고쳤다 — 생성기가 소화기 12 개는 배치하면서 플레이어만 두고 가, 시작 시야에 빈 하늘만 있었다(15m 이내 렌더러 2,322 개 중 5 개). 지금은 지정 대상 앞 1.40m 에서 그것을 보고 시작한다. (이 문제를 처음 보고할 때 거리를 44.41m 라고 적었으나 하네스가 `Units[0]` 을 기준으로 잰 것이고, 지정 대상까지는 6.32m 였다.)

**미해결 3 건.** 설비가 벽감에 끼어 정면 접근이 막힌다(좌표를 보행거리로만 산정하고 구조물 간섭을 검토한 적이 없다), 점검표를 보려고 고개를 숙이면 상호작용이 끊긴다, 기술자 인계 59 초 동안 화면에 아무 변화가 없다. 셋 다 자동 시험 75 개가 잡지 못했다. 이 축의 판정을 자동 시험으로 올릴 수 없다는 근거가 한 번 더 쌓였다.

**PASS가 아닌 이유 다섯 가지.** ① 기술자에게 몸이 없어(이동하는 NPC·애니메이션 없음) 1인칭으로 도착이 관찰되지 않는다. ② 이월이 연결할 본편(비상) 계층이 없어 "본편 초기 조건이 된다"의 절반이 미검증이다. ③ 생성기가 경고하는 유닛 간 2.00m 간격(BSN-CONC-FE-004 ↔ -006)이 상호작용 상한 3m 안이라 조준 모호 가능성이 남아 있고 확인되지 않았다. ④ 사람 플레이 검수와 프레임 캡처가 없다 — 2026-09-24 작업이 바로 "사람이 켜면 작동하게" 하는 것이었으므로 이 공백이 그 성과 자체를 미검증으로 묶는다. ⑤ 저장소에 `AudioSource`가 0건이라 요청·도착·완료 어느 것도 소리로 알려지지 않는다.

확정 사항 (2026-09-23):
- 역할 경계 — 플레이어는 육안 점검(압력계·봉인·거치대), 이상 발견 시 위치 지정 보고, 사용 공간 확보·통행 통제, 교체 후 재점검·기록. 기술자 NPC는 도착 후 분해·교체·충전을 수행하고 플레이어는 요청·입회·결과 확인. (`role_boundary_risk = staff_checks_only`, 확신 1.0)
- 첫 대상 — 저장소에 근거가 있는 단일 설비 하나로 시작한다. (`first_slice = single_device_tutorial`, 확신 1.0)
- 절차 근거 제약 — 내부 역사 매뉴얼을 확보하지 못했으므로 어떤 절차도 공식이라고 제시할 수 없다. (`manual_fidelity = 3.81/5`)

## 자동 실패 조건

초안의 `criticalFail`을 그대로 승계한다.

- Missing/failed domain blocks A; no grade from plan quality, artifact count or compile alone.
- Unsupported manual, calibrated-physics, medical, as-built or full-mission claims block A.
- Scope/rubric revisions must be logged against the user direction, not silently lowered after a failure.

## 필수 증거

초안의 `requiredReceipts`를 그대로 승계하며, G9 신설에 따라 마지막 항목을 추가한다.

- Root-adopted scope/rubric version
- Build/scene/script/input/source hashes
- Native input-handler traces and continuous player/NPC route traces
- Fresh game frames at actual target resolution
- Pinned mission event log and negative/recovery cases
- Declared player-build performance trace
- Claim-to-source procedure/site/model matrix
- Tutorial run trace showing the NPC handoff cycle and the durable state it wrote

## 현재 전체 판정

**NOT_A** (2026-09-23). PASS 영역 0개.

| 영역 | 판정 |
|---|---|
| G1 · G2 · G3 · G5 · G6 · G7 · G8 · G9 | NOT_VERIFIED |
| G4 | FAIL_GAP |

G9는 2026-09-23 기술자 인계 구현으로 NOT_EVALUATED에서 NOT_VERIFIED로 옮겼다. 설계 문서와 실행 영수증이 생겨 평가할 대상은 존재하나, 아직 통과 요건을 채우지 못한 상태다. 2026-09-24 플레이 가능성 작업(75/75) 후에도 NOT_VERIFIED를 유지한다 — 사람이 실제로 켜서 끝까지 해본 기록이 없는 한 판정을 올리지 않는다.

이전 기준(A-fixed-1)의 PARTIAL·PARTIAL PASS 판정은 평가 대상이 달라졌으므로 이 표로 옮겨 적지 않는다.
