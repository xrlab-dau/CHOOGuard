using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Hud;
using ChooGuard.App.Fps.Shell;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// The radio, the notebook and looking closely in 표준 and 실전 (<see cref="GameSettings.Guidance"/>). The radio always offers the same kinds of message —
    /// reports of what the staff member perceived, requests for teams and station measures, announcements, answers — whether or not they fit the moment. A
    /// report is assembled from slots the notebook fills (where, what, how it stands, how many hurt); a missing slot makes the office ask back. The office
    /// only acknowledges and says what it did itself; it sends a team only when asked, for the one thing the request named, and the team goes nowhere else.
    /// Looking closely shows what a person standing there sees and writes it into the notebook with the time. The guided shift (견학) keeps the radio of the
    /// family files and the Tab board as they were. Nothing here draws random numbers or knows a hazard the staff member has not perceived.
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private static bool Guided => GameSettings.Guided;

        /// <summary>표준·실전: the office calls a team itself only after this long without any answer from the scene (s, at least).</summary>
        private const float OfficeFollowUpSeconds = 90f;
        /// <summary>Seconds the office waits for an answer to a question it asked.</summary>
        private const float QuestionSeconds = 40f;
        private const int NotebookKeep = 60, NotebookShown = 16;
        /// <summary>The staff member has been this close to a hazard (in sight) and can say how many are hurt there.</summary>
        private const float CloseLookMetres = 10f;

        private static readonly string[] TeamRequests = { "119 소방", "119 구급", "112", "철도경찰", "시설 담당", "전기 담당", "도시가스", "승강기 담당" };
        private static readonly string[] MeasureRequests = { "열차 출발 보류", "에스컬레이터 정지", "수신기 복구", "CCTV 확인", "방화셔터 복구" };
        private static readonly PaScope[] BroadcastScopes = { PaScope.ClearAround, PaScope.EvacuateArea, PaScope.EvacuateStation, PaScope.Inform };
        private static readonly string[] Answers = { "예", "아니오", "확인 중" };

        // 수첩: 확인한 사실, 무전 기록, 공개 행동요령 카드(새 것이 끝에).
        private readonly List<string> facts = new List<string>();
        private readonly List<string> radioRecord = new List<string>();
        private readonly List<(string title, string body)> cards = new List<(string, string)>();
        private readonly HashSet<string> cardKeys = new HashSet<string>();
        /// <summary>What the staff member last saw of each hazard: the [상태] slot of a report.</summary>
        private readonly Dictionary<Hazard, string> seen = new Dictionary<Hazard, string>();
        /// <summary>Hazards the staff member has stood close to in sight (they can say how many are hurt there).</summary>
        private readonly HashSet<Hazard> closeLooked = new HashSet<Hazard>();
        /// <summary>Perceived hazards in the order they were perceived.</summary>
        private readonly List<Hazard> knownOrder = new List<Hazard>();
        /// <summary>What a request is for: the hazard last reported, looked at closely or perceived.</summary>
        private Hazard focus, lastReported;
        private Hazard followUpFor;
        private Agency followUpAgency;

        private sealed class Question
        {
            public string Text;
            public Action Yes, No, Unsure;
            public float Until;
        }

        private Question question;

        // ── 역무실 후속 신고 ──

        /// <summary>
        /// The office will call <paramref name="agency"/> for <paramref name="about"/> itself if nobody answers from the scene: after <paramref name="seconds"/> in the guided
        /// shift, after at least <see cref="OfficeFollowUpSeconds"/> otherwise (and then only if the staff member has said nothing on the radio since).
        /// </summary>
        private void ScheduleFollowUp(float seconds, Hazard about, Agency agency)
        {
            officeFollowUp = Time.time + (Guided ? seconds : Mathf.Max(seconds, OfficeFollowUpSeconds));
            followUpSince = Time.time;
            followUpFor = about;
            followUpAgency = agency;
        }

        /// <summary>표준·실전: nobody answered from the scene, so the office calls for the one thing it was told about and says so (the log keeps that it was the office).</summary>
        private void OfficeFollowUpUnanswered()
        {
            var hazard = followUpFor;
            if (hazard == null || hazard.UnderControl || calledBy.ContainsKey(followUpAgency)) return;
            var team = Teams.For(followUpAgency, hazard);
            var name = Teams.Name(team);
            Call(followUpAgency, "역무실 자체 판단(현장 응답 없음)", hazard, team);
            Office("역무실입니다. 현장 확인 응답이 없어 " + name + KoreanText.Object(name) + " 불렀습니다.");
            log.Add("현장 응답 없음 · 역무실이 직접 " + name + " 요청");
        }

        // ── 수첩 ──

        private string Stamp() => "<color=#FFFFFF90>" + session.Clock(session.ShiftSeconds) + "</color> ";

        private void Fact(string text)
        {
            facts.Add(Stamp() + text);
            if (facts.Count > NotebookKeep) facts.RemoveAt(0);
        }

        /// <summary>A hazard became known (<see cref="Know"/>): it goes into the notebook, becomes what requests are for, and opens its public guidance cards.</summary>
        private void Noted(Hazard hazard, string how)
        {
            knownOrder.Add(hazard);
            focus = hazard;
            // 지금 몸으로 겪는 것(보이는 것·냄새·흔들림·정전)이면 본 그대로를 [상태]로 적는다. 전해 들은 것은 상태를 모른다.
            if (PerceivesNow(hazard)) seen[hazard] = hazard.Visible;
            Fact(hazard.Label + " · " + hazard.Where + (seen.TryGetValue(hazard, out var what) ? " — " + what : "") + " <color=#FFFFFF90>(" + how + ")</color>");
            UnlockCards(hazard);
        }

        /// <summary>The staff member perceives <paramref name="hazard"/> with their own senses where they stand: in sight within its range, within the reach of its smell or sound, or everywhere for a station-wide one.</summary>
        private bool PerceivesNow(Hazard hazard)
        {
            if (!hazard.Localized) return true;
            var eye = cameraTransform.position;
            float distance = Vector3.Distance(eye, hazard.Position);
            if (hazard.NeedsSight) return distance <= hazard.StaffSightRange && HazardRegistry.CanSee(eye, hazard);
            return distance < hazard.NoticeRadius;
        }

        /// <summary>Every look around: hazards stood close to in sight let the staff member say how many are hurt there.</summary>
        private void LookCloser(Vector3 eye)
        {
            foreach (var hazard in known)
            {
                if (closeLooked.Contains(hazard) || !hazard.Localized) continue;
                if (Vector3.Distance(eye, hazard.Position) < CloseLookMetres && HazardRegistry.CanSee(eye, hazard)) closeLooked.Add(hazard);
            }
        }

        private void OnRadioPosted(RadioChannel channel, string text)
        {
            radioRecord.Add(Stamp() + "<color=#FFFFFFB0>[" + RadioFeed.ChannelName(channel) + "]</color> " + text);
            if (radioRecord.Count > NotebookKeep) radioRecord.RemoveAt(0);
        }

        // 공개 행동요령 카드: 처음 지각한 상황 속성마다 수첩에 쌓인다(지금 이걸 하라는 지시가 아니라 배경지식). 모르는 종류에는 카드가 없다.
        private static readonly Dictionary<string, (string title, string body)> CardTexts = new Dictionary<string, (string, string)>
        {
            ["extinguisher"] = ("소화기", "안전핀을 뽑고, 바람(실내는 출입문)을 등지고 노즐을 불 쪽으로 향한 뒤 손잡이를 움켜쥐고 빗자루로 쓸듯이 뿌린다. — 행정안전부·국민재난안전포털"),
            ["hydrant"] = ("옥내소화전", "소화전함 문을 열고 노즐과 호스를 꺼내 꼬이지 않게 펴며 불 가까이 간다. 개폐밸브를 돌려 물이 나오게 하고, 물이 차면 노즐 끝을 돌려 분무 또는 직선을 고른다. — 국민재난안전포털"),
            ["gas"] = ("가스 누출", "점화콕·중간밸브·용기밸브를 잠가 공급을 막고 창문·출입문을 열어 환기한다. 환풍기·선풍기 같은 전기기구 스위치는 쓰지 않는다. — 한국가스안전공사"),
            ["cpr"] = ("심폐소생술", "어깨를 두드리며 반응과 호흡을 확인하고 주변에 119 신고와 자동심장충격기를 부탁한다. 가슴 가운데를 분당 100~120회, 5~6 cm 깊이로 30회 누르고 구급대가 올 때까지 되풀이한다. — 광진구 보건소"),
            ["aed"] = ("자동심장충격기", "전원을 켜고 패드 두 장을 오른쪽 빗장뼈 아래와 왼쪽 젖꼭지 아래 중간겨드랑선에 붙인다. '분석 중' 음성이 나오면 손을 떼고, 버튼을 누르기 전 모두 떨어졌는지 확인한 뒤 곧바로 가슴압박을 다시 한다. — 서대문구 보건소"),
        };

        private static IEnumerable<string> CardKeysFor(Hazard hazard)
        {
            switch (hazard)
            {
                case FireHazard _:
                    yield return "extinguisher";
                    yield return "hydrant";
                    break;
                case GasLeakHazard _:
                    yield return "gas";
                    break;
                case CollapseHazard collapse when collapse.Level >= 2:
                    yield return "cpr";
                    yield return "aed";
                    break;
            }
        }

        private void UnlockCards(Hazard hazard)
        {
            if (Guided) return;
            foreach (var key in CardKeysFor(hazard))
            {
                if (!cardKeys.Add(key)) continue;
                var card = CardTexts[key];
                cards.Add(card);
                // 실전은 알리지 않는다(수첩에는 쌓인다).
                if (GameSettings.Guidance == GuidanceLevel.Standard) session.Hud.Toast("수첩에 공개 행동요령 추가 · " + card.title, 4f);
            }
        }

        private BoardOverlay.Column NotebookFacts()
        {
            if (Guided) return null;
            var column = new BoardOverlay.Column { Title = "확인한 사실" };
            if (jev == null || !jev.Available || jev.FailuresInARow >= 3) column.Lines.Add(jev != null ? jev.Status : "JEV 없음");
            for (int i = facts.Count - 1; i >= 0 && column.Lines.Count < NotebookShown; i--) column.Lines.Add(facts[i]);
            return column;
        }

        private BoardOverlay.Column NotebookRadio()
        {
            if (Guided) return null;
            var column = new BoardOverlay.Column { Title = "무전 기록" };
            // 부른 기관이 어디쯤인지는 무전으로 들은 그대로다(출동 중·도착).
            foreach (var pair in calledBy)
            {
                bool here = false;
                foreach (var responder in responders) if (responder.Agency == pair.Key && responder.OnScene) here = true;
                var name = Teams.Name(callTeam.TryGetValue(pair.Key, out var team) ? team : Teams.For(pair.Key, TargetFor(pair.Key)));
                column.Lines.Add(name + " · " + (here ? "현장 도착" : arrived.Contains(pair.Key) ? "도착, 이동 중" : "출동 중"));
            }
            if (question != null && Time.time < question.Until) column.Lines.Add("<color=#FFB020>역무실 질문: " + question.Text + "</color>");
            for (int i = radioRecord.Count - 1; i >= 0 && column.Lines.Count < NotebookShown; i--) column.Lines.Add(radioRecord[i]);
            return column;
        }

        private BoardOverlay.Column NotebookCards()
        {
            if (Guided) return null;
            var column = new BoardOverlay.Column { Title = "공개 행동요령" };
            if (cards.Count == 0) column.Lines.Add("<color=#FFFFFF80>처음 마주친 상황의 일반 국민 행동요령이 여기에 쌓입니다. 코레일 내부 절차가 아닙니다.</color>");
            for (int i = cards.Count - 1; i >= 0; i--) column.Lines.Add("<b>" + cards[i].title + "</b>\n<size=14>" + cards[i].body + "</size>");
            return column;
        }

        // ── 살펴보기 ──

        /// <summary>
        /// What looking closely at <paramref name="looked"/> (or straight ahead when nothing solid is within reach) shows: a hazard's <see cref="Hazard.Visible"/>,
        /// a hurt person's visible condition, a device's own reading, or that nothing is out of the ordinary. Written into the notebook; a hazard looked at becomes
        /// what requests are for and its [상태] slot is what was just seen.
        /// </summary>
        private string Observe(Collider looked)
        {
            var eye = cameraTransform.position;
            var forward = cameraTransform.forward;
            Hazard about = null;
            string text = null;
            if (looked != null)
            {
                var marker = looked.GetComponentInParent<HazardMarker>();
                var person = looked.GetComponentInParent<Passenger>();
                var device = looked.GetComponentInParent<IFpsObservable>();
                if (marker != null && marker.Hazard != null) { about = marker.Hazard; text = about.Visible; }
                else if (person != null) text = Looks(person, out about);
                else if (device != null) text = device.Observe(session.Player);
                else if (looked.GetComponentInParent<IFpsNamed>() is IFpsNamed named && named.DisplayName.Length > 0)
                    text = looked.GetComponentInParent<IFpsStated>() is IFpsStated stated && stated.StateText.Length > 0 ? named.DisplayName + " · " + stated.StateText : named.DisplayName;
            }
            if (text == null)
            {
                about = HazardAhead(eye, forward, looked);
                if (about != null) text = about.Visible;
            }
            if (about != null && about.Active || about is FireHazard)
            {
                Know(about, "직접 살펴봄");
                seen[about] = text;
                focus = about;
                UnlockCards(about);
            }
            if (string.IsNullOrEmpty(text)) text = "특별한 이상은 보이지 않음";
            Fact("살펴봄 · " + text);
            return text;
        }

        /// <summary>A hazard straight ahead that the sight rule lets the staff member see (as <see cref="LookAround"/>): within reach, in the middle of the view, not behind a wall.</summary>
        private Hazard HazardAhead(Vector3 eye, Vector3 forward, Collider looked)
        {
            float reach = looked != null ? Vector3.Distance(eye, looked.ClosestPoint(eye)) + 2.5f : EmergencySession.ObserveReachMetres + 2f;
            Hazard best = null;
            float bestAngle = 15f;
            foreach (var hazard in HazardRegistry.Active)
            {
                var to = hazard.Position + Vector3.up * .5f - eye;
                float distance = to.magnitude;
                if (hazard.NeedsSight)
                {
                    float angle = Vector3.Angle(forward, to);
                    if (distance > reach || angle > bestAngle || !HazardRegistry.CanSee(eye, hazard)) continue;
                    best = hazard;
                    bestAngle = angle;
                }
                // 냄새·소리로 아는 것은 가까이 서서 살피면 안다.
                else if (hazard.Localized && best == null && distance < hazard.NoticeRadius * .8f) best = hazard;
            }
            return best;
        }

        /// <summary>What a person looks like: a hurt one's visible condition (never a diagnosis), or what they are doing.</summary>
        private string Looks(Passenger person, out Hazard about)
        {
            about = null;
            if (person.Hurt)
            {
                CheckInjured(person);
                var collapse = CollapseOf(person);
                about = collapse;
                return collapse != null ? collapse.Visible : "다쳐서 바닥에 주저앉아 있음";
            }
            switch (person.Current)
            {
                case Passenger.Activity.Aggressive: return "소리를 지르며 거칠게 행동함";
                case Passenger.Activity.OnTrack: return "승강장 아래 선로에 내려가 있음";
                case Passenger.Activity.Evacuate: return "서둘러 역 밖으로 나가는 중";
                case Passenger.Activity.TakeCover: return "몸을 웅크려 머리를 감싸고 있음";
                case Passenger.Activity.Deciding:
                case Passenger.Activity.Watch: return "멈춰 서서 어딘가를 지켜봄";
                default: return "승객 · 특별한 이상은 보이지 않음";
            }
        }

        // ── 무전: 보고·요청·방송·응답 ──

        private IEnumerable<EmergencySession.RadioGroup> RadioGroups()
        {
            if (Stage == Phase.Ended) yield break;
            yield return new EmergencySession.RadioGroup { Label = "보고", Options = ReportOptions() };
            var teams = new List<EmergencySession.RadioOption>(TeamRequests.Length);
            for (int i = 0; i < TeamRequests.Length; i++) { int index = i; teams.Add(Option(TeamRequests[i], () => RequestTeam(index))); }
            yield return new EmergencySession.RadioGroup { Label = "요청 · 기관", Options = teams };
            var measures = new List<EmergencySession.RadioOption>(MeasureRequests.Length);
            for (int i = 0; i < MeasureRequests.Length; i++) { int index = i; measures.Add(Option(MeasureRequests[i], () => RequestMeasure(index))); }
            yield return new EmergencySession.RadioGroup { Label = "요청 · 설비", Options = measures };
            var broadcasts = new List<EmergencySession.RadioOption>(BroadcastScopes.Length * 2);
            foreach (var scope in BroadcastScopes)
                foreach (bool atReported in new[] { false, true })
                {
                    var s = scope;
                    bool r = atReported;
                    broadcasts.Add(Option(ScopeName(s) + " · " + (r ? "보고한 위치" : "현 위치"), () => Broadcast(s, r)));
                }
            yield return new EmergencySession.RadioGroup { Label = "방송", Options = broadcasts };
            var answers = new List<EmergencySession.RadioOption>(Answers.Length);
            for (int i = 0; i < Answers.Length; i++) { int index = i; answers.Add(Option(Answers[i], () => Answer(index))); }
            yield return new EmergencySession.RadioGroup { Label = question != null && Time.time < question.Until ? "응답 · 질문 있음" : "응답", Options = answers };
        }

        private List<EmergencySession.RadioOption> ReportOptions()
        {
            var options = new List<EmergencySession.RadioOption>(8);
            // 최근에 지각한 것부터 일곱 가지까지(고리 하나에 여덟 칸).
            for (int i = knownOrder.Count - 1; i >= 0 && options.Count < 7; i--)
            {
                var hazard = knownOrder[i];
                if (!hazard.Reportable || !hazard.Active && !(hazard is FireHazard)) continue;
                options.Add(Option(hazard.Named + (reported.Contains(hazard) ? " · 다시" : ""), () => ReportSlots(hazard)));
            }
            options.Add(Option("순회 중 이상 없음", ReportAllClear));
            return options;
        }

        private void Spoke() => lastFieldResponse = Time.time;

        /// <summary>
        /// A report of <paramref name="hazard"/> assembled from the notebook's slots: [어디] the place, [무엇] the label, [상태] what was last seen of it, [인원] the hurt people
        /// found there. The office acknowledges and asks back for a slot that is missing; it sends nobody on a report.
        /// </summary>
        private void ReportSlots(Hazard hazard)
        {
            Spoke();
            bool again = !reported.Add(hazard);
            lastReported = hazard;
            focus = hazard;
            seen.TryGetValue(hazard, out var state);
            int hurt = HurtNear(hazard);
            // 부상자 수는 가까이 가서 본 곳만 말할 수 있다(전해 들은 쓰러짐은 그 사람을 확인하기 전까지 모른다).
            bool counted = !hazard.Localized || closeLooked.Contains(hazard);
            var line = "역무실, " + hazard.Named;
            if (!string.IsNullOrEmpty(state)) line += ", " + state;
            if (counted) line += hurt == 0 ? ", 부상자 없음" : ", 부상자 " + (hurt >= 3 ? "3명 이상" : hurt + "명");
            Say(line + ".");
            log.Add("역무실에 " + hazard.Label + (again ? " 상황 다시 보고 · " : " 보고 · ") + hazard.Where + (state != null ? " (본 것: " + state + ")" : " (상태 미확인)"));
            // 표준: 보고한 위험은 나침반에 남는다(실전은 내 표시만).
            if (GameSettings.Guidance == GuidanceLevel.Standard && hazard.Localized) session.Hud.Compass.SetMarker("incident-" + hazard.Id, hazard.Position, MarkerKind.Incident);
            string ack = "역무실 수신. " + hazard.Named + (again ? " 상황 접수했습니다." : " 접수했습니다.");
            if (state == null) Ask(ack, "현장을 직접 확인했습니까?",
                () => { Office("역무실 수신. 직접 확인으로 접수합니다."); log.Add("역무실 질문에 '직접 확인함'으로 답함 · " + hazard.Label); },
                () => { Office("역무실 수신. 미확인으로 접수합니다."); log.Add("역무실 질문에 '직접 확인 못 함'으로 답함 · " + hazard.Label); });
            else if (!counted) Ask(ack, "부상자 있습니까?",
                () => { Office("역무실 수신. 부상자 있음으로 접수합니다."); log.Add("역무실 질문에 '부상자 있음'으로 답함 · " + hazard.Label); },
                () => { Office("역무실 수신. 부상자 없음으로 접수합니다."); log.Add("역무실 질문에 '부상자 없음'으로 답함 · " + hazard.Label); });
            else Office(ack);
        }

        /// <summary>Hurt people the staff member found near <paramref name="hazard"/> (anywhere, for a station-wide one); a collapse counts its own person.</summary>
        private int HurtNear(Hazard hazard)
        {
            int count = 0;
            float reach = Mathf.Max(hazard.Clearance, 4f) + 6f;
            foreach (var person in injuredKnown)
            {
                if (person == null || !person.Hurt || treated.Contains(person)) continue;
                if (hazard is CollapseHazard collapse && collapse.Person == person) { count++; continue; }
                if (hazard.Localized)
                {
                    var d = person.transform.position - hazard.Position;
                    if (Mathf.Abs(d.y) > 3f) continue;
                    d.y = 0;
                    if (d.magnitude > reach) continue;
                }
                count++;
            }
            return count;
        }

        private void ReportAllClear()
        {
            Spoke();
            Say("역무실, " + world.Describe(PlayerPosition) + " 순회 중 이상 없습니다.");
            Office("역무실 수신.");
            log.Add("역무실에 이상 없음 보고 · " + world.Describe(PlayerPosition));
        }

        /// <summary>The office asks back (<paramref name="text"/>) after <paramref name="ack"/>; the answer group of the radio replies.</summary>
        private void Ask(string ack, string text, Action yes, Action no, Action unsure = null)
        {
            question = new Question { Text = text, Yes = yes, No = no, Unsure = unsure ?? (() => Office("역무실 수신.")), Until = Time.time + QuestionSeconds };
            Office(ack + " " + text);
        }

        private void Answer(int index)
        {
            Spoke();
            Say("역무실, " + Answers[index] + ".");
            var asked = question;
            question = null;
            if (asked == null || Time.time > asked.Until) { Office("역무실입니다. 지금 확인할 사항은 없습니다."); return; }
            (index == 0 ? asked.Yes : index == 1 ? asked.No : asked.Unsure)();
        }

        /// <summary>
        /// What a request is for: the hazard last reported, looked at or perceived that is still going on (a fire counts once out: its scene still needs the team).
        /// Null when the staff member has perceived nothing — then nobody is sent.
        /// </summary>
        private Hazard RequestTarget()
        {
            if (focus != null && (focus.Active || focus is FireHazard)) return focus;
            for (int i = knownOrder.Count - 1; i >= 0; i--) if (knownOrder[i].Active) return knownOrder[i];
            return null;
        }

        private int UntreatedKnownPatients()
        {
            int count = 0;
            foreach (var person in injuredKnown) if (person != null && person.Hurt && !treated.Contains(person)) count++;
            return count;
        }

        private void RequestTeam(int index)
        {
            Spoke();
            string label = TeamRequests[index];
            Agency agency;
            switch (index)
            {
                case 0: agency = Agency.Fire; break;
                case 1: agency = Agency.Medical; break;
                case 2:
                case 3: agency = Agency.Police; break;
                default: agency = Agency.Facility; break;
            }
            var target = RequestTarget();
            // 구급은 확인한 부상자에게도 간다: 쓰러짐을 지각했으면 그 사람이 대상이다.
            if (agency == Agency.Medical)
            {
                CollapseHazard nearest = null;
                float best = float.PositiveInfinity;
                foreach (var hazard in known)
                    if (hazard is CollapseHazard collapse && collapse.Active && !collapse.Treated)
                    {
                        float d = Vector3.Distance(collapse.Position, PlayerPosition);
                        if (d < best) { best = d; nearest = collapse; }
                    }
                if (nearest != null) target = nearest;
            }
            if (target == null && !(agency == Agency.Medical && UntreatedKnownPatients() > 0))
            {
                Say("역무실, " + label + " 요청합니다.");
                Office("역무실 수신. 현장 확인 후 다시 보고 바랍니다.");
                log.Add("역무실에 " + label + " 요청 · 대상 없음(지각한 것 없음)");
                return;
            }
            Team team;
            switch (index)
            {
                case 0: team = Teams.For(Agency.Fire, target); break;
                case 1: team = Team.Ems; break;
                case 2: { var police = Teams.For(Agency.Police, target); team = police == Team.RailwayPolice ? Team.Patrol : police; break; }
                case 3: team = Team.RailwayPolice; break;
                case 4: team = Team.Facility; break;
                case 5: team = Team.Electric; break;
                case 6: team = Team.Gas; break;
                default: team = Team.Elevator; break;
            }
            string about = target != null ? target.Named : "부상 승객";
            Say("역무실, " + about + " 건으로 " + label + " 요청합니다.");
            if (!Call(agency, "역무원 요청", target, team))
            {
                var going = Teams.Name(callTeam.TryGetValue(agency, out var sent) ? sent : Teams.For(agency, TargetFor(agency)));
                Office("역무실 수신. " + going + KoreanText.Subject(going) + " 이미 출동했습니다.");
                return;
            }
            var name = Teams.Name(team);
            Office("역무실 수신. " + name + " 출동 요청했습니다.");
        }

        private void RequestMeasure(int index)
        {
            Spoke();
            string label = MeasureRequests[index];
            var target = RequestTarget();
            switch (index)
            {
                case 0: RequestTrainHold(target); break;
                case 1: RequestEscalatorStop(target); break;
                case 2: RequestReceiverReset(); break;
                case 3:
                    Say("역무실, " + (target != null ? target.Where + " " : "") + "CCTV 확인 부탁합니다.");
                    if (target == null || !target.Localized) { Office("역무실 수신. 현장 확인 후 다시 보고 바랍니다."); return; }
                    CctvCheck(target.Position, target.Where);
                    break;
                default:
                {
                    Say("역무실, " + label + " 요청합니다.");
                    var left = shutters.FirstOrDefault(s => shutterSeen.Contains(s) && s.Opening < .999f && !s.Triggered && !s.ControllerFault && !s.Moving);
                    if (left == null) { Office("역무실 수신. 현장 확인 후 다시 보고 바랍니다."); return; }
                    var fault = shutterFault != null && shutterFault.Active && known.Contains(shutterFault) ? shutterFault : target;
                    if (!Call(Agency.Facility, "역무원 방화셔터 복구 요청", fault, Team.Facility)) { Office("역무실 수신. 시설 쪽은 이미 출동했습니다."); return; }
                    Office("역무실 수신. 시설 담당 출동 요청했습니다.");
                    break;
                }
            }
        }

        private void RequestTrainHold(Hazard target)
        {
            Say("역무실, 5·6 타는 곳 서울행 열차 출발 보류 요청합니다.");
            bool track = target != null && target.TrainHold != null;
            if (Train == null || !Train.AtPlatform && !track) { Office("역무실 수신. 지금 타는 곳에 서 있는 열차가 없습니다."); return; }
            if (Train.AtPlatform)
            {
                holdRequested = true;
                Train.Holds.Add("역무원 요청");
            }
            // 선로 위 사람처럼 열차를 세울 이유를 보고한 것이면 그 승강장으로 들어오는 열차도 세운다.
            if (track) TrainReported(target);
            log.Add("열차 출발 보류 요청" + (target != null ? " · " + target.Named : ""));
            Office("역무실 수신. 관제에 출발 보류 요청했습니다.");
        }

        private void RequestEscalatorStop(Hazard target)
        {
            var list = InvolvedEscalators();
            // 지각한 위험 가까이(없으면 내가 선 곳 가까이) 있는 에스컬레이터. 역 전체에 걸친 위험이면 모두.
            var at = target != null && target.Localized ? target.Position : PlayerPosition;
            foreach (var escalator in world.Escalators)
                if (!closedEscalators.Contains(escalator) && !list.Contains(escalator) && (target != null && !target.Localized || Vector3.Distance(escalator.Middle, at) < 15f)) list.Add(escalator);
            Say("역무실, " + (list.Count == 1 ? list[0].Label : "근처 에스컬레이터") + " 운행 정지 요청합니다.");
            if (list.Count == 0) { Office("역무실 수신. 근처에 세울 에스컬레이터가 없습니다."); return; }
            escalatorsClosed = true;
            foreach (var escalator in list) { escalator.Stop("운행 정지·통제"); escalator.Close(); closedEscalators.Add(escalator); }
            session.Announce(PaLine.EscalatorStopped, "에스컬레이터 운행이 중지되었습니다. 옆 계단과 다른 통로를 이용해 주십시오.");
            log.Add("에스컬레이터 운행 정지·이용 통제 (" + list.Count + "대)");
            Office("역무실 수신. " + (list.Count == 1 ? list[0].Label : "에스컬레이터 " + list.Count + "대") + " 운행 정지했습니다.");
        }

        private void RequestReceiverReset()
        {
            Say("역무실, 수신기 복구 바랍니다.");
            if (!alarm) { Office("역무실 수신. 수신기에 화재 신호가 없습니다."); return; }
            // 감지기를 직접 확인한 비화재보는 바로 복구한다. 그 밖에는 한 번 되묻는다(불이 꺼진 것을 확인했는가).
            if (falseAlarm != null && falseAlarm.Checked && !fires.Exists(f => !f.Extinguished && known.Contains(f))) { ResetFromRequest(); return; }
            Ask("역무실 수신.", fires.Exists(f => known.Contains(f)) ? "불 꺼진 것 확인했습니까?" : "화재 아닌 것 확인했습니까?",
                ResetFromRequest,
                () => Office("역무실 수신. 복구 보류합니다."),
                () => Office("역무실 수신. 복구 보류합니다."));
        }

        private void ResetFromRequest()
        {
            if (falseAlarm != null && !falseAlarm.Cleared) falseAlarm.Clear();
            ResetReceiver("역무원 요청");
            if (!alarm) Office("역무실 수신. 수신기 복구했습니다.");
        }

        private static string ScopeName(PaScope scope)
        {
            switch (scope)
            {
                case PaScope.ClearAround: return "주변 통로 비우기";
                case PaScope.EvacuateArea: return "구역 대피";
                case PaScope.EvacuateStation: return "역 전체 대피";
                default: return "침착 안내";
            }
        }

        /// <summary>
        /// An announcement the staff member asks for: <paramref name="scope"/> around where they stand or around the hazard they last reported. When the reported
        /// hazard's own announcement has that scope its recorded line plays; otherwise the office reads a plain text after the chime.
        /// </summary>
        private void Broadcast(PaScope scope, bool atReported)
        {
            Spoke();
            Hazard subject;
            Vector3 centre;
            string where;
            if (atReported)
            {
                subject = lastReported != null && (lastReported.Active || lastReported is FireHazard) ? lastReported : null;
                if (subject == null)
                {
                    Say("역무실, " + ScopeName(scope) + " 안내방송 요청합니다.");
                    Office("역무실 수신. 보고된 위치가 없습니다.");
                    return;
                }
                centre = subject.Localized ? subject.Position : PlayerPosition;
                where = subject.Where;
            }
            else
            {
                centre = PlayerPosition;
                where = world.Describe(centre);
                // 내가 선 곳 가까이에서 지각한 위험이 있으면 사람들은 그것을 피한다.
                subject = null;
                float best = 20f;
                foreach (var hazard in known)
                {
                    if (!hazard.Active || !hazard.Localized) continue;
                    float d = Vector3.Distance(hazard.Position, centre);
                    if (d < best) { best = d; subject = hazard; }
                }
            }
            announced = true;
            Say("역무실, " + where + " " + ScopeName(scope) + " 안내방송 요청합니다.");
            var own = subject != null ? subject.Announcement : default;
            bool recorded = atReported && subject != null && own.Scope == scope;
            if (recorded) session.Announce(own.Line, own.Text);
            else session.Hud.Radio.Push(RadioChannel.Announcement, BroadcastText(scope, where));
            float radius = subject != null && own.Scope == scope && own.Radius > 0 ? own.Radius : scope == PaScope.ClearAround ? 15f : 30f;
            ApplyAnnouncement(scope, centre, radius, subject);
            Office("역무실 수신. 안내방송 내보냈습니다.");
        }

        private static string BroadcastText(PaScope scope, string where)
        {
            switch (scope)
            {
                case PaScope.ClearAround: return "안내 말씀 드립니다. " + where + " 주변 통로를 비워 주시기 바랍니다. 직원의 안내에 따라 주십시오.";
                case PaScope.EvacuateArea: return "안내 말씀 드립니다. " + where + " 주변에 계신 승객 여러분께서는 직원의 안내에 따라 다른 곳으로 이동해 주시기 바랍니다.";
                case PaScope.EvacuateStation: return "안내 말씀 드립니다. 승객 여러분께서는 직원의 안내에 따라 가까운 출구로 역 밖으로 대피해 주시기 바랍니다.";
                default: return "안내 말씀 드립니다. " + where + "에서 직원이 상황을 확인하고 있습니다. 승객 여러분께서는 침착하게 직원의 안내를 따라 주시기 바랍니다.";
            }
        }
    }
}
