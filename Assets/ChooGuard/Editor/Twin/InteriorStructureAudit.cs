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
    /// <summary>Read the actual current architectural surfaces and collision topology, including disabled and duplicate layers.</summary>
    public static class InteriorStructureAudit
    {
        public const string DirectoryPath=".planning/2026-10-08-interior-runtime-completion/architecture";
        static string PathOf(Transform t){var parts=new List<string>();while(t!=null){parts.Add(t.name);t=t.parent;}parts.Reverse();return string.Join("/",parts);}
        static JArray Vec(Vector3 v)=>new JArray(v.x,v.y,v.z);
        static JArray Matrix(Matrix4x4 m){var a=new JArray();for(int r=0;r<4;r++){var row=new JArray();for(int c=0;c<4;c++)row.Add(m[r,c]);a.Add(row);}return a;}
        static string Hash(string p){using(var sha=SHA256.Create())using(var s=File.OpenRead(p))return BitConverter.ToString(sha.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
        public static string Export(string directoryPath = DirectoryPath)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Architectural baseline requires edit mode");
            Directory.CreateDirectory(directoryPath+"/meshes");
            var scene=SceneManager.GetSceneByName("FpsStation");if(!scene.isLoaded)throw new InvalidOperationException("Actual FpsStation must be loaded");
            var unique=new Dictionary<Mesh,string>();var meshes=new JArray();var filters=new JArray();var colliders=new JArray();
            Func<Mesh,string> register=mesh=>
            {
                if(mesh==null)return null;if(unique.TryGetValue(mesh,out var existing))return existing;
                string id="mesh-"+unique.Count.ToString("D4");unique.Add(mesh,id);string binary=directoryPath+"/meshes/"+id+".bin";
                if(mesh.isReadable)
                {
                    using(var w=new BinaryWriter(File.Open(binary,FileMode.Create)))
                    {
                        var v=mesh.vertices;var n=mesh.normals;var t=mesh.triangles;
                        w.Write(v.Length);w.Write(t.Length);w.Write(n.Length);
                        foreach(var p in v){w.Write(p.x);w.Write(p.y);w.Write(p.z);}foreach(int index in t)w.Write(index);foreach(var p in n){w.Write(p.x);w.Write(p.y);w.Write(p.z);}
                    }
                }
                meshes.Add(new JObject {["id"]=id,["name"]=mesh.name,["asset"]=AssetDatabase.GetAssetPath(mesh),["asset_guid"]=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mesh)),["vertices"]=mesh.vertexCount,["submeshes"]=mesh.subMeshCount,["readable"]=mesh.isReadable,["binary"]=mesh.isReadable?binary:null,["binary_sha256"]=mesh.isReadable?Hash(binary):null,["bounds_min"]=Vec(mesh.bounds.min),["bounds_max"]=Vec(mesh.bounds.max)});
                return id;
            };
            foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(filter.gameObject.scene!=scene)continue;var renderer=filter.GetComponent<Renderer>();var source=filter.GetComponentInParent<ChooGuard.App.Fps.Twin.SourcePartIdentity>();
                filters.Add(new JObject {["path"]=PathOf(filter.transform),["global_object_id"]=GlobalObjectId.GetGlobalObjectIdSlow(filter).ToString(),["mesh"]=register(filter.sharedMesh),["world_matrix"]=Matrix(filter.transform.localToWorldMatrix),["active"]=filter.gameObject.activeInHierarchy,["renderer_enabled"]=renderer!=null&&renderer.enabled,["world_bounds_min"]=renderer!=null?Vec(renderer.bounds.min):null,["world_bounds_max"]=renderer!=null?Vec(renderer.bounds.max):null,["materials"]=renderer!=null?JArray.FromObject(renderer.sharedMaterials.Select(m=>m==null?"(null)":AssetDatabase.GetAssetPath(m))):new JArray(),["source_component_id"]=source!=null?source.SourceId:null,["source_component_category"]=source!=null?source.Category:null});
            }
            foreach(var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(collider.gameObject.scene!=scene)continue;
                var row=new JObject {["path"]=PathOf(collider.transform),["type"]=collider.GetType().Name,["active"]=collider.gameObject.activeInHierarchy,["enabled"]=collider.enabled,["trigger"]=collider.isTrigger,["world_matrix"]=Matrix(collider.transform.localToWorldMatrix),["world_bounds_min"]=Vec(collider.bounds.min),["world_bounds_max"]=Vec(collider.bounds.max),["global_object_id"]=GlobalObjectId.GetGlobalObjectIdSlow(collider).ToString()};
                if(collider is MeshCollider mc){row["mesh"]=register(mc.sharedMesh);row["convex"]=mc.convex;}
                if(collider is BoxCollider box){row["local_center"]=Vec(box.center);row["local_size"]=Vec(box.size);}
                colliders.Add(row);
            }
            string output=directoryPath+"/current-scene-structure.json";
            File.WriteAllText(output,new JObject {["schema"]="chooguard.actual-scene-architecture.v1",["scene"]=scene.path,["scene_file_sha256"]=Hash(scene.path),["sampled_utc"]=DateTime.UtcNow.ToString("o"),["site_accuracy_verified"]=false,["binary_format"]="little endian int32 vertexCount,indexCount,normalCount; float32 xyz vertices; int32 flattened triangles; float32 xyz normals",["meshes"]=meshes,["renderers"]=filters,["colliders"]=colliders}.ToString());
            return "Architectural baseline exported: renderers="+filters.Count+" colliders="+colliders.Count+" unique_meshes="+meshes.Count;
        }
    }
}
#endif
