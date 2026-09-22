# CS-PROOF · 사용자 가치·현장 적합성 검증

[제품 기준](../PRODUCT_BASELINE.md) · [공통 계약](../CONTRACTS.md) · [검수 보고](../review/REVIEW.md)

## CS-PROOF.01 · 실제 작성팀·자료·시간 가설 조사

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PROOF.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** StudyIntake {consent,roles,caseRefs,workflow,template,measurements,missing}; 표본 제안과 모집 완료를 분리.

### 필수 상세 명세
- [10-observability-and-study](../specs/10-observability-and-study.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

### 새 구현 경로
- `research/discovery/protocol.md`
- `research/discovery/intake.schema.json`
- `research/discovery/time-observation.schema.json`

### 선행 산출물과 소비 단계
선행 구현 산출물 없음. 현재 작업의 입력과 안전 경계는 확인한다.

### 구현 절차
1. 반복 작성자·기관 협조자·검수자·자료담당을 구분하고 최근 대본/수정/양식을 허용범위에서 관찰한다.
2. 교육·대리입력·기관회신·무인계산·사람 작업량을 분리한다. 동일인 중첩시간은 합집합이다.
3. 자료/인터뷰 미확보는 해당 고객 실증만 제한한다. 합성 코어 구현을 막는 전역 선행이 아니다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PROOF.01-P**

Given: 동의된 실제 과제  
When: 최근 업무관찰  
Then: 가명/누락/도움 표시  
Result: NOT_RUN

**TEST-CS-PROOF.01-N**

Given: 가상의 인터뷰 답  
When: 최근 업무관찰  
Then: NOT_OBSERVED  
Result: NOT_RUN

### 실제 시험 매체
- DOCUMENT_REVIEW: `research/proof/01-evaluation-protocol.md` · NOT_RUN

### 연결과 인계
제품 요구: REQ-077, REQ-078.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: DISC-01, DISC-03, DISC-05, DISC-18. [출처 등록부](../reference/sources.json).

---

## CS-PROOF.02 · 종단 작동·장애·성능 검수

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PROOF.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Qualification별 evidence bundle; 기능/성능/정량/현장 수용은 별도.

### 필수 상세 명세
- [10-observability-and-study](../specs/10-observability-and-study.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

### 새 구현 경로
- `Assets/ChooGuard/Tests/Acceptance/EndToEndFlowTests.cs`
- `Assets/ChooGuard/Tests/Acceptance/FaultInjectionTests.cs`
- `qualification/technical-profile.json`

### 선행 산출물과 소비 단계
- `CS-OPS.06:candidate` → `CS-PROOF.02:integration` / 조건 `ALWAYS`.
- `CS-PLAY.05:candidate` → `CS-PROOF.02:integration` / 조건 `ALWAYS`.
- `CS-LAB.04:candidate` → `CS-PROOF.02:integration` / 조건 `ALWAYS`.
- `CS-MODES.03:candidate` → `CS-PROOF.02:integration` / 조건 `ALWAYS`.
- `CS-SCRIPT.04:candidate` → `CS-PROOF.02:integration` / 조건 `ALWAYS`.
- `CS-PLAY.06:candidate` → `CS-PROOF.02:integration` / 조건 `ALWAYS`.
- `CS-OPS.07:candidate` → `CS-PROOF.02:integration` / 조건 `ALWAYS`.

### 구현 절차
1. A안 문제발견→B수정→비교→대본→새 run을 실제 Player에서 끝까지 수행한다.
2. 저장실패·worker장애·로그손상·중복명령·UI뒤클릭·IME·network loss를 반례로 만든다.
3. 고정 부하에서 UI/렌더/운영/solver/AI 비용을 나누고 인원·모델을 몰래 낮추지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PROOF.02-P**

Given: native A→B→대본 실제서비스  
When: E2E/장애 주입  
Then: 동일 run/version 근거  
Result: NOT_RUN

**TEST-CS-PROOF.02-N**

Given: 문서검사만 성공  
When: E2E/장애 주입  
Then: PRODUCT_NOT_TESTED  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/PROOF/Proof02Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSPROOF02PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-PROOF.02.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-001, REQ-070, REQ-074.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-PROOF.03 · A/B/C 사용자효용과 반복 비용

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PROOF.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** StudyResult {conditions,participants,quality,time,censoring,assistance,limits}; 제품정확도를 선호도로 증명하지 않음.

### 필수 상세 명세
- [10-observability-and-study](../specs/10-observability-and-study.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

### 새 구현 경로
- `research/usability/protocol.json`
- `research/usability/analysis.py`
- `research/usability/report-template.md`

### 선행 산출물과 소비 단계
- `CS-PROOF.01:candidate` → `CS-PROOF.03:integration` / 조건 `ALWAYS`.
- `CS-PROOF.02:candidate` → `CS-PROOF.03:integration` / 조건 `ALWAYS`.
- `CS-PLAY.07:candidate` → `CS-PROOF.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. A 고객기존방식, B 동일코어 표/타임라인, C UnityRTS 조건을 고정한다. B는 연구 조건이며 제품모드 추가가 아니다.
2. 순서·난이도·도움·기존AI 허용조건·검수가림을 기록하고 품질이 유지된 총인시를 비교한다.
3. 미완료·중단·보조·초기비용·유지비를 포함하며 20%는 사전합의한 탐색목표로만 쓴다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PROOF.03-P**

Given: A/B/C 모든 참가자 결과  
When: 효용분석  
Then: 중단·미완료·도움 분모유지  
Result: NOT_RUN

**TEST-CS-PROOF.03-N**

Given: 좋은 완료자만 분석  
When: 효용분석  
Then: PROTOCOL_VIOLATION  
Result: NOT_RUN

### 실제 시험 매체
- DOCUMENT_REVIEW: `research/proof/03-evaluation-protocol.md` · NOT_RUN

### 연결과 인계
제품 요구: REQ-069, REQ-080, REQ-082, REQ-089.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: DISC-05, DISC-07, DISC-08, DISC-09, DISC-11, FREE-001. [출처 등록부](../reference/sources.json).

---

## CS-PROOF.04 · 지정 현장의 독립 검증·기관 수용

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PROOF.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** UsePassport {site,scope,manuals,models,qoi,observations,uncertainty,reviews}; 실제 설비 제어 기능 없음.

### 필수 상세 명세
- [10-observability-and-study](../specs/10-observability-and-study.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

### 새 구현 경로
- `qualification/field/protocol.json`
- `qualification/field/passport.schema.json`
- `qualification/field/review-ledger.json`

### 선행 산출물과 소비 단계
- `CS-PACK.04:candidate` → `CS-PROOF.04:integration` / 조건 `ALWAYS`.
- `CS-PROOF.02:candidate` → `CS-PROOF.04:integration` / 조건 `ALWAYS`.
- `CS-SIM.05:candidate` → `CS-PROOF.04:integration` / 조건 `ALWAYS`.
- `CS-WORLD.02:candidate` → `CS-PROOF.04:integration` / 조건 `ALWAYS`.

### 구현 절차
1. QoI와 허용오차·독립 split을 사전 고정하고 대상별 규칙·형상·시간·행동·결합을 대조한다.
2. 실행가능·모델검증·현실 관측 연동·기관 지정훈련 수용을 분리한다. surrogate/reference 일치를 현장 검증으로 대체하지 않는다.
3. 안전한 도상/시범 검토와 별도 검수자를 기록한다. 추가 현상은 필요한 독립 증거가 준비될 때만 수용한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PROOF.04-P**

Given: 독립 holdout·scope·reviewer  
When: 지정 수용  
Then: 해당 QoI/장소/판본만 수용  
Result: NOT_RUN

**TEST-CS-PROOF.04-N**

Given: 같은 개발자료만 재사용  
When: 지정 수용  
Then: NOT_INDEPENDENT  
Result: NOT_RUN

### 실제 시험 매체
- DOCUMENT_REVIEW: `research/proof/04-evaluation-protocol.md` · NOT_RUN

### 연결과 인계
제품 요구: REQ-040, REQ-067, REQ-068, REQ-076, REQ-090.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: DISC-11, STD-DTC, TECH-VV. [출처 등록부](../reference/sources.json).
