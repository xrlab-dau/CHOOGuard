// Copyright (C) 2023 Nicholas Maltbie
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
// associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute,
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
// BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
// CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// Adapted from AbstractPrimitiveColliderCast.CastSelf (L69–95), OpenKCC
// a1a30ed7f7722ea82a1df6bd01849e0bfde6abf4, MIT:
// https://github.com/nicholas-maltbie/OpenKCC/blob/a1a30ed7f7722ea82a1df6bd01849e0bfde6abf4/Packages/com.nickmaltbie.openkcc/OpenKCC/Utils/ColliderCast/AbstractPrimitiveColliderCast.cs
using UnityEngine;

namespace ChooGuard.App.Fps
{
    /// <summary>Select the nearest solid first; callers then decide whether it is interactive.</summary>
    internal static class NearestSolidRay
    {
        internal static bool TryCast(Vector3 origin, Vector3 direction, float distance, int layerMask,
            RaycastHit[] hits, Transform player, Transform carriedBag, Transform carriedKey,
            out RaycastHit closest, out bool saturated)
        {
            closest = new RaycastHit { distance = Mathf.Infinity };
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, layerMask, QueryTriggerInteraction.Ignore);
            // A full NonAlloc buffer is unordered and need not contain the nearest solid.
            // Fail closed before excluding even self hits: hidden overflow may include a wall.
            saturated = count == hits.Length;
            if (saturated) return false;
            bool hitSomething = false;
            for (int i = 0; i < count; i++)
            {
                var objHit = hits[i];
                if (objHit.collider == null) continue;
                var candidate = objHit.collider.transform;
                // OpenKCC's self-transform rejection extended to owned carried hierarchies.
                if (Under(candidate, player) || Under(candidate, carriedBag) || Under(candidate, carriedKey)) continue;
                if (objHit.distance < closest.distance)
                {
                    closest = objHit;
                }
                hitSomething = true;
            }
            return hitSomething;
        }

        static bool Under(Transform candidate, Transform root) => root != null && candidate.IsChildOf(root);
    }
}
