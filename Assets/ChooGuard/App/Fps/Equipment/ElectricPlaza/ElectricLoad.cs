using System;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// A vending machine or a phone-charging kiosk as a load on the network: it shows the label of the breaker that feeds
    /// it (aiming at it names the asset tag and the circuit, as the sticker on real machines does), and the staff member can
    /// cut its own power by hand — pull the plug of a vending machine, throw the power switch of a kiosk — unless it burns
    /// too fiercely to go near. Unpowered, its screens and lit panels go dark.
    /// </summary>
    public sealed class ElectricLoad : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated, IFpsObservable
    {
        private static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");

        [Tooltip("A kiosk has a power switch; a vending machine is unplugged.")]
        public bool Kiosk;

        /// <summary>Why the machine cannot be approached now (it burns), or null. Set by the incident director.</summary>
        public Func<string> Blocked;

        /// <summary>Its own plug is out or its own switch is off.</summary>
        public bool LocalOff { get; private set; }
        /// <summary>A fire burns in it now: the state word says so and the machine keeps its screens until the feed is cut.</summary>
        public bool OnFire { get; set; }
        /// <summary>A fire burnt it out: it does not come back.</summary>
        public bool Burnt { get; private set; }
        /// <summary>The staff member switched the machine by hand (its plug or switch): the new state of <see cref="LocalOff"/>.</summary>
        public event Action<ElectricLoad, bool> HandSwitched;

        private StationEquipment equipment;
        private readonly System.Collections.Generic.List<(Renderer renderer, Material[] lit, Material[] dark)> screens = new System.Collections.Generic.List<(Renderer, Material[], Material[])>();
        private static readonly System.Collections.Generic.Dictionary<Material, Material> darkOf = new System.Collections.Generic.Dictionary<Material, Material>();
        private bool lit = true;
        private ElectricNetwork.Circuit namedFor;
        private string name_;

        private void Awake()
        {
            equipment = GetComponent<StationEquipment>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var on = renderer.sharedMaterials;
                var off = (Material[])on.Clone();
                bool any = false;
                for (int i = 0; i < on.Length; i++)
                {
                    // 화면·조명 판은 방출 색이 있는 재질이다.
                    if (on[i] == null || !on[i].HasProperty(EmissionColour) || on[i].GetColor(EmissionColour).maxColorComponent <= .01f) continue;
                    off[i] = Dark(on[i]);
                    any = true;
                }
                if (any) screens.Add((renderer, on, off));
            }
        }

        /// <summary>The same material with its glow out and its picture almost black (a screen or lit panel without power); one copy per source material for the whole station.</summary>
        private static Material Dark(Material source)
        {
            if (darkOf.TryGetValue(source, out var copy) && copy != null) return copy;
            copy = new Material(source) { name = source.name + " (꺼짐)" };
            copy.DisableKeyword("_EMISSION");
            copy.SetColor(EmissionColour, Color.black);
            if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", copy.GetColor("_BaseColor") * new Color(.06f, .06f, .06f, 1f));
            return darkOf[source] = copy;
        }

        public StationEquipment Equipment => equipment != null ? equipment : equipment = GetComponent<StationEquipment>();

        /// <summary>Burns the machine out for good (the fire it had is out).</summary>
        public void Burn()
        {
            if (Burnt) return;
            Burnt = true;
            Refresh();
        }

        /// <summary>Applies the network's verdict to the screens and the state word.</summary>
        public void Refresh()
        {
            if (equipment == null) equipment = GetComponent<StationEquipment>();
            bool powered = ElectricNetwork.Powered(equipment);
            if (powered != lit)
            {
                lit = powered;
                foreach (var screen in screens) screen.renderer.sharedMaterials = powered ? screen.lit : screen.dark;
            }
            if (equipment != null) equipment.State = OnFire ? "화재" : Burnt ? "소손" : powered ? "정상" : "전원 차단";
        }

        // 이름표(자산 번호·전원 회로)는 망이 짜인 뒤 바뀌지 않는다: 회로가 정해질 때 한 번만 만든다.
        public string DisplayName
        {
            get
            {
                var circuit = ElectricNetwork.CircuitOf(equipment);
                if (name_ == null || namedFor != circuit)
                {
                    namedFor = circuit;
                    name_ = equipment.Label + " " + ElectricNetwork.Tag(equipment) + (circuit != null ? " · 전원 " + circuit.Label : "");
                }
                return name_;
            }
        }

        public string StateText => Burnt ? "소손" : lit ? "켜짐" : "꺼짐";

        public string Observe(FirstPersonResponder responder)
        {
            string power = Burnt ? "겉이 그을려 있고 화면이 꺼져 있음" : lit ? "화면이 켜져 있음" : "화면이 꺼져 있음";
            if (!Burnt && LocalOff) power += Kiosk ? " · 전원 스위치가 꺼져 있음" : " · 전원 코드가 뽑혀 있음";
            return DisplayName + " · " + power;
        }

        public string InteractionPrompt
        {
            get
            {
                if (Burnt) return "";
                if (Kiosk) return LocalOff ? "전원 스위치 켜기" : "전원 스위치 끄기";
                return LocalOff ? "전원 코드 꽂기" : "전원 코드 뽑기 (뒤쪽)";
            }
        }

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (Burnt || responder == null || responder.IsPaused) { reason = ""; return false; }
            if (Blocked?.Invoke() is string why && why.Length > 0) { reason = why; return false; }
            return true;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            LocalOff = !LocalOff;
            Refresh();
            HandSwitched?.Invoke(this, LocalOff);
            feedback = Kiosk
                ? (LocalOff ? "충전 키오스크의 전원 스위치를 껐습니다" : "전원 스위치를 켰습니다")
                : (LocalOff ? equipment.Label + " 전원 코드를 뽑았습니다" : "전원 코드를 꽂았습니다");
            return true;
        }
    }
}
