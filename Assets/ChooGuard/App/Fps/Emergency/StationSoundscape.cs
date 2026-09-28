using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Recorded sound of the station (CC0 recordings under ThirdParty/Audio/Station): ambience beds crossfaded by where
    /// the staff member stands — indoor crowd murmur scaled by how many people are near (it thins as the hall empties),
    /// the Gwangju-station platform bed with distant Korean announcements, the city bed outside — the KTX set's approach
    /// and departure (sources ride the train), its doors opening and closing at each door car, the machinery of every
    /// running escalator, crackle at each burning fire and the staff member's own footsteps. The beds duck under a
    /// public announcement so the voice carries, and one reverb zone riding the listener takes the late reverb of the
    /// space the staff member is in. Pause is handled by <see cref="StationSound"/> (the listener pauses).
    /// </summary>
    public sealed class StationSoundscape : MonoBehaviour
    {
        private const float BedVolume = .55f, FadePerSecond = .6f, CrowdRadius = 25f, DuckGain = .45f, ReverbRate = 2.5f;

        /// <summary>
        /// Late reverb of a space in FMOD SFX reverb terms (levels in millibels): FMOD's Auditorium preset with the decay
        /// cut to 2.8 s for the 2F concourse (a large hard glass-and-steel hall that people damp), Parking Lot for the
        /// canopied platform open at the sides, Room for the furnished KTX car, and nothing outside. Recorded beds already
        /// carry their room, so only point sources, footsteps and the bell feed it.
        /// </summary>
        private readonly struct Acoustics
        {
            public readonly float Room, RoomHF, Decay, HFRatio, Reflections, ReflectionsDelay, Reverb, ReverbDelay;

            public Acoustics(float room, float roomHF, float decay, float hfRatio, float reflections, float reflectionsDelay, float reverb, float reverbDelay)
            {
                Room = room; RoomHF = roomHF; Decay = decay; HFRatio = hfRatio;
                Reflections = reflections; ReflectionsDelay = reflectionsDelay; Reverb = reverb; ReverbDelay = reverbDelay;
            }

            public static readonly Acoustics Concourse = new Acoustics(-1000, -476, 2.8f, .59f, -789, .02f, -289, .03f);
            public static readonly Acoustics Platform = new Acoustics(-1000, 0, 1.65f, 1.5f, -1363, .008f, -1153, .012f);
            public static readonly Acoustics Carriage = new Acoustics(-1000, -454, .4f, .83f, -1646, .002f, 53, .003f);
            public static readonly Acoustics Open = new Acoustics(-10000, -10000, 1f, 1f, -10000, .02f, -10000, .03f);

            public static Acoustics Lerp(in Acoustics a, in Acoustics b, float t) => new Acoustics(
                Mathf.Lerp(a.Room, b.Room, t), Mathf.Lerp(a.RoomHF, b.RoomHF, t), Mathf.Lerp(a.Decay, b.Decay, t), Mathf.Lerp(a.HFRatio, b.HFRatio, t),
                Mathf.Lerp(a.Reflections, b.Reflections, t), Mathf.Lerp(a.ReflectionsDelay, b.ReflectionsDelay, t),
                Mathf.Lerp(a.Reverb, b.Reverb, t), Mathf.Lerp(a.ReverbDelay, b.ReverbDelay, t));

            public void ApplyTo(AudioReverbZone zone)
            {
                zone.room = Mathf.RoundToInt(Room);
                zone.roomHF = Mathf.RoundToInt(RoomHF);
                zone.decayTime = Decay;
                zone.decayHFRatio = HFRatio;
                zone.reflections = Mathf.RoundToInt(Reflections);
                zone.reflectionsDelay = ReflectionsDelay;
                zone.reverb = Mathf.RoundToInt(Reverb);
                zone.reverbDelay = ReverbDelay;
            }
        }

        private EmergencySession session;
        private EmergencyArt art;
        private AudioSource concourse, platform, outdoor, feet, trainMove;
        private readonly List<AudioSource> doors = new List<AudioSource>();
        private readonly Dictionary<Escalator, AudioSource> escalators = new Dictionary<Escalator, AudioSource>();
        private readonly Dictionary<FireHazard, AudioSource> fires = new Dictionary<FireHazard, AudioSource>();
        private readonly List<FireHazard> gone = new List<FireHazard>();
        private readonly System.Random random = new System.Random(20260927);
        private CharacterController body;
        private AudioReverbZone reverb;
        private Acoustics acoustics = Acoustics.Open;
        private float stepDistance, crowd = .6f, nextCrowdCount;
        private TrainService.Phase lastStage;
        private float lastDoors;

        public void Setup(EmergencySession session)
        {
            this.session = session;
            art = session.Art;
            concourse = Bed("맞이방 환경음", art.ConcourseBed);
            platform = Bed("승강장 환경음", art.PlatformBed);
            outdoor = Bed("바깥 환경음", art.OutdoorBed);
            feet = Source("발소리", transform, 0);
            reverb = new GameObject("공간 잔향").AddComponent<AudioReverbZone>();
            reverb.transform.SetParent(transform, false);
            reverb.reverbPreset = AudioReverbPreset.User;
            reverb.minDistance = 50;
            reverb.maxDistance = 60;
            acoustics.ApplyTo(reverb);
            body = session.Player.GetComponent<CharacterController>();
            foreach (var escalator in session.World.Escalators)
            {
                var source = Point("에스컬레이터 소리 · " + escalator.Label, transform, escalator.Middle, 2f, 16f, .5f);
                source.clip = art.Escalator;
                source.loop = true;
                source.time = (float)random.NextDouble() * art.Escalator.length;
                source.Play();
                escalators[escalator] = source;
            }
            var train = session.Train;
            if (train == null) return;
            trainMove = Point("열차 주행 소리", train.Carrier, train.Carrier.position, 10f, 220f, 1f);
            foreach (var car in train.Cars)
            {
                if (car.Leaves == null || car.Leaves.Length == 0) continue;
                doors.Add(Point("출입문 소리 · " + car.Label, train.Carrier, train.World(car.DoorOutside), 1.5f, 25f, .8f));
            }
            lastStage = train.Stage;
            lastDoors = train.DoorsOpen;
        }

        private void Update()
        {
            if (session == null || session.Player == null || session.Player.IsPaused) return;
            var position = session.Player.transform.position;
            string zone = session.World.ZoneId(position);
            bool inTrain = zone == "train";
            bool outdoors = zone == "plaza" || zone == "skyplaza" || zone.Length == 0;
            bool onPlatform = zone == "tracks" || (zone.Length > 1 && zone[0] == 'p' && char.IsDigit(zone[1]));
            bool indoors = !inTrain && !outdoors && !onPlatform;
            if (Time.time >= nextCrowdCount)
            {
                nextCrowdCount = Time.time + 1f;
                int near = 0;
                foreach (var person in session.Crowd.People)
                    if (person != null && (person.transform.position - position).sqrMagnitude < CrowdRadius * CrowdRadius) near++;
                crowd = Mathf.Clamp01(.2f + near / 30f);
            }
            // 안내방송이 나오는 동안 바닥 환경음을 낮춰 음성이 묻히지 않게 한다.
            float duck = session.Sound != null && session.Sound.Speaking ? DuckGain : 1;
            Fade(concourse, indoors ? BedVolume * crowd * duck : 0);
            Fade(platform, (onPlatform ? BedVolume : inTrain ? BedVolume * .35f : 0) * duck);
            Fade(outdoor, outdoors ? BedVolume * .8f * duck : 0);
            reverb.transform.position = position;
            var space = inTrain ? Acoustics.Carriage : onPlatform ? Acoustics.Platform : outdoors ? Acoustics.Open : Acoustics.Concourse;
            acoustics = Acoustics.Lerp(acoustics, space, 1 - Mathf.Exp(-ReverbRate * Time.deltaTime));
            acoustics.ApplyTo(reverb);
            TrainSounds();
            EscalatorSounds();
            FireSounds();
            Footsteps();
        }

        private void TrainSounds()
        {
            var train = session.Train;
            if (train == null) return;
            if (train.Stage != lastStage)
            {
                // 진입음은 정차 직전에 제동이 끝나도록 접근 도중에 시작한다(열차에 붙은 소리라 다가오며 커진다).
                if (train.Stage == TrainService.Phase.Arriving) { trainMove.clip = art.TrainArrive; trainMove.PlayDelayed(Mathf.Max(0, TrainService.ApproachSeconds - art.TrainArrive.length)); }
                else if (train.Stage == TrainService.Phase.Departing) { trainMove.clip = art.TrainDepart; trainMove.Play(); }
                lastStage = train.Stage;
            }
            float open = train.DoorsOpen;
            if (open > lastDoors && lastDoors <= .02f) foreach (var door in doors) door.PlayOneShot(art.DoorOpen);
            else if (open < lastDoors && lastDoors >= .98f) foreach (var door in doors) door.PlayOneShot(art.DoorClose);
            lastDoors = open;
        }

        private void EscalatorSounds()
        {
            foreach (var pair in escalators) Fade(pair.Value, pair.Key.Running ? .5f : 0);
        }

        private void FireSounds()
        {
            foreach (var hazard in HazardRegistry.Active)
            {
                if (!(hazard is FireHazard fire) || fire.Extinguished || fires.ContainsKey(fire)) continue;
                var source = Point("불 소리", transform, fire.Position + Vector3.up * .4f, 1f, 18f, 0);
                source.clip = art.Fire;
                source.loop = true;
                source.time = (float)random.NextDouble() * art.Fire.length;
                source.Play();
                fires[fire] = source;
            }
            gone.Clear();
            foreach (var pair in fires)
            {
                bool burning = !pair.Key.Extinguished && pair.Key.Active;
                Fade(pair.Value, burning ? .35f + .65f * Mathf.Clamp01(pair.Key.Intensity) : 0);
                if (!burning && pair.Value.volume <= .001f) gone.Add(pair.Key);
            }
            foreach (var fire in gone) { Destroy(fires[fire].gameObject); fires.Remove(fire); }
        }

        private void Footsteps()
        {
            if (body == null || !body.enabled || !body.isGrounded || art.Footsteps.Length == 0) { stepDistance = 0; return; }
            var velocity = body.velocity;
            velocity.y = 0;
            float speed = velocity.magnitude;
            if (speed < .3f) { stepDistance = 0; return; }
            stepDistance += speed * Time.deltaTime;
            if (stepDistance < (speed > 3.5f ? 1.3f : .75f)) return;
            stepDistance = 0;
            feet.pitch = .92f + (float)random.NextDouble() * .16f;
            feet.PlayOneShot(art.Footsteps[random.Next(art.Footsteps.Length)], Mathf.Lerp(.25f, .45f, Mathf.InverseLerp(1f, 5f, speed)));
        }

        private AudioSource Bed(string name, AudioClip clip)
        {
            var source = Source(name, transform, 0);
            source.clip = clip;
            source.loop = true;
            source.volume = 0;
            // 녹음 자체에 공간 울림이 있어 역 잔향을 더하지 않고, 음성 수가 모자랄 때도 끊기지 않게 우선한다.
            source.reverbZoneMix = 0;
            source.priority = 16;
            if (clip == null) return source;
            source.time = (float)random.NextDouble() * clip.length;
            source.Play();
            return source;
        }

        private static AudioSource Point(string name, Transform parent, Vector3 position, float near, float far, float volume)
        {
            var source = Source(name, parent, 1);
            source.transform.position = position;
            source.minDistance = near;
            source.maxDistance = far;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 0;
            source.volume = volume;
            return source;
        }

        private static AudioSource Source(string name, Transform parent, float spatial)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial;
            return source;
        }

        private static void Fade(AudioSource source, float target)
        {
            if (source == null) return;
            source.volume = Mathf.MoveTowards(source.volume, target, FadePerSecond * Time.deltaTime);
        }
    }
}
