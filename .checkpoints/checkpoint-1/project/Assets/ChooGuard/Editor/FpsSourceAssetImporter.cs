#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChooGuard.Editor
{
    public static class FpsSourceAssetImporter
    {
        [Serializable] public class Manifest { public string schema; public Entry[] materials; }
        [Serializable] public class Entry
        {
            public string material_name,source_day_texture,export_texture,texture_sha256,alpha_mode,source_blend;
            public string additive_equation;
            public float[] source_rgba,export_rgba,source_emissive_rgb;
            public float suggested_alpha_cutoff=.5f;
            public bool double_sided_required;
        }
        private static string Hash(string path)
        {using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        private static void ValidateComponents(float[] values,int count,string field,string name)
        {
            if(values==null||values.Length!=count)throw new InvalidDataException("Missing "+field+": "+name);
            foreach(float value in values)if(float.IsNaN(value)||float.IsInfinity(value)||value<0||value>1)throw new InvalidDataException("Invalid "+field+": "+name);
        }
        public static string Import(string sourceFbx,string manifestPath,string targetDirectory)
        {
            if(!targetDirectory.StartsWith("Assets/ChooGuard/Art/Fps/",StringComparison.Ordinal)||targetDirectory.Contains(".."))throw new ArgumentException("Dedicated FPS art directory required");
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            if(manifest?.materials==null||manifest.materials.Length==0)throw new InvalidDataException("Normalized named {materials:[...]} manifest required");
            var entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
            // Validate entire source contract before publishing any asset. No default white material fallback.
            foreach(var e in manifest.materials)
            {
                if(e==null||string.IsNullOrWhiteSpace(e.material_name)||e.material_name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||e.material_name.Contains("/")||e.material_name.Contains("\\"))throw new InvalidDataException("Invalid material name");
                if(entries.ContainsKey(e.material_name))throw new InvalidDataException("Duplicate material "+e.material_name);entries.Add(e.material_name,e);
                ValidateComponents(e.source_rgba,4,"source RGBA",e.material_name);ValidateComponents(e.export_rgba,4,"export RGBA multiplier",e.material_name);ValidateComponents(e.source_emissive_rgb,3,"emissive RGB",e.material_name);
                if(e.alpha_mode!="opaque"&&e.alpha_mode!="cutout"&&e.alpha_mode!="transparent")throw new InvalidDataException("Unverified alpha mode: "+e.material_name);
                if(e.source_blend!="normal"&&e.source_blend!="additive")throw new InvalidDataException("Unsupported source blend: "+e.material_name);
                if(e.source_blend=="additive"&&e.additive_equation!="src_alpha_one")throw new InvalidDataException("Verified source additive equation required: "+e.material_name);
                if(e.alpha_mode=="cutout"&&(!(e.suggested_alpha_cutoff>0)||e.suggested_alpha_cutoff>1))throw new InvalidDataException("Invalid alpha cutoff "+e.material_name);
                if(!string.IsNullOrEmpty(e.source_day_texture)&&string.IsNullOrEmpty(e.export_texture))throw new InvalidDataException("Source texture missing export binding: "+e.material_name);
                if(!string.IsNullOrEmpty(e.export_texture))
                {
                    if(!File.Exists(e.export_texture))throw new FileNotFoundException("Missing source texture",e.export_texture);
                    if(string.IsNullOrEmpty(e.texture_sha256)||!string.Equals(Hash(e.export_texture),e.texture_sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Source texture SHA256 mismatch: "+e.material_name);
                }
            }
            var shader=Shader.Find("Universal Render Pipeline/Lit");var unlit=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null||unlit==null)throw new InvalidOperationException("Existing project URP/Lit shader unavailable");
            Directory.CreateDirectory(targetDirectory);Directory.CreateDirectory(targetDirectory+"/Textures");Directory.CreateDirectory(targetDirectory+"/Materials");
            var target=targetDirectory+"/"+Path.GetFileName(sourceFbx);File.Copy(sourceFbx,target,true);AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(target);importer.globalScale=1;importer.useFileScale=true;importer.isReadable=true;importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(target);var usedNames=new HashSet<string>(StringComparer.Ordinal);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)
            {
                if(material==null)throw new InvalidDataException("FBX contains unbound material: "+renderer.name);
                if(!entries.ContainsKey(material.name))throw new InvalidDataException("FBX material absent from normalized manifest: "+material.name);
                usedNames.Add(material.name);
            }
            if(usedNames.Count==0)throw new InvalidDataException("Imported model contains no material slots");
            var expected=new Dictionary<string,Material>(StringComparer.Ordinal);
            foreach(var name in usedNames)
            {
                var e=entries[name];var chosenShader=e.source_blend=="additive"?unlit:shader;string matPath=targetDirectory+"/Materials/"+name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(material==null){material=new Material(chosenShader){name=name};AssetDatabase.CreateAsset(material,matPath);}else material.shader=chosenShader;
                var c=e.export_rgba;material.SetColor("_BaseColor",new Color(c[0],c[1],c[2],c[3]));material.SetFloat("_Cull",e.double_sided_required?0:2);if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.2f);material.SetTexture("_BaseMap",null);
                material.SetTextureScale("_BaseMap",Vector2.one);material.SetTextureOffset("_BaseMap",Vector2.zero);
                if(!string.IsNullOrEmpty(e.export_texture))
                {
                    string texturePath=targetDirectory+"/Textures/"+e.texture_sha256.Substring(0,16)+Path.GetExtension(e.export_texture).ToLowerInvariant();File.Copy(e.export_texture,texturePath,true);AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
                    var ti=(TextureImporter)AssetImporter.GetAtPath(texturePath);ti.alphaSource=TextureImporterAlphaSource.FromInput;ti.alphaIsTransparency=false;ti.sRGBTexture=true;ti.maxTextureSize=16384;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.SaveAndReimport();
                    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);if(texture==null)throw new InvalidDataException("Texture import failed "+texturePath);material.SetTexture("_BaseMap",texture);
                }
                bool additive=e.source_blend=="additive",transparent=e.alpha_mode=="transparent"||additive,cutout=e.alpha_mode=="cutout";
                material.SetFloat("_Surface",transparent?1:0);material.SetFloat("_Blend",additive?2:0);material.SetFloat("_AlphaClip",cutout?1:0);material.SetFloat("_Cutoff",e.suggested_alpha_cutoff);
                material.SetFloat("_SrcBlend",(float)(transparent?BlendMode.SrcAlpha:BlendMode.One));material.SetFloat("_DstBlend",(float)(additive?BlendMode.One:transparent?BlendMode.OneMinusSrcAlpha:BlendMode.Zero));
                material.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);material.SetFloat("_DstBlendAlpha",(float)(additive?BlendMode.One:transparent?BlendMode.OneMinusSrcAlpha:BlendMode.Zero));material.SetFloat("_ZWrite",transparent?0:1);
                material.SetOverrideTag("RenderType",transparent?"Transparent":cutout?"TransparentCutout":"Opaque");material.renderQueue=transparent?3000:cutout?2450:2000;
                SetKeyword(material,"_SURFACE_TYPE_TRANSPARENT",transparent);SetKeyword(material,"_ALPHATEST_ON",cutout);SetKeyword(material,"_ALPHAPREMULTIPLY_ON",false);SetKeyword(material,"_ALPHAMODULATE_ON",false);
                var emission=e.source_emissive_rgb;if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",new Color(emission[0],emission[1],emission[2]));SetKeyword(material,"_EMISSION",emission[0]+emission[1]+emission[2]>0);
                EditorUtility.SetDirty(material);expected.Add(name,material);importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),name),material);
            }
            importer.SaveAndReimport();AssetDatabase.SaveAssets();model=AssetDatabase.LoadAssetAtPath<GameObject>(target);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)
                if(material==null||!expected.TryGetValue(material.name,out var bound)||material!=bound)throw new InvalidDataException("Post-import material remap verification failed: "+renderer.name);
            return target;
        }
        private static void SetKeyword(Material material,string keyword,bool value){if(value)material.EnableKeyword(keyword);else material.DisableKeyword(keyword);}
    }
}
#endif
