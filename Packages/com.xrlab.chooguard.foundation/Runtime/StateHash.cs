using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ChooGuard.Foundation
{
    internal static class StateHash
    {
        internal static string Profile(ScenarioProfile profile)
        {
            var fields = new List<string>
            {
                "chooguard.foundation.profile.v1", profile.schemaVersion, profile.scenarioId,
                profile.scenarioVersion, profile.mapId, profile.provisional ? "true" : "false",
                profile.disclaimer, profile.representativeRoleId
            };
            // CopyValidated has already sorted roles and team events using ordinal IDs.
            foreach (var role in profile.roles)
            {
                fields.Add(role.roleId);
                fields.Add(role.temporaryDisplayName);
                fields.Add(role.briefing);
                fields.Add(role.actionId);
                fields.Add(role.targetAnchorId);
                fields.Add(role.expectedQuestState);
                fields.Add(role.expectedFeedbackCode);
                fields.Add(role.feedbackText);
                foreach (var item in role.expectedVirtualTeamEvents)
                {
                    fields.Add(item.roleId);
                    fields.Add(item.eventCode);
                    fields.Add(item.state);
                }
            }
            return Compute(fields);
        }

        internal static string Session(string profileHash, TrainingPhase phase, string roleId,
            string questState, IReadOnlyList<TeamRoleState> teamStates)
        {
            var fields = new List<string>
            {
                "chooguard.foundation.state.v1", profileHash, phase.ToString(), roleId, questState
            };
            foreach (var item in teamStates)
            {
                fields.Add(item.RoleId);
                fields.Add(item.State);
            }
            return Compute(fields);
        }

        private static string Compute(IEnumerable<string> fields)
        {
            using (var bytes = new MemoryStream())
            {
                foreach (var field in fields)
                {
                    var encoded = Encoding.UTF8.GetBytes(field);
                    var length = Encoding.ASCII.GetBytes(encoded.Length.ToString(CultureInfo.InvariantCulture) + ":");
                    bytes.Write(length, 0, length.Length);
                    bytes.Write(encoded, 0, encoded.Length);
                }
                using (var sha = SHA256.Create())
                {
                    var digest = sha.ComputeHash(bytes.ToArray());
                    var result = new StringBuilder(digest.Length * 2);
                    foreach (var value in digest) result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                    return result.ToString();
                }
            }
        }
    }
}
