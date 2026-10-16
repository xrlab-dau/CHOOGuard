using System;
using System.Collections.Generic;
using System.Linq;

namespace ChooGuard.Seeded
{
    /// <summary>
    /// Local, seeded capability simulation. Rates are game parameters per second, not measured
    /// incident frequencies. Each atomic mechanism has an independent counter-based stream.
    /// Render children do not become independent risk sources unless explicitly tagged semantic.
    /// </summary>
    public sealed class CausalKernel
    {
        public void Step(World world)
        {
            Preflight(world);
            var objects = SemanticEntities(world);
            var space = new LocalSpace(objects);
            Environment(world);
            foreach (var e in objects) WaterAndSupply(world, e, space);
            var heat=HeatExchange(world,objects,space);
            foreach (var e in objects) Thermal(world, e, heat);
            foreach (var e in objects) StructureAndCommunications(world, e, space);
            foreach (var e in objects) if (e.Has("npc")) Person(world, e, space);
            world.Tick++;
        }

        /// <summary>Validate the complete input before any journal, field or entity write.
        /// The envelope is an explicit numerical contract, not a physical calibration.</summary>
        public static void Preflight(World world)
        {
            if(world==null)throw new ArgumentNullException("world");
            if(!Finite(world.StepSeconds)||world.StepSeconds<=0||world.StepSeconds>60||!Finite(world.RateMultiplier)||world.RateMultiplier<0||world.RateMultiplier>1000000||world.Tick<0||world.Tick==long.MaxValue)
                throw new ArgumentException("Invalid bounded tick contract");
            foreach(var field in world.Fields)
            {
                if(!Finite(field.Value))throw new ArgumentException("Non-finite world field: "+field.Key);
                if(field.Key.EndsWith(".radius",StringComparison.Ordinal)&&(field.Value<0||field.Value>1000))throw new ArgumentException("Invalid world radius: "+field.Key);
                if(!field.Key.Contains(".word")&&Math.Abs(field.Value)>(field.Key.StartsWith("rate.",StringComparison.Ordinal)?1e100:1000000))throw new ArgumentException("World parameter exceeds numerical envelope: "+field.Key);
            }
            foreach(var pair in world.Entities)
            {
                var e=pair.Value;
                if(e==null||pair.Key!=e.Id||string.IsNullOrEmpty(e.Id)||e.Version<0||e.Version>long.MaxValue-1024||!Finite(e.X)||!Finite(e.Y)||!Finite(e.Z)||Math.Max(Math.Abs(e.X),Math.Max(Math.Abs(e.Y),Math.Abs(e.Z)))>1000000000)
                    throw new ArgumentException("Invalid bounded entity identity/pose: "+pair.Key);
                foreach(var state in e.State)
                {
                    if(!Finite(state.Value))throw new ArgumentException("Non-finite entity state: "+e.Id+":"+state.Key);
                    // Unrelated opaque source metadata is retained; known causal inputs have
                    // bounded magnitudes to prevent multiplication/exp/distance overflow.
                    if(CausalInput(state.Key)&&Math.Abs(state.Value)>1000000)throw new ArgumentException("Causal parameter exceeds numerical envelope: "+state.Key);
                }
                foreach(string radius in new[]{"heat.reach","crowd.radius","debris.radius","collision.radius","radius","sight.radius","hearing.radius","sensor.radius","water.reach"})
                    if(e.State.ContainsKey(radius)&&(e.Get(radius)<0||e.Get(radius)>1000))throw new ArgumentException("Invalid local radius: "+radius);
                if(e.Has("npc")&&e.Get("crowd.radius",2)<.001)throw new ArgumentException("Positive crowd radius required");
            }
            // This also detects semantic parent cycles before Environment writes.
            SemanticEntities(world);
        }
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        static readonly HashSet<string> causalInputs=new HashSet<string>(new[]{"power","supply.available","power.isolated","failed","leak","water.pressure","water.isolated","wear","wetness","drainage","dry.traction","wet.slip","water.damage","water.tolerance","thermal.capacity","thermal.max-transfer-rate","thermal.conductivity","thermal.exposure","thermal.barrier","thermal.zone","thermal.vertical-reach","temperature","heat.output","fault","fault.heat","spark","fuel","fire","combustion.heat","cooling","ignition.temperature","wet.ignition-limit","smoke","ventilation","seismic.response","wind.rating","structural.heat-limit","load.capacity","integrity","damage.rate","failure.threshold","communications.available","alert.received","alert.relay","smoke.threshold","health","crowd.safe-density","medical.vulnerability","medical.condition","fallen","speed","hold","assisted","mobility.base","axis.x","axis.z","half.length","half.width"},StringComparer.Ordinal);
        static bool CausalInput(string key)=>causalInputs.Contains(key);

