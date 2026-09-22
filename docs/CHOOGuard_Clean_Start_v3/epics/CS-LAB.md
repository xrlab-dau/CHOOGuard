# CS-LAB · 저장된 운영안의 분기·비교 실험

[제품 기준](../PRODUCT_BASELINE.md) · [공통 계약](../CONTRACTS.md) · [검수 보고](../review/REVIEW.md)

## CS-LAB.01 · 이벤트 재생·완전 checkpoint

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-LAB.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Checkpoint: schemas/Checkpoint.schema.json. cutSequence/tickUs/coreStateRef/eventQueueRef/randomStreamsRef/contentLockRef와 필수 workerStates를 검증한다.

### 필수 상세 명세
- [04-checkpoint-and-comparison](../specs/04-checkpoint-and-comparison.md)
- [03-durable-operations](../specs/03-durable-operations.md)

### 새 구현 경로
- `Assets/ChooGuard/Experiments/CheckpointService.cs`
- `Assets/ChooGuard/Experiments/ReplayReader.cs`
- `Assets/ChooGuard/Experiments/CheckpointManifest.cs`

### 선행 산출물과 소비 단계
- `CS-OPS.02:candidate` → `CS-LAB.01:integration` / 조건 `ALWAYS`.
- `CS-OPS.05:candidate` → `CS-LAB.01:integration` / 조건 `ALWAYS`.
- `CS-SIM.01:candidate` → `CS-LAB.01:integration` / 조건 `EXTERNAL_WORKER_USED`.

### 구현 절차
1. 공통 cut에서 업무·예약·기관지식·미전달 메시지·난수·worker 상태 또는 재계산 recipe를 모은다.
2. 필수 worker 목록은 capability와 run config에서 구한다. 파일 이름 존재만으로 정확 복원을 허용하지 않는다.
3. hash·version·시간을 검증한 후 manifest를 durable로 게시한다. 누락되면 복구 제한을 명시한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-LAB.01-P**

Given: required w1,w2; 동일 cut7/tick20  
When: checkpoint 검사  
Then: 동일 worker집합·시간·해시  
Result: NOT_RUN

**TEST-CS-LAB.01-N**

Given: w2 누락 또는 w1 중복  
When: checkpoint 검사  
Then: INCOMPLETE_CHECKPOINT  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/LAB/Lab01Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-036.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-LAB.02 · 원본 불변 분기·재실행

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-LAB.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** BranchReceipt {newRunId,parentRunId,checkpointHash,kind,changedPlan}; 리플레이는 새 실험이 아니다.

### 필수 상세 명세
- [04-checkpoint-and-comparison](../specs/04-checkpoint-and-comparison.md)
- [03-durable-operations](../specs/03-durable-operations.md)

### 새 구현 경로
- `Assets/ChooGuard/Experiments/BranchService.cs`
- `Assets/ChooGuard/Experiments/BranchLineage.cs`

### 선행 산출물과 소비 단계
- `CS-LAB.01:candidate` → `CS-LAB.02:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 새 runId와 parent checkpoint reference를 만들고 원래 run을 갱신하지 않는다.
2. 지원하는 정확 복원과 새 초기화 실행을 구별한다. user note는 과거 원래 의도로 덮어쓰지 않는다.
3. 불완전 복원 capability이면 특정 시점 branch를 비활성화하고 다른 시작점을 명시한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-LAB.02-P**

Given: parent run-a/cut7  
When: branch b  
Then: parent bytes 불변; child generation 신규  
Result: NOT_RUN

**TEST-CS-LAB.02-N**

Given: parent의 late RESULT  
When: branch b  
Then: FOREIGN_GENERATION  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/LAB/Lab02Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-037, REQ-057.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-LAB.03 · 동일조건·불확도·비지배 비교

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-LAB.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Comparison {basis,inputs,qoi,violations,uncertainty,status,selectionReason}; 현실 전체 최적을 주장하지 않음.

### 필수 상세 명세
- [04-checkpoint-and-comparison](../specs/04-checkpoint-and-comparison.md)
- [03-durable-operations](../specs/03-durable-operations.md)

### 새 구현 경로
- `Assets/ChooGuard/Experiments/ComparisonService.cs`
- `Assets/ChooGuard/Experiments/ExogenousScenarioStreams.cs`
- `Assets/ChooGuard/Experiments/ComparisonReport.cs`

### 선행 산출물과 소비 단계
- `CS-LAB.02:candidate` → `CS-LAB.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. map/rule/model/QoI basis가 호환되는지 먼저 검사한다. 외생 난수 혁신은 stable process key로 결속한다.
2. 사용자 조치가 바꾸는 후속 이동·보고·혼잡은 각 run에서 다시 계산한다.
3. 제약 위반을 속도 점수로 상쇄하지 않는다. 불확도·실패·우열 불명과 다목적 비지배 대안을 표시한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-LAB.03-P**

Given: 동일 exogenous process key, 다른 운영안  
When: A/B 비교  
Then: 외생 혁신 동일·내생값 재계산  
Result: NOT_RUN

**TEST-CS-LAB.03-N**

Given: modelLock 다른 두 run  
When: A/B 비교  
Then: INCOMPARABLE  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/LAB/Lab03Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-038, REQ-039.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-LAB.04 · 변경 영향·캐시·부분 재실행

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-LAB.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Impact {changedInputs,dirtyOutputs,evidence,allowedReuse,requiredRecompute}; 관계 부재는 영향 없음이 아님.

### 필수 상세 명세
- [04-checkpoint-and-comparison](../specs/04-checkpoint-and-comparison.md)
- [03-durable-operations](../specs/03-durable-operations.md)

### 새 구현 경로
- `Assets/ChooGuard/Experiments/ImpactGraph.cs`
- `Assets/ChooGuard/Experiments/ReplayCache.cs`
- `Assets/ChooGuard/Experiments/RunInvalidation.cs`

### 선행 산출물과 소비 단계
- `CS-LAB.03:candidate` → `CS-LAB.04:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 표현/서식 변경과 의미/물리 입력 변경을 구분한다.
2. 입력·버전·설정 hash와 영향 경계를 입증할 수 있을 때만 파생결과를 재사용한다.
3. 영향이 누락됐거나 비선형 파급을 제한할 수 없으면 확대 재계산한다. 과거 증거를 삭제하지 않고 현재 사용 자격을 STALE로 둔다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-LAB.04-P**

Given: 의미입력 하나 변경  
When: 부분/전체 재실행 대조  
Then: 동일 허용오차내 결과  
Result: NOT_RUN

**TEST-CS-LAB.04-N**

Given: 영향 graph 불완전  
When: 부분/전체 재실행 대조  
Then: FULL_RECOMPUTE_REQUIRED  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/LAB/Lab04Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-084.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).
