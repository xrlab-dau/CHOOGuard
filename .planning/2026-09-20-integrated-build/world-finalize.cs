string stage="bind";
try {
 var w=UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Mvp.MvpWorkspace>();
 var v=UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Mvp.MvpStationView>();
 foreach(var t in v.GetComponentsInChildren<UnityEngine.Transform>(true)) t.gameObject.layer=29;
 w.RefreshLayout(); v.Bind(w); var d=w.GetComponent<ChooGuard.App.Mvp.MvpTrainingDirector>();d.Bind(w);
 UnityEditor.EditorUtility.SetDirty(w);UnityEditor.EditorUtility.SetDirty(v);UnityEditor.EditorUtility.SetDirty(d);
 stage="save";UnityEditor.AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(v.gameObject.scene);
 bool saved=UnityEditor.SceneManagement.EditorSceneManager.SaveScene(v.gameObject.scene,ChooGuard.Editor.MvpWorkspaceBuilder.ScenePath);
 return new{saved=saved,dirty=v.gameObject.scene.isDirty,stage=stage};
} catch(System.Exception e){ return new{stage=stage,error=e.ToString()}; }
