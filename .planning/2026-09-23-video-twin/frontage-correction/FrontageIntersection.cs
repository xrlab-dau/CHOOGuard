using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class FrontageIntersection
{
    static float[] V(Vector3 p) { return new[] { p.x, p.y, p.z }; }
    static string PathOf(Transform t) { return t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name; }
    static Transform station;
    static Vector3 World(float u, float v, float y) { return station.TransformPoint(new Vector3(v, y, u)); }
    static float[] UVY(Vector3 p) { var q = station.InverseTransformPoint(p); return new[] { q.z, q.x, q.y }; }
    static bool Hit(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float distance)
    {
        distance = 0; Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(direction, e2);
        float det = Vector3.Dot(e1, p); if (Mathf.Abs(det) < .0000001f) return false;
        float inv = 1 / det; Vector3 t = origin - a; float u = Vector3.Dot(t, p) * inv;
        if (u < -.000001f || u > 1.000001f) return false;
        Vector3 q = Vector3.Cross(t, e1); float v = Vector3.Dot(direction, q) * inv;
        if (v < -.000001f || u + v > 1.000001f) return false;
        distance = Vector3.Dot(e2, q) * inv; return distance > .0001f;
    }
    sealed class Surface
    {
        public MeshFilter filter; public Renderer renderer; public Mesh mesh; public Vector3[] vertices; public int[][] triangles;
    }
    public static void Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Read-only edit-mode query only.");
        station = GameObject.Find("맞이방 · 원본 정합").transform;
        var surfaces = new List<Surface>();
        foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var renderer = mf.GetComponent<Renderer>(); var mesh = mf.sharedMesh;
            if (renderer == null || !renderer.enabled || mesh == null) continue;
            // Read-only MeshData avoids changing model importer read/write flags.
            using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var d = data[0]; var native = new Unity.Collections.NativeArray<Vector3>(d.vertexCount, Unity.Collections.Allocator.Temp);
                d.GetVertices(native); var verts = native.ToArray(); native.Dispose();
                for (int k = 0; k < verts.Length; k++) verts[k] = mf.transform.TransformPoint(verts[k]);
                var indices = new int[d.subMeshCount][];
                for (int s = 0; s < d.subMeshCount; s++)
                {
                    var sub = d.GetSubMesh(s); if (sub.topology != MeshTopology.Triangles) { indices[s] = new int[0]; continue; }
                    var n = new Unity.Collections.NativeArray<int>(sub.indexCount, Unity.Collections.Allocator.Temp);
                    d.GetIndices(n, s); indices[s] = n.ToArray(); n.Dispose();
                }
                surfaces.Add(new Surface { filter = mf, renderer = renderer, mesh = mesh, vertices = verts, triangles = indices });
            }
        }
        var requests = new List<Tuple<string, Vector3, Vector3>>();
        var units = new[] { new { key = "kkotdeul", u = 19.05f, v = -47.45f, currentU = 15.84f, currentV = -47.45f, frontV = -45.54f, normalU = -1f }, new { key = "boksoondoga", u = 2.9f, v = -47.95f, currentU = 6.26f, currentV = -47.95f, frontV = -46.54f, normalU = 1f } };
        foreach (var unit in units)
        {
            var target = World(unit.currentU, unit.currentV, 10.25f);
            var origin = World(unit.currentU + unit.normalU * 2.4f, unit.currentV, 10.25f);
            requests.Add(Tuple.Create(unit.key + "/current/sign-height", origin, target));
            requests.Add(Tuple.Create(unit.key + "/current/sign-height-reverse", target, origin));
            foreach (var route in new[] { new Vector2(11f, -28f), new Vector2(11.4f, -36f), new Vector2(11f, -45f), new Vector2(11f, -58f), new Vector2(8f, -36f) })
            {
                var eye = World(route.x, route.y, 8.65f);
                string label = unit.key + "/route-" + route.x + "," + route.y;
                requests.Add(Tuple.Create(label + "/current", eye, target));
                requests.Add(Tuple.Create(label + "/candidate+v", eye, World(unit.u, unit.frontV, 10.25f)));
                requests.Add(Tuple.Create(label + "/candidate+v-opening", eye, World(unit.u, unit.frontV, 8.65f)));
            }
            requests.Add(Tuple.Create(unit.key + "/candidate+v/sign-height", World(unit.u, unit.frontV + 2.4f, 10.25f), World(unit.u, unit.frontV, 10.25f)));
            requests.Add(Tuple.Create(unit.key + "/candidate+v/eye-height", World(unit.u, unit.frontV + 2.4f, 8.65f), World(unit.u, unit.frontV, 10.25f)));
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2)
            {
                requests.Add(Tuple.Create(unit.key + "/current/corner-" + x + "," + y, origin, World(unit.currentU, unit.currentV + x * 1.24f, 10.25f + y * .31f)));
                requests.Add(Tuple.Create(unit.key + "/candidate+v/corner-" + x + "," + y, World(unit.u, unit.frontV + 2.4f, 8.65f), World(unit.u + x * 1.24f, unit.frontV, 10.25f + y * .31f)));
            }
        }
        var rows = new List<object>();
        foreach (var request in requests)
        {
            Vector3 origin = request.Item2, target = request.Item3, dir = (target - origin).normalized; float len = Vector3.Distance(origin, target);
            var ray = new Ray(origin, dir); var hits = new List<JObject>();
            foreach (var surface in surfaces)
            {
                float boundT; if (!surface.renderer.bounds.IntersectRay(ray, out boundT) || boundT > len + .001f) continue;
                Material[] materials = surface.renderer.sharedMaterials;
                for (int s = 0; s < surface.triangles.Length; s++)
                {
                    var tris = surface.triangles[s]; Material mat = s < materials.Length ? materials[s] : null;
                    for (int i = 0; i + 2 < tris.Length; i += 3)
                    {
                        Vector3 a = surface.vertices[tris[i]], b = surface.vertices[tris[i + 1]], c = surface.vertices[tris[i + 2]];
                        float t; if (!Hit(origin, dir, a, b, c, out t) || t > len + .001f) continue;
                        Vector3 point = origin + dir * t; var normal = Vector3.Cross(b - a, c - a).normalized;
                        hits.Add(JObject.FromObject(new { path = PathOf(surface.filter.transform), instanceId = surface.filter.gameObject.GetInstanceID(), mesh = AssetDatabase.GetAssetPath(surface.mesh), submesh = s, triangle = i / 3, distance = t, pointWorld = V(point), pointUVY = UVY(point), triangleWorld = new[] { V(a), V(b), V(c) }, triangleUVY = new[] { UVY(a), UVY(b), UVY(c) }, normalWorld = V(normal), frontFacing = Vector3.Dot(normal, dir) < 0, material = mat == null ? null : mat.name, shader = mat == null ? null : mat.shader.name, surface = mat != null && mat.HasProperty("_Surface") ? mat.GetFloat("_Surface") : -1f, renderQueue = mat == null ? -1 : mat.renderQueue, cull = mat != null && mat.HasProperty("_Cull") ? mat.GetFloat("_Cull") : -1f, colliders = surface.filter.GetComponents<Collider>().Select(q => new { type = q.GetType().Name, q.enabled, q.isTrigger }).ToArray() }));
                    }
                }
            }
            var colliders = Physics.RaycastAll(ray, len + .001f, ~0, QueryTriggerInteraction.Collide).OrderBy(q => q.distance).Select(q => new { path = PathOf(q.collider.transform), type = q.collider.GetType().Name, q.collider.enabled, q.collider.isTrigger, q.distance, q.triangleIndex, pointWorld = V(q.point), pointUVY = UVY(q.point) }).ToArray();
            rows.Add(new { request = request.Item1, originWorld = V(origin), originUVY = UVY(origin), targetWorld = V(target), targetUVY = UVY(target), length = len, meshHits = hits.OrderBy(q => (float)q["distance"]).ToArray(), colliderHits = colliders });
        }
        var hierarchy = station.GetComponentsInChildren<Transform>(true).Where(t => t.parent == station || t.name.Contains("kkotdeul") || t.name.Contains("boksoondoga")).Select(t => new { path = PathOf(t), localPosition = V(t.localPosition), localEuler = V(t.localEulerAngles), localScale = V(t.localScale), t.gameObject.activeSelf }).ToArray();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var result = new { schema = "chooguard.frontage-triangle-intersection.v1", measuredUtc = DateTime.UtcNow.ToString("o"), scene = scene.path, sceneDirty = scene.isDirty, physicsQueriesHitBackfaces = Physics.queriesHitBackfaces, rootPosition = V(station.position), rootEuler = V(station.eulerAngles), rootScale = V(station.lossyScale), interpretation = "Triangle queries are double-sided, enumerate visible enabled renderers independently of colliders, and are geometric intersections not final rendered occlusion. Route eye y8.65 is recorded feet y7.05 plus configured eye offset1.6, not a new walk. No transforms, imports, materials, scene, physics globals or SceneView were changed.", hierarchy, rows };
        string output = ".planning/2026-09-23-video-twin/frontage-correction/mesh-intersections.json";
        File.WriteAllText(output, JsonConvert.SerializeObject(result, Formatting.Indented));
        Debug.Log("FRONTAGE_INTERSECTIONS " + requests.Count + " rays; " + surfaces.Count + " mesh renderers; " + output);
    }
}
