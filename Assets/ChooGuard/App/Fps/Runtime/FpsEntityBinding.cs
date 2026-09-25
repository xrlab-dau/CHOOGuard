using System;
using System.Collections.Generic;
using ChooGuard.Contracts.Gameplay;
using UnityEngine;

namespace ChooGuard.App.Fps.Runtime
{
    /// <summary>Authored contact geometry, not permission or safety facts. Each cleaning region has its own work point.</summary>
    [DisallowMultipleComponent]
    public sealed class FpsEntityBinding : MonoBehaviour, IFpsInteraction
    {
        [Serializable]
        public sealed class WorkPoint
        {
            public string Id;
            public Collider Surface;
            public Transform Frame;
            public Transform Visual;
            public string CompatibleToolDefinition;
            [Min(0)] public float StrokeMetres;
            [Min(0)] public float RotationDegrees;
            public int RotationDirection = 1;
            [Min(0)] public float ContactRadius;
            [Min(0)] public float AlignmentDegrees;
            [Min(0)] public float ProbeSeconds;
            public Vector3 Axis = Vector3.forward;
            internal Quaternion InitialVisualRotation;
            internal bool VisualInitialized, CanAnimateVisual;
        }

        public Transform ExtractionFrame;
        [Min(0)] public float ExtractionMetres;
        public string EntityId, WorkPointId;
        public ActionVerb Verb = ActionVerb.Observe;
        public string ToolId, RecipientId;
        public string Label;
        public Rigidbody Body;
        public Collider ContactSurface;
        public Transform GripPoint, ToolHead, Socket;
        public Transform ApproachPoint;
        public string CompatiblePartDefinition;
        [Min(0)] public float HandReach;
        [Min(0)] public float GripTolerance = .04f;
        [Min(0)] public float ToolHeadRadius;
        [Min(0)] public float SocketPositionTolerance;
        [Min(0)] public float SocketAngleTolerance;
        [Min(0)] public float SupportProbeDistance = .06f;
        [Range(0, 1)] public float MinimumSupportUp = .75f;
        public HingeJoint Hinge;
        public ConfigurableJoint Slider;
        public float ClosedCoordinate, OpenCoordinate;
        public WorkPoint[] WorkPoints = Array.Empty<WorkPoint>();
        public DetailedInteractionController Controller;
        private readonly List<Collider> colliders = new List<Collider>(8);
        private static readonly HashSet<FpsEntityBinding> live = new HashSet<FpsEntityBinding>();
        internal static IEnumerable<FpsEntityBinding> Live => live;
        internal IReadOnlyList<Collider> Colliders => colliders;
        private string cachedLabel, cachedPrompt;
        private ActionVerb cachedVerb;
        public string InteractionPrompt
        {
            get
            {
                string label = string.IsNullOrEmpty(Label) ? EntityId : Label;
                ActionVerb verb = Controller != null ? Controller.SelectedVerb ?? Verb : Verb;
                if (cachedPrompt == null || cachedLabel != label || cachedVerb != verb)
                { cachedLabel = label; cachedVerb = verb; cachedPrompt = label + " · " + verb; }
                return cachedPrompt;
            }
        }

        private void Awake() => InitializeGeometry();
        private void OnEnable() { live.Add(this); InitializeGeometry(); }
        private void OnDisable() { live.Remove(this); StopConstraint(); }

        public void Configure(string entityId, Collider surface, Rigidbody body = null)
        {
            EntityId = entityId; ContactSurface = surface; Body = body;
            InitializeGeometry();
        }

