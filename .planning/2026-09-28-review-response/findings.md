# Findings: 내가 몰랐던 공통 런타임과, 리뷰가 옳은 이유

## 1. 무엇을 놓쳤나

`cloud/fps-gameplay-20260925`(구현 커밋 `06581714`, Cloud 수정 `5a5d8150`)에 **설계 문서 13종과
구현·검증이 끝난 공통 런타임**이 있다. 나는 `develop` 만 보고 "본편 행동 코드 0건" 이라 판단했다.
**미머지 브랜치를 확인하지 않은 것이 원인이다.**

그쪽의 2026-09-25 체크포인트: PlayMode 39/39, 선택 EditMode 77/78, Node 회귀 25/25,
PPO 4096 step, 100/300/500명 부하(p95 3.30/9.12/21.64ms), Unity Cloud 빌드 접수.

## 2. 정확히 겹치는 것

| 내가 만든 것 | 이미 있던 것 | 차이 |
|---|---|---|
| `StationNavigation` | `GameplayNavigation` (445줄) | 저쪽은 **층·포털·접근성**(`FloorNavigationBinding`·`PortalNavigationBinding`·`NavigationAccess`) |
| `StationNavigationBaker` | `GameplayNavigationBaker` | 저쪽은 frame·digest 결속 |
| `PassengerAgent` | `ActorNavigationBinding` (250줄) | 저쪽은 `HasArrived`·`IsBlocked`·`BlockedReason`·`Velocity`·motor 소유 |
| `TutorialSession.Rewind` | `RoleTrainingProgram` | 저쪽은 **격리 world/physical/NPC snapshot 복원** |

내 것은 **단층·대합실 한 곳**이다. 이미 있는 것의 열등한 부분집합이다.

## 3. 설계가 내 접근을 직접 금지한다

`DESIGN.md` 에서 그대로 인용되는 경계들이다.

1. **"기존 DotRecast를 확장. 새 Unity NavMesh와 이중 관리하지 않는다"**(§3 표)
   → 나는 두 번째 질의/베이크 체계를 만들었다.
2. **`ActionExecutor` 금지: "애니메이션/타이머 종료 = 작업 성공"**(§4.1)
   → `TechnicianDispatch.Tick → Complete → ApplyReplacement` 가 정확히 이것이다.
3. **불변조건 3 "수신 ≠ 수락 ≠ 도착 ≠ 인계"**, **불변조건 4 "요청 수락 ≠ 행동 완료"**
   → 내 인계는 시간만 지나면 완료된다. `TechnicianPresence` 의 Lerp 는 표현일 뿐 권한 문제를 못 푼다.
4. **"단일 위치 소유자"**(§4.2)
   → `PassengerAgent` 가 `transform` 을 직접 쓴다.
