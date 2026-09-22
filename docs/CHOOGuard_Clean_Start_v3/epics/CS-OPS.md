# CS-OPS · 영속 상태를 가진 다기관 운영 커널

[제품 기준](../PRODUCT_BASELINE.md) · [공통 계약](../CONTRACTS.md) · [검수 보고](../review/REVIEW.md)

## CS-OPS.01 · 신규 run·단일 writer·명령 상태머신

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-OPS.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** CommandIntent→CommandReceipt, stateVersion/readSet; 수락은 수행완료와 다르다.

### 필수 상세 명세
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [03-durable-operations](../specs/03-durable-operations.md)
- [10-observability-and-study](../specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Application/OperationsSession.cs`
- `Assets/ChooGuard/Application/CommandDispatcher.cs`
- `Assets/ChooGuard/Domain/RunState.cs`

### 선행 산출물과 소비 단계
- `CS-BOOT.02:candidate` → `CS-OPS.01:integration` / 조건 `ALWAYS`.
- `CS-PACK.01:candidate` → `CS-OPS.01:integration` / 조건 `ALWAYS`.

### 구현 절차
1. run별 mailbox와 single-writer queue를 만들고 순서는 simulation tick·priority·monotonic sequence로 고정한다.
2. Preview는 side effect 없이 현재 guard를 평가한다. Submit은 requester와 acting team을 구분해 재검사한다.
3. 같은 key+payload는 같은 receipt, 다른 payload는 INTENT_CONFLICT를 반환한다. 인메모리 double은 기능시험 전용이다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-OPS.01-P**

Given: 동일 run/requester/key/payload 2회  
When: Submit 재전송  
Then: receipt 동일1개; 효과1회  
Result: NOT_RUN

**TEST-CS-OPS.01-N**

Given: 동일 key 다른 target  
When: Submit 재전송  
Then: INTENT_CONFLICT  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/OPS/Ops01Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-001, REQ-071.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-OPS.02 · SQLite 저장·원자 예약·outbox

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-OPS.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** IRunStore.CommitAsync(CommitBatch,CancellationToken) → Task<CommitReceipt>. 정확 필드는 schemas/CommitBatch.schema.json 및 schemas/CommitReceipt.schema.json; K03 원자성 규칙 적용.

### 필수 상세 명세
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [03-durable-operations](../specs/03-durable-operations.md)
- [10-observability-and-study](../specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Persistence/SqliteRunStore.cs`
- `Assets/ChooGuard/Persistence/SqliteProvider.cs`
- `Assets/ChooGuard/Persistence/Schema.sql`
- `Assets/ChooGuard/Domain/ReservationPlanner.cs`
- `Assets/ChooGuard/Application/OutboxDispatcher.cs`

### 선행 산출물과 소비 단계
- `CS-OPS.01:candidate` → `CS-OPS.02:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 대상 Unity Player의 managed/native SQLite 조합을 smoke-test하고 pin한다. 플랫폼별 P/Invoke 성공을 확인한다.
2. BEGIN IMMEDIATE 경계에서 run revision·현재 자원 가용량을 재검사한다. 팀 구성원·승무원은 고유 ID로 중복점유를 검사한다.
3. 이벤트·예약·receipt·projection revision·outbox를 원자적으로 저장하고 commit 성공 뒤에만 acceptance를 게시한다.
4. 외부 전달은 재시도 가능하지만 jobId/resultId로 멱등 적용한다. 디스크 실패 후 candidate 메모리 상태를 게시하지 않는다.
5. 배포 SQLite는 3.51.3 이상 또는 공식 WAL-reset 수정 backport를 바이너리 버전·source-id·해시로 입증한다. 단순 managed wrapper 버전으로 판정하지 않는다.
6. 명령·예약은 하나의 actor-serialized connection에서 처리하고 WAL/FULL/foreign_keys 설정을 확인한다. 백업은 snapshot API와 blob pin으로 수행한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-OPS.02-P**

Given: capacity3, jobA 예약2, jobB 요청2  
When: 원자 예약  
Then: jobB 거부; 총예약2; 나머지 자원 미점유  
Result: NOT_RUN

**TEST-CS-OPS.02-N**

Given: commit 직전 예외  
When: 원자 예약  
Then: 전체 rollback; ACCEPTED0  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/OPS/Ops02Tests.cs` · NOT_RUN
- PLAYER_DISK: `qualification/player/CS-OPS.02-disk.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-024, REQ-071.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: TECH-TAPAAL, AUD-SQLITE-WAL. [출처 등록부](../reference/sources.json).

---

## CS-OPS.03 · 기관 권한·지원요청·인계

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-OPS.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** AuthorityGrant {holder,agency,scope,revision,ruleRef}; RequestSupport와 AssignTask는 구별.

### 필수 상세 명세
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [03-durable-operations](../specs/03-durable-operations.md)
- [10-observability-and-study](../specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Domain/AuthorityPolicy.cs`
- `Assets/ChooGuard/Domain/SupportRequest.cs`
- `Assets/ChooGuard/Domain/HandoverPolicy.cs`

### 선행 산출물과 소비 단계
- `CS-OPS.02:candidate` → `CS-OPS.03:integration` / 조건 `ALWAYS`.
- `CS-PACK.02:candidate` → `CS-OPS.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 작성자의 가상 설계권과 기관의 업무권을 구분한다.
2. 기관내 지시와 기관간 요청은 서로 다른 event로 처리한다.
3. 인계에는 원 권한 revision·범위·상대 확인·사유를 요구하며 후착 도착 자체는 전역 권한을 바꾸지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-OPS.03-P**

Given: agencyA command와 agencyB resource  
When: 외부 support 요청  
Then: B 접수 전 배정0  
Result: NOT_RUN

**TEST-CS-OPS.03-N**

Given: A가 B 자원을 직접확정  
When: 외부 support 요청  
Then: AUTHORITY_DENIED  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/OPS/Ops03Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-021, REQ-022.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: MAN-KORAIL, MAN-SOP. [출처 등록부](../reference/sources.json).

---

## CS-OPS.04 · 기관별 정보·보고·인계 상태

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-OPS.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Message {id,sender,recipient,observedTick,receivedTick,expiry,payloadRef,ack}; projection은 read-only.

### 필수 상세 명세
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [03-durable-operations](../specs/03-durable-operations.md)
- [10-observability-and-study](../specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Domain/MessageLifecycle.cs`
- `Assets/ChooGuard/Domain/KnowledgeState.cs`
- `Assets/ChooGuard/Application/AgencyProjection.cs`

### 선행 산출물과 소비 단계
- `CS-OPS.03:candidate` → `CS-OPS.04:integration` / 조건 `ALWAYS`.

### 구현 절차
1. messageId·관측시각·전달상태·TTL·수신기관을 기록한다.
2. 중복은 같은 의미효과를 재적용하지 않고 역순·상충 보고는 UNKNOWN/CONFLICTED 상태로 남긴다.
3. 작성자 전체 분석 projection이 기관 knowledge를 수정하지 못하게 한다. 미래 상태를 과거 복기의 지식으로 사용하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-OPS.04-P**

Given: message m1 두번 도착  
When: ack 처리  
Then: 지식효과1회; received 원시기록 보존  
Result: NOT_RUN

**TEST-CS-OPS.04-N**

Given: TTL 만료후 guard 충족 요청  
When: ack 처리  
Then: STALE_INFORMATION  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/OPS/Ops04Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-011, REQ-027, REQ-028.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-OPS.05 · 업무 의존·공간 접근·예약 생명주기

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-OPS.05 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** TaskState {lifecycle,guards,reasons,reservations,outcome,handoff}; GuardResult는 truth/status와 evidence refs를 가진다.

### 필수 상세 명세
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [03-durable-operations](../specs/03-durable-operations.md)
- [10-observability-and-study](../specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Domain/WorkflowEngine.cs`
- `Assets/ChooGuard/Domain/ResourceLifecycle.cs`
- `Assets/ChooGuard/Domain/TaskConditions.cs`

### 선행 산출물과 소비 단계
- `CS-OPS.04:candidate` → `CS-OPS.05:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 요청→배정→준비/이동→수행→보고/인계→반환 중 업무별 필요한 단계를 명시한다.
2. 작업 소요시간은 근거가 있는 조건부 입력으로 받는다. 합성 값은 검증된 현장 예측과 분리한다.
3. 팀 분리/결합·취소·교대·재보급은 능력과 자원 점유를 다시 검사한다. 수용·의료 인계는 운영상태이며 임상 판단을 생성하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-OPS.05-P**

Given: 선행 t1 완료; 장비reserved; report pending  
When: t2 평가  
Then: WAITING_REPORT; 자원보존  
Result: NOT_RUN

**TEST-CS-OPS.05-N**

Given: 다른 task 예약 취소  
When: t2 평가  
Then: RESERVATION_OWNERSHIP_MISMATCH  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/OPS/Ops05Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-012, REQ-023, REQ-025, REQ-026, REQ-029, REQ-045.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: MAN-MEDICAL, MAN-SOP. [출처 등록부](../reference/sources.json).

---

## CS-OPS.06 · 다축 원인·교착·형식 모델 대조

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-OPS.06 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Reason {axis,code,subject,causes,evidence,resolutionConditions}; 설명 모델은 권한·결과를 변경하지 못함.

### 필수 상세 명세
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [03-durable-operations](../specs/03-durable-operations.md)
- [10-observability-and-study](../specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Domain/ReasonEngine.cs`
- `Assets/ChooGuard/Domain/WaitForAnalyzer.cs`
- `benchmarks/operations/model-projection.json`

### 선행 산출물과 소비 단계
- `CS-OPS.05:candidate` → `CS-OPS.06:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 진행상태와 원인축을 별개로 출력하며 다수 원인을 보존한다.
2. wait-for graph에서 cycle을 탐지하되 외부 도착 예정/불충분 정보와 확정 교착을 구별한다.
3. TAPAAL에 투영 가능한 부분의 trace를 비교하고 timeout·범위 제한은 미검증으로 보고한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-OPS.06-P**

Given: taskA→B→A wait graph  
When: 대기 분석  
Then: cycle 설명; 미래 외부자원과 분리  
Result: NOT_RUN

**TEST-CS-OPS.06-N**

Given: 등록되지 않은 원인 code  
When: 대기 분석  
Then: UNKNOWN_REASON_CODE  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/OPS/Ops06Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-013, REQ-030, REQ-041.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: TECH-TAPAAL. [출처 등록부](../reference/sources.json).

---

## CS-OPS.07 · 사람 작업량·도움·계산 대기 계측

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-OPS.07 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** ActivityInterval {participantCode, activityId, category, startMonotonicUs, endMonotonicUs, observedWallAt, consentRef, assistance, completeness}; 가상시각을 사람 시간으로 집계하지 않음.

### 필수 상세 명세
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [03-durable-operations](../specs/03-durable-operations.md)
- [10-observability-and-study](../specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Application/ActivityRecorder.cs`
- `Assets/ChooGuard/Persistence/ActivityStore.cs`
- `Assets/ChooGuard/Contracts/ActivityTypes.cs`

### 선행 산출물과 소비 단계
- `CS-OPS.02:candidate` → `CS-OPS.07:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 동의한 가명 참가자에 대해 작업 시작·전환·종료를 기록한다. 전역 키로거나 문장 원문 수집은 하지 않는다.
2. 동일 참가자의 중첩 구간은 합집합으로 계산하고 기관 회신 대기·무인 계산은 별도 열에 둔다. 강제종료의 열린 구간은 검열된 구간으로 남긴다.
3. UI의 입력·복기·출력 수정과 개발자 도움을 빠짐없이 같은 caseId에 결속하고 CSV/JSON으로 내보낸다. 동의 철회·보존정책을 적용한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-OPS.07-P**

Given: 한 사람 [0,10),[5,15)분; 다른사람[0,10)  
When: 작업량 집계  
Then: 합25인분; 같은사람15분  
Result: NOT_RUN

**TEST-CS-OPS.07-N**

Given: 종료 없는 interval  
When: 작업량 집계  
Then: CENSORED; 완료자 평균에서 숨김0  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/OPS/CSOPS07Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-079.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).
