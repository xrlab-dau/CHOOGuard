// Editor-only architecture guard. No source mesh is edited; only exact retired legacy surfaces omit collision.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor.Twin
{
    public static class UpperRetiredSurfaceCollisionGuard
    {
        const string Packet="workers/twin/output/vworld-upper-native-correction-20261009/upper-native-correction-draft.json";
        const string PacketSHA="b9e7b4fceb907b7e59ed55f68b233a7cca78d777c79ac9bade72128dd6ad8b6f";
        const string Registry="content/world/observed-interiors/structural-corrections.json";
        const string Operation="retire_entire_superseded_legacy_public_architectural_surface";
        static void Need(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
        static string Hash(string p){using(var s=SHA256.Create())using(var f=File.OpenRead(p))return BitConverter.ToString(s.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
        static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
        static JObject ReadPacket(){Need(Hash(Packet)==PacketSHA,"Retired upper surface recipe changed");return JObject.Parse(File.ReadAllText(Packet));}
        public static bool HasNonDegenerateTriangle(Mesh m)
        {
            Need(m!=null&&m.isReadable,"Collision mesh absent or unreadable");var v=m.vertices;
            Need(v.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)),"Nonfinite collision vertices");
            bool valid=false;
            for(int s=0;s<m.subMeshCount;s++)
            {
                Need(m.GetTopology(s)==MeshTopology.Triangles,"Nontriangle collision submesh");var ids=m.GetIndices(s);Need(ids.Length%3==0,"Incomplete collision triangle");
                for(int i=0;i<ids.Length;i+=3)
                {
                    Need(ids[i]>=0&&ids[i]<v.Length&&ids[i+1]>=0&&ids[i+1]<v.Length&&ids[i+2]>=0&&ids[i+2]<v.Length,"Collision index out of bounds");
                    if(Vector3.Cross(v[ids[i+1]]-v[ids[i]],v[ids[i+2]]-v[ids[i]]).sqrMagnitude>1e-16f)valid=true;
                }
            }
            return valid;
        }
        static UnityEngine.Object Pin(JToken p)
        {
            string path=(string)p["path"];Need(Hash(path)==(string)p["file_sha256"]&&Hash(path+".meta")== (string)p["meta_sha256"]&&AssetDatabase.AssetPathToGUID(path)==(string)p["guid"],"Retired surface source7 asset pin changed");
            var obj=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);Need(obj!=null&&!EditorUtility.IsDirty(obj),"Source7 asset missing or dirty");return obj;
        }
        public static void SourceSevenProof(JToken row)
        {
            string receipt=(string)row["upper_native_source_receipt_path"];
            Need(!string.IsNullOrEmpty(receipt)&&Hash(receipt)==(string)row["upper_native_source_receipt_sha256"],"Retired upper collision requires pinned fresh source7 receipt");
            var j=JObject.Parse(File.ReadAllText(receipt));Need((string)j["schema"]=="chooguard.vworld-public-mezzanine-installation.v1"&&j["objects"].Count()==7,"Wrong source7 collision proof");
            Need(Hash((string)j["manifest_path"])==(string)j["manifest_sha256"],"Source7 manifest changed");
            var all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(var q in j["objects"])
            {
                Need(new[]{"3MV0100002","F04FL00002","F04MV05000"}.Contains((string)q["model_name"]),"Unexpected upper source family");
                var matches=all.Where(t=>PathOf(t)==(string)q["owner_path"]).ToArray();Need(matches.Length==1,"Missing or ambiguous source7 architecture");var t=matches[0];var m=Pin(q["mesh_pin"])as Mesh;var mat=Pin(q["material_pin"])as Material;
                if(q["texture_pin"]!=null&&q["texture_pin"].Type!=JTokenType.Null)Pin(q["texture_pin"]);
                var f=t.GetComponent<MeshFilter>();var c=t.GetComponent<MeshCollider>();var r=t.GetComponent<MeshRenderer>();
                Need(m!=null&&HasNonDegenerateTriangle(m)&&f!=null&&f.sharedMesh==m&&c!=null&&c.enabled&&!c.isTrigger&&!c.convex&&c.sharedMesh==m&&r!=null&&r.enabled&&r.sharedMaterials.Length==1&&r.sharedMaterial==mat&&t.gameObject.activeInHierarchy&&t.gameObject.layer==(int)q["layer"]&&GameObjectUtility.GetStaticEditorFlags(t.gameObject)==(StaticEditorFlags)(int)q["static_flags"],"Source7 replacement floor/stair/rail binding changed");
                var frame=t.localToWorldMatrix;for(int a=0;a<4;a++)for(int b=0;b<4;b++)Need(Mathf.Abs(frame[a,b]-(a==b?1f:0f))<.00002f,"Source7 architecture frame changed");
                string prop=(string)q["texture_property"];Need(mat.GetTextureScale(prop)==new Vector2(1,-1)&&mat.GetTextureOffset(prop)==new Vector2(0,1),"Source7 UV presentation changed");
            }
        }
        public static void ValidateSourceAssembly()
        {
            var rows=JObject.Parse(File.ReadAllText(Registry))["entries"].Where(r=>(string)r["owner"]=="맞이방 · 원본 정합/Box frame/Box_Majibang_3F_L").ToArray();
            Need(rows.Length==1&&rows[0]["upper_native_collision_policy"]!=null,"Coherent height requires complete retired floor registry");SourceSevenProof(rows[0]);
        }
        public static JObject Policy(JToken recipe)
        {
            Need((int)recipe["replacement_triangle_count"]==0&&(string)recipe["operation"]==Operation,"Only exact fully retired upper surfaces may omit collision");
            return new JObject{["schema"]="chooguard.upper-retired-collision.v1",["action"]="omit_on_rebuild",["existing_component_action"]=recipe["baseline_owner"]["mesh_collider_global_id"].Type==JTokenType.Null?"absent":"disable_and_clear",["original_component_global_id"]=recipe["baseline_owner"]["mesh_collider_global_id"].DeepClone(),["recipe_packet_sha256"]=PacketSHA};
        }
        public static bool ShouldOmit(Mesh mesh,string originalAsset,string owner)
        {
            Need(mesh!=null,"Upper collision mesh absent");if(mesh.triangles.Length!=0)return false;
            var packet=ReadPacket();var recipes=packet["recipes"].Where(r=>(string)r["owner_path"]==owner&&(string)r["operation"]==Operation&&(int)r["replacement_triangle_count"]==0).ToArray();
            if(recipes.Length==0)return false;Need(recipes.Length==1,"Ambiguous retired surface recipe");var recipe=recipes[0];
            var rows=JObject.Parse(File.ReadAllText(Registry))["entries"].Where(r=>(string)r["owner"]==owner&&(string)r["source_asset"]==originalAsset).ToArray();Need(rows.Length==1,"Retired collision registry row absent or ambiguous");var row=rows[0];
            Need((string)row["upper_native_packet"]==Packet&&(string)row["upper_native_packet_sha256"]==PacketSHA&&(string)row["upper_native_operation"]==Operation&&JToken.DeepEquals(row["upper_native_collision_policy"],Policy(recipe)),"Retired collision policy does not match exact recipe");
            string path=(string)row["replacement_asset"];Need(AssetDatabase.GetAssetPath(mesh)==path&&AssetDatabase.AssetPathToGUID(path)==(string)row["replacement_guid"]&&Hash(path)==(string)row["replacement_file_sha256"]&&Hash(path+".meta")== (string)row["replacement_meta_sha256"]&&!EditorUtility.IsDirty(mesh),"Retired replacement asset identity changed");
            string complete=ArchitecturalCorrectionRegistry.CompleteChannelDigest(mesh);Need(complete==(string)row["replacement_complete_channels_sha256"]&&complete==(string)recipe["replacement_unity_digests"]["complete_channels_sha256"]&&mesh.vertexCount==(int)recipe["replacement_vertex_count"]&&!HasNonDegenerateTriangle(mesh),"Retired render vertex channels or empty topology changed");
            SourceSevenProof(row);return true;
        }
        public static void ValidateResolved(JToken row,Mesh replacement)
        {
            if(row["upper_native_collision_policy"]==null)return;
            Need(ShouldOmit(replacement,(string)row["source_asset"],(string)row["owner"]),"Retired architectural collision policy violated");
        }
        // Accepts initial installed component or omission after a fresh build. Never edits a nonempty mesh.
        public static void VerifyInstalled(Transform root,string rootPath)
        {
            if(!File.Exists(Registry))return;var rows=JObject.Parse(File.ReadAllText(Registry))["entries"].Where(r=>r["upper_native_collision_policy"]!=null&&((string)r["owner"]).StartsWith(rootPath+"/",StringComparison.Ordinal));
            foreach(var row in rows)
            {
                var t=root.Find(((string)row["owner"]).Substring(rootPath.Length+1));Need(t!=null,"Retired upper surface missing from rebuild");var m=t.GetComponent<MeshFilter>()?.sharedMesh;Need(ShouldOmit(m,(string)row["source_asset"],(string)row["owner"]),"Retired upper surface proof failed");
                foreach(var c in t.GetComponents<MeshCollider>())Need(!c.enabled&&c.sharedMesh==null,"Retired upper MeshCollider was recreated or rebound");
            }
        }
    }
}
