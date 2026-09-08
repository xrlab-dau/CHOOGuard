using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ChooGuard.Foundation.Demo.Tests
{
    public sealed class DemoEvacuationTests
    {
        [Test]
        public void FloorGapIsUnreachableAndDuplicateCrowdCannotMasqueradeAsSixPeople()
        {
            var root=new GameObject("Gap fixture");
            foreach(var x in new[]{-3f,3f})
            {
                var f=GameObject.CreatePrimitive(PrimitiveType.Cube);f.transform.SetParent(root.transform);f.transform.position=new Vector3(x,-.15f,0);f.transform.localScale=new Vector3(3,.3f,5);
            }
            var graph=root.AddComponent<DemoWalkGraph>();graph.Rebuild();
            Assert.That(graph.Clear(new Vector3(-3,.1f,0),new Vector3(3,.1f,0)),Is.False);
            Assert.That(graph.Route(new Vector3(-3,.1f,0),new Vector3(3,.1f,0)),Is.Empty);
            var npc=new GameObject("NPC").AddComponent<DemoEvacuee>();npc.transform.SetParent(root.transform);npc.Configure("west",0,new Vector3(-3,.1f,0),new Vector3(3,.1f,0));
            var leader=new GameObject("Leader").transform;leader.SetParent(root.transform);leader.position=new Vector3(3,.1f,0);npc.Recruit();
            for(var i=0;i<100;i++)npc.Tick(.1f,leader,graph,leader.position);
            Assert.That(npc.transform.position.x,Is.EqualTo(-3).Within(.01f));Assert.That(npc.State,Is.EqualTo(EvacueeState.FollowPlayer));
            Assert.Throws<System.InvalidOperationException>(()=>DemoGameController.ValidateCrowd(new[]{npc,npc,npc,npc,npc,npc},leader.position));
            Object.DestroyImmediate(root);
        }

        [UnityTest]
        public IEnumerator FollowerRoutesAroundWallAndWaitsOnlyAfterRecruitmentAndArrival()
        {
            var root=new GameObject("Evacuation route fixture");
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(0,-.15f,0);floor.transform.localScale=new Vector3(12,.3f,12);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform);wall.transform.position=new Vector3(0,1.5f,0);wall.transform.localScale=new Vector3(.3f,3,5);
            var lead=new GameObject("Leader").transform;lead.SetParent(root.transform);lead.position=new Vector3(3,.1f,0);
            var graph=root.AddComponent<DemoWalkGraph>();graph.Rebuild();
            var npc=new GameObject("Identical follower").AddComponent<DemoEvacuee>();npc.transform.SetParent(root.transform);
            var origin=new Vector3(-3,.1f,0);npc.Configure("west",0,origin,new Vector3(3,.1f,0));
            Assert.That(graph.Route(origin,lead.position).Length,Is.GreaterThan(2),"Wall forces a corner route.");
            Assert.That(npc.State,Is.EqualTo(EvacueeState.Wander));
            npc.Tick(0,lead,graph,lead.position);Assert.That(npc.transform.position,Is.EqualTo(origin));
            Assert.That(npc.Recruit(),Is.True);Assert.That(npc.Recruit(),Is.False);
            for(var i=0;i<900&&npc.State!=EvacueeState.AssemblyWait;i++)
            {
                npc.Tick(.05f,lead,graph,lead.position);
                Assert.That(Physics.CheckCapsule(npc.transform.position+Vector3.up*.32f,npc.transform.position+Vector3.up*1.38f,.28f,1),Is.False,"NPC must not pass through wall.");
            }
            Assert.That(npc.State,Is.EqualTo(EvacueeState.AssemblyWait));
            Assert.That(DemoEvacuee.Horizontal(npc.transform.position,lead.position),Is.LessThan(.4f));
            var arrived=npc.transform.position;lead.position=origin;npc.Tick(1,lead,graph,Vector3.right*3);
            Assert.That(npc.transform.position,Is.EqualTo(arrived));
            npc.ResetActor();Assert.That(npc.State,Is.EqualTo(EvacueeState.Wander));Assert.That(npc.transform.position,Is.EqualTo(origin));
            Object.DestroyImmediate(root);yield return null;
        }
    }
}
