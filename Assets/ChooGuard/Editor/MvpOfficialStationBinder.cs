using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ChooGuard.App.Mvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChooGuard.Editor
{
    // Source visual assembly only. No floor/nav/solver rebuild or inferred interior alignment.
    public static class MvpOfficialStationBinder
    {
        public const string AssetFolder="Assets/ChooGuard/Art/OfficialBusanStation/";
        public const string StageName="Official source world staging";
        public const string ShellName="공식 자료 부산역 역사";
        public const string ContextName="공식 자료 부산역 주변 공간";
        public const string StageReceiptPath="art/world/official-station-staging-receipt.json";
        [Serializable] public sealed class Segment { public string name,file,role,sha256;public float[] sourceBoundsMin,sourceBoundsMax; }
        [Serializable] public sealed class Registration { public int version=1;public string basis,materialManifest;public float metresPerUnit=1;public Vector3 localTranslation,localEulerDegrees;public Segment[] segments; }
        [Serializable] private sealed class MaterialManifest { public int schemaVersion;public SourceMaterial[] materials; }
        [Serializable] private sealed class SourceMaterial { public string sourceName,texturePath,textureSha256,colorSpace;public float[] colorRGBA;public float alpha,roughness,metallic;public bool sourceTextured; }
        [Serializable] private sealed class CoverageReceipt { public bool success,oldGeometryExcluded;public string registrationHash,scope; }
        [Serializable] private sealed class MeshReadback { public string segment,role,asset,assetHash;public Vector3 importedMin,importedMax,fittedMin,fittedMax;public int meshes,vertices,triangles,materials,texturedSlots;public bool persistentReadableMeshes,uvPreserved; }
        [Serializable] private sealed class Receipt { public string status,registrationHash,materialManifestHash;public Vector3 localTranslation,localEulerDegrees;public MeshReadback[] segments;public string[] retainedReferences;public int[] retainedIds;public int stageInstanceId;public bool nativeAxesVerified,refsUnchanged;public string coverageReceiptHash; }
        private sealed class Backup { public Transform Transform,Parent;public Vector3 Position,Scale;public Quaternion Rotation;public bool Active; }
        private static readonly Dictionary<string,bool> TextureAlpha=new Dictionary<string,bool>();
        public static Registration ReadRegistration(string path)
        {
            var r=JsonUtility.FromJson<Registration>(File.ReadAllText(path));
            if(r==null||r.version!=1||Mathf.Abs(r.metresPerUnit-1)>.00001f||!Finite(r.localTranslation)||!Finite(r.localEulerDegrees)||r.segments==null||r.segments.Length<3||r.segments.Length>20)throw new InvalidDataException("Explicit metre-native rigid registration required.");
            if(string.IsNullOrWhiteSpace(r.basis)||!File.Exists(r.materialManifest))throw new InvalidDataException("Source basis/material manifest missing.");
            var ids=new HashSet<string>();foreach(var s in r.segments)
            {
                if(s==null||string.IsNullOrWhiteSpace(s.name)||!ids.Add(s.name)||(s.role!="station"&&s.role!="context")||!s.file.StartsWith(AssetFolder,StringComparison.Ordinal)||!s.file.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)||!File.Exists(s.file)||Hash(s.file)!=s.sha256)throw new InvalidDataException("Missing, duplicate or changed approved segment.");
                if(s.sourceBoundsMin==null||s.sourceBoundsMax==null||s.sourceBoundsMin.Length!=3||s.sourceBoundsMax.Length!=3)throw new InvalidDataException("Source metre bounds required for every segment.");
                for(int i=0;i<3;i++)if(!Finite(s.sourceBoundsMin[i])||!Finite(s.sourceBoundsMax[i])||s.sourceBoundsMax[i]<=s.sourceBoundsMin[i])throw new InvalidDataException("Invalid source bounds.");
            }
            if(!r.segments.Any(s=>s.role=="station")||!r.segments.Any(s=>s.role=="context"))throw new InvalidDataException("Full source world requires separate station and context segments.");return r;
        }
        public static void ConfigureImports(string registrationPath)
        {
            RequireEdit();var registration=ReadRegistration(registrationPath);
            foreach(var segment in registration.segments)
            {
                var importer=AssetImporter.GetAtPath(segment.file) as ModelImporter;if(importer==null)throw new InvalidDataException("FBX importer missing: "+segment.file);
                importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=false;importer.isReadable=true;importer.importAnimation=false;importer.importNormals=ModelImporterNormals.Import;importer.meshCompression=ModelImporterMeshCompression.Off;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
            }
        }
        public static GameObject StageExisting(string registrationPath)
        {
            var view=RequireStation();var retained=Retained(view);var registration=ReadRegistration(registrationPath);
            if(view.transform.Find(StageName)!=null)throw new InvalidOperationException("A prior stage exists; inspect/remove that owned stage explicitly first.");
            ConfigureImports(registrationPath);var materials=ReadMaterials(registration.materialManifest);Directory.CreateDirectory(AssetFolder+"Materials");AssetDatabase.Refresh();TextureAlpha.Clear();
            var stage=new GameObject(StageName);stage.transform.SetParent(view.transform,false);stage.SetActive(false);
            var shell=new GameObject(ShellName);shell.transform.SetParent(stage.transform,false);var context=new GameObject(ContextName);context.transform.SetParent(stage.transform,false);
            foreach(var root in new[]{shell.transform,context.transform}){root.localPosition=registration.localTranslation;root.localRotation=Quaternion.Euler(registration.localEulerDegrees);root.localScale=Vector3.one;}
            var rows=new List<MeshReadback>();
            try
            {
                foreach(var segment in registration.segments)
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(segment.file);if(prefab==null)throw new InvalidDataException("Imported source prefab missing.");
                    var parent=segment.role=="station"?shell.transform:context.transform;var instance=UnityEngine.Object.Instantiate(prefab,parent,false);instance.name=segment.name;
                    int textured=BindMaterials(instance.transform,materials);Layer(instance.transform);
                    var meshes=instance.GetComponentsInChildren<MeshFilter>(true);if(meshes.Length==0)throw new InvalidDataException("Segment has no meshes.");
                    foreach(var filter in meshes){var mesh=filter.sharedMesh;if(mesh==null||!AssetDatabase.Contains(mesh)||!mesh.isReadable||mesh.vertexCount<3)throw new InvalidDataException("Source mesh must remain persistent/readable.");mesh.UploadMeshData(false);}
                    var imported=BoundsIn(instance.transform,parent);var fitted=BoundsIn(instance.transform,view.transform);ValidateMetres(imported,segment);
                    rows.Add(new MeshReadback{segment=segment.name,role=segment.role,asset=segment.file,assetHash=Hash(segment.file),importedMin=imported.min,importedMax=imported.max,fittedMin=fitted.min,fittedMax=fitted.max,meshes=meshes.Length,vertices=meshes.Sum(x=>x.sharedMesh.vertexCount),triangles=meshes.Sum(x=>x.sharedMesh.triangles.Length/3),materials=instance.GetComponentsInChildren<Renderer>(true).Sum(x=>x.sharedMaterials.Length),texturedSlots=textured,persistentReadableMeshes=true,uvPreserved=true});
                }
                Layer(stage.transform);if(!retained.SequenceEqual(Retained(view)))throw new InvalidOperationException("Functional station references changed during staging.");
                var receipt=new Receipt{status="STAGED_INACTIVE_NATIVE_AXES_AND_FUNCTIONAL_ALIGNMENT_PENDING",registrationHash=Hash(registrationPath),materialManifestHash=Hash(registration.materialManifest),localTranslation=registration.localTranslation,localEulerDegrees=registration.localEulerDegrees,segments=rows.ToArray(),retainedReferences=retained.Select(x=>x==null?"null":x.name).ToArray(),retainedIds=retained.Select(x=>x==null?0:x.GetInstanceID()).ToArray(),stageInstanceId=stage.GetInstanceID(),refsUnchanged=true,nativeAxesVerified=false};
                Directory.CreateDirectory("art/world");File.WriteAllText(StageReceiptPath,JsonUtility.ToJson(receipt,true));AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(view.gameObject.scene);return stage;
            }
            catch {UnityEngine.Object.DestroyImmediate(stage);throw;}
        }
        // The root supplies the hash it inspected AFTER native metre/handedness/landmark readback.
        // Staging never disables the prior world. No city/floor/NavMesh builder is called here.
        public static void ApplyExisting(string registrationPath,string inspectedStageReceiptSha256,string coverageReceiptPath,bool nativeAxesVerified)
            =>PublishExisting(registrationPath,inspectedStageReceiptSha256,coverageReceiptPath,nativeAxesVerified);
        public static void PublishExisting(string registrationPath,string inspectedStageReceiptSha256,string coverageReceiptPath,bool nativeAxesVerified)
        {
            var view=RequireStation();var registration=ReadRegistration(registrationPath);var retained=Retained(view);
            if(!nativeAxesVerified||Hash(StageReceiptPath)!=inspectedStageReceiptSha256)throw new InvalidOperationException("Explicit native axis/scale readback and inspected stage receipt required.");
            var receipt=JsonUtility.FromJson<Receipt>(File.ReadAllText(StageReceiptPath));
            var coverage=JsonUtility.FromJson<CoverageReceipt>(File.ReadAllText(coverageReceiptPath));
            if(receipt.registrationHash!=Hash(registrationPath)||receipt.materialManifestHash!=Hash(registration.materialManifest)||!coverage.success||!coverage.oldGeometryExcluded||coverage.registrationHash!=receipt.registrationHash||coverage.scope!="city-root-only")throw new InvalidDataException("Registration/material/coverage receipts do not match the current source stage.");
            var stage=view.transform.Find(StageName);if(stage==null||stage.gameObject.GetInstanceID()!=receipt.stageInstanceId||!retained.Select(x=>x==null?0:x.GetInstanceID()).SequenceEqual(receipt.retainedIds))throw new InvalidOperationException("Staged instance or retained references changed; restage required.");
            var shell=stage.Find(ShellName);var context=stage.Find(ContextName);if(shell==null||context==null)throw new InvalidOperationException("Separate source shell/context missing.");
            var old=new List<Transform>();foreach(string name in new[]{"Evidence restored station facade","부산역 곡면 유리 역사","역 앞 광장",ShellName}){var candidate=view.WholeEnvelope.Find(name);if(candidate!=null)old.Add(candidate);}var oldContext=view.transform.Find(ContextName);if(oldContext!=null)old.Add(oldContext);
            // Exact authored decoration roots from StationBuilder/InteriorBuilder, not functional floor/nav roots.
            if(view.FloorRoots!=null&&view.FloorRoots.Length>0&&view.FloorRoots[0]!=null)foreach(Transform child in view.FloorRoots[0])if(child.name=="고속 열차"||child.name=="승강장 구조 · 파생 캐노피")old.Add(child);
            var saved=old.Select(x=>new Backup{Transform=x,Parent=x.parent,Position=x.localPosition,Rotation=x.localRotation,Scale=x.localScale,Active=x.gameObject.activeSelf}).ToArray();
            var backup=new GameObject("Legacy source world backup "+DateTime.UtcNow.ToString("yyyyMMddHHmmss"));backup.transform.SetParent(view.transform,false);backup.SetActive(false);
            try
            {
                foreach(var item in old){item.SetParent(backup.transform,true);item.gameObject.SetActive(false);}
                shell.SetParent(view.WholeEnvelope,true);context.SetParent(view.transform,true);shell.gameObject.SetActive(true);context.gameObject.SetActive(true);
                if(!retained.SequenceEqual(Retained(view)))throw new InvalidOperationException("Functional references changed during source-world publication.");
                receipt.status="PUBLISHED_SOURCE_WORLD_NATIVE_VISUAL_FUNCTIONAL_REALIGNMENT_STILL_REQUIRED";receipt.nativeAxesVerified=true;receipt.coverageReceiptHash=Hash(coverageReceiptPath);receipt.refsUnchanged=true;
                File.WriteAllText("art/world/official-station-publication-native-receipt.json",JsonUtility.ToJson(receipt,true));
                stage.name="Official source publication receipt holder "+DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                EditorSceneManager.MarkSceneDirty(view.gameObject.scene);if(!EditorSceneManager.SaveScene(view.gameObject.scene))throw new IOException("Scene save failed.");
            }
            catch
            {
                stage.name=StageName;shell.SetParent(stage,false);context.SetParent(stage,false);shell.localPosition=context.localPosition=registration.localTranslation;shell.localRotation=context.localRotation=Quaternion.Euler(registration.localEulerDegrees);shell.localScale=context.localScale=Vector3.one;
                foreach(var item in saved){item.Transform.SetParent(item.Parent,false);item.Transform.localPosition=item.Position;item.Transform.localRotation=item.Rotation;item.Transform.localScale=item.Scale;item.Transform.gameObject.SetActive(item.Active);}UnityEngine.Object.DestroyImmediate(backup);throw;
            }
        }
        private static Dictionary<string,SourceMaterial> ReadMaterials(string path)
        {
            var manifest=JsonUtility.FromJson<MaterialManifest>(File.ReadAllText(path));if(manifest==null||manifest.schemaVersion!=1||manifest.materials==null)throw new InvalidDataException("Source material manifest unavailable.");
            var rows=new Dictionary<string,SourceMaterial>(StringComparer.Ordinal);foreach(var m in manifest.materials)
            {
                if(m==null||string.IsNullOrWhiteSpace(m.sourceName)||rows.ContainsKey(m.sourceName)||m.colorRGBA==null||m.colorRGBA.Length!=4||m.colorRGBA.Any(v=>!Finite(v)||v<0||v>1)||!Finite(m.alpha)||m.alpha<0||m.alpha>1||!Finite(m.roughness)||m.roughness<0||m.roughness>1||!Finite(m.metallic)||m.metallic<0||m.metallic>1)throw new InvalidDataException("Invalid source material definition.");
                if(m.sourceTextured&&(string.IsNullOrEmpty(m.texturePath)||!m.texturePath.StartsWith(AssetFolder+"Textures/",StringComparison.Ordinal)||!File.Exists(m.texturePath)||Hash(m.texturePath)!=m.textureSha256))throw new InvalidDataException("Required source texture missing or changed: "+m.sourceName);
                rows.Add(m.sourceName,m);
            }return rows;
        }
        private static int BindMaterials(Transform root,Dictionary<string,SourceMaterial> manifest)
        {
            int textured=0;foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var mapped=new Material[renderer.sharedMaterials.Length];for(int i=0;i<mapped.Length;i++)
                {
                    var imported=renderer.sharedMaterials[i];if(imported==null||!manifest.TryGetValue(imported.name,out var source))throw new InvalidDataException("Unknown imported material; no fallback: "+(imported==null?"null":imported.name));
                    mapped[i]=PersistentMaterial(source);if(source.sourceTextured){textured++;var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null||filter.sharedMesh.uv.Length!=filter.sharedMesh.vertexCount)throw new InvalidDataException("Source texture UV0 missing.");}
                }renderer.sharedMaterials=mapped;
            }return textured;
        }
        private static Material PersistentMaterial(SourceMaterial source)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP Lit unavailable; prior world retained.");
            Texture2D texture=null;bool transparent=source.alpha<.999f;
            if(source.sourceTextured){texture=AssetDatabase.LoadAssetAtPath<Texture2D>(source.texturePath);if(texture==null)throw new InvalidDataException("Source texture import unavailable.");if(!TextureAlpha.TryGetValue(source.texturePath,out bool alpha)){var probe=new Texture2D(2,2,TextureFormat.RGBA32,false,true);try{if(!ImageConversion.LoadImage(probe,File.ReadAllBytes(source.texturePath),false))throw new InvalidDataException("Source texture cannot be decoded.");alpha=probe.GetPixels32().Any(p=>p.a<255);TextureAlpha[source.texturePath]=alpha;}finally{UnityEngine.Object.DestroyImmediate(probe);}}transparent|=alpha;}
            string identity=JsonUtility.ToJson(source);string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(identity))).Replace("-","").ToLowerInvariant();string path=AssetFolder+"Materials/source-"+hash.Substring(0,20)+".mat";
            var color=new Color(source.colorRGBA[0],source.colorRGBA[1],source.colorRGBA[2],source.alpha);var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(existing!=null){if(existing.shader!=shader||existing.GetTexture("_BaseMap")!=texture||existing.GetColor("_BaseColor")!=color||Mathf.Abs(existing.GetFloat("_Smoothness")-(1-source.roughness))>.0001f||Mathf.Abs(existing.GetFloat("_Metallic")-source.metallic)>.0001f||existing.GetFloat("_Cull")!=(float)CullMode.Off||existing.GetFloat("_Surface")!=(transparent?1:0))throw new InvalidDataException("Persistent source material was modified; refusing overwrite.");return existing;}
            var material=new Material(shader){name=source.sourceName};material.SetColor("_BaseColor",color);material.SetTexture("_BaseMap",texture);material.SetFloat("_Smoothness",Mathf.Clamp01(1-source.roughness));material.SetFloat("_Metallic",Mathf.Clamp01(source.metallic));material.SetFloat("_Cull",(float)CullMode.Off);
            if(transparent){material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=(int)RenderQueue.Transparent;}
            AssetDatabase.CreateAsset(material,path);return material;
        }
        private static Bounds BoundsIn(Transform root,Transform frame)
        {
            bool first=true;var bounds=new Bounds();foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){var matrix=frame.worldToLocalMatrix*filter.transform.localToWorldMatrix;foreach(var vertex in filter.sharedMesh.vertices){var p=matrix.MultiplyPoint3x4(vertex);if(!Finite(p))throw new InvalidDataException("Nonfinite source vertex.");if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}}if(first)throw new InvalidDataException("Empty source geometry.");return bounds;
        }
        private static void ValidateMetres(Bounds imported,Segment source)
        {
            var expected=Enumerable.Range(0,3).Select(i=>source.sourceBoundsMax[i]-source.sourceBoundsMin[i]).OrderBy(x=>x).ToArray();var actual=new[]{imported.size.x,imported.size.y,imported.size.z}.OrderBy(x=>x).ToArray();
            for(int i=0;i<3;i++)if(Mathf.Abs(actual[i]-expected[i])>Mathf.Max(.05f,expected[i]*.002f))throw new InvalidDataException("Native FBX size differs from source metre bounds; inspect importer axes/units, do not rescale to fit.");
        }
        private static UnityEngine.Object[] Retained(MvpStationView view)
        {
            var items=new List<UnityEngine.Object>{view,view.WholeEnvelope,view.ViewCamera,view.Navigation,view.PlatformAnchor,view.ConcourseAnchor,view.ExitAnchor,view.IncidentMarker,view.CrowdRoot};if(view.FloorRoots!=null)items.AddRange(view.FloorRoots);if(view.Teams!=null)items.AddRange(view.Teams);items.AddRange(view.GetComponents<Component>());return items.ToArray();
        }
        private static MvpStationView RequireStation(){RequireEdit();var view=UnityEngine.Object.FindFirstObjectByType<MvpStationView>();if(view==null||view.WholeEnvelope==null)throw new InvalidOperationException("Existing station functional view/envelope required.");if((view.transform.lossyScale-Vector3.one).sqrMagnitude>.00001f)throw new InvalidOperationException("Station frame must already be metre-native.");return view;}
        private static void RequireEdit(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit mode.");}
        private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        private static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
        private static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        private static void Layer(Transform root){foreach(var t in root.GetComponentsInChildren<Transform>(true)){t.gameObject.layer=29;t.gameObject.isStatic=false;GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);}}
    }
}
