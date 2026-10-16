using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    /// <summary>
    /// Exact local state/tape codec. Mono's double "R" text can lose a bit on JSON
    /// readback; binary64 values are therefore tagged hexadecimal bit strings.
    /// This is scoped to complete saved World values, never general model JSON.
    /// </summary>
    public static class WorldStateCodec
    {
        public const string Encoding="ieee754-f64-hex-v1";
        public static JToken ToToken(World world)
        {
            if(world==null)throw new ArgumentNullException(nameof(world));
            return JToken.FromObject(world,Serializer());
        }
        public static World FromToken(JToken token)
        {
            if(token==null||token.Type!=JTokenType.Object)throw new JsonSerializationException("World state object required");
            var world=token.ToObject<World>(Serializer());
            if(world==null)throw new JsonSerializationException("World state is missing");
            return world;
        }
        public static World Clone(World world)=>FromToken(ToToken(world));
        public static string Serialize(World world)=>ToToken(world).ToString(Formatting.None);
        public static double ReadExactNumber(JToken token)
        {if(token==null||token.Type==JTokenType.Null)throw new JsonSerializationException("Exact numeric value required");return token.ToObject<double>(Serializer());}
        static JsonSerializer Serializer()
        {
            var serializer=new JsonSerializer();serializer.Converters.Add(new ExactDoubleConverter());return serializer;
        }
        public sealed class ExactDoubleConverter:JsonConverter
        {
            public override bool CanConvert(Type type)=>type==typeof(double);
            public override void WriteJson(JsonWriter writer,object value,JsonSerializer serializer)
            {
                double number=(double)value;Finite(number);
                ulong bits=unchecked((ulong)BitConverter.DoubleToInt64Bits(number));
                writer.WriteValue("f64:"+bits.ToString("x16",CultureInfo.InvariantCulture));
            }
            public override object ReadJson(JsonReader reader,Type type,object existingValue,JsonSerializer serializer)
            {
                double number;
                if(reader.TokenType==JsonToken.String)
                {
                    string text=reader.Value as string;
                    if(text==null||text.Length!=20||!text.StartsWith("f64:",StringComparison.Ordinal))throw new JsonSerializationException("Expected f64 followed by exactly sixteen hexadecimal digits");
                    ulong bits;
                    if(!ulong.TryParse(text.Substring(4),NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out bits))throw new JsonSerializationException("Invalid binary64 bit string");
                    number=BitConverter.Int64BitsToDouble(unchecked((long)bits));
                }
                else if(reader.TokenType==JsonToken.Float||reader.TokenType==JsonToken.Integer)
                {
                    // Legacy numeric tokens remain readable. Their caller must still
                    // validate the recorded digest; lost old bits cannot be recovered.
                    number=Convert.ToDouble(reader.Value,CultureInfo.InvariantCulture);
                }
                else throw new JsonSerializationException("Finite numeric or tagged binary64 state required");
                Finite(number);return number;
            }
            static void Finite(double value)
            {if(double.IsNaN(value)||double.IsInfinity(value))throw new JsonSerializationException("Nonfinite world state is not recordable");}
        }
    }
}
