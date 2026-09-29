using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// An elevator stops between floors with people inside; they call the office on the car's emergency intercom. Nobody
    /// forces the doors: the maintenance contractor or facility staff bring the car to a floor (rescue run), and 119 is
    /// called when someone inside is unwell (research.md). <see cref="Level"/>: how the people inside cope.
    /// </summary>
    public sealed class ElevatorTrapHazard : Hazard
    {
        private static readonly string[] Seen =
        {
            "엘리베이터가 층 사이에 멈춤", "엘리베이터가 층 사이에 멈춰 승객이 갇힘", "갇힌 승객들이 비상통화로 계속 호출함",
            "갇힌 승객 한 명이 불안해하며 문을 두드림", "갇힌 승객 한 명이 숨이 막힌다며 주저앉음",
        };

        public Elevator Elevator { get; }
        public int Riders { get; }
        public int Level { get; private set; }
        public bool Reassured { get; set; }

        public override string Label => "엘리베이터 갇힘";
        public override float NoticeRadius => Active ? 5 : 0;
        public override bool NeedsSight => false;
        public override bool Localized => true;
        public override string SensedAs => "엘리베이터 안에서 두드리는 소리를 들음";
        public override float DangerRadius => 0;
        public override float Clearance => 0;
        public override string Visible => (Active ? Seen[Level] : "구출 운전으로 승객이 내림") + " · 안에 " + Riders + "명";

        public override Agency Command => Agency.Facility;
        public override bool Involves(Agency agency) => agency == Agency.Facility || Level >= 3 && agency == Agency.Fire;
        public override string ReportOption => Elevator.Label + " 갇힘 확인 보고";
        public override string ReportLine => "역무실, " + Elevator.Label + " 앞입니다. 안에 " + Riders + "명 갇혀 있습니다." + (Level >= 3 ? " 한 명이 많이 힘들어합니다." : " 안에서는 괜찮다고 합니다.");
        public override string OfficeReply => "역무실 수신. 승강기 유지관리업체와 시설 담당 부르겠습니다." + (Level >= 3 ? " 119에도 구조 요청하겠습니다." : "") + " 문을 억지로 열지 말고 비상통화로 계속 안심시켜 주십시오.";
        public override IEnumerable<Agency> Dispatch
        {
            get
            {
                yield return Agency.Facility;
                if (Level >= 3) yield return Agency.Fire;
            }
        }
        public override PaRequest Announcement => new PaRequest("안내방송(승강기 점검) 요청", PaLine.ElevatorCheck,
            "안내 말씀 드립니다. " + Elevator.Label + KoreanText.Object(Elevator.Label) + " 점검하고 있습니다. 옆 계단과 에스컬레이터를 이용해 주십시오.", PaScope.Inform);
        public override string State => Active ? "갇혀 있음" : "구출됨";
        public override string Handover => Elevator.Label + (Active ? " " + Riders + "명 갇힌 채" : " 갇힌 승객 구출") + (Reassured ? " · 비상통화로 안심시킴" : "");
        public override float WorkSeconds(Agency agency) => agency == Agency.Facility ? 25 : 0;

        public override string Resolve(Agency agency)
        {
            End();
            Elevator.Resume();
            return "시설 담당입니다. " + Elevator.Label + " 구출 운전으로 가까운 층에 세워 승객을 내리게 했습니다.";
        }

        public ElevatorTrapHazard(string id, Elevator elevator, Vector3 landing, int riders, int level)
        {
            Id = id;
            Kind = HazardKind.ElevatorTrap;
            Elevator = elevator;
            Position = landing;
            Riders = riders;
            Level = Mathf.Clamp(level, 0, 4);
            StartedAt = Time.time;
        }

        public void Worsen() => Level = Mathf.Min(4, Level + 1);
        public void Calm() => Level = Mathf.Max(1, Level - 1);
    }

    /// <summary>
    /// A power cut. Artificial light goes out inside (emergency lights stay), escalators and elevators stop and may trap
    /// people. Short cuts come back by themselves; longer ones wait for the facility team (research.md). The station
    /// office, the announcement system and the radio keep working on backup power.
    /// </summary>
    public sealed class PowerOutageHazard : Hazard
    {
        private static readonly string[] Seen =
        {
            "조명이 깜빡이다 잠깐 꺼짐", "일부 조명이 꺼짐", "역 안 조명이 꺼지고 비상조명만 켜짐", "정전으로 조명이 꺼지고 에스컬레이터·엘리베이터가 멈춤",
            "긴 정전으로 역 안이 어둡고 승강설비가 모두 멈춤",
        };

        public int Level { get; }
        /// <summary>When power comes back by itself (infinite: only the facility team restores it).</summary>
        public float Until { get; }
        public bool Restored { get; private set; }
        /// <summary>Escalators and elevators stop in this cut.</summary>
        public bool StopsLifts => Level >= 3;

        public override string Label => "정전";
        public override float NoticeRadius => Restored ? 0 : float.PositiveInfinity;
        public override bool NeedsSight => false;
        public override bool Localized => false;
        public override bool HasScene => true;
        public override float DangerRadius => 0;
        public override string Visible => Restored ? "전기가 다시 들어옴" : Seen[Level];

        public override Agency Command => Agency.Facility;
        public override string ReportOption => "정전 상황 보고";
        public override string ReportLine => "역무실, 정전 확인했습니다. " + (StopsLifts ? "에스컬레이터와 엘리베이터가 멈췄습니다." : "조명이 꺼졌습니다.");
        public override string OfficeReply => "역무실 수신. 전기 담당과 한전에 연락하겠습니다. 엘리베이터 갇힘 여부 확인하고 승객이 넘어지지 않게 천천히 안내해 주십시오.";
        public override PaRequest Announcement => new PaRequest("정전 안내방송 요청", PaLine.PowerOutage,
            "안내 말씀 드립니다. 정전이 발생했습니다. 비상조명을 따라 천천히 이동해 주시고 에스컬레이터와 승강기는 이용하지 마십시오.", PaScope.Inform);
        public override string State => Restored ? "복구됨" : "정전 중";
        public override bool UnderControl => Restored;
        public override string Handover => "정전" + (Restored ? " 복구됨" : " 계속");
        public override float WorkSeconds(Agency agency) => agency == Agency.Facility && !Restored ? 30 : 0;

        public override string Resolve(Agency agency)
        {
            Restore();
            return "시설 담당입니다. 전원 복구했습니다. 승강설비는 점검한 뒤 다시 돌리겠습니다.";
        }

        public PowerOutageHazard(string id, Vector3 office, int level)
        {
            Id = id;
            Kind = HazardKind.PowerOutage;
            Position = office;
            Level = Mathf.Clamp(level, 0, 4);
            Where = "역 전체";
            StartedAt = Time.time;
            Until = Level == 0 ? Time.time + 4 : Level == 1 ? Time.time + 40 : Level == 2 ? Time.time + 90 : float.PositiveInfinity;
        }

        public void Restore()
        {
            if (Restored) return;
            Restored = true;
            End();
        }
    }

    /// <summary>
    /// A fire detector trips without a fire (dust, cooking fumes, steam or a faulty head). The bell rings everywhere and the
    /// interlocked doors open until staff confirm there is no fire and the office resets the receiver; a 119 crew that
    /// was called confirms it, the facility team checks the detector (NFTC 203; research.md).
    /// </summary>
    public sealed class FalseAlarmHazard : Hazard
    {
        public string Cause { get; }
        public bool Checked { get; private set; }
        public bool Cleared { get; private set; }

        public override string Label => "화재경보";
        public override float NoticeRadius => 0;
        public override bool NeedsSight => false;
        public override bool Localized => true;
        public override float DangerRadius => 0;
        public override float Clearance => 0;
        public override string Visible => Cleared ? "수신기를 복구해 비상벨이 멈춤" : Checked ? "감지기 주변에 불이나 연기가 없음" : "비상벨이 울리고 있음";

        public override Agency Command => Agency.Facility;
        public override bool Involves(Agency agency) => agency == Agency.Facility || agency == Agency.Fire;
        public override bool Reportable => Checked && !Cleared;
        public override string ReportOption => Where + " 비화재 확인 보고";
        public override string ReportLine => "역무실, " + Where + " 확인 결과 불이나 연기는 없습니다. 비화재보로 보입니다.";
        public override string OfficeReply => "역무실 수신. 수신기 복구하고 119에는 비화재로 통보하겠습니다. 시설 담당이 감지기 점검하러 갑니다.";
        public override PaRequest Announcement => new PaRequest("오작동 안내방송 요청", PaLine.FalseAlarm,
            "안내 말씀 드립니다. 조금 전 울린 화재경보는 오작동으로 확인되었습니다. 승객 여러분께서는 안심하시고 역을 이용해 주십시오.", PaScope.Inform);
        public override string State => Cleared ? "복구됨" : Checked ? "비화재 확인" : "확인 필요";
        public override bool UnderControl => Cleared;
        public override string Handover => Where + " 화재경보 " + (Cleared ? "비화재 확인·복구" : Checked ? "비화재 확인" : "확인 전");
        public override float WorkSeconds(Agency agency) => agency == Agency.Fire && !Cleared ? 15 : agency == Agency.Facility ? 20 : 0;

        public override string Resolve(Agency agency)
        {
            if (agency == Agency.Fire)
            {
                // 소방대는 화재가 아님을 확인한다. 감지기 점검과 종료는 시설 담당이 한다.
                Check();
                Clear();
                return "소방대입니다. " + Where + " 확인 결과 화재 아닙니다. 수신기 복구 바랍니다.";
            }
            End();
            return "시설 담당입니다. " + Where + " 감지기 점검했습니다. " + Cause + " 때문에 동작한 것으로 보입니다.";
        }

        public FalseAlarmHazard(string id, Vector3 detector, string cause)
        {
            Id = id;
            Kind = HazardKind.FalseAlarm;
            Position = detector;
            Cause = cause;
            StartedAt = Time.time;
        }

        public void Check() => Checked = true;
        public void Clear() => Cleared = true;
    }

    /// <summary>A ceiling panel or hanging sign that came loose and fell without an earthquake.</summary>
    public sealed class FallingObjectHazard : Hazard
    {
        public FallingBoard Board { get; }
        public int Hit { get; set; }
        private readonly float radius;

        public override string Label => "낙하물";
        public override float NoticeRadius => Board.Landed ? 18 : 0;
        public override float DangerRadius => 0;
        public override float StaffSightRange => 20;
        public override string Visible => "천장의 " + Board.Item.Label + KoreanText.Subject(Board.Item.Label) + " 떨어져 바닥에 부서져 있음" + (Hit > 0 ? " · 맞은 사람 " + Hit + "명" : "");

        public override Agency Command => Agency.Facility;
        public override string ToldByPassenger => Where + "에 천장에서 뭐가 떨어졌어요!";
        public override string ReportLine => "역무실, " + Where + "에 천장 " + Board.Item.Label + KoreanText.Subject(Board.Item.Label) + " 떨어졌습니다." + (Hit > 0 ? " 맞은 승객 있습니다." : " 맞은 사람은 없습니다.");
        public override string OfficeReply => "역무실 수신. 시설 담당 보내겠습니다. 더 떨어질 수 있으니 주변 접근을 통제하십시오." + (Hit > 0 ? " 부상자는 119 구급 요청하겠습니다." : "");
        public override IEnumerable<Agency> Dispatch
        {
            get
            {
                yield return Agency.Facility;
                if (Hit > 0) yield return Agency.Medical;
            }
        }
        public override PaRequest Announcement => new PaRequest("안내방송(주변 우회) 요청", PaLine.SuspiciousItem,
            "안내 말씀 드립니다. " + Where + " 주변에서 안전 확인을 하고 있습니다. 해당 구역에서 떨어져 직원 안내에 따라 이동해 주십시오.", PaScope.ClearAround, 12);
        public override string State => Cordoned ? "통제 중" : "통제 필요";
        public override bool UnderControl => Cordoned || !Active;
        public override string Handover => Named + (Cordoned ? " 통제선 설치" : " 통제선 없음") + (Hit > 0 ? " · 맞은 사람 " + Hit + "명" : "");
        public override bool Cordonable => true;
        public override float CordonRadius => radius + 1.5f;
        public override float WorkSeconds(Agency agency) => agency == Agency.Facility ? 30 : 0;

        public override string Resolve(Agency agency)
        {
            End();
            return "시설 담당입니다. 떨어진 " + Board.Item.Label + " 치우고 주변 천장 고정 상태 점검했습니다.";
        }

        public FallingObjectHazard(string id, FallingBoard board)
        {
            Id = id;
            Kind = HazardKind.FallingObject;
            Board = board;
            Position = board.Item.Centre;
            radius = Mathf.Max(board.Item.Size.x, board.Item.Size.z) * .5f + .4f;
            Active = false;
            StartedAt = Time.time;
        }

        /// <summary>The board hit the floor: from now on it is something people see and walk around.</summary>
        public void Landed(Vector3 impact)
        {
            Position = impact;
            Active = true;
        }
    }

    /// <summary>
    /// A burst water pipe above the ceiling: water pours down and spreads over the floor. Slippery floors and wet
    /// electrics are the danger; the facility team shuts the supply valve (research.md). <see cref="Level"/>: flow.
    /// </summary>
    public sealed class WaterLeakHazard : Hazard
    {
        private static readonly string[] Seen =
        {
            "천장 마감재 틈으로 물이 떨어짐", "천장에서 물줄기가 쏟아짐", "천장에서 물이 쏟아져 바닥에 물이 번짐",
            "물이 쏟아져 주변 바닥이 물에 잠김", "배관이 터져 물이 쏟아지고 통로로 흘러감",
        };

        public int Level { get; private set; }
        public bool Stopped { get; private set; }
        public float Radius { get; private set; } = .6f;
        public GameObject View { get; }
        private readonly ParticleSystem stream;
        private readonly Transform puddle;

        private float Target => 1f + 1.6f * Level;

        public override string Label => "누수";
        public override float NoticeRadius => 10 + 2 * Level;
        public override float DangerRadius => 0;
        public override float Clearance => Radius + 2;
        public override string Visible => Stopped ? "물은 멈췄고 바닥이 젖어 있음" : Seen[Level];

        public override Agency Command => Agency.Facility;
        public override string ToldByPassenger => Where + " 천장에서 물이 쏟아져요!";
        public override string ReportLine => "역무실, " + Where + " 천장에서 물이 새고 있습니다." + (Level >= 3 ? " 바닥에 물이 넓게 고였습니다." : "");
        public override string OfficeReply => "역무실 수신. 시설 담당 보내 급수 밸브 잠그겠습니다. 미끄러지지 않게 주변을 통제하고, 물 가까운 전기 설비는 만지지 마십시오.";
        public override PaRequest Announcement => new PaRequest("안내방송(우회) 요청", PaLine.WetFloor,
            "안내 말씀 드립니다. " + Where + " 누수로 바닥이 미끄럽습니다. 해당 구역을 피해 다른 통로를 이용해 주십시오.", PaScope.ClearAround, Radius + 4);
        public override string State => Stopped ? "물 멈춤" : Cordoned ? "통제 중" : "물 새는 중";
        public override bool UnderControl => Stopped || Cordoned;
        public override string Handover => Named + (Stopped ? " 밸브 잠김" : " 계속 샘") + (Cordoned ? " · 통제선 설치" : "");
        public override bool Cordonable => true;
        public override float CordonRadius => Radius + 1.5f;
        public override float WorkSeconds(Agency agency) => agency == Agency.Facility && !Stopped ? 30 : 0;

        public override string Resolve(Agency agency)
        {
            Stopped = true;
            var emission = stream.emission;
            emission.rateOverTime = 0;
            End();
            return "시설 담당입니다. " + Where + " 급수 밸브 잠갔습니다. 물기 닦고 천장 점검하겠습니다.";
        }

        public WaterLeakHazard(string id, Vector3 floor, float ceiling, int level, EmergencyArt art, Transform parent)
        {
            Id = id;
            Kind = HazardKind.WaterLeak;
            Position = floor;
            Level = Mathf.Clamp(level, 0, 4);
            StartedAt = Time.time;
            View = new GameObject("누수");
            View.transform.SetParent(parent, false);
            View.transform.position = floor;
            stream = Particles.Stream(View.transform, art.Smoke, ceiling - floor.y, .3f + .35f * Level);
            puddle = Props.Puddle(View.transform, floor, art.Smoke).transform;
            puddle.localScale = Vector3.one * Radius * 2;
            var marker = View.AddComponent<HazardMarker>();
            marker.Hazard = this;
            marker.Name = "천장 누수";
            var collider = View.AddComponent<SphereCollider>();
            collider.isTrigger = false;
            collider.radius = .25f;
            collider.center = new Vector3(0, 1.2f, 0);
        }

        public void Worsen() => Level = Mathf.Min(4, Level + 1);

        public override void Tick(float deltaSeconds)
        {
            // 물은 멈출 때까지 천천히 번진다(게임 압축 시간).
            if (!Stopped) Radius = Mathf.MoveTowards(Radius, Target, deltaSeconds * .06f);
            if (puddle != null) puddle.localScale = Vector3.one * Radius * 2;
        }
    }

    /// <summary>
    /// A gas smell from a food shop's kitchen (a loose hose, a valve left open). It is smelt before anything is seen. No
    /// flames or switches near it, shut the valve, ventilate, keep people away; 119 and the city gas company come
    /// (research.md). <see cref="Level"/>: how strong and how far it spreads.
    /// </summary>
    public sealed class GasLeakHazard : Hazard
    {
        public string Shop { get; }
        public int Level { get; private set; }
        public bool Shut { get; private set; }

        public override string Label => "가스 냄새";
        public override float NoticeRadius => Shut ? 0 : 6 + 5 * Level;
        public override bool NeedsSight => false;
        public override bool Localized => true;
        public override string SensedAs => "가스 냄새를 맡음";
        public override float DangerRadius => Shut ? 0 : 1 + 1.5f * Level;
        public override float Clearance => 15;
        public override string Visible => Shut ? "가스 냄새가 옅어짐" :
            Level == 0 ? Shop + " 주방 쪽에서 희미한 가스 냄새가 남" :
            Level == 1 ? Shop + " 안에서 가스 냄새가 뚜렷함" :
            Level == 2 ? Shop + " 앞 통로까지 가스 냄새가 퍼짐" :
            Level == 3 ? "가스 냄새가 짙어 머리가 아프다는 사람이 있음" : "쉭 하는 소리와 함께 가스 냄새가 층 전체로 퍼짐";

        public override Agency Command => Agency.Fire;
        public override Agency? CitizenCalls => Agency.Fire;
        // 역무실은 119 와 도시가스사에 함께 알린다(OfficeReply). 도시가스사 안전점검원은 시설 쪽 협력업체로 온다.
        public override IEnumerable<Agency> Dispatch { get { yield return Agency.Fire; yield return Agency.Facility; } }
        public override bool Involves(Agency agency) => agency == Agency.Fire || agency == Agency.Facility;
        public override string ToldByPassenger => Where + " 쪽에서 가스 냄새가 심하게 나요!";
        public override string ReportLine => "역무실, " + Where + " " + Shop + " 쪽에서 가스 냄새가 납니다." + (Level >= 3 ? " 냄새가 짙습니다." : "");
        public override string OfficeReply => "역무실 수신. 119와 도시가스사에 신고하겠습니다. 불씨와 전기 스위치를 쓰지 말게 하고 주변 승객을 떨어뜨리십시오. 매장에 가스 중간밸브를 잠그도록 전해 주십시오.";
        public override PaRequest Announcement => new PaRequest("가스 누출 안내방송 요청", PaLine.GasLeak,
            "안내 말씀 드립니다. " + Where + " 부근에서 가스 냄새가 나고 있습니다. 라이터나 전기 스위치를 사용하지 마시고 직원의 안내에 따라 해당 구역에서 대피해 주십시오.", PaScope.EvacuateArea, 40);
        public override string State => Shut ? "밸브 잠금" : Cordoned ? "통제 중" : "누출 중";
        public override bool UnderControl => Shut || Cordoned;
        public override string Handover => Named + (Shut ? " 밸브 잠김" : " 누출 계속") + (Cordoned ? " · 통제선 설치" : "");
        public override bool Cordonable => true;
        public override float CordonRadius => 8;
        public override float WorkSeconds(Agency agency) => !Shut && (agency == Agency.Fire || agency == Agency.Facility) ? 25 : 0;

        public override string Resolve(Agency agency)
        {
            ShutOff();
            return agency == Agency.Facility
                ? "도시가스 안전점검원입니다. " + Shop + " 계량기 앞 밸브를 잠그고 누설 부위를 확인했습니다. 점검 전까지 사용 중지입니다."
                : "소방대입니다. " + Shop + " 가스 중간밸브 잠그고 환기 중입니다. 누출은 멈췄습니다.";
        }

        public GasLeakHazard(string id, Vector3 shop, string name, int level)
        {
            Id = id;
            Kind = HazardKind.GasLeak;
            Position = shop;
            Shop = name;
            Level = Mathf.Clamp(level, 0, 4);
            StartedAt = Time.time;
        }

        public void Worsen() => Level = Mathf.Min(4, Level + 1);

        public void ShutOff()
        {
            Shut = true;
            End();
        }
    }

    /// <summary>
    /// Power-cut lighting: the twin's realtime and mixed lights inside the building go out and a local exposure drop darkens
    /// the baked light indoors (daylight outside stays). Emergency lighting keeps the station from going black.
    /// </summary>
    public static class StationLighting
    {
        private static readonly List<Light> off = new List<Light>();
        private static GameObject root;

        public static bool Dark => root != null;

        public static void Dim(Transform parent, IReadOnlyList<StationPoints.ZoneEntry> zones)
        {
            if (root != null) return;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!light.enabled || light.type == LightType.Directional || light.lightmapBakeType == LightmapBakeType.Baked) continue;
                // 사건의 불빛(불꽃)과 엘리베이터 카 조명(비상 전원)은 그대로 둔다.
                if (light.GetComponentInParent<IncidentDirector>() != null || light.gameObject.name == "카 조명") continue;
                light.enabled = false;
                off.Add(light);
            }
            root = new GameObject("정전 조명");
            root.transform.SetParent(parent, false);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var colour = profile.Add<ColorAdjustments>(true);
            colour.postExposure.Override(-1.7f);
            colour.saturation.Override(-25f);
            foreach (var zone in zones)
            {
                // 바깥(광장·하늘광장)은 낮이라 그대로 밝다.
                if (zone.id == "plaza" || zone.id == "skyplaza") continue;
                var go = new GameObject("정전 · " + zone.label);
                go.transform.SetParent(root.transform, false);
                go.transform.position = (zone.min + zone.max) * .5f;
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(Mathf.Abs(zone.max.x - zone.min.x), Mathf.Abs(zone.max.y - zone.min.y) + 4, Mathf.Abs(zone.max.z - zone.min.z));
                var volume = go.AddComponent<Volume>();
                volume.isGlobal = false;
                volume.blendDistance = 4;
                volume.priority = 200;
                volume.sharedProfile = profile;
            }
        }

        public static void Restore()
        {
            foreach (var light in off) if (light != null) light.enabled = true;
            off.Clear();
            if (root != null) Object.Destroy(root);
            root = null;
        }
    }
}
