using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    // Shared source-mesh survey math. Editor authority guards remain at their callers.
    public static class StationNavigationGeometry
    {
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

        public sealed class MeshEscalator
        {
            public string Id, Label;
            public Transform Root;
            public float Lower, Upper;
            public int Belts;
            /// <summary>Direction of a single belt (<see cref="Belts"/> 1).</summary>
            public bool[] Up;
            /// <summary>For a pair: the down belt is on the left of someone riding up (otherwise on the right).</summary>
            public bool DownOnRiderLeft;
            public string[] Labels;
            public string From, To;
            public Func<MeshFilter, bool> Steps;
        }

        public static void SetBox(Lane lane, Vector3 top, Vector3 bottom, Vector3 upAxis)
        {
            var centre = (top + bottom) * .5f;
            float run = Vector3.Distance(new Vector3(top.x, 0, top.z), new Vector3(bottom.x, 0, bottom.z));
            lane.BoxCentre = new Vector3(centre.x, (top.y + bottom.y) * .5f + .6f, centre.z);
            lane.BoxRotation = Quaternion.LookRotation(upAxis, Vector3.up);
            lane.BoxSize = new Vector3(lane.Width + .25f, Mathf.Abs(top.y - bottom.y) + 3.4f, run + .3f);
        }

        public static IEnumerable<Lane> MeshLanes(MeshEscalator spec, Vector3 axis, List<string> log)
        {
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
                // 벨트는 side(+축의 오른쪽) 순서다. 위층이 +축이면 b=0 이 올라가는 사람의 왼쪽, −축이면 오른쪽이다.
                bool riderLeft = upIsPositive ? b == 0 : b == belts - 1;
                bool upward = belts == 1 ? spec.Up[0] : spec.DownOnRiderLeft != riderLeft;
                var lane = new Lane
                {
                    // 한 쌍이면 올라가는 벨트 -0, 내려가는 벨트 -1.
                    Id = spec.Id + (belts > 1 ? (upward ? "-0" : "-1") : ""), Label = spec.Labels[Mathf.Min(upward ? 0 : 1, spec.Labels.Length - 1)], Up = upward,
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

        // Original source cross-section, cluster, landing and adjacent-stair math.
        // Runtime supplies actual placed-source seed points; Editor retains its guarded
        // original Replay inputs. Seeds identify where to measure, never a new surface.
        public sealed class WellSurvey
        {
            const float Ground=0f, Concourse=7.02f;
            readonly Transform Ktx;
            public WellSurvey(Transform train) { Ktx=train??throw new ArgumentNullException(nameof(train)); }
        public float Surface(Vector3 xz, float expected, float tolerance)
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

        private float Top(Vector3 xz, float below, float above)
        {
            var hits = Physics.RaycastAll(new Vector3(xz.x, above, xz.z), Vector3.down, above - below, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NaN;
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(Ktx)) continue;
                if (float.IsNaN(best) || hit.point.y > best) best = hit.point.y;
            }
            return best;
        }

        /// <summary>
        /// Hits seen at about the same place in at least <paramref name="sections"/> cross-sections, left to right: each with its
        /// side offset fitted as a straight line over the distance from the high end (a lane need not be parallel to the seed line).
        /// </summary>
        private List<(float a, float b, float width)> Clusters(List<(float centre, float width, float t)> hits, int sections)
        {
            var result = new List<(float a, float b, float width)>();
            var used = new HashSet<int>();
            var order = Enumerable.Range(0, hits.Count).OrderBy(i => hits[i].centre).ToList();
            foreach (int seed in order)
            {
                if (used.Contains(seed)) continue;
                var group = order.Where(i => !used.Contains(i) && Mathf.Abs(hits[i].centre - hits[seed].centre) < .45f).ToList();
                if (group.Select(i => hits[i].t).Distinct().Count() < sections) continue;
                foreach (int i in group) used.Add(i);
                // 옆 거리 = a + b·t (최소제곱).
                float n = group.Count, mt = group.Average(i => hits[i].t), mc = group.Average(i => hits[i].centre);
                float stt = group.Sum(i => (hits[i].t - mt) * (hits[i].t - mt)), stc = group.Sum(i => (hits[i].t - mt) * (hits[i].centre - mc));
                float b = stt > 1e-4f ? stc / stt : 0f;
                result.Add((mc - b * mt, b, group.Average(i => hits[i].width)));
            }
            return result;
        }

        public IEnumerable<Lane> WellLanes(string id, string platform, string where, Vector2 high2, Vector2 low2, bool[] down, List<string> log)
        {
            var high = new Vector3(high2.x, Concourse, high2.y);
            var low = new Vector3(low2.x, Ground, low2.y);
            var up = high - low; up.y = 0;
            float length = up.magnitude;
            up /= length;
            // 올라가는 사람의 오른쪽.
            var side = new Vector3(up.z, 0, -up.x);
            // 씨앗 선을 위에서 아래로 따라 내려가며 표면 높이 윤곽을 잡는다.
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
            if (profile.Count < 6) { log.Add(id + ": 윤곽 없음"); yield break; }
            float HeightAt(float t)
            {
                var near = profile.OrderBy(p => Mathf.Abs(p.t - t)).First();
                return near.y;
            }
            // 경사 단면 아홉 곳을 5 cm 간격으로 가로질러, 평평한 면(이웃 표본 차 5 cm 미만)이 양옆 15 cm 안에서 30 cm 넘게 높은 것
            // (난간·스커트)에 막힌 곳을 찾는다. 폭 0.9~1.3 m 면 에스컬레이터 디딤판, 1.3~2.4 m 면 계단이다(공식 모델 우물 13곳 단면, 2026-10-05).
            const float step = .05f, reach = 4.5f;
            int count = Mathf.RoundToInt(2 * reach / step) + 1;
            var heights = new float[count];
            var laneHits = new List<(float centre, float width, float t)>();
            var stairHits = new List<(float centre, float width, float t)>();
            for (float fraction = .2f; fraction <= .801f; fraction += .075f)
            {
                float t = length * fraction;
                var centre = high - up * t;
                float y = HeightAt(t);
                for (int i = 0; i < count; i++) heights[i] = Top(centre + side * (-reach + i * step), y - 1.2f, y + 1.9f);
                for (int i = 0; i < count;)
                {
                    if (float.IsNaN(heights[i])) { i++; continue; }
                    int j = i;
                    while (j + 1 < count && !float.IsNaN(heights[j + 1]) && Mathf.Abs(heights[j + 1] - heights[j]) < .05f) j++;
                    float width = (j - i + 1) * step, h = heights[i], left = float.NegativeInfinity, right = float.NegativeInfinity;
                    for (int k = Mathf.Max(0, i - 3); k < i; k++) if (!float.IsNaN(heights[k])) left = Mathf.Max(left, heights[k]);
                    for (int k = j + 1; k <= Mathf.Min(count - 1, j + 3); k++) if (!float.IsNaN(heights[k])) right = Mathf.Max(right, heights[k]);
                    if (left > h + .3f && right > h + .3f)
                    {
                        float mid = -reach + (i + j) * .5f * step;
                        if (width >= .9f && width <= 1.3f) laneHits.Add((mid, width, t));
                        else if (width > 1.3f && width <= 2.4f) stairHits.Add((mid, width, t));
                    }
                    i = j + 1;
                }
            }
            var lanes = Clusters(laneHits, 3);
            var stairs = Clusters(stairHits, 2);
            if (lanes.Count != down.Length)
            {
                log.Add(id + ": 에스컬레이터 차선 " + lanes.Count + "개(기대 " + down.Length + ") " + string.Join(",", lanes.Select(c => (c.a + c.b * length * .5f).ToString("F2"))));
                yield break;
            }
            for (int k = 0; k < lanes.Count; k++)
            {
                bool goesDown = down[k];
                var fit = lanes[k];
                // 차선 중심을 따라 위·아래 계단참(평평한 구간)을 찾는다.
                Vector3 LanePoint(float t) => high - up * t + side * (fit.a + fit.b * t);
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
                float midT = length * .5f, midOffset = fit.a + fit.b * midT;
                if (float.IsNaN(topT) || float.IsNaN(bottomT)) { log.Add(id + ": 계단참 없음(차선 " + midOffset.ToString("F2") + ")"); continue; }
                var top = LanePoint(topT); top.y = Concourse;
                var bottom = LanePoint(bottomT); bottom.y = Ground;
                var lane = new Lane
                {
                    // 한 우물에 차선이 둘이면 올라가는 쪽 -0, 내려가는 쪽 -1.
                    Id = "esc-" + id + (lanes.Count > 1 ? (goesDown ? "-1" : "-0") : ""),
                    Label = platform + " 타는 곳 " + (where.Length > 0 ? where + " " : "") + (goesDown ? "내려가는" : "올라가는") + " 에스컬레이터",
                    Up = !goesDown,
                    Width = Mathf.Clamp(fit.width, .8f, 1.2f),
                    From = goesDown ? "concourse" : "platform",
                    To = goesDown ? "platform" : "concourse",
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
                // In the middle 5/6 well the platform floor also continues below
                // the last treads. A closest-height query can select that buried
                // floor instead of the exposed original tread (reviewed source
                // Layer0_5 faces 720/310). Keep X/Z and both landing endpoints.
                if(id=="well-m-56")for(int i=1;i+1<belt.Count;i++)
                {
                    var p=belt[i];
                    var hits=Physics.RaycastAll(p+Vector3.up*.75f,Vector3.down,1.5f,~0,QueryTriggerInteraction.Ignore)
                        .Where(h=>h.normal.y>.9f&&!h.collider.transform.IsChildOf(Ktx)).OrderBy(h=>h.distance).ToArray();
                    if(hits.Length==0)continue;
                    var hit=hits[0];float rise=hit.point.y-p.y;
                    if(rise>.3f&&rise<.6f&&hit.collider.name=="OfficialStation_Layer0_5"&&hit.collider.transform.parent.name=="Layer0PlatformsDetails")
                    {p.y=hit.point.y;belt[i]=p;log.Add(id+" exposed native tread height restored at "+i+": "+rise.ToString("F6"));}
                }
                belt[0] = top;
                belt[belt.Count - 1] = bottom;
                if (!goesDown) belt.Reverse();
                var along = (belt[belt.Count - 1] - belt[0]); along.y = 0; along.Normalize();
                lane.Path.Add(belt[0] - along * 1.0f);
                lane.Path.AddRange(belt);
                lane.Path.Add(belt[belt.Count - 1] + along * 1.0f);
                var laneUp = top - bottom; laneUp.y = 0; laneUp.Normalize();
                SetBox(lane, top, bottom, laneUp);
                // 에스컬레이터 옆 계단(차선 하나인 우물): 차선에서 가장 가까운 계단 무리.
                if (lanes.Count == 1 && stairs.Count > 0)
                {
                    var stair = stairs.OrderBy(s => Mathf.Abs(s.a + s.b * midT - midOffset)).First();
                    lane.StairsCentre = lane.BoxCentre + side * (stair.a + stair.b * midT - midOffset);
                    lane.StairsSize = new Vector3(stair.width, lane.BoxSize.y, lane.BoxSize.z + .6f);
                }
                yield return lane;
            }
        }

        }
    }
}
