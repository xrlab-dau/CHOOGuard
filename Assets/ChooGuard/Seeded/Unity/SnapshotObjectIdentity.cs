using System;
using UnityEngine;

namespace ChooGuard.Seeded.Unity
{
    // Passive source metadata only. A separate matching script filename is required
    // for Unity to persist this component's identity through prefab save/reload.
    public sealed class SnapshotObjectIdentity : MonoBehaviour
    {
        public string ObjectId, SceneFileId, SourcePath, GeometryDomain;
        public bool SourceActiveSelf, SourceActiveInHierarchy, SourcePhysicalEligible;
        public bool SourceHasRenderer;
        public Mesh SourceMesh;
        public Material[] SourceMaterials=Array.Empty<Material>();
        public Collider[] SourceColliders=Array.Empty<Collider>();
        public string[] SourceColliderFileIds=Array.Empty<string>();
        public float[] SourceWorldMatrix=Array.Empty<float>();
    }
}
