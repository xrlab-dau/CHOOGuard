using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Equipment;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>The prefabs and placement entries of the CCTV cameras and PA speakers (<see cref="SurveillanceLayout"/>), built from the low-polygon converted models.</summary>
    internal static class SurveillanceBuilder
    {
        public const string Group = "Surveillance", SpeakerKind = "pa_speaker";

        public static void BuildPrefabs()
        {
            BuildCamera("DomeCamera", "DomeCameraLow", "CCTV 돔카메라");
            BuildCamera("BulletCamera", "BulletCameraLow", "CCTV 고정카메라");
            BuildSpeaker("CeilingSpeaker", "CeilingSpeakerLow", "방송 스피커 (천장형)");
            BuildSpeaker("HornSpeaker", "HornSpeakerLow", "방송 확성기 (혼형)");
        }

        private static void BuildCamera(string name, string model, string label) =>
            EquipmentBuilder.SavePrefab(new EquipmentBuilder.PrefabSpec { Name = name, Kind = SurveillanceCamera.CameraKind, Label = label, Model = EmergencySceneBuilder.ObjaverseModel(model, readable: true), DrawDistance = 32f },
                root => root.gameObject.AddComponent<SurveillanceCamera>());

        private static void BuildSpeaker(string name, string model, string label) =>
            EquipmentBuilder.SavePrefab(new EquipmentBuilder.PrefabSpec { Name = name, Kind = SpeakerKind, Label = label, Model = EmergencySceneBuilder.ObjaverseModel(model, readable: true), DrawDistance = 28f });

        private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static readonly string[] LevelCodes = { "1f", "2f", "3f" }, LevelNames = { "1층", "2층", "3층" };

        public static List<EquipmentPlacement> Items(SurveillanceLayout.Result plan)
        {
            var items = new List<EquipmentPlacement>();
            foreach (var group in plan.Cameras.GroupBy(c => c.Level).OrderBy(g => g.Key))
            {
                int number = 0;
                foreach (var c in group.OrderBy(c => c.Dome ? 0 : 1).ThenBy(c => Mathf.Round(c.Position.x * 100)).ThenBy(c => Mathf.Round(c.Position.z * 100)))
                {
                    number++;
                    Quaternion rotation;
                    if (c.Dome) rotation = Quaternion.FromToRotation(Vector3.down, c.Normal);
                    else
                    {
                        // 벽에서 나온 카메라는 목표를 향해 벽 법선에서 최대 50° 돌고 12° 숙인다.
                        var toTarget = c.Target - c.Position;
                        toTarget.y = 0;
                        var aim = Vector3.RotateTowards(c.Normal, toTarget.normalized, 50f * Mathf.Deg2Rad, 0);
                        rotation = Quaternion.LookRotation(aim, Vector3.up) * Quaternion.Euler(12f, 0, 0);
                    }
                    items.Add(new EquipmentPlacement
                    {
                        id = "cam-" + LevelCodes[c.Level] + "-" + number.ToString("000"), kind = SurveillanceCamera.CameraKind, prefab = c.Dome ? "DomeCamera" : "BulletCamera", zone = c.ZoneId,
                        position = c.Position, rotation = rotation.eulerAngles,
                        label = "CCTV " + (c.Dome ? "돔카메라 " : "고정카메라 ") + LevelNames[c.Level] + " " + number.ToString("000") + "번" + (c.Dome ? "" : " (" + c.View + ")"),
                        data = "range=" + F(c.Range) + ";fov=" + F(c.Fov) + ";floor=" + F(c.FloorY) + ";view=" + (c.Dome ? LevelNames[c.Level] + " " + c.ZoneId : c.View),
                    });
                }
            }
            foreach (var group in plan.Speakers.GroupBy(s => s.Level).OrderBy(g => g.Key))
            {
                int number = 0;
                foreach (var s in group.OrderBy(s => Mathf.Round(s.Position.x * 100)).ThenBy(s => Mathf.Round(s.Position.z * 100)))
                {
                    number++;
                    // 혼은 벽 설치형(뒷면이 설치면, 앞 +Z 로 열림): 천장에서는 아래로 열리게 세운다.
                    var rotation = s.Horn ? Quaternion.LookRotation(s.Normal, Mathf.Abs(Vector3.Dot(s.Normal, Vector3.forward)) > .9f ? Vector3.right : Vector3.forward) : Quaternion.FromToRotation(Vector3.down, s.Normal);
                    items.Add(new EquipmentPlacement
                    {
                        id = "pa-" + LevelCodes[s.Level] + "-" + number.ToString("000"), kind = SpeakerKind, prefab = s.Horn ? "HornSpeaker" : "CeilingSpeaker", zone = s.ZoneId,
                        position = s.Position, rotation = rotation.eulerAngles,
                        label = (s.Horn ? "방송 확성기(혼형) " : "방송 스피커(천장형) ") + LevelNames[s.Level] + " " + number.ToString("000") + "번",
                        data = "range=25;watt=" + (s.Horn ? 3 : 1),
                    });
                }
            }
            return items;
        }
    }
}
