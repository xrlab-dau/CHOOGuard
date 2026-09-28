using UnityEngine;

namespace ChooGuard.App.Fps
{
    // One scene binding keeps static station signs depth-tested across dynamic font-atlas rebuilds.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class StationWorldText : MonoBehaviour
    {
        [SerializeField] private Font stationFont;
        [SerializeField] private Material worldMaterial;

        private void OnEnable()
        {
            Font.textureRebuilt += OnFontTextureRebuilt;
            BindSigns();
        }

        private void OnDisable() => Font.textureRebuilt -= OnFontTextureRebuilt;

        public void Configure(Font font, Material material)
        {
            stationFont = font;
            worldMaterial = material;
            BindSigns();
        }

        private void OnFontTextureRebuilt(Font font)
        {
            if (font == stationFont && worldMaterial != null)
                worldMaterial.mainTexture = stationFont.material.mainTexture;
        }

        private void BindSigns()
        {
            if (stationFont == null || worldMaterial == null) return;
            worldMaterial.mainTexture = stationFont.material.mainTexture;
            foreach (var text in FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text.font != stationFont) continue;
                var renderer = text.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = worldMaterial;
            }
        }
    }
}
