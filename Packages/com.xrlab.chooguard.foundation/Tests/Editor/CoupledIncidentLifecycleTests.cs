using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ChooGuard.Foundation.Multiplayer;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Tests
{
    [TestFixture]
    public sealed class CoupledIncidentLifecycleTests
    {
        private static readonly string[] ThirteenRegions = new[]
        {
            "rolling_stock_mainline",
            "rail_tracks_mainline",
            "rail_platforms_mainline",
            "rail_terminal_public",
            "station_concourse_2f",
            "station_hall_1f",
            "station_ticket_area",
            "forecourt_eurasia",
            "underground_connector",
            "underground_shopping_passage",
            "metro_concourse",
            "metro_platforms",
            "rolling_stock_metro"
        };

        private sealed class MemorySink : ICommitSink
        {
            public readonly List<ShiftCommit> Commits = new List<ShiftCommit>();
            public bool Fail;

            public void Append(ShiftCommit commit)
            {
                if (Fail) throw new IOException("Injected persistence failure");
                Commits.Add(commit.Copy());
            }
        }

        private static IncidentSchedule Create13RegionSchedule(int firstOnset = 10, int minQuiet = 5, int maxQuiet = 15)
        {
            var choices = new List<IncidentChoice>();
            for (var i = 0; i < ThirteenRegions.Length; i++)
            {
                var region = ThirteenRegions[i];
                var kind = (FoundationIncidentKind)(i % 6);
                choices.Add(new IncidentChoice
                {
                    Id = "incident-" + region,
                    ResourceKey = region,
                    Kind = kind
                });
            }

            return new IncidentSchedule
            {
                ProfileId = "thirteen-region-connected-v1",
                MaximumActive = 2,
                FirstOnsetTick = firstOnset,
                MinimumQuietTicks = minQuiet,
                MaximumQuietTicks = maxQuiet,
                Choices = choices.ToArray()
            };
        }

        private static WorldCommand MakeCommand(
            string participant,
            string team,
            CommandKind kind,
            string target = "",
            string commandId = "",
            long revision = 0,
            string argument = "")
        {
            return new WorldCommand
            {
                WorldId = "world-test",
                ShiftId = "shift-test",
                ParticipantId = participant,
                TeamId = team,
                CommandId = string.IsNullOrEmpty(commandId) ? Guid.NewGuid().ToString("N") : commandId,
                Kind = kind,
                TargetId = target,
                ExpectedRevision = revision,
                Argument = argument
            };
        }

        [Test]
        public void ThirteenRegionsCoveredByAuthoredIncidentScheduleAndDeterministicConcurrency()
        {
            var schedule = Create13RegionSchedule(firstOnset: 10, minQuiet: 5, maxQuiet: 10);
            Assert.That(schedule.Choices.Length, Is.EqualTo(13));
            Assert.That(schedule.Choices.Select(c => c.ResourceKey).Distinct().Count(), Is.EqualTo(13));

            var connectedProfilePath = "foundation/world/connected-world-profile.json";
            if (File.Exists(connectedProfilePath))
            {
                var worldDef = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(connectedProfilePath));
                var profileRegions = worldDef.Regions.Select(r => r.Id).OrderBy(x => x).ToArray();
                var expectedRegions = ThirteenRegions.OrderBy(x => x).ToArray();
                Assert.That(profileRegions, Is.EqualTo(expectedRegions), "Schedule must span exact 13 regions defined in connected-world-profile.json");
            }

            var director = new IncidentDirector(schedule, seed: 987654321);

            // Phase 1 verification: no countdown spoilers or early onsets prior to firstOnset
            for (var t = 0; t < 9; t++)
            {
                var early = director.AdvanceOne();
                Assert.That(early, Is.Null, "AdvanceOne must return null before FirstOnsetTick");
                Assert.That(director.Active, Is.Empty);
            }

            // Tick 10: First onset
            var first = director.AdvanceOne();
            Assert.That(first, Is.Not.Null, "Tick 10 reaches first onset");
            Assert.That(first.Id, Is.EqualTo("incident-1"));
            Assert.That(director.Active.Count, Is.EqualTo(1));
            var firstChoice = schedule.Choices.Single(c => c.Id == first.ChoiceId);
            Assert.That(ThirteenRegions.Contains(firstChoice.ResourceKey), Is.True);

            // Advance until second onset fires
            IncidentEpisode second = null;
            for (var i = 0; i < 50 && second == null; i++)
            {
                second = director.AdvanceOne();
            }

            Assert.That(second, Is.Not.Null, "Second incident must onset within MaximumQuietTicks");
            Assert.That(director.Active.Count, Is.EqualTo(2));
            var secondChoice = schedule.Choices.Single(c => c.Id == second.ChoiceId);

            // Verify strict resource-key mutual exclusion
            Assert.That(firstChoice.ResourceKey, Is.Not.EqualTo(secondChoice.ResourceKey),
                "Concurrent active incidents must occupy distinct resource keys");

            // Verify MaximumActive = 2 constraint: no third incident can onset
            for (var i = 0; i < 30; i++)
            {
                var blocked = director.AdvanceOne();
                Assert.That(blocked, Is.Null, "Cannot schedule a 3rd incident when MaximumActive (2) is reached");
                Assert.That(director.Active.Count, Is.EqualTo(2));
            }

            // Mitigate and complete the first incident
            Assert.Throws<ArgumentException>(() => director.Complete(first.Id), "Cannot complete unmitigated incident");
            director.Mitigate(first.Id);
            Assert.That(director.Active.Single(e => e.Id == first.Id).Mitigated, Is.True);
            director.Complete(first.Id);
            Assert.That(director.Active.Count, Is.EqualTo(1));

            // Now advancing can eventually spawn a replacement incident on an available resource key
            IncidentEpisode replacement = null;
            for (var i = 0; i < 50 && replacement == null; i++)
            {
                replacement = director.AdvanceOne();
            }

            Assert.That(replacement, Is.Not.Null, "Replacement incident should be scheduled once capacity freed");
            Assert.That(replacement.Id, Is.Not.EqualTo(first.Id), "Serial incident IDs must never be reused");
            Assert.That(director.Active.Count, Is.EqualTo(2));
        }

        [Test]
        public void CompleteFivePhaseEmergencyResponseLifecycle_WithFiveRolesAndInstructor()
        {
            var sink = new MemorySink();
            var occludedMap = new Dictionary<string, bool>(StringComparer.Ordinal);

            // Setup World with 13 regions, 5 distinct roles + 1 instructor
            var initial = new WorldState
            {
                WorldId = "world-test",
                ShiftId = "shift-test",
                Participants = new[]
                {
                    // role-01: Patrol / Initial Fire Fighting
                    new ParticipantState { ParticipantId = "actor-patrol", TeamId = "field-team", RoleId = "role-01", RegionId = "station_concourse_2f", Position = new Point3(0, 0, 0) },
                    // role-02: Evacuee Guidance
                    new ParticipantState { ParticipantId = "actor-evac", TeamId = "field-team", RoleId = "role-02", RegionId = "station_hall_1f", Position = new Point3(10, 0, 0) },
                    // role-02 backup for handoff
                    new ParticipantState { ParticipantId = "actor-evac-2", TeamId = "field-team", RoleId = "role-02", RegionId = "station_hall_1f", Position = new Point3(11, 0, 0) },
                    // role-03: Station Facility Control
                    new ParticipantState { ParticipantId = "actor-facility", TeamId = "facility-team", RoleId = "role-03", RegionId = "station_ticket_area", Position = new Point3(20, 0, 0) },
                    // role-04: Train / Platform Security
                    new ParticipantState { ParticipantId = "actor-train", TeamId = "security-team", RoleId = "role-04", RegionId = "rolling_stock_mainline", Position = new Point3(-50, 2, 15) },
                    // role-05: Incident Command / Communications
                    new ParticipantState { ParticipantId = "actor-command", TeamId = "command-team", RoleId = "role-05", RegionId = "station_concourse_2f", Position = new Point3(0, 0, 2) },
                    // instructor: Pause/Resume Shift & recovery authorization
                    new ParticipantState { ParticipantId = "instructor", TeamId = "command-team", RoleId = "instructor", IsInstructor = true, RegionId = "station_concourse_2f", Position = new Point3(0, 0, 4) }
                },
                Entities = new[]
                {
                    // role-01 equipment: Fire extinguisher / hydrant
                    new EntityState { EntityId = "equipment-fire-hydrant", RegionId = "station_concourse_2f", RequiredRoleId = "role-01", Kind = EntityKind.Equipment, Position = new Point3(1, 0, 0) },
                    // role-02 entities: Evacuee NPC and emergency exit gate
                    new EntityState { EntityId = "npc-evacuee-01", RegionId = "station_hall_1f", Kind = EntityKind.Evacuee, Position = new Point3(10.5f, 0, 0) },
                    new EntityState { EntityId = "equipment-emergency-gate", RegionId = "station_hall_1f", RequiredRoleId = "role-02", Kind = EntityKind.Equipment, Position = new Point3(11, 0, 0) },
                    // role-03 equipment: Ventilation fan & fire shutter
                    new EntityState { EntityId = "equipment-ventilation-fan", RegionId = "station_ticket_area", RequiredRoleId = "role-03", Kind = EntityKind.Equipment, Position = new Point3(21, 0, 0) },
                    // role-04 equipment: Train emergency stop switch
                    new EntityState { EntityId = "equipment-train-estop", RegionId = "rolling_stock_mainline", RequiredRoleId = "role-04", Kind = EntityKind.Equipment, Position = new Point3(-50.5f, 2, 15) },
                    // role-05 equipment: Incident command console / PA system
                    new EntityState { EntityId = "equipment-command-console", RegionId = "station_concourse_2f", RequiredRoleId = "role-05", Kind = EntityKind.Equipment, Position = new Point3(0.5f, 0, 2) },
                    // Authored Incident entity (initially inactive prior to onset)
                    new EntityState { EntityId = "incident-1", RegionId = "station_concourse_2f", Kind = EntityKind.Incident, Position = new Point3(2, 0, 0), Active = false }
                }
            };

            var shift = new AuthoritativeShift(initial, sink,
                (actor, target) => !occludedMap.TryGetValue(target.EntityId, out var occ) || !occ);

            // =========================================================================
            // PHASE 1: Normal Operation
            // =========================================================================
            Assert.That(shift.Paused, Is.False);
            Assert.That(shift.ExportCheckpoint().Sequence, Is.Zero);
            var obsPatrolPhase1 = shift.Observe("actor-patrol");
            Assert.That(obsPatrolPhase1.Entities.Any(e => e.Kind == EntityKind.Incident), Is.False,
                "Phase 1: No incidents visible during normal operation");

            // =========================================================================
            // PHASE 2: Unannounced Onset
            // =========================================================================
            // Incident onsets: authoritative update marks incident active
            var schedule = Create13RegionSchedule(firstOnset: 1, minQuiet: 5, maxQuiet: 10);
            var director = new IncidentDirector(schedule, 12345);
            var onset = director.AdvanceOne();
            Assert.That(onset, Is.Not.Null);
            Assert.That(onset.Id, Is.EqualTo("incident-1"));

            // Server-side physical publication activates incident in shift state
            var checkpointWithIncident = shift.ExportCheckpoint();
            var incidentEntity = checkpointWithIncident.Entities.Single(e => e.EntityId == "incident-1");
            incidentEntity.Active = true;
            shift = new AuthoritativeShift(checkpointWithIncident, sink,
                (actor, target) => !occludedMap.TryGetValue(target.EntityId, out var occ) || !occ);

            // Critical privacy check: incident is active on server, BUT undiscovered by any actor
            foreach (var p in initial.Participants)
            {
                var obs = shift.Observe(p.ParticipantId);
                Assert.That(obs.Entities.Any(e => e.EntityId == "incident-1"), Is.False,
                    "Phase 2: Active undiscovered incident MUST NEVER appear in ObservedState for " + p.ParticipantId);
            }

            // =========================================================================
            // PHASE 3: Physical Discovery & Incident Reporting
            // =========================================================================
            // 3a. OutOfReach test: actor-evac is at (10, 0, 0), incident is at (2, 0, 0)
            var distantDiscover = shift.Submit("actor-evac", MakeCommand("actor-evac", "field-team", CommandKind.Discover, target: "incident-1"));
            Assert.That(distantDiscover.Code, Is.EqualTo(CommandCode.OutOfReach));

            // 3b. Line-of-sight occlusion test
            occludedMap["incident-1"] = true; // Obstructed line of sight
            var occludedDiscover = shift.Submit("actor-patrol", MakeCommand("actor-patrol", "field-team", CommandKind.Discover, target: "incident-1"));
            Assert.That(occludedDiscover.Code, Is.EqualTo(CommandCode.Occluded));
            occludedMap["incident-1"] = false; // Unobstructed

            // 3c. Unobstructed discovery: actor-patrol is at (0, 0, 0), within interaction/interest radius
            var acceptedDiscover = shift.Submit("actor-patrol", MakeCommand("actor-patrol", "field-team", CommandKind.Discover, target: "incident-1", commandId: "cmd-discover-1"));
            Assert.That(acceptedDiscover.Code, Is.EqualTo(CommandCode.Accepted));

            // Now actor-patrol has discovered it
            var obsPatrolAfterDiscover = shift.Observe("actor-patrol");
            Assert.That(obsPatrolAfterDiscover.Entities.Any(e => e.EntityId == "incident-1"), Is.True,
                "Discovered incident must now appear in discovering actor's ObservedState");

            // But other actors (e.g. actor-evac, actor-command) STILL DO NOT see it
            Assert.That(shift.Observe("actor-evac").Entities.Any(e => e.EntityId == "incident-1"), Is.False);
            Assert.That(shift.Observe("actor-command").Entities.Any(e => e.EntityId == "incident-1"), Is.False);

            // 3d. Team Reporting: actor-patrol reports to field-team
            var reportCmd = MakeCommand("actor-patrol", "field-team", CommandKind.Report, target: "incident-1", commandId: "cmd-report-1");
            var reportReceipt = shift.Submit("actor-patrol", reportCmd);
            Assert.That(reportReceipt.Code, Is.EqualTo(CommandCode.Accepted));

            // Teammate actor-evac receives the TeamReport in Reports list
            var obsEvacAfterReport = shift.Observe("actor-evac");
            Assert.That(obsEvacAfterReport.Reports.Length, Is.EqualTo(1));
            Assert.That(obsEvacAfterReport.Reports[0].EntityId, Is.EqualTo("incident-1"));

            // PRIVACY PRESERVATION GUARANTEE: TeamReport does NOT grant live world vision!
            Assert.That(obsEvacAfterReport.Entities.Any(e => e.EntityId == "incident-1"), Is.False,
                "Team reports MUST NOT grant live world vision to teammates");

            // Non-teammate (security-team) does not receive field-team report
            Assert.That(shift.Observe("actor-train").Reports, Is.Empty);

            // Acknowledge report by teammate
            var reportId = obsEvacAfterReport.Reports[0].ReportId;
            var ackReceipt = shift.Submit("actor-evac", MakeCommand("actor-evac", "field-team", CommandKind.AcknowledgeReport, target: reportId));
            Assert.That(ackReceipt.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Observe("actor-evac").Reports[0].AcknowledgedBy.Contains("actor-evac"), Is.True);

            // =========================================================================
            // PHASE 4: Role-Specific Mitigation Actions & Boundary Enforcement
            // =========================================================================

            // --- role-01 (Patrol / Initial Fire Fighting) ---
            // Move actor-evac near fire hydrant: in reach, but unauthorized role -> RoleDenied
            shift.SetServerPosition("actor-evac", "station_concourse_2f", new Point3(1.5f, 0, 0));
            var deniedHydrant = shift.Submit("actor-evac", MakeCommand("actor-evac", "field-team", CommandKind.Operate, target: "equipment-fire-hydrant"));
            Assert.That(deniedHydrant.Code, Is.EqualTo(CommandCode.RoleDenied));

            // Authorized role-01 operates fire hydrant -> Accepted
            var acceptHydrant = shift.Submit("actor-patrol", MakeCommand("actor-patrol", "field-team", CommandKind.Operate, target: "equipment-fire-hydrant", commandId: "cmd-hydrant"));
            Assert.That(acceptHydrant.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.ExportCheckpoint().Entities.Single(e => e.EntityId == "equipment-fire-hydrant").Revision, Is.EqualTo(1));

            // --- role-02 (Evacuee Guidance) ---
            // Move actor-patrol near emergency gate: in reach, but unauthorized role -> RoleDenied
            shift.SetServerPosition("actor-patrol", "station_hall_1f", new Point3(11.2f, 0, 0));
            var deniedGate = shift.Submit("actor-patrol", MakeCommand("actor-patrol", "field-team", CommandKind.Operate, target: "equipment-emergency-gate"));
            Assert.That(deniedGate.Code, Is.EqualTo(CommandCode.RoleDenied));

            // Authorized role-02 operates emergency gate -> Accepted
            shift.SetServerPosition("actor-evac", "station_hall_1f", new Point3(11, 0, 0));
            var acceptGate = shift.Submit("actor-evac", MakeCommand("actor-evac", "field-team", CommandKind.Operate, target: "equipment-emergency-gate", commandId: "cmd-gate"));
            Assert.That(acceptGate.Code, Is.EqualTo(CommandCode.Accepted));

            // Claim NPC evacuee by role-02
            shift.SetServerPosition("actor-evac", "station_hall_1f", new Point3(10.5f, 0, 0));
            var claimNpc = shift.Submit("actor-evac", MakeCommand("actor-evac", "field-team", CommandKind.ClaimEvacuee, target: "npc-evacuee-01", commandId: "cmd-claim-npc"));
            Assert.That(claimNpc.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.ExportCheckpoint().Entities.Single(e => e.EntityId == "npc-evacuee-01").LeaderId, Is.EqualTo("actor-evac"));

            // Double claim race -> AlreadyClaimed
            var doubleClaim = shift.Submit("actor-evac-2", MakeCommand("actor-evac-2", "field-team", CommandKind.ClaimEvacuee, target: "npc-evacuee-01", revision: 1));
            Assert.That(doubleClaim.Code, Is.EqualTo(CommandCode.AlreadyClaimed));

            // HandOffEvacuee to another authorized guide (actor-evac-2)
            var handoffNpc = shift.Submit("actor-evac", MakeCommand("actor-evac", "field-team", CommandKind.HandOffEvacuee, target: "npc-evacuee-01", commandId: "cmd-handoff", revision: 1, argument: "actor-evac-2"));
            Assert.That(handoffNpc.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.ExportCheckpoint().Entities.Single(e => e.EntityId == "npc-evacuee-01").LeaderId, Is.EqualTo("actor-evac-2"));

            // Unauthorized role (role-01) attempting handoff -> RoleDenied
            shift.SetServerPosition("actor-patrol", "station_hall_1f", new Point3(10.5f, 0, 0));
            var invalidHandoff = shift.Submit("actor-patrol", MakeCommand("actor-patrol", "field-team", CommandKind.HandOffEvacuee, target: "npc-evacuee-01", revision: 2, argument: "actor-evac"));
            Assert.That(invalidHandoff.Code, Is.EqualTo(CommandCode.RoleDenied));

            // --- role-03 (Station Facility Control) ---
            // Move actor-patrol near ventilation fan: in reach, but unauthorized role -> RoleDenied
            shift.SetServerPosition("actor-patrol", "station_ticket_area", new Point3(21.2f, 0, 0));
            var deniedVent = shift.Submit("actor-patrol", MakeCommand("actor-patrol", "field-team", CommandKind.Operate, target: "equipment-ventilation-fan"));
            Assert.That(deniedVent.Code, Is.EqualTo(CommandCode.RoleDenied));

            // Authorized role-03 operates ventilation fan -> Accepted
            var acceptVent = shift.Submit("actor-facility", MakeCommand("actor-facility", "facility-team", CommandKind.Operate, target: "equipment-ventilation-fan", commandId: "cmd-vent"));
            Assert.That(acceptVent.Code, Is.EqualTo(CommandCode.Accepted));

            // --- role-04 (Train / Platform Security) ---
            // Move actor-facility near train emergency stop: in reach, but unauthorized role -> RoleDenied
            shift.SetServerPosition("actor-facility", "rolling_stock_mainline", new Point3(-50.4f, 2, 15));
            var deniedTrainStop = shift.Submit("actor-facility", MakeCommand("actor-facility", "facility-team", CommandKind.Operate, target: "equipment-train-estop"));
            Assert.That(deniedTrainStop.Code, Is.EqualTo(CommandCode.RoleDenied));

            // Authorized role-04 operates train emergency stop -> Accepted
            var acceptTrainStop = shift.Submit("actor-train", MakeCommand("actor-train", "security-team", CommandKind.Operate, target: "equipment-train-estop", commandId: "cmd-train-stop"));
            Assert.That(acceptTrainStop.Code, Is.EqualTo(CommandCode.Accepted));

            // --- role-05 (Incident Command / Communications) ---
            // Move actor-patrol near command console: in reach, but unauthorized role -> RoleDenied
            shift.SetServerPosition("actor-patrol", "station_concourse_2f", new Point3(0.6f, 0, 2));
            var deniedCommand = shift.Submit("actor-patrol", MakeCommand("actor-patrol", "field-team", CommandKind.Operate, target: "equipment-command-console"));
            Assert.That(deniedCommand.Code, Is.EqualTo(CommandCode.RoleDenied));

            // Authorized role-05 operates command console -> Accepted
            var acceptCommand = shift.Submit("actor-command", MakeCommand("actor-command", "command-team", CommandKind.Operate, target: "equipment-command-console", commandId: "cmd-situation"));
            Assert.That(acceptCommand.Code, Is.EqualTo(CommandCode.Accepted));

            // --- instructor authorization ---
            // Non-instructor attempting PauseShift -> RoleDenied
            var deniedPause = shift.Submit("actor-command", MakeCommand("actor-command", "command-team", CommandKind.PauseShift, commandId: "cmd-illegal-pause"));
            Assert.That(deniedPause.Code, Is.EqualTo(CommandCode.RoleDenied));

            // Instructor pauses shift -> Accepted
            var acceptPause = shift.Submit("instructor", MakeCommand("instructor", "command-team", CommandKind.PauseShift, commandId: "cmd-instructor-pause"));
            Assert.That(acceptPause.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Paused, Is.True);

            // Regular command while paused -> ShiftPaused
            var rejectedWhilePaused = shift.Submit("actor-patrol", MakeCommand("actor-patrol", "field-team", CommandKind.Operate, target: "equipment-fire-hydrant", commandId: "cmd-hydrant-2", revision: 1));
            Assert.That(rejectedWhilePaused.Code, Is.EqualTo(CommandCode.ShiftPaused));

            // Non-instructor attempting ResumeShift -> RoleDenied
            var deniedResume = shift.Submit("actor-command", MakeCommand("actor-command", "command-team", CommandKind.ResumeShift, commandId: "cmd-illegal-resume"));
            Assert.That(deniedResume.Code, Is.EqualTo(CommandCode.RoleDenied));

            // Instructor resumes shift -> Accepted
            var acceptResume = shift.Submit("instructor", MakeCommand("instructor", "command-team", CommandKind.ResumeShift, commandId: "cmd-instructor-resume"));
            Assert.That(acceptResume.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.Paused, Is.False);

            // =========================================================================
            // PHASE 5: Completion & Recovery
            // =========================================================================
            // Attempting to complete unmitigated incident -> Exception
            Assert.Throws<ArgumentException>(() => director.Complete("incident-1"));

            // Mitigate incident
            director.Mitigate("incident-1");
            Assert.That(director.Active.Single(e => e.Id == "incident-1").Mitigated, Is.True);

            // Complete incident
            director.Complete("incident-1");
            Assert.That(director.Active.Any(e => e.Id == "incident-1"), Is.False);

            // Deactivate incident in shift state
            var recoveryState = shift.ExportCheckpoint();
            var restoredIncident = recoveryState.Entities.Single(e => e.EntityId == "incident-1");
            restoredIncident.Active = false;
            restoredIncident.Revision++;
            shift = new AuthoritativeShift(recoveryState, sink, (a, t) => true);

            // Verify completed incident cannot be discovered or operated anymore
            var discoverCompleted = shift.Submit("actor-patrol", MakeCommand("actor-patrol", "field-team", CommandKind.Discover, target: "incident-1"));
            Assert.That(discoverCompleted.Code, Is.EqualTo(CommandCode.UnknownTarget));
        }

        [Test]
        public void StrictObservationPrivacy_OpticalDepthAndTeamIsolation()
        {
            // Verify SmokeOpticalField depth threshold enforcement
            // Optical depth limit in FoundationSimulationProfile is 3.0
            const double opticalDepthLimit = 3.0;

            // Define a smoke cell around (0, 0, 0) of size 10x10x4
            var clearCell = new SmokeCell
            {
                Id = "cell-clear",
                CenterX = 0, CenterY = 0, CenterZ = 0,
                WidthM = 10, DepthM = 10, BoundingHeightM = 4,
                InterfaceHeightAboveMinFloorM = 2,
                UpperExtinctionPerM = 0.05, // Light smoke: optical depth over 4m ray = 0.2 <= 3.0
                LowerExtinctionPerM = 0.01
            };

            var denseCell = new SmokeCell
            {
                Id = "cell-dense",
                CenterX = 0, CenterY = 0, CenterZ = 0,
                WidthM = 10, DepthM = 10, BoundingHeightM = 4,
                InterfaceHeightAboveMinFloorM = 0.5,
                UpperExtinctionPerM = 2.5, // Heavy smoke: optical depth over 4m ray = 10.0 > 3.0
                LowerExtinctionPerM = 1.0
            };

            var clearField = new SmokeOpticalField(new[] { clearCell });
            var denseField = new SmokeOpticalField(new[] { denseCell });

            var fromRay = new DoubleVector3(-1, 1, 0);
            var toRay = new DoubleVector3(1, 1, 0);

            var clearTrace = clearField.Trace(fromRay, toRay);
            Assert.That(clearTrace.OpticalDepth, Is.LessThanOrEqualTo(opticalDepthLimit),
                "Light smoke must allow optical transmission (depth <= 3.0)");

            var denseTrace = denseField.Trace(fromRay, toRay);
            Assert.That(denseTrace.OpticalDepth, Is.GreaterThan(opticalDepthLimit),
                "Dense smoke must obscure optical transmission (depth > 3.0)");

            // Wire this into AuthoritativeShift CanSee predicate
            var sink = new MemorySink();
            var initial = new WorldState
            {
                WorldId = "world-test",
                ShiftId = "shift-test",
                Participants = new[]
                {
                    new ParticipantState { ParticipantId = "scout-red", TeamId = "red-team", RoleId = "role-01", RegionId = "station_concourse_2f", Position = new Point3(-1, 1, 0) },
                    new ParticipantState { ParticipantId = "ally-red", TeamId = "red-team", RoleId = "role-02", RegionId = "station_concourse_2f", Position = new Point3(-1, 1, 1) },
                    new ParticipantState { ParticipantId = "rival-blue", TeamId = "blue-team", RoleId = "role-01", RegionId = "station_concourse_2f", Position = new Point3(-1, 1, 2) }
                },
                Entities = new[]
                {
                    new EntityState { EntityId = "incident-smoky-fire", RegionId = "station_concourse_2f", Kind = EntityKind.Incident, Position = new Point3(1, 1, 0), Active = true }
                }
            };

            var denseShift = new AuthoritativeShift(initial, sink,
                (actor, target) => denseField.Trace(new DoubleVector3(actor.Position.X, actor.Position.Y, actor.Position.Z),
                                                     new DoubleVector3(target.Position.X, target.Position.Y, target.Position.Z)).OpticalDepth <= opticalDepthLimit);

            // Attempting to discover through dense smoke fails with Occluded
            var discoverBlockedBySmoke = denseShift.Submit("scout-red", MakeCommand("scout-red", "red-team", CommandKind.Discover, target: "incident-smoky-fire"));
            Assert.That(discoverBlockedBySmoke.Code, Is.EqualTo(CommandCode.Occluded),
                "Heavy smoke exceeding optical depth limit must return CommandCode.Occluded");
            Assert.That(denseShift.Observe("scout-red").Entities.Any(e => e.EntityId == "incident-smoky-fire"), Is.False);

            // Under clear conditions, discovery succeeds
            var clearShift = new AuthoritativeShift(initial, sink,
                (actor, target) => clearField.Trace(new DoubleVector3(actor.Position.X, actor.Position.Y, actor.Position.Z),
                                                     new DoubleVector3(target.Position.X, target.Position.Y, target.Position.Z)).OpticalDepth <= opticalDepthLimit);

            var discoverClear = clearShift.Submit("scout-red", MakeCommand("scout-red", "red-team", CommandKind.Discover, target: "incident-smoky-fire", commandId: "cmd-clear-disc"));
            Assert.That(discoverClear.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(clearShift.Observe("scout-red").Entities.Any(e => e.EntityId == "incident-smoky-fire"), Is.True);

            // Reporting guarantees: ally-red receives report, rival-blue does not
            var reportCmd = MakeCommand("scout-red", "red-team", CommandKind.Report, target: "incident-smoky-fire", commandId: "cmd-clear-rep");
            Assert.That(clearShift.Submit("scout-red", reportCmd).Code, Is.EqualTo(CommandCode.Accepted));

            var allyObs = clearShift.Observe("ally-red");
            Assert.That(allyObs.Reports.Length, Is.EqualTo(1));
            Assert.That(allyObs.Entities.Any(e => e.EntityId == "incident-smoky-fire"), Is.False, "Ally does not get live vision via report");

            var rivalObs = clearShift.Observe("rival-blue");
            Assert.That(rivalObs.Reports, Is.Empty, "Other teams never receive isolated team reports");
            Assert.That(rivalObs.Entities.Any(e => e.EntityId == "incident-smoky-fire"), Is.False);
        }

        [Test]
        public void FullStateAndCheckpointRecovery_IncidentDirectorAndPhysicalState_ZeroDrift()
        {
            var schedule = Create13RegionSchedule(firstOnset: 5, minQuiet: 5, maxQuiet: 10);
            var seed = 0xABCD1234UL;
            var director = new IncidentDirector(schedule, seed);

            // Advance through two onsets
            for (var i = 0; i < 20; i++) director.AdvanceOne();
            Assert.That(director.Active.Count, Is.EqualTo(2));

            // Export checkpoint (CGID1 format)
            var cgid1 = director.ExportCheckpoint();
            Assert.That(cgid1.StartsWith("CGID1:", StringComparison.Ordinal), Is.True);

            // Tamper test: modifying a byte in base64 payload must be rejected by restore
            var rawBytes = Convert.FromBase64String(cgid1.Substring(6));
            rawBytes[10] ^= 0xFF;
            var tamperedCgid1 = "CGID1:" + Convert.ToBase64String(rawBytes);
            var victimDirector = new IncidentDirector(schedule, 9999);
            Assert.Throws<ArgumentException>(() => victimDirector.Restore(tamperedCgid1));

            // Restore into fresh instance
            var restoredDirector = new IncidentDirector(schedule, 9999);
            restoredDirector.Restore(cgid1);
            Assert.That(restoredDirector.ExportCheckpoint(), Is.EqualTo(cgid1),
                "Restored director checkpoint must match original bit-for-bit");
            Assert.That(restoredDirector.Tick, Is.EqualTo(director.Tick));
            Assert.That(restoredDirector.Active.Select(e => e.Id).SequenceEqual(director.Active.Select(e => e.Id)), Is.True);

            // Checkpoint within PhysicalWorldState (CGP1 format)
            var fire = new ZoneFireModel(new FireNetworkDefinition
            {
                Cells = new[] { new FireCellDefinition { Id = "test-cell", WidthM = 6, DepthM = 6, HeightM = 3.5 } }
            });

            var crowd = new CrowdMotionModel(
                new CrowdDefinition { ProfileId = "fixture", Spaces = new[] { new CrowdSpace { Id = "floor-space" } } },
                new[] { new CrowdAgent { Id = "body-1", ContactSpaceId = "floor-space", RegionId = "station_concourse_2f", FrameId = "world", SurfaceId = "floor-space" } });

            var definitionHash = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
            var physicalState = new PhysicalWorldState
            {
                DefinitionHash = definitionHash,
                SimulationTick = director.Tick,
                RandomState = 42,
                Fire = fire.ExportState(),
                CrowdCheckpoint = crowd.ExportCheckpoint(),
                DirectorState = cgid1
            };

            var proof = crowd.ExportCheckpointProof();
            var prepared = PhysicalCheckpoint.Prepare(physicalState, proof);
            var cgp1 = prepared.Encoded;
            Assert.That(cgp1.StartsWith("CGP1:", StringComparison.Ordinal), Is.True);

            // Decode and verify exact preservation
            var decodedPhysical = PhysicalCheckpoint.Decode(cgp1, definitionHash);
            Assert.That(decodedPhysical.SimulationTick, Is.EqualTo(director.Tick));
            Assert.That(decodedPhysical.DirectorState, Is.EqualTo(cgid1));

            var recoveredDirector = new IncidentDirector(schedule, 777);
            recoveredDirector.Restore(decodedPhysical.DirectorState);
            Assert.That(recoveredDirector.ExportCheckpoint(), Is.EqualTo(cgid1));

            // Continue both original and recovered directors in lockstep: must produce identical episodes
            director.Mitigate(director.Active[0].Id);
            director.Complete(director.Active[0].Id);
            recoveredDirector.Mitigate(recoveredDirector.Active[0].Id);
            recoveredDirector.Complete(recoveredDirector.Active[0].Id);

            for (var i = 0; i < 25; i++)
            {
                var ep1 = director.AdvanceOne();
                var ep2 = recoveredDirector.AdvanceOne();
                if (ep1 != null || ep2 != null)
                {
                    Assert.That(ep1, Is.Not.Null);
                    Assert.That(ep2, Is.Not.Null);
                    Assert.That(ep1.Id, Is.EqualTo(ep2.Id));
                    Assert.That(ep1.ChoiceId, Is.EqualTo(ep2.ChoiceId));
                }
            }

            Assert.That(director.ExportCheckpoint(), Is.EqualTo(recoveredDirector.ExportCheckpoint()),
                "Continuation from restored checkpoint must produce zero drift");
        }

        [Test]
        public void SimulationJournal_ReplaysCoupledActionsAcrossAllFiveRoles_DurableIntegrity()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "chooguard-coupled-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var hash = "9999999999999999999999999999999999999999999999999999999999999999";
                var fire = new ZoneFireModel(new FireNetworkDefinition
                {
                    Cells = new[] { new FireCellDefinition { Id = "cell-j", WidthM = 5, DepthM = 5, HeightM = 3 } }
                });
                var crowd = new CrowdMotionModel(
                    new CrowdDefinition { ProfileId = "j-prof", Spaces = new[] { new CrowdSpace { Id = "j-space" } } },
                    new[] { new CrowdAgent { Id = "j-agent", ContactSpaceId = "j-space", RegionId = "hall", FrameId = "world", SurfaceId = "j-space" } });

                string MakePhysical(long tick) => PhysicalCheckpoint.Encode(new PhysicalWorldState
                {
                    DefinitionHash = hash,
                    SimulationTick = tick,
                    Fire = fire.ExportState(),
                    CrowdCheckpoint = crowd.ExportCheckpoint()
                });

                var initial = new WorldState
                {
                    SchemaVersion = 3,
                    WorldId = "world-j",
                    ShiftId = "shift-j",
                    SpatialProfileId = "spatial-j",
                    SimulationDefinitionHash = hash,
                    SimulationCheckpoint = MakePhysical(0),
                    SimulationTick = 0,
                    Frames = new[] { new SpatialFrame { FrameId = "world" } },
                    Participants = new[]
                    {
                        new ParticipantState { ParticipantId = "p1", TeamId = "team-all", RoleId = "role-01", RegionId = "hall", Position = new Point3(1, 0, 0), LocalPosition = new Point3(1, 0, 0), ObservedIds = new[] { "incident-active" } },
                        new ParticipantState { ParticipantId = "p2", TeamId = "team-all", RoleId = "role-02", RegionId = "hall", Position = new Point3(2, 0, 0), LocalPosition = new Point3(2, 0, 0) },
                        new ParticipantState { ParticipantId = "p3", TeamId = "team-all", RoleId = "role-03", RegionId = "hall", Position = new Point3(3, 0, 0), LocalPosition = new Point3(3, 0, 0) },
                        new ParticipantState { ParticipantId = "p4", TeamId = "team-all", RoleId = "role-04", RegionId = "hall", Position = new Point3(4, 0, 0), LocalPosition = new Point3(4, 0, 0) },
                        new ParticipantState { ParticipantId = "p5", TeamId = "team-all", RoleId = "role-05", RegionId = "hall", Position = new Point3(5, 0, 0), LocalPosition = new Point3(5, 0, 0) },
                        new ParticipantState { ParticipantId = "cmd", TeamId = "team-all", RoleId = "instructor", IsInstructor = true, RegionId = "hall", Position = new Point3(0, 0, 0), LocalPosition = new Point3(0, 0, 0) }
                    },
                    Entities = new[]
                    {
                        new EntityState { EntityId = "eq-r1", RegionId = "hall", RequiredRoleId = "role-01", Kind = EntityKind.Equipment, Position = new Point3(1.2f, 0, 0), LocalPosition = new Point3(1.2f, 0, 0) },
                        new EntityState { EntityId = "npc-r2", RegionId = "hall", Kind = EntityKind.Evacuee, Position = new Point3(2.2f, 0, 0), LocalPosition = new Point3(2.2f, 0, 0) },
                        new EntityState { EntityId = "eq-r3", RegionId = "hall", RequiredRoleId = "role-03", Kind = EntityKind.Equipment, Position = new Point3(3.2f, 0, 0), LocalPosition = new Point3(3.2f, 0, 0) },
                        new EntityState { EntityId = "eq-r4", RegionId = "hall", RequiredRoleId = "role-04", Kind = EntityKind.Equipment, Position = new Point3(4.2f, 0, 0), LocalPosition = new Point3(4.2f, 0, 0) },
                        new EntityState { EntityId = "eq-r5", RegionId = "hall", RequiredRoleId = "role-05", Kind = EntityKind.Equipment, Position = new Point3(5.2f, 0, 0), LocalPosition = new Point3(5.2f, 0, 0) },
                        new EntityState { EntityId = "incident-active", RegionId = "hall", Kind = EntityKind.Incident, Position = new Point3(1, 0, 0), LocalPosition = new Point3(1, 0, 0), Active = true }
                    }
                };

                WorldCommand Cmd(string p, CommandKind k, string t = "", string id = "", long rev = 0, string arg = "") =>
                    new WorldCommand { WorldId = "world-j", ShiftId = "shift-j", ParticipantId = p, TeamId = "team-all", CommandId = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id, Kind = k, TargetId = t, ExpectedRevision = rev, Argument = arg };

                // Open journal and execute role actions
                using (var journal = new SimulationJournal(tempDir))
                {
                    var shift = journal.Open(initial, (a, e) => true);

                    // Role 1 operates eq-r1
                    Assert.That(shift.Submit("p1", Cmd("p1", CommandKind.Operate, "eq-r1", "act-r1")).Code, Is.EqualTo(CommandCode.Accepted));

                    // Role 2 claims npc
                    Assert.That(shift.Submit("p2", Cmd("p2", CommandKind.ClaimEvacuee, "npc-r2", "act-r2")).Code, Is.EqualTo(CommandCode.Accepted));

                    // Role 3 operates eq-r3
                    Assert.That(shift.Submit("p3", Cmd("p3", CommandKind.Operate, "eq-r3", "act-r3")).Code, Is.EqualTo(CommandCode.Accepted));

                    // Role 4 operates eq-r4
                    Assert.That(shift.Submit("p4", Cmd("p4", CommandKind.Operate, "eq-r4", "act-r4")).Code, Is.EqualTo(CommandCode.Accepted));

                    // Role 5 operates eq-r5 and reports incident
                    Assert.That(shift.Submit("p5", Cmd("p5", CommandKind.Operate, "eq-r5", "act-r5")).Code, Is.EqualTo(CommandCode.Accepted));
                    Assert.That(shift.Submit("p1", Cmd("p1", CommandKind.Report, "incident-active", "act-rep")).Code, Is.EqualTo(CommandCode.Accepted));

                    // Record physical boundary advancing simulation tick
                    var currentSim = shift.ReadSimulation();
                    currentSim.Tick = 1;
                    currentSim.Checkpoint = MakePhysical(1);

                    shift.ApplyServerSimulation(new ServerSimulationUpdate
                    {
                        Tick = 1,
                        DefinitionHash = hash,
                        Checkpoint = MakePhysical(1),
                        Frames = currentSim.Frames,
                        Actors = initial.Participants.Select(p => new ServerActorPose { ParticipantId = p.ParticipantId, Pose = new SpatialPose { RegionId = "hall", FrameId = "world", Position = p.Position, LocalPosition = p.LocalPosition } }).ToArray(),
                        Entities = currentSim.Entities
                    });

                    journal.RecordBoundary(shift.ReadSimulation());
                }

                // Re-open journal and verify exact recovery
                using (var journal = new SimulationJournal(tempDir))
                {
                    var recovered = journal.Open(initial, (a, e) => true);
                    var checkpoint = recovered.ExportCheckpoint();

                    Assert.That(checkpoint.Paused, Is.True, "Restored state must initialize Paused");
                    Assert.That(checkpoint.Sequence, Is.EqualTo(6), "6 approved commands must be restored");
                    Assert.That(checkpoint.SimulationTick, Is.EqualTo(1));

                    // Verify entity revisions and ownership
                    Assert.That(checkpoint.Entities.Single(e => e.EntityId == "eq-r1").Revision, Is.EqualTo(1));
                    Assert.That(checkpoint.Entities.Single(e => e.EntityId == "npc-r2").LeaderId, Is.EqualTo("p2"));
                    Assert.That(checkpoint.Entities.Single(e => e.EntityId == "eq-r3").Revision, Is.EqualTo(1));
                    Assert.That(checkpoint.Entities.Single(e => e.EntityId == "eq-r4").Revision, Is.EqualTo(1));
                    Assert.That(checkpoint.Entities.Single(e => e.EntityId == "eq-r5").Revision, Is.EqualTo(1));
                    Assert.That(checkpoint.Reports.Length, Is.EqualTo(1));

                    // Idempotent retry of previously logged command returns original receipt without incrementing sequence
                    var duplicate = recovered.Submit("p1", Cmd("p1", CommandKind.Operate, "eq-r1", "act-r1"));
                    Assert.That(duplicate.Code, Is.EqualTo(CommandCode.Accepted));
                    Assert.That(duplicate.Sequence, Is.EqualTo(1), "Duplicate submission must return original receipt sequence");

                    // Instructor resumes shift
                    var resumeReceipt = recovered.Submit("cmd", Cmd("cmd", CommandKind.ResumeShift, id: "act-resume"));
                    Assert.That(resumeReceipt.Code, Is.EqualTo(CommandCode.Accepted));
                    Assert.That(recovered.Paused, Is.False);

                    // Checkpoint durability
                    journal.Checkpoint(recovered.ExportCheckpoint());
                }

                // Ensure re-opening checkpointed journal produces identical durable state
                using (var journal = new SimulationJournal(tempDir))
                {
                    var reopened = journal.Open(initial, (a, e) => true);
                    Assert.That(reopened.ExportCheckpoint().Sequence, Is.EqualTo(7));
                    Assert.That(reopened.ExportCheckpoint().SimulationTick, Is.EqualTo(1));
                }
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }
    }
}
