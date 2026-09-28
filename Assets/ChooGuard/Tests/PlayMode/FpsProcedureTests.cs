using System.Collections;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Tutorial;
using ChooGuard.App.Fps.Work;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ChooGuard.Tests.PlayMode
{
    // 절차 슬라이스의 최소 계약. OS 입력을 합성하지 않는다 —
    // FirstPersonResponder.SetExternalInputMode(true) + StepInput 이 결정론적 경로다.
    public sealed class FpsProcedureTests
    {
        private GameObject playerObject,facilityObject,sessionObject;
        private FirstPersonResponder responder;
        private FpsGazeTracker tracker;
        private FacilityInspectable facility;
        private TutorialSession session;
        private TextAsset procedure;

        private const string ProcedureJson=@"{
  ""id"": ""fire-extinguisher-monthly"", ""version"": 1, ""title"": ""소화기 월간 상태점검"",
  ""basis"": ""시험용 축약본"",
  ""steps"": [
    { ""id"":""read-serial"", ""label"":""고유번호 판독"", ""basis"":""제22조3"", ""requires"":[],
      ""allFacts"":[""serial-gazed""], ""effect"":""record-serial"", ""failReason"":""고유번호를 먼저 확인하세요"" },
    { ""id"":""read-spec-plate"", ""label"":""제원표 판독"", ""basis"":""제23조2-1"", ""requires"":[""read-serial""],
      ""allFacts"":[""spec-plate-gazed""], ""effect"":""none"", ""failReason"":""제원표를 읽지 않고는 판정할 수 없습니다"" },
    { ""id"":""record-verdict"", ""label"":""판정 기재"", ""basis"":""제23조2-1"", ""requires"":[""read-spec-plate""],
      ""allFacts"":[""serial-recorded""], ""effect"":""record-verdict"", ""failReason"":""고유번호 기재가 먼저입니다"" },
    { ""id"":""issue-repair-order"", ""label"":""폐기·교체 요구 인계"", ""basis"":""제23조1-1"", ""requires"":[""record-verdict""],
      ""allFacts"":[""verdict-unfit""], ""conditional"":true, ""effect"":""issue-repair-order"", ""failReason"":""적합 판정에는 필요하지 않습니다"" },
    { ""id"":""attach-tag"", ""label"":""점검표 부착"", ""basis"":""제23조2-3"", ""requires"":[""record-verdict""],
      ""allFacts"":[""verdict-recorded""], ""effect"":""attach-tag"", ""failReason"":""판정 전에는 부착할 수 없습니다"" },
    { ""id"":""close-inspection"", ""label"":""단말 재스캔"", ""basis"":""제22조3"", ""requires"":[""attach-tag""],
      ""allFacts"":[""tag-attached""], ""effect"":""close-inspection"", ""failReason"":""점검표가 없으면 완료로 읽지 않습니다"" }
  ]
}";

        [SetUp]
        public void SetUp()
        {
            procedure=new TextAsset(ProcedureJson);

            playerObject=new GameObject("시험 플레이어",typeof(CharacterController));
            responder=playerObject.AddComponent<FirstPersonResponder>();
            responder.InteractionDistance=2.5f;
            tracker=playerObject.AddComponent<FpsGazeTracker>();
            tracker.Responder=responder;

            facilityObject=new GameObject("소화기");
            facilityObject.transform.position=new Vector3(0,1.1f,1.2f);
            facility=facilityObject.AddComponent<FacilityInspectable>();
            facility.SerialNumber="TEST-FE-001";
            facility.Corroded=true;          // 정답은 '부적합'
            facility.AllowFieldRepair=false;
            facility.Points=new[]
            {
                MakePoint("serial","고유번호",new Vector3(0,-.05f,-.06f),.5f),
                MakePoint("spec-plate","제원표",new Vector3(0,.05f,-.06f),.5f),
            };
            facility.Bind(tracker);

            sessionObject=new GameObject("세션");
            session=sessionObject.AddComponent<TutorialSession>();
            session.Responder=responder;session.GazeTracker=tracker;session.Target=facility;
            session.ProcedureAsset=procedure;session.PendingVerdict=InspectionVerdict.UNFIT;
            var terminalObject=new GameObject("현장 단말");
            terminalObject.transform.SetParent(sessionObject.transform,false);
            session.AuditTerminal=terminalObject.AddComponent<AuditTerminal>();
            responder.SetExternalInputMode(true);responder.Resume(false);
        }

        private FacilityInspectable.InspectionPoint MakePoint(string id,string label,Vector3 localOffset,float dwell)
        {
            var go=new GameObject("관측 · "+label);
            go.transform.SetParent(facilityObject.transform,false);
            go.transform.localPosition=localOffset;
            var box=go.AddComponent<BoxCollider>();
            box.isTrigger=false;box.size=new Vector3(.12f,.06f,.02f);
            return new FacilityInspectable.InspectionPoint{Id=id,Label=label,Surface=box,RequiredDwellSeconds=dwell};
        }

        [TearDown]
        public void TearDown()
        {
            foreach(var go in new[]{sessionObject,facilityObject,playerObject})if(go!=null)Object.Destroy(go);
            if(procedure!=null)Object.Destroy(procedure);
        }

        // 시선을 합성하지 않고 관측 시간만 먹인다. Update 와 같은 경로다.
        private void Gaze(string pointId,float seconds)
        {
            var point=facility.Point(pointId);
            Assert.IsNotNull(point,"관측 지점 없음 · "+pointId);
            tracker.Accumulate(point.Surface,seconds);
        }


        [UnityTest]
        public IEnumerator 제원표를_안_읽으면_점검표_부착이_거부된다()
        {
            yield return null;
            Gaze("serial",1f);
            Assert.IsTrue(session.Advance(),"고유번호 판독이 통과해야 한다 · "+session.LastReason);

            // 제원표를 건너뛴 채로 계속 눌러도 진행되지 않는다.
            Assert.IsFalse(session.Advance(),"제원표 미판독 상태에서 진행되어서는 안 된다");
            Assert.IsNull(facility.AttachedTag,"점검표가 부착되어서는 안 된다");

            // 제원표를 읽으면 다음 단계가 열린다.
            Gaze("spec-plate",1f);
            Assert.IsTrue(session.Advance(),"제원표 판독이 통과해야 한다 · "+session.LastReason);
        }

        [UnityTest]
        public IEnumerator 부식인데_적합으로_기재하면_감사관이_오판정을_열거한다()
        {
            yield return null;
            session.PendingVerdict=InspectionVerdict.FIT;   // 월드는 부식 상태다 — 오판정
            Gaze("serial",1f);Assert.IsTrue(session.Advance());
            Gaze("spec-plate",1f);Assert.IsTrue(session.Advance());
            Assert.IsTrue(session.Advance(),"판정 기재가 통과해야 한다 · "+session.LastReason);
            Assert.AreEqual(1,session.MisjudgementCount,"오판정이 계수되어야 한다");

            Assert.IsTrue(session.Advance(),"점검표 부착이 통과해야 한다 · "+session.LastReason);
            Assert.IsNotNull(facility.AttachedTag,"점검표가 소화기의 자식으로 실재해야 한다");
            Assert.AreSame(facilityObject.transform,facility.AttachedTag.parent);

            Assert.IsFalse(session.Advance());
            Assert.IsFalse(session.Finished);
            Assert.IsTrue(session.AuditTerminal.TryInteract(responder,out _));
            Assert.IsTrue(session.Finished);
            Assert.IsFalse(session.AuditTerminal.LastReport.Clean);
            Assert.IsTrue(facility.ShouldBeUnfit);
        }

        [UnityTest]
        public IEnumerator 현장수리_시도는_역할경계_위반으로_거부된다()
        {
            yield return null;
            Assert.IsFalse(session.TryFieldRepair(),"역무원의 현장 수리는 거부되어야 한다");
            Assert.AreEqual(1,session.RoleBoundaryViolations);
        }

        [UnityTest]
        public IEnumerator 부적합_판정에는_요구_인계가_필요하고_없으면_열거된다()
        {
            yield return null;
            session.PendingVerdict=InspectionVerdict.UNFIT;
            Gaze("serial",1f);Assert.IsTrue(session.Advance());
            Gaze("spec-plate",1f);Assert.IsTrue(session.Advance());
            Assert.IsTrue(session.Advance(),"판정 기재 · "+session.LastReason);
            Assert.AreEqual(InspectionVerdict.UNFIT,facility.Verdict);
            Assert.AreEqual(0,session.MisjudgementCount,"정답 판정이므로 오판정이 아니다");

            // 요구 인계를 건너뛰고 부착·마감까지 가면 감사관이 열거한다.
            Assert.IsTrue(session.Advance(),"요구 인계가 먼저 열려야 한다 · "+session.LastReason);
            Assert.IsTrue(facility.RepairOrderIssued,"부적합이면 요구 인계가 발행된다");
        }

        [UnityTest]
        public IEnumerator 되감기는_뒤_단계까지_함께_푼다()
        {
            yield return null;
            Gaze("serial",1f);Assert.IsTrue(session.Advance());
            Gaze("spec-plate",1f);Assert.IsTrue(session.Advance());
            Assert.IsTrue(session.Runner.Find("read-spec-plate").Done);

            Assert.IsTrue(session.Rewind("read-serial"));
            Assert.IsFalse(session.Runner.Find("read-serial").Done);
            Assert.IsFalse(session.Runner.Find("read-spec-plate").Done,"선행이 풀리면 뒤 단계도 풀린다");
        }

        [UnityTest]
        public IEnumerator 무점검표_재시행은_단계_수가_같다()
        {
            yield return null;
            int visibleSteps=session.Runner.Steps.Count;
            session.Restart(hideChecklist:true);
            Assert.IsFalse(session.ChecklistVisible);
            Assert.AreEqual(visibleSteps,session.Runner.Steps.Count,
                "가시성만 달라야 한다 — 단계 수가 달라지면 동형성 다리가 끊긴다");
            foreach(var step in session.Runner.Steps)Assert.IsFalse(step.Done,"재시행은 초기화된다");
        }
    }
}
