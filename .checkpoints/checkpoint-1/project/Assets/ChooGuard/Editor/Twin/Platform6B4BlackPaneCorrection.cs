#if UNITY_EDITOR
// Offline draft. Root owns actual application, rollback, and the only Scene save.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
namespace ChooGuard.Editor.Twin
{
 public static class Platform6B4BlackPaneCorrection
 {
  public const string Registry="content/world/observed-interiors/platform6-B4-source-black-pane.json";
  const string Recipe="workers/twin/output/platform6-B4-visual-obstruction-review-20261010/B4-black-pane-candidate-recipe.json";
  const string RecipeSHA="45e06bf4f211105414d8d701580a52ffe9cd5af89f32648afa24c56e7dcfa381";
  const string Physical="workers/twin/output/platform6-B4-visual-obstruction-review-20261010/B4-black-pane-physical-contract.json";
  const string PhysicalSHA="f638b02a6e6b5d454549d94b5d86d35fcff9279946d7bab4f8b20bc74a91e03c";
  const string Proof="workers/twin/output/platform6-blue-only-independent-review-20261010/independent-surface-geometry-proof.json";
  const string ProofSHA="f1b99ff2fe4a3fdaaf91c3b5910013cc1d8e3fb7263661ac617fb77315f8abb7";
  const string ScenePath="Assets/ChooGuard/Scenes/FpsStation.unity";
  const string Native="FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_6";
  const string Marker="platform6_B4_black_pane_composition";
  static readonly string[] ChangedKeys={"replacement_asset","replacement_guid","replacement_file_sha256","replacement_meta_sha256","replacement_complete_channel_digest","replacement_channel_digest","replacement_mesh_digest","replacement_binary","replacement_vertex_count","replacement_triangle_count",Marker};
  static void Need(bool b,string why)=>Platform6BankB1Replay.Need(b,"B4 exact blackpane: "+why);
  static string Hash(string p)
  {
   using(var hash=System.Security.Cryptography.SHA256.Create())
   using(var stream=File.OpenRead(p))
    return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
  }
  static string Complete(Mesh m)=>Platform6BankB1Replay.Complete(m);
  static Transform Find(string p)=>Platform6BankB1Replay.Find(p);
  static string PathOf(Transform t)=>Platform6BankB1Replay.PathOf(t);
  static JObject Json(string p)=>JObject.Parse(File.ReadAllText(p));
  static Vector3 V(JToken a)=>new Vector3((float)a[0],(float)a[1],(float)a[2]);
  static JArray PV(Vector3 v)=>new JArray(v.x,v.y,v.z);
  static JObject Pins()
  {
   Need(Hash(Recipe)==RecipeSHA&&Hash(Physical)==PhysicalSHA&&Hash(Proof)==ProofSHA,"sealed recipe/proof/physics contract changed");var j=Json(Recipe);var proof=Json(Proof);Need((string)proof["status"]=="PASS"&&proof["candidate_pins"].Count(x=>(string)x["path"]==Recipe&&(string)x["sha256"]==RecipeSHA)==1,"independent exact geometry proof required");
   Need((string)j["source_id"]=="68caeb28da0fddcd96ea7bd0"&&(string)j["source_path"]=="ROOT/g58/g62/g2/geometry"&&j["rows"].Count()==2,"only the attributed black plane and two native6 roles");
   foreach(string k in new[]{"source_plan","frame","source_archive_geometry","source_archive","source_alignment","objects_jsonl","source_material","predecessor_B24_recipe","predecessor_B24_registry","actual_before_render_probe"})Need(Hash((string)j[k])==(string)j[k+"_sha256"],"immutable source pin "+k);
   foreach(var row in j["rows"]){string role=(string)row["name"];Need(role=="native6-render"||role=="native6-collider","exact role");Need((string)row["owner_path"]==Native&&row["source_object_all_four_current_faces"].Count()==4&&row["source_faces"].Count()==3&&(int)row["new_vertex_count"]==(int)row["old_vertex_count"]+12&&(int)row["new_triangle_count"]==(int)row["old_triangle_count"]+6,"exact four-source-face/three-clipped-face scope");Need(Hash((string)row["source_binary"])==(string)row["source_binary_sha256"]&&Hash((string)row["replacement_binary"])==(string)row["replacement_binary_sha256"],"exact predecessor/successor binaries");}
   var c=Json(Physical);Need((string)c["recipe_sha256"]==RecipeSHA&&c["clear_headspace_samples"].Count()==12&&c["retained_north_and_service_samples"].Count()==9,"same21 horizontal source controls");return j;
  }
  public static JObject Read()
  {
   if(!File.Exists(Registry)){var j=Pins();foreach(var row in j["rows"])Need(!File.Exists((string)row["replacement_asset"]),"successor asset lost registry");if(File.Exists(Platform6BankB1Replay.Legacy))Need(!Json(Platform6BankB1Replay.Legacy)["meshes"].Any(x=>x[Marker]!=null),"Legacy successor lost registry");return null;}
   var r=Json(Registry);Need((string)r["schema"]=="chooguard.platform6-B4-installed-black-pane-source-aperture.v1"&&(string)r["recipe"]==Recipe&&(string)r["recipe_sha256"]==RecipeSHA&&(string)r["proof"]==Proof&&(string)r["proof_sha256"]==ProofSHA&&(string)r["physical_contract"]==Physical&&(string)r["physical_contract_sha256"]==PhysicalSHA&&r["terminal_rows"].Count()==2,"exact installed blackpane provenance");return r;
  }
  static void Digests(Mesh m,JToken d)=>Need(m!=null&&m.isReadable&&!EditorUtility.IsDirty(m)&&InteriorEnvelopeRepair.MeshDigest(m)==(string)d["mesh_sha256"]&&ArchitecturalCorrectionRegistry.ChannelDigest(m)==(string)d["channels_sha256"]&&Complete(m)==(string)d["complete_channels_sha256"],"clean exact fullchannels");
  static JObject Pin(Mesh m){string p=AssetDatabase.GetAssetPath(m);Need(!string.IsNullOrEmpty(p),"saved asset required");return new JObject{["asset"]=p,["guid"]=AssetDatabase.AssetPathToGUID(p),["file_sha256"]=Hash(p),["meta_sha256"]=Hash(p+".meta")};}
  static Mesh Asset(JToken p,string expected,JToken dig){string path=(string)p["asset"];Need(path==expected&&Hash(path)==(string)p["file_sha256"]&&Hash(path+".meta")== (string)p["meta_sha256"]&&AssetDatabase.AssetPathToGUID(path)==(string)p["guid"],"exact saved mesh identity");var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);Digests(m,dig);return m;}
  static JToken Pair(JObject r,string role,out Mesh previous,out Mesh terminal,JObject checkedPins=null)
  {
   Need(role=="native6-render"||role=="native6-collider","exact native6 role");var j=checkedPins??Pins();var row=j["rows"].Single(x=>(string)x["name"]==role);var p=r["terminal_rows"].Single(x=>(string)x["name"]==role);var b24=Json(Platform6BanksB24Terminal.Registry)["terminal_rows"].Single(x=>(string)x["name"]==role);Need((string)p["owner_path"]==Native&&JToken.DeepEquals(b24,row["source_B24_record"])&&JToken.DeepEquals(p["immutable_B24_installed_row"],b24),"entire immutable B24 predecessor retained");
   previous=Asset(p["predecessor_asset"],(string)row["source_asset"],row["source_unity_digests"]);terminal=Asset(p["terminal_asset"],(string)row["replacement_asset"],row["replacement_unity_digests"]);Need(JToken.DeepEquals(p["predecessor_asset"],b24["terminal_asset"])&&JToken.DeepEquals(p["previous_legacy_row"],b24["final_legacy_row"]),"exact predecessor asset/Legacy pins");
   var final=p["final_legacy_row"];var expected=FinalLegacy(p["previous_legacy_row"],row,(JObject)p["terminal_asset"]);Need(JToken.DeepEquals(final,expected),"only exact policy successor keys changed; all unrelated history retained");var actual=Json(Platform6BankB1Replay.Legacy)["meshes"].Single(x=>(string)x["id"]==(string)final["id"]);Need(JToken.DeepEquals(actual,final),"exact unique current Legacy row");return p;
  }
  public static Mesh ResolveB24NativeCarrier(Mesh reviewedB24,string role,string ownerPath)
  {Need(ownerPath==Native,"exact source owner");var r=Read();if(r==null)return reviewedB24;Pair(r,role,out var previous,out var terminal);Need(reviewedB24==previous,"exact immutable B24 input mesh");return ConcourseFidelitySuccessor.Resolve(terminal,role,ownerPath);}
  public static JToken ResolveB24Legacy(JToken reviewedB24,string role)
  {var r=Read();if(r==null)return reviewedB24;var p=Pair(r,role,out var previous,out var terminal);Need(JToken.DeepEquals(reviewedB24,p["previous_legacy_row"]),"exact entire B24 Legacy input");return p["final_legacy_row"];}
  public static JObject ReviewedB24CarrierFields(JObject actual,string ownerPath)
  {
   if(ownerPath!=Native)return actual;actual=ConcourseFidelitySuccessor.ReviewedFields(actual,ownerPath);var r=Read();if(r==null)return actual;Need(JToken.DeepEquals(actual,r["native_fields_after"]),"exact final native6 serialized fields");var prior=(JObject)r["native_fields_before"].DeepClone();var frozen=Json(Platform6BanksB24Terminal.Registry)["native_field_states"].Single(x=>(string)x["owner_path"]==Native)["fields"];Need(JToken.DeepEquals(prior,frozen),"entire native6 original field state equals immutable accepted B24 snapshot");var expected=(JObject)prior.DeepClone();foreach(string k in new[]{"filter","collider"}){string wrapper=k=="filter"?"MeshFilter":"MeshCollider";Need(expected[k]?[wrapper]is JObject&&actual[k]?[wrapper]is JObject,"exact native6 component wrappers");expected[k][wrapper]["m_Mesh"]=actual[k][wrapper]["m_Mesh"].DeepClone();}Need(JToken.DeepEquals(expected,actual),"only two native6 mesh references changed");return prior;
  }
  static Component Binding(string role,bool exactIDs=false)
  {var t=Find(Native);var c=role.EndsWith("-render",StringComparison.Ordinal)?(Component)t.GetComponent<MeshFilter>():t.GetComponent<MeshCollider>();Need(c!=null,"native6 binding");if(exactIDs)Need((long)GlobalObjectId.GetGlobalObjectIdSlow(c).targetObjectId==(role.EndsWith("-render",StringComparison.Ordinal)?438379229L:438379227L),"exact saved component IDs");return c;}
  static Mesh Get(Component c)=>c is MeshFilter f?f.sharedMesh:((MeshCollider)c).sharedMesh;
  static void Bind(Component c,Mesh m){if(c is MeshFilter f)f.sharedMesh=m;else((MeshCollider)c).sharedMesh=m;EditorUtility.SetDirty(c);}
  public sealed class Prepared
  {
   internal string Epoch;internal int Handle;internal bool Applied;internal JObject NativeFields;internal JArray BeforeControls;
   internal readonly Dictionary<string,Mesh> Source=new Dictionary<string,Mesh>(),Mesh=new Dictionary<string,Mesh>();internal readonly Dictionary<string,JToken> Prior=new Dictionary<string,JToken>(),B24=new Dictionary<string,JToken>();
   public readonly List<string> CreatedAssets=new List<string>();
   public string[] CanonicalFiles=>new[]{Platform6BankB1Replay.Legacy,Registry};
  }
  public static string[] PreparedCanonicalFiles()=>new[]{Platform6BankB1Replay.Legacy,Registry};
  public static Prepared Prepare()=>Prepare(Hash(ScenePath));
  public static Prepared Prepare(string expectedSavedSceneSHA256)
  {
   var s=SceneManager.GetActiveScene();Need(s.path==ScenePath&&!s.isDirty&&!EditorApplication.isCompiling&&!EditorApplication.isPlayingOrWillChangePlaymode&&Hash(ScenePath)==expectedSavedSceneSHA256,"clean stable exact saved FpsStation at prepare");var j=Pins();Need(!File.Exists(Registry)&&j["rows"].All(r=>!File.Exists((string)r["replacement_asset"])&&!File.Exists((string)r["replacement_asset"]+".meta")),"fresh registry and exact two assets");Platform6BanksB24Terminal.Verify();var p=new Prepared{Epoch=expectedSavedSceneSHA256,Handle=s.handle,NativeFields=Platform6BanksB24PlacementReplay.CarrierFields(Find(Native)),BeforeControls=CaptureBefore()};
   try{foreach(var row in j["rows"]){string role=(string)row["name"];var current=Get(Binding(role,true));Need(JToken.DeepEquals(Pin(current),row["source_asset_pin"]),"exact B24 source asset pins");Digests(current,row["source_unity_digests"]);p.Source[role]=current;var b24=Json(Platform6BanksB24Terminal.Registry)["terminal_rows"].Single(x=>(string)x["name"]==role);p.B24[role]=b24.DeepClone();p.Prior[role]=b24["final_legacy_row"].DeepClone();Need(JToken.DeepEquals(Json(Platform6BankB1Replay.Legacy)["meshes"].Single(x=>(string)x["id"]==(string)p.Prior[role]["id"]),p.Prior[role]),"exact unique initial B24 Legacy row");var old=ReadNative((string)row["source_binary"]);var fresh=ReadNative((string)row["replacement_binary"]);Prefix(old,fresh);var clone=UnityEngine.Object.Instantiate(current);clone.name="Platform6_B4_BlackPane_"+role;ApplyNative(clone,fresh,current,old);EditorUtility.ClearDirty(clone);Digests(clone,row["replacement_unity_digests"]);p.Mesh[role]=clone;}Need(Hash(ScenePath)==p.Epoch&&s.handle==p.Handle&&!s.isDirty&&JToken.DeepEquals(p.NativeFields,Platform6BanksB24PlacementReplay.CarrierFields(Find(Native))),"prepare preserved scene fields/epoch");return p;}catch{DisposePrepared(p);throw;}
  }
  static JObject FinalLegacy(JToken prior,JToken row,JObject asset)
  {var f=(JObject)prior.DeepClone();foreach(string k in new[]{"asset","guid","file_sha256","meta_sha256"})f["replacement_"+k]=asset[k].DeepClone();f["replacement_complete_channel_digest"]=row["replacement_unity_digests"]["complete_channels_sha256"].DeepClone();f["replacement_channel_digest"]=row["replacement_unity_digests"]["channels_sha256"].DeepClone();f["replacement_mesh_digest"]=row["replacement_unity_digests"]["mesh_sha256"].DeepClone();f["replacement_binary"]=new JObject{["path"]=row["replacement_binary"].DeepClone(),["sha256"]=row["replacement_binary_sha256"].DeepClone()};f["replacement_vertex_count"]=row["new_vertex_count"].DeepClone();f["replacement_triangle_count"]=row["new_triangle_count"].DeepClone();f[Marker]=new JObject{["recipe"]=Recipe,["recipe_sha256"]=RecipeSHA,["proof"]=Proof,["proof_sha256"]=ProofSHA,["previous_terminal"]=prior.DeepClone()};return f;}
  public static JObject ApplyPreparedWithoutSave(Prepared p)
  {
   Need(p!=null&&!p.Applied,"fresh prepared correction");var s=SceneManager.GetActiveScene();Need(s.path==ScenePath&&s.handle==p.Handle&&Hash(ScenePath)==p.Epoch&&!EditorApplication.isCompiling&&!EditorApplication.isPlayingOrWillChangePlaymode,"same saved epoch; outer scene dirty is allowed");var j=Pins();Need(!File.Exists(Registry)&&JToken.DeepEquals(Platform6BanksB24PlacementReplay.CarrierFields(Find(Native)),p.NativeFields),"native6 unchanged before its exact stage");var legacy=Json(Platform6BankB1Replay.Legacy);var before=(JObject)legacy.DeepClone();var pins=new JArray();
   foreach(var row in j["rows"]){string role=(string)row["name"];Need(Get(Binding(role,true))==p.Source[role],"exact live prepared mesh predecessor");Digests(p.Source[role],row["source_unity_digests"]);Need(JToken.DeepEquals(Json(Platform6BanksB24Terminal.Registry)["terminal_rows"].Single(x=>(string)x["name"]==role),p.B24[role]),"immutable complete B24 predecessor record");var actual=legacy["meshes"].Single(x=>(string)x["id"]==(string)p.Prior[role]["id"]);Need(JToken.DeepEquals(actual,p.Prior[role]),"unique native6 Legacy predecessor unchanged");string path=(string)row["replacement_asset"];Need(!File.Exists(path)&&!File.Exists(path+".meta"),"exact fresh successor asset");p.CreatedAssets.Add(path);AssetDatabase.CreateAsset(p.Mesh[role],path);AssetDatabase.SaveAssetIfDirty(p.Mesh[role]);Digests(p.Mesh[role],row["replacement_unity_digests"]);var asset=Pin(p.Mesh[role]);var final=FinalLegacy(actual,row,asset);actual.Replace(final);Bind(Binding(role,true),p.Mesh[role]);pins.Add(new JObject{["name"]=role,["owner_path"]=Native,["predecessor_asset"]=Pin(p.Source[role]),["terminal_asset"]=asset,["previous_legacy_row"]=p.Prior[role].DeepClone(),["final_legacy_row"]=final.DeepClone(),["immutable_B24_installed_row"]=p.B24[role].DeepClone()});}
   var restored=(JObject)legacy.DeepClone();foreach(var pin in pins)restored["meshes"].Single(x=>(string)x["id"]==(string)pin["previous_legacy_row"]["id"]).Replace(pin["previous_legacy_row"].DeepClone());Need(JToken.DeepEquals(restored,before),"only native6 two rows changed; blue/native5 and all other rows retained");File.WriteAllText(Platform6BankB1Replay.Legacy,legacy.ToString()+"\n");var r=new JObject{["schema"]="chooguard.platform6-B4-installed-black-pane-source-aperture.v1",["recipe"]=Recipe,["recipe_sha256"]=RecipeSHA,["proof"]=Proof,["proof_sha256"]=ProofSHA,["physical_contract"]=Physical,["physical_contract_sha256"]=PhysicalSHA,["terminal_rows"]=pins,["native_fields_before"]=p.NativeFields.DeepClone(),["native_fields_after"]=Platform6BanksB24PlacementReplay.CarrierFields(Find(Native)),["actual_before_horizontal_controls"]=p.BeforeControls.DeepClone(),["only_attributed_source_faces_clipped"]=true,["all_outside_B4_and_columns_retained"]=true,["SceneSave_executed"]=false,["site99percent_verified"]=false};File.WriteAllText(Registry,r.ToString()+"\n");p.Applied=true;Need(Hash(ScenePath)==p.Epoch,"apply cannot save scene");return Verify();
  }
  public static JObject Verify()
  {
   var r=Read();Need(r!=null,"installed blackpane registry required");var j=Pins();var t=Find(Native);Need(t.gameObject.activeInHierarchy,"exact owner active");Platform6BankB1Replay.Frame(t,j["rows"][0]["world_matrix"]);ReviewedB24CarrierFields(Platform6BanksB24PlacementReplay.CarrierFields(t),Native);foreach(var row in j["rows"]){string role=(string)row["name"];Pair(r,role,out var previous,out var terminal,j);Need(Get(Binding(role))==ConcourseFidelitySuccessor.Resolve(terminal,role,Native),"exact composed live successor binding");}Need(t.GetComponent<MeshRenderer>().enabled&&t.GetComponent<MeshRenderer>().additionalVertexStreams==null&&t.GetComponent<MeshCollider>().enabled&&!t.GetComponent<MeshCollider>().isTrigger&&!t.GetComponent<MeshCollider>().convex&&(int)t.GetComponent<MeshCollider>().cookingOptions==30,"original active source render/collision policy");return new JObject{["status"]="PASS",["verification_kind"]="exact_B24_native6_then_attributed_B4_black_pane_source_aperture",["actual_PhysX_executed"]=false,["SceneSave_executed"]=false,["site99percent_verified"]=false};
  }
  static JObject Contact(RaycastHit h)=>new JObject{["owner"]=PathOf(h.collider.transform),["triangle"]=h.triangleIndex,["point"]=PV(h.point),["normal"]=PV(h.normal),["distance"]=h.distance};
  static RaycastHit[] Hits(JToken q)=>Physics.RaycastAll(V(q["world_origin"]),V(q["world_direction"]),(float)q["world_length"],~0,QueryTriggerInteraction.Ignore).OrderBy(x=>x.distance).ToArray();
  static JArray CaptureBefore()
  {
   var c=Json(Physical);bool old=Physics.queriesHitBackfaces;try{Physics.SyncTransforms();Physics.queriesHitBackfaces=false;var rows=new JArray();var owner=Find(Native).GetComponent<MeshCollider>();foreach(var sample in c["clear_headspace_samples"].Concat(c["retained_north_and_service_samples"])){var hits=Hits(sample);var expected=new HashSet<int>(sample["before_source_contacts"].Select(x=>(int)x["triangle"]));Need(hits.Any(h=>h.collider==owner&&expected.Contains(h.triangleIndex)),"actual before ray must positively hit its exact attributed source face");rows.Add(new JObject{["sample"]=sample.DeepClone(),["all_scene_contacts"]=new JArray(hits.Select(Contact))});}return rows;}finally{Physics.queriesHitBackfaces=old;}}
  public static JObject PhysicalVerify()
  {
   var exact=Verify();var c=Json(Physical);var s=SceneManager.GetActiveScene();string saved=Hash(ScenePath);int handle=s.handle;bool dirty=s.isDirty;bool old=Physics.queriesHitBackfaces;var clear=new JArray();var retained=new JArray();bool all=true;
   try{Physics.SyncTransforms();Physics.queriesHitBackfaces=false;var owner=Find(Native).GetComponent<MeshCollider>();foreach(var sample in c["clear_headspace_samples"]){var hits=Hits(sample);bool pass=!hits.Any(h=>h.collider==owner);all&=pass;clear.Add(new JObject{["sample"]=sample.DeepClone(),["passed"]=pass,["all_scene_contacts"]=new JArray(hits.Select(Contact))});}foreach(var sample in c["retained_north_and_service_samples"]){var hits=Hits(sample);var expected=new HashSet<int>(sample["expected_final_source_contacts"].Select(x=>(int)x["triangle"]));bool pass=hits.Any(h=>h.collider==owner&&expected.Contains(h.triangleIndex));all&=pass;retained.Add(new JObject{["sample"]=sample.DeepClone(),["passed"]=pass,["all_scene_contacts"]=new JArray(hits.Select(Contact))});}}finally{Physics.queriesHitBackfaces=old;Physics.SyncTransforms();}
   Need(SceneManager.GetActiveScene().handle==handle&&Hash(ScenePath)==saved&&s.isDirty==dirty,"actual QA preserved same saved scene epoch/handle/dirty");var result=new JObject{["status"]=all?"PASS":"FAIL",["actual_scene_Physics_RaycastAll"]=true,["Physics_queriesHitBackfaces"]=false,["clipped_B4_native6_headspace_controls"]=clear,["retained_north_and_service_source_controls"]=retained,["all_scene_collision_owners_logged"]=true,["bindings_only_verification"]=exact,["SceneSave_executed"]=false,["site99percent_verified"]=false};Need(all,"actual blackpane clearance/retention FAIL: "+result.ToString(Newtonsoft.Json.Formatting.None));return result;
  }
  public static void DisposePrepared(Prepared p){if(p==null)return;foreach(var m in p.Mesh.Values)if(m!=null&&!EditorUtility.IsPersistent(m))UnityEngine.Object.DestroyImmediate(m);p.Mesh.Clear();}
    sealed class NativeData
    {
        public Vector3[] vertices,normals;
        public Vector4[] tangents,colors;
        public readonly List<Vector4>[] uv=new List<Vector4>[8];
        public readonly int[] dim=new int[8];
        public MeshTopology[] topology;
        public int[][] indices;
    }

