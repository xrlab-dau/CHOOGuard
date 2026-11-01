#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor.Twin
{
    /// <summary>
    /// Headless proof shots of placed equipment. Opens the station scene read-only, frames each named object with a
    /// temporary camera from three distances and writes PNGs plus a small manifest. The scene is never saved.
    /// Run: Unity -batchmode -projectPath . -executeMethod ChooGuard.Editor.Twin.EquipmentSnapshot.Capture
    ///      [-cgSnapshotOutput dir] [-cgSnapshotTargets name1,name2]
    /// </summary>
    public static class EquipmentSnapshot
    {
        const string ScenePath = "Assets/ChooGuard/Scenes/FpsStation.unity";
        const int Width = 1600, Height = 900;
        static readonly string[] DefaultTargets =
        {
            "MAIN2F_STATION_OFFICE_WHEELCHAIR_20261023",
            "MAIN2F_RAILWAY_POLICE_AED_20261025",
        };

        public static void Capture()
        {
            string output = Arg("-cgSnapshotOutput") ?? ".planning/2026-10-30-cardiac-arrest-solo-scenario/snapshots";
            string targetsArg = Arg("-cgSnapshotTargets");
            var targets = string.IsNullOrEmpty(targetsArg) ? DefaultTargets : targetsArg.Split(',');
            int code;
            try { int found = CaptureTargets(targets, output); code = found == targets.Length ? 0 : 1; }
            catch (Exception e) { Debug.LogError("[EquipmentSnapshot] " + e); code = 2; }
            EditorApplication.Exit(code);
        }

        /// <summary>Returns how many of the named objects were found and shot.</summary>
        public static int CaptureTargets(IEnumerable<string> names, string output)
        {
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var go = new GameObject("EquipmentSnapshotCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox; cam.fieldOfView = 50f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32); rt.Create();
            var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var entries = new List<string>();
            int found = 0;
            try
            {
                foreach (var raw in names)
                {
                    string name = raw.Trim();
                    var target = FindInScene(scene, name);
                    if (target == null)
                    {
                        Debug.LogWarning("[EquipmentSnapshot] not found in scene: " + name);
                        entries.Add("{\"object\":\"" + name + "\",\"found\":false}");
                        continue;
                    }
                    found++;
                    var bounds = BoundsOf(target);
                    float radius = Mathf.Max(0.6f, bounds.extents.magnitude);
                    var shots = new List<string>();
                    Physics.SyncTransforms();
                    foreach (var (label, mult, pitch) in new[] { ("close", 2.6f, 16f), ("context", 6f, 20f), ("wide", 10f, 38f) })
                    {
                        if (!ClearPose(target, bounds, radius * mult, pitch, out var pose, out float yaw, out float dist))
                        {
                            Debug.LogWarning("[EquipmentSnapshot] " + name + ": no unobstructed view for " + label);
                            continue;
                        }
                        cam.transform.position = pose;
                        cam.transform.LookAt(bounds.center);
                        cam.targetTexture = rt; RenderTexture.active = rt;
                        cam.Render();
                        pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply();
                        string file = Path.Combine(output, name + "-" + label + ".png");
                        File.WriteAllBytes(file, pixels.EncodeToPNG());
                        shots.Add("{\"file\":\"" + Path.GetFileName(file) + "\",\"yaw\":" + F(yaw) + ",\"distance\":" + F(dist) + "}");
                        Debug.Log("[EquipmentSnapshot] wrote " + file + " yaw=" + yaw + " distance=" + dist.ToString("0.00"));
                    }
                    var c = bounds.center; var s = bounds.size;
                    entries.Add("{\"object\":\"" + name + "\",\"found\":true,\"active\":" + (target.gameObject.activeInHierarchy ? "true" : "false")
                        + ",\"path\":\"" + HierarchyPath(target) + "\""
                        + ",\"center\":[" + F(c.x) + "," + F(c.y) + "," + F(c.z) + "],\"size\":[" + F(s.x) + "," + F(s.y) + "," + F(s.z) + "]"
                        + ",\"renderers\":" + target.GetComponentsInChildren<Renderer>(true).Length
                        + ",\"shots\":[" + string.Join(",", shots) + "]}");
                }
            }
            finally
            {
                RenderTexture.active = previous; cam.targetTexture = null;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(go);
                File.WriteAllText(Path.Combine(output, "manifest.json"), "[\n" + string.Join(",\n", entries) + "\n]\n");
            }
            return found;
        }

        static readonly float[] Yaws = { -35f, 35f, -80f, 80f, -125f, 125f, 180f, 0f, -55f, 55f, -155f, 155f };
        static readonly float[] Shrink = { 1f, 0.7f, 0.5f };

        /// <summary>
        /// A camera position that actually sees the target: not inside geometry, and the first collider on the line to the
        /// target is the target itself (or nothing). Wall-mounted equipment only has one open side, so the yaw is searched;
        /// nearer distances are tried before giving up.
        /// </summary>
        static bool ClearPose(Transform target, Bounds bounds, float distance, float pitch, out Vector3 pose, out float yaw, out float dist)
        {
            foreach (var shrink in Shrink)
            {
                dist = Mathf.Max(0.5f, distance * shrink);
                foreach (var y in Yaws)
                {
                    var dir = Quaternion.Euler(pitch, y, 0f) * Vector3.back;
                    var candidate = bounds.center - dir * dist;
                    if (Physics.CheckSphere(candidate, 0.15f)) continue;
                    if (Physics.Linecast(candidate, bounds.center, out var hit) && !hit.transform.IsChildOf(target) && !bounds.Contains(hit.point)) continue;
                    pose = candidate; yaw = y; return true;
                }
            }
            pose = default; yaw = 0f; dist = 0f; return false;
        }

        static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        static Transform FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            return null;
        }

        static string HierarchyPath(Transform t)
        {
            var parts = new List<string>();
            for (var cur = t; cur != null; cur = cur.parent) parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static Bounds BoundsOf(Transform target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(target.position, Vector3.one * 0.6f);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        static string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (string.Equals(args[i], key, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }
    }
}
#endif
