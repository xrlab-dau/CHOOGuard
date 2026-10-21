using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;
using UnityEngine;using UnityEngine.Rendering;using UnityEngine.SceneManagement;using UnityEditor;using UnityEditor.SceneManagement;
using Newtonsoft.Json;using Newtonsoft.Json.Linq;

public static class SouthwestJointReturnR30
{
 const string Root="/Users/um-yunsang/CHOOGuard";
 const string StoreKey="southwest-dain",Thread="classic:/root/r29_southwest_field_fitout",Scope="C180_R30_SW_JOINT_RETURN_APPLY_001";
 const string Packet="/Users/um-yunsang/CHOOGuard/.state/ACTIVE-ALL2F-COMPLETION-R26/store-original-r28/southwest-dain/joint-constructor-r30/FROZEN-DAIN-RETURN-SERVICE-PACKET-R30.json",PacketSHA="6b65be790b9c99f490ad96d26b86197dbab4b20700bd45b7d91eef416d703084";
 const string Moves="/Users/um-yunsang/CHOOGuard/.state/ACTIVE-ALL2F-COMPLETION-R26/store-original-r28/southwest-dain/joint-constructor-r30/FROZEN-EXACT-12-TRANSFORMS-R30.json",MovesSHA="1512dd83343631ce2906eeb1b4b99dab9b3f8be0fed8d80a0437687a32565dd5";
 const string ParentPath="FPSWorld/Architecture/2F/MAIN2F_R25_SOURCE_BOUND_SPECIALIST_SHOPS";
 static readonly Dictionary<string,Material> Materials=new Dictionary<string,Material>();
 static readonly Dictionary<string,Mesh> PrimitiveMeshes=new Dictionary<string,Mesh>();
 static readonly Dictionary<string,Mesh> OwnMeshLibrary=new Dictionary<string,Mesh>();
 static readonly List<string> CreatedAssets=new List<string>();
 static JArray V(Vector3 v){return new JArray(v.x,v.y,v.z);}
 static string Sha(string path){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
 static void Guard(){var a=JObject.Parse(File.ReadAllText(Root+"/.state/WRITER-AUTHORITY.json"));
  if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["sole_writer_thread"]!=Thread||(string)a["scope"]!=Scope)throw new Exception("ExactR25storeauthorrequired");
  var g=JObject.Parse(File.ReadAllText(Root+"/"+(string)a["delegated_author_lease"]));if((string)g["grant_nonce"]!=(string)a["scene_worker_grant_nonce"])throw new Exception("ExactFIFOgrantnonce required");
 }
 static Transform Ensure(string path){Transform current=null;foreach(string n in path.Split('/')){Transform next=current==null?SceneManager.GetActiveScene().GetRootGameObjects().Select(x=>x.transform).FirstOrDefault(x=>x.name==n):current.Find(n);if(next==null){var go=new GameObject(n);Undo.RegisterCreatedObjectUndo(go,"R25ownparent");go.transform.SetParent(current,false);next=go.transform;}current=next;}return current;}
 static void EnsureAssetDir(string path){string[] parts=path.Split('/');string q=parts[0];for(int i=1;i<parts.Length;i++){string child=q+"/"+parts[i];if(!AssetDatabase.IsValidFolder(child))AssetDatabase.CreateFolder(q,parts[i]);q=child;}}
 static Color ColorOf(JToken x){return x==null?Color.white:new Color((float)x[0],(float)x[1],(float)x[2],x.Count()>3?(float)x[3]:1);}
 static Vector3 Position(JToken spec,Vector2 origin){var q=spec["q"];if(q==null)return Vector3.zero;return new Vector3((float)q[0]-origin.x,(float?)spec["y"]??0,(float)q[1]-origin.y);}
 static Vector3 Size(JToken x){return new Vector3((float)x[0],(float)x[1],(float)x[2]);}
 static Material MaterialFor(string key){if(!Materials.ContainsKey(key))throw new Exception("Undefinedownmaterial:"+key);return Materials[key];}
 static Mesh Primitive(string kind){if(PrimitiveMeshes.ContainsKey(kind))return PrimitiveMeshes[kind];var pt=kind=="cylinder"?PrimitiveType.Cylinder:kind=="sphere"?PrimitiveType.Sphere:PrimitiveType.Cube;var tmp=GameObject.CreatePrimitive(pt);var mesh=tmp.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(tmp);PrimitiveMeshes[kind]=mesh;return mesh;}
 static void BuildMaterials(JObject p,string folder){foreach(var prop in ((JObject)p["materials"]).Properties()){var a=prop.Value;string path=folder+"/MAT_"+prop.Name+".mat";if(File.Exists(Root+"/"+path))throw new Exception("Ownmaterialreplay:"+path);var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new Exception("CurrentURPLitrequired");var m=new Material(shader){name="R25_"+StoreKey+"_"+prop.Name,enableInstancing=true};
   m.SetColor("_BaseColor",ColorOf(a["color"]));if((bool?)a["alpha_clip"]==true){m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",.5f);m.EnableKeyword("_ALPHATEST_ON");}if((bool?)a["double_sided"]==true)m.SetFloat("_Cull",0);m.SetFloat("_Metallic",(float?)a["metallic"]??0);m.SetFloat("_Smoothness",(float?)a["smoothness"]??.35f);
   if((bool?)a["transparent"]==true){m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);m.SetOverrideTag("RenderType","Transparent");m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;}
   if(a["emission"]!=null){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",ColorOf(a["emission"]));m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;}
   if(a["texture_asset"]!=null){var t=AssetDatabase.LoadAssetAtPath<Texture>((string)a["texture_asset"]);if(t!=null)m.SetTexture("_BaseMap",t);}
   if(a["normal_asset"]!=null){var normal=AssetDatabase.LoadAssetAtPath<Texture>((string)a["normal_asset"]);if(normal==null)throw new Exception("OwnPBRnormalmissing");m.SetTexture("_BumpMap",normal);m.SetFloat("_BumpScale",1f);m.EnableKeyword("_NORMALMAP");}
   if(a["metallic_gloss_asset"]!=null){var mask=AssetDatabase.LoadAssetAtPath<Texture>((string)a["metallic_gloss_asset"]);if(mask==null)throw new Exception("OwnPBRsmoothnessmissing");m.SetTexture("_MetallicGlossMap",mask);m.SetFloat("_Smoothness",1f);m.EnableKeyword("_METALLICSPECGLOSSMAP");}
   AssetDatabase.CreateAsset(m,path);CreatedAssets.Add(path);Materials[prop.Name]=m;
  }}

 static void PrepareImports(JObject p,string folder){
  foreach(var spec in (JArray)(p["source_imports"]??new JArray())){Guard();string source=(string)spec["source"],rel=(string)spec["destination"];
   if(!(source.StartsWith(Root+"/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/"+StoreKey+"/")||source.StartsWith(Root+"/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/free-assets/"))||rel.Contains("..")||Path.IsPathRooted(rel))throw new Exception("Ownimportboundaryrequired");
   if(Sha(source)!=(string)spec["sha256"])throw new Exception("Frozenfreeassetsourcemismatch");string path=folder+"/"+rel;EnsureAssetDir(Path.GetDirectoryName(path).Replace('\\','/'));
   if(File.Exists(Root+"/"+path))throw new Exception("Ownfreeassetreplay");File.Copy(source,Root+"/"+path);CreatedAssets.Add(path);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
   var ti=AssetImporter.GetAtPath(path) as TextureImporter;if(ti!=null){ti.maxTextureSize=1024;ti.mipmapEnabled=true;ti.textureCompression=TextureImporterCompression.Compressed;
    if(rel.Contains("normal.")||((JObject)p["materials"]).Properties().Any(x=>(string)x.Value["normal_asset"]==path)){ti.textureType=TextureImporterType.NormalMap;ti.sRGBTexture=false;}
    if(rel.Contains("metallic_smoothness")||((JObject)p["materials"]).Properties().Any(x=>(string)x.Value["metallic_gloss_asset"]==path)){ti.textureType=TextureImporterType.Default;ti.sRGBTexture=false;ti.alphaSource=TextureImporterAlphaSource.FromInput;ti.alphaIsTransparency=false;}
    ti.SaveAndReimport();}
   var mi=AssetImporter.GetAtPath(path) as ModelImporter;if(mi!=null){mi.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;mi.materialLocation=ModelImporterMaterialLocation.InPrefab;mi.importCameras=false;mi.importLights=false;mi.SaveAndReimport();}
  }
 }
 static Mesh LibraryMesh(JObject p,string folder,string key){if(OwnMeshLibrary.ContainsKey(key))return OwnMeshLibrary[key];var spec=p["mesh_library"][key];if(spec==null)throw new Exception("Missingownmeshprototype:"+key);var m=new Mesh{name="R30_"+StoreKey+"_"+Clean(key)};if(((JArray)spec["vertices"]).Count>65000)m.indexFormat=IndexFormat.UInt32;m.vertices=spec["vertices"].Select(v=>new Vector3((float)v[0],(float)v[1],(float)v[2])).ToArray();m.triangles=spec["triangles"].Select(v=>(int)v).ToArray();m.RecalculateNormals();m.RecalculateBounds();string path=folder+"/MESH_SHARED_"+Clean(key)+".asset";AssetDatabase.CreateAsset(m,path);CreatedAssets.Add(path);OwnMeshLibrary[key]=m;return m;}
 static Mesh FreeReferencedMesh(JToken spec,string folder){string key=(string)spec["mesh_asset"]+"|"+spec["fit_size"].ToString(Formatting.None);if(OwnMeshLibrary.ContainsKey(key))return OwnMeshLibrary[key];var original=AssetDatabase.LoadAssetAtPath<Mesh>((string)spec["mesh_asset"]);if(original==null)throw new Exception("MissingexistingCC0mesh:"+key);var m=UnityEngine.Object.Instantiate(original);m.name="R25_"+StoreKey+"_CC0_"+Path.GetFileNameWithoutExtension(key);var bounds=m.bounds;var want=Size(spec["fit_size"]);var factor=new Vector3(want.x/Mathf.Max(.001f,bounds.size.x),want.y/Mathf.Max(.001f,bounds.size.y),want.z/Mathf.Max(.001f,bounds.size.z));var pivot=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);m.vertices=m.vertices.Select(v=>Vector3.Scale(v-pivot,factor)).ToArray();m.RecalculateBounds();string path=folder+"/MESH_FREE_"+Clean(Path.GetFileNameWithoutExtension((string)spec["mesh_asset"]))+"_"+CreatedAssets.Count+".asset";AssetDatabase.CreateAsset(m,path);CreatedAssets.Add(path);OwnMeshLibrary[key]=m;return m;}

