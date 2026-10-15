using System;
using Newtonsoft.Json;
using ChooGuard.Seeded;

namespace ChooGuard.Seeded.Tests
{
    public static class CommunityScalingTests
    {
        static void Check(bool value,string reason){if(!value)throw new Exception("Community indexed equivalence: "+reason);}
        static World Fixture()
        {
            var w=new World{Seed=310};w.Fields["environment.autonomous"]=0;
            for(int i=0;i<48;i++){var p=new Entity{Id="npc:"+i,Kind="person",X=i%8,Z=i/8};p.Tags.Add("npc");p.Set("health",1);p.Set("mobility",1);p.Set("sight.radius",12);if(i%2==0)p.Links.Add("exit");if(i%5==0){p.Set("goal.active",1);p.Set("goal.x",p.X);p.Set("goal.z",p.Z);p.Set("goal.target.place",1);p.Set("goal.target.absent",1);}w.Add(p);}
            var exit=new Entity{Id="exit",Kind="place",X=15};exit.Tags.Add("exit");w.Add(exit);
            var place=new Entity{Id="place",Kind="place",X=10};place.Tags.Add("place");w.Add(place);
            var seat=new Entity{Id="seat",Kind="seat",X=2};seat.Tags.Add("seat");seat.Set("occupied",1);seat.Set("occupant.npc:1",1);w.Add(seat);w.Entities["npc:1"].Set("seated",1);w.Entities["npc:1"].Set("seat.target.seat",1);
            var danger=new Entity{Id="danger",Kind="luggage",X=1};danger.Set("fire",.6);w.Add(danger);return w;
        }
        public static void Run()
        {
            var optimized=Fixture();var baseline=WorldStateCodec.Clone(optimized);var fast=new Community();var old=new CommunityBaselineReference();
            for(int tick=0;tick<12;tick++)
            {
                foreach(var world in new[]{optimized,baseline})
                {
                    world.Tick=tick;
                    if(tick==2)world.Entities["exit"].Set("reachable",0);
                    if(tick==3)world.Entities["place"].Set("removed",1);
                    if(tick==4){world.Entities["exit"].Tags.Clear();world.Entities["place"].Set("removed",0);world.Entities["place"].Tags.Add("safe-zone");}
                    if(tick==5){var added=new Entity{Id="new-destination",Kind="place",X=20};added.Tags.Add("destination");world.Add(added);}
                    if(tick==6)world.Entities.Remove("new-destination");
                    if(tick==7)world.Entities["danger"].Set("fire",0);
                    if(tick==8)world.Entities["npc:1"].Set("occlusion.danger",1);
                }
                fast.Step(optimized);old.Step(baseline);
                Check(optimized.Digest()==baseline.Digest(),"membership/capability/removal/reachability/occlusion interventions preserve all states and goal choices at tick "+tick);
                Check(JsonConvert.SerializeObject(optimized.Journal)==JsonConvert.SerializeObject(baseline.Journal),"observer knowledge and ordered transitions exactly match baseline at tick "+tick);
            }
            Console.WriteLine("Community destination/goal sparse indexes equal baseline for state, goals, observation knowledge and ordered transitions under 12 intervention ticks.");
        }
    }
}
