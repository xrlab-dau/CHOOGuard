#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.Editor.Twin
{
    // Compose the reviewed architectural edits after the immutable B24/blue/pane records.
    // Historical records continue to identify their original output, never the new mesh.
    public static class ConcourseFidelitySuccessor
    {
        const string Contract = "content/world/observed-interiors/concourse-carrier-successor-20261010.json";
        const string ContractHash = "829a4da9f2117c666e851072c05e28b08395627236c7b3eddb595b4f7953d9bf";
        const string Initial = ".planning/2026-10-10-digital-twin-fidelity-rebuild/installation/receipt.json";
        const string Rail = "content/world/observed-interiors/concourse-perimeter-rail-20261010.json";
        const string Panel = "content/world/observed-interiors/concourse-perimeter-panel-20261010.json";
        const string N5 = Platform6BluePartitionTerminal.Native5;
        const string N6 = "FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_6";
        static void Need(bool ok, string why) { if (!ok) throw new InvalidDataException("Concourse successor: " + why); }
        static string Hash(string p) => ConcourseFidelityCorrection.Hash(p);
        static JObject Json(string p) => JObject.Parse(File.ReadAllText(p));
        static void Pin(JToken p)
        {
            string path = (string)p["path"];
            Need(Hash(path) == (string)p["sha256"] && Hash(path + ".meta") == (string)p["meta_sha256"] &&
                 AssetDatabase.AssetPathToGUID(path) == (string)p["guid"], "saved asset identity " + path);
        }
        static Mesh Asset(JToken pin)
        {
            Pin(pin); var mesh = AssetDatabase.LoadAssetAtPath<Mesh>((string)pin["path"]);
            Need(mesh != null && mesh.isReadable && !EditorUtility.IsDirty(mesh), "readable clean saved mesh"); return mesh;
        }
        static JObject Request(JObject receipt)
        {
            Need(Hash((string)receipt["request"]) == (string)receipt["request_sha256"], "immutable request");
            var request = Json((string)receipt["request"]);
            foreach (var p in request["files"]) Need(Hash((string)p["path"]) == (string)p["sha256"], "request input " + p["path"]);
            return request;
        }
        static JObject Read()
        {
            if (!File.Exists(ConcourseFidelityCorrection.Registry)) { Need(!File.Exists(Contract), "orphan successor contract"); return null; }
            Need(Hash(Contract) == ContractHash, "frozen architecture composition contract");
            foreach (var pin in Json(Contract)["files"]) Need(Hash((string)pin["path"]) == (string)pin["sha256"], "frozen composition input " + pin["path"]);
            return Json(Initial);
        }
        static void Same(Mesh expected, Mesh actual)
        {
            Need(ArchitecturalCorrectionRegistry.CompleteChannelDigest(expected) == ArchitecturalCorrectionRegistry.CompleteChannelDigest(actual),
                 "full channel and face replay differs");
        }
        static void Retired(Mesh source, Mesh output, JToken selection, int count)
        {
            var retired = new HashSet<int>(selection.Select(x => (int)x)); Need(retired.Count == count, "exact face scope");
            var copy = UnityEngine.Object.Instantiate(source); int ordinal = 0, removed = 0;
            try
            {
                for (int sm = 0; sm < source.subMeshCount; sm++)
                {
                    var ix = source.GetTriangles(sm); var keep = new List<int>();
                    for (int k = 0; k < ix.Length; k += 3, ordinal++)
                    { if (retired.Contains(ordinal)) removed++; else keep.AddRange(new[] { ix[k], ix[k + 1], ix[k + 2] }); }
                    copy.SetTriangles(keep, sm, false);
                }
                Need(removed == count, "every selected face found"); Same(copy, output);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
        static void Floor(Mesh source, Mesh output, JObject recipe)
        {
            int sm = (int)recipe["source_submesh"], first = 0;
            for (int i = 0; i < sm; i++) first += (int)source.GetIndexCount(i) / 3;
            Need(recipe["triangle_ordinals"].Select(x => (int)x).SequenceEqual(Enumerable.Range(first, 18)), "exact eighteen floor faces");
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                int slot = source.subMeshCount; var rest = source.GetSubMesh(sm); var floor = rest;
                Need(rest.topology == MeshTopology.Triangles && rest.indexCount == 180, "original floor and stairs scope");
                copy.subMeshCount = slot + 1; floor.indexCount = 54; rest.indexStart += 54; rest.indexCount -= 54;
                copy.SetSubMesh(sm, rest, MeshUpdateFlags.DontRecalculateBounds);
                copy.SetSubMesh(slot, floor, MeshUpdateFlags.DontRecalculateBounds); Same(copy, output);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
        static Mesh Replay(JObject initial, Mesh predecessor, string role, string owner)
        {
            bool render = role.EndsWith("-render", StringComparison.Ordinal);
            Need(role == (owner == N5 ? "native5-" : "native6-") + (render ? "render" : "collider"), "exact role and owner");
            var request = Request(initial);
            if (owner == N5)
            {
                int n = render ? 0 : 1; Need(predecessor == Asset(initial["source_assets"][n]), "exact blue terminal input");
                var recipe = Json((string)request["roof_recipe"])["rows"].Single(x => (string)x["role"] == role);
                var terminal = Asset(initial["outputs"][n]); Retired(predecessor, terminal, recipe["remove_triangle_ordinals"], 8); return terminal;
            }
            var rail = Json(Rail); var panel = Json(Panel); var rq = Request(rail); var pq = Request(panel);
            int index = render ? 0 : 1; var intermediate = Asset(rail["source_assets"][index]);
            if (render)
            {
                Need(predecessor == Asset(initial["source_assets"][2]) && intermediate == Asset(initial["outputs"][2]), "exact original floor split chain");
                Floor(predecessor, intermediate, Json((string)request["floor_recipe"]));
            }
            else Need(predecessor == intermediate, "exact original pane collision input");
            var afterRail = Asset(rail["outputs"][index]);
            Retired(intermediate, afterRail, Json((string)rq["recipe"])[render ? "retire_current_render_triangles" : "retire_current_collider_triangles"], 14);
            Need(afterRail == Asset(panel["source_assets"][index]), "exact rail to panel predecessor");
            var terminal6 = Asset(panel["outputs"][index]);
            Retired(afterRail, terminal6, Json((string)pq["recipe"])[render ? "retire_render_triangle_ids" : "retire_collider_triangle_ids"], 44);
            return terminal6;
        }
        public static Mesh Resolve(Mesh predecessor, string role, string owner)
        {
            Need(owner == N5 || owner == N6, "supported architectural owner"); var initial = Read(); if (initial == null) return predecessor;
            var terminal = Replay(initial, predecessor, role, owner);
            var current = Json(ConcourseFidelityCorrection.Registry); string key = owner == N5 ? "native5_after" : "native6_after";
            var fields = Platform6BanksB24PlacementReplay.CarrierFields(Platform6BankB1Replay.Find(owner));
            ReviewedFields(fields, owner);
            Need(JToken.DeepEquals(fields, current[key]), "exact current serialized carrier");
            var reference = current[key][role.EndsWith("-render", StringComparison.Ordinal) ? "filter" : "collider"];
            Need((string)reference.First.First["m_Mesh"]["guid"] == AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(terminal)), "exact current mesh reference");
            return terminal;
        }
        public static JObject ReviewedFields(JObject actual, string owner)
        {
            if (owner != N5 && owner != N6) return actual; var initial = Read(); if (initial == null) return actual;
            var current = Json(ConcourseFidelityCorrection.Registry); string prefix = owner == N5 ? "native5" : "native6";
            Need(JToken.DeepEquals(actual, current[prefix + "_after"]), "current carrier fields differ");
            var expected = (JObject)initial[prefix + "_before"].DeepClone();
            var frozenAfter = owner == N5 ? initial["native5_after"] : Json(Panel)["native6_after"];
            Need(JToken.DeepEquals(actual, frozenAfter), "exact immutable final architecture carrier");
            foreach (string key in new[] { "filter", "collider" })
            {
                string wrapper = key == "filter" ? "MeshFilter" : "MeshCollider";
                expected[key][wrapper]["m_Mesh"] = actual[key][wrapper]["m_Mesh"].DeepClone();
            }
            if (owner == N6) expected["renderer"]["MeshRenderer"]["m_Materials"] = initial["native6_after"]["renderer"]["MeshRenderer"]["m_Materials"].DeepClone();
            Need(JToken.DeepEquals(expected, actual), "only reviewed mesh references and appended floor material may change");
            return (JObject)initial[prefix + "_before"].DeepClone();
        }
    }
}
#endif
