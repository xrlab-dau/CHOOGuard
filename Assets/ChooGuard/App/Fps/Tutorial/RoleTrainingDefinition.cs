using System;
using System.Collections.Generic;
using ChooGuard.App.Fps.Work;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;

namespace ChooGuard.App.Fps.Tutorial
{
    internal enum TrainingConditionKind { Fact, Action, Custody, Parent, Unheld, Near, MeasurementAtMost, MeasurementAtLeast, Role }

    internal sealed class TrainingCondition
    {
        public StableId Id;
        public string Target, Other, Key, Actor = "player", Tool, Recipient, AfterTarget;
        public TrainingConditionKind Kind;
        public ActionVerb Verb, AfterVerb;
        public RuleTruth Expected = RuleTruth.TRUE;
        public SiUnit Unit;
        public double Number;
    }

    /// <summary>Typed practice curriculum. Predicate keys read facts; they never execute effect code.</summary>
    public sealed class RoleTrainingDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public string SourceRef => RoleTrainingFixtures.SourceRef;
        public long Revision => RoleTrainingFixtures.Revision;
        public ActorRole Role { get; }
        public IReadOnlyList<GameplayProcedureStep> Steps => steps;
        public IReadOnlyCollection<string> RequiredBindings => requiredBindings;
        internal readonly List<TrainingCondition> Conditions = new List<TrainingCondition>();
        private readonly List<GameplayProcedureStep> steps = new List<GameplayProcedureStep>();
        private readonly HashSet<string> requiredBindings = new HashSet<string>(StringComparer.Ordinal) { "player", "zone" };
        private readonly RuleExpression practice, roleGuard;

        private RoleTrainingDefinition(ActorRole role, string title)
        {
            Role = role; Title = title; Id = "practice-" + role.ToString().ToLowerInvariant();
            practice = Fact("zone", "practice.fixture");
            roleGuard = Add(new TrainingCondition { Kind = TrainingConditionKind.Role, Target = "player" });
        }
        private RuleExpression Add(TrainingCondition condition)
        {
            condition.Id = new StableId("evidence-" + Conditions.Count);
            Conditions.Add(condition); requiredBindings.Add(condition.Target);
            if (condition.Other != null) requiredBindings.Add(condition.Other);
            if (condition.Actor != null) requiredBindings.Add(condition.Actor);
            if (condition.Tool != null) requiredBindings.Add(condition.Tool);
            if (condition.Recipient != null) requiredBindings.Add(condition.Recipient);
            if (condition.AfterTarget != null) requiredBindings.Add(condition.AfterTarget);
            return RuleExpression.Fact(condition.Id);
        }
        private RuleExpression Fact(string target, string fact, RuleTruth expected = RuleTruth.TRUE)
            => Add(new TrainingCondition { Kind = TrainingConditionKind.Fact, Target = target, Key = fact, Expected = expected });
        private RuleExpression Action(string target, ActionVerb verb, string point = null, string tool = null, string recipient = null, string actor = "player")
            => Add(new TrainingCondition { Kind = TrainingConditionKind.Action, Target = target, Verb = verb, Key = point, Tool = tool, Recipient = recipient, Actor = actor });
        private RuleExpression ActionAfter(string target, ActionVerb verb, string afterTarget, ActionVerb afterVerb, string point = null)
            => Add(new TrainingCondition { Kind = TrainingConditionKind.Action, Target = target, Verb = verb, Key = point,
                AfterTarget = afterTarget, AfterVerb = afterVerb });
        private RuleExpression Custody(string target, string holder)
            => Add(new TrainingCondition { Kind = TrainingConditionKind.Custody, Target = target, Other = holder });
        private RuleExpression Parent(string target, string parent)
            => Add(new TrainingCondition { Kind = TrainingConditionKind.Parent, Target = target, Other = parent });
        private RuleExpression Unheld(string target)
            => Add(new TrainingCondition { Kind = TrainingConditionKind.Unheld, Target = target });
        private RuleExpression Near(string target, string other, double practiceMetres)
            => Add(new TrainingCondition { Kind = TrainingConditionKind.Near, Target = target, Other = other, Number = practiceMetres });
        // Authored clearance of this synthetic lab marker only; not a railway clearance distance.
        private RuleExpression ClearPracticePassage()
            => All(Fact("route", "passage.clear"), RuleExpression.Not(Near("cart", "route", 1.2)),
                RuleExpression.Not(Near("sign", "route", 1.2)));
        private RuleExpression PreservedProperty()
            => All(Unheld("lost-item"), Fact("lost-item", "waste.permitted", RuleTruth.FALSE),
                RuleExpression.Not(Parent("lost-item", "bin")), RuleExpression.Not(Parent("lost-item", "laundry-bin")));
        private RuleExpression AtMost(string target, string key, double value, SiUnit unit)
            => Add(new TrainingCondition { Kind = TrainingConditionKind.MeasurementAtMost, Target = target, Key = key, Number = value, Unit = unit });
        private RuleExpression AtLeast(string target, string key, double value, SiUnit unit)
            => Add(new TrainingCondition { Kind = TrainingConditionKind.MeasurementAtLeast, Target = target, Key = key, Number = value, Unit = unit });
        private static RuleExpression All(params RuleExpression[] expressions) => RuleExpression.All(expressions);
        private static RuleExpression Any(params RuleExpression[] expressions) => RuleExpression.Any(expressions);
        private void Step(string id, string label, string[] requires, string reason, RuleExpression evidence,
            RuleExpression start = null, RuleExpression applies = null)
        {
            steps.Add(new GameplayProcedureStep(id, label, SourceRef + "-" + Role, Revision, Role, requires,
                applies == null ? practice : All(practice, applies), start == null ? roleGuard : All(roleGuard, start), evidence, reason));
        }
        private static string[] After(params string[] ids) => ids;

