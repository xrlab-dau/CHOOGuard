#if UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Tests.EditMode
{
    public sealed class CurrentStationNavigationContractTests
    {
        [TestCase(true,false,false,"UNBOUND_PROVEN_NO_CURRENT_FLOOR")]
        [TestCase(false,true,true,"BOUND_MODEL_COMPUTED_WALKABLE")]
        public void IndependentSourceAndNavProofsHaveExplicitOutcomes(bool absent,bool physical,bool nav,string expected)
        { Assert.That(Decision(absent,physical,nav),Is.EqualTo(expected)); }

        [TestCase(false,false,false)]
        [TestCase(false,false,true)]
        [TestCase(false,true,false)]
        [TestCase(true,true,false)]
        public void UnknownSupportMissingNavigationAndContradictoryAbsenceNeverDisappear(bool absent,bool physical,bool nav)
        {
            var failure=Assert.Throws<TargetInvocationException>(()=>Decision(absent,physical,nav));
            Assert.That(failure.InnerException,Is.TypeOf<InvalidOperationException>());
        }

        static string Decision(bool absent,bool physical,bool nav)
        {
            var type=typeof(StationWorld).Assembly.GetType("ChooGuard.App.Fps.Emergency.CurrentStationNavigation",true);
            return (string)type.GetMethod("BindingDecision",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{absent,physical,nav});
        }

        [Test]
        public void OnlyActualIndexedGeometryCanSupplyANavigationSurface()
        {
            Assert.That(StationNavigationRecipe.HasSurface(0,new uint[]{0}),Is.False);
            Assert.That(StationNavigationRecipe.HasSurface(3,new uint[]{0}),Is.False);
            Assert.That(StationNavigationRecipe.HasSurface(3,new uint[]{3}),Is.True);
            Assert.Throws<InvalidOperationException>(()=>StationNavigationRecipe.HasSurface(0,new uint[]{3}));
        }

        [Test]
        public void UnavailableSourceHasNoPathLinkAndAvailabilityNeverDuplicatesOrReopensAClosure()
        {
            var origin=new Vector3(12000,0,12000);
            var sources=new System.Collections.Generic.List<NavMeshBuildSource>{
                new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(4,.2f,4),
                    transform=Matrix4x4.TRS(origin+new Vector3(-3,-.1f,0),Quaternion.identity,Vector3.one)},
                new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(4,.2f,4),
                    transform=Matrix4x4.TRS(origin+new Vector3(3,-.1f,0),Quaternion.identity,Vector3.one)}};
            var settings=StationNavigationRecipe.Settings();settings.minRegionArea=0;
            var data=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(origin,new Vector3(12,4,8)),Vector3.zero,Quaternion.identity);
            Assert.That(data,Is.Not.Null);
            var instance=NavMesh.AddNavMeshData(data);var owner=new GameObject("Isolated supplied-source link fixture");
            try
            {
                Assert.That(instance.valid,Is.True);
                var left=origin+Vector3.left*3;var right=origin+Vector3.right*3;
                Assert.That(NavMesh.SamplePosition(left,out var a,.5f,NavMesh.AllAreas),Is.True);
                Assert.That(NavMesh.SamplePosition(right,out var b,.5f,NavMesh.AllAreas),Is.True);
                var entry=new StationPoints.EscalatorEntry{id="isolated-source",label="Source fixture",path=new[]{a.position,b.position}};
                var lane=owner.AddComponent<Escalator>();lane.Setup(entry,false);
                var path=new NavMeshPath();
                bool Complete()=>NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                Assert.That(lane.HasNavigationLink,Is.False);Assert.That(Complete(),Is.False);
                lane.SetNavigationAvailable(true);
                Assert.That(lane.HasNavigationLink,Is.True);Assert.That(Complete(),Is.True);
                var field=typeof(Escalator).GetField("link",BindingFlags.Instance|BindingFlags.NonPublic);
                var first=field.GetValue(lane);lane.SetNavigationAvailable(true);
                Assert.That(field.GetValue(lane),Is.EqualTo(first),"Availability rebind must retain its one owned instance");
                lane.SetNavigationAvailable(false);Assert.That(lane.HasNavigationLink,Is.False);Assert.That(Complete(),Is.False);
                lane.SetNavigationAvailable(true);Assert.That(Complete(),Is.True);
                lane.Stop();Assert.That(lane.HasNavigationLink,Is.True,"Stopped supplied steps retain the existing walking policy");
                lane.SetEntryBarred(true);lane.SetNavigationAvailable(false);lane.SetNavigationAvailable(true);
                Assert.That(lane.HasNavigationLink,Is.False);lane.SetEntryBarred(false);Assert.That(lane.HasNavigationLink,Is.True);
                lane.Close();lane.SetNavigationAvailable(true);Assert.That(lane.HasNavigationLink,Is.False);
            }
            finally
            { UnityEngine.Object.DestroyImmediate(owner);if(instance.valid)instance.Remove();UnityEngine.Object.DestroyImmediate(data); }
        }
    }
}
#endif
