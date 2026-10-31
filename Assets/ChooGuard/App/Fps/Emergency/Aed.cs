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
            if (art != null && art.AedCabinetModel != null && marker.transform.Find("AED 보관함 (모델)") == null)   // a saved fixture may already carry the model
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
        /// on the glass. The stored unit lies with its top (handle side) towards the glass (90° about x), scaled down uniformly
        /// to the lower part of the cabinet's inner volume (80 % of width, 40 % of height, 60 % of depth), so the heart on the
        /// door stays visible. Rotation and scale are restored the moment it is taken in hand.
        /// </summary>
        private void FitStoredUnit()
        {
            var cabinetVisual = transform.Find("AED 보관함 (모델)");
            var unitVisual = Unit != null ? Unit.transform.Find("본체 (모델)") : null;
            if (cabinetVisual == null || unitVisual == null) return;
            var cabinet = Props.LocalBounds(transform);          // cabinet model, marker space (the unit is not a child yet)
            var unitBounds = Props.LocalBounds(Unit.transform);   // unit model, unit space (origin at the bottom centre)
            if (cabinet.size.x <= 0 || unitBounds.size.x <= 0) return;
            var upright = Quaternion.Euler(90, 0, 0);   // local +y (top of the case) -> +z (towards the glass)
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
    /// The station's automated external defibrillator (자동심장충격기). The staff member fetches it, sets it down beside a collapsed
    /// person and, as a trained lay rescuer (2025 한국 심폐소생술 가이드라인 일반인 알고리즘; SIM_CONTROLS_BENCHMARK §6.8), works it
    /// the way the device itself says to — in the order fixed on 2026-10-31: open the lid (E on the case), take a pad out of the
    /// lid (E on the pad) and stick it on the chest (E on the patient), the same again, hands off calling "물러나세요" (E on the
    /// patient), the shock button (E on the case) when the device says "제세동이 필요합니다", and chest compressions (R on the
    /// patient) after the shock or when it says "전기충격이 필요없습니다". The procedure is <see cref="AedProcedure"/>; this
    /// component is the case on the floor, its lid, its lamp, the two pads (<see cref="AedPad"/>) and the device's voice on the
    /// HUD. Every step goes to the shift log with the time since the collapse; the patient's course stays a JEV judgment.
    /// </summary>
    public sealed class AedUnit : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated, IFpsSecondaryInteraction
    {
        public const string LampName = "충격 버튼", PadName = "패드", LidName = "뚜껑";
        /// <summary>How far the lid swings open (degrees about its back hinge) and how long it takes.</summary>
        public const float LidOpenDegrees = 110f, LidSeconds = .4f;
        /// <summary>The procedural pad (width, thickness, length) in metres; with <see cref="EmergencyArt.AedPadModel"/> the model's own bounds apply.</summary>
        public static readonly Vector3 DefaultPadSize = new Vector3(.09f, .008f, .11f);
        /// <summary>The pad as built (width, thickness, length): its collider box, centred on the pad root.</summary>
        public Vector3 PadSize { get; private set; } = DefaultPadSize;

        public StaffHands Hands;
        public AedCabinet Home { get; private set; }
        public bool Held { get; set; }
        /// <summary>The device's own course; its counts stay when the unit is lifted again.</summary>
        public AedProcedure Procedure { get; } = new AedProcedure();
        /// <summary>The collapsed person this unit stands beside (set down within reach), else null.</summary>
        public CollapseHazard Patient { get; private set; }
        /// <summary>The two pads, in the lid, in a hand or on the chest.</summary>
        public AedPad[] Pads => pads;
        /// <summary>The lid stands open (far enough that the pads inside can be reached).</summary>
        public bool LidOpen => lidAngle >= LidOpenDegrees * .6f;

        private EmergencySession session;
        private readonly System.Collections.Generic.List<Renderer> lamp = new System.Collections.Generic.List<Renderer>();
        private MaterialPropertyBlock lampBlock;
        private Transform lidPivot;
        private GameObject closedVisual, openVisual;
        /// <summary>The case collider follows the visible case: the closed box while off, the open case's body (lid excluded) while on, so the pads on the body stand proud of it.</summary>
        private Bounds closedBox, openBox;
        private float lidAngle;
        private readonly AedPad[] pads = new AedPad[AedProcedure.PadsNeeded];
        private readonly LineRenderer[] cables = new LineRenderer[AedProcedure.PadsNeeded];
        private static Material padMaterial, cableMaterial, lampMaterial, lidMaterial;
        private bool wired;

        /// <summary>
        /// The carry case. With <see cref="EmergencyArt.AedUnitModel"/> it is the Meshy model (31 x 24 x 27 cm, origin at the bottom
        /// centre) under a box collider on its bounds; otherwise a procedural case about 32 x 26 x 11 cm: yellow body, green band, grey
        /// handle. The top carries the two pads and the shock button (<see cref="EmergencyArt.AedShockButtonModel"/> or a small round
        /// lamp): dark off, green on, amber while analysing, red flashing when charged. Opened, the case shows
        /// <see cref="EmergencyArt.AedOpenModel"/> in place of the closed one; without it a thin hinged lid swings up and back.
        /// </summary>
        public static AedUnit Build(StaffHands hands, AedCabinet home, EmergencyArt art = null)
        {
            var root = new GameObject("자동심장충격기(AED)");
            var unit = root.AddComponent<AedUnit>();
            unit.Hands = hands;
            unit.Home = home;
            Bounds top;
            if (art != null && art.AedUnitModel != null)
            {
                var visual = Object.Instantiate(art.AedUnitModel, root.transform);
                visual.name = "본체 (모델)";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                foreach (var collider in visual.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
                top = Props.LocalBounds(root.transform);
                var box = root.AddComponent<BoxCollider>();
                box.center = top.center;
                box.size = top.size;
                unit.closedVisual = visual;
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                var group = new GameObject("본체 (절차)");
                group.transform.SetParent(root.transform, false);
                unit.closedVisual = group;
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
                    part.transform.SetParent(group.transform, false);
                    part.transform.localPosition = centre;
                    part.transform.localScale = size;
                    part.GetComponent<MeshRenderer>().sharedMaterial = material;
                    if (!collide) Destroy(part.GetComponent<Collider>());
                }
                Part("본체", new Vector3(0, .13f, 0), new Vector3(.32f, .26f, .11f), Paint("AED 본체 (노랑)", new Color(.98f, .8f, .1f)), false);
                Part("덮개 띠", new Vector3(0, .2f, .056f), new Vector3(.3f, .07f, .004f), Paint("AED 표시 (초록)", new Color(0, .6f, .27f)), false);
                top = new Bounds(new Vector3(0, .13f, 0), new Vector3(.32f, .26f, .11f));
                var box = root.AddComponent<BoxCollider>();   // on the root, so it stays the aim target whichever visual shows
                box.center = top.center;
                box.size = top.size;
            }
            if (art != null && art.AedOpenModel != null)
            {
                // 열린 본체: 켜진 동안 닫힌 본체 대신 보인다. 바닥 원점을 같이 쓰고, 뚜껑은 내려놓은 사람 반대쪽(-z)에 서도록 돌린다.
                var open = Object.Instantiate(art.AedOpenModel, root.transform);
                open.name = "본체 열림 (모델)";
                open.transform.localPosition = Vector3.zero;
                open.transform.localRotation = Quaternion.identity;
                foreach (var collider in open.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
                unit.closedBox = top;
                if (OpenCaseBody(open, out var body)) top = body;
                unit.openBox = top;
                open.SetActive(false);
                unit.openVisual = open;
            }
            // 버튼은 오른쪽 끝(패드 두 장 옆), 패드는 왼쪽부터 나란히: 31 cm 폭에 12 cm 패드 두 장과 4 cm 버튼이 겹치지 않게 들어간다.
            unit.AddLamp(new Vector3(top.max.x - .03f, top.max.y + .002f, top.center.z), art != null ? art.AedShockButtonModel : null);
            if (unit.openVisual == null) unit.AddLid(top, withHandle: art == null || art.AedUnitModel == null);
            unit.AddPads(top, art != null ? art.AedPadModel : null);
            return unit;
        }

        /// <summary>
        /// Reads the open case's vertices (unit space): the lid is the tall part, and it must stand at the back (-z), away from
        /// whoever set the case down, so the model is turned round when its lid is at +z. Returns the body's bounds (the half
        /// without the lid, up to its rim) for placing the pads and the button; false when the mesh cannot be read.
        /// </summary>
        private static bool OpenCaseBody(GameObject open, out Bounds body)
        {
            body = default;
            var points = new System.Collections.Generic.List<Vector3>();
            var toUnit = open.transform.parent.worldToLocalMatrix;
            foreach (var filter in open.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var toWorld = filter.transform.localToWorldMatrix;
                foreach (var v in mesh.vertices) points.Add(toUnit.MultiplyPoint3x4(toWorld.MultiplyPoint3x4(v)));
            }
            if (points.Count == 0) return false;
            float top = float.NegativeInfinity;
            foreach (var p in points) top = Mathf.Max(top, p.y);
            float lidZ = 0; int lidCount = 0;
            foreach (var p in points) if (p.y > top * .6f) { lidZ += p.z; lidCount++; }
            bool turn = lidCount > 0 && lidZ / lidCount > 0;
            if (turn) open.transform.localRotation = Quaternion.Euler(0, 180, 0);
            float sign = turn ? -1 : 1;   // after the turn the lid is at -z; the body is the +z half
            bool any = false;
            foreach (var p in points)
            {
                if (p.y > top * .6f) continue;   // lid
                var q = new Vector3(p.x * sign, p.y, p.z * sign);
                if (q.z < 0) continue;   // the lid's half
                if (!any) { body = new Bounds(q, Vector3.zero); any = true; } else body.Encapsulate(q);
            }
            return any;
        }

        /// <summary>The shock button on the top: the Meshy button model (origin at its base) when given, else a small round lamp. Its colour is the device's state.</summary>
        private void AddLamp(Vector3 localPosition, GameObject model)
        {
            GameObject button;
            if (model != null)
            {
                button = Object.Instantiate(model, transform);
                foreach (var collider in button.GetComponentsInChildren<Collider>()) Destroy(collider);
                button.transform.localRotation = Quaternion.identity;
                lamp.AddRange(button.GetComponentsInChildren<Renderer>());
            }
            else
            {
                button = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(button.GetComponent<Collider>());   // the case's collider is the aim target
                button.transform.SetParent(transform, false);
                button.transform.localScale = new Vector3(.036f, .004f, .036f);
                localPosition.y += .004f;
                if (lampMaterial == null)
                {
                    lampMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "AED 충격 버튼" };
                    lampMaterial.SetColor("_BaseColor", new Color(.2f, .2f, .22f));
                }
                button.GetComponent<MeshRenderer>().sharedMaterial = lampMaterial;
                lamp.Add(button.GetComponent<MeshRenderer>());
            }
            button.name = LampName;
            button.transform.localPosition = localPosition;
            lampBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// A thin lid over the top of the case, hinged along its back edge (-z, away from whoever set it down); it swings up and back
        /// when opened. The procedural case carries its grey handle on the lid (<paramref name="withHandle"/>), so the pads underneath stay clear of it.
        /// </summary>
        private void AddLid(Bounds top, bool withHandle)
        {
            var pivot = new GameObject(LidName).transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(top.center.x, top.max.y + .003f, top.min.z);
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "뚜껑 판";
            Destroy(plate.GetComponent<Collider>());
            plate.transform.SetParent(pivot, false);
            plate.transform.localPosition = new Vector3(0, 0, top.extents.z);
            plate.transform.localScale = new Vector3(top.size.x, .006f, top.size.z);
            if (lidMaterial == null)
            {
                lidMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "AED 뚜껑 (초록)" };
                lidMaterial.SetColor("_BaseColor", new Color(0, .55f, .25f));
                lidMaterial.SetFloat("_Smoothness", .5f);
            }
            plate.GetComponent<MeshRenderer>().sharedMaterial = lidMaterial;
            if (withHandle)
            {
                var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handle.name = "손잡이";
                Destroy(handle.GetComponent<Collider>());
                handle.transform.SetParent(pivot, false);
                handle.transform.localPosition = new Vector3(0, .016f, top.extents.z);
                handle.transform.localScale = new Vector3(.14f, .025f, .03f);
                var grey = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "AED 손잡이" };
                grey.SetColor("_BaseColor", new Color(.25f, .26f, .27f));
                grey.SetFloat("_Smoothness", .45f);
                handle.GetComponent<MeshRenderer>().sharedMaterial = grey;
            }
            lidPivot = pivot;
        }

        /// <summary>
        /// The two pads lie side by side on the top of the case, under the lid, each its own small target: a pad root (collider,
        /// <see cref="AedPad"/>) with the Meshy pad model or a flat white block as its visual, centred on the root.
        /// </summary>
        private void AddPads(Bounds top, GameObject model)
        {
            if (padMaterial == null)
            {
                padMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "AED 패드 (흰색)" };
                padMaterial.SetColor("_BaseColor", new Color(.93f, .93f, .9f));
                cableMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "AED 패드 케이블" };
                cableMaterial.SetColor("_BaseColor", new Color(.15f, .15f, .16f));
            }
            for (int i = 0; i < pads.Length; i++)
            {
                var padRoot = new GameObject(PadName + " " + (i + 1));
                padRoot.transform.SetParent(transform, false);
                if (model != null)
                {
                    var visual = Object.Instantiate(model, padRoot.transform);
                    visual.name = "패드 (모델)";
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    foreach (var collider in visual.GetComponentsInChildren<Collider>()) Destroy(collider);
                    var bounds = Props.LocalBounds(padRoot.transform);
                    visual.transform.localPosition = -bounds.center;   // the root sits at the pad's centre
                    if (i == 0) PadSize = bounds.size;
                }
                else
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "패드 (절차)";
                    Destroy(cube.GetComponent<Collider>());
                    cube.transform.SetParent(padRoot.transform, false);
                    cube.transform.localScale = DefaultPadSize;
                    cube.GetComponent<MeshRenderer>().sharedMaterial = padMaterial;
                    if (i == 0) PadSize = DefaultPadSize;
                }
                padRoot.AddComponent<BoxCollider>().size = PadSize;
                var pad = padRoot.AddComponent<AedPad>();
                pad.Unit = this;
                pad.Index = i;
                float y = top.max.y + PadSize.y * .5f + .002f;
                float z = Mathf.Min(top.center.z, top.max.z - PadSize.z * .5f);
                pad.LidPosition = new Vector3(top.min.x + .005f + PadSize.x * .5f + i * (PadSize.x + .01f), y, z);
                pads[i] = pad;
                var cable = new GameObject("케이블 " + (i + 1)).AddComponent<LineRenderer>();
                cable.transform.SetParent(transform, false);
                cable.useWorldSpace = true;
                cable.positionCount = 3;
                cable.widthMultiplier = .006f;
                cable.numCapVertices = 2;
                cable.sharedMaterial = cableMaterial;
                cable.enabled = false;
                cables[i] = cable;
                ReturnPad(pad);
            }
        }

        /// <summary>The case's collider (and the lid pads') on or off; a pad out of the lid never aims.</summary>
        public void SetColliders(bool on)
        {
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                var pad = collider.GetComponent<AedPad>();
                collider.enabled = on && (pad == null || pad.Where == AedPad.Place.Lid);
            }
        }

        // ── 바닥에 놓임 / 다시 들림 ──────────────────────────────────────────

        /// <summary>
        /// Set down at <paramref name="beside"/> (the collapsed person within reach, else null). Beside a patient the device
        /// becomes workable and speaks through the HUD; its counts are kept on the casualty (<see cref="CollapseHazard.Aed"/>).
        /// </summary>
        public void Landed(EmergencySession owner, CollapseHazard beside)
        {
            session = owner;
            Patient = beside;
            if (Patient == null) return;
            Patient.Aed = Procedure;
            Procedure.Shockable = () => Patient != null && Patient.Active && Patient.NotBreathingNormally && !Patient.Treated;
            Procedure.HandsOn = () => Patient != null && Patient.Compressing;
            if (!wired)
            {
                wired = true;
                Procedure.Voice += Speak;
                Procedure.Logged += Note;
            }
        }

        /// <summary>Taken in hand again: a switched-on device is switched off, its pads come back into the lid and the lid shuts.</summary>
        public void Lifted()
        {
            if (Procedure.Current != AedProcedure.Stage.Off) Procedure.PowerOff();
            foreach (var pad in pads) ReturnPad(pad);
            Patient = null;
            Paint(new Color(.2f, .2f, .22f));
        }

        private void Speak(string line)
        {
            if (session != null && session.Hud != null) session.Hud.Toast("AED 음성 · " + line, 5f);
        }

        private void Note(string fact)
        {
            if (session == null || session.Log == null) return;
            if (Patient != null) session.Log.Add(fact + " · 쓰러진 지 " + Mathf.RoundToInt(Time.time - Patient.StartedAt) + "초 · " + Patient.Where);
            else session.Log.Add(fact);
        }

        // ── 패드 ─────────────────────────────────────────────────────────────

        /// <summary>The pad still in the lid with the lowest number, or null when both are out.</summary>
        public AedPad PadInLid()
        {
            foreach (var pad in pads) if (pad != null && pad.Where == AedPad.Place.Lid) return pad;
            return null;
        }

        /// <summary>This pad can be taken out of the lid now (lid open, device wanting pads, no other pad in hand).</summary>
        public bool CanTakePad(AedPad pad) =>
            !Held && Patient != null && pad != null && pad.Where == AedPad.Place.Lid && LidOpen
            && Procedure.Current == AedProcedure.Stage.PadsWanted && !Procedure.PadInHand && Hands != null;

        /// <summary>E on a pad in the lid: it goes into the staff member's hand.</summary>
        public bool TakePad(AedPad pad, out string feedback)
        {
            feedback = null;
            if (!CanTakePad(pad) || !Procedure.TakePad()) return false;
            pad.Where = AedPad.Place.Hand;
            pad.GetComponent<Collider>().enabled = false;
            Hands.HoldPad(pad);
            feedback = Procedure.Pads == 0 ? "패드를 집었습니다 · 환자의 오른쪽 빗장뼈 아래에 E · 붙이기" : "패드를 집었습니다 · 환자의 왼쪽 옆구리에 E · 붙이기";
            return true;
        }

        /// <summary>The pad goes back into its place in the lid (put down, or the unit lifted away).</summary>
        public void ReturnPad(AedPad pad)
        {
            if (pad == null) return;
            if (pad.Where == AedPad.Place.Hand) Procedure.ReturnPad();
            pad.Where = AedPad.Place.Lid;
            pad.transform.SetParent(transform, false);
            pad.transform.localPosition = pad.LidPosition;
            pad.transform.localRotation = Quaternion.identity;
            pad.transform.localScale = Vector3.one;
            pad.GetComponent<Collider>().enabled = !Held && (Home == null || !Home.Stored);
            pad.gameObject.SetActive(LidOpen);
            if (cables[pad.Index] != null) cables[pad.Index].enabled = false;
        }

        /// <summary>
        /// The pad in hand is stuck on the patient's chest: the first upper right (below the collarbone), the second lower left (the
        /// mid-axillary line), placed along the body's own axis from the humanoid bones, with a thin cable back to the case.
        /// </summary>
        public bool AttachPad(AedPad pad, out string feedback)
        {
            feedback = null;
            if (pad == null || pad.Where != AedPad.Place.Hand || Patient == null || Patient.Person == null) return false;
            if (!Procedure.AttachPad()) { feedback = Procedure.Refusal; return false; }
            int spot = Procedure.Pads - 1;   // the order they went on, not the pad's number
            var body = Patient.Person.Body;
            var animator = body != null ? body.Animator : null;
            var hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            var head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            var chest = animator != null ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
            var anchor = chest != null ? chest : Patient.Person.transform;
            var axis = hips != null && head != null ? (head.position - hips.position).normalized : Vector3.forward;
            var side = Vector3.Cross(Vector3.up, axis);
            if (side.sqrMagnitude < 1e-4f) side = Patient.Person.transform.right; else side.Normalize();
            var up = Vector3.Cross(axis, side).normalized;
            if (up.y < 0) up = -up;
            var centre = chest != null ? chest.position : Patient.Person.transform.position + Vector3.up * .25f;
            var at = spot == 0 ? centre + axis * .10f - side * .09f + up * .03f : centre - axis * .08f + side * .13f + up * .03f;
            pad.Where = AedPad.Place.Chest;
            pad.transform.SetParent(anchor, true);
            pad.transform.position = at;
            pad.transform.rotation = Quaternion.LookRotation(axis, up);
            pad.transform.localScale = InverseScale(anchor.lossyScale);   // the pad keeps its own size under a scaled bone
            pad.GetComponent<Collider>().enabled = false;
            if (cables[pad.Index] != null) cables[pad.Index].enabled = true;
            if (Procedure.Pads >= AedProcedure.PadsNeeded && Patient.AedPadsAt < 0) Patient.AedPadsAt = Time.time;
            feedback = Procedure.Pads < AedProcedure.PadsNeeded ? "패드 1장 부착 · 다른 패드를 집어 왼쪽 옆구리에" : "패드 2장 부착 · 손을 떼고 \"물러나세요\" 외치기 (환자에 E)";
            return true;
        }

        private static Vector3 InverseScale(Vector3 scale) =>
            new Vector3(scale.x > 1e-4f ? 1 / scale.x : 1, scale.y > 1e-4f ? 1 / scale.y : 1, scale.z > 1e-4f ? 1 / scale.z : 1);

        /// <summary>Cables from the case to every pad that is out of the lid (in hand or on the chest).</summary>
        private void LayCables()
        {
            var from = transform.position + Vector3.up * .05f;
            for (int i = 0; i < pads.Length; i++)
            {
                if (pads[i] == null || cables[i] == null) continue;
                bool taken = pads[i].Where != AedPad.Place.Lid;
                cables[i].enabled = taken;
                if (!taken) continue;
                var to = pads[i].transform.position;
                var mid = (from + to) * .5f;
                mid.y = Mathf.Min(from.y, to.y) + .01f;
                cables[i].SetPosition(0, from);
                cables[i].SetPosition(1, mid);
                cables[i].SetPosition(2, to);
            }
        }

        private void Paint(Color colour)
        {
            if (lampBlock == null) return;
            lampBlock.SetColor("_BaseColor", colour);
            foreach (var renderer in lamp) if (renderer != null) renderer.SetPropertyBlock(lampBlock);
        }

        private void Update()
        {
            // 뚜껑: 켜져 있으면(열렸으면) 뒤로 젖혀지고, 꺼지면 닫힌다. 안의 패드는 뚜껑이 충분히 열렸을 때만 보인다.
            // 열린 본체 모델이 있으면 판 뚜껑 대신 닫힌 본체 ↔ 열린 본체를 바꿔 보인다.
            bool on = Procedure.Current != AedProcedure.Stage.Off;
            float lidTarget = on ? LidOpenDegrees : 0f;
            if (openVisual != null && openVisual.activeSelf != on)
            {
                openVisual.SetActive(on);
                if (closedVisual != null) closedVisual.SetActive(!on);
                var box = GetComponent<BoxCollider>();
                if (box != null && openBox.size.sqrMagnitude > 0)
                {
                    var shape = on ? openBox : closedBox;
                    box.center = shape.center;
                    box.size = shape.size;
                }
            }
            if (!Mathf.Approximately(lidAngle, lidTarget))
            {
                lidAngle = Mathf.MoveTowards(lidAngle, lidTarget, LidOpenDegrees / LidSeconds * Time.deltaTime);
                if (lidPivot != null) lidPivot.localRotation = Quaternion.Euler(-lidAngle, 0, 0);
                bool show = LidOpen;
                foreach (var pad in pads) if (pad != null && pad.Where == AedPad.Place.Lid && pad.gameObject.activeSelf != show) pad.gameObject.SetActive(show);
            }
            if (Held || Patient == null) return;
            Procedure.Tick(Time.deltaTime);
            if (Patient.Treated && Procedure.Current != AedProcedure.Stage.Off) Procedure.HandOver();
            LayCables();
            // 램프: 꺼짐 어둡게, 켜짐 초록, 물러나기 대기·분석 주황 깜빡임, 충전 뒤 빨강 깜빡임, 압박 중 초록 느린 깜빡임.
            bool blink = Mathf.Repeat(Time.time, .5f) < .25f, slow = Mathf.Repeat(Time.time, 1.6f) < .8f;
            switch (Procedure.Current)
            {
                case AedProcedure.Stage.PadsWanted: Paint(new Color(.1f, .8f, .3f)); break;
                case AedProcedure.Stage.ClearWanted:
                case AedProcedure.Stage.Analysing: Paint(blink ? new Color(1f, .65f, .1f) : new Color(.3f, .2f, .05f)); break;
                case AedProcedure.Stage.ShockAdvised: Paint(Procedure.Charged ? (blink ? new Color(1f, .1f, .08f) : new Color(.3f, .05f, .04f)) : new Color(1f, .65f, .1f)); break;
                case AedProcedure.Stage.Compressions: Paint(slow ? new Color(.1f, .8f, .3f) : new Color(.05f, .25f, .1f)); break;
                default: Paint(new Color(.2f, .2f, .22f)); break;
            }
        }

        // ── 손 조작 ─────────────────────────────────────────────────────────

        public string DisplayName => "자동심장충격기(AED)";
        public string StateText => Held || Patient == null ? "" : Procedure.StateText;
        public string InteractionPrompt => Held ? "" : Patient == null ? "AED 들기" : Procedure.Prompt;

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (Hands == null || Held || responder == null || responder.IsPaused) { reason = ""; return false; }
            if (Patient == null) return true;
            if (Procedure.Prompt.Length > 0) return true;
            reason = Procedure.Refusal ?? "";
            return false;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            if (Patient == null)
            {
                Hands.TakeAed(this);
                feedback = "AED 를 들었습니다";
                return true;
            }
            switch (Procedure.Current)
            {
                case AedProcedure.Stage.Off:
                    if (!Procedure.OpenLid()) return false;
                    feedback = "AED 뚜껑을 열었습니다 · 전원이 켜졌습니다 · 음성 안내를 따르십시오";
                    return true;
                case AedProcedure.Stage.ShockAdvised:
                    if (!Procedure.PressShock()) { feedback = Patient.Compressing ? "가슴압박을 멈추고(R) 환자에게서 떨어진 뒤 누르십시오" : "아직 충전 중입니다"; return false; }
                    feedback = "전기충격 " + Procedure.Shocks + "회 · 즉시 가슴압박 계속(R)";
                    return true;
                default:
                    return false;
            }
        }

        public string SecondaryPrompt => !Held && Patient != null && Hands != null ? "AED 들기" : "";

        public bool TrySecondary(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (SecondaryPrompt.Length == 0 || responder == null || responder.IsPaused) return false;
            bool wasOn = Procedure.Current != AedProcedure.Stage.Off;
            Hands.TakeAed(this);
            feedback = wasOn ? "AED 전원을 끄고 패드를 거둬 들었습니다" : "AED 를 들었습니다";
            return true;
        }
    }

    /// <summary>
    /// One of the AED's two electrode pads. In the lid it is its own small target ("패드 들기"); in the staff member's hand it
    /// goes on the patient with E on the patient (<see cref="Passenger"/>); on the chest it stays until the unit is lifted.
    /// </summary>
    public sealed class AedPad : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public enum Place { Lid, Hand, Chest }

        public AedUnit Unit;
        public int Index;
        public Place Where { get; set; } = Place.Lid;
        /// <summary>Its place in the lid (unit local).</summary>
        public Vector3 LidPosition;

        public string DisplayName => "AED 패드 " + (Index + 1);
        public string InteractionPrompt => Where == Place.Lid ? "패드 들기" : "";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (Unit == null || responder == null || responder.IsPaused || Where != Place.Lid) { reason = ""; return false; }
            if (Unit.CanTakePad(this)) return true;
            reason = Unit.Procedure.PadInHand ? "손에 든 패드를 먼저 붙이십시오" : Unit.Procedure.Current == AedProcedure.Stage.Off ? "뚜껑을 먼저 여십시오" : "지금은 패드가 필요하지 않습니다";
            return false;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            return Unit.TakePad(this, out feedback);
        }
    }
}
