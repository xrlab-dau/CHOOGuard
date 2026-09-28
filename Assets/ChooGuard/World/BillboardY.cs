using UnityEngine;

namespace ChooGuard.World
{
    /// <summary>
    /// 승객 컷아웃 빌보드. Y축만 회전해 카메라를 향한다 - 눕거나 기울지 않는다.
    /// 에디터에서도 갱신되어 Scene 뷰 확인이 가능하다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BillboardY : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.current != null ? Camera.current : Camera.main;
            if (cam == null) return;

            Vector3 to = transform.position - cam.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-6f) return;

            transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }
    }
}
