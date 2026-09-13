using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.Foundation.Multiplayer
{
    public sealed class WorldSmokeView : MonoBehaviour
    {
        private readonly Dictionary<string, GameObject> volumes = new Dictionary<string, GameObject>();
        private Material material;
        private MaterialPropertyBlock properties;
        private ConnectedWorldRuntime world;
        private FieldView latest;
        public void Configure(Shader shader, Camera camera, ConnectedWorldRuntime connected)
        {
            world = connected;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            if (shader != null) material = new Material(shader);
            if (camera != null) camera.depthTextureMode |= DepthTextureMode.Depth;
            properties = new MaterialPropertyBlock();
        }
        public void Apply(FieldView view) { latest = view; Draw(); }
        private void LateUpdate() { if (world != null && world.Loading) Draw(); }
        private void Draw()
        {
            if (material == null || latest?.Physical?.Smoke == null || world == null) return;
            var present = new HashSet<string>();
            foreach (var observed in latest.Physical.Smoke)
            {
                if (!world.LoadedRegionIds.Contains(observed.RegionId)) continue;
                var cell = observed.Cell; present.Add(cell.Id);
                if (!volumes.TryGetValue(cell.Id, out var volume))
                {
                    volume = GameObject.CreatePrimitive(PrimitiveType.Cube); volume.name = "ObservedSmoke_" + cell.Id; volume.transform.SetParent(transform);
                    var collider = volume.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                    volume.GetComponent<MeshRenderer>().sharedMaterial = material; volumes.Add(cell.Id, volume);
                }
                volume.SetActive(true);
                volume.transform.position = new Vector3((float)cell.CenterX, (float)(cell.CenterY + cell.BoundingHeightM / 2), (float)cell.CenterZ);
                volume.transform.rotation = Quaternion.Euler(0,(float)cell.YawDegrees,0);
                volume.transform.localScale = new Vector3((float)cell.WidthM,(float)cell.BoundingHeightM,(float)cell.DepthM);
                properties.SetVector("_CellDims", new Vector4((float)cell.WidthM,(float)cell.BoundingHeightM,(float)cell.DepthM,0));
                properties.SetVector("_Layer", new Vector4((float)cell.InterfaceHeightAboveMinFloorM,(float)cell.FloorRiseM,(float)cell.SignedFloorGradientX,0));
                properties.SetVector("_Extinction", new Vector4((float)cell.LowerExtinctionPerM,(float)cell.UpperExtinctionPerM,0,0));
                volume.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
            }
            foreach (var volume in volumes) if (!present.Contains(volume.Key)) volume.Value.SetActive(false);
        }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
