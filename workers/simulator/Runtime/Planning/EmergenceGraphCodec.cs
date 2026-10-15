using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    /// <summary>Lossless encoding of the already rounded prompt graph. No entity or state is filtered.</summary>
    public static class EmergenceGraphCodec
    {
        public const string Encoding="factored-prefix-column-v2";
        public const string LegacyEncoding="prefix-column-v1";
        public const long Scale=10000;
        const int MaximumIdentities=100000,MaximumEntities=50000;
        static readonly string[] EntityColumns={"id","parent_id","kind","xyz","tag_set","state_schema","state_values","link_set","conditional_operation_set","kernel_semantic","physical_parent"};
        static readonly string[] GroupColumns={"parent_ref","kind","tag_set","state_schema","link_set","conditional_operation_set","kernel_semantic","physical_parent_ref"};
        static readonly string[] VisualColumns={"id","physical_parent_id","qualification_status"};
        static void Require(bool condition){if(!condition)throw new ArgumentException("Invalid compact emergence graph");}
        static int Integer(JToken value,int maximum)
        {Require(value!=null&&value.Type==JTokenType.Integer);long v=(long)value;Require(v>=0&&v<maximum);return (int)v;}
        static JArray Array(JToken value){var a=value as JArray;Require(a!=null);return a;}
        static string Text(JToken value){Require(value!=null&&value.Type==JTokenType.String);return (string)value;}
        static JToken Ref(Dictionary<string,int> indices,JToken value)
        {return value==null||value.Type==JTokenType.Null?JValue.CreateNull():new JValue(indices[Text(value)]);}
        static long Quantize(JToken value)
        {
            Require(value!=null&&(value.Type==JTokenType.Float||value.Type==JTokenType.Integer));double n=LocalTapeCodec.Number(value);
            Require(!double.IsNaN(n)&&!double.IsInfinity(n)&&Math.Abs(n)<=1e10);
            return checked((long)Math.Round(n*Scale,MidpointRounding.ToEven));
        }
        static JArray Columns(List<JArray> rows,int field,int width)
        {
            var result=new JArray();
            for(int col=0;col<width;col++)
            {
                var original=rows.Select(r=>r[field][col]).ToArray();
                if(original.Any(v=>Math.Abs(LocalTapeCodec.Number(v))>1e10)||original.Any(v=>BitConverter.DoubleToInt64Bits(LocalTapeCodec.Number(v))!=BitConverter.DoubleToInt64Bits((double)Quantize(v)/Scale)))
                {result.Add(new JObject{["raw"]=new JArray(original.Select(v=>v.DeepClone()))});continue;}
                long[] values=rows.Select(r=>Quantize(r[field][col])).ToArray();
                if(values.All(v=>v==values[0]))result.Add(values[0]);
                else {var differences=new JArray(values[0]);for(int i=1;i<values.Length;i++)differences.Add(checked(values[i]-values[i-1]));result.Add(differences);}
            }
            return result;
        }
        static JObject EncodeLegacy(JObject graph)
        {
            Require(graph!=null&&JToken.DeepEquals(graph["entity_columns"],JArray.FromObject(EntityColumns)));
            var entities=Array(graph["entities"]);var visuals=Array(graph["visuals"]);var linkSets=Array(graph["link_sets"]);
            Require(entities.Count<=MaximumEntities);
            var ids=new SortedSet<string>(StringComparer.Ordinal){"@world"};
            foreach(JArray row in entities)foreach(int col in new[]{0,1,10})if(row[col]!=null&&row[col].Type!=JTokenType.Null)ids.Add(Text(row[col]));
            foreach(JArray row in visuals)foreach(int col in new[]{0,1})if(row[col]!=null&&row[col].Type!=JTokenType.Null)ids.Add(Text(row[col]));
            foreach(JArray set in linkSets)foreach(var id in set)ids.Add(Text(id));
            // Preserve legacy graphs whose identity strings cannot use this bounded codec.
            if(ids.Any(id=>id.Length==0||id.Length>8192))return (JObject)graph.DeepClone();
            Require(ids.Count<=MaximumIdentities);
            var indices=ids.Select((id,index)=>new{id,index}).ToDictionary(p=>p.id,p=>p.index,StringComparer.Ordinal);
            string previous="";var encodedIds=new JArray();
            foreach(string id in ids){int prefix=0;while(prefix<Math.Min(previous.Length,id.Length)&&previous[prefix]==id[prefix])prefix++;if(prefix>0&&char.IsHighSurrogate(id[prefix-1]))prefix--;encodedIds.Add(new JArray(prefix,id.Substring(prefix)));previous=id;}
            var groups=new List<Tuple<JArray,List<JArray>>>();var groupIndices=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(JArray row in entities)
            {
                var common=new JArray(Ref(indices,row[1]),row[2],row[4],row[5],row[7],row[8],row[9],Ref(indices,row[10]));
                string key=common.ToString(Formatting.None);int index;
                if(!groupIndices.TryGetValue(key,out index)){index=groups.Count;groupIndices.Add(key,index);groups.Add(Tuple.Create(common,new List<JArray>()));}
                groups[index].Item2.Add(row);
            }
            var output=(JObject)graph.DeepClone();output.Remove("entities");output.Remove("entity_columns");
            output["encoding"]=LegacyEncoding;output["numeric_scale"]=Scale;output["identities"]=encodedIds;output["group_columns"]=JArray.FromObject(GroupColumns);
            output["entity_groups"]=new JArray(groups.Select(group=>new JObject{
                ["common"]=group.Item1,["entity_refs"]=new JArray(group.Item2.Select(r=>indices[Text(r[0])])),
                ["xyz_columns"]=Columns(group.Item2,3,3),["state_value_columns"]=Columns(group.Item2,6,Array(group.Item2[0][6]).Count)}));
            output["link_sets"]=new JArray(linkSets.Select(set=>new JArray(set.Select(id=>indices[Text(id)]))));
            output["visual_columns"]=new JArray("identity_ref","physical_parent_ref","qualification_status");
            output["visuals"]=new JArray(visuals.Select(row=>new JArray(Ref(indices,row[0]),Ref(indices,row[1]),row[2])));
            output["decode_instructions"]="identities: [prefixLength,suffix] uses the previous decoded identity prefix, length in UTF-16 code units, never splitting a surrogate pair. entity_ref indexes this table; null parent means none. Each group common follows group_columns. entity_refs sets row order. Numeric columns are a constant integer or [first,successive deltas]; cumulative sum divided by numeric_scale restores xyz/state values. A {raw:[values]} column stores large finite values without scaling. State keys follow state_schemas. Link/visual refs use the same identity table. Return entity_ref to avoid copying long names. All objects retained.";
            return output;
        }
        static JToken ExactColumn(JToken column)
        {
            var raw=column as JObject;if(raw==null)return column.DeepClone();
            Require(raw.Count==1&&raw["raw"] is JArray);
            return new JObject{["exact"]=new JArray(Array(raw["raw"]).Select(v=>"f64:"+unchecked((ulong)BitConverter.DoubleToInt64Bits(LocalTapeCodec.Number(v))).ToString("x16",CultureInfo.InvariantCulture)))};
        }
        public static JObject Encode(JObject graph)
        {
            var output=EncodeLegacy(graph);if(output["encoding"]==null)return output;
            var pool=new List<JToken>();var indices=new Dictionary<string,int>(StringComparer.Ordinal);
            Func<JToken,int> intern=column=>{var exact=ExactColumn(column);string key=exact.ToString(Formatting.None);int index;
                if(indices.TryGetValue(key,out index))return index;index=pool.Count;indices.Add(key,index);pool.Add(exact);return index;};
            var groups=new JArray();
            foreach(JObject group in Array(output["entity_groups"]))groups.Add(new JArray(group["common"].DeepClone(),group["entity_refs"].DeepClone(),
                new JArray(Array(group["xyz_columns"]).Select(c=>intern(c))),new JArray(Array(group["state_value_columns"]).Select(c=>intern(c)))));
            output["encoding"]=Encoding;output["numeric_columns"]=new JArray(pool);output["entity_groups"]=groups;
            output["entity_group_fields"]=new JArray("common","entity_refs","xyz_column_refs","state_column_refs");
            output["decode_instructions"]="Readable lossless symbol tables: identities [prefixLength,suffix] concatenate the previous identity's UTF-16 prefix without splitting a surrogate pair. entity_ref indexes the decoded identity table. Each entity_groups row is [common,entity_refs,xyz_column_refs,state_column_refs]; common follows group_columns. Numeric refs index numeric_columns: an integer is a scaled constant, an integer array is [first,successive deltas], divide cumulative values by numeric_scale. An {exact:[f64:16hex,...]} column preserves finite IEEE754 values that cannot roundtrip through scaling, one value per entity. Row order follows entity_refs; state keys follow state_schemas. Link and visual refs use the same identity table. No object, relation or state is omitted. Return canonical entity_id or entity_ref, never both.";
            return output;
        }
        public static string[] Identities(JObject graph)
        {
            Require(graph!=null&&((string)graph["encoding"]==Encoding||(string)graph["encoding"]==LegacyEncoding));
            var encoded=Array(graph["identities"]);Require(encoded.Count>0&&encoded.Count<=MaximumIdentities);
            var ids=new List<string>();string previous="";
            foreach(var token in encoded)
            {
                var row=Array(token);Require(row.Count==2);int prefix=Integer(row[0],previous.Length+1);Require(prefix==0||!char.IsHighSurrogate(previous[prefix-1]));string suffix=Text(row[1]);
                string id=previous.Substring(0,prefix)+suffix;Require(id.Length>0&&id.Length<=8192&&(ids.Count==0||StringComparer.Ordinal.Compare(previous,id)<0));ids.Add(id);previous=id;
            }
            Require(ids.Contains("@world"));return ids.ToArray();
        }
        static JToken Identity(string[] ids,JToken reference)
        {return reference==null||reference.Type==JTokenType.Null?JValue.CreateNull():new JValue(ids[Integer(reference,ids.Length)]);}
        static JArray[] ExpandColumns(JToken token,int width,int count)
        {
            var columns=Array(token);Require(columns.Count==width);var rows=Enumerable.Range(0,count).Select(i=>new JArray()).ToArray();
            foreach(var column in columns)
            {
                var raw=column as JObject;
                if(raw!=null)
                {
                    Require(raw.Count==1);var values=Array(raw["raw"]);Require(values.Count==count);
                    for(int row=0;row<count;row++){var n=values[row];Require(n.Type==JTokenType.Float||n.Type==JTokenType.Integer);double v=(double)n;Require(!double.IsNaN(v)&&!double.IsInfinity(v));rows[row].Add(n.DeepClone());}
                    continue;
                }
                var deltas=column as JArray;Require(deltas!=null?deltas.Count==count:column.Type==JTokenType.Integer);long value=0;
                for(int row=0;row<count;row++)
                {
                    JToken n=deltas==null?column:deltas[row];Require(n.Type==JTokenType.Integer);
                    value=deltas==null?(long)n:checked(value+(long)n);Require(Math.Abs((double)value)<=1e14);rows[row].Add((double)value/Scale);
                }
            }
            return rows;
        }
        static JObject Unfactor(JObject graph)
        {
            Require(JToken.DeepEquals(graph["entity_group_fields"],new JArray("common","entity_refs","xyz_column_refs","state_column_refs")));
            var pool=Array(graph["numeric_columns"]);Require(pool.Count<=1000000);
            var values=new List<JToken>();var distinct=new HashSet<string>(StringComparer.Ordinal);
            foreach(var column in pool)
            {
                Require(distinct.Add(column.ToString(Formatting.None)));
                var exact=column as JObject;
                if(exact!=null)
                {
                    Require(exact.Count==1);var encoded=Array(exact["exact"]);Require(encoded.Count>0&&encoded.Count<=MaximumEntities);var decoded=new JArray();
                    foreach(var token in encoded)
                    {
                        string text=Text(token);ulong bits=0;Require(text.Length==20&&text==text.ToLowerInvariant()&&text.StartsWith("f64:",StringComparison.Ordinal)&&
                            ulong.TryParse(text.Substring(4),NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out bits));
                        double number=BitConverter.Int64BitsToDouble(unchecked((long)bits));Require(!double.IsNaN(number)&&!double.IsInfinity(number));decoded.Add(LocalTapeCodec.Float(number));
                    }
                    values.Add(new JObject{["raw"]=decoded});
                }
                else
                {
                    var deltas=column as JArray;Require(deltas!=null?deltas.Count>0&&deltas.Count<=MaximumEntities:column.Type==JTokenType.Integer);
                    if(deltas!=null)foreach(var delta in deltas){Require(delta.Type==JTokenType.Integer);long ignored=(long)delta;}
                    else {long ignored=(long)column;}
                    values.Add(column.DeepClone());
                }
            }
            var output=(JObject)graph.DeepClone();var groups=new JArray();
            foreach(var token in Array(graph["entity_groups"]))
            {
                var row=Array(token);Require(row.Count==4);
                Func<JToken,JArray> expand=refs=>new JArray(Array(refs).Select(r=>values[Integer(r,values.Count)].DeepClone()));
                groups.Add(new JObject{["common"]=row[0].DeepClone(),["entity_refs"]=row[1].DeepClone(),["xyz_columns"]=expand(row[2]),["state_value_columns"]=expand(row[3])});
            }
            output["encoding"]=LegacyEncoding;output["entity_groups"]=groups;output.Remove("numeric_columns");output.Remove("entity_group_fields");return output;
        }
        public static JObject Decode(JObject graph)
        {Require(graph!=null);return DecodeLegacy((string)graph["encoding"]==Encoding?Unfactor(graph):graph);}
        static JObject DecodeLegacy(JObject graph)
        {
            if(graph["encoding"]==null)return (JObject)graph.DeepClone();
            string[] ids=Identities(graph);Require((long?)graph["numeric_scale"]==Scale&&JToken.DeepEquals(graph["group_columns"],JArray.FromObject(GroupColumns)));
            var tags=Array(graph["tag_sets"]);var schemas=Array(graph["state_schemas"]);var links=Array(graph["link_sets"]);var ops=Array(graph["conditional_operation_sets"]);
            var entities=new SortedDictionary<string,JArray>(StringComparer.Ordinal);int semantic=0;
            foreach(var token in Array(graph["entity_groups"]))
            {
                var group=token as JObject;Require(group!=null);var common=Array(group["common"]);Require(common.Count==8&&common[6].Type==JTokenType.Boolean);
                Text(common[1]);Integer(common[2],tags.Count);int schema=Integer(common[3],schemas.Count);Integer(common[4],links.Count);Integer(common[5],ops.Count);
                var refs=Array(group["entity_refs"]);Require(refs.Count>0&&refs.Count<=MaximumEntities&&entities.Count+refs.Count<=MaximumEntities);
                var xyz=ExpandColumns(group["xyz_columns"],3,refs.Count);var state=ExpandColumns(group["state_value_columns"],Array(schemas[schema]).Count,refs.Count);
                for(int i=0;i<refs.Count;i++)
                {
                    string id=ids[Integer(refs[i],ids.Length)];Require(id!="@world"&&!entities.ContainsKey(id));
                    entities.Add(id,new JArray(id,Identity(ids,common[0]),common[1],xyz[i],common[2],common[3],state[i],common[4],common[5],common[6],Identity(ids,common[7])));
                    if((bool)common[6])semantic++;
                }
            }
            Require((int?)graph["entity_count"]==entities.Count&&(int?)graph["kernel_semantic_count"]==semantic);
            var visuals=new JArray();var visualIds=new HashSet<string>(StringComparer.Ordinal);foreach(var token in Array(graph["visuals"]))
            {var row=Array(token);Require(row.Count==3);Text(row[2]);Require(row[0].Type==JTokenType.Integer);string id=Identity(ids,row[0]).ToString();Require(id!="@world"&&visualIds.Add(id));visuals.Add(new JArray(Identity(ids,row[0]),Identity(ids,row[1]),row[2]));}
            Require((int?)graph["visual_count"]==visuals.Count);
            var result=(JObject)graph.DeepClone();foreach(string key in new[]{"encoding","numeric_scale","identities","group_columns","entity_groups","decode_instructions"})result.Remove(key);
            result["entity_columns"]=JArray.FromObject(EntityColumns);result["entities"]=new JArray(entities.Values);result["visual_columns"]=JArray.FromObject(VisualColumns);result["visuals"]=visuals;
            result["link_sets"]=new JArray(links.Select(set=>new JArray(Array(set).Select(reference=>Identity(ids,reference)))));
            return result;
        }
        public static string ResolveEntity(JObject graph,JObject change)
        {
            bool named=change["entity_id"]!=null,referenced=change["entity_ref"]!=null;Require(named!=referenced);
            return named?Text(change["entity_id"]):Identities(graph)[Integer(change["entity_ref"],Array(graph["identities"]).Count)];
        }
    }
}
