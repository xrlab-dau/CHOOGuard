using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;using Newtonsoft.Json.Linq;
public static class MountSolidSigns007 {
 const string Root="/Users/um-yunsang/CHOOGuard",Thread="classic:/root/r25_east_storyway",Scope="C180_R25_STORYWAY_EAST_SOLID_MOUNT_007",Own="FPSWorld/Architecture/2F/MAIN2F_R25_SOURCE_BOUND_SPECIALIST_SHOPS/R25_storyway-east_SOURCE_BOUND_MODEL",Signs="ACTUAL_WEB_FREE_MODELS_AND_SOLID_SIGNS_006/SOLID_PHOTO_TRACED_SIGNS/";
 static void Guard(){var a=JObject.Parse(File.ReadAllText(Root+"/.state/WRITER-AUTHORITY.json"));if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["sole_writer_thread"]!=Thread||(string)a["scope"]!=Scope)throw new Exception("Exactownmountauthorrequired");var g=JObject.Parse(File.ReadAllText(Root+"/"+(string)a["delegated_author_lease"]));if((string)g["grant_nonce"]!=(string)a["scene_worker_grant_nonce"])throw new Exception("Exactnonce required");}
 static void Depth(Transform root,string name,float depth,JArray changed){var t=root.Find(name);if(t==null)throw new Exception("Ownmountpartrequired:"+name);Undo.RecordObject(t,"Physicalsignmountauthoring");var p=t.localPosition;p.z=-depth;t.localPosition=p;EditorUtility.SetDirty(t);changed.Add(new JObject{["part"]=name,["inward_depth_m"]=depth});}
 public static string Apply(){Guard();var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("StoppedFpsStationrequired");var root=GameObject.Find(Own);if(root==null)throw new Exception("Exactownrootrequired");Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();bool saved=false;try{var changed=new JArray();
  // Derived from authored solids: fascia19cm, glyph35mm, hemisphere radius32.5mm.
  // Source glyph fronts remain +Z. Place backs on source fascia, within approved shop front.
  Depth(root.transform,"Front_Fascia/navy_fascia_main",.15f,changed);
  Depth(root.transform,Signs+"PHOTO_TRACED_MAIN_WHITE_STORYWAY",.020f,changed);
  Depth(root.transform,Signs+"CONTINUOUS_SOLID_SMILE_MAIN",.020f,changed);
  for(int i=0;i<5;i++)Depth(root.transform,Signs+"HEMISPHERE_DOT_MAIN_"+i,.055f,changed);
  Depth(root.transform,Signs+"PHOTO_TRACED_DOOR_ORANGE_STORYWAY",.129f,changed);
  Depth(root.transform,Signs+"CONTINUOUS_SOLID_SMILE_DOOR",.129f,changed);
  for(int i=0;i<5;i++)Depth(root.transform,Signs+"HEMISPHERE_DOT_DOOR_"+i,.133f,changed);
  Depth(root.transform,Signs+"SOLID_FAST_FOOD_CATEGORY_LETTERS",2.692f,changed);
  Guard();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Ownsolidmountsavefailed");saved=true;Undo.CollapseUndoOperations(undo);SceneView.RepaintAll();var r=new JObject{["status"]="OWN_SOLID_SIGNS_ATTACHED_TO_PHYSICAL_FASCIA_AND_SAVED",["utc"]=DateTime.UtcNow.ToString("o"),["scope"]=Scope,["changed"]=changed,["outward_normal_local"]=new JArray(0,0,1),["main_white_letter_depth_m"]=.035,["main_fascia_front_inward_depth_m"]=.055,["main_letters_front_inward_depth_m"]=.020,["ribs_visible_in_front_of_flat_fascia"]=true,["door_letters_back_mount_inward_depth_m"]=.133,["category_letters_back_mount_inward_depth_m"]=2.70,["native_floor_approved_front_boundary_otherstores_unchanged"]=true,["SceneQA_Play_tests_reopen_capture"]=false};File.WriteAllText(Root+"/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/storyway-east/APPLIED-SOLID-SIGN-MOUNTS007.json",r.ToString());return r.ToString();
  }catch{if(!saved)Undo.RevertAllDownToGroup(undo);throw;}
 }
}
