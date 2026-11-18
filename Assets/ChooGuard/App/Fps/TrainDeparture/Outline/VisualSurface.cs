// Source-material connection adapted from project's KtxOpaqueTexelDepth.shader.
// CPU UV interpolation connects Godot's barycentric result to Unity Texture2D.GetPixelBilinear.
// StationBoxMap/constant-alpha paths reused from exact project shader snapshots; URP17.3
// SurfaceInput.Alpha branches are Unity Companion Licensed, see ThirdParty and native-gap report.
using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChooGuard.App.Fps.TrainDeparture
{
    [Serializable] public sealed class VisualAlphaCache
    {
        public Texture2D Source;
        [Tooltip("Readable RGBA copy preserving source alpha, dimensions, filtering and wrapping.")]
        public Texture2D Readable;
        // The Editor augmenter uses the SAME production material semantics, including custom shader branches.
        public static Texture2D MissingSource(Material material, VisualAlphaCache[] caches)
        {
            var surface = VisualSurface.Capture(material, caches);
            return !surface.Supported && surface.Diagnostic != null && surface.Diagnostic.StartsWith("Readable alpha cache required:", StringComparison.Ordinal)
                ? surface.Texture as Texture2D : null;
        }
    }
    internal sealed class VisualSurface
    {
        public Material Source;
        public Texture Texture;
        public Texture2D CpuTexture;
        public Vector2 Scale = Vector2.one, Offset;
        public float Alpha = 1, Cutoff;
        public int Cull = 2;
        public bool Supported, NeedsAlpha, BoxMap;
        public bool SampleTextureAlpha = true;
        public string Diagnostic;
        public static VisualSurface Capture(Material m, VisualAlphaCache[] caches)
        {
            var s = new VisualSurface { Source = m };
            if (m == null) { s.Diagnostic = "Missing material"; return s; }
            var shader = m.shader != null ? m.shader.name : "";
            if (shader != "Universal Render Pipeline/Lit" && shader != "Universal Render Pipeline/Unlit" &&
                shader != "Universal Render Pipeline/Simple Lit" && shader != "Universal Render Pipeline/Baked Lit" &&
                shader != "ChooGuard/StationLit" && shader != "CHOOGuard/MAIN1F/PhotoPublicTopMaskedLitV2" &&
                shader != "ChooGuard/R29PhysicalSourceVertexInk" && shader != "ChooGuard/StationStoneTileWorld")
            { s.Diagnostic = "Unsupported shader: " + shader; return s; }
            s.Cull = m.HasProperty("_Cull") ? (int)m.GetFloat("_Cull") : (int)CullMode.Back;
            // Both exact custom StoneSurface implementations set surface.alpha=1 and never clip/displace.
            if(shader=="ChooGuard/R29PhysicalSourceVertexInk" || shader=="ChooGuard/StationStoneTileWorld")
            {s.Alpha=1;s.NeedsAlpha=false;s.Supported=true;return s;}
            s.BoxMap=(shader=="ChooGuard/StationLit" || shader=="CHOOGuard/MAIN1F/PhotoPublicTopMaskedLitV2") && m.IsKeywordEnabled("_BOXMAP");
            // URP SurfaceInput.Alpha: albedo alpha is smoothness for these keyword branches.
            s.SampleTextureAlpha=!m.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A") && !m.IsKeywordEnabled("_GLOSSINESS_FROM_BASE_ALPHA");
            bool clip = m.IsKeywordEnabled("_ALPHATEST_ON");
            bool transparent = m.HasProperty("_Surface") && m.GetFloat("_Surface") > .5f;
            s.NeedsAlpha = clip || transparent;
            if(s.NeedsAlpha && m.IsKeywordEnabled("_PARALLAXMAP")) {s.Diagnostic="Alpha with per-pixel parallax needs separate resolver";return s;}
            // Transparent coverage means visible nonzero alpha; this is not a physical opacity threshold.
            s.Cutoff = clip && m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 1f / 255f;
            s.Alpha = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").a : 1;
            string property = m.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            if (m.HasProperty(property))
            {
                s.Texture = m.GetTexture(property); s.Scale = m.GetTextureScale(property); s.Offset = m.GetTextureOffset(property);
            }
            if (s.NeedsAlpha && s.SampleTextureAlpha && s.Texture != null)
            {
                var tex = s.Texture as Texture2D;
                if (tex != null && tex.isReadable) s.CpuTexture = tex;
                else if (caches != null) foreach (var c in caches)
                    if (c != null && c.Source == s.Texture && c.Readable != null && c.Readable.isReadable &&
                        c.Readable.width == c.Source.width && c.Readable.height == c.Source.height &&
                        c.Readable.wrapModeU == c.Source.wrapModeU && c.Readable.wrapModeV == c.Source.wrapModeV && c.Readable.filterMode == c.Source.filterMode)
                    { s.CpuTexture = c.Readable; break; }
                if (s.CpuTexture == null) { s.Diagnostic = "Readable alpha cache required: " + s.Texture.name; return s; }
                // Readability alone is insufficient for every compressed format. Probe once, never throw each frame.
                try { s.CpuTexture.GetPixelBilinear(.5f, .5f); }
                catch (UnityException) { s.Diagnostic = "Unsupported CPU alpha format: " + s.Texture.name; return s; }
            }
            s.Supported = true; return s;
        }
        public bool Accept(Vector2 uv)
        {
            if (!Supported) return false;
            if (!NeedsAlpha) return true; // Opaque shaders ignore the texture's alpha channel.
            uv = Vector2.Scale(uv, Scale) + Offset;
            try { return (CpuTexture == null ? 1 : CpuTexture.GetPixelBilinear(uv.x, uv.y).a) * Alpha >= Cutoff; }
            catch (UnityException) { Supported = false; Diagnostic = "CPU alpha became unreadable; Refresh required"; return false; }
        }
        // Exact UV projection port from StationLitPasses.hlsl::StationBoxMap, lines 15-27.
        // Evaluate at EACH vertex then interpolate, as the source vertex shader does.
        public static Vector2 BoxUv(Vector3 p, Vector3 n)
        {
            Vector3 a=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));
            Vector3 t,b;
            if(a.y>=a.x && a.y>=a.z){t=new Vector3(1,0,0);b=new Vector3(0,0,1);}
            else if(a.x>=a.z){t=new Vector3(0,0,n.x>=0?-1:1);b=new Vector3(0,1,0);}
            else {t=new Vector3(n.z>=0?1:-1,0,0);b=new Vector3(0,1,0);}
            return new Vector2(Vector3.Dot(p,t),Vector3.Dot(p,b));
        }
        public void BindMask(Material mask)
        {
            mask.SetTexture("_BaseMap", Texture != null ? Texture : Texture2D.whiteTexture);
            mask.SetTextureScale("_BaseMap", Scale); mask.SetTextureOffset("_BaseMap", Offset);
            mask.SetFloat("_Alpha", Alpha); mask.SetFloat("_Cutoff", Cutoff);
            mask.SetFloat("_BoxMap", BoxMap ? 1 : 0); mask.SetFloat("_SampleTextureAlpha", SampleTextureAlpha ? 1 : 0);
            mask.SetFloat("_NeedsAlpha", NeedsAlpha ? 1 : 0); mask.SetFloat("_Cull", Cull);
        }
    }
}
