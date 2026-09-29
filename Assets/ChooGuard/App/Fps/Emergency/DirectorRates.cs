using System;
using System.Collections.Generic;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>Which kind of candidate JEV judges: the scale decides the level texts and the hazard-rate table.</summary>
    public enum ImminenceScale
    {
        /// <summary>A new emergency while none is in progress.</summary>
        CalmOrigin,
        /// <summary>A separate new emergency while the staff member is already handling one.</summary>
        IncidentOrigin,
        /// <summary>A development of an emergency that exists.</summary>
        Development,
    }

    /// <summary>
    /// How imminent a candidate is, as JEV judges it, and what the game makes of that. JEV answers a Score over the verbal
    /// levels below (no numbers or time windows: jev-1.13 reads conditions, not quantities); the game owns the table that
    /// turns a level into a hazard rate (events per second of game time), separately for calm origins, origins during an
    /// incident and developments. A candidate's rate is the expectation over JEV's level probabilities, so a level JEV
    /// finds unlikely still counts in proportion, and no interpolation between levels is ever done.
    /// </summary>
    public static class Imminence
    {
        public const int LevelCount = 5;

        private static readonly List<string> OriginLevels = new List<string>
        {
            "Not now: nothing in the situation points to it and it would be pure coincidence",
            "Possible but unremarkable: it can happen at a busy station and nothing about this person, thing or place makes it more likely than for any similar one",
            "Somewhat favoured: one concrete detail of this person, thing or place, or of the train or crowd situation, makes it more likely than for a similar one",
            "Favoured: several concrete details point to it and it would fit naturally as the next thing to happen",
            "About to happen: the last conditions for it are already in place at this moment",
        };

        private static readonly List<string> DevelopmentLevels = new List<string>
        {
            "Ruled out: what is happening now makes it impossible, or it has already happened or been dealt with",
            "Not driven: it could follow, but nothing in the current situation pushes toward it",
            "Plausible: the current situation can lead to it and nothing in place prevents it",
            "Driven: its cause is present and nothing the staff or anyone else did has removed it",
            "About to happen: its cause is fully at work at this moment",
        };

        /// <summary>Level texts of <paramref name="scale"/>, lowest first.</summary>
        public static List<string> Levels(ImminenceScale scale) => scale == ImminenceScale.Development ? DevelopmentLevels : OriginLevels;

        /// <summary>The question JEV answers for one candidate.</summary>
        public static string Instructions(string description) => "How close is the following to actually happening at this moment, given the situation? " + description;

        // 사건 빈도는 수준마다 4배로 벌어진다(0 수준은 일어나지 않음). 척도별 기준값(초당)은 JEV 의 실제 판단 분포에 맞춰 보정한다
        // (.planning/2026-09-29-jev-all-emergencies/plan.md '실시간 판단 디렉터').
        private static readonly float[] Multiplier = { 0f, 1f, 4f, 16f, 64f };
        private const float CalmOriginBase = 0f;
        private const float IncidentOriginBase = 0f;
        private const float DevelopmentBase = 0f;

        private static float Base(ImminenceScale scale) =>
            scale == ImminenceScale.CalmOrigin ? CalmOriginBase : scale == ImminenceScale.IncidentOrigin ? IncidentOriginBase : DevelopmentBase;

        /// <summary>Events per second of game time for a candidate whose imminence JEV spread over the levels as <paramref name="probabilities"/>.</summary>
        public static float Rate(ImminenceScale scale, IReadOnlyList<float> probabilities)
        {
            float expected = 0;
            for (int level = 0; level < probabilities.Count && level < Multiplier.Length; level++) expected += probabilities[level] * Multiplier[level];
            return expected * Base(scale);
        }
    }

    /// <summary>
    /// Competing risks over elapsed game time: every candidate has its own constant hazard rate during the interval; the
    /// chance that something happens is 1 − exp(−Σrate · elapsed) and which candidate it is follows the rates. The result
    /// depends only on the game time covered, never on how often or how late JEV was asked.
    /// </summary>
    public static class CompetingRisks
    {
        /// <summary>The index of the candidate that happens during <paramref name="elapsed"/> seconds, or −1 when nothing does.</summary>
        public static int Draw(IReadOnlyList<float> rates, float elapsed, System.Random random)
        {
            double total = 0;
            for (int i = 0; i < rates.Count; i++) total += Math.Max(0f, rates[i]);
            if (total <= 0 || elapsed <= 0) return -1;
            if (random.NextDouble() >= 1 - Math.Exp(-total * elapsed)) return -1;
            double roll = random.NextDouble() * total;
            int last = -1;
            for (int i = 0; i < rates.Count; i++)
            {
                if (rates[i] <= 0) continue;
                last = i;
                roll -= rates[i];
                if (roll <= 0) return i;
            }
            return last;
        }
    }
}
