using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    // Coverage is a ledger of evidence and missing contracts, never a capability grant.
    public static class StationSemanticInventory
    {
        public const string Version="source-semantic-attribution-v1";
        public static JObject Build(WorldObjectRegistry registry,JObject inference)
        {
            if(registry==null||inference==null||(string)inference["sourceRegistryFingerprint"]!=registry.Fingerprint)throw new ArgumentException("Exact inference/registry epoch required");
            var associations=new Dictionary<string,List<JObject>>(StringComparer.Ordinal);
            foreach(var assembly in ((JArray)inference["assemblies"]).OfType<JObject>().Where(a=>(string)a["role"]!="public-space"))
                foreach(string id in assembly["sourceObjectIds"].Values<string>())
                {if(!registry.Objects.ContainsKey(id))throw new ArgumentException("Unknown semantic source binding");if(!associations.TryGetValue(id,out var items))associations[id]=items=new List<JObject>();items.Add(assembly);}
            var rows=new JArray();var counts=new SortedDictionary<string,int>(StringComparer.Ordinal);
            foreach(var source in registry.Objects.Values.OrderBy(s=>s.Id,StringComparer.Ordinal))
            {
                bool geometry=source.Components.Any(c=>new[]{"MeshFilter","MeshRenderer","MeshCollider","BoxCollider","SphereCollider","CapsuleCollider"}.Contains(c.Type));
                string alias=registry.PhysicalIdentityOf(source.Id);associations.TryGetValue(source.Id,out var bindings);
                string status=!source.ActiveInHierarchy?"INACTIVE_SOURCE":alias!=source.Id?"EXISTING_TYPED_ALIAS":!geometry?"KNOWN_NONCAUSAL_METADATA":bindings!=null?"SUPPORTED_INFERRED_ASSEMBLY":"UNRESOLVED_CAPABILITIES";
                counts[status]=counts.TryGetValue(status,out var n)?n+1:1;
                rows.Add(new JObject{["sourceId"]=source.Id,["name"]=source.Name,["sourcePath"]=source.Path,["sourceParentId"]=source.ParentId,["status"]=status,["physicalIdentity"]=alias,["candidateCategory"]=Category(source,geometry),
                    ["semanticBindings"]=new JArray((bindings??new List<JObject>()).Select(a=>new JObject{["assemblyId"]=a["id"],["role"]=a["role"],["profile"]=a["profile"],["evidenceOwnerId"]=a["sourceOwnerId"],["status"]="INFERRED_TRAINING",["confidence"]=a["confidence"],["assumption"]=a["assumption"]})),
                    ["evidence"]=new JObject{["sourceSceneSha256"]=registry.SceneSha256,["sourceInventorySha256"]=registry.SnapshotSha256,["immutableSourceNamesAndAncestry"]=true,["geometryComponentsPresent"]=geometry,["physicalBoundsQualified"]=source.BoundsSupported,["physicalEligible"]=source.PhysicalEligible},
                    ["missingContracts"]=Missing(source,status,bindings),["probabilityMultiplicityPolicy"]="One semantic assembly mechanism; metadata and render parts add no independent event draw."});
            }
            return new JObject{["schema"]=Version,["sourceSceneSha256"]=registry.SceneSha256,["sourceRegistryFingerprint"]=registry.Fingerprint,["objectCount"]=rows.Count,["counts"]=JObject.FromObject(counts),["objects"]=rows,["claim"]="Role/coverage provenance only. Supported inferred status is not as-built capability, operability, all-domain completion or field effectiveness."};
        }
        static string Category(WorldObjectRecord source,bool geometry)
        {
            if(!geometry)return "metadata_or_hierarchy";string name=source.Name,path=source.Path;
            if(path.Contains("디딤판")||name.StartsWith("esc-well-",StringComparison.Ordinal))return "moving_tread_system_candidate";
            if(name.IndexOf("Rotor",StringComparison.OrdinalIgnoreCase)>=0||path.IndexOf("PairedRotor",StringComparison.OrdinalIgnoreCase)>=0)return "revolving_door_candidate";
            if(name.StartsWith("MAIN1F_NEW_SF_",StringComparison.Ordinal))return "shop_fixture_candidate";
            if(path.Contains("RetailGlazing")||Regex.IsMatch(name,@"GLASS|GLAZING|MULLION|JAMB|SIDELIGHT",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return "glazing_frame_or_barrier_candidate";
            if(Regex.IsMatch(name,@"COLUMN|WALL|CEILING|SOFFIT|STAIR|TREAD",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return "architecture_or_structure_candidate";
            if(name.StartsWith("Official",StringComparison.Ordinal))return "mixed_official_mesh_requires_partition_semantics";
            return "unqualified_source_geometry";
        }
        static JArray Missing(WorldObjectRecord source,string status,List<JObject> bindings)
        {
            if(status=="KNOWN_NONCAUSAL_METADATA"||status=="INACTIVE_SOURCE")return new JArray("no independent hazard grant; source context retained");
            if(status=="EXISTING_TYPED_ALIAS")return new JArray("refer to the existing typed entity contract; no duplicated causal mechanism");
            if(bindings==null)return new JArray("source-qualified semantic assembly and relevance","material and utility evidence","motion/contact/navigation qualification where applicable");
            return new JArray("as-built material/strength/calibrated failure rates","qualified mechanism/contact/physical observations where applicable","site SOP and field effectiveness validation");
        }
    }
}
