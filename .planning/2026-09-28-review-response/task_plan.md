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
| C3 | 저장/이월의 상태 소유권 | 별도 JSON 폐기. 공통 의미 상태·run/generation·격리 저장으로. **`SqliteProvider` 는 Windows x64 지원됨** — 내 "macOS 전용" 근거는 낡았다 |
| C4 | 씬 전체 재생성 | `FireExtinguisherSliceBuilder` 가 `FpsStation` 을 다시 쓰지 않게 한다. gameplay overlay/prefab 로 분리하고 대상 자산을 특정 |

---

## D. 살리는 것 (리뷰가 가치 있다고 명시)

- 판정의 **명시적 선택**과 유닛별 분리 (절차 v3 의 `verdict-selected`)
- 점검표 **방향·재질·조준** (Quad 보이는 면 `-Z`, 전용 재질, 콜라이더 복원)
- 실제 **입력·진입 경로** (`TutorialInput` 1·2·F, `SceneEntryPoint`)
- 씬 콜라이더 기반 **배치 검증 결과** (도구로서, A2)
- 절차 콘텐츠와 **현실/공식 규정의 한계 표시**

이것들은 C2 이후 **공통 training 경계에 얹어** 좁은 PR 로 낸다. #238 runtime 의존관계를 명시한다.

---

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

## 규율 (어기지 않는다)

- **미머지 브랜치를 먼저 확인한다.** 이번 사태의 직접 원인이다. `develop` 만 보고 "없다" 고 하지 않는다.
- **끝점 보정에는 오차 검사를 붙인다.** 보정을 허용하면서 재지 않으면 도착하지 않고 도착했다고 한다.
- **버릴 코드에 시간을 쓰지 않는다.** 이식이 결정되면 그 코드의 버그는 고치지 않고 교훈만 옮긴다.
