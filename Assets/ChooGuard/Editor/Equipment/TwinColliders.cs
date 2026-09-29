using System;
using System.Collections.Generic;
using ChooGuard.App.Fps.Equipment;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Gives every visible mesh of the twin that has no collider (glass panels, cladding, fascias) a temporary MeshCollider while
    /// the placement and view builders run, so their raycasts and overlap boxes see it like any wall. The colliders are never saved:
    /// <see cref="Dispose"/> removes them (use it in a using block or a finally).
    /// </summary>
    internal sealed class TwinColliders : IDisposable
    {
        private readonly List<MeshCollider> added = new List<MeshCollider>();

        /// <summary>How many colliders were added.</summary>
        public int Count => added.Count;

        public TwinColliders()
        {
            var train = GameObject.Find(StationSurvey.KtxPath)?.transform;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                // 트리거 콜라이더만 있는 면도 광선과 겹침 검사에는 보이지 않는다: 단단한 콜라이더가 없으면 단다.
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || Array.Exists(renderer.GetComponents<Collider>(), c => !c.isTrigger)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || mesh.vertexCount < 4 || mesh.subMeshCount == 0) continue;
                // 열차는 움직이고, 작은 장식은 벽이 아니다.
                if (train != null && renderer.transform.IsChildOf(train)) continue;
                if (renderer.bounds.size.magnitude < .3f || renderer.GetComponentInParent<StationEquipment>() != null) continue;
                added.Add(renderer.gameObject.AddComponent<MeshCollider>());
            }
            Physics.SyncTransforms();
        }

        public void Dispose()
        {
            foreach (var collider in added) if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            added.Clear();
        }
    }
}
