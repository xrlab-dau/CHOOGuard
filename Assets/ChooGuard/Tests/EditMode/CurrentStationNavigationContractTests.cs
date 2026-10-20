#if UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
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

        static object Call(StationPoints points,string method,params object[] args)=>typeof(StationPoints)
            .GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(points,args);
        static T Set<T>(T value,string property,object data)
        {value.GetType().GetProperty(property,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(value,data);return value;}
        static StationPoints PhysicalCatalog(out Dictionary<string,string> admitted,out Dictionary<string,string> pending,
            out List<StationPoints.CurrentRequiredEndpoint> required)
        {
            var asset=new TextAsset(System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath,"ChooGuard/Art/Emergency/station-points.json")));
            StationPoints source;try{source=StationPoints.Load(asset);}finally{UnityEngine.Object.DestroyImmediate(asset);}
            Call(source,"RequireCurrentModelOrigin");
            var bindings=source.All.Select(point=>
            {
                var runtime=new StationPoints.Point{Id=point.Id,Kind=point.Kind,Label=point.Label,Zone=point.Zone,
                    Position=point.Position,Anchor=point.Anchor,Yaw=point.Yaw,Slot=point.Slot};
                var row=Set(new StationPoints.CurrentBinding(),"Source",point);Set(row,"Runtime",runtime);
                Set(row,"Status","BOUND_MODEL_COMPUTED_WALKABLE");Set(row,"Evidence","Controlled metadata fixture; no physical/Scene proof");return row;
            }).ToArray();
            var physical=(StationPoints)Call(source,"WithCurrentBindings",(object)bindings);
            admitted=physical.All.ToDictionary(point=>point.Id,point=>"CERTIFIED_CURRENT_MODEL_FOOT_DESTINATION; controlled software fixture");
            pending=new Dictionary<string,string>();
            required=physical.Of(PointKind.Exit).Select(exit=>
            {
                var row=Set(new StationPoints.CurrentRequiredEndpoint(),"Label",exit.Id);Set(row,"Owner","current-exit:"+exit.Id);
                Set(row,"SourcePosition",exit.Position);Set(row,"GraphPosition",exit.Position);Set(row,"Evidence","Controlled endpoint fixture");return row;
            }).ToList();
            return physical;
        }

        [Test]
        public void PendingIsAnOrthogonalCompleteAuditAndNeverAnOperationalPoint()
        {
            var physical=PhysicalCatalog(out var admitted,out var pending,out var required);
            var row=physical.CurrentBindings.First(binding=>binding.Source.Kind==PointKind.Chair);var before=row.Runtime;
            admitted.Remove(row.Source.Id);pending.Add(row.Source.Id,"PENDING_HISTORICAL_MODEL_CORRESPONDENCE; controlled expected nonadmission");
            var ledger=(IReadOnlyList<StationPoints.CurrentDestination>)Call(physical,"CaptureCurrentDestinations",admitted,pending);
            var active=(StationPoints)Call(physical,"WithCurrentDestinations",ledger,required);
            Assert.That(ledger.Count,Is.EqualTo(1449));Assert.That(active.SourceAll,Is.SameAs(physical.SourceAll));
            Assert.That(row.Bound,Is.True);Assert.That(row.Runtime,Is.SameAs(before));
            Assert.That(active.CurrentBindings,Is.SameAs(physical.CurrentBindings));
            Assert.That(active.All.Contains(before),Is.False);Assert.That(active.All.Count+1,Is.EqualTo(physical.All.Count));
            Assert.Throws<InvalidOperationException>(()=>active.RequireActiveDestination(before));
            Assert.Throws<InvalidOperationException>(()=>active.RequireActiveDestination(new StationPoints.Point{Id=active.All[0].Id}));
            Assert.DoesNotThrow(()=>active.RequireActiveDestination(active.All[0]));Assert.DoesNotThrow(()=>active.RequireActiveDestination(null));
            foreach(var kind in physical.All.Select(point=>point.Kind).Distinct())Assert.That(active.Of(kind),Is.Not.Empty);
        }

        [TestCase("clone")][TestCase("anchor")][TestCase("stale-anchor")][TestCase("origin")][TestCase("kind-collapse")][TestCase("required-exit")]
        public void StaleUnknownAndVacuousAdmissionFailsBeforePublishingAnyView(string mutation)
        {
            var physical=PhysicalCatalog(out var admitted,out var pending,out var required);
            var row=physical.CurrentBindings.First(binding=>binding.Source.Kind==PointKind.Chair);
            var before=row.Runtime;var oldPosition=before.Position;var oldEvidence=row.Evidence;
            if(mutation=="anchor")before.Anchor+=Vector3.right;
            if(mutation=="kind-collapse")foreach(var point in physical.Of(PointKind.Chair))
            {admitted.Remove(point.Id);pending.Add(point.Id,"PENDING_HISTORICAL_MODEL_CORRESPONDENCE; controlled expected nonadmission");}
            var ledger=(IReadOnlyList<StationPoints.CurrentDestination>)Call(physical,"CaptureCurrentDestinations",admitted,pending);
            if(mutation=="clone")Set(row,"Runtime",new StationPoints.Point{Id=before.Id,Kind=before.Kind,Label=before.Label,Zone=before.Zone,
                Position=before.Position,Anchor=before.Anchor,Yaw=before.Yaw,Slot=before.Slot});
            if(mutation=="stale-anchor")before.Anchor+=Vector3.right;
            if(mutation=="origin")Set(physical,"SourceSha256","unknown");
            if(mutation=="required-exit")required.Clear();
            var failure=Assert.Throws<TargetInvocationException>(()=>Call(physical,"WithCurrentDestinations",ledger,required));
            Assert.That(failure.InnerException,Is.TypeOf<InvalidOperationException>());
            Assert.That(before.Position,Is.EqualTo(oldPosition));Assert.That(row.Evidence,Is.EqualTo(oldEvidence));
            Assert.That(physical.CurrentDestinations,Is.Empty,"Failed validation cannot publish a partial admission ledger");
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
