# K05 · 계산 워커 IPC·시간·교환·물리 범위
**소유:** CS-SIM.01–.05. FDS·JuPedSim·SUMO·학습모델을 동일 capabilities로 가장하지 않는다.

## local protocol v1
stdio UTF-8 JSONL을 기본으로 고정한다. max line=1 MiB, max inline payload=64 KiB, max depth=32, immutable large blob reference를 사용한다. 로그는 stderr에만 쓰고 rotation/최대크기를 제한한다. line 크기를 allocation 전에 제한하고 newline 전 EOF·duplicate key·unknown kind·binary garbage를 거부한다. 네트워크 통신은 별도 transport ADR이며 localhost HTTP를 묵시적으로 열지 않는다.

메시지 종류: HELLO, CAPABILITIES, SUBMIT, ACCEPTED, RESULT, CANCEL, CANCELLED, HEARTBEAT, ERROR. protocolVersion과 worker/model revision을 handshake한다. retry마다 attemptId는 달라도 semantic jobId+inputDigest는 같다. job 결과는 runId/generation/workerId/modelRef/inputDigest/boundaryRevision/interval/units/frame/fieldOwner를 함께 확인한다.

개발용 handshake deadline=10s, heartbeat interval=2s, stale threshold=10s를 초기 설정값으로 둔다. solver의 실제 최대 실행시간은 job profile에서 별도 지정한다. 기준 계산이 오래 걸린다고 heartbeat TTL을 물리 실패 판정으로 재사용하지 않는다. 이 숫자는 안전 기준이나 현장 응답시간이 아니다.

## 계산 상태와 취소
QUEUED→RUNNING→RESULT_STAGED→VALIDATED→COMMITTED 또는 FAILED/CANCELLED. 취소는 의미 run generation을 fence하고 late RESULT가 도착해도 현재 상태를 덮지 못한다. worker cancel 미지원이면 subprocess를 회수하고 마지막 유효 checkpoint로 돌아간다. 운영 코어가 취소한 자원을 과거 결과가 다시 점유할 수 없다.

## coordinator 한계
각 physical field의 owner를 하나로 둔다. 정수 microsecond로 exchange boundary를 표시하되 solver의 안정 dt는 현상과 수렴시험으로 고정한다. 각 field의 valid interval을 확인하고 stale 이전 장을 현재로 반복 표시하지 않는다. 문 변경 같은 discontinuity마다 새 boundaryRevision을 만든다. 모든 required participant의 결과가 ready이고 잔차·단위·범위를 통과해야 common boundary를 publish한다.

외부 worker process는 DB transaction의 일부가 아니다. 한 워커만 전진했다가 다른 워커가 실패하면 checkpoint/replay로 공통 경계를 회복해야 한다. snapshot 없는 워커에 rollback 명령을 만들어 보내지 않는다. FMI 적용은 adapter가 실제 지원할 때만 가능하다. [AUD-FMI]

## 현상별 결정
FDS: batch reference 기본. 경계 변경 이력·geometry·material·grid·solverLock·QoI를 입력으로 고정하고 mesh/time convergence·독립 관측을 분리한다. FDS와 신경모델 일치가 현실 검증은 아니다.
JuPedSim: 기존 모델과 후보를 같은 관측/geometry로 비교한다. 계단·승강기·보조 이동에 대한 별도 조건과 교환 model을 명시한다.
SUMO: 양방향 traffic/운영 정보 소유권과 shortcut/teleport 발생을 기록한다. 미검증 접근시간을 실제 출동시간으로 출력하지 않는다.
ROM/surrogate: 필수 가속 도구가 아니라 선택 구현. material/geometry/action history 범위를 넘어가면 reference 계산·대기·평가 보류로 전환한다. validation set을 인접 영상 frame으로 누출하지 않는다.

미지원 현상을 초기 문제에서 제외하려면 필요한 QoI에 영향을 주지 않는 근거를 기록한다. 중요한 현상을 빼고 높은 정확도 명칭을 유지하지 않는다. 실시간 체감과 정확도는 각각 검증하며 fixed gameplay timer를 정밀 물리 계산으로 승격하지 않는다.

## 추가 경계 계약
SimulationJob.outputContract={fieldId,ownerWorkerId,unit,frameId}를 고정한다. FieldBatch.fieldId는 수량 ID이고 fieldOwner는 그 수량을 쓰는 worker ID다. 두 개념을 혼용하지 않는다. JSON enum에 있는 유효 단위라도 해당 job의 출력 단위와 다르면 거부한다. coordinate/frame 변환이 필요하면 검증된 변환기를 명시적으로 적용한 새 결과를 만든다.
