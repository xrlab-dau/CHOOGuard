using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Real-time judgement of the people in the station, asked of JEV as typed Choice questions over options the game builds
    /// from the live world. Something a person observes (they see, hear or smell a hazard, hear the alarm bell or an
    /// announcement, are told by staff, find their way blocked, arrive) raises a judgement at once. Everyday life is a plan:
    /// JEV is asked once when a person becomes part of the station (and the first time they need one), answers with its
    /// probabilities over what they might do, and the person then carries the plan out step by step in code, choosing each next
    /// activity among the options that still fit and re-resolving the places they were bound to. A plan is asked for again only
    /// when what it was made on changes for that person (the train, their ticket, the person they meet, an incident they learn
    /// of) or nothing is left in it. A question holds only what that person could know, and people share a request only when
    /// their observations are identical. Requests are scheduled by priority inside the crowd's share of JEV's budget. While an
    /// answer is late, or the budget is spent, the person keeps doing what they were doing; when it arrives it is checked
    /// against their current state first. Only physical reflexes (stepping back from fire, out of smoke) are decided in code,
    /// and local weights decide only in runs without JEV (no key, or TYPESAFE_API_KEY=off); those weights are design
    /// heuristics, not measured behaviour data.
    /// </summary>
    public sealed partial class CrowdMind
    {
        /// <summary>Why a judgement is needed, most urgent first (the value is the priority).</summary>
        public enum Trigger { Quake, AfterQuake, Notice, Changed, Blocked, Instruction, Cue, Ended, Routine, Route }
        /// <summary>Emergency questions per request: one bell or announcement reaches everyone at once, and a request holds about a dozen questions in the time of one.</summary>
        public int UrgentBatch = 12;
        public int RoutineBatch = 12;
        /// <summary>Requests started per frame at most (building a request is not free).</summary>
        public int SendsPerFrame = 3;

        /// <summary>What a person's judgement keeps between requests; owned by the <see cref="Passenger"/>.</summary>
        public sealed class Slot
        {
            internal Judgement Urgent, Routine, Route;
            /// <summary>JEV's plan for this person's everyday course, carried out locally; replaced when what it was made on changes.</summary>
            internal Plan Plan;
            /// <summary>JEV's last answer to "they finished what they were doing" while what they observe has stayed the same.</summary>
            internal Stance Stance;
            /// <summary>Raised whenever what the person observes changes; an answer built on an older version is stale.</summary>
            internal int Version;
            internal float JudgedAt = -1000, WaitingSince = -1;
            /// <summary>Keeps doing their current action because the next step is not decided yet.</summary>
            public bool Waiting => WaitingSince >= 0;
            /// <summary>What they last saw of each hazard they perceived directly.</summary>
            internal readonly Dictionary<Hazard, string> Seen = new Dictionary<Hazard, string>();
            /// <summary>What they heard rather than saw: the alarm bell, people running, an announcement.</summary>
            internal readonly List<string> Heard = new List<string>();
            internal string Announcement;
            internal bool ToldByStaff, RouteAsked;
            internal readonly List<string> Acts = new List<string>();
            /// <summary>The last things that happened to this person's judgement (raised, sent, answered, done), to trace a stuck person afterwards.</summary>
            internal readonly List<string> Trace = new List<string>();
            internal Vector3 Anchor;
            internal float AnchorSince = -1;
            internal Passenger.Activity AnchorActivity;
            internal bool FrozenReported;
            /// <summary>When JEV's silence last sent this person on with their trip; they wait a few seconds again from there.</summary>
            internal float ContinuedAt = -1000;

            internal void Log(string what)
            {
                Trace.Add(Time.time.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + what);
                if (Trace.Count > 16) Trace.RemoveAt(0);
            }

            internal void Forget()
            {
                Seen.Clear();
                Heard.Clear();
                Acts.Clear();
                Announcement = null;
                ToldByStaff = false;
                Stance = null;
            }
        }

        /// <summary>Applying received answers may use this much of a frame (milliseconds); the rest waits for the next frame, so one frame never moves a whole crowd.</summary>
        public float ApplyBudgetMs = 1f;
        private readonly List<(Judgement item, JevAnswer answer)> receivedUrgent = new List<(Judgement, JevAnswer)>();
        private readonly List<(Judgement item, JevAnswer answer)> receivedEveryday = new List<(Judgement, JevAnswer)>();
        private float nextWatch, applyItemMs = .1f;
        /// <summary>Requests in a row that came back without an answer (0 while JEV answers).</summary>
        private int failuresInARow;
        /// <summary>Seconds a person waits for a judgement, while JEV is failing, before they carry on with their trip.</summary>
        public float OutageSeconds = 5f;
        /// <summary>How the crowd's requests reach JEV: its client (null). A check sets one that never answers before the shift loads, to see the crowd keep going through an outage; nothing then leaves the process.</summary>
        public static Func<string, object, IReadOnlyList<JevChoice>, Action<Dictionary<string, JevAnswer>>, JevLane, IEnumerator> Transport;

        /// <summary>One thing a person could do next, with where and for how long.</summary>
        public sealed class Choice
        {
            /// <summary>Stable for the life of a plan: the same key is the same wish ("browse that shop", "leave by that exit"), never a position in a shuffled list.</summary>
            public string Key, Description, Remember, Filter;
            /// <summary>Standing places: the landmark (point label) preferred inside zone <see cref="Filter"/>.</summary>
            public string Landmark;
            public float Weight, Seconds;
            /// <summary>How long the activity lasts, drawn when the step starts (the train may be nearer now than when the option was built); null for none.</summary>
            public Func<float> Length;
            /// <summary>What carrying it out does to the wish for it: 1 nothing (waiting, sitting), between 0 and 1 less each time (a visit), 0 once only.</summary>
            public float Fade = 1;
            public Passenger.Activity Activity;
            public PointKind Kind;
            public StationPoints.Point Place;
            /// <summary>Still sensible now (the train may have left since JEV was asked); null when nothing can change.</summary>
            public Func<bool> Guard;
            /// <summary>Finds the place again when it moves (the person they meet); null when the place is fixed.</summary>
            public Func<StationPoints.Point> Resolve;
        }

        private sealed class Option
        {
            public string Key, Description;
            public Action<Passenger, Hazard, StationWorld> Run;
        }

        /// <summary>One question about one person, from the moment it was needed until it is applied or dropped.</summary>
        internal sealed class Judgement
        {
            public Passenger Who;
            public Trigger Trigger;
            public Hazard Hazard;
            /// <summary>Instruction: staff spoke to them (not an announcement). Notice: perceived directly.</summary>
            public bool Direct;
            /// <summary>Emergency lane; everyday questions use it when the person has been waiting for the answer.</summary>
            public bool Urgent;
            public bool Everyday;
            public bool First;
            public bool Sent, Done, Superseded, Received;
            public float Raised, RaisedReal, Due, NotBefore, ReadyAt, SentReal, AnsweredAt;
            public int Version, Failures, BatchSize;
            public string Key, Note;
            public HashSet<string> Offered;
            public List<Choice> Choices;
            /// <summary>Everyday questions: what the plan will be made on (the person's trip and knowledge when the question was built).</summary>
            public Basis Basis;
        }

        private readonly CrowdDirector crowd;
        private readonly JevClient jev;
        private readonly List<Judgement> queue = new List<Judgement>();
        private readonly List<Judgement> ready = new List<Judgement>();
        private float nextScan, nextProbe;
        private int serial;

        public CrowdMetrics Metrics { get; }

        /// <summary>JEV is there to answer (a key that works and is not switched off). Without it local weights decide.</summary>
        public bool Usable => jev != null && jev.Available;

        public CrowdMind(CrowdDirector crowd, JevClient jev)
        {
            this.crowd = crowd;
            this.jev = jev;
            var folder = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "jev-runs");
            string path = null;
            try
            {
                System.IO.Directory.CreateDirectory(folder);
                path = System.IO.Path.Combine(folder, "crowd-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".jsonl");
            }
            catch (Exception) { }
            Metrics = new CrowdMetrics(path);
            InitRoutine();
            // 근무 첫 요청 프레임에 Tick 이 한 번 133 ms 걸렸다(측정). 첫 JSON 직렬화의 리플렉션 준비로 보여, 적재 중에 한 번 미리 돌린다.
            Newtonsoft.Json.Linq.JToken.FromObject(new Dictionary<string, object> { ["warm"] = new List<object> { new { what = "x", where = "y" } } }).ToString(Newtonsoft.Json.Formatting.None);
        }

        // ── 관측이 바뀌었을 때 ─────────────────────────────────────────────────

        /// <summary>The person registers <paramref name="hazard"/>: they perceive it themselves, or pick up on a cue (bell, running people, announcement).</summary>
        public void OnNotice(Passenger who, Hazard hazard, bool indirect)
        {
            var slot = who.Slot;
            if (indirect) Hear(slot, hazard, who.Cue); else slot.Seen[hazard] = hazard.Visible;
            var trigger = hazard is EarthquakeHazard quake ? (quake.Shaking ? Trigger.Quake : Trigger.AfterQuake) : indirect ? Trigger.Cue : Trigger.Notice;
            Raise(who, trigger, hazard, !indirect);
        }

        /// <summary>Staff (<paramref name="direct"/>) or a public announcement tells them to leave.</summary>
        public void OnInstruction(Passenger who, bool direct)
        {
            var slot = who.Slot;
            if (direct) slot.ToldByStaff = true; else slot.Announcement = Announcement(who.Focus) ?? slot.Announcement;
            Raise(who, Trigger.Instruction, who.Focus, direct);
        }

        /// <summary>A hazard they perceive has changed since they last looked (it grew, spread, was dealt with).</summary>
        public void Observe(Passenger who, Hazard hazard)
        {
            var seen = who.Slot.Seen;
            string now = hazard.Visible;
            if (seen.TryGetValue(hazard, out var before) && before == now) return;
            seen[hazard] = now;
            if (before != null) Raise(who, Trigger.Changed, hazard, true, before);
        }

        /// <summary>The way to where they were heading is blocked.</summary>
        public void OnBlocked(Passenger who)
        {
            // 걷던 일상 걸음의 목적지가 막혔다: 그 계획은 이 걸음을 다시 고르지 않는다.
            var plan = who.Slot.Plan;
            if (plan != null && plan.Active != null && who.Current == Passenger.Activity.Walk) plan.Struck.Add(plan.Active);
            Raise(who, Trigger.Blocked, who.Focus, true);
        }

        /// <summary>They reached where they were going in an emergency, or finished watching.</summary>
        public void OnEnded(Passenger who) => Raise(who, Trigger.Ended, who.Focus, true);

        public void AfterQuake(Passenger who) => Raise(who, Trigger.AfterQuake, who.Focus, false);

        private void Hear(Slot slot, Hazard hazard, string cue)
        {
            if (string.IsNullOrEmpty(cue)) return;
            if (!slot.Heard.Contains(cue)) slot.Heard.Add(cue);
            if (IsAnnouncement(cue)) slot.Announcement = Announcement(hazard) ?? slot.Announcement;
        }

        // 방송을 들었다는 단서('an announcement …')는 IncidentDirector 가 문장으로 넘긴다. 방송 내용은 그 사건의 방송 문안이다.
        private static bool IsAnnouncement(string cue) => cue.StartsWith("an announcement", StringComparison.Ordinal);

        private static string Announcement(Hazard hazard) => hazard != null && hazard.Active ? hazard.Announcement.Text : null;

        private void Raise(Passenger who, Trigger trigger, Hazard hazard, bool direct, string note = null)
        {
            if (who == null) return;
            var slot = who.Slot;
            var old = slot.Urgent;
            bool pending = old != null && !old.Done;
            // 도착·관찰 끝남은 이미 판단이 진행 중이면 그 판단에 맡긴다. 관측이 실제로 바뀐 것만 진행 중 판단을 낡게 만든다.
            bool soft = trigger == Trigger.Ended;
            if (soft && pending) return;
            // 관측이 그대로인데 하던 일이 끝났다: 지난번 답의 확률로 지금 맞는 선택지 중에서 다시 고른다(JEV 에 다시 묻지 않는다).
            if (soft && Reuse(who, hazard)) return;
            if (!soft) slot.Version++;
            float raised = Time.time, raisedReal = Time.realtimeSinceStartup;
            if (pending)
            {
                if (!old.Sent && !old.Received)
                {
                    // 아직 보내지 않았다: 같은 판단에 새 사실을 합친다(가장 급한 이유로 묻는다).
                    if (trigger < old.Trigger) old.Trigger = trigger;
                    old.Hazard = hazard ?? old.Hazard;
                    old.Direct |= direct;
                    old.Note = note ?? old.Note;
                    return;
                }
                // 이미 보냈다: 그 답은 낡았다. 새 판단은 처음 관측한 시각부터 잰다.
                old.Superseded = true;
                Metrics.Superseded++;
                raised = old.Raised;
                raisedReal = old.RaisedReal;
            }
            slot.Log("raise " + trigger + (hazard != null ? " " + hazard.Label : ""));
            var world = crowd.World;
            var item = new Judgement
            {
                Who = who, Trigger = trigger, Hazard = hazard, Direct = direct, Urgent = true, Note = note,
                Raised = raised, RaisedReal = raisedReal, Due = raised,
                // 사람이 알아차리고 움직이기까지의 반응 시간: JEV 없이 지역 규칙으로 정할 때만 쓴다(JEV 답이 오는 시간이 곧 반응 시간).
                ReadyAt = Time.time + (trigger == Trigger.Quake ? world.Range(.2f, .7f) : world.Range(.5f, 1.4f)),
                Key = "p" + who.Number + "_" + (++serial),
            };
            slot.Urgent = item;
            queue.Add(item);
            Metrics.Raised++;
            Metrics.MaxQueued = Mathf.Max(Metrics.MaxQueued, queue.Count);
        }

        private void Finish(Judgement item)
        {
            item.Done = true;
            var slot = item.Who != null ? item.Who.Slot : null;
            if (slot == null) return;
            if (slot.Urgent == item) slot.Urgent = null;
            if (slot.Routine == item) slot.Routine = null;
            if (slot.Route == item) slot.Route = null;
        }

        // ── 주기 ────────────────────────────────────────────────────────────

        public void Tick()
        {
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            int collections = GC.CollectionCount(0), startedBefore = Metrics.Requests;
            float now = Time.time;
            bool usable = Usable;
            int resolved = 0;
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                var item = queue[i];
                if (item.Done || item.Who == null) { if (!item.Done && item.Who == null) item.Done = true; queue.RemoveAt(i); continue; }
                if (item.Sent || item.Received) continue;
                if (!usable)
                {
                    // JEV 없는 근무: 일상 판단은 활동이 끝날 때 지역 규칙이 정하고, 급한 판단은 반응 시간 뒤 지역 규칙이 정한다. 답을 적용할 때와 같은 예산으로 나눠 한 프레임에 몰지 않는다.
                    if (item.Everyday) { Finish(item); queue.RemoveAt(i); Metrics.Dropped++; }
                    else if (now >= item.ReadyAt)
                    {
                        if (resolved > 0 && Ms(System.Diagnostics.Stopwatch.GetTimestamp() - began) + applyItemMs * 1.5f > ApplyBudgetMs * .8f) continue;
                        queue.RemoveAt(i);
                        long started = System.Diagnostics.Stopwatch.GetTimestamp();
                        ResolveLocally(item);
                        resolved++;
                        applyItemMs = Mathf.Lerp(applyItemMs, Mathf.Min(2f, Ms(System.Diagnostics.Stopwatch.GetTimestamp() - started)), .05f);
                    }
                    continue;
                }
            }
            DrainApply();
            Watchdog(now);
            if (usable)
            {
                ScanPlans(now);
                Dispatch(now);
            }
            Metrics.Flush();
            Metrics.Tick((System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000f / System.Diagnostics.Stopwatch.Frequency, GC.CollectionCount(0) != collections, Metrics.Requests - startedBefore, queue.Count);
        }

        /// <summary>
        /// Twice a second: nobody may stand still for over 20 s while their action is one that moves (walking, moving away,
        /// leaving, evacuating), and nobody may wait over 20 s for a decision. A finding is counted and written with that
        /// person's own trace (raised, sent, answered, done).
        /// </summary>
        private void Watchdog(float now)
        {
            if (now < nextWatch) return;
            nextWatch = now + .5f;
            foreach (var person in crowd.People)
            {
                if (person == null || person.Hurt || person.Hostile) continue;
                var slot = person.Slot;
                var activity = person.Current;
                bool moves = activity == Passenger.Activity.Walk || activity == Passenger.Activity.MoveAway || activity == Passenger.Activity.Evacuate || activity == Passenger.Activity.Leave;
                // 목적지 3 m 안에 닿은 사람은 굳은 것이 아니다: 출구 문 앞에서는 역무원 눈에 띄지 않게 될 때까지 서 있는 것이 원래 규칙이다(Passenger.Update).
                if ((person.Body.Goal - person.transform.position).sqrMagnitude < 9f) moves = false;
                float pending = 0;
                if (slot.WaitingSince >= 0 && person.Holding) pending = now - Mathf.Max(slot.WaitingSince, slot.ContinuedAt);
                // JEV 가 답하지 않아 여정을 잇는 사람은 걷는 동안 기다리는 사람이 아니다.
                if (slot.Urgent != null && !slot.Urgent.Done && !(slot.ContinuedAt > slot.Urgent.Raised && !person.Idle)) pending = Mathf.Max(pending, now - Mathf.Max(slot.Urgent.Raised, slot.ContinuedAt));
                ContinueStill(person, now);
                var body = person.Body;
                if (!moves || body.Riding != null || body.Scripted)
                {
                    slot.AnchorSince = -1;
                }
                else
                {
                    var here = person.transform.position;
                    if (slot.AnchorSince < 0 || slot.AnchorActivity != activity || (here - slot.Anchor).sqrMagnitude > .25f)
                    {
                        slot.Anchor = here;
                        slot.AnchorSince = now;
                        slot.AnchorActivity = activity;
                        slot.FrozenReported = false;
                    }
                    else if (now - slot.AnchorSince > 20f && !slot.FrozenReported)
                    {
                        slot.FrozenReported = true;
                        ReportStuck(person, "no_progress", now - slot.AnchorSince);
                    }
                }
                if (pending > 20f && !slot.FrozenReported) { slot.FrozenReported = true; ReportStuck(person, "no_decision", pending); }
            }
        }

        private void ReportStuck(Passenger person, string kind, float seconds)
        {
            Metrics.Frozen++;
            if (Metrics.FrozenFindings.Count < 40) Metrics.FrozenFindings.Add("#" + person.Number + " " + kind + " " + person.Current + " " + System.Math.Round(seconds, 1) + " s: " + person.WalkState());
            Metrics.Record(new Newtonsoft.Json.Linq.JObject
            {
                ["frozen"] = kind, ["passenger"] = person.Number, ["activity"] = person.Current.ToString(), ["seconds"] = System.Math.Round(seconds, 1),
                ["at"] = person.Doing, ["to_goal_m"] = System.Math.Round(Vector3.Distance(person.Body.Goal, person.transform.position), 1),
                ["seen_by_staff"] = PlayerView.Sees(person.transform.position, 45), ["walk"] = person.WalkState(), ["trace"] = new Newtonsoft.Json.Linq.JArray(person.Slot.Trace),
            });
        }

        // ── 보내기 ──────────────────────────────────────────────────────────

        private void Dispatch(float now)
        {
            // 접속이 계속 실패하면 다시 물음을 줄여, 서버가 돌아오기 전에 요청이 쌓여 쏟아지지 않게 한다.
            if (failuresInARow >= 3)
            {
                if (Time.realtimeSinceStartup < nextProbe) return;
                nextProbe = Time.realtimeSinceStartup + 2f;
            }
            for (int sends = 0; sends < SendsPerFrame; sends++)
            {
                ready.Clear();
                float real = Time.realtimeSinceStartup;
                foreach (var item in queue)
                    if (!item.Sent && !item.Received && !item.Done && item.Who != null && real >= item.NotBefore) ready.Add(item);
                if (ready.Count == 0) return;
                ready.Sort(ByPriority);
                // 급한 줄이 가득 차 못 보내도 일상 줄에 자리가 있으면 그쪽 첫 질문은 보낸다(예산은 JEV 클라이언트가 지킨다).
                bool urgentOpen = CanSend(true), routineOpen = CanSend(false);
                Judgement first = null;
                foreach (var candidate in ready)
                    if (candidate.Urgent ? urgentOpen : routineOpen) { first = candidate; break; }
                if (first == null) return;
                int capacity = first.Urgent ? UrgentBatch : RoutineBatch;
                var batch = new List<Judgement>(capacity) { first };
                string signature = Signature(first.Who);
                for (int i = 1; i < ready.Count && batch.Count < capacity; i++)
                    if (ready[i].Urgent == first.Urgent && Signature(ready[i].Who) == signature) batch.Add(ready[i]);
                Send(batch, first.Urgent);
            }
        }

        private static int ByPriority(Judgement a, Judgement b)
        {
            if (a.Urgent != b.Urgent) return a.Urgent ? -1 : 1;
            if (a.Trigger != b.Trigger) return a.Trigger < b.Trigger ? -1 : 1;
            // 같은 계기면 위험에 가까운 사람부터: 예산이 모자랄 때 방송·종소리에도 불 곁 사람이 먼저 판단받는다.
            float da = DistanceToHazard(a), db = DistanceToHazard(b);
            if (da != db) return da < db ? -1 : 1;
            return a.Due.CompareTo(b.Due);
        }

        private static float DistanceToHazard(Judgement item) =>
            item.Hazard != null && item.Hazard.Localized && item.Who != null ? Vector3.Distance(item.Who.transform.position, item.Hazard.Position) : float.MaxValue;

        private static JevLane LaneOf(bool urgent) => urgent ? JevLane.CrowdUrgent : JevLane.CrowdRoutine;

        private bool CanSend(bool urgent) => jev.CanSend(LaneOf(urgent));

        private IEnumerator Request(bool urgent, string purpose, object state, IReadOnlyList<JevChoice> questions, Action<Dictionary<string, JevAnswer>> done) =>
            Transport != null ? Transport(purpose, state, questions, done, LaneOf(urgent)) : jev.Ask(purpose, state, questions, done, LaneOf(urgent));

        private void Send(List<Judgement> batch, bool urgent)
        {
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            var asked = new List<Judgement>(batch.Count);
            var questions = new List<JevChoice>(batch.Count);
            float real = Time.realtimeSinceStartup;
            foreach (var item in batch)
            {
                var question = Question(item);
                if (question == null) { Finish(item); Metrics.Dropped++; continue; }
                item.Version = item.Who.Slot.Version;
                item.Sent = true;
                item.SentReal = real;
                item.BatchSize = batch.Count;
                item.Who.Slot.Log("sent in a request of " + batch.Count);
                asked.Add(item);
                questions.Add(question);
            }
            if (asked.Count == 0) return;
            long questionsBuilt = System.Diagnostics.Stopwatch.GetTimestamp();
            Metrics.Requests++;
            Metrics.Questions += asked.Count;
            var state = State(asked[0]);
            long stateBuilt = System.Diagnostics.Stopwatch.GetTimestamp();
            // 클라이언트의 동기 구간(JSON 직렬화·SendWebRequest 시작)이 여기에 든다. 그 안은 JevClient 소관이라 겉에서 한 덩어리로 잰다.
            crowd.StartCoroutine(Request(urgent, urgent ? "crowd-urgent" : "crowd-routine", state, questions, answers => OnAnswers(asked, answers)));
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            Metrics.Step(Ms(questionsBuilt - began), Ms(stateBuilt - questionsBuilt), Ms(started - stateBuilt), asked.Count);
        }

        private static float Ms(long ticks) => ticks * 1000f / System.Diagnostics.Stopwatch.Frequency;

        /// <summary>JEV's answers arrive: they only queue up here; <see cref="DrainApply"/> applies them within the frame budget.</summary>
        private void OnAnswers(List<Judgement> asked, Dictionary<string, JevAnswer> answers)
        {
            failuresInARow = answers == null ? failuresInARow + 1 : 0;
            foreach (var item in asked)
            {
                item.Sent = false;
                if (item.Done) continue;
                if (item.Who == null) { Finish(item); continue; }
                JevAnswer answer = null;
                answers?.TryGetValue(item.Key, out answer);
                // 그 사이 같은 사람에게 더 새로운 관측이 생겨 새 판단이 이 판단을 대신한다: 이 답은 쓰지 않는다.
                if (item.Superseded) { Finish(item); if (answer != null) Metrics.Stale++; continue; }
                if (answer == null) { item.Who.Slot.Log("no answer"); Failed(item); continue; }
                Metrics.Answered++;
                item.AnsweredAt = Time.time;
                item.Received = true;
                item.Who.Slot.Log("answered after " + (Time.realtimeSinceStartup - item.SentReal).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s");
                (item.Everyday ? receivedEveryday : receivedUrgent).Add((item, answer));
            }
        }

        /// <summary>Applies received answers, urgent ones first, while the average answer still fits in <see cref="ApplyBudgetMs"/>; at least one per frame so nothing waits forever.</summary>
        private void DrainApply()
        {
            if (receivedUrgent.Count == 0 && receivedEveryday.Count == 0) return;
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            int applied = 0;
            while (receivedUrgent.Count > 0 || receivedEveryday.Count > 0)
            {
                // 다음 답을 적용하면 예산을 넘길 것 같으면 다음 프레임으로 미룬다(프레임마다 하나는 반드시 적용한다).
                if (applied > 0 && Ms(System.Diagnostics.Stopwatch.GetTimestamp() - began) + applyItemMs * 1.5f > ApplyBudgetMs * .8f) break;
                var list = receivedUrgent.Count > 0 ? receivedUrgent : receivedEveryday;
                var (item, answer) = list[0];
                list.RemoveAt(0);
                item.Received = false;
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                ApplyReceived(item, answer);
                applied++;
                applyItemMs = Mathf.Lerp(applyItemMs, Mathf.Min(2f, Ms(System.Diagnostics.Stopwatch.GetTimestamp() - started)), .05f);
            }
            Metrics.Apply(Ms(System.Diagnostics.Stopwatch.GetTimestamp() - began), applied);
        }

        private void ApplyReceived(Judgement item, JevAnswer answer)
        {
            if (item.Done) return;
            if (item.Who == null) { Finish(item); return; }
            if (item.Superseded) { Finish(item); Metrics.Stale++; return; }
            if (item.Trigger == Trigger.Route) ReceiveRoute(item, answer);
            else if (item.Everyday) ReceiveRoutine(item, answer);
            else ApplyUrgent(item, answer, Time.realtimeSinceStartup);
        }

        /// <summary>JEV did not answer this question (timeout, refusal, budget): ask again a moment later, with growing pauses.</summary>
        private void Failed(Judgement item)
        {
            Metrics.Failures++;
            item.Failures++;
            item.NotBefore = Time.realtimeSinceStartup + Mathf.Min(8f, 1f * (1 << Mathf.Min(item.Failures - 1, 3)));
            // 곧 다시 물을 것(습관)은 몇 번 실패하면 접는다. 관측에 대한 판단과 일상 계획은 답이 올 때까지 묻는다.
            if (item.Trigger == Trigger.Route && item.Failures >= 3) { Finish(item); Metrics.Dropped++; }
        }
    }
}
