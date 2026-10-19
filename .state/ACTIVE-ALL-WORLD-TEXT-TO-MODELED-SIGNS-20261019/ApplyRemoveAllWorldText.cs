using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public static class ApplyRemoveAllWorldText {
 const string R="/Users/um-yunsang/CHOOGuard",D=".state/ACTIVE-ALL-WORLD-TEXT-TO-MODELED-SIGNS-20261019",A="Assets/ChooGuard/Art/StationInterior/DirectWorldSignGeometry_20261019/TextRemoval",T="01a13cb4-b738-7823-bf16-30a39ea3d8c6",S="C180_WORLD_SIGN_GEOMETRY_REMOVE_TEXT_APPLY_SAVE";
 static string Nonce;static JArray Made;static Dictionary<string,Material> Materials=new Dictionary<string,Material>();
 static Vector3 V(JToken t){return new Vector3((float)t[0],(float)t[1],(float)t[2]);}
 static string Id(UnityEngine.Object o){return GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();}
 static string PathOf(Transform t){return t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;}
 static void Guard(){var a=JObject.Parse(File.ReadAllText(R+"/.state/WRITER-AUTHORITY.json"));if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["scope"]!=S||(string)a["sole_writer_thread"]!=T)throw new Exception("Exact own active C180 writer required");var g=JObject.Parse(File.ReadAllText(R+"/"+(string)a["delegated_author_lease"]));string n=(string)a["scene_worker_grant_nonce"];if(String.IsNullOrEmpty(n)||(string)g["grant_nonce"]!=n||(string)g["worker_thread"]!=T||(string)g["scope"]!=S)throw new Exception("Exact FIFO nonce required");if(Nonce==null)Nonce=n;else if(Nonce!=n)throw new Exception("Grant changed");if(SceneManager.GetActiveScene().path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stopped editing FpsStation required");}
 static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int k=p.LastIndexOf('/');Folder(p.Substring(0,k));AssetDatabase.CreateFolder(p.Substring(0,k),p.Substring(k+1));}
 static GameObject Get(string s){GlobalObjectId id;if(!GlobalObjectId.TryParse(s,out id))throw new Exception("Bad source identity");return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;}
 static Material Mat(string color){if(Materials.ContainsKey(color))return Materials[color];Color c;if(!ColorUtility.TryParseHtmlString(color,out c))throw new Exception("Bad physical ink color");var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name="PhysicalInk_"+color.TrimStart('#');m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.25f);m.SetFloat("_Cull",0);m.doubleSidedGI=true;AssetDatabase.CreateAsset(m,A+"/Materials/"+m.name+".mat");Materials[color]=m;return m;}
 static Mesh Mesh(string name,JObject data,float width,float height,float depth){var verts=((JArray)data["vertices"]).Select(V).Select(v=>new Vector3((v.x-.5f)*width,(v.y-.5f)*height,v.z*depth)).ToArray();var mesh=new Mesh();mesh.name=name;if(verts.Length>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.vertices=verts;mesh.triangles=((JArray)data["triangles"]).Select(x=>(int)x).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();string p=A+"/Meshes/"+name+".asset";AssetDatabase.CreateAsset(mesh,p);Made.Add(p);return mesh;}
 static GameObject Construct(GameObject old,JObject e,JArray palette,string key,float aspect,float maxWidth,float maxHeight,float depth){
  Guard();float h=Mathf.Min(maxHeight,maxWidth/Mathf.Max(aspect,.001f)),w=h*aspect;var root=new GameObject("DIRECT_MODELED_PHYSICAL_"+key);root.transform.SetParent(old.transform.parent,false);root.transform.SetPositionAndRotation(V(e["bounds_center"]),old.transform.rotation);root.transform.localScale=Vector3.one;root.isStatic=true;Undo.RegisterCreatedObjectUndo(root,"Create actual physical sign contour geometry");int i=0;
  foreach(var p in palette.OfType<JObject>()){var part=new GameObject((string)p["name"]??"Physical_Outline");part.transform.SetParent(root.transform,false);part.isStatic=true;part.AddComponent<MeshFilter>().sharedMesh=Mesh(key+"_"+i++,p,w,h,depth);part.AddComponent<MeshRenderer>().sharedMaterial=Mat((string)p["color"]);}
  return root;
 }
 static bool HasWorldText(Transform t){var c=t.GetComponentInParent<Canvas>(true);if(c!=null&&c.renderMode!=RenderMode.WorldSpace)return false;return t.GetComponent<TMP_Text>()!=null||t.GetComponent<TextMesh>()!=null||t.GetComponent<UnityEngine.UI.Text>()!=null;}
 static void Remove(GameObject go){
  var sub=go.GetComponentsInChildren<Transform>(true).Where(t=>t!=go.transform&&(t.GetComponent<TMP_SubMesh>()!=null||t.GetComponent<TMP_SubMeshUI>()!=null||t.name.StartsWith("TMP SubMesh"))).OrderByDescending(t=>PathOf(t).Length).ToArray();foreach(var t in sub)if(t!=null)Undo.DestroyObjectImmediate(t.gameObject);
  if(go.transform.childCount==0){Undo.DestroyObjectImmediate(go);return;}
  foreach(var t in go.GetComponents<Component>().Where(x=>x is TMP_Text||x is TextMesh||x is UnityEngine.UI.Text).ToArray())Undo.DestroyObjectImmediate(t);
  var f=go.GetComponent<MeshFilter>();if(f!=null&&(f.sharedMesh==null||f.sharedMesh.name.Contains("TextMeshPro"))){var r=go.GetComponent<MeshRenderer>();if(r!=null)Undo.DestroyObjectImmediate(r);Undo.DestroyObjectImmediate(f);}
 }
 public static string Apply(){
  Nonce=null;Materials.Clear();Made=new JArray();Guard();string receipt=R+"/"+D+"/APPLIED-ALL-WORLD-TEXT-REMOVED.json";if(File.Exists(receipt)||AssetDatabase.IsValidFolder(A))throw new Exception("No blind replay of settled text-removal work");
  var inventory=JObject.Parse(File.ReadAllText(R+"/"+D+"/CURRENT-WORLD-TEXT-AND-SIGN-INPUT.json"));var numbers=JObject.Parse(File.ReadAllText(R+"/"+D+"/NUMBER-AND-KIOSK-SOLIDS.json"));var source=JObject.Parse(File.ReadAllText(R+"/"+D+"/locker-source-art/LOCKER-SOURCE-CONTOUR-ART.json"));var entries=((JArray)inventory["world_texts"]).OfType<JObject>().ToArray();var targets=new Dictionary<string,GameObject>();
  foreach(var e in entries){var go=Get((string)e["id"]);if(go==null)continue;if(PathOf(go.transform)!=(string)e["path"]||go.activeInHierarchy!=(bool)e["active_in_hierarchy"])throw new Exception("Current text identity/visibility changed; preserve newer work");if(Vector3.Distance(go.transform.position,V(e["position"]))>.0004f||Quaternion.Angle(go.transform.rotation,Quaternion.Euler(V(e["euler"])))>.004f)throw new Exception("Current text pose changed");targets[(string)e["id"]]=go;}
  var current=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).Where(HasWorldText).ToArray();if(current.Any(t=>!targets.ContainsKey(Id(t.gameObject))))throw new Exception("New current world-text identity requires construction input update");
  var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty;if(dirty&&!EditorSceneManager.SaveScene(scene))throw new Exception("Preserve existing user edits before replacement");Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Remove all world text components; construct actual physical sign and numeric contours");bool saved=false;var changed=new JArray();var removed=new JArray();
  try{
   Guard();Folder(A+"/Meshes");Folder(A+"/Materials");
   foreach(var e in entries){string id=(string)e["id"];if(!targets.ContainsKey(id))continue;var old=targets[id];Guard();
    if((bool)e["active_in_hierarchy"]){string content=(string)e["text"];JObject art=null;string key=null;float aspect=1,depth=.001f;float w=(float)Math.Sqrt(Math.Pow((float)e["bounds_size"][0],2)+Math.Pow((float)e["bounds_size"][2],2));float h=(float)e["bounds_size"][1];JArray palette=null;
     if(content.All(Char.IsDigit)&&content.Length>0){art=(JObject)numbers["numbers"][content];key="LOCKER_NUMBER_"+content;aspect=(float)art["aspect"];art["name"]="Closed_Numeric_Bezier_Outline";palette=new JArray(art);depth=.00035f;}
     else if((string)e["name"]=="Kiosk_modeled_header"){art=(JObject)numbers["kiosk"];key="SOBAN_KIOSK_ACTUAL_BRAND";aspect=(float)art["aspect"];palette=(JArray)art["palette"];depth=.001f;}
     else if(content!="Lovely Cosmetics"){
      string shortId=id.Split('-')[3];art=((JArray)source["arts"]).OfType<JObject>().FirstOrDefault(x=>((JArray)x["target_gameobject_ids"]).Any(v=>(string)v==shortId));
      if(art==null)throw new Exception("Actual sign source contours missing for "+(string)e["name"]);key=(string)art["key"];aspect=(float)art["aspect"];palette=(JArray)art["palette"];depth=.0015f;
      if(key=="locker_korean"){w=.95f;h=.17f;}else if(key=="locker_english"){w=.52f;h=.075f;}
     }
     if(palette!=null){var root=Construct(old,e,palette,key,aspect,w,h,depth);changed.Add(new JObject{["old_text_id"]=id,["new_physical_id"]=Id(root),["path"]=PathOf(root.transform),["art"]=key,["true_closed_geometry"]=true,["source_font_atlas_used"]=false});}
    }
    removed.Add(new JObject{["id"]=id,["path"]=e["path"],["text_before"]=e["text"],["active_before"]=e["active_in_hierarchy"]});Remove(old);
   }
   int remaining=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).Count(HasWorldText);if(remaining!=0)throw new Exception("Construction did not remove every world text component");Guard();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Actual text-removal Scene save failed");saved=true;Undo.CollapseUndoOperations(undo);
   var r=new JObject{["status"]="APPLIED_SAVED_ALL_WORLD_TEXT_COMPONENTS_REMOVED_PHYSICAL_CONTOURS_CREATED",["utc"]=DateTime.UtcNow.ToString("o"),["scope"]=S,["grant_nonce"]=Nonce,["scene"]=scene.path,["removed_text_objects_or_components"]=removed.Count,["removed"]=removed,["physical_sign_and_number_assemblies"]=changed.Count,["replacements"]=changed,["remaining_world_text_components_in_constructor"]=remaining,["unsupported_Lovely_Cosmetics_removed"]=true,["inactive_legacy_text_also_removed"]=true,["new_mesh_assets"]=Made,["numeric_mark_sequence_qualification"]="Existing MODEL numbers retained as physical markings; source sequence not field-confirmed",["screen_ui_changed"]=false,["native_floor_booth_equipment_positions_changed"]=false,["source_assets_changed"]=false,["QA_tests_probes_reopen_captures"]=false,["validation_owner"]="user"};File.WriteAllText(receipt,r.ToString(Formatting.Indented));File.WriteAllText(R+"/.state/CURRENT-WORLD-TEXT-REMOVAL-20261019.json",r.ToString(Formatting.Indented));return new JObject{["status"]=r["status"],["removed_text_components"]=removed.Count,["physical_assemblies"]=changed.Count,["remaining_world_text_components"]=remaining,["receipt"]=D+"/APPLIED-ALL-WORLD-TEXT-REMOVED.json"}.ToString(Formatting.None);
  }catch(Exception ex){if(!saved)Undo.RevertAllDownToGroup(undo);File.WriteAllText(R+"/"+D+"/TEXT-REMOVAL-ERROR.json",new JObject{["saved"]=saved,["error"]=ex.ToString(),["assets_retained"]=Made}.ToString());throw;}
 }
}
