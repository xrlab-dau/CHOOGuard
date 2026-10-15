using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    public static class MechanicalContactGeometry
    {
        public static double PointSegmentDistance(double px,double py,double pz,double ax,double ay,double az,double bx,double by,double bz)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,length=dx*dx+dy*dy+dz*dz;
            double t=length<=1e-20?0:Math.Max(0,Math.Min(1,((px-ax)*dx+(py-ay)*dy+(pz-az)*dz)/length));
            double x=px-(ax+t*dx),y=py-(ay+t*dy),z=pz-(az+t*dz);return Math.Sqrt(x*x+y*y+z*z);
        }
    }
    /// <summary>Recorded local control input. Contact/clearance are observations, not inferred mesh proof.</summary>
    public sealed class MechanicalInput
    {
        public long Tick, Sequence;
        public string ActorId, OwnerId, PartId, Action, ToolId, TargetId;
        public double Analog, Duration, Clearance = 1, X, Y, Z;
        public double AimX, AimY, AimZ, OriginX, OriginY, OriginZ, DirectionX, DirectionY, DirectionZ;
        public bool Contact, AimContact;
        public MechanicalInput Copy() => (MechanicalInput)MemberwiseClone();
    }
    public sealed class MechanicalOutcome
    {
        public long Tick, Sequence;
        public string ActorId, OwnerId, PartId, Action, Reason, Claim = MechanicalInteractions.Claim;
        public bool Accepted;
        public double Before, After, ResourceConsumed, EffectAmount;
        public string EffectTarget;
        public JObject Json() => (JObject)LocalTapeCodec.FromObject(this);
    }
    public sealed class MechanicalPart
    {
        public string Id, Motion, Axis = "y", ToolKind;
        public double Min, Max = 1, Speed = 1, Friction = .08, Spring, Rest, Initial;
        public bool Carryable, OneWay;
        public string StateKey => "mech.part." + Id;
    }
    public sealed class MechanicalProfile
    {
        public string Id, Claim = MechanicalInteractions.Claim, Provenance = "inferred generic training mechanism; no confidential manual used";
        public double Confidence = .35;
        public List<MechanicalPart> Parts = new List<MechanicalPart>();
    }
    public sealed class MechanicalInspection
    {
        public string OwnerId, ProfileId, Claim, Provenance;
        public double Confidence;
        public bool MutableParts;
        public JObject State;
        public MechanicalPart[] Parts;
    }

    /// <summary>
    /// Portable deterministic part mechanics. All mutable control/ownership/resource state belongs to World,
    /// and therefore its digest and replay. Stable variation changes resistance, never creates an incident.
    /// No source asset, material, geometry, kernel incident, or external model is changed by this class.
    /// </summary>
    public sealed class MechanicalInteractions
    {
        public const string Version = "chooguard.mechanical.v1";
        public const string Claim = "INFERRED_TRAINING";
        readonly World world;
        readonly SortedDictionary<string, MechanicalProfile> profiles;
        readonly SortedDictionary<string, string> owners = new SortedDictionary<string, string>(StringComparer.Ordinal);
        readonly List<JObject> records = new List<JObject>();
        public World World => world;
        public IReadOnlyList<JObject> Records => records.Select(r => (JObject)r.DeepClone()).ToArray();
        public IReadOnlyList<string> Owners => owners.Keys.ToArray();
        public string CatalogHash { get; private set; }
        public event Action<MechanicalOutcome> Changed;

        public MechanicalInteractions(World world, string profilesJson = null)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            var catalog = profilesJson == null ? Defaults() : ParseProfiles(profilesJson);
            profiles = new SortedDictionary<string, MechanicalProfile>(catalog.ToDictionary(p => p.Id), StringComparer.Ordinal);
            CatalogHash = Stable.Hash(CatalogJson(profiles.Values));
            // Bind every catalog bit to replay without relying on floating-point representation of a hash.
            for (int i = 0; i < 8; i++) world.Fields["mechanics.catalog.word" + i] = uint.Parse(CatalogHash.Substring(i * 8, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            foreach (var e in world.Entities.Values)
                foreach (var p in profiles.Values)
                    if (e.Get("mech.profile." + p.Id) == 1) owners[e.Id] = p.Id;
        }
        public static string DefaultProfilesJson() => CatalogJson(Defaults());
        static string CatalogJson(IEnumerable<MechanicalProfile> values) => new JObject
        { ["schemaVersion"] = Version, ["claim"] = Claim, ["profiles"] = JArray.FromObject(values.OrderBy(p => p.Id, StringComparer.Ordinal)) }.ToString(Formatting.Indented);
        static IEnumerable<MechanicalProfile> ParseProfiles(string json)
        {
            var root = JObject.Parse(json);
            if ((string)root["schemaVersion"] != Version || (string)root["claim"] != Claim || !(root["profiles"] is JArray)) throw new ArgumentException("Mechanical catalog schema/claim");
            var result = root["profiles"].ToObject<List<MechanicalProfile>>();
            if (result.Count == 0 || result.Any(p => p == null || string.IsNullOrWhiteSpace(p.Id) || p.Claim != Claim || string.IsNullOrWhiteSpace(p.Provenance) || !Finite(p.Confidence) || p.Confidence < 0 || p.Confidence > 1 || p.Parts == null || p.Parts.Count == 0) || result.Select(p => p.Id).Distinct().Count() != result.Count) throw new ArgumentException("Mechanical profile identity/provenance");
            foreach (var p in result)
            {
                if (p.Parts.Select(x => x.Id).Distinct().Count() != p.Parts.Count) throw new ArgumentException("Duplicate mechanical part");
                foreach (var a in p.Parts)
                    if (a == null || string.IsNullOrWhiteSpace(a.Id) || !new[] { "drag", "rotate", "press", "carry" }.Contains(a.Motion) || !new[] { "x", "y", "z" }.Contains(a.Axis) || new[] { a.Min, a.Max, a.Speed, a.Friction, a.Spring, a.Rest, a.Initial }.Any(x => !Finite(x)) || a.Max <= a.Min || a.Speed <= 0 || a.Friction < 0 || a.Friction >= 1 || a.Spring < 0 || a.Rest < a.Min || a.Rest > a.Max || a.Initial < a.Min || a.Initial > a.Max) throw new ArgumentException("Invalid mechanical part parameters");
            }
            return result;
        }
        static MechanicalPart P(string id, string motion, double speed = 1, double spring = 0, string tool = null) => new MechanicalPart { Id = id, Motion = motion, Speed = speed, Spring = spring, ToolKind = tool };
        static List<MechanicalProfile> Defaults()
        {
            return new List<MechanicalProfile>
            {
                new MechanicalProfile { Id = "hinged-door", Parts = new List<MechanicalPart> { P("lock", "rotate", 1, 0, "key"), P("latch", "press", 2.8, 2), P("leaf", "rotate", .55) } },
                new MechanicalProfile { Id = "sliding-door", Parts = new List<MechanicalPart> { P("lock", "rotate", 1, 0, "key"), P("latch", "press", 2.8, 2), P("leaf", "drag", .55) } },
                new MechanicalProfile { Id = "breaker-cabinet", Parts = new List<MechanicalPart> { P("lock", "rotate", 1, 0, "key"), P("latch", "press", 2.8, 2), P("leaf", "rotate", .65), new MechanicalPart { Id = "switch", Motion = "rotate", Speed = 1.2, Initial = 1 } } },
                new MechanicalProfile { Id = "extinguisher", Parts = new List<MechanicalPart> { new MechanicalPart { Id = "body", Motion = "carry", Carryable = true }, new MechanicalPart { Id = "pin", Motion = "drag", Speed = .8, OneWay = true }, P("nozzle", "rotate", .8), P("lever", "press", 2.2, 3) } },
                new MechanicalProfile { Id = "radio", Parts = new List<MechanicalPart> { new MechanicalPart { Id = "body", Motion = "carry", Carryable = true }, new MechanicalPart { Id = "power", Motion = "rotate", Speed = 1.3, Initial = 1 }, P("channel", "rotate", .5), P("ptt", "press", 2.4, 4) } },
                new MechanicalProfile { Id = "small-tool", Parts = new List<MechanicalPart> { new MechanicalPart { Id = "body", Motion = "carry", Carryable = true } } },
                new MechanicalProfile { Id = "tool-connector", Parts = new List<MechanicalPart> { new MechanicalPart { Id = "body", Motion = "carry", Carryable = true }, P("plug", "drag", .75), P("coupler", "rotate", .55, 0, "wrench") } }
            };
        }
        public void InitializeDefaults(WorldObjectRegistry registry = null)
        {
            foreach (var e in world.Entities.Values.ToArray())
            {
                if (owners.ContainsKey(e.Id) || e.Kind=="source-object" || e.Has("source-input") || e.Has("decoration")) continue;
                string profile = e.Kind == "door" ? "sliding-door" : e.Kind == "service-cabinet" || e.Kind == "breaker" ? "breaker-cabinet" : e.Kind == "extinguisher" ? "extinguisher" : e.Kind == "public-address" || e.Kind == "radio" ? "radio" : e.Has("mechanical-connector") ? "tool-connector" : null;
                if (profile != null) Attach(e.Id, profile);
            }
            if (registry == null) return;
            foreach (var row in registry.Objects.Values)
                foreach (var binding in row.SourceBindings)
                    if (owners.ContainsKey(binding.EntityId) && row.WorldBounds != null && world.Entities[binding.EntityId].Get("mech.center.qualified") != 1)
                    {
                        var b = row.WorldBounds;
                        SetInteractionCenter(binding.EntityId, (b.MinX + b.MaxX) / 2, (b.MinY + b.MaxY) / 2, (b.MinZ + b.MaxZ) / 2);
                    }
        }
        public void Attach(string ownerId, string profileId)
        {
            Entity e; MechanicalProfile p;
            if (!world.Entities.TryGetValue(ownerId, out e) || !profiles.TryGetValue(profileId, out p)) throw new ArgumentException("Explicit owner and profile required");
            if (owners.ContainsKey(ownerId)) { if (owners[ownerId] != profileId) throw new ArgumentException("Profile already attached"); return; }
            owners.Add(ownerId, profileId); e.Set("mech.profile." + profileId, 1); e.Set("mech.profile", 1); e.Set("mech.inferred-training", 1); e.Set("mech.confidence", p.Confidence);
            e.Set("mech.friction-scale", .85 + Stable.Unit(world.Seed, "mechanical-resistance-v1", 0, ownerId) * .3);
            e.Set("mech.jammed", e.Get("jammed"));
            foreach (var part in p.Parts) e.Set(part.StateKey, Clamp(part.Id == "leaf" ? e.Get("open", part.Initial) : part.Id == "lock" ? 1 - e.Get("locked") : part.Initial, part.Min, part.Max));
            if (profileId == "extinguisher" || profileId == "radio") e.Set("mech.charge", Clamp(e.Get("charge", 1), 0, 1));
        }
        public void SetInteractionCenter(string ownerId, double x, double y, double z)
        {
            if (new[] { x, y, z }.Any(v => !Finite(v)) || !owners.ContainsKey(ownerId)) throw new ArgumentException("Finite mechanical interaction center required");
            var e = world.Entities[ownerId]; e.Set("mech.center.x", x); e.Set("mech.center.y", y); e.Set("mech.center.z", z); e.Set("mech.center.qualified", 1);
        }
        public bool HasProfile(string ownerId) => ownerId != null && owners.ContainsKey(ownerId);
        public IReadOnlyList<MechanicalPart> Parts(string ownerId) => owners.ContainsKey(ownerId) ? profiles[owners[ownerId]].Parts.Select(ClonePart).ToArray() : Array.Empty<MechanicalPart>();
        static MechanicalPart ClonePart(MechanicalPart p) => JsonConvert.DeserializeObject<MechanicalPart>(JsonConvert.SerializeObject(p));
        public double Position(string ownerId, string partId) => world.Entities[ownerId].Get("mech.part." + partId);
        public MechanicalInspection Inspect(string ownerId)
        {
            Entity e; if (!world.Entities.TryGetValue(ownerId, out e)) throw new ArgumentException("Unknown inspected object");
            bool attached = owners.ContainsKey(ownerId); var p = attached ? profiles[owners[ownerId]] : null;
            return new MechanicalInspection { OwnerId = ownerId, ProfileId = p?.Id, Claim = attached ? Claim : "SOURCE_INVENTORY_ONLY", Provenance = p?.Provenance ?? "saved source object; mechanical behavior unassigned", Confidence = p?.Confidence ?? 0, MutableParts = attached, State = JObject.FromObject(e.State), Parts = Parts(ownerId).ToArray() };
        }
        static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        static double Clamp(double v, double a, double b) => Math.Max(a, Math.Min(b, v));
        static string GripKey(string owner, string part) => "mech.grip." + owner + "::" + part;
        bool Held(Entity actor, string owner, string part) => actor.Get(GripKey(owner, part)) == 1;
        bool AnyHeld(string owner, string part) => world.Entities.Values.Any(a => Held(a, owner, part));
        bool Carried(Entity actor, string owner) => actor.Get("mech.carry." + owner) == 1;
        bool Tool(Entity actor, string toolId, string kind, out Entity tool)
        {
            tool = null;
            return !string.IsNullOrEmpty(toolId) && world.Entities.TryGetValue(toolId, out tool) && (tool.Kind == kind || tool.Has("tool:" + kind)) && Carried(actor, toolId) && tool.Get("mech.resource", 1) > 0;
        }
        string Validate(MechanicalInput input, out Entity actor, out Entity owner, out MechanicalPart part)
        {
            actor = owner = null; part = null;
            if (input == null || input.Tick != world.Tick || input.Sequence < 1 || new[] { input.Analog, input.Duration, input.Clearance, input.X, input.Y, input.Z, input.AimX, input.AimY, input.AimZ, input.OriginX, input.OriginY, input.OriginZ, input.DirectionX, input.DirectionY, input.DirectionZ }.Any(v => !Finite(v)) || input.Duration < 0 || input.Duration > world.StepSeconds || Math.Abs(input.Analog) > 1 || input.Clearance < 0 || input.Clearance > 1) return "invalid_input_envelope";
            if (string.IsNullOrEmpty(input.ActorId) || !world.Entities.TryGetValue(input.ActorId, out actor) || string.IsNullOrEmpty(input.OwnerId) || !world.Entities.TryGetValue(input.OwnerId, out owner) || !owners.ContainsKey(input.OwnerId)) return "unknown_actor_or_unassigned_mechanism";
            if (input.Sequence <= actor.Get("mech.last-sequence")) return "duplicate_or_stale_sequence";
            part = profiles[owners[input.OwnerId]].Parts.FirstOrDefault(p => p.Id == input.PartId);
            if (part == null) return "unknown_part";
            if (!new[] { "grip", "release", "drag", "rotate", "press", "carry", "place", "check", "tag", "connect", "disconnect" }.Contains(input.Action)) return "unknown_control";
            if (input.Action == "release") return Held(actor, owner.Id, part.Id) ? null : "part_not_gripped";
            if (actor.Get("health", 1) <= .05 || actor.Get("mobility", 1) <= 0) return "actor_unavailable";
            double dx = actor.X - owner.Get("mech.center.x", owner.X), dy = actor.Y + 1 - owner.Get("mech.center.y", owner.Y), dz = actor.Z - owner.Get("mech.center.z", owner.Z);
            if (!Carried(actor, owner.Id) && Math.Sqrt(dx * dx + dy * dy + dz * dz) > actor.Get("mech.reach", 2.4)) return "out_of_reach";
            if (!input.Contact && input.Action != "place") return "no_observed_part_contact";
            string actorId = actor.Id, ownerId = owner.Id, partId = part.Id;
            if (input.Action == "grip") return world.Entities.Values.Any(a => a.Id != actorId && Held(a, ownerId, partId)) ? "part_held_by_other_actor" : null;
            if (!Held(actor, owner.Id, part.Id)) return "grip_required";
            if (input.Action == "carry") return !part.Carryable ? "fixed_part" : world.Entities.Values.Any(a => a.Id != actorId && Carried(a, ownerId)) ? "carried_by_other_actor" : null;
            if (input.Action == "place")
            {
                if (owner.Kind=="source-object"||owner.Has("source-input")) return "source_inventory_pose_immutable";
                if (!part.Carryable || !Carried(actor, owner.Id)) return "carry_required";
                double x = input.X - actor.X, y = input.Y - actor.Y, z = input.Z - actor.Z;
                return input.Clearance < 1 ? "placement_blocked" : Math.Sqrt(x * x + y * y + z * z) > actor.Get("mech.reach", 2.4) ? "placement_out_of_reach" : null;
            }
            if (input.Action == "check" || input.Action == "tag")
            {
                if (input.Action == "check" && owners[owner.Id] == "radio" && part.Id == "channel")
                {
                    Entity contact;
                    if (string.IsNullOrEmpty(input.TargetId) || !world.Entities.TryGetValue(input.TargetId, out contact) || !(contact.Has("radio") || contact.Has("communications"))) return "radio_contact_required";
                    return owner.Get("communications.available", 1) <= 0 || contact.Get("communications.available", 1) <= 0 ? "radio_contact_unavailable" : input.Duration <= 0 ? "positive_duration_required" : DurationError(owner, part, input);
                }
                if (owners[owner.Id] != "breaker-cabinet" || part.Id != "switch" || Position(owner.Id, "leaf") < .65) return "cabinet_access_required";
                if (Position(owner.Id, "switch") > .05) return "isolation_required";
                Entity tool;
                if (!Tool(actor, input.ToolId, input.Action == "check" ? "voltage-probe" : "lockout-tag", out tool)) return "wrong_or_unheld_tool";
                if (input.Action == "tag" && owner.Get("mech.checked") < 1) return "check_required_before_tag";
                return input.Duration <= 0 ? "positive_duration_required" : DurationError(owner, part, input);
            }
            if (input.Action == "connect" || input.Action == "disconnect")
            {
                Entity target;
                if (owners[owner.Id] != "tool-connector" || part.Id != "coupler" || string.IsNullOrEmpty(input.TargetId) || !world.Entities.TryGetValue(input.TargetId, out target) || !target.Has("connector-socket")) return "compatible_socket_required";
                if (input.Action == "connect" && (Position(owner.Id, "plug") < .95 || Position(owner.Id, "coupler") < .95)) return "partial_connection";
                if (input.Action == "disconnect" && Position(owner.Id, "coupler") > .05) return "loosen_coupler_first";
                if (!target.Has("connector:" + owner.Kind)) return "connector_type_mismatch";
                double tx = target.Get("mech.center.x", target.X) - owner.Get("mech.center.x", owner.X), ty = target.Get("mech.center.y", target.Y) - owner.Get("mech.center.y", owner.Y), tz = target.Get("mech.center.z", target.Z) - owner.Get("mech.center.z", owner.Z);
                if (Math.Sqrt(tx * tx + ty * ty + tz * tz) > .75) return "socket_out_of_contact_range";
                return input.Clearance < 1 ? "connection_blocked" : null;
            }
            if (input.Action != part.Motion || input.Action == "carry") return "control_axis_mismatch";
            if (input.Duration <= 0) return "positive_duration_required";
            Entity required;
            if (part.ToolKind != null && !Tool(actor, input.ToolId, part.ToolKind, out required)) return "wrong_or_unheld_tool";
            if (part.Id == "lock" && !string.IsNullOrEmpty(input.ToolId) && world.Entities[input.ToolId].Get("mech.key-owner." + owner.Id) != 1 && !world.Entities[input.ToolId].Has("training-universal-key")) return "key_does_not_fit";
            if (part.Id == "leaf")
            {
                if (owner.Get("locked") > .5) return "locked";
                if (owner.Get("jammed") > .5 || owner.Get("mech.jammed") > .5) return "jammed";
                if (Position(owner.Id, "leaf") < .05 && input.Analog > 0 && Position(owner.Id, "latch") < .75) return "latch_engaged";
                if (owner.Get("mech.connected") > 0) return "connected_part_fixed";
            }
            if (part.Id == "switch")
            {
                if (Position(owner.Id, "leaf") < .65) return "cabinet_access_required";
                if (input.Analog > 0 && owner.Get("mech.tagged") > 0) return "lockout_tag_blocks_reenergizing";
            }
            if (part.Id == "lever" && Position(owner.Id, "pin") < .95) return "safety_pin_engaged";
            if (part.Id == "lever" && owner.Get("mech.charge") <= 0) return "empty_charge";
            if (part.Id == "ptt" && (Position(owner.Id, "power") < .5 || owner.Get("power", 1) <= 0 || owner.Get("mech.charge") <= 0)) return "radio_unpowered";
            if (part.Id == "coupler" && Position(owner.Id, "plug") < .95) return "insert_plug_first";
            if (part.OneWay && input.Analog < 0) return "removed_part_cannot_reinsert";
            if (input.Clearance <= 0 && Math.Abs(input.Analog) > 0) return "movement_blocked";
            return DurationError(owner, part, input);
        }
        string DurationError(Entity owner, MechanicalPart part, MechanicalInput input)
        {
            double elapsed = owner.Get("mech.duration-tick." + part.Id, -1) == world.Tick ? owner.Get("mech.duration." + part.Id) : 0;
            return elapsed + input.Duration > world.StepSeconds + 1e-9 ? "part_duration_exceeds_tick" : null;
        }
        public MechanicalOutcome Apply(MechanicalInput input)
        {
            Entity actor, owner; MechanicalPart part;
            string error = Validate(input, out actor, out owner, out part);
            var result = new MechanicalOutcome { Tick = input?.Tick ?? world.Tick, Sequence = input?.Sequence ?? 0, ActorId = input?.ActorId, OwnerId = input?.OwnerId, PartId = input?.PartId, Action = input?.Action, Accepted = error == null, Reason = error ?? "applied" };
            if (error != null) return result; // Rejected inputs mutate no ownership, state, resources or journal.
            result.Before = owner.Get(part.StateKey); result.After = result.Before;
            if (input.Action == "grip") actor.Set(GripKey(owner.Id, part.Id), 1);
            else if (input.Action == "release") actor.Set(GripKey(owner.Id, part.Id), 0);
            else if (input.Action == "carry") actor.Set("mech.carry." + owner.Id, 1);
            else if (input.Action == "place")
            {
                actor.Set("mech.carry." + owner.Id, 0); actor.Set(GripKey(owner.Id, part.Id), 0);
                owner.X = input.X; owner.Y = input.Y; owner.Z = input.Z; owner.Version++; SetInteractionCenter(owner.Id, input.X, input.Y, input.Z);
            }
            else if (input.Action == "check")
            {
                string prefix = owners[owner.Id] == "radio" ? "mech.radio-contact" : "mech.check";
                owner.Set(prefix + "-progress", Math.Min(1, owner.Get(prefix + "-progress") + input.Duration));
                owner.Set(prefix == "mech.check" ? "mech.checked" : "radio.contact", owner.Get(prefix + "-progress") >= 1 ? 1 : 0);
                if (prefix != "mech.check") owner.Set("mech.radio-contact." + input.TargetId, owner.Get("radio.contact"));
                result.After = owner.Get(prefix + "-progress");
            }
            else if (input.Action == "tag")
            {
                var tool = world.Entities[input.ToolId]; if (owner.Get("mech.tagged") == 0) { tool.Set("mech.resource", Math.Max(0, tool.Get("mech.resource", 1) - 1)); result.ResourceConsumed = 1; }
                owner.Set("mech.tagged", 1); result.After = 1;
            }
            else if (input.Action == "connect" || input.Action == "disconnect")
            {
                owner.Set("mech.connection." + input.TargetId, input.Action == "connect" ? 1 : 0);
                owner.Set("mech.connected", owner.State.Any(s => s.Key.StartsWith("mech.connection.", StringComparison.Ordinal) && s.Value > 0) ? 1 : 0); result.After = owner.Get("mech.connected");
            }
            else
            {
                double friction = part.Friction * owner.Get("mech.friction-scale", 1);
                double effort = Math.Sign(input.Analog) * Math.Max(0, Math.Abs(input.Analog) - friction) / (1 - friction);
                double next = Clamp(result.Before + effort * part.Speed * input.Duration * input.Clearance, part.Min, part.Max);
                owner.Set(part.StateKey, next); result.After = next;
                if (input.Clearance < 1 && next != result.Before) result.Reason = "partial_clearance";
                else if (next == result.Before && Math.Abs(input.Analog) > 0) result.Reason = "friction_or_end_stop";
                if (part.Id == "switch" && next > .05) { owner.Set("mech.checked", 0); owner.Set("mech.check-progress", 0); }
                if (part.Id == "lever" || part.Id == "ptt") Consume(owner, input.Duration, result);
                if (part.Id == "lever" && result.ResourceConsumed > 0) Discharge(actor, owner, input, result);
            }
            if (input.Duration > 0)
            {
                double elapsed = owner.Get("mech.duration-tick." + part.Id, -1) == world.Tick ? owner.Get("mech.duration." + part.Id) : 0;
                owner.Set("mech.duration-tick." + part.Id, world.Tick); owner.Set("mech.duration." + part.Id, elapsed + input.Duration);
            }
            actor.Set("mech.last-sequence", input.Sequence); Project(owner);
            if (result.Before != result.After) world.Journal.Add(new Transition { Tick = world.Tick, Domain = "mechanical", Mechanism = input.Action, Source = actor.Id, Target = owner.Id, Property = part.StateKey, Before = result.Before, After = result.After, Reason = Claim + "; " + result.Reason });
            Changed?.Invoke(result); return result;
        }
        void Discharge(Entity actor, Entity owner, MechanicalInput input, MechanicalOutcome result)
        {
            result.Reason = "discharged_without_qualified_hit";
            Entity target;
            if (!input.AimContact || string.IsNullOrEmpty(input.TargetId) || !world.Entities.TryGetValue(input.TargetId, out target) || !target.Has("thermal")) return;
            double ox = owner.Get("mech.center.x", owner.X), oy = owner.Get("mech.center.y", owner.Y), oz = owner.Get("mech.center.z", owner.Z);
            if (Carried(actor, owner.Id)) { ox = actor.X; oy = actor.Y + 1; oz = actor.Z; }
            double sourceDistance = Math.Sqrt(Math.Pow(input.OriginX - ox, 2) + Math.Pow(input.OriginY - oy, 2) + Math.Pow(input.OriginZ - oz, 2));
            double dx = input.AimX - input.OriginX, dy = input.AimY - input.OriginY, dz = input.AimZ - input.OriginZ;
            double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz), magnitude = Math.Sqrt(input.DirectionX * input.DirectionX + input.DirectionY * input.DirectionY + input.DirectionZ * input.DirectionZ);
            double tx = target.Get("mech.center.x", target.X), ty = target.Get("mech.center.y", target.Y), tz = target.Get("mech.center.z", target.Z);
            double targetDistance = Math.Sqrt(Math.Pow(input.AimX - tx, 2) + Math.Pow(input.AimY - ty, 2) + Math.Pow(input.AimZ - tz, 2));
            bool hitWithinTarget=targetDistance<=target.Get("mech.effect.radius",1.5);
            if(target.State.ContainsKey("mech.bounds.min.x"))hitWithinTarget=input.AimX>=target.Get("mech.bounds.min.x")-.05&&input.AimX<=target.Get("mech.bounds.max.x")+.05&&input.AimY>=target.Get("mech.bounds.min.y")-.05&&input.AimY<=target.Get("mech.bounds.max.y")+.05&&input.AimZ>=target.Get("mech.bounds.min.z")-.05&&input.AimZ<=target.Get("mech.bounds.max.z")+.05;
            if (sourceDistance > 1.5 || distance < .01 || distance > 4 || Math.Abs(magnitude - 1) > .001 || !hitWithinTarget || (dx * input.DirectionX + dy * input.DirectionY + dz * input.DirectionZ) / distance < .98) return;
            double amount = Math.Min(target.Get("fire"), result.ResourceConsumed * 3);
            target.Set("fire", Math.Max(0, target.Get("fire") - amount)); target.Set("wetness", Math.Min(1, target.Get("wetness") + result.ResourceConsumed));
            target.Set("mech.agent-deposit", target.Get("mech.agent-deposit") + result.ResourceConsumed);
            result.EffectTarget = target.Id; result.EffectAmount = amount; result.Reason = "aimed_training_discharge";
            world.Journal.Add(new Transition { Tick = world.Tick, Domain = "mechanical", Mechanism = "aimed-training-discharge", Source = owner.Id, Target = target.Id, Property = "fire", Before = target.Get("fire") + amount, After = target.Get("fire"), Reason = Claim + "; actual recorded ray contact, <=4 MODEL units, finite local charge" });
        }
        void Consume(Entity owner, double duration, MechanicalOutcome result)
        {
            string profile = owners[owner.Id]; double rate = 0;
            if (profile == "extinguisher" && Position(owner.Id, "pin") >= .95) rate = Position(owner.Id, "lever") * .07;
            if (profile == "radio" && Position(owner.Id, "power") >= .5) rate = Position(owner.Id, "ptt") * .002;
            if (rate <= 0) return;
            double before = owner.Get("mech.charge"), next = Math.Max(0, before - rate * duration);
            owner.Set("mech.charge", next); owner.Set("charge", next); result.ResourceConsumed += before - next;
        }
        void Project(Entity e)
        {
            string profile = owners[e.Id];
            if (profile == "hinged-door" || profile == "sliding-door" || profile == "breaker-cabinet")
            {
                e.Set("open", Position(e.Id, "leaf")); e.Set("locked", Position(e.Id, "lock") < .95 ? 1 : 0);
            }
            if (profile == "breaker-cabinet")
            {
                double on = Position(e.Id, "switch") > .5 ? 1 : 0;
                e.Set("power.isolated", 1 - on); e.Set("power", on * e.Get("supply.available", 1));
            }
            if (profile == "extinguisher") e.Set("mech.flow", Position(e.Id, "pin") >= .95 && e.Get("mech.charge") > 0 ? Position(e.Id, "lever") : 0);
            if (profile == "radio")
            {
                bool contactKnown = e.State.Any(s => s.Key.StartsWith("mech.radio-contact.", StringComparison.Ordinal) && s.Value > 0);
                bool receiverAvailable = !contactKnown || e.State.Where(s => s.Key.StartsWith("mech.radio-contact.", StringComparison.Ordinal) && s.Value > 0).Any(s =>
                {
                    Entity receiver; return world.Entities.TryGetValue(s.Key.Substring("mech.radio-contact.".Length), out receiver) && receiver.Get("communications.available", 1) > 0 && receiver.Get("power", 1) > 0;
                });
                bool available = e.Get("communications.available", 1) > 0 && receiverAvailable;
                if (!available) { e.Set("radio.contact", 0); e.Set("mech.radio-contact-progress", 0); }
                e.Set("radio.transmitting", Position(e.Id, "power") >= .5 && e.Get("power", 1) > 0 && e.Get("mech.charge") > 0 && Position(e.Id, "ptt") >= .6 ? 1 : 0);
                e.Set("radio.delivered", e.Get("radio.transmitting") * e.Get("radio.contact") * (available ? 1 : 0));
            }
        }
        /// <summary>Call once before the causal kernel at World.Tick. Queue is portable and order-independent.</summary>
        public JArray Tick(IEnumerable<MechanicalInput> inputs)
        {
            var queue = (inputs ?? Enumerable.Empty<MechanicalInput>()).ToArray();
            if (queue.Any(i => i == null || i.Tick != world.Tick) || queue.GroupBy(i => i.ActorId + "|" + i.Sequence).Any(g => g.Count() > 1)) throw new ArgumentException("Mechanical batch tick/duplicate sequence; no changes applied");
            if (world.Fields.ContainsKey("mechanics.last-tick") && world.Fields["mechanics.last-tick"] >= world.Tick) throw new InvalidOperationException("Mechanical tick already applied");
            string before = world.Digest(); var outcomes = new JArray();
            foreach (var i in queue.OrderBy(i => i.Sequence).ThenBy(i => i.ActorId, StringComparer.Ordinal)) outcomes.Add(Apply(i).Json());
            // Return springs run only on released parts; charge is consumed by held analog input duration.
            foreach (var entry in owners)
            {
                var e = world.Entities[entry.Key];
                foreach (var p in profiles[entry.Value].Parts)
                    if (p.Spring > 0 && !AnyHeld(e.Id, p.Id))
                    {
                        double v = e.Get(p.StateKey), step = p.Spring * world.StepSeconds;
                        e.Set(p.StateKey, v > p.Rest ? Math.Max(p.Rest, v - step) : Math.Min(p.Rest, v + step));
                    }
                string resourcePart = entry.Value == "extinguisher" ? "lever" : entry.Value == "radio" ? "ptt" : null;
                if (resourcePart != null)
                {
                    double used = e.Get("mech.duration-tick." + resourcePart, -1) == world.Tick ? e.Get("mech.duration." + resourcePart) : 0;
                    var passive = new MechanicalOutcome { Tick = world.Tick, OwnerId = e.Id, PartId = resourcePart, Action = "held_resource_flow", Accepted = true, Reason = "local_part_state", Before = e.Get("mech.charge") };
                    Consume(e, Math.Max(0, world.StepSeconds - used), passive); passive.After = e.Get("mech.charge");
                    if (passive.ResourceConsumed > 0) outcomes.Add(passive.Json());
                }
                Project(e);
            }
            world.Fields["mechanics.last-tick"] = world.Tick;
            records.Add(new JObject { ["version"] = Version, ["claim"] = Claim, ["tick"] = world.Tick, ["catalog_hash"] = CatalogHash, ["before"] = before, ["inputs"] = LocalTapeCodec.FromObject(queue.OrderBy(i => i.Sequence).ThenBy(i => i.ActorId, StringComparer.Ordinal)), ["outcomes"] = outcomes.DeepClone(), ["after"] = world.Digest() });
            return outcomes;
        }
        public string SerializeRecording() => string.Join("\n", records.Select(r => r.ToString(Formatting.None)));
        public JArray Replay(JObject record)
        {
            if ((string)record["version"] != Version || (string)record["catalog_hash"] != CatalogHash || (long?)record["tick"] != world.Tick || (string)record["before"] != world.Digest()) throw new InvalidOperationException("Mechanical replay precondition mismatch");
            var output = Tick(record["inputs"].ToObject<MechanicalInput[]>());
            if (!JToken.DeepEquals(output, record["outcomes"]) || world.Digest() != (string)record["after"]) throw new InvalidOperationException("Mechanical replay outcome mismatch");
            return output;
        }
    }
}
