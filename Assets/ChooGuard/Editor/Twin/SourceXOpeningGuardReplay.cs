// REVIEW DRAFT. Restore only the separately attributed source-X phantom-aperture slab patch.
// Guard mesh replacements replay through the existing exact structural correction registry.
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace ChooGuard.Editor.Twin
{
    public static class SourceXOpeningGuardReplay
    {
        public const string Registry="content/world/observed-interiors/source-x-opening-guards.json";
        static void Need(bool b,string m){if(!b)throw new InvalidDataException(m);}
        static string Hash(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
        static JObject Read()
        {
            if(!File.Exists(Registry))return null;var j=JObject.Parse(File.ReadAllText(Registry));
            Need((string)j["schema"]=="chooguard.source-X-opening-guards-installed.v1"&&Hash((string)j["packet_path"])==(string)j["packet_sha256"]&&Hash((string)j["proof_path"])==(string)j["proof_sha256"],"Source-X guard replay evidence changed");
            foreach(string key in new[]{"source_plan","frame","source_masks","source_authority"})Need(Hash((string)j[key+"_path"])==(string)j[key+"_sha256"],"Source-X guard authority/frame changed");return j;
        }
        static void Frame(Transform t,JToken pin)
        {for(int r=0;r<4;r++)for(int c=0;c<4;c++)Need(float.IsFinite(t.localToWorldMatrix[r,c])&&Mathf.Abs(t.localToWorldMatrix[r,c]-(float)pin[r][c])<.00002f,"Source-X guard/patch frame changed");}
        static Mesh Asset(JToken p)
        {
            string path=(string)p["asset"];var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);Need(m!=null&&!EditorUtility.IsDirty(m)&&AssetDatabase.AssetPathToGUID(path)==(string)p["guid"]&&Hash(path)==(string)p["file_sha256"]&&Hash(path+".meta")== (string)p["meta_sha256"]&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(m)==(string)p["complete_channels_sha256"],"Source-X guard/patch saved mesh changed");return m;
        }
        static Transform Guard(Transform root,JToken pin)
        {
            var t=root.Find((string)pin["name"]);Need(t!=null&&t.parent==root&&t.gameObject.activeInHierarchy,"Source-X direct guard owner missing");Frame(t,pin["world_matrix"]);var f=t.GetComponent<MeshFilter>();var r=t.GetComponent<MeshRenderer>();Need(f!=null&&r!=null&&r.enabled&&f.sharedMesh==Asset(pin),"Source-X corrected guard binding changed");
            Need(r.sharedMaterials.Length==pin["materials"].Count(),"Inherited guard material count changed");for(int i=0;i<r.sharedMaterials.Length;i++){var mat=pin["materials"][i];Need(AssetDatabase.GetAssetPath(r.sharedMaterials[i])==(string)mat["asset"]&&AssetDatabase.AssetPathToGUID((string)mat["asset"])==(string)mat["guid"]&&Hash((string)mat["asset"])==(string)mat["sha256"],"Inherited guard material identity changed");}
            if((bool)pin["has_collider"]){var c=t.GetComponent<MeshCollider>();Need(c!=null&&c.enabled&&!c.isTrigger&&!c.convex&&(int)c.cookingOptions==30&&c.sharedMesh==f.sharedMesh,"Source-X glass collision differs from render");}else Need(t.GetComponent<MeshCollider>()==null,"Unexpected stainless collider");return t;
        }
        static void Patch(Transform root,JObject j)
        {
            var p=j["patch"];var parent=root.Find((string)p["parent_name"]);Need(parent!=null,"Source-X patch parent missing");var t=parent.Find((string)p["name"]);Need(t!=null&&t.parent==parent&&t.localPosition==Vector3.zero&&t.localRotation==Quaternion.identity&&t.localScale==Vector3.one&&t.gameObject.activeInHierarchy&&t.gameObject.layer==(int)p["layer"],"Source-X patch owner/pose changed");Frame(t,p["world_matrix"]);var f=t.GetComponent<MeshFilter>();var r=t.GetComponent<MeshRenderer>();var c=t.GetComponent<MeshCollider>();Need(f!=null&&r!=null&&r.enabled&&c!=null&&c.enabled&&!c.isTrigger&&!c.convex&&(int)c.cookingOptions==30&&f.sharedMesh==Asset(p)&&c.sharedMesh==f.sharedMesh,"Source-X patch complete render/collision changed");
            var mat=p["material"];Need(r.sharedMaterials.Length==1&&AssetDatabase.GetAssetPath(r.sharedMaterial)==(string)mat["asset"]&&AssetDatabase.AssetPathToGUID((string)mat["asset"])==(string)mat["guid"]&&Hash((string)mat["asset"])==(string)mat["sha256"],"Inherited floor material changed");
        }
        public static void ValidateInstalledBeforeRebuild(string rootName)
        {
            var j=Read();if(j==null||(string)j["root"]!=rootName)return;var root=GameObject.Find(rootName)?.transform;Need(root!=null,"Installed source-X guard root missing");foreach(var p in j["guards"])Guard(root,p);Patch(root,j);
        }
        public static void RestoreForRebuild(Transform root,string rootName)
        {
            var j=Read();if(j==null||(string)j["root"]!=rootName)return;Need(root!=null&&root.name==rootName,"Wrong source-X rebuild root");foreach(var p in j["guards"])Guard(root,p);var patch=j["patch"];var parent=root.Find((string)patch["parent_name"]);Need(parent!=null&&parent.Find((string)patch["name"])==null,"Source-X patch already exists/missing parent");var mesh=Asset(patch);var mat=patch["material"];string matPath=(string)mat["asset"];var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);Need(material!=null&&!EditorUtility.IsDirty(material)&&AssetDatabase.AssetPathToGUID(matPath)==(string)mat["guid"]&&Hash(matPath)==(string)mat["sha256"],"Inherited patch material missing/changed");
            var go=new GameObject((string)patch["name"]);go.transform.SetParent(parent,false);go.layer=(int)patch["layer"];go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;var c=go.AddComponent<MeshCollider>();c.sharedMesh=mesh;c.cookingOptions=(MeshColliderCookingOptions)30;GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic);Patch(root,j);
        }
    }
}
#endif
