using System.Collections.Generic;
using ChooGuard.App.Fps.Emergency;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Hud
{
    /// <summary>M toggles a top-down floor map rendered from the station twin, with the player and known markers.</summary>
    public sealed class MapOverlay : MonoBehaviour
    {
        private const float Size = 720;
        private static readonly Color Ink = new Color(.09f, .18f, .23f, 1);
        private Canvas canvas;
        private RectTransform mapRect, player;
        private RawImage picture;
        private TMP_Text title, guideText, floorNotice, transferText;
        private Image transferChip;
        private sealed class RouteSegment { public Image Back, Line; }
        private readonly List<RouteSegment> routeLines = new List<RouteSegment>();
        private readonly List<GameObject> landmarks = new List<GameObject>();
        private readonly List<Vector3> route = new List<Vector3>();
        private Rect world;
        private Transform viewer;
        private TMP_FontAsset font;
        private StationPoints stationPoints;
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
            float aspect = worldXZ.height > 0 ? worldXZ.width / worldXZ.height : 1;
            var size = aspect >= 1 ? new Vector2(Size, Size / aspect) : new Vector2(Size * aspect, Size);
            var shadow = FpsUiFactory.Panel(root, "지도 그림자", new Vector2(0, -7), size + new Vector2(58, 154), new Color(.02f, .08f, .11f, .23f));
            shadow.sprite = HudSprites.RoundedPanel;
            shadow.type = Image.Type.Sliced;
            var frame = FpsUiFactory.Panel(root, "지도 카드", Vector2.zero, size + new Vector2(52, 148), new Color(.94f, .96f, .97f, .97f));
            frame.sprite = HudSprites.RoundedPanel;
            frame.type = Image.Type.Sliced;
            var mapBorder = FpsUiFactory.Panel(root, "지도 가장자리", new Vector2(0, -5), size + new Vector2(8, 8), new Color(.22f, .37f, .42f, .85f));
            mapBorder.sprite = HudSprites.RoundedPanel;
            mapBorder.type = Image.Type.Sliced;
            overlay.picture = FpsUiFactory.Picture(root, "지도", map);
            overlay.mapRect = overlay.picture.rectTransform;
            overlay.mapRect.anchoredPosition = new Vector2(0, -5);
            overlay.mapRect.sizeDelta = size;
            overlay.picture.color = map != null ? Color.white : new Color(.9f, .93f, .94f, 1);
            var photoWash = FpsUiFactory.Panel(overlay.mapRect, "지도 밝기", Vector2.zero, Vector2.zero, new Color(.9f, .96f, 1, .12f));
            FpsUiFactory.Stretch(photoWash.rectTransform);
            overlay.title = FpsUiFactory.Label(root, font, "층", new Vector2(-42, size.y * .5f + 43), new Vector2(size.x - 100, 38), 23, TextAlignmentOptions.Left);
            overlay.title.color = Ink;
            overlay.title.text = floorLabel;
            overlay.title.fontStyle = FontStyles.Bold;
            overlay.floorNotice = FpsUiFactory.Label(root, font, "다른 층 안내", Vector2.zero, new Vector2(size.x - 30, 65), 19);
            overlay.floorNotice.text = "이 층의 상세 평면도는 제공되지 않습니다.\n다음 이동 지점은 나침반으로 확인하세요.";
            overlay.floorNotice.color = Ink;
            overlay.floorNotice.gameObject.SetActive(false);
            var footerAccent = FpsUiFactory.Panel(root, "길 안내 색인", new Vector2(-size.x * .5f + 6, -size.y * .5f - 42), new Vector2(5, 37), CompassBar.Colour(MarkerKind.Guidance));
            footerAccent.sprite = HudSprites.RoundedPanel;
            footerAccent.type = Image.Type.Sliced;
            overlay.guideText = FpsUiFactory.Label(root, font, "길 안내", new Vector2(17, -size.y * .5f - 42), new Vector2(size.x - 45, 50), 17, TextAlignmentOptions.Left);
            overlay.guideText.color = Ink;
            var north = FpsUiFactory.Label(root, font, "북쪽", new Vector2(size.x * .5f - 30, size.y * .5f + 43), new Vector2(76, 30), 16, TextAlignmentOptions.Right);
            north.text = "▲ 북";
            north.color = new Color(.22f, .39f, .44f, 1);
            var playerHalo = FpsUiFactory.Panel(overlay.mapRect, "현재 위치 테두리", Vector2.zero, new Vector2(29, 29), Color.white);
            playerHalo.sprite = HudSprites.Ring;
            var arrow = FpsUiFactory.Panel(overlay.mapRect, "현재 위치", Vector2.zero, new Vector2(18, 18), FpsUiFactory.Staff);
            arrow.sprite = HudSprites.Diamond;
            overlay.player = arrow.rectTransform;
            playerHalo.transform.SetParent(overlay.player, false);
            playerHalo.transform.SetAsFirstSibling();
            playerHalo.rectTransform.anchoredPosition = Vector2.zero;
            var nose = FpsUiFactory.Panel(overlay.player, "진행 방향", new Vector2(0, 12), new Vector2(6, 10), FpsUiFactory.Staff);
            nose.raycastTarget = false;
            overlay.transferChip = FpsUiFactory.Panel(overlay.mapRect, "층간 이동 표식", Vector2.zero, new Vector2(150, 30), new Color(.96f, .98f, .98f, .96f));
            overlay.transferChip.sprite = HudSprites.RoundedPanel;
            overlay.transferChip.type = Image.Type.Sliced;
            overlay.transferText = FpsUiFactory.Label(overlay.transferChip.transform, font, "층간 이동", Vector2.zero, new Vector2(142, 27), 15);
            overlay.transferText.color = Ink;
            overlay.transferText.fontStyle = FontStyles.Bold;
            overlay.transferChip.gameObject.SetActive(false);
            overlay.canvas.enabled = false;
            return overlay;
        }

        public void Toggle() => canvas.enabled = !canvas.enabled;
        public void Hide() => canvas.enabled = false;

        public void SetLandmarks(StationPoints points)
        {
            stationPoints = points;
            foreach (var item in landmarks) Destroy(item);
            landmarks.Clear();
            if (points == null) return;
            // The supplied map image is the 2F floor. Labels are bound to surveyed zone/point positions.
            foreach (var zone in points.Zones)
            {
                if (zone.id != "hall2f" && zone.id != "southgate" && zone.id != "northdeck" &&
                    zone.id != "main2f" && zone.id != "eastexit") continue;
                var at = (zone.min + zone.max) * .5f;
                string label;
                switch (zone.id)
                {
                    case "hall2f": label = "맞이방"; break;
                    case "southgate": label = "남측 게이트"; break;
                    case "northdeck": label = "북측 데크"; break;
                    case "main2f": label = "본관"; break;
                    default: label = "동측 출구"; break;
                }
                AddLandmark(label, at, 132);
            }
            var office = points.Of(PointKind.Office);
            if (office.Count > 0) AddLandmark(office[0].Label, office[0].Position, 90);
            foreach (var link in points.Escalators)
            {
                string label = null;
                Vector2 offset = Vector2.zero;
                switch (link.id)
                {
                    case "esc-2f3f-up": label = "3층 연결 ▲"; offset = new Vector2(0, -35); break;
                    case "esc-1f-north-1": label = "1층 연결 ▼"; break;
                    case "esc-1f-south-1": label = "1층 연결 ▼"; break;
                    case "esc-well-s-56": label = "5·6 타는 곳 ▼"; offset = new Vector2(0, 40); break;
                }
                if (label != null && link.path != null && link.path.Length > 0)
                    AddLandmark(label, link.path[0], 112, offset);
            }
        }

        private void AddLandmark(string label, Vector3 position, float width, Vector2 offset = default)
        {
            if (!Inside(position)) return;
            var backing = FpsUiFactory.Panel(mapRect, label, ToMap(position) + offset, new Vector2(width, 28), new Color(.96f, .98f, .98f, .96f));
            backing.sprite = HudSprites.RoundedPanel;
            backing.type = Image.Type.Sliced;
            var dot = FpsUiFactory.Panel(backing.transform, "장소 표식", new Vector2(-width * .5f + 13, 0), new Vector2(7, 7), new Color(.08f, .52f, .56f, 1));
            dot.sprite = HudSprites.Disc;
            var caption = FpsUiFactory.Label(backing.transform, font, "장소", new Vector2(8, 0), new Vector2(width - 26, 25), 15);
            caption.text = label;
            caption.color = Ink;
            caption.fontStyle = FontStyles.Bold;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 12;
            caption.fontSizeMax = 15;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            landmarks.Add(backing.gameObject);
        }

        public void SetRoute(IReadOnlyList<Vector3> points, string message)
        {
            route.Clear();
            if (points != null) for (int i = 0; i < points.Count; i++) route.Add(points[i]);
            guideText.text = message ?? "";
            DrawRoute();
        }

        private void DrawRoute()
        {
            int used = 0;
            transferChip.gameObject.SetActive(false);
            if (viewer != null && Floor(viewer.position) == 2)
            {
                for (int i = 1; i < route.Count; i++)
                {
                    if (Floor(route[i - 1]) == 2 && Floor(route[i]) != 2 && Inside(route[i - 1]))
                    {
                        transferChip.rectTransform.anchoredPosition = ToMap(route[i - 1]);
                        transferText.text = FloorLabel(route[i]) + " 이동 " + (route[i].y < route[i - 1].y ? "▼" : "▲");
                        transferChip.gameObject.SetActive(true);
                    }
                    if (Floor(route[i - 1]) != 2 || Floor(route[i]) != 2 || !Inside(route[i - 1]) || !Inside(route[i])) continue;
                    Vector2 a = ToMap(route[i - 1]), b = ToMap(route[i]);
                    Vector2 delta = b - a;
                    if (delta.sqrMagnitude < 2f) continue;
                    RouteSegment segment;
                    if (used < routeLines.Count) segment = routeLines[used];
                    else
                    {
                        var back = FpsUiFactory.Panel(mapRect, "경로 외곽", Vector2.zero, Vector2.zero, new Color(.04f, .16f, .2f, .9f));
                        var line = FpsUiFactory.Panel(mapRect, "이동 경로", Vector2.zero, Vector2.zero, CompassBar.Colour(MarkerKind.Guidance));
                        back.transform.SetSiblingIndex(1);
                        line.transform.SetSiblingIndex(2);
                        segment = new RouteSegment { Back = back, Line = line };
                        routeLines.Add(segment);
                    }
                    var rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    foreach (var image in new[] { segment.Back, segment.Line })
                    {
                        image.rectTransform.anchoredPosition = (a + b) * .5f;
                        image.rectTransform.localRotation = rotation;
                        image.gameObject.SetActive(true);
                    }
                    segment.Back.rectTransform.sizeDelta = new Vector2(delta.magnitude, 10);
                    segment.Line.rectTransform.sizeDelta = new Vector2(delta.magnitude, 5);
                    used++;
                }
            }
            for (int i = used; i < routeLines.Count; i++)
            {
                routeLines[i].Back.gameObject.SetActive(false);
                routeLines[i].Line.gameObject.SetActive(false);
            }
        }

        public static int Floor(Vector3 position) => position.y < 5.5f ? 1 : position.y > 10.5f ? 3 : 2;

        public string FloorLabel(Vector3 position)
        {
            var platform = stationPoints?.PlatformAt(position);
            if (platform != null) return platform.label;
            return stationPoints?.ZoneAt(position)?.id == "tracks" ? "승강장" : Floor(position) + "층";
        }

        private bool Inside(Vector3 position) => position.x >= world.xMin && position.x <= world.xMax &&
                                                 position.z >= world.yMin && position.z <= world.yMax;

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
            int floor = Floor(viewer.position);
            picture.enabled = floor == 2;
            floorNotice.gameObject.SetActive(floor != 2);
            title.text = floor == 2 ? "2층 안내도 · 현재 위치와 이동 경로" : FloorLabel(viewer.position) + " 현재 위치 · 상세 평면도 없음";
            foreach (var item in landmarks) item.SetActive(floor == 2);
            if (floor != drawnFloor) { drawnFloor = floor; DrawRoute(); }
            player.anchoredPosition = ToMap(viewer.position);
            player.gameObject.SetActive(floor == 2 && Inside(viewer.position));
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
                view.rect.gameObject.SetActive(floor == 2 && Inside(pair.Value.world));
                view.icon.color = CompassBar.Colour(pair.Value.kind);
                view.label.text = Floor(pair.Value.world) == floor ? pair.Value.label : FloorLabel(pair.Value.world) + " · " + pair.Value.label;
                view.label.color = CompassBar.Colour(pair.Value.kind);
            }
            if (player.gameObject.activeSelf) player.SetAsLastSibling();
        }
        private int drawnFloor = 2;
    }
}
