using ChooGuard.App.Mvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.Editor
{
    public static class MvpHazardFieldBinder
    {
        [MenuItem("ChooGuard/MVP/Bind existing hazard field view")]
        public static void BindExisting()
        {
            var station=Object.FindFirstObjectByType<MvpStationView>(FindObjectsInactive.Include);
            var workspace=Object.FindFirstObjectByType<MvpWorkspace>(FindObjectsInactive.Include);
            var bridge=Object.FindFirstObjectByType<MvpPhysicsBridge>(FindObjectsInactive.Include);
            if(station==null || workspace==null || bridge==null)throw new System.InvalidOperationException("Station, Workspace, Bridge are required in the open scene.");
            var view=workspace.GetComponent<MvpHazardFieldView>();
            if(view==null)view=Undo.AddComponent<MvpHazardFieldView>(workspace.gameObject);
            Undo.RecordObject(view,"Bind FDS analysis field");view.Station=station;view.Workspace=workspace;view.Bridge=bridge;view.Font=workspace.Font;
            EditorUtility.SetDirty(view);EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        }
    }
}
