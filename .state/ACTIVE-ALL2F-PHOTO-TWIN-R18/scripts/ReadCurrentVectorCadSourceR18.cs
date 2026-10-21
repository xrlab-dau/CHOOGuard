using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class ReadCurrentVectorCadSourceR18 {
 const string R="/Users/um-yunsang/CHOOGuard", Thread="01a14ee0-a8f0-76c3-90fd-ec5483106da9", Scope="C180_2F_PHOTO_LOCATION_R15_CURRENT_VECTOR_CAD_CONSTRUCTION_SOURCE_R18";
 static string P(Transform t){return t.parent==null?t.name:P(t.parent)+"/"+t.name;}
 static string ID(UnityEngine.Object o){return o==null?null:GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();}
 static JArray V(Vector3 v){return new JArray(v.x,v.y,v.z);}
 static void Guard(){
  var a=JObject.Parse(File.ReadAllText(R+"/.state/WRITER-AUTHORITY.json"));
  if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["sole_writer_thread"]!=Thread||(string)a["scope"]!=Scope)throw new Exception("Exact CAD construction-source author required");
  var g=JObject.Parse(File.ReadAllText(R+"/"+(string)a["delegated_author_lease"]));
  if((string)g["worker_thread"]!=Thread||(string)g["scope"]!=Scope||(string)g["grant_nonce"]!=(string)a["scene_worker_grant_nonce"])throw new Exception("Exact scoped nonce required");
 }
 public static string Read(){
  Guard();var scene=SceneManager.GetActiveScene();
  if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stopped FpsStation required");
  var excluded=new HashSet<string>(JObject.Parse(File.ReadAllText(R+"/.state/CAD-RENDER-ONLY-LOD-EXCLUSIONS-20261019.json"))["render_only_object_ids"].Select(x=>(string)x));
  var rows=new JArray();var unreadable=new JArray();float[] levels={7.15f,8.4f,10.5f};int officialCount=0;
  foreach(var mf in Resources.FindObjectsOfTypeAll<MeshFilter>()){
   if(mf.gameObject.scene!=scene||!mf.gameObject.activeInHierarchy||mf.sharedMesh==null)continue;
   var rr=mf.GetComponent<MeshRenderer>();if(rr==null||!rr.enabled)continue;
   string path=P(mf.transform),id=ID(mf.gameObject);if(excluded.Contains(id))continue;
   bool upper=path.StartsWith("FPSWorld/Architecture/2F/",StringComparison.Ordinal),native=path.Contains("OfficialStation"),transport=path.Contains("MAIN1F_APPROVED_R28_CIRCULATION_C158/");
   if(!upper&&!native&&!transport)continue;
   if(native)officialCount++;
   var mesh=mf.sharedMesh;
   var row=new JObject{["object_id"]=id,["path"]=path,["mesh_asset"]=AssetDatabase.GetAssetPath(mesh),["mesh_object_id"]=ID(mesh),["world_matrix"]=new JArray(Enumerable.Range(0,16).Select(i=>mf.transform.localToWorldMatrix[i])),["bounds_min_world"]=V(rr.bounds.min),["bounds_max_world"]=V(rr.bounds.max),["source_kind"]=native?"fixed_official_reference":transport?"fixed_current_1F_to_2F_transport_reference":"current_2F_object",["material_assets"]=new JArray(rr.sharedMaterials.Select(m=>AssetDatabase.GetAssetPath(m)))};
   var sections=new JObject();var floorTriangles=new JArray();row["sections_world"]=sections;row["matching_Y7_upward_triangles_world"]=floorTriangles;
   if(rr.bounds.max.y<6.97f||rr.bounds.min.y>12.11f){rows.Add(row);continue;}
   Vector3[] vs;
   try{vs=mesh.vertices;}catch(Exception e){unreadable.Add(new JObject{["object_id"]=id,["path"]=path,["error"]=e.GetType().Name});rows.Add(row);continue;}
   for(int i=0;i<vs.Length;i++)vs[i]=mf.transform.TransformPoint(vs[i]);
   var triangles=new List<int[]>();for(int sub=0;sub<mesh.subMeshCount;sub++)triangles.Add(mesh.GetTriangles(sub));
   var cuts=new List<float>(levels.Where(y=>rr.bounds.min.y<=y&&rr.bounds.max.y>=y));
   // Low furniture/guard geometry below the main cut remains on its own actual section.
   if(upper&&rr.bounds.max.y<8.4f&&rr.bounds.max.y>7.18f){float y=Mathf.Clamp((Mathf.Max(7.01f,rr.bounds.min.y)+rr.bounds.max.y)*.5f,7.02f,rr.bounds.max.y-.0001f);if(!cuts.Any(c=>Mathf.Abs(c-y)<.001f))cuts.Add(y);row["low_object_actual_cut_y"]=y;}
   foreach(float y in cuts){
    var lines=new JArray();
    for(int sub=0;sub<triangles.Count;sub++){var ts=triangles[sub];for(int i=0;i<ts.Length;i+=3){
     var a=vs[ts[i]];var b=vs[ts[i+1]];var c=vs[ts[i+2]];var hits=new List<Vector3>(2);
     for(int edge=0;edge<3;edge++){var v=edge==0?a:edge==1?b:c;var w=edge==0?b:edge==1?c:a;if((v.y<=y)!=(w.y<=y))hits.Add(Vector3.LerpUnclamped(v,w,(y-v.y)/(w.y-v.y)));}
     if(hits.Count==2&&(hits[0]-hits[1]).sqrMagnitude>1e-10f)lines.Add(new JArray(sub,i/3,V(hits[0]),V(hits[1])));
    }}
    sections[y.ToString("0.00000",System.Globalization.CultureInfo.InvariantCulture)]=lines;
   }
   // These exact matching-level surfaces are references, not permission to fill or cut floors.
   if((upper&&path.IndexOf("FLOOR",StringComparison.OrdinalIgnoreCase)>=0)||native||transport){
    for(int sub=0;sub<triangles.Count;sub++){var ts=triangles[sub];for(int i=0;i<ts.Length;i+=3){var a=vs[ts[i]];var b=vs[ts[i+1]];var c=vs[ts[i+2]];
     if(Mathf.Abs(a.y-7f)>.03f||Mathf.Abs(b.y-7f)>.03f||Mathf.Abs(c.y-7f)>.03f)continue;var n=Vector3.Cross(b-a,c-a);if(n.sqrMagnitude<1e-12f||n.normalized.y<.9f)continue;floorTriangles.Add(new JArray(sub,i/3,V(a),V(b),V(c)));
    }}
   }
   rows.Add(row);
  }
  Guard();string output=".state/ACTIVE-ALL2F-PHOTO-TWIN-R18/inputs/CURRENT-LIVE-2F-VECTOR-CAD-SOURCE-R18.json";
  var result=new JObject{["status"]="CURRENT_LIVE_2F_VECTOR_CAD_CONSTRUCTION_SOURCE",["utc"]=DateTime.UtcNow.ToString("o"),["scene"]=scene.path,["objects"]=rows,["fixed_official_mesh_parts_inventoried"]=officialCount,["unreadable_meshes"]=unreadable,["standard_cut_y"]=new JArray(levels),["render_only_alternative_LOD_objects_excluded"]=excluded.Count,["surface_semantic_limit"]="Matching Y7 native/transport triangles and all-height bounds are reference geometry only, not occupied floor coverage. O36 is1F platform ceiling. No new fill/cut or occupancy classification inferred.",["purpose"]="User requested current2FvectorCAD source for construction correspondence with field photos, specialist drawings and original2F drawing. Not a Scene QA/probe or camera capture.",["Scene_Assets_changed"]=false,["scene_dirty_preserved"]=scene.isDirty,["validation_owner"]="user"};
  File.WriteAllText(R+"/"+output,result.ToString(Formatting.None));
  return new JObject{["status"]=result["status"],["output"]=output,["objects"]=rows.Count,["fixed_official_mesh_parts"]=officialCount,["unreadable_meshes"]=unreadable.Count,["Scene_Assets_changed"]=false}.ToString();
 }
}
