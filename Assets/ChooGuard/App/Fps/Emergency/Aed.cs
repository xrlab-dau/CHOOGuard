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

        /// <summary>Makes the cabinet on a fixture marker (collider box facing +z) and puts its AED inside, behind the glass.</summary>
        public static AedCabinet Build(GameObject marker, StaffHands hands)
        {
            var cabinet = marker.AddComponent<AedCabinet>();
            cabinet.Hands = hands;
            cabinet.Unit = AedUnit.Build(hands, cabinet);
            cabinet.Store();
            return cabinet;
        }

        /// <summary>The AED goes back behind the glass (lower half of the cabinet).</summary>
        public void Store()
        {
            Stored = true;
            Unit.Held = false;
            Unit.transform.SetParent(transform, false);
            Unit.transform.localPosition = new Vector3(0, -.1f, -.01f);
            Unit.transform.localRotation = Quaternion.identity;
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

        /// <summary>A carry case about 32 x 26 x 11 cm: yellow body, green lid band, grey handle. Procedural, no asset.</summary>
        public static AedUnit Build(StaffHands hands, AedCabinet home)
        {
            var root = new GameObject("자동심장충격기(AED)");
            var unit = root.AddComponent<AedUnit>();
            unit.Hands = hands;
            unit.Home = home;
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
