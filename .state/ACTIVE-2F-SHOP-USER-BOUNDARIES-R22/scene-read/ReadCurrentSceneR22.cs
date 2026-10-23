using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class ReadCurrentSceneR22
{
    const string Root = "/Users/um-yunsang/CHOOGuard";
    const string Work = ".state/ACTIVE-2F-SHOP-USER-BOUNDARIES-R22/scene-read";
    const string Thread = "01a13cb4-b738-7823-bf16-30a39ea3d8c6";
    const string Scope = "C180_ROOT_R22_READ_REQUESTED_SCENEVIEW";
    static JArray V(Vector3 x) { return new JArray(x.x,x.y,x.z); }
    static string P(Transform x) { return x.parent == null ? x.name : P(x.parent)+"/"+x.name; }
    static void Guard()
    {
        var a=JObject.Parse(File.ReadAllText(Root+"/.state/WRITER-AUTHORITY.json"));
        if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||
           (string)a["sole_writer_thread"]!=Thread||(string)a["scope"]!=Scope)
            throw new Exception("Exact read-only R22 lease required");
        var g=JObject.Parse(File.ReadAllText(Root+"/"+(string)a["delegated_author_lease"]));
        if((string)g["grant_nonce"]!=(string)a["scene_worker_grant_nonce"])
            throw new Exception("Exact FIFO grant nonce required");
    }
    public static string FocusRequestedView()
    {
        Guard();var sv=SceneView.lastActiveSceneView;
        if(sv==null)throw new Exception("No current SceneView");
        sv.Focus();sv.Repaint();
        return "Existing requested SceneView focused for visible source inspection; no camera, selection, Scene or Assets changes.";
    }
    public static string ReadVisibleSceneView()
    {
        Guard();var sv=SceneView.lastActiveSceneView;
        if(sv==null)throw new Exception("No current SceneView");
        var rect=sv.position;
        string output=Root+"/"+Work+"/CURRENT-VISIBLE-SCENEVIEW-R22.png";
        string crop=String.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0},{1},{2},{3}",(int)rect.x,(int)rect.y,(int)rect.width,(int)rect.height);
        var start=new System.Diagnostics.ProcessStartInfo {
            FileName="/usr/sbin/screencapture",Arguments="-x -o -R"+crop+" \""+output+"\"",
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true
        };
        var p=System.Diagnostics.Process.Start(start);bool ended=p.WaitForExit(10000);
        var result=new JObject { ["utc"]=DateTime.UtcNow.ToString("o"),["exited"]=ended,
            ["exit_code"]=ended?new JValue(p.ExitCode):JValue.CreateNull(),["crop"]=crop,
            ["stderr"]=ended?p.StandardError.ReadToEnd():"In flight; no termination",
            ["image"]=output,["Scene_Assets_changed"]=false,["camera_or_selection_moved"]=false };
        Guard();File.WriteAllText(Root+"/"+Work+"/VISIBLE-SCENEVIEW-CAPTURE-RESULT-R22.json",result.ToString());
        return result.ToString();
    }
    public static string RenderRequestedCamera()
    {
        Guard();var sv=SceneView.lastActiveSceneView;
        if(sv==null)throw new Exception("No current SceneView");
        var camera=sv.camera;
        var rt=RenderTexture.GetTemporary(1440,640,24,RenderTextureFormat.ARGB32);
        var old=RenderTexture.active;Texture2D image=null;
        try
        {
            var request=new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest {destination=rt};
            if(!UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera,request))
                throw new Exception("Actual URP does not support requested camera render");
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=rt;image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0,false);image.Apply(false,false);
            string output=Work+"/CURRENT-SCENEVIEW-CAMERA-SOURCE-R22.png";
            File.WriteAllBytes(Root+"/"+output,image.EncodeToPNG());
            var result=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["image"]=output,
                ["method"]="Actual SceneView camera pose and pipeline; explicit single camera render request",
                ["camera_position"]=V(camera.transform.position),["camera_forward"]=V(camera.transform.forward),
                ["Scene_Assets_changed"]=false,["camera_or_selection_moved"]=false};
            Guard();File.WriteAllText(Root+"/"+Work+"/REQUESTED-CAMERA-SOURCE-RESULT-R22.json",result.ToString());
            return result.ToString();
        }
        finally{RenderTexture.active=old;if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);}
    }
    public static string Read()
    {
        Guard();
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)
            throw new Exception("Current stopped FpsStation editing scene required");
        var all=new JArray();
        foreach(var mf in Resources.FindObjectsOfTypeAll<MeshFilter>())
        {
            if(mf.gameObject.scene!=scene||!mf.gameObject.activeInHierarchy||mf.sharedMesh==null)continue;
            string path=P(mf.transform);
            if(!path.Contains("OfficialStation")&&!path.StartsWith("FPSWorld/Architecture/2F/",StringComparison.Ordinal))continue;
            var rr=mf.GetComponent<Renderer>();if(rr==null||!rr.enabled)continue;
            all.Add(new JObject {
                ["id"]=GlobalObjectId.GetGlobalObjectIdSlow(mf.gameObject).ToString(),["path"]=path,
                ["mesh"]=AssetDatabase.GetAssetPath(mf.sharedMesh),
                ["matrix"]=new JArray(Enumerable.Range(0,16).Select(i=>mf.transform.localToWorldMatrix[i])),
                ["bounds_min"]=V(rr.bounds.min),["bounds_max"]=V(rr.bounds.max),
                ["materials"]=new JArray(rr.sharedMaterials.Select(m=>m==null?null:AssetDatabase.GetAssetPath(m)))
            });
        }
        var sv=SceneView.lastActiveSceneView;
        var view=new JObject { ["exists"]=sv!=null };
        if(sv!=null)
        {
            view["position"]=new JArray(sv.position.x,sv.position.y,sv.position.width,sv.position.height);
            view["pivot"]=V(sv.pivot);view["rotation"]=new JArray(sv.rotation.x,sv.rotation.y,sv.rotation.z,sv.rotation.w);
            view["size"]=sv.size;view["orthographic"]=sv.orthographic;
            var camera=sv.camera;
            view["camera_position"]=V(camera.transform.position);
            view["camera_forward"]=V(camera.transform.forward);
            var rt=camera.targetTexture;
            view["current_render_target_exists"]=rt!=null;
            if(rt!=null)
            {
                RenderTexture old=RenderTexture.active;Texture2D image=null;
                try
                {
                    RenderTexture.active=rt;
                    image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                    image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0,false);image.Apply(false,false);
                    File.WriteAllBytes(Root+"/"+Work+"/CURRENT-REQUESTED-SCENEVIEW-R22.png",image.EncodeToPNG());
                    view["image"]=Work+"/CURRENT-REQUESTED-SCENEVIEW-R22.png";
                    view["capture_method"]="read existing SceneView camera render texture; no re-render or camera changes";
                }
                finally {RenderTexture.active=old;if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
            }
        }
        var result=new JObject {
            ["utc"]=DateTime.UtcNow.ToString("o"),["scene"]=scene.path,["scene_dirty"]=scene.isDirty,
            ["requested_source_inspection"]=true,["Scene_Assets_changed"]=false,
            ["camera_or_selection_moved"]=false,["scene_view"]=view,["live_references"]=all
        };
        Guard();File.WriteAllText(Root+"/"+Work+"/ACTUAL-SCENE-AND-VIEW-R22.json",result.ToString(Formatting.None));
        return new JObject { ["status"]="ACTUAL_REQUESTED_SCENE_SOURCE_READ",["references"]=all.Count,["view"]=view,["scene_dirty"]=scene.isDirty,["Scene_Assets_changed"]=false }.ToString();
    }
}
