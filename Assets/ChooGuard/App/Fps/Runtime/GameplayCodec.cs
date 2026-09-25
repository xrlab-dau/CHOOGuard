using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Application.Gameplay
{
    /// <summary>Versioned durable records, distinct from the bounded inference wire. No type-name activation.</summary>
    public static class GameplayCodec
    {
        private sealed class SnapshotRecord
        {
            public int schemaVersion;
            public StableId runId;
            public long generation, revision, tickUs, rulesetRevision;
            public GameplayMode mode;
            public string rulesetId;
            public List<WorldEntity> entities;
            public List<ActorState> actors;
        }
        private sealed class MutationRecord
        {
            public int schemaVersion;
            public WorldMutation mutation;
        }
        private static JsonSerializer Serializer()
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Error,
                MaxDepth = 32,
                Culture = CultureInfo.InvariantCulture,
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double
            };
            settings.Converters.Add(new StringEnumConverter { AllowIntegerValues = false });
            settings.Converters.Add(new StableIdConverter());
            settings.Converters.Add(new TickConverter());
            settings.Converters.Add(new LongConverter());
            return JsonSerializer.Create(settings);
        }
        public static string SerializeSnapshot(WorldSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return Encode(new SnapshotRecord
            {
                schemaVersion = 1, runId = snapshot.RunId, generation = snapshot.Generation, revision = snapshot.Revision,
                tickUs = snapshot.Tick.Microseconds, mode = snapshot.Mode, rulesetId = snapshot.RulesetId, rulesetRevision = snapshot.RulesetRevision,
                entities = new List<WorldEntity>(snapshot.Entities.Values), actors = new List<ActorState>(snapshot.Actors.Values)
            });
        }
        public static WorldSnapshot DeserializeSnapshot(string json)
        {
            var record = Decode<SnapshotRecord>(json);
            if (record == null || record.schemaVersion != 1 || record.entities == null || record.actors == null ||
                record.entities.Count == 0 || record.entities.Count > 10000 || record.actors.Count > 2000) throw new InvalidDataException("지원되지 않는 세계 저장 형식입니다.");
            var entities = new Dictionary<StableId, WorldEntity>(); foreach (var entity in record.entities) entities.Add(entity.Id, entity);
            var actors = new Dictionary<StableId, ActorState>(); foreach (var actor in record.actors) actors.Add(actor.Id, actor);
            return new WorldSnapshot(record.runId, record.generation, record.revision, new SimTick(record.tickUs), record.mode,
                record.rulesetId, record.rulesetRevision, entities, actors);
        }
        public static string SerializeMutation(WorldMutation mutation) => Encode(new MutationRecord { schemaVersion = 1, mutation = mutation ?? throw new ArgumentNullException(nameof(mutation)) });
        public static WorldMutation DeserializeMutation(string json)
        {
            var record = Decode<MutationRecord>(json);
            if (record == null || record.schemaVersion != 1 || record.mutation == null) throw new InvalidDataException("지원되지 않는 세계 전이 기록입니다.");
            return record.mutation;
        }
        private static string Encode(object value)
        {
            using (var text = new StringWriter(CultureInfo.InvariantCulture))
            using (var writer = new JsonTextWriter(text) { Formatting = Formatting.None })
            { Serializer().Serialize(writer, value); return text.ToString(); }
        }
        private static T Decode<T>(string json)
        {
            if (json == null || json.Length > 16 * 1024 * 1024) throw new InvalidDataException("세계 저장 크기 제한을 초과했습니다.");
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { MaxDepth = 32, DateParseHandling = DateParseHandling.None, Culture = CultureInfo.InvariantCulture })
            {
                var value = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new InvalidDataException("저장 기록 뒤의 추가 데이터를 거부했습니다.");
                return value.ToObject<T>(Serializer());
            }
        }
        private sealed class StableIdConverter : JsonConverter
        {
            public override bool CanConvert(Type type) => type == typeof(StableId);
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => writer.WriteValue(((StableId)value).Value);
            public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
            { if (reader.TokenType != JsonToken.String) throw new JsonSerializationException("ID 문자열이 필요합니다."); return new StableId((string)reader.Value); }
        }
        private sealed class TickConverter : JsonConverter
        {
            public override bool CanConvert(Type type) => type == typeof(SimTick);
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => writer.WriteValue(((SimTick)value).Microseconds.ToString(CultureInfo.InvariantCulture));
            public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer) => new SimTick(ReadLong(reader));
        }
        private sealed class LongConverter : JsonConverter
        {
            public override bool CanConvert(Type type) => type == typeof(long);
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => writer.WriteValue(((long)value).ToString(CultureInfo.InvariantCulture));
            public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer) => ReadLong(reader);
        }
        private static long ReadLong(JsonReader reader)
        {
            if (reader.TokenType != JsonToken.String || !long.TryParse((string)reader.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ||
                value.ToString(CultureInfo.InvariantCulture) != (string)reader.Value) throw new JsonSerializationException("정규 Int64 문자열이 필요합니다.");
            return value;
        }
    }
}