        public void InitializeGeometry()
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            colliders.Clear();
            (Body != null ? Body.transform : transform).GetComponentsInChildren(false, colliders);
            if (ContactSurface == null && colliders.Count > 0) ContactSurface = colliders[0];
            if (ContactSurface != null && !colliders.Contains(ContactSurface)) colliders.Add(ContactSurface);
            if (Body != null)
            {
                if (Hinge == null) Hinge = Body.GetComponent<HingeJoint>();
                if (Slider == null) Slider = Body.GetComponent<ConfigurableJoint>();
            }
            foreach (var point in WorkPoints)
            {
                if (point == null) continue;
                if (point.Surface != null && !colliders.Contains(point.Surface)) colliders.Add(point.Surface);
                if (point.Visual == null) continue;
                if (!point.VisualInitialized)
                { point.InitialVisualRotation = point.Visual.localRotation; point.VisualInitialized = true; }
                point.CanAnimateVisual = point.Visual != transform && point.Visual.GetComponent<Rigidbody>() == null;
                for (int i = 0; i < colliders.Count && point.CanAnimateVisual; i++)
                    if (colliders[i].transform.IsChildOf(point.Visual)) point.CanAnimateVisual = false;
            }
        }

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            var controller = Resolve(responder);
            if (controller == null) { reason = "상세 조작 세션이 연결되지 않았습니다"; return false; }
            return controller.CanBegin(this, out reason);
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            var controller = Resolve(responder);
            if (controller == null) { feedback = "상세 조작 세션이 연결되지 않았습니다"; return false; }
            return controller.Begin(this, out feedback);
        }

        private DetailedInteractionController Resolve(FirstPersonResponder responder)
        {
            if (Controller != null && Controller.Responder == responder) return Controller;
            if (responder != null && responder.TryGetComponent<DetailedInteractionController>(out var controller))
            { Controller = controller; return controller; }
            return null;
        }

        public WorkPoint FindWorkPoint(string id)
        {
            foreach (var point in WorkPoints) if (point != null && string.Equals(point.Id, id, StringComparison.Ordinal)) return point;
            return null;
        }

        internal bool Owns(Collider collider)
        {
            for (int i = 0; i < colliders.Count; i++) if (colliders[i] == collider) return true;
            return false;
        }

        internal static FpsEntityBinding SupportOwner(Collider collider)
        {
            FpsEntityBinding owner = null;
            var nearest = collider == null ? null : collider.GetComponentInParent<FpsEntityBinding>();
            foreach (var binding in live)
            {
                if (nearest != null && nearest != binding) continue;
                if (binding == null || string.IsNullOrEmpty(binding.EntityId) || !binding.Owns(collider)) continue;
                if (owner != null) return null; // Ambiguous authored ownership cannot prove a named placement.
                owner = binding;
            }
            return owner;
        }

        public bool CanDriveBody(out string reason)
        {
            reason = null;
            if (Body == null || Body.isKinematic || !Body.detectCollisions) { reason = "충돌 가능한 동적 Rigidbody가 저작되어야 합니다"; return false; }
            bool solid = false;
            foreach (var collider in colliders)
            {
                if (!collider.enabled || collider.isTrigger || collider.attachedRigidbody != Body) continue;
                if (collider is MeshCollider mesh && !mesh.convex) { reason = "운반 물체에는 convex 충돌 형상이 필요합니다"; return false; }
                solid = true;
            }
            if (!solid) { reason = "운반 물체의 solid 충돌 형상이 없습니다"; return false; }
            return true;
        }

        /// <summary>Creates only a caller-authored practice constraint. Never moves the body transform.</summary>
        public void ConfigureHinge(Vector3 localAnchor, Vector3 localAxis, float closedDegrees, float openDegrees)
        {
            if (Body == null) throw new InvalidOperationException("Hinge body must be authored first.");
            if (Slider != null) throw new InvalidOperationException("A body cannot own both slider and hinge motion.");
            Hinge = Body.GetComponent<HingeJoint>();
            if (Hinge == null) Hinge = Body.gameObject.AddComponent<HingeJoint>();
            Hinge.anchor = localAnchor; Hinge.axis = localAxis.normalized;
            Hinge.useLimits = true;
            Hinge.limits = new JointLimits { min = Mathf.Min(closedDegrees, openDegrees), max = Mathf.Max(closedDegrees, openDegrees) };
            ClosedCoordinate = closedDegrees; OpenCoordinate = openDegrees;
        }

        public void ConfigureSlider(Vector3 localAxis, float closedMetres, float openMetres)
        {
            if (Body == null) throw new InvalidOperationException("Slider body must be authored first.");
            if (Hinge != null) throw new InvalidOperationException("A body cannot own both slider and hinge motion.");
            Slider = Body.GetComponent<ConfigurableJoint>();
            if (Slider == null) Slider = Body.gameObject.AddComponent<ConfigurableJoint>();
            Slider.axis = localAxis.normalized;
            Slider.secondaryAxis = Mathf.Abs(Vector3.Dot(Slider.axis, Vector3.up)) < .9f ? Vector3.up : Vector3.right;
            Slider.autoConfigureConnectedAnchor = false; Slider.anchor = Vector3.zero;
            Vector3 sliderAxis = Body.transform.TransformDirection(Slider.axis).normalized;
            Vector3 sliderOrigin = Body.position - sliderAxis * closedMetres;
            Vector3 anchor = sliderOrigin + sliderAxis * ((closedMetres + openMetres) * .5f);
            Slider.connectedAnchor = Slider.connectedBody == null ? anchor : Slider.connectedBody.transform.InverseTransformPoint(anchor);
            Slider.xMotion = ConfigurableJointMotion.Limited;
            Slider.yMotion = Slider.zMotion = ConfigurableJointMotion.Locked;
            Slider.angularXMotion = Slider.angularYMotion = Slider.angularZMotion = ConfigurableJointMotion.Locked;
            Slider.linearLimit = new SoftJointLimit { limit = Mathf.Abs(openMetres - closedMetres) * .5f };
            ClosedCoordinate = closedMetres; OpenCoordinate = openMetres;
        }

        internal float ConstraintCoordinate
        {
            get
            {
                if (Hinge != null) return Hinge.angle;
                if (Body == null || Slider == null) return 0;
                Vector3 axis = Slider.transform.TransformDirection(Slider.axis).normalized;
                Vector3 anchor = Slider.connectedBody == null ? Slider.connectedAnchor : Slider.connectedBody.transform.TransformPoint(Slider.connectedAnchor);
                return Vector3.Dot(Body.transform.TransformPoint(Slider.anchor) - anchor, axis) + (ClosedCoordinate + OpenCoordinate) * .5f;
            }
        }
        internal void DriveConstraint(float direction, float maximumForce)
        {
            if (Body == null || Body.isKinematic) return;
            Body.WakeUp();
            if (Hinge != null)
            {
                Hinge.motor = new JointMotor { targetVelocity = Mathf.Clamp(direction, -1, 1) * 60, force = maximumForce, freeSpin = false };
                Hinge.useMotor = true;
            }
            else if (Slider != null) Body.AddForce(Slider.transform.TransformDirection(Slider.axis).normalized * Mathf.Clamp(direction, -1, 1) * maximumForce, ForceMode.Force);
        }
        internal void StopConstraint() { if (Hinge != null) Hinge.useMotor = false; }
    }
}
