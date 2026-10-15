using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// A person down on the track beside a platform: fallen (dizziness, a stumble at the edge) or climbed down to fetch a
    /// dropped item. Trains into that platform must be stopped; staff never go down themselves, they call the office (which
    /// asks traffic control to stop trains) and 119 rescue, and keep talking to the person (research.md). The person is
    /// brought back up by the rescue team.
    /// </summary>
    public sealed class TrackFallHazard : Hazard
    {
        private static readonly string[] FellSeen =
        {
            "승강장에서 떨어진 승객이 선로에서 스스로 일어섬", "선로에 떨어진 승객이 발목을 다쳐 올라오지 못함", "선로에 떨어진 승객이 일어서지 못함",
            "선로에 떨어진 승객이 머리를 다쳐 누워 있음", "선로에 떨어진 승객이 움직이지 않음",
        };

        private static readonly string[] ClimbSeen =
        {
            "승객이 떨어뜨린 물건을 주우러 선로로 내려감", "선로로 내려간 승객이 다시 올라오려고 애씀", "선로로 내려간 승객이 올라오지 못하고 선로 위를 서성임",
            "선로로 내려간 승객이 올라오다 미끄러져 다리를 다침", "선로로 내려간 승객이 올라오다 떨어져 누워 있음",
        };

        public Passenger Person { get; }
        public string Platform { get; }
        /// <summary>Climbed down on purpose (to fetch an item) rather than fell.</summary>
        public bool Deliberate { get; }
        public int Level { get; private set; }
        /// <summary>The person is hurt (needs the paramedics once brought up).</summary>
        public bool Hurt => Deliberate ? Level >= 3 : Level >= 1;
        /// <summary>Down on the track the KTX set uses: its arrival is stopped short until the track is clear.</summary>
        public bool OnTrainTrack { get; }
        /// <summary>Where the person is brought back up to (on the platform, at the edge).</summary>
        public Vector3 Edge { get; }
        public bool Rescued { get; private set; }

        public override string Label => Deliberate ? "선로 진입" : "선로 추락";
        public override float NoticeRadius => Rescued ? 0 : 25;
        public override float StaffSightRange => 25;
        public override float DangerRadius => 0;
        public override float Clearance => 0;
        public override string Visible => Rescued ? "선로에 있던 승객을 승강장으로 구조함" : (Deliberate ? ClimbSeen : FellSeen)[Level];

        public override Agency Command => Agency.Fire;
        public override bool Involves(Agency agency) => agency == Agency.Fire || Hurt && agency == Agency.Medical;
        public override Agency? CitizenCalls => Agency.Fire;
        public override string ToldByPassenger => Platform + " 선로에 사람이 있어요! 열차 좀 세워 주세요!";
        public override string ReportLine => "역무실, " + Platform + " 선로에 승객이 " + (Deliberate ? "내려가 있습니다." : "떨어졌습니다.") + " 열차 진입 막아 주십시오." + (Hurt ? " 다친 것 같습니다." : "");
        public override string OfficeReply => "역무실 수신. 관제에 " + Platform + " 열차 진입 중지 요청했습니다. 119 구조 요청하겠습니다. 선로로 내려가지 말고 승객이 움직이지 않게 말로 안내하십시오.";
        public override IEnumerable<Agency> Dispatch
        {
            get
            {
                yield return Agency.Fire;
                if (Hurt) yield return Agency.Medical;
            }
        }
        public override string TrainHold => OnTrainTrack && !Rescued ? "선로에 사람" : null;
        public override PaRequest Announcement => new PaRequest("안내방송(선로 안전) 요청", PaLine.TrackSafety,
            "안내 말씀 드립니다. " + Platform + " 선로에 사람이 있어 열차 운행을 잠시 멈춥니다. 승객 여러분께서는 선로로 내려가지 마시고 안전선 밖으로 물러서 주십시오.", PaScope.ClearAround, 12);
        public override string State => Rescued ? "구조됨" : "선로 위";
        public override bool UnderControl => Rescued;
        public override string Handover => Platform + " " + Label + (Rescued ? " · 구조함" : " · 선로 위") + (Hurt ? " · 부상" : "");
        public override Vector3 Scene => Edge;
        public override float WorkSeconds(Agency agency) => agency == Agency.Fire && !Rescued ? 20 : 0;

        public override string Resolve(Agency agency)
        {
            Rescued = true;
            End();
            return "소방대입니다. 선로 위 승객을 구조해 승강장으로 올렸습니다." + (Hurt ? " 구급대에 인계하겠습니다." : "");
        }

        public TrackFallHazard(string id, Passenger person, string platform, bool deliberate, int level, bool onTrainTrack, Vector3 bed, Vector3 edge)
        {
            Id = id;
            Kind = HazardKind.TrackFall;
            Person = person;
            Platform = platform;
            Deliberate = deliberate;
            Level = Mathf.Clamp(level, 0, 4);
            OnTrainTrack = onTrainTrack;
            Position = bed;
            Edge = edge;
            StartedAt = Time.time;
        }

        public void Worsen() => Level = Mathf.Min(4, Level + 1);

        /// <summary>Climbed back up with bystanders' help before the rescue team came.</summary>
        public void ClimbedOut()
        {
            Rescued = true;
            End();
        }
    }
}
