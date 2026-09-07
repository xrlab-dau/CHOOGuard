using System;
using System.Collections.Generic;
using System.Linq;

namespace ChooGuard.Foundation
{
    public static class ScenarioValidation
    {
        public const string RequiredDisclaimer = "KORAIL 검증 전 예시";

        public static void Validate(ScenarioProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            Require(profile.schemaVersion == "1.0", "schemaVersion must be 1.0.");
            RequireText(profile.scenarioId, "scenarioId");
            RequireText(profile.scenarioVersion, "scenarioVersion");
            RequireText(profile.mapId, "mapId");
            Require(profile.provisional, "Foundation scenarios must remain provisional.");
            Require(profile.disclaimer == RequiredDisclaimer, "The provisional disclaimer is required.");
            Require(profile.roles != null && profile.roles.Length == 5, "Exactly five provisional roles are required.");

            var roleIds = new HashSet<string>(StringComparer.Ordinal);
            var actionIds = new HashSet<string>(StringComparer.Ordinal);
            var anchorIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var role in profile.roles)
            {
                Require(role != null, "A role cannot be null.");
                RequireText(role.roleId, "roleId");
                RequireText(role.temporaryDisplayName, "temporaryDisplayName");
                RequireText(role.briefing, "briefing");
                RequireText(role.actionId, "actionId");
                RequireText(role.targetAnchorId, "targetAnchorId");
                RequireText(role.expectedFeedbackCode, "expectedFeedbackCode");
                RequireText(role.feedbackText, "feedbackText");
                Require(roleIds.Add(role.roleId), "roleId must be unique.");
                Require(actionIds.Add(role.actionId), "Each role needs a distinct actionId.");
                Require(anchorIds.Add(role.targetAnchorId), "Each role needs a distinct targetAnchorId.");
                Require(role.expectedQuestState == "completed", "The foundation quest outcome must be completed.");
            }
            Require(profile.representativeRoleId != null && roleIds.Contains(profile.representativeRoleId),
                "representativeRoleId must reference one of the five roles.");

            foreach (var role in profile.roles)
            {
                Require(role.expectedVirtualTeamEvents != null && role.expectedVirtualTeamEvents.Length == 4,
                    "Each role must define one event for each of the other four roles.");
                var eventRoles = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in role.expectedVirtualTeamEvents)
                {
                    Require(item != null, "A virtual team event cannot be null.");
                    Require(item.roleId != null && roleIds.Contains(item.roleId) && item.roleId != role.roleId,
                        "Virtual team events must reference a different known role.");
                    Require(eventRoles.Add(item.roleId), "Only one virtual team event per other role is allowed.");
                    RequireText(item.eventCode, "eventCode");
                    Require(item.state == "notified", "Other role display states must be notified, never quest completion.");
                }
            }
        }

        internal static ScenarioProfile CopyValidated(ScenarioProfile profile)
        {
            Validate(profile);
            return new ScenarioProfile
            {
                schemaVersion = profile.schemaVersion, scenarioId = profile.scenarioId,
                scenarioVersion = profile.scenarioVersion, mapId = profile.mapId,
                provisional = profile.provisional, disclaimer = profile.disclaimer,
                representativeRoleId = profile.representativeRoleId,
                roles = profile.roles.OrderBy(role => role.roleId, StringComparer.Ordinal).Select(role => new RoleDefinition
                {
                    roleId = role.roleId, temporaryDisplayName = role.temporaryDisplayName,
                    briefing = role.briefing, actionId = role.actionId, targetAnchorId = role.targetAnchorId,
                    expectedQuestState = role.expectedQuestState, expectedFeedbackCode = role.expectedFeedbackCode,
                    feedbackText = role.feedbackText, expectedVirtualTeamEvents = CopyEvents(role.expectedVirtualTeamEvents)
                }).ToArray()
            };
        }

        internal static VirtualTeamEventDefinition[] CopyEvents(IEnumerable<VirtualTeamEventDefinition> events)
        {
            return events.OrderBy(item => item.roleId, StringComparer.Ordinal).Select(item => new VirtualTeamEventDefinition
            {
                roleId = item.roleId, eventCode = item.eventCode, state = item.state
            }).ToArray();
        }

        private static void RequireText(string value, string field)
        {
            Require(!string.IsNullOrWhiteSpace(value), field + " must not be empty.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new ArgumentException(message, "profile");
        }
    }
}
