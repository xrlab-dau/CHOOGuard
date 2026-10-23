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
public static class ApplyDownloadedLockers {
 const string R="/Users/um-yunsang/CHOOGuard";
 const string D=".state/ACTIVE-1F-LOCKERS-FREE-ASSETS-20261019";
 const string S="C180_LOCKER_1F_FREE_ASSET_APPLY_SAVE";
 const string T="01a13cb4-b738-7823-bf16-30a39ea3d8c6";
 const string A="Assets/ChooGuard/Art/StationInterior/Downloaded1FLockers_20261019";
 static string Nonce;
 static Vector3 V(JToken t){return new Vector3((float)t[0],(float)t[1],(float)t[2]);}
 static string Id(UnityEngine.Object o){return GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();}
 static string PathOf(Transform t){return t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;}
 static GameObject Get(string s){GlobalObjectId id;if(!GlobalObjectId.TryParse(s,out id))throw new Exception("Invalid scene identity");return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;}
 static void Guard(){
  var a=JObject.Parse(File.ReadAllText(R+"/.state/WRITER-AUTHORITY.json"));
  if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["scope"]!=S||(string)a["sole_writer_thread"]!=T)throw new Exception("Exact ACTIVE own C180 writer required");
  var g=JObject.Parse(File.ReadAllText(R+"/"+(string)a["delegated_author_lease"]));string n=(string)a["scene_worker_grant_nonce"];
  if(String.IsNullOrEmpty(n)||(string)g["grant_nonce"]!=n||(string)g["scope"]!=S||(string)g["worker_thread"]!=T)throw new Exception("FIFO nonce mismatch");
  if(Nonce==null)Nonce=n;else if(Nonce!=n)throw new Exception("Author nonce changed");
  if(SceneManager.GetActiveScene().path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stopped editing FpsStation required");
 }
 static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
 static GameObject Unchanged(JObject e){
  var o=Get((string)e["id"]);if(o==null||!o.activeInHierarchy||PathOf(o.transform)!=(string)e["path"])throw new Exception("Current own target no longer present: "+(string)e["name"]);
  if(Vector3.Distance(o.transform.position,V(e["position"]))>.0003f||Quaternion.Angle(o.transform.rotation,Quaternion.Euler(V(e["euler"])))>.003f||Vector3.Distance(o.transform.localScale,V(e["local_scale"]))>.0003f)throw new Exception("Preserve newer user pose: "+o.name);
  return o;
 }
 static Material Mat(string name,string hex,float metallic,float smooth,bool glass=false){
  Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new Exception("Current URP Lit shader required");
  var m=new Material(shader);m.name=name;Color c;if(!ColorUtility.TryParseHtmlString(hex,out c))throw new Exception("Bad material color");
  m.SetColor("_BaseColor",c);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
  if(glass){m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetInt("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;}
  string path=A+"/Materials/"+name+".mat";AssetDatabase.CreateAsset(m,path);return m;
 }
 static GameObject Part(string name,Transform parent,Mesh mesh,Material mat,Vector3 local){
  var p=new GameObject(name);p.transform.SetParent(parent,false);p.transform.localPosition=local;p.isStatic=true;
  p.AddComponent<MeshFilter>().sharedMesh=mesh;var r=p.AddComponent<MeshRenderer>();r.sharedMaterial=mat;
  if(name.Contains("Inspection_Glass"))r.shadowCastingMode=ShadowCastingMode.Off;
  return p;
 }
 public static string Apply(){
  Nonce=null;Guard();string receipt=R+"/"+D+"/APPLIED-DOWNLOADED-1F-LOCKERS.json";
  if(File.Exists(receipt)||AssetDatabase.IsValidFolder(A))throw new Exception("Already applied/imported; never blindly replay");
  var auth=JObject.Parse(File.ReadAllText(R+"/"+D+"/USER-AUTHORITY.json"));if((bool?)auth["direct_apply_save_authorized"]!=true||(bool?)auth["no_text_placeholder"]!=true)throw new Exception("Exact user construction authority required");
  var placement=JObject.Parse(File.ReadAllText(R+"/"+D+"/DERIVED-DOOR-POSES.json"));
  var packet=JObject.Parse(File.ReadAllText(R+"/"+D+"/PHOTO-ADAPTED-DOWNLOADED-MESHES.json"));
  var all=((JArray)placement["modules"]).OfType<JObject>().ToArray();
  if(all.Length!=17||all.Sum(x=>((JArray)x["derived_doors"]).Count)!=63)throw new Exception("Exact current module packet required");
  // These are construction ownership checks, with no separate QA/probe stage.
  foreach(var x in all){Unchanged((JObject)x["module"]);Unchanged((JObject)x["body"]);foreach(var z in ((JArray)x["doors"]).OfType<JObject>())foreach(var e in ((JArray)z["parts"]).OfType<JObject>())Unchanged(e);}
  var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty;if(dirty&&!EditorSceneManager.SaveScene(scene))throw new Exception("Could not preserve existing dirty user edits");
  Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Replace 1F locker primitives by photo-adapted downloaded real mesh");
  bool saved=false;var changes=new JArray();var disabled=new JArray();var made=new JArray();
  try{
   Guard();Folder(A+"/Meshes");Folder(A+"/Materials");Folder(A+"/Source");
   File.Copy(R+"/"+D+"/research/26dc07440e3d40dc86a1f3fc9c01b0f3-source/source/locker.fbx",R+"/"+A+"/Source/locker.fbx");
   File.Copy(R+"/"+D+"/research/ATTRIBUTION-PRIMARY.txt",R+"/"+A+"/ATTRIBUTION.txt");
   File.Copy(R+"/"+D+"/research/PRIMARY-ASSET-RECEIPT.json",R+"/"+A+"/SOURCE-PROVENANCE.json");
   File.Copy(R+"/"+D+"/research/26dc07440e3d40dc86a1f3fc9c01b0f3-gltf/license.txt",R+"/"+A+"/Source/license.txt");
   AssetDatabase.ImportAsset(A+"/Source/locker.fbx",ImportAssetOptions.ForceSynchronousImport);
   AssetDatabase.ImportAsset(A+"/ATTRIBUTION.txt",ImportAssetOptions.ForceSynchronousImport);
   var meshes=new Dictionary<string,Mesh>();
   foreach(var prop in ((JObject)packet["meshes"]).Properties()){
    Guard();var data=(JObject)prop.Value;var mesh=new Mesh();mesh.name=(string)data["name"];
    var vertices=((JArray)data["vertices"]).Select(V).ToArray();var normals=((JArray)data["normals"]).Select(V).ToArray();
    if(prop.Name=="cabinet_shell"){Vector3 size=new Vector3(.547f,1.98f,.68f);for(int i=0;i<vertices.Length;i++){vertices[i]=new Vector3(vertices[i].x/size.x,vertices[i].y/size.y,vertices[i].z/size.z);normals[i]=Vector3.Scale(normals[i],size).normalized;}}
    mesh.vertices=vertices;mesh.triangles=((JArray)data["triangles"]).Select(x=>(int)x).ToArray();mesh.normals=normals;mesh.uv=((JArray)data["uv"]).Select(x=>new Vector2((float)x[0],(float)x[1])).ToArray();mesh.RecalculateBounds();mesh.RecalculateTangents();
    string path=A+"/Meshes/"+mesh.name+".asset";AssetDatabase.CreateAsset(mesh,path);meshes.Add(prop.Name,mesh);made.Add(path);
   }
   var cream=Mat("ActualPhoto_CreamPowderCoatedSteel","#E4E7D0",.42f,.34f);
   var casing=Mat("ActualPhoto_GrayMetalCabinetCarcass","#72766E",.65f,.40f);
   var chrome=Mat("ActualPhoto_SmallChromeKeyBarrel","#B9C1BF",.88f,.68f);
   var glass=Mat("ActualPhoto_DarkTintedInspectionGlass","#28382E99",.15f,.85f,true);
   foreach(var m in all){
    Guard();var module=Unchanged((JObject)m["module"]);var body=Unchanged((JObject)m["body"]);var mf=body.GetComponent<MeshFilter>();var mr=body.GetComponent<MeshRenderer>();
    if(mf==null||mr==null||mf.sharedMesh==null||mf.sharedMesh.name!="Cube")throw new Exception("Only inventoried primitive own cabinet body may change");
    Undo.RecordObject(mf,"Use downloaded hollow locker shell");Undo.RecordObject(mr,"Photo-matched current locker casing finish");mf.sharedMesh=meshes["cabinet_shell"];mr.sharedMaterial=casing;
    var moduleRecord=new JObject{["module_id"]=Id(module),["module_path"]=PathOf(module.transform),["body_id"]=Id(body),["position_unchanged"]=true,["source_hollow_shell_mesh"]=AssetDatabase.GetAssetPath(mf.sharedMesh),["doors"]=new JArray()};
    var doorRecords=(JArray)moduleRecord["doors"];var dimensions=new List<Tuple<float,float>>();
    foreach(var dp in ((JArray)m["derived_doors"]).OfType<JObject>()){
     Guard();var oldDoor=Get((string)dp["existing_group_id"]);if(oldDoor==null||oldDoor.transform.parent!=module.transform)throw new Exception("Exact current own door group required");
     var old=(JObject)((JArray)m["doors"]).First(x=>(string)x["id"]==(string)dp["existing_group_id"]);
     foreach(var e in ((JArray)old["parts"]).OfType<JObject>())if(((string)e["name"]).StartsWith("Cream_metal_door_")||(string)e["name"]=="Transparent_inspection_window"){
      var part=Unchanged(e);Undo.RecordObject(part,"Replace own slab/window by downloaded adapted geometry");part.SetActive(false);disabled.Add((string)e["id"]);
     }
     Vector3 center=V(dp["center_in_module"]);float height=(float)dp["size"][1];string tag=height>.9f?"large":height>.5f?"medium":"small";
     var root=new GameObject("DOWNLOADED_PHOTO_ADAPTED_"+(string)dp["name"]);root.transform.SetParent(oldDoor.transform,false);root.transform.SetPositionAndRotation(body.transform.position+body.transform.rotation*center,body.transform.rotation);root.isStatic=true;Undo.RegisterCreatedObjectUndo(root,"Add genuine downloaded locker door assembly");
     Part("Downloaded_Steel_Door_With_Actual_Inspection_Aperture",root.transform,meshes["door_"+tag],cream,Vector3.zero);
     Part("Actual_Photo_Inspection_Glass",root.transform,meshes["glass_"+tag],glass,new Vector3(.08f,.04f,.003f));
     Vector3 lockCenter=V(dp["lock_center_in_module"])-center;lockCenter.z+=.010f;
     Part("Downloaded_Detailed_Key_Barrel",root.transform,meshes["lock_barrel"],chrome,lockCenter);
     dimensions.Add(Tuple.Create(center.y,height));
     doorRecords.Add(new JObject{["door_group_id"]=Id(oldDoor),["downloaded_assembly_id"]=Id(root),["height_variant"]=tag,["source_mesh"]=AssetDatabase.GetAssetPath(meshes["door_"+tag]),["true_inspection_aperture"]=true,["inspection_height_MODEL_m"]=packet["meshes"]["door_"+tag]["window_height"],["existing_small_lock_and_number_retained"]=true});
    }
    dimensions.Sort((x,y)=>x.Item1.CompareTo(y.Item1));
    for(int i=0;i<dimensions.Count-1;i++){
     float y=(dimensions[i].Item1+dimensions[i].Item2/2+dimensions[i+1].Item1-dimensions[i+1].Item2/2)/2;
     var shelf=new GameObject("Downloaded_Internal_Compartment_Divider_"+i);shelf.transform.SetParent(module.transform,false);shelf.transform.SetPositionAndRotation(body.transform.position+body.transform.rotation*new Vector3(0,y,-.012f),body.transform.rotation);shelf.isStatic=true;Undo.RegisterCreatedObjectUndo(shelf,"Add editable physical compartment separator");shelf.AddComponent<MeshFilter>().sharedMesh=meshes["internal_shelf"];shelf.AddComponent<MeshRenderer>().sharedMaterial=cream;
    }
    changes.Add(moduleRecord);
   }
   Guard();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Actual locker Scene save failed");saved=true;Undo.CollapseUndoOperations(undo);
   var result=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["status"]="APPLIED_SAVED_DOWNLOADED_PHOTO_ADAPTED_1F_LOCKERS",["scene"]=scene.path,["scene_was_dirty"]=dirty,["scope"]=S,["thread"]=T,["grant_nonce"]=Nonce,["asset_root"]=A,["source_url"]=packet["asset_source"],["source_sha256"]=packet["source_sha256"],["license"]=packet["license"],["author_credit"]=packet["author_credit"],["cabinet_shells_replaced"]=changes.Count,["door_assemblies_replaced"]=changes.Sum(x=>((JArray)x["doors"]).Count),["disabled_old_primitive_panel_and_window_ids"]=disabled,["modules"]=changes,["shared_downloaded_derivative_mesh_assets"]=made,["adaptations"]=packet["adaptations"],["glass_booth_placement_floor_columns_native_otherstores_changed"]=false,["numeric_labels_are_markings_only"]=true,["text_represents_physical_equipment"]=false,["actual_busan_manufacturer_identity_confirmed"]=false,["field_measurement_or_exact_digital_twin_completion_claimed"]=false,["user_validation_owner"]=true,["QA_tests_probes_reopen_captures"]=false};
   File.WriteAllText(receipt,result.ToString(Formatting.Indented));File.WriteAllText(R+"/.state/CURRENT-1F-DOWNLOADED-LOCKERS-20261019.json",result.ToString(Formatting.Indented));
   return new JObject{["status"]=result["status"],["cabinet_shells"]=result["cabinet_shells_replaced"],["doors"]=result["door_assemblies_replaced"],["receipt"]=D+"/APPLIED-DOWNLOADED-1F-LOCKERS.json"}.ToString(Formatting.None);
  }catch(Exception ex){if(!saved)Undo.RevertAllDownToGroup(undo);File.WriteAllText(R+"/"+D+"/APPLY-ERROR.json",new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["saved"]=saved,["error"]=ex.ToString(),["retained_new_assets"]=made}.ToString());throw;}
 }
}
