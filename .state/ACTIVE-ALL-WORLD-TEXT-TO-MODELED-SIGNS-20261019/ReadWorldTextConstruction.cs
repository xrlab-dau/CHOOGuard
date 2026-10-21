using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using TMPro;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public static class ReadWorldTextConstruction {
 const string R="/Users/um-yunsang/CHOOGuard",D=".state/ACTIVE-ALL-WORLD-TEXT-TO-MODELED-SIGNS-20261019";
 static string PathOf(Transform t){return t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;}
 static JArray V(Vector3 v){return new JArray(v.x,v.y,v.z);}
 static string Id(UnityEngine.Object o){return GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();}
 static JObject Pose(Transform t){return new JObject{["id"]=Id(t.gameObject),["name"]=t.name,["path"]=PathOf(t),["parent_id"]=t.parent==null?null:Id(t.parent.gameObject),["active_self"]=t.gameObject.activeSelf,["active_in_hierarchy"]=t.gameObject.activeInHierarchy,["position"]=V(t.position),["euler"]=V(t.eulerAngles),["local_scale"]=V(t.localScale),["lossy_scale"]=V(t.lossyScale),["children"]=t.childCount};}
 public static string Read(){
  var a=JObject.Parse(File.ReadAllText(R+"/.state/WRITER-AUTHORITY.json"));var g=JObject.Parse(File.ReadAllText(R+"/"+(string)a["delegated_author_lease"]));
  if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["sole_writer_thread"]!="01a13cb4-b738-7823-bf16-30a39ea3d8c6"||(string)a["scope"]!="C180_WORLD_SIGN_GEOMETRY_READ"||(string)g["grant_nonce"]!=(string)a["scene_worker_grant_nonce"])throw new Exception("Exact current C180 read grant required");
  var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stopped editing FpsStation required");
  var texts=new JArray();var prints=new JArray();var screenUI=new JArray();
  foreach(var t in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true))){
   var tmp=t.GetComponent<TMP_Text>();var tm=t.GetComponent<TextMesh>();var ui=t.GetComponent<UnityEngine.UI.Text>();
   if(tmp!=null||tm!=null||ui!=null){
    var e=Pose(t);var canvas=t.GetComponentInParent<Canvas>(true);bool screen=canvas!=null&&canvas.renderMode!=RenderMode.WorldSpace;
    e["type"]=tmp!=null?tmp.GetType().FullName:tm!=null?"UnityEngine.TextMesh":"UnityEngine.UI.Text";e["text"]=tmp!=null?tmp.text:tm!=null?tm.text:ui.text;e["component_id"]=Id(tmp!=null?(UnityEngine.Object)tmp:tm!=null?(UnityEngine.Object)tm:ui);e["screen_ui"]=screen;
    var r=t.GetComponent<Renderer>();if(r!=null){e["bounds_center"]=V(r.bounds.center);e["bounds_size"]=V(r.bounds.size);}
    if(tmp!=null){e["font_asset"]=AssetDatabase.GetAssetPath(tmp.font);e["source_font"]=tmp.font==null?null:AssetDatabase.GetAssetPath(tmp.font.sourceFontFile);e["color"]="#"+ColorUtility.ToHtmlStringRGBA(tmp.color);e["font_size"]=tmp.fontSize;e["alignment"]=tmp.alignment.ToString();e["text_bounds_center"]=V(tmp.textBounds.center);e["text_bounds_size"]=V(tmp.textBounds.size);}
    if(tm!=null)e["color"]="#"+ColorUtility.ToHtmlStringRGBA(tm.color);
    if(screen)screenUI.Add(e);else texts.Add(e);
   }
   if(!t.gameObject.activeInHierarchy)continue;var renderer=t.GetComponent<MeshRenderer>();if(renderer==null)continue;string path=PathOf(t);
   foreach(var m in renderer.sharedMaterials){if(m==null)continue;Texture tex=m.HasProperty("_BaseMap")?m.GetTexture("_BaseMap"):m.mainTexture;if(tex==null)continue;
    string texture=AssetDatabase.GetAssetPath(tex),material=AssetDatabase.GetAssetPath(m);
    bool relevant=Regex.IsMatch(path+" "+texture+" "+material,"Sign|Notice|Menu|Letter|Fascia|Logo|Graphic|Placard|Header|Wayfind|안내|간판|표지|광고|문자|Actual2FPhysicalSigns|ACTUAL_SOURCE_PHYSICAL",RegexOptions.IgnoreCase);
    if(!relevant||!(path.StartsWith("FPSWorld/")||path.StartsWith("WORLD/")))continue;
    var e=Pose(t);e["material"]=material;e["texture"]=texture;e["texture_size"]=new JArray(tex.width,tex.height);e["bounds_center"]=V(renderer.bounds.center);e["bounds_size"]=V(renderer.bounds.size);var f=t.GetComponent<MeshFilter>();if(f!=null&&f.sharedMesh!=null){e["mesh_asset"]=AssetDatabase.GetAssetPath(f.sharedMesh);e["mesh_name"]=f.sharedMesh.name;e["mesh_bounds_center"]=V(f.sharedMesh.bounds.center);e["mesh_bounds_size"]=V(f.sharedMesh.bounds.size);if(f.sharedMesh.vertexCount<=256&&f.sharedMesh.isReadable){e["mesh_vertices"]=new JArray(f.sharedMesh.vertices.Select(x=>(JToken)V(x)));e["mesh_uv"]=new JArray(f.sharedMesh.uv.Select(x=>(JToken)new JArray(x.x,x.y)));e["mesh_triangles"]=new JArray(f.sharedMesh.triangles);e["mesh_normals"]=new JArray(f.sharedMesh.normals.Select(x=>(JToken)V(x)));}}
    prints.Add(e);
   }
  }
  var result=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["scene"]=scene.path,["scene_dirty"]=scene.isDirty,["world_texts"]=texts,["raster_sign_candidates"]=prints,["screen_ui"]=screenUI,["purpose"]="CURRENT_CONSTRUCTION_INPUT_NOT_QA"};
  File.WriteAllText(R+"/"+D+"/CURRENT-WORLD-TEXT-AND-SIGN-INPUT.json",result.ToString(Formatting.Indented));
  return new JObject{["world_text_components"]=texts.Count,["active_world_text_components"]=texts.Count(x=>(bool)x["active_in_hierarchy"]),["raster_sign_candidates"]=prints.Count,["screen_ui_components"]=screenUI.Count}.ToString(Formatting.None);
 }
}
