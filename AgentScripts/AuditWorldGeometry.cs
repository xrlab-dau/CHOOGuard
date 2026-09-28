// Read-only scene geometry snapshot. Existence, activity, rendering and collision are separate facts.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AuditWorldGeometry
{
    public static string Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Expected audit output directory.");
        string output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);
        var scene = SceneManager.GetActiveScene();
        var meshes = new JArray();
        var renderers = new JArray();
        var colliders = new JArray();
        var meshIds = new Dictionary<Mesh, int>();
        var errors = new JArray();
        int Register(Mesh mesh)
        {
            if (!mesh) return -1;
            if (meshIds.TryGetValue(mesh, out int known)) return known;
            int id = meshIds.Count;
            meshIds.Add(mesh, id);
            var item = new JObject { ["id"] = id, ["name"] = mesh.name,
                ["asset"] = AssetDatabase.GetAssetPath(mesh), ["readable"] = mesh.isReadable };
            meshes.Add(item);
            try
            {
                var positions = mesh.vertices;
                var indices = mesh.triangles;
                string file = "mesh-" + id.ToString("D4") + ".bin";
                string path = Path.Combine(output, file);
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(new byte[] { 87, 71, 69, 79 });
                    writer.Write(positions.Length); writer.Write(indices.Length);
                    foreach (var position in positions)
                    { writer.Write(position.x); writer.Write(position.y); writer.Write(position.z); }
                    foreach (int index in indices) writer.Write(index);
                }
                item["file"] = file; item["vertices"] = positions.Length; item["triangles"] = indices.Length / 3;
                using var source = File.OpenRead(path);
                using var sha = SHA256.Create();
                item["sha256"] = BitConverter.ToString(sha.ComputeHash(source)).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception error)
            {
                item["error"] = error.Message;
                errors.Add(new JObject { ["mesh"] = id, ["message"] = error.Message });
            }
            return id;
        }
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var item = Identity(renderer.transform);
                item["type"] = renderer.GetType().Name;
                item["enabled"] = renderer.enabled;
                item["forceRenderingOff"] = renderer.forceRenderingOff;
                item["boundsMin"] = Vector(renderer.bounds.min); item["boundsMax"] = Vector(renderer.bounds.max);
                item["matrix"] = Matrix(renderer.localToWorldMatrix);
                var materials = new JArray();
                foreach (var material in renderer.sharedMaterials)
                    materials.Add(material ? AssetDatabase.GetAssetPath(material) : "");
                item["materials"] = materials;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter) item["mesh"] = Register(filter.sharedMesh);
                else if (renderer is SkinnedMeshRenderer skin)
                {
                    // Bind geometry is not treated as the current animated surface.
                    item["bindMesh"] = Register(skin.sharedMesh);
                    item["geometryStatus"] = "animated surface; bounds only, excluded from architectural cuts";
                }
                else item["geometryStatus"] = "no static mesh; bounds only";
                renderers.Add(item);
            }
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                var item = Identity(collider.transform);
                item["type"] = collider.GetType().Name;
                item["enabled"] = collider.enabled; item["trigger"] = collider.isTrigger;
                item["matrix"] = Matrix(collider.transform.localToWorldMatrix);
                if (collider is MeshCollider meshCollider)
                { item["mesh"] = Register(meshCollider.sharedMesh); item["convex"] = meshCollider.convex; }
                else if (collider is BoxCollider box)
                { item["center"] = Vector(box.center); item["size"] = Vector(box.size); }
                else if (collider is CapsuleCollider capsule)
                { item["center"] = Vector(capsule.center); item["radius"] = capsule.radius; item["height"] = capsule.height; item["direction"] = capsule.direction; }
                else if (collider is SphereCollider sphere)
                { item["center"] = Vector(sphere.center); item["radius"] = sphere.radius; }
                else if (collider is CharacterController character)
                { item["center"] = Vector(character.center); item["radius"] = character.radius; item["height"] = character.height; }
                else item["geometryStatus"] = "unsupported collider shape; do not infer empty space";
                colliders.Add(item);
            }
        }
        var result = new JObject {
            ["schema"] = "chooguard.world-geometry-audit.v1", ["utc"] = DateTime.UtcNow.ToString("O"),
            ["scene"] = scene.path, ["sceneDirty"] = scene.isDirty, ["playMode"] = EditorApplication.isPlaying,
            ["stationAngleDegrees"] = 16.2, ["matrixFormat"] = "row-major local-to-world 4x4",
            ["meshFormat"] = "WGEO, int32 vertexCount, int32 indexCount, float32 xyz, int32 indices; little endian",
            ["classificationRule"] = "A present object is not proof of a finished, navigable or evidence-accurate region. Inactive geometry is not implemented runtime coverage.",
            ["meshes"] = meshes, ["renderers"] = renderers, ["colliders"] = colliders, ["errors"] = errors
        };
        File.WriteAllText(Path.Combine(output, "world-geometry.json"), result.ToString());
        return new JObject { ["meshes"] = meshes.Count, ["renderers"] = renderers.Count,
            ["colliders"] = colliders.Count, ["errors"] = errors.Count, ["sceneModified"] = false }.ToString();
    }

    // Sample current collision, including the Y=0 floor that older upper-hall probes skipped.
    // This is clearance evidence, not a claim of a completed room or a Play-mode route test.
    public static string GroundContinuity(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Expected output JSON path.");
        var player = GameObject.Find("KORAIL 역무원");
        var controller = player ? player.GetComponent<CharacterController>() : null;
        if (!controller) throw new InvalidOperationException("Actual player controller is absent.");
        float radius = controller.radius * Mathf.Max(player.transform.lossyScale.x, player.transform.lossyScale.z);
        float height = controller.height * player.transform.lossyScale.y;
        float sine = Mathf.Sin(16.2f * Mathf.Deg2Rad), cosine = Mathf.Cos(16.2f * Mathf.Deg2Rad);
        var hits = new RaycastHit[32];
        var overlaps = new Collider[64];
        var owners = new JObject();
        var cells = new JArray();
        int supported = 0, clear = 0, saturated = 0;
        int Owner(Collider collider)
        {
            int id = collider.GetInstanceID();
            string key = id.ToString();
            if (owners[key] == null) owners[key] = Identity(collider.transform);
            return id;
        }
        bool Nearest(Vector3 origin, Vector3 direction, float distance, bool floor, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) saturated++;
            nearest = default;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == controller || (floor && Mathf.Abs(hit.normal.y) < .7072f)) continue;
                if (hit.distance < closest) { closest = hit.distance; nearest = hit; }
            }
            return !float.IsPositiveInfinity(closest);
        }
        Physics.SyncTransforms();
        for (int u = -95; u <= 85; u++)
        for (int v = -50; v <= 15; v++)
        {
            var point = new Vector3(u * sine + v * cosine, .35f, u * cosine - v * sine);
            if (!Nearest(point, Vector3.down, .8f, true, out var floor))
            {
                cells.Add(new JArray(u, v, null, null, null, null, null));
                continue;
            }
            supported++;
            var feet = floor.point + Vector3.up * .045f;
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * radius,
                feet + Vector3.up * (height - radius), radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            int blocker = 0;
            if (count == overlaps.Length) { saturated++; blocker = -1; }
            for (int i = 0; i < count; i++)
                if (overlaps[i] != controller) { blocker = Owner(overlaps[i]); break; }
            if (blocker == 0) clear++;
            bool overhead = Nearest(floor.point + Vector3.up * .2f, Vector3.up, 14f, false, out var ceiling);
            cells.Add(new JArray(u, v, floor.point.y, Owner(floor.collider), blocker,
                overhead ? (JToken)ceiling.point.y : JValue.CreateNull(),
                overhead ? (JToken)Owner(ceiling.collider) : JValue.CreateNull()));
        }
        var result = new JObject {
            ["basis"] = "Read-only live Physics queries; Y=0 included; actual player capsule dimensions.",
            ["cellFormat"] = "[u,v,floorY,floorColliderId,blockerColliderId,ceilingY,ceilingColliderId]",
            ["blockerZero"] = "clear sampled capsule; -1 means saturated overlap query",
            ["gridStepModelUnits"] = 1, ["capsuleRadius"] = radius, ["capsuleHeight"] = height,
            ["queriesHitBackfaces"] = Physics.queriesHitBackfaces, ["samples"] = cells.Count,
            ["supported"] = supported, ["clear"] = clear, ["saturatedQueries"] = saturated,
            ["limits"] = "Discrete clearance samples, not an exact occupancy boundary or proof of movement connectivity. No upward hit does not prove no visible ceiling.",
            ["owners"] = owners, ["cells"] = cells
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
        File.WriteAllText(args[0], result.ToString());
        return new JObject { ["samples"] = cells.Count, ["supported"] = supported, ["clear"] = clear,
            ["saturatedQueries"] = saturated, ["sceneModified"] = false }.ToString();
    }

    static JObject Identity(Transform transform)
    {
        string path = transform.name;
        for (var parent = transform.parent; parent; parent = parent.parent) path = parent.name + "/" + path;
        return new JObject { ["path"] = path, ["instanceId"] = transform.gameObject.GetInstanceID(),
            ["activeSelf"] = transform.gameObject.activeSelf, ["activeHierarchy"] = transform.gameObject.activeInHierarchy,
            ["layer"] = transform.gameObject.layer };
    }
    static JArray Vector(Vector3 value) => new JArray(value.x, value.y, value.z);
    static JArray Matrix(Matrix4x4 value)
    {
        var result = new JArray();
        for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++) result.Add(value[row, column]);
        return result;
    }
}
