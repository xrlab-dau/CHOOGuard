using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.Foundation.Multiplayer
{
    [Serializable] public sealed class WorldReviewShot
    {
        public string FileName, ViewId, Environment;
        public Vector3 Position, Target;
        public bool Orthographic, DoorOpen = true;
        public float OrthographicSize, FieldOfView = 60, Near = .05f, Far = 300;
    }

    /// <summary>Opt-in, dedicated native review scene. It never runs in a training scene.
    /// Camera coordinates are metres relative to the authored vehicle origin, never fitted to candidate bounds.</summary>
    public sealed class WorldReviewCapture : MonoBehaviour
    {
        public const string Unmeasured = "V05 intermediate door motion; V06 actual crossing; V08 manufacturer-detail completeness; V10 computed fire state; V11 continuous door/boarding/train motion; V12 LOD and crowd; GPU frame-time and Windows performance";
        public const int Width = 1920, Height = 1080;
        public ConnectedWorldRuntime World;
        public Camera View;
        public Light Sun;
        private readonly List<ShotReceipt> receipts = new List<ShotReceipt>();
        private string output;

        public static WorldReviewShot[] Shots()
        {
            WorldReviewShot S(string file, string id, Vector3 position, Vector3 target, string environment = "game-day", bool ortho = false, float size = 3, bool open = true)
                => new WorldReviewShot { FileName = file + ".png", ViewId = id, Position = position, Target = target,
                    Environment = environment, Orthographic = ortho, OrthographicSize = size, DoorOpen = open };
            return new[] {
                S("v01-front", "V01", new Vector3(32,1.6f,0), new Vector3(0,1.6f,0), "neutral", true, 3.5f),
                S("v01-front-quarter", "V01", new Vector3(30,9,-22), new Vector3(0,1.6f,0), "neutral"),
                S("v02-left", "V02", new Vector3(0,1.6f,-36), new Vector3(0,1.6f,0), "neutral", true, 13),
                S("v02-right", "V02", new Vector3(0,1.6f,36), new Vector3(0,1.6f,0), "neutral", true, 13),
                S("v03-rear-quarter", "V03", new Vector3(-30,7,-22), new Vector3(0,1.4f,0), "neutral"),
                S("v03-underbody", "V03", new Vector3(12,.7f,-7), new Vector3(12,.15f,0)),
                S("v04-platform", "V04", new Vector3(4,1.65f,-7), new Vector3(0,1.65f,-2.5f)),
                S("v05-door-closed-outside", "V05", new Vector3(0,1.65f,-6.5f), new Vector3(0,1.65f,-2.5f), open:false),
                S("v05-door-open-outside", "V05", new Vector3(0,1.65f,-6.5f), new Vector3(0,1.65f,-2.5f)),
                S("v05-door-closed-inside", "V05", new Vector3(0,1.65f,.8f), new Vector3(0,1.65f,-2.5f), open:false),
                S("v05-door-open-inside", "V05", new Vector3(0,1.65f,.8f), new Vector3(0,1.65f,-2.5f)),
                S("v06-threshold-top", "V06", new Vector3(.7f,2.3f,-3.3f), new Vector3(0,0,-2.5f)),
                S("v06-threshold-side", "V06", new Vector3(2.3f,.35f,-3.2f), new Vector3(0,.1f,-2.5f)),
                S("v07-aisle-forward", "V07", new Vector3(-16,1.65f,-.5f), new Vector3(16,1.65f,-.5f)),
                S("v07-aisle-back", "V07", new Vector3(16,1.65f,-.5f), new Vector3(-16,1.65f,-.5f)),
                S("v07-entry", "V07", new Vector3(0,1.65f,-3.8f), new Vector3(0,1.65f,2)),
                S("v08-seat-window", "V08", new Vector3(3,1.65f,-.7f), new Vector3(3,1.4f,2.3f)),
                S("v08-floor-joint", "V08", new Vector3(2,1.1f,.1f), new Vector3(2,.05f,2.3f)),
                S("v09-low-light", "V09", new Vector3(-16,1.65f,-.5f), new Vector3(16,1.65f,-.5f), "low-light"),
                S("v09-glass-opposite", "V09", new Vector3(3,1.65f,5.5f), new Vector3(3,1.65f,1), "game-day") };
        }

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); var at = Array.IndexOf(args, "--cg-review-captures");
            if (at < 0 || at + 1 >= args.Length) yield break;
            output = Path.GetFullPath(args[at + 1]);
            if (Directory.Exists(output) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            { Debug.LogError("Review requires a fresh output directory and a native graphics device."); Application.Quit(2); yield break; }
            Directory.CreateDirectory(output);
            Application.runInBackground = true; Application.targetFrameRate = 60;
            yield return World.Prepare(true);
            var origin = World.Definition.Region("rolling_stock_mainline").Center;
            var offset = new Vector3(origin.X, origin.Y, origin.Z);
            foreach (var shot in Shots())
            {
                Configure(shot, offset);
                yield return new WaitForSecondsRealtime(.25f);
                yield return new WaitForEndOfFrame();
                Capture(shot);
            }
            File.WriteAllText(Path.Combine(output,"capture-complete.json"), JsonUtility.ToJson(new CaptureReceipt {
                Method = "Native Unity realtime camera to1920x1080 render texture; original lossless PNG; no offline render, retouch, upscaling or image attachment",
                Width = Width, Height = Height, WorldProfile = World.Definition.ProfileId, MetresPerUnit = 1,
                Device = SystemInfo.graphicsDeviceName, GraphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                ColorSpace = QualitySettings.activeColorSpace.ToString(), Quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
                AntiAliasing = QualitySettings.antiAliasing, Shadows = QualitySettings.shadows.ToString(),
                Exposure = "Built-in renderer; no postprocess or automatic exposure; fixed camera background and ambient/sun intensities",
                MissingEvidence = Unmeasured, Shots = receipts.ToArray() }, true)+"\n");
            Application.Quit();
        }

        private void Configure(WorldReviewShot shot, Vector3 offset)
        {
            foreach (var region in World.RegionViews)
                region.gameObject.SetActive(region.RegionId == "rolling_stock_mainline" ||
                    shot.Environment != "neutral" && region.RegionId == "rail_platforms_mainline");
            foreach (var barrier in UnityEngine.Object.FindObjectsByType<ConnectedPortalBarrier>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                barrier.SetOpen(shot.DoorOpen);
            foreach (var light in World.RegionViews.SelectMany(r => r.GetComponentsInChildren<Light>(true)))
                light.enabled = shot.Environment == "game-day";
            var low = shot.Environment == "low-light";
            Sun.intensity = low ? .12f : shot.Environment == "neutral" ? 1.0f : 1.1f;
            Sun.color = Color.white; Sun.transform.rotation = Quaternion.Euler(55,-35,0); Sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = low ? new Color(.055f,.06f,.07f) : shot.Environment == "neutral" ? new Color(.5f,.5f,.5f) : new Color(.32f,.35f,.4f);
            RenderSettings.fog = false;
            View.clearFlags = CameraClearFlags.SolidColor; View.backgroundColor = new Color(.18f,.2f,.23f);
            View.allowHDR = false; View.allowDynamicResolution = false;
            View.orthographic = shot.Orthographic; View.orthographicSize = shot.OrthographicSize;
            View.fieldOfView = shot.FieldOfView; View.nearClipPlane = shot.Near; View.farClipPlane = shot.Far;
            View.aspect = Width / (float)Height; View.transform.position = offset + shot.Position;
            View.transform.LookAt(offset + shot.Target, Vector3.up);
        }

        private void Capture(WorldReviewShot shot)
        {
            var priorActive = RenderTexture.active; var priorTarget = View.targetTexture;
            var target = new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var texture = new Texture2D(Width,Height,TextureFormat.RGB24,false);
            try
            {
                View.targetTexture = target; View.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0,0,Width,Height),0,0); texture.Apply();
                var bytes = texture.EncodeToPNG(); File.WriteAllBytes(Path.Combine(output,shot.FileName),bytes);
                using var sha = SHA256.Create();
                receipts.Add(new ShotReceipt { Shot=shot, CameraPosition=View.transform.position, CameraRotation=View.transform.rotation,
                    SunIntensity=Sun.intensity, Ambient=RenderSettings.ambientLight,
                    Sha256=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(),Bytes=bytes.Length,
                    CaptureRealtimeSeconds=Time.realtimeSinceStartupAsDouble });
            }
            finally
            { View.targetTexture=priorTarget; RenderTexture.active=priorActive; target.Release(); Destroy(target); Destroy(texture); }
        }

        [Serializable] private sealed class ShotReceipt
        { public WorldReviewShot Shot; public Vector3 CameraPosition; public Quaternion CameraRotation; public float SunIntensity;
            public Color Ambient; public string Sha256; public long Bytes; public double CaptureRealtimeSeconds; }
        [Serializable] private sealed class CaptureReceipt
        { public string Method,WorldProfile,Device,GraphicsApi,ColorSpace,Quality,Shadows,Exposure,MissingEvidence;
            public int Width,Height,AntiAliasing; public float MetresPerUnit; public ShotReceipt[] Shots; }
    }
}
