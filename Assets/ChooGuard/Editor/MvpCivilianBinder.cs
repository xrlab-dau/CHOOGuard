using System;
using System.IO;
using System.Linq;
using ChooGuard.App.Mvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.Editor
{
    public static class MvpCivilianBinder
    {
        const string Folder="Assets/ChooGuard/Art/CivilianSet";
        [MenuItem("ChooGuard/MVP/민간인 모델 연결")]
        public static void BindExisting()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("편집 모드에서 연결하세요.");
            var station=UnityEngine.Object.FindFirstObjectByType<MvpStationView>(FindObjectsInactive.Include);
            if(station==null)throw new InvalidOperationException("열린 장면에 역 뷰가 없습니다.");
            Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
            string[] names={"CivilianJacket","CivilianBlue","CivilianRose"};
            var templates=new GameObject[3];var walks=new AnimationClip[3];
            for(int i=0;i<names.Length;i++)
            {
                string path=Folder+"/"+names[i]+".fbx";
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null)throw new FileNotFoundException("민간인 자료가 없습니다.",path);
                importer.globalScale=1;importer.useFileScale=true;importer.useFileUnits=true;
                importer.importAnimation=true;importer.animationType=ModelImporterAnimationType.Generic;
                importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
                var clips=importer.defaultClipAnimations;
                foreach(var clip in clips){clip.loopTime=clip.name.Contains("walk")||clip.name.Contains("idle");clip.loopPose=clip.loopTime;}
                importer.clipAnimations=clips;importer.SaveAndReimport();
                foreach(var source in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    string materialPath=Folder+"/Materials/"+source.name+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.name=source.name;material.SetColor("_BaseColor",source.color);material.SetFloat("_Smoothness",.2f);AssetDatabase.CreateAsset(material,materialPath);}
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source),material);
                }
                importer.SaveAndReimport();
                templates[i]=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                walks[i]=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>c.name.ToLowerInvariant().Contains("walk")&&!c.name.StartsWith("__"));
                if(walks[i]==null)throw new InvalidOperationException("민간인 보행 동작이 없습니다: "+names[i]);
            }
            Undo.RecordObject(station,"민간인 모델 연결");station.CivilianTemplates=templates;station.CivilianWalkClips=walks;
            EditorUtility.SetDirty(station);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(station.gameObject.scene);
        }
    }
}
