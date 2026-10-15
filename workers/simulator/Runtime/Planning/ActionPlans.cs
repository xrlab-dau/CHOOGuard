using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    public sealed class ActionStep
    {
        public string Operation, ActorId, TargetId;
        public double Amount = 1;
    }

    // A cached executable plan binds real object identities and exact dependencies, not a scenario name.
    public sealed class ActionPlan
    {
        public string Id, Goal, ModelVersion, PromptVersion, ExecutorVersion, NavVersion;
        public long TopologyVersion, CreatedTick;
        public readonly SortedDictionary<string, long> Dependencies = new SortedDictionary<string, long>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, string> DependencyGeometry = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, string> DependencyState = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, SortedSet<string>> ReadSet = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        public readonly SortedSet<string> PoseDependencies = new SortedSet<string>(StringComparer.Ordinal);
        public readonly SortedDictionary<int,string> MovementGuards = new SortedDictionary<int,string>();
        public bool StrictVersions;
        public readonly List<ActionStep> Steps = new List<ActionStep>();

        public static ActionPlan Capture(World world, string goal, string model, string prompt, string executor, string nav, IEnumerable<ActionStep> steps)
        {
            var plan = new ActionPlan { Goal = goal, ModelVersion = model, PromptVersion = prompt, ExecutorVersion = executor, NavVersion = nav,
                TopologyVersion = world.TopologyVersion, CreatedTick = world.Tick };
            var captured=new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in steps)
            {
                if (step == null || string.IsNullOrEmpty(step.ActorId) || string.IsNullOrEmpty(step.TargetId)) throw new ArgumentException("Explicit actor and target IDs required");
                Entity actor, target;
                if (!world.Entities.TryGetValue(step.ActorId, out actor) || !world.Entities.TryGetValue(step.TargetId, out target)) throw new ArgumentException("Unknown plan entity");
                plan.Steps.Add(new ActionStep { Operation = step.Operation, ActorId = step.ActorId, TargetId = step.TargetId, Amount = step.Amount });
                CaptureDependency(world,plan,actor,captured,false); CaptureDependency(world,plan,target,captured,true);
                plan.PoseDependencies.Add(target.Id);
                Read(plan,actor.Id,"goal.kind","goal.active","goal.external","authorization","extinguisher_charge","removed","health","mobility");
                Read(plan,actor.Id,"occlusion."+target.Id);Read(plan,target.Id,"visibility");
                switch(step.Operation)
                {
                    case "isolate_power":Read(plan,target.Id,"power","power.isolated","failed");break;
                    case "suppress_fire":Read(plan,target.Id,"fire","wetness");break;
                    case "assist":Read(plan,target.Id,"health","medical.condition","assisted","fallen");break;
                    case "warn":Read(plan,target.Id,"awareness","warned");break;
                    case "hold_train":Read(plan,target.Id,"hold","speed");Read(plan,actor.Id,"communications.available");break;
                    case "collect_extinguisher":Read(plan,target.Id,"charge");break;
                    case "move_to":Read(plan,target.Id,"open","jammed","locked","blocked","accessible","reachable","destination.x","destination.y","destination.z");Read(plan,actor.Id,"unreachable."+target.Id,"goal.failed","seated");break;
                    case "sit":
                    case "stand":Read(plan,target.Id,"occupied","occupant."+actor.Id,"accessible","reachable","destination.x","destination.y","destination.z");Read(plan,actor.Id,"seated","seat.target."+target.Id);break;
                    default:Read(plan,target.Id,"open","jammed","locked","blocked","accessible","reachable");break;
                }
            }
            if (plan.Steps.Count == 0) throw new ArgumentException("Plan needs supported operations");
            foreach(var d in plan.Dependencies) {var e=world.Entities[d.Key];plan.DependencyGeometry[d.Key]=Geometry(e,plan.PoseDependencies.Contains(e.Id));plan.DependencyState[d.Key]=StateFingerprint(e,plan.ReadSet[d.Key]);}
            plan.Id=Stable.Hash(new JArray("dependency-plan-v2",goal,model,prompt,executor,nav,world.Tick,
                new JArray(plan.Steps.Select(s=>new JArray(s.Operation,s.ActorId,s.TargetId,Number(s.Amount))))).ToString(Formatting.None));
            return plan;
        }

        internal static string Geometry(Entity e,bool pose=true)
        {
            return Stable.Hash(new JArray("entity-dependency-v2",pose?new JArray(Number(e.X),Number(e.Y),Number(e.Z)):null,
                e.ParentId,e.Kind,new JArray(e.Tags),new JArray(e.Links)).ToString(Formatting.None));
        }
        internal static string Number(double value){return BitConverter.DoubleToInt64Bits(value).ToString("x16",System.Globalization.CultureInfo.InvariantCulture);}
        internal static string StateFingerprint(Entity e,IEnumerable<string> keys)
        {return Stable.Hash(new JArray("state-dependency-v2",new JArray(keys.Distinct(StringComparer.Ordinal).OrderBy(k=>k,StringComparer.Ordinal).Select(k=>new JArray(k,e.State.ContainsKey(k),e.State.ContainsKey(k)?Number(e.Get(k)):null)))).ToString(Formatting.None));}
        private static void Read(ActionPlan plan,string id,params string[] keys) {SortedSet<string> set;if(!plan.ReadSet.TryGetValue(id,out set))plan.ReadSet[id]=set=new SortedSet<string>(StringComparer.Ordinal);foreach(var key in keys)set.Add(key);}
        private static void CaptureDependency(World world,ActionPlan plan,Entity e,HashSet<string> seen,bool access)
        {
            if(!seen.Add(e.Id))return;
            plan.Dependencies[e.Id]=e.Version;plan.DependencyGeometry[e.Id]=Geometry(e);
            Read(plan,e.Id,"power","power.isolated","failed","open","jammed","locked","blocked","removed");
            Entity linked;
            if(!string.IsNullOrEmpty(e.ParentId)&&world.Entities.TryGetValue(e.ParentId,out linked))
            {if(!linked.Has("npc"))plan.PoseDependencies.Add(linked.Id);CaptureDependency(world,plan,linked,seen,false);}
            // Links also encode memory/social/known places. Only typed supplying power and the selected
            // physical access chain belong to an executable dependency set; never traverse unrelated occupants.
            foreach(var id in e.Links)if(world.Entities.TryGetValue(id,out linked))
            {
                if(linked.Has("power-source"))CaptureDependency(world,plan,linked,seen,false);
                else if(access&&linked.Has("door"))CaptureDependency(world,plan,linked,seen,true);
                else if(access&&!e.Has("npc")&&linked.Has("walkable"))CaptureDependency(world,plan,linked,seen,false);
            }
            foreach(var source in world.Entities.Values)if(source.Has("power-source")&&source.Links.Contains(e.Id))CaptureDependency(world,plan,source,seen,false);
        }

        public ActionPlan Copy()
        {
            var p = new ActionPlan { Id=Id,Goal=Goal,ModelVersion=ModelVersion,PromptVersion=PromptVersion,ExecutorVersion=ExecutorVersion,NavVersion=NavVersion,TopologyVersion=TopologyVersion,CreatedTick=CreatedTick,StrictVersions=StrictVersions };
            foreach (var d in Dependencies) p.Dependencies.Add(d.Key,d.Value);
            foreach (var d in DependencyGeometry) p.DependencyGeometry.Add(d.Key,d.Value);
            foreach (var d in DependencyState) p.DependencyState.Add(d.Key,d.Value);
            foreach (var d in ReadSet) p.ReadSet.Add(d.Key,new SortedSet<string>(d.Value,StringComparer.Ordinal));
            foreach (var d in PoseDependencies) p.PoseDependencies.Add(d);
            foreach (var d in MovementGuards) p.MovementGuards.Add(d.Key,d.Value);
            foreach (var s in Steps) p.Steps.Add(new ActionStep { Operation=s.Operation,ActorId=s.ActorId,TargetId=s.TargetId,Amount=s.Amount });
            return p;
        }
    }

    public sealed class OperationSpec
    {
        public string Name, Label, TargetTag;
        public bool StaffOnly;
        public double Distance;
        internal Func<Entity,Entity,bool> Condition;
        internal Action<World,Entity,Entity,double> Apply;
    }

    public sealed class OperationCatalog
    {
        public const string Version = "platform-affordances-v3";
        private readonly SortedDictionary<string, OperationSpec> operations = new SortedDictionary<string, OperationSpec>(StringComparer.Ordinal);
        public IEnumerable<OperationSpec> All { get { return operations.Values; } }
        public void Register(OperationSpec operation)
        {
            if (operation == null || string.IsNullOrEmpty(operation.Name) || operation.Apply == null || operations.ContainsKey(operation.Name)) throw new ArgumentException("Unique executable operation required");
            operations.Add(operation.Name, operation);
        }
        public bool TryGet(string name, out OperationSpec operation) { return operations.TryGetValue(name ?? "",out operation); }
        public IReadOnlyList<OperationSpec> Available(World world, string actorId, string targetId)
        {
            Entity actor,target; var list=new List<OperationSpec>();
            if(!world.Entities.TryGetValue(actorId ?? "",out actor)||!world.Entities.TryGetValue(targetId ?? "",out target)) return list;
            foreach(var spec in operations.Values) if(Eligible(spec,actor,target)) list.Add(spec);
            return list;
        }
        internal static bool Eligible(OperationSpec spec, Entity actor, Entity target)
        {
            if(target.Get("mech.profile")>0&&(spec.Name=="open_door"||spec.Name=="close_door"||spec.Name=="isolate_power"||spec.Name=="collect_extinguisher"))return false;
            if(actor.Get("response.workflow-bound")>0&&(spec.Name=="assist"||spec.Name=="warn"||spec.Name=="hold_train"||spec.Name=="raise_alarm"||spec.Name=="cordon"||spec.Name=="clear_cordon"||spec.Name=="suppress_fire"||spec.Name=="isolate_power"))return false;
            return actor.Has("npc") && actor.Get("removed")==0 && target.Get("removed")==0 && actor.Get("health",1)>0 && actor.Get("mobility",1)>0 && (!spec.StaffOnly || actor.Has("staff")) &&
                (string.IsNullOrEmpty(spec.TargetTag)||target.Has(spec.TargetTag)) &&
                (spec.Distance<=0||actor.Distance(target)<=spec.Distance) && (spec.Condition==null||spec.Condition(actor,target));
        }
        public static OperationCatalog CreateDefault()
        {
            var c=new OperationCatalog();
            c.Register(new OperationSpec { Name="move_to",Label="이동 목표",Distance=0,Condition=(a,t)=>a.Id!=t.Id&&a.Get("seated")==0&&t.Get("reachable",1)>0&&a.Get("unreachable."+t.Id)==0,
                Apply=(w,a,t,n)=> { a.Set("goal.x",t.Get("destination.x",t.X));a.Set("goal.y",t.Get("destination.y",t.Y));a.Set("goal.z",t.Get("destination.z",t.Z));a.Set("goal.active",1);a.Set("goal.external",1); } });
            c.Register(new OperationSpec { Name="warn",Label="주변 사람에게 알림",TargetTag="npc",Distance=12,Condition=(a,t)=>a.Id!=t.Id,
                Apply=(w,a,t,n)=> { t.Set("awareness",1);t.Set("warned",1); } });
            c.Register(new OperationSpec { Name="assist",Label="부상자 지원",TargetTag="npc",Distance=2,Condition=(a,t)=>a.Id!=t.Id&&(t.Get("injury")>0||t.Get("health",1)<1),
                Apply=(w,a,t,n)=> {t.Set("assisted",1);t.Set("medical.condition",Math.Max(0,t.Get("medical.condition")-.1));} });
            c.Register(new OperationSpec { Name="isolate_power",Label="전원 차단",TargetTag="electrical",StaffOnly=true,Distance=3,Condition=(a,t)=>t.Get("power")>0&&t.Get("power.isolated")==0,
                Apply=(w,a,t,n)=> {t.Set("power.isolated",1);t.Set("power",0);} });
            c.Register(new OperationSpec { Name="suppress_fire",Label="소화기 사용",StaffOnly=true,Distance=4,Condition=(a,t)=>a.Get("extinguisher_charge")>0&&t.Get("fire")>0,
                Apply=(w,a,t,n)=> { double amount=Math.Min(a.Get("extinguisher_charge"),Math.Min(.25,n));a.Set("extinguisher_charge",Math.Max(0,a.Get("extinguisher_charge")-amount));t.Set("fire",Math.Max(0,t.Get("fire")-amount));t.Set("wetness",Math.Min(1,t.Get("wetness")+amount)); } });
            c.Register(new OperationSpec { Name="hold_train",Label="열차 정차 유지",TargetTag="train",StaffOnly=true,Distance=0,Condition=(a,t)=>t.Get("hold")==0&&(a.Distance(t)<=3||a.Has("radio")&&a.Get("communications.available")>.2),
                Apply=(w,a,t,n)=>t.Set("hold",1) });
            c.Register(new OperationSpec { Name="open_door",Label="문 열기",TargetTag="door",StaffOnly=true,Distance=3,Condition=(a,t)=>t.Get("locked")==0&&t.Get("jammed")==0&&t.Get("open")<1,
                Apply=(w,a,t,n)=>t.Set("open",1) });
            c.Register(new OperationSpec { Name="close_door",Label="문 닫기",TargetTag="door",StaffOnly=true,Distance=3,Condition=(a,t)=>t.Get("jammed")==0&&t.Get("open")>0,
                Apply=(w,a,t,n)=>t.Set("open",0) });
            c.Register(new OperationSpec { Name="collect_extinguisher",Label="소화기 확보",TargetTag="extinguisher",StaffOnly=true,Distance=2,Condition=(a,t)=>t.Get("charge",1)>0,
                Apply=(w,a,t,n)=> { a.Set("extinguisher_charge",a.Get("extinguisher_charge")+t.Get("charge",1));t.Set("charge",0); } });
            c.Register(new OperationSpec { Name="raise_alarm",Label="비상 방송·경보",TargetTag="alarm",StaffOnly=true,Distance=3,Condition=(a,t)=>t.Get("power")>0,
                Apply=(w,a,t,n)=> {t.Set("alarm.active",1);t.Set("alert.relay",1);} });
            c.Register(new OperationSpec { Name="cordon",Label="통행 통제선 설치",TargetTag="walkable",StaffOnly=true,Distance=3,Condition=(a,t)=>t.Get("blocked")==0,
                Apply=(w,a,t,n)=>t.Set("blocked",1) });
            c.Register(new OperationSpec { Name="clear_cordon",Label="통행 통제선 해제",TargetTag="walkable",StaffOnly=true,Distance=3,Condition=(a,t)=>t.Get("blocked")>0,
                Apply=(w,a,t,n)=>t.Set("blocked",0) });
            c.Register(new OperationSpec { Name="sit",Label="좌석에 앉기",TargetTag="seat",Distance=0,Condition=(a,t)=>ApproachDistance(a,t)<=1&&t.Get("occupied")==0&&t.Get("accessible",1)>0&&a.Get("seated")==0,
                Apply=(w,a,t,n)=> {t.Set("occupied",1);t.Set("occupant."+a.Id,1);a.Set("seated",1);a.Set("seat.target."+t.Id,1);a.Set("goal.active",0);a.Set("goal.external",0);} });
            c.Register(new OperationSpec { Name="stand",Label="좌석에서 일어나기",TargetTag="seat",Distance=0,Condition=(a,t)=>a.Get("seated")>0&&a.Get("seat.target."+t.Id)>0&&t.Get("occupant."+a.Id)>0,
                Apply=(w,a,t,n)=> {t.Set("occupied",0);t.Set("occupant."+a.Id,0);a.Set("seated",0);a.Set("seat.target."+t.Id,0);} });
            return c;
        }
        public static double ApproachDistance(Entity actor,Entity target)
        {double x=actor.X-target.Get("destination.x",target.X),y=actor.Y-target.Get("destination.y",target.Y),z=actor.Z-target.Get("destination.z",target.Z);return Math.Sqrt(x*x+y*y+z*z);}
    }

    public sealed class PlanCache
    {
        private readonly Dictionary<string,ActionPlan> entries=new Dictionary<string,ActionPlan>(StringComparer.Ordinal);
        public long Hits,Misses,Invalidations;
        public void Put(ActionPlan plan) { entries[plan.Goal]=plan.Copy(); }
        public bool TryGet(World world,string goal,string model,string prompt,string executor,string nav,out ActionPlan plan,out string reason)
        {
            ActionPlan found;plan=null;
            if(!entries.TryGetValue(goal,out found)) { Misses++;reason="cache_miss";return false; }
            if(!Validate(world,found,goal,model,prompt,executor,nav,out reason)) { entries.Remove(goal);Misses++;Invalidations++;return false; }
            plan=found.Copy();Hits++;return true;
        }
        public static bool Validate(World world,ActionPlan plan,string goal,string model,string prompt,string executor,string nav,out string reason)
        {
            reason="";
            if(plan==null) reason="missing_plan";
            else if(plan.Goal!=goal) reason="goal_changed";
            else if(plan.ModelVersion!=model||plan.PromptVersion!=prompt||plan.ExecutorVersion!=executor) reason="policy_version_changed";
            else if(plan.NavVersion!=nav) reason="navigation_geometry_changed";
            else if(plan.TopologyVersion!=world.TopologyVersion) reason="world_topology_changed";
            else foreach(var dep in plan.Dependencies) { Entity e;if(!world.Entities.TryGetValue(dep.Key,out e)) { reason="deleted_entity:"+dep.Key;break; }if(plan.StrictVersions&&e.Version!=dep.Value) { reason="state_version_changed:"+dep.Key;break; }string geometry;if(!plan.DependencyGeometry.TryGetValue(dep.Key,out geometry)||geometry!=ActionPlan.Geometry(e,plan.PoseDependencies.Contains(e.Id))) {reason="entity_geometry_changed:"+dep.Key;break;}string state;SortedSet<string> keys;if(!plan.ReadSet.TryGetValue(dep.Key,out keys)||!plan.DependencyState.TryGetValue(dep.Key,out state)||state!=ActionPlan.StateFingerprint(e,keys)){reason="relevant_state_changed:"+dep.Key;break;} }
            return reason.Length==0;
        }
    }

    public sealed class PlanExecutor
    {
        private readonly OperationCatalog catalog;
        private readonly HashSet<string> started=new HashSet<string>(StringComparer.Ordinal);
        public event Action<string> NavigationInvalidated;
        public PlanExecutor(OperationCatalog catalog) { this.catalog=catalog??throw new ArgumentNullException(nameof(catalog)); }
        public bool TryExecute(World world,ActionPlan plan,int index,string nav,out string reason)
        {
            if(!PlanCache.Validate(world,plan,plan==null?"":plan.Goal,plan==null?"":plan.ModelVersion,plan==null?"":plan.PromptVersion,OperationCatalog.Version,nav,out reason)) return false;
            if(index<0||index>=plan.Steps.Count) { reason="step_out_of_range";return false; }
            var step=plan.Steps[index];OperationSpec spec;Entity actor,target;
            if(!catalog.TryGet(step.Operation,out spec)) { reason="unsupported_operation";return false; }
            if(!world.Entities.TryGetValue(step.ActorId,out actor)||!world.Entities.TryGetValue(step.TargetId,out target)) { reason="deleted_entity";return false; }
            if(double.IsNaN(step.Amount)||double.IsInfinity(step.Amount)||step.Amount<=0||step.Amount>1) { reason="invalid_amount";return false; }
            if(!OperationCatalog.Eligible(spec,actor,target)) { reason="affordance_precondition_failed";return false; }
            var beforeActor=new SortedDictionary<string,double>(actor.State,StringComparer.Ordinal);
            var beforeTarget=new SortedDictionary<string,double>(target.State,StringComparer.Ordinal);
            spec.Apply(world,actor,target,step.Amount);
            // Refresh only dependencies written by this guarded step, never silently absorb unrelated world changes.
            plan.Dependencies[actor.Id]=actor.Version;plan.Dependencies[target.Id]=target.Version;
            plan.DependencyGeometry[actor.Id]=ActionPlan.Geometry(actor,plan.PoseDependencies.Contains(actor.Id));plan.DependencyGeometry[target.Id]=ActionPlan.Geometry(target,plan.PoseDependencies.Contains(target.Id));
            plan.DependencyState[actor.Id]=ActionPlan.StateFingerprint(actor,plan.ReadSet[actor.Id]);plan.DependencyState[target.Id]=ActionPlan.StateFingerprint(target,plan.ReadSet[target.Id]);
            JournalDiff(world,plan,step,actor,beforeActor);if(actor.Id!=target.Id)JournalDiff(world,plan,step,target,beforeTarget);
            started.Add(plan.Id+":"+index);
            if(step.Operation=="move_to")plan.MovementGuards[index]=ActionPlan.StateFingerprint(actor,MovementReadSet(plan,actor.Id));
            if(step.Operation=="cordon"||step.Operation=="clear_cordon"||step.Operation=="open_door"||step.Operation=="close_door")NavigationInvalidated?.Invoke(target.Id);
            reason=step.Operation=="move_to"?"movement_goal_started":"applied";return true;
        }
        private static void JournalDiff(World world,ActionPlan plan,ActionStep step,Entity entity,SortedDictionary<string,double> before)
        {
            foreach(var property in entity.State) {double old;before.TryGetValue(property.Key,out old);if(old!=property.Value)world.Journal.Add(new Transition {Tick=world.Tick,Domain="response",Mechanism=step.Operation,Source=step.ActorId,Target=entity.Id,Property=property.Key,Before=old,After=property.Value,Reason=plan.Id});}
        }
        public bool IsComplete(World world,ActionPlan plan,int index,double arrivalDistance=1)
        {
            if(plan==null||index<0||index>=plan.Steps.Count)return false;
            var step=plan.Steps[index];Entity actor,target;
            if(!world.Entities.TryGetValue(step.ActorId,out actor)||!world.Entities.TryGetValue(step.TargetId,out target))return false;
            return started.Contains(plan.Id+":"+index)&&(step.Operation!="move_to"||actor.Get("goal.failed")==0&&OperationCatalog.ApproachDistance(actor,target)<=arrivalDistance);
        }
        private static IEnumerable<string> MovementReadSet(ActionPlan plan,string actorId)
        {
            // Community may choose a new goal and normalize goal.failed from absent to zero after arrival.
            // IsComplete has already required zero failure; all other actor read values remain guarded.
            return plan.ReadSet[actorId].Where(key=>key!="goal.kind"&&key!="goal.active"&&key!="goal.external"&&key!="goal.failed");
        }
        public bool AcknowledgeMovementCompletion(World world,ActionPlan plan,int index,string nav,out string reason)
        {
            reason="movement_not_complete";
            if(plan==null||index<0||index>=plan.Steps.Count||plan.Steps[index].Operation!="move_to"||!IsComplete(world,plan,index))return false;
            var actor=world.Entities[plan.Steps[index].ActorId];string guard;
            if(!plan.MovementGuards.TryGetValue(index,out guard)||guard!=ActionPlan.StateFingerprint(actor,MovementReadSet(plan,actor.Id)))
            {reason="movement_non_goal_dependency_changed";return false;}
            // Validate a temporary expected-completion view. Every target, power, path, authorization, geometry,
            // health, occlusion and non-goal actor dependency retains its original stamp.
            var expected=plan.Copy();expected.Dependencies[actor.Id]=actor.Version;
            expected.DependencyState[actor.Id]=ActionPlan.StateFingerprint(actor,expected.ReadSet[actor.Id]);
            if(!PlanCache.Validate(world,expected,plan.Goal,plan.ModelVersion,plan.PromptVersion,OperationCatalog.Version,nav,out reason))return false;
            plan.Dependencies[actor.Id]=expected.Dependencies[actor.Id];plan.DependencyState[actor.Id]=expected.DependencyState[actor.Id];
            reason="movement_completion_acknowledged";return true;
        }
    }
}
