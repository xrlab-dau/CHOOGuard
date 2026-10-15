#if UNITY_EDITOR
// Exact source-blank partition successor only. Root owns Assets and the sole Scene save.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
namespace ChooGuard.Editor.Twin
{
 public static class Platform6BluePartitionTerminal
 {
  public const string Registry="content/world/observed-interiors/platform6-source-blank-blue-partition.json";
  public const string Recipe="workers/twin/output/platform6-blue-partition-only-20261010/blue-only-install-recipe.json";
  public const string RecipeSHA="40628c57bc7448386bf87545af9f477f47ae430d0a1904bca27147d09751f690";
  public const string Proof="workers/twin/output/platform6-blue-only-independent-review-20261010/independent-surface-geometry-proof.json";
  public const string ProofSHA="f1b99ff2fe4a3fdaaf91c3b5910013cc1d8e3fb7263661ac617fb77315f8abb7";
  public const string PhysicalContract="workers/twin/output/platform6-blue-partition-only-20261010/blue-only-actual-surface-probe-contract.json";
  public const string PhysicalContractSHA="dc510e1c2baba9d6f8d4007974540d9518c96811de3e8f0154e5cb5245287ec4";
  public const string Native5="FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_5";
  public const string Legacy="content/world/observed-interiors/legacy-west-retail-architecture-retirement.json";
  public static void Need(bool b,string why)=>Platform6BankB1Replay.Need(b,"blue sourceblank: "+why);
  public static string Hash(string path)
  {
   using(var hash=System.Security.Cryptography.SHA256.Create())
   using(var stream=File.OpenRead(path))
    return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
  }
  public static JObject Pins()
  {
   Need(Hash(Recipe)==RecipeSHA,"sealed exact two-role recipe changed");var j=JObject.Parse(File.ReadAllText(Recipe));
   Need(Hash(Proof)==ProofSHA&&Hash(PhysicalContract)==PhysicalContractSHA,"sealed independent geometry proof/actual probe request changed");var proof=JObject.Parse(File.ReadAllText(Proof));Need((string)proof["status"]=="PASS","independent geometry proof must pass");
   Need((string)j["schema"]=="chooguard.platform6-blue-partition-only-recipe.v1"&&(string)j["source_ancestor"]=="ROOT/g58/g9"&&(bool?)j["installation_authorized"]==true&&(bool?)j["no_B3_bank_floor_column_or_pose_mutation"]==true&&j["rows"].Count()==2,"only reviewed thin g9 partition scope");
   Need(j["rows"].Select(x=>(string)x["name"]).OrderBy(x=>x).SequenceEqual(new[]{"native5-collider","native5-render"}),"exact native5 two roles");
   foreach(var p in j["source_inputs"])Need(Hash((string)p["path"])==(string)p["sha256"],"immutable source/attribution/frame pin");
   Need(Hash((string)j["predecessor_recipe"])==(string)j["predecessor_recipe_sha256"],"unchanged B24 recipe");
   foreach(var r in j["rows"])
   {
    Need((string)r["owner_path"]==Native5&&(int)r["retired_triangle_count"]==1246&&(int)r["vertex_count"]==98464&&(int)r["source_triangle_count"]-(int)r["replacement_triangle_count"]==1246,"exact sourceblank-only retirement cardinality");
    foreach(string k in new[]{"source_binary","replacement_binary","historical_pre_B24_binary","face_lineage"})Need(Hash((string)r[k])==(string)r[k+"_sha256"],"sealed face lineage/fullchannel binary "+k);
    Need(r["bindings"].Count()==1,"one exact binding per role");
   }return j;
  }
  public static JObject Read()
  {
   var j=Pins();if(!File.Exists(Registry))
   {
    foreach(var r in j["rows"])Need(!File.Exists((string)r["replacement_asset"]),"orphan installed successor asset");
    if(File.Exists(Legacy))Need(!JObject.Parse(File.ReadAllText(Legacy))["meshes"].Any(x=>x["platform6_source_blank_blue_partition"]!=null),"installed marker lost registry");return null;
   }
   var installed=JObject.Parse(File.ReadAllText(Registry));Need((string)installed["schema"]=="chooguard.platform6-installed-blue-sourceblank-partition.v1"&&(string)installed["recipe"]==Recipe&&(string)installed["recipe_sha256"]==RecipeSHA&&installed["rows"].Count()==2,"installed exact recipe/roles provenance");return installed;
  }
  public static void Digest(Mesh m,JToken dig,bool clean=true)
  {
   Need(m!=null&&m.isReadable&&(!clean||!EditorUtility.IsDirty(m))&&InteriorEnvelopeRepair.MeshDigest(m)==(string)dig["mesh_sha256"]&&ArchitecturalCorrectionRegistry.ChannelDigest(m)==(string)dig["channels_sha256"]&&Platform6BankB1Replay.Complete(m)==(string)dig["complete_channels_sha256"],"exact readable clean complete channels");
  }
  static Mesh Asset(JToken pin,string path,JToken dig)
  {
   Need((string)pin["asset"]==path&&File.Exists(path)&&Hash(path)==(string)pin["file_sha256"]&&Hash(path+".meta")== (string)pin["meta_sha256"]&&AssetDatabase.AssetPathToGUID(path)==(string)pin["guid"],"exact immutable saved asset identity");var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);Digest(m,dig);return m;
  }
  static JToken Row(JObject registry,JObject recipe,string role,out Mesh previous,out Mesh terminal)
  {
   Need(role=="native5-render"||role=="native5-collider","only exact B24 native5 role");var r=recipe["rows"].Single(x=>(string)x["name"]==role);var pin=registry["rows"].Single(x=>(string)x["name"]==role);
   Need((string)pin["owner_path"]==Native5&&JToken.DeepEquals(pin["predecessor_legacy_row"],r["predecessor_legacy_row"])&&JToken.DeepEquals(pin["predecessor_B24_terminal_row"],r["predecessor_B24_terminal_row"]),"frozen exact B24 Legacy/registry predecessor");
   var b24=JObject.Parse(File.ReadAllText((string)recipe["predecessor_registry"]));Need(JToken.DeepEquals(b24["terminal_rows"].Single(x=>(string)x["name"]==role),pin["predecessor_B24_terminal_row"]),"B24 original terminal/proof rows must remain unchanged");
   previous=Asset(pin["predecessor_asset"],(string)r["source_asset"],r["source_unity_digests"]);terminal=Asset(pin["terminal_asset"],(string)r["replacement_asset"],r["replacement_unity_digests"]);
   Need((string)pin["predecessor_asset"]["guid"]==(string)r["source_guid"]&&(string)pin["predecessor_asset"]["file_sha256"]==(string)r["source_file_sha256"]&&(string)pin["predecessor_asset"]["meta_sha256"]==(string)r["source_meta_sha256"],"exact retained B24 asset pins");return pin;
  }
  static JToken FinalLegacy(JToken pin,JToken recipe)
  {
   var previous=pin["predecessor_legacy_row"];var final=pin["final_legacy_row"];var expected=(JObject)previous.DeepClone();
   foreach(string key in new[]{"replacement_asset","replacement_guid","replacement_file_sha256","replacement_meta_sha256","replacement_complete_channel_digest","replacement_channel_digest","replacement_mesh_digest","replacement_binary","replacement_triangle_count","platform6_source_blank_blue_partition"})expected[key]=final[key]?.DeepClone();
   Need(JToken.DeepEquals(expected,final),"all Electrical/Guide/B24 history and unrelated fields preserved");var marker=final["platform6_source_blank_blue_partition"];
   Need(marker!=null&&(string)marker["recipe"]==Recipe&&(string)marker["recipe_sha256"]==RecipeSHA&&(string)marker["source_ancestor"]=="ROOT/g58/g9"&&(int)marker["retired_triangles"]==1246&&JToken.DeepEquals(marker["previous_terminal"],previous),"exact g9 composition ancestry");
   Need((string)final["replacement_asset"]==(string)recipe["replacement_asset"]&&(string)final["replacement_guid"]==(string)pin["terminal_asset"]["guid"]&&(string)final["replacement_file_sha256"]==(string)pin["terminal_asset"]["file_sha256"]&&(string)final["replacement_meta_sha256"]==(string)pin["terminal_asset"]["meta_sha256"]&&(string)final["replacement_complete_channel_digest"]==(string)recipe["replacement_unity_digests"]["complete_channels_sha256"]&&(string)final["replacement_channel_digest"]==(string)recipe["replacement_unity_digests"]["channels_sha256"]&&(string)final["replacement_mesh_digest"]==(string)recipe["replacement_unity_digests"]["mesh_sha256"]&&(string)final["replacement_binary"]["path"]==(string)recipe["replacement_binary"]&&(string)final["replacement_binary"]["sha256"]==(string)recipe["replacement_binary_sha256"]&&(int)final["replacement_triangle_count"]==(int)recipe["replacement_triangle_count"],"exact final Legacy channels/asset/binary/cardinality");
   Need(JToken.DeepEquals(JObject.Parse(File.ReadAllText(Legacy))["meshes"].Single(x=>(string)x["id"]==(string)recipe["legacy_id"]),final),"current exact Legacy terminal row");return final;
  }
  // Never call B24 Pair/Verify from these adapters: the predecessor validates its own output first.
  public static Mesh ResolveB24NativeCarrier(Mesh reviewedB24,string role,string ownerPath)
  {
   Need(ownerPath==Native5&&(role=="native5-render"||role=="native5-collider"),"exact role/owner required");var installed=Read();if(installed==null)return reviewedB24;var recipe=Pins();var pin=Row(installed,recipe,role,out var previous,out var terminal);Need(reviewedB24==previous,"exact retained B24 mesh input required");FinalLegacy(pin,recipe["rows"].Single(x=>(string)x["name"]==role));return ConcourseFidelitySuccessor.Resolve(terminal,role,ownerPath);
  }
  public static JToken ResolveB24Legacy(JToken reviewedB24,string role)
  {
   Need(role=="native5-render"||role=="native5-collider","exact native5 Legacy role required");var installed=Read();if(installed==null)return reviewedB24;var recipe=Pins();var pin=Row(installed,recipe,role,out var previous,out var terminal);Need(JToken.DeepEquals(reviewedB24,pin["predecessor_legacy_row"]),"exact retained B24 Legacy input required");return FinalLegacy(pin,recipe["rows"].Single(x=>(string)x["name"]==role));
  }
  public static JObject ReviewedB24CarrierFields(JObject actual,string ownerPath)
  {
   if(ownerPath!=Native5)return actual;actual=ConcourseFidelitySuccessor.ReviewedFields(actual,ownerPath);var installed=Read();if(installed==null)return actual;var recipe=Pins();
   Need(JToken.DeepEquals(actual,installed["native_fields_after"])&&JToken.DeepEquals(installed["native_fields_before"],recipe["rows"][0]["predecessor_B24_native_field_state"]["fields"]),"exact before/after normalized fields required");var before=(JObject)installed["native_fields_before"].DeepClone();var expected=(JObject)before.DeepClone();
   foreach(string key in new[]{"filter","collider"}){string wrapper=key=="filter"?"MeshFilter":"MeshCollider";Need(expected[key]?[wrapper] is JObject&&actual[key]?[wrapper] is JObject,"explicit serialized component wrapper");expected[key][wrapper]["m_Mesh"]=actual[key][wrapper]["m_Mesh"].DeepClone();}
   Need(JToken.DeepEquals(expected,actual),"only native5 two mesh references may differ");return before;
  }
  public static void RequireB24Carrier(Mesh actual,JToken b24Row,string ownerPath)
  {
   string role=(string)b24Row["name"];Need(ownerPath==Native5&&(role=="native5-render"||role=="native5-collider"),"only native5 B24 floor binding forwarding");var reviewed=AssetDatabase.LoadAssetAtPath<Mesh>((string)b24Row["replacement_asset"]);Digest(reviewed,b24Row["replacement_unity_digests"]);Need(actual==ResolveB24NativeCarrier(reviewed,role,ownerPath),"exact installed blue-only native5 successor mandatory");
  }
  public static JObject Verify()
  {
   var installed=Read();Need(installed!=null,"installed blue-only registry required");var recipe=Pins();var t=Platform6BankB1Replay.Find(Native5);Need(t.gameObject.activeInHierarchy,"native5 owner inactive");Platform6BankB1Replay.Frame(t,recipe["rows"][0]["world_matrix"]);
   ReviewedB24CarrierFields(Platform6BanksB24PlacementReplay.CarrierFields(t),Native5);
   foreach(var row in recipe["rows"])
   {
    string role=(string)row["name"];var pin=Row(installed,recipe,role,out var previous,out var terminal);FinalLegacy(pin,row);bool render=role=="native5-render";Component c=render?(Component)t.GetComponent<MeshFilter>():t.GetComponent<MeshCollider>();Need(c!=null,"exact native5 component");
    var current=ConcourseFidelitySuccessor.Resolve(terminal,role,Native5);Need(render?((MeshFilter)c).sharedMesh==current:((MeshCollider)c).sharedMesh==current,"exact composed successor bindings");
    if(render){var mr=t.GetComponent<MeshRenderer>();Need(mr!=null&&mr.enabled&&mr.additionalVertexStreams==null,"render policy");}else{var mc=(MeshCollider)c;Need(mc.enabled&&!mc.isTrigger&&!mc.convex&&(int)mc.cookingOptions==30,"collision policy");}
   }
   return new JObject{["status"]="PASS",["retired_triangles_per_role"]=1246,["unchanged_scene_owner_pose_and_materials"]=true,["B3_bank_not_changed"]=true,["actual_PhysX_executed"]=false,["SceneSave_executed"]=false,["site99percent_verified"]=false};
  }
  public static JObject PhysicalVerify()
  {
   var exact=Verify();const string scenePath="Assets/ChooGuard/Scenes/FpsStation.unity";var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();Need(scene.path==scenePath&&!EditorApplication.isCompiling&&!EditorApplication.isPlayingOrWillChangePlaymode,"stable Edit Scene for actual owner surface QA");int handle=scene.handle;bool dirty=scene.isDirty;string saved=Hash(scenePath);var request=JObject.Parse(File.ReadAllText(PhysicalContract));
   var t=Platform6BankB1Replay.Find(Native5);var mc=t.GetComponent<MeshCollider>();Need(mc!=null&&mc.enabled&&!mc.isTrigger&&!mc.convex,"actual native5 owner MC required");Need(request["rows"].Count()==1246&&(string)request["recipe_sha256"]==RecipeSHA,"exact retired face probe scope");
   var sourceFaceMap=request["remaining_current_triangle_to_original_native5_source_face"].Select(x=>(int)x).ToArray();var retired=new HashSet<int>(request["rows"].Select(x=>(int)x["original_native5_source_face"]));var vertices=mc.sharedMesh.vertices;var triangles=mc.sharedMesh.triangles;
   Need(sourceFaceMap.Length==56982&&sourceFaceMap.Length*3==triangles.Length&&sourceFaceMap.Distinct().Count()==sourceFaceMap.Length&&!sourceFaceMap.Any(retired.Contains),"exact current remaining source-face attribution map");
   var samples=new JArray();int degenerate=0,verified=0,invalid=0,coincident=0,attributedRetained=0;bool previous=Physics.queriesHitBackfaces;Physics.SyncTransforms();
   try
   {
    Physics.queriesHitBackfaces=false;
    foreach(var q in request["rows"])
    {
     if(q["world_normal"].Type==JTokenType.Null){degenerate++;samples.Add(new JObject{["original_face"]=q["original_native5_source_face"].DeepClone(),["status"]="NO_NONDEGENERATE_SURFACE_IN_ORIGINAL",["raycast_executed"]=false});continue;}
     Vector3 center=Platform6BankB1Replay.V(q["world_center"]),normal=Platform6BankB1Replay.V(q["world_normal"]);Need(Mathf.Abs(normal.magnitude-1)<.00001f,"finite unit original world surface normal");var rays=new JArray();bool pass=true;
     foreach(float side in new[]{1f,-1f})
     {
      var ray=new Ray(center+normal*(side*.05f),normal*(-side));bool hit=mc.Raycast(ray,out var h,.1f);bool atOldSurface=hit&&Vector3.Distance(h.point,center)<=.001f;bool valid=true;float currentTriangleDistance=0;int originalFace=-1;
      if(hit)
      {
       valid=h.triangleIndex>=0&&h.triangleIndex<sourceFaceMap.Length;
       if(valid)
       {
        int k=h.triangleIndex*3;Vector3 a=t.TransformPoint(vertices[triangles[k]]),b=t.TransformPoint(vertices[triangles[k+1]]),c=t.TransformPoint(vertices[triangles[k+2]]);currentTriangleDistance=Vector3.Distance(h.point,ClosestTriangle(h.point,a,b,c));originalFace=sourceFaceMap[h.triangleIndex];valid=float.IsFinite(currentTriangleDistance)&&currentTriangleDistance<=.001f&&!retired.Contains(originalFace);
       }
       if(valid)attributedRetained++;if(atOldSurface)coincident++;
      }
      // A preserved floor may coincide with an old frame plane. Attribute it; never remove it to make this QA pass.
      pass&=valid;rays.Add(new JObject{["side"]=side,["hit"]=hit,["hit_triangle"]=hit?(JToken)new JValue(h.triangleIndex):JValue.CreateNull(),["hit_distance"]=hit?(JToken)new JValue(h.distance):JValue.CreateNull(),["contact_at_retired_surface_diagnostic_only"]=atOldSurface,["retained_original_native5_source_face"]=hit&&valid?(JToken)new JValue(originalFace):JValue.CreateNull(),["distance_to_current_retained_triangle_m"]=hit?(JToken)new JValue(currentTriangleDistance):JValue.CreateNull(),["current_retained_geometry_verified"]=valid});
     }
     if(pass)verified++;else invalid++;samples.Add(new JObject{["original_face"]=q["original_native5_source_face"].DeepClone(),["B24_terminal_face"]=q["B24_terminal_face"].DeepClone(),["source_world_center"]=q["world_center"].DeepClone(),["passed"]=pass,["rays"]=rays});
    }
   }finally{Physics.queriesHitBackfaces=previous;}
   Need(UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle==handle&&Hash(scenePath)==saved&&scene.isDirty==dirty,"actual QA must preserve Scene handle/saved bytes/dirty flag");Need(degenerate==2&&verified+invalid==1244,"all nondegenerate retired source faces must be probed");
   return new JObject{["status"]=invalid==0?"PASS":"FAIL",["exact_bindings"]=exact,["physical_contract_sha256"]=PhysicalContractSHA,["owner_only_MeshCollider_Raycast_executed"]=true,["actual_ray_count"]=2*(verified+invalid),["nondegenerate_retired_faces"]=verified+invalid,["verified_current_geometry_samples"]=verified,["invalid_or_unattributed_current_geometry_samples"]=invalid,["retained_original_face_hits"]=attributedRetained,["coincident_old_plane_hits_diagnostic_only"]=coincident,["original_degenerate_faces_skipped"]=degenerate,["samples"]=samples,["human_clearance_or_full_stair_traversal_verified"]=false,["SceneSave_executed"]=false,["site99percent_verified"]=false};
  }
  static Vector3 ClosestSegment(Vector3 p,Vector3 a,Vector3 b){var d=b-a;float size=d.sqrMagnitude;return size<=1e-20f?a:a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/size);}
  static Vector3 ClosestTriangle(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
  {
   var ab=b-a;var ac=c-a;if(Vector3.Cross(ab,ac).sqrMagnitude<=1e-20f){var candidates=new[]{ClosestSegment(p,a,b),ClosestSegment(p,b,c),ClosestSegment(p,c,a)};return candidates.OrderBy(q=>(p-q).sqrMagnitude).First();}
   var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));float va=d3*d6-d5*d4;if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));float den=va+vb+vc;Need(float.IsFinite(den)&&den!=0,"nondegenerate current hit triangle");return a+ab*(vb/den)+ac*(vc/den);
  }
 }
}
#endif
