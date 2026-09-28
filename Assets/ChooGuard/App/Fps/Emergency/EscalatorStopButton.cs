using ChooGuard.App.Fps.Hud;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// The red emergency stop button at an escalator landing. Anyone may press it when someone falls (research E1, E3); the
    /// station staff member restarts the belt with the key switch once the steps are clear (research E2, JEV 010). The
    /// button stands on the right-hand newel 0.9 m up (a generic place: the twin has no survey of the button positions).
    /// </summary>
    public sealed class EscalatorStopButton : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsSecondaryInteraction
    {
        private Escalator escalator;
        private string name_;
        private static Material red, yellow;

        /// <summary>Button on the right of someone travelling <paramref name="travel"/>, facing <paramref name="facing"/>.</summary>
        public static void Build(Escalator escalator, Vector3 landing, Vector3 travel, Vector3 facing)
        {
            var right = Vector3.Cross(Vector3.up, travel).normalized;
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "비상정지 버튼 · " + escalator.Label;
            plate.transform.SetParent(escalator.transform, false);
            plate.transform.SetPositionAndRotation(landing + right * .62f + Vector3.up * .9f, Quaternion.LookRotation(facing, Vector3.up));
            plate.transform.localScale = new Vector3(.12f, .12f, .02f);
            plate.GetComponent<MeshRenderer>().sharedMaterial = Lamp(ref yellow, "비상정지 표지 (노랑)", new Color(.98f, .8f, .1f));
            var knob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            knob.name = "버튼";
            Destroy(knob.GetComponent<Collider>());
            knob.transform.SetParent(plate.transform, false);
            knob.transform.localPosition = new Vector3(0, 0, .9f);
            knob.transform.localRotation = Quaternion.Euler(90, 0, 0);
            knob.transform.localScale = new Vector3(.45f, 1.2f, .45f);
            knob.GetComponent<MeshRenderer>().sharedMaterial = Lamp(ref red, "비상정지 버튼 (빨강)", new Color(.85f, .06f, .05f));
            var button = plate.AddComponent<EscalatorStopButton>();
            button.escalator = escalator;
            button.name_ = escalator.Label + " 비상정지 버튼";
        }

        private static Material Lamp(ref Material cache, string name, Color colour)
        {
            if (cache != null) return cache;
            cache = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            cache.SetColor("_BaseColor", colour);
            return cache;
        }

        public string DisplayName => name_;
        public string InteractionPrompt => "비상정지 버튼 누르기";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (escalator == null) { reason = ""; return false; }
            if (!escalator.Running) { reason = "정지됨 · " + escalator.StoppedBy; return false; }
            return true;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            escalator.Stop("비상정지 버튼(역무원)");
            EmergencySession.Current?.Log?.Add(escalator.Label + " 비상정지 버튼을 역무원이 누름");
            feedback = escalator.Label + " 비상정지 · 탄 사람은 걸어서 내립니다";
            return true;
        }

        public string SecondaryPrompt => escalator != null && !escalator.Running && !escalator.Closed ? "열쇠로 재가동" : "";

        public bool TrySecondary(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (SecondaryPrompt.Length == 0) return false;
            if (!escalator.Restart(out feedback)) return false;
            EmergencySession.Current?.Log?.Add(escalator.Label + " 역무원이 열쇠 스위치로 재가동");
            var session = EmergencySession.Current;
            if (session != null) session.Hud.Radio.Push(RadioChannel.Self, "역무실, " + escalator.Label + " 이상 없음 확인하고 재가동했습니다.");
            feedback = escalator.Label + " 재가동";
            return true;
        }
    }
}
