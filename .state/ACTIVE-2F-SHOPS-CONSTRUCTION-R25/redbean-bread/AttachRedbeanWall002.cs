using System;using System.IO;using System.Linq;using System.Security.Cryptography;
using UnityEngine;using UnityEngine.SceneManagement;using UnityEditor;using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;

public static class AttachRedbeanWall002
{
 const string Root="/Users/um-yunsang/CHOOGuard";
 const string Thread="classic:/root/r25_redbean_bread",Scope="C180_R25_REDBEAN_BREAD_ATTACH_WALL_002";
 const string Basis=".state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/redbean-bread/MODEL-PLACEMENT-DELTA-WALL-CONTACT-002.json";
 const string Output=".state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/redbean-bread/APPLIED-WALL-CONTACT-002.json";
 const string OwnPath="FPSWorld/Architecture/2F/MAIN2F_R25_SOURCE_BOUND_SPECIALIST_SHOPS/R25_redbean-bread_SOURCE_BOUND_MODEL";
 static string Sha(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 static JArray V(Vector3 v){return new JArray(v.x,v.y,v.z);}
 static void Guard(){var a=JObject.Parse(File.ReadAllText(Root+"/.state/WRITER-AUTHORITY.json"));
  if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["sole_writer_thread"]!=Thread||(string)a["scope"]!=Scope)throw new Exception("ExactOwnWallContact002LeaseRequired");
  var g=JObject.Parse(File.ReadAllText(Root+"/"+(string)a["delegated_author_lease"]));if((string)g["grant_nonce"]!=(string)a["scene_worker_grant_nonce"])throw new Exception("ExactFIFOGrantNonceRequired");}
 static float RearPlane(Transform own,Vector3 origin,Vector3 normal){
  var rear=own.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="SourceRearDarkPartition");
  var mf=rear.GetComponent<MeshFilter>();if(mf==null||mf.sharedMesh==null)throw new Exception("ExistingOwnRearPhysicalFaceRequired");
  var b=mf.sharedMesh.bounds;float min=float.PositiveInfinity;
  for(int i=0;i<8;i++){var p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));min=Mathf.Min(min,Vector3.Dot(rear.TransformPoint(p)-origin,normal));}
  return min;
 }
 public static string Apply(){Guard();if(File.Exists(Root+"/"+Output))throw new Exception("OwnWallContact002AlreadyAppliedNoReplay");
  var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("StoppedFpsStationRequired");
  var basis=JObject.Parse(File.ReadAllText(Root+"/"+Basis));var own=GameObject.Find(OwnPath);if(own==null)throw new Exception("OwnSavedRedbeanRootRequired");
  var q=basis["base_q"];var u=basis["u"];
  Vector3 origin=new Vector3(-.34290633335f*(float)q[0]+.93936960061f*(float)q[1],0,-.93936960061f*(float)q[0]-.34290633335f*(float)q[1]);
  Vector3 normal=new Vector3(-.34290633335f*(float)u[0]+.93936960061f*(float)u[1],0,-.93936960061f*(float)u[0]-.34290633335f*(float)u[1]).normalized;
  float beforeRear=RearPlane(own.transform,origin,normal),target=(float)basis["target_rear_contact_u"];
  float deltaNormal=target-beforeRear;if(deltaNormal>-.01f||deltaNormal< -8f)throw new Exception("UnexpectedOwnRearPositionNoBlindReplay");
  Vector3 delta=normal*deltaNormal,oldPosition=own.transform.position;Quaternion oldRotation=own.transform.rotation;Vector3 oldScale=own.transform.localScale;
  string beforeSha=Sha(Root+"/"+scene.path);Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Redbean wholebooth nativewallcontact002");
  bool saved=false;
  try{Guard();Undo.RecordObject(own.transform,"Rigid wholebooth rearwallcontact002");own.transform.position=oldPosition+delta;
   // Exactly one root translation: all existing sign, furniture, product and child transforms stay relative to their root.
   EditorSceneManager.MarkSceneDirty(scene);Guard();if(!EditorSceneManager.SaveScene(scene))throw new Exception("OwnWallContact002SaveFailed");saved=true;Undo.CollapseUndoOperations(undo);SceneView.RepaintAll();
   var receipt=new JObject{["status"]="OWN_WHOLE_BOOTH_ATTACHED_TO_NATIVE_REAR_FACADE_AND_SAVED",["utc"]=DateTime.UtcNow.ToString("o"),
    ["store_key"]="redbean-bread",["scope"]=Scope,["author"]=Thread,["root_path"]=OwnPath,["root_object_id"]=GlobalObjectId.GetGlobalObjectIdSlow(own).ToString(),
    ["placement_basis"]=Basis,["placement_basis_sha256"]=Sha(Root+"/"+Basis),["before_world_position"]=V(oldPosition),["after_world_position"]=V(own.transform.position),
    ["translation_world_m"]=V(delta),["translation_normal_m"]=deltaNormal,["normal_world"]=V(normal),["rear_contact_u_before_m"]=beforeRear,
    ["target_rear_contact_u_m"]=target,["actual_new_rear_contact_u_m"]=RearPlane(own.transform,origin,normal),
    ["native_rear_vertical_face_u_range"]=basis["vertical_face_native_u_range"].DeepClone(),["native_wall_contact_source"]=basis["native_contact_source"],
    ["rotation_unchanged"]=true,["scale_unchanged"]=true,["child_relative_TRS_unchanged"]=true,["objects_added_removed_or_replaced"]=0,
    ["assets_changed"]=false,["native_or_floor_geometry_changed"]=false,["no_inferred_new_door_aperture"]=true,
    ["native_external_portal_profile_preserved"]=basis["native_external_portal_sections"].DeepClone(),
    ["native_external_portal_selected_u_max"]=basis["native_external_portal_selected_u_max"],["Hawaiian_separate_native_entry_v"]=new JArray(8.0482,9.2982),
    ["own_v_limits_unchanged"]=basis["own_v_limits_unchanged"].DeepClone(),["all_other_native_objects_and_stores_untouched"]=true,
    ["scene_sha256_before"]=beforeSha,["scene_sha256_after"]=Sha(Root+"/"+scene.path),["QA_Play_tests_probes_reopen_capture"]=false,["validation_owner"]="user"};
   File.WriteAllText(Root+"/"+Output,receipt.ToString());return new JObject{["status"]=receipt["status"],["receipt"]=Output,["delta_normal_m"]=deltaNormal,["actual_new_rear_contact_u_m"]=receipt["actual_new_rear_contact_u_m"]}.ToString();
  }catch{if(!saved)Undo.RevertAllDownToGroup(undo);throw;}
 }
}
