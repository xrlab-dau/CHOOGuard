#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Seeded.Unity.Editor
{
    public static class WorldSpawnDiagnostics
    {
        public static void Run()
        {
            WorldSnapshotImporter.RequireIsolatedProject();
            EditorSceneManager.OpenScene("Assets/Simulator/Scenes/PlatformSeededSimulator.unity",OpenSceneMode.Single);
            var sim=UnityEngine.Object.FindFirstObjectByType<PlatformSeededSimulator>();var report=new JObject();
            try{sim.Prepare();report["prepared"]=true;}catch(Exception e){report["preparation_error"]=e.ToString();}
            report["navigation"]=sim.Navigation?.Report.DeepClone();report["snapshot"]=sim.WorldSnapshot?.Report.DeepClone();
            var samples=new JArray();
            foreach(var car in sim.Source.train.cars)
            {
                var point=PlatformNavigation.V(car.door.outside);bool found=NavMesh.SamplePosition(point,out var nav,4,NavMesh.AllAreas);
                var overlaps=Physics.OverlapCapsule(point+Vector3.up*.32f,point+Vector3.up*1.42f,.27f,~(1<<2),QueryTriggerInteraction.Ignore);
                samples.Add(new JObject{["car"]=car.number,["desired"]=PlatformSeededSimulator.Point(point),["nav_found"]=found,["nav_point"]=found?PlatformSeededSimulator.Point(nav.position):null,["safe_spawn"]=sim.Navigation.TrySpawn(point,out var feet),["feet"]=PlatformSeededSimulator.Point(feet),["overlaps"]=new JArray(overlaps.Take(30).Select(c=>new JObject{["name"]=c.name,["source_id"]=sim.WorldSnapshot?.ResolveSourceObject(c),["bounds_center"]=PlatformSeededSimulator.Point(c.bounds.center),["bounds_size"]=PlatformSeededSimulator.Point(c.bounds.size)}))});
            }
            report["platform_samples"]=samples;
            var triangulation=NavMesh.CalculateTriangulation();report["nav_vertices"]=triangulation.vertices.Length;
            Directory.CreateDirectory("Verification");File.WriteAllText("Verification/world-spawn-diagnostics.json",report.ToString(Newtonsoft.Json.Formatting.Indented));
            sim.Navigation?.Dispose();sim.WorldSnapshot?.Dispose();UnityEditor.EditorApplication.Exit(0);
        }
    }
}
#endif
