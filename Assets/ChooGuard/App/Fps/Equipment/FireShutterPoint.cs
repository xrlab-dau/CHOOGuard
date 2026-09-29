using System;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// An automatic fire shutter (자동방화셔터, a <see cref="StationEquipment"/> of kind <see cref="ShutterKind"/>) across an opening of a fire compartment boundary. The
    /// interlocked control (Building Act enforcement rules art. 14 (2) 4) closes it in two stages: a smoke or flame detector lowers it partly so people can still get out, a heat
    /// detector closes it fully; a heat signal first skips stage one. The curtain runs at 7.5 m/min (game tuning within the 2-7.5 m/min the makers give). While the
    /// linked detector signal lasts (<see cref="Triggered"/>) the shutter does not rise: the receiver has to be reset and the reset switch of the control box pressed
    /// (procedure of the makers' manual, research nftc103-compartments-cctv 2.2). A closed or lowered curtain blocks the player (a collider) and the crowd
    /// (a carving nav-mesh obstacle). The size comes from the placement entry (<c>width</c>, <c>height</c> of the clear opening in metres, <c>detectors</c> ids joined by '|').
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class FireShutterPoint : MonoBehaviour, IEquipmentPlaced
    {
        public const string ShutterKind = "fire_shutter";

        /// <summary>Curtain speed in metres per second (7.5 m/min).</summary>
        public const float Speed = .125f;
        /// <summary>Clear height left by the first stage (no source fixes it: the maker's leaflet says 1.0-1.5 m, the fire agency's card news says 600 mm of lowering).</summary>
        public const float GapHeight = 1.3f;

        /// <summary>The model's clear opening: 4.0 m wide, 4.0 m high (FireShutterBox), its roll box 0.3 m above.</summary>
        private const float ModelWidth = 4f, ModelHeight = 4f, ModelCurtainWidth = 4.02f;

        public enum Command { Up, Stop, Down, Reset }

        public StationEquipment Equipment { get; private set; }
        public float Width { get; private set; } = ModelWidth;
        public float Height { get; private set; } = ModelHeight;
        /// <summary>Share of the clear height that is open, 1 fully up, 0 fully down.</summary>
        public float Opening { get; private set; } = 1f;
        /// <summary>Where the curtain is heading.</summary>
        public float Target { get; private set; } = 1f;
        /// <summary>A linked detector's signal is still on: the shutter holds its stage until the receiver is reset.</summary>
        public bool Triggered { get; private set; }
        public string TriggerCause { get; private set; } = "";
        /// <summary>The obstacle sensor of the bottom bar does not work (a fault): the curtain does not stop for a person under it.</summary>
        public bool SensorFailed { get; set; }
        /// <summary>The lowering control is at fault: it lowers with nothing linked to it.</summary>
        public bool ControllerFault { get; private set; }
        /// <summary>Detector placement ids linked to it (both sides of the curtain).</summary>
        public string[] Detectors { get; private set; } = Array.Empty<string>();

        /// <summary>A person was under the curtain when it came down without its sensor (the director injures them).</summary>
        public event Action<FireShutterPoint, GameObject> Caught;
        /// <summary>A person or a control moved the curtain (for the log): who, what.</summary>
        public static event Action<FireShutterPoint, Command, string> Commanded;

        private Transform curtain;
        private BoxCollider blocker;
        private NavMeshObstacle obstacle;
        private float nextSense;
        private readonly Collider[] hits = new Collider[16];
        private readonly System.Collections.Generic.HashSet<GameObject> caught = new System.Collections.Generic.HashSet<GameObject>();

        /// <summary>Clear height under the curtain now.</summary>
        public float ClearHeight => Opening * Height;
        /// <summary>The curtain is low enough that nobody can pass (under 1.95 m clear).</summary>
        public bool Blocking => ClearHeight < 1.95f;
        public bool Moving => !Mathf.Approximately(Opening, Target);

        public void OnPlaced()
        {
            Equipment = GetComponent<StationEquipment>();
            Width = Equipment.Number("width", ModelWidth);
            Height = Equipment.Number("height", ModelHeight);
            var detectors = Equipment.Text("detectors");
            Detectors = detectors.Length == 0 ? Array.Empty<string>() : detectors.Split('|');
            var body = transform.Find("Body");
            if (body != null) body.localScale = new Vector3(Width / ModelWidth, Height / ModelHeight, 1f);
            curtain = transform.Find("Curtain");
            if (curtain != null)
            {
                curtain.localPosition = new Vector3(0, Height, 0);
                blocker = transform.Find("Blocker")?.GetComponent<BoxCollider>();
            }
            obstacle = GetComponentInChildren<NavMeshObstacle>(true);
            if (obstacle != null)
            {
                obstacle.size = new Vector3(Width, 2.6f, .5f);
                obstacle.center = new Vector3(0, 1.3f, 0);
            }
            Apply();
        }

        private void Update()
        {
            if (Moving)
            {
                bool lowering = Target < Opening;
                if (lowering && Time.time >= nextSense) { nextSense = Time.time + .1f; if (Sense()) return; }
                Opening = Mathf.MoveTowards(Opening, Target, Speed / Height * Time.deltaTime);
                Apply();
            }
        }

        /// <summary>
        /// The obstacle sensor: a person under the curtain stops it, unless the sensor has failed; then the curtain comes down on them when its bottom bar reaches
        /// head height (<see cref="Caught"/>). Returns true when the curtain stopped.
        /// </summary>
        private bool Sense()
        {
            float bottom = ClearHeight;
            var centre = transform.position + transform.up * Mathf.Max(.1f, bottom - .5f);
            int count = Physics.OverlapBoxNonAlloc(centre, new Vector3(Width * .5f, .5f, .35f), hits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var person = hits[i].GetComponentInParent<Emergency.Passenger>();
                GameObject who = person != null ? person.gameObject : hits[i].GetComponentInParent<FirstPersonResponder>()?.gameObject;
                if (who == null) continue;
                if (!SensorFailed)
                {
                    Target = Opening;
                    Equipment.State = "장애물 감지 · 정지";
                    return true;
                }
                if (bottom < 1.85f && caught.Add(who)) Caught?.Invoke(this, who);
            }
            return false;
        }

        private void Apply()
        {
            if (curtain != null)
            {
                float down = 1f - Opening;
                curtain.gameObject.SetActive(down > .004f);
                curtain.localScale = new Vector3(Width / ModelCurtainWidth, Mathf.Max(.001f, down * Height / ModelHeight), 1f);
            }
            bool blocking = Blocking;
            if (blocker != null)
            {
                blocker.enabled = blocking;
                float h = Mathf.Max(.05f, (1f - Opening) * Height);
                blocker.size = new Vector3(Width, h, .12f);
                blocker.center = new Vector3(0, Height - h * .5f, 0);
            }
            if (obstacle != null) obstacle.enabled = blocking;
            Equipment.State = StateText();
        }

        private string StateText()
        {
            if (ControllerFault && Opening < .999f) return "오동작 · " + Stage();
            if (Moving) return Target < Opening ? "하강 중" : "상승 중";
            return Stage();
        }

        private string Stage() => Opening >= .999f ? "열림" : Opening <= .001f ? "완전 폐쇄" : Mathf.Abs(ClearHeight - GapHeight) < .05f ? "일부 폐쇄 (1단)" : "중간에 정지";

        /// <summary>Stage one (smoke or flame detector) or stage two (heat detector): lowers from a linked detector's signal and holds until it is reset.</summary>
        public void Trigger(bool heat, string cause)
        {
            Triggered = true;
            TriggerCause = cause;
            float gap = GapHeight / Height;
            Target = heat ? 0f : Mathf.Min(Target, Mathf.Min(gap, Opening));
            caught.Clear();
        }

        /// <summary>The detector signal is gone (the receiver was reset). The curtain stays where it is until a person or the control box raises it.</summary>
        public void ClearTrigger()
        {
            Triggered = false;
            TriggerCause = "";
        }

        /// <summary>The control fails: it lowers by itself to <paramref name="target"/> with nothing linked, and its sensor may be dead.</summary>
        public void Malfunction(float target, bool sensorFailed)
        {
            ControllerFault = true;
            SensorFailed = sensorFailed;
            Target = Mathf.Min(Target, Mathf.Clamp01(target));
            caught.Clear();
        }

        /// <summary>The control is repaired or reset: the fault is gone and the sensor works.</summary>
        public void Repair()
        {
            ControllerFault = false;
            SensorFailed = false;
            Apply();
        }

        /// <summary>A person works the control box (up, stop, down) or its reset switch. Returns why nothing happened, or null.</summary>
        public string Operate(Command command, string by)
        {
            switch (command)
            {
                case Command.Stop:
                    Target = Opening;
                    break;
                case Command.Down:
                    Target = 0f;
                    caught.Clear();
                    break;
                case Command.Up:
                    if (Triggered) return "감지기 신호가 남아 있어 올라가지 않습니다 · 수신기를 복구한 뒤 복구 스위치를 누르세요";
                    if (ControllerFault) return "제어기가 고장 상태입니다 · 복구 스위치를 먼저 누르세요";
                    Target = 1f;
                    break;
                case Command.Reset:
                    if (Triggered) return "감지기 신호가 남아 있어 복구되지 않습니다 · 수신기 복구가 먼저입니다";
                    ControllerFault = false;
                    SensorFailed = false;
                    Target = 1f;
                    break;
            }
            Commanded?.Invoke(this, command, by);
            return null;
        }
    }
}
