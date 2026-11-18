using UnityEngine;

namespace ChooGuard.App.Fps.StationFireTutorial
{
    /// <summary>Visual/aural effects follow equipment and hazard state; none of these effects complete a lesson.</summary>
    public sealed class StationFireVisuals : MonoBehaviour
    {
        public ParticleSystem Flame, Smoke, Powder;
        public Transform HoseOutlet, NozzleRear, NozzleExit, StowedHose;
        public LineRenderer Hose;
        public AudioSource FireSound, SpraySound;
        public Material Paper;
        public Material CharSurface;
        public float Agent;
        float heatExposure=.15f;
        public void Project(float fire, float flow, bool nozzleOut, bool paused)
        {
            SetEmission(Flame, paused ? 0 : Mathf.Max(0, fire) * 32);
            SetEmission(Smoke, paused ? 0 : fire > .01f ? 6 + fire * 14 : 2);
            SetEmission(Powder, !paused && nozzleOut ? Mathf.Max(0, flow) * 520 : 0);
            if (StowedHose != null) StowedHose.gameObject.SetActive(!nozzleOut);
            if (FireSound != null) FireSound.volume = Mathf.Clamp01(fire) * .4f;
            if(Paper!=null)Paper.color=Color.Lerp(new Color(.085f,.06f,.035f),new Color(.7f,.42f,.18f),Mathf.Clamp01(fire/.7f));
            if(CharSurface!=null)
            {
                if(!paused)heatExposure=Mathf.Min(.85f,heatExposure+Time.deltaTime*fire*.008f);
                CharSurface.SetFloat("_CharStrength",heatExposure);CharSurface.SetFloat("_Agent",Agent);
            }
            if (SpraySound != null)
            {
                SpraySound.volume = !paused && nozzleOut ? Mathf.Clamp01(flow) * .45f : 0;
                if (SpraySound.volume > 0 && !SpraySound.isPlaying) SpraySound.Play();
                if (SpraySound.volume <= 0 && SpraySound.isPlaying) SpraySound.Stop();
            }
            if (Hose == null) return;
            Hose.enabled = nozzleOut;
            if (!nozzleOut || HoseOutlet == null || NozzleRear == null) return;
            const int points = 16; Hose.positionCount = points;
            var a = HoseOutlet.position; var b = NozzleRear.position;
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                Hose.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * (.18f * Mathf.Sin(t * Mathf.PI)));
            }
        }
        static void SetEmission(ParticleSystem effect, float rate)
        {
            if (effect == null) return;
            var emission = effect.emission; emission.rateOverTime = rate;
            if (rate > 0 && !effect.isPlaying) effect.Play();
            else if (rate <= 0 && effect.isPlaying) effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        public static ParticleSystem Particles(Transform parent, string name, Color color, float size, float lifetime, float speed, float cone)
        {
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            var scale=parent.lossyScale;
            root.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
            var particles = root.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.startColor = color; main.startSize = size; main.startLifetime = lifetime;
            main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            main.startSpeed = speed; main.maxParticles = 1600; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission; emission.rateOverTime = 0;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = cone; shape.radius = .025f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            var material = new Material(shader); material.color = Color.white;
            var fade=particles.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(.15f,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.65f,.6f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var soft = new Texture2D(32,32,TextureFormat.RGBA32,false); soft.wrapMode=TextureWrapMode.Clamp;
            var pixels=new Color[32*32];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {float radius=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-radius),1.7f));}
            soft.SetPixels(pixels);soft.Apply();material.SetTexture("_BaseMap",soft);material.SetTexture("_MainTex",soft);
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);
            material.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;renderer.material=material;
            return particles;
        }
    }
}
