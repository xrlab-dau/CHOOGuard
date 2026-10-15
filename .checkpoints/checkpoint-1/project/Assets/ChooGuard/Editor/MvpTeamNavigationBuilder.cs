using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using ChooGuard.App.Mvp;
using DotRecast.Core;
using DotRecast.Recast;
using DotRecast.Recast.Geom;
using DotRecast.Detour;
using DotRecast.Detour.Io;
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
   var geom=new RcSampleInputGeomProvider(verts,tris);
   var cfg=new RcConfig(RcPartition.WATERSHED,.2f,.05f,40,1.7f,.3f,.15f,2,4,12,1.1f,6,6,1,true,true,true,new RcAreaModification(1),true);
   var result=new RcBuilder().Build(geom,new RcBuilderConfig(cfg,geom.GetMeshBoundsMin(),geom.GetMeshBoundsMax()),false);
   var pmesh=result.Mesh;var detail=result.MeshDetail;for(int i=0;i<pmesh.npolys;i++)pmesh.flags[i]=1;
   var option=new DtNavMeshCreateParams{verts=pmesh.verts,vertCount=pmesh.nverts,polys=pmesh.polys,polyAreas=pmesh.areas,polyFlags=pmesh.flags,polyCount=pmesh.npolys,nvp=pmesh.nvp,detailMeshes=detail.meshes,detailVerts=detail.verts,detailVertsCount=detail.nverts,detailTris=detail.tris,detailTriCount=detail.ntris,walkableHeight=1.7f,walkableRadius=.3f,walkableClimb=.15f,bmin=pmesh.bmin,bmax=pmesh.bmax,cs=.2f,ch=.05f,buildBvTree=true};
   var data=DtNavMeshBuilder.CreateNavMeshData(option);if(data==null)throw new InvalidDataException("Empty bake");
   Directory.CreateDirectory(Path.GetDirectoryName(DataPath));using(var writer=new BinaryWriter(File.Create(DataPath)))new DtMeshDataWriter().Write(writer,data,RcByteOrder.LITTLE_ENDIAN,false);
   AssetDatabase.ImportAsset(DataPath,ImportAssetOptions.ForceSynchronousImport);
   var nav=view.GetComponent<MvpTeamNavigation>();if(nav==null)nav=view.gameObject.AddComponent<MvpTeamNavigation>();nav.NavData=AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
   using(var hash=SHA256.Create())nav.GeometryDigest=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join(",",verts.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))+"|"+string.Join(",",tris)))).Replace("-","").ToLowerInvariant();
   nav.Reload();EditorUtility.SetDirty(nav);EditorSceneManager.MarkSceneDirty(view.gameObject.scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(view.gameObject.scene);
   Debug.Log($"TEAM_NAV_BAKED floors={floors} obstacleMeshes={obstacles} polygons={pmesh.npolys} geometry={nav.GeometryDigest}");
  }
 }
}
