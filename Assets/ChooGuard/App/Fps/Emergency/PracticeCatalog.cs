using System;
using System.Linq;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>Which built-in lesson runs on top of a practice scenario (none: the ordinary product flow, one incident only).</summary>
    public enum PracticeLesson { None, Cardiac, Fire }

    /// <summary>
    /// One practice the "비상상황 연습" menu can start: the origin kinds (<c>Transition.Kind</c>) it lets happen, where, and how
    /// strongly. <see cref="Id"/> is what <see cref="PracticeShift.Arm"/> carries into the station.
    /// </summary>
    public sealed class PracticeScenario
    {
        public string Id, Title, Description;
        /// <summary>The origin kinds that may start this practice; the first candidate found wins.</summary>
        public string[] Kinds;
        /// <summary>Zone id prefixes the candidate must stand in, or null for anywhere in the station.</summary>
        public string[] Zones;
        /// <summary>The sentence the shift opens with: where and what will happen.</summary>
        public string Onset;
        public float Magnitude = .5f;
        public PracticeLesson Lesson;
        /// <summary>
        /// The order lesson for a scenario without a built-in lesson (docs/practice-lessons-plan.md): its own lesson when
        /// docs/emergency-lesson-steps.md has a detailed table for it, CommonOrderLesson otherwise. Null: no scoring.
        /// </summary>
        public Func<EmergencySession, PracticeOrderLesson> OrderLesson;
        /// <summary>How long the shift waits (after it settles) for a candidate. Train and elevator causes need the train or riders to come round.</summary>
        public float OfferSeconds = 90f;
        /// <summary>Why the scenario cannot happen in this build (shown greyed out in the menu), or null when it can.</summary>
        public string Unavailable;
        public bool Available => Unavailable == null;
        public int ExpectedMinSeconds = 180, ExpectedMaxSeconds = 420;
        public string Duration => $"약 {ExpectedMinSeconds / 60}~{ExpectedMaxSeconds / 60}분";

        /// <summary>The candidate of <paramref name="kind"/> standing in <paramref name="zone"/> (null when it stands nowhere in particular) may start this practice.</summary>
        public bool Accepts(string kind, string zone)
        {
            if (Kinds == null || Array.IndexOf(Kinds, kind) < 0) return false;
            if (Zones == null) return true;
            if (zone == null || StationWorld.IsOutdoorZone(zone)) return false;
            return Zones.Any(prefix => zone.StartsWith(prefix, StringComparison.Ordinal));
        }
    }

    /// <summary>A category of the "비상상황 연습" menu (화재, 환자 발생, …) and the scenarios under it.</summary>
    public sealed class PracticeGroup
    {
        public string Id, Title, Description;
        public PracticeScenario[] Scenarios;
    }

    /// <summary>
    /// The "비상상황 연습" menu, category → scenario (2026-11-18). Grouping and order follow docs/emergency-lesson-steps.md;
    /// every scenario names origin kinds the station already offers (IncidentDirector.*Origins), so nothing here invents an
    /// incident. A scenario without a lesson is the free shift's own flow with only that one incident composed.
    /// </summary>
    public static class PracticeCatalog
    {
        private static readonly string[] WaitingHall = { "hall2f", "main2f" };
        private const string Find = " 찾아서 확인(E)하고 역무실에 보고(Q)하십시오.";
        /// <summary>
        /// The equipment placement files (Art/Emergency/Equipment/*.json) were emptied on 2026-10-23: every procedural candidate
        /// without site evidence was removed, so no kitchen, electric-plaza, detector, sprinkler or shutter piece is placed and
        /// their causes are never offered. PracticeCatalogTests fails when one of them starts being offered again.
        /// </summary>
        public const string NoEquipment = "이 연습에 필요한 역 설비가 아직 준비되지 않았습니다. 다른 상황을 선택해 주세요.";
        /// <summary>
        /// Train and elevator causes depend on the KTX standing at 5·6 or riders in a running elevator. In the 2026-11-18 build
        /// PracticeCatalogTests watched a practice shift for 8 minutes and the station never offered one of them, so they stay shut
        /// until that is explained; the test fails as soon as one is offered.
        /// </summary>
        public const string NotOffered = "이 역의 열차·승강기 연습은 준비 중입니다. 다른 상황을 선택해 주세요.";

        public static readonly PracticeGroup[] Groups =
        {
            new PracticeGroup
            {
                Id = "fire", Title = "화재", Description = "역 안의 불과 연기를 확인하고 알린 뒤 초기 진화하고 소방대에 인계합니다.",
                Scenarios = new[]
                {
                    new PracticeScenario
                    {
                        Id = PracticeShift.FireFamily, Title = "맞이방 작은 불", Lesson = PracticeLesson.Fire,
                        Description = "2층 맞이방의 휴지통·보조배터리에서 난 작은 불. 신고(발신기·보고) → 승객 대피 유도 → 소화기 초기 진압 → 소방대 인계 순서를 채점합니다.",
                        Kinds = new[] { "bin_fire", "bin_battery_fire", "overheat" }, Zones = WaitingHall, Magnitude = .2f,
                        Onset = "잠시 뒤 2층 맞이방에서 작은 불이 납니다. 연기나 불을" + Find, ExpectedMinSeconds = 150, ExpectedMaxSeconds = 360,
                    },
                    new PracticeScenario
                    {
                        Id = "화재·전기설비", Title = "전기 설비 화재",
                        Description = "분전반·자판기·키오스크에서 난 불. 전기 담당에 전원 차단을 요청하고 물 대신 소화기로 대응합니다.",
                        Kinds = new[] { "board_fire", "vending_fire", "kiosk_fire" }, Magnitude = .2f, Unavailable = NoEquipment,
                        OrderLesson = PracticeLessons.Of(session => new ElectricFireLesson(session)),
                        Onset = "잠시 뒤 역 안의 전기 설비에서 불이 납니다. 연기나 불을" + Find,
                    },
                    new PracticeScenario
                    {
                        Id = "화재·주방기름", Title = "주방 기름 화재",
                        Description = "매장 주방 튀김기의 기름 불. 물을 쓰지 않고 가스를 잠근 뒤 주방용 소화기로 대응합니다.",
                        Kinds = new[] { "fryer_oil_fire" }, Magnitude = .2f, Unavailable = NoEquipment,
                        OrderLesson = PracticeLessons.Of(session => new KitchenFireLesson(session)),
                        Onset = "잠시 뒤 매장 주방의 튀김기에서 기름 불이 납니다. 연기나 불을" + Find,
                    },
                    new PracticeScenario
                    {
                        Id = "화재·주방조리기구", Title = "주방 가스레인지·오븐 화재",
                        Description = "매장 주방의 가스레인지나 오븐에서 난 불. 가스 차단과 초기 진화, 소방대 인계를 연습합니다.",
                        Kinds = new[] { "kitchen_fire", "oven_fire" }, Magnitude = .2f, Unavailable = NoEquipment,
                        OrderLesson = PracticeLessons.Of(session => new KitchenFireLesson(session)),
                        Onset = "잠시 뒤 매장 주방의 조리기구에서 불이 납니다. 연기나 불을" + Find,
                    },
                    new PracticeScenario
                    {
                        Id = "화재·열차하부", Title = "열차 하부 연기",
                        Description = "정차한 열차 바닥 아래에서 오르는 연기. 객차를 비우고 승강장을 통제한 뒤 소방대에 인계합니다.",
                        Kinds = new[] { "underfloor_smoke" }, Magnitude = .3f, OfferSeconds = 480, Unavailable = NotOffered,
                        OrderLesson = PracticeLessons.Of(session => new CommonOrderLesson(session, "열차 하부 연기")),
                        Onset = "열차가 들어와 서면 그 아래에서 연기가 오릅니다(최대 8분). 연기를" + Find,
                    },
                },
            },
            new PracticeGroup
            {
                Id = "patient", Title = "환자 발생", Description = "쓰러지거나 다친 승객을 확인하고 응급처치를 돕고 구급대에 인계합니다.",
                Scenarios = new[]
                {
                    new PracticeScenario
                    {
                        Id = PracticeShift.CardiacFamily, Title = "심정지 · AED", Lesson = PracticeLesson.Cardiac,
                        Description = "반응 확인, 도움 요청, 호흡 확인, 가슴압박, AED 사용, 구급대 인계까지 단계별로 배우고 순서를 채점합니다.",
                        Kinds = new[] { "chest_pain" }, Zones = WaitingHall, Magnitude = 1f,
                        Onset = "바로 앞 승객의 반응을 확인하며 시작합니다.", ExpectedMinSeconds = 240, ExpectedMaxSeconds = 480,
                    },
                    Patient("환자·쓰러짐", "쓰러짐", "collapse", "2층 맞이방에서 갑자기 쓰러진 승객. 반응과 호흡을 확인하고 119 구급을 부릅니다.", "승객이 쓰러집니다", session => new CollapseLesson(session)),
                    Patient("환자·경련", "경련", "seizure", "경련하는 승객. 주변 물건을 치우고 머리를 보호하며 경련 시간을 지켜봅니다.", "승객이 경련을 일으킵니다", session => new SeizureLesson(session)),
                    Patient("환자·가슴통증", "가슴 통증", "chest_pain", "가슴 통증을 호소하는 승객. 편한 자세로 쉬게 하고 증상과 시작 시각을 구급대에 전합니다.", "승객이 가슴 통증을 호소합니다", session => new ChestPainLesson(session)),
                    Patient("환자·호흡곤란", "호흡곤란", "breathing", "숨쉬기 힘들어하는 승객. 편한 자세와 맑은 공기를 확보하고 구급대를 부릅니다.", "승객이 숨쉬기 힘들어합니다", session => new BreathingLesson(session)),
                    Patient("환자·저혈당", "저혈당", "low_blood_sugar", "식은땀을 흘리며 어지러워하는 승객. 의식이 있으면 당을 주고 상태를 지켜봅니다.", "승객이 어지러워합니다", session => new LowBloodSugarLesson(session)),
                    new PracticeScenario
                    {
                        Id = "환자·넘어짐", Title = "계단·여행가방 넘어짐",
                        Description = "계단에서 넘어지거나 여행가방에 걸려 다친 승객. 움직이지 않게 하고 주변을 통제한 뒤 구급대에 인계합니다.",
                        Kinds = new[] { "stairs_fall", "suitcase_tumble" }, OfferSeconds = 180,
                        OrderLesson = PracticeLessons.Of(session => new FallLesson(session)),
                        Onset = "계단이나 맞이방에서 승객이 넘어져 다칩니다(최대 3분). 다친 승객을" + Find,
                    },
                    new PracticeScenario
                    {
                        Id = "환자·에스컬레이터", Title = "에스컬레이터 사고",
                        Description = "에스컬레이터에서 넘어지거나 끼인 승객. 비상정지 버튼으로 멈추고 승강기 담당과 구급대를 부릅니다.",
                        Kinds = new[] { "escalator_fall", "escalator_caught" }, OfferSeconds = 180,
                        OrderLesson = PracticeLessons.Of(session => new EscalatorAccidentLesson(session)),
                        Onset = "에스컬레이터에서 사고가 납니다(최대 3분). 다친 승객을" + Find,
                    },
                },
            },
            new PracticeGroup
            {
                Id = "security", Title = "보안", Description = "수상한 물건·협박·난동을 확인하고 통제한 뒤 철도경찰·경찰에 인계합니다.",
                Scenarios = new[]
                {
                    Security("보안·방치가방", "방치 가방", "bag_left", "주인 없이 남은 가방이나 소포. 만지지 않고 주변을 통제한 뒤 철도경찰에 알립니다.", "잠시 뒤 역 안에 주인 없는 가방이 남습니다. 가방을", session => new LeftBagLesson(session)),
                    new PracticeScenario
                    {
                        Id = "보안·폭발물협박", Title = "폭발물 협박",
                        Description = "역에 폭발물을 두었다는 협박 전화. 내용을 기록하고 112·철도경찰에 알린 뒤 대피를 준비합니다.",
                        Kinds = new[] { "bomb_threat" }, OrderLesson = PracticeLessons.Of(session => new BombThreatLesson(session)), Onset = "잠시 뒤 역무실에 폭발물 협박 전화가 걸려 옵니다. 무전을 듣고 역무실과 대응을 맞추십시오.",
                    },
                    Security("보안·의심물질", "의심 물질(흰 가루)", "suspicious_powder", "좌석에 뿌려진 흰 가루. 만지지 않고 접촉한 사람을 따로 모은 뒤 소방대에 인계합니다.", "잠시 뒤 역 안 좌석에 흰 가루가 발견됩니다. 가루를", session => new SuspiciousSubstanceLesson(session)),
                    Security("보안·난동", "난동 승객", "disturbance", "소리치며 위협하는 승객. 거리를 두고 주변 승객을 떨어뜨린 뒤 철도경찰을 부릅니다.", "잠시 뒤 역 안에서 승객이 난동을 부립니다. 상황을", session => new DisturbanceLesson(session)),
                },
            },
            new PracticeGroup
            {
                Id = "facility", Title = "시설 고장", Description = "승강기·전기·소방 설비·가스 이상을 확인하고 담당을 불러 조치한 뒤 인계합니다.",
                Scenarios = new[]
                {
                    Facility("시설·승강기갇힘", "승강기 갇힘", new[] { "elevator_trap" }, "멈춘 승강기에 갇힌 승객. 인터폰으로 안심시키고 승강기 담당을 부릅니다. 문을 억지로 열지 않습니다.", "승강기에 사람이 타면 멈춰 갇힙니다(최대 5분). 상황을", NotOffered, 300, session => new ElevatorTrapLesson(session)),
                    Facility("시설·정전", "정전", new[] { "power_outage" }, "역 전체 정전. 승객을 침착하게 안내하고 멈춘 승강기·에스컬레이터를 확인한 뒤 전기 담당을 부릅니다.", "잠시 뒤 역에 정전이 일어납니다. 상황을", lesson: session => new PowerOutageLesson(session)),
                    Facility("시설·비화재보", "화재 경보 오작동", new[] { "false_alarm" }, "불이 없는데 울린 감지기. 현장에서 불이 아님을 확인한 뒤 수신기를 복구합니다.", "잠시 뒤 화재 감지기가 울립니다. 현장을", NoEquipment, lesson: session => new FalseAlarmLesson(session)),
                    Facility("시설·낙하물", "낙하물", new[] { "falling_object" }, "흔들리거나 떨어진 표지판·마감재. 아래를 통제하고 시설 담당을 부릅니다.", "잠시 뒤 천장 시설물이 흔들립니다. 위험한 곳을", lesson: session => new FallingObjectLesson(session)),
                    Facility("시설·누수", "누수·스프링클러 살수", new[] { "water_leak", "sprinkler_discharge" }, "배관 누수나 스프링클러 살수. 미끄러운 바닥을 통제하고 전기 설비를 살핀 뒤 시설 담당을 부릅니다.", "잠시 뒤 역 안에서 물이 샙니다. 물이 새는 곳을", NoEquipment, lesson: session => new WaterLeakLesson(session)),
                    Facility("시설·방화셔터", "방화셔터 오동작", new[] { "shutter_malfunction" }, "불이 없는데 내려오는 방화셔터. 셔터 아래 승객을 비키게 하고 시설 담당을 부릅니다.", "잠시 뒤 방화셔터가 오동작합니다. 셔터를", NoEquipment, lesson: session => new ShutterFaultLesson(session)),
                    Facility("시설·가스누출", "가스 누출", new[] { "gas_hose_off", "gas_cock_open", "gas_meter_leak" }, "매장 주방의 가스 냄새. 불꽃을 만들지 않고 밸브를 잠근 뒤 환기하고 소방대·도시가스를 부릅니다.", "잠시 뒤 매장 주방에서 가스가 샙니다. 냄새 나는 곳을", NoEquipment, lesson: session => new GasLeakLesson(session)),
                },
            },
            new PracticeGroup
            {
                Id = "train", Title = "열차·선로", Description = "승강장과 열차에서 생긴 사고를 확인하고 열차를 멈춰 세운 뒤 인계합니다.",
                Scenarios = new[]
                {
                    new PracticeScenario
                    {
                        Id = "열차·출입문끼임", Title = "출입문 끼임",
                        Description = "열차 출입문에 끼인 승객. 열차팀에 알려 출발을 막고 문을 열게 합니다.",
                        Kinds = new[] { "door_trap" }, OfferSeconds = 480, Unavailable = NotOffered,
                        OrderLesson = PracticeLessons.Of(session => new CommonOrderLesson(session, "출입문 끼임")), Onset = "열차 출입문이 닫힐 때 늦게 타는 승객이 끼입니다(최대 8분). 상황을" + Find,
                    },
                    new PracticeScenario
                    {
                        Id = "열차·선로추락", Title = "선로 추락·진입",
                        Description = "선로로 떨어지거나 내려간 사람. 열차 진입을 막고 직접 내려가지 않은 채 구조를 요청합니다.",
                        Kinds = new[] { "track_fall", "track_climb" }, OfferSeconds = 480, Unavailable = NotOffered,
                        OrderLesson = PracticeLessons.Of(session => new CommonOrderLesson(session, "선로 추락·진입")), Onset = "승강장에 승객이 모이면 한 사람이 선로로 내려갑니다(최대 8분). 상황을" + Find,
                    },
                },
            },
            new PracticeGroup
            {
                Id = "quake", Title = "지진", Description = "흔들림 중에 몸을 보호하고, 멈춘 뒤 피해를 확인하고 승객을 안내합니다.",
                Scenarios = new[]
                {
                    new PracticeScenario
                    {
                        Id = "지진", Title = "지진",
                        Description = "역 전체가 흔들리는 지진. 흔들림이 멈춘 뒤 피해와 승강기를 확인하고 승객을 넓은 곳으로 안내합니다.",
                        Kinds = new[] { "quake" }, OrderLesson = PracticeLessons.Of(session => new EarthquakeLesson(session)), Onset = "잠시 뒤 지진이 일어납니다. 흔들림이 멈추면 피해를 확인하고 역무실에 보고(Q)하십시오.",
                    },
                },
            },
        };

        /// <summary>The scenario <paramref name="id"/> names, or null.</summary>
        public static PracticeScenario Scenario(string id) =>
            id == null ? null : Groups.SelectMany(group => group.Scenarios).FirstOrDefault(scenario => scenario.Id == id);

        public static PracticeGroup Group(string id) => Groups.FirstOrDefault(group => group.Id == id);

        public static PracticeGroup GroupOf(string scenarioId) =>
            Groups.FirstOrDefault(group => group.Scenarios.Any(scenario => scenario.Id == scenarioId));

        /// <summary>The category the last practice was started from; the menu reopens on it when a practice returns to the list.</summary>
        public static string LastGroupId;

        private static Func<EmergencySession, PracticeOrderLesson> Wrap(Func<EmergencySession, PracticeOrderLesson> lesson) =>
            lesson == null ? null : PracticeLessons.Of(lesson);

        private static PracticeScenario Patient(string id, string title, string kind, string description, string what, Func<EmergencySession, PracticeOrderLesson> lesson) => new PracticeScenario
        {
            Id = id, Title = title, Description = description, Kinds = new[] { kind }, Zones = WaitingHall, OrderLesson = Wrap(lesson),
            Onset = "잠시 뒤 2층 맞이방에서 " + what + ". 승객의 알림이나 순찰로" + Find,
        };

        private static PracticeScenario Security(string id, string title, string kind, string description, string onset, Func<EmergencySession, PracticeOrderLesson> lesson) => new PracticeScenario
        {
            Id = id, Title = title, Description = description, Kinds = new[] { kind }, Onset = onset + Find, OrderLesson = Wrap(lesson),
        };

        private static PracticeScenario Facility(string id, string title, string[] kinds, string description, string onset, string unavailable = null, float offer = 90f, Func<EmergencySession, PracticeOrderLesson> lesson = null) => new PracticeScenario
        {
            Id = id, Title = title, Description = description, Kinds = kinds, Onset = onset + Find, Unavailable = unavailable, OfferSeconds = offer, OrderLesson = Wrap(lesson),
        };
    }
}
