# Task Plan: PR #238·#240 리뷰 대응과 재분할

## Goal
`CHANGES_REQUESTED` 두 건을 해소한다. 지적을 반박하지 않는다 — **대부분 옳고, 내가 공통 런타임을
모른 채 두 번째 체계를 만든 것이 근본 원인**이다. 살릴 것만 골라 작은 PR 로 다시 낸다.

## Next Step
아래 A 묶음(기능과 무관한 분리)부터. B·C 는 선행 결정이 필요하다(§5).

## Current Phase
계획 수립 — 코드는 아직 건드리지 않음

## 원칙

1. **버리는 것을 아까워하지 않는다.** 중복 구현을 고쳐 쓰면 "두 권한 체계" 가 남는다.
2. **리뷰가 이미 고쳤다고 인정한 것을 다시 미구현으로 취급하지 않는다.** 반대로 내가 고쳤다고
   주장한 것도 근거가 있어야 한다.
3. **모델링 소유 자산을 건드리지 않는다.** 씬 전체 재생성은 어떤 묶음에도 넣지 않는다.
4. **작게 낸다.** 한 PR 이 하나의 이유만 갖는다.

---

## A. 기능과 무관한 분리 — 지금 할 수 있다

| # | 내용 | 근거 |
|---|---|---|
| A1 | **`graphify-out/` 683파일·116만 줄을 #238 에서 뺀다.** 별도 chore PR 로 내거나 되돌린다 | 리뷰 "기능 PR 에서 빼고 별도 chore PR 로 분리". 빼면 #238 의 기능 파일은 **29개** |
| A2 | **`StationWalkableSolver` 를 독립 도구 PR 로.** 오프라인 분석·배치 검증만. 씬 자동 덮어쓰기 없음. geometry 판본과 한계 명시 | 리뷰 "독립 도구 변경으로 분리하고 geometry 판본·한계를 명시" |
| A3 | **문서·영수증 PR.** `MISSION_DESIGN`·`BENCHMARK_FPS_MATRIX`·`PLAY_REVIEW_CHECKLIST`·영수증 5종. 최신 설계 정본(`design/fps-ai-20260925/`)과 **충돌하는 서술을 정리**한다 | 리뷰 "미션 정본 채택 문서도 목적에 맞게 분리하고 최신 설계 정본과 충돌하지 않게" |

A3 에서 반드시 고칠 서술: `MISSION_DESIGN.md` 8절이 "본편 행동 코드 0건" 을 전제로 순서를
정했는데 사실이 아니다. `BENCHMARK_FPS_MATRIX.md` 의 미구현 집계도 같은 전제였다.

---

## B. 폐기하고 이식 — 공통 런타임과 중복

| # | 버릴 것 | 이식 대상 |
|---|---|---|
| B1 | `StationNavigation`·`StationNavigationBaker`·`PassengerAgent`·`Settings/StationNavigation/Concourse-v1.bytes` | `GameplayNavigation`·`GameplayNavigationBaker`·`ActorNavigationBinding` |
| B2 | `PassengerBuilder` | 공통 actor registry 의 배치 경로 |
| B3 | `TechnicianPresence` | 공통 actor 가 **실제 도착·작업한 결과**를 표현하도록 |

**도착 판정 버그는 이식으로 해소된다.** `ActorNavigationBinding` 은 `ArrivalDistance`·`IsBlocked`·
`BlockedReason` 을 이미 가진다. 다만 이식할 때 **요청 목표와 보정 끝점의 오차 검사**가 그쪽에도
있는지 확인한다 — 없으면 그때 그쪽에 추가한다. 내 코드에서 고치고 버리지 않는다.

가져갈 교훈: **끝점 보정을 허용하면 보정량을 반드시 재고 허용 오차를 명시해야 한다.**

---

## C. 설계 경계에 맞춰 재작업

