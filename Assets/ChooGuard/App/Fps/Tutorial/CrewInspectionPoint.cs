using UnityEngine;

namespace ChooGuard.App.Fps.Tutorial
{
    public sealed class CrewInspectionPoint : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public string Id, Label;
        public Transform Marker;
        public Vector2 FocusSize;
        public Transform[] InspectionTargets = System.Array.Empty<Transform>();
        public CrewInspectionSession Session;
        public string DisplayName => Label;
        public string InteractionPrompt => Session != null && Session.Current != null ? Session.Current.Title : "점검";
        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            bool allowed = Session != null && Session.Accepts(this, responder);
            reason = allowed ? null : "표시된 점검 대상과 화면 안내를 따라가세요.";
            return allowed;
        }
        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            bool done = CanInteract(responder, out feedback) && Session.Perform(this, responder);
            if (done) feedback = Session.ActionFeedback;
            return done;
        }
    }
}
