using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    // 질문에 담는 것: 그 사람이 알 수 있는 것만. 공개 상태(IncidentDirector.PublicState)는 다른 곳의 불·역무원 보고·기관 도착까지
    // 담고 있어 승객에게 보내지 않는다. 공유하는 state 는 같은 관측을 한 사람들에게만 공통이고, 사람마다 다른 것은 질문 문장에 둔다.
    public sealed partial class CrowdMind
    {
        private const string Place = "KORAIL Busan Station (terminus of the Gyeongbu line): 2F concourse over the tracks, 3F shops and restaurants, 1F, station square, platforms 1–11; weekday afternoon";

        private readonly List<Hazard> scratch = new List<Hazard>();

        /// <summary>The hazards this person perceived themselves and has not lost track of, in a fixed order.</summary>
        private List<Hazard> Perceived(Passenger who)
        {
            scratch.Clear();
            foreach (var hazard in who.Noticed)
                if (hazard.Active && who.Senses(hazard) && who.Slot.Seen.ContainsKey(hazard)) scratch.Add(hazard);
            scratch.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return scratch;
        }

        /// <summary>
        /// What the person knows, as a key: two people whose keys are equal have observed the same things (the same hazards as
        /// they last saw them, the same bell or announcement, the same instruction), so one request may carry both.
        /// </summary>
        private string Signature(Passenger who)
        {
            var slot = who.Slot;
            var key = new StringBuilder(96);
            foreach (var hazard in Perceived(who)) key.Append(hazard.Id).Append('=').Append(slot.Seen[hazard]).Append(';');
            if (HasKnowledge(who))
            {
                var heard = new List<string>(slot.Heard);
                heard.Sort(string.CompareOrdinal);
                foreach (var cue in heard) key.Append('~').Append(cue);
                key.Append('|').Append(slot.Announcement).Append('|').Append(slot.ToldByStaff);
            }
            return key.ToString();
        }

        /// <summary>Knows of an incident at all: perceived it, heard a cue about it, or was told to leave. Anything heard is forgotten once nothing they know of is still going on.</summary>
        private bool HasKnowledge(Passenger who)
        {
            foreach (var hazard in who.Noticed) if (hazard.Active) return true;
            return false;
        }

        private object State(Judgement item)
        {
            var who = item.Who;
            var slot = who.Slot;
            var session = crowd.Session;
            var train = crowd.World.Train;
            var state = new Dictionary<string, object>
            {
                ["place"] = Place,
                ["clock"] = session.Clock(session.ShiftSeconds),
                ["train_board"] = train != null ? train.Status() : "none",
            };
            if (!HasKnowledge(who)) return state;
            var perceiving = new List<object>();
            foreach (var hazard in Perceived(who))
                perceiving.Add(new { what = slot.Seen[hazard], where = hazard.Where, how = hazard.NeedsSight ? "sees" : "hears, smells or feels" });
            if (perceiving.Count > 0) state["perceiving"] = perceiving;
            if (slot.Heard.Count > 0) state["heard"] = slot.Heard.ToArray();
            if (slot.Announcement != null) state["announcement"] = slot.Announcement;
            if (slot.ToldByStaff) state["a_station_staff_member_told_them_directly"] = "to leave through an exit";
            return state;
        }

        private static string Profile(Passenger p)
        {
            var text = new StringBuilder(160);
            text.Append("Passenger #").Append(p.Number).Append(" (").Append(p.Body.Female ? "woman" : "man");
            if (p.Elderly) text.Append(", elderly");
            text.Append(p.Luggage == 2 ? ", with a large suitcase" : p.Luggage == 1 ? ", with a bag" : "").Append(")");
            return text.ToString();
        }

        private JevChoice Question(Judgement item)
        {
            if (item.Trigger == Trigger.Route) return RouteQuestion(item);
            if (item.Everyday) return RoutineQuestion(item);
            var options = Options(item);
            var question = new JevChoice { Id = item.Key, Instructions = Situation(item) };
            item.Offered = new HashSet<string>();
            foreach (var (option, weight) in options)
            {
                if (weight <= 0) continue;
                question.Criteria[option.Key] = option.Description;
                item.Offered.Add(option.Key);
            }
            return question.Criteria.Count == 0 ? null : question;
        }

        /// <summary>What this person, and only this person, has just observed.</summary>
        private string Situation(Judgement item)
        {
            var p = item.Who;
            var h = item.Hazard;
            var slot = p.Slot;
            var text = new StringBuilder(560);
            text.Append(Profile(p)).Append(", currently ").Append(p.Doing).Append(". ");
            string seen = h != null && slot.Seen.TryGetValue(h, out var last) ? last : h?.Visible;
            string metres = h != null && h.Localized ? Mathf.RoundToInt(Vector3.Distance(p.transform.position, h.Position)) + " m away, at " + h.Where : null;
            switch (item.Trigger)
            {
                case Trigger.Notice:
                    if (metres == null) text.Append("Around them now: '").Append(seen).Append("'. ");
                    else text.Append(h.NeedsSight ? "They now see: '" : "They now hear or smell: '").Append(seen).Append("' about ").Append(metres).Append(". ");
                    break;
                case Trigger.Changed:
                    text.Append("What they are looking at has just changed: now '").Append(seen).Append("' (a moment ago: '").Append(item.Note).Append("')");
                    text.Append(metres != null ? ", about " + metres : "").Append(". ");
                    break;
                case Trigger.Cue:
                    text.Append("They cannot see the cause but ").Append(p.Cue ?? "people nearby are reacting").Append(" (").Append(crowd.CountNear(p.transform.position, 10, Passenger.Activity.Evacuate)).Append(" leaving within 10 m). ");
                    break;
                case Trigger.Instruction:
                    text.Append(item.Direct ? "A station staff member is telling them directly to leave through an exit. " : "A public announcement asks everyone to leave the area. ");
                    break;
                case Trigger.Ended:
                    text.Append(p.Current == Passenger.Activity.Report ? "They have been walking to tell the station staff member for a while and have not reached them yet. " :
                        p.Current == Passenger.Activity.Watch ? "They have watched from where they stand for a while. " : "They have reached the place they were moving to. ");
                    if (seen != null && p.Senses(h)) text.Append("What they last saw: '").Append(seen).Append("'. ");
                    break;
                case Trigger.Blocked:
                    text.Append("The way to where they were heading is blocked (a cordon, fire or debris across the path). ");
                    break;
                case Trigger.Quake:
                    text.Append("The whole station starts shaking strongly. ");
                    break;
                case Trigger.AfterQuake:
                    text.Append("The shaking has just stopped. ").Append(crowd.World.IsClosed(p.transform.position, 8) ? "Something fell from the ceiling close to them. " : "");
                    break;
            }
            if (h != null && h.Irritates(p.transform.position)) text.Append("They are coughing in the smoke or fumes. ");
            if (slot.Acts.Count > 0) text.Append("Since they noticed, they have already: ").Append(string.Join("; ", slot.Acts)).Append(". ");
            text.Append("People within 10 m: ").Append(crowd.CountNear(p.transform.position, 10, null)).Append(", of them leaving: ").Append(crowd.CountNear(p.transform.position, 10, Passenger.Activity.Evacuate)).Append(". ");
            text.Append(StaffNote(p));
            text.Append(p.Instructed ? "They were already told to leave. " : "");
            text.Append("Choose what this person does next, as an ordinary member of the public would.");
            return text.ToString();
        }

        /// <summary>A person sees the uniformed staff member only when close and on the same floor.</summary>
        private string StaffNote(Passenger p)
        {
            var player = crowd.Player;
            if (player == null) return "";
            var d = player.transform.position - p.transform.position;
            if (Mathf.Abs(d.y) > 3f || d.sqrMagnitude > 20f * 20f) return "";
            return "A uniformed station staff member is about " + Mathf.RoundToInt(d.magnitude) + " m from them. ";
        }
    }
}
