namespace ChooGuard.App.Fps
{
    public interface IFpsInteraction
    {
        string InteractionPrompt { get; }
        bool CanInteract(FirstPersonResponder responder,out string reason);
        bool TryInteract(FirstPersonResponder responder,out string feedback);
    }
}
