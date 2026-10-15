using ChooGuard.App.Mvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.Editor
{
    public static class MvpAgencyDispatchBuilder
    {
        [MenuItem("ChooGuard/MVP/Bind Agency Dispatch")]
        public static void EditorBindExisting()
        {
            var workspace=Object.FindFirstObjectByType<MvpWorkspace>();if(workspace==null)throw new System.InvalidOperationException("Open existing MVP workspace scene first.");
            var c=workspace.GetComponent<MvpAgencyDispatchController>()??workspace.gameObject.AddComponent<MvpAgencyDispatchController>();
            c.Workspace=workspace;c.Director=workspace.GetComponent<MvpTrainingDirector>();c.Station=c.Director.Station;
            c.RouteAsset=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ChooGuard/Art/AgencyDispatch/agency-routes.json");
            c.SourceCatalogAsset=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ChooGuard/Art/AgencyDispatch/agency-catalog.json");
            c.FireEnginePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChooGuard/Art/ResponseSet/FireEngine119.fbx");c.AmbulancePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChooGuard/Art/ResponseSet/Ambulance119.fbx");
            if(c.RouteAsset==null||c.SourceCatalogAsset==null||c.FireEnginePrefab==null||c.AmbulancePrefab==null)throw new System.InvalidOperationException("Agency assets missing.");
            if(c.Station.WholeEnvelope!=null)foreach(string name in new[]{"소방 지휘 차량","구급 차량"}){var old=c.Station.WholeEnvelope.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);}
            foreach(string name in new[]{"AgencyVehicle_fire-1","AgencyVehicle_medical-1"}){var old=c.Station.transform.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);}
            var view=workspace.GetComponent<MvpAgencyDispatchView>()??workspace.gameObject.AddComponent<MvpAgencyDispatchView>();view.Controller=c;
            EditorUtility.SetDirty(c);EditorUtility.SetDirty(view);EditorSceneManager.MarkSceneDirty(workspace.gameObject.scene);EditorSceneManager.SaveScene(workspace.gameObject.scene);
        }
    }
}
