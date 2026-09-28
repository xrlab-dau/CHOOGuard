using UnityEngine;

namespace ChooGuard.App.Fps.Hud
{
    /// <summary>Small procedural sprites so the HUD needs no texture assets: disc, ring, diamond, vignette.</summary>
    public static class HudSprites
    {
        private static Sprite disc, ring, diamond, vignette;

        public static Sprite Disc => disc != null ? disc : disc = Make(64, (x, y) => Smooth(1f - Radius(x, y), 1.5f / 32f));
        public static Sprite Ring => ring != null ? ring : ring = Make(128, (x, y) =>
        {
            float r = Radius(x, y);
            return Smooth(1f - r, 1.5f / 64f) * Smooth(r - .78f, 1.5f / 64f);
        });
        public static Sprite Diamond => diamond != null ? diamond : diamond = Make(64, (x, y) =>
            Smooth(1f - (Mathf.Abs(x) + Mathf.Abs(y)), 2f / 32f));
        public static Sprite Vignette => vignette != null ? vignette : vignette = Make(256, (x, y) =>
            Mathf.SmoothStep(0, 1, Mathf.Clamp01((Radius(x, y) - .45f) / .75f)));

        private static float Radius(float x, float y) => Mathf.Sqrt(x * x + y * y);
        private static float Smooth(float signedDistance, float width) => Mathf.Clamp01(signedDistance / width * .5f + .5f);

        private static Sprite Make(int size, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size * 2 - 1, v = (y + .5f) / size * 2 - 1;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(alpha(u, v)));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
