#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Exact native Busanok tenant faces; appearance is photographic reference, not measured site registration.</summary>
    public static class BusanokFinishesSetup
    {
        const string Packet = "workers/twin/output/busanok-finishes-20261009/busanok-finishes-packet.json";
        const string ApprovedPacketHash = "12b0c4c7e2e3898bddf9ec57616e1b90e3b332520be43e1512f174da0a0850f4";
        const string Assets = "Assets/ChooGuard/Art/StationInterior/BusanokFinishes20261009";
        const string RootName = "관측 점포 마감 3F";
        const float Epsilon = .0001f;
        public sealed class Result
        {
            public GameObject InactiveRoot;
            // Stable order: ceiling main, existing ceiling shadow child, shared floor slab.
            public MeshFilter[] TargetFilters;
            public Mesh[] OriginalMeshes;
            public MeshCollider[] TargetColliders;
            public Mesh[] OriginalColliderMeshes;
            // Stable order: exact tenant wood floor, exact tenant pale ceiling.
            public MeshFilter[] CompanionFilters;
            public MeshCollider[] CompanionColliders;
            public string[] CreatedAssetPaths;
            public JObject Receipt;
        }
        sealed class Source
        {
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public Vector3[] Vertices, Normals;
            public Vector4[] Tangents;
            public Color[] Colors;
            public Color32[] Colors32;
            public bool ColorIsBytes;
            public int[] Triangles;
            public List<Vector4>[] Uv = new List<Vector4>[8];
            public int[] UvDimensions = new int[8];
        }
        static void Require(bool ok, string message)
        { if (!ok) throw new InvalidDataException("Busanok finishes preflight: " + message); }
        static string Hash(byte[] b)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }
        public static string GeometryHash(Mesh mesh)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            var v = mesh.vertices; var t = mesh.triangles; var n = mesh.normals;
            writer.Write(v.Length); writer.Write(t.Length); writer.Write(n.Length);
            foreach (var p in v) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); }
            foreach (int i in t) writer.Write(i);
            foreach (var p in n) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); }
            writer.Flush(); return Hash(stream.ToArray());
        }
        static Vector3 V3(JToken p) => new Vector3((float)p[0], (float)p[1], (float)p[2]);
        static Vector4 V4(JToken p) => new Vector4((float)p[0], (float)p[1], (float)p[2], p.Count() > 3 ? (float)p[3] : 0);
        static void MatrixEquals(Matrix4x4 actual, JToken expected, string label)
        {
            for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++)
                Require(float.IsFinite(actual[a, b]) && Mathf.Abs(actual[a, b] - (float)expected[a][b]) < Epsilon, label + " frame changed");
        }
        static Source Read(JToken row)
        {
            var r = row["renderer"]; var m = row["mesh"];
            var go = GameObject.Find((string)r["path"]); Require(go != null, "missing exact owner " + r["path"]);
            var f = go.GetComponent<MeshFilter>(); var renderer = go.GetComponent<MeshRenderer>();
            Require(f != null && f.sharedMesh != null && renderer != null && renderer.enabled && go.activeInHierarchy, "source not active/readable");
            // Inventory IDs describe the geometry owner's MeshFilter, not its renderer component.
            Require(GlobalObjectId.GetGlobalObjectIdSlow(f).ToString() == (string)r["global_object_id"], "source MeshFilter identity changed");
            Require(AssetDatabase.GetAssetPath(f.sharedMesh) == (string)m["asset"] && !EditorUtility.IsDirty(f.sharedMesh), "source asset changed or has unsaved edits");
            Require(Hash(File.ReadAllBytes((string)m["asset"])) == (string)row["asset_file_sha256"], "source asset bytes changed");
            Require(GeometryHash(f.sharedMesh) == (string)m["binary_sha256"], "source indexed geometry changed");
            MatrixEquals(f.transform.localToWorldMatrix, r["world_matrix"], "source");
            Require(f.sharedMesh.subMeshCount == 1 && renderer.sharedMaterials.Length == 1, "source material channel contract changed");
            Require(AssetDatabase.GetAssetPath(renderer.sharedMaterial) == (string)r["materials"][0], "retained material changed");
            var s = new Source { Filter = f, Renderer = renderer, Mesh = f.sharedMesh, Vertices = f.sharedMesh.vertices,
                Normals = f.sharedMesh.normals, Tangents = f.sharedMesh.tangents, Colors = f.sharedMesh.colors,
                Colors32 = f.sharedMesh.colors32, Triangles = f.sharedMesh.triangles };
            Require(s.Normals.Length == s.Vertices.Length && (s.Tangents.Length == 0 || s.Tangents.Length == s.Vertices.Length), "source channels incomplete");
            Require(s.Mesh.bindposes.Length == 0 && s.Mesh.blendShapeCount == 0 && !s.Mesh.HasVertexAttribute(VertexAttribute.BlendWeight) &&
                !s.Mesh.HasVertexAttribute(VertexAttribute.BlendIndices), "unexpected deformable geometry");
            s.ColorIsBytes = s.Mesh.HasVertexAttribute(VertexAttribute.Color) && s.Mesh.GetVertexAttributeFormat(VertexAttribute.Color) == VertexAttributeFormat.UNorm8;
            for (int u = 0; u < 8; u++)
            {
                s.Uv[u] = new List<Vector4>(); s.Mesh.GetUVs(u, s.Uv[u]);
                Require(s.Uv[u].Count == 0 || s.Uv[u].Count == s.Vertices.Length, "UV channel length changed");
                s.UvDimensions[u] = s.Uv[u].Count == 0 ? 0 : s.Mesh.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0 + u));
            }
            var descriptors = (JArray)row["native_vertex_channels"]["descriptors"];
            var values = row["native_vertex_channels"]["values"];
            for (int a = 0; a < descriptors.Count; a++)
            {
                int dim = (int)descriptors[a]["dimension"]; var attr = (VertexAttribute)a;
                Require(s.Mesh.HasVertexAttribute(attr) == (dim != 0), "native attribute set changed");
                if (dim == 0) continue;
                Require((int)descriptors[a]["format"] == 0 && s.Mesh.GetVertexAttributeFormat(attr) == VertexAttributeFormat.Float32 &&
                    s.Mesh.GetVertexAttributeDimension(attr) == dim, "native float attribute format changed");
                var expected = (JArray)values[a.ToString()]; Require(expected.Count == s.Vertices.Length, "declared attribute count changed");
                for (int i = 0; i < expected.Count; i++)
                {
                    Vector4 actual;
                    if (attr == VertexAttribute.Position) actual = s.Vertices[i];
                    else if (attr == VertexAttribute.Normal) actual = s.Normals[i];
                    else if (attr == VertexAttribute.Tangent) actual = s.Tangents[i];
                    else if (attr == VertexAttribute.Color) actual = s.Colors[i];
                    else if (a >= (int)VertexAttribute.TexCoord0 && a <= (int)VertexAttribute.TexCoord7) actual = s.Uv[a - (int)VertexAttribute.TexCoord0][i];
                    else throw new InvalidDataException("Unsupported native attribute");
                    for (int c = 0; c < dim; c++) Require(actual[c] == (float)expected[i][c], "exact native attribute value changed");
                }
            }
            return s;
        }
        static Material Material(JToken row)
        {
            string path = (string)row["asset"]; Require(Hash(File.ReadAllBytes(path)) == (string)row["file_sha256"], "reused material changed");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path); Require(material != null && !EditorUtility.IsDirty(material), "missing reused material or unsaved material edits");
            foreach (var map in (JArray)row["maps"])
            {
                string asset = (string)map["asset"];
                Require(Hash(File.ReadAllBytes(asset)) == (string)map["sha256"] && AssetDatabase.AssetPathToGUID(asset) == (string)map["guid"], "reused texture changed");
                var texture = material.GetTexture((string)map["property"]);
                Require(texture != null && !EditorUtility.IsDirty(texture) && AssetDatabase.GetAssetPath(texture) == asset, "reused texture binding changed or has unsaved edits");
            }
            return material;
        }
        sealed class Accumulator
        {
            readonly Source source;
            readonly bool seed;
            readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            readonly List<Vector4> tangents = new List<Vector4>();
            readonly List<Color> colors = new List<Color>();
            readonly List<Color32> colors32 = new List<Color32>();
            readonly List<int> triangles = new List<int>();
            readonly List<Vector4>[] uv = Enumerable.Range(0, 8).Select(_ => new List<Vector4>()).ToArray();
            public Accumulator(Source s, bool keepOriginalVertices)
            {
                source = s; seed = keepOriginalVertices;
                if (!seed) return;
                v.AddRange(s.Vertices); n.AddRange(s.Normals); tangents.AddRange(s.Tangents); colors.AddRange(s.Colors); colors32.AddRange(s.Colors32);
                for (int u = 0; u < 8; u++) uv[u].AddRange(s.Uv[u]);
            }
            public void OriginalTriangles(IEnumerable<int> ordinals)
            { Require(seed, "original indices require native vertex seed"); foreach (int o in ordinals) triangles.AddRange(source.Triangles.Skip(o * 3).Take(3)); }
            int Add(int[] ii, Vector3 weights, Vector3 position)
            {
                int next = v.Count; v.Add(position);
                n.Add(source.Normals[ii[0]] * weights.x + source.Normals[ii[1]] * weights.y + source.Normals[ii[2]] * weights.z);
                if (source.Tangents.Length != 0) tangents.Add(source.Tangents[ii[0]] * weights.x + source.Tangents[ii[1]] * weights.y + source.Tangents[ii[2]] * weights.z);
                if (source.Colors.Length != 0)
                {
                    var color = source.Colors[ii[0]] * weights.x + source.Colors[ii[1]] * weights.y + source.Colors[ii[2]] * weights.z;
                    colors.Add(color); colors32.Add(color);
                }
                for (int u = 0; u < 8; u++) if (source.Uv[u].Count != 0)
                    uv[u].Add(source.Uv[u][ii[0]] * weights.x + source.Uv[u][ii[1]] * weights.y + source.Uv[u][ii[2]] * weights.z);
                return next;
            }
            public void Fragments(JArray fragments, bool tenant)
            {
                foreach (var fragment in fragments)
                {
                    int ordinal = (int)fragment["source_triangle"]; Require(ordinal == 16 || ordinal == 18, "unexpected clipped source face");
                    Require((string)fragment["kind"] == (tenant ? "tenant" : "retained"), "fragment role changed");
                    int[] ii = source.Triangles.Skip(ordinal * 3).Take(3).ToArray(); var points = (JArray)fragment["vertices"];
                    Require(points.Count == 3, "fragment not triangular"); int start = triangles.Count;
                    foreach (var point in points)
                    {
                        Vector3 w = V3(point["barycentric"]), p = V3(point["position"]);
                        Require(float.IsFinite(w.x) && float.IsFinite(w.y) && float.IsFinite(w.z) && w.x >= -Epsilon && w.y >= -Epsilon && w.z >= -Epsilon &&
                            Mathf.Abs(w.x + w.y + w.z - 1) < Epsilon, "invalid source barycentric weights");
                        var native = source.Vertices[ii[0]] * w.x + source.Vertices[ii[1]] * w.y + source.Vertices[ii[2]] * w.z;
                        Require((native - p).sqrMagnitude < Epsilon * Epsilon && Mathf.Abs(p.y - 12.2f) < Epsilon, "split vertex departed native support");
                        if (tenant) Require(p.x >= 26.5f - Epsilon && p.x <= 36.5f + Epsilon && p.z >= -2.35f - Epsilon && p.z <= 6.75f + Epsilon, "tenant vertex outside footprint");
                        triangles.Add(Add(ii, w, p));
                    }
                    var cross = Vector3.Cross(v[triangles[start + 1]] - v[triangles[start]], v[triangles[start + 2]] - v[triangles[start]]);
                    Require(cross.y > Epsilon && Mathf.Abs(cross.x) < Epsilon && Mathf.Abs(cross.z) < Epsilon, "split facet winding/plane changed");
                }
            }
            public void Extract(int[] ordinals)
            {
                foreach (int o in ordinals)
                {
                    int[] ii = source.Triangles.Skip(o * 3).Take(3).ToArray();
                    for (int k = 0; k < 3; k++) triangles.Add(Add(ii, k == 0 ? Vector3.right : k == 1 ? Vector3.up : Vector3.forward, source.Vertices[ii[k]]));
                }
            }
            public Mesh Finish(string name)
            {
                var mesh = seed ? UnityEngine.Object.Instantiate(source.Mesh) : new Mesh(); mesh.name = name; mesh.indexFormat = source.Mesh.indexFormat;
                mesh.SetVertices(v); mesh.SetNormals(n);
                if (tangents.Count != 0) mesh.SetTangents(tangents);
                if (colors.Count != 0) { if (source.ColorIsBytes) mesh.SetColors(colors32); else mesh.SetColors(colors); }
                for (int u = 0; u < 8; u++) if (uv[u].Count != 0)
                {
                    int dim = source.UvDimensions[u];
                    if (dim == 2) mesh.SetUVs(u, uv[u].Select(x => new Vector2(x.x, x.y)).ToList());
                    else if (dim == 3) mesh.SetUVs(u, uv[u].Select(x => new Vector3(x.x, x.y, x.z)).ToList());
                    else { Require(dim == 4, "unsupported native UV dimension"); mesh.SetUVs(u, uv[u]); }
                }
                mesh.SetTriangles(triangles, 0, false); mesh.RecalculateBounds();
                if (seed) AssertNativePrefix(source, mesh);
                return mesh;
            }
        }
        static void AssertNativePrefix(Source s, Mesh mesh)
        {
            int count = s.Vertices.Length;
            Require(mesh.vertices.Take(count).SequenceEqual(s.Vertices) && mesh.normals.Take(count).SequenceEqual(s.Normals) &&
                mesh.tangents.Take(s.Tangents.Length).SequenceEqual(s.Tangents), "retained original vertex attributes changed");
            Require(mesh.colors.Take(s.Colors.Length).SequenceEqual(s.Colors) && mesh.colors32.Take(s.Colors32.Length).SequenceEqual(s.Colors32), "retained original colors changed");
            for (int u = 0; u < 8; u++)
            {
                var actual = new List<Vector4>(); mesh.GetUVs(u, actual);
                Require(actual.Take(s.Uv[u].Count).SequenceEqual(s.Uv[u]), "retained original UV changed");
                Require((actual.Count == 0 ? 0 : mesh.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0 + u))) == s.UvDimensions[u], "UV dimension changed");
            }
        }
        static Mesh CeilingRetained(Source source) => RetainNativeCeiling(source.Mesh);
        // Same native-ceiling filtering stage, independent of a historical installed Scene.
        public static Mesh RetainNativeCeiling(Mesh source)
        {
            Require(source!=null&&source.isReadable&&source.subMeshCount==1,"Readable original ceiling required");
            var s=new Source{Mesh=source,Vertices=source.vertices,Normals=source.normals,Tangents=source.tangents,
                Colors=source.colors,Colors32=source.colors32,Triangles=source.triangles};
            for(int channel=0;channel<8;channel++)
            {
                s.Uv[channel]=new List<Vector4>();source.GetUVs(channel,s.Uv[channel]);
                s.UvDimensions[channel]=s.Uv[channel].Count==0?0:source.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0+channel));
            }

            Require(s.Vertices.Length == 20 && s.Triangles.Length == 30 && s.Triangles.Take(6).Distinct().OrderBy(i => i).SequenceEqual(new[] { 0, 1, 2, 3 }), "shared ceiling native faces changed");
            foreach (int i in s.Triangles.Take(6)) Require(Mathf.Abs(s.Vertices[i].y - 15.64f) < Epsilon && s.Normals[i].y < -.99f, "Busanok ceiling plane changed");
            var mesh = UnityEngine.Object.Instantiate(s.Mesh); mesh.name = "Busanok_ceiling_retained_other4shops_8faces";
            mesh.SetTriangles(s.Triangles.Skip(6).ToArray(), 0, false); AssertNativePrefix(s, mesh);
            Require(mesh.triangles.SequenceEqual(s.Triangles.Skip(6)), "non-Busanok ceiling indices changed"); return mesh;
        }
        static double AreaXZ(Mesh mesh, Func<Vector3, Vector3, Vector3, bool> select = null)
        {
            var v = mesh.vertices; var t = mesh.triangles; double area = 0;
            for (int o = 0; o < t.Length; o += 3)
            {
                var a = v[t[o]]; var b = v[t[o + 1]]; var c = v[t[o + 2]];
                if (select != null && !select(a, b, c)) continue;
                area += Math.Abs(((double)b.x - a.x) * ((double)c.z - a.z) - ((double)b.z - a.z) * ((double)c.x - a.x)) / 2;
            }
            return area;
        }
        static bool Top(Vector3 a, Vector3 b, Vector3 c) => Mathf.Abs(a.y - 12.2f) < Epsilon && Mathf.Abs(b.y - 12.2f) < Epsilon && Mathf.Abs(c.y - 12.2f) < Epsilon;
        static void Asset(Mesh mesh, string directory, List<string> created)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + mesh.name + ".asset"); AssetDatabase.CreateAsset(mesh, path); created.Add(path);
            Require(AssetDatabase.LoadAssetAtPath<Mesh>(path) == mesh, "asset creation failed");
        }
        static MeshFilter Companion(GameObject root, string name, Mesh mesh, Material material, Source source, bool ceiling)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false); go.layer = source.Filter.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(source.Filter.gameObject));
            var f = go.AddComponent<MeshFilter>(); f.sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = ceiling ? ShadowCastingMode.TwoSided : source.Renderer.shadowCastingMode;
            r.receiveShadows = source.Renderer.receiveShadows; r.lightProbeUsage = source.Renderer.lightProbeUsage; r.reflectionProbeUsage = source.Renderer.reflectionProbeUsage;
            r.allowOcclusionWhenDynamic = source.Renderer.allowOcclusionWhenDynamic; return f;
        }
        public static Result Stage() => Stage(Packet, Assets);
        /// <summary>Caller activates, tests, saves and publishes two canonical source entries; this method locally rolls back every Stage failure.</summary>
        public static Result Stage(string packetPath, string assetDirectory)
        {
            Require(!EditorApplication.isPlaying && !EditorApplication.isCompiling, "stable edit mode required");
            Require(Hash(File.ReadAllBytes(packetPath)) == ApprovedPacketHash, "packet differs from reviewed exact facet packet");
            var packet = JObject.Parse(File.ReadAllText(packetPath)); Require((string)packet["schema"] == "chooguard.busanok-finishes-staging.v1", "packet schema changed");
            Require(SceneManager.GetActiveScene().path == (string)packet["expected_scene"], "wrong scene");
            Require(assetDirectory.StartsWith("Assets/ChooGuard/Art/StationInterior/BusanokFinishes", StringComparison.Ordinal), "fresh companion asset directory required");
            Require(GameObject.Find("실내 트윈 마감/" + RootName) == null, "finish correction frame already exists");
            var ceiling = Read(packet["sources"]["ceiling"]); var shadow = Read(packet["sources"]["ceiling_shadow"]); var floor = Read(packet["sources"]["floor"]);
            Require(ceiling.Mesh == shadow.Mesh, "ceiling main/shadow source must be identical");
            Require(ceiling.Filter.GetComponent<MeshCollider>() == null && shadow.Filter.GetComponent<MeshCollider>() == null, "unexpected ceiling collision owner");
            foreach (var s in new[] { ceiling, shadow }) MatrixEquals(s.Filter.transform.localToWorldMatrix, packet["sources"]["floor"]["renderer"]["world_matrix"], "shared source");
            var collider = floor.Filter.GetComponent<MeshCollider>(); Require(collider != null && collider.enabled && !collider.isTrigger && !collider.convex, "fixed floor collision source missing");
            Require(GlobalObjectId.GetGlobalObjectIdSlow(collider).ToString() == (string)packet["floor_collider"]["global_object_id"] && collider.sharedMesh == floor.Mesh, "floor renderer/collider source differs");
            var wood = Material(packet["materials"]["floor"]); var gypsum = Material(packet["materials"]["ceiling"]);
            Require(gypsum.FindPass("ShadowCaster") >= 0, "reused ceiling material must cast two-sided enclosure shadows");
            Require(floor.Vertices.Length == 68 && floor.Triangles.Length == 84, "original28-face floor slab changed");
            var rect = ((JArray)packet["target"]["native_rect_xz"]).Select(x => (float)x).ToArray();
            Require(rect.SequenceEqual(new[] { 26.5f, 36.5f, -2.35f, 6.75f }) && ((JArray)packet["target"]["floor_intersected_source_ordinals"]).Select(x => (int)x).SequenceEqual(new[] { 16, 18 }), "reviewed tenant scope changed");
            var targetFilters = new[] { ceiling.Filter, shadow.Filter, floor.Filter }; var originals = new[] { ceiling.Mesh, shadow.Mesh, floor.Mesh };
            var oldFilters = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToDictionary(f => f, f => f.sharedMesh);
            var oldColliders = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToDictionary(c => c, c => c.sharedMesh);
            var parent = GameObject.Find("실내 트윈 마감"); Require(parent != null, "retained parent missing");
            var memory = new List<Mesh>(); var created = new List<string>(); GameObject root = null; bool assigned = false, complete = false; Mesh originalCollider = collider.sharedMesh;
            try
            {
                var retainedCeiling = CeilingRetained(ceiling); memory.Add(retainedCeiling);
                var outside = new Accumulator(floor, true); int[] untouched = Enumerable.Range(0, 28).Where(o => o != 16 && o != 18).ToArray();
                outside.OriginalTriangles(untouched); outside.Fragments((JArray)packet["floor_fragments"]["retained"], false);
                var retainedFloor = outside.Finish("Busanok_floor_retained_source_outside_tenant"); memory.Add(retainedFloor);
                var inside = new Accumulator(floor, false); inside.Fragments((JArray)packet["floor_fragments"]["tenant"], true);
                var tenantFloor = inside.Finish("Busanok_exact_native_tenant_wood_floor"); memory.Add(tenantFloor);
                var ceilingInside = new Accumulator(ceiling, false); ceilingInside.Extract(new[] { 0, 1 });
                var tenantCeiling = ceilingInside.Finish("Busanok_exact_native_tenant_pale_ceiling"); memory.Add(tenantCeiling);
                Require(retainedFloor.triangles.Length / 3 == 38 && tenantFloor.triangles.Length / 3 == 4 && retainedCeiling.triangles.Length / 3 == 8 && tenantCeiling.triangles.Length / 3 == 2, "reviewed facet counts changed");
                Require(retainedFloor.triangles.Take(untouched.Length * 3).SequenceEqual(untouched.SelectMany(o => floor.Triangles.Skip(o * 3).Take(3))), "untouched slab bottom/perimeter/neighbor faces changed");
                double originalArea = AreaXZ(floor.Mesh, Top), retainedArea = AreaXZ(retainedFloor, Top), tenantArea = AreaXZ(tenantFloor);
                Require(Math.Abs(tenantArea - 91) < .0001 && Math.Abs(AreaXZ(tenantCeiling) - 91) < .0001 &&
                    Math.Abs(originalArea - retainedArea - tenantArea) < .0002, "source top support area conservation failed");
                root = new GameObject(RootName); root.SetActive(false); root.transform.SetParent(parent.transform, false);
                var local = parent.transform.worldToLocalMatrix * floor.Filter.transform.localToWorldMatrix;
                root.transform.localPosition = local.GetColumn(3); root.transform.localRotation = Quaternion.LookRotation(local.GetColumn(2), local.GetColumn(1));
                root.transform.localScale = new Vector3(local.GetColumn(0).magnitude, local.GetColumn(1).magnitude, local.GetColumn(2).magnitude);
                MatrixEquals(root.transform.localToWorldMatrix, packet["sources"]["floor"]["renderer"]["world_matrix"], "inactive companion");
                var floorFilter = Companion(root, "부산옥 원본 경계 목재 바닥", tenantFloor, wood, floor, false);
                var ceilingFilter = Companion(root, "부산옥 원본 경계 옅은 천장", tenantCeiling, gypsum, ceiling, true);
                var floorCollider = floorFilter.gameObject.AddComponent<MeshCollider>(); floorCollider.sharedMesh = tenantFloor; floorCollider.convex = false; floorCollider.isTrigger = false;
                Directory.CreateDirectory(assetDirectory); AssetDatabase.Refresh(); foreach (var mesh in memory) Asset(mesh, assetDirectory, created);
                assigned = true; ceiling.Filter.sharedMesh = retainedCeiling; shadow.Filter.sharedMesh = retainedCeiling; floor.Filter.sharedMesh = retainedFloor; collider.sharedMesh = retainedFloor;
                foreach (var kv in oldFilters) if (!targetFilters.Contains(kv.Key)) Require(kv.Key.sharedMesh == kv.Value, "another geometry owner changed");
                foreach (var kv in oldColliders) if (kv.Key != collider) Require(kv.Key.sharedMesh == kv.Value, "another collision owner changed");
                foreach (var key in new[] { "ceiling", "ceiling_shadow", "floor" })
                {
                    var source = key == "floor" ? floor : key == "ceiling" ? ceiling : shadow;
                    Require(GeometryHash(source.Mesh) == (string)packet["sources"][key]["mesh"]["binary_sha256"] && Hash(File.ReadAllBytes((string)packet["sources"][key]["mesh"]["asset"])) == (string)packet["sources"][key]["asset_file_sha256"], "original source was edited");
                }
                var receipt = new JObject { ["schema"] = "chooguard.busanok-finishes-staging-receipt.v1", ["production_installed"] = false,
                    ["reference_only"] = true, ["physical_accuracy_verified"] = false, ["packet_sha256"] = Hash(File.ReadAllBytes(packetPath)),
                    ["inactive_complete_root"] = root.name, ["removed_ceiling_triangles"] = 2, ["removed_ceiling_shadow_triangles"] = 2,
                    ["retained_ceiling_triangles"] = 8, ["companion_ceiling_triangles"] = 2, ["original_floor_triangles"] = 28,
                    ["split_source_floor_triangles"] = new JArray(16, 18), ["retained_floor_triangles"] = 38, ["companion_floor_triangles"] = 4,
                    ["original_floor_top_area"] = originalArea, ["retained_floor_top_area"] = retainedArea, ["companion_floor_top_area"] = tenantArea,
                    ["support_area_conserved"] = true, ["source_assets_unchanged"] = true, ["all_other_renderers_unchanged"] = true,
                    ["all_other_colliders_unchanged"] = true, ["no_coplanar_overlay"] = true, ["fixtures_added"] = 0,
                    ["channels"] = JObject.FromObject(new { floor = new { original_vertices = floor.Vertices.Length, retained_vertices = retainedFloor.vertexCount,
                        normals = floor.Normals.Length, tangents = floor.Tangents.Length, colors = floor.Colors.Length, uv_dimensions = floor.UvDimensions },
                        ceiling = new { original_vertices = ceiling.Vertices.Length, normals = ceiling.Normals.Length, tangents = ceiling.Tangents.Length,
                            colors = ceiling.Colors.Length, uv_dimensions = ceiling.UvDimensions } }),
                    ["root_world_matrix"] = JArray.FromObject(Enumerable.Range(0, 4).Select(i => Enumerable.Range(0, 4).Select(j => root.transform.localToWorldMatrix[i, j]).ToArray()).ToArray()),
                    ["created_asset_paths"] = JArray.FromObject(created), ["registry_publish_order"] = new JArray("ceiling main only with pale-ceiling companion", "floor with wood-floor companion and same-owner fixed collider companion") };
                complete = true; return new Result { InactiveRoot = root, TargetFilters = targetFilters, OriginalMeshes = originals,
                    TargetColliders = new[] { collider }, OriginalColliderMeshes = new[] { originalCollider }, CompanionFilters = new[] { floorFilter, ceilingFilter },
                    CompanionColliders = new[] { floorCollider }, CreatedAssetPaths = created.ToArray(), Receipt = receipt };
            }
            finally
            {
                if (!complete)
                {
                    if (assigned) { for (int i = 0; i < targetFilters.Length; i++) targetFilters[i].sharedMesh = originals[i]; collider.sharedMesh = originalCollider; }
                    if (root != null) UnityEngine.Object.DestroyImmediate(root);
                    foreach (string path in created.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(path);
                    foreach (var mesh in memory) if (mesh != null && !AssetDatabase.Contains(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }
    }
}
#endif
