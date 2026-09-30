using System.Collections.Generic;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.App.Fps.Facilities
{
    /// <summary>
    /// One elevator of the twin: a car serving landings whose kit landing doors each have an identical cabin behind them
    /// (grouped by the kit's car id). The car stands at one landing at a time. Between landings every door is shut and the
    /// riders — the staff member and passengers — are carried from one cabin to the other at the same place inside
    /// (JEV 010 elevator_player_model). Calls come from the landing buttons (or E on the landing doors) and the car panel.
    /// Doors hold open 5 s at a stop and reopen for anyone in the doorway (되열림장치); arrival sounds a chime at the landing
    /// and in the car. Rules follow 교통약자의 이동편의 증진법 시행규칙 [별표 1] 2.바 (15인승 기준, 스위치 높이 0.8~1.2 m,
    /// 도착 음향신호, 되열림장치). Speed is a common 60 m/min; there is no fire recall (JEV 010) — the signs forbid use in a fire.
    /// </summary>
    public sealed class ElevatorCar : MonoBehaviour
    {
        public const float Speed = 1f, DoorSeconds = 2f, DwellSeconds = 5f, StartStopSeconds = 2f;
        public const int Capacity = 15;

        public enum State { Idle, Opening, Open, Closing, Moving }

        public sealed class Landing
        {
            public StationDoor Door;
            public string Floor;
            public float Y;
            public bool HallCall, CarCall;
            public TextMesh Inside;
            public AudioSource Sound;
        }

        /// <summary>A passenger standing in the car: <see cref="Spot"/> is cabin-local; <see cref="Cabin"/> is the landing whose cabin holds them.</summary>
        public sealed class Rider
        {
            public PersonBody Body;
            public Vector3 Spot;
            public int Cabin, To;
            public bool Walking;
        }

        public static readonly List<ElevatorCar> All = new List<ElevatorCar>();
        private static AudioClip chime, hum;

        public string Id { get; private set; }
        public readonly List<Landing> Landings = new List<Landing>();
        public readonly List<Rider> Riders = new List<Rider>();
        public int At { get; private set; }
        public State Now { get; private set; }
        public bool Running { get; private set; } = true;
        public string Label => "엘리베이터 (" + string.Join("·", Landings.ConvertAll(l => l.Floor + "층")) + ")";

        private int to = -1;
        private float stateStart, moveSeconds;
        private bool transferred, closeSoon;
        private FirstPersonResponder player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        /// <summary>
        /// Builds the cars of <paramref name="scene"/> from landing doors not yet served: landing doors with a cabin and the same
        /// car id form one car. Every elevator landing calls this on Start; the first one assembles the lot.
        /// </summary>
        public static void Assemble(Scene scene)
        {
            if (!scene.IsValid()) return;
            var groups = new Dictionary<string, List<StationDoor>>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var door in root.GetComponentsInChildren<StationDoor>(true))
                {
                    if (door.Kind != StationDoor.DoorKind.Elevator || door.Car != null || door.Cabin == null || string.IsNullOrEmpty(door.CarId) || !door.Usable) continue;
                    if (!groups.TryGetValue(door.CarId, out var list)) groups[door.CarId] = list = new List<StationDoor>();
                    list.Add(door);
                }
            foreach (var pair in groups)
            {
                if (pair.Value.Count < 2) continue;
                var go = new GameObject("엘리베이터 운행 · " + pair.Key);
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<ElevatorCar>().Setup(pair.Key, pair.Value);
            }
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);
        private void OnDestroy() { foreach (var landing in Landings) if (landing.Door != null) landing.Door.Car = null; }

        private void Setup(string id, List<StationDoor> doors)
        {
            Id = id;
            doors.Sort((a, b) => a.transform.TransformPoint(a.Centre).y.CompareTo(b.transform.TransformPoint(b.Centre).y));
            foreach (var door in doors)
            {
                var landing = new Landing { Door = door, Floor = door.Floor, Y = door.transform.TransformPoint(door.Centre).y };
                door.Car = this;
                Landings.Add(landing);
                int index = Landings.Count - 1;
                if (door.CallPanel != null) ElevatorButton.Add(door.CallPanel, this, index, ElevatorButton.Kinds.Hall, "", new Vector3(.16f, .36f, .06f));
                foreach (Transform child in door.Cabin)
                {
                    if (!child.name.StartsWith("버튼 · ", System.StringComparison.Ordinal)) continue;
                    string what = child.name.Substring(5);
                    if (what.StartsWith("층 ", System.StringComparison.Ordinal)) ElevatorButton.Add(child, this, -1, ElevatorButton.Kinds.Floor, what.Substring(2), new Vector3(.07f, .07f, .05f));
                    else if (what == "열림") ElevatorButton.Add(child, this, -1, ElevatorButton.Kinds.Open, "", new Vector3(.07f, .07f, .05f));
                    else if (what == "닫힘") ElevatorButton.Add(child, this, -1, ElevatorButton.Kinds.Close, "", new Vector3(.07f, .07f, .05f));
                    else if (what == "비상통화") ElevatorButton.Add(child, this, -1, ElevatorButton.Kinds.Alarm, "", new Vector3(.07f, .07f, .05f));
                }
                var indicator = door.Cabin.Find("표시기");
                landing.Inside = indicator != null && indicator.TryGetComponent(out TextMesh inside) ? inside : null;
                var light = new GameObject("카 조명").AddComponent<Light>();
                light.transform.SetParent(door.Cabin, false);
                light.transform.localPosition = new Vector3(0, door.CabinSize.y - .15f, door.CabinSize.z * .5f);
                light.type = LightType.Point;
                light.range = 3f;
                light.intensity = .7f;
                light.color = new Color(1f, .96f, .9f);
                light.shadows = LightShadows.None;
                landing.Sound = door.Cabin.gameObject.AddComponent<AudioSource>();
                landing.Sound.playOnAwake = false;
                landing.Sound.spatialBlend = 1;
                landing.Sound.rolloffMode = AudioRolloffMode.Linear;
                landing.Sound.minDistance = 1.5f;
                landing.Sound.maxDistance = 14;
                landing.Sound.volume = .45f;
                landing.Sound.clip = Hum();
                landing.Sound.loop = true;
            }
            At = 0;
            Now = State.Idle;
            stateStart = Time.time;
            Indicate(Landings[At].Floor);
        }

        // ── 조회 ─────────────────────────────────────────────────────────────

        public int IndexOf(StationDoor door) => Landings.FindIndex(l => l.Door == door);

        /// <summary>Landing of the car whose door stands nearest <paramref name="world"/> on the same storey, or -1 beyond <paramref name="range"/>.</summary>
        public int LandingNear(Vector3 world, float range = 2.5f)
        {
            int best = -1;
            float bestDistance = range;
            for (int i = 0; i < Landings.Count; i++)
            {
                var door = Landings[i].Door;
                var centre = door.transform.TransformPoint(door.Centre);
                if (Mathf.Abs(centre.y - world.y) > 1.6f) continue;
                centre.y = world.y;
                float d = Vector3.Distance(centre, world);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        public bool DoorsOpenAt(int landing) => At == landing && Now == State.Open;

        public bool Full => Riders.Count + (PlayerInside(out _) ? 1 : 0) >= Capacity;

        /// <summary>Whether <paramref name="world"/> is inside the cabin behind landing <paramref name="landing"/>.</summary>
        public bool InCabin(int landing, Vector3 world)
        {
            var door = Landings[landing].Door;
            var l = door.Cabin.InverseTransformPoint(world);
            var size = door.CabinSize;
            return Mathf.Abs(l.x) < size.x * .5f + .05f && l.z > -.2f && l.z < size.z + .05f && l.y > -.4f && l.y < size.y;
        }

        private bool PlayerInside(out int cabin)
        {
            cabin = -1;
            var p = Player;
            if (p == null) return false;
            for (int i = 0; i < Landings.Count; i++) if (InCabin(i, p.transform.position)) { cabin = i; return true; }
            return false;
        }

        private FirstPersonResponder Player
        {
            get
            {
                if (player == null || !player.isActiveAndEnabled) player = FindFirstObjectByType<FirstPersonResponder>();
                return player;
            }
        }

        /// <summary>World point on the door line of <paramref name="landing"/>; <paramref name="inward"/> metres into the car (negative: out on the landing).</summary>
        public Vector3 Threshold(int landing, float inward = .2f) => Landings[landing].Door.World(new Vector3(0, 0, -inward));

        public Vector3 SpotWorld(Rider rider) => Landings[rider.Cabin].Door.Cabin.TransformPoint(rider.Spot);

        /// <summary>Direction a rider faces: toward the doors.</summary>
        public Vector3 Facing(Rider rider) => -Landings[rider.Cabin].Door.Cabin.forward;

        // ── 호출 ─────────────────────────────────────────────────────────────

        public string LandingPrompt(StationDoor door)
        {
            int i = IndexOf(door);
            if (i < 0) return "";
            return DoorsOpenAt(i) || (At == i && Now == State.Opening) ? "문 열림 · 탑승" : Landings[i].HallCall ? "호출됨 · 기다리기" : "호출 버튼 누르기";
        }

        public bool CanCall(StationDoor door, out string reason)
        {
            reason = null;
            if (!Running) { reason = "운행 정지 (지진·정전·고장)"; return false; }
            int i = IndexOf(door);
            if (i < 0) { reason = ""; return false; }
            if (DoorsOpenAt(i)) { reason = "문 열림 · 들어가서 층 버튼을 누르세요"; return false; }
            return true;
        }

        public bool Call(StationDoor door, out string feedback)
        {
            feedback = null;
            int i = IndexOf(door);
            if (i < 0 || !Running) return false;
            Call(i);
            feedback = Landings[i].Floor + "층 엘리베이터 호출";
            return true;
        }

        public void Call(int landing)
        {
            if (landing < 0 || landing >= Landings.Count) return;
            if (At == landing && (Now == State.Open || Now == State.Opening)) return;
            if (At == landing && Now == State.Closing) { Begin(State.Opening); return; }
            Landings[landing].HallCall = true;
        }

        public void Press(int landing)
        {
            if (landing < 0 || landing >= Landings.Count) return;
            if (At == landing && Now != State.Moving) { if (Now == State.Closing || Now == State.Idle) Begin(State.Opening); return; }
            Landings[landing].CarCall = true;
            if (Now == State.Open) closeSoon = true;
        }

        public int FloorIndex(string floor) => Landings.FindIndex(l => l.Floor == floor);

        public void OpenButton()
        {
            if (Now == State.Closing || Now == State.Idle) Begin(State.Opening);
            else if (Now == State.Open) stateStart = Time.time;
        }

        public void CloseButton()
        {
            if (Now == State.Open) closeSoon = true;
        }

        /// <summary>Earthquake, power loss or a fault: the car stops where it is and takes no calls; a moving car stays between floors.</summary>
        public void Stop() => Running = false;

        /// <summary>Back in service: a car stopped between floors carries on to its floor and opens there.</summary>
        public void Resume() => Running = true;

        /// <summary>A passenger boards at <paramref name="from"/> for <paramref name="to"/>: a standing place near the back, or null when full.</summary>
        public Rider Board(PersonBody body, int from, int to)
        {
            if (Full) return null;
            var size = Landings[from].Door.CabinSize;
            var taken = new List<Vector3>();
            foreach (var r in Riders) if (r.Cabin == from) taken.Add(r.Spot);
            if (PlayerInside(out int cabin) && cabin == from) taken.Add(Landings[from].Door.Cabin.InverseTransformPoint(Player.transform.position));
            Vector3 best = new Vector3(0, 0, size.z * .6f);
            float bestScore = float.NegativeInfinity;
            for (int ix = -1; ix <= 1; ix++)
                for (int iz = 0; iz < 3; iz++)
                {
                    var spot = new Vector3(ix * (size.x * .5f - .32f), 0, size.z - .3f - iz * (size.z - .7f) * .5f);
                    float clear = float.PositiveInfinity;
                    foreach (var t in taken) clear = Mathf.Min(clear, Vector2.Distance(new Vector2(spot.x, spot.z), new Vector2(t.x, t.z)));
                    float score = Mathf.Min(clear, 1.2f) * 2 + spot.z + Random.value * .2f;
                    if (score > bestScore) { bestScore = score; best = spot; }
                }
            var rider = new Rider { Body = body, Spot = best, Cabin = from, To = to, Walking = true };
            Riders.Add(rider);
            Press(to);
            return rider;
        }

        public void Alight(Rider rider) => Riders.Remove(rider);

        // ── 운행 ─────────────────────────────────────────────────────────────

        private void Begin(State state)
        {
            Now = state;
            stateStart = Time.time;
            if (state == State.Open) closeSoon = false;
        }

        private int NextCall()
        {
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < Landings.Count; i++)
            {
                if (!Landings[i].HallCall && !Landings[i].CarCall) continue;
                float d = Mathf.Abs(Landings[i].Y - Landings[At].Y);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        private bool Blocked()
        {
            var door = Landings[At].Door;
            var p = Player;
            if (p != null && door.InDoorway(p.transform.position, .55f)) return true;
            // 타는 사람이 자리에 설 때까지, 내리는 사람이 다 나갈 때까지 문을 닫지 않는다.
            foreach (var r in Riders) if (r.Walking && r.Body != null && r.Cabin == At) return true;
            var all = PersonBody.All;
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].Visible && door.InDoorway(all[i].transform.position, .5f)) return true;
            return false;
        }

        private void Update()
        {
            if (Landings.Count < 2 || Time.deltaTime <= 0) return;
            Riders.RemoveAll(r => r.Body == null);
            var here = Landings[At];
            switch (Now)
            {
                case State.Idle:
                    Drive(0);
                    if (!Running) break;
                    if (here.HallCall || here.CarCall) { here.HallCall = here.CarCall = false; Begin(State.Opening); break; }
                    int next = NextCall();
                    if (next >= 0) Depart(next);
                    break;
                case State.Opening:
                    Drive(1);
                    here.HallCall = here.CarCall = false;
                    if (here.Door.Open >= .999f) Begin(State.Open);
                    break;
                case State.Open:
                    Drive(1);
                    here.HallCall = here.CarCall = false;
                    if (Blocked() || !Running) stateStart = Mathf.Max(stateStart, Time.time - (closeSoon ? 0 : DwellSeconds - 1.5f));
                    if (Time.time - stateStart >= (closeSoon ? 1f : DwellSeconds)) Begin(State.Closing);
                    break;
                case State.Closing:
                    if (Blocked() || !Running) { Begin(State.Opening); break; }
                    Drive(0);
                    if (here.Door.Open <= .001f)
                    {
                        int call = NextCall();
                        if (call >= 0) Depart(call);
                        else Begin(State.Idle);
                    }
                    break;
                case State.Moving:
                    Drive(0);
                    if (!Running) { stateStart += Time.deltaTime; break; }
                    float t = Time.time - stateStart;
                    if (!transferred && t >= moveSeconds * .5f) { Transfer(At, to); transferred = true; Indicate(Landings[to].Floor); }
                    if (t >= moveSeconds)
                    {
                        foreach (var l in Landings) if (l.Sound.isPlaying && l.Sound.clip == hum) l.Sound.Stop();
                        At = to;
                        to = -1;
                        Landings[At].HallCall = Landings[At].CarCall = false;
                        Arrive(At);
                        Begin(State.Opening);
                    }
                    break;
            }
        }

        private void Drive(float target)
        {
            for (int i = 0; i < Landings.Count; i++) Landings[i].Door.DriveTarget = i == At && Now != State.Moving ? target : 0;
        }

        private void Depart(int landing)
        {
            if (landing == At) { Begin(State.Opening); return; }
            to = landing;
            transferred = false;
            moveSeconds = Mathf.Abs(Landings[landing].Y - Landings[At].Y) / Speed + StartStopSeconds;
            Begin(State.Moving);
            foreach (var l in Landings) { l.Sound.clip = Hum(); l.Sound.loop = true; l.Sound.Play(); }
        }

        /// <summary>Moves everyone inside the cabin behind <paramref name="from"/> to the same place in the cabin behind <paramref name="landing"/>.</summary>
        private void Transfer(int from, int landing)
        {
            var a = Landings[from].Door.Cabin;
            var b = Landings[landing].Door.Cabin;
            var p = Player;
            if (p != null && InCabin(from, p.transform.position))
            {
                var position = b.TransformPoint(a.InverseTransformPoint(p.transform.position));
                float yaw = p.YawDegrees + (b.eulerAngles.y - a.eulerAngles.y);
                p.RestorePhysicalPose(position, yaw, p.PitchDegrees);
            }
            foreach (var r in Riders)
            {
                if (r.Cabin != from) continue;
                r.Cabin = landing;
                if (r.Body != null) r.Body.transform.position = SpotWorld(r);
            }
        }

        private void Arrive(int landing)
        {
            Indicate(Landings[landing].Floor);
            var sound = Landings[landing].Sound;
            sound.Stop();
            sound.loop = false;
            sound.clip = null;
            sound.PlayOneShot(Chime());
            // 승강장 쪽에서도 들리도록 문 앞에서 한 번 더(도착 음향신호).
            AudioSource.PlayClipAtPoint(Chime(), Threshold(landing, -.6f) + Vector3.up * 2.2f, .5f);
        }

        private void Indicate(string floor)
        {
            foreach (var l in Landings)
            {
                if (l.Door.Lantern != null) l.Door.Lantern.text = floor;
                if (l.Inside != null) l.Inside.text = floor;
            }
        }

        // ── 소리 ─────────────────────────────────────────────────────────────

        /// <summary>Arrival chime: two soft bell tones (generated once).</summary>
        private static AudioClip Chime()
        {
            if (chime != null) return chime;
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000, length = (int)(rate * 1.4f);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate, v = 0;
                foreach (var (at, f) in new[] { (0f, 1318.5f), (.42f, 1046.5f) })
                {
                    float s = t - at;
                    if (s < 0) continue;
                    float env = Mathf.Min(1, s * 200) * Mathf.Exp(-s * 3.2f);
                    v += (Mathf.Sin(2 * Mathf.PI * f * s) + .3f * Mathf.Sin(2 * Mathf.PI * f * 2.01f * s)) * env;
                }
                data[i] = v * .28f;
            }
            chime = AudioClip.Create("엘리베이터 도착음", length, 1, rate, false);
            chime.SetData(data, 0);
            return chime;
        }

        /// <summary>Low hum of the traction machine and air in the car while it runs (seamless loop, generated once).</summary>
        private static AudioClip Hum()
        {
            if (hum != null) return hum;
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000, length = rate * 2;
            var data = new float[length];
            var random = new System.Random(11);
            float noise = 0;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                noise = noise * .985f + (float)(random.NextDouble() * 2 - 1) * .015f;
                data[i] = (Mathf.Sin(2 * Mathf.PI * 55 * t) * .5f + Mathf.Sin(2 * Mathf.PI * 110 * t) * .2f) * .12f + noise * 1.6f;
            }
            hum = AudioClip.Create("엘리베이터 운행음", length, 1, rate, false);
            hum.SetData(data, 0);
            return hum;
        }
    }
}
