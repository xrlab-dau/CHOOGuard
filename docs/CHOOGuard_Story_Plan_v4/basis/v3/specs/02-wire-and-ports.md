# K02 · 타입·직렬화·비동기 API 계약
**소유:** CS-BOOT.02/CS-PACK.01, worker 확장은 CS-SIM.01. 메시지의 정본 구조는 `schemas/*.schema.json`이다.

## 공통 규칙
ID는 ASCII 1–128자 `^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$`; 표시이름·한글은 별도 필드다. 시간 tick은 64bit 비음수 microsecond, wall clock은 UTC RFC3339이다. Sequence는 사건 순서이며 시간이 아니다. 물리 timestep은 tick 정밀도와 다르고 수치 수렴으로 선택한다. wire 숫자의 NaN/Infinity, 중복 JSON key, 알 수 없는 필드는 거부한다. 의미 비교에서 배열의 순서가 규칙상 중요한지 명시하고 무조건 정렬하지 않는다.

## 포트 표면 (신규 C# 구현 목표)
```csharp
Task<PreviewResult> PreviewAsync(CommandIntent intent, CancellationToken cancellation);
Task<CommandReceipt> SubmitAsync(CommandIntent intent, CancellationToken cancellation);
Task<ReceiptLookup> ReadReceiptAsync(ReceiptKey key, CancellationToken cancellation);
Task<SessionProjection> ReadProjectionAsync(ProjectionQuery query, CancellationToken cancellation);
```
ReceiptKey={runId,requesterId,intentId}; 동일 run 내 다른 요청자의 같은 intentId는 충돌하지 않는다. ReceiptLookup={found,receipt}; found=false이면 receipt=null. PreviewResult={key,readSet,targetResults,proposedEffectsRef,previewExpiresAtRevision}; reasons는 targetResults 안에 있다; preview는 도메인·예약·outbox에 side effect가 없다. SessionProjection={runId,revision,simTick,viewScope,agencyId,entitiesRef,tasksRef,reasonsRef}; ProjectionQuery={runId,requesterId,viewScope,agencyId,afterRevision}. viewScope=AUTHOR_ANALYSIS 또는 AGENCY_KNOWLEDGE; backend가 requester의 허용 범위를 확인한다.

CancellationToken의 취소는 아직 시작하지 않은 요청의 대기를 중단할 수 있다. commit 뒤 응답 전 취소·연결 손실은 rollback을 뜻하지 않는다. 호출자는 같은 ReceiptKey를 조회/재전송하며 새 key로 묻지 않는다. 호출자 객체는 boundary에서 deep copy/immutable value로 고정하고 worker 스레드가 Unity API를 호출하지 않는다. UI 갱신은 main thread의 DTO projection으로만 수행한다.

## 요청·오류
CommandIntent의 fingerprint는 key를 제외한 의미 입력을 규칙화한 UTF-8 JSON에서 SHA-256으로 만든다. schemaVersion·commandKind·actor·targets·payloadRef·readSet을 포함하고 authoredAt·network attempt 같은 전송 부가정보는 제외한다. key별 최초 의미 fingerprint를 저장한다. 오래된 readSet은 STALE_STATE; 알 수 없는 target은 UNKNOWN_TARGET; 역할 부적합은 AUTHORITY_DENIED; 자원 부족은 RESOURCE_UNAVAILABLE; 같은 key 다른 fingerprint는 INTENT_CONFLICT. 오류는 구조화된 Reasons로 반환한다. 서버가 판정하지 않은 내부 exception 문자열을 권고로 제시하지 않는다.

처음 보는 malformed 메시지는 저장 전 거부할 수 있다. 의미상 거부된 유효 요청은 rejected receipt를 저장해 재전송에서 일관성을 유지한다. 저장 자체 실패에는 durable receipt가 없으므로 PERSISTENCE_UNAVAILABLE 응답을 성공처럼 캐시하지 않는다. ACCEPTED에는 commitId와 sequence가 반드시 있고 실제 업무 완료 여부는 후속 이벤트로만 전달한다.

## wire/semantic 검증의 분리
JSON Schema는 타입과 필수항목을 확인한다. 관계 검사기는 key 일치, revision, referenced ID 존재, 중복 team/target, inputDigest, tick 범위, checkpoint worker 완전성, evidence의 주장 범위를 추가 검사한다. schema 통과만으로 실행/현실 자격을 부여하지 않는다.

## 저장 포트
`IRunStore.CommitAsync(CommitBatch, CancellationToken) -> Task<CommitReceipt>`를 신규 인터페이스로 정의한다. CommitReceipt={runId,commitId,revision,intentReceipt,dispatchableJobs}; receipt key의 runId/commitId/sequence가 outer commit과 일치해야 한다. Request 취소는 commit 이후 durable 효과를 되돌리지 않는다. ReadReceiptAsync는 ReceiptLookup={found,receipt}를 반환하며 found와 nullability가 일치해야 한다.
