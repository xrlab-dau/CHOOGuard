#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Seeded.Unity.Editor
{
    public static class WorldSnapshotImporter
    {
        const string Prepared="Assets/Simulator/Prepared";
        const string ResourcesRoot="Assets/Simulator/Resources";
        static readonly HashSet<int> SafeSceneClasses=new HashSet<int>{1,4,23,33,64,65,108,135,136,137,205,224,1660057539}; // SceneRoots stores transform references only.
        public sealed class Request
        {
            public string InventoryAssetPath,GeometrySceneAssetPath,ExpectedInventorySha256,ExpectedSourceSceneSha256,ExpectedGeometrySceneSha256;
            // Domain labels do not omit or replace geometry. They scope the additive
            // station bake apart from the established platform and Car04 adapters.
            public string TrainRootId;
            public string[] PlatformRootIds=Array.Empty<string>();
            public string[] StationFloorIds=Array.Empty<string>();
            public string[] StationRegionIds=Array.Empty<string>();
            public string[] CandidateSupportIds=Array.Empty<string>();
            public string PrefabAssetPath=Prepared+"/CurrentStationGeometry.prefab";
            public string ManifestAssetPath=ResourcesRoot+"/WorldSnapshotPrefabManifest.json";
        }
        public sealed class Result
        {
            public GameObject Prefab;
            public TextAsset Inventory,Manifest;
            public JObject Receipt;
        }

        public static Result Import(Request request)
        {
            RequireIsolatedProject();
            if(request==null)throw new ArgumentNullException(nameof(request));
            RequireAssetPath(request.GeometrySceneAssetPath,"Assets/WorldSnapshot/",".geometry.unity");
            RequireAssetPath(request.InventoryAssetPath,"Assets/Simulator/Resources/",".json");
            RequireAssetPath(request.PrefabAssetPath,Prepared+"/",".prefab");
            RequireAssetPath(request.ManifestAssetPath,ResourcesRoot+"/",".json");
            CheckHash(request.GeometrySceneAssetPath,request.ExpectedGeometrySceneSha256,"safe geometry scene");
            CheckHash(request.InventoryAssetPath,request.ExpectedInventorySha256,"source object inventory");
            var text=File.ReadAllText(request.GeometrySceneAssetPath);
            foreach(Match match in Regex.Matches(text,@"^--- !u!(\d+) &",RegexOptions.Multiline))
                if(!SafeSceneClasses.Contains(int.Parse(match.Groups[1].Value)))
                    throw new InvalidOperationException("Unsafe component class in stripped geometry scene: "+match.Groups[1].Value);
            var registry=WorldObjectRegistry.Parse(File.ReadAllText(request.InventoryAssetPath),request.ExpectedInventorySha256,request.ExpectedSourceSceneSha256);
            if(string.IsNullOrEmpty(request.TrainRootId)||!registry.Objects.ContainsKey(request.TrainRootId)||request.PlatformRootIds.Length!=2||request.PlatformRootIds.Any(id=>!registry.Objects.ContainsKey(id)))
                throw new InvalidOperationException("Qualified source train and two platform root IDs are required");
            var domains=new Dictionary<string,string>(StringComparer.Ordinal){{request.TrainRootId,"ktx"}};
            foreach(var id in request.PlatformRootIds)domains.Add(id,"platform");
            if(request.StationFloorIds.Any(id=>!registry.Objects.ContainsKey(id)))throw new InvalidOperationException("Station walk surface anchors must exist in source inventory");
            if(request.StationRegionIds.Concat(request.CandidateSupportIds).Any(id=>!registry.Objects.ContainsKey(id)))throw new InvalidOperationException("Station candidate region/support IDs must exist in source inventory");
            Directory.CreateDirectory(Prepared);Directory.CreateDirectory(ResourcesRoot);
            var previous=SceneManager.GetActiveScene();Scene source=default;GameObject wrapper=null;
            var sourceById=new Dictionary<string,GameObject>(StringComparer.Ordinal);
            var cloneById=new Dictionary<string,GameObject>(StringComparer.Ordinal);
            var clonedComponents=new Dictionary<Component,Component>();
            var componentCounts=new SortedDictionary<string,int>(StringComparer.Ordinal);
            var readableImports=new JArray();
            var recordByFileId=registry.Objects.Values.ToDictionary(r=>r.SceneFileId,StringComparer.Ordinal);
            try
            {
                source=EditorSceneManager.OpenScene(request.GeometrySceneAssetPath,OpenSceneMode.Additive);
                foreach(var root in source.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var go=t.gameObject;var localId=GlobalObjectId.GetGlobalObjectIdSlow(go).targetObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    recordByFileId.TryGetValue(localId,out var record);
                    if(record==null||sourceById.ContainsKey(record.Id))throw new InvalidOperationException("Safe scene contains unknown/repeated fileID "+localId);
                    if(go.name!=record.Name||go.activeSelf!=record.ActiveSelf)throw new InvalidOperationException("Safe scene differs from inventory "+record.Id);
                    if(go.GetComponents<MonoBehaviour>().Length!=0)throw new InvalidOperationException("Legacy behaviour found in safe scene");
                    sourceById.Add(record.Id,go);
                }
                if(sourceById.Count!=registry.Objects.Count)throw new InvalidOperationException("Safe scene object census differs from snapshot inventory");
                wrapper=new GameObject("Current saved station source geometry");wrapper.SetActive(false);
                if(previous.IsValid()&&previous.isLoaded)SceneManager.MoveGameObjectToScene(wrapper,previous);
                // Build all transforms first; parent IDs and ordering come from saved source.
                foreach(var record in registry.Objects.Values.OrderBy(x=>x.Id,StringComparer.Ordinal))
                {
                    var original=sourceById[record.Id];
                    var copy=original.transform is RectTransform?new GameObject(original.name,typeof(RectTransform)):new GameObject(original.name);
                    copy.layer=original.layer;copy.tag=original.tag;copy.transform.SetParent(wrapper.transform,false);copy.SetActive(original.activeSelf);
                    GameObjectUtility.SetStaticEditorFlags(copy,GameObjectUtility.GetStaticEditorFlags(original));
                    cloneById.Add(record.Id,copy);clonedComponents.Add(original.transform,copy.transform);
                }
                foreach(var record in registry.Objects.Values.OrderBy(x=>x.Id,StringComparer.Ordinal))
                {
                    var original=sourceById[record.Id];var copy=cloneById[record.Id];
                    copy.transform.SetParent(string.IsNullOrEmpty(record.ParentId)?wrapper.transform:cloneById[record.ParentId].transform,false);
                    copy.transform.localPosition=original.transform.localPosition;copy.transform.localRotation=original.transform.localRotation;copy.transform.localScale=original.transform.localScale;
                    copy.transform.SetSiblingIndex(original.transform.GetSiblingIndex());
                    if(original.transform is RectTransform rect&&copy.transform is RectTransform cloneRect)
                    {cloneRect.anchorMin=rect.anchorMin;cloneRect.anchorMax=rect.anchorMax;cloneRect.pivot=rect.pivot;cloneRect.sizeDelta=rect.sizeDelta;cloneRect.anchoredPosition3D=rect.anchoredPosition3D;}
                    string domain=GeometryDomain(record.Id,registry,domains);
                    var identity=copy.AddComponent<SnapshotObjectIdentity>();identity.ObjectId=record.Id;identity.SceneFileId=record.SceneFileId;identity.SourcePath=record.Path;
                    identity.SourceActiveSelf=record.ActiveSelf;identity.SourceActiveInHierarchy=record.ActiveInHierarchy;identity.SourcePhysicalEligible=record.PhysicalEligible;identity.GeometryDomain=domain;
                    var originalFilter=original.GetComponent<MeshFilter>();var originalRenderer=original.GetComponent<MeshRenderer>();
                    identity.SourceMesh=originalFilter==null?null:originalFilter.sharedMesh;identity.SourceHasRenderer=originalRenderer!=null;
                    identity.SourceMaterials=originalRenderer==null?Array.Empty<Material>():originalRenderer.sharedMaterials;
                    identity.SourceWorldMatrix=MatrixValues(original.transform.localToWorldMatrix);
                    foreach(var component in original.GetComponents<Component>())
                    {
                        if(component is Transform||component==null)continue;
                        if(!(component is MeshFilter||component is MeshRenderer||component is Collider||component is LODGroup||component is Light||component is SkinnedMeshRenderer))
                            throw new InvalidOperationException("Component outside geometry allowlist: "+component.GetType().FullName);
                        if(component is SkinnedMeshRenderer)throw new InvalidOperationException("Skinned source geometry requires a separately reviewed pose snapshot");
                        var clone=copy.AddComponent(component.GetType());EditorUtility.CopySerialized(component,clone);clonedComponents.Add(component,clone);
                        var type=component.GetType().Name;componentCounts[type]=componentCounts.TryGetValue(type,out var n)?n+1:1;
                    }
                }
                // Serialized references into the opened safe scene must be remapped.
                // Shared mesh/material assets remain the exact original GUID/localID refs.
                var sourceIdByObject=sourceById.ToDictionary(p=>p.Value,p=>p.Key);
                foreach(var pair in clonedComponents)
                {
                    var serialized=new SerializedObject(pair.Value);var iterator=serialized.GetIterator();
                    while(iterator.Next(true))
                    {
                        if(iterator.propertyType!=SerializedPropertyType.ObjectReference)continue;
                        var reference=iterator.objectReferenceValue;
                        if(reference is Component c&&c.gameObject.scene==source)
                            iterator.objectReferenceValue=clonedComponents.TryGetValue(c,out var target)?target:null;
                        else if(reference is GameObject go&&go.scene==source)
                        {
                            iterator.objectReferenceValue=sourceIdByObject.TryGetValue(go,out var sourceId)?cloneById[sourceId]:null;
                        }
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    if(pair.Value is Renderer renderer){renderer.lightmapIndex=-1;renderer.realtimeLightmapIndex=-1;}
                }
                foreach(var original in sourceById.Values.OrderBy(g=>g.transform.GetSiblingIndex()))
                    cloneById[sourceIdByObject[original]].transform.SetSiblingIndex(original.transform.GetSiblingIndex());
                foreach(var pair in sourceById)
                {
                    var originalColliders=pair.Value.GetComponents<Collider>();var identity=cloneById[pair.Key].GetComponent<SnapshotObjectIdentity>();
                    identity.SourceColliders=originalColliders.Select(c=>(Collider)clonedComponents[c]).ToArray();
                    identity.SourceColliderFileIds=originalColliders.Select(c=>GlobalObjectId.GetGlobalObjectIdSlow(c).targetObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                }
                // Readability changes affect only staged importer metadata. They make
                // exact source meshes usable by the isolated physics/NavMesh runtime.
                var meshPaths=sourceById.Values.SelectMany(g=>g.GetComponents<MeshFilter>()).Where(f=>f.sharedMesh!=null).Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)).Distinct().OrderBy(x=>x,StringComparer.Ordinal);
                foreach(var meshPath in meshPaths)
                {
                    var modelImporter=AssetImporter.GetAtPath(meshPath) as ModelImporter;
                    if(modelImporter!=null&&!modelImporter.isReadable)
                    {var before=HashFile(meshPath+".meta");modelImporter.isReadable=true;modelImporter.SaveAndReimport();readableImports.Add(new JObject{["path"]=meshPath,["before_meta_sha256"]=before,["after_meta_sha256"]=HashFile(meshPath+".meta"),["isolated_recipe"]="ModelImporter.isReadable=true"});}
                }
                // Transforms were cloned under an inactive wrapper, so this cannot
                // execute source behaviour; no source behaviour exists in the prefab.
                foreach(var record in registry.Objects.Values)
                {
                    var identity=cloneById[record.Id].GetComponent<SnapshotObjectIdentity>();
                    if(!StationWorldAdapter.MatrixNear(cloneById[record.Id].transform.localToWorldMatrix,StationWorldAdapter.Matrix(identity.SourceWorldMatrix),.0002f))
                        throw new InvalidOperationException("Geometry clone failed saved world matrix check "+record.Id);
                }
                wrapper.SetActive(true);
                var prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,request.PrefabAssetPath);
                if(prefab==null)throw new InvalidOperationException("Geometry snapshot prefab save failed");
                var manifest=new JObject{["schema"]="chooguard-geometry-prefab.v1",["inventorySha256"]=request.ExpectedInventorySha256,["sourceSceneSha256"]=request.ExpectedSourceSceneSha256,["geometrySceneSha256"]=request.ExpectedGeometrySceneSha256,["prefabAssetPath"]=request.PrefabAssetPath,["objectCount"]=registry.Objects.Count,["runtimeRoots"]=new JObject{["ktx"]=request.TrainRootId,["platforms"]=new JArray(request.PlatformRootIds),["stationFloors"]=new JArray(request.StationFloorIds),["stationRegions"]=new JArray(request.StationRegionIds),["candidateSupports"]=new JArray(request.CandidateSupportIds)},["candidateSupportQualification"]="pending actual source collider/triangle, NavMesh and capsule probes",["componentCounts"]=JObject.FromObject(componentCounts),["legacyBehaviours"]=0,["isolatedReadableImportRecipes"]=readableImports,["sourceSceneModified"]=false,["frame"]="saved Unity world; identity wrapper; no registration/refit",["claim"]="Geometry inventory and exact asset references. Semantic operations and complete navigation coverage require separate qualification."};
                File.WriteAllText(request.ManifestAssetPath,manifest.ToString(Formatting.Indented)+"\n");AssetDatabase.ImportAsset(request.ManifestAssetPath,ImportAssetOptions.ForceSynchronousImport);
                CheckHash(request.GeometrySceneAssetPath,request.ExpectedGeometrySceneSha256,"safe geometry scene after prefab export");
                CheckHash(request.InventoryAssetPath,request.ExpectedInventorySha256,"source inventory after prefab export");
                AssetDatabase.SaveAssets();
                return new Result{Prefab=prefab,Inventory=AssetDatabase.LoadAssetAtPath<TextAsset>(request.InventoryAssetPath),Manifest=AssetDatabase.LoadAssetAtPath<TextAsset>(request.ManifestAssetPath),Receipt=manifest};
            }
            finally
            {
                if(wrapper!=null)UnityEngine.Object.DestroyImmediate(wrapper);
                if(source.IsValid()&&source.isLoaded)EditorSceneManager.CloseScene(source,true);
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            }
        }

        static string GeometryDomain(string id,WorldObjectRegistry registry,IReadOnlyDictionary<string,string> roots)
        {var seen=new HashSet<string>(StringComparer.Ordinal);while(!string.IsNullOrEmpty(id)){if(!seen.Add(id))throw new InvalidOperationException("Cyclic source hierarchy");if(roots.TryGetValue(id,out var domain))return domain;id=registry.Objects[id].ParentId;}return "station";}
        public static void RequireIsolatedProject()
        {var project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');if(!project.EndsWith("/output/simulator/platform-ktx-unity",StringComparison.Ordinal))throw new InvalidOperationException("World importer only operates in the isolated platform-ktx-unity project");}
        static void RequireAssetPath(string value,string prefix,string suffix)
        {if(string.IsNullOrEmpty(value)||!value.StartsWith(prefix,StringComparison.Ordinal)||!value.EndsWith(suffix,StringComparison.Ordinal)||value.Contains("..")||value.Contains('\\'))throw new InvalidOperationException("Unexpected snapshot asset path: "+value);}
        static void CheckHash(string path,string expected,string label)
        {if(string.IsNullOrEmpty(expected)||!Regex.IsMatch(expected,@"^[a-f0-9]{64}$")||HashFile(path)!=expected)throw new InvalidOperationException("Frozen "+label+" SHA256 mismatch");}
        public static string HashFile(string path)
        {using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        static float[] MatrixValues(Matrix4x4 matrix)
        {var values=new float[16];for(int r=0;r<4;r++)for(int c=0;c<4;c++)values[r*4+c]=matrix[r,c];return values;}
    }
}
#endif