| # | 지적 | 대응 |
|---|---|---|
| C1 | 시간 경과가 실제 교체를 대신한다 | `TechnicianDispatch` 를 P02 공통 executor 의 `REQUESTED→RESERVED→APPROACHING→EXECUTING→VERIFYING→COMPLETED` 에 얹고, **실제 완료 증거가 있을 때만** `ApplyReplacement`. 상태 기계 자체는 유지 가능 |
| C2 | 되감기가 새 부작용을 되돌리지 않는다 | `TutorialSession.Rewind` 를 `RoleTrainingProgram` 의 격리 snapshot 복원으로 이행. 대상·소모품·태그·관측·NPC 목표/계획·작업·generation 포함 |
| C3 | 저장/이월의 상태 소유권 | 별도 JSON 폐기. 공통 의미 상태·run/generation·격리 저장으로. **이관 전에 `SqliteProvider` 를 코드로 확인한다**(아래 C3.1) |
| C4 | 씬 전체 재생성 | `FireExtinguisherSliceBuilder` 가 `FpsStation` 을 다시 쓰지 않게 한다. gameplay overlay/prefab 로 분리하고 대상 자산을 특정 |

---

### C3.1 `SqliteProvider` — 문서 두 개가 어긋났고, 코드가 답이었다

| 출처 | 서술 |
|---|---|
| `OSS_INTEGRATION.md` §10 (2026-09-25) | "현재 SqliteProvider 는 ... **macOS 이외에서 명시적으로 실패**한다" |
| PR #238 리뷰 (2026-09-25) | "현재 `SqliteProvider` 는 **macOS/Windows x64 로딩을 지원**하도록 바뀌었다" |

**둘 다 맞다. 브랜치가 다르다.**

```
develop            : "Currently macOS only; other ABIs fail closed."
                     if (!IsOSPlatform(OSX)) throw PlatformNotSupportedException
cloud/fps-gameplay : "Exact-file native loading for macOS and Windows x64"
                     windows ? ProcessArchitecture != X64 : !IsOSPlatform(OSX) -> throw
```

내가 별도 JSON 이월의 근거로 삼은 "macOS 전용" 은 **당시 `develop` 기준으로는 맞았고 지금은 낡았다.**
`OSS_INTEGRATION` 은 설계 착수 시점을 기록한 문서이고 `EXECUTION_PLAN §0` 이 우선한다고 스스로 밝힌다.

**규율:** 플랫폼 지원·설치 여부는 **문서가 아니라 대상 브랜치의 코드로 확인한다.**
문서는 언제 기준인지 밝히고 있어도 내가 그 시점을 놓치면 같은 실수를 반복한다.

## D. 살리는 것 — 이식 지점 (2026-09-28 설계 문서 확인 후)

`INTERACTION_TUTORIAL.md` 를 읽고 **어디에 어떻게 얹는지**를 확정했다. 추측으로 만들지 않는다.

### D0. 전제 — 내 튜토리얼의 실제 위치

내가 만든 소화기 점검은 **§5.4 역무 체인의 1~4단계**다. 그 체인은 7단계이고, 역무는 **네 직무 중
하나**다(정비·청소·승무서비스·역무). 없는 것은 5~7단계(안내 요청·통로 확보·출발역→승무→도착역
인계 연결)와 나머지 세 직무다.

`EXECUTION_PLAN` P05 는 **"한 직무 완성으로 네 직무 완료를 선언하지 않는다"** 고 못박는다.

### D1. 그대로 살릴 것 — 설계가 같은 것을 요구한다

| 내 구현 | 설계 근거 | 비고 |
|---|---|---|
| 판정을 **플레이어가 명시적으로 선택** (절차 v3 `verdict-selected`) | §5.4-2 "판정을 직접 기재한다" | 그대로 |
| **오판을 기록하되 월드는 안 바뀜** (`MisjudgementCount`, `ShouldBeUnfit` 유지) | §5.4-2 "부식이 있는데 적합으로 기록하는 실수는 기록될 수 있으며, world truth의 부식을 지우지 않는다" · §2-3 | 그대로 |
| **적합이어도 점검표를 붙인다** | §5.4-3 "적합/부적합 어느 판정을 기록했든 실물 점검표를 붙인다" | 그대로 |
| 점검표 **부착점**(`TagAnchor`)과 방향·재질·콜라이더 | §5.4-3 "올바른 설비의 부착점" · §9 interaction anchor | 그대로 |
| 절차 **근거 공백 표시**(별지 제7호 서식 공백) | §1 표 "출처 공백 유지" | 그대로 |
| `SceneEntryPoint` 진입 | — | 충돌 없음 |
| `StationWalkableSolver` | §9 공간 provenance | **A2 로 분리**, 오프라인 도구 |

### D2. 고쳐서 살릴 것

