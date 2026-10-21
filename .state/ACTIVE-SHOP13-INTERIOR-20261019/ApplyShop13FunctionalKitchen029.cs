using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class ApplyShop13FunctionalKitchen029 {
 const string R="/Users/um-yunsang/CHOOGuard";
 const string D=".state/ACTIVE-SHOP13-INTERIOR-20261019";
 const string T="01a14ea8-d0b0-7c13-b624-bfa8438f6b1d";
 const string S="C180_SHOP_13_FUNCTIONAL_KITCHEN_029";
 const string AS="Assets/ChooGuard/Art/StationInterior/Shop13FunctionalKitchen029_20261019";
 static string Nonce;
 static readonly List<string> AssetsMade=new List<string>();
 static Vector3 V(JToken t){return new Vector3((float)t[0],(float)t[1],(float)t[2]);}
 static string Id(UnityEngine.Object o){return o==null?null:GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();}
 static string PathOf(Transform t){return t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;}
 static GameObject Get(string s){GlobalObjectId id;if(!GlobalObjectId.TryParse(s,out id))throw new Exception("Invalid scene identity");return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;}
 static GameObject Get(long i){return Get("GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-"+i+"-0");}
 static Vector3 WorldQ(JToken q,float y){double x=(double)q[0],z=(double)q[1];return new Vector3((float)(-.34290633335024717*x-.9393696006089878*z),y,(float)(-.9393696006089878*x+.34290633335024717*z));}
 static void Guard(){
  var a=JObject.Parse(File.ReadAllText(R+"/.state/WRITER-AUTHORITY.json"));
  if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["scope"]!=S||(string)a["sole_writer_thread"]!=T)throw new Exception("Exact own ACTIVE C180 writer required");
  string gp=(string)a["delegated_author_lease"],nonce=(string)a["scene_worker_grant_nonce"];
  var g=JObject.Parse(File.ReadAllText(System.IO.Path.IsPathRooted(gp)?gp:R+"/"+gp));
  if(string.IsNullOrEmpty(nonce)||(string)g["grant_nonce"]!=nonce||(string)g["worker_thread"]!=T||(string)g["scope"]!=S)throw new Exception("C180 FIFO grant mismatch");
  if(Nonce==null)Nonce=nonce;else if(Nonce!=nonce)throw new Exception("Native author nonce changed");
  if(SceneManager.GetActiveScene().path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stopped editing FpsStation required");
 }
 static void FixedFrame(GameObject go,string expectedPath){
  if(go==null||!go.activeInHierarchy||PathOf(go.transform)!=expectedPath||Vector3.Distance(go.transform.position,Vector3.zero)>.0003f||Quaternion.Angle(go.transform.rotation,Quaternion.identity)>.002f||Vector3.Distance(go.transform.lossyScale,Vector3.one)>.0003f)throw new Exception("Existing own construction frame changed; preserve current user edit");
 }
 static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
 static Color Col(string s){Color c;if(!ColorUtility.TryParseHtmlString(s,out c))throw new Exception("Invalid material color");return c;}
 static JObject Pose(GameObject go){return new JObject{["id"]=Id(go),["path"]=PathOf(go.transform),["active_self"]=go.activeSelf,["position"]=new JArray(go.transform.position.x,go.transform.position.y,go.transform.position.z)};}

 public static string Apply(){
  Nonce=null;AssetsMade.Clear();Guard();
  string receipt=R+"/"+D+"/APPLIED-FUNCTIONAL-KITCHEN-029.json";
  if(File.Exists(receipt))throw new Exception("029 already saved; never replay");
  var p=JObject.Parse(File.ReadAllText(R+"/"+D+"/FUNCTIONAL-KITCHEN-CONSTRUCTION-PACKET-029.json"));
  var authority=JObject.Parse(File.ReadAllText(R+"/"+(string)p["user_authority"]));
  if((string)p["status"]!="READY_USER_AUTHORIZED_INFERRED_KITCHEN_ONLY"||(bool?)authority["infer_typical_gukbap_equipment_inside_existing_kitchen"]!=true||(bool?)authority["accepted_booth_shell_fixed"]!=true||(string)authority["scope"]!=S)throw new Exception("Explicit current inferred-kitchen authority required");
  var base028=Get((string)p["base028_root_id"]);FixedFrame(base028,(string)p["base028_root_path"]);
  var shop=Get(15594470);if(shop==null||base028.transform.parent!=shop.transform)throw new Exception("Exact own shop parent required");
  var e=(JObject)p["deactivate_solid_wash_top"];var top=Get((string)e["id"]);
  if(top==null||!top.activeSelf||top.transform.parent!=base028.transform||PathOf(top.transform)!=(string)e["path"]||Vector3.Distance(top.transform.localPosition,Vector3.zero)>.0002f||Quaternion.Angle(top.transform.localRotation,Quaternion.identity)>.002f||Vector3.Distance(top.transform.localScale,Vector3.one)>.0002f)throw new Exception("Only unchanged own028 solid sink workface may be replaced");
  var topMesh=top.GetComponent<MeshFilter>();
  if(topMesh==null||AssetDatabase.GetAssetPath(topMesh.sharedMesh)!=(string)e["mesh_asset"])throw new Exception("Own028 sink top mesh changed");
  var scene=SceneManager.GetActiveScene();bool priorDirty=scene.isDirty;
  if(priorDirty){Guard();if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save existing user edits");}
  Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("SHOP13 user-authorized logical kitchen equipment029");
  bool saved=false;var created=new JArray();JObject replaced=Pose(top);GameObject root=null;
  try {
   Guard();Folder(AS+"/Materials");Folder(AS+"/Meshes");
   Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");if(shader==null)throw new Exception("Existing supported shader required");
   var mats=new Dictionary<string,Material>();
   foreach(var prop in ((JObject)p["materials"]).Properties()){
    var m=(JObject)prop.Value;Material mat;
    if(m["reuse"]!=null){mat=AssetDatabase.LoadAssetAtPath<Material>((string)m["reuse"]);if(mat==null)throw new Exception("Existing own028 shared material missing");}
    else{
     Guard();mat=new Material(shader);mat.name="S13Kitchen029_"+prop.Name;Color color=Col((string)m["color"]);
     if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",color);if(mat.HasProperty("_Color"))mat.SetColor("_Color",color);
     if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",(float)m["metallic"]);if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",(float)m["smoothness"]);if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",(float)m["smoothness"]);
     string path=AssetDatabase.GenerateUniqueAssetPath(AS+"/Materials/"+mat.name+".mat");AssetDatabase.CreateAsset(mat,path);AssetDatabase.SaveAssetIfDirty(mat);AssetsMade.Add(path);
    }
    mats.Add(prop.Name,mat);
   }
   root=new GameObject("SHOP13_USER_INFERRED_FUNCTIONAL_KITCHEN_MODEL_029");root.transform.SetParent(shop.transform,false);root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;root.isStatic=true;Undo.RegisterCreatedObjectUndo(root,"Add own inferred kitchen equipment");
   foreach(var a in ((JArray)p["assemblies"]).OfType<JObject>()){
    Guard();var go=new GameObject((string)a["name"]);go.transform.SetParent(root.transform,false);go.transform.SetPositionAndRotation(WorldQ(a["q"],(float)a["y"]),Quaternion.identity);go.isStatic=true;var members=new JArray();
    foreach(var g in ((JArray)a["groups"]).OfType<JObject>()){
     Guard();var mesh=new Mesh();mesh.name=go.name+"_"+(string)g["name"];var verts=((JArray)g["vertices"]).Select(V).ToArray();if(verts.Length>65535)mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=verts;mesh.triangles=((JArray)g["triangles"]).Select(v=>(int)v).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
     string path=AssetDatabase.GenerateUniqueAssetPath(AS+"/Meshes/"+mesh.name+".asset");AssetDatabase.CreateAsset(mesh,path);AssetDatabase.SaveAssetIfDirty(mesh);AssetsMade.Add(path);
     var part=new GameObject((string)g["name"]);part.transform.SetParent(go.transform,false);part.isStatic=true;part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=mats[(string)g["material"]];members.Add(new JObject{["id"]=Id(part),["mesh"]=path,["material"]=(string)g["material"]});
    }
    if(a["collider"]!=null&&a["collider"].Type!=JTokenType.Null){var bounds=new GameObject("OwnEquipmentPhysicalBounds");bounds.transform.SetParent(go.transform,false);bounds.transform.localRotation=Quaternion.LookRotation(WorldQ(a["front"],0).normalized,Vector3.up);bounds.isStatic=true;var box=bounds.AddComponent<BoxCollider>();Vector3 size=V(a["collider"]);box.size=size;box.center=new Vector3(0,size.y/2,0);}
    created.Add(new JObject{["id"]=Id(go),["path"]=PathOf(go.transform),["q"]=a["q"],["function"]=a["function"],["evidence_class"]="USER_AUTHORIZED_INFERRED_MODEL",["mesh_members"]=members});
   }
   Guard();Undo.RecordObject(top,"Replace own solid wash top by same footprint open sink");top.SetActive(false);
   EditorSceneManager.MarkSceneDirty(scene);Guard();if(!EditorSceneManager.SaveScene(scene))throw new Exception("Actual kitchen save failed");saved=true;Undo.CollapseUndoOperations(undo);
   var r=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["status"]="APPLIED_SAVED_USER_AUTHORIZED_INFERRED_FUNCTIONAL_KITCHEN_029",["scope"]=S,["thread"]=T,["grant_nonce"]=Nonce,["scene"]=scene.path,["scene_was_dirty"]=priorDirty,["root_id"]=Id(root),["root_path"]=PathOf(root.transform),["assembly_count"]=created.Count,["editable_mesh_groups"]=created.OfType<JObject>().Sum(x=>((JArray)x["mesh_members"]).Count),["generated_gameobjects"]=root.GetComponentsInChildren<Transform>(true).Length,["assemblies"]=created,["same_footprint_solid_wash_top_replaced_before"]=replaced,["new_own_assets"]=new JArray(AssetsMade),["accepted_shell_027_and_partitions028_changed"]=false,["dining_changed"]=false,["native_floor_columns_otherstores_original_shared_assets_changed"]=false,["inferred_model_not_actual_site_inventory"]=true,["user_validation_owner"]=true,["QA_tests_probes_reopen_captures"]=false,["decisions"]=p["decisions"]};
   File.WriteAllText(receipt,r.ToString(Formatting.Indented));
   return new JObject{["status"]=r["status"],["receipt"]=D+"/APPLIED-FUNCTIONAL-KITCHEN-029.json",["assemblies"]=r["assembly_count"],["editable_mesh_groups"]=r["editable_mesh_groups"],["root_id"]=r["root_id"]}.ToString(Formatting.None);
  }catch(Exception ex){if(!saved)Undo.RevertAllDownToGroup(undo);File.WriteAllText(R+"/"+D+"/APPLY-ERROR-029.json",new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["saved"]=saved,["error"]=ex.ToString(),["new_own_assets_retained"]=new JArray(AssetsMade)}.ToString());throw;}
 }
}
