using System;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.StationFireTutorial;
using ChooGuard.App.Fps.Tutorial;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChooGuard.Editor
{
    public static class StationFireTutorialSceneBuilder
    {
        public const string ScenePath="Assets/ChooGuard/Scenes/StationFireTutorial.unity";
        public const string Art="Assets/ChooGuard/Art/StationFireTutorial";
        const string Crowd="Assets/ChooGuard/Art/Emergency/Crowd/Prefabs/";
        [MenuItem("ChooGuard/Tutorial/Build station fire tutorial")]
        public static void Build()
        {
            Directory.CreateDirectory(Art+"/Equipment");AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(Art+"/Equipment/extinguisher-detailed.fbx") as ModelImporter;
            if(importer==null)throw new FileNotFoundException("Source-derived extinguisher is missing");
            importer.isReadable=true;importer.addCollider=false;importer.preserveHierarchy=true;importer.importAnimation=false;importer.SaveAndReimport();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("역무원 화재대응 튜토리얼");var lesson=root.AddComponent<StationFireTutorialSession>();
            lesson.KoreanFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ChooGuard/Settings/ImportedAssets/Fonts/NotoSansCJKkr SDF.asset");
            if(lesson.KoreanFont==null)throw new FileNotFoundException("Existing Korean UI font missing");
            lesson.ExtinguisherPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Equipment/extinguisher-detailed.fbx");
            lesson.CharOverlayShader=AssetDatabase.LoadAssetAtPath<Shader>(Art+"/Equipment/FireCharOverlay.shader");
            lesson.PassengerPrefabs=new[]{"Business_Male_02","Female_Adult_01","Female_Adult_04","Business_Female_01"}.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>(Crowd+n+".prefab")).ToArray();
            lesson.StaffPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Crowd+"Team_Operations_Lead.prefab")??lesson.PassengerPrefabs[0];
            lesson.SpecialistPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Crowd+"Team_Fire_Lead.prefab");
            lesson.PersonController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/ChooGuard/Art/Emergency/Crowd/Person_Male.controller");
            lesson.FireLoop=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ChooGuard/ThirdParty/Audio/Station/fire_loop.wav");
            lesson.ConfirmSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ChooGuard/ThirdParty/Audio/Interface/confirmation_001.ogg");
            lesson.GuideVoice=Enumerable.Range(0,9).Select(i=>AssetDatabase.LoadAssetAtPath<AudioClip>(Art+"/Audio/guide-"+i+".wav")).ToArray();
            lesson.ReplyVoice=Enumerable.Range(0,8).Select(i=>AssetDatabase.LoadAssetAtPath<AudioClip>(Art+"/Audio/reply-"+i+".wav")).ToArray();
            if(lesson.PassengerPrefabs.Any(p=>p==null)||lesson.StaffPrefab==null||lesson.SpecialistPrefab==null)throw new FileNotFoundException("Existing actor model missing");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);
            var catalog=AssetDatabase.LoadAssetAtPath<TutorialCatalog>(TrainTutorialSceneBuilder.CatalogPath);
            if(catalog==null)throw new FileNotFoundException("Existing tutorial catalog missing");
            var categories=catalog.Categories.Where(c=>c.Id!="station-fire-response").ToList();
            categories.Add(new TutorialCategory{Id="station-fire-response",Title="역무원 화재대응",Description="기존 역사에서 승객 안내와 Q 보고, 비치 소화기 사용과 후속 상황 전달을 연습합니다.",SceneName="StationFireTutorial",ExpectedMinSeconds=420,ExpectedMaxSeconds=600});
            catalog.Categories=categories.ToArray();EditorUtility.SetDirty(catalog);
            var builds=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).ToList();builds.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=builds.ToArray();
            AssetDatabase.SaveAssets();
            var folder=Environment.GetEnvironmentVariable("CG_FIRE_REVIEW");
            if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"scene-authoring.txt"),"StationFireTutorial saved; catalog/build entry added. Original FpsStation was not saved or modified.");}
            EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity",OpenSceneMode.Single);
            Debug.Log("[Issue318] Station fire tutorial scene and category authored.");
        }
        static GameObject BuildRadio()
        {
            var root=new GameObject("상세조작 휴대 무전기");
            try
            {
                Part(root.transform,"본체","본체",Vector3.zero,Vector3.one);
                Part(root.transform,"안테나","안테나",new Vector3(-.027f,.115f,0),Vector3.one);
                Part(root.transform,"상태 화면","상태 화면",new Vector3(0,.028f,.024f),Vector3.one);
                for(int i=0;i<5;i++)Part(root.transform,"스피커 "+i,"스피커 "+i,new Vector3(0,-.012f-i*.008f,.024f),Vector3.one);
                Part(root.transform,"PTT","본체",new Vector3(.047f,.015f,0),new Vector3(.18f,.34f,.58f));
                var box=root.AddComponent<BoxCollider>();box.size=new Vector3(.085f,.16f,.042f);
                return PrefabUtility.SaveAsPrefabAsset(root,Art+"/Equipment/DetailedRadio.prefab");
            }
            finally{Object.DestroyImmediate(root);}
        }
        static void Part(Transform parent,string name,string source,Vector3 position,Vector3 scale)
        {
            string id=source.Replace(" ","-");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ChooGuard/Art/Tutorial/Radio-"+id+".asset");
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/ChooGuard/Art/Tutorial/Radio-"+id+".mat");
            if(mesh==null||material==null)throw new FileNotFoundException("Existing beveled radio part missing: "+source);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        public static void BuildReviewPlayer()
        {
            var output=Environment.GetEnvironmentVariable("CG_FIRE_PLAYER");
            if(string.IsNullOrEmpty(output))throw new InvalidOperationException("CG_FIRE_PLAYER must name an isolated local review build");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=Path.Combine(output,"CHOOGuard.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development|BuildOptions.CompressWithLz4});
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Review player build failed: "+report.summary.result);
        }
    }
}
