using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Hud
{
    /// <summary>M toggles a top-down floor map rendered from the station twin, with the player and known markers.</summary>
    public sealed class MapOverlay : MonoBehaviour
    {
        private const float Size = 760;
        private Canvas canvas;
        private RectTransform mapRect, player;
        private Rect world;
        private Transform viewer;
        private TMP_FontAsset font;
        private readonly Dictionary<string, (RectTransform rect, Image icon, TMP_Text label)> markers = new Dictionary<string, (RectTransform, Image, TMP_Text)>();
        private readonly Dictionary<string, (Vector3 world, MarkerKind kind, string label)> sources = new Dictionary<string, (Vector3, MarkerKind, string)>();

        public bool Visible => canvas.enabled;

        public static MapOverlay Create(Transform parent, TMP_FontAsset font, Texture map, Rect worldXZ, Transform viewer, string floorLabel)
        {
            var canvasObject = FpsUiFactory.Canvas("역사 안내도", parent, 21);
            var overlay = canvasObject.AddComponent<MapOverlay>();
            overlay.canvas = canvasObject.GetComponent<Canvas>();
            overlay.world = worldXZ;
            overlay.viewer = viewer;
            overlay.font = font;
            var root = (RectTransform)canvasObject.transform;
            var shade = FpsUiFactory.Panel(root, "음영", Vector2.zero, Vector2.zero, new Color(0, 0, 0, .6f));
            FpsUiFactory.Stretch(shade.rectTransform);
            float aspect = worldXZ.height > 0 ? worldXZ.width / worldXZ.height : 1;
            var size = aspect >= 1 ? new Vector2(Size, Size / aspect) : new Vector2(Size * aspect, Size);
            var frame = FpsUiFactory.Panel(root, "지도 테두리", Vector2.zero, size + new Vector2(12, 12), FpsUiFactory.PanelStrong);
            var picture = FpsUiFactory.Picture(root, "지도", map);
            overlay.mapRect = picture.rectTransform;
            overlay.mapRect.sizeDelta = size;
            picture.color = map != null ? Color.white : new Color(.1f, .1f, .1f, 1);
            var title = FpsUiFactory.Label(root, font, "층", new Vector2(0, size.y * .5f + 26), new Vector2(size.x, 30), 20, TextAlignmentOptions.Left);
            title.text = floorLabel;
            title.fontStyle = FontStyles.Bold;
            var north = FpsUiFactory.Label(root, font, "북쪽", new Vector2(size.x * .5f - 20, size.y * .5f + 26), new Vector2(60, 30), 16, TextAlignmentOptions.Right);
            north.text = "▲ 북";
            var arrow = FpsUiFactory.Panel(overlay.mapRect, "현재 위치", Vector2.zero, new Vector2(18, 18), FpsUiFactory.Staff);
            arrow.sprite = HudSprites.Diamond;
            overlay.player = arrow.rectTransform;
            var nose = FpsUiFactory.Panel(overlay.player, "진행 방향", new Vector2(0, 12), new Vector2(6, 10), FpsUiFactory.Staff);
            nose.raycastTarget = false;
            overlay.canvas.enabled = false;
            return overlay;
        }

        public void Toggle() => canvas.enabled = !canvas.enabled;
        public void Hide() => canvas.enabled = false;

        public void SetMarker(string id, Vector3 position, MarkerKind kind, string label) => sources[id] = (position, kind, label);
        public void RemoveMarker(string id)
        {
            sources.Remove(id);
            if (markers.TryGetValue(id, out var view)) { Destroy(view.rect.gameObject); markers.Remove(id); }
        }

        private Vector2 ToMap(Vector3 position)
        {
            float u = (position.x - world.xMin) / world.width - .5f;
            float v = (position.z - world.yMin) / world.height - .5f;
            return new Vector2(u * mapRect.sizeDelta.x, v * mapRect.sizeDelta.y);
        }

        private void LateUpdate()
        {
            if (!canvas.enabled || viewer == null) return;
            player.anchoredPosition = ToMap(viewer.position);
            player.localRotation = Quaternion.Euler(0, 0, -CompassBar.Heading(viewer.forward));
            foreach (var pair in sources)
            {
                if (!markers.TryGetValue(pair.Key, out var view))
                {
                    var icon = FpsUiFactory.Panel(mapRect, "표식 " + pair.Key, Vector2.zero, new Vector2(16, 16), Color.white);
                    icon.sprite = HudSprites.Diamond;
                    var label = FpsUiFactory.Label(icon.rectTransform, font, "이름", new Vector2(0, -18), new Vector2(180, 20), 13);
                    view = (icon.rectTransform, icon, label);
                    markers.Add(pair.Key, view);
                }
                view.rect.anchoredPosition = ToMap(pair.Value.world);
                view.icon.color = CompassBar.Colour(pair.Value.kind);
                view.label.text = pair.Value.label;
                view.label.color = CompassBar.Colour(pair.Value.kind);
            }
        }
    }
}
