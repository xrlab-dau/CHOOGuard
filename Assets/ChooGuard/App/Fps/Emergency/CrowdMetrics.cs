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
        /// <summary>Times the watchdog found a person standing still over 20 s in a moving action, or waiting over 20 s for a decision (each is also written to the log with the person's trace).</summary>
        public int Frozen;
        /// <summary>The first findings of the frozen watchdog in words (who, doing what, where, with the walk's state), for a failing check or a quick look.</summary>
        public readonly List<string> FrozenFindings = new List<string>();
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
        /// <summary>Main-thread milliseconds of the steps that start a request: building the questions, building the state, the client's synchronous part.</summary>
        public readonly List<float> SendQuestionMs = new List<float>(), SendStateMs = new List<float>(), SendStartMs = new List<float>();
        /// <summary>Main-thread milliseconds applying received answers in one frame (frames that applied something); applying is time-sliced to <see cref="CrowdMind.ApplyBudgetMs"/> per frame.</summary>
        public readonly List<float> ApplyMs = new List<float>();
        /// <summary>Main-thread milliseconds of the path service in one frame (frames that served a request; the queue is worked for about a millisecond at most).</summary>
        public readonly List<float> PathMs = new List<float>();
        /// <summary>Everything the crowd costs the main thread in one frame: the judgement tick (answers applied included) plus the path service. The second list counts only frames while a hazard is active.</summary>
        public readonly List<float> CrowdFrameMs = new List<float>(), CrowdFrameIncidentMs = new List<float>();
        /// <summary>Path requests served, the longest queue of them, and pairs of places the path service could not join (each pair once).</summary>
        public int PathRequests, MaxPathQueue, UnreachablePairs;
        private RouteGraph graph;
        private float lastTickMs;

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
            lastTickMs = milliseconds;
            if (milliseconds > 16f)
                Record(new JObject { ["spike_ms"] = Math.Round(milliseconds, 1), ["at_real_s"] = Math.Round(Elapsed, 1), ["gc"] = collected, ["requests_started"] = requestsStarted, ["requests_so_far"] = Requests, ["queued"] = queued });
        }

        /// <summary>Main-thread steps of starting one request: building the questions, building the state, and the client's synchronous part (serialising, opening the web request). A slow one is logged with its parts.</summary>
        public void Step(float questions, float state, float start, int asked)
        {
            if (SendQuestionMs.Count < 30000) { SendQuestionMs.Add(questions); SendStateMs.Add(state); SendStartMs.Add(start); }
            if (questions + state + start > 16f)
                Record(new JObject { ["send_spike_ms"] = Math.Round(questions + state + start, 1), ["questions_ms"] = Math.Round(questions, 1), ["state_ms"] = Math.Round(state, 1), ["client_start_ms"] = Math.Round(start, 1), ["asked"] = asked, ["requests_so_far"] = Requests, ["at_real_s"] = Math.Round(Elapsed, 1) });
        }

        /// <summary>Applying received answers in one frame; a slow frame is logged.</summary>
        public void Apply(float milliseconds, int applied)
        {
            if (ApplyMs.Count < 30000) ApplyMs.Add(milliseconds);
            if (milliseconds > 16f)
                Record(new JObject { ["apply_spike_ms"] = Math.Round(milliseconds, 1), ["at_real_s"] = Math.Round(Elapsed, 1), ["applied"] = applied });
        }

        /// <summary>One frame of the path service, closing the frame's crowd cost (this frame's tick plus the path work).</summary>
        public void Frame(float pathMs, int served, int queued, bool incident)
        {
            if (served > 0 && PathMs.Count < 30000) PathMs.Add(pathMs);
            PathRequests += served;
            MaxPathQueue = Mathf.Max(MaxPathQueue, queued);
            float total = lastTickMs + pathMs;
            if (CrowdFrameMs.Count < 30000) CrowdFrameMs.Add(total);
            if (incident && CrowdFrameIncidentMs.Count < 30000) CrowdFrameIncidentMs.Add(total);
            if (total > 16f)
                Record(new JObject { ["crowd_frame_spike_ms"] = Math.Round(total, 1), ["tick_ms"] = Math.Round(lastTickMs, 1), ["path_ms"] = Math.Round(pathMs, 1), ["path_served"] = served, ["at_real_s"] = Math.Round(Elapsed, 1) });
        }

        /// <summary>Writes what the baked route graph holds when the world opens, and which exits parts of the station cannot reach at all (twin defects, not hidden).</summary>
        public void Graph(RouteGraph routes)
        {
            graph = routes;
            Record(new JObject { ["route_graph"] = new JObject { ["nodes"] = routes.Nodes.Count, ["edges"] = routes.EdgeCount, ["report"] = routes.Report } });
            Debug.Log("[CrowdRoutes] " + routes.Nodes.Count + " waypoints, " + routes.EdgeCount + " walks; " + routes.Report);
        }

        /// <summary>A person's goal, or an exit, that no walk reaches from where they stand: written with both positions.</summary>
        public void Unreachable(Vector3 from, Vector3 to, string why)
        {
            UnreachablePairs++;
            Record(new JObject { ["unreachable"] = new JObject { ["from"] = new JArray(Math.Round(from.x, 1), Math.Round(from.y, 1), Math.Round(from.z, 1)), ["to"] = new JArray(Math.Round(to.x, 1), Math.Round(to.y, 1), Math.Round(to.z, 1)), ["why"] = why, ["at_real_s"] = Math.Round(Elapsed, 1) } });
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
                ["apply_frame_ms"] = Spread(ApplyMs),
                ["path_frame_ms"] = Spread(PathMs),
                ["crowd_frame_ms"] = Spread(CrowdFrameMs),
                ["crowd_frame_incident_ms"] = Spread(CrowdFrameIncidentMs),
                ["path_requests"] = PathRequests,
                ["path_queue_max"] = MaxPathQueue,
                ["unreachable_pairs"] = UnreachablePairs,
                ["route_searches"] = graph != null ? graph.Searches : 0,
                ["route_search_hits"] = graph != null ? graph.SearchHits : 0,
                ["send_questions_ms"] = Spread(SendQuestionMs),
                ["send_state_ms"] = Spread(SendStateMs),
                ["send_client_start_ms"] = Spread(SendStartMs),
                ["frozen_passengers"] = Frozen,
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