        public static List<Entity> SemanticEntities(World world)
        {
            var roots = new SortedDictionary<string, Entity>(StringComparer.Ordinal);
            foreach (var e in world.Entities.Values)
            {
                if (e.Has("decoration") || e.Has("render-only") || e.Get("removed") > 0) continue;
                Entity root = e;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                while (!root.Has("semantic") && !root.Has("npc") && !string.IsNullOrEmpty(root.ParentId))
                {
                    if (!seen.Add(root.Id)) throw new ArgumentException("Semantic parent cycle: " + e.Id);
                    Entity parent;
                    if (!world.Entities.TryGetValue(root.ParentId, out parent)) break;
                    root = parent;
                }
                if (!root.Has("decoration") && !root.Has("render-only") && root.Get("removed") <= 0) roots[root.Id] = root;
            }
            return roots.Values.ToList();
        }

        public static bool Change(World w, Entity target, string property, double after, string domain,
            string mechanism, string source, string reason)
        {
            if (double.IsNaN(after) || double.IsInfinity(after)) throw new ArgumentException("Non-finite causal result");
            double before = target.Get(property);
            if (target.State.ContainsKey(property) && before == after) return false;
            target.Set(property, after);
            w.Journal.Add(new Transition { Tick=w.Tick, Domain=domain, Mechanism=mechanism,
                Source=source, Target=target.Id, Property=property, Before=before, After=after, Reason=reason });
            return true;
        }

        public static double Parameter(World w, string key, double fallback)
        {
            double value;
            return w.Fields.TryGetValue(key, out value) ? value : fallback;
        }

        public static double Clamp(double x, double min=0, double max=1) { return Math.Max(min, Math.Min(max, x)); }
        static double Rate(World w, string mechanism, double fallback) { return Math.Max(0, Parameter(w,"rate."+mechanism,fallback)); }
        static void Field(World w, string key, double value, string mechanism, string reason)
        {
            double before = w.Field(key);
            if (w.Fields.ContainsKey(key) && before == value) return;
            w.Fields[key] = value;
            w.Journal.Add(new Transition { Tick=w.Tick, Domain="environment", Mechanism=mechanism,
                Source="@environment", Target="@world", Property=key, Before=before, After=value, Reason=reason });
        }

        static void Environment(World w)
        {
            // No guaranteed incident: zero rates and zero external inputs remain quiescent.
            double dt=w.StepSeconds;
            if (Parameter(w,"environment.autonomous",1) > 0)
            {
                if (Stable.Occurs(w,"weather.rain","@weather",Rate(w,"rain-onset",0.0003)))
                    Field(w,"rain", Math.Max(w.Field("rain"), 0.1+0.9*Stable.Unit(w.Seed,"rain.intensity",w.Tick,"@weather")), "rain-input", "Seeded regional precipitation input");
                if (Stable.Occurs(w,"weather.wind","@weather",Rate(w,"wind-gust",0.0005)))
                    Field(w,"wind",Math.Max(w.Field("wind"), 4+35*Stable.Unit(w.Seed,"wind.intensity",w.Tick,"@weather")),"wind-input","Seeded wind speed game input");
                if (Stable.Occurs(w,"ground.motion","@ground",Rate(w,"seismic-impulse",0.0000003)))
                    Field(w,"seismic",Math.Max(w.Field("seismic"),0.1+0.9*Stable.Unit(w.Seed,"ground.intensity",w.Tick,"@ground")),"ground-motion","Seeded normalized ground acceleration input");
                if (Stable.Occurs(w,"public.alert","@region",Rate(w,"regional-alert",0.0000003)))
                    Field(w,"regional-alert",1,"public-alert-input","High-level civil-defense/public safety alert; no tactical content");
                if (Stable.Occurs(w,"network.disruption","@network",Rate(w,"communications-disruption",0.00001)))
                    Field(w,"communications-disruption",1,"network-input","Independent regional communications disruption");
            }
            Field(w,"rain",Math.Max(0,w.Field("rain")-dt*Parameter(w,"rain.decay",0.00005)),"precipitation-recession","Precipitation input recession");
            Field(w,"wind",Math.Max(0,w.Field("wind")-dt*Parameter(w,"wind.decay",0.015)),"wind-recession","Wind input recession");
            Field(w,"seismic",Math.Max(0,w.Field("seismic")-dt*Parameter(w,"seismic.decay",0.3)),"ground-recession","Ground motion input recession");
            Field(w,"regional-alert",Math.Max(0,w.Field("regional-alert")-dt*Parameter(w,"alert.decay",0.001)),"public-alert-recession","Public alert input recession");
            Field(w,"communications-disruption",Math.Max(0,w.Field("communications-disruption")-dt*Parameter(w,"network.decay",0.002)),"network-recovery","Regional network recovery");
        }

