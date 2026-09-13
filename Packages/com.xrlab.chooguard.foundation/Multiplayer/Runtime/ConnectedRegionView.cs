using System.Linq;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer
{
    public sealed class ConnectedRegionView : MonoBehaviour
    {
        public string RegionId, FrameId, EquipmentEntityId;
        public Vector3 LocalBoundsCenter, LocalBoundsSize;
        public bool ControlsLighting;
        public NetworkEntityAnchor Equipment;
        private MaterialPropertyBlock block;

        public void ApplyFrame(ConnectedWorldDefinition authored, SpatialFrame current)
        {
            if (current.FrameId != FrameId) throw new System.ArgumentException("Region frame mismatch.");
            var original = authored.Frame(FrameId); var region = authored.Region(RegionId);
            var point = current.ToWorld(original.ToLocal(region.Center));
            transform.position = new Vector3(point.X, point.Y, point.Z);
            // Region builders author yaw zero even when a frame's base yaw is 180 degrees.
            transform.rotation = Quaternion.Euler(0, current.YawDegrees - original.YawDegrees, 0);
        }

        public void Apply(EntityState observed, bool applyLighting=true)
        {
            if (Equipment == null) return;
            // Dynamic equipment states are projected from the server, not inferred from a loaded neighbor scene.
            foreach (var renderer in Equipment.GetComponentsInChildren<Renderer>(true)) renderer.enabled = observed != null;
            if (observed == null) return;
            block ??= new MaterialPropertyBlock();
            block.SetColor("_EmissionColor", observed.Active ? new Color(.05f, .4f, .08f) : new Color(.5f, .04f, .01f));
            foreach (var renderer in Equipment.GetComponentsInChildren<Renderer>(true).Where(r =>
                r.name.StartsWith("Status_") || r.name.StartsWith("SharedState"))) renderer.SetPropertyBlock(block);
            if (ControlsLighting && applyLighting)
                foreach (var light in GetComponentsInChildren<Light>(true)) light.enabled = observed.Active;
        }
    }
}
