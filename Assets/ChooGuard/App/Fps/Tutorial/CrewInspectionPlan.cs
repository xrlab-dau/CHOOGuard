using System;
using UnityEngine;

namespace ChooGuard.App.Fps.Tutorial
{
    public enum CrewInspectionAction { RadioStatus, IntercomTest, PaTest, Extinguisher, CabinInspect = 5, DoorInspect, HoldDeparture, OpenDoor, ClearLuggage, CloseDoor, ReportComplete, ClimateAdjust }
    [Serializable]
    public sealed class CrewInspectionStep
    {
        public string Chapter, Title;
        [TextArea] public string Instruction, Result;
        public string TargetId;
        public CrewInspectionAction Action;
        public bool HandheldRadio;
        public AudioClip GuidanceVoice, ResponseVoice;
        [TextArea] public string Observation;
    }
    [CreateAssetMenu(menuName = "ChooGuard/Crew inspection plan")]
    public sealed class CrewInspectionPlan : ScriptableObject
    {
        public CrewInspectionStep[] Steps = Array.Empty<CrewInspectionStep>();
        public bool Validate(out string reason)
        {
            if (Steps == null || Steps.Length == 0) { reason = "점검 단계가 없습니다."; return false; }
            foreach (var step in Steps)
                if (step == null || string.IsNullOrWhiteSpace(step.Title) || string.IsNullOrWhiteSpace(step.Instruction) ||
                    string.IsNullOrWhiteSpace(step.Result) || (!step.HandheldRadio && string.IsNullOrWhiteSpace(step.TargetId)) ||
                    !Enum.IsDefined(typeof(CrewInspectionAction), step.Action) ||
                    (step.HandheldRadio != (step.Action == CrewInspectionAction.HoldDeparture || step.Action == CrewInspectionAction.ReportComplete)))
                { reason = "점검 단계 자료를 확인해 주세요."; return false; }
            reason = null; return true;
        }
    }
}
