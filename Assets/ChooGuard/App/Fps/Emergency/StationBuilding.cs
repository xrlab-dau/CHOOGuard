using System.Collections.Generic;
using ChooGuard.App.Fps.Shell;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>The building's authored zones plus its real static overhead structure; never a passenger or a train roof.</summary>
    internal sealed class StationBuilding
    {
        private readonly StationPoints points;
        private readonly HashSet<Collider> structure = new HashSet<Collider>();
        private readonly Dictionary<Vector3, bool> frameResults = new Dictionary<Vector3, bool>(256);
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private int frame = -1;

        public StationBuilding(StationPoints station)
        {
            points = station;
            var scene = SceneManager.GetSceneByName(SceneFlow.StationScene);
            var train = points.Train == null ? null : GameObject.Find(points.Train.root)?.transform;
            var roots = new List<GameObject>();
            var colliders = new List<Collider>();
            if (scene.IsValid() && scene.isLoaded) scene.GetRootGameObjects(roots);
            foreach (var root in roots)
            {
                root.GetComponentsInChildren(false, colliders);
                foreach (var collider in colliders)
                {
                    if (!collider.enabled || collider.isTrigger || collider.attachedRigidbody != null) continue;
                    if (!(collider is MeshCollider) && !(collider is BoxCollider)) continue;
                    if (train != null && collider.transform.IsChildOf(train)) continue;
                    structure.Add(collider);
                }
            }
        }

        internal static bool InIndoorZone(StationPoints points, Vector3 position)
        {
            var zone = points.ZoneAt(position);
            return zone != null && !StationWorld.IsOutdoorZone(zone.id) && zone.id != "tracks" && points.PlatformAt(position) == null;
        }

        public bool Contains(Vector3 position)
        {
            if (!InIndoorZone(points, position)) return false;
            if (frame != Time.frameCount) { frame = Time.frameCount; frameResults.Clear(); }
            if (frameResults.TryGetValue(position, out bool inside)) return inside;
            int count = Physics.RaycastNonAlloc(position + Vector3.up * 40f, Vector3.down, hits, 38.4f, ~0, QueryTriggerInteraction.Ignore);
            inside = false;
            if (count < hits.Length)
                for (int i = 0; i < count; i++)
                    if (hits[i].normal.y > .5f && structure.Contains(hits[i].collider)) { inside = true; break; }
            frameResults.Add(position, inside);
            return inside;
        }
    }
}
