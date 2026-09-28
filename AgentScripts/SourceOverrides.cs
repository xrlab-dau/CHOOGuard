// SourceOverrides — the ONLY way the protected source (FPSWorld/*) gets openings. Non-destructive: FBX/source assets are never
// written; a cut copy of the object's mesh is saved as a separate asset and swapped into the scene object, with a manifest that
// makes Revert exact.
//
// USAGE (Edit mode, via run_script; every call passes --project-path /Users/um-yunsang/CHOOGuard):
//   unity command run_script --no-banner --json --project-path /Users/um-yunsang/CHOOGuard --timeout 300 \
//     --file /Users/um-yunsang/CHOOGuard/AgentScripts/SourceOverrides.cs --entry SourceOverrides.Preview \
//     --args '[["<abs openings.json>", "<abs pose1.json>", "<abs out1.png>", ...]]' --timeout_ms 280000
//   Preview <openings.json> [pose.json out.png]...  lanes + Main. In memory only: writes <name>.preview.json next to the openings
//                  file (+ PNGs for pose/out pairs, rendered with temporary HideAndDontSave clones carrying the cut mesh while the
//                  originals get the non-serialized Renderer.forceRenderingOff). No asset, scene or manifest writes.
//   Apply <openings.json>                             Main only. Upserts the file's openings (by id) into the manifest and rebuilds
//                  every affected object from its ORIGINAL mesh (never cuts a cut mesh); saves
//                  Assets/ChooGuard/Art/StationInterior/SourceOverrides/<object>__open.asset; swaps MeshFilter.sharedMesh and each
//                  MeshCollider.sharedMesh that referenced the original (or the previous override); marks the scene dirty; no save.
//   Revert [id|objectPath|openings.json ...]          Main only. No args: every object back to its original, override assets
//                  deleted, manifest emptied. Opening ids: those openings removed, their objects rebuilt from the original with the
//                  remaining openings (or restored when none remain). Object paths: that object fully restored. A .json argument
//                  means every id in that openings file. Args may also be comma-separated.
//
// CONTRACT
//   openings.json: {"openings": [{"id", "target": "<scene path or unique path suffix>", "box": {"p0": [u,v], "p1": [u,v],
//     "half": t, "y": [y0, y1]}, "evidence": [...], "by": "<lane>"}]}. Station frame UVY (metres): world x = u sin16.2 + v cos16.2,
//     z = u cos16.2 - v sin16.2, y = Y. The box is the wall segment p0->p1 in UV, +-half across it, y0..y1 absolute.
//     Box frame for reports: s = along p0->p1 (0..L), w = across (+ = left of p0->p1 in UV, i.e. +V for a +U segment), y.
//   Cut: override = original minus the union of the object's boxes. Triangles are processed in station UVY (double precision):
//     a triangle with all vertices on the outer side of one box plane (1e-5 m tolerance; surfaces lying ON a box face, e.g. a floor
//     at y0, stay) is kept with its original indices; otherwise it is decomposed into triangle minus box by the six half-spaces
//     (piece_k = T ∩ inside(planes < k) ∩ outside(plane k)), each convex piece fan-triangulated. New vertices interpolate every
//     vertex attribute barycentrically (position, normal + tangent renormalised, tangent w from the dominant corner, colour, UV0-7
//     keeping each channel's dimension); new vertices are shared along shared source edges. Original vertices are kept, so
//     untouched triangles and submeshes keep identical indices and bounds; submesh count, topology and material slots are kept;
//     index format stays the original unless the vertex count needs UInt32. Skinned/blend-shape meshes are refused.
//   Reports (preview and manifest stats), per box: triangles removed (whole) / split, pieces kept, removed area total and per face
//     (facing along/across/up/down in the box frame from the Unity front normal cross(b-a, c-a), clustered by plane offset;
//     ratioToLxH = area / (L * (y1-y0)) so a pierced wall shows ~1 per face), removed-region bounds in UVY, warnings (no hit,
//     horizontal surfaces cut). Per object checks: far triangles (UVY AABB > 1 mm from every box) preserved index-identical,
//     max centroid depth of any output triangle inside a box, area balance (original - override = removed), independent
//     per-box area (T ∩ B clipped separately), untouched submesh indices + recomputed bounds, interpolation position error.
//   Manifest .planning/2026-09-27-openings-extension/source-overrides.json: objects[] {object, originalMesh {assetPath, name,
//     guid, localId}, override {assetPath, name, guid}, meshFilter, meshColliders, prefabOverrideBefore, openings[] (raw + source),
//     stats, utc}, history[].
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class SourceOverrides
{
    const string Folder = "Assets/ChooGuard/Art/StationInterior/SourceOverrides";
    const string ManifestRel = ".planning/2026-09-27-openings-extension/source-overrides.json";
    const double Eps = 1e-5, MinArea = 1e-10, FarMargin = 1e-3;
    static readonly double Sn = Math.Sin(16.2 * Math.PI / 180), Cs = Math.Cos(16.2 * Math.PI / 180);

    // ---------------------------------------------------------------- entry points
    public static void Preview(string[] args)
    {
        EditMode();
        if (args == null || args.Length < 1 || args.Length % 2 == 0) throw new ArgumentException("Preview <openings.json> [pose.json out.png]...");
        string file = Path.GetFullPath(args[0]);
        var incoming = ReadOpenings(file);
        JObject man = LoadManifest();
        var sceneDirtyBefore = DirtyScenes();
        var plans = Plan(man, incoming);
        var objects = new JArray(); var cuts = new List<(GameObject go, Mesh mesh)>();
        try
        {
            foreach (var p in plans)
            {
                Mesh original = OriginalFor(p.Go, p.Entry, out JObject origInfo);
                var cut = Cut(p.Go, original, p.Boxes);
                cut.Mesh.hideFlags = HideFlags.HideAndDontSave;
                cuts.Add((p.Go, cut.Mesh));
                cut.Report["object"] = p.Path; cut.Report["originalMesh"] = origInfo;
                cut.Report["openingsFromManifest"] = new JArray(p.Boxes.Where(b => !incoming.Any(i => i.Id == b.Id)).Select(b => b.Id));
                objects.Add(cut.Report);
            }
            var renders = new JArray();
            if (args.Length > 1) renders = RenderPoses(cuts, args.Skip(1).ToArray());
            var report = new JObject
            {
                ["tool"] = "AgentScripts/SourceOverrides.cs Preview", ["utc"] = DateTime.UtcNow.ToString("o"), ["openings"] = file,
                ["objects"] = objects, ["renders"] = renders,
                ["sceneDirtyBefore"] = new JArray(sceneDirtyBefore), ["sceneDirtyAfter"] = new JArray(DirtyScenes()),
                ["note"] = "In-memory preview; no asset, scene or manifest writes. Apply is Main's."
            };
            string outPath = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".preview.json");
            File.WriteAllText(outPath, report.ToString(Formatting.Indented));
            Debug.Log("SOURCE_OVERRIDES_PREVIEW " + outPath + " " + Summary(objects));
        }
        finally { foreach (var c in cuts) UnityEngine.Object.DestroyImmediate(c.mesh); }
    }

    public static void Apply(string[] args)
    {
        EditMode();
        if (args == null || args.Length != 1) throw new ArgumentException("Apply <openings.json>");
        string file = Path.GetFullPath(args[0]);
        var incoming = ReadOpenings(file);
        JObject man = LoadManifest();
        var plans = Plan(man, incoming);
        var done = new JArray();
        foreach (var p in plans) done.Add(Rebuild(man, p));
        History(man, "Apply", new JArray(file), done);
        SaveManifest(man);
        Debug.Log("SOURCE_OVERRIDES_APPLY " + ManifestPath + " " + Summary(new JArray(Objects(man).Where(o => done.Any(d => (string)d == (string)o["object"])))));
    }

    public static void Revert(string[] args)
    {
        EditMode();
        JObject man = LoadManifest();
        var tokens = (args ?? new string[0]).SelectMany(a => a.Split(',')).Select(a => a.Trim()).Where(a => a.Length > 0).ToList();
        var restore = new HashSet<string>(); var dropIds = new HashSet<string>();
        if (tokens.Count == 0) foreach (var o in Objects(man)) restore.Add((string)o["object"]);
        foreach (var t in tokens)
        {
            if (t.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) { foreach (var o in ReadOpenings(Path.GetFullPath(t))) dropIds.Add(o.Id); continue; }
            var obj = Objects(man).FirstOrDefault(o => PathMatches((string)o["object"], t));
            if (obj != null) { restore.Add((string)obj["object"]); continue; }
            if (!Objects(man).Any(o => ((JArray)o["openings"]).Any(x => (string)x["id"] == t))) throw new ArgumentException("Revert: '" + t + "' is neither an applied opening id nor an overridden object.");
            dropIds.Add(t);
        }
        var done = new JArray();
        foreach (var o in Objects(man).ToList())
        {
            string path = (string)o["object"];
            var keep = ((JArray)o["openings"]).OfType<JObject>().Where(x => !dropIds.Contains((string)x["id"])).ToList();
            if (restore.Contains(path) || keep.Count == 0) { Restore(man, o); done.Add(path + " (restored)"); }
            else if (keep.Count < ((JArray)o["openings"]).Count)
            {
                var go = FindObject(path, out _);
                done.Add(Rebuild(man, new ObjPlan { Go = go, Path = path, Entry = o, Boxes = keep.Select(x => Opening.Parse(x, (string)x["source"])).ToList() }) + " (rebuilt)");
            }
        }
        History(man, "Revert", new JArray(tokens), done);
        SaveManifest(man);
        Debug.Log("SOURCE_OVERRIDES_REVERT " + done.ToString(Formatting.None));
    }

    // ---------------------------------------------------------------- planning / manifest
    sealed class ObjPlan { public GameObject Go; public string Path; public JObject Entry; public List<Opening> Boxes; }

    // Effective box list per object = manifest openings (ids not in the file) + the file's openings; objects that lose an id to
    // another target are rebuilt too.
    static List<ObjPlan> Plan(JObject man, List<Opening> incoming)
    {
        var ids = new HashSet<string>();
        foreach (var o in incoming) if (!ids.Add(o.Id)) throw new ArgumentException("Duplicate opening id " + o.Id);
        var plans = new Dictionary<string, ObjPlan>();
        foreach (var o in incoming)
        {
            var go = FindObject(o.Target, out string path);
            if (!plans.TryGetValue(path, out var p))
            {
                var entry = Objects(man).FirstOrDefault(e => (string)e["object"] == path);
                p = plans[path] = new ObjPlan { Go = go, Path = path, Entry = entry, Boxes = new List<Opening>() };
                if (entry != null) foreach (JObject x in (JArray)entry["openings"]) if (!ids.Contains((string)x["id"])) p.Boxes.Add(Opening.Parse(x, (string)x["source"]));
            }
            p.Boxes.Add(o);
        }
        foreach (var e in Objects(man))
        {
            string path = (string)e["object"];
            if (plans.ContainsKey(path)) continue;
            var arr = ((JArray)e["openings"]).OfType<JObject>().ToList();
            if (!arr.Any(x => ids.Contains((string)x["id"]))) continue;
            plans[path] = new ObjPlan { Go = FindObject(path, out _), Path = path, Entry = e, Boxes = arr.Where(x => !ids.Contains((string)x["id"])).Select(x => Opening.Parse(x, (string)x["source"])).ToList() };
        }
        return plans.Values.ToList();
    }

    static string ManifestPath => Path.Combine(Directory.GetCurrentDirectory(), ManifestRel);
    static JObject LoadManifest() => File.Exists(ManifestPath) ? JObject.Parse(File.ReadAllText(ManifestPath))
        : new JObject { ["schema"] = "source-overrides/1", ["tool"] = "AgentScripts/SourceOverrides.cs", ["objects"] = new JArray(), ["history"] = new JArray() };
    static IEnumerable<JObject> Objects(JObject man) => ((JArray)man["objects"]).OfType<JObject>();
    static void SaveManifest(JObject man) { man["updatedUtc"] = DateTime.UtcNow.ToString("o"); Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)); File.WriteAllText(ManifestPath, man.ToString(Formatting.Indented)); }
    static void History(JObject man, string action, JArray args, JArray objects) =>
        ((JArray)man["history"]).Add(new JObject { ["utc"] = DateTime.UtcNow.ToString("o"), ["action"] = action, ["args"] = args, ["objects"] = objects });

    // Rebuild one object from its original with the plan's boxes; saves/overwrites the override asset and swaps references.
    static string Rebuild(JObject man, ObjPlan p)
    {
        if (p.Boxes.Count == 0) { Restore(man, p.Entry); return p.Path + " (restored)"; }
        var filter = p.Go.GetComponent<MeshFilter>() ?? throw new InvalidOperationException(p.Path + " has no MeshFilter.");
        Mesh original = OriginalFor(p.Go, p.Entry, out JObject origInfo);
        Mesh previous = p.Entry != null ? LoadOverride(p.Entry) : null;
        if (filter.sharedMesh != original && (previous == null || filter.sharedMesh != previous))
            throw new InvalidOperationException(p.Path + ": MeshFilter holds '" + (filter.sharedMesh ? filter.sharedMesh.name : "null") + "', neither the original nor the recorded override.");
        var cut = Cut(p.Go, original, p.Boxes);
        string assetPath = p.Entry != null ? (string)p.Entry["override"]["assetPath"] : UniqueAssetPath(man, p.Go.name);
        EnsureFolder(Folder);
        Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        cut.Mesh.name = Path.GetFileNameWithoutExtension(assetPath);
        if (asset != null) { EditorUtility.CopySerialized(cut.Mesh, asset); EditorUtility.SetDirty(asset); UnityEngine.Object.DestroyImmediate(cut.Mesh); }
        else { AssetDatabase.CreateAsset(cut.Mesh, assetPath); asset = cut.Mesh; }
        AssetDatabase.SaveAssets();
        bool filterOverrideBefore = p.Entry != null ? (bool)p.Entry["prefabOverrideBefore"]["meshFilter"] : Overridden(filter);
        var colliders = p.Go.GetComponents<MeshCollider>();
        JArray colliderBefore = p.Entry != null ? (JArray)p.Entry["prefabOverrideBefore"]["meshColliders"] : new JArray(colliders.Select(c => c.sharedMesh == original && Overridden(c)));
        Undo.RecordObject(filter, "SourceOverrides Apply"); filter.sharedMesh = asset;
        int swapped = 0;
        foreach (var c in colliders)
            if (c.sharedMesh == original || (previous != null && c.sharedMesh == previous)) { Undo.RecordObject(c, "SourceOverrides Apply"); c.sharedMesh = asset; swapped++; }
        EditorSceneManager.MarkSceneDirty(p.Go.scene);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long _);
        var entry = new JObject
        {
            ["object"] = p.Path, ["originalMesh"] = origInfo,
            ["override"] = new JObject { ["assetPath"] = assetPath, ["name"] = asset.name, ["guid"] = guid },
            ["meshFilter"] = true, ["meshColliders"] = swapped,
            ["prefabOverrideBefore"] = new JObject { ["meshFilter"] = filterOverrideBefore, ["meshColliders"] = colliderBefore },
            ["openings"] = new JArray(p.Boxes.Select(b => b.Raw)), ["stats"] = cut.Report, ["utc"] = DateTime.UtcNow.ToString("o")
        };
        var arr = (JArray)man["objects"];
        if (p.Entry != null) arr[arr.IndexOf(p.Entry)] = entry; else arr.Add(entry);
        return p.Path;
    }

    static void Restore(JObject man, JObject entry)
    {
        if (entry == null) return;
        string path = (string)entry["object"];
        var go = FindObject(path, out _);
        Mesh original = LoadOriginal(entry), over = LoadOverride(entry);
        var filter = go.GetComponent<MeshFilter>();
        if (filter != null && (filter.sharedMesh == over || filter.sharedMesh == null))
        {
            Undo.RecordObject(filter, "SourceOverrides Revert"); filter.sharedMesh = original;
            if (!(bool)entry["prefabOverrideBefore"]["meshFilter"]) RevertOverride(filter);
        }
        var colliders = go.GetComponents<MeshCollider>(); var before = (JArray)entry["prefabOverrideBefore"]["meshColliders"];
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i].sharedMesh == over)
            {
                Undo.RecordObject(colliders[i], "SourceOverrides Revert"); colliders[i].sharedMesh = original;
                if (i < before.Count && !(bool)before[i]) RevertOverride(colliders[i]);
            }
        EditorSceneManager.MarkSceneDirty(go.scene);
        string asset = (string)entry["override"]["assetPath"];
        if (!string.IsNullOrEmpty(asset) && AssetDatabase.LoadAssetAtPath<Mesh>(asset) != null) AssetDatabase.DeleteAsset(asset);
        ((JArray)man["objects"]).Remove(entry);
    }

    static bool Overridden(Component c)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(c)) return false;
        var sp = new SerializedObject(c).FindProperty("m_Mesh");
        return sp != null && sp.prefabOverride;
    }
    static void RevertOverride(Component c)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(c)) return;
        var sp = new SerializedObject(c).FindProperty("m_Mesh");
        if (sp != null && sp.prefabOverride) PrefabUtility.RevertPropertyOverride(sp, InteractionMode.AutomatedAction);
    }

    static Mesh OriginalFor(GameObject go, JObject entry, out JObject info)
    {
        Mesh m;
        if (entry != null) m = LoadOriginal(entry);
        else
        {
            m = (go.GetComponent<MeshFilter>() ?? throw new InvalidOperationException(go.name + " has no MeshFilter.")).sharedMesh
                ?? throw new InvalidOperationException(go.name + " has no mesh.");
            string p = AssetDatabase.GetAssetPath(m);
            if (string.IsNullOrEmpty(p)) throw new InvalidOperationException(go.name + ": original mesh '" + m.name + "' is not a persistent asset; unsupported (Revert could not be exact).");
            if (p.StartsWith(Folder + "/")) throw new InvalidOperationException(go.name + " already holds an override (" + p + ") that is not in the manifest; refusing to cut a cut mesh.");
        }
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out string guid, out long localId);
        info = new JObject { ["assetPath"] = AssetDatabase.GetAssetPath(m), ["name"] = m.name, ["guid"] = guid, ["localId"] = localId };
        return m;
    }
    static Mesh LoadOriginal(JObject entry)
    {
        var o = (JObject)entry["originalMesh"];
        string path = AssetDatabase.GUIDToAssetPath((string)o["guid"]);
        if (string.IsNullOrEmpty(path)) path = (string)o["assetPath"];
        long id = (long)o["localId"];
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out string _, out long l) && l == id) return a;
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(a => a.name == (string)o["name"])
            ?? throw new InvalidOperationException("Original mesh " + o.ToString(Formatting.None) + " not found.");
    }
    static Mesh LoadOverride(JObject entry) => AssetDatabase.LoadAssetAtPath<Mesh>((string)entry["override"]["assetPath"]);

    static string UniqueAssetPath(JObject man, string objectName)
    {
        string safe = new string(objectName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || ch == '/' || ch == '\\' ? '_' : ch).ToArray());
        var used = new HashSet<string>(Objects(man).Select(o => (string)o["override"]["assetPath"]));
        string path = Folder + "/" + safe + "__open.asset";
        for (int k = 2; used.Contains(path) || AssetDatabase.LoadMainAssetAtPath(path) != null; k++) path = Folder + "/" + safe + "__open_" + k + ".asset";
        return path;
    }
    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    // ---------------------------------------------------------------- openings
    sealed class Face { public double Area; public int Pieces; public double S0 = 1e9, S1 = -1e9, W0 = 1e9, W1 = -1e9, Y0 = 1e9, Y1 = -1e9; }

    sealed class Opening
    {
        public string Id, Target; public JObject Raw;
        public double U0, V0, Half, Y0, Y1, L, Au, Av, Nu, Nv, UMin, UMax, VMin, VMax;
        // per-cut statistics
        public int Removed, Split, Kept; public double RemovedArea, IndependentArea;
        public readonly Dictionary<string, Face> Faces = new Dictionary<string, Face>();
        public D3 BMin = new D3(1e9, 1e9, 1e9), BMax = new D3(-1e9, -1e9, -1e9);

        public static Opening Parse(JObject o, string source)
        {
            var b = (JObject)o["box"] ?? throw new ArgumentException("Opening without box: " + o.ToString(Formatting.None));
            var x = new Opening { Id = (string)o["id"], Target = (string)o["target"], Raw = (JObject)o.DeepClone() };
            if (string.IsNullOrEmpty(x.Id) || string.IsNullOrEmpty(x.Target)) throw new ArgumentException("Opening needs id and target: " + o.ToString(Formatting.None));
            if (source != null) x.Raw["source"] = source;
            double u1 = (double)b["p1"][0], v1 = (double)b["p1"][1];
            x.U0 = (double)b["p0"][0]; x.V0 = (double)b["p0"][1]; x.Half = (double)b["half"]; x.Y0 = (double)b["y"][0]; x.Y1 = (double)b["y"][1];
            x.L = Math.Sqrt((u1 - x.U0) * (u1 - x.U0) + (v1 - x.V0) * (v1 - x.V0));
            if (!(x.L > 1e-6 && x.Half > 0 && x.Y1 > x.Y0)) throw new ArgumentException("Degenerate box in opening " + x.Id);
            x.Au = (u1 - x.U0) / x.L; x.Av = (v1 - x.V0) / x.L; x.Nu = -x.Av; x.Nv = x.Au;
            x.UMin = Math.Min(x.U0, u1) - x.Half; x.UMax = Math.Max(x.U0, u1) + x.Half;
            x.VMin = Math.Min(x.V0, v1) - x.Half; x.VMax = Math.Max(x.V0, v1) + x.Half;
            return x;
        }
        public double S(D3 p) => (p.X - U0) * Au + (p.Y - V0) * Av;
        public double W(D3 p) => (p.X - U0) * Nu + (p.Y - V0) * Nv;
        // Plane k value; > 0 = outside. D3 = (u, v, y).
        public double F(int k, D3 p)
        {
            switch (k)
            {
                case 0: return -S(p);
                case 1: return S(p) - L;
                case 2: return -W(p) - Half;
                case 3: return W(p) - Half;
                case 4: return Y0 - p.Z;
                default: return p.Z - Y1;
            }
        }
        public double Depth(D3 p) { double d = double.MaxValue; for (int k = 0; k < 6; k++) d = Math.Min(d, -F(k, p)); return d; }
        public bool AabbOverlap(D3 lo, D3 hi, double m) => hi.X >= UMin - m && lo.X <= UMax + m && hi.Y >= VMin - m && lo.Y <= VMax + m && hi.Z >= Y0 - m && lo.Z <= Y1 + m;
    }

    static List<Opening> ReadOpenings(string file)
    {
        var root = JObject.Parse(File.ReadAllText(file));
        var arr = root["openings"] as JArray ?? throw new ArgumentException(file + " has no openings array.");
        return arr.OfType<JObject>().Select(o => Opening.Parse(o, file)).ToList();
    }

    // ---------------------------------------------------------------- geometry
    readonly struct D3
    {
        public readonly double X, Y, Z;
        public D3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static D3 operator +(D3 a, D3 b) => new D3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static D3 operator -(D3 a, D3 b) => new D3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static D3 operator *(D3 a, double s) => new D3(a.X * s, a.Y * s, a.Z * s);
        public static D3 Cross(D3 a, D3 b) => new D3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public double Len => Math.Sqrt(X * X + Y * Y + Z * Z);
        public static D3 Min(D3 a, D3 b) => new D3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
        public static D3 Max(D3 a, D3 b) => new D3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
    }
    // Polygon vertex: barycentric weights w.r.t. the source triangle + station UVY position.
    readonly struct PV { public readonly D3 B, P; public PV(D3 b, D3 p) { B = b; P = p; } }

    static double PolyArea(List<PV> poly, out D3 normal)
    {
        var acc = new D3(0, 0, 0);
        for (int i = 1; i + 1 < poly.Count; i++) acc = acc + D3.Cross(poly[i].P - poly[0].P, poly[i + 1].P - poly[0].P);
        normal = acc; return acc.Len * .5;
    }
    static double PolyArea(List<PV> poly) => PolyArea(poly, out _);

    static void SplitPoly(List<PV> poly, double[] f, List<PV> inside, List<PV> outside)
    {
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n; double fa = f[i], fb = f[j];
            int sa = fa > Eps ? 1 : fa < -Eps ? -1 : 0, sb = fb > Eps ? 1 : fb < -Eps ? -1 : 0;
            if (sa <= 0) inside.Add(poly[i]);
            if (sa >= 0) outside.Add(poly[i]);
            if (sa * sb < 0)
            {
                double t = fa / (fa - fb);
                var m = new PV(poly[i].B + (poly[j].B - poly[i].B) * t, poly[i].P + (poly[j].P - poly[i].P) * t);
                inside.Add(m); outside.Add(m);
            }
        }
    }

    // Triangle/polygon minus box: appends the outside convex pieces, returns the inside remainder (null when none).
    static List<PV> Subtract(List<PV> poly, Opening box, List<List<PV>> outside)
    {
        var cur = poly; var f = new double[16];
        for (int k = 0; k < 6; k++)
        {
            if (f.Length < cur.Count) f = new double[cur.Count * 2];
            double mn = double.MaxValue, mx = double.MinValue;
            for (int i = 0; i < cur.Count; i++) { f[i] = box.F(k, cur[i].P); mn = Math.Min(mn, f[i]); mx = Math.Max(mx, f[i]); }
            if (mn >= -Eps) { outside.Add(cur); return null; }
            if (mx <= Eps) continue;
            var inn = new List<PV>(cur.Count + 2); var outp = new List<PV>(cur.Count + 2);
            SplitPoly(cur, f, inn, outp);
            if (outp.Count >= 3 && PolyArea(outp) > MinArea) outside.Add(outp);
            if (inn.Count < 3) return null;
            cur = inn;
        }
        return cur;
    }
    // Box ∩ polygon only (independent area check).
    static double InsideArea(List<PV> poly, Opening box)
    {
        var cur = poly; var f = new double[16];
        for (int k = 0; k < 6 && cur.Count >= 3; k++)
        {
            if (f.Length < cur.Count) f = new double[cur.Count * 2];
            for (int i = 0; i < cur.Count; i++) f[i] = box.F(k, cur[i].P);
            var inn = new List<PV>(); var outp = new List<PV>();
            SplitPoly(cur, f, inn, outp); cur = inn;
        }
        return cur.Count >= 3 ? PolyArea(cur) : 0;
    }

    static void AddRemoved(Opening box, List<PV> piece, D3 frontNormal)
    {
        double area = PolyArea(piece);
        if (area <= MinArea) return;
        box.RemovedArea += area;
        double n = frontNormal.Len; if (n <= 0) return;
        double nu = frontNormal.X / n, nv = frontNormal.Y / n, ny = frontNormal.Z / n;
        double along = nu * box.Au + nv * box.Av, across = nu * box.Nu + nv * box.Nv;
        var c = new D3(0, 0, 0); foreach (var q in piece) c = c + q.P; c = c * (1.0 / piece.Count);
        string label; double offset;
        if (Math.Abs(ny) >= Math.Abs(along) && Math.Abs(ny) >= Math.Abs(across)) { label = ny > 0 ? "up" : "down"; offset = c.Z; }
        else if (Math.Abs(across) >= Math.Abs(along)) { label = across > 0 ? "across+" : "across-"; offset = box.W(c); }
        else { label = along > 0 ? "along+" : "along-"; offset = box.S(c); }
        string key = label + "@" + (Math.Round(offset / .05) * .05).ToString("0.00");
        if (!box.Faces.TryGetValue(key, out var face)) box.Faces[key] = face = new Face();
        face.Area += area; face.Pieces++;
        foreach (var q in piece)
        {
            double s = box.S(q.P), w = box.W(q.P);
            face.S0 = Math.Min(face.S0, s); face.S1 = Math.Max(face.S1, s); face.W0 = Math.Min(face.W0, w); face.W1 = Math.Max(face.W1, w);
            face.Y0 = Math.Min(face.Y0, q.P.Z); face.Y1 = Math.Max(face.Y1, q.P.Z);
            box.BMin = D3.Min(box.BMin, q.P); box.BMax = D3.Max(box.BMax, q.P);
        }
    }

    readonly struct VKey : IEquatable<VKey>
    {
        readonly int a, b, c; readonly long wa, wb;
        public VKey(int a, int b, int c, long wa, long wb) { this.a = a; this.b = b; this.c = c; this.wa = wa; this.wb = wb; }
        public bool Equals(VKey o) => a == o.a && b == o.b && c == o.c && wa == o.wa && wb == o.wb;
        public override bool Equals(object o) => o is VKey k && Equals(k);
        public override int GetHashCode() => unchecked(((((a * 397) ^ b) * 397 ^ c) * 397 ^ wa.GetHashCode()) * 397 ^ wb.GetHashCode());
    }

    sealed class CutOutput { public Mesh Mesh; public JObject Report; }

    static CutOutput Cut(GameObject go, Mesh src, List<Opening> boxes)
    {
        if (!src.isReadable) throw new InvalidOperationException(src.name + " is not readable.");
        if (src.blendShapeCount > 0 || src.HasVertexAttribute(VertexAttribute.BlendWeight) || src.HasVertexAttribute(VertexAttribute.BlendIndices))
            throw new NotSupportedException(src.name + ": skinned/blend-shape meshes are not supported.");
        Matrix4x4 m = go.transform.localToWorldMatrix;
        Vector3[] pos = src.vertices; int nv0 = pos.Length;
        var P = new D3[nv0];
        for (int i = 0; i < nv0; i++)
        {
            double lx = pos[i].x, ly = pos[i].y, lz = pos[i].z;
            double x = m.m00 * lx + m.m01 * ly + m.m02 * lz + m.m03, y = m.m10 * lx + m.m11 * ly + m.m12 * lz + m.m13, z = m.m20 * lx + m.m21 * ly + m.m22 * lz + m.m23;
            P[i] = new D3(Sn * x + Cs * z, Cs * x - Sn * z, y);
        }
        bool hasN = src.HasVertexAttribute(VertexAttribute.Normal), hasT = src.HasVertexAttribute(VertexAttribute.Tangent), hasC = src.HasVertexAttribute(VertexAttribute.Color);
        var nrm = hasN ? new List<Vector3>(src.normals) : null;
        var tan = hasT ? new List<Vector4>(src.tangents) : null;
        var col = hasC ? new List<Color>(src.colors) : null;
        var uvs = new List<Vector4>[8]; var uvDim = new int[8];
        for (int c = 0; c < 8; c++)
        {
            var attr = VertexAttribute.TexCoord0 + c;
            if (!src.HasVertexAttribute(attr)) continue;
            uvDim[c] = src.GetVertexAttributeDimension(attr); uvs[c] = new List<Vector4>(nv0); src.GetUVs(c, uvs[c]);
        }
        var outPos = new List<Vector3>(pos);
        var keyed = new Dictionary<VKey, int>();
        var newVertexUvy = new List<D3>();
        double maxInterpErr = 0;

        int AddVertex(int i0, int i1, int i2, D3 w, D3 uvy)
        {
            double[] ws = { w.X, w.Y, w.Z }; int[] ix = { i0, i1, i2 };
            for (int k = 0; k < 3; k++) if (ws[k] >= 1 - 1e-9) return ix[k];
            var parts = new List<(int i, double w)>();
            for (int k = 0; k < 3; k++) if (Math.Abs(ws[k]) > 1e-9) parts.Add((ix[k], ws[k]));
            parts.Sort((p, q) => p.i.CompareTo(q.i));
            var key = new VKey(parts[0].i, parts.Count > 1 ? parts[1].i : -1, parts.Count > 2 ? parts[2].i : -1,
                (long)Math.Round(parts[0].w * 1e8), parts.Count > 1 ? (long)Math.Round(parts[1].w * 1e8) : 0);
            if (keyed.TryGetValue(key, out int found)) return found;
            double sum = ws[0] + ws[1] + ws[2];
            double a = ws[0] / sum, b = ws[1] / sum, c = ws[2] / sum;
            int idx = outPos.Count;
            outPos.Add(new Vector3((float)(a * pos[i0].x + b * pos[i1].x + c * pos[i2].x), (float)(a * pos[i0].y + b * pos[i1].y + c * pos[i2].y), (float)(a * pos[i0].z + b * pos[i1].z + c * pos[i2].z)));
            if (hasN) nrm.Add(((float)a * nrm[i0] + (float)b * nrm[i1] + (float)c * nrm[i2]).normalized);
            if (hasT)
            {
                Vector4 t = (float)a * tan[i0] + (float)b * tan[i1] + (float)c * tan[i2];
                int dom = a >= b && a >= c ? i0 : b >= c ? i1 : i2;
                var xyz = new Vector3(t.x, t.y, t.z).normalized; tan.Add(new Vector4(xyz.x, xyz.y, xyz.z, tan[dom].w));
            }
            if (hasC) col.Add((float)a * col[i0] + (float)b * col[i1] + (float)c * col[i2]);
            for (int ch = 0; ch < 8; ch++) if (uvs[ch] != null) uvs[ch].Add((float)a * uvs[ch][i0] + (float)b * uvs[ch][i1] + (float)c * uvs[ch][i2]);
            // Interpolation check: the stored float local position must map back onto the double clip position.
            var v = outPos[idx]; double x = m.m00 * v.x + m.m01 * v.y + m.m02 * v.z + m.m03, y = m.m10 * v.x + m.m11 * v.y + m.m12 * v.z + m.m13, z = m.m20 * v.x + m.m21 * v.y + m.m22 * v.z + m.m23;
            maxInterpErr = Math.Max(maxInterpErr, (new D3(Sn * x + Cs * z, Cs * x - Sn * z, y) - uvy).Len);
            keyed[key] = idx; newVertexUvy.Add(uvy);
            return idx;
        }

        int subCount = src.subMeshCount;
        var outIdx = new List<int>[subCount]; var topo = new MeshTopology[subCount]; var cutTris = new int[subCount];
        double areaOrig = 0, areaKeptCutTris = 0, areaOrigCutTris = 0;
        int farTris = 0, cutTotal = 0;
        var farSet = new List<(int s, int a, int b, int c)>();
        var nonTriangle = new JArray();
        foreach (var bx in boxes) { bx.Removed = bx.Split = bx.Kept = 0; bx.RemovedArea = bx.IndependentArea = 0; bx.Faces.Clear(); }

        for (int s = 0; s < subCount; s++)
        {
            topo[s] = src.GetTopology(s);
            int[] idx = src.GetIndices(s, true);
            if (topo[s] != MeshTopology.Triangles) { outIdx[s] = new List<int>(idx); nonTriangle.Add(s); continue; }
            var o = new List<int>(idx.Length);
            for (int t = 0; t + 2 < idx.Length; t += 3)
            {
                int i0 = idx[t], i1 = idx[t + 1], i2 = idx[t + 2];
                D3 a = P[i0], b = P[i1], c = P[i2];
                D3 lo = D3.Min(a, D3.Min(b, c)), hi = D3.Max(a, D3.Max(b, c));
                D3 front = D3.Cross(b - a, c - a);
                double triArea = front.Len * .5; areaOrig += triArea;
                bool far = true;
                List<List<PV>> polys = null;
                foreach (var box in boxes)
                {
                    if (!box.AabbOverlap(lo, hi, FarMargin)) continue;
                    far = false;
                    var triPoly = new List<PV> { new PV(new D3(1, 0, 0), a), new PV(new D3(0, 1, 0), b), new PV(new D3(0, 0, 1), c) };
                    box.IndependentArea += InsideArea(triPoly, box);
                    if (polys == null)
                    {
                        bool outside = false;
                        for (int k = 0; k < 6 && !outside; k++) outside = box.F(k, a) >= -Eps && box.F(k, b) >= -Eps && box.F(k, c) >= -Eps;
                        if (outside) continue;
                        polys = new List<List<PV>> { triPoly };
                    }
                    var next = new List<List<PV>>(); double removedHere = 0;
                    foreach (var poly in polys)
                    {
                        var inside = Subtract(poly, box, next);
                        if (inside != null) { double ar = PolyArea(inside); if (ar > MinArea) { AddRemoved(box, inside, front); removedHere += ar; } }
                    }
                    if (removedHere > 0) { if (next.Count == 0) box.Removed++; else box.Split++; }
                    polys = next;
                }
                if (far) { farTris++; farSet.Add((s, i0, i1, i2)); }
                if (polys == null) { o.Add(i0); o.Add(i1); o.Add(i2); continue; }
                cutTris[s]++; cutTotal++; areaOrigCutTris += triArea;
                double kept = 0;
                foreach (var poly in polys)
                {
                    kept += PolyArea(poly);
                    for (int k = 1; k + 1 < poly.Count; k++)
                    {
                        var tri = new List<PV> { poly[0], poly[k], poly[k + 1] };
                        if (PolyArea(tri) <= MinArea) continue;
                        o.Add(AddVertex(i0, i1, i2, poly[0].B, poly[0].P)); o.Add(AddVertex(i0, i1, i2, poly[k].B, poly[k].P)); o.Add(AddVertex(i0, i1, i2, poly[k + 1].B, poly[k + 1].P));
                    }
                }
                areaKeptCutTris += kept;
            }
            outIdx[s] = o;
        }

        // ---- build mesh
        var mesh = new Mesh { name = src.name + "__open" };
        int nv = outPos.Count;
        mesh.indexFormat = src.indexFormat == IndexFormat.UInt32 || nv > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(outPos);
        if (hasN) mesh.SetNormals(nrm);
        if (hasT) mesh.SetTangents(tan);
        if (hasC) mesh.SetColors(col);
        for (int ch = 0; ch < 8; ch++)
        {
            if (uvs[ch] == null) continue;
            if (uvDim[ch] == 2) mesh.SetUVs(ch, uvs[ch].Select(q => (Vector2)q).ToList());
            else if (uvDim[ch] == 3) mesh.SetUVs(ch, uvs[ch].Select(q => (Vector3)q).ToList());
            else mesh.SetUVs(ch, uvs[ch]);
        }
        mesh.subMeshCount = subCount;
        for (int s = 0; s < subCount; s++) mesh.SetIndices(outIdx[s], topo[s], s, false);
        Bounds all = default; bool anyB = false;
        var subReports = new JArray(); double maxUntouchedBoundsDelta = 0; bool untouchedIdentical = true;
        for (int s = 0; s < subCount; s++)
        {
            var d = mesh.GetSubMesh(s); var od = src.GetSubMesh(s);
            if (cutTris[s] == 0)
            {
                d.bounds = od.bounds; d.firstVertex = od.firstVertex; d.vertexCount = od.vertexCount;
                // independent check: bounds recomputed from the vertices this submesh uses vs the original descriptor
                var rb = UsedBounds(outIdx[s], outPos);
                if (outIdx[s].Count > 0) maxUntouchedBoundsDelta = Math.Max(maxUntouchedBoundsDelta, Math.Max((rb.min - od.bounds.min).magnitude, (rb.max - od.bounds.max).magnitude));
                if (!outIdx[s].SequenceEqual(src.GetIndices(s, true))) untouchedIdentical = false;
            }
            else
            {
                int lo = int.MaxValue, hi = -1; foreach (int i in outIdx[s]) { lo = Math.Min(lo, i); hi = Math.Max(hi, i); }
                if (hi >= 0) { d.firstVertex = lo; d.vertexCount = hi - lo + 1; d.bounds = UsedBounds(outIdx[s], outPos); } else { d.firstVertex = 0; d.vertexCount = 0; d.bounds = default; }
                subReports.Add(new JObject { ["submesh"] = s, ["cutTriangles"] = cutTris[s], ["indicesBefore"] = od.indexCount, ["indicesAfter"] = outIdx[s].Count });
            }
            mesh.SetSubMesh(s, d, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            if (d.indexCount > 0) { if (anyB) all.Encapsulate(d.bounds); else { all = d.bounds; anyB = true; } }
        }
        mesh.bounds = anyB ? all : src.bounds;

        // ---- checks
        var outTris = new HashSet<(int, int, int, int)>();
        double areaNew = 0, maxDepth = double.MinValue; string deepest = null;
        for (int s = 0; s < subCount; s++)
        {
            if (topo[s] != MeshTopology.Triangles) continue;
            var o = outIdx[s];
            for (int t = 0; t + 2 < o.Count; t += 3)
            {
                outTris.Add((s, o[t], o[t + 1], o[t + 2]));
                D3 a = UvyOf(o[t]), b = UvyOf(o[t + 1]), c = UvyOf(o[t + 2]);
                areaNew += D3.Cross(b - a, c - a).Len * .5;
                D3 cen = (a + b + c) * (1.0 / 3);
                foreach (var box in boxes) { double dep = box.Depth(cen); if (dep > maxDepth) { maxDepth = dep; deepest = box.Id + " submesh " + s + " tri " + t / 3; } }
            }
        }
        D3 UvyOf(int i) => i < nv0 ? P[i] : newVertexUvy[i - nv0];
        int farMissing = farSet.Count(f => !outTris.Contains(f));
        double removedTotal = boxes.Sum(b => b.RemovedArea);
        var boxReports = new JArray();
        foreach (var box in boxes)
        {
            double lxh = box.L * (box.Y1 - box.Y0);
            var faces = new JArray(box.Faces.OrderByDescending(f => f.Value.Area).Select(f => new JObject
            {
                ["face"] = f.Key, ["area"] = R(f.Value.Area), ["pieces"] = f.Value.Pieces,
                ["s"] = new JArray(R(f.Value.S0), R(f.Value.S1)), ["w"] = new JArray(R(f.Value.W0), R(f.Value.W1)), ["y"] = new JArray(R(f.Value.Y0), R(f.Value.Y1)),
                ["rectFill"] = R(f.Value.Area / Math.Max(1e-12, f.Key.StartsWith("across") ? (f.Value.S1 - f.Value.S0) * (f.Value.Y1 - f.Value.Y0)
                    : f.Key.StartsWith("along") ? (f.Value.W1 - f.Value.W0) * (f.Value.Y1 - f.Value.Y0) : (f.Value.S1 - f.Value.S0) * (f.Value.W1 - f.Value.W0))),
                ["ratioToLxH"] = f.Key.StartsWith("across") ? R(f.Value.Area / lxh) : null
            }));
            var warnings = new JArray();
            if (box.RemovedArea <= MinArea) warnings.Add("box removes nothing from this object");
            double horiz = box.Faces.Where(f => f.Key.StartsWith("up") || f.Key.StartsWith("down")).Sum(f => f.Value.Area);
            if (horiz > .01) warnings.Add("cuts horizontal surfaces (" + R(horiz) + " m2): check floors/ceilings stay intact");
            boxReports.Add(new JObject
            {
                ["id"] = box.Id, ["by"] = box.Raw["by"], ["source"] = box.Raw["source"], ["box"] = box.Raw["box"],
                ["length"] = R(box.L), ["height"] = R(box.Y1 - box.Y0), ["LxH"] = R(lxh),
                ["trianglesRemoved"] = box.Removed, ["trianglesSplit"] = box.Split,
                ["removedArea"] = R(box.RemovedArea), ["independentInsideArea"] = R(box.IndependentArea),
                ["removedBoundsUVY"] = box.RemovedArea > 0 ? new JObject { ["min"] = new JArray(R(box.BMin.X), R(box.BMin.Y), R(box.BMin.Z)), ["max"] = new JArray(R(box.BMax.X), R(box.BMax.Y), R(box.BMax.Z)) } : null,
                ["faces"] = faces, ["warnings"] = warnings
            });
        }
        var attrs = new JArray(src.GetVertexAttributes().Select(a => a.attribute + ":" + a.format + "x" + a.dimension));
        var report = new JObject
        {
            ["mesh"] = new JObject
            {
                ["vertices"] = nv0, ["verticesAfter"] = nv, ["newVertices"] = nv - nv0, ["triangles"] = src.triangles.Length / 3,
                ["trianglesAfter"] = outTris.Count, ["submeshes"] = subCount, ["indexFormat"] = src.indexFormat.ToString(), ["indexFormatAfter"] = mesh.indexFormat.ToString(),
                ["attributes"] = attrs, ["attributesAfter"] = new JArray(mesh.GetVertexAttributes().Select(a => a.attribute + ":" + a.format + "x" + a.dimension)),
                ["nonTriangleSubmeshes"] = nonTriangle, ["cutSubmeshes"] = subReports,
                ["boundsBefore"] = BoundsJson(src.bounds), ["boundsAfter"] = BoundsJson(mesh.bounds)
            },
            ["boxes"] = boxReports,
            ["checks"] = new JObject
            {
                ["cutTriangles"] = cutTotal,
                ["farTriangles"] = farTris, ["farTrianglesMissing"] = farMissing,
                ["untouchedSubmeshIndicesIdentical"] = untouchedIdentical, ["untouchedSubmeshBoundsMaxDelta"] = maxUntouchedBoundsDelta,
                ["maxCentroidDepthInsideBox"] = boxes.Count > 0 ? R(maxDepth, 7) : null, ["deepestTriangle"] = deepest,
                ["areaBefore"] = R(areaOrig), ["areaAfter"] = R(areaNew), ["areaRemovedStats"] = R(removedTotal),
                ["areaBalanceError"] = R(areaOrig - areaNew - removedTotal, 7),
                ["cutTrianglesAreaBefore"] = R(areaOrigCutTris), ["cutTrianglesAreaKept"] = R(areaKeptCutTris),
                ["maxNewVertexPositionError"] = R(maxInterpErr, 7),
                ["pass"] = farMissing == 0 && untouchedIdentical && maxUntouchedBoundsDelta < 1e-4 && (boxes.Count == 0 || maxDepth <= 1e-4)
                    && Math.Abs(areaOrig - areaNew - removedTotal) <= 1e-6 * Math.Max(1, areaOrig) + 1e-4 && maxInterpErr < 1e-3
            }
        };
        return new CutOutput { Mesh = mesh, Report = report };
    }

    static Bounds UsedBounds(List<int> idx, List<Vector3> pos)
    {
        if (idx.Count == 0) return default;
        Vector3 lo = pos[idx[0]], hi = lo;
        foreach (int i in idx) { lo = Vector3.Min(lo, pos[i]); hi = Vector3.Max(hi, pos[i]); }
        var b = new Bounds(); b.SetMinMax(lo, hi); return b;
    }
    static JObject BoundsJson(Bounds b) => new JObject { ["min"] = new JArray(b.min.x, b.min.y, b.min.z), ["max"] = new JArray(b.max.x, b.max.y, b.max.z) };
    static double R(double v, int d = 4) => Math.Round(v, d);

    // ---------------------------------------------------------------- scene helpers
    static void EditMode() { if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("SourceOverrides runs in Edit mode only."); }
    static bool PathMatches(string full, string target) { target = target.Trim().Trim('/'); return full == target || full.EndsWith("/" + target); }
    static List<string> DirtyScenes()
    {
        var l = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++) { var s = SceneManager.GetSceneAt(i); if (s.isDirty) l.Add(s.path); }
        return l;
    }
    static GameObject FindObject(string target, out string fullPath)
    {
        var hits = new List<(GameObject go, string path)>();
        void Walk(Transform t, string path)
        {
            if (PathMatches(path, target)) hits.Add((t.gameObject, path));
            foreach (Transform c in t) Walk(c, path + "/" + c.name);
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i); if (!s.isLoaded) continue;
            foreach (var r in s.GetRootGameObjects()) Walk(r.transform, r.name);
        }
        if (hits.Count != 1) throw new InvalidOperationException("Target '" + target + "' matched " + hits.Count + " objects" + (hits.Count > 1 ? ": " + string.Join(" | ", hits.Select(h => h.path)) : "."));
        fullPath = hits[0].path; return hits[0].go;
    }
    static string Summary(JArray objects) => new JArray(objects.OfType<JObject>().Select(o => new JObject
    {
        ["object"] = o["object"], ["pass"] = (o["checks"] ?? o["stats"]?["checks"])?["pass"],
        ["boxes"] = new JArray(((JArray)(o["boxes"] ?? o["stats"]?["boxes"]) ?? new JArray()).Select(b => b["id"] + " removed " + b["trianglesRemoved"] + " split " + b["trianglesSplit"] + " area " + b["removedArea"]))
    })).ToString(Formatting.None);

    // ---------------------------------------------------------------- preview render
    static JArray RenderPoses(List<(GameObject go, Mesh mesh)> cuts, string[] pairs)
    {
        var clones = new List<GameObject>(); var hidden = new List<Renderer>(); var outList = new JArray();
        try
        {
            foreach (var (go, mesh) in cuts)
            {
                var r = go.GetComponent<MeshRenderer>(); if (r == null) continue;
                var c = new GameObject("SourceOverrides preview " + go.name) { hideFlags = HideFlags.HideAndDontSave, layer = go.layer };
                c.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation); c.transform.localScale = go.transform.lossyScale;
                c.AddComponent<MeshFilter>().sharedMesh = mesh;
                var cr = c.AddComponent<MeshRenderer>();
                cr.sharedMaterials = r.sharedMaterials; cr.lightmapIndex = r.lightmapIndex; cr.lightmapScaleOffset = r.lightmapScaleOffset;
                cr.shadowCastingMode = r.shadowCastingMode; cr.receiveShadows = r.receiveShadows; cr.renderingLayerMask = r.renderingLayerMask;
                clones.Add(c);
                if (!r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
            }
            for (int i = 0; i + 1 < pairs.Length; i += 2) { Capture(pairs[i], pairs[i + 1]); outList.Add(Path.GetFullPath(pairs[i + 1])); }
        }
        finally
        {
            foreach (var r in hidden) if (r) r.forceRenderingOff = false;
            foreach (var c in clones) UnityEngine.Object.DestroyImmediate(c);
        }
        return outList;
    }

    // Same projection as CaptureRegisteredScene.Calibrated (pose {"width","height","verticalFov","position","forward","up"}).
    static void Capture(string posePath, string output)
    {
        var pose = JObject.Parse(File.ReadAllText(posePath));
        int width = (int)pose["width"], height = (int)pose["height"]; float fov = (float)pose["verticalFov"];
        Vector3 V(JToken t) => new Vector3((float)t[0], (float)t[1], (float)t[2]);
        var go = new GameObject("SourceOverrides preview camera") { hideFlags = HideFlags.HideAndDontSave };
        RenderTexture target = null; Texture2D image = null; var prevActive = RenderTexture.active;
        try
        {
            var cam = go.AddComponent<Camera>();
            var view = SceneView.lastActiveSceneView; if (view != null) cam.CopyFrom(view.camera);
            cam.enabled = false; cam.orthographic = false; cam.usePhysicalProperties = false; cam.fieldOfView = fov; cam.nearClipPlane = .08f;
            go.transform.SetPositionAndRotation(V(pose["position"]), Quaternion.LookRotation(V(pose["forward"]), V(pose["up"])));
            target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = target; cam.aspect = (float)width / height; cam.ResetProjectionMatrix(); cam.Render();
            RenderTexture.active = target;
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(false, false);
            string path = Path.GetFullPath(output); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
            go.GetComponent<Camera>().targetTexture = null;
        }
        finally
        {
            RenderTexture.active = prevActive;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            if (target != null) RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
