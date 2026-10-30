# Findings

## 환경
- Unity 6000.3.23f1 설치됨 = 프로젝트 버전 일치. ULF 라이선스 `StopDate 2026-11-11`(10-13 갱신) — 유효.
- 저장소: `C:\CHOOGuard`(경로 260자 한계 때문에 루트에 둠, `core.longpaths=true`). 브랜치 `wheelchair-aed-local` = #310 head `b34fd6470`.
- `Library` 는 구 `Choo` 에서 이동해 옴(Artifacts 18,904개, ArtifactDB 10-13) → 첫 실행이 전체 재임포트가 아님.
- 씬 `FpsStation.unity` 는 Git LFS(152MB). 대합실 벤치 28뱅크(`MAIN2F_R31_PUBLIC_WAITING_BENCH_WB01~28`)·휠체어 `MAIN2F_STATION_OFFICE_WHEELCHAIR_20261023`·AED `MAIN2F_RAILWAY_POLICE_AED_20261025` 모두 씬 본문에서 확인.

## Phase 1 — 정리 대상 판별 (구 `C:\Users\박효원\Choo`)
살린 것 → `C:\Users\박효원\Choo-keep\` (118MB, 361파일)
- `stash/stash0-ci-workflow-edits.patch`(335줄, CI/워크플로 로컬 수정 — 상류보다 오래된 내용이라 사실상 폐기 후보), `stash1-navmesh-measure.patch`(16줄)
- `branches/local-branches.bundle`(63KB): `collect/pre265`(고유 2커밋), `practice/family-drill-prototype`(고유 9커밋 — #270/#272 측정·연습 프로토타입)
- `uncommitted/ExtinguisherPlacementProbe.cs(+meta)` — 사용자 지시로 커밋 안 한 측정 도구
- `uncommitted/practice-drill-prototype/` — `PracticeDrill.cs`, `PracticeDrillStageTests.cs`, 실행 기록 json, `SOURCE-COMMIT.txt` (아래 Phase 3 핵심 참고)
- `untracked/local-unique-153/` — 상류와 내용이 다른 로컬 파일 153개
- `untracked/not-upstream/` — 상류에 없는 미추적 199개(101MB; workers/twin, source-evidence/retained-work-originals, docs 일부, asset-library/research-public, .planning/2026-10-05-practice-drill, .claude/settings.json)
버릴 것
- `.checkpoints/checkpoint-1/project/` 전체 사본 30,330개 — 상류 `CHECKPOINTS.md` 가 가리키는 체크포인트 1 의 로컬 사본(#295 에서 산출물 정리됨). 상류에 없음 = 로컬 임시물.
- `.git` 6.2GB(모든 커밋은 origin 또는 번들에 있음), 추적 파일 전부(새 클론에 있음), `Library`(이미 이동).
- 홈의 `C:\Users\박효원\.git`(Administrators 소유, 내용 거의 없음) — git 이 홈 아래에서 엉뚱한 저장소를 잡게 만드는 원인.
메모리의 `.planning/2026-09-28-extinguisher-replacement/` 는 이미 로컬에 없음(메모리 기록이 낡음).

## Phase 2 — 스크린샷 경로
- 기존 `Editor/Twin/InteriorReferenceViews.cs` 는 `SceneView.lastActiveSceneView` 필수 → batchmode 불가. `ElectricPlazaViews` 는 전기실 고정.
- CI 패턴(`.github/scripts/unity_ci.sh`): `Unity -batchmode -projectPath … -executeMethod ChooGuard.Editor.X.Y -cg… -logFile …`.
- 새 도구 `Assets/ChooGuard/Editor/Twin/EquipmentSnapshot.cs`(미추적): 씬을 읽기 전용으로 열고 이름으로 객체를 찾아 Renderer 경계 기준 close/context/high 3컷 PNG + `manifest.json`. 저장 안 함.
- 출력: `.planning/2026-10-30-cardiac-arrest-solo-scenario/snapshots/`.

## Phase 3 — 심정지 단독 시나리오 구조
### 하드룰 (`.agents/rules/jev-emergency-composition.md`)
- 비상상황은 **JEV 가 합성**한다. 사건 목록·시나리오 ID·대본 금지. 종류·장소·시각·개수 고정 가정 금지.
- JEV 없으면(`TYPESAFE_API_KEY` 없음/off) `IncidentDirector.Compose()` 가 **후보조차 만들지 않는다** (`Compose.cs:111`).
- 한 근무에 독립 원인 1건(2026-10-08 승인). 2026-10-16 이후 provider/Seeded 경로가 초기 조건을 1회 생성하고 로컬 인과 커널로 전개(`workers/simulator/docs/approved-play/`, `docs/current-station-play.md`).
### 심정지에 해당하는 것
- 고정 "심정지" 종류는 없다. `IncidentDirector.Casualty.cs` 의 `Condition ChestPain`(Kind `chest_pain`, Levels: 가슴 답답→식은땀→실신→의식 없음(숨 쉼)→…) 이 가장 가깝고, `Faint`(`collapse`) 도 레벨 3~4 가 의식 없음. 심정지 = **ChestPain/Faint 의 상위 레벨 전개**.
- 원인 후보 생성: `CasualtyOrigins(roster)` — `ChestPain` 은 `p.Current != InTrain || p.Elderly` 인 승객마다 후보(`Casualty.cs:84-91`). 계열 조립: `IncidentDirector.cs:380-389 OriginSteps` (Fire→Casualty→Security→Train→Facility→Equipment).
- 전개·치료: `CasualtyDevelopments()`, `ChangeCondition`, `AedSetDown(Vector3)`(AED 를 환자 곁에 두면 기록·인계 시 "AED 포함"), `CasualtyRadio()`. AED 상호작용은 이미 존재(#310 의 `Aed.cs` 는 모델·배치).
### 단독 발생을 만드는 사내 선례
- **`PracticeDrill.cs`(구 브랜치 `practice/family-drill-prototype`, 커밋 `3c3a21cd7`, #270 B 방식)**: 에디터 전용. JEV 끈 무대에서 디렉터의 private `Origins`/`Execute`/`Know` 를 리플렉션으로 불러 "지정 계열(`CG_PRACTICE_FAMILY`, Kind 필터)의 후보가 오르면 **한 건만 Execute**", 전후 위험 목록 차이만 인지, 그 건의 보고 선택지만 무전, `Hazard.OfficeReply` 를 받아 적음. 승객 정착 대기 `CG_PRACTICE_SETTLE`(30초). 시험 `PracticeDrillStageTests`(8/8). **develop 미승격 — #270 설계 판단 대기**. 경계 리트머스(Tutorial↔Emergency 상호 참조 금지) 때문에 리플렉션 사용; 승인되면 공개 호출 3곳으로 승격.
- `EmergencyForceAll`(제품 내 존재, 전부 강제) 과 달리 한 건만.
- #283(`feat/instant-first-emergency`, 열림·충돌): 첫 원인을 위험률 시계 없이 JEV 첫 판단 즉시 — 시각만 바꿈. 단독 시나리오엔 불필요(강제 Execute 가 시각을 정함).
- 튜토리얼 계층: `TutorialCatalog`(Id/Title/SceneName 로 카테고리 추가, 메뉴 분기 불필요), `CrewInspectionSession`(오프라인 authored 연습, JEV 불필요) — "훈련" 모드의 공식 자리.
### 설계 선택지 (사용자 결정 필요)
A. **PracticeDrill 승격·좁히기** — 계열 필터를 `chest_pain`(+`collapse`) 로 한정, 실제 승객 한 명에서 후보가 나오는 즉시 1건 Execute, JEV 꺼진 상태 → 다른 사건 0건 보장. 하드룰과 양립(목록이 아니라 "지금 세계 후보 중 필터"). 변경: Editor 도구 → 제품 모드로 승격 시 리트머스 완화 결정 필요.
B. **튜토리얼 카테고리 "응급환자 대응"** — `CrewTrainTutorial` 처럼 authored 오프라인 씬. 하드룰 영향 없음(튜토리얼은 Emergency 미참조)이나 AED·구급 인계 로직을 다시 써야 함(중복).
C. **JEV 켠 채 Casualty 외 계열 차단** — 하드룰 위반 소지(종류 고정) + JEV 과금. 비권장.
권장: A. 다음 세션 구현 단계는 task_plan 참조.

## 수용 기준 점검 (2026-10-31, stage6 즉시형 · stage7 악화형 기준)
| 기준 | 결과 | 근거 |
|---|---|---|
| (a) 한 근무에 위험 1건, Kind chest_pain, 시작 레벨 ≥ 3 | 충족 | drill-191946(4단계), drill-193408(3→4단계), guidance 1건 단정 통과 |
| (b) JEV 요청 0건 | 충족 | `TYPESAFE_API_KEY=off`, stage6/7 로그에 JEV 호출 없음(Begin 서명만) |
| (c) AED 설치 → 인계 로그 "AED 포함" | **배치 미검증** | 하네스는 역무원 행동을 대신하지 않음(AedAt=null). 사람이 에디터에서 플레이해 확인해야 함 |
| (d) 다른 계열 위험 0건 | 충족 | guidance.Count==1 단정, 타임라인에 다른 사건 없음 |
| (e) 시험: 기존 통과 유지 + 심정지 2케이스 | 부분 | `PracticeDrillStageTests` 는 1개 시험(환경변수로 케이스 분기) — 즉시형·악화형 각 1회 통과. 10-5 프로토타입의 "8/8" 은 당시 다른 측정 시험 집계라 이 브랜치엔 없음 |

추가로 실증된 것: 자동 인계 경로(구급대장 도착 → 업무·인계 항목 검토 → 재인계 → 근무 종료, stage6 근무 242초), 구역 필터로 대합실 한정, 역무원 시점 촬영 2편(`film/take1.mp4` 남측 게이트, `film/take2.mp4` 대합실).
남은 수동 확인: 메뉴 `ChooGuard/Practice/심정지 환자 한 건` 경로(편집 모드 → Bootstrap 재생 → 타이틀에서 플레이 시작)와 AED 가져다 두기(기준 c).

## ECC 리뷰 (2026-10-31, 커밋 전)
- 일반 리뷰(code-reviewer) 지적 2건:
  1. [MEDIUM] 심정지 계열이 크기(1.0/.75)·악화 시각(타이머)을 코드로 고정 → 하드룰의 "사건군 선택→매개변수 추첨" 금지 패턴과 형태가 같음. 종류는 여전히 살아 있는 후보에서 고르고, 에디터 전용 어셈블리(`ChooGuard.Editor`, Editor 플랫폼만)라 제품 경로에는 영향 없음. **처리: 코드 변경 없음, 에디터 훈련 도구를 하드룰 예외로 둘지 #270 PM 결정 항목에 추가.**
  2. [MEDIUM] 시험이 "이름상 최신" 기록 파일을 읽어 하네스 저장 전 무대가 끝나면 이전 실행 파일을 읽을 수 있음. **처리: 시작 전 파일 집합과의 차이로 "이번 실행이 쓴 파일 1개"를 집도록 수정(`NewDrill`).** 무대 기본 150초 = 악화형 최악 경로(30+60+60)와 같으므로 악화형은 `CG_PRACTICE_WAIT` 를 180 이상으로 주는 것을 실행 명령에 명시.
  - 확인된 것: 제품 어셈블리·CI 게이트 누출 없음(시험은 `[Explicit]`, CI 필터는 RouteGraphTests 만), `Recorder` private→internal 은 같은 시험 어셈블리 내부.
- C# 리뷰(csharp-reviewer) 1차는 응답 없이 실패 → 범위 좁혀 재실행.
- C# 리뷰(csharp-reviewer 2차) 8건 처리:
  1. ZoneId null → **오탐**(`StationWorld.ZoneId` 는 "" 폴백, `IsOutdoorZone("")`=true 로 실외 처리).
  2. RadioGroupProviders null 항목 → 제품 코드(`EmergencySession.cs:520`)도 같은 가정으로 순회. 미반영.
  3. 무장 후 첫 update 전에 역 씬이 뜨는 경쟁 → 메뉴 경로는 Bootstrap(타이틀)에서 시작하므로 역 씬은 사용자가 고른 뒤 로드됨. 이미 떠 있으면 `EnsureInstalled` 가 오류로 알림. 미반영, 기록.
  4. 재생 중 도메인 리로드 시 유예 seam 원복 누락 가능 → 알려진 한계로 기록(연습 중 스크립트 재컴파일 금지). 미반영.
  5. `OpenScene` 결과 미확인 → **반영**(`IsValid()` 아니면 무장하지 않고 오류).
  6. `Onset` 환경변수를 여러 번 읽음 → **반영**(`Drill` 에서 한 번 스냅샷, `Magnitude` 에 인자로).
  7. 후보 대기는 실시간, 악화는 근무 시각 → 알려진 것(실행 명령에 WAIT≥180 명시). 미반영.
  8. `NewDrill` 이 정확히 1개를 단정 → 의도(불명확한 상태를 조용히 넘기지 않음). 미반영.
