using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer
{
    /// <summary>Server-side swept capsule with continuous ground support and topology/loading constraints.</summary>
    public static class GroundedWorldMotor
    {
        public const float Radius = .3f, Height = 1.8f, MaximumSlopeDegrees = 35;
        private const float Skin = .035f, GroundProbeRise = .32f, MaximumDrop = .32f;
        public static string LastFailure { get; private set; }
        private static readonly Vector3[] SupportDirections = { Vector3.zero, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        public struct SupportPoint { public Vector3 Position, Normal; public ConnectedWalkableSurface Surface; }

        public static bool TryMove(ConnectedWorldDefinition world, SpatialPose actor, Vector3 offset,
            ISet<string> loadedRegions, ISet<string> closedPortals, out SpatialPose next, float radius = Radius, float height = Height)
        {
            next = actor;
            if (loadedRegions == null || !loadedRegions.Contains(actor.RegionId)) { LastFailure = "current region not loaded"; return false; }
            if (!TryGroundedStep(V(actor.Position), offset, out var grounded, radius, height)) return false;
            if (!world.TryLocate(actor, P(grounded), radius, closedPortals, out var located)) { LastFailure = "outside authored region/portal"; return false; }
            if (!loadedRegions.Contains(located.RegionId)) { LastFailure = "destination not loaded"; return false; }
            next = located; return true;
        }

        public static bool TryGroundedStep(Vector3 from, Vector3 offset, out Vector3 next, float radius = Radius, float height = Height)
        {
            next = from;
            LastFailure = "";
            if (!Finite(from) || !Finite(offset) || !Body(radius, height) || Mathf.Abs(offset.y) > .001f || offset.magnitude > 5) return false;
            var steps = Mathf.Max(1, Mathf.CeilToInt(offset.magnitude / .12f));
            var previous = from;
            for (var i = 1; i <= steps; i++)
            {
                var horizontal = from + offset * (i / (float)steps); horizontal.y = previous.y;
                if (!TrySampleSupport(horizontal, out var support, radius, height)) return false;
                var candidate = support.Position;
                if (Blocked(previous, candidate, radius, height)) return false;
                previous = candidate;
            }
            next = previous; return true;
        }

        public static bool TrySampleSupport(Vector3 target, out SupportPoint support, float radius = Radius, float height = Height)
        {
            support = default; var grounded = target;
            if (!Finite(target) || !Body(radius, height)) return false;
            for (var i = 0; i < SupportDirections.Length; i++)
            {
                var origin = target + SupportDirections[i] * (radius * (.28f / Radius)) + Vector3.up * GroundProbeRise;
                var supported = false;
                foreach (var hit in Physics.RaycastAll(origin, Vector3.down, GroundProbeRise + MaximumDrop, ~0,
                    QueryTriggerInteraction.Ignore).Where(h => !WorldBodyPresentation.IsPresentation(h.collider)).OrderBy(h => h.distance))
                {
                    if (hit.collider.GetComponent<ConnectedWalkableSurface>() == null)
                    { LastFailure = "support obstacle " + hit.collider.name; return false; }
                    var surfaceNormal = hit.collider.transform.up;
                    if (Vector3.Dot(surfaceNormal, Vector3.up) < Mathf.Cos(MaximumSlopeDegrees * Mathf.Deg2Rad))
                    { LastFailure = "support slope " + surfaceNormal; return false; }
                    // A tilted box's end cap can be above the adjoining landing by a few millimetres.
                    // Only its authored upper face is support; continue to the overlapping landing below.
                    if (Vector3.Dot(hit.normal, surfaceNormal) < .99f) continue;
                    if (i == 0)
                    { grounded.y = hit.point.y; support.Surface = hit.collider.GetComponent<ConnectedWalkableSurface>(); support.Normal = surfaceNormal; }
                    supported = true; break;
                }
                if (!supported) { LastFailure = "unsupported at " + origin; return false; }
            }
            // Also reject an upper floor close enough to intersect the head, even if it is marked walkable.
            foreach (var direction in SupportDirections)
                foreach (var ceiling in Physics.RaycastAll(grounded + direction * (radius * (.28f / Radius)) + Vector3.up * .1f, Vector3.up, height - .1f, ~0, QueryTriggerInteraction.Ignore))
                    if (!WorldBodyPresentation.IsPresentation(ceiling.collider)) { LastFailure = "headroom " + ceiling.collider.name; return false; }
            support.Position = grounded;
            return true;
        }

        private static bool Blocked(Vector3 from, Vector3 to, float radius, float height)
        {
            var bottom = from + Vector3.up * (radius + Skin);
            var top = from + Vector3.up * (height - radius);
            var delta = to - from;
            if (delta.sqrMagnitude > 1e-10f)
                foreach (var hit in Physics.CapsuleCastAll(bottom, top, radius, delta.normalized,
                    delta.magnitude + Skin, ~0, QueryTriggerInteraction.Ignore))
                    // Support/normal/headroom are checked separately at every substep. A floor's vertical
                    // end face must not become a wall when the capsule descends through a flush ramp seam.
                    if (hit.collider.GetComponent<ConnectedWalkableSurface>() == null && !WorldBodyPresentation.IsPresentation(hit.collider))
                    { LastFailure = "sweep " + hit.collider.name; return true; }
            var overlap = Physics.OverlapCapsule(to + Vector3.up * (radius + Skin), to + Vector3.up * (height - radius),
                radius, ~0, QueryTriggerInteraction.Ignore).FirstOrDefault(c => c.GetComponent<ConnectedWalkableSurface>() == null && !WorldBodyPresentation.IsPresentation(c));
            if (overlap != null) LastFailure = "overlap " + overlap.name;
            return overlap != null;
        }

        private static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) &&
            !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
        private static bool Body(float radius, float height) => radius >= .05f && radius <= 1 && height >= 2 * radius + Skin && height <= 3;
        private static Vector3 V(Point3 p) => new Vector3(p.X, p.Y, p.Z);
        private static Point3 P(Vector3 p) => new Point3(p.x, p.y, p.z);
    }
}
