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
    // "시험은 통과하는데 사람이 켜면 아무것도 안 되는" 상태를 막는 계약.
    //
    // 2026-09-24 확인: 생성기가 월드 상태를 보고 정답을 PendingVerdict 에 미리 넣어 두어
    // 플레이어가 고를 것이 없었고 MisjudgementCount 가 영원히 0 이었다. 점검표는 렌더러 없는
    // 빈 GameObject 라 보이지 않았고, TryFieldRepair 는 호출처가 0 건이었다.
    public sealed class FpsPlayableTests
    {
        private string tempDir,savedOverride;
        private GameObject playerObject,facilityObject,anchorObject,sessionObject,tagTemplate;
        private FirstPersonResponder responder;
        private FpsGazeTracker tracker;
        private FacilityInspectable facility;
        private TutorialSession session;
        private TutorialInput input;
        private TextAsset procedure;

        // v3. record-verdict 가 verdict-selected 를 요구한다 — 배포 자료와 같은 게이트다.
        private const string ProcedureJson=@"{
  ""id"": ""playable-test"", ""version"": 3, ""title"": ""플레이 가능성 시험용"", ""basis"": ""시험"",
  ""steps"": [
    { ""id"":""record-verdict"", ""label"":""판정 기재"", ""basis"":""시험"", ""requires"":[],
      ""allFacts"":[""verdict-selected""], ""effect"":""record-verdict"",
      ""failReason"":""적합(1)·부적합(2) 중 하나를 고른 뒤 기재하세요"" },
    { ""id"":""attach-tag"", ""label"":""점검표 부착"", ""basis"":""시험"", ""requires"":[""record-verdict""],
      ""allFacts"":[""verdict-recorded""], ""effect"":""attach-tag"", ""failReason"":""판정 전에는 부착할 수 없습니다"" }
  ]
}";

        [SetUp]
        public void SetUp()
        {
            savedOverride=TutorialCarryoverStore.OverridePath;
            tempDir=Path.Combine(Path.GetTempPath(),"chooguard-playable-"+System.Guid.NewGuid().ToString("N"));
            TutorialCarryoverStore.OverridePath=Path.Combine(tempDir,"tutorial-carryover.json");

            procedure=new TextAsset(ProcedureJson);
            playerObject=new GameObject("시험 플레이어",typeof(CharacterController));
            responder=playerObject.AddComponent<FirstPersonResponder>();
            tracker=playerObject.AddComponent<FpsGazeTracker>();
            tracker.Responder=responder;

            facilityObject=new GameObject("소화기 · TEST-FE-001");
            facility=facilityObject.AddComponent<FacilityInspectable>();
            facility.SerialNumber="TEST-FE-001";
            facility.Corroded=true;                       // 정답은 부적합
            facility.AllowFieldRepair=false;
            facility.Bind(tracker);

            // 생성기가 만드는 부착 지점과 같은 구성
            anchorObject=new GameObject("점검표 부착 지점");
            anchorObject.transform.SetParent(facilityObject.transform,false);
            anchorObject.transform.localPosition=new Vector3(0,.26f,.1f);
            facility.TagAnchor=anchorObject.transform;

            tagTemplate=GameObject.CreatePrimitive(PrimitiveType.Quad);
            tagTemplate.name="점검표 원본";
            Object.DestroyImmediate(tagTemplate.GetComponent<Collider>());
            tagTemplate.SetActive(false);                 // 생성기와 같이 비활성 템플릿

            sessionObject=new GameObject("세션");
            sessionObject.SetActive(false);
            session=sessionObject.AddComponent<TutorialSession>();
            session.Responder=responder;session.GazeTracker=tracker;session.Target=facility;
            session.ProcedureAsset=procedure;session.InspectionTagPrefab=tagTemplate;
            session.DriveHandoffWithFrameTime=false;
            session.WriteCarryover=false;session.ApplyCarryoverOnStart=false;
            input=sessionObject.AddComponent<TutorialInput>();
            input.Session=session;input.Responder=responder;
            input.ReadKeyboard=false;                     // OS 입력을 합성하지 않는다
            sessionObject.SetActive(true);
            session.EnsureLoaded();session.Bind();

            // 응답자는 IsPaused=true 로 시작한다. 키 입력 경로는 그 게이트를 지나야 하므로 재개한다
            // (공개 메서드는 게이트를 통과하지 않아 이것 없이도 통과했다 — 그래서 놓칠 뻔했다).
            responder.SetExternalInputMode(true);
            Assert.IsTrue(responder.Resume(false),"외부 입력 모드에서 재개되어야 한다");
        }

        [TearDown]
        public void TearDown()
        {
            foreach(var go in new[]{sessionObject,tagTemplate,facilityObject,playerObject})if(go!=null)Object.Destroy(go);
            if(procedure!=null)Object.Destroy(procedure);
            TutorialCarryoverStore.OverridePath=savedOverride;
            try { if(Directory.Exists(tempDir))Directory.Delete(tempDir,true); } catch { /* 정리 실패는 시험 결과가 아니다 */ }
        }

        // ① 판정 선택 ------------------------------------------------------------

        [Test]
        public void 판정을_고르지_않으면_기재가_막힌다()
        {
            Assert.IsFalse(session.VerdictSelected,"시작 시 아무것도 고르지 않은 상태여야 합니다");

            Assert.IsFalse(session.Advance(),"고르지 않았는데 기재가 진행됐습니다");
            StringAssert.Contains("고른 뒤",session.LastReason);
            Assert.AreEqual(InspectionVerdict.NOT_RECORDED,facility.Verdict);
        }

        [Test]
        public void 고른_뒤에는_기재된다()
        {
            Assert.IsTrue(input.SelectUnfit(),"부적합 선택이 거부됐습니다");
            Assert.IsTrue(session.VerdictSelected);

            Assert.IsTrue(session.Advance(),"선택 후에도 기재가 막혔습니다 · "+session.LastReason);
            Assert.AreEqual(InspectionVerdict.UNFIT,facility.Verdict);
        }

        // 이것이 이 변경의 핵심이다. 정답을 미리 꽂아 두면 이 시험이 성립하지 않는다.
        [Test]
        public void 적합으로_잘못_고르면_오판정이_집계된다()
        {
            Assert.IsTrue(input.SelectFit(),"적합 선택이 거부됐습니다");
            Assert.IsTrue(session.Advance(),"기재가 막혔습니다 · "+session.LastReason);

            Assert.AreEqual(InspectionVerdict.FIT,facility.Verdict);
            Assert.IsTrue(facility.ShouldBeUnfit,"사전 조건: 월드는 부적합이어야 합니다");
            Assert.AreEqual(1,session.MisjudgementCount,"오판정이 집계되지 않았습니다");
        }

        [Test]
        public void 이미_기재한_판정은_바꿀_수_없다()
        {
            input.SelectUnfit();session.Advance();
            Assert.AreEqual(InspectionVerdict.UNFIT,facility.Verdict);

            Assert.IsFalse(input.SelectFit(),"기재 후에 판정이 바뀌었습니다");
            StringAssert.Contains("되감기",session.LastReason);
            Assert.AreEqual(InspectionVerdict.UNFIT,facility.Verdict);
        }

        // ECC 지적 — "되감기로 돌아가세요"라고 안내하면서 되감아도 다시 고를 수 없었다.
        [Test]
        public void 되감으면_판정을_다시_고를_수_있다()
        {
            input.SelectFit();session.Advance();                  // 잘못 고름
            Assert.AreEqual(InspectionVerdict.FIT,facility.Verdict);
            Assert.AreEqual(1,session.MisjudgementCount);

            Assert.IsTrue(session.Rewind("record-verdict"),"되감기가 거부됐습니다 · "+session.LastReason);

            Assert.AreEqual(InspectionVerdict.NOT_RECORDED,facility.Verdict,"되감았는데 판정이 남아 있습니다");
            Assert.IsFalse(session.VerdictSelected,"되감았는데 선택이 남아 있습니다");
            Assert.AreEqual(0,session.MisjudgementCount,"되감았는데 오판정 집계가 남아 있습니다");

            Assert.IsTrue(input.SelectUnfit(),"되감은 뒤에도 재선택이 막혔습니다 · "+session.LastReason);
            Assert.IsTrue(session.Advance(),"재기재가 막혔습니다 · "+session.LastReason);
            Assert.AreEqual(InspectionVerdict.UNFIT,facility.Verdict);
            Assert.AreEqual(0,session.MisjudgementCount,"고쳐 기재했는데 오판정이 남았습니다");
        }

        [Test]
        public void 되감기를_반복해도_오판정이_누적되지_않는다()
        {
            for(int i=0;i<3;i++)
            {
                input.SelectFit();session.Advance();
                Assert.AreEqual(1,session.MisjudgementCount,"회차 "+i+" 에서 집계가 1 이 아닙니다");
                session.Rewind("record-verdict");
                Assert.AreEqual(0,session.MisjudgementCount,"회차 "+i+" 되감기 후 0 이 아닙니다");
            }
        }

        // ECC 지적 — Update 의 키 매핑이 어느 시험에서도 실행되지 않았다.
        [Test]
        public void 키_매핑이_해당_동작으로_이어진다()
        {
            input.HandleKeys(false,true,false);                   // 2 = 부적합
            Assert.AreEqual(InspectionVerdict.UNFIT,session.PendingVerdict);

            input.HandleKeys(false,false,true);                   // F = 현장 수리 시도
            Assert.AreEqual(1,session.RoleBoundaryViolations,"F 가 현장 수리 시도로 이어지지 않았습니다");
        }

        [Test]
        public void 일시정지_중에는_키_입력을_받지_않는다()
        {
            responder.Pause();
            input.HandleKeys(true,false,true);

            Assert.IsFalse(session.VerdictSelected,"일시정지 중에 판정이 선택됐습니다");
            Assert.AreEqual(0,session.RoleBoundaryViolations,"일시정지 중에 현장 수리가 시도됐습니다");
        }

        // ② 점검표 표시 ----------------------------------------------------------

        [Test]
        public void 점검표가_부착_지점_아래에_보이게_붙는다()
        {
            input.SelectUnfit();session.Advance();         // 판정
            Assert.IsTrue(session.Advance(),"점검표 부착이 막혔습니다 · "+session.LastReason);

            var tag=facility.AttachedTag;
            Assert.IsNotNull(tag,"점검표가 부착되지 않았습니다");
            Assert.AreSame(anchorObject.transform,tag.parent,"부착 지점 아래에 붙지 않았습니다");
            Assert.IsTrue(tag.gameObject.activeInHierarchy,"비활성 템플릿의 사본이 꺼진 채 붙었습니다");
            Assert.IsNotNull(tag.GetComponent<MeshRenderer>(),"렌더러가 없어 눈에 보이지 않습니다");
        }

        [Test]
        public void 점검표는_조준을_가로채지_않는다()
        {
            input.SelectUnfit();session.Advance();session.Advance();

            var tag=facility.AttachedTag;
            Assert.IsNotNull(tag);
            Assert.IsNull(tag.GetComponent<Collider>(),
                "점검표에 콜라이더가 있으면 가장 가까운 솔리드로 잡혀 판독면 조준을 가린다");
        }

        // ③ 현장 수리 시도 --------------------------------------------------------

        [Test]
        public void 현장_수리_시도가_거부되고_감점된다()
        {
            Assert.AreEqual(0,session.RoleBoundaryViolations,"시작 시 0 이어야 합니다");

            Assert.IsFalse(input.AttemptFieldRepair(),"현장 수리가 허용됐습니다");

            Assert.AreEqual(1,session.RoleBoundaryViolations,"역할 경계 위반이 집계되지 않았습니다");
            StringAssert.Contains("소방관리 책임자",session.LastReason);
        }

        // ⑤ 진입점 ---------------------------------------------------------------

        [Test]
        public void 빌드_설정에_없는_씬은_거부한다()
        {
            var go=new GameObject("진입점");
            try
            {
                var entry=go.AddComponent<SceneEntryPoint>();
                entry.LoadOnStart=false;
                entry.SceneName="없는씬이름";
                LogAssert.Expect(LogType.Error,new Regex("빌드 설정에 없는 씬"));
                Assert.IsFalse(entry.Load(),"없는 씬인데 로드를 시도했습니다");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
