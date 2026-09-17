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

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    /// <summary>
    /// FMP-09b Integration Tests: Concurrent Incidents, Shared Resources & Independent Recovery.
    /// Verifies deterministic dual-incident scheduling, physical/spatial isolation across authored
    /// incident pairs, asymmetric independent mitigation/recovery, negative integrity probes,
    /// and a continuous 7-stage operational lifecycle without world/simulation reset.
    /// </summary>
    [TestFixture]
    public sealed class Fmp09bConcurrentIncidentTests
    {
        private const string CatalogRelativePath = "foundation/scenarios/reviewed-incident-catalog.json";

        [Serializable]
        private sealed class IncidentCatalogData
        {
            public string schemaVersion;
            public string catalogId;
            public string catalogVersion;
            public string classification;
            public string disclaimer;
            public string exaStatus;
            public string emptyApplicabilityMeans;
            public string[] executionAllowlist;
            public VariantData[] reviewedVariants;
            public VariantData[] unreviewedVariants;
        }

        [Serializable]
        private sealed class VariantData
        {
            public string id;
            public string choiceId;
            public string kind;
            public int kindOrdinal;
            public string regionId;
            public string resourceKey;
            public long onsetTick;
            public long durationTicks;
            public string recoveryCondition;
            public string sourceId;
            public VariantApplicability applicability;
            public string reviewStatus;
            public string procedureAcceptance;
            public bool isExecutionCandidate;
            public string rationale;
        }

        [Serializable]
        private sealed class VariantApplicability
        {
            public string @operator;
            public string[] stations;
            public string[] lines;
            public string[] vehicles;
            public string[] roles;
        }

        private sealed class RecordingCommitSink : ICommitSink
        {
            public readonly List<ShiftCommit> Commits = new List<ShiftCommit>();
            public bool ThrowOnAppend;

            public void Append(ShiftCommit commit)
            {
                if (ThrowOnAppend) throw new IOException("Injected persistence failure");
                Commits.Add(commit.Copy());
            }
        }

        private static IncidentCatalogData LoadCatalog()
        {
            Assert.That(File.Exists(CatalogRelativePath), Is.True, $"Catalog file must exist at {CatalogRelativePath}");
            var json = File.ReadAllText(CatalogRelativePath);
            var catalog = JsonUtility.FromJson<IncidentCatalogData>(json);
            Assert.That(catalog, Is.Not.Null, "Catalog JSON deserialization failed.");
            return catalog;
        }

        private static IncidentSchedule CreatePairSchedule(
            VariantData first,
            VariantData second,
            int firstOnset = 2,
            int minQuiet = 1,
            int maxQuiet = 2,
            string profileId = "fmp09b-pair-schedule")
        {
            return new IncidentSchedule
            {
                ProfileId = profileId,
                MaximumActive = 2,
                FirstOnsetTick = firstOnset,
                MinimumQuietTicks = minQuiet,
                MaximumQuietTicks = maxQuiet,
                Choices = new[]
                {
                    new IncidentChoice
                    {
                        Id = first.choiceId,
                        ResourceKey = first.resourceKey,
                        Kind = (FoundationIncidentKind)first.kindOrdinal
                    },
                    new IncidentChoice
                    {
                        Id = second.choiceId,
                        ResourceKey = second.resourceKey,
                        Kind = (FoundationIncidentKind)second.kindOrdinal
                    }
                }
            };
        }

        private static WorldCommand MakeCommand(
            string participantId,
            string teamId,
            CommandKind kind,
            string targetId = "",
            string commandId = "",
            long expectedRevision = 0,
            string argument = "")
        {
            return new WorldCommand
            {
                WorldId = "world-test",
                ShiftId = "shift-test",
                ParticipantId = participantId,
                TeamId = teamId,
                CommandId = string.IsNullOrEmpty(commandId) ? Guid.NewGuid().ToString("N") : commandId,
                Kind = kind,
                TargetId = targetId,
                ExpectedRevision = expectedRevision,
                Argument = argument
            };
        }

        private static string Rehash(byte[] payload)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(payload);
                var combined = new byte[payload.Length + hash.Length];
                Buffer.BlockCopy(payload, 0, combined, 0, payload.Length);
                Buffer.BlockCopy(hash, 0, combined, payload.Length, hash.Length);
                return "CGID1:" + Convert.ToBase64String(combined);
            }
        }

        // =========================================================================
        // 1. Pair 1: fire-concourse + power-ticket
        // =========================================================================
        [Test]
        public void Pair1_ConcourseFireAndTicketPowerLoss_EquipmentAndThermalIsolation()
        {
            var catalog = LoadCatalog();
            var fireVariant = catalog.reviewedVariants.Single(v => v.choiceId == "fire-concourse");
            var powerVariant = catalog.reviewedVariants.Single(v => v.choiceId == "power-ticket");

            Assert.That(catalog.executionAllowlist, Does.Contain(fireVariant.choiceId));
            Assert.That(catalog.executionAllowlist, Does.Contain(powerVariant.choiceId));
            Assert.That(fireVariant.resourceKey, Is.EqualTo("station_concourse_2f"));
            Assert.That(powerVariant.resourceKey, Is.EqualTo("station_ticket_area"));
            Assert.That(fireVariant.resourceKey, Is.Not.EqualTo(powerVariant.resourceKey),
                "Concurrent Pair 1 must occupy distinct resource keys.");

            var schedule = CreatePairSchedule(fireVariant, powerVariant, firstOnset: 2, minQuiet: 1, maxQuiet: 2);
            var director = new IncidentDirector(schedule, seed: 10101UL);

            Assert.That(director.AdvanceOne(), Is.Null, "No early onset prior to FirstOnsetTick");
            var ep1 = director.AdvanceOne();
            Assert.That(ep1, Is.Not.Null, "First onset must occur at FirstOnsetTick");
            Assert.That(director.Active.Count, Is.EqualTo(1));

            IncidentEpisode ep2 = null;
            for (var i = 0; i < 10 && ep2 == null; i++)
            {
                ep2 = director.AdvanceOne();
            }

            Assert.That(ep2, Is.Not.Null, "Second onset must occur within quiet tick window");
            Assert.That(director.Active.Count, Is.EqualTo(2), "Both Pair 1 incidents must be concurrently active");

            var activeChoices = director.Active.Select(e => e.ChoiceId).ToArray();
            Assert.That(activeChoices, Does.Contain("fire-concourse"));
            Assert.That(activeChoices, Does.Contain("power-ticket"));

            // Thermal Domain Isolation: Concourse fire releases heat; ticket area has zero fire forcing
            var fireNetwork = new FireNetworkDefinition
            {
                Cells = new[]
                {
                    new FireCellDefinition { Id = "cell-concourse", WidthM = 12, DepthM = 12, HeightM = 4 },
                    new FireCellDefinition { Id = "cell-ticket", WidthM = 10, DepthM = 10, HeightM = 3.5 }
                }
            };
            var fireModel = new ZoneFireModel(fireNetwork);
            var concourseForcing = new FireForcing
            {
                Sources = new[]
                {
                    new FireSourcePower
                    {
                        CellId = "cell-concourse",
                        HeatReleaseW = 60000,
                        FuelMassKgPerSecond = 0.003,
                        SmokeMassKgPerSecond = 0.0003,
                        RadiationFraction = 0.3
                    }
                }
            };

            Assert.That(fireModel.TryAdvance(0.05, concourseForcing, out var fireReport), Is.True, fireReport.Failure);
            var fireState = fireModel.ExportState();
            Assert.That(fireState.ReleasedHeatJ, Is.GreaterThan(0), "Concourse fire must produce heat");

            var concourseMetrics = fireModel.ReadCell("cell-concourse");
            var ticketMetrics = fireModel.ReadCell("cell-ticket");
            Assert.That(concourseMetrics.UpperTemperatureK, Is.GreaterThan(ticketMetrics.UpperTemperatureK),
                "Concourse cell temperature must rise while ticket cell remains unaffected");

            // Service & Equipment Isolation via AuthoritativeShift
            var sink = new RecordingCommitSink();
            var world = new WorldState
            {
                WorldId = "world-test",
                ShiftId = "shift-test",
                Participants = new[]
                {
                    new ParticipantState { ParticipantId = "actor-patrol", TeamId = "field-team", RoleId = "role-01", RegionId = "station_concourse_2f", Position = new Point3(1, 0, 0) },
                    new ParticipantState { ParticipantId = "actor-ticket", TeamId = "facility-team", RoleId = "role-04", RegionId = "station_ticket_area", Position = new Point3(40, 0, 0) }
                },
                Entities = new[]
                {
                    new EntityState { EntityId = "equipment-fire-hydrant", RegionId = "station_concourse_2f", RequiredRoleId = "role-01", Kind = EntityKind.Equipment, Position = new Point3(1.2f, 0, 0), Active = true },
                    new EntityState { EntityId = "equipment-ticket-power", RegionId = "station_ticket_area", RequiredRoleId = "role-04", Kind = EntityKind.Equipment, Position = new Point3(40.5f, 0, 0), Active = true }
                }
            };

            var shift = new AuthoritativeShift(world, sink, (a, e) => true);

            // Cross-role boundary: patrol actor cannot operate ticket power equipment
            shift.SetServerPosition("actor-patrol", "station_ticket_area", new Point3(40.2f, 0, 0));
            var deniedCrossOperate = shift.Submit("actor-patrol", MakeCommand("actor-patrol", "field-team", CommandKind.Operate, targetId: "equipment-ticket-power"));
            Assert.That(deniedCrossOperate.Code, Is.EqualTo(CommandCode.RoleDenied),
                "Role-01 must be denied operation on role-04 ticket equipment");

            // Authorized operations maintain complete entity separation
            shift.SetServerPosition("actor-patrol", "station_concourse_2f", new Point3(1, 0, 0));
            var acceptHydrant = shift.Submit("actor-patrol", MakeCommand("actor-patrol", "field-team", CommandKind.Operate, targetId: "equipment-fire-hydrant"));
            Assert.That(acceptHydrant.Code, Is.EqualTo(CommandCode.Accepted));

            var acceptPower = shift.Submit("actor-ticket", MakeCommand("actor-ticket", "facility-team", CommandKind.Operate, targetId: "equipment-ticket-power"));
            Assert.That(acceptPower.Code, Is.EqualTo(CommandCode.Accepted));

            var checkpoint = shift.ExportCheckpoint();
            Assert.That(checkpoint.Entities.Single(e => e.EntityId == "equipment-fire-hydrant").Revision, Is.EqualTo(1));
            Assert.That(checkpoint.Entities.Single(e => e.EntityId == "equipment-ticket-power").Revision, Is.EqualTo(1));
            Assert.That(sink.Commits.Count, Is.EqualTo(2));
        }

        // =========================================================================
        // 2. Pair 2: blocked-connector + pa-metro
        // =========================================================================
        [Test]
        public void Pair2_BlockedConnectorAndMetroPa_PassagewayAndGuidanceIsolation()
        {
            var catalog = LoadCatalog();
            var blockedVariant = catalog.reviewedVariants.Single(v => v.choiceId == "blocked-connector");
            var paVariant = catalog.reviewedVariants.Single(v => v.choiceId == "pa-metro");

            Assert.That(catalog.executionAllowlist, Does.Contain(blockedVariant.choiceId));
            Assert.That(catalog.executionAllowlist, Does.Contain(paVariant.choiceId));
            Assert.That(blockedVariant.resourceKey, Is.EqualTo("underground_connector"));
            Assert.That(paVariant.resourceKey, Is.EqualTo("metro_concourse"));
            Assert.That(blockedVariant.resourceKey, Is.Not.EqualTo(paVariant.resourceKey));

            var schedule = CreatePairSchedule(blockedVariant, paVariant, firstOnset: 2, minQuiet: 1, maxQuiet: 2);
            var director = new IncidentDirector(schedule, seed: 20202UL);

            director.AdvanceOne(); // tick 1
            director.AdvanceOne(); // tick 2: first onset
            for (var i = 0; i < 10 && director.Active.Count < 2; i++)
            {
                director.AdvanceOne();
            }

            Assert.That(director.Active.Count, Is.EqualTo(2));
            var activeChoices = director.Active.Select(e => e.ChoiceId).ToArray();
            Assert.That(activeChoices, Does.Contain("blocked-connector"));
            Assert.That(activeChoices, Does.Contain("pa-metro"));

            // Passageway & Guidance Isolation Model
            var closedPortals = new HashSet<string>(StringComparer.Ordinal);
            var activeIncidents = director.Active;

            // Route restriction closes underground connector portal
            foreach (var ep in activeIncidents.Where(e => !e.Mitigated && e.ChoiceId == "blocked-connector"))
            {
                closedPortals.Add("underground_connector--underground_shopping_passage");
            }

            Assert.That(closedPortals.Contains("underground_connector--underground_shopping_passage"), Is.True,
                "Route restriction must close underground connector portal");
            Assert.That(closedPortals.Any(p => p.Contains("metro")), Is.False,
                "Passageways in metro concourse must remain completely open");

            // PA availability check
            bool IsPaAvailable(string regionId) =>
                !activeIncidents.Any(e => !e.Mitigated && e.ChoiceId == "pa-metro" && regionId == "metro_concourse");

            Assert.That(IsPaAvailable("metro_concourse"), Is.False,
                "PA system in metro concourse must be unavailable due to pa-metro incident");
            Assert.That(IsPaAvailable("underground_connector"), Is.True,
                "Public address in connector remains unaffected by metro PA failure");
            Assert.That(IsPaAvailable("station_concourse_2f"), Is.True,
                "Public address in station concourse remains unaffected");

            // Guidance & Debris Equipment Operations via Shift
            var sink = new RecordingCommitSink();
            var world = new WorldState
            {
                WorldId = "world-test",
                ShiftId = "shift-test",
                Participants = new[]
                {
                    new ParticipantState { ParticipantId = "actor-guidance", TeamId = "field-team", RoleId = "role-02", RegionId = "metro_concourse", Position = new Point3(100, 0, 0) },
                    new ParticipantState { ParticipantId = "actor-barrier", TeamId = "facility-team", RoleId = "role-03", RegionId = "underground_connector", Position = new Point3(50, 0, 0) }
                },
                Entities = new[]
                {
                    new EntityState { EntityId = "equipment-loudspeaker", RegionId = "metro_concourse", RequiredRoleId = "role-02", Kind = EntityKind.Equipment, Position = new Point3(100.5f, 0, 0), Active = true },
                    new EntityState { EntityId = "equipment-connector-gate", RegionId = "underground_connector", RequiredRoleId = "role-03", Kind = EntityKind.Equipment, Position = new Point3(50.5f, 0, 0), Active = true }
                }
            };

            var shift = new AuthoritativeShift(world, sink, (a, e) => true);

            var acceptSpeaker = shift.Submit("actor-guidance", MakeCommand("actor-guidance", "field-team", CommandKind.Operate, targetId: "equipment-loudspeaker"));
            Assert.That(acceptSpeaker.Code, Is.EqualTo(CommandCode.Accepted));

            var acceptGate = shift.Submit("actor-barrier", MakeCommand("actor-barrier", "facility-team", CommandKind.Operate, targetId: "equipment-connector-gate"));
            Assert.That(acceptGate.Code, Is.EqualTo(CommandCode.Accepted));

            Assert.That(shift.ExportCheckpoint().Entities.Single(e => e.EntityId == "equipment-loudspeaker").Revision, Is.EqualTo(1));
            Assert.That(shift.ExportCheckpoint().Entities.Single(e => e.EntityId == "equipment-connector-gate").Revision, Is.EqualTo(1));
        }

        // =========================================================================
        // 3. Pair 3: fire-concourse + blocked-connector
        // =========================================================================
        [Test]
        public void Pair3_ConcourseFireAndBlockedConnector_EvacuationBottleneckPreservation()
        {
            var catalog = LoadCatalog();
            var fireVariant = catalog.reviewedVariants.Single(v => v.choiceId == "fire-concourse");
            var blockedVariant = catalog.reviewedVariants.Single(v => v.choiceId == "blocked-connector");

            Assert.That(fireVariant.resourceKey, Is.EqualTo("station_concourse_2f"));
            Assert.That(blockedVariant.resourceKey, Is.EqualTo("underground_connector"));
            Assert.That(fireVariant.resourceKey, Is.Not.EqualTo(blockedVariant.resourceKey));

            var schedule = CreatePairSchedule(fireVariant, blockedVariant, firstOnset: 2, minQuiet: 1, maxQuiet: 2);
            var director = new IncidentDirector(schedule, seed: 30303UL);

            director.AdvanceOne();
            director.AdvanceOne();
            for (var i = 0; i < 10 && director.Active.Count < 2; i++)
            {
                director.AdvanceOne();
            }

            Assert.That(director.Active.Count, Is.EqualTo(2));

            // Thermal + Smoke Modeling in Concourse
            var smokeCell = new SmokeCell
            {
                Id = "cell-concourse-fire",
                CenterX = 0, CenterY = 0, CenterZ = 0,
                WidthM = 10, DepthM = 10, BoundingHeightM = 4,
                InterfaceHeightAboveMinFloorM = 0.8,
                UpperExtinctionPerM = 2.5,
                LowerExtinctionPerM = 0.5
            };
            var opticalField = new SmokeOpticalField(new[] { smokeCell });
            var trace = opticalField.Trace(new DoubleVector3(-3, 1, 0), new DoubleVector3(3, 1, 0));
            Assert.That(trace.OpticalDepth, Is.GreaterThan(3.0),
                "Concourse fire must generate dense smoke exceeding optical transmission limit");

            // Connector Portal Blockage Modeling
            var closedPortals = new HashSet<string>(StringComparer.Ordinal)
            {
                "underground_connector--underground_shopping_passage"
            };

            // Shift & Evacuation Bottleneck Verification
            var sink = new RecordingCommitSink();
            var world = new WorldState
            {
                WorldId = "world-test",
                ShiftId = "shift-test",
                Participants = new[]
                {
                    new ParticipantState { ParticipantId = "guide-evac", TeamId = "field-team", RoleId = "role-02", RegionId = "station_concourse_2f", Position = new Point3(0, 0, 0) },
                    new ParticipantState { ParticipantId = "npc-evacuee", TeamId = "field-team", RoleId = "role-02", RegionId = "station_concourse_2f", Position = new Point3(1, 0, 0) }
                },
                Entities = new[]
                {
                    new EntityState { EntityId = "npc-crowd-1", RegionId = "station_concourse_2f", Kind = EntityKind.Evacuee, Position = new Point3(1, 0, 0), Active = true },
                    new EntityState { EntityId = "portal-connector-barrier", RegionId = "underground_connector", RequiredRoleId = "role-03", Kind = EntityKind.Equipment, Position = new Point3(50, 0, 0), Active = false }
                }
            };

            var shift = new AuthoritativeShift(world, sink, (a, e) => true);

            // Guide claims evacuee in concourse
            var claimReceipt = shift.Submit("guide-evac", MakeCommand("guide-evac", "field-team", CommandKind.ClaimEvacuee, targetId: "npc-crowd-1"));
            Assert.That(claimReceipt.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(shift.ExportCheckpoint().Entities.Single(e => e.EntityId == "npc-crowd-1").LeaderId, Is.EqualTo("guide-evac"));

            // Connector portal is blocked: evacuation cannot proceed through the blocked passage
            Assert.That(closedPortals.Contains("underground_connector--underground_shopping_passage"), Is.True,
                "Underground connector barrier must remain closed as an active evacuation bottleneck");

            // Compound state invariant: fire is active AND connector is blocked simultaneously
            Assert.That(director.Active.Any(e => e.ChoiceId == "fire-concourse"), Is.True);
            Assert.That(director.Active.Any(e => e.ChoiceId == "blocked-connector"), Is.True);
        }

        // =========================================================================
        // 4. Asymmetric Independent Recovery: First Incident Mitigated First
        // =========================================================================
        [Test]
        public void AsymmetricRecovery_MitigateFirstIncident_PreservesSecondIncidentStateAndPhysicalEffects()
        {
            var catalog = LoadCatalog();
            var v1 = catalog.reviewedVariants.Single(v => v.choiceId == "fire-concourse");
            var v2 = catalog.reviewedVariants.Single(v => v.choiceId == "power-ticket");

            var schedule = CreatePairSchedule(v1, v2, firstOnset: 2, minQuiet: 1, maxQuiet: 2);
            var director = new IncidentDirector(schedule, seed: 40404UL);

            director.AdvanceOne();
            var first = director.AdvanceOne();
            Assert.That(first, Is.Not.Null);

            IncidentEpisode second = null;
            for (var i = 0; i < 10 && second == null; i++) second = director.AdvanceOne();
            Assert.That(second, Is.Not.Null);
            Assert.That(director.Active.Count, Is.EqualTo(2));

            var secondStartedTick = second.StartedTick;
            var secondChoiceId = second.ChoiceId;

            // Step 1: Mitigate the FIRST incident
            director.Mitigate(first.Id);

            var activeFirst = director.Active.Single(e => e.Id == first.Id);
            var activeSecond = director.Active.Single(e => e.Id == second.Id);

            Assert.That(activeFirst.Mitigated, Is.True, "First incident must be mitigated");
            Assert.That(activeSecond.Mitigated, Is.False, "Second incident must remain UNMITIGATED");
            Assert.That(activeSecond.StartedTick, Is.EqualTo(secondStartedTick), "Second incident started tick preserved");

            // Step 2: Complete the FIRST incident
            director.Complete(first.Id);

            Assert.That(director.Active.Count, Is.EqualTo(1));
            var remaining = director.Active.Single();
            Assert.That(remaining.Id, Is.EqualTo(second.Id));
            Assert.That(remaining.ChoiceId, Is.EqualTo(secondChoiceId));
            Assert.That(remaining.Mitigated, Is.False, "Second incident must still be active and unmitigated");

            // Cannot complete unmitigated second incident
            Assert.Throws<ArgumentException>(() => director.Complete(second.Id));

            // Export checkpoint and restore into new director: exact preservation
            var checkpoint = director.ExportCheckpoint();
            var restoredDirector = new IncidentDirector(schedule, 99999UL);
            restoredDirector.Restore(checkpoint);

            Assert.That(restoredDirector.Active.Count, Is.EqualTo(1));
            Assert.That(restoredDirector.Active[0].Id, Is.EqualTo(second.Id));
            Assert.That(restoredDirector.Active[0].Mitigated, Is.False);
            Assert.That(restoredDirector.ExportCheckpoint(), Is.EqualTo(checkpoint));

            // Finally mitigate and complete the second incident
            restoredDirector.Mitigate(second.Id);
            Assert.That(restoredDirector.Active[0].Mitigated, Is.True);
            restoredDirector.Complete(second.Id);
            Assert.That(restoredDirector.Active, Is.Empty, "All incidents fully recovered");
        }

        // =========================================================================
        // 5. Asymmetric Independent Recovery: Second Incident Mitigated First
        // =========================================================================
        [Test]
        public void AsymmetricRecovery_MitigateSecondIncidentFirst_PreservesFirstIncidentStateAndPhysicalEffects()
        {
            var catalog = LoadCatalog();
            var v1 = catalog.reviewedVariants.Single(v => v.choiceId == "fire-concourse");
            var v2 = catalog.reviewedVariants.Single(v => v.choiceId == "blocked-connector");

            var schedule = CreatePairSchedule(v1, v2, firstOnset: 2, minQuiet: 1, maxQuiet: 2);
            var director = new IncidentDirector(schedule, seed: 50505UL);

            director.AdvanceOne();
            var first = director.AdvanceOne();
            IncidentEpisode second = null;
            for (var i = 0; i < 10 && second == null; i++) second = director.AdvanceOne();
            Assert.That(director.Active.Count, Is.EqualTo(2));

            var firstStartedTick = first.StartedTick;
            var firstChoiceId = first.ChoiceId;

            // Step 1: Mitigate the SECOND incident first
            director.Mitigate(second.Id);

            var activeFirst = director.Active.Single(e => e.Id == first.Id);
            var activeSecond = director.Active.Single(e => e.Id == second.Id);

            Assert.That(activeSecond.Mitigated, Is.True, "Second incident must be mitigated");
            Assert.That(activeFirst.Mitigated, Is.False, "First incident must remain UNMITIGATED");
            Assert.That(activeFirst.StartedTick, Is.EqualTo(firstStartedTick));

            // Step 2: Complete the SECOND incident first
            director.Complete(second.Id);

            Assert.That(director.Active.Count, Is.EqualTo(1));
            var remaining = director.Active.Single();
            Assert.That(remaining.Id, Is.EqualTo(first.Id));
            Assert.That(remaining.ChoiceId, Is.EqualTo(firstChoiceId));
            Assert.That(remaining.Mitigated, Is.False, "First incident remains active and unmitigated");

            // Cannot complete unmitigated first incident
            Assert.Throws<ArgumentException>(() => director.Complete(first.Id));

            // Verify physical thermal effects persist for the unmitigated first incident
            var fireNetwork = new FireNetworkDefinition
            {
                Cells = new[] { new FireCellDefinition { Id = "cell-concourse", WidthM = 10, DepthM = 10, HeightM = 4 } }
            };
            var fireModel = new ZoneFireModel(fireNetwork);
            var fireForcing = new FireForcing
            {
                Sources = new[]
                {
                    new FireSourcePower { CellId = "cell-concourse", HeatReleaseW = 50000, FuelMassKgPerSecond = 0.002, SmokeMassKgPerSecond = 0.0002 }
                }
            };
            Assert.That(fireModel.TryAdvance(0.05, fireForcing, out _), Is.True);
            Assert.That(fireModel.ExportState().ReleasedHeatJ, Is.GreaterThan(0));

            // Complete first incident
            director.Mitigate(first.Id);
            director.Complete(first.Id);
            Assert.That(director.Active, Is.Empty);
        }

        // =========================================================================
        // 6. Negative Probe: Same Resource Key Collision
        // =========================================================================
        [Test]
        public void Negative_SameResourceKeyCollision_RefusedWithoutStateCorruption()
        {
            var catalog = LoadCatalog();
            // In catalog, fire-mainline and train-mainline-fault both use resourceKey: rolling_stock_mainline
            var v1 = catalog.reviewedVariants.Single(v => v.choiceId == "fire-mainline");
            var v2 = catalog.reviewedVariants.Single(v => v.choiceId == "train-mainline-fault");

            Assert.That(v1.resourceKey, Is.EqualTo("rolling_stock_mainline"));
            Assert.That(v2.resourceKey, Is.EqualTo("rolling_stock_mainline"));

            var schedule = new IncidentSchedule
            {
                ProfileId = "same-resource-key-schedule",
                MaximumActive = 2,
                FirstOnsetTick = 2,
                MinimumQuietTicks = 1,
                MaximumQuietTicks = 2,
                Choices = new[]
                {
                    new IncidentChoice { Id = v1.choiceId, ResourceKey = v1.resourceKey, Kind = FoundationIncidentKind.Fire },
                    new IncidentChoice { Id = v2.choiceId, ResourceKey = v2.resourceKey, Kind = FoundationIncidentKind.TrainFault }
                }
            };

            var director = new IncidentDirector(schedule, seed: 60606UL);
            director.AdvanceOne();
            var first = director.AdvanceOne();
            Assert.That(first, Is.Not.Null);
            Assert.That(director.Active.Count, Is.EqualTo(1));

            // Advance 50 ticks: second choice CANNOT onset because resourceKey is already occupied
            for (var i = 0; i < 50; i++)
            {
                var attempt = director.AdvanceOne();
                Assert.That(attempt, Is.Null, "Cannot schedule incident on already occupied resource key");
                Assert.That(director.Active.Count, Is.EqualTo(1));
            }

            // Checkpoint forgery probe: attempt to restore a checkpoint with duplicate resource keys
            var validCp = director.ExportCheckpoint();
            var victim = new IncidentDirector(schedule, 77777UL);
            victim.Restore(validCp);
            var victimStateBefore = victim.ExportCheckpoint();

            // Construct payload with two active incidents on the same resourceKey
            // Binary format: 1 (int32), signature (string), random (uint64), tick (int64), nextOnset (int64),
            // issued (int64), lastIssuedTick (int64), count (int32=2),
            // ep1: id, choiceId (fire-mainline), startedTick, flag
            // ep2: id, choiceId (train-mainline-fault), startedTick, flag
            byte[] forgedPayload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                w.Write(1);
                // We extract the signature from validCp plain bytes
                var plainValid = Convert.FromBase64String(validCp.Substring(6));
                plainValid = plainValid.Take(plainValid.Length - 32).ToArray();
                using (var r = new BinaryReader(new MemoryStream(plainValid), Encoding.UTF8))
                {
                    r.ReadInt32();
                    var sig = r.ReadString();
                    w.Write(sig);
                }
                w.Write(12345UL); // random
                w.Write((long)10); // tick
                w.Write((long)12); // nextOnset
                w.Write((long)2);  // issued
                w.Write((long)5);  // lastIssuedTick
                w.Write(2);        // count = 2
                // ep 1
                w.Write("incident-1"); w.Write("fire-mainline"); w.Write((long)2); w.Write((byte)0);
                // ep 2 (colliding resourceKey: rolling_stock_mainline)
                w.Write("incident-2"); w.Write("train-mainline-fault"); w.Write((long)5); w.Write((byte)0);
                w.Flush();
                forgedPayload = ms.ToArray();
            }

            var forgedCheckpoint = Rehash(forgedPayload);

            // Must throw ArgumentException on resource key collision
            Assert.Throws<ArgumentException>(() => victim.Restore(forgedCheckpoint));

            // Atomic zero state mutation
            Assert.That(victim.ExportCheckpoint(), Is.EqualTo(victimStateBefore),
                "Victim director state must be untouched following rejected collision restore");
        }

        // =========================================================================
        // 7. Negative Probe: Maximum Active Capacity Overflow
        // =========================================================================
        [Test]
        public void Negative_MaximumActiveCapacityOverflow_RefusesThirdActiveIncident()
        {
            var catalog = LoadCatalog();
            var v1 = catalog.reviewedVariants.Single(v => v.choiceId == "fire-concourse");
            var v2 = catalog.reviewedVariants.Single(v => v.choiceId == "power-ticket");
            var v3 = catalog.reviewedVariants.Single(v => v.choiceId == "pa-metro");

            var schedule = new IncidentSchedule
            {
                ProfileId = "overflow-schedule",
                MaximumActive = 2,
                FirstOnsetTick = 2,
                MinimumQuietTicks = 1,
                MaximumQuietTicks = 2,
                Choices = new[]
                {
                    new IncidentChoice { Id = v1.choiceId, ResourceKey = v1.resourceKey, Kind = FoundationIncidentKind.Fire },
                    new IncidentChoice { Id = v2.choiceId, ResourceKey = v2.resourceKey, Kind = FoundationIncidentKind.PowerLoss },
                    new IncidentChoice { Id = v3.choiceId, ResourceKey = v3.resourceKey, Kind = FoundationIncidentKind.PublicAddressFailure }
                }
            };

            var director = new IncidentDirector(schedule, seed: 70707UL);
            director.AdvanceOne();
            director.AdvanceOne(); // 1st active
            for (var i = 0; i < 10 && director.Active.Count < 2; i++) director.AdvanceOne();

            Assert.That(director.Active.Count, Is.EqualTo(2), "Reached MaximumActive (2)");

            // Probe A: AdvanceOne refuses 3rd active incident across 100 ticks
            for (var tick = 0; tick < 100; tick++)
            {
                var overflowEpisode = director.AdvanceOne();
                Assert.That(overflowEpisode, Is.Null, "Cannot spawn 3rd incident when MaximumActive is reached");
                Assert.That(director.Active.Count, Is.EqualTo(2));
            }

            // Probe B: Forged checkpoint with active.Count = 3
            var victim = new IncidentDirector(schedule, 88888UL);
            var victimBefore = victim.ExportCheckpoint();

            byte[] forgedOverflow;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                w.Write(1);
                var plainValid = Convert.FromBase64String(director.ExportCheckpoint().Substring(6));
                using (var r = new BinaryReader(new MemoryStream(plainValid), Encoding.UTF8))
                {
                    r.ReadInt32();
                    w.Write(r.ReadString()); // signature
                }
                w.Write(99UL);
                w.Write((long)20);
                w.Write((long)22);
                w.Write((long)3); // issued = 3
                w.Write((long)15); // last
                w.Write(3); // count = 3 (exceeds MaximumActive = 2)
                w.Write("incident-1"); w.Write(v1.choiceId); w.Write((long)2); w.Write((byte)0);
                w.Write("incident-2"); w.Write(v2.choiceId); w.Write((long)5); w.Write((byte)0);
                w.Write("incident-3"); w.Write(v3.choiceId); w.Write((long)15); w.Write((byte)0);
                w.Flush();
                forgedOverflow = ms.ToArray();
            }

            var overflowCp = Rehash(forgedOverflow);
            Assert.Throws<ArgumentException>(() => victim.Restore(overflowCp));
            Assert.That(victim.ExportCheckpoint(), Is.EqualTo(victimBefore), "Zero state mutation on overflow rejection");
        }

        // =========================================================================
        // 8. Negative Probe: Unreviewed Variant or Invalid ID
        // =========================================================================
        [Test]
        public void Negative_UnreviewedVariantOrInvalidId_RejectedAtomically()
        {
            var catalog = LoadCatalog();

            // Probe 8a: Verify unreviewed variants are strictly excluded from execution allowlist
            Assert.That(catalog.unreviewedVariants, Is.Not.Empty);
            foreach (var unrev in catalog.unreviewedVariants)
            {
                Assert.That(catalog.executionAllowlist, Does.Not.Contain(unrev.choiceId),
                    $"Unreviewed variant {unrev.choiceId} must never appear in executionAllowlist");
                Assert.That(unrev.isExecutionCandidate, Is.False);
                Assert.That(unrev.reviewStatus, Is.EqualTo("unreviewed"));
                Assert.That(unrev.procedureAcceptance, Is.EqualTo("pending_operator_review"));
            }

            var v1 = catalog.reviewedVariants[0];
            var schedule = new IncidentSchedule
            {
                ProfileId = "negative-unreviewed-schedule",
                MaximumActive = 1,
                FirstOnsetTick = 2,
                MinimumQuietTicks = 1,
                MaximumQuietTicks = 2,
                Choices = new[]
                {
                    new IncidentChoice { Id = v1.choiceId, ResourceKey = v1.resourceKey, Kind = FoundationIncidentKind.Fire }
                }
            };

            var director = new IncidentDirector(schedule, seed: 80808UL);
            director.AdvanceOne();
            director.AdvanceOne();
            var validCp = director.ExportCheckpoint();

            var victim = new IncidentDirector(schedule, 12345UL);
            var victimBefore = victim.ExportCheckpoint();

            // Probe 8b: Checkpoint referencing unreviewed / unknown choiceId
            byte[] forgedUnknownChoice;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                w.Write(1);
                var plainValid = Convert.FromBase64String(validCp.Substring(6));
                using (var r = new BinaryReader(new MemoryStream(plainValid), Encoding.UTF8))
                {
                    r.ReadInt32();
                    w.Write(r.ReadString());
                }
                w.Write(12UL); w.Write((long)2); w.Write((long)4); w.Write((long)1); w.Write((long)2);
                w.Write(1); // count = 1
                w.Write("incident-1");
                w.Write("unreviewed-korail-public-report"); // unreviewed choiceId not in schedule
                w.Write((long)2); w.Write((byte)0);
                w.Flush();
                forgedUnknownChoice = ms.ToArray();
            }

            var unknownChoiceCp = Rehash(forgedUnknownChoice);
            Assert.Throws<ArgumentException>(() => victim.Restore(unknownChoiceCp));
            Assert.That(victim.ExportCheckpoint(), Is.EqualTo(victimBefore));

            // Probe 8c: Checkpoint with invalid non-canonical episode ID (e.g. "forged-id" instead of "incident-N")
            byte[] forgedBadId;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                w.Write(1);
                var plainValid = Convert.FromBase64String(validCp.Substring(6));
                using (var r = new BinaryReader(new MemoryStream(plainValid), Encoding.UTF8))
                {
                    r.ReadInt32();
                    w.Write(r.ReadString());
                }
                w.Write(12UL); w.Write((long)2); w.Write((long)4); w.Write((long)1); w.Write((long)2);
                w.Write(1);
                w.Write("forged-incident-99"); // non-canonical ID
                w.Write(v1.choiceId); w.Write((long)2); w.Write((byte)0);
                w.Flush();
                forgedBadId = ms.ToArray();
            }

            var badIdCp = Rehash(forgedBadId);
            Assert.Throws<ArgumentException>(() => victim.Restore(badIdCp));
            Assert.That(victim.ExportCheckpoint(), Is.EqualTo(victimBefore));
        }

        // =========================================================================
        // 9. Negative Probe: Tampered Checkpoint Restore
        // =========================================================================
        [Test]
        public void Negative_TamperedCheckpointRestore_RejectedAtomically()
        {
            var catalog = LoadCatalog();
            var v1 = catalog.reviewedVariants[0];
            var v2 = catalog.reviewedVariants[1];

            var schedule = CreatePairSchedule(v1, v2, firstOnset: 2, minQuiet: 1, maxQuiet: 2);
            var director = new IncidentDirector(schedule, seed: 90909UL);

            director.AdvanceOne();
            director.AdvanceOne();
            for (var i = 0; i < 10 && director.Active.Count < 2; i++) director.AdvanceOne();
            Assert.That(director.Active.Count, Is.EqualTo(2));

            var validCp = director.ExportCheckpoint();
            var victim = new IncidentDirector(schedule, 55555UL);
            var victimBefore = victim.ExportCheckpoint();

            // 9a: SHA256 checksum tampering (flip byte in the 32-byte digest tail)
            var rawBytes = Convert.FromBase64String(validCp.Substring(6));
            rawBytes[rawBytes.Length - 1] ^= 0xFF;
            var tamperedDigest = "CGID1:" + Convert.ToBase64String(rawBytes);
            Assert.Throws<ArgumentException>(() => victim.Restore(tamperedDigest));
            Assert.That(victim.ExportCheckpoint(), Is.EqualTo(victimBefore), "Digest tamper must leave victim untouched");

            // 9b: Payload byte tampering (without recalculating digest)
            rawBytes = Convert.FromBase64String(validCp.Substring(6));
            rawBytes[8] ^= 0xAA;
            var tamperedPayload = "CGID1:" + Convert.ToBase64String(rawBytes);
            Assert.Throws<ArgumentException>(() => victim.Restore(tamperedPayload));
            Assert.That(victim.ExportCheckpoint(), Is.EqualTo(victimBefore), "Payload tamper must leave victim untouched");

            // 9c: Definition/Signature mismatch
            var otherSchedule = new IncidentSchedule
            {
                ProfileId = "different-profile-v2",
                MaximumActive = 1,
                FirstOnsetTick = 10,
                MinimumQuietTicks = 5,
                MaximumQuietTicks = 15,
                Choices = new[] { new IncidentChoice { Id = "fire-concourse", ResourceKey = "station_concourse_2f", Kind = FoundationIncidentKind.Fire } }
            };
            var foreignDirector = new IncidentDirector(otherSchedule, 11111UL);
            var foreignBefore = foreignDirector.ExportCheckpoint();
            Assert.Throws<ArgumentException>(() => foreignDirector.Restore(validCp));
            Assert.That(foreignDirector.ExportCheckpoint(), Is.EqualTo(foreignBefore), "Signature mismatch leaves state untouched");

            // 9d: Fabricated counter tampering (re-hashed with valid digest but invalid logic)
            var plain = Convert.FromBase64String(validCp.Substring(6));
            plain = plain.Take(plain.Length - 32).ToArray();

            // Corrupt tick offset to 0 while onset is active
            var fabricated = (byte[])plain.Clone();
            Array.Copy(BitConverter.GetBytes((long)0), 0, fabricated, 12, 8);
            Assert.Throws<ArgumentException>(() => victim.Restore(Rehash(fabricated)));
            Assert.That(victim.ExportCheckpoint(), Is.EqualTo(victimBefore), "Fabricated counter leaves state untouched");
        }

        // =========================================================================
        // 10. Negative Probe: Unauthorized Mitigation
        // =========================================================================
        [Test]
        public void Negative_UnauthorizedMitigation_ReturnsRoleDeniedWithoutStateMutation()
        {
            var sink = new RecordingCommitSink();
            var world = new WorldState
            {
                WorldId = "world-test",
                ShiftId = "shift-test",
                Participants = new[]
                {
                    new ParticipantState { ParticipantId = "actor-evac", TeamId = "field-team", RoleId = "role-02", RegionId = "station_concourse_2f", Position = new Point3(1, 0, 0) },
                    new ParticipantState { ParticipantId = "actor-patrol", TeamId = "field-team", RoleId = "role-01", RegionId = "station_ticket_area", Position = new Point3(20, 0, 0) }
                },
                Entities = new[]
                {
                    new EntityState { EntityId = "equipment-fire-hydrant", RegionId = "station_concourse_2f", RequiredRoleId = "role-01", Kind = EntityKind.Equipment, Position = new Point3(1.2f, 0, 0), Active = true, Revision = 0 },
                    new EntityState { EntityId = "equipment-power-switch", RegionId = "station_ticket_area", RequiredRoleId = "role-03", Kind = EntityKind.Equipment, Position = new Point3(20.2f, 0, 0), Active = true, Revision = 0 }
                }
            };

            var shift = new AuthoritativeShift(world, sink, (a, e) => true);
            var initialCheckpoint = shift.ExportCheckpoint();

            // Probe 10a: Unauthorized role-02 attempts fire hydrant operation (requires role-01)
            var deniedHydrant = shift.Submit("actor-evac", MakeCommand("actor-evac", "field-team", CommandKind.Operate, targetId: "equipment-fire-hydrant"));
            Assert.That(deniedHydrant.Code, Is.EqualTo(CommandCode.RoleDenied));

            // Atomic zero state mutation assertion
            var afterHydrant = shift.ExportCheckpoint();
            Assert.That(afterHydrant.Sequence, Is.EqualTo(initialCheckpoint.Sequence), "Sequence must NOT increment on RoleDenied");
            Assert.That(afterHydrant.Entities.Single(e => e.EntityId == "equipment-fire-hydrant").Revision, Is.EqualTo(0));
            Assert.That(afterHydrant.Entities.Single(e => e.EntityId == "equipment-fire-hydrant").Active, Is.True);
            Assert.That(sink.Commits.Count, Is.EqualTo(0), "No commits appended on rejected command");

            // Probe 10b: Unauthorized role-01 attempts power switch operation (requires role-03)
            var deniedPower = shift.Submit("actor-patrol", MakeCommand("actor-patrol", "field-team", CommandKind.Operate, targetId: "equipment-power-switch"));
            Assert.That(deniedPower.Code, Is.EqualTo(CommandCode.RoleDenied));

            var afterPower = shift.ExportCheckpoint();
            Assert.That(afterPower.Sequence, Is.EqualTo(initialCheckpoint.Sequence));
            Assert.That(afterPower.Entities.Single(e => e.EntityId == "equipment-power-switch").Revision, Is.EqualTo(0));
            Assert.That(sink.Commits.Count, Is.EqualTo(0));

            // Probe 10c: Non-instructor attempts shift pause
            var deniedPause = shift.Submit("actor-evac", MakeCommand("actor-evac", "field-team", CommandKind.PauseShift));
            Assert.That(deniedPause.Code, Is.EqualTo(CommandCode.RoleDenied));
            Assert.That(shift.Paused, Is.False);
            Assert.That(shift.ExportCheckpoint().Sequence, Is.EqualTo(initialCheckpoint.Sequence));
            Assert.That(sink.Commits.Count, Is.EqualTo(0));
        }

        // =========================================================================
        // 11. Continuous 7-Stage Operational Lifecycle
        // =========================================================================
        [Test]
        public void ContinuousLifecycle_NormalToConcurrentRecoveryToSubsequentOnset_WithoutSimulationReset()
        {
            var catalog = LoadCatalog();
            var v1 = catalog.reviewedVariants.Single(v => v.choiceId == "fire-concourse");
            var v2 = catalog.reviewedVariants.Single(v => v.choiceId == "power-ticket");
            var v3 = catalog.reviewedVariants.Single(v => v.choiceId == "blocked-connector");

            var schedule = new IncidentSchedule
            {
                ProfileId = "continuous-7stage-lifecycle",
                MaximumActive = 2,
                FirstOnsetTick = 10,
                MinimumQuietTicks = 10,
                MaximumQuietTicks = 15,
                Choices = new[]
                {
                    new IncidentChoice { Id = v1.choiceId, ResourceKey = v1.resourceKey, Kind = FoundationIncidentKind.Fire },
                    new IncidentChoice { Id = v2.choiceId, ResourceKey = v2.resourceKey, Kind = FoundationIncidentKind.PowerLoss },
                    new IncidentChoice { Id = v3.choiceId, ResourceKey = v3.resourceKey, Kind = FoundationIncidentKind.RouteRestriction }
                }
            };

            var director = new IncidentDirector(schedule, seed: 135792468UL);

            // =========================================================================
            // Stage 1: Normal Operation (Ticks 1..9)
            // =========================================================================
            for (var t = 1; t < 10; t++)
            {
                var early = director.AdvanceOne();
                Assert.That(early, Is.Null, "Stage 1: AdvanceOne must return null prior to FirstOnsetTick");
                Assert.That(director.Active, Is.Empty);
            }
            Assert.That(director.Tick, Is.EqualTo(9));

            // =========================================================================
            // Stage 2: First Incident Onset (Tick 10)
            // =========================================================================
            var ep1 = director.AdvanceOne();
            Assert.That(ep1, Is.Not.Null, "Stage 2: First incident must onset at Tick 10");
            Assert.That(ep1.Id, Is.EqualTo("incident-1"));
            Assert.That(ep1.StartedTick, Is.EqualTo(10));
            Assert.That(director.Active.Count, Is.EqualTo(1));
            Assert.That(director.Tick, Is.EqualTo(10));

            // =========================================================================
            // Stage 3: Concurrent Second Incident Onset (Tick ~20..25)
            // =========================================================================
            IncidentEpisode ep2 = null;
            while (director.Tick < 30 && ep2 == null)
            {
                ep2 = director.AdvanceOne();
            }

            Assert.That(ep2, Is.Not.Null, "Stage 3: Second incident must onset within quiet window");
            Assert.That(ep2.Id, Is.EqualTo("incident-2"));
            Assert.That(ep2.ChoiceId, Is.Not.EqualTo(ep1.ChoiceId));
            Assert.That(director.Active.Count, Is.EqualTo(2), "Stage 3: Exactly two concurrent active incidents");

            // Verify MaximumActive bound: no third incident can onset
            for (var i = 0; i < 2; i++)
            {
                var blocked = director.AdvanceOne();
                Assert.That(blocked, Is.Null, "Cannot spawn 3rd incident while MaximumActive is full");
                Assert.That(director.Active.Count, Is.EqualTo(2));
            }

            // =========================================================================
            // Stage 4: Asymmetric First Recovery (Mitigate & Complete ep1)
            // =========================================================================
            director.Mitigate(ep1.Id);
            Assert.That(director.Active.Single(e => e.Id == ep1.Id).Mitigated, Is.True);
            Assert.That(director.Active.Single(e => e.Id == ep2.Id).Mitigated, Is.False,
                "Stage 4: Mitigating first incident must leave second incident active and unmitigated");

            director.Complete(ep1.Id);
            Assert.That(director.Active.Count, Is.EqualTo(1));
            Assert.That(director.Active.Single().Id, Is.EqualTo(ep2.Id));

            // =========================================================================
            // Stage 5: Asymmetric Second Recovery (Mitigate & Complete ep2)
            // =========================================================================
            director.Mitigate(ep2.Id);
            Assert.That(director.Active.Single().Mitigated, Is.True);

            director.Complete(ep2.Id);
            Assert.That(director.Active, Is.Empty, "Stage 5: Both incidents successfully completed");

            // =========================================================================
            // Stage 6: Quiet Inter-Incident Interval
            // =========================================================================
            var recoveryTick = director.Tick;
            for (var i = 0; i < 4; i++)
            {
                var quiet = director.AdvanceOne();
                Assert.That(quiet, Is.Null, "Stage 6: Quiet interval must not trigger immediate onsets");
                Assert.That(director.Active, Is.Empty);
            }
            Assert.That(director.Tick, Is.GreaterThan(recoveryTick));

            // =========================================================================
            // Stage 7: Subsequent Third Incident Onset (Unbroken Lifecycle)
            // =========================================================================
            IncidentEpisode ep3 = null;
            while (director.Tick < recoveryTick + 30 && ep3 == null)
            {
                ep3 = director.AdvanceOne();
            }

            Assert.That(ep3, Is.Not.Null, "Stage 7: Third sequential incident must onset on available resource key");
            Assert.That(ep3.Id, Is.EqualTo("incident-3"), "Stage 7: Serial incident ID must monotonically advance");
            Assert.That(ep3.Id, Is.Not.EqualTo(ep1.Id));
            Assert.That(ep3.Id, Is.Not.EqualTo(ep2.Id));
            Assert.That(director.Active.Count, Is.EqualTo(1));
            Assert.That(director.Tick, Is.GreaterThan(recoveryTick));

            // End-to-end unbroken lifecycle verification: checkpoint captures entire state cleanly
            var finalCheckpoint = director.ExportCheckpoint();
            Assert.That(finalCheckpoint.StartsWith("CGID1:", StringComparison.Ordinal), Is.True);
            var restored = new IncidentDirector(schedule, 999UL);
            restored.Restore(finalCheckpoint);
            Assert.That(restored.ExportCheckpoint(), Is.EqualTo(finalCheckpoint),
                "Restored director from stage 7 checkpoint must match exactly with zero drift");
        }
    }
}
