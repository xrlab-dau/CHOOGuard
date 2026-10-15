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
    public sealed class DetailedManipulationController : MonoBehaviour
    {
        public MechanicalInteractions Mechanics { get; private set; }
        public string Hint { get; private set; } = "Aim at a part; hold left mouse to grip; drag or wheel to move; release to let go.";
        public string SelectedToolId { get; private set; }
        public string InspectedObjectId { get; private set; }
        public SourceLeafClearanceResult LastClearance {get;private set;}
        public bool ShowHints = true;
        PlatformSeededSimulator simulator;
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
        DetailedPartBinding held, aimed;
        RaycastHit aimedHit;
        string auxiliaryAction;
        sealed class PartPose { public DetailedPartBinding Binding; public Vector3 Rest; public Quaternion Rotation; public MechanicalPart Definition; }
        sealed class LeafPose { public string OwnerId; public Transform Transform; public Vector3 Closed, Open; public bool External; }

        public void Initialize(PlatformSeededSimulator sim)
        {
            if (sim == null || sim.State == null) throw new ArgumentException("A running local world is required");
            Clear(); simulator = sim;
            Mechanics = new MechanicalInteractions(sim.State, Resources.Load<TextAsset>("MechanicalProfiles")?.text);
            Mechanics.InitializeDefaults(sim.WorldSnapshot?.Registry);
            generated = new GameObject("Inferred granular training controls").transform; generated.SetParent(sim.transform, false);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { name = "INFERRED_TRAINING control material" }; material.color = new Color(.8f, .31f, .06f);
            sequence = (long)sim.State.Entities["player"].Get("mech.last-sequence");
            foreach (var ownerId in Mechanics.Owners) BuildControls(ownerId);
            BuildTrainingTools(); Project();
        }
        void Clear()
        {
            if (generated != null) Destroy(generated.gameObject); if (material != null) Destroy(material);
            pending.Clear(); poses.Clear(); leaves.Clear(); bodies.Clear(); accumulated.Clear(); effortTime.Clear(); tools.Clear(); held = aimed = null; Mechanics = null; SelectedToolId = null;
        }
        void OnDestroy() { Clear(); }
        public void ResetInputState() { pending.Clear(); accumulated.Clear(); effortTime.Clear(); held = aimed = null; auxiliaryAction = null; }
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
            foreach (var existing in simulator.GetComponentsInChildren<SemanticBinding>(true))
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
            var player = simulator.State.Entities["player"]; int index = 0;
            foreach (var kind in new[] { "key", "wrench", "voltage-probe", "lockout-tag" })
            {
                string id = "training-tool:" + kind;
                if (!simulator.State.Entities.ContainsKey(id))
                {
                    var tool = new Entity { Id = id, Kind = kind, X = player.X + .55 + .18 * index, Y = player.Y + .3, Z = player.Z + .4 };
                    tool.Tags.Add("training-model"); tool.Tags.Add("tool:" + kind);
                    if (kind == "key") foreach (var owner in Mechanics.Owners.Where(id => Mechanics.Inspect(id).ProfileId == "breaker-cabinet" && !simulator.State.Entities[id].Has("source-object"))) tool.Set("mech.key-owner." + owner, 1);
                    tool.Set("mech.resource", kind == "lockout-tag" ? 4 : 1); simulator.State.Add(tool);
                }
                Mechanics.Attach(id, "small-tool"); BuildControls(id); tools.Add(id); index++;
            }
        }
        MechanicalInput Input(DetailedPartBinding part, string action, double analog = 0, double duration = 0)
        {
            return new MechanicalInput { Tick = Mechanics.World.Tick, Sequence = ++sequence, ActorId = "player", OwnerId = part.OwnerId, PartId = part.PartId, Action = action, ToolId = SelectedToolId, Analog = analog, Duration = duration, Contact = held == part || aimed != null && aimed.OwnerId == part.OwnerId && aimed.PartId == part.PartId, Clearance = 1 };
        }
        void Update()
        {
            if (simulator == null || !simulator.Running || simulator.IsReplaying || Mechanics == null || simulator.State != Mechanics.World || simulator.Player == null) return;
            var mouse = Mouse.current; var keyboard = Keyboard.current; if (mouse == null || keyboard == null) return;
            if (!simulator.Player.Captured) { if (held != null) { FlushHeld(); pending.Enqueue(Input(held, "release")); } held = null; return; }
            aimed = null; InspectedObjectId = null;
            var ray = simulator.Player.View.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            if (Physics.Raycast(ray, out aimedHit, 2.4f, ~(1 << 2), QueryTriggerInteraction.Collide))
            {
                aimed = aimedHit.collider.GetComponent<DetailedPartBinding>();
                if (aimed != null) { InspectedObjectId = aimed.OwnerId; Hint = aimed.OwnerId + " / " + aimed.PartId + " · " + Mechanics.Inspect(aimed.OwnerId).Claim; }
                else if (simulator.WorldSnapshot != null)
                {
                    InspectedObjectId = simulator.WorldSnapshot.ResolveSourceObject(aimedHit.collider);
                    if (!string.IsNullOrEmpty(InspectedObjectId)) Hint = "Inspect " + InspectedObjectId + " · saved source; mechanism unassigned";
                }
            }
            if (keyboard.tabKey.wasPressedThisFrame && tools.Count > 0)
            {
                int current = tools.IndexOf(SelectedToolId); SelectedToolId = tools[(current + 1) % tools.Count]; Hint = "Selected " + SelectedToolId + "; pick up its body before use";
            }
            if (mouse.leftButton.wasPressedThisFrame && aimed != null) { held = aimed; pending.Enqueue(Input(held, "grip")); }
            if (mouse.leftButton.wasReleasedThisFrame && held != null) { FlushHeld(); pending.Enqueue(Input(held, "release")); held = null; }
            if (held == null) return;
            string key = held.OwnerId + "|" + held.PartId;
            var definition = Mechanics.Parts(held.OwnerId).First(p => p.Id == held.PartId);
            if (keyboard.fKey.wasPressedThisFrame && definition.Carryable) pending.Enqueue(Input(held, "carry"));
            if (keyboard.gKey.wasPressedThisFrame && definition.Carryable)
            {
                Vector3 destination = aimedHit.collider != null ? aimedHit.point + aimedHit.normal * .15f : simulator.Player.transform.position + simulator.Player.transform.forward * .7f;
                var input = Input(held, "place"); input.X = destination.x; input.Y = destination.y; input.Z = destination.z; input.Clearance = PlacementClear(held.OwnerId, destination) ? 1 : 0; pending.Enqueue(input);
            }
            if (keyboard.vKey.wasPressedThisFrame || keyboard.bKey.wasPressedThisFrame)
            {
                FlushHeld(); var input = Input(held, keyboard.vKey.wasPressedThisFrame ? "connect" : "disconnect");
                input.TargetId = aimedHit.collider != null ? aimedHit.collider.GetComponentInParent<SemanticBinding>()?.EntityId : null;
                input.Clearance = aimedHit.collider != null ? 1 : 0; pending.Enqueue(input);
            }
            string action = keyboard.cKey.isPressed ? "check" : keyboard.tKey.isPressed ? "tag" : null;
            if (action != auxiliaryAction) { FlushHeld(); auxiliaryAction = action; }
            double analog = definition.Motion == "press" ? 1 : Math.Max(-1, Math.Min(1, mouse.delta.ReadValue().x * .04 + mouse.scroll.ReadValue().y * .02));
            if (keyboard.leftShiftKey.isPressed) analog = -Math.Abs(analog == 0 ? 1 : analog);
            double dt = Math.Min(Time.deltaTime, .1f);
            if (definition.Motion != "carry" && (Math.Abs(analog) > 0 || auxiliaryAction != null))
            {
                accumulated[key] = accumulated.TryGetValue(key, out var previous) ? previous + dt : dt;
                effortTime[key] = effortTime.TryGetValue(key, out var previousEffort) ? previousEffort + analog * dt : analog * dt;
            }
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
            return LastClearance=SourceLeafClearance.Measure(boundLeaves.Select(l=>l.Transform),delta,ownerId,collider=>simulator.WorldSnapshot?.ResolveSourceObject(collider)??"");
        }
        void ObserveAim(MechanicalInput input)
        {
            if (input.PartId != "lever") return;
            if (!poses.TryGetValue(input.OwnerId + "|nozzle", out var nozzle)) return;
            Vector3 origin = nozzle.Binding.transform.position, direction = nozzle.Binding.transform.forward;
            input.OriginX = origin.x; input.OriginY = origin.y; input.OriginZ = origin.z; input.DirectionX = direction.x; input.DirectionY = direction.y; input.DirectionZ = direction.z;
            var hit = Physics.RaycastAll(origin, direction, 4, ~(1 << 2), QueryTriggerInteraction.Collide).Where(h => !WithinOwner(h.collider, input.OwnerId)).OrderBy(h => h.distance).FirstOrDefault();
            if (hit.collider == null) return;
            var semantic = hit.collider.GetComponentInParent<SemanticBinding>(); string target = semantic?.EntityId;
            if(target!=null)target=simulator.ResolveResponseSubject(target);
            if (simulator.WorldSnapshot != null)
            {
                string sourceId = simulator.WorldSnapshot.ResolveSourceObject(hit.collider);
                if (!string.IsNullOrEmpty(sourceId)&&simulator.WorldSnapshot.Registry.Objects.ContainsKey(sourceId))
                {
                    string mapped=simulator.ResolveResponseSubject(simulator.WorldSnapshot.Registry.PhysicalIdentityOf(sourceId));
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
            var list = new List<MechanicalInput>();
            while (pending.Count > 0) { var input = pending.Dequeue(); input.Tick = Mechanics.World.Tick; list.Add(input); }
            accumulated.Clear(); effortTime.Clear(); return list.ToArray();
        }
        public JArray Tick(IEnumerable<MechanicalInput> inputs)
        {
            var outcomes = Mechanics.Tick(inputs);
            foreach (JObject result in outcomes) Hint = (string)result["OwnerId"] + " / " + (string)result["PartId"] + ": " + (string)result["Reason"];
            Project(); return outcomes;
        }
        public void Project()
        {
            if (Mechanics == null) return;
            foreach (var pair in bodies)
            {
                var actor = Mechanics.World.Entities["player"];
                if (actor.Get("mech.carry." + pair.Key) > 0) { pair.Value.position = simulator.Player.View.transform.TransformPoint(new Vector3(.3f, -.3f, .6f)); pair.Value.rotation = simulator.Player.View.transform.rotation; }
                else pair.Value.position = OwnerPoint(pair.Key);
            }
            foreach (var pose in poses.Values)
            {
                double value = Mechanics.Position(pose.Binding.OwnerId, pose.Binding.PartId); var part = pose.Definition; var t = pose.Binding.transform;
                Vector3 axis = part.Axis == "x" ? Vector3.right : part.Axis == "z" ? Vector3.forward : Vector3.up;
                t.localPosition = pose.Rest + (part.Motion == "press" ? -Vector3.up * .035f * (float)value : part.Motion == "drag" ? Vector3.right * .12f * (float)value : Vector3.zero);
                t.localRotation = pose.Rotation * (part.Motion == "rotate" ? Quaternion.AngleAxis((float)value * (part.Id == "leaf" ? 95 : 75), axis) : Quaternion.identity);
            }
            foreach (var leaf in leaves.Values) if (!leaf.External && leaf.Transform != null) leaf.Transform.localPosition = Vector3.Lerp(leaf.Closed, leaf.Open, (float)Mechanics.Position(leaf.OwnerId, "leaf"));
        }
        void OnGUI()
        {
            if (!ShowHints || simulator == null || !simulator.Running || Mechanics == null) return;
            var rect = new Rect(Math.Max(8, Screen.width - 410), Math.Max(8, Screen.height - 142), Math.Min(402, Screen.width - 16), 134);
            GUI.Box(rect, ""); GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 5, rect.width - 16, rect.height - 10));
            GUILayout.Label(Hint); GUILayout.Label("LMB grip/release · drag/wheel move · Shift reverse · F carry · G place · Tab tool · hold C check / T tag · V/B connect/disconnect");
            GUILayout.Label("Tool: " + (SelectedToolId ?? "none") + " · simulated normalized controls / INFERRED_TRAINING");
            GUILayout.EndArea();
        }
    }
}
