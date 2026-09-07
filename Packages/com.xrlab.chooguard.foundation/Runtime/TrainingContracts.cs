using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ChooGuard.Foundation
{
    public enum InputModality { VR, Desktop }
    public enum TrainingPhase { RoleSelection, Briefing, Ready, Feedback }
    public enum ActionResultCode
    {
        Accepted, InvalidAttemptId, DuplicateAttempt, InvalidModality,
        ScenarioVersionMismatch, RoleMismatch, TargetAnchorMismatch,
        ActionMismatch, StaleState, NotReady
    }

    // C# 7.3 equivalent of the architectural record contract. Input adapters only submit actions.
    public readonly struct TrainingAction
    {
        public string AttemptId { get; }
        public string ScenarioVersion { get; }
        public string RoleId { get; }
        public string PreStateHash { get; }
        public string ActionId { get; }
        public string TargetAnchorId { get; }
        public InputModality Modality { get; }

        public TrainingAction(string attemptId, string scenarioVersion, string roleId,
            string preStateHash, string actionId, string targetAnchorId, InputModality modality)
        {
            AttemptId = attemptId;
            ScenarioVersion = scenarioVersion;
            RoleId = roleId;
            PreStateHash = preStateHash;
            ActionId = actionId;
            TargetAnchorId = targetAnchorId;
            Modality = modality;
        }
    }

    public interface ITrainingActionHandler
    {
        ActionResult Submit(in TrainingAction action);
    }

    public readonly struct VirtualTeamEvent
    {
        public string RoleId { get; }
        public string EventCode { get; }
        public string State { get; }

        public VirtualTeamEvent(string roleId, string eventCode, string state)
        {
            RoleId = roleId;
            EventCode = eventCode;
            State = state;
        }
    }

    public readonly struct TeamRoleState
    {
        public string RoleId { get; }
        public string State { get; }

        public TeamRoleState(string roleId, string state) { RoleId = roleId; State = state; }
    }

    public interface ITeamStateProvider
    {
        IReadOnlyList<TeamRoleState> States { get; }
    }

    public sealed class DescriptiveFeedback
    {
        public string Code { get; }
        public string Text { get; }
        public string Disclaimer { get; }

        internal DescriptiveFeedback(string code, string text, string disclaimer)
        {
            Code = code;
            Text = text;
            Disclaimer = disclaimer;
        }
    }

    public sealed class RoleBriefing
    {
        public string RoleId { get; }
        public string TemporaryDisplayName { get; }
        public string Text { get; }
        public string ActionId { get; }
        public string TargetAnchorId { get; }
        public string Disclaimer { get; }

        internal RoleBriefing(RoleDefinition role, string disclaimer)
        {
            RoleId = role.roleId;
            TemporaryDisplayName = role.temporaryDisplayName;
            Text = role.briefing;
            ActionId = role.actionId;
            TargetAnchorId = role.targetAnchorId;
            Disclaimer = disclaimer;
        }
    }

    public sealed class TrainingSnapshot
    {
        public TrainingPhase Phase { get; }
        public string RoleId { get; }
        public string QuestState { get; }
        public string PreStateHash { get; }

        internal TrainingSnapshot(TrainingPhase phase, string roleId, string questState, string preStateHash)
        {
            Phase = phase;
            RoleId = roleId;
            QuestState = questState;
            PreStateHash = preStateHash;
        }
    }

    public sealed class ActionResult
    {
        public bool Accepted { get { return Code == ActionResultCode.Accepted; } }
        public ActionResultCode Code { get; }
        public string QuestState { get; }
        public IReadOnlyList<VirtualTeamEvent> VirtualTeamEvents { get; }
        public DescriptiveFeedback Feedback { get; }
        public string StateHash { get; }

        internal ActionResult(ActionResultCode code, string questState,
            IEnumerable<VirtualTeamEvent> events, DescriptiveFeedback feedback, string stateHash)
        {
            Code = code;
            QuestState = questState;
            VirtualTeamEvents = new ReadOnlyCollection<VirtualTeamEvent>(new List<VirtualTeamEvent>(events));
            Feedback = feedback;
            StateHash = stateHash;
        }
    }
}
