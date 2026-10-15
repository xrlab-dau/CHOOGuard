using System;
using System.Collections.Generic;
using System.Linq;

namespace ChooGuard.Seeded
{
    /// <summary>Local perception and persistent goals. The adapter owns movement/line-of-sight input.</summary>
    public sealed class Community
    {
        // goal.kind: 0 routine, 1 avoid/evacuate, 2 assist, 3 shelter. No global hazard oracle.
        public void Step(World world)
        {
            if(world==null) throw new ArgumentNullException("world");
            var entities=CausalKernel.SemanticEntities(world);
            var space=new LocalSpace(entities);
            var destinations=entities.Where(DestinationCandidate).ToList();
            var semanticIds=new HashSet<string>(entities.Select(e=>e.Id),StringComparer.Ordinal);
            // Snapshot social knowledge: one update cannot relay across an arbitrary chain in one tick.
            var knowledge=entities.Where(e=>e.Has("npc")).ToDictionary(e=>e.Id,e=>e.Get("awareness"),StringComparer.Ordinal);
            foreach(var person in entities.Where(e=>e.Has("npc"))) Perceive(world,person,destinations,semanticIds,space,knowledge);
        }

        static void Write(World w,Entity e,string key,double value,string mechanism,string source,string reason)
        { CausalKernel.Change(w,e,key,value,"community",mechanism,source,reason); }

        static bool Visible(World w,Entity observer,Entity target)
        {
            // A Unity raycast adapter can set observer-specific occlusion.<stable entity ID> to 1.
            // Absence is a position-only approximation, not a claim of physical line-of-sight.
            return target.Get("visibility",1)>0 && observer.Get("occlusion."+target.Id)<=0
                && observer.Distance(target)<=observer.Get("sight.radius",12);
        }

