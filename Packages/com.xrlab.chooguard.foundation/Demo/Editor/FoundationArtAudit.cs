using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace ChooGuard.Foundation.Demo.Editor
{
    public static class FoundationArtAudit
    {
        [Serializable] public sealed class Instance
        {public string assetId;public string path;public bool active;public string[] materials;public Vector3 center;public Vector3 size;}
        [Serializable] private sealed class Inventory {public Instance[] instances;public string[] unsupportedMeshes;public string scope;}
        private static string Argument(string key,string fallback)
        {var args=Environment.GetCommandLineArgs();var at=Array.IndexOf(args,key);return at>=0&&at+1<args.Length?args[at+1]:fallback;}
        private static string Hierarchy(Transform transform)
        {return transform.parent==null?transform.name:Hierarchy(transform.parent)+"/"+transform.name;}
        public static void ExportSceneInventoryBatch()
        {
            RequireSavedScenes();
            var scene=EditorSceneManager.OpenScene(FoundationDemoSceneBuilder.DefaultScenePath,OpenSceneMode.Single);
            var meshes=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MeshFilter>(true)).ToArray();
            var inventory=new Inventory{scope="Serialized simulation geometry, including inactive virtual/legacy guidance. Unit-shell uses are named by hierarchy; a shared cube is not a detail fidelity claim.",
                unsupportedMeshes=meshes.Where(m=>!AssetDatabase.GetAssetPath(m.sharedMesh).StartsWith(FoundationBlenderAssets.Path,StringComparison.Ordinal)).Select(m=>Hierarchy(m.transform)).ToArray(),
                instances=meshes.Where(m=>AssetDatabase.GetAssetPath(m.sharedMesh).StartsWith(FoundationBlenderAssets.Path,StringComparison.Ordinal)).Select(m=>
                {var r=m.GetComponent<Renderer>();return new Instance{assetId=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(m.sharedMesh)),path=Hierarchy(m.transform),active=r!=null&&r.enabled&&m.gameObject.activeInHierarchy,
                 materials=r==null?Array.Empty<string>():r.sharedMaterials.Select(x=>x.name).ToArray(),center=r==null?Vector3.zero:r.bounds.center,size=r==null?Vector3.zero:r.bounds.size};}).ToArray()};
            var path=Path.GetFullPath(Argument("--choo-art-inventory","Builds/ArtReview/scene-inventory.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(inventory,true));
            if(inventory.unsupportedMeshes.Length>0)throw new InvalidOperationException("Non-Blender geometry in simulation inventory.");
            Debug.Log("CHOO_ART_INVENTORY "+inventory.instances.Length+" mesh instances exported.");
        }
        public static void BuildMacReviewBatch()
        {
            RequireSavedScenes();
            const string output="Builds/ArtReview";const string owner="chooguard.native-art-review.v1";
            RejectLinks(output);
            if(Directory.Exists(output)&&(!File.Exists(output+"/owner.txt")||File.ReadAllText(output+"/owner.txt")!=owner))
                throw new InvalidOperationException("Review build output belongs to another task.");
            Directory.CreateDirectory(output);File.WriteAllText(output+"/owner.txt",owner);
            const string sceneRoot="Assets/CHOOguardGenerated/ArtReview";
            RejectLinks(sceneRoot);
            if(Directory.Exists(sceneRoot)&&(!File.Exists(sceneRoot+"/owner.txt")||File.ReadAllText(sceneRoot+"/owner.txt")!=owner))
                throw new InvalidOperationException("Review scene output is not owned.");
            if(!AssetDatabase.IsValidFolder(sceneRoot))AssetDatabase.CreateFolder("Assets/CHOOguardGenerated","ArtReview");
            File.WriteAllText(sceneRoot+"/owner.txt",owner);AssetDatabase.ImportAsset(sceneRoot+"/owner.txt");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("ArtReview");
            var camera=new GameObject("ReviewCamera").AddComponent<Camera>();camera.transform.SetParent(root.transform);
            camera.transform.position=new Vector3(0,0,-15);camera.orthographic=true;camera.orthographicSize=3.3f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.80f,.83f,.85f);
            var names=AssetDatabase.FindAssets("t:Model",new[]{FoundationBlenderAssets.Path.TrimEnd('/')})
                .Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".fbx")).Select(Path.GetFileNameWithoutExtension).OrderBy(x=>x).ToArray();
            var materials=AssetDatabase.FindAssets("t:Material",new[]{FoundationDemoSceneBuilder.DefaultGeneratedRoot+"/Materials"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            var cells=new Transform[names.Length];var focusCenters=new Vector3[names.Length];var focusZoom=new float[names.Length];
            for(var i=0;i<names.Length;i++)
            {
                cells[i]=new GameObject(names[i]).transform;cells[i].SetParent(root.transform,false);
                cells[i].localPosition=new Vector3((i%3-1)*3.0f,i%6<3?1.35f:-1.35f,0);
                var model=FoundationBlenderAssets.Instantiate(names[i],cells[i],materials);
                var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                var ratio=1.9f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                var center=model.InverseTransformPoint(bounds.center);
                var focus=FoundationAssetPhysics.ReviewFocus(names[i],new Bounds(center,bounds.size));
                model.localScale=Vector3.one*ratio;model.localPosition=-center*ratio;
                focusCenters[i]=(focus.center-center)*ratio;
                focusZoom[i]=4.2f/(Mathf.Max(focus.size.x,focus.size.y,focus.size.z)*ratio);
            }
            var controller=root.AddComponent<ArtReviewController>();controller.Configure(cells,names,camera,focusCenters,focusZoom);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.38f,.41f,.44f);
            var reflectionPath=sceneRoot+"/StudioReflection.asset";
            var studio=AssetDatabase.LoadAssetAtPath<Cubemap>(reflectionPath);
            if(studio!=null&&(studio.width!=32||studio.mipmapCount<=1)){AssetDatabase.DeleteAsset(reflectionPath);studio=null;}
            if(studio==null)
            {
                if(File.Exists(reflectionPath))throw new InvalidOperationException("Studio reflection path contains another asset.");
                studio=new Cubemap(32,TextureFormat.RGB24,true);AssetDatabase.CreateAsset(studio,reflectionPath);
            }
            for(var face=0;face<6;face++)
            {
                var pixels=new Color[1024];
                for(var y=0;y<32;y++)for(var x=0;x<32;x++)
                {
                    var u=2*x/31f-1;var v=2*y/31f-1;
                    var direction=face==0?new Vector3(1,v,-u):face==1?new Vector3(-1,v,u):
                        face==2?new Vector3(u,1,-v):face==3?new Vector3(u,-1,v):face==4?new Vector3(u,v,1):new Vector3(-u,v,-1);
                    var value=Mathf.Lerp(.30f,.90f,(direction.normalized.y+1)/2);
                    pixels[y*32+x]=new Color(value,value,value);
                }
                studio.SetPixels(pixels,(CubemapFace)face);
            }
            studio.Apply();EditorUtility.SetDirty(studio);AssetDatabase.SaveAssetIfDirty(studio);
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=studio;
            foreach(var pair in new[]{new Vector3(35,-30,0),new Vector3(-25,140,0)})
            {var light=new GameObject("StudioLight").AddComponent<Light>();light.transform.SetParent(root.transform);light.type=LightType.Directional;light.intensity=pair.y<0?.82f:.25f;light.transform.rotation=Quaternion.Euler(pair);}
            var scenePath=sceneRoot+"/ArtReview.unity";EditorSceneManager.SaveScene(scene,scenePath);
            var product=PlayerSettings.productName;var width=PlayerSettings.defaultScreenWidth;var height=PlayerSettings.defaultScreenHeight;
            try
            {
                PlayerSettings.productName="CHOOGuard Art Review";PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=960;
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scenePath},locationPathName=output+"/ChooGuardArtReview.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
                if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Art review player build failed.");
            }
            finally {PlayerSettings.productName=product;PlayerSettings.defaultScreenWidth=width;PlayerSettings.defaultScreenHeight=height;}
        }
        private static void RejectLinks(string path)
        {
            for(var d=new DirectoryInfo(Path.GetFullPath(path));d!=null;d=d.Parent)
                if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Review output cannot use a linked parent.");
            if(Directory.Exists(path))foreach(var entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
                if((entry.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Review output contains a link.");
        }
        private static void RequireSavedScenes()
        {for(var i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save user scenes before art inspection.");}
    }
}
