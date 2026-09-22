# CS-MODES · 매뉴얼 교육과 제약 기반 랜덤 실험

[제품 기준](../PRODUCT_BASELINE.md) · [공통 계약](../CONTRACTS.md) · [검수 보고](../review/REVIEW.md)

## CS-MODES.01 · 한 개의 근거 기반 교육 과정

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-MODES.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** TutorialCourse {source,scope,goals,partialOrder,hints,assessment}; 공식 이수·훈련 정확재현은 별도 검수.

### 필수 상세 명세
- [08-modes-and-scenario-generation](../specs/08-modes-and-scenario-generation.md)
- [04-checkpoint-and-comparison](../specs/04-checkpoint-and-comparison.md)

### 새 구현 경로
- `Assets/ChooGuard/Scenarios/TutorialDirector.cs`
- `content/exercises/tutorial/course.json`
- `Assets/ChooGuard/Presentation/Tutorial/TutorialOverlay.prefab`
- `Assets/ChooGuard/Presentation/Tutorial/TutorialPresenter.cs`

### 선행 산출물과 소비 단계
- `CS-PACK.02:candidate` → `CS-MODES.01:integration` / 조건 `ALWAYS`.
- `CS-OPS.05:candidate` → `CS-MODES.01:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 실제 사례를 재구성한 부분과 원 대본이 확인된 부분을 구분한다.
2. 평가는 부분순서·조건·증거를 사용하고 독립 업무의 유효한 순서 변경을 허용한다.
3. 힌트는 표시만 바꾸며 물리·기관권한·자원능력·전달시간을 수정하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-MODES.01-P**

Given: 독립 taskA/taskB 목표  
When: 순서 AB와 BA  
Then: 둘 다 학습통과  
Result: NOT_RUN

**TEST-CS-MODES.01-N**

Given: hint가 speed 변경  
When: 순서 AB와 BA  
Then: CORE_PARITY_FAIL  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/MODES/Modes01Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-003, REQ-004, REQ-065.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: CASE-READY2026, CASE-YULHYEON, MAN-SOP. [출처 등록부](../reference/sources.json).

---

## CS-MODES.02 · 제약 기반 랜덤 상황

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-MODES.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** GeneratedScenario {spec,seedManifest,provenance,constraintReport,qualification}; 무작위 발생확률은 현실 빈도와 별개.

### 필수 상세 명세
- [08-modes-and-scenario-generation](../specs/08-modes-and-scenario-generation.md)
- [04-checkpoint-and-comparison](../specs/04-checkpoint-and-comparison.md)

### 새 구현 경로
- `Assets/ChooGuard/Scenarios/ScenarioGenerator.cs`
- `Assets/ChooGuard/Scenarios/ScenarioConstraintChecker.cs`
- `content/exercises/random/grammar.json`

### 선행 산출물과 소비 단계
- `CS-PACK.01:candidate` → `CS-MODES.02:integration` / 조건 `ALWAYS`.
- `CS-OPS.06:candidate` → `CS-MODES.02:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 기관·공간·자원과 검토된 사건 문법을 입력으로 받고 조건부로 사건을 표본화한다.
2. 성공/거부 분포와 seed·각 난수 혁신·거부 이유를 보존한다. 반복상한에서 무한생성 대신 조건 조정을 요구한다.
3. FEASIBLE·ESCALATION_REQUIRED·CONFLICTED_INPUT·UNSUPPORTED_DOMAIN을 구분한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-MODES.02-P**

Given: 고정 grammar와 동일 seed manifest  
When: 제약상황 생성  
Then: 같은 spec; rejection 추적  
Result: NOT_RUN

**TEST-CS-MODES.02-N**

Given: 64회 연속 조건실패  
When: 제약상황 생성  
Then: GENERATION_EXHAUSTED  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/MODES/Modes02Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-005, REQ-006, REQ-007.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-MODES.03 · 현장 재사용·모드 연결·다음 판단 지점

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-MODES.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Mode={TUTORIAL,RANDOM_OPERATIONS_LAB}; RuntimeConfig는 같은 코어와 lock을 공유.

### 필수 상세 명세
- [08-modes-and-scenario-generation](../specs/08-modes-and-scenario-generation.md)
- [04-checkpoint-and-comparison](../specs/04-checkpoint-and-comparison.md)

### 새 구현 경로
- `Assets/ChooGuard/Scenarios/WorkspaceSessionFactory.cs`
- `Assets/ChooGuard/Scenarios/DecisionBoundaryRunner.cs`
- `Assets/ChooGuard/Scenarios/ModePolicy.cs`
- `Assets/ChooGuard/Presentation/Scenarios/ScenarioSetupPresenter.cs`
- `Assets/ChooGuard/Presentation/Scenarios/SiteSelectionPresenter.cs`
- `Assets/ChooGuard/Presentation/Scenarios/ScenarioSetup.prefab`
- `Assets/ChooGuard/Presentation/Scenarios/SiteSelection.prefab`

### 선행 산출물과 소비 단계
- `CS-MODES.01:candidate` → `CS-MODES.03:integration` / 조건 `ALWAYS`.
- `CS-MODES.02:candidate` → `CS-MODES.03:integration` / 조건 `ALWAYS`.
- `CS-LAB.02:candidate` → `CS-MODES.03:integration` / 조건 `ALWAYS`.
- `CS-PLAY.05:candidate` → `CS-MODES.03:integration` / 조건 `ALWAYS`.
- `CS-SIM.01:candidate` → `CS-MODES.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 현장·규칙·template·model 묶음을 읽고 새 운영안을 만든다. 원본 번들은 바꾸지 않는다.
2. 제품 모드는 두 개만 허용하고 analysis/branch/solver profile은 공통 도구로 둔다.
3. 다음 판단지점 이동은 사건과 solver를 순차 처리한다. 미래 결과를 예측 없이 미리 게시하거나 dt를 늘리지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-MODES.03-P**

Given: 다음 의사결정 이벤트 tick100  
When: 진행요청  
Then: 순차계산 후100; 미래정보 노출0  
Result: NOT_RUN

**TEST-CS-MODES.03-N**

Given: worker 미준비  
When: 진행요청  
Then: COMPUTING_NOT_FAST_FORWARD  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/MODES/Modes03Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSMODES03PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-MODES.03.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-002, REQ-083, REQ-085.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).
