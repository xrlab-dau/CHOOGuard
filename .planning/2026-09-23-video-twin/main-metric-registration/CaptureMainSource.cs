using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
public static class CaptureMainSource {
 public static void Main() {
  if(EditorApplication.isPlaying) throw new InvalidOperationException("Edit mode required");
  var path=".planning/2026-09-23-video-twin/main-metric-registration/";
  var keep=GameObject.Find("FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_0").GetComponent<Renderer>();
  var disabled=new List<Renderer>(); var go=new GameObject("__MainMetricIsolatedCamera");go.hideFlags=HideFlags.HideAndDontSave;
  RenderTexture rt=null; Texture2D tex=null; var previous=RenderTexture.active;
  try {
   foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)) if(r!=keep && r.enabled){disabled.Add(r);r.enabled=false;}
   float s=Mathf.Sin(16.2f*Mathf.Deg2Rad),c=Mathf.Cos(16.2f*Mathf.Deg2Rad);
   var cam=go.AddComponent<Camera>();cam.enabled=false;cam.transform.position=new Vector3(11*s-55*c,8.6f,11*c+55*s);cam.transform.rotation=Quaternion.LookRotation(new Vector3(c,0,-s),Vector3.up);
   cam.fieldOfView=60;cam.nearClipPlane=.03f;cam.farClipPlane=500;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.15f,.16f,.18f);cam.allowHDR=false;
   rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
   tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(path+"source-vestibule.png",tex.EncodeToPNG());
   var verts=JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(File.ReadAllText(path+"source-mesh.json"))["verticesUVY"];
   var rows=new List<object>();int id=0;
   foreach(var p in verts){float u=(float)p[0],v=(float)p[1],y=(float)p[2];if(u>-12 && u<34 && v>-57 && v<-30 && y>6.8f && y<18){var pix=cam.WorldToScreenPoint(new Vector3(u*s+v*c,y,u*c-v*s));rows.Add(new {vertexIndex=id,uvy=new[]{u,v,y},pixel=new[]{pix.x,720-pix.y,pix.z}});}id++;}
   File.WriteAllText(path+"source-vestibule-projected.json",JsonConvert.SerializeObject(new{positionUVY=new[]{11f,-55f,8.6f},forwardUVY=new[]{0f,1f,0f},width=1280,height=720,verticalFov=60,vertices=rows}));
   cam.targetTexture=null;
  } finally {RenderTexture.active=previous;if(tex!=null)UnityEngine.Object.DestroyImmediate(tex);if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}UnityEngine.Object.DestroyImmediate(go);foreach(var r in disabled)if(r!=null)r.enabled=true;}
  Debug.Log("MAIN_SOURCE_CAPTURED source only, camera destroyed, all renderers restored, scene not saved");
 }
}
