#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Twin;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Staged follow-up only: root must first reconcile all exact native faces/material submeshes/colliders.</summary>
    public static class ReviewedNativeAutomaticDoorBindings
    {
        const float Tolerance=.001f;
        static Vector3 V(JToken t)=>new Vector3((float)t[0],(float)t[1],(float)t[2]);
        static SourcePartIdentity Identity(string id)=>UnityEngine.Object.FindObjectsByType<SourcePartIdentity>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.SourceId==id);
        sealed class Plan
        {
            public JToken Row;
            public SourcePartIdentity Button;
            public SourcePartIdentity[] Leaves;
            public JToken[] LeafRows;
            public StationDoor.DoorUse Use;
        }
        /// <param name="reviewedUse">Explicit policy for each button to install. Main-entrance four are Public in source; fifth unnamed opening requires root classification.</param>
        /// <remarks>Root importer must assign each new leaf SourceId=packet.part_id, Category=automatic_door_leaf_source_part,
        /// LegacyGeometrySeparated=true and retain exact UV/material/normal/collider buffers. Original405 identities stay intact.
        /// This method performs no native batch cuts and must never be run before a live geometry reconciliation.</remarks>
        public static StationDoor[] Bind(string packetPath,IReadOnlyDictionary<string,StationDoor.DoorUse> reviewedUse)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit-mode reviewed binding only");
            var packet=JObject.Parse(File.ReadAllText(packetPath));
            if((string)packet["schema"]!="chooguard.exact-native-sliding-dependencies.v1")throw new InvalidDataException("Unexpected source packet");
            var leafRows=((JArray)packet["leaves"]).ToDictionary(x=>(string)x["part_id"]);
            var plans=new List<Plan>();
            foreach(var row in (JArray)packet["bindings"])
            {
                var buttonId=(string)row["source_button_id"];
                if(!reviewedUse.TryGetValue(buttonId,out var use))continue;
                if(UnityEngine.Object.FindObjectsByType<StationDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(x=>x.Element==(string)row["operator_element"]))throw new InvalidOperationException("Already bound exact native opening: "+buttonId);
                var button=Identity(buttonId);
                if(button.Category!="automatic_door_button"||!button.LegacyGeometrySeparated||button.GetComponent<SourcePartAutomaticButton>()!=null)throw new InvalidDataException("Button owner was already bound or not reconciled: "+buttonId);
                var partIds=((JArray)row["leaf_part_ids"]).Select(x=>(string)x).ToArray();
                var leaves=partIds.Select(Identity).ToArray();var geometry=partIds.Select(x=>leafRows[x]).ToArray();
                for(int i=0;i<leaves.Length;i++)
                {
                    var identity=leaves[i];var source=geometry[i];
                    if(identity.Category!="automatic_door_leaf_source_part"||!identity.LegacyGeometrySeparated)throw new InvalidDataException("Native leaf surgery was not committed: "+identity.SourceId);
                    var filter=identity.GetComponentsInChildren<MeshFilter>(true).Single();var mesh=filter.sharedMesh;
                    if(mesh==null||mesh.triangles.Length/3!=((JArray)source["triangles"]).Count||mesh.uv.Length!=mesh.vertexCount)throw new InvalidDataException("Leaf face/UV conservation differs: "+identity.SourceId);
                    if(mesh.normals.Length!=mesh.vertexCount)throw new InvalidDataException("Missing preserved normals: "+identity.SourceId);
                    if(filter.GetComponent<MeshCollider>()?.sharedMesh!=mesh)throw new InvalidDataException("Exact source collision mesh missing: "+identity.SourceId);
                    var expected=((JArray)source["current_world_vertices"]).Select(V).ToArray();
                    float error=mesh.vertices.Select(v=>filter.transform.TransformPoint(v)).Max(v=>expected.Min(p=>Vector3.Distance(v,p)));
                    if(error>Tolerance)throw new InvalidDataException("Live source frame differs from packet; refresh reviewed frame: "+error);
                }
                plans.Add(new Plan {Row=row,Button=button,Leaves=leaves,LeafRows=geometry,Use=use});
            }
            if(plans.Count==0)throw new InvalidOperationException("No explicit reviewed opening policy supplied");
            var allIdentityCount=UnityEngine.Object.FindObjectsByType<SourcePartIdentity>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(x=>x.Category!="escalator_tread_component"&&x.Category!="automatic_door_leaf_source_part");
            if(allIdentityCount!=405)throw new InvalidDataException("Original405 source components changed");
            var oldParents=new List<(Transform Node,Transform Parent)>();var wrappers=new List<GameObject>();var installedButtons=new List<SourcePartAutomaticButton>();
            try
            {
                var result=new List<StationDoor>();
                foreach(var plan in plans)
                {
                    var row=plan.Row;var wrapper=new GameObject("원본 자동문 · "+plan.Button.SourceId);wrapper.SetActive(false);wrappers.Add(wrapper);
                    wrapper.transform.SetParent(GameObject.Find("FPSWorld/원본 분리 부품").transform,false);
                    wrapper.transform.SetPositionAndRotation(V(row["opening_bottom_current_world"]),Quaternion.LookRotation(V(row["normal_current_world"]),Vector3.up));
                    var door=wrapper.AddComponent<StationDoor>();door.Element=(string)row["operator_element"];door.Label=(string)row["label"];door.Kind=StationDoor.DoorKind.Sliding;door.Use=plan.Use;
                    door.Width=(float)row["native_opening_width_m"];door.Height=(float)row["native_opening_height_m"];door.Centre=Vector3.zero;
                    door.Normal=wrapper.transform.InverseTransformDirection(V(row["normal_current_world"]));door.Along=wrapper.transform.InverseTransformDirection(V(row["along_current_world"]));door.ObservedOpen=0;
                    var entries=new List<StationDoor.Leaf>();
                    for(int i=0;i<plan.Leaves.Length;i++)
                    {
                        var identity=plan.Leaves[i];var source=plan.LeafRows[i];var pivot=new GameObject("원본 유리 문짝 · "+identity.SourceId).transform;pivot.SetParent(wrapper.transform,false);
                        var sourceMatrix=source["native_world_matrix"];
                        var rotation=packet["source_to_current_world_rotation"];var translation=V(packet["source_to_current_world_translation"]);
                        var nativeOrigin=new Vector3((float)sourceMatrix[0][3],(float)sourceMatrix[1][3],(float)sourceMatrix[2][3]);
                        var origin=new Vector3((float)rotation[0][0]*nativeOrigin.x+(float)rotation[0][1]*nativeOrigin.y+(float)rotation[0][2]*nativeOrigin.z,(float)rotation[1][0]*nativeOrigin.x+(float)rotation[1][1]*nativeOrigin.y+(float)rotation[1][2]*nativeOrigin.z,(float)rotation[2][0]*nativeOrigin.x+(float)rotation[2][1]*nativeOrigin.y+(float)rotation[2][2]*nativeOrigin.z)+translation;
                        pivot.SetPositionAndRotation(origin,wrapper.transform.rotation);
                        oldParents.Add((identity.transform,identity.transform.parent));identity.transform.SetParent(pivot,true);
                        entries.Add(new StationDoor.Leaf {Pivot=pivot,Slide=pivot.InverseTransformVector(V(source["slide_current_world"]))});
                        identity.OperationStatus="exact_native_sliding_leaf_bound";
                    }
                    // Native button belongs to the first moving leaf's original source group. Keep its exact world pose, do not duplicate it.
                    oldParents.Add((plan.Button.transform,plan.Button.transform.parent));plan.Button.transform.SetParent(entries[0].Pivot,true);
                    door.Leaves=entries.ToArray();var control=plan.Button.gameObject.AddComponent<SourcePartAutomaticButton>();installedButtons.Add(control);
                    control.Door=door;control.Plate=plan.Button.GetComponentsInChildren<MeshFilter>(true).Single();control.PressDirectionLocal=Vector3.left; // Actual retained Unity plate: mounting x=0, exposed front x>0. Archive axis differs after import.control.Travel=(float)row["press_travel_m"];
                    plan.Button.OperationStatus="exact_native_push_plate_mapped_to_source_operator";result.Add(door);
                }
                // Entire operator subtree stays inactive until root completes final conservation review. Root may then activate all doors together.
                // Never mark or save the production scene automatically. Root reviews live source frame/collider conservation and runs operation tests first.
                return result.ToArray();
            }
            catch
            {
                foreach(var c in installedButtons)if(c!=null)UnityEngine.Object.DestroyImmediate(c);
                foreach(var p in oldParents.AsEnumerable().Reverse())if(p.Node!=null)p.Node.SetParent(p.Parent,true);
                foreach(var w in wrappers)if(w!=null)UnityEngine.Object.DestroyImmediate(w);
                foreach(var plan in plans){plan.Button.OperationStatus="push_plate_target_unconfirmed";foreach(var leaf in plan.Leaves)leaf.OperationStatus="preserved_source_geometry_operation_not_bound";}
                throw;
            }
        }
    }
}
#endif
