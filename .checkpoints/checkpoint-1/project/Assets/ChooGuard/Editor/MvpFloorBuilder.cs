using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor
{
 public static class MvpFloorBuilder
 {
  const string Folder="Assets/ChooGuard/Settings/MvpFloors";
  const float Thickness=.3f,TileMetres=.7f;
  [Serializable] sealed class Site { public Feature[] features; }
  [Serializable] sealed class Feature { public string id; public Point[] points; }
  [Serializable] sealed class Point { public float x,z; }

  public static void Build(Transform parent,string name,List<Vector2> polygon,float top,int floor)
  {
   var old=parent.Find(name);var node=old!=null?old.gameObject:new GameObject(name);
   if(old==null)node.transform.SetParent(parent,false);
   Apply(node,polygon,top,floor);
  }
  public static void ReferencePatch(Transform parent)
  {
   Build(parent,"국소 기준 계산 영역",new List<Vector2>{new Vector2(-52,-48),new Vector2(-22,-48),new Vector2(-22,-28),new Vector2(-52,-28)},5.05f,1);
  }
  [MenuItem("ChooGuard/MVP/Upgrade Existing Floor Finishes")]
  public static void UpgradeExistingFloors()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("바닥 갱신은 편집 모드에서 실행하세요.");
   var scene=SceneManager.GetSceneByPath(MvpWorkspaceBuilder.ScenePath);
   if(!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("훈련 장면을 먼저 여세요.");
   var root=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="부산역 훈련 공간");
   if(root==null)throw new InvalidOperationException("기존 부산역 훈련 공간이 없습니다.");
   var feature=JsonUtility.FromJson<Site>(File.ReadAllText("asset-library/space-references/busan-reconstruction/busan-site-selected.json")).features.First(f=>f.id=="165346389");
   var source=feature.points.Select(p=>new Vector2(p.x,p.z)).ToList();if(Vector2.Distance(source[0],source[source.Count-1])<.001f)source.RemoveAt(source.Count-1);
   var clipped=new List<Vector2>();for(int i=0;i<source.Count;i++){var a=source[i];var b=source[(i+1)%source.Count];if(a.y<=68)clipped.Add(a);if((a.y<=68)!=(b.y<=68))clipped.Add(Vector2.Lerp(a,b,(68-a.y)/(b.y-a.y)));}
   var third=new List<Vector2>{new Vector2(-52,-72),new Vector2(-24,-80),new Vector2(12,-20),new Vector2(48,-32),new Vector2(60,-4),new Vector2(-4,16),new Vector2(-20,-20)};
   // Require the three existing slabs before modifying any of them; no whole-scene rebuilding.
   string[] names={"공개 외곽 기반 바닥","공개 외곽 기반 바닥","안내도 기반 식당가와 발코니"};
   for(int f=0;f<3;f++)if(root.transform.Find((f+1)+"층/"+names[f])==null)throw new InvalidDataException((f+1)+"층 기존 바닥이 없습니다.");
   for(int f=0;f<3;f++)Build(root.transform.Find((f+1)+"층"),names[f],f==2?third:clipped,f*5,f);
   ReferencePatch(root.transform.Find("2층"));AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
   if(!EditorSceneManager.SaveScene(scene))throw new IOException("바닥 변경 장면 저장 실패");
  }
  static void Apply(GameObject node,List<Vector2> source,float top,int floor)
  {
   Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
   var poly=Clean(source);var topIndices=Triangulate(poly);double polygonArea=Math.Abs(Area(poly)),triangleArea=0;
   for(int i=0;i<topIndices.Count;i+=3)triangleArea+=Math.Abs(Cross(poly[topIndices[i+1]]-poly[topIndices[i]],poly[topIndices[i+2]]-poly[topIndices[i]]))*.5;
   if(Math.Abs(triangleArea-polygonArea)>Math.Max(.001,polygonArea*.00001))throw new InvalidDataException("바닥 삼각형 면적 불일치: "+node.name);
   var vertices=new List<Vector3>();var uv=new List<Vector2>();var sides=new List<int>();
   foreach(var p in poly){vertices.Add(new Vector3(p.x,top,p.y));uv.Add(p/(TileMetres*4));}
   for(int i=0;i<poly.Count;i++)
   {
    var a=poly[i];var b=poly[(i+1)%poly.Count];int start=vertices.Count;float length=Vector2.Distance(a,b);
    vertices.AddRange(new[]{new Vector3(a.x,top,a.y),new Vector3(b.x,top,b.y),new Vector3(a.x,top-Thickness,a.y),new Vector3(b.x,top-Thickness,b.y)});
    uv.AddRange(new[]{Vector2.zero,new Vector2(length,0),new Vector2(0,Thickness),new Vector2(length,Thickness)});
    sides.AddRange(new[]{start,start+1,start+2,start+1,start+3,start+2});
   }
   int bottom=vertices.Count;foreach(var p in poly){vertices.Add(new Vector3(p.x,top-Thickness,p.y));uv.Add(p/(TileMetres*4));}
   for(int i=0;i<topIndices.Count;i+=3)sides.AddRange(new[]{bottom+topIndices[i],bottom+topIndices[i+2],bottom+topIndices[i+1]});
   var mesh=new Mesh{name=node.name+" · 0.3m 석재 슬래브"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=2;mesh.SetTriangles(topIndices,0);mesh.SetTriangles(sides,1);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
   mesh=MvpMeshPersistence.Store(Folder+"/"+node.transform.parent.name+"-"+node.name+".asset",mesh);
   var filter=node.GetComponent<MeshFilter>();if(filter==null)filter=node.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
   var renderer=node.GetComponent<MeshRenderer>();if(renderer==null)renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{Finish(floor),Edge()};
   node.transform.localPosition=Vector3.zero;node.transform.localRotation=Quaternion.identity;node.transform.localScale=Vector3.one;node.layer=node.transform.parent.gameObject.layer;
   GameObjectUtility.SetStaticEditorFlags(node,GameObjectUtility.GetStaticEditorFlags(node)&~StaticEditorFlags.BatchingStatic);
   EditorUtility.SetDirty(node);Debug.Log($"바닥 면적 확인 {node.transform.parent.name}/{node.name}: polygon={polygonArea:F3}m² topTriangles={triangleArea:F3}m² top={top:F2}m bottom={top-Thickness:F2}m UV=.7m tile");
  }
  static List<Vector2> Clean(List<Vector2> source)
  {
   var p=new List<Vector2>();foreach(var v in source)if(p.Count==0||Vector2.Distance(p[p.Count-1],v)>.0001f)p.Add(v);
   if(p.Count>1&&Vector2.Distance(p[0],p[p.Count-1])<.0001f)p.RemoveAt(p.Count-1);
   bool changed=true;while(changed&&p.Count>3){changed=false;for(int i=0;i<p.Count;i++)if(Mathf.Abs(Cross(p[i]-p[(i+p.Count-1)%p.Count],p[(i+1)%p.Count]-p[i]))<.000001f){p.RemoveAt(i);changed=true;break;}}
   if(p.Count<3)throw new InvalidDataException("바닥 다각형이 유효하지 않습니다.");if(Area(p)<0)p.Reverse();return p;
  }
  static List<int> Triangulate(List<Vector2> p)
  {
   var left=Enumerable.Range(0,p.Count).ToList();var result=new List<int>();int guard=0;
   while(left.Count>2&&guard++<p.Count*p.Count)
   {
    bool found=false;
    for(int i=0;i<left.Count;i++)
    {
     int a=left[(i+left.Count-1)%left.Count],b=left[i],c=left[(i+1)%left.Count];if(Cross(p[b]-p[a],p[c]-p[b])<=.000001f)continue;
     if(left.Any(q=>q!=a&&q!=b&&q!=c&&Cross(p[b]-p[a],p[q]-p[a])>=-.000001f&&Cross(p[c]-p[b],p[q]-p[b])>=-.000001f&&Cross(p[a]-p[c],p[q]-p[c])>=-.000001f))continue;
     // Reversed XZ winding gives +Y normals.
     result.AddRange(new[]{a,c,b});left.RemoveAt(i);found=true;break;
    }
    if(!found)throw new InvalidDataException("바닥 다각형 삼각분할이 중단되었습니다. 원본 형상을 보존합니다.");
   }
   return result;
  }
  static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
  static double Area(List<Vector2> p){double a=0;for(int i=0;i<p.Count;i++)a+=(double)p[i].x*p[(i+1)%p.Count].y-(double)p[(i+1)%p.Count].x*p[i].y;return a*.5;}
  static Material Finish(int floor)
  {
   string[] names={"1층 중성 회색 석재","2층 밝은 테라조","3층 따뜻한 석재"};Color[] palette={new Color(.58f,.59f,.58f),new Color(.75f,.75f,.70f),new Color(.68f,.62f,.51f)};
   string path=Folder+"/"+names[floor]+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
   string texPath=Folder+"/"+names[floor]+"-700mm.asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
   if(texture==null)
   {
    const int size=512;texture=new Texture2D(size,size,TextureFormat.RGBA32,true){name=names[floor]+" · 파생 마감",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=8};var pixels=new Color[size*size];
    for(int y=0;y<size;y++)for(int x=0;x<size;x++)
    {
     int tx=x/128,ty=y/128;float tile=1+(((tx*17+ty*29+floor*7)%11)-5)*.004f;
     float grain=(Mathf.PerlinNoise(x*.19f+floor*3,y*.19f)-.5f)*.025f;
     if(floor==1&&((x*73856093L^y*19349663L)&127)<7)grain+=.06f;
     float seam=(x%128==0||y%128==0)?.92f:1;pixels[y*size+x]=palette[floor]*(tile+grain)*seam;
    }
    texture.SetPixels(pixels);texture.Apply(true,false);AssetDatabase.CreateAsset(texture,texPath);
   }
   material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",texture);material.SetFloat("_Smoothness",floor==1?.32f:.24f);material.SetFloat("_Metallic",0);EditorUtility.SetDirty(material);return material;
  }
  static Material Edge(){string path=Folder+"/석재 측면.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",new Color(.46f,.45f,.41f));m.SetFloat("_Smoothness",.18f);EditorUtility.SetDirty(m);return m;}
 }
}
