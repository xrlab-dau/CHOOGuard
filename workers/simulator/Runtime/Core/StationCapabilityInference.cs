using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    public sealed class StationCapabilityReceipt
    {
        readonly JObject json;
        public int AddedEntities {get;private set;}
        public int ExistingEntities {get;private set;}
        public int AssemblyCount {get;private set;}
        public int UnknownObjectCount {get;private set;}
        public string ProfilesSha256 {get;private set;}
        public JObject Json=>(JObject)json.DeepClone();
        internal StationCapabilityReceipt(JObject value)
        {json=(JObject)value.DeepClone();AddedEntities=(int)value["addedEntities"];ExistingEntities=(int)value["existingEntities"];AssemblyCount=(int)value["assemblyCount"];UnknownObjectCount=(int)value["unknownObjectCount"];ProfilesSha256=(string)value["profilesSha256"];}
    }

    /// <summary>
    /// Explicit INFERRED_TRAINING assembly capabilities, independent of source inventory.
    /// Narrow name/ancestry evidence proposes a role; qualified bounds locate it. The
    /// profile supplies assumptions. Source transforms/materials/records remain unchanged.
    /// </summary>
    public sealed class StationCapabilityInference
    {
        public const string Version="station-capability-inference-v2";
        sealed class Profile
        {
            public string Id,Role,Assumption;public string[] Tags;public double Confidence;
            public readonly SortedDictionary<string,double> State=new SortedDictionary<string,double>(StringComparer.Ordinal);
            public readonly SortedDictionary<string,double[]> Variations=new SortedDictionary<string,double[]>(StringComparer.Ordinal);
        }
        sealed class Rule{public Profile Profile;public Regex Pattern,AssemblyPattern;public bool Ancestor;public string Group;}
        sealed class Assembly
        {
            public string Key,Owner,Zone;public Profile Profile;public ObjectBounds Bounds;
            public readonly SortedDictionary<string,WorldObjectRecord> Members=new SortedDictionary<string,WorldObjectRecord>(StringComparer.Ordinal);
        }
        readonly SortedDictionary<string,Profile> profiles=new SortedDictionary<string,Profile>(StringComparer.Ordinal);
        readonly Rule[] rules;readonly string publicSpacePath;readonly string[] unsupported;
        public string ProfilesSha256 {get;private set;}
        public StationCapabilityInference(string profilesJson)
        {
            if(string.IsNullOrWhiteSpace(profilesJson))throw new ArgumentException("Inference profiles required");
            JObject root;
            using(var reader=new JsonTextReader(new System.IO.StringReader(profilesJson)))
            {root=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});if(reader.Read())throw new ArgumentException("Trailing profile content");}
            Require((string)root["schema"]=="chooguard.station-capability-profiles.v1"&&(string)root["status"]=="INFERRED_TRAINING","explicit inferred-training profile schema required");
            ProfilesSha256=Stable.Hash(profilesJson);
            foreach(var row in (JArray)root["profiles"])
            {
                var p=new Profile{Id=(string)row["id"],Role=(string)row["role"],Assumption=(string)row["assumption"],Confidence=(double)row["confidence"],Tags=row["tags"].Select(x=>(string)x).ToArray()};
                Require(!string.IsNullOrWhiteSpace(p.Id)&&!profiles.ContainsKey(p.Id)&&!string.IsNullOrWhiteSpace(p.Role)&&!string.IsNullOrWhiteSpace(p.Assumption)&&Finite(p.Confidence)&&p.Confidence>=0&&p.Confidence<=1,"unique profile ID and finite confidence/assumption required");
                Require(p.Tags.Contains("semantic")&&p.Tags.Contains("inferred-training")&&!p.Tags.Contains("render-only")&&!p.Tags.Contains("decoration"),"derived semantic provenance required");
                Require(!p.Tags.Contains("electrical")||new[]{"electrical-device","power-network"}.Contains(p.Role),"opaque geometry cannot imply electrification");
                Require(!p.Tags.Contains("combustible")||((bool?)row["materialAssumption"]==true&&p.Role=="material-liner"),"combustion requires an explicit named material profile");
                Require(!p.Tags.Contains("power-source")||p.Role=="power-network","power sources must be explicit owned network profiles");
                Require(!p.Tags.Contains("water-source")||p.Role=="water-network","water sources must be explicit owned network profiles");
                Require(!p.Tags.Any(t=>new[]{"door","npc","moving-body","heat-source","battery","self-powered","walkable","place","exit","destination"}.Contains(t)),"profile cannot silently grant motion, operability, floor/exit, person or autonomous energy capabilities");
                foreach(var pair in (JObject)row["state"]){double value=(double)pair.Value;Require(Finite(value)&&Math.Abs(value)<=1e6,"bounded finite training parameter required");p.State.Add(pair.Key,value);}
                foreach(var pair in (JObject)(row["variations"]??new JObject()))
                {var range=pair.Value.Select(x=>(double)x).ToArray();Require(!new[]{"fire","fault","failed","leak","smoke"}.Contains(pair.Key)&&range.Length==2&&range.All(Finite)&&range[0]<=range[1]&&range.All(x=>Math.Abs(x)<=1e6),"bounded upstream seeded variation required; incident flags cannot be seeded");p.Variations.Add(pair.Key,range);}
                Require(p.State.All(x=>!new[]{"fire","fault","failed","leak","smoke"}.Contains(x.Key)||x.Value==0),"profiles cannot force an incident");
                profiles.Add(p.Id,p);
            }
            foreach(var required in new[]{"owned-power-network","owned-water-network","public-space-context"})Require(profiles.ContainsKey(required),"required owned network/context profile missing");
            rules=((JArray)root["rules"]).Select(row=>
            {
                var profileId=(string)row["profile"];Require(profiles.ContainsKey(profileId),"rule references missing profile");
                var rule=new Rule{Profile=profiles[profileId],Pattern=Pattern((string)row["pattern"]),Group=(string)row["group"],Ancestor=(bool?)row["ancestor"]??false};
                Require(new[]{"source","regex","shop","door-family"}.Contains(rule.Group),"unsupported grouping rule");
                if(rule.Group=="regex"){rule.AssemblyPattern=Pattern((string)row["assemblyPattern"]);Require(rule.AssemblyPattern.GetGroupNames().Contains("assembly"),"assembly capture required");}
                return rule;
            }).ToArray();
            publicSpacePath=(string)root["publicSpacePath"];Require(!string.IsNullOrWhiteSpace(publicSpacePath),"source context path required");unsupported=root["unsupported"].Select(x=>(string)x).ToArray();
        }

        public StationCapabilityReceipt Apply(World world,WorldObjectRegistry registry)
        {
            if(world==null||registry==null)throw new ArgumentNullException("world/registry");
            Require(registry.Objects.Keys.All(world.Entities.ContainsKey),"apply complete source inventory before capability inference");
            var groups=new SortedDictionary<string,Assembly>(StringComparer.Ordinal);var unknown=new JArray();var assigned=new HashSet<string>(StringComparer.Ordinal);int inactive=0,typed=0;
            foreach(var row in registry.Objects.Values.OrderBy(o=>o.Id,StringComparer.Ordinal))
            {
                if(!row.ActiveInHierarchy){inactive++;continue;}
                if(registry.PhysicalIdentityOf(row.Id)!=row.Id){typed++;continue;}
                if(!HasGeometry(row)){unknown.Add(Unknown(row,"metadata-only-source-object-no-independent-cause"));continue;}
                Rule selected=null;WorldObjectRecord owner=null;
                for(var ancestor=row;ancestor!=null;ancestor=ancestor.ParentId==null?null:registry.Objects[ancestor.ParentId])
                {selected=rules.FirstOrDefault(r=>r.Ancestor&&r.Pattern.IsMatch(ancestor.Name));if(selected!=null){owner=ancestor;break;}}
                if(selected==null){selected=rules.FirstOrDefault(r=>!r.Ancestor&&r.Pattern.IsMatch(row.Name));owner=row;}
                if(selected==null){unknown.Add(Unknown(row,"no-qualified-role-profile"));continue;}
                string key=selected.Profile.Role+"|"+GroupKey(selected,owner,registry);
                if(!groups.TryGetValue(key,out var assembly))
                {assembly=new Assembly{Key=key,Owner=owner.Id,Zone=Zone(owner,registry),Profile=selected.Profile};groups.Add(key,assembly);}
                assembly.Members.Add(row.Id,row);assembly.Bounds=Union(assembly.Bounds,QualifiedBounds(row));assigned.Add(row.Id);
            }
            var context=registry.Objects.Values.SingleOrDefault(o=>o.ActiveInHierarchy&&o.Path==publicSpacePath);
            if(context!=null)
            {
                var assembly=new Assembly{Key="public-space|"+context.Id,Owner=context.Id,Zone=context.Id,Profile=profiles["public-space-context"]};
                foreach(var row in registry.Objects.Values.Where(o=>o.ActiveInHierarchy&&HasGeometry(o)&&IsDescendant(o,context.Id,registry)))
                {assembly.Members.Add(row.Id,row);assembly.Bounds=Union(assembly.Bounds,QualifiedBounds(row));}
                if(assembly.Bounds!=null)groups.Add(assembly.Key,assembly);
            }
            var unresolved=groups.Values.Where(g=>g.Bounds==null).ToArray();
            foreach(var group in unresolved){groups.Remove(group.Key);foreach(var row in group.Members.Values)unknown.Add(Unknown(row,"role-candidate-without-qualified-geometry-bounds"));}
            var pending=new List<Entity>();var assemblyReport=new JArray();var doorReport=new JArray();
            foreach(var group in groups.Values)
            {
                var entity=Create(world,registry,group);pending.Add(entity);
                var evidence=new JObject{["id"]=entity.Id,["role"]=group.Profile.Role,["profile"]=group.Profile.Id,["status"]="INFERRED_TRAINING",["confidence"]=group.Profile.Confidence,["sourceOwnerId"]=group.Owner,["sourceObjectIds"]=new JArray(group.Members.Keys),["bounds"]=BoundsJson(group.Bounds),["assumption"]=group.Profile.Assumption};
                assemblyReport.Add(evidence);
                if(group.Profile.Role=="door")
                {
                    var leaves=group.Members.Values.Where(o=>IsExplicitLeaf(o.Name)&&QualifiedBounds(o)!=null).ToArray();
                    doorReport.Add(new JObject{["assemblyId"]=entity.Id,["geometryQualifiedSingleLeafId"]=leaves.Length==1?leaves[0].Id:null,["candidateLeafIds"]=new JArray(leaves.Select(l=>l.Id)),["operable"]=false,["mode"]="UNSUPPORTED_HINGE_PIVOT_AXIS_TRAVEL",["motionConfidence"]=0,["sourceBounds"]=leaves.Length==1?BoundsJson(QualifiedBounds(leaves[0])):null,["reason"]="Source names and AABBs do not qualify a moving leaf mechanism; no source-leaf tag or motion capability granted."});
                }
            }
            int power=AddNetworks(world,registry,groups,pending,"electrical-device","owned-power-network");
            int water=AddNetworks(world,registry,groups,pending,"water-utility","owned-water-network");
            // Spatial/thermal association is explicit and bounded. It is a conservative
            // geometry-envelope hypothesis, not proof of contact or thermal conduction.
            foreach(var group in groups.Values.Where(g=>g.Profile.Tags.Contains("thermal")))
            {
                var entity=pending.Single(e=>e.Id==EntityId(group.Key));
                foreach(var other in groups.Values.Where(g=>g.Key!=group.Key&&g.Zone==group.Zone&&g.Profile.Tags.Contains("thermal")&&g.Bounds.Intersects(group.Bounds.Expand(.1))))
                    entity.Links.Add(EntityId(other.Key));
            }
            var metadata=Metadata(registry);foreach(var pair in metadata)if(world.Fields.TryGetValue(pair.Key,out var value))Require(value==pair.Value,"stale source/profile/version hash");
            int present=metadata.Keys.Count(world.Fields.ContainsKey);Require(present==0||present==metadata.Count,"partial inference hash metadata");
            int existing=0;
            foreach(var entity in pending)
            {
                if(!world.Entities.TryGetValue(entity.Id,out var found))continue;
                Require(found.Kind==entity.Kind&&found.ParentId==entity.ParentId&&found.X==entity.X&&found.Y==entity.Y&&found.Z==entity.Z&&entity.Tags.All(found.Tags.Contains)&&entity.Links.All(found.Links.Contains),"derived entity collision or stale assembly evidence: "+entity.Id);existing++;
            }
            // No mutation precedes the graph, profile, metadata and every ID check.
            foreach(var pair in metadata)world.Fields[pair.Key]=pair.Value;
            foreach(var entity in pending)if(!world.Entities.ContainsKey(entity.Id))world.Add(entity);
            var receipt=new JObject{["schema"]="chooguard.station-capability-inference-receipt.v1",["status"]="INFERRED_TRAINING",["profilesSha256"]=ProfilesSha256,["sourceSnapshotSha256"]=registry.SnapshotSha256,["sourceSceneSha256"]=registry.SceneSha256,["sourceRegistryFingerprint"]=registry.Fingerprint,["version"]=Version,["sourceObjectCount"]=registry.Objects.Count,["sourceInputsPreserved"]=true,["inactiveObjectsExcluded"]=inactive,["typedAliasObjectsExcluded"]=typed,["assemblyCount"]=groups.Count,["ownedPowerNetworks"]=power,["ownedWaterNetworks"]=water,["addedEntities"]=pending.Count-existing,["existingEntities"]=existing,["unknownObjectCount"]=unknown.Count,["unknownObjects"]=unknown,["assemblies"]=assemblyReport,["ownedNetworks"]=new JArray(pending.Where(e=>e.Kind.EndsWith("-network",StringComparison.Ordinal)).Select(e=>new JObject{["id"]=e.Id,["kind"]=e.Kind,["sourceOwnerId"]=e.ParentId,["center"]=new JArray(e.X,e.Y,e.Z),["links"]=new JArray(e.Links),["status"]="INFERRED_TRAINING"})),["profileAssumptions"]=new JArray(profiles.Values.Select(p=>new JObject{["profile"]=p.Id,["assumption"]=p.Assumption,["confidence"]=p.Confidence})),["doorMotionCandidates"]=doorReport,["unsupported"]=new JArray(unsupported),["causalClaim"]="Profile-defined inferred assemblies and explicit owned supply relationships participate in the existing causal kernel. No forced incident, per-render-part hazard, source mesh/TRS/material mutation or as-built semantics claim."};
            receipt["sourceAttributionInventory"]=StationSemanticInventory.Build(registry,receipt);
            return new StationCapabilityReceipt(receipt);
        }

        int AddNetworks(World world,WorldObjectRegistry registry,SortedDictionary<string,Assembly> groups,List<Entity> entities,string role,string networkProfile)
        {
            int count=0;
            foreach(var zone in groups.Values.Where(g=>g.Profile.Role==role).GroupBy(g=>g.Zone,StringComparer.Ordinal).OrderBy(g=>g.Key,StringComparer.Ordinal))
            {
                var network=new Assembly{Key=profiles[networkProfile].Role+"|"+zone.Key,Owner=zone.First().Owner,Zone=zone.Key,Profile=profiles[networkProfile]};
                foreach(var assembly in zone){network.Bounds=Union(network.Bounds,assembly.Bounds);foreach(var member in assembly.Members)network.Members[member.Key]=member.Value;}
                var entity=Create(world,registry,network);entities.Add(entity);
                foreach(var assembly in zone){var target=entities.Single(e=>e.Id==EntityId(assembly.Key));target.Links.Add(entity.Id);entity.Links.Add(target.Id);}
                count++;
            }
            return count;
        }
        Entity Create(World world,WorldObjectRegistry registry,Assembly group)
        {
            var b=group.Bounds;var e=new Entity{Id=EntityId(group.Key),ParentId=group.Owner,Kind="inferred-station-"+group.Profile.Role,X=(b.MinX+b.MaxX)*.5,Y=(b.MinY+b.MaxY)*.5,Z=(b.MinZ+b.MaxZ)*.5};
            foreach(var tag in group.Profile.Tags)e.Tags.Add(tag);
            e.Tags.Add("inference-profile:"+group.Profile.Id);e.Tags.Add("inference-contract:"+Stable.Hash(Version+"|"+ProfilesSha256+"|"+registry.Fingerprint));
            foreach(var pair in group.Profile.State)e.Set(pair.Key,pair.Value);
            foreach(var pair in group.Profile.Variations)e.Set(pair.Key,pair.Value[0]+Stable.Unit(world.Seed,"station-capability:"+group.Profile.Id,0,e.Id,DrawIndex(pair.Key))*(pair.Value[1]-pair.Value[0]));
            if(e.State.ContainsKey("temperature"))e.Set("temperature",world.Fields.TryGetValue("ambient.temperature",out var ambient)?ambient:20);
            e.Set("inference.confidence",group.Profile.Confidence);e.Set("source.assembly.member-count",group.Members.Count);e.Set("source.geometry.bounds-qualified",1);e.Set("mechanism.qualified",0);
            foreach(var id in group.Members.Keys)e.Links.Add(id);
            return e;
        }
        static int DrawIndex(string key)=>unchecked((int)uint.Parse(Stable.Hash(key).Substring(0,8),NumberStyles.HexNumber,CultureInfo.InvariantCulture));
        public static string EntityId(string assemblyKey)=>"station:inferred:"+Stable.Hash(assemblyKey).Substring(0,32);
        SortedDictionary<string,double> Metadata(WorldObjectRegistry registry)
        {
            var result=new SortedDictionary<string,double>(StringComparer.Ordinal);
            foreach(var pair in new[]{new[]{"inference.station.profiles.sha256",ProfilesSha256},new[]{"inference.station.registry.sha256",registry.Fingerprint},new[]{"inference.station.version.sha256",Stable.Hash(Version)}})
                for(int i=0;i<8;i++)result.Add(pair[0]+".word"+i,uint.Parse(pair[1].Substring(i*8,8),NumberStyles.HexNumber,CultureInfo.InvariantCulture));
            return result;
        }
        static string GroupKey(Rule rule,WorldObjectRecord row,WorldObjectRegistry registry)
        {
            if(rule.Group=="source")return row.Id;
            if(rule.Group=="regex"){var match=rule.AssemblyPattern.Match(row.Name);Require(match.Success&&!string.IsNullOrEmpty(match.Groups["assembly"].Value),"assembly grouping pattern failed");return registry.SceneGuid+":"+match.Groups["assembly"].Value.ToUpperInvariant();}
            if(rule.Group=="door-family")return registry.SceneGuid+":"+Regex.Replace(row.Name,@"(?:_(?:GLASS|OPAQUE|METAL))?_(?:LEAF|FRAME|GLASS|PANEL)_[0-9]+$","",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant).ToUpperInvariant();
            var shop=Regex.Match(row.Name,@"(?:^|_)(A[0-9]{2})(?:[A-Z0-9]*_|_)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            if(shop.Success)return registry.SceneGuid+":shop:"+shop.Groups[1].Value.ToUpperInvariant();
            var retail=Regex.Match(row.Name,@"^(MAIN1F_RETAIL_NEW_[RD][0-9]+_P[0-9]+_S[0-9]+)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            return registry.SceneGuid+":"+(retail.Success?retail.Groups[1].Value:Regex.Replace(row.Name,@"_[0-9]+$","")).ToUpperInvariant();
        }
        static string Zone(WorldObjectRecord row,WorldObjectRegistry registry)
        {
            for(var node=row;node!=null;node=node.ParentId==null?null:registry.Objects[node.ParentId])
            {
                var shop=Regex.Match(node.Name,@"(?:^|_)(A[0-9]{2})(?:[A-Z0-9]*_|_)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);if(shop.Success)return registry.SceneGuid+":shop:"+shop.Groups[1].Value.ToUpperInvariant();
                var wc=Regex.Match(node.Name,@"^MAIN1F_NEW_WC_(EM|EW|WM|WW)_",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);if(wc.Success)return registry.SceneGuid+":wc:"+wc.Groups[1].Value.ToUpperInvariant();
                if(Regex.IsMatch(node.Name,@"^MAIN1F_(?:EV_|CENTRAL_ESC_|INDOOR_EAST_ESC_)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return node.Id;
            }
            return row.ParentId??row.Id;
        }
        static bool IsDescendant(WorldObjectRecord row,string ancestorId,WorldObjectRegistry registry)
        {for(var node=row;node!=null;node=node.ParentId==null?null:registry.Objects[node.ParentId])if(node.Id==ancestorId)return true;return false;}
        static bool HasGeometry(WorldObjectRecord row)=>row.Components.Any(c=>new[]{"MeshFilter","MeshRenderer","MeshCollider","BoxCollider","SphereCollider","CapsuleCollider"}.Contains(c.Type));
        static bool IsExplicitLeaf(string name)=>Regex.IsMatch(name,@"(?:_LEAF_[0-9]+$|_DOOR_GLASS_[0-9]+$|_STALLDOOR_[0-9]+$)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)&&!Regex.IsMatch(name,@"FRAME|WALL|SIGN",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        static ObjectBounds QualifiedBounds(WorldObjectRecord row)
        {
            if(!row.ActiveInHierarchy)return null;if(row.WorldBounds!=null)return row.WorldBounds;ObjectBounds result=null;
            // Disabled-collider serialized bounds still locate source visual geometry;
            // they do not grant collision, contact, visibility or navigation coverage.
            foreach(var component in row.Components)
            {
                var data=component.Data;var bounds=data["worldBounds"];if(bounds==null||bounds.Type==JTokenType.Null||!new[]{"serialized-mesh-aabb","serialized-box","serialized-sphere","serialized-capsule","native-collider-aabb"}.Contains((string)data["boundsEvidence"]))continue;
                var min=bounds["min"].Select(x=>(double)x).ToArray();var max=bounds["max"].Select(x=>(double)x).ToArray();
                if(min.Length!=3||max.Length!=3)continue;result=Union(result,new ObjectBounds(min[0],min[1],min[2],max[0],max[1],max[2]));
            }
            return result;
        }
        static ObjectBounds Union(ObjectBounds a,ObjectBounds b)=>b==null?a:a==null?b:new ObjectBounds(Math.Min(a.MinX,b.MinX),Math.Min(a.MinY,b.MinY),Math.Min(a.MinZ,b.MinZ),Math.Max(a.MaxX,b.MaxX),Math.Max(a.MaxY,b.MaxY),Math.Max(a.MaxZ,b.MaxZ));
        static JObject BoundsJson(ObjectBounds b)=>new JObject{["min"]=new JArray(b.MinX,b.MinY,b.MinZ),["max"]=new JArray(b.MaxX,b.MaxY,b.MaxZ)};
        static JObject Unknown(WorldObjectRecord row,string reason)=>new JObject{["sourceId"]=row.Id,["name"]=row.Name,["reason"]=reason,["capabilities"]="UNKNOWN"};
        static Regex Pattern(string value){Require(!string.IsNullOrWhiteSpace(value)&&value.Length<1024,"bounded rule expression required");return new Regex(value,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));}
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        static void Require(bool condition,string message){if(!condition)throw new ArgumentException("Station inference: "+message);}
    }
}
