#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Shell;
using Object=UnityEngine.Object;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class Main2fSourceCompletionContactRecorder : MonoBehaviour
    {
        public struct Contact { public Collider Collider; public Vector3 Point,Normal; }
        public readonly List<Contact> Contacts=new List<Contact>();
        void OnControllerColliderHit(ControllerColliderHit hit){Contacts.Add(new Contact{Collider=hit.collider,Point=hit.point,Normal=hit.normal});}
    }

    // This is a source-local physical correction test, never a whole-floor or field-accuracy test.
    public sealed class Main2fSourceCompletionPlayTests
    {
        const string Config=".planning/2026-10-09-interior-99-reconstruction/pass3-integration/source64-runtime-config.json";
        const string Spec="workers/twin/output/main2f-source-completion-20261009-pass3/source64-actual-player-trials.json";
        const string SpecSha="8c732cb9e66d148be1cbcb9a53399ff68b8b1b5239ae0ac9f34970fa1f942fee";
        const string Packet="workers/twin/output/main2f-source-completion-20261009-pass3/main2f-source-completion-packet.json";
        const string PacketSha="c224f46792cde1e58d499aca276760cc20aa1abfbf92f65cabd5fcc196a82c9d";
        const string ScenePath="Assets/ChooGuard/Scenes/FpsStation.unity";
        const float Step=.035f,Down=.06f,HorizontalTolerance=.08f,FloorTolerance=.075f;
        static string Sha(byte[] bytes){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        static string Sha(string path)=>Sha(File.ReadAllBytes(path));
        static JArray P(Vector3 p)=>new JArray(p.x,p.y,p.z);
        static Vector3 V(JToken p)=>new Vector3((float)p[0],(float)p[1],(float)p[2]);
        static string PathOf(Transform t){var a=new List<string>();for(;t!=null;t=t.parent)a.Add(t.name);a.Reverse();return string.Join("/",a);}
        static string Id(Object o)=>GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();
        static float HorizontalDistance(Vector3 a,Vector3 b){var d=a-b;d.y=0;return d.magnitude;}
        static bool Near(Vector3 a,Vector3 b,float epsilon=.000025f)=>Vector3.Distance(a,b)<=epsilon;
        static JObject ReadPinned(string path,string expected){if(Sha(path)!=expected)throw new InvalidDataException("Pinned dependency changed: "+path);return JObject.Parse(File.ReadAllText(path));}
        static byte[] FrozenChannels(Mesh mesh)
        {
            if(mesh==null||!mesh.isReadable)throw new InvalidDataException("Exact mesh channels unavailable");
            if(mesh.blendShapeCount!=0||mesh.boneWeights.Length!=0||mesh.bindposes.Length!=0)throw new InvalidDataException("Unexpected skinning/blendshapes; exporter contract must be extended");
            using(var memory=new MemoryStream())
            {
                using(var w=new BinaryWriter(memory,System.Text.Encoding.UTF8,true))
                {
                    w.Write(1);var vertices=mesh.vertices;w.Write(vertices.Length);foreach(var v in vertices){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
                    var normals=mesh.normals;w.Write(normals.Length);foreach(var v in normals){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
                    var tangents=mesh.tangents;w.Write(tangents.Length);foreach(var v in tangents){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}
                    var colors=mesh.colors;w.Write(colors.Length);foreach(var v in colors){w.Write(v.r);w.Write(v.g);w.Write(v.b);w.Write(v.a);}
                    for(int u=0;u<8;u++){var uv=new List<Vector4>();mesh.GetUVs(u,uv);w.Write(uv.Count);w.Write(uv.Count==0?0:mesh.GetVertexAttributeDimension((UnityEngine.Rendering.VertexAttribute)((int)UnityEngine.Rendering.VertexAttribute.TexCoord0+u)));foreach(var v in uv){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}}
                    w.Write(mesh.subMeshCount);for(int s=0;s<mesh.subMeshCount;s++){w.Write((int)mesh.GetTopology(s));var ix=mesh.GetIndices(s);w.Write(ix.Length);foreach(int i in ix)w.Write(i);}
                }
                return memory.ToArray();
            }
        }
        static JObject Triangle(MeshCollider mc,int ordinal)
        {
            if(mc==null||mc.sharedMesh==null||!mc.sharedMesh.isReadable||ordinal<0)return new JObject{["exact_geometry_available"]=false};
            var ix=mc.sharedMesh.triangles;if(ordinal*3+2>=ix.Length)return new JObject{["exact_geometry_available"]=false};
            var v=mc.sharedMesh.vertices;int a=ix[ordinal*3],b=ix[ordinal*3+1],c=ix[ordinal*3+2];var q=new[]{mc.transform.TransformPoint(v[a]),mc.transform.TransformPoint(v[b]),mc.transform.TransformPoint(v[c])};
            var cross=Vector3.Cross(q[1]-q[0],q[2]-q[0]);return new JObject{["exact_geometry_available"]=true,["triangle_ordinal"]=ordinal,["indices"]=new JArray(a,b,c),["world_vertices"]=new JArray(q.Select(P)),["geometric_normal"]=P(cross.normalized),["normal_up_absolute_dot"]=Mathf.Abs(cross.normalized.y),["world_y_span"]=q.Max(p=>p.y)-q.Min(p=>p.y),["world_area"]=cross.magnitude*.5f};
        }
        static JObject Hit(RaycastHit h)
        {
            var c=h.collider;var mc=c as MeshCollider;string asset=mc!=null&&mc.sharedMesh!=null?AssetDatabase.GetAssetPath(mc.sharedMesh):null;
            return new JObject{["owner_path"]=PathOf(c.transform),["collider_global_object_id"]=Id(c),["collider_type"]=c.GetType().Name,["asset"]=asset,["asset_guid"]=string.IsNullOrEmpty(asset)?null:AssetDatabase.AssetPathToGUID(asset),["asset_sha256"]=string.IsNullOrEmpty(asset)||!File.Exists(asset)?null:Sha(asset),["point"]=P(h.point),["normal"]=P(h.normal),["distance"]=h.distance,["triangle_index"]=h.triangleIndex,["triangle_evidence"]=Triangle(mc,h.triangleIndex)};
        }
        static bool Accepted(JObject hit,JArray approved,JArray approvedAssets,float expectedY,out string reason)
        {
            reason="No exact root-reviewed mesh triangle matches this hit";
            var tri=hit["triangle_evidence"];if(!(bool)tri["exact_geometry_available"])return false;
            if((float)hit["normal"][1]<.95f||(float)tri["normal_up_absolute_dot"]<.98f||(float)tri["world_y_span"]>.025f||(float)tri["world_area"]<.0000005f||Mathf.Abs((float)hit["point"][1]-expectedY)>.08f){reason="Actual hit is not a horizontal triangle near the expected modeled floor";return false;}
            foreach(var p in approved)
            {
                if((string)p["accepted_source_evidence"]==null||string.IsNullOrWhiteSpace((string)p["accepted_source_evidence"]))continue;
                if((string)p["collider_global_object_id"]!=(string)hit["collider_global_object_id"]||(string)p["owner_path"]!=(string)hit["owner_path"]||(string)p["asset_guid"]!=(string)hit["asset_guid"]||(string)p["asset_sha256"]!=(string)hit["asset_sha256"]||(int)p["triangle_ordinal"]!=(int)hit["triangle_index"])continue;
                bool same=true;for(int k=0;k<3;k++)same&=(int)p["indices"][k]==(int)tri["indices"][k]&&Near(V(p["world_vertices"][k]),V(tri["world_vertices"][k]));
                if(same){reason="Exact reviewed owner, asset SHA/GUID, ordinal, indices and world vertices match";return true;}
            }
            foreach(var p in approvedAssets)
            {
                if(string.IsNullOrWhiteSpace((string)p["accepted_source_evidence"]))continue;
                if((string)p["collider_global_object_id"]!=(string)hit["collider_global_object_id"]||(string)p["owner_path"]!=(string)hit["owner_path"]||(string)p["asset_guid"]!=(string)hit["asset_guid"]||(string)p["asset_sha256"]!=(string)hit["asset_sha256"])continue;
                // SHA pins all vertices and indices in this exact asset. Every queried face still
                // has to independently prove horizontal Y7 geometry; this never accepts by name.
                if(((JArray)tri["world_vertices"]).All(v=>Mathf.Abs((float)v[1]-expectedY)<.025f))
                {reason="Root-reviewed exact whole floor asset; each actual hit triangle independently proves horizontal modeled Y7 geometry";return true;}
            }
            return false;
        }
        static bool Support(Vector3 feet,CharacterController cc,JArray approved,JArray approvedAssets,out float high,out JArray proof)
        {
            bool all=true;high=float.NegativeInfinity;proof=new JArray();
            for(int k=-1;k<8;k++)
            {
                var offset=k<0?Vector3.zero:new Vector3(Mathf.Cos(k*Mathf.PI/4),0,Mathf.Sin(k*Mathf.PI/4))*cc.radius*.92f;
                var origin=feet+offset+Vector3.up*.65f;
                var hits=Physics.RaycastAll(origin,Vector3.down,1.3f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider!=cc&&!h.collider.transform.IsChildOf(cc.transform)).OrderBy(h=>h.distance).ToArray();
                if(hits.Length==0){all=false;proof.Add(new JObject{["ray"]=k,["origin"]=P(origin),["supported"]=false,["reason"]="No actual surface hit"});continue;}
                // The physically exposed nearest surface must itself be reviewed; do not select a buried approved face.
                var hit=Hit(hits[0]);bool valid=Accepted(hit,approved,approvedAssets,7f,out string reason)&&Mathf.Abs(feet.y-hits[0].point.y)<FloorTolerance;
                all&=valid;high=Mathf.Max(high,hits[0].point.y);proof.Add(new JObject{["ray"]=k,["origin"]=P(origin),["supported"]=valid,["identity_reason"]=reason,["nearest_actual_hit"]=hit,["other_hit_count"]=hits.Length-1});
            }
            return all;
        }
        static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*d1/(d1-d3);
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*d2/(d2-d6);
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*(d4-d3)/((d4-d3)+(d5-d6));
            float den=1/(va+vb+vc);return a+ab*(vb*den)+ac*(vc*den);
        }
        static JObject Contact(Main2fSourceCompletionContactRecorder.Contact h,JObject colliderRow,bool before,Transform sourceFrame)
        {
            var row=new JObject{["owner_path"]=h.Collider==null?null:PathOf(h.Collider.transform),["collider_global_object_id"]=h.Collider==null?null:Id(h.Collider),["point"]=P(h.Point),["normal"]=P(h.Normal),["matches_exact_removed_source_MC_face"]=false};
            if(h.Collider==null)return row;
            // ControllerColliderHit contains no triangle index; make the recovery query explicit.
            var ray=new Ray(h.Point+h.Normal*.04f,-h.Normal);
            if(h.Collider.Raycast(ray,out var recovered,.09f))row["recovered_contact_triangle_query"]=Hit(recovered);
            if(!before||PathOf(h.Collider.transform)!=(string)colliderRow["owner_path"]||Id(h.Collider)!=(string)colliderRow["exact_geometry_component_global_id"]||Mathf.Abs(h.Normal.y)>.5f)return row;
            var mc=h.Collider as MeshCollider;if(mc==null)return row;var verts=mc.sharedMesh.vertices;
            var distances=new JArray();bool exact=false;
            foreach(var i in colliderRow["remove_source_indices"])
            {
                var a=sourceFrame.TransformPoint(verts[(int)i[0]]);var b=sourceFrame.TransformPoint(verts[(int)i[1]]);var c=sourceFrame.TransformPoint(verts[(int)i[2]]);float distance=Vector3.Distance(h.Point,Closest(h.Point,a,b,c));
                distances.Add(distance);exact|=distance<.075f;
            }
            row["removed_source_face_distances_m"]=distances;row["matches_exact_removed_source_MC_face"]=exact;return row;
        }
        static Mesh Geometry(Component c)=>c is MeshCollider mc?mc.sharedMesh:((MeshFilter)c).sharedMesh;
        static Mesh SavedRendererMesh(Component c,string asset,string guid,out bool staticBatch)
        {
            staticBatch=false;var live=Geometry(c);
            if(!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(live)))return live;
            var renderer=c.GetComponent<MeshRenderer>();
            if(!(c is MeshFilter)||renderer==null||!renderer.isPartOfStaticBatch)throw new InvalidDataException("Missing persistent mesh without a real static-batched renderer");
            GlobalObjectId id;if(!GlobalObjectId.TryParse(Id(c),out id))throw new InvalidDataException("Invalid saved renderer identity");
            string yaml=File.ReadAllText(ScenePath),header="--- !u!33 &"+id.targetObjectId;
            int start=yaml.IndexOf(header+"\n",StringComparison.Ordinal);if(start<0)start=yaml.IndexOf(header+"\r\n",StringComparison.Ordinal);
            if(start<0)throw new InvalidDataException("Saved MeshFilter component missing");
            int end=yaml.IndexOf("\n--- !u!",start+header.Length,StringComparison.Ordinal);if(end<0)end=yaml.Length;
            string block=yaml.Substring(start,end-start);
            if(!System.Text.RegularExpressions.Regex.IsMatch(block,@"m_Mesh:\s*\{fileID:\s*4300000,\s*guid:\s*"+guid+@",\s*type:\s*2\}"))throw new InvalidDataException("Exact saved renderer mesh GUID differs");
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(asset);if(saved==null)throw new InvalidDataException("Saved renderer asset missing");
            staticBatch=true;return saved;
        }
        static Component FindGeometry(JObject row)
        {
            string path=(string)row["owner_path"],kind=(string)row["owner_kind"];
            var matches=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.gameObject.scene==SceneManager.GetActiveScene()&&PathOf(t)==path).ToArray();
            if(matches.Length!=1)throw new InvalidDataException("Exact source owner missing/ambiguous: "+path);
            Component c=kind=="MeshCollider"?(Component)matches[0].GetComponent<MeshCollider>():matches[0].GetComponent<MeshFilter>();if(c==null)throw new InvalidDataException("Exact source component missing");
            if(Id(c)!=(string)row["exact_geometry_component_global_id"])throw new InvalidDataException("Exact source component ID changed: "+path);
            for(int r=0;r<4;r++)for(int col=0;col<4;col++)if(Mathf.Abs(c.transform.localToWorldMatrix[r,col]-(float)row["world_matrix"][r][col])>.000025f)throw new InvalidDataException("Source owner frame changed");
            return c;
        }
        static JArray GeometryProof(JObject packet,JObject config,bool before)
        {
            var results=new JArray();
            foreach(JObject row in packet["meshes"])
            {
                var c=FindGeometry(row);string expectedAsset=(string)row["source_asset"],expectedGuid=(string)row["source_asset_guid"];
                if(!before){var binding=((JArray)config["after_geometry_bindings"]).Single(x=>(string)x["role"]==(string)row["role"]);expectedAsset=(string)binding["asset"];expectedGuid=(string)binding["guid"];}
                var mesh=SavedRendererMesh(c,expectedAsset,expectedGuid,out bool staticBatch);string asset=AssetDatabase.GetAssetPath(mesh);string guid=AssetDatabase.AssetPathToGUID(asset);string assetSha=Sha(asset);
                if(before&&(asset!=(string)row["source_asset"]||guid!=(string)row["source_asset_guid"]||assetSha!=(string)row["source_file_sha256"]))throw new InvalidDataException("Before source asset changed");
                if(!before)
                {
                    var after=((JArray)config["after_geometry_bindings"]).Single(x=>(string)x["role"]==(string)row["role"]);
                    if(asset!=(string)after["asset"]||guid!=(string)after["guid"]||assetSha!=(string)after["sha256"])throw new InvalidDataException("After root-approved binding changed");
                }
                string frozen=(string)row[before?"source_full_binary":"retained_full_binary"],expected=(string)row[before?"source_full_binary_sha256":"retained_full_binary_sha256"];
                if(Sha(frozen)!=expected)throw new InvalidDataException("Frozen full-channel binary changed");
                var expectedBytes=File.ReadAllBytes(frozen);
                var bytes=FrozenChannels(mesh);bool exact=Sha(bytes)==expected&&bytes.SequenceEqual(expectedBytes);
                if(!exact)throw new InvalidDataException("All-channel/index-order proof failed for "+(string)row["role"]);
                var sourceFaces=new JArray();var vertices=mesh.vertices;
                for(int j=0;j<((JArray)row["remove_source_indices"]).Count;j++)
                {
                    var inds=row["remove_source_indices"][j];sourceFaces.Add(new JObject{["source_triangle_ordinal"]=row["remove_triangle_ordinals"][j],["source_indices"]=inds.DeepClone(),["world_vertices"]=new JArray(Enumerable.Range(0,3).Select(k=>P(c.transform.TransformPoint(vertices[(int)inds[k]])))),["present_in_current_ordered_index_list"]=before,["removed_from_current_ordered_index_list"]=!before});
                }
                results.Add(new JObject{["role"]=row["role"],["owner_path"]=PathOf(c.transform),["global_object_id"]=Id(c),["asset"]=asset,["guid"]=guid,["sha256"]=assetSha,["frozen_full_channels_sha256"]=expected,["exact_all_channels_and_retained_index_order"]=exact,["static_batched_renderer_saved_scene_binding_verified"]=staticBatch,["channel_proof_target"]=staticBatch?"exact persistent source asset and saved scene GUID; live renderer is statically batched":"actual persistent scene component mesh",["vertex_count"]=mesh.vertexCount,["triangle_count"]=mesh.triangles.Length/3,["source_correction_triangles"]=sourceFaces});
            }
            return results;
        }
        [UnityTest,Timeout(240000)]
        public IEnumerator ActualSavedPlayerPhysicallyCrossesBothSource64LinesInBothDirections()
        {
            var proof=new JObject{["schema"]="chooguard.main2f-source64-real-player-proof.v1",["started_utc"]=DateTime.UtcNow.ToString("o"),["passed"]=false,["scope"]="Two source-open south service-room crossings after64source-fragment correction",["traversal_teleports"]=0,["scene_geometry_modified"]=false,["field_metric_accuracy_verified"]=false};
            string result=System.IO.Path.GetDirectoryName(Config)+"/source64-unconfigured-failure-"+Guid.NewGuid().ToString("N")+".json";
            FirstPersonResponder responder=null;CharacterController cc=null;Main2fSourceCompletionContactRecorder recorder=null;
            Vector3 oldPosition=default;float oldYaw=0,oldPitch=0;bool oldExternal=false,oldPaused=true,oldEnabled=true;bool actorSaved=false;
            int oldCapture=Time.captureFramerate;float oldScale=Time.timeScale;var oldCursor=Cursor.lockState;bool oldCursorVisible=Cursor.visible;
            try
            {
                var config=JObject.Parse(File.ReadAllText(Config));string requested=(string)config["output_proof"];
                if(string.IsNullOrWhiteSpace(requested))throw new InvalidDataException("Root must provide a unique output proof path");if(File.Exists(requested))throw new IOException("Frozen proof already exists; choose a new output path");result=requested;
                var spec=ReadPinned(Spec,SpecSha);var packet=ReadPinned(Packet,PacketSha);string state=(string)config["expected_state"];
                if(state!="after_clear")throw new InvalidDataException("Source64 fixture requires positive after_clear expectations");bool before=false;
                if(Sha(ScenePath)!=(string)config["expected_saved_scene_sha256"])throw new InvalidDataException("Saved scene epoch changed");
                if(before&&Sha(ScenePath)!=(string)packet["scene_epoch"])throw new InvalidDataException("Before baseline differs from source packet epoch");
                var approved=(JArray)config["approved_floor_triangles"]??new JArray();var approvedAssets=(JArray)config["approved_floor_assets"]??new JArray();if(approved.Count+approvedAssets.Count==0)throw new InvalidDataException("No root-reviewed exact floor triangles/assets; unreviewed floors cannot pass");
                proof["expected_state"]=state;proof["saved_scene_sha256"]=Sha(ScenePath);proof["config_sha256"]=Sha(Config);proof["source_spec_sha256"]=SpecSha;proof["source_packet_sha256"]=PacketSha;proof["floor_identity_approval_count"]=approved.Count;proof["floor_asset_identity_approval_count"]=approvedAssets.Count;
                yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene,LoadSceneMode.Single);yield return null;Time.captureFramerate=30;Time.timeScale=1;
                proof["geometry_correspondence"]=GeometryProof(packet,config,before);
                if(spec["cases"].Count()!=2)throw new InvalidDataException("Two exact source64cases required");
                responder=Object.FindObjectsByType<FirstPersonResponder>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.gameObject.scene==SceneManager.GetActiveScene());cc=responder.GetComponent<CharacterController>();
                if(Id(cc)!=(string)spec["existing_controller_global_id"])throw new InvalidDataException("Actual serialized player controller identity differs");
                if(Mathf.Abs(cc.height-1.72f)>.0001f||Mathf.Abs(cc.radius-.28f)>.0001f||!Near(cc.center,new Vector3(0,.86f,0),.0001f)||Mathf.Abs(cc.skinWidth-.025f)>.0001f||Mathf.Abs(cc.stepOffset-.28f)>.0001f||(responder.transform.lossyScale-Vector3.one).magnitude>.0001f)throw new InvalidDataException("Actual serialized player dimensions differ");
                proof["actual_controller"]=new JObject{["owner_path"]=PathOf(cc.transform),["global_object_id"]=Id(cc),["height"]=cc.height,["radius"]=cc.radius,["center"]=P(cc.center),["skin_width"]=cc.skinWidth,["step_offset"]=cc.stepOffset,["min_move_distance"]=cc.minMoveDistance,["slope_limit"]=cc.slopeLimit};
                oldPosition=responder.transform.position;oldYaw=responder.YawDegrees;oldPitch=responder.PitchDegrees;oldExternal=responder.ExternalInputMode;oldPaused=responder.IsPaused;oldEnabled=cc.enabled;actorSaved=true;
                responder.SetExternalInputMode(true);responder.Pause();recorder=responder.gameObject.AddComponent<Main2fSourceCompletionContactRecorder>();
                var colliderRow=(JObject)((JArray)packet["meshes"]).Single(x=>(string)x["owner_kind"]=="MeshCollider");var colliderComponent=FindGeometry(colliderRow);
                var trials=new JArray();proof["actual_bidirectional_trials"]=trials;int placements=0;
                foreach(JObject crossing in spec["cases"])foreach(bool reverse in new[]{false,true})
                {
                    var start=V(crossing[reverse?"world_end_feet_reference":"world_start_feet_reference"]);var target=V(crossing[reverse?"world_start_feet_reference":"world_end_feet_reference"]);
                    responder.RestorePhysicalPose(start+Vector3.up*.04f,oldYaw,oldPitch);placements++;Physics.SyncTransforms();
                    for(int settle=0;settle<10;settle++){cc.Move(Vector3.down*Down);yield return null;}
                    bool initialSupport=Support(responder.transform.position,cc,approved,approvedAssets,out float floor,out var initial);
                    var trial=new JObject{["case_id"]=crossing["id"].DeepClone(),["reverse"]=reverse,["source_start"]=crossing[reverse?"source_end_pixel":"source_start_pixel"].DeepClone(),["source_end"]=crossing[reverse?"source_start_pixel":"source_end_pixel"].DeepClone(),["world_start"]=P(start),["world_target"]=P(target),["initial_actual_feet"]=P(responder.transform.position),["initial_grounded"]=cc.isGrounded,["initial_support"]=initial,["initial_support_valid"]=initialSupport,["crossing_passed"]=false,["expected_block_proved"]=false};trials.Add(trial);
                    var samples=new JArray();trial["samples"]=samples;bool valid=initialSupport&&cc.isGrounded&&Mathf.Abs(responder.transform.position.y-floor)<FloorTolerance;
                    bool reached=false,blocked=false;int stalled=0;string failure=valid?null:"Initial full capsule footprint is ungrounded or lacks reviewed physical floor";
                    int maxSteps=Mathf.CeilToInt(HorizontalDistance(start,target)/Step)+35;
                    for(int k=0;k<maxSteps&&valid;k++)
                    {
                        var actualBefore=responder.transform.position;var move=target-actualBefore;move.y=0;
                        if(move.magnitude<.045f){reached=true;break;}
                        move=Vector3.ClampMagnitude(move,Step);recorder.Contacts.Clear();var flags=cc.Move(move+Vector3.down*Down);yield return null;
                        var actual=responder.transform.position;var supportOK=Support(actual,cc,approved,approvedAssets,out floor,out var supports);var advance=actual-actualBefore;advance.y=0;
                        var line=target-start;line.y=0;var along=actual-start;along.y=0;float lateral=(along-Vector3.Project(along,line)).magnitude;
                        var contacts=new JArray(recorder.Contacts.Select(h=>Contact(h,colliderRow,before,colliderComponent.transform)));
                        bool exactDividerContact=contacts.Any(h=>(bool)h["matches_exact_removed_source_MC_face"]);
                        bool validStep=supportOK&&cc.isGrounded&&(flags&CollisionFlags.Below)!=0&&Mathf.Abs(actual.y-floor)<FloorTolerance&&cc.enabled&&advance.magnitude<=Step+.005f&&lateral<HorizontalTolerance;
                        bool blockingStep=validStep&&exactDividerContact&&(flags&CollisionFlags.Sides)!=0&&advance.magnitude<Step*.25f;
                        stalled=blockingStep?stalled+1:0;blocked=stalled>=8&&HorizontalDistance(actual,target)>.4f;
                        samples.Add(new JObject{["sample"]=k,["command_horizontal_m"]=move.magnitude,["actual_feet"]=P(actual),["actual_horizontal_advance_m"]=advance.magnitude,["lateral_route_error_m"]=lateral,["distance_to_target_m"]=HorizontalDistance(actual,target),["collision_flags"]=(int)flags,["grounded"]=cc.isGrounded,["floor_identity_and_footprint_valid"]=supportOK,["support"]=supports,["contacts"]=contacts,["exact_removed_MC_face_contact"]=exactDividerContact,["valid_step"]=validStep,["expected_block_proved"]=blocked});
                        valid&=validStep;if(!valid){failure="Actual Move lost reviewed support/grounding or left the source-local route";break;}
                        if(blocked)break;
                    }
                    reached|=valid&&HorizontalDistance(responder.transform.position,target)<.045f;
                    bool finalSupport=Support(responder.transform.position,cc,approved,approvedAssets,out floor,out var finalProof);valid&=finalSupport&&cc.isGrounded;
                    bool outcome=valid&&(before?blocked&&!reached:reached&&!blocked);
                    trial["actual_finish"]=P(responder.transform.position);trial["end_support"]=finalProof;trial["reached_endpoint"]=reached;trial["expected_block_proved"]=blocked;trial["crossing_passed"]=!before&&outcome;trial["expected_outcome_verified"]=outcome;
                    trial["failure"]=failure??(outcome?null:(before?"Exact source divider blockage was not proved":"Actual controller did not reach the reviewed endpoint"));
                }
                proof["initial_fixture_placements"]=placements;proof["all_expected_outcomes_verified"]=trials.Count==4&&trials.All(t=>(bool)t["expected_outcome_verified"]);
                proof["actual_clear_crossings"]=trials.Count(t=>(bool)t["crossing_passed"]);proof["actual_blocked_trials"]=trials.Count(t=>(bool)t["expected_block_proved"]);
                proof["passed"]=(bool)proof["all_expected_outcomes_verified"]&&Sha(ScenePath)==(string)config["expected_saved_scene_sha256"];
            }
            finally
            {
                if(actorSaved&&responder!=null){responder.RestorePhysicalPose(oldPosition,oldYaw,oldPitch);responder.SetExternalInputMode(oldExternal);if(oldPaused)responder.Pause();else responder.Resume(!oldExternal);if(cc!=null)cc.enabled=oldEnabled;proof["runtime_pose_restored"]=Near(responder.transform.position,oldPosition,.0001f)&&Mathf.Abs(Mathf.DeltaAngle(responder.YawDegrees,oldYaw))<.001f&&Mathf.Abs(responder.PitchDegrees-oldPitch)<.001f;proof["runtime_input_state_restored"]=responder.ExternalInputMode==oldExternal&&responder.IsPaused==oldPaused;}
                if(recorder!=null)Object.Destroy(recorder);Time.captureFramerate=oldCapture;Time.timeScale=oldScale;Cursor.lockState=oldCursor;Cursor.visible=oldCursorVisible;
                proof["time_settings_restored"]=Time.captureFramerate==oldCapture&&Mathf.Abs(Time.timeScale-oldScale)<.0001f;proof["finished_utc"]=DateTime.UtcNow.ToString("o");
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(result));using(var stream=new FileStream(result,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var writer=new StreamWriter(stream))writer.Write(proof.ToString());
            }
            Assert.That((bool)proof["passed"],Is.True,"Source-local physical divider outcome failed; inspect "+result);
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            var scene=SceneManager.GetSceneByName(SceneFlow.StationScene);if(scene.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("Main2fSource64ProbeScratch"));yield return SceneManager.UnloadSceneAsync(scene);}
        }
    }
}
#endif
