using System;
using System.Collections.Generic;
using ChooGuard.App.Mvp;
using UnityEngine;

namespace ChooGuard.App.Fps.Runtime
{
    [Flags]
    public enum NavigationAccess { Walk = 1, StepFree = 2, Assisted = 4, All = Walk | StepFree | Assisted }
    public enum NavigationPortalKind { Doorway, Stairs, Ramp, LiftLanding, TrainBoarding }

    [Serializable]
    public sealed class FloorNavigationBinding
    {
        public string FloorId;
        public string GeometryRevision;
        public TextAsset NavData;
        public Transform Frame;
        public NavigationAccess Access = NavigationAccess.All;
        public float HorizontalTolerance = .12f;
        public float VerticalTolerance = .3f;
    }

    [Serializable]
    public sealed class PortalNavigationBinding
    {
        public string PortalId;
        public string FromFloorId;
        public string ToFloorId;
        public string GeometryRevision;
        public TextAsset NavData;
        public Transform Frame;
        public Vector3 Entry;
        public Vector3 Exit;
        public NavigationPortalKind Kind;
        public NavigationAccess Access = NavigationAccess.Walk;
        public bool Bidirectional = true;
        public bool Available = true;
    }

    [Serializable]
    public sealed class DoorNavigationBinding
    {
        public string DoorId;
        public string SurfaceId;
        public string GeometryRevision;
        public Collider Threshold;
        public bool IsOpen;
    }

    [DisallowMultipleComponent]
    public sealed class GameplayNavigation : MonoBehaviour
    {
        private sealed class Surface
        {
            public string Id;
            public DetourNavigationSurface Mesh;
            public NavigationAccess Access;
            public Portal Portal;
        }
        private sealed class Portal
        {
            public Surface Surface;
            public Surface From;
            public Surface To;
            public Vector3 Entry;
            public Vector3 Exit;
            public bool Available;
            public bool Bidirectional;
        }
        private sealed class Door
        {
            public Surface Surface;
            public long[] Polygons;
            public bool IsOpen;
        }
        private struct Node
        {
            public Surface Primary;
            public Surface Connector;
            public Vector3 Position;
        }

        private readonly List<Surface> surfaces = new List<Surface>();
        private readonly Dictionary<string, Surface> byId = new Dictionary<string, Surface>(StringComparer.Ordinal);
        private readonly Dictionary<string, Door> doors = new Dictionary<string, Door>(StringComparer.Ordinal);
        private readonly List<Portal> portals = new List<Portal>();
        private readonly List<Node> nodes = new List<Node>();
        private readonly List<Vector3> segment = new List<Vector3>(128);
        private readonly List<int> chain = new List<int>();
        private readonly Dictionary<string, ActorNavigationBinding> actorIds = new Dictionary<string, ActorNavigationBinding>(StringComparer.Ordinal);
        private readonly Dictionary<Vector2Int, List<ActorNavigationBinding>> neighbors = new Dictionary<Vector2Int, List<ActorNavigationBinding>>();
        private readonly Dictionary<ActorNavigationBinding, Vector2Int> actorCells = new Dictionary<ActorNavigationBinding, Vector2Int>();
        private const float NeighborCellSize = 2.5f;
        private float[] costs = Array.Empty<float>();
        private int[] previous = Array.Empty<int>();
        private bool[] visited = Array.Empty<bool>();

        public CharacterController PlayerObstacle;
        public string GeometryRevision { get; private set; } = "";
        public int RouteRevision { get; private set; }
        private string status = "navigation_not_bound";
        public string Status
        {
            get => FloorCount > 0 && !IsReady ? "geometry_frame_changed_rebind_required" : status;
            private set => status = value;
        }
        public int FloorCount { get; private set; }
        public int PortalCount => portals.Count;
        public int ActorCount => actorIds.Count;
        public bool IsReady
        {
            get
            {
                if (FloorCount == 0) return false;
                foreach (var surface in surfaces) if (!surface.Mesh.IsCurrent) return false;
                return true;
            }
        }

