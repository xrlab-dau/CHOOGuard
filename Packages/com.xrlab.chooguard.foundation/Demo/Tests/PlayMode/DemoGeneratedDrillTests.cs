#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace ChooGuard.Foundation.Demo.Tests
{
    public sealed class DemoGeneratedDrillTests
    {
        private Scene loaded;
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(loaded.IsValid()&&loaded.isLoaded)yield return SceneManager.UnloadSceneAsync(loaded);
        }
        [UnityTest]
        public IEnumerator ThreeDrillsAcrossFiveRolesRequireOrderedObjectsAndSixWalkingFollowers()
        {
            loaded=EditorSceneManager.LoadSceneInPlayMode("Assets/CHOOguardGenerated/FoundationDemo/FoundationDemo.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var root=loaded.GetRootGameObjects().Single();var game=root.GetComponentInChildren<DemoGameController>();
            var world=root.GetComponentInChildren<StationWorldController>();if(world!=null)world.EnableLegacyRegression();
            Assert.That(game.IsConfigured,Is.True);game.enabled=false;
            var player=game.Player;var graph=root.GetComponentInChildren<DemoWalkGraph>();var character=player.GetComponent<CharacterController>();
            for(var drill=0;drill<3;drill++)for(var role=1;role<=5;role++)
            {
                game.RestartDemo();game.SelectDrill(drill);game.SelectRole("role-"+role.ToString("00"));game.BeginIncident();player.SetControlEnabled(false);
                Assert.That(game.TryCompleteAssembly(),Is.False);
                var stepCount=game.Exercise.Steps.Count;
                for(var step=0;step<stepCount;step++)
                {
                    var target=game.CurrentTarget;Assert.That(target,Is.Not.Null);
                    var approach=target.transform.position-target.transform.forward*1.3f;approach.y=.1f;
                    var route=graph.Route(player.transform.position,approach).Concat(new[]{approach}).ToArray();
                    Assert.That(route.Length,Is.GreaterThan(1),target.name);
                    foreach(var point in route)
                    {
                        var ticks=0;
                        while(DemoEvacuee.Horizontal(player.transform.position,point)>.04f && ticks++<300)
                        {
                            var delta=point-player.transform.position;delta.y=0;character.Move(Vector3.ClampMagnitude(delta,.13f));game.TickExercise(.05f);
                        }
                        Assert.That(ticks,Is.LessThan(300),"Player route blocked: "+target.name+" at "+point);
                    }
                    player.ViewCamera.transform.rotation=Quaternion.LookRotation(target.InteractionPoint-player.ViewCamera.transform.position);
                    Physics.SyncTransforms();
                    if(target.AnchorId=="assembly-register")
                    {
                        if(!game.AllEvacueesArrived)Assert.That(game.TryInteract(),Is.False,"Registration cannot bypass missing followers.");
                        var wait=0;
                        while(!game.AllEvacueesArrived && wait++<1600)game.TickExercise(.05f);
                        Assert.That(game.AllEvacueesArrived,Is.True,"Missing NPC after walking drill "+drill+" role "+role+": "+string.Join(",",game.Evacuees.Select(x=>x.name+"="+x.State+"@"+x.transform.position+" route="+string.Join("/",graph.Route(x.transform.position,x.WaitingSlot).Select(v=>v.ToString()))+" overlap="+string.Join("/",Physics.OverlapCapsule(x.transform.position+Vector3.up*.32f,x.transform.position+Vector3.up*1.38f,.30f,1,QueryTriggerInteraction.Ignore).Select(c=>c.name)))));
                    }
                    Assert.That(game.TryInteract(),Is.True,"Current visible target: "+target.name+" drill "+drill+" role "+role);
                    Assert.That(game.TryInteract(),Is.False,"Repeated device input cannot consume next step.");
                    if(target.AnchorId.StartsWith("rally-"))
                    {
                        var positions=game.Evacuees.Select(x=>x.transform.position).ToArray();
                        var states=game.Evacuees.Select(x=>x.State).ToArray();
                        var destination=root.GetComponentInChildren<DemoWorldGuide>().DestinationId;
                        game.PauseDemo();for(var p=0;p<20;p++)game.TickExercise(.2f);
                        Assert.That(game.Evacuees.Select(x=>x.transform.position),Is.EqualTo(positions));
                        Assert.That(game.Evacuees.Select(x=>x.State),Is.EqualTo(states));
                        Assert.That(root.GetComponentInChildren<DemoWorldGuide>().DestinationId,Is.EqualTo(destination));
                        Assert.That(game.TryInteract(),Is.False);Assert.That(game.TryCompleteAssembly(),Is.False);
                        game.ResumeDemo();player.SetControlEnabled(false);Assert.That(game.IsPaused,Is.False);
                    }
                    Assert.That(game.Exercise.Index,Is.EqualTo(step+1));
                    if(step<stepCount-1)Assert.That(game.TryCompleteAssembly(),Is.False);
                }
                Assert.That(game.Flow.LastAction.Accepted,Is.True);Assert.That(game.Flow.LastAction.VirtualTeamEvents.Count,Is.EqualTo(4));
                Assert.That(game.TryCompleteAssembly(),Is.True);Assert.That(game.Flow.Phase,Is.EqualTo(DemoPhase.Results));
                game.RestartDemo();Assert.That(game.Evacuees.All(x=>x.State==EvacueeState.Wander),Is.True);
                yield return null;
            }
        }
    }
}
#endif
