using System;
using System.Collections.Generic;
using ChooGuard.App.Fps.Hud;
using ChooGuard.App.Fps.Shell;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Root of the main-game shift, loaded additively on top of FpsStation. Owns the HUD, menus and player
    /// input beyond movement. Tutorial code is never referenced from here (and vice versa).
    /// </summary>
    public sealed class EmergencySession : MonoBehaviour
    {
        [Header("화면")]
        public TMP_FontAsset KoreanFont;
        public Texture StationMap;
        [Tooltip("지도 텍스처가 덮는 월드 XZ 범위 (x, z, 폭, 깊이)")]
        public Rect StationMapBounds;
        public string StationMapLabel = "부산역 전역";
        [Header("근무")]
        [Tooltip("근무 시작 시각 (시)")]
        public float ShiftStartHour = 14;
        [Tooltip("승객·사건 난수 시드. 0 이면 매 근무 다르게 정한다.")]
        public int Seed;

        /// <summary>Seed for the next session to start (automated runs make shifts repeatable); 0 leaves <see cref="Seed"/>.</summary>
        public static int NextSeed;
        [Header("세계")]
        public EmergencyArt Art;
        public int Passengers = 110;

        public static EmergencySession Current { get; private set; }
        public FirstPersonResponder Player { get; private set; }
        public GameHud Hud { get; private set; }
        public PauseMenu Pause { get; private set; }
        public BoardOverlay Board { get; private set; }
        public MapOverlay Map { get; private set; }
        public RadioWheel Wheel { get; private set; }
        public float ShiftSeconds { get; private set; }
        public StationWorld World { get; private set; }
        public CrowdDirector Crowd { get; private set; }
        public IncidentDirector Incidents { get; private set; }
        public StaffHands Hands { get; private set; }
        public JevClient Jev { get; private set; }
        public TrainService Train { get; private set; }
        public ShiftLog Log { get; private set; }
        public StationSound Sound { get; private set; }

        /// <summary>Radio messages the wheel offers right now in the guided shift (견학). Owners register providers; order is preserved.</summary>
        public readonly List<Func<IEnumerable<RadioOption>>> RadioProviders = new List<Func<IEnumerable<RadioOption>>>();
        /// <summary>
        /// The radio in 표준 and 실전: groups (보고, 요청, 방송, 응답) that always offer the same kinds of message, right or wrong for the
        /// moment. Q held shows the groups, a left click opens one, a right click goes back, letting go of Q sends.
        /// </summary>
        public readonly List<Func<IEnumerable<RadioGroup>>> RadioGroupProviders = new List<Func<IEnumerable<RadioGroup>>>();
        /// <summary>Columns shown on the Tab board. Owners register providers.</summary>
        public readonly List<Func<BoardOverlay.Column>> BoardProviders = new List<Func<BoardOverlay.Column>>();
        /// <summary>Held-equipment slots after the radio. Owners register providers.</summary>
        public readonly List<Func<GameHud.Slot?>> SlotProviders = new List<Func<GameHud.Slot?>>();
        public string SituationText = "평시 근무 · 부산역 순회";
        public Color SituationColour = Color.white;

        /// <summary>What the route guidance shows now (<see cref="RefreshGuide"/> sets it every couple of seconds and when the setting changes).</summary>
        public enum GuideState { Off, Unaware, NoTarget, Arrived, Blocked, Guiding }
        public GuideState GuideStatus { get; private set; } = GuideState.Unaware;
        /// <summary>The line the map shows under its title for <see cref="GuideStatus"/>.</summary>
        public string GuideMessage { get; private set; } = "";
        /// <summary>The hazard being guided to while <see cref="GuideStatus"/> is <see cref="GuideState.Guiding"/>, <see cref="GuideState.Arrived"/> or <see cref="GuideState.Blocked"/>.</summary>
        public Hazard GuideTarget { get; private set; }
        /// <summary>The floor points of the route shown (empty unless guiding).</summary>
        public IReadOnlyList<Vector3> GuidePath => guideRoute;
        public event Action Primary, PrimaryReleased, Drop;
        /// <summary>The player looked closely (right mouse held): the collider looked at, or null when nothing solid is within reach; the director answers what is seen.</summary>
        public Func<Collider, string> Observer;

        /// <summary>How long the right mouse is held on the same thing before it is looked at closely (s), and how far looking closely reaches (m).</summary>
        public const float ObserveSeconds = .75f, ObserveReachMetres = 6f;
        private const float PingReach = 60f;

        private readonly List<RadioOption> wheelOptions = new List<RadioOption>();
        private readonly List<BoardOverlay.Column> boardColumns = new List<BoardOverlay.Column>();
        private readonly List<GameHud.Slot> slots = new List<GameHud.Slot>();
        private bool wasPaused;
        private float nextGuideRefresh;
        private readonly List<Vector3> guideRoute = new List<Vector3>();
        private WorldRouteGuide worldGuide;
        private GuideRoute guide;
        private readonly List<RadioGroup> wheelGroups = new List<RadioGroup>();
        private readonly List<string> wheelLabels = new List<string>();
        private int wheelGroup = -1;
        private bool wheelGrouped;
        private Collider observing;
        private float observeHeld;
        private bool observed;
        private Vector3? pinned;
        private readonly RaycastHit[] sightHits = new RaycastHit[16];

        public struct RadioOption
        {
            public string Label;
            public Action Send;
        }

        public struct RadioGroup
        {
            public string Label;
            public List<RadioOption> Options;
        }

        private void Awake()
        {
            if (Current != null && Current != this) { Destroy(gameObject); return; }
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            // 역무원(역 씬)이 근무 씬보다 오래 남으면 끊긴 캔버스로 일시정지 알림이 온다.
            if (Player != null) Player.PauseChanged -= OnPauseChanged;
            GameSettings.RouteChanged -= OnSettingsChanged;
            GameSettings.Changed -= OnOptionsChanged;
            World?.Dispose();
            HazardRegistry.Clear();
            Facilities.StationSignals.Clear();
            Time.timeScale = 1;
        }

        private void Start()
        {
            if (KoreanFont == null) { Debug.LogError("[EmergencySession] 한국어 폰트가 없습니다.", this); enabled = false; return; }
            Player = FindFirstObjectByType<FirstPersonResponder>();
            if (Player == null) { Debug.LogError("[EmergencySession] FpsStation 의 역무원(FirstPersonResponder)을 찾지 못했습니다.", this); enabled = false; return; }
            GameSettings.Apply(Player);
            GameSettings.Changed += OnOptionsChanged;
            // 튜토리얼 시절의 중앙 HUD 는 본게임 HUD 로 대체한다. 같은 플레이어 오브젝트를 두 HUD 가 동시에 그리지 않는다.
            var legacyHud = Player.GetComponent<FirstPersonInteractionHud>();
            // 역 씬이 먼저 불러와지면 옛 HUD 가 이미 캔버스를 만들었을 수 있다. 끄는 것만으로는 캔버스가 남으므로 지운다(OnDestroy 가 캔버스를 지운다).
            if (legacyHud != null) Destroy(legacyHud);
            EnsureEventSystem();

            Hud = GameHud.Create(transform, KoreanFont, Player);
            worldGuide = WorldRouteGuide.Create(transform);
            Board = BoardOverlay.Create(transform, KoreanFont);
            Map = MapOverlay.Create(transform, KoreanFont, StationMap, StationMapBounds, Player.PlayerCamera != null ? Player.PlayerCamera.transform : Player.transform, StationMapLabel);
            Wheel = RadioWheel.Create(transform, KoreanFont);
            Pause = PauseMenu.Create(transform, KoreanFont, Player, "근무 시작",
                "부산역 오후 근무입니다. 서울에서 KTX가 곧 5·6 타는 곳에 들어옵니다.\n승객들은 각자의 여정대로 움직입니다. 무슨 일이 언제, 어디서 일어날지는 정해져 있지 않습니다.\n이상을 발견하면 알리고, 사람들을 지키고, 도착한 기관에 인계하세요.\n" +
                "<size=15>안내 수준 " + GameSettings.Label(GameSettings.Guidance) + " · " + GameSettings.Describe(GameSettings.Guidance) + " (설정에서 바꿀 수 있습니다)</size>");
            Player.PauseChanged += OnPauseChanged;
            RadioProviders.Add(RoutineReports);
            OnPauseChanged(Player.IsPaused);
            StartWorld();
        }

        private void StartWorld()
        {
            if (Art == null) { Debug.LogError("[EmergencySession] EmergencyArt 가 연결되지 않았습니다. ChooGuard/Emergency/Build session scene and title 을 실행하세요.", this); return; }
            int seed = NextSeed != 0 ? NextSeed : Seed != 0 ? Seed : Environment.TickCount & 0x7fffffff;
            NextSeed = 0;
            World = new StationWorld(Art, seed, transform);
            Map.SetLandmarks(World.Points);
            GameSettings.RouteChanged += OnSettingsChanged;
            Facilities.StationSignals.FireAlarm = false;
            Facilities.StationSignals.DoorOpen = Art.DoorOpen;
            Facilities.StationSignals.DoorClose = Art.DoorClose;
            PlayerView.Camera = Player.PlayerCamera;
            if (World.Points.Train != null)
            {
                Train = gameObject.AddComponent<TrainService>();
                Train.Setup(this, World.Points.Train, Art);
                World.Train = Train;
            }
            Log = new ShiftLog(this);
            var logFolder = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "jev-runs");
            System.IO.Directory.CreateDirectory(logFolder);
            Jev = new JevClient(System.IO.Path.Combine(logFolder, "jev-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".jsonl"));
            StartCoroutine(Jev.Warm());
            // 승객이 역무원을 사람으로 피해 가도록 한다(길찾기 장애물, 길을 깎지는 않는다).
            var obstacle = Player.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
            obstacle.radius = .3f;
            obstacle.height = 1.8f;
            obstacle.center = new Vector3(0, .9f, 0);
            obstacle.carving = false;
            Hands = gameObject.AddComponent<StaffHands>();
            Hands.Setup(this, Art);
            Incidents = gameObject.AddComponent<IncidentDirector>();
            Crowd = gameObject.AddComponent<CrowdDirector>();
            Crowd.Target = Passengers;
            Incidents.Begin(this, World, Crowd, Jev, Art, Log);
            Crowd.Begin(this, World, Art.Crowd, Jev);
            Sound = gameObject.AddComponent<StationSound>();
            Sound.Setup(this);
            gameObject.AddComponent<StationSoundscape>().Setup(this);
            RefreshGuide();
            Log.Add("근무 시작 · 부산역 (시드 " + seed + ", " + Jev.Status + ")");
            Debug.Log("CG_SHIFT_START seed=" + seed + " passengers=" + Crowd.People.Count + " jev=" + Jev.Status);
        }

        /// <summary>A public-address announcement: the text in the feed, the chime and its pre-rendered voice.</summary>
        public void Announce(PaLine line, string text)
        {
            if (Sound != null) Sound.Announce(line, text);
            else Hud.Radio.Push(RadioChannel.Announcement, text);
        }

        /// <summary>Clock text for a moment of the shift (seconds since shift start).</summary>
        public string Clock(float shiftSeconds)
        {
            float hours = ShiftStartHour + shiftSeconds / 3600f;
            int h = Mathf.FloorToInt(hours) % 24, m = Mathf.FloorToInt((hours - Mathf.Floor(hours)) * 60), s = Mathf.FloorToInt(shiftSeconds % 60);
            return h.ToString("00") + ":" + m.ToString("00") + ":" + s.ToString("00");
        }

        /// <summary>Stops play for the end screen: the pause menu stays hidden and the controller stays paused.</summary>
        public void EndShift()
        {
            Pause.Suppressed = true;
            if (Wheel.Open) Wheel.Close();
            Board.Hide();
            Map.Hide();
            Player.Pause();
        }

        private IEnumerable<RadioOption> RoutineReports()
        {
            if (Incidents != null && Incidents.PlayerKnowsIncident) yield break;
            yield return new RadioOption
            {
                Label = "역무실 · 순회 중 이상 없음",
                Send = () =>
                {
                    Hud.Radio.Push(RadioChannel.Self, "역무실, " + World.Describe(Player.transform.position) + " 순회 중 이상 없습니다.");
                    Hud.Radio.Push(RadioChannel.Office, "역무실 수신. 계속 순회 바랍니다.");
                },
            };
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var events = new GameObject("근무 EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void OnPauseChanged(bool paused)
        {
            Time.timeScale = paused ? 0 : 1;
            if (paused)
            {
                if (Wheel.Open) Wheel.Close();
                Board.Hide();
                Map.Hide();
            }
        }

        private void Update()
        {
            if (Player == null) return;
            if (World != null && Time.unscaledTime >= nextGuideRefresh)
            {
                nextGuideRefresh = Time.unscaledTime + 2f;
                RefreshGuide();
            }
            ShiftSeconds += Time.deltaTime;
            float hours = ShiftStartHour + ShiftSeconds / 3600f;
            int h = Mathf.FloorToInt(hours) % 24, m = Mathf.FloorToInt((hours - Mathf.Floor(hours)) * 60);
            Hud.SetStatus(h.ToString("00") + ":" + m.ToString("00"), SituationText, SituationColour);
            RefreshSlots();

            bool paused = Player.IsPaused;
            if (paused) { wasPaused = true; return; }
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null) return;
            // 재개 클릭이 곧바로 '장비 사용'이 되지 않도록 한 프레임 쉰다.
            if (wasPaused) { wasPaused = false; return; }

            if (keyboard.tabKey.isPressed) ShowBoard(); else if (Board.Visible) Board.Hide();
            if (keyboard.mKey.wasPressedThisFrame) Map.Toggle();

            if (keyboard.qKey.wasPressedThisFrame && !Wheel.Open) OpenWheel();
            bool wheelClick = false;
            if (Wheel.Open)
            {
                Player.SuppressLookInput = true;
                if (mouse != null) Wheel.Steer(mouse.delta.ReadValue());
                if (wheelGrouped && mouse != null)
                {
                    // 묶음 무전: 좌클릭이 묶음을 열고 우클릭이 묶음 목록으로 돌아간다(장비 사용으로 넘어가지 않는다).
                    if (mouse.leftButton.wasPressedThisFrame) { wheelClick = true; if (wheelGroup < 0 && Wheel.Selected >= 0 && Wheel.Selected < wheelGroups.Count) ShowWheelGroup(Wheel.Selected); }
                    if (mouse.rightButton.wasPressedThisFrame && wheelGroup >= 0) ShowWheelGroups();
                }
                if (!keyboard.qKey.isPressed)
                {
                    int chosen = Wheel.Close();
                    Player.SuppressLookInput = false;
                    if (!wheelGrouped) { if (chosen >= 0 && chosen < wheelOptions.Count) wheelOptions[chosen].Send?.Invoke(); }
                    else if (wheelGroup >= 0 && chosen >= 0 && chosen < wheelGroups[wheelGroup].Options.Count) wheelGroups[wheelGroup].Options[chosen].Send?.Invoke();
                    wheelGroup = -1;
                }
            }

            if (mouse != null)
            {
                // 묶음 무전의 좌클릭은 묶음을 여는 데 쓰였다(장비 사용으로 넘기지 않는다).
                if (mouse.leftButton.wasPressedThisFrame && !wheelClick) Primary?.Invoke();
                if (mouse.leftButton.wasReleasedThisFrame) PrimaryReleased?.Invoke();
                Look(mouse.rightButton.isPressed && !Wheel.Open && !Player.Holding);
                if (mouse.middleButton.wasPressedThisFrame && !Wheel.Open) Ping();
            }
            if (keyboard.gKey.wasPressedThisFrame) Drop?.Invoke();
        }

        /// <summary>
        /// Right mouse held on the same thing for <see cref="ObserveSeconds"/> looks at it closely: the director answers only what a person standing here can
        /// see, and the notebook keeps it. The nearest solid thing in sight counts (no looking through walls); open view ahead counts as one thing too.
        /// </summary>
        private void Look(bool held)
        {
            if (!held || Observer == null) { observing = null; observeHeld = 0; observed = false; Hud.SetObserveProgress(null); return; }
            var target = Sighted(ObserveReachMetres);
            if (target != observing) { observing = target; observeHeld = 0; observed = false; }
            if (observed) { Hud.SetObserveProgress(null); return; }
            observeHeld += Time.deltaTime;
            Hud.SetObserveProgress(observeHeld / ObserveSeconds);
            if (observeHeld < ObserveSeconds) return;
            observed = true;
            Hud.SetObserveProgress(null);
            var text = Observer(observing);
            if (!string.IsNullOrEmpty(text)) Hud.ShowObservation(text);
        }

        /// <summary>The middle mouse marks the spot looked at on the compass (the player's own pin, one at a time; again on it removes it).</summary>
        private void Ping()
        {
            var camera = Player.PlayerCamera;
            if (camera == null || !Physics.Raycast(camera.transform.position, camera.transform.forward, out var hit, PingReach, ~0, QueryTriggerInteraction.Ignore)) return;
            if (pinned.HasValue && Vector3.Distance(pinned.Value, hit.point) < 1.5f) { pinned = null; RemoveMarker("ping"); return; }
            pinned = hit.point;
            Hud.Compass.SetMarker("ping", hit.point, MarkerKind.Task);
            Map.SetMarker("ping", hit.point, MarkerKind.Task, "내 표시");
        }

        /// <summary>The nearest solid collider straight ahead within <paramref name="reach"/>, ignoring the player's own body.</summary>
        private Collider Sighted(float reach)
        {
            var camera = Player.PlayerCamera;
            if (camera == null) return null;
            int count = Physics.RaycastNonAlloc(camera.transform.position, camera.transform.forward, sightHits, reach, ~0, QueryTriggerInteraction.Ignore);
            Collider best = null;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var collider = sightHits[i].collider;
                if (collider == null || collider.transform.IsChildOf(Player.transform) || sightHits[i].distance >= nearest) continue;
                nearest = sightHits[i].distance;
                best = collider;
            }
            return best;
        }

        private void OnOptionsChanged()
        {
            GameSettings.Apply(Player);
            Hud.RefreshHints();
            nextGuideRefresh = 0;
        }

        private void OnSettingsChanged()
        {
            nextGuideRefresh = 0;
            RefreshGuide();
        }

        /// <summary>
        /// Recomputes what the map, the compass and the floor marks show for the route to the incident now. It uses only what the player knows
        /// (<see cref="IncidentDirector.GiveKnownObstructions"/>) and the baked station (<see cref="GuideRoute"/>), so an unseen hazard or closure
        /// changes nothing on screen, and it never draws from the world's random numbers.
        /// </summary>
        public void RefreshGuide()
        {
            if (Map == null || Hud == null || World == null || Incidents == null || Player == null) return;
            GuideTarget = null;
            guideRoute.Clear();
            if (!GameSettings.ShowRoute) { ShowGuide(GuideState.Off, "길 안내 꺼짐 · 설정에서 켤 수 있습니다"); return; }
            if (!Incidents.PlayerKnowsIncident) { ShowGuide(GuideState.Unaware, "사고를 인지하면 이동 안내가 나타납니다"); return; }
            if (!Incidents.TryGetKnownGuideTarget(out var target)) { ShowGuide(GuideState.NoTarget, "이동해서 확인할 사고 현장이 없습니다 · 역 전체에 걸친 상황이거나 이미 정리되었습니다"); return; }
            GuideTarget = target;
            guide ??= new GuideRoute(World);
            Incidents.GiveKnownObstructions(guide);
            var outcome = guide.Plan(Player.transform.position, target, guideRoute);
            if (outcome == GuideRoute.Outcome.Arrived) { ShowGuide(GuideState.Arrived, target.Where + " " + target.Label + " 접근 지점에 도착했습니다"); return; }
            if (outcome == GuideRoute.Outcome.NoRoute) { ShowGuide(GuideState.Blocked, "알고 있는 정보로는 통행 가능한 현장 접근 경로를 확인하지 못했습니다"); return; }
            Vector3 next = guideRoute[guideRoute.Count - 1];
            for (int i = 1; i < guideRoute.Count; i++)
            {
                if (Vector3.Distance(guideRoute[0], guideRoute[i]) < 4f &&
                    MapOverlay.Floor(guideRoute[0]) == MapOverlay.Floor(guideRoute[i])) continue;
                next = guideRoute[i];
                break;
            }
            float remaining = 0;
            for (int i = 1; i < guideRoute.Count; i++) remaining += Vector3.Distance(guideRoute[i - 1], guideRoute[i]);
            string floor = MapOverlay.Floor(target.Scene) != MapOverlay.Floor(Player.transform.position)
                ? " · 목적지 " + Map.FloorLabel(target.Scene) : "";
            string transfer = MapOverlay.Floor(next) != MapOverlay.Floor(Player.transform.position)
                ? " · 다음 " + Map.FloorLabel(next) + " 연결 지점" : "";
            GuideStatus = GuideState.Guiding;
            GuideMessage = target.Where + " " + target.Label + " 접근 · 약 " + Mathf.RoundToInt(remaining) + "m" + floor + transfer;
            Map.SetRoute(guideRoute, GuideMessage);
            // 나침반 표식은 지우지 않고 제자리에서 옮긴다(2초마다 UI 오브젝트를 만들고 부수지 않는다).
            Hud.Compass.SetMarker("guide-next", next, MarkerKind.Guidance);
            worldGuide?.SetRoute(guideRoute);
        }

        /// <summary>No route to show: the map says why, the compass mark and the floor marks go.</summary>
        private void ShowGuide(GuideState state, string message)
        {
            GuideStatus = state;
            GuideMessage = message;
            Hud.Compass.RemoveMarker("guide-next");
            worldGuide?.Clear();
            Map.SetRoute(null, message);
        }

        private void OpenWheel()
        {
            wheelGroup = -1;
            wheelGrouped = !GameSettings.Guided && RadioGroupProviders.Count > 0;
            if (wheelGrouped)
            {
                wheelGroups.Clear();
                foreach (var provider in RadioGroupProviders)
                    foreach (var group in provider())
                        if (group.Options != null && group.Options.Count > 0) wheelGroups.Add(group);
                ShowWheelGroups();
                return;
            }
            wheelOptions.Clear();
            foreach (var provider in RadioProviders) wheelOptions.AddRange(provider());
            wheelLabels.Clear();
            foreach (var option in wheelOptions) wheelLabels.Add(option.Label);
            Wheel.Show(wheelLabels);
        }

        private void ShowWheelGroups()
        {
            wheelGroup = -1;
            wheelLabels.Clear();
            foreach (var group in wheelGroups) wheelLabels.Add(group.Label);
            Wheel.Show(wheelLabels, "무전\n<size=11>방향 고른 뒤\n좌클릭 열기</size>");
        }

        private void ShowWheelGroup(int index)
        {
            wheelGroup = index;
            wheelLabels.Clear();
            foreach (var option in wheelGroups[index].Options) wheelLabels.Add(option.Label);
            Wheel.Show(wheelLabels, wheelGroups[index].Label + "\n<size=11>떼면 보냄\n우클릭 뒤로</size>");
        }

        private void ShowBoard()
        {
            boardColumns.Clear();
            foreach (var provider in BoardProviders)
            {
                var column = provider();
                if (column != null) boardColumns.Add(column);
            }
            Board.Show(boardColumns, GameSettings.Guided ? "근무 상황판" : "수첩");
        }

        private void RefreshSlots()
        {
            slots.Clear();
            slots.Add(new GameHud.Slot { Label = "무전기", Hint = GameSettings.Guided ? "Q 누른 채 선택" : "Q 누른 채 · 좌클릭 열기", Active = Wheel.Open });
            foreach (var provider in SlotProviders)
            {
                var slot = provider();
                if (slot.HasValue) slots.Add(slot.Value);
            }
            if (slots.Count == 1) slots.Add(new GameHud.Slot { Label = "빈손", Hint = "E 로 장비 들기" });
            Hud.SetSlots(slots);
        }

        /// <summary>
        /// Shared marker placement for compass and map. The map shows every mark; the compass carries them only in the guided shift (견학). In 표준 the compass
        /// gets the player's own pin and the hazards they reported (<see cref="IncidentDirector"/> puts those there); in 실전 only the pin.
        /// </summary>
        public void SetMarker(string id, Vector3 world, MarkerKind kind, string label)
        {
            if (GameSettings.Guided) Hud.Compass.SetMarker(id, world, kind);
            Map.SetMarker(id, world, kind, label);
        }

        public void RemoveMarker(string id)
        {
            Hud.Compass.RemoveMarker(id);
            Map.RemoveMarker(id);
        }
    }
}
