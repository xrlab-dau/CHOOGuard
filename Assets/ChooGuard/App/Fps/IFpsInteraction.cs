namespace ChooGuard.App.Fps
{
    public interface IFpsInteraction
    {
        string InteractionPrompt { get; }
        bool CanInteract(FirstPersonResponder responder,out string reason);
        bool TryInteract(FirstPersonResponder responder,out string feedback);
    }
    // 조준한 대상의 이름. HUD 중앙이 '무엇을 보고 있는가'를 동작 문구와 따로 보여 준다.
    public interface IFpsNamed
    {
        string DisplayName { get; }
    }
    // 역무원 열쇠 등 두 번째 동작(R). 지금 할 수 없으면 빈 문구를 돌려준다.
    public interface IFpsSecondaryInteraction
    {
        string SecondaryPrompt { get; }
        bool TrySecondary(FirstPersonResponder responder,out string feedback);
    }
}
