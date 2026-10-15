#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChooGuard.Seeded.Unity.Editor
{
    public static class StandaloneSceneBuilder
    {
        const string Root="Assets/Simulator";
        [MenuItem("ChooGuard Seeded/Build standalone station simulator")]
        public static void Build()
        {
            var project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            if(!project.Replace('\\','/').EndsWith("/output/simulator/platform-ktx-unity",StringComparison.Ordinal))
                throw new InvalidOperationException("Builder only operates in explicitly isolated platform-ktx-unity project");
            Directory.CreateDirectory(Root+"/Prepared");Directory.CreateDirectory(Root+"/Scenes");Directory.CreateDirectory(project+"/Verification");
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root+"/Prepared/PlatformRenderer.asset");if(renderer==null){renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,Root+"/Prepared/PlatformRenderer.asset");}
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Prepared/PlatformPipeline.asset");if(pipeline==null){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,Root+"/Prepared/PlatformPipeline.asset");}GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var manifest=JObject.Parse(Resources.Load<TextAsset>("SourceManifest").text);
            var go=new GameObject("CHOOGuard seeded station simulator");var simulator=go.AddComponent<PlatformSeededSimulator>();
            if(manifest["world_snapshot"] is JObject snapshot)
            {
                var roots=(JObject)snapshot["runtime_roots"];
                var imported=WorldSnapshotImporter.Import(new WorldSnapshotImporter.Request{
                    InventoryAssetPath=(string)snapshot["inventory_asset_path"],GeometrySceneAssetPath=(string)snapshot["geometry_scene_asset_path"],
                    ExpectedInventorySha256=(string)snapshot["inventory_sha256"],ExpectedSourceSceneSha256=(string)snapshot["source_scene_sha256"],ExpectedGeometrySceneSha256=(string)snapshot["geometry_scene_sha256"],
                    TrainRootId=(string)roots["ktx"],PlatformRootIds=roots["platforms"].Select(x=>(string)x).ToArray(),StationFloorIds=(roots["station_floors"]??new JArray()).Select(x=>(string)x).ToArray(),
                    StationRegionIds=(roots["station_regions"]??new JArray()).Select(x=>(string)x).ToArray(),CandidateSupportIds=(roots["candidate_supports"]??new JArray()).Select(x=>(string)x).ToArray()});
                var adapter=go.AddComponent<StationWorldAdapter>();adapter.SnapshotPrefab=imported.Prefab;adapter.SnapshotInventory=imported.Inventory;adapter.SnapshotManifest=imported.Manifest;simulator.WorldSnapshot=adapter;
                File.WriteAllText(project+"/Verification/world-snapshot-import.json",imported.Receipt.ToString(Formatting.Indented));
            }
            else
            {
            var platformPaths=new[]{"Assets/ChooGuard/Art/OfficialBusanStation/OfficialBusanStation_PlatformsParking.fbx","Assets/ChooGuard/Art/OfficialBusanStation/OfficialBusanStation_Layer0PlatformsDetails.fbx"};
            var prefabs=new List<GameObject>();
            foreach(var path in platformPaths)
            {
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;if(importer!=null&&!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(source==null)throw new FileNotFoundException(path);
                var instance=UnityEngine.Object.Instantiate(source);instance.name=source.name;
                foreach(var meshRenderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var rows=manifest["platform_materials"][meshRenderer.name] as JArray;if(rows==null)continue;
                    var materials=new List<Material>();
                    foreach(var row in rows)
                    {
                        string materialPath=AssetDatabase.GUIDToAssetPath((string)row["guid"]);long localId=(long)row["fileID"];
                        var material=AssetDatabase.LoadAllAssetsAtPath(materialPath).OfType<Material>().FirstOrDefault(m=>AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string guid,out long id)&&id==localId);
                        materials.Add(material);
                    }
                    if(materials.Count>0&&materials.All(m=>m!=null))meshRenderer.sharedMaterials=materials.ToArray();
                }
                string prefabPath=Root+"/Prepared/"+source.name+".prefab";var prefab=PrefabUtility.SaveAsPrefabAsset(instance,prefabPath);prefabs.Add(prefab);UnityEngine.Object.DestroyImmediate(instance);
            }
            var ktxPath="Assets/ChooGuard/Art/Fps/KTX/ktx-single-10car-left-open.fbx";
            var ktxImporter=AssetImporter.GetAtPath(ktxPath) as ModelImporter;if(ktxImporter!=null&&!ktxImporter.isReadable){ktxImporter.isReadable=true;ktxImporter.SaveAndReimport();}
            var ktx=AssetDatabase.LoadAssetAtPath<GameObject>(ktxPath);
            if(ktx==null)throw new FileNotFoundException(ktxPath);
            simulator.KtxPrefab=ktx;simulator.PlatformPrefabs=prefabs.ToArray();
            }
            EditorSceneManager.SaveScene(scene,Root+"/Scenes/PlatformSeededSimulator.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"/Scenes/PlatformSeededSimulator.unity",true)};
            simulator.Prepare();simulator.StartRun("20261013",false);
            var report=simulator.Probe();report["isolated_project"]=project;report["staging_fingerprint"]=(string)manifest["runtime_fingerprint"];report["source_scene_modified"]=false;report["proof"]="Editor executes isolated geometry/nav/physics preparation. Gameplay timing and training effectiveness are not certified.";
            report["physical_door_passage"]=simulator.Navigation.PhysicalDoorProbes();
            var unknown=simulator.State.Entities.Values.Where(e=>e.Kind=="decoration").Select(e=>e.Id).ToArray();report["render_only_untyped_count"]=unknown.Length;report["render_only_untyped_ids"]=new JArray(unknown);
            // A deterministic recorded-observation replay check, not an assertion that
            // Unity physics independently reproduces crowd trajectories.
            simulator.StartRun("20261013",true);
            for(int tick=0;tick<20;tick++)simulator.Tick();
            string tape=simulator.LastRecordingPath;string recorded=simulator.State.Digest();simulator.StopRun();bool loaded=simulator.LoadReplay(tape);
            if(loaded)for(int tick=0;tick<21&&simulator.Running;tick++)simulator.Tick();
            report["recorded_observation_replay"]=new JObject{["loaded"]=loaded,["recorded_digest"]=recorded,["replayed_digest"]=simulator.State.Digest(),["passed"]=loaded&&recorded==simulator.State.Digest()&&simulator.Status.StartsWith("Replay complete",StringComparison.Ordinal),["ticks"]=20,["tape"]=tape,["status"]=simulator.Status};
            File.WriteAllText(project+"/Verification/geometry-nav-probes.json",report.ToString(Formatting.Indented));
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
            {
                Capture(simulator.Player.View,project+"/Verification/fps-platform.png",1280,720);
                var observer=new GameObject("Verification overview",typeof(Camera));observer.transform.position=new Vector3(72,18,65);observer.transform.LookAt(new Vector3(60,0,15));var camera=observer.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.55f,.68f,.81f);camera.farClipPlane=1000;
                Capture(camera,project+"/Verification/platform-ktx-overview.png",1600,1000);UnityEngine.Object.DestroyImmediate(observer);
            }
            else File.WriteAllText(project+"/Verification/SCREENSHOT-NOT-CAPTURED.txt","Graphics device is Null. Geometry/physics probes are not visual acceptance. Re-run graphics-enabled Build to capture views.\n");
            simulator.StopRun();simulator.Navigation.Dispose();simulator.WorldSnapshot?.Dispose();
            // Native NavMesh data/links are not scene-owned. Editor validation
            // must release them explicitly before the separate PlayMode bake.
            // The persisted scene contains immutable prefab references and configuration;
            // runtime generation repeats after Play. Transient validation geometry is not saved.
            AssetDatabase.SaveAssets();Debug.Log("CG_SEEDED_ISOLATED_BUILD "+project+" nav="+simulator.Navigation.Hash+" door_links="+simulator.Navigation.DoorLinks.Count);
            EditorSceneManager.OpenScene(Root+"/Scenes/PlatformSeededSimulator.unity",OpenSceneMode.Single);
        }
        public static void Capture(Camera camera,string path,int width,int height)
        {
            var prior=camera.targetTexture;var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=prior;
            UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
#endif
