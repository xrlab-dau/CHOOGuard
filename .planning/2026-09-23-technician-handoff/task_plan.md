# Task Plan: 기술자 NPC 인계 주기

## Goal
정비 튜토리얼의 `issue-repair-order`를 불리언 플래그에서 **관찰 가능한 인계 주기**로 바꾼다. 수신·이동·수행·결과가 월드에서 보이고, 플레이어는 요청·입회·결과 확인만 한다. G9의 최대 공백을 메운다.

## Next Step
Phase 1~6 완료(PlayMode 45/45). 남은 것은 **씬 배선**인데, 슬라이스 생성기를 실행하면 소화기 11개가 지워지므로 보류했다(사용자 결정 C, 2026-09-24).
선행 조건: `.planning/2026-09-22-station-interior-build/extinguisher-placement-v3.json`의 12개 좌표를 생성기에 주입. 상세와 복원용 배선 코드는 `findings.md`.

## Current Phase
완료 (씬 배선 보류)

## 설계 제약 (기존 코드에서 도출, 어기지 않는다)
- **벽시계 금지.** `TutorialSession` 주석: "벽시계가 없다 — Time.time 기반 실패 조건을 두지 않는다. 시계는 플레이어가 쥔다." → 진행은 명시적 `Tick(seconds)`로만. 시간 초과 실패 없음.
- **네임스페이스 경계.** Tutorial 네임스페이스는 Emergency 심볼을 참조하지 않는다.
- **설비는 규칙을 모른다.** `FacilityInspectable`은 월드 상태만 들고, 절차 규칙은 세션이 소유한다. 새 상태도 같은 규율.
- **사실 기반 판정.** 완료는 버튼이 아니라 `Observe()`가 읽는 월드 상태로 판정된다.
- **거부는 감점으로.** 설교하지 않는다 — `RoleBoundaryViolations` 방식을 따른다.

## Phases

### Phase 1: TechnicianDispatch 컴포넌트
- [x] `App/Fps/Work/TechnicianDispatch.cs` — `HandoffStage { NONE, RECEIVED, TRAVELLING, WORKING, COMPLETED }`
- [x] `Request(serial, reason)` 수신, `Tick(seconds)` 이동·수행 진행, `Reset()`
- [x] 단계별 소요는 인스펙터 값. 실패 조건 없음 — 늦어질 뿐이다.
- **Status:** complete (TechnicianDispatch.cs)

### Phase 2: 설비 월드 상태
- [x] `FacilityInspectable`에 교체 결과 상태 추가(결함 해소·대체 고유번호). 규칙은 넣지 않는다.
- [x] `ResetInspection()`이 이 상태도 되돌리는지 확인
- **Status:** complete (ApplyReplacement·상태 복원)

### Phase 3: 세션 배선
- [x] `issue-repair-order` 효과가 `dispatch.Request(...)`를 부른다
- [x] `Observe()`가 기술자 사실을 발행 — `technician-received` / `technician-onsite` / `technician-completed` / `replacement-witnessed`
- [x] `Audit()`이 결과 미확인을 미충족으로 열거
- [x] `Restart()`가 인계도 초기화
- **Status:** complete (Dispatch 배선·사실 5개·Audit·Restart)

### Phase 4: 절차 자료 v2
- [x] `fire-extinguisher-monthly.json`에 `witness-replacement` 조건부 단계 추가, `version` 2로
- [x] 근거 조문 확인 후 `basis` 기재. 근거 없으면 설계 선택임을 `sourceNote`에 명시
- **Status:** complete (절차 v2, witness-replacement)

### Phase 5: 시험·실행
- [x] `Tests/PlayMode/FpsTechnicianHandoffTests.cs` — 단계 전이, 즉시 완료되지 않음, 대기 중 다른 행동 가능, 결과 미확인 시 감사 열거
- [x] PlayMode 전체 재실행, 회귀 없음 확인, 영수증 기록
- **Status:** complete (시험 13건, PlayMode 45/45)

### Phase 6: ECC 리뷰·문서 반영
- [x] csharp-reviewer 리뷰 후 반영
- [x] `TUTORIAL_MAINTENANCE_DESIGN.md` 5절 (1) 해소, `QUALITY_A_RUBRIC.md` G9 갱신
- **Status:** complete (ECC HIGH 1·MEDIUM 1 반영, 영수증·문서 갱신)
