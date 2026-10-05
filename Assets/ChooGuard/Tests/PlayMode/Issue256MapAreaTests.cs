#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Hud;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class Issue256MapAreaTests
    {
        private string savedKey;

        [SetUp]
        public void SetUp()
        {
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
            EmergencySession.NextSeed = 20261004;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Environment.SetEnvironmentVariable(JevKey.Variable, savedKey);
            EmergencySession.NextSeed = 0;
            Time.timeScale = 1;
            var clean = SceneManager.CreateScene("Issue256MapCleanup");
            SceneManager.SetActiveScene(clean);
            var emergency = SceneManager.GetSceneByName("StationEmergency");
            if (emergency.isLoaded) yield return SceneManager.UnloadSceneAsync(emergency);
            yield return null;
            var station = SceneManager.GetSceneByName("FpsStation");
            if (station.isLoaded) yield return SceneManager.UnloadSceneAsync(station);
        }

        [UnityTest]
        public IEnumerator LatestWorldMapKeepsSurveyedNamesFixedWithinThe130MetreSquare()
        {
            yield return SceneManager.LoadSceneAsync("FpsStation", LoadSceneMode.Single);
            if (!SceneManager.GetSceneByName("StationEmergency").isLoaded)
                yield return SceneManager.LoadSceneAsync("StationEmergency", LoadSceneMode.Additive);
            for (int i = 0; i < 300 && EmergencySession.Current?.World == null; i++) yield return null;
            var session = EmergencySession.Current;
            Assert.That(session?.World, Is.Not.Null);
            Assert.That(session.StationMapBounds.width, Is.GreaterThan(500), "최신 develop 전역 지도를 사용한다");
            session.Map.Hide();
            var root = new GameObject("전역 지도 시험");
            SceneManager.MoveGameObjectToScene(root, SceneManager.GetSceneByName("StationEmergency"));
            var viewer = new GameObject("지도 시험 플레이어").transform;
            viewer.SetParent(root.transform);
            var map = MapOverlay.Create(root.transform, session.KoreanFont, session.StationMap,
                session.StationMapBounds, viewer, "시험");
            var points = session.World.Points;
            map.SetLandmarks(points);
            map.Toggle();
            RectTransform Label(string id) => map.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "장소 " + id);
            RectTransform Pin(string id) => map.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "장소 위치 " + id);
            var picture = map.GetComponentsInChildren<RawImage>(true).Single(t => t.name == "지도");
            var mapArea = map.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "지도 영역");
            Assert.That(map.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("장소 연결 ") || t.name.StartsWith("주변 범위 경계 ") || t.name == "이름표 표시 범위"), Is.False);
            foreach (var omitted in points.All.Where(p => p.Kind == PointKind.Shop || p.Kind == PointKind.Toilet || p.Kind == PointKind.Counter))
                Assert.That(map.GetComponentsInChildren<Transform>(true).Any(t => t.name == "장소 " + omitted.Id), Is.False, omitted.Id);
            var office = points.Of(PointKind.Office).First();
            var initialImagePosition = picture.rectTransform.anchoredPosition;
            var initialImageSize = picture.rectTransform.sizeDelta;

            viewer.position = new Vector3(64, 7, -2);
            yield return null;
            CaptureForLocalReview(map, "spawn-130m");

            // A square includes its diagonal corner (90m from the player), but excludes anything beyond
            // either 65m axis. Both directions use the same rule; floor changes remain independent.
            viewer.position = office.Position + new Vector3(64, 0, 64);
            yield return null;
            Assert.That(Label(office.Id).gameObject.activeSelf, Is.True, "130×130m 범위는 원형 반경이 아니다");
            foreach (var offset in new[] { new Vector3(65.1f, 0, 0), new Vector3(-65.1f, 0, 0), new Vector3(0, 0, 65.1f), new Vector3(0, 0, -65.1f) })
            {
                viewer.position = office.Position + offset;
                yield return null;
                Assert.That(Label(office.Id).gameObject.activeSelf, Is.False, "65m 축 경계를 벗어난 장소는 숨긴다");
            }
            viewer.position = office.Position + new Vector3(64.9f, 0, 0);
            yield return null;
            Assert.That(Label(office.Id).gameObject.activeSelf, Is.True);
            viewer.position = new Vector3(office.Position.x, 12.2f, office.Position.z);
            yield return null;
            Assert.That(Label(office.Id).gameObject.activeSelf, Is.False, "현재 층의 구조물 이름만 표시한다");

            // The south-gate caption and surveyed pin must stay in the exact same place while the player
            // moves, even when other captions enter/leave the square or a known marker is added.
            var south = points.Zone("southgate");
            viewer.position = (south.min + south.max) * .5f;
            yield return null;
            var captionPosition = Label("zone-southgate-2").anchoredPosition;
            var pinPosition = Pin("zone-southgate-2").anchoredPosition;
            var centre = (south.min + south.max) * .5f;
            var expectedPin = new Vector2((centre.x - session.StationMapBounds.xMin) / session.StationMapBounds.width - .5f,
                (centre.z - session.StationMapBounds.yMin) / session.StationMapBounds.height - .5f) * mapArea.sizeDelta;
            Assert.That(Vector2.Distance(pinPosition, expectedPin), Is.LessThan(.01f));
            foreach (var offset in new[] { new Vector3(20, 0, 0), new Vector3(-20, 0, 20), new Vector3(15, 0, -15) })
            {
                viewer.position = centre + offset;
                map.SetMarker("known-test", viewer.position, MarkerKind.Guidance, "알고 있는 이동 지점");
                yield return null;
                Assert.That(Label("zone-southgate-2").gameObject.activeSelf, Is.True);
                Assert.That(Vector2.Distance(Label("zone-southgate-2").anchoredPosition, captionPosition), Is.LessThan(.01f), "이름표가 플레이어를 따라 이동하면 안 된다");
                Assert.That(Vector2.Distance(Pin("zone-southgate-2").anchoredPosition, pinPosition), Is.LessThan(.01f));
                Assert.That(picture.rectTransform.anchoredPosition, Is.EqualTo(initialImagePosition));
                Assert.That(picture.rectTransform.sizeDelta, Is.EqualTo(initialImageSize));
            }
            map.RemoveMarker("known-test");

            foreach (var zone in points.Zones.Where(z => z.id != "world" && z.id != "tracks"))
            {
                viewer.position = (zone.min + zone.max) * .5f;
                yield return null;
                Assert.That(Label("zone-" + zone.id + "-" + MapOverlay.Floor(viewer.position)).gameObject.activeSelf, Is.True, zone.id);
                Assert.That(picture.enabled, Is.True, "야외/다른 층에서도 전역 지도는 유지한다");
                AssertCaptionsFitAndDoNotOverlap(map);
                CaptureForLocalReview(map, zone.id);
            }
            foreach (var point in points.All.Where(p => p.Kind == PointKind.Office || p.Kind == PointKind.Exit))
            {
                viewer.position = point.Position;
                yield return null;
                Assert.That(Label(point.Id).gameObject.activeSelf, Is.True, point.Id);
                AssertCaptionsFitAndDoNotOverlap(map);
            }
            foreach (var escalator in points.Escalators)
            {
                if (escalator.stairs)
                {
                    viewer.position = escalator.stairsTop;
                    yield return null;
                    Assert.That(Label(escalator.id + "-stairs-top").gameObject.activeSelf, Is.True, escalator.id);
                    AssertCaptionsFitAndDoNotOverlap(map);
                    CaptureForLocalReview(map, escalator.id);
                    viewer.position = escalator.stairsBottom;
                    yield return null;
                    Assert.That(Label(escalator.id + "-stairs-bottom").gameObject.activeSelf, Is.True, escalator.id);
                }
                else
                {
                    viewer.position = escalator.path[0];
                    yield return null;
                    Assert.That(Label(escalator.id).gameObject.activeSelf, Is.True, escalator.id);
                }
                AssertCaptionsFitAndDoNotOverlap(map);
            }
            foreach (var platform in points.Platforms)
            {
                var fixedPin = Pin("platform-" + platform.id).anchoredPosition;
                foreach (var at in new[] { platform.a, platform.b, (platform.a + platform.b) * .5f })
                {
                    viewer.position = at;
                    yield return null;
                    Assert.That(Label("platform-" + platform.id).gameObject.activeSelf, Is.True, platform.id + ": 긴 승강장 끝에서도 이름을 표시한다");
                    Assert.That(Pin("platform-" + platform.id).anchoredPosition, Is.EqualTo(fixedPin));
                    AssertCaptionsFitAndDoNotOverlap(map);
                }
            }
            var main = points.Zone("main2f");
            viewer.position = new Vector3(main.max.x - 1, 7, main.max.z - 1);
            yield return null;
            Assert.That(Label("zone-main2f-2").gameObject.activeSelf, Is.True, "넓은 본관의 끝에서도 구조물 면적이 범위에 들어오면 이름을 표시한다");
            foreach (var elevator in points.Elevators)
                foreach (var stop in elevator.stops)
                {
                    viewer.position = stop.door;
                    yield return null;
                    Assert.That(Label(elevator.id + "-" + stop.floor).gameObject.activeSelf, Is.True);
                }

            // All nearby places obey the same axes, and the filtering square remains 130m
            // in both world directions (the world map is rectangular but its pixel projection is uniform).
            viewer.position = new Vector3(64, 7, -2);
            yield return null;
            foreach (var point in points.All.Where(p => p.Kind == PointKind.Office || p.Kind == PointKind.Exit))
            {
                bool inSquare = MapOverlay.Floor(point.Position) == 2 && Math.Abs(point.Position.x - 64) <= 65 && Math.Abs(point.Position.z + 2) <= 65;
                Assert.That(Label(point.Id).gameObject.activeSelf, Is.EqualTo(inSquare), point.Id);
            }
            Assert.That(mapArea.sizeDelta.x / session.StationMapBounds.width, Is.EqualTo(mapArea.sizeDelta.y / session.StationMapBounds.height).Within(.001f));
            CaptureForLocalReview(map, "spawn-130m");

            map.Hide();
            viewer.position = new Vector3(-150, 0, 80);
            yield return null;
            map.Toggle();
            yield return null;
            Assert.That(Label(office.Id).gameObject.activeSelf, Is.False);
            Assert.That(Label("zone-busstop-1").gameObject.activeSelf, Is.True);
            Assert.That(picture.enabled, Is.True);
            CaptureForLocalReview(map, "outdoor-west");
            viewer.position = new Vector3(-250, 0, -100);
            yield return null;
            Assert.That(picture.enabled, Is.True);
            Assert.That(picture.rectTransform.sizeDelta, Is.EqualTo(initialImageSize));
            CaptureForLocalReview(map, "outdoor-far-west");

            viewer.position = new Vector3(64, 7, -2);
            map.SetRoute(new[] { new Vector3(session.StationMapBounds.xMin - 50, 7, -2), new Vector3(session.StationMapBounds.xMax + 50, 7, -2) }, "시험 경로");
            yield return null;
            var line = map.GetComponentsInChildren<Image>(true).Single(i => i.name == "이동 경로" && i.gameObject.activeSelf);
            Assert.That(line.rectTransform.sizeDelta.x, Is.EqualTo(mapArea.sizeDelta.x).Within(.01f), "지도 밖 경로도 보이는 부분은 잘라서 그린다");
            map.Hide();

            var rampExit = points.Of(PointKind.Exit).First(p => p.Position.y > 4 && p.Position.y < 5.5f);
            session.Player.enabled = false;
            session.Player.GetComponent<CharacterController>().enabled = false;
            session.Player.transform.position = rampExit.Position;
            session.Map.Toggle();
            yield return null;
            Assert.That(session.Map.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "층").text, Does.Contain("1층"), "플레이어 눈높이 대신 발 위치로 층을 판단한다");
            Assert.That(session.Map.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "장소 " + rampExit.Id).gameObject.activeSelf, Is.True);
            Object.Destroy(root);
        }

        private static void CaptureForLocalReview(MapOverlay map, string name)
        {
            var directory = System.Environment.GetEnvironmentVariable("CG_ISSUE256_MAP_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            // Batchmode has no Game View render texture. Render the real uGUI canvas through a temporary
            // camera instead; this opt-in local artifact does not affect CI or the simulation camera.
            var canvas = map.GetComponent<Canvas>();
            var cameraObject = new GameObject("지도 검수 렌더");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 450;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.13f, .21f, .23f);
            camera.cullingMask = 1 << 31;
            var transforms = map.GetComponentsInChildren<Transform>(true);
            var layers = transforms.Select(t => t.gameObject.layer).ToArray();
            for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = 31;
            var render = RenderTexture.GetTemporary(1440, 900, 24);
            var previous = RenderTexture.active;
            var texture = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                camera.targetTexture = render;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = render;
                texture.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
                texture.Apply();
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(render);
                Object.Destroy(texture);
                Object.Destroy(cameraObject);
            }
        }

        private static void AssertCaptionsFitAndDoNotOverlap(MapOverlay map)
        {
            CaptureForLocalReview(map, "last-layout-check");
            var area = map.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "지도 영역");
            var labels = map.GetComponentsInChildren<RectTransform>(true)
                .Where(t => t.name.StartsWith("장소 ") && t.Find("장소") != null && t.gameObject.activeSelf).ToArray();
            for (int i = 0; i < labels.Length; i++)
            {
                var caption = labels[i].GetComponentInChildren<TMP_Text>();
                caption.ForceMeshUpdate();
                Assert.That(caption.isTextOverflowing, Is.False, labels[i].name + ": 이름표 문구가 잘리면 안 된다");
                Assert.That(caption.isTextTruncated, Is.False, labels[i].name + ": 이동 시설의 종류까지 읽을 수 있어야 한다");
                var a = new Rect(labels[i].anchoredPosition - labels[i].sizeDelta * .5f, labels[i].sizeDelta);
                Assert.That(a.xMin, Is.GreaterThanOrEqualTo(-area.sizeDelta.x * .5f), labels[i].name);
                Assert.That(a.xMax, Is.LessThanOrEqualTo(area.sizeDelta.x * .5f), labels[i].name);
                Assert.That(a.yMin, Is.GreaterThanOrEqualTo(-area.sizeDelta.y * .5f), labels[i].name);
                Assert.That(a.yMax, Is.LessThanOrEqualTo(area.sizeDelta.y * .5f), labels[i].name);
                for (int j = i + 1; j < labels.Length; j++)
                {
                    var b = new Rect(labels[j].anchoredPosition - labels[j].sizeDelta * .5f, labels[j].sizeDelta);
                    Assert.That(a.Overlaps(b), Is.False, labels[i].name + " " + a + " / " + labels[j].name + " " + b);
                }
            }
        }
    }
}
#endif
