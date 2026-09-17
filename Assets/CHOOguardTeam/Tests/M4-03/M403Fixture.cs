using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ChooGuard.Foundation;
using UnityEngine;

namespace ChooGuard.Team.M403.Tests
{
    // Read-only view of the M4-01 candidate fixture (artifact:32:M4-01-fixture:candidate).
    // Field names follow foundation/team-fixtures/M4-01/role-action-fixture.json; unknown fields are ignored.
    [Serializable] public sealed class M403Fixture
    {
        public string fixtureId;
        public string disclaimer;
        public M403Snapshot canonicalScenarioSnapshot;
        public M403Row[] roleRows;
    }

    [Serializable] public sealed class M403Snapshot
    {
        public string sourcePath;
        public string sourceSha256;
    }

    [Serializable] public sealed class M403Row
    {
        public string rowId;
        public string roleId;
        public bool representative;
        public string temporaryDisplayName;
        public string actionId;
        public string targetAnchorId;
        public string scenarioVersion;
        public M403PreState preState;
        public string expectedQuestState;
        public VirtualTeamEventDefinition[] expectedVirtualTeamEvents;
        public string expectedFeedbackCode;
        public string feedbackText;
        public M403Marks provisionalMarks;
    }

    [Serializable] public sealed class M403PreState
    {
        public string phase;
        public string questState;
        public bool briefingAcknowledged;
        public M403TeamState[] virtualTeamStates;
    }

    [Serializable] public sealed class M403TeamState
    {
        public string roleId;
        public string state;
    }

    [Serializable] public sealed class M403Marks
    {
        public M403Mark role;
        public M403Mark action;
        public M403Mark feedback;
    }

    [Serializable] public sealed class M403Mark
    {
        public bool synthetic;
        public bool provisional;
        public bool official;
        public bool officialKorailProcedure;
        public bool officialScoring;
    }

    public sealed class M403Plan
    {
        public bool Ready { get { return Reason == null; } }
        public string Reason { get; private set; }
        public M403Row Row { get; private set; }
        public InputModality Modality { get; private set; }

        public static M403Plan Blocked(string reason) { return new M403Plan { Reason = reason }; }
        public static M403Plan Of(M403Row row, InputModality modality) { return new M403Plan { Row = row, Modality = modality }; }
    }

    public static class M403Inputs
    {
        public const string FixturePath = "foundation/team-fixtures/M4-01/role-action-fixture.json";
        public const string ScenarioPath = "foundation/scenarios/foundation-demo.json";
        public static readonly string[] NonRepresentativeRoles = { "role-02", "role-03", "role-04", "role-05" };

        public static string RepoRoot { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); } }

        public static string Sha256(string relativePath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(Path.Combine(RepoRoot, relativePath)))
                return string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
        }

        public static M403Fixture LoadFixture()
        {
            var path = Path.Combine(RepoRoot, FixturePath);
            if (!File.Exists(path)) return null;
            return ParseFixture(File.ReadAllText(path));
        }

        // Empty or unparsable text yields no fixture (Resolve then reports fixture_missing) instead of an exception.
        // Keys absent from a parsable document are left at their defaults and are caught by Resolve's field checks.
        public static M403Fixture ParseFixture(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonUtility.FromJson<M403Fixture>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public static ScenarioProfile LoadScenario()
        {
            return JsonUtility.FromJson<ScenarioProfile>(File.ReadAllText(Path.Combine(RepoRoot, ScenarioPath)));
        }

        // A role-action case runs only when the fixture row, its target, its expected outputs and the input modality
        // are all present. Anything missing is reported as blocked and is never executed or downgraded to a pass.
        public static M403Plan Resolve(M403Fixture fixture, string roleId, InputModality? modality)
        {
            if (fixture == null || fixture.roleRows == null) return M403Plan.Blocked("fixture_missing");
            var row = fixture.roleRows.FirstOrDefault(item => item != null && item.roleId == roleId);
            if (row == null) return M403Plan.Blocked("fixture_row_missing");
            if (row.representative) return M403Plan.Blocked("representative_role_out_of_scope");
            if (!modality.HasValue) return M403Plan.Blocked("modality_missing");
            if (!Enum.IsDefined(typeof(InputModality), modality.Value)) return M403Plan.Blocked("modality_unknown");
            if (string.IsNullOrEmpty(row.targetAnchorId)) return M403Plan.Blocked("target_missing");
            if (string.IsNullOrEmpty(row.actionId)) return M403Plan.Blocked("action_missing");
            if (row.preState == null || string.IsNullOrEmpty(row.preState.phase) || string.IsNullOrEmpty(row.preState.questState)
                || row.preState.virtualTeamStates == null || row.preState.virtualTeamStates.Length == 0)
                return M403Plan.Blocked("prestate_missing");
            if (string.IsNullOrEmpty(row.expectedQuestState) || string.IsNullOrEmpty(row.expectedFeedbackCode)
                || row.expectedVirtualTeamEvents == null || row.expectedVirtualTeamEvents.Length == 0)
                return M403Plan.Blocked("expected_output_missing");
            return M403Plan.Of(row, modality.Value);
        }

        public static string Events(IEnumerable<VirtualTeamEventDefinition> events)
        {
            return string.Join(",", events.Select(item => item.roleId + ":" + item.eventCode + ":" + item.state));
        }

        public static string Events(IEnumerable<VirtualTeamEvent> events)
        {
            return string.Join(",", events.Select(item => item.RoleId + ":" + item.EventCode + ":" + item.State));
        }

        public static string Team(IEnumerable<TeamRoleState> states)
        {
            return string.Join(",", states.Select(item => item.RoleId + ":" + item.State));
        }

        public static string Team(IEnumerable<M403TeamState> states)
        {
            return string.Join(",", states.Select(item => item.roleId + ":" + item.state));
        }
    }
}
