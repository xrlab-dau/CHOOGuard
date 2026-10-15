using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Hud
{
    public enum MarkerKind { Task, Incident, Responder, Exit, Guidance }

    /// <summary>Top-centre heading strip with world markers. North is Unity +Z (station survey convention: +X east, +Z north).</summary>
    public sealed class CompassBar : MonoBehaviour
    {
        private const float Width = 640, Height = 30, VisibleDegrees = 150;
        private const float PixelsPerDegree = Width / VisibleDegrees;
        private static readonly string[] Cardinal = { "북", "북동", "동", "남동", "남", "남서", "서", "북서" };

        private sealed class Tick { public float Angle; public RectTransform Rect; public Graphic Graphic; }
        private sealed class Marker { public Vector3 World; public MarkerKind Kind; public RectTransform Rect; public Image Icon; public TMP_Text Distance; }

        private readonly List<Tick> ticks = new List<Tick>();
        private readonly Dictionary<string, Marker> markers = new Dictionary<string, Marker>();
        private RectTransform strip, markerLayer;
        private TMP_Text headingText;
        private TMP_FontAsset font;
        private Transform viewer;

        public static CompassBar Create(RectTransform parent, TMP_FontAsset font)
        {
            var root = FpsUiFactory.Node(parent, "나침반");
            FpsUiFactory.Place(root, new Vector2(.5f, 1), new Vector2(0, -16), new Vector2(Width, Height + 40));
            var bar = root.gameObject.AddComponent<CompassBar>();
            bar.font = font;
            var back = FpsUiFactory.Block(root, "바탕", new Vector2(.5f, 1), Vector2.zero, new Vector2(Width, Height), new Color(0, 0, 0, .32f));
            back.gameObject.AddComponent<RectMask2D>();
            bar.strip = back.rectTransform;
            for (int angle = 0; angle < 360; angle += 15)
            {
                bool cardinal = angle % 45 == 0;
                var label = FpsUiFactory.Label(bar.strip, font, "눈금 " + angle, Vector2.zero, new Vector2(56, Height), cardinal ? 17 : 12);
                label.text = cardinal ? Cardinal[angle / 45] : angle.ToString();
                label.color = cardinal ? Color.white : new Color(1, 1, 1, .55f);
                if (cardinal) label.fontStyle = FontStyles.Bold;
                bar.ticks.Add(new Tick { Angle = angle, Rect = label.rectTransform, Graphic = label });
            }
            var pointer = FpsUiFactory.Block(root, "기준선", new Vector2(.5f, 1), new Vector2(0, -Height), new Vector2(2, 8), Color.white);
            pointer.raycastTarget = false;
            bar.headingText = FpsUiFactory.Text(root, font, "방위", new Vector2(.5f, 1), new Vector2(0, -Height - 8), new Vector2(80, 20), 13, TextAlignmentOptions.Top);
            bar.headingText.color = new Color(1, 1, 1, .8f);
            bar.markerLayer = FpsUiFactory.Node(root, "표식");
            FpsUiFactory.Place(bar.markerLayer, new Vector2(.5f, 1), Vector2.zero, new Vector2(Width, Height));
            return bar;
        }

        public void Bind(Transform viewTransform) => viewer = viewTransform;

        public void SetMarker(string id, Vector3 world, MarkerKind kind)
        {
            if (!markers.TryGetValue(id, out var marker))
            {
                var rect = FpsUiFactory.Node(markerLayer, "표식 " + id);
                rect.sizeDelta = new Vector2(16, 16);
                var icon = rect.gameObject.AddComponent<Image>();
                icon.sprite = HudSprites.Diamond;
                icon.raycastTarget = false;
                var distance = FpsUiFactory.Label(rect, font, "거리", new Vector2(0, -26), new Vector2(70, 18), 12);
                marker = new Marker { Rect = rect, Icon = icon, Distance = distance };
                markers.Add(id, marker);
            }
            marker.World = world;
            marker.Kind = kind;
            marker.Icon.color = Colour(kind);
            marker.Distance.color = Colour(kind);
        }

        public void RemoveMarker(string id)
        {
            if (!markers.TryGetValue(id, out var marker)) return;
            Destroy(marker.Rect.gameObject);
            markers.Remove(id);
        }

        public static Color Colour(MarkerKind kind)
        {
            switch (kind)
            {
                case MarkerKind.Incident: return FpsUiFactory.Danger;
                case MarkerKind.Responder: return FpsUiFactory.Staff;
                case MarkerKind.Exit: return new Color(.55f, .95f, .6f, 1);
                case MarkerKind.Guidance: return new Color(.33f, .95f, .88f, 1);
                default: return FpsUiFactory.Accent;
            }
        }

        public static float Heading(Vector3 forward)
        {
            float angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            return angle < 0 ? angle + 360 : angle;
        }

        private void LateUpdate()
        {
            if (viewer == null) return;
            float heading = Heading(viewer.forward);
            headingText.text = Mathf.RoundToInt(heading) % 360 + "°";
            foreach (var tick in ticks)
            {
                float delta = Mathf.DeltaAngle(heading, tick.Angle);
                bool visible = Mathf.Abs(delta) <= VisibleDegrees * .5f;
                tick.Graphic.enabled = visible;
                if (visible) tick.Rect.anchoredPosition = new Vector2(delta * PixelsPerDegree, 0);
            }
            var origin = viewer.position;
            foreach (var marker in markers.Values)
            {
                var toward = marker.World - origin;
                float delta = Mathf.DeltaAngle(heading, Heading(new Vector3(toward.x, 0, toward.z)));
                float x = Mathf.Clamp(delta, -VisibleDegrees * .5f, VisibleDegrees * .5f) * PixelsPerDegree;
                marker.Rect.anchoredPosition = new Vector2(x, -Height * .5f);
                bool edge = Mathf.Abs(delta) > VisibleDegrees * .5f;
                marker.Icon.color = edge ? Colour(marker.Kind) * new Color(1, 1, 1, .5f) : Colour(marker.Kind);
                float metres = new Vector2(toward.x, toward.z).magnitude;
                string floor = Mathf.Abs(toward.y) > 2.5f ? (toward.y > 0 ? " ▲" : " ▼") : "";
                marker.Distance.text = (marker.Kind == MarkerKind.Guidance ? "길 " : "") + Mathf.RoundToInt(metres) + "m" + floor;
            }
        }
    }
}
