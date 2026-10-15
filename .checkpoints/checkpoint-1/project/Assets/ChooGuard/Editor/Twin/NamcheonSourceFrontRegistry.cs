#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using ChooGuard.App.Fps.Facilities;

namespace ChooGuard.Editor.Twin
{
    // Draft for root integration. This composes exact saved corrections; it never edits source assets.
    public static class NamcheonSourceFrontRegistry
    {
        public const string Registry="content/world/observed-interiors/namcheon-source-front-alignment.json";
        public const string Manifest="workers/twin/output/floor2-whole-layout-20261009/newphoto-scope/namcheon-source-rebuild-manifest.json";
        const string ManifestSha="e7a89996e64d3658af54f3fec6a253a7fe77a245d9ef26d84c5c9c0ea4d417c5";
        const string PacketSha="669aca90a32f28c9eb55f86298b2c48db323fa0d1e5612bed8ee6c8889fc726f";
        const string DoorElement="2F-main-front-04-남천할매떡볶이";
        const string DoorPath="실내 트윈 마감/2F 본관/문 · 남천할매떡볶이 출입문";
        static void Require(bool ok,string message){if(!ok)throw new InvalidDataException("Namcheon source replay: "+message);}
        static string Hash(string path){Require(File.Exists(path),"missing file "+path);using(var h=SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
        static JObject Plan(){Require(Hash(Manifest)==ManifestSha,"mapping manifest changed");var m=JObject.Parse(File.ReadAllText(Manifest));Require((string)m["packet_sha256"]==PacketSha&&Hash((string)m["packet"])==PacketSha,"reviewed packet changed");return m;}
        static JObject Packet(JObject plan)=>JObject.Parse(File.ReadAllText((string)plan["packet"]));
        static JObject Installed(){Require(File.Exists(Registry),"installed registry missing; rebuilding would lose source part");var r=JObject.Parse(File.ReadAllText(Registry));Require((string)r["schema"]=="chooguard.namcheon-source-front-registry.v1"&&(string)r["packet_sha256"]==PacketSha,"installed registry changed");Require(JToken.DeepEquals(r["dependencies"],Plan()["installed_dependencies"]),"installed dependency identities changed");return r;}
        static JToken Row(JObject p,string source)=>((JArray)p["entries"]).SingleOrDefault(r=>(string)r["source_asset"]==source||(string)r["baseline_asset"]==source);
        static string Digest(Mesh m){Require(m!=null&&m.isReadable&&m.blendShapeCount==0&&m.bindposes.Length==0&&m.boneWeights.Length==0,"missing/unreviewed complete mesh channels");return ArchitecturalCorrectionRegistry.CompleteChannelDigest(m);}
        static string PathOf(Transform t){var names=new List<string>();while(t!=null){names.Add(t.name);t=t.parent;}names.Reverse();return string.Join("/",names);}
        static Matrix4x4 Matrix(JToken rows){Require(rows is JArray a&&a.Count==4,"missing reviewed frame");var m=new Matrix4x4();for(int r=0;r<4;r++)for(int c=0;c<4;c++){m[r,c]=(float)rows[r][c];Require(float.IsFinite(m[r,c]),"nonfinite reviewed frame");}return m;}
        static void Frame(Matrix4x4 actual,JToken rows){var e=Matrix(rows);for(int r=0;r<4;r++)for(int c=0;c<4;c++)Require(float.IsFinite(actual[r,c])&&Mathf.Abs(actual[r,c]-e[r,c])<.00004f,"owner frame changed");}
        static Vector3 V(JToken p)=>new Vector3((float)p[0],(float)p[1],(float)p[2]);
        static Quaternion Q(JToken p)=>new Quaternion((float)p[0],(float)p[1],(float)p[2],(float)p[3]);
        static void Near(Vector3 a,Vector3 b,string label)=>Require((a-b).sqrMagnitude<1e-10f,label+" changed");
        static void Near(Quaternion a,Quaternion b,string label)=>Require(Mathf.Abs(Quaternion.Dot(a,b))>.9999999f,label+" changed");
        static IEnumerable<JToken> Patches(JObject packet)=>packet["mesh_pose_patches"].Concat(packet["native_wall_clips"]);
        static string Role(JToken patch)=>(string)(patch["mesh_id"]??patch["role"]);
        static JToken Patch(JObject packet,string role)=>Patches(packet).Single(p=>Role(p)==role);
        static Mesh CheckedMesh(string path,string guid,string file,string meta,string digest)
        {
            Require(Hash(path)==file&&(meta==null||Hash(path+".meta")==meta)&&AssetDatabase.AssetPathToGUID(path)==guid,"persistent mesh identity changed "+path);
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);Require(mesh!=null&&!EditorUtility.IsDirty(mesh)&&Digest(mesh)==digest,"persistent complete channels changed "+path);return mesh;
        }
        static Mesh Output(JToken row,JObject installed)
        {
            var deps=((JArray)installed["dependencies"]).Where(x=>(string)x["role"]==(string)row["role"]).ToArray();Require(deps.Length==1,"lost/duplicate installed output "+row["role"]);var d=deps[0];
            Require((string)d["complete_channel_digest"]==(string)row["output_complete_digest"],"installed output digest changed");return LegacyWestRetailArchitectureRegistry.ResolveSavedDerivative(CheckedMesh((string)d["asset"],(string)d["guid"],(string)d["file_sha256"],null,(string)row["output_complete_digest"]));
        }
        static void Baseline(JToken row)
        {
            CheckedMesh((string)row["source_asset"],(string)row["source_guid"],(string)row["source_file_sha256"],(string)row["source_meta_sha256"],(string)row["source_complete_digest"]);
            CheckedMesh((string)row["baseline_asset"],(string)row["baseline_guid"],(string)row["baseline_file_sha256"],(string)row["baseline_meta_sha256"],(string)row["baseline_complete_digest"]);
            if(row["prior_registry_row"]?.Type!=JTokenType.Null&&row["prior_registry_row"]!=null)
            {
                string path=(string)row["prior_registry"];Require(File.Exists(path),"prior source correction missing");var old=JObject.Parse(File.ReadAllText(path));
                var entries=(JArray)old["meshes"];var matches=entries.Where(r=>(string)r["source_asset"]==(string)row["source_asset"]).ToArray();Require(matches.Length==1&&JToken.DeepEquals(matches[0],row["prior_registry_row"]),"prior source correction row changed");
            }
            if(row["architectural_row"]?.Type!=JTokenType.Null&&row["architectural_row"]!=null)
            {
                var old=JObject.Parse(File.ReadAllText(ArchitecturalCorrectionRegistry.Registry));var matches=((JArray)old["entries"]).Where(r=>(string)r["source_asset"]==(string)row["source_asset"]).ToArray();Require(matches.Length==1&&JToken.DeepEquals(matches[0],row["architectural_row"]),"prior architectural correction changed");
            }
        }
        static void Resources(JObject plan,JObject packet,JObject installed)
        {
            Require(((JArray)plan["entries"]).Count==13&&((JArray)installed["dependencies"]).Count==13,"thirteen source composites required");
            foreach(var row in plan["entries"]){Baseline(row);Output(row,installed);}
            foreach(var proof in packet["material_dependencies"])Require(Hash((string)proof["path"])==(string)proof["file_sha256"]&&Hash((string)proof["path"]+".meta")== (string)proof["meta_sha256"]&&AssetDatabase.AssetPathToGUID((string)proof["path"])==(string)proof["guid"],"material dependency changed");
            foreach(var proof in packet["source_geometry_dependencies"])Require(Hash((string)proof["path"])==(string)proof["sha256"],"native geometry source changed");
            foreach(var photo in packet["photos"])Require(Hash((string)photo["path"])==(string)photo["sha256"],"original photographic support changed");
            Require(Hash((string)packet["source_support"]["path"])==(string)packet["source_support"]["sha256"],"floor/ceiling support changed");
            Require(Hash((string)packet["source_full_channel_metadata"])==(string)packet["source_full_channel_metadata_sha256"],"full source channel export changed");
            foreach(var patch in Patches(packet)){Require(Hash((string)patch["full_channel_export"]["path"])==(string)patch["full_channel_export"]["binary_sha256"],"source binary changed");Require(Hash((string)patch["expected_output"]["path"])==(string)patch["expected_output"]["binary_sha256"],"sealed output binary changed");}
            foreach(var leaf in plan["door_leaves"])CheckedMesh((string)leaf["asset"],(string)leaf["guid"],(string)leaf["file_sha256"],(string)leaf["meta_sha256"],(string)leaf["complete_digest"]);
        }
        public static bool HasCorrection(string source)
        {
            if(source==null)return false;var p=Plan();var row=Row(p,source);if(row==null)return false;Installed();return true;
        }
        public static string[] SourceAssetsForRoot(string ownerRoot)
        {
            var p=Plan();string prefix=ownerRoot.Trim('/')+"/";var rows=((JArray)p["entries"]).Where(r=>!(bool)r["native"]&&((JArray)r["owner_paths"]).Any(o=>((string)o).StartsWith(prefix,StringComparison.Ordinal))).ToArray();
            if(rows.Length==0)return Array.Empty<string>();Installed();return rows.Select(r=>(string)r["source_asset"]).Distinct().ToArray();
        }
        public static string[] InstalledAssetsForRoot(string ownerRoot)
        {
            var p=Plan();var sources=SourceAssetsForRoot(ownerRoot);if(sources.Length==0)return Array.Empty<string>();var i=Installed();return ((JArray)p["entries"]).Where(r=>sources.Contains((string)r["source_asset"])).Select(r=>AssetDatabase.GetAssetPath(Output(r,i))).ToArray();
        }
        // Only saved persistent output may bypass the preceding original correction. Fresh shifted recipes do not.
        public static bool TryResolveInstalled(Mesh candidate,string source,out Mesh resolved)
        {
            resolved=null;var plan=Plan();var row=Row(plan,source);if(row==null)return false;var installed=Installed();Baseline(row);var target=Output(row,installed);
            if(candidate==null)return false;
            if(AssetDatabase.GetAssetPath(candidate)==AssetDatabase.GetAssetPath(target)){Require(Digest(candidate)==Digest(target),"installed candidate channels changed");resolved=target;return true;}
            Mesh derivative;if(LegacyWestRetailArchitectureRegistry.TryResolvePriorSavedOutput(candidate,out derivative)&&derivative==target){resolved=target;return true;}
            return false;
        }
        // Called after the old guide resolver. Keep its result until observed facade validation has also finished.
        public static Mesh VerifyCompositeBaseline(Mesh baseline,string source)
        {
            var plan=Plan();var row=Row(plan,source);if(row==null)return baseline;var installed=Installed();Baseline(row);Output(row,installed);
            Require(Digest(baseline)==(string)row["baseline_complete_digest"],"fresh complete baseline changed; ordinal rebase is forbidden "+source);
            var original=AssetDatabase.LoadAssetAtPath<Mesh>((string)row["baseline_asset"]);Require(baseline.indexFormat==original.indexFormat&&baseline.GetVertexAttributes().SequenceEqual(original.GetVertexAttributes())&&Vector3.Distance(baseline.bounds.center,original.bounds.center)<.00004f&&Vector3.Distance(baseline.bounds.size,original.bounds.size)<.00004f,"fresh baseline attribute layout/bounds changed");return baseline;
        }
        // Public registry resolution returns the saved composite. Builders use the baseline-only API until facade validation finishes.
        public static Mesh ResolveComposedForRebuild(Mesh baseline,string source)
        {
            var plan=Plan();var row=Row(plan,source);if(row==null)return baseline;Mesh target;if(TryResolveInstalled(baseline,source,out target))return target;VerifyCompositeBaseline(baseline,source);return Output(row,Installed());
        }
        // Architectural resolution can precede a guide stage; do not prematurely replace that intermediate source.
        public static Mesh VerifyArchitecturalIntermediate(Mesh baseline,string source)
        {
            var p=Plan();var row=Row(p,source);if(row==null)return baseline;
            if(row["prior_registry"]?.Type!=JTokenType.Null&&row["prior_registry"]!=null)return baseline;
            return VerifyCompositeBaseline(baseline,source);
        }
        public static Mesh ResolveForRebuild(Mesh freshBaseline,string source,string owner,Matrix4x4 worldMatrix)
        {
            var plan=Plan();var row=Row(plan,source);if(row==null)return freshBaseline;Require(!(bool)row["native"],"native wall requires explicit complete guide baseline");
            var packet=Packet(plan);var patch=Patch(packet,(string)row["role"]);var proofs=patch["render_owners"]??patch["collider_owners"];
            var match=proofs.Children().SingleOrDefault(o=>(string)o["owner"]["path"]==owner);Require(match!=null,"generated owner changed "+owner);Frame(worldMatrix,match["owner"]["world_matrix"]);
            Mesh resolved;if(TryResolveInstalled(freshBaseline,source,out resolved))return resolved;
            VerifyCompositeBaseline(freshBaseline,source);return Output(row,Installed());
        }
        public static Mesh ResolveNativeCompositeForRebuild(Mesh completeGuideBaseline,string source,string owner,Matrix4x4 worldMatrix)
        {
            var plan=Plan();var row=Row(plan,source);Require(row!=null&&(bool)row["native"],"unknown native composite source");var p=Patch(Packet(plan),(string)row["role"]);Require((string)p["owner_receipt"]["owner"]["path"]==owner,"native composite owner changed");Frame(worldMatrix,p["owner_receipt"]["owner"]["world_matrix"]);
            VerifyCompositeBaseline(completeGuideBaseline,source);return Output(row,Installed());
        }
        static IEnumerable<JToken> RenderOwners(JToken p)=>p["render_owners"]!=null?p["render_owners"].Children():((string)p["role"]).EndsWith("render",StringComparison.Ordinal)?new[]{p["owner_receipt"]}:Enumerable.Empty<JToken>();
        static IEnumerable<JToken> ColliderOwners(JToken p)=>p["collider_owners"]!=null?p["collider_owners"].Children():((string)p["role"]).EndsWith("collider",StringComparison.Ordinal)?new[]{p["owner_receipt"]}:Enumerable.Empty<JToken>();
        // The official drawing supersedes a specifically registered obsolete wall. Preserve
        // the original source identity and exact mesh/pose checks while recognizing its retirement.
        static bool IsReviewedFloorplanRetired(Component component,Mesh expected)
        {
            if(!File.Exists(OfficialFloorplanArchitectureRegistry.Registry))return false;
            var reg=JObject.Parse(File.ReadAllText(OfficialFloorplanArchitectureRegistry.Registry));
            string path=PathOf(component.transform);var rows=reg["retirements"].Where(x=>(string)x["path"]==path).ToArray();
            if(rows.Length==0)return false;Require(rows.Length==1,"ambiguous reviewed retirement");var row=rows[0];
            Require(OfficialFloorplanArchitectureRegistry.MeshHash(expected)==(string)row["mesh_payload_sha256"]&&AssetDatabase.GetAssetPath(expected)==(string)row["asset"]&&AssetDatabase.AssetPathToGUID((string)row["asset"])==(string)row["guid"],"retired source identity changed");
            Frame(component.transform.localToWorldMatrix,row["world_matrix"]);
            var r=component.GetComponent<MeshRenderer>();Require(r!=null&&!r.enabled&&component.GetComponents<Collider>().All(c=>!c.enabled),"reviewed retired wall reactivated");return true;
        }
        static void InstalledBindings(JObject plan,JObject packet,JObject installed)
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var filters=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.gameObject.scene==scene).ToArray();var colliders=UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.gameObject.scene==scene).ToArray();
            foreach(var row in plan["entries"])
            {
                var patch=Patch(packet,(string)row["role"]);var target=Output(row,installed);
                foreach(var proof in RenderOwners(patch))
                {
                    string path=(string)proof["owner"]["path"];var fs=filters.Where(f=>PathOf(f.transform)==path).ToArray();Require(fs.Length==1,"missing/duplicate installed render owner "+path);var f=fs[0];var r=f.GetComponent<MeshRenderer>();Require(r!=null&&(r.enabled||IsReviewedFloorplanRetired(r,target))&&f.gameObject.activeInHierarchy&&f.sharedMesh==target&&r.additionalVertexStreams==null,"lost installed source part "+path);Frame(f.transform.localToWorldMatrix,proof["owner"]["world_matrix"]);
                    if((bool)row["native"])Require(GlobalObjectId.GetGlobalObjectIdSlow(r).ToString()==(string)proof["owner"]["global_object_id"],"native renderer identity changed");
                    Require(r.sharedMaterials.Select(AssetDatabase.GetAssetPath).SequenceEqual(proof["owner"]["materials"].Select(x=>(string)x)),"render material slots changed");
                }
                foreach(var proof in ColliderOwners(patch))
                {
                    string path=(string)proof["owner"]["path"];var cs=colliders.Where(c=>PathOf(c.transform)==path).ToArray();Require(cs.Length==1,"missing/duplicate installed collision owner "+path);var c=cs[0];Require((c.enabled||IsReviewedFloorplanRetired(c,target))&&c.gameObject.activeInHierarchy&&c.sharedMesh==target&&c.isTrigger==(bool)proof["owner"]["trigger"]&&c.convex==(bool)proof["owner"]["convex"],"lost installed physical composite/flags "+path);Frame(c.transform.localToWorldMatrix,proof["owner"]["world_matrix"]);
                    var settings=plan["collider_settings"].Single(x=>(string)x["role"]==(string)row["role"]&&(string)x["owner"]==path);
                    Require((int)c.cookingOptions==(int)settings["cooking_options"]&&c.includeLayers.value==(int)settings["include_layers"]&&c.excludeLayers.value==(int)settings["exclude_layers"]&&c.layerOverridePriority==(int)settings["priority"]&&c.providesContacts==(bool)settings["provides_contacts"]&&c.sharedMaterial==null,"collision cooking/filter settings changed");
                    if((bool)row["native"])Require(GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()==(string)proof["owner"]["global_object_id"],"native collider identity changed");
                }
            }
            var doors=UnityEngine.Object.FindObjectsByType<StationDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(d=>d.gameObject.scene==scene&&d.Element==DoorElement&&d.Index==0).ToArray();Require(doors.Length==1&&PathOf(doors[0].transform)==DoorPath&&doors[0].enabled&&LegacyWestRetailArchitectureRegistry.HasValidNamcheonDoorParentState(doors[0]),"installed door missing/duplicated");VerifyForRebuild(doors[0]);
        }
        public static void ValidateInstalledBeforeRebuild(string ownerRoot)
        {
            if(SourceAssetsForRoot(ownerRoot).Length==0)return;Require(!EditorApplication.isPlaying&&!EditorApplication.isCompiling,"stable Edit mode required");var p=Plan();var packet=Packet(p);var installed=Installed();Resources(p,packet,installed);InstalledBindings(p,packet,installed);
        }
        public static string VerifyInstalled(string[] args)
        {
            Require(args==null||args.Length==0,"no override arguments accepted");var p=Plan();var packet=Packet(p);var installed=Installed();Resources(p,packet,installed);InstalledBindings(p,packet,installed);return "EXACT_NAMCHEON_SOURCE_COMPOSITES_VERIFIED 13; door parent delta; zero StationDoor field edits";
        }
        public static void RejectLegacyNativeReplacement(GameObject go)
        {
            Native0SourceXFloorReplay.RejectUncomposedOriginalReplacement(go);
            if(go==null)return;var p=Plan();string path=PathOf(go.transform);if(!((JArray)p["entries"]).Any(r=>(bool)r["native"]&&((JArray)r["owner_paths"]).Any(o=>(string)o==path)))return;
            var packet=Packet(p);var installed=Installed();Resources(p,packet,installed);InstalledBindings(p,packet,installed);throw new InvalidDataException("Namcheon native render/collision aperture is a protected composite. Legacy original-source replacement is rejected before writes. Supply the exact complete guide baseline to ResolveNativeCompositeForRebuild in a reviewed transaction.");
        }
        public static bool IsProtectedDoor(string element,int index)=>element==DoorElement&&index==0;
        public static bool IsProtectedLeafAsset(string path)=>path!=null&&((JArray)Plan()["door_leaves"]).Any(x=>(string)x["asset"]==path);
        public static Mesh ResolveLeafForRebuild(Mesh fresh,string path)
        {
            var leaf=((JArray)Plan()["door_leaves"]).Single(l=>(string)l["asset"]==path);Installed();Require(Digest(fresh)==(string)leaf["complete_digest"],"fresh door leaf complete channels changed");return CheckedMesh(path,(string)leaf["guid"],(string)leaf["file_sha256"],(string)leaf["meta_sha256"],(string)leaf["complete_digest"]);
        }
        static void MaterialCandidate(Material actual,string path)
        {
            var expected=AssetDatabase.LoadAssetAtPath<Material>(path);Require(actual!=null&&expected!=null&&actual.shader==expected.shader,"door material shader changed");
            for(int i=0;i<expected.shader.GetPropertyCount();i++)
            {
                string n=expected.shader.GetPropertyName(i);switch(expected.shader.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:Require(actual.GetColor(n)==expected.GetColor(n),"door material color changed");break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:Require(actual.GetVector(n)==expected.GetVector(n),"door material vector changed");break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:case UnityEngine.Rendering.ShaderPropertyType.Range:Require(actual.GetFloat(n)==expected.GetFloat(n),"door material scalar changed");break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:Require(actual.GetTexture(n)==expected.GetTexture(n)&&actual.GetTextureScale(n)==expected.GetTextureScale(n)&&actual.GetTextureOffset(n)==expected.GetTextureOffset(n),"door material texture changed "+path+" property="+n+" fresh="+(actual.GetTexture(n)==null?"null":AssetDatabase.GetAssetPath(actual.GetTexture(n)))+" saved="+(expected.GetTexture(n)==null?"null":AssetDatabase.GetAssetPath(expected.GetTexture(n)))+" freshScale="+actual.GetTextureScale(n)+" savedScale="+expected.GetTextureScale(n)+" freshOffset="+actual.GetTextureOffset(n)+" savedOffset="+expected.GetTextureOffset(n));break;
                    case UnityEngine.Rendering.ShaderPropertyType.Int:Require(actual.GetInteger(n)==expected.GetInteger(n),"door material integer changed");break;
                    default:throw new InvalidDataException("Unreviewed door shader property");
                }
            }
        }
        static void DoorLocal(StationDoor door,JObject p)
        {
            var f=p["door_fields"];Require(door.Element==(string)f["Element"]&&door.Index==(int)f["Index"]&&door.Label==(string)f["Label"]&&(int)door.Kind==(int)f["Kind"]&&(int)door.Use==(int)f["Use"]&&door.Width==(float)f["Width"]&&door.Height==(float)f["Height"]&&door.ObservedOpen==(float)f["ObservedOpen"]&&door.Sealed==(string)f["Sealed"]&&door.Floor==(string)f["Floor"]&&door.CarId==(string)f["CarId"]&&door.CallPanel==null&&door.Lantern==null&&door.Cabin==null,"StationDoor baseline fields changed");
            Near(door.Centre,V(f["Centre"]),"parent-local Centre");Near(door.Normal,V(f["Normal"]),"parent-local Normal");Near(door.Along,V(f["Along"]),"parent-local Along");Near(door.CabinSize,V(f["CabinSize"]),"CabinSize");Require(door.Leaves.Length==2,"door leaf count changed");
            foreach(var l in p["door_leaves"])
            {
                var matches=door.Leaves.Where(x=>x.Pivot!=null&&x.Pivot.name==(string)l["name"]).ToArray();Require(matches.Length==1,"missing/duplicate door leaf");var leaf=matches[0];Require(leaf.Pivot.parent==door.transform,"door leaf parent changed");Near(leaf.Pivot.localPosition,V(l["position"]),"leaf local position");Near(leaf.Pivot.localRotation,Q(l["rotation"]),"leaf local rotation");Near(leaf.Pivot.localScale,V(l["scale"]),"leaf local scale");Near(leaf.Slide,V(l["slide"]),"leaf animation vector");Require(leaf.Swing==(float)l["swing"]&&leaf.Length==(float)l["length"],"leaf operation fields changed");
                var filter=leaf.Pivot.GetComponent<MeshFilter>();var renderer=leaf.Pivot.GetComponent<MeshRenderer>();var boxes=leaf.Pivot.GetComponents<BoxCollider>();Require(filter!=null&&Digest(filter.sharedMesh)==(string)l["complete_digest"]&&renderer!=null&&renderer.enabled&&boxes.Length==1,"door channels/collider missing");var box=boxes[0];Near(box.center,V(l["box_center"]),"leaf collision center");Near(box.size,V(l["box_size"]),"leaf collision size");Require(box.isTrigger==(bool)l["box_is_trigger"]&&box.enabled==(bool)l["box_enabled"],"leaf collision flags changed");Require(renderer.sharedMaterials.Length==l["materials"].Count(),"door material count changed");for(int i=0;i<renderer.sharedMaterials.Length;i++)MaterialCandidate(renderer.sharedMaterials[i],(string)l["materials"][i]);
            }
        }
        static void Pose(Transform t,Matrix4x4 world)
        {
            var local=t.parent==null?world:t.parent.worldToLocalMatrix*world;var sx=local.GetColumn(0).magnitude;var sy=local.GetColumn(1).magnitude;var sz=local.GetColumn(2).magnitude;Require(sx>0&&sy>0&&sz>0,"degenerate door TRS");t.localPosition=local.GetColumn(3);t.localRotation=Quaternion.LookRotation(local.GetColumn(2)/sz,local.GetColumn(1)/sy);t.localScale=new Vector3(sx,sy,sz);
        }
        public static void ApplyForRebuild(StationDoor door)
        {
            if(!IsProtectedDoor(door.Element,door.Index))return;var p=Plan();Installed();var packet=Packet(p);DoorLocal(door,p);var pose=packet["door_parent_pose"];Frame(door.transform.localToWorldMatrix,pose["old_parent_world_matrix"]);string fields=EditorJsonUtility.ToJson(door);Pose(door.transform,Matrix(pose["expected_parent_world_matrix"]));Require(EditorJsonUtility.ToJson(door)==fields,"pose replay altered StationDoor fields");Frame(door.transform.localToWorldMatrix,pose["expected_parent_world_matrix"]);LegacyWestRetailArchitectureRegistry.ApplyReviewedNamcheonDoorParentRetirement(door);
        }
        public static void VerifyForRebuild(StationDoor door)
        {
            if(!IsProtectedDoor(door.Element,door.Index))return;var p=Plan();Installed();DoorLocal(door,p);Frame(door.transform.localToWorldMatrix,Packet(p)["door_parent_pose"]["expected_parent_world_matrix"]);
        }
    }
}
#endif
