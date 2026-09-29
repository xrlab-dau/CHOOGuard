using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Real-time judgement of the people in the station, asked of JEV as typed Choice questions over options the game builds
    /// from the live world. Something a person observes (they see, hear or smell a hazard, hear the alarm bell or an
    /// announcement, are told by staff, find their way blocked, arrive, or finish what they were doing) raises a judgement at
    /// once; people near an active incident are judged again every few seconds; everyday choices are asked ahead of time at
    /// low priority. A question holds only what that person could know, and people share a request only when their
    /// observations are identical. Requests are scheduled by priority inside the crowd's share of JEV's budget. While an
    /// answer is late, or the budget is spent, the person keeps doing what they were doing; when it arrives it is checked
    /// against their current state first. Only physical reflexes (stepping back from fire, out of smoke) are decided in code,
    /// and local weights decide only in runs without JEV (no key, or TYPESAFE_API_KEY=off); those weights are design
    /// heuristics, not measured behaviour data.
    /// </summary>
    public sealed partial class CrowdMind
    {
        /// <summary>Why a judgement is needed, most urgent first (the value is the priority).</summary>
        public enum Trigger { Quake, AfterQuake, Instruction, Notice, Changed, Blocked, Cue, Ended, Periodic, Routine, Route }

        // 사건 근처 재판단 주기·거리는 CrowdDirector 에서 조정한다(인스펙터).
        private float PeriodicSeconds => crowd.JudgePeriodSeconds;
        private float NearMeters => crowd.JudgeNearMeters;
        /// <summary>Emergency questions per request: one bell or announcement reaches everyone at once, and a request holds about a dozen questions in the time of one.</summary>
        public int UrgentBatch = 12;
        public int RoutineBatch = 12;
        /// <summary>Requests started per frame at most (building a request is not free).</summary>
        public int SendsPerFrame = 3;

        /// <summary>What a person's judgement keeps between requests; owned by the <see cref="Passenger"/>.</summary>
        public sealed class Slot
        {
            internal Judgement Urgent, Routine, Route;
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

            internal void Forget()
            {
                Seen.Clear();
                Heard.Clear();
                Acts.Clear();
                Announcement = null;
                ToldByStaff = false;
            }
        }

        /// <summary>One thing a person could do next, with where and for how long.</summary>
        public sealed class Choice
        {
            public string Key, Description, Remember, Filter;
            public float Weight, Seconds;
            public Passenger.Activity Activity;
            public PointKind Kind;
            public StationPoints.Point Place;
            /// <summary>Still sensible now (the train may have left since JEV was asked); null when nothing can change.</summary>
            public Func<bool> Guard;
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
            public bool Sent, Done, Superseded;
            public float Raised, RaisedReal, Due, NotBefore, ReadyAt, SentReal, AnsweredAt;
            public int Version, Failures, BatchSize;
            public string Key, Answer, Note;
            public Dictionary<string, float> Odds;
            public HashSet<string> Offered;
            public List<Choice> Choices;
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
        public void OnBlocked(Passenger who) => Raise(who, Trigger.Blocked, who.Focus, true);

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
            // 도착·관찰 끝남·주기는 이미 판단이 진행 중이면 그 판단에 맡긴다. 관측이 실제로 바뀐 것만 진행 중 판단을 낡게 만든다.
            bool soft = trigger == Trigger.Ended || trigger == Trigger.Periodic;
            if (soft && pending) return;
            if (!soft) slot.Version++;
            float raised = Time.time, raisedReal = Time.realtimeSinceStartup;
            if (pending)
            {
                if (!old.Sent)
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
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                var item = queue[i];
                if (item.Done || item.Who == null || item.Answer != null) { if (!item.Done && item.Who == null) item.Done = true; queue.RemoveAt(i); continue; }
                if (item.Sent) continue;
                if (!usable)
                {
                    // JEV 없는 근무: 일상 판단은 활동이 끝날 때 지역 규칙이 정하고, 급한 판단은 반응 시간 뒤 지역 규칙이 정한다.
                    if (item.Everyday) { Finish(item); queue.RemoveAt(i); Metrics.Dropped++; }
                    else if (now >= item.ReadyAt) { queue.RemoveAt(i); ResolveLocally(item); }
                    continue;
                }
                if (Outdated(item, now)) { Finish(item); queue.RemoveAt(i); Metrics.Stale++; }
            }
            if (usable)
            {
                ScanNearIncident(now);
                Dispatch(now);
            }
            Metrics.Flush();
            Metrics.Tick((System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000f / System.Diagnostics.Stopwatch.Frequency, GC.CollectionCount(0) != collections, Metrics.Requests - startedBefore, queue.Count);
        }

        /// <summary>A question nobody needs the answer to any more: the next round asks a fresh one.</summary>
        private bool Outdated(Judgement item, float now)
        {
            if (item.Trigger == Trigger.Periodic) return now - item.Raised > PeriodicSeconds * 2f;
            // 일상 판단은 활동이 끝나면 어차피 급한 판단으로 올라간다. 그 전에 오래 묵은 것은 다음 활동에서 다시 묻는다.
            if (item.Everyday && !item.Urgent) return now - item.Due > 90f;
            return false;
        }

        /// <summary>People near an incident they know of are judged again every <see cref="PeriodicSeconds"/>.</summary>
        private void ScanNearIncident(float now)
        {
            if (now < nextScan) return;
            nextScan = now + .5f;
            foreach (var person in crowd.People)
            {
                if (person == null) continue;
                // 다음 걸음을 기다리며 하던 일을 잇는 시간(가장 긴 것을 잰다).
                if (person.Slot.WaitingSince >= 0) Metrics.LongestWait = Mathf.Max(Metrics.LongestWait, now - person.Slot.WaitingSince);
                if (person.Hurt || person.Hostile) continue;
                var focus = person.Focus;
                if (focus == null || !focus.Active) continue;
                switch (person.Current)
                {
                    case Passenger.Activity.Evacuate:
                    case Passenger.Activity.Injured:
                    case Passenger.Activity.OnTrack:
                    case Passenger.Activity.Aggressive:
                    case Passenger.Activity.Report:
                    case Passenger.Activity.TakeCover:
                        continue;
                }
                var slot = person.Slot;
                if (slot.Urgent != null && !slot.Urgent.Done) continue;
                // 역 전체가 겪는 일(정전 등)은 곁에서 벌어지는 일보다 느리게 다시 묻는다.
                float interval = PeriodicSeconds * (focus.Localized ? 1f : 3f);
                if (now - slot.JudgedAt < interval || !Near(person, focus)) continue;
                Raise(person, Trigger.Periodic, focus, true);
            }
        }

        private bool Near(Passenger person, Hazard hazard)
        {
            if (!hazard.Localized) return true;
            var d = person.transform.position - hazard.Position;
            float reach = Mathf.Max(NearMeters, hazard.NoticeRadius);
            return Mathf.Abs(d.y) < 4f && d.x * d.x + d.z * d.z < reach * reach;
        }

        // ── 보내기 ──────────────────────────────────────────────────────────

        private void Dispatch(float now)
        {
            // 접속이 계속 실패하면 다시 물음을 줄여, 서버가 돌아오기 전에 요청이 쌓여 쏟아지지 않게 한다.
            if (jev.FailuresInARow >= 3)
            {
                if (Time.realtimeSinceStartup < nextProbe) return;
                nextProbe = Time.realtimeSinceStartup + 2f;
            }
            for (int sends = 0; sends < SendsPerFrame; sends++)
            {
                ready.Clear();
                float real = Time.realtimeSinceStartup;
                foreach (var item in queue)
                    if (!item.Sent && !item.Done && item.Who != null && item.Answer == null && real >= item.NotBefore) ready.Add(item);
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
                string signature = Signature(first);
                for (int i = 1; i < ready.Count && batch.Count < capacity; i++)
                    if (ready[i].Urgent == first.Urgent && Signature(ready[i]) == signature) batch.Add(ready[i]);
                Send(batch, first.Urgent);
            }
        }

        private static int ByPriority(Judgement a, Judgement b)
        {
            if (a.Urgent != b.Urgent) return a.Urgent ? -1 : 1;
            if (a.Trigger != b.Trigger) return a.Trigger < b.Trigger ? -1 : 1;
            return a.Due.CompareTo(b.Due);
        }

        private static JevLane LaneOf(bool urgent) => urgent ? JevLane.CrowdUrgent : JevLane.CrowdRoutine;

        private bool CanSend(bool urgent) => jev.CanSend(LaneOf(urgent));

        private IEnumerator Request(bool urgent, string purpose, object state, IReadOnlyList<JevChoice> questions, Action<Dictionary<string, JevAnswer>> done) =>
            jev.Ask(purpose, state, questions, done, LaneOf(urgent));

        private void Send(List<Judgement> batch, bool urgent)
        {
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
                asked.Add(item);
                questions.Add(question);
            }
            if (asked.Count == 0) return;
            Metrics.Requests++;
            Metrics.Questions += asked.Count;
            var state = State(asked[0]);
            crowd.StartCoroutine(Request(urgent, urgent ? "crowd-urgent" : "crowd-routine", state, questions, answers => OnAnswers(asked, answers)));
        }

        private void OnAnswers(List<Judgement> asked, Dictionary<string, JevAnswer> answers)
        {
            float real = Time.realtimeSinceStartup;
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            foreach (var item in asked)
            {
                item.Sent = false;
                if (item.Done) continue;
                if (item.Who == null) { Finish(item); continue; }
                JevAnswer answer = null;
                answers?.TryGetValue(item.Key, out answer);
                // 그 사이 같은 사람에게 더 새로운 관측이 생겨 새 판단이 이 판단을 대신한다: 이 답은 쓰지 않는다.
                if (item.Superseded) { Finish(item); if (answer != null) Metrics.Stale++; continue; }
                if (answer == null) { Failed(item); continue; }
                Metrics.Answered++;
                item.AnsweredAt = Time.time;
                if (item.Trigger == Trigger.Route) ReceiveRoute(item, answer);
                else if (item.Everyday) ReceiveRoutine(item, answer);
                else ApplyUrgent(item, answer, real);
            }
            Metrics.Apply((System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000f / System.Diagnostics.Stopwatch.Frequency, asked.Count);
        }

        /// <summary>JEV did not answer this question (timeout, refusal, budget): ask again a moment later, with growing pauses.</summary>
        private void Failed(Judgement item)
        {
            Metrics.Failures++;
            item.Failures++;
            item.NotBefore = Time.realtimeSinceStartup + Mathf.Min(8f, 1f * (1 << Mathf.Min(item.Failures - 1, 3)));
            // 곧 다시 물을 것들(주기·습관)은 몇 번 실패하면 접는다. 관측에 대한 판단은 답이 올 때까지 묻는다.
            if ((item.Trigger == Trigger.Periodic || item.Trigger == Trigger.Route) && item.Failures >= 3) { Finish(item); Metrics.Dropped++; }
        }
    }
}
