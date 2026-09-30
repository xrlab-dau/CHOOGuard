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
        public Rect StationMapBounds = new Rect(-80, -110, 180, 200);
        public string StationMapLabel = "2층 맞이방";
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

        /// <summary>Radio messages the wheel offers right now. Owners register providers; order is preserved.</summary>
        public readonly List<Func<IEnumerable<RadioOption>>> RadioProviders = new List<Func<IEnumerable<RadioOption>>>();
        /// <summary>Columns shown on the Tab board. Owners register providers.</summary>
        public readonly List<Func<BoardOverlay.Column>> BoardProviders = new List<Func<BoardOverlay.Column>>();
        /// <summary>Held-equipment slots after the radio. Owners register providers.</summary>
        public readonly List<Func<GameHud.Slot?>> SlotProviders = new List<Func<GameHud.Slot?>>();
        public string SituationText = "평시 근무 · 부산역 순회";
        public Color SituationColour = Color.white;

        public event Action Primary, PrimaryReleased, Drop;

        private readonly List<RadioOption> wheelOptions = new List<RadioOption>();
        private readonly List<BoardOverlay.Column> boardColumns = new List<BoardOverlay.Column>();
        private readonly List<GameHud.Slot> slots = new List<GameHud.Slot>();
        private bool wasPaused;

        public struct RadioOption
        {
            public string Label;
            public Action Send;
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
            // 튜토리얼 시절의 중앙 HUD 는 본게임 HUD 로 대체한다. 같은 플레이어 오브젝트를 두 HUD 가 동시에 그리지 않는다.
            var legacyHud = Player.GetComponent<FirstPersonInteractionHud>();
            // 역 씬이 먼저 불러와지면 옛 HUD 가 이미 캔버스를 만들었을 수 있다. 끄는 것만으로는 캔버스가 남으므로 지운다(OnDestroy 가 캔버스를 지운다).
            if (legacyHud != null) Destroy(legacyHud);
            EnsureEventSystem();

            Hud = GameHud.Create(transform, KoreanFont, Player);
            Board = BoardOverlay.Create(transform, KoreanFont);
            Map = MapOverlay.Create(transform, KoreanFont, StationMap, StationMapBounds, Player.PlayerCamera != null ? Player.PlayerCamera.transform : Player.transform, StationMapLabel);
            Wheel = RadioWheel.Create(transform, KoreanFont);
            Pause = PauseMenu.Create(transform, KoreanFont, Player, "근무 시작",
                "부산역 오후 근무입니다. 서울에서 KTX가 곧 5·6 타는 곳에 들어옵니다.\n승객들은 각자의 여정대로 움직입니다. 무슨 일이 언제, 어디서 일어날지는 정해져 있지 않습니다.\n이상을 발견하면 알리고, 사람들을 지키고, 도착한 기관에 인계하세요.");
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
            if (Wheel.Open)
            {
                Player.SuppressLookInput = true;
                if (mouse != null) Wheel.Steer(mouse.delta.ReadValue());
                if (!keyboard.qKey.isPressed)
                {
                    int chosen = Wheel.Close();
                    Player.SuppressLookInput = false;
                    if (chosen >= 0 && chosen < wheelOptions.Count) wheelOptions[chosen].Send?.Invoke();
                }
            }

            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) Primary?.Invoke();
                if (mouse.leftButton.wasReleasedThisFrame) PrimaryReleased?.Invoke();
            }
            if (keyboard.gKey.wasPressedThisFrame) Drop?.Invoke();
        }

        private void OpenWheel()
        {
            wheelOptions.Clear();
            foreach (var provider in RadioProviders) wheelOptions.AddRange(provider());
            var labels = new List<string>(wheelOptions.Count);
            foreach (var option in wheelOptions) labels.Add(option.Label);
            Wheel.Show(labels);
        }

        private void ShowBoard()
        {
            boardColumns.Clear();
            foreach (var provider in BoardProviders)
            {
                var column = provider();
                if (column != null) boardColumns.Add(column);
            }
            Board.Show(boardColumns);
        }

        private void RefreshSlots()
        {
            slots.Clear();
            slots.Add(new GameHud.Slot { Label = "무전기", Hint = "Q 누른 채 선택", Active = Wheel.Open });
            foreach (var provider in SlotProviders)
            {
                var slot = provider();
                if (slot.HasValue) slots.Add(slot.Value);
            }
            if (slots.Count == 1) slots.Add(new GameHud.Slot { Label = "빈손", Hint = "E 로 장비 들기" });
            Hud.SetSlots(slots);
        }

        /// <summary>Shared marker placement for compass and map.</summary>
        public void SetMarker(string id, Vector3 world, MarkerKind kind, string label)
        {
            Hud.Compass.SetMarker(id, world, kind);
            Map.SetMarker(id, world, kind, label);
        }

        public void RemoveMarker(string id)
        {
            Hud.Compass.RemoveMarker(id);
            Map.RemoveMarker(id);
        }
    }
}
