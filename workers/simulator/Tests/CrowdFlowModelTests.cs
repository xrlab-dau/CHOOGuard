using System;
using System.Linq;
using Newtonsoft.Json;

namespace ChooGuard.Seeded.Tests
{
    public static class CrowdFlowModelTests
    {
        static void Check(bool value,string reason){if(!value)throw new Exception("Crowd flow: "+reason);}
        static string Signature(CrowdFlowPlan plan)=>LocalTapeCodec.Encode((Newtonsoft.Json.Linq.JObject)LocalTapeCodec.FromObject(new {people=plan.Individuals})).ToString(Formatting.None);
        static World World(){var w=new World{Seed=123};w.Add(new Entity{Id="entry",Kind="place"});w.Add(new Entity{Id="exit",Kind="place",X=12});w.Add(new Entity{Id="train",Kind="train"});return w;}
        public static void Run()
        {
            var source=new CrowdArrivalSource{Id="station-entry",EntryEntityId="entry",DestinationEntityId="exit",Role="visitor",RateUnit="VISITOR_PERSONS_PER_HOUR",StartSeconds=-1200,EndSeconds=3600,ArrivalsPerHour=600,MinimumStaySeconds=600,MaximumStaySeconds=1800,MaximumPartySize=4};
            var wave=new CrowdTrainWave{Id="train-arrival",TrainEntityId="train",EntryEntityId="entry",DestinationEntityId="exit",AtSeconds=900,Capacity=500,OccupancyFraction=.7,MinimumStaySeconds=30,MaximumStaySeconds=600};
            var a=CrowdFlowPlan.Generate(123,new[]{source},new[]{wave},10000);var b=CrowdFlowPlan.Generate(123,new[]{source},new[]{wave},10000);var c=CrowdFlowPlan.Generate(124,new[]{source},new[]{wave},10000);
            Check(Signature(a)==Signature(b)&&Signature(a)!=Signature(c),"individual arrival/stay/party/train relations repeat for same input and differ across seeds");
            Check(a.Individuals.Select(p=>p.Id).Distinct().Count()==a.Individuals.Count&&a.Individuals.All(p=>p.IntendedStaySeconds>=0),"all individuals have persistent unique IDs, not representative samples");
            var extra=new CrowdArrivalSource{Id="a-unrelated-staff-flow",EntryEntityId="entry",DestinationEntityId="exit",Role="staff",RateUnit="STAFF_PERSONS_PER_HOUR",StartSeconds=0,EndSeconds=10,ArrivalsPerHour=1000,MinimumStaySeconds=1,MaximumStaySeconds=1};
            var extended=CrowdFlowPlan.Generate(123,new[]{extra,source},new[]{wave},10000);Check(a.Individuals.Select(p=>p.Id).OrderBy(x=>x).SequenceEqual(extended.Individuals.Where(p=>p.SourceId!=extra.Id).Select(p=>p.Id).OrderBy(x=>x)),"unrelated source cannot shift another flow's IDs or keyed draws");
            Check(Signature(extended)==Signature(CrowdFlowPlan.Generate(123,new[]{source,extra},new[]{wave},10000)),"input list order does not consume a shared RNG");
            var world=World();a.RegisterAll(world);Check(world.Entities.Count==3+a.Individuals.Count,"every individual is retained as world/model input including pending and distant population");
            Check(world.Entities.Values.Where(e=>e.Has("crowd-individual")).All(e=>!e.Has("npc")&&e.Get("crowd.physical-presence-observed")==0),"arrival intents do not fabricate physical presence/movement or hazard-bearing active NPCs");
            string before=world.Digest();var near=CrowdDetailSelection.Select(world,0,0,0,10,12);var far=CrowdDetailSelection.Select(world,100,0,100,10,12);Check(world.Digest()==before&&near.Count<=12&&far.Count==0,"detail-tier changes preserve all individual state, seed, goals and identity");
            Check((int)a.Census(0)["planned_presence_envelope"]<a.Individuals.Count,"cumulative flow and planned simultaneous-presence envelope are distinct denominators");
            var bad=World();bad.Entities.Remove("exit");string digest=bad.Digest();bool refused=false;try{a.RegisterAll(bad);}catch(ArgumentException){refused=true;}Check(refused&&bad.Digest()==digest,"missing source endpoint refuses whole population atomically");
            refused=false;try{CrowdFlowPlan.Generate(123,new[]{source},new[]{wave},1);}catch(ArgumentException){refused=true;}Check(refused,"resource envelope is a refusal, not hidden population truncation");
            var empty=CrowdFlowPlan.Generate(123,new[]{new CrowdArrivalSource{Id="quiet",RateUnit="VISITOR_PERSONS_PER_HOUR",EntryEntityId="entry",DestinationEntityId="exit",StartSeconds=0,EndSeconds=10,ArrivalsPerHour=0,MinimumStaySeconds=0,MaximumStaySeconds=0}},Array.Empty<CrowdTrainWave>(),1);Check(empty.Individuals.Count==0,"quiet flow allowed without mandatory crowd/emergency");
            PassengerRateConservation();
            Console.WriteLine("Crowd flow scheduled individual/party/train arrivals and stay intents PASS; daily ridership is not concurrent occupancy. Physical arrival/nav/render integration remains unverified.");
        }
        static void PassengerRateConservation()
        {
            // Predeclared compound-Poisson Chernoff bound: 512 seeds, 60 persons/h,
            // uniform 1..4 passenger parties, t=.01, two-sided alpha=.001.
            const int seeds=512;const double peopleRate=60,t=.01,alpha=.001;double expectedParty=2.5,partyLambda=peopleRate/expectedParty;
            double mgfPlus=Enumerable.Range(1,4).Average(k=>Math.Exp(t*k)),mgfMinus=Enumerable.Range(1,4).Average(k=>Math.Exp(-t*k));
            double tolerance=Math.Max((partyLambda*(mgfPlus-1)-t*peopleRate+Math.Log(2/alpha)/seeds)/t,(partyLambda*(mgfMinus-1)+t*peopleRate+Math.Log(2/alpha)/seeds)/t);
            var flow=new CrowdArrivalSource{Id="rail-person-rate",Role="passenger",RateUnit="PASSENGER_PERSON_EVENTS_PER_HOUR",EntryEntityId="entry",DestinationEntityId="exit",StartSeconds=0,EndSeconds=3600,ArrivalsPerHour=peopleRate,MaximumPartySize=4,MaximumNonPassengerCompanions=2,MinimumStaySeconds=0,MaximumStaySeconds=0};
            Check(CrowdFlowPlan.EffectivePartyArrivalsPerHour(flow)==24,"passenger persons rate divided by E[passenger party size], not multiplied by party membership");
            long passengers=0,companions=0;for(ulong seed=1;seed<=seeds;seed++){var plan=CrowdFlowPlan.Generate(seed,new[]{flow},Array.Empty<CrowdTrainWave>(),1000);passengers+=(int)plan.Census(0)["planned_rail_passenger_events"];companions+=(int)plan.Census(0)["non_passenger_companions"];}
            double observed=passengers/(double)seeds;Check(Math.Abs(observed-peopleRate)<=tolerance&&companions>0,"passenger event rate preserved; non-passenger companions excluded from official rail denominator");
            var staff=new CrowdArrivalSource{Id="staff",Role="staff",RateUnit="STAFF_PERSONS_PER_HOUR",EntryEntityId="entry",DestinationEntityId="exit",StartSeconds=0,EndSeconds=3600,ArrivalsPerHour=60,MinimumStaySeconds=0,MaximumStaySeconds=0};Check((int)CrowdFlowPlan.Generate(123,new[]{staff},Array.Empty<CrowdTrainWave>(),1000).Census(0)["planned_rail_passenger_events"]==0,"staff never included in rail passenger count");
            Console.WriteLine("Crowd persons-rate compound-Poisson fixture seeds="+seeds+" observed="+observed+" expected="+peopleRate+" Chernoff tolerance="+tolerance+" alpha="+alpha+"; companions/staff are separate, uncalibrated assumptions.");
        }
    }
}
