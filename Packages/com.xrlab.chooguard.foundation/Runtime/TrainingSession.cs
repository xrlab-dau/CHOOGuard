using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ChooGuard.Foundation
{
    // One session represents one selected role's attempt. Create a new session to choose again.
    // Call on one owner thread (normally Unity's main thread); this is not a network authority.
    public sealed class TrainingSession : ITrainingActionHandler
    {
        private readonly ScenarioProfile profile;
        private readonly string profileHash;
        private readonly Dictionary<string, RoleDefinition> roles;
        private readonly HashSet<string> acceptedAttemptIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly VirtualTeamSimulator team;
        private TrainingPhase phase = TrainingPhase.RoleSelection;
        private RoleDefinition selectedRole;
        private string questState = "pending";

        public string ScenarioId { get { return profile.scenarioId; } }
        public string ScenarioVersion { get { return profile.scenarioVersion; } }
        public string MapId { get { return profile.mapId; } }
        public string RepresentativeRoleId { get { return profile.representativeRoleId; } }
        public bool Provisional { get { return profile.provisional; } }
        public string Disclaimer { get { return profile.disclaimer; } }
        public IReadOnlyList<RoleBriefing> AvailableRoles { get; }
        public ITeamStateProvider TeamStateProvider { get { return team; } }
        public DescriptiveFeedback Feedback { get; private set; }

        public TrainingSnapshot Snapshot
        {
            get
            {
                var roleId = selectedRole == null ? string.Empty : selectedRole.roleId;
                return new TrainingSnapshot(phase, roleId, questState,
                    StateHash.Session(profileHash, phase, roleId, questState, team.States));
            }
        }

        public TrainingSession(ScenarioProfile scenario)
        {
            profile = ScenarioValidation.CopyValidated(scenario);
            profileHash = StateHash.Profile(profile);
            roles = profile.roles.ToDictionary(role => role.roleId, StringComparer.Ordinal);
            team = new VirtualTeamSimulator(roles.Keys);
            AvailableRoles = new ReadOnlyCollection<RoleBriefing>(profile.roles
                .Select(role => new RoleBriefing(role, profile.disclaimer)).ToList());
        }

        public RoleBriefing SelectRole(string roleId)
        {
            if (phase != TrainingPhase.RoleSelection)
                throw new InvalidOperationException("A role is already selected. Start a new session to choose again.");
            RoleDefinition role;
            if (roleId == null || !roles.TryGetValue(roleId, out role))
                throw new ArgumentException("Unknown provisional role.", nameof(roleId));
            selectedRole = role;
            phase = TrainingPhase.Briefing;
            return new RoleBriefing(role, profile.disclaimer);
        }

        public void AcknowledgeBriefing()
        {
            if (phase != TrainingPhase.Briefing)
                throw new InvalidOperationException("Select a role and show its briefing before acknowledging it.");
            phase = TrainingPhase.Ready;
        }

        public ActionResult Submit(in TrainingAction action)
        {
            var before = Snapshot;
            var validation = ValidateAction(in action, before);
            if (validation != ActionResultCode.Accepted)
                return new ActionResult(validation, before.QuestState,
                    new VirtualTeamEvent[0], null, before.PreStateHash);

            // Definitions are a private validated copy, and all effects are prepared before mutation.
            var events = selectedRole.expectedVirtualTeamEvents.Select(item =>
                new VirtualTeamEvent(item.roleId, item.eventCode, item.state)).ToArray();
            var feedback = new DescriptiveFeedback(selectedRole.expectedFeedbackCode,
                selectedRole.feedbackText, profile.disclaimer);
            team.ApplyValidated(events);
            questState = selectedRole.expectedQuestState;
            phase = TrainingPhase.Feedback;
            Feedback = feedback;
            acceptedAttemptIds.Add(action.AttemptId);
            return new ActionResult(ActionResultCode.Accepted, questState, events, feedback, Snapshot.PreStateHash);
        }

        public RoleActionFixture CreateRoleActionFixture()
        {
            if (phase != TrainingPhase.Ready)
                throw new InvalidOperationException("A role fixture requires an acknowledged briefing and an uncompleted quest.");
            return new RoleActionFixture
            {
                roleId = selectedRole.roleId, representative = selectedRole.roleId == profile.representativeRoleId,
                scenarioVersion = profile.scenarioVersion, actionId = selectedRole.actionId,
                targetAnchorId = selectedRole.targetAnchorId, preStateHash = Snapshot.PreStateHash,
                expectedQuestState = selectedRole.expectedQuestState,
                expectedVirtualTeamEvents = ScenarioValidation.CopyEvents(selectedRole.expectedVirtualTeamEvents),
                expectedFeedbackCode = selectedRole.expectedFeedbackCode
            };
        }

        private ActionResultCode ValidateAction(in TrainingAction action, TrainingSnapshot before)
        {
            if (string.IsNullOrWhiteSpace(action.AttemptId)) return ActionResultCode.InvalidAttemptId;
            if (acceptedAttemptIds.Contains(action.AttemptId)) return ActionResultCode.DuplicateAttempt;
            if (action.Modality != InputModality.VR && action.Modality != InputModality.Desktop)
                return ActionResultCode.InvalidModality;
            if (!string.Equals(action.ScenarioVersion, profile.scenarioVersion, StringComparison.Ordinal))
                return ActionResultCode.ScenarioVersionMismatch;
            if (selectedRole == null || !string.Equals(action.RoleId, selectedRole.roleId, StringComparison.Ordinal))
                return ActionResultCode.RoleMismatch;
            if (!string.Equals(action.TargetAnchorId, selectedRole.targetAnchorId, StringComparison.Ordinal))
                return ActionResultCode.TargetAnchorMismatch;
            if (!string.Equals(action.ActionId, selectedRole.actionId, StringComparison.Ordinal))
                return ActionResultCode.ActionMismatch;
            if (!string.Equals(action.PreStateHash, before.PreStateHash, StringComparison.Ordinal))
                return ActionResultCode.StaleState;
            if (phase != TrainingPhase.Ready) return ActionResultCode.NotReady;
            return ActionResultCode.Accepted;
        }
    }
}
