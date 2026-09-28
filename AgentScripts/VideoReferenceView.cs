// Set an explicitly inferred SfM comparison camera. This is NOT a movement verification.
// args: [videoSeconds, scene|game|both]
using System;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class VideoReferenceView
{
    public static void Main(string[] args)
    {
        float seconds = float.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture);
        string mode = args.Length > 1 ? args[1] : "both";
        var doc = JObject.Parse(File.ReadAllText(".planning/2026-09-23-video-twin/registered-video-cameras.json"));
        var record = ((JArray)doc["cameras"]).Cast<JObject>().OrderBy(p => Mathf.Abs((float)p["seconds"] - seconds)).First();
        var p = (JArray)record["worldFeet"];
        var foot = new Vector3((float)p[0], (float)p[1], (float)p[2]);
        float yaw = (float)record["yaw"], pitch = (float)record["pitch"];
        float focal = seconds < 83 ? 579.64723f : seconds <= 197.5f ? 589.52008f : 581.41650f;
        float fieldOfView = 2 * Mathf.Atan(270f / focal) * Mathf.Rad2Deg;
        Vector3 eye = foot + Vector3.up * 1.6f;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        if (mode == "scene" || mode == "both")
        {
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.orthographic = false;
            view.sceneLighting = true;
            view.cameraSettings.fieldOfView = fieldOfView;
            view.rotation = rotation;
            view.size = .01f;
            view.pivot = eye + rotation * Vector3.forward * (.01f / Mathf.Sin(fieldOfView * Mathf.Deg2Rad * .5f));
            // A hidden dock tab does not necessarily repaint before the next CLI call.
            // Synchronize the real camera too; otherwise captures can silently retain the previous pose.
            view.camera.transform.SetPositionAndRotation(eye, rotation);
            view.camera.fieldOfView = fieldOfView;
            view.Focus();
            view.Repaint();
        }
        if (mode == "game" || mode == "both")
        {
            var actor = UnityEngine.Object.FindFirstObjectByType<FirstPersonResponder>();
            if (actor == null) throw new InvalidOperationException("Player is absent.");
            actor.SetExternalInputMode(true);
            var controller = actor.GetComponent<CharacterController>();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            actor.transform.SetPositionAndRotation(foot, Quaternion.Euler(0, yaw, 0));
            actor.PlayerCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            actor.PlayerCamera.fieldOfView = fieldOfView;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
            actor.Resume(false);
            var gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(gameView).Focus();
        }
        Debug.Log("VIDEO_REFERENCE_VIEW t=" + record["seconds"] + " eye=" + eye + " FOV=" + fieldOfView
            + " alignment=INFERRED_METRIC; comparison-only placement, not walked arrival");
    }
}
