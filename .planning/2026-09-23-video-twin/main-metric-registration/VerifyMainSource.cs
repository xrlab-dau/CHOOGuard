using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public static class VerifyMainSource {
 public static void Main() {
  if(EditorApplication.isPlaying) throw new InvalidOperationException("Read-only edit-mode verification required.");
  string dir=".planning/2026-09-23-video-twin/main-metric-registration/";
  var before=JObject.Parse(File.ReadAllText(dir+"source-mesh.json"));
  var go=GameObject.Find((string)before["sourcePath"]);var mf=go.GetComponent<MeshFilter>();var mesh=mf.sharedMesh;
  float s=Mathf.Sin(16.2f*Mathf.Deg2Rad),c=Mathf.Cos(16.2f*Mathf.Deg2Rad),maxDelta=0;bool indicesSame=true;
  using(var data=Mesh.AcquireReadOnlyMeshData(mesh)) {
   var d=data[0];if(d.vertexCount!=(int)before["vertexCount"])throw new InvalidOperationException("Vertex count changed");
   var native=new Unity.Collections.NativeArray<Vector3>(d.vertexCount,Unity.Collections.Allocator.Temp);d.GetVertices(native);
   for(int i=0;i<native.Length;i++){var p=mf.transform.TransformPoint(native[i]);var b=before["verticesUVY"][i];maxDelta=Mathf.Max(maxDelta,Mathf.Abs(p.x*s+p.z*c-(float)b[0]),Mathf.Abs(p.x*c-p.z*s-(float)b[1]),Mathf.Abs(p.y-(float)b[2]));}native.Dispose();
   if(d.subMeshCount!=((JArray)before["submeshes"]).Count)indicesSame=false;
   for(int k=0;k<d.subMeshCount && indicesSame;k++){var sub=d.GetSubMesh(k);var previous=(JArray)before["submeshes"][k];if(sub.indexCount!=previous.Count){indicesSame=false;break;}var ids=new Unity.Collections.NativeArray<int>(sub.indexCount,Unity.Collections.Allocator.Temp);d.GetIndices(ids,k);for(int j=0;j<ids.Length;j++)if(ids[j]!=(int)previous[j]){indicesSame=false;break;}ids.Dispose();}
  }
  var result=new{schema="chooguard.original-source-preservation.v1",vertexCount=mesh.vertexCount,maxWorldUVYVertexDelta=maxDelta,allSubmeshIndicesIdentical=indicesSame,preserved=maxDelta<.00001f&&indicesSame,sourcePath=(string)before["sourcePath"],sceneSaved=false,sceneViewChanged=false,playModeChanged=false};
  File.WriteAllText(dir+"source-preservation.json",JsonConvert.SerializeObject(result,Formatting.Indented));
  if(!result.preserved)throw new InvalidOperationException("Original source geometry differs from initial export");
  Debug.Log("MAIN_SOURCE_PRESERVED "+mesh.vertexCount+" vertices and every submesh index unchanged");
 }
}
