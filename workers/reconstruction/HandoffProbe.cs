// Copy only into Assets/Editor of an isolated probe project, never the game scene.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class HandoffProbe
{
    [Serializable]
    public sealed class Expected
    {
        public string fbxSha256;
        public string sourceKind;
        public float[] worldVertices;
        public int triangles;
        public bool requireColors;
    }

    [Serializable]
    public sealed class Report
    {
        public string status, unityVersion, fbxSha256, sourceKind;
        public float globalScale, maxVertexErrorMetres;
        public bool useFileScale, bakeAxisConversion, colorsPresent, normalsPresent;
        public int vertices, triangles;
        public Vector3 boundsMin, boundsMax;
        public Vector3[] worldVertices;
        public string scope = "Isolated analytic mesh import only; not a CHOOGuard player build or building-accuracy acceptance";
    }

    public static void Run()
    {
        var expected = JsonUtility.FromJson<Expected>(File.ReadAllText("probe_expected.json"));
        if (expected.sourceKind != "test-fixture" || expected.worldVertices == null ||
            expected.worldVertices.Length == 0 || expected.worldVertices.Length % 3 != 0)
            throw new InvalidOperationException("Probe requires explicit analytic fixture coordinates");
        const string assetPath = "Assets/Prepared.fbx";
        string hash;
        using (var sha = SHA256.Create())
        using (var input = File.OpenRead(assetPath))
            hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        if (hash != expected.fbxSha256)
            throw new InvalidOperationException("FBX hash differs from probe expectation");
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("No FBX ModelImporter");
        importer.globalScale = 1;
        importer.useFileScale = true;
        importer.isReadable = true;
        importer.bakeAxisConversion = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        try
        {
            var vertices = new List<Vector3>();
            int triangles = 0;
            bool colors = true, normals = true;
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                foreach (var point in mesh.vertices) vertices.Add(filter.transform.TransformPoint(point));
                triangles += mesh.triangles.Length / 3;
                colors &= mesh.colors32.Length == mesh.vertexCount;
                normals &= mesh.normals.Length == mesh.vertexCount;
            }
            if (vertices.Count == 0) throw new InvalidOperationException("Imported mesh is empty");
            var reference = new List<Vector3>();
            for (int i = 0; i < expected.worldVertices.Length; i += 3)
                reference.Add(new Vector3(expected.worldVertices[i], expected.worldVertices[i + 1], expected.worldVertices[i + 2]));
            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var point in vertices) bounds.Encapsulate(point);
            float error = Mathf.Max(DirectedError(vertices, reference), DirectedError(reference, vertices));
            bool passed = error <= .001f && triangles == expected.triangles && normals && (!expected.requireColors || colors);
            var report = new Report {
                status = passed ? "fixture_import_passed" : "fixture_import_failed",
                unityVersion = Application.unityVersion, fbxSha256 = hash, sourceKind = expected.sourceKind,
                globalScale = importer.globalScale, useFileScale = importer.useFileScale,
                bakeAxisConversion = importer.bakeAxisConversion, maxVertexErrorMetres = error,
                vertices = vertices.Count, triangles = triangles, colorsPresent = colors, normalsPresent = normals,
                boundsMin = bounds.min, boundsMax = bounds.max, worldVertices = vertices.ToArray()
            };
            File.WriteAllText("probe_result.json", JsonUtility.ToJson(report, true));
            if (!passed) throw new InvalidOperationException("FBX fixture import differs from expected geometry; see probe_result.json");
            Debug.Log("HANDOFF_PROBE_PASSED: geometry, normals and required colors imported");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static float DirectedError(List<Vector3> from, List<Vector3> to)
    {
        float maximum = 0;
        foreach (var point in from)
        {
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))
                return float.PositiveInfinity;
            float nearest = float.PositiveInfinity;
            foreach (var other in to) nearest = Mathf.Min(nearest, Vector3.Distance(point, other));
            maximum = Mathf.Max(maximum, nearest);
        }
        return maximum;
    }
}
#endif
