#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor.Twin
{
    // Final services-only composition. Fresh generation omits four inferred rows;
    // saved output retains every unselected post-office/guide/facade source channel.
    public static class StairInfillOmissionRegistry
    {
        public const string Registry="content/world/observed-interiors/stair-infill-omission.json";
        public const string SourcePlan="content/world/observed-interiors/stair-infill-omission-source.json";
        public const string Spec="content/world/observed-interiors/services-2f.json";
        public const string Root="실내 트윈 마감/2F 편의";
        public const string Schema="chooguard.saved-stair-infill-omission.v1";
        public static readonly string[] Omitted={"RM-2F-S-back0","RM-2F-S-back1","RM-2F-S-back0-door","RM-2F-S-back1-door"};
        public static void Require(bool yes,string message){if(!yes)throw new InvalidDataException(message);}
        public static string Hash(string path){using(var f=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
        public static string PathOf(Transform t){var n=new List<string>();for(;t!=null;t=t.parent)n.Add(t.name);n.Reverse();return string.Join("/",n);}
        public static void Frame(Matrix4x4 actual,JToken expected)
        {
            Require(expected is JArray a&&a.Count==4&&a.All(r=>r is JArray row&&row.Count==4),"Missing exact frame");
            for(int r=0;r<4;r++)for(int c=0;c<4;c++){float e=(float)expected[r][c];Require(float.IsFinite(e)&&float.IsFinite(actual[r,c])&&Mathf.Abs(e-actual[r,c])<.00004f,"Stair omission source frame changed");}
        }
        static Vector3 V(JToken a){Require(a is JArray v&&v.Count==3,"Missing vector");var q=new Vector3((float)a[0],(float)a[1],(float)a[2]);Require(float.IsFinite(q.x)&&float.IsFinite(q.y)&&float.IsFinite(q.z),"Nonfinite vector");return q;}
        public static JObject Read()
        {
            if(!File.Exists(Registry))return null;
            var p=JObject.Parse(File.ReadAllText(Registry));Require((string)p["schema"]==Schema,"Stair omission registry schema changed");
            Require((string)p["source_plan"]==SourcePlan&&Hash(SourcePlan)==(string)p["source_plan_sha256"],"Sealed stair omission source plan changed");
            Require((string)p["source_spec"]==Spec&&Hash(Spec)==(string)p["omitted_spec_sha256"],"Fresh omission canonical source changed");
            var rows=p["entries"] as JArray;Require(rows!=null&&rows.Count==11&&rows.Select(x=>(string)x["generation_source_asset"]).Distinct().Count()==11,"Exact eleven source aliases required");
            Require(p["omitted_element_ids"] is JArray ids&&ids.Select(x=>(string)x).OrderBy(x=>x).SequenceEqual(Omitted.OrderBy(x=>x)),"Four exact omitted rows required");
            VerifyDependencies(p);return p;
        }
        public static JObject Plan(JObject p)=>JObject.Parse(File.ReadAllText((string)p["source_plan"]));
        static JToken Row(JObject p,string source)=>p==null?null:p["entries"].SingleOrDefault(x=>(string)x["generation_source_asset"]==source);
        public static bool HasCorrection(string source)=>source!=null&&Row(Read(),source)!=null;
        public static string[] SourceAssetsForRoot(string root)=>root.Trim('/')==Root&&Read() is JObject p?p["entries"].Select(x=>(string)x["generation_source_asset"]).ToArray():Array.Empty<string>();
        public static string[] InstalledAssetsForRoot(string root)=>root.Trim('/')==Root&&Read() is JObject p?p["entries"].Select(x=>(string)x["replacement_asset"]).Concat(p["companions"].Select(x=>(string)x["mesh_asset"])).Concat(Main2fDividerRetentionRegistry.InstalledAssetsForRoot(root)).Concat(MatchingHallSourceRetentionRegistry.InstalledAssetsForRoot(root)).Concat(Main2fSourceCompletionRegistry.InstalledAssetsForRoot(root)).Distinct().ToArray():Array.Empty<string>();
        public static void GuardElement(JObject e)
        {
            if(e==null||!Omitted.Contains((string)e["id"])||!File.Exists(Registry))return;
            var p=Read();var expected=Plan(p)["omitted_source_rows"].Single(x=>(string)x["id"]==(string)e["id"]).DeepClone();expected["tier"]="c";
            Require((string)e["tier"]=="c"&&JToken.DeepEquals(expected,e),"Original inferred stair infill cannot be regenerated; four rows are explicitly withheld");
        }
        public static Mesh CheckedMesh(JToken row,string prefix)
        {
            string path=(string)row[prefix+"_asset"];var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Require(mesh!=null&&!EditorUtility.IsDirty(mesh)&&AssetDatabase.AssetPathToGUID(path)==(string)row[prefix+"_guid"]&&Hash(path)==(string)row[prefix+"_file_sha256"]&&Hash(path+".meta")== (string)row[prefix+"_meta_sha256"]&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(mesh)==(string)row[prefix+"_complete_digest"],"Pinned stair composition changed: "+path);return mesh;
        }
        public static void VerifyFresh(Mesh generated,JToken expected)
        {
            Require(generated!=null&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated)==(string)expected["complete_channel_digest"],"Fresh four-row omission channels changed");
            Require(generated.vertexCount==(int)expected["vertex_count"]&&(int)generated.indexFormat==(int)expected["index_format"]&&generated.blendShapeCount==0&&generated.bindposes.Length==0&&generated.boneWeights.Length==0,"Fresh omitted source layout changed");
            var a=generated.GetVertexAttributes();var b=(JArray)expected["attributes"];Require(a.Length==b.Count,"Fresh source attribute count changed");
            for(int i=0;i<a.Length;i++)Require((int)a[i].attribute==(int)b[i]["attribute"]&&(int)a[i].format==(int)b[i]["format"]&&a[i].dimension==(int)b[i]["dimension"]&&a[i].stream==(int)b[i]["stream"],"Fresh source attribute descriptor changed");
            Require(Vector3.Distance(generated.bounds.center,V(expected["bounds_center"]))<.00004f&&Vector3.Distance(generated.bounds.size,V(expected["bounds_size"]))<.00004f,"Fresh source bounds changed");
        }
        static bool SameLayout(Mesh actual,Mesh expected)=>actual!=null&&actual.vertexCount==expected.vertexCount&&actual.indexFormat==expected.indexFormat&&actual.GetVertexAttributes().SequenceEqual(expected.GetVertexAttributes())&&Vector3.Distance(actual.bounds.center,expected.bounds.center)<.00004f&&Vector3.Distance(actual.bounds.size,expected.bounds.size)<.00004f&&actual.blendShapeCount==0&&actual.bindposes.Length==0&&actual.boneWeights.Length==0;
        public static Mesh ResolveBySource(Mesh generated,string source)
        {
            var p=Read();var row=Row(p,source);if(row==null)return generated;CheckedMesh(row,"baseline");var output=CheckedMesh(row,"replacement");
            var preHall=Main2fDividerRetentionRegistry.ResolveComposed(output,source);
            var composed=Main2fSourceCompletionRegistry.ResolveComposed(MatchingHallSourceRetentionRegistry.ResolveComposed(preHall,source),source);
            if(generated!=null&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated)==ArchitecturalCorrectionRegistry.CompleteChannelDigest(composed)){Require(SameLayout(generated,composed),"Installed divider composition layout changed");return composed;}
            if(generated!=null&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated)==(string)row["replacement_complete_digest"]){Require(SameLayout(generated,output),"Installed composition layout changed");return composed;}
            if(generated!=null&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated)==ArchitecturalCorrectionRegistry.CompleteChannelDigest(preHall)){Require(SameLayout(generated,preHall),"Known pre-hall MAIN divider layout changed");return composed;}
            VerifyFresh(generated,row["fresh_omission"]);return composed;
        }
        public static Mesh ResolveForRebuild(Mesh generated,string source,string owner,Matrix4x4 frame)
        {
            var p=Read();var row=Row(p,source);if(row==null)return generated;
            Require((string)row["owner_path"]==owner,"Fresh stair omission owner changed");Frame(frame,row["world_matrix"]);
            return ResolveBySource(generated,source);
        }
        public static JObject CaptureDependencies(JObject plan)
        {
            var result=new JObject();var sources=plan["patches"].Select(x=>(string)x["generation_source_asset"]).ToArray();
            string[] files={ArchitecturalCorrectionRegistry.Registry,FloorGuideLayoutRegistry.Registry,ObservedFacadeRegistry.Registry};
            foreach(string path in files)
            {
                if(!File.Exists(path))continue;var j=JObject.Parse(File.ReadAllText(path));var arr=(j["entries"]??j["meshes"]) as JArray;
                result[path]=arr==null?new JArray():new JArray(arr.Where(r=>sources.Contains((string)r["source_asset"])).Select(r=>r.DeepClone()));
            }
            return result;
        }
        static void VerifyDependencies(JObject p)
        {
            var actual=CaptureDependencies(Plan(p));Require(JToken.DeepEquals(actual,p["prior_registry_rows"]),"Prior office/guide/facade source composition changed");
        }
        public static void PreflightRebuild(string sourceSpec,string ownerRoot)
        {
            if(ownerRoot.Trim('/')!=Root||!File.Exists(Registry))return;
            Require(Path.GetFullPath(sourceSpec)==Path.GetFullPath(Spec),"Exact omitted services source required");
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Stable edit mode required");VerifyInstalled();
        }
        static Component ResolveGlobal(string id)
        {
            GlobalObjectId g;Require(GlobalObjectId.TryParse(id,out g),"Malformed scoped component identity");var c=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(g) as Component;Require(c!=null,"Missing scoped component identity");return c;
        }
        public static Component ExactOriginalOwner(JToken proof)
        {
            var c=ResolveGlobal((string)proof["owner"]["global_object_id"]);Require(PathOf(c.transform)==(string)proof["owner"]["path"],"Original scoped source owner changed");Frame(c.transform.localToWorldMatrix,proof["owner"]["world_matrix"]);return c;
        }
        static Component OwnerAt(JToken row)
        {
            string path=(string)row["owner_path"];var scene=SceneManager.GetActiveScene();var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>PathOf(t)==path).ToArray();Require(all.Length==1,"Missing/ambiguous installed owner "+path);
            Component c=(string)row["owner_type"]=="MeshCollider"?(Component)all[0].GetComponent<MeshCollider>():all[0].GetComponent<MeshFilter>();Require(c!=null,"Installed owner component missing");return c;
        }
        // Serializes every original setting while allowing only the mesh reference to change.
        public static string Settings(Component c)
        {
            var j=JObject.Parse(EditorJsonUtility.ToJson(c));var fields=j[c.GetType().Name] as JObject;Require(fields!=null,"Expected typed composed owner serialization");fields.Remove("m_Mesh");fields.Remove("m_GameObject");fields.Remove("m_CorrespondingSourceObject");fields.Remove("m_PrefabInstance");fields.Remove("m_PrefabAsset");return j.ToString(Newtonsoft.Json.Formatting.None);
        }
        static void CompanionAsset(JToken c)
        {
            string path=(string)c["prefab_asset"];Require(AssetDatabase.AssetPathToGUID(path)==(string)c["prefab_guid"]&&Hash(path)==(string)c["prefab_file_sha256"]&&Hash(path+".meta")== (string)c["prefab_meta_sha256"],"Preserved office companion recipe changed");CheckedMesh(c,"mesh");
            foreach(var m in c["materials"])Require(AssetDatabase.AssetPathToGUID((string)m["asset"])==(string)m["guid"]&&Hash((string)m["asset"])==(string)m["file_sha256"],"Preserved companion material changed");
        }
        static void VerifyCompanion(Transform child,JToken c)
        {
            CompanionAsset(c);Require(child!=null&&PathOf(child)==(string)c["owner_path"]&&child.gameObject.activeInHierarchy&&child.GetComponentsInChildren<Collider>(true).Length==0,"Preserved office glazing companion changed");Frame(child.localToWorldMatrix,c["world_matrix"]);
            var f=child.GetComponent<MeshFilter>();var r=child.GetComponent<MeshRenderer>();Require(f!=null&&r!=null&&f.sharedMesh==CheckedMesh(c,"mesh")&&r.enabled&&Settings(r)==(string)c["renderer_settings"]&&r.sharedMaterials.Select(AssetDatabase.GetAssetPath).SequenceEqual(c["materials"].Select(m=>(string)m["asset"]))&&GameObjectUtility.GetStaticEditorFlags(child.gameObject)==(StaticEditorFlags)(int)c["static_flags"]&&child.gameObject.layer==(int)c["layer"],"Preserved office glazing channels/settings changed");
        }
        public static void RestoreCompanionsForRebuild(MeshFilter target,string source)
        {
            var p=Read();var row=Row(p,source);if(row==null)return;Require(target!=null&&PathOf(target.transform)==(string)row["owner_path"]&&target.sharedMesh==Main2fSourceCompletionRegistry.ResolveComposed(MatchingHallSourceRetentionRegistry.ResolveComposed(Main2fDividerRetentionRegistry.ResolveComposed(CheckedMesh(row,"replacement"),source),source),source),"Composed rebuild attachment owner changed");
            foreach(var c in p["companions"].Where(c=>(string)c["parent_source_asset"]==source))
            {
                CompanionAsset(c);string name=(string)c["child_name"];Require(target.transform.Find(name)==null,"Duplicate preserved office companion");
                var child=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>((string)c["prefab_asset"]),target.transform);
                Require(child!=null,"Preserved companion instantiation failed");child.name=name;VerifyCompanion(child.transform,c);
            }
        }
        public static void VerifyInstalled()
        {
            var p=Read();Require(p!=null,"No installed stair omission registry");
            foreach(var row in p["entries"])
            {
                CheckedMesh(row,"baseline");var output=Main2fSourceCompletionRegistry.ResolveComposed(MatchingHallSourceRetentionRegistry.ResolveComposed(Main2fDividerRetentionRegistry.ResolveComposed(CheckedMesh(row,"replacement"),(string)row["generation_source_asset"]),(string)row["generation_source_asset"]),(string)row["generation_source_asset"]);var c=OwnerAt(row);Frame(c.transform.localToWorldMatrix,row["world_matrix"]);
                Require(c.gameObject.activeInHierarchy&&Settings(c)==(string)row["owner_settings"],"Installed composed owner physical settings changed");
                Require(c is MeshFilter f?f.sharedMesh==output:c is MeshCollider mc&&mc.sharedMesh==output,"Installed composed binding changed");
                if(c is MeshFilter mf)Require(mf.GetComponent<MeshRenderer>()!=null&&Settings(mf.GetComponent<MeshRenderer>())==(string)row["renderer_settings"],"Installed renderer channels/flags changed");
            }
            var scene=SceneManager.GetActiveScene();var transforms=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach(var comp in p["companions"]){var matches=transforms.Where(t=>PathOf(t)==(string)comp["owner_path"]).ToArray();Require(matches.Length==1,"Preserved office companion missing/duplicated");VerifyCompanion(matches[0],comp);}
            var forbidden=transforms.Select(t=>t.GetComponent<ChooGuard.App.Fps.Facilities.StationDoor>()).Where(d=>d!=null&&Omitted.Contains(d.Element)&&d.gameObject.activeInHierarchy).ToArray();Require(forbidden.Length==0,"Withheld inferred ServiceDoor was regenerated or reactivated");
            foreach(var omitted in p["inactive_objects"])
            {
                GlobalObjectId g;Require(GlobalObjectId.TryParse((string)omitted["global_object_id"],out g),"Invalid inactive owner id");var go=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(g) as GameObject;
                // Fresh source rebuild omits these objects entirely. The first live install retains inactive originals.
                if(go!=null)Require(!go.activeSelf&&PathOf(go.transform)==(string)omitted["path"],"Withheld original inferred owner reactivated");
            }
            MatchingHallSourceRetentionRegistry.VerifyInstalled();
        }
        public static string Verify(string[] args){Require(args==null||args.Length==0,"No override arguments");VerifyInstalled();return "EXACT_STAIR_INFILL_OMISSION_VERIFIED 11 shared sources; 492 render + 84 collider triangles; preserved original stair axis and office glazing; runtime traversal unverified";}
    }
}
#endif
