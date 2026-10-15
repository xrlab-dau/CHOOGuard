using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class StreamingDigestTests
    {
        static void Check(bool value,string reason){if(!value)throw new Exception("Streaming v2 digest: "+reason);}
        static string Reference(World w)
        {
            var fields=new JArray(w.Fields.Select(f=>new JArray(f.Key,InitialConditionMaterial.FloatBits(f.Value))));
            var entities=new JArray(w.Entities.Values.Select(e=>new JArray(e.Id,e.ParentId,e.Kind,e.Version,InitialConditionMaterial.FloatBits(e.X),InitialConditionMaterial.FloatBits(e.Y),InitialConditionMaterial.FloatBits(e.Z),new JArray(e.Tags),new JArray(e.Links),new JArray(e.State.Select(s=>new JArray(s.Key,InitialConditionMaterial.FloatBits(s.Value)))))));
            return new JArray(World.EngineVersion,w.Seed.ToString(CultureInfo.InvariantCulture),w.Tick,w.TopologyVersion,InitialConditionMaterial.FloatBits(w.StepSeconds),InitialConditionMaterial.FloatBits(w.RateMultiplier),fields,entities).ToString(Formatting.None);
        }
        static void Compare(World w)
        {
            string reference=Reference(w),streamed;using(var text=new StringWriter(CultureInfo.InvariantCulture))using(var writer=new JsonTextWriter(text){Formatting=Formatting.None}){w.WriteCanonical(writer);writer.Flush();streamed=text.ToString();}
            Check(streamed==reference&&w.Digest()==Stable.Hash(reference),"exact canonical JSON text/UTF8/SHA equal old materialized path including every field");
        }
        public static void Run()
        {
            Compare(new World{Seed=ulong.MaxValue});var w=new World{Seed=ulong.MaxValue,Tick=long.MaxValue,RateMultiplier=BitConverter.Int64BitsToDouble(long.MinValue)};
            var e=new Entity{Id="부산|\"\\\n😀",ParentId=null,Kind="null",X=BitConverter.Int64BitsToDouble(unchecked((long)0x405009e6cbe6f3c0UL)),Y=double.Epsilon,Z=-0.0};e.Tags.Add("tab\tquote\"😀");e.Links.Add("null");e.Set("zero",BitConverter.Int64BitsToDouble(long.MinValue));e.Set("max",double.MaxValue);w.Add(e);w.Fields["한글😀"]=.7123456789012345;Compare(w);
            var order=WorldStateCodec.Clone(w);order.Fields.Clear();order.Fields.Add("z",1);order.Fields.Add("a",2);var reverse=WorldStateCodec.Clone(order);reverse.Fields.Clear();reverse.Fields.Add("a",2);reverse.Fields.Add("z",1);Check(order.Digest()==reverse.Digest(),"dictionary insertion order does not alter canonical bytes");
            var captured=JObject.Parse(File.ReadLines("output/simulator/platform-ktx-unity/Runs/seed-20261013-20261014T013756412.jsonl").First());Compare(WorldStateCodec.FromToken(captured["initial_state"]));
            foreach(string kind in new[]{"nan","infinity","surrogate-high","surrogate-low","null-entity"})
            {
                var invalid=new World();if(kind=="nan")invalid.Fields["bad"]=double.NaN;if(kind=="infinity")invalid.StepSeconds=double.PositiveInfinity;if(kind=="surrogate-high")invalid.Fields["\ud800"]=1;if(kind=="surrogate-low")invalid.Fields["\udc00"]=1;if(kind=="null-entity")invalid.Entities.Add("bad",null);
                bool refused=false;try{invalid.Digest();}catch(ArgumentException){refused=true;}Check(refused&&invalid.Tick==0&&invalid.TopologyVersion==0,"malformed/nonfinite input refuses without world writes "+kind);
            }
            Console.WriteLine("Streaming digest canonical JSON/UTF8/SHA equals old v2 path, Unicode/paired-surrogate/escaping/negativezero/ulong/keyorder/empty/full5105; malformed refusal PASS.");
        }
    }
}
