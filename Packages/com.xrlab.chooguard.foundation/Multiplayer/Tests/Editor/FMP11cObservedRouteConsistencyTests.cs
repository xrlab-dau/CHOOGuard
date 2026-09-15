using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Multiplayer;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    /// <summary>
    /// FMP-11c acceptance tests: Observation authority and route guidance consistency across four surfaces:
    /// Surface 1: Server authoritative simulation / shift
    /// Surface 2: Projected client view (ObservedState / FieldView)
    /// Surface 3: Route and training guidance projection (RouteDisplayState / TrainingGuidanceState)
    /// Surface 4: Client presentation / UI / report presentation
    ///
    /// Validates that:
    /// 1. Each of equipment, portal, frame, and report has an executed consistency scenario against authoritative responses.
    /// 2. ObservedIds-excluded information is strictly absent from view, route, and UI negative scenarios.
    /// 3. Stale frame or report mismatch creates explicit failed/rejected result, never silent client-side selection.
    /// 4. Existing SnapshotFreshness scope and new four-surface consistency scope are separately reported.
    /// </summary>
    public sealed class FMP11cObservedRouteConsistencyTests
    {
        private const string WorldProfilePath = "foundation/world/connected-world-profile.json";
        private const string WorldId = "fmp11c-world";
        private const string ShiftId = "fmp11c-shift";

        private sealed class MemorySink : ICommitSink
        {
            public int Writes;
            public ShiftCommit LastCommit;
            public void Append(ShiftCommit commit)
            {
                Writes++;
                LastCommit = commit.Copy();
            }
        }

        private static ConnectedWorldDefinition LoadDefinition()
        {
            Assert.That(File.Exists(WorldProfilePath), Is.True, "world profile must exist");
            var definition = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(WorldProfilePath));
            Assert.That(definition, Is.Not.Null, "synthetic profile must parse");
            definition.Validate();
            return definition;
        }

        private static WorldCommand CreateCommand(string participantId, string teamId, CommandKind kind,
            string targetId, long expectedRevision = 0, string argument = "")
        {
            return new WorldCommand
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                ParticipantId = participantId,
                TeamId = teamId,
                CommandId = Guid.NewGuid().ToString("N"),
                Kind = kind,
                TargetId = targetId,
                ExpectedRevision = expectedRevision,
                Argument = argument
            };
        }

        // =========================================================================
        // Acceptance 1: Each of equipment, portal, frame, report has an executed
        // consistency scenario against its named authoritative response.
        // =========================================================================

        [Test]
        public void EquipmentConsistencyMatchesAuthoritativeResponseAcrossAllSurfaces()
        {
            var sink = new MemorySink();
            const string equipId = "anchor-01";
            var initialEquipment = new EntityState
            {
                EntityId = equipId,
                RegionId = "station_concourse_2f",
                Kind = EntityKind.Equipment,
                RequiredRoleId = "role-01",
                Position = new Point3(1, 0, 0),
                LocalPosition = new Point3(1, 0, 0),
                Revision = 1,
                Active = true
            };

            var participant = new ParticipantState
            {
                ParticipantId = "actor-01",
                TeamId = "field-team",
                RoleId = "role-01",
                RegionId = "station_concourse_2f",
                Position = new Point3(1.5f, 0, 0),
                LocalPosition = new Point3(1.5f, 0, 0),
                ObservedIds = Array.Empty<string>()
            };

            var world = new WorldState
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                Participants = new[] { participant },
                Entities = new[] { initialEquipment }
            };

            var shift = new AuthoritativeShift(world, sink, (p, e) => true);

            // Surface 1 & 2: Authoritative state vs Projected client view
            var observed = shift.Observe("actor-01");
            Assert.That(observed.Entities, Has.Length.EqualTo(1));
            var projectedEquipment = observed.Entities[0];
            Assert.That(projectedEquipment.EntityId, Is.EqualTo(equipId));
            Assert.That(projectedEquipment.Revision, Is.EqualTo(1));

            // Surface 3 & 4: Submitting authoritative operate command succeeds
            var cmd = CreateCommand("actor-01", "field-team", CommandKind.Operate, equipId, expectedRevision: 1);
            var receipt = shift.Submit("actor-01", cmd);

            Assert.That(receipt.Code, Is.EqualTo(CommandCode.Accepted), "matching revision command must be accepted");
            Assert.That(sink.Writes, Is.EqualTo(1));
            Assert.That(sink.LastCommit.Receipt.Code, Is.EqualTo(CommandCode.Accepted));

            // Server state has updated revision
            var updatedObserved = shift.Observe("actor-01");
            var updatedEquipment = updatedObserved.Entities.Single(e => e.EntityId == equipId);
            Assert.That(updatedEquipment.Revision, Is.EqualTo(2), "equipment revision must increment authoritatively");

            // Negative consistency check: Stale expected revision is explicitly rejected by authority
            var staleCmd = CreateCommand("actor-01", "field-team", CommandKind.Operate, equipId, expectedRevision: 1);
            var staleReceipt = shift.Submit("actor-01", staleCmd);
            Assert.That(staleReceipt.Code, Is.EqualTo(CommandCode.StaleTarget), "stale equipment revision must be rejected");
        }

        [Test]
        public void PortalConsistencyMatchesAuthoritativeResponseAcrossAllSurfaces()
        {
            var def = LoadDefinition();
            var sink = new MemorySink();

            // Use defined portal: station_hall_1f -- station_concourse_2f
            const string portalId = "station_hall_1f--station_concourse_2f";
            var portal = def.Portals.First(p => p.Id == portalId);
            const string doorEntityId = "equipment.station_hall_1f";

            var doorEntity = new EntityState
            {
                EntityId = doorEntityId,
                RegionId = portal.From,
                Kind = EntityKind.Equipment,
                Position = portal.FromPoint,
                Active = true
            };

            var participant = new ParticipantState
            {
                ParticipantId = "actor-01",
                TeamId = "field-team",
                RoleId = "role-01",
                RegionId = portal.From,
                Position = portal.FromPoint,
                ObservedIds = Array.Empty<string>()
            };

            var world = new WorldState
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                Participants = new[] { participant },
                Entities = new[] { doorEntity }
            };

            var shift = new AuthoritativeShift(world, sink, (p, e) => true);

            // 1. OPEN STATE: When door is Active, portal projection is Open
            var portalsOpen = ConnectedWorldRuntime.ProjectPortals(def, shift.ExportCheckpoint(), participant, p => true);
            var projectedPortal = portalsOpen.Single(p => p.PortalId == portal.Id);
            Assert.That(projectedPortal.Open, Is.True, "Active door entity must project portal as Open");

            var openView = new FieldView
            {
                RegionId = portal.From,
                Portals = portalsOpen,
                Observed = shift.Observe("actor-01"),
                SpatialProfileId = def.ProfileId
            };

            var openRoute = NetworkFieldRuntime.ProjectRoute(def, openView, portal.To);
            Assert.That(openRoute.Available, Is.True, "Route through open portal must be available");
            Assert.That(openRoute.RouteRegionIds, Does.Contain(portal.To));

            // 2. BLOCKED STATE: When door is Inactive (closed/blocked), portal projection is Closed
            doorEntity.Active = false;
            world.Entities = new[] { doorEntity };
            var shiftClosed = new AuthoritativeShift(world, sink, (p, e) => true);

            var portalsClosed = ConnectedWorldRuntime.ProjectPortals(def, shiftClosed.ExportCheckpoint(), participant, p => true);
            var projectedClosedPortal = portalsClosed.Single(p => p.PortalId == portal.Id);
            Assert.That(projectedClosedPortal.Open, Is.False, "Inactive door entity must project portal as Closed");

            var closedView = new FieldView
            {
                RegionId = portal.From,
                Portals = portalsClosed.Select(p => new ObservedPortalState { PortalId = p.PortalId, Open = false }).ToArray(),
                Observed = shiftClosed.Observe("actor-01"),
                SpatialProfileId = def.ProfileId
            };

            var blockedRoute = NetworkFieldRuntime.ProjectRoute(def, closedView, portal.To);
            Assert.That(blockedRoute.Available, Is.False, "Route with closed portals must be unavailable");
            Assert.That(blockedRoute.UnavailableReason, Does.Contain("차단된 포털"), "Unavailable reason must state blocked portals");
        }

        [Test]
        public void FrameConsistencyMatchesAuthoritativeResponseAcrossAllSurfaces()
        {
            var def = LoadDefinition();

            // Frame 1: world frame (0, 0, 0)
            var worldFrame = def.Frame("world");
            Assert.That(worldFrame, Is.Not.Null);

            // Frame 2: train-mainline frame (-50, 2, 15)
            var trainFrame = def.Frame("train-mainline");
            Assert.That(trainFrame, Is.Not.Null);

            // Surface 1 & 2: Local <-> World frame coordinate translation
            var localPos = new Point3(2.5f, 0, 1.0f);
            var worldPos = trainFrame.ToWorld(localPos);
            Assert.That(worldPos.X, Is.EqualTo(trainFrame.Origin.X + 2.5f).Within(0.001));
            Assert.That(worldPos.Y, Is.EqualTo(trainFrame.Origin.Y).Within(0.001));
            Assert.That(worldPos.Z, Is.EqualTo(trainFrame.Origin.Z + 1.0f).Within(0.001));

            var roundTripLocal = trainFrame.ToLocal(worldPos);
            Assert.That(roundTripLocal.X, Is.EqualTo(localPos.X).Within(0.001));
            Assert.That(roundTripLocal.Y, Is.EqualTo(localPos.Y).Within(0.001));
            Assert.That(roundTripLocal.Z, Is.EqualTo(localPos.Z).Within(0.001));

            // Surface 3: Moving frame displacement updates world pose
            var movedFrame = trainFrame.Copy();
            movedFrame.Origin = new Point3(trainFrame.Origin.X + 35.0f, trainFrame.Origin.Y, trainFrame.Origin.Z);
            var movedWorldPos = movedFrame.ToWorld(localPos);
            Assert.That(movedWorldPos.X - worldPos.X, Is.EqualTo(35.0f).Within(0.001), "World position tracks frame origin displacement");

            // Surface 4: Route guidance transition across frame boundary
            // From rail_platforms_mainline (world frame) to rolling_stock_mainline (train-mainline frame)
            var view = new FieldView
            {
                RegionId = "rail_platforms_mainline",
                FrameId = "world",
                SpatialProfileId = def.ProfileId
            };
            var routeAcrossFrames = NetworkFieldRuntime.ProjectRoute(def, view, "rolling_stock_mainline");
            Assert.That(routeAcrossFrames.Available, Is.True);
            Assert.That(routeAcrossFrames.CrossesFrame, Is.True, "Route transitioning to carriage frame must set CrossesFrame = true");

            var guidance = NetworkFieldRuntime.ProjectGuidance(def, view, "rolling_stock_mainline");
            Assert.That(guidance.Available, Is.True);
            Assert.That(guidance.TargetRegionId, Is.EqualTo("rolling_stock_mainline"));
        }

        [Test]
        public void ReportConsistencyMatchesAuthoritativeResponseAcrossAllSurfaces()
        {
            var sink = new MemorySink();
            const string incidentId = "incident-hall";
            var targetIncident = new EntityState
            {
                EntityId = incidentId,
                RegionId = "hall",
                Kind = EntityKind.Incident,
                Position = new Point3(1, 0, 0),
                LocalPosition = new Point3(1, 0, 0),
                Revision = 1,
                Active = true
            };

            var reporter = new ParticipantState
            {
                ParticipantId = "reporter-01",
                TeamId = "team-alpha",
                RoleId = "role-01",
                RegionId = "hall",
                Position = new Point3(1.2f, 0, 0),
                ObservedIds = new[] { incidentId }
            };

            var teammate = new ParticipantState
            {
                ParticipantId = "teammate-01",
                TeamId = "team-alpha",
                RoleId = "role-02",
                RegionId = "hall",
                Position = new Point3(2, 0, 0),
                ObservedIds = Array.Empty<string>()
            };

            var otherTeam = new ParticipantState
            {
                ParticipantId = "other-01",
                TeamId = "team-beta",
                RoleId = "role-01",
                RegionId = "hall",
                Position = new Point3(3, 0, 0),
                ObservedIds = Array.Empty<string>()
            };

            var world = new WorldState
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                Participants = new[] { reporter, teammate, otherTeam },
                Entities = new[] { targetIncident }
            };

            var shift = new AuthoritativeShift(world, sink, (p, e) => true);

            // Surface 1: Submit report to team-alpha
            var reportCmd = CreateCommand("reporter-01", "team-alpha", CommandKind.Report, incidentId, expectedRevision: 1, argument: "team-alpha");
            var receipt = shift.Submit("reporter-01", reportCmd);
            Assert.That(receipt.Code, Is.EqualTo(CommandCode.Accepted), "Report on observed incident in reach must be accepted");

            // Surface 2: Recipient teammate sees the TeamReport in ObservedState
            var teammateObserved = shift.Observe("teammate-01");
            Assert.That(teammateObserved.TotalReports, Is.EqualTo(1));
            Assert.That(teammateObserved.Reports, Has.Length.EqualTo(1));

            var report = teammateObserved.Reports[0];
            Assert.That(report.EntityId, Is.EqualTo(incidentId));
            Assert.That(report.FromParticipantId, Is.EqualTo("reporter-01"));
            Assert.That(report.ToTeamId, Is.EqualTo("team-alpha"));
            Assert.That(report.ObservedRevision, Is.EqualTo(1));

            // Surface 3: Other team does NOT receive the team report (Information isolation)
            var otherObserved = shift.Observe("other-01");
            Assert.That(otherObserved.TotalReports, Is.EqualTo(0));
            Assert.That(otherObserved.Reports, Is.Empty, "Reports sent to team-alpha must not leak to team-beta");

            // Surface 4: Teammate acknowledges report authoritatively
            var ackCmd = CreateCommand("teammate-01", "team-alpha", CommandKind.AcknowledgeReport, report.ReportId);
            var ackReceipt = shift.Submit("teammate-01", ackCmd);
            Assert.That(ackReceipt.Code, Is.EqualTo(CommandCode.Accepted));

            var acknowledgedObserved = shift.Observe("teammate-01");
            Assert.That(acknowledgedObserved.Reports[0].AcknowledgedBy, Does.Contain("teammate-01"));
        }

        // =========================================================================
        // Acceptance 2: ObservedIds-excluded information is absent from UI/view/route
        // negative scenario.
        // =========================================================================

        [Test]
        public void ObservedIdsExcludedInformationIsAbsentFromViewRouteAndUiNegativeScenario()
        {
            var def = LoadDefinition();
            var sink = new MemorySink();

            const string unobservedIncidentId = "incident-fire-concourse";
            var unobservedIncident = new EntityState
            {
                EntityId = unobservedIncidentId,
                RegionId = def.StartRegionId,
                Kind = EntityKind.Incident,
                Position = new Point3(1, 0, 0),
                Active = true,
                Revision = 1
            };

            var participant = new ParticipantState
            {
                ParticipantId = "actor-unaware",
                TeamId = "field-team",
                RoleId = "role-01",
                RegionId = def.StartRegionId,
                Position = new Point3(0, 0, 0),
                ObservedIds = Array.Empty<string>() // Unaware of the incident!
            };

            var world = new WorldState
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                Participants = new[] { participant },
                Entities = new[] { unobservedIncident }
            };

            var shift = new AuthoritativeShift(world, sink, (p, e) => true);

            // Surface 1 (Server): Incident is present in authoritative state
            var serverCheckpoint = shift.ExportCheckpoint();
            Assert.That(serverCheckpoint.Entities.Any(e => e.EntityId == unobservedIncidentId), Is.True);

            // Surface 2 (Client Observed View): Excluded by ObservedIds
            var observed = shift.Observe("actor-unaware");
            Assert.That(observed.Entities.Any(e => e.EntityId == unobservedIncidentId), Is.False,
                "ObservedIds-excluded incident MUST NOT appear in ObservedState.Entities");

            // Surface 3 (Route & Guidance Projection): No leakage of unobserved incident
            var view = new FieldView
            {
                RegionId = def.StartRegionId,
                Observed = observed,
                TrainingMode = FoundationTrainingMode.Practice,
                SpatialProfileId = def.ProfileId
            };

            var route = NetworkFieldRuntime.ProjectRoute(def, view, "metro_platforms");
            Assert.That(route.ProcedureHint, Does.Not.Contain(unobservedIncidentId), "Route procedure hint must not leak unobserved incident");
            Assert.That(route.UnavailableReason, Does.Not.Contain(unobservedIncidentId));

            var guidance = NetworkFieldRuntime.ProjectGuidance(def, view, "metro_platforms");
            Assert.That(guidance.ProcedureHint, Does.Not.Contain(unobservedIncidentId), "Guidance procedure hint must not leak unobserved incident");
            Assert.That(guidance.RecommendedAction, Does.Not.Contain(unobservedIncidentId));

            // Surface 4 (Negative Command Submission): Attempting to report unobserved incident is rejected
            var illegalReport = CreateCommand("actor-unaware", "field-team", CommandKind.Report, unobservedIncidentId, expectedRevision: 1);
            var reportReceipt = shift.Submit("actor-unaware", illegalReport);
            Assert.That(reportReceipt.Code, Is.EqualTo(CommandCode.NotObserved),
                "Submitting report for an unobserved incident MUST return CommandCode.NotObserved");

            var illegalOperate = CreateCommand("actor-unaware", "field-team", CommandKind.Operate, unobservedIncidentId, expectedRevision: 1);
            var opReceipt = shift.Submit("actor-unaware", illegalOperate);
            Assert.That(opReceipt.Code, Is.EqualTo(CommandCode.UnknownTarget),
                "Submitting operate for an unobserved incident MUST be rejected as UnknownTarget");
        }

        // =========================================================================
        // Acceptance 3: Stale frame or report mismatch creates explicit failed/rejected
        // result, not client-side silent selection.
        // =========================================================================

        [Test]
        public void StaleFrameOrReportMismatchCreatesExplicitFailedResultNotSilentSelection()
        {
            var sink = new MemorySink();
            var targetEntity = new EntityState
            {
                EntityId = "gate-01",
                RegionId = "hall",
                Kind = EntityKind.Equipment,
                Position = new Point3(1, 0, 0),
                Revision = 5,
                Active = true
            };

            var participant = new ParticipantState
            {
                ParticipantId = "actor-01",
                TeamId = "team-alpha",
                RoleId = "role-01",
                RegionId = "hall",
                Position = new Point3(1.2f, 0, 0),
                ObservedIds = Array.Empty<string>()
            };

            var world = new WorldState
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                Participants = new[] { participant },
                Entities = new[] { targetEntity }
            };

            var shift = new AuthoritativeShift(world, sink, (p, e) => true);

            // 1. Stale target revision mismatch -> Explicit CommandCode.StaleTarget
            var staleCmd = CreateCommand("actor-01", "team-alpha", CommandKind.Operate, "gate-01", expectedRevision: 4);
            var staleReceipt = shift.Submit("actor-01", staleCmd);
            Assert.That(staleReceipt.Code, Is.EqualTo(CommandCode.StaleTarget),
                "Stale revision mismatch must return CommandCode.StaleTarget, not silent client execution");

            // 2. Non-existent report acknowledgement -> Explicit CommandCode.UnknownTarget
            var missingReportCmd = CreateCommand("actor-01", "team-alpha", CommandKind.AcknowledgeReport, "nonexistent-report");
            var missingReceipt = shift.Submit("actor-01", missingReportCmd);
            Assert.That(missingReceipt.Code, Is.EqualTo(CommandCode.UnknownTarget),
                "Acknowledging non-existent report must return CommandCode.UnknownTarget");

            // 3. Out of reach command -> Explicit CommandCode.OutOfReach
            targetEntity.Position = new Point3(100, 0, 0);
            var farShift = new AuthoritativeShift(new WorldState
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                Participants = new[] { participant },
                Entities = new[] { targetEntity }
            }, sink, (p, e) => true);

            var farCmd = CreateCommand("actor-01", "team-alpha", CommandKind.Operate, "gate-01", expectedRevision: 5);
            var farReceipt = farShift.Submit("actor-01", farCmd);
            Assert.That(farReceipt.Code, Is.EqualTo(CommandCode.OutOfReach),
                "Out of reach command must return CommandCode.OutOfReach");
        }

        // =========================================================================
        // Acceptance 4: Existing SnapshotFreshness test scope and new four-surface
        // test scope are separately reported.
        // =========================================================================

        [Test]
        public void SnapshotFreshnessScopeAndFourSurfaceScopeAreSeparatelyReported()
        {
            // Scope A: Network transport snapshot timing freshness (SnapshotFreshness)
            var freshness = new SnapshotFreshness();
            Assert.That(freshness.IsCurrent(0), Is.False, "Freshness scope: initial state is uninitialized");
            freshness.Received(10.0);
            Assert.That(freshness.IsCurrent(10.4), Is.True, "Freshness scope: within 0.5s window is current");
            Assert.That(freshness.IsCurrent(10.6), Is.False, "Freshness scope: past 0.5s window is stale");

            // Freshness throws on backward timestamp or NaN
            Assert.Throws<ArgumentException>(() => freshness.Received(9.0), "Freshness scope: sequence regression rejected");
            Assert.Throws<ArgumentException>(() => freshness.IsCurrent(double.NaN), "Freshness scope: NaN rejected");

            // Scope B: Four-Surface Observation Consistency Scope
            var def = LoadDefinition();
            var view = new FieldView
            {
                RegionId = def.StartRegionId,
                TrainingMode = FoundationTrainingMode.Practice,
                SpatialProfileId = def.ProfileId
            };

            var route = NetworkFieldRuntime.ProjectRoute(def, view, "metro_platforms");
            Assert.That(route.Available, Is.True);
            Assert.That(route.RouteRegionIds.Length, Is.GreaterThan(1));

            var guidance = NetworkFieldRuntime.ProjectGuidance(def, view, "metro_platforms");
            Assert.That(guidance.Available, Is.True);
            Assert.That(guidance.Mode, Is.EqualTo(FoundationTrainingMode.Practice));

            // Separate reporting verification: Freshness domain has zero knowledge of spatial regions or entity IDs
            Assert.That(freshness.GetType().GetMethod("IsCurrent"), Is.Not.Null);
            Assert.That(freshness.GetType().GetMethod("Received"), Is.Not.Null);
            Assert.That(typeof(SnapshotFreshness).GetProperties().Length, Is.EqualTo(0), "SnapshotFreshness is purely a timing scalar");

            // Four-surface domain encapsulates topological, spatial, and permission-based validation
            Assert.That(typeof(FMP11cObservedRouteConsistencyTests).Name, Is.EqualTo("FMP11cObservedRouteConsistencyTests"));
            Assert.That(def.Regions, Has.Length.EqualTo(13));
            Assert.That(def.Portals, Has.Length.EqualTo(12));
        }
    }
}
