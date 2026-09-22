# 실행 인터페이스 및 데이터 계약

**설계 계약 / 1.0.0.** 이 문서는 현재 저장소에 존재하는 API 목록이 아니다. `schemas/`와 `examples/`는 읽기 가능한 wire 계약이며 `interfaces/ArchitecturePorts.cs`는 경계 스케치다. C# DTO 생성·컴파일·Unity binding은 EP01/02/04의 산출물이다. 예제의 모든 값·해시는 synthetic fixture이며 실제 현장 검증 증거가 아니다.

## 1. 경계별 호출

| Port / 호출 | 요청의 의미 | 응답 및 금지 |
|---|---|---|
| ICommandPort.Preview | 읽기 집합·권한·자원·예상 영향의 현재 검사 | PreviewToken 및 각 대상 Reason; 예약·수행을 변경하지 않음 |
| ICommandPort.Submit | requester가 acting agency/team의 작업을 요청 | durable Receipt. ACCEPTED는 작업 완료가 아님 |
| IOperationsReadPort.Query | 분석 또는 기관 관측 scope의 현재 투영 | committedSequence/asOfTicks. hidden state 누설 금지 |
| IAtomicCommitPort.Commit | candidate events/receipt/reservations/outbox의 원자 기록 | commitSeq와 durable acknowledgement; 실패 시 상태 미게시 |
| IWorkerPort.Submit | 고정된 inputHash로 격리된 계산을 실행 | job handle, result manifest; Unity/SQLite write 권한 없음 |
| IBranchPort.Prepare | 수락 경계까지 정지하고 완전성 검사 | CheckpointCapabilities; 아직 child run을 만들지 않음 |
| IBranchPort.Fork | 고정 parent cutoff와 변경 계획으로 child 생성 | 새 runId/epoch/plan revision. parent 불변 |
| IAuthoringPort.Propose | 허용된 EvidencePack에서 초안 제안 | DraftPatch + references, 실행 명령 아님 |
| Review command | 인증된 검수자의 한 revision에 대한 판단 | decision/evidence/scope/authorityRef, 원문 문자열과 분리 |

동일 작업은 프로세스 내부에서도 같은 의미 계약을 쓴다. UI 상태의 예상 revision과 서버 저장 revision을 비교한다. APP→OPS 호출을 통해 읽기 집합을 확인하고, commit을 기다리는 동안 해당 session의 다른 mutation을 진행하지 않는다. 느린 DB 호출은 UI thread를 막지 않으며 mailbox에 backpressure를 적용한다. 저장을 맡은 스레드와 도메인 소유 스레드가 같은 데이터를 병렬로 수정하지 않는다.

## 2. 명령 지문과 재전송

키는 `(runId, requesterId, intentId)`이다. fingerprint는 wire bytes가 아니라 versioned semantic encoding이다. 필드 순서를 schema별로 고정하고 문자열은 UTF-8 length-prefixed, 집합은 ordinal 정렬, 순서가 의미인 배열은 보존한다. 가상 시간은 signed Int64 decimal string이며 범위를 검사한다. JSON float 텍스트 표현이나 locale을 그대로 hash에 넣지 않는다. enum·단위·expected revision·acting identity·payload는 포함하고 createdAt 같은 전달 메타데이터는 제외한다.

같은 key+같은 fingerprint는 저장된 receipt를 반환한다. key가 같고 fingerprint가 다르면 `ID_CONFLICT`다. `PENDING_DURABILITY`는 아직 승인하지 않았다는 상태이며 UI가 자원을 소유한 것으로 그리지 않는다. accepted receipt가 lost된 경우 QueryReceipt로 재조회한다. 운영계약 v1의 event chain은 legacy SHA 직렬화와 별도 버전으로 관리하며 기존 hash를 조용히 바꾸지 않는다.

## 3. 로컬 worker protocol v1

4바이트 unsigned little-endian 길이 + UTF-8 JSON body. 최대 body 1 MiB. JSON 파싱 전 길이·유효 UTF-8·depth·수량 한도를 검사한다. stdout은 protocol 전용이고 진단은 stderr다. 부모가 생성한 pipes와 임시 작업 directory만 전달한다. endpoint·실행파일·argv는 allowlist이며 사용자 문서/LLM에서 만들지 않는다.

공통 envelope: `protocolMajor, protocolMinor, messageId, correlationId, runId, workerId, workerEpoch, inputHash, kind, payload`. 메시지 종류는 `HELLO / CAPABILITIES / SUBMIT / ACCEPTED / PROGRESS / RESULT / RESULT_ACK / CANCEL / CANCEL_ACK / HEARTBEAT / ERROR / SHUTDOWN`으로 제한한다. `commandId`, `jobId`, `resultId`는 역할이 다른 ID다.

HELLO→버전/프로파일/nonce 확인→CAPABILITIES 검증→SUBMIT 순서다. bootstrap token은 argv나 public log가 아니라 private pipe로 전달한다. minor unknown 필드는 협상된 schema에서만 허용하고 major mismatch는 종료한다. retry는 transient 전송/파일 실패에만 최대 2회라는 초기 제안을 둔다. validation 오류, 권한 오류, OOD에는 재시도로 통과를 시도하지 않는다.

`CANCEL_ACK`는 실제 정지 경계와 현재 checkpoint 가능성을 포함해야 한다. timeout만으로 안전 취소를 확정하지 않는다. stale result는 `(runId, epoch, jobId, inputHash, boundaryRevision)`과 허용 시간구간을 검사한 뒤 quarantine한다. worker가 과거 결과를 다시 보낼 때도 unique result가 효과를 두 번 게시하지 않는다.

## 4. 큰 수치 payload와 좌표

