// Root-review draft: copy into Assets/ChooGuard/Editor/Twin after installation.
// This file depends only on the production Editor assembly; no AgentScript types.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using ChooGuard.App.Fps.Facilities;

namespace ChooGuard.Editor.Twin
{
    public static class Floor1ElectricalRearWallRegistry
    {
        const string FilePath="content/world/observed-interiors/floor1-electrical-rear-wall.json";
        const string Packet="workers/twin/output/floor1-architecture-20261009-pass2/electrical-rear-wall-repair-packet.json";
        const string PacketSha="467eda9549402a6b2ca2432d791d896d2b968f8cbaf442fb40c352142ffdea12";
        const string Owner="FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_5";
        const string RenderOriginal="Assets/ChooGuard/Art/StationInterior/PlatformCirculation20261009/Background/43090f67efeec44098bcf31fad5ac48a_OfficialStation_-부산역_5.asset";
        const string ColliderOriginal="Assets/ChooGuard/Art/Fps/SourceParts20261008/SeparatedBackground/OfficialStation_-부산역_5.asset";
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("1F source rear replay: "+why);}
        static string Hash(string p){using(var h=SHA256.Create())using(var f=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
        static string Digest(Mesh mesh)=>ArchitecturalCorrectionRegistry.CompleteChannelDigest(mesh);
        static JObject ReadPacket(){Require(Hash(Packet)==PacketSha,"Frozen packet changed");return JObject.Parse(File.ReadAllText(Packet));}
        static JObject Installed(){var p=JObject.Parse(File.ReadAllText(FilePath));Require((string)p["packet_sha256"]==PacketSha,"Installed registry packet changed");return p;}
        static string Role(string source)
        {
            if(source==RenderOriginal||source=="Assets/ChooGuard/Art/StationInterior/FloorGuideLayout20261009/2f-passages/2F-002.asset")return "native-shell5-render";
            if(source==ColliderOriginal||source=="Assets/ChooGuard/Art/StationInterior/FloorGuideLayout20261009/2f-passages/2F-003.asset")return "native-shell5-collider";
            return null;
        }
        public static bool HasNativeCorrection(string source)=>File.Exists(FilePath)&&Role(source)!=null;
        static Mesh Output(JToken row,JObject saved)
        {
            var dep=saved["dependencies"].Single(d=>(string)d["role"]==(string)row["role"]);string path=(string)dep["asset"];var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Require(m!=null&&AssetDatabase.AssetPathToGUID(path)==(string)dep["guid"]&&Hash(path)==(string)dep["file_sha256"]&&Digest(m)==(string)row["expected_output"]["expected_complete_channel_digest"],"Installed native dependency changed");return LegacyWestRetailArchitectureRegistry.ResolveSavedDerivative(m);
        }
        static void Resources(JObject packet,JObject saved)
        {
            Require(Hash((string)packet["registration"])==(string)packet["registration_sha256"]&&Hash((string)packet["source_pdf_raster"])==(string)packet["source_pdf_raster_sha256"],"Source PDF/localization changed");
            Require(Hash((string)packet["new_wall_kit"])==(string)packet["new_wall_kit_sha256"],"One-wall source kit changed");
            foreach(var row in packet["mesh_patches"]){var src=row["full_channel_export"];Require(Hash((string)src["asset"])==(string)row["source_live_asset_sha256"]&&AssetDatabase.AssetPathToGUID((string)src["asset"])==(string)src["guid"]&&Hash((string)src["path"])==(string)src["binary_sha256"],"Pinned native platform-guide baseline changed");Require(Hash((string)row["expected_output"]["path"])==(string)row["expected_output"]["binary_sha256"],"Reviewed native output changed");Output(row,saved);}
            foreach(var dep in saved["source_wall_meshes"]){string path=(string)dep["path"];var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);Require(m!=null&&AssetDatabase.AssetPathToGUID(path)==(string)dep["guid"]&&Hash(path)==(string)dep["file_sha256"]&&Digest(m)==(string)dep["complete_channel_digest"],"Installed source rear-wall mesh changed");}
        }
        public static Mesh ResolveAfterPlatformGuide(Mesh completeGuideBaseline,string source,string owner,Matrix4x4 frame)
        {
            if(!HasNativeCorrection(source))return completeGuideBaseline;var packet=ReadPacket();var saved=Installed();Resources(packet,saved);string role=Role(source);var row=packet["mesh_patches"].Single(r=>(string)r["role"]==role);Require(owner==Owner,"Native owner changed");for(int r=0;r<4;r++)for(int c=0;c<4;c++)Require(float.IsFinite(frame[r,c])&&Mathf.Abs(frame[r,c]-(float)row["source_world_matrix"][r][c])<.00004f,"Native source frame changed");
            Mesh terminal=Output(row,saved);string digest=Digest(completeGuideBaseline);Require(digest==(string)row["full_channel_export"]["complete_channel_digest"]||digest==(string)row["expected_output"]["expected_complete_channel_digest"]||(completeGuideBaseline==terminal&&digest==Digest(terminal)),"Fresh native mesh lacks exact prior platform-guide composition or verified terminal identity; clipping is not remapped");return terminal;
        }
        // Root adds this at the start of ArchitecturalCorrectionRegistry.ResolveForRebuild
        // for these two native sources. An uncomposed legacy source is rejected BEFORE
        // any asset write. A reviewed native rebuild composes the existing platform-guide
        // openings first, then calls ResolveAfterPlatformGuide on the resulting mesh.
        public static Mesh ResolveOrRejectBeforeNativeReplacement(Mesh candidate,string source,string owner,Matrix4x4 frame)=>ResolveAfterPlatformGuide(candidate,source,owner,frame);
        public static void ValidateWallBeforeRebuild(string spec,string root)
        {
            if(!File.Exists(FilePath))return;var p=ReadPacket();var s=Installed();Resources(p,s);if(root!=(string)s["source_wall_root"])return;Require(Path.GetFullPath(spec)==Path.GetFullPath((string)p["new_wall_kit"])&&Hash(spec)==(string)p["new_wall_kit_sha256"],"One-wall root may only rebuild from its exact source kit; do not also append the same wall to the83-element root");
        }
        public static void ValidateSourceDoorBeforeRebuild(string spec,string root)
        {
            if(!File.Exists(FilePath)||root!="실내 트윈 마감/1F PDF 원도 복원")return;var p=ReadPacket();Resources(p,Installed());string hash=Hash(spec);Require(hash==(string)p["original_source_spec_sha256"]||hash==(string)p["source_corrected_A_only_spec_sha256"],"Fresh83-element source root must retain original source doorA373..386; preserve the separate one-wall root and never append wall84 or translate A");
        }
        public static void ValidateSourceDoorAfterRebuild(string root)
        {
            if(!File.Exists(FilePath)||root!="실내 트윈 마감/1F PDF 원도 복원")return;var p=ReadPacket();var obj=GameObject.Find("/"+root);Require(obj!=null,"Source operator root missing");var doors=obj.GetComponentsInChildren<StationDoor>(true);Require(doors.Length==24&&doors.Sum(d=>d.Leaves.Length)==28,"Source operator/leaf count changed");var a=doors.Single(d=>d.Element=="electric-rear-double-a");var v=p["door_a_source_pose_and_operation"]["expected_world_centre"];Require(Vector3.Distance(a.World(Vector3.zero),new Vector3((float)v[0],(float)v[1],(float)v[2]))<.0001f&&a.Leaves.Length==2&&Mathf.Abs(a.Leaves[0].Swing+90)<.001f&&Mathf.Abs(a.Leaves[1].Swing-90)<.001f,"Door A source translation duplicated or modeled sweep not restored");
        }
    }
}
