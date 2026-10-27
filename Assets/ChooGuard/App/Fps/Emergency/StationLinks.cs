using System.Collections;
using System.Collections.Generic;
using ChooGuard.Circulation;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>A navmesh link people ride instead of walking (escalator belt, elevator car).</summary>
    public interface IStationLink
    {
        string Label { get; }
        /// <summary>Moves <paramref name="body"/> from the link start to its end. Must finish at the end position.</summary>
        IEnumerator Traverse(PersonBody body, OffMeshLinkData data);
    }

    /// <summary>What the staff member can see right now. Spawning, vanishing into elevators and toilets wait until unseen.</summary>
    public static class PlayerView
    {
        public static Camera Camera;
        private static readonly RaycastHit[] hits = new RaycastHit[8];

        public static bool Sees(Vector3 point, float range)
        {
            if (Camera == null) return false;
            var eye = Camera.transform.position;
            var to = point + Vector3.up - eye;
            float distance = to.magnitude;
            if (distance > range) return false;
            if (distance < 3f) return true;
            float halfWidth = Mathf.Atan(Mathf.Tan(Camera.fieldOfView * .5f * Mathf.Deg2Rad) * Camera.aspect) * Mathf.Rad2Deg;
            if (Vector3.Angle(Camera.transform.forward, to) > halfWidth + 8) return false;
            int count = Physics.RaycastNonAlloc(eye, to / distance, hits, distance - .5f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (hits[i].collider.GetComponentInParent<PersonBody>() == null) return false;
            return true;
        }
    }

    /// <summary>
    /// One escalator belt. People step on at the boarding landing, stand at belt speed (0.5 m/s, 승강기 안전기준 별표 24)
    /// about two steps apart, and walk off at the other landing. A stopped belt is walked like stairs; a closed one, or one
    /// whose entry flap gate is shut, is not offered to path finding. Link traversal follows Unity NavMeshComponents
    /// AgentLinkMover (MIT, 2016).
    /// </summary>
    public sealed class Escalator : MonoBehaviour, IStationLink, IEscalatorDrive
    {
        public const float BeltSpeed = .5f, Spacing = 1.1f, StoppedWalk = .5f;

        private sealed class Rider { public PersonBody Body; public float S = -1; public bool Fallen; }

        public StationPoints.EscalatorEntry Entry { get; private set; }
        public string Label => Entry.label;
        public bool Running { get; private set; } = true;
        public float DriveSpeed => Running ? BeltSpeed : 0f;
        public double TravelDistance { get; private set; }
        public bool Closed { get; private set; }
        /// <summary>The flap gate at the boarding landing is shut (it follows a stopped belt): nobody is routed onto it.</summary>
        public bool EntryBarred { get; private set; }
        public int RiderCount => riders.Count;
        public Vector3 Middle => PointAt(length * .5f);
        public Vector3 Start => Entry.path[0];
        public Vector3 End => Entry.path[Entry.path.Length - 1];

        private readonly List<Rider> riders = new List<Rider>();
        private NavMeshLinkInstance link;
        private float[] distance;
        private float length;

        public void Setup(StationPoints.EscalatorEntry entry, bool sourceNavigationAvailable = true)
        {
            Entry = entry;
            NavigationAvailable = sourceNavigationAvailable;
            distance = new float[entry.path.Length];
            for (int i = 1; i < entry.path.Length; i++) distance[i] = distance[i - 1] + Vector3.Distance(entry.path[i - 1], entry.path[i]);
            length = distance[distance.Length - 1];
            AddLink();
        }

        /// <summary>Binds supplied real part animations and ride volumes to this catalogued lane. Creates no geometry.</summary>
        public void BindDrive(Transform partsRoot)
        {
            if (partsRoot == null) return;
            foreach (var ride in partsRoot.GetComponentsInChildren<EscalatorRide>(true)) ride.Bind(this);
            foreach (var motion in partsRoot.GetComponentsInChildren<EscalatorMotion>(true)) motion.Bind(this);
        }

        private void Update() => AdvanceDrive(Time.deltaTime);

        /// <summary>Advances only the operating belt; exposed for deterministic mechanical checks.</summary>
        public void AdvanceDrive(float deltaSeconds)
        {
            if (!Running || deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
            TravelDistance += BeltSpeed * (double)deltaSeconds;
        }

        /// <summary>Logical controls remain available; only supplied physical machinery grants a path link.</summary>
        public bool NavigationAvailable { get; private set; } = true;
        public bool HasNavigationLink => NavMesh.IsLinkValid(link);
        public void SetNavigationAvailable(bool available)
        {
            NavigationAvailable=available;
            if(!available) { if(NavMesh.IsLinkValid(link))NavMesh.RemoveLink(link); }
            else if(!Closed&&!EntryBarred&&!NavMesh.IsLinkValid(link))AddLink();
        }

        private void AddLink()
        {
            if(!NavigationAvailable||Closed||EntryBarred||NavMesh.IsLinkValid(link))return;
            link = NavMesh.AddLink(new NavMeshLinkData { startPosition = Start, endPosition = End, width = 0, bidirectional = false, area = 3, costModifier = -1 });
            if (NavMesh.IsLinkValid(link)) NavMesh.SetLinkOwner(link, this);
        }

        private void OnDestroy()
        {
            if (NavMesh.IsLinkValid(link)) NavMesh.RemoveLink(link);
        }

        /// <summary>Why the steps stand still ("" while running): a stop button, the quake sensor, a staff closure.</summary>
        public string StoppedBy { get; private set; } = "";

        /// <summary>Emergency stop (earthquake sensor, someone pressed the stop button). People on it walk the rest.</summary>
        public void Stop(string cause = "비상정지 버튼")
        {
            if (Running) StoppedBy = cause;
            Running = false;
        }

        /// <summary>
        /// Restart with the key switch at the landing (research E2) — only with the steps clear and nobody fallen; a quake stop
        /// waits for inspection and a closed escalator stays closed.
        /// </summary>
        public bool Restart(out string reason)
        {
            reason = null;
            if (Running) { reason = "운행 중"; return false; }
            if (Closed) { reason = "이용 통제 중이라 재가동하지 않습니다"; return false; }
            if (StoppedBy == "지진 감지") { reason = "지진 감지로 정지 · 점검 전에는 재가동하지 않습니다"; return false; }
            riders.RemoveAll(r => r.Body == null);
            foreach (var r in riders) if (r.Fallen) { reason = "넘어진 승객이 있어 재가동할 수 없습니다"; return false; }
            if (riders.Count > 0) { reason = "디딤판에 사람이 있습니다 · 모두 내린 뒤 재가동"; return false; }
            Running = true;
            StoppedBy = "";
            return true;
        }

        /// <summary>Staff closes the escalator: path finding stops using it; people already on it finish.</summary>
        public void Close()
        {
            Closed = true;
            if (NavMesh.IsLinkValid(link)) NavMesh.RemoveLink(link);
        }

        /// <summary>
        /// The entry flap gate at the boarding landing shut or opened again. While shut the belt is not offered to path finding
        /// (unlike an ungated stopped belt, which people walk like stairs); people already on it finish.
        /// </summary>
        public void SetEntryBarred(bool barred)
        {
            if (barred == EntryBarred) return;
            EntryBarred = barred;
            if (barred) { if (NavMesh.IsLinkValid(link)) NavMesh.RemoveLink(link); }
            else if (!Closed && !NavMesh.IsLinkValid(link)) AddLink();
        }

        /// <summary>How many stop buttons were found at this escalator's landings and built (<see cref="AddStopButtons"/>).</summary>
        public int StopButtons { get; private set; }
        /// <summary>Someone standing at a landing can press a stop button here (one was observed and built).</summary>
        public bool HasStopButton => StopButtons > 0;

        /// <summary>
        /// Builds the stop buttons observed at this escalator's landings (<see cref="EscalatorStopSurvey"/>): each where its record puts it
        /// relative to the belt end at that landing (side of a rider, height, distance out onto the landing, distance from the centre line).
        /// A landing with no record gets no button.
        /// </summary>
        public void AddStopButtons(IReadOnlyList<EscalatorStopSurvey.Record> records, GameObject buttonModel = null)
        {
            // 경로는 타는 쪽 계단참 → 벨트 시작 … 벨트 끝 → 내리는 쪽 계단참이다. 계단참 점은 navmesh 로 옆으로 비켜 있을 수 있어
            // 벨트 끝(디딤판이 계단참 바닥과 만나는 곳)과 벨트 전체 방향을 기준으로 삼는다.
            int last = Entry.path.Length - 1, beltStart = last >= 3 ? 1 : 0, beltEnd = last >= 3 ? last - 1 : last;
            bool startIsBottom = Entry.path[beltStart].y <= Entry.path[beltEnd].y;
            // 디딤판이 움직이는 방향. 탄 사람의 오른쪽이 기록의 'right'다. 승강장 쪽(바깥)은 타는 곳에서는 반대 방향, 내리는 곳에서는 같은 방향이다.
            var travel = Horizontal(Entry.path[beltEnd] - Entry.path[beltStart]);
            var right = Vector3.Cross(Vector3.up, travel).normalized;
            foreach (var record in records)
            {
                if (record.escalator != Entry.id) continue;
                bool atStart = record.AtBottom == startIsBottom;
                var landing = atStart ? Entry.path[beltStart] : Entry.path[beltEnd];
                var outward = atStart ? -travel : travel;
                var sideways = right * (record.OnLeft ? -1f : 1f);
                var surveyed = landing + outward * record.along + sideways * record.offset + Vector3.up * record.height;
                if (!MountOnNamedTwin(record.mountObject, landing, outward, sideways, record.height, out var position, out var facing))
                    MountOnTwin(surveyed, outward, sideways, out position, out facing);
                EscalatorStopButton.Build(this, position, facing, buttonModel);
                StopButtons++;
            }
        }

        /// <summary>How far from the surveyed point the twin's own escalator surface may be for the plate to be put on it (m).</summary>
        public const float MountReach = .6f;
        // 이름이 주어진 트윈 메시에서 난간 끝을 찾는 범위: 벨트 끝에서 앞뒤로 이 거리(m)까지, 이 간격으로 훑는다. 옆으로는 이만큼 떨어진 곳에서 쏜다.
        private const float NewelSpan = 5f, NewelStep = .1f, NewelAcross = 8f;
        private static readonly float[] NewelInsets = { .005f, .02f, .04f, .07f, .1f, .14f, .2f };

        /// <summary>
        /// Puts the plate on the named twin mesh's balustrade end at this landing, on the record's side: from out on the landing inward,
        /// a horizontal probe across the lane at the record's height finds where that mesh's balustrade face on that side begins (the
        /// newel), and a probe from the landing back toward the belt finds its end face; the plate goes on the end face (facing out onto
        /// the landing), or on the balustrade face itself when there is no end face. The twin's donor escalator models may stand some
        /// metres from the navmesh belt path (2F-3F pair, 2026-10-27), so the path's own offsets are not trusted here. False when no
        /// collider of that name is in the scene or the mesh has no such face within <see cref="NewelSpan"/> of the landing.
        /// </summary>
        public static bool MountOnNamedTwin(string meshName, Vector3 landing, Vector3 outward, Vector3 sideways, float height, out Vector3 mounted, out Vector3 facing)
        {
            mounted = landing; facing = outward;
            if (string.IsNullOrEmpty(meshName)) return false;
            Collider twin = null;
            float nearest = float.PositiveInfinity;
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (c.name != meshName) continue;
                float d = c.bounds.SqrDistance(landing);
                if (d < nearest) { nearest = d; twin = c; }
            }
            if (twin == null) { Debug.LogWarning("[EscalatorStops] 붙일 트윈 메시를 찾지 못함: " + meshName); return false; }
            float y = landing.y + height;
            for (float a = NewelSpan; a >= -NewelSpan; a -= NewelStep)
            {
                var from = landing + outward * a + sideways * NewelAcross; from.y = y;
                if (!FaceOf(twin, from, -sideways, NewelAcross * 2f, sideways, out var side)) continue;
                // 끝면: 바깥면에서 조금씩 안쪽으로 들어간 선을 따라 승강장 쪽에서 벨트 쪽으로 쏜다(유리 끝은 얇고 둥글어 한 줄로는 비껴간다).
                foreach (float inset in NewelInsets)
                {
                    var endFrom = side.point - sideways * inset + outward * .6f;
                    if (FaceOf(twin, endFrom, -outward, 1.5f, outward, out var end)) { Mount(end, out mounted, out facing); return true; }
                }
                Mount(side, out mounted, out facing);
                return true;
            }
            Debug.LogWarning("[EscalatorStops] " + meshName + " 에서 벨트 끝 " + landing.ToString("F1") + " 근처의 난간 면을 찾지 못함");
            return false;
        }

        /// <summary>The nearest face of <paramref name="twin"/> along the ray whose normal leans toward <paramref name="wantNormal"/>.</summary>
        private static bool FaceOf(Collider twin, Vector3 origin, Vector3 direction, float length, Vector3 wantNormal, out RaycastHit face)
        {
            face = default;
            float best = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(origin, direction, length, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != twin || hit.distance >= best || Vector3.Dot(hit.normal, wantNormal) < .5f) continue;
                best = hit.distance; face = hit;
            }
            return best < float.PositiveInfinity;
        }

        private static void Mount(RaycastHit face, out Vector3 mounted, out Vector3 facing)
        {
            facing = face.normal; facing.y = 0; facing.Normalize();
            mounted = face.point + facing * .003f;
        }

        /// <summary>
        /// Puts the plate on the twin's own escalator geometry next to the surveyed point (a record gives the place to a few centimetres,
        /// the mesh the exact face): a horizontal probe through the point from out on the landing toward the belt finds the newel end;
        /// failing that, probes from the centre line outward and from outside inward find the balustrade faces. The hit nearest the
        /// point within <see cref="MountReach"/> is the mount, its face normal the plate's facing. Nothing near: the surveyed point stands.
        /// </summary>
        public static bool MountOnTwin(Vector3 point, Vector3 outward, Vector3 sideways, out Vector3 mounted, out Vector3 facing)
        {
            mounted = point; facing = outward;
            var probes = new[] { (point + outward * MountReach, -outward), (point - sideways * MountReach, sideways), (point + sideways * MountReach, -sideways) };
            for (int p = 0; p < probes.Length; p++)
            {
                float best = MountReach;
                bool found = false;
                foreach (var hit in Physics.RaycastAll(probes[p].Item1, probes[p].Item2, MountReach * 2f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    if (Mathf.Abs(hit.normal.y) > .7f) continue;   // 데크 윗면·바닥은 붙일 곳이 아니다
                    float away = Vector3.Distance(hit.point, point);
                    if (away >= best) continue;
                    if (hit.collider.GetComponentInParent<Escalator>() != null || hit.collider.GetComponentInParent<PersonBody>() != null || hit.collider.GetComponentInParent<FirstPersonResponder>() != null) continue;
                    var face = hit.normal; face.y = 0;
                    if (face.sqrMagnitude < 1e-4f) continue;
                    best = away; found = true;
                    facing = face.normalized;
                    mounted = hit.point + facing * .003f;
                }
                if (found) return true;   // 뉴얼 끝면이 있으면 그곳, 없을 때만 난간 옆면
            }
            return false;
        }

        private static Vector3 Horizontal(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        public Vector3 PointAt(float s)
        {
            s = Mathf.Clamp(s, 0, length);
            for (int i = 1; i < distance.Length; i++)
                if (s <= distance[i]) return Vector3.Lerp(Entry.path[i - 1], Entry.path[i], (s - distance[i - 1]) / Mathf.Max(1e-4f, distance[i] - distance[i - 1]));
            return End;
        }

        /// <summary>Distance from <paramref name="point"/> to the belt line.</summary>
        public float DistanceTo(Vector3 point)
        {
            float best = float.PositiveInfinity;
            for (int i = 1; i < Entry.path.Length; i++)
            {
                var a = Entry.path[i - 1]; var b = Entry.path[i];
                var ab = b - a;
                float t = ab.sqrMagnitude < 1e-6f ? 0 : Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector3.Distance(point, a + ab * t));
            }
            return best;
        }

        /// <summary>A rider fell on the steps: they stay where they are; people behind them stop too.</summary>
        public bool Fall(PersonBody body)
        {
            foreach (var r in riders) if (r.Body == body && r.S >= 0) { r.Fallen = true; return true; }
            return false;
        }

        /// <summary>The rider right behind <paramref name="body"/> on the belt, if any.</summary>
        public PersonBody Behind(PersonBody body)
        {
            int index = riders.FindIndex(r => r.Body == body);
            return index >= 0 && index + 1 < riders.Count && riders[index + 1].S >= 0 ? riders[index + 1].Body : null;
        }

        /// <summary>Whether <paramref name="body"/> is standing on the moving belt now.</summary>
        public bool Carries(PersonBody body)
        {
            foreach (var r in riders) if (r.Body == body && r.S >= distance[1]) return true;
            return false;
        }

        public IEnumerable<PersonBody> Bodies()
        {
            foreach (var r in riders) if (r.Body != null) yield return r.Body;
        }

        public IEnumerator Traverse(PersonBody body, OffMeshLinkData data)
        {
            var me = new Rider { Body = body };
            riders.Add(me);
            try
            {
                float boardS = distance[1], leaveS = distance[distance.Length - 2];
                var forward = Entry.path[1] - Entry.path[0];
                // 앞사람이 두 계단쯤 올라갈 때까지 계단참에서 기다린다.
                while (Ahead(me) is Rider waitFor && (waitFor.S < 0 || waitFor.S < boardS + Spacing))
                {
                    body.Ride(body.transform.position, forward);
                    yield return null;
                }
                yield return body.WalkLine(Entry.path[1], Mathf.Min(body.WalkSpeed, 1.2f));
                me.S = boardS;
                while (me.S < leaveS)
                {
                    if (me.Fallen) { body.transform.position = PointAt(me.S); yield return null; continue; }
                    float speed = Running ? DriveSpeed : StoppedWalk;
                    float next = me.S + speed * Time.deltaTime;
                    // 앞사람을 앞질러 가지 않는다(정지한 벨트를 걸어서 가도 앞사람 뒤에 붙는다).
                    if (Ahead(me) is Rider leader && leader.S > me.S) next = Mathf.Min(next, leader.S - Spacing);
                    next = Mathf.Max(me.S, next);
                    var position = PointAt(next);
                    var direction = PointAt(next + .3f) - position;
                    if (Running) body.Ride(position, direction);
                    else { body.transform.position = position; body.Move(direction, next > me.S ? speed : 0); }
                    me.S = next;
                    yield return null;
                }
                yield return body.WalkLine(End, body.WalkSpeed);
            }
            finally { riders.Remove(me); }
        }

        private Rider Ahead(Rider me)
        {
            // 사라진 사람(열차 출발·퇴장으로 지워진 몸)은 줄에서 뺀다.
            riders.RemoveAll(r => r.Body == null);
            int index = riders.IndexOf(me);
            return index > 0 ? riders[index - 1] : null;
        }
    }

    /// <summary>
    /// One elevator of the station data (a stop per floor). Where the twin has a working car — kit landing doors with a cabin
    /// behind them (<see cref="Facilities.ElevatorCar"/>) — riders call it, walk in when the doors open, stand in the car among
    /// whoever else is aboard (the staff member included), ride and walk out. Elsewhere the doors cannot open, so people enter
    /// and leave the car only while the staff member is not looking at that door (height at 1 m/s plus door time).
    /// </summary>
    public sealed class Elevator : MonoBehaviour, IStationLink
    {
        public const float Speed = 1f, DoorSeconds = 5f;
        public const int Capacity = 8;

        public StationPoints.ElevatorEntry Entry { get; private set; }
        public string Label => Entry.label;
        private bool serviceRunning = true;
        public bool Running => serviceRunning && NavigationAvailable;
        public bool NavigationAvailable { get; private set; }
        public string NavigationIssue { get; private set; } = "CURRENT_SOURCE_NOT_QUALIFIED";
        public readonly List<PersonBody> Inside = new List<PersonBody>();
        public int Waiting { get; private set; }

        private readonly List<NavMeshLinkInstance> links = new List<NavMeshLinkInstance>();
        private float carFreeAt, carY, lookAgainAt;
        private Facilities.ElevatorCar car;
        private int[] landing;
        private bool currentSourceRequired, currentSourceQualified;

        public void Setup(StationPoints.ElevatorEntry entry, bool sourceNavigationAvailable = true)
        {
            ReleaseLinks();NavigationAvailable=false;car=null;landing=null;
            currentSourceRequired=!sourceNavigationAvailable;currentSourceQualified=false;
            Entry = entry;
            carY = entry.stops[0].door.y;
            SetNavigationAvailable(sourceNavigationAvailable,sourceNavigationAvailable?"SUPPLIED_DATA_NAVIGATION":"CURRENT_SOURCE_NOT_QUALIFIED");
        }

        internal static string CurrentSourceDecision(int stops,int matchedStops,bool uniqueCompleteBank)
        {
            if(stops<2||matchedStops<0||matchedStops>stops)throw new System.ArgumentException("Invalid current elevator source partition");
            if(matchedStops==0)return "UNBOUND_NO_CURRENT_ELEVATOR_LANDING_SOURCE";
            if(matchedStops!=stops||!uniqueCompleteBank)
                throw new System.InvalidOperationException("Current elevator source bank is incomplete or ambiguous");
            return "BOUND_CURRENT_ELEVATOR_SOURCE";
        }

        internal static void RequireCurrentLanding(bool navigationFound,float verticalDelta,string identity)
        {
            if(!navigationFound||float.IsNaN(verticalDelta)||float.IsInfinity(verticalDelta)||System.Math.Abs(verticalDelta)>.35f)
                throw new System.InvalidOperationException("Current source elevator landing is not on its own storey: "+identity);
        }

        /// <summary>Match the same authored landing-door/cabin bank used by the canonical Editor survey. Catalog coordinates alone supply no machinery.</summary>
        internal bool TryBindCurrentSource(UnityEngine.SceneManagement.Scene sourceScene)
        {
            SetNavigationAvailable(false,"CURRENT_SOURCE_NOT_QUALIFIED");car=null;landing=null;currentSourceQualified=false;
            if(!sourceScene.IsValid()||!sourceScene.isLoaded)throw new System.InvalidOperationException("Current elevator source scene is absent");
            var doors=new List<Facilities.StationDoor>();
            foreach(var root in sourceScene.GetRootGameObjects())
                foreach(var door in root.GetComponentsInChildren<Facilities.StationDoor>(true))
                    if(door.Kind==Facilities.StationDoor.DoorKind.Elevator)doors.Add(door);
            var matched=new Facilities.StationDoor[Entry.stops.Length];int count=0;
            for(int stop=0;stop<Entry.stops.Length;stop++)
            {
                foreach(var door in doors)
                {
                    // StationSurvey.Elevators uses this actual door front and an .8m
                    // navigation query. This is source association only; the current
                    // landing's .6m/.35m navigation guard remains separate and strict.
                    var front=door.World(new Vector3(0,0,1f));
                    if(Vector3.Distance(front,Entry.stops[stop].door)>.8f||Mathf.Abs(front.y-Entry.stops[stop].door.y)>.35f)continue;
                    if(matched[stop]!=null)throw new System.InvalidOperationException("Ambiguous current elevator landing owner: "+Entry.id);
                    matched[stop]=door;
                }
                if(matched[stop]!=null)count++;
            }
            var status=CurrentSourceDecision(Entry.stops.Length,count,count==Entry.stops.Length);
            if(count==0){NavigationIssue=status;return false;}
            var source=matched[0].Car;var map=new int[matched.Length];var unique=new HashSet<Facilities.StationDoor>();
            for(int stop=0;stop<matched.Length;stop++)
            {
                var door=matched[stop];
                if(!door.isActiveAndEnabled||!door.Usable||door.Cabin==null||string.IsNullOrEmpty(door.CarId)||
                    source==null||!source.isActiveAndEnabled||source.gameObject.scene!=sourceScene||door.Car!=source||source.Id!=door.CarId||
                    !unique.Add(door)||(map[stop]=source.IndexOf(door))<0)
                    throw new System.InvalidOperationException("Current elevator landing has no complete authored live source bank: "+Entry.id);
            }
            if(source.Landings.Count!=matched.Length)throw new System.InvalidOperationException("Current elevator catalog/source stop count differs: "+Entry.id);
            car=source;landing=map;
            if(!serviceRunning)car.Stop();
            currentSourceQualified=true;NavigationIssue=status;return true;
        }

        internal void SetNavigationAvailable(bool available,string issue)
        {
            if(available&&currentSourceRequired&&!currentSourceQualified)throw new System.InvalidOperationException("Current elevator navigation requires its complete source bank");
            NavigationIssue=issue;
            if(NavigationAvailable==available)return;
            NavigationAvailable=available;
            if(!available){ReleaseLinks();return;}
            for(int i=0;i<Entry.stops.Length;i++)for(int j=i+1;j<Entry.stops.Length;j++)
            {
                var link=NavMesh.AddLink(new NavMeshLinkData {startPosition=Entry.stops[i].door,endPosition=Entry.stops[j].door,width=0,bidirectional=true,area=4,costModifier=-1});
                if(NavMesh.IsLinkValid(link)){NavMesh.SetLinkOwner(link,this);links.Add(link);}
            }
        }

        private void ReleaseLinks()
        {
            foreach(var link in links)if(NavMesh.IsLinkValid(link))NavMesh.RemoveLink(link);
            links.Clear();
        }
        private void OnDestroy()=>ReleaseLinks();

        /// <summary>The twin's car serving every stop of this elevator (landing doors within 2.5 m of each stop), if any.</summary>
        public Facilities.ElevatorCar Car
        {
            get
            {
                if (!NavigationAvailable) return null;
                if (currentSourceRequired) return car;
                if (car != null || Time.time < lookAgainAt) return car;
                lookAgainAt = Time.time + 5;
                foreach (var candidate in Facilities.ElevatorCar.All)
                {
                    var map = new int[Entry.stops.Length];
                    bool every = true;
                    for (int i = 0; i < map.Length && every; i++) every = (map[i] = candidate.LandingNear(Entry.stops[i].door)) >= 0;
                    if (!every) continue;
                    car = candidate;
                    landing = map;
                    if (!Running) car.Stop();
                    break;
                }
                return car;
            }
        }

        /// <summary>Earthquake, power loss or a fault: the car stops where it is (between floors when moving); nobody enters.</summary>
        public void Stop()
        {
            serviceRunning = false;
            var working = Car;
            if (working != null) working.Stop();
        }

        /// <summary>Back in service (a technician's rescue run, power restored): the car goes on to where it was going.</summary>
        public void Resume()
        {
            serviceRunning = true;
            var working = Car;
            if (working != null) working.Resume();
        }

        public Vector3 Door(int stop) => Entry.stops[stop].door;

        private int Nearest(Vector3 p)
        {
            int best = 0; float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < Entry.stops.Length; i++)
            {
                float d = Vector3.Distance(Entry.stops[i].door, p);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        private Vector3 IntoShaft(int stop) => Quaternion.Euler(0, Entry.stops[stop].yaw, 0) * Vector3.forward;

        public IEnumerator Traverse(PersonBody body, OffMeshLinkData data)
        {
            if(!NavigationAvailable)throw new System.InvalidOperationException("Elevator navigation is unavailable: "+NavigationIssue);
            if(currentSourceRequired&&Car==null)throw new System.InvalidOperationException("Bound current elevator source car was lost");
            int a = Nearest(data.startPos), b = Nearest(data.endPos);
            int from = Vector3.Distance(body.transform.position, Entry.stops[a].door) <= Vector3.Distance(body.transform.position, Entry.stops[b].door) ? a : b;
            int to = from == a ? b : a;
            if (Car != null) yield return RideCar(body, from, to);
            else yield return RideUnseen(body, from, to);
        }

        /// <summary>Waits beside the landing doors, boards when they stand open, rides among the others and walks out.</summary>
        private IEnumerator RideCar(PersonBody body, int from, int to)
        {
            int lf = landing[from], lt = landing[to];
            var into = IntoShaft(from);
            var side = new Vector3(into.z, 0, -into.x);
            Waiting++;
            Facilities.ElevatorCar.Rider rider = null;
            try
            {
                // 문 앞에서 조금 비켜 서서 기다린다(내리는 사람이 먼저).
                var spot = Entry.stops[from].door - into * (.6f + Random.value * .5f) + side * Random.Range(-.9f, .9f);
                yield return body.WalkLine(StationWorld.OnNavMesh(spot, .6f), Mathf.Min(body.WalkSpeed, 1.1f));
                while (rider == null)
                {
                    if (car.Running && car.DoorsOpenAt(lf) && !car.Full) { rider = car.Board(body, lf, lt); break; }
                    if (car.Running && !car.DoorsOpenAt(lf)) car.Call(lf);
                    var look = car.Threshold(lf) - body.transform.position;
                    body.Ride(body.transform.position, look);
                    yield return null;
                }
            }
            finally { Waiting--; }
            Inside.Add(body);
            try
            {
                yield return body.WalkLine(car.Threshold(lf, -.35f), 1f);
                yield return body.WalkLine(car.Threshold(lf, .4f), 1f);
                yield return body.WalkLine(car.SpotWorld(rider), .9f);
                rider.Walking = false;
                // 목적 층에서 문이 다 열릴 때까지 자리에 선다(가는 동안 카가 사람을 옮긴다).
                while (!(rider.Cabin == lt && car.DoorsOpenAt(lt)))
                {
                    body.Ride(car.SpotWorld(rider), car.Facing(rider));
                    yield return null;
                }
                rider.Walking = true;
                yield return body.WalkLine(car.Threshold(lt, .4f), 1f);
                yield return body.WalkLine(car.Threshold(lt, -.45f), 1f);
            }
            finally
            {
                Inside.Remove(body);
                car.Alight(rider);
            }
            yield return body.WalkLine(Entry.stops[to].door, body.WalkSpeed);
        }

        /// <summary>No working car in the twin: the doors cannot open, so people step in and out only while unseen.</summary>
        private IEnumerator RideUnseen(PersonBody body, int from, int to)
        {
            var door = Entry.stops[from].door;
            var into = IntoShaft(from);
            var side = new Vector3(into.z, 0, -into.x);
            Waiting++;
            // 문 앞에서 조금 비켜 서서 기다린다.
            var spot = door - into * (.6f + Random.value * .5f) + side * Random.Range(-.9f, .9f);
            yield return body.WalkLine(StationWorld.OnNavMesh(spot, .6f), Mathf.Min(body.WalkSpeed, 1.1f));
            // 차는 한 대: 부른 층까지 오는 시간, 문, 가는 시간을 차례로 쓴다.
            float arrive = Mathf.Max(Time.time, carFreeAt) + Mathf.Abs(carY - door.y) / Speed + DoorSeconds * .5f;
            float travel = Mathf.Abs(Entry.stops[to].door.y - door.y) / Speed + DoorSeconds;
            carFreeAt = arrive + travel;
            carY = Entry.stops[to].door.y;
            Inside.RemoveAll(x => x == null);
            while (Time.time < arrive || !Running || PlayerView.Sees(door, 18) || Inside.Count >= Capacity)
            {
                body.Ride(body.transform.position, into);
                yield return null;
            }
            Waiting--;
            yield return body.WalkLine(door, 1f);
            body.SetVisible(false);
            Inside.Add(body);
            try
            {
                var target = Entry.stops[to].door;
                body.transform.position = target + IntoShaft(to) * .7f;
                float until = Time.time + travel;
                while (Time.time < until || !Running) yield return null;
                // 내리는 문도 역무원이 보고 있지 않을 때 연다(최대 15초 기다림).
                float giveUp = Time.time + 15;
                while (PlayerView.Sees(target, 18) && Time.time < giveUp) yield return null;
            }
            finally
            {
                Inside.Remove(body);
                body.SetVisible(true);
            }
            yield return body.WalkLine(Entry.stops[to].door, body.WalkSpeed);
        }
    }
}
