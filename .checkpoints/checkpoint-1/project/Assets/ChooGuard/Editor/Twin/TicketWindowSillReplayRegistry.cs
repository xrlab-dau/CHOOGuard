#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace ChooGuard.Editor.Twin
{
    // Narrow composition overlay. The original seven locker sources and sealed packet remain immutable.
    public static class TicketWindowSillReplayRegistry
    {
        public const string Registry="content/world/observed-interiors/ticket-window-sill-replay.json";
        public const string Root="실내 트윈 마감/2F 본관 보강";
        public const string Spec="content/world/observed-interiors/2f-main-enhance.json";
        public const string ApprovedSpecSha="4b2b69bae36784f6515343feebe28abd72f2a661c6d21c935dbd83dbefb01854";
        public const string Folder="workers/twin/output/floor2-raster-reconstruction-20261009/held-architecture-resolution/";
        public const string Keys=Folder+"korail-retired-collider-triangle-position-keys.json";
        public const string KeysSha="88bf0c3183c8f61688b6511b0473b29bb92a87382dc08e55caead7d183326a3f";
        public const string Fix=Folder+"ticket-window-sill-fix-packet.json";
        public const string FixSha="45f4f9224721136c1a264b48724d94c373eea9127460c510c4bc69d2113206f8";
        public static void Need(bool ok,string message){if(!ok)throw new InvalidDataException("Ticket sill replay: "+message);}
        public static string Hash(string path)=>OfficialFloorplanArchitectureRegistry.Hash(path);
        public static string Digest(Mesh m)=>ArchitecturalCorrectionRegistry.CompleteChannelDigest(m);
        static string KeyPath(string p)=>p.Normalize(System.Text.NormalizationForm.FormC);
        static bool ColliderSource(string p)=>KeyPath(p)==KeyPath(SourceFolder()+"/Kit_Colliders.asset");
        public static string SourceFolder()
        {
            var packet=LockerPlacementWithholdingRegistry.ReadPacket();
            return Path.GetDirectoryName((string)packet["meshes"].Single(x=>(string)x["role"]=="mesh-3476")["full_channel_export"]["asset"]).Replace('\\','/');
        }
        static JObject Read()
        {
            if(!File.Exists(Registry))return null;
            Need(Hash(Keys)==KeysSha&&Hash(Fix)==FixSha,"sealed source proof changed");
            var j=JObject.Parse(File.ReadAllText(Registry));
            Need((string)j["schema"]=="chooguard.ticket-window-sill-replay.v1"&&(string)j["source_spec"]==Spec&&Hash(Spec)==ApprovedSpecSha&&(string)j["source_spec_sha256"]==ApprovedSpecSha,"canonical recipe / overlay changed");
            var rows=(JArray)j["rows"];
            Need(rows.Count==2&&rows.Select(x=>KeyPath((string)x["source_asset"])).Distinct().Count()==2&&rows.All(x=>(string)x["source_asset"]==SourceFolder()+"/Kit_Joint.asset"||(string)x["source_asset"]==SourceFolder()+"/Kit_Colliders.asset"),"only Joint and Colliders may be composed");
            return j;
        }
        public static string[] KorailKeys()
        {
            Need(Hash(Keys)==KeysSha,"sealed lounge keys changed");
            var p=JObject.Parse(File.ReadAllText(Keys));var keys=p["exact_sorted_local_float32_triangle_keys_hex"].Select(x=>(string)x).ToArray();
            Need(keys.Length==12&&keys.All(x=>x.Length==72),"exact twelve lounge keys required");return keys;
        }
        public static string TriangleKey(Vector3 a,Vector3 b,Vector3 c)
        {
            // Hex ordering equals bytewise ordering, preserving IEEE float bits including signed zero.
            string V(Vector3 p){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(p.x);w.Write(p.y);w.Write(p.z);return BitConverter.ToString(s.ToArray()).Replace("-","").ToLowerInvariant();}}
            return string.Concat(new[]{V(a),V(b),V(c)}.OrderBy(x=>x,StringComparer.Ordinal));
        }
        public static string[] PositionKeys(Mesh m,IEnumerable<int> ordinals)
        {
            Need(m.subMeshCount==1&&m.GetTopology(0)==MeshTopology.Triangles,"single triangle submesh required");
            var v=m.vertices;var t=m.GetIndices(0);return ordinals.Select(i=>{Need(i>=0&&i<t.Length/3,"source ordinal outside frozen source");return TriangleKey(v[t[i*3]],v[t[i*3+1]],v[t[i*3+2]]);}).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        public static Mesh RemoveKeys(Mesh m,string[] keys,out int[] mapped)
        {
            Need(m!=null&&m.isReadable&&m.subMeshCount==1&&m.GetTopology(0)==MeshTopology.Triangles,"unsupported mesh topology");
            var t=m.GetIndices(0);var v=m.vertices;var wanted=keys.GroupBy(x=>x).ToDictionary(g=>g.Key,g=>g.Count());var found=new Dictionary<string,List<int>>();
            for(int i=0;i<t.Length/3;i++){string k=TriangleKey(v[t[i*3]],v[t[i*3+1]],v[t[i*3+2]]);if(!wanted.ContainsKey(k))continue;if(!found.ContainsKey(k))found[k]=new List<int>();found[k].Add(i);}
            Need(wanted.All(k=>found.ContainsKey(k.Key)&&found[k.Key].Count==k.Value),"position-key occurrence multiplicity changed; never use stale ordinals");
            mapped=found.Values.SelectMany(x=>x).OrderBy(x=>x).ToArray();Need(mapped.Length==keys.Length,"exact removal count changed");var rm=new HashSet<int>(mapped);
            var result=UnityEngine.Object.Instantiate(m);result.hideFlags=HideFlags.None;
            result.SetIndices(Enumerable.Range(0,t.Length/3).Where(i=>!rm.Contains(i)).SelectMany(i=>t.Skip(i*3).Take(3)).ToArray(),MeshTopology.Triangles,0,false);
            result.bounds=LockerPlacementWithholdingRegistry.ReferencedBounds(result);return result;
        }
        public static Mesh Load(JToken pin)
        {
            string path=(string)pin["asset"];Need(File.Exists(path)&&Hash(path)==(string)pin["file_sha256"]&&AssetDatabase.AssetPathToGUID(path)==(string)pin["guid"],"replacement file / GUID changed: "+path);
            var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);Need(m!=null&&!EditorUtility.IsDirty(m)&&Digest(m)==(string)pin["complete_digest"]&&OfficialFloorplanArchitectureRegistry.MeshHash(m)==(string)pin["payload_sha256"],"replacement channels changed: "+path);return m;
        }
        static JToken Row(JObject j,string source)=>j?["rows"].SingleOrDefault(x=>KeyPath((string)x["source_asset"])==KeyPath(source));
        public static bool TryResolveInstalled(Mesh candidate,string source,out Mesh resolved)
        {
            resolved=null;var j=Read();var row=Row(j,source);if(row==null||candidate==null)return false;
            var target=Load(row["locker_output"]);
            if(AssetDatabase.GetAssetPath(candidate)!=AssetDatabase.GetAssetPath(target))return false;
            Need(Digest(candidate)==Digest(target),"saved new locker stage changed");resolved=target;return true;
        }
        public static bool TryResolveForRebuild(Mesh fresh,string source,out Mesh resolved)
        {
            resolved=null;var j=Read();var row=Row(j,source);if(row==null)return false;
            Mesh installed;if(TryResolveInstalled(fresh,source,out installed)){resolved=installed;return true;}
            Need(Digest(fresh)==(string)row["fresh_complete_digest"]&&(int)fresh.indexFormat==(int)row["fresh_index_format"]&&fresh.GetVertexAttributes().Select(x=>x.ToString()).SequenceEqual(row["fresh_vertex_attributes"].Select(x=>(string)x)),"fresh generated baseline/layout changed: "+source);
            var originalRow=LockerPlacementWithholdingRegistry.ReadPacket()["meshes"].Single(x=>KeyPath((string)x["full_channel_export"]["asset"])==KeyPath(source));
            var exactKeys=PositionKeys(LockerPlacementWithholdingRegistry.Source(originalRow),originalRow["selected_triangle_ordinals"].Select(x=>(int)x));
            Need(exactKeys.SequenceEqual(row["locker_position_keys"].Select(x=>(string)x))&&(int)row["locker_removed_count"]==(int)originalRow["selected_triangle_count"],"sealed locker POSITION selection changed");
            var expected=RemoveKeys(fresh,exactKeys,out var removed);
            try{Need(removed.Length==(int)row["locker_removed_count"],"locker removal count changed");resolved=Load(row["locker_output"]);Need(Digest(expected)==Digest(resolved),"locker-only replay mismatch");return true;}
            finally{UnityEngine.Object.DestroyImmediate(expected);}
        }
        public static Mesh InstalledForWithholding(string source,Mesh oldLockerOutput)
        {
            var j=Read();var row=Row(j,source);
            if(row!=null)
            {
                var stage=Load(row["locker_output"]);var final=Load(row["installed_output"]);
                if(ColliderSource(source))VerifyLoungeComposition(stage,final);
                else Need(stage==final,"Joint may only contain the preserved locker cut");
                return final;
            }
            // Pre-existing Official architecture correction is a validated composition, not a relaxed collider exemption.
            if(!ColliderSource(source)||!File.Exists(OfficialFloorplanArchitectureRegistry.Registry))return oldLockerOutput;
            var registry=JObject.Parse(File.ReadAllText(OfficialFloorplanArchitectureRegistry.Registry));
            var rows=(registry["collider_corrections"] as JArray??new JArray()).Where(x=>(string)x["path"]==Root+"/Kit_Colliders").ToArray();
            if(rows.Length==0)return oldLockerOutput;Need(rows.Length==1,"ambiguous official collider composition");var r=rows[0];
            Need(OfficialFloorplanArchitectureRegistry.MeshHash(oldLockerOutput)==(string)r["mesh_payload_sha256"],"official source payload differs from preserved locker stage");
            string path=(string)r["replacement_asset"];var finalMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Need(finalMesh!=null&&AssetDatabase.AssetPathToGUID(path)==(string)r["replacement_guid"]&&OfficialFloorplanArchitectureRegistry.MeshHash(finalMesh)==(string)r["replacement_payload_sha256"],"official composite binding changed");
            VerifyLoungeComposition(oldLockerOutput,finalMesh);return finalMesh;
        }
        public static void VerifyLoungeComposition(Mesh stage,Mesh final)
        {
            var expected=RemoveKeys(stage,KorailKeys(),out var mapped);
            try{Need(mapped.Length==12&&Digest(expected)==Digest(final),"official collider must be exactly preserved channels minus the twelve lounge triangles");}
            finally{UnityEngine.Object.DestroyImmediate(expected);}
        }
        public static string[] InstalledAssets()=>Read()?["rows"].SelectMany(x=>new[]{(string)x["locker_output"]["asset"],(string)x["installed_output"]["asset"]}).Distinct().ToArray()??Array.Empty<string>();
    }
}
#endif