5. **"모델 담당이 소유한 Assets/씬은 포괄 쓰기 허가가 아니다"**(EXECUTION_PLAN §1)
   → 내 생성기가 `FpsStation.unity` 를 통째로 다시 쓴다(#240 에서 ±20,186줄).

## 4. 순서도 어긋났다

`P05`(네 직무 튜토리얼)의 선행은 **P03+P08** 이고 **"튜토리얼 전용 NPC 실행기를 따로 만들지
않는다"** 고 명시돼 있다. 나는 P01(공통 계약)·P02(실행기·예약·기록) 없이 P05 와 P06 언저리를
각자 구현했다.

## 5. 리뷰가 찾은 실제 버그 — 내가 놓친 것

> 요청 `(16,4.5,2)` → 반환 끝점 `(16,3.08281231,2)` · ARRIVED 계산 true · 요청점과 **1.41718769m** 차이

두 겹이다.
- `StationNavigation.TryPlan` 이 `SnapExtent=(1,2,1)` 안에서 끝점을 보정하고 **원래 목표와의 오차를
  검사하지 않는다.**
- `PassengerAgent.Tick` 이 `Destination` 이 아니라 **마지막 corner 까지의 수평 거리**로 완료를 판정한다.

내가 "25.1m 걸어 도착" 이라고 보고한 실행도 같은 구멍을 지났다. 이동 거리와 직선 거리는 따로
단언했지만 **요청 목표와의 오차는 한 번도 재지 않았다.**

## 6. 내 근거가 낡은 것

`SqliteProvider` 는 이제 **macOS/Windows x64 로딩을 지원**한다. 내가 별도 JSON 이월을 만들며
근거로 삼은 "macOS 전용" 주석은 더 이상 사실이 아니다. Windows Player 수용이 끝났다는 뜻은 아니다.

## 7. 살릴 수 있다고 리뷰가 명시한 것

판정의 명시적 선택·유닛별 분리, 점검표 방향/재질/조준, 실제 입력·진입 경로, 씬 콜라이더 기반
배치 검증. 그리고 61/61·75/75·NOT_VERIFIED 로 한계를 남긴 기록은 **유효한 과거 증거로 존중**한다고 했다.

## 8. 현재 PR 구성 (재분할 근거)

| PR | 파일 | 비고 |
|---|---|---|
| #238 | **712** | 그중 **683개가 `graphify-out/`** — 기능 파일은 **29개** |
| #240 | 68 | `FpsStation.unity` 단독 **±20,186줄** |

---

## 9. 설계 문서를 읽고 확인한 것 (2026-09-28)

`INTERACTION_TUTORIAL.md` 전문과 `DESIGN.md`·`EXECUTION_PLAN.md`(§0·P01~P08)를 읽었다.

### 내 튜토리얼의 실제 위치

§5.4 **역무 체인의 1~4단계**다. 그 체인은 7단계이고 역무는 **네 직무 중 하나**다.
없는 것: 5~7단계(안내 요청·통로 확보·출발역→승무→도착역 인계)와 정비·청소·승무서비스.

### 설계가 내 판단을 지지한 것

- §5.4-2 "부식이 있는데 적합으로 기록하는 실수는 기록될 수 있으며, **world truth의 부식을 지우지 않는다**"
  → 내 `MisjudgementCount`·`ShouldBeUnfit` 구조와 같다.
- §5.4-3 "**적합/부적합 어느 판정을 기록했든** 실물 점검표를 붙인다"
  → 내 절차가 판정과 무관하게 `attach-tag` 를 요구하는 것과 같다.
- §1 표 "출처 공백 유지" → 별지 제7호 서식 공백 표시를 유지한 것과 같다.

### 설계가 내 구현을 직접 지목한 것

- §7 "현재 `TutorialSession.Rewind()`/`ProcedureRunner.Rewind()`는 Done와 의존 단계만 푼다.
  **태그·기록·소모품·물리 상태를 복원하는 기능이라고 표시하면 안 된다**"
  → 내가 `RevertEffect` 로 일부 되돌린 것은 방향이 맞지만 범위가 한참 모자란다.
- §5.4-3 "**태그를 손에 들거나** 다른 설비에 붙인 것은 완료가 아니다"
  → 내 `attach-tag` 는 효과로 즉시 부착한다. custody 이전이 없다.
- §4 "`conditional=true` 는 guard 가 UNKNOWN/CONFLICTED 여도 건너뛴다. 확장에서는
  **적용 FALSE만 근거 있는 비해당**이며 UNKNOWN/CONFLICTED 는 판정 보류이다"
  → 내 v3 도 여전히 건너뛴다. 고쳐야 한다.
- §8 "현재 하드코딩 입력은 **Input System action map 으로 이행**"
  → 내 `TutorialInput` 은 `Keyboard.current` 를 직접 읽는다.

### 요약

살릴 것과 버릴 것이 **문서로 확정**됐다. 더 이상 추측으로 이식 지점을 고르지 않는다.
상세는 `task_plan.md` §D.

---

## 10. `NPC_SCENARIO.md` 를 읽고 (2026-09-28)

### 내 승객은 설계가 금지하는 형태다

§1.1 이 요구하는 것은 `관측 → 자기 목표 형성 → 계획 → 행동 → 결과 관측 → 재계획` 을 **스스로
도는** 에이전트다.

> "플레이어가 말을 걸거나 사건 director 가 호출해야만 움직이는 구조가 아니다"
> "대사/플레이어 버튼을 기다리는 **반응형 인형으로 축소하지 않는다**"

내 `PassengerAgent` 는 `StartDestination` 을 받아 한 번 걷고 끝난다. **정확히 그 반응형 인형이다.**
"이미 있는 것의 부분집합" 이라는 내 앞선 진단보다 이쪽이 정확하다.

### 인계 4단계는 6단계로 대체된다

설계 §5: `REQUESTED → ACKNOWLEDGED → ACCEPTED → EN_ROUTE → ONSITE → HANDOFF_ACCEPTED`.

- `ACKNOWLEDGED` = 수신, `ACCEPTED` = **맡겠다는 약속**, 마지막이 **책임 이전**.
- "이전 담당자가 인계 전에 이탈하면 **미인계가 남는다**."

내 `RECEIVED→TRAVELLING→WORKING→COMPLETED` 에는 수락도 책임 이전도 없다.
그리고 §10 실패표가 내 구현을 그대로 금지 사례로 적었다.

> 무전은 수신했지만 담당자가 도착하지 않음 → 기존 담당자 책임 유지 / **금지: ACK 를 인계 완료로 표시**

### 막힘 처리도 다르다

§5·§6 은 막힘을 **`plan revision` + `막힌 이유` + `재개 조건`** 으로 남기고 목표를 자동
삭제하지 말라고 한다. 같은 실패를 매 프레임 재질문하지도 말라고 한다.
내 `STUCK` 은 상태만 바꾸고 멈춘다.

### 규모를 잘못 알고 있었다

- **300명 동시 재판단 = dispatch 에만 최소 25초** (초당 12회 상한, `/future/step` 과 합산).
- "모든 시민 2초 내 재판단" 은 불가능. 2초는 admission 된 foreground **단일** 판단 목표다.
- 즉시 반응은 **local rule** 로 하고 provenance 를 구별해 기록한다.
- 비용 **$3.63/시간** (최대 지속 부하 가정, JEV 입력만).

승객 NPC 는 "캡슐을 걷게 하는 일" 이 아니라 **P06 + P07 + P08** 이다. 이동은 P04 의 한 조각이다.

### 새로 드러난 결정 사항

`DESIGN.md` §7 이 **"이 문서는 과금 호출·부하 실험을 승인하지 않는다"** 고 못박는다.
내가 P07/P08 에 손댄다면 **원격 추론 호출의 승인 경계**를 먼저 정해야 한다.
`task_plan.md` §F 에 5번으로 추가했다.
