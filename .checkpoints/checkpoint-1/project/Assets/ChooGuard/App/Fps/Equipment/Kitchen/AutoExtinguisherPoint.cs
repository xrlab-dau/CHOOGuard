using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The automatic diffusion extinguisher for kitchens (주방화재용 자동확산소화기, NFTC 101 표 2.1.1.3 제1호) hanging under a
    /// range or fryer hood. Its glass bulb bursts when the flame reaches it and it throws its wet chemical over what burns below
    /// for <see cref="DischargeSeconds"/>. Placement data: <c>shop</c>, <c>over</c> (the appliance it hangs above). The builder
    /// points <see cref="body"/> at the model and <see cref="bulbSlot"/> at its Bulb material.
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class AutoExtinguisherPoint : MonoBehaviour
    {
        public const string Kind = "kitchen_auto_extinguisher";
        public const float DischargeSeconds = 8f;

        [SerializeField] private Renderer body;
        [SerializeField] private int bulbSlot;
        [SerializeField] private Material burst;

        public StationEquipment Equipment { get; private set; }
        public string Shop { get; private set; } = "";
        public string Over { get; private set; } = "";
        public bool Discharged { get; private set; }
        public bool Discharging => Discharged && Time.time - dischargedAt < DischargeSeconds;
        /// <summary>The nozzle: the point the spray leaves from, pointing down.</summary>
        public Vector3 Nozzle => transform.position + Vector3.down * .42f;

        private ParticleSystem spray;
        private float dischargedAt;

        public void Bind()
        {
            Equipment = GetComponent<StationEquipment>();
            Shop = Equipment.Text("shop");
            Over = Equipment.Text("over");
        }

        /// <summary>The bulb bursts and the spray starts (<paramref name="agent"/>: the smoke/powder particle material).</summary>
        public void Discharge(Material agent)
        {
            if (Discharged) return;
            Discharged = true;
            dischargedAt = Time.time;
            Equipment.State = "방출";
            var materials = body.sharedMaterials;
            materials[bulbSlot] = burst;
            body.sharedMaterials = materials;
            var nozzle = new GameObject("방출구").transform;
            nozzle.SetParent(transform, false);
            nozzle.localPosition = Vector3.down * .42f;
            nozzle.localRotation = Quaternion.Euler(90, 0, 0);
            spray = Particles.Powder(nozzle, agent);
            var main = spray.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.12f, .3f);
            var shape = spray.shape;
            shape.angle = 28;
            var emission = spray.emission;
            emission.rateOverTime = 180;
            spray.Play();
        }

        private void Update()
        {
            if (spray == null || Discharging) return;
            var emission = spray.emission;
            emission.rateOverTime = 0;
            spray = null;
        }
    }
}
