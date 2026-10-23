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
public static class ApplyActual2FSigns {
 const string R="/Users/um-yunsang/CHOOGuard",D=".state/ACTIVE-1F-STORE-SIGN-GRAPHICS-20261019",A="Assets/ChooGuard/Art/StationInterior/Actual2FPhysicalSigns_20261019",T="01a13cb4-b738-7823-bf16-30a39ea3d8c6",S="C180_STORE_SIGNS_DIRECT_GRAPHICS_2F_APPLY_SAVE";
 static string Nonce;static JObject Packet;static JArray Made,Disabled,Changes;static Dictionary<string,JObject> Expected;static int Counter;
 static Vector3 V(JToken t){return new Vector3((float)t[0],(float)t[1],(float)t[2]);}
 static string Id(UnityEngine.Object o){return GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();}
 static string PathOf(Transform t){return t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;}
 static void Guard(){var a=JObject.Parse(File.ReadAllText(R+"/.state/WRITER-AUTHORITY.json"));if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["sole_writer_thread"]!=T||(string)a["scope"]!=S)throw new Exception("Exact own ACTIVE C180 writer required");var g=JObject.Parse(File.ReadAllText(R+"/"+(string)a["delegated_author_lease"]));string n=(string)a["scene_worker_grant_nonce"];if(String.IsNullOrEmpty(n)||(string)g["grant_nonce"]!=n||(string)g["scope"]!=S||(string)g["worker_thread"]!=T)throw new Exception("FIFO nonce mismatch");if(Nonce==null)Nonce=n;else if(Nonce!=n)throw new Exception("Author changed");if(SceneManager.GetActiveScene().path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stopped editing FpsStation required");}
 static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
 static GameObject Get(string id){GlobalObjectId i;if(!GlobalObjectId.TryParse(id,out i))throw new Exception("Invalid target ID");return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(i) as GameObject;}
 static GameObject Target(string name){
  if(!Expected.ContainsKey(name))throw new Exception("Exact current 2F sign target absent: "+name);var e=Expected[name];var o=Get((string)e["id"]);
  if(o==null||!o.activeInHierarchy||PathOf(o.transform)!=(string)e["path"]||!PathOf(o.transform).Contains("/2F/MAIN2F_R25_SOURCE_BOUND_SPECIALIST_SHOPS/"))throw new Exception("Current own2F target changed: "+name+";id="+(string)e["id"]+";found="+(o!=null)+";active="+(o!=null&&o.activeInHierarchy)+";path="+(o==null?"NULL":PathOf(o.transform)));
  if(Vector3.Distance(o.transform.position,V(e["position"]))>.0003f||Quaternion.Angle(o.transform.rotation,Quaternion.Euler(V(e["euler"])))>.003f||Vector3.Distance(o.transform.localScale,V(e["local_scale"]))>.0003f)throw new Exception("Preserve newer user sign pose: "+name);
  return o;
 }
 static Transform Shop(Transform t){while(t!=null&&!t.name.StartsWith("R25_"))t=t.parent;if(t==null)throw new Exception("Current2F store owner required");return t;}
 static GameObject Own(GameObject old,string name){var root=Shop(old.transform);var matches=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name==name&&t.gameObject.activeInHierarchy).ToArray();if(matches.Length!=1)throw new Exception("Exact own sign physical member required: "+name);return matches[0].gameObject;}
 static void Hide(GameObject o){if(!o.activeSelf)return;Undo.RecordObject(o,"Replace current text/proxy sign by actual constructed source sign");Disabled.Add(new JObject{["id"]=Id(o),["path"]=PathOf(o.transform)});o.SetActive(false);}
 static Material Material(string name,string hex,bool luminous=false,Texture tex=null){Guard();var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=name;Color c;if(!ColorUtility.TryParseHtmlString(hex,out c))throw new Exception("Invalid sign color");m.SetColor("_BaseColor",c);m.SetFloat("_Metallic",luminous?.12f:.05f);m.SetFloat("_Smoothness",.36f);m.SetFloat("_Cull",0);m.doubleSidedGI=true;if(tex!=null)m.SetTexture("_BaseMap",tex);if(luminous){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*(tex==null?1.15f:.25f));if(tex!=null)m.SetTexture("_EmissionMap",tex);}string path=A+"/Materials/"+name+".mat";AssetDatabase.CreateAsset(m,path);Made.Add(path);return m;}
 static Mesh SaveMesh(string name,Vector3[] v,int[] tris,Vector2[] uv=null){Guard();var m=new Mesh();m.name=name;if(v.Length>65535)m.indexFormat=IndexFormat.UInt32;m.vertices=v;m.triangles=tris;if(uv!=null)m.uv=uv;m.RecalculateNormals();m.RecalculateBounds();string p=A+"/Meshes/"+name+".asset";AssetDatabase.CreateAsset(m,p);Made.Add(p);return m;}
 static GameObject Root(GameObject old,string suffix,Vector3 center){var go=new GameObject("ACTUAL_SOURCE_PHYSICAL_"+suffix);go.transform.SetParent(old.transform.parent,false);go.transform.SetPositionAndRotation(center,old.transform.rotation);go.transform.localScale=Vector3.one;go.isStatic=true;Undo.RegisterCreatedObjectUndo(go,"Construct actual source2F sign");return go;}
 static void MeshPart(GameObject root,string name,Mesh mesh,Material mat){var p=new GameObject(name);p.transform.SetParent(root.transform,false);p.isStatic=true;p.AddComponent<MeshFilter>().sharedMesh=mesh;p.AddComponent<MeshRenderer>().sharedMaterial=mat;}
 static float FrontSign(GameObject old,GameObject board){if(board==null)return 1;return (Quaternion.Inverse(old.transform.rotation)*(old.transform.position-board.transform.position)).z>=0?1:-1;}
 static GameObject Art(string target,string key,float height,float maxWidth,string boardName=null,bool lit=true,bool plaque=false){
  Guard();var old=Target(target);var board=boardName==null?null:Own(old,boardName);var spec=(JObject)Packet["arts"][key];var groups=((JArray)spec["groups"]).OfType<JObject>().ToArray();float aspect=(float)groups[0]["canvas_aspect"];height=Mathf.Min(height,maxWidth/aspect);float front=FrontSign(old,board);
  Vector3 center=old.GetComponent<Renderer>().bounds.center;var root=Root(old,key+"_"+Counter++,center);
  if(plaque){var backing=GameObject.CreatePrimitive(PrimitiveType.Cube);backing.name="Actual_White_Physical_Sign_Plaque";backing.transform.SetParent(root.transform,false);backing.transform.localPosition=new Vector3(0,0,-front*.004f);backing.transform.localScale=new Vector3(aspect*height,height,.007f);UnityEngine.Object.DestroyImmediate(backing.GetComponent<Collider>());backing.GetComponent<MeshRenderer>().sharedMaterial=Material("PlaqueWhite_"+Counter,"#EEEFE4");}
  foreach(var g in groups){var vertices=((JArray)g["vertices"]).Select(V).Select(v=>new Vector3(v.x*height,v.y*height,-v.z*front)).ToArray();var mesh=SaveMesh(key+"_"+(string)g["name"]+"_"+Counter,vertices,((JArray)g["triangles"]).Select(x=>(int)x).ToArray());MeshPart(root,(string)g["name"],mesh,Material(key+"_"+(string)g["name"]+"_"+Counter,(string)g["color"],lit));}
  Hide(old);Changes.Add(new JObject{["target_id"]=Id(old),["root_id"]=Id(root),["path"]=PathOf(root.transform),["source"]=spec["source"],["art"]=key,["method"]="ACTUAL_SOURCE_CONTOURS_CLOSED_EXTRUSION_NO_FONT"});return root;
 }
 static void Storyway(string target,bool orange){
  Guard();var old=Target(target);var specs=((JArray)Packet["storyway_reuse"]).OfType<JObject>().ToArray();var sourceMeshes=specs.Select(x=>AssetDatabase.LoadAssetAtPath<Mesh>((string)x["mesh_asset"])).ToArray();if(sourceMeshes.Any(x=>x==null))throw new Exception("Existing actual traced StoryWay source geometry required");
  var b=sourceMeshes[0].bounds;foreach(var m in sourceMeshes.Skip(1))b.Encapsulate(m.bounds);float height=orange?.18f:.47f,maxWidth=orange?.9f:3.55f;float factor=Mathf.Min(height/b.size.y,maxWidth/b.size.x);
  var board=Own(old,orange?"MAY26_left_frosted_logo_panel":"navy_fascia_main");float front=FrontSign(old,board);Vector3 center=old.GetComponent<Renderer>().bounds.center;
  var oldSymbol=old.transform.parent.GetComponentsInChildren<Transform>(true).Where(t=>t.gameObject.activeInHierarchy&&(t.name.StartsWith("MAY26_white_smile_arc_")||t.name.StartsWith("MAY26_smile_top_dot_"))).ToArray();
  if(!orange&&oldSymbol.Length>0){var union=old.GetComponent<Renderer>().bounds;foreach(var t in oldSymbol){var r=t.GetComponent<Renderer>();if(r!=null)union.Encapsulate(r.bounds);}center=union.center;}
  var root=Root(old,orange?"StoryWay_Actual_Frosted_Print":"StoryWay_Custom_Wordmark_And_Smile",center);
  for(int j=0;j<specs.Length;j++){
   var src=sourceMeshes[j];var v=src.vertices.Select(x=>new Vector3((x.x-b.center.x)*factor,(x.y-b.center.y)*factor,(x.z-b.min.z)*front)).ToArray();var m=SaveMesh("StoryWay_ActualContour_"+j+"_"+(orange?"Glass":"Main"),v,src.triangles);
   var original=AssetDatabase.LoadAssetAtPath<Material>((string)specs[j]["material_asset"]);Color col=orange?new Color(.9f,.29f,.045f,1):original.GetColor("_BaseColor");string hex="#"+ColorUtility.ToHtmlStringRGBA(col);MeshPart(root,(string)specs[j]["name"],m,Material("StoryWay_Actual_"+j+"_"+(orange?"Glass":"Main"),hex,!orange));
  }
  Hide(old);if(!orange)foreach(var t in oldSymbol)Hide(t.gameObject);
  Changes.Add(new JObject{["target_id"]=Id(old),["root_id"]=Id(root),["art"]="Actual_Source_StoryWay_Custom_Contours",["source_assets_read_only"]=true,["source_parts"]=new JArray(specs.Select(x=>(string)x["mesh_asset"]))});
 }
 static Dictionary<string,Material> PrintMaterials=new Dictionary<string,Material>();
 static Material PrintMat(string key){if(PrintMaterials.ContainsKey(key))return PrintMaterials[key];var spec=(JObject)Packet["prints"][key];string texPath=A+"/Prints/"+key+".png";Guard();File.Copy(R+"/"+(string)spec["path"],R+"/"+texPath);AssetDatabase.ImportAsset(texPath,ImportAssetOptions.ForceSynchronousImport);var importer=AssetImporter.GetAtPath(texPath) as TextureImporter;if(importer!=null){importer.maxTextureSize=2048;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();}var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);if(tex==null)throw new Exception("Actual source print import failed");var mat=Material(key+"_ActualSourcePrint","#FFFFFF",true,tex);PrintMaterials.Add(key,mat);Made.Add(texPath);return mat;}
 static void PrintFace(string target,string boardName,string key,float maxWidth,float height,IEnumerable<string> hides){
  Guard();var old=Target(target);var board=Own(old,boardName);float front=FrontSign(old,board);float aspect=(float)Packet["prints"][key]["aspect"];height=Mathf.Min(height,maxWidth/aspect);float width=height*aspect;
  Vector3 normal=old.transform.rotation*(Vector3.forward*front);var root=Root(old,key+"_"+Counter++,board.GetComponent<Renderer>().bounds.center+normal*(Mathf.Abs(board.transform.lossyScale.z)/2+.0025f));
  var mesh=SaveMesh(key+"_Physical_Face_"+Counter,new[]{new Vector3(-width/2,-height/2,0),new Vector3(width/2,-height/2,0),new Vector3(width/2,height/2,0),new Vector3(-width/2,height/2,0)},new[]{0,2,1,0,3,2},new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)});MeshPart(root,"Actual_Photographed_Printed_Sign_Face",mesh,PrintMat(key));
  foreach(var n in hides)Hide(Target(n));
  Changes.Add(new JObject{["root_id"]=Id(root),["board_id"]=Id(board),["art"]=key,["source"]=Packet["prints"][key]["source"],["source_aspect_preserved"]=true,["method"]="ACTUAL_SOURCE_PRINT_ON_EXISTING_PHYSICAL_BOARD_NO_TYPED_MENU_SUBSTITUTE"});
 }
 public static string Apply(){
  Nonce=null;Counter=0;PrintMaterials.Clear();Guard();string receipt=R+"/"+D+"/APPLIED-ACTUAL-2F-PHYSICAL-SIGNS.json";if(File.Exists(receipt)||AssetDatabase.IsValidFolder(A))throw new Exception("Already applied/imported; no blind replay");
  var authority=JObject.Parse(File.ReadAllText(R+"/"+D+"/USER-AUTHORITY.json"));if((string)authority["floors"][0]!="2F"||(bool?)authority["direct_apply_save_authorized"]!=true)throw new Exception("Explicit2F-only user instruction required");
  Packet=JObject.Parse(File.ReadAllText(R+"/"+D+"/ACTUAL-2F-SIGN-ART-PACKET.json"));var inventory=JObject.Parse(File.ReadAllText(R+"/"+D+"/CURRENT-LIVE-SIGN-CONSTRUCTION-STATE.json"));Expected=((JArray)inventory["texts"]).OfType<JObject>().ToDictionary(x=>(string)x["name"],x=>x);var required=new List<string>();for(int i=0;i<3;i++)required.Add("rear_bay_"+i+"_header_lettering");foreach(var side in new[]{"left","right"})for(int i=0;i<6;i++)required.Add("Menu_row_"+side+i);for(int i=0;i<5;i++)required.Add("Kitchen_menu_category_"+i);required.Add("Digital_menu_modeled_text");required.Add("Tray_return_direct_letters");foreach(var key in required)Target(key);
  var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty;if(dirty&&!EditorSceneManager.SaveScene(scene))throw new Exception("Could not preserve current user edits");Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Build actual2F shop logo contour meshes and source sign prints");bool saved=false;Made=new JArray();Disabled=new JArray();Changes=new JArray();
  try{
   Guard();Folder(A+"/Meshes");Folder(A+"/Materials");Folder(A+"/Prints");
   // Current per-store authors already saved the actual main logo meshes. Preserve those exact results.
   for(int i=0;i<3;i++){string n="rear_bay_"+i+"_header_lettering";PrintFace(n,"rear_bay_"+i+"_red_product_header","gijang_actual_header",1.04f,.18f,new[]{n});}
   foreach(string side in new[]{"left","right"}){
    string n="Menu_row_"+side+"0";var old=Target(n);var own=Shop(old.transform);
    // Supersede only the same printed menu's previous separate logo layers to avoid duplicate print.
    foreach(var t in own.GetComponentsInChildren<Transform>(true).Where(t=>t.gameObject.activeInHierarchy&&(t.name=="SOBAN_PHOTO_SOLID_Menu_brand_"+side||t.name=="SOBAN_PHOTO_BEVELED_RETURN_Menu_brand_"+side)).ToArray())Hide(t.gameObject);
    var hides=new List<string>();for(int i=0;i<6;i++)hides.Add("Menu_row_"+side+i);PrintFace(n,"Tall_menu_backing_"+side,"soban_printed_menu",1.60f,2.25f,hides);
   }
   PrintFace("Kitchen_menu_category_0","Kitchen_high_menu_lightbox","soban_kitchen_lightbox",5.35f,.56f,Enumerable.Range(0,5).Select(i=>"Kitchen_menu_category_"+i));
   PrintFace("Digital_menu_modeled_text","Entrance_digital_menu_screen","soban_actual_tv",.92f,.52f,new[]{"Digital_menu_modeled_text"});
   Art("Tray_return_direct_letters","tray_return",.125f,.38f,null,false,true);
   Guard();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Actual2F signs Scene save failed");saved=true;Undo.CollapseUndoOperations(undo);
   var result=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["status"]="APPLIED_SAVED_ACTUAL_SOURCE_2F_PHYSICAL_SIGNS",["scene"]=scene.path,["scene_was_dirty"]=dirty,["scope"]=S,["thread"]=T,["grant_nonce"]=Nonce,["floor"]=2,["stores"]=new JArray("Gijang-specialties","Soban"),["text_proxies_replaced"]=22,["constructed_sign_assemblies"]=Changes.Count,["changes"]=Changes,["disabled_prior_text_and_primitive_symbol_ids"]=Disabled,["new_runtime_assets"]=Made,["source_map"]=Packet["source_mapping"],["source_prices_epoch_not_current_claim"]=true,["current_main_signs_by_existing_store_authors_preserved"]=true,["all1F_signs_and_locker_state_fixed"]=true,["booth_native_floor_columns_circulation_original_source_assets_changed"]=false,["exact_measured_complete_digital_twin_claimed"]=false,["user_validation_owner"]=true,["QA_tests_probes_reopen_captures"]=false};
   File.WriteAllText(receipt,result.ToString(Formatting.Indented));File.WriteAllText(R+"/.state/CURRENT-2F-ACTUAL-PHYSICAL-SIGNS-20261019.json",result.ToString(Formatting.Indented));return new JObject{["status"]=result["status"],["text_proxies_replaced"]=22,["assemblies"]=Changes.Count,["receipt"]=D+"/APPLIED-ACTUAL-2F-PHYSICAL-SIGNS.json"}.ToString(Formatting.None);
  }catch(Exception ex){if(!saved)Undo.RevertAllDownToGroup(undo);File.WriteAllText(R+"/"+D+"/APPLY-ERROR.json",new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["saved"]=saved,["error"]=ex.ToString(),["retained_new_assets"]=Made}.ToString());throw;}
 }
}
