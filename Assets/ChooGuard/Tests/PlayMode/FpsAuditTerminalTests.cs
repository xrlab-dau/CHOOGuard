using System.Collections;
using System.Linq;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Tutorial;
using ChooGuard.App.Fps.Work;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ChooGuard.Tests.PlayMode
{
    // 판정 단말 — 감사 열거의 소재지.
    //
    // 삭제된 FpsChecklistHud 는 감사 결과를 작업 중 화면에 상주시켰다. 그것이 가장 나쁜 배치였던 이유는
    // 아직 일어나지 않은 판정을 계속 보여줬기 때문이다. TutorialSession.Audit() 위 주석이 스스로
    // "감사관 단말 · 즉시 야단치지 않는다" 라고 적었고, 이 시험들이 그 배치를 코드로 고정한다.
    //
    // 렌더(한국어 TMP 폰트 필요)와 구성(문자열)을 분리했으므로 여기서는 폰트 없이 '무엇이 쓰이는가'를 단정한다.
    public sealed class FpsAuditTerminalTests
    {
        private GameObject playerObject,facilityObject,terminalObject,sessionObject;
        private FirstPersonResponder responder;
        private FpsGazeTracker tracker;
        private FacilityInspectable facility;
        private AuditTerminal terminal;
        private TutorialSession session;
        private TextAsset procedure;

        // close-inspection 까지 도달 가능한 최소 절차. 감사는 그 단계의 효과로만 열린다.
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
            // 이월은 FpsCarryoverTests 가 다룬다. 여기서 실제 저장 경로를 건드리지 않는다.
            session.WriteCarryover=false;session.ApplyCarryoverOnStart=false;

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
        // 절차를 끝까지 돌려 Audit() 을 연다.
        private void RunToClose(InspectionVerdict verdict,bool corroded)
        {
            facility.Corroded=corroded;session.PendingVerdict=verdict;
            Gaze("serial");
            Assert.IsTrue(session.Advance(),"고유번호 판독 · "+session.LastReason);
            Assert.IsTrue(session.Advance(),"판정 기재 · "+session.LastReason);
            Assert.IsTrue(session.Advance(),"단말 재스캔 · "+session.LastReason);
            Assert.IsTrue(session.Finished,"감사가 열려야 한다");
        }

        [UnityTest]
        public IEnumerator 점검_종료_전에는_단말이_거부한다()
        {
            yield return null;
            Assert.IsFalse(session.Finished);
            Assert.IsFalse(terminal.CanInteract(responder,out var reason),"점검 중에는 단말이 열려서는 안 된다");
            StringAssert.Contains("끝나지 않았습니다",reason);
            Assert.IsFalse(terminal.IsOpen);
            // 단말은 사라지지 않는다 — 월드에 있고 거부만 한다(present_but_refuses).
            Assert.IsTrue(terminalObject.activeInHierarchy,"단말 오브젝트는 절차 중에도 존재해야 한다");
        }

        [UnityTest]
        public IEnumerator 점검_종료_후_E_한_번에_지적이_열거된다()
        {
            yield return null;
            RunToClose(InspectionVerdict.FIT,corroded:true);   // 부식인데 적합 → 오판정
            Assert.IsFalse(session.AuditFindings.Count==0,"오판정이 열거되어야 한다");

            Assert.IsTrue(terminal.TryInteract(responder,out var feedback),"종료 후에는 단말이 열려야 한다");
            Assert.IsTrue(terminal.IsOpen);
            StringAssert.Contains("지적",feedback);

            // 세션이 만든 문장이 화면 본문에 온다. 단말은 문장을 다시 짓지 않고
            // 부제가 이미 말한 대상 이름 접두사만 걷어낸다.
            var body=AuditTerminalView.ComposeBody(terminal.LastReport,"지적 사항 없음");
            string subject=terminal.LastReport.Subject;
            foreach(var finding in session.AuditFindings)
            {
                var expected=finding.StartsWith(subject+" · ")?finding.Substring((subject+" · ").Length):finding;
                StringAssert.Contains(expected,body,"감사 문장이 본문에 빠짐없이 와야 한다");
            }
            StringAssert.Contains("오판정",body);
            Assert.AreEqual(session.AuditFindings.Count-1,body.Count(c=>c=='\n'),"지적 한 건이 한 줄이어야 한다");
            // 대상 이름은 부제에서 한 번만 읽는다 — 본문에서 N 번 반복하지 않는다.
            StringAssert.Contains(subject,AuditTerminalView.ComposeSubtitle(terminal.LastReport));
            Assert.AreEqual(0,body.Split(new[]{subject},System.StringSplitOptions.None).Length-1,
                "본문에 대상 이름이 반복되어서는 안 된다 · "+body);
        }

        [UnityTest]
        public IEnumerator 지적_0건이면_목록_대신_적합_문구가_나온다()
        {
            yield return null;
            RunToClose(InspectionVerdict.FIT,corroded:false);   // 월드가 멀쩡하고 적합 판정 → 무결
            Assert.AreEqual(0,session.AuditFindings.Count,"지적이 없어야 한다 · "+string.Join(" | ",session.AuditFindings));

            Assert.IsTrue(terminal.TryInteract(responder,out _));
            Assert.IsTrue(terminal.LastReport.Clean);
            var body=AuditTerminalView.ComposeBody(terminal.LastReport,"지적 사항 없음 — 절차를 모두 충족했습니다.");
            StringAssert.Contains("지적 사항 없음",body);
            StringAssert.DoesNotContain("미완료",body,"빈 목록이나 미완료 문구를 그리지 않는다");
            StringAssert.Contains("0건",AuditTerminalView.ComposeSubtitle(terminal.LastReport));
        }

        [UnityTest]
        public IEnumerator 열린_단말은_E_를_다시_누르면_닫힌다()
        {
            yield return null;
            RunToClose(InspectionVerdict.FIT,corroded:true);
            Assert.IsTrue(terminal.TryInteract(responder,out _));
            Assert.IsTrue(terminal.IsOpen);
            Assert.AreEqual("감사 결과 닫기",terminal.InteractionPrompt,"열린 뒤에는 프롬프트가 닫기로 바뀐다");

            Assert.IsTrue(terminal.TryInteract(responder,out _),"닫기는 언제나 허용되어야 한다");
            Assert.IsFalse(terminal.IsOpen);
            Assert.AreEqual("감사 결과 확인",terminal.InteractionPrompt);
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
        public IEnumerator 단말은_튜토리얼_세션_타입을_참조하지_않는다()
        {
            yield return null;
            // 경계 리트머스. 컴파일러가 막아주지 않으므로(App/Fps 와 App/Mvp 가 단일 어셈블리) 시험이 지킨다.
            // 단말이 TutorialSession 을 직접 들면 비상대응 모드가 같은 단말을 영영 재사용할 수 없다.
            var offenders=typeof(AuditTerminal).GetFields()
                .Where(f=>f.FieldType.FullName!=null&&f.FieldType.FullName.Contains("Tutorial"))
                .Select(f=>f.Name).ToArray();
            Assert.IsEmpty(offenders,"단말이 Tutorial 타입을 들고 있다 · "+string.Join(",",offenders));
            Assert.AreEqual("ChooGuard.App.Fps.Work",typeof(AuditTerminal).Namespace,"단말은 모드 중립 네임스페이스에 있어야 한다");
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
            StringAssert.Contains("끝나지 않았습니다",responder.CurrentPrompt,"거부 사유가 중앙 프롬프트로 흐른다");

            RunToClose(InspectionVerdict.FIT,corroded:true);
            responder.RefreshInteraction();
            Assert.AreEqual("E · 감사 결과 확인",responder.CurrentPrompt,"허용되면 'E · '+라벨 로 조립된다");
        }
    }
}