        static void Perceive(World w,Entity p,List<Entity> destinations,HashSet<string> semanticIds,LocalSpace space,Dictionary<string,double> knowledge)
        {
            double sight=p.Get("sight.radius",12), hearing=p.Get("hearing.radius",20), signal=0;
            Entity strongest=null, patient=null; string source=p.Id;
            foreach(var e in space.Near(p,Math.Max(sight,hearing)))
            {
                if(e.Id==p.Id) continue;
                double distance=p.Distance(e), seen=0;
                if(Visible(w,p,e))
                {
                    seen=Math.Max(e.Get("fire"),Math.Max(e.Get("smoke"),e.Has("structural")?e.Get("failed"):0));
                    if(e.Has("npc") && (e.Get("fallen")>0 || e.Get("health",1)<0.75))
                    { seen=Math.Max(seen,0.4); if(patient==null) patient=e; }
                    if(e.Has("walkable")) seen=Math.Max(seen,e.Get("wetness")*0.3);
                }
                if(e.Get("alarm.active")>0 && distance<=Math.Min(hearing,e.Get("alarm.radius",20))) seen=Math.Max(seen,e.Get("alarm.active"));
                double aware;
                if(e.Has("npc") && p.Links.Contains(e.Id) && distance<=p.Get("conversation.radius",3)
                    && knowledge.TryGetValue(e.Id,out aware)) seen=Math.Max(seen,aware*0.8);
                if(seen>signal) { signal=seen; strongest=e; source=e.Id; }
            }
            if(p.Has("radio") && p.Get("communications.available")>0.2 && p.Get("alert.received")>0)
            { signal=Math.Max(signal,p.Get("alert.received")); source=p.Id; }
            double oldAwareness=p.Get("awareness"), age=signal>0?0:p.Get("memory.age")+w.StepSeconds;
            double awareness=Math.Max(signal,Math.Max(0,oldAwareness-w.StepSeconds*p.Get("memory.decay",0.005)));
            Write(w,p,"memory.age",age,"memory-age",source,"Time since locally perceived evidence");
            Write(w,p,"awareness",awareness,"local-observation",source,"Visible local condition, audible alarm, functioning personal radio or nearby social link");
            if(strongest!=null && signal>0)
            {
                Write(w,p,"memory.x",strongest.X,"observed-location",source,"Remember locally observed location");
                Write(w,p,"memory.y",strongest.Y,"observed-location",source,"Remember locally observed location");
                Write(w,p,"memory.z",strongest.Z,"observed-location",source,"Remember locally observed location");
            }
            double delay=p.Get("reaction.remaining");
            if(oldAwareness<0.15 && signal>=0.15)
                delay=p.Get("reaction.minimum",0.3)+Stable.Unit(w.Seed,"community.reaction",w.Tick,p.Id)*p.Get("reaction.spread",3);
            else delay=Math.Max(0,delay-w.StepSeconds);
            Write(w,p,"reaction.remaining",delay,"reaction-latency",source,"Seeded personal response delay after perceived evidence");
            double stress=CausalKernel.Clamp(p.Get("stress")+w.StepSeconds*(awareness*0.08-(signal<=0?0.025:0)));
            Write(w,p,"stress",stress,"stress-balance",source,"Perceived urgency and recovery");
            if(p.Get("seated")>0)
            {
                var seat=destinations.FirstOrDefault(e=>e.Has("seat")&&p.Get("seat.target."+e.Id)>0&&e.Get("occupant."+p.Id)>0&&e.Get("removed")==0);
                if(seat!=null&&awareness<0.15&&seat.Get("failed")==0)
                {
                    Write(w,p,"fatigue",Math.Max(0,p.Get("fatigue")-w.StepSeconds*p.Get("rest.recovery",0.01)),"seated-rest",seat.Id,"Abstract rest at an owned source seat; no posture-animation or clinical recovery claim");
                    return;
                }
                foreach(var owned in Targets(w,p,"seat.target.").Where(e=>e.Has("seat")))
                {
                    if(owned.Get("occupant."+p.Id)>0)
                    {
                        Write(w,owned,"occupant."+p.Id,0,"seat-release",p.Id,"Locally perceived urgency or lost seat ends this occupant relation");
                        Write(w,owned,"occupied",0,"seat-release",p.Id,"Release only the actor's owned seat");
                    }
                    Write(w,p,"seat.target."+owned.Id,0,"seat-release",owned.Id,"Release prior source-seat identity");
                }
                Write(w,p,"seated",0,"seat-release",source,"Stand before responding to local evidence or lost seat");
            }
            if(p.Get("mobility",1)<=0 || delay>0) return;
            if(p.Get("goal.active")>0)
            {
                double dx=p.X-p.Get("goal.x",p.X),dy=p.Y-p.Get("goal.y",p.Y),dz=p.Z-p.Get("goal.z",p.Z);
                bool arrived=Math.Sqrt(dx*dx+dy*dy+dz*dz)<=p.Get("arrival.radius",0.75);
                bool blocked=p.Get("goal.failed")>0;
                bool dangerInvalidates=signal>=0.15 && p.Get("goal.kind")==0 && p.Get("goal.external")<=0;
                if(p.Get("goal.external")>0 && signal>0.7) dangerInvalidates=true;
                if(!arrived && !blocked && !dangerInvalidates) return;
                Write(w,p,"goal.active",0,"goal-finished",source,arrived?"Arrived at existing destination":blocked?"Navigation reported unreachable":"Local danger invalidates routine goal");
                if(arrived) foreach(var destination in Targets(w,p,"goal.target.").Where(e=>semanticIds.Contains(e.Id)))
                    Write(w,p,"visit."+destination.Id,w.Tick,"destination-memory",destination.Id,"Remember visit to actual destination");
                Write(w,p,"goal.failed",0,"goal-finished",source,"Consume navigation failure report");
                Write(w,p,"goal.external",0,"goal-finished",source,"Release completed or invalidated action goal");
            }
            if(awareness>=0.15)
            {
                if(patient!=null && p.Has("first-aid") && stress<p.Get("assist.stress-limit",0.7))
                { Goal(w,p,2,patient.X,patient.Y,patient.Z,patient.Id,"Locally observed patient and first-aid capability"); return; }
                Entity destination=null;
                foreach(var e in destinations)
                {
                    if(!e.Has("exit") && !e.Has("shelter") && !e.Has("safe-zone")) continue;
                    if(!p.Links.Contains(e.Id) && !Visible(w,p,e)) continue;
                    if(e.Get("reachable",1)<=0 || p.Get("unreachable."+e.Id)>0) continue;
                    // Knowledge permits a destination, but unseen actual hazards are not consulted.
                    if(Visible(w,p,e) && (e.Get("fire")>0.1 || e.Get("failed")>0 || e.Get("blocked")>0)) continue;
                    if(destination==null || p.Distance(e)<p.Distance(destination)) destination=e;
                }
                if(destination!=null)
                { Goal(w,p,destination.Has("shelter")?3:1,destination.X,destination.Y,destination.Z,destination.Id,"Seen or previously known destination outside observed obstruction"); return; }
                destination=ChooseDestination(w,p,destinations,true);
                if(destination!=null) Goal(w,p,1,Destination(destination,"x"),Destination(destination,"y"),Destination(destination,"z"),destination.Id,"Choose a known physical place farther from remembered danger");
                else Write(w,p,"goal.active",0,"navigation-wait",source,"No known reachable physical refuge; wait for local information or an instruction");
                return;
            }
            Entity routine=ChooseDestination(w,p,destinations,false);
            if(routine!=null) Goal(w,p,0,Destination(routine,"x"),Destination(routine,"y"),Destination(routine,"z"),routine.Id,"Seeded utility over real known/visible destinations, personal preference, distance and visit memory");
        }

