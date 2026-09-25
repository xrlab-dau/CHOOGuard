using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ChooGuard.App.Mvp;
using ChooGuard.App.Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.Editor
{
 public static class MvpTeamNavigationBuilder
 {
  public const string DataPath="Assets/ChooGuard/Settings/MvpTeamNavigation/SecondFloor-v1.bytes";
  [MenuItem("ChooGuard/MVP/Bake Team Navigation")]
  public static void Build()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Bake requires edit mode");
   var view=UnityEngine.Object.FindFirstObjectByType<MvpStationView>(FindObjectsInactive.Include);
   if(view==null||view.FloorRoots==null||view.FloorRoots.Length<2)throw new InvalidOperationException("Station second floor missing");
   var verts=new List<float>();var tris=new List<int>();int floors=0,obstacles=0;
   foreach(var mf in view.FloorRoots[1].GetComponentsInChildren<MeshFilter>(true))
   {
    if(mf.sharedMesh==null)continue;
    bool floor=mf.name=="공개 외곽 기반 바닥"||mf.name=="국소 기준 계산 영역";
    var mesh=mf.sharedMesh;var points=mesh.vertices.Select(p=>view.transform.InverseTransformPoint(mf.transform.TransformPoint(p))).ToArray();
    if(points.Length==0)continue;
    float low=points.Min(p=>p.y),high=points.Max(p=>p.y);
    // Exclude ceilings, signage, low trim and other floors; include actual mesh obstacles at walking body height.
    if(!floor&&(low>6.8f||high<5.35f))continue;
    int offset=verts.Count/3;foreach(var p in points){verts.Add(p.x);verts.Add(p.y);verts.Add(p.z);}
    var indices=floor?mesh.GetTriangles(0):mesh.triangles;foreach(int i in indices)tris.Add(offset+i);
    if(floor)floors++;else obstacles++;
   }
   if(floors!=2)throw new InvalidDataException("Both actual 2F slab and reference floor required");
   var bytes=GameplayNavigationBaker.BakeTriangles(verts,tris,out var digest);
   Directory.CreateDirectory(Path.GetDirectoryName(DataPath));File.WriteAllBytes(DataPath,bytes);
   AssetDatabase.ImportAsset(DataPath,ImportAssetOptions.ForceSynchronousImport);
   var nav=view.GetComponent<MvpTeamNavigation>();if(nav==null)nav=view.gameObject.AddComponent<MvpTeamNavigation>();nav.NavData=AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
   nav.GeometryDigest=digest;
   nav.Reload();EditorUtility.SetDirty(nav);EditorSceneManager.MarkSceneDirty(view.gameObject.scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(view.gameObject.scene);
   Debug.Log($"TEAM_NAV_BAKED floors={floors} obstacleMeshes={obstacles} bytes={bytes.Length} geometry={nav.GeometryDigest}");
  }
 }
}
