#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
namespace ChooGuard.Editor.Twin
{
    // Opt-in composition only. The original FloorGuide resolver must validate the generated
    // source first; this bridge accepts that exact saved intermediate, not an arbitrary mesh.
    public static class Main1FPostGuideWallReplay
    {
        const string Scope="main1f57-source-walls-pass1";
        static void Need(bool b,string m){if(!b)throw new InvalidDataException(m);}
        static bool Scoped(JToken r)=>(string)r["exact_retirement_scope"]==Scope;
        static void Proof(JToken row)
        {
            string path=(string)row["exact_recipe_path"],proofPath=(string)row["prepare_proof_path"];
            Need(OfficialFloorplanArchitectureRegistry.Hash(path)==(string)row["exact_recipe_sha256"]&&OfficialFloorplanArchitectureRegistry.Hash(proofPath)==(string)row["prepare_proof_sha256"],"Exact1F replay evidence changed");
            var recipe=JObject.Parse(File.ReadAllText(path))["component_recipes"].Single(x=>(string)x["component_global_id"]==(string)row["source_component_global_id"]);
            Need((string)recipe["mesh_kind"]=="MeshRenderer"&&recipe["keep_triangle_ordinals"].Count()>0&&(string)recipe["owner_path"]==(string)row["owner"]&&(string)recipe["asset_path"]==(string)row["source_asset"],"Exact1F replay owner/partition mismatch");
            var proof=JObject.Parse(File.ReadAllText(proofPath));var q=proof["components"].Single(x=>(string)x["component_global_id"]==(string)row["source_component_global_id"]);
            Need((string)proof["status"]=="PREPARED_READONLY"&&(string)q["source_complete_channels_sha256"]==(string)row["source_complete_channels_sha256"]&&(string)q["replacement_complete_channels_sha256"]==(string)row["replacement_complete_channels_sha256"],"Exact1F replay source/replacement proof changed");
            string src=(string)row["source_asset"];var source=AssetDatabase.LoadAssetAtPath<Mesh>(src);
            Need(source!=null&&!EditorUtility.IsDirty(source)&&OfficialFloorplanArchitectureRegistry.Hash(src)==(string)recipe["asset_sha256"]&&AssetDatabase.AssetPathToGUID(src)==(string)recipe["asset_guid"]&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(source)==(string)row["source_complete_channels_sha256"],"Exact1F replay intermediate asset changed");
        }
        static JToken Guide(JToken row)
        {
            if(!File.Exists(FloorGuideLayoutRegistry.Registry))return null;
            var matches=JObject.Parse(File.ReadAllText(FloorGuideLayoutRegistry.Registry))["meshes"].Where(x=>(string)x["replacement_asset"]==(string)row["source_asset"]).ToArray();
            Need(matches.Length<=1,"Ambiguous1F FloorGuide intermediate");if(matches.Length==0)return null;
            Need((string)matches[0]["replacement_digest"]==(string)row["source_complete_channels_sha256"],"FloorGuide intermediate attributes differ from sealed1F replay source");return matches[0];
        }
        // ArchitecturalCorrectionRegistry.SourceAssetsForRoot calls this for each row.
        public static string SourceForEnumeration(JToken row)
        {
            if(!Scoped(row))return (string)row["source_asset"];Proof(row);var guide=Guide(row);return guide==null?(string)row["source_asset"]:(string)guide["source_asset"];
        }
        // Insert immediately after FloorGuide.ResolveBaselineForRebuild at the four builder
        // generation/preflight sites. Pass the actual owner and actual matrix, never saved ones.
        public static Mesh ResolveAfterFloorGuide(Mesh resolved,string authoredSource,string owner,Matrix4x4 actualWorld)
        {
            if(!File.Exists(ArchitecturalCorrectionRegistry.Registry))return resolved;
            var rows=JObject.Parse(File.ReadAllText(ArchitecturalCorrectionRegistry.Registry))["entries"].Where(Scoped).Where(x=>(string)x["owner"]==owner).ToArray();
            if(rows.Length==0)return resolved;Need(rows.Length==1,"Ambiguous exact1F replay owner");var row=rows[0];Proof(row);var guide=Guide(row);
            if(guide==null)return resolved;Need((string)guide["source_asset"]==authoredSource,"Unexpected authored1F source after FloorGuide");
            if(resolved!=null&&AssetDatabase.GetAssetPath(resolved)==(string)row["replacement_asset"])
            {
                var source=AssetDatabase.LoadAssetAtPath<Mesh>((string)row["source_asset"]);
                var expected=ArchitecturalCorrectionRegistry.ResolveForRebuild(source,(string)row["source_asset"],owner,actualWorld);
                Need(expected==resolved,"Installed1F final partition asset changed");return resolved;
            }
            Need(resolved!=null&&AssetDatabase.GetAssetPath(resolved)==(string)row["source_asset"]&&ArchitecturalCorrectionRegistry.CompleteChannelDigest(resolved)==(string)row["source_complete_channels_sha256"],"Generated source did not resolve to exact1F FloorGuide intermediate");
            return ArchitecturalCorrectionRegistry.ResolveForRebuild(resolved,(string)row["source_asset"],owner,actualWorld);
        }
    }
}
#endif
