#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Hud;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class Issue256NavigationSmokeTests
    {
        [UnityTest]
        public IEnumerator KnownStationRouteAndToggleAreUsable()
        {
            bool oldSetting = GameSettings.ShowRoute;
            GameObject hazardRoot = null;
            FireHazard fire = null;
            IncidentDirector director = null;
            HashSet<Hazard> known = null;
            IncidentDirector.Phase oldStage = IncidentDirector.Phase.Calm;
            Hazard oldMain = null;
            bool oldKnown = false;
            WorldRouteGuide worldGuide = null;
            try
            {
                yield return SceneManager.LoadSceneAsync("FpsStation", LoadSceneMode.Single);
                if (!SceneManager.GetSceneByName("StationEmergency").isLoaded)
                    yield return SceneManager.LoadSceneAsync("StationEmergency", LoadSceneMode.Additive);
                for (int i = 0; i < 240 && (EmergencySession.Current == null || EmergencySession.Current.World == null); i++) yield return null;
                var session = EmergencySession.Current;
                Assert.That(session, Is.Not.Null);
                Assert.That(session.World, Is.Not.Null);
                Assert.That(session.Map, Is.Not.Null);
                Assert.That(session.Map.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text == "맞이방"), Is.True);
                Assert.That(session.Map.transform.Find("음영"), Is.Null, "안내도가 화면 전체를 어둡게 덮지 않아야 한다");
                var card = session.Map.transform.Find("지도 카드").GetComponent<Image>();
                Assert.That(card.sprite, Is.Not.Null);
                Assert.That(card.color.r, Is.GreaterThan(.8f));
                worldGuide = session.GetComponentInChildren<WorldRouteGuide>(true);
                Assert.That(worldGuide, Is.Not.Null);
                Assert.That(worldGuide.Visible, Is.False);

                GameSettings.ShowRoute = false;
                var guide = session.Map.GetComponentsInChildren<TMP_Text>(true).First(t => t.gameObject.name == "길 안내");
                Assert.That(guide.text, Does.Contain("꺼짐"));
                GameSettings.ShowRoute = true;
                Assert.That(guide.text, Does.Contain("사고를 인지하면"));

                var origin = session.World.Points.Of(PointKind.Counter)[0].Position;
                var target = session.World.Points.Of(PointKind.Office)[0].Position;
                hazardRoot = new GameObject("길 안내 시험 화재");
                fire = new FireHazard("route-test", target, "시험", .3f, session.Art, hazardRoot.transform);
                HazardRegistry.Add(fire);
                var route = new List<Vector3>();
                var method = typeof(EmergencySession).GetMethod("TryGuideRoute", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                bool found = (bool)method.Invoke(session, new object[] { origin, fire, route });
                Assert.That(found, Is.True, "실제 역 위치 자료에서 현장 접근 경로를 찾아야 한다");
                Assert.That(route.Count, Is.GreaterThanOrEqualTo(2));
                AssertRouteClear(session, route);

                // The same incident route must appear on the floor without opening M, and the setting must hide both views.
                director = session.Incidents;
                oldStage = director.Stage;
                oldMain = director.Main;
                oldKnown = director.PlayerKnowsIncident;
                known = (HashSet<Hazard>)typeof(IncidentDirector).GetField("known", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director);
                known.Add(fire);
                SetAutoProperty(director, "Stage", IncidentDirector.Phase.Incident);
                SetAutoProperty(director, "Main", fire);
                SetAutoProperty(director, "PlayerKnowsIncident", true);
                var controller = session.Player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                session.Player.transform.position = origin;
                if (controller != null) controller.enabled = true;
                typeof(EmergencySession).GetMethod("RefreshGuide", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
                Assert.That(worldGuide.Visible, Is.True, "사고를 인지하면 눈앞에 이동 점선이 나타나야 한다");
                var routeMeshes = worldGuide.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh).ToArray();
                Assert.That(routeMeshes.Length, Is.EqualTo(2));
                Assert.That(routeMeshes.All(mesh => mesh.vertexCount > 6), Is.True, "점선과 화살표의 외곽 및 밝은 면이 모두 생성되어야 한다");
                Assert.That(guide.text, Does.Contain("접근"));
                GameSettings.ShowRoute = false;
                Assert.That(worldGuide.Visible, Is.False);
                Assert.That(guide.text, Does.Contain("꺼짐"));
                GameSettings.ShowRoute = true;
                Assert.That(worldGuide.Visible, Is.True);

                var platform = session.World.Points.Of(PointKind.PlatformWait)[0].Position;
                route.Clear();
                found = (bool)method.Invoke(session, new object[] { platform, fire, route });
                Assert.That(found, Is.True, "승강장에서 2층으로 올라오는 연결 경로를 찾아야 한다");
                Assert.That(route.Any(point => MapOverlay.Floor(point) == 1), Is.True);
                Assert.That(route.Any(point => MapOverlay.Floor(point) == 2), Is.True);
                AssertRouteClear(session, route);

                var blocked = route[route.Count / 2];
                session.World.Closed.Add((blocked, 3f, "시험 통제 구역"));
                route.Clear();
                found = (bool)method.Invoke(session, new object[] { platform, fire, route });
                if (found) AssertRouteClear(session, route);
                session.World.Closed.RemoveAt(session.World.Closed.Count - 1);
            }
            finally
            {
                if (director != null)
                {
                    known?.Remove(fire);
                    SetAutoProperty(director, "Stage", oldStage);
                    SetAutoProperty(director, "Main", oldMain);
                    SetAutoProperty(director, "PlayerKnowsIncident", oldKnown);
                }
                if (fire != null) { HazardRegistry.Remove(fire); fire.End(); }
                if (hazardRoot != null) Object.Destroy(hazardRoot);
                if (worldGuide != null) worldGuide.Clear();
                GameSettings.ShowRoute = oldSetting;
            }
        }

        private static void SetAutoProperty<T>(IncidentDirector director, string name, T value)
        {
            var field = typeof(IncidentDirector).GetField("<" + name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(director, value);
        }

        private static void AssertRouteClear(EmergencySession session, IReadOnlyList<Vector3> route)
        {
            var clear = typeof(EmergencySession).GetMethod("GuidePointClear", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 1; i < route.Count; i++)
            {
                float length = Vector3.Distance(route[i - 1], route[i]);
                for (float distance = 0; distance <= length; distance += .5f)
                {
                    Vector3 point = Vector3.Lerp(route[i - 1], route[i], length < .001f ? 0 : distance / length);
                    Assert.That((bool)clear.Invoke(session, new object[] { point }), Is.True, "안내 경로가 통제·위험 구역을 통과했다");
                }
                Assert.That((bool)clear.Invoke(session, new object[] { route[i] }), Is.True);
            }
        }
    }
}
#endif
