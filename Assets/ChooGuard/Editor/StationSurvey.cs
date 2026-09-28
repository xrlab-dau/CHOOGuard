using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Measures the Busan twin (FpsStation open) for the whole-station world: escalator lanes in the platform wells and
    /// the named escalators, elevator stops from their signs, platform strips, and the KTX set's platform-side doors,
    /// aisles and seats. Everything is found by raycasts and mesh geometry; the seeds below only say where to look.
    /// </summary>
    internal static class StationSurvey
    {
        public const float Ground = 0f, Concourse = 7.02f, Upper = 12.2f;
        public const string KtxPath = "FPSWorld/KTXSource";

        // 트윈 시험 bake(2026-09-26)의 경사 군집에서 잰 우물 계단참(높은 쪽 = 2층, 낮은 쪽 = 승강장).
        // 남측 게이트 우물은 타는 방향(내려감), 북측 데크 우물은 나가는 방향(올라감) — JEV 008 판정 0.95.
        private static readonly (string id, string platform, Vector2 high, Vector2 low, bool down)[] Wells =
        {
            ("well-n-1", "1", new Vector2(39.0f, 66.8f), new Vector2(45.5f, 82.2f), false),
            ("well-n-34", "3·4", new Vector2(62.9f, 69.9f), new Vector2(68.9f, 86.1f), false),
            ("well-n-56", "5·6", new Vector2(82.2f, 73.2f), new Vector2(88.2f, 89.4f), false),
            ("well-n-89", "8·9", new Vector2(107.3f, 77.4f), new Vector2(113.3f, 93.8f), false),
            ("well-n-1011", "10·11", new Vector2(126.5f, 80.7f), new Vector2(132.6f, 97.1f), false),
            ("well-m-1011", "10·11", new Vector2(113.6f, 47.6f), new Vector2(107.7f, 31.8f), false),
            ("well-s-1", "1", new Vector2(1.5f, -29.9f), new Vector2(7.1f, -14.9f), true),
            ("well-s-34", "3·4", new Vector2(20.9f, -37.4f), new Vector2(26.0f, -21.8f), true),
            ("well-s-56", "5·6", new Vector2(36.8f, -43.2f), new Vector2(42.6f, -27.0f), true),
            ("well-s-89", "8·9", new Vector2(57.3f, -51.0f), new Vector2(62.7f, -35.0f), true),
            ("well-s-1011", "10·11", new Vector2(71.0f, -63.0f), new Vector2(76.8f, -47.3f), true),
        };

        // 승강장 띠: KTX 옆 5·6 타는 곳 한 점(69.2, 0, 32.3)을 기준으로 선로 직각 방향 거리(m)와 폭. 선로 횡단 레이 측정값.
        private static readonly (string id, string label, float offset, float halfWidth)[] PlatformStrips =
        {
            ("p1", "1 타는 곳", -43f, 6.5f),
            ("p34", "3·4 타는 곳", -20.5f, 4.5f),
            ("p56", "5·6 타는 곳", -3.5f, 4.5f),
            ("p89", "8·9 타는 곳", 18.5f, 4.5f),
            ("p1011", "10·11 타는 곳", 35.5f, 4.5f),
        };
        private static readonly Vector3 PlatformReference = new Vector3(69.2f, 0, 32.3f);

        public sealed class Lane
        {
            public string Id, Label, From, To;
            public bool Up;
            public float Width;
            /// <summary>Walk-on point on the landing, belt start, belt end, walk-off point (boarding order).</summary>
            public List<Vector3> Path = new List<Vector3>();
            public Vector3 BoxCentre, BoxSize;
            public Quaternion BoxRotation;
            /// <summary>Stairs beside the escalator (navmesh area 5) so people can prefer or avoid them. Zero size when none.</summary>
            public Vector3 StairsCentre, StairsSize;
        }

        private static Transform ktx;
        private static Transform Ktx => ktx != null ? ktx : ktx = GameObject.Find(KtxPath)?.transform ?? throw new InvalidOperationException("KTXSource 없음");

        /// <summary>Unit vector along the track, pointing north (toward Seoul): from car 9 to car 0.</summary>
        public static Vector3 TrackAxis()
        {
            Vector3 Centre(string prefix)
            {
                foreach (Transform car in Ktx) if (car.name.StartsWith(prefix, StringComparison.Ordinal)) return Bounds(car).center;
                throw new InvalidOperationException(prefix + " 차량 없음");
            }
            var axis = Centre("Car00") - Centre("Car09");
            axis.y = 0;
            return axis.normalized;
        }

        public static Bounds Bounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.position, Vector3.zero);
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        // ── 표면 ────────────────────────────────────────────────────────────────

        /// <summary>Walkable surface height at (x, z) closest to <paramref name="expected"/>, ignoring the train. NaN when none within tolerance.</summary>
        public static float Surface(Vector3 xz, float expected, float tolerance)
        {
            var hits = Physics.RaycastAll(new Vector3(xz.x, expected + 2.5f, xz.z), Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NaN, bestDelta = tolerance;
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(Ktx)) continue;
                float delta = Mathf.Abs(hit.point.y - expected);
                if (delta <= bestDelta) { bestDelta = delta; best = hit.point.y; }
            }
            return best;
        }

        // ── 승강장 우물 에스컬레이터 ─────────────────────────────────────────────

        public static List<Lane> Escalators(List<string> log)
        {
            var lanes = new List<Lane>();
            foreach (var well in Wells)
            {
                var lane = WellLane(well.id, well.platform, well.high, well.low, well.down, log);
                if (lane != null) lanes.Add(lane);
            }
            lanes.AddRange(NamedEscalators(log));
            lanes.AddRange(KitEscalators(log));
            return lanes;
        }

        private static Lane WellLane(string id, string platform, Vector2 high2, Vector2 low2, bool down, List<string> log)
        {
            var high = new Vector3(high2.x, Concourse, high2.y);
            var low = new Vector3(low2.x, Ground, low2.y);
            var up = high - low; up.y = 0;
            float length = up.magnitude;
            up /= length;
            var side = new Vector3(up.z, 0, -up.x);
            // 씨앗 선(계단 쪽)을 위에서 아래로 따라 내려가며 표면 높이 윤곽을 잡는다.
            var profile = new List<(float t, float y)>();
            float expected = Concourse;
            for (float t = 0; t <= length + .01f; t += .5f)
            {
                var p = high - up * t;
                float y = Surface(p, expected, 1.1f);
                if (float.IsNaN(y)) continue;
                profile.Add((t, y));
                expected = y;
            }
            if (profile.Count < 6) { log.Add(id + ": 윤곽 없음"); return null; }
            float HeightAt(float t)
            {
                var near = profile.OrderBy(p => Mathf.Abs(p.t - t)).First();
                return near.y;
            }
            // 경사 중간 세 곳에서 가로로 훑어 좁은 차선(난간 사이 약 1 m)을 찾는다.
            var candidates = new List<(float centre, float width, float fraction)>();
            var stairs = new List<(float centre, float width)>();
            float width = 0;
            foreach (float fraction in new[] { .4f, .5f, .6f })
            {
                float t = length * fraction;
                var centre = high - up * t;
                float y = HeightAt(t);
                var runs = new List<(float from, float to)>();
                float start = float.NaN, last = float.NaN;
                for (float s = -4.5f; s <= 4.51f; s += .1f)
                {
                    bool surface = !float.IsNaN(Surface(centre + side * s, y, .3f));
                    if (surface) { if (float.IsNaN(start)) start = s; last = s; }
                    else if (!float.IsNaN(start)) { runs.Add((start, last)); start = float.NaN; }
                }
                if (!float.IsNaN(start)) runs.Add((start, last));
                foreach (var narrow in runs.Where(r => r.to - r.from >= .55f && r.to - r.from <= 1.5f))
                {
                    candidates.Add(((narrow.from + narrow.to) * .5f, narrow.to - narrow.from + .1f, fraction));
                }
                var wide = runs.Where(r => r.to - r.from > 1.5f).OrderByDescending(r => r.to - r.from).FirstOrDefault();
                if (wide.to - wide.from > 1.5f) stairs.Add(((wide.from + wide.to) * .5f, wide.to - wide.from + .1f));
            }
            // 세 단면 중 두 곳 이상에서 같은 자리(0.4 m 안)에 나온 좁은 차선을 에스컬레이터로 본다.
            var cluster = candidates.Select(c => candidates.Where(o => Mathf.Abs(o.centre - c.centre) < .4f).ToList())
                .Where(g => g.Select(o => o.fraction).Distinct().Count() >= 2)
                .OrderByDescending(g => g.Select(o => o.fraction).Distinct().Count()).ThenBy(g => Mathf.Abs(g.Average(o => o.centre))).FirstOrDefault();
            if (cluster == null) { log.Add(id + ": 에스컬레이터 차선 없음 " + string.Join(",", candidates.Select(c => c.centre.ToString("F2")))); return null; }
            float offset = cluster.Average(o => o.centre);
            width = cluster.Max(o => o.width);
            // 차선 중심을 따라 위·아래 계단참(평평한 구간)을 찾는다.
            Vector3 LanePoint(float t) => high - up * t + side * offset;
            float Level(float t, float guess) => Surface(LanePoint(t), guess, .45f);
            float topT = float.NaN, bottomT = float.NaN;
            float guessY = HeightAt(length * .5f);
            for (float t = length * .5f; t >= -6; t -= .25f)
            {
                float y = Level(t, guessY);
                if (float.IsNaN(y)) continue;
                guessY = y;
                if (y >= Concourse - .08f) { topT = t; break; }
            }
            guessY = HeightAt(length * .5f);
            for (float t = length * .5f; t <= length + 6; t += .25f)
            {
                float y = Level(t, guessY);
                if (float.IsNaN(y)) continue;
                guessY = y;
                if (y <= Ground + .08f) { bottomT = t; break; }
            }
            if (float.IsNaN(topT) || float.IsNaN(bottomT)) { log.Add(id + ": 계단참 없음"); return null; }
            var top = LanePoint(topT); top.y = Concourse;
            var bottom = LanePoint(bottomT); bottom.y = Ground;
            var lane = new Lane
            {
                Id = "esc-" + id,
                Label = platform + " 타는 곳 " + (down ? "내려가는" : "올라가는") + " 에스컬레이터",
                Up = !down,
                Width = width,
                From = down ? "concourse" : "platform",
                To = down ? "platform" : "concourse",
            };
            // 타는 쪽 계단참 1 m → 벨트 시작 → 중간 높이 표본 → 벨트 끝 → 내리는 쪽 계단참 1 m.
            var belt = new List<Vector3>();
            float tb = bottomT, tt = topT;
            for (float t = tt; t <= tb + .01f; t += .5f)
            {
                var p = LanePoint(t);
                float y = Level(t, HeightAt(Mathf.Clamp(t, 0, length)));
                p.y = float.IsNaN(y) ? Mathf.Lerp(Concourse, Ground, (t - tt) / (tb - tt)) : y;
                belt.Add(p);
            }
            belt[0] = top;
            belt[belt.Count - 1] = bottom;
            if (!down) belt.Reverse();
            var along = (belt[belt.Count - 1] - belt[0]); along.y = 0; along.Normalize();
            lane.Path.Add(belt[0] - along * 1.0f);
            lane.Path.AddRange(belt);
            lane.Path.Add(belt[belt.Count - 1] + along * 1.0f);
            SetBox(lane, top, bottom, up);
            if (stairs.Count >= 2)
            {
                float s = stairs.Average(x => x.centre), w = stairs.Min(x => x.width);
                var shift = side * (s - offset);
                lane.StairsCentre = lane.BoxCentre + shift;
                lane.StairsSize = new Vector3(w, lane.BoxSize.y, lane.BoxSize.z + .6f);
            }
            return lane;
        }

        private static void SetBox(Lane lane, Vector3 top, Vector3 bottom, Vector3 upAxis)
        {
            var centre = (top + bottom) * .5f;
            float run = Vector3.Distance(new Vector3(top.x, 0, top.z), new Vector3(bottom.x, 0, bottom.z));
            lane.BoxCentre = new Vector3(centre.x, (top.y + bottom.y) * .5f + .6f, centre.z);
            lane.BoxRotation = Quaternion.LookRotation(upAxis, Vector3.up);
            lane.BoxSize = new Vector3(lane.Width + .25f, Mathf.Abs(top.y - bottom.y) + 3.4f, run + .3f);
        }

        // ── 맞이방 2F-3F·1F-2F 에스컬레이터(메시에서 벨트) ──────────────────────────

        /// <summary>A mesh escalator installation along the building grid: belt centre lines from its step/handrail vertices.</summary>
        private sealed class MeshEscalator
        {
            public string Id, Label;
            public Transform Root;
            public float Lower, Upper;
            public int Belts;
            public bool[] Up;
            public string[] Labels;
            public string From, To;
            public Func<MeshFilter, bool> Steps;
        }

        private static IEnumerable<Lane> NamedEscalators(List<string> log)
        {
            var group = GameObject.Find("FPSWorld/맞이방 · 에스컬레이터");
            if (group != null)
                foreach (Transform child in group.transform)
                {
                    bool upward = child.name.Contains("상행");
                    var lane = MeshLanes(new MeshEscalator
                    {
                        Id = upward ? "esc-2f3f-up" : "esc-2f3f-down", Root = child, Lower = Concourse, Upper = Upper, Belts = 1,
                        Up = new[] { upward }, Labels = new[] { upward ? "3층 올라가는 에스컬레이터" : "2층 내려가는 에스컬레이터" },
                        From = "concourse", To = "upper", Steps = f => true,
                    }, log).FirstOrDefault();
                    if (lane != null) yield return lane;
                }
            else log.Add("맞이방 에스컬레이터 없음");
        }

        private static IEnumerable<Lane> KitEscalators(List<string> log)
        {
            var kits = new List<(string id, string label, string path)>
            {
                ("1f-north", "1층 북측", "실내 트윈 마감/수직 동선"),
                ("1f-south", "1층 남측", "실내 트윈 마감/1F 공용 에스컬레이터"),
            };
            foreach (var (id, label, path) in kits)
            {
                var root = GameObject.Find(path);
                if (root == null) { log.Add(label + ": 에스컬레이터 없음"); continue; }
                // 계단판 폭이 2.4 m 를 넘으면 벨트 두 개(오르내림 한 쌍)로 본다.
                foreach (var lane in MeshLanes(new MeshEscalator
                {
                    Id = "esc-" + id, Root = root.transform, Lower = Ground, Upper = Concourse, Belts = 0,
                    Up = new[] { true, false }, Labels = new[] { label + " 2층 올라가는 에스컬레이터", label + " 1층 내려가는 에스컬레이터" },
                    From = "ground", To = "concourse", Steps = f => f.name.StartsWith("Kit_EscStep", StringComparison.Ordinal),
                }, log)) yield return lane;
            }
        }

        private static IEnumerable<Lane> MeshLanes(MeshEscalator spec, List<string> log)
        {
            var axis = TrackAxis();
            var side = new Vector3(axis.z, 0, -axis.x);
            var points = new List<Vector3>();
            foreach (var filter in spec.Root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!spec.Steps(filter) || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                foreach (var v in filter.sharedMesh.vertices) points.Add(filter.transform.TransformPoint(v));
            }
            if (points.Count < 8) { log.Add(spec.Id + ": 메시를 읽을 수 없음"); yield break; }
            float along(Vector3 p) => Vector3.Dot(p, axis);
            float across(Vector3 p) => Vector3.Dot(p, side);
            float amin = points.Min(along), amax = points.Max(along), smin = points.Min(across), smax = points.Max(across);
            // 높은 쪽(위층) 방향: 양 끝 15% 꼭짓점의 평균 높이로 정한다.
            float length = amax - amin;
            float lowEnd = points.Where(p => along(p) < amin + length * .15f).Average(p => p.y);
            float highEnd = points.Where(p => along(p) > amax - length * .15f).Average(p => p.y);
            bool upIsPositive = highEnd > lowEnd;
            int belts = spec.Belts > 0 ? spec.Belts : smax - smin > 2.4f ? 2 : 1;
            float beltWidth = (smax - smin) / belts;
            log.Add(spec.Id + ": 길이 " + length.ToString("F1") + " 폭 " + (smax - smin).ToString("F1") + " 벨트 " + belts + (upIsPositive ? " (+축이 위층)" : " (-축이 위층)"));
            for (int b = 0; b < belts; b++)
            {
                float centreS = smin + beltWidth * (b + .5f);
                var near = points.Where(p => Mathf.Abs(across(p) - centreS) < beltWidth * .3f).ToList();
                if (near.Count < 4) near = points;
                var belt = new List<Vector3>();
                // 아래층 끝 → 위층 끝 순서로 0.5 m 마다 벨트 윗면 높이(가운데 줄 꼭짓점의 최고점).
                float start = upIsPositive ? amin + .3f : amax - .3f, stop = upIsPositive ? amax - .3f : amin + .3f, step = upIsPositive ? .5f : -.5f;
                for (float a = start; upIsPositive ? a <= stop : a >= stop; a += step)
                {
                    var slice = near.Where(p => Mathf.Abs(along(p) - a) < .3f).ToList();
                    float y = slice.Count > 0 ? Mathf.Clamp(slice.Max(p => p.y), spec.Lower, spec.Upper) : float.NaN;
                    var point = axis * a + side * centreS;
                    point.y = float.IsNaN(y) ? Mathf.Lerp(spec.Lower, spec.Upper, Mathf.InverseLerp(start, stop, a)) : y;
                    belt.Add(point);
                }
                if (belt.Count < 6) continue;
                // 손잡이·난간 꼭짓점이 섞이면 윗면이 튄다: 단조 증가로 다듬고 끝을 층 높이에 맞춘다.
                for (int i = 1; i < belt.Count; i++) if (belt[i].y < belt[i - 1].y) belt[i] = new Vector3(belt[i].x, belt[i - 1].y, belt[i].z);
                belt[0] = new Vector3(belt[0].x, spec.Lower, belt[0].z);
                belt[belt.Count - 1] = new Vector3(belt[belt.Count - 1].x, spec.Upper, belt[belt.Count - 1].z);
                bool upward = spec.Up[Mathf.Min(b, spec.Up.Length - 1)];
                var lane = new Lane
                {
                    Id = spec.Id + (belts > 1 ? "-" + b : ""), Label = spec.Labels[Mathf.Min(upward ? 0 : 1, spec.Labels.Length - 1)], Up = upward,
                    Width = Mathf.Min(1.1f, beltWidth - .2f), From = upward ? spec.From : spec.To, To = upward ? spec.To : spec.From,
                };
                if (spec.Belts == 1) lane.Label = spec.Labels[0];
                var ordered = new List<Vector3>(belt);
                if (!upward) ordered.Reverse();
                var dir = ordered[ordered.Count - 1] - ordered[0]; dir.y = 0; dir.Normalize();
                lane.Path.Add(ordered[0] - dir * 1.0f);
                lane.Path.AddRange(ordered);
                lane.Path.Add(ordered[ordered.Count - 1] + dir * 1.0f);
                var upAxis = upIsPositive ? axis : -axis;
                SetBox(lane, belt[belt.Count - 1], belt[0], upAxis);
                yield return lane;
            }
        }

        // ── 엘리베이터 ─────────────────────────────────────────────────────────

        /// <summary>
        /// Elevators people can ride: kit landing doors (<see cref="ChooGuard.App.Fps.Facilities.StationDoor"/>, kind Elevator)
        /// with a cabin, grouped by car id; each stop is the walkable point 1 m in front of the landing doors, facing the shaft.
        /// Landings whose other floors the twin does not have (no car) are not elevators for passengers. The earlier sign-based
        /// survey paired fronts by nearby walkable floor and put one elevator's stops behind its doors (2026-09-28).
        /// </summary>
        public static List<StationPoints.ElevatorEntry> Elevators(Func<Vector3, float, Vector3?> onNavMesh, List<string> log)
        {
            var station = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(EmergencySceneBuilder.StationScenePath);
            var cars = new Dictionary<string, List<(Vector3 door, float yaw, float floor)>>();
            foreach (var landing in station.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ChooGuard.App.Fps.Facilities.StationDoor>(true)))
            {
                if (landing.Kind != ChooGuard.App.Fps.Facilities.StationDoor.DoorKind.Elevator) continue;
                if (landing.Cabin == null || string.IsNullOrEmpty(landing.CarId)) { log.Add("엘리베이터 승강장(운행 카 없음) " + landing.Label + " " + landing.transform.TransformPoint(landing.Centre).ToString("F1")); continue; }
                var front = onNavMesh(landing.World(new Vector3(0, 0, 1f)), .8f);
                if (front == null) { log.Add("엘리베이터 승강장 앞이 navmesh 밖 " + landing.Label); continue; }
                var into = -landing.transform.TransformDirection(landing.Normal);
                if (!cars.TryGetValue(landing.CarId, out var stops)) cars[landing.CarId] = stops = new List<(Vector3, float, float)>();
                stops.Add((front.Value, Mathf.Atan2(into.x, into.z) * Mathf.Rad2Deg, landing.transform.TransformPoint(landing.Centre).y));
            }
            var result = new List<StationPoints.ElevatorEntry>();
            foreach (var pair in cars.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var bank = pair.Value.OrderBy(s => s.floor).ToList();
                if (bank.Count < 2) { log.Add("엘리베이터 한 층뿐 " + pair.Key); continue; }
                result.Add(new StationPoints.ElevatorEntry
                {
                    id = "ev-" + result.Count,
                    label = string.Join("·", bank.Select(o => FloorName(o.floor))) + " 엘리베이터",
                    stops = bank.Select(o => new StationPoints.ElevatorStop { floor = FloorName(o.floor), door = o.door, yaw = o.yaw }).ToArray(),
                });
            }
            return result;
        }

        public static string FloorName(float y) => y < 3.5f ? "1층" : y < 9.6f ? "2층" : "3층";
        private static float Horizontal(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }

        // ── 승강장 ──────────────────────────────────────────────────────────────

        public static List<StationPoints.PlatformEntry> Platforms(List<string> log)
        {
            var axis = TrackAxis();
            var lateral = new Vector3(axis.z, 0, -axis.x);
            var list = new List<StationPoints.PlatformEntry>();
            foreach (var (id, label, offset, halfWidth) in PlatformStrips)
            {
                var centre = PlatformReference + lateral * offset;
                float Extent(float sign)
                {
                    float last = 0, miss = 0;
                    for (float t = 0; t < 260; t += 1f)
                    {
                        var p = centre + axis * (t * sign);
                        if (!float.IsNaN(Surface(p, Ground, .12f))) { last = t; miss = 0; }
                        else if (++miss > 4) break;
                    }
                    return last;
                }
                float north = Extent(1), south = Extent(-1);
                list.Add(new StationPoints.PlatformEntry { id = id, label = label, a = centre - axis * south, b = centre + axis * north, halfWidth = halfWidth });
                log.Add(label + ": 길이 " + (north + south).ToString("F0") + " m");
            }
            return list;
        }

        // ── KTX 객실 ────────────────────────────────────────────────────────────

        /// <summary>
        /// Platform-side door, step, aisle and seats of every passenger car with a passenger door, as world positions at
        /// the stop pose. The platform side is the side facing <paramref name="platform"/>.
        /// </summary>
        public static StationPoints.TrainEntry Train(StationPoints.PlatformEntry platform, List<string> log)
        {
            var axis = TrackAxis();
            var lateral = new Vector3(axis.z, 0, -axis.x);
            var platformCentre = (platform.a + platform.b) * .5f;
            var cars = new List<StationPoints.CarEntry>();
            foreach (Transform car in Ktx)
            {
                if (!car.name.StartsWith("Car", StringComparison.Ordinal) || car.name.StartsWith("Car00", StringComparison.Ordinal) || car.name.StartsWith("Car09", StringComparison.Ordinal)) continue;
                int number = int.Parse(car.name.Substring(3, 2));
                var leavesL = car.Cast<Transform>().Where(c => c.name.Contains("_D L_")).ToList();
                var leavesR = car.Cast<Transform>().Where(c => c.name.Contains("_D R_")).ToList();
                if (leavesL.Count == 0 && leavesR.Count == 0) { log.Add(car.name + ": 승객 문 없음"); continue; }
                float SideDistance(List<Transform> leaves) => leaves.Count == 0 ? float.MaxValue : Mathf.Abs(Vector3.Dot(Bounds(leaves[0]).center - platformCentre, lateral));
                bool left = SideDistance(leavesL) < SideDistance(leavesR);
                var leaves = left ? leavesL : leavesR;
                var lift = car.Cast<Transform>().FirstOrDefault(c => c.name.Contains(left ? "Lift L_000" : "Lift R_000"));
                // 트윈은 승강장 쪽 문이 열린 모양으로 저장돼 있다. 닫힌 위치는 반대쪽 문짝의 차량 축 좌표(0.0155)다.
                var other = (left ? leavesR : leavesL).FirstOrDefault();
                var open = leaves[0].localPosition;
                var closed = other != null ? new Vector3(0, other.localPosition.y, open.z) : new Vector3(0, open.y - .0105f, open.z);
                var openDelta = open - closed;
                var liftDelta = lift != null ? new Vector3(lift.localPosition.x, 0, 0) : Vector3.zero;
                var carBounds = Bounds(car);
                // 문 개구부 중심 = 발판 위치. 바깥 = 승강장 쪽.
                var opening = lift != null ? Bounds(lift).center : Bounds(leaves[0]).center - axis * Mathf.Sign(Vector3.Dot(Bounds(leaves[0]).center - carBounds.center, axis)) * 1.05f;
                var outward = lateral * Mathf.Sign(Vector3.Dot(platformCentre - opening, lateral));
                var interior = ScanInterior(car, axis, outward, opening, log);
                if (interior == null) continue;
                var door = new StationPoints.DoorEntry
                {
                    leaves = leaves.Select(l => l.name).ToArray(),
                    openDelta = openDelta,
                    lift = lift != null ? lift.name : "",
                    liftDelta = liftDelta,
                    outside = new Vector3(opening.x, Ground, opening.z) + outward * .8f,
                    inside = new Vector3(opening.x, interior.floor, opening.z) - outward * .95f,
                };
                interior.door = door;
                interior.name = car.name;
                interior.number = number;
                interior.label = number + "호차" + (car.name.Contains("_S ") ? " 특실" : "");
                interior.min = carBounds.min;
                interior.max = carBounds.max;
                cars.Add(interior);
            }
            return new StationPoints.TrainEntry { root = KtxPath, platform = platform.id, track = "6", axis = axis, cars = cars.OrderBy(c => c.number).ToArray() };
        }

        private static StationPoints.CarEntry ScanInterior(Transform car, Vector3 axis, Vector3 outward, Vector3 opening, List<string> log)
        {
            var bounds = Bounds(car);
            var centre = new Vector3(bounds.center.x, 0, bounds.center.z);
            const float step = .1f;
            int nu = 261, nv = 33;
            var height = new float[nu, nv];
            var hits = new RaycastHit[24];
            for (int i = 0; i < nu; i++)
                for (int k = 0; k < nv; k++)
                {
                    var p = centre + axis * ((i - nu / 2) * step) - outward * ((k - nv / 2) * step);
                    int n = Physics.RaycastNonAlloc(new Vector3(p.x, 2.15f, p.z), Vector3.down, hits, 3.2f, ~0, QueryTriggerInteraction.Ignore);
                    float top = float.NaN;
                    for (int h = 0; h < n; h++) if (hits[h].collider.transform.IsChildOf(car) && (float.IsNaN(top) || hits[h].point.y > top)) top = hits[h].point.y;
                    height[i, k] = top;
                }
            // 바닥: 0.2~0.65 m 사이 가장 흔한 높이.
            var floorSamples = new List<float>();
            foreach (var h in height) if (!float.IsNaN(h) && h > .2f && h < .65f) floorSamples.Add(h);
            if (floorSamples.Count < 50) { log.Add(car.name + ": 바닥 없음"); return null; }
            floorSamples.Sort();
            float floor = floorSamples[floorSamples.Count / 2];
            bool IsSeat(float h) => !float.IsNaN(h) && h > floor + .3f && h < floor + .75f;
            bool IsBack(float h) => !float.IsNaN(h) && h > floor + .8f;
            // 줄(행): 좌석 방석 칸이 많은 u 구간.
            var seatCount = new int[nu];
            for (int i = 0; i < nu; i++) for (int k = 0; k < nv; k++) if (IsSeat(height[i, k])) seatCount[i]++;
            var rows = new List<(int from, int to)>();
            int start = -1;
            for (int i = 0; i < nu; i++)
            {
                bool on = seatCount[i] >= 8;
                if (on && start < 0) start = i;
                if (!on && start >= 0) { if (i - start >= 2) rows.Add((start, i - 1)); start = -1; }
            }
            // 방석이 두 토막으로 잡힌 줄(틈 0.45 m 미만)은 한 줄로 합친다. KTX 좌석 간격은 0.96 m 이상이다.
            var merged = new List<(int from, int to)>();
            foreach (var row in rows)
            {
                if (merged.Count > 0 && (row.from - merged[merged.Count - 1].to) * step < .45f) merged[merged.Count - 1] = (merged[merged.Count - 1].from, row.to);
                else merged.Add(row);
            }
            rows = merged;
            if (rows.Count < 4) { log.Add(car.name + ": 좌석 줄 " + rows.Count); return null; }
            // 통로: 좌석 줄에서 방석이 없는 가운데 칸의 중앙값.
            var aisleCentres = new List<float>();
            var blocksPerRow = new List<List<(int from, int to)>>();
            foreach (var (from, to) in rows)
            {
                int mid = (from + to) / 2;
                var blocks = new List<(int, int)>();
                int b = -1;
                for (int k = 0; k < nv; k++)
                {
                    bool seat = IsSeat(height[mid, k]) || IsSeat(height[Mathf.Max(from, mid - 1), k]) || IsSeat(height[Mathf.Min(to, mid + 1), k]);
                    if (seat && b < 0) b = k;
                    if (!seat && b >= 0) { if (k - b >= 3) blocks.Add((b, k - 1)); b = -1; }
                }
                if (b >= 0 && nv - b >= 3) blocks.Add((b, nv - 1));
                blocksPerRow.Add(blocks);
                if (blocks.Count >= 2)
                {
                    var sorted = blocks.OrderBy(x => x.Item1).ToList();
                    for (int j = 0; j + 1 < sorted.Count; j++)
                        if (sorted[j + 1].Item1 - sorted[j].Item2 >= 4) aisleCentres.Add((sorted[j].Item2 + sorted[j + 1].Item1) * .5f);
                }
            }
            if (aisleCentres.Count < 3) { log.Add(car.name + ": 통로 없음"); return null; }
            aisleCentres.Sort();
            float aisleK = aisleCentres[aisleCentres.Count / 2];
            float V(float k) => (k - nv / 2) * step;
            float U(float i) => (i - nu / 2) * step;
            Vector3 World(float u, float v, float y) { var p = centre + axis * u - outward * v; p.y = y; return p; }
            // 문(출입대) 쪽에서 반대쪽 끝 줄까지 통로 선.
            float doorU = Vector3.Dot(opening - centre, axis);
            float firstU = U(rows.First().from), lastU = U(rows.Last().to);
            float farU = Mathf.Abs(firstU - doorU) > Mathf.Abs(lastU - doorU) ? firstU : lastU;
            var aisle = new List<Vector3> { World(doorU, V(aisleK), floor) };
            float dir = Mathf.Sign(farU - doorU);
            for (float u = doorU + dir * 1.0f; (farU - u) * dir > -.01f; u += dir * 1.0f) aisle.Add(World(u, V(aisleK), floor));
            var seats = new List<StationPoints.SeatEntry>();
            int rowNumber = 0;
            // 방석 앞(발 쪽): 등받이가 있는 쪽의 반대. 등받이가 낮은 u 쪽에 있으면 +u 를 본다. KTX-산천 좌석은 한 방향(회전식)이라
            // 다수와 반대로 잡힌 줄은 등받이를 방석으로 잘못 읽은 것으로 보고 뺀다.
            var facings = new float[rows.Count];
            for (int r = 0; r < rows.Count; r++)
            {
                var (from, to) = rows[r];
                int probeK = blocksPerRow[r].Count > 0 ? (blocksPerRow[r][0].from + blocksPerRow[r][0].to) / 2 : nv / 2;
                bool backBelow = from - 2 >= 0 && IsBack(height[from - 2, probeK]);
                bool backAbove = to + 2 < nu && IsBack(height[to + 2, probeK]);
                facings[r] = backBelow && !backAbove ? 1 : backAbove && !backBelow ? -1 : 0;
            }
            float majority = Mathf.Sign(facings.Sum() == 0 ? 1 : facings.Sum());
            for (int r = 0; r < rows.Count; r++)
            {
                var (from, to) = rows[r];
                if (facings[r] != 0 && facings[r] != majority) continue;
                float facingSign = majority;
                var facing = axis * facingSign;
                float cushionU = U((from + to) * .5f);
                rowNumber++;
                foreach (var (b0, b1) in blocksPerRow[r])
                {
                    float width = (b1 - b0 + 1) * step;
                    int count = Mathf.Clamp(Mathf.RoundToInt(width / .5f), 1, 3);
                    for (int s = 0; s < count; s++)
                    {
                        float k = b0 + (b1 - b0 + 1) * (s + .5f) / count - .5f;
                        var anchor = World(cushionU, V(k), floor);
                        var front = anchor + facing * .46f;
                        var aislePoint = World(cushionU + facingSign * .46f, V(aisleK), floor);
                        seats.Add(new StationPoints.SeatEntry
                        {
                            id = car.name.Substring(3, 2) + "-" + rowNumber.ToString("00") + (char)('A' + seats.Count(x => x.row == rowNumber)),
                            row = rowNumber,
                            anchor = anchor,
                            front = front,
                            aisle = aislePoint,
                            yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg,
                        });
                    }
                }
            }
            log.Add(car.name + ": 바닥 " + floor.ToString("F2") + " 줄 " + rows.Count + " 좌석 " + seats.Count + " 통로 v=" + V(aisleK).ToString("F2"));
            return new StationPoints.CarEntry { floor = floor, aisle = aisle.ToArray(), seats = seats.ToArray() };
        }
    }
}
