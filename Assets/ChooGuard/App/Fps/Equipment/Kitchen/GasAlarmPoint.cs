using System;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// A stand-alone gas leak alarm (단독형 가스누설경보기, NFTC 206) high on a shop's wall: it sounds when gas has reached it, its
    /// red LED lights and it beeps until the leak is gone and the staff member (or a technician) resets it. Placement data:
    /// <c>shop</c>. The builder points <see cref="body"/> and the LED materials at the model's Led_Red slot.
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class GasAlarmPoint : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public const string Kind = "gas_alarm";

        [SerializeField] private Renderer body;
        [SerializeField] private int redSlot;
        [SerializeField] private Material redOn, redOff;

        public StationEquipment Equipment { get; private set; }
        public string Shop { get; private set; } = "";
        public bool Sounding { get; private set; }
        /// <summary>Set by the incident director: is there still gas where the alarm can smell it?</summary>
        public Func<GasAlarmPoint, bool> GasPresent;

        private AudioSource speaker;

        public void Bind()
        {
            Equipment = GetComponent<StationEquipment>();
            Shop = Equipment.Text("shop");
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.clip = KitchenSound.Beep;
            speaker.loop = true;
            speaker.playOnAwake = false;
            speaker.spatialBlend = 1f;
            speaker.minDistance = 2f;
            speaker.maxDistance = 30f;
            speaker.rolloffMode = AudioRolloffMode.Linear;
        }

        /// <summary>Gas reached the sensor: the LED lights and the beeping starts.</summary>
        public void Sound()
        {
            if (Sounding) return;
            Sounding = true;
            Equipment.State = "동작";
            SetLed(redOn);
            speaker.Play();
        }

        /// <summary>The alarm is reset: beeping stops, the LED goes dark.</summary>
        public void Silence()
        {
            if (!Sounding) return;
            Sounding = false;
            Equipment.State = "정상";
            SetLed(redOff);
            speaker.Stop();
        }

        private void SetLed(Material material)
        {
            var materials = body.sharedMaterials;
            materials[redSlot] = material;
            body.sharedMaterials = materials;
        }

        public string DisplayName => Equipment.Label + (Sounding ? " · 울림" : "");
        public string InteractionPrompt => Sounding ? "가스누설경보기 확인·복귀" : "";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            bool available = Sounding && responder != null && !responder.IsPaused;
            reason = available ? null : "";
            return available;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out _)) return false;
            if (GasPresent != null && GasPresent(this))
            {
                feedback = "가스가 남아 있어 경보가 복귀되지 않습니다 · 밸브를 잠그고 환기하세요";
                return true;
            }
            Silence();
            EmergencySession.Current?.Log?.Add(Equipment.Label + " 확인 · 복귀");
            feedback = "가스누설경보기를 확인하고 복귀했습니다";
            return true;
        }
    }
}
