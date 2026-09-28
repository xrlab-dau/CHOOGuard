// 씬 뷰 카메라를 지정 좌표·방위로 세운다.
// args: [x, y, z, yaw, pitch]
using System;
using System.Globalization;
using UnityEngine;
using UnityEditor;

public static class SceneViewLook
{
    static float F(string[] a, int i, float d)
    {
        if (a == null || i >= a.Length || string.IsNullOrEmpty(a[i])) return d;
        return float.Parse(a[i], CultureInfo.InvariantCulture);
    }

    public static void Main(string[] args)
    {
        var pos = new Vector3(F(args, 0, 0f), F(args, 1, 5f), F(args, 2, 0f));
        var rot = Quaternion.Euler(F(args, 4, 8f), F(args, 3, 0f), 0f);

        var sv = SceneView.lastActiveSceneView;
        if (sv == null)
        {
            foreach (SceneView v in SceneView.sceneViews) { sv = v; break; }
        }
        if (sv == null) throw new InvalidOperationException("SceneView 없음");

        sv.in2DMode = false;
        sv.orthographic = false;
        sv.rotation = rot;
        sv.size = 0.01f;
        sv.pivot = pos + rot * (Vector3.forward * 0.01f);
        sv.Repaint();
        Debug.Log("SCENEVIEW " + pos.ToString("F1") + " yaw=" + F(args, 3, 0f));
    }
}
