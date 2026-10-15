#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Shell;
using Object=UnityEngine.Object;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class UpperCoherentPhysicalContacts : MonoBehaviour
    {
        public struct Contact { public Collider Collider; public Vector3 Point,Normal; }
        public readonly List<Contact> Hits=new List<Contact>();
        void OnControllerColliderHit(ControllerColliderHit h)=>Hits.Add(new Contact{Collider=h.collider,Point=h.point,Normal=h.normal});
    }

    // Architecture clearance only. One initial placement; all fixed-stair traversal uses the real saved CC.
    public sealed class UpperCoherentStructuralPlayTests
    {
        const string ConfigPath=".planning/2026-10-09-interior-99-reconstruction/public-upper-mezzanine/coherent-structure-runtime-config.json";
        const string ScenePath="Assets/ChooGuard/Scenes/FpsStation.unity";
        const string WaypointSha="8eb661ec5cd923d492af21d7e19a093e74a9e655fed302966fd2b0723681ccb7";
        const float Dt=.02f,WalkSpeed=1.2f,Gravity=-9.81f,Reach=.05f,StepSupportBand=.30f;
        JObject config,result;
        FirstPersonResponder player;
        CharacterController cc;
        UpperCoherentPhysicalContacts recorder;
        Vector3 savedPosition;float savedYaw,savedPitch,vertical;
        bool savedExternal,savedPause,playerSaved,timeSaved;
        int savedCapture;float savedScale,savedFixed;
        CursorLockMode savedCursor;bool savedCursorVisible;
        readonly Dictionary<int,JObject> descriptions=new Dictionary<int,JObject>();
        readonly Dictionary<string,MeshCollider> pinned=new Dictionary<string,MeshCollider>();

        static string Sha(string p){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
        static string Sha(byte[] b){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
        static Vector3 V(JToken a)=>new Vector3((float)a[0],(float)a[1],(float)a[2]);
        static JArray P(Vector3 v)=>new JArray(v.x,v.y,v.z);
        static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
        static float XZ(Vector3 a,Vector3 b){var d=a-b;d.y=0;return d.magnitude;}
        static float Lateral(Vector3 p,Vector3 a,Vector3 b){var d=p-a;d.y=0;var axis=b-a;axis.y=0;return axis.sqrMagnitude<1e-8f?d.magnitude:(d-Vector3.Project(d,axis)).magnitude;}
        bool Self(Collider c)=>c==cc||c.transform.IsChildOf(cc.transform);
        void Need(bool c,string why)
        {
            if(!c&&result!=null)
            {
                result["failure"]=why;
                if(config!=null)
                {
                    string dir=(string)config["output_directory"];Directory.CreateDirectory(dir);
                    var leg=(result["legs"] as JArray)?.LastOrDefault();
                    var segment=(leg?["segments"] as JArray)?.LastOrDefault();
                    var frame=(segment?["frames"] as JArray)?.LastOrDefault();
                    File.WriteAllText(Path.Combine(dir,"physical-group-failure.json"),new JObject{["scene_sha256"]=Sha(ScenePath),["failure"]=why,["last_sample"]=frame?.DeepClone(),["last_segment_index"]=segment?["index"]?.DeepClone(),["last_segment_target"]=segment?["target"]?.DeepClone(),["physical_group"]=result.DeepClone()}.ToString());
                }
            }
            Assert.That(c,Is.True,why);
        }
        static JObject Settings(CharacterController c)=>new JObject{["radius"]=c.radius,["height"]=c.height,["center"]=P(c.center),["skin_width"]=c.skinWidth,["step_offset"]=c.stepOffset,["slope_limit"]=c.slopeLimit,["min_move_distance"]=c.minMoveDistance,["scale"]=P(c.transform.lossyScale),["enabled"]=c.enabled};
        static bool Stock(CharacterController c)=>c!=null&&c.enabled&&Mathf.Abs(c.radius-.28f)<.0001f&&Mathf.Abs(c.height-1.72f)<.0001f&&Mathf.Abs(c.skinWidth-.025f)<.0001f&&Mathf.Abs(c.stepOffset-.28f)<.0001f&&Mathf.Abs(c.slopeLimit-45)<.0001f&&Mathf.Abs(c.minMoveDistance)<.0001f&&Vector3.Distance(c.center,Vector3.up*.86f)<.0001f&&Vector3.Distance(c.transform.lossyScale,Vector3.one)<.0001f;
        public static byte[] MeshChannels(Mesh m)
        {
            using(var ms=new MemoryStream()){using(var w=new BinaryWriter(ms,System.Text.Encoding.UTF8,true))
            {w.Write(1);var v=m.vertices;w.Write(v.Length);foreach(var p in v){w.Write(p.x);w.Write(p.y);w.Write(p.z);}var n=m.normals;w.Write(n.Length);foreach(var p in n){w.Write(p.x);w.Write(p.y);w.Write(p.z);}var t=m.tangents;w.Write(t.Length);foreach(var p in t){w.Write(p.x);w.Write(p.y);w.Write(p.z);w.Write(p.w);}var co=m.colors;w.Write(co.Length);foreach(var p in co){w.Write(p.r);w.Write(p.g);w.Write(p.b);w.Write(p.a);}for(int u=0;u<8;u++){var uv=new List<Vector4>();m.GetUVs(u,uv);w.Write(uv.Count);w.Write(uv.Count==0?0:m.GetVertexAttributeDimension((UnityEngine.Rendering.VertexAttribute)((int)UnityEngine.Rendering.VertexAttribute.TexCoord0+u)));foreach(var p in uv){w.Write(p.x);w.Write(p.y);w.Write(p.z);w.Write(p.w);}}w.Write(m.subMeshCount);for(int i=0;i<m.subMeshCount;i++){w.Write((int)m.GetTopology(i));var ix=m.GetIndices(i);w.Write(ix.Length);foreach(int q in ix)w.Write(q);}}return ms.ToArray();}
        }
        JObject Describe(Collider c)
        {
            if(c==null)return null;if(descriptions.TryGetValue(c.GetInstanceID(),out var q))return (JObject)q.DeepClone();
            var m=(c as MeshCollider)?.sharedMesh;string path=m!=null?AssetDatabase.GetAssetPath(m):null;
            q=new JObject{["owner_path"]=PathOf(c.transform),["collider_id"]=GlobalObjectId.GetGlobalObjectIdSlow(c).ToString(),["type"]=c.GetType().Name,["mesh_asset"]=path,["guid"]=string.IsNullOrEmpty(path)?null:AssetDatabase.AssetPathToGUID(path),["asset_sha256"]=!string.IsNullOrEmpty(path)&&File.Exists(path)?Sha(path):null};descriptions[c.GetInstanceID()]=q;return (JObject)q.DeepClone();
        }
        JObject Hit(RaycastHit h){var j=Describe(h.collider);j["point"]=P(h.point);j["normal"]=P(h.normal);j["distance"]=h.distance;j["triangle_index"]=h.triangleIndex;return j;}
        // Pinned original CGVM + reviewed34-waypoint geometry, not a larger global foot band.
        // max rise0.2046575213m; minimum tread depth0.2637834m; ring radius0.2576m.
        const float SourceMaxRiser=.20465753f,SourceMinTreadDepth=.2637834f,SourceSurfaceTolerance=.002f;
        bool PinnedSourceStair(Collider c)
        {
            if(c==null||c.transform.parent==null||c.transform.parent.name!="3MV0100002__b00410046259400327764142600")return false;
            return pinned.TryGetValue(PathOf(c.transform),out var saved)&&saved==c&&c.name.EndsWith("__fixed_stairs",StringComparison.Ordinal);
        }
        bool AdjacentSourceStairRing(RaycastHit centre,RaycastHit ring)
        {
            if(!PinnedSourceStair(centre.collider)||centre.normal.y<=.9f||ring.collider==null||ring.normal.y<=.9f)return false;
            if(!cc.isGrounded||cc.radius*.92f>=SourceMinTreadDepth||cc.stepOffset<SourceMaxRiser)return false;
            if(!recorder.Hits.Any(h=>PinnedSourceStair(h.Collider)&&h.Normal.y>.2f))return false;
            if(Mathf.Abs(player.transform.position.y-centre.point.y)>StepSupportBand)return false;
            if(Mathf.Abs(centre.point.y-ring.point.y)>SourceMaxRiser+SourceSurfaceTolerance)return false;
            if(PinnedSourceStair(ring.collider))return true;
            // Only the source first-tread/lower-floor junction may use a non-stair neighbour.
            // Other floor owners, missing rays, flatfloor holes and arbitrary lower slabs still fail.
            return PathOf(ring.collider.transform)=="맞이방 · 원본 정합/Majibang_2F_Floor"
                &&Mathf.Abs(centre.point.y-7.20458357f)<=SourceSurfaceTolerance
                &&Mathf.Abs(ring.point.y-7f)<=SourceSurfaceTolerance;
        }
        bool FeetProof(out JArray rays,out RaycastHit centre)
        {
            rays=new JArray();centre=default;bool good=true;var feet=player.transform.position;
            for(int i=-1;i<8;i++)
            {
                var offset=i<0?Vector3.zero:new Vector3(Mathf.Cos(i*Mathf.PI/4),0,Mathf.Sin(i*Mathf.PI/4))*cc.radius*.92f;
                // Ring probes must reach the already permitted one-riser neighbour. Acceptance still uses the strict source envelope.
                float probeLength=i<0?.75f:.36f+StepSupportBand+SourceMaxRiser+SourceSurfaceTolerance;
                var h=Physics.RaycastAll(feet+offset+Vector3.up*.36f,Vector3.down,probeLength,~0,QueryTriggerInteraction.Ignore).Where(x=>!Self(x.collider)).OrderBy(x=>x.distance).ToArray();
                bool originalBand=h.Length>0&&h[0].normal.y>.9f&&Mathf.Abs(feet.y-h[0].point.y)<=StepSupportBand;
                bool sourceAdjacent=i>=0&&!originalBand&&h.Length>0&&AdjacentSourceStairRing(centre,h[0]);
                // A ring ray exactly on an acquired tread edge can meet its nearly
                // vertical riser. Require real capsule support and a pinned centre
                // tread; this never accepts a missing ray or a different owner.
                bool sourceRiserBoundary=i>=0&&!originalBand&&!sourceAdjacent&&h.Length>0
                    &&PinnedSourceStair(centre.collider)&&centre.normal.y>.9f
                    &&Mathf.Abs(feet.y-centre.point.y)<=StepSupportBand&&cc.isGrounded
                    &&PinnedSourceStair(h[0].collider)&&Mathf.Abs(h[0].normal.y)<.002f
                    &&Mathf.Abs(h[0].point.y-centre.point.y)<=SourceMaxRiser+SourceSurfaceTolerance
                    &&cc.radius*.92f<SourceMinTreadDepth
                    &&recorder.Hits.Any(hit=>PinnedSourceStair(hit.Collider)&&hit.Normal.y>.2f);
                bool ok=originalBand||sourceAdjacent||sourceRiserBoundary;
                good&=ok;if(i<0&&h.Length>0)centre=h[0];
                rays.Add(new JObject{["ray"]=i<0?"centre":"ring_"+i,["offset"]=P(offset),["passed"]=ok,["nearest"]=h.Length>0?Hit(h[0]):null,["feet_minus_surface_y"]=h.Length>0?(JToken)(feet.y-h[0].point.y):JValue.CreateNull(),["original_absolute_band_passed"]=originalBand,["source_adjacent_tread_envelope_passed"]=sourceAdjacent,["source_supported_riser_boundary_passed"]=sourceRiserBoundary});
            }
            return good;
        }
        JObject Sample(Vector3 before,Vector3 requested,CollisionFlags flags,bool flatEndpoint=false,string expectedOwner=null)
        {
            Physics.SyncTransforms();bool supported=FeetProof(out var rays,out var centre);var feet=player.transform.position;
            var head=new JArray(Physics.OverlapSphere(feet+Vector3.up*(cc.height-cc.radius),cc.radius,~0,QueryTriggerInteraction.Ignore).Where(c=>!Self(c)).Select(Describe));
            var contacts=new JArray(recorder.Hits.Where(h=>h.Collider!=null&&!Self(h.Collider)).Select(h=>{var q=Describe(h.Collider);q["point"]=P(h.Point);q["normal"]=P(h.Normal);return q;}));
            bool headContact=recorder.Hits.Any(h=>h.Collider!=null&&!Self(h.Collider)&&(h.Normal.y<-.2f||h.Point.y>feet.y+cc.height-cc.radius));
            bool expected=string.IsNullOrEmpty(expectedOwner)||centre.collider!=null&&PathOf(centre.collider.transform)==expectedOwner;
            bool endpoint=!flatEndpoint||cc.isGrounded&&centre.collider!=null&&Mathf.Abs(feet.y-centre.point.y)<=.075f;
            float moved=Vector3.Distance(before,feet);bool budget=moved<.35f;
            bool safe=supported&&head.Count==0&&!headContact&&(flags&CollisionFlags.Above)==0&&Stock(cc)&&player.ExternalInputMode&&player.IsPaused&&expected&&endpoint&&budget;
            return new JObject{["frame"]=Time.frameCount,["physics_time"]=Time.fixedTimeAsDouble,["before"]=P(before),["requested_cc_move"]=P(requested),["feet"]=P(feet),["actual_displacement"]=P(feet-before),["displacement_m"]=moved,["grounded"]=cc.isGrounded,["collision_flags"]= (int)flags,["nine_foot_rays"]=rays,["footprint_supported"]=supported,["controller_contacts"]=contacts,["head_contact"]=headContact,["head_overlaps"]=head,["expected_floor_owner"]=expectedOwner,["expected_owner_passed"]=expected,["flat_grounded_endpoint_passed"]=endpoint,["no_traversal_jump_budget_passed"]=budget,["passed"]=safe};
        }
        IEnumerator GravityFrame(JArray trace,Vector3 horizontal,Vector3? segmentStart=null,Vector3? segmentEnd=null)
        {
            recorder.Hits.Clear();var before=player.transform.position;Physics.SyncTransforms();if(cc.isGrounded&&vertical<0)vertical=-2;vertical=Mathf.Max(-30,vertical+Gravity*Dt);var requested=horizontal+Vector3.up*(vertical*Dt);var flags=cc.Move(requested);
            yield return new WaitForFixedUpdate();
            if((flags&CollisionFlags.Below)!=0&&vertical<0)vertical=-2;var sample=Sample(before,requested,flags);trace.Add(sample);
            if (!(bool)sample["passed"])
            {
                string diagnostic=Path.Combine((string)config["output_directory"],"failed-physical-frame.json");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(diagnostic)));
                File.WriteAllText(diagnostic,new JObject{["scene_sha256"]=Sha(ScenePath),["last_sample"]=sample.DeepClone(),["physical_group"]=result.DeepClone(),["validation_weakened"]=false}.ToString());
            }
            Need((bool)sample["passed"],"Actual CC support/headroom/displacement failed; inspect last frame");
            if(segmentStart.HasValue&&segmentEnd.HasValue){float lateral=Lateral(player.transform.position,segmentStart.Value,segmentEnd.Value);sample["lateral_error"]=lateral;Need(lateral<=.24f,"Actual CC left the pinned segment corridor");}
        }
        IEnumerator PlaceOnce(Vector3 feet)
        {
            Need((int)result["initial_placements"]==0,"Only one initial placement permitted in this physical group");player.RestorePhysicalPose(feet+Vector3.up*.04f,savedYaw,savedPitch);result["initial_placements"]=1;Physics.SyncTransforms();
            var trace=new JArray();result["initial_settle"]=trace;for(int i=0;i<15;i++)yield return GravityFrame(trace,Vector3.zero);
            var s=Sample(player.transform.position,Vector3.zero,CollisionFlags.None,true);result["settled_start"]=s;Need((bool)s["passed"],"Initial physical group has no flat grounded supported start");
        }
        IEnumerator WalkPath(JArray points,JObject leg,int startIndex=0)
        {
            var segments=new JArray();leg["segments"]=segments;leg["passed"]=false;leg["initial_placement_in_leg"]=false;
            for(int i=startIndex;i<points.Count;i++)
            {
                var q=points[i];var target=V(q["world_XYZ"]);var start=player.transform.position;bool flat=(bool?)q["flat_endpoint"]==true;string owner=(string)q["expected_floor_owner"];
                var seg=new JObject{["index"]=i,["kind"]=q["kind"]?.DeepClone(),["target"]=P(target),["started_at"]=P(start),["passed"]=false};var frames=new JArray();seg["frames"]=frames;segments.Add(seg);int limit=Mathf.CeilToInt(XZ(start,target)/(WalkSpeed*Dt))+160;bool reached=false;int stalled=0;float airborne=0;
                for(int f=0;f<limit;f++)
                {
                    var before=player.transform.position;var d=target-before;d.y=0;var horizontal=d.magnitude>Reach?Vector3.ClampMagnitude(d,WalkSpeed*Dt):Vector3.zero;
                    yield return GravityFrame(frames,horizontal,start,target);
                    airborne=cc.isGrounded?0:airborne+Dt;Need(airborne<=.6f,"Actual CC airborne longer than 0.6 seconds");
                    float tolerance=flat?.10f:.32f;
                    if(XZ(player.transform.position,target)<=Reach&&Mathf.Abs(player.transform.position.y-target.y)<=tolerance)
                    {
                        var endpoint=Sample(player.transform.position,Vector3.zero,CollisionFlags.None,flat,owner);seg["endpoint"]=endpoint;
                        if((bool)endpoint["passed"]&&cc.isGrounded){reached=true;break;}
                    }
                    stalled=horizontal.sqrMagnitude>0&&XZ(before,player.transform.position)<.001f?stalled+1:0;Need(stalled<75,"Real CC stalled on pinned geometry before waypoint");
                }
                seg["actual_end"]=P(player.transform.position);seg["passed"]=reached;Need(reached,"Pinned source/derivative waypoint did not converge with grounded CC");
            }
            leg["passed"]=true;
        }
        IEnumerator OpenGroup(string mode)
        {
            descriptions.Clear();pinned.Clear();config=JObject.Parse(File.ReadAllText(ConfigPath));Need((string)config["schema"]=="chooguard.upper-coherent-structure-config.v1","Bad runtime config");Need(Sha(ScenePath)==(string)config["scene_sha256"],"Saved final coherent scene hash changed");
            foreach(var pin in config["file_pins"])Need(File.Exists((string)pin["path"])&&Sha((string)pin["path"])==(string)pin["sha256"],"Pinned acceptance/registry/source/route dependency changed: "+pin["path"]);
            Need((long)config["responder_cc_file_id"]==33488353,"Must use saved responder CC33488353");
            Need(File.ReadAllText(ScenePath).Contains("--- !u!143 &33488353\n"),"Saved CC33488353 missing from scene");
            result["config_sha256"]=Sha(ConfigPath);result["saved_scene_sha256"]=Sha(ScenePath);
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;yield return null;
            savedCapture=Time.captureFramerate;savedScale=Time.timeScale;savedFixed=Time.fixedDeltaTime;savedCursor=Cursor.lockState;savedCursorVisible=Cursor.visible;timeSaved=true;
            Time.captureFramerate=50;Time.timeScale=1;Time.fixedDeltaTime=Dt;
            player=Object.FindObjectsByType<FirstPersonResponder>(FindObjectsSortMode.None).Single();cc=player.GetComponent<CharacterController>();Need(Stock(cc),"Saved responder runtime stock dimensions/settings changed");
            var id=GlobalObjectId.GetGlobalObjectIdSlow(cc);Need(id.targetObjectId==33488353&&id.assetGUID.ToString()==(string)config["scene_guid"],"Runtime responder is not the saved CC33488353 instance");
            savedPosition=player.transform.position;savedYaw=player.YawDegrees;savedPitch=player.PitchDegrees;savedExternal=player.ExternalInputMode;savedPause=player.IsPaused;playerSaved=true;result["controller_before"]=Settings(cc);result["controller_saved_global_id"]=id.ToString();
            player.SetExternalInputMode(true);player.Pause();recorder=player.gameObject.AddComponent<UpperCoherentPhysicalContacts>();vertical=0;
            foreach(var p in config["collider_pins"])
            {
                string owner=(string)p["owner_path"];var go=GameObject.Find("/"+owner);Need(go!=null,"Pinned actual collider owner missing: "+owner);var mc=go.GetComponent<MeshCollider>();Need(mc!=null&&mc.enabled&&!mc.isTrigger&&!mc.convex&&mc.sharedMesh!=null&&mc.sharedMesh.isReadable,"Pinned collider disabled/unreadable/non-fixed: "+owner);
                var m=mc.sharedMesh;string asset=AssetDatabase.GetAssetPath(m);Need(asset==(string)p["asset"]&&AssetDatabase.AssetPathToGUID(asset)==(string)p["guid"]&&Sha(asset)==(string)p["asset_sha256"]&&Sha(MeshChannels(m))==(string)p["channels_sha256"],"Actual persistent collider mesh/attributes pin mismatch: "+owner);
                for(int r=0;r<4;r++)for(int c=0;c<4;c++)Need(Mathf.Abs(mc.transform.localToWorldMatrix[r,c]-(float)p["world_matrix"][r][c])<.00002f,"Pinned actual collider frame changed: "+owner);
                pinned.Add(owner,mc);
            }
            result["collider_pins_checked"]=pinned.Count;result["phase"]="actual_traversal";
        }
        IEnumerator FixedStairAndPrivate()
        {
            var path=(string)config["fixed_stair_waypoints"];Need(Sha(path)==WaypointSha,"Reviewed34 source stair waypoints changed");var j=JObject.Parse(File.ReadAllText(path));var points=(JArray)j["ordered_waypoints"].DeepClone();Need(points.Count==34,"Expected complete34 fixed source stair waypoints");
            foreach(var p in points){p["flat_endpoint"]=(int)p["index"]==0||(int)p["index"]==33;p["expected_floor_owner"]=(int)p["index"]==33?config["fixed_stair_upper_floor_owner"].DeepClone():null;}
            var extensions=config["upper_private_extension"] as JArray ?? new JArray();result["upper_private_extension_requested"]=extensions.Count>0;
            if(extensions.Count>0)
            {
                Need(extensions.Count>=2,"Explicit structural private continuation needs two or more points");
                Need(XZ(V(extensions[0]["world_XYZ"]),V(points[33]["world_XYZ"]))<.001f,"Private continuation must begin at exact source stair exit");
                foreach(var p in extensions.Skip(1))points.Add(p.DeepClone());
                Need((string)points.Last["expected_floor_owner"]==(string)config["required_private_support_owner"],"Structural continuation must end on its pinned private floor");
            }
            yield return PlaceOnce(V(points[0]["world_XYZ"]));var legs=(JArray)result["legs"];
            var up=new JObject{["direction"]=extensions.Count>0?"source_stair_to_private_floor":"source_fixed_stair_lower_to_upper"};legs.Add(up);yield return WalkPath(points,up);
            var down=new JObject{["direction"]=extensions.Count>0?"private_floor_to_source_stair":"source_fixed_stair_upper_to_lower"};legs.Add(down);yield return WalkPath(new JArray(points.Reverse().Select(x=>x.DeepClone())),down);
        }




        IEnumerator Run(string mode)
        {
            config=JObject.Parse(File.ReadAllText(ConfigPath));string output=Path.Combine((string)config["output_directory"],mode+".json");Assert.That(!File.Exists(output),Is.True,"Fresh physical proof required: "+output);
            result=new JObject{["schema"]="chooguard.upper-coherent-actual-physical-proof.v1",["group"]=mode,["passed"]=false,["initial_placements"]=0,["traversal_teleports"]=0,["jump_used"]=false,["controller_settings_modified"]=false,["geometry_modified"]=false,["physical_step_seconds"]=Dt,["walk_speed_mps"]=WalkSpeed,["legs"]=new JArray(),["field_accuracy99percent_verified"]=false,["phase"]="preflight"};
            try
            {
                yield return OpenGroup(mode);
                yield return FixedStairAndPrivate();
                result["controller_after"]=Settings(cc);Need(JToken.DeepEquals(result["controller_before"],result["controller_after"])&&Stock(cc),"Actual stock controller settings changed during traversal");Need(Sha(ScenePath)==(string)config["scene_sha256"],"Saved final scene changed during PlayMode proof");
                result["passed"]=((JArray)result["legs"]).Count==2&&((JArray)result["legs"]).All(x=>(bool)x["passed"])&&(int)result["initial_placements"]==1;result["phase"]="physical_roundtrip_finished";
            }
            finally
            {
                var errors=new JArray();result["restoration_errors"]=errors;
                try
                {
                    if(recorder!=null)Object.DestroyImmediate(recorder);
                    if(playerSaved&&player!=null){player.RestorePhysicalPose(savedPosition,savedYaw,savedPitch);player.SetExternalInputMode(savedExternal);if(savedPause)player.Pause();else player.Resume(!savedExternal);result["restoration_pose_only"]=true;Need(Vector3.Distance(player.transform.position,savedPosition)<.0001f&&player.ExternalInputMode==savedExternal&&player.IsPaused==savedPause,"Player restoration mismatch");}
                }
                catch(Exception e){errors.Add(e.ToString());}
                finally
                {
                    if(timeSaved){Time.captureFramerate=savedCapture;Time.timeScale=savedScale;Time.fixedDeltaTime=savedFixed;Cursor.lockState=savedCursor;Cursor.visible=savedCursorVisible;}playerSaved=false;timeSaved=false;
                    if(errors.Count>0)result["passed"]=false;result["saved_scene_unchanged"]=Sha(ScenePath)==(string)config["scene_sha256"];
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));using(var f=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var sw=new StreamWriter(f))sw.Write(result.ToString());
                }
            }
            Assert.That((bool)result["passed"],Is.True,"Inspect real saved-controller physical proof: "+output);
        }
        [UnityTest,Timeout(600000)]public IEnumerator SourceFixedStairStructureRoundtrip()=>Run("source-stair-structure-roundtrip");
        [UnityTearDown]public IEnumerator Cleanup(){var s=SceneManager.GetSceneByName(SceneFlow.StationScene);if(s.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("UpperCoherentPhysicalScratch"));yield return SceneManager.UnloadSceneAsync(s);}}
    }
}
#endif
