using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace ChooGuard.Seeded
{
    // Local tape only. Typed nodes preserve literal marker strings and objects.
    // Model/provider/native-control JSON never passes through this codec.
    public static class LocalTapeCodec
    {
        public const string Encoding="local-exact-json-tree-v1";
        static readonly IContractResolver Contracts=new DefaultContractResolver();
        static void Require(bool value){if(!value)throw new JsonSerializationException("Malformed local tape encoding");}
        // JValue(double) in the installed Mono/Json.NET normalizes -0. Boxed
        // numeric values keep their bits. This must run at the first DTO boundary;
        // an already lossy JToken cannot have its original bits recovered.
        public static JValue Float(double value)
        {Require(!double.IsNaN(value)&&!double.IsInfinity(value));return new JValue((object)value);}
        public static double Number(JToken token)
        {
            var value=(token as JValue)?.Value;
            if(value is double d)return d;
            if(value is float f)return (double)f;
            return WorldStateCodec.ReadExactNumber(token);
        }
        public static JToken FromObject(object value)=>ObjectNode(value,0);
        static JToken ObjectNode(object value,int depth)
        {
            Require(depth<=128);if(value==null)return JValue.CreateNull();
            if(value is JToken token)
            {
                if(token is JObject obj){var copy=new JObject();foreach(var p in obj.Properties())copy.Add(p.Name,ObjectNode(p.Value,depth+1));return copy;}
                if(token is JArray arr)return new JArray(arr.Select(x=>ObjectNode(x,depth+1)));
                if(token.Type==JTokenType.Float)return Float(Number(token));
                return token.DeepClone();
            }
            if(value is double d)return Float(d);
            if(value is float f)return Float((double)f);
            if(value is string||value is char||value is bool)return new JValue(value);
            var type=value.GetType();
            if(type.IsEnum)return new JValue(Convert.ToInt64(value,CultureInfo.InvariantCulture));
            if(value is byte||value is sbyte||value is short||value is ushort||value is int||value is uint||value is long||value is ulong)return new JValue(value);
            if(value is DateTime||value is DateTimeOffset||value is Guid)return new JValue(JsonConvert.DeserializeObject<string>(JsonConvert.SerializeObject(value)));
            if(value is IDictionary dictionary){var obj=new JObject();foreach(DictionaryEntry item in dictionary){Require(item.Key is string);obj.Add((string)item.Key,ObjectNode(item.Value,depth+1));}return obj;}
            if(value is IEnumerable sequence){var arr=new JArray();foreach(var item in sequence)arr.Add(ObjectNode(item,depth+1));return arr;}
            var contract=Contracts.ResolveContract(type) as JsonObjectContract;Require(contract!=null);var result=new JObject();
            foreach(var p in contract.Properties.Where(p=>p.Readable&&!p.Ignored))
                if((p.ShouldSerialize==null||p.ShouldSerialize(value))&&(p.GetIsSpecified==null||p.GetIsSpecified(value)))result.Add(p.PropertyName,ObjectNode(p.ValueProvider.GetValue(value),depth+1));
            return result;
        }
        public static T ToObject<T>(JToken token)
        {var serializer=new JsonSerializer();serializer.Converters.Add(new TraceNumberReader());return token.ToObject<T>(serializer);}
        sealed class TraceNumberReader:JsonConverter
        {
            public override bool CanConvert(Type type)=>type==typeof(double)||type==typeof(float);
            public override bool CanWrite=>false;
            public override void WriteJson(JsonWriter writer,object value,JsonSerializer serializer)=>throw new NotSupportedException();
            public override object ReadJson(JsonReader reader,Type type,object existing,JsonSerializer serializer)
            {
                Require(reader.TokenType==JsonToken.Float||reader.TokenType==JsonToken.Integer);double value=reader.Value is double d?d:reader.Value is float f?(double)f:Convert.ToDouble(reader.Value,CultureInfo.InvariantCulture);
                Require(!double.IsNaN(value)&&!double.IsInfinity(value));if(type==typeof(float)){float narrowed=(float)value;Require(!float.IsInfinity(narrowed));return narrowed;}return value;
            }
        }
        public static JObject Encode(JObject row)
        {if(row==null)throw new ArgumentNullException(nameof(row));return new JObject{["tape_encoding"]=Encoding,["payload"]=Node(row)};}
        public static JObject Parse(string line)=>Decode(ParseJsonData(line));
        public static JObject ParseJsonData(string json)
        {
            using(var reader=new JsonTextReader(new StringReader(json??"")){DateParseHandling=DateParseHandling.None})
            {var result=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});Require(!reader.Read());return result;}
        }
        static JArray Node(JToken token)
        {
            switch(token.Type)
            {
                case JTokenType.Object:return new JArray("object",new JArray(((JObject)token).Properties().Select(p=>new JArray(p.Name,Node(p.Value)))));
                case JTokenType.Array:return new JArray("array",new JArray(token.Select(Node)));
                case JTokenType.String:return new JArray("string",(string)token);
                case JTokenType.Boolean:return new JArray("boolean",(bool)token);
                case JTokenType.Integer:return new JArray("integer",token.ToString(Formatting.None));
                case JTokenType.Float:return new JArray("float64",InitialConditionMaterial.FloatBits(Number(token)));
                case JTokenType.Null:return new JArray("null");
                // JSON has no date primitive. Already parsed local metadata dates are
                // written as their JSON string representation; raw literals use ParseJsonData.
                case JTokenType.Date:return new JArray("string",JsonConvert.DeserializeObject<string>(token.ToString(Formatting.None)));
                default:throw new JsonSerializationException("Unsupported local tape token");
            }
        }
        public static JObject Decode(JObject envelope)
        {
            Require(envelope!=null&&envelope.Properties().Count()==2&&(string)envelope["tape_encoding"]==Encoding&&envelope["payload"] is JArray);
            var decoded=Value(envelope["payload"],0) as JObject;Require(decoded!=null);return decoded;
        }
        static JToken Value(JToken token,int depth)
        {
            var a=token as JArray;Require(a!=null&&depth<=128&&a.Count>=1&&a[0].Type==JTokenType.String);
            string kind=(string)a[0];if(kind=="null"){Require(a.Count==1);return JValue.CreateNull();}
            Require(a.Count==2);
            switch(kind)
            {
                case "object":
                    Require(a[1] is JArray);var result=new JObject();
                    foreach(var pair in a[1]){var p=pair as JArray;Require(p!=null&&p.Count==2&&p[0].Type==JTokenType.String&&result.Property((string)p[0],StringComparison.Ordinal)==null);result.Add((string)p[0],Value(p[1],depth+1));}return result;
                case "array":Require(a[1] is JArray);return new JArray(a[1].Select(v=>Value(v,depth+1)));
                case "string":Require(a[1].Type==JTokenType.String);return new JValue((string)a[1]);
                case "boolean":Require(a[1].Type==JTokenType.Boolean);return new JValue((bool)a[1]);
                case "integer":
                    Require(a[1].Type==JTokenType.String);string text=(string)a[1];long signed;ulong unsigned;
                    if(long.TryParse(text,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out signed)&&signed.ToString(CultureInfo.InvariantCulture)==text)return new JValue(signed);
                    if(ulong.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out unsigned)&&unsigned.ToString(CultureInfo.InvariantCulture)==text)return new JValue(unsigned);
                    throw new JsonSerializationException("Out-of-range local tape integer");
                case "float64":Require(a[1].Type==JTokenType.String);return Float(WorldStateCodec.ReadExactNumber(a[1]));
                default:throw new JsonSerializationException("Unknown local tape token type");
            }
        }
    }
}
