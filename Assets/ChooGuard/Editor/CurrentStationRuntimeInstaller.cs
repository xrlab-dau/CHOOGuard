using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.Seeded.Unity;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor
{
    /// <summary>Scoped scene application only. Does not run gameplay, validation, captures or a scene rebuild.</summary>
    public static class CurrentStationRuntimeInstaller
    {
        const string ScenePath="Assets/ChooGuard/Scenes/FpsStation.unity";
        const string ResourceRoot="Assets/ChooGuard/Seeded/Resources/";
        const string RequestPath=".state/APPLY-GAME-PROVIDER.json";
        const string ReceiptPath=".planning/2026-10-16-game-provider-npc/APPLIED.json";
        static double nextCheck;

        [InitializeOnLoadMethod]
        static void Register(){EditorApplication.update-=TryPending;EditorApplication.update+=TryPending;}

        static void TryPending()
        {
            if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+1;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(RequestPath))return;
            File.Move(RequestPath,RequestPath+".applying");
            try{Apply();File.Delete(RequestPath+".applying");}
            catch(Exception error)
            {
                File.WriteAllText(ReceiptPath,new JObject{["status"]="FAILED",["error"]=error.ToString()}.ToString());
                Debug.LogException(error);
            }
        }

        [MenuItem("ChooGuard/Play/Apply current station provider runtime")]
        public static void Apply()
        {
            var authority=JObject.Parse(File.ReadAllText(".state/WRITER-AUTHORITY.json"));
            if((string)authority["status"]!="ACTIVE"||(string)authority["sole_writer_agent"]!="/root"||(string)authority["lease_task"]!="2026-10-16-game-provider-npc")
                throw new InvalidOperationException("A scoped root runtime writer lease is required.");
            var inventory=AssetDatabase.LoadAssetAtPath<TextAsset>(ResourceRoot+"WorldSnapshotInventory.json");
            var manifest=AssetDatabase.LoadAssetAtPath<TextAsset>(ResourceRoot+"WorldSnapshotPrefabManifest.json");
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ChooGuard/Settings/ImportedAssets/Fonts/NotoSansCJKkr SDF.asset");
            var crowd=AssetDatabase.LoadAssetAtPath<CrowdCatalog>("Assets/ChooGuard/Art/Emergency/Crowd/CrowdCatalog.asset");
            if(inventory==null||manifest==null||font==null||crowd==null)throw new InvalidOperationException("Current inventory, manifest, Korean font and existing crowd catalog must be imported.");
            var expected=JObject.Parse(manifest.text);
            if(Hash(ScenePath)!=(string)expected["sourceSceneSha256"])
                throw new InvalidOperationException("The saved scene changed after inventory extraction. Refresh current input; do not restore an older scene.");
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var byId=new Dictionary<string,GameObject>(StringComparer.Ordinal);
            foreach(var root in scene.GetRootGameObjects())
                foreach(var node in root.GetComponentsInChildren<Transform>(true))
                    byId.Add(GlobalObjectId.GetGlobalObjectIdSlow(node.gameObject).targetObjectId.ToString(CultureInfo.InvariantCulture),node.gameObject);
            var rows=(JArray)JObject.Parse(inventory.text)["objects"];
            var references=new List<GameObject>();var ids=new List<string>();
            foreach(JObject row in rows)
            {
                string fileId=(string)row["sceneFileId"];
                if(!byId.TryGetValue(fileId,out var go))throw new InvalidOperationException("Current object is missing: "+fileId);
                var matrix=((JArray)row["worldTransform"]["localToWorldMatrix4x4"]).Select(x=>(float)x).ToArray();
                if(!StationWorldAdapter.MatrixNear(go.transform.localToWorldMatrix,StationWorldAdapter.Matrix(matrix),.002f))
                    throw new InvalidOperationException("Unsaved geometry transform differs: "+go.name+". Preserve it and refresh the input inventory.");
                references.Add(go);ids.Add((string)row["id"]);
            }
            if(GameObject.Find("CHOOGuard Provider Runtime")!=null)throw new InvalidOperationException("Runtime is already installed; do not duplicate it.");
            var runtime=new GameObject("CHOOGuard Provider Runtime");runtime.SetActive(false);SceneManager.MoveGameObjectToScene(runtime,scene);
            var adapter=runtime.AddComponent<StationWorldAdapter>();adapter.LiveObjects=references.ToArray();adapter.LiveObjectIds=ids.ToArray();
            adapter.SnapshotInventory=inventory;adapter.SnapshotManifest=manifest;adapter.LiveFloorLevels=new[]{0f,7f};
            var simulator=runtime.AddComponent<PlatformSeededSimulator>();simulator.WorldSnapshot=adapter;simulator.ProviderMenuFont=font;
            simulator.PassengerModels=crowd.FemalePassengers.Concat(crowd.MalePassengers).Where(p=>p!=null).ToArray();
            simulator.InitialSeed="20261016";simulator.Population=110;simulator.UseSubscription=false;
            runtime.AddComponent<StationPlayPanel>();runtime.SetActive(true);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save current station runtime.");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/ChooGuard/Scenes/Bootstrap.unity"); // Product entry always offers the requested mode choice.
            AssetDatabase.SaveAssets();
            var receipt=new JObject{["status"]="APPLIED_AND_SAVED",["scene"]=ScenePath,["sceneSha256"]=Hash(ScenePath),["sourceInventorySceneSha256"]=(string)expected["sourceSceneSha256"],
                ["currentSourceObjects"]=references.Count,["runtimeRoot"]=runtime.name,["passengerModels"]=simulator.PassengerModels.Length,["providerFont"]=AssetDatabase.GetAssetPath(font),
                ["playModeStartScene"]="Bootstrap mode selection",["agentPlayOrValidationRun"]=false,["sourceGeometryRebuilt"]=false,["modelRequests"]=0};
            File.WriteAllText(ReceiptPath,receipt.ToString());
            authority=JObject.Parse(File.ReadAllText(".state/WRITER-AUTHORITY.json"));
            authority["latest_scene_sha256"]=(string)receipt["sceneSha256"];authority["current_scene_state"]="CURRENT_STATION_PROVIDER_NPC_RUNTIME_APPLIED_AND_SAVED";
            authority["runtime_integration_receipt"]=ReceiptPath;File.WriteAllText(".state/WRITER-AUTHORITY.json",authority.ToString()+"\n");
            Debug.Log("CG_CURRENT_PROVIDER_RUNTIME_APPLIED objects="+references.Count+" models="+simulator.PassengerModels.Length+" scene="+ScenePath);
        }

        static string Hash(string path)
        {using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(File.ReadAllBytes(path)).Select(x=>x.ToString("x2",CultureInfo.InvariantCulture)));}
    }
}
