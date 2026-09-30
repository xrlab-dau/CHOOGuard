using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// The station's walkable floor exactly as it was baked. A second copy of the world navmesh is added <see cref="Shift"/> metres to the side of the
    /// station, where no obstacle ever stands: fires, locked doors, cordons and shutters carve the live navmesh inside the station and the
    /// copy stays whole, so a question asked here has the same answer whatever has happened in the session. It has no escalator or elevator links
    /// (those are the route graph's). Queries take and return station coordinates; the shift is this class's business. Reusable buffers, no allocation per query.
    /// </summary>
    public sealed class BakedFloor : IDisposable
    {
        /// <summary>Far enough beside the station (whose navmesh spans a few hundred metres) that nothing in it touches the copy; small enough to keep float precision near a millimetre.</summary>
        public static readonly Vector3 Shift = new Vector3(2048f, 0, 0);

        private NavMeshDataInstance instance;
        private readonly NavMeshPath path = new NavMeshPath();
        private readonly Vector3[] corners = new Vector3[128];

        public BakedFloor(NavMeshData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            instance = NavMesh.AddNavMeshData(data, Shift, Quaternion.identity);
            if (!instance.valid) throw new InvalidOperationException("기준 바닥(navmesh 사본)을 올리지 못했습니다.");
        }

        /// <summary>The walkable point nearest <paramref name="position"/> within <paramref name="radius"/>, or false (then <paramref name="floor"/> is <paramref name="position"/>).</summary>
        public bool Sample(Vector3 position, float radius, out Vector3 floor)
        {
            bool found = NavMesh.SamplePosition(position + Shift, out var hit, radius, NavMesh.AllAreas);
            floor = found ? hit.position - Shift : position;
            return found;
        }

        /// <summary>
        /// Appends the corners of the walk from <paramref name="from"/> to <paramref name="to"/> (walkable points) to <paramref name="into"/>, leaving out
        /// the first one when it is where <paramref name="into"/> already ends. False, and nothing appended, when the floor has no complete walk between them.
        /// </summary>
        public bool Walk(Vector3 from, Vector3 to, List<Vector3> into)
        {
            if (!NavMesh.CalculatePath(from + Shift, to + Shift, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            int count = path.GetCornersNonAlloc(corners);
            if (count == 0 || count >= corners.Length) return false;
            int first = into.Count > 0 && (into[into.Count - 1] - (corners[0] - Shift)).sqrMagnitude < .01f ? 1 : 0;
            for (int i = first; i < count; i++) into.Add(corners[i] - Shift);
            return true;
        }

        public void Dispose()
        {
            if (instance.valid) instance.Remove();
        }
    }
}
