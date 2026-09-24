using ChooGuard.App.Fps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.EditorTools
{
    // Bootstrap 씬에 진입점을 꽂는다. 빌드 설정에는 Bootstrap 과 FpsStation 이 모두 등록돼 있지만
    // 둘을 잇는 코드가 없어, 빌드해서 실행하면 Bootstrap 에 머물렀다(2026-09-24 확인).
    public static class BootstrapEntryBuilder
    {
        private const string BootstrapPath="Assets/ChooGuard/Scenes/Bootstrap.unity";
        private const string MarkerName="Bootstrap Marker";

        [MenuItem("ChooGuard/수직 슬라이스/Bootstrap 진입점 연결")]
        public static void BuildMenu(){Build(true);}

        public static bool Build(bool saveScene)
        {
            var scene=EditorSceneManager.OpenScene(BootstrapPath,OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[진입] Bootstrap 씬을 열지 못했습니다 · "+BootstrapPath);return false;}

            GameObject marker=null;
            foreach(var root in scene.GetRootGameObjects())
                if(root!=null&&root.name==MarkerName){marker=root;break;}
            if(marker==null)
            {
                marker=new GameObject(MarkerName);
                Undo.RegisterCreatedObjectUndo(marker,"Bootstrap Marker 생성");
            }

            var entry=marker.GetComponent<SceneEntryPoint>();
            if(entry==null)entry=Undo.AddComponent<SceneEntryPoint>(marker);
            entry.SceneName="FpsStation";
            // 즉시 넘기지 않는다. 첫 화면이 두 프레임 만에 사라지면 표시할 것을 표시할 수 없고,
            // 기존 시험(CSBOOT0101)이 카메라·캔버스를 찾지 못해 깨진다.
            entry.LoadOnStart=false;
            entry.WaitForInput=true;
            EditorUtility.SetDirty(entry);

            EditorSceneManager.MarkSceneDirty(scene);
            if(saveScene)EditorSceneManager.SaveScene(scene);
            Debug.Log("[진입] Bootstrap → "+entry.SceneName+" 연결 완료");
            return true;
        }
    }
}