제어 JSON과 numeric arrays는 분리한다. 원본 배열 descriptor는 `artifactHash, bytes, dtype, shape, order, byteOrder, compression, unit, frameId, spatialSupportId, startTicks, endTicks, sampleTicks, validity, uncertaintyRef`를 갖는다. 초기 교환은 float32/float64/int32/int64의 little-endian, row-major C-order, 무압축 raw binary+JSON sidecar를 기본으로 한다. 원본 solver의 HDF5/VTK/NPZ 등은 변환 worker가 읽으며 Unity에 해당 포맷 parser를 강제하지 않는다.

shape product와 dtype 크기가 실제 byte length와 일치해야 한다. NaN/Infinity는 필드별로 명시한 missing mask 외에는 거부한다. 단위변환·좌표변환·보간·reduction은 별도의 versioned transfer operator로 기록한다. scalar sensor, grid cell, face flux, particle trajectory를 같은 spatialSupport로 처리하지 않는다.

Time 구간은 `[fromTicks, toTicks]`의 endpoint 의미와 sample placement를 profile에 기록한다. 저장은 정수 clock, solver API 입력은 해당 quantum으로 변환한 seconds. time quantum이 1µs여도 모델 정확도가 1µs라는 뜻은 아니다. 기존 tick mapping을 잃지 않는다.

canonical geometry는 현장 ENU double과 CRS/origin을 보관하며 Unity 렌더 좌표는 E,U,N이다. 이 변환의 handedness·triangle winding·normal·rotation을 비대칭 기준체로 검사한다. 차량 local frame의 parent transform과 observation 시점도 같이 보존한다.

## 5. worker capability와 profile

capability는 실행 코드에서 probe하고 lock한다. `executionKind`, `step`, `snapshot`, `restore`, `earlyReturn`, `restartPolicy`, `ownedQuantities`, `inputGeometry`, `boundaryMutation`, `determinismClass`, `versionHash`, `resourceBudget`를 포함한다. schema가 있는 것만으로 runtime 지원이 입증되지 않는다.

FDS adapter의 기본은 BATCH_REFERENCE다. 임의 연속 step/rollback/live door change를 전제로 쓰지 않는다. 실제 profile에서 허용한 입력 이력으로 offline 검증한다. 주 런타임에서 해당 현상의 정량 예측이 필요하면 검증된 stateful adapter/ROM과 비교 근거가 있어야 한다. 없으면 계산 대기 또는 정량 평가 비활성화를 표시한다.

`EXACT_STATE`는 필수 state를 모두 보존한다는 뜻이다. bitwise replay와 동의어가 아니다. exact/replay/none이 체크포인트 정책이고, bitwise/numerical/semantic/statistical은 별도 동등성 검사 수준이다. `REPLAY_FROM_START`는 replay recipe·입력 이력·이전 판본·재실행 비용이 필요하다.

## 6. SiteBundle과 ModelLock

SiteBundle의 최소 산출물은 `site.manifest.json`, `entities.json`, `frames.json`, `topology.json`, `prefab-bindings.json`, `calculation-geometry.manifest.json`, `rule-bundle.json`, `model-lock.json`, `source-index.json`, `template-profile.json`, `qualification-index.json`이다. 이 목록은 실제 확보된 파일 목록이 아니라 EP01/03의 예정 산출물이다.

manifest는 각 항목의 상대 경로·content hash·media type·source revision·source qualification을 고정한다. 임의 외부 URL을 열어 runtime에 파일을 실행하는 bundle은 허용하지 않는다. optional media와 필수 calculation geometry를 구분하고 필수 입력 누락 시 지원하지 않는 주장을 명시한다. 무료 시각 asset이 교체돼도 physicalAssetId와 계산 형상이 유지돼야 하며 실제 비례가 달라지면 validation 영향이 있다.

ModelLock은 code/weights/config/parameter/geometry/boundary/transfer-operator/replay profile와 환경을 묶는다. 업데이트는 새 lock, 기존 run은 이전 lock을 유지한다. unsupported field를 optional로 바꿔 숨기지 않는다.

## 7. 데이터베이스 트랜잭션 경계

`database/schema.sql`은 설계 DDL이다. 실제 Unity SQLite native provider·마이그레이션·암호화·power-loss durability 구현은 포함하지 않는다. commit batch는 ledger insert, last_sequence update, resource_hold 변경, receipt insert, outbox insert를 한 transaction에서 수행한다. 낡은 last_sequence는 compare-and-swap 조건으로 막는다. SQLite 한 writer만 있으므로 worker 결과와 사용자 명령도 serialize한다.

outbox 전송은 DB commit 뒤다. worker_result와 field artifact를 검증한 후 result accepted event와 projection revision을 원자 게시한다. 미승인 numeric result를 UI 또는 다른 worker의 현재 입력으로 사용하지 않는다. 임시 blob flush/publish 뒤 DB 참조를 만들며, DB commit 실패의 orphan blob만 GC 대상으로 둔다.

백업과 아카이브는 DB만 복사하지 않고 consistent DB snapshot, blob inventory, schemas, ModelLock, source/review scope를 결속한다. complete checkpoint라고 쓰여 있어도 참조 blob이 없으면 복구를 거부한다.

## 8. 계약 검사와 제품 검사 구분

JSON Schema는 구조만 검증한다. checkpoint requiredWorkerIds의 집합, 동일 입력·time, worker capability 일치, 상태 파일 존재·hash, source scope와 review authority, 유효한 action-role 조합은 별도 semantic validator가 검사한다. schema 통과가 물리·매뉴얼·기관 승인이 아니다.

예제 10개는 문서 이해와 validator regression 전용이며 실제 파일·매뉴얼·solver·사용자 행동을 대표하지 않는다. 외부 실행·Unity 컴파일·기관 승인 테스트는 인수시험 명세로만 제공한다.
