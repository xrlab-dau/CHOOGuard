using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ChooGuard.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.EditorTools
{
    /// <summary>
    /// 2층 보행 영역이 어디서 왜 끊겼는지 잰다. 고치지 않는다 - 재기만 한다.
    /// </summary>
    /// <remarks>
    /// 왜 필요한가: 소화기 배치 측정(#242)에서 FE-001·002·007·008 이 각각 **혼자만 있는
    /// 보행 성분**으로 나왔다. 넷 다 본관 서쪽(x -5~-17, z -38 이남)이고, 같은 본관의
    /// 기준점(-22, 7.1, -5)은 가장 큰 성분에 있다. 즉 본관 안에서 z 가 내려가면 끊긴다.
    ///
    /// 같은 곳을 다른 측정도 가리킨다. 수집 하네스의 순찰이 (-27, -43) 근처에서 140 게임초를
    /// 갇혔고, #242 의 성분 측정도 2층을 여러 조각으로 봤다.
    ///
    /// **원인을 가르지 않으면 고칠 수 없다.** 구멍(바닥이 없거나 walkable 이 아님)·문턱(낮은
    /// 턱이 agent climb 보다 높음)·급경사·장애물(콜라이더가 길을 막음)은 고치는 방법이 전부
    /// 다르다. 이 도구는 끊긴 구간을 0.25m 로 짚어 그 넷을 구분할 단서를 낸다.
    ///
    /// 판단은 하지 않고 수치만 남긴다. 해석은 사람이 한다.
    /// </remarks>
    public static class NavMeshGapProbe
    {
        private const float Step = .25f;          // 선을 따라 짚는 간격
        private const float SampleRadius = 1f;    // 이 거리 안에 NavMesh 가 있으면 '위에 있다'
        private const float SameFloor = 1.5f;     // 표본이 이만큼 넘게 어긋나면 다른 층이다
        private const string ScenePath = "Assets/ChooGuard/Scenes/FpsStation.unity";
        private const string OutFolder = ".planning/2026-10-01-navmesh-gaps";

        /// <summary>본관 큰 성분의 기준점. 소화기 측정이 성분 1 로 보고한 자리다.</summary>
        private static readonly Vector3 Main2F = new Vector3(-22f, 7.1f, -5f);

        /// <summary>각자 혼자인 성분으로 나온 네 대. 좌표는 성분 진단이 찍은 값이다.</summary>
        private static readonly string[] IslandNames = { "FE-002", "FE-007", "FE-001", "FE-008" };

        private static readonly Vector3[] IslandSpots =
        {
            new Vector3(-5.8f, 7.1f, -38.9f),
            new Vector3(-8.5f, 7.1f, -53.1f),
            new Vector3(-9.4f, 7.1f, -72.4f),
            new Vector3(-16.6f, 7.1f, -89f),
        };

        [MenuItem("ChooGuard/Emergency/2층 보행 끊김 원인 측정 (#242)")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) { Debug.LogError("[끊김측정] 씬을 열지 못했습니다 · " + ScenePath); return; }

            // **씬의 m_NavMeshData 는 0 이다.** 보행 영역은 실행 중 StationWorld 가
            // NavMesh.AddNavMeshData 로 올린다. 올리지 않고 재면 표본이 전부 실패해
            // '전 구간이 끊겼다' 는 거짓 결과가 나온다 - 2026-10-01 에 실제로 그랬다.
            // ExtinguisherPlacementProbe 가 같은 이유로 같은 일을 한다.
            var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(StationNavigationBuilder.NavMeshPath);
            if (data == null)
            {
                Debug.LogError("[끊김측정] navmesh 자산이 없습니다 · " + StationNavigationBuilder.NavMeshPath);
                return;
            }
            var instance = NavMesh.AddNavMeshData(data);

            var settings = NavMesh.GetSettingsByID(0);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[끊김측정] 에이전트 반지름 {0:F2}m · 키 {1:F2}m · 오를 수 있는 턱 {2:F2}m · 경사 {3:F0}도",
                settings.agentRadius, settings.agentHeight, settings.agentClimb, settings.agentSlope));

            var report = new StringBuilder("{\n  \"agent\": {")
                .Append("\"radius\": ").Append(F(settings.agentRadius))
                .Append(", \"height\": ").Append(F(settings.agentHeight))
                .Append(", \"climb\": ").Append(F(settings.agentClimb))
                .Append(", \"slope\": ").Append(F(settings.agentSlope))
                .Append("},\n  \"pairs\": [\n");

            for (int i = 0; i < IslandSpots.Length; i++)
            {
                report.Append("    ").Append(Walk(Main2F, IslandSpots[i], "main2f", IslandNames[i]));
                report.Append(i < IslandSpots.Length - 1 ? ",\n" : "\n");
            }
            report.Append("  ]\n}\n");

            Directory.CreateDirectory(OutFolder);
            var path = Path.Combine(OutFolder, "measure.json");
            File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
            Debug.Log("CG_GAP_PROBE out=" + path);

            // 올린 것을 내린다. 남겨 두면 다음 측정이 두 벌의 navmesh 위에서 돈다.
            instance.Remove();
        }

        /// <summary>두 점 사이 직선을 짚어 NavMesh 가 끊긴 구간을 모은다.</summary>
        private static string Walk(Vector3 from, Vector3 to, string fromName, string toName)
        {
            var flat = new Vector3(to.x - from.x, 0, to.z - from.z);
            float span = flat.magnitude;
            var direction = flat.normalized;

            var starts = new List<float>();
            var ends = new List<float>();
            var before = new List<float>();
            var after = new List<float>();

            bool onMesh = true;
            float gapStart = 0, yBefore = from.y;

            for (float travelled = 0; travelled <= span; travelled += Step)
            {
                // 높이는 두 끝점을 선형으로 이어 기대값으로 쓴다. 그러지 않으면 표본이
                // 위층·아래층으로 튀어 '길이 있다' 고 잘못 읽는다.
                float expectedY = Mathf.Lerp(from.y, to.y, span > 0 ? travelled / span : 0);
                var here = new Vector3(from.x + direction.x * travelled, expectedY,
                                       from.z + direction.z * travelled);
                bool standing = NavMesh.SamplePosition(here, out var hit, SampleRadius, NavMesh.AllAreas)
                                && Mathf.Abs(hit.position.y - expectedY) <= SameFloor;

                if (onMesh && !standing)
                {
                    onMesh = false;
                    gapStart = travelled;
                    yBefore = expectedY;
                }
                else if (!onMesh && standing)
                {
                    onMesh = true;
                    starts.Add(gapStart); ends.Add(travelled);
                    before.Add(yBefore); after.Add(hit.position.y);
                }
            }
            if (!onMesh)
            {
                starts.Add(gapStart); ends.Add(span);
                before.Add(yBefore); after.Add(to.y);
            }

            var text = new StringBuilder("{\"from\": \"").Append(fromName)
                .Append("\", \"to\": \"").Append(toName)
                .Append("\", \"spanMetres\": ").Append(F(span))
                .Append(", \"gaps\": [");

            for (int i = 0; i < starts.Count; i++)
            {
                float length = ends[i] - starts[i];
                var middle = new Vector3(from.x + direction.x * (starts[i] + length / 2f),
                                         Mathf.Lerp(before[i], after[i], .5f),
                                         from.z + direction.z * (starts[i] + length / 2f));
                string under = Under(middle), around = Around(middle);
                text.Append(i > 0 ? ", " : "").Append("{\"startMetres\": ").Append(F(starts[i]))
                    .Append(", \"lengthMetres\": ").Append(F(length))
                    .Append(", \"dropMetres\": ").Append(F(after[i] - before[i]))
                    .Append(", \"at\": ").Append(V(middle))
                    .Append(", \"under\": ").Append(under)
                    .Append(", \"around\": ").Append(around)
                    .Append('}');

                // 짧은 구간은 벽 모서리의 정상적인 여유일 때가 많다. 0.5m 이상만 눈에 띄게 남긴다.
                if (length >= .5f)
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[끊김측정] {0} -> {1} · {2:F1}m 지점부터 {3:F2}m 끊김 · 높이차 {4:+0.00;-0.00}m · 바닥 {5} · 주변 {6}",
                        fromName, toName, starts[i], length, after[i] - before[i], under, around));
            }
            return text.Append("], \"gapCount\": ").Append(starts.Count).Append('}').ToString();
        }

        /// <summary>그 자리 바로 아래에 무엇이 있는지. 바닥이 아예 없으면 구멍이다.</summary>
        private static string Under(Vector3 point)
        {
            return Physics.Raycast(point + Vector3.up * .5f, Vector3.down, out var hit, 4f, ~0,
                                   QueryTriggerInteraction.Ignore)
                ? "\"" + Escape(hit.collider.gameObject.name) + " "
                  + hit.distance.ToString("F2", CultureInfo.InvariantCulture) + "m\""
                : "null";
        }

        /// <summary>그 자리 주변의 콜라이더 이름들. 길을 막는 물건을 찾는 단서다.</summary>
        private static string Around(Vector3 point)
        {
            var names = new List<string>();
            foreach (var collider in Physics.OverlapSphere(point, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                var name = collider.gameObject.name;
                if (!names.Contains(name)) names.Add(name);
                if (names.Count >= 6) break;
            }
            var text = new StringBuilder("[");
            for (int i = 0; i < names.Count; i++)
                text.Append(i > 0 ? ", " : "").Append('"').Append(Escape(names[i])).Append('"');
            return text.Append(']').ToString();
        }

        private static string F(float value) => value.ToString("F3", CultureInfo.InvariantCulture);

        private static string V(Vector3 v) =>
            "{\"x\": " + F(v.x) + ", \"y\": " + F(v.y) + ", \"z\": " + F(v.z) + "}";

        private static string Escape(string text) =>
            (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
