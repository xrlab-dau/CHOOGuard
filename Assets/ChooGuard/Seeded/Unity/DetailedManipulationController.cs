using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChooGuard.Seeded.Unity
{
    /// <summary>Only explicit control proxies or qualified source leaves receive local part motion.</summary>
    public sealed class DetailedPartBinding : MonoBehaviour
    {
        public string OwnerId, PartId;
        public bool TrainingProxy;
    }
    public sealed partial class DetailedManipulationController : MonoBehaviour
    {
        public MechanicalInteractions Mechanics { get; private set; }
        public string Hint { get; private set; } = "Aim at a part; hold left mouse to grip; drag or wheel to move; release to let go.";
        public string SelectedToolId { get; private set; }
        public string InspectedObjectId { get; private set; }
        public SourceLeafClearanceResult LastClearance {get;private set;}
        public bool ShowHints = true;
        public bool ExternalInputMode { get; private set; }
        public DetailedPartBinding AimedPart => aimed;
        public DetailedPartBinding HeldPart => held;
        public Func<bool> InputBlocked;
        public bool ConsumesPointerMotion=>InputBlocked?.Invoke()==true||held!=null&&poses.TryGetValue(held.OwnerId+"|"+held.PartId,out var p)&&p.Explicit!=null&&(p.Definition.Motion=="drag"||p.Definition.Motion=="rotate");
        readonly Dictionary<string,double> requestedPositions=new Dictionary<string,double>();
        int ignoreGripThroughFrame=-1;
        public void GuardCaptureClick(){ignoreGripThroughFrame=Time.frameCount+1;}
        public string PlacementFeedback => Time.unscaledTime < placementFeedbackUntil ? placementFeedback : null;
        string placementFeedback;
        float placementFeedbackUntil;
        IDetailedManipulationHost host;
        Transform generated;
        Material material;
        long sequence;
        readonly Queue<MechanicalInput> pending = new Queue<MechanicalInput>();
        readonly Dictionary<string, Transform> bodies = new Dictionary<string, Transform>(StringComparer.Ordinal);
        readonly Dictionary<string, PartPose> poses = new Dictionary<string, PartPose>(StringComparer.Ordinal);
        readonly Dictionary<string, LeafPose> leaves = new Dictionary<string, LeafPose>(StringComparer.Ordinal);
        readonly Dictionary<string, double> accumulated = new Dictionary<string, double>(StringComparer.Ordinal);
        readonly Dictionary<string, double> effortTime = new Dictionary<string, double>(StringComparer.Ordinal);
        readonly List<string> tools = new List<string>();
        readonly Dictionary<string, EquipmentPose> equipment = new Dictionary<string, EquipmentPose>(StringComparer.Ordinal);
        readonly Dictionary<long, Quaternion> placementRotations = new Dictionary<long, Quaternion>();
        DetailedPartBinding held, aimed;
        RaycastHit aimedHit;
        string auxiliaryAction;
        sealed class PartPose
        {
            public DetailedPartBinding Binding; public Vector3 Rest; public Quaternion Rotation; public MechanicalPart Definition;
            public DetailedPartPoseBinding Explicit; public Quaternion PartToAimRotation;
        }
        sealed class EquipmentPose
        {
            public Transform Root; public Vector3 CenterLocal, CarryLocal, LastActorPosition, CarryStart;
            public Transform DockAnchor; public Vector3 DockLocal; public Quaternion DockRotation;
            public bool StableBodyDock, DockSettled; public Quaternion DockBasis; public Vector3 DockLastOrigin;
            public Quaternion CarryRotation, PlacedRotation, AnchorRotation, CarryStartRotation;
            public bool WasCarried, FollowActorHeading; public float CarryProgress; public int ProjectionFrame=-1;
        }
        sealed class SimulatorHost : IDetailedManipulationHost
        {
            readonly PlatformSeededSimulator simulator;
            public SimulatorHost(PlatformSeededSimulator value) { simulator=value; }
            public World State => simulator.State;
            public Camera View => simulator.Player != null ? simulator.Player.View : null;
            public Transform Actor => simulator.Player != null ? simulator.Player.transform : null;
            public Transform Root => simulator.transform;
            public bool Running => simulator.Running;
            public bool Captured => simulator.Player != null && simulator.Player.Captured;
            public bool IsReplaying => simulator.IsReplaying;
            public StationWorldAdapter WorldSnapshot => simulator.WorldSnapshot;
            public string ResolveResponseSubject(string id) => simulator.ResolveResponseSubject(id);
        }
        sealed class LeafPose { public string OwnerId; public Transform Transform; public Vector3 Closed, Open; public bool External; }

        public void Initialize(PlatformSeededSimulator sim)
        {
            if (sim == null || sim.State == null) throw new ArgumentException("A running local world is required");
            var mechanics = new MechanicalInteractions(sim.State, Resources.Load<TextAsset>("MechanicalProfiles")?.text);
            mechanics.InitializeDefaults(sim.WorldSnapshot?.Registry);
            Initialize(new SimulatorHost(sim), mechanics, true);
        }
        /// <summary>Uses an existing local world and mechanisms; no keys or primitive equipment are added by default.</summary>
        public void Initialize(IDetailedManipulationHost context, MechanicalInteractions mechanics, bool createTrainingControls = false)
        {
            if (context == null || mechanics == null || context.State != mechanics.World || context.Root == null ||
                !context.State.Entities.ContainsKey("player")) throw new ArgumentException("Matching local host and mechanics required");
            Clear(); host = context; Mechanics = mechanics;
            sequence = (long)context.State.Entities["player"].Get("mech.last-sequence");
            if (createTrainingControls)
            {
                generated = new GameObject("Inferred granular training controls").transform; generated.SetParent(context.Root, false);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { name = "INFERRED_TRAINING control material" }; material.color = new Color(.8f, .31f, .06f);
                foreach (var ownerId in Mechanics.Owners) BuildControls(ownerId);
                BuildTrainingTools();
            }
            Project();
        }
        public void SetExternalInputMode(bool enabled) { ResetInputState(); ExternalInputMode = enabled; }
        /// <summary>Associates only explicitly authored runtime equipment with real components and their physical poses.</summary>
        public void BindEquipment(string ownerId, Transform carryRoot, IEnumerable<DetailedPartPoseBinding> parts,
            Vector3? carryLocalPosition = null, Quaternion? carryLocalRotation = null)
        {
            if (Mechanics == null || host == null || carryRoot == null || !carryRoot.IsChildOf(host.Root) ||
                !Mechanics.HasProfile(ownerId)) throw new ArgumentException("Owned runtime equipment and assigned profile required");
            var definitions = Mechanics.Parts(ownerId).ToDictionary(p => p.Id, StringComparer.Ordinal);
            var requested = (parts ?? Enumerable.Empty<DetailedPartPoseBinding>()).ToArray();
            if (requested.Length == 0 || requested.Any(p => p == null || p.Part == null || !p.Part.IsChildOf(carryRoot) ||
                !definitions.ContainsKey(p.PartId) || !Finite(p.MinLocalPosition) || !Finite(p.MaxLocalPosition) ||
                !Finite(p.MinLocalRotation) || !Finite(p.MaxLocalRotation) ||
                p.Part.GetComponentsInChildren<Collider>(true).Length == 0 ||
                float.IsNaN(p.AimAfterNormalized) || float.IsInfinity(p.AimAfterNormalized) || p.AimAfterNormalized < 0 || p.AimAfterNormalized > 1) ||
                requested.Select(p => p.PartId).Distinct(StringComparer.Ordinal).Count() != requested.Length)
                throw new ArgumentException("Unique real collider-backed components and finite explicit poses required");
            var carryAt = carryLocalPosition ?? new Vector3(.3f, -.3f, .6f);
            var carryRotation = carryLocalRotation ?? Quaternion.identity;
            if (!Finite(carryAt) || !Finite(carryRotation)) throw new ArgumentException("Finite carry pose required");
            if (equipment.TryGetValue(ownerId, out var previous)) SetCarryCollisions(previous, false);
            bodies[ownerId] = carryRoot;
            equipment[ownerId] = new EquipmentPose { Root = carryRoot, CenterLocal = carryRoot.InverseTransformPoint(OwnerPoint(ownerId)),
                CarryLocal = carryAt, CarryRotation = carryRotation, PlacedRotation = carryRoot.rotation };
            foreach (var part in requested)
            {
                var binding = part.Part.GetComponent<DetailedPartBinding>() ?? part.Part.gameObject.AddComponent<DetailedPartBinding>();
                binding.OwnerId = ownerId; binding.PartId = part.PartId; binding.TrainingProxy = false;
                var semantic = part.Part.GetComponent<SemanticBinding>() ?? part.Part.gameObject.AddComponent<SemanticBinding>(); semantic.EntityId = ownerId;
                var aimOrigin = part.AimOrigin != null ? part.AimOrigin : part.Part;
                poses[ownerId + "|" + part.PartId] = new PartPose { Binding = binding, Definition = definitions[part.PartId], Explicit = part,
                    PartToAimRotation = Quaternion.Inverse(part.Part.rotation) * aimOrigin.rotation };
            }
            if (definitions.Values.Any(p => p.Carryable) && !tools.Contains(ownerId)) tools.Add(ownerId);
            Project();
        }
        void Clear()
        {
            requestedPositions.Clear();latchedPress=null;latchedDuration=0;
            if(carryProbe!=null)Destroy(carryProbe.gameObject);carryProbe=null;
            if (generated != null) Destroy(generated.gameObject); if (material != null) Destroy(material);
            foreach (var item in equipment.Values) SetCarryCollisions(item, false);
            pending.Clear(); poses.Clear(); leaves.Clear(); bodies.Clear(); equipment.Clear(); placementRotations.Clear(); accumulated.Clear(); effortTime.Clear(); tools.Clear(); held = aimed = null; Mechanics = null; SelectedToolId = null; host = null;
        }
        void OnDestroy() { Clear(); }
        public void ResetInputState() { pending.Clear(); accumulated.Clear(); effortTime.Clear(); requestedPositions.Clear(); held = aimed = pickupTarget = null;latchedPress=null;latchedDuration=0; auxiliaryAction = null; ClearPartInputFeedback(); }
        public void Queue(MechanicalInput input)
        {
            if (input == null || Mechanics == null) throw new ArgumentException("Initialized mechanical input required");
            pending.Enqueue(input.Copy()); sequence = Math.Max(sequence, input.Sequence);
        }
        /// <summary>Caller supplies an exact, reviewed runtime leaf association; names never qualify a movable mesh.</summary>
        public void RegisterLeaf(string ownerId, Transform leaf, Vector3 closedLocal, Vector3 openLocal, bool projectedBySimulator = true)
        {
            if (Mechanics == null || !Mechanics.HasProfile(ownerId) || leaf == null || !Finite(closedLocal) || !Finite(openLocal)) throw new ArgumentException("Explicit finite runtime leaf binding required");
            string key = ownerId + "|" + leaf.GetInstanceID();
            leaves[key] = new LeafPose { OwnerId = ownerId, Transform = leaf, Closed = closedLocal, Open = openLocal, External = projectedBySimulator };
            var collider = leaf.GetComponent<Collider>(); var renderer = leaf.GetComponent<Renderer>();
            if (Mechanics.World.Entities[ownerId].Get("mech.center.qualified") != 1)
            { if (collider != null) Center(ownerId, collider.bounds); else if (renderer != null) Center(ownerId, renderer.bounds); }
            var binding = leaf.GetComponent<DetailedPartBinding>() ?? leaf.gameObject.AddComponent<DetailedPartBinding>();
            binding.OwnerId = ownerId; binding.PartId = "leaf"; binding.TrainingProxy = false;
            PositionDoorControls(ownerId);
        }
        static bool Finite(Quaternion value) => Finite(new Vector3(value.x, value.y, value.z)) && !float.IsNaN(value.w) && !float.IsInfinity(value.w) && value.x*value.x+value.y*value.y+value.z*value.z+value.w*value.w > .000001f;
        static bool Finite(Vector3 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.y) && !float.IsNaN(point.z) && !float.IsInfinity(point.z);
        void Center(string owner, Bounds b) { Mechanics.SetInteractionCenter(owner, b.center.x, b.center.y, b.center.z); }
        Vector3 OwnerPoint(string id)
        {
            var e = Mechanics.World.Entities[id];
            return new Vector3((float)e.Get("mech.center.x", e.X), (float)e.Get("mech.center.y", e.Y + .6), (float)e.Get("mech.center.z", e.Z));
        }
        void PositionDoorControls(string owner)
        {
            if (bodies.TryGetValue(owner, out var body)) body.position = OwnerPoint(owner);
        }
        void BuildControls(string ownerId)
        {
            var profile = Mechanics.Inspect(ownerId).ProfileId; var root = new GameObject("Training mechanism " + ownerId).transform; root.SetParent(generated, false); root.position = OwnerPoint(ownerId); bodies[ownerId] = root;
            // Replace only the simulator's generated training cube. Original station renderers are untouched.
            foreach (var existing in host.Root.GetComponentsInChildren<SemanticBinding>(true))
                if (existing.EntityId == ownerId && existing.name.StartsWith("Training parameter prop", StringComparison.Ordinal)) existing.gameObject.SetActive(false);
            foreach (var part in Mechanics.Parts(ownerId))
            {
                Vector3 offset, scale; PrimitiveType primitive = PrimitiveType.Cube;
                if (part.Id == "body") { offset = Vector3.zero; scale = profile == "extinguisher" ? new Vector3(.18f, .36f, .18f) : new Vector3(.14f, .22f, .08f); primitive = profile == "extinguisher" ? PrimitiveType.Cylinder : PrimitiveType.Cube; }
                else if (part.Id == "leaf") { offset = new Vector3(.03f, 0, 0); scale = profile == "breaker-cabinet" ? new Vector3(.36f, .48f, .05f) : new Vector3(.08f, .08f, .08f); }
                else if (part.Id == "latch") { offset = new Vector3(.17f, .04f, -.06f); scale = new Vector3(.09f, .035f, .035f); }
                else if (part.Id == "lock") { offset = new Vector3(.17f, -.045f, -.07f); scale = new Vector3(.04f, .04f, .035f); }
                else if (part.Id == "switch") { offset = new Vector3(0, .04f, -.035f); scale = new Vector3(.045f, .075f, .04f); }
                else if (part.Id == "pin") { offset = new Vector3(.10f, .34f, 0); scale = new Vector3(.06f, .025f, .025f); }
                else if (part.Id == "lever") { offset = new Vector3(0, .39f, 0); scale = new Vector3(.16f, .025f, .05f); }
                else if (part.Id == "nozzle") { offset = new Vector3(.17f, .2f, 0); scale = new Vector3(.04f, .04f, .16f); }
                else if (part.Id == "ptt") { offset = new Vector3(.09f, .04f, 0); scale = new Vector3(.035f, .065f, .05f); }
                else if (part.Id == "power" || part.Id == "channel") { offset = new Vector3(part.Id == "power" ? -.04f : .04f, .15f, 0); scale = new Vector3(.035f, .04f, .035f); }
                else { offset = new Vector3(part.Id == "plug" ? .13f : 0, 0, 0); scale = new Vector3(.06f, .06f, .06f); }
                var go = GameObject.CreatePrimitive(primitive); go.name = part.Id + " · INFERRED_TRAINING"; go.transform.SetParent(root, false); go.transform.localPosition = offset; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
                var binding = go.AddComponent<DetailedPartBinding>(); binding.OwnerId = ownerId; binding.PartId = part.Id; binding.TrainingProxy = true;
                go.AddComponent<SemanticBinding>().EntityId = ownerId; go.GetComponent<Collider>().isTrigger = true;
                poses[ownerId + "|" + part.Id] = new PartPose { Binding = binding, Rest = offset, Rotation = Quaternion.identity, Definition = part };
            }
            if (profile == "breaker-cabinet")
            {
                var back = GameObject.CreatePrimitive(PrimitiveType.Cube); back.name = "Cabinet training back"; back.transform.SetParent(root, false); back.transform.localPosition = new Vector3(0, 0, .09f); back.transform.localScale = new Vector3(.4f, .55f, .12f); back.GetComponent<Renderer>().sharedMaterial = material;back.GetComponent<Collider>().isTrigger=true;
            }
        }
        void BuildTrainingTools()
        {
            var player = host.State.Entities["player"]; int index = 0;
            foreach (var kind in new[] { "key", "wrench", "voltage-probe", "lockout-tag" })
            {
                string id = "training-tool:" + kind;
                if (!host.State.Entities.ContainsKey(id))
                {
                    var tool = new Entity { Id = id, Kind = kind, X = player.X + .55 + .18 * index, Y = player.Y + .3, Z = player.Z + .4 };
                    tool.Tags.Add("training-model"); tool.Tags.Add("tool:" + kind);
                    if (kind == "key") foreach (var owner in Mechanics.Owners.Where(id => Mechanics.Inspect(id).ProfileId == "breaker-cabinet" && !host.State.Entities[id].Has("source-object"))) tool.Set("mech.key-owner." + owner, 1);
                    tool.Set("mech.resource", kind == "lockout-tag" ? 4 : 1); host.State.Add(tool);
                }
                Mechanics.Attach(id, "small-tool"); BuildControls(id); tools.Add(id); index++;
            }
        }
        MechanicalInput Input(DetailedPartBinding part, string action, double analog = 0, double duration = 0)
        {
            return new MechanicalInput { Tick = Mechanics.World.Tick, Sequence = ++sequence, ActorId = "player", OwnerId = part.OwnerId, PartId = part.PartId, Action = action, ToolId = SelectedToolId, Analog = analog, Duration = duration, Contact = held == part || latchedPress == part || aimed != null && aimed.OwnerId == part.OwnerId && aimed.PartId == part.PartId, Clearance = 1 };
        }
        void Update()
        {
            if (ExternalInputMode) return;
            var mouse = Mouse.current; var keyboard = Keyboard.current; if (mouse == null || keyboard == null) return;
            StepInput(new DetailedInputFrame {
                GripPressed = mouse.leftButton.wasPressedThisFrame, GripReleased = mouse.leftButton.wasReleasedThisFrame,
                CarryPressed = keyboard.fKey.wasPressedThisFrame, PlacePressed = keyboard.gKey.wasPressedThisFrame,
                CycleToolPressed = keyboard.tabKey.wasPressedThisFrame, ConnectPressed = keyboard.vKey.wasPressedThisFrame,
                DisconnectPressed = keyboard.bKey.wasPressedThisFrame, CheckHeld = keyboard.cKey.isPressed,
                TagHeld = keyboard.tKey.isPressed, ReverseHeld = keyboard.leftShiftKey.isPressed,
                PointerDeltaX = mouse.delta.ReadValue().x, PointerDeltaY = mouse.delta.ReadValue().y,
                ScrollDelta = WheelDetents(mouse.scroll.ReadValue().y)
            }, Mathf.Min(Time.deltaTime, .1f));
        }
        /// <summary>Shared physical input path: the real camera ray, grip and placement observations qualify every command.</summary>
        public bool StepInput(DetailedInputFrame frame, float deltaSeconds)
        {
            if (host == null || !host.Running || host.IsReplaying || Mechanics == null || host.State != Mechanics.World || host.Actor == null ||
                host.View == null || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0 || deltaSeconds > .1f ||
                float.IsNaN(frame.PointerDeltaX) || float.IsInfinity(frame.PointerDeltaX) || float.IsNaN(frame.PointerDeltaY) || float.IsInfinity(frame.PointerDeltaY) || float.IsNaN(frame.ScrollDelta) || float.IsInfinity(frame.ScrollDelta)) return false;
            if(InputBlocked?.Invoke()==true)
            {
                Mechanics.ReleaseActorGrips("player");ResetInputState();ClearPartInputFeedback();return false;
            }
            if (!host.Captured)
            {
                if (held != null) { FlushHeld(); pending.Enqueue(Input(held, "release")); }
                held = aimed = null; InspectedObjectId = null; return false;
            }
            aimed = null; InspectedObjectId = null;
            Physics.SyncTransforms();
            var ray = host.View.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            aimedHit = Physics.RaycastAll(ray, 2.4f, ~(1 << 2), QueryTriggerInteraction.Collide)
                .Where(h => !h.collider.transform.IsChildOf(host.Actor)).OrderBy(h => h.distance).FirstOrDefault();
            if (aimedHit.collider != null)
            {
                aimed = ResolvePartTarget(ray,aimedHit.collider);
                if (aimed != null && Mechanics.HasProfile(aimed.OwnerId)) { InspectedObjectId = aimed.OwnerId; Hint = aimed.OwnerId + " / " + aimed.PartId + " · " + Mechanics.Inspect(aimed.OwnerId).Claim; }
                else
                {
                    aimed = null;
                    if (host.WorldSnapshot != null)
                    {
                        InspectedObjectId = host.WorldSnapshot.ResolveSourceObject(aimedHit.collider);
                        if (!string.IsNullOrEmpty(InspectedObjectId)) Hint = "Inspect " + InspectedObjectId + " · saved source; mechanism unassigned";
                    }
                }
            }
            else aimed=ResolvePartTarget(ray,null);
            if(aimed!=null)InspectedObjectId=aimed.OwnerId;
            if (frame.CycleToolPressed && tools.Count > 0)
            {
                int current = tools.IndexOf(SelectedToolId); SelectedToolId = tools[(current + 1) % tools.Count]; Hint = "Selected " + SelectedToolId + "; pick up its body before use";
            }
            UpdatePickupTarget(ray);
            AccumulateLatched(deltaSeconds);
            if(frame.GripPressed&&Time.frameCount>ignoreGripThroughFrame&&aimed!=null&&
                poses.TryGetValue(aimed.OwnerId+"|"+aimed.PartId,out var clicked)&&clicked.Explicit?.ClickLatched==true)
            {
                if(latchedPress==aimed)StopLatchedPress();
                else {StopLatchedPress();latchedPress=aimed;pending.Enqueue(Input(latchedPress,"grip"));}
                frame.GripPressed=false;
            }
            if(frame.CarryPressed&&TryDirectCarry())return true;
            if(frame.PlacePressed&&TryDirectPlace())return true;
            if (frame.GripPressed && Time.frameCount>ignoreGripThroughFrame && aimed != null)
            {
                if (held != null && held != aimed) { FlushHeld(); pending.Enqueue(Input(held, "release")); }
                held = aimed; pending.Enqueue(Input(held, "grip"));
                ClearPartInputFeedback();
                requestedPositions[held.OwnerId+"|"+held.PartId]=Mechanics.Position(held.OwnerId,held.PartId);
            }
            else if(frame.GripPressed&&Time.frameCount>ignoreGripThroughFrame&&aimed==null)
                ExplainPartInput("선택된 부품이 없습니다. 화면 중앙의 조준점을 부품 가까이에 맞춰 주세요.");
            if (frame.GripReleased && held != null) { FlushHeld(); pending.Enqueue(Input(held, "release")); held = null; }
            if (held == null)
            {
                if(Mathf.Abs(frame.ScrollDelta)>.001f)ExplainPartInput("부품을 잡지 않은 상태입니다. 조준한 부품을 좌클릭으로 누른 채 조작하세요.");
                return true;
            }
            string key = held.OwnerId + "|" + held.PartId;
            var definition = Mechanics.Parts(held.OwnerId).First(p => p.Id == held.PartId);
            if (frame.CarryPressed && definition.Carryable) pending.Enqueue(Input(held, "carry"));
            if (frame.PlacePressed && definition.Carryable)
            {
                if (equipment.ContainsKey(held.OwnerId))
                {
                    if (TryExplicitPlacement(held.OwnerId, out var destination, out var rotation))
                    {
                        var place = Input(held, "place"); place.X = destination.x; place.Y = destination.y; place.Z = destination.z;
                        placementRotations[place.Sequence] = rotation; pending.Enqueue(place);
                    }
                }
                else
                {
                    Vector3 destination = aimedHit.collider != null ? aimedHit.point + aimedHit.normal * .15f : host.Actor.position + host.Actor.forward * .7f;
                    var input = Input(held, "place"); input.X = destination.x; input.Y = destination.y; input.Z = destination.z; input.Clearance = PlacementClear(held.OwnerId, destination) ? 1 : 0; pending.Enqueue(input);
                }
            }
            if (frame.ConnectPressed || frame.DisconnectPressed)
            {
                FlushHeld(); var input = Input(held, frame.ConnectPressed ? "connect" : "disconnect");
                input.TargetId = aimedHit.collider != null ? aimedHit.collider.GetComponentInParent<SemanticBinding>()?.EntityId : null;
                input.Clearance = aimedHit.collider != null ? 1 : 0; pending.Enqueue(input);
            }
            string action = frame.CheckHeld ? "check" : frame.TagHeld ? "tag" : null;
            if (action != auxiliaryAction) { FlushHeld(); auxiliaryAction = action; }
            double dt = deltaSeconds;
            double analog;
            if(ConsumesPointerMotion)
            {
                double requested=requestedPositions.TryGetValue(key,out var previousTarget)?previousTarget:Mechanics.Position(held.OwnerId,held.PartId);
                double change=PointerStroke(poses[key],frame)*.008+frame.ScrollDelta*.08;
                if(frame.ReverseHeld)change=-Math.Abs(change==0?definition.Speed*dt:change);
                double minimum=definition.OneWay?Math.Max(definition.Min,Mechanics.Position(held.OwnerId,held.PartId)):definition.Min;
                requested=Math.Max(minimum,Math.Min(definition.Max,requested+change));requestedPositions[key]=requested;
                if(definition.OneWay&&change<-.0001)ExplainPartInput("분리 방향으로 움직여 주세요 · "+PullDirection(poses[key])+" / 휠 위로");
                double friction=definition.Friction*Mechanics.World.Entities[held.OwnerId].Get("mech.friction-scale",1);
                double predicted=Mechanics.Position(held.OwnerId,held.PartId);
                if(accumulated.TryGetValue(key,out var queuedTime)&&queuedTime>0&&effortTime.TryGetValue(key,out var queuedEffort))
                {
                    double mean=queuedEffort/queuedTime;
                    double effortBefore=Math.Sign(mean)*Math.Max(0,Math.Abs(mean)-friction)/(1-friction);
                    predicted+=effortBefore*definition.Speed*Math.Min(queuedTime,Mechanics.World.StepSeconds);
                }
                double error=requested-predicted;if(definition.OneWay)error=Math.Max(0,error);
                if(Math.Abs(error)<1e-8)error=0;
                double effort=Math.Max(-1,Math.Min(1,error/(definition.Speed*dt)));
                analog=effort==0?0:Math.Sign(effort)*(friction+Math.Abs(effort)*(1-friction));
            }
            else
            {
                analog=definition.Motion=="press"?1:Math.Max(-1,Math.Min(1,frame.PointerDeltaX*.04+frame.ScrollDelta*.02));
                if(frame.ReverseHeld)analog=-Math.Abs(analog==0?1:analog);
            }
            if (definition.Motion != "carry" && (Math.Abs(analog) > 0 || auxiliaryAction != null))
            {
                accumulated[key] = accumulated.TryGetValue(key, out var previous) ? previous + dt : dt;
                effortTime[key] = effortTime.TryGetValue(key, out var previousEffort) ? previousEffort + analog * dt : analog * dt;
            }
            return true;
        }
        bool TryExplicitPlacement(string owner, out Vector3 center, out Quaternion rotation)
        {
            center = default; rotation = Quaternion.identity;
            var item = equipment[owner];
            if(item.DockAnchor!=null)
            {
                DockPose(item,out var dockPosition,out rotation);
                center=dockPosition+rotation*Vector3.Scale(item.CenterLocal,item.Root.lossyScale);
                return true;
            }
            var ray = host.View.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            var hit = Physics.RaycastAll(ray, 2.4f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                .Where(h => !WithinOwner(h.collider, owner) && !h.collider.transform.IsChildOf(host.Actor)).OrderBy(h => h.distance).FirstOrDefault();
            if (hit.collider == null || hit.normal.y < .65f)
            { PlacementFailed("놓을 수 있는 가까운 바닥을 향해 주세요.","view-hit="+hit.collider?.name+" at="+hit.point+" normal="+hit.normal); return false; }
            var localBounds = EquipmentBounds(item.Root);
            var scale = item.Root.lossyScale;
            rotation = Quaternion.Euler(0, host.Actor.eulerAngles.y, 0);
            var rootPosition = hit.point + Vector3.up * (.015f - localBounds.min.y * Mathf.Abs(scale.y));
            var boundsCenter = rootPosition + rotation * Vector3.Scale(localBounds.center, scale);
            var half = Vector3.Scale(localBounds.extents, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            half = Vector3.Max(half - Vector3.one * .003f, Vector3.one * .005f);
            var blockers=Physics.OverlapBox(boundsCenter,half,rotation,~(1<<2),QueryTriggerInteraction.Ignore).Where(c=>!WithinOwner(c,owner)).ToArray();
            if (blockers.Length>0)
            { PlacementFailed("물체와 사람이 없는 바닥 공간에 놓아 주세요.","center="+boundsCenter+" half="+half+" blockers="+string.Join(";",blockers.Select(c=>c.name+" "+c.bounds))); return false; }
            placementFeedback=null;
            center = rootPosition + rotation * Vector3.Scale(item.CenterLocal, scale); return true;
        }
        void PlacementFailed(string message,string observation)
        {
            Hint=placementFeedback=message;placementFeedbackUntil=Time.unscaledTime+3;
            if(ExternalInputMode)Debug.LogWarning("[Observed placement] "+observation);
        }
        static Bounds EquipmentBounds(Transform root)
        {
            return AuthoredEquipmentBounds(root);
        }
        void SetCarryCollisions(EquipmentPose item, bool carried)
        {
            if (host?.Actor == null || item.Root == null) return;
            var actorColliders = host.Actor.GetComponents<Collider>();
            foreach (var own in item.Root.GetComponentsInChildren<Collider>(true))
                foreach (var actor in actorColliders) if (own != null && actor != null && own != actor) Physics.IgnoreCollision(own, actor, carried);
        }
        bool PlacementClear(string owner, Vector3 at)
        {
            return !Physics.OverlapBox(at, Vector3.one * .15f, Quaternion.identity, ~(1 << 2), QueryTriggerInteraction.Ignore).Any(c => !WithinOwner(c, owner));
        }
        bool WithinOwner(Collider collider, string owner)
        {
            var part = collider.GetComponentInParent<DetailedPartBinding>(); if (part != null && part.OwnerId == owner) return true;
            return collider.GetComponentInParent<SemanticBinding>()?.EntityId == owner;
        }
        double ObservedClearance(DetailedPartBinding part, double analog, double duration)
        {
            if(poses.TryGetValue(part.OwnerId+"|"+part.PartId,out var pose)&&pose.Explicit!=null&&pose.Definition.Motion!="carry")
                return ExplicitPartClearance(pose,analog,duration);
            if (part.PartId != "leaf") return 1;
            return MeasureClearance(part.OwnerId,analog,duration).Clearance;
        }
        public SourceLeafClearanceResult MeasureClearance(string ownerId,double analog,double duration)
        {
            var boundLeaves=leaves.Values.Where(l=>l.OwnerId==ownerId&&l.Transform!=null).ToArray();
            if(boundLeaves.Length==0)return LastClearance=new SourceLeafClearanceResult{Reason="generated_training_control_no_source_leaf",Method="procedural control; source sweep unassigned"};
            var definition=Mechanics.Parts(ownerId).First(p=>p.Id=="leaf");var first=boundLeaves[0];
            Vector3 delta=first.Transform.parent!=null?first.Transform.parent.TransformVector(first.Open-first.Closed):first.Open-first.Closed;delta*=(float)(analog*definition.Speed*duration);
            foreach(var bound in boundLeaves)
            {
                Vector3 other=bound.Transform.parent!=null?bound.Transform.parent.TransformVector(bound.Open-bound.Closed):bound.Open-bound.Closed;other*=(float)(analog*definition.Speed*duration);
                if((other-delta).sqrMagnitude>1e-8f)return LastClearance=new SourceLeafClearanceResult{Qualified=false,Clearance=0,Reason="nonuniform_owned_leaf_translation_requires_separate_motion_contract"};
            }
            return LastClearance=SourceLeafClearance.Measure(boundLeaves.Select(l=>l.Transform),delta,ownerId,collider=>host.WorldSnapshot?.ResolveSourceObject(collider)??"");
        }
        void ObserveAim(MechanicalInput input)
        {
            if (input.PartId != "lever") return;
            if (!poses.TryGetValue(input.OwnerId + "|nozzle", out var nozzle)) return;
            var originTransform = nozzle.Explicit?.AimOrigin != null ? nozzle.Explicit.AimOrigin : nozzle.Binding.transform;
            Vector3 origin = originTransform.position, direction = originTransform.forward;
            input.OriginX = origin.x; input.OriginY = origin.y; input.OriginZ = origin.z; input.DirectionX = direction.x; input.DirectionY = direction.y; input.DirectionZ = direction.z;
            // A physical outlet may never shoot from the far side of a solid obstruction.
            // Check the camera-to-outlet segment as well as the outgoing ray (which can start inside a collider).
            var tether=origin-host.View.transform.position;
            if(tether.sqrMagnitude>.000001f&&Physics.RaycastAll(host.View.transform.position,tether.normalized,tether.magnitude,~0,QueryTriggerInteraction.Ignore)
                .Any(h=>!WithinOwner(h.collider,input.OwnerId)&&!h.collider.transform.IsChildOf(host.Actor)))return;
            var hit = Physics.RaycastAll(origin, direction, 4, ~(1 << 2), QueryTriggerInteraction.Collide).Where(h => !WithinOwner(h.collider, input.OwnerId)).OrderBy(h => h.distance).FirstOrDefault();
            if (hit.collider == null) return;
            var semantic = hit.collider.GetComponentInParent<SemanticBinding>(); string target = semantic?.EntityId;
            if(target!=null)target=host.ResolveResponseSubject(target);
            if (host.WorldSnapshot != null)
            {
                string sourceId = host.WorldSnapshot.ResolveSourceObject(hit.collider);
                if (!string.IsNullOrEmpty(sourceId)&&host.WorldSnapshot.Registry.Objects.ContainsKey(sourceId))
                {
                    string mapped=host.ResolveResponseSubject(host.WorldSnapshot.Registry.PhysicalIdentityOf(sourceId));
                    if(target==null||mapped!=sourceId)target=mapped;
                }
            }
            input.TargetId = target; input.AimContact = target != null; input.AimX = hit.point.x; input.AimY = hit.point.y; input.AimZ = hit.point.z;
        }
        void FlushHeld()
        {
            if (held == null) return;
            string key = held.OwnerId + "|" + held.PartId;
            if (!accumulated.TryGetValue(key, out var duration) || duration <= 0) return;
            var definition = Mechanics.Parts(held.OwnerId).First(p => p.Id == held.PartId); double analog = effortTime[key] / duration;
            var input = Input(held, auxiliaryAction ?? definition.Motion, analog, Math.Min(Mechanics.World.StepSeconds, duration));
            input.Clearance = ObservedClearance(held, analog, input.Duration); ObserveAim(input);
            if (auxiliaryAction == "check" && Mechanics.Inspect(held.OwnerId).ProfileId == "radio") input.TargetId = Mechanics.World.Entities.Values.FirstOrDefault(e => e.Id != held.OwnerId && (e.Has("radio") || e.Has("communications")))?.Id;
            pending.Enqueue(input); accumulated.Remove(key); effortTime.Remove(key);
        }
        public MechanicalInput[] CollectInputs()
        {
            if (Mechanics == null) return Array.Empty<MechanicalInput>();
            FlushHeld();
            FlushLatched();
            var list = new List<MechanicalInput>();
            while (pending.Count > 0) { var input = pending.Dequeue(); input.Tick = Mechanics.World.Tick; list.Add(input); }
            accumulated.Clear(); effortTime.Clear(); return list.ToArray();
        }
        public JArray Tick(IEnumerable<MechanicalInput> inputs)
        {
            var outcomes = Mechanics.Tick(inputs);
            foreach (JObject result in outcomes)
            {
                Hint = (string)result["OwnerId"] + " / " + (string)result["PartId"] + ": " + (string)result["Reason"];
                ObservePartInputResult(result);
                long id = (long?)result["Sequence"] ?? 0;
                if (placementRotations.TryGetValue(id, out var rotation))
                {
                    if ((bool?)result["Accepted"] == true && equipment.TryGetValue((string)result["OwnerId"], out var item)) item.PlacedRotation = rotation;
                    placementRotations.Remove(id);
                }
            }
            Project(); return outcomes;
        }
        public void Project()
        {
            if (Mechanics == null) return;
            foreach (var pair in bodies)
            {
                var actor = Mechanics.World.Entities["player"];
                bool carried = actor.Get("mech.carry." + pair.Key) > 0;
                if (equipment.TryGetValue(pair.Key, out var item))
                {
                    if (carried && host?.Actor != null && host.View != null)
                    {
                        item.DockSettled=false;
                        if (!item.WasCarried)
                        {
                            item.AnchorRotation = Quaternion.Euler(0, host.Actor.eulerAngles.y, 0); item.LastActorPosition = host.Actor.position;
                            item.CarryStart = item.Root.position; item.CarryStartRotation = item.Root.rotation; item.CarryProgress = 0;
                            SetCarryCollisions(item, true);
                        }
                        var movement = host.Actor.position - item.LastActorPosition; movement.y = 0;
                        if (item.FollowActorHeading||movement.sqrMagnitude > .000001f) item.AnchorRotation = Quaternion.Euler(0, host.Actor.eulerAngles.y, 0);
                        item.LastActorPosition = host.Actor.position;
                        if (item.ProjectionFrame != Time.frameCount && host.Captured) item.CarryProgress = Mathf.Min(1, item.CarryProgress + Mathf.Min(Time.deltaTime, .1f) / .25f);
                        item.ProjectionFrame = Time.frameCount;
                        var at = host.Actor.position + Vector3.up * (host.View.transform.position.y - host.Actor.position.y) + item.AnchorRotation * item.CarryLocal;
                        var turn = item.AnchorRotation * item.CarryRotation;
                        float blend = Mathf.SmoothStep(0, 1, item.CarryProgress);
                        // Lift clear of the original floor holder before pulling and turning.
                        // Keep the same collision sweep at both stages; a blocked pickup never
                        // jumps through the holder, glass or another person.
                        var lift=item.CarryStart+Vector3.up*Mathf.Max(0,at.y-item.CarryStart.y);
                        float pull=Mathf.Clamp01((blend-.5f)*2);
                        var requested=blend<.5f?Vector3.Lerp(item.CarryStart,lift,blend*2):Vector3.Lerp(lift,at,pull);
                        ApplyCarryPose(pair.Key,item,requested,Quaternion.Slerp(item.CarryStartRotation,turn,pull));
                    }
                    else
                    {
                        if (item.WasCarried) SetCarryCollisions(item, item.DockAnchor==host.Actor);
                        if(item.DockAnchor!=null) ProjectDock(pair.Key,item);
                        else
                        {
                            item.Root.rotation = item.PlacedRotation;
                            item.Root.position = OwnerPoint(pair.Key) - item.Root.rotation * Vector3.Scale(item.CenterLocal, item.Root.lossyScale);
                        }
                    }
                    item.WasCarried = carried;
                }
                else if (carried && host?.View != null) { pair.Value.position = host.View.transform.TransformPoint(new Vector3(.3f, -.3f, .6f)); pair.Value.rotation = host.View.transform.rotation; }
                else pair.Value.position = OwnerPoint(pair.Key);
            }
            foreach (var pose in poses.Values)
            {
                double value = Mechanics.Position(pose.Binding.OwnerId, pose.Binding.PartId); var part = pose.Definition; var t = pose.Binding.transform;
                if (pose.Explicit != null)
                {
                    float amount = Mathf.InverseLerp((float)part.Min, (float)part.Max, (float)value);
                    ProjectExplicitPart(pose,amount);
                    continue;
                }
                Vector3 axis = part.Axis == "x" ? Vector3.right : part.Axis == "z" ? Vector3.forward : Vector3.up;
                t.localPosition = pose.Rest + (part.Motion == "press" ? -Vector3.up * .035f * (float)value : part.Motion == "drag" ? Vector3.right * .12f * (float)value : Vector3.zero);
                t.localRotation = pose.Rotation * (part.Motion == "rotate" ? Quaternion.AngleAxis((float)value * (part.Id == "leaf" ? 95 : 75), axis) : Quaternion.identity);
            }
            foreach (var leaf in leaves.Values) if (!leaf.External && leaf.Transform != null) leaf.Transform.localPosition = Vector3.Lerp(leaf.Closed, leaf.Open, (float)Mechanics.Position(leaf.OwnerId, "leaf"));
        }
        void LateUpdate() { if (equipment.Count > 0 && host != null && host.Running && host.Captured) Project(); }
        void OnGUI()
        {
            if (!ShowHints || host == null || !host.Running || Mechanics == null) return;
            var rect = new Rect(Math.Max(8, Screen.width - 410), Math.Max(8, Screen.height - 142), Math.Min(402, Screen.width - 16), 134);
            GUI.Box(rect, ""); GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 5, rect.width - 16, rect.height - 10));
            GUILayout.Label(Hint); GUILayout.Label("LMB grip/release · drag/wheel move · Shift reverse · F carry · G place · Tab tool · hold C check / T tag · V/B connect/disconnect");
            GUILayout.Label("Tool: " + (SelectedToolId ?? "none") + " · simulated normalized controls / INFERRED_TRAINING");
            GUILayout.EndArea();
        }
    }
}
