using System.Collections;
using System.IO;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Tutorial;
using ChooGuard.App.Fps.Work;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace ChooGuard.Tests.PlayMode
{
    // 회귀 시험이 아니다. 사람이 볼 프레임을 남기는 하네스다.
    //
    // 2026-09-24 에 시험 75 개가 통과하는 동안 판정 선택·점검표·입력·진입이 넷 다 깨져 있었다.
    // 공통 원인은 시험이 세션 API 를 직접 불러 사람이 지나는 경로를 건너뛴 것이었다. 그래서 이
    // 하네스는 화면을 띄운 채 첫 씬부터 실제 조작으로만 진행하고, 단계마다 프레임을 남긴다.
    // "보이는가" 는 단언으로 증명되지 않는다 — 프레임을 보는 쪽이 판정한다.
    //
    // [Explicit] 이므로 평소 실행에는 잡히지 않는다. 렌더링이 필요해 배치모드에서는 의미가 없다.
    [Explicit("화면 렌더링이 필요한 캡처 하네스")]
    public sealed class FpsCaptureHarness
    {
        private string outDir;
        private int shot;
        private FirstPersonResponder responder;
        private TutorialSession session;
        private TutorialInput input;

        [UnityTest]
        public IEnumerator 첫화면부터_점검표까지_실제_조작으로_진행한다()
        {
            // [Explicit] 만 믿으면 안 된다. 2026-09-25 배치모드 회귀 실행에 이 하네스가 그대로 섞여
            // 들어갔고, 배치모드에서는 WaitForEndOfFrame 이 반환되지 않아 40 분간 멈춰 있었다.
            // 팀의 회귀 실행을 내 진단 도구가 망가뜨리는 일은 없어야 한다. 조용히 넘어가지 않고
            // 결과에 '건너뜀' 으로 남긴다.
            if(UnityEngine.Application.isBatchMode)
                Assert.Ignore("렌더링이 필요한 캡처 하네스다. 배치모드에서는 프레임을 남길 수 없고 "
                              +"WaitForEndOfFrame 이 반환되지 않아 실행이 멈춘다. 에디터로 실행하라.");

            outDir=System.Environment.GetEnvironmentVariable("CHOO_CAPTURE_DIR");
            if(string.IsNullOrEmpty(outDir))outDir=Path.Combine(Path.GetTempPath(),"choo-captures");
            Directory.CreateDirectory(outDir);
            Debug.Log("[캡처] 저장 위치 · "+outDir);

            // ── 1. 첫 화면 ───────────────────────────────────────────────
            SceneManager.LoadScene("Bootstrap");
            yield return null;yield return null;
            yield return Shot("첫화면");

            var entry=Object.FindFirstObjectByType<SceneEntryPoint>();
            Assert.IsNotNull(entry,"Bootstrap 에 SceneEntryPoint 가 없다 — 진입 경로가 끊겼다");
            Assert.IsTrue(entry.Load(),"FpsStation 으로 이동하지 못했다");
            for(int i=0;i<10&&SceneManager.GetActiveScene().name!="FpsStation";i++)yield return null;
            Assert.AreEqual("FpsStation",SceneManager.GetActiveScene().name,"씬이 바뀌지 않았다");
            yield return null;
            yield return Shot("진입직후-일시정지안내");

            // ── 2. 조작 개시 ─────────────────────────────────────────────
            responder=Object.FindFirstObjectByType<FirstPersonResponder>();
            session=Object.FindFirstObjectByType<TutorialSession>();
            input=Object.FindFirstObjectByType<TutorialInput>();
            Assert.IsNotNull(responder,"FirstPersonResponder 가 없다");
            Assert.IsNotNull(session,"TutorialSession 이 없다");
            Assert.IsNotNull(input,"TutorialInput 이 없다 — 판정을 고를 수단이 없다");
            Assert.Greater(session.Units.Count,0,"세션에 유닛이 하나도 없다");

            // 합성 입력을 OS 로 쏘지 않는다. 같은 경로를 명시적으로 구동하는 어댑터를 쓴다.
            responder.SetExternalInputMode(true);
            Assert.IsTrue(responder.Resume(false),"재개하지 못했다");
            yield return null;
            yield return Shot("재개직후-HUD");

            // 세션이 물린 대상을 쓴다. Units[0] 을 쓰면 튜토리얼이 시작하는 설비가 아닌 것을 잰다 —
            // 2026-09-25 에 이 실수로 시작 지점 거리를 44.41m 로 보고했는데, 실제 지정 대상까지는
            // 6.32m 였다. 무엇을 재는지 틀리면 수치가 아무리 정확해도 결론이 틀린다.
            var facility=session.Target!=null?session.Target:session.Units[0].Facility;
            Assert.IsNotNull(facility,"세션에 물린 설비가 없다");
            Debug.Log("[계측] 대상 "+facility.SerialNumber
                      +(session.Target==null?" (Target 이 비어 Units[0] 사용)":" (세션 Target)"));

            // ── 2.5 월드 계측 ────────────────────────────────────────────
            // 첫 실행에서 화면이 온통 청록색이고 설비까지 걸어가지 못했다. 원인을 추측하지 않고
            // 씬 안에서 직접 잰다. 이 수치가 없으면 "멀어서" 인지 "바닥이 없어서" 인지 구분되지 않는다.
            var start=responder.transform.position;
            var facilityPosition=facility.transform.position;
            Debug.Log("[계측] 플레이어 "+start.ToString("F2")+" · 첫 설비 "+facilityPosition.ToString("F2")
                      +" · 수평거리 "+Vector3.Distance(new Vector3(start.x,0,start.z),
                                                       new Vector3(facilityPosition.x,0,facilityPosition.z)).ToString("F2")+"m");
            Debug.Log("[계측] 발밑 바닥 "+(Physics.Raycast(start+Vector3.up*.5f,Vector3.down,out var ground,50f)
                      ? ground.collider.name+" · "+ground.distance.ToString("F2")+"m 아래"
                      : "없음 — 60m 아래까지 아무것도 없다"));
            Debug.Log("[계측] 스카이박스 "+(RenderSettings.skybox==null?"없음":RenderSettings.skybox.name
                      +" / shader="+(RenderSettings.skybox.shader==null?"없음":RenderSettings.skybox.shader.name)
                      +" / 지원="+(RenderSettings.skybox.shader!=null&&RenderSettings.skybox.shader.isSupported)));
            Debug.Log("[계측] 카메라 clearFlags="+responder.PlayerCamera.clearFlags
                      +" · far="+responder.PlayerCamera.farClipPlane);
            int near=0,total=0;
            foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                total++;
                if(Vector3.Distance(r.bounds.center,start)<=15f)near++;
            }
            Debug.Log("[계측] 렌더러 총 "+total+" · 플레이어 15m 이내 "+near);
            var sceneLights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            Debug.Log("[계측] 광원 "+sceneLights.Length
                      +(sceneLights.Length>0?" · 첫 광원 "+sceneLights[0].type+" 밝기 "+sceneLights[0].intensity:""));

            // 어디를 향해 걷는지 먼저 남긴다. 빈 화면이면 그것도 증거다.
            yield return Aim(facilityPosition+Vector3.up*.8f);
            yield return Shot("첫설비-바라본-방향");

            // ── 3. 설비 앞으로 ───────────────────────────────────────────
            // 가까우면 실제로 걸어간다. 멀면 길찾기를 흉내내지 않고 건너뛴다 — 그건 튜토리얼이 아니라
            // 내 경로 알고리즘을 시험하는 것이 된다. 어느 쪽을 했는지 반드시 남긴다.
            float startDistance=Vector3.Distance(new Vector3(start.x,0,start.z),
                                                 new Vector3(facilityPosition.x,0,facilityPosition.z));
            if(startDistance<=4f)
            {
                Debug.Log("[계측] 시작 지점에서 "+startDistance.ToString("F2")+"m · 걸어서 접근한다");
                yield return WalkTo(facilityPosition,1.3f);
            }
            else
            {
                Debug.Log("[건너뜀] 시작 지점에서 "+startDistance.ToString("F2")
                          +"m · 길찾기를 건너뛰고 설비 앞에 선다. 걸어서 닿는지는 사람이 확인해야 한다");
                yield return PlaceInFrontOf(facility);
            }
            yield return Shot("설비-앞");
            yield return Shot("접근-"+Safe(facility.name));

            // ── 4. 절차를 실제 조작으로 진행 ─────────────────────────────
            yield return GazeAndInteract(facility,"serial","고유번호");
            yield return GazeAndInteract(facility,"spec-plate","제원표");
            yield return GazeAndInteract(facility,"gauge","지시압력계");
            yield return GazeAndInteract(facility,"body","본체");
            yield return Shot("관측4종-완료");

            // 판정은 월드가 정한다. 하네스가 정답을 미리 아는 것이 아니라, 사람이 눈으로 볼
            // 것과 같은 값(ShouldBeUnfit)을 키로 입력한다 — 고르는 행위 자체는 실제 경로다.
            bool unfit=facility.ShouldBeUnfit;
            Assert.IsTrue(unfit?input.SelectUnfit():input.SelectFit(),"판정 키가 먹지 않았다");
            yield return null;
            yield return Shot("판정선택-"+(unfit?"부적합":"적합"));

            yield return Interact("판정 기재");
            Assert.AreNotEqual(InspectionVerdict.NOT_RECORDED,facility.Verdict,"판정이 기재되지 않았다");
            yield return Shot("판정기재-완료");

            // 역할 경계. 거부가 곧 채점 항목이다 — 실제로 거부되는지 화면으로 남긴다.
            int before=session.RoleBoundaryViolations;
            input.AttemptFieldRepair();
            yield return null;
            yield return Shot("현장수리-거부");
            Assert.Greater(session.RoleBoundaryViolations,before,"현장 수리 시도가 집계되지 않았다");

            // ── 5. 부적합이면 인계, 적합이면 바로 점검표 ─────────────────
            if(unfit)
            {
                yield return Interact("수리 요구");
                // 대상 설비에 묶인 인계를 찾는다. Units[0] 을 쓰면 다른 소화기의 인계를 본다 —
                // 같은 실수를 이 하네스에서만 세 번째 한다(대상 선택, 거리 측정, 그리고 여기).
                TechnicianDispatch dispatch=null;
                foreach(var unit in session.Units)
                    if(unit.Facility==facility){dispatch=unit.Dispatch;break;}
                Assert.IsNotNull(dispatch,"기술자 인계가 배선되지 않았다");
                Assert.IsTrue(dispatch.Requested,"요구가 발행되지 않았다");
                yield return Shot("인계-요청직후");

                // 기다리는 동안 무엇이 보이는가. 이 구간이 몸 없는 기술자의 실제 체감이다.
                float waited=0f;
                while(dispatch.Stage!=HandoffStage.COMPLETED&&waited<90f){waited+=Time.deltaTime;yield return null;}
                Assert.AreEqual(HandoffStage.COMPLETED,dispatch.Stage,
                                "기술자가 "+waited.ToString("0")+"초 안에 끝내지 못했다");
                yield return Shot("인계-완료");

                yield return Interact("교체 확인");
            }

            // ── 6. 점검표 — 이 하네스의 핵심. 보이는가 ───────────────────
            // 붙은 뒤 한 장만 찍으면 "원래 있던 라벨" 과 구분되지 않는다. 같은 각도의 전/후를 남긴다.
            var tagSpot=facility.TagAnchor!=null?facility.TagAnchor.position:facility.transform.position;
            var plateSpot=facility.Point("serial").Surface.bounds.center;
            yield return Aim(tagSpot);
            // 점검표 자리는 본체 아래쪽(로컬 y=.26)이다. 거기를 보면 상호작용 대상이 잡히는지 남긴다 —
            // 실제로 여기를 조준한 채 E 를 누르면 아무 일도 일어나지 않았다(2026-09-25).
            Debug.Log("[계측] 점검표 자리 조준 시 닿는 것 "
                      +(responder.CurrentTargetCollider==null?"없음":responder.CurrentTargetCollider.name)
                      +" · 안내 \""+responder.CurrentPrompt+"\"");
            yield return Shot("점검표-부착전");

            yield return Aim(plateSpot);   // 조작은 판독면을 보고 한다
            yield return Interact("점검표 부착");
            yield return Aim(tagSpot);
            Assert.IsNotNull(facility.AttachedTag,"점검표가 붙지 않았다");
            var tagRenderer=facility.AttachedTag.GetComponentInChildren<Renderer>();
            Assert.IsNotNull(tagRenderer,"점검표에 렌더러가 없다 — 보이지 않는다");
            Assert.IsTrue(tagRenderer.enabled,"점검표 렌더러가 꺼져 있다");

            // 렌더러가 있고 켜져 있다는 것은 "보인다" 가 아니다. 2026-09-25 부착 전/후 프레임이
            // 완전히 같았다 — 단언은 통과하는데 화면에 아무것도 없었다. 무엇이 어긋났는지 잰다.
            var tag=facility.AttachedTag;
            var eyeNow=responder.PlayerCamera.transform.position;
            var toTag=(tag.position-eyeNow).normalized;
            Debug.Log("[계측] 점검표 위치 "+tag.position.ToString("F2")
                      +" · 눈에서 "+Vector3.Distance(eyeNow,tag.position).ToString("F2")+"m"
                      +" · 월드크기 "+tag.lossyScale.ToString("F3")
                      +" · 활성 "+tag.gameObject.activeInHierarchy);
            Debug.Log("[계측] 점검표 bounds "+tagRenderer.bounds.size.ToString("F3")
                      +" · isVisible "+tagRenderer.isVisible
                      +" · 머티리얼 "+(tagRenderer.sharedMaterial==null?"없음":tagRenderer.sharedMaterial.name)
                      +" · 셰이더 "+(tagRenderer.sharedMaterial==null||tagRenderer.sharedMaterial.shader==null
                                     ?"없음":tagRenderer.sharedMaterial.shader.name));
            // 쿼드는 한쪽 면만 그리고 보이는 면은 -forward 쪽이다. 따라서 dot(forward, 눈→점검표)
            // 이 양수일 때 보인다 — 처음에 이 부호를 반대로 적어 "뒷면" 이라고 찍혔는데 실제로는
            // 그려지고 있었다. 판정은 이 로그가 아니라 부착 전/후 픽셀 비교로 한다.
            Debug.Log("[계측] 점검표 forward "+tag.forward.ToString("F2")
                      +" · 눈→점검표 "+toTag.ToString("F2")
                      +" · dot "+Vector3.Dot(tag.forward,toTag).ToString("F2")
                      +" ("+(Vector3.Dot(tag.forward,toTag)>0?"보이는 면":"뒷면 — 그려지지 않는다")+")");
            yield return Shot("점검표-부착직후");

            // 한 발 물러나 실제로 눈에 들어오는 크기인지 남긴다.
            yield return StepBack(.8f);
            yield return Shot("점검표-물러나서");

            Debug.Log("[캡처] 완료 · "+shot+" 장 · "+outDir);
        }

        // ── 보조 ────────────────────────────────────────────────────────
        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            var bytes=texture.EncodeToPNG();
            Object.Destroy(texture);
            var path=Path.Combine(outDir,(++shot).ToString("00")+"-"+Safe(name)+".png");
            File.WriteAllBytes(path,bytes);
            Debug.Log("[캡처] "+Path.GetFileName(path));
        }

        private static string Safe(string name)
        {
            foreach(var c in Path.GetInvalidFileNameChars())name=name.Replace(c,'-');
            return name.Replace(' ','-');
        }

        // 시선을 맞춘다. yaw 는 더해지고 pitch 는 빼진다(FirstPersonResponder.Simulate 참조).
        private IEnumerator Aim(Vector3 point,int maxFrames=240)
        {
            for(int i=0;i<maxFrames;i++)
            {
                var camera=responder.PlayerCamera.transform;
                var to=point-camera.position;
                if(to.sqrMagnitude<1e-6f)yield break;
                var direction=to.normalized;
                float desiredYaw=Quaternion.LookRotation(direction).eulerAngles.y;
                float desiredPitch=-Mathf.Asin(Mathf.Clamp(direction.y,-1f,1f))*Mathf.Rad2Deg;
                float dYaw=Mathf.DeltaAngle(responder.YawDegrees,desiredYaw);
                float dPitch=responder.PitchDegrees-desiredPitch;
                if(Mathf.Abs(dYaw)<.15f&&Mathf.Abs(dPitch)<.15f)yield break;
                responder.StepInput(Vector2.zero,new Vector2(Mathf.Clamp(dYaw,-10,10),Mathf.Clamp(dPitch,-10,10)),
                                    false,false,false,Mathf.Clamp(Time.deltaTime,.005f,.05f));
                yield return null;
            }
        }

        private IEnumerator WalkTo(Vector3 target,float stopDistance,int maxFrames=4000)
        {
            var start=responder.transform.position;
            float closest=float.PositiveInfinity;int stalled=0;
            for(int i=0;i<maxFrames;i++)
            {
                var here=responder.transform.position;
                var flat=new Vector3(target.x,here.y,target.z);
                float distance=Vector3.Distance(here,flat);
                if(distance<=stopDistance)yield break;
                // 제자리걸음을 조용히 반복하지 않는다. 막혔으면 막혔다고 말한다.
                if(distance<closest-.02f){closest=distance;stalled=0;}else stalled++;
                if(stalled>240)
                    Assert.Fail("벽에 막혔다 · 남은 거리 "+distance.ToString("F2")+"m · 현재 "+here.ToString("F2")
                                +" · 목표 "+flat.ToString("F2"));
                if(here.y<start.y-20f)
                    Assert.Fail("바닥이 없어 아래로 떨어졌다 · 시작 y="+start.y.ToString("F2")+" 현재 y="+here.y.ToString("F2"));
                float desiredYaw=Quaternion.LookRotation((flat-here).normalized).eulerAngles.y;
                float dYaw=Mathf.DeltaAngle(responder.YawDegrees,desiredYaw);
                responder.StepInput(new Vector2(0,1),new Vector2(Mathf.Clamp(dYaw,-10,10),0),
                                    true,false,false,Mathf.Clamp(Time.deltaTime,.005f,.05f));
                yield return null;
            }
            Assert.Fail("걸어도 닿지 않았다 · 남은 거리 "
                        +Vector3.Distance(responder.transform.position,
                                          new Vector3(target.x,responder.transform.position.y,target.z)).ToString("F2")+"m");
        }

        // 설비의 판독면 쪽으로 1.1m 앞에 세운다. CharacterController 는 직접 위치를 넣으면
        // 되밀기 때문에 잠깐 끈 뒤 옮긴다. 이동을 대신하는 것이 아니라 이동을 생략하는 것이다.
        private IEnumerator PlaceInFrontOf(FacilityInspectable facility)
        {
            var serial=facility.Point("serial");
            Assert.IsNotNull(serial?.Surface,"판독면이 없어 설비 앞에 설 수 없다");
            // 어느 자리에 서야 판독면이 보이는지를 추측하지 않고 실제로 찾는다. 앞서 두 번,
            // 방향을 계산으로 정했다가 벽에 박히고 바닥 아래로 떨어졌다. 여기서 한 자리도
            // 찾지 못하면 그 자체가 결과다 — "설 수 있는 자리가 없다" 는 것도 사실이다.
            var plate=serial.Surface.bounds.center;
            var controller=responder.GetComponent<CharacterController>();
            var tried=new System.Text.StringBuilder();
            bool placed=false;

            foreach(var sign in new[]{1f,-1f})
            {
            if(placed)break;
            foreach(var distance in new[]{1.0f,1.3f,1.6f,2.0f})
            {
                var facing=facility.transform.forward*sign;
                facing.y=0;
                if(facing.sqrMagnitude<1e-4f)continue;
                facing.Normalize();
                var spot=facility.transform.position+facing*distance;

                // 바닥을 먼저 찾는다. 중력에 맡기면 틈으로 빠진다.
                if(!Physics.Raycast(spot+Vector3.up*2f,Vector3.down,out var floor,6f,~0,QueryTriggerInteraction.Ignore))
                {
                    tried.Append("\n  "+(sign>0?"정면":"뒷면")+" "+distance.ToString("F1")+"m · 바닥 없음");
                    continue;
                }
                controller.enabled=false;
                responder.transform.position=floor.point+Vector3.up*.02f;
                controller.enabled=true;
                yield return null;

                var eye=responder.PlayerCamera.transform.position;
                bool blocked=Physics.Raycast(eye,(plate-eye).normalized,out var hit,
                                             Vector3.Distance(eye,plate)+.05f,~0,QueryTriggerInteraction.Ignore)
                             &&hit.collider!=serial.Surface;
                tried.Append("\n  "+(sign>0?"정면":"뒷면")+" "+distance.ToString("F1")+"m · 바닥 "
                             +floor.collider.name+" · "
                             +(blocked?"가림 "+hit.collider.name+" ("+hit.distance.ToString("F2")+"m)":"시야 확보"));
                if(blocked)continue;
                placed=true;
                Debug.Log("[계측] 선 자리 "+responder.transform.position.ToString("F2")
                          +" · "+(sign>0?"정면":"뒷면")+" "+distance.ToString("F1")+"m");
                break;
            }
            }
            Assert.IsTrue(placed,"판독면이 보이는 자리를 한 곳도 찾지 못했다 · 시도한 자리:"+tried);

            yield return Aim(plate);

            // 무엇을 조준하고 있는지 눈에 보이지 않을 때를 대비해 남긴다.
            var camera=responder.PlayerCamera.transform;
            var target=serial.Surface.bounds.center;
            Debug.Log("[계측] 배치 후 플레이어 "+responder.transform.position.ToString("F2")
                      +" · 판독면 "+target.ToString("F2")
                      +" · 눈에서 거리 "+Vector3.Distance(camera.position,target).ToString("F2")+"m"
                      +" · 상호작용 상한 "+responder.InteractionDistance+"m");
            Debug.Log("[계측] 조준 중인 콜라이더 "
                      +(responder.CurrentTargetCollider==null?"없음":responder.CurrentTargetCollider.name)
                      +" · 안내 \""+responder.CurrentPrompt+"\"");
            if(Physics.Raycast(camera.position,(target-camera.position).normalized,out var blocker,
                               Vector3.Distance(camera.position,target)+.1f,~0,QueryTriggerInteraction.Ignore))
                Debug.Log("[계측] 시선이 처음 닿는 것 "+blocker.collider.name
                          +" ("+blocker.distance.ToString("F2")+"m) · 판독면인가 "
                          +(blocker.collider==serial.Surface));
        }

        private IEnumerator StepBack(float distance,int maxFrames=300)
        {
            var start=responder.transform.position;
            for(int i=0;i<maxFrames&&Vector3.Distance(start,responder.transform.position)<distance;i++)
            {
                responder.StepInput(new Vector2(0,-1),Vector2.zero,false,false,false,
                                    Mathf.Clamp(Time.deltaTime,.005f,.05f));
                yield return null;
            }
        }

        // 관측 지점을 보고 체류시킨 뒤 E 로 단계를 넘긴다. 시선은 FpsGazeTracker 가 프레임마다 쌓는다.
        private IEnumerator GazeAndInteract(FacilityInspectable facility,string pointId,string label)
        {
            var point=facility.Point(pointId);
            Assert.IsNotNull(point,"관측 지점이 없다 · "+pointId);
            Assert.IsNotNull(point.Surface,"관측 지점에 콜라이더가 없다 · "+pointId);
            yield return Aim(point.Surface.bounds.center);
            for(int i=0;i<300&&!facility.Observed(pointId);i++)yield return null;
            Assert.IsTrue(facility.Observed(pointId),
                          label+"("+pointId+") 를 조준했는데 시선이 쌓이지 않았다 — 빗나가거나 가려져 있다");
            yield return Interact(label);
        }

        private IEnumerator Interact(string what)
        {
            Assert.IsTrue(responder.TryInteract(),
                          what+" · E 가 먹지 않았다 · 안내=\""+responder.CurrentPrompt
                          +"\" 결과=\""+responder.LastFeedback+"\"");
            yield return null;
        }
    }
}
