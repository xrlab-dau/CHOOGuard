using System;
using System.Linq;
using ChooGuard.Seeded;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class MechanicalInteractionsTests
    {
        static void Check(bool ok, string why) { if (!ok) throw new Exception("Detailed mechanics: " + why); }
        sealed class Fixture
        {
            public World World = new World { Seed = 20261014, StepSeconds = .5 };
            public MechanicalInteractions Mechanics;
            public Entity Actor, Owner;
            long sequence;
            public Fixture(string profile, string kind = null, bool locked = false, double open = 0)
            {
                Actor = new Entity { Id = "player", Kind = "staff" }; Actor.Tags.Add("staff"); World.Add(Actor);
                Owner = new Entity { Id = "fixture:mechanism", Kind = kind ?? profile, Y = 1 }; Owner.Set("locked", locked ? 1 : 0); Owner.Set("open", open); Owner.Set("power", 1); World.Add(Owner);
                Mechanics = new MechanicalInteractions(World); Mechanics.Attach(Owner.Id, profile);
            }
            public Entity Tool(string id, string kind, bool carried = true)
            {
                var e = new Entity { Id = id, Kind = kind }; e.Tags.Add("tool:" + kind); e.Set("mech.resource", 2); World.Add(e);
                if (carried) Actor.Set("mech.carry." + id, 1); return e;
            }
            public MechanicalInput Input(string part, string action, double analog = 0, double duration = 0, string tool = null)
            {
                return new MechanicalInput { Tick = World.Tick, Sequence = ++sequence, ActorId = Actor.Id, OwnerId = Owner.Id, PartId = part, Action = action, Analog = analog, Duration = duration, ToolId = tool, Contact = true };
            }
            public MechanicalOutcome Apply(string part, string action, double analog = 0, double duration = 0, string tool = null)
            {
                if (duration > 0) World.Tick++;
                return Mechanics.Apply(Input(part, action, analog, duration, tool));
            }
            public void Grip(string part) { Check(Apply(part, "grip").Accepted, "grip " + part); }
            public void Move(string part, string action, double analog, int ticks, string tool = null)
            {
                for (int i = 0; i < ticks; i++) Check(Apply(part, action, analog, .5, tool).Accepted, "move " + part);
            }
            public void AtomicReject(MechanicalInput input, string reason)
            {
                string before = World.Digest(); int journal = World.Journal.Count; var r = Mechanics.Apply(input);
                Check(!r.Accepted && r.Reason == reason, "expected rejection " + reason + ", got " + r.Reason);
                Check(before == World.Digest() && journal == World.Journal.Count, "negative input atomic: " + reason);
            }
        }
        public static void Run()
        {
            Check(MechanicalContactGeometry.PointSegmentDistance(10,0,0,0,0,0,20,0,0)==0,"midsegment exposure on a 20 MODEL-unit route is detected");
            Check(MechanicalContactGeometry.PointSegmentDistance(10,0,4,0,0,0,20,0,0)==4&&MechanicalContactGeometry.PointSegmentDistance(10,4,0,0,0,0,20,0,0)==4,"off-route and other-height hazards outside3 MODEL units stay outside exposure");
            PartialMotionLocksAndClearance(); SpringsAndDuration(); BreakerSequenceAndTools(); DischargeAndResources(); CarryPlaceAndConnections(); RadioContactAndPower(); ReplayAndNoIncidentForcing();
            Console.WriteLine("Detailed mechanics: partial motion, exact key/tool fit, locks, jam/contact/clearance, springs, duration budget, isolation/check/tag, aimed finite discharge, carry/place/connector, radio contact and digest replay PASS.");
        }
        static void PartialMotionLocksAndClearance()
        {
            var f = new Fixture("hinged-door", "door", true); f.Grip("leaf");
            f.AtomicReject(f.Input("leaf", "rotate", 1, .5), "locked");
            f.Grip("lock"); f.Tool("bad-tool", "wrench"); f.AtomicReject(f.Input("lock", "rotate", 1, .5, "bad-tool"), "wrong_or_unheld_tool");
            var key = f.Tool("key", "key"); f.AtomicReject(f.Input("lock", "rotate", 1, .5, "key"), "key_does_not_fit");
            key.Set("mech.key-owner." + f.Owner.Id, 1); f.Move("lock", "rotate", 1, 2, "key");
            Check(f.Owner.Get("locked") == 0, "key rotation unlocks only at complete range");
            f.AtomicReject(f.Input("leaf", "rotate", 1, .5), "latch_engaged");
            f.Grip("latch"); f.Move("latch", "press", 1, 1); f.Move("leaf", "rotate", .6, 1);
            double partial = f.Mechanics.Position(f.Owner.Id, "leaf"); Check(partial > 0 && partial < .3 && f.Owner.Get("open") == partial, "partial leaf projects incremental door state");
            f.Owner.Set("jammed", 1); f.AtomicReject(f.Input("leaf", "rotate", 1, .5), "jammed"); f.Owner.Set("jammed", 0);
            f.World.Tick++; var input = f.Input("leaf", "rotate", 1, .5); input.Clearance = .25; var outcome = f.Mechanics.Apply(input);
            Check(outcome.Accepted && outcome.Reason == "partial_clearance" && outcome.After - outcome.Before < .1, "partial clearance limits actual progress");
            f.World.Tick++; input = f.Input("leaf", "rotate", 1, .5); input.Clearance = 0; f.AtomicReject(input, "movement_blocked");
            input = f.Input("leaf", "rotate", 1, .5); input.Contact = false; f.AtomicReject(input, "no_observed_part_contact");
        }
        static void SpringsAndDuration()
        {
            var f = new Fixture("sliding-door", "door"); f.Grip("latch"); f.Move("latch", "press", 1, 1);
            Check(f.Mechanics.Position(f.Owner.Id, "latch") == 1, "latch reaches stop");
            f.AtomicReject(f.Input("latch", "press", 1, .1), "part_duration_exceeds_tick");
            f.Apply("latch", "release"); f.World.Tick++; f.Mechanics.Tick(Array.Empty<MechanicalInput>());
            Check(f.Mechanics.Position(f.Owner.Id, "latch") == 0, "released spring returns to rest");
            string before = f.World.Digest(); bool rejected = false;
            try { f.Mechanics.Tick(new[] { f.Input("leaf", "grip"), f.Input("leaf", "grip") }); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && before == f.World.Digest(), "duplicate fixed tick rejected before effects");
            var invalid = f.Input("leaf", "rotate", double.NaN, .5); f.AtomicReject(invalid, "invalid_input_envelope");
        }
        static void BreakerSequenceAndTools()
        {
            var f = new Fixture("breaker-cabinet", "service-cabinet", open: 1); f.Grip("switch");
            f.Tool("probe", "voltage-probe"); f.Tool("tag", "lockout-tag");
            f.AtomicReject(f.Input("switch", "check", 0, .5, "probe"), "isolation_required");
            f.Move("switch", "rotate", -1, 2); Check(f.Owner.Get("power.isolated") == 1 && f.Owner.Get("power") == 0, "switch mechanically isolates power");
            f.AtomicReject(f.Input("switch", "tag", 0, .5, "tag"), "check_required_before_tag");
            f.Apply("switch", "check", 0, .5, "probe"); Check(f.Owner.Get("mech.checked") == 0 && f.Owner.Get("mech.check-progress") == .5, "check remains partial");
            f.Apply("switch", "check", 0, .5, "probe"); Check(f.Owner.Get("mech.checked") == 1, "duration-qualified check");
            var tag = f.Apply("switch", "tag", 0, .5, "tag"); Check(tag.Accepted && tag.ResourceConsumed == 1 && f.World.Entities["tag"].Get("mech.resource") == 1, "one actual tag consumed");
            f.AtomicReject(f.Input("switch", "rotate", 1, .5), "lockout_tag_blocks_reenergizing");
        }
        static void DischargeAndResources()
        {
            var f = new Fixture("extinguisher"); f.Grip("lever"); f.AtomicReject(f.Input("lever", "press", 1, .5), "safety_pin_engaged");
            f.Grip("pin"); f.Move("pin", "drag", 1, 3); f.AtomicReject(f.Input("pin", "drag", -1, .5), "removed_part_cannot_reinsert");
            var fire = new Entity { Id = "test:thermal-target", Kind = "explicit-thermal-fixture", Z = 2, Y = 1 }; fire.Tags.Add("thermal"); fire.Set("fire", .8); f.World.Add(fire);
            f.World.Tick++; var hit = f.Input("lever", "press", 1, .5); hit.TargetId = fire.Id; hit.AimContact = true; hit.OriginY = 1; hit.AimY = 1; hit.AimZ = 2; hit.DirectionZ = 1;
            var outcome = f.Mechanics.Apply(hit); Check(outcome.Accepted && outcome.EffectTarget == fire.Id && outcome.EffectAmount > 0 && fire.Get("fire") < .8 && f.Owner.Get("charge") < 1, "real part state and ray observation consume charge and reduce only explicit thermal target");
            double before = fire.Get("fire"); f.World.Tick++; hit = f.Input("lever", "press", 1, .5); hit.TargetId = fire.Id; hit.AimContact = true; hit.OriginY = 1; hit.AimY = 1; hit.AimZ = 2; hit.DirectionZ = -1;
            outcome = f.Mechanics.Apply(hit); Check(outcome.Accepted && outcome.ResourceConsumed > 0 && outcome.EffectAmount == 0 && fire.Get("fire") == before, "wrong aim wastes charge without remote effect");
            f.Apply("lever", "release"); f.World.Tick++; f.Mechanics.Tick(Array.Empty<MechanicalInput>()); Check(f.Owner.Get("mech.flow") == 0, "released lever spring stops flow");
            f.Owner.Set("mech.charge", 0); f.Grip("lever"); f.AtomicReject(f.Input("lever", "press", 1, .5), "empty_charge");
        }
        static void CarryPlaceAndConnections()
        {
            var f = new Fixture("tool-connector", "hose"); f.Grip("body"); Check(f.Apply("body", "carry").Accepted, "carry small body");
            f.Apply("body", "release"); Check(f.Actor.Get("mech.carry." + f.Owner.Id) == 1, "tool stays carried after grip released");
            f.Grip("body"); var place = f.Input("body", "place"); place.X = 1; place.Clearance = 0; f.AtomicReject(place, "placement_blocked");
            place = f.Input("body", "place"); place.X = .4; place.Y = 1; Check(f.Mechanics.Apply(place).Accepted && f.Owner.X == .4, "clear local place changes training body pose");
            var socket = new Entity { Id = "test:socket", Kind = "socket", X = .4, Y = 1 }; socket.Tags.Add("connector-socket"); socket.Tags.Add("connector:hose"); f.World.Add(socket);
            f.Grip("coupler"); var connect = f.Input("coupler", "connect"); connect.TargetId = socket.Id; f.AtomicReject(connect, "partial_connection");
            f.Tool("wrench", "wrench"); f.Grip("plug"); f.Move("plug", "drag", 1, 3); f.Move("coupler", "rotate", 1, 4, "wrench");
            connect = f.Input("coupler", "connect"); connect.TargetId = socket.Id; Check(f.Mechanics.Apply(connect).Accepted && f.Owner.Get("mech.connected") == 1, "seated plug and tightened compatible coupler connect");
            var disconnect = f.Input("coupler", "disconnect"); disconnect.TargetId = socket.Id; f.AtomicReject(disconnect, "loosen_coupler_first");
        }
        static void RadioContactAndPower()
        {
            var f = new Fixture("radio"); f.Owner.Tags.Add("radio"); var contact = new Entity { Id = "test:receiver", Kind = "radio" }; contact.Tags.Add("radio"); f.World.Add(contact);
            f.Grip("ptt"); f.Move("ptt", "press", 1, 1); Check(f.Owner.Get("radio.transmitting") == 1 && f.Owner.Get("radio.delivered") == 0, "PTT transmits but unconfirmed contact cannot deliver");
            f.Grip("channel");
            for (int i = 0; i < 2; i++) { f.World.Tick++; var input = f.Input("channel", "check", 0, .5); input.TargetId = contact.Id; Check(f.Mechanics.Apply(input).Accepted, "radio contact duration"); }
            Check(f.Owner.Get("radio.contact") == 1 && f.Owner.Get("radio.delivered") == 1, "contact state plus held PTT qualifies local delivery");
            f.Owner.Set("communications.available", 0); f.World.Tick++; f.Mechanics.Tick(Array.Empty<MechanicalInput>()); Check(f.Owner.Get("radio.contact") == 0 && f.Owner.Get("radio.delivered") == 0, "lost communication invalidates radio contact");
            f.Owner.Set("power", 0); f.AtomicReject(f.Input("ptt", "press", 1, .5), "radio_unpowered");
        }
        static void ReplayAndNoIncidentForcing()
        {
            var a = new Fixture("sliding-door", "door"); var b = new Fixture("sliding-door", "door");
            var first = new[] { a.Input("latch", "grip"), a.Input("latch", "press", 1, .5), a.Input("leaf", "grip") };
            a.Mechanics.Tick(first); b.Mechanics.Replay(a.Mechanics.Records.Last()); Check(a.World.Digest() == b.World.Digest(), "first fixed-tick replay exact");
            a.World.Tick++; b.World.Tick++; a.Mechanics.Tick(new[] { a.Input("leaf", "drag", .7, .5) }); b.Mechanics.Replay(a.Mechanics.Records.Last());
            Check(a.World.Digest() == b.World.Digest() && a.Mechanics.SerializeRecording().Split('\n').Length == 2, "mechanical input/outcome/digest recording replay exact");
            Check(a.World.Entities.Values.All(e => !e.State.ContainsKey("fire") && !e.State.ContainsKey("smoke") && !e.State.ContainsKey("fault")), "no mechanics action creates scripted incidents");
            var restored = new MechanicalInteractions(a.World, MechanicalInteractions.DefaultProfilesJson()); Check(restored.HasProfile(a.Owner.Id) && restored.CatalogHash == a.Mechanics.CatalogHash, "state/catalog reconstruct mechanical ownership");
            var source = new Entity { Id = "source:plain-wall", Kind = "source-object" }; source.Tags.Add("source-object"); a.World.Add(source);
            Check(!restored.Inspect(source.Id).MutableParts && restored.Inspect(source.Id).Parts.Length == 0, "all source objects inspectable without invented part motion");
            double friction = a.Owner.Get("mech.friction-scale"); a.World.Add(new Entity { Id = "unrelated", Kind = "decoration" }); Check(a.Owner.Get("mech.friction-scale") == friction, "unrelated object does not reroll mechanical variation");
            string digest = a.World.Digest(); var duplicate = a.Input("leaf", "drag", 1, .5); bool rejected = false;
            try { a.World.Tick++; duplicate.Tick = a.World.Tick; a.Mechanics.Tick(new[] { duplicate, duplicate.Copy() }); } catch (ArgumentException) { rejected = true; }
            Check(rejected && a.Mechanics.Records.Count == 2, "duplicate input queue rejected before publication");
        }
    }
}
