using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Equipment;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// The checks the builder holds the exposed sprinkler pipe to, and the distance test the layout uses to keep runs apart. Two rules, both counted over every exposed line:
    /// <list type="bullet">
    /// <item>Head clearance: no exposed horizontal pipe passes lower than <see cref="SprinklerLayout.ClearHeight"/> above the walkable floor under it (risers are vertical and stand at walls).</item>
    /// <item>No pipe through pipe: two runs that are not joined keep their surfaces <see cref="ClashGap"/> apart; where a run ends on another (a tee) or two runs share an end (an elbow) the
    /// joint itself is left out - the part of both runs within <see cref="JointFactor"/> diameters of the joint - and the rest must not overlap.</item>
    /// </list>
    /// </summary>
    internal static class SprinklerAudit
    {
        public const float ClashGap = .05f, JointFactor = 3f, Step = .03f, OnAxis = .03f;

        public sealed class Report
        {
            public int LowPipes, Interpenetrating, TooClose, PairsChecked;
            public float LowestClearance = float.MaxValue;
            public readonly List<string> Notes = new List<string>();
        }

        public static float Radius(SprinklerLayout.Line line) => SprinklerPipeLine.OuterDiameter(line.Dn) * .5f;

        public static Report Run(SprinklerLayout.Result plan, StationCeilings.Result survey)
        {
            var report = new Report();
            LowPipes(plan, survey, report);
            var exposed = plan.Lines.Where(l => l.Exposed).ToList();
            for (int i = 0; i < exposed.Count; i++)
                for (int j = i + 1; j < exposed.Count; j++)
                {
                    if (!BoxesTouch(exposed[i], exposed[j])) continue;
                    report.PairsChecked++;
                    float gap = Gap(exposed[i], exposed[j], out bool joint);
                    if (gap < -1e-4f) { report.Interpenetrating++; Note(report, "pipes interpenetrate (gap " + gap.ToString("F3") + " m): " + Describe(exposed[i]) + " | " + Describe(exposed[j])); }
                    else if (!joint && gap < ClashGap - 1e-4f) { report.TooClose++; Note(report, "pipes cross closer than 5 cm (gap " + gap.ToString("F3") + " m): " + Describe(exposed[i]) + " | " + Describe(exposed[j])); }
                }
            return report;
        }

        private static void Note(Report report, string text) { if (report.Notes.Count < 12) report.Notes.Add(text); }

        private static string Describe(SprinklerLayout.Line l) => l.Valve + " " + l.Role + " DN" + l.Dn + " " + l.A.ToString("F1") + "->" + l.B.ToString("F1");

        /// <summary>The walkable floors of the twin by 1 m cell, to tell how far above the floor a person stands under a point a pipe passes.</summary>
        public sealed class Floors
        {
            private readonly Dictionary<(int, int), List<float>> byCell = new Dictionary<(int, int), List<float>>();

            public Floors(StationCeilings.Result survey)
            {
                foreach (var c in survey.Cells)
                {
                    if (!c.Walkable) continue;
                    if (!byCell.TryGetValue((c.X, c.Z), out var list)) byCell[(c.X, c.Z)] = list = new List<float>();
                    list.Add(c.Floor.y);
                }
            }

            /// <summary>
            /// The smallest height of the lowest surface of a pipe of <paramref name="radius"/> along a-b above the floor a person stands on under it (the highest walkable floor below that
            /// surface); <see cref="float.MaxValue"/> when nobody can stand under any point of it.
            /// </summary>
            public float Clearance(Vector3 a, Vector3 b, float radius)
            {
                float worst = float.MaxValue;
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .5f));
                for (int i = 0; i <= n; i++)
                {
                    var p = Vector3.Lerp(a, b, i / (float)n);
                    float bottom = p.y - radius;
                    if (!byCell.TryGetValue((Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z)), out var list)) continue;
                    float floor = float.NegativeInfinity;
                    foreach (float f in list) if (f <= bottom + .05f && f > floor) floor = f;
                    if (!float.IsNegativeInfinity(floor)) worst = Mathf.Min(worst, bottom - floor);
                }
                return worst;
            }
        }

        private static void LowPipes(SprinklerLayout.Result plan, StationCeilings.Result survey, Report report)
        {
            var floors = new Floors(survey);
            foreach (var line in plan.Lines)
            {
                if (!line.Exposed || line.Role == "riser") continue;
                float worst = floors.Clearance(line.A, line.B, Radius(line));
                if (worst == float.MaxValue) continue;
                report.LowestClearance = Mathf.Min(report.LowestClearance, worst);
                if (worst < SprinklerLayout.ClearHeight - .001f) { report.LowPipes++; Note(report, "exposed pipe " + worst.ToString("F2") + " m above the floor: " + Describe(line)); }
            }
        }

        private static bool BoxesTouch(SprinklerLayout.Line a, SprinklerLayout.Line b)
        {
            float margin = Radius(a) + Radius(b) + ClashGap + .1f;
            return Mathf.Min(a.A.x, a.B.x) - margin <= Mathf.Max(b.A.x, b.B.x) && Mathf.Min(b.A.x, b.B.x) - margin <= Mathf.Max(a.A.x, a.B.x)
                && Mathf.Min(a.A.y, a.B.y) - margin <= Mathf.Max(b.A.y, b.B.y) && Mathf.Min(b.A.y, b.B.y) - margin <= Mathf.Max(a.A.y, a.B.y)
                && Mathf.Min(a.A.z, a.B.z) - margin <= Mathf.Max(b.A.z, b.B.z) && Mathf.Min(b.A.z, b.B.z) - margin <= Mathf.Max(a.A.z, a.B.z);
        }

        /// <summary>The smallest gap between the surfaces of two runs outside their joint (negative: they overlap); <see cref="float.MaxValue"/> when nothing is left to compare.</summary>
        public static float Gap(SprinklerLayout.Line a, SprinklerLayout.Line b, out bool joint) => Gap(a.A, a.B, Radius(a), b.A, b.B, Radius(b), out joint);

        public static float Gap(Vector3 a1, Vector3 b1, float r1, Vector3 a2, Vector3 b2, float r2, out bool joint)
        {
            var joints = new List<Vector3>(2);
            foreach (var p in new[] { a1, b1 }) if (PointSegment(p, a2, b2) < OnAxis) joints.Add(p);
            foreach (var p in new[] { a2, b2 })
                if (PointSegment(p, a1, b1) < OnAxis && !joints.Exists(j => (j - p).sqrMagnitude < OnAxis * OnAxis)) joints.Add(p);
            joint = joints.Count > 0;
            float radii = r1 + r2;
            if (!joint) return SegmentDistance(a1, b1, a2, b2) - radii;
            float exclude = JointFactor * radii + .05f;
            float best = Mathf.Min(Nearest(a1, b1, a2, b2, joints, exclude), Nearest(a2, b2, a1, b1, joints, exclude));
            return best == float.MaxValue ? float.MaxValue : best - radii;
        }

        /// <summary>Smallest distance from the points of a-b (outside the joint zones) to c-d, ignoring the part of c-d inside a joint zone.</summary>
        private static float Nearest(Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<Vector3> joints, float exclude)
        {
            float best = float.MaxValue, exclude2 = exclude * exclude;
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / Step));
            var cd = d - c;
            float cdLength2 = cd.sqrMagnitude;
            for (int i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n);
                if (joints.Exists(j => (j - p).sqrMagnitude <= exclude2)) continue;
                float t = cdLength2 < 1e-10f ? 0 : Mathf.Clamp01(Vector3.Dot(p - c, cd) / cdLength2);
                var q = c + cd * t;
                if (joints.Exists(j => (j - q).sqrMagnitude <= exclude2)) continue;
                best = Mathf.Min(best, (p - q).magnitude);
            }
            return best;
        }

        private static float PointSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float length2 = ab.sqrMagnitude;
            float t = length2 < 1e-10f ? 0 : Mathf.Clamp01(Vector3.Dot(p - a, ab) / length2);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Closest distance between the segments p1-q1 and p2-q2.</summary>
        private static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            var d1 = q1 - p1; var d2 = q2 - p2; var r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-10f && e <= 1e-10f) return r.magnitude;
            if (a <= 1e-10f) { s = 0; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-10f) { t = 0; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), denominator = a * e - b * b;
                    s = denominator > 1e-10f ? Mathf.Clamp01((b * f - c * e) / denominator) : 0;
                    t = (b * s + f) / e;
                    if (t < 0) { t = 0; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1) { t = 1; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            return ((p1 + d1 * s) - (p2 + d2 * t)).magnitude;
        }
    }
}
