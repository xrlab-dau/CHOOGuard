using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class ExportFinal2FGeometryR26_008 {
 const string Root="/Users/um-yunsang/CHOOGuard", Thread="classic:/root/r26_final_cad", Scope="C180_R26_FINAL_CAD_SNAPSHOT_008";
 const string Out=".state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-008";
 static string P(Transform t){return t.parent==null?t.name:P(t.parent)+"/"+t.name;}
 static string ID(UnityEngine.Object o){return o==null?null:GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();}
 static JArray V(Vector3 v){return new JArray(v.x,v.y,v.z);}
 static string Sha(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 static void Guard(){var a=JObject.Parse(File.ReadAllText(Root+"/.state/WRITER-AUTHORITY.json"));if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["sole_writer_thread"]!=Thread||(string)a["scope"]!=Scope)throw new Exception("Exact scoped CAD snapshot authority required");var g=JObject.Parse(File.ReadAllText(Root+"/"+(string)a["delegated_author_lease"]));if((string)g["worker_thread"]!=Thread||(string)g["scope"]!=Scope||(string)g["grant_nonce"]!=(string)a["scene_worker_grant_nonce"])throw new Exception("Exact FIFO nonce required");}
 public static string Read(){
  Guard();var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stopped FpsStation required");
  Directory.CreateDirectory(Root+"/"+Out);string started=DateTime.UtcNow.ToString("o"),sceneSha=Sha(Root+"/"+scene.path);bool dirty=scene.isDirty;
  var exclusions=JObject.Parse(File.ReadAllText(Root+"/.state/CAD-RENDER-ONLY-LOD-EXCLUSIONS-20261019.json"));var excluded=new HashSet<string>(exclusions["render_only_object_ids"].Select(x=>(string)x));
  var objects=new JArray();var meshes=new JArray();var unreadable=new JArray();var ignored=new JArray();var meshIndex=new Dictionary<int,int>();int nativeCount=0,skinnedCount=0,excludedCount=0;
  string geo=Root+"/"+Out+"/CURRENT-2F-UNIQUE-MESH-GEOMETRY.bin.gz";
  using(var file=File.Create(geo))using(var zip=new GZipStream(file,System.IO.Compression.CompressionLevel.Optimal))using(var w=new BinaryWriter(zip)){
   foreach(var renderer in Resources.FindObjectsOfTypeAll<Renderer>().OrderBy(x=>x.GetInstanceID())){
    if(renderer.gameObject.scene!=scene||!renderer.gameObject.activeInHierarchy||!renderer.enabled)continue;
    var mf=renderer.GetComponent<MeshFilter>();var smr=renderer as SkinnedMeshRenderer;if(mf==null&&smr==null)continue;Mesh mesh=mf!=null?mf.sharedMesh:smr.sharedMesh;if(mesh==null)continue;
    string path=P(renderer.transform),id=ID(renderer.gameObject);if(excluded.Contains(id)){excludedCount++;continue;}
    bool native=path.Contains("OfficialStation"),upper=path.StartsWith("FPSWorld/Architecture/2F/",StringComparison.Ordinal),r25=path.Contains("/MAIN2F_R25_SOURCE_BOUND_SPECIALIST_SHOPS/"),crosses=renderer.bounds.max.y>=6.97f&&renderer.bounds.min.y<=14.03f;
    if(!native&&!upper&&!crosses)continue;
    if(native)nativeCount++;
    string role=native?"OFFICIAL_REFERENCE":r25?"CURRENT_R25_MODEL_NOT_NEW_USER_CONFIRMATION":upper?"CURRENT_2F_GEOMETRY":"MATCHING_LEVEL_CROSSING_REFERENCE";
    var row=new JObject{["object_id"]=id,["renderer_id"]=ID(renderer),["path"]=path,["source_kind"]=role,["matching_2f_height_band"]=crosses,["bounds_min_world"]=V(renderer.bounds.min),["bounds_max_world"]=V(renderer.bounds.max),["world_matrix_column_major"]=new JArray(Enumerable.Range(0,16).Select(i=>renderer.transform.localToWorldMatrix[i])),["source_mesh_asset"]=AssetDatabase.GetAssetPath(mesh),["source_mesh_object_id"]=ID(mesh),["materials"]=new JArray(renderer.sharedMaterials.Select(m=>new JObject{["object_id"]=ID(m),["asset"]=AssetDatabase.GetAssetPath(m),["name"]=m==null?null:m.name})),["layer"]=renderer.gameObject.layer,["lod_render_only_excluded"]=false};
    objects.Add(row);if(!crosses)continue;
    Mesh baked=null;if(smr!=null){baked=new Mesh();smr.BakeMesh(baked);mesh=baked;skinnedCount++;row["skinned_current_pose_baked_to_transient_memory"]=true;}
    try{
     int key=mesh.GetInstanceID(),mi;if(!meshIndex.TryGetValue(key,out mi)){
      mi=meshes.Count;Vector3[] vs;var subs=new List<int[]>();var topologies=new List<int>();
      try{vs=mesh.vertices;for(int s=0;s<mesh.subMeshCount;s++){subs.Add(mesh.GetIndices(s));topologies.Add((int)mesh.GetTopology(s));}}
      catch{using(var data=Mesh.AcquireReadOnlyMeshData(mesh)){var d=data[0];var vertices=new NativeArray<Vector3>(d.vertexCount,Allocator.Temp);try{d.GetVertices(vertices);vs=vertices.ToArray();}finally{vertices.Dispose();}for(int s=0;s<d.subMeshCount;s++){var desc=d.GetSubMesh(s);var indices=new NativeArray<int>(desc.indexCount,Allocator.Temp);try{d.GetIndices(indices,s,true);subs.Add(indices.ToArray());topologies.Add((int)desc.topology);}finally{indices.Dispose();}}}}
      w.Write(mi);w.Write(vs.Length);foreach(var v in vs){w.Write(v.x);w.Write(v.y);w.Write(v.z);}w.Write(subs.Count);for(int s=0;s<subs.Count;s++){w.Write(topologies[s]);w.Write(subs[s].Length);foreach(var ix in subs[s])w.Write(ix);}
      meshes.Add(new JObject{["index"]=mi,["source_mesh_asset"]=AssetDatabase.GetAssetPath(mesh),["source_mesh_object_id"]=ID(mesh),["name"]=mesh.name,["vertex_count"]=vs.Length,["submesh_index_counts"]=new JArray(subs.Select(x=>x.Length)),["topologies"]=new JArray(topologies)});meshIndex[key]=mi;
     }row["mesh_index"]=mi;
    }catch(Exception e){unreadable.Add(new JObject{["object_id"]=id,["path"]=path,["error"]=e.GetType().Name,["message"]=e.Message});}
    finally{if(baked!=null)UnityEngine.Object.DestroyImmediate(baked);}
   }
  }
  Guard();string ended=DateTime.UtcNow.ToString("o"),afterSha=Sha(Root+"/"+scene.path);
  var record=new JObject{["schema"]="chooguard.actual_current_2f_shared_mesh_snapshot.r26.final.v1",["status"]="ACTUAL_CURRENT_2F_MESH_GEOMETRY_EXPORTED",["started_utc"]=started,["utc"]=ended,["scene"]=scene.path,["scene_sha256_before"]=sceneSha,["scene_sha256_after"]=afterSha,["scene_dirty_before"]=dirty,["scene_dirty_after"]=scene.isDirty,["objects"]=objects,["meshes"]=meshes,["mesh_binary"]=Out+"/CURRENT-2F-UNIQUE-MESH-GEOMETRY.bin.gz",["mesh_binary_sha256"]=Sha(geo),["binary_format"]="gzip little-endian: repeated meshIndex:int32 vertexCount:int32 vertices:float32[x,y,z]*n submeshCount:int32 each(topology:int32 indexCount:int32 indices:int32*n); untilEOF",["fixed_official_mesh_parts_inventoried"]=nativeCount,["alternative_LOD1_registry_count"]=excluded.Count,["actual_excluded_renderers"]=excludedCount,["skinned_baked_transient_count"]=skinnedCount,["unreadable_meshes"]=unreadable,["Scene_Assets_changed"]=false,["no_scene_QA_probe_capture_reopen"]=true,["purpose"]="Current object mesh source for requested 2F vector CAD authoring; actual material/geometry identity, not userconfirmation/occupancy/floor authority",["new_user_confirmation_claim"]=false,["whole_2f_completion_claim"]=false,["author"]=Thread,["scope"]=Scope};
  string output=Out+"/CURRENT-2F-MESH-SNAPSHOT.json";File.WriteAllText(Root+"/"+output,record.ToString(Formatting.None));
  return new JObject{["status"]=record["status"],["output"]=output,["objects"]=objects.Count,["unique_meshes"]=meshes.Count,["official_mesh_parts"]=nativeCount,["unreadable_meshes"]=unreadable.Count,["scene_sha256"]=afterSha,["Scene_Assets_changed"]=false}.ToString(Formatting.None);
 }
}
