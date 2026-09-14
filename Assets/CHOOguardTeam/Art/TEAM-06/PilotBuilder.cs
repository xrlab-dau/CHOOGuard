#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Team.Art.Team06
{
    // Same importer contract as FoundationBlenderImporter, scoped to this pilot's generated folder only.
    public sealed class PilotModelImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if(!assetPath.StartsWith(PilotBuilder.GeneratedRoot,StringComparison.Ordinal))return;
            PilotBuilder.ApplyImportSettings((ModelImporter)assetImporter);
        }
    }

    /// <summary>
    /// TEAM-06 round-trip check: imports both Blender rounds, compares them with each other, with the
    /// shared baseline FBX and with the Blender-side manifest, and proves each detector rejects a
    /// mirrored model, an unmapped material and a displaced collider.
    /// Batch: Unity -batchmode -nographics -projectPath . -executeMethod ChooGuard.Team.Art.Team06.PilotBuilder.RunBatch
    /// </summary>
    public static class PilotBuilder
    {
        public const string GeneratedRoot="Assets/CHOOguardTeam/Art/TEAM-06/Generated/";
        private const string Module="InformationKiosk";
        private const string PilotRoot=Module+"_Pilot";
        private const string SharedFbx="Assets/CHOOguardArt/Blender/"+Module+".fbx";
        private const string ManifestPath="foundation/art/team/TEAM-06/pilot-manifest.json";
        private const string BaselinePath="foundation/art/asset-manifest.json";
        private const float Tolerance=1e-3f;
        private static readonly Regex BuildOrderSuffix=new Regex(@"\.\d{3}$");

        public static void ApplyImportSettings(ModelImporter model)
        {
            model.globalScale=1;model.useFileScale=true;model.importAnimation=false;model.importCameras=false;model.importLights=false;
            model.addCollider=false;model.isReadable=true;model.importNormals=ModelImporterNormals.Import;
            model.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        }

        [MenuItem("CHOOguard/Team/TEAM-06/Run round-trip pilot")]
        public static void RunFromMenu()
        {
            var verdict=Run();
            Debug.Log("TEAM06_UNITY verdict="+verdict+" -> "+ManifestPath);
        }

        public static void RunBatch()
        {
            try
            {
                var verdict=Run();
                Debug.Log("TEAM06_UNITY verdict="+verdict);
                EditorApplication.Exit(verdict=="pass"?0:2);
            }
            catch(Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static string Run()
        {
            var manifest=JObject.Parse(File.ReadAllText(ManifestPath));
            var baseline=JObject.Parse(File.ReadAllText(BaselinePath));
            var blenderRuns=(JObject)manifest["blender"]["runs"];
            foreach(var run in new[]{"1","2"})
                foreach(var name in new[]{Module,Module+"_RoundTrip"})
                    AssetDatabase.ImportAsset(FbxPath(run,name),ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);

            var failures=new List<string>();
            var unity=new JObject{["unityVersion"]=Application.unityVersion,["capturedAt"]=DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")};
            unity["importSettings"]=CompareImportSettings(failures);
            var palette=baseline["materialNames"].Select(x=>(string)x).ToArray();
            var reference=Load(SharedFbx);
            var runs=new JObject();
            foreach(var run in new[]{"1","2"})runs[run]=InspectRun(run,(JObject)blenderRuns[run],reference,palette,failures);
            unity["runs"]=runs;
            unity["comparison"]=CompareRuns((JObject)runs["1"],(JObject)runs["2"],failures);
            unity["negativeCases"]=NegativeCases(palette,(JObject)blenderRuns["1"],reference,failures);
            unity["failures"]=new JArray(failures);
            var verdict=failures.Count==0?"pass":"fail";
            unity["verdict"]=verdict;
            unity["builderSha256"]=Sha256(BuilderPath);
            unity["environment"]=BuildEnvironment();
            // Earlier verdicts stay on record; a later pass never replaces a failed attempt.
            if(manifest["unity"] is JObject previous)
            {
                var attempts=manifest["unityAttempts"] as JArray??new JArray();
                attempts.Add(new JObject{["capturedAt"]=previous["capturedAt"],["verdict"]=previous["verdict"],
                    ["failures"]=previous["failures"],["builderSha256"]=previous["builderSha256"]});
                manifest["unityAttempts"]=attempts;
            }
            manifest["unity"]=unity;
            File.WriteAllText(ManifestPath,manifest.ToString(Formatting.Indented).Replace("\r\n","\n")+"\n");
            return verdict;
        }

        private const string BuilderPath="Assets/CHOOguardTeam/Art/TEAM-06/PilotBuilder.cs";

        private static string FbxPath(string run,string name)=>GeneratedRoot+"run"+run+"/"+name+".fbx";

        private static string Sha256(string path)
        {
            using(var sha=System.Security.Cryptography.SHA256.Create())
                return string.Concat(sha.ComputeHash(File.ReadAllBytes(path)).Select(b=>b.ToString("x2")));
        }

        // The IlInterpreter asmdef patch from scripts/dev/prepare_multiplayer_dependencies.py is a compile precondition.
        private static JObject BuildEnvironment()
        {
            var pipeline=Directory.GetDirectories("Library/PackageCache","com.unity.pipeline@*");
            var asmdef=pipeline.Length==1?Path.Combine(pipeline[0],"Runtime/IlInterpreter/Unity.Pipeline.IlInterpreter.asmdef"):null;
            return new JObject{["pipelineIlInterpreterAsmdefSha256"]=asmdef!=null&&File.Exists(asmdef)?Sha256(asmdef):null,
                ["pipelinePatchSource"]="scripts/dev/prepare_multiplayer_dependencies.py --apply (foundation/network/dependency-contract.json afterSha256)"};
        }

        private static GameObject Load(string path)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset==null)throw new InvalidOperationException("Model missing: "+path+". Run scripts/art/team/TEAM-06/pilot_module.py first.");
            return asset;
        }

        private static JObject CompareImportSettings(List<string> failures)
        {
            JObject Read(string path)
            {
                var m=(ModelImporter)AssetImporter.GetAtPath(path);
                return new JObject{["globalScale"]=m.globalScale,["useFileScale"]=m.useFileScale,["importAnimation"]=m.importAnimation,
                    ["importCameras"]=m.importCameras,["importLights"]=m.importLights,["addCollider"]=m.addCollider,["isReadable"]=m.isReadable,
                    ["importNormals"]=m.importNormals.ToString(),["materialImportMode"]=m.materialImportMode.ToString()};
            }
            var shared=Read(SharedFbx);
            var result=new JObject{["reference"]=SharedFbx,["referenceSettings"]=shared};
            foreach(var run in new[]{"1","2"})
                foreach(var name in new[]{Module,Module+"_RoundTrip"})
                {
                    var path=FbxPath(run,name);var equal=JToken.DeepEquals(shared,Read(path));
                    result[path]=equal;
                    if(!equal)failures.Add("import settings differ from FoundationBlenderImporter: "+path);
                }
            return result;
        }

        private static JObject InspectRun(string run,JObject blender,GameObject reference,string[] palette,List<string> failures)
        {
            var result=new JObject();
            // Reference-compatible export: every mesh equals the shared baseline FBX after importer settings.
            var plain=Load(FbxPath(run,Module));
            var plainMeshes=MeshTable(plain.transform);
            var referenceMeshes=MeshTable(reference.transform);
            var meshMatches=new JObject();
            foreach(var key in referenceMeshes.Keys.Union(plainMeshes.Keys).OrderBy(x=>x,StringComparer.Ordinal))
            {
                var equal=plainMeshes.TryGetValue(key,out var a)&&referenceMeshes.TryGetValue(key,out var b)&&SameMesh(a,b);
                meshMatches[key]=equal;
                if(!equal)failures.Add("run"+run+" "+Module+".fbx mesh differs from shared baseline: "+key);
            }
            result["referenceMeshesEqual"]=meshMatches;
            result["referenceRawNames"]=new JArray(reference.transform.Cast<Transform>().Select(t=>t.name).OrderBy(x=>x,StringComparer.Ordinal));
            result["pilotRawNames"]=new JArray(plain.transform.Cast<Transform>().Select(t=>t.name).OrderBy(x=>x,StringComparer.Ordinal));

            var model=Load(FbxPath(run,Module+"_RoundTrip"));
            var root=model.transform.Find(PilotRoot)??model.transform;
            result["hierarchyRoot"]=root==model.transform?model.name:model.name+"/"+PilotRoot;
            result["rootTransform"]=new JObject{["position"]=Vec(root.localPosition),["rotation"]=Vec(root.localEulerAngles),["scale"]=Vec(root.lossyScale)};
            if(root.localPosition!=Vector3.zero||root.localRotation!=Quaternion.identity||root.lossyScale!=Vector3.one)
                failures.Add("run"+run+" pivot/axis/scale: pilot root is not identity");

            var lodGroup=model.GetComponentInChildren<LODGroup>(true);
            if(lodGroup==null){failures.Add("run"+run+" LODGroup was not generated from _LOD<n> names");return result;}
            var lods=new JArray();
            var blenderLods=(JArray)blender["lods"];
            var levels=lodGroup.GetLODs();
            for(var level=0;level<levels.Length;level++)
            {
                var renderers=levels[level].renderers.Where(r=>r!=null).ToArray();
                var triangles=renderers.Sum(r=>Triangles(r.GetComponent<MeshFilter>().sharedMesh));
                var materials=renderers.SelectMany(r=>r.sharedMaterials).Select(m=>m.name).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                var textures=renderers.SelectMany(r=>r.sharedMaterials).SelectMany(m=>m.GetTexturePropertyNames().Select(m.GetTexture)).Count(t=>t!=null);
                var entry=new JObject{["lod"]=level,["renderers"]=new JArray(renderers.Select(r=>r.name).OrderBy(x=>x,StringComparer.Ordinal)),
                    ["triangles"]=triangles,["materials"]=new JArray(materials),["textures"]=textures,["screenRelativeHeight"]=levels[level].screenRelativeTransitionHeight};
                lods.Add(entry);
                var expected=level<blenderLods.Count?(JObject)blenderLods[level]:null;
                if(expected==null||(int)expected["triangles"]!=triangles)failures.Add("run"+run+" LOD"+level+" triangles "+triangles+" != Blender "+expected?["triangles"]);
                if(expected!=null&&!JToken.DeepEquals(expected["materials"],new JArray(materials)))failures.Add("run"+run+" LOD"+level+" material slots differ from Blender");
                if(textures!=0)failures.Add("run"+run+" LOD"+level+" has "+textures+" textures; baseline has none");
                if(renderers.Any(r=>r.name.StartsWith("Collider_",StringComparison.Ordinal)))failures.Add("run"+run+" LOD"+level+" includes a collider mesh");
            }
            result["lods"]=lods;
            if(levels.Length!=blenderLods.Count)failures.Add("run"+run+" LOD count "+levels.Length+" != Blender "+blenderLods.Count);

            var lod0=levels[0].renderers.Where(r=>r!=null).ToArray();
            var visualBounds=RendererBounds(root,lod0);
            result["lod0BoundsUnity"]=BoundsJson(visualBounds);
            var expectedBounds=(JObject)blender["lod0DriverRecord"]["boundsUnity"];
            if(!BoundsEqual(visualBounds,expectedBounds))failures.Add("run"+run+" axis/mirror: LOD0 bounds differ from Blender boundsUnity");
            result["axisCheck"]=MirrorCheck(lod0,reference.transform);
            if(!(bool)result["axisCheck"]["pass"])failures.Add("run"+run+" axis/mirror: per-mesh centroids differ from shared baseline");
            result["materialRemap"]=RemapCheck(lod0,palette);
            if(!(bool)result["materialRemap"]["pass"])failures.Add("run"+run+" material remap: "+result["materialRemap"]["error"]);
            result["colliders"]=ColliderCheck(root,lod0,(JArray)blender["colliders"],out var colliderFailures);
            failures.AddRange(colliderFailures.Select(x=>"run"+run+" "+x));
            result["visualColliderSeparation"]=SeparationCheck(model,out var separationFailure);
            if(separationFailure!=null)failures.Add("run"+run+" "+separationFailure);
            return result;
        }

        private static JObject CompareRuns(JObject a,JObject b,List<string> failures)
        {
            var keys=new[]{"referenceMeshesEqual","referenceRawNames","pilotRawNames","hierarchyRoot","rootTransform","lods","lod0BoundsUnity","axisCheck","materialRemap","colliders","visualColliderSeparation"};
            var result=new JObject();
            foreach(var key in keys)
            {
                var equal=JToken.DeepEquals(a[key],b[key]);
                result[key]=equal;
                if(!equal)failures.Add("run1/run2 differ in Unity import: "+key);
            }
            return result;
        }

        // Each detector must reject a deliberately broken instance; otherwise a pass proves nothing.
        private static JArray NegativeCases(string[] palette,JObject blender,GameObject reference,List<string> failures)
        {
            var cases=new JArray();
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(Load(FbxPath("1",Module+"_RoundTrip")));
            try
            {
                var root=instance.transform.Find(PilotRoot)??instance.transform;
                var lod0=instance.GetComponentInChildren<LODGroup>().GetLODs()[0].renderers.Where(r=>r!=null).ToArray();

                root.localScale=new Vector3(-1,1,1);
                var mirror=MirrorCheck(lod0,reference.transform);
                root.localScale=Vector3.one;
                cases.Add(Negative("mirroredX","localScale.x=-1 on "+root.name,(bool)mirror["pass"],mirror,failures));

                var missing=palette.Where(x=>x!="ClearGlass").ToArray();
                var remap=RemapCheck(lod0,missing);
                cases.Add(Negative("materialRemapMissing","palette without ClearGlass",(bool)remap["pass"],remap,failures));

                var collider=root.Cast<Transform>().First(t=>t.name.StartsWith("Collider_",StringComparison.Ordinal));
                collider.localPosition+=new Vector3(0,0,.25f);
                var displaced=ColliderCheck(root,lod0,(JArray)blender["colliders"],out var colliderFailures);
                cases.Add(Negative("colliderMeshMismatch",collider.name+" moved +0.25 m on Z",colliderFailures.Count==0,
                    new JObject{["colliders"]=displaced,["failures"]=new JArray(colliderFailures)},failures));
            }
            finally{UnityEngine.Object.DestroyImmediate(instance);}
            return cases;
        }

        private static JObject Negative(string id,string mutation,bool detectorPassed,JToken evidence,List<string> failures)
        {
            if(detectorPassed)failures.Add("negative case not detected: "+id);
            return new JObject{["id"]=id,["mutation"]=mutation,["expected"]="fail",["detected"]=!detectorPassed,["evidence"]=evidence};
        }

        private static Dictionary<string,Mesh> MeshTable(Transform root)=>
            root.GetComponentsInChildren<MeshFilter>(true).ToDictionary(f=>Normalize(f.name),f=>f.sharedMesh);

        private static string Normalize(string name)=>BuildOrderSuffix.Replace(Regex.Replace(name,@"_LOD\d+$",""),"");

        private static bool SameMesh(Mesh a,Mesh b)=>
            a.vertexCount==b.vertexCount&&a.subMeshCount==b.subMeshCount&&Triangles(a)==Triangles(b)&&
            BoundsEqual(a.bounds,b.bounds)&&a.vertices.Zip(b.vertices,(x,y)=>(x-y).sqrMagnitude).All(d=>d<1e-10f);

        private static int Triangles(Mesh mesh)=>Enumerable.Range(0,mesh.subMeshCount).Sum(i=>(int)mesh.GetIndexCount(i)/3);

        // World-space centroids: assets and fresh instances sit at the origin, so any mirror in the mesh data
        // or on any transform (Unity collapses the single pilot root into the asset root) moves them.
        private static JObject MirrorCheck(Renderer[] lod0,Transform reference)
        {
            var referenceCentroids=reference.GetComponentsInChildren<MeshFilter>(true).ToDictionary(f=>Normalize(f.name),Centroid);
            var result=new JObject();var pass=true;
            foreach(var renderer in lod0.OrderBy(r=>r.name,StringComparer.Ordinal))
            {
                var key=Normalize(renderer.name);var centroid=Centroid(renderer.GetComponent<MeshFilter>());
                var ok=referenceCentroids.TryGetValue(key,out var expected)&&(centroid-expected).magnitude<Tolerance;
                result[key]=new JObject{["centroid"]=Vec(centroid),["match"]=ok};
                pass&=ok;
            }
            return new JObject{["pass"]=pass,["meshes"]=result};
        }

        private static Vector3 Centroid(MeshFilter filter)
        {
            var vertices=filter.sharedMesh.vertices;var sum=Vector3.zero;
            foreach(var vertex in vertices)sum+=filter.transform.TransformPoint(vertex);
            return sum/vertices.Length;
        }

        private static JObject RemapCheck(Renderer[] renderers,string[] palette)
        {
            var shader=Shader.Find("Standard")??Shader.Find("Hidden/InternalErrorShader");
            var library=palette.ToDictionary(x=>x,x=>new Material(shader){name=x});
            try
            {
                foreach(var renderer in renderers)
                    foreach(var material in renderer.sharedMaterials)
                        if(material==null||!library.ContainsKey(material.name))
                            return new JObject{["pass"]=false,["error"]="Unmapped Blender material in "+renderer.name+": "+(material==null?"null":material.name)};
                return new JObject{["pass"]=true,["mapped"]=new JArray(renderers.SelectMany(r=>r.sharedMaterials).Select(m=>m.name).Distinct().OrderBy(x=>x,StringComparer.Ordinal))};
            }
            finally{foreach(var material in library.Values)UnityEngine.Object.DestroyImmediate(material);}
        }

        private static JArray ColliderCheck(Transform root,Renderer[] lod0,JArray expected,out List<string> failures)
        {
            failures=new List<string>();
            var visual=RendererBounds(root,lod0);visual.Expand(2*Tolerance);
            var result=new JArray();
            var found=root.Cast<Transform>().Where(t=>t.name.StartsWith("Collider_",StringComparison.Ordinal)).OrderBy(t=>t.name,StringComparer.Ordinal).ToArray();
            foreach(JObject item in expected)
            {
                var name=(string)item["name"];
                var collider=found.FirstOrDefault(t=>t.name==name);
                if(collider==null){failures.Add("collider missing: "+name);continue;}
                var bounds=LocalBounds(root,collider.GetComponent<MeshFilter>());
                var matchesAuthored=BoundsEqual(bounds,(JObject)item["boundsUnity"]);
                var insideVisual=visual.Contains(bounds.min)&&visual.Contains(bounds.max);
                result.Add(new JObject{["name"]=name,["boundsUnity"]=BoundsJson(bounds),["matchesAuthoredBox"]=matchesAuthored,["insideLod0Visual"]=insideVisual});
                if(!matchesAuthored)failures.Add("collider/mesh mismatch: "+name+" differs from authored collisionBox");
                if(!insideVisual)failures.Add("collider/mesh mismatch: "+name+" leaves LOD0 visual bounds");
            }
            if(found.Length!=expected.Count)failures.Add("collider count "+found.Length+" != authored "+expected.Count);
            return result;
        }

        // Builder output: gameplay colliders become BoxColliders without renderers; visual meshes keep none.
        private static JObject SeparationCheck(GameObject model,out string failure)
        {
            failure=null;
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                var importedMeshColliders=instance.GetComponentsInChildren<MeshCollider>(true).Length;
                foreach(var collider in instance.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Collider_",StringComparison.Ordinal)))
                {
                    var bounds=collider.GetComponent<MeshFilter>().sharedMesh.bounds;
                    UnityEngine.Object.DestroyImmediate(collider.GetComponent<MeshRenderer>());
                    UnityEngine.Object.DestroyImmediate(collider.GetComponent<MeshFilter>());
                    var box=collider.gameObject.AddComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;
                }
                var visualWithCollider=instance.GetComponentsInChildren<Renderer>(true).Count(r=>r.GetComponent<Collider>()!=null);
                var colliderWithRenderer=instance.GetComponentsInChildren<Collider>(true).Count(c=>c.GetComponent<Renderer>()!=null);
                var boxes=instance.GetComponentsInChildren<BoxCollider>(true).Length;
                if(importedMeshColliders!=0||visualWithCollider!=0||colliderWithRenderer!=0)
                    failure="visual/collider separation failed: meshColliders="+importedMeshColliders+" visualWithCollider="+visualWithCollider+" colliderWithRenderer="+colliderWithRenderer;
                return new JObject{["importedMeshColliders"]=importedMeshColliders,["gameplayBoxColliders"]=boxes,
                    ["visualRenderersWithCollider"]=visualWithCollider,["collidersWithRenderer"]=colliderWithRenderer};
            }
            finally{UnityEngine.Object.DestroyImmediate(instance);}
        }

        private static Bounds RendererBounds(Transform root,Renderer[] renderers)
        {
            var bounds=LocalBounds(root,renderers[0].GetComponent<MeshFilter>());
            foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(LocalBounds(root,renderer.GetComponent<MeshFilter>()));
            return bounds;
        }

        private static Bounds LocalBounds(Transform root,MeshFilter filter)
        {
            var vertices=filter.sharedMesh.vertices;
            var first=root.InverseTransformPoint(filter.transform.TransformPoint(vertices[0]));
            var bounds=new Bounds(first,Vector3.zero);
            foreach(var vertex in vertices)bounds.Encapsulate(root.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
            return bounds;
        }

        private static bool BoundsEqual(Bounds a,Bounds b)=>(a.min-b.min).magnitude<Tolerance&&(a.max-b.max).magnitude<Tolerance;

        private static bool BoundsEqual(Bounds a,JObject expected)=>
            BoundsEqual(a,new Bounds{min=ToVector(expected["min"]),max=ToVector(expected["max"])});

        private static Vector3 ToVector(JToken t)=>new Vector3((float)t[0],(float)t[1],(float)t[2]);

        private static JArray Vec(Vector3 v)=>new JArray(Math.Round(v.x,6),Math.Round(v.y,6),Math.Round(v.z,6));

        private static JObject BoundsJson(Bounds b)=>new JObject{["min"]=Vec(b.min),["max"]=Vec(b.max)};
    }
}
#endif
