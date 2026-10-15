using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>Who of an agency walks in: the unit its call is for.</summary>
    public enum Team { Fire, Rescue, Hazmat, Ems, RailwayPolice, Patrol, BombSquad, Facility, Elevator, Gas, Electric, Crew }

    /// <summary>A vehicle that parks on the station square while its team works (lights on).</summary>
    public enum VehicleKind { None, FireEngine, Ambulance, PoliceCar, SwatVan }

    /// <summary>
    /// The unit an agency sends, chosen by what it is coming for: 119 sends its fire unit, rescue unit (someone on the
    /// track, people trapped in a lift), hazmat unit (an unknown powder) or an ambulance crew; the police send the
    /// station's railway police, with 112 patrol officers for a disturbance and the police special unit's bomb
    /// technicians for a threat or a left bag; a facility call brings station staff or the elevator, gas or electrical
    /// contractor. The team decides who walks in, what they carry, how long they take and what the radio calls them;
    /// the agency still decides command and the handover.
    /// </summary>
    public static class Teams
    {
        public static Team For(Agency agency, Hazard target)
        {
            switch (agency)
            {
                case Agency.Fire:
                    if (target is SuspiciousSubstanceHazard) return Team.Hazmat;
                    if (target is TrackFallHazard || target is ElevatorTrapHazard) return Team.Rescue;
                    return Team.Fire;
                case Agency.Police:
                    if (target is BombThreatHazard || target is SuspiciousItemHazard) return Team.BombSquad;
                    if (target is DisturbanceHazard) return Team.Patrol;
                    return Team.RailwayPolice;
                case Agency.Medical: return Team.Ems;
                case Agency.Crew: return Team.Crew;
                default:
                    if (target is ElevatorTrapHazard) return Team.Elevator;
                    if (target is GasLeakHazard) return Team.Gas;
                    if (target is PowerOutageHazard || target is FireHazard { Electric: true }) return Team.Electric;
                    return Team.Facility;
            }
        }

        /// <summary>The agency a unit belongs to (it commands, takes handovers and is called as that agency).</summary>
        public static Agency AgencyOf(Team team)
        {
            switch (team)
            {
                case Team.Fire:
                case Team.Rescue:
                case Team.Hazmat: return Agency.Fire;
                case Team.Ems: return Agency.Medical;
                case Team.RailwayPolice:
                case Team.Patrol:
                case Team.BombSquad: return Agency.Police;
                case Team.Crew: return Agency.Crew;
                default: return Agency.Facility;
            }
        }

        /// <summary>
        /// Whether <paramref name="team"/> has something to do at <paramref name="hazard"/>: its agency must be involved (<see cref="Hazard.Involves"/>, which
        /// every kind answers), and on the facility side only the person for that installation can work it (the gas company a gas leak, the lift engineer a
        /// trapped car, the electrician a power fault: the unit <see cref="For"/> would send). An unknown kind falls back to its agency.
        /// </summary>
        public static bool Fits(Team team, Hazard hazard)
        {
            if (hazard == null) return false;
            var agency = AgencyOf(team);
            if (!hazard.Involves(agency)) return false;
            return agency != Agency.Facility || team == For(Agency.Facility, hazard);
        }

        public static string Name(Team team)
        {
            switch (team)
            {
                case Team.Fire: return "소방대";
                case Team.Rescue: return "119 구조대";
                case Team.Hazmat: return "119 화학구조대";
                case Team.Ems: return "구급대";
                case Team.RailwayPolice: return "철도경찰";
                case Team.Patrol: return "철도경찰·지구대";
                case Team.BombSquad: return "경찰특공대";
                case Team.Elevator: return "승강기 유지보수 기사";
                case Team.Gas: return "도시가스 안전점검원";
                case Team.Electric: return "전기 담당";
                case Team.Crew: return "열차 승무원";
                default: return "시설 담당";
            }
        }

        /// <summary>What a member is called when looked at: <paramref name="index"/> 0 is the team lead.</summary>
        public static string Member(Team team, int index)
        {
            bool lead = index == 0;
            switch (team)
            {
                case Team.Fire: return lead ? "소방대 선착 대장" : "소방대원";
                case Team.Rescue: return lead ? "구조대장" : "구조대원";
                case Team.Hazmat: return lead ? "화학구조대장" : "화학구조대원";
                case Team.Ems: return "구급대원";
                case Team.RailwayPolice: return lead ? "철도경찰 팀장" : "철도경찰관";
                case Team.Patrol: return lead ? "철도경찰 팀장" : "지구대 경찰관";
                case Team.BombSquad: return lead ? "철도경찰 팀장" : index == 1 ? "폭발물처리요원" : "경찰특공대원";
                case Team.Elevator: return "승강기 유지보수 기사";
                case Team.Gas: return "도시가스 안전점검원";
                case Team.Electric: return "전기 담당 직원";
                case Team.Crew: return lead ? "열차팀장" : "승무원";
                default: return "시설 담당 직원";
            }
        }

        /// <summary>The prefabs that walk in, lead first. Older catalogs without a team fall back to the agency's people.</summary>
        public static GameObject[] Members(CrowdCatalog crowd, Team team)
        {
            GameObject[] Or(GameObject[] wanted, GameObject[] fallback) => wanted != null && wanted.Length > 0 ? wanted : fallback;
            var railway = crowd.Police;
            switch (team)
            {
                case Team.Fire: return crowd.Firefighters;
                case Team.Rescue: return Or(crowd.Rescuers, crowd.Firefighters);
                case Team.Hazmat: return Or(crowd.HazmatTeam, crowd.Firefighters);
                case Team.Ems: return crowd.Paramedics.Length == 1 ? new[] { crowd.Paramedics[0], crowd.Paramedics[0] } : crowd.Paramedics;
                case Team.Patrol: return crowd.PatrolPolice.Length > 0 && railway.Length > 0 ? new[] { railway[0], crowd.PatrolPolice[0] } : railway;
                case Team.BombSquad:
                    if (crowd.BombSquad.Length == 0 || railway.Length == 0) return railway;
                    var squad = new GameObject[crowd.BombSquad.Length + 1];
                    squad[0] = railway[0];
                    crowd.BombSquad.CopyTo(squad, 1);
                    return squad;
                case Team.RailwayPolice: return railway;
                case Team.Elevator: return Or(crowd.ElevatorTechnicians, new[] { crowd.Colleague });
                case Team.Gas: return Or(crowd.GasTechnicians, new[] { crowd.Colleague });
                case Team.Electric: return Or(crowd.Electricians, new[] { crowd.Colleague });
                case Team.Crew: return Or(crowd.TrainCrew, new[] { crowd.Colleague });
                default: return Or(crowd.FacilityStaff, new[] { crowd.Colleague });
            }
        }

        /// <summary>
        /// Seconds from the call to walking in (compressed like the rest of the shift): crew are on the train, station
        /// staff and the railway police are in the building, 119 comes from the nearest station, specialist units and
        /// contractors from further away.
        /// </summary>
        public static Vector2 Delay(Team team)
        {
            switch (team)
            {
                case Team.Crew: return new Vector2(15, 30);
                case Team.Facility:
                case Team.Electric: return new Vector2(50, 80);
                case Team.RailwayPolice:
                case Team.Patrol: return new Vector2(80, 120);
                case Team.Elevator: return new Vector2(110, 150);
                case Team.Hazmat:
                case Team.BombSquad:
                case Team.Gas: return new Vector2(150, 200);
                default: return new Vector2(100, 140);
            }
        }

        public static VehicleKind Vehicle(Team team)
        {
            switch (team)
            {
                case Team.Fire:
                case Team.Rescue:
                case Team.Hazmat: return VehicleKind.FireEngine;
                case Team.Ems: return VehicleKind.Ambulance;
                case Team.Patrol: return VehicleKind.PoliceCar;
                case Team.BombSquad: return VehicleKind.SwatVan;
                default: return VehicleKind.None;
            }
        }
    }
}
