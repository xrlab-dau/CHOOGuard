using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    // Expected identities come from immutable inventory, never from the observed
    // runtime count. Generated meshes cannot substitute for a missing source row.
    public sealed class SnapshotMeshContract
    {
        public string SourceEpoch {get;private set;}
        public string TrainRootId {get;private set;}
        public IReadOnlyList<string> ExpectedMeshSourceIds {get;private set;}
        public SnapshotMeshContract(WorldObjectRegistry registry,string trainRootId)
        {
            if(registry==null||!registry.Objects.ContainsKey(trainRootId))throw new ArgumentException("Immutable registered train root required");
            SourceEpoch=registry.SceneSha256;TrainRootId=trainRootId;
            ExpectedMeshSourceIds=Array.AsReadOnly(registry.Objects.Values.Where(o=>Below(o,trainRootId,registry)&&o.Components.Any(c=>c.Type=="MeshFilter"&&c.Data["mesh"] is JObject mesh&&!string.IsNullOrEmpty((string)mesh["fileId"])&&(string)mesh["fileId"]!="0")).Select(o=>o.Id).OrderBy(id=>id,StringComparer.Ordinal).ToArray());
            if(ExpectedMeshSourceIds.Count==0)throw new ArgumentException("Train inventory has no qualified mesh references");
        }
        static bool Below(WorldObjectRecord source,string root,WorldObjectRegistry registry)
        {for(var node=source;node!=null;node=node.ParentId==null?null:registry.Objects[node.ParentId])if(node.Id==root)return true;return false;}
        public JObject Compare(IEnumerable<string> observedSourceMeshIds)
        {
            if(observedSourceMeshIds==null)throw new ArgumentNullException(nameof(observedSourceMeshIds));
            var observed=observedSourceMeshIds.ToArray();var seen=new HashSet<string>(observed,StringComparer.Ordinal);var expected=new HashSet<string>(ExpectedMeshSourceIds,StringComparer.Ordinal);
            var missing=expected.Except(seen,StringComparer.Ordinal).OrderBy(id=>id,StringComparer.Ordinal).ToArray();var unexpected=seen.Except(expected,StringComparer.Ordinal).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
            var repeated=observed.GroupBy(id=>id,StringComparer.Ordinal).Where(g=>g.Count()>1).Select(g=>g.Key).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
            return new JObject{["source_epoch"]=SourceEpoch,["train_root_source_id"]=TrainRootId,["expected_mesh_source_count"]=expected.Count,["observed_mesh_source_count"]=observed.Length,["missing_source_mesh_ids"]=new JArray(missing),["unexpected_source_mesh_ids"]=new JArray(unexpected),["repeated_source_mesh_ids"]=new JArray(repeated),["passed"]=missing.Length==0&&unexpected.Length==0&&repeated.Length==0};
        }
    }
}
