using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Equipment;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Twin;
using ChooGuard.Circulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Coverage audit of actual scene bindings. Mesh fragments are not counted as complete real-world objects.</summary>
    public static class TwinSceneAudit
    {
        [Serializable] public sealed class Record
        {
            public string scene_id, scene, hierarchy, display_name, source_asset_guid;
            public Vector3 position, rotation;
            public string[] components, interactions;
            public int mesh_vertices, mesh_submeshes;
            public bool has_collider, independently_verified;
            public string issue;
        }
        [Serializable] public sealed class Report
        {
            public string schema = "chooguard.scene-coverage.v1", generated_at;
            public bool field_accuracy_verified;
            public string count_semantics = "scene bindings and mesh fragments; not a physical object completeness claim";
            public Record[] records;
            public int interaction_bindings, mesh_fragments, elevator_landings, unbound_elevator_landings, verified_equipment;
            public int ride_volumes, mapped_ride_volumes, motion_bindings, animated_parts, animated_surfaces, authored_escalator_lanes;
        }

        public static string Export(string path)
        {
            var report = new Report { generated_at = DateTimeOffset.UtcNow.ToString("o") };
            var pointsAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ChooGuard/Art/Emergency/station-points.json");
            var lanes = pointsAsset != null ? new HashSet<string>(StationPoints.Load(pointsAsset).Escalators.Select(e => e.id), StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
            report.authored_escalator_lanes = lanes.Count;
            var records = new List<Record>();
            foreach (var component in Resources.FindObjectsOfTypeAll<Transform>())
            {
                var go = component.gameObject;
                if (!go.scene.IsValid() || !go.scene.isLoaded || EditorUtility.IsPersistent(go)) continue;
                var components = go.GetComponents<Component>().Where(c => c != null).ToArray();
                var interactions = components.Where(c => c is IFpsInteraction).Select(c => c.GetType().FullName).ToArray();
                var filter = go.GetComponent<MeshFilter>();
                var door = go.GetComponent<StationDoor>();
                var equipment = go.GetComponent<StationEquipment>();
                var ride = go.GetComponent<EscalatorRide>();
                var motion = go.GetComponent<EscalatorMotion>();
                if (interactions.Length == 0 && filter == null && equipment == null && ride == null && motion == null) continue;
                string assetGuid = "";
                if (filter != null && filter.sharedMesh != null) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(filter.sharedMesh, out assetGuid, out long _);
                var record = new Record
                {
                    scene_id = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString(), scene = go.scene.path,
                    hierarchy = PathOf(component), display_name = go.name, source_asset_guid = assetGuid,
                    position = component.position, rotation = component.eulerAngles,
                    components = components.Select(c => c.GetType().FullName).ToArray(), interactions = interactions,
                    mesh_vertices = filter != null && filter.sharedMesh != null ? filter.sharedMesh.vertexCount : 0,
                    mesh_submeshes = filter != null && filter.sharedMesh != null ? filter.sharedMesh.subMeshCount : 0,
                    has_collider = go.GetComponent<Collider>() != null,
                    independently_verified = false,
                    issue = "no_independent_site_verification"
                };
                if (equipment != null)
                {
                    var item = new EquipmentPlacement { id = equipment.Id, position = component.position, rotation = component.eulerAngles, verification = equipment.Verification };
                    record.independently_verified = MeasuredTwinImport.VerifyAgainstManifest(item, out _) &&
                        equipment.Verification.ValidateModelDimensions(go, out _);
                    record.issue = record.independently_verified ? "" : "equipment_requires_structured_placement_record";
                    if (record.independently_verified) report.verified_equipment++;
                }
                if (interactions.Length > 0) report.interaction_bindings++;
                if (filter != null) report.mesh_fragments++;
                if (ride != null)
                {
                    report.ride_volumes++;
                    var binding = ride.GetComponentInParent<EscalatorDriveBinding>();
                    if (binding != null && lanes.Contains(binding.lane_id)) report.mapped_ride_volumes++;
                    else record.issue = "missing_or_unknown_explicit_lane_binding";
                }
                if (motion != null)
                {
                    motion.RebuildBindings();
                    report.motion_bindings++;
                    report.animated_parts += motion.AnimatedPartCount;
                    report.animated_surfaces += motion.AnimatedSurfaceCount;
                }
                if (door != null && door.Kind == StationDoor.DoorKind.Elevator)
                {
                    report.elevator_landings++;
                    if (door.Cabin == null || string.IsNullOrWhiteSpace(door.CarId)) { report.unbound_elevator_landings++; record.issue = "missing_cabin_or_car_mapping"; }
                }
                records.Add(record);
            }
            report.records = records.OrderBy(r => r.scene, StringComparer.Ordinal).ThenBy(r => r.hierarchy, StringComparer.Ordinal).ToArray();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));
            File.WriteAllText(path, JsonUtility.ToJson(report, true) + "\n");
            return "CG_TWIN_AUDIT records=" + records.Count + " interactions=" + report.interaction_bindings + " elevator_landings=" + report.elevator_landings + " unbound_landings=" + report.unbound_elevator_landings;
        }

        private static string PathOf(Transform transform)
        {
            var names = new Stack<string>();
            for (var t = transform; t != null; t = t.parent) names.Push(t.name + "[" + t.GetSiblingIndex() + "]");
            return string.Join("/", names);
        }
    }
}
