using System;
using System.Linq;
using NUnit.Framework;
namespace ChooGuard.Foundation.Demo.Tests
{
    public sealed class StationWorldSessionTests
    {
        private static StationWorldConfig Config() { return StationWorldConfig.TestProfile(); }
        private static IncidentCandidate[] Candidates() { return new[]{new IncidentCandidate("central",new[]{"npc-0","npc-1"}),new IncidentCandidate("west",new[]{"npc-2","npc-3"}),new IncidentCandidate("east",new[]{"npc-4","npc-5"})}; }
        [Test]
        public void SeededOnsetIsIndependentOfFrameSizeAndDoesNotRevealTheIncident()
        {
            var a=new StationWorldSession(Config(),1729);var b=new StationWorldSession(Config(),1729);
            a.Tick(20,Candidates());for(var i=0;i<200;i++)b.Tick(.1,Candidates());
            Assert.That(a.Active,Is.Not.Null);Assert.That(b.Active.Id,Is.EqualTo(a.Active.Id));
            Assert.That(b.Active.SiteId,Is.EqualTo(a.Active.SiteId));Assert.That(b.Active.Kind,Is.EqualTo(a.Active.Kind));
            Assert.That(b.Active.StartedAt,Is.EqualTo(a.Active.StartedAt).Within(.0001));Assert.That(a.Active.Discovered,Is.False);
            Assert.That(a.Trace.Single(x=>x.action=="onset").seconds,Is.EqualTo(b.Trace.Single(x=>x.action=="onset").seconds));
        }
        [Test]
        public void OccupiedOrUnknownSitesNeverStartAndInvalidDeltaCannotChangeTheClock()
        {
            var s=new StationWorldSession(Config(),4);s.Tick(30,new IncidentCandidate[0]);Assert.That(s.Active,Is.Null);
            var before=s.Seconds;Assert.Throws<ArgumentException>(()=>s.Tick(double.NaN,Candidates()));Assert.That(s.Seconds,Is.EqualTo(before));
            Assert.Throws<ArgumentException>(()=>s.Tick(1,new[]{new IncidentCandidate("foreign",new[]{"npc-0"})}));
        }
        [TestCase(1)] [TestCase(2)] [TestCase(11)] [TestCase(1729)]
        public void ResponseHasCausalPrerequisitesAcceptsIndependentOrderAndReturnsToNormal(int seed)
        {
            var s=new StationWorldSession(Config(),seed);s.Tick(20,Candidates());var id=s.Active.Id;
            Assert.That(s.Act(id,s.Revision,StationAction.Report,s.Active.ReportAnchor).Accepted,Is.False);
            Assert.That(s.Act(id,s.Revision,StationAction.Observe,"anchor-01").Accepted,Is.True);
            Assert.That(s.Act(id,s.Revision,StationAction.SelectRoute,s.Active.SiteId).Accepted,Is.False);
            var route=Config().sites.First(x=>x.id!=s.Active.SiteId).id;
            // Route selection and reporting are independent after observation.
            Assert.That(s.Act(id,s.Revision,StationAction.SelectRoute,route).Accepted,Is.True);
            Assert.That(s.Act(id,s.Revision,StationAction.Recruit,s.Active.AffectedNpcIds[0]).Accepted,Is.False);
            Assert.That(s.Act(id,s.Revision,StationAction.Report,s.Active.ReportAnchor).Accepted,Is.True);
            if(s.Active.RequiresNotice)Assert.That(s.Act(id,s.Revision,StationAction.Notify,"anchor-02").Accepted,Is.True);
            foreach(var npc in s.Active.AffectedNpcIds)
            {
                Assert.That(s.Act(id,s.Revision,StationAction.Recruit,npc).Accepted,Is.True);
                Assert.That(s.Act(id,s.Revision,StationAction.RecordArrival,npc).Accepted,Is.True);
            }
            Assert.That(s.Act(id,s.Revision,StationAction.CloseIncident,"assembly-register").Accepted,Is.True);
            Assert.That(s.Phase,Is.EqualTo(StationPhase.Recovery));s.Tick(3,Candidates());
            Assert.That(s.Phase,Is.EqualTo(StationPhase.Ordinary));Assert.That(s.History.Count,Is.EqualTo(1));
            s.Tick(20,Candidates());Assert.That(s.Active.Id,Is.Not.EqualTo(id));
            Assert.That(s.Act(id,s.Revision,StationAction.Observe,"anchor-01").Accepted,Is.False);
        }
        [Test]
        public void PausingFreezesTimeAndActionsWhileStaleRevisionAndDuplicateNpcCannotAdvance()
        {
            var s=new StationWorldSession(Config(),7);s.Tick(20,Candidates());var id=s.Active.Id;var rev=s.Revision;
            s.Paused=true;var t=s.Seconds;s.Tick(100,Candidates());Assert.That(s.Seconds,Is.EqualTo(t));
            Assert.That(s.Act(id,rev,StationAction.Observe,"anchor-01").Accepted,Is.False);
            s.Paused=false;Assert.That(s.Act(id,rev,StationAction.Observe,"anchor-01").Accepted,Is.True);
            Assert.That(s.Act(id,rev,StationAction.Report,s.Active.ReportAnchor).Accepted,Is.False);
            Assert.That(s.Act(id,s.Revision,StationAction.RecordArrival,"npc-0").Accepted,Is.False);
        }
        [Test]
        public void EscalationIsRecordedWithoutInventingPassFailOrChangingAffectedPeople()
        {
            var s=new StationWorldSession(Config(),8);s.Tick(20,Candidates());var affected=s.Active.AffectedNpcIds.ToArray();
            s.Tick(100,Candidates());Assert.That(s.Active.Escalated,Is.True);Assert.That(s.Active.AffectedNpcIds,Is.EqualTo(affected));
            Assert.That(s.Trace.Any(x=>x.action=="escalated"),Is.True);
        }
        [Test]
        public void SyntheticGeometryCannotBeMarkedVerifiedWithoutFacilityAndManualEvidence()
        {
            var config=Config();config.evidenceStatus="verified";
            Assert.Throws<ArgumentException>(()=>new StationWorldSession(config,1));
        }
    }
}
