# 정비 시뮬레이션 튜토리얼 설계

2026-09-23 · CS-EXEC.01.01 · 품질 영역 **G9**의 정본 문서 · 담당 Adrianaline

이 문서는 새 설계를 제안하는 것이 아니라, **이미 저장소에 구현된 튜토리얼 계층을 설계로 확정하고 남은 공백을 명시**한다. 절차 정본은 코드가 아니라 데이터 파일이며, 이 문서는 그 데이터가 왜 그렇게 생겼는지와 무엇이 아직 없는지를 기록한다.

## 1. 확정 사항

| 축 | 확정 내용 | 근거 |
|---|---|---|
| 장르 | 본편과 같은 1인칭 몸으로 하는 정비 시뮬레이션 | 2026-09-23 사용자 지시 |
| 역할 경계 | 플레이어는 점검·기록·인계 발행까지. 분해·교체·충전은 하지 않는다 | `role_boundary_risk = staff_checks_only` (확신 1.0) |
| 첫 대상 | 저장소에 근거가 있는 단일 설비 하나 | `first_slice = single_device_tutorial` (확신 1.0) |
| 본편 연결 | 튜토리얼 완료가 지속 상태로 남아 본편 초기 조건이 된다 | `layer_coupling = state_carryover` (0.63) |
| 절차 근거 | 내부 역사 매뉴얼 미확보 → 어떤 절차도 공식이라 제시 불가 | `manual_fidelity = 3.81/5` |

판정 출처는 `.planning/2026-09-22-tutorial-fps-axis/jev-tutorial-fps-axis-001.result.json`이다.

## 2. 첫 대상 설비: 소화기 월간 상태점검

절차 정본은 [`Assets/ChooGuard/Art/Procedures/fire-extinguisher-monthly.json`](../../Assets/ChooGuard/Art/Procedures/fire-extinguisher-monthly.json) (`id: fire-extinguisher-monthly`, `version: 1`)이다.

이 설비를 첫 대상으로 삼는 근거는 세 가지다. 공개 시행세칙 조문에서 단계를 직접 유도할 수 있어 `manual_fidelity` 제약을 지키면서도 절차를 만들 수 있고, #237이 법규 기반 소화기 12개를 역사에 배치해 대상이 씬에 실재하며, 조문 자체가 "역무원은 직접 수리하지 않는다"는 역할 경계를 담고 있어 별도 규칙을 덧붙이지 않아도 된다.

### 2.1 단계 정의

각 단계는 `requires`(선행 단계), `allFacts`(월드에서 읽어야 하는 사실), `effect`(월드에 가하는 변화), `observe`(무엇으로 완료를 확인하는가), `failReason`(거부 시 현장에 표시할 이유)을 갖는다.

| # | `id` | 라벨 | `effect` | 역할 |
|---|---|---|---|---|
| 1 | `read-serial` | 고유번호 판독 | `record-serial` | 대상 특정 |
| 2 | `read-spec-plate` | 제원표 판독 | — | 유지관리 기준 확인 |
| 3 | `check-gauge` | 지시압력계 육안 확인 | — | 외관점검 |
| 4 | `check-body` | 외관 부식·기계적 결함 확인 | — | 외관점검 |
| 5 | `record-verdict` | 적합·부적합 판정 기재 | `record-verdict` | 판단 |
| 6 | `issue-repair-order` | 폐기·교체 요구 인계 발행 (`conditional`) | `issue-repair-order` | **역할 경계 지점** |
| 7 | `attach-tag` | 점검표 부착 | `attach-tag` | 완료 증거를 월드에 남김 |
| 8 | `close-inspection` | 역무실 단말 재스캔 | `close-inspection` | 대조·감사 |

6번은 `conditional: true`다. 부적합 판정일 때만 요구되며, 적합 판정에 발행을 시도하면 거부된다. 이 단계가 "플레이어는 요구를 발행하고, 실제 폐기·교체는 하지 않는다"는 경계를 절차 안에 박아 넣는다.

### 2.2 근거의 한계

데이터 파일의 `sourceNote`가 그대로 제약이다.

> 별지 제7호서식의 실제 항목은 HWP 표 추출에서 미확보 상태이므로 서식 항목 수치를 넣지 않았다. 여기의 단계는 본문 조문에서만 유도했다.

따라서 **점검표에 표시하는 항목 수와 서식 형태는 게임 설계상의 선택이지 서식 재현이 아니다.** 화면에 서식을 공식처럼 제시하지 않는다. 각 단계의 `basis` 필드에 적힌 조문만이 근거로 인정되는 범위다.

## 3. 무엇이 이것을 게임으로 만드는가

절차를 순서대로 누르는 체크리스트는 게임이 아니다. 현재 구현은 세 가지 장치로 판단을 만든다.

