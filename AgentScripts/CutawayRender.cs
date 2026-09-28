// 임의 평면에서 잘라낸 직교 투영을 실제로 렌더한다. 근평면이 절단면이다.
// args: [outPng, posX, posY, posZ, eulX, eulY, eulZ, orthoHalf, pixelsPerMetre, depth, isolateName?]
//   isolateName 이 주어지면 그 이름의 트랜스폼 하위 렌더러만 남기고 전부 끄고, 렌더 후 정확히 복구한다.
// 산출: PNG + 같은 이름 .json (카메라 파라미터)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class CutawayRender
{
    public static void Main(string[] args)
    {
        string outPng = Arg(args, 0, ".planning/2026-09-22-station-interior-build/cutaway.png");
        var pos = new Vector3(F(Arg(args, 1, "26")), F(Arg(args, 2, "10.4")), F(Arg(args, 3, "-48")));
        var eul = new Vector3(F(Arg(args, 4, "90")), F(Arg(args, 5, "0")), F(Arg(args, 6, "0")));
        float half = F(Arg(args, 7, "45"));
        float ppm = F(Arg(args, 8, "8"));
        float depth = F(Arg(args, 9, "12"));
        string isolate = Arg(args, 10, "");

        var disabled = new List<Renderer>();
        Transform keep = null;
        if (!string.IsNullOrEmpty(isolate))
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t.name == isolate) { keep = t; break; }
            if (keep == null) throw new InvalidOperationException("isolate 대상 없음: " + isolate);
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (r.enabled && !r.transform.IsChildOf(keep)) { r.enabled = false; disabled.Add(r); }
        }

        int side = Mathf.Clamp(Mathf.RoundToInt(half * 2f * ppm), 64, 4096);
        var go = new GameObject("__cutaway_cam");
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = half;
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.Euler(eul);
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = depth;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.05f, 1f);
            cam.cullingMask = ~0;
            cam.allowHDR = false;
            cam.allowMSAA = false;

            var rt = new RenderTexture(side, side, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(side, side, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, side, side), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prev;

            string full = Path.GetFullPath(outPng);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, tex.EncodeToPNG());

            var right = cam.transform.right;
            var up = cam.transform.up;
            string meta = "{\n  \"png\": \"" + Path.GetFileName(full) + "\",\n" +
                "  \"camPos\": [" + N(pos.x) + ", " + N(pos.y) + ", " + N(pos.z) + "],\n" +
                "  \"camEuler\": [" + N(eul.x) + ", " + N(eul.y) + ", " + N(eul.z) + "],\n" +
                "  \"orthoHalf\": " + N(half) + ",\n  \"pixelsPerMetre\": " + N(ppm) + ",\n  \"depth\": " + N(depth) + ",\n" +
                "  \"sidePixels\": " + side + ",\n" +
                "  \"right\": [" + N(right.x) + ", " + N(right.y) + ", " + N(right.z) + "],\n" +
                "  \"up\": [" + N(up.x) + ", " + N(up.y) + ", " + N(up.z) + "],\n" +
                "  \"isolate\": \"" + isolate + "\",\n" +
                "  \"pixelToWorld\": \"world = camPos + right*((px+0.5)/ppm - orthoHalf) + up*(orthoHalf - (py+0.5)/ppm)\"\n}\n";
            File.WriteAllText(full.Substring(0, full.Length - 4) + ".json", meta, new System.Text.UTF8Encoding(false));

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("CUTAWAY " + full + " side=" + side + " isolate=" + isolate + " hidden=" + disabled.Count);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            for (int i = 0; i < disabled.Count; i++) if (disabled[i] != null) disabled[i].enabled = true;
        }
    }

    static string N(float v) => v.ToString("F3", CultureInfo.InvariantCulture);
    static string Arg(string[] a, int i, string d) => a != null && a.Length > i && !string.IsNullOrEmpty(a[i]) ? a[i] : d;
    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}
