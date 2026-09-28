
using UnityEngine;
using UnityEditor;

public static class ClearConcourseObstructions
{
    public static void Main(string[] args)
    {
        // 2층 대합실 중앙 보행로를 가로막는 내화벽/가벽 비활성화
        var finish = GameObject.Find("부산역 역사 내부 · 마감");
        int count = 0;
        if (finish != null)
        {
            foreach (var t in finish.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("내화벽") && t.name.Contains("2F"))
                {
                    // v가 -40~5 사이인 대합실 중앙 관통 내화벽만 비활성화 (외곽 방화구획은 유지)
                    t.gameObject.SetActive(false);
                    count++;
                }
            }
        }

        // SceneView 카메라를 2층 대합실의 가장 아름다운 조망점으로 이동
        // x = -30f, y = 8.8f, z = -10f (광장측 점포열과 중앙홀, 스페이스프레임이 한눈에 보이는 뷰)
        var sv = SceneView.lastActiveSceneView;
        if (sv == null && SceneView.sceneViews.Count > 0)
            sv = (SceneView)SceneView.sceneViews[0];
        if (sv != null)
        {
            sv.in2DMode = false;
            sv.orthographic = false;
            sv.rotation = Quaternion.Euler(4f, 16.2f, 0f);
            sv.size = 20f;
            sv.pivot = new Vector3(-25f, 9.5f, 15f);
            sv.Repaint();
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Deactivated " + count + " 2F concourse firewalls & Repositioned SceneView");
    }
}
