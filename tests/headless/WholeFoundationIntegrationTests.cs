using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChooGuard.Foundation.Multiplayer;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;

namespace ChooGuard.Foundation.Headless
{
    // Exercises canonical C# and committed public/synthetic data in one process.
    // Twenty identities are NOT twenty transport clients; no Unity engine is mocked.
    public sealed class WholeFoundationIntegrationTests
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions {
            IncludeFields = true, IgnoreReadOnlyProperties = true
        };
        private static T Read<T>(string relative) => JsonSerializer.Deserialize<T>(File.ReadAllText(relative), Json);
        private static ConnectedWorldDefinition World() => Read<ConnectedWorldDefinition>("foundation/world/connected-world-profile.json");
        private static FoundationSimulationProfile Profile() => Read<FoundationSimulationProfile>("foundation/world/foundation-simulation-profile.json");
        private sealed class MemoryJournal : ICommitSink
        {
            public readonly List<ShiftCommit> Commits = new List<ShiftCommit>();
            public void Append(ShiftCommit commit) => Commits.Add(commit.Copy());
        }
        private static WorldState Roster(ConnectedWorldDefinition world)
        {
            world.Validate(); var region = world.Region(world.StartRegionId); var frame = world.Frame(region.FrameId);
            var participants = Enumerable.Range(0, 20).Select(i => new ParticipantState {
                ParticipantId = "p" + i, TeamId = i < 10 ? "red" : "blue", RoleId = "role-01",
                IsInstructor = i == 19, RegionId = region.Id, FrameId = frame.FrameId,
                Position = region.Hub, LocalPosition = frame.ToLocal(region.Hub)
            }).ToArray();
            var entities = Enumerable.Range(0, 100).Select(i => new EntityState {
                EntityId = "npc-" + i, Kind = EntityKind.Evacuee, RegionId = region.Id, FrameId = frame.FrameId,
                Position = region.Hub, LocalPosition = frame.ToLocal(region.Hub)
            }).ToList();
            entities.Add(new EntityState { EntityId = "fixture-door", Kind = EntityKind.Equipment,
                RequiredRoleId = "role-01", RegionId = region.Id, FrameId = frame.FrameId,
                Position = region.Hub, LocalPosition = frame.ToLocal(region.Hub) });
            entities.AddRange(Enumerable.Range(0, 2).Select(i => new EntityState {
                EntityId = "incident-" + i, Kind = EntityKind.Incident, RegionId = region.Id,
                FrameId = frame.FrameId, Position = region.Hub, LocalPosition = frame.ToLocal(region.Hub)
            }));
            return new WorldState { SchemaVersion = 2, WorldId = "synthetic-world", ShiftId = "synthetic-shift",
                SpatialProfileId = world.ProfileId, Frames = world.Frames.Select(f => f.Copy()).ToArray(),
                Participants = participants, Entities = entities.ToArray() };
        }
        private static WorldCommand Command(WorldState world, int actor, string id, CommandKind kind,
            string target = "", long revision = 0, string argument = "") => new WorldCommand {
                WorldId = world.WorldId, ShiftId = world.ShiftId, ParticipantId = "p" + actor,
                TeamId = world.Participants[actor].TeamId, CommandId = id, Kind = kind, TargetId = target,
                ExpectedRevision = revision, Argument = argument
            };

        [Test]
        public void CommittedWorldAndSimulationProfilesValidateTogether()
        {
            var world = World(); var profile = Profile(); profile.Validate(world);
            Assert.That(world.Regions.Length, Is.EqualTo(13)); Assert.That(world.Portals.Length, Is.EqualTo(12));
            Assert.That(profile.NpcCount, Is.EqualTo(100)); Assert.That(profile.Schedule.MaximumActive, Is.EqualTo(2));
            Assert.That(profile.NpcSpawnRegions, Is.EquivalentTo(world.Regions.Select(r => r.Id)));
            Assert.That(profile.Schedule.Choices.Select(c => c.Kind).Distinct().Count(), Is.EqualTo(6));
        }

        [Test]
        public void AllRegionPairsHaveRoutesAndEachClosedTreeEdgePartitionsTheWorld()
        {
            var world = World(); world.Validate();
            foreach (var from in world.Regions) foreach (var to in world.Regions) {
                var route = world.Route(from.Id, to.Id);
                Assert.That(route.First(), Is.EqualTo(from.Id)); Assert.That(route.Last(), Is.EqualTo(to.Id));
                Assert.That(route.Distinct().Count(), Is.EqualTo(route.Length));
                for (var i = 1; i < route.Length; i++) Assert.That(world.Neighbors(route[i - 1]), Does.Contain(route[i]));
            }
            foreach (var portal in world.Portals) {
                var closed = new HashSet<string> { portal.Id };
                Assert.That(world.Route(portal.From, portal.To, closed), Is.Empty);
                Assert.That(world.Route(portal.To, portal.From, closed), Is.Empty);
            }
        }

