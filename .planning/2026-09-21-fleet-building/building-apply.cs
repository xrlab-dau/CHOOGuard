var s=UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Mvp.MvpStationView>();
ChooGuard.Editor.MvpEvidenceBuildingBuilder.BuildCityWithExclusions(s.transform);
ChooGuard.Editor.MvpEvidenceBuildingBuilder.ApplyExisting(s.transform);
ChooGuard.Editor.MvpCameraRigBinder.Bind();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
bool saved=UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s.gameObject.scene);
return new{saved,scene=s.gameObject.scene.path,city=s.transform.Find("도시 공개지형")!=null,depots=s.transform.Find("도시 공개지형/Evidence restored fire facilities")!=null,facade=s.WholeEnvelope.Find("Evidence restored station facade")!=null,cameraSurface=s.Navigation.Surface!=null,placementReceipt=System.IO.File.Exists("art/world/building-depot-placement-receipt.json")};
