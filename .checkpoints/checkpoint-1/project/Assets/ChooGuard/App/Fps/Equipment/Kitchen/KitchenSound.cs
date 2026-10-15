using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The two sounds the kitchen equipment makes, synthesised once (deterministically) when first asked for: the hiss of gas
    /// escaping under pressure (band-limited noise) and the leak alarm's beep (a 2.9 kHz tone in bursts; NFTC 206 asks 70 dB
    /// at 1 m and a tone that stands out from other noise). Both loop.
    /// </summary>
    public static class KitchenSound
    {
        private const int Rate = 22050;
        private static AudioClip hiss, beep;

        public static AudioClip Hiss => hiss != null ? hiss : hiss = MakeHiss();

        public static AudioClip Beep => beep != null ? beep : beep = MakeBeep();

        private static AudioClip MakeHiss()
        {
            var samples = new float[Rate * 2];
            var random = new System.Random(20260930);
            float previous = 0, low = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float noise = (float)(random.NextDouble() * 2 - 1);
                // 고역만 남긴다(1차 고역 통과 뒤 저역을 살짝 깎는다): 좁은 틈으로 빠지는 가스의 쉬익 소리.
                float high = noise - previous * .96f;
                previous = noise;
                low += (high - low) * .55f;
                samples[i] = low * .55f;
            }
            var clip = AudioClip.Create("가스 새는 소리", samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip MakeBeep()
        {
            // 0.2 s 울림 · 0.15 s 쉼을 세 번, 뒤에 0.35 s 쉼.
            const float on = .2f, off = .15f, tail = .35f;
            var samples = new float[Mathf.RoundToInt((3 * (on + off) + tail) * Rate)];
            for (int burst = 0; burst < 3; burst++)
            {
                int start = Mathf.RoundToInt(burst * (on + off) * Rate), length = Mathf.RoundToInt(on * Rate);
                for (int i = 0; i < length; i++)
                {
                    // 시작·끝 5 ms 는 서서히(딸깍 소리 방지).
                    float envelope = Mathf.Min(1f, Mathf.Min(i, length - i) / (.005f * Rate));
                    samples[start + i] = Mathf.Sin(2 * Mathf.PI * 2900f * i / Rate) * .7f * envelope;
                }
            }
            var clip = AudioClip.Create("가스누설경보기 경보음", samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
