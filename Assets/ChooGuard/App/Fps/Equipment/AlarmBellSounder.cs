using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The station's district sounders as sound: the fire bell rings from the real bells on the walls (<see cref="BellKind"/>, placed so that every spot of a floor lies within 25 m of one,
    /// NFTC 203 2.5.1.3), not from the sky. Every bell of a station rings at once, but the audio engine cannot mix a hundred voices: this keeps a few sources (<see cref="Voices"/>) on
    /// the bells nearest to the listener and moves them as the listener walks, so what the staff member hears is always the bells around them, louder and more directional near one
    /// and a chorus from afar. A bell that stays among the nearest keeps its voice (no restart, no click); a new one starts at a random point of the clip so neighbouring bells do not
    /// beat in step. The rolloff is a curve, not the physical inverse distance: a 90 dB bell (2.5.1.4) is meant to be heard across the concourse.
    /// </summary>
    public sealed class AlarmBellSounder : MonoBehaviour
    {
        public const string BellKind = "alarm_bell";

        /// <summary>Bells heard at once, the audio voice limit of the alarm.</summary>
        public const int Voices = 6;
        /// <summary>Farthest bell heard, metres, and seconds between two looks at which bells are the nearest.</summary>
        public const float Audible = 70f, Interval = .25f;

        private readonly AudioSource[] sources = new AudioSource[Voices];
        private readonly StationEquipment[] heard = new StationEquipment[Voices];
        private readonly List<StationEquipment> near = new List<StationEquipment>();
        private Transform listener;
        private float nextLook;

        /// <summary>The bells the voices are on now (nearest first is not guaranteed), for the test and the log.</summary>
        public IReadOnlyList<StationEquipment> Heard => heard;

        public bool Ringing { get; private set; }

        /// <summary>Makes the voices: all loop the same bell clip; <paramref name="volume"/> is one voice's volume; <paramref name="ear"/> is the listener (the staff member's head).</summary>
        public void Setup(AudioClip bell, float volume, Transform ear)
        {
            listener = ear;
            var rolloff = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(3f / Audible, 1f), new Keyframe(12f / Audible, .5f), new Keyframe(25f / Audible, .25f), new Keyframe(45f / Audible, .08f), new Keyframe(1f, 0f));
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("경종 " + (i + 1));
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.clip = bell;
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Custom;
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloff);
                source.minDistance = 1f;
                source.maxDistance = Audible;
                source.dopplerLevel = 0f;
                source.spread = 40f;
                source.volume = volume;
                source.priority = 0;
                sources[i] = source;
            }
        }

        /// <summary>The alarm starts or stops; stopping silences every voice.</summary>
        public void Ring(bool on)
        {
            if (on == Ringing) return;
            Ringing = on;
            nextLook = 0f;
            if (on) return;
            for (int i = 0; i < Voices; i++)
            {
                sources[i].Stop();
                heard[i] = null;
            }
        }

        private void Update()
        {
            if (!Ringing || listener == null || Time.time < nextLook) return;
            nextLook = Time.time + Interval;
            near.Clear();
            EquipmentRegistry.Within(BellKind, listener.position, Audible, near);
            var ear = listener.position;
            near.Sort((a, b) => (a.transform.position - ear).sqrMagnitude.CompareTo((b.transform.position - ear).sqrMagnitude));
            int wanted = Mathf.Min(Voices, near.Count);
            // 여전히 가까운 종은 목소리를 그대로 두고, 멀어진 종의 목소리를 새로 가까워진 종에 넘긴다.
            for (int i = 0; i < Voices; i++)
            {
                if (heard[i] == null) continue;
                int rank = near.IndexOf(heard[i]);
                if (rank < 0 || rank >= wanted) { sources[i].Stop(); heard[i] = null; }
            }
            for (int rank = 0; rank < wanted; rank++)
            {
                if (System.Array.IndexOf(heard, near[rank]) >= 0) continue;
                int free = System.Array.IndexOf(heard, null);
                if (free < 0) break;
                heard[free] = near[rank];
                var source = sources[free];
                source.transform.position = near[rank].transform.position;
                source.time = Offset(near[rank].Id) * source.clip.length;
                source.Play();
            }
        }

        /// <summary>A start point in 0..1 of the clip that is the same for the same bell every time (the world's random stream is not spent on sound).</summary>
        private static float Offset(string id)
        {
            uint hash = 2166136261u;
            foreach (char c in id) hash = (hash ^ c) * 16777619u;
            return (hash % 1000u) / 1000f;
        }
    }
}
