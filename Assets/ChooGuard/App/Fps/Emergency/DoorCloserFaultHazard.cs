#if CHOOGUARD_DOOR_CLOSER_FAULT
using ChooGuard.App.Fps.Facilities;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>One existing door's failed closer and its manual isolation, physical maintenance and return test.</summary>
    public sealed class DoorCloserFaultHazard : Hazard
    {
        public DoorOperatorFault Fault { get; }
        public StationDoor Door => Fault != null ? Fault.Door : null;
        public override string Label => "출입문 자동 복귀 불량";
        public override float NoticeRadius => Active ? 4 : 0;
        public override float DangerRadius => 0;
        public override float Clearance => 0;
        public override float ApproachRadius => 2.2f;
        public override Vector3 Scene => Door != null ? Door.World(new Vector3(0, 0, .8f)) : Position;
        public override string Visible => Fault != null ? Fault.StateText : "문 제어 연결 소실 · 현장 재확인 필요";
        public override Agency Command => Agency.Facility;
        public override bool Reportable => Fault != null && Fault.Diagnosed;
        public override string ReportOption => Named + " 클로저 점검 요청";
        public override string ReportLine => "역무실, " + Where + " 출입문이 놓아도 닫히지 않는 것을 직접 확인했습니다. " +
            (Fault != null && Fault.Secured ? "문을 손으로 닫고 출입을 통제했습니다." : "수동 폐쇄와 주변 통제를 진행 중입니다.") + " 시설 담당 현장 점검 요청합니다.";
        public override string OfficeReply => "역무실 수신. 시설 담당 보내겠습니다. 문짝을 직접 닫고 문턱을 비워 출입을 통제해 주십시오. 정비 뒤 실제 개방·자동 닫힘을 확인하기 전에는 복구로 보고하지 마십시오.";
        public override PaRequest Announcement => new PaRequest("출입문 점검 안내 요청", PaLine.DoorCheck,
            "안내 말씀 드립니다. " + Where + " 출입문을 점검하고 있습니다. 해당 문으로 접근하지 말고 직원 안내에 따라 이동해 주십시오.", PaScope.Inform);
        public override string State => Fault != null ? Fault.StateText : "제어 연결 재확인 필요";
        public override bool UnderControl => !Active || Fault != null && Fault.Secured;
        public override string Handover => Named + " · " + State;
        public override float WorkSeconds(Agency agency) => Active && agency == Agency.Facility && Fault != null &&
            Fault.Secured && Fault.ServiceAuthorized && !Fault.Serviced ? 18 : 0;

        public override bool Blocks(Vector3 position)
        {
            if (!Active || Fault == null || !Fault.Secured || Door == null) return false;
            var local = Door.Local(position);
            return Mathf.Abs(local.y) < 2.2f && Mathf.Abs(local.x) < Door.Width * .5f + .2f && Mathf.Abs(local.z) < .5f;
        }

        public override string Resolve(Agency agency)
        {
            if (agency != Agency.Facility || Fault == null || !Fault.CompleteService()) return null;
            // The event stays active until staff witness the actual repaired leaf closing by its own driver.
            return "시설 담당입니다. " + Where + " 클로저 정비했습니다. 통제 해제 후 실제 문을 열었다 놓고 자동 닫힘을 확인해 주십시오.";
        }

        public DoorCloserFaultHazard(string id, DoorOperatorFault fault)
        {
            Id = id; Kind = HazardKind.DoorCloserFault; Fault = fault;
            Position = fault.Door.World(new Vector3(0, 1.05f, 0)); StartedAt = Time.time;
        }

        public bool ConfirmVerified()
        {
            if (!Active || Fault == null || !Fault.Verified) return false;
            End(); return true;
        }
    }
}
#endif