        static void WaterAndSupply(World w, Entity e, LocalSpace space)
        {
            double dt=w.StepSeconds;
            if (e.Has("electrical") || e.Has("power-source"))
            {
                double supply = e.Has("power-source") || e.Has("battery") || e.Has("self-powered") ? e.Get("supply.available",1) : 0;
                string source=e.Id;
                foreach (string link in e.Links)
                {
                    Entity feed;
                    if (!w.Entities.TryGetValue(link,out feed) || feed.Get("removed")>0 || !feed.Has("power-source")) continue;
                    double available=feed.Get("supply.available",1)*(1-Clamp(feed.Get("power.isolated")))*(1-Clamp(feed.Get("failed")));
                    if (available> supply) { supply=available; source=feed.Id; }
                }
                double power=Clamp(supply)*(1-Clamp(e.Get("power.isolated")))*(1-Clamp(e.Get("failed")));
                Change(w,e,"power",power,"electrical","supply-connectivity",source,"Explicit supply relation, isolation and component integrity");
            }
            if (e.Has("water-source"))
            {
                double leak=e.Get("leak"), pressure=e.Get("water.pressure",1)*(1-Clamp(e.Get("water.isolated")));
                double condition=Clamp(e.Get("wear",0.05));
                if (pressure>0 && Stable.Occurs(w,"water.leak",e.Id,Rate(w,"water-leak",0.00002)*condition))
                    leak=Math.Max(leak,0.15+0.85*Stable.Unit(w.Seed,"water.leak-size",w.Tick,e.Id));
                Change(w,e,"leak",leak,"water","seal-failure",e.Id,"Pressurized water capability and component wear");
                Change(w,e,"water.outflow",leak*pressure,"water","pressure-flow",e.Id,"Isolation removes pressure-driven outflow");
            }
            if (e.Has("exposed") || e.Has("walkable") || e.Has("water-sensitive") || e.Has("electrical") || e.Has("combustible"))
            {
                double rain=e.Has("exposed") && !e.Has("sheltered") ? w.Field("rain")*0.03 : 0;
                double incoming=rain; var causes=new SortedSet<string>(StringComparer.Ordinal); if(rain>0) causes.Add("@weather");
                foreach (var other in space.Near(e,Parameter(w,"water.radius",3)))
                {
                    if (other.Id==e.Id || !other.Has("water-source")) continue;
                    bool related=e.Links.Contains(other.Id) || other.Links.Contains(e.Id);
                    double distance=e.Distance(other), reach=other.Get("water.reach",2);
                    if (!related && (distance>reach || Math.Abs(e.Y-other.Y)>1)) continue;
                    double flow=other.Get("water.outflow")*0.08/Math.Max(1,distance);
                    incoming+=flow; if(flow>0) causes.Add(other.Id);
                }
                double wet=Clamp(e.Get("wetness")+dt*(incoming-e.Get("drainage",0.004)));
                Change(w,e,"wetness",wet,"water","surface-wetting",causes.Count>0?string.Join(",",causes):e.Id,"Exposure or nearby/linked pressure flow minus drainage; source lists all positive contributors");
                if(e.Has("walkable")) Change(w,e,"traction",Clamp(e.Get("dry.traction",1)-wet*e.Get("wet.slip",0.65)),"water","traction-loss",e.Id,"Material-specific wet traction");
                if(e.Has("water-sensitive")) Change(w,e,"water.damage",Clamp(e.Get("water.damage")+dt*Math.Max(0,wet-e.Get("water.tolerance",0.5))*0.01),"water","water-ingress",e.Id,"Moisture above material tolerance");
            }
        }

