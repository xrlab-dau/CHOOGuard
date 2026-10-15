#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Four source-diagram entrances; rejects missing support/close attachment/complete original hall route.</summary>
    public static class SourcePdfToiletDestinationRegistry
    {
        public const string RegistryPath = "content/world/observed-interiors/floor1-source-pdf-toilet-destinations-20261009.json";
        public const string RegistrySha = "6b7c8d81711d175c265718da2638d1e623931bdeac2d8ca277c799e8e2a454ae";
        const string WalkingPath = "Assets/ChooGuard/Art/Emergency/Resources/StationWalkingRoutes.json";
        const float Radius = .3f, Height = 1.72f, Attach3D = .2f, AttachHorizontal = .15f, AttachVertical = .15f;
        static readonly HashSet<string> Allowed = new HashSet<string>(new[]
        {
            "toilet-1f-source-left-upper", "toilet-1f-source-left-lower",
            "toilet-1f-source-right-upper", "toilet-1f-source-right-lower"
        }, StringComparer.Ordinal);

        sealed class LinkOwner : ScriptableObject { public string sourceId, kind; }
        sealed class LinkReceipt
        {
            public string id, kind; public Vector3 start, end; public bool bidirectional;
            public JObject Json() => new JObject { ["id"] = id, ["kind"] = kind, ["start"] = V(start), ["end"] = V(end), ["bidirectional"] = bidirectional };
        }
        sealed class Graph : IDisposable
        {
            public readonly List<NavMeshLinkInstance> links = new List<NavMeshLinkInstance>();
            public readonly List<LinkOwner> owners = new List<LinkOwner>();
            public readonly List<LinkReceipt> attached = new List<LinkReceipt>();
            public readonly JArray rejected = new JArray();
            NavMeshDataInstance data;
            public Graph(JObject original)
            {
                // A foreign graph/link must not make an original-source entrance appear reachable.
                if (NavMesh.CalculateTriangulation().indices.Length != 0)
                    throw new InvalidOperationException("Probe requires an editor with no preloaded navigation graph; remove the prior runtime/probe context first.");
                var nav = AssetDatabase.LoadAssetAtPath<NavMeshData>(StationNavigationBuilder.NavMeshPath);
                if (nav == null || EditorUtility.IsDirty(nav)) throw new InvalidOperationException("Saved current navmesh required; root must bake/save it first.");
                try
                {
                    data = NavMesh.AddNavMeshData(nav);
                    if (!data.valid) throw new InvalidOperationException("Saved navmesh did not instantiate.");
                    foreach (var e in (JArray)original["escalators"])
                    {
                        var path = (JArray)e["path"];
                        if (path == null || path.Count < 2) throw new InvalidOperationException("Missing original escalator path: " + e["id"]);
                        Add((string)e["id"], "escalator", Vec(path[0]), Vec(path[path.Count - 1]), false, StationNavigationBuilder.EscalatorArea, -1);
                    }
                    foreach (var e in (JArray)original["elevators"])
                    {
                        var stops = (JArray)e["stops"];
                        for (int i = 0; i < stops.Count; i++) for (int j = i + 1; j < stops.Count; j++)
                            Add((string)e["id"] + ":" + i + ":" + j, "elevator", Vec(stops[i]["door"]), Vec(stops[j]["door"]), true, StationNavigationBuilder.ElevatorArea, -1);
                    }
                    foreach (var w in StationWalkingRoutes.Load())
                    {
                        try { VerifyWalk(w); Add(StationWalkingRoutes.LinkPrefix + w.id, "owned_walk", w.Start, w.End, true, 0, w.LinkData().costModifier); }
                        catch (Exception ex) { rejected.Add(new JObject { ["id"] = StationWalkingRoutes.LinkPrefix + w.id, ["reason"] = ex.Message }); }
                    }
                }
                catch { Dispose(); throw; }
            }
            void Add(string id, string kind, Vector3 start, Vector3 end, bool both, int area, float cost)
            {
                // These tests query the original endpoints; no nav sample is substituted into the link.
                if (!CloseNav(start, out _) || !CloseNav(end, out _))
                {
                    rejected.Add(new JObject { ["id"] = id, ["reason"] = "original endpoint lacks close 3D Nav attachment", ["start"] = V(start), ["end"] = V(end) });
                    return;
                }
                var l = NavMesh.AddLink(new NavMeshLinkData { startPosition = start, endPosition = end, width = 0, bidirectional = both, area = area, costModifier = cost, agentTypeID = StationNavigationBuilder.Settings().agentTypeID });
                links.Add(l);
                if (!NavMesh.IsLinkValid(l))
                {
                    rejected.Add(new JObject { ["id"] = id, ["reason"] = "original link rejected by navmesh" }); return;
                }
                var owner = ScriptableObject.CreateInstance<LinkOwner>(); owner.name = id; owner.sourceId = id; owner.kind = kind; owner.hideFlags = HideFlags.HideAndDontSave;
                owners.Add(owner); NavMesh.SetLinkOwner(l, owner);
                attached.Add(new LinkReceipt { id = id, kind = kind, start = start, end = end, bidirectional = both });
            }
            public void Dispose()
            {
                foreach (var l in links) if (NavMesh.IsLinkValid(l)) NavMesh.RemoveLink(l);
                links.Clear(); foreach (var o in owners) if (o != null) Object.DestroyImmediate(o); owners.Clear();
                if (data.valid) data.Remove();
            }
        }

        [MenuItem("ChooGuard/Emergency/Probe source PDF 1F toilet destinations")]
        public static void ProbeMenu() => Debug.Log(Probe().ToString());

        /// <summary>Read-only: no files/scene/assets are changed. Temporary exact-source nav context is removed.</summary>
        public static JObject Probe()
        {
            var registry = ReadRegistry(); var original = ReadPoints(); RequireSavedStation();
            var report = NewReport(original, registry);
            using (var graph = new Graph(original))
            {
                report["attached_original_links"] = new JArray(graph.attached.Select(x => x.Json()));
                report["unattached_original_links"] = graph.rejected;
                if (!CloseNav(StationNavigationBuilder.HallCentre, out var hall))
                    throw new InvalidOperationException("Original HallCentre lacks close 3D navigation attachment.");
                report["hall_anchor"] = V(StationNavigationBuilder.HallCentre); report["hall_attachment"] = V(hall);
                var accepted = (JArray)report["accepted"]; var rejected = (JArray)report["rejected"];
                foreach (var row in (JArray)registry["entries"])
                {
                    try { accepted.Add(Evaluate(row, registry, original, hall, graph.attached)); }
                    catch (Exception ex) { rejected.Add(new JObject { ["point_id"] = (string)row["point_id"], ["reason"] = ex.Message, ["mouth_world"] = row["mouth_world_xyz"].DeepClone() }); }
                }
            }
            report["accepted_count"] = ((JArray)report["accepted"]).Count;
            report["rejected_count"] = ((JArray)report["rejected"]).Count;
            report["status"] = ((JArray)report["rejected"]).Count == 0 ? "ALL_FOUR_REACHABLE" : "BOUNDED_ACCEPTED_WITH_REJECTIONS";
            return report;
        }

        [MenuItem("ChooGuard/Emergency/Apply source PDF 1F toilet destinations")]
        public static void ApplyMenu() => Debug.Log(Apply().ToString());

        /// <summary>Atomic station-points file replacement. Only four stable IDs may change; every other token survives.</summary>
        public static JObject Apply()
        {
            var report = Probe();
            byte[] beforeBytes = File.ReadAllBytes(StationNavigationBuilder.PointsPath);
            if (BytesSha(beforeBytes) != (string)report["points_before_sha256"]) throw new InvalidOperationException("Station points changed during probe.");
            var before = ParsePoints(Encoding.UTF8.GetString(beforeBytes));
            var points = (JArray)before["points"]; var accepted = (JArray)report["accepted"];
            foreach (var reject in (JArray)report["rejected"])
                if (points.Any(p => (string)p["id"] == (string)reject["point_id"]))
                    throw new InvalidOperationException("Existing source destination now fails evidence; refusing stale replay: " + reject["point_id"]);
            var after = SourcePdfToiletPointPatch.BuildCandidate(before, accepted.Select(r => (JObject)r["point"]));
            AssertPreserved(before, after);
            if (accepted.Count == 0) { report["apply_status"] = "NO_ACCEPTED_DESTINATIONS_NO_WRITE"; return report; }
            string path = StationNavigationBuilder.PointsPath;
            string temp = path + ".source-toilets-" + Guid.NewGuid().ToString("N") + ".tmp";
            string backupDir = ".planning/2026-10-09-floor2-completion-pass2/source-toilet-destinations-backups";
            Directory.CreateDirectory(backupDir);
            string backup = Path.Combine(backupDir, "station-points-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N") + ".json");
            bool replaced = false;
            try
            {
                byte[] candidate = new UTF8Encoding(false).GetBytes(after.ToString(Formatting.Indented) + "\n");
                // Vector3 values enter the draft as float JValues; JSON parses them
                // as doubles. Compare saved data to the exact serialized candidate,
                // while retaining the strict complement and order checks.
                var persistedCandidate = ParsePoints(Encoding.UTF8.GetString(candidate));
                AssertPreserved(before, persistedCandidate);
                File.WriteAllBytes(temp, candidate);
                AssertPreserved(before, ParsePoints(File.ReadAllText(temp)));
                if (Sha(path) != BytesSha(beforeBytes) || Sha(StationNavigationBuilder.NavMeshPath) != (string)report["navmesh_sha256"] || Sha(RegistryPath) != RegistrySha || Sha(WalkingPath) != (string)report["owned_walk_registry_sha256"])
                    throw new InvalidOperationException("Source points/nav/registry changed before atomic commit.");
                File.Replace(temp, path, backup, true); replaced = true;
                var saved = ReadPoints(); AssertPreserved(before, saved);
                if (!JToken.DeepEquals(saved, persistedCandidate)) throw new InvalidOperationException("Saved points candidate differs.");
                AssetDatabase.ImportAsset(path);
                report["apply_status"] = "APPLIED_BOUNDED_SOURCE_DESTINATIONS";
                report["points_after_sha256"] = Sha(path); report["backup"] = backup;
                report["preserved_non_source_data_sha256"] = BytesSha(Encoding.UTF8.GetBytes(Complement(saved).ToString(Formatting.None)));
                report["all_other_fields_and_list_order_preserved"] = true;
                return report;
            }
            catch
            {
                if (replaced)
                {
                    string rollback = path + ".source-toilets-rollback-" + Guid.NewGuid().ToString("N") + ".tmp";
                    File.WriteAllBytes(rollback, beforeBytes); File.Replace(rollback, path, null, true);
                    if (Sha(path) != BytesSha(beforeBytes)) throw new InvalidOperationException("Rollback bytes mismatch; preserve backup and stop.");
                    AssetDatabase.ImportAsset(path);
                }
                throw;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        /// <summary>Called while BuildPoints owns its nav/link context. Original transport endpoints must match exactly.</summary>
        public static void AppendForBuild(List<StationPoints.PointEntry> into, List<string> rejected,
            Func<Vector3, string> zoneAt, List<StationPoints.EscalatorEntry> currentEscalators, List<StationPoints.ElevatorEntry> currentElevators)
        {
            if (!File.Exists(RegistryPath)) return;
            var registry = ReadRegistry(); var original = ReadPoints(); RequireSavedStation();
            // A full point rebuild may re-survey transports. This hook never adopts changed source endpoints.
            RequireOriginalTransportEndpoints(original, currentEscalators, currentElevators);
            foreach (var walk in VerifiedWalks()) if (!CloseNav(walk.Start, out _) || !CloseNav(walk.End, out _))
                throw new InvalidOperationException("Original owned walking endpoint no longer attaches: " + walk.id);
            if (!CloseNav(StationNavigationBuilder.HallCentre, out var hall)) throw new InvalidOperationException("Original HallCentre no longer attaches.");
            foreach (var row in (JArray)registry["entries"])
            {
                try
                {
                    var receipt = Evaluate(row, registry, original, hall, null);
                    var point = receipt["point"].ToObject<StationPoints.PointEntry>();
                    if (zoneAt(point.position) != point.zone) throw new InvalidOperationException("Current zone assignment differs from preserved source zones.");
                    if (into.Any(p => p.id == point.id)) throw new InvalidOperationException("Duplicate stable source point ID.");
                    into.Add(point);
                }
                catch (Exception ex) { rejected.Add((string)row["point_id"] + ": " + ex.Message); }
            }
        }

        static JObject Evaluate(JToken row, JObject registry, JObject original, Vector3 hall, List<LinkReceipt> links)
        {
            ValidateGeometry(row, registry);
            var sourceRoot = GameObject.Find((string)registry["installed_root"]).transform;
            var mouth = sourceRoot.TransformPoint(Vec(row["mouth_root_xyz"]));
            RequireNear(mouth, Vec(row["mouth_world_xyz"]), .001f, "Saved source mouth pose changed.");
            var sourceFloor = Footprint(mouth, (float)row["spec_row"]["geometry"]["y"]);
            if (!CloseNav(mouth, out var attach)) throw new InvalidOperationException("No close 3D nav attachment at source mouth; rejected without relocation.");
            var connector = new JArray();
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(mouth, attach) / .05f));
            for (int i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(mouth, attach, i / (float)n);
                connector.Add(new JObject { ["position"] = V(p), ["floor"] = Footprint(p, mouth.y) });
                if (Physics.CheckCapsule(p + Vector3.up * (Radius + .02f), p + Vector3.up * (Height - Radius + .02f), Radius, ~0, QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("Full .3m/1.72m capsule blocked at source mouth/close connector; no invented passage.");
            }
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(hall, attach, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2)
                throw new InvalidOperationException("Complete hall route absent using original escalator/elevator/owned walking links.");
            RequireNear(path.corners[0], hall, .03f, "Hall route start mismatch.");
            RequireNear(path.corners[path.corners.Length - 1], attach, .03f, "Hall route finish mismatch.");
            var used = new JArray();
            if (links != null)
                for (int i = 1; i < path.corners.Length; i++)
                    foreach (var l in links)
                        if (Near(path.corners[i - 1], l.start) && Near(path.corners[i], l.end) || l.bidirectional && Near(path.corners[i - 1], l.end) && Near(path.corners[i], l.start))
                            used.Add(l.Json());
            string zone = ZoneAt(original, attach);
            if (string.IsNullOrEmpty(zone)) throw new InvalidOperationException("Source mouth outside preserved zones; no zone change allowed.");
            var inward = sourceRoot.TransformDirection(Vec(row["inward_root_xyz"]));
            var point = new JObject
            {
                ["id"] = (string)row["point_id"], ["kind"] = "Toilet", ["label"] = (string)row["label"],
                ["zone"] = zone, ["slot"] = (string)row["point_id"], ["position"] = V(attach),
                ["anchor"] = V(mouth), ["yaw"] = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg
            };
            return new JObject
            {
                ["point_id"] = (string)row["point_id"], ["point"] = point,
                ["mouth_world"] = V(mouth), ["nav_attach_world"] = V(attach), ["attachment_3d_distance"] = Vector3.Distance(mouth, attach),
                ["source_mouth_footprint"] = sourceFloor, ["attachment_connector"] = connector,
                ["hall_path_status"] = path.status.ToString(), ["hall_route_corners"] = new JArray(path.corners.Select(V)),
                ["original_owned_links_used"] = used, ["structural_floor_under_finish"] = (string)row["spec_row"]["params"]["structuralFloorUnderFinish"],
                ["field_dimensions_verified"] = false
            };
        }

        static JArray Footprint(Vector3 bottom, float floorY)
        {
            var result = new JArray();
            for (int i = -1; i < 16; i++)
            {
                var offset = i < 0 ? Vector3.zero : new Vector3(Mathf.Cos(i * Mathf.PI / 8), 0, Mathf.Sin(i * Mathf.PI / 8)) * Radius;
                var hits = Physics.RaycastAll(bottom + offset + Vector3.up * .75f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => !(h.collider is CharacterController) && h.collider.GetComponentInParent<PersonBody>() == null).OrderBy(h => h.distance).ToArray();
                if (hits.Length == 0 || hits[0].normal.y < .9f || Mathf.Abs(hits[0].point.y - floorY) > .08f)
                    throw new InvalidOperationException("Exposed existing floor fails full-radius footprint sample " + i + " at " + bottom);
                var h = hits[0];
                if (h.collider.gameObject.scene.path != EmergencySceneBuilder.StationScenePath || EditorUtility.IsDirty(h.collider))
                    throw new InvalidOperationException("Floor authority is not an unchanged saved station collider.");
                var receipt = new JObject { ["offset"] = V(offset), ["position"] = V(h.point), ["owner"] = PathOf(h.collider.transform), ["global_object_id"] = GlobalObjectId.GetGlobalObjectIdSlow(h.collider).ToString(), ["triangle"] = h.triangleIndex };
                if (h.collider is MeshCollider mc)
                {
                    string p = AssetDatabase.GetAssetPath(mc.sharedMesh);
                    if (string.IsNullOrEmpty(p) || EditorUtility.IsDirty(mc.sharedMesh)) throw new InvalidOperationException("Unsaved floor mesh cannot certify mouth.");
                    receipt["mesh_asset"] = p; receipt["mesh_asset_sha256"] = Sha(p); receipt["mesh_guid"] = AssetDatabase.AssetPathToGUID(p);
                }
                result.Add(receipt);
            }
            return result;
        }

        static void ValidateGeometry(JToken row, JObject registry)
        {
            var spec = JObject.Parse(File.ReadAllText((string)registry["source_spec"]));
            var actual = ((JArray)spec["elements"]).SingleOrDefault(e => (string)e["type"] == "SourceDiagramRoom" && (string)e["id"] == (string)row["room_id"]);
            if (actual == null || !JToken.DeepEquals(actual, row["spec_row"])) throw new InvalidOperationException("Exact source room row changed or missing.");
            var root = GameObject.Find((string)registry["installed_root"]);
            if (root == null || !root.activeInHierarchy) throw new InvalidOperationException("Installed source-PDF room root absent/inactive.");
            MatrixMatches(root.transform.localToWorldMatrix, registry["installed_root_world_matrix"]);
            if (Sha((string)registry["source_registration"]) != (string)registry["source_registration_sha256"]) throw new InvalidOperationException("Source registration changed.");
            foreach (var dep in row["installed_geometry"])
            {
                var go = GameObject.Find((string)dep["path"]);
                if (go == null || !go.activeInHierarchy) throw new InvalidOperationException("Installed source geometry missing/inactive: " + dep["path"]);
                MatrixMatches(go.transform.localToWorldMatrix, dep["world_matrix"]);
                Mesh m; Component component;
                if ((string)dep["component"] == "MeshFilter") { var c = go.GetComponent<MeshFilter>(); if (c == null) throw new InvalidOperationException("Source MeshFilter missing."); component = c; m = c.sharedMesh; var r = go.GetComponent<Renderer>(); if (r == null || !r.enabled) throw new InvalidOperationException("Source renderer inactive."); }
                else { var c = go.GetComponent<MeshCollider>(); if (c == null || !c.enabled || c.isTrigger) throw new InvalidOperationException("Source collider inactive."); component = c; m = c.sharedMesh; }
                string asset = (string)dep["asset"];
                if (m == null || AssetDatabase.GetAssetPath(m) != asset || AssetDatabase.AssetPathToGUID(asset) != (string)dep["asset_guid"] || Sha(asset) != (string)dep["asset_sha256"] || Sha(asset + ".meta") != (string)dep["asset_meta_sha256"] || EditorUtility.IsDirty(m) || EditorUtility.IsDirty(component) || ArchitecturalCorrectionRegistry.CompleteChannelDigest(m) != (string)dep["complete_channel_digest"])
                    throw new InvalidOperationException("Actual saved source geometry identity/channels changed: " + dep["path"]);
            }
        }
        static void VerifyWalk(StationWalkingRouteEntry r)
        {
            var raw = JObject.Parse(File.ReadAllText(WalkingPath));
            var row = ((JArray)raw["routes"]).Single(x => (string)x["id"] == r.id);
            if ((bool?)row["physicalVerified"] != true || !File.Exists(r.proof) || Sha(r.proof) != (string)row["proof_sha256"])
                throw new InvalidOperationException("Owned walking proof missing/changed: " + r.id);
            foreach (var p in r.path)
            {
                if (!NativeLandingWalker.Floor(p, r, null, out _))
                    throw new InvalidOperationException("Owned walk current runtime floor predicate rejects its source path: " + r.id);
            }
        }
        static StationWalkingRouteEntry[] VerifiedWalks()
        {
            var routes = StationWalkingRoutes.Load(); foreach (var r in routes) VerifyWalk(r); return routes;
        }
        static bool CloseNav(Vector3 source, out Vector3 position)
        {
            position = default;
            if (!Finite(source) || !NavMesh.SamplePosition(source, out var hit, Attach3D, NavMesh.AllAreas)) return false;
            var d = hit.position - source; var horizontal = new Vector2(d.x, d.z).magnitude;
            if (d.magnitude > Attach3D || horizontal > AttachHorizontal || Mathf.Abs(d.y) > AttachVertical) return false;
            position = hit.position; return true;
        }
        static void RequireOriginalTransportEndpoints(JObject source, List<StationPoints.EscalatorEntry> escalators, List<StationPoints.ElevatorEntry> elevators)
        {
            if (((JArray)source["escalators"]).Count != escalators.Count || ((JArray)source["elevators"]).Count != elevators.Count)
                throw new InvalidOperationException("Transport source set changed; re-probe in original endpoint context before rebuild.");
            foreach (var e in (JArray)source["escalators"])
            {
                var match = escalators.Single(x => x.id == (string)e["id"]); var path = (JArray)e["path"];
                if (match.path.Length != path.Count || match.up != (bool)e["up"] || match.from != (string)e["from"] || match.to != (string)e["to"])
                    throw new InvalidOperationException("Original escalator route changed: " + match.id);
                for (int i = 0; i < path.Count; i++) RequireNear(match.path[i], Vec(path[i]), .0001f, "Original escalator route changed: " + match.id);
            }
            foreach (var e in (JArray)source["elevators"])
            {
                var match = elevators.Single(x => x.id == (string)e["id"]); var stops = (JArray)e["stops"];
                if (stops.Count != match.stops.Length) throw new InvalidOperationException("Original elevator stops changed.");
                for (int i = 0; i < stops.Count; i++) { if (match.stops[i].floor != (string)stops[i]["floor"]) throw new InvalidOperationException("Original elevator floor changed."); RequireNear(match.stops[i].door, Vec(stops[i]["door"]), .0001f, "Original elevator endpoint changed."); }
            }
        }
        static JObject ReadRegistry()
        {
            if (Sha(RegistryPath) != RegistrySha) throw new InvalidOperationException("Source toilet registry changed; strict requalification required.");
            var j = JObject.Parse(File.ReadAllText(RegistryPath));
            if ((string)j["schema"] != "chooguard.source-pdf-toilet-destinations.v1" || ((JArray)j["entries"]).Count != 4 || !new HashSet<string>(((JArray)j["entries"]).Select(e => (string)e["point_id"])).SetEquals(Allowed))
                throw new InvalidOperationException("Exactly four bounded source destinations required.");
            return j;
        }
        static JObject ReadPoints() => ParsePoints(File.ReadAllText(StationNavigationBuilder.PointsPath));
        static JObject ParsePoints(string json)
        {
            var j = JObject.Parse(json);
            if ((int?)j["version"] != 2 || !(j["points"] is JArray) || !(j["zones"] is JArray) || !(j["escalators"] is JArray) || !(j["elevators"] is JArray)) throw new InvalidOperationException("Station-points contract absent.");
            var ids = ((JArray)j["points"]).Select(x => (string)x["id"]).ToArray();
            if (ids.Any(string.IsNullOrEmpty) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new InvalidOperationException("Duplicate/missing station-point IDs.");
            return j;
        }
        static JObject NewReport(JObject original, JObject registry) => new JObject
        {
            ["schema"] = "chooguard.source-pdf-toilet-destination-probe.v1", ["utc"] = DateTime.UtcNow.ToString("o"),
            ["points_before_sha256"] = Sha(StationNavigationBuilder.PointsPath), ["navmesh_sha256"] = Sha(StationNavigationBuilder.NavMeshPath),
            ["registry_sha256"] = RegistrySha, ["source_scene_epoch_sha256"] = (string)registry["source_scene_epoch_sha256"],
            ["actual_saved_scene_sha256"] = Sha(EmergencySceneBuilder.StationScenePath), ["owned_walk_registry_sha256"] = Sha(WalkingPath),
            ["accepted"] = new JArray(), ["rejected"] = new JArray(), ["new_floor_created"] = false,
            ["source_walls_or_axes_changed"] = false, ["original_endpoints_or_zones_changed"] = false, ["probe_writes_files"] = false,
            ["field_dimensions_verified"] = false
        };
        static JObject Complement(JObject j) => SourcePdfToiletPointPatch.Complement(j);
        public static void AssertPreserved(JObject before, JObject after) => SourcePdfToiletPointPatch.AssertPreserved(before, after);
        static void RequireSavedStation()
        {
            var scene = SceneManager.GetSceneByPath(EmergencySceneBuilder.StationScenePath);
            if (!scene.IsValid() || !scene.isLoaded || scene.isDirty || EditorApplication.isPlaying) throw new InvalidOperationException("Open the actual saved station in EditMode before source toilet probe.");
        }
        static string ZoneAt(JObject data, Vector3 p)
        {
            JToken best = null;
            foreach (var z in (JArray)data["zones"])
            {
                var lo = Vec(z["min"]); var hi = Vec(z["max"]);
                if (p.x < lo.x || p.y < lo.y || p.z < lo.z || p.x > hi.x || p.y > hi.y || p.z > hi.z) continue;
                if (best == null || (int)z["priority"] > (int)best["priority"]) best = z;
            }
            return (string)best?["id"] ?? "";
        }
        static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .2f;
        static void RequireNear(Vector3 a, Vector3 b, float tolerance, string error) { if (!Finite(a) || !Finite(b) || Vector3.Distance(a, b) > tolerance) throw new InvalidOperationException(error); }
        static bool Finite(Vector3 p) => !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));
        static Vector3 Vec(JToken t) => t is JArray ? new Vector3((float)t[0], (float)t[1], (float)t[2]) : new Vector3((float)t["x"], (float)t["y"], (float)t["z"]);
        static JObject V(Vector3 p) => new JObject { ["x"] = p.x, ["y"] = p.y, ["z"] = p.z };
        static string PathOf(Transform t) { var names = new List<string>(); while (t != null) { names.Add(t.name); t = t.parent; } names.Reverse(); return string.Join("/", names); }
        static void MatrixMatches(Matrix4x4 m, JToken rows) { for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) { float expected = (float)rows[r][c]; if (float.IsNaN(expected) || float.IsInfinity(expected) || float.IsNaN(m[r,c]) || float.IsInfinity(m[r,c]) || Mathf.Abs(m[r,c] - expected) > .0005f) throw new InvalidOperationException("Saved source geometry pose changed."); } }
        static string Sha(string path) { using (var h = SHA256.Create()) using (var s = File.OpenRead(path)) return Hex(h.ComputeHash(s)); }
        static string BytesSha(byte[] b) { using (var h = SHA256.Create()) return Hex(h.ComputeHash(b)); }
        static string Hex(byte[] b) => BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();
    }
}
#endif