        public static RoleTrainingDefinition Create(ActorRole role)
        {
            switch (role)
            {
                case ActorRole.Maintenance: return Maintenance();
                case ActorRole.Cleaning: return Cleaning();
                case ActorRole.PassengerService: return Service();
                case ActorRole.StationStaff: return Station();
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }
        private static RoleTrainingDefinition Maintenance()
        {
            var d = new RoleTrainingDefinition(ActorRole.Maintenance, "무하중 지그 부품 교체와 확인자 인계");
            d.Step("identity", "작업표와 구/신 부품 식별·문서 판본 대조", After(), "작업표·구품·신품의 실제 표기를 관찰하세요. 실제 차종 매뉴얼은 미확보입니다.",
                All(d.Action("work-order", ActionVerb.Observe), d.Action("old-part", ActionVerb.Observe), d.Action("new-part", ActionVerb.Observe),
                    d.Fact("work-order", "identity.confirmed"), d.Fact("work-order", "revision.confirmed")));
            d.Step("isolate", "무전원 지그의 격리 조작 확인", After("identity"), "명시된 연습 지그 허가/격리 순서가 필요합니다. 무전압 측정은 추정하지 않습니다.",
                All(d.Action("socket", ActionVerb.Isolate), d.Fact("socket", "isolated")), d.Fact("work-order", "assignment.permitted"));
            d.Step("prepare", "공구·계측기 상태 관찰과 트레이 배치", After("identity"), "지정 공구를 관찰하고 트레이를 직접 운반·안정 배치하세요.",
                All(d.Action("driver", ActionVerb.Observe), d.Action("meter", ActionVerb.Observe), d.Fact("driver", "condition.good"),
                    d.Fact("meter", "calibration.valid"), d.Action("tray", ActionVerb.PickUp), d.Action("tray", ActionVerb.PutDown)));
            d.Step("unfasten", "받침 유지·체결점 a/b 해제", After("isolate", "prepare"), "호환 공구 실제 접촉과 받침을 유지하세요. 중단된 회전은 해제 완료가 아닙니다.",
                All(d.Action("old-part", ActionVerb.Unfasten, "a", "driver"), d.Action("old-part", ActionVerb.Unfasten, "b", "driver"),
                    d.Fact("old-part", "released:a"), d.Fact("old-part", "released:b"),
                    d.Fact("old-part", "fastened:a", RuleTruth.FALSE), d.Fact("old-part", "fastened:b", RuleTruth.FALSE)), d.Custody("driver", "player"));
            d.Step("remove", "공구 내려놓기·구품 분리 후 트레이 보존", After("unfasten"), "빈 손과 받침으로 분리한 실물을 지정 트레이에 내려놓으세요.",
                All(d.Action("old-part", ActionVerb.Remove), d.Fact("old-part", "installed", RuleTruth.FALSE), d.Parent("old-part", "tray"), d.Unheld("old-part")));
            d.Step("sample", "구품 표면·접점 관찰과 유효 연습 시료 기록", After("remove"), "교정 확인된 계측기를 수령하고 실제 probe 접촉으로 시료를 얻으세요. 전기/NDT 합격이 아닙니다.",
                All(d.Action("old-part", ActionVerb.Observe, "contact"), d.Action("old-part", ActionVerb.Measure, "main", "meter"), d.Fact("old-part", "sample.valid:main")));
            d.Step("install", "호환 신품 운반·정렬·자리맞춤", After("sample"), "계측기를 내려놓고 신품을 집어 소켓에 설치하세요. 설치는 체결 완료가 아닙니다.",
                All(d.Action("socket", ActionVerb.Install, tool: "new-part"), d.Parent("new-part", "socket"), d.Fact("new-part", "installed")));
            d.Step("fasten", "신품 체결점 a/b의 연습 회전 수행", After("install"), "두 체결점을 각각 완료하세요. 토크·각도·실차 순서 인증은 미확인입니다.",
                All(d.Action("new-part", ActionVerb.Fasten, "a", "driver"), d.Action("new-part", ActionVerb.Fasten, "b", "driver"),
                    d.Fact("new-part", "fastened:a"), d.Fact("new-part", "fastened:b")));
            d.Step("verify", "현재 체결 상태 재검사", After("fasten"), "모든 요구점이 현재 체결되어야 연습 검사가 성립합니다. 재해제/부분 작업은 재검사를 요구합니다.",
                All(d.Action("new-part", ActionVerb.Verify), d.Fact("new-part", "practice.verified")));
            d.Step("recover", "공구·계측기·분리품 회수", After("verify"), "드라이버와 계측기, 구품 모두 트레이에 실제 보존하세요.",
                All(d.Parent("driver", "tray"), d.Parent("meter", "tray"), d.Parent("old-part", "tray"), d.Unheld("driver"), d.Unheld("meter")));
            d.Step("handoff", "미확인 항목 기록·정비 확인자 실물 인계", After("recover"), "작업표에 결과/미확인을 보고하고 확인자에게 실제 전달하세요. 확인자 동의·현장 도착이 필요합니다.",
                All(d.Action("work-order", ActionVerb.Report), d.Action("work-order", ActionVerb.Handoff, recipient: "maintainer"), d.Custody("work-order", "maintainer"),
                    d.Fact("new-part", "practice.verified"), d.Fact("socket", "isolated")));
            return d;
        }
        private static RoleTrainingDefinition Cleaning()
        {
            var d = new RoleTrainingDefinition(ActorRole.Cleaning, "정리·접촉 잔류 제거·보충과 미건조 인계");
            d.Step("survey", "실제 구역·통행·소유불명 물품 구분", After(), "구역과 소유불명 물품을 관찰하세요. 개인 물건을 쓰레기로 지우지 않습니다.",
                All(d.Action("zone", ActionVerb.Observe), d.Action("lost-item", ActionVerb.Observe), d.Fact("lost-item", "waste.permitted", RuleTruth.FALSE)));
            d.Step("segregate", "유실물 보존·기록·담당자 요청", After("survey"), "소유불명 물건은 그대로 보존하고 기록/담당 인계를 요청하세요. 동의 없는 집기는 막힙니다.",
                All(d.Action("lost-item", ActionVerb.Report), d.Action("lost-item", ActionVerb.RequestHelp), d.PreservedProperty()));
            d.Step("place", "카트와 젖음 표지 운반·통로 외 배치", After("survey"), "카트/표지를 실제로 옮겨 실습 면 곁에 놓고 통로를 재관찰하세요.",
                All(d.Action("cart", ActionVerb.PutDown), d.Action("sign", ActionVerb.PutDown), d.Near("sign", "surface", 2), d.Unheld("sign"), d.ClearPracticePassage(), d.Action("route", ActionVerb.Observe)));
            d.Step("waste", "허용 폐기물만 수거 용기 안으로 운반", After("segregate", "place"), "허용 종이 폐기물을 집어 여유가 확인된 전용 수거함에 넣으세요.",
                All(d.Action("waste", ActionVerb.DisposeWaste, recipient: "bin"), d.Parent("waste", "bin"), d.Unheld("waste")));
            d.Step("clean", "지정 닦개로 main/edge 실제 접촉·잔류 제거", After("waste"), "소모품 잔량·도구 오염을 확인하며 두 구역을 닦으세요. 접촉하지 않은 면은 남습니다.",
                All(d.AtMost("surface", "soil:main", 0, SiUnit.Dimensionless), d.AtMost("surface", "soil:edge", 0, SiUnit.Dimensionless),
                    d.Action("surface", ActionVerb.Clean, tool: "cleaning-tool"),
                    d.AtMost("cleaning-tool", "supply", .000020000001, SiUnit.CubicMetre), d.Fact("surface", "dirty", RuleTruth.FALSE)),
                All(d.Fact("surface", "safe.material.known"), d.Fact("cleaning-tool", "contaminated", RuleTruth.FALSE)));
            d.Step("laundry", "사용 도구를 실제 회수함으로", After("clean"), "젖은 사용 도구를 버리지 말고 지정 세탁/도구 회수함에 넣으세요.",
                All(d.Action("cleaning-tool", ActionVerb.PutDown, recipient: "laundry-bin"), d.Parent("cleaning-tool", "laundry-bin"), d.Unheld("cleaning-tool")));
            d.Step("refill", "보충 용기 직접 수령·허용 저장칸 보충", After("laundry"), "공급 잔량과 보충 용기의 증가가 함께 확인되어야 합니다.",
                All(d.Action("dispenser", ActionVerb.Refill, tool: "supply"), d.AtLeast("dispenser", "supply", .001, SiUnit.CubicMetre), d.AtMost("supply", "supply", 0, SiUnit.CubicMetre)));
            d.Step("handoff", "잔류 재관찰·미건조/폐기물/도구 인계 보고", After("refill"), "젖음·건조 근거 미확인을 보고하고 담당자에게 작업표를 실제 인계하세요. 표지는 유지하며 제한 범위의 정리 실습만 완료합니다.",
                All(d.ActionAfter("surface", ActionVerb.Observe, "surface", ActionVerb.Clean), d.Action("surface", ActionVerb.Report), d.Action("surface", ActionVerb.RequestHelp),
                    d.Action("bin", ActionVerb.Report), d.Action("laundry-bin", ActionVerb.Report), d.Near("sign", "surface", 2), d.Unheld("sign"),
                    d.Fact("surface", "wet"), d.Fact("surface", "dirty", RuleTruth.FALSE), d.Unheld("lost-item"),
                    d.Action("work-order", ActionVerb.Report), d.Action("work-order", ActionVerb.Handoff, recipient: "colleague"), d.Custody("work-order", "colleague")));
            return d;
        }
        private static RoleTrainingDefinition Service()
        {
            var d = new RoleTrainingDefinition(ActorRole.PassengerService, "출무 수령·고객지원·실제 도착 인계");
            d.Step("checkin", "출무 작업표·장비·재고 직접 확인", After(), "담당 구역/약속과 무전기 상태를 확인하고 실물 장비를 수령하세요.",
                All(d.Action("work-order", ActionVerb.Observe), d.Action("radio", ActionVerb.Observe), d.Action("radio", ActionVerb.PickUp),
                    d.Action("service-supply", ActionVerb.Observe), d.Fact("radio", "condition.good")));
            d.Step("passage", "통로·개인 물품 소유와 동의 확인", After("checkin"), "통로를 관찰하고 고객에게 지원을 제안하세요. 타인의 동의를 대신 쓰지 않습니다.",
                All(d.Action("route", ActionVerb.Observe), d.ClearPracticePassage(), d.Action("passenger", ActionVerb.RequestHelp),
                    d.Action("passenger", ActionVerb.Consent, recipient: "player", actor: "passenger")));
            d.Step("supply", "저장 위치의 비품을 실제 고객에게 전달", After("passage"), "무전기를 내려놓고 비품을 집어 고객에게 직접 인계하세요. 대사 수락만으로 재고는 바뀌지 않습니다.",
                All(d.Action("service-supply", ActionVerb.PickUp), d.Action("service-supply", ActionVerb.Handoff, recipient: "passenger"), d.Custody("service-supply", "passenger")));
            d.Step("route", "희망 목적지·지원 채널·현장 경로 확인", After("supply"), "고객과 목적지를 관찰/기록하고 동의한 경로를 확인하세요. 관측하지 않은 지도는 확정하지 않습니다.",
                All(d.Action("passenger", ActionVerb.Observe), d.Action("passenger", ActionVerb.Report), d.Action("destination", ActionVerb.Observe), d.ClearPracticePassage()));
            d.Step("escort", "고객 동의 후 안내 시작·실제 만남점 도착", After("route"), "Escort 수락은 도착이 아닙니다. 고객의 자율 이동과 만남점 실제 위치를 확인하세요.",
                All(d.Action("passenger", ActionVerb.Escort), d.Near("passenger", "destination", 1)));
            d.Step("defect", "설비 관측 현상·미확인 담당 통보", After("checkin"), "설비를 직접 관찰해 보고/전문 도움을 요청하세요. 승무서비스는 정비/출입문 권한을 얻지 않습니다.",
                All(d.Action("facility", ActionVerb.Observe), d.Action("facility", ActionVerb.Report), d.Action("facility", ActionVerb.RequestHelp)));
            d.Step("handoff", "도착 담당자 실제 만남·지원 책임 인수", After("escort", "defect"), "고객/수신 담당자의 동의와 실제 현장 도착 후 지원 책임을 인계하세요.",
                All(d.Action("passenger", ActionVerb.Handoff, recipient: "colleague"), d.Parent("passenger", "colleague"),
                    d.Near("passenger", "destination", 1), d.Near("colleague", "destination", 1)));
            d.Step("close", "남은 장비 반납·미해결 요청 보고", After("handoff"), "무전기를 실제 반납함에 놓고 작업표를 마감 보고하세요. 고객 비품은 고객 custody로 남아야 합니다.",
                All(d.Parent("radio", "return-bin"), d.Unheld("radio"), d.Action("work-order", ActionVerb.Report), d.Custody("service-supply", "passenger")));
            return d;
        }
        private static RoleTrainingDefinition Station()
        {
            var d = new RoleTrainingDefinition(ActorRole.StationStaff, "외관점검·현장 통로·접근 제한과 인계");
            d.Step("inspect", "설비 식별·제원·계기·외관의 각 면 관찰", After(), "serial/spec/gauge/body 각각 실제 관찰하세요. 계기 영상은 숨은 결함 판정이 아닙니다.",
                All(d.Action("facility", ActionVerb.Observe, "serial"), d.Action("facility", ActionVerb.Observe, "spec"),
                    d.Action("facility", ActionVerb.Observe, "gauge"), d.Action("facility", ActionVerb.Observe, "body")));
            d.Step("verdict", "본인 판정을 기록 · 설비 truth와 별개", After("inspect"), "fit 또는 unfit 개인 판정을 기재하세요. 적합 오판도 기록되지만 부식은 지워지지 않습니다.",
                Any(d.Action("facility", ActionVerb.Report, "fit"), d.Action("facility", ActionVerb.Report, "unfit")));
            d.Step("request", "관측된 부적합 설비의 담당 처리 요청", After("verdict"), "물리 부식이 있으면 판정 문장과 관계없이 처리 요청을 남기세요. 요청은 교체 수행이 아닙니다.",
                d.Action("facility", ActionVerb.RequestHelp), applies: d.Fact("facility", "corroded"));
            d.Step("tag", "어느 판정이든 지정 설비 실물 점검표 부착", After("verdict"), "점검표를 집어 정확한 설비 부착점에 놓으세요. 손에 들거나 다른 설비에 붙인 것은 미완료입니다.",
                All(d.Action("tag-socket", ActionVerb.Install, tool: "tag"), d.Parent("tag", "tag-socket"), d.Parent("tag-socket", "facility"), d.Fact("tag", "installed")));
            d.Step("audit", "현장 단말과 설비 현재 상태 재확인", After("tag", "request"), "단말 열기만으로 합격하지 않습니다. 설비를 재관찰하고 부식/오판/미인수를 보고하세요.",
                All(d.ActionAfter("terminal", ActionVerb.Observe, "tag-socket", ActionVerb.Install),
                    d.Action("facility", ActionVerb.Report), d.ActionAfter("facility", ActionVerb.Observe, "tag-socket", ActionVerb.Install, "body")));
            d.Step("lost", "유실물 보존·담당 알림", After("audit"), "소유불명 물건을 폐기하지 말고 보고/담당 확인을 요청하세요.",
                All(d.Action("lost-item", ActionVerb.Observe), d.Action("lost-item", ActionVerb.Report), d.Action("lost-item", ActionVerb.RequestHelp),
                    d.PreservedProperty()));
            d.Step("passage", "현장 통행 관찰·카트 권한 내 이동", After("lost"), "카트를 실제로 운반/안정 배치하고 목적지와 현재 통로를 관찰하세요.",
                All(d.Action("cart", ActionVerb.PutDown), d.Action("route", ActionVerb.Observe), d.ClearPracticePassage(), d.Action("destination", ActionVerb.Observe)));
            d.Step("cordon", "실물 표지로 합성 구역 접근 제한", After("passage"), "표지를 수령해 지정 실습 구역에 배치하세요. 표지는 결함 제거/전기 격리가 아닙니다.",
                All(d.Action("cordon-zone", ActionVerb.Cordon, tool: "sign"), d.Fact("cordon-zone", "cordoned"), d.Near("sign", "cordon-zone", 1), d.Unheld("sign")));
            d.Step("support", "이용객 동의·안내·실제 만남점 이동", After("cordon"), "요청 후 본인의 동의·Escort와 자율 이동을 기다리세요. 거절/미도착은 미완료입니다.",
                All(d.Action("passenger", ActionVerb.RequestHelp), d.Action("passenger", ActionVerb.Consent, recipient: "player", actor: "passenger"),
                    d.Action("passenger", ActionVerb.Escort), d.Near("passenger", "destination", 1)));
            d.Step("handback", "직원 도착·지원 인수·미해결 시설 인계", After("support"), "직원이 만남점에 실제 도착하고 고객의 지원 책임을 인수해야 합니다. 철도 운행재개 승인은 아닙니다.",
                All(d.Action("passenger", ActionVerb.Handoff, recipient: "colleague"), d.Parent("passenger", "colleague"),
                    d.Near("colleague", "destination", 1), d.Near("passenger", "destination", 1), d.Action("terminal", ActionVerb.Report),
                    d.Fact("cordon-zone", "cordoned"), d.Parent("tag", "tag-socket")));
            return d;
        }
    }
}
