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
        public const float NearbyLabelSpan = 130f;
        private static readonly Color Ink = new Color(.09f, .18f, .23f, 1);
        private Canvas canvas;
        private RectTransform mapRect, player;
        private RawImage picture;
        private RectTransform background;
        private TMP_Text title, guideText, floorNotice, transferText;
        private Image transferChip;
        private sealed class RouteSegment { public Image Back, Line; }
        private readonly List<RouteSegment> routeLines = new List<RouteSegment>();
        private sealed class Landmark
        {
            public RectTransform View, Pin;
            public Vector3 Position;
            public int Floor, Priority;
            public Vector2 Anchor;
            public Rect? Area;
            public StationPoints.PlatformEntry Platform;
            public TMP_Text Caption;
            public string Label, AccessType;
            public bool Visible;
            public CaptionGroup Group;
        }
        private readonly List<Landmark> landmarks = new List<Landmark>();
        private sealed class CaptionGroup
        {
            public Landmark Owner;
            public readonly List<Landmark> Members = new List<Landmark>();
            public Vector2 Anchor;
        }
        private readonly List<CaptionGroup> captionGroups = new List<CaptionGroup>();
        private readonly List<CaptionGroup> nearby = new List<CaptionGroup>();
        // A group is a compact local cluster, never a chain spanning an entire platform row.
        private const float AccessClusterMetres = 28;

        private readonly List<Rect> occupiedLabels = new List<Rect>();
        private readonly List<Vector3> route = new List<Vector3>();
        private Rect world;
        private Transform viewer;
        private Transform positionSource;
        private Vector3 ViewerPosition => positionSource != null ? positionSource.position : viewer.position;
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
            // The camera's eye height must not classify a 1F ramp as 2F or a stair landing as 3F.
            overlay.positionSource = viewer != null ? (viewer.GetComponentInParent<FirstPersonResponder>()?.transform ?? viewer) : null;
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
            overlay.mapRect = FpsUiFactory.Panel(root, "지도 영역", Vector2.zero, size, new Color(.84f, .91f, .92f, 1)).rectTransform;
            overlay.mapRect.anchoredPosition = new Vector2(0, -5);
            overlay.mapRect.gameObject.AddComponent<RectMask2D>();
            overlay.background = FpsUiFactory.Node(overlay.mapRect, "지도 바탕");
            FpsUiFactory.Stretch(overlay.background);
            overlay.picture = FpsUiFactory.Picture(overlay.background, "지도", map);
            overlay.picture.rectTransform.sizeDelta = size;
            overlay.picture.color = map != null ? Color.white : new Color(.9f, .93f, .94f, 1);
            var photoWash = FpsUiFactory.Panel(overlay.picture.transform, "지도 밝기", Vector2.zero, Vector2.zero, new Color(.9f, .96f, 1, .12f));
            FpsUiFactory.Stretch(photoWash.rectTransform);
            overlay.title = FpsUiFactory.Label(root, font, "층", new Vector2(-42, size.y * .5f + 43), new Vector2(size.x - 100, 38), 23, TextAlignmentOptions.Left);
            overlay.title.color = Ink;
            overlay.title.text = floorLabel;
            overlay.title.fontStyle = FontStyles.Bold;
            overlay.floorNotice = FpsUiFactory.Label(root, font, "다른 층 안내", new Vector2(0, size.y * .5f + 12), new Vector2(size.x - 30, 24), 16);
            overlay.floorNotice.text = "";
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
            foreach (var item in landmarks)
            {
                Destroy(item.View.gameObject);
                Destroy(item.Pin.gameObject);
            }
            landmarks.Clear();
            captionGroups.Clear();
            if (points == null) return;
            // Keep the latest full-world image and its surveyed projection together. Changing the player's
            // position changes which names are visible, never the map extent or a place's coordinates.
            foreach (var zone in points.Zones)
            {
                if (zone.id == "world" || zone.id == "tracks") continue; // Extent and duplicate platform coverage.
                var at = (zone.min + zone.max) * .5f;
                var floors = new HashSet<int> { Floor(at) };
                foreach (var point in points.All) if (point.Zone == zone.id) floors.Add(Floor(point.Position));
                var area = Rect.MinMaxRect(zone.min.x, zone.min.z, zone.max.x, zone.max.z);
                foreach (int floor in floors)
                {
                    string label = zone.label.Replace("2층 ", "").Replace("(타는 곳)", "").Replace("(나가는 곳)", "");
                    if (zone.id == "skyplaza") label = "하늘광장";
                    else if (zone.id == "plaza") label = "역 광장";
                    else if (zone.id == "busstop") label = "버스정류장";
                    AddLandmark("zone-" + zone.id + "-" + floor, label, at, floor, 0, area);
                }
            }
            // A full-world overview needs destinations, not a directory of every shop and facility.
            foreach (var point in points.All)
            {
                if (point.Kind != PointKind.Office && point.Kind != PointKind.Exit) continue;
                string label = point.Label;
                if (point.Id == "exit-west-north") label = "초량 방면";
                else if (point.Id == "exit-west-south") label = "중앙동 방면";
                else if (point.Id == "exit-port") label = "부산항 방면";
                AddLandmark(point.Id, label, point.Position, Floor(point.Position), 1);
            }
            foreach (var link in points.Escalators)
            {
                if (link.path == null || link.path.Length < 2) continue;
                var from = link.path[0];
                var to = link.path[link.path.Length - 1];
                if (link.stairs)
                {
                    // One caption for each surveyed stair access; the adjacent escalator is not a
                    // second overlapping destination. Use the survey's platform number on both floors.
                    string destination = link.label.Split(new[] { " 타는 곳" }, System.StringSplitOptions.None)[0];
                    string label = destination + "번 계단";
                    AddLandmark(link.id + "-stairs-top", label, link.stairsTop, Floor(link.stairsTop), 2).AccessType = "계단";
                    AddLandmark(link.id + "-stairs-bottom", label, link.stairsBottom, Floor(link.stairsBottom), 2).AccessType = "계단";
                }
                else
                    AddLandmark(link.id, FloorLabel(to).Replace(" 타는 곳", "번") + "\n에스컬레이터", from, Floor(from), 2).AccessType = "에스컬레이터";
            }
            foreach (var elevator in points.Elevators)
                if (elevator.stops != null) foreach (var stop in elevator.stops)
                    AddLandmark(elevator.id + "-" + stop.floor, "승강기", stop.door, Floor(stop.door), 2).AccessType = "승강기";
            foreach (var platform in points.Platforms)
            {
                var item = AddLandmark("platform-" + platform.id, platform.label.Replace(" 타는 곳", "번\n승강장"), (platform.a + platform.b) * .5f, Floor(platform.a), 0);
                item.Platform = platform;
            }
            BuildCaptionGroups();
            LayoutLandmarks();
            drawnFloor = 0;
        }

        private Landmark AddLandmark(string id, string label, Vector3 position, int floor, int priority, Rect? area = null)
        {
            var backing = FpsUiFactory.Panel(mapRect, "장소 " + id, Vector2.zero, new Vector2(100, 22), new Color(.96f, .98f, .98f, .90f));
            backing.sprite = HudSprites.RoundedPanel;
            backing.type = Image.Type.Sliced;
            var dot = FpsUiFactory.Panel(mapRect, "장소 위치 " + id, Vector2.zero, new Vector2(4, 4), new Color(.18f, .36f, .40f, .75f));
            dot.sprite = HudSprites.Disc;
            var caption = FpsUiFactory.Label(backing.transform, font, "장소", Vector2.zero, new Vector2(90, 20), priority == 0 ? 13 : 12);
            caption.text = label;
            caption.color = Ink;
            caption.fontStyle = priority == 0 ? FontStyles.Bold : FontStyles.Normal;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            var preferred = caption.GetPreferredValues(label);
            float width = Mathf.Clamp(preferred.x + 12, 42, 180);
            float height = Mathf.Max(22, Mathf.Ceil(preferred.y + 5));
            backing.rectTransform.sizeDelta = new Vector2(width, height);
            caption.rectTransform.sizeDelta = new Vector2(width - 8, height - 2);
            var item = new Landmark { View = backing.rectTransform, Pin = dot.rectTransform,
                Position = position, Floor = floor, Priority = priority, Area = area, Caption = caption, Label = label };
            SetLandmarkActive(item, false);
            landmarks.Add(item);
            return item;
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
            int floor = viewer != null ? Floor(ViewerPosition) : 2;
            transferChip.gameObject.SetActive(false);
            if (viewer != null)
            {
                for (int i = 1; i < route.Count; i++)
                {
                    if (Floor(route[i - 1]) == floor && Floor(route[i]) != floor && Inside(route[i - 1], floor))
                    {
                        transferChip.rectTransform.anchoredPosition = ToMap(route[i - 1], floor);
                        transferText.text = FloorLabel(route[i]) + " 이동 " + (route[i].y < route[i - 1].y ? "▼" : "▲");
                        transferChip.gameObject.SetActive(true);
                    }
                    if (Floor(route[i - 1]) != floor || Floor(route[i]) != floor) continue;
                    Vector2 a = ToMap(route[i - 1], floor), b = ToMap(route[i], floor);
                    if (!ClipToMap(ref a, ref b)) continue;
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

        private bool ClipToMap(ref Vector2 a, ref Vector2 b)
        {
            var half = mapRect.sizeDelta * .5f;
            return ClipToRect(ref a, ref b, new Rect(-half, half * 2));
        }

        private static bool ClipToRect(ref Vector2 a, ref Vector2 b, Rect bounds)
        {
            var start = a;
            var delta = b - a;
            float enter = 0, leave = 1;
            bool Edge(float direction, float gap)
            {
                if (Mathf.Abs(direction) < .001f) return gap >= 0;
                float ratio = gap / direction;
                if (direction < 0) enter = Mathf.Max(enter, ratio);
                else leave = Mathf.Min(leave, ratio);
                return enter <= leave;
            }
            if (!Edge(-delta.x, start.x - bounds.xMin) || !Edge(delta.x, bounds.xMax - start.x) ||
                !Edge(-delta.y, start.y - bounds.yMin) || !Edge(delta.y, bounds.yMax - start.y)) return false;
            a = start + delta * enter;
            b = start + delta * leave;
            return true;
        }
        private bool Inside(Vector3 position, int floor)
        {
            var bounds = world;
            return position.x >= bounds.xMin && position.x <= bounds.xMax && position.z >= bounds.yMin && position.z <= bounds.yMax;
        }

        public void SetMarker(string id, Vector3 position, MarkerKind kind, string label) => sources[id] = (position, kind, label);
        public void RemoveMarker(string id)
        {
            sources.Remove(id);
            if (markers.TryGetValue(id, out var view)) { Destroy(view.rect.gameObject); markers.Remove(id); }
        }

        private Vector2 ToMap(Vector3 position, int floor)
        {
            var bounds = world;
            float u = (position.x - bounds.xMin) / bounds.width - .5f;
            float v = (position.z - bounds.yMin) / bounds.height - .5f;
            return new Vector2(u * mapRect.sizeDelta.x, v * mapRect.sizeDelta.y);
        }

        private void LateUpdate()
        {
            if (!canvas.enabled || viewer == null) return;
            int floor = Floor(ViewerPosition);
            picture.enabled = true;
            floorNotice.gameObject.SetActive(true);
            floorNotice.text = "현재 층 · 주변 130 × 130m와 겹치는 주요 구조물";
            title.text = "역 전체 안내도 · " + FloorLabel(ViewerPosition) + " 현재 위치";
            if (floor != drawnFloor) { drawnFloor = floor; DrawRoute(); }
            player.anchoredPosition = ToMap(ViewerPosition, floor);
            player.gameObject.SetActive(Inside(ViewerPosition, floor));
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
                view.rect.anchoredPosition = ToMap(pair.Value.world, floor);
                view.rect.gameObject.SetActive(Inside(pair.Value.world, floor));
                view.icon.color = CompassBar.Colour(pair.Value.kind);
                view.label.text = Floor(pair.Value.world) == floor ? pair.Value.label : FloorLabel(pair.Value.world) + " · " + pair.Value.label;
                view.label.color = CompassBar.Colour(pair.Value.kind);
            }
            UpdateLandmarks(floor);
            if (player.gameObject.activeSelf) player.SetAsLastSibling();
        }

        private void UpdateLandmarks(int floor)
        {
            var at = ViewerPosition;
            float half = NearbyLabelSpan * .5f;
            var scope = new Rect(at.x - half, at.z - half, NearbyLabelSpan, NearbyLabelSpan);
            bool changed = false;
            foreach (var item in landmarks)
            {
                bool within;
                if (item.Area.HasValue)
                {
                    var area = item.Area.Value;
                    within = area.xMin <= scope.xMax && area.xMax >= scope.xMin && area.yMin <= scope.yMax && area.yMax >= scope.yMin;
                }
                else if (item.Platform != null)
                {
                    var platform = item.Platform;
                    var a = new Vector2(platform.a.x, platform.a.z);
                    var b = new Vector2(platform.b.x, platform.b.z);
                    var expanded = new Rect(scope.min - Vector2.one * platform.halfWidth, scope.size + Vector2.one * platform.halfWidth * 2);
                    within = ClipToRect(ref a, ref b, expanded);
                }
                else within = Mathf.Abs(item.Position.x - at.x) <= half && Mathf.Abs(item.Position.z - at.z) <= half;
                // Long structures count when their surveyed footprint touches the square. Their caption
                // and pin still stay at the same world location; never move them towards the player.
                bool visible = item.Floor == floor && Inside(item.Position, floor) && within;
                changed |= item.Visible != visible;
                item.Visible = visible;
                item.Pin.gameObject.SetActive(visible);
            }
            if (!changed) return;
            foreach (var group in captionGroups)
            {
                // The caption has a fixed layout, but names outside the square never leak through a group.
                string text = GroupCaption(group, false);
                group.Owner.View.gameObject.SetActive(text.Length > 0);
                group.Owner.Caption.text = text;
            }
        }

        private void BuildCaptionGroups()
        {
            captionGroups.Clear();
            foreach (var item in landmarks)
            {
                item.Anchor = ToMap(item.Position, item.Floor);
                item.Pin.anchoredPosition = item.Anchor;
                CaptionGroup match = null;
                if (!string.IsNullOrEmpty(item.AccessType))
                    foreach (var group in captionGroups)
                    {
                        if (group.Owner.Floor != item.Floor || group.Owner.AccessType != item.AccessType) continue;
                        bool fits = true;
                        foreach (var member in group.Members)
                        {
                            var delta = new Vector2(member.Position.x - item.Position.x, member.Position.z - item.Position.z);
                            if (delta.sqrMagnitude <= AccessClusterMetres * AccessClusterMetres) continue;
                            fits = false;
                            break;
                        }
                        if (fits) { match = group; break; }
                    }
                if (match == null)
                {
                    match = new CaptionGroup { Owner = item };
                    captionGroups.Add(match);
                }
                item.Group = match;
                match.Members.Add(item);
            }
            foreach (var group in captionGroups)
            {
                foreach (var member in group.Members) group.Anchor += member.Anchor;
                group.Anchor /= group.Members.Count;
                var owner = group.Owner;
                string text = GroupCaption(group, true);
                var preferred = owner.Caption.GetPreferredValues(text);
                var size = new Vector2(Mathf.Clamp(preferred.x + 12, 42, 180), Mathf.Max(22, Mathf.Ceil(preferred.y + 5)));
                owner.View.sizeDelta = size;
                owner.Caption.rectTransform.sizeDelta = size - new Vector2(8, 2);
                owner.Caption.text = text;
            }
        }

        private static string GroupCaption(CaptionGroup group, bool includeHidden)
        {
            Landmark single = null;
            int count = 0;
            var destinations = new SortedSet<string>();
            var numbers = new SortedSet<int>();
            bool numeric = true;
            foreach (var member in group.Members)
            {
                if (!includeHidden && !member.Visible) continue;
                single = member;
                count++;
                string destination = member.Label.Split('\n')[0];
                if (member.AccessType == "계단") destination = destination.Replace("번 계단", "");
                else if (destination.EndsWith("번")) destination = destination.Substring(0, destination.Length - 1);
                destinations.Add(destination);
                foreach (var token in destination.Split('·'))
                    if (int.TryParse(token, out int number)) numbers.Add(number);
                    else numeric = false;
            }
            if (count == 0) return "";
            if (count == 1 || single.AccessType == "승강기") return single.Label;
            string where = numeric ? string.Join("·", numbers) + "번" : string.Join(" · ", destinations);
            return where + (single.AccessType == "계단" ? " 계단" : "\n" + single.AccessType);
        }

        private void LayoutLandmarks()
        {
            // Lay out every group once. Movement changes membership visibility, not its position or size.
            for (int floor = 1; floor <= 3; floor++)
            {
                nearby.Clear();
                foreach (var group in captionGroups)
                    if (group.Owner.Floor == floor) nearby.Add(group);
                nearby.Sort((a, b) =>
                {
                    int Rank(CaptionGroup group) => group.Owner.Priority == 2 ? 0 : group.Owner.Priority == 1 ? 1 : 2;
                    int priority = Rank(a).CompareTo(Rank(b));
                    return priority != 0 ? priority : string.CompareOrdinal(a.Owner.View.name, b.Owner.View.name);
                });
                occupiedLabels.Clear();
                foreach (var item in landmarks)
                    if (item.Floor == floor) occupiedLabels.Add(new Rect(item.Anchor - Vector2.one * 3, Vector2.one * 6));
                foreach (var group in nearby) PlaceCaption(group);
            }
            foreach (var item in landmarks) item.Pin.SetAsLastSibling();
            foreach (var item in landmarks) item.View.SetAsLastSibling();
        }
        private void PlaceCaption(CaptionGroup group)
        {
            var item = group.Owner;
            var half = item.View.sizeDelta * .5f;
            var limit = mapRect.sizeDelta * .5f - half - Vector2.one * 4;
            var preferred = group.Anchor + new Vector2(0, half.y + 5);
            Vector2 chosen = preferred;
            float bestOverlap = float.PositiveInfinity, bestDistance = float.PositiveInfinity;
            void Consider(Vector2 candidate)
            {
                candidate = new Vector2(Mathf.Clamp(candidate.x, -limit.x, limit.x), Mathf.Clamp(candidate.y, -limit.y, limit.y));
                var rect = new Rect(candidate - half - Vector2.one, item.View.sizeDelta + Vector2.one * 2);
                float overlap = 0;
                foreach (var occupied in occupiedLabels)
                    overlap += Mathf.Max(0, Mathf.Min(rect.xMax, occupied.xMax) - Mathf.Max(rect.xMin, occupied.xMin)) *
                               Mathf.Max(0, Mathf.Min(rect.yMax, occupied.yMax) - Mathf.Max(rect.yMin, occupied.yMin));
                float distance = (candidate - preferred).sqrMagnitude;
                if (overlap > bestOverlap || (overlap == bestOverlap && distance >= bestDistance)) return;
                bestOverlap = overlap;
                bestDistance = distance;
                chosen = candidate;
            }
            // Compact, local placement only. Never send a caption across the overview to find a free
            // column: without leader lines its proximity to the surveyed structure is the location cue.
            if (item.Area.HasValue)
            {
                // A region caption may use free space inside that region's surveyed footprint. Point
                // destinations stay local; large areas can absorb captions without long leader lines.
                var area = item.Area.Value;
                var min = ToMap(new Vector3(area.xMin, 0, area.yMin), item.Floor) - half - Vector2.one * 4;
                var max = ToMap(new Vector3(area.xMax, 0, area.yMax), item.Floor) + half + Vector2.one * 4;
                for (float y = min.y; y <= max.y; y += 4)
                    for (float x = min.x; x <= max.x; x += 4) Consider(new Vector2(x, y));
            }
            else if (item.Platform != null)
            {
                // Parallel platforms are too close for five captions at their midpoints. Stagger the
                // captions along their actual strips, once, rather than outside the station footprint.
                var a = ToMap(item.Platform.a, item.Floor);
                var b = ToMap(item.Platform.b, item.Floor);
                for (int i = 10; i <= 90; i += 2)
                    Consider(Vector2.Lerp(a, b, i * .01f) + new Vector2(0, half.y + 5));
            }
            else
                for (int y = -44; y <= 44; y += 4)
                    for (int x = -44; x <= 44; x += 4)
                        if (x * x + y * y <= 44 * 44) Consider(preferred + new Vector2(x, y));
            item.View.anchoredPosition = chosen;
            occupiedLabels.Add(new Rect(chosen - half - Vector2.one, item.View.sizeDelta + Vector2.one * 2));
        }

        private static void SetLandmarkActive(Landmark item, bool visible)
        {
            item.View.gameObject.SetActive(visible);
            item.Pin.gameObject.SetActive(visible);
        }
        private int drawnFloor = 2;
    }
}
