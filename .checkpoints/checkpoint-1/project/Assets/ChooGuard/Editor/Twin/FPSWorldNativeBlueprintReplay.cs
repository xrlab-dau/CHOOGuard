#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace ChooGuard.Editor.Twin
{
    // Bounded immutable-original replay. This class changes no scene objects or assets.
    public static class FPSWorldNativeBlueprintReplay
    {
        static void Need(bool value,string why){if(!value)throw new InvalidDataException("Native blueprint replay: "+why);}
        static void Pin(JToken pin){Need(FPSWorldBlueprintBuilder.Hash((string)pin["path"])==(string)pin["sha256"],"exact immutable input "+(string)pin["path"]);}
        static byte[] Hex(string text,string key){var match=Regex.Match(text,@"\b"+key+@":\s*([0-9a-f]+)");Need(match.Success,"serialized byte channel "+key);string hex=match.Groups[1].Value;Need(hex.Length%2==0,"whole byte channel");var bytes=new byte[hex.Length/2];for(int i=0;i<bytes.Length;i++){int a=hex[i*2]<='9'?hex[i*2]-'0':hex[i*2]-'a'+10,b=hex[i*2+1]<='9'?hex[i*2+1]-'0':hex[i*2+1]-'a'+10;bytes[i]=(byte)(a*16+b);}return bytes;}
        static byte[] RawVertices(Mesh mesh){using(var data=Mesh.AcquireReadOnlyMeshData(mesh))return data[0].GetVertexData<byte>(0).ToArray();}
        static float Float(byte[] bytes,int offset)=>BitConverter.ToSingle(bytes,offset);
        static int[][] SerializedTriangles(string text,byte[] bytes)
        {
            Need(Regex.IsMatch(text,@"m_IndexFormat: 1\b"),"reviewed UInt32 index format");var block=Regex.Match(text,@"m_SubMeshes:\s*\n(.*?)\n  m_Shapes:",RegexOptions.Singleline);Need(block.Success,"serialized submesh descriptors");
            var descriptors=Regex.Matches(block.Groups[1].Value,@"firstByte: (\d+)\s+indexCount: (\d+)\s+topology: (\d+)\s+baseVertex: (\d+)");Need(descriptors.Count>0,"explicit submesh slots");
            return descriptors.Cast<Match>().Select(m=>{int start=int.Parse(m.Groups[1].Value),count=int.Parse(m.Groups[2].Value),topology=int.Parse(m.Groups[3].Value),baseVertex=int.Parse(m.Groups[4].Value);Need(topology==0&&count%3==0&&start+count*4<=bytes.Length,"triangle submesh byte range");return Enumerable.Range(0,count).Select(i=>BitConverter.ToInt32(bytes,start+i*4)+baseVertex).ToArray();}).ToArray();
        }
        static double[][] World(int[] indices,byte[] source,JToken matrix)
        {
            return indices.Select(i=>Enumerable.Range(0,3).Select(r=>(double)matrix[r][0]*Float(source,i*48)+(double)matrix[r][1]*Float(source,i*48+4)+(double)matrix[r][2]*Float(source,i*48+8)+(double)matrix[r][3]).ToArray()).ToArray();
        }
        static double[] Pixel(double[] point,JToken frame)
        {
            double a=(double)frame[0][0],b=(double)frame[0][1],c=(double)frame[1][0],d=(double)frame[1][1],x=point[0]-(double)frame[2][0],z=point[2]-(double)frame[2][1],det=a*d-b*c;return new[]{(d*x-c*z)/det,(-b*x+a*z)/det};
        }
        static double[] Bary(JToken token){Need(token is JArray a&&a.Count==3,"barycentric triple");var b=token.Select(x=>(double)x).ToArray();Need(b.All(double.IsFinite)&&b.Min()>=-1e-9&&Math.Abs(b.Sum()-1)<1e-9,"finite in-triangle barycentric coordinates");return b;}
        static double Determinant(double[][] q)=>q[0][0]*(q[1][1]*q[2][2]-q[1][2]*q[2][1])-q[0][1]*(q[1][0]*q[2][2]-q[1][2]*q[2][0])+q[0][2]*(q[1][0]*q[2][1]-q[1][1]*q[2][0]);
        static double PolygonFraction(JToken polygon){var q=polygon.Select(Bary).ToArray();double area=0;for(int i=1;i+1<q.Length;i++)area+=Math.Abs(Determinant(new[]{q[0],q[i],q[i+1]}));return area;}
        static bool NamedShortPatch(JToken change,JToken scope,int[] indices)
        {
            if((int)change["submesh"]!=26)return false;int t=(int)change["source_triangle_submesh_local"];
            int[] expected=t==686?new[]{73678,73659,73679}:t==687?new[]{73680,73679,73659}:null;
            if(expected==null)return false;var exception=scope["explicit_source_qualified_patch_exceptions"];Need(exception!=null&&(int)exception["submesh"]==26&&exception["source_ordinals"].Select(x=>(int)x).SequenceEqual(new[]{686,687})&&indices.SequenceEqual(expected),"exact two named short-patch source tuples");Pin(exception["proof"]);return true;
        }
        static double[][] At(double[][] bary,double[][] world)=>bary.Select(b=>Enumerable.Range(0,3).Select(k=>Enumerable.Range(0,3).Sum(i=>b[i]*world[i][k])).ToArray()).ToArray();
        static List<double[]> Clip(List<double[]> poly,Func<double[],double> side)
        {
            var output=new List<double[]>();for(int i=0;i<poly.Count;i++){var a=poly[i];var b=poly[(i+1)%poly.Count];double da=side(a),db=side(b);if(da>=0)output.Add(a);if((da>=0)!=(db>=0)){double t=da/(da-db);output.Add(Enumerable.Range(0,3).Select(k=>a[k]+t*(b[k]-a[k])).ToArray());}}return output;
        }
        static double IntersectionFraction(double[][] triangle,double[][] world,JToken frame,JToken box)
        {
            Func<double[],double[]> position=b=>Enumerable.Range(0,3).Select(k=>Enumerable.Range(0,3).Sum(i=>b[i]*world[i][k])).ToArray();var limits=box["source_box_px"];var ys=box["model_Y_interval"];var poly=triangle.ToList();
            var planes=new Func<double[],double>[]{b=>Pixel(position(b),frame)[0]-(double)limits[0],b=>(double)limits[2]-Pixel(position(b),frame)[0],b=>Pixel(position(b),frame)[1]-(double)limits[1],b=>(double)limits[3]-Pixel(position(b),frame)[1],b=>position(b)[1]-(double)ys[0],b=>(double)ys[1]-position(b)[1]};
            foreach(var plane in planes){poly=Clip(poly,plane);if(poly.Count<3)return 0;}double area=0;for(int i=1;i+1<poly.Count;i++)area+=Math.Abs(Determinant(new[]{poly[0],poly[i],poly[i+1]}));return area;
        }
        static JObject NormalizeCumulative(JToken proof,JToken edit,JToken frame,JToken packet,byte[] source)
        {
            Need(packet!=null&&(string)packet["schema"]=="chooguard.exact-native-source-terminal.v1"&&packet["native_mesh_edits"].Any(x=>JToken.DeepEquals(x,edit)),"cumulative edit belongs to exact sealed transaction");
            FPSWorldBlueprintBuilder.ValidatePrimarySource(packet["source_blueprint"],"2F");FPSWorldBlueprintBuilder.ValidatePrimarySource(packet["source1F_original"],"1F");Pin(proof["prior1F_selector"]);Pin(proof["prior1F_terminal"]);
            var prior=JObject.Parse(File.ReadAllText((string)proof["prior1F_selector"]["path"]));Need(prior["changes"].Count()==85&&(int)proof["source_vertex_count"]==98464&&(int)proof["prior1F_vertex_count"]==98540,"exact85lower source/prior76appends");
            Need(JToken.DeepEquals(prior["source_asset"],edit["source_asset"]),"cumulative lower and upper share immutable original source");
            var old=prior["changes"].ToDictionary(x=>((int)x["submesh"],(int)x["source_triangle_submesh_local"]));var normalized=new JArray();int lower=0,upper=0;
            foreach(var change in proof["changes"]){int slot=(int)change["submesh"],ordinal=(int)change["source_triangle"];var indices=change["source_indices"].Select(x=>(int)x).ToArray();var world=World(indices,source,edit["world_matrix"]);bool isLower=(string)change["stage"]=="1F_prior85_exact";
                if(isLower){Need(old.TryGetValue((slot,ordinal),out var original)&&JToken.DeepEquals(original["source_indices"],change["source_indices"])&&change["removed_barycentric_polygons"].Count()==1&&JToken.DeepEquals(original["removed_barycentric_polygon"],change["removed_barycentric_polygons"][0])&&JToken.DeepEquals(original["terminal_remaining_barycentric_triangles"],change["terminal_barycentric_triangles"]),"exact prior85 removal and outside barycentric geometry retained cumulatively");lower++;}
                else{Need((string)change["stage"]=="2F_connection_minimum4windows"&&slot==40&&new[]{69,70,76,77,83}.Contains(ordinal),"exact five named upper connection source triangles");upper++;}
                normalized.Add(new JObject{["submesh"]=slot,["source_triangle_submesh_local"]=ordinal,["source_indices"]=change["source_indices"].DeepClone(),["source_world_vertices"]=new JArray(world.Select(p=>new JArray(p))),["removed_barycentric_polygon"]=isLower?change["removed_barycentric_polygons"][0].DeepClone():new JArray(change["removed_barycentric_polygons"].SelectMany(p=>p).Select(p=>p.DeepClone())),["removed_barycentric_polygons"]=change["removed_barycentric_polygons"].DeepClone(),["removed_area_fraction"]=change["removed_barycentric_polygons"].Sum(PolygonFraction),["terminal_remaining_barycentric_triangles"]=change["terminal_barycentric_triangles"].DeepClone(),["terminal_triangle_ordinals_submesh_local"]=change["terminal_ordinals"].DeepClone(),["cumulative_2F_windows"]=!isLower});
            }
            Need(lower==85&&upper==5&&normalized.Count==90,"exact85+5 cumulative change set");var result=(JObject)proof.DeepClone();result["changes"]=normalized;result["source_raw"]=prior["source_raw"].DeepClone();result["source_complete_channel_digest"]=prior["source_complete_channel_digest"].DeepClone();result["scope"]=prior["scope"].DeepClone();result["scope"]["cumulative_2F_windows"]=packet["new2F_scope"].DeepClone();
            var priorBytes=Hex(File.ReadAllText((string)proof["prior1F_terminal"]["path"]),"_typelessdata");var terminal=Hex(File.ReadAllText((string)proof["terminal_asset"]["path"]),"_typelessdata");Need(priorBytes.Length==98540*48&&terminal.Take(priorBytes.Length).SequenceEqual(priorBytes),"all original records plus prior76appends exactly preserved");return result;
        }
        static void Qualified(JToken change,int[] indices,byte[] source,JToken matrix,JToken frame,JToken scope)
        {
            Need(indices.SequenceEqual(change["source_indices"].Select(x=>(int)x)),"exact original source triangle indices");var world=World(indices,source,matrix);var pixels=world.Select(p=>Pixel(p,frame)).ToArray();
            if((bool?)change["cumulative_2F_windows"]==true){var windows=scope["cumulative_2F_windows"].ToArray();Need(windows.Length==4&&(int)change["submesh"]==40&&new[]{69,70,76,77,83}.Contains((int)change["source_triangle_submesh_local"]),"bounded four-window upper source set");
                foreach(var polygon in change["removed_barycentric_polygons"]){var points=At(polygon.Select(Bary).ToArray(),world);Need(windows.Any(box=>points.All(point=>{var px=Pixel(point,frame);var b=box["source_box_px"];var y=box["model_Y_interval"];return px[0]>=(double)b[0]-1e-7&&px[0]<=(double)b[2]+1e-7&&px[1]>=(double)b[1]-1e-7&&px[1]<=(double)b[3]+1e-7&&point[1]>=(double)y[0]-1e-8&&point[1]<=(double)y[1]+1e-8;})),"removed upper polygon within one exact source/model window");}
                foreach(var remainder in change["terminal_remaining_barycentric_triangles"])foreach(var window in windows)Need(IntersectionFraction(remainder.Select(Bary).ToArray(),world,frame,window)<1e-9,"upper remaining triangles exclude reviewed source-window interiors");return;}
            var u=world[1].Zip(world[0],(a,b)=>a-b).ToArray();var v=world[2].Zip(world[0],(a,b)=>a-b).ToArray();var cross=new[]{u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]};double length=Math.Sqrt(cross.Sum(x=>x*x)),xspan=pixels.Max(p=>p[0])-pixels.Min(p=>p[0]),yspan=world.Max(p=>p[1])-world.Min(p=>p[1]);
            bool namedShort=scope["explicit_source_qualified_patch_exceptions"]!=null&&NamedShortPatch(change,scope,indices);
            Need(length>1e-12&&Math.Abs(cross[1]/length)<(double)scope["maximum_abs_world_face_normal_y"]&&(xspan>=(double)scope["minimum_source_projected_x_span_px"]||(namedShort&&xspan>=8&&xspan<10))&&yspan>=(double)scope["minimum_model_vertical_span"],"long vertical source face or exact two named short-patch qualification");
            Need(scope["opaque_material_slots"].Select(x=>(int)x).Contains((int)change["submesh"])&&pixels.Min(p=>p[1])>=(double)scope["source_v_qualification_range"][0]-1e-8&&pixels.Max(p=>p[1])<=(double)scope["source_v_qualification_range"][1]+1e-8&&pixels.Max(p=>p[1])-pixels.Min(p=>p[1])<xspan*.02+.25,"opaque near-parallel source strip qualification");
            for(int i=0;i<3;i++)for(int j=0;j<3;j++)Need(Math.Abs(world[i][j]-(double)change["source_world_vertices"][i][j])<1e-8,"immutable source world geometry replay");
            foreach(var token in change["removed_barycentric_polygon"]){var b=Bary(token);var point=Enumerable.Range(0,3).Select(k=>Enumerable.Range(0,3).Sum(i=>b[i]*world[i][k])).ToArray();var px=Pixel(point,frame);Need(point[1]>=(double)scope["model_y_clip_range"][0]-1e-8&&point[1]<=(double)scope["model_y_clip_range"][1]+1e-8&&px[0]>=(double)scope["source_u_range"][0]-1e-7&&px[0]<=(double)scope["source_u_range"][1]+1e-7,"removed points remain inside reviewed vertical/source bounds");}
        }
        public static Mesh Generate(JToken edit,JToken frame,JToken cumulativePacket=null)=>GenerateInternal(edit,frame,cumulativePacket,false);
        public static Mesh GenerateExactNative5InternalFacade(JToken edit,JToken frame,JToken transaction)=>GenerateInternal(edit,frame,transaction,true);
        // An exact five-face upper-storey selector uses its own bounds; lower north-strip rules remain unchanged.
        static void QualifiedInternalFacade(JToken change,int[] indices,byte[] source,JToken matrix,JToken frame)
        {
            int ordinal=(int)change["source_triangle_submesh_local"];
            Need((int)change["submesh"]==40&&new[]{69,70,76,77,83}.Contains(ordinal)&&indices.SequenceEqual(change["source_indices"].Select(x=>(int)x)),"exact five named Native5 internal facade source faces");
            var world=World(indices,source,matrix);var u=world[1].Zip(world[0],(a,b)=>a-b).ToArray();var v=world[2].Zip(world[0],(a,b)=>a-b).ToArray();var cross=new[]{u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]};double length=Math.Sqrt(cross.Sum(x=>x*x));
            Need(length>1e-12&&Math.Abs(cross[1]/length)<1e-4,"exact upper faces remain vertical opaque facade geometry");
            for(int i=0;i<3;i++)for(int j=0;j<3;j++)Need(Math.Abs(world[i][j]-(double)change["source_world_vertices"][i][j])<1e-8,"fresh immutable source world coordinates");
            var window=new JObject{["source_box_px"]=new JArray(145.5,306.0,248.2,307.0),["model_Y_interval"]=new JArray(6.7999,12.1)};
            var polygons=change["removed_barycentric_polygons"] as JArray;Need(polygons!=null&&polygons.Count>0,"explicit removed barycentric polygons");
            double removed=0;foreach(var polygon in polygons){var points=At(polygon.Select(Bary).ToArray(),world);Need(points.Length>=3,"removed polygon vertices");foreach(var point in points){var px=Pixel(point,frame);Need(px[0]>=145.5-1e-7&&px[0]<=248.2+1e-7&&px[1]>=306.0-1e-7&&px[1]<=307.0+1e-7&&point[1]>=6.7999-1e-7&&point[1]<=12.1+1e-7,"removed geometry stays in exact named internal span and floor-to-underside interval");}removed+=PolygonFraction(polygon);}
            Need(Math.Abs(removed-(double)change["removed_area_fraction"])<1e-9,"explicit removed area fraction");
            foreach(var remainder in change["terminal_remaining_barycentric_triangles"])Need(IntersectionFraction(remainder.Select(Bary).ToArray(),world,frame,window)<1e-9,"outside remainders retain no reviewed internal barrier area");
        }
        static Mesh GenerateInternal(JToken edit,JToken frame,JToken cumulativePacket,bool exactInternalFacade)
        {
            if(exactInternalFacade){Need(cumulativePacket!=null&&(string)cumulativePacket["schema"]=="chooguard.exact-native-source-terminal.v1"&&cumulativePacket["native_mesh_edits"].Any(e=>JToken.DeepEquals(e,edit)),"exact internal facade transaction membership");FPSWorldBlueprintBuilder.ValidatePrimarySource(cumulativePacket["source_blueprint"],"2F");Pin(cumulativePacket["foundation_registry"]);foreach(var pin in cumulativePacket["sealed_files"] as JArray??new JArray())Pin(pin);}
            foreach(string key in new[]{"source_asset","source_meta","terminal_asset","selector_and_replay"})Pin(edit[key]);var proof=JObject.Parse(File.ReadAllText((string)edit["selector_and_replay"]["path"]));if(proof["prior1F_selector"]!=null){var originalBytes=Hex(File.ReadAllText((string)edit["source_asset"]["path"]),"_typelessdata");proof=NormalizeCumulative(proof,edit,frame,cumulativePacket,originalBytes);}foreach(string key in new[]{"source_raw","source_channel_payload"})Pin(proof[key]);if(exactInternalFacade){Need((double)proof["scope"]["model_y_clip_range"][0]==6.7999&&(double)proof["scope"]["model_y_clip_range"][1]==12.1,"named source floor-to-original-underside height qualification");for(int r=0;r<3;r++)for(int c=0;c<2;c++)Need(Math.Abs((double)proof["scope"]["source_px_to_world_XZ"][r][c]-(double)frame[r][c])<1e-12,"explicit accepted MAIN2F frame for selected upper faces");}
            Need(JToken.DeepEquals(edit["source_asset"],proof["source_asset"])&&JToken.DeepEquals(edit["terminal_asset"],proof["terminal_asset"]),"exact source and terminal proof lineage");var original=AssetDatabase.LoadAssetAtPath<Mesh>((string)edit["source_asset"]["path"]);Need(original!=null&&!EditorUtility.IsDirty(original)&&original.vertexBufferCount==1&&original.GetVertexBufferStride(0)==48,"clean original single48byteFloat32 stream");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original,out string guid,out long local);Need(guid==(string)edit["source_GUID"]&&local==(long)edit["source_local_file_id"]&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(original)==(string)proof["source_complete_channel_digest"],"immutable original GUID/localID/all channels");
            var attributes=original.GetVertexAttributes();Need(attributes.All(a=>a.stream==0&&a.format==VertexAttributeFormat.Float32),"reviewed Float32 channel layout");string sourceText=File.ReadAllText((string)edit["source_asset"]["path"]),terminalText=File.ReadAllText((string)edit["terminal_asset"]["path"]);var source=Hex(sourceText,"_typelessdata");Need(source.SequenceEqual(RawVertices(original))&&source.Length==original.vertexCount*48,"fresh immutable original byte prefix");
            var terminal=Hex(terminalText,"_typelessdata");int count=(int)proof["terminal_vertex_count"];Need(terminal.Length==count*48&&terminal.Take(source.Length).SequenceEqual(source),"terminal retains every original channel byte");var expected=new byte[terminal.Length];Buffer.BlockCopy(source,0,expected,0,source.Length);var assigned=new HashSet<int>();
            var output=SerializedTriangles(terminalText,Hex(terminalText,"m_IndexBuffer"));Need(output.Length==original.subMeshCount,"all original submesh slots preserved");var changes=proof["changes"].ToDictionary(c=>((int)c["submesh"],(int)c["source_triangle_submesh_local"]));if(exactInternalFacade)Need(original.vertexCount==98464&&original.subMeshCount==85&&changes.Count==5&&changes.Keys.All(k=>k.Item1==40)&&changes.Keys.Select(k=>k.Item2).OrderBy(x=>x).SequenceEqual(new[]{69,70,76,77,83}),"exact current Native5 source and five-face selector");Need(changes.Count==((int?)edit["changed_triangles"]??(int)edit["changed_source_triangles"]),"exact reviewed changed-face count");var expectedIndices=new List<int[]>();int visited=0;
            for(int s=0;s<original.subMeshCount;s++)
            {
                Need(original.GetTopology(s)==MeshTopology.Triangles,"original triangle topology");var input=original.GetTriangles(s);var indices=new List<int>();for(int t=0;t<input.Length/3;t++)
                {
                    var tri=input.Skip(t*3).Take(3).ToArray();if(!changes.TryGetValue((s,t),out var change)){indices.AddRange(tri);continue;}visited++;if(exactInternalFacade)QualifiedInternalFacade(change,tri,source,edit["world_matrix"],frame);else Qualified(change,tri,source,edit["world_matrix"],frame,proof["scope"]);double fraction=(double)change["removed_area_fraction"];var remainders=change["terminal_remaining_barycentric_triangles"].ToArray();var ordinals=change["terminal_triangle_ordinals_submesh_local"].Select(x=>(int)x).ToArray();Need(remainders.Length==ordinals.Length,"outside triangle correspondence");
                    for(int r=0;r<remainders.Length;r++)
                    {
                        Need(ordinals[r]==indices.Count/3,"outside triangles retain exact source slot/order");var bary=remainders[r].Select(Bary).ToArray();Need(bary.Length==3,"outside triangle barycentric vertices");fraction+=Math.Abs(Determinant(bary));var ids=output[s].Skip(ordinals[r]*3).Take(3).ToArray();Need(ids.Length==3,"terminal outside triangle");
                        for(int k=0;k<3;k++)
                        {
                            int id=ids[k];Need(id>=0&&id<count,"terminal vertex range");var record=new byte[48];for(int channel=0;channel<12;channel++){float value=(float)(bary[k][0]*Float(source,tri[0]*48+channel*4)+bary[k][1]*Float(source,tri[1]*48+channel*4)+bary[k][2]*Float(source,tri[2]*48+channel*4));Need(float.IsFinite(value),"finite interpolated channel");Buffer.BlockCopy(BitConverter.GetBytes(value),0,record,channel*4,4);}
                            if(id<original.vertexCount)Need(record.SequenceEqual(source.Skip(id*48).Take(48)),"original outside corner preserves exact record");else if(assigned.Add(id))Buffer.BlockCopy(record,0,expected,id*48,48);else Need(record.SequenceEqual(expected.Skip(id*48).Take(48)),"shared outside vertex deterministic replay");
                        }
                        indices.AddRange(ids);
                    }
                    Need(Math.Abs(fraction-1)<1e-9,"removed plus outside area equals original source triangle");
                }
                Need(indices.SequenceEqual(output[s]),"all unaffected triangle indices exact slot/order");expectedIndices.Add(indices.ToArray());
            }
            Need(visited==changes.Count&&assigned.Count==count-original.vertexCount&&expected.SequenceEqual(terminal),"fresh immutable original to terminal all48byte records exactly replayed");
            var mesh=new Mesh{name="FreshNativeBlueprintReplay",indexFormat=IndexFormat.UInt32};try{mesh.SetVertexBufferParams(count,attributes);mesh.SetVertexBufferData(expected,0,0,expected.Length,0,MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);var all=expectedIndices.SelectMany(x=>x).ToArray();mesh.SetIndexBufferParams(all.Length,IndexFormat.UInt32);mesh.SetIndexBufferData(all,0,0,all.Length,MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);mesh.subMeshCount=output.Length;int start=0;for(int s=0;s<output.Length;s++){var ix=output[s];var bounds=original.GetSubMesh(s).bounds;int first=0,vertices=0;if(ix.Length>0){first=ix.Min();vertices=ix.Max()-first+1;Vector3 low=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity),high=new Vector3(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);foreach(int id in ix){var point=new Vector3(Float(expected,id*48),Float(expected,id*48+4),Float(expected,id*48+8));low=Vector3.Min(low,point);high=Vector3.Max(high,point);}bounds=new Bounds((low+high)*.5f,high-low);}mesh.SetSubMesh(s,new SubMeshDescriptor(start,ix.Length,MeshTopology.Triangles){baseVertex=0,firstVertex=first,vertexCount=vertices,bounds=bounds},MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);start+=ix.Length;}mesh.bounds=original.bounds;Need(mesh.GetVertexAttributes().SequenceEqual(attributes)&&RawVertices(mesh).SequenceEqual(expected),"fresh mesh exact complete raw channel layout");return mesh;}catch{UnityEngine.Object.DestroyImmediate(mesh);throw;}
        }
    }
}
#endif
