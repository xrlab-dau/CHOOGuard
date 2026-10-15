#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ChooGuard.App.Fps;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor
{
    // Draft: publish only after native asset/material/axis review. No RTS builder is invoked.
    public static class FpsStationSceneBuilder
    {
        [Serializable] public sealed class ModelSpec
        {
            public string name, assetPath;
            public Vector3 position, euler;
            public bool nativeAxesAndMaterialsVerified;
        }
        [Serializable] public sealed class OpeningSpec { public string meshPath,generatedAsset; public Vector3 center,size,euler; }
        [Serializable] public sealed class BuildSpec
        {
            public string outputScene="Assets/ChooGuard/Scenes/FpsStation.unity";
            public string koreanFontAsset;
            public OpeningSpec[] openings=Array.Empty<OpeningSpec>();
            public string[] excludedSourcePaths=Array.Empty<string>();
            public bool createPrototypeConnectorMaterials;
            public string receiptPath=".planning/2026-09-21-fps-reposition/fps-scene-native-receipt.json";
            public ModelSpec[] models=Array.Empty<ModelSpec>();
            // Exact paths relative to the composed scene root. No heuristic roof/floor classification.
            public string[] collisionMeshPaths=Array.Empty<string>();
            public string[] supportedFloorPaths=Array.Empty<string>();
            // World-space ray origins above candidate source-supported ground; ordered by preference.
            public Vector3[] spawnProbeOrigins=Array.Empty<Vector3>();
            public float probeDepth=6;
            public float spawnYaw;
            public string connectionManifest;
            public string connectorFloorMaterial,connectorRailMaterial;
            public string connectionStatus="DISCONNECTED_SOURCE_GEOMETRY";
        }
        [Serializable] public sealed class Receipt
        {
            public string scene, connectionManifest, connectionStatus;
            public int meshCount, colliderCount, materialSlots;
            public Vector3 spawnFeet;
            public string spawnSurface;
            public string[] collisionMeshes;
            public Bounds worldBounds;
            public OpeningSpec[] authoredOpenings;
            public string[] excludedSourcePaths;
            public string validation="Native scene composition and spawn ray/capsule only; no traversal/product acceptance";
        }

        public static void BuildFromJson(string specPath)
        {
            var spec=JsonUtility.FromJson<BuildSpec>(File.ReadAllText(specPath));
            if(spec==null)throw new InvalidOperationException("Missing build spec");
            Build(spec);
        }
        public static void Build(BuildSpec spec)
        {
            if(spec.outputScene!="Assets/ChooGuard/Scenes/FpsStation.unity")throw new InvalidOperationException("Only new FPS scene target is supported");
            if(File.Exists(spec.outputScene))throw new InvalidOperationException("FPS scene exists; review it before replacing");
            var original=SceneManager.GetActiveScene();
            if(original.name!="MvpWorkspace")throw new InvalidOperationException("Open original MvpWorkspace as active source scene");
            if(original.isDirty)throw new InvalidOperationException("Save or resolve existing source scene changes first; builder never discards them");
            if(spec.connectionStatus!="DISCONNECTED_SOURCE_GEOMETRY" && spec.connectionStatus!="PROVISIONAL_CONNECTOR_UNVERIFIED")throw new InvalidOperationException("Builder cannot certify connected traversal");
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(spec.koreanFontAsset);
            if(font==null)throw new InvalidOperationException("Explicit Korean TMP font required");
            var official=FindExact(original,"공식 자료 부산역 역사");
            var site=FindExact(original,"공식 자료 부산역 주변 공간");
            var stationView=official.GetComponentInParent<ChooGuard.App.Mvp.MvpStationView>(true);
            if(stationView==null||!site.IsChildOf(stationView.transform))throw new InvalidOperationException("Both official roots must share the original station frame");
            var sourceFrame=stationView.transform;
            if((sourceFrame.lossyScale-Vector3.one).sqrMagnitude>.0001f)throw new InvalidOperationException("Source station frame must have unit metre scale");
            var sourceAmbientMode=RenderSettings.ambientMode;var sourceAmbientLight=RenderSettings.ambientLight;
            var sourceAmbientSky=RenderSettings.ambientSkyColor;var sourceAmbientEquator=RenderSettings.ambientEquatorColor;var sourceAmbientGround=RenderSettings.ambientGroundColor;
            float sourceAmbientIntensity=RenderSettings.ambientIntensity,sourceReflectionIntensity=RenderSettings.reflectionIntensity;
            var sourceSkybox=RenderSettings.skybox;var sourceSun=RenderSettings.sun;
            bool sourceFog=RenderSettings.fog;var sourceFogColor=RenderSettings.fogColor;var sourceFogMode=RenderSettings.fogMode;
            float sourceFogDensity=RenderSettings.fogDensity,sourceFogStart=RenderSettings.fogStartDistance,sourceFogEnd=RenderSettings.fogEndDistance;
            var created=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            bool saved=false;
            try
            {
                SceneManager.SetActiveScene(created);
                RenderSettings.ambientMode=sourceAmbientMode;RenderSettings.ambientLight=sourceAmbientLight;RenderSettings.ambientSkyColor=sourceAmbientSky;RenderSettings.ambientEquatorColor=sourceAmbientEquator;RenderSettings.ambientGroundColor=sourceAmbientGround;RenderSettings.ambientIntensity=sourceAmbientIntensity;
                RenderSettings.skybox=sourceSkybox;RenderSettings.reflectionIntensity=sourceReflectionIntensity;RenderSettings.sun=null;
                RenderSettings.fog=sourceFog;RenderSettings.fogColor=sourceFogColor;RenderSettings.fogMode=sourceFogMode;RenderSettings.fogDensity=sourceFogDensity;RenderSettings.fogStartDistance=sourceFogStart;RenderSettings.fogEndDistance=sourceFogEnd;
                var root=new GameObject("FPSWorld");
                CloneVisuals(official,root.transform,sourceFrame);CloneVisuals(site,root.transform,sourceFrame);
                foreach(var officialRoot in new[]{root.transform.Find(official.name),root.transform.Find(site.name)})
                    if(Mathf.Abs(officialRoot.position.y)>100)throw new InvalidOperationException("Official geometry is outside normalized station frame: "+officialRoot.name);
                foreach(var excludedPath in spec.excludedSourcePaths)
                {
                    var excluded=root.transform.Find(excludedPath);
                    if(excluded==null)throw new InvalidOperationException("Explicit clone exclusion missing: "+excludedPath);
                    UnityEngine.Object.DestroyImmediate(excluded.gameObject);
                }
                foreach(var model in spec.models)
                {
                    if(!model.nativeAxesAndMaterialsVerified)throw new InvalidOperationException("Native import review required: "+model.name);
                    var asset=AssetDatabase.LoadAssetAtPath<GameObject>(model.assetPath);
                    if(asset==null)throw new InvalidOperationException("Missing model "+model.assetPath);
                    var clone=UnityEngine.Object.Instantiate(asset,root.transform);clone.name=model.name;
                    ApplyCanonicalPlacement(clone.transform,asset.transform,model);
                    ValidateCanonicalBounds(clone,asset,model);
                    StripBehavioursAndColliders(clone);
                }
                // Clone lights only, never camera/UI/legacy runtime components or world parents.
                foreach(var sourceRoot in original.GetRootGameObjects())foreach(var light in sourceRoot.GetComponentsInChildren<Light>(false))
                {
                    var go=new GameObject(light.name,typeof(Light));go.transform.SetParent(root.transform,false);
                    go.transform.SetPositionAndRotation(sourceFrame.InverseTransformPoint(light.transform.position),Quaternion.Inverse(sourceFrame.rotation)*light.transform.rotation);
                    var copied=go.GetComponent<Light>();EditorUtility.CopySerialized(light,copied);
                    copied.cullingMask=~0; // NEW scene lights illuminate all cloned/imported/authored geometry layers.
                    if(light==sourceSun)RenderSettings.sun=copied;
                }
                foreach(var opening in spec.openings)
                {
                    var cutTransform=root.transform.Find(opening.meshPath);var filter=cutTransform==null?null:cutTransform.GetComponent<MeshFilter>();
                    if(filter==null)throw new InvalidOperationException("Opening source missing: "+opening.meshPath);
                    if(!opening.generatedAsset.StartsWith("Assets/ChooGuard/Art/Fps/",StringComparison.Ordinal))throw new InvalidOperationException("Opening must be an isolated generated FPS mesh asset");
                    Directory.CreateDirectory(Path.GetDirectoryName(opening.generatedAsset));
                    FpsSourceOpening.Cut(filter,new Bounds(opening.center,opening.size),opening.euler,opening.generatedAsset);
                }
                var floors=new HashSet<Collider>();var colliderPaths=new List<string>();
                if(!string.IsNullOrEmpty(spec.connectionManifest))
                {
                    if(spec.createPrototypeConnectorMaterials)
                    {
                        EnsurePrototypeMaterial(spec.connectorFloorMaterial,new Color(.34f,.39f,.42f,1));
                        EnsurePrototypeMaterial(spec.connectorRailMaterial,new Color(.65f,.69f,.72f,1));
                    }
                    var floorMaterial=AssetDatabase.LoadAssetAtPath<Material>(spec.connectorFloorMaterial);
                    var railMaterial=AssetDatabase.LoadAssetAtPath<Material>(spec.connectorRailMaterial);
                    if(floorMaterial==null||railMaterial==null)throw new InvalidOperationException("Explicit visible authored connector materials required");
                    var transfer=JsonUtility.FromJson<FpsAuthoredTransferBuilder.Manifest>(File.ReadAllText(spec.connectionManifest));
                    foreach(var support in FpsAuthoredTransferBuilder.Build(root.transform,transfer,floorMaterial,railMaterial))floors.Add(support);
                }
                foreach(var path in spec.collisionMeshPaths)
                {
                    var t=root.transform.Find(path);var mesh=t==null?null:t.GetComponent<MeshFilter>();
                    if(mesh==null||mesh.sharedMesh==null)throw new InvalidOperationException("Explicit collision mesh missing: "+path);
                    var col=t.gameObject.AddComponent<MeshCollider>();col.sharedMesh=mesh.sharedMesh;col.convex=false;colliderPaths.Add(path);
                }
                foreach(var path in spec.supportedFloorPaths)
                {
                    var t=root.transform.Find(path);var col=t==null?null:t.GetComponent<MeshCollider>();
                    if(col==null)throw new InvalidOperationException("Supported floor has no selected source collider: "+path);floors.Add(col);
                }
                if(floors.Count==0)throw new InvalidOperationException("No reviewed source-supported floors");
                // Remove original scene colliders from physics queries without changing source state.
                var priorStates=new List<(GameObject obj,bool active)>();
                Vector3 feet=default;string hitPath=null;
                try
                {
                    foreach(var sourceRoot in original.GetRootGameObjects()){priorStates.Add((sourceRoot,sourceRoot.activeSelf));sourceRoot.SetActive(false);}
                    Physics.SyncTransforms();
                    foreach(var origin in spec.spawnProbeOrigins)
                    {
                        if(!Physics.Raycast(origin,Vector3.down,out var hit,Mathf.Clamp(spec.probeDepth,.1f,30),~0,QueryTriggerInteraction.Ignore))continue;
                        if(!floors.Contains(hit.collider)||Vector3.Dot(hit.normal,Vector3.up)<.7072f)continue;
                        var candidate=hit.point+Vector3.up*.045f;
                        if(Physics.CheckCapsule(candidate+Vector3.up*.28f,candidate+Vector3.up*1.44f,.28f,~0,QueryTriggerInteraction.Ignore))continue;
                        bool supported=true;
                        foreach(var offset in new[]{Vector3.right*.25f,Vector3.left*.25f,Vector3.forward*.25f,Vector3.back*.25f})
                            if(!Physics.Raycast(candidate+offset+Vector3.up*.15f,Vector3.down,out var edge,.28f,~0,QueryTriggerInteraction.Ignore)||!floors.Contains(edge.collider)||Vector3.Dot(edge.normal,Vector3.up)<.7072f){supported=false;break;}
                        if(!supported)continue;feet=candidate;hitPath=AnimationUtility.CalculateTransformPath(hit.transform,root.transform);break;
                    }
                }
                finally{foreach(var state in priorStates)state.obj.SetActive(state.active);Physics.SyncTransforms();}
                if(hitPath==null)throw new InvalidOperationException("No candidate passed native support ray and full capsule clearance");
                var player=new GameObject("KORAIL 역무원",typeof(CharacterController),typeof(FirstPersonResponder),typeof(FirstPersonInteractionHud));
                player.transform.SetPositionAndRotation(feet,Quaternion.Euler(0,spec.spawnYaw,0));
                var body=player.GetComponent<CharacterController>();body.height=1.72f;body.radius=.28f;body.center=Vector3.up*.86f;body.skinWidth=.025f;body.stepOffset=.28f;body.slopeLimit=45;
                var cameraObject=new GameObject("FirstPersonCamera",typeof(Camera),typeof(AudioListener));cameraObject.tag="MainCamera";cameraObject.transform.SetParent(player.transform,false);cameraObject.transform.localPosition=Vector3.up*1.60f;
                var camera=cameraObject.GetComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=1500;camera.fieldOfView=70;
                var responder=player.GetComponent<FirstPersonResponder>();responder.PlayerCamera=camera;responder.BodyHeight=1.72f;responder.EyeHeight=1.60f;responder.EnableJump=false;
                var hud=player.GetComponent<FirstPersonInteractionHud>();hud.Responder=responder;hud.KoreanFont=font;
                var receipt=new Receipt{scene=spec.outputScene,connectionManifest=spec.connectionManifest,connectionStatus=spec.connectionStatus,spawnFeet=feet,spawnSurface=hitPath,collisionMeshes=colliderPaths.ToArray(),authoredOpenings=spec.openings,excludedSourcePaths=spec.excludedSourcePaths,colliderCount=root.GetComponentsInChildren<Collider>(true).Length};
                foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true))if(mesh.sharedMesh!=null)receipt.meshCount++;
                bool firstBounds=true;
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(firstBounds){receipt.worldBounds=renderer.bounds;firstBounds=false;}else receipt.worldBounds.Encapsulate(renderer.bounds);
                    foreach(var material in renderer.sharedMaterials)
                    {
                    if(material==null||material.shader==null||material.shader.name=="Hidden/InternalErrorShader")throw new InvalidOperationException("Missing rendering material: "+renderer.name);
                    receipt.materialSlots++;
                    }
                }
                if(receipt.worldBounds.min.y < -100 || receipt.worldBounds.max.y > 500 || receipt.worldBounds.size.x > 5000 || receipt.worldBounds.size.z > 5000)throw new InvalidOperationException("Scene bounds violate station-local metre-frame guard: "+receipt.worldBounds);
                Directory.CreateDirectory(Path.GetDirectoryName(spec.outputScene));
                if(!EditorSceneManager.SaveScene(created,spec.outputScene))throw new IOException("SaveScene failed");saved=true;
                File.WriteAllText(spec.receiptPath,JsonUtility.ToJson(receipt,true));
                // Source history stays saved as-is, but unload to avoid duplicate geometry in play mode.
                EditorSceneManager.CloseScene(original,true);SceneManager.SetActiveScene(created);
            }
            catch
            {
                if(!saved){EditorSceneManager.CloseScene(created,true);SceneManager.SetActiveScene(original);}throw;
            }
        }
        // Preserve the FBX root conversion (for example rotation270deg/scale100) while composing world placement.
        // The object name and child hierarchy stay unchanged, so every existing collision path still resolves.
        private static void ApplyCanonicalPlacement(Transform target,Transform asset,ModelSpec model)
        {
            var placementRotation=Quaternion.Euler(model.euler);
            target.localPosition=model.position+placementRotation*asset.localPosition;
            target.localRotation=placementRotation*asset.localRotation;
            target.localScale=asset.localScale;
        }
        private static Bounds MeshBoundsInFrame(GameObject root,Matrix4x4 worldToFrame)
        {
            bool first=true;var result=new Bounds();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null)continue;var bounds=filter.sharedMesh.bounds;var matrix=worldToFrame*filter.transform.localToWorldMatrix;
                for(int bits=0;bits<8;bits++)
                {
                    var point=matrix.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3((bits&1)==0?-1:1,(bits&2)==0?-1:1,(bits&4)==0?-1:1)));
                    if(first){result=new Bounds(point,Vector3.zero);first=false;}else result.Encapsulate(point);
                }
            }
            if(first)throw new InvalidOperationException("No model meshes: "+root.name);return result;
        }
        private static void ValidateCanonicalBounds(GameObject clone,GameObject asset,ModelSpec model)
        {
            var parent=clone.transform.parent;
            if(parent==null||(parent.lossyScale-Vector3.one).sqrMagnitude>.0001f||parent.rotation!=Quaternion.identity||parent.position.sqrMagnitude>.0001f)throw new InvalidOperationException("FPS model parent must be identity metre frame");
            var expected=MeshBoundsInFrame(asset,Matrix4x4.identity);
            var inversePlacement=Matrix4x4.TRS(model.position,Quaternion.Euler(model.euler),Vector3.one).inverse;
            var actual=MeshBoundsInFrame(clone,inversePlacement);
            float tolerance=Mathf.Max(.005f,expected.size.magnitude*.0001f);
            if((actual.size-expected.size).magnitude>tolerance||(actual.center-expected.center).magnitude>tolerance)throw new InvalidOperationException("Canonical imported bounds mismatch: "+model.name+" expected="+expected+" actual="+actual);
            if(model.name=="KTXSource"&&(actual.size.z<195||actual.size.z>205||actual.size.x<2.8f||actual.size.x>3.5f||actual.size.y<4.5f||actual.size.y>5.6f))throw new InvalidOperationException("KTX metre bounds outside native reviewed envelope: "+actual);
        }
        [Serializable] private sealed class RepairReceipt { public string status;public int collidersBefore,collidersAfter;public string[] repairedModels; }
        public static string RepairExistingFromJson(string specPath)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Repair requires edit mode");
            var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity")throw new InvalidOperationException("Open the existing FPS scene");
            var spec=JsonUtility.FromJson<BuildSpec>(File.ReadAllText(specPath));var world=FindExact(scene,"FPSWorld");
            var targets=new List<Transform>();var assets=new List<GameObject>();var positions=new List<Vector3>();var rotations=new List<Quaternion>();var scales=new List<Vector3>();
            foreach(var model in spec.models)
            {
                var target=world.Find(model.name);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(model.assetPath);
                if(target==null||asset==null||!model.nativeAxesAndMaterialsVerified)throw new InvalidOperationException("Missing verified model repair dependency: "+model.name);
                targets.Add(target);assets.Add(asset);positions.Add(target.localPosition);rotations.Add(target.localRotation);scales.Add(target.localScale);
            }
            int before=world.GetComponentsInChildren<Collider>(true).Length;var names=new List<string>();
            try
            {
                for(int i=0;i<targets.Count;i++){Undo.RecordObject(targets[i],"Repair FPS imported canonical transform");ApplyCanonicalPlacement(targets[i],assets[i].transform,spec.models[i]);ValidateCanonicalBounds(targets[i].gameObject,assets[i],spec.models[i]);names.Add(spec.models[i].name);}
                Physics.SyncTransforms();int after=world.GetComponentsInChildren<Collider>(true).Length;if(before!=after)throw new InvalidOperationException("Repair changed collider count");
                EditorSceneManager.MarkSceneDirty(scene);
                return JsonUtility.ToJson(new RepairReceipt{status="CANONICAL_TRANSFORMS_REPAIRED_TRAVERSAL_NOT_TESTED",collidersBefore=before,collidersAfter=after,repairedModels=names.ToArray()},true);
            }
            catch
            {
                for(int i=0;i<targets.Count;i++){targets[i].localPosition=positions[i];targets[i].localRotation=rotations[i];targets[i].localScale=scales[i];}Physics.SyncTransforms();throw;
            }
        }
        private static void EnsurePrototypeMaterial(string path,Color color)
        {
            if(string.IsNullOrEmpty(path)||!path.StartsWith("Assets/ChooGuard/Art/Fps/Generated/",StringComparison.Ordinal)||path.Contains(".."))throw new InvalidOperationException("Authored connector material requires dedicated Generated path");
            if(AssetDatabase.LoadAssetAtPath<Material>(path)!=null)return;
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("Existing URP/Lit shader missing");
            Directory.CreateDirectory(Path.GetDirectoryName(path));var material=new Material(shader){name=Path.GetFileNameWithoutExtension(path)+"_AuthoredPrototype"};material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(material,path);
        }
        private static Transform FindExact(Scene source,string name)
        {
            Transform result=null;
            foreach(var root in source.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)
            {if(result!=null)throw new InvalidOperationException("Ambiguous source: "+name);result=t;}
            if(result==null)throw new InvalidOperationException("Missing official source: "+name);return result;
        }
        private static void CloneVisuals(Transform source,Transform parent,Transform sourceFrame)
        {
            var clone=UnityEngine.Object.Instantiate(source.gameObject);clone.name=source.name;
            var relative=sourceFrame.worldToLocalMatrix*source.localToWorldMatrix;
            clone.transform.SetParent(parent,false);clone.transform.localPosition=relative.GetColumn(3);clone.transform.localRotation=relative.rotation;clone.transform.localScale=relative.lossyScale;
            clone.SetActive(true);StripBehavioursAndColliders(clone);
        }
        private static void StripBehavioursAndColliders(GameObject root)
        {
            foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(behaviour);
            foreach(var col in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(col);
            foreach(var camera in root.GetComponentsInChildren<Camera>(true))UnityEngine.Object.DestroyImmediate(camera);
            foreach(var listener in root.GetComponentsInChildren<AudioListener>(true))UnityEngine.Object.DestroyImmediate(listener);
        }
    }
}
#endif
