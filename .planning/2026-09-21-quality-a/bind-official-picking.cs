ChooGuard.Editor.MvpOfficialStationPickBinder.BindExisting();
var w=UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Mvp.MvpWorkspace>();
w.SelectFloor(0);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(w.gameObject.scene);
return new{saved=UnityEditor.SceneManagement.EditorSceneManager.SaveScene(w.gameObject.scene),receipt=System.IO.File.ReadAllText("art/world/official-station-picking-receipt.json")};
