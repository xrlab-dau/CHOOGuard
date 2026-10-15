#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.Editor.Twin
{
    // Exact reviewed source successors. Original packets/assets remain immutable.
    public static class Source1FReviewedRepairReplay
    {
        public const string WestReceipt="content/world/observed-interiors/source1f-west-split2-corrected-20261010.json";
        public const string WestRoot="Source1F_WestSplit2_Corrected_20261010";
        public const string WestOwner=WestRoot+"/PLAN1F-WEST-SPLIT2-CORRECTED";
        const string Packet="workers/twin/output/architecture-resume-20261010/partition-correction-packet.json";
        const string PacketSHA="4b5ab1eddaeb1dce69e8cd0155f518d55f97ac4d44e8898b0893f6fa7755d841";
        static void Need(bool b,string why) { if(!b)throw new InvalidDataException("Reviewed1F replay: "+why); }
        static string H(string p)=>ConcourseFidelityCorrection.Hash(p);
        static JObject J(string p)=>JObject.Parse(File.ReadAllText(p));
        static Vector3 V(JToken p)=>new Vector3((float)p[0],(float)p[1],(float)p[2]);
        static void Pin(JToken p) { string path=(string)p["path"];Need(H(path)==(string)p["sha256"]&&H(path+".meta")== (string)p["meta_sha256"]&&AssetDatabase.AssetPathToGUID(path)==(string)p["guid"],"saved asset pin "+path); }
        static JObject West()
        {
            Need(File.Exists(WestReceipt)&&H(Packet)==PacketSHA,"installed receipt and frozen corrected packet");var r=J(WestReceipt);Need((string)r["packet"]==Packet&&(string)r["packet_sha256"]==PacketSHA&&(string)r["owner"]==WestOwner,"exact installed source owner");
            var p=J(Packet);foreach(string k in new[]{"source","spec","frame"})Need(H((string)p[k])==(string)p[k+"_sha256"],"source/frame "+k);
            Need(p["objects"].Count()==1&&(string)p["objects"][0]["id"]=="PLAN1F-WEST-SPLIT2-CORRECTED"&&JToken.DeepEquals(p["objects"][0]["source_centerline_px"],new JArray(new JArray(24.5,164.0),new JArray(44.5,152.5))),"only source-qualified corrected trace; obsolete held trace forbidden");
            Need(H((string)r["completion_seal"])==(string)r["completion_seal_sha256"],"frozen complete nonduplicate proof seal");Pin(r["asset"]);Pin(r["material"]);return r;
        }
        // Fresh construction from the frozen corrected source packet, never from a live mesh copy.
        public static Mesh GenerateWestFromSource()
        {
            West();var row=J(Packet)["objects"].Single();var m=new Mesh{name=(string)row["id"],indexFormat=IndexFormat.UInt32};
            m.vertices=row["vertices"].Select(V).ToArray();m.normals=row["normals"].Select(V).ToArray();m.uv=row["uv"].Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray();m.triangles=row["triangles"].Select(x=>(int)x).ToArray();m.RecalculateBounds();return m;
        }
        public static Mesh ResolveWestFresh(Mesh generated,string owner,Matrix4x4 world)
        {
            var r=West();Need(owner==WestOwner&&generated!=null&&!EditorUtility.IsPersistent(generated)&&world==Matrix4x4.identity,"fresh exact identity-frame west owner");var m=AssetDatabase.LoadAssetAtPath<Mesh>((string)r["asset"]["path"]);Need(m!=null&&!EditorUtility.IsDirty(m),"clean persistent reviewed mesh");
            Need(generated.indexFormat==m.indexFormat&&generated.GetVertexAttributes().SequenceEqual(m.GetVertexAttributes())&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated)==(string)r["complete_channels_sha256"]&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(m)==(string)r["complete_channels_sha256"],"complete ordered fresh/source channels differ");
            Need(generated.bounds==m.bounds,"fresh source bounds differ");return m;
        }
        // Explicit fresh builder entry. Recreating IDs requires a separately reviewed new receipt;
        // existing builders preserve the separate installed root and its saved IDs.
        public static GameObject BuildWestRoot()
        {
            Need(GameObject.Find("/"+WestRoot)==null,"west root already exists; refuse duplicate");var r=West();var fresh=GenerateWestFromSource();Mesh mesh;
            try{mesh=ResolveWestFresh(fresh,WestOwner,Matrix4x4.identity);}finally{UnityEngine.Object.DestroyImmediate(fresh);}
            var root=new GameObject(WestRoot);try{var go=new GameObject("PLAN1F-WEST-SPLIT2-CORRECTED");go.transform.SetParent(root.transform,false);go.layer=(int)r["owner_state"]["gameobject"]["GameObject"]["m_Layer"];go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>((string)r["material"]["path"]);var c=go.AddComponent<MeshCollider>();c.cookingOptions=(MeshColliderCookingOptions)30;c.sharedMesh=mesh;return root;}catch{UnityEngine.Object.DestroyImmediate(root);throw;}
        }
        public static void VerifyWestInstalled()
        {
            if(!File.Exists(WestReceipt))
            {
                Need(!UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Any(g=>g.name==WestRoot),"installed west root exists but canonical receipt is missing");return;
            }
            var r=West();var t=Platform6BankB1Replay.Find(WestOwner);Need(t.localToWorldMatrix==Matrix4x4.identity&&JToken.DeepEquals(Platform6BanksB24PlacementReplay.CarrierFields(t),r["owner_state"]),"exact saved west owner fields");
            Need((long)GlobalObjectId.GetGlobalObjectIdSlow(t.GetComponent<MeshFilter>()).targetObjectId==(long)r["filter_id"]&&(long)GlobalObjectId.GetGlobalObjectIdSlow(t.GetComponent<MeshRenderer>()).targetObjectId==(long)r["renderer_id"]&&(long)GlobalObjectId.GetGlobalObjectIdSlow(t.GetComponent<MeshCollider>()).targetObjectId==(long)r["collider_id"],"exact saved west component IDs");
            var fresh=GenerateWestFromSource();try{var mesh=ResolveWestFresh(fresh,WestOwner,t.localToWorldMatrix);Need(t.GetComponent<MeshFilter>().sharedMesh==mesh&&t.GetComponent<MeshCollider>().sharedMesh==mesh,"render/physical same reviewed source");}finally{UnityEngine.Object.DestroyImmediate(fresh);}
            Need(JToken.DeepEquals(Platform6BanksB24PlacementReplay.CarrierFields(Platform6BankB1Replay.Find((string)r["adjacent_owner"])),r["adjacent_state"]),"retained adjacent source changed");
        }
        public const string Owner032="공식1F MAIN 원문벽57 common_source_v1/wall/PLAN1F-WALL-032";
        public const string Receipt032="content/world/observed-interiors/source1f-032-duplicate-west-removed-20261010.json";
        const string Packet032="workers/twin/output/architecture-resume-20261010/next032-remove-only/packet.json";
        const string Packet032SHA="d2e2e678ed5bd43687045ec3ceea79310dc23c1876f15414f1ed69ac9d0372ab";
        const string Original032="Assets/ChooGuard/Art/StationInterior/OfficialFloorplan/Main1F57_common_source_v1/PLAN1F-WALL-032.asset";
        const string OriginalPackage032=".planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/main1f-wall-mesh-package.json";
        public static bool Has032Receipt=>File.Exists(Receipt032);
        static byte[] VerticesRaw(Mesh m) { using(var data=Mesh.AcquireReadOnlyMeshData(m))return data[0].GetVertexData<byte>(0).ToArray(); }
        static byte[] IndicesRaw(Mesh m) { using(var data=Mesh.AcquireReadOnlyMeshData(m))return data[0].GetIndexData<byte>().ToArray(); }
        static string BytesHash(byte[] b) { using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant(); }
        static JObject Read032()
        {
            Need(Has032Receipt&&H(Packet032)==Packet032SHA,"exact installed032 removal-only packet");var r=J(Receipt032);var p=J(Packet032);
            Need((string)r["packet"]==Packet032&&(string)r["packet_sha256"]==Packet032SHA&&(string)r["owner"]==Owner032,"exact032 source lineage");
            Need(H(Original032)==(string)p["source_asset_sha256"]&&H(Original032+".meta")== (string)p["source_meta_sha256"]&&AssetDatabase.AssetPathToGUID(Original032)=="1b3432285ca3f4328af254b79a403a85","immutable original032 asset");
            Need(H((string)p["frame"])==(string)p["frame_sha256"]&&H(OriginalPackage032)=="99dff75c46141e707d04142ab3a22382fa43a85000148ed2cd89ff1e7e311dc4","original source package/frame");
            Need(H((string)r["completion_seal"])==(string)r["completion_seal_sha256"],"frozen032 complete proof seal");Pin(r["asset"]);return r;
        }
        static Mesh Terminal032(JObject r)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>((string)r["asset"]["path"]);var original=AssetDatabase.LoadAssetAtPath<Mesh>(Original032);
            Need(mesh!=null&&original!=null&&!EditorUtility.IsDirty(mesh)&&mesh.vertexCount==228&&mesh.subMeshCount==1&&mesh.indexFormat==IndexFormat.UInt32&&mesh.GetTopology(0)==MeshTopology.Triangles&&mesh.GetIndices(0).Length==120&&mesh.GetIndices(0).All(i=>i>=132&&i<228),"exact closed-remnant topology without referenced old prefix");
            Need(BytesHash(VerticesRaw(mesh))=="bb3a583d507e0b45fc74f6f24ba09eb8ed276e923db26a14763514f0deab9966"&&BytesHash(IndicesRaw(mesh))=="1715a79e562fb5a6c36a80ac80cb4ad9efc8f84c001ccfb18795b8ff3942c718"&&VerticesRaw(mesh).Take(132*32).SequenceEqual(VerticesRaw(original)),"exact supplied vertex/index bytes and untouched original prefix");
            Need(mesh.GetVertexAttributes().SequenceEqual(original.GetVertexAttributes())&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(mesh)==(string)r["complete_channels_sha256"],"complete terminal032 channels");return mesh;
        }
        public static Mesh Generate032OriginalFromSource()
        {
            Read032();var row=J(OriginalPackage032)["objects"].Single(p=>(string)p["id"]=="PLAN1F-WALL-032");var m=new Mesh{name="PLAN1F-WALL-032",indexFormat=IndexFormat.UInt32};m.vertices=row["vertices"].Select(V).ToArray();m.triangles=row["triangles"].Select(x=>(int)x).ToArray();m.uv=row["uv"].Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray();m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        public static Mesh Resolve032Fresh(Mesh generated,string owner,Matrix4x4 world)
        {
            var r=Read032();Need(owner==Owner032&&generated!=null&&!EditorUtility.IsPersistent(generated)&&world==Matrix4x4.identity,"fresh exact032 source/identity frame");var original=AssetDatabase.LoadAssetAtPath<Mesh>(Original032);
            Need(generated.indexFormat==original.indexFormat&&generated.GetVertexAttributes().SequenceEqual(original.GetVertexAttributes())&&VerticesRaw(generated).SequenceEqual(VerticesRaw(original))&&IndicesRaw(generated).SequenceEqual(IndicesRaw(original))&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated)==(string)r["original_complete_channels_sha256"],"fresh original032 complete source bytes differ");return Terminal032(r);
        }
        // Compose only the original032 pin with its exact reviewed terminal; all other rows retain their old contract.
        public static bool TryVerify032Owner(MeshFilter f,JToken originalPin)
        {
            if((string)originalPin["path"]!=Owner032||!Has032Receipt)return false;var r=Read032();var original=AssetDatabase.LoadAssetAtPath<Mesh>(Original032);var t=f.transform;var mc=t.GetComponent<MeshCollider>();var mr=t.GetComponent<MeshRenderer>();
            Need((string)originalPin["asset"]==Original032&&(string)originalPin["guid"]=="1b3432285ca3f4328af254b79a403a85"&&OfficialFloorplanArchitectureRegistry.MeshHash(original)==(string)originalPin["mesh_payload_sha256"],"immutable original collection032 pin");
            Need(Platform6BankB1Replay.PathOf(t)==Owner032&&t.localToWorldMatrix==Matrix4x4.identity&&f.sharedMesh==Terminal032(r)&&mc!=null&&mc.sharedMesh==f.sharedMesh&&mc.enabled&&!mc.isTrigger&&!mc.convex&&(int)mc.cookingOptions==30&&mr!=null&&mr.enabled&&mr.additionalVertexStreams==null&&mr.sharedMaterials.Length==1&&AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mr.sharedMaterial))=="2f455925b31a3446291227ddeada52fd","exact live032 mesh/material/physics/frame policy");
            Need((long)GlobalObjectId.GetGlobalObjectIdSlow(f).targetObjectId==200549806&&(long)GlobalObjectId.GetGlobalObjectIdSlow(mc).targetObjectId==200549804&&(long)GlobalObjectId.GetGlobalObjectIdSlow(mr).targetObjectId==200549805&&JToken.DeepEquals(Platform6BanksB24PlacementReplay.CarrierFields(t),r["owner_state_after"]),"exact saved032 IDs and complete serialized fields");
            foreach(var p in r["retained_Kit_owners"])Need(JToken.DeepEquals(Platform6BanksB24PlacementReplay.CarrierFields(Platform6BankB1Replay.Find((string)p["owner"])),p["state"]),"correct retained Kit owner changed");return true;
        }
    }
}
#endif
