using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Runtime
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
    public sealed class ActorNavigationBinding : MonoBehaviour
    {
        public string ActorId;
        public GameplayNavigation Navigation;
        public NavigationAccess RequiredAccess = NavigationAccess.Walk;
        [Min(.1f)] public float Speed = 1.35f;
        [Min(.1f)] public float Acceleration = 3f;
        [Range(.04f, .2f)] public float ArrivalDistance = .1f;
        [Min(.5f)] public float BlockedAfterSeconds = 2f;
        public bool HasArrived { get; private set; }
        public bool IsBlocked { get; private set; }
        public string BlockedReason { get; private set; } = "destination_not_set";
        public Vector3 Velocity { get; private set; }
        public float Radius => motor == null ? .3f : motor.radius;
        public bool HasDestination => hasDestination;
        public Vector3 Destination => destination;
        public int PlannedRevision { get; private set; } = -1;

        private readonly List<Vector3> route = new List<Vector3>(128);
        private readonly List<Vector3> checkpointRoute = new List<Vector3>(4);
        private CharacterController motor;
        private GameplayNavigation registeredNavigation;
        private string registeredId;
        private Vector3 destination;
        private Vector3 arrivalPoint;
        private int cursor;
        private bool hasDestination;
        private bool held;
        private float stalled;
        private float retryAfter;

        private void Awake()
        {
            motor = GetComponent<CharacterController>();
            motor.height = 1.7f;
            motor.radius = .3f;
            motor.center = Vector3.up * .85f;
            motor.stepOffset = .15f;
            motor.slopeLimit = 40f;
            motor.skinWidth = .015f;
            motor.minMoveDistance = 0f;
        }

        private void Start()
        {
            if (!EnsureOwner(out var reason)) Block(reason);
        }

        public bool SetDestination(Vector3 target, out string reason)
        {
            HasArrived = false;
            Velocity = Vector3.zero;
            if (!DetourNavigationSurface.Finite(target)) { reason = "non_finite_endpoint"; Block(reason); return false; }
            destination = target;
            hasDestination = true;
            held = false;
            if (!EnsureOwner(out reason)) { Block(reason); return false; }
            return Plan(out reason);
        }

        // Work/consent/reservation logic may pause locomotion without deleting its pending destination.
        public void SetHeld(bool value)
        {
            held = value;
            Velocity = Vector3.zero;
            if (value) { IsBlocked = true; BlockedReason = "actor_held_for_action"; }
            else if (hasDestination)
            {
                if (EnsureOwner(out var reason)) Plan(out _);
                else Block(reason);
            }
        }

        public void ClearDestination()
        {
            hasDestination = false;
            route.Clear();
            HasArrived = false;
            IsBlocked = false;
            BlockedReason = "destination_not_set";
            Velocity = Vector3.zero;
        }

        // Explicit tutorial rewind only. Ordinary travel and arrival never use this discontinuous reset.
        // Restore checkpoint geometry/door state before calling; a rejected reset preserves the live route.
        public bool RestoreCheckpointPosition(Vector3 position, out string reason)
        {
            if (!EnsureOwner(out reason)) return false;
            if (!Navigation.TryRoute(position, position, RequiredAccess, checkpointRoute, out reason)) return false;
            var projected = checkpointRoute[0];
            if (new Vector2(projected.x - position.x, projected.z - position.z).sqrMagnitude > .000625f)
            { reason = "checkpoint_position_outside_walkable_surface"; return false; }
            var wasEnabled = motor.enabled;
            motor.enabled = false;
            try { transform.position = position; }
            finally { motor.enabled = wasEnabled; }
            Navigation.UpdateActorPosition(this);
            ClearDestination();
            destination = position;
            arrivalPoint = position;
            cursor = 0;
            held = false;
            stalled = 0;
            retryAfter = 0;
            PlannedRevision = Navigation.RouteRevision;
            reason = "checkpoint_position_restored";
            return true;
        }

        private bool EnsureOwner(out string reason)
        {
            if (!isActiveAndEnabled) { reason = "actor_motion_disabled"; return false; }
            if (Navigation == null) { reason = "navigation_not_bound"; return false; }
            if (motor == null) motor = GetComponent<CharacterController>();
            if (motor == null || !motor.enabled) { reason = "actor_collision_controller_missing"; return false; }
            if (GetComponent<Rigidbody>() != null)
            { reason = "conflicting_rigidbody_pose_owner"; return false; }
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) { reason = "conflicting_navmesh_pose_owner"; return false; }
            var animator = GetComponent<Animator>();
            if (animator != null && animator.applyRootMotion) { reason = "conflicting_root_motion_pose_owner"; return false; }
            if ((transform.lossyScale - Vector3.one).sqrMagnitude > .000001f)
            { reason = "actor_requires_unit_scale"; return false; }
            if (registeredNavigation != null && (registeredNavigation != Navigation || registeredId != ActorId))
            { reason = "actor_identity_or_navigation_changed_while_bound"; return false; }
            if (!Navigation.RegisterActor(this, out reason)) return false;
            registeredNavigation = Navigation;
            registeredId = ActorId;
            return true;
        }

        private bool Plan(out string reason)
        {
            PlannedRevision = Navigation.RouteRevision;
            cursor = 0;
            stalled = 0;
            if (!Navigation.TryRoute(transform.position, destination, RequiredAccess, route, out reason))
            { Block(reason); return false; }
            arrivalPoint = route[route.Count - 1];
            IsBlocked = false;
            HasArrived = false;
            BlockedReason = "";
            return true;
        }

        private void FixedUpdate()
        {
            if (!hasDestination || held || Navigation == null || !isActiveAndEnabled) return;
            if (registeredNavigation != Navigation || registeredId != ActorId)
            { Block("actor_identity_or_navigation_changed_while_bound"); return; }
            if (PlannedRevision != Navigation.RouteRevision)
            {
                if (!Plan(out _)) return;
            }
            if (!Navigation.IsReady) { Block("geometry_frame_changed_rebind_required"); return; }
            if (IsBlocked)
            {
                // Geometry blocks wait for revision; physical congestion is sampled at a bounded rate.
                if (BlockedReason != "physical_route_obstructed" || Time.fixedTime < retryAfter) return;
                retryAfter = Time.fixedTime + .5f;
                if (!Plan(out _)) return;
            }
            if (HasArrived) return;
            var dt = Mathf.Min(Time.fixedDeltaTime, .05f);
            if (dt <= 0 || !DetourNavigationSurface.Finite(Speed) || Speed <= 0) return;
            var position = transform.position;
            while (cursor < route.Count && Near(position, route[cursor], ArrivalDistance)) cursor++;
            if (cursor >= route.Count)
            {
                HasArrived = Near(position, arrivalPoint, ArrivalDistance);
                Velocity = Vector3.zero;
                if (!HasArrived) Block("arrival_not_observed");
                return;
            }
            var delta = route[cursor] - position;
            var horizontal = new Vector3(delta.x, 0, delta.z);
            var forward = horizontal.sqrMagnitude > .000001f ? horizontal.normalized * Speed : Vector3.zero;
            var desired = Navigation.Avoidance(this, forward);
            var velocity = Vector3.MoveTowards(Velocity, desired, Mathf.Max(.1f, Acceleration) * dt);
            var right = Vector3.Cross(Vector3.up, forward.normalized);
            // If keeping right hits a baked boundary, try the other side before reporting congestion.
            if (!TryWalkableStep(position, route[cursor], velocity, dt, out var walkable) &&
                !TryWalkableStep(position, route[cursor], Vector3.Reflect(velocity, right), dt, out walkable))
            {
                Velocity = Vector3.zero;
                RecordStall(dt);
                return;
            }
            // CharacterController is the sole pose writer. Neither corners nor route completion assign position.
            var displacement = walkable - position;
            if (Mathf.Abs(displacement.y) < .001f) displacement.y = -.02f;
            motor.Move(displacement);
            Navigation.UpdateActorPosition(this);
            var actual = transform.position - position;
            Velocity = new Vector3(actual.x, 0, actual.z) / dt;
            if (Velocity.sqrMagnitude > .01f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(Velocity), 360f * dt);
                stalled = 0;
            }
            else RecordStall(dt);
            if (Near(transform.position, arrivalPoint, ArrivalDistance))
            { HasArrived = true; IsBlocked = false; BlockedReason = ""; Velocity = Vector3.zero; }
        }

        private bool TryWalkableStep(Vector3 position, Vector3 corner, Vector3 velocity, float dt, out Vector3 walkable)
        {
            var horizontal = new Vector2(corner.x - position.x, corner.z - position.z).magnitude;
            var distance = Mathf.Min(velocity.magnitude * dt, horizontal);
            var proposed = position + (velocity.sqrMagnitude > .000001f ? velocity.normalized * distance : Vector3.zero);
            proposed.y = Mathf.MoveTowards(position.y, corner.y, Speed * dt);
            return Navigation.TryStep(position, proposed, RequiredAccess, out walkable) &&
                (walkable - position).magnitude <= Speed * dt + .02f;
        }

        private static bool Near(Vector3 a, Vector3 b, float distance) =>
            new Vector2(a.x - b.x, a.z - b.z).sqrMagnitude <= distance * distance && Mathf.Abs(a.y - b.y) <= .15f;
        private void RecordStall(float dt)
        {
            stalled += dt;
            if (stalled >= Mathf.Max(.5f, BlockedAfterSeconds))
            { Block("physical_route_obstructed"); retryAfter = Time.fixedTime + .5f; }
        }
        private void Block(string reason)
        {
            IsBlocked = true; HasArrived = false; BlockedReason = reason; Velocity = Vector3.zero;
        }
        private void OnDisable()
        {
            Velocity = Vector3.zero;
            if (registeredNavigation != null) registeredNavigation.UnregisterActor(registeredId, this);
            registeredNavigation = null;
            registeredId = null;
        }
        private void OnEnable()
        {
            if (Navigation == null || string.IsNullOrWhiteSpace(ActorId)) return;
            if (!EnsureOwner(out var reason)) Block(reason);
            else if (hasDestination) Plan(out _);
        }
    }
}
