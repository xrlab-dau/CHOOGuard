using UnityEngine;

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
    /// <summary>
    /// How the looked-at thing stands now, as anyone standing there sees it ("열림", "반쯤 잠김", "꺼짐"), kept apart from <see cref="IFpsNamed.DisplayName"/>
    /// so the HUD can show it in 표준 and leave it to looking closely in 실전. Empty when there is nothing to say.
    /// </summary>
    public interface IFpsStated
    {
        string StateText { get; }
    }
    // 역무원 열쇠 등 두 번째 동작(R). 지금 할 수 없으면 빈 문구를 돌려준다.
    public interface IFpsSecondaryInteraction
    {
        string SecondaryPrompt { get; }
        bool TrySecondary(FirstPersonResponder responder,out string feedback);
    }

    /// <summary>How a handle moves under the hand while E is held (the HUD shows the matching glyph).</summary>
    public enum HoldStyle
    {
        /// <summary>A quarter-turn handle (a ball valve, a burner cock): drag it round.</summary>
        Turn,
        /// <summary>A handwheel of several turns (a hydrant valve): circle the mouse; the wheel turns it a notch.</summary>
        Crank,
        /// <summary>A lever between two ends (a breaker): drag it up or down; it snaps over past the middle.</summary>
        Lever,
        /// <summary>A hinged leaf (a door, a cabinet door): push or pull it.</summary>
        Swing,
        /// <summary>A covered push button (a call point, an emergency stop): keep pressing until it goes in.</summary>
        Press,
    }

    /// <summary>
    /// A handle worked by hand: holding E grabs it, the mouse (and the wheel) move it, letting go leaves it where it is.
    /// While <see cref="FirstPersonResponder.SimpleControls"/> is on, or <see cref="Holdable"/> is false, the target's
    /// plain E press (<see cref="IFpsInteraction.TryInteract"/>) applies instead. Nothing here decides whether it was the
    /// right thing to do: the world shows what the handle's position does.
    /// </summary>
    public interface IFpsHoldInteraction
    {
        HoldStyle HoldStyle { get; }
        /// <summary>The handle can be grabbed now; otherwise <see cref="IFpsInteraction.CanInteract"/> gives the reason.</summary>
        bool Holdable(FirstPersonResponder responder);
        void BeginHold(FirstPersonResponder responder);
        /// <summary>
        /// Every simulation step while E stays held. <paramref name="mouse"/> is the hand's movement in degrees of the look
        /// it replaces (x right, y up), <paramref name="wheel"/> the scroll steps (+ away from the player).
        /// </summary>
        void Hold(FirstPersonResponder responder, Vector2 mouse, float wheel, float deltaSeconds);
        /// <summary>The hand lets go (E released, the player paused or walked away). Returns the feedback line, or null.</summary>
        string EndHold(FirstPersonResponder responder);
        /// <summary>How far through its travel the handle is now (0..1) for the HUD ring; negative for no ring.</summary>
        float HoldProgress { get; }
    }

    /// <summary>What looking closely (right mouse held) shows: only what a person standing there could see or read.</summary>
    public interface IFpsObservable
    {
        string Observe(FirstPersonResponder responder);
    }
}
