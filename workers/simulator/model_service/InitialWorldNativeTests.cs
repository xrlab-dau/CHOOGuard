using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ChooGuard.Seeded;

namespace ChooGuard.Seeded.Tests
{
    public static class InitialWorldNativeTests
    {
        static void Check(bool value,string reason){if(!value)throw new InvalidOperationException("Local tape consumer: "+reason);}
        public static void Run()
        {
            string directory=Path.Combine(Path.GetTempPath(),"chooguard-tape-consumer-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try
            {
                string path=Path.Combine(directory,"fixture.jsonl");double precise=BitConverter.Int64BitsToDouble(unchecked((long)0x3fe278e93a3bfa76UL));double negativeZero=BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000UL));
                float floatZero=BitConverter.ToSingle(BitConverter.GetBytes(0x80000000U),0);
                var original=new {type="RunHeader",literal_date="2026-10-14T12:34:56Z",literal_marker="f64:3ff0000000000000",exact_scalar=precise,signed_zero=negativeZero,float_signed_zero=floatZero,unsigned_counter=ulong.MaxValue,marker_object=new {float64="ordinary-text"}};
                var header=(JObject)LocalTapeCodec.FromObject(original);
                Check(BitConverter.DoubleToInt64Bits(LocalTapeCodec.Number(header["signed_zero"]))==BitConverter.DoubleToInt64Bits(negativeZero)&&BitConverter.DoubleToInt64Bits(LocalTapeCodec.Number(header["float_signed_zero"]))==BitConverter.DoubleToInt64Bits(negativeZero),"actual production CLR DTO builder preserves double and float signedzero at the first token boundary");
                File.WriteAllText(path,LocalTapeCodec.Encode(header).ToString(Formatting.None)+"\n"+LocalTapeCodec.Encode(new JObject{["type"]="Tick"}).ToString(Formatting.None)+"\n");
                var read=InitialWorldNative.Load(path);
                Check((string)read["type"]=="RunHeader"&&read["literal_date"].Type==JTokenType.String&&(string)read["literal_date"]=="2026-10-14T12:34:56Z","typed local tape keeps ISO date literal string at the actual file consumer");
                Check(BitConverter.DoubleToInt64Bits((double)read["exact_scalar"])==BitConverter.DoubleToInt64Bits(precise)&&BitConverter.DoubleToInt64Bits((double)read["signed_zero"])==BitConverter.DoubleToInt64Bits(negativeZero),"consumer preserves exact IEEE754 and negative zero bits");
                Check(BitConverter.DoubleToInt64Bits(LocalTapeCodec.Number(read["float_signed_zero"]))==BitConverter.DoubleToInt64Bits(negativeZero),"full production builder/file/consumer pipeline preserves float32 signedzero promoted to double");
                Check((ulong)read["unsigned_counter"]==ulong.MaxValue&&(string)read["literal_marker"]=="f64:3ff0000000000000"&&(string)read["marker_object"]["float64"]=="ordinary-text","consumer distinguishes integer, literal marker string and object shapes");
                var corrupt=LocalTapeCodec.Encode(header);corrupt["tape_encoding"]="unsupported-version";File.WriteAllText(path,corrupt.ToString(Formatting.None));bool refused=false;try{InitialWorldNative.Load(path);}catch(JsonException){refused=true;}Check(refused,"wrong local tape version fails closed");
                File.WriteAllText(path,header.ToString(Formatting.None));refused=false;try{InitialWorldNative.Load(path);}catch(JsonException){refused=true;}Check(refused,"raw legacy tape is not reinterpreted or silently upgraded");
                string ordinary=Path.Combine(directory,"normal.json");File.WriteAllText(ordinary,"{\"model\":\"normal-model\",\"tape_encoding\":\"ordinary-field\"}");
                Check((string)InitialWorldNative.Load(ordinary)["model"]=="normal-model","ordinary provider/state JSON does not pass through local tape decoder");
            }
            finally{Directory.Delete(directory,true);}
        }
    }
}
