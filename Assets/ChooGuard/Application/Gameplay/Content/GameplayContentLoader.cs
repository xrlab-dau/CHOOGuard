using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using ChooGuard.Domain.Gameplay;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Application.Gameplay.Content
{
    public sealed class GameplaySeed
    {
        public string Id { get; internal set; }
        public string Label { get; internal set; }
        public string RulesetId { get; internal set; }
        public long RulesetRevision { get; internal set; }
        public StableId ZoneId { get; internal set; }
        public WorldPoint HumanPosition { get; internal set; }
        public WorldPoint PopulationOrigin { get; internal set; }
        public int PopulationColumns { get; internal set; }
        public float PopulationSpacing { get; internal set; }
        public IReadOnlyList<WorldEntity> Entities { get; internal set; }
        public IReadOnlyList<GameplayActorSeed> Actors { get; internal set; }
        public IReadOnlyList<GameplayPopulationActor> NamedActors { get; internal set; }
        public IReadOnlyDictionary<string, string> ParameterReferences { get; internal set; }
    }

    public sealed class GameplayActorSeed
    {
        public ActorRole Role { get; internal set; }
        public string Persona { get; internal set; }
        public GoalKind Goal { get; internal set; }
        public StableId TargetId { get; internal set; }
        public string CompletionFact { get; internal set; }
        public IReadOnlyDictionary<GoalKind, double> Drives { get; internal set; }
    }

    public sealed class GameplayPopulationActor
    {
        public StableId Id { get; internal set; }
        public WorldPoint Position { get; internal set; }
        public int TemplateIndex { get; internal set; }
    }

    public sealed class GameplayContent
    {
        public GameplaySeed Seed { get; internal set; }
        public IReadOnlyList<TransitionDefinition> Transitions { get; internal set; }
    }

    /// <summary>Shared Unity/learning-host content boundary. Missing facts remain unknown; only authored booleans are accepted.</summary>
    public static class GameplayContentLoader
    {
        public static GameplayContent Load(string seedJson, string transitionJson)
        {
            var root = JObject.Parse(seedJson ?? throw new ArgumentNullException(nameof(seedJson)));
            var rules = JObject.Parse(transitionJson ?? throw new ArgumentNullException(nameof(transitionJson)));
            if (RequiredInt(root, "version") != 1 || RequiredInt(rules, "version") != 1) throw new FormatException("Unsupported gameplay content version.");
            if (root.Value<bool?>("syntheticPractice") != true) throw new FormatException("This seed loader requires explicitly labelled synthetic practice content.");
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var token in RequiredArray(root, "parameters"))
            {
                var parameter = (JObject)token;
                var id = RequiredString(parameter, "id"); new StableId(id);
                parameters.Add(id, RequiredString(parameter, "source"));
            }
            var seed = new GameplaySeed
            {
                Id = RequiredString(root, "id"), Label = RequiredString(root, "label"),
                RulesetId = RequiredString(root, "rulesetId"), RulesetRevision = RequiredInt(root, "rulesetRevision"),
                ZoneId = new StableId(RequiredString(root, "zoneId")), HumanPosition = Point(root["humanPosition"]),
                ParameterReferences = new ReadOnlyDictionary<string, string>(parameters)
            };
            new StableId(seed.Id); new StableId(seed.RulesetId);
            var population = (JObject)root["population"] ?? throw new FormatException("population is required");
            seed.PopulationOrigin = Point(population["origin"]); seed.PopulationColumns = RequiredInt(population, "columns");
            seed.PopulationSpacing = population.Value<float?>("spacing") ?? throw new FormatException("population spacing is required");
            if (seed.PopulationColumns <= 0 || float.IsNaN(seed.PopulationSpacing) || float.IsInfinity(seed.PopulationSpacing) || seed.PopulationSpacing <= 0)
                throw new FormatException("Invalid authored population layout.");
            var entities = new List<WorldEntity>(); var ids = new HashSet<StableId>();
            foreach (var token in RequiredArray(root, "entities"))
            {
                var entity = (JObject)token; var id = new StableId(RequiredString(entity, "id"));
                if (!ids.Add(id)) throw new FormatException("Duplicate entity: " + id.Value);
                var measurements = new Dictionary<string, SiValue>(StringComparer.Ordinal);
                if (entity["measurements"] is JObject values)
                    foreach (var pair in values)
                    {
                        var value = (JObject)pair.Value;
                        measurements.Add(pair.Key, new SiValue(value.Value<double?>("value") ?? throw new FormatException("Measurement value missing"), ParseEnum<SiUnit>(RequiredString(value, "unit"))));
                    }
                entities.Add(new WorldEntity(id, RequiredString(entity, "label"), ParseEnum<EntityKind>(RequiredString(entity, "kind")), 0,
                    Point(entity["position"]), new StableId(RequiredString(entity, "zoneId")),
                    OptionalId(entity, "custodianId"), OptionalId(entity, "parentId"), RequiredString(entity, "definitionId"), Facts(entity["facts"]), measurements));
            }
            if (!ids.Contains(seed.ZoneId)) throw new FormatException("Seed zone entity is absent.");
            foreach (var entity in entities)
            {
                if (!ids.Contains(entity.ZoneId)) throw new FormatException("Unknown zone: " + entity.ZoneId.Value);
                if (entity.ParentId.HasValue && !ids.Contains(entity.ParentId.Value)) throw new FormatException("Unknown parent: " + entity.ParentId.Value.Value);
            }
            seed.Entities = entities.AsReadOnly();
            var actors = new List<GameplayActorSeed>();
            foreach (var token in RequiredArray(root, "actorTemplates"))
            {
                var actor = (JObject)token; var drives = new Dictionary<GoalKind, double>();
                foreach (var pair in (JObject)actor["drives"] ?? throw new FormatException("Actor drives missing"))
                {
                    double drive = pair.Value.Value<double>();
                    if (double.IsNaN(drive) || double.IsInfinity(drive) || drive < 0 || drive > 1) throw new FormatException("Invalid actor drive.");
                    drives.Add(ParseEnum<GoalKind>(pair.Key), drive);
                }
                var target = new StableId(RequiredString(actor, "targetId"));
                if (!ids.Contains(target)) throw new FormatException("Actor assignment target absent: " + target.Value);
                actors.Add(new GameplayActorSeed { Role = ParseEnum<ActorRole>(RequiredString(actor, "role")), Persona = RequiredString(actor, "persona"),
                    Goal = ParseEnum<GoalKind>(RequiredString(actor, "goal")), TargetId = target, CompletionFact = actor.Value<string>("completionFact"),
                    Drives = new ReadOnlyDictionary<GoalKind, double>(drives) });
            }
            if (actors.Count == 0) throw new FormatException("At least one authored actor template is required.");
            seed.Actors = actors.AsReadOnly();
            var named = new List<GameplayPopulationActor>();
            if (population["namedActors"] is JArray namedActors)
                foreach (var token in namedActors)
                {
                    var person = (JObject)token;
                    int template = RequiredInt(person, "templateIndex");
                    var id = new StableId(RequiredString(person, "id"));
                    if (template < 0 || template >= actors.Count || id.Equals(GameplayInitialWorld.HumanId) || !ids.Add(id))
                        throw new FormatException("Invalid named population actor.");
                    named.Add(new GameplayPopulationActor { Id = id, Position = Point(person["position"]), TemplateIndex = template });
                }
            seed.NamedActors = named.AsReadOnly();
            if (RequiredString(rules, "rulesetId") != seed.RulesetId || RequiredInt(rules, "rulesetRevision") != seed.RulesetRevision)
                throw new FormatException("Seed and transition ruleset revisions differ.");
            var transitions = new List<TransitionDefinition>(); var transitionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in RequiredArray(rules, "transitions"))
            {
                var rule = (JObject)token; string id = RequiredString(rule, "id"), parameter = RequiredString(rule, "parameterSetId");
                if (!transitionIds.Add(id) || !parameters.ContainsKey(parameter)) throw new FormatException("Duplicate transition or unknown parameter reference: " + id);
                transitions.Add(new TransitionDefinition(id, parameter, RequiredString(rule, "label"), ParseEnum<EntityKind>(RequiredString(rule, "sourceKind")),
                    ParseEnum<EntityKind>(RequiredString(rule, "targetKind")), ParseEnum<TransitionRelation>(RequiredString(rule, "relation")),
                    Facts(rule["sourceConditions"]), Facts(rule["targetConditions"]), Facts(rule["effects"])));
            }
            return new GameplayContent { Seed = seed, Transitions = transitions.AsReadOnly() };
        }
        private static string RequiredString(JObject value, string key)
        {
            if (!(value[key] is JValue token) || token.Type != JTokenType.String || string.IsNullOrWhiteSpace(token.Value<string>())) throw new FormatException(key + " must be a nonempty string.");
            return token.Value<string>();
        }
        private static int RequiredInt(JObject value, string key)
        {
            if (value[key]?.Type != JTokenType.Integer) throw new FormatException(key + " must be an integer.");
            return value.Value<int>(key);
        }
        private static JArray RequiredArray(JObject value, string key) => value[key] as JArray ?? throw new FormatException(key + " must be an array.");
        private static StableId? OptionalId(JObject value, string key) => value[key] == null ? (StableId?)null : new StableId(RequiredString(value, key));
        private static T ParseEnum<T>(string text) where T : struct
        {
            if (!Enum.TryParse(text, false, out T value) || !Enum.IsDefined(typeof(T), value)) throw new FormatException("Unsupported " + typeof(T).Name + ": " + text);
            return value;
        }
        private static WorldPoint Point(JToken token)
        {
            if (!(token is JArray point) || point.Count != 3) throw new FormatException("Position must contain three metre coordinates.");
            return new WorldPoint(point[0].Value<float>(), point[1].Value<float>(), point[2].Value<float>());
        }
        private static Dictionary<string, RuleTruth> Facts(JToken token)
        {
            var result = new Dictionary<string, RuleTruth>(StringComparer.Ordinal);
            if (token == null) return result;
            if (!(token is JObject facts)) throw new FormatException("Facts must be an object.");
            foreach (var pair in facts)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value.Type != JTokenType.Boolean) throw new FormatException("Only explicitly confirmed boolean facts may be authored.");
                result.Add(pair.Key, pair.Value.Value<bool>() ? RuleTruth.TRUE : RuleTruth.FALSE);
            }
            return result;
        }
    }
}
