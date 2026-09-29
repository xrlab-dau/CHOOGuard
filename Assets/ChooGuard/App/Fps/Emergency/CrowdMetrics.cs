using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// What the crowd's judgement costs and how fast it reacts, measured while it runs: requests and questions sent, answers
    /// by source, stale answers dropped, how long people waited, and the time from an observation to the decision that
    /// followed it (game seconds and real seconds; JEV's round trip apart). Every applied emergency decision is also written
    /// as one line of <c>crowd-*.jsonl</c> beside the JEV run log, so a shift can be audited afterwards.
    /// </summary>
    public sealed class CrowdMetrics
    {
        public int Raised, Requests, Questions, Answered, Failures, Stale, Superseded, Moot, Dropped, Escalations, Itineraries;
        /// <summary>JEV answers in which an option JEV had given a chance no longer fitted the person's state when the answer arrived.</summary>
        public int Revalidated;
        public int UrgentByJev, UrgentLocally, RoutineByJev, RoutineLocally, FirstAnswers;
        public int MaxQueued;
        /// <summary>Longest a person kept doing their current action while the next step was still being judged (game seconds).</summary>
        public float LongestWait;

        /// <summary>Observation to applied decision, game seconds (emergency decisions judged by JEV).</summary>
        public readonly List<float> ReactionGame = new List<float>();
        /// <summary>The same in real seconds.</summary>
        public readonly List<float> ReactionReal = new List<float>();
        /// <summary>JEV's round trip for emergency requests, real seconds.</summary>
        public readonly List<float> RoundTrip = new List<float>();
        /// <summary>Wait between the moment a person needed a routine answer and the answer, game seconds.</summary>
        public readonly List<float> RoutineWait = new List<float>();
        /// <summary>CPU time of <see cref="CrowdMind.Tick"/> per frame (milliseconds; the first 30,000 frames).</summary>
        public readonly List<float> TickMs = new List<float>();
        /// <summary>CPU time of applying one request's answers (milliseconds).</summary>
        public readonly List<float> ApplyMs = new List<float>();

        private readonly string path;
        private readonly StringBuilder pending = new StringBuilder();
        private readonly float startedReal;
        private float nextFlush;

        public CrowdMetrics(string logPath)
        {
            path = logPath;
            startedReal = Time.realtimeSinceStartup;
        }

        /// <summary>Seconds of real time the crowd has been judging, as of the last frame (the clock restarts when play mode ends, so it is not read later).</summary>
        public float Elapsed { get; private set; }

        public void Reaction(float game, float real, float roundTrip)
        {
            ReactionGame.Add(game);
            ReactionReal.Add(real);
            RoundTrip.Add(roundTrip);
        }

        /// <summary>
        /// One frame of <see cref="CrowdMind.Tick"/>. A frame slower than 16 ms is written to the run log with what it did
        /// (requests started, a garbage collection inside it) so a hitch can be traced to its cause.
        /// </summary>
        public void Tick(float milliseconds, bool collected, int requestsStarted, int queued)
        {
            Elapsed = Time.realtimeSinceStartup - startedReal;
            if (TickMs.Count < 30000) TickMs.Add(milliseconds);
            if (milliseconds > 16f)
                Record(new JObject { ["spike_ms"] = Math.Round(milliseconds, 1), ["at_real_s"] = Math.Round(Elapsed, 1), ["gc"] = collected, ["requests_started"] = requestsStarted, ["requests_so_far"] = Requests, ["queued"] = queued });
        }

        /// <summary>Applying one request's answers (moving several people at once, path queries included); slow ones are logged.</summary>
        public void Apply(float milliseconds, int questions)
        {
            if (ApplyMs.Count < 30000) ApplyMs.Add(milliseconds);
            if (milliseconds > 16f)
                Record(new JObject { ["apply_spike_ms"] = Math.Round(milliseconds, 1), ["at_real_s"] = Math.Round(Elapsed, 1), ["questions"] = questions });
        }

        /// <summary>Appends one decision to the run log (written in batches, never on the frame that made it).</summary>
        public void Record(JObject entry)
        {
            if (string.IsNullOrEmpty(path)) return;
            pending.Append(entry.ToString(Formatting.None)).Append('\n');
        }

        /// <summary>Writes buffered lines every couple of seconds; <paramref name="force"/> writes them now.</summary>
        public void Flush(bool force = false)
        {
            if (pending.Length == 0 || string.IsNullOrEmpty(path)) return;
            float now = Time.realtimeSinceStartup;
            if (!force && now < nextFlush) return;
            nextFlush = now + 2f;
            try { File.AppendAllText(path, pending.ToString()); }
            catch (Exception) { }
            pending.Clear();
        }

        public static float Percentile(List<float> samples, float percentile)
        {
            if (samples.Count == 0) return 0;
            var sorted = new List<float>(samples);
            sorted.Sort();
            float rank = percentile / 100f * (sorted.Count - 1);
            int low = Mathf.FloorToInt(rank);
            int high = Mathf.Min(sorted.Count - 1, low + 1);
            return Mathf.Lerp(sorted[low], sorted[high], rank - low);
        }

        private static JObject Spread(List<float> samples) => new JObject
        {
            ["n"] = samples.Count,
            ["p50"] = Math.Round(Percentile(samples, 50), 3),
            ["p95"] = Math.Round(Percentile(samples, 95), 3),
            ["max"] = samples.Count == 0 ? 0 : Math.Round(Mathf.Max(samples.ToArray()), 3),
        };

        /// <summary>Everything measured so far as JSON (for the shift log and for play-mode checks).</summary>
        public JObject Summary(JevClient jev)
        {
            float minutes = Mathf.Max(1f / 60f, Elapsed / 60f);
            var summary = new JObject
            {
                ["elapsed_real_s"] = Math.Round(Elapsed, 1),
                ["raised"] = Raised,
                ["requests"] = Requests,
                ["questions"] = Questions,
                ["answered"] = Answered,
                ["failures"] = Failures,
                ["stale_dropped"] = Stale,
                ["superseded"] = Superseded,
                ["moot"] = Moot,
                ["answers_revalidated"] = Revalidated,
                ["dropped"] = Dropped,
                ["escalations"] = Escalations,
                ["itineraries"] = Itineraries,
                ["first_answers"] = FirstAnswers,
                ["max_queued"] = MaxQueued,
                ["longest_wait_game_s"] = Math.Round(LongestWait, 1),
                ["requests_per_min"] = Math.Round(Requests / minutes, 1),
                ["decisions"] = new JObject
                {
                    ["urgent_jev"] = UrgentByJev, ["urgent_local"] = UrgentLocally,
                    ["routine_jev"] = RoutineByJev, ["routine_local"] = RoutineLocally,
                },
                ["reaction_game_s"] = Spread(ReactionGame),
                ["reaction_real_s"] = Spread(ReactionReal),
                ["jev_round_trip_s"] = Spread(RoundTrip),
                ["routine_wait_game_s"] = Spread(RoutineWait),
                ["mind_tick_ms"] = Spread(TickMs),
                ["apply_answers_ms"] = Spread(ApplyMs),
            };
            if (jev != null)
            {
                summary["jev_all_lanes"] = Lane(jev.Usage, minutes);
                summary["jev_crowd_urgent"] = Lane(jev.UsageOf(JevLane.CrowdUrgent), minutes);
                summary["jev_crowd_routine"] = Lane(jev.UsageOf(JevLane.CrowdRoutine), minutes);
                summary["jev_director"] = Lane(jev.UsageOf(JevLane.Director), minutes);
            }
            return summary;
        }

        /// <summary>The summary as one line of JSON text (for logs and play-mode checks).</summary>
        public string SummaryText(JevClient jev) => Summary(jev).ToString(Formatting.None);

        /// <summary>One lane's spend: the last minute's rate, its peak, and the average over the shift so far (input tokens only; JEV's output is free).</summary>
        private static JObject Lane(JevUsage usage, float minutes) => new JObject
        {
            ["requests"] = usage.Requests,
            ["failures"] = usage.Failures,
            ["input_tokens"] = usage.InputTokens,
            ["usd_total"] = Math.Round(usage.Dollars, 4),
            ["req_per_min_last_minute"] = usage.RequestsPerMinute,
            ["req_per_min_peak"] = usage.PeakRequestsPerMinute,
            ["req_per_min_average"] = Math.Round(usage.Requests / minutes, 1),
            ["tokens_per_min_last_minute"] = usage.TokensPerMinute,
            ["usd_per_hour_last_minute"] = Math.Round(usage.DollarsPerHour, 3),
            ["usd_per_hour_peak"] = Math.Round(usage.PeakDollarsPerHour, 3),
            ["usd_per_hour_average"] = Math.Round(usage.Dollars * 60.0 / minutes, 3),
        };
    }
}
