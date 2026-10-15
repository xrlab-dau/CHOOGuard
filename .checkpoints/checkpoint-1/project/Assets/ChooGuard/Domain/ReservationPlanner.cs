using System;
using System.Collections.Generic;
using ChooGuard.Contracts;

namespace ChooGuard.Domain
{
    internal static class ReservationValues
    {
        internal static StableId Id(StableId id)
        {
            if (!id.IsValid) throw new ArgumentException("초기화된 식별자가 필요합니다.");
            return id;
        }
        internal static string Unit(string unit) => new StableId(unit).Value;
        internal static long Nonnegative(long value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "비음수 값이 필요합니다.");
            return value;
        }
        internal static void Window(SimTick start, SimTick end)
        {
            if (start.Microseconds >= end.Microseconds) throw new ArgumentException("예약 구간은 시작보다 끝이 커야 합니다.");
        }
        internal static IReadOnlyList<ReservationDemand> Demands(IReadOnlyList<ReservationDemand> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new List<ReservationDemand>();
            var ids = new HashSet<StableId>();
            foreach (var demand in source)
            {
                if (demand == null || !ids.Add(demand.ResourceId)) throw new ArgumentException("자원 요구는 null 또는 중복일 수 없습니다.");
                copy.Add(demand);
            }
            if (copy.Count == 0) throw new ArgumentException("하나 이상의 자원 요구가 필요합니다.");
            return copy.AsReadOnly();
        }
    }

    public sealed class ReservationDemand
    {
        public StableId ResourceId { get; }
        public string Unit { get; }
        public long Quantity { get; }
        public ReservationDemand(StableId resourceId, string unit, long quantity)
        {
            ResourceId = ReservationValues.Id(resourceId);
            Unit = ReservationValues.Unit(unit);
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "양수 수량이 필요합니다.");
            Quantity = quantity;
        }
    }

    public sealed class ReservationResource
    {
        public StableId ResourceId { get; }
        public string Unit { get; }
        public long Capacity { get; }
        public ReservationResource(StableId resourceId, string unit, long capacity)
        {
            ResourceId = ReservationValues.Id(resourceId);
            Unit = ReservationValues.Unit(unit);
            Capacity = ReservationValues.Nonnegative(capacity);
        }
    }

    public sealed class ReservationRequest
    {
        public StableId ReservationId { get; }
        public StableId OwnerTaskId { get; }
        public SimTick Start { get; }
        public SimTick End { get; }
        public IReadOnlyList<ReservationDemand> Demands { get; }
        public ReservationRequest(StableId reservationId, StableId ownerTaskId, SimTick start, SimTick end, IReadOnlyList<ReservationDemand> demands)
        {
            ReservationId = ReservationValues.Id(reservationId);
            OwnerTaskId = ReservationValues.Id(ownerTaskId);
            ReservationValues.Window(start, end);
            Start = start; End = end;
            Demands = ReservationValues.Demands(demands);
        }
    }

    public sealed class ActiveReservation
    {
        public StableId ReservationId { get; }
        public StableId OwnerTaskId { get; }
        public long Version { get; }
        public SimTick Start { get; }
        public SimTick End { get; }
        public IReadOnlyList<ReservationDemand> Demands { get; }
        public bool IsActive { get; }
        public ActiveReservation(StableId reservationId, StableId ownerTaskId, long version, SimTick start, SimTick end,
            IReadOnlyList<ReservationDemand> demands, bool isActive)
        {
            ReservationId = ReservationValues.Id(reservationId);
            OwnerTaskId = ReservationValues.Id(ownerTaskId);
            Version = ReservationValues.Nonnegative(version);
            ReservationValues.Window(start, end);
            Start = start; End = end;
            Demands = ReservationValues.Demands(demands);
            IsActive = isActive;
        }
    }

    public sealed class ReservationCancellation
    {
        public StableId ReservationId { get; }
        public StableId OwnerTaskId { get; }
        public long Version { get; }
        public ReservationCancellation(StableId reservationId, StableId ownerTaskId, long version)
        {
            ReservationId = ReservationValues.Id(reservationId);
            OwnerTaskId = ReservationValues.Id(ownerTaskId);
            Version = ReservationValues.Nonnegative(version);
        }
    }

    public enum ReservationPlanStatus { Planned, Invalid, ReservationIdConflict, ResourceDeficit, ResourceUnavailable }
    public enum ReservationCancellationStatus { Ready, AlreadyInactive, Invalid, NotFound, OwnerMismatch, VersionMismatch }

    public sealed class ReservationPlanResult
    {
        public ReservationPlanStatus Status { get; }
        public string Code { get; }
        public IReadOnlyList<StableId> LockOrder { get; }
        public IReadOnlyList<ReservationRequest> ProposedReservations { get; }
        internal ReservationPlanResult(ReservationPlanStatus status, IList<StableId> locks, ReservationRequest request = null)
        {
            Status = status;
            Code = status == ReservationPlanStatus.ResourceDeficit ? "RESOURCE_DEFICIT" :
                status == ReservationPlanStatus.ResourceUnavailable ? "RESOURCE_UNAVAILABLE" :
                status == ReservationPlanStatus.ReservationIdConflict ? "RESERVATION_ID_CONFLICT" :
                status == ReservationPlanStatus.Invalid ? "INVALID" : "PLANNED";
            LockOrder = new List<StableId>(locks).AsReadOnly();
            var proposals = new List<ReservationRequest>();
            if (status == ReservationPlanStatus.Planned && request != null) proposals.Add(request);
            ProposedReservations = proposals.AsReadOnly();
        }
    }

    public sealed class ReservationCancellationResult
    {
        public ReservationCancellationStatus Status { get; }
        public ReservationCancellation Request { get; }
        internal ReservationCancellationResult(ReservationCancellationStatus status, ReservationCancellation request)
        { Status = status; Request = request; }
    }

    /// <summary>
    /// 상태를 저장하지 않는 후보 계산기. 호출자가 팀·운전자·장비를 서로 다른 stable ID로 확장해야 하며 roster를 추정하지 않는다.
    /// 자원 부족은 요청 구간 내 요청 자원에 대해 판정한다. 실제 lock/버전 재검사/commit/receipt는 영속 계층의 책임이다.
    /// 입력 목록은 호출 중 다른 스레드에서 변경하지 않아야 한다. 반환값은 commit 또는 취소 완료 증거가 아니다.
    /// </summary>
    public sealed class ReservationPlanner
    {
        private readonly struct Boundary
        {
            internal readonly long Tick;
            internal readonly long Quantity;
            internal readonly bool IsStart;
            internal Boundary(long tick, long quantity, bool isStart) { Tick = tick; Quantity = quantity; IsStart = isStart; }
        }

        public ReservationPlanResult Plan(ReservationRequest request, IReadOnlyList<ReservationResource> resources, IReadOnlyList<ActiveReservation> active)
        {
            var locks = new List<StableId>();
            if (request == null || resources == null || active == null) return new ReservationPlanResult(ReservationPlanStatus.Invalid, locks);
            foreach (var demand in request.Demands) locks.Add(demand.ResourceId);
            locks.Sort((a, b) => StringComparer.Ordinal.Compare(a.Value, b.Value));
            var resourceCopy = new List<ReservationResource>(resources);
            var activeCopy = new List<ActiveReservation>(active);
            var byResource = new Dictionary<StableId, ReservationResource>();
            foreach (var resource in resourceCopy)
            {
                if (resource == null || byResource.ContainsKey(resource.ResourceId)) return new ReservationPlanResult(ReservationPlanStatus.Invalid, locks);
                byResource.Add(resource.ResourceId, resource);
            }
            var reservationIds = new HashSet<StableId>();
            foreach (var reservation in activeCopy)
            {
                if (reservation == null || !reservationIds.Add(reservation.ReservationId) || !Matches(reservation.Demands, byResource))
                    return new ReservationPlanResult(ReservationPlanStatus.Invalid, locks);
            }
            if (!Matches(request.Demands, byResource)) return new ReservationPlanResult(ReservationPlanStatus.Invalid, locks);
            if (reservationIds.Contains(request.ReservationId)) return new ReservationPlanResult(ReservationPlanStatus.ReservationIdConflict, locks);

            bool unavailable = false;
            foreach (var demand in request.Demands)
            {
                var capacity = byResource[demand.ResourceId].Capacity;
                var events = new List<Boundary>();
                foreach (var reservation in activeCopy)
                {
                    if (!reservation.IsActive || reservation.End.Microseconds <= request.Start.Microseconds ||
                        reservation.Start.Microseconds >= request.End.Microseconds) continue;
                    foreach (var occupied in reservation.Demands)
                    {
                        if (!occupied.ResourceId.Equals(demand.ResourceId)) continue;
                        events.Add(new Boundary(Math.Max(request.Start.Microseconds, reservation.Start.Microseconds), occupied.Quantity, true));
                        events.Add(new Boundary(Math.Min(request.End.Microseconds, reservation.End.Microseconds), occupied.Quantity, false));
                    }
                }
                // 동일 tick에서는 종료부터 처리하므로 [start,end) 인접 예약은 충돌하지 않는다.
                events.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.IsStart.CompareTo(b.IsStart));
                long used = 0;
                if (demand.Quantity > capacity) unavailable = true;
                foreach (var boundary in events)
                {
                    if (!boundary.IsStart) { used -= boundary.Quantity; continue; }
                    // 더하기 전에 잔여량을 비교한다. 실제 점유의 long overflow도 기존 capacity 결손이다.
                    if (boundary.Quantity > capacity - used) return new ReservationPlanResult(ReservationPlanStatus.ResourceDeficit, locks);
                    used += boundary.Quantity;
                    if (demand.Quantity > capacity - used) unavailable = true;
                }
            }
            return new ReservationPlanResult(unavailable ? ReservationPlanStatus.ResourceUnavailable : ReservationPlanStatus.Planned, locks, request);
        }

        private static bool Matches(IReadOnlyList<ReservationDemand> demands, Dictionary<StableId, ReservationResource> resources)
        {
            foreach (var demand in demands)
                if (!resources.TryGetValue(demand.ResourceId, out var resource) || !StringComparer.Ordinal.Equals(demand.Unit, resource.Unit)) return false;
            return true;
        }

        public ReservationCancellationResult ValidateCancellation(ReservationCancellation request, IReadOnlyList<ActiveReservation> reservations)
        {
            if (request == null || reservations == null) return new ReservationCancellationResult(ReservationCancellationStatus.Invalid, request);
            var copy = new List<ActiveReservation>(reservations);
            var ids = new HashSet<StableId>();
            ActiveReservation target = null;
            foreach (var reservation in copy)
            {
                if (reservation == null || !ids.Add(reservation.ReservationId)) return new ReservationCancellationResult(ReservationCancellationStatus.Invalid, request);
                if (reservation.ReservationId.Equals(request.ReservationId)) target = reservation;
            }
            var status = target == null ? ReservationCancellationStatus.NotFound :
                !target.OwnerTaskId.Equals(request.OwnerTaskId) ? ReservationCancellationStatus.OwnerMismatch :
                target.Version != request.Version ? ReservationCancellationStatus.VersionMismatch :
                !target.IsActive ? ReservationCancellationStatus.AlreadyInactive : ReservationCancellationStatus.Ready;
            return new ReservationCancellationResult(status, request);
        }
    }
}
