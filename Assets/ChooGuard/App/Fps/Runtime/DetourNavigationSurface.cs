using System;
using System.Collections.Generic;
using System.IO;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using UnityEngine;

namespace ChooGuard.App.Fps.Runtime
{
    // Shared by gameplay and the acknowledged-time MVP team navigator. All query buffers are reused.
    internal sealed class DetourNavigationSurface
    {
        private sealed class DoorFilter : IDtQueryFilter
        {
            public readonly Dictionary<long, int> Blocked = new Dictionary<long, int>();
            public bool PassFilter(long reference, DtMeshTile tile, DtPoly poly) =>
                poly.flags != 0 && poly.GetPolyType() == DtPolyTypes.DT_POLYTYPE_GROUND && !Blocked.ContainsKey(reference);
            public float GetCost(RcVec3f a, RcVec3f b, long previous, DtMeshTile previousTile,
                DtPoly previousPoly, long current, DtMeshTile currentTile, DtPoly currentPoly,
                long next, DtMeshTile nextTile, DtPoly nextPoly) => RcVec3f.Distance(a, b);
        }

        private readonly DtNavMeshQuery query;
        private readonly DoorFilter filter = new DoorFilter();
        private readonly DtQueryDefaultFilter unblockedFilter = new DtQueryDefaultFilter();
        private readonly long[] polygons = new long[4096];
        private readonly DtStraightPath[] corners = new DtStraightPath[4096];
        private readonly Transform frame;
        private readonly Matrix4x4 localToWorld;
        private readonly Matrix4x4 worldToLocal;
        private readonly float horizontalTolerance;
        private readonly float verticalTolerance;
        public string Id { get; }
        public bool IsCurrent => frame == null ? !hasFrame : frame.localToWorldMatrix == localToWorld;
        private readonly bool hasFrame;

        private DetourNavigationSurface(string id, DtNavMeshQuery query, Transform frame,
            float horizontalTolerance, float verticalTolerance)
        {
            Id = id;
            this.query = query;
            this.frame = frame;
            hasFrame = frame != null;
            localToWorld = frame == null ? Matrix4x4.identity : frame.localToWorldMatrix;
            worldToLocal = localToWorld.inverse;
            this.horizontalTolerance = horizontalTolerance;
            this.verticalTolerance = verticalTolerance;
        }

