using System;
using System.Collections.Generic;
using ChooGuard.Application.Gameplay;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ChooGuard.App.Fps.Runtime
{
    /// <summary>Human input adapter. Physics owns poses; WorldSession alone owns custody, work and completion.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    public sealed class DetailedInteractionController : MonoBehaviour
    {
        public FirstPersonResponder Responder;
        public Transform HandAnchor;
        [Min(0)] public float MaximumGripForce = 180;
        [Min(0)] public float MaximumGripTorque = 30;
        [Min(0)] public float PositionSpring = 450;
        [Min(0)] public float PositionDamping = 45;
        [Min(0)] public float RotationSpring = 30;
        [Min(0)] public float RotationDamping = 6;
        [Min(0)] public float MaximumHandSpeed = .8f;
        [Min(0)] public float MaximumRotationSpeed = 90;
        private bool inputConsumed;
        public bool InputConsumed
        {
            get => inputConsumed;
            set
            {
                if (inputConsumed == value) return;
                inputConsumed = value;
                if (value) Suspend("메뉴 입력 · 작업 중단/보유 유지");
                RefreshInputOwnership();
            }
        }
        public string LastFeedback { get; private set; } = "";
        public string ActivePrompt => active == null ? held == null ? "" : "운반 · 드래그/방향키 이동 · 휠 깊이 · R 회전 · 내려놓기 동사 선택" :
            activePrompt;
        public ActionVerb? SelectedVerb => selectedVerb;
        public string SelectedToolId => selectedTool;
        public string SelectedWorkPointId => selectedPoint;
        public bool IsManipulating => active != null || held != null;
        public FpsEntityBinding HeldEntity => held;
        public ActorActionIntent ActiveIntent => active;
        public ActionReceipt LastReceipt { get; private set; }
        public event Action<string> FeedbackChanged;

        public FpsPhysicalCheckpoint CapturePhysicalCheckpoint()
        {
            if (active != null) throw new InvalidOperationException("Finish or cancel contact work before a checkpoint.");
            return FpsPhysicalCheckpoint.Capture(session, Responder);
        }
        public void ValidatePhysicalCheckpoint(FpsPhysicalCheckpoint checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            checkpoint.Validate(session, Responder);
        }
        public void RestorePhysicalCheckpoint(FpsPhysicalCheckpoint checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            checkpoint.Restore(session, Responder);
            EndLocalAction(); ReleasePhysicalGrip(); generation = session.Generation;
            suppressUntilRelease = true; RefreshBindings(); SynchronizeCustody();
        }

        private WorldSession session;
        private StableId actorId;
        private bool actorAssigned;
        private ActorActionIntent active;
        private FpsEntityBinding target, tool, held, recipient;
        private FpsEntityBinding.WorkPoint point;
        private readonly FpsPhysicalQueries queries = new FpsPhysicalQueries();
        private readonly Dictionary<string, FpsEntityBinding> bindings = new Dictionary<string, FpsEntityBinding>(StringComparer.Ordinal);
        private ActionVerb? selectedVerb;
        private string selectedTool, selectedPoint, activePrompt;
        private long generation = -1, roleRevision = -1, lastEvidenceTick = -1;
        private Vector3 handOffset, gripTarget, localGrip, previousHead;
        private Quaternion gripRotation, previousToolRotation;
        private bool actuating, confirm, suppressUntilRelease, inputWasHeld, frozenGrip;
        private float rotationInput, previousConstraint, probeElapsed, rotationTravel, creditedRotation;
        private CollisionDetectionMode priorCollisionMode;

        public WorldSession Session
        {
            get => session;
            set
            {
                if (ReferenceEquals(session, value)) return;
                CancelActive("세션 변경으로 조작이 중단되었습니다"); ReleasePhysicalGrip();
                if (session != null) session.Committed -= OnCommitted;
                session = value; generation = session == null ? -1 : session.Generation;
                if (session != null) session.Committed += OnCommitted;
                RefreshBindings(); RefreshInputOwnership();
            }
        }
        public StableId ActorId
        {
            get => actorId;
            set
            {
                if (actorAssigned && actorId.Equals(value)) return;
                CancelActive("조작자 변경으로 작업이 중단되었습니다"); ReleasePhysicalGrip();
                actorId = value; actorAssigned = true;
            }
        }
        public void Bind(WorldSession world, StableId actor, FirstPersonResponder responder)
        {
            Session = world; ActorId = actor; Responder = responder;
            RefreshBindings(); SynchronizeCustody(); RefreshInputOwnership();
        }
        public void RefreshBindings()
        {
            bindings.Clear();
            foreach (var binding in FpsEntityBinding.Live)
                if (binding != null && !string.IsNullOrEmpty(binding.EntityId) && session != null &&
                    session.TryGetEntity(new StableId(binding.EntityId), out _)) bindings[binding.EntityId] = binding;
        }
        public void SelectVerb(ActionVerb verb) { CancelActive("동작 선택 변경"); selectedVerb = verb; }
        public void SelectTool(string entityId) { CancelActive("공구 선택 변경"); selectedTool = entityId; }
        public void SelectWorkPoint(string id) { CancelActive("작업점 선택 변경"); selectedPoint = id; }
        public void ClearSelection() { CancelActive("작업 선택 해제"); selectedVerb = null; selectedTool = selectedPoint = null; }

        private void Awake() { if (Responder == null) Responder = GetComponent<FirstPersonResponder>(); }
        private void OnDisable() { CancelActive("조작 비활성화"); FreezeGrip(); RefreshInputOwnership(); }
        private void OnDestroy()
        {
            if (session != null) session.Committed -= OnCommitted;
            ReleasePhysicalGrip();
        }
        private void OnApplicationFocus(bool focus) { if (!focus) Suspend("초점 상실 · 부분 작업과 보유 물품은 유지됩니다"); }
        private void OnApplicationPause(bool pause) { if (pause) Suspend("응용 프로그램 일시 정지"); }
        private Vector3 HandPosition => HandAnchor != null ? HandAnchor.position : Responder.PlayerCamera.transform.position;
        private Transform View => Responder.PlayerCamera.transform;
        private bool IsInputBlocked => InputConsumed || Responder == null || Responder.PlayerCamera == null || Responder.IsPaused ||
            (!Responder.ExternalInputMode && (!Responder.HasFocus || Cursor.lockState != CursorLockMode.Locked)) ||
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public bool CanBegin(FpsEntityBinding binding, out string reason)
        {
            reason = null;
            if (session == null || !actorAssigned || binding == null || string.IsNullOrEmpty(binding.EntityId))
            { reason = "월드/조작자/대상이 연결되지 않았습니다"; return false; }
            if (IsInputBlocked) { reason = "메뉴 또는 입력 포커스를 먼저 닫으세요"; return false; }
            if (!session.TryGetActor(actorId, out _) || !session.TryGetEntity(new StableId(binding.EntityId), out var currentEntity))
            { reason = "현재 월드에 조작자 또는 대상이 없습니다"; return false; }
            if (binding.ContactSurface == null || !binding.ContactSurface.enabled || binding.ContactSurface.isTrigger)
            { reason = "실제 solid 접촉 형상이 저작되지 않았습니다"; return false; }
            var verb = selectedVerb ?? binding.Verb;
            var workPoint = binding.FindWorkPoint(selectedPoint ?? binding.WorkPointId);
            Vector3 contact = workPoint != null && workPoint.Surface != null ? workPoint.Surface.ClosestPoint(HandPosition) : binding.ContactSurface.ClosestPoint(HandPosition);
            if (!queries.Visible(View.position, contact, Responder.transform, binding))
            { reason = queries.Saturated ? "차폐 질의 포화 · 선택 보류" : "대상이 다른 물체에 가려져 있습니다"; return false; }
            if (!SupportedVerb(verb)) { reason = "이 동사의 물리 실행기가 저작되지 않았습니다"; return false; }
            if (NeedsReach(verb) && (binding.HandReach <= 0 || (contact - HandPosition).sqrMagnitude > binding.HandReach * binding.HandReach))
            { reason = "저작된 손/도구 작업 범위에 접근하세요"; return false; }
            if (currentEntity.Kind == EntityKind.Actor && (verb == ActionVerb.PickUp || verb == ActionVerb.Remove || verb == ActionVerb.PutDown))
            { reason = "인물의 지원 책임은 물체 운반과 다릅니다"; return false; }
            if ((verb == ActionVerb.PickUp || verb == ActionVerb.Remove || verb == ActionVerb.PutDown) && !binding.CanDriveBody(out reason)) return false;
            if ((verb == ActionVerb.PickUp || verb == ActionVerb.Remove) && held != null && held != binding) { reason = "먼저 보유 물품을 내려놓거나 인계하세요"; return false; }
            if ((verb == ActionVerb.PutDown || verb == ActionVerb.DisposeWaste ||
                verb == ActionVerb.Handoff && currentEntity.Kind != EntityKind.Actor) && held != binding)
            { reason = "실제로 보유한 물품을 선택하세요"; return false; }
            if (verb == ActionVerb.Remove)
            {
                if (binding.ExtractionFrame == null || binding.ExtractionFrame.IsChildOf(binding.transform) || binding.ExtractionMetres <= 0)
                { reason = "고정된 분리 방향/원점과 실제 인출 거리가 필요합니다"; return false; }
                if (currentEntity.Fact("installed") != RuleTruth.TRUE || !currentEntity.ParentId.HasValue ||
                    !session.TryGetEntity(currentEntity.ParentId.Value, out var socketEntity) || socketEntity.Fact("isolated") != RuleTruth.TRUE)
                { reason = "현재 설치와 격리 상태가 확인되지 않았습니다"; return false; }
                foreach (var fact in currentEntity.Facts)
                    if (fact.Key.StartsWith("required.fastener:", StringComparison.Ordinal) && fact.Value == RuleTruth.TRUE &&
                        currentEntity.Fact("released:" + fact.Key.Substring(18)) != RuleTruth.TRUE)
                    { reason = "필수 체결점을 먼저 해제하세요"; return false; }
            }
            if (verb == ActionVerb.Open || verb == ActionVerb.Close || verb == ActionVerb.Isolate)
            {
                if (!binding.CanDriveBody(out reason)) return false;
                if ((binding.Hinge == null) == (binding.Slider == null) || Mathf.Abs(binding.OpenCoordinate - binding.ClosedCoordinate) < .0001f)
                { reason = "한 가지 실제 관절과 열린/닫힌 끝점이 필요합니다"; return false; }
            }
            if (verb == ActionVerb.Clean || verb == ActionVerb.Fasten || verb == ActionVerb.Unfasten || verb == ActionVerb.Measure)
            {
                if (workPoint == null || workPoint.Surface == null || workPoint.Frame == null || workPoint.ContactRadius <= 0)
                { reason = "작업점 접촉 영역과 방향이 저작되지 않았습니다"; return false; }
                string toolId = selectedTool ?? binding.ToolId;
                if (string.IsNullOrEmpty(toolId) || !bindings.TryGetValue(toolId, out var selected) || selected != held || selected.ToolHead == null || selected.ToolHeadRadius <= 0)
                { reason = "해당 실물 공구를 집고 공구 헤드를 지정하세요"; return false; }
                if (!session.TryGetEntity(new StableId(toolId), out var toolEntity) || string.IsNullOrEmpty(workPoint.CompatibleToolDefinition) || toolEntity.DefinitionId != workPoint.CompatibleToolDefinition)
                { reason = "작업점에 호환되는 공구 근거가 없습니다"; return false; }
                if (verb == ActionVerb.Clean && workPoint.StrokeMetres <= 0 ||
                    (verb == ActionVerb.Fasten || verb == ActionVerb.Unfasten) && (workPoint.RotationDegrees <= 0 || workPoint.Axis.sqrMagnitude < .001f) ||
                    verb == ActionVerb.Measure && workPoint.ProbeSeconds <= 0)
                { reason = "해당 작업의 실제 이동/회전/샘플 안정 조건이 없습니다"; return false; }
            }
            if (verb == ActionVerb.Install || verb == ActionVerb.Refill)
            {
                if (held == null || binding.Socket == null || binding.SocketPositionTolerance <= 0 || binding.SocketAngleTolerance <= 0 || string.IsNullOrEmpty(binding.CompatiblePartDefinition))
                { reason = "보유 부품과 저작된 호환 소켓/정렬 허용범위가 필요합니다"; return false; }
                if (!session.TryGetEntity(new StableId(held.EntityId), out var part) || part.DefinitionId != binding.CompatiblePartDefinition)
                { reason = "소켓과 보유 부품이 호환되지 않습니다"; return false; }
            }
            if (verb == ActionVerb.DisposeWaste && (string.IsNullOrEmpty(binding.RecipientId) ||
                !bindings.TryGetValue(binding.RecipientId, out var bin) || bin.Socket == null ||
                bin.SocketPositionTolerance <= 0 || bin.SocketAngleTolerance <= 0))
            { reason = "실물 수거 용기와 투입점이 필요합니다"; return false; }
            if (verb == ActionVerb.Cordon && (held == null || !held.CanDriveBody(out reason)))
            { reason = "직접 운반한 표지를 안정된 지지면에 배치하세요"; return false; }
            return true;
        }

        public bool Begin(FpsEntityBinding binding, out string reason)
        {
            RefreshBindings();
            if (!CanBegin(binding, out reason)) { Feedback(reason); return false; }
            CancelActive("새 작업을 시작합니다");
            var verb = selectedVerb ?? binding.Verb;
            string toolId = selectedTool ?? binding.ToolId;
            if ((verb == ActionVerb.Install || verb == ActionVerb.Refill || verb == ActionVerb.Cordon) && held != null) toolId = held.EntityId;
            var intent = session.CreateIntent(actorId, verb, new StableId(binding.EntityId),
                string.IsNullOrEmpty(toolId) ? (StableId?)null : new StableId(toolId),
                string.IsNullOrEmpty(binding.RecipientId) ? (StableId?)null : new StableId(binding.RecipientId), selectedPoint ?? binding.WorkPointId);
            LastReceipt = session.RequestAction(intent); reason = LastReceipt.Reason;
            if (Terminal(LastReceipt.Phase)) { Feedback(reason); return false; }
            active = intent; target = binding; point = binding.FindWorkPoint(intent.WorkPointId);
            tool = !string.IsNullOrEmpty(toolId) && bindings.TryGetValue(toolId, out var found) ? found : null;
            recipient = !string.IsNullOrEmpty(binding.RecipientId) && bindings.TryGetValue(binding.RecipientId, out var receiver) ? receiver : null;
            generation = session.Generation; roleRevision = intent.RoleRevision; lastEvidenceTick = -1;
            previousHead = ToolPosition(); previousToolRotation = tool == null ? Quaternion.identity : tool.transform.rotation;
            previousConstraint = target.ConstraintCoordinate; probeElapsed = rotationTravel = creditedRotation = 0;
            actuating = confirm = inputWasHeld = false;
            suppressUntilRelease = true; frozenGrip = false;
            if (verb == ActionVerb.Remove) InitializeGripGeometry(target);
            RefreshInputOwnership();
            activePrompt = verb + " · 좌클릭/Space 작업 · Enter 확인 · Backspace 취소";
            reason = "작업 접수 · 실제 접촉 후 마우스/키보드로 조작하세요"; Feedback(reason); return true;
        }

        private void Update()
        {
            if (session == null || !actorAssigned || Responder == null) return;
            CheckFences();
            RefreshInputOwnership();
            if (IsInputBlocked) { if (active != null || actuating) Suspend("입력 중단 · 부분 작업/보유 책임 유지"); return; }
            if (Responder.ExternalInputMode) return;
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            bool pressed = mouse != null && mouse.leftButton.isPressed || keyboard != null && keyboard.spaceKey.isPressed;
            if (suppressUntilRelease) { if (!pressed) suppressUntilRelease = false; return; }
            if (keyboard != null && keyboard.backspaceKey.wasPressedThisFrame) { CancelActive("사용자 취소 · 부분 작업/보유 책임 유지"); return; }
            Vector2 drag = mouse != null && pressed ? mouse.delta.ReadValue() * .002f : Vector2.zero;
            float depth = mouse == null ? 0 : Mathf.Clamp(mouse.scroll.ReadValue().y / 120, -1, 1) * .04f;
            float rotation = 0;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed) drag.x -= Time.deltaTime * .3f;
                if (keyboard.rightArrowKey.isPressed) drag.x += Time.deltaTime * .3f;
                if (keyboard.upArrowKey.isPressed) drag.y += Time.deltaTime * .3f;
                if (keyboard.downArrowKey.isPressed) drag.y -= Time.deltaTime * .3f;
                if (keyboard.pageUpKey.isPressed) depth += Time.deltaTime * .3f;
                if (keyboard.pageDownKey.isPressed) depth -= Time.deltaTime * .3f;
                if (keyboard.rKey.isPressed) rotation += MaximumRotationSpeed * Time.deltaTime;
                if (keyboard.fKey.isPressed) rotation -= MaximumRotationSpeed * Time.deltaTime;
                if (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed) { rotation += depth * 500 + drag.x * 300; drag = Vector2.zero; depth = 0; }
            }
            StepManipulation(drag, depth, rotation, pressed, keyboard != null && keyboard.enterKey.wasPressedThisFrame, Time.deltaTime);
        }

        /// <summary>Same geometry path for accessible controls and deterministic external input; never submits caller-made evidence.</summary>
        public bool StepManipulation(Vector2 drag, float depth, float rotationDegrees, bool operate, bool confirmPlacement, float seconds)
        {
            if (!Finite(seconds) || seconds <= 0 || seconds > .25f || !Finite(drag.x) || !Finite(drag.y) || !Finite(depth) || !Finite(rotationDegrees) || IsInputBlocked) return false;
            CheckFences();
            if (suppressUntilRelease) { if (!operate) suppressUntilRelease = false; return false; }
            if (inputWasHeld && !operate && active != null && !confirmPlacement) CancelActive("입력 해제 · 부분 작업은 유지됩니다");
            inputWasHeld = operate; actuating = operate; confirm |= confirmPlacement; frozenGrip = false;
            float limit = MaximumHandSpeed * seconds;
            Vector3 motion = Vector3.ClampMagnitude(new Vector3(drag.x, drag.y, depth), limit);
            var moving = held != null ? held : active != null && active.Verb == ActionVerb.Remove ? target : null;
            if (moving != null)
            {
                handOffset += motion;
                handOffset = Vector3.ClampMagnitude(handOffset, Mathf.Max(.05f, moving.HandReach));
                gripTarget = HandPosition + View.rotation * handOffset;
                float angle = Mathf.Clamp(rotationDegrees, -MaximumRotationSpeed * seconds, MaximumRotationSpeed * seconds);
                Vector3 axis = point != null && point.Frame != null ? point.Frame.TransformDirection(point.Axis).normalized : View.forward;
                gripRotation = Quaternion.AngleAxis(angle, axis) * gripRotation;
            }
            rotationInput = Mathf.Clamp(rotationDegrees / Mathf.Max(.001f, MaximumRotationSpeed * seconds) + drag.x / Mathf.Max(.001f, limit), -1, 1);
            if (Mathf.Abs(rotationInput) < .001f && Mathf.Abs(depth) > 0) rotationInput = Mathf.Sign(depth);
            return true;
        }

        private void FixedUpdate()
        {
            if (session == null || !actorAssigned || Responder == null) return;
            CheckFences();
            if (IsInputBlocked) { if (active != null) Suspend("입력 중단 · 물품 보유 유지"); FreezeGrip(); }
            DriveGrip();
            session.SetActorPose(actorId, ToWorld(Responder.transform.position));
            if (held != null && held.Body != null) session.SetPhysicalPose(new StableId(held.EntityId), ToWorld(held.Body.position));
            if (target != null && target.Body != null && target != held) session.SetPhysicalPose(new StableId(target.EntityId), ToWorld(target.Body.position));
            if (active == null || target == null || IsInputBlocked || !actuating && !confirm) return;
            if (!session.TryGetEntity(active.TargetId, out var entity)) { CancelActive("대상이 현재 월드에서 사라졌습니다"); return; }
            if (session.Tick.Microseconds == lastEvidenceTick) return;
            Vector3 contact = ContactPosition();
            bool visible = queries.Visible(View.position, contact, Responder.transform, target, tool);
            if (!visible) { Suspend(queries.Saturated ? "차폐 질의 포화 · 작업 보류" : "차폐로 작업 중단 · 부분 결과 유지"); return; }
            bool reachable = !NeedsReach(active.Verb) || target.HandReach > 0 && (contact - HandPosition).sqrMagnitude <= target.HandReach * target.HandReach;
            bool touching = reachable && queries.ClearPath(HandPosition, contact, .015f, Responder.transform, target, tool);
            bool aligned = true, supported = false;
            StableId? supportId = null;
            double work = 0;
            switch (active.Verb)
            {
                case ActionVerb.PickUp:
                    supported = target.Body != null; work = touching ? 1 : 0; break;
                case ActionVerb.Remove:
                    supported = touching && actuating;
                    float extraction = Vector3.Dot(target.Body.position - target.ExtractionFrame.position, target.ExtractionFrame.forward);
                    work = supported && extraction >= target.ExtractionMetres ? 1 : 0;
                    break;
                case ActionVerb.PutDown:
                    supported = queries.CanPlace(target, Responder.transform, out var placementReason, out var actualSupport);
                    if (supported && actualSupport != null) supportId = new StableId(actualSupport.EntityId);
                    if (supported && active.RecipientId.HasValue && !active.RecipientId.Equals(supportId))
                    { supported = false; placementReason = "선택한 용기/받침의 실제 지지가 확인되지 않았습니다"; }
                    if (!supported) Feedback(placementReason);
                    work = confirm && supported ? 1 : 0; break;
                case ActionVerb.Open:
                case ActionVerb.Close:
                case ActionVerb.Isolate:
                    float endpoint = active.Verb == ActionVerb.Open ? target.OpenCoordinate : target.ClosedCoordinate;
                    float range = Mathf.Abs(target.OpenCoordinate - target.ClosedCoordinate);
                    float coordinate = target.ConstraintCoordinate;
                    float direction = Mathf.Sign(target.OpenCoordinate - target.ClosedCoordinate) * (active.Verb == ActionVerb.Open ? 1 : -1);
                    float remaining = (endpoint - coordinate) * direction;
                    float advancement = (coordinate - previousConstraint) * direction;
                    aligned = Mathf.Abs(remaining) <= range * .01f;
                    work = touching ? aligned ? 1 : Mathf.Clamp01(advancement / range) : 0;
                    if (touching && actuating) target.DriveConstraint(rotationInput, MaximumGripForce);
                    else target.StopConstraint();
                    if (advancement > 0) previousConstraint = coordinate;
                    break;
                case ActionVerb.Clean:
                case ActionVerb.Fasten:
                case ActionVerb.Unfasten:
                case ActionVerb.Measure:
                    touching &= ToolContact(out contact, out aligned);
                    if (active.Verb == ActionVerb.Unfasten) supported = queries.CanPlace(target, Responder.transform, out _, out _);
                    if (touching && aligned)
                    {
                        if (active.Verb == ActionVerb.Clean) work = Mathf.Clamp01(Vector3.Distance(previousHead, ToolPosition()) / point.StrokeMetres);
                        else if (active.Verb == ActionVerb.Measure)
                        { probeElapsed += Time.fixedDeltaTime; work = probeElapsed >= point.ProbeSeconds ? 1 : 0; }
                        else
                        {
                            Vector3 axis = point.Frame.TransformDirection(point.Axis).normalized;
                            Quaternion delta = tool.transform.rotation * Quaternion.Inverse(previousToolRotation);
                            delta.ToAngleAxis(out float angle, out Vector3 actualAxis);
                            if (angle > 180) angle -= 360;
                            float sign = active.Verb == ActionVerb.Unfasten ? -1 : 1;
                            float travel = angle * Vector3.Dot(actualAxis, axis) * point.RotationDirection * sign;
                            if (Finite(travel)) rotationTravel += travel;
                            work = Mathf.Clamp01((rotationTravel - creditedRotation) / point.RotationDegrees);
                            if (rotationTravel > creditedRotation) creditedRotation = rotationTravel;
                        }
                    }
                    else probeElapsed = 0;
                    previousHead = ToolPosition(); previousToolRotation = tool.transform.rotation;
                    break;
                case ActionVerb.Install:
                case ActionVerb.Refill:
                    aligned = SocketAligned(target);
                    supported = aligned && queries.CanPlace(held, Responder.transform, out _, out _);
                    touching &= aligned;
                    work = confirm && touching && supported ? 1 : 0;
                    break;
                case ActionVerb.DisposeWaste:
                    aligned = SocketAligned(recipient);
                    touching &= aligned && recipient != null && queries.Visible(View.position, recipient.Socket.position, Responder.transform, recipient, target);
                    supported = touching && queries.CanPlace(target, Responder.transform, out _, out _);
                    work = confirm && touching && supported ? 1 : 0;
                    break;
                case ActionVerb.Cordon:
                    supported = held != null && queries.CanPlace(held, Responder.transform, out _, out _);
                    aligned = held != null && (held.Body.position - contact).sqrMagnitude <= target.GripTolerance * target.GripTolerance;
                    work = confirm && touching && supported && aligned ? 1 : 0;
                    break;
                case ActionVerb.Handoff:
                    // Recipient consent/reach/custody are atomically rechecked by the kernel.
                    supported = held == target; work = confirm && touching ? 1 : 0; break;
                case ActionVerb.Observe:
                case ActionVerb.Verify:
                case ActionVerb.Report:
                case ActionVerb.RequestHelp:
                case ActionVerb.Consent:
                case ActionVerb.Escort:
                    work = confirm && touching ? 1 : 0; break;
            }
            confirm = false;
            if (work <= 0) return;
            lastEvidenceTick = session.Tick.Microseconds;
            LastReceipt = session.AdvanceAction(actorId, active.IntentId,
                new ActionEvidence(ToWorld(Responder.transform.position), ToWorld(contact), visible, touching, supported, aligned, work, entity.Revision, supportId));
            Feedback(LastReceipt.Reason);
            RefreshWorkVisual();
            if (LastReceipt.Phase == ActionPhase.Completed)
            {
                if (active.Verb == ActionVerb.PickUp || active.Verb == ActionVerb.Remove) AttachGrip(target);
                else if (active.Verb == ActionVerb.PutDown || active.Verb == ActionVerb.Handoff && held == target || active.Verb == ActionVerb.Install ||
                    active.Verb == ActionVerb.DisposeWaste || active.Verb == ActionVerb.Cordon) ReleasePhysicalGrip();
            }
            if (Terminal(LastReceipt.Phase)) EndLocalAction();
        }

        private bool ToolContact(out Vector3 contact, out bool aligned)
        {
            Vector3 head = ToolPosition(); contact = point.Surface.ClosestPoint(head);
            float radius = Mathf.Min(tool.ToolHeadRadius, point.ContactRadius);
            aligned = Vector3.Angle(tool.ToolHead.forward, point.Frame.TransformDirection(point.Axis)) <= point.AlignmentDegrees;
            if (Vector3.Distance(head, contact) > radius + .003f) return false;
            if (!queries.ClearMotion(previousHead, head, radius, Responder.transform, tool, target)) return false;
            // A long stroke cannot skip an intervening region or credit a noncontact start.
            int samples = Mathf.CeilToInt(Vector3.Distance(previousHead, head) / Mathf.Max(.001f, radius));
            if (samples > 32) return false;
            for (int i = 0; i <= samples; i++)
            {
                Vector3 sample = Vector3.Lerp(previousHead, head, samples == 0 ? 0 : (float)i / samples);
                if (Vector3.Distance(sample, point.Surface.ClosestPoint(sample)) > radius + .003f) return false;
            }
            return queries.ClearPath(HandPosition, head, radius, Responder.transform, target, tool);
        }
        private bool SocketAligned(FpsEntityBinding socket)
        {
            if (held == null || socket == null || socket.Socket == null) return false;
            var insertion = held.GripPoint != null ? held.GripPoint : held.transform;
            return Vector3.Distance(insertion.position, socket.Socket.position) <= socket.SocketPositionTolerance &&
                Quaternion.Angle(insertion.rotation, socket.Socket.rotation) <= socket.SocketAngleTolerance;
        }
        private Vector3 ToolPosition() => tool != null && tool.ToolHead != null ? tool.ToolHead.position : ContactPosition();
        private Vector3 ContactPosition()
        {
            if (point != null && point.Surface != null)
                return point.Surface.ClosestPoint(tool != null && tool.ToolHead != null ? tool.ToolHead.position : HandPosition);
            if (target != null && target.GripPoint != null) return target.GripPoint.position;
            return target == null || target.ContactSurface == null ? HandPosition : target.ContactSurface.ClosestPoint(HandPosition);
        }
        private void AttachGrip(FpsEntityBinding binding)
        {
            if (held == binding) return;
            ReleasePhysicalGrip(); held = binding;
            var body = binding.Body;
            InitializeGripGeometry(binding);
            priorCollisionMode = body.collisionDetectionMode; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.WakeUp(); frozenGrip = false;
        }
        private void InitializeGripGeometry(FpsEntityBinding binding)
        {
            var body = binding.Body;
            Vector3 grip = binding.GripPoint != null ? binding.GripPoint.position : binding.ContactSurface.ClosestPoint(HandPosition);
            localGrip = body.transform.InverseTransformPoint(grip); gripTarget = grip;
            handOffset = Quaternion.Inverse(View.rotation) * (grip - HandPosition); gripRotation = body.rotation;
        }
        private void DriveGrip()
        {
            var moving = held != null ? held : active != null && active.Verb == ActionVerb.Remove && actuating ? target : null;
            if (moving == null || moving.Body == null || moving.Body.isKinematic) return;
            var body = moving.Body;
            Vector3 currentGrip = body.transform.TransformPoint(localGrip);
            if (!frozenGrip) gripTarget = HandPosition + View.rotation * handOffset;
            // No MovePosition/parenting/teleport: PhysX resolves the bounded forces against every obstacle.
            Vector3 force = (gripTarget - currentGrip) * PositionSpring - body.GetPointVelocity(currentGrip) * PositionDamping;
            body.AddForceAtPosition(Vector3.ClampMagnitude(force, MaximumGripForce), currentGrip, ForceMode.Force);
            Quaternion error = gripRotation * Quaternion.Inverse(body.rotation);
            error.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180) angle -= 360;
            if (Finite(axis.x) && Finite(axis.y) && Finite(axis.z))
                body.AddTorque(Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad * RotationSpring) - body.angularVelocity * RotationDamping, MaximumGripTorque), ForceMode.Force);
        }
        private void FreezeGrip()
        {
            if (held == null || held.Body == null || frozenGrip) return;
            gripTarget = held.Body.transform.TransformPoint(localGrip); gripRotation = held.Body.rotation; frozenGrip = true;
        }
        private void ReleasePhysicalGrip()
        {
            if (held != null && held.Body != null) held.Body.collisionDetectionMode = priorCollisionMode;
            held = null;
        }
        public void CancelActive(string reason)
        {
            if (active != null && session != null && active.Generation == session.Generation)
                LastReceipt = session.CancelAction(actorId, active.IntentId, reason);
            EndLocalAction(); FreezeGrip();
            if (!string.IsNullOrEmpty(reason)) Feedback(reason);
        }
        private void EndLocalAction()
        {
            if (target != null) target.StopConstraint();
            active = null; target = tool = recipient = null; point = null;
            actuating = confirm = inputWasHeld = false; rotationInput = probeElapsed = 0;
            RefreshInputOwnership();
        }
        private void Suspend(string reason) { CancelActive(reason); suppressUntilRelease = true; }
        private void CheckFences()
        {
            if (session == null || !actorAssigned) return;
            if (generation != session.Generation)
            {
                EndLocalAction(); ReleasePhysicalGrip(); generation = session.Generation; suppressUntilRelease = true;
                RefreshBindings();
                foreach (var pair in bindings)
                {
                    var binding = pair.Value;
                    if (binding.Body == null || !session.TryGetEntity(new StableId(pair.Key), out var entity)) continue;
                    binding.Body.position = ToVector(entity.Position);
                    binding.Body.velocity = Vector3.zero; binding.Body.angularVelocity = Vector3.zero;
                }
                SynchronizeCustody(); Feedback("새 실행 분기 · 이전 접촉/입력 증거 폐기");
            }
            if (active != null && (!session.TryGetActor(actorId, out var actor) || actor.RoleRevision != roleRevision))
                Suspend("직무/권한 변경 · 부분 작업과 보유 책임 유지");
            if (held != null && (!session.TryGetEntity(new StableId(held.EntityId), out var item) || !item.CustodianId.HasValue || !item.CustodianId.Value.Equals(actorId)))
                ReleasePhysicalGrip();
        }
        private void OnCommitted(WorldMutation mutation)
        {
            if (!actorAssigned || mutation.Generation != generation) return;
            foreach (var item in mutation.Entities)
            {
                if (item.Kind == EntityKind.Actor) continue;
                bool owned = item.CustodianId.HasValue && item.CustodianId.Value.Equals(actorId);
                if (held != null && held.EntityId == item.Id.Value && !owned) ReleasePhysicalGrip();
                if (!owned || held != null || Responder == null || Responder.PlayerCamera == null ||
                    !bindings.TryGetValue(item.Id.Value, out var binding) || !binding.CanDriveBody(out _)) continue;
                AttachGrip(binding);
            }
        }
        private void SynchronizeCustody()
        {
            if (session == null || !actorAssigned || Responder == null || Responder.PlayerCamera == null) return;
            foreach (var pair in bindings)
            {
                if (!session.TryGetEntity(new StableId(pair.Key), out var item) || item.Kind == EntityKind.Actor ||
                    !item.CustodianId.HasValue || !item.CustodianId.Value.Equals(actorId)) continue;
                if (pair.Value.CanDriveBody(out _)) { AttachGrip(pair.Value); FreezeGrip(); break; }
            }
        }
        private void RefreshWorkVisual()
        {
            if (point == null || point.Visual == null || !point.CanAnimateVisual || !session.TryGetEntity(active.TargetId, out var entity)) return;
            if (active.Verb != ActionVerb.Fasten && active.Verb != ActionVerb.Unfasten) return;
            bool fastening = active.Verb == ActionVerb.Fasten;
            var progress = entity.Measurement((fastening ? "progress:fasten:" : "progress:unfasten:") + point.Id);
            if (progress.HasValue && progress.Value.Unit == SiUnit.Dimensionless)
                point.Visual.localRotation = point.InitialVisualRotation * Quaternion.AngleAxis(
                    (float)(fastening ? progress.Value.Value : 1 - progress.Value.Value) * point.RotationDegrees * point.RotationDirection, point.Axis.normalized);
        }
        private void Feedback(string message)
        {
            if (string.IsNullOrEmpty(message) || string.Equals(message, LastFeedback, StringComparison.Ordinal)) return;
            LastFeedback = message; FeedbackChanged?.Invoke(message); if (Responder != null) Responder.ShowFeedback(message);
        }
        private void RefreshInputOwnership()
        {
            if (Responder == null) return;
            bool bound = session != null && isActiveAndEnabled;
            Responder.SuppressLookInput = bound && (active != null || held != null && actuating);
            // Carrying alone must still permit E to select a socket, work point or put-down action.
            Responder.SuppressInteractionInput = bound && (InputConsumed || active != null);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Terminal(ActionPhase phase) => phase == ActionPhase.Completed || phase == ActionPhase.Cancelled || phase == ActionPhase.Failed || phase == ActionPhase.Blocked;
        private static bool NeedsReach(ActionVerb verb) => verb != ActionVerb.Observe && verb != ActionVerb.Report && verb != ActionVerb.RequestHelp;
        private static bool SupportedVerb(ActionVerb verb) => verb != ActionVerb.Wait && verb != ActionVerb.MoveTo && verb != ActionVerb.Cancel;
        private static WorldPoint ToWorld(Vector3 point) => new WorldPoint(point.x, point.y, point.z);
        private static Vector3 ToVector(WorldPoint point) => new Vector3(point.X, point.Y, point.Z);
    }
}
