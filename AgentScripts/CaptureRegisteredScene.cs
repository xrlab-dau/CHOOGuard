using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// Captures the actual Scene-view camera with the reference aspect, not a stretched dock viewport.
public static class CaptureRegisteredScene
{
    public static void Main(string[] args)
    {
        if(args.Length!=1)throw new ArgumentException("Provide a project-relative PNG output path.");
        var view=SceneView.lastActiveSceneView;
        if(view==null)throw new InvalidOperationException("No active Scene view.");
        Capture(view.camera,args[0],1280,720,"Actual SceneView.camera render",null);
    }

    // Explicit calibrated pose; the user's SceneView and player camera are never moved.
    public static void Calibrated(string[] args)
    {
        if(args.Length!=2)throw new ArgumentException("Provide pose JSON and PNG output paths.");
        var pose=JObject.Parse(File.ReadAllText(args[0]));
        int width=(int)pose["width"],height=(int)pose["height"];
        float fov=(float)pose["verticalFov"];
        if(width<=0||height<=0||!(fov>0&&fov<180))throw new ArgumentException("Invalid calibrated projection.");
        var go=new GameObject("Calibrated reference camera"){hideFlags=HideFlags.HideAndDontSave};
        try
        {
            var camera=go.AddComponent<Camera>();
            var view=SceneView.lastActiveSceneView;
            if(view!=null)camera.CopyFrom(view.camera);
            camera.enabled=false;camera.orthographic=false;camera.usePhysicalProperties=false;
            camera.fieldOfView=fov;camera.nearClipPlane=.08f;
            go.transform.SetPositionAndRotation(Vector(pose["position"]),
                Quaternion.LookRotation(Vector(pose["forward"]),Vector(pose["up"])));
            Capture(camera,args[1],width,height,"Calibrated temporary camera",pose);
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }

    static Vector3 Vector(JToken value)=>new Vector3((float)value[0],(float)value[1],(float)value[2]);

    static void Capture(Camera camera,string output,int width,int height,string source,JObject calibration)
    {
        string path=Path.GetFullPath(output);Directory.CreateDirectory(Path.GetDirectoryName(path));
        var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
        float previousAspect=camera.aspect;Matrix4x4 previousProjection=camera.projectionMatrix;
        var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        Texture2D image=null;
        try
        {
            camera.targetTexture=target;camera.aspect=(float)width/height;camera.ResetProjectionMatrix();
            camera.Render();RenderTexture.active=target;
            image=new Texture2D(width,height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply(false,false);
            File.WriteAllBytes(path,image.EncodeToPNG());
            File.WriteAllText(path+".json",JsonConvert.SerializeObject(new {
                source,width,height,aspect=camera.aspect,verticalFov=camera.fieldOfView,
                position=new[]{camera.transform.position.x,camera.transform.position.y,camera.transform.position.z},
                euler=new[]{camera.transform.eulerAngles.x,camera.transform.eulerAngles.y,camera.transform.eulerAngles.z},
                forward=new[]{camera.transform.forward.x,camera.transform.forward.y,camera.transform.forward.z},
                up=new[]{camera.transform.up.x,camera.transform.up.y,camera.transform.up.z},
                calibration,note="Projection matches the recorded dimensions. Compare with undistorted reference pixels; not an as-built accuracy certificate."
            },Formatting.Indented));
        }
        finally
        {
            camera.targetTexture=previousTarget;camera.aspect=previousAspect;camera.projectionMatrix=previousProjection;
            RenderTexture.active=previousActive;if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(target);
        }
        Debug.Log("REFERENCE_SCENE_CAPTURE "+path);
    }
}
