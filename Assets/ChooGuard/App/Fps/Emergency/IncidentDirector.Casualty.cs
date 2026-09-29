using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Hud;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Casualty family: medical emergencies of specific passengers (sudden collapse, seizure, chest pain up to cardiac
    /// arrest, breathing trouble, low blood sugar), falls on stairs, falls and comb-plate entrapment on escalators and a
    /// suitcase tumbling down an escalator. Medical conditions can worsen or ease while people wait; bystanders help,
    /// fetch staff or phone 119. First-aid advice follows public guidance (research.md), not an internal SOP.
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private readonly List<CollapseHazard> casualties = new List<CollapseHazard>();
        private readonly Dictionary<CollapseHazard, Condition> conditions = new Dictionary<CollapseHazard, Condition>();
        private readonly HashSet<Passenger> treated = new HashSet<Passenger>();
        private readonly HashSet<Escalator> closedEscalators = new HashSet<Escalator>();
        private bool escalatorsClosed;

        private IEnumerable<CollapseHazard> Casualties => casualties;

        /// <summary>A medical condition: what JEV reads per level and what onlookers see per level (0 conscious .. 4 not breathing normally).</summary>
        private sealed class Condition
        {
            public string Kind, En, Cause, Told, Advice;
            public string[] Levels, States;
            /// <summary>What the person answers when the staff member checks on them at levels 0 and 1 (still able to talk).</summary>
            public string[] Says;
            /// <summary>From this level on the person lies on the floor (below it they sit or crouch, conscious).</summary>
            public int DownFrom;
        }

        private static readonly Condition Faint = new Condition
        {
            Kind = "collapse", En = "suddenly feels faint and collapses", Cause = "쓰러진", Told = "에 사람이 쓰러졌어요! 빨리 와 주세요.", Advice = "", DownFrom = 1,
            Levels = new[] { "dizzy and sits down on the floor, fully conscious", "falls down but answers when spoken to", "falls down and responds only weakly", "unconscious but breathing", "unconscious and not breathing normally" },
            States = new[] { "어지러워하며 주저앉음(의식 있음)", "쓰러졌으나 부르면 대답함", "쓰러져 반응이 약함", "의식을 잃고 쓰러짐(숨은 쉼)", "의식을 잃고 숨이 고르지 않음" },
            Says = new[] { "어지러워서 잠깐 앉아 있을게요…", "머리가 핑 돌아요… 일어설 수가 없어요." },
        };

        private static readonly Condition Seizure = new Condition
        {
            Kind = "seizure", En = "suddenly has a seizure", Cause = "경련을 일으킨", Told = "에 사람이 쓰러져서 온몸을 떨어요! 빨리 와 주세요.", DownFrom = 1,
            Advice = " 경련 중에는 붙잡거나 입에 아무것도 넣지 말고 주변 물건을 치워 주십시오.",
            Levels = new[] { "a brief jerking spell, then sits down dazed", "falls and convulses for about a minute, then is drowsy", "convulses for several minutes", "has repeated seizures without waking in between", "stops breathing normally after the seizure" },
            States = new[] { "잠깐 몸을 떨다 멍하게 주저앉음", "쓰러져 1분쯤 경련한 뒤 졸려 함", "쓰러져 몇 분째 경련함", "깨어나지 못한 채 경련을 되풀이함", "경련 뒤 숨을 제대로 쉬지 않음" },
            Says = new[] { "방금 몸이 떨렸어요… 머리가 멍해요.", "(졸린 듯 느리게) …괜찮… 아요…" },
        };

        private static readonly Condition ChestPain = new Condition
        {
            Kind = "chest_pain", En = "clutches their chest in sudden pain", Cause = "가슴 통증을 호소하는", Told = "에 가슴을 움켜쥐고 쓰러진 분이 있어요!", Advice = "", DownFrom = 2,
            Levels = new[] { "chest tightness, sits down to rest", "severe chest pain and cold sweat, sits on the floor", "chest pain, then faints", "collapses unconscious but breathing", "collapses and is not breathing normally (cardiac arrest)" },
            States = new[] { "가슴이 답답하다며 앉아 쉼", "심한 가슴 통증에 식은땀을 흘리며 바닥에 앉음", "가슴을 움켜쥐다 정신을 잃음", "의식을 잃고 쓰러짐(숨은 쉼)", "의식을 잃고 숨을 제대로 쉬지 않음" },
            Says = new[] { "가슴이 답답해요… 조금 쉬면 될 것 같아요.", "가슴이 너무 아파요… 식은땀이 나요." },
        };

        private static readonly Condition Breathing = new Condition
        {
            Kind = "breathing", En = "starts struggling to breathe (asthma or an allergic reaction)", Cause = "숨쉬기 힘들어하는", Told = "에 숨을 못 쉬는 분이 있어요! 빨리 와 주세요.", DownFrom = 3,
            Advice = " 편한 자세로 앉히고 가진 흡입기나 자가주사기가 있으면 쓰게 도와 주십시오.",
            Levels = new[] { "short of breath, uses an inhaler", "wheezing, cannot finish a sentence", "struggling for air, lips turning pale", "near collapse from lack of air", "collapses and is not breathing normally" },
            States = new[] { "숨이 차 흡입기를 꺼내 씀", "쌕쌕거리며 말을 잇지 못함", "숨을 몰아쉬고 입술이 창백함", "숨이 막혀 쓰러짐", "쓰러져 숨을 제대로 쉬지 않음" },
            Says = new[] { "숨이… 차요… 흡입기… 있어요.", "숨이… 잘… 안 쉬어져요…" },
        };

        private static readonly Condition LowSugar = new Condition
        {
            Kind = "low_blood_sugar", En = "turns shaky, sweaty and confused (low blood sugar)", Cause = "저혈당 증세를 보이는", Told = "에 사람이 식은땀을 흘리며 쓰러졌어요!", DownFrom = 2,
            Advice = " 의식이 또렷하면 단 음료를 조금씩 마시게 하고, 의식이 흐리면 아무것도 먹이지 마십시오.",
            Levels = new[] { "shaky and sweaty, sits down", "confused and unsteady on their feet", "slumps down and answers only weakly", "unconscious but breathing", "unconscious with irregular breathing" },
            States = new[] { "손을 떨고 식은땀을 흘리며 주저앉음", "말이 어눌하고 비틀거림", "주저앉아 부르면 겨우 대답함", "의식을 잃고 쓰러짐(숨은 쉼)", "의식을 잃고 숨이 고르지 않음" },
            Says = new[] { "손이 떨리고 식은땀이 나요… 당이 떨어졌나 봐요.", "여기가… 어디죠…?" },
        };

        // ── 원인 ──

        private IEnumerable<Transition> CasualtyOrigins(Pools pools)
        {
            foreach (var p in pools.Spread(p => p.Current != Passenger.Activity.Walk || p.Elderly, 2)) yield return Medical(p, Faint);
            foreach (var p in pools.Spread(p => true, 1)) yield return Medical(p, Seizure);
            foreach (var p in pools.Spread(p => p.Current != Passenger.Activity.InTrain || p.Elderly, 1)) yield return Medical(p, ChestPain);
            foreach (var p in pools.Spread(p => true, 1)) yield return Medical(p, Breathing);
            foreach (var p in pools.Spread(p => p.Current != Passenger.Activity.Walk, 1)) yield return Medical(p, LowSugar);
            foreach (var p in pools.Spread(p => p.Current == Passenger.Activity.Walk && OnStairs(p), 2)) yield return StairsFall(p);
            int falls = 0, caught = 0;
            foreach (var escalator in world.Escalators.OrderBy(e => Rank(e.Entry.id)))
            {
                if (!escalator.Running) continue;
                var riders = escalator.Bodies().Where(escalator.Carries).Select(b => b.GetComponent<Passenger>()).Where(p => p != null && !p.Hurt && !p.Hostile).ToList();
                if (riders.Count == 0) continue;
                if (falls < 2) { yield return EscalatorFall(riders.OrderBy(p => Rank(p)).First(), escalator); falls++; }
                if (caught < 1) { yield return CombCaught(riders[riders.Count - 1], escalator); caught++; }
                if (!escalator.Entry.up) continue;
                foreach (var owner in riders.Where(p => p.Luggage == 2))
                {
                    var below = Below(owner, escalator);
                    if (below != null) { yield return SuitcaseTumble(owner, below, escalator); break; }
                }
            }
        }

        /// <summary>
        /// Who a suitcase tumbling down an up escalator from <paramref name="owner"/> reaches first: the rider right behind
        /// on the steps, else the person nearest the foot of the escalator (within 3 m of where the steps come out), else
        /// nobody. Riders rarely share a belt here, so the foot is where a falling bag usually meets someone.
        /// </summary>
        private Passenger Below(Passenger owner, Escalator escalator)
        {
            var behind = escalator.Behind(owner.Body)?.GetComponent<Passenger>();
            if (behind != null) return behind.Hurt || behind.Hostile ? null : behind;
            return NearFoot(escalator, owner, null);
        }

        private Passenger NearFoot(Escalator escalator, Passenger owner, Passenger besides)
        {
            var foot = escalator.Start;
            Passenger best = null;
            float bestDistance = 3f;
            foreach (var p in crowd.People)
            {
                if (p == owner || p == besides || p.Hurt || p.Hostile || !p.Body.Visible || escalator.Carries(p.Body)) continue;
                var offset = p.transform.position - foot;
                if (Mathf.Abs(offset.y) > 1f) continue;
                offset.y = 0;
                if (offset.magnitude < bestDistance) { bestDistance = offset.magnitude; best = p; }
            }
            return best;
        }

        /// <summary>Standing or walking on a flight of stairs (the navmesh stair area).</summary>
        private static bool OnStairs(Passenger p) =>
            p.Body.OnNavMesh && NavMesh.SamplePosition(p.transform.position, out var hit, .4f, 1 << StationWorld.StairsArea) && Mathf.Abs(hit.position.y - p.transform.position.y) < .3f;

        private Transition Medical(Passenger person, Condition condition) => new Transition
        {
            Key = condition.Kind + "_" + person.Number, Kind = condition.Kind, Origin = true,
            Description = Profile(person) + ", " + person.Doing + " at " + Place(person.transform.position) + ", " + condition.En + ".",
            Levels = condition.Levels.ToList(),
            Apply = m => StartMedical(person, condition, m),
        };

        private Transition StairsFall(Passenger walker) => new Transition
        {
            Key = "stairs_" + walker.Number, Kind = "stairs_fall", Origin = true,
            Description = Profile(walker) + " walking on the stairs at " + Place(walker.transform.position) + " misses a step and falls.",
            Levels = new List<string> { "stumbles and catches the handrail", "falls and bruises a knee, gets up slowly", "falls and cannot stand up (ankle or hip)", "tumbles down several steps, head bleeding", "falls down the flight and lies unconscious" },
            Apply = m => StartStairsFall(walker, m),
        };

        private Transition EscalatorFall(Passenger rider, Escalator escalator) => new Transition
        {
            Key = "fall_" + rider.Number, Kind = "escalator_fall", Origin = true,
            Description = Profile(rider) + " riding the " + escalator.Entry.label + " loses footing and falls on the moving steps.",
            Levels = new List<string> { "stumbles and catches the handrail", "falls and is bruised, tries to get up", "falls and cannot get up", "falls and knocks down the person behind", "a serious fall; several people pile up" },
            Apply = m => StartFall(rider, escalator, m, .9f, "에스컬레이터 넘어짐", "에스컬레이터에서 넘어진", "fell on the escalator",
                m < .2f ? "에스컬레이터에서 휘청였다가 손잡이를 잡음" : "에스컬레이터 계단에 넘어진 승객", "에스컬레이터에서 넘어짐"),
        };

        private Transition CombCaught(Passenger rider, Escalator escalator) => new Transition
        {
            Key = "comb_" + rider.Number, Kind = "escalator_caught", Origin = true,
            Description = "As " + Profile(rider) + " reaches the end of the " + escalator.Entry.label + ", a shoe or trouser hem gets caught in the comb plate.",
            Levels = new List<string> { "a shoelace is caught and pulled free at once", "a shoe is pulled off and caught; the rider stumbles", "the foot is caught and the rider falls at the landing", "the foot is caught and injured; riders behind bunch up", "the foot is badly caught; riders behind pile up and fall" },
            // 발이 끼이면 곁의 사람이 바로 비상정지 버튼을 누르는 일이 많다(한국승강기안전공단 안내, research.md).
            Apply = m => StartFall(rider, escalator, m, .4f, "에스컬레이터 끼임", "에스컬레이터 발판에 발이 끼인", "had a foot caught at the escalator comb plate",
                m < .3f ? "에스컬레이터 끝 발판에 신발이 끼었다 빠짐" : "에스컬레이터 끝 발판에 발이 끼여 넘어짐", "에스컬레이터 끝 발판에 발이 끼임"),
        };

        private Transition SuitcaseTumble(Passenger owner, Passenger below, Escalator escalator) => new Transition
        {
            Key = "tumble_" + owner.Number, Kind = "suitcase_tumble", Origin = true,
            Description = "The large suitcase of " + Profile(owner) + " on the " + escalator.Entry.label + " slips from their hand and tumbles down toward " +
                Profile(below) + (escalator.Carries(below.Body) ? ", riding right behind them." : ", at the foot of the escalator."),
            Levels = new List<string> { "it bumps their legs and is caught", "it knocks them off balance", "they fall", "they fall and knock down the person behind them", "several people fall and pile up" },
            Apply = m => StartTumble(owner, below, escalator, m),
        };

        // ── 적용 ──

        private CollapseHazard AddCasualty(Passenger person, string label, string cause, string causeEn, string visible, int level)
        {
            var casualty = new CollapseHazard("casualty-" + ++serial, person, label, cause, causeEn, visible, level) { Where = world.Describe(person.transform.position) };
            casualties.Add(casualty);
            return casualty;
        }

        private void StartMedical(Passenger person, Condition condition, float magnitude)
        {
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            var casualty = AddCasualty(person, "응급 환자", condition.Cause, condition.Kind == "collapse" ? "collapsed" : condition.En, "승객이 " + condition.States[level], level);
            casualty.Told = condition.Told;
            casualty.Advice = condition.Advice;
            conditions[casualty] = condition;
            // 의식이 있는 동안은 앉거나 웅크리고, 그 위로는 바닥에 쓰러진다(좌석에서는 앞으로 늘어진다).
            person.Injure(condition.States[level], level >= condition.DownFrom);
            if (person.Current == Passenger.Activity.InTrain && Train != null) Train.Holds.Add("차내 응급환자");
            Register(casualty);
            log.Add("응급 환자 · " + casualty.Where + " — " + condition.States[level]);
        }

        private void StartStairsFall(Passenger walker, float magnitude)
        {
            string[] states = { "계단에서 휘청였다가 난간을 잡고 주저앉음", "계단에서 넘어져 무릎을 다침", "계단에서 넘어져 일어서지 못함", "계단 여러 칸을 굴러 머리에 피가 남", "계단 아래로 굴러 의식 없이 누워 있음" };
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            var casualty = AddCasualty(walker, "계단 넘어짐", "계단에서 넘어진", "fell on the stairs", states[level], -1);
            casualty.Told = "에서 사람이 계단에서 넘어졌어요!";
            walker.Injure("계단에서 넘어짐 · " + states[level], level >= 3);
            Register(casualty);
            log.Add("계단 넘어짐 · " + casualty.Where + " — " + states[level]);
        }

        private void StartFall(Passenger rider, Escalator escalator, float magnitude, float stopAbove, string label, string cause, string causeEn, string visible, string injury)
        {
            escalator.Fall(rider.Body);
            var casualty = AddCasualty(rider, label, cause, causeEn, visible, -1);
            casualty.Where = escalator.Label;
            casualty.Escalator = escalator;
            casualty.Told = "에서 사람이 넘어졌어요! 에스컬레이터 좀 세워 주세요!";
            rider.Injure(injury);
            if (magnitude > .7f)
            {
                var behind = escalator.Behind(rider.Body)?.GetComponent<Passenger>();
                if (behind != null) { escalator.Fall(behind.Body); behind.Injure("앞사람과 함께 넘어짐"); }
            }
            if (magnitude > stopAbove) { escalator.Stop("비상정지 버튼(승객)"); log.Add(escalator.Label + " 비상 정지"); }
            Register(casualty);
            log.Add(label + " · " + escalator.Label);
        }

        private void StartTumble(Passenger owner, Passenger below, Escalator escalator, float magnitude)
        {
            log.Add("여행가방이 " + escalator.Label + " 아래로 굴러 내려감");
            // 0: 다리에 부딪혀 멈춤, 1: 휘청임까지는 넘어지지 않는다. 그 사이 다친 사람이면 가방만 굴러간다.
            if (below == null || below.Hurt || magnitude < .4f) { log.Add(magnitude < .2f ? "굴러 내려간 가방을 아래 사람이 붙잡음" : "굴러 내려간 가방에 아래 사람이 휘청였으나 넘어지지 않음"); return; }
            bool onSteps = escalator.Carries(below.Body);
            if (onSteps) escalator.Fall(below.Body);
            var casualty = AddCasualty(below, "에스컬레이터 넘어짐", "굴러 내려온 가방에 넘어진", "was knocked down by a tumbling suitcase", "굴러 내려온 여행가방에 부딪혀 넘어진 승객", -1);
            casualty.Where = escalator.Label;
            casualty.Escalator = escalator;
            casualty.Told = "에서 가방이 굴러 떨어져 사람이 넘어졌어요!";
            below.Injure("굴러 내려온 여행가방에 부딪혀 넘어짐");
            if (magnitude > .6f)
            {
                var next = onSteps ? escalator.Behind(below.Body)?.GetComponent<Passenger>() : NearFoot(escalator, owner, below);
                if (next != null && !next.Hurt) { if (onSteps) escalator.Fall(next.Body); next.Injure("앞사람과 함께 넘어짐"); }
            }
            if (magnitude > .8f) { escalator.Stop("비상정지 버튼(승객)"); log.Add(escalator.Label + " 비상 정지"); }
            Register(casualty);
        }

        /// <summary>A medical condition worsens (one level up) or eases (one down): the pose follows the level.</summary>
        private void ChangeCondition(CollapseHazard casualty, int step)
        {
            if (!conditions.TryGetValue(casualty, out var condition) || casualty.Person == null) return;
            int level = Mathf.Clamp(casualty.Level + step, 0, 4);
            if (level == casualty.Level) return;
            bool wasDown = casualty.Level >= condition.DownFrom, down = level >= condition.DownFrom;
            casualty.Change(level, "승객이 " + condition.States[level]);
            var body = casualty.Person.Body;
            if (down != wasDown && body.Seat == PersonBody.SeatPhase.None && !body.Scripted)
            {
                body.ClearPoses();
                if (down) body.SetDown(true); else { body.SetCrouch(true); body.SetCough(true); }
            }
            else if (down != wasDown && body.Seat != PersonBody.SeatPhase.None) body.SetSlump(down);
            log.Add((step > 0 ? "환자 상태 악화 · " : "환자 상태 조금 나아짐 · ") + casualty.Where + " — " + condition.States[level]);
        }

        // ── 전개 ──

        private IEnumerable<Transition> CasualtyDevelopments()
        {
            foreach (var casualty in casualties)
            {
                if (!casualty.Active || casualty.Person == null) continue;
                var c = casualty;
                var helper = NearestPerson(c.Position, 12, p => !p.Hostile && !p.Hurt && !p.Helping && p.Current != Passenger.Activity.Evacuate && p.Current != Passenger.Activity.InTrain && p != c.Person);
                // 곁에서 돕는 사람은 둘까지(모두가 모여들지 않는다).
                int helping = crowd.People.Count(p => p.Helping && p.Focus == c);
                if (helper != null && helping < 2 && Ready("bystander_helps"))
                    yield return new Transition { Key = "help_" + helper.Number, Kind = "bystander_helps", Description = "A passenger nearby goes over to help the person who " + c.CauseEn + " at " + c.Where, Apply = _ => { helper.HelpNearby(c); log.Add("주변 승객이 " + c.Cause + " 승객을 도우러 감 · " + c.Where); } };
                if (!known.Contains(c) && helper != null && Ready("passenger_reports"))
                    yield return new Transition { Key = "creport_" + helper.Number, Kind = "passenger_reports", Description = "A passenger runs to fetch the station staff member for the person who " + c.CauseEn + " at " + c.Where, Apply = _ => { helper.ReportToStaff(); log.Add("승객이 역무원을 부르러 감 · " + c.Where); } };
                if (CitizenMayCall(c) && Ready("citizen_calls_119"))
                    yield return new Transition { Key = "call119_" + c.Id, Kind = "citizen_calls_119", Description = "Someone near " + c.Where + " calls 119 for the person who " + c.CauseEn, Apply = _ => CitizenCall(null, c) };
                if (conditions.ContainsKey(c) && !c.Treated)
                {
                    if (c.Level < 4 && Ready("worsens_" + c.Id))
                        yield return new Transition { Key = "worsens_" + c.Id, Kind = "condition_worsens", Description = "The condition of the person who " + c.CauseEn + " at " + c.Where + " gets worse (" + conditions[c].Levels[c.Level + 1] + ")", Apply = _ => ChangeCondition(c, 1) };
                    if (c.Level > 0 && c.Level < 4 && Ready("eases_" + c.Id))
                        yield return new Transition { Key = "eases_" + c.Id, Kind = "condition_eases", Description = "The person who " + c.CauseEn + " at " + c.Where + " improves a little (" + conditions[c].Levels[c.Level - 1] + ")", Apply = _ => ChangeCondition(c, -1) };
                }
                if (c.Escalator != null && c.Escalator.Running)
                {
                    var escalator = c.Escalator;
                    yield return new Transition { Key = "estop_" + escalator.Entry.id, Kind = "escalator_stopped", Description = "A bystander presses the emergency stop button of the " + escalator.Entry.label, Apply = _ => { escalator.Stop("비상정지 버튼(승객)"); log.Add(escalator.Label + " 비상 정지(승객이 버튼을 누름)"); } };
                    var behind = escalator.Behind(c.Person.Body)?.GetComponent<Passenger>();
                    if (behind != null && !behind.Hurt && Ready("pileup"))
                        yield return new Transition { Key = "pileup_" + behind.Number, Kind = "pileup", Description = "The moving steps carry " + Profile(behind) + " into the fallen person and they fall too", Apply = _ => { escalator.Fall(behind.Body); behind.Injure("에스컬레이터에서 앞사람에 걸려 넘어짐"); } };
                }
            }
        }

        // ── 규칙 ──

        private void CasualtyTick()
        {
            if (Stage != Phase.Incident) return;
            foreach (var casualty in casualties)
                if (casualty.Active && Time.time - casualty.StartedAt > 90 && CountAware(casualty) >= 2) CitizenCall(null, casualty);
        }

        public string CheckInjured(Passenger person)
        {
            if (injuredKnown.Add(person))
            {
                log.Add("역무원이 부상 승객을 확인 · " + world.Describe(person.transform.position));
                session.SetMarker("injured-" + person.Number, person.transform.position, MarkerKind.Task, "부상자");
                foreach (var casualty in casualties) if (casualty.Person == person) Know(casualty, "부상자 직접 확인");
            }
            if (treated.Contains(person)) return "구급대원이 처치 중입니다";
            // 부르면 대답하지 못하는 사람은 말 대신 보이는 상태를 알려 준다(반응·호흡).
            var collapse = CollapseOf(person);
            if (collapse != null && collapse.Level >= 2) return collapse.Visible;
            if (collapse != null && collapse.Level >= 0 && conditions.TryGetValue(collapse, out var condition)) return "환자: " + condition.Says[collapse.Level];
            return "부상 승객: 숨쉬기가 힘들어요… 움직이기가 어려워요.";
        }

        /// <summary>The active casualty (collapse, illness or fall) of <paramref name="person"/>, or null.</summary>
        public CollapseHazard CollapseOf(Passenger person) => casualties.Find(c => c.Active && c.Person == person);

        /// <summary>
        /// The staff member set the AED down at <paramref name="at"/>. Beside a collapsed person (2.5 m, same floor) it waits for
        /// the paramedics: the first delivery goes to the shift log with the time since the collapse (JEV 011 carry_and_hand_over,
        /// logged_only). Returns the feedback line, or null when nobody is near.
        /// </summary>
        public string AedSetDown(Vector3 at)
        {
            CollapseHazard best = null;
            float bestDistance = 2.5f;
            foreach (var casualty in casualties)
            {
                if (!casualty.Active || casualty.Person == null) continue;
                var d = casualty.Person.transform.position - at;
                if (Mathf.Abs(d.y) > 1.5f) continue;
                d.y = 0;
                if (d.magnitude < bestDistance) { bestDistance = d.magnitude; best = casualty; }
            }
            if (best == null) return null;
            if (best.AedAt >= 0) return "AED 가 이미 환자 곁에 있습니다";
            best.AedAt = Time.time;
            log.Add("AED 를 환자 곁에 둠 · 쓰러진 지 " + Mathf.RoundToInt(best.AedAt - best.StartedAt) + "초 · " + best.Where);
            return treated.Contains(best.Person) ? "AED 를 구급대원 곁에 두었습니다" : "AED 를 환자 곁에 두었습니다 · 구급대에 인계합니다";
        }

        /// <summary>Nearest untreated injured person; <paramref name="inTrain"/> false skips people still inside a KTX car.</summary>
        public Passenger NextPatient(Vector3 from, bool inTrain = true)
        {
            Passenger best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (var person in crowd.Injured)
            {
                if (person == null || treated.Contains(person)) continue;
                if (!inTrain && person.Current == Passenger.Activity.InTrain) continue;
                // 선로 위 사람은 구조대가 승강장으로 올린 뒤에 처치한다.
                if (OnTrack(person)) continue;
                float d = Vector3.Distance(from, person.transform.position);
                if (d < bestDistance) { bestDistance = d; best = person; }
            }
            return best;
        }

        public void OnTreated(Passenger person)
        {
            if (!treated.Add(person)) return;
            log.Add("구급대가 부상 승객 1명을 처치");
            foreach (var casualty in casualties)
            {
                if (casualty.Person != person || !casualty.Active) continue;
                if (casualty.AedAt >= 0) log.Add("구급대 인계 · 역무원이 곁에 둔 AED 포함");
                casualty.Treated = true;
                casualty.End();
                HazardRegistry.Remove(casualty);
            }
            if (Train != null && !casualties.Exists(c => c.Active && c.Person != null && c.Person.Current == Passenger.Activity.InTrain)) Train.Holds.Remove("차내 응급환자");
        }

        private List<Escalator> InvolvedEscalators()
        {
            var list = new List<Escalator>();
            foreach (var casualty in casualties) if (casualty.Escalator != null && known.Contains(casualty) && !closedEscalators.Contains(casualty.Escalator) && !list.Contains(casualty.Escalator)) list.Add(casualty.Escalator);
            if (escalatorsStopped && (quake != null && known.Contains(quake) || outage != null && known.Contains(outage)))
                foreach (var escalator in world.Escalators) if (!closedEscalators.Contains(escalator) && !list.Contains(escalator)) list.Add(escalator);
            return list;
        }

        private IEnumerable<EmergencySession.RadioOption> CasualtyRadio()
        {
            int injuries = 0;
            foreach (var person in injuredKnown) if (person != null && person.Hurt && !treated.Contains(person)) injuries++;
            if (injuries > 0 && !calledBy.ContainsKey(Agency.Medical))
                yield return Option("역무실 · 부상자 " + injuries + "명, 119 구급 요청", () =>
                {
                    Say("역무실, 부상 승객 " + injuries + "명 있습니다. 구급대 요청합니다.");
                    Office("역무실 수신. 119 구급 요청하겠습니다.");
                    Call(Agency.Medical, "역무원 요청");
                });
            var involved = InvolvedEscalators();
            if (involved.Count > 0 && !escalatorsClosed)
                yield return Option("역무실 · " + (involved.Count == 1 ? involved[0].Label : "에스컬레이터") + " 운행 정지·통제 요청", () =>
                {
                    escalatorsClosed = true;
                    foreach (var escalator in involved) { escalator.Stop("운행 정지·통제"); escalator.Close(); closedEscalators.Add(escalator); }
                    Say("역무실, " + (involved.Count == 1 ? involved[0].Label : "에스컬레이터") + " 운행 정지하고 이용 통제 요청합니다.");
                    session.Announce(PaLine.EscalatorStopped, "에스컬레이터 운행이 중지되었습니다. 옆 계단과 다른 통로를 이용해 주십시오.");
                    log.Add("에스컬레이터 운행 정지·이용 통제");
                    if (!calledBy.ContainsKey(Agency.Facility)) Call(Agency.Facility, "역무원 요청(에스컬레이터)");
                });
        }

        private void CasualtyActions(BoardOverlay.Column column)
        {
            if (casualties.Exists(c => c.Escalator != null)) column.Lines.Add((escalatorsClosed ? "● " : "○ ") + "에스컬레이터 통제");
        }
    }
}
