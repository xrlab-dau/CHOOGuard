using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Builds the ElectricPlaza equipment group (ChooGuard/Emergency/Equipment/Electric and plaza): the electrical equipment and
    /// the concourse/plaza fittings of the twin — distribution boards, vending machines, phone-charging kiosks, litter bins,
    /// recycling stations and the cigarette-butt bins of the smoking area. The prefabs come from the converted free models
    /// (<see cref="ElectricPlazaPrefabs"/>), the positions from the standards' rules applied to the twin's measured walls
    /// (<see cref="ElectricPlazaPlacement"/>); the file is written deterministically, so a second run changes nothing.
    /// </summary>
    public static class ElectricPlazaBuilder
    {
        public const string Group = "ElectricPlaza";

        [MenuItem("ChooGuard/Emergency/Equipment/Electric and plaza")]
        public static void Build()
        {
            EquipmentBuilder.EnsureStation();
            ElectricPlazaPrefabs.BuildAll();
            var points = StationPoints.Load(AssetDatabase.LoadAssetAtPath<TextAsset>(StationNavigationBuilder.PointsPath));
            var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(StationNavigationBuilder.NavMeshPath) ?? throw new FileNotFoundException(StationNavigationBuilder.NavMeshPath);
            var instance = NavMesh.AddNavMeshData(data);
            try
            {
                var survey = WallSpots.Find(points);
                var placement = new ElectricPlazaPlacement(points, survey);
                var items = placement.Place();
                const string note = "Distribution boards, vending machines, phone-charging kiosks, litter bins, recycling stations and cigarette-butt bins. " +
                    "Written by ChooGuard.Editor.ElectricPlazaBuilder from the twin's measured walls; positions are the wall contact point, rotation faces into the room.";
                EquipmentBuilder.WritePlacements(Group, note, items);
                var perKind = items.GroupBy(i => i.kind).OrderBy(g => g.Key).Select(g => g.Key + "=" + g.Count());
                var perZone = items.GroupBy(i => i.zone).OrderBy(g => g.Key).Select(g => g.Key + "=" + g.Count());
                var area = survey.Area.OrderBy(a => a.Key).Select(a => a.Key + "=" + Mathf.RoundToInt(a.Value) + "m2");
                Debug.Log("CG_ELECTRIC_PLAZA placed=" + items.Count + " spots=" + survey.Spots.Count + " kinds[" + string.Join(" ", perKind) + "] zones[" + string.Join(" ", perZone) + "] floor[" + string.Join(" ", area) + "]");
                var table = items.GroupBy(i => i.zone).OrderBy(g => g.Key).Select(g => g.Key + " " + string.Join(" ", g.GroupBy(i => i.kind).OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Count())));
                Debug.Log("CG_ELECTRIC_PLAZA_ZONES\n" + string.Join("\n", table));
                Debug.Log("CG_ELECTRIC_PLAZA_REJECTED (count, rule)\n" + string.Join("\n", placement.Rejected.Select(r => r.Value + "\t" + r.Key)));
                string report = Path.Combine(Application.dataPath, "..", ".batch", "w4-clearance.csv");
                Directory.CreateDirectory(Path.GetDirectoryName(report));
                File.WriteAllLines(report, new[] { "id,kind,zone,free_m,wall_half_m,enclosed,door_m,landing_m,elevator_m,person_m,tightest_margin_m" }.Concat(placement.Clearances));
            }
            finally { NavMesh.RemoveNavMeshData(instance); }
        }
    }
}
