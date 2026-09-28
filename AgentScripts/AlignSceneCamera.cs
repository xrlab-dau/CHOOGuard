
using UnityEngine;
using UnityEditor;

public static class AlignSceneCamera
{
    public static void Main(string[] args)
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null && SceneView.sceneViews.Count > 0)
            sv = (SceneView)SceneView.sceneViews[0];
        if (sv == null) return;

        // 2층 대합실 바닥(FL 7.00m) 위 1.65m 눈높이 = y 8.65m
        // 중앙 보행로 한가운데: x = -14.4f, z = 4.2f
        var eyePos = new Vector3(-14.4f, 8.65f, 4.2f);
        var rot = Quaternion.Euler(3f, 16.2f, 0f);

        sv.in2DMode = false;
        sv.orthographic = false;
        sv.rotation = rot;
        sv.size = 0.05f; // 1인칭 시점
        sv.pivot = eyePos + rot * Vector3.forward * 0.05f;
        sv.Repaint();
        Debug.Log("SceneView Aligned to 2F Center Pedestrian Eye");
    }
}
