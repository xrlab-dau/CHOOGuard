using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// A passenger (often drunk) turns aggressive: shouting, shoving, throwing things, threatening people with an object.
    /// It is heard before it is seen. Staff keep their distance, report to 112 and the railway police and move people
    /// away; the police restrain the person and lead them out (public guidance, research.md). The person is moved by the
    /// incident director while <see cref="Passenger.Activity.Aggressive"/>.
    /// </summary>
    public sealed class DisturbanceHazard : Hazard
    {
        private static readonly string[] Seen =
        {
            "술에 취한 듯한 승객이 큰 소리로 욕설을 하며 소리침", "승객이 소리치며 주변 사람을 밀침", "승객이 소리치며 물건을 집어 던짐",
            "승객이 손에 든 물건을 휘두르며 사람들을 위협함", "승객이 휘두른 물건에 다른 승객이 맞아 쓰러짐",
        };

        public Passenger Person { get; }
        /// <summary>0 shouting .. 4 someone was hit.</summary>
        public int Level { get; private set; }
        public bool Restrained { get; private set; }
        /// <summary>Where it started: the person keeps to the area around it.</summary>
        public Vector3 Home { get; }

        public override string Label => "난동 승객";
        public override float NoticeRadius => Restrained ? 0 : 14 + 4 * Level;
        public override bool NeedsSight => false;
        public override bool Localized => true;
        public override string SensedAs => "고함 소리를 들음";
        public override float DangerRadius => Restrained ? 0 : 2.5f + 1.2f * Level;
        public override float Clearance => DangerRadius + 8;
        public override string Visible => Restrained ? "경찰관이 소란을 피우던 승객을 제지함" : Seen[Level];

        public override Agency Command => Agency.Police;
        public override Agency? CitizenCalls => Agency.Police;
        public override string ToldByPassenger => Where + "에서 어떤 사람이 난동을 부려요! " + (Level >= 3 ? "뭘 휘두르고 있어요!" : "소리 지르고 사람을 밀어요!");
        public override string ReportLine => "역무실, " + Where + "에 난동 승객 있습니다. " + (Level >= 3 ? "물건을 휘두르며 사람들을 위협하고 있습니다." : "소리치며 주변 승객과 시비 중입니다.");
        public override string OfficeReply => "역무실 수신. 112와 철도경찰에 신고하겠습니다. 직접 제지하지 말고 거리를 둔 채 주변 승객을 떨어뜨려 주십시오.";
        public override PaRequest Announcement => new PaRequest("안내방송(주변 대피) 요청", PaLine.SuspiciousItem,
            "안내 말씀 드립니다. " + Where + " 주변에서 안전 확인을 하고 있습니다. 해당 구역에서 떨어져 직원 안내에 따라 이동해 주십시오.", PaScope.EvacuateArea, 30);
        public override string State => Restrained ? "경찰 제지" : Level >= 3 ? "위협 중" : "소란 중";
        public override string BoardState => Visible;
        public override bool UnderControl => Restrained || !Active;
        public override string Handover => Named + (Restrained ? " 경찰이 제지함" : " 계속 소란");

        public DisturbanceHazard(string id, Passenger person, int level)
        {
            Id = id;
            Kind = HazardKind.Disturbance;
            Person = person;
            Level = Mathf.Clamp(level, 0, 4);
            Home = person.transform.position;
            Position = Home;
            StartedAt = Time.time;
        }

        public void Escalate(int step) => Level = Mathf.Clamp(Level + step, 0, 4);

        public void Restrain() => Restrained = true;

        public override void Tick(float deltaSeconds)
        {
            if (Person == null) { End(); return; }
            Position = Person.transform.position;
        }
    }

    /// <summary>
    /// A phoned threat to the station office that an explosive has been placed in the station. Nothing is seen: the office
    /// tells staff and calls the police at once; staff look out for unattended items without touching them and ask for
    /// the announcement; the police search (research.md). <see cref="Level"/>: how specific and credible the call is.
    /// </summary>
    public sealed class BombThreatHazard : Hazard
    {
        private static readonly string[] Calls =
        {
            "장난처럼 들리는 짧은 협박 전화", "장소를 말하지 않은 협박 전화", "역 안 특정 장소를 말한 협박 전화",
            "장소와 시각을 말한 협박 전화", "여러 곳에 설치했다는 구체적인 협박 전화",
        };

        public int Level { get; private set; }
        /// <summary>The part of the station the caller named ("역 안 어딘가" when none).</summary>
        public string Claimed { get; private set; }
        public bool Searched { get; private set; }

        public override string Label => "폭발물 협박";
        public override float NoticeRadius => 0;
        public override bool NeedsSight => false;
        public override bool Localized => false;
        public override bool HasScene => true;
        public override float DangerRadius => 0;
        public override string Visible => "역무실에 " + Calls[Level] + "가 걸려 옴 · " + Claimed;

        public override Agency Command => Agency.Police;
        public override string ReportOption => "폭발물 협박 순회 상황 보고";
        public override string ReportLine => "역무실, 협박 전화 관련 " + Claimed + " 쪽 순회하겠습니다. 수상한 물건은 만지지 않고 보고하겠습니다.";
        public override string OfficeReply => Level >= 3
            ? "역무실 수신. 경찰이 역 전체 대피를 요청했습니다. 대피 안내방송 하겠습니다. 수상한 물건은 절대 만지지 마십시오."
            : "역무실 수신. 경찰 도착 전까지 수상한 물건이 있는지 살피고, 발견하면 만지지 말고 주변을 통제하십시오.";
        public override PaRequest Announcement => new PaRequest("대피 안내방송 요청", PaLine.SafetyEvacuation,
            "안내 말씀 드립니다. 역 안 안전 점검을 위해 승객 여러분께서는 직원의 안내에 따라 역 밖으로 대피해 주시기 바랍니다.", PaScope.EvacuateStation);
        public override string State => Searched ? "수색 끝" : "경찰 수색 필요";
        public override bool UnderControl => Searched || !Active;
        public override string Handover => "폭발물 협박(" + Claimed + ")" + (Searched ? " 수색 결과 이상 없음" : " 수색 전");
        public override float WorkSeconds(Agency agency) => agency == Agency.Police ? 40 : 0;

        public override string Resolve(Agency agency)
        {
            Searched = true;
            End();
            return "철도경찰입니다. " + Claimed + " 수색 결과 폭발물로 보이는 물건은 없습니다. 계속 주의 바랍니다.";
        }

        public BombThreatHazard(string id, Vector3 claimedSpot, string claimed, int level)
        {
            Id = id;
            Kind = HazardKind.BombThreat;
            Position = claimedSpot;
            Claimed = claimed;
            Level = Mathf.Clamp(level, 0, 4);
            Where = "역무실 전화";
            StartedAt = Time.time;
        }

        /// <summary>The caller rings again, more specific (a named place, a time).</summary>
        public void CallsAgain(Vector3 spot, string claimed)
        {
            Level = Mathf.Min(4, Level + 1);
            Position = spot;
            Claimed = claimed;
        }
    }

    /// <summary>
    /// An envelope or package spilling an unknown white powder near seats. People close by cough and their eyes sting. Do
    /// not touch it, keep people away and let those who touched it wait apart; 119 hazmat and the police deal with it
    /// (research.md). <see cref="Level"/>: how much spilled and how people react.
    /// </summary>
    public sealed class SuspiciousSubstanceHazard : Hazard
    {
        private static readonly string[] Seen =
        {
            "의자 옆 바닥에 흰 가루가 조금 흩어져 있음", "뜯어진 봉투에서 흰 가루가 쏟아져 있음", "흰 가루 주변 사람들이 기침을 함",
            "흰 가루 가까이 있던 사람들이 눈과 목이 따갑다며 괴로워함", "흰 가루 가까이 있던 사람이 숨쉬기 힘들어하며 주저앉음",
        };

        public int Level { get; }
        public GameObject View { get; }

        public override string Label => "의심 물질";
        public override float NoticeRadius => Active ? 6 : 0;
        public override float DangerRadius => 1.5f + .6f * Level;
        public override float Clearance => 12;
        public override float StaffSightRange => 7;
        public override string Visible => Active ? Seen[Level] : "흰 가루를 수거함";
        public override bool Irritates(Vector3 position) => Active && Level >= 2 && Mathf.Abs(position.y - Position.y) < 2.5f && (position - Position).sqrMagnitude < (1.5f + Level) * (1.5f + Level);

        public override Agency Command => Agency.Fire;
        public override bool Involves(Agency agency) => agency == Agency.Fire || agency == Agency.Police;
        public override Agency? CitizenCalls => Agency.Police;
        public override string ToldByPassenger => Where + "에 이상한 흰 가루가 쏟아져 있어요. 사람들이 기침해요!";
        public override string ReportLine => "역무실, " + Where + "에 정체를 알 수 없는 흰 가루가 쏟아져 있습니다." + (Level >= 2 ? " 주변 승객이 기침을 합니다." : "");
        public override string OfficeReply => "역무실 수신. 119와 112에 신고하겠습니다. 가루는 만지지 말고 주변 접근을 통제하십시오. 가루에 닿은 사람은 그 자리 가까이에서 따로 기다리게 하십시오.";
        public override IEnumerable<Agency> Dispatch
        {
            get
            {
                yield return Agency.Fire;
                yield return Agency.Police;
            }
        }
        public override PaRequest Announcement => new PaRequest("안내방송(주변 대피) 요청", PaLine.SuspiciousItem,
            "안내 말씀 드립니다. " + Where + " 주변에서 안전 확인을 하고 있습니다. 해당 구역에서 떨어져 직원 안내에 따라 이동해 주십시오.", PaScope.EvacuateArea, 30);
        public override string State => Active ? (Cordoned ? "통제 중" : "확인 필요") : "수거됨";
        public override bool UnderControl => Cordoned || !Active;
        public override string Handover => Named + (Active ? (Cordoned ? " 통제선 안에 그대로" : " 통제 없이 그대로") : " 소방이 수거");
        public override bool Cordonable => true;
        public override float CordonRadius => 5;
        public override float WorkSeconds(Agency agency) => agency == Agency.Fire ? 30 : 0;

        public override string Resolve(Agency agency)
        {
            End();
            if (View != null) Object.Destroy(View);
            return "소방대입니다. " + Where + " 흰 가루 수거했습니다. 검사 결과가 나올 때까지 통제는 그대로 두십시오.";
        }

        public SuspiciousSubstanceHazard(string id, Vector3 floor, int level, EmergencyArt art, Transform parent)
        {
            Id = id;
            Kind = HazardKind.Substance;
            Position = floor;
            Level = Mathf.Clamp(level, 0, 4);
            StartedAt = Time.time;
            View = new GameObject("의심 물질");
            View.transform.SetParent(parent, false);
            View.transform.position = floor;
            Particles.Pile(View.transform, art.Smoke, new Color(.96f, .96f, .93f, .95f), .25f + .12f * Level);
            var collider = View.AddComponent<SphereCollider>();
            collider.radius = .35f;
            collider.center = new Vector3(0, .1f, 0);
            var marker = View.AddComponent<HazardMarker>();
            marker.Hazard = this;
            marker.Name = "흰 가루";
        }
    }
}
