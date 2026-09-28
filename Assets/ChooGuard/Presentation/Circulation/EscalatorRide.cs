// EscalatorRide.cs
// -----------------------------------------------------------------------------
// 출처 / Attribution
//   아이디어 참고: MisutaaAsriel/VRC-Escalators (탑승자 이송 개념),
//                 mimisukeMaster/Belt-Conveyor-System, Jayometric/3D-Conveyor-Belt-for-Unity
//                 (Rigidbody 컨베이어 이송 개념).
//   위 프로젝트들은 VRChat SDK 또는 자체 프레임워크에 의존하므로 코드를 가져오지
//   않았다. 본 파일은 순수 UnityEngine API 만으로 새로 작성한 구현이다.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace ChooGuard.Circulation
{
    /// <summary>
    /// 에스컬레이터/무빙워크의 트리거 볼륨 안에 있는 <see cref="CharacterController"/> 또는
    /// <see cref="Rigidbody"/> 를 진행 방향으로 일정 속도 이송한다.
    /// </summary>
    /// <remarks>
    /// <para><b>부착 방법</b></para>
    /// <list type="number">
    /// <item><description>
    /// 에스컬레이터 프리팹 아래에 빈 GameObject 를 만들고 이름을 <c>RideVolume_Incline</c> 으로 둔다.
    /// 이 오브젝트의 로컬 +Z 가 <b>경사 상승 방향</b>을 향하도록 회전시킨다
    /// (부산역 역사 생성기 기준 경사 30.0°, 유효폭 2.4m).
    /// </description></item>
    /// <item><description>
    /// <c>BoxCollider</c> 를 추가하고 <c>Is Trigger = true</c> 로 둔다.
    /// 크기는 유효폭 2.4m × 높이 2.0m(탑승자 키) × 경사 구간 길이.
    /// 박스 중심은 디딤판 표면에서 위로 1.0m 올린다.
    /// </description></item>
    /// <item><description>
    /// 같은 오브젝트에 이 컴포넌트를 추가하고 <see cref="rideAxis"/> 를 <c>(0,0,1)</c> 로 둔 뒤
    /// <see cref="direction"/> 을 Up/Down 으로 지정한다.
    /// <see cref="rideAxis"/> 는 <b>이 트랜스폼의 로컬 좌표계</b> 기준이며 월드로 변환되어 쓰인다.
    /// </description></item>
    /// <item><description>
    /// 상·하부 <b>착지판(landing plate)</b> 구간에는 같은 방식으로 볼륨을 하나 더 만들되
    /// <see cref="horizontalOnly"/> 를 켠다. 이러면 수평 성분만 남아 탑승자가
    /// 착지판에서 위/아래로 밀려 뜨거나 파묻히지 않는다.
    /// </description></item>
    /// <item><description>
    /// 플레이어에 <c>CharacterController</c> 가 있으면 <see cref="affectCharacterController"/>,
    /// 화물/소품 Rigidbody 도 실으려면 <see cref="affectRigidbody"/> 를 켠다.
    /// 플레이어 콜라이더가 트리거 이벤트를 받으려면 두 오브젝트 중 하나 이상이
    /// 물리 레이어 매트릭스에서 서로 충돌 가능해야 한다.
    /// </description></item>
    /// </list>
    /// <para><b>속도 근거</b></para>
    /// 「교통약자의 이동편의 증진법 시행규칙」 별표1 의 에스컬레이터 기준 운행속도
    /// 30 m/min = 0.5 m/s 를 <see cref="speed"/> 기본값으로 사용한다.
    /// </remarks>
    [AddComponentMenu("CHOOGuard/Circulation/Escalator Ride")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EscalatorRide : MonoBehaviour
    {
        /// <summary>이송 방향(운행 방향).</summary>
        public enum RideDirection
        {
            /// <summary><see cref="rideAxis"/> 정방향(상행).</summary>
            Up = 1,

            /// <summary><see cref="rideAxis"/> 역방향(하행).</summary>
            Down = -1
        }

        [Header("운행")]
        [Tooltip("이송 속도 (m/s). 교통약자의 이동편의 증진법 시행규칙 별표1 " +
                 "에스컬레이터 기준 운행속도 30 m/min = 0.5 m/s.")]
        [Min(0f)]
        [SerializeField]
        private float speed = 0.5f;

        [Tooltip("상행(Up) / 하행(Down).")]
        [SerializeField]
        private RideDirection direction = RideDirection.Up;

        [Tooltip("진행 방향 벡터(이 트랜스폼의 로컬 좌표계 기준). 보통 (0,0,1).")]
        [SerializeField]
        private Vector3 rideAxis = Vector3.forward;

        [Tooltip("착지판 구간. 켜면 수직 성분을 제거하고 수평 이송만 한다.")]
        [SerializeField]
        private bool horizontalOnly;

        [Header("대상")]
        [Tooltip("CharacterController 를 이송한다.")]
        [SerializeField]
        private bool affectCharacterController = true;

        [Tooltip("Rigidbody 를 이송한다.")]
        [SerializeField]
        private bool affectRigidbody = true;

        /// <summary>정규화된 로컬 진행 축. <see cref="OnValidate"/> 에서 갱신된다.</summary>
        private Vector3 normalizedAxis = Vector3.forward;

        /// <summary>이송 속도 (m/s).</summary>
        public float Speed
        {
            get => speed;
            set
            {
                speed = Mathf.Max(0f, value);
            }
        }

        /// <summary>운행 방향.</summary>
        public RideDirection Direction
        {
            get => direction;
            set => direction = value;
        }

        /// <summary>착지판(수평 이송 전용) 여부.</summary>
        public bool HorizontalOnly
        {
            get => horizontalOnly;
            set => horizontalOnly = value;
        }

        /// <summary>현재 프레임에 적용될 월드 공간 이송 속도 벡터 (m/s).</summary>
        public Vector3 WorldVelocity
        {
            get
            {
                Vector3 worldDir = transform.TransformDirection(normalizedAxis) * (int)direction;

                if (horizontalOnly)
                {
                    worldDir.y = 0f;
                }

                float magnitude = worldDir.magnitude;
                if (magnitude < 1e-5f)
                {
                    return Vector3.zero;
                }

                return (worldDir / magnitude) * speed;
            }
        }

        private void Reset()
        {
            // 트리거 볼륨으로 쓰이도록 기본값을 맞춰 준다.
            if (TryGetComponent(out Collider col))
            {
                col.isTrigger = true;
            }

            OnValidate();
        }

        /// <summary>
        /// 인스펙터에서 값을 바꿔도 즉시 반영되도록 축과 속도를 정규화한다.
        /// </summary>
        private void OnValidate()
        {
            speed = Mathf.Max(0f, speed);

            normalizedAxis = rideAxis.sqrMagnitude < 1e-8f
                ? Vector3.forward
                : rideAxis.normalized;

            // 인스펙터 표시값도 정규화된 형태로 되돌려 준다.
            rideAxis = normalizedAxis;
        }

        private void Awake()
        {
            OnValidate();
        }

        private void OnTriggerStay(Collider other)
        {
            if (speed <= 0f || other == null)
            {
                return;
            }

            Vector3 velocity = WorldVelocity;
            if (velocity.sqrMagnitude < 1e-8f)
            {
                return;
            }

            if (affectCharacterController &&
                other.TryGetComponent(out CharacterController controller))
            {
                controller.Move(velocity * Time.deltaTime);
                return;
            }

            if (!affectRigidbody)
            {
                return;
            }

            Rigidbody body = other.attachedRigidbody;
            if (body == null)
            {
                return;
            }

            // 정지해 잠든 Rigidbody 는 MovePosition 후에도 보간이 멈춰 있을 수 있다.
            if (body.IsSleeping())
            {
                body.WakeUp();
            }

            body.MovePosition(body.position + velocity * Time.deltaTime);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 velocity = WorldVelocity;
            if (velocity.sqrMagnitude < 1e-8f)
            {
                return;
            }

            Vector3 origin = transform.position;
            Vector3 tip = origin + velocity.normalized * 2f;

            Gizmos.color = horizontalOnly ? Color.cyan : Color.green;
            Gizmos.DrawLine(origin, tip);
            Gizmos.DrawSphere(tip, 0.08f);
        }
#endif
    }
}
