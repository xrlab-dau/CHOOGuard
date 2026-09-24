using System.Collections;
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
    // 지속 상태 이월의 계약. layer_coupling=state_carryover 판정이 요구하는 것은
    // "튜토리얼 완료가 지속 상태로 남아 본편 초기 조건이 되고, 건너뛰면 막히는 게 아니라 조건이 달라진다"이다.
    //
    // 실제 persistentDataPath 를 건드리지 않는다 — OverridePath 로 임시 경로를 꽂는다.
    public sealed class FpsCarryoverTests
    {
        private string tempDir,savedOverride;
        private GameObject playerObject,facilityObject,sessionObject,dispatchObject;
        private FirstPersonResponder responder;
        private FpsGazeTracker tracker;
        private FacilityInspectable facility;
        private TechnicianDispatch dispatch;
        private TutorialSession session;
        private TextAsset procedure;

        private const string ProcedureJson=@"{
  ""id"": ""carryover-test"", ""version"": 2, ""title"": ""이월 시험용"", ""basis"": ""시험"",
  ""steps"": [
    { ""id"":""record-verdict"", ""label"":""판정 기재"", ""basis"":""시험"", ""requires"":[],
      ""allFacts"":[], ""effect"":""record-verdict"", ""failReason"":""판정을 기재하세요"" },
    { ""id"":""issue-repair-order"", ""label"":""요구 인계"", ""basis"":""시험"", ""requires"":[""record-verdict""],
      ""allFacts"":[""verdict-unfit""], ""conditional"":true, ""effect"":""issue-repair-order"", ""failReason"":""필요 없습니다"" },
    { ""id"":""attach-tag"", ""label"":""점검표 부착"", ""basis"":""시험"", ""requires"":[""record-verdict""],
      ""allFacts"":[""verdict-recorded""], ""effect"":""attach-tag"", ""failReason"":""판정 전에는 부착할 수 없습니다"" },
    { ""id"":""close-inspection"", ""label"":""단말 재스캔"", ""basis"":""시험"", ""requires"":[""attach-tag""],
      ""allFacts"":[""tag-attached""], ""effect"":""close-inspection"", ""failReason"":""점검표가 없으면 완료로 읽지 않습니다"" }
  ]
}";

        [SetUp]
        public void SetUp()
        {
            savedOverride=TutorialCarryoverStore.OverridePath;
            tempDir=Path.Combine(Path.GetTempPath(),"chooguard-carryover-"+System.Guid.NewGuid().ToString("N"));
            TutorialCarryoverStore.OverridePath=Path.Combine(tempDir,"tutorial-carryover.json");

            procedure=new TextAsset(ProcedureJson);
            playerObject=new GameObject("시험 플레이어",typeof(CharacterController));
            responder=playerObject.AddComponent<FirstPersonResponder>();
            tracker=playerObject.AddComponent<FpsGazeTracker>();
            tracker.Responder=responder;

            facilityObject=new GameObject("소화기 · BSN-CONC-FE-010");
            facility=facilityObject.AddComponent<FacilityInspectable>();
            facility.SerialNumber="BSN-CONC-FE-010";
            facility.Corroded=true;
            facility.AllowFieldRepair=false;
            facility.Bind(tracker);

            dispatchObject=new GameObject("기술자 인계");
            dispatch=dispatchObject.AddComponent<TechnicianDispatch>();
            dispatch.Target=facility;
            dispatch.AcknowledgeSeconds=1f;dispatch.TravelSeconds=1f;dispatch.WorkSeconds=1f;
        }

        // 세션은 시험마다 다른 설정으로 만들어야 해서 SetUp 에서 만들지 않는다 —
        // AddComponent 직후 Start 가 돌기 때문에 필드를 먼저 꽂을 수 없다.
        private TutorialSession MakeSession(bool applyOnStart)
        {
            sessionObject=new GameObject("세션");
            sessionObject.SetActive(false);                 // Start 를 미뤄 설정을 먼저 꽂는다
            session=sessionObject.AddComponent<TutorialSession>();
            session.Responder=responder;session.GazeTracker=tracker;session.Target=facility;
            session.Dispatch=dispatch;session.ProcedureAsset=procedure;
            session.PendingVerdict=InspectionVerdict.UNFIT;
            session.DriveHandoffWithFrameTime=false;
            session.ApplyCarryoverOnStart=applyOnStart;
            sessionObject.SetActive(true);
            return session;
        }

        [TearDown]
        public void TearDown()
        {
            foreach(var go in new[]{sessionObject,dispatchObject,facilityObject,playerObject})if(go!=null)Object.Destroy(go);
            if(procedure!=null)Object.Destroy(procedure);
            TutorialCarryoverStore.OverridePath=savedOverride;
            try { if(Directory.Exists(tempDir))Directory.Delete(tempDir,true); } catch { /* 정리 실패는 시험 결과가 아니다 */ }
        }

        // 판정 → 요구 → 점검표 → 닫기.
        private void RunInspection(TutorialSession s)
        {
            Assert.IsTrue(s.Advance(),"판정 기재 · "+s.LastReason);
            Assert.IsTrue(s.Advance(),"요구 발행 · "+s.LastReason);
            Assert.IsTrue(s.Advance(),"점검표 부착 · "+s.LastReason);
            Assert.IsTrue(s.Advance(),"단말 재스캔 · "+s.LastReason);
            Assert.IsTrue(s.Finished,"점검이 닫히지 않았습니다");
        }

        [Test]
        public void 점검을_닫으면_결과가_파일로_남는다()
        {
            RunInspection(MakeSession(false));

            Assert.IsTrue(File.Exists(TutorialCarryoverStore.ResolvedPath),"이월 기록 파일이 없습니다");
            var outcome=TutorialCarryoverStore.Load().Find("소화기 · BSN-CONC-FE-010");
            Assert.IsNotNull(outcome,"이 설비의 결과가 기록되지 않았습니다");
            Assert.AreEqual("UNFIT",outcome.verdict);
            Assert.IsTrue(outcome.repairOrderIssued,"요구 발행이 기록되지 않았습니다");
            Assert.IsTrue(outcome.tagAttached,"점검표 부착이 기록되지 않았습니다");
        }

        [Test]
        public void 점검을_닫기_전에는_기록하지_않는다()
        {
            var s=MakeSession(false);
            s.Advance();                                   // 판정만 기재하고 중단
            Assert.IsFalse(File.Exists(TutorialCarryoverStore.ResolvedPath),
                "닫기 전에 기록되면 되감기·재시행의 중간 상태가 다음 회차로 샌다");
        }

        // Start() 경로를 시험하므로 [UnityTest] 다 — Unity 는 SetActive(true) 에 Awake 만 즉시 부르고
        // Start 는 다음 프레임으로 미룬다. [Test] 로 두면 이월이 적용되기 전에 단언하게 된다.
        [UnityTest]
        public IEnumerator 교체된_설비는_다음_회차에서_결함이_없다()
        {
            var first=MakeSession(false);
            first.Advance();first.Advance();               // 판정 → 요구
            dispatch.Tick(1f);dispatch.Tick(1f);dispatch.Tick(1f);
            Assert.IsTrue(facility.ServiceCompleted,"교체가 일어나지 않았습니다");
            first.Advance();first.Advance();               // 점검표 → 닫기
            Assert.IsTrue(first.Finished);

            // 다음 회차. 설비를 씬 초기 상태(부식)로 되돌린 뒤 이월을 적용한다.
            Object.DestroyImmediate(sessionObject);
            facility.ResetInspection();
            dispatch.ResetHandoff();
            Assert.IsTrue(facility.Corroded,"사전 조건: 초기 상태는 부식이어야 합니다");

            MakeSession(true);
            yield return null;                             // Start() 가 돌 기회를 준다

            Assert.IsTrue(facility.ServiceCompleted,"이전 회차의 교체가 이월되지 않았습니다");
            Assert.IsFalse(facility.Corroded,"교체된 설비인데 부식이 남아 있습니다");
            Assert.IsFalse(facility.ShouldBeUnfit,"교체된 설비가 여전히 부적합 조건입니다");
        }

        // Start 가 이월을 부르는지 자체를 못박는다. 위 시험이 통과해도 이 경로가 빠지면 실제 플레이에서 안 된다.
        [UnityTest]
        public IEnumerator ApplyCarryoverOnStart_가_꺼져_있으면_시작_시_적용하지_않는다()
        {
            TutorialCarryoverStore.Record("carryover-test",new FacilityOutcome
            {
                facilityId="소화기 · BSN-CONC-FE-010",serial="BSN-CONC-FE-010-R",
                replacedFromSerial="BSN-CONC-FE-010",serviceCompleted=true,verdict="UNFIT",
            });

            MakeSession(false);
            yield return null;

            Assert.IsFalse(facility.ServiceCompleted,"꺼져 있는데 이월이 적용됐습니다");
            Assert.IsTrue(facility.Corroded,"꺼져 있는데 월드 상태가 바뀌었습니다");
        }

        [Test]
        public void 기록이_없으면_막지_않고_그대로_시작한다()
        {
            var s=MakeSession(true);                       // 이월 파일이 없는 상태
            Assert.IsFalse(s.ApplyCarryover(),"기록이 없는데 적용했다고 보고했습니다");
            Assert.IsTrue(facility.Corroded,"초기 월드 상태가 바뀌었습니다");
            Assert.IsTrue(s.Advance(),"기록이 없다고 진행이 막혔습니다 · "+s.LastReason);
        }

        [Test]
        public void 깨진_기록은_비어_있는_것으로_읽고_던지지_않는다()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TutorialCarryoverStore.ResolvedPath));
            File.WriteAllText(TutorialCarryoverStore.ResolvedPath,"{ 이건 JSON 이 아니다 ");
            // 경고를 남기는 것까지가 계약이다. ignoreFailingMessages 로 전부 덮으면
            // 저장소와 무관한 예외가 같은 시험 안에서 나도 통과해 버린다 — 기대하는 것만 정확히 매치한다.
            LogAssert.Expect(LogType.Warning,new Regex("이월.*비어 있는 것으로 시작"));

            var data=TutorialCarryoverStore.Load();

            Assert.IsNotNull(data);
            CollectionAssert.IsEmpty(data.facilities,"깨진 기록에서 항목이 나왔습니다");
        }

        [Test]
        public void 같은_설비를_다시_점검하면_최신_결과로_덮는다()
        {
            TutorialCarryoverStore.Record("p",new FacilityOutcome{facilityId="소화기 · A",verdict="FIT"});
            TutorialCarryoverStore.Record("p",new FacilityOutcome{facilityId="소화기 · A",verdict="UNFIT"});

            var data=TutorialCarryoverStore.Load();
            Assert.AreEqual(1,data.facilities.Count,"같은 설비가 두 번 쌓였습니다");
            Assert.AreEqual("UNFIT",data.Find("소화기 · A").verdict);
        }

        [Test]
        public void 쓰기가_끝나면_임시_파일이_남지_않는다()
        {
            TutorialCarryoverStore.Record("p",new FacilityOutcome{facilityId="소화기 · A"});
            TutorialCarryoverStore.Record("p",new FacilityOutcome{facilityId="소화기 · B"});

            Assert.IsFalse(File.Exists(TutorialCarryoverStore.ResolvedPath+".writing"),
                "임시 파일이 남았습니다 — 다음 쓰기가 무엇을 읽을지 모르게 됩니다");
            Assert.AreEqual(2,TutorialCarryoverStore.Load().facilities.Count);
        }
    }
}
