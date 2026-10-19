using System;
using System.Collections.Generic;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.Circulation;
using UnityEngine;

namespace ChooGuard.App.Fps.Twin
{
    /// <summary>An explicit source-object to existing lane mapping. Never guesses a lane from distance or creates missing machinery.</summary>
    [DisallowMultipleComponent]
    public sealed class EscalatorDriveBinding : MonoBehaviour
    {
        public string lane_id;
        public bool Bound { get; private set; }
        public string Issue { get; private set; } = "unbound_lane";
        public bool Bind(IReadOnlyList<Escalator> lanes)
        {
            Bound = false;
            Escalator match = null;
            foreach (var lane in lanes)
                if (lane != null && lane.Entry != null && string.Equals(lane.Entry.id, lane_id, StringComparison.Ordinal))
                {
                    if (match != null) return Unbind("ambiguous_lane_id");
                    match = lane;
                }
            if (match == null) return Unbind("missing_lane_id");
            // Controller/trigger stubs can survive source removal. They are not
            // operable machinery: bind only supplied tread geometry or an authored
            // motion channel. Platform Prepare retains its strict partial-loss guard.
            bool operable = false;
            foreach (var run in GetComponentsInChildren<PlatformEscalatorRun>(true))
            {
                run.Prepare();
                if (!run.SourceAvailable || !run.IsBound) return Unbind("missing_operable_parts");
                operable = true;
            }
            foreach (var motion in GetComponentsInChildren<EscalatorMotion>(true))
            {
                if (motion.Drive == null) motion.RebuildBindings();
                operable |= motion.AnimatedPartCount > 0 || motion.AnimatedSurfaceCount > 0;
            }
            if (!operable) return Unbind("missing_operable_parts");
            match.BindDrive(transform);
            Bound = true; Issue = ""; return true;
        }

        private bool Unbind(string issue)
        {
            foreach (var ride in GetComponentsInChildren<EscalatorRide>(true)) ride.Bind(null);
            foreach (var motion in GetComponentsInChildren<EscalatorMotion>(true)) motion.Bind(null);
            Issue = issue; return false;
        }
    }
}
