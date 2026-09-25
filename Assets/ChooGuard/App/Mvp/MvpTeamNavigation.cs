using System;
using System.Collections.Generic;
using ChooGuard.App.Fps.Runtime;
using UnityEngine;
namespace ChooGuard.App.Mvp
{
    // The MVP caller supplies station-local coordinates and advances only on acknowledged engine time.
    public sealed class MvpTeamNavigation : MonoBehaviour
    {
        public TextAsset NavData;
        public string GeometryDigest;
        private DetourNavigationSurface surface;
        private TextAsset loadedData;
        private string loadedDigest;
        private readonly List<Vector3> buffer = new List<Vector3>(128);
        public string LastReason { get; private set; } = "navigation_not_loaded";

        public void Reload()
        {
            surface = null;
            loadedData = null;
            loadedDigest = null;
            LastReason = "navigation_not_loaded";
        }

        public bool TryPlan(Vector3 start, Vector3 end, out Vector3[] corners, out string reason)
        {
            corners = Array.Empty<Vector3>();
            if (surface == null || loadedData != NavData || loadedDigest != GeometryDigest)
            {
                if (string.IsNullOrWhiteSpace(GeometryDigest))
                { LastReason = reason = "geometry_revision_missing"; return false; }
                if (!DetourNavigationSurface.TryLoad("mvp-station", NavData == null ? null : NavData.bytes,
                    null, .12f, .3f, out surface, out reason))
                { LastReason = reason; return false; }
                loadedData = NavData;
                loadedDigest = GeometryDigest;
            }
            var ready = surface.TryRoute(start, end, buffer, out reason);
            LastReason = reason;
            if (ready) corners = buffer.ToArray();
            return ready;
        }
    }
}
