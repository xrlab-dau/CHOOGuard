using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;

public static class ApplyBoksoondogaServiceFinishR18 {
 const string R="/Users/um-yunsang/CHOOGuard",Thread="01a14ee0-a8f0-76c3-90fd-ec5483106da9",Scope="C180_2F_PHOTO_LOCATION_R15_BOKSOONDOGA_SERVICE_PUBLIC_FINISH_R18_APPLY_SAVE";
 const string Payload=".state/ACTIVE-ALL2F-PHOTO-TWIN-R18/geometry/BOKSOONDOGA-SERVICE-WALL-FINISH-PAYLOAD-R18.json",Hash="8c3e3b3535ddc7d3bf7c9cd36bfa7168f09aa1b9e6e98dc79dfedec141c080df",Receipt=".state/ACTIVE-ALL2F-PHOTO-TWIN-R18/receipts/APPLIED-BOKSOONDOGA-SERVICE-PUBLIC-FINISH-R18.json";
 static string Sha(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 static string ID(UnityEngine.Object o){return GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();}
 static void Guard(){
  var a=JObject.Parse(File.ReadAllText(R+"/.state/WRITER-AUTHORITY.json"));
  if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["sole_writer_thread"]!=Thread||(string)a["scope"]!=Scope)throw new Exception("Exact scoped2Ffinishauthor required");
  var g=JObject.Parse(File.ReadAllText(R+"/"+(string)a["delegated_author_lease"]));
  if((string)g["worker_thread"]!=Thread||(string)g["scope"]!=Scope||(string)g["grant_nonce"]!=(string)a["scene_worker_grant_nonce"])throw new Exception("Exact scoped nonce required");
 }
 public static string Apply(){
  Guard();var scene=SceneManager.GetActiveScene();
  if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stopped FpsStation required");
  if(Sha(R+"/"+Payload)!=Hash||File.Exists(R+"/"+Receipt))throw new Exception("Frozen payload changed or replay");
  var p=JObject.Parse(File.ReadAllText(R+"/"+Payload));
  foreach(JObject f in p["frozen_inputs"])if(Sha(R+"/"+(string)f["path"])!=(string)f["sha256"])throw new Exception("Frozen construction/CAD/source changed: "+(string)f["path"]);
  var row=(JObject)p["target"];string path=(string)row["path"];
  if(!path.StartsWith("FPSWorld/Architecture/2F/MAIN2F_MARKED_FROM_ACTUAL_LOWER_WALLS_R5_",StringComparison.Ordinal))throw new Exception("Only own currentR5 upper wall");
  var go=GameObject.Find(path);if(go==null||ID(go)!=(string)row["object_id"])throw new Exception("Exact target missing");
  var mf=go.GetComponent<MeshFilter>();var rr=go.GetComponent<MeshRenderer>();
  if(mf==null||rr==null||!rr.enabled||ID(mf.sharedMesh)!=(string)row["mesh_object_id"]||AssetDatabase.GetAssetPath(mf.sharedMesh)!=(string)row["mesh_asset"])throw new Exception("Current wall mesh changed");
  for(int i=0;i<16;i++)if(Math.Abs(go.transform.localToWorldMatrix[i]-(double)row["world_matrix"][i])>.00005)throw new Exception("Current wall transform changed");
  if(!rr.sharedMaterials.Select(AssetDatabase.GetAssetPath).SequenceEqual(row["material_assets"].Select(x=>(string)x)))throw new Exception("Current materials changed");
  var old=mf.sharedMesh;var oldm=rr.sharedMaterials;
  if(old.subMeshCount!=6||oldm.Length!=6)throw new Exception("Exact six-source-submesh layout required");
  foreach(JObject t in p["selected_triangles"]){int sub=(int)t["submesh"],i=(int)t["triangle_index"];var ts=old.GetTriangles(sub);if(sub!=1||i<42||i>62||i*3+2>=ts.Length||!ts.Skip(i*3).Take(3).SequenceEqual(t["vertex_indices"].Select(x=>(int)x)))throw new Exception("Exact21public triangles changed");}
  string asset=(string)p["new_mesh_asset"],matasset=(string)p["existing_finish_material"];
  if(File.Exists(R+"/"+asset))throw new Exception("Own derivative already exists");
  var mat=AssetDatabase.LoadAssetAtPath<Material>(matasset);if(mat==null)throw new Exception("Qualified existing bluegray finish absent");
  bool saved=false,wasDirty=scene.isDirty;string before=Sha(R+"/"+scene.path);
  Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Source and vectorCAD bound Boksoondoga public wall finish");
  try{
   Guard();var mesh=UnityEngine.Object.Instantiate(old);mesh.name=Path.GetFileNameWithoutExtension(asset);mesh.subMeshCount=old.subMeshCount+1;
   var selected=new List<int>();
   for(int sub=0;sub<old.subMeshCount;sub++){
    var ts=old.GetTriangles(sub);var take=new HashSet<int>(p["selected_triangles"].Where(x=>(int)x["submesh"]==sub).Select(x=>(int)x["triangle_index"]));var keep=new List<int>();
    for(int i=0;i<ts.Length;i+=3){var dest=take.Contains(i/3)?selected:keep;dest.Add(ts[i]);dest.Add(ts[i+1]);dest.Add(ts[i+2]);}
    mesh.SetTriangles(keep,sub,false);
   }
   if(selected.Count!=63)throw new Exception("Only exact21public triangles permitted");
   mesh.SetTriangles(selected,old.subMeshCount,false);AssetDatabase.CreateAsset(mesh,asset);
   Undo.RecordObject(mf,"Independent public-finish derivative");Undo.RecordObject(rr,"Reuse separately observed bluegray finish");mf.sharedMesh=mesh;rr.sharedMaterials=oldm.Concat(new[]{mat}).ToArray();
   EditorUtility.SetDirty(mf);EditorUtility.SetDirty(rr);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Guard();
   if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scoped finish save failed");saved=true;Undo.CollapseUndoOperations(group);SceneView.RepaintAll();
  }catch{
   if(!saved){Undo.RevertAllDownToGroup(group);if(File.Exists(R+"/"+asset))AssetDatabase.DeleteAsset(asset);}throw;
  }
  var result=new JObject{["status"]="APPLIED_SAVED_BOKSOONDOGA_SERVICE_PUBLIC_BLUEGRAY_FINISH_R18",["utc"]=DateTime.UtcNow.ToString("o"),["scene"]=scene.path,["scene_saved"]=true,["scene_was_dirty"]=wasDirty,["scene_sha256_before"]=before,["scene_sha256"]=Sha(R+"/"+scene.path),["object_id"]=ID(go),["path"]=path,["prior_mesh_asset"]=row["mesh_asset"].DeepClone(),["mesh_asset"]=asset,["mesh_object_id"]=ID(mf.sharedMesh),["material_asset"]=matasset,["selected_public_triangles"]=21,["source_packet"]=p["source_packet"].DeepClone(),["current_vector_CAD_correspondence"]=p["current_vector_CAD_correspondence"].DeepClone(),["source_triple_comparison_shared_base_dependency_disclosed"]=true,["all_original_positions_UVs_normals_TRS_unselected_triangles_joints_toestrip_colliders_preserved"]=true,["geometry_change"]=false,["doors_firecabinets_frames_and_other_new_features"]=false,["native_1F_WCs_columns_floorR17_R18rails_Flowersfinish_and_all_other_walls_preserved"]=true,["all2F_complete"]=false,["fidelity_measured_percent"]=JValue.CreateNull(),["QA_tests_probes_reopen_captures"]=false,["validation_owner"]="user"};
  Guard();File.WriteAllText(R+"/"+Receipt,result.ToString());File.WriteAllText(R+"/.state/CURRENT-2F-PHOTO-LOCATION-DIGITAL-TWIN-R18.json",result.ToString());
  var authority=JObject.Parse(File.ReadAllText(R+"/.state/WRITER-AUTHORITY.json"));authority["latest_scene_sha256"]=result["scene_sha256"].DeepClone();authority["latest_receipt"]=Receipt;authority["latest_applied_scope"]=Scope;authority["current_2f_vector_cad_scene_delta"]=Receipt;authority["current_2f_vector_cad_geometry_changed_by_latest_delta"]=false;File.WriteAllText(R+"/.state/WRITER-AUTHORITY.json",authority.ToString());
  return new JObject{["status"]=result["status"],["scene_saved"]=true,["own_wall_changed"]=1,["geometry_changed"]=false,["receipt"]=Receipt}.ToString();
 }
}
