using System;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Facilities
{
    /// <summary>
    /// One door of the Busan twin, built by the interior kit (MajibangBuilder KitBuild / KitDoors) with its leaves on pivots in
    /// this object's space (the kit frame).
    /// Sliding doors (entrance banks, shop fronts) open for anyone in the sensor field, stay open while someone stands in the
    /// opening and close after a short hold; staff switch the operator key 자동 → 상시 개방 → 잠금 on public entrance doors, and
    /// a fire signal opens them (automatic-door controller modes, research D1; JEV 010). Hinged doors are pushed open with E
    /// and pulled shut by their closer; staff-room doors are locked from outside and open with the staff key, while the lever
    /// inside always opens (JEV 010 staff_key_lock). Elevator landing doors (and the car doors of the cabin behind them) are
    /// driven by <see cref="ElevatorCar"/>. A door with nothing walkable beyond it in the twin stays shut and says so.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StationDoor : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated, IFpsSecondaryInteraction, IFpsHoldInteraction, IFpsObservable
    {
        public enum DoorKind { Sliding, Swing, Elevator }
        /// <summary>Public: anyone (entrance banks, stairs). Staff: 관계자 rooms, key from outside. Tenant: shop doors (the shop runs them).</summary>
        public enum DoorUse { Public, Staff, Tenant }
        public enum OperatorMode { Auto, HoldOpen, Locked }

        [Serializable]
        public sealed class Leaf
        {
            public Transform Pivot;
            [Tooltip("Pivot-local travel of a sliding leaf when fully open (m).")]
            public Vector3 Slide;
            [Tooltip("Hinged leaf: degrees about the pivot's up axis when fully open; the sign is the opening direction.")]
            public float Swing;
            [Tooltip("Hinged leaf: hinge-to-free-edge length (m).")]
            public float Length;
            [NonSerialized] public Vector3 ClosedPosition;
            [NonSerialized] public Quaternion ClosedRotation;
        }

        [Header("키트")]
        public string Element;
        public int Index;
        public string Label;
        public DoorKind Kind;
        public DoorUse Use;
        [Tooltip("Opening centre on the floor, the element's outward normal and its along direction, in this object's space.")]
        public Vector3 Centre, Normal = Vector3.forward, Along = Vector3.right;
        public float Width = 1, Height = 2.1f;
        [Tooltip("Opening seen in the evidence (0 shut, 1 open). A fully open entrance bank starts held open.")]
        public float ObservedOpen;
        [Tooltip("Why the door cannot be used (nothing walkable beyond it in the twin). Empty when usable.")]
        public string Sealed = "";
        public Leaf[] Leaves = Array.Empty<Leaf>();

        [Header("엘리베이터 승강장")]
        public string Floor = "";
        public string CarId = "";
        [Tooltip("Landing call panel (hall buttons).")]
        public Transform CallPanel;
        [Tooltip("Floor text in the hall lantern.")]
        public TextMesh Lantern;
        [Tooltip("Cabin behind the landing doors: origin on the car floor at the door line, +z into the car.")]
        public Transform Cabin;
        public Vector3 CabinSize;

        // 게임 압축 없이 실제 자동문 수준: 전개 약 1.2초, 감지가 끊긴 뒤 2.5초 유지, 닫힘 약 2초.
        public const float SlideOpenSeconds = 1.2f, SlideCloseSeconds = 2f, SlideHoldSeconds = 2.5f, SensorDepth = 2f;
        public const float SwingOpenSeconds = 1f, SwingCloseSeconds = 2.4f, CloserDelay = 4f;

        public float Open { get; private set; }
        public OperatorMode Mode { get; private set; }
        public ElevatorCar Car { get; internal set; }
        /// <summary>Elevator landing doors: where the car wants these doors (0..1).</summary>
        internal float DriveTarget;
        public bool Usable => string.IsNullOrEmpty(Sealed);
        /// <summary>
        /// Whether passengers walk through: automatic sliding doors (entrances and shops) open as they come near and public
        /// hinged doors are pushed open (JEV 010 npc_door_use). Staff doors, elevator landings and doors that cannot be used
        /// stay shut to them. The navmesh bake keeps these doorways walkable; a locked entrance carves its doorway at runtime.
        /// </summary>
        public bool PassableByPeople => Usable && (Kind == DoorKind.Sliding ? Use != DoorUse.Staff : Kind == DoorKind.Swing && Use == DoorUse.Public);
        public string DisplayName => Label;

        private float target, holdUntil, closeAt = -1;
        private bool alarmOpen;
        // 손으로 여닫이를 밀고 당기는 동안(E 홀드): 문짝이 손을 따르고, 놓으면 도어 클로저가 CloserDelay 뒤 닫는다.
        // 손맛 수치(튜닝 대상): 시선 35도의 끌기에 문이 끝까지 열린다.
        private const float DragDegreesForFullSwing = 35f;
        private bool holding, keyFeedbackPending;
        private Leaf holdLeaf;
        private Vector3 worldCentre;
        private NavMeshObstacle lockBlock;
        private AudioSource sound;
        private static FirstPersonResponder player;
        private static AudioClip latch;

        private void Awake()
        {
            foreach (var leaf in Leaves)
            {
                if (leaf.Pivot == null) continue;
                leaf.ClosedPosition = leaf.Pivot.localPosition;
                leaf.ClosedRotation = leaf.Pivot.localRotation;
                // A moving collider without a body is re-inserted into the static tree every frame it moves.
                if (!leaf.Pivot.TryGetComponent(out Rigidbody body)) body = leaf.Pivot.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
            }
            worldCentre = transform.TransformPoint(Centre + Vector3.up * 1.1f);
            Mode = Kind == DoorKind.Sliding && Use == DoorUse.Public && ObservedOpen >= .95f ? OperatorMode.HoldOpen : OperatorMode.Auto;
            if (Kind == DoorKind.Sliding && !Usable) Mode = OperatorMode.Locked;
            if (Kind == DoorKind.Sliding)
            {
                // 문 위 구동부(운전 모드 키 스위치 자리): 문이 열려 있어도 보고 조작할 수 있게 한다. 머리 위라 걸음은 막지 않는다.
                var header = new GameObject("자동문 구동부");
                header.transform.SetParent(transform, false);
                header.transform.localPosition = Centre + Vector3.up * (Height + .2f);
                header.transform.localRotation = Quaternion.LookRotation(Normal, Vector3.up);
                header.AddComponent<BoxCollider>().size = new Vector3(Width, .36f, .24f);
            }
        }

        private void Start()
        {
            if (Kind == DoorKind.Elevator) ElevatorCar.Assemble(gameObject.scene);
            Apply();
        }

        private static FirstPersonResponder Player
        {
            get
            {
                if (player == null || !player.isActiveAndEnabled) player = FindFirstObjectByType<FirstPersonResponder>();
                return player;
            }
        }

        // ── 판정 ─────────────────────────────────────────────────────────────

        /// <summary>Opening-local coordinates of a world point: x along the door, y up from the floor, z out along the normal.</summary>
        public Vector3 Local(Vector3 world)
        {
            var d = transform.InverseTransformPoint(world) - Centre;
            return new Vector3(Vector3.Dot(d, Along), d.y, Vector3.Dot(d, Normal));
        }

        /// <summary>World point of opening-local coordinates (see <see cref="Local"/>).</summary>
        public Vector3 World(Vector3 local) => transform.TransformPoint(Centre + Along * local.x + Vector3.up * local.y + Normal * local.z);

        /// <summary>Whether someone at <paramref name="world"/> is standing in the doorway (between the leaves' travel).</summary>
        public bool InDoorway(Vector3 world, float depth = .45f)
        {
            var l = Local(world);
            return l.y > -.5f && l.y < 2.2f && Mathf.Abs(l.x) < Width * .5f + .05f && Mathf.Abs(l.z) < depth;
        }

        private bool InField(Vector3 world, float depth)
        {
            if ((world + Vector3.up * 1.1f - worldCentre).sqrMagnitude > (depth + Width) * (depth + Width)) return false;
            var l = Local(world);
            return l.y > -.5f && l.y < 2.2f && Mathf.Abs(l.x) < Width * .5f + .5f && Mathf.Abs(l.z) < depth;
        }

        /// <summary>Someone (the staff member or a visible passenger/responder) in the field; <paramref name="doorway"/> when one stands in the opening.</summary>
        private bool Sense(float depth, bool people, out bool doorway)
        {
            doorway = false;
            bool any = false;
            var p = Player;
            if (p != null && p.isActiveAndEnabled)
            {
                var at = p.transform.position;
                if (InField(at, depth)) { any = true; doorway |= InDoorway(at); }
            }
            if (!people) return any;
            var all = PersonBody.All;
            for (int i = 0; i < all.Count; i++)
            {
                var body = all[i];
                if (body == null || !body.Visible) continue;
                var at = body.transform.position;
                if (!InField(at, depth)) continue;
                any = true;
                if (InDoorway(at)) { doorway = true; break; }
            }
            return any;
        }

        // ── 움직임 ───────────────────────────────────────────────────────────

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            float before = Open;
            switch (Kind)
            {
                case DoorKind.Sliding: UpdateSliding(dt); break;
                case DoorKind.Swing: UpdateSwing(dt); break;
                default:
                    Open = Mathf.MoveTowards(Open, DriveTarget, dt / ElevatorCar.DoorSeconds);
                    break;
            }
            if (!Mathf.Approximately(before, Open)) { Apply(); Sounds(before); }
        }

        private void UpdateSliding(float dt)
        {
            bool alarm = StationSignals.FireAlarm && Use == DoorUse.Public && Usable;
            if (alarm != alarmOpen) alarmOpen = alarm;
            SetLockBlock(Mode == OperatorMode.Locked && !alarmOpen && Usable);
            if (alarmOpen || Mode == OperatorMode.HoldOpen) target = 1;
            else if (Mode == OperatorMode.Locked || !Usable) target = 0;
            else
            {
                bool someone = Sense(SensorDepth, true, out bool doorway);
                if (someone) holdUntil = Time.time + SlideHoldSeconds;
                // 문턱에 사람이 있으면 닫지 않는다(보조 광전 센서).
                target = someone || doorway || Time.time < holdUntil ? 1 : 0;
            }
            Open = Mathf.MoveTowards(Open, target, dt / (target > Open ? SlideOpenSeconds : SlideCloseSeconds));
        }

        private void UpdateSwing(float dt)
        {
            if (!Usable) { Open = 0; return; }
            // 손이 문짝을 잡고 있는 동안은 손이 정한다(Hold). 놓은 뒤에 도어 클로저가 이어받는다.
            if (holding) return;
            // 공용 여닫이는 승객이 밀고 지나간다(JEV 010 npc_door_use). 관계자 문은 승객이 쓰지 않는다.
            if (Use == DoorUse.Public && Sense(1.2f, true, out _) && target < 1 && !PlayerNear()) { target = 1; closeAt = -1; }
            // 도어 클로저: 열린 뒤 아무도 문 앞을 지나지 않으면 천천히 닫힌다.
            if (target > 0)
            {
                bool busy = Sense(1.1f, Use == DoorUse.Public, out bool doorway) && doorway;
                if (busy || closeAt < 0) closeAt = Time.time + CloserDelay;
                else if (Time.time >= closeAt) { target = 0; closeAt = -1; }
            }
            float limit = SwingLimit();
            float goal = Mathf.Min(target, limit);
            Open = Mathf.MoveTowards(Open, goal, dt / (goal > Open ? SwingOpenSeconds : SwingCloseSeconds));
        }

        private bool PlayerNear()
        {
            var p = Player;
            return p != null && InField(p.transform.position, 1.2f);
        }

        /// <summary>How far the hinged leaves may open before one touches the staff member standing on their swing side (0..1).</summary>
        private float SwingLimit()
        {
            var p = Player;
            if (p == null) return 1;
            float limit = 1;
            foreach (var leaf in Leaves)
            {
                if (leaf.Pivot == null || Mathf.Abs(leaf.Swing) < 1 || leaf.Length <= 0) continue;
                var local = Quaternion.Inverse(leaf.ClosedRotation) * (leaf.Pivot.parent.InverseTransformPoint(p.transform.position) - leaf.ClosedPosition);
                if (local.y < -.5f || local.y > 2.2f) continue;
                float r = new Vector2(local.x, local.z).magnitude;
                if (r > leaf.Length + .35f || r < .05f) continue;
                // Euler(0, θ, 0) 은 x 축을 (cos θ, 0, -sin θ) 로 돌린다: 사람이 있는 방향까지의 여는 각.
                float phi = Mathf.Atan2(-local.z, local.x) * Mathf.Rad2Deg * Mathf.Sign(leaf.Swing);
                float sweep = Mathf.Abs(leaf.Swing);
                if (phi <= -10 || phi > sweep + 25) continue;
                float margin = Mathf.Asin(Mathf.Clamp01(.34f / Mathf.Max(r, .35f))) * Mathf.Rad2Deg;
                limit = Mathf.Min(limit, Mathf.Clamp01((phi - margin) / sweep));
            }
            return limit;
        }

        private void Apply()
        {
            foreach (var leaf in Leaves)
            {
                if (leaf.Pivot == null) continue;
                if (Mathf.Abs(leaf.Swing) > .01f) leaf.Pivot.localRotation = leaf.ClosedRotation * Quaternion.Euler(0, leaf.Swing * Open, 0);
                else leaf.Pivot.localPosition = leaf.ClosedPosition + leaf.ClosedRotation * (leaf.Slide * Open);
            }
        }

        private void SetLockBlock(bool on)
        {
            if (lockBlock == null)
            {
                if (!on) return;
                var go = new GameObject("잠금 · 길 막음");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Centre + Vector3.up * 1.1f;
                go.transform.localRotation = Quaternion.LookRotation(Normal, Vector3.up);
                lockBlock = go.AddComponent<NavMeshObstacle>();
                lockBlock.shape = NavMeshObstacleShape.Box;
                lockBlock.size = new Vector3(Width + .3f, 2.2f, .4f);
                lockBlock.carving = true;
                lockBlock.carveOnlyStationary = false;
            }
            if (lockBlock.enabled != on) lockBlock.enabled = on;
        }

        private void Sounds(float before)
        {
            AudioClip clip = null;
            float pitch = 1;
            if (Kind == DoorKind.Swing)
            {
                if (before > .02f && Open <= .02f) { clip = Latch(); pitch = 1; }
            }
            else if (before <= .01f && Open > .01f) clip = StationSignals.DoorOpen;
            else if (before > .02f && Open <= .02f) clip = StationSignals.DoorClose;
            if (clip == null) return;
            if (sound == null)
            {
                var source = new GameObject("문 소리");
                source.transform.SetParent(transform, false);
                source.transform.localPosition = Centre + Vector3.up * 2f;
                sound = source.AddComponent<AudioSource>();
                sound.playOnAwake = false;
                sound.spatialBlend = 1;
                sound.rolloffMode = AudioRolloffMode.Linear;
                sound.minDistance = 1.5f;
                sound.maxDistance = 16;
                sound.volume = Kind == DoorKind.Swing ? .5f : .3f;
            }
            sound.pitch = pitch;
            sound.PlayOneShot(clip);
        }

        /// <summary>Latch click of a hinged door shutting: a short damped noise burst (generated once).</summary>
        private static AudioClip Latch()
        {
            if (latch != null) return latch;
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000, length = rate / 8;
            var data = new float[length];
            var random = new System.Random(7);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float click = (float)(random.NextDouble() * 2 - 1) * Mathf.Exp(-t * 90);
                float thud = Mathf.Sin(2 * Mathf.PI * 95 * t) * Mathf.Exp(-t * 28) * .8f;
                data[i] = (click * .5f + thud) * .7f;
            }
            latch = AudioClip.Create("문 닫힘 걸쇠", length, 1, rate, false);
            latch.SetData(data, 0);
            return latch;
        }

        // ── 상호작용 ─────────────────────────────────────────────────────────

        private bool Outside(FirstPersonResponder responder) => Local(responder.transform.position).z >= 0;

        public string InteractionPrompt
        {
            get
            {
                switch (Kind)
                {
                    case DoorKind.Swing:
                        if (target > 0 || Open > .05f) return "닫기";
                        return Use == DoorUse.Staff && Player != null && Outside(Player) ? "열쇠로 열기" : "열기";
                    case DoorKind.Elevator: return Car != null ? Car.LandingPrompt(this) : "";
                    default: return "";
                }
            }
        }

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (!Usable) { reason = Sealed; return false; }
            switch (Kind)
            {
                case DoorKind.Swing: return true;
                case DoorKind.Elevator:
                    if (Car == null) { reason = "다른 층 승강장이 트윈에 없어 운행하지 않습니다"; return false; }
                    return Car.CanCall(this, out reason);
                default:
                    reason = SlidingState();
                    return false;
            }
        }

        private string SlidingState()
        {
            if (alarmOpen) return "화재 신호 연동 · 열림 유지";
            switch (Mode)
            {
                case OperatorMode.HoldOpen: return "상시 개방";
                case OperatorMode.Locked: return "잠금 · 열리지 않습니다";
                default: return Use == DoorUse.Tenant ? "자동문 · 다가가면 열립니다 (점포 관리)" : "자동문 · 다가가면 열립니다";
            }
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            if (Kind == DoorKind.Elevator) return Car.Call(this, out feedback);
            if (Kind != DoorKind.Swing) return false;
            if (target > 0 || Open > .05f) { target = 0; closeAt = -1; return true; }
            if (Use == DoorUse.Staff && Outside(responder)) feedback = "열쇠로 문을 열었습니다";
            target = 1;
            closeAt = Time.time + CloserDelay;
            return true;
        }

        public string SecondaryPrompt
        {
            get
            {
                // 운전 모드 키 스위치는 역무원이 관리하는 공용 출입문에만 있다(점포 문은 점포가 관리).
                if (Kind != DoorKind.Sliding || Use != DoorUse.Public || !Usable) return "";
                switch (Mode)
                {
                    case OperatorMode.Auto: return "열쇠 스위치 · 상시 개방으로";
                    case OperatorMode.HoldOpen: return "열쇠 스위치 · 잠금으로";
                    default: return "열쇠 스위치 · 자동으로";
                }
            }
        }

        public bool TrySecondary(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (SecondaryPrompt.Length == 0) return false;
            Mode = Mode == OperatorMode.Auto ? OperatorMode.HoldOpen : Mode == OperatorMode.HoldOpen ? OperatorMode.Locked : OperatorMode.Auto;
            feedback = Label + " · " + (Mode == OperatorMode.Auto ? "자동 운전" : Mode == OperatorMode.HoldOpen ? "상시 개방" : "잠금") + (alarmOpen ? " (화재 신호가 풀릴 때까지 열림 유지)" : "");
            return true;
        }

        /// <summary>Sets the operator mode (scripted drills, tests).</summary>
        public void SetMode(OperatorMode mode) => Mode = mode;

        // ── 손 조작 ──────────────────────────────────────────────────────────

        public HoldStyle HoldStyle => HoldStyle.Swing;

        /// <summary>Hinged doors that can be used are pushed and pulled by hand; sliding and elevator doors keep their own controls (<see cref="CanInteract"/> gives their state).</summary>
        public bool Holdable(FirstPersonResponder responder) => Kind == DoorKind.Swing && Usable && responder != null && !responder.IsPaused;

        public void BeginHold(FirstPersonResponder responder)
        {
            holding = true;
            closeAt = -1;
            target = Open;
            // 겨냥한 문짝(두 짝 문이면 그쪽)이 손에 잡힌 문짝이다.
            holdLeaf = null;
            Leaf first = null;
            var aimed = responder.CurrentTargetCollider;
            foreach (var leaf in Leaves)
            {
                if (leaf.Pivot == null || Mathf.Abs(leaf.Swing) < 1) continue;
                if (first == null) first = leaf;
                if (aimed != null && aimed.transform.IsChildOf(leaf.Pivot)) { holdLeaf = leaf; break; }
            }
            if (holdLeaf == null) holdLeaf = first;
            // 관계자 문은 바깥에서 열쇠로 연다: 열리는 순간 한 번만 알린다.
            keyFeedbackPending = Use == DoorUse.Staff && Open <= .05f && Outside(responder);
        }

        public void Hold(FirstPersonResponder responder, Vector2 mouse, float wheel, float deltaSeconds)
        {
            if (!holding || holdLeaf == null || !Usable || responder.PlayerCamera == null) return;
            // 문짝 가운데가 열리며 지나는 방향을 시점 좌표로 바꾸어, 문짝을 끄는 쪽으로 마우스를 움직이면 그만큼 열린다(밀면 앞으로, 당기면 뒤로).
            var offset = holdLeaf.ClosedRotation * (Quaternion.Euler(0, holdLeaf.Swing * Open, 0) * (Vector3.right * (Mathf.Max(holdLeaf.Length, .2f) * .5f)));
            var direction = Equipment.HandGesture.SwingDirection(holdLeaf.Pivot.parent, offset, Mathf.Sign(holdLeaf.Swing));
            float next = Mathf.Clamp01(Open + Equipment.HandGesture.Along(responder.PlayerCamera.transform, direction, mouse) / DragDegreesForFullSwing);
            // 서 있는 사람(역무원) 쪽으로는 닿기 전까지만 열린다. 이미 닿는 자리라면 문짝이 천천히 밀려난다.
            float limit = SwingLimit();
            if (next > limit) next = Mathf.Max(limit, Mathf.MoveTowards(Open, limit, deltaSeconds / SwingCloseSeconds));
            float before = Open;
            if (Mathf.Approximately(next, before)) return;
            Open = next;
            Apply();
            Sounds(before);
            if (keyFeedbackPending && Open > .05f)
            {
                keyFeedbackPending = false;
                responder.ShowFeedback("열쇠로 문을 열었습니다");
            }
        }

        public string EndHold(FirstPersonResponder responder)
        {
            holding = false;
            holdLeaf = null;
            keyFeedbackPending = false;
            // 놓은 자리에서 도어 클로저가 시간을 잰다: 열려 있으면 CloserDelay 뒤 닫힌다.
            if (Open > .05f) { target = Open; closeAt = Time.time + CloserDelay; }
            else { target = 0; closeAt = -1; }
            return null;
        }

        /// <summary>How far the leaf has swung (0..1); no ring for the other kinds.</summary>
        public float HoldProgress => Kind == DoorKind.Swing ? Open : -1f;

        // ── 상태 ─────────────────────────────────────────────────────────────

        public string StateText
        {
            get
            {
                if (!Usable) return "";
                if (Kind == DoorKind.Sliding && Mode == OperatorMode.Locked && !alarmOpen) return "잠김";
                return Open >= .95f ? "열림" : Open <= .05f ? "닫힘" : Kind == DoorKind.Swing ? "반쯤 열림" : "여닫는 중";
            }
        }

        public string Observe(FirstPersonResponder responder)
        {
            if (!Usable) return Label + " · " + Sealed;
            switch (Kind)
            {
                case DoorKind.Swing:
                    return Label + " · " + (Open >= .95f ? "활짝 열려 있음" : Open <= .05f ? "닫혀 있음" : "반쯤 열려 있음") + (Use == DoorUse.Staff ? " · 관계자 출입문" : "");
                case DoorKind.Elevator:
                    return Label + " · " + (Open >= .95f ? "문이 열려 있음" : Open <= .05f ? "문이 닫혀 있음" : "문이 여닫히는 중");
                default:
                    return Label + " · " + SlidingState();
            }
        }
    }
}
