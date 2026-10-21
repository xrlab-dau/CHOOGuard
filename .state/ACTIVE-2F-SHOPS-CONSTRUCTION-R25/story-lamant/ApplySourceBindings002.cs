using System;using System.IO;using System.Linq;using System.Security.Cryptography;
using UnityEngine;using UnityEngine.SceneManagement;using UnityEditor;using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;
public static class StoryLamantSourceBindings002 {
 const string Root="/Users/um-yunsang/CHOOGuard",Thread="classic:/root/r25_story_lamant",Scope="C180_R25_STORY_LAMANT_SOURCE_BINDINGS_002";
 const string Folder="Assets/ChooGuard/Art/StationInterior/R25_Shops/story-lamant";
 static void Guard(){var a=JObject.Parse(File.ReadAllText(Root+"/.state/WRITER-AUTHORITY.json"));if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["sole_writer_thread"]!=Thread||(string)a["scope"]!=Scope)throw new Exception("Exact own C180 author required");var g=JObject.Parse(File.ReadAllText(Root+"/"+(string)a["delegated_author_lease"]));if((string)g["grant_nonce"]!=(string)a["scene_worker_grant_nonce"])throw new Exception("Exact nonce required");}
 static Material Mat(string key){var m=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/MAT_"+key+".mat");if(m==null)throw new Exception("Own material missing:"+key);return m;}
 static string Sha(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 public static string Apply(){Guard();var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stopped FpsStation required");
  var own=GameObject.Find("FPSWorld/Architecture/2F/MAIN2F_R25_SOURCE_BOUND_SPECIALIST_SHOPS/R25_story-lamant_SOURCE_BOUND_MODEL");if(own==null)throw new Exception("Own applied source model required");
  var changes=new JArray();foreach(var wrapper in own.GetComponentsInChildren<Transform>(true)){
   string type=wrapper.name.Contains("PlywoodChromeChair_")||wrapper.name.StartsWith("FrontChair_")?"chair":wrapper.name=="DownloadedLowBlackGlazedSandwichCase"?"case":wrapper.name=="DownloadedPOSTerminal"?"pos":null;if(type==null)continue;
   foreach(var r in wrapper.GetComponentsInChildren<Renderer>(true)){Guard();string key=null;
    if(type=="chair"){if(r.name=="Object_2")key="black";else if(r.name=="Object_3")key="chrome";else if(r.name=="Object_4")key="plywood";}
    if(type=="case"){if(r.name=="Object_2")key="glass";else if(r.name=="Object_3")key="black";else if(r.name=="Object_4")key="stone";}
    if(type=="pos"){if(r.name.Contains("2100454184"))key="pos_texture";else if(r.name.Contains("2100454185"))key="black";}
    if(key==null)continue;Undo.RecordObject(r,"Own source provenance material binding");r.sharedMaterials=r.sharedMaterials.Select(_=>Mat(key)).ToArray();EditorUtility.SetDirty(r);changes.Add(new JObject{["wrapper"]=wrapper.name,["source_renderer"]=r.name,["own_material"]=key});
   }
  }AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Guard();if(!EditorSceneManager.SaveScene(scene))throw new Exception("Own material save failed");
  var receipt=new JObject{["status"]="OWN_SOURCE_RENDERER_MATERIALS_APPLIED_SAVED",["utc"]=DateTime.UtcNow.ToString("o"),["scope"]=Scope,["thread"]=Thread,["source_binding_basis"]="SOURCE-RENDERER-MATERIAL-BINDINGS-R25.json",["renderer_bindings_applied"]=changes,["renderer_binding_count"]=changes.Count,["scene_sha256_after"]=Sha(Root+"/"+scene.path),["QA_Play_tests_reopen_capture"]=false};string output=".state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/story-lamant/APPLIED-SOURCE-MATERIAL-BINDINGS002.json";File.WriteAllText(Root+"/"+output,receipt.ToString());return new JObject{["status"]=receipt["status"],["renderer_binding_count"]=changes.Count,["receipt"]=output}.ToString();
 }
}
