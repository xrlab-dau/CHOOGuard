using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public static class ApplyVectorSignSurfaces {
 const string R="/Users/um-yunsang/CHOOGuard",D=".state/ACTIVE-ALL-WORLD-TEXT-TO-MODELED-SIGNS-20261019",T="01a13cb4-b738-7823-bf16-30a39ea3d8c6";
 static string S,A,Floor,Nonce;static JArray Made;static Dictionary<string,Material> MaterialCache;static Dictionary<string,Mesh> MeshCache;static Dictionary<string,Material[]> MeshMaterials;
 static Vector3 V(JToken t){return new Vector3((float)t[0],(float)t[1],(float)t[2]);}
 static string Id(UnityEngine.Object o){return GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();}
 static string PathOf(Transform t){return t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;}
 static string ShortHash(string s){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-","").Substring(0,20);}
 static void Guard(){var a=JObject.Parse(File.ReadAllText(R+"/.state/WRITER-AUTHORITY.json"));if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["scope"]!=S||(string)a["sole_writer_thread"]!=T)throw new Exception("Exact active own C180 scoped writer required");var g=JObject.Parse(File.ReadAllText(R+"/"+(string)a["delegated_author_lease"]));string n=(string)a["scene_worker_grant_nonce"];if(String.IsNullOrEmpty(n)||(string)g["grant_nonce"]!=n||(string)g["scope"]!=S||(string)g["worker_thread"]!=T)throw new Exception("FIFO nonce changed");if(Nonce==null)Nonce=n;else if(Nonce!=n)throw new Exception("Current native author changed");if(SceneManager.GetActiveScene().path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stopped editing FpsStation required");}
 static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
 static GameObject Get(string s){GlobalObjectId id;if(!GlobalObjectId.TryParse(s,out id))throw new Exception("Bad current target identity");return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;}
 static Texture BaseMap(Material m){return m==null?null:m.HasProperty("_BaseMap")?m.GetTexture("_BaseMap"):m.mainTexture;}
 static GameObject Target(JObject e){
  var o=Get((string)e["id"]);if(o==null||!o.activeInHierarchy||PathOf(o.transform)!=(string)e["path"]||!PathOf(o.transform).Contains("/"+Floor+"/"))throw new Exception("Current source print target changed: "+(string)e["name"]);
  if(Vector3.Distance(o.transform.position,V(e["position"]))>.0005f||Quaternion.Angle(o.transform.rotation,Quaternion.Euler(V(e["euler"])))>.004f||Vector3.Distance(o.transform.localScale,V(e["local_scale"]))>.0005f)throw new Exception("Preserve newer user print pose");
  var f=o.GetComponent<MeshFilter>();var r=o.GetComponent<MeshRenderer>();if(f==null||r==null||AssetDatabase.GetAssetPath(f.sharedMesh)!=(string)e["mesh_asset"]||r.sharedMaterials.Length!=1||AssetDatabase.GetAssetPath(BaseMap(r.sharedMaterial))!=(string)e["texture"])throw new Exception("Only unchanged exact current printed face may be replaced: "+o.name);return o;
 }
 static Material ColorMaterial(string hex,Material source){
  Color c;if(!ColorUtility.TryParseHtmlString(hex,out c))throw new Exception("Bad source ink color");if(source.HasProperty("_BaseColor"))c*=source.GetColor("_BaseColor");float smooth=source.HasProperty("_Smoothness")?source.GetFloat("_Smoothness"):.24f;float emission=source.IsKeywordEnabled("_EMISSION")&&source.HasProperty("_EmissionColor")?source.GetColor("_EmissionColor").maxColorComponent:0;
  string key=ColorUtility.ToHtmlStringRGBA(c)+"_"+smooth.ToString("0.00",CultureInfo.InvariantCulture)+"_"+emission.ToString("0.00",CultureInfo.InvariantCulture);if(MaterialCache.ContainsKey(key))return MaterialCache[key];
  var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name="SourcePhysicalInk_"+ShortHash(key);m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",0);m.SetFloat("_Cull",0);m.doubleSidedGI=true;m.enableInstancing=true;if(emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*emission);}
  string path=A+"/Materials/"+m.name+".mat";AssetDatabase.CreateAsset(m,path);Made.Add(path);MaterialCache[key]=m;return m;
 }
 struct Mapping {public Vector3 Position,Normal;}
 static Mapping MapUV(Vector2 p,Vector3[] verts,Vector2[] uvs,Vector3[] normals,int[] tris){
  float best=-1e30f;int selected=-1;Vector3 bary=Vector3.zero;
  for(int i=0;i<tris.Length;i+=3){int ia=tris[i],ib=tris[i+1],ic=tris[i+2];Vector2 a=uvs[ia],b=uvs[ib],c=uvs[ic];Vector2 ab=b-a,ac=c-a,ap=p-a;float den=ab.x*ac.y-ab.y*ac.x;if(Mathf.Abs(den)<1e-12f)continue;float wb=(ap.x*ac.y-ap.y*ac.x)/den,wc=(ab.x*ap.y-ab.y*ap.x)/den,wa=1-wb-wc,score=Mathf.Min(wa,Mathf.Min(wb,wc));if(score>best){best=score;selected=i;bary=new Vector3(wa,wb,wc);}if(score>=-.00002f)break;}
  if(selected<0||best<-.005f)throw new Exception("Source UV artwork lies outside retained print face");
  if(best<0){bary=new Vector3(Mathf.Max(0,bary.x),Mathf.Max(0,bary.y),Mathf.Max(0,bary.z));bary/=bary.x+bary.y+bary.z;}
  int x=tris[selected],y=tris[selected+1],z=tris[selected+2];Vector3 normal=normals.Length==verts.Length?(normals[x]*bary.x+normals[y]*bary.y+normals[z]*bary.z).normalized:Vector3.Cross(verts[y]-verts[x],verts[z]-verts[x]).normalized;
  return new Mapping{Position=verts[x]*bary.x+verts[y]*bary.y+verts[z]*bary.z,Normal=normal};
 }
 static Mesh BuildMesh(GameObject target,JObject e,JObject art,string cacheKey,out Material[] materials){
  if(MeshCache.ContainsKey(cacheKey)){materials=MeshMaterials[cacheKey];return MeshCache[cacheKey];}
  var f=target.GetComponent<MeshFilter>();var old=f.sharedMesh;Vector3[] oldV=old.vertices,oldN=old.normals;Vector2[] oldUV=old.uv;int[] oldT=old.triangles;if(oldUV.Length!=oldV.Length)throw new Exception("Source face UVs missing");
  float u0=oldUV.Min(x=>x.x),u1=oldUV.Max(x=>x.x),v0=oldUV.Min(x=>x.y),v1=oldUV.Max(x=>x.y);
  var vertices=new List<Vector3>();var groups=new List<int[]>();var mats=new List<Material>();var sourceMat=target.GetComponent<MeshRenderer>().sharedMaterial;
  foreach(var p in ((JArray)art["palette"]).OfType<JObject>()){
   int offset=vertices.Count;
   foreach(var point in ((JArray)p["vertices"])){
    Vector3 uvz=V(point);var map=MapUV(new Vector2(Mathf.Lerp(u0,u1,uvz.x),Mathf.Lerp(v0,v1,uvz.y)),oldV,oldUV,oldN,oldT);float normalScale=Mathf.Max(.00001f,target.transform.TransformVector(map.Normal).magnitude);
    vertices.Add(map.Position+map.Normal*((.00006f+uvz.z*.00040f)/normalScale));
   }
   groups.Add(((JArray)p["triangles"]).Select(x=>(int)x+offset).ToArray());mats.Add(ColorMaterial((string)p["color"],sourceMat));
  }
  if(vertices.Count<4||groups.Count==0)throw new Exception("No actual physical contour geometry");
  var m=new Mesh();m.name="Direct_Source_Contour_Sign_"+ShortHash(cacheKey);if(vertices.Count>65535)m.indexFormat=IndexFormat.UInt32;m.SetVertices(vertices);m.subMeshCount=groups.Count;for(int i=0;i<groups.Count;i++)m.SetTriangles(groups[i],i,false);m.RecalculateNormals();m.RecalculateBounds();
  string path=A+"/Meshes/"+m.name+".asset";AssetDatabase.CreateAsset(m,path);Made.Add(path);materials=mats.ToArray();MeshCache[cacheKey]=m;MeshMaterials[cacheKey]=materials;return m;
 }
 static string Apply(string floor){
  Floor=floor;S="C180_WORLD_SIGN_GEOMETRY_RASTER_"+floor+"_APPLY_SAVE";A="Assets/ChooGuard/Art/StationInterior/DirectWorldSignGeometry_20261019/VectorSigns"+floor;Nonce=null;Made=new JArray();MaterialCache=new Dictionary<string,Material>();MeshCache=new Dictionary<string,Mesh>();MeshMaterials=new Dictionary<string,Material[]>();Guard();
  string receipt=R+"/"+D+"/APPLIED-SOURCE-CONTOUR-SIGNS-"+floor+".json";if(File.Exists(receipt)||AssetDatabase.IsValidFolder(A))throw new Exception("No blind replay of applied vector sign batch");
  var packet=JObject.Parse(File.ReadAllText(R+"/"+D+"/vector-"+floor.ToLowerInvariant()+"/"+(floor=="1F"?"VECTOR-1F-PACKET.json":"ACTUAL-SOURCE-RELIEF-PACKET.json")));var art=((JArray)packet["textures"]).OfType<JObject>().ToDictionary(x=>(string)x["texture"],x=>x);
  var input=JObject.Parse(File.ReadAllText(R+"/"+D+"/RASTER-SIGNS-"+floor+".json"));var entries=((JArray)input["items"]).OfType<JObject>().ToArray();foreach(var e in entries){if(!art.ContainsKey((string)e["texture"]))throw new Exception("Source texture relief packet incomplete");Target(e);}
  var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty;if(dirty&&!EditorSceneManager.SaveScene(scene))throw new Exception("Could not preserve existing user edits");Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Directly construct actual-source signs/notices as colored solid contours "+floor);bool saved=false;var changed=new JArray();
  try{
   Guard();Folder(A+"/Meshes");Folder(A+"/Materials");
   foreach(var e in entries){Guard();var go=Target(e);var src=art[(string)e["texture"]];string cacheKey=(string)e["mesh_asset"]+"|"+(string)e["texture"]+"|"+e["lossy_scale"].ToString(Formatting.None);Material[] materials;var mesh=BuildMesh(go,e,src,cacheKey,out materials);var f=go.GetComponent<MeshFilter>();var r=go.GetComponent<MeshRenderer>();Undo.RecordObject(f,"Replace photo sign plane by closed actual-source contours");Undo.RecordObject(r,"Use actual source ink colors without image/font atlas");f.sharedMesh=mesh;r.sharedMaterials=materials;
    changed.Add(new JObject{["id"]=Id(go),["path"]=PathOf(go.transform),["source_texture"]=e["texture"],["source_mesh_preserved"]=e["mesh_asset"],["new_closed_contour_mesh"]=AssetDatabase.GetAssetPath(mesh),["source_uv_bounds"]=src["source_uv_bounds"],["color_material_groups"]=materials.Length,["triangles"]=mesh.triangles.Length/3,["text_or_image_atlas_used"]=false,["transform_and_collider_unchanged"]=true});
   }
   Guard();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Actual source-contour signs Scene save failed");saved=true;Undo.CollapseUndoOperations(undo);
   var result=new JObject{["status"]="APPLIED_SAVED_DIRECT_SOURCE_CONTOUR_SIGNS_"+floor,["utc"]=DateTime.UtcNow.ToString("o"),["scene"]=scene.path,["scope"]=S,["grant_nonce"]=Nonce,["floor"]=floor,["actual_print_faces_replaced_by_geometry"]=changed.Count,["shared_contour_mesh_count"]=MeshCache.Count,["unique_triangles"]=MeshCache.Values.Sum(x=>x.triangles.Length/3),["placed_triangles"]=changed.Sum(x=>(long)x["triangles"]),["changes"]=changed,["new_runtime_assets"]=Made,["source_packet"]=D+"/vector-"+floor.ToLowerInvariant()+"/"+(floor=="1F"?"VECTOR-1F-PACKET.json":"ACTUAL-SOURCE-RELIEF-PACKET.json"),["source_photos_and_assets_changed"]=false,["native_floor_columns_booth_equipment_poses_changed"]=false,["new_scene_light_components"]=0,["new_world_text_components"]=0,["image_plane_or_font_atlas_substitutes"]=false,["source_blur_not_recovered_or_invented"]=true,["source_menu_epoch_not_current_price_claim"]=true,["user_validation_owner"]=true,["QA_tests_probes_reopen_captures"]=false};
   File.WriteAllText(receipt,result.ToString(Formatting.Indented));return new JObject{["status"]=result["status"],["faces"]=changed.Count,["shared_meshes"]=MeshCache.Count,["receipt"]=D+"/APPLIED-SOURCE-CONTOUR-SIGNS-"+floor+".json"}.ToString(Formatting.None);
  }catch(Exception ex){if(!saved)Undo.RevertAllDownToGroup(undo);File.WriteAllText(R+"/"+D+"/VECTOR-"+floor+"-ERROR.json",new JObject{["saved"]=saved,["error"]=ex.ToString(),["retained_new_assets"]=Made}.ToString());throw;}
 }
 public static string Apply1F(){return Apply("1F");}
 public static string Apply2F(){return Apply("2F");}
}
