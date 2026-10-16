using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    // Engine-independent state. Tags are explicit capabilities, never guessed from mesh names.
    public sealed class Entity
    {
        public string Id, ParentId, Kind;
        public double X, Y, Z;
        public long Version;
        public readonly SortedSet<string> Tags = new SortedSet<string>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, double> State = new SortedDictionary<string, double>(StringComparer.Ordinal);
        public readonly SortedSet<string> Links = new SortedSet<string>(StringComparer.Ordinal);
        public double Get(string key, double fallback = 0) { double value; return State.TryGetValue(key, out value) ? value : fallback; }
        public bool Has(string tag) { return Tags.Contains(tag); }
        public void Set(string key, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("Non-finite state");
            if (!State.ContainsKey(key) || BitConverter.DoubleToInt64Bits(State[key]) != BitConverter.DoubleToInt64Bits(value))
            {
                if(Version<0||Version==long.MaxValue)throw new ArgumentException("Entity version cannot advance");
                long next=checked(Version+1);State[key]=value;Version=next;
            }
        }
        public double Distance(Entity other) { double x=X-other.X, y=Y-other.Y, z=Z-other.Z; return Math.Sqrt(x*x+y*y+z*z); }
    }

    public sealed class World
    {
        public const string EngineVersion = "chooguard-causal-v2";
        public ulong Seed;
        public long Tick, TopologyVersion;
        public double StepSeconds = 0.5, RateMultiplier = 1;
        public readonly SortedDictionary<string, Entity> Entities = new SortedDictionary<string, Entity>(StringComparer.Ordinal);
        public readonly List<Transition> Journal = new List<Transition>();
        public readonly SortedDictionary<string, double> Fields = new SortedDictionary<string, double>(StringComparer.Ordinal);
        public double Field(string key) { double value; return Fields.TryGetValue(key, out value) ? value : 0; }
        public void Add(Entity entity)
        {
            if (entity == null || string.IsNullOrEmpty(entity.Id) || Entities.ContainsKey(entity.Id)) throw new ArgumentException("Unique stable entity ID required");
            Entities.Add(entity.Id, entity); TopologyVersion++;
        }
        public string Digest()
        {
            // Same v2 canonical bytes, incrementally hashed: no full token tree,
            // giant JSON string or duplicated UTF8 byte array per crowd tick.
            using(var sha=SHA256.Create())using(var sink=new CryptoStream(Stream.Null,sha,CryptoStreamMode.Write))
            using(var text=new StreamWriter(sink,new UTF8Encoding(false,true),4096,true))
            using(var writer=new JsonTextWriter(text){Formatting=Formatting.None,CloseOutput=false})
            {WriteCanonical(writer);writer.Flush();text.Flush();sink.FlushFinalBlock();return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();}
        }
        public void WriteCanonical(JsonWriter writer)
        {
            if(writer==null)throw new ArgumentNullException(nameof(writer));
            writer.WriteStartArray();Text(writer,EngineVersion);Text(writer,Seed.ToString(CultureInfo.InvariantCulture));writer.WriteValue(Tick);writer.WriteValue(TopologyVersion);Text(writer,InitialConditionMaterial.FloatBits(StepSeconds));Text(writer,InitialConditionMaterial.FloatBits(RateMultiplier));
            writer.WriteStartArray();foreach(var field in Fields){writer.WriteStartArray();Text(writer,field.Key);Text(writer,InitialConditionMaterial.FloatBits(field.Value));writer.WriteEndArray();}writer.WriteEndArray();
            writer.WriteStartArray();foreach(var e in Entities.Values)
            {
                if(e==null)throw new ArgumentException("Null world entity is not hashable");
                writer.WriteStartArray();Text(writer,e.Id);Text(writer,e.ParentId);Text(writer,e.Kind);writer.WriteValue(e.Version);Text(writer,InitialConditionMaterial.FloatBits(e.X));Text(writer,InitialConditionMaterial.FloatBits(e.Y));Text(writer,InitialConditionMaterial.FloatBits(e.Z));
                writer.WriteStartArray();foreach(var tag in e.Tags)Text(writer,tag);writer.WriteEndArray();writer.WriteStartArray();foreach(var link in e.Links)Text(writer,link);writer.WriteEndArray();
                writer.WriteStartArray();foreach(var state in e.State){writer.WriteStartArray();Text(writer,state.Key);Text(writer,InitialConditionMaterial.FloatBits(state.Value));writer.WriteEndArray();}writer.WriteEndArray();writer.WriteEndArray();
            }
            writer.WriteEndArray();writer.WriteEndArray();
        }
        static void Text(JsonWriter writer,string value)
        {
            // JSON/UTF8 cannot represent a lone UTF16 surrogate as an identity.
            if(value!=null)for(int i=0;i<value.Length;i++)
                if(char.IsHighSurrogate(value[i])){if(i+1>=value.Length||!char.IsLowSurrogate(value[++i]))throw new ArgumentException("Malformed UTF16 world text");}
                else if(char.IsLowSurrogate(value[i]))throw new ArgumentException("Malformed UTF16 world text");
            writer.WriteValue(value);
        }
    }

    public sealed class Transition
    {
        public long Tick;
        public string Domain, Mechanism, Source, Target, Property, Reason;
        public double Before, After;
        public override string ToString() { return Tick+"|"+Domain+"|"+Mechanism+"|"+Source+"|"+Target+"|"+Property+"|"+Before.ToString("R",CultureInfo.InvariantCulture)+"|"+After.ToString("R",CultureInfo.InvariantCulture); }
    }

    public static class Stable
    {
        public const string RandomVersion = "sha256-counter-v2";
        public static string Hash(string text)
        {
            using(var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        public static double Unit(ulong seed, string stream, long tick, string id, int draw = 0)
        {
            byte[] bytes;
            if(string.IsNullOrEmpty(stream)||string.IsNullOrEmpty(id)||tick<0)throw new ArgumentException("Explicit RNG stream/entity and nonnegative tick required");
            using(var h = SHA256.Create()) bytes = h.ComputeHash(Encoding.UTF8.GetBytes(new JArray(RandomVersion,seed.ToString(CultureInfo.InvariantCulture),stream,tick,id,draw).ToString(Formatting.None)));
            ulong n = 0; for(int i=0;i<7;i++) n=(n<<8)|bytes[i];
            return (n >> 3) * (1.0 / 9007199254740992.0);
        }
        public static bool Occurs(World world, string stream, string id, double ratePerSecond)
        {
            if(world==null||double.IsNaN(ratePerSecond)||double.IsInfinity(ratePerSecond)||double.IsNaN(world.StepSeconds)||double.IsInfinity(world.StepSeconds)||world.StepSeconds<=0||double.IsNaN(world.RateMultiplier)||double.IsInfinity(world.RateMultiplier)||world.RateMultiplier<0)
                throw new ArgumentException("Finite occurrence inputs required");
            return ratePerSecond > 0 && Unit(world.Seed,stream,world.Tick,id) < 1-Math.Exp(-ratePerSecond*world.StepSeconds*world.RateMultiplier);
        }
    }
}
