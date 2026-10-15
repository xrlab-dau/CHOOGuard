#if UNITY_EDITOR
// REVIEW DRAFT: one minimal exact old-hole source-floor owner. Complete B2/B4 banks+floor compose before a single root-owned SceneSave.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
namespace ChooGuard.Editor.Twin
{
 public static class Platform6B24OldHoleFloorSupport
 {
  public const string Registry="content/world/observed-interiors/platform6-B2-B4-old-hole-floor.json";
  const string Recipe="workers/twin/output/platform6-B2B4-old-hole-floor-20261010/B2B4-old-hole-floor-recipe.json";
  const string RecipeSHA="b204972b4b814464b327261264563e32c9d61242aa0d7e66a6427ad25cd49b02";
  const string Proof="workers/twin/output/platform6-B2B4-old-hole-floor-20261010/independent-B2B4-old-hole-floor-proof.json";
  const string ProofSHA="4ac39d44bda3a27b22ce55d79a31dc666ddb4c29af92c68772dae867faa5147e";
  const string ScenePath="Assets/ChooGuard/Scenes/FpsStation.unity";
  const string Owner="부산역 탑승구 도면 이전 계단 바닥 복원_P6B24";
  public sealed class Prepared
  {
   internal JObject Recipe;
   internal Mesh Mesh;
   internal string Epoch;
   internal int SceneHandle;
   internal bool Applied;
   internal JArray RetainedBaseline;
   public string[] CanonicalFiles=new[]{Registry};
   public readonly List<string> CreatedAssets=new List<string>();
   public GameObject CreatedOwner;
  }
  static void Need(bool b,string message){if(!b)throw new InvalidDataException("B2/B4 exact old-hole floor: "+message);}
  static string Hash(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
  static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
  static string Complete(Mesh m)=>ArchitecturalCorrectionRegistry.CompleteChannelDigest(m);
  static Transform[] Named(string path)=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>PathOf(t)==path).ToArray();
  static Transform Find(string path){var rows=Named(path);Need(rows.Length==1,"missing/ambiguous source owner "+path);return rows[0];}
  static void Frame(Transform t,JToken a){var m=t.localToWorldMatrix;for(int r=0;r<4;r++)for(int c=0;c<4;c++)Need(float.IsFinite(m[r,c])&&Mathf.Abs(m[r,c]-(float)a[r][c])<.00002f,"source frame changed "+PathOf(t));}
  static void Identity(Transform t){Need(t.parent==null,"floor must be one independent root");var m=t.localToWorldMatrix;for(int r=0;r<4;r++)for(int c=0;c<4;c++)Need(BitConverter.SingleToInt32Bits(m[r,c])==BitConverter.SingleToInt32Bits(r==c?1f:0f),"exact identity root frame");}
  static Mesh MeshOf(Component c)=>c is MeshFilter f?f.sharedMesh:c is MeshCollider mc?mc.sharedMesh:null;
  static void CheckDigests(Mesh mesh,JToken d){Need(mesh!=null&&mesh.isReadable&&InteriorEnvelopeRepair.MeshDigest(mesh)==(string)d["mesh_sha256"]&&ArchitecturalCorrectionRegistry.ChannelDigest(mesh)==(string)d["channels_sha256"]&&Complete(mesh)==(string)d["complete_channels_sha256"],"exact new fullchannels changed");}
  static JObject Pins()
  {
   Need(Hash(Recipe)==RecipeSHA&&Hash(Proof)==ProofSHA,"sealed old-hole recipe/proof changed");var j=JObject.Parse(File.ReadAllText(Recipe));var p=JObject.Parse(File.ReadAllText(Proof));Need((string)p["status"]=="PASS"&&(string)p["recipe_sha256"]==RecipeSHA&&p["checks"].All(q=>(bool)q["passed"]),"independent wholepolygon/fullchannel proof required");
   Need((string)j["new_owner"]==Owner&&(int)j["vertex_count"]==4044&&(int)j["triangle_count"]==1348&&(double)j["floor_top_y"]==7&&(double)j["floor_bottom_y"]==6.7&&(bool)j["identity_root"]&&!(bool)j["existing_owners_or_meshes_modified"],"exact minimal source-floor scope");
   foreach(var pin in j["source_inputs"])Need(Hash((string)pin["path"])==(string)pin["sha256"],"immutable source input changed "+(string)pin["path"]);
   Need(Hash((string)j["footprints"])==(string)j["footprints_sha256"]&&Hash((string)j["binary"])==(string)j["binary_sha256"]&&Hash((string)j["material"])==(string)j["material_sha256"],"floor binary/footprints/existing material changed");
   Need(Hash((string)j["physical_baseline"]["path"])==(string)j["physical_baseline"]["sha256"]&&Hash((string)j["physical_baseline"]["attribution"])==(string)j["physical_baseline"]["attribution_sha256"]&&Hash((string)j["physical_contract"]["path"])==(string)j["physical_contract"]["sha256"],"exact allScene baseline or physical contract changed");
   var contract=JObject.Parse(File.ReadAllText((string)j["physical_contract"]["path"]));Need(contract["positive_floor_samples"].Count()==545&&contract["bank_expected_empty_flat_Y7_outside_original_moved_profiles"].Count()==253&&contract["retained_source_contact_controls"].Count()==389,"unchanged545/253/six+383physical source contract");return j;
  }
  static void SourceFiles(JObject j)
  {
   var material=AssetDatabase.LoadAssetAtPath<Material>((string)j["material"]);Need(material!=null&&!EditorUtility.IsDirty(material),"clean immutable existing floor material");
   var protectedPin=JObject.Parse(File.ReadAllText("workers/twin/output/platform6-B2B4-old-hole-floor-20261010/protected-native7-actual-contact-footprint.json"));string asset=(string)protectedPin["native7_export"]["asset"];Need(Hash(asset)==(string)protectedPin["unchanged_source_asset_sha256"]&&AssetDatabase.AssetPathToGUID(asset)==(string)protectedPin["native7_export"]["guid"],"unchanged protected native7 cap source asset");var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(asset);Need(mesh!=null&&!EditorUtility.IsDirty(mesh)&&Complete(mesh)==(string)protectedPin["native7_export"]["complete_channel_digest"],"protected native7 fullchannels");var t=Find((string)protectedPin["native7_owner"]["path"]);Frame(t,protectedPin["native7_owner"]["world_matrix"]);Need(t.gameObject.activeInHierarchy&&t.GetComponent<MeshFilter>()?.sharedMesh==mesh&&t.GetComponent<MeshCollider>()?.sharedMesh==mesh,"protected native7 exact original bindings/frame");
  }
  static void SourceBindings(JObject j,bool final,bool exactIDs=false)
  {
   var source=JObject.Parse(File.ReadAllText((string)j["source_candidate"]));foreach(var row in source["rows"])
   {
    string path=row["owner"].Type==JTokenType.String?(string)row["owner"]:(string)row["owner"]["path"];var t=Find(path);Frame(t,row["world_matrix"]);Need(t.gameObject.activeInHierarchy,"source floor carrier inactive");
    foreach(var b in row["bindings"])
    {
     string type=(string)(b["type"]??b["component_type"]);long id=(long)(b["id"]??b["component_file_id"]);Component c=type=="MeshFilter"?(Component)t.GetComponent<MeshFilter>():t.GetComponent<MeshCollider>();Need(c!=null,"exact carrier component type missing");if(exactIDs)Need(GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()=="GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-"+id+"-0","prepared original exact carrier component ID");
     if(c is MeshFilter f)Need(f.GetComponent<MeshRenderer>()!=null&&f.GetComponent<MeshRenderer>().enabled&&f.GetComponent<MeshRenderer>().additionalVertexStreams==null,"active source render policy");else{var mc=(MeshCollider)c;Need(mc.enabled&&!mc.isTrigger&&!mc.convex&&(int)mc.cookingOptions==30,"active source collision policy");}
     var mesh=MeshOf(c);if(final&&((string)row["name"]=="native5-render"||(string)row["name"]=="native5-collider"))Platform6BluePartitionTerminal.RequireB24Carrier(mesh,row,path);else if(final&&((string)row["name"]).StartsWith("native6-",StringComparison.Ordinal)){var reviewed=AssetDatabase.LoadAssetAtPath<Mesh>((string)row["replacement_asset"]);Need(reviewed!=null&&!EditorUtility.IsDirty(reviewed)&&Complete(reviewed)==(string)row["replacement_unity_digests"]["complete_channels_sha256"],"exact retained B24 native6 source carrier");Need(mesh==Platform6B4BlackPaneCorrection.ResolveB24NativeCarrier(reviewed,(string)row["name"],path),"exact B24 then attributed blackpane native6 successor");}else Need(mesh!=null&&!EditorUtility.IsDirty(mesh)&&Complete(mesh)==(string)row[final?"replacement_unity_digests":"source_unity_digests"]["complete_channels_sha256"]&&AssetDatabase.GetAssetPath(mesh)==(string)row[final?"replacement_asset":"source_asset"],"exact before/after native5/native6/Main carrier "+(string)row["name"]);
    }
   }
  }
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
    static Native Read(string path)
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
  static Mesh NewMesh(JObject j)
  {
   var d=Read((string)j["binary"]);Need(d.vertices.Length==(int)j["vertex_count"]&&d.indices.Length==1&&d.indices[0].Length/3==(int)j["triangle_count"]&&d.colors.Length==0,"newmesh exact scope");
   var m=new Mesh{name=(string)j["mesh_name"]??"PLAN2F-B24-OLD-HOLE-FLOOR",indexFormat=UnityEngine.Rendering.IndexFormat.UInt16};try
   {
    m.SetVertices(d.vertices);m.SetNormals(d.normals);m.SetTangents(d.tangents);for(int u=0;u<8;u++)if(d.uv[u].Count>0){Need(d.dim[u]==2&&u==0,"only exact source UV0 channel");m.SetUVs(u,d.uv[u].Select(v=>new Vector2(v.x,v.y)).ToList());}m.subMeshCount=1;m.SetIndices(d.indices[0],MeshTopology.Triangles,0,false,0);m.RecalculateBounds();CheckDigests(m,j["unity_digests"]);Need(m.GetVertexAttributes().Length==4&&m.blendShapeCount==0&&m.bindposes.Length==0&&m.boneWeights.Length==0,"unexpected new channels/skin");return m;
   }catch{UnityEngine.Object.DestroyImmediate(m);throw;}
  }
  public static Prepared Prepare()
  {
   var scene=SceneManager.GetActiveScene();Need(scene.path==ScenePath&&!scene.isDirty&&!EditorApplication.isCompiling&&!EditorApplication.isPlayingOrWillChangePlaymode,"prepare needs clean stable saved FpsStation");var j=Pins();SourceFiles(j);Need(!File.Exists(Platform6BanksB24Terminal.Registry),"B2/B4 successors must be absent at joint prepare");Platform6B1ContactTerminal.Verify();SourceBindings(j,false,true);
   Need(!File.Exists(Registry)&&Named(Owner).Length==0&&!File.Exists((string)j["new_asset"])&&AssetDatabase.LoadAssetAtPath<Mesh>((string)j["new_asset"])==null,"exact new registry/owner/asset must be absent");return new Prepared{Recipe=j,Mesh=NewMesh(j),Epoch=Hash(ScenePath),SceneHandle=scene.handle,RetainedBaseline=CaptureRetainedBaseline(j)};
  }
  static GameObject MakeOwner(JObject j,Mesh mesh)
  {
   Need(Named(Owner).Length==0,"new floor owner already exists");var go=new GameObject(Owner);go.isStatic=true;go.layer=0;go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);go.transform.localScale=Vector3.one;var f=go.AddComponent<MeshFilter>();var r=go.AddComponent<MeshRenderer>();var mc=go.AddComponent<MeshCollider>();f.sharedMesh=mesh;r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>((string)j["material"]);r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;r.receiveShadows=true;mc.sharedMesh=mesh;mc.convex=false;mc.isTrigger=false;mc.cookingOptions=(MeshColliderCookingOptions)30;Identity(go.transform);return go;
  }
  public static JObject ApplyPreparedWithoutSave(Prepared p)
  {
   Need(p!=null&&!p.Applied&&p.Mesh!=null,"fresh prepared floor required");var scene=SceneManager.GetActiveScene();Need(scene.path==ScenePath&&scene.handle==p.SceneHandle&&Hash(ScenePath)==p.Epoch&&!EditorApplication.isCompiling&&!EditorApplication.isPlayingOrWillChangePlaymode,"same prepared Scene/file epoch required; dirty Scene from atomic bank move is allowed");var j=Pins();SourceFiles(j);Platform6BanksB24Terminal.Verify();SourceBindings(j,true,true);Need(!File.Exists(Registry)&&Named(Owner).Length==0&&!File.Exists((string)j["new_asset"]),"new floor state must still be absent");CheckDigests(p.Mesh,j["unity_digests"]);
   string path=(string)j["new_asset"];p.CreatedAssets.Add(path);AssetDatabase.CreateAsset(p.Mesh,path);AssetDatabase.SaveAssetIfDirty(p.Mesh);CheckDigests(p.Mesh,j["unity_digests"]);p.CreatedOwner=MakeOwner(j,p.Mesh);
   var record=new JObject{["schema"]="chooguard.platform6-B2B4-installed-exact-old-hole-floor.v1",["recipe"]=Recipe,["recipe_sha256"]=RecipeSHA,["proof"]=Proof,["proof_sha256"]=ProofSHA,["source_authority"]="official_2F_exact_old_missing_profiles_outside_all_source_banks_X_walls_posts_and_moved_MC_profiles",["owner_path"]=Owner,["world_matrix"]=new JArray(new JArray(1,0,0,0),new JArray(0,1,0,0),new JArray(0,0,1,0),new JArray(0,0,0,1)),["asset"]=path,["guid"]=AssetDatabase.AssetPathToGUID(path),["file_sha256"]=Hash(path),["meta_sha256"]=Hash(path+".meta"),["complete_channels_sha256"]=j["unity_digests"]["complete_channels_sha256"].DeepClone(),["material_asset"]=j["material"].DeepClone(),["material_sha256"]=j["material_sha256"].DeepClone(),["all_existing_source_owners_and_fullchannels_unchanged"]=true,["root_atomic_assembly_coupling_required"]=true,["retained_source_contact_baseline"]=p.RetainedBaseline.DeepClone(),["site99percent_verified"]=false};File.WriteAllText(Registry,record.ToString());p.Applied=true;return Verify();
  }
  static JObject Registered()
  {
   Need(File.Exists(Registry),"installed exact floor registry missing");var r=JObject.Parse(File.ReadAllText(Registry));Need((string)r["schema"]=="chooguard.platform6-B2B4-installed-exact-old-hole-floor.v1"&&(string)r["recipe"]==Recipe&&(string)r["recipe_sha256"]==RecipeSHA&&(string)r["proof"]==Proof&&(string)r["proof_sha256"]==ProofSHA&&(string)r["owner_path"]==Owner,"exact registered source floor provenance");return r;
  }
  static Mesh RegisteredMesh(JObject j,JObject r)
  {
   string path=(string)r["asset"];Need(path==(string)j["new_asset"]&&Hash(path)==(string)r["file_sha256"]&&Hash(path+".meta")== (string)r["meta_sha256"]&&AssetDatabase.AssetPathToGUID(path)==(string)r["guid"],"new floor saved asset identity");var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);Need(mesh!=null&&!EditorUtility.IsDirty(mesh),"new floor loaded asset state");CheckDigests(mesh,j["unity_digests"]);Need((string)r["complete_channels_sha256"]==Complete(mesh),"registered complete digest");return mesh;
  }
  public static JObject Verify()
  {
   var j=Pins();var r=Registered();SourceFiles(j);Platform6BanksB24Terminal.Verify();SourceBindings(j,true);var mesh=RegisteredMesh(j,r);var t=Find(Owner);Identity(t);var go=t.gameObject;Need(go.activeSelf&&go.activeInHierarchy&&go.isStatic&&go.layer==0&&go.GetComponents<Component>().Length==4&&t.childCount==0,"one active static root with exact Transform/MF/MR/MC only");var f=go.GetComponent<MeshFilter>();var mr=go.GetComponent<MeshRenderer>();var mc=go.GetComponent<MeshCollider>();Need(f!=null&&mr!=null&&mc!=null&&f.sharedMesh==mesh&&mc.sharedMesh==mesh&&mr.enabled&&mr.additionalVertexStreams==null&&mr.sharedMaterials.Length==1&&AssetDatabase.GetAssetPath(mr.sharedMaterial)==(string)j["material"]&&mr.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.On&&mr.receiveShadows&&mc.enabled&&!mc.isTrigger&&!mc.convex&&(int)mc.cookingOptions==30,"exact source floor render/collision bindings and settings");
   return new JObject{["status"]="PASS",["recipe_sha256"]=RecipeSHA,["proof_sha256"]=ProofSHA,["owner"]=Owner,["asset"]=(string)r["asset"],["complete_channels_sha256"]=Complete(mesh),["source_preserved"]=true,["all_source_banks_X_and_moved_profiles_excluded"]=true,["verification_kind"]="exact_bindings_provenance_channels_only",["actual_PhysX_or_stock_body_executed"]=false,["SceneSave_executed"]=false,["site99percent_verified"]=false};
  }
  static Vector3 V(JToken a)=>new Vector3((float)a[0],(float)a[1],(float)a[2]);
  static JArray PV(Vector3 p)=>new JArray(p.x,p.y,p.z);
  static JObject Contact(RaycastHit h)=>new JObject{["owner"]=PathOf(h.collider.transform),["triangle"]=h.triangleIndex,["point"]=PV(h.point),["normal"]=PV(h.normal),["asset"]=h.collider is MeshCollider mc?AssetDatabase.GetAssetPath(mc.sharedMesh):null};
  static RaycastHit[] FlatY7(JToken sample)=>Physics.RaycastAll(V(sample["world_origin"]),Vector3.down,3f,~0,QueryTriggerInteraction.Ignore).Where(h=>Mathf.Abs(h.point.y-7)<=.04f&&h.normal.y>=.9f).OrderBy(h=>h.distance).ToArray();
  static JArray CaptureRetainedBaseline(JObject j)
  {
   var contract=JObject.Parse(File.ReadAllText((string)j["physical_contract"]["path"]));Physics.SyncTransforms();bool old=Physics.queriesHitBackfaces;try{Physics.queriesHitBackfaces=false;var rows=new JArray(contract["retained_source_contact_controls"].Select(q=>new JObject{["source_px"]=q["source_px"].DeepClone(),["world_origin"]=q["world_origin"].DeepClone(),["contacts"]=new JArray(FlatY7(q).Select(Contact))}));Need(rows.Count==389&&rows.All(q=>q["contacts"].Any()),"all six+383retained controls need actual baseline support");return rows;}finally{Physics.queriesHitBackfaces=old;}
  }
  // A vertical ray has fixed XZ. PhysX reconstructs hit positions from retriangulated
  // Float32 mesh barycentrics: compare both hits to that SAME ray, not to each other.
  // Eight ULP is the observed bounded envelope of all 389 controls in the archived actual run.
  static long OrderedFloat(float value){int bits=BitConverter.SingleToInt32Bits(value);return bits<0?-(long)(bits&0x7fffffff):(long)bits;}
  static bool OnFixedRay(float actual,float origin)=>float.IsFinite(actual)&&float.IsFinite(origin)&&Math.Abs(OrderedFloat(actual)-OrderedFloat(origin))<=8;
  static bool SameRetainedContact(JToken before,JToken after,JToken fixedOrigin)
  {
   var b=V(before["point"]);var a=V(after["point"]);var o=V(fixedOrigin);
   return (string)before["owner"]==(string)after["owner"]&&Mathf.Abs(b.y-a.y)<.00002f&&Vector3.Distance(V(before["normal"]),V(after["normal"]))<.00002f&&OnFixedRay(b.x,o.x)&&OnFixedRay(b.z,o.z)&&OnFixedRay(a.x,o.x)&&OnFixedRay(a.z,o.z);
  }
  static JObject StockBodyWalk(JObject contract)
  {
   var points=contract["stock_body_world_path"].Select(V).ToArray();var trials=new JArray();bool all=true;
   var probe=new GameObject("Temporary B2B4 restored old-hole actual stock-body QA"){hideFlags=HideFlags.HideAndDontSave};try
   {
    var cc=probe.AddComponent<CharacterController>();cc.height=1.72f;cc.radius=.28f;cc.center=Vector3.up*.86f;cc.skinWidth=.025f;cc.stepOffset=.28f;cc.slopeLimit=45;cc.minMoveDistance=0;
    foreach(bool reverse in new[]{false,true})
    {
     var path=reverse?points.Reverse().ToArray():points;cc.enabled=false;probe.transform.position=path[0];cc.enabled=true;Physics.SyncTransforms();bool pass=true;var samples=new JArray();int moves=0;
     for(int segment=1;segment<path.Length&&pass;segment++)
     {
      var begin=path[segment-1];var end=path[segment];int count=Mathf.CeilToInt(Vector3.Distance(begin,end)/.08f);
      for(int step=1;step<=count;step++)
      {
       var target=Vector3.Lerp(begin,end,(float)step/count);var delta=target-probe.transform.position;delta.y=-.03f;var flags=cc.Move(delta);moves++;var actual=probe.transform.position;float error=new Vector2(actual.x-target.x,actual.z-target.z).magnitude;
       var hits=Physics.RaycastAll(actual+Vector3.up*.3f,Vector3.down,.6f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider!=cc&&Mathf.Abs(h.point.y-7)<.08f&&h.normal.y>.5f).ToArray();bool okay=error<.04f&&Mathf.Abs(actual.y-7)<.08f&&hits.Length>0;
       if(step==count||step%20==0||!okay)samples.Add(new JObject{["segment"]=segment,["step"]=step,["target"]=PV(target),["actual"]=PV(actual),["horizontal_error"]=error,["flags"]=flags.ToString(),["passed"]=okay,["support_contacts"]=new JArray(hits.Select(Contact))});
       if(!okay){pass=false;break;}
      }
     }
     all&=pass;trials.Add(new JObject{["reverse"]=reverse,["passed"]=pass,["actual_Move_calls"]=moves,["samples"]=samples});
    }
   }finally{UnityEngine.Object.DestroyImmediate(probe);Physics.SyncTransforms();}
   return new JObject{["status"]=all?"PASS":"FAIL",["actual_CharacterController_Move_executed"]=true,["source_path"]=contract["stock_body_source_path"].DeepClone(),["height"]=1.72,["radius"]=.28,["skin_width"]=.025,["trials"]=trials};
  }
  public static JObject PhysicalVerify()
  {
   var exact=Verify();var j=Pins();var r=Registered();var contract=JObject.Parse(File.ReadAllText((string)j["physical_contract"]["path"]));var scene=SceneManager.GetActiveScene();int handle=scene.handle;string saved=Hash(ScenePath);bool wasDirty=scene.isDirty;Need(scene.path==ScenePath&&!EditorApplication.isCompiling&&!EditorApplication.isPlayingOrWillChangePlaymode,"stable in-memory Edit-mode physical QA");
   bool old=Physics.queriesHitBackfaces;Physics.SyncTransforms();var positive=new JArray();var negative=new JArray();var b2=new JArray();JObject stock;bool all=true;
   try
   {
    Physics.queriesHitBackfaces=false;
    foreach(var q in contract["positive_floor_samples"]){var hits=FlatY7(q);bool pass=hits.Any(h=>PathOf(h.collider.transform)==Owner);all&=pass;positive.Add(new JObject{["source_px"]=q["source_px"].DeepClone(),["passed"]=pass,["contacts"]=new JArray(hits.Select(Contact))});}
    foreach(var q in contract["bank_expected_empty_flat_Y7_outside_original_moved_profiles"]){var hits=FlatY7(q);bool pass=hits.Length==0;all&=pass;negative.Add(new JObject{["source_px"]=q["source_px"].DeepClone(),["passed"]=pass,["unexpected_contacts"]=new JArray(hits.Select(Contact))});}
    var baseline=(JArray)r["retained_source_contact_baseline"];Need(baseline!=null&&baseline.Count==contract["retained_source_contact_controls"].Count(),"actual prepared six+383retained contact baseline required");
    for(int i=0;i<baseline.Count;i++){var q=contract["retained_source_contact_controls"][i];var after=new JArray(FlatY7(q).Select(Contact));var before=(JArray)baseline[i]["contacts"];bool pass=JToken.DeepEquals(q["source_px"],baseline[i]["source_px"])&&before.Count>0&&before.Count==after.Count&&before.All(b=>after.Any(a=>SameRetainedContact(b,a,q["world_origin"])))&&after.All(a=>before.Any(b=>SameRetainedContact(b,a,q["world_origin"])));all&=pass;b2.Add(new JObject{["source_px"]=q["source_px"].DeepClone(),["passed"]=pass,["before_contacts"]=before.DeepClone(),["after_contacts"]=after});}
    stock=StockBodyWalk(contract);all&=(string)stock["status"]=="PASS";
   }finally{Physics.queriesHitBackfaces=old;}
   Need(SceneManager.GetActiveScene().handle==handle&&Hash(ScenePath)==saved&&scene.isDirty==wasDirty,"physical QA must preserve outer transaction Scene handle/saved bytes/dirty flag");
   var report=new JObject{["status"]=all?"PASS":"FAIL",["verification_kind"]="actual_PhysX_raycast_and_stock_CharacterController_Move",["exact_bindings"]=exact,["physical_contract_sha256"]=j["physical_contract"]["sha256"].DeepClone(),["positive_samples"]=positive,["positive_passed"]=positive.Count(q=>(bool)q["passed"]),["negative_bank_samples"]=negative,["negative_passed"]=negative.Count(q=>(bool)q["passed"]),["retained_source_contact_controls"]=b2,["stock_body_approach"]=stock,["SceneSave_executed"]=false,["source_geometry_regeneration_executed"]=false,["site99percent_verified"]=false};Need(all,"actual old-hole physical QA FAIL: "+report.ToString(Newtonsoft.Json.Formatting.None));return report;
  }
  public static void BeforeRebuild(string rootPath)
  {
   if(!File.Exists(Registry)){Need(Named(Owner).Length==0&&!File.Exists("Assets/ChooGuard/Art/StationInterior/OfficialFloorplan/Platform6_B2B4_OldHoleSourceFloor.asset"),"installed floor exists but source registry is missing");return;}if(rootPath=="FPSWorld"||rootPath.StartsWith("FPSWorld/",StringComparison.Ordinal)||rootPath=="맞이방 · 원본 정합"||rootPath.StartsWith("맞이방 · 원본 정합/",StringComparison.Ordinal)||rootPath=="실내 트윈 마감"||rootPath.StartsWith("실내 트윈 마감/",StringComparison.Ordinal)||rootPath==Owner)Verify();
  }
  // Independent top-level correction survives normal source-root replacement. If an explicit full-scene reconstruction omitted it, restore only this exact registered owner.
  public static JObject RestoreForRebuild()
  {
   var j=Pins();var r=Registered();SourceFiles(j);Platform6BanksB24Terminal.Verify();SourceBindings(j,true);var mesh=RegisteredMesh(j,r);if(Named(Owner).Length==0)MakeOwner(j,mesh);return Verify();
  }
  public static void DisposePrepared(Prepared p){if(p==null)return;if(p.Mesh!=null&&!AssetDatabase.Contains(p.Mesh))UnityEngine.Object.DestroyImmediate(p.Mesh);p.Mesh=null;}
 }
}
#endif
