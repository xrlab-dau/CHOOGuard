using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public static class ReadCurrentStoreSigns {
 const string R="/Users/um-yunsang/CHOOGuard";
 static string PathOf(Transform t){return t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;}
 static JArray V(Vector3 v){return new JArray(v.x,v.y,v.z);}
 public static string Run(){
  var a=JObject.Parse(File.ReadAllText(R+"/.state/WRITER-AUTHORITY.json"));var g=JObject.Parse(File.ReadAllText(R+"/"+(string)a["delegated_author_lease"]));
  if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["scope"]!="C180_STORE_SIGNS_DIRECT_GRAPHICS_READ"||(string)a["sole_writer_thread"]!="01a13cb4-b738-7823-bf16-30a39ea3d8c6"||(string)g["grant_nonce"]!=(string)a["scene_worker_grant_nonce"])throw new Exception("Exact active own read grant required");
  var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stopped editing FpsStation required");
  var texts=new JArray();var signs=new JArray();
  foreach(var t in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true))){
   if(!t.gameObject.activeInHierarchy)continue;string path=PathOf(t);if(!(path.Contains("/1F/")||path.Contains("/2F/")))continue;
   bool shop=Regex.IsMatch(path,"SHOP|R25|STORE|전문점",RegexOptions.IgnoreCase);
   bool sign=Regex.IsMatch(path,"sign|fascia|logo|brand|letter|간판|상호",RegexOptions.IgnoreCase);
   if(!shop||!sign)continue;
   foreach(var c in t.GetComponents<Component>()){
    if(c==null)continue;string text=null;
    if(c is TextMesh)text=((TextMesh)c).text;
    else if(c is MonoBehaviour){var so=new SerializedObject(c);var prop=so.FindProperty("m_text");if(prop!=null&&prop.propertyType==SerializedPropertyType.String)text=prop.stringValue;}
    if(String.IsNullOrEmpty(text))continue;
    var j=new JObject{["id"]=GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).ToString(),["path"]=path,["name"]=t.name,["text"]=text,["type"]=c.GetType().FullName,["position"]=V(t.position),["euler"]=V(t.eulerAngles),["local_scale"]=V(t.localScale),["lossy_scale"]=V(t.lossyScale),["parent_id"]=GlobalObjectId.GetGlobalObjectIdSlow(t.parent.gameObject).ToString()};
    var rr=t.GetComponent<Renderer>();if(rr!=null){j["bounds_center"]=V(rr.bounds.center);j["bounds_size"]=V(rr.bounds.size);}
    var rt=t as RectTransform;if(rt!=null){j["rect_size"]=new JArray(rt.rect.width,rt.rect.height);}
    texts.Add(j);
   }
   if(Regex.IsMatch(t.name,"FASCIA|SIGNBOARD|LOGO|BRAND",RegexOptions.IgnoreCase)){
    var j=new JObject{["id"]=GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).ToString(),["path"]=path,["name"]=t.name,["position"]=V(t.position),["euler"]=V(t.eulerAngles),["local_scale"]=V(t.localScale)};
    var r=t.GetComponent<Renderer>();if(r!=null){j["bounds_center"]=V(r.bounds.center);j["bounds_size"]=V(r.bounds.size);j["materials"]=new JArray(r.sharedMaterials.Select(x=>AssetDatabase.GetAssetPath(x)));}
    var mf=t.GetComponent<MeshFilter>();if(mf!=null&&mf.sharedMesh!=null){j["mesh_asset"]=AssetDatabase.GetAssetPath(mf.sharedMesh);j["mesh_name"]=mf.sharedMesh.name;}
    signs.Add(j);
   }
  }
  var result=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["texts"]=texts,["signs"]=signs,["construction_inventory_only"]=true};
  File.WriteAllText(R+"/.state/ACTIVE-1F-STORE-SIGN-GRAPHICS-20261019/CURRENT-LIVE-SIGN-CONSTRUCTION-STATE.json",result.ToString(Formatting.Indented));
  return new JObject{["text_candidates"]=texts.Count,["sign_objects"]=signs.Count,["inventory_saved"]=true}.ToString(Formatting.None);
 }
}
