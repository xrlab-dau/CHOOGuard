using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;
using UnityEngine;using UnityEngine.SceneManagement;using UnityEditor;using UnityEditor.SceneManagement;using Newtonsoft.Json.Linq;

public static class ArtboxSourceSlotBindR25
{
 const string Root="/Users/um-yunsang/CHOOGuard";
 const string Scope="C180_R25_ARTBOX_SOURCE_MATERIAL_BIND_002_R1",Thread="classic:/root/r25_artbox";
 const string Basis=".state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/artbox/SOURCE-MATERIAL-BINDINGS-R25-002.json";
 const string ExpectedSHA="9483c367c9ed0de00c936ab6effe9379785bfc1a11b2f4b790a3d561a82a1a2d";
 const string OwnAssets="Assets/ChooGuard/Art/StationInterior/R25_Shops/artbox/";
 static string Sha(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 static void Guard(){var a=JObject.Parse(File.ReadAllText(Root+"/.state/WRITER-AUTHORITY.json"));
  if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["sole_writer_thread"]!=Thread||(string)a["scope"]!=Scope)throw new Exception("Exact Artbox source-slot C180 lease required");
  var g=JObject.Parse(File.ReadAllText(Root+"/"+(string)a["delegated_author_lease"]));if((string)g["grant_nonce"]!=(string)a["scene_worker_grant_nonce"])throw new Exception("Exact C180 nonce required");}
 static string CleanId(string s){return new string(s.Select(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'?c:'_').ToArray());}
 static string Canon(string s){return new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());}
 public static string Apply(){Guard();if(Sha(Root+"/"+Basis)!=ExpectedSHA)throw new Exception("Frozen exact source binding mismatch");
  var p=JObject.Parse(File.ReadAllText(Root+"/"+Basis));var scene=SceneManager.GetActiveScene();
  if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stopped FpsStation required");
  string packet=(string)p["frozen_construction_packet"];if(Sha(Path.IsPathRooted(packet)?packet:Root+"/"+packet)!=(string)p["frozen_construction_packet_sha256"])throw new Exception("Original authored packet changed");
  string ownRoot=(string)p["root_path"];var root=GameObject.Find(ownRoot);if(root==null)throw new Exception("Existing exact Artbox root required");
  var targets=new List<Tuple<Renderer,Material[],JObject>>();var assigned=new HashSet<Renderer>();
  foreach(var w in (JArray)p["wrappers"]){
   var tr=root.transform.Find(CleanId((string)w["wrapper_id"]));
   if(tr==null){tr=root.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name==CleanId((string)w["wrapper_id"]));}
   if(tr==null||!tr.IsChildOf(root.transform))throw new Exception("Exact own authored wrapper missing:"+(string)w["wrapper_id"]);
   var renderers=tr.GetComponentsInChildren<Renderer>(true);
   if(w["intentional_photo_color_override"]!=null&&w["intentional_photo_color_override"].Type!=JTokenType.Null){
    // The frozen packet intentionally gives every original slot this one photo
    // colour. Directly bind that authored override even for encoded FBX names.
    string path=OwnAssets+"MAT_"+(string)w["intentional_photo_color_override"]+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null)throw new Exception("Exact own photo colour material missing");
    foreach(var renderer in renderers){if(!assigned.Add(renderer))throw new Exception("Duplicate own override renderer");targets.Add(Tuple.Create(renderer,new[]{material},new JObject{["wrapper_id"]=(string)w["wrapper_id"],["source_renderer"]=renderer.name,["original_source_slots"]="Every original source renderer has one authored material slot",["intentional_frozen_packet_override"]=(string)w["intentional_photo_color_override"],["assigned_material_assets"]=new JArray(path)}));}
    continue;
   }
   foreach(var b in (JArray)w["renderer_bindings"]){string sourceName=(string)b["source_renderer"];
    var hits=renderers.Where(r=>r.name==sourceName).ToArray();
    if(hits.Length==0){string c=Canon(sourceName);hits=renderers.Where(r=>Canon(r.name)==c).ToArray();}
    if(hits.Length!=1)throw new Exception("Unambiguous original renderer identity required:"+tr.name+"/"+sourceName);
    var renderer=hits[0];if(!assigned.Add(renderer))throw new Exception("Duplicate authored renderer assignment:"+tr.name+"/"+sourceName);
    var materials=((JArray)b["target_material_assets"]).Select(x=>{string path=(string)x;if(!path.StartsWith(OwnAssets))throw new Exception("Own material boundary required");var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null)throw new Exception("Authored own material missing:"+path);return m;}).ToArray();
    targets.Add(Tuple.Create(renderer,materials,new JObject{["wrapper_id"]=(string)w["wrapper_id"],["source_renderer"]=sourceName,["source_material_slots"]=b["source_material_slots"].DeepClone(),["assigned_material_assets"]=b["target_material_assets"].DeepClone()}));
   }
  }
  string before=Sha(Root+"/"+scene.path);Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Artbox exact original source material slot binding R25-002");bool saved=false;
  try{Guard();foreach(var t in targets){Undo.RecordObject(t.Item1,"Artbox source slot binding");t.Item1.sharedMaterials=t.Item2;EditorUtility.SetDirty(t.Item1);}EditorSceneManager.MarkSceneDirty(scene);Guard();if(!EditorSceneManager.SaveScene(scene))throw new Exception("Artbox source slot save failed");saved=true;Undo.CollapseUndoOperations(undo);
   var receipt=new JObject{["status"]="EXACT_OWN_SOURCE_RENDERER_MATERIAL_SLOTS_BOUND_AND_SAVED",["store_key"]="artbox",["utc"]=DateTime.UtcNow.ToString("o"),["scope"]=Scope,["author"]=Thread,["root_path"]=ownRoot,["basis"]=Basis,["basis_sha256"]=ExpectedSHA,["wrapper_count"]=((JArray)p["wrappers"]).Count,["renderer_count"]=targets.Count,["material_slot_count"]=targets.Sum(t=>t.Item2.Length),["assignments"]=new JArray(targets.Select(t=>t.Item3)),["scene_sha256_before"]=before,["scene_sha256_after"]=Sha(Root+"/"+scene.path),["source_material_name_fallback_used"]=false,["original_source_provenance_direct_binding"]=true,["geometry_placement_mesh_native_changes"]=false,["Assets_import_or_reimport"]=false,["QA_Play_tests_probes_reopen_capture"]=false};
   string output=".state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/artbox/APPLIED-OWN-SOURCE-MATERIAL-BIND-R25-002.json";File.WriteAllText(Root+"/"+output,receipt.ToString());return new JObject{["status"]=receipt["status"],["renderer_count"]=targets.Count,["receipt"]=output}.ToString();
  }catch{if(!saved)Undo.RevertAllDownToGroup(undo);throw;}
 }
}