| 내 구현 | 무엇이 틀렸나 | 이식 방법 |
|---|---|---|
| `attach-tag` 가 효과로 **즉시 부착** | §5.4-3 **"태그를 손에 들거나 다른 설비에 붙인 것은 완료가 아니다"** — 가져와서(custody) 붙이는 체인이 필요 | §2 행동 생애 `REQUESTED→RESERVED→APPROACHING→EXECUTING→VERIFYING→COMPLETED` 에 얹고, 태그를 실제 물체로 custody 이전 |
| 절차 `conditional` 이 guard 미충족 시 **건너뜀** | §4 **"적용 FALSE만 근거 있는 비해당이며 UNKNOWN/CONFLICTED는 판정 보류"** — 내 v3 은 UNKNOWN 도 건너뛴다 | `ProcedureRunner` 확장. 러너 교체가 아니라 판정 보류 상태 추가 |
| `TutorialInput` 이 `Keyboard.current` **직접 읽기** | §8 "하드코딩 입력은 **Input System action map 으로 이행**하며 이동/작업/단말 모드를 **배타적으로** 소비" | action map 으로 이행. §8 표의 키 배치 제안을 따름 |
| `close-inspection` 이 `Audit()` 를 직접 호출 | §4 "콘텐츠의 '단말 재스캔' 설명과 실제 단말 완료 행위를 맞추되, **단말 열기 자체를 전체 검사 합격으로 만들지 않는다**" | 단말에서의 실제 확인 행위와 절차 완료를 분리 |
| 다중 대상(`UnitBinding`) | 방향은 맞음 — §4 "대상 instance binding 은 절차 정의와 분리" | P05-1 "single Target → definition/instance 다중 binding" 에 합류 |

### D3. 버릴 것 — 부분집합이거나 설계가 금지하는 형태다

`NPC_SCENARIO.md` 를 읽고 근거를 정확히 고쳤다. "이미 있다" 보다 **"설계가 금지한다"** 가 맞는
항목이 있다.

| 버릴 것 | 근거 |
|---|---|
| `StationNavigation`·`StationNavigationBaker`·`Concourse-v1.bytes` | 공통 런타임의 단층 부분집합. NPC_SCENARIO §8 "기존 DotRecast route 데이터를 유지·확장" · INTERACTION_TUTORIAL §10 "Unity NavMesh 와 이중 소유 금지" · **OSS_INTEGRATION §13 "DotRecast 를 유지하는 동안 병렬 기본 경로를 추가하지 않는다. 교체 시 caller·베이크·경로 오류 의미까지 한 번에 이관"** — 내 것은 두 번째 DotRecast 베이크·질의 체계다 |
| `PassengerAgent`·`PassengerBuilder` | **설계가 금지하는 형태**다. §1.1 "플레이어가 말을 걸거나 사건 director 가 호출해야만 움직이는 구조가 아니다" · "대사/플레이어 버튼을 기다리는 **반응형 인형으로 축소하지 않는다**". 내 승객은 `StartDestination` 을 받아 한 번 걷고 끝난다 |
| `PassengerAgent.STUCK` | §5 "같은 실패를 매 프레임 JEV 에 재질문하지 않는다" · §6 "목표/약속을 자동 완료·삭제하지 않고 **plan revision·막힌 이유·재개 조건**을 남긴다". 내 `STUCK` 은 그냥 멈춘다 |
| `TechnicianDispatch` 의 4단계 | §5 협업 지원은 **6단계** `REQUESTED → ACKNOWLEDGED → ACCEPTED → EN_ROUTE → ONSITE → HANDOFF_ACCEPTED`. 내 것에는 **수락(ACCEPTED)도 책임 이전(HANDOFF_ACCEPTED)도 없다** |
| `TechnicianDispatch` 의 시간 기반 완료 | §10 실패표 — "무전은 수신했지만 담당자가 도착하지 않음 → 기존 담당자 책임 유지 / **금지: ACK 를 인계 완료로 표시**". 내 구현이 그 금지 항목이다 |
| `TechnicianPresence` 의 타이머 Lerp | 표현이 수행 증거를 대체하지 못함 |
| `TutorialCarryoverStore` 별도 JSON | INTERACTION_TUTORIAL §7 저장 경계·run generation 과 충돌 |

### D3.1 승객 NPC 를 다시 만들 때의 실제 범위