        [Test]
        public void EveryAuthoredPortalPreservesClearanceAndReversibleFrameCoordinates()
        {
            var world = World(); world.Validate();
            foreach (var portal in world.Portals) {
                var midpoint = new Point3((portal.FromPoint.X + portal.ToPoint.X) / 2,
                    (portal.FromPoint.Y + portal.ToPoint.Y) / 2, (portal.FromPoint.Z + portal.ToPoint.Z) / 2);
                Assert.That(portal.Contains(midpoint, .3f, out var along), Is.True, portal.Id);
                Assert.That(along, Is.EqualTo(.5).Within(.00001), portal.Id);
                Assert.That(portal.Contains(midpoint, portal.ClearWidth / 2 + .01f, out _), Is.False, portal.Id);
                foreach (var region in new[] { portal.From, portal.To }) {
                    var frame = world.Frame(world.Region(region).FrameId);
                    Assert.That(frame.ToWorld(frame.ToLocal(midpoint)).DistanceSquared(midpoint), Is.LessThan(.0001));
                }
            }
        }

        [Test]
        public void TwentyAuthenticatedIdentitiesRaceOneEquipmentRevisionAndReplayExactlyOnce()
        {
            var world = Roster(World()); var sink = new MemoryJournal();
            var config = new ServerSessionConfig { World = world, Tickets = world.Participants.Select(p => new AdmissionTicket {
                ParticipantId = p.ParticipantId, SecretSha256 = SessionAdmission.Digest("fixture-" + p.ParticipantId)
            }).ToArray() };
            var admission = new SessionAdmission(config);
            var shift = new AuthoritativeShift(world, sink, (a, e) => true);
            var commands = Enumerable.Range(0, 20).Select(i => Command(world, i, "race-" + i, CommandKind.Operate, "fixture-door")).ToArray();
            for (var i = 0; i < 20; i++) {
                Assert.That(admission.Allows(new JoinCredential { WorldId = world.WorldId, ShiftId = world.ShiftId,
                    ParticipantId = "p" + i, Secret = "fixture-p" + i }), Is.True);
                Assert.That(shift.Submit("p" + i, commands[i]).Code, Is.EqualTo(i == 0 ? CommandCode.Accepted : CommandCode.StaleTarget));
            }
            Assert.That(sink.Commits.Count, Is.EqualTo(1));
            var recoveredSink = new MemoryJournal();
            var recovered = AuthoritativeShift.Restore(world, recoveredSink, (a, e) => true);
            recovered.Replay(sink.Commits.Single());
            Assert.That(recovered.Paused, Is.True);
            Assert.That(recovered.Submit("p0", commands[0]).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(recoveredSink.Commits, Is.Empty, "A retry after restore must not append a second commit.");
            Assert.That(recovered.ExportCheckpoint().Entities.Single(e => e.EntityId == "fixture-door").Revision, Is.EqualTo(1));
        }

        [Test]
        public void HundredNpcOwnershipsAreExclusiveAndEveryExplicitHandoffSurvivesReplay()
        {
            var world = Roster(World()); var sink = new MemoryJournal();
            var shift = new AuthoritativeShift(world, sink, (a, e) => true);
            for (var i = 0; i < 100; i++) {
                var owner = i % 19; var next = (owner + 1) % 19; var target = "npc-" + i;
                Assert.That(shift.Submit("p" + owner, Command(world, owner, "claim-" + i, CommandKind.ClaimEvacuee, target)).Code, Is.EqualTo(CommandCode.Accepted));
                Assert.That(shift.Submit("p" + next, Command(world, next, "race-" + i, CommandKind.ClaimEvacuee, target, 1)).Code, Is.EqualTo(CommandCode.AlreadyClaimed));
                Assert.That(shift.Submit("p" + owner, Command(world, owner, "handoff-" + i, CommandKind.HandOffEvacuee, target, 1, "p" + next)).Code, Is.EqualTo(CommandCode.Accepted));
            }
            var restored = AuthoritativeShift.Restore(world, new MemoryJournal(), (a, e) => true);
            foreach (var commit in sink.Commits) restored.Replay(commit);
            var actual = restored.ExportCheckpoint().Entities.Where(e => e.Kind == EntityKind.Evacuee).ToArray();
            Assert.That(actual.Length, Is.EqualTo(100)); Assert.That(sink.Commits.Count, Is.EqualTo(200));
            for (var i = 0; i < 100; i++) {
                Assert.That(actual.Single(e => e.EntityId == "npc-" + i).LeaderId, Is.EqualTo("p" + ((i % 19 + 1) % 19)));
                Assert.That(actual.Single(e => e.EntityId == "npc-" + i).Revision, Is.EqualTo(2));
            }
        }

        [Test]
        public void ObservedSnapshotsRoundTripWithoutUnseenIncidentOrServerHistory()
        {
            var world = Roster(World()); var shift = new AuthoritativeShift(world, new MemoryJournal(), (a, e) => true);
            Assert.That(shift.Submit("p0", Command(world, 0, "discover", CommandKind.Discover, "incident-0")).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Submit("p0", Command(world, 0, "report", CommandKind.Report, "incident-0")).Code, Is.EqualTo(CommandCode.Accepted));
            for (var i = 0; i < 20; i++) {
                var observed = shift.Observe("p" + i);
                Assert.That(observed.Entities.Any(e => e.EntityId == "incident-1"), Is.False);
                Assert.That(observed.Entities.Any(e => e.EntityId == "incident-0"), Is.EqualTo(i == 0));
                Assert.That(observed.Reports.Length, Is.EqualTo(i < 10 ? 1 : 0));
                var json = JsonSerializer.Serialize(observed, Json);
                using (var parsed = JsonDocument.Parse(json)) {
                    foreach (var forbidden in new[] { "Receipts", "Participants", "SimulationCheckpoint", "Seed", "Schedule" })
                        Assert.That(parsed.RootElement.TryGetProperty(forbidden, out _), Is.False, forbidden);
                }
                var packets = SnapshotFragments.Split(i + 1, SnapshotPayload.Encode(json));
                var receiver = new SnapshotAssembler(); byte[] payload = null; long serial = 0;
                foreach (var packet in packets.Reverse()) receiver.Accept(packet, 0, out payload, out serial);
                Assert.That(serial, Is.EqualTo(i + 1)); Assert.That(SnapshotPayload.Decode(payload), Is.EqualTo(json));
                var decoded = JsonSerializer.Deserialize<ObservedState>(SnapshotPayload.Decode(payload), Json);
                Assert.That(decoded.ParticipantId, Is.EqualTo("p" + i));
                Assert.That(receiver.Accept(packets[0], 1, out _, out _), Is.EqualTo(FragmentResult.Ignored));
            }
        }

        [Test]
        public void CanonicalIncidentScheduleRestoresAndRecoversOneOfTwoWithoutResettingTheOther()
        {
            var profile = Profile(); var director = new IncidentDirector(profile.Schedule, (ulong)profile.Seed);
            for (var i = 0; i < profile.Schedule.FirstOnsetTick - 1; i++) Assert.That(director.AdvanceOne(), Is.Null);
            Assert.That(director.AdvanceOne(), Is.Not.Null);
            for (var i = 0; i <= profile.Schedule.MaximumQuietTicks; i++) director.AdvanceOne();
            Assert.That(director.Active.Count, Is.EqualTo(2));
            var restored = new IncidentDirector(profile.Schedule, 7); restored.Restore(director.ExportCheckpoint());
            var first = director.Active[0].Id; var second = director.Active[1].Id;
            director.Mitigate(first); restored.Mitigate(first); director.Complete(first); restored.Complete(first);
            Assert.That(director.Active.Select(e => e.Id), Does.Contain(second));
            for (var i = 0; i < 5000; i++) {
                director.AdvanceOne(); restored.AdvanceOne();
                Assert.That(director.Active.Count, Is.LessThanOrEqualTo(2));
            }
            Assert.That(restored.ExportCheckpoint(), Is.EqualTo(director.ExportCheckpoint()));
        }

        [Test]
        public void TwentyMemberVoiceGrantsIsolateTeamsAndReserveCommandPublishingForInstructor()
        {
            var world = Roster(World()); var issuer = new VoiceTokenIssuer("fixture-key", new string('s', 40), () => 1000);
            var teamRooms = new Dictionary<string, string>(); string commandRoom = null;
            foreach (var participant in world.Participants) {
                var team = issuer.Join(world.WorldId, world.ShiftId, "epoch-1", participant, "team", "ws://127.0.0.1:7880");
                var command = issuer.Join(world.WorldId, world.ShiftId, "epoch-1", participant, "command", "ws://127.0.0.1:7880");
                if (teamRooms.TryGetValue(participant.TeamId, out var existing)) Assert.That(team.Room, Is.EqualTo(existing));
                teamRooms[participant.TeamId] = team.Room;
                if (commandRoom != null) Assert.That(command.Room, Is.EqualTo(commandRoom)); commandRoom = command.Room;
                Assert.That(command.CanPublish, Is.EqualTo(participant.IsInstructor)); Assert.That(team.CanPublish, Is.True);
                Assert.That(command.Room, Is.Not.EqualTo(team.Room));
                var rotated = issuer.Join(world.WorldId, world.ShiftId, "epoch-2", participant, "team", "ws://127.0.0.1:7880");
                Assert.That(rotated.Room, Is.Not.EqualTo(team.Room));
            }
            Assert.That(teamRooms.Values.Distinct().Count(), Is.EqualTo(2));
        }
    }
}
