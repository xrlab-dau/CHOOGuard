#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>
    /// Films a real-scene practice walkthrough when CG_PRACTICE_VIDEO names a folder: the player's camera with the HUD drawn into it,
    /// one JPG every <see cref="Interval"/> seconds of real time, plus events.txt that puts every CG_PRACTICE line, warning, error and
    /// exception next to the frame it happened on. ffmpeg turns the frames into a video afterwards. Off by default; needs a graphics
    /// device (run without -nographics).
    /// </summary>
    /// <remarks>
    /// batchmode에는 화면이 없어서 카메라를 RenderTexture에 고정하고 직접 Render한다. 화면 오버레이 캔버스는 카메라 렌더에 안 들어가므로
    /// 찍는 동안 ScreenSpaceCamera로 바꿔 둔다(테스트가 끝나면 씬째 버려진다).
    /// </remarks>
    internal sealed class PracticeVideoRecorder : MonoBehaviour
    {
        public const string Variable = "CG_PRACTICE_VIDEO";
        private const int Width = 1280, Height = 720;
        private const float Interval = .5f;

        private static PracticeVideoRecorder current;
        private Camera view;
        private RenderTexture target;
        private Texture2D shot;
        private string folder;
        private float started, next;
        private int frames;
        private readonly StringBuilder events = new StringBuilder();
        private readonly HashSet<Canvas> moved = new HashSet<Canvas>();

        public static void Begin(string id, EmergencySession session)
        {
            End();
            string root = Environment.GetEnvironmentVariable(Variable);
            if (string.IsNullOrEmpty(root) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = session.Player != null ? session.Player.PlayerCamera : null;
            if (camera == null) return;
            current = new GameObject("PracticeVideoRecorder").AddComponent<PracticeVideoRecorder>();
            current.Open(Path.Combine(root, id), camera);
        }

        public static void Mark(string text) => current?.Add("MARK", text);

        /// <summary>While filming, a beat after each action so the HUD's answer (toast, log line, prompt) shows up in the video.</summary>
        public static object Beat(float seconds = 2.5f) => current != null ? new WaitForSecondsRealtime(seconds) : null;

        public static void End()
        {
            if (current == null) return;
            current.Close();
            if (current != null) Destroy(current.gameObject);
            current = null;
        }

        private void Open(string path, Camera camera)
        {
            folder = path;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            view = camera;
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            view.targetTexture = target;
            view.aspect = (float)Width / Height;
            started = Time.realtimeSinceStartup;
            next = started;
            Application.logMessageReceived += Heard;
            Add("MARK", "녹화 시작");
        }

        private void Heard(string message, string stack, LogType type)
        {
            if (type == LogType.Log && !message.StartsWith("CG_PRACTICE")) return;
            string line = type == LogType.Log ? message : message + (string.IsNullOrEmpty(stack) ? "" : "\n    " + stack.Trim().Replace("\n", "\n    "));
            Add(type.ToString().ToUpperInvariant(), line);
        }

        private void Add(string kind, string text) =>
            events.Append((Time.realtimeSinceStartup - started).ToString("0000.0")).Append("s f").Append(frames.ToString("00000"))
                  .Append(' ').Append(kind).Append(' ').Append(text).Append('\n');

        private void LateUpdate()
        {
            if (view == null || Time.realtimeSinceStartup < next) return;
            next += Interval;
            if (next < Time.realtimeSinceStartup) next = Time.realtimeSinceStartup + Interval;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = view;
                // 정렬 순서가 큰 캔버스가 앞에 오도록 조금씩 앞으로 당긴다.
                canvas.planeDistance = .2f - Mathf.Clamp(canvas.sortingOrder, 0, 100) * .0005f;
                moved.Add(canvas);
            }
            if (view.targetTexture != target) view.targetTexture = target;
            view.Render();
            var wasActive = RenderTexture.active;
            RenderTexture.active = target;
            shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            shot.Apply(false);
            RenderTexture.active = wasActive;
            File.WriteAllBytes(Path.Combine(folder, "f" + frames.ToString("00000") + ".jpg"), shot.EncodeToJPG(80));
            frames++;
        }

        private void Close()
        {
            if (folder == null) return;
            Application.logMessageReceived -= Heard;
            Add("MARK", "녹화 끝 · 프레임 " + frames);
            File.WriteAllText(Path.Combine(folder, "events.txt"), events.ToString(), new UTF8Encoding(false));
            if (view != null && view.targetTexture == target) view.targetTexture = null;
            if (view != null) view.ResetAspect();
            if (target != null) { target.Release(); DestroyImmediate(target); }
            if (shot != null) DestroyImmediate(shot);
            folder = null;
        }

        private void OnDestroy()
        {
            Close();
            if (current == this) current = null;
        }
    }
}
#endif
