# CS-SIM · 다중 물리·행동 계산과 정량 검증

[제품 기준](../PRODUCT_BASELINE.md) · [공통 계약](../CONTRACTS.md) · [검수 보고](../review/REVIEW.md)

## CS-SIM.01 · 새 worker 프로토콜·시험 워커

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SIM.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** WorkerCapability, SimulationJob, FieldBatch; 임의 shell 문자열을 입력으로 실행하지 않는다.

### 필수 상세 명세
- [05-worker-and-cosimulation](../specs/05-worker-and-cosimulation.md)
- [04-checkpoint-and-comparison](../specs/04-checkpoint-and-comparison.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

### 새 구현 경로
- `Assets/ChooGuard/Contracts/WorkerTypes.cs`
- `Assets/ChooGuard/Simulation/WorkerProcessClient.cs`
- `workers/fixture/worker.py`
- `workers/protocol/worker.schema.json`

### 선행 산출물과 소비 단계
- `CS-BOOT.02:candidate` → `CS-SIM.01:integration` / 조건 `ALWAYS`.
- `CS-PACK.01:candidate` → `CS-SIM.01:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 로컬 child process 표준입출력에 UTF-8 JSONL을 사용한다. 1줄 최대 1 MiB, 깊이 32, inline payload 64 KiB, 큰 장은 allowlisted relative blobRef로 전달한다. stdout은 protocol 전용, stderr는 bounded log다. 프로세스는 검수된 executable·argv만 실행한다.
2. capability에 현상·단위·time-step·batch/interactive·checkpoint·검증영역을 선언한다.
3. worker timeout/exit/corrupt/late result/다른 branch 응답을 검사한다. 시험 워커는 SYNTHETIC_FIXTURE이며 실제 모델로 승격하지 않는다.
4. requestId·jobId·attemptId·runId·generation·inputDigest를 확인하고 다음 correlation이 없는 결과는 격리한다. 취소 후 generation이 이전인 결과는 publish하지 않는다.
5. worker handle은 OS process 종료·timeout과 메모리/출력 한도를 검사한다. 이런 제한은 sandbox 보장을 뜻하지 않으므로 임의 내려받은 코드를 실행하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SIM.01-P**

Given: JSONL 1줄+정상 correlation  
When: worker 결과 수신  
Then: job/run/generation/input 일치  
Result: NOT_RUN

**TEST-CS-SIM.01-N**

Given: 1MiB 초과 또는 duplicate key  
When: worker 결과 수신  
Then: PROTOCOL_REJECT  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SIM/Sim01Tests.cs` · NOT_RUN
- WORKER_INTEGRATION: `benchmarks/tests/CS-SIM.01.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-049.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: AUD-FMI. [출처 등록부](../reference/sources.json).

---

## CS-SIM.02 · 보행·층간 연결 어댑터

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SIM.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** PedestrianState {entityId,frameId,position,velocity,tick,modelRef}; 독립 관측 전에는 현장 정확도 미판정.

### 필수 상세 명세
- [05-worker-and-cosimulation](../specs/05-worker-and-cosimulation.md)
- [04-checkpoint-and-comparison](../specs/04-checkpoint-and-comparison.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

### 새 구현 경로
- `Assets/ChooGuard/Simulation/PedestrianAdapter.cs`
- `workers/pedestrian/runner.py`
- `benchmarks/pedestrian/protocol.json`

### 선행 산출물과 소비 단계
- `CS-SIM.01:candidate` → `CS-SIM.02:integration` / 조건 `ALWAYS`.
- `CS-WORLD.01:candidate` → `CS-SIM.02:integration` / 조건 `ALWAYS`.
- `CS-WORLD.02:candidate` → `CS-SIM.02:qualification` / 조건 `NAMED_SITE_ACCURACY`.

### 구현 절차
1. JuPedSim 등의 기준·후보를 동일 geometry·독립 현상 데이터로 비교한다.
2. 위치의 정본은 한 엔진에만 둔다. Unity의 표시·회피 로직과 이중 소유하지 않는다.
3. 계단/승강기/보조 이동은 지원 범위를 별도 기술하고 기본 보행모델의 정확도를 자동 상속하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SIM.02-P**

Given: 동일 geometry,독립 pedestrian 관측  
When: adapter 비교  
Then: 위치 owner1; holdout별 오차  
Result: NOT_RUN

**TEST-CS-SIM.02-N**

Given: Unity 별도 위치갱신  
When: adapter 비교  
Then: DUPLICATE_FIELD_OWNER  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SIM/Sim02Tests.cs` · NOT_RUN
- WORKER_INTEGRATION: `benchmarks/tests/CS-SIM.02.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-019, REQ-043.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: TECH-JPS14, TECH-JUPED, TECH-PEDDATA. [출처 등록부](../reference/sources.json).

---

## CS-SIM.03 · 접근교통·차량 운행 어댑터

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SIM.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** TrafficState {vehicleId,crewIds,routeId,frameId,tick,provenance}; 철도 운행은 충돌/탈선 동역학과 다름.

### 필수 상세 명세
- [05-worker-and-cosimulation](../specs/05-worker-and-cosimulation.md)
- [04-checkpoint-and-comparison](../specs/04-checkpoint-and-comparison.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

### 새 구현 경로
- `Assets/ChooGuard/Simulation/TrafficAdapter.cs`
- `workers/traffic/runner.py`
- `benchmarks/traffic/protocol.json`

### 선행 산출물과 소비 단계
- `CS-SIM.01:candidate` → `CS-SIM.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. SUMO 등 후보에 현지 경로·가용성·차량·운행조건을 결속한다.
2. 정량 프로파일에서는 teleport·숨은 우회 등을 탐지하고 해당 실행의 자격을 제한한다.
3. 해당 QoI가 필요 없는 과제는 명시적 도착 시나리오 입력을 쓸 수 있지만 실제 출동시간 예측으로 부르지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SIM.03-P**

Given: 입력 route/model lock  
When: traffic 실행  
Then: 사례범위 및 shortcut 기록  
Result: NOT_RUN

**TEST-CS-SIM.03-N**

Given: teleport 사용 실행을 정상예측 표시  
When: traffic 실행  
Then: UNQUALIFIED_TRAFFIC_RESULT  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SIM/Sim03Tests.cs` · NOT_RUN
- WORKER_INTEGRATION: `benchmarks/tests/CS-SIM.03.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-044.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: TECH-SUMO, TECH-SUMO-RAIL. [출처 등록부](../reference/sources.json).

---

## CS-SIM.04 · 화재·열·연기 기준 계산

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SIM.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** HazardRun {inputHash,solverLock,boundaryHistory,grid,qoi,resultRefs,validation}; 실제 관측없는 기준계산 일치는 현실 검증이 아님.

### 필수 상세 명세
- [05-worker-and-cosimulation](../specs/05-worker-and-cosimulation.md)
- [04-checkpoint-and-comparison](../specs/04-checkpoint-and-comparison.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

### 새 구현 경로
- `Assets/ChooGuard/Simulation/HazardReferenceAdapter.cs`
- `workers/hazard/reference_runner.py`
- `benchmarks/hazard/reference-protocol.json`

### 선행 산출물과 소비 단계
- `CS-SIM.01:candidate` → `CS-SIM.04:integration` / 조건 `ALWAYS`.
- `CS-WORLD.02:candidate` → `CS-SIM.04:qualification` / 조건 `NAMED_SITE_ACCURACY`.

### 구현 절차
1. 고정된 FDS 실행 버전과 물성·형상·개구·열원·경계·관측을 검사한다.
2. 기본 경로는 batch reference이며 live rollback을 지원한다고 가정하지 않는다. 수렴·독립 데이터·QoI를 별도 평가한다.
3. 필요 기준을 실행 전에 고정한다. 영상 전환·VFX를 온도/농도 데이터로 사용하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SIM.04-P**

Given: 고정 grid/material/history  
When: reference 계산  
Then: QoI와 수렴/관측 역할 분리  
Result: NOT_RUN

**TEST-CS-SIM.04-N**

Given: FDS 결과만으로 현장검증 선언  
When: reference 계산  
Then: NO_INDEPENDENT_OBSERVATION  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SIM/Sim04Tests.cs` · NOT_RUN
- WORKER_INTEGRATION: `benchmarks/tests/CS-SIM.04.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-042.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: TECH-FDS, TECH-FDS-SCOPE. [출처 등록부](../reference/sources.json).

---

## CS-SIM.05 · 공동시간·불확도·가속·정직한 복구

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SIM.05 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** CommittedBoundary {tick,sequence,inputs,fieldOwners,resultHashes,capabilities}; 독립 reference/모델 결과는 runtime schema와 별도로 qualification.

### 필수 상세 명세
- [05-worker-and-cosimulation](../specs/05-worker-and-cosimulation.md)
- [04-checkpoint-and-comparison](../specs/04-checkpoint-and-comparison.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

### 새 구현 경로
- `Assets/ChooGuard/Simulation/CosimulationCoordinator.cs`
- `Assets/ChooGuard/Simulation/QuantityOwnership.cs`
- `Assets/ChooGuard/Simulation/ModelQualification.cs`
- `workers/hazard/surrogate_runner.py`
- `benchmarks/coupling/protocol.json`

### 선행 산출물과 소비 단계
- `CS-SIM.01:candidate` → `CS-SIM.05:integration` / 조건 `ALWAYS`.
- `CS-OPS.05:candidate` → `CS-SIM.05:integration` / 조건 `ALWAYS`.
- `CS-LAB.01:candidate` → `CS-SIM.05:integration` / 조건 `ALWAYS`.
- `CS-SIM.02:candidate` → `CS-SIM.05:integration` / 조건 `PEDESTRIAN_USED`.
- `CS-SIM.03:candidate` → `CS-SIM.05:integration` / 조건 `TRAFFIC_USED`.
- `CS-SIM.04:candidate` → `CS-SIM.05:integration` / 조건 `HAZARD_USED`.

### 구현 절차
1. run에서 사용하는 worker만 참여시켜 시간·단위·좌표·수량별 writer와 불연속 사건 동기화를 검사한다.
2. required worker 결과가 실패하면 경계를 commit하지 않는다. 복원이 안 되면 공통 유효 checkpoint/시작 상태부터 재계산한다.
3. 선택적 ROM/PhysicsNeMo 후보는 기준·관측·독립 형상에서 장기 roll-out·보존량·OOD를 검사한다. 가속모델이 없다는 이유로 필수 reference 경로를 삭제하지 않는다.
4. 훈련/비교에 쓰는 QoI가 요구하는 현상만 설치하되, 중요한 물리를 제외하고 정확도 주장을 유지하지 않는다.
5. UI 배속과 독립인 정수 microsecond tick으로 교환 경계를 만든다. 각 참여 solver의 안정 step과 valid interval을 유지하며 fresh barrier를 모두 통과한 경계만 원자 게시한다.
6. 문 변경 후 이전 inputDigest의 solver 결과는 거부한다. 실패시 모든 영향 worker를 마지막 공통 cut에 복원하거나 재실행하며 일부 전진 상태를 current로 쓰지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SIM.05-P**

Given: 두 worker 동일 경계 ready  
When: co-sim publish  
Then: single committed boundary  
Result: NOT_RUN

**TEST-CS-SIM.05-N**

Given: worker1전진,worker2실패  
When: co-sim publish  
Then: NO_PARTIAL_PUBLISH; 공통cut 복원  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SIM/Sim05Tests.cs` · NOT_RUN
- WORKER_INTEGRATION: `benchmarks/tests/CS-SIM.05.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-035, REQ-046, REQ-047, REQ-048, REQ-049.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: STD-FMI, TECH-MAPANYTHING, TECH-PHYSICSNEMO. [출처 등록부](../reference/sources.json).
