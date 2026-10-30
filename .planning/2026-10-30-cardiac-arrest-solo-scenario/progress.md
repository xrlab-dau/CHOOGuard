# Progress

- 2026-10-30 18:00 계획 생성. Phase 1~3 병렬 조사 시작.
- 18:10 Phase 1: `Library` → `C:\CHOOGuard\Library` 이동 완료(41항목). 보관 폴더 `C:\Users\박효원\Choo-keep` 에 stash 패치 2, 브랜치 번들(11커밋), 측정 도구, PracticeDrill 프로토타입 평문, 로컬 고유 153 + 미추적 199(101MB) 저장 — 합계 118MB/361파일. `.checkpoints` 사본 30,330개는 폐기 대상으로 판정. 삭제는 사용자 실행 대기.
- 18:20 Phase 2: 라이선스 유효 확인(StopDate 2026-11-11). `EquipmentSnapshot.cs` 작성. 첫 실행 시도는 Bash heredoc 파싱 오류로 아무것도 실행되지 않음 → Write 도구로 재작성 후 Unity 배치 실행 시작(백그라운드).
- 18:25 Phase 3: 하드룰·Casualty 조건·Compose 경로·PracticeDrill 선례·#283 확인. 설계 선택지 A/B/C 정리(권장 A). findings 기록.
- 18:31~18:49 Unity 2차(분리 실행): 임포트 25,626개 후 컴파일·씬 열기·6컷 촬영 성공. 단 고정 각도라 AED 는 벽 뒤, context/high 는 건물 밖. 1차 시도는 하네스 저메모리 보호로 중단(여유 0.1GB 까지 떨어짐).
- 18:51~18:54 3차: `ClearPose`(CheckSphere+Linecast 로 12방향×3거리 탐색) 적용. 휠체어 close(1.05m), AED close(1.56m)·context(2.52m) 확보, wide 는 전 방향 가림. 구 컷 3장 제거. PNG 3장 사용자에게 전달.
- 19:00 사용자 결정: A. `incident-outline.md`(12장) 작성·전달.
- 19:10 구현 준비: 브랜치 `feat/cardiac-arrest-drill`(wheelchair-aed-local 에서 분기) 생성, 번들에서 `3c3a21cd7` cherry-pick → `47502ed32`(PracticeDrill.cs, PracticeDrillStageTests.cs, drill json). 리플렉션 대상 4개·ChangeCondition·Transition 필드 현재 코드에 동일 존재 확인. Unity 컴파일 검증 실행.
- 19:46 컴파일 통과(exit 0, Tundra 15 items, error 0).
- 19:47~19:58 무대 시험 1차(`Arm` + `PracticeDrillStageTests`, 질환, EXPECT=1): **실패** — "디렉터가 부팅 안에 준비되지 않았습니다(null)", 539초. 원인: `EmergencySession.cs:140` 이 `CHOOGuard Provider Runtime` 가 있으면 스스로 꺼짐(#301). 레거시 시험은 `RetainedEmergencySessionFixture` 를 상속해야 provider 를 비활성화하고 보존 근무를 띄움 — 10/5 시험은 상속 안 함.
- 20:05 시험 클래스에 `: RetainedEmergencySessionFixture` 추가 후 2차 실행(분리). CI 는 develop 에서 3일째 실패(라이선스 단계, Edit/PlayMode skipped)라 지표 불가.
- 설계 함의: 선택지 A 는 보존(legacy) IncidentDirector 경로 위에서 돈다. 에디터 전용 훈련 도구로는 가능, 제품 훈련 모드는 Seeded 런타임 재구축 필요 → PM 결정 항목으로 승격.
- 20:01~20:11 무대 시험 2차(픽스처 상속 후): **통과** 1/1, 518초. 30.0초 `collapse_2` 강제 발생(2층 남측 게이트) → 30.2초 인지(practice) → 30.3초 보고·구급 요청. 승객 142명, 무대 150초, 기록 `drill-20261030-110915.json`. → 프로토타입이 현재 코드 위에서 작동함을 실증. **구현 준비 완료.**
- 미커밋 상태: 브랜치 `feat/cardiac-arrest-drill` 에 cherry-pick 커밋 `47502ed32` + 시험 파일 1줄 수정(픽스처 상속). 커밋 전 ECC 리뷰 필요(메모리 규칙).
- 대기: 사용자 `Choo` 삭제 실행, PM 선행 결정(에디터 전용 vs 제품 훈련 모드).

## 2026-10-31 새벽 세션(03:19~04:00) — 기록 누락분을 산출물로 복원 (10-31 낮 세션에서 보충)
- 사용자 결정: **에디터 전용**으로 가고 바로 구현 시작. 제품 훈련 모드는 설명만(Seeded 런타임 재구축 필요, PM 결정).
- 구현(미커밋, 브랜치 `feat/cardiac-arrest-drill`): `PracticeDrill.cs` 에 계열 `심정지={chest_pain}`, 메뉴 `ChooGuard/Practice/심정지 환자 한 건`, 즉시형(m=1.0→4단계)/악화형(`CG_PRACTICE_ONSET=worsen`, m=.75→3단계, `CG_PRACTICE_WORSEN_AFTER` 기본 60초 뒤 `ChangeCondition(+1)` 리플렉션), 실내 필터(`CG_PRACTICE_INDOOR`)·구역 필터(`CG_PRACTICE_ZONES`), 기록에 startLevel/level/notBreathingNormally/aedAt/handover/zone/candidates 추가, `RetainedSession`(provider 유예 seam + 루트 비활성 + 연습용 플레이어) 으로 메뉴 경로도 보존 근무 선택. 메뉴는 편집 모드에서 Bootstrap 열고 무장 후 재생 진입.
- 시험: `PracticeDrillStageTests` 에 `RetainedEmergencySessionFixture` 상속, 드릴 폴더 변경, 심정지 기록 단정(가슴 통증·시작/끝 단계·AED 지시 문장), 촬영(`CG_PRACTICE_FILM`, `ShiftSampleTests.Recorder` 를 internal 로), 현장이 보이는 자리에 역무원 세우기(`Face`), 구급대장 도착 시 자동 인계(`CG_PRACTICE_HANDOVER`).
- 03:28/03:32 컴파일 2·3차 통과. 03:33 4차 전 시험(stage3) 실패(원인 로그 미확인 — stage3 로그 참조).
- 03:35~03:46 **stage4 통과 1/1**: `drill-20261030-184041.json` — 2층 남측 게이트, chest_pain_2, 즉시형 4단계, 역무실 지시에 AED 문장 포함, 보고까지 0.3초, 구급 요청. 무대 420초·근무 388.7초·승객 138명. 자동 인계 시도됨(업무·인계 4항목 검토 요구 문구 확인). 촬영 1883장 → `film/take1.mp4`(854×480, 5fps).
- 03:49~03:58 stage5(구역 `hall2f,main2f` 로 좁힘): `drill-20261030-185651.json` — 2층 대합실 의자 구역, chest_pain_3(노인), 즉시형 4단계, 보고됨. 하지만 **촬영 555장(111초)에서 중단**(결과 XML 없음, 종료 크래시 = 프로세스 강제 종료). 프레임은 `film/take2-partial-interrupted/` 로 치움.
- 남은 것: take2 완주(대합실) + mp4, 악화형 실증, progress/task_plan 정리, ECC 리뷰 후 커밋. 구 `C:\Users\박효원\Choo` 는 아직 삭제 안 됨(사용자 실행 대기).

- 10-31 낮: stage6 = take2 재실행 시작(구역 hall2f,main2f · WAIT 420 · 촬영 · EXPECT 1). 잔존 `Temp/UnityLockfile`(03:49) 제거 후 기동.
- 04:10~04:23 **stage6 통과 1/1** (대합실 한정, 촬영): `drill-20261030-191946.json` — 2층 대합실 의자 구역, chest_pain_3, 즉시형 4단계, 후보 2건 중 다른 구역 1건 거름. 근무 241.8초: 구급대장 도착 후 하네스가 인계 시도 → "업무·인계 4항목 검토" 요구 → 항목 5개 전송 후 재인계 성공("검토 내용을 구급대에 인계했습니다") → 근무 종료. 프레임 1115장 → `film/take2.mp4`(2.8MB, 5fps). 자동 인계 경로 첫 실증.
- 04:25 악화형 보강: 기록에 `treated` 추가, 시험 단정을 "악화(level 4·worsenedAt) 또는 그 전에 처치(treated)" 로 완화. stage7(ONSET=worsen, WAIT 180, nographics, 대합실) 실행 시작.
- 04:27~04:35 **stage7 통과 1/1** (악화형): `drill-20261030-193408.json` — 대합실 의자 구역, chest_pain_3, m=.75 → 3단계 "의식을 잃고 쓰러짐(숨은 쉼)", 30.2초 보고(역무실 지시에 AED 문장 없음 = 3단계 규칙대로), 90.2초 `ChangeCondition(+1)` → 4단계 "숨을 제대로 쉬지 않음", treated=false, 승객 142명. 즉시형·악화형 둘 다 실증 완료.
- 04:40~04:58 ECC 리뷰: code-reviewer 지적 2건(하드룰 형태 이탈은 PM 항목으로, 시험의 "최신 파일" 읽기는 `NewDrill` 집합 차이로 수정). 기록에 `treated` 추가. 컴파일 4차 통과(오류 0). 중단된 5차가 남긴 `Assets/InitTestScene*.unity` 임시 씬 삭제. csharp-reviewer 1차 응답 없음 → 2차 실행 중.
