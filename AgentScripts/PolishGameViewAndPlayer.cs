
using UnityEngine;
using UnityEditor;

public static class PolishGameViewAndPlayer
{
    public static void Main(string[] args)
    {
        // 1. 플레이어 자신의 메쉬가 1인칭 카메라를 가리는 현상 제거
        var player = GameObject.Find("KORAIL 역무원");
        if (player != null)
        {
            var cam = player.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cam.nearClipPlane = 0.25f; // 카메라 코앞 클리핑 완화
            }
            // 역무원 렌더러들은 그림자만 켜고 1인칭 카메라에서는 보이지 않게 처리
            foreach(var mr in player.GetComponentsInChildren<MeshRenderer>(true))
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
            foreach(var smr in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
            Debug.Log("Player self-clipping eliminated");
        }

        // 2. 2F 천장 선형 조명기구가 시야를 가리지 않도록 높이 상향 및 배치 정돈
        var lightsRoot = GameObject.Find("부산역 역사 내부 · 조명기구/2F");
        if (lightsRoot != null)
        {
            int raised = 0;
            foreach (Transform t in lightsRoot.transform)
            {
                // 천장 스페이스프레임 상현 부근(y = 14.8m)으로 올린다
                if (t.position.y < 13.5f)
                {
                    t.position = new Vector3(t.position.x, 14.5f, t.position.z);
                    raised++;
                }
            }
            Debug.Log("Raised " + raised + " 2F lighting fixtures to y=14.5m");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
