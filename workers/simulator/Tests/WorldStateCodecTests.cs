using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class WorldStateCodecTests
    {
        static void Check(bool value,string message){if(!value)throw new Exception("World state codec: "+message);}
        static void Reject(Action action,string message)
        {bool rejected=false;try{action();}catch(JsonException){rejected=true;}catch(ArgumentException){rejected=true;}Check(rejected,message);}
        static double Bits(ulong value)=>BitConverter.Int64BitsToDouble(unchecked((long)value));
        static long Raw(double value)=>BitConverter.DoubleToInt64Bits(value);
        public static void Run()
        {
            ExactBitsAndClone();NegativeContracts();LegacyDigestGuard();CapturedFullWorld();
            Console.WriteLine("World state codec: known Mono one-ULP loss, finite IEEE754 extremes/subnormals/negative-zero, ulong/int/string preservation, malformed/nonfinite rejection, legacy digest refusal and full5105-state roundtrip PASS.");
        }
        static World Fixture()
        {
            var world=new World{Seed=ulong.MaxValue,Tick=17,TopologyVersion=1};
            var e=new Entity{Id="f64:literal-string",ParentId="parent",Kind="fixture",Version=long.MaxValue,X=Bits(0x405009e6cbe6f3c0),Y=Bits(0x8000000000000000),Z=double.Epsilon};
            e.Tags.Add("f64:not-a-number");e.Links.Add("unchanged-link");
            var values=new[]{0.0,Bits(0x8000000000000000),double.Epsilon,-double.Epsilon,double.MaxValue,double.MinValue,Bits(0x405009e6cbe6f3c0),Bits(0x405009e6cbe6f3bf),Bits(0x3fb70a3e20000000),1.0/3};
            for(int i=0;i<values.Length;i++){e.State.Add("value:"+i,values[i]);world.Fields.Add("field:"+i,values[i]);}
            world.Entities.Add(e.Id,e);world.Journal.Add(new Transition{Tick=12,Before=Bits(0x405009e6cbe6f3c0),After=double.Epsilon});return world;
        }
        static void EqualBits(World before,World after)
        {
            Check(before.Seed==after.Seed&&before.Tick==after.Tick&&before.TopologyVersion==after.TopologyVersion&&before.Entities.Count==after.Entities.Count,"world integral fields and topology preserved");
            Check(Raw(before.StepSeconds)==Raw(after.StepSeconds)&&Raw(before.RateMultiplier)==Raw(after.RateMultiplier),"world double scalars exact");
            foreach(var pair in before.Fields)Check(after.Fields.ContainsKey(pair.Key)&&Raw(pair.Value)==Raw(after.Fields[pair.Key]),"every world field binary64 bit exact");
            foreach(var pair in before.Entities)
            {
                var a=pair.Value;var b=after.Entities[pair.Key];Check(a.Id==b.Id&&a.ParentId==b.ParentId&&a.Kind==b.Kind&&a.Version==b.Version&&a.Tags.SequenceEqual(b.Tags)&&a.Links.SequenceEqual(b.Links),"entity strings/integral/set fields preserved");
                Check(Raw(a.X)==Raw(b.X)&&Raw(a.Y)==Raw(b.Y)&&Raw(a.Z)==Raw(b.Z),"every coordinate binary64 bit exact");
                foreach(var value in a.State)Check(b.State.ContainsKey(value.Key)&&Raw(value.Value)==Raw(b.State[value.Key]),"every entity state binary64 bit exact");
            }
            Check(before.Journal.Count==after.Journal.Count,"journal count retained");
            for(int i=0;i<before.Journal.Count;i++)Check(Raw(before.Journal[i].Before)==Raw(after.Journal[i].Before)&&Raw(before.Journal[i].After)==Raw(after.Journal[i].After),"journal numeric state binary64 bit exact");
            Check(before.Digest()==after.Digest(),"same runtime canonical digest retained");
        }
        static void ExactBitsAndClone()
        {
            var world=Fixture();var token=WorldStateCodec.ToToken(world);
            Check((string)token["Entities"]["f64:literal-string"]["X"]=="f64:405009e6cbe6f3c0","known one-ULP source value encoded from original bits");
            Check((string)token["Entities"]["f64:literal-string"]["Y"]=="f64:8000000000000000","negative zero sign retained");
            EqualBits(world,WorldStateCodec.FromToken(JObject.Parse(token.ToString(Formatting.None))));EqualBits(world,WorldStateCodec.Clone(world));
            EqualBits(world,WorldStateCodec.FromToken(JObject.Parse(WorldStateCodec.Serialize(world))));
        }
        static void NegativeContracts()
        {
            foreach(string bad in new[]{"f64:123","f64:000000000000000g","f64:7ff0000000000000","f64:fff0000000000000","f64:7ff8000000000001","64.1547117"})
            {var token=WorldStateCodec.ToToken(Fixture());token["Entities"]["f64:literal-string"]["X"]=bad;Reject(()=>WorldStateCodec.FromToken(token),"malformed/nonfinite/string numeric rejected");}
            var nullNumber=WorldStateCodec.ToToken(Fixture());nullNumber["StepSeconds"]=JValue.CreateNull();Reject(()=>WorldStateCodec.FromToken(nullNumber),"null double rejected");
            Reject(()=>WorldStateCodec.FromToken(new JArray()),"nonobject state rejected");
            var world=Fixture();world.Fields["bad"]=double.NaN;Reject(()=>WorldStateCodec.Serialize(world),"nonfinite write rejected");
        }
        static void LegacyDigestGuard()
        {
            var simple=new World{Seed=5};simple.Fields["simple"]=.5;EqualBits(simple,WorldStateCodec.FromToken(JObject.FromObject(simple)));
            // Minimal reproduction independent of the captured tape. Mono "R" emits
            // 64.1547117 for ...c0, but reads it back as ...bf on the tested toolchain.
            double original=Bits(0x405009e6cbe6f3c0);string numeric=JsonConvert.SerializeObject(original);double legacy=JsonConvert.DeserializeObject<double>(numeric);
            Check(Raw(WorldStateCodec.Clone(Fixture()).Entities["f64:literal-string"].X)==Raw(original),"codec recovers original ...c0, not legacy ...bf");
            if(Raw(legacy)!=Raw(original))Console.WriteLine("Mono numeric regression: "+Raw(original).ToString("x16")+" -> "+numeric+" -> "+Raw(legacy).ToString("x16"));
        }
        static void CapturedFullWorld()
        {
            const string path="output/simulator/platform-ktx-unity/Runs/seed-20261013-20261014T013756412.jsonl";
            if(!File.Exists(path))throw new Exception("Captured native5105-state fixture missing");
            var header=JObject.Parse(File.ReadLines(path).First());var captured=WorldStateCodec.FromToken(header["initial_state"]);
            Check(captured.Entities.Count==5105,"all5105 captured native entities retained");
            // This old decimal tape already lost information. Do not rewrite it or
            // claim its parsed state is the authoritative native in-memory baseline.
            Check(captured.Digest()!=(string)header["initial_digest"],"old lossy tape remains rejected by its existing initial digest guard");
            EqualBits(captured,WorldStateCodec.FromToken(JObject.Parse(WorldStateCodec.Serialize(captured))));
            Console.WriteLine("Captured native5105-state exact codec preserves every parsed baseline bit; original legacy digest remains refused.");
        }
    }
}