**월드가 완료를 판정한다.** `TutorialSession.Advance()`는 효과를 적용한 뒤 사실을 다시 읽어 같은 단계를 재판정하고, 그때도 요건이 충족될 때만 커밋한다. 버튼을 눌렀다는 사실이 아니라 월드 상태가 완료의 근거다. 실패하면 "월드 상태가 단계 요건을 충족하지 못해 완료로 기록하지 않았습니다"가 뜬다.

**오판정이 점수가 된다.** `MisjudgementCount`는 대상이 부식·결함 상태(`ShouldBeUnfit`)인데 적합으로 기재하면 올라간다. 제원표의 기한만 보고 통과시키면 걸린다. 즉 4단계 외관 확인을 형식적으로 지나치면 5단계에서 틀린다.

**역할 경계 위반이 설교가 아니라 감점으로 전달된다.** `TryFieldRepair()`는 현장 수리를 시도할 수 있게 열어 두되, 거부하면서 `RoleBoundaryViolations`를 올린다. 코드 주석이 의도를 명시한다 — "거부가 곧 채점 항목이다 — 설교가 아니라 감점으로 전달된다."

**감사 단말은 즉시 야단치지 않는다.** `Audit()`은 8단계에서 월드를 다시 읽어 미충족 항목을 `위치 · 항목 — 근거 조문` 형태로 열거한다. 진행 중에는 막지 않고, 끝난 뒤에 무엇이 비었는지 보여준다. 별도 퀴즈나 복기 화면이 아니라 역무실 단말이라는 월드 안 오브젝트다.

되감기(`Rewind`)와 재시작(`Restart(hideChecklist)`)이 있어, 체크리스트를 숨긴 채 다시 하는 것으로 숙달을 확인할 수 있다.

## 4. 현재 구현 상태 (2026-09-23 실측)

| 요소 | 경로 | 상태 |
|---|---|---|
| 절차 데이터 | `Art/Procedures/fire-extinguisher-monthly.json` | 8단계 완성 |
| 절차 실행기 | `App/Fps/Work/ProcedureRunner.cs` | 존재 |
| 튜토리얼 세션 | `App/Fps/Tutorial/TutorialSession.cs` (204줄) | 존재 |
| 점검 대상 | `App/Fps/Work/FacilityInspectable.cs` (95줄) | 존재 |
| 시선 추적 | `App/Fps/Work/FpsGazeTracker.cs` (40줄) | 존재 |
| 씬 생성 | `Editor/FireExtinguisherSliceBuilder.cs` (346줄) | 존재 |
| 시험 | `Tests/PlayMode/FpsProcedureTests.cs` (194줄), `FpsAuditTerminalTests.cs` (230줄) | **32/32 통과** (2026-09-23) |

시험 실행 영수증: [`state/evidence/2026-09-23-tutorial-playmode.json`](state/evidence/2026-09-23-tutorial-playmode.json), 결과 원본 `.planning/2026-09-23-tutorial-playmode/playmode-results.xml`. 커밋 `d61997d0`, Unity 6000.3.23f1, 배치모드 `-runTests -testPlatform PlayMode -assemblyNames ChooGuard.PlayModeTests`, 종료 코드 0.

| 시험 클래스 | 결과 |
|---|---|
| `FpsProcedureTests` | 7/7 |
| `FpsAuditTerminalTests` | 8/8 |
| `FpsPromptPipelineTests` | 4/4 |
| `IntegratedInputPlayModeTests` | 12/12 |
| `CSBOOT0101PlayModeTests` | 1/1 |

같은 어셈블리의 기존 시험이 함께 통과했으므로 회귀는 없다. 다만 이 통과는 **구현된 범위만** 증명한다 — 기술자 인계 주기와 지속 상태 이월은 시험 대상 자체가 없다.

## 5. G9 PASS까지 남은 것

아래 네 가지가 채워지기 전에는 G9를 PASS로 판정하지 않는다.

**(1) 기술자 인계 주기 — 구현함 (2026-09-23), 다만 몸이 없다.** `App/Fps/Work/TechnicianDispatch.cs`가 `RECEIVED → TRAVELLING → WORKING → COMPLETED` 상태 기계를 만들고, `issue-repair-order` 효과가 `Request()`를 부른다. 완료 시 설비의 결함이 해소되고 대체 고유번호가 부여된다. 절차 v2의 `witness-replacement` 조건부 단계가 플레이어의 결과 확인을 받는다.

진행은 `Tick(seconds)`로만 일어난다 — 기존 규율("시계는 플레이어가 쥔다")을 지켜 시간 초과 실패 조건이 없고, `DriveHandoffWithFrameTime=false`로 두면 시험이 결정론적으로 구동한다. `witness-replacement`를 `conditional`로 둔 덕분에 기술자를 기다리는 동안 점검표 부착 같은 다른 단계를 진행할 수 있다(시험 `기다리는_동안_다른_단계를_진행할_수_있다`).

