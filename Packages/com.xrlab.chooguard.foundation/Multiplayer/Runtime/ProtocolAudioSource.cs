using System;
using LiveKit;

namespace ChooGuard.Foundation.Multiplayer
{
    // Explicit --cg-voice-fixture only: exercises the SDK media path without touching a microphone.
    public sealed class ProtocolAudioSource : RtcAudioSource
    {
        public override event Action<float[], int, int> AudioRead;
        private double phase;
        public ProtocolAudioSource() : base(RtcAudioSourceType.AudioSourceCustom, 48000, 1) { }
        public void Emit20Milliseconds()
        {
            var pcm = new float[960];
            for (var i = 0; i < pcm.Length; i++)
            { pcm[i] = (float)(.1 * Math.Sin(phase)); phase += 2 * Math.PI * 440 / 48000; }
            AudioRead?.Invoke(pcm, 1, 48000);
        }
    }
}