        sealed class HeatPair
        {
            public Entity Hot,Cold;
            public double Energy;
        }
        sealed class HeatBalance
        {
            public double Power;
            public readonly SortedSet<string> Sources=new SortedSet<string>(StringComparer.Ordinal);
        }
        static bool ThermalBody(Entity e)
        { return e.Has("thermal") || e.Has("combustible") || e.Has("electrical") || e.Has("heat-source"); }

        static Dictionary<string,HeatBalance> HeatExchange(World w,List<Entity> objects,LocalSpace space)
        {
            // Exchange uses one temperature snapshot, not iteration-updated temperatures. Each pair
            // subtracts exactly the energy it adds. Degrees/capacity are explicit game units rather
            // than a measured CFD/material model. Source/receiver budgets bound multi-body transfer.
            var bodies=objects.Where(ThermalBody).ToList();
            var balances=bodies.ToDictionary(e=>e.Id,e=>new HeatBalance(),StringComparer.Ordinal);
            var pairs=new List<HeatPair>();
            var outgoing=new Dictionary<string,double>(StringComparer.Ordinal);
            var incoming=new Dictionary<string,double>(StringComparer.Ordinal);
            var maximumDonor=new Dictionary<string,double>(StringComparer.Ordinal);
            double ambient=Parameter(w,"ambient.temperature",20);
            foreach(var a in bodies) foreach(var b in space.Near(a,Math.Max(0,a.Get("heat.reach",3))))
            {
                if(StringComparer.Ordinal.Compare(a.Id,b.Id)>=0 || !ThermalBody(b)) continue;
                double distance=a.Distance(b);
                if(distance>Math.Min(a.Get("heat.reach",3),b.Get("heat.reach",3))) continue;
                bool linked=a.Links.Contains(b.Id)||b.Links.Contains(a.Id);
                if(!linked && (a.Get("thermal.link-required")>0 || b.Get("thermal.link-required")>0
                    || Math.Abs(a.Y-b.Y)>Math.Min(a.Get("thermal.vertical-reach",1.5),b.Get("thermal.vertical-reach",1.5))
                    || a.Get("thermal.zone")!=b.Get("thermal.zone"))) continue;
                double exposed=Clamp(a.Get("thermal.exposure",1))*Clamp(b.Get("thermal.exposure",1));
                if(a.Has("thermal-insulated") || b.Has("thermal-insulated") || a.Get("thermal.barrier")>0 || b.Get("thermal.barrier")>0) exposed=0;
                double conductance=Math.Max(0,Math.Min(a.Get("thermal.conductivity",0.12),b.Get("thermal.conductivity",0.12)))*exposed;
                if(!linked) conductance/=1+distance*distance;
                if(conductance<=0) continue;
                double ta=a.Get("temperature",ambient),tb=b.Get("temperature",ambient);
                if(ta==tb) continue;
                Entity hot=ta>tb?a:b,cold=ta>tb?b:a;
                double ch=Math.Max(0.01,hot.Get("thermal.capacity",1)),cc=Math.Max(0.01,cold.Get("thermal.capacity",1));
                double energy=Math.Min(Math.Abs(ta-tb)*conductance*w.StepSeconds,Math.Abs(ta-tb)/(1/ch+1/cc)*0.25);
                if(energy<=0) continue;
                pairs.Add(new HeatPair{Hot=hot,Cold=cold,Energy=energy});
                AddEnergy(outgoing,hot.Id,energy);AddEnergy(incoming,cold.Id,energy);
                double previous;maximumDonor.TryGetValue(cold.Id,out previous);maximumDonor[cold.Id]=Math.Max(previous,hot.Get("temperature",ambient));
            }
            foreach(var pair in pairs)
            {
                double hc=Math.Max(0.01,pair.Hot.Get("thermal.capacity",1)),cc=Math.Max(0.01,pair.Cold.Get("thermal.capacity",1));
                double hotBudget=Math.Min(Math.Max(0,pair.Hot.Get("temperature",ambient)-ambient)*hc*0.25,
                    Math.Max(0,pair.Hot.Get("thermal.max-transfer-rate",20))*hc*w.StepSeconds);
                double coldBudget=Math.Min(Math.Max(0,maximumDonor[pair.Cold.Id]-pair.Cold.Get("temperature",ambient))*cc*0.25,
                    Math.Max(0,pair.Cold.Get("thermal.max-transfer-rate",20))*cc*w.StepSeconds);
                double scale=Math.Min(1,Math.Min(hotBudget/outgoing[pair.Hot.Id],coldBudget/incoming[pair.Cold.Id]));
                double power=pair.Energy*scale/w.StepSeconds;
                balances[pair.Hot.Id].Power-=power;balances[pair.Cold.Id].Power+=power;
                if(power>0) balances[pair.Cold.Id].Sources.Add(pair.Hot.Id);
            }
            return balances;
        }
        static void AddEnergy(Dictionary<string,double> values,string id,double amount)
        { double old;values.TryGetValue(id,out old);values[id]=old+amount; }