 static string Clean(string s){return new string(s.Select(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'?c:'_').ToArray());}
 static string PathOf(Transform t){return t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;}
 static Transform ExactTarget(JToken spec,string requiredParent){GlobalObjectId id;if(!GlobalObjectId.TryParse((string)spec["current_object_id"],out id))throw new Exception("ExactCurrentObjectIDRequired");var o=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);var go=o as GameObject;var t=go!=null?go.transform:o as Transform;if(t==null||!PathOf(t).StartsWith(requiredParent+"/",StringComparison.Ordinal))throw new Exception("ExactScopedEquipmentOwnershipRequired:"+(string)spec["current_object_id"]);var p=t.position;float qx=-.34290633335f*p.x-.93936960061f*p.z,qy=.93936960061f*p.x-.34290633335f*p.z;var old=spec["old_q"];if(Mathf.Abs(qx-(float)old[0])>.025f||Mathf.Abs(qy-(float)old[1])>.025f)throw new Exception("SourceBoundOldEquipmentPoseChanged:"+t.name);return t;}
 public static string Apply(){Guard();if(Sha((Path.IsPathRooted(Packet)?Packet:Root+"/"+Packet))!=PacketSHA)throw new Exception("FrozenstorepacketSHA mismatch");var p=JObject.Parse(File.ReadAllText((Path.IsPathRooted(Packet)?Packet:Root+"/"+Packet)));if((string)p["store_key"]!=StoreKey)throw new Exception("Packetstoreownership mismatch");
  var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("StoppedFpsStation required");
  string folder="Assets/ChooGuard/Art/StationInterior/R30_SW_Dain_JointReturn/"+StoreKey;string name="R29_"+Clean(StoreKey)+"_SOURCE_OBSERVED_MODEL";
  var existing=GameObject.Find(ParentPath+"/"+name);if(existing==null)throw new Exception("ExistingSavedDain42RootRequired");
  if(existing.transform.Find("R30_RETURN_SOURCE_SUPPLEMENT")!=null)throw new Exception("NoJointSupplementReplay");
  if(Sha(Moves)!=MovesSHA)throw new Exception("ExactFrozen12TransformsRequired");var movements=JObject.Parse(File.ReadAllText(Moves));
  var jointAuthority=JObject.Parse(File.ReadAllText((string)movements["authority"]));if((string)jointAuthority["supplement_packet_sha256"]!=PacketSHA || (int)jointAuthority["new_own_Dain_parts"]!=230)throw new Exception("CurrentRootJointAuthorityRequired");
  if(((JArray)p["objects"]).Count!=230)throw new Exception("Exact230NewPartsRequired");
  var targets=new List<KeyValuePair<JToken,Transform>>();foreach(var x in (JArray)movements["Flowers"]){var t=ExactTarget(x,ParentPath+"/R25_flowers_SOURCE_BOUND_MODEL");targets.Add(new KeyValuePair<JToken,Transform>(x,t));}
  var posTarget=ExactTarget(movements["POS"],ParentPath+"/"+name);targets.Add(new KeyValuePair<JToken,Transform>(movements["POS"],posTarget));
  var moved=new JArray();
  var originToken=p["origin_q"];var origin=new Vector2((float)originToken[0],(float)originToken[1]);float floor=(float?)p["floor_world_y"]??7f;
  bool saved=false;string before=Sha(Root+"/"+scene.path);Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("R25sourceboundstore:"+StoreKey);var applied=new JArray();var createdObjects=new List<GameObject>();
  try{Guard();EnsureAssetDir(folder);PrepareImports(p,folder);BuildMaterials(p,folder);
   foreach(var pair in targets){Guard();var spec=pair.Key;var t=pair.Value;Vector3 beforePosition=t.position;Quaternion beforeRotation=t.rotation;Undo.RecordObject(t,"R30ExactSourceBoundEquipmentCorrection");
    if(spec["delta_packet_q"]!=null){var delta=spec["delta_packet_q"];float dx=(float)delta[0],dy=(float)delta[1];t.position+=new Vector3(-.34290633335f*dx+.93936960061f*dy,0,-.93936960061f*dx-.34290633335f*dy);float rotation=(float)spec["proposed_yaw"]-(float)spec["old_yaw"];if(Mathf.Abs(rotation)>.001f)t.rotation=Quaternion.AngleAxis(rotation,Vector3.up)*t.rotation;}
    else{var q=spec["proposed_q"];float x=(float)q[0],y=(float)q[1];t.position=new Vector3(-.34290633335f*x+.93936960061f*y,beforePosition.y,-.93936960061f*x-.34290633335f*y);t.rotation=existing.transform.rotation*Quaternion.Euler(0,(float)spec["proposed_yaw"],0);}
    EditorUtility.SetDirty(t);moved.Add(new JObject{["object_id"]=spec["current_object_id"].DeepClone(),["name"]=t.name,["world_position_before"]=V(beforePosition),["world_position_after"]=V(t.position),["world_rotation_before"]=V(beforeRotation.eulerAngles),["world_rotation_after"]=V(t.rotation.eulerAngles),["local_scale_unchanged"]=V(t.localScale),["source_mesh_material_children_preserved"]=true,["glyph_children_follow_actual_wrapper"]=true});
   }
   var own=existing;var supplement=new GameObject("R30_RETURN_SOURCE_SUPPLEMENT");Undo.RegisterCreatedObjectUndo(supplement,"R30SourceReturnSupplement");supplement.transform.SetParent(own.transform,false);
   var groups=new Dictionary<string,Transform>();groups[""]=supplement.transform;
   foreach(var spec in (JArray)p["objects"]){Guard();string kind=(string)spec["kind"],id=Clean((string)spec["id"]);string group=(string)spec["group"]??"";
    if(!groups.ContainsKey(group)){var groupObject=new GameObject(group);Undo.RegisterCreatedObjectUndo(groupObject,"R25editablegroup");groupObject.transform.SetParent(supplement.transform,false);groups[group]=groupObject.transform;}
    GameObject go;
    if(kind=="asset"){
     string asset=(string)spec["asset"];var original=AssetDatabase.LoadAssetAtPath<GameObject>(asset);if(original==null)throw new Exception("Missingfreeasset:"+asset);
     go=new GameObject(id);Undo.RegisterCreatedObjectUndo(go,"R25freeassetwrapper");go.transform.SetParent(groups[group],false);
     var model=(GameObject)PrefabUtility.InstantiatePrefab(original);if(model==null)model=UnityEngine.Object.Instantiate(original);Undo.RegisterCreatedObjectUndo(model,"R25freeassetinstance");model.transform.SetParent(go.transform,false);model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one;model.transform.localPosition=Vector3.zero;foreach(var c in model.GetComponentsInChildren<Component>(true)){if(c==null)continue;string n=c.GetType().Name;if(c is Light || c is TextMesh || n=="TextMeshPro" || n=="TextMeshProUGUI")UnityEngine.Object.DestroyImmediate(c);}
     var bounds=new Bounds();bool first=true;foreach(var mf in model.GetComponentsInChildren<MeshFilter>()){if(mf.sharedMesh==null)continue;var bb=mf.sharedMesh.bounds;for(int c=0;c<8;c++){var local=bb.center+Vector3.Scale(bb.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1));var v=go.transform.InverseTransformPoint(mf.transform.TransformPoint(local));if(first){bounds=new Bounds(v,Vector3.zero);first=false;}else bounds.Encapsulate(v);}}
     if(!first){Vector3 factor=Vector3.one;if(spec["fit_size"]!=null){Vector3 want=Size(spec["fit_size"]);factor=new Vector3(want.x/Mathf.Max(bounds.size.x,.001f),want.y/Mathf.Max(bounds.size.y,.001f),want.z/Mathf.Max(bounds.size.z,.001f));}model.transform.localScale=factor;model.transform.localPosition=-Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),factor);}
     if(spec["material"]!=null)foreach(var rr in model.GetComponentsInChildren<Renderer>())rr.sharedMaterials=rr.sharedMaterials.Select(_=>MaterialFor((string)spec["material"])).ToArray();if(spec["material_map"] is JObject map)foreach(var rr in model.GetComponentsInChildren<Renderer>()){rr.sharedMaterials=rr.sharedMaterials.Select(mm=>{var mk=map[rr.name]??(mm==null?null:map[mm.name])??map["*"];return mk==null?mm:MaterialFor((string)mk);}).ToArray();}if((bool?)spec["collider"]!=false&&model.GetComponentsInChildren<Collider>().Length==0&&!first){var cc=go.AddComponent<BoxCollider>();Vector3 fitted=Vector3.Scale(bounds.size,model.transform.localScale);cc.center=new Vector3(0,fitted.y*.5f,0);cc.size=fitted;}
    }else{
     go=new GameObject(id);Undo.RegisterCreatedObjectUndo(go,"R25sourcepart");go.transform.SetParent(groups[group],false);
     if(kind=="text" || kind=="light")throw new Exception("No scene Light or Text components permitted");
     {
      Mesh mesh;
      if(kind=="mesh"){
       mesh=new Mesh{name="R25_"+StoreKey+"_"+id};if(((JArray)spec["vertices"]).Count>65000)mesh.indexFormat=IndexFormat.UInt32;
       mesh.vertices=spec["vertices"].Select(v=>new Vector3((float)v[0],(float)v[1],(float)v[2])).ToArray();mesh.triangles=spec["triangles"].Select(v=>(int)v).ToArray();if(spec["uv"]!=null)mesh.uv=spec["uv"].Select(v=>new Vector2((float)v[0],(float)v[1])).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();string ap=folder+"/MESH_"+id+".asset";AssetDatabase.CreateAsset(mesh,ap);CreatedAssets.Add(ap);
      }else if(kind=="library_mesh")mesh=LibraryMesh(p,folder,(string)spec["mesh_key"]);else if(kind=="mesh_ref")mesh=FreeReferencedMesh(spec,folder);else mesh=Primitive(kind);
      go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=MaterialFor((string)spec["material"]);renderer.shadowCastingMode=(bool?)spec["cast_shadows"]==false?ShadowCastingMode.Off:ShadowCastingMode.On;renderer.receiveShadows=true;
      if((bool?)spec["collider"]!=false){if(kind=="mesh"){go.AddComponent<MeshCollider>().sharedMesh=mesh;}else if(kind=="cylinder"){var cc=go.AddComponent<CapsuleCollider>();cc.radius=.5f;cc.height=2f;}else if(kind=="sphere"){go.AddComponent<SphereCollider>();}else go.AddComponent<BoxCollider>();}
     }
    }
    go.transform.localPosition=Position(spec,origin);float yaw=(float?)spec["yaw"]??0;var angles=spec["euler"];go.transform.localRotation=angles==null?Quaternion.Euler(0,yaw,0):Quaternion.Euler((float)angles[0],(float)angles[1],(float)angles[2]);
    if(spec["size"]!=null&&kind!="text")go.transform.localScale=Vector3.Scale(go.transform.localScale,Size(spec["size"]));
    if(kind=="edge"){
     var a=spec["a"];var b=spec["b"];float ax=(float)a[0],az=(float)a[1],bx=(float)b[0],bz=(float)b[1];float height=(float)spec["height"],bottom=(float?)spec["bottom"]??0;go.transform.localPosition=new Vector3((ax+bx)/2-origin.x,bottom+height/2,(az+bz)/2-origin.y);go.transform.localRotation=Quaternion.Euler(0,-Mathf.Atan2(bz-az,bx-ax)*Mathf.Rad2Deg,0);go.transform.localScale=new Vector3(Vector2.Distance(new Vector2(ax,az),new Vector2(bx,bz)),height,(float)spec["thickness"]);
    }
    createdObjects.Add(go);applied.Add(new JObject{["id"]=id,["kind"]=kind,["source_ids"]=spec["source_ids"]==null?new JArray():spec["source_ids"].DeepClone(),["geometry_qualification"]=(string)spec["qualification"]??"PHOTO_REFERENCED_MODEL_NOT_FIELD_MEASUREMENT"});
   }
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Guard();if(!EditorSceneManager.SaveScene(scene))throw new Exception("Ownstoresavefailed");saved=true;for(int i=0;i<createdObjects.Count;i++)applied[i]["object_id"]=GlobalObjectId.GetGlobalObjectIdSlow(createdObjects[i]).ToString();Undo.CollapseUndoOperations(undo);
   var receipt=new JObject{["status"]="JOINT_SOURCE_RETURN_FITOUT_APPLIED_AND_SAVED",["utc"]=DateTime.UtcNow.ToString("o"),["store_key"]=StoreKey,["scope"]=Scope,["author"]=Thread,["root_path"]=ParentPath+"/"+name,["packet"]=Packet,["packet_sha256"]=PacketSHA,["scene_sha256_before"]=before,["scene_sha256_after"]=Sha(Root+"/"+scene.path),["source_packet"]=p["source_packet"]==null?null:p["source_packet"].DeepClone(),["objects"]=applied,["object_count"]=applied.Count,["existing_transforms_changed"]=moved,["exact_existing_transform_count"]=moved.Count,["joint_authority"]=movements["authority"].DeepClone(),["supplement_root"]=ParentPath+"/"+name+"/R30_RETURN_SOURCE_SUPPLEMENT",["created_asset_paths"]=new JArray(CreatedAssets),["all2F_complete"]=false,["99percent_achieved"]=false,["source_epoch_qualification"]="ObservedDain2022/2024, guidephotographed2026; notOctober2026survey",["new_shell_or_ceiling"]=false,["all_displayed_letters_closed_mesh"]=true,["source_placement_basis"]=p["source_packet"].DeepClone(),["pastry_counter_status"]="SOURCE_OBSERVED_CASE_STOCK_COUNTER_CASHIER_AND_RETURN_HEADER_APPLIED_QUALIFIED_MODEL",["QA_Play_tests_reopen_capture"]=false};
   string output=".state/ACTIVE-ALL2F-COMPLETION-R26/store-original-r28/southwest-dain/joint-constructor-r30/APPLIED-JOINT-SOURCE-RETURN-R30.json";File.WriteAllText(Root+"/"+output,receipt.ToString());return new JObject{["status"]=receipt["status"],["store_key"]=StoreKey,["object_count"]=applied.Count,["receipt"]=output}.ToString();
  }catch{if(!saved){Undo.RevertAllDownToGroup(undo);foreach(string ap in CreatedAssets.AsEnumerable().Reverse())AssetDatabase.DeleteAsset(ap);}throw;}
 }
}
