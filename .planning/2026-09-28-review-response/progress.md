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
