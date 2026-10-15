using System;
using UnityEngine;
namespace ChooGuard.App.Fps.Work
{
    public enum InspectionVerdict { NOT_RECORDED, FIT, UNFIT }

    // 점검 대상 설비. 월드 상태만 들고 있고 절차 규칙은 모른다.
    // FpsInteractable 의 주석 "Mission rules belong to the scene's gameplay owner" 를 그대로 지킨다 —
    // 같은 소화기가 튜토리얼 세션과 비상 세션 아래에서 다른 것을 의미할 수 있어야 한다.
    public class FacilityInspectable : FpsInteractable
    {
        [Serializable] public class InspectionPoint
        {
            public string Id;                 // 절차 정의의 관측 대상 이름과 맞춘다
            public Collider Surface;          // 반드시 non-trigger — 레이캐스트가 QueryTriggerInteraction.Ignore 다
            public float RequiredDwellSeconds=.6f;
            public string Label;
        }

        [Header("식별")]
        public string SerialNumber="";        // 제22조3 — 소화기에 고유번호를 부여
        [Header("관측 지점")]
        public InspectionPoint[] Points=new InspectionPoint[0];
        [Header("월드 상태 (씬 또는 시드가 정한다)")]
        public bool Corroded;                 // 제23조②1 — 부식
        public bool MechanicallyDefective;    // 제23조②1 — 기계적 결함
        public bool PressureOutOfRange;       // 지시압력계 지시침이 정상 범위 밖
        public bool ExpiryPassed;             // 제원표 기한 도달
        [Header("역할 경계")]
        public bool AllowFieldRepair;         // 제23조①1 — 역무원은 수리를 '요구'한다. 기본 false.
        public string FieldRepairRefusal="소화기의 수리·구입 예산은 소방관리 책임자가 요구합니다";

        public InspectionVerdict Verdict { get; private set; }=InspectionVerdict.NOT_RECORDED;
        public string RecordedSerial { get; private set; }="";
        public bool RepairOrderIssued { get; private set; }
        public Transform AttachedTag { get; private set; }
        // 제23조②1 — 기한 도달 이전이라도 부식·결함이면 폐기다. 기한만 보고 통과시키면 틀린다.
        public bool ShouldBeUnfit=>Corroded||MechanicallyDefective||PressureOutOfRange||ExpiryPassed;

        private FpsGazeTracker boundTracker;
        public void Bind(FpsGazeTracker tracker)=>boundTracker=tracker;

        public InspectionPoint Point(string id)
        {
            if(Points==null||string.IsNullOrEmpty(id))return null;
            foreach(var point in Points)if(point!=null&&point.Id==id)return point;
            return null;
        }
        public bool Observed(string pointId)
        {
            var point=Point(pointId);
            return point?.Surface!=null&&boundTracker!=null&&boundTracker.Dwelled(point.Surface,point.RequiredDwellSeconds);
        }
        public float ObservedSeconds(string pointId)
        {
            var point=Point(pointId);
            return point?.Surface==null||boundTracker==null?0:boundTracker.DwellOf(point.Surface);
        }

        public void RecordSerial(){RecordedSerial=SerialNumber??"";}
        public void RecordVerdict(InspectionVerdict verdict){Verdict=verdict;}
        public void IssueRepairOrder(){RepairOrderIssued=true;}
        // 부착이 곧 완료 증거다 — 다음 회차에 낡은 채로 읽힌다(제23조②3).
        public Transform AttachTag(GameObject tag)
        {
            if(tag==null)return null;
            tag.transform.SetParent(transform,false);AttachedTag=tag.transform;return AttachedTag;
        }
        public sealed class InspectionSnapshot
        {
            internal bool Corroded,MechanicallyDefective,PressureOutOfRange,ExpiryPassed,RepairOrderIssued;
            internal InspectionVerdict Verdict;
            internal string RecordedSerial;
            internal Transform Tag;
        }
        public InspectionSnapshot CaptureInspection()=>new InspectionSnapshot
        {
            Corroded=Corroded,MechanicallyDefective=MechanicallyDefective,PressureOutOfRange=PressureOutOfRange,
            ExpiryPassed=ExpiryPassed,Verdict=Verdict,RecordedSerial=RecordedSerial,RepairOrderIssued=RepairOrderIssued,Tag=AttachedTag,
        };
        public void RestoreInspection(InspectionSnapshot snapshot)
        {
            if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));
            if(AttachedTag!=null&&AttachedTag!=snapshot.Tag)
            {
                var discarded=AttachedTag.gameObject;discarded.SetActive(false);
                if(UnityEngine.Application.isPlaying)Destroy(discarded);else DestroyImmediate(discarded);
            }
            Corroded=snapshot.Corroded;MechanicallyDefective=snapshot.MechanicallyDefective;
            PressureOutOfRange=snapshot.PressureOutOfRange;ExpiryPassed=snapshot.ExpiryPassed;
            Verdict=snapshot.Verdict;RecordedSerial=snapshot.RecordedSerial;RepairOrderIssued=snapshot.RepairOrderIssued;
            AttachedTag=snapshot.Tag;if(AttachedTag!=null)AttachedTag.gameObject.SetActive(true);
        }
        public void ResetInspection()
        {
            Verdict=InspectionVerdict.NOT_RECORDED;RecordedSerial="";RepairOrderIssued=false;
            if(AttachedTag!=null){var go=AttachedTag.gameObject;AttachedTag=null;if(UnityEngine.Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
        }

        // 절차 게이트. 세션이 델리게이트를 꽂는다 — 설비는 세션 타입을 모른다.
        // null 을 돌려주면 통과, 문자열을 돌려주면 그 사유로 거부한다.
        // FirstPersonResponder.cs:116 이 reason 을 CurrentPrompt 에 넣으므로 배선 추가가 필요 없다.
        public Func<string> GateReason;

        public override bool CanInteract(FirstPersonResponder responder,out string reason)
        {
            if(!base.CanInteract(responder,out reason))return false;
            var gate=GateReason?.Invoke();
            if(!string.IsNullOrEmpty(gate)){reason=gate;return false;}
            return true;
        }

        // 현장 수리 가능 여부. 제23조①1 — 역무원은 수리를 '요구'한다. 거부가 곧 채점 항목이다.
        public bool CanFieldRepair(out string reason)
        {
            if(AllowFieldRepair){reason=null;return true;}
            reason=FieldRepairRefusal;return false;
        }
    }
}
