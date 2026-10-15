#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChooGuard.Seeded.Unity.GraphicsResume.Editor
{
    public static class BuildGraphicsPlayer
    {
        public static void Build()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            if(!project.Replace('\\','/').EndsWith("/output/simulator/platform-ktx-unity",StringComparison.Ordinal))
                throw new InvalidOperationException("Graphics proof build requires the isolated simulator project.");
            string destination=project+"/diagnostics/graphics-resume/PlatformGraphics.app";
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{"Assets/Simulator/Scenes/PlatformSeededSimulator.unity"},
                locationPathName=destination,target=BuildTarget.StandaloneOSX,
                options=BuildOptions.Development
            });
            var receipt=new JObject{
                ["schema"]="chooguard.standalone-graphics-build.v1",
                ["project"]=project,["player"]=destination,["unity_version"]=Application.unityVersion,
                ["source_assembly_hash"]=(string)JObject.Parse(Resources.Load<TextAsset>("SourceManifest").text)["runtime_fingerprint"],
                ["result"]=report.summary.result.ToString(),["errors"]=report.summary.totalErrors,["warnings"]=report.summary.totalWarnings,
                ["bytes"]=report.summary.totalSize,["duration_seconds"]=report.summary.totalTime.TotalSeconds,
                ["graphics_apis"]=new JArray(PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneOSX).Select(x=>x.ToString())),
                ["messages"]=new JArray(report.steps.SelectMany(x=>x.messages).Where(x=>x.type==LogType.Error||x.type==LogType.Exception).Select(x=>x.content))
            };
            File.WriteAllText(project+"/diagnostics/graphics-resume/build-receipt.json",receipt.ToString());
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Standalone graphics diagnostic build failed.");
        }
    }
}
#endif
