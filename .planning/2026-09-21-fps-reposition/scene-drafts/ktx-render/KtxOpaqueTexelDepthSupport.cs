#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace ChooGuard.Editor
{
    public static class KtxOpaqueTexelDepthSupport
    {
        private const string ChildName="__KtxOpaqueTexelDepth";
        private const string Folder="Assets/ChooGuard/Art/Fps/Generated/KtxDepth";
        [Serializable] private class Manifest {public Entry[] materials;}
        [Serializable] private class Entry {public string material_name,alpha_mode,source_blend;public float[] export_rgba;public Histogram export_texture_alpha_summary;}
        [Serializable] private class Histogram {public int opaque;}
        [Serializable] private class Receipt {public string status;public int collidersBefore,collidersAfter,depthRenderers;public string[] supportedMaterials;}
        // Idempotent: add/update depth-only renderer children. Source color shaders/FBX/UV/colliders never changed.
        public static string Repair(string manifestPath)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit mode required");
            var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity")throw new InvalidOperationException("Existing FPS scene required");
            Transform world=null;foreach(var root in scene.GetRootGameObjects())if(root.name=="FPSWorld")world=root.transform;
            var ktx=world==null?null:world.Find("KTXSource");if(ktx==null)throw new InvalidOperationException("KTXSource not found");
            var shader=Shader.Find("ChooGuard/KTX/OpaqueTexelDepth");if(shader==null)throw new InvalidOperationException("Compile the scoped KTX depth shader first");
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));if(manifest?.materials==null)throw new InvalidDataException("Normalized KTX material manifest required");
            var eligible=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in manifest.materials)
                if(entry.source_blend=="normal"&&entry.alpha_mode=="transparent"&&entry.export_rgba!=null&&entry.export_rgba.Length==4&&entry.export_rgba[3]>=.99999f&&entry.export_texture_alpha_summary!=null&&entry.export_texture_alpha_summary.opaque>0)eligible.Add(entry.material_name);
            int before=world.GetComponentsInChildren<Collider>(true).Length;int count=0;var used=new HashSet<string>();var generated=new Dictionary<Material,Material>();
            Directory.CreateDirectory(Folder);
            var discard=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/DiscardAll.mat");if(discard==null){discard=new Material(shader);AssetDatabase.CreateAsset(discard,Folder+"/DiscardAll.mat");}discard.SetFloat("_OpaqueThreshold",2);EditorUtility.SetDirty(discard);
            var renderers=ktx.GetComponentsInChildren<MeshRenderer>(true);
            // Preflight all affected renderers before creating any support children.
            foreach(var renderer in renderers)
            {
                if(renderer.name==ChildName)continue;
                foreach(var source in renderer.sharedMaterials)if(source!=null&&eligible.Contains(source.name))
                    if(renderer.GetComponent<MeshFilter>()?.sharedMesh==null||!source.HasProperty("_BaseMap")||source.GetTexture("_BaseMap")==null||source.GetFloat("_ZWrite")!=0)throw new InvalidOperationException("Unexpected KTX color material contract: "+renderer.name);
            }
            foreach(var renderer in renderers)
            {
                if(renderer.name==ChildName)continue;var originals=renderer.sharedMaterials;var supports=new Material[originals.Length];bool needed=false;
                for(int slot=0;slot<originals.Length;slot++)
                {
                    var source=originals[slot];supports[slot]=discard;if(source==null||!eligible.Contains(source.name))continue;
                    needed=true;used.Add(source.name);
                    if(!generated.TryGetValue(source,out var depth))
                    {
                        string path=Folder+"/"+source.name+"_OpaqueDepth.mat";depth=AssetDatabase.LoadAssetAtPath<Material>(path);if(depth==null){depth=new Material(shader);AssetDatabase.CreateAsset(depth,path);}else depth.shader=shader;
                        depth.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));depth.SetTextureScale("_BaseMap",source.GetTextureScale("_BaseMap"));depth.SetTextureOffset("_BaseMap",source.GetTextureOffset("_BaseMap"));depth.SetColor("_BaseColor",source.GetColor("_BaseColor"));depth.SetFloat("_Cull",source.GetFloat("_Cull"));depth.SetFloat("_OpaqueThreshold",.99999f);depth.renderQueue=2499;EditorUtility.SetDirty(depth);generated.Add(source,depth);
                    }
                    supports[slot]=depth;
                }
                var existing=renderer.transform.Find(ChildName);
                if(!needed){if(existing!=null)UnityEngine.Object.DestroyImmediate(existing.gameObject);continue;}
                var child=existing==null?new GameObject(ChildName,typeof(MeshFilter),typeof(MeshRenderer)):existing.gameObject;child.transform.SetParent(renderer.transform,false);child.transform.localPosition=Vector3.zero;child.transform.localRotation=Quaternion.identity;child.transform.localScale=Vector3.one;child.layer=renderer.gameObject.layer;
                child.GetComponent<MeshFilter>().sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                var support=child.GetComponent<MeshRenderer>();support.sharedMaterials=supports;support.enabled=renderer.enabled;support.shadowCastingMode=ShadowCastingMode.Off;support.receiveShadows=false;support.lightProbeUsage=LightProbeUsage.Off;support.reflectionProbeUsage=ReflectionProbeUsage.Off;count++;
            }
            int after=world.GetComponentsInChildren<Collider>(true).Length;if(before!=after)throw new InvalidOperationException("Rendering repair changed collision count");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            return JsonUtility.ToJson(new Receipt{status="OPAQUE_TEXEL_DEPTH_INSTALLED_NATIVE_VISUAL_UNVERIFIED",collidersBefore=before,collidersAfter=after,depthRenderers=count,supportedMaterials=new List<string>(used).ToArray()},true);
        }
    }
}
#endif
