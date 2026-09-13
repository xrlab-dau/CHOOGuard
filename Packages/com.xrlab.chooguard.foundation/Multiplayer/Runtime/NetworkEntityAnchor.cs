using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer
{
    public sealed class NetworkEntityAnchor : MonoBehaviour
    {
        public string EntityId;
        public string RegionId = "hall";
        public string RequiredRoleId = "";
        public EntityKind Kind;
    }
}
