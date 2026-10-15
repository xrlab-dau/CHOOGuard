#if UNITY_EDITOR
// Draft replay of source-backed complete B2/B4 assemblies; preserves existing runtime fields.
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
namespace ChooGuard.Editor.Twin
{
 public static class Platform6BanksB24PlacementReplay
 {
  public const string Manifest="workers/twin/output/platform6-interior-object-audit-20261010/B2-B4-followup/B2-B4-placement-replay-manifest.json";
  public const string ManifestSHA="b50c880b9e2e6aa7a9dfdd155ffb7a2cd7ad33e72e67a90ff11f144e90f7ad8e";
  static void Need(bool b,string why)=>Platform6BankB1Replay.Need(b,"B2/B4 placement: "+why);
  static string Hash(string p)=>Platform6BankB1Replay.Hash(p);
  static Vector3 V(JToken p)=>Platform6BankB1Replay.V(p);
  static void Frame(Transform t,JToken p)=>Platform6BankB1Replay.Frame(t,p);
  static string PathOf(Transform t)=>Platform6BankB1Replay.PathOf(t);
  static System.Linq.ILookup<string,Transform> Lookup()=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToLookup(PathOf);
  static Transform One(System.Linq.ILookup<string,Transform> index,string path){var a=index[path].ToArray();Need(a.Length==1,"missing/ambiguous exact path "+path);return a[0];}
  public static JObject CarrierFields(Transform t)=>new JObject{["gameobject"]=Platform6BankB1Replay.Fields(t.gameObject),["transform"]=Platform6BankB1Replay.Fields(t),["filter"]=t.GetComponent<MeshFilter>()!=null?Platform6BankB1Replay.Fields(t.GetComponent<MeshFilter>()):null,["renderer"]=t.GetComponent<MeshRenderer>()!=null?Platform6BankB1Replay.Fields(t.GetComponent<MeshRenderer>()):null,["collider"]=t.GetComponent<MeshCollider>()!=null?Platform6BankB1Replay.Fields(t.GetComponent<MeshCollider>()):null};
  public static JObject Pins()
  {
   Need(Hash(Manifest)==ManifestSHA,"sealed complete membership/pose manifest changed");var m=JObject.Parse(File.ReadAllText(Manifest));Need(m["banks"].Count()==2&&(int)m["sourcepart_owner_count"]==162&&(int)m["moving_well_transform_count"]==98&&m["body_groups"].Count()==3,"complete two bank membership contract");
   Need(Hash((string)m["source_plan"])==(string)m["source_plan_sha256"]&&Hash((string)m["complete_pose_manifest"])==(string)m["complete_pose_manifest_sha256"]&&Hash((string)m["terminal_recipe"])==(string)m["terminal_recipe_sha256"],"source and exact geometry pins");return m;
  }
  static JObject Read()
  {
   var r=Platform6BanksB24Terminal.Read();if(r==null)return null;Need((string)r["placement_manifest"]==Manifest&&(string)r["placement_manifest_sha256"]==ManifestSHA&&r["bank_states"].Count()==2&&r["body_rows"].Count()==3,"complete installed placement/replay provenance");return r;
  }
  static void SourceOwners(JObject m,bool moved,bool exactIDs)
  {
   var index=Lookup();foreach(var b in m["banks"])foreach(var p in b["complete_actual_SourcePart_owner_pins"])
   {
    var t=One(index,(string)p["path"]);Need(t.gameObject.activeInHierarchy,"complete sourcepart inactive");Frame(t,p[moved?"after_rigid_world_matrix":"actual_world_matrix"]);var mf=t.GetComponent<MeshFilter>();var mr=t.GetComponent<MeshRenderer>();var mc=t.GetComponent<MeshCollider>();Need(mf!=null&&mr!=null&&mc!=null&&mr.enabled&&mr.additionalVertexStreams==null&&mc.enabled&&!mc.isTrigger&&!mc.convex&&(int)mc.cookingOptions==30,"sourcepart render/collision policy");
    Need(mf.sharedMesh!=null&&mf.sharedMesh==mc.sharedMesh&&!EditorUtility.IsDirty(mf.sharedMesh)&&Platform6BankB1Replay.Complete(mf.sharedMesh)==(string)p["mesh_complete_digest"]&&AssetDatabase.GetAssetPath(mf.sharedMesh)==(string)p["mesh_asset"]&&AssetDatabase.AssetPathToGUID((string)p["mesh_asset"])==(string)p["mesh_guid"],"sourcepart mesh identity/channels");
    Need(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mf.sharedMesh,out string guid,out long fileId)&&guid==(string)p["mesh_guid"]&&fileId==(long)p["mesh_local_file_id"],"sourcepart exact FBX subasset");Need(mr.sharedMaterials.Select(AssetDatabase.GetAssetPath).SequenceEqual(p["materials"].Select(x=>(string)x)),"sourcepart inherited material sequence");
    if(exactIDs){Need((long)GlobalObjectId.GetGlobalObjectIdSlow(t).targetObjectId==(long)p["transform_id"]&&(long)GlobalObjectId.GetGlobalObjectIdSlow(mf).targetObjectId==(long)p["filter_id"]&&(long)GlobalObjectId.GetGlobalObjectIdSlow(mr).targetObjectId==(long)p["renderer_id"]&&(long)GlobalObjectId.GetGlobalObjectIdSlow(mc).targetObjectId==(long)p["collider_id"],"exact current sourcepart owner IDs");}
   }
   Need(Hash((string)m["banks"][0]["complete_actual_SourcePart_owner_pins"][0]["mesh_asset"])==(string)m["source_parts_FBX_sha256"],"immutable source-part FBX changed");
  }
  public static JArray CaptureBeforeMovement()
  {
   Need(Read()==null,"fresh two-bank registration required");var m=Pins();SourceOwners(m,false,true);string pointsPath=(string)m["banks"][0]["stationpoints"]["path"];string pointsText=File.ReadAllText(pointsPath);var points=JObject.Parse(pointsText);var importedPoints=AssetDatabase.LoadAssetAtPath<TextAsset>(pointsPath);Need(importedPoints!=null&&importedPoints.text==pointsText,"preparation StationPoints disk/import agreement");foreach(var b in m["banks"])Need(JToken.DeepEquals(points["escalators"].Single(x=>(string)x["id"]==(string)b["stationpoints"]["row_id"]),b["stationpoints"]["before_row"]),"preparation exact scoped StationPoints old row");var index=Lookup();var states=new JArray();foreach(var b in m["banks"])
   {
    var p=b["sourcepart_parent_move_once"];var parent=One(index,(string)p["path"]);Frame(parent,p["world_matrix_at_read"]);Need((long)GlobalObjectId.GetGlobalObjectIdSlow(parent).targetObjectId==(long)p["transform_id"],"exact source common parent ID");var well=One(index,(string)b["moving_well_path"]);Need(well.GetComponentsInChildren<Transform>(true).Length==49,"complete moving well49");foreach(var c in b["moving_well_all_children"]){var t=One(index,(string)c["path"]);Frame(t,c["world_matrix_at_read"]);Need((long)GlobalObjectId.GetGlobalObjectIdSlow(t).targetObjectId==(long)c["transform_id"],"exact original well transform IDs");}
    states.Add(new JObject{["bank_id"]=b["bank_id"].DeepClone(),["sourcepart_state"]=Platform6BankB1Replay.Snapshot(parent,true),["well_state"]=Platform6BankB1Replay.Snapshot(well,true)});
   }return states;
  }
  static void Move(Transform t,JToken oldMatrix,JToken nextMatrix,JToken state)
  {
   Frame(t,oldMatrix);Platform6BankB1Replay.SnapshotMatches(t,state);t.position+=new Vector3((float)nextMatrix[0][3]-(float)oldMatrix[0][3],(float)nextMatrix[1][3]-(float)oldMatrix[1][3],(float)nextMatrix[2][3]-(float)oldMatrix[2][3]);Frame(t,nextMatrix);Platform6BankB1Replay.SnapshotMatches(t,state);
  }
  public static void MovePreparedBanksAndStationPointsWithoutSave(JArray states)
  {
   var m=Pins();Need(states!=null&&states.Count==2,"both complete prepared state snapshots required");SourceOwners(m,false,true);var index=Lookup();string path=(string)m["banks"][0]["stationpoints"]["path"];var points=JObject.Parse(File.ReadAllText(path));foreach(var b in m["banks"]){var s=states.Single(x=>(string)x["bank_id"]==(string)b["bank_id"]);var p=b["sourcepart_parent_move_once"];Platform6BankB1Replay.SnapshotMatches(One(index,(string)p["path"]),s["sourcepart_state"]);Platform6BankB1Replay.SnapshotMatches(One(index,(string)b["moving_well_path"]),s["well_state"]);var sp=b["stationpoints"];Need(JToken.DeepEquals(points["escalators"].Single(x=>(string)x["id"]==(string)sp["row_id"]),sp["before_row"]),"exact scoped StationPoints old row");}
   foreach(var b in m["banks"]){var s=states.Single(x=>(string)x["bank_id"]==(string)b["bank_id"]);var p=b["sourcepart_parent_move_once"];Move(One(index,(string)p["path"]),p["world_matrix_at_read"],p["world_matrix_after_rigid_move"],s["sourcepart_state"]);var w=b["moving_well_all_children"].Single(x=>(long)x["transform_id"]==(long)b["moving_well_parent_id"]);Move(One(index,(string)b["moving_well_path"]),w["world_matrix_at_read"],w["world_matrix_after_rigid_move"],s["well_state"]);points["escalators"].Single(x=>(string)x["id"]==(string)b["stationpoints"]["row_id"]).Replace(b["stationpoints"]["after_row"].DeepClone());}
   File.WriteAllText(path,points.ToString()+"\n");AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var imported=AssetDatabase.LoadAssetAtPath<TextAsset>(path);Need(imported!=null&&imported.text==File.ReadAllText(path),"StationPoints disk/import agreement");SourceOwners(m,true,true);
  }
  static Mesh Body(JToken pin,JToken row,string role)
  {
   var p=pin[role+"_asset"];var d=row[role];string a=(string)p["asset"];Need(a==(string)d["asset"]&&Hash(a)==(string)p["file_sha256"]&&Hash(a+".meta")== (string)p["meta_sha256"]&&AssetDatabase.AssetPathToGUID(a)==(string)p["guid"],"saved complete source-body asset identity");var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(a);Need(mesh!=null&&!EditorUtility.IsDirty(mesh)&&Platform6BankB1Replay.Complete(mesh)==(string)d["unity_digests"]["complete_channels_sha256"],"exact complete source-body channel digest");return mesh;
  }
  public static JObject VerifyInstalled()
  {
   var r=Read();Need(r!=null,"installed complete B2/B4 registry missing");var m=Pins();Platform6BanksB24Terminal.Verify();SourceOwners(m,true,false);var index=Lookup();foreach(var b in m["banks"])
   {
    var s=r["bank_states"].Single(x=>(string)x["bank_id"]==(string)b["bank_id"]);var p=b["sourcepart_parent_move_once"];var parent=One(index,(string)p["path"]);Frame(parent,p["world_matrix_after_rigid_move"]);Platform6BankB1Replay.SnapshotMatches(parent,s["sourcepart_state"]);var well=One(index,(string)b["moving_well_path"]);Need(well.GetComponentsInChildren<Transform>(true).Length==49,"complete inherited well49");Platform6BankB1Replay.SnapshotMatches(well,s["well_state"]);foreach(var c in b["moving_well_all_children"])Frame(One(index,(string)c["path"]),c["world_matrix_after_rigid_move"]);
   }
   foreach(var group in m["body_groups"]){var p=r["body_rows"].Single(x=>(string)x["bank_id"]==(string)group["bank_id"]&&(string)x["carrier"]==(string)group["carrier"]);var t=One(index,(string)group["body_owner_path"]);Frame(t,group["after_rigid_world_matrix"]);Need(t.GetComponents<Component>().Length==4&&t.gameObject.activeInHierarchy,"exact independent attributed body owner");Need(t.GetComponent<MeshFilter>().sharedMesh==Body(p,group,"render")&&t.GetComponent<MeshCollider>().sharedMesh==Body(p,group,"collider"),"independent body exact render/collision bindings");Need(JToken.DeepEquals(Platform6BankB1Replay.Fields(t.gameObject),p["gameobject_fields"])&&JToken.DeepEquals(Platform6BankB1Replay.Fields(t.GetComponent<MeshRenderer>()),p["renderer_fields"])&&JToken.DeepEquals(Platform6BankB1Replay.Fields(t.GetComponent<MeshCollider>()),p["collider_fields"]),"body fields/materials/settings preserved");}
   string path=(string)m["banks"][0]["stationpoints"]["path"];string text=File.ReadAllText(path);var points=JObject.Parse(text);foreach(var b in m["banks"])Need(JToken.DeepEquals(points["escalators"].Single(x=>(string)x["id"]==(string)b["stationpoints"]["row_id"]),b["stationpoints"]["after_row"]),"translated original StationPoints row");var imported=AssetDatabase.LoadAssetAtPath<TextAsset>(path);Need(imported!=null&&imported.text==text,"StationPoints imported asset agreement");return new JObject{["status"]="PASS",["full_SourcePart_MR_MC_owners"]=162,["inherited_well_transforms"]=98,["independent_native_body_owners"]=3,["runtime_semantics_changed"]=false,["actual_PhysX_executed"]=false,["SceneSave_executed"]=false};
  }
  public static void BeforeRebuild(string rootPath){if(Read()==null)return;Platform6B24OldHoleFloorSupport.BeforeRebuild(rootPath);if(rootPath=="FPSWorld"||rootPath=="FPSWorld/공식 자료 부산역 역사/MainShell"||rootPath=="FPSWorld/원본 분리 부품"||rootPath=="FPSWorld/승강장 디딤판 운행"||rootPath=="맞이방 · 원본 정합")VerifyInstalled();}
  public static void AfterSourcePartsBuilt()
  {
   var r=Read();if(r==null)return;var m=Pins();SourceOwners(m,false,false);var index=Lookup();foreach(var b in m["banks"]){var s=r["bank_states"].Single(x=>(string)x["bank_id"]==(string)b["bank_id"]);var p=b["sourcepart_parent_move_once"];Move(One(index,(string)p["path"]),p["world_matrix_at_read"],p["world_matrix_after_rigid_move"],s["sourcepart_state"]);}SourceOwners(m,true,false);
  }
  public static void AfterMovingWellsBuilt()
  {
   var r=Read();if(r==null)return;var m=Pins();var index=Lookup();foreach(var b in m["banks"]){var s=r["bank_states"].Single(x=>(string)x["bank_id"]==(string)b["bank_id"]);var w=b["moving_well_all_children"].Single(x=>(long)x["transform_id"]==(long)b["moving_well_parent_id"]);Move(One(index,(string)b["moving_well_path"]),w["world_matrix_at_read"],w["world_matrix_after_rigid_move"],s["well_state"]);}
  }
  public static Vector3 SurveyWellOffset(string id){if(id!="well-s-56"&&id!="well-s-1")return Vector3.zero;if(Read()==null)return Vector3.zero;var m=Pins();return V(m["banks"].Single(x=>(string)x["moving_well_path"]=="FPSWorld/승강장 디딤판 운행/esc-"+id)["Unity_world_translation_XYZ"]);}
  public static void AfterRebuild(Transform built,string rootPath)
  {
   var r=Read();if(r==null)return;var m=Pins();foreach(var group in m["body_groups"])
   {
    if(!((string)group["native_owner_path"]).StartsWith(rootPath+"/",StringComparison.Ordinal))continue;var source=built.GetComponentsInChildren<Transform>(true).Single(x=>PathOf(x)==(string)group["native_owner_path"]);Need(!built.GetComponentsInChildren<Transform>(true).Any(x=>PathOf(x)==(string)group["body_owner_path"]),"fresh independent body owner required");var pin=r["body_rows"].Single(x=>(string)x["bank_id"]==(string)group["bank_id"]&&(string)x["carrier"]==(string)group["carrier"]);var go=new GameObject(((string)group["body_owner_path"]).Split('/').Last());go.transform.SetParent(source.parent,false);go.transform.localPosition=source.localPosition;go.transform.localRotation=source.localRotation;go.transform.localScale=source.localScale;var b=m["banks"].Single(x=>(string)x["bank_id"]==(string)group["bank_id"]);go.transform.position+=V(b["Unity_world_translation_XYZ"]);go.layer=source.gameObject.layer;go.tag=source.gameObject.tag;GameObjectUtility.SetStaticEditorFlags(go,GameObjectUtility.GetStaticEditorFlags(source.gameObject));var mf=go.AddComponent<MeshFilter>();var mr=go.AddComponent<MeshRenderer>();var mc=go.AddComponent<MeshCollider>();EditorUtility.CopySerialized(source.GetComponent<MeshRenderer>(),mr);EditorUtility.CopySerialized(source.GetComponent<MeshCollider>(),mc);mf.sharedMesh=Body(pin,group,"render");mc.sharedMesh=Body(pin,group,"collider");Need(mr.gameObject==go&&mc.gameObject==go,"body copy retains attributed owner");Frame(go.transform,group["after_rigid_world_matrix"]);Need(JToken.DeepEquals(Platform6BankB1Replay.Fields(go),pin["gameobject_fields"])&&JToken.DeepEquals(Platform6BankB1Replay.Fields(mr),pin["renderer_fields"])&&JToken.DeepEquals(Platform6BankB1Replay.Fields(mc),pin["collider_fields"]),"fresh inherited body settings");
   }
   if(rootPath=="FPSWorld"||rootPath=="FPSWorld/공식 자료 부산역 역사/MainShell"||rootPath=="맞이방 · 원본 정합")Platform6B24OldHoleFloorSupport.RestoreForRebuild();
  }
 }
}
#endif
