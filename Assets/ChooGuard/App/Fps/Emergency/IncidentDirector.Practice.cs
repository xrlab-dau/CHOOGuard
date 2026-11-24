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
        private const float PracticeSettleSeconds = 30f;

        private PracticeScenario practice;
        /// <summary>A practice shift is running: only the practised cause happens, nothing is composed.</summary>
        public bool Practicing => practice != null;
        /// <summary>The scenario of the running practice shift (PracticeCatalog), or null in a free shift.</summary>
        public PracticeScenario Practice => practice;

        private void BeginPractice()
        {
            var scenario = PracticeCatalog.Scenario(PracticeShift.Take());
            if (scenario == null || !scenario.Available) return;
            practice = scenario;
            if (session.IsCardiacTutorial) { log.Add("고정 심정지 조작 학습 · 사건 대기 및 자동 인계 없음"); return; }
            log.Add("연습 근무 · " + scenario.Title + " 한 건만 일어난다 (튜토리얼)");
            session.Hud.Toast("연습 근무 · " + scenario.Onset, 10f);
            StartCoroutine(RunPractice(scenario));
        }

        /// <summary>
        /// The origin candidates the world offers right now, as (kind, zone id; null when the cause stands nowhere in particular).
        /// What <see cref="PracticeScenario.Accepts"/> is asked about; PlayMode tests read it to check every menu scenario can happen.
        /// </summary>
        public List<(string Kind, string Zone)> PracticeCandidates()
        {
            var found = new List<(string Kind, string Zone)>();
            foreach (var candidate in Origins())
                found.Add((candidate.Kind, candidate.Subject is Component subject && subject != null ? world.ZoneId(subject.transform.position) : null));
            return found;
        }

        private IEnumerator RunPractice(PracticeScenario scenario)
        {
            while (session.ShiftSeconds < PracticeSettleSeconds) yield return null;
            Transition chosen = null;
            float deadline = Time.realtimeSinceStartup + scenario.OfferSeconds;
            while (chosen == null && Time.realtimeSinceStartup < deadline)
            {
                foreach (var candidate in Origins())
                {
                    string zone = candidate.Subject is Component subject && subject != null ? world.ZoneId(subject.transform.position) : null;
                    if (!scenario.Accepts(candidate.Kind, zone)) continue;
                    chosen = candidate;
                    break;
                }
                if (chosen == null) yield return new WaitForSecondsRealtime(.5f);
            }
            if (chosen == null)
            {
                log.Add("연습 · " + scenario.OfferSeconds + "초 안에 " + scenario.Title + " 후보가 없었다");
                session.Hud.Toast("연습 · 이번 근무에는 " + scenario.Title + " 상황을 만들 자리가 없어 아무 일도 생기지 않습니다. Esc → 타이틀로 돌아가 다시 시작하세요.", 8f);
                yield break;
            }
            // 심정지는 4단계(의식 없음·호흡 비정상)가 곧 심정지: 크기 1.0. 화재는 손 소화기로 진화되는 낮은 크기. 나머지는 가운데.
            int before = all.Count;
            Execute(chosen, scenario.Magnitude, 1, "practice");
            yield return null;
            log.Add(all.Count > before ? "연습 · 발생: " + chosen.Description : "연습 · 후보가 사이에 사라져 아무것도 생기지 않았다: " + chosen.Key);
        }
    }
}
