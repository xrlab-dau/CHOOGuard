using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Seeded.Unity
{
    /// <summary>Local virtual communications and recorded physics observations; never external messages.</summary>
    public sealed class ResponseInteractionController : MonoBehaviour
    {
        public ResponseWorkflows Engine { get; private set; }
        public string SelectedActorId = "player", SubjectId, ObservationId, RecipientId, ChannelId = "face-to-face", RouteId, PatientId, ResourceId, MessageId, ExecutorId;
        public string Feedback = "Observe a nearby condition, then report it through an available local channel.";
        public bool ShowPanel = true;
        PlatformSeededSimulator simulator;
        readonly Queue<ResponseInput> pending = new Queue<ResponseInput>();
        readonly Dictionary<string, long> sequences = new Dictionary<string, long>(StringComparer.Ordinal);
        ResponseInput heldAction;
        double heldSeconds;
        int heldFrame = -10;
        Vector2 scroll;
        static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            ["observe"]="Observe nearby condition", ["report"]="Send local report", ["request:isolation"]="Request electrical isolation", ["request:evacuation"]="Request evacuation support", ["request:medical"]="Request medical responder", ["request:specialist"]="Request specialist", ["request:accessible-assistance"]="Request accessible assistance", ["acknowledge"]="Acknowledge receipt", ["readback"]="Read back message code", ["authorize"]="Send authorization to executor", ["protect"]="Hold: warn and keep clear", ["assess-person"]="Hold: assess person", ["support-person"]="Hold: support consenting person", ["control-access"]="Hold: check placed access barrier", ["verify-isolation"]="Hold: verify checked and tagged isolation", ["evacuate"]="Hold: begin own evacuation", ["guide-evacuation"]="Hold: begin authorized escort", ["assist-evacuation"]="Hold: begin accessible escort", ["handover"]="Hold: hand over local evidence"
        };
        public void Initialize(PlatformSeededSimulator sim, ResponseRole playerRole, bool addTrainingTeam)
        {
            simulator = sim ?? throw new ArgumentNullException(nameof(sim));
            if (sim.State == null) throw new ArgumentException("Existing world required");
            ResetInputState(); sequences.Clear();
            if (addTrainingTeam) AddTeam(sim);
            AddPlayerRadio(sim);
            Engine = new ResponseWorkflows(sim.State, Resources.Load<TextAsset>("ResponseProfiles")?.text);
            bool first=!sim.State.Fields.ContainsKey("response.adapters.initialized");
            if(first)Engine.InitializeDefaultRoles();
            if (Engine.Role("player") != playerRole) Engine.SetRole("player", playerRole);
            foreach (var e in sim.State.Entities.Values.Where(e => e.Get("response.actor") == 1).ToArray())
            {
                e.Set("response.workflow-bound", 1);
                // Actor radio capability is a receiver. A transmitter is separate detailed equipment.
                e.Set("response.channel.radio", 0);
                sequences[e.Id] = (long)e.Get("response.last-sequence");
            }
            sim.State.Fields["response.adapters.initialized"]=1;
            if (!sim.State.Entities.ContainsKey(SelectedActorId)) SelectedActorId = "player";
            if (RecipientId == null || !sim.State.Entities.ContainsKey(RecipientId)) RecipientId = sim.State.Entities.Values.FirstOrDefault(e => e.Id != SelectedActorId && Engine.Role(e.Id) == ResponseRole.Control)?.Id;
            if (RouteId == null || !sim.State.Entities.ContainsKey(RouteId)) RouteId = sim.State.Entities.Values.Where(e => e.Has("exit") || e.Has("safe-zone") || e.Has("shelter")).OrderBy(e => e.Id, StringComparer.Ordinal).FirstOrDefault()?.Id;
            if (ChannelId != "face-to-face" && !sim.State.Entities.ContainsKey(ChannelId)) ChannelId = "face-to-face";
        }
        static void AddPlayerRadio(PlatformSeededSimulator sim)
        {
            const string id = "training-radio:player"; if (sim.State.Entities.ContainsKey(id)) return;
            var player = sim.State.Entities["player"];
            var radio = new Entity { Id = id, ParentId = player.Id, Kind = "radio", X = player.X + .4, Y = player.Y + .7, Z = player.Z + .4 };
            foreach (string tag in new[] { "radio", "communication-equipment", "training-model", "INFERRED_TRAINING", "battery", "self-powered" }) radio.Tags.Add(tag);
            radio.Set("power", 1); radio.Set("supply.available", 1); radio.Set("charge", 1); radio.Set("integrity", 1); sim.State.Add(radio);
        }
        static void AddTeam(PlatformSeededSimulator sim)
        {
            var player = sim.State.Entities["player"]; int index = 0;
            foreach (ResponseRole role in Enum.GetValues(typeof(ResponseRole)))
            {
                if (role == ResponseRole.Passenger) continue;
                string id = "training-team:" + role; if (sim.State.Entities.ContainsKey(id)) { index++; continue; }
                Vector3 desired = new Vector3((float)player.X + (index % 3 - 1) * 1.2f, (float)player.Y, (float)player.Z + 1.7f + (index / 3) * 1.2f);
                if (!sim.Navigation.TrySpawn(desired, out var feet) || sim.State.Entities.Values.Where(e => e.Has("local-training-team")).Any(e => Vector3.Distance(feet, new Vector3((float)e.X, (float)e.Y, (float)e.Z)) < .7f)) { index++; continue; }
                var member = new Entity { Id = id, Kind = "response-team", X = feet.x, Y = feet.y, Z = feet.z };
                foreach (string tag in new[] { "npc", "staff", "response-actor", "radio", "local-training-team", "training-model", "INFERRED_TRAINING" }) member.Tags.Add(tag);
                member.Set("health", 1); member.Set("mobility", 1); member.Set("response.actor", 1); member.Set("response.role", (int)role); member.Set("response.inferred-training", 1); member.Set("response.radio-equipped", 1); member.Set("navigation.qualified", 1); sim.State.Add(member); index++;
            }
        }
        public void ResetInputState() { pending.Clear(); heldAction = null; heldSeconds = 0; heldFrame = -10; }
        public void Queue(ResponseInput input)
        {
            if (input == null || Engine == null) throw new ArgumentException("Initialized response input required");
            pending.Enqueue(JsonConvert.DeserializeObject<ResponseInput>(JsonConvert.SerializeObject(input)));
            if (!string.IsNullOrEmpty(input.ActorId)) sequences[input.ActorId] = Math.Max(sequences.TryGetValue(input.ActorId, out var last) ? last : 0, input.Sequence);
        }
        ResponseInput Create(string action)
        {
            string request = action.StartsWith("request:", StringComparison.Ordinal) ? action.Substring(8) : null;
            return new ResponseInput { Tick = Engine.World.Tick, ActorId = SelectedActorId, Action = request == null ? action : "request", RequestKind = request, SubjectId = SubjectId, ObservationId = ObservationId, RecipientId = RecipientId, ChannelId = ChannelId, MessageId = MessageId, RouteId = RouteId, PatientId = PatientId ?? SubjectId, ResourceId = ResourceId, ExecutorId = ExecutorId };
        }
        void EnqueueNew(ResponseInput input)
        {
            input.Sequence = (sequences.TryGetValue(input.ActorId, out var n) ? n : 0) + 1; Queue(input);
        }
        void Update()
        {
            if (simulator == null || !simulator.Running || Engine == null || simulator.State != Engine.World || simulator.IsReplaying) return;
            if (heldAction != null && heldFrame >= Time.frameCount - 1) heldSeconds += Math.Min(Time.deltaTime, .1f);
        }
        public ResponseInput[] CollectInputs()
        {
            if (heldAction != null && heldSeconds > 0)
            { var i = JsonConvert.DeserializeObject<ResponseInput>(JsonConvert.SerializeObject(heldAction)); i.Duration = Math.Min(Engine.World.StepSeconds, heldSeconds); EnqueueNew(i); }
            heldSeconds = 0;
            var result = new List<ResponseInput>(); while (pending.Count > 0) { var i = pending.Dequeue(); i.Tick = Engine.World.Tick; result.Add(i); }
            return result.OrderBy(i => i.Sequence).ThenBy(i => i.ActorId, StringComparer.Ordinal).ToArray();
        }
        static Vector3 Center(Entity e) => new Vector3((float)e.Get("mech.center.x", e.X), (float)e.Get("mech.center.y", e.Y), (float)e.Get("mech.center.z", e.Z));
        bool Belongs(Collider collider, string id)
        {
            var semantic = collider.GetComponentInParent<SemanticBinding>(); if (semantic?.EntityId == id) return true;
            if (simulator.WorldSnapshot == null) return false;
            string source = simulator.WorldSnapshot.ResolveSourceObject(collider);
            if(!string.IsNullOrEmpty(source)&&simulator.WorldSnapshot.Registry.Objects.ContainsKey(source))
            {
                if(simulator.ResolveResponseSubject(source)==id)return true;
                if(source==id||simulator.WorldSnapshot.Registry.PhysicalIdentityOf(source)==id)return true;
            }
            return simulator.WorldSnapshot.Objects.TryGetValue(id, out var parent) && collider.transform.IsChildOf(parent);
        }
        JObject Sight(Entity actor, Entity subject)
        {
            Vector3 from = new Vector3((float)actor.X, (float)actor.Y + 1.5f, (float)actor.Z), to = Center(subject);
            if (subject.Has("npc")) to += Vector3.up;
            Vector3 delta = to - from; bool sourceObject=subject.Kind=="source-object"||subject.Has("source-input");bool known = !sourceObject || subject.Get("response.bounds-known") == 1&&subject.Get("source.active-hierarchy")==1;
            bool blocked = !known || delta.magnitude > 12; string blocker = null;
            if (!blocked && delta.sqrMagnitude > .0001f)
            {
                var hits = Physics.RaycastAll(from, delta.normalized, delta.magnitude, ~(1 << 2), QueryTriggerInteraction.Collide).Where(h => !Belongs(h.collider, actor.Id) && h.collider.GetComponentInParent<DetailedPartBinding>() == null).OrderBy(h => h.distance);
                foreach (var hit in hits)
                {
                    if (Belongs(hit.collider, subject.Id)) break;
                    if (hit.collider.isTrigger && hit.collider.GetComponentInParent<SeededNpcController>() == null) continue;
                    blocked = true; blocker = simulator.WorldSnapshot?.ResolveSourceObject(hit.collider);if(string.IsNullOrEmpty(blocker))blocker=hit.collider.name;break;
                }
            }
            return new JObject { ["kind"] = "los", ["actor"] = actor.Id, ["subject"] = subject.Id, ["occluded"] = blocked ? 1 : 0, ["bounds_known"] = known, ["blocker"] = blocker, ["from"] = PlatformSeededSimulator.Point(from), ["to"] = PlatformSeededSimulator.Point(to) };
        }
        JObject Route(Entity patient, Entity destination)
        {
            Vector3 start = new Vector3((float)patient.X, (float)patient.Y, (float)patient.Z), end = new Vector3((float)destination.Get("destination.x", destination.X), (float)destination.Get("destination.y", destination.Y), (float)destination.Get("destination.z", destination.Z));
            var path = new NavMeshPath(); bool complete = NavMesh.SamplePosition(start, out var a, .35f, NavMesh.AllAreas) && NavMesh.SamplePosition(end, out var b, .6f, NavMesh.AllAreas) && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            string reason = complete ? "native_path_and_capsule_sweep" : "native_path_incomplete"; double risk = 0;
            if (complete && path.corners.Length > 65) { complete = false; reason = "bounded_route_segment_limit"; }
            if (complete)
                for (int n = 1; n < path.corners.Length; n++)
                {
                    Vector3 from = path.corners[n - 1], delta = path.corners[n] - from;
                    var hits = Physics.CapsuleCastAll(from + Vector3.up * .3f, from + Vector3.up * 1.42f, .28f, delta.normalized, delta.magnitude, ~(1 << 2), QueryTriggerInteraction.Collide).Where(h => !Belongs(h.collider, patient.Id) && h.collider.GetComponentInParent<DetailedPartBinding>() == null && (!h.collider.isTrigger || h.collider.GetComponentInParent<SeededNpcController>() != null)).ToArray();
                    if (hits.Length > 0) { complete = false; reason = "capsule_blocked:" + hits[0].collider.name; break; }
                    foreach (var hazard in CausalKernel.SemanticEntities(Engine.World).Where(e => e.Get("fire") > .1 || e.Get("smoke") > .3))
                    {
                        var point=Center(hazard);var endPoint=path.corners[n];
                        if(MechanicalContactGeometry.PointSegmentDistance(point.x,point.y,point.z,from.x,from.y,from.z,endPoint.x,endPoint.y,endPoint.z)<=3)risk=Math.Max(risk,Math.Max(hazard.Get("fire"),hazard.Get("smoke")));
                    }
                }
            return new JObject { ["kind"] = "route", ["patient"] = patient.Id, ["destination"] = destination.Id, ["complete"] = complete ? 1 : 0, ["verified_tick"] = Engine.World.Tick, ["risk"] = LocalTapeCodec.Float(risk), ["accessible"] = complete && destination.Has("qualified-accessible-route") ? 1 : 0, ["reason"] = reason, ["corners"] = new JArray(path.corners.Select(PlatformSeededSimulator.Point)), ["claim"] = "recorded native path and capsule sweep; actual walking acceptance is separate" };
        }
        public JArray CaptureObservations(IEnumerable<ResponseInput> inputs)
        {
            var commands = (inputs ?? Array.Empty<ResponseInput>()).ToList(); var observations = new JArray();
            foreach(var progress in Engine.World.Entities.Values.Where(e=>e.Kind=="response-progress"&&e.Get("in-progress")==1&&e.Get("complete")==0).Take(32))
            {
                Func<string,string> tag=key=>progress.Tags.FirstOrDefault(t=>t.StartsWith(key+":",StringComparison.Ordinal))?.Substring(key.Length+1);
                commands.Add(new ResponseInput{ActorId=tag("actor"),SubjectId=progress.ParentId,PatientId=tag("patient"),RouteId=tag("route"),Action="guide-evacuation"});
            }
            foreach(var patient in Engine.World.Entities.Values.Where(e=>e.Get("response.support-active")==1).Take(32))
                foreach(var supporter in patient.State.Where(s=>s.Key.StartsWith("response.supporter.",StringComparison.Ordinal)&&s.Value==1))
                    commands.Add(new ResponseInput{ActorId=supporter.Key.Substring("response.supporter.".Length),SubjectId=patient.Id});
            foreach(var message in Engine.World.Entities.Values.Where(e=>e.Kind=="response-message"&&e.Get("delivered")==0&&e.Get("lost")==0).Take(32))
            {
                string sender=message.Tags.FirstOrDefault(t=>t.StartsWith("actor:",StringComparison.Ordinal))?.Substring(6),receiver=message.Tags.FirstOrDefault(t=>t.StartsWith("recipient:",StringComparison.Ordinal))?.Substring(10);
                commands.Add(new ResponseInput{ActorId=sender,SubjectId=message.ParentId,RecipientId=receiver});
            }
            foreach (var group in commands.GroupBy(i => i.ActorId, StringComparer.Ordinal))
            {
                if (!Engine.World.Entities.TryGetValue(group.Key, out var actor)) continue;
                var wanted = new HashSet<string>(group.SelectMany(i => new[] { i.SubjectId, i.RecipientId, i.PatientId }).Where(id => id != null), StringComparer.Ordinal);
                foreach (var e in Engine.World.Entities.Values.Where(e => e.Id != actor.Id && (e.Has("npc") || e.Has("extinguisher") || e.Has("barrier") || e.Has("first-aid") || e.Has("accessible-transport")) && Vector3.Distance(Center(e), Center(actor)) <= 8).Take(48)) wanted.Add(e.Id);
                foreach (string id in wanted.OrderBy(id => id, StringComparer.Ordinal).Take(64)) if (Engine.World.Entities.TryGetValue(id, out var subject)) observations.Add(Sight(actor, subject));
                foreach (var input in group.Where(i => i.RouteId != null))
                {
                    string patientId = input.Action == "evacuate" ? input.ActorId : input.PatientId;
                    if (patientId != null && Engine.World.Entities.TryGetValue(patientId, out var patient) && Engine.World.Entities.TryGetValue(input.RouteId, out var route)) observations.Add(Route(patient, route));
                }
            }
            return observations;
        }
        public void ApplyObservations(JArray observations)
        {
            foreach (JObject row in observations ?? new JArray())
            {
                if ((string)row["kind"] == "los" && Engine.World.Entities.TryGetValue((string)row["actor"], out var actor)) actor.Set("response.occluded." + (string)row["subject"], (double)row["occluded"]);
                if ((string)row["kind"] == "route" && Engine.World.Entities.TryGetValue((string)row["patient"], out var patient))
                {
                    string prefix = "response.route." + (string)row["destination"] + ".";
                    patient.Set(prefix + "complete", (double)row["complete"]); patient.Set(prefix + "verified.tick", (double)row["verified_tick"]); patient.Set(prefix + "risk", (double)row["risk"]); patient.Set(prefix + "accessible", (double)row["accessible"]);
                }
            }
        }
        public JArray Tick(IEnumerable<ResponseInput> inputs)
        {
            var queue = (inputs ?? Array.Empty<ResponseInput>()).ToArray();
            if (queue.Any(i => i == null || i.Tick != Engine.World.Tick) || queue.GroupBy(i => i.ActorId + "|" + i.Sequence).Any(g => g.Count() > 1)) throw new ArgumentException("Response batch tick/sequence mismatch");
            var outcomes = new JArray();
            foreach (var i in queue.OrderBy(i => i.Sequence).ThenBy(i => i.ActorId, StringComparer.Ordinal))
            {
                var outcome = Engine.Apply(i); outcomes.Add(outcome.Json()); Feedback = outcome.Reason;
                if (outcome.Accepted && i.ActorId == SelectedActorId) { if (i.Action == "observe") ObservationId = outcome.RecordId; if (i.Action == "request" || i.Action == "report" || i.Action == "authorize") MessageId = outcome.RecordId; }
            }
            Engine.Step(); return outcomes;
        }
        string Name(string id)
        {
            if (id == null) return "none"; if (id == "player") return "You";
            if (Engine.World.Entities.TryGetValue(id, out var e) && Engine.Role(id).HasValue) return Engine.Role(id).Value + (e.Has("local-training-team") ? " (local training participant)" : " · " + id);
            if (simulator.WorldSnapshot!=null&&simulator.WorldSnapshot.Registry.Objects.TryGetValue(id, out var source)) return source.Name;
            return id.StartsWith("training-", StringComparison.Ordinal) ? id.Replace("training-", "Training ") : id;
        }
        string Choose(string title, string value, IEnumerable<string> options)
        {
            var values = options.Distinct(StringComparer.Ordinal).ToArray(); GUILayout.BeginHorizontal(); GUILayout.Label(title + ": " + Name(value));
            if (values.Length > 0 && GUILayout.Button("Next", GUILayout.Width(50))) value = values[(Array.IndexOf(values, value) + 1) % values.Length]; GUILayout.EndHorizontal(); return value;
        }
        void OnGUI()
        {
            if (!ShowPanel || simulator == null || !simulator.Running || simulator.IsReplaying || Engine == null || Engine.World != simulator.State) return;
            GUILayout.BeginArea(new Rect(Math.Max(490, Screen.width - 440), 16, Math.Min(424, Screen.width - 500), Math.Max(100, Screen.height - 180)), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll); GUILayout.Label("Local response · INFERRED_TRAINING"); GUILayout.Label("Procedures, timings and messages are simulated; no external report is sent.");
            SelectedActorId = Choose("Acting participant", SelectedActorId, Engine.World.Entities.Values.Where(e => Engine.Role(e.Id).HasValue && (e.Id == "player" || e.Has("local-training-team"))).Select(e => e.Id));
            if (GUILayout.Button("Use the object under the pointer")) { SubjectId = simulator.ResolveResponseSubject(simulator.CurrentTarget); ObservationId = null; }
            GUILayout.Label("Condition: " + Name(SubjectId)); RecipientId = Choose("Recipient", RecipientId, Engine.World.Entities.Values.Where(e => e.Id != SelectedActorId && Engine.Role(e.Id).HasValue).Select(e => e.Id));
            ChannelId = Choose("Channel", ChannelId, new[] { "face-to-face" }.Concat(Engine.World.Entities.Values.Where(e => new[] { "radio", "intercom", "pa" }.Any(c => e.Get("response.channel." + c) == 1)).Select(e => e.Id)));
            ExecutorId = Choose("Executor", ExecutorId, Engine.World.Entities.Values.Where(e => Engine.Role(e.Id).HasValue && Engine.Role(e.Id) != ResponseRole.Passenger).Select(e => e.Id));
            RouteId = Choose("Destination", RouteId, Engine.World.Entities.Values.Where(e => e.Has("exit") || e.Has("safe-zone") || e.Has("shelter")).Select(e => e.Id));
            ResourceId = Choose("Resource", ResourceId, Engine.World.Entities.Values.Where(e => e.Has("barrier") || e.Has("accessible-transport") || e.Has("first-aid")).Select(e => e.Id));
            foreach (string action in new[] { "observe", "report", "request:isolation", "request:evacuation", "request:medical", "request:specialist", "request:accessible-assistance" }) if (GUILayout.Button(Labels[action])) EnqueueNew(Create(action));
            bool holding = false;
            foreach (string action in new[] { "protect", "assess-person", "support-person", "control-access", "verify-isolation", "evacuate", "guide-evacuation", "assist-evacuation", "handover" })
                if (GUILayout.RepeatButton(Labels[action])) { if (heldAction == null || heldAction.Action != action) heldAction = Create(action); heldFrame = Time.frameCount; holding = true; }
            if (!holding && Event.current.type == EventType.Repaint && heldFrame < Time.frameCount) heldAction = null;
            GUILayout.Label("Pending tasks / recorded progress");
            foreach (var task in Engine.Tasks(SelectedActorId).Take(16))
            {
                GUILayout.Label((Labels.TryGetValue(task.Action, out var label) ? label : task.Action) + " · " + task.Status + " " + (task.Progress * 100).ToString("F0") + "%");
                if ((task.Action == "acknowledge" || task.Action == "readback" || task.Action == "authorize") && GUILayout.Button(Labels[task.Action]))
                {
                    var input = Create(task.Action); input.SubjectId = task.SubjectId; input.ObservationId = task.ObservationId; input.MessageId = task.MessageId;
                    if (task.Action == "readback") input.Readback = Engine.Inspect(task.MessageId).ReadbackCode; EnqueueNew(input);
                }
                if (GUILayout.Button("Use this task's evidence")) { SubjectId = task.SubjectId; ObservationId = task.ObservationId; MessageId = task.MessageId ?? MessageId; }
            }
            if (MessageId != null && Engine.RecordIds.Contains(MessageId)) { var message = Engine.Inspect(MessageId); GUILayout.Label("Message code " + message.ReadbackCode + " · delivered " + (message.State.TryGetValue("delivered", out var delivered) ? delivered : 0)); }
            GUILayout.Label(Feedback); GUILayout.EndScrollView(); GUILayout.EndArea();
        }
    }
}
