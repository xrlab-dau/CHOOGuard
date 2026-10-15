#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace ChooGuard.Editor.Twin
{
    // Only the five closure and one electrical-precision historical official-collider corrections superseded by the approved131-control clip.
    public static class Main1FNegativeClosureReplay
    {
        const string Receipt="content/world/observed-interiors/source1f-negative-closure-20261010.json";
        const string Packet="workers/twin/output/fidelity-reference-audit-20261010/one-f-legacy-closure/sealed-batch-packet.json";
        const string PrecisionOwner="실내 트윈 마감/1F PDF 원도 복원/SourceTrace_pdf1f-electrical-room-edge-2_Kit_Colliders";
        const string PrecisionReceipt="content/world/observed-interiors/source1f-precision-20261010.json";
        static readonly string[] Owners={"실내 트윈 마감/1F 전기실 원도 후면벽/Kit_Colliders","실내 트윈 마감/1F PDF 원도 복원/SourceTrace_pdf1f-disaster-center-edge-3_Kit_Colliders","실내 트윈 마감/1F PDF 원도 복원/SourceTrace_pdf1f-disaster-center-edge-1_Kit_Colliders","실내 트윈 마감/1F PDF 원도 복원/SourceTrace_pdf1f-electrical-room-edge-1_Kit_Colliders","실내 트윈 마감/1F PDF 원도 복원/SourceTrace_pdf1f-military-consultation-edge-2_Kit_Colliders"};
        static void Need(bool b,string why){if(!b)throw new InvalidDataException("Exact131-control successor: "+why);}
        static string H(string p)=>ConcourseFidelityCorrection.Hash(p);
        static JObject J(string p)=>JObject.Parse(File.ReadAllText(p));
        static string D(Mesh m)=>ArchitecturalCorrectionRegistry.CompleteChannelDigest(m);
        static byte[] Raw(Mesh m,bool vertex){using(var d=Mesh.AcquireReadOnlyMeshData(m))return vertex?d[0].GetVertexData<byte>(0).ToArray():d[0].GetIndexData<byte>().ToArray();}
        static string BH(byte[] b){using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
        static void Pin(JToken p){string path=(string)p["path"];Need(H(path)==(string)p["sha256"]&&H(path+".meta")== (string)p["meta_sha256"]&&AssetDatabase.AssetPathToGUID(path)==(string)p["guid"],"immutable asset "+path);}
        static JObject Inputs()
        {
            Need(H(Receipt)=="27f65126f28c30376a60764c173e09d31640b1634b5c53838cb1005e87983810"&&H(Packet)=="7c2f8f5ecaccb86125b5d1ba4ff53dedb9e790a91035476d15415ba35ff0baed","frozen installed receipt/packet");var p=J(Packet);var r=J(Receipt);
            Need((string)r["packet"]==Packet&&(string)r["packet_sha256"]==H(Packet)&&(string)r["source_controls_protected"]=="all131plus2px","exact reviewed source-control scope");
            foreach(var q in p["frozen_files"])Need(H((string)q["path"])==(string)q["sha256"],"sealed clip/131-control proof "+q["path"]);
            foreach(string k in new[]{"source_raster","source_vectors","registration"})Need(H((string)p[k]["path"])==(string)p[k]["sha256"],"source registration "+k);return p;
        }
        // Construct complete reviewed local vertex/index channels from the sealed recipe bytes.
        // The exact retained source prefix, source channels, owner and frame are checked before construction.
        public static Mesh ResolveFresh(Mesh source,string owner,Matrix4x4 frame,JToken prior)
        {
            if(owner==PrecisionOwner)return PrecisionFresh(source,frame,prior);
            Need(Owners.Contains(owner)&&source!=null,"named supported owner/source");var packet=Inputs();var r=J(Receipt)["owner_changes"].Single(q=>(string)q["owner"]==owner&&(string)q["role"]=="MeshCollider");var p=packet["owners"].Single(q=>q["paired_colliders"].Any(c=>(string)c["owner"]==owner));var c=p["paired_colliders"].Single(q=>(string)q["owner"]==owner);var recipe=J((string)r["source_recipe"]);
            Need(H((string)r["source_recipe"])==(string)r["source_recipe_sha256"]&&(string)p["recipe_sha256"]==(string)r["source_recipe_sha256"],"exact recipe lineage");Pin(r["source_asset"]);Pin(r["derived_asset"]);
            Need((string)prior["path"]==owner&&(string)prior["replacement_asset"]==(string)r["source_asset"]["path"]&&(string)prior["replacement_guid"]==(string)r["source_asset"]["guid"]&&OfficialFloorplanArchitectureRegistry.MeshHash(source)==(string)prior["replacement_payload_sha256"]&&D(source)==(string)prior["replacement_complete_channels_sha256"]&&D(source)==(string)p["current_expected_unity_digests"]["complete_channels_sha256"],"exact historical correction source");
            for(int a=0;a<4;a++)for(int b=0;b<4;b++)Need(float.IsFinite(frame[a,b])&&Mathf.Abs(frame[a,b]-(float)c["world_matrix"][a][b])<.00004f&&Mathf.Abs(frame[a,b]-(float)prior["world_matrix"][a][b])<.00004f,"exact owner frame");
            Need(BH(Raw(source,true))==(string)c["current_vertex_bytes_sha256"]&&BH(Raw(source,false))==(string)c["current_index_bytes_sha256"],"full ordered source raw channels");
            string vp=(string)recipe["vertices_path"],ip=(string)recipe["indices_path"];Need(H(vp)==(string)recipe["vertices_sha256"]&&H(ip)==(string)recipe["indices_sha256"],"sealed recipe bytes");var vertices=File.ReadAllBytes(vp);var indices=File.ReadAllBytes(ip);Need(vertices.Take(Raw(source,true).Length).SequenceEqual(Raw(source,true)),"original full vertex prefix");
            var fresh=new Mesh{name="Exact131ControlReplay",indexFormat=source.indexFormat};try
            {
                fresh.SetVertexBufferParams((int)recipe["replacement_vertex_count"],source.GetVertexAttributes());fresh.SetVertexBufferData(vertices,0,0,vertices.Length,0,MeshUpdateFlags.DontRecalculateBounds);fresh.SetIndexBufferParams(indices.Length/4,IndexFormat.UInt32);fresh.SetIndexBufferData(indices,0,0,indices.Length,MeshUpdateFlags.DontRecalculateBounds);fresh.subMeshCount=1;fresh.SetSubMesh(0,new SubMeshDescriptor(0,indices.Length/4,MeshTopology.Triangles));fresh.RecalculateBounds();
                var terminal=AssetDatabase.LoadAssetAtPath<Mesh>((string)r["derived_asset"]["path"]);Need(terminal!=null&&!EditorUtility.IsDirty(terminal)&&D(fresh)==(string)p["candidate_unity_digests"]["complete_channels_sha256"]&&D(terminal)==D(fresh)&&Raw(terminal,true).SequenceEqual(vertices)&&Raw(terminal,false).SequenceEqual(indices),"fresh sealed all-channel clip and exact terminal");return terminal;
            }
            finally{UnityEngine.Object.DestroyImmediate(fresh);}
        }
        static Mesh PrecisionFresh(Mesh source,Matrix4x4 frame,JToken prior)
        {
            Need(H(PrecisionReceipt)=="62d9f42f7ebe8ed689999508968900ec4df45d2385ee6bf364140d05c41d3eb2","frozen electrical-precision receipt");var saved=J(PrecisionReceipt);Need(H((string)saved["request"])==(string)saved["request_sha256"],"exact precision request");foreach(var q in J((string)saved["request"])["files"])Need(H((string)q["path"])==(string)q["sha256"],"frozen precision131-source evidence");var r=saved["owner_changes"].Single(q=>(string)q["owner"]==PrecisionOwner);Pin(r["source_asset"]);Pin(r["derived_asset"]);Need(H((string)r["source_recipe"])==(string)r["source_recipe_sha256"],"exact precision recipe");var recipe=J((string)r["source_recipe"]);var c=recipe["owners"].Single(q=>(string)q["owner"]==PrecisionOwner);
            Need(source!=null&&(string)prior["path"]==PrecisionOwner&&(string)prior["replacement_asset"]==(string)r["source_asset"]["path"]&&(string)prior["replacement_guid"]==(string)r["source_asset"]["guid"]&&OfficialFloorplanArchitectureRegistry.MeshHash(source)==(string)prior["replacement_payload_sha256"]&&D(source)==(string)prior["replacement_complete_channels_sha256"],"exact precision historical source");for(int a=0;a<4;a++)for(int b=0;b<4;b++)Need(float.IsFinite(frame[a,b])&&Mathf.Abs(frame[a,b]-(float)c["current_world_matrix"][a][b])<.00004f&&Mathf.Abs(frame[a,b]-(float)prior["world_matrix"][a][b])<.00004f,"exact precision frame");
            string vp=(string)recipe["vertices_path"],ip=(string)recipe["indices_path"];Need(H(vp)==(string)recipe["vertices_sha256"]&&H(ip)==(string)recipe["indices_sha256"],"precision all-channel recipe bytes");var vertices=File.ReadAllBytes(vp);var indices=File.ReadAllBytes(ip);Need(vertices.Take(Raw(source,true).Length).SequenceEqual(Raw(source,true)),"precision original full vertex prefix");var fresh=new Mesh{name="ExactPrecision131Replay",indexFormat=source.indexFormat};try{fresh.SetVertexBufferParams((int)recipe["vertex_count"],source.GetVertexAttributes());fresh.SetVertexBufferData(vertices,0,0,vertices.Length,0,MeshUpdateFlags.DontRecalculateBounds);fresh.SetIndexBufferParams(indices.Length/4,IndexFormat.UInt32);fresh.SetIndexBufferData(indices,0,0,indices.Length,MeshUpdateFlags.DontRecalculateBounds);fresh.subMeshCount=1;fresh.SetSubMesh(0,new SubMeshDescriptor(0,indices.Length/4,MeshTopology.Triangles));fresh.RecalculateBounds();var terminal=AssetDatabase.LoadAssetAtPath<Mesh>((string)r["derived_asset"]["path"]);Need(terminal!=null&&!EditorUtility.IsDirty(terminal)&&D(fresh)==D(terminal)&&Raw(terminal,true).SequenceEqual(vertices)&&Raw(terminal,false).SequenceEqual(indices),"exact fresh precision all-channel terminal");return terminal;}finally{UnityEngine.Object.DestroyImmediate(fresh);}
        }
        // The fresh legacy-kit chain also changes six render owners. It is outside this five-MC adapter.
        // Reject its rebuild before writes until that complete chain has a separately reviewed replay.
        public static void BeforeRebuild(string root)
        {
            if(File.Exists(Receipt)&&Owners.Concat(new[]{PrecisionOwner}).Any(p=>p.StartsWith(root.Trim('/')+"/",StringComparison.Ordinal)))
                throw new InvalidDataException("Approved131-control negative closure requires the complete reviewed MR/MC rebuild chain; legacy-root rebuild is rejected before writes.");
        }
        public static bool TryVerify(MeshCollider collider,JToken prior)
        {
            string owner=(string)prior["path"];if(!(Owners.Contains(owner)||owner==PrecisionOwner)||!File.Exists(owner==PrecisionOwner?PrecisionReceipt:Receipt))return false;Need(Platform6BankB1Replay.PathOf(collider.transform)==owner,"exact live owner");var row=J(owner==PrecisionOwner?PrecisionReceipt:Receipt)["owner_changes"].Single(q=>(string)q["owner"]==owner);var source=AssetDatabase.LoadAssetAtPath<Mesh>((string)row["source_asset"]["path"]);var terminal=ResolveFresh(source,owner,collider.transform.localToWorldMatrix,prior);
            Need(collider.sharedMesh==terminal&&JToken.DeepEquals(Platform6BanksB24PlacementReplay.CarrierFields(collider.transform),row["after"]),"complete saved live owner fields");long expected=owner==PrecisionOwner?919378126:(long)J(Packet)["owners"].SelectMany(q=>q["paired_colliders"]).Single(q=>(string)q["owner"]==owner)["component_id"];Need((long)GlobalObjectId.GetGlobalObjectIdSlow(collider).targetObjectId==expected,"exact collider ID");return true;
        }
    }
}
#endif
