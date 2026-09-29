using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// One straight run of sprinkler pipe between two ends (a <see cref="StationEquipment"/> of kind <see cref="PipeKind"/>): a cross main, a branch line with its heads, an
    /// audit head's short arm or the feed to the alarm valve. Where it is exposed (under a slab, a deck or a roof) it is red steel pipe made of 3 m lengths with a grooved
    /// coupling at every joint, hung on clevis hangers; the lengths and hangers are merged into one mesh when the spawner places it (<see cref="OnPlaced"/>), so a run of
    /// 60 m is one object. Above a finished ceiling it is a record with no mesh: a pipe in the ceiling void that a joint can fail in. The numbers come from the
    /// placement entry (<c>a</c>, <c>b</c> end points in world metres, <c>dn</c> nominal size, <c>role</c>, <c>exposed</c>, <c>valve</c>, <c>heads</c>, <c>ceil</c> mean ceiling height).
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class SprinklerPipeLine : MonoBehaviour, IEquipmentPlaced
    {
        public const string PipeKind = "sprinkler_pipe";

        /// <summary>Length of one pipe as delivered and pitch of the couplings: the model is 3.0 m with its coupling at the far end.</summary>
        public const float Pitch = 3f;
        private const float ModelOuterDiameter = .0763f, RodLength = .3f, HangerDrop = .376f;

        [SerializeField] private Mesh runMesh, hangerMesh;
        [SerializeField] private Material[] runMaterials = System.Array.Empty<Material>(), hangerMaterials = System.Array.Empty<Material>();

        public StationEquipment Equipment { get; private set; }
        public Vector3 A { get; private set; }
        public Vector3 B { get; private set; }
        /// <summary>Nominal size in millimetres (DN25 … DN150).</summary>
        public int Dn { get; private set; }
        public string Role { get; private set; } = "";
        public string Valve { get; private set; } = "";
        public int HeadCount { get; private set; }
        public bool Exposed { get; private set; }
        /// <summary>Height of the floor under it (what the staff stands on when it leaks).</summary>
        public float FloorY { get; private set; }
        public float Length => Vector3.Distance(A, B);

        /// <summary>The point at <paramref name="t"/> (0 at <see cref="A"/>, 1 at <see cref="B"/>) on the pipe axis.</summary>
        public Vector3 PointAt(float t) => Vector3.Lerp(A, B, Mathf.Clamp01(t));

        /// <summary>The coupling nearest <paramref name="t"/> (joints lie every <see cref="Pitch"/> metres; a pipe shorter than that has its ends).</summary>
        public Vector3 JointNear(float t)
        {
            int pieces = Pieces();
            int joint = Mathf.Clamp(Mathf.RoundToInt(t * pieces), 0, pieces);
            return PointAt(joint / (float)pieces);
        }

        private int Pieces() => Mathf.Max(1, Mathf.RoundToInt(Length / Pitch));

        /// <summary>Reads the placement data (called by the spawner and again by the director, which needs the numbers of pipes that were never built).</summary>
        public void Read()
        {
            Equipment = GetComponent<StationEquipment>();
            A = Point(Equipment.Text("a"));
            B = Point(Equipment.Text("b"));
            Dn = Mathf.RoundToInt(Equipment.Number("dn", 50));
            Role = Equipment.Text("role");
            Valve = Equipment.Text("valve");
            HeadCount = Mathf.RoundToInt(Equipment.Number("heads"));
            Exposed = Equipment.Text("exposed") == "1";
            FloorY = Equipment.Number("floor", A.y - 3f);
        }

        private static Vector3 Point(string text)
        {
            var parts = text.Split(',');
            if (parts.Length != 3) return Vector3.zero;
            return new Vector3(Parse(parts[0]), Parse(parts[1]), Parse(parts[2]));
        }

        private static float Parse(string text) => float.Parse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);

        public void OnPlaced()
        {
            Read();
            if (Exposed && runMesh != null) Build();
        }

        private void Build()
        {
            var dir = (B - A).normalized;
            float scale = Dn <= 0 ? 1f : OuterDiameter(Dn) / ModelOuterDiameter;
            int pieces = Pieces();
            float pitch = Length / pieces;
            float sign = Mathf.Sign(runMesh.bounds.center.x);
            var rotation = Quaternion.FromToRotation(new Vector3(sign, 0, 0), dir);
            var pipes = new List<Matrix4x4>(pieces);
            for (int i = 0; i < pieces; i++)
                pipes.Add(transform.worldToLocalMatrix * Matrix4x4.TRS(A + dir * (i * pitch), rotation, new Vector3(pitch / Pitch, scale, scale)));
            Attach("Run", Merge(runMesh, pipes, null, 0), runMaterials);
            // 수직 배관(입상관)에는 행거를 달지 않는다: 벽에 붙는다.
            if (hangerMesh == null || Mathf.Abs(dir.y) > .5f) return;
            var hangers = new List<Matrix4x4>(pieces);
            var extras = new List<float>(pieces);
            var flat = new Vector3(dir.x, 0, dir.z).normalized;
            var hangerRotation = Quaternion.FromToRotation(Vector3.right, flat);
            float ceiling = Equipment.Number("ceil", A.y + HangerDrop * scale);
            for (int i = 0; i < pieces; i++)
            {
                var at = A + dir * ((i + .5f) * pitch);
                hangers.Add(transform.worldToLocalMatrix * Matrix4x4.TRS(new Vector3(at.x, at.y + HangerDrop * scale, at.z), hangerRotation, Vector3.one * scale));
                extras.Add(Mathf.Max(0f, (ceiling - (at.y + HangerDrop * scale)) / scale));
            }
            Attach("Hanger", Merge(hangerMesh, hangers, extras, RodLength), hangerMaterials);
        }

        private void Attach(string name, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// Copies of <paramref name="source"/> under the given matrices in one mesh (one submesh per source submesh, so the source's materials apply). With
        /// <paramref name="rodExtra"/> the vertices above <c>-rodLength</c> are stretched and the ones below moved down by the instance's extra length: a hanger
        /// whose rod has to reach a higher ceiling.
        /// </summary>
        private static Mesh Merge(Mesh source, IReadOnlyList<Matrix4x4> matrices, IReadOnlyList<float> rodExtra, float rodLength)
        {
            var vertices = source.vertices;
            var normals = source.normals;
            var uvs = source.uv;
            var merged = new Mesh { name = source.name + " (merged)", indexFormat = IndexFormat.UInt32 };
            var outVertices = new List<Vector3>(vertices.Length * matrices.Count);
            var outNormals = new List<Vector3>(vertices.Length * matrices.Count);
            var outUvs = new List<Vector2>(vertices.Length * matrices.Count);
            var triangles = new List<int>[source.subMeshCount];
            for (int s = 0; s < triangles.Length; s++) triangles[s] = new List<int>();
            for (int m = 0; m < matrices.Count; m++)
            {
                int offset = outVertices.Count;
                float extra = rodExtra != null ? rodExtra[m] : 0f;
                for (int v = 0; v < vertices.Length; v++)
                {
                    var p = vertices[v];
                    if (extra > 0f) p.y = p.y > -rodLength ? p.y * (1f + extra / rodLength) : p.y - extra;
                    outVertices.Add(matrices[m].MultiplyPoint3x4(p));
                    outNormals.Add(matrices[m].MultiplyVector(normals[v]).normalized);
                    outUvs.Add(uvs.Length > v ? uvs[v] : Vector2.zero);
                }
                for (int s = 0; s < triangles.Length; s++)
                    foreach (int index in source.GetIndices(s)) triangles[s].Add(index + offset);
            }
            merged.SetVertices(outVertices);
            merged.SetNormals(outNormals);
            merged.SetUVs(0, outUvs);
            merged.subMeshCount = triangles.Length;
            for (int s = 0; s < triangles.Length; s++) merged.SetTriangles(triangles[s], s);
            merged.RecalculateBounds();
            return merged;
        }

        /// <summary>Outer diameter in metres of the steel pipe (KS D 3507) for a nominal size in millimetres.</summary>
        public static float OuterDiameter(int dn)
        {
            switch (dn)
            {
                case 25: return .034f;
                case 32: return .0427f;
                case 40: return .0486f;
                case 50: return .0605f;
                case 65: return .0763f;
                case 80: return .0891f;
                case 90: return .1016f;
                case 100: return .1143f;
                case 125: return .1398f;
                default: return .1652f;
            }
        }
    }
}
