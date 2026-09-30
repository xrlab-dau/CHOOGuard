using System;
using System.Collections;
using System.Collections.Generic;
using ChooGuard.App.Fps.Hud;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// One of the station's legally placed extinguishers (ABC powder, 3.3 kg class) turned into a usable tool for the
    /// shift. Its gauge/defect state comes from the twin's inspection data: a unit with no pressure does not discharge.
    /// </summary>
    public sealed class Extinguisher : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        /// <summary>Approximate full discharge time of a 3.3 kg powder unit (seconds).</summary>
        public const float DischargeSeconds = 14f;

        public string Serial = "";
        /// <summary>What it sprays: the twin's extinguishers are ABC powder, the K-class extinguishers of the shop kitchens wet chemical.</summary>
        public ExtinguishAgent Type = ExtinguishAgent.Powder;
        public bool Defective;
        public StaffHands Hands;
        public float Agent { get; set; } = 1f;
        public bool PinPulled { get; set; }
        public bool Held { get; set; }
        public float SprayedSeconds { get; set; }

        public string DisplayName => (Type == ExtinguishAgent.WetChemical ? "K급 소화기 · " : "소화기 · ") + Serial;
        public string InteractionPrompt => Held ? "" : Agent <= .01f ? "빈 소화기" : "소화기 들기";

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
            Hands.PickUp(this);
            feedback = PinPulled ? "소화기를 들었습니다" : "소화기를 들었습니다 · 좌클릭을 길게 눌러 안전핀을 뽑습니다";
            return true;
        }
    }

    /// <summary>
    /// An indoor hydrant cabinet (옥내소화전함) of the twin turned into a usable hose: the staff member takes the nozzle for
    /// early firefighting (research: 관계인·발견자가 호스·관창으로 초기 소화; JEV 010 include_hydrant_cabinet). The hose reaches
    /// 30 m (two 15 m lengths, the usual cabinet set — not measured here); letting go rewinds it into the cabinet.
    /// </summary>
    public sealed class HydrantHose : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public const float Reach = 30f;
        public StaffHands Hands;
        public bool Out { get; set; }

        /// <summary>Where the hose leaves the cabinet (front face, near the floor).</summary>
        public Vector3 Outlet => transform.position + transform.forward * .15f + Vector3.down * .5f;

        public string DisplayName => "옥내소화전";
        public string InteractionPrompt => Out ? "" : "호스·관창 꺼내기 (방수)";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            bool available = Hands != null && !Out && responder != null && !responder.IsPaused;
            reason = available ? null : Out ? "호스가 나가 있습니다" : "";
            return available;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out _)) return false;
            Hands.TakeHose(this);
            feedback = "관창을 들었습니다 · 좌클릭을 누른 채 불의 아랫부분에 방수 · G 로 되감기";
            return true;
        }
    }

    /// <summary>
    /// What the staff member holds: an extinguisher (pin, discharge), a hydrant nozzle (aim at the base of the fire), or the
    /// AED (carried to a collapsed person and set down); G drops or rewinds.
    /// </summary>
    public sealed class StaffHands : MonoBehaviour
    {
        private const float PinSeconds = .8f;

        public Extinguisher Held { get; private set; }
        public HydrantHose Hose { get; private set; }
        public AedUnit Aed { get; private set; }
        public bool Spraying { get; private set; }

        private EmergencySession session;
        private Transform view;
        private ParticleSystem powder, water;
        private LineRenderer hoseLine;
        private bool trigger, warnedEmpty;
        private float pinHold;
        private readonly List<Renderer> heldRenderers = new List<Renderer>();
        public void Setup(EmergencySession owner, EmergencyArt art)
        {
            session = owner;
            view = owner.Player.PlayerCamera.transform;
            powder = Particles.Powder(view, art.Smoke);
            powder.transform.localPosition = new Vector3(.16f, -.26f, .75f);
            water = Particles.Water(view, art.Smoke);
            water.transform.localPosition = new Vector3(.12f, -.3f, .8f);
            hoseLine = new GameObject("옥내소화전 호스").AddComponent<LineRenderer>();
            hoseLine.transform.SetParent(transform, false);
            hoseLine.useWorldSpace = true;
            hoseLine.positionCount = 4;
            hoseLine.widthMultiplier = .045f;
            hoseLine.numCapVertices = 2;
            hoseLine.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "호스 (흰 섬유)" };
            hoseLine.sharedMaterial.SetColor("_BaseColor", new Color(.86f, .84f, .78f));
            hoseLine.enabled = false;
            owner.Primary += () => trigger = true;
            owner.PrimaryReleased += () => { trigger = false; pinHold = 0; };
            owner.Drop += Drop;
            owner.SlotProviders.Add(Slot);
            // 역사 소화기를 게임용 도구로 바꾼다. 점검 상태(압력·결함)는 그대로 가져온다. 씬 자산은 바뀌지 않는다.
            foreach (var inspectable in Object.FindObjectsByType<Work.FacilityInspectable>(FindObjectsSortMode.None))
            {
                var tool = inspectable.gameObject.AddComponent<Extinguisher>();
                tool.Serial = inspectable.SerialNumber;
                tool.Defective = inspectable.PressureOutOfRange || inspectable.MechanicallyDefective;
                tool.Hands = this;
                Destroy(inspectable);
            }
            foreach (var fixture in Object.FindObjectsByType<Facilities.StationFixture>(FindObjectsSortMode.None))
            {
                if (fixture.Kind == Facilities.StationFixture.FixtureKind.Hydrant) fixture.gameObject.AddComponent<HydrantHose>().Hands = this;
                else if (fixture.Kind == Facilities.StationFixture.FixtureKind.Aed) AedCabinet.Build(fixture.gameObject, this);
            }
        }

        /// <summary>Takes the AED (from its cabinet or the floor); anything else in hand is put down first.</summary>
        public void TakeAed(AedUnit unit)
        {
            if (Hose != null) RewindHose();
            if (Held != null) Drop();
            if (Aed != null && Aed != unit) SetDownAed(null);
            if (unit.Home != null && unit.Home.Stored) unit.Home.Release();
            Aed = unit;
            unit.Held = true;
            unit.SetColliders(false);
            foreach (var renderer in unit.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            unit.transform.SetParent(view, false);
            unit.transform.localPosition = new Vector3(.3f, -.46f, .5f);
            unit.transform.localRotation = Quaternion.Euler(8, 162, 0);
            session.Log.Add("AED 를 들었다 · " + session.World.Describe(session.Player.transform.position));
        }

        /// <summary>Puts the AED back in its own cabinet.</summary>
        public void StoreAed(AedCabinet cabinet)
        {
            if (Aed == null || Aed != cabinet.Unit) return;
            foreach (var renderer in Aed.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            Aed = null;
            cabinet.Store();
            session.Log.Add("AED 를 보관함에 넣었다");
        }

        /// <summary>Sets the AED down on the floor beside <paramref name="person"/> (on the staff member's side).</summary>
        public string PlaceAedBeside(Passenger person)
        {
            if (Aed == null) return null;
            var toward = session.Player.transform.position - person.transform.position;
            toward.y = 0;
            var spot = person.transform.position + (toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward) * .6f;
            return SetDownAed(spot) ?? "AED 를 내려놓았습니다";
        }

        /// <summary>Puts the AED on the floor (<paramref name="spot"/>, else just ahead); returns the hand-over line if a collapsed person is beside it.</summary>
        private string SetDownAed(Vector3? spot)
        {
            var unit = Aed;
            Aed = null;
            unit.Held = false;
            unit.transform.SetParent(null, true);
            var at = spot ?? view.position + Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized * .7f;
            var floor = at - Vector3.up * 1.5f;
            float best = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * .5f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.distance < best && hit.collider.GetComponentInParent<PersonBody>() == null) { best = hit.distance; floor = hit.point; }
            unit.transform.SetPositionAndRotation(floor, Quaternion.Euler(0, view.eulerAngles.y + 180, 0));
            unit.SetColliders(true);
            foreach (var renderer in unit.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return session.Incidents.AedSetDown(floor);
        }

        public void TakeHose(HydrantHose hose)
        {
            if (Held != null) Drop();
            if (Aed != null) SetDownAed(null);
            if (Hose != null) RewindHose();
            Hose = hose;
            hose.Out = true;
            hoseLine.enabled = true;
            session.Log.Add("옥내소화전 호스를 꺼내 관창을 들었다 · " + session.World.Describe(hose.transform.position));
        }

        private void RewindHose()
        {
            if (Hose == null) return;
            Hose.Out = false;
            Hose = null;
            hoseLine.enabled = false;
            Spraying = false;
            SetWater(0);
        }

        public void PickUp(Extinguisher tool)
        {
            if (Hose != null) RewindHose();
            if (Aed != null) SetDownAed(null);
            if (Held != null) Drop();
            Held = tool;
            tool.Held = true;
            foreach (var collider in tool.GetComponentsInChildren<Collider>()) collider.enabled = false;
            heldRenderers.Clear();
            tool.GetComponentsInChildren(heldRenderers);
            var bounds = heldRenderers.Count > 0 ? heldRenderers[0].bounds : new Bounds(tool.transform.position, Vector3.one * .5f);
            foreach (var renderer in heldRenderers) { bounds.Encapsulate(renderer.bounds); renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            // 본체 윗면 중앙에서 원점까지의 거리(도구 좌표계). 손에 들면 윗면이 화면 오른쪽 아래에 오도록 원점을 둔다.
            var pivotFromTop = Quaternion.Inverse(tool.transform.rotation) * (tool.transform.position - new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
            var grip = Quaternion.Euler(12, -20, 0);
            tool.transform.SetParent(view, false);
            tool.transform.localRotation = grip;
            tool.transform.localPosition = new Vector3(.24f, -.13f, .55f) + grip * pivotFromTop;
            session.Log.Picked(tool);
        }

        public void Drop()
        {
            if (Hose != null) { session.Log.Add("옥내소화전 호스를 되감았다"); RewindHose(); return; }
            if (Aed != null)
            {
                var handover = SetDownAed(null);
                if (handover != null) session.Hud.Toast(handover);
                return;
            }
            if (Held == null) return;
            var tool = Held;
            Held = null;
            Spraying = false;
            SetEmission(0);
            tool.Held = false;
            tool.transform.SetParent(null, true);
            var ahead = view.position + Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized * .7f;
            var floor = Physics.Raycast(ahead + Vector3.up * .5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : ahead - Vector3.up * 1.5f;
            tool.transform.SetPositionAndRotation(floor, Quaternion.Euler(0, view.eulerAngles.y, 0));
            foreach (var collider in tool.GetComponentsInChildren<Collider>()) collider.enabled = true;
            foreach (var renderer in heldRenderers) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        private GameHud.Slot? Slot()
        {
            if (Hose != null)
            {
                float used = Vector3.Distance(Hose.Outlet, session.Player.transform.position) / HydrantHose.Reach;
                return new GameHud.Slot { Label = "옥내소화전 관창", Hint = "좌클릭 · 방수  G · 되감기", Fill = 1 - Mathf.Clamp01(used), Active = Spraying };
            }
            if (Aed != null) return new GameHud.Slot { Label = "자동심장충격기(AED)", Hint = "쓰러진 사람 곁에 E · 내려놓기  G · 놓기", Fill = 1, Active = false };
            if (Held == null) return null;
            string hint = !Held.PinPulled ? "좌클릭 길게 · 안전핀" : Held.Agent <= .01f ? "약제 없음 · G 놓기" : "좌클릭 · 분사  G · 놓기";
            return new GameHud.Slot { Label = (Held.Type == ExtinguishAgent.WetChemical ? "K급 소화기 " : "소화기 ") + Held.Serial.Replace("BSN-CONC-", ""), Hint = hint, Fill = Held.Agent, Active = Spraying };
        }

        private void Update()
        {
            if (session != null && Hose != null) { UseHose(); return; }
            if (session == null || Held == null) { Spraying = false; SetEmission(0); return; }
            var hud = session.Hud;
            if (session.Player.IsPaused) { trigger = false; Spraying = false; SetEmission(0); hud.SetProgress(null); return; }
            if (!trigger) { Spraying = false; SetEmission(0); if (!Held.PinPulled) hud.SetProgress(null); return; }
            if (!Held.PinPulled)
            {
                pinHold += Time.deltaTime;
                hud.SetProgress(pinHold / PinSeconds);
                if (pinHold >= PinSeconds)
                {
                    Held.PinPulled = true;
                    hud.SetProgress(null);
                    hud.Toast("안전핀을 뽑았습니다 · 불의 아랫부분을 향해 누른 채 쓸듯이 분사");
                    session.Log.PinPulled(Held);
                    trigger = false;
                }
                return;
            }
            if (Held.Defective)
            {
                Spraying = false;
                SetEmission(Held.SprayedSeconds < .3f ? 40 : 0);
                Held.SprayedSeconds += Time.deltaTime;
                if (!warnedEmpty) { warnedEmpty = true; hud.Toast("레버를 눌러도 약제가 거의 나오지 않습니다 · 지시압력계가 정상 범위 밖입니다"); session.Log.DefectiveUsed(Held); }
                return;
            }
            if (Held.Agent <= 0) { Spraying = false; SetEmission(0); return; }
            Spraying = true;
            SetEmission(260);
            Held.Agent = Mathf.Max(0, Held.Agent - Time.deltaTime / Extinguisher.DischargeSeconds);
            Held.SprayedSeconds += Time.deltaTime;
            // 끝에서부터: 불이 꺼지면 OnFireOut 이 그 위험을 목록에서 빼므로 앞으로 돌면 목록이 바뀌었다는 예외가 난다.
            for (int i = HazardRegistry.Active.Count - 1; i >= 0; i--)
            {
                var hazard = HazardRegistry.Active[i];
                if (!(hazard is FireHazard fire) || fire.Extinguished) continue;
                float quality = AimQuality(view, fire);
                if (quality <= 0) continue;
                fire.SuppressWith(Held.Type, quality, Time.deltaTime);
                session.Log.Sprayed(fire, quality, Time.deltaTime);
                if (fire.Extinguished) session.Incidents.OnFireOut(fire, "역무원 소화기");
            }
        }

        /// <summary>
        /// 0..1: distance (about 1.5–5 m is right), angle off the fire and whether the stream is aimed at the base
        /// rather than the smoke. Figures follow public extinguisher guidance loosely; they are gameplay tuning.
        /// </summary>
        public static float AimQuality(Transform view, FireHazard fire)
        {
            var target = fire.Position + Vector3.up * .3f;
            var toFire = target - view.position;
            float distance = toFire.magnitude;
            float distanceQuality = distance < 1.2f ? .5f : distance <= 5f ? 1f : distance >= 8f ? 0f : 1f - (distance - 5f) / 3f;
            float angle = Vector3.Angle(view.forward, toFire);
            float angleQuality = angle <= 8 ? 1 : angle >= 22 ? 0 : 1 - (angle - 8) / 14f;
            float aimHeight = view.position.y + view.forward.y * distance;
            float baseQuality = aimHeight < fire.Position.y + .5f + fire.Intensity * .8f ? 1f : .55f;
            return distanceQuality * angleQuality * baseQuality;
        }
        /// <summary>A hydrant nozzle: 2–10 m is right (a water stream carries farther than powder); gameplay tuning.</summary>
        public static float HoseAimQuality(Transform view, FireHazard fire)
        {
            var target = fire.Position + Vector3.up * .3f;
            var toFire = target - view.position;
            float distance = toFire.magnitude;
            float distanceQuality = distance < 1.5f ? .6f : distance <= 10f ? 1f : distance >= 14f ? 0f : 1f - (distance - 10f) / 4f;
            float angle = Vector3.Angle(view.forward, toFire);
            float angleQuality = angle <= 6 ? 1 : angle >= 18 ? 0 : 1 - (angle - 6) / 12f;
            return distanceQuality * angleQuality;
        }

        private void UseHose()
        {
            var hud = session.Hud;
            // 호스는 소화전함 방수구에 이어져 있다: 뻗은 길이를 넘으면 더 끌 수 없어 관창을 놓는다.
            var feet = session.Player.transform.position;
            var hand = view.position + view.right * .12f - view.up * .3f + view.forward * .5f;
            float stretch = Vector3.Distance(Hose.Outlet, feet);
            if (stretch > HydrantHose.Reach) { hud.Toast("호스 길이(30 m) 끝입니다 · 관창을 놓아 되감았습니다"); session.Log.Add("호스 길이를 넘어 관창을 놓았다"); RewindHose(); return; }
            var floorOut = Hose.Outlet; floorOut.y = feet.y + .03f;
            hoseLine.SetPosition(0, Hose.Outlet);
            hoseLine.SetPosition(1, floorOut + Hose.transform.forward * .3f);
            hoseLine.SetPosition(2, new Vector3(feet.x, feet.y + .03f, feet.z));
            hoseLine.SetPosition(3, hand);
            if (session.Player.IsPaused || !trigger) { Spraying = false; SetWater(0); return; }
            Spraying = true;
            SetWater(320);
            for (int i = HazardRegistry.Active.Count - 1; i >= 0; i--)
            {
                var hazard = HazardRegistry.Active[i];
                if (!(hazard is FireHazard fire) || fire.Extinguished) continue;
                float quality = HoseAimQuality(view, fire);
                if (quality <= 0) continue;
                fire.SuppressWith(ExtinguishAgent.Water, quality, Time.deltaTime);
                session.Log.Sprayed(fire, quality, Time.deltaTime);
                if (fire.Extinguished) session.Incidents.OnFireOut(fire, "역무원 옥내소화전");
            }
        }

        private void SetEmission(float rate)
        {
            if (powder == null) return;
            var emission = powder.emission;
            emission.rateOverTime = rate;
            if (rate > 0 && !powder.isPlaying) powder.Play();
        }

        private void SetWater(float rate)
        {
            if (water == null) return;
            var emission = water.emission;
            emission.rateOverTime = rate;
            if (rate > 0 && !water.isPlaying) water.Play();
        }
    }

    /// <summary>Retractable-belt style cordon around a spot. People stop choosing places inside it.</summary>
    public static class Cordons
    {
        public static GameObject Place(Transform parent, Vector3 centre, float radius, string label, EmergencyArt art, StationWorld world, MonoBehaviour host)
        {
            var root = new GameObject("통제선 · " + label);
            root.transform.SetParent(parent, false);
            int count = Mathf.Clamp(Mathf.RoundToInt(2 * Mathf.PI * radius / 2.2f), 6, 28);
            var posts = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                var p = centre + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                // 기둥은 걸을 수 있는 바닥에 세운다(의자 위에 서지 않게).
                posts[i] = NavMesh.SamplePosition(p, out var floor, 1.2f, NavMesh.AllAreas) ? floor.position
                    : Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : new Vector3(p.x, centre.y, p.z);
                Props.Stanchion(root.transform, posts[i], art);
            }
            var segments = new List<GameObject>(count);
            for (int i = 0; i < count; i++)
            {
                var a = posts[i] + Vector3.up * .86f;
                var b = posts[(i + 1) % count] + Vector3.up * .86f;
                segments.Add(Props.Belt(root.transform, a, b, art.CordonTape));
            }
            world.Closed.Add((centre, radius, label));
            host.StartCoroutine(Close(segments, 4f));
            return root;
        }

        // 안쪽 사람들이 빠져나갈 시간을 준 뒤 길찾기에서 막는다.
        private static IEnumerator Close(List<GameObject> segments, float delay)
        {
            yield return new WaitForSeconds(delay);
            foreach (var segment in segments)
            {
                if (segment == null) continue;
                // 벨트 한 칸을 따라 폭 10 cm 띠로 길을 깎는다(전의 상자 띠와 같은 크기).
                var obstacle = segment.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = new Vector3(.096f, .075f, segment.GetComponent<MeshFilter>().sharedMesh.bounds.size.z);
                obstacle.carving = true;
            }
        }
    }

    /// <summary>A hanging board that comes loose in an earthquake and falls to the floor.</summary>
    public sealed class FallingBoard : MonoBehaviour
    {
        public EmergencyArt.HangingItem Item { get; private set; }
        public bool Landed { get; private set; }
        public Vector3 Impact { get; private set; }
        public event Action<FallingBoard> OnLanded;

        private float velocity, floor, settle;
        private Vector3 spinAxis;
        private Quaternion restFrom, restTo;
        private readonly List<Renderer> renderers = new List<Renderer>();

        public static FallingBoard Drop(EmergencyArt.HangingItem item, Transform parent, float floorY, System.Random random)
        {
            var root = new GameObject("낙하물 · " + item.Label);
            root.transform.SetParent(parent, false);
            root.transform.position = item.Centre;
            foreach (var part in item.Parts)
            {
                var original = GameObject.Find(part.Path);
                if (part.Mesh == null)
                {
                    // 정적 결합이 아닌 부품(글자 판 등)은 원본을 그대로 떨어뜨린다.
                    if (original != null) original.transform.SetParent(root.transform, true);
                    continue;
                }
                if (original != null && original.TryGetComponent<Renderer>(out var hidden)) hidden.enabled = false;
                var copy = new GameObject(part.Path.Substring(part.Path.LastIndexOf('/') + 1), typeof(MeshFilter), typeof(MeshRenderer));
                copy.transform.SetParent(root.transform, false);
                copy.transform.SetPositionAndRotation(part.Position, part.Rotation);
                copy.transform.localScale = part.Scale;
                copy.GetComponent<MeshFilter>().sharedMesh = part.Mesh;
                copy.GetComponent<MeshRenderer>().sharedMaterials = part.Materials;
            }
            var board = root.AddComponent<FallingBoard>();
            board.Item = item;
            board.floor = floorY;
            board.spinAxis = new Vector3((float)random.NextDouble() - .5f, 0, (float)random.NextDouble() - .5f).normalized;
            root.GetComponentsInChildren(board.renderers);
            return board;
        }

        private void Update()
        {
            if (Landed) return;
            if (settle > 0)
            {
                // 바닥에 닿은 뒤 넘어지며 거의 눕는다.
                settle = Mathf.Max(0, settle - Time.deltaTime);
                transform.rotation = Quaternion.Slerp(restTo, restFrom, settle / .35f);
                Snap();
                if (settle == 0) Finish();
                return;
            }
            velocity += 9.81f * Time.deltaTime;
            transform.position += Vector3.down * velocity * Time.deltaTime;
            transform.Rotate(spinAxis, 14 * Time.deltaTime, Space.World);
            if (Lowest() > floor) return;
            Snap();
            restFrom = transform.rotation;
            // 세로로 선 판(두께 방향이 수평)은 바닥에 닿으면 넘어져 눕고, 가로로 누운 판은 거의 그대로 내려앉는다.
            Renderer main = null;
            foreach (var renderer in renderers) if (renderer != null && (main == null || renderer.bounds.size.sqrMagnitude > main.bounds.size.sqrMagnitude)) main = renderer;
            var local = main.localBounds.size;
            var thinLocal = local.x <= local.y && local.x <= local.z ? Vector3.right : local.y <= local.z ? Vector3.up : Vector3.forward;
            var thin = main.transform.TransformDirection(thinLocal).normalized;
            bool upright = Mathf.Abs(thin.y) < .5f;
            var hinge = Vector3.Cross(Vector3.up, thin).normalized;
            restTo = upright ? Quaternion.AngleAxis(Vector3.Dot(spinAxis, thin) >= 0 ? 84 : -84, hinge) : Quaternion.AngleAxis(6, spinAxis);
            settle = .35f;
        }

        private float Lowest()
        {
            float lowest = float.PositiveInfinity;
            foreach (var renderer in renderers) if (renderer != null) lowest = Mathf.Min(lowest, renderer.bounds.min.y);
            return lowest;
        }

        private void Snap() => transform.position += Vector3.up * (floor - Lowest());

        private void Finish()
        {
            Landed = true;
            var bounds = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
            }
            Impact = new Vector3(bounds.center.x, floor, bounds.center.z);
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = transform.InverseTransformPoint(bounds.center);
            box.size = new Vector3(Mathf.Max(.3f, bounds.size.x), Mathf.Max(.3f, bounds.size.y), Mathf.Max(.3f, bounds.size.z));
            OnLanded?.Invoke(this);
        }
    }
}
