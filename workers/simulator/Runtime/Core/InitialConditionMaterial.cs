using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    public sealed class InitialConditionPrior
    {
        public string EntityId { get; private set; }
        public string Property { get; private set; }
        public double Lower { get; private set; }
        public double Upper { get; private set; }
        public InitialConditionPrior(string entityId,string property,double lower,double upper)
        { EntityId=entityId;Property=property;Lower=lower;Upper=upper; }
    }
    public sealed class InitialConditionValue
    {
        public string EntityId { get; private set; }
        public string Property { get; private set; }
        public double Value { get; private set; }
        internal InitialConditionValue(string entityId,string property,double value)
        {EntityId=entityId;Property=property;Value=value;}
    }
    public sealed class InitialConditionRule
    {
        public string Property { get; private set; }
        public string Target { get; private set; }
        public double Min { get; private set; }
        public double Max { get; private set; }
        readonly Func<Entity,bool> match;
        internal InitialConditionRule(string property,string target,double min,double max,Func<Entity,bool> match=null)
        {Property=property;Target=target;Min=min;Max=max;this.match=match;}
        public bool Matches(Entity e)=>e==null?Target=="@world":match!=null&&match(e);
    }
    /// <summary>Versioned, immutable upstream causal data; never completed emergency scripts.
    /// Bounds are training assumptions. Sampling is uniform, not a calibrated physical distribution.</summary>
    public sealed class InitialConditionMaterial
    {
        public const string Schema="seeded-causal-material-v1";
        public const string MaterializationVersion="bounded-uniform-counter-v1";
        public const int MaximumPriors=16;
        static bool Thermal(Entity e)=>e.Has("thermal")||e.Has("combustible")||e.Has("electrical")||e.Has("heat-source");
        static bool Wetted(Entity e)=>e.Has("exposed")||e.Has("walkable")||e.Has("water-sensitive")||e.Has("electrical")||e.Has("combustible");
        static readonly InitialConditionRule[] rules={
            new InitialConditionRule("wetness","exposed|walkable|water-sensitive|electrical|combustible",0,1,Wetted),
            new InitialConditionRule("drainage","exposed|walkable|water-sensitive|electrical|combustible",0,.05,Wetted),
            new InitialConditionRule("insulation","electrical",0,1,e=>e.Has("electrical")),
            new InitialConditionRule("wear","electrical|water-source",0,1,e=>e.Has("electrical")||e.Has("water-source")),
            new InitialConditionRule("water.pressure","water-source",0,1.5,e=>e.Has("water-source")),
            new InitialConditionRule("temperature","thermal|combustible|electrical|heat-source",5,80,Thermal),
            new InitialConditionRule("cooling","thermal|combustible|electrical|heat-source",.01,.2,Thermal),
            new InitialConditionRule("ventilation","thermal|combustible|electrical|heat-source",0,.3,Thermal),
            new InitialConditionRule("fuel","combustible",0,1,e=>e.Has("combustible")),
            new InitialConditionRule("load.capacity","structural",.3,1.5,e=>e.Has("structural")),
            new InitialConditionRule("wind.rating","structural&exposed",15,80,e=>e.Has("structural")&&e.Has("exposed")),
            new InitialConditionRule("medical.vulnerability","npc",0,.1,e=>e.Has("npc")),
            new InitialConditionRule("mobility.base","npc",.3,1,e=>e.Has("npc")),
            new InitialConditionRule("rain","@world",0,1),new InitialConditionRule("wind","@world",0,35),
            new InitialConditionRule("ambient.temperature","@world",5,40),new InitialConditionRule("rate_multiplier","@world",0,4)
        };
        public static IReadOnlyList<InitialConditionRule> Rules=>Array.AsReadOnly(rules);
        public string ApplicabilityFingerprint {get;private set;}
        public string ProviderBindingFingerprint {get;private set;}
        public string ContentSha256 {get;private set;}
        readonly InitialConditionPrior[] priors;
        public IReadOnlyList<InitialConditionPrior> Priors=>Array.AsReadOnly(priors);
        InitialConditionMaterial(string applicability,string binding,InitialConditionPrior[] values)
        {ApplicabilityFingerprint=applicability;ProviderBindingFingerprint=binding;priors=values;ContentSha256=Stable.Hash(Payload().ToString(Formatting.None));}
        static void Require(bool value,string reason){if(!value)throw new ArgumentException(reason);}
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        static bool Hash(string value)=>value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
        public static string FloatBits(double value)
        {Require(Finite(value),"nonfinite_material_bound");return "f64:"+unchecked((ulong)BitConverter.DoubleToInt64Bits(value)).ToString("x16",CultureInfo.InvariantCulture);}
        static double ParseBits(JToken token)
        {
            string value=token?.Type==JTokenType.String?(string)token:null;ulong bits;
            Require(value!=null&&value.Length==20&&value.StartsWith("f64:",StringComparison.Ordinal),"invalid_material_bound_encoding");
            Require(ulong.TryParse(value.Substring(4),NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out bits),"invalid_material_bound_encoding");
            double number=BitConverter.Int64BitsToDouble(unchecked((long)bits));Require(Finite(number),"nonfinite_material_bound");return number;
        }
        static bool DynamicPose(World world,Entity e)
        {
            var seen=new HashSet<string>(StringComparer.Ordinal);
            for(var current=e;current!=null;)
            {Require(seen.Add(current.Id),"material_parent_cycle");if(current.Has("npc"))return true;Entity next;if(string.IsNullOrEmpty(current.ParentId)||!world.Entities.TryGetValue(current.ParentId,out next))break;current=next;}
            return false;
        }
        /// <summary>All identities, capability/state schemas, links, static geometry and epoch metadata.
        /// Seed-dependent NPC poses and upstream numeric samples are deliberately not cache identity.
        /// Missing/source changes, including inactive flags, are not silently ignored.</summary>
        public static string Applicability(World world)
        {
            Require(world!=null&&world.Tick==0,"material_requires_initial_world");
            var entities=new JArray();
            foreach(var e in world.Entities.Values)
            {
                Require(e!=null&&!string.IsNullOrEmpty(e.Id)&&world.Entities[e.Id]==e,"invalid_material_entity_identity");
                bool moving=DynamicPose(world,e)&&!e.Has("source-input");
                var immutableState=new JObject();
                foreach(var state in e.State.Where(s=>s.Key.StartsWith("source.",StringComparison.Ordinal)))immutableState[state.Key]=FloatBits(state.Value);
                entities.Add(new JArray(e.Id,e.ParentId,e.Kind,new JArray(e.Tags),new JArray(e.Links),new JArray(e.State.Keys),
                    moving?JValue.CreateNull():(JToken)new JArray(FloatBits(e.X),FloatBits(e.Y),FloatBits(e.Z)),immutableState));
            }
            var epoch=new JObject();
            foreach(var field in world.Fields.Where(f=>f.Key.StartsWith("source.",StringComparison.Ordinal)||f.Key.StartsWith("inference.",StringComparison.Ordinal)||f.Key.StartsWith("mechanics.catalog.",StringComparison.Ordinal)||f.Key.StartsWith("response.catalog.",StringComparison.Ordinal)))epoch[field.Key]=FloatBits(field.Value);
            return Stable.Hash(new JObject{["schema"]=Schema,["engine"]=World.EngineVersion,["rng"]=Stable.RandomVersion,["topology"]=world.TopologyVersion,["step"]=FloatBits(world.StepSeconds),["entities"]=entities,["epoch"]=epoch}.ToString(Formatting.None));
        }
        public static bool ValidatePrior(World world,InitialConditionPrior p,out string reason)
        {
            reason="";Entity e=null;
            if(world==null||world.Tick!=0){reason="material_requires_initial_world";return false;}
            if(p==null||string.IsNullOrEmpty(p.EntityId)||string.IsNullOrEmpty(p.Property)||!Finite(p.Lower)||!Finite(p.Upper)||p.Lower>p.Upper){reason="invalid_material_prior";return false;}
            if(p.EntityId!="@world"&&(!world.Entities.TryGetValue(p.EntityId,out e)||!CausalKernel.SemanticEntities(world).Any(x=>x.Id==e.Id)||e.Has("decoration")||e.Has("render-only"))){reason="unknown_or_untyped_target";return false;}
            var rule=rules.FirstOrDefault(r=>r.Property==p.Property&&r.Matches(e));
            if(rule==null||p.Lower<rule.Min||p.Upper>rule.Max){reason="unsupported_or_out_of_range_property";return false;}
            return true;
        }
        public static InitialConditionMaterial Create(World world,IEnumerable<InitialConditionPrior> values,string providerBindingFingerprint)
        {
            Require(world!=null&&world.Tick==0,"material_requires_initial_world");
            Require(values!=null&&Hash(providerBindingFingerprint),"material_requires_logical_provider_binding");
            var copy=values.Take(MaximumPriors+1).ToArray();Require(copy.Length<=MaximumPriors,"invalid_material_prior_count");
            var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in copy){string reason;Require(ValidatePrior(world,p,out reason),reason);Require(keys.Add(JsonConvert.SerializeObject(new[]{p.EntityId,p.Property})),"duplicate_material_prior");}
            return new InitialConditionMaterial(Applicability(world),providerBindingFingerprint,copy.OrderBy(x=>x.EntityId,StringComparer.Ordinal).ThenBy(x=>x.Property,StringComparer.Ordinal).ToArray());
        }
        JObject Payload()=>new JObject{["schema"]=Schema,["sampling"]=MaterializationVersion,["applicability"]=ApplicabilityFingerprint,["provider_binding"]=ProviderBindingFingerprint,
            ["priors"]=new JArray(priors.Select(p=>new JObject{["entity_id"]=p.EntityId,["property"]=p.Property,["lower"]=FloatBits(p.Lower),["upper"]=FloatBits(p.Upper)}))};
        public string Serialize(){var result=Payload();result["sha256"]=ContentSha256;return result.ToString(Formatting.None);}
        public static InitialConditionMaterial Parse(string json)
        {
            JObject value;using(var reader=new JsonTextReader(new StringReader(json??""))){value=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});Require(!reader.Read(),"trailing_material_content");}
            Require(value.Properties().All(p=>new[]{"schema","sampling","applicability","provider_binding","priors","sha256"}.Contains(p.Name)),"unknown_material_field");
            Require((string)value["schema"]==Schema&&(string)value["sampling"]==MaterializationVersion&&Hash((string)value["applicability"])&&Hash((string)value["provider_binding"]),"invalid_material_contract");
            var rows=value["priors"] as JArray;Require(rows!=null&&rows.Count<=MaximumPriors,"invalid_material_prior_count");
            var parsed=new List<InitialConditionPrior>();var unique=new HashSet<string>(StringComparer.Ordinal);
            foreach(var row in rows)
            {var r=row as JObject;Require(r!=null&&r.Properties().Count()==4&&r.Properties().All(p=>new[]{"entity_id","property","lower","upper"}.Contains(p.Name))&&r["entity_id"]?.Type==JTokenType.String&&r["property"]?.Type==JTokenType.String,"invalid_material_prior_schema");
                var p=new InitialConditionPrior((string)r["entity_id"],(string)r["property"],ParseBits(r["lower"]),ParseBits(r["upper"]));Require(p.Lower<=p.Upper&&unique.Add(JsonConvert.SerializeObject(new[]{p.EntityId,p.Property})),"invalid_or_duplicate_material_prior");parsed.Add(p);}
            var material=new InitialConditionMaterial((string)value["applicability"],(string)value["provider_binding"],parsed.OrderBy(p=>p.EntityId,StringComparer.Ordinal).ThenBy(p=>p.Property,StringComparer.Ordinal).ToArray());
            Require(material.ContentSha256==(string)value["sha256"],"material_content_hash_mismatch");return material;
        }
        public bool CanApply(World world,out string reason)
        {
            reason="";if(world==null||world.Tick!=0){reason="material_requires_initial_world";return false;}
            if(Applicability(world)!=ApplicabilityFingerprint){reason="material_epoch_or_capability_mismatch";return false;}
            foreach(var p in priors)if(!ValidatePrior(world,p,out reason))return false;
            return true;
        }
        public IReadOnlyList<InitialConditionValue> Sample(World world)
        {
            string reason;Require(CanApply(world,out reason),reason);
            return Array.AsReadOnly(priors.Select(p=>new InitialConditionValue(p.EntityId,p.Property,
                p.Lower==p.Upper?p.Lower:p.Lower+(p.Upper-p.Lower)*Stable.Unit(world.Seed,"initial-material:"+Schema+":"+ContentSha256+":"+p.Property,0,p.EntityId))).ToArray());
        }
    }
}
