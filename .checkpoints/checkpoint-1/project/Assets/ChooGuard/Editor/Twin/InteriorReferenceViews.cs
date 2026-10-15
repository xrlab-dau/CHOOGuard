#if UNITY_EDITOR
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Render the production scene from retained video camera poses; poses inherit source-model registration uncertainty.</summary>
    public static class InteriorReferenceViews
    {
        static Vector3 V(JToken row) => new Vector3((float)row[0], (float)row[1], (float)row[2]);
        public static string CaptureGround(string frame, string label)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Reference capture requires edit mode");
            var cameras = JObject.Parse(File.ReadAllText(".planning/2026-09-23-video-twin/metric-registration/registered-cameras.json"));
            var pose = cameras[frame + ".jpg"] ?? throw new InvalidDataException("No observed camera pose for " + frame);
            string directory = ".planning/2026-10-08-interior-runtime-completion/architecture/reference-views";
            Directory.CreateDirectory(directory);
            // Use the editor-owned camera. Constructing even a DontSave camera in the
            // production scene can mark the scene dirty and interrupt test/development flows.
            var camera = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
            if (camera == null) throw new InvalidOperationException("A Scene view is required for read-only reference capture");
            var position = camera.transform.position; var rotation = camera.transform.rotation;
            float fov = camera.fieldOfView, aspect = camera.aspect, near = camera.nearClipPlane, far = camera.farClipPlane;
            bool hdr = camera.allowHDR, msaa = camera.allowMSAA;
            bool orthographic = camera.orthographic;
            var projection = camera.projectionMatrix; var worldToCamera = camera.worldToCameraMatrix; var culling = camera.cullingMatrix;
            var flags = camera.clearFlags; var background = camera.backgroundColor; var target = camera.targetTexture;
            var texture = RenderTexture.GetTemporary(960, 540, 24);
            var previous = RenderTexture.active; Texture2D pixels = null;
            try
            {
                camera.transform.SetPositionAndRotation(V(pose["position"]), Quaternion.LookRotation(V(pose["forward"]), V(pose["up"])));
                camera.fieldOfView = (float)pose["verticalFov"]; camera.aspect = 960f / 540f;
                camera.nearClipPlane = .04f; camera.farClipPlane = 1000; camera.allowHDR = false; camera.allowMSAA = false;
                camera.orthographic = false; camera.ResetWorldToCameraMatrix(); camera.ResetCullingMatrix();
                camera.projectionMatrix = Matrix4x4.Perspective(camera.fieldOfView, camera.aspect, camera.nearClipPlane, camera.farClipPlane);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .025f, .025f);
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                pixels = new Texture2D(960, 540, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply();
                string output = directory + "/" + frame + "-" + label + ".png"; File.WriteAllBytes(output, pixels.EncodeToPNG());
                File.WriteAllText(output + ".json", new JObject { ["frame"] = frame, ["label"] = label, ["camera"] = pose.DeepClone(),
                    ["source"] = "https://www.youtube.com/watch?v=3TLdCQCjeXE", ["coordinate_convention"] = "Retained registered camera is already Unity world ENU[x,z,y]; no additional station-root rotation",
                    ["site_accuracy_verified"] = false, ["lens_distortion"] = "Unity view is pinhole; raw source has retained k1. Use calibrated projection comparison for pixel error, not this render alone.",
                    ["sampled_utc"] = DateTime.UtcNow.ToString("o") }.ToString());
                return output;
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = target;
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.fieldOfView = fov; camera.aspect = aspect; camera.nearClipPlane = near; camera.farClipPlane = far;
                camera.allowHDR = hdr; camera.allowMSAA = msaa; camera.clearFlags = flags; camera.backgroundColor = background;
                camera.orthographic = orthographic; camera.worldToCameraMatrix = worldToCamera;
                camera.projectionMatrix = projection; camera.cullingMatrix = culling;
                RenderTexture.ReleaseTemporary(texture); if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
#endif
