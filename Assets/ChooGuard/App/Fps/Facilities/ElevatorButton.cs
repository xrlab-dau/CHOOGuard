using UnityEngine;

namespace ChooGuard.App.Fps.Facilities
{
    /// <summary>A push button of an elevator: the landing call panel or a car panel button (floor, open, close, emergency call).</summary>
    public sealed class ElevatorButton : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public enum Kinds { Hall, Floor, Open, Close, Alarm }

        public ElevatorCar Car;
        public Kinds Kind;
        /// <summary>Hall buttons: the landing they call to.</summary>
        public int Landing = -1;
        /// <summary>Floor buttons: the floor label printed on the button.</summary>
        public string Floor = "";

        private string prompt, name_;

        public static ElevatorButton Add(Transform at, ElevatorCar car, int landing, Kinds kind, string floor, Vector3 size)
        {
            var button = at.gameObject.AddComponent<ElevatorButton>();
            button.Car = car;
            button.Kind = kind;
            button.Landing = landing;
            button.Floor = floor;
            var box = at.gameObject.AddComponent<BoxCollider>();
            box.size = size;
            button.prompt = kind == Kinds.Hall ? "호출" : kind == Kinds.Floor ? floor + "층 누르기" : kind == Kinds.Open ? "열림 누르기" : kind == Kinds.Close ? "닫힘 누르기" : "비상통화 누르기";
            button.name_ = kind == Kinds.Hall ? "엘리베이터 호출 버튼" : kind == Kinds.Floor ? "엘리베이터 조작반 · " + floor + "층" : kind == Kinds.Open ? "엘리베이터 조작반 · 열림" : kind == Kinds.Close ? "엘리베이터 조작반 · 닫힘" : "엘리베이터 조작반 · 비상통화";
            return button;
        }

        public string DisplayName => name_;
        public string InteractionPrompt => prompt;

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (Car == null) { reason = ""; return false; }
            if (!Car.Running && Kind != Kinds.Alarm) { reason = "운행 정지 (지진 감지) · 비상통화만 됩니다"; return false; }
            return true;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            switch (Kind)
            {
                case Kinds.Hall:
                    Car.Call(Landing);
                    feedback = Car.Landings[Landing].Floor + "층 호출";
                    return true;
                case Kinds.Floor:
                    int to = Car.FloorIndex(Floor);
                    if (to < 0) return false;
                    Car.Press(to);
                    feedback = Floor + "층";
                    return true;
                case Kinds.Open: Car.OpenButton(); return true;
                case Kinds.Close: Car.CloseButton(); return true;
                default:
                    feedback = "비상통화 · 승강기 관리 담당에 연결됩니다";
                    return true;
            }
        }
    }
}
