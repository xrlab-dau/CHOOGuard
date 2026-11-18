using System;
using ChooGuard.Seeded;
using NUnit.Framework;
using ChooGuard.Seeded.Unity;
using UnityEngine;

namespace ChooGuard.Tests.StationFireTutorial
{
    public sealed class StationFireMechanicalTests
    {
        World world; MechanicalInteractions mechanics; long sequence;
        [SetUp] public void Setup()
        {
            world=new World{Seed=318,StepSeconds=.05};
            world.Add(new Entity{Id="player",Kind="staff"});
            var extinguisher=new Entity{Id="ext",Kind="extinguisher"};extinguisher.Set("charge",1);world.Add(extinguisher);
            mechanics=new MechanicalInteractions(world);mechanics.Attach("ext","extinguisher");sequence=0;
        }
        void Input(string part,string action,double analog=0,double duration=0)
        {
            mechanics.Tick(new[]{new MechanicalInput{Tick=world.Tick,Sequence=++sequence,ActorId="player",OwnerId="ext",PartId=part,
                Action=action,Contact=true,Clearance=1,Analog=analog,Duration=duration}});world.Tick++;
        }
        void EmptyTick(){mechanics.Tick(Array.Empty<MechanicalInput>());world.Tick++;}
        void PreparePart(string part)
        {
            Input(part,"grip");
            for(int i=0;i<35;i++)Input(part,part=="pin"?"drag":"rotate",1,.05);
            Input(part,"release");
            Assert.That(mechanics.Position("ext",part),Is.GreaterThanOrEqualTo(.95));
        }
        [Test] public void ReleasingLeverImmediatelyStopsFlowAndDoesNotConsumeResidualSpringCharge()
        {
            PreparePart("pin");Input("lever","grip");for(int i=0;i<8;i++)Input("lever","press",1,.05);
            Assert.That(world.Entities["ext"].Get("mech.flow"),Is.GreaterThan(0));
            double before=world.Entities["ext"].Get("mech.charge");Input("lever","release");
            Assert.That(world.Entities["ext"].Get("mech.flow"),Is.Zero);
            for(int i=0;i<8;i++)EmptyTick();
            Assert.That(world.Entities["ext"].Get("mech.charge"),Is.EqualTo(before));
        }
        [Test] public void SupervisedDischargeCannotSpendAgentBeforeHandoffOrNozzlePreparation()
        {
            PreparePart("pin");var ext=world.Entities["ext"];ext.Set("mech.discharge-enabled",0);ext.Set("mech.require-nozzle-ready",1);
            Input("lever","grip");Input("lever","press",1,.05);
            Assert.That(ext.Get("mech.flow"),Is.Zero);Assert.That(ext.Get("mech.charge"),Is.EqualTo(1));
            ext.Set("mech.discharge-enabled",1);Input("lever","press",1,.05);
            Assert.That(ext.Get("mech.flow"),Is.Zero);Assert.That(ext.Get("mech.charge"),Is.EqualTo(1));
            Input("lever","release");PreparePart("nozzle");Input("lever","grip");Input("lever","press",1,.05);
            Assert.That(ext.Get("mech.flow"),Is.GreaterThan(0));Assert.That(ext.Get("mech.charge"),Is.LessThan(1));
        }
        [Test] public void CaptureLossReleasesGripWithoutReinsertingPinOrRefillingCharge()
        {
            PreparePart("pin");Input("lever","grip");Input("lever","press",1,.05);
            double before=world.Entities["ext"].Get("mech.charge");mechanics.ReleaseActorGrips("player");EmptyTick();
            Assert.That(world.Entities["ext"].Get("mech.flow"),Is.Zero);
            Assert.That(world.Entities["ext"].Get("mech.charge"),Is.EqualTo(before));
            Assert.That(mechanics.Position("ext","pin"),Is.GreaterThanOrEqualTo(.95));
        }
        sealed class Host : IDetailedManipulationHost
        {
            public World State {get;set;}
            public Camera View=>null;public Transform Actor=>null;public Transform Root {get;set;}
            public bool Running=>true;public bool Captured=>false;public bool IsReplaying=>true;
            public StationWorldAdapter WorldSnapshot=>null;
            public string ResolveResponseSubject(string id)=>id;
        }
        [Test] public void InputBufferResetPreservesRecordedInitialDigestAndNewTapeReplaysExactly()
        {
            var root=new GameObject("replay buffer regression");
            try
            {
                var initial=WorldStateCodec.Clone(world);var digest=world.Digest();
                var controls=root.AddComponent<DetailedManipulationController>();
                controls.Initialize(new Host{State=world,Root=root.transform},mechanics,false);
                controls.ResetInputState();
                Assert.That(world.Digest(),Is.EqualTo(digest),"Restoring a tape may clear buffers but cannot project extra state fields");
                PreparePart("pin");Input("lever","grip");Input("lever","press",1,.05);Input("lever","release");EmptyTick();
                var replay=new MechanicalInteractions(initial);
                foreach(var record in mechanics.Records){replay.Replay(record);initial.Tick++;}
                Assert.That(initial.Digest(),Is.EqualTo(world.Digest()));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void ExistingResourceCatalogRemainsReadableWithNewReplayBehavior()
        {
            var resource=Resources.Load<TextAsset>("MechanicalProfiles");Assert.That(resource,Is.Not.Null);
            var restored=WorldStateCodec.Clone(world);
            var loaded=new MechanicalInteractions(restored,resource.text);
            Assert.That(loaded.CatalogHash,Is.EqualTo(mechanics.CatalogHash));
        }
    }
}
