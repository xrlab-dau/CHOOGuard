using ChooGuard.App.Mvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.Editor
{
    public static class MvpJevOperationalKernelBinder
    {
        [MenuItem("ChooGuard/MVP/Bind Internal Operational Kernel")]
        public static void BindExisting()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Use Edit mode.");
            var workspace=Object.FindFirstObjectByType<MvpWorkspace>();if(workspace==null)throw new System.InvalidOperationException("Open existing workspace.");
            var kernel=workspace.GetComponent<MvpJevOperationalKernel>()??workspace.gameObject.AddComponent<MvpJevOperationalKernel>();
            EditorUtility.SetDirty(kernel);EditorSceneManager.MarkSceneDirty(workspace.gameObject.scene);EditorSceneManager.SaveScene(workspace.gameObject.scene);
        }
    }
}
