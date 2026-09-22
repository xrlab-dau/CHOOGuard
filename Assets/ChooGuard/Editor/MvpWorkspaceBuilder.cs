using System;
using System.IO;
using ChooGuard.App.Mvp;
using ChooGuard.Presentation.Commands;
using ChooGuard.Presentation.Input;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor
{
    public static class MvpWorkspaceBuilder
    {
        public const string ScenePath="Assets/ChooGuard/Scenes/MvpWorkspace.unity";
        private static void ConfigureAssets(MvpWorkspace workspace)
        {
            workspace.Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ChooGuard/Settings/ImportedAssets/Fonts/NotoSansCJKkr SDF.asset");
            if(workspace.Font==null) throw new InvalidOperationException("한국어 글꼴 자료를 찾을 수 없습니다.");
            workspace.TrainIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ChooGuard/ThirdParty/Icons/Lucide/train-front.png");
            workspace.TeamIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ChooGuard/ThirdParty/Icons/Lucide/users.png");
            workspace.LocationIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ChooGuard/ThirdParty/Icons/Lucide/map-pin.png");
            EditorUtility.SetDirty(workspace);
        }
        private static void PrepareKoreanText(MvpWorkspace workspace)
        {
            if(workspace.Preview!=null) workspace.Preview.Localize(workspace.Font);
            var characters=new System.Text.StringBuilder("명령 검토 승인 처리결과 재조회 원래 명령 재시도 이전 다음 닫기 역무 소방 의료 훈련팀 승강장 대합실 출입구 대기 이동 반영 위치 유지 접수됨 거절됨 처리 대기 훈련 상태가 변경되었습니다 명령을 다시 검토해 주세요 기존 처리결과와 다른 명령입니다 가상 훈련 데이터에만 적용되는 명령입니다 상태 변경 다시 검토 필요 처리 중 확인 종료 제한 사유 목적지 추가 참고자료 안내 불확실합니다 조건 내용 일치하지 않습니다 저장된 기기에 저장합니다 0123456789");
            foreach(var text in workspace.GetComponentsInChildren<TMP_Text>(true)) { text.font=workspace.Font; characters.Append(text.text); EditorUtility.SetDirty(text); }
            if(workspace.Font.atlasPopulationMode==AtlasPopulationMode.Dynamic) workspace.Font.TryAddCharacters(characters.ToString(),out string missing);
            EditorUtility.SetDirty(workspace.Font);
        }
        [MenuItem("ChooGuard/MVP/Build Training Workspace")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build in Edit mode.");
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var loaded=SceneManager.GetSceneAt(i);
                if(loaded.path!=ScenePath) continue;
                foreach(var root in loaded.GetRootGameObjects())
                    if(root.GetComponent<MvpWorkspace>()!=null)
                    {
                        ConfigureAssets(root.GetComponent<MvpWorkspace>());
                        root.GetComponent<MvpWorkspace>().RefreshLayout();
                        PrepareKoreanText(root.GetComponent<MvpWorkspace>());
                        EditorSceneManager.MarkSceneDirty(loaded);
                        if (!EditorSceneManager.SaveScene(loaded, ScenePath)) throw new IOException("Could not save owned MVP scene.");
                        Selection.activeGameObject=root; return;
                    }
                throw new InvalidOperationException("Existing scene has no workspace; refusing replacement.");
            }
            if(File.Exists(ScenePath)) throw new InvalidOperationException("Scene already exists. Open it explicitly; refusing overwrite.");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChooGuard/Presentation/Commands/CommandPreview.prefab");
            var actions=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/ChooGuard/Presentation/Input/Operations.inputactions");
            if(prefab==null || actions==null) throw new InvalidOperationException("Command preview/input assets are required.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                    throw new InvalidOperationException("Save the existing untitled scene to a new path before additive MVP creation. No existing scene was changed.");
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var root=new GameObject("ChooGuard Training Workspace");
                var workspace=root.AddComponent<MvpWorkspace>();
                ConfigureAssets(workspace);
                workspace.EnsureBuilt();
                workspace.RefreshLayout();
                var events=new GameObject("Workspace EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(root.transform,false);
                var router=events.AddComponent<InputContextRouter>();
                router.Configure(events.GetComponent<EventSystem>(),events.GetComponent<InputSystemUIInputModule>(),actions);
                workspace.Router=router;
                var canvas=root.transform.Find("WorkspaceCanvas");
                var overlay=new GameObject("CommandModalLayer",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));
                overlay.transform.SetParent(canvas,false);
                var rect=overlay.GetComponent<RectTransform>(); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
                var modalCanvas=overlay.GetComponent<Canvas>(); modalCanvas.overrideSorting=true; modalCanvas.sortingOrder=20;
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                instance.transform.SetParent(overlay.transform,false);
                workspace.Preview=instance.GetComponent<CommandPreviewPresenter>();
                instance.SetActive(false);
                PrepareKoreanText(workspace);
                EditorUtility.SetDirty(workspace);
                Directory.CreateDirectory("Assets/ChooGuard/Scenes");
                if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new IOException("Could not save owned MVP scene.");
                Selection.activeGameObject=root;
                Debug.Log("MVP workspace saved: "+ScenePath+". Open this scene alone for its single EventSystem; existing scenes were preserved.");
            }
            finally { if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }
    }
}
