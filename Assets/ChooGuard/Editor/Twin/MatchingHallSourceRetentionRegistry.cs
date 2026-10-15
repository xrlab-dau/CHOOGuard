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
    // Runs only after the verified stair/office and MAIN2F divider layers.
    public static class MatchingHallSourceRetentionRegistry
    {
        public const string Registry="content/world/observed-interiors/matchinghall-source-retention.json";
        public const string Source="content/world/observed-interiors/matchinghall-source-architecture-source.json";
        public const string WallSpec="content/world/observed-interiors/matchinghall-source-architecture.json";
        public const string Schema="chooguard.matchinghall-source-retention.v1";
        static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        public static JObject Read()
        {
            if(!File.Exists(Registry))return null;
            var p=JObject.Parse(File.ReadAllText(Registry));
            Require((string)p["schema"]==Schema&&(string)p["source"]==Source&&StairInfillOmissionRegistry.Hash(Source)==(string)p["source_sha256"],"Hall retention source changed");
            var source=JObject.Parse(File.ReadAllText(Source));
            Require((int)source["summary"]["exact_removed_render_triangles"]==432&&(int)source["summary"]["exact_removed_collider_triangles"]==48&&source["meshes"].Count()==7&&
                StairInfillOmissionRegistry.Hash((string)source["source_plan"])==(string)source["source_plan_sha256"],"Hall source plan or attribution changed");
            Require(File.Exists(WallSpec)&&StairInfillOmissionRegistry.Hash(WallSpec)==(string)source["supported_new_walls_sha256"]&&StairInfillOmissionRegistry.Hash((string)source["native_separation"])==(string)source["native_separation_sha256"],"Hall new-wall or native separation source changed");
            var rows=p["entries"] as JArray;
            Require(rows!=null&&rows.Count==7&&rows.Select(r=>(string)r["generation_source_asset"]).Distinct().Count()==7,"Exact seven hall retention aliases required");
            return p;
        }
        static JToken Row(JObject p,string source)=>p?["entries"]?.SingleOrDefault(r=>(string)r["generation_source_asset"]==source);
        public static string[] InstalledAssetsForRoot(string root)=>root.Trim('/')==StairInfillOmissionRegistry.Root&&Read() is JObject p?p["entries"].Select(r=>(string)r["replacement_asset"]).ToArray():Array.Empty<string>();
        public static Mesh RemoveExact(Mesh baseline,JToken proof)
        {
            Require(baseline!=null&&baseline.isReadable&&baseline.subMeshCount==1&&baseline.GetTopology(0)==MeshTopology.Triangles&&
                baseline.vertexCount==(int)proof["source_vertex_count"]&&baseline.triangles.Length/3==(int)proof["source_triangle_count"]&&
                ArchitecturalCorrectionRegistry.CompleteChannelDigest(baseline)==(string)proof["source_complete_channels_sha256"],"Hall source channels changed");
            var indices=baseline.GetTriangles(0);var removed=proof["remove_triangle_ordinals"].ToObject<int[]>();
            Require(removed.Length==(int)proof["removed_triangle_count"]&&removed.Distinct().Count()==removed.Length&&removed.All(i=>i>=0&&i<indices.Length/3)&&removed.Length==proof["remove_source_indices"].Count(),"Hall face selection changed");
            for(int i=0;i<removed.Length;i++)Require(indices.Skip(removed[i]*3).Take(3).SequenceEqual(proof["remove_source_indices"][i].ToObject<int[]>()),"Hall ordered triangle changed");
            var clone=UnityEngine.Object.Instantiate(baseline);
            try
            {
                Require(ArchitecturalCorrectionRegistry.CompleteChannelDigest(clone)==ArchitecturalCorrectionRegistry.CompleteChannelDigest(baseline),"Hall clone changed channels");
                var selected=removed.ToHashSet();clone.SetTriangles(Enumerable.Range(0,indices.Length/3).Where(i=>!selected.Contains(i)).SelectMany(i=>indices.Skip(i*3).Take(3)).ToArray(),0,false);
                Require(clone.indexFormat==baseline.indexFormat&&clone.GetVertexAttributes().SequenceEqual(baseline.GetVertexAttributes())&&clone.bounds==baseline.bounds&&
                    ArchitecturalCorrectionRegistry.CompleteChannelDigest(clone)==(string)proof["retained_complete_channels_sha256"],"Hall retention changed unrelated channels");
                clone.name=baseline.name+"_HallSourceArchitecture";clone.hideFlags=HideFlags.None;return clone;
            }
            catch{UnityEngine.Object.DestroyImmediate(clone);throw;}
        }
        public static Mesh ResolveComposed(Mesh generated,string source)
        {
            var p=Read();var row=Row(p,source);if(row==null)return generated;
            var baseline=StairInfillOmissionRegistry.CheckedMesh(row,"baseline");var target=StairInfillOmissionRegistry.CheckedMesh(row,"replacement");
            var proof=JObject.Parse(File.ReadAllText(Source))["meshes"].Single(r=>(string)r["role"]==(string)row["role"]);
            Require((string)proof["source_asset"]==(string)row["baseline_asset"]&&(string)proof["retained_complete_channels_sha256"]==(string)row["replacement_complete_digest"],"Hall layer baseline changed");
            var check=RemoveExact(baseline,proof);
            try{Require(ArchitecturalCorrectionRegistry.CompleteChannelDigest(check)==ArchitecturalCorrectionRegistry.CompleteChannelDigest(target),"Hall retained asset differs from its exact source partition");}
            finally{UnityEngine.Object.DestroyImmediate(check);}
            string digest=generated==null?null:ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated);
            Require(digest==(string)row["baseline_complete_digest"]||digest==(string)row["replacement_complete_digest"],"Unreviewed pre-hall composition");return target;
        }
        // The unchanged original Room recipes are regenerated for strict source identity.
        // Their five real lights follow the same exact scoped withholding as their old ceilings.
        public static void ApplyLightPolicy(Light light)
        {
            if(light==null||!File.Exists(Registry))return;
            string path=StairInfillOmissionRegistry.PathOf(light.transform);
            var source=JObject.Parse(File.ReadAllText(Source));var matches=source["old_ceiling_lights_to_disable"].Where(r=>(string)r["path"]==path).ToArray();
            if(matches.Length==0)return;Require(matches.Length==1,"Ambiguous withheld hall light");Read();light.enabled=false;
        }
        public static bool HasNewWallCorrection(string asset)=>Read() is JObject p&&p["new_wall_meshes"].Any(r=>(string)r["asset"]==asset);
        static Mesh CheckedNewWallMesh(JToken row)
        {
            string path=(string)row["asset"];var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Require(mesh!=null&&!EditorUtility.IsDirty(mesh)&&AssetDatabase.AssetPathToGUID(path)==(string)row["asset_guid"]&&
                StairInfillOmissionRegistry.Hash(path)==(string)row["asset_file_sha256"]&&StairInfillOmissionRegistry.Hash(path+".meta")== (string)row["asset_meta_sha256"]&&
                ArchitecturalCorrectionRegistry.CompleteChannelDigest(mesh)==(string)row["complete_channel_digest"],"Source wall persistent channels changed");return mesh;
        }
        public static Mesh ResolveNewWallForRebuild(Mesh generated,string asset,string owner,Matrix4x4 frame)
        {
            var p=Read();if(p==null)return generated;var rows=p["new_wall_meshes"].Where(r=>(string)r["asset"]==asset).ToArray();if(rows.Length==0)return generated;
            Require(rows.Length==1,"Ambiguous new wall rebuild source");var row=rows[0];var target=CheckedNewWallMesh(row);
            Require((string)row["owner_path"]==owner&&generated!=null&&generated.indexFormat==target.indexFormat&&generated.GetVertexAttributes().SequenceEqual(target.GetVertexAttributes())&&
                ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated)==(string)row["complete_channel_digest"],"Fresh new wall source geometry or channels changed");
            StairInfillOmissionRegistry.Frame(frame,row["world_matrix"]);return target;
        }
        public static void PreflightWallRebuild(string specPath,string owner)
        {
            if(owner.Trim('/')!="실내 트윈 마감/2F 원도 맞이방 구조"||!File.Exists(Registry))return;
            Require(Path.GetFullPath(specPath)==Path.GetFullPath(WallSpec),"Exact pinned hall wall spec required");Read();VerifyInstalled();
        }
        static string ExpectedSettings(JToken row,string key,bool retired)
        {
            string expected=(string)row[key];if(!retired)return expected;
            var json=JObject.Parse(expected);Require(json.Count==1&&json.Properties().Single().Value is JObject,"Typed retired settings required");
            var fields=(JObject)json.Properties().Single().Value;
            if(fields["m_Enabled"]!=null){Require((bool)fields["m_Enabled"],"Original source enabled contract changed");fields["m_Enabled"]=false;}
            return json.ToString(Newtonsoft.Json.Formatting.None);
        }
        public static void VerifyInstalled()
        {
            var p=Read();if(p==null)return;
            var scene=SceneManager.GetActiveScene();var components=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c is MeshFilter||c is MeshCollider).ToArray();
            foreach(var row in p["entries"])
            {
                var owners=components.Where(c=>StairInfillOmissionRegistry.PathOf(c.transform)==(string)row["owner_path"]&&c.GetType().Name==(string)row["owner_kind"]).ToArray();
                Require(owners.Length==1,"Missing or ambiguous hall retained owner");var owner=owners[0];StairInfillOmissionRegistry.Frame(owner.transform.localToWorldMatrix,row["world_matrix"]);
                var mesh=owner is MeshFilter mf?mf.sharedMesh:((MeshCollider)owner).sharedMesh;
                Require(mesh==Main2fSourceCompletionRegistry.ResolveComposed(StairInfillOmissionRegistry.CheckedMesh(row,"replacement"),(string)row["generation_source_asset"])&&StairInfillOmissionRegistry.Settings(owner)==(string)row["owner_settings"],"Hall retained binding or physical settings changed");
            }
            var newRows=p["new_wall_meshes"] as JArray;Require(newRows!=null&&newRows.Count==2&&newRows.Select(r=>(string)r["asset"]).Distinct().Count()==2,"Exact new wall render and collision sources required");
            foreach(var row in newRows)
            {
                var owners=components.Where(c=>StairInfillOmissionRegistry.PathOf(c.transform)==(string)row["owner_path"]&&c.GetType().Name==(string)row["owner_kind"]).ToArray();Require(owners.Length==1,"Source wall owner missing or duplicated");
                var owner=owners[0];StairInfillOmissionRegistry.Frame(owner.transform.localToWorldMatrix,row["world_matrix"]);var mesh=owner is MeshFilter f?f.sharedMesh:((MeshCollider)owner).sharedMesh;
                bool retired=OfficialFloorplanArchitectureRegistry.VerifyRetiredSourceComponent(owner);
                Require(owner.gameObject.activeInHierarchy&&mesh==CheckedNewWallMesh(row)&&StairInfillOmissionRegistry.Settings(owner)==ExpectedSettings(row,"owner_settings",retired)&&
                    owner.gameObject.layer==(int)row["layer"]&&GameObjectUtility.GetStaticEditorFlags(owner.gameObject)==(StaticEditorFlags)(int)row["static_flags"],"Source wall binding or physical settings changed");
                if(owner is MeshFilter)
                {
                    var renderer=owner.GetComponent<MeshRenderer>();Require(renderer!=null&&renderer.enabled==!retired&&StairInfillOmissionRegistry.Settings(renderer)==ExpectedSettings(row,"renderer_settings",retired),"Source wall renderer settings changed");
                    var materials=row["materials"] as JArray;Require(materials!=null&&renderer.sharedMaterials.Length==materials.Count,"Source wall material slots changed");
                    for(int i=0;i<materials.Count;i++){var material=renderer.sharedMaterials[i];string path=AssetDatabase.GetAssetPath(material);Require(material!=null&&!EditorUtility.IsDirty(material)&&path==(string)materials[i]["asset"]&&AssetDatabase.AssetPathToGUID(path)==(string)materials[i]["guid"]&&StairInfillOmissionRegistry.Hash(path)==(string)materials[i]["file_sha256"]&&StairInfillOmissionRegistry.Hash(path+".meta")== (string)materials[i]["meta_sha256"],"Source wall material or slot order changed");}
                }
            }
            var source=JObject.Parse(File.ReadAllText(Source));var lights=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).ToArray();
            foreach(var row in source["old_ceiling_lights_to_disable"]){var hits=lights.Where(l=>StairInfillOmissionRegistry.PathOf(l.transform)==(string)row["path"]).ToArray();Require(hits.Length==1&&!hits[0].enabled,"Withheld hall ceiling light regenerated");}
        }
        public static string Verify(string[] args){Require(args==null||args.Length==0,"No override arguments");VerifyInstalled();return "MATCHINGHALL_SOURCE_RETENTION_VERIFIED 432 render +48 collider faces, exact five old lights withheld; source wall runtime checked separately";}
    }
}
#endif
