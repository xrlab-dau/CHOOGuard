using System;

namespace ChooGuard.Foundation
{
    // Public fields deliberately match the repository JSON and Unity JsonUtility convention.
    [Serializable]
    public sealed class ScenarioProfile
    {
        public string schemaVersion;
        public string scenarioId;
        public string scenarioVersion;
        public string mapId;
        public bool provisional;
        public string disclaimer;
        public string representativeRoleId;
        public RoleDefinition[] roles;
    }

    [Serializable]
    public sealed class RoleDefinition
    {
        public string roleId;
        public string temporaryDisplayName;
        public string briefing;
        public string actionId;
        public string targetAnchorId;
        public string expectedQuestState;
        public VirtualTeamEventDefinition[] expectedVirtualTeamEvents;
        public string expectedFeedbackCode;
        public string feedbackText;
    }

    [Serializable]
    public sealed class VirtualTeamEventDefinition
    {
        public string roleId;
        public string eventCode;
        public string state;
    }

    [Serializable]
    public sealed class RoleActionFixture
    {
        public string roleId;
        public bool representative;
        public string scenarioVersion;
        public string actionId;
        public string targetAnchorId;
        public string preStateHash;
        public string expectedQuestState;
        public VirtualTeamEventDefinition[] expectedVirtualTeamEvents;
        public string expectedFeedbackCode;
    }
}