        static void Thermal(World w, Entity e, Dictionary<string,HeatBalance> balances)
        {
            double dt=w.StepSeconds;
            if (!ThermalBody(e)) return;
            double temperature=e.Get("temperature",20), input=e.Has("heat-source")?e.Get("heat.output",0):0;
            var causes=new SortedSet<string>(StringComparer.Ordinal); if(input>0) causes.Add(e.Id);
            var exchange=balances[e.Id];input+=exchange.Power;foreach(string source in exchange.Sources)causes.Add(source);
            if (e.Has("electrical"))
            {
                double stress=e.Get("power")*Clamp(e.Get("wetness")*(1-Clamp(e.Get("insulation",0.8))))+e.Get("power")*Clamp(e.Get("wear"))*0.02;
                if(e.Get("power")>0 && stress>0 && Stable.Occurs(w,"electrical.short",e.Id,Rate(w,"electrical-short",0.02)*stress))
                    Change(w,e,"fault",1,"electrical","conductive-fault",e.Id,"Powered conductor, ingress/wear and insulation relation");
                input+=e.Get("power")*e.Get("fault")*e.Get("fault.heat",50);
                if(e.Get("power")*e.Get("fault")>0) causes.Add(e.Id);
                Change(w,e,"spark",e.Get("power")*e.Get("fault"),"electrical","fault-energy",e.Id,"Electrical isolation removes sustained fault energy");
            }
            double fire=e.Get("fire"), fuel=e.Get("fuel",e.Has("combustible")?1:0);
            if(fire>0) causes.Add(e.Id);
            string heatSource=causes.Count>0?string.Join(",",causes):e.Id;
            temperature=Math.Max(w.Field("ambient.temperature")>0?w.Field("ambient.temperature"):20,
                temperature+dt*((input+fire*e.Get("combustion.heat",80))/Math.Max(0.01,e.Get("thermal.capacity",1))
                    -Math.Max(0,temperature-20)*e.Get("cooling",0.04)-e.Get("wetness")*4));
            Change(w,e,"temperature",temperature,"thermal","energy-balance",heatSource,"Snapshot temperature-gradient exchange with equal donor loss/receiver gain, local exposure/conductivity and bounded capacity; powered faults, finite-fuel combustion, cooling and water");
            double ignition=e.Get("ignition.temperature",170);
            if(e.Has("combustible") && fuel>0 && temperature>ignition && e.Get("wetness")<e.Get("wet.ignition-limit",0.85)
                && Stable.Occurs(w,"material.ignition",e.Id,Rate(w,"ignition",0.2)*(temperature-ignition)/Math.Max(1,ignition)))
                fire=Math.Max(fire,0.1);
            if(fire>0) fire=Clamp(fire+dt*(fuel>0?0.08: -0.5)-dt*e.Get("wetness")*0.4);
            Change(w,e,"fire",fire,"thermal","material-combustion",heatSource,"Combustible material, ignition temperature, available fuel and moisture");
            Change(w,e,"fuel",Math.Max(0,fuel-dt*fire*0.004),"thermal","fuel-consumption",e.Id,"Combustion consumes finite material fuel");
            Change(w,e,"smoke",Clamp(e.Get("smoke")+dt*(fire*0.1+e.Get("spark")*0.02-e.Get("ventilation",0.02))),"thermal","smoke-balance",e.Id,"Combustion/fault emissions and ventilation");
        }

