using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.Foundation.Multiplayer
{
    /// <summary>Renders only current nearby server observations; never creates a local crowd simulation.</summary>
    public sealed class WorldBodyView : MonoBehaviour
    {
        private GameObject prefab;
        private ConnectedWorldRuntime world;
        private FieldView latest;
        private readonly Dictionary<string, GameObject> bodies = new Dictionary<string, GameObject>();
        private bool rendered;
        public void Configure(GameObject bodyPrefab, ConnectedWorldRuntime connected)
        { prefab = bodyPrefab; world = connected; rendered = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null; }
        public void Apply(FieldView view) { latest = view; Draw(); }
        private void LateUpdate() { if (world != null && world.Loading) Draw(); }
        private void Draw()
        {
            if (!rendered || latest?.Physical == null || world == null) return;
            if (prefab == null) throw new InvalidOperationException("The Foundation player has no authored body model.");
            var present = new HashSet<string>();
            void Body(string id, string region, string frame, Point3 position, bool participant)
            {
                if (!world.LoadedRegionIds.Contains(region) || !latest.Physical.Frames.Any(f => f.FrameId == frame)) return;
                present.Add(id);
                if (!bodies.TryGetValue(id, out var body))
                {
                    body = Instantiate(prefab, transform); body.name = "Observed_" + id;
                    body.AddComponent<WorldBodyPresentation>().BodyId = id;
                    foreach (var collider in body.GetComponentsInChildren<Collider>(true)) collider.isTrigger = true;
                    if (participant) body.transform.localScale = new Vector3((float)(WorldBodyPresentation.PlayerRadiusM / WorldBodyPresentation.NpcRadiusM), 1, (float)(WorldBodyPresentation.PlayerRadiusM / WorldBodyPresentation.NpcRadiusM));
                    bodies.Add(id, body);
                }
                body.SetActive(true);
                var next = new Vector3(position.X, position.Y, position.Z); var delta = next - body.transform.position; delta.y = 0;
                if (delta.sqrMagnitude > .0001 && delta.sqrMagnitude < 4) body.transform.rotation = Quaternion.LookRotation(delta);
                body.transform.position = next;
            }
            foreach (var entity in latest.Observed.Entities.Where(e => e.Kind == EntityKind.Evacuee)) Body(entity.EntityId, entity.RegionId, entity.FrameId, entity.Position, false);
            foreach (var participant in latest.Physical.Bodies) Body(participant.ParticipantId, participant.RegionId, participant.FrameId, participant.Position, true);
            foreach (var item in bodies) if (!present.Contains(item.Key)) item.Value.SetActive(false);
        }
    }
}
