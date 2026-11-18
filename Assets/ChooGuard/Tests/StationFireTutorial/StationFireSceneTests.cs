using System;
using System.Collections;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.StationFireTutorial;
using ChooGuard.App.Fps.Shell;
using ChooGuard.App.Fps.TrainDeparture;
using ChooGuard.Seeded.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChooGuard.Tests.StationFireTutorial
{
    public sealed class StationFireSceneTests
    {
        StationFireTutorialSession lesson;
        float Dt => Mathf.Clamp(Time.deltaTime,.001f,.1f);
        float Horizontal(Vector3 a,Vector3 b) { a.y=b.y=0;return Vector3.Distance(a,b); }
        void Look(Vector3 point)
        {
            var direction=point-lesson.View.transform.position;
            float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            float pitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
            lesson.Player.StepInput(Vector2.zero,new Vector2(Mathf.DeltaAngle(lesson.Player.YawDegrees,yaw),
                lesson.Player.PitchDegrees-pitch),false,Dt);
        }
        IEnumerator WaitFor(Func<bool> condition,float seconds,string detail)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while(!condition()&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(condition(),Is.True,detail+"; step="+lesson?.Step+"; failure="+lesson?.Failure);
        }
        IEnumerator FollowVisibleRoute()
        {
            yield return null;yield return null;
            float deadline=Time.realtimeSinceStartup+35,stuck=0;
            while(lesson.VisibleRoute.Count>1&&Horizontal(lesson.Player.transform.position,lesson.VisibleRoute.Last())>.4f&&Time.realtimeSinceStartup<deadline)
            {
                var corner=lesson.VisibleRoute.Skip(1).FirstOrDefault(p=>Horizontal(p,lesson.Player.transform.position)>.25f);
                var delta=corner-lesson.Player.transform.position;delta.y=0;
                Look(lesson.Player.transform.position+delta.normalized*3+Vector3.up*1.6f);
                var local=lesson.Player.transform.InverseTransformDirection(delta.normalized);var before=lesson.Player.transform.position;
                lesson.Player.StepInput(new Vector2(local.x,local.z),Vector2.zero,false,Dt);lesson.Controls.StepInput(default,Dt);
                stuck=Horizontal(before,lesson.Player.transform.position)<.003f?stuck+Dt:0;
                if(stuck>1.9f)yield return Shot("guidance-blocked-route-"+lesson.Step);
                if(stuck>=2)
                {
                    var feet=lesson.Player.transform.position;
                    var blockers=Physics.OverlapCapsule(feet+Vector3.up*.35f,feet+Vector3.up*1.45f,.7f,~0,QueryTriggerInteraction.Ignore)
                        .Where(c=>!c.transform.IsChildOf(lesson.Player.transform)).Select(c=>c.name+" at "+c.bounds.center);
                    Assert.Fail("The actual visible route must be walkable without a hidden detour; step="+lesson.Step+"; "+lesson.Controls.CarryObservation+"; at="+feet+"; to="+corner+"; nearby colliders="+string.Join(" | ",blockers));
                }
                yield return null;
            }
            Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),"Reach the destination shown to the user");
        }
        IEnumerator TalkVisible()
        {
            yield return FollowVisibleRoute();
            for(int i=0;i<3;i++){Look(lesson.VisibleAimPoint);lesson.Controls.StepInput(default,Dt);yield return null;}
            Assert.That(lesson.InteractAimed(),Is.True,"The displayed actor target must accept E from the displayed approach");yield return null;
        }
        DetailedPartBinding Binding(string owner,string part)=>lesson.GetComponentsInChildren<DetailedPartBinding>(true)
            .Single(b=>b.OwnerId==owner&&b.PartId==part);
        IEnumerator AimPart(string owner,string part)
        {
            var binding=Binding(owner,part);var collider=binding.GetComponentsInChildren<Collider>()
                .First(c=>c.enabled&&!c.isTrigger&&c.GetComponentInParent<DetailedPartBinding>()==binding);
            for(int i=0;i<3;i++){Look(collider.bounds.center);lesson.Controls.StepInput(default,Dt);yield return null;}
            Assert.That(lesson.Controls.AimedPart,Is.EqualTo(binding),"One aim at the visible part must reach "+owner+"/"+part+"; actual="+lesson.Controls.AimedPart?.PartId+"; "+lesson.Controls.Hint);
        }
        IEnumerator PickVisible(string owner)
        {
            for(int i=0;i<3;i++){Look(lesson.VisibleAimPoint);lesson.Controls.StepInput(default,Dt);yield return null;}
            if(owner=="extinguisher")yield return Shot("play-03-extinguisher-pickup");
            Assert.That(lesson.Controls.FocusedEquipmentId,Is.EqualTo(owner),"Visible equipment marker must permit F; "+lesson.Controls.Hint);
            yield return Input(new DetailedInputFrame{CarryPressed=true});yield return Hold(.5f);
            Assert.That(lesson.State.Entities["player"].Get("mech.carry."+owner),Is.EqualTo(1),"F alone picks "+owner);
        }
        IEnumerator Input(DetailedInputFrame frame)
        {lesson.Controls.StepInput(frame,Dt);yield return null;yield return null;yield return null;}
        IEnumerator Hold(float seconds,float scroll=0)
        {
            float until=Time.realtimeSinceStartup+seconds;
            while(Time.realtimeSinceStartup<until){lesson.Controls.StepInput(new DetailedInputFrame{ScrollDelta=scroll},Dt);yield return null;}
        }
        IEnumerator Shot(string name)
        {
            string folder=Environment.GetEnvironmentVariable("CG_FIRE_REVIEW");
            if(string.IsNullOrEmpty(folder)||SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)yield break;
            foreach(var size in new[]{new Vector2Int(1440,900),new Vector2Int(1280,720)})
            {
                var canvases=lesson.GetComponentsInChildren<Canvas>(true);
                var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();
                var distances=canvases.Select(c=>c.planeDistance).ToArray();
                var target=new RenderTexture(size.x,size.y,24,RenderTextureFormat.ARGB32);target.Create();
                var oldTarget=lesson.View.targetTexture;var oldActive=RenderTexture.active;float aspect=lesson.View.aspect;
                Texture2D pixels=null;
                try
                {
                    lesson.View.targetTexture=target;lesson.View.aspect=(float)size.x/size.y;
                    foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=lesson.View;canvas.planeDistance=.15f;}
                    yield return null;yield return null;Canvas.ForceUpdateCanvases();
                    foreach(var text in lesson.GetComponentsInChildren<TMPro.TMP_Text>())
                    {
                        if(!text.gameObject.activeInHierarchy||!text.isActiveAndEnabled||string.IsNullOrEmpty(text.text))continue;
                        if(!new[]{"현재 목표","이유와 다음 행동","단계별 조작","실제 선택과 반응","다음 행동","현재 조작","화자 자막","교육 안내","현재 단계 다시 보기"}.Contains(text.name))continue;
                        text.ForceMeshUpdate();
                        Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+1),name+" "+size+" overflowing guidance: "+text.name+"; "+text.text);
                    }
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(lesson.View,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    RenderTexture.active=target;pixels=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                    pixels.ReadPixels(new Rect(0,0,size.x,size.y),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(folder,name+"-"+size.x+"x"+size.y+".png"),pixels.EncodeToPNG());
                }
                finally
                {
                    for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];}
                    lesson.View.targetTexture=oldTarget;lesson.View.aspect=aspect;RenderTexture.active=oldActive;
                    target.Release();UnityEngine.Object.Destroy(target);if(pixels!=null)UnityEngine.Object.Destroy(pixels);
                }
            }
        }
        IEnumerator Radio(int group,int message)
        {
            lesson.StepRadioInput(true,true,Vector2.zero);yield return null;yield return Shot("guidance-radio-groups-"+lesson.Step);
            lesson.StepRadioInput(false,true,new Vector2(group==0?0:0,group==0?100:-100),true);yield return null;
            lesson.StepRadioInput(false,true,new Vector2(0,message==0?100:-100));yield return null;yield return Shot("guidance-radio-message-"+lesson.Step);
            lesson.StepRadioInput(false,false,Vector2.zero);yield return null;yield return null;yield return null;
        }
        IEnumerator Notches(int count)
        {
            for(int i=0;i<count;i++){yield return Input(new DetailedInputFrame{ScrollDelta=1});yield return Hold(.12f);}
        }
        IEnumerator BeginOriginalMapLesson()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.TitleScene);yield return null;
            var menu=UnityEngine.Object.FindFirstObjectByType<DepartureGameView>();
            Assert.That(menu,Is.Not.Null,"Current train-departure title ready");
            menu.ShowPractice();var cards=menu.GetComponentsInChildren<Button>();
            Assert.That(cards.Any(c=>c.GetComponentInChildren<TMPro.TMP_Text>()?.text=="심정지 환자 대응"),Is.True);
            cards.Single(c=>c.GetComponentInChildren<TMPro.TMP_Text>()?.text=="역무원 화재대응").onClick.Invoke();
            yield return WaitFor(()=>StationFireTutorialSession.Current!=null,300,"Actual fire menu entry");lesson=StationFireTutorialSession.Current;
            yield return WaitFor(()=>lesson.Ready||lesson.Failure.Length>0,600,"Original-map incident ready");
            Assert.That(lesson.Failure,Is.Empty);
            yield return Shot("guidance-ready-pause");
        }
        [UnityTest,Timeout(900000)]
        public IEnumerator ExistingMapIncidentUsesQRadioOriginalEquipmentAndRealPartsThroughHandover()
        {
            yield return BeginOriginalMapLesson();
            Assert.That(lesson.IncidentSourceName,Is.EqualTo("C165_S01_OBSERVED_CHAIR_1_0_1"));
            Assert.That(lesson.ExtinguisherSourceName,Is.EqualTo("C165_S01_P5_SOURCE_LEFT_ENTRY_PORTABLE_EXTINGUISHER"));
            Assert.That(Vector3.Distance(lesson.OriginalExtinguisherPosition,lesson.ExtinguisherBody.position),Is.LessThan(.001f),"Original placement unchanged before pickup");
            Assert.That(lesson.GetComponentsInChildren<DetailedPartBinding>().Any(b=>b.OwnerId=="radio"||b.OwnerId.StartsWith("barrier")),Is.False,"No worn radio or cordon equipment");
            lesson.SetVerificationInput(true);lesson.ResumeLesson();yield return Hold(1);yield return Shot("r5-01-original-map-incident");
            yield return TalkVisible();Assert.That(lesson.Step,Is.EqualTo(StationFireStep.Report));
            yield return Radio(0,0);yield return WaitFor(()=>lesson.ReportReceived,70,"Q report acknowledged");
            yield return WaitFor(()=>lesson.Step==StationFireStep.Extinguish,3,"Equipment available without passenger arrival gate");
            Assert.That(lesson.PassengersSafe,Is.False,"Player can prepare equipment while passengers move");
            yield return FollowVisibleRoute();yield return PickVisible("extinguisher");yield return Shot("r5-02-original-extinguisher-carried");
            Assert.That(lesson.ExtinguisherBody.position.y,Is.GreaterThan(lesson.OriginalExtinguisherPosition.y+.3f),"F must visibly lift the original body out of its holder, not only set carry state; "+lesson.Controls.CarryObservation);
            Look(lesson.Player.transform.position+lesson.Player.transform.forward*3+Vector3.up*1.6f);yield return Hold(.3f);
            var pinScreen=lesson.View.WorldToViewportPoint(Binding("extinguisher","pin").transform.position);
            Assert.That(pinScreen.z,Is.GreaterThan(0));Assert.That(pinScreen.x,Is.InRange(.05f,.95f));Assert.That(pinScreen.y,Is.InRange(.05f,.95f),"Held original pin should be visible at a normal forward view");
            yield return AimPart("extinguisher","lever");yield return Input(new DetailedInputFrame{GripPressed=true});yield return Hold(.2f);
            Assert.That(lesson.State.Entities["extinguisher"].Get("mech.flow"),Is.Zero,"Pin prevents discharge");yield return Input(new DetailedInputFrame{GripReleased=true});
            yield return AimPart("extinguisher","pin");var pin=Binding("extinguisher","pin").transform;var rest=pin.position;
            yield return Input(new DetailedInputFrame{GripPressed=true});yield return Notches(4);yield return Shot("guidance-pin-held-partial");
            double partial=lesson.Mechanics.Position("extinguisher","pin");Assert.That(partial,Is.InRange(.15,.65),"Realistic one-notch strokes move the actual original pin");
            lesson.SetPaused(true);yield return null;Assert.That(lesson.Controls.HeldPart,Is.Null,"Pause must release the actual grip");yield return Shot("guidance-pin-pause-reread");
            lesson.ResumeLesson();yield return Hold(.2f);
            Assert.That(lesson.Mechanics.Position("extinguisher","pin"),Is.EqualTo(partial).Within(.001),"Rereading instructions must preserve preparation");
            yield return Radio(0,0);Assert.That(lesson.Mechanics.Position("extinguisher","pin"),Is.EqualTo(partial).Within(.001),"Q menu preserves preparation");
            yield return AimPart("extinguisher","pin");yield return Input(new DetailedInputFrame{GripPressed=true});yield return Notches(10);yield return Shot("guidance-pin-ready-still-held");yield return Input(new DetailedInputFrame{GripReleased=true});
            Assert.That(lesson.Mechanics.Position("extinguisher","pin"),Is.GreaterThanOrEqualTo(.95));Assert.That(Vector3.Distance(rest,pin.position),Is.GreaterThan(.035f));
            Assert.That(lesson.Mechanics.Position("extinguisher","nozzle"),Is.Zero);
            yield return AimPart("extinguisher","nozzle");yield return Input(new DetailedInputFrame{GripPressed=true});yield return Notches(14);yield return Shot("guidance-nozzle-ready-still-held");yield return Input(new DetailedInputFrame{GripReleased=true});
            Assert.That(lesson.Mechanics.Position("extinguisher","nozzle"),Is.GreaterThanOrEqualTo(.95));yield return FollowVisibleRoute();
            yield return Shot("r5-03-original-pin-and-nozzle-prepared");
            yield return AimPart("extinguisher","lever");yield return Input(new DetailedInputFrame{GripPressed=true});
            double fire=lesson.State.Entities["fire"].Get("fire");
            for(int i=0;i<5;i++){Look(lesson.Player.transform.position+Vector3.up*2+lesson.Player.transform.right*3);lesson.Controls.StepInput(default,Dt);yield return null;}
            yield return Hold(.5f);Assert.That(lesson.State.Entities["fire"].Get("fire"),Is.EqualTo(fire),"Missed discharge cannot extinguish");
            var tip=Binding("extinguisher","nozzle").GetComponentsInChildren<Transform>().Single(t=>t.name=="NozzleExit");
            float until=Time.realtimeSinceStartup+12;bool captured=false;
            while(lesson.Step==StationFireStep.Extinguish&&Time.realtimeSinceStartup<until)
            {
                Look(lesson.FireTarget.position+lesson.View.transform.position-tip.position);lesson.Controls.StepInput(default,Dt);yield return null;
                if(!captured&&lesson.State.Entities["fire"].Get("fire")<fire)
                {
                    captured=true;yield return Hold(.4f);
                    foreach(var point in new[]{tip.position,Binding("extinguisher","nozzle").transform.position})
                    {
                        var screen=lesson.View.WorldToViewportPoint(point);
                        Assert.That(screen.z,Is.GreaterThan(0),"A real effective spray must leave the nozzle in front of the player");
                        Assert.That(screen.x,Is.InRange(.05f,.95f),"The actual ready nozzle must remain visible while aiming at the real fire; "+lesson.Controls.CarryObservation);
                        Assert.That(screen.y,Is.InRange(.05f,.95f),"The actual nozzle and outlet must be visible during effective discharge");
                    }
                    yield return Shot("r5-04-actual-suppression");
                }
            }
            Assert.That(lesson.Step,Is.EqualTo(StationFireStep.Release),lesson.SafetyMessage+"; "+lesson.Controls.Hint);
            double charge=lesson.State.Entities["extinguisher"].Get("mech.charge");lesson.SetPaused(true);yield return null;
            Assert.That(lesson.State.Entities["extinguisher"].Get("mech.flow"),Is.Zero);lesson.ResumeLesson();yield return Hold(.3f);
            Assert.That(lesson.State.Entities["extinguisher"].Get("mech.charge"),Is.EqualTo(charge));
            yield return WaitFor(()=>lesson.Step==StationFireStep.ResultReport,3,"Explicit outcome report before arrival");
            yield return Radio(0,1);Assert.That(lesson.ResultReported,Is.True);
            yield return WaitFor(()=>lesson.Specialist.gameObject.activeSelf&&lesson.Specialist.Arrived,100,"Fire service walks from explicit time transition");
            yield return Hold(.2f);yield return TalkVisible();yield return WaitFor(()=>lesson.Step==StationFireStep.Complete,70,"Observed situation transfer completed");
            Assert.That(lesson.SpecialistReceived,Is.True);Assert.That(lesson.PhysicalActionCount,Is.GreaterThanOrEqualTo(2));
            yield return Shot("r5-05-initial-response-complete");
        }
        [UnityTest,Timeout(900000)]
        public IEnumerator SafeWithdrawalTransfersRemainingFireWithoutInventingExtinguisherUse()
        {
            yield return BeginOriginalMapLesson();lesson.SetVerificationInput(true);lesson.ResumeLesson();yield return Hold(1);
            yield return TalkVisible();yield return Radio(0,0);yield return WaitFor(()=>lesson.ReportReceived,70,"Location report acknowledged");
            yield return Radio(1,1);
            Assert.That(lesson.WithdrawalRequested,Is.True);Assert.That(lesson.ResultReported,Is.True);
            Assert.That(lesson.State.Entities["extinguisher"].Get("mech.charge"),Is.EqualTo(1));
            Assert.That(lesson.State.Entities["fire"].Get("fire"),Is.GreaterThan(.01));
            yield return WaitFor(()=>lesson.Specialist.gameObject.activeSelf&&lesson.Specialist.Arrived,100,"Fire service approaches after explicit transition");
            yield return Radio(0,1);
            var spoken=lesson.GetComponentsInChildren<TMPro.TMP_Text>().Single(t=>t.name=="화자 자막").text;
            Assert.That(spoken,Does.Not.Contain("소진"),"A voluntary stop with a full extinguisher must not report empty agent");
            Assert.That(lesson.State.Entities["extinguisher"].Get("mech.charge"),Is.EqualTo(1));
            yield return TalkVisible();
            Assert.That(lesson.SpecialistReceived,Is.True,"E must reach the marked fire-service actor; "+lesson.Evidence.LastOrDefault(e=>e.Contains("actor-contact:")));
            yield return WaitFor(()=>lesson.Step==StationFireStep.Complete,70,"Remaining incident transferred; received="+lesson.SpecialistReceived+"; captured="+lesson.Captured+"; scale="+Time.timeScale);
            Assert.That(lesson.SpecialistReceived,Is.True);Assert.That(lesson.ResponseWithdrawn,Is.True);
            Assert.That(lesson.State.Entities["fire"].Get("fire"),Is.GreaterThan(.01),"Handover must not silently extinguish the incident");
            yield return Shot("r5-06-safe-withdrawal-transferred");
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            string folder=Environment.GetEnvironmentVariable("CG_FIRE_REVIEW");
            if(!string.IsNullOrEmpty(folder)&&lesson!=null)
            {
                File.WriteAllLines(Path.Combine(folder,"last-input-evidence.txt"),lesson.Evidence);
                if(lesson.Ready)File.WriteAllLines(Path.Combine(folder,"last-physical-poses.txt"),lesson.Passengers.Select(a=>a.ActorId+" position="+a.transform.position+" destination="+a.Destination+" goal="+a.HasGoal+" error="+a.Failure+" velocity="+a.Agent.velocity+" stopped="+a.Agent.isStopped+" remaining="+a.Agent.remainingDistance)
                    .Concat(new[]{"player="+lesson.Player.transform.position,"extinguisher="+lesson.ExtinguisherBody.position,"visibleAim="+lesson.VisibleAimPoint,"carry="+lesson.Controls.CarryObservation,"hint="+lesson.Controls.Hint}));
                if(lesson.State!=null)File.WriteAllText(Path.Combine(folder,"last-input-world.json"),ChooGuard.Seeded.WorldStateCodec.Serialize(lesson.State));
            }
            Time.timeScale=1;yield return SceneManager.LoadSceneAsync(SceneFlow.TitleScene);
        }
    }
}
