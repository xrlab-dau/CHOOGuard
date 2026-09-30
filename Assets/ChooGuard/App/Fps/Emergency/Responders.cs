using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    public enum Agency { Fire, Police, Medical, Facility, Crew }

    /// <summary>
    /// A member of an arriving team (<see cref="Team"/>: 119 fire, rescue, hazmat or ambulance crew, railway police with
    /// 112 patrol officers or the police special unit's bomb technicians, station staff or a contractor, train crew). They
    /// walk to the scene and do their part — put a fire out, treat the injured, restrain an aggressive person, or do
    /// hands-on work the hazard asks for (a rescue run, a sweep, shutting a valve: <see cref="Hazard.WorkSeconds"/>);
    /// the team lead takes the staff member's handover, which ends the shift. They bring what their job needs: the
    /// nozzle man lays a hose from the entrance, an ambulance crew pushes a stretcher, technicians carry a toolbox, a
    /// bomb technician in a blast suit has a robot beside him, facility staff set out wet-floor signs. A job inside a KTX
    /// car (a fire at a seat, a passenger who collapsed in the car) is reached through the door and along the aisle, the
    /// way passengers board: the car floor is not part of the navmesh.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PersonBody))]
    public sealed class Responder : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public Agency Agency { get; private set; }
        public Team Team { get; private set; }
        /// <summary>Order in the team: 0 is the lead who takes the handover.</summary>
        public int Index { get; private set; }
        public bool Lead => Index == 0;
        /// <summary>Does the hands-on work the hazard asks for: the lead, or the bomb technician of the special unit.</summary>
        public bool Worker { get; private set; }
        public bool OnScene { get; private set; }
        public float OnSceneAt { get; private set; }

        private IncidentDirector director;
        private PersonBody body;
        private Vector3 goal;
        private Hazard target;
        private ParticleSystem hose;
        private Passenger patient, supporting;
        private float treatUntil, nextRepath, nearSince = -1, workUntil = -1, approachSince = -1;
        private bool working;
        private Vector3 kneelAt, chest;
        private EmergencyArt art;
        private GameObject medicalBag;

        // 장비: 손에 든 것(뼈 위치를 따라가되 몸 기준으로 곧게), 밀고 가는 들것, 곁에서 가는 로봇, 바닥에 깐 호스·표지.
        private readonly List<(GameObject Prop, HumanBodyBones Bone, Vector3 Offset, Quaternion Rotation)> held = new List<(GameObject, HumanBodyBones, Vector3, Quaternion)>();
        private readonly List<GameObject> placed = new List<GameObject>();
        private GameObject toolbox, cot, robot;
        private LineRenderer hoseLine;
        private readonly List<Vector3> hosePath = new List<Vector3>();
        // 열차 안 현장
        private TrainService Train => director.World.Train;
        private TrainService.Car car;
        private bool inside, walkingAisle;

        public static string AgencyName(Agency agency)
        {
            switch (agency)
            {
                case Agency.Fire: return "소방대";
                case Agency.Police: return "철도경찰";
                case Agency.Medical: return "구급대";
                case Agency.Crew: return "열차 승무원";
                default: return "시설 담당";
            }
        }

        public string DisplayName => Teams.Member(Team, Index);

        public void Setup(IncidentDirector owner, Agency agency, Team team, int index, Vector3 destination, Hazard hazard, EmergencyArt art)
        {
            director = owner;
            Agency = agency;
            Team = team;
            Index = index;
            Worker = team == Team.BombSquad ? index == 1 : index == 0;
            goal = destination;
            target = hazard;
            this.art = art;
            body = GetComponent<PersonBody>();
            body.Home = transform.parent;
            // 출동한 사람은 길 요청이 군중 뒤에 줄을 서지 않는다(수십 명의 대피·일상 요청 뒤에서 몇십 초씩 서 있었다).
            body.Rescuer = true;
            Equip();
            // 할 일이 열차 안이면 한 사람(구급대장·소방대원)이 그 차의 출입문으로 들어가고, 나머지는 문 앞에서 기다린다.
            bool enters = agency == Agency.Medical ? Lead : agency == Agency.Fire && !Lead;
            if (Train != null && hazard != null && hazard.Localized && !(hazard is FireHazard { Beneath: true }))
            {
                var within = Train.CarAt(hazard.Position);
                if (within != null)
                {
                    goal = Train.World(within.DoorOutside);
                    if (enters) car = within;
                }
            }
            // 먼 길(승강장·다른 층)은 계단참·승강설비 앞·30 m 마다 끊어 짧은 구간씩 간다.
            legs.AddRange(owner.World.Route(transform.position, goal, "stairs"));
            legs.Add(goal);
            body.GoTo(legs[0], 2.4f);
        }

        /// <summary>What this member brings in, by team and place in it.</summary>
        private void Equip()
        {
            switch (Team)
            {
                case Team.Fire when !Lead && art.NozzleModel != null:
                {
                    // 관창은 두 손으로 허리 앞에 들고(몸 기준으로 고정) 물은 관창 끝에서 앞으로 나간다. 호스는 들어온 길을 따라 깐다.
                    var nozzle = Instantiate(art.NozzleModel, transform);
                    nozzle.name = "관창";
                    nozzle.transform.localPosition = new Vector3(.1f, .98f, .34f);
                    nozzle.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    hose = Particles.Water(transform, art.Smoke);
                    hose.transform.localPosition = new Vector3(.1f, 1.02f, .58f);
                    if (art.HoseMaterial != null) LayHose();
                    break;
                }
                case Team.Ems when Index == 1 && art.CotModel != null:
                    cot = Instantiate(art.CotModel, transform);
                    cot.name = "주들것";
                    cot.transform.localPosition = new Vector3(0, 0, 1.25f);
                    break;
                case Team.Rescue when Lead:
                case Team.Facility when Lead:
                case Team.Elevator when Lead:
                case Team.Gas when Lead:
                case Team.Electric when Lead:
                    if (art.ToolboxModel != null) toolbox = Hold(art.ToolboxModel, "공구함", HumanBodyBones.LeftHand, new Vector3(0, -.3f, 0), Quaternion.identity);
                    if (Team == Team.Electric && art.FlashlightModel != null) Hold(art.FlashlightModel, "손전등", HumanBodyBones.RightHand, new Vector3(0, -.02f, .06f), Quaternion.Euler(0, -90, 0));
                    break;
                case Team.BombSquad when Worker && art.EodRobotModel != null:
                    robot = Instantiate(art.EodRobotModel, transform.position + transform.right * 1.1f, transform.rotation, transform.parent);
                    robot.name = "폭발물 처리 로봇";
                    break;
            }
        }

        /// <summary>A prop in one hand: it follows that hand but stays upright in the body's frame (no bone-axis guesswork).</summary>
        private GameObject Hold(GameObject prefab, string name, HumanBodyBones bone, Vector3 offset, Quaternion rotation)
        {
            var prop = Instantiate(prefab, transform);
            prop.name = name;
            prop.transform.localRotation = rotation;
            held.Add((prop, bone, offset, rotation));
            return prop;
        }

        private void LateUpdate()
        {
            var animator = body != null ? body.Animator : null;
            foreach (var (prop, bone, offset, rotation) in held)
            {
                if (prop == null || prop.transform.parent != transform) continue;
                var hand = animator != null ? animator.GetBoneTransform(bone) : null;
                prop.transform.SetPositionAndRotation(hand != null ? hand.position + transform.rotation * offset : transform.TransformPoint(new Vector3(-.3f, .5f, 0)), transform.rotation * rotation);
            }
            if (hoseLine != null) UpdateHose();
            if (robot != null) FollowRobot();
        }

        // ── 소방 호스: 들어온 입구에서부터 대원이 걸은 길을 따라 바닥에 깔리고 끝은 관창으로 올라온다. ──

        private void LayHose()
        {
            var line = new GameObject("소방 호스");
            line.transform.SetParent(transform.parent, false);
            hoseLine = line.AddComponent<LineRenderer>();
            hoseLine.sharedMaterial = art.HoseMaterial;
            hoseLine.widthMultiplier = .075f;
            hoseLine.numCornerVertices = 2;
            hoseLine.textureMode = LineTextureMode.Tile;
            hoseLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hosePath.Add(transform.position + Vector3.up * .04f);
        }

        private void UpdateHose()
        {
            var floor = transform.position + Vector3.up * .04f;
            if ((floor - hosePath[hosePath.Count - 1]).sqrMagnitude > .5f) hosePath.Add(floor);
            hoseLine.positionCount = hosePath.Count + 2;
            for (int i = 0; i < hosePath.Count; i++) hoseLine.SetPosition(i, hosePath[i]);
            // 발밑에서 관창 뒤쪽으로 올라온다.
            hoseLine.SetPosition(hosePath.Count, floor - transform.forward * .25f);
            hoseLine.SetPosition(hosePath.Count + 1, transform.TransformPoint(new Vector3(.1f, .98f, .14f)));
        }

        // ── 폭발물 처리 로봇: 요원 옆을 따라가고, 요원이 물체 앞에서 일할 때는 물체 1.5 m 앞에 선다. ──

        private void FollowRobot()
        {
            Vector3 want;
            if (working && target != null && target.Localized)
            {
                var from = target.Scene - transform.position;
                from.y = 0;
                want = target.Scene - (from.sqrMagnitude > .01f ? from.normalized : transform.forward) * 1.5f;
            }
            else want = transform.position + transform.right * 1.1f - transform.forward * .4f;
            want = StationWorld.OnNavMesh(want, 1.5f);
            var step = want - robot.transform.position;
            if (step.sqrMagnitude < .0025f) return;
            var flat = new Vector3(step.x, 0, step.z);
            robot.transform.position = Vector3.MoveTowards(robot.transform.position, want, 1.6f * Time.deltaTime);
            if (flat.sqrMagnitude > .01f) robot.transform.rotation = Quaternion.RotateTowards(robot.transform.rotation, Quaternion.LookRotation(flat), 120 * Time.deltaTime);
        }

        private readonly List<Vector3> legs = new List<Vector3>();
        private int leg, idleRetries;
        private float nextLegCheck;
        private Vector3 lastCheck;

        public string InteractionPrompt => Lead && OnScene && director.CanHandOver(this) ? "현장 인계" : "";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            bool available = responder != null && !responder.IsPaused && InteractionPrompt.Length > 0;
            reason = available ? null : Lead && !OnScene ? "현장으로 이동 중입니다" : Lead ? director.HandOverRefusal(this) : "";
            return available;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out _)) return false;
            feedback = director.HandOver(this);
            return true;
        }

        private void OnDestroy()
        {
            if (car != null) car.Visitors.Remove(transform);
            if (medicalBag != null) Destroy(medicalBag);
            if (hoseLine != null) Destroy(hoseLine.gameObject);
            if (robot != null) Destroy(robot);
            if (cot != null && cot.transform.parent != transform) Destroy(cot);
            if (toolbox != null && toolbox.transform.parent != transform) Destroy(toolbox);
            foreach (var item in placed) if (item != null) Destroy(item);
        }

        private void Update()
        {
            if (director == null || walkingAisle) return;
            if (supporting != null) { WatchSupport(); return; }
            if (!OnScene)
            {
                // 도착은 실제 거리로 판단한다(길이 아직 없거나 끊긴 경우 '멈춤'을 도착으로 착각하지 않는다).
                var point = legs[leg];
                var gap = transform.position - point;
                bool reached = new Vector2(gap.x, gap.z).magnitude < 1.6f && Mathf.Abs(gap.y) < 2f;
                if (!reached && Time.time > nextLegCheck)
                {
                    // 멈춰 있는데 아직 멀면 다시 길을 찾는다. 10초 넘게 조금도 나아가지 못할 때만 닿을 수 있는 가장 가까운 곳을 현장으로 삼는다.
                    nextLegCheck = Time.time + 2f;
                    bool progressed = (transform.position - lastCheck).sqrMagnitude > 1f;
                    lastCheck = transform.position;
                    if (progressed) idleRetries = 0;
                    else if (body.Scripted) idleRetries = 0;   // 엘리베이터를 기다리거나 타는 중
                    else if (++idleRetries > 5) reached = true;
                    else if (body.Arrived(1.2f)) body.GoTo(point, 2.4f);
                }
                if (!reached) return;
                idleRetries = 0;
                if (leg < legs.Count - 1) { leg++; body.GoTo(legs[leg], 2.4f); return; }
                // 차 안으로는 그 차 출입문 앞에 실제로 섰을 때만 들어간다. 닿지 못한 채 가장 가까운 곳에서 멈췄다면
                // (길이 끊긴 경우) 문까지 허공을 가로질러 들어가지 않고 그 자리에서 현장에 도착한 것으로 한다.
                if (car != null && !inside && AtDoor()) { EnterCar(); return; }
                ArriveOnScene();
                return;
            }
            var player = director.PlayerPosition;
            switch (Agency)
            {
                case Agency.Fire when target is FireHazard fire && !fire.Extinguished && !Lead:
                {
                    body.Face(fire.Position);
                    // 소방 호스·대형 소화기는 역무원 소화기보다 멀리(약 9 m) 닿고 훨씬 빠르게 끈다.
                    bool reach = Vector3.Distance(transform.position, fire.Position) < 9;
                    SetHose(reach ? 260 : 0);
                    if (reach)
                    {
                        fire.Suppress(1f, Time.deltaTime * 6f);
                        if (fire.Extinguished) director.OnFireOut(fire, "소방대");
                    }
                    break;
                }
                case Agency.Medical when inside:
                    SetHose(0);
                    TreatInCar();
                    break;
                case Agency.Medical:
                    SetHose(0);
                    TreatInjured();
                    break;
                default:
                    SetHose(0);
                    // 차 안의 불을 끈 소방대원은 승강장으로 나온다.
                    if (inside) { LeaveCar(null); break; }
                    if (Agency == Agency.Police && target is DisturbanceHazard disturbance) { Police(disturbance); break; }
                    if (Worker && target != null && target.Active && target.WorkSeconds(Agency) > 0) { Work(); break; }
                    if (Lead && Vector3.Distance(transform.position, player) < 8) body.Face(player);
                    else if (target != null && target.Localized) body.Face(target.Position);
                    break;
            }
        }

        private void ArriveOnScene()
        {
            OnScene = true;
            OnSceneAt = Time.time;
            // 누수: 시설 담당이 도착하면 미끄럼 주의 표지를 물웅덩이 둘레(앞뒤 2.5 m)에 세운다.
            if (Team == Team.Facility && Lead && target is WaterLeakHazard leak && art.WetFloorSignModel != null)
                foreach (var side in new[] { -1f, 1f })
                {
                    var along = Vector3.Cross(Vector3.up, (leak.Scene - transform.position).normalized);
                    var spot = StationWorld.OnNavMesh(leak.Scene + along * 2.5f * side, 1.5f);
                    var sign = Instantiate(art.WetFloorSignModel, spot, Quaternion.LookRotation(transform.position - spot, Vector3.up), transform.parent);
                    sign.name = "미끄럼 주의 표지";
                    placed.Add(sign);
                }
            director.OnResponderArrived(this);
        }

        /// <summary>Sets a carried prop down on the floor at the right side (work) or takes it back into the hand.</summary>
        private void SetDown(GameObject prop, bool down)
        {
            if (prop == null) return;
            if (down)
            {
                prop.transform.SetParent(transform.parent, true);
                prop.transform.SetPositionAndRotation(StationWorld.OnNavMesh(transform.position + transform.right * .45f, .6f), transform.rotation);
            }
            else prop.transform.SetParent(transform, true);
        }

        /// <summary>
        /// Hands-on work at the scene (a technician's rescue run, a police sweep, shutting a valve, lifting someone off the
        /// track): walks up to it, works for as long as the hazard takes, then tells the director it is done.
        /// </summary>
        private void Work()
        {
            var spot = target.Scene;
            if (!working)
            {
                var gap = transform.position - spot;
                bool close = new Vector2(gap.x, gap.z).magnitude < 1.8f && Mathf.Abs(gap.y) < 2.5f;
                if (!close)
                {
                    if (approachSince < 0) approachSince = Time.time;
                    // 걸어서 더 다가갈 수 없으면(길 끝에 섰으면) 닿은 곳에서 한다.
                    bool stuck = Time.time - approachSince > 6 && body.Arrived(.6f);
                    if (!stuck)
                    {
                        if (Time.time > nextRepath) { nextRepath = Time.time + 1.5f; body.GoTo(StationWorld.WalkableNear(spot, 3f), 2f); }
                        return;
                    }
                }
                body.Stop();
                working = true;
                workUntil = Time.time + target.WorkSeconds(Agency);
                SetDown(toolbox, true);
                // 손으로 하는 일(밸브·기계 조작, 구조)은 무릎 꿇고, 수색은 서서 둘러보며 한다.
                if (!(target is BombThreatHazard)) body.SetTreat(true);
                return;
            }
            body.Face(spot);
            if (Time.time < workUntil) return;
            body.SetTreat(false);
            if (body.Kneeling) return;
            working = false;
            SetDown(toolbox, false);
            director.OnWorked(this, target);
        }

        /// <summary>
        /// Police at a disturbance: the team lead closes in and restrains the person, who is then walked out of the station
        /// (<see cref="IncidentDirector.Restrain"/>) with the second officer close behind. The lead stays for the handover.
        /// </summary>
        private void Police(DisturbanceHazard disturbance)
        {
            var person = disturbance.Person;
            if (person == null || !disturbance.Active) { if (Lead) body.Face(director.PlayerPosition); return; }
            if (!disturbance.Restrained)
            {
                if (!Lead) { body.Face(person.transform.position); return; }
                if (Vector3.Distance(transform.position, person.transform.position) > 1.6f)
                {
                    if (Time.time > nextRepath) { nextRepath = Time.time + .8f; body.GoTo(StationWorld.OnNavMesh(person.transform.position, 1.5f), 2.2f); }
                    return;
                }
                body.Stop();
                body.Face(person.transform.position);
                director.Restrain(disturbance);
                return;
            }
            if (Lead) { body.Face(person.transform.position); return; }
            if (Time.time > nextRepath) { nextRepath = Time.time + .8f; body.GoTo(StationWorld.OnNavMesh(person.transform.position - person.transform.forward * .9f, 1.5f), 1.4f); }
        }

        private bool AtDoor()
        {
            var gap = transform.position - Train.World(car.DoorOutside);
            return new Vector2(gap.x, gap.z).magnitude < 1.6f && Mathf.Abs(gap.y) < 1f;
        }

        /// <summary>Steps in through the car door and walks the aisle to the row of the job (car frame, like boarding passengers).</summary>
        private void EnterCar()
        {
            var row = Train.AisleNear(car, target.Position);
            var path = new List<Vector3> { car.DoorInside };
            TrainService.AddAisle(path, car, row, true);
            path.Add(row);
            walkingAisle = true;
            car.Visitors.Add(transform);
            body.FollowInFrame(Train.Carrier, path, 1.2f, () =>
            {
                walkingAisle = false;
                inside = true;
                if (!OnScene) ArriveOnScene();
            });
        }

        /// <summary>Walks back along the aisle and steps down onto the platform; <paramref name="then"/> runs outside.</summary>
        private void LeaveCar(System.Action then)
        {
            var here = transform.localPosition;
            var path = new List<Vector3> { here };
            TrainService.AddAisle(path, car, here, false);
            path.Add(car.Entry.aisle[0]);
            path.Add(car.DoorInside);
            path.Add(car.DoorOutside);
            walkingAisle = true;
            body.SetTreat(false);
            body.FollowInFrame(Train.Carrier, path, 1.2f, () =>
            {
                walkingAisle = false;
                inside = false;
                car.Visitors.Remove(transform);
                body.ReturnToNavMesh(Train.World(car.DoorOutside));
                car = null;
                then?.Invoke();
            });
        }

        /// <summary>
        /// The patient collapsed in the car: treats them at the seat, then walks them off onto the platform — the patient in
        /// front, the paramedic close behind with both hands on their shoulders (<see cref="PersonBody.SupportFromBehind"/>).
        /// </summary>
        private void TreatInCar()
        {
            var person = (target as CollapseHazard)?.Person;
            if (person == null || !person.Hurt || person.TrainSeat == null) { body.SetTreat(false); if (!body.Kneeling) LeaveCar(null); return; }
            body.Face(person.transform.position);
            if (treatUntil == 0) { body.SetTreat(true); treatUntil = Time.time + 8f; return; }
            if (Time.time < treatUntil) return;
            // 처치를 마치면 일어선 뒤에 나간다.
            body.SetTreat(false);
            if (body.Kneeling) return;
            treatUntil = 0;
            director.OnTreated(person);
            // 환자가 통로로 나올 자리를 비우려고 먼저 통로 안쪽(문 반대쪽)으로 한 걸음 물러선 뒤 환자 뒤에 붙는다.
            // 통로 끝 줄이라 물러설 곳이 없으면 전처럼 먼저 나가고 환자가 뒤따른다.
            var aisle = car.Entry.aisle;
            var here = transform.localPosition;
            float row = Flat(aisle[0], here);
            Vector3? back = null;
            foreach (var point in aisle) if (Flat(aisle[0], point) >= row + .8f) { back = point; break; }
            if (back == null) { LeaveCar(person.HelpedOff); return; }
            walkingAisle = true;
            body.FollowInFrame(Train.Carrier, new List<Vector3> { back.Value }, .8f, () =>
            {
                walkingAisle = false;
                supporting = person;
                person.HelpedOff();
                body.SupportFromBehind(person.Body);
            });
        }

        /// <summary>Lets go once the patient stands on the platform; the paramedic stays with them there.</summary>
        private void WatchSupport()
        {
            bool down = supporting == null || supporting.Current != Passenger.Activity.InTrain && !supporting.Body.Scripted && supporting.Body.Seat == PersonBody.SeatPhase.None;
            if (!down) return;
            body.StopSupporting();
            if (car != null) car.Visitors.Remove(transform);
            inside = false;
            car = null;
            supporting = null;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            var d = a - b;
            d.y = 0;
            return d.magnitude;
        }

        private void TreatInjured()
        {
            if (patient == null || !patient.Hurt)
            {
                // 처치를 마쳤으면 일어선 뒤에 다음 환자에게 걷는다.
                body.SetTreat(false);
                if (body.Kneeling) return;
                patient = director.NextPatient(transform.position, false);
                if (patient == null) return;
                kneelAt = KneelSpot(patient, out chest);
                body.GoTo(kneelAt, 2f);
                treatUntil = 0;
                nearSince = -1;
                return;
            }
            if (treatUntil == 0)
            {
                // 에스컬레이터 계단 위처럼 바로 옆까지 걸어갈 수 없는 곳은 길 끝(가장 가까운 자리)에서, 자리가 막혀(벽·다른 대원)
                // 환자 3 m 안에 든 뒤 4초 넘게 닿지 못하면 2.5 m 안 그 자리에서 처치한다.
                var gap = transform.position - kneelAt;
                float reach = Vector3.Distance(transform.position, patient.transform.position);
                // 밀고 온 들것은 환자 4 m 앞에서 세워 두고(환자 쪽을 향해) 맨손으로 다가간다.
                if (cot != null && cot.transform.parent == transform && reach < 4f)
                {
                    cot.transform.SetParent(transform.parent, true);
                    var toward = patient.transform.position - transform.position;
                    toward.y = 0;
                    cot.transform.SetPositionAndRotation(StationWorld.OnNavMesh(transform.position + transform.right * .9f, 1f), toward.sqrMagnitude > .01f ? Quaternion.LookRotation(toward) : transform.rotation);
                }
                if (reach >= 3f) nearSince = -1;
                else if (nearSince < 0) nearSince = Time.time;
                bool close = new Vector2(gap.x, gap.z).magnitude < .35f || body.Arrived(.3f) && reach < 3.5f || nearSince >= 0 && Time.time - nearSince > 4f && reach < 2.5f;
                if (!close)
                {
                    if (Time.time > nextRepath) { nextRepath = Time.time + 1; kneelAt = KneelSpot(patient, out chest); body.GoTo(kneelAt, 2f); }
                    return;
                }
                body.Stop();
                body.SetTreat(true);
                treatUntil = Time.time + 8f;
                SetDownBag();
            }
            body.Face(chest);
            if (Time.time > treatUntil)
            {
                director.OnTreated(patient);
                patient = null;
                treatUntil = 0;
                if (medicalBag != null) { Destroy(medicalBag); medicalBag = null; }
            }
        }

        /// <summary>The team lead sets the first-aid bag down on the floor at their side while treating, and packs it up after.</summary>
        private void SetDownBag()
        {
            if (!Lead || art == null || art.MedicalBagModel == null || medicalBag != null) return;
            // 환자 가슴을 보고 선 방향의 오른쪽 바닥(처치 자세로 돌아서는 중이어도 같은 자리).
            var facing = chest - transform.position;
            facing.y = 0;
            var side = facing.sqrMagnitude > .01f ? Vector3.Cross(Vector3.up, facing.normalized) : transform.right;
            var floor = StationWorld.OnNavMesh(transform.position + side.normalized * .5f, .6f);
            medicalBag = Instantiate(art.MedicalBagModel, floor, Quaternion.LookRotation(side) * Quaternion.Euler(0, 90, 0), transform.parent);
            medicalBag.name = "구급 가방";
        }

        /// <summary>
        /// Where a paramedic kneels at a casualty: beside the chest, the team lead on one side and the second paramedic on
        /// the other. On a lying body the sides are across the hips-to-head line; for someone crouched, across their facing.
        /// </summary>
        private Vector3 KneelSpot(Passenger person, out Vector3 chestAt)
        {
            var animator = person.Body.Animator;
            var hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            var head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            var upper = animator != null ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
            chestAt = upper != null ? upper.position : person.transform.position;
            var axis = hips != null && head != null ? head.position - hips.position : Vector3.zero;
            axis.y = 0;
            var side = axis.sqrMagnitude > .09f ? Vector3.Cross(Vector3.up, axis.normalized) : person.transform.right;
            var spot = chestAt + side * (Lead ? .62f : -.62f);
            spot.y = person.transform.position.y;
            return StationWorld.OnNavMesh(spot, 1f);
        }

        private void SetHose(float rate)
        {
            if (hose == null) return;
            var emission = hose.emission;
            emission.rateOverTime = rate;
            if (rate > 0 && !hose.isPlaying) hose.Play();
        }
    }
}