        static void StructureAndCommunications(World w, Entity e, LocalSpace space)
        {
            double dt=w.StepSeconds;
            if(e.Has("structural"))
            {
                double seismic=w.Field("seismic")*e.Get("seismic.response",1);
                double wind=e.Has("exposed")?Math.Pow(w.Field("wind")/Math.Max(1,e.Get("wind.rating",35)),2):0;
                double heat=Math.Max(0,e.Get("temperature",20)-e.Get("structural.heat-limit",350))/500;
                double overload=Math.Max(0,seismic+wind+heat-e.Get("load.capacity",0.6));
                double integrity=Clamp(e.Get("integrity",1)-dt*overload*e.Get("damage.rate",0.08));
                Change(w,e,"integrity",integrity,"structural","load-damage",seismic>0?"@ground":wind>0?"@weather":e.Id,"Ground/wind/thermal loading relative to explicit capacity");
                if(integrity<=e.Get("failure.threshold",0.15)) Change(w,e,"failed",1,"structural","capacity-failure",e.Id,"Integrity below component failure threshold");
            }
            if(e.Has("radio") || e.Has("communications") || e.Has("alarm"))
            {
                double operating=(1-Clamp(e.Get("water.damage")))*(1-Clamp(e.Get("failed")));
                if(e.Has("electrical")) operating*=e.Get("power");
                double available=operating*(1-Clamp(w.Field("communications-disruption")));
                Change(w,e,"communications.available",available,"communications","network-reachability","@network","Regional network, local power and component condition");
                if(e.Has("radio") || e.Has("communications"))
                    Change(w,e,"alert.received",available>0.2?w.Field("regional-alert"):0,"civil-defense","public-alert-delivery","@region","Alert reaches functioning receiver only");
                if(e.Has("alarm"))
                {
                    double detected=Math.Max(e.Get("alert.received"),e.Get("alert.relay")); string source=e.Id;
                    foreach(var other in space.Near(e,e.Get("sensor.radius",8)))
                        if(other.Get("smoke")>e.Get("smoke.threshold",0.1) || other.Get("fire")>0.1) { detected=1; source=other.Id; }
                    Change(w,e,"alarm.active",operating>0.2?detected:0,"communications","local-alarm",source,"Local sensor, manual relay or received alert and operating circuit; regional network outage does not disable a local alarm");
                }
            }
        }

        static void Person(World w, Entity e, LocalSpace space)
        {
            if(e.Get("health",1)<=0) return;
            double dt=w.StepSeconds, density=0, surfaceRisk=0, exposure=0, structural=0, collision=0;
            var causes=new SortedSet<string>(StringComparer.Ordinal);
            string surface=e.Id, hazard=e.Id;
            foreach(var other in space.Near(e,Math.Max(4,e.Get("crowd.radius",2))))
            {
                if(other.Id==e.Id) continue;
                double d=e.Distance(other);
                if(other.Has("npc") && d<e.Get("crowd.radius",2)) density+=1;
                double local=other.Get("fire")/Math.Max(1,d*d)+other.Get("smoke")/Math.Max(1,d);
                if(d<3 && local>0) { exposure+=local; hazard=other.Id; causes.Add(other.Id); }
                if(other.Has("structural") && other.Get("failed")>0 && d<other.Get("debris.radius",3)) { structural+=1; hazard=other.Id; causes.Add(other.Id); }
                if(other.Has("moving-body") && d<other.Get("collision.radius",1.5))
                { double amount=Math.Max(0,other.Get("speed"))*(1-Clamp(other.Get("hold")))*0.02; collision+=amount; if(amount>0) { hazard=other.Id;causes.Add(other.Id); } }
            }
            foreach(var other in space.SurfacesAt(e))
            { double risk=1-other.Get("traction",1); if(risk>surfaceRisk) { surfaceRisk=risk; surface=other.Id; } }
            density/=Math.PI*Math.Pow(e.Get("crowd.radius",2),2);
            Change(w,e,"crowd.density",density,"crowd","local-density",e.Id,"Actual nearby semantic people per square metre");
            double pressure=Math.Max(0,density-e.Get("crowd.safe-density",2));
            Change(w,e,"crowd.pressure",pressure,"crowd","compression",e.Id,"Local density relative to personal safe-density game parameter");
            double medical=e.Get("medical.vulnerability",e.Has("medical-vulnerable")?0.3:0.01);
            if(Stable.Occurs(w,"health.acute",e.Id,Rate(w,"medical",0.00001)*Math.Max(0,medical)))
                Change(w,e,"medical.condition",Math.Max(e.Get("medical.condition"),0.1+0.6*Stable.Unit(w.Seed,"health.severity",w.Tick,e.Id)),"medical","acute-condition",e.Id,"Individual susceptibility and independent seeded physiological trigger");
            if(e.Get("fallen")<=0 && e.Get("speed",0)>0.1 && Stable.Occurs(w,"locomotion.fall",e.Id,Rate(w,"fall",0.02)*(surfaceRisk+pressure*0.05)*e.Get("speed")))
                Change(w,e,"fallen",1,"fall","balance-loss",surface,"Moving person, local surface traction or crowd pressure");
            double medicalDamage=e.Get("medical.condition")*0.003*(1-Clamp(e.Get("assisted"))*0.8);
            if(medicalDamage>0 || pressure>0) causes.Add(e.Id);
            double damage=dt*(exposure*0.02+structural*0.08+pressure*0.003+medicalDamage+collision);
            double health=Clamp(e.Get("health",1)-damage);
            Change(w,e,"health",health,"medical","exposure-injury",causes.Count>0?string.Join(",",causes):hazard,"Local heat/smoke/debris/crowd exposure and physiological condition; all positive contributors retained");
            Change(w,e,"mobility",Clamp(e.Get("mobility.base",1)*health*(1-e.Get("fallen"))),"medical","mobility",e.Id,"Personal baseline, health and balance state constrain locomotion");
        }
    }

