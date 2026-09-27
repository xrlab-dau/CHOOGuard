# Progress Log

## Session: 2026-09-28

### Current Status
- **Phase:** 계획 수립 완료. **코드는 건드리지 않음.**
- PR #238·#240 모두 `CHANGES_REQUESTED` (umyunsang, 2026-09-25).
- `develop` 이동 없음. 내 브랜치 8 앞.

### 한 일
- 팀 깃 확인 중 새 원격 브랜치 `cloud/fps-gameplay-20260925` 발견.
- 리뷰 2건 전문과 설계 문서 `design/fps-ai-20260925/` 의 `DESIGN.md` 전체,
  `EXECUTION_PLAN.md` §0·P01~P08 을 읽음.
- 공통 런타임(`GameplayNavigation` 445줄 · `ActorNavigationBinding` 250줄 ·
  `GameplayNavigationBaker` · `RoleTrainingProgram`)의 공개면 확인.
- 재분할 계획 작성 — `task_plan.md` 의 A(분리)·B(폐기·이식)·C(재작업)·D(살리기)·E(순서)·F(PM 결정).

### 확인한 수치
| | |
|---|---|
| #238 | 712 파일 · 그중 `graphify-out/` **683** · 기능 파일 **29** |
| #240 | 68 파일 · `FpsStation.unity` 단독 **±20,186줄** |

### 내가 틀린 것
1. **미머지 브랜치를 확인하지 않았다.** `develop` 만 보고 "본편 행동 코드 0건" 이라 판단해
   이미 있는 공통 런타임의 열등한 부분집합을 하루 걸려 만들었다.
2. **끝점 보정에 오차 검사를 넣지 않았다.** 리뷰가 요청점과 **1.417m** 차이로 ARRIVED 가 되는 것을
   재현했다. 내 "25.1m 걸어 도착" 보고도 같은 구멍을 지났다.
3. **낡은 근거를 계속 썼다.** `SqliteProvider` 는 Windows x64 를 지원하는데 "macOS 전용" 주석을
   근거로 별도 JSON 이월을 만들었다.
4. **Graphify 683파일 분리를 미뤘다.** 내가 열어둔 결정 사항이었는데 정리하지 않고 넘어갔다.

### Next
`task_plan.md` §F 의 네 가지가 정해져야 B·C 를 시작할 수 있다.
A 묶음(graphify 분리 · 도구 분리 · 문서 정리)은 선행 없이 지금 가능하다.

## 2026-09-28 (이어서) — 설계 문서 정독

### 한 일
- `DESIGN.md` 전문, `EXECUTION_PLAN.md` §0·P01~P08, **`INTERACTION_TUTORIAL.md` 전문** 읽음.
- `task_plan.md` §D 를 **이식 지점 표**로 다시 씀 — D0(위치)·D1(그대로 살림)·D2(고쳐서 살림)·
  D3(버림)·D4(되감기 추가 요구).
- 공통 런타임의 끝점 오차 검사 확인 — `DetourNavigationSurface` 가 보정 후 수평·수직 오차를
  따로 재측정하고 초과 시 `endpoint_outside_walkable_surface` 로 거부한다.
  허용치도 내 것보다 엄격하다(수평 0.3/수직 0.5 대 내 1/2, 도착 반경 0.04~0.2 대 0.6).
  경로가 끝났는데 미도달이면 `Block("arrival_not_observed")` 로 막는다.
  → **PR #240 댓글에 적은 "없으면 추가하겠다" 는 불필요하다. 이미 있고 더 엄격하다.**

### 이식 지점 요약
| | |
|---|---|
| 그대로 | 판정 명시 선택 · 오판 기록 · 적합이어도 태그 부착 · 부착점 · 근거 공백 표시 · 진입 |
| 고쳐서 | 태그 custody 이전 · `conditional` 의 UNKNOWN 보류 · Input System action map · 단말 확인 분리 |
| 버림 | nav/승객 일체 · 인계 시간 완료 · 기술자 Lerp · 이월 JSON |

### 아직 막혀 있는 것
`task_plan.md` §F 의 네 가지(특히 **cloud 브랜치 머지 순서**). 그것이 정해져야 B·C 착수 가능.

### Next
PM 답변 대기. 답이 오면 §D 표대로 착수한다.
