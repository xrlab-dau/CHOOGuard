using UnityEngine;

namespace ChooGuard.Foundation.Demo
{
    // Lightweight first-person view model. It has no collider and never performs a quest itself.
    public sealed class DemoHands : MonoBehaviour
    {
        [SerializeField] private DemoPlayerController player;
        [SerializeField] private Transform model;
        private Vector3 rest;
        private float pulseUntil;
        public void Configure(DemoPlayerController actor, Transform visual)
        {
            player = actor;
            model = visual;
            rest = visual.localPosition;
            model.gameObject.SetActive(false);
        }
        private void Awake() { if (model != null) rest = model.localPosition; }
        public void Pulse() { pulseUntil = Time.time + .28f; }
        private void LateUpdate()
        {
            if (model == null || player == null) return;
            model.gameObject.SetActive(player.ControlEnabled);
            if (!player.ControlEnabled) return;
            var pulse = Mathf.Clamp01((pulseUntil - Time.time) / .28f);
            var reach = Mathf.Sin(pulse * Mathf.PI);
            model.localPosition = rest + new Vector3(0, Mathf.Sin(Time.time * 2) * .002f + reach * .025f, reach * .05f);
        }
    }
}
