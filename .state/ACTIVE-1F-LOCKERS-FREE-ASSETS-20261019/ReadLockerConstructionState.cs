using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public static class ReadLockerConstructionState {
 const string R="/Users/um-yunsang/CHOOGuard";
 public static string Run(){
  var a=JObject.Parse(File.ReadAllText(R+"/.state/WRITER-AUTHORITY.json"));
  if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["sole_writer_thread"]!="01a13cb4-b738-7823-bf16-30a39ea3d8c6"||(string)a["scope"]!="C180_LOCKER_1F_FREE_ASSET_READ")throw new Exception("Exact own scope required");
  var g=JObject.Parse(File.ReadAllText(R+"/"+(string)a["delegated_author_lease"]));
  if((string)g["grant_nonce"]!=(string)a["scene_worker_grant_nonce"])throw new Exception("Grant mismatch");
  var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stopped editing FpsStation required");
  var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
  var facility=all.SingleOrDefault(x=>x.name=="LOCKER_FACILITY"&&x.parent!=null&&x.parent.name=="MAIN1F_USER_COLUMNS_WALLS_LOCKERS_C163");
  if(facility==null)throw new Exception("Exact current 1F locker facility missing");
  Func<Transform,string> path=null;path=t=>t.parent==null?t.name:path(t.parent)+"/"+t.name;
  Func<Vector3,JArray> v=x=>new JArray(x.x,x.y,x.z);
  var parts=new JArray();
  foreach(var t in facility.GetComponentsInChildren<Transform>(true)){
   var r=t.GetComponent<Renderer>();var mf=t.GetComponent<MeshFilter>();var tx=t.GetComponent<TextMesh>();
   var j=new JObject{["id"]=GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).ToString(),["name"]=t.name,["path"]=path(t),["parent_id"]=t.parent==null?null:GlobalObjectId.GetGlobalObjectIdSlow(t.parent.gameObject).ToString(),["active_self"]=t.gameObject.activeSelf,["active_in_hierarchy"]=t.gameObject.activeInHierarchy,["position"]=v(t.position),["local_position"]=v(t.localPosition),["euler"]=v(t.eulerAngles),["local_scale"]=v(t.localScale),["lossy_scale"]=v(t.lossyScale)};
   if(r!=null){j["bounds_center"]=v(r.bounds.center);j["bounds_size"]=v(r.bounds.size);j["materials"]=new JArray(r.sharedMaterials.Select(x=>AssetDatabase.GetAssetPath(x)));}
   if(mf!=null&&mf.sharedMesh!=null){j["mesh_asset"]=AssetDatabase.GetAssetPath(mf.sharedMesh);j["mesh_name"]=mf.sharedMesh.name;j["vertex_count"]=mf.sharedMesh.vertexCount;}
   if(tx!=null)j["text"]=tx.text;
   parts.Add(j);
  }
  var texts=new JArray();foreach(var t in all){var x=t.GetComponent<TextMesh>();if(x!=null&&(x.text.Contains("보관")||x.text.Contains("물품")||x.text.ToLowerInvariant().Contains("locker")||x.text.ToLowerInvariant().Contains("luggage")))texts.Add(new JObject{["path"]=path(t),["id"]=GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).ToString(),["text"]=x.text,["active"]=t.gameObject.activeInHierarchy,["position"]=v(t.position)});}
  var result=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["scene"]=scene.path,["scene_dirty"]=scene.isDirty,["parts"]=parts,["related_texts"]=texts,["read_only_construction_inventory"]=true};
  File.WriteAllText(R+"/.state/ACTIVE-1F-LOCKERS-FREE-ASSETS-20261019/CURRENT-LOCKER-CONSTRUCTION-STATE.json",result.ToString(Formatting.Indented));
  return new JObject{["parts"]=parts.Count,["texts"]=texts,["saved_inventory"]=true}.ToString(Formatting.None);
 }
}
