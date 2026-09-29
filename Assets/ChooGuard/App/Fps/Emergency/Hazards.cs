using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    public enum HazardKind { Fire, SuspiciousItem, Earthquake, Collapse, DoorTrap, Disturbance, BombThreat, Substance, GasLeak, ElevatorTrap, PowerOutage, FalseAlarm, FallingObject, WaterLeak, TrackFall }

    /// <summary>How far an announcement asks people to move.</summary>
    public enum PaScope { ClearAround, EvacuateArea, EvacuateStation, Inform }

    /// <summary>The announcement staff can ask the office for: the pre-recorded line, the feed text and what the crowd is asked to do.</summary>
    public readonly struct PaRequest
    {
        /// <summary>Radio wheel label ("대피 안내방송 요청").</summary>
        public readonly string Option;
        public readonly PaLine Line;
        public readonly string Text;
        public readonly PaScope Scope;
        /// <summary>Metres around the hazard for <see cref="PaScope.ClearAround"/> and <see cref="PaScope.EvacuateArea"/>.</summary>
        public readonly float Radius;

        public PaRequest(string option, PaLine line, string text, PaScope scope, float radius = 0)
        {
            Option = option;
            Line = line;
            Text = text;
            Scope = scope;
            Radius = radius;
        }
    }

    /// <summary>Korean particles chosen by the last syllable of the noun (받침 or not).</summary>
    public static class KoreanText
    {
        private static bool Final(string noun)
        {
            if (string.IsNullOrEmpty(noun)) return false;
            char last = noun[noun.Length - 1];
            return last >= '가' && last <= '힣' && (last - '가') % 28 != 0;
        }

        /// <summary>Subject particle: 이 after a final consonant, 가 after a vowel.</summary>
        public static string Subject(string noun) => Final(noun) ? "이" : "가";
        /// <summary>Object particle: 을 after a final consonant, 를 after a vowel.</summary>
        public static string Object(string noun) => Final(noun) ? "을" : "를";
        /// <summary>Instrumental particle: 으로 after a final consonant other than ㄹ, 로 otherwise.</summary>
        public static string Instrument(string noun) => Final(noun) && (noun[noun.Length - 1] - '가') % 28 != 8 ? "으로" : "로";
    }

    /// <summary>
    /// Something people can notice and react to. Owned and advanced by the incident director. Each kind also says how the
    /// station responds to it — who takes command, what the staff member reports and the office answers, which announcement
    /// fits, how it stands — so the director handles every kind the same way.
    /// </summary>
    public abstract class Hazard
    {
        public string Id { get; protected set; }
        public HazardKind Kind { get; protected set; }
        public Vector3 Position { get; protected set; }
        public bool Active { get; protected set; } = true;
        public float StartedAt { get; protected set; }
        /// <summary>Place name the station uses for this spot (for radio reports and JEV context).</summary>
        public string Where { get; set; } = "";
        /// <summary>Short Korean name: 화재, 의심 물체, 지진.</summary>
        public abstract string Label { get; }
        /// <summary>Distance at which a passenger can notice it now (line of sight needed when <see cref="NeedsSight"/>).</summary>
        public abstract float NoticeRadius { get; }
        /// <summary>Distance inside which staying is dangerous now (reflex retreat).</summary>
        public abstract float DangerRadius { get; }
        /// <summary>Distance escape routes should keep from it.</summary>
        public virtual float Clearance => DangerRadius + 4;
        /// <summary>Short Korean description of what an onlooker can see now. Never hidden truth.</summary>
        public abstract string Visible { get; }
        /// <summary>Noticing needs line of sight (smoke, items, people); otherwise it is heard, smelt or felt within <see cref="NoticeRadius"/>.</summary>
        public virtual bool NeedsSight => true;
        /// <summary>It has a place people can move away from (false for station-wide shaking, a power cut or a phoned threat).</summary>
        public virtual bool Localized => NeedsSight;
        /// <summary>Chance per perception tick (about two a second) that someone in range registers it as wrong.</summary>
        public virtual float NoticeChance => 1f;
        /// <summary>How far away the staff member recognises it by sight.</summary>
        public virtual float StaffSightRange => NoticeRadius * 1.2f;
        /// <summary>How the staff member notices it without seeing it (smell, noise) within its radius; null when it must be seen.</summary>
        public virtual string SensedAs => null;
        /// <summary>Makes people at <paramref name="position"/> cough (smoke, gas, powder).</summary>
        public virtual bool Irritates(Vector3 position) => false;
        public virtual void Tick(float deltaSeconds) { }
        public virtual void End() { Active = false; }

        // ── 대응 ──

        /// <summary>The agency whose team takes command on arrival and takes the staff member's handover.</summary>
        public abstract Agency Command { get; }
        /// <summary>Whether a team of <paramref name="agency"/> has something to do here (it is sent here when called).</summary>
        public virtual bool Involves(Agency agency) => agency == Command;
        /// <summary>The agency a passenger who phones it in reaches (119 fire/EMS or 112), or null when nobody would call.</summary>
        public virtual Agency? CitizenCalls => null;
        /// <summary>What a passenger who runs to the staff member says.</summary>
        public virtual string ToldByPassenger => "여기 좀 봐 주세요!";
        /// <summary>Radio wheel label for reporting it to the office.</summary>
        public virtual string ReportOption => Named + " 보고";
        /// <summary>There is something to report now (a false alarm only once staff have looked).</summary>
        public virtual bool Reportable => true;
        /// <summary>The staff member's radio report.</summary>
        public abstract string ReportLine { get; }
        /// <summary>The station office's answer to the report.</summary>
        public abstract string OfficeReply { get; }
        /// <summary>Agencies the office calls on the report.</summary>
        public virtual IEnumerable<Agency> Dispatch { get { yield return Command; } }
        /// <summary>Why the KTX at the platform may not leave while this lasts (added on the report), or null.</summary>
        public virtual string TrainHold => null;
        /// <summary>The announcement staff can ask for.</summary>
        public abstract PaRequest Announcement { get; }
        /// <summary>A few words on how it stands now (HUD strip).</summary>
        public abstract string State { get; }
        /// <summary>The Tab board line.</summary>
        public virtual string BoardState => State;
        /// <summary>The situation is under control: the HUD shows it in the task colour instead of danger.</summary>
        public virtual bool UnderControl => !Active;
        /// <summary>Its part of the handover summary.</summary>
        public virtual string Handover => Named + " " + State;
        /// <summary>Where arriving teams head and work.</summary>
        public virtual Vector3 Scene => Position;
        /// <summary>Teams have a place to go (<see cref="Scene"/>) even when people need not move away from it.</summary>
        public virtual bool HasScene => Localized;
        /// <summary>Seconds a team of <paramref name="agency"/> works here with their hands before it is safe; 0 when they only take over.</summary>
        public virtual float WorkSeconds(Agency agency) => 0;
        /// <summary>The team of <paramref name="agency"/> finished its work: the hazard is made safe. Returns what the team radios, or null.</summary>
        public virtual string Resolve(Agency agency) { End(); return null; }
        /// <summary>Staff may put a cordon round it.</summary>
        public virtual bool Cordonable => false;
        public virtual float CordonRadius => 6;
        public bool Cordoned { get; set; }

        /// <summary>"Place + what" for radio calls and summaries, without repeating a word the place name already ends with.</summary>
        public string Named
        {
            get
            {
                var where = Where ?? "";
                var label = Label;
                int space = label.IndexOf(' ');
                if (space > 0 && where.EndsWith(label.Substring(0, space), System.StringComparison.Ordinal)) label = label.Substring(space + 1);
                return (where + " " + label).Trim();
            }
        }
    }

    /// <summary>Registry the crowd reads every perception tick.</summary>
    public static class HazardRegistry
    {
        private static readonly List<Hazard> active = new List<Hazard>();
        private static readonly RaycastHit[] hits = new RaycastHit[12];
        public static IReadOnlyList<Hazard> Active => active;
        public static void Add(Hazard hazard) { if (!active.Contains(hazard)) active.Add(hazard); }
        public static void Remove(Hazard hazard) => active.Remove(hazard);
        public static void Clear() => active.Clear();

        public static bool CanSee(Vector3 eye, Hazard hazard)
        {
            if (!hazard.NeedsSight) return true;
            var target = hazard.Position + Vector3.up * (hazard is FireHazard fire ? .6f + fire.Intensity * 2f : .35f);
            var direction = target - eye;
            float distance = direction.magnitude;
            if (distance < .5f) return true;
            int count = Physics.RaycastNonAlloc(eye, direction / distance, hits, distance - .3f, ~0, QueryTriggerInteraction.Ignore);
            // 사람 몸과 사건 물체 자신은 가림으로 보지 않는다. 벽·기둥·점포 칸막이만 시야를 막는다.
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider.GetComponentInParent<PersonBody>() != null || collider.GetComponentInParent<HazardMarker>() != null) continue;
                return false;
            }
            return true;
        }
    }

    /// <summary>Tags the scene objects that belong to a hazard (sight checks ignore them; the player can look at them).</summary>
    public sealed class HazardMarker : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public Hazard Hazard;
        public string Name = "";
        public System.Func<string> Prompt;
        public System.Func<FirstPersonResponder, string> Act;

        public string DisplayName => Name;
        public string InteractionPrompt => Prompt?.Invoke() ?? "";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            bool available = responder != null && !responder.IsPaused && Act != null && !string.IsNullOrEmpty(InteractionPrompt);
            reason = available ? null : "";
            return available;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out _)) return false;
            feedback = Act(responder);
            return true;
        }
    }

    /// <summary>
    /// A fire that starts small and grows unless suppressed. Beyond intensity 1 a hand extinguisher barely helps
    /// (the fire has left its first stage). Growth rates are game-compressed, not fire-engineering figures. What burns
    /// (<see cref="Subject"/>: a bag, a litter bin, a fryer, a ticket machine, the underside of a KTX car) only changes
    /// what people see and what the office advises.
    /// </summary>
    public sealed partial class FireHazard : Hazard
    {
        public const float Growth = .0035f;
        public float Intensity { get; private set; }
        public float SmokeRadius { get; private set; }
        public string Source { get; }
        /// <summary>What is burning, as people see it ("가방", "휴지통", "튀김기").</summary>
        public string Subject { get; }
        /// <summary>Burning in or under the KTX set at the platform: the set is held and the train crew comes too.</summary>
        public bool Aboard { get; set; }
        /// <summary>Burning under the car (running gear), not in it: fought from the platform beside the car, not through the door.</summary>
        public bool Beneath { get; set; }
        /// <summary>Extra advice the office gives with its answer (cooking oil, electricity).</summary>
        public string Advice { get; set; } = "";
        public bool Extinguished { get; private set; }
        public float SuppressedSeconds { get; private set; }
        public GameObject View { get; }

        private readonly ParticleSystem flames, smoke;
        private readonly Light glow;
        private readonly NavMeshObstacle obstacle;
        private float smokeLinger = 45;

        public override string Label => "화재";
        public override float NoticeRadius => Extinguished ? 6 : 8 + 32 * Mathf.Min(Intensity, 1.1f);
        public override float DangerRadius => Extinguished ? 0 : 1.2f + 3.2f * Intensity;
        public override string Visible => Extinguished ? "꺼진 불 자리에서 연기가 남아 있음" :
            Intensity < .3f ? Subject + "에서 흰 연기가 새어 나옴" :
            Intensity < .7f ? Subject + "에서 불꽃과 짙은 회색 연기가 오름" :
            Intensity < 1f ? "불꽃이 주변으로 옮겨붙고 검은 연기가 퍼짐" : "불길이 커져 천장 쪽으로 검은 연기가 번짐";
        public override bool Irritates(Vector3 position) => InSmoke(position);

        public override Agency Command => Agency.Fire;
        public override bool Involves(Agency agency) => agency == Agency.Fire || Aboard && agency == Agency.Crew || FeedCutBy == agency;
        public override Agency? CitizenCalls => Agency.Fire;
        public override string ToldByPassenger => "저기 " + Where + " 쪽에서 연기가 나요!";
        public override string ReportLine => "역무실, " + Where + " 화재 발생. " + (Intensity > .6f ? "불길이 큽니다." : "초기 단계입니다.");
        public override string OfficeReply => "역무실 수신. 119 신고하겠습니다." + (Aboard ? " 열차 출발 보류하고 승무원 보내겠습니다." : "") + Advice + " 초기 진화 가능하면 시도하고 무리하지 마십시오.";
        public override IEnumerable<Agency> Dispatch
        {
            get
            {
                yield return Agency.Fire;
                if (Aboard) yield return Agency.Crew;
                if (FeedCutBy is Agency cutter) yield return cutter;
            }
        }
        public override string TrainHold => Aboard ? "차내 화재" : null;
        public override PaRequest Announcement => new PaRequest("대피 안내방송 요청", PaLine.Fire,
            "안내 말씀 드립니다. " + Where + "에 화재가 발생했습니다. 승객 여러분께서는 직원의 안내에 따라 가까운 출구로 대피해 주시기 바랍니다.", PaScope.EvacuateStation);
        public override string State => Extinguished ? "진화됨" : "타는 중";
        public override string BoardState => Extinguished ? "꺼짐 · 연기 남음" : Intensity > 1 ? "크게 번짐 · 소화기로는 어려움" : "타는 중";
        public override bool UnderControl => Extinguished;
        public override string Handover => Where + (Extinguished ? " 불 꺼짐" : " 불 계속 탐") + FeedNote;

        public FireHazard(string id, Vector3 position, string source, string subject, float intensity, EmergencyArt art, Transform parent)
        {
            Id = id;
            Kind = HazardKind.Fire;
            Position = position;
            Source = source;
            Subject = subject;
            Intensity = intensity;
            StartedAt = Time.time;
            View = new GameObject("화재 · " + source);
            View.transform.SetParent(parent, false);
            View.transform.position = position;
            flames = Particles.Flames(View.transform, art.Flame);
            smoke = Particles.Smoke(View.transform, art.Smoke);
            var light = new GameObject("불빛", typeof(Light));
            light.transform.SetParent(View.transform, false);
            light.transform.localPosition = new Vector3(0, .6f, 0);
            glow = light.GetComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, .55f, .2f);
            glow.shadows = LightShadows.None;
            obstacle = View.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            var collider = View.AddComponent<SphereCollider>();
            collider.isTrigger = false;
            collider.radius = .35f;
            collider.center = new Vector3(0, .3f, 0);
            var marker = View.AddComponent<HazardMarker>();
            marker.Hazard = this;
            marker.Name = "불";
            Apply();
        }

        public bool InSmoke(Vector3 position)
        {
            if (SmokeRadius <= 0) return false;
            var d = position - Position;
            // 연기는 그 층에 퍼지고, 위층(뚫린 맞이방·계단)으로는 좁게만 올라간다. 아래층으로는 내려가지 않는다.
            if (d.y < -2f || d.y > 6.5f) return false;
            float radius = d.y > 3.5f ? SmokeRadius * .6f : SmokeRadius;
            d.y = 0;
            return d.sqrMagnitude < radius * radius * .45f;
        }

        public override void Tick(float deltaSeconds)
        {
            if (Extinguished)
            {
                smokeLinger -= deltaSeconds;
                SmokeRadius = Mathf.MoveTowards(SmokeRadius, 0, deltaSeconds * .25f);
                if (smokeLinger <= 0 && View != null) { var e = smoke.emission; e.rateOverTime = 0; }
                ApplyVisuals();
                return;
            }
            if (SuppressedSeconds <= 0) Intensity = Mathf.Min(1.4f, Intensity + Growth * deltaSeconds);
            SuppressedSeconds = Mathf.Max(0, SuppressedSeconds - deltaSeconds);
            float targetRadius = 3 + 24 * Intensity;
            SmokeRadius = Mathf.MoveTowards(SmokeRadius, targetRadius, deltaSeconds * (targetRadius > SmokeRadius ? .35f : .15f));
            Apply();
        }

        /// <summary>
        /// Applies extinguishing agent. <paramref name="quality"/> is aim quality 0..1 (base of the flame, distance). A hand
        /// extinguisher barely helps beyond the first stage (intensity 1); an indoor hydrant's water stream still does
        /// (<paramref name="hose"/>). Rates are game tuning, not fire-engineering figures.
        /// </summary>
        public void Suppress(float quality, float deltaSeconds, bool hose = false)
        {
            if (Extinguished) return;
            SuppressedSeconds = .6f;
            if (hose) WaterApplied();
            // 초기 단계(1 이하)에서만 소화기가 제대로 듣는다. 옥내소화전 방수는 그 뒤에도 듣는다.
            float effect = hose ? (Intensity > 1f ? .7f : 1.6f) : Intensity > 1f ? .15f : 1f;
            // 불을 먹이는 것(통전된 전기, 새는 가스)이 그대로면 불씨 이하로는 꺼지지 않는다.
            Intensity = Mathf.Max(Intensity - .08f * quality * effect * deltaSeconds, FeedFloor);
            if (Intensity <= 0) Extinguish();
            else Apply();
        }

        public void Grow(float amount)
        {
            if (Extinguished) return;
            Intensity = Mathf.Min(1.4f, Intensity + amount);
            Apply();
        }

        /// <summary>Smoke drifts farther than the fire's size alone would push it (air movement in the hall).</summary>
        public void SpreadSmoke(float metres) { if (!Extinguished) SmokeRadius += metres; }

        public void Extinguish()
        {
            Intensity = 0;
            Extinguished = true;
            Active = false;
            var e = flames.emission; e.rateOverTime = 0;
            glow.enabled = false;
            obstacle.enabled = false;
            ApplyVisuals();
        }

        public override void End()
        {
            base.End();
            if (View != null) Object.Destroy(View);
        }

        private void Apply()
        {
            // 길은 불꽃 자리만 깎는다(위험 반경 전체를 깎으면 대합실 의자 사이 통로가 끊겨 옆 사람이 갇힌다).
            obstacle.radius = .3f + .8f * Intensity;
            obstacle.height = 2f;
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            if (!Extinguished)
            {
                var fe = flames.emission; fe.rateOverTime = 18 + 90 * Intensity;
                var fm = flames.main; fm.startSize = new ParticleSystem.MinMaxCurve(.2f + .5f * Intensity, .4f + 1.1f * Intensity);
                var fs = flames.shape; fs.radius = Mathf.Min(.12f + .9f * Intensity, Footprint);
                glow.intensity = (2 + 10 * Intensity) * (0.85f + .15f * Mathf.PerlinNoise(Time.time * 7, 0));
                glow.range = 4 + 10 * Intensity;
            }
            var se = smoke.emission; se.rateOverTime = Extinguished ? (smokeLinger > 0 ? 6 : 0) : 8 + 55 * Intensity;
            var sm = smoke.main; sm.startSize = new ParticleSystem.MinMaxCurve(.8f + 1.2f * Intensity, 1.6f + 3.2f * Intensity);
            var ss = smoke.shape; ss.radius = .2f + .8f * Intensity;
            var sc = smoke.main; sc.startColor = Extinguished ? new Color(.75f, .75f, .75f, .35f) :
                Color.Lerp(new Color(.82f, .82f, .82f, .8f), new Color(.12f, .12f, .12f, .9f), Mathf.Clamp01(Intensity * 1.3f));
        }
    }

    /// <summary>An unattended suitcase. It becomes a hazard only once its owner has walked away from it.</summary>
    public sealed class SuspiciousItemHazard : Hazard
    {
        public GameObject View { get; }
        public Passenger Owner { get; }
        public bool Unattended { get; private set; }
        public bool Touched { get; set; }
        public float UnattendedAt { get; private set; }

        public override string Label => "의심 물체";
        // 주인 없는 가방은 가까이 있는 사람이, 한동안 그대로 놓인 뒤에야 이상하다고 여긴다.
        public override float NoticeRadius => Unattended ? 5 : 0;
        public override float NoticeChance => Time.time - UnattendedAt < 30 ? .015f : .05f;
        public override float DangerRadius => 0;
        public override float Clearance => 20;
        public override float StaffSightRange => 8;
        public override string Visible => "주인 없이 놓인 검은 여행가방" + (Time.time - UnattendedAt > 60 ? " (1분 넘게 그대로)" : "");

        public override Agency Command => Agency.Police;
        public override Agency? CitizenCalls => Agency.Police;
        public override string ToldByPassenger => Where + "에 주인 없는 가방이 한참 놓여 있어요.";
        public override string ReportLine => "역무실, " + Where + "에 주인 없는 여행가방 있습니다.";
        public override string OfficeReply => "역무실 수신. 112와 철도경찰에 신고하겠습니다. 가방은 건드리지 말고 주변 접근을 통제하십시오.";
        public override PaRequest Announcement => new PaRequest("안내방송(주변 대피) 요청", PaLine.SuspiciousItem,
            "안내 말씀 드립니다. " + Where + " 주변에서 안전 확인을 하고 있습니다. 해당 구역에서 떨어져 직원 안내에 따라 이동해 주십시오.", PaScope.EvacuateArea, 50);
        public override string State => Cordoned ? "통제 중" : "확인 필요";
        public override string BoardState => Cordoned ? "통제선 안" : "그대로 놓여 있음";
        public override bool UnderControl => Cordoned;
        public override string Handover => (Touched ? "승객이 가방을 만졌음" : "가방은 아무도 만지지 않았음") + (Cordoned ? " · 통제선 설치" : " · 통제선 없음");
        public override bool Cordonable => true;

        public SuspiciousItemHazard(string id, Vector3 position, Quaternion rotation, Passenger owner, EmergencyArt art, Transform parent)
        {
            Id = id;
            Kind = HazardKind.SuspiciousItem;
            Position = position;
            Owner = owner;
            Active = false;
            StartedAt = Time.time;
            View = Props.Suitcase(parent, position, rotation, art);
            var marker = View.AddComponent<HazardMarker>();
            marker.Hazard = this;
            marker.Name = "주인 없는 여행가방";
        }

        public void MarkUnattended()
        {
            if (Unattended) return;
            Unattended = true;
            Active = true;
            UnattendedAt = Time.time;
        }
    }

    /// <summary>Ground shaking felt by everyone in the hall for a while, then aftershock risk.</summary>
    public sealed class EarthquakeHazard : Hazard
    {
        public float ShakeUntil { get; private set; }
        public float Strength { get; private set; }
        public bool Shaking => Time.time < ShakeUntil;
        public int Aftershocks { get; private set; }
        /// <summary>Fallen boards the staff member has seen (for the board and the handover).</summary>
        public int FallenKnown { get; set; }

        public override string Label => "지진";
        public override float NoticeRadius => float.PositiveInfinity;
        public override float DangerRadius => 0;
        public override bool NeedsSight => false;
        public override string Visible => Shaking ? "역사 전체가 크게 흔들리고 천장 구조물이 삐걱거림" : "흔들림이 멈춤";

        public override Agency Command => Agency.Facility;
        public override string ReportOption => "지진 흔들림 보고";
        public override string ReportLine => "역무실, 지진 흔들림 확인. 피해 상황 확인하겠습니다.";
        public override string OfficeReply => "역무실 수신. 낙하물과 부상자, 승강설비 확인해서 알려 주십시오.";
        // 역무실은 보고만으로 부르지 않는다: 낙하물 보고나 일정 시간 뒤 자체 판단으로 시설 담당을 부른다.
        public override IEnumerable<Agency> Dispatch { get { yield break; } }
        public override PaRequest Announcement => new PaRequest("대피 안내방송 요청", PaLine.Earthquake,
            "안내 말씀 드립니다. 지진이 발생했습니다. 낙하물에 주의하시고 머리를 보호하며 직원 안내에 따라 이동해 주십시오. 승강기는 이용하지 마십시오.", PaScope.EvacuateStation);
        public override string State => Shaking ? "흔들림" : "흔들림 멈춤";
        public override string BoardState => (Shaking ? "흔들림 계속" : "흔들림 멈춤") + " · 낙하물 " + FallenKnown + "곳";
        public override bool UnderControl => false;
        public override string Handover => "낙하물 " + FallenKnown + "곳 확인";

        public EarthquakeHazard(string id, Vector3 hallCentre, float seconds, float strength)
        {
            Id = id;
            Kind = HazardKind.Earthquake;
            Position = hallCentre;
            StartedAt = Time.time;
            ShakeUntil = Time.time + seconds;
            Strength = strength;
        }

        public void Aftershock(float seconds, float strength)
        {
            Aftershocks++;
            ShakeUntil = Time.time + seconds;
            Strength = strength;
        }
    }

    /// <summary>
    /// A person who collapsed or fell ill (medical) or fell (stairs, escalator) and cannot get up. Bystanders who see it may
    /// help, report or watch; the danger is to the person, not to those around. <see cref="Level"/> 0 (conscious) .. 4 (not
    /// breathing normally) for medical conditions, which may worsen or ease; -1 for a fall.
    /// </summary>
    public sealed class CollapseHazard : Hazard
    {
        public Passenger Person { get; }
        /// <summary>Korean adjective phrase for the person ("쓰러진", "경련을 일으킨").</summary>
        public string Cause { get; }
        /// <summary>The same in English for JEV descriptions ("collapsed", "had a seizure").</summary>
        public string CauseEn { get; }
        public int Level { get; private set; } = -1;
        public bool NotBreathingNormally => Level >= 4;
        /// <summary>When an AED was set down beside the person (-1: not yet). Logged only (JEV 011).</summary>
        public float AedAt { get; set; } = -1;
        /// <summary>The escalator the person fell on, if any (the facility team is involved and the belt can be stopped).</summary>
        public Escalator Escalator { get; set; }
        public bool Treated { get; set; }
        /// <summary>What a bystander who runs to the staff member says (after the place).</summary>
        public string Told { get; set; } = "에 사람이 쓰러졌어요! 빨리 와 주세요.";
        /// <summary>Extra advice the office gives (a seizure, an asthma attack).</summary>
        public string Advice { get; set; } = "";
        private readonly string label;
        private string visible;

        public override string Label => label;
        public override float NoticeRadius => 14;
        public override float DangerRadius => 0;
        public override float Clearance => 0;
        public override float StaffSightRange => 14;
        public override string Visible => visible;

        public override Agency Command => Agency.Medical;
        public override bool Involves(Agency agency) => agency == Agency.Medical || Escalator != null && agency == Agency.Facility;
        public override Agency? CitizenCalls => Agency.Medical;
        public override string ToldByPassenger => Where + Told;
        public override string ReportLine => "역무실, " + Where + (Escalator != null ? "에서 승객이 넘어졌습니다. " : "에 " + Cause + " 승객 있습니다. ") + visible + ".";
        public override string OfficeReply => "역무실 수신. 119 구급 요청하겠습니다. 환자 곁을 지키고 주변을 비워 주십시오." + (Escalator != null ? " 에스컬레이터 정지 확인 바랍니다." : "") + Advice +
            // 반응 없고 숨이 고르지 않으면 119 신고와 함께 가까운 AED 를 가져오게 한다(2025 한국 심폐소생술 가이드라인).
            (NotBreathingNormally ? " 가까운 자동심장충격기를 가져와 환자 곁에 두십시오." : "");
        public override PaRequest Announcement => new PaRequest("안내방송(통로 비우기) 요청", PaLine.Medical,
            "안내 말씀 드립니다. " + Where + "에 응급 환자가 있습니다. 통로를 비워 주시고 구급대 진입에 협조해 주십시오.", PaScope.ClearAround, 15);
        public override string State => Treated ? "처치 중" : "처치 필요";
        public override string BoardState => visible;
        public override bool UnderControl => Treated || !Active;
        public override string Handover => Named + (Treated ? " 처치 중" : " 처치 전") + (AedAt >= 0 ? " · AED 곁에 둠(쓰러진 지 " + Mathf.RoundToInt(AedAt - StartedAt) + "초)" : "");

        public CollapseHazard(string id, Passenger person, string label, string cause, string causeEn, string visible, int level = -1)
        {
            Id = id;
            Kind = HazardKind.Collapse;
            Person = person;
            Cause = cause;
            CauseEn = causeEn;
            this.label = label;
            this.visible = visible;
            Level = level;
            Position = person.transform.position;
            StartedAt = Time.time;
        }

        /// <summary>The condition changed (worsened or eased): what people see now.</summary>
        public void Change(int level, string nowVisible)
        {
            Level = level;
            visible = nowVisible;
        }

        public override void Tick(float deltaSeconds)
        {
            if (Person == null) { End(); return; }
            Position = Person.transform.position;
        }
    }

    /// <summary>A closing KTX door caught a passenger's bag or arm; the set cannot leave until someone frees it.</summary>
    public sealed class DoorTrapHazard : Hazard
    {
        public TrainService.Car Car { get; }
        public Passenger Person { get; }
        public bool Freed { get; private set; }
        /// <summary>An arm, not a belonging, is caught: the passenger is hurt once freed.</summary>
        public bool Injures { get; }
        private readonly string what;

        public override string Label => "출입문 끼임";
        public override float NoticeRadius => 12;
        public override float DangerRadius => 0;
        public override float Clearance => 0;
        public override string Visible => Freed ? "출입문에 끼였던 " + what + KoreanText.Object(what) + " 빼냈음" : "KTX " + Car.Label + " 출입문에 " + what + KoreanText.Subject(what) + " 끼여 문이 다 닫히지 않음";

        public override Agency Command => Agency.Crew;
        public override string ToldByPassenger => "KTX " + Car.Label + " 문에 " + what + KoreanText.Subject(what) + " 끼었어요!";
        public override string ReportLine => "역무실, 5·6 타는 곳 KTX " + Car.Label + " 출입문 끼임입니다. 출발 보류 바랍니다.";
        public override string OfficeReply => "역무실 수신. 출발 보류하고 열차팀장에게 전달하겠습니다.";
        public override PaRequest Announcement => new PaRequest("안내방송(통로 비우기) 요청", PaLine.DoorCheck,
            "안내 말씀 드립니다. " + Where + " 출입문을 점검하고 있습니다. 열차 출발이 잠시 늦어지니 출입문에서 물러서 주십시오.", PaScope.ClearAround, 15);
        public override string State => Active ? "출발 보류" : "해소";
        public override string BoardState => Active ? "끼인 채 · 출발 보류" : "해소";
        public override string Handover => Car.Label + (Active ? " 문 끼임 계속" : " 문 끼임 해소");

        public DoorTrapHazard(string id, TrainService.Car car, Passenger person, string what, bool injures, Vector3 position)
        {
            Id = id;
            Kind = HazardKind.DoorTrap;
            Car = car;
            Person = person;
            Injures = injures;
            this.what = what;
            Position = position;
            StartedAt = Time.time;
        }

        public void Free()
        {
            Freed = true;
            End();
        }
    }

    /// <summary>Runtime particle systems for fire, smoke and extinguishing powder (URP particle materials from EmergencyArt).</summary>
    public static class Particles
    {
        public static ParticleSystem Flames(Transform parent, Material material)
        {
            var ps = Make(parent, "불꽃", material, new Vector3(0, .1f, 0));
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 1.3f);
            main.maxParticles = 600;
            main.gravityModifier = -.12f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8;
            shape.rotation = new Vector3(-90, 0, 0);
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, .85f, .45f), 0), new GradientColorKey(new Color(1f, .45f, .12f), .45f), new GradientColorKey(new Color(.6f, .12f, .05f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.95f, .12f), new GradientAlphaKey(.6f, .6f), new GradientAlphaKey(0, 1) });
            colour.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .5f), new Keyframe(.35f, 1), new Keyframe(1, .15f)));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .35f;
            noise.frequency = 1.4f;
            ps.Play();
            return ps;
        }

        public static ParticleSystem Smoke(Transform parent, Material material)
        {
            var ps = Make(parent, "연기", material, new Vector3(0, .5f, 0));
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 1.4f);
            main.maxParticles = 900;
            main.gravityModifier = 0;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22;
            shape.rotation = new Vector3(-90, 0, 0);
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.8f, .12f), new GradientAlphaKey(.5f, .7f), new GradientAlphaKey(0, 1) });
            colour.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .4f), new Keyframe(1, 2.6f)));
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.radial = new ParticleSystem.MinMaxCurve(.3f, .7f);
            // 뜨거운 연기는 오르다 식으며 느려지고 옆으로 퍼진다. 제한 없이 오르면 지붕을 뚫고 사라진다.
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 1.2f), new Keyframe(.5f, .5f), new Keyframe(1, .25f)));
            limit.dampen = .12f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .4f;
            noise.frequency = .35f;
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-.4f, .4f);
            ps.Play();
            return ps;
        }

        public static ParticleSystem Powder(Transform parent, Material material)
        {
            var ps = Make(parent, "소화 약제", material, Vector3.zero);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.5f, .9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.15f, .35f);
            main.startColor = new Color(.95f, .95f, .92f, .55f);
            main.maxParticles = 500;
            main.gravityModifier = .15f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7;
            shape.radius = .02f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .3f), new Keyframe(1, 3.2f)));
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(.9f, 0), new GradientAlphaKey(.4f, .6f), new GradientAlphaKey(0, 1) });
            colour.color = gradient;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            return ps;
        }

        /// <summary>Water stream of an indoor hydrant nozzle: faster, heavier and narrower than extinguisher powder.</summary>
        public static ParticleSystem Water(Transform parent, Material material)
        {
            var ps = Powder(parent, material);
            ps.gameObject.name = "방수";
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(11f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(.06f, .14f);
            main.startColor = new Color(.78f, .88f, 1f, .45f);
            main.gravityModifier = .9f;
            var shape = ps.shape;
            shape.angle = 3;
            var size = ps.sizeOverLifetime;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .5f), new Keyframe(1, 2.4f)));
            return ps;
        }

        /// <summary>A small heap of spilt powder lying flat on the floor (particles that stay where they fell).</summary>
        public static ParticleSystem Pile(Transform parent, Material material, Color colour, float radius)
        {
            var ps = Make(parent, "흩어진 가루", material, new Vector3(0, .02f, 0));
            var main = ps.main;
            main.loop = false;
            main.duration = 1;
            main.startLifetime = 100000f;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(.08f, .22f);
            main.startColor = colour;
            main.maxParticles = 80;
            main.gravityModifier = 0;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(90, 0, 0);
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 60) });
            ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            ps.Play();
            return ps;
        }

        /// <summary>Water pouring from a ceiling <paramref name="height"/> m above the floor point; <paramref name="flow"/> scales the rate.</summary>
        public static ParticleSystem Stream(Transform parent, Material material, float height, float flow)
        {
            height = Mathf.Max(1.5f, height - .1f);
            var ps = Make(parent, "쏟아지는 물", material, new Vector3(0, height, 0));
            var main = ps.main;
            const float gravity = 1.2f;
            main.startLifetime = Mathf.Sqrt(2 * height / (9.81f * gravity));
            main.startSpeed = new ParticleSystem.MinMaxCurve(.1f, .5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.05f, .12f);
            main.startColor = new Color(.75f, .85f, .95f, .5f);
            main.gravityModifier = gravity;
            main.maxParticles = 900;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .12f;
            shape.rotation = new Vector3(90, 0, 0);
            var emission = ps.emission;
            emission.rateOverTime = 120 * flow;
            ps.Play();
            return ps;
        }

        private static ParticleSystem Make(Transform parent, string name, Material material, Vector3 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            return ps;
        }
    }

    /// <summary>Small props: real models and baked meshes from <see cref="EmergencyArt"/>, placed at runtime.</summary>
    public static class Props
    {
        /// <summary>
        /// A carry-on hard-shell spinner (<see cref="EmergencyArt.SuitcaseModel"/>, 36 × 24 × 60 cm) standing on
        /// <paramref name="floor"/>, with a box collider round it so it can be looked at and inspected.
        /// </summary>
        public static GameObject Suitcase(Transform parent, Vector3 floor, Quaternion rotation, EmergencyArt art)
        {
            var root = Object.Instantiate(art.SuitcaseModel, floor, rotation, parent);
            root.name = "여행가방";
            var bounds = LocalBounds(root.transform);
            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
            return root;
        }

        /// <summary>Bounds of every mesh under <paramref name="root"/>, in the root's own space.</summary>
        public static Bounds LocalBounds(Transform root)
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = toRoot.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { bounds = new Bounds(corner, Vector3.zero); any = true; }
                    else bounds.Encapsulate(corner);
                }
            }
            return bounds;
        }

        /// <summary>A retractable-belt stanchion (<see cref="EmergencyArt.Stanchion"/>: steel post on a weighted base, black belt head) standing on <paramref name="floor"/>.</summary>
        public static GameObject Stanchion(Transform parent, Vector3 floor, EmergencyArt art)
        {
            var go = new GameObject("벨트 차단봉");
            go.transform.SetParent(parent, false);
            go.transform.position = floor;
            go.AddComponent<MeshFilter>().sharedMesh = art.Stanchion;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { art.CordonPost, art.CordonHead };
            return go;
        }

        /// <summary>Length of belt (m) one repeat of the printed belt texture covers (1024×64 px, so the print keeps its shape on a 5 cm belt).</summary>
        public const float BeltRepeat = .8f;

        /// <summary>
        /// A flat strap between two points: the cordon belt (5 cm high, printed on both faces so the lettering reads left to right
        /// from either side, the print repeated along its length) or, narrower, a bag strap. <paramref name="repeat"/> is the
        /// length (m) one texture repeat covers.
        /// </summary>
        public static GameObject Belt(Transform parent, Vector3 a, Vector3 b, Material material, float height = .05f, float repeat = BeltRepeat, string name = "통제 벨트")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation((a + b) * .5f, Quaternion.LookRotation(b - a));
            float half = Vector3.Distance(a, b) * .5f, h = height * .5f, u = half * 2 / repeat;
            var mesh = new Mesh { name = name };
            // 왼쪽 면(-x)에서 보면 오른쪽이 -z, 오른쪽 면(+x)에서 보면 +z 다. 두 면 모두 시계 방향으로 감고 글자가 바로 읽히게 u 를 준다.
            mesh.vertices = new[]
            {
                new Vector3(0, -h, half), new Vector3(0, h, half), new Vector3(0, h, -half), new Vector3(0, -h, -half),
                new Vector3(0, -h, -half), new Vector3(0, h, -half), new Vector3(0, h, half), new Vector3(0, -h, half),
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(u, 1), new Vector2(u, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(u, 1), new Vector2(u, 0) };
            mesh.normals = new[] { Vector3.left, Vector3.left, Vector3.left, Vector3.left, Vector3.right, Vector3.right, Vector3.right, Vector3.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A prop from a mesh asset at <paramref name="parent"/>'s origin (the mesh carries its own size and pivot).</summary>
        public static GameObject Model(Transform parent, string name, Mesh mesh, params Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        /// <summary>A round steel litter bin (0.4 m across, 0.75 m tall) standing on <paramref name="floor"/>.</summary>
        public static GameObject LitterBin(Transform parent, Vector3 floor, EmergencyArt art)
        {
            var bin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bin.name = "휴지통";
            bin.transform.SetParent(parent, true);
            bin.transform.SetPositionAndRotation(floor + Vector3.up * .375f, Quaternion.identity);
            bin.transform.localScale = new Vector3(.4f, .375f, .4f);
            bin.GetComponent<Renderer>().sharedMaterial = art.CordonPost;
            return bin;
        }

        /// <summary>A shallow puddle on the floor: a flat soft-edged disc (scale it to the wet diameter).</summary>
        public static GameObject Puddle(Transform parent, Vector3 floor, Material material)
        {
            var go = new GameObject("물웅덩이");
            go.transform.SetParent(parent, false);
            go.transform.position = floor + Vector3.up * .012f;
            var mesh = new Mesh { name = "물웅덩이" };
            mesh.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 0, -.5f) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(material) { name = "물웅덩이", color = new Color(.32f, .4f, .48f, .6f) };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
