using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// The real-time composition loop. The candidates (every cause or development the live world makes possible now, see
    /// Origins/Developments) are listed on a heartbeat and again the moment the situation changes; JEV rates how imminent
    /// each one is (a Score per candidate, all in one request over a focused state); the game turns the levels into hazard
    /// rates and draws with competing risks over the game time that has passed. A rating is reused until the candidate
    /// changes, the situation the state describes changes, or it grows old, so JEV is asked when there is something new to
    /// judge rather than on a timer. Nothing here uses <c>world.Random</c>, and nothing runs without JEV.
    /// </summary>
    public sealed partial class IncidentDirector
    {
        /// <summary>Environment variable a calibration run sets to scale every hazard rate (see plan.md, 실시간 판단 디렉터): it records hazard trajectories with almost no events. Unset = 1.</summary>
        public const string RateScaleVariable = "CHOOGUARD_RATE_SCALE";

        private float rateScale = 1f;

        /// <summary>The first seconds of a shift are quiet (the KTX from Seoul comes in and people get off): no hazard is drawn against.</summary>
        private const float QuietSeconds = 30f;
        /// <summary>Candidates are listed and rated this long before the quiet ends, so the first draw has judgments to use.</summary>
        private const float WarmUpSeconds = 4f;
        private const float HeartbeatSeconds = 1f;
        /// <summary>How often the cheap situation signature (hazards, train stage, escalators, staff response) is compared.</summary>
        private const float WatchSeconds = .25f;
        /// <summary>
        /// A rating is asked for again after this long even if nothing changed. The levels depend on the situation the state
        /// describes, which has its own trigger, and on slowly moving things (the clock, the crowd), so a dozen seconds keeps
        /// them current while a dozen candidates a second are not re-asked: with about fifty candidates of ~170 tokens each,
        /// the director spends a few tenths of a dollar per hour of play (measured in the shift record).
        /// </summary>
        private const float JudgmentLife = 12f;
        /// <summary>A rating whose description drifted (a walking person's place changed) is renewed at most this often.</summary>
        private const float RejudgeSeconds = 4f;
        /// <summary>A rating older than this (JEV silent or over budget) stops counting toward the hazard.</summary>
        private const float MaxJudgmentAge = 30f;
        /// <summary>The longest game time one draw covers: time nobody judged is not made up later.</summary>
        private const float MaxStep = 5f;
        private const int MaxQuestions = 100;

        private sealed class Candidate
        {
            public string Key, Description;
            public Transition Transition;
            public ImminenceScale Scale;
            /// <summary>JEV's probability per imminence level for <see cref="Description"/>; null until rated.</summary>
            public float[] Levels;
            /// <summary>Events per second, from <see cref="Levels"/> and the scale's table.</summary>
            public float Rate;
            public float JudgedAt = -1;
            public int JudgedState;
            /// <summary>The description changed since the rating (a walking person's place): the rating still counts until the new one arrives.</summary>
            public bool Stale;
            public bool Seen;

            public bool Rated => JudgedAt >= 0;

            /// <summary>The candidate became a different kind of thing (its scale changed): the old rating means nothing.</summary>
            public void Forget()
            {
                Levels = null;
                Rate = 0;
                JudgedAt = -1;
                Stale = false;
            }
        }

        private readonly Dictionary<string, Candidate> candidates = new Dictionary<string, Candidate>();
        private readonly List<Candidate> gone = new List<Candidate>();
        private readonly List<float> rates = new List<float>();
        private readonly List<Candidate> drawn = new List<Candidate>();
        private readonly Stopwatch stopwatch = new Stopwatch();
        private System.Random drawRandom;
        private float nextBeat, nextWatch, integratedAt, lastEmergencyAt = -1;
        private int watchHash, stateHash, listedOrigins;
        private bool judging, applying;

        private void BeginCompose()
        {
            // 사건 추첨은 세계의 난수와 따로 굴린다: 판단이 몇 번 오갔는지가 승객·열차의 난수 흐름을 바꾸지 않는다.
            drawRandom = new System.Random(world.Seed ^ 0x0d1ce5);
            if (float.TryParse(System.Environment.GetEnvironmentVariable(RateScaleVariable), NumberStyles.Float, CultureInfo.InvariantCulture, out float scale) && scale >= 0) rateScale = scale;
            log.Director.RateScale = rateScale;
        }

        private void Compose()
        {
            // 비상상황은 JEV 만 만든다. JEV 가 없으면 후보를 만들지도 않는다.
            if (jev == null || !jev.Available || session.Player.IsPaused) return;
            float now = session.ShiftSeconds;
            if (now < QuietSeconds - WarmUpSeconds) return;
            bool beat = now >= nextBeat;
            if (now >= nextWatch)
            {
                nextWatch = now + WatchSeconds;
                int hash = SituationHash();
                if (hash != watchHash) { watchHash = hash; beat = true; }
            }
            if (beat) Beat(now);
        }

        /// <summary>
        /// Something that changes what could happen or how likely it is: a hazard's state, the train stage, a late boarder at
        /// a closing door, escalators stopping or starting, what the staff member or an agency has done. Compared on a few
        /// hundred microseconds' worth of hashing, so a change is answered within a quarter of a second.
        /// </summary>
        private int SituationHash()
        {
            unchecked
            {
                int h = 17;
                foreach (var hazard in HazardRegistry.Active) h = ((h * 31 + hazard.Id.GetHashCode()) * 31 + hazard.State.GetHashCode()) * 31 + hazard.Visible.GetHashCode();
                if (Train != null)
                {
                    h = (h * 31 + (int)Train.Stage) * 31 + Train.Holds.Count;
                    if (Train.Stage == TrainService.Phase.Closing) h = h * 31 + (AnyLateBoarder() ? 1 : 0);
                }
                int running = 0;
                foreach (var escalator in world.Escalators) if (escalator.Running) running++;
                h = h * 31 + running;
                return h * 31 + reported.Count + 7 * calledBy.Count + 13 * arrived.Count + 3 * known.Count + (announced ? 1 : 0) + (alarm ? 2 : 0) + (handedOver ? 4 : 0);
            }
        }

        private bool AnyLateBoarder()
        {
            foreach (var car in Train.Cars) if (LateBoarder(car) != null) return true;
            return false;
        }

        // ── 후보 ────────────────────────────────────────────────────────────

        /// <summary>What could happen next: a new emergency while none is going on; otherwise developments of those that are, and separate new ones.</summary>
        private List<Transition> Enumerate()
        {
            if (Stage == Phase.Calm) return Origins();
            var list = Developments();
            foreach (var origin in Origins())
            {
                // 이미 있는 종류는 다시 만들지 않고, 지진은 진행 중인 사건 도중에 겹치지 않는다.
                if (origin.Kind == "quake" || composedKinds.Contains(origin.Kind)) continue;
                origin.Description = "Separately from the emergency already in progress, and unrelated to it: " + origin.Description;
                list.Add(origin);
            }
            return list;
        }

        /// <summary>Brings the candidate set in line with the fresh list: new ones appear, ones whose text changed are marked for a new rating, ones no longer possible go.</summary>
        private void Reconcile(List<Transition> list)
        {
            foreach (var candidate in candidates.Values) candidate.Seen = false;
            foreach (var transition in list)
            {
                var scale = !transition.Origin ? ImminenceScale.Development : Stage == Phase.Calm ? ImminenceScale.CalmOrigin : ImminenceScale.IncidentOrigin;
                if (!candidates.TryGetValue(transition.Key, out var candidate)) candidates[transition.Key] = candidate = new Candidate { Key = transition.Key };
                else if (candidate.Scale != scale) candidate.Forget();
                else if (candidate.Description != transition.Description) candidate.Stale = true;
                candidate.Transition = transition;
                candidate.Description = transition.Description;
                candidate.Scale = scale;
                candidate.Seen = true;
            }
            gone.Clear();
            listedOrigins = 0;
            foreach (var candidate in candidates.Values)
            {
                if (!candidate.Seen) gone.Add(candidate);
                else if (candidate.Scale != ImminenceScale.Development) listedOrigins++;
            }
            foreach (var candidate in gone) candidates.Remove(candidate.Key);
        }

        // ── 판단 ────────────────────────────────────────────────────────────

        private void Beat(float now)
        {
            stopwatch.Restart();
            nextBeat = now + HeartbeatSeconds;
            Reconcile(Enumerate());
            var focus = Focus(now, out stateHash);
            float enumerationMs = (float)stopwatch.Elapsed.TotalMilliseconds;
            float requestMs = -1;
            if (!judging && !applying)
            {
                stopwatch.Restart();
                if (SendDue(now, focus)) requestMs = (float)stopwatch.Elapsed.TotalMilliseconds;
            }
            log.Director.Beat(enumerationMs, requestMs);
            Integrate(now);
        }

        /// <summary>The rating needs asking for: none yet, made under another situation, the description drifted (not too often) or it is older than <paramref name="life"/>.</summary>
        private bool Due(Candidate candidate, float now, float life) =>
            !candidate.Rated || candidate.JudgedState != stateHash || now - candidate.JudgedAt >= life || candidate.Stale && now - candidate.JudgedAt >= RejudgeSeconds;

        /// <summary>Asks JEV about every candidate that is new, changed, judged under another situation or old; false when nothing needed asking or JEV cannot be asked now.</summary>
        private bool SendDue(float now, Dictionary<string, object> focus)
        {
            bool needed = false;
            foreach (var candidate in candidates.Values)
                if (Due(candidate, now, JudgmentLife)) { needed = true; break; }
            if (!needed || !jev.CanSend(JevLane.Director)) return false;
            // 하나라도 물어야 하면 곧 낡을 것도 함께 묻는다(요청 수를 줄인다). 오래 답을 못 받은 것부터.
            var batch = new List<Candidate>();
            foreach (var candidate in candidates.Values)
                if (Due(candidate, now, JudgmentLife * .5f)) batch.Add(candidate);
            batch.Sort((a, b) => a.JudgedAt.CompareTo(b.JudgedAt));
            if (batch.Count > MaxQuestions) batch.RemoveRange(MaxQuestions, batch.Count - MaxQuestions);
            var questions = new List<JevChoice>(batch.Count);
            var descriptions = new List<string>(batch.Count);
            foreach (var candidate in batch)
            {
                questions.Add(new JevChoice { Id = candidate.Key, Instructions = Imminence.Instructions(candidate.Description), Levels = Imminence.Levels(candidate.Scale) });
                descriptions.Add(candidate.Description);
            }
            int askedState = stateHash;
            judging = true;
            StartCoroutine(jev.Ask("judge", focus, questions, answers => Judged(answers, batch, descriptions, askedState, now), JevLane.Director));
            return true;
        }

        private void Judged(Dictionary<string, JevAnswer> answers, List<Candidate> batch, List<string> descriptions, int askedState, float askedAt)
        {
            judging = false;
            if (Stage == Phase.Ended) return;
            if (answers == null) { log.Director.Round(false); NoticeSilence(); return; }
            log.Director.Round(true);
            for (int i = 0; i < batch.Count; i++)
            {
                var candidate = batch[i];
                if (!answers.TryGetValue(candidate.Key, out var answer)) continue;
                // 답이 오는 사이 그 후보가 사라졌으면 그 답은 버린다. 이야기가 조금 달라졌으면(사람이 걸어 자리가 바뀜) 답은 받고 다시 묻는다.
                if (!candidates.TryGetValue(candidate.Key, out var live) || live != candidate) continue;
                candidate.Levels = answer.LevelProbabilities(Imminence.LevelCount);
                candidate.Rate = Imminence.Rate(candidate.Scale, candidate.Levels);
                candidate.JudgedAt = askedAt;
                candidate.JudgedState = askedState;
                candidate.Stale = candidate.Description != descriptions[i];
                log.Director.Rated(candidate.Scale, candidate.Levels);
            }
            // 판단하는 사이 상황이 또 바뀌었으면 바로 다시 판단한다.
            if (askedState != stateHash) nextBeat = 0;
            Integrate(session.ShiftSeconds);
        }

        private void NoticeSilence()
        {
            if (jev.FailuresInARow < 3 || session.ShiftSeconds < jevNoticeAt) return;
            jevNoticeAt = session.ShiftSeconds + 90;
            session.Hud.Toast(jev.Rejected ? "JEV 가 키를 거부했습니다 · 비상상황이 더 만들어지지 않습니다" : "JEV 응답이 없어 상황 전개를 기다리는 중입니다", 5f);
        }

        // ── 추첨 ────────────────────────────────────────────────────────────

        /// <summary>
        /// Draws against the hazard over the game time since the last step: every rated candidate has its rate for that
        /// stretch, something happens with probability 1 − exp(−Σrate · time) and which one follows the rates. The first
        /// <see cref="QuietSeconds"/> of the shift carry no hazard at all.
        /// </summary>
        private void Integrate(float now)
        {
            float dt = Mathf.Clamp(now - Mathf.Max(integratedAt, QuietSeconds), 0, MaxStep);
            integratedAt = now;
            rates.Clear();
            drawn.Clear();
            float origin = 0, development = 0, share = Imminence.OriginShare(listedOrigins);
            int first = -1, second = -1, third = -1;
            if (now >= QuietSeconds)
                foreach (var candidate in candidates.Values)
                {
                    if (!candidate.Rated || now - candidate.JudgedAt > MaxJudgmentAge) continue;
                    bool isOrigin = candidate.Scale != ImminenceScale.Development;
                    float rate = candidate.Rate * rateScale * (isOrigin ? share : 1f);
                    if (rate <= 0) continue;
                    rates.Add(rate);
                    drawn.Add(candidate);
                    if (isOrigin) origin += rate; else development += rate;
                    int index = rates.Count - 1;
                    if (first < 0 || rate > rates[first]) { third = second; second = first; first = index; }
                    else if (second < 0 || rate > rates[second]) { third = second; second = index; }
                    else if (third < 0 || rate > rates[third]) third = index;
                }
            if (dt > 0 || now < QuietSeconds) log.Director.Trace(dt, origin, development, candidates.Count, rates.Count, Terms(first, second, third));
            if (applying || dt <= 0) return;
            int pick = CompetingRisks.Draw(rates, dt, drawRandom);
            if (pick >= 0) Happen(drawn[pick], rates[pick]);
        }

        /// <summary>The highest rates right now as key:events per second, for the record.</summary>
        private string Terms(params int[] indices)
        {
            var text = new System.Text.StringBuilder();
            foreach (int index in indices)
                if (index >= 0) text.Append(text.Length > 0 ? " " : "").Append(drawn[index].Key).Append(':').Append(rates[index].ToString("0.#####"));
            return text.ToString();
        }

        /// <summary>The draw picked <paramref name="chosen"/>: check it is still possible, ask how strongly it plays out, and make it happen.</summary>
        private void Happen(Candidate chosen, float rate)
        {
            string detail = "JEV 수준 확률 " + string.Join("/", System.Array.ConvertAll(chosen.Levels, p => p.ToString("0.00"))) + " · 초당 " + rate.ToString("0.#####") + " / 후보 " + candidates.Count + "개";
            var fresh = Fresh(chosen.Key);
            if (fresh == null) { log.Director.Vanish(); return; }
            if (fresh.Levels == null) { Execute(fresh, .5f, candidates.Count, detail); return; }
            applying = true;
            var question = new JevChoice
            {
                Id = "magnitude",
                Instructions = "This is now happening at Busan Station: " + fresh.Description + " How does it play out? Give each level the probability that it plays out that way for this person and place; the game draws one from your probabilities.",
                Levels = fresh.Levels,
            };
            var focus = Focus(session.ShiftSeconds, out _);
            StartCoroutine(jev.Ask("magnitude", focus, new[] { question }, answers =>
            {
                applying = false;
                if (Stage == Phase.Ended) return;
                // 크기를 JEV 가 답하지 않으면 이 전이는 일어나지 않는다.
                if (answers == null || !answers.TryGetValue("magnitude", out var answer)) { log.Director.Round(false); NoticeSilence(); return; }
                // 답이 오는 사이 세계가 바뀌었을 수 있다: 그 사람·물건이 아직 후보일 때만 일어난다.
                var again = Fresh(chosen.Key);
                if (again == null) { log.Director.Vanish(); return; }
                int levels = question.Levels.Count, level = answer.DrawLevel(drawRandom, levels);
                float magnitude = levels > 1 ? level / (float)(levels - 1) : .5f;
                Execute(again, magnitude, candidates.Count, detail + " · 크기 " + (level + 1) + "/" + levels);
            }, JevLane.Director));
        }

        /// <summary>
        /// The transition for <paramref name="key"/> as the world makes it now, or null when it is no longer a candidate: a key
        /// names one concrete person, thing or place, so the subject is the same and still qualifies whenever the key is listed.
        /// </summary>
        private Transition Fresh(string key)
        {
            foreach (var transition in Enumerate())
                if (transition.Key == key) return transition;
            return null;
        }

        // ── JEV 에 보이는 상황 ──────────────────────────────────────────────

        /// <summary>
        /// The state JEV rates candidates against: only what bears on the judgment, in words rather than numbers. The hash
        /// covers everything except the clock, so it changes exactly when a rating made earlier may no longer hold.
        /// </summary>
        private Dictionary<string, object> Focus(float now, out int hash)
        {
            var emergencies = new List<object>();
            int uncontrolled = 0;
            foreach (var hazard in HazardRegistry.Active)
            {
                if (!hazard.UnderControl) uncontrolled++;
                if (emergencies.Count < 6) emergencies.Add(new { what = hazard.Label, where = hazard.Where, condition = hazard.State, seen = hazard.Visible });
            }
            var called = new List<string>();
            foreach (var pair in calledBy) if (!arrived.Contains(pair.Key)) called.Add(Responder.AgencyName(pair.Key));
            var onScene = new List<string>();
            foreach (var responder in responders) if (responder.Lead && responder.OnScene) onScene.Add(Responder.AgencyName(responder.Agency));
            bool cordoned = false;
            foreach (var hazard in all) if (hazard.Cordoned) cordoned = true;
            var focus = new Dictionary<string, object>
            {
                ["place"] = "KORAIL Busan Station (terminus of the Gyeongbu line), weekday afternoon",
                ["train"] = TrainWords(),
                ["emergencies_in_progress"] = emergencies,
                ["load"] = uncontrolled == 0 ? "the staff member is not handling any emergency" : uncontrolled == 1 ? "one emergency is in progress" : "more than one emergency is in progress at the same time",
                ["staff_response"] = new
                {
                    staff_member_knows_of_an_emergency = PlayerKnowsIncident, station_office_informed = reported.Count > 0, station_announcement_made = announced,
                    area_cordoned_off = cordoned, fire_alarm_ringing = alarm, train_held_at_platform = holdRequested,
                    agencies_on_the_way = called, agencies_on_scene = onScene,
                },
                ["last_new_emergency"] = lastEmergencyAt < 0 ? "no emergency has happened yet this shift" : now - lastEmergencyAt < 60 ? "a new emergency began moments ago" : now - lastEmergencyAt < 300 ? "a new emergency began a little while ago" : "the last new emergency began a long while ago",
            };
            hash = JsonConvert.SerializeObject(focus).GetHashCode();
            focus["clock"] = session.Clock(now);
            return focus;
        }

        private string TrainWords()
        {
            if (Train == null) return "no train service";
            switch (Train.Stage)
            {
                case TrainService.Phase.Away: return Train.TrackHolds.Count > 0 ? "the train from Seoul is held short of platform 5·6" : "no train at platform 5·6 right now";
                case TrainService.Phase.Arriving: return "the train from Seoul is pulling into platform 5·6";
                case TrainService.Phase.Opening:
                case TrainService.Phase.Alighting: return "the train from Seoul stands at platform 5·6 and passengers are getting off";
                case TrainService.Phase.Turnaround: return "the train stands at platform 5·6 being prepared for the run back to Seoul";
                case TrainService.Phase.Boarding: return "passengers are boarding the train to Seoul at platform 5·6";
                case TrainService.Phase.Closing: return Train.Holds.Count > 0 ? "the departure of the train to Seoul is held at platform 5·6" : "the doors of the train to Seoul at platform 5·6 are about to close";
                default: return "the train to Seoul is leaving platform 5·6";
            }
        }
    }
}
