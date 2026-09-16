using System;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Multiplayer.Editor;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    /// <summary>
    /// FMP-11b acceptance tests: Practice versus Evaluation mode separation inside the same server world
    /// identity. Practice responses expose the requested synthetic guidance; Evaluation responses strictly
    /// exclude procedure hints; a mid-shift mode transition is refused while the shift is live.
    /// Every mode, world identity and pose below is read from a real <see cref="FoundationWorldSimulation"/>
    /// and <see cref="AuthoritativeShift"/> fixture, never from a hand-written literal. Nothing here is a
    /// surveyed facility, an accepted staff procedure or a facility acceptance claim.
    /// </summary>
    public sealed class FMP11bModeResponseTests
    {
        private const string WorldProfilePath = "foundation/world/connected-world-profile.json";
        private const string SimulationProfilePath = "foundation/world/foundation-simulation-profile.json";
        private const string WorldLayoutPath = "foundation/network/connected-world-layout.json";
        private const string TransitionDecisionPath = "docs/context/projections/FMP-11b-transition-decision.json";
        private const string ParticipantId = "fmp11b-instructor";
        private const string TargetRegion = "metro_platforms";

        private SceneSetup[] previousSetup;
        private string generatedFolder;
        private ConnectedWorldDefinition world;
        private ConnectedRegionView[] views;
        private FoundationWorldSimulation practiceSimulation, evaluationSimulation;
        private AuthoritativeShift practiceShift, evaluationShift;

        [OneTimeSetUp] public void BuildServerFixture()
        {
            previousSetup = EditorSceneManager.GetSceneManagerSetup();
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) Assert.Ignore("Preserve unsaved scenes.");
            generatedFolder = "Assets/CHOOguardGenerated/FMP11bMode_" + Guid.NewGuid().ToString("N");
            var paths = ConnectedWorldSceneBuilder.Build(generatedFolder);
            foreach (var path in paths.Skip(1)) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            world = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(ConnectedWorldSceneBuilder.DefinitionPath));
            world.Validate();
            views = UnityEngine.Object.FindObjectsByType<ConnectedRegionView>(FindObjectsSortMode.None);
            Physics.SyncTransforms();
            practiceSimulation = new FoundationWorldSimulation(world, views, Profile(world, FoundationTrainingMode.Practice), Seed(world));
            evaluationSimulation = new FoundationWorldSimulation(world, views, Profile(world, FoundationTrainingMode.Evaluation), Seed(world));
            practiceShift = Shift(practiceSimulation);
            evaluationShift = Shift(evaluationSimulation);
        }

        [OneTimeTearDown] public void ClearServerFixture()
        {
            practiceSimulation?.Dispose(); evaluationSimulation?.Dispose();
            practiceSimulation = null; evaluationSimulation = null;
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (previousSetup != null && previousSetup.Any(s => s.isActive && s.isLoaded) && previousSetup.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            if (generatedFolder != null) AssetDatabase.DeleteAsset(generatedFolder);
        }

        private sealed class Sink : ICommitSink { public void Append(ShiftCommit commit) { } }

        private static AuthoritativeShift Shift(FoundationWorldSimulation simulation) =>
            new AuthoritativeShift(simulation.InitialState, new Sink(), (_, __) => true, simulation.CanOperate);

        /// <summary>Authored synthetic simulation profile with the training mode under test. NpcCount is
        /// reduced only to keep the fixture small; the world definition and incident bindings are authored.</summary>
        private static FoundationSimulationProfile Profile(ConnectedWorldDefinition world, FoundationTrainingMode mode)
        {
            var profile = JsonUtility.FromJson<FoundationSimulationProfile>(File.ReadAllText(SimulationProfilePath));
            profile.TrainingMode = mode;
            profile.NpcCount = 2; profile.NpcSpawnRegions = new[] { world.StartRegionId };
            return profile;
        }

        /// <summary>Same authored profile with an onset on the first tick so the mid-shift guard is exercised
        /// on a shift that already has live incidents, the way the coupled server runs it.</summary>
        private static FoundationSimulationProfile IncidentProfile(ConnectedWorldDefinition world, FoundationTrainingMode mode)
        {
            var profile = Profile(world, mode);
            profile.Schedule.FirstOnsetTick = profile.Schedule.MinimumQuietTicks = profile.Schedule.MaximumQuietTicks = 1;
            profile.Schedule.Choices = profile.Schedule.Choices.Where(c => c.Kind == FoundationIncidentKind.Fire).ToArray();
            profile.Incidents = profile.Incidents.Where(i => profile.Schedule.Choices.Any(c => c.Id == i.ChoiceId)).ToArray();
            return profile;
        }

        private static WorldState Seed(ConnectedWorldDefinition world)
        {
            var state = JsonUtility.FromJson<WorldState>(File.ReadAllText(WorldLayoutPath));
            var region = world.Region(world.StartRegionId); var pose = world.Pose(region.Id, region.Hub);
            state.Participants = new[] { new ParticipantState { ParticipantId = ParticipantId, TeamId = "command", RoleId = "instructor",
                IsInstructor = true, RegionId = pose.RegionId, FrameId = pose.FrameId, Position = pose.Position, LocalPosition = pose.LocalPosition } };
            return state;
        }

        /// <summary>The client field view the server would send: identity, pose and training mode come from the
        /// running simulation and its command authority. Only the observed portal states are synthesized open,
        /// because the closure channel is a client observation and this fixture asserts topology, not closures.</summary>
        private static FieldView ServerView(ConnectedWorldDefinition definition, FoundationWorldSimulation simulation, AuthoritativeShift shift)
        {
            var actor = shift.Participant(ParticipantId);
            return new FieldView
            {
                Observed = shift.Observe(ParticipantId),
                Position = actor.Position,
                LocalPosition = actor.LocalPosition,
                TeamId = actor.TeamId,
                RoleId = actor.RoleId,
                Instructor = actor.IsInstructor,
                RegionId = actor.RegionId,
                FrameId = actor.FrameId,
                PortalId = actor.PortalId,
                SpatialProfileId = definition.ProfileId,
                RequiredRegions = definition.RequiredRegions(actor.RegionId),
                TrainingMode = NetworkFieldRuntime.ProjectedTrainingMode(simulation),
                Portals = definition.Portals.Select(p => new ObservedPortalState { PortalId = p.Id, Open = true }).ToArray()
            };
        }

        [Serializable] private sealed class TransitionDecisionRecord
        {
            public int schemaVersion;
            public string workId;
            public int issue;
            public string decisionId;
            public string decisionState;
            public string recordKind;
            public bool approved;
            public string approvalRef;
            public bool canonicalWriteAllowed;
            public string proposalMode;
            public string blockedReason;
        }

        // Acceptance 1: both modes consume the existing FoundationTrainingMode enum, the TrainingMode profile
        // field/validation and the FoundationWorldSimulation getter, with no duplicate mode vocabulary.
        [Test]
        public void FoundationTrainingModeEnumAndProfileValidationConsumedDirectly()
        {
            Assert.That(world.Regions, Has.Length.EqualTo(13), "profile region count");
            Assert.That(world.Portals, Has.Length.EqualTo(12), "profile portal count");
            Assert.That(ConnectedWorldSceneBuilder.DefinitionPath, Is.EqualTo(WorldProfilePath),
                "the fixture world must be the authored profile the route projection suite consumes");

            // The mode vocabulary is exactly the one existing enum; no second enum or parallel world branch.
            Assert.That(Enum.GetValues(typeof(FoundationTrainingMode)).Cast<FoundationTrainingMode>(),
                Is.EquivalentTo(new[] { FoundationTrainingMode.Practice, FoundationTrainingMode.Evaluation }),
                "training modes must come from the existing enum, not a redefinition");

            // Both modes consume the same profile contract and the same authored world definition.
            Assert.DoesNotThrow(() => Profile(world, FoundationTrainingMode.Practice).Validate(world));
            Assert.DoesNotThrow(() => Profile(world, FoundationTrainingMode.Evaluation).Validate(world));
            Assert.Throws<ArgumentException>(() => Profile(world, (FoundationTrainingMode)999).Validate(world),
                "an undefined mode must be rejected by the existing profile validation");

            // The running server simulation, not the test, carries the mode.
            Assert.That(practiceSimulation.TrainingMode, Is.EqualTo(FoundationTrainingMode.Practice));
            Assert.That(evaluationSimulation.TrainingMode, Is.EqualTo(FoundationTrainingMode.Evaluation));
            Assert.That(practiceSimulation.NpcCount, Is.EqualTo(evaluationSimulation.NpcCount));

            // The projection helper the server stamps views with reads that simulation; without a coupled
            // simulation it falls back to Practice instead of forking a second world branch.
            Assert.That(NetworkFieldRuntime.ProjectedTrainingMode(practiceSimulation), Is.EqualTo(FoundationTrainingMode.Practice));
            Assert.That(NetworkFieldRuntime.ProjectedTrainingMode(evaluationSimulation), Is.EqualTo(FoundationTrainingMode.Evaluation));
            Assert.That(NetworkFieldRuntime.ProjectedTrainingMode(null), Is.EqualTo(FoundationTrainingMode.Practice));
        }

        // Acceptance 2: in Practice mode the requested guidance response includes the synthetic evidence fields.
        [Test]
        public void PracticeModeGuidanceResponseIncludesApprovedEvidenceFields()
        {
            var view = ServerView(world, practiceSimulation, practiceShift);
            Assert.That(view.TrainingMode, Is.EqualTo(FoundationTrainingMode.Practice), "mode must come from the running server simulation");

            var route = NetworkFieldRuntime.ProjectRoute(world, view, TargetRegion);
            Assert.That(route.Available, Is.True, "route must be available");
            Assert.That(route.TrainingMode, Is.EqualTo(FoundationTrainingMode.Practice));
            Assert.That(route.ProcedureHintsVisible, Is.True, "Practice mode must make procedure hints visible");
            Assert.That(route.ProcedureHint, Is.Not.Empty, "Practice mode must include procedure hint text");
            Assert.That(route.ProcedureHint, Does.Contain("연습 힌트"));
            Assert.That(route.GuidanceEvidenceBasis, Is.Not.Empty, "Practice mode must include evidence basis");

            var guidance = NetworkFieldRuntime.ProjectGuidance(world, view, TargetRegion);
            Assert.That(guidance.Mode, Is.EqualTo(FoundationTrainingMode.Practice));
            Assert.That(guidance.ProcedureHintsVisible, Is.True);
            Assert.That(guidance.Available, Is.True);
            Assert.That(guidance.WorldId, Is.EqualTo(view.Observed.WorldId), "guidance must carry the server world identity");
            Assert.That(guidance.ProcedureHint, Is.Not.Empty, "Practice guidance must include procedure hint");
            Assert.That(guidance.ProcedureHint, Does.Contain("연습 모드 절차 안내"));
            Assert.That(guidance.RecommendedAction, Does.StartWith("MoveTo:"));
            Assert.That(guidance.EvidenceBasis, Does.Contain("synthetic_sop_reference"));
            Assert.That(guidance.SyntheticLabel, Does.Contain("합성"));
        }

        // Acceptance 3: in Evaluation mode the response excludes the procedure hint fields (negative test).
        [Test]
        public void EvaluationModeResponseExcludesProcedureHintFieldsInNegativeTest()
        {
            var view = ServerView(world, evaluationSimulation, evaluationShift);
            Assert.That(view.TrainingMode, Is.EqualTo(FoundationTrainingMode.Evaluation), "mode must come from the running server simulation");

            var route = NetworkFieldRuntime.ProjectRoute(world, view, TargetRegion);
            Assert.That(route.Available, Is.True, "route calculation remains functional in Evaluation");
            Assert.That(route.TrainingMode, Is.EqualTo(FoundationTrainingMode.Evaluation));
            Assert.That(route.ProcedureHintsVisible, Is.False, "Evaluation mode must suppress procedure hints");
            Assert.That(string.IsNullOrEmpty(route.ProcedureHint), Is.True, "Evaluation route must exclude procedure hint");
            Assert.That(string.IsNullOrEmpty(route.GuidanceEvidenceBasis), Is.True, "Evaluation route must exclude guidance evidence basis");

            var guidance = NetworkFieldRuntime.ProjectGuidance(world, view, TargetRegion);
            Assert.That(guidance.Mode, Is.EqualTo(FoundationTrainingMode.Evaluation));
            Assert.That(guidance.ProcedureHintsVisible, Is.False);
            Assert.That(guidance.Available, Is.True);
            Assert.That(string.IsNullOrEmpty(guidance.ProcedureHint), Is.True, "Evaluation guidance must exclude procedure hint");
            Assert.That(string.IsNullOrEmpty(guidance.RecommendedAction), Is.True, "Evaluation guidance must exclude recommended action");
            Assert.That(string.IsNullOrEmpty(guidance.EvidenceBasis), Is.True, "Evaluation guidance must exclude evidence basis");
        }

        // Acceptance 4: both modes keep the same server world identity and topology, read through the actual
        // server projection (AuthoritativeShift.Observe) rather than two hand-written identical strings.
        [Test]
        public void BothModesProjectTheSameServerWorldIdentityAndTopology()
        {
            var practiceView = ServerView(world, practiceSimulation, practiceShift);
            var evaluationView = ServerView(world, evaluationSimulation, evaluationShift);

            // The two simulations really are different modes ...
            Assert.That(practiceView.TrainingMode, Is.EqualTo(FoundationTrainingMode.Practice));
            Assert.That(evaluationView.TrainingMode, Is.EqualTo(FoundationTrainingMode.Evaluation));

            // ... and the server identity they project is the same one, grounded in the server's own state.
            Assert.That(practiceView.Observed.WorldId, Is.EqualTo(practiceSimulation.InitialState.WorldId), "server world identity");
            Assert.That(practiceView.Observed.ShiftId, Is.EqualTo(practiceSimulation.InitialState.ShiftId), "server shift identity");
            Assert.That(practiceView.Observed.WorldId, Is.Not.Empty);
            Assert.That(practiceView.Observed.WorldId, Is.EqualTo(evaluationView.Observed.WorldId), "same world in both modes");
            Assert.That(practiceView.Observed.ShiftId, Is.EqualTo(evaluationView.Observed.ShiftId), "same shift in both modes");
            Assert.That(practiceView.RegionId, Is.EqualTo(evaluationView.RegionId), "same actor region in both modes");
            Assert.That(practiceView.SpatialProfileId, Is.EqualTo(evaluationView.SpatialProfileId));

            // Topology is identical; the mode is not a second world branch.
            var practiceRoute = NetworkFieldRuntime.ProjectRoute(world, practiceView, TargetRegion);
            var evaluationRoute = NetworkFieldRuntime.ProjectRoute(world, evaluationView, TargetRegion);
            Assert.That(practiceRoute.Available, Is.True);
            Assert.That(evaluationRoute.Available, Is.True);
            CollectionAssert.AreEqual(practiceRoute.RouteRegionIds, evaluationRoute.RouteRegionIds, "same world route regions");
            CollectionAssert.AreEqual(practiceRoute.RoutePortalIds, evaluationRoute.RoutePortalIds, "same world route portals");
            CollectionAssert.AreEqual(practiceRoute.RouteLabels, evaluationRoute.RouteLabels, "same world route labels");
            Assert.That(practiceRoute.RegionId, Is.EqualTo(evaluationRoute.RegionId));
            Assert.That(practiceRoute.TargetRegionId, Is.EqualTo(evaluationRoute.TargetRegionId));
            Assert.That(practiceRoute.CrossesLevel, Is.EqualTo(evaluationRoute.CrossesLevel));
            Assert.That(practiceRoute.CrossesFrame, Is.EqualTo(evaluationRoute.CrossesFrame));
            Assert.That(practiceRoute.RouteSyntheticLabel, Is.EqualTo(evaluationRoute.RouteSyntheticLabel));

            // The difference is isolated to the mode response filtering, not to identity or topology.
            Assert.That(practiceRoute.ProcedureHintsVisible, Is.True);
            Assert.That(evaluationRoute.ProcedureHintsVisible, Is.False);
            Assert.That(practiceRoute.ProcedureHint, Is.Not.Empty);
            Assert.That(string.IsNullOrEmpty(evaluationRoute.ProcedureHint), Is.True);

            // Guard the projection that the existing PlayMode suite also asserts: adding the mode field must
            // not empty the authored geometry status, which the route label above consumes.
            Assert.That(practiceRoute.GeometryStatus, Is.EqualTo(world.Region(practiceView.RegionId).GeometryStatus),
                "the mode field must not replace the authored geometry status");
            Assert.That(practiceRoute.RouteSyntheticLabel, Does.Contain(practiceRoute.GeometryStatus),
                "the route label must still carry the current region geometry status");
        }

        // Acceptance 4b: the current-location projection keeps the authored geometry status that the synthetic
        // banner and the route label consume. Deleting that assignment must fail here.
        [Test]
        public void ModeFieldDoesNotReplaceGeometryStatusOrRouteLabelInputs()
        {
            var view = ServerView(world, practiceSimulation, practiceShift);
            var region = world.Region(view.RegionId);

            var current = NetworkFieldRuntime.ProjectCurrentLocation(world, view);
            Assert.That(current.RegionId, Is.EqualTo(region.Id));
            Assert.That(current.GeometryStatus, Is.EqualTo(region.GeometryStatus), "authored geometry status must survive the mode field");
            Assert.That(current.GeometryStatus, Is.Not.Empty, "an empty geometry status means the existing assignment was deleted");
            Assert.That(current.SyntheticLabel, Does.Contain(region.GeometryStatus));
            Assert.That(current.TrainingMode, Is.EqualTo(FoundationTrainingMode.Practice));
            Assert.That(current.ProcedureHintsVisible, Is.True);

            var evaluationView = ServerView(world, evaluationSimulation, evaluationShift);
            var evaluationCurrent = NetworkFieldRuntime.ProjectCurrentLocation(world, evaluationView);
            Assert.That(evaluationCurrent.GeometryStatus, Is.EqualTo(region.GeometryStatus));
            Assert.That(evaluationCurrent.SyntheticLabel, Does.Contain(region.GeometryStatus));
            Assert.That(evaluationCurrent.ProcedureHintsVisible, Is.False);

            var route = NetworkFieldRuntime.ProjectRoute(world, evaluationView, TargetRegion);
            Assert.That(route.RouteSyntheticLabel, Does.Contain(region.GeometryStatus),
                "the route synthetic label is built from the current region geometry status");
            Assert.That(route.RouteSyntheticLabel, Does.Contain(string.Join(", ", route.RouteGeometryStatuses.Distinct())),
                "the route synthetic label still carries the authored portal geometry statuses");
        }

        // Acceptance 5: a mid-shift mode transition without an accepted decision is refused by the real
        // simulation, leaves the shift untouched, and a same-mode request is an idempotent no-op.
        [Test]
        public void MidShiftModeTransitionIsBlockedWithoutAcceptedDecision()
        {
            using var simulation = new FoundationWorldSimulation(world, views, IncidentProfile(world, FoundationTrainingMode.Practice), Seed(world));
            var shift = Shift(simulation);
            var input = new[] { new ServerMovementInput { ParticipantId = ParticipantId, LoadedRegions = world.Regions.Select(r => r.Id).ToArray() } };
            for (var tick = 0; tick < 3; tick++) Assert.That(simulation.TryAdvance(shift, input, out var report), Is.True, report.Failure);

            var modeBefore = simulation.TrainingMode;
            var tickBefore = simulation.Tick;
            var activeBefore = simulation.ActiveIncidentCount;
            var boundaryBefore = shift.ExportCheckpoint();
            Assert.That(modeBefore, Is.EqualTo(FoundationTrainingMode.Practice));
            Assert.That(tickBefore, Is.EqualTo(3), "the guard must be exercised on a live, advanced shift");
            Assert.That(activeBefore, Is.GreaterThan(0), "a live incident must exist for the invariance assertion to mean anything");

            // A real mode change request against the live shift is refused, with the documented reason.
            Assert.That(simulation.TryTransitionMode(FoundationTrainingMode.Evaluation, out var blockedReason), Is.False,
                "a mid-shift mode change must be refused while the shift is live");
            Assert.That(blockedReason, Is.EqualTo(FoundationWorldSimulation.MidShiftTransitionBlockedReason),
                "the refusal must return the documented blocked-reason contract");
            Assert.That(blockedReason, Does.Contain("Mid-shift mode transition is blocked"));
            Assert.That(blockedReason, Does.Contain("FMP-11b-D01"));
            Assert.That(blockedReason, Is.Not.Empty);

            // The refusal must not mutate, restart or clear the running shift.
            Assert.That(simulation.TrainingMode, Is.EqualTo(modeBefore), "a refused transition must not change the mode");
            Assert.That(simulation.Tick, Is.EqualTo(tickBefore), "a refused transition must not restart the shift clock");
            Assert.That(simulation.ActiveIncidentCount, Is.EqualTo(activeBefore), "a refused transition must not clear live incidents");
            var boundaryAfter = shift.ExportCheckpoint();
            Assert.That(boundaryAfter.SimulationTick, Is.EqualTo(boundaryBefore.SimulationTick));
            Assert.That(boundaryAfter.SimulationCheckpoint, Is.EqualTo(boundaryBefore.SimulationCheckpoint),
                "a refused transition must not auto-restart the shift");
            Assert.That(boundaryAfter.Participants.Single(p => p.ParticipantId == ParticipantId).RegionId,
                Is.EqualTo(boundaryBefore.Participants.Single(p => p.ParticipantId == ParticipantId).RegionId));

            // Same-mode request semantics: idempotent no-op, reports success, still changes nothing.
            Assert.That(simulation.TryTransitionMode(FoundationTrainingMode.Practice, out var sameModeReason), Is.True,
                "requesting the mode the live shift already runs is an idempotent no-op");
            Assert.That(sameModeReason, Is.Empty);
            Assert.That(simulation.TrainingMode, Is.EqualTo(modeBefore));
            Assert.That(simulation.Tick, Is.EqualTo(tickBefore));
            Assert.That(simulation.ActiveIncidentCount, Is.EqualTo(activeBefore));
            Assert.That(shift.ExportCheckpoint().SimulationCheckpoint, Is.EqualTo(boundaryBefore.SimulationCheckpoint));

            // The guard is symmetric: an Evaluation shift may not switch to Practice either.
            using var reverseSimulation = new FoundationWorldSimulation(world, views, IncidentProfile(world, FoundationTrainingMode.Evaluation), Seed(world));
            var reverseBoundary = Shift(reverseSimulation).ExportCheckpoint();
            Assert.That(reverseSimulation.TrainingMode, Is.EqualTo(FoundationTrainingMode.Evaluation));
            Assert.That(reverseSimulation.TryTransitionMode(FoundationTrainingMode.Practice, out var reverseReason), Is.False,
                "an Evaluation shift must not switch to Practice either");
            Assert.That(reverseReason, Is.EqualTo(FoundationWorldSimulation.MidShiftTransitionBlockedReason));
            Assert.That(reverseSimulation.TrainingMode, Is.EqualTo(FoundationTrainingMode.Evaluation), "a refused transition must not change the mode");
            Assert.That(reverseSimulation.Tick, Is.EqualTo(reverseBoundary.SimulationTick));
            Assert.That(reverseSimulation.ActiveIncidentCount, Is.EqualTo(0));
        }

        // Acceptance 6: the transition decision artifact cannot substantiate an approval claim. No PM decision
        // ref is linked, so it must record an unapproved proposal, not an accepted policy, and must not claim
        // canonical write authority. Implementing a safe default block is not evidence that the policy was approved.
        [Test]
        public void TransitionDecisionArtifactIsAnUnapprovedProposalWithoutApprovalRef()
        {
            Assert.That(File.Exists(TransitionDecisionPath), Is.True, "FMP-11b transition decision record must exist");
            var jsonText = File.ReadAllText(TransitionDecisionPath);
            Assert.That(jsonText, Does.Contain("FMP-11b-D01"));
            Assert.That(jsonText, Does.Contain("Mid-shift mode transition is blocked"));

            var decision = JsonUtility.FromJson<TransitionDecisionRecord>(jsonText);
            Assert.That(decision, Is.Not.Null);
            Assert.That(decision.schemaVersion, Is.EqualTo(1));
            Assert.That(decision.workId, Is.EqualTo("FMP-11b"));
            Assert.That(decision.issue, Is.EqualTo(138));
            Assert.That(decision.decisionId, Is.EqualTo("FMP-11b-D01"));

            // No verifiable PM decision ref is linked, so approval must not be claimed.
            Assert.That(decision.approvalRef, Is.Empty,
                "no verifiable PM decision ref is linked by this record, so it must not be fabricated");
            Assert.That(decision.approved, Is.False,
                "an artifact without a linked PM decision ref cannot claim an approved policy decision");
            Assert.That(decision.canonicalWriteAllowed, Is.False,
                "an unapproved proposal must not claim canonical write authority");
            Assert.That(decision.recordKind, Is.EqualTo("proposal_not_approved"));
            Assert.That(decision.decisionState, Is.EqualTo("unapproved_proposal_blocked_by_default"));
            Assert.That(decision.proposalMode, Is.EqualTo("exclusive"), "the proposal is scoped exclusively to FMP-11b");

            // The record documents the code's real refusal contract, not a wish.
            Assert.That(decision.blockedReason, Is.Not.Empty);
            Assert.That(decision.blockedReason, Is.EqualTo(FoundationWorldSimulation.MidShiftTransitionBlockedReason),
                "the recorded blocked reason must be the contract the server actually returns");
        }
    }
}
