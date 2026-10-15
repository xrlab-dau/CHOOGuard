using System;
using System.Collections.Generic;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Who asks JEV. The lanes share one budget and are served in this order: the emergency director first (it is the
    /// game's spine), urgent crowd decisions (a person reacting to something happening) next, routine crowd decisions
    /// (what to do after a purchase or a rest) with whatever is left.
    /// </summary>
    public enum JevLane { Director, CrowdUrgent, CrowdRoutine }

    /// <summary>What one lane (or all of them) spent in the last minute, and since the shift began.</summary>
    public readonly struct JevUsage
    {
        public readonly int RequestsPerMinute;
        public readonly long TokensPerMinute;
        /// <summary>Input-token spend of the last minute, extrapolated to an hour.</summary>
        public readonly double DollarsPerHour;
        public readonly int PeakRequestsPerMinute;
        public readonly double PeakDollarsPerHour;
        public readonly long Requests;
        public readonly int Failures;
        public readonly long InputTokens;
        public readonly long OutputTokens;

        public JevUsage(int requestsPerMinute, long tokensPerMinute, int peakRequestsPerMinute, long peakTokensPerMinute, long requests, int failures, long inputTokens, long outputTokens)
        {
            RequestsPerMinute = requestsPerMinute;
            TokensPerMinute = tokensPerMinute;
            DollarsPerHour = JevBudget.DollarsPerHour(tokensPerMinute);
            PeakRequestsPerMinute = peakRequestsPerMinute;
            PeakDollarsPerHour = JevBudget.DollarsPerHour(peakTokensPerMinute);
            Requests = requests;
            Failures = failures;
            InputTokens = inputTokens;
            OutputTokens = outputTokens;
        }

        /// <summary>Total input-token spend so far.</summary>
        public double Dollars => InputTokens * JevBudget.DollarsPerMillionInputTokens / 1000000.0;
    }

    /// <summary>
    /// The JEV budget: at most <see cref="MaxRequestsPerMinute"/> requests per minute (the public limit is 1,200) and
    /// <see cref="MaxDollarsPerHour"/> of input tokens per hour of play, measured as the spend of the last minute so
    /// the hourly rate holds at every moment of a shift. Each lane may always use the share reserved for it; a lower lane
    /// may only use what the lanes above it have not used of theirs, so a full window never starves the director.
    /// Time is passed in, so the accounting is a pure function of what was charged when.
    /// </summary>
    public sealed class JevBudget
    {
        public const int MaxRequestsPerMinute = 1000;
        public const double MaxDollarsPerHour = 3;
        /// <summary>TypeSafe System One input price (output is free).</summary>
        public const double DollarsPerMillionInputTokens = 0.042;
        public const float WindowSeconds = 60f;
        public static readonly long MaxTokensPerMinute = TokensPerMinute(MaxDollarsPerHour);

        /// <summary>One request in flight: charged with an estimate when it starts, settled with JEV's own count when it ends.</summary>
        public sealed class Ticket
        {
            internal JevLane Lane;
            internal float At;
            internal long Tokens;
            internal bool InWindow = true;
        }

        private sealed class LaneAccount
        {
            public readonly int MaxInFlight, ReservedRequests;
            public readonly long ReservedTokens;
            public readonly Queue<Ticket> Window = new Queue<Ticket>();
            public int InFlight, WindowRequests, Failures, PeakRequests;
            public long WindowTokens, PeakTokens, Requests, InputTokens, OutputTokens;

            public LaneAccount(int maxInFlight, int reservedRequests, double reservedDollarsPerHour)
            {
                MaxInFlight = maxInFlight;
                ReservedRequests = reservedRequests;
                ReservedTokens = TokensPerMinute(reservedDollarsPerHour);
            }

            public void Prune(float now)
            {
                while (Window.Count > 0 && now - Window.Peek().At > WindowSeconds)
                {
                    var old = Window.Dequeue();
                    old.InWindow = false;
                    WindowRequests--;
                    WindowTokens -= old.Tokens;
                }
            }
        }

        // 순서가 우선순위다(JevLane). 디렉터의 몫은 한 분에 150건·시간당 $1.0, 급한 군중 판단은 250건·$0.75, 일상 판단은 나머지.
        private readonly LaneAccount[] lanes =
        {
            new LaneAccount(2, 150, 1.0),
            new LaneAccount(4, 250, .75),
            new LaneAccount(3, 0, 0),
        };
        private int peakRequests;
        private long peakTokens;

        public static long TokensPerMinute(double dollarsPerHour) => (long)(dollarsPerHour / 60.0 / DollarsPerMillionInputTokens * 1000000.0);

        public static double DollarsPerHour(long tokensPerMinute) => tokensPerMinute * 60.0 * DollarsPerMillionInputTokens / 1000000.0;

        /// <summary>
        /// True when a request of about <paramref name="estimatedTokens"/> input tokens may start now: the lane has an
        /// in-flight slot, and the minute still has room after what the lanes above it have reserved and not yet used.
        /// </summary>
        public bool CanSend(JevLane lane, float now, long estimatedTokens = 0)
        {
            var own = lanes[(int)lane];
            if (own.InFlight >= own.MaxInFlight) return false;
            int requests = 0, held = 0;
            long tokens = 0, heldTokens = 0;
            for (int i = 0; i < lanes.Length; i++)
            {
                var other = lanes[i];
                other.Prune(now);
                requests += other.WindowRequests;
                tokens += other.WindowTokens;
                if (i >= (int)lane) continue;
                held += Math.Max(0, other.ReservedRequests - other.WindowRequests);
                heldTokens += Math.Max(0, other.ReservedTokens - other.WindowTokens);
            }
            return requests + held < MaxRequestsPerMinute && tokens + heldTokens + estimatedTokens <= MaxTokensPerMinute;
        }

        /// <summary>A request starts: it takes an in-flight slot and its estimated tokens count against the minute at once.</summary>
        public Ticket Begin(JevLane lane, float now, long estimatedTokens)
        {
            var account = lanes[(int)lane];
            var ticket = new Ticket { Lane = lane, At = now, Tokens = estimatedTokens };
            account.Window.Enqueue(ticket);
            account.WindowRequests++;
            account.WindowTokens += estimatedTokens;
            account.InFlight++;
            account.Requests++;
            NotePeak(account);
            return ticket;
        }

        /// <summary>
        /// The request ended. <paramref name="inputTokens"/> is what JEV reported (replaces the estimate); a request that
        /// never got an answer keeps its estimate, since a timed-out request may still have been billed.
        /// </summary>
        public void End(Ticket ticket, long? inputTokens, long outputTokens, bool failed)
        {
            var account = lanes[(int)ticket.Lane];
            account.InFlight--;
            if (failed) account.Failures++;
            if (inputTokens.HasValue)
            {
                long change = inputTokens.Value - ticket.Tokens;
                if (ticket.InWindow) account.WindowTokens += change;
                ticket.Tokens = inputTokens.Value;
                account.InputTokens += inputTokens.Value;
                account.OutputTokens += outputTokens;
            }
            else account.InputTokens += ticket.Tokens;
            NotePeak(account);
        }

        public JevUsage Usage(float now)
        {
            int requests = 0, failures = 0;
            long tokens = 0, total = 0, input = 0, output = 0;
            foreach (var account in lanes)
            {
                account.Prune(now);
                requests += account.WindowRequests;
                tokens += account.WindowTokens;
                total += account.Requests;
                failures += account.Failures;
                input += account.InputTokens;
                output += account.OutputTokens;
            }
            return new JevUsage(requests, tokens, peakRequests, peakTokens, total, failures, input, output);
        }

        public JevUsage Usage(JevLane lane, float now)
        {
            var account = lanes[(int)lane];
            account.Prune(now);
            return new JevUsage(account.WindowRequests, account.WindowTokens, account.PeakRequests, account.PeakTokens, account.Requests, account.Failures, account.InputTokens, account.OutputTokens);
        }

        private void NotePeak(LaneAccount account)
        {
            account.PeakRequests = Math.Max(account.PeakRequests, account.WindowRequests);
            account.PeakTokens = Math.Max(account.PeakTokens, account.WindowTokens);
            int requests = 0;
            long tokens = 0;
            foreach (var each in lanes) { requests += each.WindowRequests; tokens += each.WindowTokens; }
            peakRequests = Math.Max(peakRequests, requests);
            peakTokens = Math.Max(peakTokens, tokens);
        }
    }
}
