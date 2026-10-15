#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Retain guide-aligned layouts when an older authored source batch is rebuilt.</summary>
    public static class FloorGuideLayoutRegistry
    {
        public const string Registry = "content/world/observed-interiors/floor-guide-layout.json";
        static string FileDigest(string path)
        {
            using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        static bool ProvenTriangleReorder(JToken row,Mesh original,Mesh generated,string digest)
        {
            if(!(row["verified_rebuild_variants"] is JArray variants))return false;
            foreach(var variant in variants.Where(v => (string)v["complete_digest"]==digest))
            {
                string path=(string)variant["certificate"];
                if(!File.Exists(path)||FileDigest(path)!=(string)variant["certificate_sha256"])continue;
                var proof=JObject.Parse(File.ReadAllText(path));
                if((string)proof["schema"]!="chooguard.two-source-regeneration-triangle-certificate.v1"||
                    (string)proof["source_asset"]!=(string)row["source_asset"]||
                    (string)proof["provided_original_complete_digest"]!=(string)row["source_digest"]||
                    (string)proof["provided_generated_complete_digest"]!=digest||
                    (bool?)proof["exact_oriented_position_UV_triangle_multiset_equal"]!=true||
                    (bool?)proof["exact_provided_normal_tangent_triangle_multiset_equal"]!=true||
                    (int)proof["unmatched_original_triangles"]!=0||(int)proof["unmatched_generated_triangles"]!=0||
                    (double)proof["normal_residual"]["maximum_absolute_component"]!=0||
                    (double)proof["tangent_residual"]["maximum_absolute_component"]!=0||
                    FileDigest((string)row["baseline_asset"])!=(string)proof["source_asset_file_sha256"]||
                    FileDigest((string)proof["probe_path"])!=(string)proof["probe_sha256"])continue;
                // The independent certificate contains these authored batch channels.
                // Reject any omitted channel instead of assuming it survived a reorder.
                if(original.vertexCount!=generated.vertexCount||original.subMeshCount!=1||generated.subMeshCount!=1||
                    original.GetTopology(0)!=MeshTopology.Triangles||generated.GetTopology(0)!=MeshTopology.Triangles||
                    original.blendShapeCount!=0||generated.blendShapeCount!=0||original.boneWeights.Length!=0||generated.boneWeights.Length!=0||
                    original.bindposes.Length!=0||generated.bindposes.Length!=0||original.colors.Length!=0||generated.colors.Length!=0)continue;
                bool extra=false;
                for(int channel=1;channel<8;channel++)
                {
                    var a=new System.Collections.Generic.List<Vector4>();var b=new System.Collections.Generic.List<Vector4>();
                    original.GetUVs(channel,a);generated.GetUVs(channel,b);extra|=a.Count!=0||b.Count!=0;
                }
                if(!extra)return true;
            }
            return false;
        }
        public static bool HasCorrection(string sourceAsset) => File.Exists(Registry) &&
            ((JArray)JObject.Parse(File.ReadAllText(Registry))["meshes"]).Any(r => (string)r["source_asset"] == sourceAsset);
        public static string[] SourceAssetsForRoot(string ownerRoot)
        {
            if (!File.Exists(Registry)) return Array.Empty<string>();
            string prefix=ownerRoot.Trim('/')+"/";
            var sources=new System.Collections.Generic.List<string>();
            var packets=new System.Collections.Generic.Dictionary<string,JObject>();
            foreach(var row in (JArray)JObject.Parse(File.ReadAllText(Registry))["meshes"])
            {
                string packetPath=(string)row["packet"];
                if(!packets.TryGetValue(packetPath,out var packet))
                {
                    using(var stream=File.OpenRead(packetPath))using(var sha=SHA256.Create())
                        if(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=(string)row["packet_sha256"])
                            throw new InvalidDataException("Saved guide source-owner evidence changed");
                    packet=JObject.Parse(File.ReadAllText(packetPath));packets.Add(packetPath,packet);
                }
                var patch=((JArray)packet["mesh_patches"]).Single(p => (string)p["source_asset"] == ((string)row["patch_baseline_asset"] ?? (string)row["baseline_asset"]));
                var owners=((JArray)patch["owners"]).Concat((JArray)patch["collider_owners"] ?? new JArray());
                if(owners.Any(o => ((string)o["path"]).StartsWith(prefix,StringComparison.Ordinal))) sources.Add((string)row["source_asset"]);
            }
            return sources.Distinct().ToArray();
        }
        public static Mesh ResolveForRebuild(Mesh generated, string sourceAsset)
        {
            if (!File.Exists(Registry)) return generated;
            var rows = ((JArray)JObject.Parse(File.ReadAllText(Registry))["meshes"])
                .Where(r => (string)r["source_asset"] == sourceAsset).ToArray();
            if (rows.Length == 0) return generated;
            if (rows.Length != 1) throw new InvalidDataException("Ambiguous guide layout source");
            var row = rows[0];
            var replacement = AssetDatabase.LoadAssetAtPath<Mesh>((string)row["replacement_asset"]);
            if (replacement == null || ArchitecturalCorrectionRegistry.CompleteChannelDigest(replacement) != (string)row["replacement_digest"])
                throw new InvalidDataException("Saved floor guide layout changed or missing");
            if (ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated) == (string)row["replacement_digest"])
                return replacement;
            // A source plan can be replayed at its old coordinates or the reviewed new coordinates.
            // Vertex attributes and topology must still identify the same original authored batch.
            var original = AssetDatabase.LoadAssetAtPath<Mesh>((string)row["baseline_asset"] ?? sourceAsset);
            if (original == null || ArchitecturalCorrectionRegistry.CompleteChannelDigest(original) != (string)row["source_digest"])
                throw new InvalidDataException("Original floor guide layout source changed");
            if(ProvenTriangleReorder(row,original,generated,ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated)))
                return replacement;
            var oldVertices = original.vertices; var newVertices = replacement.vertices; var actual = generated.vertices;
            if (actual.Length != oldVertices.Length || generated.subMeshCount != original.subMeshCount)
                throw new InvalidDataException("Guide layout source topology changed");
            for (int sub = 0; sub < original.subMeshCount; sub++)
                if (generated.GetTopology(sub) != original.GetTopology(sub) || !generated.GetIndices(sub).SequenceEqual(original.GetIndices(sub)))
                    throw new InvalidDataException("Guide layout source indices changed");
            bool oldPose = true, newPose = true;
            for (int i = 0; i < actual.Length; i++)
            {
                oldPose &= Vector3.Distance(actual[i], oldVertices[i]) < .0002f;
                newPose &= Vector3.Distance(actual[i], newVertices[i]) < .0002f;
            }
            if (!oldPose && !newPose) throw new InvalidDataException("Guide layout source contains an unreviewed geometry change");
            var attributeCheck = UnityEngine.Object.Instantiate(generated);
            try
            {
                attributeCheck.vertices = oldVertices;
                if (ArchitecturalCorrectionRegistry.CompleteChannelDigest(attributeCheck) != (string)row["source_digest"])
                    throw new InvalidDataException("Guide layout source vertex attributes changed");
            }
            finally { UnityEngine.Object.DestroyImmediate(attributeCheck); }
            return replacement;
        }
        // Called only while constructing a new instance at its original spec pose.
        // Scene installation applies the same reviewed delta separately.
        public static void ApplyInstancePose(Transform instance)
        {
            if (!File.Exists(Registry)) return;
            var parts = new System.Collections.Generic.List<string>();
            for (var t = instance; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse(); string path = string.Join("/", parts);
            var motions = (JArray)JObject.Parse(File.ReadAllText(Registry))["motions"];
            foreach (var row in motions ?? new JArray())
                if ((string)row["kind"] == "path" && (string)row["path"] == path)
                {
                    ApplyMotion(instance,row);
                }
        }
        public static void ApplyDoorPose(ChooGuard.App.Fps.Facilities.StationDoor door)
        {
            if (!File.Exists(Registry)) return;
            var motions = (JArray)JObject.Parse(File.ReadAllText(Registry))["motions"];
            foreach (var row in motions ?? new JArray())
                if ((string)row["kind"] == "door" && (string)row["door_element"] == door.Element)
                {
                    ApplyMotion(door.transform,row);
                }
        }
        static void ApplyMotion(Transform target,JToken row)
        {
            if(row["world_rigid_delta"] is JObject rigid)
            {
                var q=rigid["rotation"];var rotation=new Quaternion((float)q[0],(float)q[1],(float)q[2],(float)q[3]);
                if(Mathf.Abs(Quaternion.Dot(rotation,rotation)-1)>.00001f)throw new InvalidDataException("Invalid saved guide rigid rotation");
                var d=rigid["translation"];target.position=rotation*target.position+new Vector3((float)d[0],(float)d[1],(float)d[2]);target.rotation=rotation*target.rotation;
            }
            else{var d=row["world_delta"];target.position+=new Vector3((float)d[0],(float)d[1],(float)d[2]);}
        }
    }
}
#endif
