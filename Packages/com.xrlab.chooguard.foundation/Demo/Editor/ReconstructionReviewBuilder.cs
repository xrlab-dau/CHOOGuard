using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Foundation.Demo.Editor
{
    public static class ReconstructionReviewBuilder
    {
        public const string DefaultSourceDirectory="reconstruction/output/busan-concourse-pilot";
        public const string DefaultGeneratedRoot="Assets/CHOOguardGenerated/ReconstructionReview";
        private const string Owner="chooguard.reconstruction-review.v1";
        private static readonly float[] ReviewToUnity={1,0,0,0,0,1,0,0,0,0,-1,0,0,0,0,1};
        [Serializable] public sealed class ReviewBounds {public Vector3 center;public Vector3 size;}
        [Serializable] public sealed class ColorSample {public Vector3 position;public Color color;}
        [Serializable] public sealed class ReviewView
        {public string id;public string @object;public int inputTriangles;public int outputTriangles;public int vertices;public ReviewBounds expectedUnityBounds;public ColorSample[] colorSamples;}
        [Serializable] public sealed class ReviewManifest
        {
            public string schemaVersion;public string unit;public bool collisionApproved;public string colorEncoding;public string shadingMode;
            public string fbxSha256;public string inputManifestSha256;public string sourceGlbSha256;
            public float[] unityFromReviewRowMajor;public ReviewView[] views;
        }
        public sealed class Source
        {public string directory;public string fbxPath;public string manifestPath;public string manifestJson;public string manifestSha256;public ReviewManifest manifest;}
        [Serializable] public sealed class ViewReceipt
        {public string id;public int authoredVertices;public int importedVertices;public int triangles;public Vector3 center;public Vector3 size;public bool vertexColors;public int colorSamplesValidated;public float maximumColorError;}
        [Serializable] private sealed class ImportReceipt
        {
            public string schemaVersion="unity-reconstruction-review-1";public string sourceDirectory;public string fbxSha256;public string blenderManifestSha256;
            public string unit="model_relative";public bool metricApproved;public bool collisionApproved;public string colorEncoding="linear-rgb";public string shadingMode;
            public float[] unityFromReviewRowMajor;public string importer;public string scope;public ViewReceipt[] views;
        }
        public static Vector3 ReviewToUnityPoint(Vector3 point)=>new Vector3(point.x,point.y,-point.z);
        public static string SourceDirectoryFromArguments()=>Argument("--choo-reconstruction-dir",DefaultSourceDirectory);
        // Unity batch startup has a clean untitled scene that cannot coexist with an additive scene.
        // Never use this exception for interactive, dirty, saved, or multiple loaded scenes.
        public static bool CanReplaceBatchStartup(bool batchMode,int sceneCount,string scenePath,bool isDirty)=>
            batchMode&&sceneCount==1&&string.IsNullOrEmpty(scenePath)&&!isDirty;

        public static ReviewManifest ParseManifest(string json)
        {
            if(string.IsNullOrWhiteSpace(json))throw new InvalidOperationException("Missing Blender review manifest.");
            ReviewManifest manifest;
            try {manifest=JsonUtility.FromJson<ReviewManifest>(json);}
            catch(Exception error){throw new InvalidOperationException("Invalid Blender review manifest JSON.",error);}
            if(manifest==null||manifest.schemaVersion!="blender-reconstruction-review-1"||manifest.unit!="model_relative"||manifest.collisionApproved)
                throw new InvalidOperationException("Only uncalibrated Blender review surfaces are accepted.");
            if(manifest.colorEncoding!="linear-rgb")throw new InvalidOperationException("Review vertex colors must explicitly use linear-rgb encoding.");
            var shadingFields=Regex.Matches(json,"\"shadingMode\"\\s*:").Count;
            if(shadingFields==0)manifest.shadingMode="observed-unlit";
            else if(shadingFields!=1)throw new InvalidOperationException("Ambiguous shading mode.");
            ShaderNameForMode(manifest.shadingMode);
            RequireLiteral(json,"metersPerUnit","null");RequireLiteral(json,"collisionApproved","false");
            foreach(var key in new[]{"schemaVersion","unit","colorEncoding","fbxSha256","inputManifestSha256","sourceGlbSha256","unityFromReviewRowMajor","views"})
                if(Regex.Matches(json,"\""+key+"\"\\s*:").Count!=1)throw new InvalidOperationException("Ambiguous or missing manifest field: "+key);
            foreach(var hash in new[]{manifest.fbxSha256,manifest.inputManifestSha256,manifest.sourceGlbSha256})
                if(hash==null||!Regex.IsMatch(hash,"^[0-9a-f]{64}$"))throw new InvalidOperationException("Manifest SHA256 is missing or malformed.");
            if(manifest.unityFromReviewRowMajor==null||!manifest.unityFromReviewRowMajor.SequenceEqual(ReviewToUnity))
                throw new InvalidOperationException("Unexpected review-to-Unity coordinate transform. Lead review and geometry tests are required.");
            if(manifest.views==null||manifest.views.Length==0)throw new InvalidOperationException("No review surfaces declared.");
            var ids=new HashSet<string>(StringComparer.Ordinal);var objects=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in manifest.views)
            {
                if(item==null||string.IsNullOrWhiteSpace(item.id)||string.IsNullOrWhiteSpace(item.@object)||!ids.Add(item.id)||!objects.Add(item.@object)||
                   item.vertices<3||item.outputTriangles<1||item.expectedUnityBounds==null||!Finite(item.expectedUnityBounds.center)||!Finite(item.expectedUnityBounds.size)||
                   item.expectedUnityBounds.size.x<0||item.expectedUnityBounds.size.y<0||item.expectedUnityBounds.size.z<0||item.expectedUnityBounds.size.sqrMagnitude<=0)
                    throw new InvalidOperationException("Review surface identity, geometry or bounds are invalid.");
                RequireColorSamples(item);
            }
            return manifest;
        }

        public static Source ReadSource(string directory)
        {
            var project=Directory.GetParent(Application.dataPath).FullName;
            var allowed=Path.GetFullPath(Path.Combine(project,"reconstruction/output"));
            var absolute=Path.GetFullPath(Path.IsPathRooted(directory)?directory:Path.Combine(project,directory));
            if(!absolute.StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.Ordinal)||!Directory.Exists(absolute))
                throw new InvalidOperationException("Review source must be an existing child of this project's reconstruction/output directory.");
            RejectLinks(absolute);
            var manifestPath=Path.Combine(absolute,"review-manifest.blender.json");var fbx=Path.Combine(absolute,"refined-review.fbx");
            var json=File.ReadAllText(manifestPath);var manifest=ParseManifest(json);
            RequireHash(fbx,manifest.fbxSha256);RequireHash(Path.Combine(absolute,"review-manifest.json"),manifest.inputManifestSha256);
            RequireHash(Path.Combine(absolute,"review-surfaces.glb"),manifest.sourceGlbSha256);
            return new Source{directory=absolute,fbxPath=fbx,manifestPath=manifestPath,manifestJson=json,manifestSha256=Hash(manifestPath),manifest=manifest};
        }

        public static ViewReceipt ValidateImportedView(Transform root,ReviewView expected)
        {
            RequireColorSamples(expected);
            if(root.GetComponentsInChildren<Collider>(true).Length!=0||root.GetComponentsInChildren<Rigidbody>(true).Length!=0)
                throw new InvalidOperationException("Uncalibrated review surfaces cannot inherit physics components.");
            var filters=root.GetComponentsInChildren<MeshFilter>(true);var initialized=false;var bounds=new Bounds();var vertices=0;var triangles=0;
            var nearest=Enumerable.Repeat(float.PositiveInfinity,expected.colorSamples.Length).ToArray();
            var colorErrors=Enumerable.Repeat(float.PositiveInfinity,expected.colorSamples.Length).ToArray();
            foreach(var filter in filters)
            {
                var mesh=filter.sharedMesh;
                if(mesh==null||!mesh.isReadable||mesh.vertexCount<3||mesh.colors32.Length!=mesh.vertexCount)
                    throw new InvalidOperationException("Review mesh must remain readable and carry a color for every vertex: "+expected.id);
                var points=mesh.vertices;var colors=mesh.colors;vertices+=points.Length;
                for(var sub=0;sub<mesh.subMeshCount;sub++)
                {
                    if(mesh.GetTopology(sub)!=MeshTopology.Triangles)throw new InvalidOperationException("Review mesh must contain triangles.");
                    triangles+=checked((int)mesh.GetIndexCount(sub))/3;
                }
                for(var pointIndex=0;pointIndex<points.Length;pointIndex++)
                {
                    var world=filter.transform.TransformPoint(points[pointIndex]);if(!Finite(world))throw new InvalidOperationException("Non-finite imported vertex.");
                    if(!ValidColor(colors[pointIndex]))throw new InvalidOperationException("Invalid imported vertex color.");
                    if(!initialized){bounds=new Bounds(world,Vector3.zero);initialized=true;}else bounds.Encapsulate(world);
                    for(var sampleIndex=0;sampleIndex<expected.colorSamples.Length;sampleIndex++)
                    {
                        var sample=expected.colorSamples[sampleIndex];var distance=(world-sample.position).sqrMagnitude;
                        var colorError=ColorError(colors[pointIndex],sample.color);
                        if(distance<nearest[sampleIndex]-1e-12f){nearest[sampleIndex]=distance;colorErrors[sampleIndex]=colorError;}
                        // FBX can split a source vertex at a normal or corner-color boundary.
                        else if(Mathf.Abs(distance-nearest[sampleIndex])<=1e-12f)colorErrors[sampleIndex]=Mathf.Min(colorErrors[sampleIndex],colorError);
                    }
                }
            }
            if(!initialized||triangles!=expected.outputTriangles)throw new InvalidOperationException("Imported triangle count differs from Blender receipt: "+expected.id);
            var tolerance=Mathf.Max(.0001f,expected.expectedUnityBounds.size.magnitude*.0001f);
            if((bounds.center-expected.expectedUnityBounds.center).magnitude>tolerance||(bounds.size-expected.expectedUnityBounds.size).magnitude>tolerance)
                throw new InvalidOperationException($"Imported bounds disagree with Blender axis/scale receipt for {expected.id}: center {bounds.center:F6}, size {bounds.size:F6}; expected center {expected.expectedUnityBounds.center:F6}, size {expected.expectedUnityBounds.size:F6}.");
            for(var i=0;i<nearest.Length;i++)
                if(nearest[i]>tolerance*tolerance||colorErrors[i]>.008f)
                    throw new InvalidOperationException($"Imported linear color sample {i} disagrees with Blender receipt for {expected.id}: position error {Mathf.Sqrt(nearest[i]):F6}, maximum RGBA error {colorErrors[i]:F6} (limit 0.008).");
            return new ViewReceipt{id=expected.id,authoredVertices=expected.vertices,importedVertices=vertices,triangles=triangles,center=bounds.center,size=bounds.size,vertexColors=true,
                colorSamplesValidated=nearest.Length,maximumColorError=colorErrors.Max()};
        }

        public static Scene Build(string sourceDirectory=DefaultSourceDirectory,string generatedRoot=DefaultGeneratedRoot)
        {
            RequireSavedScenes();var source=ReadSource(sourceDirectory);
            if(generatedRoot!=DefaultGeneratedRoot&&!generatedRoot.StartsWith(DefaultGeneratedRoot+"/",StringComparison.Ordinal))
                throw new InvalidOperationException("Generated reconstruction assets must remain in their dedicated root.");
            if(Path.GetFullPath(generatedRoot)!=Path.GetFullPath(DefaultGeneratedRoot)&&!Path.GetFullPath(generatedRoot).StartsWith(Path.GetFullPath(DefaultGeneratedRoot)+Path.DirectorySeparatorChar,StringComparison.Ordinal))
                throw new InvalidOperationException("Invalid generated review path.");
            EnsureOwned(DefaultGeneratedRoot);
            if(generatedRoot!=DefaultGeneratedRoot)EnsureOwned(generatedRoot);
            AssetDatabase.Refresh();
            var fbxPath=generatedRoot+"/refined-review.fbx";File.Copy(source.fbxPath,fbxPath,true);
            File.WriteAllText(generatedRoot+"/review-manifest.blender.json",source.manifestJson);
            AssetDatabase.ImportAsset(fbxPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            var importer=AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if(importer==null)throw new InvalidOperationException("The reconstruction FBX has no ModelImporter.");
            importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=false;
            // Keep a named assembly root even when the FBX has only one root node.
            importer.preserveHierarchy=true;
            importer.addCollider=false;importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
            importer.isReadable=true;importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
            RequireHash(fbxPath,source.manifest.fbxSha256);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if(model==null)throw new InvalidOperationException("FBX failed to import.");
            var shader=Shader.Find(ShaderNameForMode(source.manifest.shadingMode));
            if(shader==null)throw new InvalidOperationException("Reconstruction vertex color shader is unavailable.");
            var materialPath=generatedRoot+"/VertexColors.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}else {material.shader=shader;EditorUtility.SetDirty(material);}
            var scenePath=generatedRoot+"/ReconstructionReview.unity";
            var existing=SceneManager.GetSceneByPath(scenePath);if(existing.IsValid()&&existing.isLoaded)EditorSceneManager.CloseScene(existing,true);
            var previous=SceneManager.GetActiveScene();
            var mode=CanReplaceBatchStartup(Application.isBatchMode,SceneManager.sceneCount,previous.path,previous.isDirty)?NewSceneMode.Single:NewSceneMode.Additive;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,mode);
            try
            {
                SceneManager.SetActiveScene(scene);var root=new GameObject("ReconstructionReview");
                var authored=source.manifest.shadingMode=="authored-lit";
                var imported=UnityEngine.Object.Instantiate(model,root.transform,false);imported.name=authored?"AuthoredRelativeStudy":"ObservationSurfaces";
                if(imported.GetComponentsInChildren<Collider>(true).Length!=0||imported.GetComponentsInChildren<Rigidbody>(true).Length!=0)
                    throw new InvalidOperationException("Imported reconstruction unexpectedly contains physics.");
                var transforms=imported.GetComponentsInChildren<Transform>(true);var surfaces=new Transform[source.manifest.views.Length];
                var receipts=new ViewReceipt[surfaces.Length];var bounds=new Bounds[surfaces.Length];var covered=new HashSet<MeshFilter>();
                for(var i=0;i<surfaces.Length;i++)
                {
                    var item=source.manifest.views[i];var matches=transforms.Where(x=>x.name==item.@object).ToArray();
                    if(matches.Length!=1)throw new InvalidOperationException("Expected exactly one imported object: "+item.@object);
                    surfaces[i]=matches[0];receipts[i]=ValidateImportedView(surfaces[i],item);bounds[i]=new Bounds(receipts[i].center,receipts[i].size);
                    foreach(var filter in surfaces[i].GetComponentsInChildren<MeshFilter>(true))
                        if(!covered.Add(filter))throw new InvalidOperationException("Declared review surfaces overlap in the imported hierarchy.");
                }
                if(covered.Count!=imported.GetComponentsInChildren<MeshFilter>(true).Length)throw new InvalidOperationException("Undeclared mesh in reconstruction FBX.");
                foreach(var renderer in imported.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.sharedMaterials=Enumerable.Repeat(material,renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount).ToArray();
                ConfigureStudyLighting(root.transform,source.manifest.shadingMode);
                if(authored)
                {
                    RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.12f,.12f,.12f);
                    RenderSettings.reflectionIntensity=0;RenderSettings.skybox=null;
                }
                var camera=new GameObject("FirstCameraReview").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);
                camera.fieldOfView=60;camera.nearClipPlane=.001f;camera.farClipPlane=Mathf.Max(100,bounds.Max(x=>x.max.magnitude)*4);
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.065f,.075f,.09f);
                root.AddComponent<ReconstructionReviewController>().Configure(surfaces,source.manifest.views.Select(x=>x.id).ToArray(),bounds,camera,source.manifest.fbxSha256,source.manifest.shadingMode);
                var project=Directory.GetParent(Application.dataPath).FullName;
                var receipt=new ImportReceipt{sourceDirectory=source.directory.Substring(project.Length+1).Replace('\\','/'),fbxSha256=source.manifest.fbxSha256,
                    blenderManifestSha256=source.manifestSha256,unityFromReviewRowMajor=ReviewToUnity,views=receipts,shadingMode=source.manifest.shadingMode,
                    importer="FBX globalScale=1, useFileScale=true, bakeAxisConversion=false. Blender stages rotation Z=180 degrees. Validated Unity world bounds; vertex splits are reported, not hidden.",
                    scope=(authored?"Authored volumetric study with illustrative key/fill lighting; geometry and appearance are hypotheses from references. ":"Partial, two-sided per-view observation surfaces. ")+
                        "Model-relative units; metersPerUnit=null. No fusion, gravity, north, metric or collision approval; not a training SceneBundle. Camera moves; geometry is not normalized."};
                File.WriteAllText(generatedRoot+"/import-review.json",JsonUtility.ToJson(receipt,true));
                AssetDatabase.ImportAsset(generatedRoot+"/review-manifest.blender.json");AssetDatabase.ImportAsset(generatedRoot+"/import-review.json");
                AssetDatabase.SaveAssetIfDirty(material);
                if(!EditorSceneManager.SaveScene(scene,scenePath))throw new InvalidOperationException("Failed to save reconstruction review scene.");
                Debug.Log("CHOO_RECONSTRUCTION_REVIEW "+receipts.Sum(x=>x.triangles)+" triangles; "+receipts.Length+" separately visible surfaces; source "+source.manifest.fbxSha256);
                return scene;
            }
            catch
            {
                EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);throw;
            }
        }

        public static void BuildBatch()=>Build(SourceDirectoryFromArguments());
        public static string ShaderNameForMode(string shadingMode)
        {
            if(shadingMode=="observed-unlit")return "CHOOGuard/ReconstructionVertexColor";
            if(shadingMode=="authored-lit")return "CHOOGuard/ReconstructionVertexColorLit";
            throw new InvalidOperationException("Unknown reconstruction shading mode: "+shadingMode);
        }

        public static void ConfigureStudyLighting(Transform root,string shadingMode)
        {
            ShaderNameForMode(shadingMode);
            if(shadingMode!="authored-lit")return;
            var group=new GameObject("IllustrativeStudyLighting").transform;group.SetParent(root,false);
            foreach(var key in new[]{true,false})
            {
                var light=new GameObject(key?"StudyKey":"StudyFill").AddComponent<Light>();light.transform.SetParent(group,false);
                light.type=LightType.Directional;light.color=Color.white;light.intensity=key ? .9f : .3f;light.shadows=LightShadows.None;
                light.transform.localRotation=Quaternion.Euler(key?new Vector3(50,-30,0):new Vector3(25,145,0));
            }
        }

        public static void BuildMacReviewBatch()
        {
            const string output="Builds/ReconstructionReview";RequireSavedScenes();EnsureOwned(output);
            var scene=Build(SourceDirectoryFromArguments());var product=PlayerSettings.productName;
            var width=PlayerSettings.defaultScreenWidth;var height=PlayerSettings.defaultScreenHeight;
            try
            {
                PlayerSettings.productName="CHOOGuard Reconstruction Review";PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=960;
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scene.path},locationPathName=output+"/ChooGuardReconstructionReview.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
                if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Reconstruction review Mac player build failed.");
            }
            finally {PlayerSettings.productName=product;PlayerSettings.defaultScreenWidth=width;PlayerSettings.defaultScreenHeight=height;}
        }

        private static void RequireLiteral(string json,string key,string value)
        {
            var pattern="\""+key+"\"\\s*:";
            if(Regex.Matches(json,pattern).Count!=1||!Regex.IsMatch(json,pattern+"\\s*"+value+"\\s*[,}]"))throw new InvalidOperationException("Manifest requires "+key+"="+value+".");
        }
        private static bool Finite(Vector3 v)=>!(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z));
        private static bool ValidColor(Color color)=>Finite(new Vector3(color.r,color.g,color.b))&&!float.IsNaN(color.a)&&!float.IsInfinity(color.a)&&
            color.r>=0&&color.r<=1&&color.g>=0&&color.g<=1&&color.b>=0&&color.b<=1&&color.a>=0&&color.a<=1;
        private static float ColorError(Color actual,Color expected)=>Mathf.Max(Mathf.Max(Mathf.Abs(actual.r-expected.r),Mathf.Abs(actual.g-expected.g)),Mathf.Max(Mathf.Abs(actual.b-expected.b),Mathf.Abs(actual.a-expected.a)));
        private static void RequireColorSamples(ReviewView view)
        {
            if(view.colorSamples==null||view.colorSamples.Length!=3||view.colorSamples.Any(sample=>sample==null||!Finite(sample.position)||!ValidColor(sample.color)))
                throw new InvalidOperationException("Each review surface requires three finite linear RGBA color samples with Unity positions.");
        }
        private static string Hash(string path){using(var algorithm=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        private static void RequireHash(string path,string expected){if(!File.Exists(path)||Hash(path)!=expected)throw new InvalidOperationException("Reconstruction source hash mismatch: "+Path.GetFileName(path));}
        private static string Argument(string key,string fallback){var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,key);if(index<0)return fallback;if(index+1>=args.Length||args[index+1].StartsWith("--",StringComparison.Ordinal))throw new InvalidOperationException("Missing value for "+key);return args[index+1];}
        private static void RequireSavedScenes(){for(var i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save open user scenes before reconstruction review generation.");}
        private static void EnsureOwned(string directory)
        {
            RejectLinks(directory);var marker=Path.Combine(directory,"owner.txt");
            if(Directory.Exists(directory)&&(!File.Exists(marker)||File.ReadAllText(marker)!=Owner))throw new InvalidOperationException("Reconstruction output belongs to another task: "+directory);
            Directory.CreateDirectory(directory);File.WriteAllText(marker,Owner);
        }
        private static void RejectLinks(string path)
        {
            for(var current=new DirectoryInfo(Path.GetFullPath(path));current!=null;current=current.Parent)
                if(current.Exists&&(current.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Reconstruction paths cannot use linked parents.");
            if(!Directory.Exists(path))return;
            var pending=new Stack<DirectoryInfo>();pending.Push(new DirectoryInfo(path));
            while(pending.Count>0)foreach(var entry in pending.Pop().EnumerateFileSystemInfos())
            {
                if((entry.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Reconstruction directory contains a link.");
                if(entry is DirectoryInfo child)pending.Push(child);
            }
        }
    }
}
