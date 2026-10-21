using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;
using UnityEngine;using UnityEngine.Rendering;using UnityEngine.SceneManagement;using UnityEditor;using UnityEditor.SceneManagement;
using TMPro;using Newtonsoft.Json;using Newtonsoft.Json.Linq;

public static class StoreBuilderR25
{
 const string Root="/Users/um-yunsang/CHOOGuard";
 const string StoreKey="storyway-southwest",Thread="classic:/root/r25_storyway_southwest",Scope="C180_R25_STORYWAY_SOUTHWEST_APPLY_002";
 const string Packet="/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/storyway-southwest/STORE-PACKET-R25.json",PacketSHA="d80fc595637d4488b417c32b0e6efd26a8676c3336ad32c2c079306db6dd28d8";
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
   m.SetColor("_BaseColor",ColorOf(a["color"]));m.SetFloat("_Metallic",(float?)a["metallic"]??0);m.SetFloat("_Smoothness",(float?)a["smoothness"]??.35f);
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
 static Mesh LibraryMesh(JObject p,string folder,string key){if(OwnMeshLibrary.ContainsKey(key))return OwnMeshLibrary[key];var spec=p["mesh_library"][key];if(spec==null)throw new Exception("Missingownmeshprototype:"+key);var m=new Mesh{name="R25_"+StoreKey+"_"+Clean(key)};m.vertices=spec["vertices"].Select(v=>new Vector3((float)v[0],(float)v[1],(float)v[2])).ToArray();m.triangles=spec["triangles"].Select(v=>(int)v).ToArray();m.RecalculateNormals();m.RecalculateBounds();string path=folder+"/MESH_SHARED_"+Clean(key)+".asset";AssetDatabase.CreateAsset(m,path);CreatedAssets.Add(path);OwnMeshLibrary[key]=m;return m;}
 static Mesh FreeReferencedMesh(JToken spec,string folder){string key=(string)spec["mesh_asset"]+"|"+spec["fit_size"].ToString(Formatting.None);if(OwnMeshLibrary.ContainsKey(key))return OwnMeshLibrary[key];var original=AssetDatabase.LoadAssetAtPath<Mesh>((string)spec["mesh_asset"]);if(original==null)throw new Exception("MissingexistingCC0mesh:"+key);var m=UnityEngine.Object.Instantiate(original);m.name="R25_"+StoreKey+"_CC0_"+Path.GetFileNameWithoutExtension(key);var bounds=m.bounds;var want=Size(spec["fit_size"]);var factor=new Vector3(want.x/Mathf.Max(.001f,bounds.size.x),want.y/Mathf.Max(.001f,bounds.size.y),want.z/Mathf.Max(.001f,bounds.size.z));var pivot=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);m.vertices=m.vertices.Select(v=>Vector3.Scale(v-pivot,factor)).ToArray();m.RecalculateBounds();string path=folder+"/MESH_FREE_"+Clean(Path.GetFileNameWithoutExtension((string)spec["mesh_asset"]))+"_"+CreatedAssets.Count+".asset";AssetDatabase.CreateAsset(m,path);CreatedAssets.Add(path);OwnMeshLibrary[key]=m;return m;}

 static string Clean(string s){return new string(s.Select(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'?c:'_').ToArray());}

 static JObject OwnApertureReceipt;
 static void BuildOwnSourceEntrance(string folder,JObject packet){
  Guard();const string expectedPath="Assets/ChooGuard/Art/StationInterior/PhotoLocationTwinR15_Batch04_20261019/R15_WALL_001.asset",expectedSHA="d3b375d70d4b9b0491b6c07aa1fde87a9ce5134074eec1d1886f5f5e6c35fab7";
  GlobalObjectId gid;if(!GlobalObjectId.TryParse("GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-718422858-0",out gid))throw new Exception("Own generated front identity parse");
  var go=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as GameObject;if(go==null||go.name!="R11-WALL-FOOTPRINT-015_P1_1")throw new Exception("Exact own generated storefront required");
  var mf=go.GetComponent<MeshFilter>();var original=mf==null?null:mf.sharedMesh;
  if(original==null||AssetDatabase.GetAssetPath(original)!=expectedPath||Sha(Root+"/"+expectedPath)!=expectedSHA||original.subMeshCount!=8)throw new Exception("Current generated source identity changed");
  var old=original.vertices;var ix=original.GetTriangles(4);if(ix.Length!=36)throw new Exception("Current generated pane source is not the identified box");
  float xmin=float.PositiveInfinity,xmax=float.NegativeInfinity,zmin=float.PositiveInfinity,zmax=float.NegativeInfinity,ymin=float.PositiveInfinity,ymax=float.NegativeInfinity;
  foreach(var i in ix){var w=go.transform.TransformPoint(old[i]);float x=-.34290633335f*w.x-.93936960061f*w.z,z=.93936960061f*w.x-.34290633335f*w.z;xmin=Mathf.Min(xmin,x);xmax=Mathf.Max(xmax,x);zmin=Mathf.Min(zmin,z);zmax=Mathf.Max(zmax,z);ymin=Mathf.Min(ymin,w.y);ymax=Mathf.Max(ymax,w.y);}
  if(Mathf.Abs(zmin+24.659073f)>.002f||Mathf.Abs(zmax+24.647072f)>.002f||Mathf.Abs(ymin-7.1f)>.002f||Mathf.Abs(ymax-9.62f)>.002f)throw new Exception("Identified generated glass face changed");
  // Exact current mullion inside faces. The100mm original low threshold/frame is kept.
  const float left=-48.26294f,right=-46.96796f,head=9.45f;
  var mesh=UnityEngine.Object.Instantiate(original);mesh.name="R25_SW_OWN_GENERATED_GLASS_SOURCE_ENTRANCE";
  var verts=new List<Vector3>(original.vertices);var normals=new List<Vector3>(original.normals);var uv=new List<Vector2>(original.uv);while(normals.Count<verts.Count)normals.Add(Vector3.up);while(uv.Count<verts.Count)uv.Add(Vector2.zero);var ids=new List<int>();
  Action<float,float,float,float> pane=(xa,xb,ya,yb)=>{var cube=Primitive("box");int start=verts.Count;var vv=cube.vertices;var nn=cube.normals;var tt=cube.uv;for(int i=0;i<vv.Length;i++){float qx=(xa+xb)*.5f+vv[i].x*(xb-xa),qy=(zmin+zmax)*.5f+vv[i].z*(zmax-zmin),wy=(ya+yb)*.5f+vv[i].y*(yb-ya);var w=new Vector3(-.34290633335f*qx+.93936960061f*qy,wy,-.93936960061f*qx-.34290633335f*qy);verts.Add(go.transform.InverseTransformPoint(w));normals.Add(go.transform.InverseTransformDirection(new Vector3(-.34290633335f*nn[i].x+.93936960061f*nn[i].z,nn[i].y,-.93936960061f*nn[i].x-.34290633335f*nn[i].z)).normalized);uv.Add(tt[i]);}foreach(int i in cube.triangles)ids.Add(start+i);};
  pane(xmin,left,ymin,ymax);pane(right,xmax,ymin,ymax);pane(left,right,head,ymax);
  mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(ids,4);mesh.RecalculateBounds();string path=folder+"/MESH_OWN_GENERATED_FRONTGLASS_OPEN_ENTRY.asset";AssetDatabase.CreateAsset(mesh,path);CreatedAssets.Add(path);
  var colliders=go.GetComponents<MeshCollider>();if(colliders.Length!=1||colliders[0].sharedMesh==null)throw new Exception("Exact generated current collision component required");
  var cp=(JObject)packet["generated_collider_aperture"];var originalCollision=colliders[0].sharedMesh;string collisionPath=(string)cp["source_mesh"];
  if(AssetDatabase.GetAssetPath(originalCollision)!=collisionPath||Sha(Root+"/"+collisionPath)!=(string)cp["source_sha256"]||originalCollision.vertexCount!=(int)cp["original_vertex_count"])throw new Exception("Exact separate generated collision source changed");
  var collisionMesh=UnityEngine.Object.Instantiate(originalCollision);collisionMesh.name="R25_SW_OWN_GENERATED_COLLISION_LOCAL_ENTRANCE";var cv=new List<Vector3>(originalCollision.vertices);cv.AddRange(cp["appended_vertices"].Select(v=>new Vector3((float)v[0],(float)v[1],(float)v[2])));collisionMesh.SetVertices(cv);int sub=0;foreach(var tr in cp["submesh_triangles"])collisionMesh.SetTriangles(tr.Select(v=>(int)v).ToArray(),sub++);collisionMesh.RecalculateNormals();collisionMesh.RecalculateBounds();string collisionNew=folder+"/MESH_OWN_GENERATED_COLLISION_OPEN_ENTRY.asset";AssetDatabase.CreateAsset(collisionMesh,collisionNew);CreatedAssets.Add(collisionNew);
  Undo.RecordObject(mf,"Own source visible storefront entrance");Undo.RecordObject(colliders[0],"Own source visible entrance collider");mf.sharedMesh=mesh;colliders[0].sharedMesh=null;colliders[0].sharedMesh=collisionMesh;EditorUtility.SetDirty(mf);EditorUtility.SetDirty(colliders[0]);
  OwnApertureReceipt=new JObject{["authority"]="OWN-GENERATED-STOREFRONT-APERTURE-AUTHORITY-002.json",["object_id"]=gid.ToString(),["original_mesh"]=expectedPath,["original_mesh_sha256"]=expectedSHA,["own_derivative_mesh"]=path,["changed_submesh"]=4,["aperture_qx_min"]=left,["aperture_qx_max"]=right,["aperture_worldY_min"]=ymin,["aperture_worldY_max"]=head,["original_low_frame_top_worldY"]=7.1f,["other_submeshes_frames_native_floor_and_TRS_preserved"]=true,["matching_collider_saved"]=true,["original_separate_collider_source"]=collisionPath,["independent_collider_derivative"]=collisionNew,["collider_original_sha256"]=cp["source_sha256"],["collision_outside_entry_preserved"]=true,["metric_status"]="CURRENT_MULLION_FACE_BOUNDED_PHOTO_PROPORTION_MODEL_NOT_SURVEY"};
 }

 public static string Apply(){Guard();if(Sha((Path.IsPathRooted(Packet)?Packet:Root+"/"+Packet))!=PacketSHA)throw new Exception("FrozenstorepacketSHA mismatch");var p=JObject.Parse(File.ReadAllText((Path.IsPathRooted(Packet)?Packet:Root+"/"+Packet)));if((string)p["store_key"]!=StoreKey)throw new Exception("Packetstoreownership mismatch");
  var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("StoppedFpsStation required");
  string folder="Assets/ChooGuard/Art/StationInterior/R25_Shops/"+StoreKey;string name="R25_"+Clean(StoreKey)+"_SOURCE_BOUND_MODEL";
  if(GameObject.Find(ParentPath+"/"+name)!=null)throw new Exception("Ownstorealreadyexists; no automaticdelete/replay");
  var originToken=p["origin_q"];var origin=new Vector2((float)originToken[0],(float)originToken[1]);float floor=(float?)p["floor_world_y"]??7f;
  bool saved=false;string before=Sha(Root+"/"+scene.path);Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("R25sourceboundstore:"+StoreKey);var applied=new JArray();var createdObjects=new List<GameObject>();
  try{Guard();EnsureAssetDir(folder);PrepareImports(p,folder);BuildMaterials(p,folder);var parent=Ensure(ParentPath);var own=new GameObject(name);Undo.RegisterCreatedObjectUndo(own,"R25ownstore");own.transform.SetParent(parent,false);
   own.transform.position=new Vector3(-.34290633335f*origin.x+.93936960061f*origin.y,floor,-.93936960061f*origin.x-.34290633335f*origin.y);
   own.transform.rotation=Quaternion.LookRotation(new Vector3(.93936960061f,0,-.34290633335f),Vector3.up);own.transform.localScale=Vector3.one;
   var groups=new Dictionary<string,Transform>();groups[""]=own.transform;TMP_FontAsset localFont=null;
   foreach(var spec in (JArray)p["objects"]){Guard();string kind=(string)spec["kind"],id=Clean((string)spec["id"]);string group=(string)spec["group"]??"";
    if(!groups.ContainsKey(group)){var groupObject=new GameObject(group);Undo.RegisterCreatedObjectUndo(groupObject,"R25editablegroup");groupObject.transform.SetParent(own.transform,false);groups[group]=groupObject.transform;}
    GameObject go;
    if(kind=="asset"){
     string asset=(string)spec["asset"];var original=AssetDatabase.LoadAssetAtPath<GameObject>(asset);if(original==null)throw new Exception("Missingfreeasset:"+asset);
     go=new GameObject(id);Undo.RegisterCreatedObjectUndo(go,"R25freeassetwrapper");go.transform.SetParent(groups[group],false);
     var model=(GameObject)PrefabUtility.InstantiatePrefab(original);if(model==null)model=UnityEngine.Object.Instantiate(original);Undo.RegisterCreatedObjectUndo(model,"R25freeassetinstance");model.transform.SetParent(go.transform,false);model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one;model.transform.localPosition=Vector3.zero;
     var bounds=new Bounds();bool first=true;foreach(var mf in model.GetComponentsInChildren<MeshFilter>()){if(mf.sharedMesh==null)continue;var bb=mf.sharedMesh.bounds;for(int c=0;c<8;c++){var local=bb.center+Vector3.Scale(bb.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1));var v=go.transform.InverseTransformPoint(mf.transform.TransformPoint(local));if(first){bounds=new Bounds(v,Vector3.zero);first=false;}else bounds.Encapsulate(v);}}
     if(!first){Vector3 factor=Vector3.one;if(spec["fit_size"]!=null){Vector3 want=Size(spec["fit_size"]);factor=new Vector3(want.x/Mathf.Max(bounds.size.x,.001f),want.y/Mathf.Max(bounds.size.y,.001f),want.z/Mathf.Max(bounds.size.z,.001f));}model.transform.localScale=factor;model.transform.localPosition=-Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),factor);}
     if(spec["material"]!=null)foreach(var rr in model.GetComponentsInChildren<Renderer>())rr.sharedMaterials=rr.sharedMaterials.Select(_=>MaterialFor((string)spec["material"])).ToArray();if(spec["material_map"] is JObject map)foreach(var rr in model.GetComponentsInChildren<Renderer>()){rr.sharedMaterials=rr.sharedMaterials.Select(mm=>{var mk=map[rr.name]??(mm==null?null:map[mm.name])??map["*"];return mk==null?mm:MaterialFor((string)mk);}).ToArray();}if((bool?)spec["collider"]!=false&&model.GetComponentsInChildren<Collider>().Length==0&&!first){var cc=go.AddComponent<BoxCollider>();Vector3 fitted=Vector3.Scale(bounds.size,model.transform.localScale);cc.center=new Vector3(0,fitted.y*.5f,0);cc.size=fitted;}
    }else{
     go=new GameObject(id);Undo.RegisterCreatedObjectUndo(go,"R25sourcepart");go.transform.SetParent(groups[group],false);
     if(kind=="text"){
      if(kind=="text")throw new Exception("R25 latest user forbids text standins and text storefront signs; author physical source logo/letter solids instead");
      if(localFont==null){var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/ChooGuard/ThirdParty/Fonts/NotoSansCJKkr-Regular.otf");if(source==null)throw new Exception("ExistingNotoOTFrequired");string chars=new string(string.Concat(((JArray)p["objects"]).Where(x=>(string)x["kind"]=="text").Select(x=>(string)x["text"])).Distinct().ToArray());localFont=TMP_FontAsset.CreateFontAsset(source,90,9,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);if(localFont==null)throw new Exception("Ownfontcreationfailed");localFont.name="R25_"+StoreKey+"_POPULATED_KOREAN";localFont.fallbackFontAssetTable=new List<TMP_FontAsset>();string missing;if(!localFont.TryAddCharacters(chars,out missing))throw new Exception("Ownfontmissingglyphs:"+missing);localFont.atlasPopulationMode=AtlasPopulationMode.Static;string ap=folder+"/KoreanPopulatedFont.asset";AssetDatabase.CreateAsset(localFont,ap);CreatedAssets.Add(ap);AssetDatabase.AddObjectToAsset(localFont.material,localFont);foreach(var tex in localFont.atlasTextures){if(tex!=null){tex.name="R25_"+StoreKey+"_OWN_ATLAS";AssetDatabase.AddObjectToAsset(tex,localFont);EditorUtility.SetDirty(tex);}}EditorUtility.SetDirty(localFont);}
      var text=go.AddComponent<TextMeshPro>();text.font=localFont;text.text=(string)spec["text"];text.fontSize=10;text.color=ColorOf(spec["color"]);text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.NoWrap;text.enableAutoSizing=false;text.rectTransform.sizeDelta=new Vector2(100,20);text.ForceMeshUpdate();var b=text.textBounds;float targetWidth=(float)spec["width"],targetHeight=(float)spec["height"];float scale=Mathf.Min(targetWidth/Mathf.Max(b.size.x,.001f),targetHeight/Mathf.Max(b.size.y,.001f));go.transform.localScale=Vector3.one*scale;
     }else if(kind=="light"){
      var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=ColorOf(spec["color"]);light.range=(float?)spec["range"]??2;light.intensity=(float?)spec["intensity"]??.3f;light.shadows=LightShadows.None;
     }else{
      Mesh mesh;
      if(kind=="mesh"){
       mesh=new Mesh{name="R25_"+StoreKey+"_"+id};if(((JArray)spec["vertices"]).Count>65000)mesh.indexFormat=IndexFormat.UInt32;
       mesh.vertices=spec["vertices"].Select(v=>new Vector3((float)v[0],(float)v[1],(float)v[2])).ToArray();mesh.triangles=spec["triangles"].Select(v=>(int)v).ToArray();if(spec["uv"]!=null)mesh.uv=spec["uv"].Select(v=>new Vector2((float)v[0],(float)v[1])).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();string ap=folder+"/MESH_"+id+".asset";AssetDatabase.CreateAsset(mesh,ap);CreatedAssets.Add(ap);
      }else if(kind=="library_mesh")mesh=LibraryMesh(p,folder,(string)spec["mesh_key"]);else if(kind=="mesh_ref")mesh=FreeReferencedMesh(spec,folder);else mesh=Primitive(kind);
      go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=MaterialFor((string)spec["material"]);renderer.shadowCastingMode=(bool?)spec["cast_shadows"]==false?ShadowCastingMode.Off:ShadowCastingMode.On;renderer.receiveShadows=true;
      if((bool?)spec["collider"]!=false){if(kind=="mesh"){go.AddComponent<MeshCollider>().sharedMesh=mesh;}else if(kind=="cylinder"){var cc=go.AddComponent<CapsuleCollider>();cc.radius=.5f;cc.height=2f;}else if(kind=="sphere"){go.AddComponent<SphereCollider>();}else go.AddComponent<BoxCollider>();}
     }
    }
    go.transform.localPosition=Position(spec,origin);float yaw=(float?)spec["yaw"]??0;var angles=spec["euler"];go.transform.localRotation=angles==null?Quaternion.Euler(0,yaw,0):Quaternion.Euler((float)angles[0],(float)angles[1],(float)angles[2]);if(kind=="text")go.transform.localRotation=go.transform.localRotation*Quaternion.Euler(0,180,0);
    if(spec["size"]!=null&&kind!="text")go.transform.localScale=Vector3.Scale(go.transform.localScale,Size(spec["size"]));
    if(kind=="edge"){
     var a=spec["a"];var b=spec["b"];float ax=(float)a[0],az=(float)a[1],bx=(float)b[0],bz=(float)b[1];float height=(float)spec["height"],bottom=(float?)spec["bottom"]??0;go.transform.localPosition=new Vector3((ax+bx)/2-origin.x,bottom+height/2,(az+bz)/2-origin.y);go.transform.localRotation=Quaternion.Euler(0,-Mathf.Atan2(bz-az,bx-ax)*Mathf.Rad2Deg,0);go.transform.localScale=new Vector3(Vector2.Distance(new Vector2(ax,az),new Vector2(bx,bz)),height,(float)spec["thickness"]);
    }
    createdObjects.Add(go);applied.Add(new JObject{["id"]=id,["kind"]=kind,["source_ids"]=spec["source_ids"]==null?new JArray():spec["source_ids"].DeepClone(),["geometry_qualification"]=(string)spec["qualification"]??"PHOTO_REFERENCED_MODEL_NOT_FIELD_MEASUREMENT"});
   }
   BuildOwnSourceEntrance(folder,p);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Guard();if(!EditorSceneManager.SaveScene(scene))throw new Exception("Ownstoresavefailed");saved=true;for(int i=0;i<createdObjects.Count;i++)applied[i]["object_id"]=GlobalObjectId.GetGlobalObjectIdSlow(createdObjects[i]).ToString();Undo.CollapseUndoOperations(undo);SceneView.RepaintAll();
   var receipt=new JObject{["status"]="OWN_STORE_APPLIED_AND_SAVED",["utc"]=DateTime.UtcNow.ToString("o"),["store_key"]=StoreKey,["scope"]=Scope,["author"]=Thread,["root_path"]=ParentPath+"/"+name,["packet"]=Packet,["packet_sha256"]=PacketSHA,["scene_sha256_before"]=before,["scene_sha256_after"]=Sha(Root+"/"+scene.path),["source_packet"]=p["source_packet"]==null?null:p["source_packet"].DeepClone(),["objects"]=applied,["object_count"]=applied.Count,["created_asset_paths"]=new JArray(CreatedAssets),["all2F_complete"]=false,["99percent_achieved"]=false,["QA_Play_tests_reopen_capture"]=false,["own_generated_storefront_entrance"]=OwnApertureReceipt};
   string output=".state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/"+StoreKey+"/APPLIED-STORE-R25.json";File.WriteAllText(Root+"/"+output,receipt.ToString());return new JObject{["status"]=receipt["status"],["store_key"]=StoreKey,["object_count"]=applied.Count,["receipt"]=output}.ToString();
  }catch{if(!saved){Undo.RevertAllDownToGroup(undo);foreach(string ap in CreatedAssets.AsEnumerable().Reverse())AssetDatabase.DeleteAsset(ap);}throw;}
 }
}
