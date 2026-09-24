using System.IO;
using System.Text.RegularExpressions;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Tutorial;
using ChooGuard.App.Fps.Work;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ChooGuard.Tests.PlayMode
{
    // 기술자 인계 주기의 계약. 확정된 역할 경계는 "요청이 즉시 완료되지 않고
    // 수신·이동·수행·결과가 보인다"이므로, 여기서 시험하는 것은 단계가 실제로 지나가는가다.
    // 벽시계를 쓰지 않는다 — DriveHandoffWithFrameTime=false 로 두고 Tick 을 직접 먹인다.
    public sealed class FpsTechnicianHandoffTests
    {
        private GameObject playerObject,facilityObject,sessionObject,dispatchObject;
        private FirstPersonResponder responder;
        private FpsGazeTracker tracker;
        private FacilityInspectable facility;
        private TechnicianDispatch dispatch;
        private TutorialSession session;
        private TextAsset procedure;

        // issue-repair-order 까지만 있으면 인계 주기를 시험하기에 충분하다.
        private const string ProcedureJson=@"{
  ""id"": ""handoff-test"", ""version"": 2, ""title"": ""인계 시험용"",
  ""basis"": ""시험용 축약본"",
  ""steps"": [
    { ""id"":""record-verdict"", ""label"":""판정 기재"", ""basis"":""시험"", ""requires"":[],
      ""allFacts"":[], ""effect"":""record-verdict"", ""failReason"":""판정을 기재하세요"" },
    { ""id"":""issue-repair-order"", ""label"":""폐기·교체 요구 인계"", ""basis"":""시험"", ""requires"":[""record-verdict""],
      ""allFacts"":[""verdict-unfit""], ""conditional"":true, ""effect"":""issue-repair-order"", ""failReason"":""적합 판정에는 필요하지 않습니다"" },
    { ""id"":""witness-replacement"", ""label"":""교체 결과 입회·확인"", ""basis"":""시험"", ""requires"":[""issue-repair-order""],
      ""allFacts"":[""technician-completed""], ""conditional"":true, ""effect"":""witness-replacement"", ""failReason"":""기술자 작업이 끝나지 않았습니다"" },
    { ""id"":""attach-tag"", ""label"":""점검표 부착"", ""basis"":""시험"", ""requires"":[""record-verdict""],
      ""allFacts"":[""verdict-recorded""], ""effect"":""attach-tag"", ""failReason"":""판정 전에는 부착할 수 없습니다"" },
    { ""id"":""close-inspection"", ""label"":""단말 재스캔"", ""basis"":""시험"", ""requires"":[""attach-tag""],
      ""allFacts"":[""tag-attached""], ""effect"":""close-inspection"", ""failReason"":""점검표가 없으면 완료로 읽지 않습니다"" }
  ]
}";

        [SetUp]
        public void SetUp()
        {
            procedure=new TextAsset(ProcedureJson);

            playerObject=new GameObject("시험 플레이어",typeof(CharacterController));
            responder=playerObject.AddComponent<FirstPersonResponder>();
            tracker=playerObject.AddComponent<FpsGazeTracker>();
            tracker.Responder=responder;

            facilityObject=new GameObject("소화기");
            facility=facilityObject.AddComponent<FacilityInspectable>();
            facility.SerialNumber="FE-001";
            facility.Corroded=true;                 // 정답은 부적합
            facility.AllowFieldRepair=false;
            facility.Bind(tracker);

            dispatchObject=new GameObject("기술자 인계");
            dispatch=dispatchObject.AddComponent<TechnicianDispatch>();
            dispatch.Target=facility;
            dispatch.AcknowledgeSeconds=2f;dispatch.TravelSeconds=5f;dispatch.WorkSeconds=3f;

            sessionObject=new GameObject("세션");
            session=sessionObject.AddComponent<TutorialSession>();
            session.Responder=responder;session.GazeTracker=tracker;session.Target=facility;
            session.Dispatch=dispatch;session.ProcedureAsset=procedure;
            session.PendingVerdict=InspectionVerdict.UNFIT;
            session.DriveHandoffWithFrameTime=false;   // 시계는 시험이 쥔다
            // 이월은 FpsCarryoverTests 가 다룬다. 여기서 실제 저장 경로를 건드리지 않는다.
            session.WriteCarryover=false;session.ApplyCarryoverOnStart=false;
        }

        [TearDown]
        public void TearDown()
        {
            foreach(var go in new[]{sessionObject,dispatchObject,facilityObject,playerObject})if(go!=null)Object.Destroy(go);
            if(procedure!=null)Object.Destroy(procedure);
        }

        // 판정 기재 → 요구 발행까지 진행시킨다.
        private void IssueOrder()
        {
            Assert.IsTrue(session.Advance(),"판정 기재가 진행되지 않았습니다 · "+session.LastReason);
            Assert.IsTrue(session.Advance(),"요구 발행이 진행되지 않았습니다 · "+session.LastReason);
        }

        [Test]
        public void 요구를_발행해도_교체가_즉시_끝나지_않는다()
        {
            IssueOrder();

            Assert.IsTrue(facility.RepairOrderIssued,"요구가 발행되지 않았습니다");
            Assert.AreEqual(HandoffStage.RECEIVED,dispatch.Stage,"요구 직후에는 접수 단계여야 합니다");
            Assert.IsFalse(dispatch.Completed,"요구만으로 교체가 완료되면 인계가 아닙니다");
            Assert.IsFalse(facility.ServiceCompleted,"기술자가 오기 전에 설비가 교체되었습니다");
            Assert.IsTrue(facility.Corroded,"기술자가 오기 전에 결함이 사라졌습니다");
        }

        [Test]
        public void 수신_이동_수행_결과_순서로_각_단계가_관찰된다()
        {
            IssueOrder();
            Assert.AreEqual(HandoffStage.RECEIVED,dispatch.Stage);

            dispatch.Tick(2f);
            Assert.AreEqual(HandoffStage.TRAVELLING,dispatch.Stage,"접수 후에는 이동이어야 합니다");

            dispatch.Tick(5f);
            Assert.AreEqual(HandoffStage.WORKING,dispatch.Stage,"이동 후에는 작업이어야 합니다");
            Assert.IsTrue(dispatch.OnSite,"작업 중에는 현장에 있어야 입회가 가능합니다");

            dispatch.Tick(3f);
            Assert.AreEqual(HandoffStage.COMPLETED,dispatch.Stage,"작업 후에는 완료여야 합니다");
        }

        [Test]
        public void 한_번의_큰_Tick_이_여러_단계를_건너뛰지_않는다()
        {
            IssueOrder();
            dispatch.Tick(999f);
            Assert.AreEqual(HandoffStage.TRAVELLING,dispatch.Stage,
                "한 번의 Tick 이 여러 단계를 건너뛰면 이동·수행이 관찰되지 않습니다");
        }

        // ECC 리뷰 지적 — 기존 시험이 요구 시간과 '정확히 일치하는 값'만 먹여 초과분 소실을 지나쳤다.
        // 필요치보다 살짝 많은 값을 주고, 남은 양이 다음 단계로 넘어가는지를 본다.
        [Test]
        public void 초과한_시간은_다음_단계로_이월된다()
        {
            IssueOrder();                       // 접수(2초 필요)

            dispatch.Tick(3f);                  // 1초 초과
            Assert.AreEqual(HandoffStage.TRAVELLING,dispatch.Stage);
            Assert.AreEqual(1f,dispatch.SecondsInStage,1e-4f,"초과분 1초가 이월되지 않았습니다");

            dispatch.Tick(4f);                  // 이월 1 + 4 = 5 = 이동 필요치
            Assert.AreEqual(HandoffStage.WORKING,dispatch.Stage,
                "이월분을 합치면 이동이 끝나야 하는데 그대로입니다 — 초과분이 버려졌습니다");
        }

        [Test]
        public void 완료_단계에서는_남은_시간을_더_세지_않는다()
        {
            IssueOrder();
            dispatch.Tick(2f);dispatch.Tick(5f);
            dispatch.Tick(100f);                // 작업 3초 + 97초 초과

            Assert.AreEqual(HandoffStage.COMPLETED,dispatch.Stage);
            Assert.AreEqual(0f,dispatch.SecondsInStage,1e-4f,"완료 후에도 시간이 누적되고 있습니다");
        }

        [Test]
        public void 교체_완료가_설비의_월드_상태를_바꾼다()
        {
            IssueOrder();
            dispatch.Tick(2f);dispatch.Tick(5f);dispatch.Tick(3f);

            Assert.IsTrue(facility.ServiceCompleted,"교체가 월드에 반영되지 않았습니다");
            Assert.IsFalse(facility.Corroded,"교체했는데 부식이 남아 있습니다");
            Assert.IsFalse(facility.ShouldBeUnfit,"교체 후에도 부적합 조건이 남아 있습니다");
            Assert.AreEqual("FE-001",facility.ReplacedFromSerial,"교체 전 고유번호가 기록되지 않았습니다");
            Assert.AreNotEqual("FE-001",facility.SerialNumber,"교체품에 새 고유번호가 없습니다");
        }

        [Test]
        public void 기술자가_끝나기_전에는_결과를_확인할_수_없다()
        {
            IssueOrder();
            dispatch.Tick(2f);

            Assert.IsFalse(session.WitnessRepairResult(),"도착 전인데 결과 확인이 허용됐습니다");
            Assert.IsFalse(dispatch.ResultWitnessed,"확인되지 않아야 합니다");
        }

        [Test]
        public void 기다리는_동안_다른_단계를_진행할_수_있다()
        {
            IssueOrder();
            // 기술자는 아직 접수 단계다. witness-replacement 는 조건부이므로 막지 않아야 한다.
            Assert.IsTrue(session.Advance(),"대기 중 점검표 부착이 막혔습니다 · "+session.LastReason);
            Assert.IsNotNull(facility.AttachedTag,"점검표가 부착되지 않았습니다");
            Assert.AreEqual(HandoffStage.RECEIVED,dispatch.Stage,"대기 중 다른 일을 해도 인계 단계는 그대로여야 합니다");
        }

        [Test]
        public void 결과를_확인하지_않고_닫으면_감사가_열거한다()
        {
            IssueOrder();
            session.Advance();                 // 점검표 부착
            session.Advance();                 // 단말 재스캔 → Audit

            Assert.IsTrue(session.Finished,"점검이 닫히지 않았습니다");
            CollectionAssert.IsNotEmpty(session.AuditFindings,"감사 결과가 비어 있습니다");
            StringAssert.Contains("교체 결과 미확인",string.Join(" / ",session.AuditFindings));
        }

        [Test]
        public void 재시작하면_교체_전_월드_상태로_돌아간다()
        {
            IssueOrder();
            dispatch.Tick(2f);dispatch.Tick(5f);dispatch.Tick(3f);
            Assert.IsTrue(facility.ServiceCompleted);

            session.Restart(false);

            Assert.AreEqual(HandoffStage.NONE,dispatch.Stage,"인계가 초기화되지 않았습니다");
            Assert.IsFalse(facility.ServiceCompleted,"교체 상태가 남아 있습니다");
            Assert.IsTrue(facility.Corroded,"재시행인데 결함이 복원되지 않았습니다");
            Assert.AreEqual("FE-001",facility.SerialNumber,"재시행인데 고유번호가 복원되지 않았습니다");
        }

        // ECC 리뷰 지적 — 판본 검사가 어휘를 제한하지 않으면 version 은 장식이 된다.
        [Test]
        public void v1_자료가_v2_어휘를_쓰면_거부된다()
        {
            const string V1WithV2Vocabulary=@"{
  ""id"": ""version-test"", ""version"": 1, ""title"": ""판본 시험"", ""basis"": ""시험"",
  ""steps"": [
    { ""id"":""witness-replacement"", ""label"":""교체 확인"", ""basis"":""시험"", ""requires"":[],
      ""allFacts"":[""technician-completed""], ""effect"":""witness-replacement"", ""failReason"":""시험"" }
  ]
}";
            // ProcedureRunner 는 침묵 실패를 만들지 않으려고 LogError 를 남긴다. 그것까지가 계약이다.
            LogAssert.Expect(LogType.Error,new Regex("허용되지 않은 효과.*witness-replacement"));
            var asset=new TextAsset(V1WithV2Vocabulary);
            try
            {
                var runner=new ProcedureRunner();
                Assert.IsFalse(runner.Load(asset),"v1 자료가 v2 어휘를 쓰는데 로드에 성공했습니다");
                StringAssert.Contains("판본",runner.StatusReason);
            }
            finally { Object.Destroy(asset); }
        }

        [Test]
        public void 배포된_절차_자료가_실제로_로드된다()
        {
            // 시험이 자체 축약본만 쓰면 실제 자료의 판본·어휘 오류를 영영 못 잡는다.
            // Resources 밖에 있으므로 디스크에서 직접 읽는다 — 에디터 PlayMode 에서만 유효하다.
            var path=Path.Combine(Application.dataPath,"ChooGuard/Art/Procedures/fire-extinguisher-monthly.json");
            Assert.IsTrue(File.Exists(path),"배포된 절차 자료가 예상 경로에 없습니다 · "+path);
            var asset=new TextAsset(File.ReadAllText(path));
            try
            {
                var runner=new ProcedureRunner();
                Assert.IsTrue(runner.Load(asset),"배포된 절차 자료를 읽지 못했습니다 · "+runner.StatusReason);
                Assert.IsTrue(runner.Ready);
                CollectionAssert.IsNotEmpty(runner.Steps);
            }
            finally { Object.Destroy(asset); }
        }

        [Test]
        public void 같은_대상에_요구를_두_번_해도_기술자는_하나다()
        {
            IssueOrder();
            dispatch.Tick(2f);
            Assert.AreEqual(HandoffStage.TRAVELLING,dispatch.Stage);

            Assert.IsFalse(dispatch.Request("FE-001","중복 요구"),"중복 요구가 수락됐습니다");
            Assert.AreEqual(HandoffStage.TRAVELLING,dispatch.Stage,"중복 요구가 진행 중인 인계를 되돌렸습니다");
        }
    }
}
