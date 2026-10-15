#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using ChooGuard.App.Fps.Architecture;

namespace ChooGuard.Editor.Twin
{
    // Reset-epoch construction. Deliberately does not call historical top-root installers/guards.
    public static class FPSWorldBlueprintBuilder
    {
        public const string Authority="content/world/observed-interiors/fpsworld-blueprint-source-authority-20261010.json";
        const string AuthoritySHA="15b5dfaaee55c538aa2c741a966d8511a98322131e89891a8bfd86a01ece1461";
        const string ScenePath="Assets/ChooGuard/Scenes/FpsStation.unity";
        const string Art="Assets/ChooGuard/Art/StationInterior/BlueprintRebuild20261010";
        static void Need(bool b,string s){if(!b)throw new InvalidDataException("FPSWorld blueprint construction: "+s);}
        static JObject J(string p)=>JObject.Parse(File.ReadAllText(p));
        public static string Hash(string p){using(var h=System.Security.Cryptography.SHA256.Create())using(var f=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
        static long Id(UnityEngine.Object o)=>(long)GlobalObjectId.GetGlobalObjectIdSlow(o).targetObjectId;
        static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
        static string Safe(string s){Need(s!=null&&Regex.IsMatch(s,"^[A-Za-z0-9][A-Za-z0-9_-]{0,99}$"),"safe explicit generated identifier");return s;}
        static JObject Rules(){Need(Hash(Authority)==AuthoritySHA,"canonical user blueprint/hierarchy authority changed");return J(Authority);}
        public static Transform ExistingFPSWorld()
        {
            var scene=SceneManager.GetActiveScene();Need(scene.path==ScenePath,"current FpsStation required");var roots=scene.GetRootGameObjects();
            Need(roots.Length==2&&roots.Count(g=>g.name=="FPSWorld")==1&&roots.Count(g=>g.name=="ProbeVolumePerSceneData")==1,"only observed FPSWorld and existing engine Probe roots permitted");
            var fps=roots.Single(g=>g.name=="FPSWorld");Need(Id(fps)==610033089&&Id(roots.Single(g=>g.name=="ProbeVolumePerSceneData"))==1626042824,"exact user-retained root IDs");
            Need(fps.transform.localToWorldMatrix==Matrix4x4.identity,"reviewed blueprint common frame requires retained FPSWorld identity");return fps.transform;
        }
        public static void ValidateParent(Transform parent)
        {
            var fps=ExistingFPSWorld();Need(parent!=null&&(parent==fps||parent.IsChildOf(fps)),"existing FPSWorld descendant parent is mandatory");
        }
        static long[] Roots()=>SceneManager.GetActiveScene().GetRootGameObjects().Select(Id).OrderBy(x=>x).ToArray();
        public static void AssertNoNewRoots(long[] before){ExistingFPSWorld();Need(before.SequenceEqual(Roots()),"new or replaced top-level root rejected");}
        public static void ValidatePrimarySource(JToken source,string storey)
        {
            var rules=Rules();Need(source is JObject&&source["path"]!=null&&source["sha256"]!=null,"explicit primary blueprint source/path/hash");
            var rows=rules["allowed_primary_layout_sources"].Where(p=>(string)p["path"]==(string)source["path"]&&(string)p["storey"]==storey&&(string)p["sha256"]==(string)source["sha256"]).ToArray();
            Need(rows.Length==1&&(string)rows[0]["kind"]=="original_blueprint","primary layout source must be the exact approved blueprint for this storey");Need(Hash((string)source["path"])==(string)source["sha256"],"primary blueprint content changed");
        }
        static void SourcePart(JToken part)
        {
            Safe((string)part["id"]);string floor=(string)part["storey"];Need(floor!=null&&Regex.IsMatch(floor,"^[1-5]F$"),"explicit storey");ValidatePrimarySource(part["source_blueprint"],floor);
            Need(!string.IsNullOrWhiteSpace((string)part["feature_id"])&&part["source_controls_px"] is JArray controls&&controls.Count>0,"per-part blueprint feature and source-pixel controls");Need(part["height_basis"]!=null&&!string.IsNullOrWhiteSpace(HeightBasis(part)),"explicit measured or provisional model height basis");
            string role=(string)part["role"];Need(new[]{"wall","partition","column","floor","slab","shaft","guard","stair","door-frame","aperture-lintel"}.Contains(role),"explicit architectural role; interaction parts unsupported");
            if(floor=="2F"&&(role=="floor"||role=="slab"||role=="guard"))Need(part["front_X_void_proof"] is JObject,"2F slab/guard requires reviewed large-front-X open-void proof");
            foreach(var pin in part["proof_files"] as JArray??new JArray())Need(Hash((string)pin["path"])==(string)pin["sha256"],"per-part sealed blueprint proof changed");
            if(part["front_X_void_proof"] is JObject proof)Need(Hash((string)proof["path"])==(string)proof["sha256"],"reviewed open-void proof changed");
        }
        static string HeightBasis(JToken part)=>part["height_basis"]?.Type==JTokenType.String?(string)part["height_basis"]:part["height_basis"]?.ToString(Newtonsoft.Json.Formatting.None);
        // Forward-only contract: historical receipts keep their original provenance unchanged.
        public static void ValidateNewFrameRegistration(JToken part)
        {
            const string phase1="content/world/observed-interiors/fpsworld-accepted-region-frames-phase1-20261010.json";
            const string boardingGround="content/world/observed-interiors/fpsworld-accepted-boarding-ground-frame-20261010.json";
            const string boardingGroundR5="content/world/observed-interiors/fpsworld-accepted-boarding-ground-frame-r5-20261010.json";
            Need(part["frame_registry"] is JObject&&part["registrySHA"]!=null&&!string.IsNullOrWhiteSpace((string)part["frame_id"]),"new geometry requires explicit frame_id, registrySHA and frame_registry");
            string registry=(string)part["frame_registry"]["path"];
            Need((registry==phase1||registry==boardingGround||registry==boardingGroundR5)&&(string)part["frame_registry"]["sha256"]==(string)part["registrySHA"]&&Hash(registry)==(string)part["registrySHA"],"exact accepted canonical frame registry");
            var frames=J(registry);Need(frames["global_fallback"].Type==JTokenType.Null,"no global registration fallback");
            string registrationRegion=(string)(part["registration_region_id"]??part["region_id"]);
            var matches=frames["regions"].Where(r=>(string)r["frame_id"]==(string)part["frame_id"]&&(string)r["region_id"]==registrationRegion).ToArray();
            Need(matches.Length==1&&(string)matches[0]["status"]=="ACCEPTED_BOUNDED_USE"&&(string)part["storey"]=="2F","new part must bind one accepted bounded storey/region");
            Need(JToken.DeepEquals(part["registration"],frames["candidate_registry"]),"new part preserves exact original reviewed frame provenance");
            foreach(string key in new[]{"candidate_registry","independent_review"})Need(Hash((string)frames[key]["path"])==(string)frames[key]["sha256"],"accepted frame evidence changed");
            Need((string)part["source_blueprint"]["sha256"]==(string)frames["source_raster"]["sha256"],"frame and source raster identity");
            if(registry==boardingGround||registry==boardingGroundR5)
            {
                Need(registrationRegion=="BOARDING2F_GROUND_FEATURES"&&(string)matches[0]["qualification_scope"]=="SOURCE_GROUND_ONLY"&&new[]{"floor","slab"}.Contains((string)part["role"]),"boarding frame is qualified for reviewed ground only");
                Need(JToken.DeepEquals(part["ground_geometry_packet"],frames["ground_geometry_packet"]),"exact independently reviewed ground packet binding");
                var pin=frames["ground_geometry_packet"];Need(Hash((string)pin["path"])==(string)pin["sha256"],"qualified ground source packet changed");
                var approved=J((string)pin["path"])["parts"].Where(p=>(string)p["id"]==(string)part["id"]).ToArray();
                Need(approved.Length==1,"part belongs to the finite qualified ground packet");
                foreach(string channel in new[]{"vertices_world","triangles","uv","normals","source_controls_px","height_basis"})Need(JToken.DeepEquals(part[channel],approved[0][channel]),"qualified ground geometry/source arrays changed: "+channel);
            }
        }
        static string AssetHash(JToken part,string key)=> (string)(part[key+"_sha256"]??part[key.Replace("_asset","")+"_sha256"]);
        static Transform Child(Transform parent,string name,List<GameObject> created)
        {
            ValidateParent(parent);var found=Enumerable.Range(0,parent.childCount).Select(parent.GetChild).Where(t=>t.name==name).ToArray();Need(found.Length<=1,"ambiguous generated hierarchy");if(found.Length==1){Need(!name.StartsWith("Blueprint_",StringComparison.Ordinal)||created.Contains(found[0].gameObject),"preexisting generated group without exact receipt is rejected");Need(found[0].localPosition==Vector3.zero&&found[0].localRotation==Quaternion.identity&&found[0].localScale==Vector3.one,"existing container must retain identity frame");return found[0];}
            var go=new GameObject(name);go.transform.SetParent(parent,false);created.Add(go);return go.transform;
        }
        static JObject MeshPin(Mesh mesh){string p=AssetDatabase.GetAssetPath(mesh);return new JObject{["path"]=p,["sha256"]=Hash(p),["meta_sha256"]=Hash(p+".meta"),["guid"]=AssetDatabase.AssetPathToGUID(p),["complete_channels_sha256"]=ArchitecturalCorrectionRegistry.CompleteChannelDigest(mesh)};}
        static Mesh Import(JToken part,string sourceKey,string dest,List<string> outputs)
        {
            string source=(string)part[sourceKey];Need(source!=null&&source.EndsWith(".asset",StringComparison.Ordinal)&&Hash(source)==AssetHash(part,sourceKey),"exact sealed mesh asset required");Need(!File.Exists(dest),"refuse overwrite of existing saved mesh");File.Copy(source,dest,false);outputs.Add(dest);AssetDatabase.ImportAsset(dest,ImportAssetOptions.ForceSynchronousImport);var m=AssetDatabase.LoadAssetAtPath<Mesh>(dest);Need(m!=null&&Hash(dest)==AssetHash(part,sourceKey)&&!EditorUtility.IsDirty(m),"exact imported reviewed mesh");Need(m.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z))&&m.subMeshCount>0&&Enumerable.Range(0,m.subMeshCount).All(s=>m.GetTopology(s)==MeshTopology.Triangles),"finite triangle architecture");return m;
        }
        static float[] Numbers(JToken token,int length,string label)
        {
            Need(token is JArray a&&a.Count==length,label+" component count");var values=token.Select(v=>(float)v).ToArray();Need(values.All(float.IsFinite),label+" finite components");return values;
        }
        // Fresh packet arrays are the immutable construction source, never a copy of a live scene mesh.
        public static Mesh ConstructPacketMesh(JToken part)
        {
            Need(part["vertices_world"] is JArray vertices&&vertices.Count>=4,"explicit fresh source-world vertices");Need(part["triangles"] is JArray triangles&&triangles.Count>=12&&triangles.Count%3==0,"explicit triangle index triples");
            var points=part["vertices_world"].Select(v=>Numbers(v,3,"vertex")).Select(v=>new Vector3(v[0],v[1],v[2])).ToArray();
            var indices=part["triangles"].Select(i=>{Need(i.Type==JTokenType.Integer,"integer triangle index");int n=(int)i;Need(n>=0&&n<points.Length,"triangle index within vertex array");return n;}).ToArray();
            for(int i=0;i<indices.Length;i+=3)Need(indices[i]!=indices[i+1]&&indices[i]!=indices[i+2]&&indices[i+1]!=indices[i+2]&&Vector3.Cross(points[indices[i+1]]-points[indices[i]],points[indices[i+2]]-points[indices[i]]).sqrMagnitude>1e-16f,"nondegenerate source triangle");
            var mesh=new Mesh{name=Safe((string)part["id"]),indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=points;mesh.triangles=indices;
            try
            {
                if(part["normals"] is JArray normals){Need(normals.Count==points.Length,"normal channel vertex count");mesh.normals=normals.Select(v=>Numbers(v,3,"normal")).Select(v=>new Vector3(v[0],v[1],v[2])).ToArray();}else mesh.RecalculateNormals();
                if(part["uv"] is JArray uv){Need(uv.Count==points.Length,"UV channel vertex count");mesh.uv=uv.Select(v=>Numbers(v,2,"UV")).Select(v=>new Vector2(v[0],v[1])).ToArray();}
                mesh.RecalculateBounds();return mesh;
            }
            catch{UnityEngine.Object.DestroyImmediate(mesh);throw;}
        }
        static Mesh CreatePartMesh(JToken part,string dest,List<string> outputs)
        {
            bool sealedAsset=part["mesh_asset"]!=null,freshArrays=part["vertices_world"]!=null;Need(sealedAsset!=freshArrays,"exactly one sealed mesh asset or fresh source arrays required");
            if(sealedAsset)return Import(part,"mesh_asset",dest,outputs);
            Need(!File.Exists(dest),"refuse overwrite of generated mesh");var mesh=ConstructPacketMesh(part);AssetDatabase.CreateAsset(mesh,dest);outputs.Add(dest);AssetDatabase.SaveAssets();Need(AssetDatabase.LoadAssetAtPath<Mesh>(dest)==mesh&&!EditorUtility.IsDirty(mesh),"persistent fresh packet mesh");return mesh;
        }
        static void ReplayMeshSource(JToken part,Mesh saved)
        {
            if(part["mesh_asset"]!=null){Need(Hash((string)part["mesh_asset"])==AssetHash(part,"mesh_asset")&&Hash(AssetDatabase.GetAssetPath(saved))==AssetHash(part,"mesh_asset"),"exact sealed original mesh source replay");return;}
            var fresh=ConstructPacketMesh(part);try{Need(ArchitecturalCorrectionRegistry.CompleteChannelDigest(fresh)==ArchitecturalCorrectionRegistry.CompleteChannelDigest(saved),"fresh immutable packet all-channel replay");}finally{UnityEngine.Object.DestroyImmediate(fresh);}
        }
        static void VerifySealedProofs(JObject packet)
        {
            foreach(var pin in packet["sealed_files"] as JArray??new JArray())Need(Hash((string)pin["path"])==(string)pin["sha256"],"sealed construction proof changed");
        }
        static void VerifySaved(JObject receipt)
        {
            ExistingFPSWorld();Rules();Need(Hash((string)receipt["packet"])==(string)receipt["packet_sha256"],"saved immutable construction packet changed");var sourcePacket=J((string)receipt["packet"]);VerifySealedProofs(sourcePacket);foreach(var row in receipt["parts"]){var matches=SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="FPSWorld").GetComponentsInChildren<BlueprintPartProvenance>(true).Where(p=>p.PartId==(string)row["id"]&&p.PacketSha256==(string)receipt["packet_sha256"]).ToArray();Need(matches.Length==1,"exact saved provenance owner absent/ambiguous");var p=matches[0];ValidateParent(p.transform);Need(PathOf(p.transform)==(string)row["owner"]&&Id(p.gameObject)==(long)row["gameobject_id"]&&Id(p)==(long)row["provenance_id"],"exact saved part owner/IDs");Need(JToken.DeepEquals(sourcePacket["parts"].Single(x=>(string)x["id"]==(string)row["id"]),row["source_spec"]),"saved part exactly matches immutable construction packet");SourcePart(row["source_spec"]);Need(p.transform.localToWorldMatrix==Matrix4x4.identity,"saved source-world part frame changed");var mf=p.GetComponent<MeshFilter>();var mr=p.GetComponent<MeshRenderer>();var pin=row["mesh"];Need(mf!=null&&mr!=null&&mr.enabled&&AssetDatabase.GetAssetPath(mf.sharedMesh)==(string)pin["path"]&&Hash((string)pin["path"])==(string)pin["sha256"]&&Hash((string)pin["path"]+".meta")== (string)pin["meta_sha256"]&&AssetDatabase.AssetPathToGUID((string)pin["path"])==(string)pin["guid"]&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(mf.sharedMesh)==(string)pin["complete_channels_sha256"],"exact saved all-channel part mesh");var spec=row["source_spec"];ReplayMeshSource(spec,mf.sharedMesh);string material=(string)spec["material_asset"];Need(mr.sharedMaterials.Length==1&&AssetDatabase.GetAssetPath(mr.sharedMaterial)==material&&Hash(material)==AssetHash(spec,"material_asset")&&mr.additionalVertexStreams==null&&p.gameObject.layer==((int?)spec["layer"]??0),"saved material/layer/render stream changed");var mc=p.GetComponent<MeshCollider>();Need((bool)row["collider"]?(mc!=null&&mc.enabled&&!mc.isTrigger&&!mc.convex&&(int)mc.cookingOptions==30&&mc.sharedMesh==mf.sharedMesh):mc==null,"exact part collider contract");Need(EditorJsonUtility.ToJson(p)==(string)row["provenance_json"],"source provenance changed");}
        }
        public static string Install(string[] args)
        {
            Need(args.Length==3,"Provide reviewed packet, exact approved SHA, and workdir");Need(Hash(args[0])==args[1],"exact root-reviewed packet hash");var packet=J(args[0]);Need((string)packet["schema"]=="chooguard.fpsworld-blueprint-construction.v1","reset-safe packet schema");Rules();var fps=ExistingFPSWorld();var scene=SceneManager.GetActiveScene();Need(!scene.isDirty&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"clean stable current scene");string id=Safe((string)packet["group_id"]),receiptPath="content/world/observed-interiors/fpsworld-blueprint-"+id+".json";var roots=Roots();
            if(File.Exists(receiptPath)){var existing=J(receiptPath);Need((string)existing["packet_sha256"]==args[1]&&Hash(scene.path)==(string)existing["scene_sha256"],"existing group only supports exact saved idempotent replay");VerifySaved(existing);AssertNoNewRoots(roots);return "IDEMPOTENT_EXACT_SAVED_BLUEPRINT_GROUP_VERIFIED";}
            Need(Hash(scene.path)==(string)packet["baseline_scene_sha256"],"exact reviewed reset/construction scene epoch");var parts=packet["parts"] as JArray;Need(parts!=null&&parts.Count>0&&parts.Select(p=>(string)p["id"]).Distinct().Count()==parts.Count,"nonempty unique coherent parts");foreach(var part in parts){SourcePart(part);ValidateNewFrameRegistration(part);}
            Need((packet["native_mesh_edits"] as JArray??new JArray()).Count==0,"native edits require their separate exact reviewed source/terminal transaction; never silently ignore edits");
            VerifySealedProofs(packet);
            Directory.CreateDirectory(args[2]);File.Copy(scene.path,args[2]+"/before.unity",false);string art=Art+"/"+id;Need(!Directory.Exists(art),"fresh artifact group required");Directory.CreateDirectory(art);var created=new List<GameObject>();var outputs=new List<string>();var savedParts=new JArray();
            try
            {
                var architecture=Child(fps,"Architecture",created);foreach(var part in parts){string pid=Safe((string)part["id"]),floor=(string)part["storey"];var parent=Child(Child(architecture,floor,created),"Blueprint_"+id,created);Need(!Enumerable.Range(0,parent.childCount).Select(parent.GetChild).Any(t=>t.name==pid),"refuse duplicate generated part");var mesh=CreatePartMesh(part,art+"/"+pid+".asset",outputs);string material=(string)part["material_asset"];Need(material!=null&&Hash(material)==AssetHash(part,"material_asset"),"explicit reviewed material pin");var mat=AssetDatabase.LoadAssetAtPath<Material>(material);Need(mat!=null,"material asset required");var go=new GameObject(pid);go.transform.SetParent(parent,false);created.Add(go);go.layer=(int?)part["layer"]??0;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;bool collision=(bool?)part["collider"]??true;if(collision){var mc=go.AddComponent<MeshCollider>();mc.cookingOptions=(MeshColliderCookingOptions)30;mc.sharedMesh=mesh;}var provenance=go.AddComponent<BlueprintPartProvenance>();provenance.PacketPath=args[0];provenance.PacketSha256=args[1];provenance.PartId=pid;provenance.Storey=floor;provenance.ArchitecturalRole=(string)part["role"];provenance.BlueprintPath=(string)part["source_blueprint"]["path"];provenance.BlueprintSha256=(string)part["source_blueprint"]["sha256"];provenance.SourceFeatureId=(string)part["feature_id"];provenance.SourceControlsPixels=part["source_controls_px"].ToString(Newtonsoft.Json.Formatting.None);provenance.ModelHeightBasis=HeightBasis(part);ValidateParent(go.transform);savedParts.Add(new JObject{["id"]=pid,["owner"]=PathOf(go.transform),["gameobject_id"]=Id(go),["provenance_id"]=Id(provenance),["provenance_json"]=EditorJsonUtility.ToJson(provenance),["source_spec"]=part.DeepClone(),["mesh"]=MeshPin(mesh),["collider"]=collision});}
                AssertNoNewRoots(roots);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Need(EditorSceneManager.SaveScene(scene),"save reconstructed geometry");
                // Newly saved scene IDs are authoritative; never retain temporary pre-save IDs.
                foreach(var row in savedParts){var p=fps.GetComponentsInChildren<BlueprintPartProvenance>(true).Single(p=>p.PartId==(string)row["id"]&&p.PacketSha256==args[1]);row["gameobject_id"]=Id(p.gameObject);row["provenance_id"]=Id(p);row["provenance_json"]=EditorJsonUtility.ToJson(p);}
                var receipt=new JObject{["schema"]="chooguard.fpsworld-blueprint-installed-group.v1",["packet"]=args[0],["packet_sha256"]=args[1],["authority"]=Authority,["authority_sha256"]=AuthoritySHA,["baseline_scene_sha256"]=packet["baseline_scene_sha256"],["scene_sha256"]=Hash(scene.path),["root_ids"]=new JArray(roots),["parts"]=savedParts,["no_new_top_roots"]=true,["whole_building_complete"]=false,["site_accuracy_verified"]=false};VerifySaved(receipt);AssertNoNewRoots(roots);File.WriteAllText(receiptPath,receipt.ToString());File.WriteAllText(args[2]+"/receipt.json",receipt.ToString());return "SAVED_BLUEPRINT_CHILD_GROUP parts="+parts.Count+" scene="+Hash(scene.path);
            }
            catch
            {
                // The backup is the current user reset epoch, never a pre-reset historical scene.
                foreach(var go in created.AsEnumerable().Reverse())if(go!=null)UnityEngine.Object.DestroyImmediate(go);foreach(string path in outputs)if(File.Exists(path))AssetDatabase.DeleteAsset(path);if(File.Exists(receiptPath))File.Delete(receiptPath);if(AssetDatabase.IsValidFolder(art))AssetDatabase.DeleteAsset(art);else if(Directory.Exists(art))Directory.Delete(art,true);File.Copy(args[2]+"/before.unity",scene.path,true);EditorSceneManager.OpenScene(scene.path,OpenSceneMode.Single);throw;
            }
        }
        public static string VerifyReopened(string[] args)
        {
            Need(args.Length==2,"Installed receipt/output");var receipt=J(args[0]);Need(!SceneManager.GetActiveScene().isDirty&&Hash(ScenePath)==(string)receipt["scene_sha256"],"exact saved construction scene");EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);VerifySaved(receipt);AssertNoNewRoots(receipt["root_ids"].Select(x=>(long)x).ToArray());Need(!SceneManager.GetActiveScene().isDirty&&Hash(ScenePath)==(string)receipt["scene_sha256"],"reopen verification changed scene");File.WriteAllText(args[1],new JObject{["status"]="PASS",["scene_sha256"]=receipt["scene_sha256"],["saved_scene_reopened"]=true,["exact_mesh_source_provenance_and_colliders"]=true,["part_count"]=receipt["parts"].Count(),["strict_FPSWorld_hierarchy"]=true,["no_new_top_roots"]=true}.ToString());return "REOPENED_BLUEPRINT_CHILD_GROUP_VERIFIED";
        }
        // Forward explicit user authority across several historical groups without rewriting their receipts.
        public static string VerifyFinalReopenedWithUserRemovalGroups(string[] args)
        {
            Need(args.Length==5,"Receipt-path JSON array, exact current saved SHA, multi-group user-removal override, override SHA, output");
            Need(Hash(args[2])==args[3],"exact multi-group user-removal override pin");var removal=J(args[2]);
            Need((string)removal["schema"]=="chooguard.explicit-current-user-removal-groups-override.v1"&&(string)removal["status"]=="CURRENT_USER_AUTHORITY"&&(string)removal["current_scene_sha256"]==args[1]&&(bool)removal["must_not_restore_removed_parts"],"exact current user authority");
            var paths=JArray.Parse(args[0]).Select(x=>(string)x).ToArray();Need(paths.Length>0&&paths.Distinct().Count()==paths.Length,"unique complete historical group list");
            var overrides=removal["historical_group_overrides"] as JArray;Need(overrides!=null&&overrides.Count>0,"explicit affected groups");
            Need(overrides.Select(g=>(string)g["historical_group_receipt"]).Distinct().Count()==overrides.Count,"one override per affected group");
            var receipts=paths.Select(J).ToArray();var removed=new List<string>();
            foreach(var group in overrides)
            {
                string path=(string)group["historical_group_receipt"];Need(paths.Count(p=>p==path)==1&&Hash(path)==(string)group["historical_group_receipt_sha256"],"affected immutable historical receipt pin");
                var affected=receipts[Array.IndexOf(paths,path)];var ids=group["removed_part_ids"].Select(x=>(string)x).ToArray();
                Need(ids.Length>0&&ids.Distinct().Count()==ids.Length&&group["removed_historical_parts"].Count()==ids.Length,"finite unique group removals");
                foreach(string id in ids){var old=affected["parts"].Where(p=>(string)p["id"]==id).ToArray();Need(old.Length==1&&JToken.DeepEquals(old[0],group["removed_historical_parts"].Single(p=>(string)p["id"]==id)),"exact historical removed source/owner/mesh/provenance");}
                affected["parts"]=new JArray(affected["parts"].Where(p=>!ids.Contains((string)p["id"])).Select(p=>p.DeepClone()));
                Need(affected["parts"].Count()==(int)group["current_group_part_count"],"explicit current group count");removed.AddRange(ids);
            }
            Need(removed.Distinct().Count()==removed.Count&&removed.OrderBy(x=>x).SequenceEqual(removal["removed_part_ids"].Select(x=>(string)x).OrderBy(x=>x)),"exact union of user removed IDs");
            Need(receipts.Sum(r=>r["parts"].Count())==(int)removal["current_all_added_part_count"],"explicit current total count");
            Need(!SceneManager.GetActiveScene().isDirty&&Hash(ScenePath)==args[1],"exact clean current scene; never restore old epoch");
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var current=ExistingFPSWorld().GetComponentsInChildren<BlueprintPartProvenance>(true);
            Need(!current.Any(p=>removed.Contains(p.PartId)),"all user removed legacy parts remain absent");
            var expected=receipts.SelectMany(r=>r["parts"]).Select(p=>(string)p["id"]).OrderBy(x=>x).ToArray();Need(expected.Distinct().Count()==expected.Length&&current.Select(p=>p.PartId).OrderBy(x=>x).SequenceEqual(expected),"exact complete retained part set");
            foreach(var receipt in receipts){VerifySaved(receipt);AssertNoNewRoots(receipt["root_ids"].Select(x=>(long)x).ToArray());}
            Need(!SceneManager.GetActiveScene().isDirty&&Hash(ScenePath)==args[1],"reopened user-authority verification changed scene");
            File.WriteAllText(args[4],new JObject{["status"]="PASS",["current_scene_sha256"]=args[1],["saved_scene_reopened"]=true,["historical_group_receipts_preserved"]=true,["explicit_user_removal_override"]=args[2],["override_sha256"]=args[3],["removed_parts_still_absent"]=new JArray(removed.OrderBy(x=>x)),["affected_group_count"]=overrides.Count,["part_count"]=expected.Length,["exact_mesh_source_provenance_and_colliders"]=true,["strict_FPSWorld_hierarchy"]=true,["no_new_top_roots"]=true,["source_registration_qualified"]=false,["whole_building_complete"]=false}.ToString());return "REOPENED_CURRENT_USER_MULTI_GROUP_AUTHORITY_VERIFIED parts="+expected.Length;
        }
        public static string VerifyFinalReopenedWithUserRemovals(string[] args)
        {
            Need(args.Length==5,"Receipt-path JSON array, exact current saved SHA, explicit user-removal override, override SHA, output");
            Need(Hash(args[2])==args[3],"exact explicit user-removal override pin");var removal=J(args[2]);
            Need((string)removal["schema"]=="chooguard.explicit-current-user-removal-override.v1"&&(string)removal["status"]=="CURRENT_USER_AUTHORITY"&&(string)removal["current_scene_sha256"]==args[1]&&(bool)removal["must_not_restore_removed_parts"],"current user authority and exact saved epoch");
            string changedGroup=(string)removal["historical_group_receipt"];Need(Hash(changedGroup)==(string)removal["historical_group_receipt_sha256"],"historical receipt preserved byte-exact");
            var paths=JArray.Parse(args[0]).Select(x=>(string)x).ToArray();Need(paths.Length>0&&paths.Distinct().Count()==paths.Length&&paths.Count(p=>p==changedGroup)==1,"unique complete group list with explicit affected group");
            var removed=removal["removed_part_ids"].Select(x=>(string)x).ToArray();Need(removed.Length>0&&removed.Distinct().Count()==removed.Length,"finite unique user removals");
            Need(!SceneManager.GetActiveScene().isDirty&&Hash(ScenePath)==args[1],"exact current clean saved scene; never restore historical scene");
            var receipts=paths.Select(J).ToArray();var affected=receipts[Array.IndexOf(paths,changedGroup)];
            foreach(string id in removed){var historical=affected["parts"].Where(p=>(string)p["id"]==id).ToArray();Need(historical.Length==1&&JToken.DeepEquals(historical[0],removal["removed_historical_parts"].Single(p=>(string)p["id"]==id)),"exact historical removed part identity/source/mesh row");}
            // Filter only an in-memory verification copy. Immutable original receipts/packets are never edited.
            affected["parts"]=new JArray(affected["parts"].Where(p=>!removed.Contains((string)p["id"])).Select(p=>p.DeepClone()));
            Need(affected["parts"].Count()==(int)removal["current_group_part_count"]&&receipts.Sum(r=>r["parts"].Count())==(int)removal["current_all_added_part_count"],"explicit current retained counts");
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var current=ExistingFPSWorld().GetComponentsInChildren<BlueprintPartProvenance>(true);
            Need(!current.Any(p=>removed.Contains(p.PartId)),"user removed parts must remain absent");
            var expected=receipts.SelectMany(r=>r["parts"]).Select(p=>(string)p["id"]).OrderBy(x=>x).ToArray();Need(current.Select(p=>p.PartId).OrderBy(x=>x).SequenceEqual(expected),"exact all-current part set including user removals");
            foreach(var receipt in receipts){VerifySaved(receipt);AssertNoNewRoots(receipt["root_ids"].Select(x=>(long)x).ToArray());}
            Need(!SceneManager.GetActiveScene().isDirty&&Hash(ScenePath)==args[1],"user-authority final verification changed scene");
            File.WriteAllText(args[4],new JObject{["status"]="PASS",["current_scene_sha256"]=args[1],["saved_scene_reopened"]=true,["historical_group_receipts_preserved"]=true,["explicit_user_removal_override"]=args[2],["override_sha256"]=args[3],["removed_parts_still_absent"]=new JArray(removed),["part_count"]=expected.Length,["exact_mesh_source_provenance_and_colliders"]=true,["strict_FPSWorld_hierarchy"]=true,["no_new_top_roots"]=true,["whole_building_complete"]=false}.ToString());return "REOPENED_CURRENT_USER_AUTHORITY_ALL_PARTS_VERIFIED parts="+expected.Length;
        }
        public static string VerifyFinalReopened(string[] args)
        {
            Need(args.Length==3,"Receipt-path JSON array, explicit final saved scene SHA, output");var paths=JArray.Parse(args[0]).Select(x=>(string)x).ToArray();Need(paths.Length>0&&paths.Distinct().Count()==paths.Length,"unique installed group receipts required");
            Need(!SceneManager.GetActiveScene().isDirty&&Hash(ScenePath)==args[1],"explicit exact final saved construction scene");var receipts=paths.Select(J).ToArray();EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            foreach(var receipt in receipts){VerifySaved(receipt);AssertNoNewRoots(receipt["root_ids"].Select(x=>(long)x).ToArray());}
            Need(!SceneManager.GetActiveScene().isDirty&&Hash(ScenePath)==args[1],"final all-group verification changed scene");
            File.WriteAllText(args[2],new JObject{["status"]="PASS",["final_scene_sha256"]=args[1],["saved_scene_reopened"]=true,["group_receipts"]=new JArray(paths),["historical_group_scene_hashes_preserved"]=true,["exact_mesh_source_provenance_and_colliders"]=true,["part_count"]=receipts.Sum(r=>r["parts"].Count()),["strict_FPSWorld_hierarchy"]=true,["no_new_top_roots"]=true}.ToString());return "REOPENED_FINAL_ALL_BLUEPRINT_GROUPS_VERIFIED";
        }
    }
}
#endif
