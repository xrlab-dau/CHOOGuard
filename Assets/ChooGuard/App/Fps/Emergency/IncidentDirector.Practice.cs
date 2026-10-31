using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// A practice shift (<see cref="PracticeShift"/>, chosen from the title's tutorial list): the world offers its causes as
    /// always, and once the shift has settled the director picks the first cause of the practised family that stands indoors
    /// in the 2F waiting hall and lets it happen — one incident, no JEV composition. The staff member finds it the way they
    /// would in a real shift (a passenger comes to tell them, or they see it). What the cause does afterwards is the ordinary
    /// product code: the collapse, the radio, the AED, the paramedics. The runtime counterpart of the editor's PracticeDrill,
    /// without the drill's record file.
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private const float PracticeSettleSeconds = 30f, PracticeOfferSeconds = 90f;
        private static readonly string[] PracticeZones = { "hall2f", "main2f" };
        private static readonly Dictionary<string, string[]> PracticeFamilies = new Dictionary<string, string[]>
        {
            ["질환"] = new[] { "collapse", "seizure", "chest_pain", "breathing", "low_blood_sugar" },
            [PracticeShift.CardiacFamily] = new[] { "chest_pain" },
        };

        private string practiceFamily;
        /// <summary>A practice shift is running: only the practised cause happens, nothing is composed.</summary>
        public bool Practicing => practiceFamily != null;

        private void BeginPractice()
        {
            var family = PracticeShift.Take();
            if (family == null || !PracticeFamilies.ContainsKey(family)) return;
            practiceFamily = family;
            log.Add("연습 근무 · " + family + " 한 건만 일어난다 (튜토리얼)");
            session.Hud.Toast("연습 근무 · 잠시 뒤 2층 맞이방에서 " + family + " 환자가 생깁니다. 승객의 알림이나 순찰로 찾아 확인(E)하고 역무실에 보고(Q)하십시오.", 10f);
            StartCoroutine(RunPractice(family));
        }

        private IEnumerator RunPractice(string family)
        {
            var wanted = PracticeFamilies[family];
            while (session.ShiftSeconds < PracticeSettleSeconds) yield return null;
            Transition chosen = null;
            float deadline = Time.realtimeSinceStartup + PracticeOfferSeconds;
            while (chosen == null && Time.realtimeSinceStartup < deadline)
            {
                foreach (var candidate in Origins())
                {
                    if (Array.IndexOf(wanted, candidate.Kind) < 0) continue;
                    if (!(candidate.Subject is Component subject)) continue;
                    var zone = world.ZoneId(subject.transform.position);
                    if (StationWorld.IsOutdoorZone(zone)) continue;
                    if (!Array.Exists(PracticeZones, z => zone.StartsWith(z, StringComparison.Ordinal))) continue;
                    chosen = candidate;
                    break;
                }
                if (chosen == null) yield return null;
            }
            if (chosen == null)
            {
                log.Add("연습 · " + PracticeOfferSeconds + "초 안에 2층 맞이방의 " + family + " 후보가 없었다");
                session.Hud.Toast("연습 · 맞이방에 " + family + " 후보가 없어 이번 근무에는 환자가 생기지 않습니다", 8f);
                yield break;
            }
            // 심정지는 4단계(의식 없음·호흡 비정상)가 곧 심정지: 크기 1.0. 질환 계열은 가운데.
            float magnitude = family == PracticeShift.CardiacFamily ? 1f : .5f;
            int before = all.Count;
            Execute(chosen, magnitude, 1, "practice");
            yield return null;
            log.Add(all.Count > before ? "연습 · 발생: " + chosen.Description : "연습 · 후보가 사이에 사라져 아무것도 생기지 않았다: " + chosen.Key);
        }
    }
}
