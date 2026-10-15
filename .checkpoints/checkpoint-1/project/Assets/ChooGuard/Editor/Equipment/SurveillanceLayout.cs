using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Where the CCTV cameras and the public-address speakers of the station go.
    /// <list type="bullet">
    /// <item>Cameras: the Railway Safety Act (art. 39-3, enforcement decree art. 30 and its annex 4-4 no. 3) has the operator watch the platforms, the concourses and the ends of
    /// the lifts and escalators, and never a toilet. No source gives heights or spacing (research nftc103-compartments-cctv 4.1), so this is game design: ceiling dome
    /// cameras (the type seen over the gate line of Seoul Station, photo receipts in that report) placed one by one until every walkable floor point is within 18 m of a dome (24 m
    /// under a ceiling of 5.5 m or more), plus bullet cameras on a wall 3 m above the floor looking at each exit, each end of every escalator and both sides of each fire shutter.</item>
    /// <item>Speakers (NFTC 202 2.1.1): every point of a floor within 25 m of a speaker (horizontal), 1 W indoors and 3 W outdoors; the layout uses 9 m under finished ceilings
    /// (ceiling speakers, the type in the Busan Station photo) and 14 m for horns on canopies and high ceilings (a horn carries farther; the spacing is game design).</item>
    /// </list>
    /// </summary>
    internal static class SurveillanceLayout
    {
        public const float DomeRange = 18f, HighDomeRange = 24f, CeilingSpeakerSpacing = 9f, HornSpacing = 14f, MaxMountHeight = 8f, HighCeiling = 5.5f, BulletFov = 75f;

        public sealed class Camera
        {
            public Vector3 Position, Normal, Target;
            public bool Dome;
            public float Range, Fov, FloorY;
            public int Level;
            public string ZoneId = "", View = "";
        }

        public sealed class Speaker
        {
            public Vector3 Position, Normal;
            public bool Horn;
            public int Level;
            public string ZoneId = "";
        }

        public sealed class Result
        {
            public readonly List<Camera> Cameras = new List<Camera>();
            public readonly List<Speaker> Speakers = new List<Speaker>();
            public int UncoveredCameraCells, UncoveredSpeakerCells;
        }

        public static Result Plan(StationCeilings.Result survey, StationPoints points, StationWalls walls, List<Vector3> avoid, List<CompartmentLayout.Shutter> shutters, List<string> notes)
        {
            var result = new Result();
            var context = new DetectorLayout.Context(survey, .5f, .3f);
            var toilets = points.Of(PointKind.Toilet).ToList();
            bool Toilet(StationCeilings.Cell c) => c.Height < 4.5f && toilets.Exists(t => Mathf.Abs(t.Position.y - c.Floor.y) < 1f && new Vector2(t.Position.x - c.Floor.x, t.Position.z - c.Floor.z).magnitude < 3f);
            var cells = survey.Cells.Where(c => c.Zone.Length > 0 && c.Zone != "plaza" && c.Height <= 20f).GroupBy(c => (DetectorLayout.Level(c), c.X, c.Z)).Select(g => g.OrderBy(c => c.CeilingY).First()).OrderBy(c => c.X).ThenBy(c => c.Z).ToList();
            foreach (var level in cells.GroupBy(DetectorLayout.Level))
                foreach (var component in DetectorLayout.Components(level.ToList())) context.Register(component);
            var mountable = cells.Where(c => c.Height <= MaxMountHeight && !Toilet(c)).ToList();
            PlaceDomes(survey, context, cells.Where(c => c.Walkable && !Toilet(c)).ToList(), mountable, avoid, result, notes);
            PlaceBullets(survey, points, walls, shutters, result, notes);
            PlaceSpeakers(context, cells.Where(c => !Toilet(c)).ToList(), cells.Where(c => c.Walkable).ToList(), avoid, result, notes);
            return result;
        }

        private static float RangeOf(float height) => height < HighCeiling ? DomeRange : HighDomeRange;

        private static bool Seen(List<Camera> cameras, StationCeilings.Cell cell) =>
            cameras.Exists(c => c.Level == DetectorLayout.Level(cell) && Mathf.Abs(c.FloorY - cell.Floor.y) < 1.5f && new Vector2(c.Position.x - cell.Floor.x, c.Position.z - cell.Floor.z).magnitude <= c.Range);

        private static bool Clash(List<Vector3> avoid, Vector2 point, float ceilingY, float distance)
        {
            foreach (var a in avoid) if (Mathf.Abs(a.y - ceilingY) < 1f && (new Vector2(a.x, a.z) - point).sqrMagnitude < distance * distance) return true;
            return false;
        }

        private static void PlaceDomes(StationCeilings.Result survey, DetectorLayout.Context context, List<StationCeilings.Cell> floor, List<StationCeilings.Cell> mountable, List<Vector3> avoid, Result result, List<string> notes)
        {
            int stuck = 0;
            foreach (var cell in floor)
            {
                if (Seen(result.Cameras, cell)) continue;
                StationCeilings.Cell pick = null;
                Vector2 spot = default;
                float best = float.MaxValue;
                foreach (var candidate in mountable)
                {
                    if (DetectorLayout.Level(candidate) != DetectorLayout.Level(cell) || Mathf.Abs(candidate.Floor.y - cell.Floor.y) > 1.5f) continue;
                    float d = new Vector2(candidate.Floor.x - cell.Floor.x, candidate.Floor.z - cell.Floor.z).sqrMagnitude;
                    float reach = RangeOf(candidate.Height) * .85f;
                    if (d > reach * reach || d >= best) continue;
                    var s = context.SpotNear(candidate, new Vector2(candidate.Floor.x, candidate.Floor.z), q => Clash(avoid, q, candidate.CeilingY, .6f));
                    if (s == null) continue;
                    best = d; pick = candidate; spot = s.Value;
                }
                if (pick == null) { stuck++; continue; }
                var hit = survey.CeilingAt(spot.x, spot.y, pick.Floor.y);
                var camera = new Camera
                {
                    Position = new Vector3(spot.x, hit?.y ?? pick.CeilingY, spot.y), Normal = hit?.normal ?? pick.Normal, Dome = true, Range = RangeOf(pick.Height), Fov = 360f, FloorY = pick.Floor.y,
                    Level = DetectorLayout.Level(pick), ZoneId = pick.Zone, View = "천장 돔카메라",
                };
                camera.Target = new Vector3(spot.x, pick.Floor.y, spot.y);
                result.Cameras.Add(camera);
                avoid.Add(camera.Position);
            }
            result.UncoveredCameraCells = floor.Count(c => !Seen(result.Cameras, c));
            notes.Add("cameras: " + result.Cameras.Count + " domes; " + result.UncoveredCameraCells + " walkable cells outside every dome's range (" + stuck + " could not be given a dome)");
        }

        private static void PlaceBullets(StationCeilings.Result survey, StationPoints points, StationWalls walls, List<CompartmentLayout.Shutter> shutters, Result result, List<string> notes)
        {
            var targets = new List<(Vector3 point, string view)>();
            foreach (var exit in points.Of(PointKind.Exit).OrderBy(e => e.Id, StringComparer.Ordinal)) targets.Add((exit.Position, exit.Label + " 출입구"));
            foreach (var escalator in points.Escalators.Where(e => !e.stairs && e.path != null && e.path.Length > 1).OrderBy(e => e.id, StringComparer.Ordinal))
            {
                var top = escalator.path.OrderByDescending(p => p.y).First();
                var bottom = escalator.path.OrderBy(p => p.y).First();
                targets.Add((top, escalator.label + " 상부"));
                targets.Add((bottom, escalator.label + " 하부"));
            }
            foreach (var shutter in shutters.OrderBy(s => s.Id, StringComparer.Ordinal))
                foreach (float side in new[] { 1f, -1f })
                    targets.Add((shutter.Position + shutter.Normal * (side * 2.5f), "방화셔터 " + shutter.Id.Substring(shutter.Id.Length - 2) + "번 " + (side > 0 ? "앞" : "뒤")));
            int placed = 0, missed = 0;
            foreach (var (target, view) in targets)
            {
                // 목표 바닥점 위 3 m 에서 16 방향으로 쏘아 가장 가까운 벽(2-9 m)에 단다. 카메라는 벽에서 나와 목표를 본다.
                var origin = target + Vector3.up * 3f;
                StationWalls.Hit best = default;
                bool found = false;
                for (int k = 0; k < 16; k++)
                {
                    float a = k * 22.5f * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    if (!walls.Cast(origin, dir, 2f, 9f, out var hit) || found && hit.Distance >= best.Distance) continue;
                    if (hit.Owner.Contains("Glass")) continue;
                    best = hit; found = true;
                }
                if (!found) { missed++; notes.Add("camera view '" + view + "': no wall within 9 m of " + target.ToString("F0")); continue; }
                var cell = survey.Cells.Where(c => Mathf.Abs(c.Floor.y - target.y) < 1f).OrderBy(c => (new Vector2(c.Floor.x, c.Floor.z) - new Vector2(target.x, target.z)).sqrMagnitude).First();
                result.Cameras.Add(new Camera
                {
                    Position = best.Point + best.Normal * .004f, Normal = best.Normal, Target = target, Dome = false, Range = Mathf.Min(24f, best.Distance + 8f), Fov = BulletFov, FloorY = target.y,
                    Level = DetectorLayout.Level(cell), ZoneId = cell.Zone, View = view,
                });
                placed++;
            }
            notes.Add("cameras: " + placed + " bullet cameras on walls (" + missed + " views without a wall)");
        }

        private static void PlaceSpeakers(DetectorLayout.Context context, List<StationCeilings.Cell> mountable, List<StationCeilings.Cell> floor, List<Vector3> avoid, Result result, List<string> notes)
        {
            foreach (var cell in mountable)
            {
                bool horn = cell.Height >= HighCeiling || !DetectorLayout.Enclosed.Contains(cell.Zone);
                float spacing = horn ? HornSpacing : CeilingSpeakerSpacing;
                bool near = false;
                foreach (var s in result.Speakers)
                    if (s.Level == DetectorLayout.Level(cell) && Mathf.Abs(s.Position.y - cell.CeilingY) < 8f && new Vector2(s.Position.x - cell.Floor.x, s.Position.z - cell.Floor.z).magnitude < spacing) { near = true; break; }
                if (near) continue;
                var spot = context.SpotNear(cell, new Vector2(cell.Floor.x, cell.Floor.z), q => Clash(avoid, q, cell.CeilingY, .6f));
                if (spot == null) continue;
                result.Speakers.Add(new Speaker { Position = new Vector3(spot.Value.x, cell.CeilingY, spot.Value.y), Normal = cell.Normal, Horn = horn, Level = DetectorLayout.Level(cell), ZoneId = cell.Zone });
                avoid.Add(result.Speakers[result.Speakers.Count - 1].Position);
            }
            result.UncoveredSpeakerCells = floor.Count(c => !result.Speakers.Exists(s => s.Level == DetectorLayout.Level(c) && Mathf.Abs(s.Position.y - c.CeilingY) < 8f && new Vector2(s.Position.x - c.Floor.x, s.Position.z - c.Floor.z).magnitude <= 25f));
            notes.Add("speakers: " + result.Speakers.Count(s => !s.Horn) + " ceiling speakers, " + result.Speakers.Count(s => s.Horn) + " horns; " + result.UncoveredSpeakerCells + " walkable cells beyond 25 m of every speaker (NFTC 202 2.1.1.2)");
        }
    }
}