내가 "캡슐을 걷게 하는 일" 로 본 것은 설계에서 **P06(욕구·목표·기억) + P07(JEV broker·예산) +
P08(agent loop·대화·협업)** 이다. 이동은 P04 의 한 조각일 뿐이다.

규모가 내 예상과 다르다.

- **300명 동시 재판단은 dispatch 에만 최소 25초.** 상한이 초당 12회이고 `/future/step` 과 **합산**이다.
- "모든 시민이 2초 안에 재판단" 은 **불가능**하다. 2초는 admission 된 foreground 단일 판단 목표다.
- 즉시 반응은 **local rule** 로 처리하고 provenance 를 `local_rule` 로 구별해 기록한다.
- 비용은 최대 지속 부하 가정에서 **$3.63/시간**(JEV 입력만, 대화·worker·네트워크 제외).
- **국소회피가 빠져 있다.** OSS_INTEGRATION §5.2 는 `DotRecast.Detour.Crowd`(같은 2026.3.1 계열)를
  도입 후보로 두고 경로 추종과 국소회피를 같은 계열로 구성하라고 한다. 내 `PassengerAgent` 에는
  회피가 없다. 승객 한 명이라 드러나지 않았을 뿐이다.

### D4. C2(되감기)에 추가된 요구

설계 §7 이 내 `Rewind()` 를 **이름으로 지목**한다 —
"태그·기록·소모품·물리 상태를 복원하는 기능이라고 표시하면 안 된다".

되감기 지점은 **안정된 작업 경계의 snapshot** 이고 저장할 것은:
도구 custody · 부품/체결 · 오염/잔량 · pose/속도 · 관측 · 절차 · 예약 ·
실습 NPC 의 욕구/목표/계획·약속·기억 · 난수 상태.

그리고 **run generation 을 증가**시켜 늦게 도착한 NPC/미래 응답과 가상 branch 를 버린다.
`(runId, generation, actorId, intentId)` 키와 저장 인덱스에 generation 을 포함한다.

## E. 순서

```
A1 ─┐
A2 ─┼─> 즉시 (선행 없음)
A3 ─┘
        ┌─> B1·B2·B3 (cloud 브랜치 머지 후)
        └─> C1·C2·C3 (P01·P02 이후) ─> D (P03 이후, 통합 수용은 P08 이후)
```

설계의 P05 선행이 **P03+P08** 이므로, 내 튜토리얼 작업의 **통합 수용은 그 뒤**다.
지금 당장 머지 가능한 것은 **A 묶음과 D 의 일부**뿐이다.

---

## F. 내가 정할 수 없는 것 — PM 결정 필요

1. **`cloud/fps-gameplay-20260925` 가 `develop` 에 먼저 들어가는가?**
   그것이 선행이면 B·C 는 그 뒤다. 아니면 이식 대상이 움직인다.
2. **#238·#240 을 닫고 다시 쪼갤 것인가, 현 PR 을 축소할 것인가.**
   닫으면 번호·연결 이슈(#239·#241·#242·#243)를 다시 걸어야 한다.
3. **소화기 배치 v4 를 누가 적용하는가.** 좌표는 검증됐지만 씬 반영은 모델링 소유다.
4. **`.claude/` 를 커밋할지 무시할지** — 며칠째 미정.
5. **원격 추론 호출이 내 작업 범위에 들어오는가.**
   설계는 `/npc/decision` 과 `/future/step` 이 **실제 과금**되는 JEV 호출임을 전제한다
   (최대 지속 부하 $3.63/시간, 공유 계정·비용 한도를 실행 전 구성해야 함).
   `DESIGN.md` §7 은 **"이 문서는 과금 호출·부하 실험을 승인하지 않는다"** 고 못박는다.
   내가 P07/P08 에 손댄다면 이 경계를 먼저 정해야 한다.

## 규율 (어기지 않는다)

- **미머지 브랜치를 먼저 확인한다.** 이번 사태의 직접 원인이다. `develop` 만 보고 "없다" 고 하지 않는다.
- **끝점 보정에는 오차 검사를 붙인다.** 보정을 허용하면서 재지 않으면 도착하지 않고 도착했다고 한다.
- **버릴 코드에 시간을 쓰지 않는다.** 이식이 결정되면 그 코드의 버그는 고치지 않고 교훈만 옮긴다.
