
using UnityEngine;
using UnityEditor;

public static class InstallUnifiedFloorAndFixPlayer
{
    public static void Main(string[] args)
    {
        // 1. 2F 대합실 전체를 빈틈없이 덮는 단일 일체형 무봉합 보행 바닥 콜라이더 설치
        var interior = GameObject.Find("부산역 역사 내부/2F · 지상 2층");
        if (interior != null)
        {
            var old = interior.transform.Find("Concourse_UnifiedWalkingFloor");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var floorGo = new GameObject("Concourse_UnifiedWalkingFloor");
            floorGo.transform.SetParent(interior.transform, false);

            // 2층 대합실 전체를 커버하는 3개의 주 보행 영역 박스 콜라이더
            // 장축 16.2도 회전 적용
            float th = 16.2f * Mathf.Deg2Rad;
            floorGo.transform.rotation = Quaternion.Euler(0f, 16.2f, 0f);

            // 메인 대합실 홀 영역: u: -85~70 (155m), v: -50~10 (60m)
            // 상단면이 정확히 y = 7.00m가 되도록 (두께 0.5m, 중심 y = 6.75m)
            float cu = -7.5f, cv = -20f;
            Vector3 worldCenter = new Vector3(cu * Mathf.Sin(th) + cv * Mathf.Cos(th), 6.75f, cu * Mathf.Cos(th) - cv * Mathf.Sin(th));
            floorGo.transform.position = worldCenter;

            var col = floorGo.AddComponent<BoxCollider>();
            col.size = new Vector3(160f, 0.5f, 65f); // 160m x 65m 거대 일체형 콜라이더
            Debug.Log("Installed unified concourse floor collider at y=6.75 (top y=7.00)");
        }

        // 2. 플레이어 위치 및 스케일 완전 정상화
        var player = GameObject.Find("KORAIL 역무원");
        if (player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.localScale = Vector3.one; // 스케일 정확히 1.0 유지
            // 2층 대합실 중앙 광장 보행로 (u = -20, v = -20)
            float th = 16.2f * Mathf.Deg2Rad;
            float pu = -20f, pv = -20f;
            player.transform.position = new Vector3(pu * Mathf.Sin(th) + pv * Mathf.Cos(th), 7.08f, pu * Mathf.Cos(th) - pv * Mathf.Sin(th));
            player.transform.rotation = Quaternion.Euler(0f, 16.2f, 0f);

            if (cc != null)
            {
                cc.height = 1.72f;
                cc.radius = 0.28f;
                cc.center = new Vector3(0f, 0.86f, 0f);
                cc.stepOffset = 0.3f;
                cc.enabled = true;
            }
            Debug.Log("Player set to 2F concourse: " + player.transform.position);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
