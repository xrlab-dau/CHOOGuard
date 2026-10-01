using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    // 일상 계획: JEV 가 한 사람의 여정에 대해 내린 판단(어떤 일을 어떤 비율로 하려는가)을 받아 두고, 그 사람이 활동이 끝날 때마다 지역에서 다음 걸음을 꺼내 쓴다.
    // 활동이 끝났다고 다시 묻지 않는다. 다시 묻는 것은 계획이 전제한 것이 달라졌을 때(열차·표·만날 사람·알게 된 사건)와 더 고를 것이 남지 않았을 때뿐이고, 달라진 그 사람에게만 묻는다.
    public sealed partial class CrowdMind
    {
        /// <summary>A trip with a purpose shows its train this far ahead of departure: the platform option exists from here on.</summary>
        private const float EarlySeconds = 420f;
        /// <summary>Standing places are offered zone first: this many zones per question.</summary>
        private const int LivingZonesPerQuestion = 3;
        /// <summary>A plan is spent when what is left of it (visits wear down) is under this share of what JEV gave it.</summary>
        private const float SpentShare = .25f;
        /// <summary>What carrying out a shop or cafe visit does to the wish for it (the next pick is less likely the same one).</summary>
        private const float VisitFade = .4f;

        /// <summary>What a plan was made on. The plan holds while both are as they were; otherwise the person is asked again.</summary>
        internal readonly struct Basis
        {
            /// <summary>Where the trip stands (purpose, ticket, missed train, boarding open, partner), as flags.</summary>
            public readonly int Trip;
            /// <summary>Which incidents they know of and whether they were told to leave (order independent).</summary>
            public readonly int Known;

            public Basis(int trip, int known)
            {
                Trip = trip;
                Known = known;
            }
        }

        /// <summary>
        /// What JEV decided about one person's course through the station: its probability for each option it was offered, the
        /// options with the places they were bound to, and what they were made on. Carrying out a step draws from the options that
        /// still fit; nothing is asked of JEV for that.
        /// </summary>
        internal sealed class Plan
        {
            public readonly Basis Basis;
            public readonly List<Choice> Choices;
            public readonly Dictionary<string, float> Odds;
            /// <summary>Keys already carried out and worn thin (a visit loses appeal each time).</summary>
            public readonly Dictionary<string, float> Worn = new Dictionary<string, float>();
            /// <summary>Keys not to choose again: done once, or the way to them was blocked.</summary>
            public readonly HashSet<string> Struck = new HashSet<string>();
            /// <summary>The option being carried out now, so a blocked way strikes the right one.</summary>
            public string Active;
            public int Steps;

            public Plan(Basis basis, List<Choice> choices, Dictionary<string, float> odds)
            {
                Basis = basis;
                Choices = choices;
                Odds = odds;
            }
        }

        /// <summary>What JEV last answered to "they finished what they were doing" while what the person observes stayed the same.</summary>
        internal sealed class Stance
        {
            public Hazard Hazard;
            public int Version;
            public string Signature;
            public JevAnswer Answer;
            public HashSet<string> Offered;
        }

        private readonly List<(Choice choice, float weight)> stepPool = new List<(Choice, float)>();

        private bool Boarding(Passenger p)
        {
            var train = crowd.World.Train;
            return train != null && (train.BoardingOpen || train.Stage == TrainService.Phase.Closing) && train.Service == p.Service;
        }

        /// <summary>Where the person's trip stands, as flags: every fact that changes which options exist or what they want.</summary>
        private int TripOf(Passenger p)
        {
            int trip = (int)p.Trip;
            if (p.MissedTrain) trip |= 1 << 2;
            if (p.HasTicket) trip |= 1 << 3;
            if (p.Trip == Passenger.Purpose.Depart && !p.MissedTrain && SecondsToTrain(p) <= 300f) trip |= 1 << 9;
            switch (p.Trip)
            {
                case Passenger.Purpose.Depart:
                    if (Boarding(p)) trip |= 1 << 4;
                    else if (!p.MissedTrain && SecondsToTrain(p) < EarlySeconds) trip |= 1 << 5;
                    break;
                case Passenger.Purpose.Arrive:
                    if (p.Partner != null && !p.Met && p.Partner.Current == Passenger.Activity.Meet) trip |= 1 << 6;
                    if (p.Met) trip |= 1 << 7;
                    break;
                case Passenger.Purpose.Greet:
                    if (p.Partner != null) trip |= 1 << 6;
                    if (p.Met) trip |= 1 << 7;
                    if (p.Memory.Count > 4) trip |= 1 << 8;
                    break;
            }
            return trip;
        }

        private static int KnownOf(Passenger p)
        {
            int known = 0;
            unchecked
            {
                foreach (var hazard in p.Noticed) if (hazard.Active) known += (hazard.Id ?? "").GetHashCode();
                if (p.Instructed) known ^= 0x2545F491;
            }
            return known;
        }

        private Basis BasisOf(Passenger p) => new Basis(TripOf(p), KnownOf(p));

        /// <summary>The person has a plan and nothing it was made on has changed.</summary>
        private bool Holds(Passenger who)
        {
            var plan = who.Slot.Plan;
            return plan != null && plan.Basis.Trip == TripOf(who) && plan.Basis.Known == KnownOf(who);
        }

        /// <summary>Why the person has no usable plan, for the count of why people are asked again.</summary>
        private string WhyAsk(Passenger who)
        {
            var plan = who.Slot.Plan;
            if (plan == null) return "missing";
            if (plan.Basis.Trip != TripOf(who)) return "trip";
            if (plan.Basis.Known != KnownOf(who)) return "knowledge";
            return "exhausted";
        }

        /// <summary>Asks JEV for this person's plan (a low-priority everyday question; it moves to the emergency lane only if they stand waiting for it).</summary>
        private Judgement Ask(Passenger who, string why, bool first = false)
        {
            var item = NewEveryday(who, Trigger.Routine, 0, "r");
            item.First = first;
            who.Slot.Routine = item;
            Metrics.PlanQuestion(why);
            who.Slot.Log("plan asked: " + why);
            return item;
        }

        /// <summary>
        /// Someone became part of the station (shift start, walked in from the city, stepped off the train): their plan and the way they
        /// usually change floors are asked once. Arrivals keep walking the first step of their trip until the plan comes.
        /// </summary>
        public void Bootstrap(Passenger who)
        {
            if (!Usable) return;
            var slot = who.Slot;
            if (slot.Plan == null && (slot.Routine == null || slot.Routine.Done)) Ask(who, "bootstrap", who.Current == Passenger.Activity.Walk);
            if (!slot.RouteAsked) { slot.RouteAsked = true; slot.Route = NewEveryday(who, Trigger.Route, 1e4f, "route"); }
        }

        /// <summary>
        /// Twice a second: counts how long people wait for a decision, and asks again for the plan of anyone whose trip changed under
        /// it (the train opened for boarding or left, the ticket was bought, the person they meet is on the way). Knowledge of an
        /// incident is not chased here: the people it concerns are busy reacting, and a plan made on what they knew is replaced when they
        /// next need a step.
        /// </summary>
        private void ScanPlans(float now)
        {
            if (now < nextScan) return;
            nextScan = now + .5f;
            foreach (var person in crowd.People)
            {
                if (person == null) continue;
                var slot = person.Slot;
                // 다음 걸음을 기다리며 하던 일을 잇는 시간(가장 긴 것을 잰다). 다른 일(대피·지켜보기)이 이미 맡았으면 더는 기다리는 것이 아니다.
                if (slot.WaitingSince >= 0)
                {
                    if (person.Holding) Metrics.LongestWait = Mathf.Max(Metrics.LongestWait, now - slot.WaitingSince);
                    else slot.WaitingSince = -1;
                }
                var plan = slot.Plan;
                if (plan == null || person.Hurt || person.Hostile || person.Aboard || person.Leaving || !Passenger.Routine(person.Current)) continue;
                // 마중 나와 도착하는 사람을 기다리는 사람은 만날 때까지 그 자리에 있다(Passenger.UpdateMeet): 계획을 쓸 일이 없다. 상대가 사라지면 그때 다시 본다.
                if (person.Current == Passenger.Activity.Meet && person.Partner != null && !person.Met) continue;
                if (slot.Routine != null && !slot.Routine.Done) continue;
                if (plan.Basis.Trip != TripOf(person)) Ask(person, "trip");
            }
        }

        /// <summary>
        /// The next step of a plan: one option drawn by JEV's probabilities among those that still fit now (their guard holds, their place
        /// is not cut off, they were not done once or struck), whose place can be held. Null when nothing is left, or when what is left
        /// has worn down to a fraction of what they wanted (they did what they came for; JEV judges again with what they have done).
        /// Waiting does not wear, and neither does leaving, so a person who only visits drifts toward the exit as their visits fade.
        /// </summary>
        private Choice StepOf(Plan plan, Passenger who)
        {
            stepPool.Clear();
            float total = 0, wanted = 0;
            foreach (var choice in plan.Choices)
            {
                if (!plan.Odds.TryGetValue(choice.Key, out var odds) || odds <= 0 || plan.Struck.Contains(choice.Key)) continue;
                if (choice.Guard != null && !choice.Guard()) continue;
                if (!Fits(who, choice)) continue;
                float weight = odds * (plan.Worn.TryGetValue(choice.Key, out var worn) ? worn : 1f);
                if (weight <= 0) continue;
                stepPool.Add((choice, weight));
                total += weight;
                wanted += odds;
            }
            if (plan.Steps > 0 && total < wanted * SpentShare) return null;
            while (stepPool.Count > 0)
            {
                float roll = (float)crowd.World.Random.NextDouble() * total;
                int pick = stepPool.Count - 1;
                for (int i = 0; i < stepPool.Count; i++)
                {
                    roll -= stepPool[i].weight;
                    if (roll <= 0) { pick = i; break; }
                }
                var (chosen, picked) = stepPool[pick];
                if (Reserve(chosen, who))
                {
                    plan.Active = chosen.Key;
                    plan.Steps++;
                    if (chosen.Fade <= 0) plan.Struck.Add(chosen.Key);
                    else if (chosen.Fade < 1) plan.Worn[chosen.Key] = (plan.Worn.TryGetValue(chosen.Key, out var before) ? before : 1f) * chosen.Fade;
                    return chosen;
                }
                total -= picked;
                stepPool.RemoveAt(pick);
            }
            return null;
        }

        /// <summary>A fixed goal stays bound to the place JEV judged; a closed or dangerous target is filtered, never silently replaced.</summary>
        private bool Fits(Passenger who, Choice choice)
        {
            if (choice.Resolve != null)
            {
                choice.Place = choice.Resolve();
                return choice.Place != null && !Avoided(who, choice.Place.Position);
            }
            return choice.Place == null || !Avoided(who, choice.Place.Position);
        }
    }
}