        // Call only with the actual geometry version; existing routes are invalidated, identities are not.
        public void BeginGeometry(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision)) throw new ArgumentException("Geometry revision is required", nameof(revision));
            GeometryRevision = revision;
            surfaces.Clear(); byId.Clear(); portals.Clear(); doors.Clear(); nodes.Clear();
            FloorCount = 0;
            RouteRevision++;
            Status = "navigation_not_bound";
        }

        public bool RegisterExistingMvp(MvpTeamNavigation source, out string reason)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.GeometryDigest))
                return Fail("geometry_revision_missing", out reason);
            if (string.IsNullOrEmpty(GeometryRevision)) BeginGeometry(source.GeometryDigest);
            return RegisterFloor(new FloorNavigationBinding
            {
                FloorId = "station-2f", GeometryRevision = source.GeometryDigest,
                NavData = source.NavData, Frame = source.transform
            }, out reason);
        }

        public bool RegisterFloor(FloorNavigationBinding binding, out string reason) =>
            RegisterFloor(binding, binding?.NavData == null ? null : binding.NavData.bytes, out reason);

        public bool RegisterFloor(FloorNavigationBinding binding, byte[] bakedNavData, out string reason)
        {
            if (binding == null) return Fail("floor_binding_missing", out reason);
            if (!ValidateIdentity(binding.FloorId, binding.GeometryRevision, out reason)) return false;
            if (!DetourNavigationSurface.TryLoad(binding.FloorId, bakedNavData,
                binding.Frame, binding.HorizontalTolerance, binding.VerticalTolerance, out var mesh, out reason))
                return Fail(reason, out reason);
            var surface = new Surface { Id = binding.FloorId, Mesh = mesh, Access = binding.Access };
            surfaces.Add(surface); byId.Add(surface.Id, surface); FloorCount++;
            return Changed(out reason);
        }

        // Independent heightfield/detail bakes may differ by one height cell each.
        // Keep horizontal coincidence strict; this is not permission to bridge a gap.
        private static bool ContinuousEndpoint(Vector3 floor, Vector3 connector) =>
            new Vector2(floor.x - connector.x, floor.z - connector.z).sqrMagnitude <= .0004f &&
            Mathf.Abs(floor.y - connector.y) <= 2 * GameplayNavigationBaker.CellHeight + .0001f;

        public bool RegisterPortal(PortalNavigationBinding binding, out string reason) =>
            RegisterPortal(binding, binding?.NavData == null ? null : binding.NavData.bytes, out reason);

        public bool RegisterPortal(PortalNavigationBinding binding, byte[] bakedNavData, out string reason)
        {
            if (binding == null) return Fail("portal_binding_missing", out reason);
            if (!ValidateIdentity(binding.PortalId, binding.GeometryRevision, out reason)) return false;
            if (!byId.TryGetValue(binding.FromFloorId ?? "", out var from) || from.Portal != null ||
                !byId.TryGetValue(binding.ToFloorId ?? "", out var to) || to.Portal != null)
                return Fail("portal_floor_not_bound", out reason);
            if (!DetourNavigationSurface.TryLoad(binding.PortalId, bakedNavData,
                binding.Frame, .12f, .3f, out var mesh, out reason)) return Fail(reason, out reason);
            if (!from.Mesh.TryProject(binding.Entry, out var entry) || !to.Mesh.TryProject(binding.Exit, out var exit) ||
                !mesh.TryProject(entry, out var connectorEntry) || !mesh.TryProject(exit, out var connectorExit) ||
                !ContinuousEndpoint(entry, connectorEntry) || !ContinuousEndpoint(exit, connectorExit))
                return Fail("portal_endpoints_not_continuous_with_floor_geometry", out reason);
            if (!mesh.TryRoute(entry, exit, segment, out reason)) return Fail("portal_" + reason, out reason);
            // A lift landing is a boarding surface, not authority to jump between landings.
            if (binding.Kind == NavigationPortalKind.LiftLanding && Mathf.Abs(entry.y - exit.y) > .3f)
                return Fail("lift_requires_observed_platform_transport", out reason);
            var access = binding.Kind == NavigationPortalKind.Stairs ? binding.Access & ~NavigationAccess.StepFree : binding.Access;
            var surface = new Surface { Id = binding.PortalId, Mesh = mesh, Access = access };
            var portal = new Portal { Surface = surface, From = from, To = to, Entry = entry, Exit = exit,
                Available = binding.Available, Bidirectional = binding.Bidirectional };
            surface.Portal = portal;
            surfaces.Add(surface); byId.Add(surface.Id, surface); portals.Add(portal);
            return Changed(out reason);
        }

        // Threshold is a collider from the real doorway. The bake must contain its open-state walkable surface.
        // Conservative polygon blocking may close a larger area until the doorway bake is sufficiently subdivided.
        public bool RegisterDoor(DoorNavigationBinding binding, out string reason)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.DoorId) || binding.Threshold == null)
                return Fail("door_geometry_binding_missing", out reason);
            if (binding.GeometryRevision != GeometryRevision || string.IsNullOrEmpty(GeometryRevision))
                return Fail("geometry_revision_mismatch", out reason);
            if (doors.ContainsKey(binding.DoorId)) return Fail("duplicate_door_id", out reason);
            if (!byId.TryGetValue(binding.SurfaceId ?? "", out var surface)) return Fail("door_surface_not_bound", out reason);
            var bounds = binding.Threshold.bounds;
            if (bounds.size.sqrMagnitude < .000001f) return Fail("door_threshold_has_no_active_geometry", out reason);
            if (!surface.Mesh.TryDoorPolygons(bounds, out var refs, out reason)) return Fail(reason, out reason);
            var door = new Door { Surface = surface, Polygons = refs, IsOpen = binding.IsOpen };
            doors.Add(binding.DoorId, door);
            if (!door.IsOpen) surface.Mesh.Block(refs, true);
            return Changed(out reason);
        }

        public bool SetDoorState(string doorId, bool isOpen, out string reason)
        {
            if (!doors.TryGetValue(doorId ?? "", out var door)) return Fail("door_not_bound", out reason);
            if (!door.Surface.Mesh.IsCurrent) return Fail("geometry_frame_changed_rebind_required", out reason);
            if (door.IsOpen != isOpen)
            {
                door.Surface.Mesh.Block(door.Polygons, !isOpen);
                door.IsOpen = isOpen;
                return Changed(out reason);
            }
            reason = "ready"; return true;
        }

        // Train boarding/landing availability follows actual docking/door state; never an elapsed-time teleport.
        public bool SetPortalAvailable(string portalId, bool available, out string reason)
        {
            if (!byId.TryGetValue(portalId ?? "", out var surface) || surface.Portal == null)
                return Fail("portal_not_bound", out reason);
            if (!surface.Mesh.IsCurrent) return Fail("geometry_frame_changed_rebind_required", out reason);
            if (surface.Portal.Available != available)
            { surface.Portal.Available = available; return Changed(out reason); }
            reason = "ready"; return true;
        }

        public bool TryRoute(Vector3 start, Vector3 target, List<Vector3> output, out string reason) =>
            TryRoute(start, target, NavigationAccess.Walk, output, out reason);

        public bool TryRoute(Vector3 start, Vector3 target, NavigationAccess requiredAccess, List<Vector3> output, out string reason)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            if (!IsReady) return Fail(FloorCount == 0 ? "navigation_not_bound" : "geometry_frame_changed_rebind_required", out reason);
            if (!DetourNavigationSurface.Finite(start) || !DetourNavigationSurface.Finite(target)) return Fail("non_finite_endpoint", out reason);
            if (!Resolve(start, requiredAccess, out var a) || !Resolve(target, requiredAccess, out var b))
                return Fail("unsupported_floor_or_endpoint_not_walkable", out reason);
            nodes.Clear();
            nodes.Add(new Node { Primary = a, Position = start });
            nodes.Add(new Node { Primary = b, Position = target });
            foreach (var portal in portals)
            {
                if (!Permits(portal.Surface, requiredAccess)) continue;
                nodes.Add(new Node { Primary = portal.From, Connector = portal.Surface, Position = portal.Entry });
                nodes.Add(new Node { Primary = portal.To, Connector = portal.Surface, Position = portal.Exit });
            }
            EnsureCapacity(nodes.Count);
            for (var i = 0; i < nodes.Count; i++) { costs[i] = float.PositiveInfinity; previous[i] = -1; visited[i] = false; }
            costs[0] = 0;
            for (var step = 0; step < nodes.Count; step++)
            {
                var current = -1;
                for (var i = 0; i < nodes.Count; i++)
                    if (!visited[i] && !float.IsPositiveInfinity(costs[i]) && (current < 0 || costs[i] < costs[current])) current = i;
                if (current < 0 || current == 1) break;
                visited[current] = true;
                for (var next = 0; next < nodes.Count; next++)
                {
                    if (visited[next] || next == current || !TryEdge(nodes[current], nodes[next], requiredAccess)) continue;
                    var length = 0f;
                    for (var c = 1; c < segment.Count; c++) length += Vector3.Distance(segment[c - 1], segment[c]);
                    if (costs[current] + length >= costs[next]) continue;
                    costs[next] = costs[current] + length; previous[next] = current;
                }
            }
            if (previous[1] < 0) return Fail("unreachable_or_partial_path", out reason);
            chain.Clear();
            for (var i = 1; i >= 0; i = previous[i]) chain.Add(i);
            for (var i = chain.Count - 1; i > 0; i--)
            {
                if (!TryEdge(nodes[chain[i]], nodes[chain[i - 1]], requiredAccess))
                { output.Clear(); return Fail("route_changed_during_query", out reason); }
                foreach (var point in segment) DetourNavigationSurface.AddDistinct(output, point);
            }
            Status = reason = "ready";
            return true;
        }

        private bool TryEdge(Node a, Node b, NavigationAccess access)
        {
            if (a.Primary == b.Primary && TryEdgeOn(a.Primary, a.Position, b.Position, access)) return true;
            if (a.Connector != null && a.Connector == b.Connector && TryEdgeOn(a.Connector, a.Position, b.Position, access)) return true;
            if (a.Primary == b.Connector && TryEdgeOn(a.Primary, a.Position, b.Position, access)) return true;
            return a.Connector == b.Primary && TryEdgeOn(b.Primary, a.Position, b.Position, access);
        }

        private bool TryEdgeOn(Surface shared, Vector3 start, Vector3 end, NavigationAccess access)
        {
            if (shared == null || !Permits(shared, access)) return false;
            var portal = shared.Portal;
            if (portal != null && !portal.Bidirectional)
            {
                if (!shared.Mesh.TryRoute(portal.Entry, start, segment, out _)) return false;
                var fromDistance = SegmentLength();
                if (!shared.Mesh.TryRoute(portal.Entry, end, segment, out _)) return false;
                if (fromDistance > SegmentLength() + .001f) return false;
            }
            return shared.Mesh.TryRoute(start, end, segment, out _);
        }

        private float SegmentLength()
        {
            var length = 0f;
            for (var i = 1; i < segment.Count; i++) length += Vector3.Distance(segment[i - 1], segment[i]);
            return length;
        }

        internal bool TryStep(Vector3 start, Vector3 proposed, NavigationAccess access, out Vector3 result)
        {
            result = start;
            foreach (var surface in surfaces)
                if (Permits(surface, access) && surface.Mesh.TryStep(start, proposed, out result)) return true;
            return false;
        }

        internal bool RegisterActor(ActorNavigationBinding actor, out string reason)
        {
            if (string.IsNullOrWhiteSpace(actor.ActorId)) { reason = "actor_id_missing"; return false; }
            if (actorIds.TryGetValue(actor.ActorId, out var existing))
            { reason = existing == actor ? "ready" : "duplicate_actor_pose_owner"; return existing == actor; }
            actorIds.Add(actor.ActorId, actor);
            UpdateActorPosition(actor);
            reason = "ready"; return true;
        }

        internal void UnregisterActor(string id, ActorNavigationBinding actor)
        {
            if (id != null && actorIds.TryGetValue(id, out var existing) && existing == actor)
            {
                actorIds.Remove(id);
                if (actorCells.TryGetValue(actor, out var cell)) neighbors[cell].Remove(actor);
                actorCells.Remove(actor);
            }
        }

        internal void UpdateActorPosition(ActorNavigationBinding actor)
        {
            var cell = Cell(actor.transform.position);
            if (actorCells.TryGetValue(actor, out var previousCell))
            {
                if (cell == previousCell) return;
                neighbors[previousCell].Remove(actor);
            }
            if (!neighbors.TryGetValue(cell, out var occupants))
            { occupants = new List<ActorNavigationBinding>(); neighbors.Add(cell, occupants); }
            occupants.Add(actor);
            actorCells[actor] = cell;
        }

        private static Vector2Int Cell(Vector3 position) =>
            new Vector2Int(Mathf.FloorToInt(position.x / NeighborCellSize), Mathf.FloorToInt(position.z / NeighborCellSize));

        internal Vector3 Avoidance(ActorNavigationBinding actor, Vector3 desired)
        {
            var result = desired;
            var position = actor.transform.position;
            var cell = Cell(position);
            var right = Vector3.Cross(Vector3.up, desired.normalized);
            for (var x = cell.x - 1; x <= cell.x + 1; x++)
                for (var z = cell.y - 1; z <= cell.y + 1; z++)
                {
                    if (!neighbors.TryGetValue(new Vector2Int(x, z), out var occupants)) continue;
                    foreach (var other in occupants)
                    {
                        if (other == actor || other == null || !other.isActiveAndEnabled) continue;
                        var delta = position - other.transform.position;
                        if (Mathf.Abs(delta.y) > .8f) continue;
                        delta.y = 0;
                        var clearance = actor.Radius + other.Radius + .12f;
                        var distance = delta.magnitude;
                        if (distance >= clearance + .8f) continue;
                        var normal = distance > .001f ? delta / distance :
                            (string.CompareOrdinal(actor.ActorId, other.ActorId) < 0 ? Vector3.right : Vector3.left);
                        var approach = Vector3.Dot(desired, -normal);
                        if (approach <= 0 && distance >= clearance) continue;
                        var weight = Mathf.Clamp01((clearance + .8f - distance) / .8f);
                        result += normal * actor.Speed * weight;
                        // Both parties keep their right; no random direction flips or identity replacement.
                        result += right * actor.Speed * weight * .65f;
                    }
                }
            if (PlayerObstacle != null && PlayerObstacle.enabled && PlayerObstacle.gameObject.activeInHierarchy)
            {
                var delta = position - PlayerObstacle.transform.position;
                if (Mathf.Abs(delta.y) < PlayerObstacle.height)
                {
                    delta.y = 0;
                    var distance = delta.magnitude;
                    var clearance = actor.Radius + PlayerObstacle.radius + .12f;
                    if (distance < clearance + .8f)
                    {
                        var normal = distance > .001f ? delta / distance : -desired.normalized;
                        if (Vector3.Dot(desired, -normal) > 0 || distance < clearance)
                        {
                            var weight = Mathf.Clamp01((clearance + .8f - distance) / .8f);
                            result += (normal + right * .65f) * actor.Speed * weight;
                        }
                    }
                }
            }
            return Vector3.ClampMagnitude(result, actor.Speed);
        }

        private bool Resolve(Vector3 position, NavigationAccess access, out Surface result)
        {
            result = null;
            var nearest = float.PositiveInfinity;
            foreach (var surface in surfaces)
            {
                if (!Permits(surface, access) || !surface.Mesh.TryProject(position, out var point)) continue;
                var distance = (point - position).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance; result = surface;
            }
            return result != null;
        }
        private static bool Permits(Surface surface, NavigationAccess access) =>
            access != 0 && (surface.Access & access) == access && (surface.Portal == null || surface.Portal.Available);
        private bool ValidateIdentity(string id, string revision, out string reason)
        {
            if (string.IsNullOrWhiteSpace(id)) return Fail("surface_id_missing", out reason);
            if (string.IsNullOrEmpty(GeometryRevision) || revision != GeometryRevision) return Fail("geometry_revision_mismatch", out reason);
            if (byId.ContainsKey(id)) return Fail("duplicate_surface_id", out reason);
            reason = "ready"; return true;
        }
        private void EnsureCapacity(int count)
        {
            if (costs.Length >= count) return;
            var capacity = Mathf.NextPowerOfTwo(count);
            costs = new float[capacity]; previous = new int[capacity]; visited = new bool[capacity];
        }
        private bool Changed(out string reason) { RouteRevision++; Status = reason = "ready"; return true; }
        private bool Fail(string value, out string reason) { Status = reason = value; return false; }
    }
}