    // Position-only neighbourhoods, stable output ordering, independent of mesh/node insertion order.
    internal sealed class LocalSpace
    {
        const double CellSize=8;
        readonly Dictionary<string,List<Entity>> cells=new Dictionary<string,List<Entity>>(StringComparer.Ordinal);
        readonly List<Entity> surfaces=new List<Entity>();
        public LocalSpace(IEnumerable<Entity> entities)
        {
            foreach(var e in entities)
            {
                if(e.Has("walkable")) surfaces.Add(e);
                string key=Key(Cell(e.X),Cell(e.Y),Cell(e.Z)); List<Entity> bucket;
                if(!cells.TryGetValue(key,out bucket)) { bucket=new List<Entity>(); cells.Add(key,bucket); }
                bucket.Add(e);
            }
        }
        static int Cell(double x) { return (int)Math.Floor(x/CellSize); }
        static string Key(int x,int y,int z) { return x+":"+y+":"+z; }
        public IEnumerable<Entity> SurfacesAt(Entity person)
        {
            foreach(var e in surfaces)
            {
                if(Math.Abs(person.Y-e.Y)>1.5) continue;
                double dx=person.X-e.X,dz=person.Z-e.Z;
                double axisX=e.Get("axis.x",1),axisZ=e.Get("axis.z",0),length=Math.Sqrt(axisX*axisX+axisZ*axisZ);
                if(length<=0) { axisX=1; axisZ=0; length=1; }
                double along=(dx*axisX+dz*axisZ)/length,across=(-dx*axisZ+dz*axisX)/length;
                bool inside=e.State.ContainsKey("half.length")
                    ? Math.Abs(along)<=e.Get("half.length") && Math.Abs(across)<=e.Get("half.width",2)
                    : Math.Sqrt(dx*dx+dz*dz)<e.Get("radius",3);
                if(inside) yield return e;
            }
        }
        public List<Entity> Near(Entity e,double radius)
        {
            if(double.IsNaN(radius)||double.IsInfinity(radius)||radius<0||radius>1000)throw new ArgumentException("Bounded local query radius required");
            radius=Math.Max(0,radius);
            var result=new List<Entity>();
            // Large but valid radii use a finite scan rather than potentially billions
            // of mostly empty grid cells. Ordinary local queries retain the spatial grid.
            if(radius>32)
            {foreach(var bucket in cells.Values)foreach(var other in bucket)if(e.Distance(other)<=radius)result.Add(other);result.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Id,b.Id));return result;}
            for(int x=Cell(e.X-radius);x<=Cell(e.X+radius);x++)
                for(int y=Cell(e.Y-radius);y<=Cell(e.Y+radius);y++)
                    for(int z=Cell(e.Z-radius);z<=Cell(e.Z+radius);z++)
                    {
                        List<Entity> bucket;
                        if(cells.TryGetValue(Key(x,y,z),out bucket)) foreach(var other in bucket) if(e.Distance(other)<=radius) result.Add(other);
                    }
            result.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Id,b.Id)); return result;
        }
    }
}
