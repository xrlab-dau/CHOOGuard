using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
namespace ChooGuard.App.Mvp
{
    // Reusable training kit capacity: reservation -> field use -> timed reconditioning.
    // Quantities are authored gameplay allocations, never actual institution inventory.
    public sealed class MvpFacilityResources : MonoBehaviour
    {
        [Serializable] private sealed class Definition { public int version;public string allocationBoundary;public Supply[] supplies; }
        [Serializable] private sealed class Supply { public string agencyId,agencyLabel,workType,label;public int capacity; }
        private sealed class Bin { public Supply Definition;public int Available,Committed,Consumed,TotalConsumed; }
        private sealed class Reservation { public string OperationId,AgencyId,TeamId,WorkType,State; }
        public sealed class StockSnapshot
        {
            public string AgencyId { get; } public string AgencyLabel { get; } public string WorkType { get; } public string Label { get; }
            public int Capacity { get; } public int Stock { get; } public int Committed { get; } public int Consumed { get; } public int TotalConsumed { get; } public int Revision { get; }
            public bool Available=>Stock>0;
            public string Reason=>Available?"지원 꾸러미 예약 가능":"지원 꾸러미 없음 · 사용 중인 꾸러미의 복귀·보충을 기다리세요";
            internal StockSnapshot(string agency,string agencyLabel,string work,string label,int capacity,int stock,int committed,int consumed,int total,int revision){AgencyId=agency;AgencyLabel=agencyLabel;WorkType=work;Label=label;Capacity=capacity;Stock=stock;Committed=committed;Consumed=consumed;TotalConsumed=total;Revision=revision;}
        }
        public sealed class ReservationSnapshot
        {
            public string OperationId { get; } public string AgencyId { get; } public string TeamId { get; } public string WorkType { get; } public string State { get; }
            internal ReservationSnapshot(string op,string agency,string team,string work,string state){OperationId=op;AgencyId=agency;TeamId=team;WorkType=work;State=state;}
        }
        public TextAsset ResourceAsset;
        public bool Ready { get; private set; }
        public string StatusReason { get; private set; }="기관 자원 자료 확인 전";
        public string ConfigHash { get; private set; }
        public int Revision { get; private set; }
        public int RunRevision { get; private set; }
        private readonly Dictionary<string,Bin> bins=new Dictionary<string,Bin>();
        private readonly Dictionary<string,Reservation> reservations=new Dictionary<string,Reservation>();
        private readonly object gate=new object();
        private static string Key(string agency,string work)=>agency+"|"+work;
        public IReadOnlyList<StockSnapshot> Stocks
        {
            get {lock(gate){var result=new List<StockSnapshot>();foreach(var b in bins.Values)result.Add(new StockSnapshot(b.Definition.agencyId,b.Definition.agencyLabel,b.Definition.workType,b.Definition.label,b.Definition.capacity,b.Available,b.Committed,b.Consumed,b.TotalConsumed,Revision));result.Sort((a,b)=>string.CompareOrdinal(Key(a.AgencyId,a.WorkType),Key(b.AgencyId,b.WorkType)));return result.AsReadOnly();}}
        }
        public IReadOnlyList<ReservationSnapshot> Reservations
        {
            get {lock(gate){var result=new List<ReservationSnapshot>();foreach(var r in reservations.Values)result.Add(new ReservationSnapshot(r.OperationId,r.AgencyId,r.TeamId,r.WorkType,r.State));return result.AsReadOnly();}}
        }
        private void Awake()=>Initialize();
        public bool Initialize()
        {
            lock(gate)
            {
                if(Ready)return true;
                try
                {
                    if(ResourceAsset==null)throw new InvalidOperationException();var definition=JsonUtility.FromJson<Definition>(ResourceAsset.text);
                    if(definition==null||definition.version!=1||definition.supplies==null||definition.supplies.Length<1||definition.supplies.Length>32)throw new InvalidOperationException();
                    var staged=new Dictionary<string,Bin>();foreach(var supply in definition.supplies)
                    {
                        if(supply==null||string.IsNullOrWhiteSpace(supply.agencyId)||string.IsNullOrWhiteSpace(supply.agencyLabel)||string.IsNullOrWhiteSpace(supply.label)||(supply.workType!="evacuation-support"&&supply.workType!="medical-support")||supply.capacity<1||supply.capacity>16||staged.ContainsKey(Key(supply.agencyId,supply.workType)))throw new InvalidOperationException();
                        staged.Add(Key(supply.agencyId,supply.workType),new Bin{Definition=supply,Available=supply.capacity});
                    }
                    using(var sha=SHA256.Create())ConfigHash=BitConverter.ToString(sha.ComputeHash(ResourceAsset.bytes)).Replace("-","").ToLowerInvariant();
                    bins.Clear();foreach(var pair in staged)bins.Add(pair.Key,pair.Value);Ready=true;StatusReason="기관 지원 꾸러미 준비됨";Revision++;return true;
                }
                catch {Ready=false;StatusReason="기관 지원 꾸러미 자료를 확인할 수 없습니다";return false;}
            }
        }
        public bool CanReserve(string agencyId,string workType,out string reason)
        {
            if(!Initialize()){reason=StatusReason;return false;}lock(gate){if(!bins.TryGetValue(Key(agencyId,workType),out var bin)){reason="이 기관에 해당 지원 꾸러미가 없습니다";return false;}if(bin.Available<1){reason=bin.Definition.label+" 없음 · 복귀 후 보충을 기다리세요";return false;}reason="예약 가능";return true;}
        }
        public bool TryReserve(string operationId,string agencyId,string teamId,string workType,out string reason)
        {
            if(string.IsNullOrEmpty(operationId)||string.IsNullOrEmpty(teamId)){reason="업무 배정 정보가 없습니다";return false;}
            if(!Initialize()){reason=StatusReason;return false;}
            lock(gate)
            {
                if(reservations.TryGetValue(operationId,out var existing)){bool same=existing.AgencyId==agencyId&&existing.TeamId==teamId&&existing.WorkType==workType&&(existing.State=="Committed"||existing.State=="Consumed");reason=same?"이미 예약된 업무입니다":"종료되었거나 다른 자원 배정 정보입니다";return same;}
                if(!bins.TryGetValue(Key(agencyId,workType),out var bin)||bin.Available<1){reason=bin==null?"이 기관에 해당 지원 꾸러미가 없습니다":bin.Definition.label+" 없음 · 복귀 후 보충을 기다리세요";return false;}
                bin.Available--;bin.Committed++;reservations.Add(operationId,new Reservation{OperationId=operationId,AgencyId=agencyId,TeamId=teamId,WorkType=workType,State="Committed"});Revision++;reason="지원 꾸러미 예약됨";return true;
            }
        }
        public bool ConsumeForExecution(string operationId,out string reason)
        {
            lock(gate)
            {
                if(!reservations.TryGetValue(operationId,out var r)){reason="예약된 지원 꾸러미가 없습니다";return false;}
                if(r.State=="Consumed"){reason="이 업무에서 이미 사용 처리했습니다";return true;}
                if(r.State!="Committed"){reason="종료된 자원 배정입니다";return false;}
                bool executing=false;var controller=GetComponent<MvpAgencyDispatchController>();if(controller!=null)foreach(var m in controller.MissionSnapshots)if(m.OperationId==operationId&&m.TeamId==r.TeamId&&m.AgencyId==r.AgencyId&&m.State=="Working")executing=true;
                if(!executing){reason="실제 현장 업무 실행 시에만 사용 처리할 수 있습니다";return false;}
                var bin=bins[Key(r.AgencyId,r.WorkType)];if(bin.Committed<1){reason="지원 꾸러미 배정 불일치";return false;}
                bin.Committed--;bin.Consumed++;bin.TotalConsumed++;r.State="Consumed";Revision++;reason="현장 업무에 지원 꾸러미 사용";return true;
            }
        }
        public bool CanRefundBeforeDeparture(string operationId,out string reason)
        {
            lock(gate){if(!reservations.TryGetValue(operationId,out var r)||r.State!="Committed"){reason="환급할 출발 전 예약이 없습니다";return false;}var controller=GetComponent<MvpAgencyDispatchController>();if(controller!=null)foreach(var m in controller.MissionSnapshots)if(m.OperationId==operationId&&m.TeamId==r.TeamId&&m.AgencyId==r.AgencyId&&m.State=="Requested"&&m.DistanceTravelled==0){reason="출발 전 예약 환급 가능";return true;}reason="출발 전 취소로만 예약을 환급할 수 있습니다";return false;}
        }
        public bool RefundBeforeDeparture(string operationId,out string reason)
        {if(!CanRefundBeforeDeparture(operationId,out reason))return false;return RefundCommitted(operationId,"Canceled",out reason);}
        // Only the controller's failed acceptance transaction calls this before the job exists.
        internal bool RollbackRejectedReservation(string operationId,out string reason)
        {
            var controller=GetComponent<MvpAgencyDispatchController>();if(controller!=null)foreach(var m in controller.MissionSnapshots)if(m.OperationId==operationId&&(m.DistanceTravelled>0||(m.State!="Blocked"&&m.State!="Unavailable"))){reason="활성 업무의 자원 배정을 되돌릴 수 없습니다";return false;}
            return RefundCommitted(operationId,"Rejected",out reason);
        }
        private bool RefundCommitted(string operationId,string state,out string reason)
        {
            lock(gate){if(!reservations.TryGetValue(operationId,out var r)||r.State!="Committed"){reason="환급할 예약이 없습니다";return false;}var bin=bins[Key(r.AgencyId,r.WorkType)];if(bin.Committed<1||bin.Available>=bin.Definition.capacity){reason="지원 꾸러미 수량 불일치";return false;}bin.Committed--;bin.Available++;r.State=state;Revision++;reason="예약 꾸러미 환급됨";return true;}
        }
        public bool CompleteReplenishment(string operationId,out string reason)
        {
            lock(gate)
            {
                if(!reservations.TryGetValue(operationId,out var r)){reason="보충할 자원 배정이 없습니다";return false;}
                if(r.State=="Restocked"){reason="이미 보충 완료했습니다";return true;}
                if(r.State!="Committed"&&r.State!="Consumed"){reason="보충할 현장 자원이 없습니다";return false;}
                bool completed=false;var controller=GetComponent<MvpAgencyDispatchController>();if(controller!=null)foreach(var m in controller.MissionSnapshots)if(m.OperationId==operationId&&m.TeamId==r.TeamId&&m.AgencyId==r.AgencyId&&m.State=="Replenishing"&&m.TurnaroundRemainingSeconds<=.0001f&&m.TurnaroundTotalSeconds>=15)completed=true;
                if(!completed){reason="실제 복귀 후 재출동 준비를 완료해야 보충할 수 있습니다";return false;}
                var bin=bins[Key(r.AgencyId,r.WorkType)];if(bin.Available>=bin.Definition.capacity||(r.State=="Consumed"?bin.Consumed:bin.Committed)<1){reason="지원 꾸러미 보충 수량 불일치";return false;}
                if(r.State=="Consumed")bin.Consumed--;else bin.Committed--;bin.Available++;r.State="Restocked";Revision++;reason="지원 꾸러미 보충 완료";return true;
            }
        }
        public void ResetRun()
        {
            Initialize();lock(gate){reservations.Clear();foreach(var bin in bins.Values){bin.Available=bin.Definition.capacity;bin.Committed=0;bin.Consumed=0;bin.TotalConsumed=0;}RunRevision++;Revision++;}
        }
    }
}
