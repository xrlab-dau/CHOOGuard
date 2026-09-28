using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
public static class ExportMainSource {
 public static void Main() {
  if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit-mode read-only query required.");
  var go=GameObject.Find("FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_0");
  if(go==null) throw new InvalidOperationException("Exact original source mesh path missing");
  var mf=go.GetComponent<MeshFilter>(); var mesh=mf.sharedMesh;
  float theta=16.2f*Mathf.Deg2Rad, s=Mathf.Sin(theta), c=Mathf.Cos(theta);
  using(var data=Mesh.AcquireReadOnlyMeshData(mesh)) {
   var d=data[0]; var native=new Unity.Collections.NativeArray<Vector3>(d.vertexCount,Unity.Collections.Allocator.Temp);
   d.GetVertices(native); var local=native.ToArray(); native.Dispose();
   var vertices=local.Select(p=>mf.transform.TransformPoint(p)).Select(p=>new[]{p.x*s+p.z*c,p.x*c-p.z*s,p.y}).ToArray();
   var submeshes=new int[d.subMeshCount][];
   for(int k=0;k<d.subMeshCount;k++) { var sub=d.GetSubMesh(k);var ids=new Unity.Collections.NativeArray<int>(sub.indexCount,Unity.Collections.Allocator.Temp);d.GetIndices(ids,k);submeshes[k]=ids.ToArray();ids.Dispose(); }
   var result=new {schema="chooguard.original-source-readonly.v1",sourcePath="FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_0",meshAsset=AssetDatabase.GetAssetPath(mesh),meshName=mesh.name,scenePath=go.scene.path,sceneDirty=go.scene.isDirty,vertexCount=vertices.Length,coordinateFrame="UVY; worldX=u*sin(16.2)+v*cos(16.2),worldZ=u*cos(16.2)-v*sin(16.2)",verticesUVY=vertices,submeshes=submeshes,materials=go.GetComponent<Renderer>().sharedMaterials.Select(m=>m==null?null:m.name).ToArray()};
   File.WriteAllText(".planning/2026-09-23-video-twin/main-metric-registration/source-mesh.json",JsonConvert.SerializeObject(result));
   Debug.Log("MAIN_SOURCE_EXPORTED "+vertices.Length+" vertices; "+submeshes.Length+" submeshes; no scene mutations");
  }
 }
}
