using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChooGuard.Seeded.Unity.GraphicsResume
{
    // This diagnostic only attaches in an explicitly requested standalone Player.
    public sealed class PlayerGraphicsProof : MonoBehaviour
    {
        string destination;
        readonly JObject images=new JObject();
        readonly JObject report=new JObject();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Attach()
        {
#if !UNITY_EDITOR
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-chooguard-graphics-proof");
            if(index<0||index+1>=args.Length)return;
            var target=Path.GetFullPath(args[index+1]);
            if(!target.Replace('\\','/').Contains("/output/simulator/platform-ktx-unity/diagnostics/graphics-resume/"))
                throw new InvalidOperationException("Graphics proof destination must remain in isolated diagnostics.");
            var go=new GameObject("Explicit standalone graphics proof");DontDestroyOnLoad(go);
            go.AddComponent<PlayerGraphicsProof>().destination=target;
#endif
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(destination);
            report["schema"]="chooguard.standalone-graphics-proof.v1";
            report["started_utc"]=DateTime.UtcNow.ToString("O");report["proof"]="Actual standalone Unity Player scene frames and offscreen graphics; not Editor PlayMode or field fidelity.";
            report["unity_version"]=Application.unityVersion;report["is_editor"]=Application.isEditor;
            report["graphics_device"]=SystemInfo.graphicsDeviceType.ToString();report["graphics_device_name"]=SystemInfo.graphicsDeviceName;
            report["graphics_device_version"]=SystemInfo.graphicsDeviceVersion;report["images"]=images;
            Write();
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null){report["passed"]=false;report["reason"]="Standalone Player graphics device is Null.";Write();Application.Quit(4);yield break;}
            var sim=FindFirstObjectByType<PlatformSeededSimulator>();
            if(sim==null){report["passed"]=false;report["reason"]="Simulator was not loaded.";Write();Application.Quit(4);yield break;}
            sim.Prepare();sim.enabled=false;sim.StartRun("20261013",false);
            report["source_assembly_hash"]=(string)JObject.Parse(Resources.Load<TextAsset>("SourceManifest").text)["runtime_fingerprint"];
            report["staging_fingerprint"]=report["source_assembly_hash"].DeepClone();
            report["full_world_fingerprint"]=sim.WorldSnapshot?.Fingerprint;
            for(int frame=0;frame<4;frame++)yield return null;
            Capture(sim.Player.View,"fps-platform.png",1280,720);
            var observer=new GameObject("Standalone verification overview",typeof(Camera));
            var camera=observer.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.55f,.68f,.81f);camera.farClipPlane=1000;
            camera.transform.position=new Vector3(72,18,65);camera.transform.LookAt(new Vector3(60,0,15));
            Capture(camera,"platform-ktx-overview.png",1600,1000);
            if(sim.WorldSnapshot!=null)
            {
                var places=sim.State.Entities.Values.Where(e=>e.Kind=="station-walk-place").OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray();
                if(places.Length>0)
                {
                    var center=new Vector3((float)places.Average(e=>e.X),1.55f,(float)places.Average(e=>e.Z));
                    foreach(int index in new[]{0,places.Length/3,places.Length*2/3}.Distinct())
                    {
                        var place=places[index];camera.transform.position=new Vector3((float)place.X,(float)place.Y+1.55f,(float)place.Z);
                        camera.transform.LookAt(center+Vector3.right*.1f);Capture(camera,"station-source-place-"+index+".png",1600,1000);
                    }
                }
            }
            foreach(var car in sim.Source.train.cars)
            {
                var outside=PlatformNavigation.V(car.door.outside)+Vector3.up*1.55f;
                var inside=PlatformNavigation.V(car.door.inside)+Vector3.up*1.55f;
                camera.transform.position=outside;camera.transform.LookAt(inside);
                Capture(camera,"car-"+car.number+"-door.png",1280,720);
                camera.transform.position=inside;camera.transform.LookAt(inside+(inside-outside).normalized*3);
                Capture(camera,"car-"+car.number+"-interior.png",1280,720);
            }
            Destroy(observer);
            var harness=new GameObject("Explicit standalone runtime acceptance");
            WholeWorldAcceptanceRunner whole=null;AcceptancePlayModeRunner legacy=null;
            if(sim.WorldSnapshot!=null){whole=harness.AddComponent<WholeWorldAcceptanceRunner>();whole.Simulator=sim;}
            else{legacy=harness.AddComponent<AcceptancePlayModeRunner>();legacy.Simulator=sim;}
            float deadline=Time.realtimeSinceStartup+160;
            while((whole!=null?whole.Result:legacy.Result)==null&&Time.realtimeSinceStartup<deadline)yield return null;
            report["runtime_acceptance"]=(whole!=null?whole.Result:legacy.Result)??new JObject{["passed"]=false,["reason"]="Standalone runtime acceptance exceeded 160 seconds."};
            Capture(sim.Player.View,"fps-playmode.png",1280,720);
            report["completed_utc"]=DateTime.UtcNow.ToString("O");report["real_graphics_captured"]=images.Count>=2;
            report["passed"]=(bool?)report["runtime_acceptance"]["passed"]==true&&images.Count>=2;
            sim.StopRun();Write();Application.Quit((bool)report["passed"]?0:4);
        }
        void Write(){File.WriteAllText(Path.Combine(destination,"player-graphics-proof.json"),report.ToString());}
        void Capture(Camera camera,string filename,int width,int height)
        {
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);target.Create();
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
            if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("Current render pipeline does not support explicit single-camera capture.");
            RenderPipeline.SubmitRenderRequest(camera,request);
            var prior=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
            var bytes=texture.EncodeToPNG();File.WriteAllBytes(Path.Combine(destination,filename),bytes);
            using(var sha=SHA256.Create())images[filename]=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            RenderTexture.active=prior;Destroy(texture);target.Release();Destroy(target);Write();
        }
    }
}