**남은 것:** 기술자에게 아직 **몸이 없다.** 상태 기계와 월드 상태 변화만 있고, 실제로 걸어오는 NPC 캐릭터·애니메이션·현장 표현이 없다. 1인칭으로 "도착하는 것이 보이는가"는 미검증이며, `PRODUCTION_GAME_BENCHMARK.md`의 Firefighting Simulator 항목이 요구하는 관찰 가능성은 절반만 충족한 상태다.

**정정 (2026-09-24):** 앞서 "대기 중 점검할 두 번째 소화기가 없다"고 적었으나 사실과 다르다. `FpsStation.unity`에 `FacilityInspectable`이 **12개** 배치되어 있다(#237). 실제 제약은 `TutorialSession.Target`이 단수 필드이고 `FireExtinguisherSliceBuilder.Placement.TutorialTarget` 주석이 "절차 세션이 물릴 대상. 정확히 하나여야 한다"로 못박은 것이다. 즉 설비가 없는 게 아니라 **세션이 하나만 다룬다.** 같은 날 발견한 또 하나: `TechnicianDispatch`가 씬 생성기에 배선되어 있지 않아, 시험은 통과해도 실제 플레이에서는 `Dispatch`가 null이라 인계가 일어나지 않았다. 생성기에 부착 코드를 추가해 해소했다.

**(2) 지속 상태 이월 — 구현함 (2026-09-24), 다만 받을 본편이 없다.** `App/Fps/Work/TutorialCarryoverStore.cs`가 설비별 결과를 JSON 파일로 남긴다. 기록 시점은 `Audit()` 한 곳뿐이고, 이월하는 것은 "월드가 실제로 어떻게 됐는가"(교체 여부)뿐이다 — 플레이어가 기재한 판정과 점검표는 이번 회차에 다시 해야 하므로 되살리지 않는다. `ApplyCarryoverOnStart`가 켜져 있으면 `Start()`에서 반영한다. 영수증 [`state/evidence/2026-09-24-carryover.json`](state/evidence/2026-09-24-carryover.json), PlayMode 53/53.

`ChooGuard.Persistence`는 쓰지 않았다. `SqliteProvider`가 "Currently macOS only; other ABIs fail closed"이고 `SqliteRunStore`는 `ICommitMaterializer`·readSet 검증을 요구하는 운영 커밋 저장소라 목적이 다르다. ECC 리뷰가 이 판단을 타당하다고 확인했다.

**남은 것:** 이월이 연결해야 할 **본편(비상) 계층이 아직 없다.** 따라서 검증된 것은 튜토리얼 회차 사이의 이월이고, "점검하지 않은 소화기가 본편 비상 상황에서 비어 있다"의 본편 쪽 절반은 미검증이다.

**(3) 절차가 1종뿐이다.** `Art/Procedures/`에 파일이 하나다. `single_device_tutorial` 판정에 따라 하나로 시작하는 것은 맞으나, 계층이라 부르려면 두 번째 설비에서 같은 구조가 재사용되는지 확인해야 한다.

**(4) 자동 시험을 넘어선 검수가 없다.** ~~시험 실행 결과가 없다.~~ 2026-09-23 실행으로 해소했다(32/32, 4절 참조). 다만 통과한 것은 코드 경로이며, 사람이 1인칭으로 한 번 끝까지 플레이한 검수·프레임 캡처는 아직 없다. G9의 "completion is legible" 요건은 자동 시험으로 증명되지 않는다.

## 6. 다음 작업 순서

1. ~~시험 2종을 실행해 영수증을 남긴다.~~ **완료 (2026-09-23, 32/32)**
2. ~~기술자 NPC 인계 주기를 구현한다.~~ **상태 기계 완료 (2026-09-23, 45/45)** — 영수증 [`state/evidence/2026-09-23-technician-handoff.json`](state/evidence/2026-09-23-technician-handoff.json)
3. ~~지속 상태 이월을 구현한다.~~ **완료 (2026-09-24, 53/53)** — 본편이 읽는 쪽은 본편이 생긴 뒤에 연결한다.
4. **`TutorialSession`을 다중 대상으로 만든다. (다음 최우선)** 소화기 12개가 이미 배치돼 있으나 `Target`이 단수라 세션·인계가 1개에만 걸린다. 이것만 되면 "기다리는 동안 다른 소화기 점검"이 시험이 아니라 실제 플레이에서 성립한다. `.planning/2026-09-23-technician-handoff/findings.md`의 A/B안 참조 — `one_each_hoisted` Jev 판정 재검토를 동반한다.
5. 기술자에게 몸을 준다 — 걸어오는 NPC와 현장 작업 표현. 1인칭으로 도착이 보여야 한다.
6. 사람 플레이 검수를 한 번 수행하고 캡처를 남긴다.

## 7. 현재 판정

**G9: NOT_EVALUATED → 재평가 대상.** 절차·실행기·세션·시험이 모두 존재하므로 "설계 문서 없음"은 이 문서로 해소된다. 다만 5절의 (1)과 (2)가 확정된 역할 경계와 `layer_coupling` 요건을 아직 만족하지 않으므로 PASS가 아니다.
