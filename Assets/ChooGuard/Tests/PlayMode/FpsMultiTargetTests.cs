using System.Collections;
using System.Collections.Generic;
using System.IO;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Tutorial;
using ChooGuard.App.Fps.Work;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ChooGuard.Tests.PlayMode
{
    // 다중 대상의 계약. jev-inject-structure-005 의 두 판정이 요구하는 구조다 —
    // session_and_terminal=one_each_hoisted(0.87): 세션과 단말은 각각 하나,
    // inspectable_scope=all_twelve_with_per_unit_state(0.65): 설비는 전부 점검 가능하고 상태는 유닛별.
    //
    // 여기서 시험하는 것은 "기술자를 기다리는 동안 다른 소화기를 점검할 수 있는가"가
    // 시험 안의 주장이 아니라 실제 세션 구조에서 성립하는가다.
    public sealed class FpsMultiTargetTests
    {
        private string tempDir,savedOverride;
        private GameObject playerObject,sessionObject;
        private readonly List<GameObject> facilityObjects=new List<GameObject>();
        private readonly List<GameObject> dispatchObjects=new List<GameObject>();
        private FirstPersonResponder responder;
        private FpsGazeTracker tracker;
        private TutorialSession session;
        private TextAsset procedure;

        private FacilityInspectable A,B;
        private TechnicianDispatch dispatchA,dispatchB;

        private const string ProcedureJson=@"{
  ""id"": ""multi-test"", ""version"": 3, ""title"": ""다중 대상 시험용"", ""basis"": ""시험"",
  ""steps"": [
    { ""id"":""record-verdict"", ""label"":""판정 기재"", ""basis"":""시험"", ""requires"":[],
      ""allFacts"":[""verdict-selected""], ""effect"":""record-verdict"", ""failReason"":""판정을 먼저 고르세요"" },
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
            tempDir=Path.Combine(Path.GetTempPath(),"chooguard-multi-"+System.Guid.NewGuid().ToString("N"));
            TutorialCarryoverStore.OverridePath=Path.Combine(tempDir,"tutorial-carryover.json");

            procedure=new TextAsset(ProcedureJson);
            playerObject=new GameObject("시험 플레이어",typeof(CharacterController));
            responder=playerObject.AddComponent<FirstPersonResponder>();
            tracker=playerObject.AddComponent<FpsGazeTracker>();
            tracker.Responder=responder;

            A=MakeFacility("소화기 · TEST-FE-001",true);
            B=MakeFacility("소화기 · TEST-FE-002",true);
            dispatchA=MakeDispatch(A);
            dispatchB=MakeDispatch(B);

            // 응답자는 IsPaused=true 로 시작하고 FpsInteractable.CanInteract 가 그것을 가장 먼저 막는다.
            // 재개하지 않으면 TryInteract 가 게이트 델리게이트까지 도달하지 못한다.
            responder.SetExternalInputMode(true);
            Assert.IsTrue(responder.Resume(false),"외부 입력 모드에서 재개되어야 한다");
        }

        private FacilityInspectable MakeFacility(string name,bool corroded)
        {
            var go=new GameObject(name);
            facilityObjects.Add(go);
            var f=go.AddComponent<FacilityInspectable>();
            f.SerialNumber=name.Replace("소화기 · ","");
            f.Corroded=corroded;f.AllowFieldRepair=false;
            return f;
        }

        private TechnicianDispatch MakeDispatch(FacilityInspectable target)
        {
            var go=new GameObject("기술자 인계 · "+target.name);
            dispatchObjects.Add(go);
            var d=go.AddComponent<TechnicianDispatch>();
            d.Target=target;
            d.AcknowledgeSeconds=1f;d.TravelSeconds=1f;d.WorkSeconds=1f;
            return d;
        }

        // seed 는 유닛마다 심을 초기 판정이다. NOT_RECORDED 로 주면 각 유닛이 '아직 안 고름'으로 시작해
        // v3 게이트가 실제로 걸린다 — 유출 시험이 그 조건을 쓴다.
        private TutorialSession MakeSession(FacilityInspectable startTarget)
            =>MakeSession(startTarget,InspectionVerdict.UNFIT);

        private TutorialSession MakeSession(FacilityInspectable startTarget,InspectionVerdict seed)
        {
            sessionObject=new GameObject("세션");
            sessionObject.SetActive(false);
            session=sessionObject.AddComponent<TutorialSession>();
            session.Responder=responder;session.GazeTracker=tracker;
            session.Units=new List<TutorialSession.UnitBinding>
            {
                new TutorialSession.UnitBinding{Facility=A,Dispatch=dispatchA},
                new TutorialSession.UnitBinding{Facility=B,Dispatch=dispatchB},
            };
            session.Target=startTarget;
            session.ProcedureAsset=procedure;
            session.PendingVerdict=seed;
            session.DriveHandoffWithFrameTime=false;
            session.WriteCarryover=false;session.ApplyCarryoverOnStart=false;
            sessionObject.SetActive(true);
            session.EnsureLoaded();session.Bind();
            return session;
        }

        [TearDown]
        public void TearDown()
        {
            if(sessionObject!=null)Object.Destroy(sessionObject);
            foreach(var go in dispatchObjects)if(go!=null)Object.Destroy(go);
            foreach(var go in facilityObjects)if(go!=null)Object.Destroy(go);
            dispatchObjects.Clear();facilityObjects.Clear();
            if(playerObject!=null)Object.Destroy(playerObject);
            if(procedure!=null)Object.Destroy(procedure);
            TutorialCarryoverStore.OverridePath=savedOverride;
            try { if(Directory.Exists(tempDir))Directory.Delete(tempDir,true); } catch { /* 정리 실패는 시험 결과가 아니다 */ }
        }

        [Test]
        public void 세션은_하나인데_유닛은_둘이다()
        {
            MakeSession(A);
            Assert.AreEqual(2,session.UnitCount,"유닛이 둘로 구성되지 않았습니다");
            Assert.AreSame(A,session.ActiveFacility,"지정한 시작 대상이 활성이 아닙니다");
        }

        [Test]
        public void 활성_전환이_각_유닛의_진행_상태를_보존한다()
        {
            MakeSession(A);
            Assert.IsTrue(session.Advance(),"A 판정 기재 · "+session.LastReason);   // A: 판정
            Assert.IsTrue(session.Advance(),"A 요구 발행 · "+session.LastReason);   // A: 요구

            session.Activate(B);
            Assert.AreSame(B,session.ActiveFacility);
            Assert.AreEqual(InspectionVerdict.NOT_RECORDED,B.Verdict,"B 가 A 의 진행을 물려받았습니다");
            Assert.IsTrue(session.Advance(),"B 판정 기재 · "+session.LastReason);   // B: 판정
            Assert.AreEqual(InspectionVerdict.UNFIT,B.Verdict);

            session.Activate(A);
            // A 로 돌아오면 A 의 다음 단계여야 한다. A 는 요구까지 끝냈으므로 점검표 차례다.
            Assert.AreEqual("점검표 부착",session.Peek().Step?.Label,"A 의 진행 상태가 보존되지 않았습니다");
        }

        [Test]
        public void 기술자를_기다리는_동안_다른_소화기를_점검할_수_있다()
        {
            MakeSession(A);
            session.Advance();session.Advance();                 // A: 판정 → 요구
            Assert.AreEqual(HandoffStage.RECEIVED,dispatchA.Stage,"A 의 인계가 시작되지 않았습니다");
            Assert.IsFalse(dispatchA.Completed);

            // A 의 기술자가 오는 동안 B 로 걸어가 끝까지 점검한다.
            session.Activate(B);
            Assert.IsTrue(session.Advance(),"B 판정 · "+session.LastReason);
            Assert.IsTrue(session.Advance(),"B 요구 · "+session.LastReason);
            Assert.IsTrue(session.Advance(),"B 점검표 · "+session.LastReason);
            Assert.IsTrue(session.Advance(),"B 닫기 · "+session.LastReason);

            Assert.AreEqual(HandoffStage.RECEIVED,dispatchA.Stage,"B 를 점검하는 사이 A 의 인계가 움직였습니다");
            session.Activate(A);
            Assert.IsFalse(session.Finished,"B 를 닫았다고 A 까지 닫혔습니다");
        }

        // ECC 리뷰 지적 — 이전 판은 dispatchA.Tick 을 직접 불러서, 검증하려던
        // TutorialSession.Update() 의 foreach 를 전혀 실행하지 않았다. Update 를 지워도 통과하는
        // 동어반복이었다. 이제 프레임 시간으로 구동해 세션이 실제로 미는지를 본다.
        [UnityTest]
        public IEnumerator 세션이_활성이_아닌_유닛의_인계도_프레임마다_진행시킨다()
        {
            MakeSession(A);
            session.Advance();session.Advance();                 // A: 판정 → 요구
            session.Activate(B);                                 // 활성은 B

            // 소요를 0 으로 둔다. 배치모드의 deltaTime 은 아주 작을 수 있어 짧은 값이라도
            // 프레임 수를 예측할 수 없지만, 0 이면 Tick 한 번에 정확히 한 단계씩 넘어간다.
            dispatchA.AcknowledgeSeconds=0f;dispatchA.TravelSeconds=0f;dispatchA.WorkSeconds=0f;
            session.DriveHandoffWithFrameTime=true;              // 세션의 Update 가 민다

            for(int i=0;i<30&&dispatchA.Stage!=HandoffStage.COMPLETED;i++)yield return null;

            Assert.AreEqual(HandoffStage.COMPLETED,dispatchA.Stage,
                "활성이 아니라고 A 의 기술자가 멈추면 '기다리는 동안 다른 일을 함'이 성립하지 않습니다");
            Assert.IsTrue(A.ServiceCompleted,"A 가 교체되지 않았습니다");
            Assert.IsFalse(B.ServiceCompleted,"B 까지 교체됐습니다");
            Assert.AreEqual(HandoffStage.NONE,dispatchB.Stage,"요구하지 않은 B 의 인계가 시작됐습니다");
        }

        // ECC 리뷰 지적 — 7건 중 어디서도 Bind() 가 꽂은 상호작용 클로저를 발화시키지 않아,
        // 클로저가 엉뚱한 유닛을 잡도록 회귀해도 전부 통과했다. 실제 상호작용 경로를 태운다.
        [Test]
        public void 상호작용한_유닛이_활성이_되고_그_유닛만_진행한다()
        {
            MakeSession(A);                                      // 활성은 A

            Assert.IsTrue(B.TryInteract(responder,out _),"B 상호작용이 거부됐습니다");

            Assert.AreSame(B,session.ActiveFacility,"상호작용한 유닛이 활성이 되지 않았습니다");
            Assert.AreEqual(InspectionVerdict.UNFIT,B.Verdict,"B 의 단계가 진행되지 않았습니다");
            Assert.AreEqual(InspectionVerdict.NOT_RECORDED,A.Verdict,"상호작용하지 않은 A 까지 진행됐습니다");
        }

        [Test]
        public void 감사_결과는_모든_유닛을_모아_보고한다()
        {
            MakeSession(A);
            session.Advance();session.Advance();session.Advance();session.Advance();  // A 끝까지
            session.Activate(B);
            session.Advance();session.Advance();session.Advance();session.Advance();  // B 끝까지

            var report=string.Join(" / ",session.AuditFindings);
            StringAssert.Contains("TEST-FE-001",report,"A 의 지적이 보고에 없습니다");
            StringAssert.Contains("TEST-FE-002",report,"B 의 지적이 보고에 없습니다");
        }

        [Test]
        public void 활성이_아닌_유닛도_자기_다음_단계를_안내한다()
        {
            MakeSession(A);
            session.Advance();                                   // A 만 판정 기재

            // B 의 프롬프트는 B 의 상태를 보고 만들어져야 한다. A 를 따라가면 안 된다.
            Assert.AreEqual("판정 기재",B.Prompt,"활성이 아닌 유닛이 자기 단계를 안내하지 않습니다");
            // A 는 부적합으로 기재했으므로 조건부 단계인 요구 인계가 살아난다. 점검표는 그다음이다.
            Assert.AreEqual("요구 인계",A.Prompt,"활성 유닛의 안내가 갱신되지 않았습니다");
        }

        // ECC 지적 — PendingVerdict 가 세션 전역이라 A 에서 고른 값이 B 로 샜다.
        // B 에서 아무것도 고르지 않았는데 게이트가 열리고 A 의 판정이 그대로 기재되던 결함이다.
        [Test]
        public void 한_유닛의_판정_선택이_다른_유닛으로_새지_않는다()
        {
            MakeSession(A,InspectionVerdict.NOT_RECORDED);        // 둘 다 '아직 안 고름'

            Assert.IsTrue(session.SelectVerdict(InspectionVerdict.FIT),"A 판정 선택이 거부됐습니다");
            Assert.IsTrue(session.Advance(),"A 기재가 막혔습니다 · "+session.LastReason);
            Assert.AreEqual(InspectionVerdict.FIT,A.Verdict);

            session.Activate(B);

            Assert.IsFalse(session.VerdictSelected,"A 에서 고른 판정이 B 로 샜습니다");
            Assert.IsFalse(session.Advance(),"B 에서 고르지 않았는데 기재가 진행됐습니다");
            Assert.AreEqual(InspectionVerdict.NOT_RECORDED,B.Verdict,"B 에 A 의 판정이 기재됐습니다");
        }

        [Test]
        public void 유닛마다_고른_판정이_따로_보존된다()
        {
            MakeSession(A,InspectionVerdict.NOT_RECORDED);

            session.SelectVerdict(InspectionVerdict.FIT);          // A: 적합
            session.Activate(B);
            session.SelectVerdict(InspectionVerdict.UNFIT);        // B: 부적합

            session.Activate(A);
            Assert.AreEqual(InspectionVerdict.FIT,session.PendingVerdict,"A 의 선택이 B 것으로 덮였습니다");
            session.Activate(B);
            Assert.AreEqual(InspectionVerdict.UNFIT,session.PendingVerdict,"B 의 선택이 사라졌습니다");
        }

        [Test]
        public void 재시행은_모든_유닛을_되돌린다()
        {
            MakeSession(A);
            session.Advance();session.Advance();
            session.Activate(B);
            session.Advance();

            session.Restart(false);

            Assert.AreEqual(InspectionVerdict.NOT_RECORDED,A.Verdict,"A 가 초기화되지 않았습니다");
            Assert.AreEqual(InspectionVerdict.NOT_RECORDED,B.Verdict,"B 가 초기화되지 않았습니다");
            Assert.AreEqual(HandoffStage.NONE,dispatchA.Stage,"A 의 인계가 초기화되지 않았습니다");
            Assert.IsTrue(A.Corroded,"A 의 월드 상태가 복원되지 않았습니다");
        }
    }
}
