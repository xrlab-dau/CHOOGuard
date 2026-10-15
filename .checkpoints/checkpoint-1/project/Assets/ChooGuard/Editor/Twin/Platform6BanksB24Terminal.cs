#if UNITY_EDITOR
// Draft exact successor adapter. Root owns installation, atomic rollback and sole Scene save.
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
namespace ChooGuard.Editor.Twin
{
 public static class Platform6BanksB24Terminal
 {
  public const string Registry="content/world/observed-interiors/platform6-B2-B4-source-banks.json";
  const string Recipe="workers/twin/output/platform6-interior-object-audit-20261010/B2-B4-followup/B1-contact-rebased/B2-B4-coherent-source-architecture-candidates.json";
  const string RecipeSHA="da8a6566605e9000e5bd253082ec91f64df3de16334c5f749f268a4cd86d8bb9";
  const string Proof="workers/twin/output/platform6-interior-object-audit-20261010/B2-B4-followup/B1-contact-rebased/B2-B4-coherent-source-architecture-independent-proof.json";
  const string ProofSHA="35532e15b5521b2f811d22a17e25d6666bf37615cfa950c4d5df20917ae5b468";
  const string FloorProof="workers/twin/output/platform6-interior-object-audit-20261010/B2-B4-followup/B1-contact-rebased/B2-B4-contact-rebased-independent-proof.json";
  const string FloorProofSHA="70f8cf7039c64caa05c543e9c41c3bf341304dbfb832faff920669039551b127";
  const string Native6="FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_6";
  const string Main="맞이방 · 원본 정합/Majibang_2F_Floor";
  const string Structural="content/world/observed-interiors/structural-corrections.json";
  static void Need(bool b,string why)=>Platform6BankB1Replay.Need(b,"B2/B4 successor: "+why);
  static string Hash(string p)=>Platform6BankB1Replay.Hash(p);
  static string Complete(Mesh m)=>Platform6BankB1Replay.Complete(m);
  static string Owner(JToken row)=>row["owner"].Type==JTokenType.String?(string)row["owner"]:(string)row["owner"]["path"];
  static JObject Pins()
  {
   Need(Hash(Recipe)==RecipeSHA&&Hash(Proof)==ProofSHA&&Hash(FloorProof)==FloorProofSHA,"sealed exact geometry/proofs changed");var j=JObject.Parse(File.ReadAllText(Recipe));var p=JObject.Parse(File.ReadAllText(Proof));var f=JObject.Parse(File.ReadAllText(FloorProof));
   Need((string)p["status"]=="PASS"&&(string)p["packet_sha256"]==RecipeSHA&&(string)f["status"]=="PASS"&&p["rows"].All(x=>(string)x["status"]=="PASS")&&f["rows"].All(x=>(string)x["status"]=="PASS"),"independent exact source/channel proofs required");
   Need(j["rows"].Count()==5&&j["rows"].Select(x=>(string)x["name"]).Distinct().Count()==5,"only exact five native5/native6/Main role successors");
   foreach(string k in new[]{"source_plan","frame","predecessor_contact_recipe"})Need(Hash((string)j[k])==(string)j[k+"_sha256"],"source/frame/contact recipe pin "+k);
   foreach(var row in j["rows"]){Need(Hash((string)row["source_binary"])==(string)row["source_binary_sha256"]&&Hash((string)row["replacement_binary"])==(string)row["replacement_binary_sha256"],"exact predecessor/successor fullchannel binary");if(((string)row["name"]).StartsWith("native6-",StringComparison.Ordinal))Need(row["body_retired_faces"].Count()==2107&&row["source_aperture_clipped_faces"].Count()==34&&row["g40_architecture_revision"]["changed_source_faces"].Count()==17,"exact B2 body and B2/B4 source aperture/architecture scopes");}
   return j;
  }
  public static JObject Read()
  {
   if(!File.Exists(Registry))
   {
    // Optional replay may no-op before first install, never after pins/assets remain.
    var j=Pins();foreach(var row in j["rows"])Need(!File.Exists((string)row["replacement_asset"]),"successor asset lost exact installed registry");
    if(File.Exists(Platform6BankB1Replay.Legacy))Need(!JObject.Parse(File.ReadAllText(Platform6BankB1Replay.Legacy))["meshes"].Any(x=>x["platform6_B24_source_bank_composition"]!=null),"Legacy successor lost registry");
    if(File.Exists(Structural))Need(!JObject.Parse(File.ReadAllText(Structural))["entries"].Any(x=>x["platform6_B24_source_bank_composition"]!=null),"Main structural successor lost registry");return null;
   }
   var r=JObject.Parse(File.ReadAllText(Registry));Need((string)r["schema"]=="chooguard.platform6-B2-B4-installed-source-banks.v1"&&(string)r["recipe"]==Recipe&&(string)r["recipe_sha256"]==RecipeSHA&&(string)r["proof"]==Proof&&(string)r["proof_sha256"]==ProofSHA&&(string)r["floor_proof"]==FloorProof&&(string)r["floor_proof_sha256"]==FloorProofSHA&&r["terminal_rows"].Count()==5&&r["native_field_states"].Count()==3,"installed exact successor provenance");return r;
  }
  static void Digests(Mesh mesh,JToken dig)=>Need(mesh!=null&&mesh.isReadable&&!EditorUtility.IsDirty(mesh)&&InteriorEnvelopeRepair.MeshDigest(mesh)==(string)dig["mesh_sha256"]&&ArchitecturalCorrectionRegistry.ChannelDigest(mesh)==(string)dig["channels_sha256"]&&Complete(mesh)==(string)dig["complete_channels_sha256"],"clean exact fullchannel mesh digest");
  static Mesh Asset(JToken pin,string expectedPath,JToken dig)
  {
   string path=(string)pin["asset"];Need(path==expectedPath&&File.Exists(path)&&Hash(path)==(string)pin["file_sha256"]&&Hash(path+".meta")== (string)pin["meta_sha256"]&&AssetDatabase.AssetPathToGUID(path)==(string)pin["guid"],"retained saved asset identity");var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);Digests(mesh,dig);return mesh;
  }
  static JToken Legacy(JToken pin,JToken row)
  {
   string role=(string)row["name"];var prior=pin["predecessor_legacy_row"];var final=pin["final_legacy_row"];var expected=(JObject)prior.DeepClone();
   foreach(string k in new[]{"replacement_asset","replacement_guid","replacement_file_sha256","replacement_meta_sha256","replacement_complete_channel_digest","replacement_channel_digest","replacement_mesh_digest","replacement_binary","replacement_vertex_count","replacement_triangle_count","platform6_B24_source_bank_composition"})expected[k]=final[k]?.DeepClone();Need(JToken.DeepEquals(expected,final),"unrelated Legacy policy/source history changed");
   Need((string)final["replacement_guid"]==(string)pin["terminal_asset"]["guid"]&&(string)final["replacement_file_sha256"]==(string)pin["terminal_asset"]["file_sha256"]&&(string)final["replacement_meta_sha256"]==(string)pin["terminal_asset"]["meta_sha256"],"Legacy terminal saved asset pins");Need((string)final["replacement_asset"]==(string)row["replacement_asset"]&&(string)final["replacement_complete_channel_digest"]==(string)row["replacement_unity_digests"]["complete_channels_sha256"]&&(string)final["replacement_binary"]["path"]==(string)row["replacement_binary"]&&(string)final["replacement_binary"]["sha256"]==(string)row["replacement_binary_sha256"],"Legacy exact terminal channel/binary pins");
   var marker=final["platform6_B24_source_bank_composition"];Need(marker!=null&&(string)marker["recipe"]==Recipe&&(string)marker["recipe_sha256"]==RecipeSHA&&JToken.DeepEquals(marker["previous_terminal"],prior),"Legacy immutable predecessor chain");
   var actual=JObject.Parse(File.ReadAllText(Platform6BankB1Replay.Legacy))["meshes"].Single(x=>(string)x["id"]==(string)prior["id"]);var current=(role=="native5-render"||role=="native5-collider")?Platform6BluePartitionTerminal.ResolveB24Legacy(final,role):(role=="native6-render"||role=="native6-collider")?Platform6B4BlackPaneCorrection.ResolveB24Legacy(final,role):final;Need(JToken.DeepEquals(actual,current),"exact current Legacy terminal row or pinned blue-only successor");return current;
  }
  static JToken MainStructural(JToken pin,JToken row)
  {
   var prior=pin["predecessor_structural_row"];Need(JToken.DeepEquals(prior,row["structural_terminal_predecessor_row"]),"retained SourceX then continuous-seam structural predecessor");var final=pin["final_structural_row"];var expected=(JObject)prior.DeepClone();foreach(string k in new[]{"replacement_asset","replacement_guid","replacement_file_sha256","replacement_meta_sha256","replacement_channels_sha256","replacement_complete_channels_sha256","platform6_B24_source_bank_composition"})expected[k]=final[k]?.DeepClone();Need(JToken.DeepEquals(expected,final),"Main source/X/seam history or unrelated fields changed");
   Need((string)final["replacement_guid"]==(string)pin["terminal_asset"]["guid"]&&(string)final["replacement_file_sha256"]==(string)pin["terminal_asset"]["file_sha256"]&&(string)final["replacement_meta_sha256"]==(string)pin["terminal_asset"]["meta_sha256"],"Main terminal saved asset pins");Need((string)final["replacement_asset"]==(string)row["replacement_asset"]&&(string)final["replacement_complete_channels_sha256"]==(string)row["replacement_unity_digests"]["complete_channels_sha256"],"Main exact terminal channels");var marker=final["platform6_B24_source_bank_composition"];Need(marker!=null&&(string)marker["recipe"]==Recipe&&(string)marker["recipe_sha256"]==RecipeSHA&&JToken.DeepEquals(marker["previous_terminal"],prior),"Main exact predecessor composition marker");var actual=JObject.Parse(File.ReadAllText(Structural))["entries"].Single(x=>(string)x["source_asset"]==(string)prior["source_asset"]);Need(JToken.DeepEquals(actual,final),"Main exact current structural terminal row");return final;
  }
  static JToken Pair(JObject registry,string role,out Mesh previous,out Mesh terminal,JObject checkedPins=null)
  {
   var j=checkedPins??Pins();var row=j["rows"].Single(x=>(string)x["name"]==role);var pin=registry["terminal_rows"].Single(x=>(string)x["name"]==role);Need((string)pin["owner_path"]==Owner(row),"exact role owner");previous=Asset(pin["predecessor_asset"],(string)row["source_asset"],row["source_unity_digests"]);terminal=Asset(pin["terminal_asset"],(string)row["replacement_asset"],row["replacement_unity_digests"]);
   if(role.StartsWith("native6-",StringComparison.Ordinal))
   {
    Need(File.Exists(Platform6B1ContactTerminal.Registry)&&File.Exists(Platform6BankB1Replay.Registry),"accepted complete B1 and contact predecessor required");var contact=JObject.Parse(File.ReadAllText(Platform6B1ContactTerminal.Registry));var c=contact["rows"].Single(x=>(string)x["name"]==role);Need(JToken.DeepEquals(c,pin["predecessor_contact_installed_row"])&&JToken.DeepEquals(c["final_legacy_row"],pin["predecessor_legacy_row"]),"immutable installed B1 contact predecessor record");var b=Platform6BankB1Replay.Read();Need(b!=null&&JToken.DeepEquals(c["frozen_B1_asset_row"],b["assets"].Single(x=>(string)x["name"]==role)),"immutable first B1 complement chain");
    Need((string)c["asset"]==(string)pin["predecessor_asset"]["asset"]&&(string)c["file_sha256"]==(string)pin["predecessor_asset"]["file_sha256"]&&(string)c["meta_sha256"]==(string)pin["predecessor_asset"]["meta_sha256"]&&(string)c["guid"]==(string)pin["predecessor_asset"]["guid"],"retained contact asset identity binding");Legacy(pin,row);
   }
   else if(role=="main-floor-render-collider"){var old=row["structural_terminal_predecessor_row"];Need((string)pin["predecessor_asset"]["guid"]==(string)old["replacement_guid"]&&(string)pin["predecessor_asset"]["file_sha256"]==(string)old["replacement_file_sha256"]&&(string)pin["predecessor_asset"]["meta_sha256"]==(string)old["replacement_meta_sha256"],"retained Main seam exact prior asset pins");MainStructural(pin,row);}
   else{Need((string)pin["predecessor_asset"]["guid"]==(string)row["legacy_row"]["replacement_guid"]&&(string)pin["predecessor_asset"]["file_sha256"]==(string)row["legacy_row"]["replacement_file_sha256"]&&(string)pin["predecessor_asset"]["meta_sha256"]==(string)row["legacy_row"]["replacement_meta_sha256"],"native5 retained source asset pins");Need(JToken.DeepEquals(row["legacy_row"],pin["predecessor_legacy_row"]),"native5 exact prior source and scoped history");Legacy(pin,row);}if(role=="native5-render"||role=="native5-collider")terminal=Platform6BluePartitionTerminal.ResolveB24NativeCarrier(terminal,role,Owner(row));return pin;
  }
  public static Mesh ResolveContactCarrier(Mesh reviewedContact,string role,string ownerPath)
  {
   Need((role=="native6-render"||role=="native6-collider")&&ownerPath==Native6,"exact contact successor role/owner only");var r=Read();if(r==null)return reviewedContact;Pair(r,role,out var previous,out var terminal);Need(reviewedContact==previous,"exact retained B1 contact mesh required");return Platform6B4BlackPaneCorrection.ResolveB24NativeCarrier(terminal,role,ownerPath);
  }
  public static JToken ResolveContactLegacy(JToken reviewedContactRow,string role)
  {
   Need(role=="native6-render"||role=="native6-collider","exact contact Legacy role only");var r=Read();if(r==null)return reviewedContactRow;var pin=Pair(r,role,out var previous,out var terminal);Need(JToken.DeepEquals(reviewedContactRow,pin["predecessor_legacy_row"]),"exact retained contact Legacy input required");return Platform6B4BlackPaneCorrection.ResolveB24Legacy(pin["final_legacy_row"],role);
  }
  public static Mesh ResolveMainCarrier(Mesh reviewedSeam,string ownerPath)
  {
   Need(ownerPath==Main,"exact continuous-seam owner only");var r=Read();if(r==null)return reviewedSeam;Pair(r,"main-floor-render-collider",out var previous,out var terminal);Need(reviewedSeam==previous,"retained continuous-seam asset required");return terminal;
  }
  public static void RequireMainFloorCarrier(Mesh actual,string reviewedSeamDigest,string ownerPath)
  {
   Need(ownerPath==Main&&reviewedSeamDigest=="24a6641ee05d7d67ca614ed3614752560f6291e4dc4ba4e7fa7ea134061301ed","exact B1 floor main-source predecessor only");var r=Read();if(r==null){Need(actual!=null&&!EditorUtility.IsDirty(actual)&&Complete(actual)==reviewedSeamDigest,"unregistered exact continuous seam required");return;}Pair(r,"main-floor-render-collider",out var previous,out var terminal);Need(Complete(previous)==reviewedSeamDigest&&actual==terminal,"Main registered B4 source-aperture successor mandatory");
  }
  public static JToken ReviewedSeamStructuralRow(JToken actualCurrentRow)
  {
   var r=Read();if(r==null)return actualCurrentRow;var pin=Pair(r,"main-floor-render-collider",out var previous,out var terminal);Need(JToken.DeepEquals(actualCurrentRow,pin["final_structural_row"]),"exact current Main structural row required");return pin["predecessor_structural_row"];
  }
  public static JObject Verify()
  {
   var r=Read();Need(r!=null,"installed successor registry required at Verify");var j=Pins();foreach(var row in j["rows"])
   {
    string role=(string)row["name"];Pair(r,role,out var previous,out var terminal,j);if(role=="native6-render"||role=="native6-collider")terminal=Platform6B4BlackPaneCorrection.ResolveB24NativeCarrier(terminal,role,Owner(row));var t=Platform6BankB1Replay.Find(Owner(row));Platform6BankB1Replay.Frame(t,row["world_matrix"]);Need(t.gameObject.activeInHierarchy,"successor owner inactive");var state=r["native_field_states"].Single(x=>(string)x["owner_path"]==Owner(row));Need(JToken.DeepEquals(Platform6B4BlackPaneCorrection.ReviewedB24CarrierFields(Platform6BluePartitionTerminal.ReviewedB24CarrierFields(Platform6BanksB24PlacementReplay.CarrierFields(t),Owner(row)),Owner(row)),state["fields"]),"exact native source owner fields/materials/settings");bool render=role.EndsWith("-render",StringComparison.Ordinal)||role=="main-floor-render-collider";bool collision=role.EndsWith("-collider",StringComparison.Ordinal)||role=="main-floor-render-collider";
    if(render){var mf=t.GetComponent<MeshFilter>();var mr=t.GetComponent<MeshRenderer>();Need(mf!=null&&mf.sharedMesh==terminal&&mr!=null&&mr.enabled&&mr.additionalVertexStreams==null,"exact successor render binding/policy");}if(collision){var mc=t.GetComponent<MeshCollider>();Need(mc!=null&&mc.sharedMesh==terminal&&mc.enabled&&!mc.isTrigger&&!mc.convex&&(int)mc.cookingOptions==30,"exact successor collision binding/policy");}
   }
   return new JObject{["status"]="PASS",["verification_kind"]="exact_complete_B1_then_contact_then_B2_B4_and_SourceX_then_continuous_seam_then_B4_carrier_chains",["source_physical_contracts_preserved"]=true,["actual_PhysX_executed"]=false,["SceneSave_executed"]=false,["site99percent_verified"]=false};
  }
 }
}
#endif
