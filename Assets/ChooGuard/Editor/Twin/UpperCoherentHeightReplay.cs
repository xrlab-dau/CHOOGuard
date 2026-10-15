// Promote to Assets/ChooGuard/Editor/Twin only after coupled candidate physical acceptance.
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.Circulation;

namespace ChooGuard.Editor.Twin
{
    public static class UpperCoherentHeightReplay
    {
        public const string Registry="content/world/observed-interiors/public3f-coherent-height.json";
        static void Need(bool c,string s){if(!c)throw new InvalidDataException(s);}
        static Vector3 V(JToken p)=>new Vector3((float)p[0],(float)p[1],(float)p[2]);
        static string P(Transform t)=>t.parent==null?t.name:P(t.parent)+"/"+t.name;
        public static string PathOf(Transform t)=>P(t);
        static JObject Read()
        {
            if(!File.Exists(Registry))return null;var j=JObject.Parse(File.ReadAllText(Registry));
            Need((string)j["schema"]=="chooguard.public3f-coherent-height-replay.v1","Bad upper replay registry");
            Need((string)j["source_candidate"]=="workers/twin/output/upper-coherent-height-20261009-pass3/coherent-candidate.json"&&(string)j["source_candidate_sha256"]=="874d98f7e97cd163f7e225006b0c422c89bf4dc53236be697ca8f988728c8708","Wrong upper replay candidate pin");
            Need(Hash((string)j["source_candidate"])==(string)j["source_candidate_sha256"]&&Hash((string)j["source_manifest"])==(string)j["source_manifest_sha256"],"Upper replay source files changed");
            Need(j["owners"].Count()==146&&j["owners"].Select(r=>(string)r["original_transform_global_id"]).Distinct().Count()==146&&j["roots"].Count()==6&&j["individual_translations"].Count()==11,"Upper replay owner partition changed");PartitionProof(j);return j;
        }
        static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
        static void PartitionProof(JObject j)
        {
            var candidate=JObject.Parse(File.ReadAllText((string)j["source_candidate"]));float delta=(float)candidate["datum"]["delta"];
            Need(Mathf.Abs((float)j["source_floor_target_y"]-(float)candidate["datum"]["source_target_floor_y"])<.000002f&&Mathf.Abs((float)j["rise"]-((float)j["source_floor_target_y"]-7))<.000002f,"Upper replay datum changed");
            foreach(var g in candidate["assembly_translation_groups"])
            {
                var matches=j["roots"].Where(r=>(string)r["path"]==(string)g["path"]).ToArray();Need(matches.Length==1,"Upper replay root identity changed");var row=matches[0];
                Need((V(row["before_world_position"])-V(g["baseline_local_position"])).sqrMagnitude<1e-9f&&(V(row["after_world_position"])-V(row["before_world_position"])-Vector3.up*delta).sqrMagnitude<1e-9f,"Upper replay root translation changed");
            }
            Need(Hash("workers/twin/output/vworld-upper-native-correction-20261009/upper-native-correction-draft.json")=="b9e7b4fceb907b7e59ed55f68b233a7cca78d777c79ac9bade72128dd6ad8b6f","Upper replay topology source changed");
            var topology=JObject.Parse(File.ReadAllText("workers/twin/output/vworld-upper-native-correction-20261009/upper-native-correction-draft.json"));
            foreach(var owner in candidate["mesh_owner_partition"])
            {
                var matches=j["owners"].Where(r=>(string)r["path"]==(string)owner["owner"]&&(string)r["original_transform_global_id"]==(string)owner["transform_global_id"]).ToArray();Need(matches.Length==1,"Upper replay mesh owner identity changed");var row=matches[0];
                var digests=new JArray(owner["baseline_unity_digests"]["complete_channels_sha256"].DeepClone());var correction=topology["recipes"].SingleOrDefault(r=>(string)r["owner_path"]==(string)owner["owner"]);if(correction!=null)digests.Add(correction["replacement_unity_digests"]["complete_channels_sha256"].DeepClone());
                Need((string)row["assembly"]==(string)owner["assembly"]&&(string)row["source_asset"]==(string)owner["baseline_entry"]["asset"]&&(string)row["source_guid"]==(string)owner["baseline_entry"]["guid"]&&JToken.DeepEquals(row["allowed_digests"],digests),"Upper replay ownership/digests changed");
                if((string)owner["assembly"]==(string)owner["owner"])
                {
                    var p=j["individual_translations"].Where(r=>(string)r["path"]==(string)owner["owner"]).ToArray();Need(p.Length==1,"Upper individual translation identity changed");var m=owner["baseline_owner"]["local_to_world"];var before=new Vector3((float)m[0][3],(float)m[1][3],(float)m[2][3]);
                    Need((V(p[0]["before_world_position"])-before).sqrMagnitude<1e-8f&&(V(p[0]["after_world_position"])-before-Vector3.up*delta).sqrMagnitude<1e-8f&&JToken.DeepEquals(p[0]["allowed_digests"],digests),"Upper individual translation/digests changed");
                }
            }
            Need(j["persistent_meshes"].Count()==5&&j["escalators"].Count()==2,"Upper derived architecture set changed");
            foreach(var e in candidate["escalators"])
            {
                var matches=j["escalators"].Where(r=>(string)r["path"]==(string)e["baseline_owner"]["owner_path"]).ToArray();Need(matches.Length==1&&(string)matches[0]["complete_digest"]==(string)e["replacement_unity_digests"]["complete_channels_sha256"],"Named architectural incline derivative changed");
            }
        }
        static bool Scoped(JObject j,string rootPath)=>j["roots"].Any(r=>(string)r["path"]==rootPath)||j["individual_translations"].Any(r=>((string)r["path"]).StartsWith(rootPath+"/",StringComparison.Ordinal))||j["escalators"].Any(r=>((string)r["path"]).StartsWith(rootPath+"/",StringComparison.Ordinal));
        static Transform Relative(Transform root,string path,string rootPath,JToken identity=null)
        {
            Need(path.StartsWith(rootPath+"/",StringComparison.Ordinal),"Replay owner outside requested root");
            var candidates=root.GetComponentsInChildren<Transform>(true).Where(t=>P(t)==path);
            if(identity!=null)candidates=candidates.Where(t=>{var m=t.GetComponent<MeshFilter>()?.sharedMesh??t.GetComponent<MeshCollider>()?.sharedMesh;return m!=null&&identity["allowed_digests"].Any(d=>(string)d==ArchitecturalCorrectionRegistry.CompleteChannelDigest(m));});
            var matches=candidates.ToArray();Need(matches.Length==1,"Missing or ambiguous rebuilt upper companion: "+path);return matches[0];
        }
        static void SourceProof(JObject j)
        {
            Need((string)j["schema"]=="chooguard.public3f-coherent-height-replay.v1","Bad upper replay registry");
            UpperRetiredSurfaceCollisionGuard.ValidateSourceAssembly();
            var floor=j["source_floor_proof"] as JArray;Need(floor!=null&&floor.Count==2,"Accepted public floor proof absent");
            foreach(var q in floor)
            {
                var go=GameObject.Find("/"+(string)q["path"]);Need(go!=null,"Accepted public floor missing");var f=go.GetComponent<MeshFilter>();var c=go.GetComponent<MeshCollider>();var m=f!=null?f.sharedMesh:null;
                Need(m!=null&&c!=null&&c.enabled&&!c.isTrigger&&c.sharedMesh==m&&AssetDatabase.GetAssetPath(m)==(string)q["asset"]&&AssetDatabase.AssetPathToGUID((string)q["asset"])==(string)q["guid"]&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(m)==(string)q["complete_digest"],"Accepted public floor proof changed");
                for(int r=0;r<4;r++)for(int k=0;k<4;k++)Need(Mathf.Abs(go.transform.localToWorldMatrix[r,k]-(float)q["world_matrix"][r][k])<.00002f,"Accepted public floor frame changed");
            }
        }
        // All three inferred support slabs remain explicit architectural dependencies.
        static void PrivateSupportProof(JObject j)
        {
            var candidate=JObject.Parse(File.ReadAllText((string)j["source_candidate"]));int i=2;
            foreach(var q in candidate["additional_private_support"])
            {
                string path="공공3층 점포 바닥 지지_추정_"+((string)j["source_candidate_sha256"]).Substring(0,12)+"/"+(string)q["label"]+" [inferred]";
                var go=GameObject.Find("/"+path);Need(go!=null,"Inferred architectural support missing");var f=go.GetComponent<MeshFilter>();var c=go.GetComponent<MeshCollider>();
                string suffix="/support_"+(i++)+".asset";var pin=j["persistent_meshes"].Single(p=>((string)p["asset"]).EndsWith(suffix,StringComparison.Ordinal));var m=f?.sharedMesh;
                Need(m!=null&&AssetDatabase.GetAssetPath(m)==(string)pin["asset"]&&AssetDatabase.AssetPathToGUID((string)pin["asset"])==(string)pin["guid"]&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(m)==(string)pin["complete_digest"]&&c!=null&&c.enabled&&!c.isTrigger&&!c.convex&&c.sharedMesh==m&&go.activeInHierarchy,"Inferred architectural support binding changed");
                for(int a=0;a<4;a++)for(int b=0;b<4;b++)Need(Mathf.Abs(go.transform.localToWorldMatrix[a,b]-(a==b?1f:0f))<.00002f,"Inferred architectural support frame changed");
                Need(m.vertices.All(p=>Mathf.Abs(p.y-(float)j["source_floor_target_y"])<.00002f),"Inferred support height changed");
            }
        }
        // Scope is exactly the two accepted named 2F-3F installations. Other survey lanes keep their authored datum.
        public static bool TryGetEscalatorUpper(Transform t,out float upper)
        {
            upper=0;var j=Read();if(j==null)return false;var q=j["escalators"].SingleOrDefault(x=>(string)x["path"]==P(t));if(q==null)return false;SourceProof(j);
            var f=t.GetComponent<MeshFilter>();var m=f!=null?f.sharedMesh:null;
            Need(m!=null&&AssetDatabase.GetAssetPath(m)==(string)q["asset"]&&AssetDatabase.AssetPathToGUID((string)q["asset"])==(string)q["guid"]&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(m)==(string)q["complete_digest"],"Accepted named escalator derivative changed");
            for(int r=0;r<4;r++)for(int k=0;k<4;k++)Need(Mathf.Abs(t.localToWorldMatrix[r,k]-(float)q["world_matrix"][r][k])<.00002f,"Accepted named escalator frame changed");
            var v=m.vertices;Need(v.Length==82*24,"Named escalator box topology changed");upper=v.Skip(70*24).Take(24).Max(p=>t.TransformPoint(p).y);
            Need(Mathf.Abs(upper-(float)j["source_floor_target_y"])<.002f,"Named upper landing is not flush with accepted public floor");return true;
        }
        public static bool TryGetWingElevatorRise(StationDoor lower,StationDoor upper,out float rise)
        {
            rise=5.2f;var j=Read();if(j==null)return false;SourceProof(j);var stops=j["wing_elevator_landings"] as JArray;Need(stops!=null&&stops.Count==2,"Accepted wing elevator proof missing");
            var doors=new[]{lower,upper};for(int i=0;i<2;i++)
            {
                var d=doors[i];var q=stops[i];Need(d!=null&&d.Element==(string)q["element"]&&d.CarId=="맞이방 날개 승강기"&&P(d.transform)==(string)q["path"]&&d.Cabin!=null&&P(d.Cabin)==(string)q["cabin_path"],"Accepted wing landing identity changed");
                Need(Mathf.Abs(d.transform.TransformPoint(d.Centre).y-(float)q["floor_y"])<.002f&&(d.Cabin.position-V(q["cabin_world_position"])).sqrMagnitude<.000004f,"Accepted wing landing or cabin frame changed");
            }
            rise=(float)j["rise"];Need(Mathf.Abs(rise-((float)j["source_floor_target_y"]-7))<.00002f&&Mathf.Abs(upper.Cabin.position.y-lower.Cabin.position.y-rise)<.002f,"Wing elevator rise differs from accepted source datum");return true;
        }
        // Called at entry before touching a shared source asset/root. A modified source cannot silently reset the level.
        public static void BeforeRebuild(string rootPath)
        {
            var j=Read();if(j==null||!Scoped(j,rootPath))return;SourceProof(j);PrivateSupportProof(j);
            foreach(var q in j["persistent_meshes"]){string asset=(string)q["asset"];var m=AssetDatabase.LoadAssetAtPath<Mesh>(asset);Need(m!=null&&AssetDatabase.AssetPathToGUID(asset)==(string)q["guid"]&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(m)==(string)q["complete_digest"],"Saved coherent upper mesh changed: "+asset);}
            foreach(var row in j["roots"].Where(r=>(string)r["path"]==rootPath)){var go=GameObject.Find("/"+rootPath);Need(go!=null&&(go.transform.position-V(row["after_world_position"])).sqrMagnitude<1e-9f,"Installed coherent upper root moved");}
        }
        // Call before the old KitBuild root is destroyed, passing the actual newly constructed root (avoids duplicate-path lookup).
        public static void AfterRebuild(Transform built,string rootPath)
        {
            var j=Read();if(j==null||!Scoped(j,rootPath))return;Need(built!=null&&P(built)==rootPath,"Wrong upper rebuild root");SourceProof(j);PrivateSupportProof(j);
            var group=j["roots"].SingleOrDefault(r=>(string)r["path"]==rootPath);
            if(group!=null)
            {
                var before=V(group["before_world_position"]);var after=V(group["after_world_position"]);
                Need((built.position-before).sqrMagnitude<1e-9f||(built.position-after).sqrMagnitude<1e-9f,"Unexpected rebuilt upper parent position");
                // Validate actual generated/saved upper meshes before changing their world datum.
                foreach(var row in j["owners"].Where(r=>(string)r["assembly"]==rootPath))
                {
                    var t=Relative(built,(string)row["path"],rootPath,row);var m=t.GetComponent<MeshFilter>()?.sharedMesh??t.GetComponent<MeshCollider>()?.sharedMesh;
                    Need(m!=null&&((JArray)row["allowed_digests"]).Any(d=>(string)d==ArchitecturalCorrectionRegistry.CompleteChannelDigest(m)),"Rebuilt upper source changed: "+P(t));
                }
                built.position=after;
            }
            foreach(var row in j["individual_translations"].Where(r=>((string)r["path"]).StartsWith(rootPath+"/",StringComparison.Ordinal)))
            {
                var t=Relative(built,(string)row["path"],rootPath);var before=V(row["before_world_position"]);var after=V(row["after_world_position"]);
                Need((t.position-before).sqrMagnitude<1e-8f||(t.position-after).sqrMagnitude<1e-8f,"Rebuilt native upper pose changed");var m=t.GetComponent<MeshFilter>()?.sharedMesh??t.GetComponent<MeshCollider>()?.sharedMesh;
                Need(m!=null&&((JArray)row["allowed_digests"]).Any(d=>(string)d==ArchitecturalCorrectionRegistry.CompleteChannelDigest(m)),"Rebuilt native upper source changed");t.position=after;
            }
            foreach(var row in j["extra_finish_offsets"].Where(r=>((string)r["path"]).StartsWith(rootPath+"/",StringComparison.Ordinal)))
            {var t=Relative(built,(string)row["path"],rootPath);t.localPosition=V(row["after_local_position"]);}
            foreach(var row in j["escalators"].Where(r=>((string)r["path"]).StartsWith(rootPath+"/",StringComparison.Ordinal)))
            {
                var t=Relative(built,(string)row["path"],rootPath);var m=AssetDatabase.LoadAssetAtPath<Mesh>((string)row["asset"]);Need(m!=null&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(m)==(string)row["complete_digest"],"Saved upper escalator derivative changed");t.GetComponent<MeshFilter>().sharedMesh=m;t.GetComponent<MeshCollider>().sharedMesh=m;
                float rise=(float)j["rise"],run=5.2f/Mathf.Tan(30*Mathf.Deg2Rad);var incline=t.Find("RideVolume_Incline");Need(incline!=null,"Rebuilt escalator drive volume missing");incline.localPosition=new Vector3(0,rise*.5f+1,1.2f+run*.5f);incline.localRotation=Quaternion.Euler(-Mathf.Atan2(rise,run)*Mathf.Rad2Deg,0,0);var box=incline.GetComponent<BoxCollider>();box.size=new Vector3(box.size.x,2,Mathf.Sqrt(run*run+rise*rise));var lower=t.Find("RideVolume_Landing");Need(lower!=null,"Rebuilt lower landing ride missing");var upper=t.Find("RideVolume_UpperLanding");if(upper==null)upper=UnityEngine.Object.Instantiate(lower.gameObject,t).transform;upper.name="RideVolume_UpperLanding";upper.localPosition=new Vector3(0,rise+1,1.2f+run+.6f);upper.localRotation=Quaternion.identity;
            }
            UpperRetiredSurfaceCollisionGuard.VerifyInstalled(built,rootPath);
            foreach(var motion in built.GetComponentsInChildren<EscalatorMotion>(true))motion.RebuildBindings();
            Physics.SyncTransforms();
        }
    }
}
