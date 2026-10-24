using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// A wall AED cabinet of the twin (kit fixture 'aed', placed by MajibangBuilder KitFixture / KitFixtureMarkers). It holds one
    /// automated external defibrillator until someone takes it; the staff member may put it back.
    /// </summary>
    public sealed class AedCabinet : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public StaffHands Hands;
        public AedUnit Unit { get; private set; }
        public bool Stored { get; private set; }
        /// <summary>Where the stored AED sits (cabinet local) and how much it is scaled down to fit behind the glass; 1 = as modelled.</summary>
        public Vector3 StoredLocalPosition { get; private set; } = new Vector3(0, -.1f, -.01f);
        public Quaternion StoredLocalRotation { get; private set; } = Quaternion.identity;
        public float StoredScale { get; private set; } = 1f;

        /// <summary>
        /// Makes the cabinet on a fixture marker (collider box facing +z) and puts its AED inside, behind the glass. With
        /// <see cref="EmergencyArt.AedCabinetModel"/> the Meshy cabinet model (31 x 42 x 20 cm, origin at its centre, door facing +z)
        /// is placed on the marker; without it the marker's own visual stays as it was.
        /// </summary>
        public static AedCabinet Build(GameObject marker, StaffHands hands, EmergencyArt art = null)
        {
            var cabinet = marker.AddComponent<AedCabinet>();
            cabinet.Hands = hands;
            if (art != null && art.AedCabinetModel != null)
            {
                var visual = Object.Instantiate(art.AedCabinetModel, marker.transform);
                visual.name = "AED 보관함 (모델)";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                foreach (var collider in visual.GetComponentsInChildren<Collider>()) Object.Destroy(collider);   // the marker's collider is the aim target
            }
            cabinet.Unit = AedUnit.Build(hands, cabinet, art);
            cabinet.FitStoredUnit();
            cabinet.Store();
            return cabinet;
        }

        /// <summary>
        /// With both Meshy models the AED case (31 × 24 × 27 cm) is deeper than the cabinet (20 cm) and would sit over the heart
        /// on the glass. The stored unit stands upright on its side (90° about z), scaled down uniformly to the lower part of the
        /// cabinet's inner volume (80 % of width, 40 % of height, 60 % of depth), so the heart on the door stays visible.
        /// Rotation and scale are restored the moment it is taken in hand.
        /// </summary>
        private void FitStoredUnit()
        {
            var cabinetVisual = transform.Find("AED 보관함 (모델)");
            var unitVisual = Unit != null ? Unit.transform.Find("본체 (모델)") : null;
            if (cabinetVisual == null || unitVisual == null) return;
            var cabinet = Props.LocalBounds(transform);          // cabinet model, marker space (the unit is not a child yet)
            var unitBounds = Props.LocalBounds(Unit.transform);   // unit model, unit space (origin at the bottom centre)
            if (cabinet.size.x <= 0 || unitBounds.size.x <= 0) return;
            var upright = Quaternion.Euler(0, 0, 90);
            var rotated = new Bounds(upright * unitBounds.center, Vector3.zero);
            for (int i = 0; i < 8; i++)
                rotated.Encapsulate(upright * (unitBounds.center + Vector3.Scale(unitBounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            var inner = Vector3.Scale(cabinet.size, new Vector3(.80f, .40f, .60f));
            float scale = Mathf.Min(1f, inner.x / rotated.size.x, inner.y / rotated.size.y, inner.z / rotated.size.z);
            var target = new Vector3(cabinet.center.x, cabinet.min.y + cabinet.size.y * .07f + rotated.extents.y * scale, cabinet.center.z - .01f);
            StoredScale = scale;
            StoredLocalRotation = upright;
            StoredLocalPosition = target - rotated.center * scale;
        }

        /// <summary>The AED goes back behind the glass (lower half of the cabinet).</summary>
        public void Store()
        {
            Stored = true;
            Unit.Held = false;
            Unit.transform.SetParent(transform, false);
            Unit.transform.localPosition = StoredLocalPosition;
            Unit.transform.localRotation = StoredLocalRotation;
            Unit.transform.localScale = Vector3.one * StoredScale;
            Unit.SetColliders(false);
        }

        /// <summary>The AED leaves the cabinet (taken in hand).</summary>
        public void Release() => Stored = false;

        public string DisplayName => "자동심장충격기(AED) 보관함";

        public string InteractionPrompt => Stored ? "AED 꺼내기" : Hands != null && Hands.Aed == Unit ? "AED 보관함에 넣기" : "";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (Hands == null || responder == null || responder.IsPaused) { reason = ""; return false; }
            if (Stored || Hands.Aed == Unit) return true;
            reason = "보관함이 비어 있습니다";
            return false;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            if (Stored)
            {
                Hands.TakeAed(Unit);
                feedback = "AED 를 꺼냈습니다 · 쓰러진 사람 곁에서 E 또는 G 로 내려놓기";
            }
            else
            {
                Hands.StoreAed(this);
                feedback = "AED 를 보관함에 넣었습니다";
            }
            return true;
        }
    }

    /// <summary>
    /// The station's automated external defibrillator (자동심장충격기). The staff member fetches it and sets it down beside a
    /// collapsed person; paramedics or a bystander operate it, not the player (JEV 011 carry_and_hand_over). What was done and
    /// when goes to the shift log only (JEV 011 logged_only). Research: the 2025 Korean CPR guideline has a named bystander
    /// fetch the nearest AED while another calls 119 (질병관리청; 헬스조선 2026-01-13).
    /// </summary>
    public sealed class AedUnit : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public StaffHands Hands;
        public AedCabinet Home { get; private set; }
        public bool Held { get; set; }

        /// <summary>
        /// The carry case. With <see cref="EmergencyArt.AedUnitModel"/> it is the Meshy model (31 x 24 x 27 cm, origin at the bottom
        /// centre) under a box collider on its bounds; otherwise a procedural case about 32 x 26 x 11 cm: yellow body, green lid band, grey handle.
        /// </summary>
        public static AedUnit Build(StaffHands hands, AedCabinet home, EmergencyArt art = null)
        {
            var root = new GameObject("자동심장충격기(AED)");
            var unit = root.AddComponent<AedUnit>();
            unit.Hands = hands;
            unit.Home = home;
            if (art != null && art.AedUnitModel != null)
            {
                var visual = Object.Instantiate(art.AedUnitModel, root.transform);
                visual.name = "본체 (모델)";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                foreach (var collider in visual.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
                var bounds = Props.LocalBounds(root.transform);
                var box = root.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
                return unit;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Material Paint(string name, Color colour)
            {
                var m = new Material(shader) { name = name };
                m.SetColor("_BaseColor", colour);
                m.SetFloat("_Smoothness", .45f);
                return m;
            }
            void Part(string name, Vector3 centre, Vector3 size, Material material, bool collide)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = name;
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = centre;
                part.transform.localScale = size;
                part.GetComponent<MeshRenderer>().sharedMaterial = material;
                if (!collide) Destroy(part.GetComponent<Collider>());
            }
            Part("본체", new Vector3(0, .13f, 0), new Vector3(.32f, .26f, .11f), Paint("AED 본체 (노랑)", new Color(.98f, .8f, .1f)), true);
            Part("덮개 띠", new Vector3(0, .2f, .056f), new Vector3(.3f, .07f, .004f), Paint("AED 표시 (초록)", new Color(0, .6f, .27f)), false);
            Part("손잡이", new Vector3(0, .28f, 0), new Vector3(.14f, .025f, .03f), Paint("AED 손잡이", new Color(.25f, .26f, .27f)), false);
            return unit;
        }

        public void SetColliders(bool on)
        {
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = on;
        }

        public string DisplayName => "자동심장충격기(AED)";
        public string InteractionPrompt => Held ? "" : "AED 들기";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            bool available = Hands != null && !Held && responder != null && !responder.IsPaused;
            reason = available ? null : "";
            return available;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out _)) return false;
            Hands.TakeAed(this);
            feedback = "AED 를 들었습니다";
            return true;
        }
    }
}
