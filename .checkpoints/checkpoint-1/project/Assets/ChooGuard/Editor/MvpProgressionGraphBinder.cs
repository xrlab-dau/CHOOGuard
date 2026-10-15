using ChooGuard.App.Mvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.Editor
{
    public static class MvpProgressionGraphBinder
    {
        [MenuItem("ChooGuard/MVP/Bind Progression Graph")]
        public static void BindExisting()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Use Edit mode.");
            var workspace=Object.FindFirstObjectByType<MvpWorkspace>();if(workspace==null)throw new System.InvalidOperationException("Open existing workspace scene.");
            var graph=workspace.GetComponent<MvpProgressionGraph>()??workspace.gameObject.AddComponent<MvpProgressionGraph>();
            graph.GraphAsset=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ChooGuard/Art/Progression/response-flow.json");
            if(!graph.Initialize())throw new System.InvalidOperationException(graph.StatusReason);
            EditorUtility.SetDirty(graph);EditorSceneManager.MarkSceneDirty(workspace.gameObject.scene);EditorSceneManager.SaveScene(workspace.gameObject.scene);
        }
    }
}
