using System;
using System.Collections.Generic;
using System.Globalization;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using ChooGuard.Domain.Gameplay;

namespace ChooGuard.Application.Gameplay.Content
{
    public static class GameplayInitialWorld
    {
        public static readonly StableId HumanId = new StableId("practice-player");

        /// <summary>Creates stable individual identities, not pooled NPC state. Count excludes the single human.</summary>
        public static WorldSnapshot Create(GameplaySeed seed, int count, GameplayMode mode)
        {
            if (seed == null) throw new ArgumentNullException(nameof(seed));
            if (count < seed.NamedActors.Count || count < 1 || count > 1000) throw new ArgumentOutOfRangeException(nameof(count));
            var entities = new Dictionary<StableId, WorldEntity>();
            foreach (var entity in seed.Entities) entities.Add(entity.Id, entity);
            var actors = new Dictionary<StableId, ActorState>();
            var humanFacts = new Dictionary<string, RuleTruth> { { "practice.fixture", RuleTruth.TRUE }, { "hazardous", RuleTruth.FALSE } };
            entities.Add(HumanId, new WorldEntity(HumanId, "실습자", EntityKind.Actor, 0, seed.HumanPosition, seed.ZoneId, definitionId: "practice-human", facts: humanFacts));
            actors.Add(HumanId, new ActorState(HumanId, ActorRole.Maintenance, 0, 0, 0, true, "독립 합성 조작 실습자 · 실제 철도 작업권한 없음"));
            for (int index = 0; index < count; index++)
            {
                var named = index < seed.NamedActors.Count ? seed.NamedActors[index] : null;
                var template = seed.Actors[named == null ? index % seed.Actors.Count : named.TemplateIndex];
                var id = named?.Id ?? new StableId("npc-" + index.ToString("D4", CultureInfo.InvariantCulture));
                var position = named?.Position ?? new WorldPoint(seed.PopulationOrigin.X + index % seed.PopulationColumns * seed.PopulationSpacing,
                    seed.PopulationOrigin.Y, seed.PopulationOrigin.Z + index / seed.PopulationColumns * seed.PopulationSpacing);
                var facts = new Dictionary<string, RuleTruth> { { "practice.fixture", RuleTruth.TRUE }, { "hazardous", RuleTruth.FALSE } };
                entities.Add(id, new WorldEntity(id, "실습 인물 " + (index + 1).ToString(CultureInfo.InvariantCulture), EntityKind.Actor, 0, position, seed.ZoneId, definitionId: "practice-person", facts: facts));
                var memory = new[] { new ActorObservation(id, template.TargetId, entities[template.TargetId].Revision, new SimTick(0), "assignment.available", RuleTruth.TRUE, ObservationSource.Assignment) };
                var plan = new GoalPlan(new StableId("assignment-" + index.ToString("D4", CultureInfo.InvariantCulture)), template.Goal, template.TargetId, 0,
                    template.Persona, template.CompletionFact, Array.Empty<ActionVerb>());
                actors.Add(id, new ActorState(id, template.Role, 0, 1, 0, false, template.Persona, plan, memory, TransitionKernel.Copy(template.Drives)));
            }
            return new WorldSnapshot(new StableId(seed.Id + (mode == GameplayMode.Tutorial ? "-tutorial" : "-main")), 0, 0, new SimTick(0), mode,
                seed.RulesetId, seed.RulesetRevision, entities, actors);
        }
    }
}
