using System;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.App.Fps.Facilities
{
    /// <summary>
    /// A flap gate line at an escalator landing (1F video 000121: stainless cabinets, a green arrow lamp on the side people may
    /// enter from, a red no-entry lamp on the other, no ticket reader). It follows the escalator it fronts (JEV 011
    /// mirror_escalator): while the belt runs, the entry side shows the arrow and the flaps stay folded; once the belt is
    /// stopped or staff-closed, the flaps close across the lane, the entry lamp turns red and the belt is taken out of path
    /// finding (<see cref="Escalator.SetEntryBarred"/>). An exit-only line keeps its no-entry lamp and open flaps. No direct
    /// control: the staff member works the escalator (stop button, key restart). Placed by the interior kit (MajibangBuilder
    /// KitGate / KitFixtureMarkers); the folded flaps and lamps are in the kit batches, this adds the closed state over them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EscalatorGate : MonoBehaviour
    {
        public enum GateMode { Entry, Exit, Both }

        [Serializable]
        public struct Flap
        {
            [Tooltip("Bottom of the hinge edge, in this object's space.")] public Vector3 Origin;
            [Tooltip("Direction the flap reaches across the lane.")] public Vector3 Across;
            public float ClosedReach, Height;
        }

        [Serializable]
        public struct Lamp
        {
            [Tooltip("Lamp face centre on the cabinet end, in this object's space.")] public Vector3 Centre;
            public Vector3 Right, Out;
            public float Width, Height;
        }

        /// <summary>How far from the gate line the boarding point of its escalator may be.</summary>
        public const float EscalatorSearch = 6f;

        public string Element;
        public GateMode Mode;
        public Flap[] Flaps = Array.Empty<Flap>();
        [Tooltip("Entry-side lamps (green arrow in the kit batch) that show red no-entry while the gate is shut.")]
        public Lamp[] EntryLamps = Array.Empty<Lamp>();
        public Material Glass, Red;

        public Escalator Escalator { get; private set; }
        public bool Shut { get; private set; }

        private GameObject closed;
        private float nextSearch;
        private static Material blank;

        private void Awake()
        {
            closed = new GameObject("닫힘 (플랩·진입 금지 표시)");
            closed.transform.SetParent(transform, false);
            foreach (var flap in Flaps) BuildFlap(flap);
            foreach (var lamp in EntryLamps) BuildLamp(lamp);
            closed.SetActive(false);
        }

        /// <summary>Ties the gate to its escalator (the shift finds it by the boarding point; tests set it).</summary>
        public void Bind(Escalator escalator) => Escalator = escalator;

        private void Update()
        {
            if (Mode == GateMode.Exit) return;
            if (Escalator == null)
            {
                if (Time.time < nextSearch) return;
                nextSearch = Time.time + 1;
                Escalator = Nearest(EmergencySession.Current != null ? EmergencySession.Current.World : null);
                if (Escalator == null) return;
            }
            SetShut(!Escalator.Running || Escalator.Closed);
        }

        private void SetShut(bool shut)
        {
            if (shut == Shut) return;
            Shut = shut;
            closed.SetActive(shut);
            Escalator.SetEntryBarred(shut);
        }

        private Escalator Nearest(StationWorld world)
        {
            if (world == null) return null;
            Escalator best = null;
            float bestDistance = EscalatorSearch;
            var at = transform.position;
            foreach (var escalator in world.Escalators)
            {
                if (escalator == null || Mathf.Abs(escalator.Start.y - at.y) > 1.5f) continue;
                var d = escalator.Start - at;
                d.y = 0;
                if (d.magnitude < bestDistance) { bestDistance = d.magnitude; best = escalator; }
            }
            return best;
        }

        private void BuildFlap(Flap flap)
        {
            var across = flap.Across.normalized;
            var rotation = Quaternion.LookRotation(Vector3.Cross(across, Vector3.up), Vector3.up);
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "플랩";
            panel.transform.SetParent(closed.transform, false);
            panel.transform.localPosition = flap.Origin + across * (flap.ClosedReach * .5f) + Vector3.up * (flap.Height * .5f);
            panel.transform.localRotation = rotation;
            panel.transform.localScale = new Vector3(flap.ClosedReach, flap.Height, .012f);
            panel.GetComponent<MeshRenderer>().sharedMaterial = Glass;
            var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = "플랩 끝 표시";
            edge.transform.SetParent(closed.transform, false);
            edge.transform.localPosition = flap.Origin + across * (flap.ClosedReach - .01f) + Vector3.up * (flap.Height * .5f);
            edge.transform.localRotation = rotation;
            edge.transform.localScale = new Vector3(.02f, flap.Height, .016f);
            edge.GetComponent<MeshRenderer>().sharedMaterial = Red;
            Destroy(edge.GetComponent<Collider>());
        }

        private void BuildLamp(Lamp lamp)
        {
            // An opaque dark face a little larger than the lamp, in front of the kit's green arrow (KArrow/KCross glyphs stand
            // 5 mm off the lamp face), hides it; the red X goes on top.
            if (blank == null)
            {
                blank = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "게이트 표시등 바탕" };
                blank.SetColor("_BaseColor", new Color(.03f, .03f, .035f));
            }
            var rotation = Quaternion.LookRotation(-lamp.Out.normalized, Vector3.up);
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = "진입 금지 표시";
            face.transform.SetParent(closed.transform, false);
            face.transform.localPosition = lamp.Centre + lamp.Out.normalized * .008f;
            face.transform.localRotation = rotation;
            face.transform.localScale = new Vector3(lamp.Width + .03f, lamp.Height + .02f, 1);
            face.GetComponent<MeshRenderer>().sharedMaterial = blank;
            Destroy(face.GetComponent<Collider>());
            foreach (float angle in new[] { 45f, -45f })
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bar.name = "진입 금지 표시 (X)";
                bar.transform.SetParent(closed.transform, false);
                bar.transform.localPosition = lamp.Centre + lamp.Out.normalized * .009f;
                bar.transform.localRotation = rotation * Quaternion.Euler(0, 0, angle);
                bar.transform.localScale = new Vector3(lamp.Height * .8f, .014f, 1);
                bar.GetComponent<MeshRenderer>().sharedMaterial = Red;
                Destroy(bar.GetComponent<Collider>());
            }
        }
    }
}
