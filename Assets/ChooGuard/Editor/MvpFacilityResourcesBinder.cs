using ChooGuard.App.Mvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.Editor
{
    public static class MvpFacilityResourcesBinder
    {
        [MenuItem("ChooGuard/MVP/Bind Facility Resources")]
        public static void BindExisting()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Use Edit mode.");
            var workspace=Object.FindFirstObjectByType<MvpWorkspace>();if(workspace==null)throw new System.InvalidOperationException("Open existing workspace.");
            var resources=workspace.GetComponent<MvpFacilityResources>()??workspace.gameObject.AddComponent<MvpFacilityResources>();
            resources.ResourceAsset=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ChooGuard/Art/Resources/facility-resources.json");
            if(!resources.Initialize())throw new System.InvalidOperationException(resources.StatusReason);
            EditorUtility.SetDirty(resources);EditorSceneManager.MarkSceneDirty(workspace.gameObject.scene);EditorSceneManager.SaveScene(workspace.gameObject.scene);
        }
    }
}
