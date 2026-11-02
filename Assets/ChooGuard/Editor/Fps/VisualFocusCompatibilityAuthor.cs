#if UNITY_EDITOR
// Reuses reviewed AuthorVisualFocus.Bake: official RenderTexture.active/GetRTPixels example,
// Graphics.Blit/ReadPixels and Alpha8 conversion. Readback/Apply/restore also reuses
// Core17.3 LookDev Environment.GetLatLongThumbnailTexture (Unity Companion License). See StaticBatchGap/Foundation + report.
// MeshApiExamples e82668f26fbf615d3ee4b1a5941c8fe167d3d485 original mesh/object transform pairing (MIT).
// Existing CPU geometry assets are referenced only. NO extraction, BVH build or geometry asset writes.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChooGuard.App.Fps.TrainDeparture;
public static class VisualFocusCompatibilityAuthor
{
    const string CacheFolder="Assets/ChooGuard/Art/TrainDeparture/FocusAlpha";
    [Serializable] sealed class Authority { public string status="",sole_writer_thread=""; public bool Native_inflight=false; }
    [Serializable] sealed class Progress
    {
        public int sourceBindings,geometryCaches,missingGeometry,transientMeshes,alphaBefore,alphaRequired,alphaProcessed,alphaCreated,alphaReused;
        public bool complete,bound,sourceBindingsPreserved;
        public string[] requiredTextures,missingGeometryNames;
    }
    sealed class Original { public MeshFilter Filter; public Mesh Mesh; public MeshRenderer Renderer; public Material[] Materials; public Matrix4x4 Pose; public Vector3 LocalPosition,LocalScale; public Quaternion LocalRotation; }
    sealed class ColliderOriginal { public MeshCollider Collider; public Mesh Mesh; }
    static DepartureVisualFocus focus;
    static List<Original> originals;
    static List<ColliderOriginal> colliders;
    static List<VisualSourceBinding> sources;
    static List<VisualAlphaCache> alpha;
    static List<Texture2D> pending;
    static Material[] sampledMaterials;
    static Dictionary<string,string> hashes;
    static Progress progress;
    // Import into the stable ChooGuard.Editor assembly. run_script wrappers only CALL these APIs.
    // NO callbacks/tasks outlive a call. Begin after reload reuses saved alpha assets without bulk geometry rebake.
    public static string Begin()
    {
        Guard();Require(progress==null,"Call Finish or Abort before Begin");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Require(scene.path=="Assets/ChooGuard/Scenes/TrainDepartureDemo.unity"&&scene.isLoaded,"Expected active Demo edit scene");
        var selected=UnityEngine.Object.FindObjectsByType<DepartureVisualFocus>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.gameObject.scene==scene);
        Require(selected.GeometryCatalog!=null,"Existing baked catalog required");
        var caches=new Dictionary<Mesh,VisualGeometryCache>();
        foreach(var c in selected.GeometryCatalog.Caches)if(c!=null&&c.Source!=null)caches[c.Source]=c;
        var localSources=new List<VisualSourceBinding>();var localOriginals=new List<Original>();var missing=new List<string>();int transient=0;
        foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(r.gameObject.scene!=scene)continue;
            Require(!r.isPartOfStaticBatch,"Edit scene is already combined; original source ownership cannot be inferred");
            var f=r.GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null)continue;
            var m=f.sharedMesh;
            localOriginals.Add(new Original {Filter=f,Mesh=m,Renderer=r,Materials=r.sharedMaterials,Pose=f.transform.localToWorldMatrix,LocalPosition=f.transform.localPosition,LocalRotation=f.transform.localRotation,LocalScale=f.transform.localScale});
            if(m.vertexCount==0)continue;
            if(!EditorUtility.IsPersistent(m)){transient++;continue;}
            if(!caches.TryGetValue(m,out var c)||!c.Matches(m)){missing.Add(r.name+" -> "+m.name);continue;}
            localSources.Add(new VisualSourceBinding {Renderer=r,Source=m});
        }
        // Do not replace existing source bindings with a partial mapping.
        Require(missing.Count==0,"Missing existing geometry caches: "+string.Join("; ",missing.Take(12))+" ("+missing.Count+")");
        var localAlpha=new List<VisualAlphaCache>(selected.AlphaCaches??Array.Empty<VisualAlphaCache>());
        var textures=new HashSet<Texture2D>();var materials=new HashSet<Material>();var existingAlpha=localAlpha.ToArray();
        foreach(var o in localOriginals)
        {
            if(o.Renderer.name.Contains("__KtxOpaqueTexelDepth"))continue;
            if(o.Materials.Any(m=>m!=null&&m.shader!=null&&m.shader.name.Contains("OpaqueTexelDepth")))continue;
            foreach(var m in o.Materials)if(m!=null&&materials.Add(m))
            {var texture=VisualAlphaCache.MissingSource(m,existingAlpha);if(texture!=null)textures.Add(texture);}
        }
        var localPending=textures.OrderBy(t=>AssetDatabase.GetAssetPath(t),StringComparer.Ordinal).ThenBy(t=>t.name,StringComparer.Ordinal).ToList();
        var localHashes=new Dictionary<string,string>();
        foreach(var texture in localPending)
        {
            string path=AssetDatabase.GetAssetPath(texture);Require(!string.IsNullOrEmpty(path),"Required alpha texture is not persistent");
            if(!localHashes.ContainsKey(path)){localHashes.Add(path,Hash(path));localHashes.Add(path+".meta",Hash(path+".meta"));}
        }
        Require(AssetDatabase.IsValidFolder(CacheFolder),"Reviewed existing FocusAlpha folder required");
        colliders=UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c.gameObject.scene==scene).Select(c=>new ColliderOriginal {Collider=c,Mesh=c.sharedMesh}).ToList();
        sampledMaterials=materials.ToArray();
        focus=selected;originals=localOriginals;sources=localSources;alpha=localAlpha;pending=localPending;hashes=localHashes;
        progress=new Progress {sourceBindings=sources.Count,geometryCaches=selected.GeometryCatalog.Caches.Length,missingGeometry=missing.Count,transientMeshes=transient,
            alphaBefore=alpha.Count,alphaRequired=pending.Count,requiredTextures=pending.Select(AssetDatabase.GetAssetPath).ToArray(),missingGeometryNames=missing.ToArray(),complete=pending.Count==0};
        return Status();
    }
    public static string Next(int maximumTextures=1,int maximumMilliseconds=1000)
    {
        Guard();Require(progress!=null,"Call Begin first");var timer=Stopwatch.StartNew();int count=0;
        while(progress.alphaProcessed<pending.Count&&count<Math.Max(1,maximumTextures)&&(count==0||timer.ElapsedMilliseconds<Math.Max(1,maximumMilliseconds)))
        {
            var source=pending[progress.alphaProcessed];var readable=Bake(source,out bool created);
            alpha.RemoveAll(c=>c!=null&&c.Source==source);alpha.Add(new VisualAlphaCache {Source=source,Readable=readable});
            progress.alphaProcessed++;if(created)progress.alphaCreated++;else progress.alphaReused++;count++;
        }
        progress.complete=progress.alphaProcessed==pending.Count;return Status();
    }
    public static string Finish()
    {
        Guard();Require(progress!=null&&progress.complete,"Complete Next first");
        foreach(var o in originals)Require(o.Filter!=null&&o.Renderer!=null&&o.Filter.sharedMesh==o.Mesh&&o.Filter.transform.localToWorldMatrix==o.Pose&&o.Filter.transform.localPosition==o.LocalPosition&&o.Filter.transform.localRotation==o.LocalRotation&&o.Filter.transform.localScale==o.LocalScale&&o.Renderer.sharedMaterials.SequenceEqual(o.Materials),"Source render/mesh/pose binding changed");
        foreach(var c in colliders)Require(c.Collider!=null&&c.Collider.sharedMesh==c.Mesh,"Collider source binding changed");
        foreach(var pair in hashes)Require(Hash(pair.Key)==pair.Value,"Source texture/importer changed: "+pair.Key);
        var finalAlpha=alpha.ToArray();
        foreach(var m in sampledMaterials)Require(VisualAlphaCache.MissingSource(m,finalAlpha)==null,"Alpha source still missing: "+m.name);
        progress.sourceBindingsPreserved=true;
        focus.SourceBindings=sources.ToArray();focus.AlphaCaches=finalAlpha;
        EditorUtility.SetDirty(focus);if(PrefabUtility.IsPartOfPrefabInstance(focus))PrefabUtility.RecordPrefabInstancePropertyModifications(focus);
        EditorSceneManager.MarkSceneDirty(focus.gameObject.scene);Require(EditorSceneManager.SaveScene(focus.gameObject.scene),"Scene save failed");
        progress.bound=true;string result=Status();Release();return result;
    }
    public static string Status(){return progress==null?"{\"active\":false}":JsonUtility.ToJson(progress,true);}
    public static string Abort(){Guard();string result=Status();Release();return result;}
    static void Release(){focus=null;originals=null;colliders=null;sources=null;alpha=null;pending=null;sampledMaterials=null;hashes=null;progress=null;}
    static Texture2D Bake(Texture2D source,out bool created)
    {
        Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long localId),"Source texture identity missing");
        string path=CacheFolder+"/"+guid+"_"+localId+".asset";var cached=AssetDatabase.LoadAssetAtPath<Texture2D>(path);created=false;
        if(cached!=null)
        {
            Require(cached.width==source.width&&cached.height==source.height&&cached.isReadable&&cached.wrapModeU==source.wrapModeU&&cached.wrapModeV==source.wrapModeV&&cached.filterMode==source.filterMode,"Stale alpha cache: "+path);
            return cached;
        }
        Require(!File.Exists(path),"Alpha cache destination occupied");
        var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);Texture2D rgba=null,output=null;
        try
        {
            Graphics.Blit(source,rt);RenderTexture.active=rt;
            rgba=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);
            rgba.ReadPixels(new Rect(0,0,source.width,source.height),0,0,false);rgba.Apply(false);
            var pixels=rgba.GetPixels32();var bytes=new byte[pixels.Length];for(int i=0;i<bytes.Length;i++)bytes[i]=pixels[i].a;
            output=new Texture2D(source.width,source.height,TextureFormat.Alpha8,false,true) {name=source.name+"_ReadableAlpha",filterMode=source.filterMode,wrapModeU=source.wrapModeU,wrapModeV=source.wrapModeV,wrapModeW=source.wrapModeW,anisoLevel=source.anisoLevel};
            output.SetPixelData(bytes,0);output.Apply(false,false);
            var raw=output.GetRawTextureData<byte>();Require(raw.Length==bytes.Length,"Alpha8 payload size mismatch");
            for(int i=0;i<bytes.Length;i++)Require(raw[i]==bytes[i],"Alpha8 source readback byte mismatch");
            foreach(int i in new[]{0,bytes.Length/2,bytes.Length-1})Require(Mathf.Abs(output.GetPixel(i%source.width,i/source.width).a-bytes[i]/255f)<.00001f,"Native Alpha8 sampling mismatch");
            Guard();AssetDatabase.CreateAsset(output,path);AssetDatabase.SaveAssetIfDirty(output);created=true;return output;
        }
        finally
        {
            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(rgba!=null)UnityEngine.Object.DestroyImmediate(rgba);
            if(output!=null&&!EditorUtility.IsPersistent(output))UnityEngine.Object.DestroyImmediate(output);
        }
    }
    static void Guard()
    {
        var a=JsonUtility.FromJson<Authority>(File.ReadAllText(Path.Combine(Application.dataPath,"../.state/WRITER-AUTHORITY.json")));
        Require(a!=null&&a.status=="ACTIVE"&&a.Native_inflight&&a.sole_writer_thread=="01a19bde-6b5f-7bf1-a13d-822ca0abe444","Native sole writer authority required");
        Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating,"Settled edit mode required");
    }
    static string Hash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream));}
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
#endif
