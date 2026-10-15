#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using ChooGuard.App.Fps.Facilities;

namespace ChooGuard.Editor.Twin
{
    // Root-reviewable draft. A terminal exact derivative, composed after prior sources.
    public static class LegacyWestRetailArchitectureRegistry
    {
        public const string Registry="content/world/observed-interiors/legacy-west-retail-architecture-retirement.json";
        public static string PathOf(Transform t){var a=new List<string>();while(t!=null){a.Add(t.name);t=t.parent;}a.Reverse();return string.Join("/",a);}
        static void Need(bool ok,string why){if(!ok)throw new InvalidDataException("Legacy-west retail: "+why);}
        static string D(Mesh m)=>ArchitecturalCorrectionRegistry.CompleteChannelDigest(m);
        static JObject Read(){if(!File.Exists(Registry))return null;var j=JObject.Parse(File.ReadAllText(Registry));Need((string)j["schema"]=="chooguard.legacy-west-retail.installed-exact-derivatives.v1","schema");Need(OfficialFloorplanArchitectureRegistry.Hash((string)j["packet"])==(string)j["packet_sha256"],"sealed packet changed");return j;}
        public static bool HasOwner(string owner){var j=Read();return j!=null&&j["meshes"].Any(r=>(string)r["owner_path"]==owner);}
        public static bool HasCorrection(string source){var j=Read();return j!=null&&j["meshes"].Any(r=>r["original_source_keys"].Any(k=>(string)k==source));}
        static void Frame(Matrix4x4 m,JToken a){for(int r=0;r<4;r++)for(int c=0;c<4;c++)Need(float.IsFinite(m[r,c])&&Mathf.Abs(m[r,c]-(float)a[r][c])<.00004f,"owner frame changed");}
        static Mesh Output(JToken row)
        {
            string source=(string)row["source_asset"];Need(OfficialFloorplanArchitectureRegistry.Hash(source)==(string)row["source_live_file_sha256"],"retained source asset changed");var original=AssetDatabase.LoadAssetAtPath<Mesh>(source);Need(original!=null&&D(original)==(string)row["source_complete_channel_digest"],"retained complete source changed");
            string path=(string)row["replacement_asset"];Need(OfficialFloorplanArchitectureRegistry.Hash(path)==(string)row["replacement_file_sha256"]&&AssetDatabase.AssetPathToGUID(path)==(string)row["replacement_guid"],"saved derivative identity");var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);Need(m!=null&&!EditorUtility.IsDirty(m)&&D(m)==(string)row["replacement_complete_channel_digest"],"saved derivative channels");return m;
        }
        // Used ONLY after the prior Namcheon registry has positively validated its old13 output.
        public static bool TryResolvePriorSavedOutput(Mesh old,out Mesh result)
        {
            result=null;var j=Read();if(j==null||old==null)return false;string p=AssetDatabase.GetAssetPath(old);var rs=j["meshes"].Where(r=>(string)r["source_asset"]==p).ToArray();if(rs.Length==0)return false;Need(rs.Length==1,"ambiguous prior output");var row=rs[0];Need(D(old)==(string)row["source_complete_channel_digest"],"prior saved output channels changed");result=Output(row);return true;
        }
        public static Mesh ResolveSavedDerivative(Mesh old){Mesh m;return TryResolvePriorSavedOutput(old,out m)?m:old;}
        public static Mesh ResolveFinalForRebuild(Mesh baseline,string source,string owner,Matrix4x4 frame)
        {
            var j=Read();if(j==null)return baseline;var owners=j["meshes"].Where(r=>(string)r["owner_path"]==owner).ToArray();if(owners.Length==0)return baseline;var rs=owners.Where(r=>r["original_source_keys"].Any(k=>(string)k==source)).ToArray();Need(rs.Length==1,"missing/ambiguous exact owner and source role");var row=rs[0];Frame(frame,row["world_matrix"]);Need(row["original_source_keys"].Any(k=>(string)k==source),"generated source key changed");var output=Output(row);string digest=D(baseline);Need(digest==(string)row["source_complete_channel_digest"]||digest==(string)row["replacement_complete_channel_digest"],"terminal baseline changed; no ordinal rebase");return output;
        }
        static IEnumerable<Transform> Desc(Transform root)=>root.GetComponentsInChildren<Transform>(true);
        static Component ActionComponent(Transform root,JToken row,bool strictId)
        {
            string path=(string)row["path"],id=(string)row["global_object_id"],kind=(string)row["kind"];Type type=kind=="set_Light_enabled_false"?typeof(Light):kind=="set_Collider_enabled_false"?typeof(Collider):typeof(Renderer);
            var candidates=Desc(root).Where(t=>PathOf(t)==path).SelectMany(t=>t.GetComponents(type)).Cast<Component>().ToArray();
            GlobalObjectId gid;var exact=strictId&&GlobalObjectId.TryParse(id,out gid)?GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as Component:null;
            if(exact!=null){Need(candidates.Contains(exact),"saved explicit ID changed owner");candidates=new[]{exact};}
            else if(row["snapshot"]?["world_matrix"] is JArray mat)candidates=candidates.Where(c=>Enumerable.Range(0,4).All(r=>Enumerable.Range(0,4).All(k=>Mathf.Abs(c.transform.localToWorldMatrix[r,k]-(float)mat[r][k])<.00004f))).ToArray();
            Need(candidates.Length==1,"explicit component missing/ambiguous "+path);return candidates[0];
        }
        static bool Enabled(Component c)=>c is Renderer r?r.enabled:c is Collider co?co.enabled:((Light)c).enabled;
        static void Disable(Component c){if(c is Renderer r)r.enabled=false;else if(c is Collider co)co.enabled=false;else((Light)c).enabled=false;}
        // Only the reviewed misplaced Namcheon parent may be inactive. Original local
        // fields, leaf meshes/materials/boxes and animation vectors remain Namcheon-validated.
        const string RetiredDoorPath="실내 트윈 마감/2F 본관/문 · 남천할매떡볶이 출입문";
        const string RetiredDoorElement="2F-main-front-04-남천할매떡볶이";
        const string DoorPolicyKey="platform6_Namcheon_door_parent_retirement";
        const string DoorPlan="workers/twin/evidence/floor2-architectural-pdf-search-20261009/official-100083811-store-plan-page-1-embedded-2.jpeg";
        const string DoorPlanSha="49695f811421e97ea1b4a0b0c6f6657bf917a90730a0d9e19e152d1e1117caf6";
        const string DoorPdf="asset-library/research-public/2026-10-09/floor2-architectural-pdf-search/official-100083811-store-plan.pdf";
        const string DoorPdfSha="e43f640490af6d1fb20f3c58607cc3933398b8a1690383be8e73ed0d09350f4b";
        const string DoorManifest="workers/twin/output/floor2-whole-layout-20261009/newphoto-scope/namcheon-source-rebuild-manifest.json";
        const string DoorManifestSha="e7a89996e64d3658af54f3fec6a253a7fe77a245d9ef26d84c5c9c0ea4d417c5";
        const string DoorPacket="workers/twin/output/floor2-whole-layout-20261009/newphoto-scope/namcheon-source-front-packet.json";
        const string DoorPacketSha="669aca90a32f28c9eb55f86298b2c48db323fa0d1e5612bed8ee6c8889fc726f";
        static JObject ReviewedDoorPolicy(StationDoor door)
        {
            var j=Read();if(j==null||j[DoorPolicyKey]==null)return null;
            var row=j[DoorPolicyKey] as JObject;Need(row!=null,"exact door-parent retirement object required");
            Need((string)row["schema"]=="chooguard.platform6-namcheon-door-parent-retirement.v1"&&(string)row["action"]=="set_exact_parent_active_self_false"&&(string)row["path"]==RetiredDoorPath&&(string)row["element"]==RetiredDoorElement&&(int?)row["index"]==0,"unsupported door-parent retirement");
            if(door==null||door.Element!=RetiredDoorElement||door.Index!=0)return null;
            Need(PathOf(door.transform)==RetiredDoorPath&&door.transform.parent!=null&&PathOf(door.transform.parent)=="실내 트윈 마감/2F 본관"&&door.transform.parent.gameObject.activeInHierarchy&&door.enabled,"exact door parent/active ancestor changed");
            foreach(var pair in new[]{(path:DoorPlan,hash:DoorPlanSha,key:"source_plan"),(path:DoorPdf,hash:DoorPdfSha,key:"source_labeled_pdf"),(path:DoorManifest,hash:DoorManifestSha,key:"original_Namcheon_manifest"),(path:DoorPacket,hash:DoorPacketSha,key:"original_Namcheon_packet")})
                Need((string)row[pair.key]==pair.path&&(string)row[pair.key+"_sha256"]==pair.hash&&OfficialFloorplanArchitectureRegistry.Hash(pair.path)==pair.hash,"exact door retirement evidence changed");
            Need((bool?)row["annotation_is_identity_location_not_metric_footprint"]==true&&(bool?)row["retain_station_component_and_all_leaf_data"]==true,"door retirement scope changed");
            var packet=JObject.Parse(File.ReadAllText(DoorPacket));Frame(door.transform.localToWorldMatrix,packet["door_parent_pose"]["expected_parent_world_matrix"]);
            return row;
        }
        public static bool IsReviewedNamcheonDoorParentRetired(StationDoor door)
        {
            if(ReviewedDoorPolicy(door)==null)return false;
            Need(!door.gameObject.activeSelf&&!door.gameObject.activeInHierarchy,"reviewed retired door parent reactivated");return true;
        }
        public static bool HasValidNamcheonDoorParentState(StationDoor door)
        {
            if(ReviewedDoorPolicy(door)==null)return door!=null&&door.gameObject.activeInHierarchy;
            // Do not short-circuit on activeInHierarchy: a registered retired parent
            // must fail if it becomes active again.
            return IsReviewedNamcheonDoorParentRetired(door);
        }
        // Called only at the end of Namcheon.ApplyForRebuild, after its original field,
        // original-frame, sealed-pose and zero-component-field-change checks pass.
        public static void ApplyReviewedNamcheonDoorParentRetirement(StationDoor door)
        {
            if(ReviewedDoorPolicy(door)==null)return;
            Need(door.gameObject.activeSelf&&door.gameObject.activeInHierarchy,"fresh reviewed door parent is not active");
            string before=EditorJsonUtility.ToJson(door);door.gameObject.SetActive(false);
            Need(EditorJsonUtility.ToJson(door)==before,"parent retirement altered StationDoor fields");
            Need(IsReviewedNamcheonDoorParentRetired(door),"exact door-parent retirement failed");
        }
        static Transform RootFor(string fullPath){bool native=fullPath=="FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_5"||fullPath=="FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_6"||fullPath=="FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_0";string rootName=native?"FPSWorld/공식 자료 부산역 역사/MainShell":fullPath.StartsWith("실내 트윈 마감/")?string.Join("/",fullPath.Split('/').Take(2)):"맞이방 · 원본 정합";var root=GameObject.Find("/"+rootName);Need(root!=null,"root missing "+rootName);return root.transform;}
        public static void VerifyInstalled()
        {
            var j=Read();if(j==null)return;foreach(var row in j["meshes"]){GlobalObjectId gid;Need(GlobalObjectId.TryParse((string)row["component_global_object_id"],out gid),"saved ID");var c=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as Component;if(c==null){var matches=Desc(RootFor((string)row["owner_path"])).Where(t=>PathOf(t)==(string)row["owner_path"]).ToArray();Need(matches.Length==1,"rebuilt owner missing/ambiguous");c=(string)row["component_kind"]=="MeshCollider"?(Component)matches[0].GetComponent<MeshCollider>():matches[0].GetComponent<MeshRenderer>();}Need(c!=null&&PathOf(c.transform)==(string)row["owner_path"],"saved owner changed");var mesh=c is MeshCollider mc?mc.sharedMesh:c.GetComponent<MeshFilter>().sharedMesh;Need(mesh==Output(row),"saved derivative binding changed");Frame(c.transform.localToWorldMatrix,row["world_matrix"]);}
            foreach(var row in j["actions"])Need(!Enabled(ActionComponent(RootFor((string)row["path"]),row,true)),"obsolete exact component reactivated");
            foreach(var row in j["shadow_rows"]){var root=RootFor((string)row["path"]);var matches=Desc(root).Where(t=>PathOf(t)==(string)row["path"]).ToArray();Need(matches.Length==1,"saved shadow missing");var p=j["meshes"].Single(r=>(string)r["id"]==(string)row["primary_id"]);Need(matches[0].GetComponent<MeshFilter>().sharedMesh==Output(p),"old shadow roof remained");}
        }
        // Call before touching an existing root, so a lost installed derivative cannot be hidden by regeneration.
        public static void BeforeRebuild(string rootPath){Platform6BankB1Replay.BeforeRebuild(rootPath);Platform6BanksB24PlacementReplay.BeforeRebuild(rootPath);var j=Read();if(j==null)return;if(j["meshes"].Any(r=>((string)r["owner_path"]).StartsWith(rootPath+"/",StringComparison.Ordinal))||j["actions"].Any(r=>((string)r["path"]).StartsWith(rootPath+"/",StringComparison.Ordinal)))VerifyInstalled();}
        public static void AfterRebuild(Transform built,string rootPath)
        {
            var j=Read();if(j==null)return;
            foreach(var row in j["meshes"].Where(r=>((string)r["owner_path"]).StartsWith(rootPath+"/",StringComparison.Ordinal)))
            {
                var ts=Desc(built).Where(t=>PathOf(t)==(string)row["owner_path"]).ToArray();Need(ts.Length==1,"new derivative owner missing");var t=ts[0];Frame(t.localToWorldMatrix,row["world_matrix"]);var current=(string)row["component_kind"]=="MeshCollider"?t.GetComponent<MeshCollider>().sharedMesh:t.GetComponent<MeshFilter>().sharedMesh;Need(current==Output(row),"final Batch resolver hook missing");
            }
            foreach(var row in j["actions"].Where(r=>((string)r["path"]).StartsWith(rootPath+"/",StringComparison.Ordinal)))Disable(ActionComponent(built,row,false));
            foreach(var row in j["shadow_rows"].Where(r=>((string)r["path"]).StartsWith(rootPath+"/",StringComparison.Ordinal)))
            {
                var p=j["meshes"].Single(r=>(string)r["id"]==(string)row["primary_id"]);var parents=Desc(built).Where(t=>PathOf(t)==(string)p["owner_path"]).ToArray();Need(parents.Length==1,"shadow primary missing");string child=(string)row["child_name"];var t=parents[0].Find(child);
                if(t==null){t=new GameObject(child).transform;t.SetParent(parents[0],false);t.localPosition=V(row["local_position"]);t.localRotation=new Quaternion((float)row["local_rotation"][0],(float)row["local_rotation"][1],(float)row["local_rotation"][2],(float)row["local_rotation"][3]);t.localScale=V(row["local_scale"]);t.gameObject.SetActive((bool)row["active_self"]);t.gameObject.AddComponent<MeshFilter>();var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterials=row["materials"].Select(x=>AssetDatabase.LoadAssetAtPath<Material>((string)x)).ToArray();r.enabled=(bool)row["enabled"];r.shadowCastingMode=(UnityEngine.Rendering.ShadowCastingMode)(int)row["shadow_casting_mode"];r.receiveShadows=(bool)row["receive_shadows"];}
                Need(t.GetComponents<Collider>().Length==0,"unexpected shadow collider");t.GetComponent<MeshFilter>().sharedMesh=Output(p);
            }
            Platform6BankB1Replay.AfterRebuild(built,rootPath);Platform6BanksB24PlacementReplay.AfterRebuild(built,rootPath);
        }
        static Vector3 V(JToken a)=>new Vector3((float)a[0],(float)a[1],(float)a[2]);
        public static string[] SourceAssetsForRoot(string rootPath){var j=Read();return j==null?Array.Empty<string>():j["meshes"].Where(r=>((string)r["owner_path"]).StartsWith(rootPath+"/",StringComparison.Ordinal)).Select(r=>(string)r["generation_source_asset"]).Distinct().ToArray();}
        public static string[] InstalledAssetsForRoot(string rootPath){var j=Read();return j==null?Array.Empty<string>():j["meshes"].Where(r=>((string)r["owner_path"]).StartsWith(rootPath+"/",StringComparison.Ordinal)).Select(r=>(string)r["replacement_asset"]).ToArray();}
    }
}
#endif