        public static bool TryLoad(string id, byte[] bytes, Transform frame, float horizontalTolerance,
            float verticalTolerance, out DetourNavigationSurface surface, out string reason)
        {
            surface = null;
            reason = "navigation_not_baked";
            if (bytes == null || bytes.Length == 0) return false;
            if (horizontalTolerance <= 0 || horizontalTolerance > .3f || verticalTolerance <= 0 ||
                verticalTolerance > .5f || !Finite(horizontalTolerance) || !Finite(verticalTolerance))
            { reason = "invalid_endpoint_tolerance"; return false; }
            if (frame != null && ((frame.lossyScale - Vector3.one).sqrMagnitude > .000001f ||
                Vector3.Dot(frame.up, Vector3.up) < .99999f))
            { reason = "navigation_frame_requires_unit_scale_and_y_up"; return false; }
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(bytes, false)))
                {
                    var data = new DtMeshDataReader().Read(reader, 6);
                    var mesh = new DtNavMesh();
                    if (!mesh.Init(data, 6, 0).Succeeded())
                    { reason = "navmesh_init_failed"; return false; }
                    surface = new DetourNavigationSurface(id, new DtNavMeshQuery(mesh), frame,
                        horizontalTolerance, verticalTolerance);
                }
                reason = "ready";
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is IndexOutOfRangeException || ex is InvalidOperationException)
            { reason = "navigation_data_invalid:" + ex.GetType().Name; return false; }
        }

        public bool TryProject(Vector3 world, out Vector3 point)
        {
            point = default;
            if (!IsCurrent || !Finite(world)) return false;
            if (!TryNearest(worldToLocal.MultiplyPoint3x4(world), filter, out _, out var nearest)) return false;
            point = localToWorld.MultiplyPoint3x4(Unity(nearest));
            return true;
        }

        private bool TryNearest(Vector3 point, IDtQueryFilter activeFilter, out long reference, out RcVec3f nearest)
        {
            var extent = new RcVec3f(horizontalTolerance, verticalTolerance, horizontalTolerance);
            var status = query.FindNearestPoly(Rc(point), extent, activeFilter, out reference, out nearest, out _);
            return Complete(status) && reference != 0 &&
                new Vector2(point.x - nearest.X, point.z - nearest.Z).sqrMagnitude <= horizontalTolerance * horizontalTolerance &&
                Mathf.Abs(point.y - nearest.Y) <= verticalTolerance;
        }

        public bool TryRoute(Vector3 start, Vector3 end, List<Vector3> output, out string reason)
        {
            output.Clear();
            if (!IsCurrent) { reason = "geometry_frame_changed_rebind_required"; return false; }
            if (!Finite(start) || !Finite(end)) { reason = "non_finite_endpoint"; return false; }
            var localStart = worldToLocal.MultiplyPoint3x4(start);
            var localEnd = worldToLocal.MultiplyPoint3x4(end);
            if (!TryNearest(localStart, filter, out var a, out var ap) || !TryNearest(localEnd, filter, out var b, out var bp))
            { reason = "endpoint_outside_walkable_surface"; return false; }
            var status = query.FindPath(a, b, ap, bp, filter, polygons, out var count, polygons.Length);
            if (!Complete(status) || count == 0 || polygons[count - 1] != b)
            { reason = "unreachable_or_partial_path"; return false; }
            // Include polygon crossings: a line from the foot of a stair to its top must not cut through the stairs.
            status = query.FindStraightPath(ap, bp, polygons, count, corners, out var cornerCount, corners.Length,
                DtStraightPathOptions.DT_STRAIGHTPATH_ALL_CROSSINGS);
            if (!Complete(status) || cornerCount < 1 || (Unity(corners[cornerCount - 1].pos) - Unity(bp)).sqrMagnitude > .0001f)
            { reason = "incomplete_corner_path"; return false; }
            for (var i = 0; i < cornerCount; i++) AddDistinct(output, localToWorld.MultiplyPoint3x4(Unity(corners[i].pos)));
            reason = "ready";
            return true;
        }

        // Accept only an uninterrupted segment on this baked surface. Never project a step across a wall or gap.
        public bool TryStep(Vector3 start, Vector3 proposed, out Vector3 result)
        {
            result = start;
            if (!IsCurrent || !Finite(start) || !Finite(proposed)) return false;
            var a = worldToLocal.MultiplyPoint3x4(start);
            var b = worldToLocal.MultiplyPoint3x4(proposed);
            if (!TryNearest(a, filter, out var reference, out var nearest)) return false;
            if (new Vector2(a.x - nearest.X, a.z - nearest.Z).sqrMagnitude > .000625f) return false;
            var status = query.Raycast(reference, nearest, Rc(b), filter, out var t, out _, polygons, out var count, polygons.Length);
            if (!Complete(status) || t < 1f || count == 0) return false;
            if (!query.GetPolyHeight(polygons[count - 1], Rc(b), out var height).Succeeded()) return false;
            if (Mathf.Abs(height - b.y) > verticalTolerance) return false;
            b.y = height;
            result = localToWorld.MultiplyPoint3x4(b);
            return true;
        }

        public bool TryDoorPolygons(Bounds worldBounds, out long[] references, out string reason)
        {
            references = null;
            if (!IsCurrent) { reason = "geometry_frame_changed_rebind_required"; return false; }
            var local = new Bounds(worldToLocal.MultiplyPoint3x4(worldBounds.center), Vector3.zero);
            for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                    for (var z = -1; z <= 1; z += 2)
                        local.Encapsulate(worldToLocal.MultiplyPoint3x4(worldBounds.center + Vector3.Scale(worldBounds.extents, new Vector3(x, y, z))));
            var status = query.QueryPolygons(Rc(local.center), Rc(local.extents), unblockedFilter,
                polygons, out var count, polygons.Length);
            if (!Complete(status) || count == 0) { reason = "door_does_not_bind_walkable_polygons"; return false; }
            references = new long[count];
            Array.Copy(polygons, references, count);
            reason = "ready";
            return true;
        }

        public void Block(long[] references, bool blocked)
        {
            foreach (var reference in references)
            {
                filter.Blocked.TryGetValue(reference, out var count);
                if (blocked) filter.Blocked[reference] = count + 1;
                else if (count <= 1) filter.Blocked.Remove(reference);
                else filter.Blocked[reference] = count - 1;
            }
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        internal static void AddDistinct(List<Vector3> output, Vector3 value)
        {
            if (output.Count == 0 || (output[output.Count - 1] - value).sqrMagnitude > .000001f) output.Add(value);
        }
        private static bool Complete(DtStatus status) => status.Succeeded() && !status.Failed() &&
            !status.IsPartial() && !status.Has(DtStatus.DT_BUFFER_TOO_SMALL) && !status.Has(DtStatus.DT_OUT_OF_NODES);
        private static RcVec3f Rc(Vector3 p) => new RcVec3f(p.x, p.y, p.z);
        private static Vector3 Unity(RcVec3f p) => new Vector3(p.X, p.Y, p.Z);
    }
}
