#if UNITY_EDITOR
// Exact versioned successor of the retained B1 complement. Root owns atomic rollback and the sole SceneSave.
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
 public static class Platform6B1ContactTerminal
 {
  public const string Registry="content/world/observed-interiors/platform6-B1-source-contact-terminal.json";
  const string Recipe="workers/twin/output/platform6-B1-floor-contact-revision-20261010/B1-contact-revision-recipe.json";
  const string RecipeSHA="594d5a0390ca7bc578046daa2267ebe39d23dc2b2e6253944dd6a8eb3c9acb18";
  const string Proof="workers/twin/output/platform6-B1-floor-contact-revision-20261010/independent-contact-terminal-proof.json";
  const string ProofSHA="642513bedcb91d43f1be7cd4493c6ab8733afe6beeee938b1d7a97b620e3dd48";
  const string ScenePath="Assets/ChooGuard/Scenes/FpsStation.unity";
  const string NativeOwner="FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_6";
  public sealed class Prepared
  {
   internal JObject recipe; internal string epoch; internal int handle; internal bool applied;
   internal JArray protectedState;
   internal readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
   public string[] CanonicalFiles=new[]{Registry,Platform6BankB1Replay.Legacy};
   public readonly List<string> CreatedAssets=new List<string>();
  }
  static void Need(bool b,string why)=>Platform6BankB1Replay.Need(b,"contact terminal: "+why);
  static string Hash(string p)=>Platform6BankB1Replay.Hash(p);
  static string Complete(Mesh m)=>Platform6BankB1Replay.Complete(m);
  static Transform Find(string p)=>Platform6BankB1Replay.Find(p);
  static string PathOf(Transform t)=>Platform6BankB1Replay.PathOf(t);
  static Mesh Get(Component c)=>c is MeshFilter f?f.sharedMesh:((MeshCollider)c).sharedMesh;
  static void Bind(Component c,Mesh mesh){if(c is MeshFilter f)f.sharedMesh=mesh;else((MeshCollider)c).sharedMesh=mesh;EditorUtility.SetDirty(c);}
  static JObject Pins()
  {
   Need(Hash(Recipe)==RecipeSHA&&Hash(Proof)==ProofSHA,"sealed recipe/proof changed");var j=JObject.Parse(File.ReadAllText(Recipe));var proof=JObject.Parse(File.ReadAllText(Proof));
   Need((string)proof["status"]=="PASS"&&(string)proof["recipe_sha256"]==RecipeSHA&&proof["checks"].All(c=>(bool)c["passed"]),"independent whole source/channel proof");
   Need(j["rows"].Count()==2&&j["rows"].All(r=>(string)r["owner_path"]==NativeOwner&&(int)r["changed_source_faces"]==57&&(int)r["replacement_vertex_count"]==(int)r["source_vertex_count"]+24),"exact two native6 scopes");
   foreach(string k in new[]{"source_plan","frame_path","source_wall_spec","source_observed_placement_controls","predecessor_bank_manifest","predecessor_floor_recipe","predecessor_floor_binary"})Need(Hash((string)j[k])==(string)j[k=="frame_path"?"frame_sha256":k+"_sha256"],"immutable source "+k);
   Need(Hash((string)j["archive_attribution"]["path"])==(string)j["archive_attribution"]["sha256"],"independent original g32/column lineage");
   foreach(var r in j["rows"])foreach(string k in new[]{"frozen_predecessor_binary","replacement_binary","lineage"})Need(Hash((string)r[k])==(string)r[k+"_sha256"],"fullchannel or face proof pin");return j;
  }
  static void Digests(Mesh mesh,JToken d)
  {
   Need(mesh!=null&&mesh.isReadable&&InteriorEnvelopeRepair.MeshDigest(mesh)==(string)d["mesh_sha256"]&&ArchitecturalCorrectionRegistry.ChannelDigest(mesh)==(string)d["channels_sha256"]&&Complete(mesh)==(string)d["complete_channels_sha256"],"exact native fullchannel digests");
  }
  static Component Binding(JToken r,bool exactId)
  {
   var t=Find((string)r["owner_path"]);Need(t.gameObject.activeInHierarchy,"native owner inactive");Platform6BankB1Replay.Frame(t,r["world_matrix"]);
   bool render=(string)r["name"]=="native6-render";Component c=render?(Component)t.GetComponent<MeshFilter>():t.GetComponent<MeshCollider>();Need(c!=null,"native binding missing");
   if(exactId)Need((long)GlobalObjectId.GetGlobalObjectIdSlow(c).targetObjectId==(long)r["bindings"].Single()["component_file_id"],"exact original native component ID");
   if(render){var mr=t.GetComponent<MeshRenderer>();Need(mr!=null&&mr.enabled&&mr.additionalVertexStreams==null,"native render policy");}
   else{var mc=(MeshCollider)c;Need(mc.enabled&&!mc.isTrigger&&!mc.convex&&(int)mc.cookingOptions==30,"native collision policy");}return c;
  }
  static void SourceFiles(JObject j)
  {
   // Call-local immutable file identity checks; no cache survives this validation.
   var hashes=new Dictionary<string,string>(StringComparer.Ordinal);
   Func<string,string> fileHash=p=>{if(!hashes.TryGetValue(p,out var value)){value=Hash(p);hashes.Add(p,value);}return value;};
   foreach(var r in j["rows"]){string a=(string)r["source_current_asset"];Need(fileHash(a)==(string)r["source_current_file_sha256"]&&fileHash(a+".meta")== (string)r["source_current_meta_sha256"]&&AssetDatabase.AssetPathToGUID(a)==(string)r["source_current_guid"],"immutable pre-B1 source identity");var m=AssetDatabase.LoadAssetAtPath<Mesh>(a);Need(m!=null&&!EditorUtility.IsDirty(m)&&Complete(m)==(string)r["source_current_complete_digest"],"immutable pre-B1 source fullchannels");}
   foreach(var p in j["protected_column_owner_assets"]){string a=(string)p["asset"];Need(fileHash(a)==(string)p["file_sha256"]&&fileHash(a+".meta")== (string)p["meta_sha256"]&&AssetDatabase.AssetPathToGUID(a)==(string)p["guid"],"protected native7/53 column source changed");Need(!EditorUtility.IsDirty(AssetDatabase.LoadAssetAtPath<Mesh>(a)),"protected column loaded asset dirty");}
  }
  static JArray ProtectedSnapshot(JObject j)
  {
   var owners=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToLookup(Platform6BankB1Replay.PathOf,StringComparer.Ordinal);var result=new JArray();
   foreach(var p in j["protected_column_owner_assets"]){var matches=owners[(string)p["owner_path"]].ToArray();Need(matches.Length==1,"missing/ambiguous protected column path");result.Add(new JObject{["owner_path"]=p["owner_path"].DeepClone(),["state"]=Platform6BankB1Replay.Snapshot(matches[0],false)});}return result;
  }
  static void ProtectedMatches(JObject j,JToken state)=>Need(JToken.DeepEquals(ProtectedSnapshot(j),state),"protected native7/53 source column fields, meshes or poses changed");
  static JObject Read()
  {
   if(!File.Exists(Registry))
   {
    foreach(string role in new[]{"render","collider"})Need(!File.Exists("Assets/ChooGuard/Art/StationInterior/OfficialFloorplan/Platform6B1_native6-"+role+"_ContactTerminal.asset"),"installed terminal asset lost exact registry");
    if(File.Exists(Platform6BankB1Replay.Legacy))Need(!JObject.Parse(File.ReadAllText(Platform6BankB1Replay.Legacy))["meshes"].Any(r=>r["platform6_B1_contact_terminal_composition"]!=null),"installed terminal Legacy row lost exact registry");return null;
   }
   var j=JObject.Parse(File.ReadAllText(Registry));Need((string)j["schema"]=="chooguard.platform6-B1-installed-contact-terminal.v1"&&(string)j["recipe"]==Recipe&&(string)j["recipe_sha256"]==RecipeSHA&&(string)j["proof"]==Proof&&(string)j["proof_sha256"]==ProofSHA&&j["rows"].Count()==2,"exact terminal registration provenance");return j;
  }
  static Mesh Frozen(JToken bankRow,JToken recipeRow)
  {
   string asset=(string)bankRow["complement_asset"];Need(asset==(string)recipeRow["frozen_predecessor_asset"]&&Hash(asset)==(string)bankRow["complement_file_sha256"]&&Hash(asset+".meta")== (string)bankRow["complement_meta_sha256"]&&AssetDatabase.AssetPathToGUID(asset)==(string)bankRow["complement_guid"],"frozen original B1 complement asset");var m=AssetDatabase.LoadAssetAtPath<Mesh>(asset);Need(m!=null&&!EditorUtility.IsDirty(m),"clean retained original B1 complement");Digests(m,recipeRow["frozen_predecessor_unity_digests"]);Need(Complete(m)==(string)bankRow["complement_complete_digest"],"frozen B1 fullchannel registration");return m;
  }
  static Mesh Terminal(JToken pin,JToken row)
  {
   string a=(string)pin["asset"];Need(a==(string)row["replacement_asset"]&&Hash(a)==(string)pin["file_sha256"]&&Hash(a+".meta")== (string)pin["meta_sha256"]&&AssetDatabase.AssetPathToGUID(a)==(string)pin["guid"],"exact successor asset");var m=AssetDatabase.LoadAssetAtPath<Mesh>(a);Need(m!=null&&!EditorUtility.IsDirty(m),"clean saved contact successor");Digests(m,row["replacement_unity_digests"]);return m;
  }
  static JToken ValidatePair(JObject j,JObject r,string role,out Mesh baseline,out Mesh terminal)
  {
   Need(role=="native6-render"||role=="native6-collider","exact role only");var row=j["rows"].Single(q=>(string)q["name"]==role);var pin=r["rows"].Single(q=>(string)q["name"]==role);var bank=Platform6BankB1Replay.Read();Need(bank!=null,"source bank registration required");var br=bank["assets"].Single(q=>(string)q["name"]==role);
   Need(JToken.DeepEquals(pin["frozen_B1_asset_row"],br)&&JToken.DeepEquals(pin["previous_legacy_row"],br["legacy_final_row"]),"retained B1 source/Legacy provenance changed");baseline=Frozen(br,row);terminal=Terminal(pin,row);var actual=JObject.Parse(File.ReadAllText(Platform6BankB1Replay.Legacy))["meshes"].Single(q=>(string)q["id"]=="platform6-"+role);Need(JToken.DeepEquals(actual,Platform6BanksB24Terminal.ResolveContactLegacy(pin["final_legacy_row"],role)),"exact current terminal Legacy successor chain changed");return pin;
  }
  // These adapters accept an exact reviewed predecessor only as an input; after registration the Scene must use the exact successor.
  public static Mesh ResolveB1NativeCarrier(Mesh reviewedBase,string role,string ownerPath)
  {
   Need(ownerPath==NativeOwner,"exact source owner path");var r=Read();if(r==null)return reviewedBase;var j=Pins();ValidatePair(j,r,role,out var baseline,out var terminal);Need(reviewedBase==baseline,"reviewed frozen B1 complement required");return Platform6BanksB24Terminal.ResolveContactCarrier(terminal,role,ownerPath);
  }
  public static JToken ResolveB1LegacyRow(JToken reviewedB1FinalRow,string role)
  {
   var r=Read();if(r==null)return reviewedB1FinalRow;var j=Pins();var pin=ValidatePair(j,r,role,out var baseline,out var terminal);Need(JToken.DeepEquals(reviewedB1FinalRow,pin["previous_legacy_row"]),"reviewed frozen Legacy predecessor required");return Platform6BanksB24Terminal.ResolveContactLegacy(pin["final_legacy_row"],role);
  }
  public static void RequireFloorCarrier(Mesh actual,string role,string reviewedBaseDigest,string ownerPath)
  {
   Need(ownerPath==NativeOwner,"exact floor coupling owner");var r=Read();if(r==null){Need(actual!=null&&!EditorUtility.IsDirty(actual)&&Complete(actual)==reviewedBaseDigest,"unregistered original B1 carrier required");return;}
   var j=Pins();ValidatePair(j,r,role,out var baseline,out var terminal);Need(Complete(baseline)==reviewedBaseDigest&&actual==Platform6BanksB24Terminal.ResolveContactCarrier(terminal,role,ownerPath),"registered exact contact then B2/B4 successor mandatory for floor coupling");
  }
  public static Prepared Prepare()
  {
   var scene=SceneManager.GetActiveScene();Need(scene.path==ScenePath&&!scene.isDirty&&!EditorApplication.isCompiling&&!EditorApplication.isPlayingOrWillChangePlaymode,"clean saved source Scene required at preparation");Need(Read()==null&&Platform6BankB1Replay.Read()==null,"fresh combined B1/contact installation required");var j=Pins();SourceFiles(j);var p=new Prepared{recipe=j,epoch=Hash(ScenePath),handle=scene.handle,protectedState=ProtectedSnapshot(j)};
   try
   {
    foreach(var row in j["rows"])
    {
     var c=Binding(row,true);var source=Get(c);Need(AssetDatabase.GetAssetPath(source)==(string)row["source_current_asset"]&&Complete(source)==(string)row["source_current_complete_digest"],"exact original source binding at prepare");string target=(string)row["replacement_asset"];Need(!File.Exists(target)&&AssetDatabase.LoadAssetAtPath<Mesh>(target)==null,"fresh terminal path");var old=ReadNative((string)row["frozen_predecessor_binary"]);var fresh=ReadNative((string)row["replacement_binary"]);Prefix(old,fresh);var clone=UnityEngine.Object.Instantiate(source);clone.name="Platform6B1_"+(string)row["name"]+"_ContactTerminal";p.meshes.Add((string)row["name"],clone);ApplyNative(clone,fresh,source);Digests(clone,row["replacement_unity_digests"]);
    }
    Need(Hash(ScenePath)==p.epoch&&!scene.isDirty,"prepare changed saved Scene");return p;
   }catch{DisposePrepared(p);throw;}
  }
  static JObject FinalLegacy(JToken old,JToken row,JObject pin)
  {
   var f=(JObject)old.DeepClone();f["replacement_asset"]=pin["asset"].DeepClone();f["replacement_guid"]=pin["guid"].DeepClone();f["replacement_file_sha256"]=pin["file_sha256"].DeepClone();f["replacement_meta_sha256"]=pin["meta_sha256"].DeepClone();f["replacement_complete_channel_digest"]=row["replacement_unity_digests"]["complete_channels_sha256"].DeepClone();f["replacement_channel_digest"]=row["replacement_unity_digests"]["channels_sha256"].DeepClone();f["replacement_mesh_digest"]=row["replacement_unity_digests"]["mesh_sha256"].DeepClone();f["replacement_binary"]=new JObject{["path"]=row["replacement_binary"].DeepClone(),["sha256"]=row["replacement_binary_sha256"].DeepClone()};f["replacement_vertex_count"]=row["replacement_vertex_count"].DeepClone();f["replacement_triangle_count"]=row["replacement_triangles"].DeepClone();
   f["platform6_B1_contact_terminal_composition"]=new JObject{["recipe"]=Recipe,["recipe_sha256"]=RecipeSHA,["proof"]=Proof,["proof_sha256"]=ProofSHA,["previous_terminal"]=old.DeepClone(),["only_source_proven_shared_g32_plinth_rail_and_matching_trim"]=true,["all_Y7_floor_faces_and_below_floor_geometry_preserved"]=true};return f;
  }
  public static JObject ApplyPreparedWithoutSave(Prepared p)
  {
   Need(p!=null&&!p.applied&&p.meshes.Count==2,"fresh prepared contact successor");var scene=SceneManager.GetActiveScene();Need(scene.path==ScenePath&&scene.handle==p.handle&&Hash(ScenePath)==p.epoch&&!EditorApplication.isCompiling&&!EditorApplication.isPlayingOrWillChangePlaymode,"prepared saved epoch required; dirty Scene from outer bank/floor transaction allowed");Need(Read()==null,"contact terminal already installed");var j=Pins();SourceFiles(j);ProtectedMatches(j,p.protectedState);Platform6BankB1Replay.Verify();var bank=Platform6BankB1Replay.Read();Need(bank!=null,"complete bank and original floor must already be applied");var legacy=JObject.Parse(File.ReadAllText(Platform6BankB1Replay.Legacy));var before=(JObject)legacy.DeepClone();var registry=new JObject{["schema"]="chooguard.platform6-B1-installed-contact-terminal.v1",["recipe"]=Recipe,["recipe_sha256"]=RecipeSHA,["proof"]=Proof,["proof_sha256"]=ProofSHA,["protected_column_state"]=p.protectedState.DeepClone(),["rows"]=new JArray(),["site99percent_verified"]=false};
   foreach(var row in j["rows"])
   {
    string role=(string)row["name"];var br=bank["assets"].Single(q=>(string)q["name"]==role);var frozen=Frozen(br,row);Need(Get(Binding(row,true))==frozen,"exact retained predecessor binding immediately before mutation");Need(JToken.DeepEquals(legacy["meshes"].Single(q=>(string)q["id"]=="platform6-"+role),br["legacy_final_row"]),"exact retained predecessor row");Digests(p.meshes[role],row["replacement_unity_digests"]);Need(!File.Exists((string)row["replacement_asset"]),"fresh terminal asset at mutation");
   }
   foreach(var row in j["rows"])
   {
    string role=(string)row["name"],a=(string)row["replacement_asset"];var br=bank["assets"].Single(q=>(string)q["name"]==role);var mesh=p.meshes[role];p.CreatedAssets.Add(a);AssetDatabase.CreateAsset(mesh,a);AssetDatabase.SaveAssetIfDirty(mesh);Digests(mesh,row["replacement_unity_digests"]);Bind(Binding(row,true),mesh);
    var pin=new JObject{["name"]=role,["asset"]=a,["guid"]=AssetDatabase.AssetPathToGUID(a),["file_sha256"]=Hash(a),["meta_sha256"]=Hash(a+".meta"),["frozen_B1_asset_row"]=br.DeepClone(),["previous_legacy_row"]=br["legacy_final_row"].DeepClone()};var final=FinalLegacy(br["legacy_final_row"],row,pin);pin["final_legacy_row"]=final.DeepClone();legacy["meshes"].Single(q=>(string)q["id"]=="platform6-"+role).Replace(final);((JArray)registry["rows"]).Add(pin);
   }
   var untouched=(JObject)legacy.DeepClone();foreach(var pin in registry["rows"])untouched["meshes"].Single(q=>(string)q["id"]=="platform6-"+(string)pin["name"]).Replace(pin["previous_legacy_row"].DeepClone());Need(JToken.DeepEquals(untouched,before),"unrelated Legacy row or policy changed");File.WriteAllText(Platform6BankB1Replay.Legacy,legacy.ToString()+"\n");File.WriteAllText(Registry,registry.ToString()+"\n");p.applied=true;Physics.SyncTransforms();return Verify();
  }
  public static JObject Verify()
  {
   var r=Read();if(r==null)return new JObject{["status"]="NOT_INSTALLED",["SceneSave_executed"]=false};var j=Pins();SourceFiles(j);ProtectedMatches(j,r["protected_column_state"]);
   foreach(var row in j["rows"]){ValidatePair(j,r,(string)row["name"],out var baseline,out var terminal);Need(Get(Binding(row,false))==Platform6BanksB24Terminal.ResolveContactCarrier(terminal,(string)row["name"],NativeOwner),"registered exact successor binding mandatory");}
   return new JObject{["status"]="PASS",["verification_kind"]="exact_retained_predecessor_and_terminal_bindings_provenance_channels_columns",["recipe_sha256"]=RecipeSHA,["proof_sha256"]=ProofSHA,["unmodified_predecessor_PhysicalVerify_required"]=true,["actual_PhysX_executed"]=false,["SceneSave_executed"]=false,["site99percent_verified"]=false};
  }
  public static void DisposePrepared(Prepared p){if(p==null)return;foreach(var m in p.meshes.Values)if(m!=null&&!EditorUtility.IsPersistent(m))UnityEngine.Object.DestroyImmediate(m);p.meshes.Clear();}
    sealed class Native
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
    static Native ReadNative(string path)
    {
        using(var stream=File.OpenRead(path))using(var r=new BinaryReader(stream))
        {
            Need(r.ReadInt32()==1,"Unsupported fullchannel binary");var d=new Native{vertices=V3(r),normals=V3(r),tangents=V4(r),colors=V4(r)};int n=d.vertices.Length;
            Need(n>0&&new[]{d.normals.Length,d.tangents.Length,d.colors.Length}.All(x=>x==0||x==n),"Native channel cardinality differs");
            for(int u=0;u<8;u++){int count=Count(r),dim=r.ReadInt32();Need(count==0?dim==0:count==n&&dim>=2&&dim<=4,"UV count/dimension differs");d.dim[u]=dim;d.uv[u]=new List<Vector4>(count);for(int i=0;i<count;i++)d.uv[u].Add(new Vector4(F(r),F(r),F(r),F(r)));}
            int sm=Count(r,1024);Need(sm>0&&sm<=1024,"Native material submesh count required");d.topology=new MeshTopology[sm];d.indices=new int[sm][];
            for(int s=0;s<sm;s++){d.topology[s]=(MeshTopology)r.ReadInt32();int count=Count(r,30000000);Need(d.topology[s]==MeshTopology.Triangles&&count%3==0,"Triangle topology required");d.indices[s]=new int[count];for(int i=0;i<count;i++){int index=r.ReadInt32();Need(index>=0&&index<n,"Index outside native vertex array");d.indices[s][i]=index;}}
            Need(stream.Position==stream.Length&&d.colors.Length==0,"Unexpected binary tail/color format");return d;
        }
    }
    static bool Bits(float a,float b)=>BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b);
    static bool Bits(Vector3 a,Vector3 b)=>Bits(a.x,b.x)&&Bits(a.y,b.y)&&Bits(a.z,b.z);
    static bool Bits(Vector4 a,Vector4 b)=>Bits(a.x,b.x)&&Bits(a.y,b.y)&&Bits(a.z,b.z)&&Bits(a.w,b.w);
    static void Prefix<T>(IList<T> source,IList<T> replacement,Func<T,T,bool> equal)
    {
        Need(source.Count==0?replacement.Count==0:replacement.Count>=source.Count,"Original channel count lost");for(int i=0;i<source.Count;i++)Need(equal(source[i],replacement[i]),"Original native vertex channel prefix changed");
    }
    static void Prefix(Native old,Native fresh)
    {
        Prefix(old.vertices,fresh.vertices,(a,b)=>Bits(a,b));Prefix(old.normals,fresh.normals,(a,b)=>Bits(a,b));Prefix(old.tangents,fresh.tangents,(a,b)=>Bits(a,b));Prefix(old.colors,fresh.colors,(a,b)=>Bits(a,b));
        Need(old.topology.SequenceEqual(fresh.topology),"Original submesh topology changed");for(int u=0;u<8;u++){Need(old.dim[u]==fresh.dim[u],"UV dimension changed");Prefix(old.uv[u],fresh.uv[u],(a,b)=>Bits(a,b));}
    }
    static void ApplyNative(Mesh clone,Native d,Mesh baseline)
    {
        clone.SetVertices(d.vertices);if(d.normals.Length>0)clone.SetNormals(d.normals);if(d.tangents.Length>0)clone.SetTangents(d.tangents);
        for(int u=0;u<8;u++)if(d.uv[u].Count>0){if(d.dim[u]==2)clone.SetUVs(u,d.uv[u].Select(v=>new Vector2(v.x,v.y)).ToList());else if(d.dim[u]==3)clone.SetUVs(u,d.uv[u].Select(v=>new Vector3(v.x,v.y,v.z)).ToList());else clone.SetUVs(u,d.uv[u]);}
        clone.subMeshCount=d.indices.Length;for(int s=0;s<d.indices.Length;s++)clone.SetIndices(d.indices[s],d.topology[s],s,false,0);clone.bounds=baseline.bounds;
        Need(clone.indexFormat==baseline.indexFormat&&clone.GetVertexAttributes().SequenceEqual(baseline.GetVertexAttributes())&&clone.blendShapeCount==0&&clone.bindposes.Length==0&&clone.boneWeights.Length==0,"Original vertex/index format or skin state changed");
        var active=d.indices.SelectMany(v=>v).Distinct();foreach(int i in active)Need(baseline.bounds.Contains(d.vertices[i]),"Referenced successor vertex escaped exact original bounds");for(int i=baseline.vertexCount;i<d.vertices.Length;i++)Need(baseline.bounds.Contains(d.vertices[i]),"Appended successor vertex escaped exact original bounds");
    }

 }
}
#endif
