using System;
using System.Linq;

namespace ChooGuard.Foundation.Demo
{
    public enum DemoPhase { RoleSelection, Briefing, Incident, ReachAssembly, Results }

    // Presentation flow around the unchanged action contract. Assembly is a game objective,
    // never a new railway procedure, score or a virtual teammate's completed quest.
    public sealed class DemoFlow
    {
        private readonly ScenarioProfile profile;
        public TrainingSession Session { get; private set; }
        public DemoPhase Phase { get; private set; }
        public RoleBriefing SelectedRole { get; private set; }
        public ActionResult LastAction { get; private set; }

        public DemoFlow(ScenarioProfile profile)
        {
            ScenarioValidation.Validate(profile);
            // Own the source so a caller cannot mutate the next restart through the input DTO.
            this.profile = new ScenarioProfile
            {
                schemaVersion = profile.schemaVersion, scenarioId = profile.scenarioId,
                scenarioVersion = profile.scenarioVersion, mapId = profile.mapId,
                provisional = profile.provisional, disclaimer = profile.disclaimer,
                representativeRoleId = profile.representativeRoleId,
                roles = profile.roles.Select(role => new RoleDefinition
                {
                    roleId = role.roleId, temporaryDisplayName = role.temporaryDisplayName,
                    briefing = role.briefing, actionId = role.actionId, targetAnchorId = role.targetAnchorId,
                    expectedQuestState = role.expectedQuestState, expectedFeedbackCode = role.expectedFeedbackCode,
                    feedbackText = role.feedbackText,
                    expectedVirtualTeamEvents = role.expectedVirtualTeamEvents.Select(item => new VirtualTeamEventDefinition
                    { roleId = item.roleId, eventCode = item.eventCode, state = item.state }).ToArray()
                }).ToArray()
            };
            Restart();
        }

        public void Restart()
        {
            Session = new TrainingSession(profile);
            Phase = DemoPhase.RoleSelection;
            SelectedRole = null;
            LastAction = null;
        }

        public void SelectRole(string roleId)
        {
            SelectedRole = Session.SelectRole(roleId);
            Phase = DemoPhase.Briefing;
        }

        public void BeginIncident()
        {
            if (Phase != DemoPhase.Briefing)
                throw new InvalidOperationException("A displayed role briefing is required before the incident.");
            Session.AcknowledgeBriefing();
            Phase = DemoPhase.Incident;
        }

        // A local presentation API; the engine adapter must enforce reach and visibility first.
        public bool TryInteract(string anchorId)
        {
            if (Phase != DemoPhase.Incident) return false;
            var fixture = Session.CreateRoleActionFixture();
            var action = new TrainingAction(Guid.NewGuid().ToString("N"), fixture.scenarioVersion,
                fixture.roleId, fixture.preStateHash, fixture.actionId, anchorId, InputModality.Desktop);
            var result = Session.Submit(in action);
            if (!result.Accepted) return false;
            LastAction = result;
            Phase = DemoPhase.ReachAssembly;
            return true;
        }

        // The controller calls this only after a spatial check against the assembly point.
        public bool TryReachAssembly()
        {
            if (Phase != DemoPhase.ReachAssembly) return false;
            Phase = DemoPhase.Results;
            return true;
        }
    }
}
