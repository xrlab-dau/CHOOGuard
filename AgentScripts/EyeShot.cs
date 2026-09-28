// 임시 카메라를 정확한 좌표·방위에 세우고 RenderTexture 로 찍어 PNG 로 저장한다.
// SceneView 상태에 의존하지 않는다.
// args: [outPng, x, y, z, yaw, pitch, fov, width, height]

using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public static class EyeShot
{
    static float F(string[] a, int i, float d)
    {
        if (a == null || i >= a.Length || string.IsNullOrEmpty(a[i])) return d;
        return float.Parse(a[i], CultureInfo.InvariantCulture);
    }

    public static void Main(string[] args)
    {
        string outPath = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/captures/eye.png";
        var pos = new Vector3(F(args, 1, 0f), F(args, 2, 1.6f), F(args, 3, 0f));
        float yaw = F(args, 4, 0f), pitch = F(args, 5, 0f), fov = F(args, 6, 60f);
        int w = (int)F(args, 7, 1280f), h = (int)F(args, 8, 720f);

        var go = new GameObject("__EyeShot");
        try
        {
            var cam = go.AddComponent<Camera>();
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1500f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.enabled = false;

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            string full = Path.GetFullPath(outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, tex.EncodeToPNG());

            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("EYESHOT " + outPath + " " + w + "x" + h + " @" + pos.ToString("F1") + " yaw=" + yaw);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
