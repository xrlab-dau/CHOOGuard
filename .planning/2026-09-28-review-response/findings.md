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
