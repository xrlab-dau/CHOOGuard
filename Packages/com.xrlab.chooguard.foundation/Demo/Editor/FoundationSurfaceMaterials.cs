using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Foundation.Demo.Editor
{
    // Small deterministic authored textures. No photographs, offline render or light bake.
    public static class FoundationSurfaceMaterials
    {
        public static readonly string[] FloorNames = { "ConcourseFloor", "CorridorFloor", "AssemblyFloor",
            "WestWaitingFloor", "WestBypassFloor", "WestLinkFloor", "EastWaitingFloor", "EastBypassFloor", "EastLinkFloor" };
        public static readonly string[] TextureNames = { "StoneTile", "BrushedSteel", "RoofPanel" };

        public static void CreateTextures(string root)
        {
            if (!AssetDatabase.IsValidFolder(root+"/Textures")) AssetDatabase.CreateFolder(root,"Textures");
            foreach(var name in TextureNames)
            {
                var size=name=="StoneTile"?512:128;
                var texture=new Texture2D(size,size,TextureFormat.RGB24,false);
                var pixels=new Color[size*size];
                for(var y=0;y<size;y++) for(var x=0;x<size;x++)
                {
                    uint seed=unchecked((uint)(x*374761393+y*668265263+1376312589));
                    seed=(seed^(seed>>13))*1274126177u;
                    var grain=(seed&1023)/1023f;
                    float value;
                    if(name=="StoneTile")
                    {
                        value=.83f+(grain-.5f)*.18f;
                        if(grain<.035f)value-=.16f;
                        if(x<2||y<2||x>=size-2||y>=size-2)value=.55f;
                    }
                    else if(name=="BrushedSteel") value=.84f+Mathf.Sin(y*7.13f)*.035f+(grain-.5f)*.025f;
                    else value=(x%32<2?.55f:.82f)+(grain-.5f)*.025f;
                    pixels[y*size+x]=new Color(value,value,value,1);
                }
                texture.SetPixels(pixels);texture.Apply();
                var path=root+"/Textures/"+name+".png";
                File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
                importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;
                importer.mipmapEnabled=true;importer.maxTextureSize=size;importer.anisoLevel=4;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
        }

        public static void Configure(Material material,string name,string root)
        {
            var metallic=name=="Stainless"?.85f:0;
            var smooth=name=="Stainless"?.62f:name=="Glass"?.78f:name=="Floor"||name=="Stone"?.42f:
                name=="Rubber"?.08f:name=="Roof"?.18f:.28f;
            Set(material,"_Metallic",metallic);Set(material,"_Glossiness",smooth);Set(material,"_Smoothness",smooth);
            Set(material,"_SpecularHighlights",1);Set(material,"_GlossyReflections",1);
            material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");material.DisableKeyword("_GLOSSYREFLECTIONS_OFF");
            var textureName=name=="Floor"||name=="Stone"?"StoneTile":name=="Stainless"?"BrushedSteel":name=="Ceiling"?"RoofPanel":null;
            if(material.HasProperty("_MainTex")||material.HasProperty("_BaseMap"))
            {
                material.mainTexture=textureName==null?null:AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/Textures/"+textureName+".png");
                material.mainTextureScale=Vector2.one;
            }
            // Clerestory glazing uses a daylight-tinted opaque surface: no view through an unmodeled exterior.
            // It is a representation limit, not a surveyed optical property of the station's glass.
            var emissive=name=="Diffuser"||name=="Glass";
            if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",emissive?
                (name=="Diffuser"?new Color(.75f,.78f,.72f):new Color(.14f,.20f,.23f)):Color.black);
            if(emissive)material.EnableKeyword("_EMISSION");else material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
        }

        public static void ApplyFloorScale(Transform environment,string root,Material floor)
        {
            foreach(var name in FloorNames)
            {
                var surface=environment.Find(name);
                if(surface==null)throw new System.InvalidOperationException("Missing floor surface: "+name);
                var path=root+"/Materials/Floor_"+name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(floor);AssetDatabase.CreateAsset(material,path);}
                else {material.shader=floor.shader;material.CopyPropertiesFromMaterial(floor);}
                material.name="Floor_"+name;
                material.mainTextureScale=new Vector2(surface.lossyScale.x,surface.lossyScale.z);
                surface.GetComponent<Renderer>().sharedMaterial=material;
                EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
            }
        }
        private static void Set(Material material,string property,float value)
        {if(material.HasProperty(property))material.SetFloat(property,value);}
    }
}
