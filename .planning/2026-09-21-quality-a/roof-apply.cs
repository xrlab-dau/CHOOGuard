var s=UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Mvp.MvpStationView>();
var refs=new UnityEngine.Object[]{s.WholeEnvelope,s.ViewCamera,s.Navigation,s.PlatformAnchor,s.ConcourseAnchor,s.ExitAnchor,s.IncidentMarker,s.CrowdRoot};
ChooGuard.Editor.MvpEvidenceBuildingBuilder.ApplyExisting(s.transform);
bool same=System.Linq.Enumerable.SequenceEqual(refs,new UnityEngine.Object[]{s.WholeEnvelope,s.ViewCamera,s.Navigation,s.PlatformAnchor,s.ConcourseAnchor,s.ExitAnchor,s.IncidentMarker,s.CrowdRoot});
if(!same)throw new System.InvalidOperationException("Station references changed");
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
return new{saved=UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s.gameObject.scene),stationReferencesUnchanged=same,applyReceipt=System.IO.File.ReadAllText("art/world/building-restoration-apply-receipt.json")};
