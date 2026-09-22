using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChooGuard.Editor
{
    // Editor-only, visual geometry. Never registers colliders or solver surfaces.
    public static class MvpWorldSurfaceBuilder
    {
        const string Folder="Assets/ChooGuard/Art/WorldSurfaces";
        [Serializable] public class Point { public float x,z; }
        [Serializable] public class Layer { public string kind; public Point[] points; }
        [Serializable] public class Building { public string id,district,name; public int archetype; }
        [Serializable] public class Prop { public string kind; public float x,z,size,angle,footprintWidth,footprintDepth; }
        [Serializable] class TextureSpec { public string baseColor,roughness,normalGL; }
        [Serializable] class MaterialSpec { public string name;public float[] baseColor;public float roughness,metallic;public TextureSpec textures; }
        [Serializable] class ModelSpec { public string name,fbx; }
        [Serializable] class Manifest { public MaterialSpec[] materials;public ModelSpec[] models; }
        static Dictionary<string,Material> worldMaterials;
        [Serializable] public class Heightfield { public float minX,minZ,step;public int nx,nz;public float[] heights; }
        [Serializable] public class Data { public Heightfield heightfield; public float sourceScale; public Layer[] layers;public Building[] buildings;public Prop[] props; }
        static Data current;
        public static Data Read() { current=JsonUtility.FromJson<Data>(File.ReadAllText("art/world/world-layers.json"));if(Mathf.Abs(current.sourceScale-1f)>.0001f)throw new InvalidDataException("Regenerate world-layers.json for metre-native scene.");return current; }
        public static float Height(float worldX,float worldZ)
        {
            var h=current?.heightfield;if(h==null||h.heights==null)return 0;
            float fx=Mathf.Clamp((worldX-h.minX)/h.step,0,h.nx-1.001f),fz=Mathf.Clamp((worldZ-h.minZ)/h.step,0,h.nz-1.001f);int x=Mathf.FloorToInt(fx),z=Mathf.FloorToInt(fz);fx-=x;fz-=z;
            float a=h.heights[z*h.nx+x],b=h.heights[z*h.nx+x+1],c=h.heights[(z+1)*h.nx+x+1],d=h.heights[(z+1)*h.nx+x];
            return (fx>=fz?a+(b-a)*fx+(c-b)*fz:a+(c-d)*fx+(d-a)*fz);
        }
        // These meshes are already spatially/material batched. A controlled comparison in
        // this scene found automatic static batching displaced facade detail in Play mode.
        public static void RepairStaticFlags(Transform root)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.gameObject.isStatic=false;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,0);
            }
        }
        public static void BuildUnder(Transform root,Data data)
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();ConfigureWorldSetMaterials();
            var chunks=new Dictionary<string,List<Vector3>>();var kinds=new Dictionary<string,string>();
            foreach(var layer in data.layers) for(int i=0;i+2<layer.points.Length;i+=3)
            {
                var p=layer.points[i];string key=layer.kind+"_"+Mathf.FloorToInt(p.x/512f)+"_"+Mathf.FloorToInt(p.z/512f);
                if(!chunks.TryGetValue(key,out var vertices)){vertices=new List<Vector3>();chunks[key]=vertices;kinds[key]=layer.kind;}
                float y=layer.kind=="water"?-.20f:layer.kind=="land"?-.15f:layer.kind=="park"?-.08f:layer.kind=="walk"?.04f:layer.kind=="plaza"?.03f:layer.kind=="asphalt"?-.05f:layer.kind=="curb"?.10f:0f;
                // Constrained triangulation may use either winding; force +Y normals.
                var q1=layer.points[i+1];var q2=layer.points[i+2];float cross=(q1.x-p.x)*(q2.z-p.z)-(q1.z-p.z)*(q2.x-p.x);if(Mathf.Abs(cross)<.000001f)continue;
                foreach(int n in cross>0?new[]{0,2,1}:new[]{0,1,2}){var q=layer.points[i+n];vertices.Add(new Vector3(q.x*data.sourceScale,y+(layer.kind=="water"?0:Height(q.x*data.sourceScale,q.z*data.sourceScale)),q.z*data.sourceScale));}
            }
            int triangleCount=0;
            foreach(var pair in chunks){Emit(root,pair.Key,pair.Value,Surface(kinds[pair.Key]));triangleCount+=pair.Value.Count/3;}
            int loaded=0,fallback=0;var propMeshes=new Dictionary<string,List<CombineInstance>>();var propMaterials=new Dictionary<string,Material>();
            foreach(var prop in data.props)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChooGuard/Art/WorldSet/"+prop.kind+".fbx");
                // Missing authored assets are explicit; root rebuilds after import.
                if(prefab==null){fallback++;continue;}
                var go=UnityEngine.Object.Instantiate(prefab,root);go.name=prop.kind;go.transform.localPosition=new Vector3(prop.x*data.sourceScale,Height(prop.x*data.sourceScale,prop.z*data.sourceScale)+.04f,prop.z*data.sourceScale);go.transform.localRotation=Quaternion.Euler(0,prop.angle,0);
                var renderers=go.GetComponentsInChildren<Renderer>();var bounds=new Bounds();bool first=true;
                foreach(var rr in renderers){var mapped=rr.sharedMaterials;for(int mi=0;mi<mapped.Length;mi++)if(mapped[mi]!=null&&worldMaterials.TryGetValue(mapped[mi].name.Replace(" (Instance)",""),out var replacement))mapped[mi]=replacement;rr.sharedMaterials=mapped;if(first){bounds=rr.bounds;first=false;}else bounds.Encapsulate(rr.bounds);rr.gameObject.layer=29;}
                if(bounds.size.y>.001f)
                {
                    float sy=prop.size*data.sourceScale/bounds.size.y;
                    var current=go.transform.localScale;
                    go.transform.localScale=prop.footprintWidth>0&&bounds.size.x>.001f&&bounds.size.z>.001f?Vector3.Scale(current,new Vector3(prop.footprintWidth*data.sourceScale/bounds.size.x,sy,prop.footprintDepth*data.sourceScale/bounds.size.z)):current*sy;
                    // Centre the measured authored bounds on the source footprint centroid and ground its base.
                    bool firstFit=true;var fitted=new Bounds();foreach(var rr in renderers){if(firstFit){fitted=rr.bounds;firstFit=false;}else fitted.Encapsulate(rr.bounds);}
                    var desired=root.TransformPoint(new Vector3(prop.x*data.sourceScale,Height(prop.x*data.sourceScale,prop.z*data.sourceScale)+.04f,prop.z*data.sourceScale));go.transform.position+=new Vector3(desired.x-fitted.center.x,desired.y-fitted.min.y,desired.z-fitted.center.z);
                }
                foreach(var col in go.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(col);
                go.layer=29;go.isStatic=false;GameObjectUtility.SetStaticEditorFlags(go,0);loaded++;
                if(prop.kind!="BusanTower"&&prop.kind!="PortTerminal"&&prop.kind!="JagalchiMarket")
                {
                    foreach(var filter in go.GetComponentsInChildren<MeshFilter>())
                    {
                        var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||filter.sharedMesh==null)continue;
                        for(int sub=0;sub<filter.sharedMesh.subMeshCount&&sub<renderer.sharedMaterials.Length;sub++)
                        {
                            var material=renderer.sharedMaterials[sub];if(material==null)continue;
                            string key=prop.kind+"_"+Mathf.FloorToInt(prop.x/512f)+"_"+Mathf.FloorToInt(prop.z/512f)+"_"+material.name;
                            if(!propMeshes.TryGetValue(key,out var list)){list=new List<CombineInstance>();propMeshes[key]=list;propMaterials[key]=material;}
                            list.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=sub,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                        }
                    }
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
            foreach(var pair in propMeshes)
            {
                var mesh=new Mesh{name=pair.Key,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                string path=Folder+"/prop_"+pair.Key.Replace("/","_")+".asset";mesh=MvpMeshPersistence.Store(path,mesh);
                var go=new GameObject(pair.Key,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.layer=29;go.isStatic=false;GameObjectUtility.SetStaticEditorFlags(go,0);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=propMaterials[pair.Key];
            }
            RepairStaticFlags(root);
            Debug.Log("World surfaces: chunks="+chunks.Count+", triangles="+triangleCount+", authored placements="+loaded+", missing asset placements="+fallback+" (visual only)");
        }
        public static void ConfigureWorldSetMaterials()
        {
            worldMaterials=new Dictionary<string,Material>();var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("art/blender/world-set-manifest.json"));
            foreach(var model in manifest.models)if(!File.Exists(model.fbx))throw new FileNotFoundException("WorldSet FBX missing",model.fbx);
            foreach(var spec in manifest.materials)
            {
                string path=Folder+"/"+spec.name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}material.shader=Shader.Find("Universal Render Pipeline/Lit");
                material.SetColor("_BaseColor",new Color(spec.baseColor[0],spec.baseColor[1],spec.baseColor[2],spec.baseColor.Length>3?spec.baseColor[3]:1));material.SetFloat("_Metallic",spec.metallic);material.SetFloat("_Smoothness",1-spec.roughness);
                if(spec.textures!=null)
                {
                    if(!string.IsNullOrEmpty(spec.textures.baseColor))material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(spec.textures.baseColor));
                    if(!string.IsNullOrEmpty(spec.textures.normalGL))
                    {
                        var importer=AssetImporter.GetAtPath(spec.textures.normalGL) as TextureImporter;if(importer!=null&&importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();}
                        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(spec.textures.normalGL));material.SetFloat("_BumpScale",.45f);material.EnableKeyword("_NORMALMAP");
                    }
                    string packed=Folder+"/"+spec.name+"-metallic-smoothness.png";
                    if(File.Exists(packed)){var importer=AssetImporter.GetAtPath(packed) as TextureImporter;if(importer!=null&&importer.sRGBTexture){importer.sRGBTexture=false;importer.SaveAndReimport();}material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(packed));material.SetFloat("_Smoothness",1);material.EnableKeyword("_METALLICSPECGLOSSMAP");}
                }
                worldMaterials[spec.name]=material;EditorUtility.SetDirty(material);
            }
        }
        static Material Surface(string kind)
        {
            string path=Folder+"/"+kind+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            Color color=kind=="asphalt"?new Color(.19f,.21f,.22f):kind=="curb"?new Color(.68f,.66f,.59f):kind=="water"?new Color(.075f,.30f,.37f):kind=="park"?new Color(.30f,.39f,.22f):kind=="land"?new Color(.48f,.47f,.39f):kind=="plaza"?new Color(.70f,.66f,.55f):new Color(.57f,.57f,.53f);
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",kind=="water"?.78f:.16f);
            string texPath=Folder+"/"+kind+"-grain.asset";var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if(tex==null){tex=new Texture2D(64,64,TextureFormat.RGBA32,true);tex.name=kind+" grain";tex.wrapMode=TextureWrapMode.Repeat;var colors=new Color[4096];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float v=.88f+Mathf.PerlinNoise(x*.31f,y*.31f)*.20f;if((kind=="sidewalk"||kind=="plaza")&&(x%16==0||y%16==0))v*=.80f;colors[y*64+x]=new Color(v,v,v,1);}tex.SetPixels(colors);tex.Apply();AssetDatabase.CreateAsset(tex,texPath);}
            m.SetTexture("_BaseMap",tex);EditorUtility.SetDirty(m);return m;
        }
        static void Emit(Transform root,string name,List<Vector3> vertices,Material material)
        {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);var triangles=new int[vertices.Count];var uv=new List<Vector2>();for(int i=0;i<vertices.Count;i++){triangles[i]=i;uv.Add(new Vector2(vertices[i].x*.5f,vertices[i].z*.5f));}mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Folder+"/"+name+".asset";mesh=MvpMeshPersistence.Store(path,mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.layer=29;go.isStatic=false;GameObjectUtility.SetStaticEditorFlags(go,0);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
    }
}
