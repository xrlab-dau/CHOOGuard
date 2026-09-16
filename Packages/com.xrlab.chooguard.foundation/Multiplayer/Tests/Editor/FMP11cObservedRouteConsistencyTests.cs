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
    /// FMP-11c acceptance tests: observation authority and route guidance consistency.
    ///
    /// Three surfaces are exercised by this file, all of them machine surfaces this assembly can
    /// reach out of the editor:
    ///   Surface 1: server authoritative simulation / shift (AuthoritativeShift).
    ///   Surface 2: projected client view (ObservedState / FieldView / ObservedPortalState).
    ///   Surface 3: route and guidance projection (RouteDisplayState / TrainingGuidanceState).
    /// There is no UI surface here. This file references no panel or view type; the presentation
    /// layer that would consume Surface 3 is out of scope for this package and is NOT asserted.
    ///
    /// Validates that:
    /// 1. Each of equipment, portal, frame, and report has an executed consistency scenario against its named authoritative response.
    /// 2. ObservedIds-excluded information is absent from the projected view and from the route/guidance negative scenario.
    /// 3. A stale revision or an unacknowledgeable report creates an explicit failed/rejected result, never a silent client-side selection.
    /// 4. The existing SnapshotFreshness scope and this file's projection-consistency scope are separable by behaviour.
    ///
    /// Deliberately asserted gaps (listed as capability limits in the FMP-11c evidence record):
    /// the route projection never reconciles the client's observed portal set against the server's
    /// door state, so a stale observation is projected exactly as observed. That is pinned as
    /// behaviour by ObservedPortalDisagreementWithAuthoritativeDoorIsExposedNotReconciled rather
    /// than repaired, because no reconciliation seam exists in production.
    /// </summary>
    public sealed class FMP11cObservedRouteConsistencyTests
    {
        private const string WorldProfilePath = "foundation/world/connected-world-profile.json";
        private const string WorldId = "fmp11c-world";
        private const string ShiftId = "fmp11c-shift";
        private const string ConfluencePortalId = "rail_terminal_public--station_concourse_2f";

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

        /// <summary>Every door entity named by the profile, so that a projection which ignores the
        /// actor's region can still resolve each linked door instead of throwing on a missing id.</summary>
        private static EntityState[] DoorEntities(ConnectedWorldDefinition definition, bool active)
        {
            return definition.Portals.Where(p => p.LinkedDoorEntityIds.Length > 0)
                .SelectMany(p => p.LinkedDoorEntityIds.Select(id => new { Id = id, Region = p.From, Position = p.FromPoint }))
                .GroupBy(x => x.Id).Select(g => g.First())
                .Select(x => new EntityState
                {
                    EntityId = x.Id, RegionId = x.Region, Kind = EntityKind.Equipment,
                    Position = x.Position, Active = active, Revision = 1
                }).ToArray();
        }

        private static FieldView View(string regionId, string profileId, params ObservedPortalState[] portals)
        {
            return new FieldView
            {
                RegionId = regionId,
                Portals = portals,
                TrainingMode = FoundationTrainingMode.Practice,
                SpatialProfileId = profileId
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
                Portals = portalsClosed,
                Observed = shiftClosed.Observe("actor-01"),
                SpatialProfileId = def.ProfileId
            };

            var blockedRoute = NetworkFieldRuntime.ProjectRoute(def, closedView, portal.To);
            Assert.That(blockedRoute.Available, Is.False, "Route with closed portals must be unavailable");
            Assert.That(blockedRoute.UnavailableReason, Does.Contain("차단된 포털"), "Unavailable reason must state blocked portals");

            // 3. REGION SCOPING: the projection is scoped to the portals the actor's own region borders,
            // not merely to every portal inside the interest radius. rolling_stock_mainline sits 15 m
            // from rail_platforms_mainline, so two portal midpoints fall inside the radius while only
            // one of them touches the actor's region.
            var mainlineActor = new ParticipantState
            {
                ParticipantId = "actor-mainline",
                TeamId = "field-team",
                RoleId = "role-01",
                RegionId = "rolling_stock_mainline",
                Position = def.Region("rolling_stock_mainline").Hub,
                ObservedIds = Array.Empty<string>()
            };
            var mainlineWorld = new WorldState
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                Participants = new[] { mainlineActor },
                Entities = DoorEntities(def, true)
            };
            var mainlineShift = new AuthoritativeShift(mainlineWorld, sink, (p, e) => true);

            var inRadius = def.Portals.Where(p =>
                new Point3((p.FromPoint.X + p.ToPoint.X) / 2, (p.FromPoint.Y + p.ToPoint.Y) / 2,
                    (p.FromPoint.Z + p.ToPoint.Z) / 2).DistanceSquared(mainlineActor.Position) <= AuthoritativeShift.InterestRadius * AuthoritativeShift.InterestRadius)
                .Select(p => p.Id).ToArray();
            var bordering = def.Portals.Where(p => p.From == mainlineActor.RegionId || p.To == mainlineActor.RegionId)
                .Select(p => p.Id).ToArray();
            Assert.That(inRadius.Length, Is.GreaterThan(bordering.Length),
                "fixture guard: the interest radius must contain a portal the actor's region does not border, or this scoping assertion cannot move");

            var scopedPortals = ConnectedWorldRuntime.ProjectPortals(def, mainlineShift.ExportCheckpoint(), mainlineActor, p => true);
            Assert.That(scopedPortals.Select(p => p.PortalId), Is.EquivalentTo(bordering),
                "portal projection must be scoped to the actor's region, not to the interest radius alone");

            // 4. VISIBILITY: the client's own visibility function is a closure input too, so a portal
            // it rejects is not projected even when the region scope and the interest radius admit it.
            var hiddenPortals = ConnectedWorldRuntime.ProjectPortals(def, shift.ExportCheckpoint(), participant, p => p.Id != portal.Id);
            Assert.That(hiddenPortals.Any(p => p.PortalId == portal.Id), Is.False,
                "a portal the actor's visibility function rejects MUST NOT be projected");
        }

        [Test]
        public void FrameConsistencyMatchesAuthoritativeResponseAcrossAllSurfaces()
        {
            var def = LoadDefinition();

            // Frame 1: world frame (0, 0, 0)
            var worldFrame = def.Frame("world");
            // Frame 2: train-mainline frame (-50, 2, 15)
            var trainFrame = def.Frame("train-mainline");

            // The frame registry lookups above are load-bearing for every assertion below: the world
            // frame must be the identity translation, and the carriage frame must genuinely be
            // somewhere else or the cross-frame assertions further down would be vacuous.
            var localPos = new Point3(2.5f, 0, 1.0f);
            Assert.That(worldFrame.ToWorld(localPos).DistanceSquared(localPos), Is.LessThan(1e-6f),
                "the stationary world frame must be an identity local-to-world translation");
            Assert.That(def.Region("rail_platforms_mainline").FrameId, Is.EqualTo(worldFrame.FrameId));
            Assert.That(def.Region("rolling_stock_mainline").FrameId, Is.EqualTo(trainFrame.FrameId));
            Assert.That(trainFrame.Origin.DistanceSquared(worldFrame.Origin), Is.GreaterThan(1f),
                "the carriage frame must not coincide with the world frame, or the cross-frame route assertion below is vacuous");

            // Surface 1 & 2: Local <-> World frame coordinate translation
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

            // Surface 4 negative: acknowledgement authority is team-scoped, so a participant on
            // another team cannot acknowledge a report addressed to team-alpha even by id.
            var crossAckCmd = CreateCommand("other-01", "team-beta", CommandKind.AcknowledgeReport, report.ReportId);
            var crossAckReceipt = shift.Submit("other-01", crossAckCmd);
            Assert.That(crossAckReceipt.Code, Is.EqualTo(CommandCode.UnknownTarget),
                "a participant on another team MUST NOT be able to acknowledge a report addressed to a different team");

            var afterCrossAck = shift.Observe("teammate-01");
            Assert.That(afterCrossAck.Reports[0].AcknowledgedBy, Does.Not.Contain("other-01"),
                "the rejected cross-team acknowledgement must not have been written to the report");
        }

        // =========================================================================
        // Acceptance 2: ObservedIds-excluded information is absent from the view and
        // from the route/guidance negative scenario.
        // =========================================================================

        [Test]
        public void ObservedIdsExcludedInformationIsAbsentFromViewAndRouteNegativeScenario()
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

            // A second incident the actor HAS discovered. Without a non-empty observed entity list the
            // leak assertions below would hold for every possible projection, observed or not.
            const string discoveredIncidentId = "incident-smoke-concourse";
            var discoveredIncident = new EntityState
            {
                EntityId = discoveredIncidentId,
                RegionId = def.StartRegionId,
                Kind = EntityKind.Incident,
                Position = new Point3(2, 0, 0),
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
                ObservedIds = new[] { discoveredIncidentId } // aware of one incident only
            };

            var world = new WorldState
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                Participants = new[] { participant },
                Entities = new[] { unobservedIncident, discoveredIncident }
            };

            var shift = new AuthoritativeShift(world, sink, (p, e) => true);

            // Surface 1 (Server): both incidents are present in authoritative state
            var serverCheckpoint = shift.ExportCheckpoint();
            Assert.That(serverCheckpoint.Entities.Any(e => e.EntityId == unobservedIncidentId), Is.True);
            Assert.That(serverCheckpoint.Entities.Any(e => e.EntityId == discoveredIncidentId), Is.True);

            // Surface 2 (Client Observed View): the undiscovered incident is excluded by ObservedIds
            var observed = shift.Observe("actor-unaware");
            Assert.That(observed.Entities.Any(e => e.EntityId == unobservedIncidentId), Is.False,
                "ObservedIds-excluded incident MUST NOT appear in ObservedState.Entities");
            Assert.That(observed.Entities.Select(e => e.EntityId), Is.EquivalentTo(new[] { discoveredIncidentId }),
                "fixture guard: the observed entity list must be non-empty, otherwise the leak assertions below cannot move");

            // Surface 3 (Route & Guidance Projection): neither the undiscovered nor the discovered
            // incident identity may reach the route/guidance string surfaces. Route guidance is
            // region-level; entity identity is not part of its contract.
            var view = View(def.StartRegionId, def.ProfileId);
            view.Observed = observed;

            var route = NetworkFieldRuntime.ProjectRoute(def, view, "metro_platforms");
            Assert.That(route.Available, Is.True, "fixture guard: this is the available-route branch");
            Assert.That(route.ProcedureHint, Does.Not.Contain(unobservedIncidentId), "Route procedure hint must not leak the undiscovered incident");
            Assert.That(route.ProcedureHint, Does.Not.Contain(discoveredIncidentId), "Route procedure hint must not echo an observed entity identity");

            var guidance = NetworkFieldRuntime.ProjectGuidance(def, view, "metro_platforms");
            Assert.That(guidance.ProcedureHint, Does.Not.Contain(unobservedIncidentId), "Guidance procedure hint must not leak the undiscovered incident");
            Assert.That(guidance.RecommendedAction, Does.Not.Contain(unobservedIncidentId));
            Assert.That(guidance.ProcedureHint, Does.Not.Contain(discoveredIncidentId), "Guidance procedure hint must not echo an observed entity identity");
            Assert.That(guidance.RecommendedAction, Does.Not.Contain(discoveredIncidentId));

            // Surface 3 negative: an unavailable route states a reason, and that reason must not
            // carry incident identity either. (The assertion above runs on an available route, whose
            // UnavailableReason is the empty string by contract, so it is asserted here instead.)
            var blockedPortal = def.Portals.First(p => p.Id == ConfluencePortalId);
            var blockedView = View(def.StartRegionId, def.ProfileId,
                new ObservedPortalState { PortalId = blockedPortal.Id, Open = false });
            blockedView.Observed = observed;

            var blockedRoute = NetworkFieldRuntime.ProjectRoute(def, blockedView, "metro_platforms");
            Assert.That(blockedRoute.Available, Is.False, "fixture guard: closing the confluence portal must make the target unreachable");
            Assert.That(blockedRoute.UnavailableReason, Is.Not.Empty, "an unavailable route must state why");
            Assert.That(blockedRoute.UnavailableReason, Does.Not.Contain(unobservedIncidentId));
            Assert.That(blockedRoute.UnavailableReason, Does.Not.Contain(discoveredIncidentId));

            var blockedGuidance = NetworkFieldRuntime.ProjectGuidance(def, blockedView, "metro_platforms");
            Assert.That(blockedGuidance.Available, Is.False);
            Assert.That(blockedGuidance.UnavailableReason, Is.Not.Empty,
                "an unavailable guidance projection must state why, or the leak assertion below passes on an empty reason");
            Assert.That(blockedGuidance.UnavailableReason, Does.Not.Contain(unobservedIncidentId));

            // Surface 2 negative: observation authority is region-scoped, not merely distance-scoped.
            // An incident the actor has in ObservedIds, standing at the actor's own position, is still
            // not observed when it belongs to another region.
            const string foreignIncidentId = "incident-neighbour-region";
            var foreignIncident = new EntityState
            {
                EntityId = foreignIncidentId,
                RegionId = "station_hall_1f",
                Kind = EntityKind.Incident,
                Position = new Point3(0, 0, 0),
                Active = true,
                Revision = 1
            };
            var crossRegionActor = new ParticipantState
            {
                ParticipantId = "actor-cross-region",
                TeamId = "field-team",
                RoleId = "role-01",
                RegionId = def.StartRegionId,
                Position = new Point3(0, 0, 0),
                ObservedIds = new[] { foreignIncidentId }
            };
            var crossRegionWorld = new WorldState
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                Participants = new[] { crossRegionActor },
                Entities = new[] { foreignIncident }
            };
            var crossRegionShift = new AuthoritativeShift(crossRegionWorld, sink, (p, e) => true);
            var crossRegionObserved = crossRegionShift.Observe("actor-cross-region");
            Assert.That(crossRegionObserved.Entities.Any(e => e.EntityId == foreignIncidentId), Is.False,
                "schema-1 observation is scoped to the actor's own region: a discovered incident across the boundary is still not observed");

            // Surface 4 (Negative Command Submission): Attempting to report unobserved incident is rejected
            var illegalReport = CreateCommand("actor-unaware", "field-team", CommandKind.Report, unobservedIncidentId, expectedRevision: 1);
            var reportReceipt = shift.Submit("actor-unaware", illegalReport);
            Assert.That(reportReceipt.Code, Is.EqualTo(CommandCode.NotObserved),
                "Submitting report for an unobserved incident MUST return CommandCode.NotObserved");

            // The Operate path deliberately has no ObservedIds gate of its own: Observe() already
            // excludes undiscovered incidents, and every operable Equipment target in reach is always
            // observed. The rejection below is therefore the target-KIND check (an incident is not
            // operable), not an observation gate - do not read it as observation authority.
            var illegalOperate = CreateCommand("actor-unaware", "field-team", CommandKind.Operate, unobservedIncidentId, expectedRevision: 1);
            var opReceipt = shift.Submit("actor-unaware", illegalOperate);
            Assert.That(opReceipt.Code, Is.EqualTo(CommandCode.UnknownTarget),
                "Operating a non-Equipment target MUST be rejected as UnknownTarget by the target-kind check");
        }

        // =========================================================================
        // Acceptance 2 (continued): the observed portal set is the only closure input to
        // the route projection, so an observation that disagrees with the server is
        // projected as observed. This test constructs that disagreement explicitly.
        // =========================================================================

        [Test]
        public void ObservedPortalDisagreementWithAuthoritativeDoorIsExposedNotReconciled()
        {
            var def = LoadDefinition();
            var sink = new MemorySink();

            var portal = def.Portals.First(p => p.Id == "station_hall_1f--station_concourse_2f");
            const string doorEntityId = "equipment.station_hall_1f";

            // Server truth: the door is inactive, so the authoritative portal projection is Closed.
            var doorEntity = new EntityState
            {
                EntityId = doorEntityId,
                RegionId = portal.From,
                Kind = EntityKind.Equipment,
                Position = portal.FromPoint,
                Active = false,
                Revision = 1
            };
            var actor = new ParticipantState
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
                Participants = new[] { actor },
                Entities = new[] { doorEntity }
            };
            var shift = new AuthoritativeShift(world, sink, (p, e) => true);

            var authoritative = ConnectedWorldRuntime.ProjectPortals(def, shift.ExportCheckpoint(), actor, p => true)
                .Single(p => p.PortalId == portal.Id);
            Assert.That(authoritative.Open, Is.False, "an inactive door must project an authoritatively Closed portal");

            // The client's last snapshot still says the door was open. It is not written as a literal: it is
            // the same projection run against a world where the door was still active, so the disagreement
            // below is produced by the two world states rather than asserted by the fixture. A literal
            // compared against the value the assertion directly above already pinned could never fail on
            // its own: the two sides would be the same expression, so the fixture would assert nothing.
            var openDoorEntity = new EntityState
            {
                EntityId = doorEntityId,
                RegionId = portal.From,
                Kind = EntityKind.Equipment,
                Position = portal.FromPoint,
                Active = true,
                Revision = 1
            };
            var openWorld = new WorldState
            {
                WorldId = WorldId,
                ShiftId = ShiftId,
                Participants = new[] { actor },
                Entities = new[] { openDoorEntity }
            };
            var openShift = new AuthoritativeShift(openWorld, sink, (p, e) => true);
            var staleObserved = ConnectedWorldRuntime.ProjectPortals(def, openShift.ExportCheckpoint(), actor, p => true)
                .Where(p => p.PortalId == portal.Id).ToArray();
            Assert.That(staleObserved, Has.Length.EqualTo(1),
                "fixture guard: the door-open projection must carry the portal under test");
            Assert.That(staleObserved[0].Open, Is.Not.EqualTo(authoritative.Open),
                "fixture guard: the client observation must actually disagree with the authority");

            var observed = shift.Observe(actor.ParticipantId);

            // Declared behaviour: the route projection's only closure input is the client's observed
            // portal set, so the stale observation is projected as-is and no mismatch is reported.
            var staleView = new FieldView
            {
                RegionId = portal.From,
                Portals = staleObserved,
                Observed = observed,
                TrainingMode = FoundationTrainingMode.Practice,
                SpatialProfileId = def.ProfileId
            };
            var staleRoute = NetworkFieldRuntime.ProjectRoute(def, staleView, portal.To);
            Assert.That(staleRoute.Available, Is.True,
                "the route projection is observation-authoritative: a stale observed-open portal yields an available route the server's door state does not support");
            Assert.That(staleRoute.RoutePortalIds, Does.Contain(portal.Id),
                "the stale observation makes the route cross the authoritatively closed portal");

            // Control: the same world projected from the authoritative portal state closes the route,
            // so the difference above is caused by the observation and nothing else in the fixture.
            var reconciledView = new FieldView
            {
                RegionId = portal.From,
                Portals = new[] { authoritative },
                Observed = observed,
                TrainingMode = FoundationTrainingMode.Practice,
                SpatialProfileId = def.ProfileId
            };
            var reconciledRoute = NetworkFieldRuntime.ProjectRoute(def, reconciledView, portal.To);
            Assert.That(reconciledRoute.Available, Is.False,
                "with the authoritative portal state the same route is blocked, so no reconciliation seam exists in the projection");
        }

        // =========================================================================
        // Acceptance 3: Stale revision or an unacknowledgeable report creates an explicit
        // failed/rejected result, not client-side silent selection.
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

            // 1. Stale target revision mismatch -> Explicit CommandCode.StaleTarget. This is the only
            // revision comparison production performs: the command's ExpectedRevision against the
            // entity's live Revision at admission time.
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
        // Acceptance 4: the existing SnapshotFreshness scope and this file's
        // projection-consistency scope are separable.
        // =========================================================================

        [Test]
        public void SnapshotFreshnessScopeAndFourSurfaceScopeAreSeparatelyReported()
        {
            // Scope A: Network transport snapshot timing freshness (SnapshotFreshness). Its verdict is
            // a function of the clock alone - the same object, no spatial input changed.
            var freshness = new SnapshotFreshness();
            Assert.That(freshness.IsCurrent(0), Is.False, "Freshness scope: initial state is uninitialized");
            freshness.Received(10.0);
            Assert.That(freshness.IsCurrent(10.4), Is.True, "Freshness scope: within 0.5s window is current");
            Assert.That(freshness.IsCurrent(10.6), Is.False, "Freshness scope: past 0.5s window is stale");

            // Freshness throws on backward timestamp or NaN
            Assert.Throws<ArgumentException>(() => freshness.Received(9.0), "Freshness scope: sequence regression rejected");
            Assert.Throws<ArgumentException>(() => freshness.IsCurrent(double.NaN), "Freshness scope: NaN rejected");

            // Scope B: projection consistency. Its verdict is a function of the observed portal set
            // alone - same region, same profile, no clock read.
            var def = LoadDefinition();
            var openView = View(def.StartRegionId, def.ProfileId);
            var blockedPortal = def.Portals.First(p => p.Id == ConfluencePortalId);
            var shutView = View(def.StartRegionId, def.ProfileId,
                new ObservedPortalState { PortalId = blockedPortal.Id, Open = false });

            var openRoute = NetworkFieldRuntime.ProjectRoute(def, openView, "metro_platforms");
            var shutRoute = NetworkFieldRuntime.ProjectRoute(def, shutView, "metro_platforms");
            Assert.That(openRoute.Available, Is.True);
            Assert.That(openRoute.RouteRegionIds.Length, Is.GreaterThan(1));

            var guidance = NetworkFieldRuntime.ProjectGuidance(def, openView, "metro_platforms");
            Assert.That(guidance.Available, Is.True);
            Assert.That(guidance.Mode, Is.EqualTo(FoundationTrainingMode.Practice));

            // Separability, asserted behaviourally and in both directions: each scope owns an input
            // that moves its own verdict, and the other scope's verdict is not what moved.
            Assert.That(openRoute.Available, Is.Not.EqualTo(shutRoute.Available),
                "route scope: the verdict moves with the observed portal set, an input the freshness scope cannot express");
            Assert.That(freshness.IsCurrent(10.4), Is.Not.EqualTo(freshness.IsCurrent(10.9)),
                "freshness scope: the verdict moves with the clock, an input the route projection does not read");

            // The projection's own inputs are the authored profile, bound by digest in the evidence
            // record. Asserting them here keeps the projection scenarios above tied to that input.
            Assert.That(def.Regions, Has.Length.EqualTo(13));
            Assert.That(def.Portals, Has.Length.EqualTo(12));
        }
    }
}
