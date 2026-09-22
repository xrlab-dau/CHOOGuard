using System.Threading;
using System.Threading.Tasks;

namespace ChooGuard.Contracts
{
    /// <summary>
    /// K02 운영 경계. 구현은 Unity API 없이 immutable DTO를 소비한다.
    /// 취소는 시작 전 대기를 중단할 수 있으나 commit 후 취소/응답 유실은 rollback이 아니다.
    /// 호출자는 새 key를 발급하지 않고 동일 ReceiptKey로 조회/재전송한다.
    /// 이 선언은 저장소·운영 backend 또는 취소/commit 원자성을 구현하지 않는다.
    /// </summary>
    public interface IOperationsPort
    {
        /// <summary>도메인 상태, 예약, outbox에 side effect 없이 preview한다.</summary>
        Task<PreviewResult> PreviewAsync(CommandIntent intent, CancellationToken cancellation);
        /// <summary>key별 의미 fingerprint를 비교하고 유효한 거부도 durable receipt로 보존한다.</summary>
        Task<CommandReceipt> SubmitAsync(CommandIntent intent, CancellationToken cancellation);
        /// <summary>runId/requesterId/intentId 모두로 조회한다. 저장 실패를 성공처럼 캐시하지 않는다.</summary>
        Task<ReceiptLookup> ReadReceiptAsync(ReceiptKey key, CancellationToken cancellation);
        /// <summary>backend가 requester의 viewScope/agency 접근 권한을 확인한다. UI 적용은 main thread에서 한다.</summary>
        Task<SessionProjection> ReadProjectionAsync(ProjectionQuery query, CancellationToken cancellation);
    }

    public interface IOutboxClock
    {
        System.DateTimeOffset UtcNow { get; }
    }

    public interface IOutboxStore
    {
        // Null means no eligible job. Lease must be positive and at most five minutes.
        Task<OutboxDelivery> ClaimAsync(StableId runId, StableId ownerId, System.TimeSpan lease, CancellationToken cancellation);
        Task<bool> AcknowledgeAsync(OutboxDeliveryAck acknowledgement, CancellationToken cancellation);
    }

    /// <summary>Must resolve the verified OutboxRef and return an explicit remote delivery acknowledgement.
    /// Completion alone is not acceptance; simulation results are a separate protocol.</summary>
    public interface IOutboxTransport
    {
        Task<OutboxDeliveryAck> DeliverAsync(OutboxDelivery delivery, CancellationToken cancellation);
    }

    /// <summary>저장 경계 선언만 제공한다. commit 이후 취소는 durable 효과를 되돌리지 않는다.</summary>
    public interface IRunStore
    {
        Task<CommitReceipt> CommitAsync(CommitBatch batch, CancellationToken cancellation);
    }
}
