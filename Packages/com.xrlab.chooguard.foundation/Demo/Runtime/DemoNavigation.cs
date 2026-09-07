using UnityEngine;

namespace ChooGuard.Foundation.Demo
{
    public static class DemoNavigation
    {
        public static float Bearing(Quaternion facing, Vector3 position, Vector3 target)
        {
            var delta = target - position;
            delta.y = 0;
            if (delta.sqrMagnitude < .000001f) return 0;
            var forward = facing * Vector3.forward;
            forward.y = 0;
            return Vector3.SignedAngle(forward, delta, Vector3.up);
        }

        public static string Hint(float bearing)
        {
            if (Mathf.Abs(bearing) > 150) return "뒤돌아 목표물을 확인하세요";
            if (bearing < -12) return "← 왼쪽으로 " + Mathf.Abs(bearing).ToString("0") + "°";
            if (bearing > 12) return "오른쪽으로 " + bearing.ToString("0") + "° →";
            return "↑ 정면 목표물로 이동";
        }
    }
}
