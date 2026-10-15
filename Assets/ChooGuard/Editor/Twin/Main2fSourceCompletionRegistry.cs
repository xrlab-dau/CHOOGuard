#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor.Twin
{
    // Last layer: stair omission -> original MAIN18 -> Hall480 -> MAIN64.
    // The earlier reviewed source layers and their byte-pinned baselines remain intact.
    public static class Main2fSourceCompletionRegistry
    {
        public const string Registry="content/world/observed-interiors/main2f-source-completion-retention.json";
        public const string Source="content/world/observed-interiors/main2f-source-completion-source.json";
        public const string Schema="chooguard.main2f-source-completion-retention.v1";
        static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        public static JObject Read()
        {
            if(!File.Exists(Registry))return null;
            var p=JObject.Parse(File.ReadAllText(Registry));
            Require((string)p["schema"]==Schema&&(string)p["source"]==Source&&StairInfillOmissionRegistry.Hash(Source)==(string)p["source_sha256"],"MAIN64 source changed");
            var s=JObject.Parse(File.ReadAllText(Source));
            Require((int)s["released_triangles"]==64&&s["meshes"].Count()==3&&StairInfillOmissionRegistry.Hash((string)s["source_plan"])==(string)s["source_plan_sha256"],"MAIN64 exact plan/selection changed");
            Require(StairInfillOmissionRegistry.Hash((string)s["prior_composition_registry"])==(string)s["prior_composition_registry_sha256"],"Prior Hall composition changed");
            Require(p["entries"].Count()==3&&p["entries"].Select(r=>(string)r["generation_source_asset"]).Distinct().Count()==3,"Exact three MAIN64 aliases required");
            return p;
        }
        public static string[] InstalledAssetsForRoot(string root)=>root.Trim('/')==StairInfillOmissionRegistry.Root&&Read() is JObject p?p["entries"].Select(r=>(string)r["replacement_asset"]).ToArray():Array.Empty<string>();
        public static Mesh RemoveExact(Mesh source,JToken proof)
        {
            Require(source!=null&&source.isReadable&&source.subMeshCount==1&&source.GetTopology(0)==MeshTopology.Triangles&&source.vertexCount==(int)proof["source_vertex_count"]&&source.triangles.Length/3==(int)proof["source_triangle_count"]&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(source)==(string)proof["source_complete_channels_sha256"],"MAIN64 baseline changed");
            int[] indices=source.GetTriangles(0),removed=proof["remove_triangle_ordinals"].ToObject<int[]>();
            Require(removed.Length==(int)proof["removed_triangle_count"]&&removed.Distinct().Count()==removed.Length&&removed.All(i=>i>=0&&i<indices.Length/3)&&removed.Length==proof["remove_source_indices"].Count(),"MAIN64 ordered selection invalid");
            for(int k=0;k<removed.Length;k++)Require(indices.Skip(removed[k]*3).Take(3).SequenceEqual(proof["remove_source_indices"][k].ToObject<int[]>()),"MAIN64 triangle changed");
            var clone=UnityEngine.Object.Instantiate(source);
            try
            {
                Require(ArchitecturalCorrectionRegistry.CompleteChannelDigest(clone)==ArchitecturalCorrectionRegistry.CompleteChannelDigest(source),"MAIN64 clone channels changed");
                var selected=removed.ToHashSet();clone.SetTriangles(Enumerable.Range(0,indices.Length/3).Where(i=>!selected.Contains(i)).SelectMany(i=>indices.Skip(i*3).Take(3)).ToArray(),0,false);
                Require(clone.vertexCount==source.vertexCount&&clone.indexFormat==source.indexFormat&&clone.GetVertexAttributes().SequenceEqual(source.GetVertexAttributes())&&clone.bounds==source.bounds&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(clone)==(string)proof["retained_complete_channels_sha256"],"MAIN64 unrelated channels changed");
                clone.name=source.name+"_MainSourceCompletion";clone.hideFlags=HideFlags.None;return clone;
            }
            catch{UnityEngine.Object.DestroyImmediate(clone);throw;}
        }
        public static Mesh ResolveComposed(Mesh generated,string source)
        {
            var p=Read();var row=p?["entries"]?.SingleOrDefault(r=>(string)r["generation_source_asset"]==source);if(row==null)return generated;
            var baseline=StairInfillOmissionRegistry.CheckedMesh(row,"baseline");var target=StairInfillOmissionRegistry.CheckedMesh(row,"replacement");
            var proof=JObject.Parse(File.ReadAllText(Source))["meshes"].Single(r=>(string)r["role"]==(string)row["role"]);
            Require((string)proof["source_asset"]==(string)row["baseline_asset"]&&(string)proof["source_complete_channels_sha256"]==(string)row["baseline_complete_digest"]&&(string)proof["retained_complete_channels_sha256"]==(string)row["replacement_complete_digest"],"MAIN64 retained binding changed");
            var expected=RemoveExact(baseline,proof);try{Require(ArchitecturalCorrectionRegistry.CompleteChannelDigest(expected)==ArchitecturalCorrectionRegistry.CompleteChannelDigest(target),"MAIN64 retention changed");}finally{UnityEngine.Object.DestroyImmediate(expected);}
            string digest=generated==null?null:ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated);Require(digest==(string)row["baseline_complete_digest"]||digest==(string)row["replacement_complete_digest"],"Unreviewed pre-MAIN64 composition");return target;
        }
        public static void VerifyInstalled()
        {
            var p=Read();if(p==null)return;
            var components=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c is MeshFilter||c is MeshCollider).ToArray();
            foreach(var row in p["entries"])
            {
                var owners=components.Where(c=>StairInfillOmissionRegistry.PathOf(c.transform)==(string)row["owner_path"]&&c.GetType().Name==(string)row["owner_kind"]).ToArray();Require(owners.Length==1,"MAIN64 owner missing or ambiguous");
                var c=owners[0];StairInfillOmissionRegistry.Frame(c.transform.localToWorldMatrix,row["world_matrix"]);var actual=c is MeshFilter f?f.sharedMesh:((MeshCollider)c).sharedMesh;
                Require(actual==ResolveComposed(StairInfillOmissionRegistry.CheckedMesh(row,"baseline"),(string)row["generation_source_asset"])&&StairInfillOmissionRegistry.Settings(c)==(string)row["owner_settings"],"MAIN64 owner/settings changed");
            }
        }
    }
}
#endif
