using System;
using ChooGuard.App.Fps.Equipment;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// A fire that something keeps supplying: an energized circuit that goes on arcing and re-lighting it, a gas line that
    /// keeps feeding the flame. While the feed is live the fire can be beaten down to its embers but never put out, whoever
    /// sprays it (<see cref="FeedFloor"/>); cutting the feed (a breaker down, the plug out, the valve shut) is what lets
    /// the extinguisher finish it. The staff member cuts it, or the agency that can (<see cref="FeedCutBy"/>: the electrician,
    /// the gas company) comes for it. Water on a live electrical fire is dangerous (<see cref="WaterIsDangerous"/>): the stream
    /// conducts back to the person holding the nozzle. Other fires ignore all of this.
    /// </summary>
    public sealed partial class FireHazard
    {
        /// <summary>Intensity a live feed keeps the fire at however it is sprayed: the arc or the gas jet re-lights it.</summary>
        public const float LiveEmbers = .12f;

        /// <summary>What supplies the fire now ("전원", "가스"), or null when nothing does (never fed, or cut).</summary>
        public string Feed { get; private set; }
        /// <summary>What supplied the fire, kept after the cut.</summary>
        public string FeedName { get; private set; }
        /// <summary>The agency that can cut the feed by hand while it is live (the office sends it), or null.</summary>
        public Agency? FeedCutBy => Feed != null ? feedCutBy : null;
        /// <summary>How long the agency works to cut it (s).</summary>
        public float FeedWorkSeconds { get; private set; }
        /// <summary>The fire burns in an electrical installation: the electrician comes, the water advice differs.</summary>
        public bool Electric { get; set; }
        /// <summary>Spraying water while the feed is live shocks whoever holds the hose (an electrical feed).</summary>
        public bool WaterIsDangerous { get; set; }
        /// <summary>The installation the fire started in (a vending machine, a kiosk, a distribution board), or null.</summary>
        public StationEquipment Installation { get; set; }
        /// <summary>Who cut the feed and how, once it is cut.</summary>
        public string CutBy { get; private set; }

        /// <summary>Water reached a live fire (raised at most every 6 s): the director reacts.</summary>
        public event Action<FireHazard> WetWhileLive;
        /// <summary>The feed was cut.</summary>
        public event Action<FireHazard> FeedCut;

        private Agency? feedCutBy;
        private float lastWet = -100;

        private float FeedFloor => Feed != null ? Math.Min(LiveEmbers, Intensity) : 0f;

        private string FeedNote => FeedName == null ? "" : Feed != null ? " · " + FeedName + " 통전 중" : " · " + FeedName + " 차단됨" + (Inspected ? "·전기 담당 점검함" : "");

        /// <summary>Starts the feed: <paramref name="name"/> keeps the fire supplied until it is cut, by hand or by <paramref name="cutBy"/>.</summary>
        public void SetFeed(string name, Agency? cutBy, float workSeconds)
        {
            Feed = name;
            FeedName = name;
            feedCutBy = cutBy;
            FeedWorkSeconds = workSeconds;
            CutBy = null;
        }

        /// <summary>Cuts the feed. Returns false when nothing was feeding the fire.</summary>
        public bool CutFeed(string by)
        {
            if (Feed == null) return false;
            Feed = null;
            CutBy = by;
            FeedCut?.Invoke(this);
            return true;
        }

        /// <summary>Puts the feed back (a breaker switched on again): the fire, if it still burns, is supplied again.</summary>
        public bool Restore()
        {
            if (Feed != null || FeedName == null || Extinguished) return false;
            Feed = FeedName;
            CutBy = null;
            return true;
        }

        /// <summary>The agency that cut or should cut the feed has checked the installation afterwards (a cut circuit stays down until it has).</summary>
        public bool Inspected { get; private set; }

        private const float InspectSeconds = 20f;

        public override float WorkSeconds(Agency agency)
        {
            if (feedCutBy != agency) return 0;
            if (Feed != null) return FeedWorkSeconds;
            return Electric && FeedName != null && !Inspected ? InspectSeconds : 0;
        }

        public override string Resolve(Agency agency)
        {
            if (feedCutBy != agency) return null;
            if (Feed != null)
            {
                CutFeed(Electric ? "전기 담당" : Responder.AgencyName(agency));
                return Electric
                    ? "전기 담당입니다. " + Where + " 전원을 차단했습니다. 점검이 끝날 때까지 다시 넣지 마십시오."
                    : Responder.AgencyName(agency) + "입니다. " + Where + " " + FeedName + "을 차단했습니다.";
            }
            if (!Electric || Inspected) return null;
            Inspected = true;
            return "전기 담당입니다. " + Where + " 차단 상태와 배선을 확인했습니다. 교체하기 전까지 그 회로는 내려 둡니다.";
        }

        // 물이 닿은 통전 화재: 방수하는 사람이 감전된다(반응은 사건 쪽에서).
        private void WaterApplied()
        {
            if (Feed == null || !WaterIsDangerous || UnityEngine.Time.time - lastWet < 6f) return;
            lastWet = UnityEngine.Time.time;
            WetWhileLive?.Invoke(this);
        }
    }
}
