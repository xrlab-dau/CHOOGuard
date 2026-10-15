#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.Seeded.Unity.Editor
{
    [InitializeOnLoad]
    public static class PlayModeAcceptance
    {
        const string Key="CHOOGuard.IsolatedAcceptance.Pending";
        static double deadline;
        static AcceptancePlayModeRunner runner;static Car04CandidateProbeRunner candidate;static WholeWorldAcceptanceRunner wholeWorld;
        static PlayModeAcceptance(){EditorApplication.update+=Update;}
        // Launch without -quit: this entrypoint exits only after actual PlayMode
        // frames complete or a bounded timeout. SessionState survives domain reload.
        public static void Run()
        {
            SessionState.SetString(Key+".mode","main");StandaloneSceneBuilder.Build();SessionState.SetBool(Key,true);SessionState.SetString(Key+".started",DateTime.UtcNow.ToString("O"));
            EditorApplication.delayCall+=()=>EditorApplication.EnterPlaymode();
        }
        public static void RunCar04()
        {
            SessionState.SetString(Key+".mode","car04");StandaloneSceneBuilder.Build();SessionState.SetBool(Key,true);SessionState.SetString(Key+".started",DateTime.UtcNow.ToString("O"));EditorApplication.delayCall+=()=>EditorApplication.EnterPlaymode();
        }
        static void Update()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(deadline==0)deadline=EditorApplication.timeSinceStartup+120;
            if(EditorApplication.timeSinceStartup>deadline){Finish(new JObject{["passed"]=false,["reason"]="Actual PlayMode acceptance exceeded 120-second owner deadline"});return;}
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(runner==null&&candidate==null&&wholeWorld==null)
            {
                var sim=UnityEngine.Object.FindFirstObjectByType<PlatformSeededSimulator>();if(sim==null)return;
                if(SessionState.GetString(Key+".mode","")=="car04"){candidate=new GameObject("Explicit Car04 candidate diagnostic").AddComponent<Car04CandidateProbeRunner>();candidate.Simulator=sim;}
                else if(sim.WorldSnapshot!=null){wholeWorld=new GameObject("Explicit whole-world isolated acceptance").AddComponent<WholeWorldAcceptanceRunner>();wholeWorld.Simulator=sim;}
                else{runner=new GameObject("Explicit isolated acceptance").AddComponent<AcceptancePlayModeRunner>();runner.Simulator=sim;}return;
            }
            var result=runner!=null?runner.Result:wholeWorld!=null?wholeWorld.Result:candidate.Result;if(result==null)return;Finish(result);
        }
        static string Hash(string path){using(var algorithm=SHA256.Create())return BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        static void Finish(JObject report)
        {
            SessionState.SetBool(Key,false);
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));Directory.CreateDirectory(project+"/Verification");
            report["started_utc"]=SessionState.GetString(Key+".started","");report["completed_utc"]=DateTime.UtcNow.ToString("O");report["graphics_device"]=SystemInfo.graphicsDeviceType.ToString();
            var simulator=runner!=null?runner.Simulator:wholeWorld!=null?wholeWorld.Simulator:null;
            if(simulator!=null&&SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
            {
                StandaloneSceneBuilder.Capture(simulator.Player.View,project+"/Verification/fps-playmode.png",1280,720);
                var observer=new GameObject("Acceptance overview",typeof(Camera));observer.transform.position=new Vector3(72,18,65);observer.transform.LookAt(new Vector3(60,0,15));var camera=observer.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.55f,.68f,.81f);camera.farClipPlane=1000;
                StandaloneSceneBuilder.Capture(camera,project+"/Verification/overview-playmode.png",1600,1000);UnityEngine.Object.DestroyImmediate(observer);report["real_graphics_captured"]=true;report["graphics_artifacts"]=new JObject{["fps-playmode.png"]=Hash(project+"/Verification/fps-playmode.png"),["overview-playmode.png"]=Hash(project+"/Verification/overview-playmode.png")};
            }
            else report["real_graphics_captured"]=false;
            File.WriteAllText(project+"/Verification/"+(SessionState.GetString(Key+".mode","")=="car04"?"car04-candidate-probes.json":wholeWorld!=null?"wholeworld-playmode-probes.json":"playmode-probes.json"),report.ToString(Formatting.Indented));
            EditorApplication.Exit((bool?)report["passed"]==true?0:4);
        }
    }
}
#endif
