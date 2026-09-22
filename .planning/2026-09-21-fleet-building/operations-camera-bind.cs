ChooGuard.Editor.MvpAgencyDispatchBuilder.EditorBindExisting();
ChooGuard.Editor.MvpCameraRigBinder.Bind();
var w=UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Mvp.MvpWorkspace>();
var s=UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Mvp.MvpStationView>();
s.Navigation.enabled=true;w.SelectFloor(0);
UnityEditor.EditorUtility.SetDirty(w);UnityEditor.EditorUtility.SetDirty(s.Navigation);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(w.gameObject.scene);
bool saved=UnityEditor.SceneManagement.EditorSceneManager.SaveScene(w.gameObject.scene);
return new{saved,scene=w.gameObject.scene.path,dirty=w.gameObject.scene.isDirty,routeAsset=w.GetComponent<ChooGuard.App.Mvp.MvpAgencyDispatchController>().RouteAsset.name,surface=s.Navigation.Surface!=null};
