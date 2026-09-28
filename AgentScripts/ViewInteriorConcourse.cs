
using UnityEngine;
using UnityEditor;

public static class ViewInteriorConcourse
{
    public static void Main(string[] args)
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null && SceneView.sceneViews.Count > 0)
            sv = (SceneView)SceneView.sceneViews[0];
        if (sv == null) return;

        // 2층 대합실 광장측 출입구 부근에서 중앙홀을 바라보는 뷰
        // x = -28f, y = 8.65f, z = -12f, yaw = 18f
        sv.in2DMode = false;
        sv.orthographic = false;
        sv.rotation = Quaternion.Euler(5f, 18f, 0f);
        sv.size = 16f;
        sv.pivot = new Vector3(-20f, 9.2f, 8f);
        sv.Repaint();
        Debug.Log("SceneView updated to concourse hero view");
    }
}
