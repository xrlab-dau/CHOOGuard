using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;

namespace ChooGuard.App.Fps.Tutorial
{
    public sealed class RoleTrainingFixture
    {
        public WorldSnapshot Snapshot { get; }
        public IReadOnlyDictionary<string, StableId> Bindings { get; }
        internal RoleTrainingFixture(WorldSnapshot snapshot, Dictionary<string, StableId> bindings)
        { Snapshot = snapshot; Bindings = new ReadOnlyDictionary<string, StableId>(bindings); }
    }

    /// <summary>Explicit unloaded practice laboratory, not a reconstruction or approved railway SOP.</summary>
    public static class RoleTrainingFixtures
    {
        public const string SourceRef = "docs/CHOOGuard_Story_Plan_v5/design/fps-ai-20260925/INTERACTION_TUTORIAL.md#5";
        public const long Revision = 1;
        public const string RulesetId = "four-role-practice-20260925";

        public static RoleTrainingFixture Create(StableId runId, ActorRole role)
        {
            if (role == ActorRole.Citizen || !Enum.IsDefined(typeof(ActorRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            var bindings = new Dictionary<string, StableId>(StringComparer.Ordinal);
            var entities = new Dictionary<StableId, WorldEntity>();
            var actors = new Dictionary<StableId, ActorState>();
            StableId Id(string key)
            {
                if (!bindings.TryGetValue(key, out var id)) { id = new StableId("practice-" + key); bindings.Add(key, id); }
                return id;
            }
            void Add(string key, string label, EntityKind kind, float x, float y, float z,
                string definition = null, string[] yes = null, string[] no = null,
                IDictionary<string, SiValue> measurements = null, string parent = null)
            {
                var facts = new Dictionary<string, RuleTruth> { ["practice.fixture"] = RuleTruth.TRUE };
                if (yes != null) foreach (var fact in yes) facts.Add(fact, RuleTruth.TRUE);
                if (no != null) foreach (var fact in no) facts.Add(fact, RuleTruth.FALSE);
                var id = Id(key);
                entities.Add(id, new WorldEntity(id, label + " · 합성 실습", kind, 0, new WorldPoint(x, y, z), Id("zone"),
                    parentId: parent == null ? (StableId?)null : Id(parent), definitionId: definition, facts: facts, measurements: measurements));
            }
            void Person(string key, string label, ActorRole actorRole, bool human, float x, float z, GoalKind drive)
            {
                Add(key, label, EntityKind.Actor, x, 0, z);
                var id = Id(key);
                actors.Add(id, new ActorState(id, actorRole, 0, 0, 0, human,
                    "조작 연습 참여자. 동의는 본인이 판단하며 요청/도착/인수를 구별한다.",
                    drives: new Dictionary<GoalKind, double> { [GoalKind.KeepSchedule] = .4, [drive] = .8 }));
            }
            var portable = new[] { "portable" };
            var detached = new[] { "installed", "hazardous" };
            Add("zone", "무하중 직무 연습실", EntityKind.Zone, 7.5f, 0, 3, yes: new[] { "passage.clear" });
            Person("player", "실습자", role, true, 0, 0, GoalKind.KeepSchedule);
            Person("maintainer", "정비 확인 담당", ActorRole.Maintenance, false, 2, 0, GoalKind.InspectEquipment);
            Person("colleague", "역무 인수 담당", ActorRole.StationStaff, false, 12, 0, GoalKind.MaintainAccess);
            Person("passenger", "지원 의사를 가진 이용객", ActorRole.Citizen, false, 10, 1, GoalKind.KeepSchedule);
            Add("destination", "합성 만남점", EntityKind.Zone, 12, 0, 5, yes: new[] { "passage.clear" });
            Add("route", "관찰 가능한 실습 통로", EntityKind.Zone, 10, 0, 3, yes: new[] { "passage.clear" });
            Add("work-order", "연습 작업표 / practice-panel", EntityKind.Equipment, 0, 1, 2,
                yes: new[] { "identity.confirmed", "revision.confirmed", "assignment.permitted", "portable" }, no: detached);
            Add("socket", "무전원 지그 소켓", EntityKind.Socket, 0, 1, 3,
                yes: new[] { "isolation.procedure.known", "isolation.permitted", "occupied", "accepts:practice-panel", "occupant:practice-old-part" },
                no: new[] { "isolated", "power.on" });
            Add("access-panel", "관절 조작용 접근 덮개", EntityKind.Equipment, -1.4f, 1.2f, 3, "practice-hinge",
                yes: new[] { "isolated", "access.permitted" }, no: new[] { "open" });
            Add("old-part", "분리할 지그 패널", EntityKind.Part, 0, 1, 3, "practice-panel",
                new[] { "installed", "required.fastener:a", "required.fastener:b", "fastened:a", "fastened:b", "portable" },
                new[] { "hazardous" }, new Dictionary<string, SiValue> { ["sample:main"] = new SiValue(.5, SiUnit.Dimensionless) }, "socket");
            Add("new-part", "교체용 지그 패널", EntityKind.Part, .8f, 1, 3, "practice-panel",
                new[] { "portable", "required.fastener:a", "required.fastener:b" },
                new[] { "installed", "hazardous", "fastened:a", "fastened:b" });
            Add("driver", "지그 드라이버", EntityKind.Tool, 1.3f, 1, 3, "practice-driver",
                new[] { "portable", "tool.fastening", "condition.good" }, detached);
            Add("meter", "무차원 연습 계측기", EntityKind.Tool, 1.8f, 1, 3, "practice-meter",
                new[] { "portable", "tool.measurement", "calibration.valid" }, detached);
            Add("tray", "분리품 받침 트레이", EntityKind.Container, 2.4f, 1, 3,
                yes: new[] { "portable", "capacity.available" }, no: detached);
            Add("surface", "접촉 구역 두 곳이 있는 실습 면", EntityKind.Surface, 5, 1, 3,
                yes: new[] { "dirty", "safe.material.known" }, no: new[] { "wet" },
                measurements: new Dictionary<string, SiValue> { ["soil:main"] = new SiValue(1, SiUnit.Dimensionless), ["soil:edge"] = new SiValue(1, SiUnit.Dimensionless) });
            Add("cleaning-tool", "연습 닦개 / 물", EntityKind.Tool, 5.8f, 1, 3, "practice-cloth",
                new[] { "portable", "tool.cleaning" }, new[] { "installed", "hazardous", "contaminated" },
                new Dictionary<string, SiValue> { ["supply"] = new SiValue(.00004, SiUnit.CubicMetre),
                    ["consumption.per-work"] = new SiValue(.00001, SiUnit.CubicMetre) });
            Add("cart", "정리 카트", EntityKind.Container, 6.4f, 0, 3, yes: portable, no: detached);
            Add("sign", "젖음/접근 제한 안내 표지", EntityKind.Sign, 4.4f, 0, 3, yes: portable, no: detached);
            Add("lost-item", "소유 불명 개인 물품", EntityKind.PersonalItem, 5, 1, 4,
                yes: new[] { "portable" }, no: new[] { "installed", "hazardous", "waste.permitted" });
            Add("waste", "실습용 일반 종이 폐기물", EntityKind.Part, 5.5f, 1, 4,
                yes: new[] { "portable", "waste.permitted" }, no: detached);
            Add("bin", "일반 폐기물 수거함", EntityKind.Container, 6, 0, 4, yes: new[] { "capacity.available" });
            Add("laundry-bin", "오염 도구 회수함", EntityKind.Container, 6.8f, 0, 4, yes: new[] { "capacity.available" });
            Add("supply", "보충용 연습 물 용기", EntityKind.Consumable, 5, 1, 2, "practice-water", portable, detached,
                new Dictionary<string, SiValue> { ["supply"] = new SiValue(.001, SiUnit.CubicMetre) });
            Add("dispenser", "보충 확인 용기", EntityKind.Container, 5.6f, 1, 2,
                yes: new[] { "accepts:practice-water" }, measurements: new Dictionary<string, SiValue>
                { ["capacity"] = new SiValue(.001, SiUnit.CubicMetre), ["supply"] = new SiValue(0, SiUnit.CubicMetre) });
            Add("radio", "업무 수령 무전기", EntityKind.Tool, 10, 1, 3, yes: new[] { "portable", "condition.good" }, no: detached);
            Add("service-supply", "고객 제공 비품", EntityKind.Consumable, 10.7f, 1, 3, yes: portable, no: detached, parent: "return-bin");
            Add("return-bin", "장비 반납/비품 저장함", EntityKind.Container, 11.5f, 1, 3, yes: new[] { "capacity.available" });
            Add("facility", "외관 점검 연습 설비", EntityKind.Equipment, 15, 1, 3,
                yes: new[] { "corroded", "mechanically.defective" }, no: new[] { "pressure.out.of.range" });
            Add("tag", "설비별 실물 점검표", EntityKind.Part, 15.8f, 1, 3, "inspection.tag",
                new[] { "portable", "inspection.tag" }, detached);
            Add("tag-socket", "점검표 부착점", EntityKind.Socket, 15, 1, 3, "inspection.tag.mount",
                yes: new[] { "accepts:inspection.tag", "inspection.tag.mount", "isolated" }, no: new[] { "occupied" }, parent: "facility");
            Add("terminal", "현장 재확인 단말", EntityKind.Equipment, 16.5f, 1, 3);
            Add("cordon-zone", "합성 접근 제한 구역", EntityKind.Zone, 15, 0, 5, no: new[] { "cordoned" });
            return new RoleTrainingFixture(new WorldSnapshot(runId, 0, 0, new SimTick(0), GameplayMode.Tutorial,
                RulesetId, Revision, entities, actors), bindings);
        }
    }
}
