using System;
using System.Collections.Generic;
using ChooGuard.App.Fps.Hud;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Public-address lines with a pre-rendered voice (JEV voice-002): the three fixed KTX lines and one generic line
    /// per incident announcement; the feed text carries the exact place. New lines are appended so the voice array of
    /// EmergencyArt keeps its order.
    /// </summary>
    public enum PaLine { TrainArriving, TrainBoarding, TrainDeparting, Fire, SuspiciousItem, Medical, DoorCheck, Earthquake, EscalatorStopped, SafetyEvacuation, GasLeak, PowerOutage, WetFloor, FalseAlarm, TrackSafety, ElevatorCheck }

    /// <summary>
    /// The station fire bell, the chime and voice of public announcements and the staff radio. The bell, chime and radio
    /// tones are synthesised at start; announcement voices are pre-rendered clips (EmergencyArt.Announcements).
    /// District sounders sit on every storey within 25 m of every spot (NFTC 203 2.5.1.3) and a building under 11
    /// storeys rings everywhere at once (2.5.1.2), so the bell is heard alike wherever the staff member is. It rings
    /// from the moment a detector trips until the shift ends (JEV 009). Radio lines get the talk-permit beep (own
    /// transmission) or a squelch burst (incoming) and stay text (JEV voice-002). The station speakers and the radio on
    /// the staff member's body are separate devices, so their queues are separate: a radio beep never waits for or cuts
    /// into an announcement voice. Everything pauses with the game.
    /// </summary>
    public sealed class StationSound : MonoBehaviour
    {
        private const float BellVolume = .3f, ChimeVolume = .55f, RadioVolume = .35f, VoiceVolume = .85f;
        private const int MaxQueued = 2;

        private IncidentDirector incidents;
        private FirstPersonResponder player;
        private RadioFeed radio;
        private AudioSource bell, speaker, handset;
        private AudioClip chime, squelch, talkPermit;
        private AudioClip[] voices;
        private readonly Queue<(AudioClip Clip, float Volume)> announcements = new Queue<(AudioClip, float)>();
        private readonly Queue<AudioClip> beeps = new Queue<AudioClip>();
        private float speakerBusyUntil, handsetBusyUntil;

        /// <summary>True while an announcement (its chime and voice) is sounding; the soundscape ducks the ambience beds under it.</summary>
        public bool Speaking => Time.time < speakerBusyUntil;

        public void Setup(EmergencySession session)
        {
            incidents = session.Incidents;
            player = session.Player;
            radio = session.Hud.Radio;
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            bell = Source("비상벨", BellVolume);
            bell.clip = Clip("비상벨", Bell(rate), rate);
            bell.loop = true;
            speaker = Source("안내방송", 1);
            handset = Source("무전기", 1);
            // 안내방송 음성과 차임에는 공간 울림이 이미 들어 있고 무전기는 몸에 단 소리라 역 잔향을 더하지 않는다.
            speaker.reverbZoneMix = handset.reverbZoneMix = 0;
            speaker.priority = handset.priority = bell.priority = 0;
            chime = Clip("안내방송 차임", Chime(rate), rate);
            squelch = Clip("무전 수신", Squelch(rate), rate);
            talkPermit = Clip("무전 송신", TalkPermit(rate), rate);
            voices = session.Art != null ? session.Art.Announcements : null;
            radio.Pushed += OnRadio;
            player.PauseChanged += OnPause;
            OnPause(player.IsPaused);
        }

        /// <summary>Posts an announcement to the feed (its chime) and speaks the pre-rendered voice right after the chime.</summary>
        public void Announce(PaLine line, string text)
        {
            radio.Push(RadioChannel.Announcement, text);
            int index = (int)line;
            if (voices != null && index < voices.Length && voices[index] != null) announcements.Enqueue((voices[index], VoiceVolume));
        }

        private void OnRadio(RadioChannel channel)
        {
            if (channel == RadioChannel.Announcement) { announcements.Enqueue((chime, ChimeVolume)); return; }
            // 송신과 응답이 한 프레임에 오면 차례로 울리고, 그보다 밀린 무전음은 버린다.
            if (beeps.Count >= MaxQueued) return;
            beeps.Enqueue(channel == RadioChannel.Self ? talkPermit : squelch);
        }

        private void OnPause(bool paused) => AudioListener.pause = paused;

        private void Update()
        {
            if (player == null || player.IsPaused) return;
            bool ring = incidents != null && incidents.AlarmRinging;
            if (ring && !bell.isPlaying) bell.Play();
            else if (!ring && bell.isPlaying) bell.Stop();
            if (announcements.Count > 0 && Time.time >= speakerBusyUntil)
            {
                var (clip, volume) = announcements.Dequeue();
                speaker.PlayOneShot(clip, volume);
                // 차임 끝자락에 음성이 이어지고, 음성은 끝까지 말한 뒤에야 다음 방송이 나온다.
                speakerBusyUntil = Time.time + (clip == chime ? clip.length * .8f : clip.length + .4f);
            }
            if (beeps.Count > 0 && Time.time >= handsetBusyUntil)
            {
                var clip = beeps.Dequeue();
                handset.PlayOneShot(clip, RadioVolume);
                handsetBusyUntil = Time.time + clip.length * .8f;
            }
        }

        private void OnDestroy()
        {
            if (radio != null) radio.Pushed -= OnRadio;
            if (player != null) player.PauseChanged -= OnPause;
            AudioListener.pause = false;
        }

        private AudioSource Source(string name, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = volume;
            return source;
        }

        private static AudioClip Clip(string name, float[] samples, int rate)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Electric fire bell: a hammer at 20 strikes a second on a steel gong. Each gong mode is a two-pole resonator
        /// driven by the strikes. The strike pattern repeats every clip and the resonators run in for three clips
        /// first, so the clip is their steady state and loops without a seam.
        /// </summary>
        private static float[] Bell(int rate)
        {
            const int strikes = 40;
            int period = rate / 20, length = period * strikes;
            var random = new System.Random(203);
            var strength = new double[strikes];
            var offset = new int[strikes];
            for (int i = 0; i < strikes; i++) { strength[i] = .8 + .2 * random.NextDouble(); offset[i] = random.Next(rate / 1000); }
            // 징의 모드: 기본음(1.1 kHz) 대비 주파수비, 세기, 60 dB 감쇠 시간(초). 감쇠가 짧아야 타격마다 소리가 떨린다.
            var modes = new (double Ratio, double Level, double T60)[] { (1, 1, .7), (1.59, .6, .5), (2.14, .45, .35), (2.65, .3, .25), (3.5, .2, .15) };
            var result = new float[length];
            int total = length * 4;
            foreach (var mode in modes)
            {
                double w = 2 * Math.PI * 1100 * mode.Ratio / rate, r = Math.Pow(10, -3 / (mode.T60 * rate));
                double a1 = 2 * r * Math.Cos(w), a2 = -r * r, gain = mode.Level * Math.Sin(w), y1 = 0, y2 = 0;
                for (int n = 0; n < total; n++)
                {
                    int m = n % length, strike = m / period;
                    double y = a1 * y1 + a2 * y2 + (m == strike * period + offset[strike] ? gain * strength[strike] : 0);
                    y2 = y1;
                    y1 = y;
                    if (n >= total - length) result[m] += (float)y;
                }
            }
            // 망치가 징을 칠 때의 딸깍 소리(되감기 이음매를 넘으면 앞으로 감는다).
            var click = new float[length];
            for (int s = 0; s < strikes; s++)
                for (int k = 0, at = s * period + offset[s]; k < rate / 250; k++)
                    click[(at + k) % length] += (float)((random.NextDouble() * 2 - 1) * strength[s] * Math.Exp(-k / (rate * .0012)));
            Normalize(result, 1);
            Normalize(click, .5f);
            for (int n = 0; n < length; n++) result[n] = (float)(Math.Tanh(1.8 * (result[n] + click[n])) / Math.Tanh(1.8));
            Normalize(result, .9f);
            return result;
        }

        /// <summary>
        /// Four ascending notes (도·미·솔·도) before a public announcement, vibraphone-like (fundamental and a fading
        /// fourth harmonic), with three early reflections standing in for the concourse.
        /// </summary>
        private static float[] Chime(int rate)
        {
            double[] notes = { 523.25, 659.25, 783.99, 1046.5 };
            const double step = .36, ring = 1.8;
            int length = (int)((step * (notes.Length - 1) + ring + .3) * rate);
            var dry = new float[length];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(k * step * rate);
                for (int n = 0; start + n < length && n < ring * rate; n++)
                {
                    double t = (double)n / rate;
                    double envelope = Math.Min(1, t / .005) * Math.Min(1, (ring - t) / .2) * Math.Exp(-t * 2.8);
                    dry[start + n] += (float)(envelope * (Math.Sin(2 * Math.PI * notes[k] * t) + .3 * Math.Exp(-t * 8) * Math.Sin(2 * Math.PI * notes[k] * 4 * t)));
                }
            }
            var wet = new float[length];
            foreach (var (delay, gain) in new[] { (0.0, 1f), (.083, .35f), (.157, .22f), (.241, .12f) })
                for (int n = (int)(delay * rate), d = n; n < length; n++) wet[n] += gain * dry[n - d];
            Normalize(wet, .8f);
            return wet;
        }

        /// <summary>Incoming radio line: a short burst of static, band-limited to a voice channel, as the squelch opens.</summary>
        private static float[] Squelch(int rate)
        {
            const double seconds = .14;
            var samples = new float[(int)(seconds * rate)];
            var random = new System.Random(202);
            double lowCoefficient = Math.Exp(-2 * Math.PI * 3000 / rate), highCoefficient = Math.Exp(-2 * Math.PI * 300 / rate);
            double low = 0, lower = 0, high = 0, previous = 0;
            for (int n = 0; n < samples.Length; n++)
            {
                double t = (double)n / rate;
                low = (1 - lowCoefficient) * (random.NextDouble() * 2 - 1) + lowCoefficient * low;
                lower = (1 - lowCoefficient) * low + lowCoefficient * lower;
                high = highCoefficient * (high + lower - previous);
                previous = lower;
                samples[n] = (float)(high * Math.Min(1, t / .004) * Math.Min(1, (seconds - t) / .05));
            }
            Normalize(samples, .7f);
            return samples;
        }

        /// <summary>Own transmission: the short talk-permit beep of a digital radio when the key is pressed.</summary>
        private static float[] TalkPermit(int rate)
        {
            const double seconds = .09;
            var samples = new float[(int)(seconds * rate)];
            for (int n = 0; n < samples.Length; n++)
            {
                double t = (double)n / rate;
                double envelope = Math.Min(1, t / .004) * Math.Min(1, (seconds - t) / .01);
                samples[n] = (float)(envelope * (.7 * Math.Sin(2 * Math.PI * 1760 * t) + .3 * Math.Sin(2 * Math.PI * 880 * t)));
            }
            Normalize(samples, .6f);
            return samples;
        }

        private static void Normalize(float[] samples, float peak)
        {
            float max = 0;
            foreach (var sample in samples) max = Math.Max(max, Math.Abs(sample));
            if (max <= 0) return;
            float scale = peak / max;
            for (int i = 0; i < samples.Length; i++) samples[i] *= scale;
        }
    }
}
