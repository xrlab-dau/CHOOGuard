using UnityEngine;

namespace ChooGuard.App.Fps.Runtime
{
    /// <summary>Bounded nonalloc queries fail closed when even the overflow buffer saturates.</summary>
    public sealed class FpsPhysicalQueries
    {
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private readonly RaycastHit[] overflowHits = new RaycastHit[128];
        private readonly Collider[] overlaps = new Collider[128];
        public bool Saturated { get; private set; }

        public bool FirstSolid(Vector3 from, Vector3 to, float radius, Transform actor, FpsEntityBinding ignore,
            out RaycastHit nearest, FpsEntityBinding ignoreSecond = null)
        {
            nearest = default; Saturated = false;
            Vector3 delta = to - from; float distance = delta.magnitude;
            if (distance <= .00001f) return false;
            var buffer = hits;
            int count = Cast(from, delta / distance, radius, distance, buffer);
            if (count == buffer.Length)
            {
                buffer = overflowHits; count = Cast(from, delta / distance, radius, distance, buffer);
                if (count == buffer.Length) { Saturated = true; return false; }
            }
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var collider = buffer[i].collider;
                if (collider == null || actor != null && collider.transform.IsChildOf(actor) ||
                    ignore != null && ignore.Owns(collider) || ignoreSecond != null && ignoreSecond.Owns(collider)) continue;
                if (buffer[i].distance >= best) continue;
                nearest = buffer[i]; best = nearest.distance;
            }
            return best < float.PositiveInfinity;
        }

        private static int Cast(Vector3 from, Vector3 direction, float radius, float distance, RaycastHit[] buffer)
        {
            return radius > 0
                ? Physics.SphereCastNonAlloc(from, radius, direction, buffer, distance, ~0, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(from, direction, buffer, distance, ~0, QueryTriggerInteraction.Ignore);
        }

        public bool Visible(Vector3 from, Vector3 point, Transform actor, FpsEntityBinding target, FpsEntityBinding ignore = null)
        {
            if (!OriginClear(from, .001f, actor, ignore, target)) return false;
            Vector3 delta = point - from;
            bool hit = FirstSolid(from, point + delta.normalized * .015f, 0, actor, ignore, out var nearest);
            return !Saturated && (!hit || target.Owns(nearest.collider) && Vector3.Distance(nearest.point, point) <= .03f);
        }

        public bool ClearPath(Vector3 from, Vector3 to, float radius, Transform actor, FpsEntityBinding target, FpsEntityBinding ignore)
        {
            if (!OriginClear(from, Mathf.Max(.001f, radius), actor, ignore, target)) return false;
            bool hit = FirstSolid(from, to, radius, actor, ignore, out var nearest);
            return !Saturated && (!hit || target != null && target.Owns(nearest.collider) &&
                nearest.distance >= Vector3.Distance(from, to) - radius - .03f);
        }

        public bool ClearMotion(Vector3 from, Vector3 to, float radius, Transform actor, FpsEntityBinding tool, FpsEntityBinding surface)
        {
            if (!OriginClear(from, Mathf.Max(.001f, radius), actor, tool, surface)) return false;
            bool hit = FirstSolid(from, to, radius, actor, tool, out _, surface);
            return !Saturated && !hit;
        }

        private bool OriginClear(Vector3 origin, float radius, Transform actor, FpsEntityBinding ignore, FpsEntityBinding allowed)
        {
            Saturated = false;
            int count = Physics.OverlapSphereNonAlloc(origin, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) { Saturated = true; return false; }
            for (int i = 0; i < count; i++)
            {
                var collider = overlaps[i];
                if (collider == null || actor != null && collider.transform.IsChildOf(actor) ||
                    ignore != null && ignore.Owns(collider) || allowed != null && allowed.Owns(collider)) continue;
                if (Vector3.Distance(origin, collider.ClosestPoint(origin)) < radius) return false;
            }
            return true;
        }

        /// <summary>Checks real penetration and support under the mass centre, returning the actual collider owner.</summary>
        public bool CanPlace(FpsEntityBinding binding, Transform actor, out string reason, out FpsEntityBinding supportedBy)
        {
            reason = null; supportedBy = null; Saturated = false;
            if (!binding.CanDriveBody(out reason)) return false;
            bool hasBounds = false; Bounds bounds = default;
            var colliders = binding.Colliders;
            for (int ownIndex = 0; ownIndex < colliders.Count; ownIndex++)
            {
                var own = colliders[ownIndex];
                if (own == null || !own.enabled || own.isTrigger || own.attachedRigidbody != binding.Body) continue;
                if (!hasBounds) { bounds = own.bounds; hasBounds = true; } else bounds.Encapsulate(own.bounds);
                int count = Physics.OverlapBoxNonAlloc(own.bounds.center, own.bounds.extents + Vector3.one * .002f,
                    overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                if (count == overlaps.Length) { Saturated = true; reason = "배치 충돌 질의가 포화되었습니다"; return false; }
                for (int i = 0; i < count; i++)
                {
                    var other = overlaps[i];
                    if (other == null || binding.Owns(other)) continue;
                    if (Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > .003f)
                    { reason = "다른 물체와 겹쳐 내려놓을 수 없습니다"; return false; }
                }
            }
            if (!hasBounds) { reason = "지지할 충돌 형상이 없습니다"; return false; }
            var centre = binding.Body.worldCenterOfMass;
            var probe = new Vector3(centre.x, bounds.min.y + .01f, centre.z);
            bool supported = FirstSolid(probe, probe + Vector3.down * (binding.SupportProbeDistance + .01f), 0,
                actor, binding, out var support);
            if (Saturated || !supported || Vector3.Dot(support.normal, Vector3.up) < binding.MinimumSupportUp)
            { reason = "무게중심 아래의 지지면이 확인되지 않았습니다"; return false; }
            if (support.rigidbody != null && !support.rigidbody.isKinematic && support.rigidbody.velocity.sqrMagnitude > .01f)
            { reason = "지지면이 움직이고 있습니다"; return false; }
            if (binding.Body.velocity.sqrMagnitude > .04f || binding.Body.angularVelocity.sqrMagnitude > .04f)
            { reason = "물체를 안정시킨 뒤 내려놓으세요"; return false; }
            supportedBy = FpsEntityBinding.SupportOwner(support.collider);
            return true;
        }
    }
}
