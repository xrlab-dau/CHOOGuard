#if UNITY_EDITOR
// MeshData read/extract/dispose sequence adapted from Unity MeshApiExamples
// e82668f26fbf615d3ee4b1a5941c8fe167d3d485 CreateMeshFromWholeScene.cs:79,175-181,203-217.
// MIT copyright (c) 2019 Unity Technologies; full notice: Outline/ThirdParty/MeshApiExamples-LICENSE.
// Unlike the example/bag prototype, writes only CPU ScriptableObjects, never source meshes/render bindings/importers.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ChooGuard.App.Fps.TrainDeparture;
public static class VisualGeometryCacheAuthor
{
    const string Thread="01a19bde-6b5f-7bf1-a13d-822ca0abe444";
    const string Folder="Assets/ChooGuard/Art/TrainDeparture/VisualCpuGeometry";
    const string CatalogPath=Folder+"/VisualGeometryCatalog.asset";
    [Serializable] sealed class Authority { public string status="",sole_writer_thread="";public bool Native_inflight=false; }
    [Serializable] public sealed class Progress
    {
        public int total,processed,created,reused,unreadable,skippedEmpty,skippedTransient,failed;
        public long vertices,triangles,payloadBytes;
        public double elapsedMs;
        public bool complete,bound,sourceBindingsPreserved;
        public string[] errors;
    }
    sealed class SourceFile { public string Path,File,Meta,Dependency; }
    sealed class Binding { public MeshFilter Filter;public Mesh Mesh;public Renderer Renderer;public Material[] Materials;public Matrix4x4 Pose; }
    sealed class ColliderBinding { public MeshCollider Collider;public Mesh Mesh; }
    static List<Mesh> pending;
    static List<VisualGeometryCache> finished;
    static Dictionary<string,SourceFile> files;
    static List<Binding> bindings;
    static List<ColliderBinding> colliderBindings;
    static List<string> errors;
    static DepartureVisualFocus focus;
    static Progress progress;
    static long started;
    // Import this type into Assets/ChooGuard/Editor/Fps/VisualGeometryCacheAuthor.cs before invoking.
    // run_script wrappers must CALL this stable type, not recompile this entire helper each time.
    // Domain reload recovery: Begin/Resume rebuilds the worklist and reuses already-saved matching binary caches.
    public static string Resume() {RequireAuthority();Release();return Begin();}
    public static string Begin()
    {
        RequireAuthority();
        if(pending!=null)throw new InvalidOperationException("A bake is already active. Finish or Abort it first.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Require(scene.IsValid()&&scene.isLoaded&&scene.path=="Assets/ChooGuard/Scenes/TrainDepartureDemo.unity","Expected active Demo scene");
        focus=UnityEngine.Object.FindObjectsByType<DepartureVisualFocus>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(f=>f.gameObject.scene==scene);
        pending=new List<Mesh>();finished=new List<VisualGeometryCache>();files=new Dictionary<string,SourceFile>();bindings=new List<Binding>();colliderBindings=new List<ColliderBinding>();errors=new List<string>();progress=new Progress();started=Stopwatch.GetTimestamp();
        var seen=new HashSet<Mesh>();
        foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(filter.gameObject.scene!=scene||filter.sharedMesh==null)continue;
            var mesh=filter.sharedMesh;var renderer=filter.GetComponent<MeshRenderer>();
            bindings.Add(new Binding {Filter=filter,Mesh=mesh,Renderer=renderer,Materials=renderer!=null?renderer.sharedMaterials:null,Pose=filter.transform.localToWorldMatrix});
            if(!seen.Add(mesh))continue;
            if(mesh.vertexCount==0){progress.skippedEmpty++;continue;}
            string path=AssetDatabase.GetAssetPath(mesh);
            if(string.IsNullOrEmpty(path)||!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long localId)) {progress.skippedTransient++;continue;}
            if(!files.ContainsKey(path))files.Add(path,new SourceFile {Path=path,File=HashFile(path),Meta=HashFile(path+".meta"),Dependency=AssetDatabase.GetAssetDependencyHash(path).ToString()});
            pending.Add(mesh);if(!mesh.isReadable)progress.unreadable++;
        }
        foreach(var collider in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(collider.gameObject.scene==scene)colliderBindings.Add(new ColliderBinding {Collider=collider,Mesh=collider.sharedMesh});
        progress.total=pending.Count;
        EnsureFolder(Folder);
        return Status();
    }
    // Synchronous bounded chunks: NO update/delayCall/task callbacks survive return. Caller keeps broker lease for each call.
    public static string BakeNext(int maximumMeshes=8,int maximumMilliseconds=1000)
    {
        RequireAuthority();Require(pending!=null,"Call Begin first");
        var timer=Stopwatch.StartNew();int count=0;
        while(progress.processed<pending.Count && count<Math.Max(1,maximumMeshes) && (count==0||timer.ElapsedMilliseconds<Math.Max(1,maximumMilliseconds)))
        {
            Mesh mesh=pending[progress.processed];count++;
            try
            {
                string path=AssetDatabase.GetAssetPath(mesh);
                Require(files.TryGetValue(path,out var source),"Source asset disappeared");
                Require(AssetDatabase.GetAssetDependencyHash(path).ToString()==source.Dependency,"Source dependency changed during bake: "+path);
                Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long localId),"Source identity unavailable");
                string destination=Folder+"/"+guid+"_"+localId+".asset";
                var cache=AssetDatabase.LoadAssetAtPath<VisualGeometryCache>(destination);
                if(cache!=null && cache.Matches(mesh)&&cache.SourceDependencyHash==source.Dependency)progress.reused++;
                else
                {
                    var data=Extract(mesh);data.Validate();data.Bvh=VisualTriangleBvh.Build(data.Vertices,data.Submeshes);
                    bool create=cache==null;
                    if(create){Require(!File.Exists(destination),"Cache destination occupied");cache=ScriptableObject.CreateInstance<VisualGeometryCache>();}
                    cache.Version=VisualGeometryCache.CurrentVersion;cache.Source=mesh;cache.SourceGuid=guid;cache.SourceLocalId=localId;cache.SourceDependencyHash=source.Dependency;cache.Data=data;
                    RequireAuthority();
                    if(create)AssetDatabase.CreateAsset(cache,destination);else EditorUtility.SetDirty(cache);
                    AssetDatabase.SaveAssetIfDirty(cache);progress.created++;
                }
                finished.Add(cache);progress.vertices+=cache.Data.Vertices.LongLength;progress.triangles+=cache.Data.Bvh.Triangles.LongLength;progress.payloadBytes+=cache.Data.PayloadBytes;
            }
            catch(Exception e){progress.failed++;errors.Add(mesh.name+": "+e.Message);}
            progress.processed++;
        }
        progress.complete=progress.processed==progress.total;
        return Status();
    }
    // Only final catalog/focus binding is committed here, after all caches and original-bindings checks complete.
    public static string Finish()
    {
        RequireAuthority();Require(pending!=null&&progress.complete,"BakeNext must complete all meshes");Require(progress.failed==0,"Bake contains errors; catalog not bound. Inspect Status then Abort/retry.");
        foreach(var file in files.Values)
            Require(HashFile(file.Path)==file.File&&HashFile(file.Path+".meta")==file.Meta&&AssetDatabase.GetAssetDependencyHash(file.Path).ToString()==file.Dependency,"Source/importer changed: "+file.Path);
        foreach(var b in bindings)
        {
            Require(b.Filter!=null&&b.Filter.sharedMesh==b.Mesh&&b.Filter.transform.localToWorldMatrix==b.Pose,"Source mesh/pose binding changed");
            if(b.Renderer!=null)Require(b.Renderer.sharedMaterials.SequenceEqual(b.Materials),"Source material binding changed");
        }
        foreach(var c in colliderBindings)Require(c.Collider!=null&&c.Collider.sharedMesh==c.Mesh,"Collider binding changed");
        progress.sourceBindingsPreserved=true;
        var catalog=AssetDatabase.LoadAssetAtPath<VisualGeometryCatalog>(CatalogPath);
        if(catalog==null){Require(!File.Exists(CatalogPath),"Catalog destination occupied");catalog=ScriptableObject.CreateInstance<VisualGeometryCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
        catalog.Caches=finished.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
        focus.GeometryCatalog=catalog;EditorUtility.SetDirty(focus);
        if(PrefabUtility.IsPartOfPrefabInstance(focus))PrefabUtility.RecordPrefabInstancePropertyModifications(focus);
        EditorSceneManager.MarkSceneDirty(focus.gameObject.scene);Require(EditorSceneManager.SaveScene(focus.gameObject.scene),"Scene save failed");
        progress.bound=true;string result=Status();Release();return result;
    }
    public static string Status()
    {
        if(progress==null)return "{\"active\":false}";
        progress.elapsedMs=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;progress.errors=errors.ToArray();return JsonUtility.ToJson(progress,true);
    }
    public static string Abort() {RequireAuthority();string result=Status();Release();return result;}
    static void Release(){pending=null;finished=null;files=null;bindings=null;colliderBindings=null;errors=null;focus=null;progress=null;}
    public static VisualMeshData Extract(Mesh mesh)
    {
        // Official MeshApiExamples typed attribute extraction; Editor reader bypasses CPU-readability flag without importer edits.
        using(var input=MeshUtility.AcquireReadOnlyMeshData(mesh))
        {
            var src=input[0];var result=new VisualMeshData {Submeshes=new VisualSubmesh[src.subMeshCount]};
            using(var vertices=new NativeArray<Vector3>(src.vertexCount,Allocator.Temp)) {src.GetVertices(vertices);result.Vertices=vertices.ToArray();}
            if(src.HasVertexAttribute(VertexAttribute.Normal))using(var normals=new NativeArray<Vector3>(src.vertexCount,Allocator.Temp)){src.GetNormals(normals);result.Normals=normals.ToArray();}
            if(src.HasVertexAttribute(VertexAttribute.TexCoord0))using(var uv=new NativeArray<Vector2>(src.vertexCount,Allocator.Temp)){src.GetUVs(0,uv);result.Uv=uv.ToArray();}
            for(int sub=0;sub<src.subMeshCount;sub++)
            {
                var descriptor=src.GetSubMesh(sub);Require(descriptor.topology==MeshTopology.Triangles,"Unsupported mesh topology");
                // MeshData supports UInt16/UInt32 source conversion and applies descriptor.baseVertex.
                using(var indices=new NativeArray<int>(descriptor.indexCount,Allocator.Temp))
                {src.GetIndices(indices,sub,true);result.Submeshes[sub]=new VisualSubmesh {Indices=indices.ToArray()};}
            }
            return result;
        }
    }
    static void RequireAuthority()
    {
        var auth=JsonUtility.FromJson<Authority>(File.ReadAllText(Path.Combine(Application.dataPath,"../.state/WRITER-AUTHORITY.json")));
        Require(auth!=null&&auth.status=="ACTIVE"&&auth.Native_inflight&&auth.sole_writer_thread==Thread,"Expected sole writer thread ACTIVE with Native_inflight=true");
        Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating,"Settled Editor edit mode required");
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static string HashFile(string path){if(!File.Exists(path))return "";using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
    static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\','/'));Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path))),"Cache folder creation failed");
    }
}
#endif
