using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Twin;
using ChooGuard.Editor.Twin;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChooGuard.Tests.EditMode
{
    public sealed class NativeCounterleafFragmentsSceneTests
    {
        const string ScenePath = "Assets/ChooGuard/Scenes/FpsStation.unity";
        const string PacketPath = "workers/twin/output/native-counterleaf-recovery-20261009/absent-fragment-packet.json";
        const string ReceiptPath = ".planning/2026-10-08-interior-runtime-completion/native-counterleaf-fragments-installation.json";
        const string PacketSha256 = "df1e591f2e6918235ec2f0e8f51743f7d016b24252474944fe503fe25faf08e2";
        const string Category = "partial_counterleaf_fragment_source_part";
        const float FrameTolerance = .0001f, SourceTolerance = .0003f;
        static readonly string[] SourceIds =
        {
            "501b0b5be16448a0d5c95d78", "5d4f4242d54a39380767bf43", "ab427631ad34251695dc2ee6",
            "2db06c0adff1128fd9c0c5fb", "8f09a2b4ce0d2f2a58e31347", "d1b879fc4e65e56b6c367995"
        };

        // JsonUtility reads the narrow metadata; JsonValues below only reads nested numeric arrays.
        [Serializable] sealed class AssetProof
        {
            public string asset, guid, sha256, file_sha256, complete_channels_sha256;
        }
        [Serializable] sealed class Face
        {
            public int source_triangle_ordinal, native_flattened_triangle_ordinal, original_material_slot;
            public int native_to_original_winding_parity;
            public int[] native_vertex_indices, native_to_original_corner_mapping;
        }
        [Serializable] sealed class Fragment
        {
            public string source_id, source_path, source_definition, source_sha256, category;
            public string target_counterleaf_source_id, reference_renderer_path, reference_renderer_global_object_id;
            public string reference_mesh_asset, reference_mesh_guid, reference_mesh_name, reference_mesh_asset_file_sha256;
            public int restored_triangles, reference_mesh_vertex_count;
            public AssetProof[] materials;
            public Face[] selected_faces;
            public bool operation_bound, authored_pose_changed;
        }
        [Serializable] sealed class OwnerProof
        {
            public string source_id, owner;
            public AssetProof mesh;
            public AssetProof[] materials;
        }
        [Serializable] sealed class Installation
        {
            public bool production_installed, complete_counterleaf_recovery, operation_bound;
            public int new_partial_fragments, restored_native_faces, old_source_count, saved_source_count;
            public string packet_path, packet_sha256;
            public OwnerProof[] fragment_proofs, source_proofs;
        }

        [Test]
        public void SavedPartialCounterleavesKeepOnly24OriginalFacesAndRemainUnbound()
        {
            var saved = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath);
                Assert.That(scene.isDirty, Is.False, "Test must inspect the saved production scene");
                Assert.That(Hash(PacketPath), Is.EqualTo(PacketSha256), "Reviewed original-face packet changed");
                string packetJson = File.ReadAllText(PacketPath), receiptJson = File.ReadAllText(ReceiptPath);
                var receipt = JsonUtility.FromJson<Installation>(receiptJson);
                Assert.That(receipt.production_installed, Is.True);
                Assert.That(receipt.complete_counterleaf_recovery, Is.False);
                Assert.That(receipt.operation_bound, Is.False);
                Assert.That(receipt.packet_path, Is.EqualTo(PacketPath));
                Assert.That(receipt.packet_sha256, Is.EqualTo(PacketSha256));
                Assert.That(receipt.new_partial_fragments, Is.EqualTo(6));
                Assert.That(receipt.restored_native_faces, Is.EqualTo(24));
                Assert.That(receipt.old_source_count, Is.EqualTo(1029));
                Assert.That(receipt.saved_source_count, Is.EqualTo(1035));
                Assert.That(receipt.fragment_proofs, Has.Length.EqualTo(6));
                Assert.That(receipt.source_proofs, Has.Length.EqualTo(6));
                CollectionAssert.AreEquivalent(SourceIds, receipt.fragment_proofs.Select(p => p.source_id));

                var identities = UnityEngine.Object.FindObjectsByType<SourcePartIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(s => s.gameObject.scene == scene).ToArray();
                var nativeFixedStair = identities.Single(s => s.Category == "fixed_stair_architecture");
                Assert.That(nativeFixedStair.SourceId, Is.EqualTo("93f4f5ec5996ef5390adf44a"));
                Assert.That(nativeFixedStair.PreservedTriangles, Is.EqualTo(1306));
                var priorIdentities = identities.Where(s => s != nativeFixedStair).ToArray();
                Assert.That(priorIdentities, Has.Length.EqualTo(1035));
                Assert.That(priorIdentities.Select(s => s.SourceId).Distinct().Count(), Is.EqualTo(1035));
                var pdfRoot = GameObject.Find("실내 트윈 마감/1F PDF 원도 복원").transform;
                var savedDoors = UnityEngine.Object.FindObjectsByType<StationDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(d => d.gameObject.scene == scene).ToArray();
                Assert.That(savedDoors.Count(d => !d.transform.IsChildOf(pdfRoot)), Is.EqualTo(103), "Original door controller count changed");
                Assert.That(savedDoors.Count(d => d.transform.IsChildOf(pdfRoot)), Is.EqualTo(24));
                var fragments = identities.Where(s => s.Category == Category).ToArray();
                Assert.That(fragments, Has.Length.EqualTo(6));
                CollectionAssert.AreEquivalent(SourceIds, fragments.Select(s => s.SourceId));
                var filters = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(f => f.gameObject.scene == scene).ToArray();
                var packetRows = JsonValues.Array(JsonValues.Property(packetJson, "fragments"));
                var proofRows = JsonValues.Array(JsonValues.Property(receiptJson, "fragment_proofs"));
                var sourceProofRows = JsonValues.Array(JsonValues.Property(receiptJson, "source_proofs"));
                Assert.That(packetRows, Has.Length.EqualTo(6));
                CollectionAssert.AreEquivalent(SourceIds, packetRows.Select(r => JsonUtility.FromJson<Fragment>(r).source_id));
                Physics.SyncTransforms();

                int actualTriangles = 0, centroidProbes = 0;
                float maximumOriginalCornerError = 0;
                foreach (string rowJson in packetRows)
                {
                    var row = JsonUtility.FromJson<Fragment>(rowJson);
                    var identity = fragments.Single(s => s.SourceId == row.source_id);
                    var filter = identity.GetComponent<MeshFilter>();
                    var renderer = identity.GetComponent<MeshRenderer>();
                    var collider = identity.GetComponent<MeshCollider>();
                    Assert.That(filter, Is.Not.Null, row.source_id);
                    Assert.That(renderer, Is.Not.Null, row.source_id);
                    Assert.That(collider, Is.Not.Null, row.source_id);
                    Assert.That(identity.gameObject.activeInHierarchy, Is.True);
                    Assert.That(identity.SourcePath, Is.EqualTo(row.source_path));
                    Assert.That(identity.SourceDefinition, Is.EqualTo(row.source_definition));
                    Assert.That(identity.SourceSha256, Is.EqualTo(row.source_sha256));
                    Assert.That(identity.LegacyGeometrySeparated, Is.False);
                    Assert.That(identity.SiteAccuracyVerified, Is.False);
                    Assert.That(identity.PreservedTriangles, Is.EqualTo(row.restored_triangles));
                    Assert.That(identity.TransferMaximumError, Is.InRange(0f, SourceTolerance));
                    Assert.That(identity.ReconciliationReceipt, Is.EqualTo(PacketPath));
                    Assert.That(identity.OperationStatus, Is.EqualTo("partial_counterleaf_" + row.restored_triangles + "_of_1054_source_faces_restored_operation_unbound"));
                    Assert.That(identity.GetComponentInParent<StationDoor>(), Is.Null, "Partial source must remain operation-unbound");
                    Assert.That(identity.GetComponentsInChildren<MonoBehaviour>(true).All(b => b is SourcePartIdentity), Is.True,
                        "A partial fragment must not gain an operating controller");
                    Assert.That(row.operation_bound || row.authored_pose_changed, Is.False);
                    Assert.That(identity.transform.parent.name, Is.EqualTo("N_" + row.target_counterleaf_source_id));
                    SameFrame(identity.transform.parent.localToWorldMatrix, rowJson, "target_counterleaf_world_matrix", row.source_id + " parent");
                    SameFrame(identity.transform.localToWorldMatrix, rowJson, "planned_world_matrix", row.source_id);

                    var nativeFilter = filters.Single(f => PathOf(f.transform) == row.reference_renderer_path);
                    Assert.That(GlobalObjectId.GetGlobalObjectIdSlow(nativeFilter).ToString(), Is.EqualTo(row.reference_renderer_global_object_id));
                    SameFrame(nativeFilter.transform.localToWorldMatrix, rowJson, "reference_renderer_world_matrix", "native counterpart");
                    var native = nativeFilter.sharedMesh;
                    Assert.That(native, Is.Not.Null);
                    Assert.That(native.isReadable, Is.True);
                    Assert.That(AssetDatabase.GetAssetPath(native), Is.EqualTo(row.reference_mesh_asset));
                    Assert.That(AssetDatabase.AssetPathToGUID(row.reference_mesh_asset), Is.EqualTo(row.reference_mesh_guid));
                    Assert.That(Hash(row.reference_mesh_asset), Is.EqualTo(row.reference_mesh_asset_file_sha256));
                    Assert.That(native.name, Is.EqualTo(row.reference_mesh_name));
                    Assert.That(native.vertexCount, Is.EqualTo(row.reference_mesh_vertex_count));
                    var frozenVertices = JsonValues.Numbers(rowJson, "reference_native_vertices", row.reference_mesh_vertex_count, 3);
                    CollectionAssert.AreEqual(frozenVertices.Select(v => new Vector3(v[0], v[1], v[2])).ToArray(), native.vertices);
                    CollectionAssert.AreEqual(JsonValues.Numbers(rowJson, "reference_mesh_triangles", native.triangles.Length / 3, 3)
                        .SelectMany(t => t.Select(Integer)).ToArray(), native.triangles);
                    var partial = filter.sharedMesh;
                    Assert.That(partial, Is.Not.Null);
                    Assert.That(partial.isReadable, Is.True);
                    Assert.That(EditorUtility.IsDirty(partial) || EditorUtility.IsDirty(native), Is.False, "Unsaved mesh cannot prove saved channels");
                    SameNativeChannels(partial, native);
                    Assert.That(collider.sharedMesh, Is.SameAs(partial));
                    var nativeCollider = nativeFilter.GetComponent<MeshCollider>();
                    Assert.That(nativeCollider, Is.Not.Null);
                    Assert.That(nativeCollider.sharedMesh, Is.SameAs(native));
                    SameCollider(collider, nativeCollider);
                    var nativeRenderer = nativeFilter.GetComponent<MeshRenderer>();
                    Assert.That(nativeRenderer, Is.Not.Null);
                    SameRenderer(renderer, nativeRenderer);
                    CollectionAssert.AreEqual(nativeRenderer.sharedMaterials, renderer.sharedMaterials);
                    Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(row.materials.Length));
                    for (int material = 0; material < row.materials.Length; material++)
                        SameAsset(renderer.sharedMaterials[material], row.materials[material]);

                    string proofJson = proofRows.Single(p => JsonUtility.FromJson<OwnerProof>(p).source_id == row.source_id);
                    SameOwnerProof(filter, proofJson);
                    SameOwnerProof(nativeFilter, sourceProofRows.Single(p => JsonUtility.FromJson<OwnerProof>(p).owner == row.reference_renderer_path));
                    Assert.That(AssetDatabase.GetAssetPath(partial), Does.StartWith("Assets/ChooGuard/Art/Fps/NativeCounterleafFragments20261009/"));

                    var witnesses = JsonValues.Array(JsonValues.Property(rowJson, "selected_faces"))
                        .Select(raw => new { Raw = raw, Face = JsonUtility.FromJson<Face>(raw) })
                        .OrderBy(w => w.Face.native_flattened_triangle_ordinal).ToArray();
                    Assert.That(witnesses, Has.Length.EqualTo(row.restored_triangles));
                    Assert.That(witnesses.Select(w => w.Face.native_flattened_triangle_ordinal).Distinct().Count(), Is.EqualTo(row.restored_triangles));
                    for (int submesh = 0; submesh < partial.subMeshCount; submesh++)
                    {
                        var expected = witnesses.Where(w => w.Face.original_material_slot == submesh)
                            .SelectMany(w => w.Face.native_vertex_indices).ToArray();
                        CollectionAssert.AreEqual(expected, partial.GetTriangles(submesh), row.source_id + " submesh " + submesh);
                    }
                    var indices = partial.triangles;
                    Assert.That(indices, Has.Length.EqualTo(row.restored_triangles * 3));
                    CollectionAssert.AreEqual(witnesses.SelectMany(w => w.Face.native_vertex_indices).ToArray(), indices,
                        "Actual selected native corner indices changed");
                    actualTriangles += indices.Length / 3;
                    var vertices = partial.vertices;
                    for (int face = 0; face < witnesses.Length; face++)
                    {
                        var witness = witnesses[face];
                        var world = Enumerable.Range(0, 3).Select(c => filter.transform.TransformPoint(vertices[indices[face * 3 + c]])).ToArray();
                        var original = JsonValues.Numbers(witness.Raw, "expected_original_world_triangle", 3, 3)
                            .Select(v => new Vector3(v[0], v[1], v[2])).ToArray();
                        CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, witness.Face.native_to_original_corner_mapping);
                        float error = Enumerable.Range(0, 3).Max(c => Vector3.Distance(world[c], original[witness.Face.native_to_original_corner_mapping[c]]));
                        Assert.That(error, Is.LessThanOrEqualTo(SourceTolerance), row.source_id + " original authored face " + witness.Face.source_triangle_ordinal);
                        maximumOriginalCornerError = Mathf.Max(maximumOriginalCornerError, error);
                        var normal = Vector3.Cross(world[1] - world[0], world[2] - world[0]);
                        Assert.That(normal.sqrMagnitude, Is.GreaterThan(1e-20f));
                        int parity = Vector3.Dot(normal, Vector3.Cross(original[1] - original[0], original[2] - original[0])) > 0 ? 1 : -1;
                        Assert.That(parity, Is.EqualTo(witness.Face.native_to_original_winding_parity));
                        var centroid = (world[0] + world[1] + world[2]) / 3f;
                        normal.Normalize();
                        // Always cast both rays: reflected native winding may expose either side to MeshCollider.Raycast.
                        bool firstHit = collider.Raycast(new Ray(centroid + normal * .001f, -normal), out var first, .002f);
                        bool secondHit = collider.Raycast(new Ray(centroid - normal * .001f, normal), out var second, .002f);
                        Assert.That((firstHit && Vector3.Distance(first.point, centroid) <= SourceTolerance) ||
                            (secondHit && Vector3.Distance(second.point, centroid) <= SourceTolerance), Is.True,
                            row.source_id + " missing selected-face collision at original ordinal " + witness.Face.source_triangle_ordinal);
                        centroidProbes++;
                    }
                }
                Assert.That(actualTriangles, Is.EqualTo(24));
                Assert.That(centroidProbes, Is.EqualTo(24));
                Assert.That(maximumOriginalCornerError, Is.LessThanOrEqualTo(SourceTolerance));
                Assert.That(scene.isDirty, Is.False, "Regression must leave the inspected production scene untouched");
            }
            finally
            {
                // Discard test state; never save it. Unsaved/empty original setups cannot safely be restored.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (saved.Any(s => s.isLoaded) && saved.Count(s => s.isActive) == 1 &&
                    saved.Where(s => s.isLoaded).All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(saved);
            }
        }

        static void SameNativeChannels(Mesh partial, Mesh native)
        {
            Assert.That(partial.vertexCount, Is.EqualTo(native.vertexCount));
            Assert.That(partial.subMeshCount, Is.EqualTo(native.subMeshCount));
            Assert.That(partial.indexFormat, Is.EqualTo(native.indexFormat));
            Assert.That(partial.bounds, Is.EqualTo(native.bounds));
            CollectionAssert.AreEqual(native.GetVertexAttributes(), partial.GetVertexAttributes());
            CollectionAssert.AreEqual(native.vertices, partial.vertices);
            CollectionAssert.AreEqual(native.normals, partial.normals);
            CollectionAssert.AreEqual(native.tangents, partial.tangents);
            CollectionAssert.AreEqual(native.colors, partial.colors);
            CollectionAssert.AreEqual(native.colors32, partial.colors32);
            for (int channel = 0; channel < 8; channel++)
            {
                var source = new List<Vector4>(); var recovered = new List<Vector4>();
                native.GetUVs(channel, source); partial.GetUVs(channel, recovered);
                CollectionAssert.AreEqual(source, recovered, "Original native UV channel " + channel);
            }
            for (int submesh = 0; submesh < native.subMeshCount; submesh++)
                Assert.That(partial.GetTopology(submesh), Is.EqualTo(native.GetTopology(submesh)));
        }

        static void SameCollider(MeshCollider actual, MeshCollider expected)
        {
            Assert.That(actual.enabled, Is.EqualTo(expected.enabled));
            Assert.That(actual.convex, Is.EqualTo(expected.convex));
            Assert.That(actual.isTrigger, Is.EqualTo(expected.isTrigger));
            Assert.That(actual.cookingOptions, Is.EqualTo(expected.cookingOptions));
            Assert.That(actual.sharedMaterial, Is.SameAs(expected.sharedMaterial));
            Assert.That(actual.contactOffset, Is.EqualTo(expected.contactOffset));
            Assert.That(actual.includeLayers, Is.EqualTo(expected.includeLayers));
            Assert.That(actual.excludeLayers, Is.EqualTo(expected.excludeLayers));
            Assert.That(actual.layerOverridePriority, Is.EqualTo(expected.layerOverridePriority));
            Assert.That(actual.providesContacts, Is.EqualTo(expected.providesContacts));
            Assert.That(actual.gameObject.layer, Is.EqualTo(expected.gameObject.layer));
        }

        static void SameRenderer(MeshRenderer actual, MeshRenderer expected)
        {
            Assert.That(actual.enabled, Is.EqualTo(expected.enabled));
            Assert.That(actual.shadowCastingMode, Is.EqualTo(expected.shadowCastingMode));
            Assert.That(actual.receiveShadows, Is.EqualTo(expected.receiveShadows));
            Assert.That(actual.motionVectorGenerationMode, Is.EqualTo(expected.motionVectorGenerationMode));
            Assert.That(actual.lightProbeUsage, Is.EqualTo(expected.lightProbeUsage));
            Assert.That(actual.reflectionProbeUsage, Is.EqualTo(expected.reflectionProbeUsage));
            Assert.That(actual.probeAnchor, Is.SameAs(expected.probeAnchor));
            Assert.That(actual.lightProbeProxyVolumeOverride, Is.SameAs(expected.lightProbeProxyVolumeOverride));
            Assert.That(actual.renderingLayerMask, Is.EqualTo(expected.renderingLayerMask));
            Assert.That(actual.allowOcclusionWhenDynamic, Is.EqualTo(expected.allowOcclusionWhenDynamic));
            Assert.That(actual.forceRenderingOff, Is.EqualTo(expected.forceRenderingOff));
            Assert.That(actual.sortingLayerID, Is.EqualTo(expected.sortingLayerID));
            Assert.That(actual.sortingOrder, Is.EqualTo(expected.sortingOrder));
            Assert.That(actual.rendererPriority, Is.EqualTo(expected.rendererPriority));
            Assert.That(actual.rayTracingMode, Is.EqualTo(expected.rayTracingMode));
            Assert.That(actual.staticShadowCaster, Is.EqualTo(expected.staticShadowCaster));
            Assert.That(actual.receiveGI, Is.EqualTo(expected.receiveGI));
            Assert.That(actual.scaleInLightmap, Is.EqualTo(expected.scaleInLightmap));
            Assert.That(actual.stitchLightmapSeams, Is.EqualTo(expected.stitchLightmapSeams));
            Assert.That(actual.lightmapIndex, Is.EqualTo(expected.lightmapIndex));
            Assert.That(actual.lightmapScaleOffset, Is.EqualTo(expected.lightmapScaleOffset));
            Assert.That(actual.realtimeLightmapIndex, Is.EqualTo(expected.realtimeLightmapIndex));
            Assert.That(actual.realtimeLightmapScaleOffset, Is.EqualTo(expected.realtimeLightmapScaleOffset));
            Assert.That(GameObjectUtility.GetStaticEditorFlags(actual.gameObject), Is.EqualTo(GameObjectUtility.GetStaticEditorFlags(expected.gameObject)));
            Assert.That(actual.gameObject.layer, Is.EqualTo(expected.gameObject.layer));
            Assert.That(actual.HasPropertyBlock() || expected.HasPropertyBlock(), Is.False, "Transient appearance cannot prove saved native rendering");
        }

        static void SameOwnerProof(MeshFilter filter, string raw)
        {
            var proof = JsonUtility.FromJson<OwnerProof>(raw);
            Assert.That(PathOf(filter.transform), Is.EqualTo(proof.owner));
            SameAsset(filter.sharedMesh, proof.mesh);
            Assert.That(ArchitecturalCorrectionRegistry.CompleteChannelDigest(filter.sharedMesh), Is.EqualTo(proof.mesh.complete_channels_sha256));
            SameFrame(filter.transform.localToWorldMatrix, raw, "world_matrix", proof.owner);
            var materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
            Assert.That(materials, Has.Length.EqualTo(proof.materials.Length));
            for (int index = 0; index < materials.Length; index++) SameAsset(materials[index], proof.materials[index]);
        }

        static void SameAsset(UnityEngine.Object asset, AssetProof proof)
        {
            Assert.That(asset, Is.Not.Null);
            Assert.That(EditorUtility.IsDirty(asset), Is.False, "Unsaved dependency cannot prove saved appearance");
            Assert.That(AssetDatabase.GetAssetPath(asset), Is.EqualTo(proof.asset));
            Assert.That(AssetDatabase.AssetPathToGUID(proof.asset), Is.EqualTo(proof.guid));
            Assert.That(Hash(proof.asset), Is.EqualTo(string.IsNullOrEmpty(proof.file_sha256) ? proof.sha256 : proof.file_sha256));
        }

        static void SameFrame(Matrix4x4 actual, string json, string property, string label)
        {
            var expected = JsonValues.Numbers(json, property, 4, 4);
            for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++)
            {
                Assert.That(float.IsFinite(actual[row, column]), Is.True, label);
                Assert.That(Mathf.Abs(actual[row, column] - expected[row][column]), Is.LessThanOrEqualTo(FrameTolerance), label);
            }
        }

        static int Integer(float value)
        {
            int integer = checked((int)value);
            if (value != integer) throw new InvalidDataException("Non-integral native index in reviewed packet");
            return integer;
        }

        static string PathOf(Transform transform)
        {
            var names = new List<string>();
            while (transform != null) { names.Add(transform.name); transform = transform.parent; }
            names.Reverse(); return string.Join("/", names);
        }

        static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var input = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }

        // Read-only value cursor, keeping nested-array parsing out of the test assembly dependencies.
        static class JsonValues
        {
            public static string Property(string json, string name)
            {
                int at = 0; Space(json, ref at); Expect(json, ref at, '{'); Space(json, ref at);
                while (at < json.Length && json[at] != '}')
                {
                    int keyStart = at; StringEnd(json, ref at);
                    string key = json.Substring(keyStart + 1, at - keyStart - 2);
                    Space(json, ref at); Expect(json, ref at, ':'); Space(json, ref at);
                    int valueStart = at; ValueEnd(json, ref at);
                    if (key == name) return json.Substring(valueStart, at - valueStart);
                    Space(json, ref at);
                    if (at < json.Length && json[at] == '}') break;
                    Expect(json, ref at, ','); Space(json, ref at);
                }
                throw new InvalidDataException("Required JSON property missing: " + name);
            }

            public static string[] Array(string json)
            {
                int at = 0; Space(json, ref at); Expect(json, ref at, '['); Space(json, ref at);
                var values = new List<string>();
                while (at < json.Length && json[at] != ']')
                {
                    int start = at; ValueEnd(json, ref at); values.Add(json.Substring(start, at - start));
                    Space(json, ref at);
                    if (at < json.Length && json[at] == ']') break;
                    Expect(json, ref at, ','); Space(json, ref at);
                }
                Expect(json, ref at, ']'); Space(json, ref at);
                if (at != json.Length) throw new InvalidDataException("Trailing JSON array content");
                return values.ToArray();
            }

            public static float[][] Numbers(string json, string property, int rows, int columns)
            {
                var lines = Array(Property(json, property));
                if (lines.Length != rows) throw new InvalidDataException("Wrong numeric row count: " + property);
                return lines.Select(line =>
                {
                    var values = Array(line);
                    if (values.Length != columns) throw new InvalidDataException("Wrong numeric column count: " + property);
                    return values.Select(value =>
                    {
                        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || !float.IsFinite(number))
                            throw new InvalidDataException("Non-finite JSON number: " + property);
                        return number;
                    }).ToArray();
                }).ToArray();
            }

            static void ValueEnd(string json, ref int at)
            {
                if (at >= json.Length) throw new InvalidDataException("Missing JSON value");
                if (json[at] == '"') { StringEnd(json, ref at); return; }
                if (json[at] == '{' || json[at] == '[')
                {
                    char close = json[at++] == '{' ? '}' : ']'; Space(json, ref at);
                    while (at < json.Length && json[at] != close)
                    {
                        if (char.IsWhiteSpace(json[at]) || json[at] == ',' || json[at] == ':') { at++; continue; }
                        ValueEnd(json, ref at);
                    }
                    Expect(json, ref at, close); return;
                }
                int start = at;
                while (at < json.Length && !char.IsWhiteSpace(json[at]) && json[at] != ',' && json[at] != ']' && json[at] != '}' && json[at] != ':') at++;
                if (at == start) throw new InvalidDataException("Invalid JSON value");
            }

            static void StringEnd(string json, ref int at)
            {
                Expect(json, ref at, '"');
                while (at < json.Length)
                {
                    char character = json[at++];
                    if (character == '\\') { if (at >= json.Length) break; at++; }
                    else if (character == '"') return;
                }
                throw new InvalidDataException("Unterminated JSON string");
            }

            static void Space(string json, ref int at) { while (at < json.Length && char.IsWhiteSpace(json[at])) at++; }
            static void Expect(string json, ref int at, char token)
            {
                if (at >= json.Length || json[at++] != token) throw new InvalidDataException("Invalid JSON delimiter: " + token);
            }
        }
    }
}
