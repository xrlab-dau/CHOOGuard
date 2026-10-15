using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Twin;
using ChooGuard.Circulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Connects four existing kit flights by their original builder index ranges and collider corners.</summary>
    public static class ExistingGroundEscalatorSetup
    {
        private const string Output = "Assets/ChooGuard/Art/StationInterior/GroundCirculation";
        private sealed class Bank
        {
            public string Path, Id;
            public int FlatSteps, StepVertices;
            public float Runout;
            public string[] Flights;
        }
        private static readonly Bank[] banks = {
            new Bank { Path = "실내 트윈 마감/수직 동선", Id = "core", FlatSteps = 2, Runout = .9f, StepVertices = 272,
                Flights = new[] { "esc-1f-north-1", "esc-1f-north-0" } },
            new Bank { Path = "실내 트윈 마감/1F 공용 에스컬레이터", Id = "public", FlatSteps = 3, Runout = .65f, StepVertices = 288,
                Flights = new[] { "esc-1f-south-0", "esc-1f-south-1" } }
        };
        public sealed class FlightGeometry
        {
            public Vector3 Side, Forward;
            public float Width, Pitch;
            public Vector3[] Ramp = new Vector3[4];
        }
        [Serializable] private sealed class IndexReceipt
        {
            public string lane_id, surface, source_mesh_path, source_mesh_guid, source_mesh_sha256, generated_mesh_path;
            public string source_object_id, source_scene_path;
            public int builder_flight, first_source_vertex, source_vertex_count, first_source_index, source_index_count;
            public float[] source_local_to_world;
        }
        [Serializable] private sealed class ReceiptFile
        {
            public string status = "EXACT_EXISTING_BUILDER_INDEX_PARTITIONS";
            public bool physical_accuracy_verified = false;
            public bool closed_return_paths_created = false;
            public IndexReceipt[] surfaces;
        }
        private sealed class Prepared
        {
            public Bank Spec;
            public Transform Root;
            public MeshFilter Steps, Rubber;
            public Mesh[] StepFlights, RubberFlights;
            public FlightGeometry[] Geometry;
            public StationPoints.EscalatorEntry[] Lanes;
        }

        public static string Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Ground setup requires edit mode");
            var points = StationPoints.Load(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ChooGuard/Art/Emergency/station-points.json"));
            var prepared = new List<Prepared>();
            try
            {
                foreach (var spec in banks) prepared.Add(Prepare(spec, points));
                EnsureFolder(Output);
                foreach (var bank in prepared) Install(bank);
                AssetDatabase.SaveAssets();
                WriteReceipts(prepared);
                foreach (var scene in prepared.Select(b => b.Root.gameObject.scene).Distinct()) EditorSceneManager.SaveScene(scene);
                return "CG_GROUND_ESCALATOR_BINDING banks=2 lanes=4 preserved_step_quads=140 original_rubber_surfaces=4 ride_segments=12 invented_return_loops=0 field_accuracy_verified=false";
            }
            finally
            {
                foreach (var bank in prepared)
                    foreach (var mesh in bank.StepFlights.Concat(bank.RubberFlights))
                        if (mesh != null && !EditorUtility.IsPersistent(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private static Prepared Prepare(Bank spec, StationPoints points)
        {
            var root = GameObject.Find(spec.Path)?.transform;
            if (root == null) throw new InvalidOperationException("Existing bank missing: " + spec.Path);
            var steps = root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.StartsWith("Kit_EscStep_", StringComparison.Ordinal)).ToArray();
            var rubber = root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name == "Kit_Rubber").ToArray();
            if (steps.Length != 1 || rubber.Length != 1 || steps[0].transform.parent != root || rubber[0].transform.parent != root ||
                steps[0].sharedMesh == null || steps[0].sharedMesh.vertexCount != spec.StepVertices)
                throw new InvalidOperationException("Existing builder batches do not match: " + spec.Path);
            var data = new Prepared { Spec = spec, Root = root, Steps = steps[0], Rubber = rubber[0] };
            data.StepFlights = SplitBuilderFlights(data.Steps.sharedMesh);
            try
            {
                data.RubberFlights = SplitBuilderFlights(data.Rubber.sharedMesh);
                data.Geometry = new FlightGeometry[2]; data.Lanes = new StationPoints.EscalatorEntry[2];
                var colliders = root.GetComponentsInChildren<MeshCollider>(true).Where(c => c.name == "Kit_Colliders" && c.sharedMesh != null && c.sharedMesh.isReadable).ToArray();
                if (colliders.Length != 1) throw new InvalidOperationException("Original bank collider missing/ambiguous: " + spec.Path);
                var colliderVertices = colliders[0].sharedMesh.vertices.Select(colliders[0].transform.TransformPoint).ToArray();
                for (int flight = 0; flight < 2; flight++)
                {
                    data.Lanes[flight] = points.Escalators.Single(e => e.id == spec.Flights[flight]);
                    data.Geometry[flight] = DescribeFlight(data.StepFlights[flight], data.Steps.transform, spec.FlatSteps, spec.Runout);
                    ValidateLane(data.StepFlights[flight], data.Steps.transform, data.Lanes[flight]);
                    foreach (var centre in data.Geometry[flight].Ramp)
                        foreach (float side in new[] { -.5f, .5f })
                        {
                            var corner = centre + data.Geometry[flight].Side * (data.Geometry[flight].Width * side);
                            if (!colliderVertices.Any(v => (v - corner).sqrMagnitude <= .000004f))
                                throw new InvalidOperationException("Derived ramp corner is absent from original collider: " + spec.Flights[flight] + " " + corner);
                        }
                }
                return data;
            }
            catch
            {
                foreach (var mesh in data.StepFlights.Concat(data.RubberFlights ?? Array.Empty<Mesh>())) UnityEngine.Object.DestroyImmediate(mesh);
                throw;
            }
        }

        /// <summary>Copy the exact disjoint flight ranges emitted by KEscalatorUnit; source positions/attributes are unchanged.</summary>
        public static Mesh[] SplitBuilderFlights(Mesh source)
        {
            if (source == null || !source.isReadable || source.subMeshCount != 1 || source.vertexCount < 8 || source.vertexCount % 2 != 0)
                throw new InvalidOperationException("Source is not a two-flight readable builder batch");
            var indices = source.GetTriangles(0);
            if (indices.Length == 0 || indices.Length % 6 != 0) throw new InvalidOperationException("Unequal builder index ranges");
            int vertexHalf = source.vertexCount / 2, indexHalf = indices.Length / 2;
            for (int i = 0; i < indices.Length; i++)
                if (indices[i] < (i < indexHalf ? 0 : vertexHalf) || indices[i] >= (i < indexHalf ? vertexHalf : source.vertexCount))
                    throw new InvalidOperationException("Source indices cross the original builder flight boundary");
            var positions = source.vertices; var normals = source.normals; var tangents = source.tangents; var colours = source.colors;
            var uv = new List<Vector4>[8];
            for (int channel = 0; channel < uv.Length; channel++) { uv[channel] = new List<Vector4>(); source.GetUVs(channel, uv[channel]); }
            var result = new Mesh[2];
            for (int flight = 0; flight < 2; flight++)
            {
                int first = flight * vertexHalf;
                var mesh = new Mesh { name = source.name + "_original_flight_" + flight, indexFormat = IndexFormat.UInt32 };
                mesh.vertices = positions.Skip(first).Take(vertexHalf).ToArray();
                if (normals.Length == source.vertexCount) mesh.normals = normals.Skip(first).Take(vertexHalf).ToArray();
                if (tangents.Length == source.vertexCount) mesh.tangents = tangents.Skip(first).Take(vertexHalf).ToArray();
                if (colours.Length == source.vertexCount) mesh.colors = colours.Skip(first).Take(vertexHalf).ToArray();
                for (int channel = 0; channel < uv.Length; channel++)
                    if (uv[channel].Count == source.vertexCount) mesh.SetUVs(channel, uv[channel].Skip(first).Take(vertexHalf).ToList());
                mesh.SetTriangles(indices.Skip(flight * indexHalf).Take(indexHalf).Select(i => i - first).ToArray(), 0);
                mesh.RecalculateBounds(); result[flight] = mesh;
            }
            return result;
        }

        public static FlightGeometry DescribeFlight(Mesh flight, Transform frame, int flatSteps, float runout)
        {
            var vertices = flight.vertices;
            if (vertices.Length % 4 != 0 || flatSteps < 1 || vertices.Length / 4 <= 2 * flatSteps + 1)
                throw new InvalidOperationException("Unexpected original tread quad count");
            int quads = vertices.Length / 4, incline = quads - 2 * flatSteps;
            Vector3 a = frame.TransformPoint(vertices[0]), b = frame.TransformPoint(vertices[1]);
            Vector3 forward = frame.TransformPoint(vertices[3]) - a; forward.y = 0; forward.Normalize();
            var side = (b - a).normalized;
            var pitchVector = frame.TransformPoint(vertices[4]) - a; pitchVector.y = 0;
            float pitch = Vector3.Dot(pitchVector, forward), width = Vector3.Distance(a, b);
            float rise = frame.TransformPoint(vertices[(quads - 1) * 4]).y - a.y;
            if (pitch <= .2f || pitch >= .7f || Mathf.Abs(width - 1) > .002f || Mathf.Abs(rise - 7) > .002f || Mathf.Abs(Vector3.Dot(forward, side)) > .002f)
                throw new InvalidOperationException("Tread dimensions no longer match the original existing bank");
            var origin = (a + b) * .5f - forward * runout - Vector3.up * .005f;
            float x0 = runout + flatSteps * pitch, x1 = x0 + incline * pitch, total = x1 + x0;
            return new FlightGeometry { Forward = forward, Side = side, Width = width,
                Pitch = Vector3.Distance(frame.TransformPoint(vertices[flatSteps * 4]), frame.TransformPoint(vertices[(flatSteps + 1) * 4])),
                Ramp = new[] { origin, origin + forward * (x0 - pitch * .5f),
                    origin + forward * (x1 - pitch * .5f) + Vector3.up * rise, origin + forward * total + Vector3.up * rise } };
        }

        private static void ValidateLane(Mesh flight, Transform frame, StationPoints.EscalatorEntry lane)
        {
            if (lane.path == null || lane.path.Length < 6 || lane.stairs || lane.up != lane.id.EndsWith("-0", StringComparison.Ordinal))
                throw new InvalidOperationException("Explicit authored lane does not match its builder flight: " + lane.id);
            var vertices = flight.vertices;
            for (int quad = 0; quad < vertices.Length; quad += 4)
            {
                var centre = frame.TransformPoint((vertices[quad] + vertices[quad + 1] + vertices[quad + 2] + vertices[quad + 3]) * .25f);
                float best = float.PositiveInfinity;
                for (int index = 0; index + 1 < lane.path.Length; index++)
                {
                    var a = lane.path[index]; var b = lane.path[index + 1]; a.y = b.y = centre.y;
                    var ab = b - a;
                    if (ab.sqrMagnitude <= .000001f) continue;
                    best = Mathf.Min(best, Vector3.Distance(centre, a + ab * Mathf.Clamp01(Vector3.Dot(centre - a, ab) / ab.sqrMagnitude)));
                }
                // Existing StationSurvey samples along TrackAxis; these two source headings differ slightly from that axis.
                if (best > .3f) throw new InvalidOperationException("Original tread is outside its explicit existing navigation lane: " + lane.id);
            }
        }

        private static void Install(Prepared bank)
        {
            for (int flight = 0; flight < 2; flight++)
            {
                var lane = bank.Lanes[flight]; var geometry = bank.Geometry[flight];
                var root = FindOrCreate(bank.Root, "운행 연결 · " + lane.id);
                var binding = GetOrAdd<EscalatorDriveBinding>(root.gameObject);
                var motion = GetOrAdd<EscalatorMotion>(root.gameObject);
                Undo.RecordObjects(new UnityEngine.Object[] { binding, motion }, "Connect existing ground flight");
                binding.lane_id = lane.id;
                var step = Surface(root, "기존 디딤판", bank.Steps, bank.StepFlights[flight], lane, geometry.Pitch);
                var rubber = Surface(root, "기존 손잡이", bank.Rubber, bank.RubberFlights[flight], lane, 1);
                motion.Loops = Array.Empty<EscalatorMotion.Loop>(); motion.Surfaces = Array.Empty<EscalatorMotion.Surface>();
                motion.StepSurfaces = new[] { new EscalatorMotion.StepSurface { Renderer = step, MaterialIndex = 0 },
                    new EscalatorMotion.StepSurface { Renderer = rubber, MaterialIndex = 0 } };
                for (int segment = 0; segment < 3; segment++)
                {
                    var volume = FindOrCreate(root, "기존 램프 이송 · " + segment);
                    var a = geometry.Ramp[segment]; var b = geometry.Ramp[segment + 1];
                    var direction = (b - a).normalized; var rotation = Quaternion.LookRotation(direction, Vector3.up);
                    volume.SetPositionAndRotation((a + b) * .5f + rotation * Vector3.up, rotation);
                    var box = GetOrAdd<BoxCollider>(volume.gameObject);
                    Undo.RecordObject(box, "Original ramp ride bounds"); box.isTrigger = true; box.center = Vector3.zero;
                    box.size = new Vector3(geometry.Width, 2, Vector3.Distance(a, b));
                    var ride = GetOrAdd<EscalatorRide>(volume.gameObject);
                    Undo.RecordObject(ride, "Original flight direction");
                    ride.Direction = lane.up ? EscalatorRide.RideDirection.Up : EscalatorRide.RideDirection.Down;
                    ride.HorizontalOnly = segment != 1;
                    EditorUtility.SetDirty(box); EditorUtility.SetDirty(ride);
                }
                motion.RebuildBindings(); EditorUtility.SetDirty(binding); EditorUtility.SetDirty(motion);
            }
            foreach (var source in new[] { bank.Steps, bank.Rubber })
            {
                var renderer = source.GetComponent<Renderer>();
                Undo.RecordObject(renderer, "Replace batched flight surfaces with exact source index partitions");
                renderer.enabled = false; EditorUtility.SetDirty(renderer);
            }
            EditorSceneManager.MarkSceneDirty(bank.Root.gameObject.scene);
        }

        private static Renderer Surface(Transform root, string name, MeshFilter source, Mesh split, StationPoints.EscalatorEntry lane, float period)
        {
            var at = FindOrCreate(root, name);
            at.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation); at.localScale = source.transform.localScale;
            EnsureSurfaceComponents(at.gameObject, out var filter, out var renderer);
            var oldRenderer = source.GetComponent<Renderer>();
            if (oldRenderer == null || oldRenderer.sharedMaterials.Length != 1 || oldRenderer.sharedMaterial == null)
                throw new InvalidOperationException("Original surface material missing: " + source.name);
            var original = oldRenderer.sharedMaterial;
            var material = new Material(original) { name = lane.id + "_" + source.name + "_drive" };
            material.shader = Shader.Find("CHOOGuard/Escalator/Step");
            if (material.shader == null) throw new InvalidOperationException("Existing drive shader unavailable");
            material.SetFloat("_DriveBound", 1); material.SetFloat("_TravelMetres", 0); material.SetFloat("_TexMetres", period);
            material.SetFloat("_Direction", lane.up ? -1 : 1); // Builder UV +V follows the ascending direction; patterns translate opposite UV offset.
            var scale = original.GetTextureScale("_BaseMap");
            float scaleZ = at.TransformVector(Vector3.forward).magnitude;
            material.SetTextureScale("_BaseMap", new Vector2(scale.x, scale.y * period / Mathf.Max(scaleZ, .0001f)));
            Undo.RecordObjects(new UnityEngine.Object[] { filter, renderer }, "Preserve exact existing flight geometry");
            filter.sharedMesh = KeepAsset(split, Output + "/" + lane.id + "_" + source.name + ".asset");
            renderer.sharedMaterial = KeepAsset(material, Output + "/" + lane.id + "_" + source.name + ".mat");
            renderer.shadowCastingMode = oldRenderer.shadowCastingMode; renderer.receiveShadows = oldRenderer.receiveShadows;
            renderer.lightProbeUsage = oldRenderer.lightProbeUsage; renderer.reflectionProbeUsage = oldRenderer.reflectionProbeUsage;
            EditorUtility.SetDirty(filter); EditorUtility.SetDirty(renderer); return renderer;
        }

        private static void WriteReceipts(List<Prepared> prepared)
        {
            var receipts = new List<IndexReceipt>();
            foreach (var bank in prepared)
                for (int flight = 0; flight < 2; flight++)
                    foreach (var source in new[] { bank.Steps, bank.Rubber })
                    {
                        string path = AssetDatabase.GetAssetPath(source.sharedMesh);
                        string hash;
                        using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path)))).Replace("-", "").ToLowerInvariant();
                        var matrix = source.transform.localToWorldMatrix; var values = new float[16];
                        for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++) values[row * 4 + column] = matrix[row, column];
                        receipts.Add(new IndexReceipt { lane_id = bank.Lanes[flight].id, surface = source == bank.Steps ? "tread" : "handrail",
                            source_mesh_path = path, source_mesh_guid = AssetDatabase.AssetPathToGUID(path), source_mesh_sha256 = hash,
                            generated_mesh_path = Output + "/" + bank.Lanes[flight].id + "_" + source.name + ".asset",
                            source_object_id = GlobalObjectId.GetGlobalObjectIdSlow(source).ToString(), source_scene_path = source.gameObject.scene.path,
                            builder_flight = flight, first_source_vertex = flight * (source.sharedMesh.vertexCount / 2),
                            source_vertex_count = source.sharedMesh.vertexCount / 2,
                            first_source_index = flight * (source.sharedMesh.triangles.Length / 2), source_index_count = source.sharedMesh.triangles.Length / 2,
                            source_local_to_world = values });
                    }
            string receiptPath = Output + "/source-index-partitions.json";
            File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, receiptPath), JsonUtility.ToJson(new ReceiptFile { surfaces = receipts.ToArray() }, true));
            AssetDatabase.ImportAsset(receiptPath);
        }

        private static T GetOrAdd<T>(GameObject at) where T : Component
        {
            var component = at.GetComponent<T>();
            // Native Unity components can return a managed placeholder for a missing component. ?? does not test Unity null.
            if (component == null) component = Undo.AddComponent<T>(at);
            if (component == null) throw new InvalidOperationException("Failed to attach " + typeof(T).Name + " to " + at.name);
            return component;
        }

        /// <summary>Repair/reuse a partial prior setup surface without duplicating its GameObject or components.</summary>
        public static void EnsureSurfaceComponents(GameObject at, out MeshFilter filter, out MeshRenderer renderer)
        {
            if (at == null) throw new ArgumentNullException(nameof(at));
            filter = GetOrAdd<MeshFilter>(at);
            renderer = GetOrAdd<MeshRenderer>(at);
        }

        private static T KeepAsset<T>(T value, string path) where T : UnityEngine.Object
        {
            var current = AssetDatabase.LoadAssetAtPath<T>(path);
            if (current == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, current); EditorUtility.SetDirty(current); UnityEngine.Object.DestroyImmediate(value); return current;
        }
        private static Transform FindOrCreate(Transform parent, string name)
        {
            var existing = parent.Find(name); if (existing != null) return existing;
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Connect existing circulation parts");
            go.transform.SetParent(parent, false); return go.transform;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int last = path.LastIndexOf('/'); string parent = path.Substring(0, last); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(last + 1));
        }
    }
}
