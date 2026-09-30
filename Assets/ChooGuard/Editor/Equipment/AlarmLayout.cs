using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Where the district sounders (지구음향장치, the fire bells) and the receiver go. NFTC 203 2.5.1.3: on every floor no spot of the floor is farther than 25 m (horizontally) from a
    /// bell; 2.5.1.5: in a large space where no column or wall lies within that reach, the bell goes on the nearest wall or column. The bells hang on the walls and columns of the twin at 2.4 m
    /// (under the ceiling of a low room): for every walkable cell not yet within 25 m of a bell, the wall farthest away within 18 m in 16 directions takes the next one, which covers
    /// the most new floor. The open platforms, the tracks and the plaza have no walls to hang bells on and are the horns' ground (the broadcast system of NFTC 202 linked to the detectors
    /// takes the place of the district sounders there, NFTC 203 2.5.1.3 proviso), so only the enclosed zones of <see cref="DetectorLayout.Enclosed"/> are covered here. The receiver goes in the station office, where someone is always on duty (2.2.3.1), on a wall of the room at 1.3 m so its switches lie between 0.8 and 1.5 m
    /// (2.2.3.7): of the spots 2.5-4 m from the office point the one with most walls around it is the room, and the nearest wall face of the twin's room walls in it carries the panel.
    /// </summary>
    internal static class AlarmLayout
    {
        public const float Reach = 25f, MountHeight = 2.4f, Search = 18f, FarSearch = 30f, PanelHeight = 1.3f;

        public sealed class Bell
        {
            public Vector3 Position, Normal;
            public int Level;
            public string ZoneId = "";
            public float FloorY;
        }

        public sealed class Panel
        {
            public Vector3 Position, Normal;
            public string Zone = "", Owner = "";
        }

        public sealed class Result
        {
            public readonly List<Bell> Bells = new List<Bell>();
            public Panel Receiver;
            public int Uncovered;
        }

        private static bool Covered(List<Bell> bells, StationCeilings.Cell cell) =>
            bells.Exists(b => b.Level == DetectorLayout.Level(cell) && Mathf.Abs(b.FloorY - cell.Floor.y) < 1.5f && new Vector2(b.Position.x - cell.Floor.x, b.Position.z - cell.Floor.z).magnitude <= Reach);

        public static Result Plan(StationCeilings.Result survey, StationPoints points, StationWalls walls, List<string> notes)
        {
            var result = new Result();
            var floor = survey.Cells.Where(c => c.Walkable && DetectorLayout.Enclosed.Contains(c.Zone)).OrderBy(c => DetectorLayout.Level(c)).ThenBy(c => c.X).ThenBy(c => c.Z).ToList();
            int stuck = 0;
            foreach (var cell in floor)
            {
                if (Covered(result.Bells, cell)) continue;
                var ceiling = survey.CeilingAt(cell.Floor.x, cell.Floor.z, cell.Floor.y)?.y ?? cell.CeilingY;
                float height = Mathf.Min(MountHeight, ceiling - cell.Floor.y - .3f);
                var origin = new Vector3(cell.Floor.x, cell.Floor.y + height, cell.Floor.z);
                StationWalls.Hit best = default;
                bool found = false;
                foreach (float limit in new[] { Search, FarSearch })
                {
                    for (int k = 0; k < 16; k++)
                    {
                        float a = k * 22.5f * Mathf.Deg2Rad;
                        var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        if (!walls.Cast(origin, dir, .6f, limit, out var hit) || hit.Owner.Contains("Glass") || found && hit.Distance <= best.Distance) continue;
                        best = hit;
                        found = true;
                    }
                    if (found) break;
                }
                if (!found) { stuck++; notes.Add("bell: no wall within " + FarSearch + " m of " + cell.Floor.ToString("F0")); continue; }
                result.Bells.Add(new Bell { Position = best.Point + best.Normal * .003f, Normal = best.Normal, Level = DetectorLayout.Level(cell), ZoneId = cell.Zone, FloorY = cell.Floor.y });
            }
            result.Uncovered = floor.Count(c => !Covered(result.Bells, c));
            notes.Add("bells: " + result.Bells.Count + " on walls and columns; " + result.Uncovered + " walkable cells of the enclosed zones beyond " + Reach + " m of every bell (NFTC 203 2.5.1.3), " + stuck + " without a wall to hang one on");
            PlacePanel(survey, points, walls, result, notes);
            return result;
        }

        /// <summary>The wall face is unbroken 0.4 m either side of the hit point and 0.35 m above and below the panel's middle (no door opening, no corner).</summary>
        private static bool Backed(StationWalls walls, Vector3 room, StationWalls.Hit hit)
        {
            var tangent = Vector3.Cross(Vector3.up, hit.Normal);
            foreach (float across in new[] { -.4f, 0f, .4f })
                foreach (float lift in new[] { -.35f, .35f })
                {
                    var origin = room + Vector3.up * (PanelHeight + lift);
                    var target = hit.Point + tangent * across;
                    target.y = origin.y;
                    var direction = (target - origin).normalized;
                    if (!walls.Cast(origin, direction, .3f, 8f, out var other) || Mathf.Abs(Vector3.Dot(other.Point - hit.Point, hit.Normal)) > .05f) return false;
                }
            return true;
        }

        private static void PlacePanel(StationCeilings.Result survey, StationPoints points, StationWalls walls, Result result, List<string> notes)
        {
            var office = points.Of(PointKind.Office).OrderBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
            if (office == null) { notes.Add("receiver: the twin has no office point"); return; }
            // 사무실 안쪽: 출입구에서 2.5-4 m 떨어진 자리 가운데 둘레에 벽이 가장 많은 곳.
            Vector3 room = default;
            int enclosed = -1;
            foreach (float distance in new[] { 2.5f, 4f })
                for (int k = 0; k < 8; k++)
                {
                    float a = k * 45f * Mathf.Deg2Rad;
                    var spot = office.Position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * distance;
                    int around = 0;
                    for (int r = 0; r < 16; r++)
                    {
                        float b = r * 22.5f * Mathf.Deg2Rad;
                        if (walls.Cast(spot + Vector3.up * PanelHeight, new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)), .3f, 8f, out _)) around++;
                    }
                    if (around > enclosed) { enclosed = around; room = spot; }
                }
            StationWalls.Hit best = default;
            bool found = false;
            // 벽이 끊기는 자리(출입구 옆)를 피한다: 패널 폭(0.42 m)과 높이(0.6 m) 전체가 같은 면 뒤에 있어야 한다.
            for (int k = 0; k < 48; k++)
            {
                float a = k * 7.5f * Mathf.Deg2Rad;
                if (!walls.Cast(room + Vector3.up * PanelHeight, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), 1.2f, 8f, out var hit) || !hit.Owner.Contains("Wall") || hit.Owner.Contains("Glass") || found && hit.Distance >= best.Distance) continue;
                if (!Backed(walls, room, hit)) continue;
                best = hit;
                found = true;
            }
            if (!found) { notes.Add("receiver: no room wall within 8 m of " + room.ToString("F1") + " (enclosure " + enclosed + "/16)"); return; }
            result.Receiver = new Panel { Position = best.Point + best.Normal * .003f, Normal = best.Normal, Zone = office.Zone, Owner = best.Owner };
            notes.Add("receiver: office '" + office.Label + "' room spot " + room.ToString("F1") + " (walls around " + enclosed + "/16), panel on " + best.Owner + " at " + result.Receiver.Position.ToString("F2"));
        }
    }
}