        // Fresh per-step membership, no cached capability/removal index. Mutable
        // reachability/hazard/occupancy checks remain in the actual decision loop.
        static bool DestinationCandidate(Entity e)=>e.Has("destination")||e.Has("seat")||e.Has("door")||e.Has("place")||e.Has("exit")||e.Has("shelter")||e.Has("safe-zone");
        static Entity[] Targets(World world,Entity person,string prefix)
        {
            return person.State.Where(s=>s.Key.StartsWith(prefix,StringComparison.Ordinal)&&s.Value>0).Select(s=>s.Key.Substring(prefix.Length)).Where(world.Entities.ContainsKey).OrderBy(id=>id,StringComparer.Ordinal).Select(id=>world.Entities[id]).ToArray();
        }

        static double Destination(Entity e,string axis)=>e.Get("destination."+axis,axis=="x"?e.X:axis=="y"?e.Y:e.Z);

        static Entity ChooseDestination(World w,Entity p,List<Entity> destinations,bool avoid)
        {
            Entity best=null; double bestUtility=double.NegativeInfinity;
            foreach(var e in destinations)
            {
                if(e.Id==p.Id || !(e.Has("destination") || e.Has("seat") || e.Has("door") || e.Has("place") || e.Has("exit") || e.Has("shelter") || e.Has("safe-zone"))) continue;
                if(e.Has("seat")&&e.Get("occupied")>0) continue;
                if(!p.Links.Contains(e.Id) && !Visible(w,p,e)) continue;
                if(e.Get("reachable",1)<=0 || p.Get("unreachable."+e.Id)>0 || p.Distance(e)<=p.Get("arrival.radius",0.75)) continue;
                if(Visible(w,p,e) && (e.Get("fire")>0.1 || e.Get("failed")>0 || e.Get("blocked")>0 || (e.Has("door") && e.Get("jammed")>0))) continue;
                double utility=Stable.Unit(w.Seed,"community.destination-utility",w.Tick,p.Id+">"+e.Id)*0.4-p.Distance(e)*0.02;
                if(e.Has("seat")) utility+=p.Get("preference.seat",0.3)+p.Get("fatigue");
                if(e.Has("door")) utility+=p.Get("preference.board",0.2);
                if(e.Has("place")) utility+=p.Get("preference.place",0.2);
                double visited;
                if(p.State.TryGetValue("visit."+e.Id,out visited)) utility-=Math.Max(0,1-(w.Tick-visited)/120.0);
                if(avoid)
                {
                    double dx=e.X-p.Get("memory.x",p.X),dz=e.Z-p.Get("memory.z",p.Z);
                    utility+=Math.Sqrt(dx*dx+dz*dz)*0.05;
                }
                if(utility>bestUtility) { best=e;bestUtility=utility; }
            }
            return best;
        }

        static void Goal(World w,Entity p,int kind,double x,double y,double z,string source,string reason)
        {
            Write(w,p,"goal.kind",kind,"goal-update",source,reason);
            Write(w,p,"goal.active",1,"goal-update",source,reason);
            foreach(var e in Targets(w,p,"goal.target."))
                if(e.Id!=source) Write(w,p,"goal.target."+e.Id,0,"goal-update",source,"Release prior destination identity");
            if(w.Entities.ContainsKey(source)) Write(w,p,"goal.target."+source,1,"goal-update",source,"Actual destination identity");
            Write(w,p,"goal.x",x,"goal-update",source,reason);
            Write(w,p,"goal.y",y,"goal-update",source,reason);
            Write(w,p,"goal.z",z,"goal-update",source,reason);
        }
    }
}
