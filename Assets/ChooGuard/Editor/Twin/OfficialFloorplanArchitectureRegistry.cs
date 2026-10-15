#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor.Twin
{
    // Preserve explicitly reviewed drawing geometry and exact legacy retirement decisions.
    // Rebuilds cannot silently reinstate a known misplaced wall.
    public static class OfficialFloorplanArchitectureRegistry
    {
        public const string Registry="content/world/observed-interiors/official-floorplan-installed-architecture.json";
        static void Need(bool value,string message){if(!value)throw new InvalidDataException(message);}
        public static string Hash(string path){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        public static string MeshHash(Mesh mesh)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s))
            {
                var v=mesh.vertices;var t=mesh.triangles;var n=mesh.normals;w.Write(v.Length);w.Write(t.Length);w.Write(n.Length);
                foreach(var p in v){w.Write(p.x);w.Write(p.y);w.Write(p.z);}foreach(var i in t)w.Write(i);foreach(var p in n){w.Write(p.x);w.Write(p.y);w.Write(p.z);}w.Flush();
                using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        public static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
        static JObject Read()=>File.Exists(Registry)?JObject.Parse(File.ReadAllText(Registry)):null;
        static MeshFilter Find(Transform root,string path)
        {
            var matches=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>PathOf(f.transform)==path).ToArray();
            Need(matches.Length==1,"Exact floorplan owner absent or ambiguous: "+path);return matches[0];
        }
        static void VerifyOwner(MeshFilter f,JToken pin)
        {
            var m=f.sharedMesh;Need(m!=null&&MeshHash(m)==(string)pin["mesh_payload_sha256"],"Reviewed floorplan geometry changed: "+PathOf(f.transform));
            for(int r=0;r<4;r++)for(int c=0;c<4;c++)Need(Mathf.Abs(f.transform.localToWorldMatrix[r,c]-(float)pin["world_matrix"][r][c])<.00005f,"Reviewed floorplan pose changed");
        }
        static MeshCollider FindCollider(Transform root,string path)
        {
            var matches=root.GetComponentsInChildren<MeshCollider>(true).Where(c=>PathOf(c.transform)==path).ToArray();Need(matches.Length==1,"Exact architecture collider absent or ambiguous: "+path);return matches[0];
        }
        static void VerifyCollider(MeshCollider c,JToken pin,string digestKey="mesh_payload_sha256")
        {
            Need(c.sharedMesh!=null&&MeshHash(c.sharedMesh)==(string)pin[digestKey],"Reviewed architectural collision changed: "+PathOf(c.transform));
            for(int r=0;r<4;r++)for(int k=0;k<4;k++)Need(Mathf.Abs(c.transform.localToWorldMatrix[r,k]-(float)pin["world_matrix"][r][k])<.00005f,"Architectural collision pose changed");
        }
        // Opt-in only for the sealed1F57 exact wall transaction. Existing rows keep their old contract.
        static bool Main1F(JToken row)=>(string)row["exact_retirement_scope"]=="main1f57-source-walls-pass1";
        static void Main1FSource(JToken row,string kind)
        {
            Need(Main1F(row),"Unknown scoped1F retirement");
            string recipePath=(string)row["exact_recipe_path"],proofPath=(string)row["prepare_proof_path"];
            Need(Hash(recipePath)==(string)row["exact_recipe_sha256"]&&Hash(proofPath)==(string)row["prepare_proof_sha256"],"Scoped1F proof changed");
            var recipes=JObject.Parse(System.IO.File.ReadAllText(recipePath));
            var r=recipes["component_recipes"].Single(x=>(string)x["component_global_id"]==(string)row["source_component_global_id"]);
            Need((string)r["owner_path"]==(string)row["path"]&&(string)r["mesh_kind"]==kind&&
                (string)r["asset_path"]==(string)row["source_asset"]&&(string)r["asset_guid"]==(string)row["source_guid"]&&
                (string)r["asset_sha256"]==(string)row["source_file_sha256"],"Scoped1F source/owner proof mismatch");
            var proof=JObject.Parse(System.IO.File.ReadAllText(proofPath));
            var q=proof["components"].Single(x=>(string)x["component_global_id"]==(string)row["source_component_global_id"]);
            Need((string)proof["status"]=="PREPARED_READONLY"&&(string)q["source_complete_channels_sha256"]==(string)row["source_complete_channels_sha256"],"Scoped1F prepared source mismatch");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>((string)row["source_asset"]);
            Need(mesh!=null&&Hash((string)row["source_asset"])==(string)row["source_file_sha256"]&&
                AssetDatabase.AssetPathToGUID((string)row["source_asset"])==(string)row["source_guid"]&&
                ArchitecturalCorrectionRegistry.CompleteChannelDigest(mesh)==(string)row["source_complete_channels_sha256"]&&
                MeshHash(mesh)==(string)row["mesh_payload_sha256"],"Scoped1F original source attributes changed");
            if((bool?)row["clear_shared_mesh"]==true||kind=="MeshRenderer")
                Need(r["keep_triangle_ordinals"].Count()==0,"Whole1F retirement includes unreviewed retained geometry");
        }
        static void Main1FPose(Transform t,JToken row)
        {
            for(int r=0;r<4;r++)for(int k=0;k<4;k++)
                Need(float.IsFinite(t.localToWorldMatrix[r,k])&&Mathf.Abs(t.localToWorldMatrix[r,k]-(float)row["world_matrix"][r][k])<.00005f,"Scoped1F retirement frame changed");
        }
        static void Main1FRetiredCollider(MeshCollider c,JToken row,bool fresh)
        {
            Main1FSource(row,"MeshCollider");Main1FPose(c.transform,row);
            Need((bool?)row["clear_shared_mesh"]==true,"Scoped1F null-collider policy missing");
            if(fresh){if(!c.enabled&&c.sharedMesh==null)return;VerifyCollider(c,row);Need(ArchitecturalCorrectionRegistry.CompleteChannelDigest(c.sharedMesh)==(string)row["source_complete_channels_sha256"],"Fresh1F collider source changed");c.enabled=false;c.sharedMesh=null;}
            else Need(!c.enabled&&c.sharedMesh==null,"Retired1F collision rebound or activated");
        }
        public static void VerifyInstalled()
        {
            var j=Read();if(j==null)return;
            foreach(var collection in j["collections"])
            {
                var package=(string)collection["package"];Need(Hash(package)==(string)collection["package_sha256"],"Reviewed floorplan package changed");
                var p=JObject.Parse(File.ReadAllText(package));
                foreach(string key in new[]{"source","spec","frame"})Need(Hash((string)p[key])==(string)p[key+"_sha256"],"Floorplan source/frame changed");
                var root=GameObject.Find("/"+(string)collection["root"]);Need(root!=null&&root.activeInHierarchy,"Reviewed floorplan root missing");
                foreach(var pin in collection["owners"])
                {
                    var f=Find(root.transform,(string)pin["path"]);
                    if(!Source1FReviewedRepairReplay.TryVerify032Owner(f,pin))
                    {
                        VerifyOwner(f,pin);
                        Need(AssetDatabase.GetAssetPath(f.sharedMesh)==(string)pin["asset"]&&AssetDatabase.AssetPathToGUID((string)pin["asset"])==(string)pin["guid"],"Reviewed floorplan asset binding changed");
                    }
                    var renderer=f.GetComponent<MeshRenderer>();Need(renderer!=null&&renderer.enabled,"Reviewed architecture hidden");
                    if((bool)pin["collider"]){var c=f.GetComponent<MeshCollider>();Need(c!=null&&c.enabled&&!c.isTrigger&&c.sharedMesh==f.sharedMesh,"Reviewed architectural collision changed");}
                }
            }
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach(var row in (j["retirements"] as JArray??new JArray()).Where(Main1F))
            {
                Main1FSource(row,"MeshRenderer");
                var f=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true)).Single(x=>PathOf(x.transform)==(string)row["path"]);
                VerifyOwner(f,row);Need(!f.GetComponent<MeshRenderer>().enabled&&f.GetComponents<Collider>().All(c=>!c.enabled),"Retired1F wall reactivated");
            }
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>(true)).ToArray();
            foreach(var row in j["collider_retirements"] as JArray??new JArray())
            {var c=all.Single(x=>PathOf(x.transform)==(string)row["path"]);if(Main1F(row))Main1FRetiredCollider(c,row,false);else{VerifyCollider(c,row);Need(!c.enabled,"Retired invisible wall collision reactivated");}}
            foreach(var row in j["collider_corrections"] as JArray??new JArray())
            {var c=all.Single(x=>PathOf(x.transform)==(string)row["path"]);if(Main1FNegativeClosureReplay.TryVerify(c,row))continue;VerifyCollider(c,row,"replacement_payload_sha256");Need(c.enabled&&AssetDatabase.GetAssetPath(c.sharedMesh)==(string)row["replacement_asset"],"Mixed architecture/fixture collision binding changed");if(Main1F(row)){Main1FSource(row,"MeshCollider");Need(AssetDatabase.AssetPathToGUID((string)row["replacement_asset"])==(string)row["replacement_guid"]&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(c.sharedMesh)==(string)row["replacement_complete_channels_sha256"],"Complete1F collision replacement changed");}}
            Source1FReviewedRepairReplay.VerifyWestInstalled();
        }
        // Earlier source owners remain byte-exact after a later reviewed full-plan trace retires them.
        // Only an explicit current retirement row can change the enabled-state expectation.
        public static bool VerifyRetiredSourceComponent(Component owner)
        {
            Need(owner is MeshFilter||owner is MeshCollider,"Retirement composition requires exact mesh owner");
            var j=Read();if(j==null)return false;string path=PathOf(owner.transform);
            string key=owner is MeshFilter?"retirements":"collider_retirements";
            var rows=(j[key] as JArray??new JArray()).Where(r=>(string)r["path"]==path).ToArray();
            if(rows.Length==0)return false;Need(rows.Length==1&&!Main1F(rows[0]),"Ambiguous or unsupported source retirement composition");
            var row=rows[0];
            if(owner is MeshFilter f)
            {
                VerifyOwner(f,row);string asset=(string)row["asset"];
                Need(AssetDatabase.GetAssetPath(f.sharedMesh)==asset&&AssetDatabase.AssetPathToGUID(asset)==(string)row["guid"],"Retired source asset identity changed");
                var r=f.GetComponent<MeshRenderer>();Need(r!=null&&!r.enabled&&f.GetComponents<Collider>().All(c=>!c.enabled),"Retired source renderer or collision reactivated");
            }
            else {var c=(MeshCollider)owner;VerifyCollider(c,row);Need(!c.enabled,"Retired source collision reactivated");}
            VerifyInstalled();return true;
        }
        public static void BeforeRebuild(string rootPath)
        {
            Main1FNegativeClosureReplay.BeforeRebuild(rootPath);var j=Read();if(j==null)return;VerifyInstalled();
            var rows=j["retirements"].Where(q=>((string)q["path"]).StartsWith(rootPath+"/",StringComparison.Ordinal)).ToArray();if(rows.Length==0)return;
            var root=GameObject.Find("/"+rootPath);Need(root!=null,"Retired-source rebuild root missing");
            foreach(var row in rows){var f=Find(root.transform,(string)row["path"]);VerifyOwner(f,row);Need(!f.GetComponent<MeshRenderer>().enabled&&f.GetComponents<Collider>().All(c=>!c.enabled),"Misplaced legacy architecture was reactivated");}
        }
        public static void AfterRebuild(Transform built,string rootPath)
        {
            Main1FNegativeClosureReplay.BeforeRebuild(rootPath);var j=Read();if(j==null)return;
            // Both old/new roots exist here. Validate the new subtree directly before removing the old root.
            foreach(var row in j["retirements"].Where(q=>((string)q["path"]).StartsWith(rootPath+"/",StringComparison.Ordinal)))
            {
                var f=Find(built,(string)row["path"]);VerifyOwner(f,row);f.GetComponent<MeshRenderer>().enabled=false;
                foreach(var c in f.GetComponents<Collider>())c.enabled=false;
            }
            foreach(var row in (j["collider_retirements"] as JArray??new JArray()).Where(q=>((string)q["path"]).StartsWith(rootPath+"/",StringComparison.Ordinal)))
            {var c=FindCollider(built,(string)row["path"]);if(Main1F(row))Main1FRetiredCollider(c,row,true);else{VerifyCollider(c,row);c.enabled=false;}}
            foreach(var row in (j["collider_corrections"] as JArray??new JArray()).Where(q=>((string)q["path"]).StartsWith(rootPath+"/",StringComparison.Ordinal)))
            {
                var c=FindCollider(built,(string)row["path"]);var mesh=AssetDatabase.LoadAssetAtPath<Mesh>((string)row["replacement_asset"]);
                Need(mesh!=null&&AssetDatabase.AssetPathToGUID((string)row["replacement_asset"])==(string)row["replacement_guid"]&&MeshHash(mesh)==(string)row["replacement_payload_sha256"],"Mixed collision replacement changed");
                if(Main1F(row))
                {
                    Main1FSource(row,"MeshCollider");Main1FPose(c.transform,row);Need(c.enabled&&c.sharedMesh!=null,"Fresh1F collision absent or inactive");
                    string actual=MeshHash(c.sharedMesh),complete=ArchitecturalCorrectionRegistry.CompleteChannelDigest(c.sharedMesh);
                    // A shared MR/MC batch can already carry the proven index partition.
                    // Accept only the exact prepared source or exact prepared final geometry.
                    Need((actual==(string)row["mesh_payload_sha256"]&&complete==(string)row["source_complete_channels_sha256"])||
                        (actual==(string)row["replacement_payload_sha256"]&&complete==(string)row["replacement_complete_channels_sha256"]),"Fresh1F collision is neither exact source nor exact final partition");
                    Need(ArchitecturalCorrectionRegistry.CompleteChannelDigest(mesh)==(string)row["replacement_complete_channels_sha256"],"Complete1F saved collision replacement changed");
                }
                else VerifyCollider(c,row);
                c.sharedMesh=mesh;
            }
        }
    }
}
#endif
