# 심정지 단독 시나리오 제작 준비 (2026-10-30)

사용자 지시: "필요 없는 거 삭제한 후 맵에 aed랑 휠체어 잘 있는지 스크린샷 찍고 심정지 환자 발생 시나리오(이 사건만 일어나는 거) 제작 준비해"

작업 저장소: `C:\CHOOGuard`, 브랜치 `wheelchair-aed-local` (= #310 head b34fd6470, develop 2ee3b85f4 포함)

## Phase 1 — 정리 (삭제 전 보전 → 삭제 목록 확정 → 사용자 실행)
- [x] 구 `C:\Users\박효원\Choo` 고유 자산 판별: stash 2개, 로컬 전용 브랜치 2개(고유 11커밋), 커밋 안 한 측정 도구, 미추적 30,529개(.checkpoints 사본 30,330 포함), `Library`
- [x] 살릴 것은 `C:\Users\박효원\Choo-keep\`(118MB, 361파일)로, `Library` 는 `C:\CHOOGuard\Library` 로 이동
- [x] 삭제 명령을 사용자에게 제시 — `Remove-Item -LiteralPath 'C:\Users\박효원\Choo' -Recurse -Force` (세션 cwd 가 Choo 라 작업 종료 후 실행)
- [ ] 사용자 실행 확인 (선택: 홈의 `C:\Users\박효원\.git` 도 제거)

## Phase 2 — 스크린샷 (AED·휠체어 확인)
- [x] 라이선스 유효성 확인 — ULF StopDate 2026-11-11 (로그상 그룹 만료 2026-12-07)
- [x] 기존 캡처 경로 조사 → batchmode 불가 → 새 `Editor/Twin/EquipmentSnapshot.cs`(미추적) 작성, `ClearPose` 로 가림 없는 각도 탐색
- [x] 휠체어 close(1.05m) · AED close(1.56m)/context(2.52m) PNG + `manifest.json` (좌표: 휠체어 27.79,7.45,-7.80 / AED -22.61,8.35,-62.05, 둘 다 active, renderer 1)
- [x] PNG 3장 사용자에게 전달
- 미달: wide(10m) 컷은 전 방향 가림 — 대합실 전경이 필요하면 플레이어 시점 캡처(PlayMode)로 별도

## Phase 3 — 심정지 단독 시나리오 제작 준비 (코드 변경 없음, 설계·진입점 확정)
- [x] 발생 경로 추적: `CasualtyOrigins`(Casualty.cs:84) → `OriginSteps`(IncidentDirector.cs:380) → JEV 판단 → `Execute`. 심정지 = `ChestPain`(chest_pain)/`Faint`(collapse) 의 레벨 3~4 전개. AED 는 `AedSetDown` 으로 이미 연결.
- [x] 최소 변경 지점 결정: 하드룰(종류 고정 금지, JEV 없으면 후보 없음) 때문에 "rates=0" 식은 불가. 선례 `PracticeDrill.cs`(#270, 로컬 브랜치, Choo-keep 에 평문 보관)가 정답: JEV 끈 무대 + 계열 필터로 후보 1건만 `Execute`.
- [x] 설계 문서 대조 완료 (findings 참조)
- [x] 제작 단계·수용 기준·시험 방법 정리 (아래)

**결정(2026-10-30): 사용자가 A 선택.** 사건 진행 개요는 [incident-outline.md](incident-outline.md) (0~11장, 코드 근거 포함).

## 구현 준비 상태 (2026-10-30 20:15) — **준비 완료**
- 기준선 실증: `Arm`(질환) + `PracticeDrillStageTests`(픽스처 상속) = Passed 1/1 (518s). 강제 1건·인지·보고·구급 요청까지 기록됨(`.planning/2026-10-05-practice-drill/drill-20261030-110915.json`).
- 남은 선행 결정(PM): 에디터 전용 도구로 둘지 / 제품 훈련 모드(Seeded 런타임 재구축)로 갈지. 전자면 아래 수정 지점만으로 착수 가능.
- 브랜치 `feat/cardiac-arrest-drill` (base `wheelchair-aed-local` = develop 2ee3b85f4 + #310). 프로토타입 cherry-pick `47502ed32`. 컴파일 통과(Tundra 15 items, error 0).
- 리플렉션 대상 현존 확인: `IncidentDirector.cs:343 Execute(Transition,float,int,string)`, `:423 Origins()`, `:527 Know(Hazard,string)`, `:88 all`; `Casualty.cs:291 ChangeCondition(CollapseHazard,int)`; `Transition{Key,Kind,Description,Origin,Levels,Apply}`(`IncidentDirector.cs:233`).
- **정확한 수정 지점 (`Assets/ChooGuard/Editor/PracticeDrill.cs`)**
  - L47 `OutFolder` → `.planning/2026-10-30-cardiac-arrest-solo-scenario/drills`
  - L57-60 `Families` 에 `["심정지"] = new[] { "chest_pain" }` 추가 (collapse 포함 여부는 훈련 범위 결정)
  - L64-65 메뉴 추가 `[MenuItem("ChooGuard/Practice/심정지 환자 한 건")] public static void CardiacArrest() => Run("심정지");`
  - L192-193 크기: `.5f` 고정 → 계열별. 심정지 즉시형 `1.0f`(4단계), 악화형 `.75f`(3단계) + 타이머로 `ChangeCondition(+1)` 리플렉션 호출(새 메서드, `Members` 플래그 재사용)
  - 환자 선택(L174 `FirstOrDefault`) → 거리·층 필터 옵션(`CG_PRACTICE_MIN_M/MAX_M`)
  - 기록에 `aedAt`(쓰러진 지 N초)·`handover` 여부 추가: `CollapseHazard.AedAt`, `Handover` 는 public 이라 직접 읽기
- **시험 (`Tests/PlayMode/PracticeDrillStageTests.cs`)**: L31 `DrillFolder` 를 새 폴더로; 심정지 케이스 2개 추가(즉시형: kind==chest_pain && 시작 레벨 4 / 악화형: T초 뒤 레벨 4). 필터 `ChooGuard.Tests.PlayMode.PracticeDrillStageTests`, asmdef `ChooGuard.PlayModeTests`, JEV 는 SetUp 에서 `TYPESAFE_API_KEY=off`.
- **#301 이후 필수 선행 수정(발견 2026-10-30 20:00)**: 역 씬에 `CHOOGuard Provider Runtime` 루트가 있으면 `EmergencySession.Start()`(`EmergencySession.cs:140`)가 스스로 꺼져 `Incidents` 가 생기지 않는다. 레거시 경로를 쓰려면 PlayMode 시험은 `RetainedEmergencySessionFixture` 상속(적용 완료, 미커밋), **메뉴/배치 `Arm` 경로도 같은 처리 필요**: 리플렉션으로 `ChooGuard.Seeded.Unity.PlatformSeededSimulator.DeferPreparationForLegacyTests`(internal static bool) 를 true 로 두고, `SceneManager.sceneLoaded` 에서 `FpsStation` 로드 시 provider 루트 `SetActive(false)`(렌더러·콜라이더·메시 없는 루트인지 단정), 활성 `FirstPersonResponder` 가 없으면 테스트 플레이어 생성(픽스처 `EnsureRetainedResponder` 와 동일), 종료 시 원복. 픽스처 코드는 PlayModeTests 어셈블리에 있어 Editor 에서 직접 참조 불가 → `PracticeDrill.cs` 에 같은 로직을 두거나 공용 런타임 헬퍼로 승격(후자는 리트머스 결정 필요).
- **설계 함의**: 이 도구는 보존(legacy) IncidentDirector 경로 위에서 돈다. 에디터 전용 훈련 도구는 가능. 제품 "훈련" 모드로 올리려면 Seeded 런타임(`workers/simulator/docs/approved-play/`) 위에 재구축해야 하며 그건 별도 설계. → 제작 단계 8 의 PM 결정을 **착수 전 선행 결정**으로 올림.
- 실행 명령(검증용): `Unity -batchmode -projectPath C:\CHOOGuard -executeMethod ChooGuard.Editor.PracticeDrill.Arm -runTests -testPlatform PlayMode -testFilter ChooGuard.Tests.PlayMode.PracticeDrillStageTests -testResults <xml> -logFile <log>` + 환경변수 `CG_PRACTICE_FAMILY=심정지 CG_PRACTICE_EXPECT=1`.

## 선택지 A 제작 단계 (승인됨 — 다음 세션 착수)
1. `Choo-keep/uncommitted/practice-drill-prototype/PracticeDrill.cs` 를 `Assets/ChooGuard/Editor/Practice/` 로 복원(번들 `local-branches.bundle` 의 커밋 `3c3a21cd7` cherry-pick 도 가능).
2. `Families` 에 `"심정지" → { "chest_pain", "collapse" }` 추가(기존 "질환" 은 5종 전부). 메뉴 `ChooGuard/Practice/심정지 환자 한 건`. 환경변수 `CG_PRACTICE_FAMILY=심정지`.
3. 레벨 강제: 선택된 Transition 의 `Levels` 중 "의식 없음" 이상(ChestPain 3~4, Faint 3~4)으로 `Execute` 의 magnitude 를 고정 — 프로토타입은 레벨을 추첨에 맡겼으므로 이 한 줄이 "심정지" 를 보장한다. (`Execute` 시그니처 확인 필요: `IncidentDirector.Compose.cs` 의 호출부.)
4. 다른 사건 0건 보장: `TYPESAFE_API_KEY=off` 로 무대 → `Compose()` 가 후보를 만들지 않음(Compose.cs:111). 프로토타입의 `PracticeDrillStageTests` 방식 그대로.
5. 플레이어 동선 검증: 역무실 옆 휠체어(27.8,7.45,-7.8) ↔ 철도경찰 앞 AED(-22.6,8.35,-62.1) 는 2F 에서 약 74m 떨어짐. 환자 위치는 실제 승객에서 나오므로 고정 불가 — 대신 `AedSetDown` 까지의 경과 시간을 기록에 남긴다(ShiftLog 타임라인).
6. 수용 기준: (a) 한 근무에 위험 1건, Kind ∈ {chest_pain, collapse}, 시작 레벨 ≥ 3; (b) JEV 요청 0건; (c) AED 설치 → 구급 인계 로그에 "AED 포함"; (d) 다른 계열 위험 0건; (e) 기존 PracticeDrillStageTests 8/8 유지 + 심정지 케이스 2개 추가.
7. 시험: PlayMode 배치(`unity_ci.sh test PlayMode` 패턴, `TYPESAFE_API_KEY=off`), 무대 150~180초, 측정 3회.
8. 승격 판단은 #270 이슈에 — 에디터 전용으로 둘지(리플렉션 유지) 제품 "훈련" 모드로 올릴지(리트머스 완화, Tutorial↔Emergency 경계 결정 필요)는 PM 결정.

## 결정/제약
- 삭제는 사용자가 직접 실행 (자동 모드 분류기 차단)
- 시나리오 "제작 준비"까지만 — 실제 구현은 선택지 승인 후
- 하드룰: 사건 목록·고정 대본 금지 → "필터 + 강제 1건" 방식만 허용

## 현재 상태 (2026-10-31 낮, 데스크톱 이어서) — 구현 중, 에디터 전용 확정
사용자 결정(10-31 새벽): 에디터 전용으로 가고 바로 구현. 제품 훈련 모드는 설명만.

- [x] 즉시형 심정지 1건 강제 발생·인지·보고·구급 요청 실증 (stage4 Passed, 남측 게이트) + 촬영 `film/take1.mp4`
- [x] 구역 필터(`CG_PRACTICE_ZONES`)로 대합실 한정 — 후보 선택 실증(stage5 drill json)
- [ ] 대합실 take2 완주(stage6 실행 중) → `take2.mp4` — 자동 인계(업무·인계 4항목 검토 후 재인계) 첫 검증
- [ ] 악화형(`CG_PRACTICE_ONSET=worsen`) 실증 — 기록에 `treated` 추가하고 시험 단정을 "악화됐거나 그 전에 처치됨" 으로 완화할지 결정
- [ ] 수용 기준 (a)~(e) 점검표 작성 — (e) 기존 8/8 은 이 환경에서 아직 안 돌림
- [ ] progress/findings 정리 → ECC 리뷰(csharp-reviewer·code-reviewer) → 커밋 (브랜치 `feat/cardiac-arrest-drill`, `47502ed32` 위)
- [ ] 사용자: 구 `C:\Users\박효원\Choo` 삭제 (아직 존재, 이번 세션 cwd 가 그 폴더라 종료 후 실행)
