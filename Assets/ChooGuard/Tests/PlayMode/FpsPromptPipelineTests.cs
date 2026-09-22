using System.Collections;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Tutorial;
using ChooGuard.App.Fps.Work;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ChooGuard.Tests.PlayMode
{
    // 나열식 점검표를 삭제할 수 있다는 근거를 못 박는 시험.
    //
    // 2026-09-22 조사 결론: 단계 라벨·거부 사유·완료 피드백은 이미 중앙 프롬프트 배관으로 흐른다.
    //   TutorialSession.RefreshPrompt() → Target.Prompt / Target.UnavailableMessage
    //   FirstPersonResponder.RefreshInteraction() → CurrentPrompt = "E · "+prompt  또는  reason
    // 이 세 단정이 통과하면 별도 패널은 같은 정보를 두 번 그리는 중복이다.
    // 실패하면 새 UI 를 만들 것이 아니라 배관을 고쳐야 한다는 뜻이다 — 어느 쪽이든 다음 행동이 정해진다.
    public sealed class FpsPromptPipelineTests
    {
        private GameObject playerObject,facilityObject,sessionObject;
        private FirstPersonResponder responder;
        private FpsGazeTracker tracker;
        private FacilityInspectable facility;
        private TutorialSession session;
        private TextAsset procedure;

        private const string Json=@"{
  ""id"": ""prompt-pipeline"", ""version"": 1, ""title"": ""배관 시험"", ""basis"": ""시험용"",
  ""steps"": [
    { ""id"":""read-serial"", ""label"":""고유번호 판독"", ""basis"":""제22조3"", ""requires"":[],
      ""allFacts"":[""serial-gazed""], ""effect"":""record-serial"", ""failReason"":""소화기의 고유번호를 먼저 확인하세요"" },
    { ""id"":""read-spec-plate"", ""label"":""제원표 판독"", ""basis"":""제23조2-1"", ""requires"":[""read-serial""],
      ""allFacts"":[""spec-plate-gazed""], ""effect"":""none"", ""failReason"":""제원표를 읽지 않고는 판정할 수 없습니다"" }
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
            facility.SerialNumber="PIPE-001";
            facility.Points=new[]{Point("serial"),Point("spec-plate")};
            facility.Bind(tracker);

            sessionObject=new GameObject("세션");
            session=sessionObject.AddComponent<TutorialSession>();
            session.Responder=responder;session.GazeTracker=tracker;session.Target=facility;
            session.ProcedureAsset=procedure;session.PendingVerdict=InspectionVerdict.UNFIT;
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
            foreach(var go in new[]{sessionObject,facilityObject,playerObject})if(go!=null)Object.Destroy(go);
            if(procedure!=null)Object.Destroy(procedure);
        }
        private void Gaze(string id)
        {
            var point=facility.Point(id);
            tracker.Accumulate(point.Surface,point.RequiredDwellSeconds+.2f);
        }

        [UnityTest]
        public IEnumerator 현재_단계_라벨이_프롬프트로_흐른다()
        {
            yield return null;
            Gaze("serial");
            var decision=session.Peek();
            Assert.IsTrue(decision.Allowed,"고유번호 판독이 가능해야 한다 · "+decision.Reason);
            Assert.AreEqual(decision.Step.Label,facility.Prompt,
                "단계 라벨이 설비 프롬프트에 이미 들어 있어야 한다 — 별도 패널이 필요 없는 근거");
            Assert.AreEqual("고유번호 판독",facility.InteractionPrompt);
        }

        [UnityTest]
        public IEnumerator 거부_사유가_프롬프트로_흐른다()
        {
            yield return null;
            // 관측하지 않은 상태 — 게이트가 막고 사유를 전달해야 한다
            var decision=session.Peek();
            Assert.IsFalse(decision.Allowed);
            Assert.AreEqual(decision.Reason,facility.UnavailableMessage,
                "거부 사유가 설비의 UnavailableMessage 에 이미 들어 있어야 한다");
            Assert.IsFalse(facility.CanInteract(responder,out var reason),"게이트가 막아야 한다");
            Assert.AreEqual("소화기의 고유번호를 먼저 확인하세요",reason);
        }

        [UnityTest]
        public IEnumerator 완료_피드백이_방금_끝낸_단계를_담는다()
        {
            yield return null;
            Gaze("serial");
            Assert.IsTrue(session.Advance(),session.LastReason);
            Assert.AreEqual("고유번호 판독 완료",facility.SuccessMessage,
                "SuccessMessage 는 방금 끝낸 단계를 담아야 한다 — TryInteract 가 핸들러 뒤에 이것을 읽는다");
            Assert.AreEqual("제원표 판독",facility.Prompt,"프롬프트는 다음 단계로 넘어가야 한다");
        }

        [UnityTest]
        public IEnumerator 배관은_실제_레이캐스트_경로에서도_흐른다()
        {
            yield return null;
            responder.SetExternalInputMode(true);
            Assert.IsTrue(responder.Resume(false),"외부 입력 모드에서 재개되어야 한다");
            var plate=facility.Point("serial").Surface;
            var camera=responder.PlayerCamera.transform;
            camera.rotation=Quaternion.LookRotation((plate.bounds.center-camera.position).normalized,Vector3.up);
            responder.RefreshInteraction();
            Assert.AreSame(plate,responder.CurrentTargetCollider,"관측 지점이 조준되어야 한다");
            // 미관측 상태이므로 거부 사유가 그대로 CurrentPrompt 에 온다(FirstPersonResponder.cs 의 else 분기)
            Assert.AreEqual("소화기의 고유번호를 먼저 확인하세요",responder.CurrentPrompt);

            Gaze("serial");
            responder.RefreshInteraction();
            Assert.AreEqual("E · 고유번호 판독",responder.CurrentPrompt,
                "허용 상태에서는 'E · '+라벨 로 조립되어야 한다 — 화면에 그릴 문자열이 이미 완성돼 있다");
        }
    }
}
