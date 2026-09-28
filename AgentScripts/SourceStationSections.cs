// Read-only horizontal cuts of the original station meshes. No semantic room inference.
// args: [outputJson]
using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public static class SourceStationSections
{
    public static void Main(string[] args)
    {
        string path = args[0];
        var root = GameObject.Find("FPSWorld/공식 자료 부산역 역사/MainShell");
        if (root == null) throw new InvalidOperationException("Original MainShell is absent.");
        float[] heights = { 1.5f, 8.5f, 12.5f };
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var stream = File.CreateText(path))
        using (var writer = new JsonTextWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("basis"); writer.WriteValue("Original MainShell mesh/plane intersections; not surveyed as-built interior walls.");
            writer.WritePropertyName("worldCutHeights"); writer.WriteStartArray();
            foreach (float height in heights) writer.WriteValue(height);
            writer.WriteEndArray();
            writer.WritePropertyName("format"); writer.WriteValue("[cutIndex, worldX1, worldZ1, worldX2, worldZ2]");
            writer.WritePropertyName("segments"); writer.WriteStartArray();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var vertices = mesh.vertices;
                var matrix = filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    int[] triangles = mesh.GetTriangles(sub);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                        for (int cut = 0; cut < heights.Length; cut++)
                        {
                            int count = 0;
                            Vector3 first = default, second = default;
                            Intersect(a, b, heights[cut], ref count, ref first, ref second);
                            Intersect(b, c, heights[cut], ref count, ref first, ref second);
                            Intersect(c, a, heights[cut], ref count, ref first, ref second);
                            if (count != 2 || (first - second).sqrMagnitude < .000001f) continue;
                            writer.WriteStartArray(); writer.WriteValue(cut);
                            writer.WriteValue(first.x); writer.WriteValue(first.z);
                            writer.WriteValue(second.x); writer.WriteValue(second.z); writer.WriteEndArray();
                        }
                    }
                }
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        Debug.Log("SOURCE_SECTIONS " + path);
    }

    static void Intersect(Vector3 a, Vector3 b, float height, ref int count, ref Vector3 first, ref Vector3 second)
    {
        // Half-open edge intervals count a shared vertex once and skip coplanar faces.
        if ((a.y <= height && b.y > height) || (b.y <= height && a.y > height))
        {
            Vector3 point = Vector3.LerpUnclamped(a, b, (height - a.y) / (b.y - a.y));
            if (count == 0) first = point; else second = point;
            count++;
        }
    }
}
