using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    public enum HazardKind { Fire, SuspiciousItem, Earthquake, Collapse, DoorTrap }

    /// <summary>Something people can notice and react to. Owned and advanced by the incident director.</summary>
    public abstract class Hazard
    {
        public string Id { get; protected set; }
        public HazardKind Kind { get; protected set; }
        public Vector3 Position { get; protected set; }
        public bool Active { get; protected set; } = true;
        public float StartedAt { get; protected set; }
        /// <summary>Place name the station uses for this spot (for radio reports and JEV context).</summary>
        public string Where { get; set; } = "";
        /// <summary>Short Korean name: 화재, 의심 물체, 지진.</summary>
        public abstract string Label { get; }
        /// <summary>Distance at which a passenger with line of sight can notice it now.</summary>
        public abstract float NoticeRadius { get; }
        /// <summary>Distance inside which staying is dangerous now (reflex retreat).</summary>
        public abstract float DangerRadius { get; }
        /// <summary>Distance escape routes should keep from it.</summary>
        public virtual float Clearance => DangerRadius + 4;
        /// <summary>Short Korean description of what an onlooker can see now. Never hidden truth.</summary>
        public abstract string Visible { get; }
        /// <summary>Whether noticing needs line of sight (smoke/items) or is felt everywhere (shaking).</summary>
        public virtual bool NeedsSight => true;
        /// <summary>Chance per perception tick (about two a second) that someone in range registers it as wrong.</summary>
        public virtual float NoticeChance => 1f;
        public virtual void Tick(float deltaSeconds) { }
        public virtual void End() { Active = false; }
    }

    /// <summary>Registry the crowd reads every perception tick.</summary>
    public static class HazardRegistry
    {
        private static readonly List<Hazard> active = new List<Hazard>();
        private static readonly RaycastHit[] hits = new RaycastHit[12];
        public static IReadOnlyList<Hazard> Active => active;
        public static void Add(Hazard hazard) { if (!active.Contains(hazard)) active.Add(hazard); }
        public static void Remove(Hazard hazard) => active.Remove(hazard);
        public static void Clear() => active.Clear();

        public static bool CanSee(Vector3 eye, Hazard hazard)
        {
            if (!hazard.NeedsSight) return true;
            var target = hazard.Position + Vector3.up * (hazard is FireHazard fire ? .6f + fire.Intensity * 2f : .35f);
            var direction = target - eye;
            float distance = direction.magnitude;
            if (distance < .5f) return true;
            int count = Physics.RaycastNonAlloc(eye, direction / distance, hits, distance - .3f, ~0, QueryTriggerInteraction.Ignore);
            // 사람 몸과 사건 물체 자신은 가림으로 보지 않는다. 벽·기둥·점포 칸막이만 시야를 막는다.
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider.GetComponentInParent<PersonBody>() != null || collider.GetComponentInParent<HazardMarker>() != null) continue;
                return false;
            }
            return true;
        }
    }

    /// <summary>Tags the scene objects that belong to a hazard (sight checks ignore them; the player can look at them).</summary>
    public sealed class HazardMarker : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public Hazard Hazard;
        public string Name = "";
        public System.Func<string> Prompt;
        public System.Func<FirstPersonResponder, string> Act;

        public string DisplayName => Name;
        public string InteractionPrompt => Prompt?.Invoke() ?? "";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            bool available = responder != null && !responder.IsPaused && Act != null && !string.IsNullOrEmpty(InteractionPrompt);
            reason = available ? null : "";
            return available;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out _)) return false;
            feedback = Act(responder);
            return true;
        }
    }

    /// <summary>
    /// A fire that starts small and grows unless suppressed. Beyond intensity 1 a hand extinguisher barely helps
    /// (the fire has left its first stage). Growth rates are game-compressed, not fire-engineering figures.
    /// </summary>
    public sealed class FireHazard : Hazard
    {
        public const float Growth = .0035f;
        public float Intensity { get; private set; }
        public float SmokeRadius { get; private set; }
        public string Source { get; }
        public bool Extinguished { get; private set; }
        public float SuppressedSeconds { get; private set; }
        public GameObject View { get; }

        private readonly ParticleSystem flames, smoke;
        private readonly Light glow;
        private readonly NavMeshObstacle obstacle;
        private float smokeLinger = 45;

        public override string Label => "화재";
        public override float NoticeRadius => Extinguished ? 6 : 8 + 32 * Mathf.Min(Intensity, 1.1f);
        public override float DangerRadius => Extinguished ? 0 : 1.2f + 3.2f * Intensity;
        public override string Visible => Extinguished ? "꺼진 불 자리에서 연기가 남아 있음" :
            Intensity < .3f ? "가방에서 흰 연기가 새어 나옴" :
            Intensity < .7f ? "가방에서 불꽃과 짙은 회색 연기가 오름" :
            Intensity < 1f ? "불꽃이 의자로 옮겨붙고 검은 연기가 퍼짐" : "불길이 커져 천장 쪽으로 검은 연기가 번짐";

        public FireHazard(string id, Vector3 position, string source, float intensity, EmergencyArt art, Transform parent)
        {
            Id = id;
            Kind = HazardKind.Fire;
            Position = position;
            Source = source;
            Intensity = intensity;
            StartedAt = Time.time;
            View = new GameObject("화재 · " + source);
            View.transform.SetParent(parent, false);
            View.transform.position = position;
            flames = Particles.Flames(View.transform, art.Flame);
            smoke = Particles.Smoke(View.transform, art.Smoke);
            var light = new GameObject("불빛", typeof(Light));
            light.transform.SetParent(View.transform, false);
            light.transform.localPosition = new Vector3(0, .6f, 0);
            glow = light.GetComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, .55f, .2f);
            glow.shadows = LightShadows.None;
            obstacle = View.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            var collider = View.AddComponent<SphereCollider>();
            collider.isTrigger = false;
            collider.radius = .35f;
            collider.center = new Vector3(0, .3f, 0);
            var marker = View.AddComponent<HazardMarker>();
            marker.Hazard = this;
            marker.Name = "불";
            Apply();
        }

        public bool InSmoke(Vector3 position)
        {
            if (SmokeRadius <= 0) return false;
            var d = position - Position;
            // 연기는 그 층에 퍼지고, 위층(뚫린 맞이방·계단)으로는 좁게만 올라간다. 아래층으로는 내려가지 않는다.
            if (d.y < -2f || d.y > 6.5f) return false;
            float radius = d.y > 3.5f ? SmokeRadius * .6f : SmokeRadius;
            d.y = 0;
            return d.sqrMagnitude < radius * radius * .45f;
        }

        public override void Tick(float deltaSeconds)
        {
            if (Extinguished)
            {
                smokeLinger -= deltaSeconds;
                SmokeRadius = Mathf.MoveTowards(SmokeRadius, 0, deltaSeconds * .25f);
                if (smokeLinger <= 0 && View != null) { var e = smoke.emission; e.rateOverTime = 0; }
                ApplyVisuals();
                return;
            }
            if (SuppressedSeconds <= 0) Intensity = Mathf.Min(1.4f, Intensity + Growth * deltaSeconds);
            SuppressedSeconds = Mathf.Max(0, SuppressedSeconds - deltaSeconds);
            float targetRadius = 3 + 24 * Intensity;
            SmokeRadius = Mathf.MoveTowards(SmokeRadius, targetRadius, deltaSeconds * (targetRadius > SmokeRadius ? .35f : .15f));
            Apply();
        }

        /// <summary>
        /// Applies extinguishing agent. <paramref name="quality"/> is aim quality 0..1 (base of the flame, distance). A hand
        /// extinguisher barely helps beyond the first stage (intensity 1); an indoor hydrant's water stream still does
        /// (<paramref name="hose"/>). Rates are game tuning, not fire-engineering figures.
        /// </summary>
        public void Suppress(float quality, float deltaSeconds, bool hose = false)
        {
            if (Extinguished) return;
            SuppressedSeconds = .6f;
            // 초기 단계(1 이하)에서만 소화기가 제대로 듣는다. 옥내소화전 방수는 그 뒤에도 듣는다.
            float effect = hose ? (Intensity > 1f ? .7f : 1.6f) : Intensity > 1f ? .15f : 1f;
            Intensity -= .08f * quality * effect * deltaSeconds;
            if (Intensity <= 0) Extinguish();
            else Apply();
        }

        public void Grow(float amount)
        {
            if (Extinguished) return;
            Intensity = Mathf.Min(1.4f, Intensity + amount);
            Apply();
        }

        /// <summary>Smoke drifts farther than the fire's size alone would push it (air movement in the hall).</summary>
        public void SpreadSmoke(float metres) { if (!Extinguished) SmokeRadius += metres; }

        public void Extinguish()
        {
            Intensity = 0;
            Extinguished = true;
            Active = false;
            var e = flames.emission; e.rateOverTime = 0;
            glow.enabled = false;
            obstacle.enabled = false;
            ApplyVisuals();
        }

        public override void End()
        {
            base.End();
            if (View != null) Object.Destroy(View);
        }

        private void Apply()
        {
            // 길은 불꽃 자리만 깎는다(위험 반경 전체를 깎으면 대합실 의자 사이 통로가 끊겨 옆 사람이 갇힌다).
            obstacle.radius = .3f + .8f * Intensity;
            obstacle.height = 2f;
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            if (!Extinguished)
            {
                var fe = flames.emission; fe.rateOverTime = 18 + 90 * Intensity;
                var fm = flames.main; fm.startSize = new ParticleSystem.MinMaxCurve(.2f + .5f * Intensity, .4f + 1.1f * Intensity);
                var fs = flames.shape; fs.radius = .12f + .9f * Intensity;
                glow.intensity = (2 + 10 * Intensity) * (0.85f + .15f * Mathf.PerlinNoise(Time.time * 7, 0));
                glow.range = 4 + 10 * Intensity;
            }
            var se = smoke.emission; se.rateOverTime = Extinguished ? (smokeLinger > 0 ? 6 : 0) : 8 + 55 * Intensity;
            var sm = smoke.main; sm.startSize = new ParticleSystem.MinMaxCurve(.8f + 1.2f * Intensity, 1.6f + 3.2f * Intensity);
            var ss = smoke.shape; ss.radius = .2f + .8f * Intensity;
            var sc = smoke.main; sc.startColor = Extinguished ? new Color(.75f, .75f, .75f, .35f) :
                Color.Lerp(new Color(.82f, .82f, .82f, .8f), new Color(.12f, .12f, .12f, .9f), Mathf.Clamp01(Intensity * 1.3f));
        }
    }

    /// <summary>An unattended suitcase. It becomes a hazard only once its owner has walked away from it.</summary>
    public sealed class SuspiciousItemHazard : Hazard
    {
        public GameObject View { get; }
        public Passenger Owner { get; }
        public bool Unattended { get; private set; }
        public bool Touched { get; set; }
        public float UnattendedAt { get; private set; }

        public override string Label => "의심 물체";
        // 주인 없는 가방은 가까이 있는 사람이, 한동안 그대로 놓인 뒤에야 이상하다고 여긴다.
        public override float NoticeRadius => Unattended ? 5 : 0;
        public override float NoticeChance => Time.time - UnattendedAt < 30 ? .015f : .05f;
        public override float DangerRadius => 0;
        public override float Clearance => 20;
        public override string Visible => "주인 없이 놓인 검은 여행가방" + (Time.time - UnattendedAt > 60 ? " (1분 넘게 그대로)" : "");

        public SuspiciousItemHazard(string id, Vector3 position, Quaternion rotation, Passenger owner, EmergencyArt art, Transform parent)
        {
            Id = id;
            Kind = HazardKind.SuspiciousItem;
            Position = position;
            Owner = owner;
            Active = false;
            StartedAt = Time.time;
            View = Props.Suitcase(parent, position, rotation, art);
            var marker = View.AddComponent<HazardMarker>();
            marker.Hazard = this;
            marker.Name = "주인 없는 여행가방";
        }

        public void MarkUnattended()
        {
            if (Unattended) return;
            Unattended = true;
            Active = true;
            UnattendedAt = Time.time;
        }
    }

    /// <summary>Ground shaking felt by everyone in the hall for a while, then aftershock risk.</summary>
    public sealed class EarthquakeHazard : Hazard
    {
        public float ShakeUntil { get; private set; }
        public float Strength { get; private set; }
        public bool Shaking => Time.time < ShakeUntil;
        public int Aftershocks { get; private set; }

        public override string Label => "지진";
        public override float NoticeRadius => float.PositiveInfinity;
        public override float DangerRadius => 0;
        public override bool NeedsSight => false;
        public override string Visible => Shaking ? "역사 전체가 크게 흔들리고 천장 구조물이 삐걱거림" : "흔들림이 멈춤";

        public EarthquakeHazard(string id, Vector3 hallCentre, float seconds, float strength)
        {
            Id = id;
            Kind = HazardKind.Earthquake;
            Position = hallCentre;
            StartedAt = Time.time;
            ShakeUntil = Time.time + seconds;
            Strength = strength;
        }

        public void Aftershock(float seconds, float strength)
        {
            Aftershocks++;
            ShakeUntil = Time.time + seconds;
            Strength = strength;
        }
    }

    /// <summary>
    /// A person who collapsed (medical) or fell (escalator, stairs, platform) and cannot get up. Bystanders who see it
    /// may help, report or watch; the danger is to the person, not to those around.
    /// </summary>
    public sealed class CollapseHazard : Hazard
    {
        public Passenger Person { get; }
        public string Cause { get; }
        /// <summary>Collapse level 0 (dizzy, conscious) .. 4 (unconscious, not breathing normally); -1 for a fall.</summary>
        public int Level { get; set; } = -1;
        public bool NotBreathingNormally => Level >= 4;
        /// <summary>When an AED was set down beside the person (-1: not yet). Logged only (JEV 011).</summary>
        public float AedAt { get; set; } = -1;
        private readonly string label, visible;

        public override string Label => label;
        public override float NoticeRadius => 14;
        public override float DangerRadius => 0;
        public override float Clearance => 0;
        public override string Visible => visible;

        public CollapseHazard(string id, Passenger person, string label, string cause, string visible)
        {
            Id = id;
            Kind = HazardKind.Collapse;
            Person = person;
            Cause = cause;
            this.label = label;
            this.visible = visible;
            Position = person.transform.position;
            StartedAt = Time.time;
        }

        public override void Tick(float deltaSeconds)
        {
            if (Person == null) { End(); return; }
            Position = Person.transform.position;
        }
    }

    /// <summary>A closing KTX door caught a passenger's bag or arm; the set cannot leave until someone frees it.</summary>
    public sealed class DoorTrapHazard : Hazard
    {
        public TrainService.Car Car { get; }
        public Passenger Person { get; }
        public bool Freed { get; private set; }
        /// <summary>An arm, not a belonging, is caught: the passenger is hurt once freed.</summary>
        public bool Injures { get; }
        private readonly string what;

        public override string Label => "출입문 끼임";
        public override float NoticeRadius => 12;
        public override float DangerRadius => 0;
        public override float Clearance => 0;
        public override string Visible => Freed ? "출입문에 끼였던 " + what + "을 빼냈음" : "KTX " + Car.Label + " 출입문에 " + what + "이 끼여 문이 다 닫히지 않음";

        public DoorTrapHazard(string id, TrainService.Car car, Passenger person, string what, bool injures, Vector3 position)
        {
            Id = id;
            Kind = HazardKind.DoorTrap;
            Car = car;
            Person = person;
            Injures = injures;
            this.what = what;
            Position = position;
            StartedAt = Time.time;
        }

        public void Free()
        {
            Freed = true;
            End();
        }
    }

    /// <summary>Runtime particle systems for fire, smoke and extinguishing powder (URP particle materials from EmergencyArt).</summary>
    public static class Particles
    {
        public static ParticleSystem Flames(Transform parent, Material material)
        {
            var ps = Make(parent, "불꽃", material, new Vector3(0, .1f, 0));
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 1.3f);
            main.maxParticles = 600;
            main.gravityModifier = -.12f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8;
            shape.rotation = new Vector3(-90, 0, 0);
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, .85f, .45f), 0), new GradientColorKey(new Color(1f, .45f, .12f), .45f), new GradientColorKey(new Color(.6f, .12f, .05f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.95f, .12f), new GradientAlphaKey(.6f, .6f), new GradientAlphaKey(0, 1) });
            colour.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .5f), new Keyframe(.35f, 1), new Keyframe(1, .15f)));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .35f;
            noise.frequency = 1.4f;
            ps.Play();
            return ps;
        }

        public static ParticleSystem Smoke(Transform parent, Material material)
        {
            var ps = Make(parent, "연기", material, new Vector3(0, .5f, 0));
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 1.4f);
            main.maxParticles = 900;
            main.gravityModifier = 0;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22;
            shape.rotation = new Vector3(-90, 0, 0);
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.8f, .12f), new GradientAlphaKey(.5f, .7f), new GradientAlphaKey(0, 1) });
            colour.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .4f), new Keyframe(1, 2.6f)));
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.radial = new ParticleSystem.MinMaxCurve(.3f, .7f);
            // 뜨거운 연기는 오르다 식으며 느려지고 옆으로 퍼진다. 제한 없이 오르면 지붕을 뚫고 사라진다.
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 1.2f), new Keyframe(.5f, .5f), new Keyframe(1, .25f)));
            limit.dampen = .12f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .4f;
            noise.frequency = .35f;
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-.4f, .4f);
            ps.Play();
            return ps;
        }

        public static ParticleSystem Powder(Transform parent, Material material)
        {
            var ps = Make(parent, "소화 약제", material, Vector3.zero);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.5f, .9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.15f, .35f);
            main.startColor = new Color(.95f, .95f, .92f, .55f);
            main.maxParticles = 500;
            main.gravityModifier = .15f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7;
            shape.radius = .02f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .3f), new Keyframe(1, 3.2f)));
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(.9f, 0), new GradientAlphaKey(.4f, .6f), new GradientAlphaKey(0, 1) });
            colour.color = gradient;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            return ps;
        }

        /// <summary>Water stream of an indoor hydrant nozzle: faster, heavier and narrower than extinguisher powder.</summary>
        public static ParticleSystem Water(Transform parent, Material material)
        {
            var ps = Powder(parent, material);
            ps.gameObject.name = "방수";
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(11f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(.06f, .14f);
            main.startColor = new Color(.78f, .88f, 1f, .45f);
            main.gravityModifier = .9f;
            var shape = ps.shape;
            shape.angle = 3;
            var size = ps.sizeOverLifetime;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .5f), new Keyframe(1, 2.4f)));
            return ps;
        }

        private static ParticleSystem Make(Transform parent, string name, Material material, Vector3 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            return ps;
        }
    }

    /// <summary>Small props: real models and baked meshes from <see cref="EmergencyArt"/>, placed at runtime.</summary>
    public static class Props
    {
        /// <summary>
        /// A carry-on hard-shell spinner (<see cref="EmergencyArt.SuitcaseModel"/>, 36 × 24 × 60 cm) standing on
        /// <paramref name="floor"/>, with a box collider round it so it can be looked at and inspected.
        /// </summary>
        public static GameObject Suitcase(Transform parent, Vector3 floor, Quaternion rotation, EmergencyArt art)
        {
            var root = Object.Instantiate(art.SuitcaseModel, floor, rotation, parent);
            root.name = "여행가방";
            var bounds = LocalBounds(root.transform);
            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
            return root;
        }

        /// <summary>Bounds of every mesh under <paramref name="root"/>, in the root's own space.</summary>
        public static Bounds LocalBounds(Transform root)
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = toRoot.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { bounds = new Bounds(corner, Vector3.zero); any = true; }
                    else bounds.Encapsulate(corner);
                }
            }
            return bounds;
        }

        /// <summary>A retractable-belt stanchion (<see cref="EmergencyArt.Stanchion"/>: steel post on a weighted base, black belt head) standing on <paramref name="floor"/>.</summary>
        public static GameObject Stanchion(Transform parent, Vector3 floor, EmergencyArt art)
        {
            var go = new GameObject("벨트 차단봉");
            go.transform.SetParent(parent, false);
            go.transform.position = floor;
            go.AddComponent<MeshFilter>().sharedMesh = art.Stanchion;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { art.CordonPost, art.CordonHead };
            return go;
        }

        /// <summary>Length of belt (m) one repeat of the printed belt texture covers (1024×64 px, so the print keeps its shape on a 5 cm belt).</summary>
        public const float BeltRepeat = .8f;

        /// <summary>
        /// A flat strap between two points: the cordon belt (5 cm high, printed on both faces so the lettering reads left to right
        /// from either side, the print repeated along its length) or, narrower, a bag strap. <paramref name="repeat"/> is the
        /// length (m) one texture repeat covers.
        /// </summary>
        public static GameObject Belt(Transform parent, Vector3 a, Vector3 b, Material material, float height = .05f, float repeat = BeltRepeat, string name = "통제 벨트")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation((a + b) * .5f, Quaternion.LookRotation(b - a));
            float half = Vector3.Distance(a, b) * .5f, h = height * .5f, u = half * 2 / repeat;
            var mesh = new Mesh { name = name };
            // 왼쪽 면(-x)에서 보면 오른쪽이 -z, 오른쪽 면(+x)에서 보면 +z 다. 두 면 모두 시계 방향으로 감고 글자가 바로 읽히게 u 를 준다.
            mesh.vertices = new[]
            {
                new Vector3(0, -h, half), new Vector3(0, h, half), new Vector3(0, h, -half), new Vector3(0, -h, -half),
                new Vector3(0, -h, -half), new Vector3(0, h, -half), new Vector3(0, h, half), new Vector3(0, -h, half),
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(u, 1), new Vector2(u, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(u, 1), new Vector2(u, 0) };
            mesh.normals = new[] { Vector3.left, Vector3.left, Vector3.left, Vector3.left, Vector3.right, Vector3.right, Vector3.right, Vector3.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A prop from a mesh asset at <paramref name="parent"/>'s origin (the mesh carries its own size and pivot).</summary>
        public static GameObject Model(Transform parent, string name, Mesh mesh, params Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }
    }
}
