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
        const string RetainedShopRoot = "실내 트윈 마감/점포 내부 1F";
        const string RetainedShopFolder = "Assets/ChooGuard/Art/StationInterior/Kit/shops_1F";
        const string RawShopCollider = RetainedShopFolder + "/Kit_Colliders.asset";
        const string FixtureShopCollider = "Assets/ChooGuard/Art/StationInterior/ArchitectureCorrections/1F_KitFixturesOnly.asset";
        static readonly string[] RetainedShopCaps = { "Kit_CeilingGrid", "Kit_Light_조명기구_downlight", "Kit_ShopCeiling_Kit_GridWhite" };
        static readonly string[] RetainedShopSources = RetainedShopCaps.Select(n => RetainedShopFolder + "/" + n + ".asset").Concat(new[]{RawShopCollider}).ToArray();

        // Reenact authored source geometry; the canonical enclosure-owner policy is still false.
        // Only this pinned observed spec/root/folder may enter the retained-source generation lane.
        public static bool NeedsRetainedShopEnvelopeReplay(JObject spec, string specPath, string ownerRoot, string folder)
        {
            if(ownerRoot.Trim('/') != RetainedShopRoot || !File.Exists(Registry)) return false;
            var rows=(JArray)JObject.Parse(File.ReadAllText(Registry))["meshes"];
            var rawRows=rows.Where(r => (string)r["source_asset"] == RawShopCollider).ToArray();
            if(rawRows.Length==0) return false; // The unmigrated registry still fails its missing-source gate.
            if(rawRows.Length!=1) throw new InvalidDataException("Ambiguous retained shop collision source");
            var preprocessing=rawRows[0]["source_preprocess"] as JObject;
            if(preprocessing==null || (string)preprocessing["schema"]!="chooguard.exact-collision-envelope-partition.v1" ||
                folder!=RetainedShopFolder || (string)spec["root"]!=RetainedShopRoot || (string)spec["zone"]!="shops 1F" ||
                (string)preprocessing["owner"]!=RetainedShopRoot+"/Kit_Colliders")
                throw new InvalidDataException("Retained shop replay scope changed");
            string canonical=(string)preprocessing["canonical_spec"], specHash=(string)preprocessing["canonical_spec_sha256"];
            if(canonical!="content/world/observed-interiors/shops-1f.json" || !File.Exists(canonical) ||
                FileDigest(canonical)!=specHash || !File.Exists(specPath) || FileDigest(specPath)!=specHash ||
                !JToken.DeepEquals(spec,JObject.Parse(File.ReadAllText(specPath))))
                throw new InvalidDataException("Retained shop replay spec changed");
            var elements=spec["elements"] as JArray;
            if(elements==null || elements.Count!=16 || elements.Any(e => (string)e["type"]!="ShopInterior" ||
                (string)e["frame"]!="root" || (string)e["tier"]!="b" || (bool?)e["params"]?["buildEnvelope"]!=false ||
                (bool?)e["params"]?["collider"]!=true))
                throw new InvalidDataException("Retained shop replay ownership policy changed");
            var sources=SourceAssetsForRoot(ownerRoot);
            // Guide corrections can register further existing finish batches (Unit8 adds its paint wall).
            // KitVerifyCorrections still regenerates the complete current registered set exactly once.
            if(sources.Length<23 || RetainedShopSources.Except(sources).Any() ||
                sources.Any(path => !path.StartsWith(RetainedShopFolder+"/",StringComparison.Ordinal)))
                throw new InvalidDataException("Retained shop replay requires every registered source");
            return true;
        }

        // The three retained secondary caps remain available for future source verification.
        // Disabling all descendant renderers also disables separately-created ShadowCaster children.
        public static void WithholdRetainedShopEnvelopeRenderers(Transform built)
        {
            if(built==null) throw new InvalidDataException("Retained shop replay root missing");
            var parts=new System.Collections.Generic.List<string>();
            for(var t=built;t!=null;t=t.parent) parts.Add(t.name);
            parts.Reverse();
            if(string.Join("/",parts)!=RetainedShopRoot) throw new InvalidDataException("Retained shop withholding root changed");
            var rows=(JArray)JObject.Parse(File.ReadAllText(Registry))["meshes"];
            foreach(string name in RetainedShopCaps)
            {
                var owner=built.Find(name);var row=rows.Single(r => (string)r["source_asset"]==RetainedShopFolder+"/"+name+".asset");
                var filter=owner!=null?owner.GetComponent<MeshFilter>():null;
                var renderers=owner!=null?owner.GetComponentsInChildren<MeshRenderer>(true):Array.Empty<MeshRenderer>();
                if(filter==null || renderers.Length==0 || AssetDatabase.GetAssetPath(filter.sharedMesh)!=(string)row["replacement_asset"] ||
                    ArchitecturalCorrectionRegistry.CompleteChannelDigest(filter.sharedMesh)!=(string)row["replacement_digest"] ||
                    owner.GetComponentsInChildren<MeshCollider>(true).Length!=0)
                    throw new InvalidDataException("Retained secondary shop envelope assignment changed");
                foreach(var renderer in renderers) renderer.enabled=false;
            }
        }

        // Accept the fresh raw source only, then prove the exact ordinal partition against its saved baseline.
        // Return the persistent baseline after comparison, avoiding a transient mesh escaping this method.
        static Mesh PreprocessForRebuild(Mesh generated, JToken row, string sourceAsset)
        {
            var policy=row["source_preprocess"] as JObject;
            if(policy==null) return generated;
            if((string)policy["schema"]!="chooguard.exact-collision-envelope-partition.v1" || sourceAsset!=RawShopCollider ||
                (string)row["source_asset"]!=RawShopCollider || (string)row["baseline_asset"]!=FixtureShopCollider ||
                (string)policy["owner"]!=RetainedShopRoot+"/Kit_Colliders" || (string)policy["raw_source_asset"]!=RawShopCollider ||
                (string)policy["baseline_asset"]!=FixtureShopCollider || (string)row["source_digest"]!=(string)policy["baseline_complete_digest"])
                throw new InvalidDataException("Unreviewed guide source preprocessing");
            string ordinalFile=(string)policy["ordinal_source"];
            if(ordinalFile!=".planning/2026-10-08-interior-runtime-completion/architecture-kitshop-overlap.json")
                throw new InvalidDataException("Pinned historical partition identity changed");
            bool maintained=!File.Exists(ordinalFile);
            if(maintained)ordinalFile="content/fixtures/architecture/retained-shop-collision-partition.json";
            string ordinalHash=maintained?"ef291fe2df2524bb7539297d492332ed4e9bf1e044a1e697f5266f4d6c0b5f42":(string)policy["ordinal_source_sha256"];
            if(!File.Exists(ordinalFile) || FileDigest(ordinalFile)!=ordinalHash ||
                !File.Exists(RawShopCollider) || FileDigest(RawShopCollider)!=(string)policy["raw_source_file_sha256"] ||
                !File.Exists(FixtureShopCollider) || FileDigest(FixtureShopCollider)!=(string)policy["baseline_file_sha256"])
                throw new InvalidDataException("Pinned shop partition authority changed");
            var raw=AssetDatabase.LoadAssetAtPath<Mesh>(RawShopCollider);
            var baseline=AssetDatabase.LoadAssetAtPath<Mesh>(FixtureShopCollider);
            if(generated==null || raw==null || baseline==null || generated.blendShapeCount!=0 ||
                generated.boneWeights.Length!=0 || generated.bindposes.Length!=0 || raw.vertexCount!=768 || raw.subMeshCount!=1 ||
                raw.GetTopology(0)!=MeshTopology.Triangles || raw.triangles.Length!=384*3 ||
                ArchitecturalCorrectionRegistry.CompleteChannelDigest(raw)!=(string)policy["raw_source_complete_digest"] ||
                baseline.vertexCount!=768 || baseline.subMeshCount!=1 || baseline.GetTopology(0)!=MeshTopology.Triangles ||
                baseline.triangles.Length!=132*3 ||
                ArchitecturalCorrectionRegistry.CompleteChannelDigest(baseline)!=(string)policy["baseline_complete_digest"])
                throw new InvalidDataException("Pinned raw or baseline shop collision changed");
            var ordinals=JObject.Parse(File.ReadAllText(ordinalFile));
            if(maintained&&((string)ordinals["schema"]!="chooguard.maintained-retained-shop-collision-partition.v1"||
                (string)ordinals["kit_mesh"]["complete_channels_sha256"]!=(string)policy["raw_source_complete_digest"]||
                (string)ordinals["baseline_mesh"]["complete_channels_sha256"]!=(string)policy["baseline_complete_digest"]))
                throw new InvalidDataException("Maintained partition fixture identity changed");
            if((string)ordinals["kit_owner"]!=(string)policy["owner"] || (string)ordinals["kit_mesh"]["asset"]!=RawShopCollider)
                throw new InvalidDataException("Shop collision ordinal owner changed");
            var removed=ordinals["duplicate_kit_triangle_ordinals"].ToObject<int[]>();
            var kept=ordinals["preserve_remaining_triangle_ordinals"].ToObject<int[]>();
            var all=removed.Concat(kept).ToArray();
            if(removed.Length!=252 || kept.Length!=132 || all.Distinct().Count()!=384 ||
                all.Any(n => n<0 || n>=384) || Enumerable.Range(0,384).Except(all).Any() ||
                !kept.SequenceEqual(Enumerable.Range(0,384).Except(removed)))
                throw new InvalidDataException("Invalid exact shop collision partition");
            var rawIndices=raw.triangles;
            var expected=kept.SelectMany(n => rawIndices.Skip(n*3).Take(3)).ToArray();
            if(!expected.SequenceEqual(baseline.triangles)) throw new InvalidDataException("Pinned shop fixture index order changed");
            // Saved-source checks may supply the exact reviewed fixture baseline or the latest guide target.
            // These states are accepted only after all original authority and partition checks above.
            string generatedDigest=ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated);
            if(generatedDigest==(string)row["source_digest"] || generatedDigest==(string)row["replacement_digest"])
                return generated;
            if(generated.vertexCount!=768 || generated.subMeshCount!=1 || generated.GetTopology(0)!=MeshTopology.Triangles ||
                generated.triangles.Length!=384*3 || generated.blendShapeCount!=0 || generated.boneWeights.Length!=0 ||
                generated.bindposes.Length!=0 || generatedDigest!=(string)policy["raw_source_complete_digest"] ||
                !generated.triangles.SequenceEqual(rawIndices))
                throw new InvalidDataException("Fresh raw shop collision differs from its pinned source");
            var partition=InteriorEnvelopeRepair.KeepOtherTriangles(generated,removed);
            try
            {
                if(partition.vertexCount!=768 || partition.triangles.Length!=132*3 ||
                    ArchitecturalCorrectionRegistry.CompleteChannelDigest(partition)!=(string)policy["baseline_complete_digest"])
                    throw new InvalidDataException("Fresh shop fixture partition attributes changed");
            }
            finally { UnityEngine.Object.DestroyImmediate(partition); }
            return baseline;
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
        public static bool HasCorrection(string sourceAsset) => (File.Exists(Registry) &&
            ((JArray)JObject.Parse(File.ReadAllText(Registry))["meshes"]).Any(r => (string)r["source_asset"] == sourceAsset)) || NamcheonSourceFrontRegistry.HasCorrection(sourceAsset) || LockerPlacementWithholdingRegistry.HasCorrection(sourceAsset);
        public static string[] SourceAssetsForRoot(string ownerRoot)
        {
            if (!File.Exists(Registry)) return NamcheonSourceFrontRegistry.SourceAssetsForRoot(ownerRoot).Concat(LockerPlacementWithholdingRegistry.SourceAssetsForRoot(ownerRoot)).Distinct().ToArray();
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
            return sources.Concat(NamcheonSourceFrontRegistry.SourceAssetsForRoot(ownerRoot)).Concat(LockerPlacementWithholdingRegistry.SourceAssetsForRoot(ownerRoot)).Distinct().ToArray();
        }
        public static Mesh ResolveForRebuild(Mesh generated, string sourceAsset) =>
            LockerPlacementWithholdingRegistry.ResolveComposedForRebuild(
                NamcheonSourceFrontRegistry.ResolveComposedForRebuild(ResolveBaselineForRebuild(generated, sourceAsset), sourceAsset), sourceAsset);
        // Existing baseline chain stays available for observed facade preflight before composition.
        public static Mesh ResolveBaselineForRebuild(Mesh generated, string sourceAsset)
        {
            if (StairInfillOmissionRegistry.HasCorrection(sourceAsset)) return StairInfillOmissionRegistry.ResolveBySource(generated, sourceAsset);
            Mesh installedComposite;
            if (NamcheonSourceFrontRegistry.TryResolveInstalled(generated, sourceAsset, out installedComposite)) return installedComposite;
            if (LockerPlacementWithholdingRegistry.TryResolveInstalled(generated, sourceAsset, out installedComposite)) return installedComposite;
            return LockerPlacementWithholdingRegistry.VerifyCompositeBaseline(
                NamcheonSourceFrontRegistry.VerifyCompositeBaseline(ResolveGuideStageForRebuild(generated, sourceAsset), sourceAsset), sourceAsset);
        }
        // Deterministic guide stage, independent of retired downstream construction packets.
        // The normal builder entry points retain their installed-composite validation chain.
        public static Mesh ResolveGuideStageForRebuild(Mesh generated, string sourceAsset)
        {
            if (!File.Exists(Registry)) return generated;
            var rows = ((JArray)JObject.Parse(File.ReadAllText(Registry))["meshes"])
                .Where(r => (string)r["source_asset"] == sourceAsset).ToArray();
            if (rows.Length == 0) return generated;
            if (rows.Length != 1) throw new InvalidDataException("Ambiguous guide layout source");
            var row = rows[0];
            generated = PreprocessForRebuild(generated, row, sourceAsset);
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
        public static void WithholdDuplicateDoor(ChooGuard.App.Fps.Facilities.StationDoor door)
        {
            if(!File.Exists(Registry))return;
            var rows=JObject.Parse(File.ReadAllText(Registry))["withheld_duplicate_doors"] as JArray;
            if(rows==null)return;
            var matches=rows.Where(r=>(string)r["element"]==door.Element&&(int)r["index"]==door.Index).ToArray();
            if(matches.Length==0)return;if(matches.Length!=1)throw new InvalidDataException("Ambiguous duplicate door policy");
            var row=matches[0];string evidence=(string)row["evidence"];
            if(FileDigest(evidence)!=(string)row["evidence_sha256"])throw new InvalidDataException("Original doorway reuse proof changed");
            var native=UnityEngine.Object.FindObjectsByType<ChooGuard.App.Fps.Facilities.StationDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(d=>d.gameObject.scene==door.gameObject.scene&&d.Element==(string)row["native_element"]).ToArray();
            if(native.Length!=1||!native[0].enabled||!native[0].gameObject.activeInHierarchy||!native[0].PassableByPeople)
                throw new InvalidDataException("Original operating doorway missing");
            door.gameObject.SetActive(false);
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
