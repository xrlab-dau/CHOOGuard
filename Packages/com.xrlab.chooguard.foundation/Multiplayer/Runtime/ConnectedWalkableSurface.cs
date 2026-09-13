using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer
{
    // Only builder-authored floor and ramp surfaces provide support. Props and rail beds do not.
    public sealed class ConnectedWalkableSurface : MonoBehaviour
    {
        public string SurfaceId, RegionId, FrameId, PortalId = "";
    }
}
