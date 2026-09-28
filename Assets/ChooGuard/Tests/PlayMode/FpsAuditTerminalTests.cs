using System.Collections;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Tutorial;
using ChooGuard.App.Fps.Work;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ChooGuard.Tests.PlayMode
{
    // 실제 단말 상호작용이 마감을 수행하며 판정 기록은 물리 결함을 고치지 않는다.
    public sealed class FpsAuditTerminalTests
    {
        private GameObject playerObject,facilityObject,terminalObject,sessionObject;
        private FirstPersonResponder responder;
        private FpsGazeTracker tracker;
        private FacilityInspectable facility;
        private AuditTerminal terminal;
        private TutorialSession session;
        private TextAsset procedure;

        // 현장 점검 후 실제 단말에서만 마감 가능한 최소 절차.
        private const string Json=@"{
  ""id"": ""audit-terminal"", ""version"": 1, ""title"": ""감사 단말 시험"", ""basis"": ""시험용"",
  ""steps"": [
    { ""id"":""read-serial"", ""label"":""고유번호 판독"", ""basis"":""제22조3"", ""requires"":[],
      ""allFacts"":[""serial-gazed""], ""effect"":""record-serial"", ""failReason"":""고유번호를 먼저 확인하세요"" },
    { ""id"":""record-verdict"", ""label"":""판정 기재"", ""basis"":""제23조2-1"", ""requires"":[""read-serial""],
      ""allFacts"":[""serial-recorded""], ""effect"":""record-verdict"", ""failReason"":""고유번호 기재가 먼저입니다"" },
    { ""id"":""close-inspection"", ""label"":""단말 재스캔"", ""basis"":""제23조2-3"", ""requires"":[""record-verdict""],
      ""allFacts"":[""verdict-recorded""], ""effect"":""close-inspection"", ""failReason"":""판정을 먼저 기재하세요"" }
  ]
}";

        [SetUp]
        public void SetUp()
        {
            procedure=new TextAsset(Json);
            playerObject=new GameObject("시험 플레이어",typeof(CharacterController));
            responder=playerObject.AddComponent<FirstPersonResponder>();
            tracker=playerObject.AddComponent<FpsGazeTracker>();tracker.Responder=responder;

            facilityObject=new GameObject("소화기");
            facilityObject.transform.position=new Vector3(0,1.1f,1.2f);
            facility=facilityObject.AddComponent<FacilityInspectable>();
            facility.SerialNumber="TERM-001";
            facility.Points=new[]{Point("serial")};
            facility.Bind(tracker);

            terminalObject=new GameObject("판정 단말");
            terminalObject.transform.position=new Vector3(1.4f,1.2f,.6f);
            var box=terminalObject.AddComponent<BoxCollider>();box.isTrigger=false;box.size=new Vector3(.5f,.4f,.1f);
            terminal=terminalObject.AddComponent<AuditTerminal>();

            sessionObject=new GameObject("세션");
            session=sessionObject.AddComponent<TutorialSession>();
            session.Responder=responder;session.GazeTracker=tracker;session.Target=facility;
            session.AuditTerminal=terminal;session.ProcedureAsset=procedure;

            // 응답자는 IsPaused=true 로 시작하고 FpsInteractable.CanInteract 가 그것을 가장 먼저 막는다.
            // 재개하지 않으면 게이트 델리게이트까지 도달하지 못해 모든 거부 사유가 기본 문구로 덮인다.
            // OS 입력을 합성하지 않는 외부 입력 모드로 재개한다(캡처 없이).
            responder.SetExternalInputMode(true);
            Assert.IsTrue(responder.Resume(false),"외부 입력 모드에서 재개되어야 한다");
        }
        private FacilityInspectable.InspectionPoint Point(string id)
        {
            var go=new GameObject("관측 · "+id);
            go.transform.SetParent(facilityObject.transform,false);
            var box=go.AddComponent<BoxCollider>();box.isTrigger=false;box.size=Vector3.one*.1f;
            return new FacilityInspectable.InspectionPoint{Id=id,Label=id,Surface=box,RequiredDwellSeconds=.5f};
        }
        [TearDown]
        public void TearDown()
        {
            foreach(var go in new[]{sessionObject,terminalObject,facilityObject,playerObject})if(go!=null)Object.Destroy(go);
            if(procedure!=null)Object.Destroy(procedure);
        }
        private void Gaze(string id)
        {
            var point=facility.Point(id);
            tracker.Accumulate(point.Surface,point.RequiredDwellSeconds+.2f);
        }
        // 현장에서는 마감할 수 없고 단말에서만 완료된다.
        private void RunToClose(InspectionVerdict verdict,bool corroded)
        {
            facility.Corroded=corroded;session.PendingVerdict=verdict;
            Gaze("serial");
            Assert.IsTrue(session.Advance(),"고유번호 판독 · "+session.LastReason);
            Assert.IsTrue(session.Advance(),"판정 기재 · "+session.LastReason);
            Assert.IsFalse(session.Advance());
            Assert.IsFalse(session.Finished);
            Assert.IsTrue(terminal.TryInteract(responder,out _),session.LastReason);
            Assert.IsTrue(session.Finished);
            terminal.Close();
        }

        [UnityTest]
        public IEnumerator 점검_종료_전에는_단말이_거부한다()
        {
            yield return null;
            Assert.IsFalse(session.Finished);
            Assert.IsFalse(terminal.CanInteract(responder,out var reason),"점검 중에는 단말이 열려서는 안 된다");
            Assert.IsFalse(terminal.IsOpen);
            // 단말은 사라지지 않는다 — 월드에 있고 거부만 한다(present_but_refuses).
            Assert.IsTrue(terminalObject.activeInHierarchy,"단말 오브젝트는 절차 중에도 존재해야 한다");
        }

        [UnityTest]
        public IEnumerator 부식_적합_오판은_단말을_열어도_설비를_정상으로_바꾸지_않는다()
        {
            yield return null;
            RunToClose(InspectionVerdict.FIT,corroded:true);
            Assert.IsTrue(terminal.TryInteract(responder,out _));
            Assert.IsTrue(terminal.IsOpen);
            Assert.IsFalse(terminal.LastReport.Clean);
            Assert.AreEqual(1,session.MisjudgementCount);
            Assert.AreEqual(InspectionVerdict.FIT,facility.Verdict);
            Assert.IsTrue(facility.Corroded);
            Assert.IsTrue(facility.ShouldBeUnfit);
        }

        [UnityTest]
        public IEnumerator 지적_0건이면_목록_대신_적합_문구가_나온다()
        {
            yield return null;
            RunToClose(InspectionVerdict.FIT,corroded:false);   // 월드가 멀쩡하고 적합 판정 → 무결
            Assert.AreEqual(0,session.AuditFindings.Count,"지적이 없어야 한다 · "+string.Join(" | ",session.AuditFindings));

            Assert.IsTrue(terminal.TryInteract(responder,out _));
            Assert.IsTrue(terminal.LastReport.Clean);
        }

        [UnityTest]
        public IEnumerator 열린_단말은_E_를_다시_누르면_닫힌다()
        {
            yield return null;
            RunToClose(InspectionVerdict.FIT,corroded:true);
            Assert.IsTrue(terminal.TryInteract(responder,out _));
            Assert.IsTrue(terminal.IsOpen);

            Assert.IsTrue(terminal.TryInteract(responder,out _),"닫기는 언제나 허용되어야 한다");
            Assert.IsFalse(terminal.IsOpen);
        }

        [UnityTest]
        public IEnumerator 되감기면_열린_단말이_스스로_닫힌다()
        {
            yield return null;
            RunToClose(InspectionVerdict.FIT,corroded:true);
            Assert.IsTrue(terminal.TryInteract(responder,out _));
            Assert.IsTrue(terminal.IsOpen);

            // 되감기는 Finished 를 false 로 돌린다 — 열려 있던 감사 결과는 그 순간 거짓이 된다.
            Assert.IsTrue(session.Rewind("record-verdict"));
            Assert.IsFalse(session.Finished);
            yield return null;   // Update() 한 프레임
            Assert.IsFalse(terminal.IsOpen,"게이트가 다시 닫히면 화면도 닫혀야 한다");
        }

        [UnityTest]
        public IEnumerator 단말에서_멀어지면_닫힌다()
        {
            yield return null;
            RunToClose(InspectionVerdict.FIT,corroded:true);
            Assert.IsTrue(terminal.TryInteract(responder,out _));
            Assert.IsTrue(terminal.IsOpen);

            yield return null;
            Assert.IsTrue(terminal.IsOpen,"제자리에서는 열린 채로 있어야 한다");

            playerObject.transform.position=new Vector3(0,0,-20);
            yield return null;
            Assert.IsFalse(terminal.IsOpen,"걸어서 멀어지면 닫힌다 — 시선 이탈로 닫으면 읽는 중 마우스 흔들림이 곧 닫힘이 된다");
        }


        [UnityTest]
        public IEnumerator 단말_거부_사유가_실제_레이캐스트_경로로_흐른다()
        {
            yield return null;
            var camera=responder.PlayerCamera.transform;
            var box=terminalObject.GetComponent<BoxCollider>();
            camera.rotation=Quaternion.LookRotation((box.bounds.center-camera.position).normalized,Vector3.up);
            responder.RefreshInteraction();
            Assert.AreSame(box,responder.CurrentTargetCollider,"단말이 조준되어야 한다");
            Assert.IsFalse(responder.TryInteract());

            RunToClose(InspectionVerdict.FIT,corroded:true);
            responder.RefreshInteraction();
            Assert.IsTrue(responder.TryInteract());
            Assert.IsTrue(terminal.IsOpen);
        }
    }
}
