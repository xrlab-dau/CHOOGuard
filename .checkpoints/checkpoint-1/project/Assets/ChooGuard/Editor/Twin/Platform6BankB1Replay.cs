#if UNITY_EDITOR
// REVIEW DRAFT: root promotes with the coupled floor transaction, never independently.
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
    public static class Platform6BankB1Replay
    {
        public const string Registry="content/world/observed-interiors/platform6-B1-source-placement.json";
        public const string Legacy="content/world/observed-interiors/legacy-west-retail-architecture-retirement.json";
        public static void Need(bool b,string why){if(!b)throw new InvalidDataException("Platform6 B1: "+why);}
        public static string Hash(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
        public static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
        public static Vector3 V(JToken a)=>new Vector3((float)a[0],(float)a[1],(float)a[2]);
        public static Transform Find(string path){var a=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>PathOf(t)==path).ToArray();Need(a.Length==1,"missing/ambiguous exact path "+path);return a[0];}
        public static void Frame(Transform t,JToken a,float tolerance=.0002f){var m=t.localToWorldMatrix;for(int r=0;r<4;r++)for(int c=0;c<4;c++)Need(float.IsFinite(m[r,c])&&Mathf.Abs(m[r,c]-(float)a[r][c])<tolerance,"world frame "+PathOf(t));}
        public static string Complete(Mesh m)=>ArchitecturalCorrectionRegistry.CompleteChannelDigest(m);
        public static JObject Read()
        {
            if(!File.Exists(Registry))
            {
                // Optional before first install; a retained terminal composition proves an installed registry was lost.
                if(File.Exists(Legacy)){var legacy=JObject.Parse(File.ReadAllText(Legacy));Need(!legacy["meshes"].Any(r=>r["platform6_B1_rigid_bank_composition"]!=null),"installed exact B1 registry lost while native terminal composition remains");}return null;
            }
            var j=JObject.Parse(File.ReadAllText(Registry));Need((string)j["schema"]=="chooguard.platform6-B1-installed-rigid-bank.v1","registry schema");Need(Hash((string)j["manifest"])==(string)j["manifest_sha256"],"sealed manifest changed");return j;
        }
        public static JObject Manifest(JObject registry)=>JObject.Parse(File.ReadAllText((string)registry["manifest"]));
        static string Ref(UnityEngine.Object o)
        {
            if(o==null)return "null";
            if(EditorUtility.IsPersistent(o)&&AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string guid,out long id)&&!string.IsNullOrEmpty(guid))return "asset:"+guid+":"+id;
            if(o is GameObject go)return "go:"+PathOf(go.transform);
            if(o is Component c)return "component:"+PathOf(c.transform)+":"+c.GetType().FullName+":"+Array.IndexOf(c.GetComponents(c.GetType()),c);
            throw new InvalidDataException("B1 unnormalized reference "+o.GetType());
        }
        static JToken Normalize(JToken token)
        {
            if(token is JObject ob)
            {
                if(ob.Count==1&&ob["instanceID"]!=null){int id=(int)ob["instanceID"];return new JValue(id==0?"null":Ref(EditorUtility.InstanceIDToObject(id)));}
                foreach(var p in ob.Properties().ToArray())p.Value=Normalize(p.Value);
            }
            else if(token is JArray ar)for(int i=0;i<ar.Count;i++)ar[i]=Normalize(ar[i]);return token;
        }
        public static JToken Fields(UnityEngine.Object obj,bool ignorePosition=false)
        {
            var j=Normalize(JObject.Parse(EditorJsonUtility.ToJson(obj))) as JObject;
            if(ignorePosition)
            {
                Need(obj is Transform,"position exemption is Transform-only");
                var inner=j["Transform"]as JObject;Need(inner!=null&&inner.Remove("m_LocalPosition"),"wrapped Transform m_LocalPosition exemption must be explicit");
            }return j;
        }
        // Cross-session stable references use exact paths or asset GUID/localIDs, never ephemeral instanceIDs.
        public static JArray Snapshot(Transform root,bool ignoreParentPosition)
        {
            var a=new JArray();foreach(var t in root.GetComponentsInChildren<Transform>(true).OrderBy(PathOf,StringComparer.Ordinal))
            {
                Need(t.GetComponents<Component>().All(c=>c!=null),"missing scripts");
                a.Add(new JObject{["path"]=PathOf(t),["kind"]="GameObject",["ordinal"]=0,["fields"]=Fields(t.gameObject)});
                foreach(var c in t.GetComponents<Component>())a.Add(new JObject{["path"]=PathOf(t),["kind"]=c.GetType().AssemblyQualifiedName,["ordinal"]=Array.IndexOf(c.GetComponents(c.GetType()),c),["fields"]=Fields(c,ignoreParentPosition&&t==root&&c is Transform)});
            }return a;
        }
        public static void SnapshotMatches(Transform root,JToken expected)=>Need(JToken.DeepEquals(Snapshot(root,true),expected),"source hierarchy, local poses, materials, component fields or state changed: "+PathOf(root));
        public static void VerifyOwners(JObject m,bool moved)
        {
            Vector3 delta=moved?V(m["Unity_world_translation_XYZ"]):Vector3.zero;
            // Invocation-local index; every call rechecks missing/duplicate paths and all original fields.
            var owners=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToLookup(PathOf,StringComparer.Ordinal);
            foreach(var p in m["complete_sourcepart_owner_pins"])
            {
                var matches=owners[(string)p["path"]].ToArray();Need(matches.Length==1,"missing/ambiguous exact sourcepart path "+(string)p["path"]);var t=matches[0];var mr=t.GetComponent<MeshRenderer>();var mf=t.GetComponent<MeshFilter>();var mc=t.GetComponent<MeshCollider>();
                Need(mr!=null&&mr.enabled&&mr.additionalVertexStreams==null&&t.gameObject.activeInHierarchy&&mf!=null&&mc!=null&&mc.enabled&&!mc.isTrigger&&!mc.convex&&(int)mc.cookingOptions==30,"active complete sourcepart render/collision "+t.name);
                var world=(JArray)p["world_matrix"].DeepClone();world[0][3]=(double)world[0][3]+delta.x;world[1][3]=(double)world[1][3]+delta.y;world[2][3]=(double)world[2][3]+delta.z;Frame(t,world);
                Need(mf.sharedMesh!=null&&mf.sharedMesh==mc.sharedMesh&&Complete(mf.sharedMesh)==(string)p["mesh_complete_digest"]&&AssetDatabase.GetAssetPath(mf.sharedMesh)==(string)p["mesh_asset"],"sourcepart fullchannel/MC mesh");
                Need(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mf.sharedMesh,out string guid,out long id)&&guid==(string)p["mesh_guid"]&&id==(long)p["mesh_local_file_id"],"sourcepart subasset identity");
                Need(mr.sharedMaterials.Select(AssetDatabase.GetAssetPath).SequenceEqual(p["materials"].Select(x=>(string)x)),"sourcepart material sequence");
            }
        }
        static Mesh Output(JToken row,string role)
        {
            string p=(string)row[role+"_asset"];Need(File.Exists(p)&&Hash(p)==(string)row[role+"_file_sha256"]&&Hash(p+".meta")== (string)row[role+"_meta_sha256"]&&AssetDatabase.AssetPathToGUID(p)==(string)row[role+"_guid"],"body/terminal saved asset identity");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(p);Need(mesh!=null&&!EditorUtility.IsDirty(mesh)&&Complete(mesh)==(string)row[role+"_complete_digest"],"body/terminal saved channels");return mesh;
        }
        public static void Verify()
        {
            var j=Read();if(j==null)return;Platform6B1SourceFloorSupport.Verify();var m=Manifest(j);VerifyOwners(m,true);
            var p=m["sourcepart_parent_move_once"];Frame(Find((string)p["path"]),p["world_matrix_after_rigid_move"]);
            var w=m["moving_well_all_children"].Single(x=>(long)x["transform_id"]==(long)m["moving_well_parent_transform_id"]);Frame(Find((string)w["path"]),w["world_matrix_after_rigid_move"]);
            SnapshotMatches(Find((string)p["path"]),j["sourcepart_state"]);SnapshotMatches(Find((string)w["path"]),j["well_state"]);
            var native=Find((string)m["native_body_derivatives"][0]["owner"]["path"]);var body=Find((string)m["body_owner_path"]);
            Frame(native,m["native_body_derivatives"][0]["source_world_matrix"]);Frame(body,m["native_body_derivatives"][0]["body_world_matrix_after_rigid_move"]);
            var legacy=JObject.Parse(File.ReadAllText(Legacy));
            foreach(var row in j["assets"])
            {
                bool render=(string)row["name"]=="native6-render";var complement=Platform6B1ContactTerminal.ResolveB1NativeCarrier(Output(row,"complement"),(string)row["name"],PathOf(native));var b=Output(row,"body");
                Need(render?native.GetComponent<MeshFilter>().sharedMesh==complement&&body.GetComponent<MeshFilter>().sharedMesh==b:native.GetComponent<MeshCollider>().sharedMesh==complement&&body.GetComponent<MeshCollider>().sharedMesh==b,"body and native mesh bindings");
                var lr=legacy["meshes"].Single(r=>(string)r["id"]=="platform6-"+(string)row["name"]);Need(JToken.DeepEquals(lr,Platform6B1ContactTerminal.ResolveB1LegacyRow(row["legacy_final_row"],(string)row["name"])),"scoped exact Legacy terminal composition");
            }
            Need(body.GetComponents<Component>().Length==4&&body.gameObject.activeInHierarchy&&body.GetComponent<MeshRenderer>().enabled&&body.GetComponent<MeshCollider>().enabled&&!body.GetComponent<MeshCollider>().isTrigger&&!body.GetComponent<MeshCollider>().convex,"attributed body component/state");
            Need(JToken.DeepEquals(Fields(body.gameObject),j["body_gameobject_fields"])&&JToken.DeepEquals(Fields(body.GetComponent<MeshRenderer>()),j["body_renderer_fields"])&&JToken.DeepEquals(Fields(body.GetComponent<MeshCollider>()),j["body_collider_fields"]),"body materials/render/collider settings");
            string pointsPath=(string)m["stationpoints"]["path"];string pointsText=File.ReadAllText(pointsPath);var points=JObject.Parse(pointsText);Need(JToken.DeepEquals(points["escalators"].Single(r=>(string)r["id"]=="esc-well-s-89"),m["stationpoints"]["after_row"]),"translated StationPoints row");var imported=AssetDatabase.LoadAssetAtPath<TextAsset>(pointsPath);Need(imported!=null&&imported.text==pointsText,"StationPoints imported asset must match disk");
        }
        static void RequireStateAndMove(Transform t,JToken oldMatrix,JToken newMatrix,JToken state)
        {
            // Fresh source generation must positively match original local fields before translation.
            Frame(t,oldMatrix);SnapshotMatches(t,state);Vector3 old=t.position;var a=newMatrix;var b=oldMatrix;t.position=old+new Vector3((float)a[0][3]-(float)b[0][3],(float)a[1][3]-(float)b[1][3],(float)a[2][3]-(float)b[2][3]);Frame(t,newMatrix);SnapshotMatches(t,state);
        }
        public static void AfterSourcePartsBuilt()
        {
            var j=Read();if(j==null)return;var m=Manifest(j);VerifyOwners(m,false);var p=m["sourcepart_parent_move_once"];RequireStateAndMove(Find((string)p["path"]),p["world_matrix_at_read"],p["world_matrix_after_rigid_move"],j["sourcepart_state"]);VerifyOwners(m,true);
        }
        public static void AfterMovingWellsBuilt()
        {
            var j=Read();if(j==null)return;var m=Manifest(j);var w=m["moving_well_all_children"].Single(x=>(long)x["transform_id"]==(long)m["moving_well_parent_transform_id"]);RequireStateAndMove(Find((string)w["path"]),w["world_matrix_at_read"],w["world_matrix_after_rigid_move"],j["well_state"]);
        }
        public static Vector3 SurveyWellOffset(string id)
        {
            if(id!="well-s-89")return Vector3.zero;var j=Read();if(j==null)return Vector3.zero;Verify();var m=Manifest(j);return V(m["Unity_world_translation_XYZ"]);
        }
        public static void BeforeRebuild(string rootPath)
        {
            if(Read()==null)return;Platform6B1SourceFloorSupport.BeforeRebuild(rootPath);if(rootPath=="FPSWorld"||rootPath=="FPSWorld/공식 자료 부산역 역사/MainShell"||rootPath=="FPSWorld/원본 분리 부품"||rootPath=="FPSWorld/승강장 디딤판 운행")Verify();
        }
        public static void AfterRebuild(Transform built,string rootPath)
        {
            var j=Read();if(j==null)return;var m=Manifest(j);string nativePath=(string)m["native_body_derivatives"][0]["owner"]["path"];
            if(!nativePath.StartsWith(rootPath+"/",StringComparison.Ordinal))return;
            // A builder may retain an inactive previous root until commit. Scope lookup to the fresh root.
            var sources=built.GetComponentsInChildren<Transform>(true).Where(t=>PathOf(t)==nativePath).ToArray();Need(sources.Length==1,"fresh exact native6 owner missing");var source=sources[0];
            Need(!built.GetComponentsInChildren<Transform>(true).Any(t=>PathOf(t)==(string)m["body_owner_path"]),"body must be fresh after parent rebuild");
            var go=new GameObject(((string)m["body_owner_path"]).Split('/').Last());go.transform.SetParent(source.parent,false);go.transform.localPosition=source.localPosition;go.transform.localRotation=source.localRotation;go.transform.localScale=source.localScale;go.transform.position+=V(m["Unity_world_translation_XYZ"]);go.layer=source.gameObject.layer;go.tag=source.gameObject.tag;GameObjectUtility.SetStaticEditorFlags(go,GameObjectUtility.GetStaticEditorFlags(source.gameObject));
            var mf=go.AddComponent<MeshFilter>();var mr=go.AddComponent<MeshRenderer>();var mc=go.AddComponent<MeshCollider>();EditorUtility.CopySerialized(source.GetComponent<MeshRenderer>(),mr);EditorUtility.CopySerialized(source.GetComponent<MeshCollider>(),mc);
            mf.sharedMesh=Output(j["assets"].Single(r=>(string)r["name"]=="native6-render"),"body");mc.sharedMesh=Output(j["assets"].Single(r=>(string)r["name"]=="native6-collider"),"body");
            Need(mr.gameObject==go&&mc.gameObject==go,"component copies must retain attributed owner");Frame(go.transform,m["native_body_derivatives"][0]["body_world_matrix_after_rigid_move"]);
            Need(JToken.DeepEquals(Fields(go),j["body_gameobject_fields"])&&JToken.DeepEquals(Fields(mr),j["body_renderer_fields"])&&JToken.DeepEquals(Fields(mc),j["body_collider_fields"]),"fresh body fields");
        }
    }
}
#endif
