using UnityEngine;

namespace ChooGuard.Foundation.Demo
{
    // Matches the incident cue using material color, including hosts using URP Unlit materials.
    public sealed class DemoIncidentVisual : MonoBehaviour
    {
        [SerializeField] private Light cue;
        [SerializeField] private Renderer indicator;
        private MaterialPropertyBlock block;

        public void Configure(Light incidentCue, Renderer visual)
        {
            cue = incidentCue;
            indicator = visual;
            if (indicator != null) indicator.enabled = false;
        }

        private void LateUpdate()
        {
            if (indicator == null) return;
            var active = cue != null && cue.enabled;
            indicator.enabled = active;
            if (!active) return;
            if (block == null) block = new MaterialPropertyBlock();
            var pulse = .5f + Mathf.Sin(Time.time * 4) * .5f;
            var color = Color.Lerp(new Color(.55f, .08f, .02f), new Color(1, .5f, .05f), pulse);
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            block.SetColor("_EmissionColor", color);
            indicator.SetPropertyBlock(block);
        }

        private void OnDisable()
        {
            if (indicator == null) return;
            indicator.SetPropertyBlock(null);
            indicator.enabled = false;
        }
    }
}
