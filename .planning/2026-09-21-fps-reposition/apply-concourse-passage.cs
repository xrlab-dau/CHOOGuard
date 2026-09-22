if(UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Edit required");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity")throw new System.Exception("Expected FPS scene");
string backup="Assets/ChooGuard/Scenes/SessionBackup-FpsConcourseWall-20260921.unity";
if(!System.IO.File.Exists(backup)){UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);if(!UnityEditor.AssetDatabase.CopyAsset(scene.path,backup))throw new System.Exception("Backup failed");}
var spec=UnityEngine.JsonUtility.FromJson<ChooGuard.Editor.FpsStationSceneBuilder.BuildSpec>(System.IO.File.ReadAllText(".planning/2026-09-21-fps-reposition/fps-scene-build-spec.json"));var opening=spec.openings[spec.openings.Length-1];
var root=UnityEngine.GameObject.Find("FPSWorld");var target=root.transform.Find(opening.meshPath);var mf=target.GetComponent<UnityEngine.MeshFilter>();var col=target.GetComponent<UnityEngine.MeshCollider>();
string before=UnityEditor.AssetDatabase.GetAssetPath(mf.sharedMesh);
ChooGuard.Editor.FpsSourceOpening.Cut(mf,new UnityEngine.Bounds(opening.center,opening.size),opening.euler,opening.generatedAsset);
col.sharedMesh=null;col.sharedMesh=mf.sharedMesh;UnityEngine.Physics.SyncTransforms();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
return new{saved=UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene),before,after=UnityEditor.AssetDatabase.GetAssetPath(mf.sharedMesh),triangles=mf.sharedMesh.triangles.Length/3,scope="Authored underground connector void through source road support; not measured station passage"};