    static int Count(BinaryReader r,int max=5000000){int n=r.ReadInt32();Need(n>=0&&n<=max,"Invalid channel count");return n;}
    static float F(BinaryReader r){float f=r.ReadSingle();Need(float.IsFinite(f),"Nonfinite channel");return f;}
    static Vector3[] V3(BinaryReader r){int n=Count(r);var a=new Vector3[n];for(int i=0;i<n;i++)a[i]=new Vector3(F(r),F(r),F(r));return a;}
    static Vector4[] V4(BinaryReader r){int n=Count(r);var a=new Vector4[n];for(int i=0;i<n;i++)a[i]=new Vector4(F(r),F(r),F(r),F(r));return a;}
    static NativeData ReadNative(string path)
    {
        using(var stream=File.OpenRead(path))using(var r=new BinaryReader(stream))
        {
            Need(r.ReadInt32()==1,"Unsupported fullchannel binary");var d=new NativeData{vertices=V3(r),normals=V3(r),tangents=V4(r),colors=V4(r)};int n=d.vertices.Length;
            Need(n>0&&new[]{d.normals.Length,d.tangents.Length,d.colors.Length}.All(x=>x==0||x==n),"NativeData channel cardinality differs");
            for(int u=0;u<8;u++){int count=Count(r),dim=r.ReadInt32();Need(count==0?dim==0:count==n&&dim>=2&&dim<=4,"UV count/dimension differs");d.dim[u]=dim;d.uv[u]=new List<Vector4>(count);for(int i=0;i<count;i++)d.uv[u].Add(new Vector4(F(r),F(r),F(r),F(r)));}
            int sm=Count(r,1024);Need(sm>0&&sm<=1024,"NativeData material submesh count required");d.topology=new MeshTopology[sm];d.indices=new int[sm][];
            for(int s=0;s<sm;s++){d.topology[s]=(MeshTopology)r.ReadInt32();int count=Count(r,30000000);Need(d.topology[s]==MeshTopology.Triangles&&count%3==0,"Triangle topology required");d.indices[s]=new int[count];for(int i=0;i<count;i++){int index=r.ReadInt32();Need(index>=0&&index<n,"Index outside native vertex array");d.indices[s][i]=index;}}
            Need(stream.Position==stream.Length&&d.colors.Length==0,"Unexpected binary tail/color format");return d;
        }
    }
    static bool Bits(float a,float b)=>BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b);
    static bool Bits(Vector3 a,Vector3 b)=>Bits(a.x,b.x)&&Bits(a.y,b.y)&&Bits(a.z,b.z);
    static bool Bits(Vector4 a,Vector4 b)=>Bits(a.x,b.x)&&Bits(a.y,b.y)&&Bits(a.z,b.z)&&Bits(a.w,b.w);
    static void Prefix<T>(IList<T> source,IList<T> replacement,Func<T,T,bool> equal)
    {
        Need(source.Count==0?replacement.Count==0:replacement.Count>=source.Count,"Original channel count lost");for(int i=0;i<source.Count;i++)if(!equal(source[i],replacement[i]))Need(false,"Original native vertex channel prefix changed at "+i);
    }
    static void Prefix(NativeData old,NativeData fresh)
    {
        Prefix(old.vertices,fresh.vertices,(a,b)=>Bits(a,b));Prefix(old.normals,fresh.normals,(a,b)=>Bits(a,b));Prefix(old.tangents,fresh.tangents,(a,b)=>Bits(a,b));Prefix(old.colors,fresh.colors,(a,b)=>Bits(a,b));
        Need(old.topology.SequenceEqual(fresh.topology),"Original submesh topology changed");for(int u=0;u<8;u++){Need(old.dim[u]==fresh.dim[u],"UV dimension changed");Prefix(old.uv[u],fresh.uv[u],(a,b)=>Bits(a,b));}
    }
    static void ApplyNative(Mesh clone,NativeData d,Mesh baseline,NativeData old)
    {
        Prefix(old,d);clone.SetVertices(d.vertices);if(d.normals.Length>0)clone.SetNormals(d.normals);if(d.tangents.Length>0)clone.SetTangents(d.tangents);
        for(int u=0;u<8;u++)if(d.uv[u].Count>0){if(d.dim[u]==2)clone.SetUVs(u,d.uv[u].Select(v=>new Vector2(v.x,v.y)).ToList());else if(d.dim[u]==3)clone.SetUVs(u,d.uv[u].Select(v=>new Vector3(v.x,v.y,v.z)).ToList());else clone.SetUVs(u,d.uv[u]);}
        clone.subMeshCount=d.indices.Length;for(int z=0;z<d.indices.Length;z++)clone.SetIndices(d.indices[z],d.topology[z],z,false,0);clone.bounds=baseline.bounds;
        Need(clone.indexFormat==baseline.indexFormat&&clone.GetVertexAttributes().SequenceEqual(baseline.GetVertexAttributes())&&clone.blendShapeCount==0&&clone.bindposes.Length==0&&clone.boneWeights.Length==0,"original vertex/index format or skin state changed");
        var referenced=new HashSet<int>(old.indices.SelectMany(v=>v));var min=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);var max=new Vector3(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);foreach(int i in referenced){min=Vector3.Min(min,old.vertices[i]);max=Vector3.Max(max,old.vertices[i]);}
        // Original active Float32 vertices can be a few ULP beyond serialized bounds.
        // Retain bounds bitexact and forbid activating unused old prefix vertices.
        foreach(int i in d.indices.SelectMany(v=>v))Need(i>=old.vertices.Length||referenced.Contains(i),"unused old prefix vertex activated");
        for(int i=old.vertices.Length;i<d.vertices.Length;i++){var v=d.vertices[i];Need(v.x>=min.x&&v.x<=max.x&&v.y>=min.y&&v.y<=max.y&&v.z>=min.z&&v.z<=max.z,"new barycentric vertex escaped exact original referenced support");}
        Need(Bits(clone.bounds.center,baseline.bounds.center)&&Bits(clone.bounds.extents,baseline.bounds.extents),"serialized source bounds must remain bitexact");
    }

 }
}
#endif
