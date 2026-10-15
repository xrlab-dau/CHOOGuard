using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Equipment;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// The prefabs and placement entries of the sprinkler system (<see cref="SprinklerLayout"/>): closed heads (pendant and upright), pipe runs, alarm valve stations.
    /// A head prefab carries its bare frame under "Live" so the burst state shows without changing the batched body; a pipe prefab carries the meshes it merges its
    /// runs and hangers from when the spawner places it.
    /// </summary>
    internal static class SprinklerBuilder
    {
        public const string Group = "Sprinklers";
        private static readonly string[] LevelCodes = { "1f", "2f", "3f" }, LevelNames = { "1층", "2층", "3층" };
        private static string MeshRoot => EquipmentBuilder.Root + "/Meshes";

        public static void BuildPrefabs()
        {
            BuildHead("SprinklerHead", "SprinklerHeadLow", "SprinklerHeadLow_m7");
            BuildHead("SprinklerUpright", "SprinklerUprightLow", "SprinklerUprightLow_m0");
            BuildPipe();
            BuildValve();
        }

        /// <summary>
        /// A head prefab from a converted model: the model's mesh as "Body" and, inactive under "Live", the same frame without its glass bulb (the material named
        /// <paramref name="bulbMaterial"/>). The converted model has a material for every part (the frame of the low head has seven equal grey ones); parts with equal
        /// materials are merged into one submesh so a batch of heads draws with as few materials as it has looks.
        /// </summary>
        private static void BuildHead(string name, string modelName, string bulbMaterial)
        {
            var model = EmergencySceneBuilder.ObjaverseModel(modelName, readable: true);
            var source = model.GetComponentInChildren<MeshFilter>().sharedMesh;
            var materials = model.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            int bulb = Array.FindIndex(materials, m => m != null && m.name == bulbMaterial);
            if (bulb < 0) throw new InvalidOperationException(modelName + ": the model has no material '" + bulbMaterial + "'");
            var (bodyMesh, bodyMaterials) = MergeEqualMaterials(source, materials, -1, name + "Body");
            var (burstMesh, burstMaterials) = MergeEqualMaterials(source, materials, bulb, name + "Burst");
            EquipmentBuilder.SavePrefab(new EquipmentBuilder.PrefabSpec
            {
                Name = name, Kind = SprinklerHeadPoint.HeadKind, Label = "스프링클러 헤드", Model = null, DrawDistance = 24f,
            }, root =>
            {
                root.gameObject.AddComponent<SprinklerHeadPoint>();
                var body = new GameObject("Body");
                body.transform.SetParent(root, false);
                body.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
                body.AddComponent<MeshRenderer>().sharedMaterials = bodyMaterials;
                var live = new GameObject(EquipmentSpawner.LiveChild);
                live.transform.SetParent(root, false);
                var frame = new GameObject("Burst");
                frame.transform.SetParent(live.transform, false);
                frame.AddComponent<MeshFilter>().sharedMesh = burstMesh;
                frame.AddComponent<MeshRenderer>().sharedMaterials = burstMaterials;
                live.SetActive(false);
            });
        }

        private static bool SameLook(Material a, Material b) =>
            a == b || a.GetColor("_BaseColor") == b.GetColor("_BaseColor") && a.GetTexture("_BaseMap") == b.GetTexture("_BaseMap") && a.GetTexture("_BumpMap") == b.GetTexture("_BumpMap") &&
            a.GetTexture("_MetallicGlossMap") == b.GetTexture("_MetallicGlossMap") && Mathf.Approximately(a.GetFloat("_Metallic"), b.GetFloat("_Metallic")) && Mathf.Approximately(a.GetFloat("_Smoothness"), b.GetFloat("_Smoothness"));

        /// <summary>A copy of <paramref name="source"/> without submesh <paramref name="skip"/> (-1 keeps all) and with the submeshes of equal-looking materials merged; saved as a mesh asset.</summary>
        private static (Mesh mesh, Material[] materials) MergeEqualMaterials(Mesh source, Material[] materials, int skip, string name)
        {
            EmergencySceneBuilder.EnsureFolder(MeshRoot);
            string path = MeshRoot + "/" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            var unique = new List<Material>();
            var triangles = new List<List<int>>();
            for (int s = 0; s < source.subMeshCount; s++)
            {
                if (s == skip) continue;
                int group = unique.FindIndex(m => SameLook(m, materials[s]));
                if (group < 0) { unique.Add(materials[s]); triangles.Add(new List<int>()); group = unique.Count - 1; }
                triangles[group].AddRange(source.GetTriangles(s));
            }
            var copy = new Mesh { name = name, indexFormat = source.indexFormat };
            copy.vertices = source.vertices;
            copy.normals = source.normals;
            copy.uv = source.uv;
            copy.subMeshCount = unique.Count;
            for (int i = 0; i < unique.Count; i++) copy.SetTriangles(triangles[i], i);
            copy.RecalculateBounds();
            AssetDatabase.CreateAsset(copy, path);
            return (AssetDatabase.LoadAssetAtPath<Mesh>(path), unique.ToArray());
        }

        private static void BuildPipe()
        {
            var run = EmergencySceneBuilder.ObjaverseModel("PipeRun", readable: true);
            var hanger = EmergencySceneBuilder.ObjaverseModel("PipeHanger", readable: true);
            EquipmentBuilder.SavePrefab(new EquipmentBuilder.PrefabSpec
            {
                Name = "SprinklerPipe", Kind = SprinklerPipeLine.PipeKind, Label = "스프링클러 배관", Model = null, DrawDistance = 48f,
            }, root =>
            {
                var line = root.gameObject.AddComponent<SprinklerPipeLine>();
                var so = new SerializedObject(line);
                so.FindProperty("runMesh").objectReferenceValue = run.GetComponentInChildren<MeshFilter>().sharedMesh;
                so.FindProperty("hangerMesh").objectReferenceValue = hanger.GetComponentInChildren<MeshFilter>().sharedMesh;
                SetMaterials(so.FindProperty("runMaterials"), run.GetComponentInChildren<MeshRenderer>().sharedMaterials);
                SetMaterials(so.FindProperty("hangerMaterials"), hanger.GetComponentInChildren<MeshRenderer>().sharedMaterials);
                so.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static void SetMaterials(SerializedProperty property, Material[] materials)
        {
            property.arraySize = materials.Length;
            for (int i = 0; i < materials.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
        }

        private static void BuildValve()
        {
            EquipmentBuilder.SavePrefab(new EquipmentBuilder.PrefabSpec
            {
                Name = "AlarmValve", Kind = SprinklerValvePoint.ValveKind, Label = "유수검지장치", Model = EmergencySceneBuilder.ObjaverseModel("AlarmValve", readable: true), DrawDistance = 40f,
                Interactable = true, Batchable = true,
            }, root => root.gameObject.AddComponent<SprinklerValvePoint>());
        }

        // ── 배치 항목 ──

        private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static string P(Vector3 v) => F(v.x) + "," + F(v.y) + "," + F(v.z);
        private static readonly string[] RoleNames = { "riser", "feed", "main", "branch", "spur" };
        private static string RoleKo(string role) => role == "main" ? "교차" : role == "branch" ? "가지" : role == "spur" ? "헤드 접속" : role == "riser" ? "입상" : "급수";

        public static List<EquipmentPlacement> Items(SprinklerLayout.Result plan)
        {
            var items = new List<EquipmentPlacement>();
            // 방호구역 번호: 층마다 타일 순서로 1, 2, 3 …
            var zoneNumbers = new Dictionary<string, int>();
            foreach (var level in plan.Valves.Select(v => v.Level).Distinct().OrderBy(l => l))
            {
                int next = 1;
                foreach (var valve in plan.Valves.Where(v => v.Level == level).OrderBy(v => v.Tile.x).ThenBy(v => v.Tile.y)) zoneNumbers[valve.Key] = next++;
            }
            string ZoneName(int level, string key) => LevelNames[level] + " " + zoneNumbers[key] + "방호구역";
            string ZoneCode(int level, string key) => LevelCodes[level] + "-z" + zoneNumbers[key].ToString("00");

            var lineIds = new Dictionary<SprinklerLayout.Line, string>();
            foreach (var group in plan.Lines.GroupBy(l => l.Valve).OrderBy(g => zoneNumbers.ContainsKey(g.Key) ? g.First().Level * 1000 + zoneNumbers[g.Key] : int.MaxValue))
            {
                var counters = new Dictionary<string, int>();
                foreach (var line in group.OrderBy(l => Array.IndexOf(RoleNames, l.Role)).ThenBy(l => Mathf.Round(l.A.x * 100)).ThenBy(l => Mathf.Round(l.A.z * 100)).ThenBy(l => Mathf.Round(l.B.x * 100)).ThenBy(l => Mathf.Round(l.B.z * 100)))
                {
                    counters.TryGetValue(line.Role, out int n);
                    counters[line.Role] = ++n;
                    lineIds[line] = "sp-" + ZoneCode(line.Level, line.Valve) + "-" + line.Role + "-" + n.ToString("000");
                }
            }
            var ceilings = new Dictionary<SprinklerLayout.Line, float>();
            foreach (var group in plan.Heads.Where(h => h.Line >= 0).GroupBy(h => plan.Lines[h.Line])) ceilings[group.Key] = group.Average(h => h.Ceiling.y);

            foreach (var line in lineIds.OrderBy(p => p.Value, StringComparer.Ordinal))
            {
                var l = line.Key;
                float ceil = ceilings.TryGetValue(l, out float mean) ? mean : l.A.y + (l.Exposed ? SprinklerLayout.PipeDrop : -SprinklerLayout.PlenumRise);
                string zone = ZoneName(l.Level, l.Valve);
                items.Add(new EquipmentPlacement
                {
                    id = line.Value, kind = SprinklerPipeLine.PipeKind, prefab = "SprinklerPipe", zone = "", position = l.A, rotation = Vector3.zero,
                    label = "스프링클러 " + RoleKo(l.Role) + " 배관 DN" + l.Dn + " " + zone + " " + line.Value.Substring(line.Value.Length - 3) + "번",
                    data = "a=" + P(l.A) + ";b=" + P(l.B) + ";dn=" + l.Dn + ";role=" + l.Role + ";exposed=" + (l.Exposed ? 1 : 0) + ";valve=" + l.Valve + ";heads=" + l.Heads + ";ceil=" + F(ceil) + ";floor=" + F(l.FloorY) + ";zone=" + zone,
                });
            }

            foreach (var group in plan.Heads.GroupBy(h => h.Valve).OrderBy(g => g.First().Level * 1000 + zoneNumbers[g.Key]))
            {
                int number = 0;
                foreach (var h in group.OrderBy(h => Mathf.Round(h.Ceiling.x * 100)).ThenBy(h => Mathf.Round(h.Ceiling.z * 100)))
                {
                    number++;
                    var line = plan.Lines[h.Line];
                    string zone = ZoneName(h.Level, h.Valve);
                    Vector3 position = h.Ceiling;
                    var rotation = Quaternion.FromToRotation(Vector3.down, h.Normal);
                    if (h.Exposed)
                    {
                        // 노출 배관 위에 똑바로 세운 헤드: 관 윗면에 앉는다.
                        position = new Vector3(h.Ceiling.x, line.A.y + SprinklerPipeLine.OuterDiameter(line.Dn) * .5f, h.Ceiling.z);
                        rotation = Quaternion.identity;
                    }
                    items.Add(new EquipmentPlacement
                    {
                        id = "sh-" + ZoneCode(h.Level, h.Valve) + "-" + number.ToString("000"), kind = SprinklerHeadPoint.HeadKind, prefab = h.Exposed ? "SprinklerUpright" : "SprinklerHead",
                        zone = h.ZoneId, position = position, rotation = rotation.eulerAngles,
                        label = "스프링클러 헤드 " + zone + " " + number.ToString("000") + "번",
                        data = "valve=" + h.Valve + ";pipe=" + lineIds[line] + ";mount=" + (h.Exposed ? "upright" : "pendant") + ";reach=" + F(SprinklerLayout.Reach) + ";temp=68;floor=" + F(h.FloorY) + ";height=" + F(h.Height) + ";zone=" + zone,
                    });
                }
            }

            foreach (var valve in plan.Valves.OrderBy(v => v.Level * 1000 + zoneNumbers[v.Key]))
            {
                string zone = ZoneName(valve.Level, valve.Key);
                items.Add(new EquipmentPlacement
                {
                    id = "sv-" + ZoneCode(valve.Level, valve.Key), kind = SprinklerValvePoint.ValveKind, prefab = "AlarmValve", zone = "",
                    position = valve.Position, rotation = Quaternion.LookRotation(valve.Normal, Vector3.up).eulerAngles,
                    label = "유수검지장치 " + zone,
                    data = "key=" + valve.Key + ";zone=" + zone + ";heads=" + valve.Heads + ";area=" + F(valve.Area),
                });
            }
            return items;
        }

        public static IEnumerable<string> Log(SprinklerLayout.Result plan, List<string> notes)
        {
            yield return "CG_SPRINKLERS heads=" + plan.Heads.Count + " (pendant " + plan.Heads.Count(h => !h.Exposed) + ", upright " + plan.Heads.Count(h => h.Exposed) + ", audit " + plan.Heads.Count(h => h.Spur) + ") pipes=" + plan.Lines.Count + " valves=" + plan.Valves.Count;
            yield return "  pipe metres by kind: " + string.Join(", ", plan.Lines.GroupBy(l => (l.Exposed, l.Role)).OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2, StringComparer.Ordinal).Select(g => (g.Key.Item1 ? "exposed " : "concealed ") + g.Key.Item2 + " " + g.Sum(l => Vector3.Distance(l.A, l.B)).ToString("F0")));
            if (plan.Audit != null)
            {
                yield return "CG_PIPE_AUDIT exposed horizontal pipe below " + SprinklerLayout.ClearHeight.ToString("0.0") + " m above the walkable floor = " + plan.Audit.LowPipes + " (lowest " + (plan.Audit.LowestClearance == float.MaxValue ? "n/a" : plan.Audit.LowestClearance.ToString("0.00") + " m") + "); interpenetrating pipe pairs outside joints = " + plan.Audit.Interpenetrating
                    + "; crossing pairs closer than 5 cm = " + plan.Audit.TooClose + " (" + plan.Audit.PairsChecked + " pairs checked)";
                foreach (var note in plan.Audit.Notes) yield return "  audit " + note;
            }
            foreach (var r in plan.Regions) yield return "  region " + r.Name + ": cells(m2)=" + r.Cells + " heads=" + r.Heads + " m2/head=" + r.AreaPerHead.ToString("0.0") + " (limit " + (SprinklerLayout.Spacing * SprinklerLayout.Spacing).ToString("0.0") + ") uncovered=" + r.Uncovered;
            foreach (var v in plan.Valves.OrderBy(v => v.Level).ThenBy(v => v.Tile.x).ThenBy(v => v.Tile.y)) yield return "  zone " + v.Key + ": heads=" + v.Heads + " area(m2)=" + v.Area.ToString("F0") + " (limit 3000)";
            foreach (var n in notes.Take(30)) yield return "  note " + n;
        }
    }
}
