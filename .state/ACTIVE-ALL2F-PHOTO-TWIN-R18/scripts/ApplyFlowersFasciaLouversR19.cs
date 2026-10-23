using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class ApplyFlowersFasciaLouversR19 {
 const string R="/Users/um-yunsang/CHOOGuard",Thread="01a14ee0-a8f0-76c3-90fd-ec5483106da9",Scope="C180_2F_PHOTO_LOCATION_R15_FLOWERS_FASCIA_LOUVERS_R19_APPLY_SAVE";
 const string Payload=".state/ACTIVE-ALL2F-PHOTO-TWIN-R18/geometry/FLOWERS-FASCIA-LOUVER-PAYLOAD-R19.json",Hash="35c9a0bbfa2b9793e0c024cc520515587c784e3d76f1490ddb7535a6f3133c95",Receipt=".state/ACTIVE-ALL2F-PHOTO-TWIN-R18/receipts/APPLIED-FLOWERS-FASCIA-LOUVERS-R19.json",CadDelta=".state/ACTIVE-ALL2F-PHOTO-TWIN-R18/geometry/CURRENT-CAD-ACTUAL-FLOWERS-HEADER-DELTA-R19.json";
 static string Sha(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 static string ID(UnityEngine.Object o){return o==null?null:GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();}
 static Vector3 Vec(JToken a){return new Vector3((float)a[0],(float)a[1],(float)a[2]);}
 static JArray V(Vector3 v){return new JArray(v.x,v.y,v.z);}
 static void Guard(){var a=JObject.Parse(File.ReadAllText(R+"/.state/WRITER-AUTHORITY.json"));if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["sole_writer_thread"]!=Thread||(string)a["scope"]!=Scope)throw new Exception("Exact scoped header author required");var g=JObject.Parse(File.ReadAllText(R+"/"+(string)a["delegated_author_lease"]));if((string)g["worker_thread"]!=Thread||(string)g["scope"]!=Scope||(string)g["grant_nonce"]!=(string)a["scene_worker_grant_nonce"])throw new Exception("Exact scoped nonce required");}
 static float Y(Vector3 bary,Vector3 a,Vector3 b,Vector3 c){return a.y*bary.x+b.y*bary.y+c.y*bary.z;}
 static List<Vector3> Clip(List<Vector3> poly,Vector3 a,Vector3 b,Vector3 c,float plane,bool keepAbove){
  var result=new List<Vector3>();if(poly.Count==0)return result;var last=poly[poly.Count-1];float ly=Y(last,a,b,c);bool inside=keepAbove?ly>=plane:ly<=plane;
  foreach(var cur in poly){float cy=Y(cur,a,b,c);bool next=keepAbove?cy>=plane:cy<=plane;if(inside!=next)result.Add(Vector3.LerpUnclamped(last,cur,(plane-ly)/(cy-ly)));if(next)result.Add(cur);last=cur;ly=cy;inside=next;}return result;
 }
 static Mesh SplitPublicHeader(Mesh old,Transform transform,JObject payload){
  var original=old.vertices;var normal=old.normals;var tangent=old.tangents;var color=old.colors;
  if(normal.Length!=original.Length)throw new Exception("Original pervertex normals required");
  var all=new List<Vector3>(original);var nn=new List<Vector3>(normal);var tt=new List<Vector4>(tangent);var cc=new List<Color>(color);var channels=new List<Vector4>[8];var alluv=new List<Vector4>[8];
  for(int ch=0;ch<8;ch++){channels[ch]=new List<Vector4>();old.GetUVs(ch,channels[ch]);if(channels[ch].Count!=0&&channels[ch].Count!=original.Length)throw new Exception("Unexpected original UV layout");alluv[ch]=new List<Vector4>(channels[ch]);}
  var submeshes=new List<int>[8];for(int s=0;s<8;s++)submeshes[s]=new List<int>();
  var selected=new HashSet<int>(payload["selected_triangles"].Select(x=>(int)x["triangle_index"]));
  for(int sub=0;sub<6;sub++){
   var ts=old.GetTriangles(sub);
   for(int off=0;off<ts.Length;off+=3){
    if(sub!=1||!selected.Contains(off/3)){submeshes[sub].AddRange(new[]{ts[off],ts[off+1],ts[off+2]});continue;}
    int ia=ts[off],ib=ts[off+1],ic=ts[off+2];Vector3 wa=transform.TransformPoint(original[ia]),wb=transform.TransformPoint(original[ib]),wc=transform.TransformPoint(original[ic]);
    float[] low={-10000f,9.213f,9.663f,10.163f},high={9.213f,9.663f,10.163f,10000f};int[] dest={1,6,7,1};
    for(int band=0;band<4;band++){
     var poly=new List<Vector3>{Vector3.right,Vector3.up,Vector3.forward};poly=Clip(poly,wa,wb,wc,low[band],true);poly=Clip(poly,wa,wb,wc,high[band],false);if(poly.Count<3)continue;
     for(int fan=1;fan+1<poly.Count;fan++){
      var bary=new[]{poly[0],poly[fan],poly[fan+1]};var positions=bary.Select(w=>original[ia]*w.x+original[ib]*w.y+original[ic]*w.z).ToArray();if(Vector3.Cross(positions[1]-positions[0],positions[2]-positions[0]).sqrMagnitude<1e-16f)continue;
      for(int k=0;k<3;k++){var w=bary[k];int index=all.Count;all.Add(positions[k]);nn.Add((normal[ia]*w.x+normal[ib]*w.y+normal[ic]*w.z).normalized);if(tangent.Length==original.Length)tt.Add(tangent[ia]*w.x+tangent[ib]*w.y+tangent[ic]*w.z);if(color.Length==original.Length)cc.Add(color[ia]*w.x+color[ib]*w.y+color[ic]*w.z);for(int ch=0;ch<8;ch++)if(channels[ch].Count!=0)alluv[ch].Add(channels[ch][ia]*w.x+channels[ch][ib]*w.y+channels[ch][ic]*w.z);submeshes[dest[band]].Add(index);}
     }
    }
   }
  }
  var mesh=UnityEngine.Object.Instantiate(old);mesh.name="R19_FLOWERS_PUBLIC_HEADER_SELECTED_DERIVATIVE_134908987";mesh.SetVertices(all);mesh.SetNormals(nn);if(tangent.Length==original.Length)mesh.SetTangents(tt);if(color.Length==original.Length)mesh.SetColors(cc);for(int ch=0;ch<8;ch++)if(channels[ch].Count!=0)mesh.SetUVs(ch,alluv[ch]);mesh.subMeshCount=8;for(int s=0;s<8;s++)mesh.SetTriangles(submeshes[s],s,false);mesh.RecalculateBounds();return mesh;
 }
 static Mesh Blades(JObject p,Transform parent){
  var vv=new List<Vector3>();var nn=new List<Vector3>();var uv=new List<Vector2>();var ts=new List<int>();
  foreach(var blade in p["louver_blades_MODEL"]){var source=blade["vertices_world"].Select(x=>parent.InverseTransformPoint(Vec(x))).ToArray();var indices=blade["triangles"].Select(x=>(int)x).ToArray();for(int i=0;i<indices.Length;i+=3){var a=source[indices[i]];var b=source[indices[i+1]];var c=source[indices[i+2]];var n=Vector3.Cross(b-a,c-a).normalized;foreach(var point in new[]{a,b,c}){ts.Add(vv.Count);vv.Add(point);nn.Add(n);uv.Add(new Vector2(point.x,point.y));}}}
  var m=new Mesh{name="R19_FLOWERS_UPPER_HORIZONTAL_LOUVER_BLADES_MODEL"};m.SetVertices(vv);m.SetNormals(nn);m.SetUVs(0,uv);m.SetTriangles(ts,0);m.RecalculateBounds();return m;
 }
 static JObject CadRow(GameObject go){
  var mf=go.GetComponent<MeshFilter>();var rr=go.GetComponent<MeshRenderer>();var mesh=mf.sharedMesh;var vs=mesh.vertices;for(int i=0;i<vs.Length;i++)vs[i]=go.transform.TransformPoint(vs[i]);var sections=new JObject();
  foreach(float y in new[]{7.15f,8.4f,10.5f,9.913f}){var lines=new JArray();for(int sub=0;sub<mesh.subMeshCount;sub++){var ts=mesh.GetTriangles(sub);for(int i=0;i<ts.Length;i+=3){var a=vs[ts[i]];var b=vs[ts[i+1]];var c=vs[ts[i+2]];var hit=new List<Vector3>();for(int e=0;e<3;e++){var u=e==0?a:e==1?b:c;var v=e==0?b:e==1?c:a;if((u.y<=y)!=(v.y<=y))hit.Add(Vector3.LerpUnclamped(u,v,(y-u.y)/(v.y-u.y)));}if(hit.Count==2&&(hit[0]-hit[1]).sqrMagnitude>1e-10f)lines.Add(new JArray(sub,i/3,V(hit[0]),V(hit[1])));}}if(lines.Count>0)sections[y.ToString("0.00000",System.Globalization.CultureInfo.InvariantCulture)]=lines;}
  string path=go.name;var t=go.transform.parent;while(t!=null){path=t.name+"/"+path;t=t.parent;}
  return new JObject{["object_id"]=ID(go),["path"]=path,["mesh_asset"]=AssetDatabase.GetAssetPath(mesh),["mesh_object_id"]=ID(mesh),["world_matrix"]=new JArray(Enumerable.Range(0,16).Select(i=>go.transform.localToWorldMatrix[i])),["bounds_min_world"]=V(rr.bounds.min),["bounds_max_world"]=V(rr.bounds.max),["source_kind"]="current_2F_object",["material_assets"]=new JArray(rr.sharedMaterials.Select(AssetDatabase.GetAssetPath)),["sections_world"]=sections,["matching_Y7_upward_triangles_world"]=new JArray(),["auxiliary_header_detail_cut_y"]=9.913f};
 }
 public static string Apply(){
  Guard();var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stopped FpsStation required");if(File.Exists(R+"/"+Receipt)||Sha(R+"/"+Payload)!=Hash)throw new Exception("Frozen packet changed or replay");var p=JObject.Parse(File.ReadAllText(R+"/"+Payload));foreach(var f in p["frozen_inputs"])if(Sha(R+"/"+(string)f["path"])!=(string)f["sha256"])throw new Exception("Frozen construction source changed");
  var row=p["target"];var go=GameObject.Find((string)row["path"]);if(go==null||ID(go)!=(string)row["object_id"])throw new Exception("Exact target absent");var mf=go.GetComponent<MeshFilter>();var rr=go.GetComponent<MeshRenderer>();if(mf==null||rr==null||!rr.enabled||ID(mf.sharedMesh)!=(string)row["mesh_object_id"]||AssetDatabase.GetAssetPath(mf.sharedMesh)!=(string)row["mesh_asset"])throw new Exception("Exact current target mesh changed");for(int i=0;i<16;i++)if(Math.Abs(go.transform.localToWorldMatrix[i]-(double)row["world_matrix"][i])>.00005)throw new Exception("Target transform changed");if(!rr.sharedMaterials.Select(AssetDatabase.GetAssetPath).SequenceEqual(row["material_assets"].Select(x=>(string)x)))throw new Exception("Target materials changed");if(mf.sharedMesh.subMeshCount!=6||rr.sharedMaterials.Length!=6)throw new Exception("Exact original six submeshes required");
  foreach(var a in p["anchors"]){var g=GameObject.Find((string)a["path"]);if(g==null||ID(g)!=(string)a["object_id"]||ID(g.GetComponent<MeshFilter>().sharedMesh)!=(string)a["mesh_object_id"])throw new Exception("Current construction anchor changed");for(int i=0;i<16;i++)if(Math.Abs(g.transform.localToWorldMatrix[i]-(double)a["world_matrix"][i])>.00005)throw new Exception("Anchor pose changed");}
  foreach(var t in p["selected_triangles"]){var old=mf.sharedMesh.GetTriangles(1);int k=(int)t["triangle_index"];if(!old.Skip(k*3).Take(3).SequenceEqual(t["vertex_indices"].Select(x=>(int)x)))throw new Exception("Exact selected public triangles changed");}
  string folder=(string)p["asset_folder"],wallasset=folder+"/"+(string)p["new_wall_mesh"],bladeasset=folder+"/"+(string)p["new_blades_mesh"];var keys=new[]{"smooth_charcoal_fascia","upper_dark_louver_backing","louver_blades"};var newpaths=new List<string>{wallasset,bladeasset};newpaths.AddRange(keys.Select(k=>folder+"/R19_FLOWERS_"+k.ToUpperInvariant()+"_MODEL.asset"));if(newpaths.Any(x=>File.Exists(R+"/"+x))||go.transform.Find((string)p["new_child"])!=null)throw new Exception("Own new header assets already exist");var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new Exception("Current URP shader required");
  bool saved=false,dirty=scene.isDirty;string before=Sha(R+"/"+scene.path);var oldm=rr.sharedMaterials;GameObject child=null;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Flowers source and currentCAD bound fascia and louvers");
  try{
   Guard();var wall=SplitPublicHeader(mf.sharedMesh,go.transform,p);var blade=Blades(p,go.transform);AssetDatabase.CreateAsset(wall,wallasset);AssetDatabase.CreateAsset(blade,bladeasset);var mats=new List<Material>();for(int i=0;i<keys.Length;i++){var x=p["materials"][keys[i]];var c=x["basecolor_sRGB_MODEL"];var mat=new Material(shader){name="R19_FLOWERS_"+keys[i].ToUpperInvariant()+"_MODEL"};mat.SetColor("_BaseColor",new Color((float)c[0],(float)c[1],(float)c[2],(float)c[3]));mat.SetFloat("_Metallic",(float)x["metallic_MODEL"]);mat.SetFloat("_Smoothness",1f-(float)x["roughness_MODEL"]);AssetDatabase.CreateAsset(mat,newpaths[i+2]);mats.Add(mat);}
   Undo.RecordObject(mf,"Independent bounded publicheader derivative");Undo.RecordObject(rr,"Source dark fascia and backing finish");mf.sharedMesh=wall;rr.sharedMaterials=oldm.Concat(mats.Take(2)).ToArray();child=new GameObject((string)p["new_child"]);Undo.RegisterCreatedObjectUndo(child,"Thin source horizontal louver blades");child.transform.SetParent(go.transform,false);child.AddComponent<MeshFilter>().sharedMesh=blade;child.AddComponent<MeshRenderer>().sharedMaterial=mats[2];EditorUtility.SetDirty(mf);EditorUtility.SetDirty(rr);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Guard();if(!EditorSceneManager.SaveScene(scene))throw new Exception("Own bounded header save failed");saved=true;Undo.CollapseUndoOperations(group);SceneView.RepaintAll();
  }catch{if(!saved){Undo.RevertAllDownToGroup(group);foreach(var asset in newpaths)if(File.Exists(R+"/"+asset))AssetDatabase.DeleteAsset(asset);}throw;}
  var cad=new JObject{["status"]="ACTUAL_SAVED_OWN_HEADER_VECTOR_CAD_DELTA",["utc"]=DateTime.UtcNow.ToString("o"),["source_save_receipt"]=Receipt,["objects"]=new JArray(CadRow(go),CadRow(child)),["base_scene_CAD_source"]=".state/ACTIVE-ALL2F-PHOTO-TWIN-R18/inputs/CURRENT-LIVE-2F-VECTOR-CAD-SOURCE-R18.json",["purpose"]="Own actual construction output for userrequested CAD synchronization; not an added Scene QA/probe/capture."};Guard();File.WriteAllText(R+"/"+CadDelta,cad.ToString(Formatting.None));
  var result=new JObject{["status"]="APPLIED_SAVED_FLOWERS_BOUNDED_FASCIA_AND_HORIZONTAL_LOUVERS_R19",["utc"]=DateTime.UtcNow.ToString("o"),["scene_saved"]=true,["scene_was_dirty"]=dirty,["scene"]=scene.path,["scene_sha256_before"]=before,["scene_sha256"]=Sha(R+"/"+scene.path),["object_id"]=ID(go),["path"]=row["path"].DeepClone(),["mesh_asset"]=wallasset,["mesh_object_id"]=ID(mf.sharedMesh),["new_child_object_id"]=ID(child),["new_child_path"]=((string)row["path"])+"/"+child.name,["new_child_mesh"]=bladeasset,["new_material_assets"]=new JArray(newpaths.Skip(2)),["source_packet"]=p["source_packet"].DeepClone(),["payload"]=Payload,["payload_sha256"]=Hash,["current_vector_CAD_construction_delta"]=CadDelta,["fascia_y_MODEL"]=new JArray(9.213,9.663),["horizontal_louver_y_MODEL"]=new JArray(9.663,10.163),["blade_count_MODEL"]=13,["current_opening_width_MODEL_unchanged"]=2.07199985456,["topY_unchanged"]=12.0999947,["all_other_submeshes_public_lower_upper_complements_UV_normals_colliders_TRS_unchanged"]=true,["doors_openings_signs_furniture_or_C21_store_groups_created"]=false,["native_1F_WC_floorR17_columns_circulation_all_other_owners_preserved"]=true,["model_dimensions_not_field_measured"]=true,["whole2F_complete"]=false,["fidelity_measured_percent"]=JValue.CreateNull(),["QA_tests_probes_reopen_captures"]=false,["validation_owner"]="user"};Guard();File.WriteAllText(R+"/"+Receipt,result.ToString());File.WriteAllText(R+"/.state/CURRENT-2F-PHOTO-LOCATION-DIGITAL-TWIN-R18.json",result.ToString());var authority=JObject.Parse(File.ReadAllText(R+"/.state/WRITER-AUTHORITY.json"));authority["latest_scene_sha256"]=result["scene_sha256"].DeepClone();authority["latest_receipt"]=Receipt;authority["latest_applied_scope"]=Scope;authority["current_2f_vector_cad_requires_refresh"]=true;authority["current_2f_vector_cad_scene_delta"]=Receipt;File.WriteAllText(R+"/.state/WRITER-AUTHORITY.json",authority.ToString());return new JObject{["status"]=result["status"],["scene_saved"]=true,["receipt"]=Receipt,["own_vector_CAD_delta"]=CadDelta}.ToString();
 }
}
