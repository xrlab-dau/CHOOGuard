using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    public enum Agency { Fire, Police, Medical, Facility, Crew }

    /// <summary>
    /// A member of an arriving team (119 fire, 112/railway police, 119 EMS, station facility staff, train crew). They walk to
    /// the scene and do their part; the team lead takes the staff member's handover, which ends the shift. A job inside a KTX
    /// car (a fire at a seat, a passenger who collapsed in the car) is reached through the door and along the aisle, the way
    /// passengers board: the car floor is not part of the navmesh.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PersonBody))]
    public sealed class Responder : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public Agency Agency { get; private set; }
        public bool Lead { get; private set; }
        public bool OnScene { get; private set; }
        public float OnSceneAt { get; private set; }

        private IncidentDirector director;
        private PersonBody body;
        private Vector3 goal;
        private Hazard target;
        private ParticleSystem hose;
        private Passenger patient, supporting;
        private float treatUntil, nextRepath, nearSince = -1;
        private Vector3 kneelAt, chest;
        private EmergencyArt art;
        private GameObject medicalBag;

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

        public string DisplayName
        {
            get
            {
                switch (Agency)
                {
                    case Agency.Fire: return Lead ? "소방대 선착 대장" : "소방대원";
                    case Agency.Police: return Lead ? "철도경찰 팀장" : "경찰관";
                    case Agency.Medical: return "구급대원";
                    case Agency.Crew: return Lead ? "열차팀장" : "승무원";
                    default: return "시설 담당 직원";
                }
            }
        }

        public void Setup(IncidentDirector owner, Agency agency, bool lead, Vector3 destination, Hazard hazard, EmergencyArt art)
        {
            director = owner;
            Agency = agency;
            Lead = lead;
            goal = destination;
            target = hazard;
            this.art = art;
            body = GetComponent<PersonBody>();
            body.Home = transform.parent;
            if (agency == Agency.Fire && !lead)
            {
                hose = Particles.Powder(transform, art.Smoke);
                hose.transform.localPosition = new Vector3(.2f, 1.1f, .5f);
            }
            // 할 일이 열차 안이면 한 사람(구급대장·소방대원)이 그 차의 출입문으로 들어가고, 나머지는 문 앞에서 기다린다.
            bool enters = agency == Agency.Medical ? lead : agency == Agency.Fire && !lead;
            if (Train != null && hazard != null && hazard.NeedsSight)
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

        private readonly List<Vector3> legs = new List<Vector3>();
        private int leg, idleRetries;
        private float nextLegCheck;
        private Vector3 lastCheck;

        public string InteractionPrompt => Lead && OnScene && director.CanHandOver(this) ? "현장 인계" : "";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            bool available = responder != null && !responder.IsPaused && InteractionPrompt.Length > 0;
            reason = available ? null : Lead && !OnScene ? "현장으로 이동 중입니다" : "";
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
                    if (Lead && Vector3.Distance(transform.position, player) < 8) body.Face(player);
                    else if (target != null && target.NeedsSight) body.Face(target.Position);
                    break;
            }
        }

        private void ArriveOnScene()
        {
            OnScene = true;
            OnSceneAt = Time.time;
            director.OnResponderArrived(this);
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
