#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor.Twin
{
    // Post-automatic-door-split native0 floor terminal only. No scene/asset writes.
    public static class Native0SourceXFloorReplay
    {
        const string Packet="workers/twin/output/source-X-native0-residue-20261010/native0-source-X-floor-candidate.json";
        const string PacketSha="e624eeb8028a81fd38e732eae3ad2ac417c9b199922e739f2a7b88df8e7cbaa7";
        const string Owner="FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_0";
        const string RenderBackground="Assets/ChooGuard/Art/Fps/NativeAutomaticDoors20261009/native-door-render-background-2809044.asset";
        const string ColliderBackground="Assets/ChooGuard/Art/Fps/NativeAutomaticDoors20261009/native-door-collision-background-2809044.asset";
        static void Need(bool ok,string why){if(!ok)throw new InvalidDataException("Native0 source X floor: "+why);}
        static string Hash(string p)=>OfficialFloorplanArchitectureRegistry.Hash(p);
        static string Digest(Mesh m)=>ArchitecturalCorrectionRegistry.CompleteChannelDigest(m);
        static JObject Read()
        {
            Need(Hash(Packet)==PacketSha,"frozen exact candidate changed");var p=JObject.Parse(File.ReadAllText(Packet));
            Need((string)p["schema"]=="chooguard.native0-source-X-floor-candidate.v1"&&p["rows"].Count()==2&&p["original_native_door_assets13"].Count()==13,"exact two-background/13-original contract changed");
            foreach(var pair in new[]{("source_controls","source_controls_sha256"),("original_native_automatic_door_receipt","original_native_automatic_door_receipt_sha256"),("original_native_automatic_door_packet","original_native_automatic_door_packet_sha256")})Need(Hash((string)p[pair.Item1])==(string)p[pair.Item2],"original source authority/automatic-door installation changed");
            foreach(var row in p["original_native_door_assets13"]){string asset=(string)row["asset"];Need(Hash(asset)==(string)row["file_sha256"]&&Hash(asset+".meta")== (string)row["meta_sha256"]&&AssetDatabase.AssetPathToGUID(asset)==(string)row["guid"],"original automatic-door asset changed");}
            return p;
        }
        static void Frame(Matrix4x4 actual,JToken expected)
        {for(int r=0;r<4;r++)for(int c=0;c<4;c++)Need(float.IsFinite(actual[r,c])&&Mathf.Abs(actual[r,c]-(float)expected[r][c])<.00004f,"original native0 frame changed");}
        public static string VerifyOriginalDependencies(){Read();return "ORIGINAL_NATIVE_AUTOMATIC_DOOR_13_ASSETS_AND_RECEIPT_PRESERVED";}
        public static Mesh ResolveAfterAutomaticDoorSplit(Mesh exactBackground,string sourceBackground,string owner,Matrix4x4 frame)
        {
            Need(sourceBackground==RenderBackground||sourceBackground==ColliderBackground,"unreviewed background role");
            var p=Read();var row=p["rows"].Single(r=>(string)r["source_asset"]==sourceBackground);Need(owner==Owner,"native0 owner changed");Frame(frame,row["owner"]["world_matrix"]);
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(sourceBackground);Need(old!=null&&!EditorUtility.IsDirty(old)&&AssetDatabase.AssetPathToGUID(sourceBackground)==(string)row["source_guid"]&&Hash(sourceBackground)==(string)row["source_file_sha256"]&&Digest(old)==(string)row["source_complete_channel_digest"],"exact retained automatic-door background changed");
            var terminal=LegacyWestRetailArchitectureRegistry.ResolveSavedDerivative(old);
            Need(terminal!=old&&EditorUtility.IsPersistent(terminal)&&!EditorUtility.IsDirty(terminal)&&AssetDatabase.GetAssetPath(terminal)==(string)row["replacement_asset"]&&Digest(terminal)==(string)p["replacement_digests"]["complete_channels_sha256"],"exact installed native0 floor terminal missing/changed");
            string digest=Digest(exactBackground);Need(digest==(string)row["source_complete_channel_digest"]||(exactBackground==terminal&&digest==Digest(terminal)),"input lacks exact prior automatic-door split; no raw native/ordinal rebase");return terminal;
        }
        static bool HasTerminals()=>LegacyWestRetailArchitectureRegistry.HasCorrection(RenderBackground)||LegacyWestRetailArchitectureRegistry.HasCorrection(ColliderBackground);
        public static string VerifyInstalled()
        {
            Need(HasTerminals(),"native0 terminal registration missing");var p=Read();var go=GameObject.Find("/"+Owner);Need(go!=null&&go.activeInHierarchy,"exact active native0 owner missing");
            var f=go.GetComponent<MeshFilter>();var r=go.GetComponent<MeshRenderer>();var c=go.GetComponent<MeshCollider>();Need(f!=null&&r!=null&&r.enabled&&r.additionalVertexStreams==null&&c!=null&&c.enabled&&!c.isTrigger,"native0 render/collider state changed");
            foreach(var row in p["rows"])
            {
                bool render=(string)row["role"]=="native0-floor-render";Component component=render?(Component)r:c;
                Need(GlobalObjectId.GetGlobalObjectIdSlow(component).ToString()==(string)row["owner"]["global_object_id"],"exact native0 component identity changed");
                Mesh current=render?f.sharedMesh:c.sharedMesh;Need(current==ResolveAfterAutomaticDoorSplit(current,(string)row["source_asset"],Owner,go.transform.localToWorldMatrix),"native0 final binding changed");
                if(render)Need(GlobalObjectId.GetGlobalObjectIdSlow(f).ToString()==(string)row["owner"]["filter_global_object_id"]&&r.sharedMaterials.Select(AssetDatabase.GetAssetPath).SequenceEqual(row["owner"]["materials"].Select(x=>(string)x)),"native0 filter/material slots changed");
            }
            return "EXACT_NATIVE0_SOURCE_X_FLOOR_RENDER_COLLIDER_VERIFIED";
        }
        // Existing SourceOverrides apply/revert preflights call Namcheon native protection.
        // Once this terminal is registered, raw original replacement must also reject here.
        public static void RejectUncomposedOriginalReplacement(GameObject go)
        {
            if(go==null||LegacyWestRetailArchitectureRegistry.PathOf(go.transform)!=Owner||!HasTerminals())return;
            VerifyInstalled();throw new InvalidDataException("Native0 retains exact automatic-door face separation and source-X floor composition. Direct original replacement is rejected before writes; use the reviewed post-automatic-door-split resolver.");
        }
    }
}
#endif
