using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer
{
    public sealed class ConnectedPortalBarrier : MonoBehaviour
    {
        public string PortalId;
        public string[] LinkedDoorEntityIds;
        public bool Open = true;
        public void SetOpen(bool open)
        {
            Open = open;
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = !open;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = !open;
        }
    }
}
