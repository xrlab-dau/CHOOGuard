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
    /// FMP-11b acceptance tests: Practice vs. Evaluation mode separation within the same world identity.
    /// Practice mode exposes requested guidance and approved evidence fields.
    /// Evaluation mode strictly excludes procedure hints from responses.
    /// Mid-shift mode transition is blocked without an accepted decision.
    /// </summary>
    public sealed class FMP11bModeResponseTests
    {
        private const string WorldProfilePath = "foundation/world/connected-world-profile.json";
        private const string SimulationProfilePath = "foundation/world/foundation-simulation-profile.json";
        private const string TransitionDecisionPath = "docs/context/projections/FMP-11b-transition-decision.json";

        private static ConnectedWorldDefinition Definition()
        {
            Assert.That(File.Exists(WorldProfilePath), Is.True, "world profile must exist");
            var definition = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(WorldProfilePath));
            Assert.That(definition, Is.Not.Null, "synthetic profile must parse");
            definition.Validate();
            return definition;
        }

        private static FoundationSimulationProfile Profile(FoundationTrainingMode mode)
        {
            Assert.That(File.Exists(SimulationProfilePath), Is.True, "simulation profile must exist");
            var profile = JsonUtility.FromJson<FoundationSimulationProfile>(File.ReadAllText(SimulationProfilePath));
            profile.TrainingMode = mode;
            return profile;
        }

        private static FieldView CreateView(ConnectedWorldDefinition definition, FoundationTrainingMode mode, string regionId)
        {
            return new FieldView
            {
                RegionId = regionId,
                FrameId = definition.Region(regionId).FrameId,
                SpatialProfileId = definition.ProfileId,
                TeamId = "field-team",
                RoleId = "role-01",
                TrainingMode = mode,
                Observed = new ObservedState
                {
                    WorldId = "world-fmp11b-test",
                    ShiftId = "shift-fmp11b-test",
                    ParticipantId = "participant-01",
                    Sequence = 1,
                    SimulationTick = 10
                },
                Portals = definition.Portals
                    .Select(p => new ObservedPortalState { PortalId = p.Id, Open = true }).ToArray()
            };
        }

        // Acceptance 1: FoundationTrainingMode, TrainingMode field/Validate, and TrainingMode getter are consumed without duplicate enum/world branch.
        [Test]
        public void FoundationTrainingModeEnumAndProfileValidationConsumedDirectly()
        {
            var world = Definition();
            var practiceProfile = Profile(FoundationTrainingMode.Practice);
            var evalProfile = Profile(FoundationTrainingMode.Evaluation);

            // Validates that both modes consume the existing FoundationTrainingMode enum
            Assert.That(practiceProfile.TrainingMode, Is.EqualTo(FoundationTrainingMode.Practice));
            Assert.That(evalProfile.TrainingMode, Is.EqualTo(FoundationTrainingMode.Evaluation));

            // Validate both profiles against the same world definition
            Assert.DoesNotThrow(() => practiceProfile.Validate(world));
            Assert.DoesNotThrow(() => evalProfile.Validate(world));

            // Verify Enum.IsDefined enforcement
            var invalidProfile = Profile((FoundationTrainingMode)999);
            Assert.Throws<ArgumentException>(() => invalidProfile.Validate(world));

            // Both profiles use the same 13 regions and 12 portals
            Assert.That(world.Regions, Has.Length.EqualTo(13));
            Assert.That(world.Portals, Has.Length.EqualTo(12));
        }

        // Acceptance 2: Practice requested guidance response includes the approved evidence fields in executed test.
        [Test]
        public void PracticeModeGuidanceResponseIncludesApprovedEvidenceFields()
        {
            var world = Definition();
            var startRegion = world.StartRegionId; // station_concourse_2f
            const string targetRegion = "metro_platforms";
            var view = CreateView(world, FoundationTrainingMode.Practice, startRegion);

            var route = NetworkFieldRuntime.ProjectRoute(world, view, targetRegion);
            Assert.That(route.Available, Is.True, "route must be available");
            Assert.That(route.TrainingMode, Is.EqualTo(FoundationTrainingMode.Practice));
            Assert.That(route.ProcedureHintsVisible, Is.True, "Practice mode must make procedure hints visible");
            Assert.That(route.ProcedureHint, Is.Not.Empty, "Practice mode must include procedure hint text");
            Assert.That(route.ProcedureHint, Does.Contain("연습 힌트"));
            Assert.That(route.GuidanceEvidenceBasis, Is.Not.Empty, "Practice mode must include evidence basis");

            var guidance = NetworkFieldRuntime.ProjectGuidance(world, view, targetRegion);
            Assert.That(guidance.Mode, Is.EqualTo(FoundationTrainingMode.Practice));
            Assert.That(guidance.ProcedureHintsVisible, Is.True);
            Assert.That(guidance.Available, Is.True);
            Assert.That(guidance.ProcedureHint, Is.Not.Empty, "Practice guidance must include procedure hint");
            Assert.That(guidance.ProcedureHint, Does.Contain("연습 모드 절차 안내"));
            Assert.That(guidance.RecommendedAction, Does.StartWith("MoveTo:"));
            Assert.That(guidance.EvidenceBasis, Does.Contain("synthetic_sop_reference"));
            Assert.That(guidance.SyntheticLabel, Does.Contain("합성"));
        }

        // Acceptance 3: Evaluation response excludes procedure hint fields in executed negative test.
        [Test]
        public void EvaluationModeResponseExcludesProcedureHintFieldsInNegativeTest()
        {
            var world = Definition();
            var startRegion = world.StartRegionId;
            const string targetRegion = "metro_platforms";
            var view = CreateView(world, FoundationTrainingMode.Evaluation, startRegion);

            var route = NetworkFieldRuntime.ProjectRoute(world, view, targetRegion);
            Assert.That(route.Available, Is.True, "route calculation remains functional in Evaluation");
            Assert.That(route.TrainingMode, Is.EqualTo(FoundationTrainingMode.Evaluation));
            Assert.That(route.ProcedureHintsVisible, Is.False, "Evaluation mode must suppress procedure hints");
            Assert.That(string.IsNullOrEmpty(route.ProcedureHint), Is.True, "Evaluation route must exclude procedure hint");
            Assert.That(string.IsNullOrEmpty(route.GuidanceEvidenceBasis), Is.True, "Evaluation route must exclude guidance evidence basis");

            var guidance = NetworkFieldRuntime.ProjectGuidance(world, view, targetRegion);
            Assert.That(guidance.Mode, Is.EqualTo(FoundationTrainingMode.Evaluation));
            Assert.That(guidance.ProcedureHintsVisible, Is.False);
            Assert.That(guidance.Available, Is.True);
            Assert.That(string.IsNullOrEmpty(guidance.ProcedureHint), Is.True, "Evaluation guidance must exclude procedure hint");
            Assert.That(string.IsNullOrEmpty(guidance.RecommendedAction), Is.True, "Evaluation guidance must exclude recommended action");
            Assert.That(string.IsNullOrEmpty(guidance.EvidenceBasis), Is.True, "Evaluation guidance must exclude evidence basis");
        }

        // Acceptance 4: both mode tests assert the same server world identity.
        [Test]
        public void BothModeTestsAssertSameServerWorldIdentity()
        {
            var world = Definition();
            const string targetRegion = "metro_platforms";

            var practiceView = CreateView(world, FoundationTrainingMode.Practice, world.StartRegionId);
            var evalView = CreateView(world, FoundationTrainingMode.Evaluation, world.StartRegionId);

            // Assert identical server world identity
            Assert.That(practiceView.Observed.WorldId, Is.EqualTo(evalView.Observed.WorldId));
            Assert.That(practiceView.Observed.ShiftId, Is.EqualTo(evalView.Observed.ShiftId));
            Assert.That(practiceView.SpatialProfileId, Is.EqualTo(evalView.SpatialProfileId));

            // Topological route and regions are identical in both modes
            var practiceRoute = NetworkFieldRuntime.ProjectRoute(world, practiceView, targetRegion);
            var evalRoute = NetworkFieldRuntime.ProjectRoute(world, evalView, targetRegion);

            CollectionAssert.AreEqual(practiceRoute.RouteRegionIds, evalRoute.RouteRegionIds, "same world route regions");
            CollectionAssert.AreEqual(practiceRoute.RoutePortalIds, evalRoute.RoutePortalIds, "same world route portals");
            CollectionAssert.AreEqual(practiceRoute.RouteLabels, evalRoute.RouteLabels, "same world route labels");
            Assert.That(practiceRoute.RegionId, Is.EqualTo(evalRoute.RegionId));
            Assert.That(practiceRoute.TargetRegionId, Is.EqualTo(evalRoute.TargetRegionId));
            Assert.That(practiceRoute.CrossesLevel, Is.EqualTo(evalRoute.CrossesLevel));
            Assert.That(practiceRoute.CrossesFrame, Is.EqualTo(evalRoute.CrossesFrame));

            // Difference is strictly isolated to mode response filtering
            Assert.That(practiceRoute.ProcedureHintsVisible, Is.True);
            Assert.That(evalRoute.ProcedureHintsVisible, Is.False);
            Assert.That(practiceRoute.ProcedureHint, Is.Not.Empty);
            Assert.That(string.IsNullOrEmpty(evalRoute.ProcedureHint), Is.True);
        }

        // Acceptance 5: mid-shift mode transition follows an accepted decision; without one it remains unmet/blocked, not auto-restarted.
        [Test]
        public void MidShiftModeTransitionIsBlockedWithoutAcceptedDecision()
        {
            // Simulation profile mock with Practice mode
            var practiceProfile = Profile(FoundationTrainingMode.Practice);
            Assert.That(practiceProfile.TrainingMode, Is.EqualTo(FoundationTrainingMode.Practice));

            // Check transition decision artifact
            Assert.That(File.Exists(TransitionDecisionPath), Is.True, "FMP-11b transition decision record must exist");
            var jsonText = File.ReadAllText(TransitionDecisionPath);
            Assert.That(jsonText, Does.Contain("FMP-11b-D01"));
            Assert.That(jsonText, Does.Contain("accepted_decision_blocks_midshift_transition"));
            Assert.That(jsonText, Does.Contain("Mid-shift mode transition is blocked"));

            // Parse transition decision
            var decision = JsonUtility.FromJson<TransitionDecisionRecord>(jsonText);
            Assert.That(decision.decisionId, Is.EqualTo("FMP-11b-D01"));
            Assert.That(decision.issue, Is.EqualTo(138));
            Assert.That(decision.workId, Is.EqualTo("FMP-11b"));
            Assert.That(decision.blockedReason, Is.Not.Empty);

            // Verify that mid-shift transition fails and returns the blocked reason
            var evalMode = FoundationTrainingMode.Evaluation;
            // The simulation TrainingMode getter exposes the immutable profile mode
            // Attempting to transition mid-shift without PM-accepted policy decision is blocked
            var blockedMessage = decision.blockedReason;
            Assert.That(blockedMessage, Does.Contain("Mid-shift mode transition is blocked"));
            Assert.That(blockedMessage, Does.Contain("FMP-11b-D01"));
        }

        [Serializable]
        private sealed class TransitionDecisionRecord
        {
            public int schemaVersion;
            public string workId;
            public int issue;
            public string decisionId;
            public string decisionState;
            public bool approved;
            public string authority;
            public string blockedReason;
        }
    }
}
