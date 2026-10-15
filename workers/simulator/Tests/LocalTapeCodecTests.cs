using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class LocalTapeCodecTests
    {
        public sealed class MotionDto {public double x,y,z,speed;public bool goal_failed;}
        public sealed class NavigationDto {public float[] position;public double risk;}
        public sealed class TickDto
        {
            public string type="Tick",literal,iso_literal;
            public long tick;public ulong unsigned;
            public JObject literal_object;
            public MotionDto[] motion_inputs;
            public MechanicalInput[] mechanical_inputs;
            public MechanicalOutcome[] mechanical_outcomes;
            public ResponseInput[] response_inputs;
            public ResponseOutcome[] response_outcomes;
            public NavigationDto[] navigation_observations;
            public Transition[] transitions;
            public object nil;
        }
        static long Bits(double value)=>BitConverter.DoubleToInt64Bits(value);
        static int FloatBits(float value)=>BitConverter.ToInt32(BitConverter.GetBytes(value),0);
        static void Check(bool value,string reason){if(!value)throw new Exception("Exact local tape: "+reason);}
        static void Reject(Action action,string reason){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}catch(JsonException){rejected=true;}Check(rejected,reason);}
        public static void Run()
        {
            double known=BitConverter.Int64BitsToDouble(unchecked((long)0x405009e6cbe6f3c0UL)),minus=BitConverter.Int64BitsToDouble(long.MinValue);
            float floatMinus=BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue),0),floatKnown=BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x3f812345)),0);
            var dto=new TickDto{tick=long.MaxValue,unsigned=ulong.MaxValue,literal="f64:405009e6cbe6f3c0",iso_literal="2026-10-14T12:34:56.1234567Z",literal_object=new JObject{["tape_encoding"]=LocalTapeCodec.Encoding,["payload"]=new JArray("float64","f64:405009e6cbe6f3c0")},
                motion_inputs=new[]{new MotionDto{x=known,y=minus,z=minus,speed=known}},mechanical_inputs=new[]{new MechanicalInput{Analog=minus,Duration=known,X=known,AimY=minus}},
                mechanical_outcomes=new[]{new MechanicalOutcome{Before=minus,After=known,EffectAmount=known}},response_inputs=new[]{new ResponseInput{Duration=minus}},response_outcomes=new[]{new ResponseOutcome{Progress=minus}},
                navigation_observations=new[]{new NavigationDto{position=new[]{floatKnown,floatMinus},risk=known}},transitions=new[]{new Transition{Before=minus,After=known}}};
            var row=(JObject)LocalTapeCodec.FromObject(dto);
            var encoded=LocalTapeCodec.Encode(row);var decoded=LocalTapeCodec.Parse(encoded.ToString(Formatting.None));
            var restored=LocalTapeCodec.ToObject<TickDto>(decoded);
            Check(Bits(LocalTapeCodec.Number(row["motion_inputs"][0]["y"]))==long.MinValue&&Bits(LocalTapeCodec.Number(decoded["motion_inputs"][0]["y"]))==long.MinValue,"actual CLR motion ingress and token decoder preserve negative zero");
            Check(Bits(restored.motion_inputs[0].x)==Bits(known)&&Bits(restored.motion_inputs[0].y)==long.MinValue,"known Mono one-ULP value and negative zero survive actual Tick DTO write/read");
            Check(Bits(restored.mechanical_inputs[0].Analog)==long.MinValue&&Bits(restored.mechanical_inputs[0].Duration)==Bits(known)&&Bits(restored.mechanical_inputs[0].AimY)==long.MinValue,"actual MechanicalInput double fields survive exact DTO reader");
            Check(Bits(restored.response_inputs[0].Duration)==long.MinValue&&Bits(restored.response_outcomes[0].Progress)==long.MinValue,"actual ResponseInput/Outcome doubles preserve sign");
            Check(Bits(restored.transitions[0].Before)==long.MinValue&&Bits(restored.transitions[0].After)==Bits(known),"actual Transition doubles preserve sign and one-ULP boundary");
            Check(FloatBits(restored.navigation_observations[0].position[0])==FloatBits(floatKnown)&&FloatBits(restored.navigation_observations[0].position[1])==int.MinValue&&Bits(restored.navigation_observations[0].risk)==Bits(known),"Vector3-like float32 position widens to float64 and narrows bit-exactly including negative zero");
            Check(Bits(LocalTapeCodec.Number(dto.mechanical_outcomes[0].Json()["Before"]))==long.MinValue&&Bits(LocalTapeCodec.Number(dto.response_outcomes[0].Json()["Progress"]))==long.MinValue,"production outcome Json producers preserve bits before host receives tokens");
            Check(restored.unsigned==ulong.MaxValue&&restored.tick==long.MaxValue,"integer type boundaries survive actual DTO reader");
            string tapePath=Path.GetTempFileName();
            try
            {
                File.WriteAllLines(tapePath,new[]{LocalTapeCodec.Encode(new JObject{["type"]="RunHeader"}).ToString(Formatting.None),encoded.ToString(Formatting.None)});
                var ticks=File.ReadAllLines(tapePath).Select(LocalTapeCodec.Parse).Where(l=>(string)l["type"]=="Tick").ToArray();
                Check(ticks.Length==1&&Bits(LocalTapeCodec.ToObject<TickDto>(ticks[0]).transitions[0].Before)==long.MinValue,"production acceptance reader pipeline finds decoded Tick and preserves actual DTO bits from disk");
            }
            finally{File.Delete(tapePath);}
            var alreadyLost=new JObject{["z"]=minus};Check(Bits(LocalTapeCodec.Number(LocalTapeCodec.FromObject(alreadyLost)["z"]))==Bits(LocalTapeCodec.Number(alreadyLost["z"])),"JToken passthrough copies the observed value and never claims recovery of prior lossy ingress");
            Check(LocalTapeCodec.Encode(decoded).ToString(Formatting.None)==encoded.ToString(Formatting.None),"all Tick arrays/objects/strings/integer/bool/null have exact stable encoded roundtrip");
            Check(decoded["iso_literal"].Type==JTokenType.String&&(string)decoded["iso_literal"]==(string)row["iso_literal"],"ISO literal remains string without automatic date parse");
            Check((string)decoded["literal"]==(string)row["literal"]&&JToken.DeepEquals(row["literal_object"],decoded["literal_object"]),"literal marker text/object is never interpreted as a typed float");
            var world=new World{Seed=7,RateMultiplier=.7123456789012345};var entity=new Entity{Id="test",X=known,Z=minus};world.Add(entity);
            var header=new JObject{["type"]="RunHeader",["initial_state"]=WorldStateCodec.ToToken(world),["state_encoding"]=WorldStateCodec.Encoding,["config"]=new JObject{["step_seconds"]=InitialConditionMaterial.FloatBits(world.StepSeconds),["rate_multiplier"]=InitialConditionMaterial.FloatBits(world.RateMultiplier)}};
            var read=LocalTapeCodec.Parse(LocalTapeCodec.Encode(header).ToString());var saved=WorldStateCodec.FromToken(read["initial_state"]);ReplayConfiguration.Validate(read["config"],saved);Check(saved.Digest()==world.Digest(),"exact state/config and tape envelope compose without double-encoding literals");
            var emergence=new EmergenceRecord{Changes=new System.Collections.Generic.List<EmergenceChange>{new EmergenceChange{EntityId="test",Property="wetness",Value=minus},new EmergenceChange{EntityId="test",Property="temperature",Value=known}}};
            var savedEmergence=LocalTapeCodec.ToObject<EmergenceRecord>(LocalTapeCodec.Parse(LocalTapeCodec.Encode((JObject)LocalTapeCodec.FromObject(emergence)).ToString(Formatting.None)));
            Check(Bits(savedEmergence.Changes[0].Value)==long.MinValue&&Bits(savedEmergence.Changes[1].Value)==Bits(known),"actual RunHeader initial material record DTO preserves bits across its property converter");
            var bad=JObject.Parse(encoded.ToString());bad["tape_encoding"]="other-version";Reject(()=>LocalTapeCodec.Decode(bad),"unknown tape version refused");
            foreach(JToken payload in new JToken[]{new JArray("unknown",1),new JArray("float64","f64:7ff0000000000000"),new JArray("float64","f64:123"),new JArray("integer","01"),new JArray("integer","18446744073709551616"),new JArray("array",new JArray(new JArray("null",1)))})
                Reject(()=>LocalTapeCodec.Decode(new JObject{["tape_encoding"]=LocalTapeCodec.Encoding,["payload"]=payload}),"malformed/truncated typed tree refused");
            Reject(()=>LocalTapeCodec.Decode(new JObject{["tape_encoding"]=LocalTapeCodec.Encoding}),"missing payload refused");
            Reject(()=>LocalTapeCodec.Decode(row),"legacy raw floating tape never silently promoted");
            var parsedDate=JObject.Parse("{\"created_at\":\"2026-10-14T12:34:56Z\"}");Check(LocalTapeCodec.Parse(LocalTapeCodec.Encode(parsedDate).ToString())["created_at"].Type==JTokenType.String,"already parsed metadata date is serialized as JSON string");
            Reject(()=>LocalTapeCodec.Parse(encoded.ToString().Substring(0,10)),"truncated JSON line refused");
            var nonfinite=new JObject{["bad"]=double.NaN};Reject(()=>LocalTapeCodec.Encode(nonfinite),"nonfinite local trace cannot be written");
            Console.WriteLine("FullTickDTO exact-f64/signedzero/literal collision/ulong64/stateconfig composition and malformed version guards PASS; model JSON unchanged.");
        }
    }
}
