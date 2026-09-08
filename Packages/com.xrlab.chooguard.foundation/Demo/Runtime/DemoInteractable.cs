using UnityEngine;

namespace ChooGuard.Foundation.Demo
{
    public sealed class DemoInteractable : MonoBehaviour
    {
        [SerializeField] private string anchorId;
        [SerializeField] private string displayName;
        [SerializeField, Range(0, 4)] private int effectKind;
        [SerializeField] private Renderer[] feedbackSurfaces;
        private Renderer[] surfaces;
        private Transform movingPart;
        private Quaternion initialRotation;
        private bool initialized;
        private MaterialPropertyBlock block;

        public string AnchorId { get { return anchorId; } }
        public string DisplayName { get { return displayName; } }
        public bool Completed { get; private set; }
        public Vector3 InteractionPoint { get { return transform.position; } }

        public void Configure(string anchor, string label, int effect)
        {
            if (initialized) ResetVisual();
            Completed = false;
            anchorId = anchor;
            displayName = label;
            effectKind = Mathf.Clamp(effect, 0, 4);
            initialized = false;
        }

        public void ConfigureFeedback(Renderer[] indicators)
        {
            feedbackSurfaces = indicators;
            initialized = false;
        }

        private void InitializeVisual()
        {
            if (initialized) return;
            surfaces = feedbackSurfaces != null && feedbackSurfaces.Length > 0
                ? feedbackSurfaces : GetComponentsInChildren<Renderer>();
            movingPart = transform.Find("MovingPart");
            if (movingPart != null) initialRotation = movingPart.localRotation;
            block = new MaterialPropertyBlock();
            initialized = true;
        }

        public void ApplyInteraction()
        {
            if (Completed) return;
            InitializeVisual();
            Completed = true;
            if (effectKind == 4 && movingPart != null)
                movingPart.localRotation = initialRotation * Quaternion.Euler(0, 75, 0);
            Tint(new Color(.2f, 1f, .65f));
        }

        public void ResetVisual()
        {
            InitializeVisual();
            Completed = false;
            if (movingPart != null) movingPart.localRotation = initialRotation;
            foreach (var surface in surfaces)
                if (surface != null) surface.SetPropertyBlock(null);
        }

        private void Update()
        {
            if (!Completed || (effectKind != 1 && effectKind != 2)) return;
            var pulse = .75f + Mathf.Sin(Time.time * 5) * .25f;
            Tint(Color.Lerp(new Color(.1f, .45f, .3f), new Color(.3f, 1, .7f), pulse));
        }

        public void ShowState(Color color){InitializeVisual();Tint(color);}

        private void Tint(Color color)
        {
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            block.SetColor("_EmissionColor", color * .3f);
            foreach (var surface in surfaces)
                if (surface != null) surface.SetPropertyBlock(block);
        }
    }
}
