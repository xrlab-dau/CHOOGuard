using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class EmergenceGraphCodecTests
    {
        static void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException("Factored graph: "+reason);}
        static JObject Graph()
        {
            double minus=BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000UL));
            double ulp=BitConverter.Int64BitsToDouble(unchecked((long)0x3fc278e93a3bfa76UL));
            Func<JArray> xyz=()=>new JArray(LocalTapeCodec.Float(minus),LocalTapeCodec.Float(ulp),LocalTapeCodec.Float(1e100));
            Func<JArray> state=()=>new JArray(LocalTapeCodec.Float(ulp),LocalTapeCodec.Float(minus));
            return new JObject{
                ["entity_columns"]=new JArray("id","parent_id","kind","xyz","tag_set","state_schema","state_values","link_set","conditional_operation_set","kernel_semantic","physical_parent"),
                ["entities"]=new JArray(new JArray("a😀",null,"source",xyz(),0,0,state(),0,0,false,null),new JArray("b한글","a😀","source",xyz(),0,0,state(),0,0,false,"a😀")),
                ["tag_sets"]=new JArray(new object[]{new JArray("decoration")}),["state_schemas"]=new JArray(new object[]{new JArray("amount","zero")}),
                ["link_sets"]=new JArray(new object[]{new JArray("a😀","b한글")}),["conditional_operation_sets"]=new JArray(new object[]{new JArray()}),
                ["entity_count"]=2,["kernel_semantic_count"]=0,["visual_columns"]=new JArray("id","physical_parent_id","qualification_status"),
                ["visuals"]=new JArray(new object[]{new JArray("v😀","a😀","MODEL")}),["visual_count"]=1,
                ["visual_claim"]="fixture",["numeric_summary"]="Fixture exact numeric input; no further rounding"};
        }
        static void Reject(JObject graph)
        {bool refused=false;try{EmergenceGraphCodec.Decode(graph);}catch(ArgumentException){refused=true;}catch(OverflowException){refused=true;}Check(refused,"malformed numeric/table/reference graph refused");}
        public static void Run()
        {
            var raw=Graph();var encoded=EmergenceGraphCodec.Encode(raw);
            Check((string)encoded["encoding"]==EmergenceGraphCodec.Encoding,"explicit new encoding");
            Check(encoded["numeric_columns"] is JArray&&encoded["entity_groups"][0] is JArray,"readable shared numeric dictionary and group row schema");
            var decoded=EmergenceGraphCodec.Decode(JObject.Parse(encoded.ToString(Formatting.None)));
            Check(JToken.DeepEquals(raw,decoded),"all identities/parent/links/states/visuals preserved");
            for(int row=0;row<2;row++)foreach(int field in new[]{3,6})for(int i=0;i<((JArray)raw["entities"][row][field]).Count;i++)
                Check(BitConverter.DoubleToInt64Bits(LocalTapeCodec.Number(raw["entities"][row][field][i]))==BitConverter.DoubleToInt64Bits(LocalTapeCodec.Number(decoded["entities"][row][field][i])),"wire roundtrip preserves one ULP/signed zero/large finite numeric bits");
            Check(EmergenceGraphCodec.Encode(raw).ToString(Formatting.None)==encoded.ToString(Formatting.None),"canonical encoding deterministic");
            var ids=EmergenceGraphCodec.Identities(encoded);Check(EmergenceGraphCodec.ResolveEntity(encoded,new JObject{["entity_ref"]=Array.IndexOf(ids,"b한글")})=="b한글","alias resolves canonical ID");
            foreach(int test in Enumerable.Range(0,10))
            {
                var bad=(JObject)encoded.DeepClone();
                switch(test)
                {
                    case 0:bad["entity_groups"][0][2][0]=-1;break;
                    case 1:bad["entity_groups"][0][3][0]=((JArray)bad["numeric_columns"]).Count;break;
                    case 2:((JArray)bad["numeric_columns"]).Add(bad["numeric_columns"][0].DeepClone());break;
                    case 3:bad["numeric_columns"][0]=new JObject{["exact"]=new JArray("f64:7ff0000000000000")};break;
                    case 4:bad["numeric_columns"][0]=new JObject{["exact"]=new JArray()};break;
                    case 5:bad["entity_group_fields"][0]="unknown";break;
                    case 6:((JArray)bad["entity_groups"][0]).Add(0);break;
                    case 7:bad["entity_groups"][1][1][0]=bad["entity_groups"][0][1][0].DeepClone();break;
                    case 8:((JArray)bad["visuals"]).Add(bad["visuals"][0].DeepClone());bad["visual_count"]=2;break;
                    case 9:bad["numeric_columns"][0]=new JObject{["unknown"]=new JArray(0)};break;
                }
                Reject(bad);
            }
        }
    }
}
