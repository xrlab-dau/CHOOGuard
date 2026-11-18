using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>The existing visible shock button owns a padded collider, rather than accepting any point on the case.</summary>
    public sealed class AedShockControl : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsSecondaryInteraction
    {
        public AedUnit Unit;
        SphereCollider target;
        public string DisplayName => "AED 충격 버튼";
        public string InteractionPrompt => Unit.Procedure.Prompt;
        public string SecondaryPrompt => Unit.SecondaryPrompt;
        void Awake() { target = gameObject.AddComponent<SphereCollider>(); target.radius = .065f; target.enabled = false; }
        void Update() => target.enabled = Unit != null && !Unit.Held && Unit.Patient != null && Unit.Procedure.Current == AedProcedure.Stage.ShockAdvised;
        public bool CanInteract(FirstPersonResponder responder, out string reason) => Unit.CanInteract(responder, out reason);
        public bool TryInteract(FirstPersonResponder responder, out string feedback) => Unit.TryInteract(responder, out feedback);
        public bool TrySecondary(FirstPersonResponder responder, out string feedback) => Unit.TrySecondary(responder, out feedback);
    }
}
